using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D20: a failing carrier operation is contained. A staged Apply that throws after create
/// queues its carrier for removal and never tracks it; a Remove is retried three times, then expired once, then left to
/// its LifeTime; a failing query stays the event's own fault; an unreadable owner is skipped as owned.</summary>
public partial class DependencyFailureTests
{
    static readonly DateTime C0 = Zones.Utc(2026, 9, 26, 21, 0);
    static readonly EmpowerAction Surge = new(["Faction_Bandits"], [], [], false, new EmpowerStats(MaxHealth: 1.5));

    static (FakeCarrierOps Ops, CarrierLedger Ledger, List<string> Log) Carriers(int units)
    {
        var ops = FakeCarrierOps.WithBandits(units);
        var log = new List<string>();
        var ledger = new CarrierLedger(ops, log.Add);
        ledger.Start("surge", Surge, C0.AddSeconds(600), C0);
        return (ops, ledger, log);
    }

    static void Tick(CarrierLedger ledger, DateTime now, int budget = 200, string? id = "surge")
    {
        ledger.BeginTick(budget);
        if (id is not null) ledger.TickEvent(id, now);
    }

    [Theory]
    [InlineData("lifetime")]
    [InlineData("strip")]
    [InlineData("modifiers")]
    public void A_throw_after_create_queues_the_carrier_and_never_tracks_it(string stage)
    {
        var (ops, ledger, log) = Carriers(1);
        ops.ThrowAt = stage;
        Tick(ledger, C0);
        Assert.Equal(1, ops.CallCount("create"));
        Assert.Equal("removing", ledger.CarrierOf(1));               // untracked, but no second carrier until it is gone
        Assert.Equal(0, ledger.CountFor("surge"));
        Assert.Equal(1, ledger.PendingRemovals);
        Assert.Contains($"empower surge: apply failed: {stage} failed", log);
        Tick(ledger, C0.AddSeconds(1), id: null);
        Assert.Empty(ops.Buffs);                                     // removed through Remove on the next tick
        Assert.Null(ledger.CarrierOf(1));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("mark")]
    public void A_throw_at_create_and_mark_leaves_nothing_in_the_world_or_the_ledger(string stage)
    {
        var (ops, ledger, log) = Carriers(1);
        ops.ThrowAt = stage;
        Tick(ledger, C0);
        Assert.Empty(ops.Buffs);                                     // nothing unmarked exists to outlive a restart (A2)
        Assert.Equal(0, ledger.PendingRemovals);
        Assert.Equal(0, ledger.Carriers);
        Assert.Contains($"empower surge: apply failed: {stage} failed", log);
    }

    [Fact]
    public void A_resweep_whose_query_throws_leaves_the_ledger_unchanged()
    {
        var (ops, ledger, _) = Carriers(3);
        Tick(ledger, C0);
        ops.Buffs.Remove(ops.BuffUnit.First(kv => kv.Value == 1).Key);   // unit 1's carrier vanished
        ops.QueryThrows = true;
        ledger.BeginTick(200);
        Assert.Throws<InvalidOperationException>(() => ledger.TickEvent("surge", C0.AddSeconds(15)));
        Assert.Equal(3, ledger.Carriers);                            // not pruned before the failed query
        ops.QueryThrows = false;
        Tick(ledger, C0.AddSeconds(16));
        Assert.Equal(3, ledger.CountFor("surge"));                   // pruned, then unit 1 re-applied
        Assert.Equal(4, ops.CallCount("create"));
    }

    [Fact]
    public void One_failing_unit_does_not_stop_the_sweep_and_a_streak_logs_once()
    {
        var (ops, ledger, log) = Carriers(6);
        ops.ThrowAt = "strip";
        ops.ThrowFor.UnionWith([2, 3, 5]);
        Tick(ledger, C0);
        Assert.Equal(3, ledger.CountFor("surge"));                   // units 1, 4 and 6
        Assert.Equal(2, log.Count(l => l.Contains("apply failed")));  // the streak 2-3, then 5 after 4 succeeded
        Assert.Equal("empower surge: sweep 3 applied, 0 skipped, 3 failed", log.Last());
    }

    [Fact]
    public void A_failing_remove_is_retried_three_times_then_expired_once()
    {
        var (ops, ledger, log) = Carriers(1);
        Tick(ledger, C0);
        ledger.Stop("surge");
        ops.RemoveThrows = int.MaxValue;
        for (var i = 1; i <= 6; i++) Tick(ledger, C0.AddSeconds(i), id: null);
        Assert.Equal(4, ops.CallCount("remove"));
        Assert.Equal(1, ops.CallCount("expire"));
        Assert.Empty(ops.Buffs);
        Assert.Equal("empower surge stopped: 1 removed, 0 left to expire", log.Last());
    }

    [Fact]
    public void A_remove_that_recovers_needs_no_fallback()
    {
        var (ops, ledger, _) = Carriers(1);
        Tick(ledger, C0);
        ledger.Stop("surge");
        ops.RemoveThrows = 2;
        for (var i = 1; i <= 4; i++) Tick(ledger, C0.AddSeconds(i), id: null);
        Assert.Equal(3, ops.CallCount("remove"));
        Assert.Equal(0, ops.CallCount("expire"));
        Assert.Empty(ops.Buffs);
    }

    [Fact]
    public void A_carrier_whose_fallback_also_fails_is_left_to_expire_and_counted()
    {
        var (ops, ledger, log) = Carriers(2);
        Tick(ledger, C0);
        ledger.Stop("surge");
        ops.RemoveThrows = int.MaxValue;
        ops.ExpireThrows = true;
        for (var i = 1; i <= 6; i++) Tick(ledger, C0.AddSeconds(i), id: null);
        Assert.Equal(8, ops.CallCount("remove"));
        Assert.Equal(2, ops.CallCount("expire"));
        Assert.Equal(0, ledger.PendingRemovals);
        Assert.Equal(2, log.Count(l => l.StartsWith("empower surge: carrier removal failed, left to expire", StringComparison.Ordinal)));
        Assert.Equal("empower surge stopped: 0 removed, 2 left to expire", log.Last());
    }

    [Fact]
    public void A_failing_query_is_the_events_own_fault_and_changes_nothing()
    {
        var (ops, ledger, _) = Carriers(3);
        ledger.Start("other", Surge with { Factions = ["Faction_Legion"] }, C0.AddSeconds(600), C0);
        ops.QueryThrows = true;
        ledger.BeginTick(200);
        Assert.Throws<InvalidOperationException>(() => ledger.TickEvent("surge", C0));
        Assert.False(ledger.SweepInProgress("surge"));
        Assert.True(ledger.IsActive("surge"));
        ops.QueryThrows = false;
        ledger.TickEvent("other", C0);                               // the next event in the same tick still runs
        Assert.Equal(2, ops.Queries);
        Tick(ledger, C0.AddSeconds(1));                              // and the failed one retries on the next tick
        Assert.Equal(3, ledger.CountFor("surge"));
    }

    [Theory]
    [InlineData(OwnerLink.Unresolvable, OwnerLink.Absent, OwnerLink.NotPlayer)]
    [InlineData(OwnerLink.Absent, OwnerLink.Unresolvable, OwnerLink.NotPlayer)]
    [InlineData(OwnerLink.Absent, OwnerLink.Absent, OwnerLink.Unresolvable)]
    public void A_unit_with_an_unreadable_owner_follower_or_team_is_never_empowered(OwnerLink follower, OwnerLink owner, OwnerLink team)
    {
        var (ops, ledger, log) = Carriers(0);
        ops.Units[1] = FakeCarrierOps.Bandit() with { OwnedByPlayer = Ownership.Decide(new OwnershipFacts(follower, owner, team)) };
        Tick(ledger, C0);
        Assert.Equal(0, ops.CallCount("create"));
        Assert.Equal("empower surge: sweep 0 applied, 1 skipped (owned 1)", log.Last());
    }
}
