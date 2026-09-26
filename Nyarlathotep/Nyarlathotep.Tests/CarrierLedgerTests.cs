using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D5: the carrier ledger plans every carrier operation within the per-tick budget.</summary>
public class CarrierLedgerTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 9, 26, 20, 0);
    static readonly EmpowerAction Bandits = new(["Faction_Bandits"], [], [], false, new EmpowerStats(PhysicalPower: 1.3));

    sealed class Rig
    {
        public FakeCarrierOps Ops { get; }
        public List<string> Log { get; } = [];
        public CarrierLedger Ledger { get; }
        public Rig(FakeCarrierOps ops)
        {
            Ops = ops;
            Ledger = new CarrierLedger(ops, Log.Add);
        }

        /// <summary>One server tick: removals first, then each event; returns the operations it performed.</summary>
        public int Tick(DateTime now, int budget, params string[] events)
        {
            var before = Ops.Operations;
            Ledger.BeginTick(budget);
            foreach (var id in events) Ledger.TickEvent(id, now);
            return Ops.Operations - before;
        }
    }

    static Rig Started(int units, DateTime? ends = null, string id = "surge")
    {
        var rig = new Rig(FakeCarrierOps.WithBandits(units));
        rig.Ledger.Start(id, Bandits, ends ?? T0.AddSeconds(600), T0);
        return rig;
    }

    [Fact]
    public void A_tick_never_performs_more_than_the_budget()
    {
        var rig = Started(500);
        var per = new List<int>();
        for (var i = 0; i < 4; i++) per.Add(rig.Tick(T0.AddMilliseconds(i * 100), 200, "surge"));
        Assert.Equal([200, 200, 100, 0], per);
        Assert.Equal(500, rig.Ledger.CountFor("surge"));
        Assert.Equal(["empower surge: query 500 of 503 faction entities", "empower surge: sweep 500 applied, 0 skipped"], rig.Log);
    }

    [Fact]
    public void A_sweep_is_not_queued_again_while_unfinished_and_is_requeued_15_s_after_it_started()
    {
        var rig = Started(500);
        for (var i = 0; i < 9; i++)                                  // 9 ticks of 50 over 18 s: still unfinished
        {
            rig.Tick(T0.AddSeconds(i * 2), 50, "surge");
            Assert.Equal(1, rig.Ops.Queries);
            Assert.True(rig.Ledger.SweepInProgress("surge"));
        }
        rig.Tick(T0.AddSeconds(18), 50, "surge");                    // the 10th finishes it
        Assert.False(rig.Ledger.SweepInProgress("surge"));
        Assert.Equal(1, rig.Ops.Queries);
        rig.Tick(T0.AddSeconds(19), 50, "surge");                    // 15 s after its start has passed: queued now
        Assert.Equal(2, rig.Ops.Queries);

        var fresh = Started(10);
        fresh.Tick(T0, 200, "surge");
        fresh.Tick(T0.AddSeconds(14.9), 200, "surge");
        Assert.Equal(1, fresh.Ops.Queries);
        fresh.Tick(T0.AddSeconds(15), 200, "surge");
        Assert.Equal(2, fresh.Ops.Queries);
    }

    [Fact]
    public void A_resweep_catches_a_new_unit_and_skips_the_carried_ones()
    {
        var rig = Started(3);
        rig.Tick(T0, 200, "surge");
        rig.Ops.Units[99] = FakeCarrierOps.Bandit();                 // a respawn
        rig.Tick(T0.AddSeconds(15), 200, "surge");
        Assert.Equal(4, rig.Ledger.CountFor("surge"));
        Assert.Equal("empower surge: sweep 1 applied, 3 skipped (carried 3)", rig.Log.Last());
    }

    [Fact]
    public void Each_carrier_lives_exactly_the_seconds_left_and_none_is_applied_under_one_second()
    {
        var rig = Started(1, ends: T0.AddSeconds(100));
        rig.Ledger.BeginTick(200);
        rig.Ledger.TickEvent("surge", T0.AddSeconds(10.5));
        var recipe = Assert.Single(rig.Ops.Recipes.Values);
        Assert.Equal(89.5f, recipe.LifeTimeSeconds);

        var late = Started(1, ends: T0.AddSeconds(10));
        late.Tick(T0.AddSeconds(9.01), 200, "surge");                // 0.99 s left
        Assert.Equal(0, late.Ops.CallCount("create"));
        var edge = Started(1, ends: T0.AddSeconds(10));
        edge.Tick(T0.AddSeconds(9), 200, "surge");                   // exactly 1 s left
        Assert.Equal(1f, Assert.Single(edge.Ops.Recipes.Values).LifeTimeSeconds);
    }

    [Fact]
    public void A_stop_mid_sweep_drops_the_sweep_and_queues_every_carrier()
    {
        var rig = Started(500);
        rig.Tick(T0, 100, "surge");
        Assert.Equal(100, rig.Ledger.CountFor("surge"));
        rig.Ledger.Stop("surge");
        Assert.False(rig.Ledger.IsActive("surge"));
        Assert.Equal(100, rig.Ledger.PendingRemovals);
        Assert.Equal(0, rig.Ledger.Carriers);
        for (var i = 1; i < 5; i++) rig.Tick(T0.AddSeconds(i), 100, "surge");
        Assert.Equal(100, rig.Ops.CallCount("create"));              // no Apply after the stop
        Assert.Equal(100, rig.Ops.CallCount("remove"));
        Assert.Empty(rig.Ops.Buffs);
        Assert.Equal("empower surge stopped: 100 removed, 0 left to expire", rig.Log.Last());
    }

    [Fact]
    public void A_stop_finishes_within_ceil_n_over_budget_plus_one_ticks()
    {
        var rig = Started(450);
        rig.Tick(T0, 1000, "surge");
        rig.Ledger.Stop("surge");
        var ticks = 0;
        while (rig.Ledger.PendingRemovals > 0 && ticks < 10)
        {
            rig.Tick(T0.AddSeconds(1 + ticks), 200);
            ticks++;
        }
        Assert.True(ticks <= (int)Math.Ceiling(450 / 200.0) + 1, $"{ticks} ticks");
        Assert.Empty(rig.Ops.Buffs);
    }

    [Fact]
    public void A_stop_with_no_carriers_reports_at_once()
    {
        var rig = Started(0);
        rig.Ledger.Stop("surge");
        Assert.Equal(["empower surge stopped: 0 removed, 0 left to expire"], rig.Log);
    }

    [Fact]
    public void Removals_come_before_applies_and_share_the_budget()
    {
        var ops = FakeCarrierOps.WithBandits(150);
        for (var i = 0; i < 300; i++) ops.Units[1000_000 + i] = FakeCarrierOps.Bandit() with { Faction = "Faction_Legion" };
        var rig = new Rig(ops);
        rig.Ledger.Start("a", Bandits, T0.AddSeconds(600), T0);
        rig.Tick(T0, 1000, "a");
        rig.Ledger.Stop("a");
        rig.Ledger.Start("b", Bandits with { Factions = ["Faction_Legion"] }, T0.AddSeconds(600), T0);
        var used = rig.Tick(T0.AddSeconds(1), 200, "b");
        Assert.Equal(200, used);
        Assert.Equal(150, ops.CallCount("remove"));
        Assert.Equal(0, rig.Ledger.PendingRemovals);
    }

    [Fact]
    public void A_natural_end_queues_nothing()
    {
        var rig = Started(20);
        rig.Tick(T0, 200, "surge");
        rig.Ledger.End("surge");
        Assert.False(rig.Ledger.IsActive("surge"));
        Assert.Equal(0, rig.Ledger.PendingRemovals);
        rig.Tick(T0.AddSeconds(1), 200, "surge");
        Assert.Equal(0, rig.Ops.CallCount("remove"));
        Assert.Equal(20, rig.Ops.Buffs.Count);                       // left to their LifeTime, which ends now
    }

    // ---- A4: the natural-end watch. ----

    static Rig Ended(int units, out DateTime end)
    {
        end = T0.AddSeconds(60);
        var rig = Started(units, end);
        rig.Tick(T0, 500, "surge");
        rig.Ledger.End("surge");
        return rig;
    }

    [Fact]
    public void A_carrier_still_there_5_s_after_the_natural_end_is_queued_and_removed()
    {
        var rig = Ended(20, out var end);
        Assert.Equal(20, rig.Ledger.Watched);
        Assert.Equal("removing", rig.Ledger.CarrierOf(1));          // no second carrier while it is watched
        rig.Ledger.BeginTick(200, end.AddSeconds(4.9));
        Assert.Equal(20, rig.Ledger.Watched);                        // not due yet
        foreach (var b in rig.Ops.Buffs.Where(b => rig.Ops.BuffUnit[b] > 5).ToList()) rig.Ops.Buffs.Remove(b);   // 15 expired
        rig.Ledger.BeginTick(200, end.AddSeconds(5));
        Assert.Equal(0, rig.Ledger.Watched);
        Assert.Equal(5, rig.Ops.CallCount("remove"));                // the 5 that outlived it, in the same tick
        Assert.Empty(rig.Ops.Buffs);
        Assert.Contains("empower surge ended: 5 carriers outlived the end, queued for removal", rig.Log);
        Assert.Null(rig.Ledger.CarrierOf(1));
        Assert.Null(rig.Ledger.CarrierOf(20));
    }

    [Fact]
    public void An_event_ended_later_but_due_earlier_is_checked_at_its_own_due_time()
    {
        var ops = FakeCarrierOps.WithBandits(2);
        var rig = new Rig(ops);
        rig.Ledger.Start("late", Bandits with { IncludeUnits = [] }, T0.AddSeconds(600), T0);
        rig.Tick(T0, 200, "late");
        rig.Ledger.End("late");                                      // due T0 + 605
        ops.Units[10] = FakeCarrierOps.Bandit();
        rig.Ledger.Start("early", Bandits, T0.AddSeconds(60), T0);
        rig.Tick(T0.AddSeconds(1), 200, "early");                   // the free unit 10 gets early's carrier
        rig.Ledger.End("early");                                     // due T0 + 65, ended second
        rig.Ledger.BeginTick(200, T0.AddSeconds(65));
        Assert.Equal(2, rig.Ledger.Watched);                         // late's two still wait
        Assert.Contains("empower early ended: 1 carriers outlived the end, queued for removal", rig.Log);
        Assert.Null(rig.Ledger.CarrierOf(10));
    }

    [Fact]
    public void Carriers_that_end_on_time_need_no_removal_and_no_line()
    {
        var rig = Ended(20, out var end);
        rig.Ops.Buffs.Clear();
        rig.Ledger.BeginTick(200, end.AddSeconds(5));
        Assert.Equal(0, rig.Ledger.Watched);
        Assert.Equal(0, rig.Ops.CallCount("remove"));
        Assert.DoesNotContain(rig.Log, l => l.Contains("outlived", StringComparison.Ordinal));
        Assert.Null(rig.Ledger.CarrierOf(1));
    }

    [Fact]
    public void The_watch_checks_at_most_the_budget_per_tick()
    {
        var rig = Ended(300, out var end);
        rig.Ledger.BeginTick(200, end.AddSeconds(5));
        Assert.Equal(100, rig.Ledger.Watched);
        Assert.DoesNotContain(rig.Log, l => l.Contains("outlived", StringComparison.Ordinal));   // logged once, when all are checked
        rig.Ledger.BeginTick(200, end.AddSeconds(6));
        Assert.Equal(0, rig.Ledger.Watched);
        Assert.Single(rig.Log, l => l == "empower surge ended: 300 carriers outlived the end, queued for removal");
    }

    [Fact]
    public void A_purge_queues_the_watched_carriers_at_once()
    {
        var rig = Ended(20, out _);
        rig.Ledger.StopAll();
        Assert.Equal(0, rig.Ledger.Watched);
        Assert.Equal(20, rig.Ledger.PendingRemovals);
        rig.Ledger.BeginTick(200);
        Assert.Empty(rig.Ops.Buffs);
        Assert.Null(rig.Ledger.CarrierOf(1));
    }

    [Fact]
    public void A_removal_pass_that_throws_loses_no_queued_carrier()
    {
        var rig = Started(3);
        rig.Tick(T0, 200, "surge");
        rig.Ledger.Stop("surge");
        rig.Ops.ExistsThrows = true;
        Assert.Throws<InvalidOperationException>(() => rig.Ledger.BeginTick(200));
        Assert.Equal(3, rig.Ledger.PendingRemovals);
        Assert.Equal("removing", rig.Ledger.CarrierOf(1));
        rig.Ops.ExistsThrows = false;
        rig.Ledger.BeginTick(200);
        Assert.Empty(rig.Ops.Buffs);
        Assert.Equal("empower surge stopped: 3 removed, 0 left to expire", rig.Log.Last());
    }

    [Fact]
    public void A_carried_unit_keeps_the_first_events_carrier()
    {
        var ops = FakeCarrierOps.WithBandits(3);
        var rig = new Rig(ops);
        rig.Ledger.Start("a", Bandits, T0.AddSeconds(600), T0);
        rig.Ledger.Start("b", Bandits with { Factions = ["Faction_Legion"], IncludeUnits = ["CHAR_Bandit_Thug"] }, T0.AddSeconds(600), T0);
        rig.Tick(T0, 200, "a", "b");
        Assert.Equal(3, rig.Ledger.CountFor("a"));
        Assert.Equal(0, rig.Ledger.CountFor("b"));
        Assert.Equal(3, ops.CallCount("create"));
        Assert.Equal("empower b: sweep 0 applied, 3 skipped (carried 3)", rig.Log.Last());
    }

    [Fact]
    public void Vanished_entities_are_dropped_without_an_operation()
    {
        var rig = Started(4);
        rig.Tick(T0, 200, "surge");
        long BuffOf(long unit) => rig.Ops.BuffUnit.First(kv => kv.Value == unit).Key;
        rig.Ops.Units.Remove(1);                                     // unit 1 died and took its buff
        rig.Ops.Buffs.Remove(BuffOf(1));
        rig.Ops.Units[5] = FakeCarrierOps.Bandit();
        rig.Ledger.BeginTick(200);
        rig.Ledger.TickEvent("surge", T0.AddSeconds(15));
        Assert.Equal(5, rig.Ops.CallCount("create"));                // only the new unit 5, never unit 1 again
        Assert.Equal(4, rig.Ledger.CountFor("surge"));               // unit 1's entry was dropped at the re-sweep

        var late = Started(3);
        late.Ledger.BeginTick(2);
        late.Ledger.TickEvent("surge", T0);                          // visits units 1 and 2
        late.Ops.Units.Remove(3);                                    // unit 3 vanishes before its turn
        late.Tick(T0.AddSeconds(1), 200, "surge");
        Assert.Equal(2, late.Ops.CallCount("create"));

        rig.Ledger.Stop("surge");
        rig.Ops.Buffs.Remove(BuffOf(2));                             // unit 2's carrier vanished after the stop
        rig.Tick(T0.AddSeconds(16), 200);
        Assert.Equal(3, rig.Ops.CallCount("remove"));                // units 3, 4, 5; the vanished one is not operated on
        Assert.Equal("empower surge stopped: 4 removed, 0 left to expire", rig.Log.Last());
    }

    [Fact]
    public void Skips_are_counted_by_reason()
    {
        var ops = new FakeCarrierOps();
        ops.Units[1] = FakeCarrierOps.Bandit();
        ops.Units[2] = FakeCarrierOps.Bandit() with { IsDead = true };
        ops.Units[3] = FakeCarrierOps.Bandit() with { HasVBloodUnit = true };
        ops.Units[4] = FakeCarrierOps.Bandit() with { IsDead = true };
        ops.Units[5] = FakeCarrierOps.Bandit() with { Faction = "Faction_Players" };
        var rig = new Rig(ops);
        rig.Ledger.Start("surge", Bandits, T0.AddSeconds(600), T0);
        rig.Tick(T0, 200, "surge");
        Assert.Equal("empower surge: sweep 1 applied, 4 skipped (dead 2 denied 1 vblood 1)", rig.Log.Last());
    }

    [Fact]
    public void Boot_removals_remove_only_the_buffs()
    {
        var rig = new Rig(new FakeCarrierOps());
        rig.Ops.Buffs.UnionWith([7, 8]);
        rig.Ledger.QueueBootRemovals([7, 8]);
        rig.Tick(T0, 200);
        Assert.Equal(["remove 7", "remove 8"], rig.Ops.Calls);
        Assert.Empty(rig.Log);
    }

    [Fact]
    public void BuffOf_names_the_tracked_carrier_until_it_is_dropped()
    {
        var rig = Started(1);
        Assert.Null(rig.Ledger.BuffOf(1));
        rig.Tick(T0, 200, "surge");
        Assert.Equal(rig.Ops.Buffs.Single(), rig.Ledger.BuffOf(1));
        rig.Ledger.Stop("surge");
        Assert.Null(rig.Ledger.BuffOf(1));
    }

    [Fact]
    public void A_unit_whose_stopped_carrier_is_still_queued_is_not_given_a_second_one()
    {
        var rig = Started(1);
        rig.Tick(T0, 200, "surge");
        rig.Ledger.Stop("surge");
        rig.Ops.RemoveThrows = 1;                                    // the old carrier's removal fails once and stays queued
        rig.Ledger.Start("again", Bandits, T0.AddSeconds(600), T0);
        rig.Tick(T0.AddSeconds(1), 200, "again");
        Assert.Equal(1, rig.Ops.CallCount("create"));                // skipped as carried while its removal is pending
        Assert.Equal("removing", rig.Ledger.CarrierOf(1));
        rig.Tick(T0.AddSeconds(2), 200, "again");                    // removal succeeds
        rig.Tick(T0.AddSeconds(17), 200, "again");                   // next resweep applies the new carrier
        Assert.Equal(2, rig.Ops.CallCount("create"));
        Assert.Single(rig.Ops.Buffs);
        Assert.Equal("again", rig.Ledger.CarrierOf(1));
    }

    [Fact]
    public void Stale_removal_entries_count_against_the_budget_and_boot_removals_are_deduplicated()
    {
        var rig = new Rig(new FakeCarrierOps());
        rig.Ledger.QueueBootRemovals(Enumerable.Range(1, 500).Select(i => (long)i));   // none exists any more
        rig.Ledger.QueueBootRemovals([1, 2, 3]);
        Assert.Equal(500, rig.Ledger.PendingRemovals);
        rig.Ledger.BeginTick(200);
        Assert.Equal(300, rig.Ledger.PendingRemovals);
        Assert.Empty(rig.Ops.Calls);
    }

    [Fact]
    public void LifeTime_never_rounds_above_the_seconds_left()
    {
        foreach (var s in new[] { 0.1, 1.0000001, 599.99999999, 7199.9999999, 123.456789012345, 89.5 })
        {
            var f = CarrierRecipe.LifeTimeOf(s);
            Assert.True(f <= s, $"{s}: {f}");
            Assert.True(s - f < 0.01, $"{s}: {f}");
        }
        var up = 0.1;                                                // (float)0.1 is 0.100000001, above 0.1
        Assert.True((float)up > up);
        Assert.True(CarrierRecipe.LifeTimeOf(up) < up);
    }

    [Fact]
    public void Purge_stops_every_event()
    {
        var ops = FakeCarrierOps.WithBandits(5);
        var rig = new Rig(ops);
        rig.Ledger.Start("a", Bandits, T0.AddSeconds(600), T0);
        rig.Ledger.Start("b", Bandits with { Factions = ["Faction_Legion"] }, T0.AddSeconds(600), T0);
        rig.Tick(T0, 200, "a", "b");
        rig.Ledger.StopAll();
        Assert.False(rig.Ledger.IsActive("a"));
        Assert.False(rig.Ledger.IsActive("b"));
        rig.Tick(T0.AddSeconds(1), 200);
        Assert.Empty(ops.Buffs);
        Assert.Contains("empower a stopped: 5 removed, 0 left to expire", rig.Log);
        Assert.Contains("empower b stopped: 0 removed, 0 left to expire", rig.Log);
    }
}
