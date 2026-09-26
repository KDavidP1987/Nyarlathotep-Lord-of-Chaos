using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation step 5: the engine's pure half (waves, end and grace, faults, conditions, wave sizing,
/// the events.json editor and the admin lines).</summary>
public partial class EngineTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 9, 24, 20, 0);

    static ControlState Open(int active = 0, int max = 3, bool purge = false) =>
        new(purge, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), active, max);

    static EventEngine Engine(params string[] events) => Engine(null, events);

    static EventEngine Engine(IDictionary<string, DateTime>? starts, params string[] events)
    {
        var catalog = new EventCatalog();
        var r = EventValidator.Parse(Json.File(events), FakeUnits.Default());
        Assert.Null(catalog.Reload(r, FileStamp.Of(T0, [1])));
        return starts is null ? new EventEngine(catalog) : new EventEngine(catalog, () => starts);
    }

    // ValidAction: 3 waves every 60 s; duration 600 s.
    [Fact]
    public void Waves_come_at_start_plus_k_intervals_once_each()
    {
        var e = Engine(Json.Event("raid"));
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        var w = e.NextWave("raid", T0);
        Assert.NotNull(w);
        Assert.Equal((1, 3), (w!.Wave, w.Waves));
        e.WaveSpawned("raid");
        Assert.Null(e.NextWave("raid", T0.AddSeconds(59)));
        Assert.Equal(2, e.NextWave("raid", T0.AddSeconds(60))!.Wave);
        e.WaveSpawned("raid");
        Assert.Equal(3, e.NextWave("raid", T0.AddSeconds(500))!.Wave);
        e.WaveSpawned("raid");
        Assert.Null(e.NextWave("raid", T0.AddSeconds(599)));
    }

    [Fact]
    public void A_wave_due_at_or_after_the_end_never_comes()
    {
        var e = Engine(Json.Event("raid", action:
            "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1 } ], " +
            "\"waves\": 10, \"intervalSeconds\": 300, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }"));
        e.Start("raid", "manual", T0, Open());
        e.WaveSpawned("raid");
        e.WaveSpawned("raid");
        Assert.Null(e.NextWave("raid", T0.AddSeconds(600)));   // wave 3 would be at the 600 s end
    }

    [Fact]
    public void Start_replies_in_order_unknown_already_active_then_controls()
    {
        var e = Engine(Json.Event("raid"), Json.Event("other"));
        Assert.Equal("unknown event nope", e.Start("nope", "manual", T0, Open()));
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        Assert.Equal("already active", e.Start("raid", "manual", T0, Open(active: 3, max: 1, purge: true)));
        Assert.Equal("skipped by MaxConcurrentEvents", e.Start("other", "manual", T0, Open(active: 1, max: 1)));
        Assert.Equal("purge cooldown active", e.Start("other", "manual", T0, Open(purge: true)));
        Assert.Single(e.Active);
        Assert.Single(e.Catalog.Running);
    }

    [Fact]
    public void An_admin_location_needs_the_admin_position()
    {
        var e = Engine(Json.Event("raid", action:
            "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1 } ], " +
            "\"waves\": 1, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Admin\" } }"));
        Assert.NotNull(e.Start("raid", "manual", T0, Open()));
        Assert.Null(e.Start("raid", "manual", T0, Open(), (1f, 2f, 3f)));
        Assert.Equal((1f, 2f, 3f), e.Find("raid")!.Origin);
    }

    [Fact]
    public void Expiry_ends_the_event_and_queues_its_cleanup_after_the_grace()
    {
        var e = Engine(Json.Event("raid"));
        e.Start("raid", "manual", T0, Open());
        Assert.Empty(e.Expire(T0.AddSeconds(599), 30));
        var ended = Assert.Single(e.Expire(T0.AddSeconds(600), 30));
        Assert.Equal("raid", ended.Id);
        Assert.Empty(e.Active);
        Assert.Empty(e.Catalog.Running);
        Assert.Empty(e.DueCleanups(T0.AddSeconds(629)));
        var c = Assert.Single(e.DueCleanups(T0.AddSeconds(630)));
        Assert.Equal(new Cleanup("raid", DateTime.MaxValue, T0.AddSeconds(630)), c);   // A17: the end tick's spawns too
        Assert.Empty(e.DueCleanups(T0.AddSeconds(700)));        // once
    }

    [Fact]
    public void A_restart_inside_the_grace_bounds_the_ended_instances_cleanup_at_its_start()
    {
        var e = Engine(Json.Event("raid"), Json.Event("other"));
        e.Start("raid", "manual", T0, Open());
        e.Start("other", "manual", T0, Open());
        e.Expire(T0.AddSeconds(600), 30);
        Assert.Null(e.Start("raid", "manual", T0.AddSeconds(610), Open()));
        Assert.Equal(new Cleanup("raid", T0.AddSeconds(610), T0.AddSeconds(630)),
            Assert.Single(e.PendingCleanups, c => c.EventId == "raid"));
        Assert.Equal(DateTime.MaxValue, Assert.Single(e.PendingCleanups, c => c.EventId == "other").SpawnedBefore);
    }

    [Fact]
    public void Three_faults_in_a_row_cancel_and_a_healthy_tick_resets()
    {
        var e = Engine(Json.Event("raid"));
        e.Start("raid", "manual", T0, Open());
        Assert.False(e.Fault("raid"));
        Assert.False(e.Fault("raid"));
        e.Healthy("raid");
        Assert.False(e.Fault("raid"));
        Assert.False(e.Fault("raid"));
        Assert.True(e.Fault("raid"));
        Assert.NotNull(e.Cancel("raid"));
        Assert.Null(e.Cancel("raid"));
        Assert.Empty(e.Catalog.Running);
    }

    [Fact]
    public void Purge_cancels_every_event_and_drops_pending_cleanups()
    {
        var e = Engine(Json.Event("raid"), Json.Event("other"));
        e.Start("raid", "manual", T0, Open());
        e.Expire(T0.AddSeconds(600), 30);
        e.Start("other", "manual", T0.AddSeconds(601), Open());
        Assert.Single(e.PendingCleanups);
        Assert.Single(e.CancelAll());
        Assert.Empty(e.PendingCleanups);
        Assert.Empty(e.Catalog.Running);
    }

    [Fact]
    public void A_running_instance_keeps_its_definition_through_a_reload()
    {
        var e = Engine(Json.Event("raid"));
        e.Start("raid", "manual", T0, Open());
        var r = EventValidator.Parse(Json.File(Json.Event("raid").Replace("\"durationSeconds\": 600", "\"durationSeconds\": 60")), FakeUnits.Default());
        e.Catalog.Reload(r, FileStamp.Of(T0, [2]));
        Assert.Equal(600, e.Find("raid")!.Definition.DurationSeconds);
        Assert.Equal(60, e.Catalog.Current.Find("raid")!.DurationSeconds);
    }

    [Fact]
    public void Last_start_is_kept_for_cooldowns()
    {
        var e = Engine(Json.Event("raid"));
        Assert.Null(e.LastStartUtc("raid"));
        e.Start("raid", "manual", T0, Open());
        Assert.Equal(T0, e.LastStartUtc("raid"));
    }

    [Fact]
    public void Last_start_lives_in_the_state_so_a_restart_keeps_the_cooldown()
    {
        var starts = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        Engine(starts, Json.Event("raid")).Start("raid", "manual", T0, Open());
        Assert.Equal(T0, starts["raid"]);                        // written to state.json's LastStart
        Assert.Equal(T0, Engine(starts, Json.Event("raid")).LastStartUtc("raid"));   // a new engine after a restart
    }

    // ---- conditions (automatic starts only)

    static ConditionContext Ctx(int players = 5, GameMode mode = GameMode.Pve, int h = 20, DateTime? last = null, int roll = 1) =>
        new(players, mode, new TimeOnly(h, 0), T0, last, roll);

    [Fact]
    public void Conditions_refuse_each_rule()
    {
        Assert.Null(ConditionCheck.Blocker(new Conditions(), Ctx()));
        Assert.Equal("needs 6 players online, 5 are", ConditionCheck.Blocker(new Conditions(MinPlayers: 6), Ctx()));
        Assert.Equal("runs on pvp servers only", ConditionCheck.Blocker(new Conditions(Mode: GameMode.Pvp), Ctx()));
        Assert.Null(ConditionCheck.Blocker(new Conditions(Mode: GameMode.Pve), Ctx()));
        Assert.Equal("cooldown 60 min", ConditionCheck.Blocker(new Conditions(CooldownMinutes: 60), Ctx(last: T0.AddMinutes(-59))));
        Assert.Null(ConditionCheck.Blocker(new Conditions(CooldownMinutes: 60), Ctx(last: T0.AddMinutes(-60))));
        Assert.Equal("chance 50% not met", ConditionCheck.Blocker(new Conditions(ChancePercent: 50), Ctx(roll: 51)));
        Assert.Null(ConditionCheck.Blocker(new Conditions(ChancePercent: 50), Ctx(roll: 50)));
        var night = new TimeWindow(new TimeOnly(22, 0), new TimeOnly(4, 0));
        Assert.Equal("outside its window 22:00-04:00", ConditionCheck.Blocker(new Conditions(Window: night), Ctx(h: 20)));
        Assert.Null(ConditionCheck.Blocker(new Conditions(Window: night), Ctx(h: 23)));
        Assert.Null(ConditionCheck.Blocker(new Conditions(Window: night), Ctx(h: 3)));
    }

    [Fact]
    public void Windows_are_from_inclusive_to_exclusive_and_equal_ends_mean_all_day()
    {
        var day = new TimeWindow(new TimeOnly(8, 0), new TimeOnly(18, 0));
        Assert.True(ConditionCheck.InWindow(day, new TimeOnly(8, 0)));
        Assert.False(ConditionCheck.InWindow(day, new TimeOnly(18, 0)));
        Assert.True(ConditionCheck.InWindow(new TimeWindow(new TimeOnly(5, 0), new TimeOnly(5, 0)), new TimeOnly(1, 0)));
    }

    // ---- wave sizing (D22)

    [Fact]
    public void A_wave_is_clamped_by_MaxUnitsPerWave_then_by_free_slots_and_split_in_order()
    {
        var units = new List<UnitEntry> { new("CHAR_A", 15), new("CHAR_B", 5) };
        var log = new List<string>();
        var first = WavePlan.Split(units, maxPerWave: 5, occupied: 0, maxTracked: 8, log);
        Assert.Equal([new UnitEntry("CHAR_A", 5)], first);
        Assert.Equal(["clamped by MaxUnitsPerWave: 20 -> 5"], log);
        log.Clear();
        var second = WavePlan.Split(units, 5, occupied: 5, maxTracked: 8, log);
        Assert.Equal([new UnitEntry("CHAR_A", 3)], second);
        Assert.Equal(["clamped by MaxUnitsPerWave: 20 -> 5", "skipped by MaxTrackedUnits: 2 of 5"], log);
        log.Clear();
        Assert.Equal([new UnitEntry("CHAR_A", 2), new UnitEntry("CHAR_B", 1)],
            WavePlan.Split([new("CHAR_A", 2), new("CHAR_B", 4)], 3, 0, 150, log));
        Assert.Empty(WavePlan.Split(units, 5, occupied: 8, maxTracked: 8, []));
    }

    // ---- the ledger's event end

    [Fact]
    public void Ending_an_event_queues_only_its_older_units_and_cancels_its_orders()
    {
        var ledger = new SpawnLedger(new LedgerLimits(150, 50, 2, 5));
        ledger.Request("CHAR_A", "raid", 3, new UnitLifetime(DateTime.MaxValue, 60), UnitTuning.None, _ => (0, 0, 0));
        ledger.Request("CHAR_A", "other", 1, new UnitLifetime(DateTime.MaxValue, 60), UnitTuning.None, _ => (0, 0, 0));
        var batch = ledger.TakeSpawns();                         // two raid units in flight
        ledger.Confirm(batch[0], 1, T0);
        ledger.Confirm(batch[1], 2, T0.AddSeconds(10));
        var (queued, cancelled) = ledger.EndEvent("raid", T0.AddSeconds(5));
        Assert.Equal((1, 1), (queued, cancelled));              // key 1 only; the third raid order cancelled
        Assert.Equal(1, ledger.PendingDespawns);
        Assert.Single(ledger.TakeSpawns());                      // the other event's order is kept
    }

    [Fact]
    public void The_despawn_after_the_grace_keeps_a_restarted_events_waiting_orders()
    {
        var ledger = new SpawnLedger(new LedgerLimits(150, 50, 2, 5));
        ledger.Request("CHAR_A", "raid", 1, new UnitLifetime(DateTime.MaxValue, 60), UnitTuning.None, _ => (0, 0, 0));
        ledger.Confirm(ledger.TakeSpawns()[0], 1, T0);           // the ended instance's unit
        ledger.Request("CHAR_A", "raid", 1, new UnitLifetime(DateTime.MaxValue, 60), UnitTuning.None, _ => (0, 0, 0));   // the restart's wave, waiting
        var (queued, cancelled) = ledger.EndEvent("raid", T0.AddSeconds(5), cancelOrders: false);
        Assert.Equal((1, 0), (queued, cancelled));
        Assert.Single(ledger.TakeSpawns());
    }

    // ---- timing log (D24)

    [Fact]
    public void Tick_timer_reports_once_a_minute()
    {
        var t = new TickTimer();
        Assert.Null(t.Add(1.0, T0));
        Assert.Null(t.Add(3.0, T0.AddSeconds(59)));
        Assert.Equal("tick timing: avg 2.000 ms, max 3.000 ms over 3 ticks", t.Add(2.0, T0.AddSeconds(60)));
        Assert.Null(t.Add(5.0, T0.AddSeconds(61)));
    }

    // ---- events.json editor (event set / enable / disable)

    [Fact]
    public void Editor_changes_one_field_and_the_result_still_validates()
    {
        var file = Json.File(Json.Event("raid"), Json.Event("other"));
        var text = EventsEditor.Apply(file, "raid", "enabled", false, out var error);
        Assert.Null(error);
        var r = EventValidator.Parse(text!, FakeUnits.Default());
        Assert.False(r.Set.Find("raid")!.Enabled);
        Assert.True(r.Set.Find("other")!.Enabled);

        text = EventsEditor.Apply(text!, "raid", "conditions.minPlayers", 4, out error);
        text = EventsEditor.Apply(text!, "raid", "action.waves", 7, out error);
        text = EventsEditor.Apply(text!, "raid", "name", "Raid de l'été", out error);
        r = EventValidator.Parse(text!, FakeUnits.Default());
        var d = r.Set.Find("raid")!;
        Assert.Equal((4, 7, "Raid de l'été"), (d.Conditions.MinPlayers, d.Action!.Waves, d.Name));
        Assert.Contains("été", text);
    }

    [Fact]
    public void Editor_refuses_an_unknown_event_or_a_broken_file()
    {
        Assert.Null(EventsEditor.Apply(Json.File(Json.Event("raid")), "nope", "enabled", true, out var error));
        Assert.Equal("unknown event nope", error);
        Assert.Null(EventsEditor.Apply("{ \"events\": [ ", "raid", "enabled", true, out error));
        Assert.StartsWith("events.json does not parse", error);
    }

    // ---- admin lines

    [Fact]
    public void Event_list_pages_ten_per_page_with_reasons()
    {
        var events = Enumerable.Range(1, 11).Select(i => Json.Event($"e{i:00}")).Append(Json.Event("zz", trigger: "{ \"type\": \"Nope\" }")).ToArray();
        var set = EventValidator.Parse(Json.File(events), FakeUnits.Default()).Set;
        var p1 = EventLines.List(set, 1, ["e01"]);
        Assert.Equal("page 1/2", p1[0]);
        Assert.Equal(11, p1.Count);
        Assert.Equal("e01 enabled spawns manual RUNNING", p1[1]);
        var p2 = EventLines.List(set, 2, []);
        Assert.Equal(["page 2/2", "e11 enabled spawns manual", "zz disabled: unknown trigger type Nope"], p2);
        Assert.Equal([EventLines.NoEvents], EventLines.List(DefinitionSet.Empty, 1, []));
        Assert.Equal(1, EventLines.Pages(0));
    }

    [Fact]
    public void Event_info_names_every_part()
    {
        var d = Json.One(Json.Event("raid", trigger: "{ \"type\": \"Schedule\", \"days\": [\"Mon\",\"Fri\"], \"times\": [\"20:00\"] }"));
        var lines = EventLines.Info(d, null, T0);
        Assert.Equal("raid \"Bandit raid\" enabled pillar spawns trigger schedule Mon,Fri 20:00 duration 600s", lines[0]);
        Assert.Contains("minPlayers 0", lines[1]);
        Assert.Equal("action: 3 waves every 60s, radius 10, at -1200.5 -800, units 5 CHAR_Bandit_Thug", lines[2]);
        Assert.Equal("not running", lines[3]);
    }
}
