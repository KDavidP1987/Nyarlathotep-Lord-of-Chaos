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
