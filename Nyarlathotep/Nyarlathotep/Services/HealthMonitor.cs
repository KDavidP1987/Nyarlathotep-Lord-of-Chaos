using System.Collections.Generic;
using System.Linq;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// How an admin learns the mod is broken (foundation D31; Design › Failure &amp; observability; seventh in
/// Core.TryInitialize). Every 10 minutes the log gets "nyar health: &lt;n&gt; events, &lt;m&gt; tracked, degraded:
/// &lt;list&gt;"; the list (unavailable hooks and pillars whose event was cancelled after its faults) is also what
/// `.nyar status` shows admins and what the Announcer's login notice sends them.
/// </summary>
internal static class HealthMonitor
{
    static HealthClock _clock = new();

    internal static void Initialize() => _clock = new HealthClock();

    /// <summary>Every degraded part, "hook &lt;name&gt;" first.</summary>
    internal static IReadOnlyList<string> Degraded() =>
        TriggerBus.Hooks.Unavailable.Select(h => $"hook {h}").Concat(EventRuntime.Degraded).ToList();

    /// <summary>The scheduler's health phase.</summary>
    internal static void Tick(DateTime now)
    {
        if (!_clock.Due(now)) return;
        Core.Log.LogInfo($"[nyar] {HealthClock.Line(EventRuntime.Engine.Active.Count, SpawnTracker.Ledger.Tracked, Degraded())}");
    }
}
