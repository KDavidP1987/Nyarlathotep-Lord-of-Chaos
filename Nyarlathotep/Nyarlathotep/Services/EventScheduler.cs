using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using UnityEngine;

namespace Nyarlathotep.Services;

/// <summary>
/// The one-second tick (foundation D24, D25; Design › Startup: last in Core.TryInitialize, replacing step 4's temporary
/// tick). Each tick runs its phases in order: the spawn and despawn queues, the triggers, the Interval clock and the
/// player triggers (every 5 s; automation D2, D9), the events, the Hunt seeds, the
/// announcement queue, the push lines, the health line, then the state.json flush. Every phase has its own try/catch, logged once per failure streak, so a fault in one never stops the others,
/// and an event's own fault is counted inside EventRuntime (D25). Nothing ticks before Core.IsReady (D28). With
/// Debug.TimingLog the tick's average and maximum are logged once a minute (D24); a tick of 250 ms or more logs one
/// warning naming its slowest phases, at most once a minute, whatever TimingLog says (event-library D36).
/// </summary>
internal static class EventScheduler
{
    static Coroutine _tick;
    static readonly TickTimer _timer = new();
    static readonly SlowTickLog _slow = new();
    static readonly List<(string Phase, double Ms)> _phases = new();
    static readonly Dictionary<string, FailureStreak> _faults = new();

    internal static void Start() => _tick = Core.StartCoroutine(Loop());

    /// <summary>Plugin.Unload: stop ticking before the final state flush.</summary>
    internal static void Stop()
    {
        if (_tick is not null) Core.StopCoroutine(_tick);
        _tick = null;
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            if (!Core.IsReady) continue;
            try { RunPhases(); }
            catch (Exception ex) { Core.Log.LogError($"[nyar] tick failed outside its phases: {ex.Message}"); }   // the coroutine must outlive any fault
        }
    }

    static void RunPhases()
    {
        var watch = Stopwatch.StartNew();
        var now = DateTime.UtcNow;
        _phases.Clear();
        WalkCheck.Budget.Reset();                                         // walkable-spawns D3, A3
        Phase("spawn queues", SpawnTracker.Tick);
        Phase("triggers", () => TriggerBus.Poll(now));
        Phase("interval", () => TriggerBus.PollIntervals(now));            // automation D2
        Phase("player triggers", () => TriggerBus.ScanPlayers(now));        // automation D9
        Phase("events", () => EventRuntime.Tick(now));
        Phase("hunt", () => HuntAction.Tick(now));                         // event-spawns D13, every 5 s; its own phase (A70)
        Phase("announcements", () => Announcer.Tick(now));
        Phase("push", () => Pusher.Tick(now));
        Phase("health", () => HealthMonitor.Beat(now));
        Phase("state flush", () => Persistence.State.Flush());
        watch.Stop();
        var ms = watch.Elapsed.TotalMilliseconds;
        var scans = TriggerBus.TakeScans();                                    // automation A8
        if (Settings.TimingLog.Value) _timer.Scanned(scans);                   // only while windows close (review F4)
        if (Settings.TimingLog.Value && _timer.Add(ms, now, _phases) is { } line)
        {
            Core.Log.LogInfo($"[nyar] hunt targets: {HuntAction.LastTargets}");    // event-spawns D24, before its window
            Core.Log.LogInfo($"[nyar] {line}");
            if (_timer.Slowest is { } slowest) Core.Log.LogInfo($"[nyar] {slowest}");   // A70
        }
        try
        {
            // the tick's end, not its start: a tick of a minute or more must not open the next quiet minute early
            if (_slow.Add(ms, _phases, DateTime.UtcNow) is { } slow) Core.Log.LogWarning($"[nyar] {slow}");
        }
        catch { /* a diagnostic never breaks the tick */ }
    }

    static void Phase(string name, Action work)
    {
        if (!_faults.TryGetValue(name, out var streak)) _faults[name] = streak = new FailureStreak();
        var start = Stopwatch.GetTimestamp();
        try
        {
            work();
            streak.Ok();
        }
        catch (Exception ex)
        {
            if (streak.Fail()) Core.Log.LogError($"[nyar] tick phase {name} failed: {ex.Message}; retrying every second");
        }
        _phases.Add((name, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency));
    }
}
