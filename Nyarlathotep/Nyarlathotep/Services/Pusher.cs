using Nyarlathotep.Config;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// The Raphael push lines (raphael-api-core D5, D6, D12, D21; contract § Push events). One Logic/PushHub holds the
/// subscriptions, the queue of 50 and the push warning clock, all in memory. EventRuntime, WaveAction and EventStore
/// report the transitions, Patches/UserDisconnectPatch the disconnects, and the scheduler's push phase sends at most 5
/// lines a tick to the connected subscribers. Every entry point catches inside the hub, so a push fault never stops
/// the caller. Seventh in Core.TryInitialize, after the Announcer; the boot load's config-changed goes to the
/// placeholder hub that Initialize replaces, which has no subscriber.
/// </summary>
internal static class Pusher
{
    static PushHub _hub = new(new Announcer.GameUsers(), Limits.DefaultWarningOffsets, _ => { });

    internal static void Initialize()
    {
        _hub = new PushHub(new Announcer.GameUsers(), Settings.WarningOffsets, line => Core.Log.LogInfo($"[nyar] {line}"));
        Core.Log.LogInfo($"[nyar] push: ready (queue {PushQueue.Capacity}, {PushQueue.PerTick} lines a tick, at most {Subscriptions.Capacity} subscribers)");
    }

    /// <summary>`.nyar api sub on` for the caller's own SteamID (D5, D7).</summary>
    [Mutating]
    internal static string Subscribe(ulong platformId) => _hub.Subscribe(platformId);

    /// <summary>`.nyar api sub off` for the caller's own SteamID.</summary>
    [Mutating]
    internal static string Unsubscribe(ulong platformId) => _hub.Unsubscribe(platformId);

    internal static void Disconnected(ulong platformId) => _hub.Disconnected(platformId);

    internal static void EventStarted(RunningInstance instance) => _hub.EventStarted(instance);

    internal static void EventEnded(string id) => _hub.EventEnded(id);

    internal static void Wave(string id, int wave) => _hub.Wave(id, wave);

    internal static void Purged(int cooldownSeconds) => _hub.Purged(cooldownSeconds);

    internal static void ConfigChanged() => _hub.ConfigChanged();

    /// <summary>The scheduler's push phase, after the announcements.</summary>
    internal static void Tick(DateTime now) => _hub.Tick(now, EventRuntime.Engine.Active, Settings.WaveWarnings.Value);
}
