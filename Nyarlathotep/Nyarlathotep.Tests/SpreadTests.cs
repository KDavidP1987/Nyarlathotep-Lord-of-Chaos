using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D7: the apocalypse family's spread draws perKillMin..perKillMax units (0-3) per qualifying kill,
/// each a member of the spread pool, inside roamers.max, the per-minute limit and the free tracked slots; pool entries in
/// the units draw a member of their pool; a 0.13.x `perKill` n reads as n..n.</summary>
public class SpreadTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    const string Pools = "\"pools\": { \"troops\": [ \"" + Apo.Zombie + "\", \"" + Apo.Thug + "\" ], \"elite\": [ \"" + Apo.Deadeye + "\" ] }";
    const string PoolUnits = "[ { \"pool\": \"troops\", \"count\": 8 }, { \"pool\": \"elite\", \"count\": 1, \"modifiers\": { \"level\": 40 } } ]";

    static string Roamers(string kill = "\"perKillMin\": 1, \"perKillMax\": 3", int max = 100, int perMinute = 30, string? pool = "elite") =>
        "{ \"perHotspot\": 3" + (kill.Length > 0 ? ", " + kill : "") + ", \"max\": " + max + ", \"perMinute\": " + perMinute + ", \"huntRange\": 40" +
        (pool is null ? "" : ", \"pool\": \"" + pool + "\"") + " }";

    static ApocalypseAction Action(string? roamers = null, string units = PoolUnits, string? extra = Pools) =>
        Apo.Def(Apo.Action(units: units, roamers: roamers ?? Roamers(), extra: extra)).Apocalypse!;

    static string? Refusal(string action)
    {
        var r = EventValidator.Parse(Json.File(Apo.Event(action: action)), FakeUnits.Default(), regions: FakeRegions.All());
        return Assert.Single(r.Set.All).DisabledReason;
    }

    static readonly ScoreSides Kill = new(null, new OurUnit("apo", 101, T0));

    [Fact]
    public void Spread_fails_when_a_draw_leaves_min_max()
    {
        var roamers = Action().Roamers;
        Assert.Equal((1, 3), (roamers.PerKillMin, roamers.PerKillMax));
        var rng = new SystemRandom(new Random(7));
        var draws = Enumerable.Range(0, 3000).Select(_ => SpreadGate.Draw(roamers, rng)).ToList();
        Assert.All(draws, d => Assert.InRange(d, 1, 3));
        foreach (var n in new[] { 1, 2, 3 }) Assert.InRange(draws.Count(d => d == n), 850, 1150);     // uniform
        // one kill queues its draw
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(units: PoolUnits, roamers: Roamers(), extra: Pools)), T0));
        var w = new ApoWorld { Random = new SystemRandom(new Random(3)) };
        for (var i = 0; i < 20; i++)
        {
            var before = w.Ledger.PendingSpawns;
            run.SpreadOn(w, Kill, (i * 10, 0), T0.AddSeconds(i * 4));
            Assert.InRange(w.Ledger.PendingSpawns - before, 1, 3);
        }
    }

    [Fact]
    public void Spread_fails_when_a_member_outside_the_pool_spawns()
    {
        var a = Apo.Active(Apo.Def(Apo.Action(units: PoolUnits, roamers: Roamers(), extra: Pools)), T0);
        var run = Apo.Run(a);
        var w = new ApoWorld { Random = new SystemRandom(new Random(11)) };
        for (var i = 0; i < 30; i++) run.SpreadOn(w, Kill, (i, 0), T0.AddSeconds(i * 10));
        var spread = w.Ledger.TakeSpawns();
        Assert.NotEmpty(spread);
        Assert.All(spread, o => Assert.Equal(Apo.Deadeye, o.Prefab));                  // the elite pool only
        Assert.All(spread, o => Assert.NotNull(o.Tuning.Level));                       // the elite entry's modifiers
        // the units' pool entries draw members of their pools, never a pool name or an empty prefab
        var top = Apo.Run(a);
        var w2 = new ApoWorld { Random = new SystemRandom(new Random(5)) };
        top.TopUp(w2, T0);
        var units = new List<SpawnOrder>();
        for (var b = w2.Ledger.TakeSpawns(); b.Count > 0; b = w2.Ledger.TakeSpawns()) units.AddRange(b);
        Assert.Equal(6, units.Count);
        Assert.All(units, o => Assert.Contains(o.Prefab, new[] { Apo.Zombie, Apo.Thug, Apo.Deadeye }));
        var horde = HordeSchedule.Units(a.Definition.Apocalypse!, 1, 50, 0, 200, new SystemRandom(new Random(9)), []);
        Assert.All(horde, u => Assert.Contains(u.Unit.Prefab, new[] { Apo.Zombie, Apo.Thug, Apo.Deadeye }));
        Assert.Contains(horde, u => u.Unit.Prefab == Apo.Zombie);
        Assert.Contains(horde, u => u.Unit.Prefab == Apo.Thug);
    }

    [Fact]
    public void Spread_fails_when_the_max_the_minute_or_the_free_slots_are_passed()
    {
        // a chain: every living unit kills every 2 s and spreads in turn
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(units: PoolUnits, roamers: Roamers(max: 40, perMinute: 9), extra: Pools)), T0));
        var w = new ApoWorld { Random = new SystemRandom(new Random(1)) };
        var at = T0;
        run.SpreadOn(w, Kill, (0, 0), at);
        for (var i = 0; i < 300; i++)
        {
            at = at.AddSeconds(2);
            w.SpawnAll(run, at);
            foreach (var u in w.Ledger.Units.ToList()) run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", u.Wave, u.SpawnedUtc)), (u.Key, 0), at);
            Assert.True(run.RoamersPerHotspot(w).Sum() <= 40);
            Assert.True(run.Spread.InWindow(at) <= 9);
        }
        Assert.Equal(40, run.RoamersPerHotspot(w).Sum());                             // the chain reaches max and stops there
        // the free tracked slots: 2 free, a draw of 3 queues 2
        var gate = new SpreadGate();
        var r = Action().Roamers;
        Assert.Equal(2, gate.Allow(T0, r, 0, 2, 3));
        Assert.Equal(0, gate.Allow(T0, r, 0, 0, 3));
        var full = Apo.Run(Apo.Active(Apo.Def(Apo.Action(units: PoolUnits, roamers: Roamers(), extra: Pools)), T0));
        var w2 = new ApoWorld(maxTracked: 2) { Random = new SystemRandom(new Random(2)) };
        for (var i = 0; i < 10; i++) full.SpreadOn(w2, Kill, (i, 0), T0.AddSeconds(i));
        Assert.Equal(2, w2.Ledger.PendingSpawns);
    }

    [Fact]
    public void Spread_fails_when_a_0_13_perKill_reads_differently()
    {
        var old = Apo.Def().Apocalypse!.Roamers;                                      // perKill 2, as 0.13.1 wrote it
        Assert.Equal(new ApocalypseRoamers(3, 2, 12, 4, 40), old);
        Assert.Equal((2, 2, 2), (old.PerKill, old.PerKillMin, old.PerKillMax));
        Assert.Null(old.Pool);
        var rng = new SystemRandom(new Random(4));
        Assert.All(Enumerable.Range(0, 100), _ => Assert.Equal(2, SpreadGate.Draw(old, rng)));
        Assert.Equal("roamers: 3 per hotspot, +2 per kill, max 12, 4 spread per minute, hunt range 40",
            EventLines.ApocalypseLines(Apo.Def().Apocalypse!).Single(l => l.StartsWith("roamers:")));
        // the 0.13.x spread line, unchanged
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Equal("apocalypse apo spread: +2 (2/12, 2/4 this minute)", run.SpreadOn(new ApoWorld(), Kill, (5, 5), T0));
    }

    [Fact]
    public void Spread_fails_when_a_draw_form_or_a_pool_is_wrong()
    {
        Assert.Equal(EventValidator.KillDrawBoth, Refusal(Apo.Action(units: PoolUnits, extra: Pools,
            roamers: Roamers("\"perKill\": 2, \"perKillMin\": 1, \"perKillMax\": 3"))));
        Assert.Equal(EventValidator.PerKillMaxRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMin\": 1"))));
        Assert.Equal(EventValidator.PerKillMinRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMax\": 2"))));
        Assert.Equal(EventValidator.PerKillMinRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMin\": 4, \"perKillMax\": 4"))));
        Assert.Equal(EventValidator.PerKillMaxRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMin\": 2, \"perKillMax\": 1"))));
        Assert.Equal(EventValidator.PerKillMaxRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMin\": 1, \"perKillMax\": 4"))));
        Assert.Equal(EventValidator.PerKillRule, Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers(""))));
        Assert.Equal(EventValidator.NoPool("action.roamers", "ghosts"), Refusal(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers(pool: "ghosts"))));
        Assert.Equal(EventValidator.NoPool("action.roamers", "elite"), Refusal(Apo.Action(roamers: Roamers(pool: "elite"))));
        // a pool neither the units nor the spread name
        Assert.Equal(EventValidator.PoolUnused("elite"), Refusal(Apo.Action(units: "[ { \"pool\": \"troops\", \"count\": 8 } ]", extra: Pools, roamers: Roamers(pool: null))));
        Assert.Equal(EventValidator.NoPool("action.units.1", "ghosts"), Refusal(Apo.Action(units: "[ { \"pool\": \"ghosts\", \"count\": 8 } ]", extra: Pools)));
    }

    [Fact]
    public void Spread_passes_a_range_from_a_pool_and_shows_it()
    {
        var a = Action();
        Assert.Equal(new ApocalypseRoamers(3, 1, 100, 30, 40, 3, "elite"), a.Roamers);
        Assert.Equal(["CHAR_Undead_SkeletonSoldier_Base", "CHAR_Bandit_Thug"], a.Pools!["troops"]);
        var lines = EventLines.ApocalypseLines(a).ToList();
        Assert.Contains("roamers: 3 per hotspot, +1-3 per kill from elite, max 100, 30 spread per minute, hunt range 40", lines);
        Assert.Contains("pools: troops (2 units): CHAR_Undead_SkeletonSoldier_Base, CHAR_Bandit_Thug", lines);
        Assert.Contains("units: 8 from troops, 1 from elite L40", lines);
        // the spread pool alone uses a pool
        Assert.Null(Refusal(Apo.Action(units: "[ { \"pool\": \"troops\", \"count\": 8 } ]", extra: Pools, roamers: Roamers())));
        // a range without a pool draws from the units
        Assert.Null(Action(Roamers(pool: null), units: "[ " + Apo.Unit() + ", { \"pool\": \"troops\", \"count\": 2 } ]",
            extra: "\"pools\": { \"troops\": [ \"" + Apo.Thug + "\" ] }").Roamers.Pool);
    }

    [Fact]
    public void Spread_empty_a_zero_draw_spreads_nothing()
    {
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(units: PoolUnits, extra: Pools, roamers: Roamers("\"perKillMin\": 0, \"perKillMax\": 0"))), T0));
        var w = new ApoWorld();
        for (var i = 0; i < 10; i++) Assert.Null(run.SpreadOn(w, Kill, (i, 0), T0.AddSeconds(i)));
        Assert.Equal(0, w.Ledger.PendingSpawns);
        Assert.Equal("roamers: 3 per hotspot, +0 per kill from elite, max 100, 30 spread per minute, hunt range 40",
            EventLines.ApocalypseLines(run.Action).Single(l => l.StartsWith("roamers:")));
    }
}
