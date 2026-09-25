using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D13 (Epic D42): wave warnings, the daily banner clock (A18, A19), share limits, the login gate,
/// the one-per-second queue of 20, and the health clock (D31).</summary>
public class AnnouncerTests
{
    static readonly TimeZoneInfo Zone = Zones.UsEastLike;
    static readonly DateTime T0 = Zones.Utc(2026, 9, 24, 20, 0);
    static readonly int[] Offsets = [300, 60, 10];

    // ---- wave warnings ----

    /// <summary>Ticks once a second from <paramref name="firstLeft"/> seconds before the wave down to it, and returns
    /// the offsets that fired.</summary>
    static List<int> Run(WarningClock clock, string key, int firstLeft, int step = 1)
    {
        var fired = new List<int>();
        for (var left = firstLeft; left >= -2; left -= step)
            if (clock.Due(key, left) is { } o) fired.Add(o);
        return fired;
    }

    [Fact]
    public void A_warning_fires_once_at_each_offset()
    {
        Assert.Equal([300, 60, 10], Run(new WarningClock(Offsets), "raid|1|2", 400));
    }

    [Fact]
    public void Offsets_longer_than_the_time_left_are_skipped()
    {
        Assert.Equal([60, 10], Run(new WarningClock(Offsets), "raid|1|2", 90));
        Assert.Equal([60, 10], Run(new WarningClock(Offsets), "raid|1|2", 60));   // exactly at an offset: it fires
    }

    [Fact]
    public void A_late_tick_fires_only_the_nearer_offset_and_never_after_the_wave()
    {
        var clock = new WarningClock(Offsets);
        Assert.Null(clock.Due("k", 100));
        Assert.Equal(10, clock.Due("k", 5));   // 60 and 10 both passed: 10 only
        Assert.Null(clock.Due("k", 4));
        var late = new WarningClock(Offsets);
        Assert.Null(late.Due("w", 20));
        Assert.Null(late.Due("w", 0));         // the wave is due: nothing more
        Assert.Null(late.Due("w", -1));
    }

    [Fact]
    public void Each_wave_and_each_restart_warns_afresh_and_forgotten_waves_are_dropped()
    {
        var clock = new WarningClock(Offsets);
        var a = WarningClock.Key("raid", T0, 2);
        var b = WarningClock.Key("raid", T0, 3);
        var restart = WarningClock.Key("raid", T0.AddMinutes(20), 2);
        Assert.Equal(60, clock.Due(a, 60));
        Assert.Equal(60, clock.Due(b, 60));
        Assert.Equal(60, clock.Due(restart, 60));
        Assert.Null(clock.Due(a, 59));
        clock.Keep([b]);
        Assert.Equal(1, clock.Tracked);
    }

    static EventDefinition Raid(int waves = 3, int interval = 90, int duration = 600, bool warnings = true) => new(
        "raid", "Bandit raid", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), duration,
        new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 5)], waves, interval, 10, new Location(LocationType.Point, 0, 0), null),
        new Announce([], [], warnings));

    [Fact]
    public void The_upcoming_wave_is_the_next_unspawned_one_before_the_end()
    {
        var active = new ActiveEvent(new RunningInstance(Raid(), T0, T0.AddSeconds(600)), "Manual", null);
        Assert.Equal((1, T0), UpcomingWave.Of(active));
        active.WavesSpawned = 1;
        Assert.Equal((2, T0.AddSeconds(90)), UpcomingWave.Of(active));
        active.WavesSpawned = 3;
        Assert.Null(UpcomingWave.Of(active));
        var short_ = new ActiveEvent(new RunningInstance(Raid(duration: 100), T0, T0.AddSeconds(100)), "Manual", null) { WavesSpawned = 2 };
        Assert.Null(UpcomingWave.Of(short_));   // wave 3 would come at +180, after the end
        Assert.Equal(90, UpcomingWave.SecondsLeft(T0.AddSeconds(90), T0));
        Assert.Equal(1, UpcomingWave.SecondsLeft(T0.AddSeconds(90), T0.AddSeconds(89.2)));
    }

    [Fact]
    public void A_three_wave_event_warns_at_60_and_10_seconds_before_waves_2_and_3()
    {
        // D30's event: 3 waves 90 s apart; the service's loop, one tick a second.
        var active = new ActiveEvent(new RunningInstance(Raid(), T0, T0.AddSeconds(600)), "Manual", null);
        var clock = new WarningClock(Offsets);
        var fired = new List<(int Wave, int Offset)>();
        for (var t = T0; t <= T0.AddSeconds(200); t = t.AddSeconds(1))
        {
            if (UpcomingWave.Of(active) is { } next)
            {
                if (next.AtUtc <= t) { active.WavesSpawned++; continue; }   // the wave spawns
                var key = WarningClock.Key(active.Id, T0, next.Wave);
                if (clock.Due(key, UpcomingWave.SecondsLeft(next.AtUtc, t)) is { } o) fired.Add((next.Wave, o));
            }
        }
        Assert.Equal([(2, 60), (2, 10), (3, 60), (3, 10)], fired);
    }

    // ---- daily banner (A18, A19) ----

    static DefinitionSet Scheduled(params (string Id, DayOfWeek Day, string Time, bool Enabled)[] defs) => new(defs.Select(d =>
        Raid() with
        {
            Id = d.Id, Name = $"Event {d.Id}", Enabled = d.Enabled,
            Trigger = new Trigger(TriggerType.Schedule, [d.Day], [TimeOnly.Parse(d.Time)], DayPhase.Night, []),
        }));

    [Fact]
    public void The_daily_banner_fires_once_per_local_day_and_never_replays_a_missed_day()
    {
        var state = new StateDocument();
        var fired = new List<string>();
        // 24 Sep to 28 Sep, but the server is down from 25 Sep 12:00 to 27 Sep 12:00 (local UTC-4 in September).
        for (var t = Zones.Utc(2026, 9, 24, 4, 0); t < Zones.Utc(2026, 9, 29, 4, 0); t = t.AddSeconds(15))
        {
            if (t >= Zones.Utc(2026, 9, 25, 16, 0) && t < Zones.Utc(2026, 9, 27, 16, 0)) continue;
            if (DailyBanner.Due(state, new TimeOnly(20, 0), t, Zone, DefinitionSet.Empty) is not null) fired.Add(state.DailyBanner!);
        }
        Assert.Equal(["2026-09-24 20:00", "2026-09-27 20:00", "2026-09-28 20:00"], fired);
    }

    [Fact]
    public void A_restart_in_the_banner_minute_does_not_fire_it_again()
    {
        var at = Zones.Utc(2026, 9, 25, 0, 0);   // 20:00 local
        var state = new StateDocument();
        Assert.NotNull(DailyBanner.Due(state, new TimeOnly(20, 0), at, Zone, DefinitionSet.Empty));
        var reloaded = StateDocument.TryParse(state.Serialize())!;
        Assert.Equal("2026-09-24 20:00", reloaded.DailyBanner);
        Assert.Null(DailyBanner.Due(reloaded, new TimeOnly(20, 0), at.AddSeconds(30), Zone, DefinitionSet.Empty));
    }

    [Fact]
    public void The_banner_names_enabled_schedule_events_still_due_today_in_time_order()
    {
        var at = Zones.Utc(2026, 9, 25, 0, 0);   // Thursday 24 Sep, 20:00 local
        var set = Scheduled(
            ("late", DayOfWeek.Thursday, "22:30", true),
            ("soon", DayOfWeek.Thursday, "20:30", true),
            ("past", DayOfWeek.Thursday, "19:00", true),
            ("now", DayOfWeek.Thursday, "20:00", true),
            ("off", DayOfWeek.Thursday, "21:00", false),
            ("friday", DayOfWeek.Friday, "21:00", true));
        var names = DailyBanner.Due(new StateDocument(), new TimeOnly(20, 0), at, Zone, set);
        Assert.Equal(["Event soon", "Event late"], names);
        Assert.Equal("Tonight: Event soon, Event late.", Messages.DailyBannerText(names!, 0));
    }

    [Fact]
    public void With_nothing_scheduled_the_banner_is_spent_with_no_names()
    {
        var state = new StateDocument();
        var names = DailyBanner.Due(state, new TimeOnly(20, 0), Zones.Utc(2026, 9, 25, 0, 0), Zone, DefinitionSet.Empty);
        Assert.NotNull(names);
        Assert.Empty(names!);
        Assert.NotNull(state.DailyBanner);
    }

    [Fact]
    public void The_banner_lists_five_names_then_a_count_and_stays_within_the_line_limit()
    {
        var names = Enumerable.Range(1, 8).Select(i => $"Event {i}").ToList();
        Assert.Equal("Tonight: Event 1, Event 2, Event 3, Event 4, Event 5 and 3 more.", Messages.DailyBannerText(names, 0));
        var wide = Enumerable.Range(1, 5).Select(_ => new string('漢', 40)).ToList();   // 120 bytes each
        var line = Messages.DailyBannerText(wide, 0);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes);
        Assert.EndsWith("and 2 more.", line);
    }

    // ---- shares ----

    [Fact]
    public void A_share_is_refused_within_the_players_cooldown()
    {
        var limiter = new ShareLimiter(300, 3);
        Assert.Null(limiter.TryShare(1, T0));
        Assert.Equal("share: wait 1 s", limiter.TryShare(1, T0.AddSeconds(299.5)));
        Assert.Null(limiter.TryShare(1, T0.AddSeconds(300)));
    }

    [Fact]
    public void The_server_passes_at_most_ShareMaxPerMinute_in_any_minute()
    {
        var limiter = new ShareLimiter(10, 3);
        Assert.Null(limiter.TryShare(1, T0));
        Assert.Null(limiter.TryShare(2, T0.AddSeconds(10)));
        Assert.Null(limiter.TryShare(3, T0.AddSeconds(20)));
        Assert.StartsWith("share: the server allows 3 a minute", limiter.TryShare(4, T0.AddSeconds(30)));
        Assert.StartsWith("share: the server allows 3 a minute", limiter.TryShare(4, T0.AddSeconds(59.9)));
        Assert.Null(limiter.TryShare(4, T0.AddSeconds(60)));   // the first share left the window
    }

    [Fact]
    public void Of_two_shares_racing_for_the_last_allowance_the_first_processed_passes()
    {
        var limiter = new ShareLimiter(10, 2);
        Assert.Null(limiter.TryShare(1, T0));
        Assert.Null(limiter.TryShare(2, T0.AddSeconds(5)));   // first of the two
        Assert.NotNull(limiter.TryShare(3, T0.AddSeconds(5)));
    }

    [Fact]
    public void A_refused_share_uses_no_allowance()
    {
        var limiter = new ShareLimiter(300, 2);
        Assert.Null(limiter.TryShare(1, T0));
        Assert.NotNull(limiter.TryShare(1, T0.AddSeconds(1)));   // cooldown
        Assert.Null(limiter.TryShare(2, T0.AddSeconds(2)));      // still the second of 2
    }

    // ---- login ----

    [Fact]
    public void The_login_message_fires_once_per_connect_and_not_on_a_quick_reconnect()
    {
        var gate = new LoginGate();
        Assert.True(gate.ShouldGreet(7, T0));
        Assert.False(gate.ShouldGreet(7, T0.AddSeconds(30)));
        Assert.False(gate.ShouldGreet(7, T0.AddSeconds(89)));   // 59 s after the last connect
        Assert.True(gate.ShouldGreet(7, T0.AddSeconds(149)));   // 60 s after it
        Assert.True(gate.ShouldGreet(8, T0.AddSeconds(149)));   // per player
        Assert.True(gate.ShouldGreet(9, T0.AddSeconds(500)));
        Assert.Equal(1, gate.Tracked);                            // 7 and 8 left the quiet window
    }

    [Fact]
    public void Share_state_forgets_players_whose_cooldown_ran_out()
    {
        var limiter = new ShareLimiter(300, 20);
        foreach (var p in Enumerable.Range(1, 10)) Assert.Null(limiter.TryShare((ulong)p, T0));
        Assert.Equal(10, limiter.Tracked);
        Assert.Null(limiter.TryShare(99, T0.AddSeconds(300)));
        Assert.Equal(1, limiter.Tracked);
    }

    // ---- queue ----

    [Fact]
    public void Lines_leave_at_most_one_per_second()
    {
        var queue = new AnnounceQueue(_ => { });
        foreach (var i in Enumerable.Range(1, 3)) queue.Enqueue(new QueuedLine($"line {i}", LineKind.Info));
        Assert.Equal("line 1", queue.Next(T0)?.Text);
        Assert.Null(queue.Next(T0.AddSeconds(0.99)));
        Assert.Equal("line 2", queue.Next(T0.AddSeconds(1))?.Text);
        Assert.Equal("line 3", queue.Next(T0.AddSeconds(5))?.Text);
        Assert.Null(queue.Next(T0.AddSeconds(10)));
    }

    [Fact]
    public void A_full_queue_drops_its_oldest_informational_line_with_a_log_line()
    {
        var log = new List<string>();
        var queue = new AnnounceQueue(log.Add);
        Assert.Equal(20, AnnounceQueue.Capacity);
        queue.Enqueue(new QueuedLine("warn 0", LineKind.Warning, "raid"));
        foreach (var i in Enumerable.Range(1, 19)) queue.Enqueue(new QueuedLine($"info {i}", LineKind.Info));
        Assert.Equal(20, queue.Count);
        Assert.Empty(log);                                  // 20 fit
        queue.Enqueue(new QueuedLine("warn 20", LineKind.Warning, "raid"));
        Assert.Equal(20, queue.Count);
        Assert.Contains("dropped the oldest informational line: info 1", Assert.Single(log));
        Assert.Equal("warn 0", queue.Next(T0)?.Text);   // no warning dropped while an informational line was queued
    }

    [Fact]
    public void A_queue_full_of_warnings_drops_its_oldest_warning()
    {
        var log = new List<string>();
        var queue = new AnnounceQueue(log.Add);
        foreach (var i in Enumerable.Range(0, 20)) queue.Enqueue(new QueuedLine($"warn {i}", LineKind.Warning, "raid"));
        Assert.Empty(log);
        queue.Enqueue(new QueuedLine("warn 20", LineKind.Warning, "raid"));
        Assert.Equal(20, queue.Count);
        Assert.Contains("dropped the oldest wave warning: warn 0", Assert.Single(log));
        Assert.Equal("warn 1", queue.Next(T0)?.Text);
    }

    [Fact]
    public void A_warning_still_queued_when_its_wave_arrives_is_dropped_not_sent_late()
    {
        var log = new List<string>();
        var queue = new AnnounceQueue(log.Add);
        queue.Enqueue(new QueuedLine("banner", LineKind.Info));
        queue.Enqueue(new QueuedLine("wave 2 in 10 s", LineKind.Warning, "raid", T0.AddSeconds(1)));
        queue.Enqueue(new QueuedLine("later", LineKind.Info));
        Assert.Equal("banner", queue.Next(T0)?.Text);
        Assert.Equal("later", queue.Next(T0.AddSeconds(1))?.Text);   // the wave came at T0 + 1 s
        Assert.Contains("dropped 1 line(s)", Assert.Single(log));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void An_ended_event_takes_its_unsent_warnings_with_it()
    {
        var queue = new AnnounceQueue(_ => { });
        queue.Enqueue(new QueuedLine("raid warn", LineKind.Warning, "raid"));
        queue.Enqueue(new QueuedLine("siege warn", LineKind.Warning, "siege"));
        queue.Enqueue(new QueuedLine("raid banner", LineKind.Info, "raid"));
        Assert.Equal(1, queue.DropWarnings("raid"));
        Assert.Equal(1, queue.DropWarnings());
        Assert.Equal("raid banner", queue.Next(T0)?.Text);
    }

    // ---- health (D31) ----

    [Fact]
    public void The_health_line_is_due_every_ten_minutes()
    {
        var clock = new HealthClock();
        var due = new List<DateTime>();
        for (var t = T0; t <= T0.AddMinutes(35); t = t.AddSeconds(1)) if (clock.Due(t)) due.Add(t);
        Assert.Equal([T0.AddMinutes(10), T0.AddMinutes(20), T0.AddMinutes(30)], due);
        Assert.Equal("nyar health: 2 events, 40 tracked, degraded: none", HealthClock.Line(2, 40, []));
        Assert.Equal("nyar health: 0 events, 0 tracked, degraded: hook UserConnect, zones (event x faulted)",
            HealthClock.Line(0, 0, ["hook UserConnect", "zones (event x faulted)"]));
    }

    // ---- warning text ----

    [Fact]
    public void A_warning_under_a_minute_ahead_says_almost_here()
    {
        Assert.Equal("Wave 2 of 3 of the Bandit raid arrives in 1 min.", Messages.WaveWarning(Raid(), 2, 60, 0));
        Assert.Equal("Wave 2 of 3 of the Bandit raid arrives in 5 min.", Messages.WaveWarning(Raid(), 2, 300, 0));
        Assert.Equal("Wave 2 of 3 of the Bandit raid is almost here.", Messages.WaveWarning(Raid(), 2, 10, 0));
    }
}
