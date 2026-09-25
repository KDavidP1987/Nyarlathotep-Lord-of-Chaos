#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The event engine's pure half (foundation build step 5): which definitions a trigger reaches, whether a
// definition's own conditions allow an automatic start, how a wave is sized, and the life of a running instance
// (waves due, faults, end, the despawn after the grace). Services/EventRuntime, TriggerBus and WaveAction drive it
// on the server main thread, so there are no locks.

/// <summary>What the conditions of a definition are checked against when a trigger fires.</summary>
public sealed record ConditionContext(int Players, GameMode ServerMode, TimeOnly LocalNow, DateTime UtcNow, DateTime? LastStartUtc, int Roll);

/// <summary>A definition's conditions (Design › Data): they gate automatic starts only; an admin's `.nyar event start`
/// is deliberate and meets the controls of <see cref="Precedence"/> alone.</summary>
public static class ConditionCheck
{
    /// <summary>Why the conditions refuse the start, or null. <see cref="ConditionContext.Roll"/> is 1–100.</summary>
    public static string? Blocker(Conditions c, ConditionContext x)
    {
        if (x.Players < c.MinPlayers) return $"needs {c.MinPlayers} players online, {x.Players} are";
        if (c.Mode != GameMode.Any && c.Mode != x.ServerMode) return $"runs on {c.Mode.ToString().ToLowerInvariant()} servers only";
        if (c.Window is { } w && !InWindow(w, x.LocalNow)) return $"outside its window {w.From:HH\\:mm}-{w.To:HH\\:mm}";
        if (c.CooldownMinutes > 0 && x.LastStartUtc is { } last && x.UtcNow - last < TimeSpan.FromMinutes(c.CooldownMinutes))
            return $"cooldown {c.CooldownMinutes} min";
        if (x.Roll > c.ChancePercent) return $"chance {c.ChancePercent}% not met";
        return null;
    }

    /// <summary>From inclusive, To exclusive; a window whose To is earlier crosses midnight; From == To is all day.</summary>
    public static bool InWindow(TimeWindow w, TimeOnly t) =>
        w.From == w.To || (w.From < w.To ? t >= w.From && t < w.To : t >= w.From || t < w.To);
}

/// <summary>Which definitions an automatic trigger reaches. <see cref="Candidates"/> is the one enabled check for every
/// trigger kind (foundation D40): a disabled definition is never returned, so it never starts and logs nothing.</summary>
public static class TriggerRouter
{
    public static IEnumerable<EventDefinition> Candidates(DefinitionSet set, TriggerType type) =>
        set.All.Where(d => d.Trigger.Type == type && d.Startable);

    /// <summary>Schedule definitions due this minute, with the occurrence key to record (D4).</summary>
    public static IReadOnlyList<(EventDefinition Definition, string Occurrence)> ScheduleDue(DefinitionSet set, DateTime utcNow,
        TimeZoneInfo zone, Func<string, string?> lastOccurrence)
    {
        var due = new List<(EventDefinition, string)>();
        foreach (var d in Candidates(set, TriggerType.Schedule))
        {
            var key = Schedule.Due(d.Trigger.Days, d.Trigger.Times, utcNow, zone, lastOccurrence(d.Id));
            if (key is not null) due.Add((d, key));
        }
        return due;
    }

    public static IReadOnlyList<EventDefinition> PhaseEntered(DefinitionSet set, DayPhase phase) =>
        Candidates(set, TriggerType.GameTime).Where(d => d.Trigger.Phase == phase).ToList();

    /// <summary>VBloodKilled definitions naming <paramref name="prefab"/> or "any".</summary>
    public static IReadOnlyList<EventDefinition> VBloodKilled(DefinitionSet set, string prefab) =>
        Candidates(set, TriggerType.VBloodKilled)
            .Where(d => d.Trigger.Bosses.Any(b => b == "any" || string.Equals(b, prefab, StringComparison.Ordinal)))
            .ToList();
}

/// <summary>How one wave is sized (Business rules 1, D22): the whole wave is clamped by MaxUnitsPerWave, then by the free
/// MaxTrackedUnits slots, and the units left are given to the entries in order.</summary>
public static class WavePlan
{
    public static IReadOnlyList<UnitEntry> Split(IReadOnlyList<UnitEntry> units, int maxPerWave, int occupied, int maxTracked, List<string> log)
    {
        var requested = units.Sum(u => u.Count);
        var left = Precedence.WaveSize(requested, maxPerWave, occupied, maxTracked, log);
        var result = new List<UnitEntry>();
        foreach (var u in units)
        {
            if (left <= 0) break;
            var n = Math.Min(u.Count, left);
            result.Add(new UnitEntry(u.Prefab, n));
            left -= n;
        }
        return result;
    }
}

/// <summary>A running instance and its progress. <see cref="Origin"/> is the starting admin's position for an Admin
/// location, otherwise null.</summary>
public sealed class ActiveEvent(RunningInstance instance, string trigger, (float X, float Y, float Z)? origin)
{
    public RunningInstance Instance { get; } = instance;
    public EventDefinition Definition => Instance.Definition;
    public string Id => Instance.Definition.Id;
    public string Trigger { get; } = trigger;
    public (float X, float Y, float Z)? Origin { get; } = origin;
    public int WavesSpawned { get; internal set; }
    public int Faults { get; internal set; }
}

/// <summary>Wave <see cref="Wave"/> (1-based) of <see cref="Event"/> is due.</summary>
public sealed record WaveDue(ActiveEvent Event, int Wave, int Waves);

/// <summary>The units of an event that ended are queued for despawn at <see cref="DueUtc"/> (its end + GraceSeconds);
/// only units spawned before <see cref="SpawnedBefore"/> belong to that instance. It is unbounded until the same event
/// starts again inside the grace, and then that start's time, so the restart keeps its new units and a unit spawned in
/// the tick the event ended still belongs to the ended instance (A17).</summary>
public sealed record Cleanup(string EventId, DateTime SpawnedBefore, DateTime DueUtc);

/// <summary>The running events (D6, D16, D25, Business rules 2 and 8). A start reads the current definition set; the
/// instance keeps that definition until it ends, whatever a reload does. Each start's time goes into
/// <paramref name="lastStarts"/>, state.json's LastStart, so conditions.cooldownMinutes outlives a restart
/// (Design › Data).</summary>
public sealed class EventEngine(EventCatalog catalog, Func<IDictionary<string, DateTime>>? lastStarts = null)
{
    public const int FaultLimit = 3;

    readonly Dictionary<string, ActiveEvent> _active = new(StringComparer.Ordinal);
    readonly IDictionary<string, DateTime> _ownStarts = new Dictionary<string, DateTime>(StringComparer.Ordinal);
    IDictionary<string, DateTime> Starts => lastStarts?.Invoke() ?? _ownStarts;
    readonly List<Cleanup> _cleanups = [];

    public EventCatalog Catalog => catalog;
    public IReadOnlyCollection<ActiveEvent> Active => _active.Values;
    public IReadOnlyList<Cleanup> PendingCleanups => _cleanups;
    public ActiveEvent? Find(string id) => _active.TryGetValue(id, out var a) ? a : null;
    public DateTime? LastStartUtc(string id) => Starts.TryGetValue(id, out var t) ? t : null;

    /// <summary>Starts the current definition <paramref name="id"/>. The reply on refusal, highest first: "unknown event",
    /// "already active", then the controls of <see cref="Precedence.StartBlocker"/>.</summary>
    public string? Start(string id, string trigger, DateTime utcNow, ControlState controls, (float X, float Y, float Z)? origin = null)
    {
        var def = catalog.Current.Find(id);
        if (def is null) return $"unknown event {id}";
        if (_active.ContainsKey(id)) return "already active";
        var blocker = Precedence.StartBlocker(def, controls);
        if (blocker is not null) return blocker;
        if (def.Action?.Location.Type == LocationType.Admin && origin is null) return $"event {id} spawns at the admin: start it with .nyar event start";
        var error = catalog.TryStart(id, utcNow, out var instance);
        if (error is not null) return error;
        _active[id] = new ActiveEvent(instance!, trigger, origin);
        Starts[id] = utcNow;
        for (var i = 0; i < _cleanups.Count; i++)                   // the ended instance's units are all older (A17)
            if (_cleanups[i].EventId == id && _cleanups[i].SpawnedBefore > utcNow)
                _cleanups[i] = _cleanups[i] with { SpawnedBefore = utcNow };
        return null;
    }

    /// <summary>The next wave of <paramref name="id"/> when it is due: wave k (0-based) is due at start + k × interval,
    /// and a wave due at or after the event's end never comes.</summary>
    public WaveDue? NextWave(string id, DateTime utcNow)
    {
        if (!_active.TryGetValue(id, out var a) || a.Definition.Action is not { } action) return null;
        if (a.WavesSpawned >= action.Waves) return null;
        var at = a.Instance.StartedUtc.AddSeconds((double)a.WavesSpawned * action.IntervalSeconds);
        if (at > utcNow || at >= a.Instance.EndsUtc) return null;
        return new WaveDue(a, a.WavesSpawned + 1, action.Waves);
    }

    public void WaveSpawned(string id)
    {
        if (_active.TryGetValue(id, out var a)) a.WavesSpawned++;
    }

    /// <summary>A fault in the event's tick; true when it is the <see cref="FaultLimit"/>-th in a row, and the caller
    /// then cancels the event (D25).</summary>
    public bool Fault(string id) => _active.TryGetValue(id, out var a) && ++a.Faults >= FaultLimit;

    /// <summary>A tick without a fault ends the streak.</summary>
    public void Healthy(string id)
    {
        if (_active.TryGetValue(id, out var a)) a.Faults = 0;
    }

    /// <summary>Removes the events whose end has come and schedules their units' despawn after the grace
    /// (Business rules 2).</summary>
    public IReadOnlyList<ActiveEvent> Expire(DateTime utcNow, int graceSeconds)
    {
        var ended = _active.Values.Where(a => a.Instance.EndsUtc <= utcNow).ToList();
        foreach (var a in ended)
        {
            Remove(a.Id);
            _cleanups.Add(new Cleanup(a.Id, DateTime.MaxValue, a.Instance.EndsUtc.AddSeconds(graceSeconds)));
        }
        return ended;
    }

    /// <summary>The cleanups whose time has come; each is returned once.</summary>
    public IReadOnlyList<Cleanup> DueCleanups(DateTime utcNow)
    {
        var due = _cleanups.Where(c => c.DueUtc <= utcNow).ToList();
        foreach (var c in due) _cleanups.Remove(c);
        return due;
    }

    /// <summary>Ends <paramref name="id"/> now (stop, fault limit): the caller queues its units at once. Null when it is
    /// not active. A pending cleanup of an earlier instance stays.</summary>
    public ActiveEvent? Cancel(string id)
    {
        if (!_active.TryGetValue(id, out var a)) return null;
        Remove(id);
        return a;
    }

    /// <summary>The purge: every event ends now and every pending cleanup is dropped, since the purge queues every
    /// tracked unit itself (D20).</summary>
    public IReadOnlyList<ActiveEvent> CancelAll()
    {
        var all = _active.Values.ToList();
        foreach (var a in all) Remove(a.Id);
        _cleanups.Clear();
        return all;
    }

    void Remove(string id)
    {
        _active.Remove(id);
        catalog.TryEnd(id);
    }
}

/// <summary>Debug.TimingLog (D24): the scheduler adds each tick's duration; once a minute has passed since the first
/// sample of the window it returns "tick timing: avg &lt;a&gt; ms, max &lt;m&gt; ms over &lt;n&gt; ticks" and starts a
/// new window.</summary>
public sealed class TickTimer
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    DateTime? _since;
    double _total;
    double _max;
    int _count;

    public string? Add(double milliseconds, DateTime utcNow)
    {
        _since ??= utcNow;
        _total += milliseconds;
        _max = Math.Max(_max, milliseconds);
        _count++;
        if (utcNow - _since.Value < Window) return null;
        var line = FormattableString.Invariant($"tick timing: avg {_total / _count:0.000} ms, max {_max:0.000} ms over {_count} ticks");
        _since = null;
        _total = _max = 0;
        _count = 0;
        return line;
    }
}
