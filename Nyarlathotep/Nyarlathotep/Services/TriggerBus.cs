using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM;
using Unity.Entities;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// Where triggers become starts (foundation D29, D40; Design › Startup: fourth in Core.TryInitialize). The hooks are
/// registered through Logic/IHookRegistry, each on its own, so an unavailable one disables only the triggers that need
/// it (D9, D31). Each second the scheduler asks for the Schedule minute and the day/night edge; the DeathEvent patch
/// reports a dead V Blood. Logic/TriggerRouter picks the enabled definitions a trigger reaches, a trigger from the same
/// source within 5 s fires once (D6), and every start runs as System through the gateway (D10, D11).
/// <list type="bullet">
/// <item>Interval (automation D2): each tick polls Logic/IntervalClock over state.json's NextInterval, loaded once at init
/// so downtime is never replayed; every change marks state.json dirty, and a failed flush keeps the nexts in memory.</item>
/// <item>RegionEntered and FactionKills (automation D9, D11, D14, D15): Logic/PlayerTriggerFeed, fed by the 5 s scan of
/// the tick phase "player triggers" and by each DeathEvent row; each start carries its player as the focus (D13) and
/// hands its refusal line to PlayerTriggerGate's throttle. No row, id or position reaches a line (D19).</item>
/// </list>
/// </summary>
internal static class TriggerBus
{
    static HookSet _hooks = new(new Registry(), _ => { });
    static readonly TriggerDedupe _dedupe = new();
    static PhaseSampler _phases = NewSampler();
    static EntityQuery _dayNight;
    static readonly IRandom _rng = new SystemRandom(new Random());
    static readonly PlayerTriggerFeed _feed = new();
    static DateTime _nextScan;

    internal static HookSet Hooks => _hooks;

    /// <summary>The player-trigger health entries (automation D15), for HealthMonitor.</summary>
    internal static IReadOnlyList<string> Degraded => _feed.Health;

    internal static void Initialize()
    {
        _phases = NewSampler();
        _hooks = new HookSet(new Registry(), line => Core.Log.LogWarning($"[nyar] {line}"));
        _hooks.RegisterAll();
        var down = _hooks.Unavailable.Count == 0 ? "all hooks available" : $"unavailable: {string.Join(", ", _hooks.Unavailable)}";
        Core.Log.LogInfo($"[nyar] triggers: {down}");
        _feed.Clear();
        _nextScan = DateTime.MinValue;
        // automation D2: the stored nexts of startable Interval definitions, one at or before the boot redrawn from now
        var doc = Persistence.State.Document;
        var loaded = IntervalClock.Load(doc.NextInterval, EventStore.Catalog.Current, DateTime.UtcNow, _rng);
        if (doc.NextInterval is null || !SameNexts(doc.NextInterval, loaded)) Persistence.State.MarkDirty();
        doc.NextInterval = loaded;
    }

    static bool SameNexts(IReadOnlyDictionary<string, DateTime> a, IReadOnlyDictionary<string, DateTime> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    /// <summary>Each scheduler tick: Schedule definitions due this server-local minute (the occurrence is recorded in
    /// state.json whether or not the start is allowed, so it is never retried or replayed, D4), then the day/night edge.</summary>
    internal static void Poll(DateTime now)
    {
        var fired = Persistence.State.Document.LastFired;
        var due = TriggerTick.Collect(EventStore.Catalog.Current, now, TimeZoneInfo.Local,
            id => fired.TryGetValue(id, out var last) ? last.Occurrence : null, _hooks.IsAvailable(Hook.DayNight) ? _phases : null);
        foreach (var (def, occurrence) in due.Scheduled)
        {
            fired[def.Id] = new LastFired(occurrence, now);
            Persistence.State.MarkDirty();
            Fire(def, $"Schedule {occurrence}");
        }
        PollIntervals();

        if (due.Entered is not { } phase) return;
        Core.Log.LogInfo($"[nyar] trigger: GameTime {phase.ToString().ToLowerInvariant()} began");
        foreach (var def in due.PhaseStarts)
            if (_dedupe.ShouldFire($"{def.Id}|GameTime {phase}", now)) Fire(def, $"GameTime {phase.ToString().ToLowerInvariant()}");
    }

    /// <summary>The day and night read through Logic's PhaseSampler (event-library A5, D19): a read that throws logs once
    /// per failure streak and starts no GameTime event, and the tick's Schedule starts still fire.</summary>
    // DayNightCycle.TimeOfDay (A14): the plan named DayNightCycleExtensions.IsDay, which the pinned assemblies lack.
    static PhaseSampler NewSampler() => new(() => _dayNight.GetSingleton<DayNightCycle>().TimeOfDay == TimeOfDay.Day,
        line => Core.Log.LogError($"[nyar] {line}"));

    /// <summary>automation D2: every startable Interval definition is polled; a due one starts, and the clock draws its
    /// next on the first poll that sees it inactive again, whatever ended it or refused it.</summary>
    static void PollIntervals()
    {
        var doc = Persistence.State.Document;
        var nexts = doc.NextInterval ??= new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var utcNow = DateTime.UtcNow;
        var (due, changed) = IntervalClock.PollAll(EventStore.Catalog.Current, nexts, id => EventRuntime.Engine.Find(id) is not null, utcNow, _rng);
        if (changed) Persistence.State.MarkDirty();
        foreach (var def in due) Fire(def, nameof(TriggerType.Interval));
    }

    /// <summary>The next Interval start of <paramref name="id"/>, for `.nyar event info` (automation D16).</summary>
    internal static DateTime? NextInterval(string id) =>
        Persistence.State.Document.NextInterval is { } nexts && nexts.TryGetValue(id, out var next) ? next : null;

    /// <summary>The tick phase "player triggers" (automation D9): every 5 s one scan of the online players against the
    /// RegionEntered definitions, and the kill counters pruned.</summary>
    internal static void ScanPlayers(DateTime utcNow)
    {
        if (utcNow < _nextScan) return;
        _nextScan = utcNow.AddSeconds(TriggerLimits.ScanSeconds);
        var regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : (Func<float, float, string>)null;
        _feed.Scan(ReadPlayers, regionOf, EventStore.Catalog.Current, IsActive, utcNow,
            line => Core.Log.LogWarning($"[nyar] {line}"), FirePlayer, Verbose);
    }

    static IReadOnlyList<ScanRow> ReadPlayers() =>
        PlayerQuery.Read().Select(p => new ScanRow(p.PlatformId, p.X, p.Z, p.Alive)).ToList();

    /// <summary>True when a death is to be read for FactionKills (Patches/DeathEventPatch reads the victim's ledger
    /// membership only then).</summary>
    internal static bool WantsKills => PlayerTriggerFeed.WantsKills(EventStore.Catalog.Current, _hooks.AllowsTrigger);

    /// <summary>A death, for FactionKills (automation D11). <paramref name="victimOurs"/> gives the ledger membership read
    /// before SpawnTracker.Died forgot the unit, or throws when that read failed; a failing read skips this death.</summary>
    internal static void Died(Entity killer, Entity died, Func<bool> victimOurs)
    {
        var regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : (Func<float, float, string>)null;
        _feed.Died(needsPosition => KillReader.Read(killer, died, victimOurs, needsPosition), EventStore.Catalog.Current, regionOf,
            _hooks.AllowsTrigger, IsActive, DateTime.UtcNow, line => Core.Log.LogWarning($"[nyar] {line}"), FirePlayer, Verbose);
    }

    static bool IsActive(string id) => EventRuntime.Engine.Find(id) is not null;

    static void Verbose(string line)
    {
        if (Config.Settings.VerboseLogging.Value) Core.Log.LogInfo($"[nyar] {line}");
    }

    /// <summary>A player-action start (automation D13, D14): as System through the gateway, focused on the player, its
    /// refusal line throttled to one per definition per minute.</summary>
    static void FirePlayer(PlayerFire fire)
    {
        var def = fire.Definition;
        string refused = null;
        Gateway.Run(ActionKind.StartEvent, Actor.System,
            () => EventRuntime.StartEvent(def.Id, fire.Trigger, Actor.System, null, fire.At, fire.PlayerId, line => refused = line), def.Startable);
        if (refused is not null && _feed.Gate.Refused(def.Id, refused, DateTime.UtcNow) is { } line) Core.Log.LogInfo($"[nyar] {line}");
    }

    static readonly FailureStreak _killPositionFaults = new();

    /// <summary>A V Blood died (Patches/DeathEventPatch). <paramref name="prefab"/> is its prefab name and
    /// <paramref name="kill"/> its x/z, null when unreadable: then only Global definitions start, and "vblood kill:
    /// position unreadable" is logged once per streak when a scoped one was reached (regions D4).</summary>
    internal static void VBloodKilled(string prefab, Func<(float X, float Z)?> readKill)
    {
        Core.Log.LogInfo($"[nyar] trigger: VBloodKilled {prefab}");
        if (!_hooks.AllowsTrigger(TriggerType.VBloodKilled)) return;
        var now = DateTime.UtcNow;
        var set = EventStore.Catalog.Current;
        var kill = TriggerRouter.KillFor(set, prefab, readKill);                  // read only for a scoped definition (A38)
        if (TriggerRouter.UnreadableKillLogs(set, prefab, kill, _killPositionFaults)) Core.Log.LogWarning("[nyar] vblood kill: position unreadable");
        var regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : (Func<float, float, string>)null;
        foreach (var def in TriggerRouter.VBloodKilled(set, prefab, kill, regionOf))
            if (_dedupe.ShouldFire($"{def.Id}|VBloodKilled {prefab}", now))
                Fire(def, $"VBloodKilled {prefab}", def.Trigger.Scope.IsGlobal ? null : kill);
    }

    static void Fire(EventDefinition def, string trigger, (float X, float Z)? kill = null) =>
        Gateway.Run(ActionKind.StartEvent, Actor.System, () => EventRuntime.StartEvent(def.Id, trigger, Actor.System, null, kill), def.Startable);

    /// <summary>A hook is available when what it attaches to exists: the DeathEvent patch applied, the DayNightCycle
    /// singleton present, the ServerBootstrapSystem connect and disconnect patches (Patches/UserConnectPatch, UserDisconnectPatch) applied. In a Debug build,
    /// Debug.FaultInjection = hook:&lt;name&gt; makes that hook report unavailable (D31).</summary>
    sealed class Registry : IHookRegistry
    {
        public void Register(Hook hook)
        {
#if DEBUG
            var fault = Config.Settings.FaultInjection?.Value ?? "";
            if (fault == $"hook:{hook}" || fault == $"hook:{SystemName(hook)}") throw new InvalidOperationException("Debug.FaultInjection");
#endif
            switch (hook)
            {
                case Hook.DeathEvent:
                    if (!Plugin.Harmony.GetPatchedMethods().Any(m => m.DeclaringType == typeof(DeathEventListenerSystem)))
                        throw new InvalidOperationException("DeathEventListenerSystem.OnUpdate is not patched");
                    break;
                case Hook.DayNight:
                    _dayNight = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<DayNightCycle>()));
                    if (_dayNight.CalculateEntityCount() != 1) throw new InvalidOperationException("DayNightCycle singleton not found");
                    break;
                case Hook.UserConnect:
                    RequirePatched(nameof(ServerBootstrapSystem.OnUserConnected));
                    break;
                case Hook.UserDisconnect:
                    RequirePatched(nameof(ServerBootstrapSystem.OnUserDisconnected));
                    break;
            }
        }

        // Each login hook checks its own method, so one applied patch never reports the other available.
        static void RequirePatched(string method)
        {
            if (!Plugin.Harmony.GetPatchedMethods().Any(m => m.DeclaringType == typeof(ServerBootstrapSystem) && m.Name == method))
                throw new InvalidOperationException($"ServerBootstrapSystem.{method} is not patched");
        }

        static string SystemName(Hook hook) => hook switch
        {
            Hook.DeathEvent => nameof(DeathEventListenerSystem),
            Hook.DayNight => nameof(DayNightCycle),
            _ => nameof(ServerBootstrapSystem),
        };
    }
}

/// <summary>The kill facts of one death (automation D11), read-only, the way Bloodcraft's ValidateSource and XPRising's
/// DeathHook resolve a killer: the killer itself when it is a player character, else the player character its EntityOwner
/// names, or the player that owner follows (a familiar's summon). The victim's position is read only when a scoped
/// FactionKills definition needs it. A throw reaches PlayerTriggerFeed's guard.</summary>
internal static class KillReader
{
    internal static KillFacts Read(Entity killer, Entity died, Func<bool> victimOurs, bool needsPosition)
    {
        var ours = victimOurs();
        var killerPlayer = PlatformOf(killer);
        string ownerPlayer = null;
        if (killerPlayer is null && killer.TryGetComponent<EntityOwner>(out var owner))
            ownerPlayer = PlatformOf(owner.Owner) ?? (owner.Owner.TryGetComponent<Follower>(out var follows) ? PlatformOf(follows.Followed._Value) : null);
        float? x = null, z = null;
        if (needsPosition && died.TryGetComponent<Translation>(out var t)) (x, z) = (t.Value.x, t.Value.z);
        var faction = died.TryGetComponent<FactionReference>(out var f) ? f.FactionGuid._Value.GetPrefabName() : null;
        return new KillFacts(killerPlayer, ownerPlayer, died.Has<PlayerCharacter>(), died.Has<Minion>(), ours, faction, x, z,
            killer == died);
    }

    /// <summary>The platform id of a player character, or null for anything else.</summary>
    static string PlatformOf(Entity e) =>
        e.Has<PlayerCharacter>() && e.GetSteamId() is var id and not 0 ? id.ToString(CultureInfo.InvariantCulture) : null;
}
