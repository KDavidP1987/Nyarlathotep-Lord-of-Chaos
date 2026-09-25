using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// Mod-initiated chat (foundation D13, D30, D31; Design › Startup: sixth in Core.TryInitialize). Wave warnings, event
/// banners and the daily banner each need their [Announcements] switch; `.nyar announce` needs only the admin. Every
/// server-wide line goes through Logic/AnnounceQueue, one a second from a queue of 20, sent by the scheduler's
/// announcements phase to each connected user. The login hook (Patches/UserConnectPatch) sends an admin the private
/// degraded notice. The clan-only target of siege messages is left to the sieges child; login stats and player shares
/// to the stats child (the switches are bound now and the limits are Logic/ShareLimiter and Logic/LoginGate).
/// </summary>
internal static class Announcer
{
    static readonly System.Random _random = new();
    static AnnounceQueue _queue = new(_ => { });
    static WarningClock _warnings = new(Limits.DefaultWarningOffsets);
    static readonly LoginGate _logins = new();
    static Broadcaster _broadcaster = new(new GameUsers(), _ => { });
    static readonly FailureStreak _privateFaults = new();
    static readonly List<(Entity User, ulong PlatformId, DateTime DueUtc)> _notices = new();

    /// <summary>A private login line waits this long, so it reaches a client that has finished loading.</summary>
    static readonly TimeSpan NoticeDelay = TimeSpan.FromSeconds(10);

    internal static int Queued => _queue.Count;

    internal static void Initialize()
    {
        _queue = new AnnounceQueue(line => Core.Log.LogWarning($"[nyar] {line}"));
        _warnings = new WarningClock(Settings.WarningOffsets);
        _broadcaster = new Broadcaster(new GameUsers(), line => Core.Log.LogWarning($"[nyar] {line}"));
        var on = new[]
        {
            ("WaveWarnings", Settings.WaveWarnings.Value), ("EventBanners", Settings.EventBanners.Value),
            ("DailyBanner", Settings.DailyBanner.Value), ("LoginStats", Settings.LoginStats.Value), ("PlayerShare", Settings.PlayerShare.Value),
        }.Where(s => s.Item2).Select(s => s.Item1).ToList();
        Core.Log.LogInfo($"[nyar] announcements: {(on.Count == 0 ? "all off" : string.Join(", ", on))}; warning offsets {string.Join(",", Settings.WarningOffsets)} s; daily banner {Settings.DailyBannerTime:HH\\:mm}");
    }

    /// <summary>EventRuntime: an event started. With EventBanners on, its start banner is queued.</summary>
    internal static void EventStarted(RunningInstance instance)
    {
        if (!Settings.EventBanners.Value) return;
        var minutes = (int)Math.Ceiling((instance.EndsUtc - instance.StartedUtc).TotalMinutes);
        _queue.Enqueue(new QueuedLine(Messages.StartBanner(instance.Definition, minutes, _random.Next()), LineKind.Info, instance.Definition.Id), DateTime.UtcNow);
    }

    /// <summary>EventRuntime: an event ended (expired, stopped or cancelled). Its unsent warnings go; with EventBanners on,
    /// its end banner is queued.</summary>
    internal static void EventEnded(EventDefinition def)
    {
        _queue.DropWarnings(def.Id);
        if (!Settings.EventBanners.Value) return;
        _queue.Enqueue(new QueuedLine(Messages.EndBanner(def, _random.Next()), LineKind.Info, def.Id), DateTime.UtcNow);
    }

    /// <summary>The purge ends every event without banners; unsent warnings go.</summary>
    internal static void Purged() => _queue.DropWarnings();

    /// <summary>`.nyar announce &lt;text&gt;` (D14, D30): the admin's text, already checked by TextSink, is queued as an
    /// informational line whatever the switches say.</summary>
    [Mutating]
    internal static string AdminAnnounce(string text)
    {
        _queue.Enqueue(new QueuedLine(text, LineKind.Info), DateTime.UtcNow);
        return _queue.Count <= 1 ? "announced" : $"announced (queued behind {_queue.Count - 1})";
    }

    /// <summary>The scheduler's announcements phase: warnings due this second, the daily banner, then at most one line
    /// out.</summary>
    internal static void Tick(DateTime now)
    {
        SendDueNotices(now);
        QueueWarnings(now);
        if (Settings.DailyBanner.Value) QueueDailyBanner(now);
        if (_queue.Next(now) is not { } line) return;
        var reached = _broadcaster.SendToAll(TextSink.CutToBytes(line.Text, Wire.MaxBytes));
        if (Settings.VerboseLogging.Value) Core.Log.LogInfo($"[nyar] announced to {reached}: {line.Text}");
    }

    static void QueueWarnings(DateTime now)
    {
        var live = new List<string>();
        if (Settings.WaveWarnings.Value)
        {
            foreach (var active in EventRuntime.Engine.Active)
            {
                if (!active.Definition.Announce.Warnings || UpcomingWave.Of(active) is not { } next) continue;
                var key = WarningClock.Key(active.Id, active.Instance.StartedUtc, next.Wave);
                live.Add(key);
                var left = UpcomingWave.SecondsLeft(next.AtUtc, now);
                if (_warnings.Due(key, left) is null) continue;
                _queue.Enqueue(new QueuedLine(Messages.WaveWarning(active.Definition, next.Wave, left, _random.Next()),
                    LineKind.Warning, active.Id, next.AtUtc), now);
            }
        }
        _warnings.Keep(live);
    }

    static void QueueDailyBanner(DateTime now)
    {
        var state = Persistence.State.Document;
        var names = DailyBanner.Due(state, Settings.DailyBannerTime, now, TimeZoneInfo.Local, EventStore.Catalog.Current);
        if (names is null) return;
        Persistence.State.MarkDirty();
        if (names.Count == 0) { Core.Log.LogInfo("[nyar] daily banner: nothing scheduled today"); return; }
        _queue.Enqueue(new QueuedLine(Messages.DailyBannerText(names, _random.Next()), LineKind.Info), now);
        Core.Log.LogInfo($"[nyar] daily banner queued: {names.Count} events");
    }

    /// <summary>Patches/UserConnectPatch: a user connected. An admin (adminauth'd, or on the admin list) connecting
    /// while something is degraded gets one private line <see cref="NoticeDelay"/> later, not again on a reconnect
    /// within 60 s (D31). Nothing is queued when nothing is degraded at the connect (Codex 331bb3f F3).</summary>
    internal static void UserConnected(Entity userEntity)
    {
        if (!userEntity.Exists() || !userEntity.Has<User>()) return;   // a stale approved-user entry: nothing to greet
        var user = Core.EntityManager.GetComponentData<User>(userEntity);
        if (!_logins.ShouldGreet(user.PlatformId, DateTime.UtcNow)) return;
        if (!IsAdmin(user) || HealthMonitor.Degraded().Count == 0) return;
        _notices.Add((userEntity, user.PlatformId, DateTime.UtcNow + NoticeDelay));
    }

    // The degraded list is read when the notice leaves, so a notice due after everything recovered is not sent; the
    // recipient must still be the same player and still an admin (Codex 331bb3f F2).
    static void SendDueNotices(DateTime now)
    {
        for (var i = _notices.Count - 1; i >= 0; i--)
        {
            var (entity, platformId, due) = _notices[i];
            if (due > now) continue;
            _notices.RemoveAt(i);
            if (!entity.Exists() || !entity.Has<User>()) continue;
            var user = Core.EntityManager.GetComponentData<User>(entity);
            var degraded = HealthMonitor.Degraded();
            if (!user.IsConnected || user.PlatformId != platformId || !IsAdmin(user) || degraded.Count == 0) continue;
            SendPrivate(user, AdminLines.DegradedNotice(degraded));
            Core.Log.LogInfo($"[nyar] degraded notice sent to admin {TextSink.Name(user.CharacterName.ToString())}");
        }
    }

    static bool IsAdmin(User user)
    {
        if (user.IsAdmin) return true;
        var auth = Core.Server.GetExistingSystemManaged<AdminAuthSystem>();
        return auth is not null && auth._LocalAdminList.Contains(user.PlatformId);
    }

    static void SendPrivate(User user, string text)
    {
        try
        {
            var message = new FixedString512Bytes(TextSink.CutToBytes(text, Wire.MaxBytes));
            ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref message);
            _privateFaults.Ok();
        }
        catch (Exception ex)
        {
            if (_privateFaults.Fail()) Core.Log.LogWarning($"[nyar] private message failed: {ex.Message}");
        }
    }

    /// <summary>The connected users, read once per send (Logic/IUserSource).</summary>
    sealed class GameUsers : IUserSource
    {
        readonly Dictionary<ulong, User> _users = new();

        public IReadOnlyList<ulong> Connected()
        {
            _users.Clear();
            var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
            try
            {
                var users = query.ToComponentDataArray<User>(Allocator.Temp);
                try
                {
                    foreach (var u in users)
                        if (u.IsConnected) _users[u.PlatformId] = u;
                }
                finally { users.Dispose(); }
            }
            finally { query.Dispose(); }
            return _users.Keys.ToList();
        }

        public void Send(ulong platformId, string text)
        {
            var user = _users[platformId];
            var message = new FixedString512Bytes(text);
            ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref message);
        }
    }
}
