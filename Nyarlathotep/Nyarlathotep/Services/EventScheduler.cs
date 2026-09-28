using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using UnityEngine;

namespace Nyarlathotep.Services;

/// <summary>
/// The one-second tick (foundation D24, D25; Design › Startup: last in Core.TryInitialize, replacing step 4's temporary
/// tick). Each tick runs its phases in order: the spawn and despawn queues, the triggers, the events, the announcement
/// queue, the push lines, the health line, then the state.json flush. Every phase has its own try/catch, logged once per failure streak, so a fault in one never stops the others,
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
            try { Tick(); }
            catch (Exception ex) { Core.Log.LogError($"[nyar] tick failed outside its phases: {ex.Message}"); }   // the coroutine must outlive any fault
        }
    }

    static void Tick()
    {
        var watch = Stopwatch.StartNew();
        var now = DateTime.UtcNow;
        _phases.Clear();
        Phase("spawn queues", SpawnTracker.Tick);
        Phase("triggers", () => TriggerBus.Tick(now));
        Phase("events", () => EventRuntime.Tick(now));
        Phase("announcements", () => Announcer.Tick(now));
        Phase("push", () => Pusher.Tick(now));
        Phase("health", () => HealthMonitor.Tick(now));
        Phase("state flush", () => Persistence.State.Flush());
        watch.Stop();
        var ms = watch.Elapsed.TotalMilliseconds;
        if (Settings.TimingLog.Value && _timer.Add(ms, now) is { } line)
            Core.Log.LogInfo($"[nyar] {line}");
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
