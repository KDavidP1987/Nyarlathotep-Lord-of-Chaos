using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D4: real-clock schedules and GameTime edges.</summary>
public class ScheduleTests
{
    static readonly TimeZoneInfo Zone = Zones.UsEastLike;

    /// <summary>Runs a 1-second ticking scheduler from <paramref name="fromUtc"/> for <paramref name="span"/>,
    /// keeping the last key like EventRuntime does, and returns every key it fired.</summary>
    static List<string> Run(DayOfWeek[] days, TimeOnly[] times, DateTime fromUtc, TimeSpan span, string? lastKey = null, int stepSeconds = 1)
    {
        var fired = new List<string>();
        for (var t = fromUtc; t < fromUtc + span; t = t.AddSeconds(stepSeconds))
        {
            var key = Schedule.Due(days, times, t, Zone, lastKey);
            if (key is null) continue;
            fired.Add(key);
            lastKey = key;
        }
        return fired;
    }

    [Fact]
    public void Fires_once_per_matching_local_minute()
    {
        // Thursday 2026-09-24 20:00 local (EDT, UTC−4) = 00:00 UTC on the 25th.
        var fired = Run([DayOfWeek.Thursday], [new TimeOnly(20, 0)], Zones.Utc(2026, 9, 24, 23, 55), TimeSpan.FromMinutes(10));
        Assert.Equal(["2026-09-24 20:00"], fired);
    }

    [Fact]
    public void Wrong_day_does_not_fire()
    {
        var fired = Run([DayOfWeek.Friday], [new TimeOnly(20, 0)], Zones.Utc(2026, 9, 24, 23, 55), TimeSpan.FromMinutes(10));
        Assert.Empty(fired);
    }

    [Fact]
    public void Dst_repeated_minute_fires_once()
    {
        // Sunday 2026-11-01: 01:30 local happens at 05:30 UTC (EDT) and again at 06:30 UTC (EST).
        var fired = Run([DayOfWeek.Sunday], [new TimeOnly(1, 30)], Zones.Utc(2026, 11, 1, 4, 0), TimeSpan.FromHours(4), stepSeconds: 10);
        Assert.Equal(["2026-11-01 01:30"], fired);
    }

    [Fact]
    public void Dst_repeated_minute_fires_once_across_a_restart()
    {
        // The first instant fires; the server restarts; the key is read back from state.json as a string.
        var first = Schedule.Due([DayOfWeek.Sunday], [new TimeOnly(1, 30)], Zones.Utc(2026, 11, 1, 5, 30), Zone, null);
        Assert.Equal("2026-11-01 01:30", first);
        var persisted = new string(first!.ToCharArray());
        var second = Schedule.Due([DayOfWeek.Sunday], [new TimeOnly(1, 30)], Zones.Utc(2026, 11, 1, 6, 30), Zone, persisted);
        Assert.Null(second);
    }

    [Fact]
    public void Skipped_hour_is_not_replayed()
    {
        // Sunday 2026-03-08: 02:00 local jumps to 03:00, so 02:30 never exists; 03:30 still fires.
        var fired = Run([DayOfWeek.Sunday], [new TimeOnly(2, 30), new TimeOnly(3, 30)], Zones.Utc(2026, 3, 8, 5, 0), TimeSpan.FromHours(4), stepSeconds: 10);
        Assert.Equal(["2026-03-08 03:30"], fired);
    }

    [Fact]
    public void Backward_clock_change_does_not_refire()
    {
        var days = new[] { DayOfWeek.Thursday };
        var times = new[] { new TimeOnly(20, 0) };
        var last = Schedule.Due(days, times, Zones.Utc(2026, 9, 25, 0, 0), Zone, null);
        Assert.NotNull(last);
        // The host clock is set back ten minutes and runs through 20:00 again.
        var again = Run(days, times, Zones.Utc(2026, 9, 24, 23, 50), TimeSpan.FromMinutes(15), last);
        Assert.Empty(again);
    }

    [Fact]
    public void Backward_clock_change_does_not_fire_an_earlier_time_of_the_same_day()
    {
        var days = new[] { DayOfWeek.Thursday };
        var times = new[] { new TimeOnly(19, 0), new TimeOnly(20, 0) };
        var last = Schedule.Due(days, times, Zones.Utc(2026, 9, 25, 0, 0), Zone, "2026-09-24 19:00");
        Assert.Equal("2026-09-24 20:00", last);
        var again = Run(days, times, Zones.Utc(2026, 9, 24, 22, 55), TimeSpan.FromMinutes(10), last);
        Assert.Empty(again);
    }

    [Fact]
    public void Downtime_is_not_replayed()
    {
        // Last fired a week ago; the server was down at 20:00 and comes back at 20:05.
        var fired = Run([DayOfWeek.Thursday], [new TimeOnly(20, 0)], Zones.Utc(2026, 9, 25, 0, 5), TimeSpan.FromHours(1), "2026-09-17 20:00");
        Assert.Empty(fired);
    }

    [Fact]
    public void Next_week_fires_again()
    {
        var fired = Run([DayOfWeek.Thursday], [new TimeOnly(20, 0)], Zones.Utc(2026, 10, 1, 23, 59), TimeSpan.FromMinutes(3), "2026-09-24 20:00");
        Assert.Equal(["2026-10-01 20:00"], fired);
    }

    [Fact]
    public void Daily_banner_fires_once_per_local_day()
    {
        string? last = null;
        var fired = new List<string>();
        for (var t = Zones.Utc(2026, 9, 24, 4, 0); t < Zones.Utc(2026, 9, 27, 4, 0); t = t.AddSeconds(20))
        {
            var k = Schedule.DailyDue(new TimeOnly(20, 0), t, Zone, last);
            if (k is null) continue;
            fired.Add(k);
            last = k;
        }
        Assert.Equal(["2026-09-24 20:00", "2026-09-25 20:00", "2026-09-26 20:00"], fired);
    }

    [Fact]
    public void Game_time_edges_fire_once_per_change()
    {
        var edges = new DayNightEdges();
        var samples = new[] { true, true, false, false, false, true, true, false };
        var got = samples.Select(edges.Sample).ToList();
        Assert.Equal(new DayPhase?[] { null, null, DayPhase.Night, null, null, DayPhase.Day, null, DayPhase.Night }, got);
    }

    [Fact]
    public void First_game_time_sample_fires_nothing()
    {
        Assert.Null(new DayNightEdges().Sample(false));
    }
}
