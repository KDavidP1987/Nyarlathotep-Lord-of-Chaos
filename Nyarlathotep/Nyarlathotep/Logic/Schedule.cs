#nullable enable
using System.Globalization;

namespace Nyarlathotep.Logic;

/// <summary>Real-clock schedules (foundation Business rules 5, D4). A schedule fires at most once per
/// occurrence, keyed by the server-local date and the configured HH:mm. The key of the last firing is kept per
/// event in state.json and firing needs a strictly later key, so:
/// <list type="bullet">
/// <item>both UTC instants of a DST-repeated local minute give one firing, across a restart too;</item>
/// <item>a skipped local minute (spring forward) and minutes during downtime are never replayed, because only
/// the current minute is ever tested;</item>
/// <item>a clock set backwards never re-fires an occurrence at or before the last one.</item>
/// </list></summary>
public static class Schedule
{
    public static string OccurrenceKey(DateTime local, TimeOnly time) =>
        local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The occurrence key to fire now, or null.</summary>
    public static string? Due(IReadOnlyList<DayOfWeek> days, IReadOnlyList<TimeOnly> times, DateTime utcNow, TimeZoneInfo zone, string? lastKey)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        if (!days.Contains(local.DayOfWeek)) return null;
        var minute = new TimeOnly(local.Hour, local.Minute);
        if (!times.Contains(minute)) return null;
        var key = OccurrenceKey(local, minute);
        if (lastKey is not null && string.CompareOrdinal(key, lastKey) <= 0) return null;
        return key;
    }

    static readonly DayOfWeek[] EveryDay =
        [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    /// <summary>The daily banner: once per server-local day at <paramref name="time"/>, under the same rules.</summary>
    public static string? DailyDue(TimeOnly time, DateTime utcNow, TimeZoneInfo zone, string? lastKey) =>
        Due(EveryDay, [time], utcNow, zone, lastKey);
}

/// <summary>GameTime triggers fire on the day/night edge (Epic rule 6). The scheduler feeds one IsDay sample per
/// tick; the first sample establishes the phase and fires nothing.</summary>
public sealed class DayNightEdges
{
    bool? _lastIsDay;

    /// <summary>The phase just entered, or null when there is no edge.</summary>
    public DayPhase? Sample(bool isDay)
    {
        var last = _lastIsDay;
        _lastIsDay = isDay;
        if (last is null || last == isDay) return null;
        return isDay ? DayPhase.Day : DayPhase.Night;
    }
}

/// <summary>The day and night read of the GameTime triggers (event-library D19, A5): <paramref name="isDay"/> is the
/// game's read, which may throw. A throwing read faults only the GameTime triggers: this sample gives no edge, and the
/// failure is logged once per streak; a good read ends the streak.</summary>
public sealed class PhaseSampler(Func<bool> isDay, Action<string> logError)
{
    readonly DayNightEdges _edges = new();
    readonly FailureStreak _faults = new();

    /// <summary>The phase just entered, or null (no edge, or a failed read).</summary>
    public DayPhase? Sample()
    {
        bool day;
        try
        {
            day = isDay();
            _faults.Ok();
        }
        catch (Exception ex)
        {
            if (_faults.Fail()) logError($"day/night read failed: {ex.Message}; GameTime triggers wait");
            return null;
        }
        return _edges.Sample(day);
    }
}

/// <summary>The Interval trigger's clock (automation D2, Business rules 1). An Interval definition waits a random whole number
/// of seconds between minMinutes and maxMinutes after the moment it was last seen inactive, then fires once. The next start
/// is kept in state.json's NextInterval and outlives a restart while it is in the future; downtime is never replayed.</summary>
public static class IntervalClock
{
    public readonly record struct Step(bool Fire, DateTime? Next);

    /// <summary>One poll of one definition: while <paramref name="active"/> there is no next; with none, one is drawn and
    /// nothing fires; a next beyond utcNow + maxMinutes (a shortened definition) is redrawn; a next at or before utcNow
    /// fires once and is removed, so the poll after a refused start or any end draws from that moment.</summary>
    public static Step Poll(int minMinutes, int maxMinutes, DateTime? next, bool active, DateTime utcNow, IRandom rng)
    {
        if (active) return new(false, null);
        if (next is not { } n || n > utcNow.AddMinutes(maxMinutes)) return new(false, Draw(minMinutes, maxMinutes, utcNow, rng));
        return n <= utcNow ? new(true, null) : new(false, n);
    }

    /// <summary>utcNow plus a uniform whole number of seconds in minMinutes×60..maxMinutes×60.</summary>
    public static DateTime Draw(int minMinutes, int maxMinutes, DateTime utcNow, IRandom rng)
    {
        var min = minMinutes * 60;
        var span = (maxMinutes - minMinutes) * 60;
        var seconds = min + Math.Min(span, (int)(rng.NextDouble() * (span + 1)));
        return utcNow.AddSeconds(seconds);
    }

    /// <summary>The stored nexts kept at boot: only ids of startable Interval definitions, and a next at or before
    /// <paramref name="bootUtc"/> redrawn from the boot, so downtime is never replayed (Epic Business rules 6).</summary>
    public static Dictionary<string, DateTime> Load(IReadOnlyDictionary<string, DateTime>? stored, DefinitionSet set, DateTime bootUtc, IRandom rng)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        if (stored is null) return result;
        foreach (var d in TriggerRouter.Candidates(set, TriggerType.Interval))
            if (stored.TryGetValue(d.Id, out var next))
                result[d.Id] = next > bootUtc ? next : Draw(d.Trigger.MinMinutes, d.Trigger.MaxMinutes, bootUtc, rng);
        return result;
    }

    /// <summary>One scheduler tick over every Interval definition: ids that left the startable Interval set are pruned,
    /// each definition is polled, and <paramref name="nexts"/> is updated in place. Returns the definitions to start and
    /// whether the nexts changed (state.json is then dirty).</summary>
    public static (IReadOnlyList<EventDefinition> Due, bool Changed) PollAll(DefinitionSet set, IDictionary<string, DateTime> nexts,
        Func<string, bool> isActive, DateTime utcNow, IRandom rng)
    {
        var due = new List<EventDefinition>();
        var changed = false;
        var candidates = TriggerRouter.Candidates(set, TriggerType.Interval).ToList();
        foreach (var id in nexts.Keys.Where(id => candidates.All(d => d.Id != id)).ToList())
        {
            nexts.Remove(id);
            changed = true;
        }
        foreach (var d in candidates)
        {
            DateTime? now = nexts.TryGetValue(d.Id, out var n) ? n : null;
            var step = Poll(d.Trigger.MinMinutes, d.Trigger.MaxMinutes, now, isActive(d.Id), utcNow, rng);
            if (step.Fire) due.Add(d);
            if (step.Next == now) continue;
            if (step.Next is { } next) nexts[d.Id] = next;
            else nexts.Remove(d.Id);
            changed = true;
        }
        return (due, changed);
    }
}

/// <summary>One scheduler tick's trigger starts (Services/TriggerBus, event-library A5): the Schedule definitions due
/// this server-local minute, then, when <paramref name="phases"/> is given (the day and night hook is available), the
/// GameTime definitions of the phase just entered. A failed phase read leaves the Schedule starts of the same tick.</summary>
public static class TriggerTick
{
    public sealed record Due(
        IReadOnlyList<(EventDefinition Definition, string Occurrence)> Scheduled,
        DayPhase? Entered,
        IReadOnlyList<EventDefinition> PhaseStarts);

    public static Due Collect(DefinitionSet set, DateTime utcNow, TimeZoneInfo zone, Func<string, string?> lastOccurrence, PhaseSampler? phases)
    {
        var scheduled = TriggerRouter.ScheduleDue(set, utcNow, zone, lastOccurrence);
        var entered = phases?.Sample();
        var starts = entered is { } phase ? TriggerRouter.PhaseEntered(set, phase) : [];
        return new Due(scheduled, entered, starts);
    }
}
