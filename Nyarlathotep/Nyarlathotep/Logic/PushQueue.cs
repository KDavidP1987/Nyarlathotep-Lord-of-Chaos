#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The push rules without the game (raphael-api-core D5, D6, D21; contract § Push events). Services/Pusher holds one
// PushHub. Logic/EventEngine and EventCatalog report each transition to it through IPushSink where the transition
// happens (A6); the disconnect hook and the scheduler tick call it through Pusher.

/// <summary>Where Logic/EventEngine and EventCatalog report the transitions a push announces (D6, A6): a start, an end
/// (expiry, stop or fault cancel), a wave queued, the purge, and a successful reload. <see cref="PushHub"/> is the
/// implementation; its entry points never throw.</summary>
public interface IPushSink
{
    void EventStarted(RunningInstance instance);
    void EventEnded(string id);
    void Wave(string id, int wave);
    void Purged(int cooldownSeconds);
    void ConfigChanged();
}

/// <summary>One queued push line. <see cref="EventId"/> ties a wave-warn to its event, so the event's end drops it.</summary>
public sealed record PushLine(string Text, string Type, string? EventId);

/// <summary>The `[NYAR:ev]` lines of the six push types. secs is the event's length for event-start, the time until the
/// wave for wave-warn, the purge cooldown for killswitch, and 0 for event-end, wave and config-changed (A5).</summary>
public static class PushLines
{
    public const string EventStartType = "event-start";
    public const string EventEndType = "event-end";
    public const string WaveType = "wave";
    public const string WaveWarnType = "wave-warn";
    public const string KillswitchType = "killswitch";
    public const string ConfigChangedType = "config-changed";

    public static PushLine EventStart(RunningInstance instance)
    {
        var secs = Math.Max(0, (int)Math.Ceiling((instance.EndsUtc - instance.StartedUtc).TotalSeconds));
        return new(Wire.Ev(EventStartType, instance.Definition.Id, secs), EventStartType, instance.Definition.Id);
    }

    public static PushLine EventEnd(string id) => new(Wire.Ev(EventEndType, id, 0), EventEndType, id);

    public static PushLine Wave(string id, int wave) => new(Wire.Ev(WaveType, id, 0, wave), WaveType, id);

    public static PushLine WaveWarn(string id, int wave, int secs) => new(Wire.Ev(WaveWarnType, id, secs, wave), WaveWarnType, id);

    public static PushLine Killswitch(int cooldownSeconds) => new(Wire.Ev(KillswitchType, "-", cooldownSeconds), KillswitchType, null);

    public static PushLine ConfigChanged() => new(Wire.Ev(ConfigChangedType, "-", 0), ConfigChangedType, null);

    /// <summary>The wave-warn lines due now under S-1, the same gates as the chat warning: WaveWarnings on, the
    /// definition's announce.warnings true, once per WarningOffsets offset per wave (Logic/WarningClock). Waves no longer
    /// upcoming are forgotten.</summary>
    public static List<PushLine> Warnings(IEnumerable<ActiveEvent> active, bool waveWarnings, WarningClock clock, DateTime utcNow)
    {
        var lines = new List<PushLine>();
        var live = new List<string>();
        if (waveWarnings)
        {
            foreach (var a in active)
            {
                if (!a.Definition.Announce.Warnings || UpcomingWave.Of(a) is not { } next) continue;
                var key = WarningClock.Key(a.Id, a.Instance.StartedUtc, next.Wave);
                live.Add(key);
                var left = UpcomingWave.SecondsLeft(next.AtUtc, utcNow);
                if (clock.Due(key, left) is null) continue;
                lines.Add(WaveWarn(a.Id, next.Wave, left));
            }
        }
        clock.Keep(live);
        return lines;
    }
}

/// <summary>The push queue: at most <see cref="Capacity"/> lines, the oldest dropped at overflow with one log line per
/// overflow streak (a streak ends when the queue has room again); a config-changed line that is already waiting absorbs a new one; at most <see cref="PerTick"/>
/// lines leave per tick.</summary>
public sealed class PushQueue(Action<string> log)
{
    public const int Capacity = 50;
    public const int PerTick = 5;

    readonly List<PushLine> _lines = [];
    bool _overflowing;

    public int Count => _lines.Count;

    public IReadOnlyList<PushLine> Lines => _lines;

    public void Enqueue(PushLine line)
    {
        if (line.Type == PushLines.ConfigChangedType && _lines.Exists(l => l.Type == PushLines.ConfigChangedType)) return;
        if (_lines.Count < Capacity) _overflowing = false;          // room again, by a send or a dropped warning
        if (_lines.Count >= Capacity)
        {
            _lines.RemoveAt(0);
            if (!_overflowing) log("push queue full, oldest dropped");
            _overflowing = true;
        }
        _lines.Add(line);
    }

    /// <summary>Drops the unsent wave-warn lines of <paramref name="eventId"/>, or of every event when null (a purge).</summary>
    public int DropWarnings(string? eventId = null) =>
        _lines.RemoveAll(l => l.Type == PushLines.WaveWarnType && (eventId is null || l.EventId == eventId));

    /// <summary>The next lines to send, at most <paramref name="max"/>, oldest first.</summary>
    public IReadOnlyList<PushLine> Take(int max = PerTick)
    {
        var n = Math.Min(max, _lines.Count);
        var taken = _lines.GetRange(0, n);
        _lines.RemoveRange(0, n);
        return taken;
    }
}

/// <summary>The subscriptions, the queue and the push warning clock behind Services/Pusher. Every entry point catches
/// and logs once per failure streak (per entry point), so a push fault never reaches the event tick that called it
/// (D21).</summary>
public sealed class PushHub(IUserSource users, IReadOnlyList<int> warningOffsets, Action<string> log) : IPushSink
{
    readonly WarningClock _warnings = new(warningOffsets);
    readonly Dictionary<string, FailureStreak> _faults = new(StringComparer.Ordinal);

    public Subscriptions Subscriptions { get; } = new(log);
    public PushQueue Queue { get; } = new(log);

    /// <summary>`sub on` for the caller's own id (through Gateway.Run in the service).</summary>
    public string Subscribe(ulong id) => Subscriptions.On(id, users);

    /// <summary>`sub off` for the caller's own id.</summary>
    public string Unsubscribe(ulong id) => Subscriptions.Off(id);

    public void Disconnected(ulong id) => Guard("disconnect", () => Subscriptions.Disconnected(id));

    public void EventStarted(RunningInstance instance) => Guard(PushLines.EventStartType, () => Queue.Enqueue(PushLines.EventStart(instance)));

    public void EventEnded(string id) => Guard(PushLines.EventEndType, () =>
    {
        Queue.DropWarnings(id);
        Queue.Enqueue(PushLines.EventEnd(id));
    });

    public void Wave(string id, int wave) => Guard(PushLines.WaveType, () => Queue.Enqueue(PushLines.Wave(id, wave)));

    public void Purged(int cooldownSeconds) => Guard(PushLines.KillswitchType, () =>
    {
        Queue.DropWarnings();
        Queue.Enqueue(PushLines.Killswitch(cooldownSeconds));
    });

    public void ConfigChanged() => Guard(PushLines.ConfigChangedType, () => Queue.Enqueue(PushLines.ConfigChanged()));

    /// <summary>The scheduler's push phase: the wave-warn lines due now, then at most <see cref="PushQueue.PerTick"/>
    /// lines, each to every connected subscriber. The two have their own guards, so a fault in the warnings never stops
    /// the send. Returns the number of sends.</summary>
    public int Tick(DateTime utcNow, IEnumerable<ActiveEvent> active, bool waveWarnings)
    {
        Guard("warnings", () =>
        {
            foreach (var line in PushLines.Warnings(active, waveWarnings, _warnings, utcNow)) Queue.Enqueue(line);
        });
        var sends = 0;
        Guard("send", () =>
        {
            foreach (var line in Queue.Take()) sends += Subscriptions.Deliver(users, line.Text);
        });
        return sends;
    }

    void Guard(string entry, Action work)
    {
        if (!_faults.TryGetValue(entry, out var streak)) _faults[entry] = streak = new FailureStreak();
        try
        {
            work();
            streak.Ok();
        }
        catch (Exception ex)
        {
            if (streak.Fail()) log($"push: {entry} failed ({ex.GetType().Name}); skipped");
        }
    }
}
