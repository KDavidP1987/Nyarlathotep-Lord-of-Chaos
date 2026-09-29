using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D21: the dependency failures of the spawn services, one control per category of
/// tools/preflight-checks.json › dependencySuites.event-spawns (territory, hunt-seed, player-query, unit-recipe). Each
/// failure stays in its wave or event, is logged once per streak (SpawnHealth) and never throws.</summary>
public class SpawnsDependencyFailureTests
{
    const string Id = "raid";
    const string Unit = "CHAR_Bandit_Thug";
    static readonly DateTime T0 = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    static WaveDecision Gate(WaveFacts f) => WaveGate.Decide(f, () => [Unit], 20, 0, 150);

    static SpawnLedger Ledger() => new(new LedgerLimits(10, 10, 10, 10));

    // ---- territory

    [Fact]
    public void Territory_fails_when_out_of_range_block_kept()
    {
        var (blocks, ignored) = Territory.Build([(-1, 5), (5, 1280), (1280, 1280), (0, 0), (1279, 1279), (640, 640)]);
        Assert.Equal(3, ignored);
        Assert.Equal([(0, 0), (640, 640), (1279, 1279)], blocks.OrderBy(b => b.X).ToList());
    }

    [Fact]
    public void Territory_fails_when_failed_build_spawns_a_wave_that_needs_it()
    {
        // Point with allowTerritory false, AroundPlayer and Hunt need the map: a failed build skips each (fail closed).
        foreach (var f in new[]
        {
            new WaveFacts(1, Id, false, MapFailed: true),
            new WaveFacts(1, Id, false, MapFailed: true, Location: LocationType.AroundPlayer, AllowTerritory: true),
            new WaveFacts(1, Id, false, MapFailed: true, Behaviour: BehaviourType.Hunt, AllowTerritory: true),
        })
        {
            var d = Gate(f);
            Assert.Equal(WaveOutcome.Skip, d.Outcome);
            Assert.Equal(WaveLines.TerritoryUnknown(1, Id), d.Line);
        }
    }

    [Fact]
    public void Territory_fails_when_failed_build_keeps_hunting()
    {
        var maps = new TerritoryMaps();
        maps.Built(Id, new HashSet<(int X, int Z)> { (1, 1) });
        maps.Failed(Id);
        Assert.Null(maps.ForHunt(Id));                                         // no map: HuntAction seeds nobody
        var health = new SpawnHealth();
        Assert.True(health.Territory(maps.AnyFailed));                         // opened once
        Assert.False(health.Territory(maps.AnyFailed));
        Assert.Contains(SpawnHealth.TerritoryEntry, health.Entries);
    }

    [Fact]
    public void Territory_passes_rebuilt_map_clears_territory_unknown()
    {
        var maps = new TerritoryMaps();
        var health = new SpawnHealth();
        maps.Failed(Id);
        health.Territory(maps.AnyFailed);
        maps.Built(Id, new HashSet<(int X, int Z)> { (2, 3) });
        Assert.False(health.Territory(maps.AnyFailed));
        Assert.Empty(health.Entries);
        Assert.NotNull(maps.ForHunt(Id));
        // allowTerritory on a plain Point does not need the map: it spawns even while the build fails.
        Assert.Equal(WaveOutcome.Spawn, Gate(new WaveFacts(1, Id, false, MapFailed: true, AllowTerritory: true)).Outcome);
    }

    [Fact]
    public void Territory_empty_no_castle_hearts()
    {
        var (blocks, ignored) = Territory.Build([]);
        Assert.Empty(blocks);
        Assert.Equal(0, ignored);
        Assert.False(Territory.IsClaimed(blocks, 0, 0));
    }

    // ---- hunt-seed

    [Fact]
    public void HuntSeed_fails_when_failure_ends_the_wave_or_repeats_its_line()
    {
        var ledger = Ledger();
        var life = new UnitLifetime(T0.AddMinutes(5), 400);
        ledger.Request(Unit, Id, 2, life, UnitTuning.None, i => (i, 0, 0), hunt: new HuntTag(0, 0, 30));
        var orders = ledger.TakeSpawns();
        Assert.All(orders, o => Assert.Equal(new HuntTag(0, 0, 30), o.Hunt));
        Assert.True(ledger.Confirm(orders[0], 1, T0));
        Assert.True(ledger.Confirm(orders[1], 2, T0));

        var health = new SpawnHealth();
        var logged = 0;
        for (var tick = 0; tick < 3; tick++)
            if (health.Failing(SpawnFailure.HuntSeed, Id)) logged++;       // three failing ticks, one line
        Assert.Equal(1, logged);
        Assert.Equal(2, ledger.Tracked);                                       // the wave keeps its units
        Assert.All(ledger.Units, u => Assert.Equal(life.DueUtc, u.DueUtc));
    }

    [Fact]
    public void HuntSeed_fails_when_partial_write_leaves_a_record_the_buffer_lacks()
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(1, Id, new AggroSeed(7, 500, 1));
        // the write of target 8 threw before its record: only 7 is recorded, and 7 is gone from the buffer
        var (kept, left) = seeds.Reconcile(1, new Dictionary<long, AggroSeed>());
        Assert.Equal((0, 0), (kept, left));
        Assert.Empty(seeds.SeededOn(1));
    }

    [Fact]
    public void HuntSeed_passes_recovered_seeding_clears_entry()
    {
        var health = new SpawnHealth();
        health.Failing(SpawnFailure.HuntSeed, Id);
        health.Recovered(SpawnFailure.HuntSeed, Id);
        Assert.Empty(health.Entries);
        Assert.True(health.Failing(SpawnFailure.HuntSeed, Id));                // a new streak logs again
    }

    [Fact]
    public void HuntSeed_empty_no_hunt_units()
    {
        var seeds = new HuntSeeds();
        Assert.Equal(0, seeds.Count);
        Assert.Equal((0, 0), seeds.Reconcile(1, new Dictionary<long, AggroSeed>()));
        Assert.Empty(new SpawnHealth().Entries);
    }

    // ---- player-query

    [Fact]
    public void PlayerQuery_fails_when_nan_position_is_picked()
    {
        var players = new[]
        {
            new PickCandidate(float.NaN, 0, 5, true, true, false), new PickCandidate(20000, 0, 5, true, true, false),
            new PickCandidate(100, float.NaN, 100, true, true, false), new PickCandidate(100, float.PositiveInfinity, 100, true, true, false),
            new PickCandidate(100, 0, float.NegativeInfinity, true, true, false),                  // A60: every axis, the height included
        };
        var r = PlayerPick.Choose(players, new ScriptedRandom(), 10, 20, (_, _) => false, null);
        Assert.Equal(PickOutcome.NoEligible, r.Outcome);
        Assert.False(PlayerPosition.Usable(100, 20000, 100));
    }

    [Fact]
    public void PlayerQuery_fails_when_failed_query_or_region_read_spawns()
    {
        var player = new[] { new PickCandidate(100, 0, 100, true, true, false) };
        var claimedThrows = PlayerPick.Choose(player, new ScriptedRandom(0.1, 0.1, 0.1), 10, 20, (_, _) => throw new InvalidOperationException("territory"), null);
        var regionThrows = PlayerPick.Choose(player, new ScriptedRandom(0.1, 0.1, 0.1), 10, 20, (_, _) => false, (_, _) => throw new InvalidOperationException("region"));
        foreach (var r in new[] { claimedThrows, regionThrows })
        {
            Assert.Equal(PickOutcome.QueryFailed, r.Outcome);
            var d = Gate(new WaveFacts(3, Id, false, Location: LocationType.AroundPlayer, Pick: r.Outcome));
            Assert.Equal(WaveOutcome.Skip, d.Outcome);
            Assert.Equal(WaveLines.PlayerQueryFailed(3, Id), d.Line);
            Assert.Empty(d.Units);
        }
        var health = new SpawnHealth();
        Assert.True(health.Failing(SpawnFailure.PlayerQuery, Id));
        Assert.False(health.Failing(SpawnFailure.PlayerQuery, Id));
        Assert.Equal([SpawnHealth.FailingEntry(SpawnFailure.PlayerQuery, Id)], health.Entries);
    }

    [Fact]
    public void PlayerQuery_passes_readable_player_is_picked()
    {
        var player = new[] { new PickCandidate(100, 7, 100, true, true, false) };
        var r = PlayerPick.Choose(player, new ScriptedRandom(0.1, 0.1, 0.1), 10, 20, (_, _) => false, null);
        Assert.Equal(PickOutcome.Picked, r.Outcome);
        Assert.Equal(7, r.Centre.Y);
        Assert.Equal(WaveOutcome.Spawn, Gate(new WaveFacts(3, Id, false, Location: LocationType.AroundPlayer, Pick: r.Outcome)).Outcome);
    }

    [Fact]
    public void PlayerQuery_empty_no_players()
    {
        var r = PlayerPick.Choose([], new ScriptedRandom(), 10, 20, (_, _) => false, null);
        Assert.Equal(PickOutcome.NoEligible, r.Outcome);
        Assert.Equal(WaveLines.NoEligiblePlayer(3, Id), Gate(new WaveFacts(3, Id, false, Location: LocationType.AroundPlayer, Pick: r.Outcome)).Line);
    }

    // ---- unit-recipe

    [Fact]
    public void UnitRecipe_fails_when_failing_unit_untracks_the_others_or_stays_tracked()
    {
        var ledger = Ledger();
        var life = new UnitLifetime(T0.AddMinutes(5), 400);
        ledger.Request(Unit, Id, 3, life, new UnitTuning(new LevelArg(true, 2), []), i => (i, 0, 0));
        var orders = ledger.TakeSpawns();
        Assert.True(ledger.Confirm(orders[0], 1, T0));
        ledger.Fail(orders[1]);                                                // its recipe threw: discarded, not tracked
        Assert.True(ledger.Confirm(orders[2], 3, T0));
        Assert.Equal([1L, 3L], ledger.Units.Select(u => u.Key).OrderBy(k => k).ToList());
        Assert.Equal(2, ledger.Occupied);                                      // its slot is free again
        Assert.All(ledger.Units, u => Assert.Equal(life.DueUtc, u.DueUtc));

        var health = new SpawnHealth();
        Assert.True(health.Failing(SpawnFailure.UnitSetup, Id));
        Assert.False(health.Failing(SpawnFailure.UnitSetup, Id));              // once per streak
    }

    [Fact]
    public void UnitRecipe_passes_every_recipe_applies()
    {
        var ledger = Ledger();
        var tuning = new UnitTuning(new LevelArg(false, 40), [new StatScale(TuningStat.MaxHealth, 0.5f)]);
        ledger.Request(Unit, Id, 2, new UnitLifetime(T0.AddMinutes(5), 400), tuning, i => (i, 0, 0), loot: true);
        var orders = ledger.TakeSpawns();
        Assert.All(orders, o => Assert.Equal((tuning, false), (o.Tuning, o.ClearDrops)));
        Assert.True(ledger.Confirm(orders[0], 1, T0));
        Assert.True(ledger.Confirm(orders[1], 2, T0));
        Assert.Equal(2, ledger.Tracked);
        var health = new SpawnHealth();
        health.Recovered(SpawnFailure.UnitSetup, Id);
        Assert.Empty(health.Entries);
    }

    [Fact]
    public void UnitRecipe_empty_zero_units_requested()
    {
        var ledger = Ledger();
        Assert.Equal(0, ledger.Request(Unit, Id, 0, new UnitLifetime(T0, 1), UnitTuning.None, i => (i, 0, 0)).Queued);
        Assert.Empty(ledger.TakeSpawns());
    }
}
