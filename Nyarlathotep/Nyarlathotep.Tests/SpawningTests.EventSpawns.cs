using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>A random source that returns a fixed sequence and counts its draws; a draw past the end throws, so a test
/// that expects no roll fails loudly (event-spawns D8, D16).</summary>
sealed class ScriptedRandom(params double[] values) : IRandom
{
    int _next;
    public int Calls => _next;

    public double NextDouble() =>
        _next < values.Length ? values[_next++] : throw new InvalidOperationException($"no draw scripted at {_next + 1}");
}

/// <summary>event-spawns D8 (WaveRoll), D9 (Tuning), D11 (Loot), D13 (Hunt), D16 (PlayerPick) and D17 (Territory).</summary>
public partial class SpawningTests
{
    const string A = "CHAR_Bandit_Thug", B = "CHAR_Bandit_Deadeye";

    static WaveDecision Gate(WaveFacts facts, IReadOnlyList<string> rolled, int maxPerWave = 20, int occupied = 0, int maxTracked = 150) =>
        WaveGate.Decide(facts, () => rolled, maxPerWave, occupied, maxTracked);

    static WaveFacts Facts(PickOutcome? pick = null, bool claimed = false, bool allow = false, LocationType location = LocationType.Point) =>
        new(1, "raid", false, Location: location, Pick: pick, CentreClaimed: claimed, AllowTerritory: allow);

    // ---- D8 WaveRoll

    [Fact]
    public void WaveRoll_fails_when_chance_one_loses_a_copy()
    {
        var rng = new ScriptedRandom();
        Assert.Equal(Enumerable.Repeat(A, 5), WaveRoll.Expand([new UnitEntry(A, 5)], rng));
        Assert.Equal(Enumerable.Repeat(A, 3), WaveRoll.Expand([new UnitEntry(A, 3, 1.0)], rng));
        Assert.Equal(0, rng.Calls);                                            // a chance of 1.0 draws nothing
    }

    [Fact]
    public void WaveRoll_fails_when_copies_rolled_as_one()
    {
        var rng = new ScriptedRandom(0.1, 0.9, 0.49, 0.5);
        Assert.Equal([A, A], WaveRoll.Expand([new UnitEntry(A, 4, 0.5)], rng));
        Assert.Equal(4, rng.Calls);                                            // one draw per copy
    }

    [Fact]
    public void WaveRoll_fails_when_order_changes()
    {
        var rolled = WaveRoll.Expand([new UnitEntry(A, 2), new UnitEntry(B, 3, 0.5), new UnitEntry(A, 1)], new ScriptedRandom(0.9, 0.2, 0.3));
        Assert.Equal([A, A, B, B, A], rolled);
        Assert.Equal([new UnitEntry(A, 2), new UnitEntry(B, 2), new UnitEntry(A, 1)], WaveRoll.Group(rolled));
    }

    [Fact]
    public void WaveRoll_fails_when_caps_clamp_pre_roll_count()
    {
        var draws = Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? 0.1 : 0.9).ToArray();
        var rolled = WaveRoll.Expand([new UnitEntry(A, 30, 0.5)], new ScriptedRandom(draws));
        Assert.Equal(15, rolled.Count);
        var d = Gate(Facts(), rolled);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal(15, d.Units.Sum(u => u.Count));                           // 30 asked, 15 rolled: under MaxUnitsPerWave 20
        Assert.Empty(d.CapLines);

        var many = Gate(Facts(), Enumerable.Repeat(A, 25).ToList());
        Assert.Equal(20, many.Units.Sum(u => u.Count));
        Assert.Equal(["clamped by MaxUnitsPerWave: 25 -> 20"], many.CapLines);
        var full = Gate(Facts(), rolled, occupied: 140);
        Assert.Equal(10, full.Units.Sum(u => u.Count));
        Assert.Equal(["skipped by MaxTrackedUnits: 5 of 15"], full.CapLines);
    }

    [Fact]
    public void WaveRoll_fails_when_zero_roll_spawns_or_throws()
    {
        var rolled = WaveRoll.Expand([new UnitEntry(A, 3, 0.05)], new ScriptedRandom(0.9, 0.5, 0.05));
        Assert.Empty(rolled);
        var d = Gate(Facts() with { Wave = 2 }, rolled);
        Assert.Equal(WaveOutcome.ZeroRolled, d.Outcome);
        Assert.Equal("wave 2 of raid: 0 units rolled", d.Line);
        Assert.Empty(d.Units);
        Assert.Empty(d.CapLines);
    }

    [Fact]
    public void WaveRoll_passes_independent_copies_in_entry_order()
    {
        var rng = new ScriptedRandom(0.04, 0.05, 0.5, 0.99);
        var rolled = WaveRoll.Expand([new UnitEntry(B, 2, 0.05), new UnitEntry(A, 2), new UnitEntry(B, 2, 0.6)], rng);
        Assert.Equal([B, A, A, B], rolled);
        Assert.Equal(4, rng.Calls);
        var seeded = WaveRoll.Expand([new UnitEntry(A, 50, 0.3)], new SystemRandom(new Random(7)));
        Assert.InRange(seeded.Count, 5, 30);
    }

    [Fact]
    public void WaveRoll_empty_unit_list()
    {
        var rng = new ScriptedRandom();
        Assert.Empty(WaveRoll.Expand([], rng));
        Assert.Empty(WaveRoll.Group([]));
        var d = Gate(Facts(), WaveRoll.Expand([], rng));
        Assert.Equal((WaveOutcome.ZeroRolled, "wave 1 of raid: 0 units rolled"), (d.Outcome, d.Line));
        Assert.Empty(d.Units);
    }

    // ---- D9 Tuning

    [Theory]
    [InlineData("maxHealth", new[] { TuningStat.MaxHealth })]
    [InlineData("power", new[] { TuningStat.PhysicalPower, TuningStat.SpellPower })]
    [InlineData("moveSpeed", new[] { TuningStat.MovementSpeed })]
    [InlineData("attackSpeed", new[] { TuningStat.PrimaryAttackSpeed, TuningStat.AbilityAttackSpeed })]
    public void Tuning_fails_when_stat_maps_to_another(string key, TuningStat[] stats)
    {
        var m = key switch
        {
            "maxHealth" => new SpawnModifiers(MaxHealth: 1.5),
            "power" => new SpawnModifiers(Power: 1.5),
            "moveSpeed" => new SpawnModifiers(MoveSpeed: 1.5),
            _ => new SpawnModifiers(AttackSpeed: 1.5),
        };
        Assert.Equal(stats, SpawnTuning.TuningFrom(m).Stats.Select(s => s.Stat));
    }

    [Theory]
    [InlineData(0.5, -0.5f)]
    [InlineData(1.2, 0.2f)]
    [InlineData(1.25, 0.25f)]
    [InlineData(3.0, 2.0f)]
    public void Tuning_fails_when_value_not_multiplier_minus_one(double multiplier, float value)
    {
        var t = SpawnTuning.TuningFrom(new SpawnModifiers(MaxHealth: multiplier, Power: multiplier, MoveSpeed: multiplier, AttackSpeed: multiplier));
        Assert.Equal(6, t.Stats.Count);
        Assert.All(t.Stats, s => Assert.Equal(value, s.Value, 5));
    }

    [Fact]
    public void Tuning_fails_when_power_or_attack_speed_not_two_entries()
    {
        Assert.Equal(2, SpawnTuning.TuningFrom(new SpawnModifiers(Power: 2.0)).Stats.Count);
        Assert.Equal(2, SpawnTuning.TuningFrom(new SpawnModifiers(AttackSpeed: 0.75)).Stats.Count);
        Assert.Single(SpawnTuning.TuningFrom(new SpawnModifiers(MaxHealth: 2.0)).Stats);
        Assert.Single(SpawnTuning.TuningFrom(new SpawnModifiers(MoveSpeed: 2.0)).Stats);
    }

    [Fact]
    public void Tuning_fails_when_one_gives_an_entry()
    {
        var t = SpawnTuning.TuningFrom(new SpawnModifiers(MaxHealth: 1.0, Power: 1.5, MoveSpeed: 1.0, AttackSpeed: 1.0));
        Assert.Equal([TuningStat.PhysicalPower, TuningStat.SpellPower], t.Stats.Select(s => s.Stat));
        var level = SpawnTuning.TuningFrom(new SpawnModifiers(Level: 30));
        Assert.Empty(level.Stats);
        Assert.Equal(new LevelArg(false, 30), level.Level);
        Assert.False(level.IsNone);
    }

    [Theory]
    [InlineData(3, 40, 43)]
    [InlineData(5, 118, 120)]
    [InlineData(-5, 3, 1)]
    [InlineData(0, 60, 60)]
    public void Tuning_fails_when_level_delta_not_from_prefab_level(int delta, int prefabLevel, int resolved)
    {
        var t = SpawnTuning.TuningFrom(new SpawnModifiers(LevelDelta: delta));
        Assert.Equal(new LevelArg(true, delta), t.Level);
        Assert.Equal(resolved, t.Level!.Value.Resolve(prefabLevel));
        Assert.InRange(t.Level.Value.Resolve(prefabLevel), 1, 120);
        Assert.Equal(30, SpawnTuning.TuningFrom(new SpawnModifiers(Level: 30)).Level!.Value.Resolve(prefabLevel));   // absolute: the prefab's does not count
    }

    [Fact]
    public void Tuning_passes_every_modifier()
    {
        var t = SpawnTuning.TuningFrom(new SpawnModifiers(null, 2, 1.2, 1.5, 0.8, 2.0));
        Assert.Equal(new LevelArg(true, 2), t.Level);
        Assert.Equal(
            [
                new StatScale(TuningStat.MaxHealth, 0.2f), new StatScale(TuningStat.PhysicalPower, 0.5f), new StatScale(TuningStat.SpellPower, 0.5f),
                new StatScale(TuningStat.MovementSpeed, -0.2f), new StatScale(TuningStat.PrimaryAttackSpeed, 1.0f),
                new StatScale(TuningStat.AbilityAttackSpeed, 1.0f),
            ],
            t.Stats.Select(s => s with { Value = MathF.Round(s.Value, 4) }));
    }

    [Fact]
    public void Tuning_passes_spawn_command_same_function()
    {
        var fromEvent = SpawnTuning.TuningFrom(new SpawnModifiers(Level: 40, MaxHealth: 2.0, Power: 1.5));
        var fromCommand = SpawnTuning.TuningFrom(new LevelArg(false, 40), maxHealth: 2.0, power: 1.5);
        Assert.Equal(fromEvent.Level, fromCommand.Level);
        Assert.Equal(fromEvent.Stats, fromCommand.Stats);
        Assert.Same(UnitTuning.None, SpawnTuning.TuningFrom((LevelArg?)null));
    }

    [Fact]
    public void Tuning_empty_no_modifiers()
    {
        Assert.Same(UnitTuning.None, SpawnTuning.TuningFrom((SpawnModifiers?)null));
        Assert.True(SpawnTuning.TuningFrom((SpawnModifiers?)null).IsNone);
        Assert.True(SpawnTuning.TuningFrom(new SpawnModifiers()).IsNone);
    }

    // ---- D11 Loot

    static readonly LedgerLimits Limits = new(150, 20, 10, 10);
    static readonly UnitLifetime Life = new(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc), 600);

    static IReadOnlyList<SpawnOrder> Orders(bool? loot)
    {
        var ledger = new SpawnLedger(Limits);
        if (loot is { } l) ledger.Request(A, "raid", 3, Life, UnitTuning.None, _ => (0, 0, 0), loot: l);
        else ledger.Request(A, "raid", 3, Life, UnitTuning.None, _ => (0, 0, 0));
        return ledger.TakeSpawns();
    }

    [Fact]
    public void Loot_fails_when_order_without_loot_keeps_drops()
    {
        Assert.All(Orders(null), o => Assert.True(o.ClearDrops));
        Assert.All(Orders(false), o => Assert.True(o.ClearDrops));
    }

    [Fact]
    public void Loot_fails_when_loot_order_clears_drops()
    {
        var orders = Orders(true);
        Assert.Equal(3, orders.Count);
        Assert.All(orders, o => Assert.False(o.ClearDrops));
        Assert.All(orders, o => Assert.True(o.Loot));
    }

    [Fact]
    public void Loot_passes_each_order_carries_its_flag()
    {
        var ledger = new SpawnLedger(Limits);
        ledger.Request(A, "raid", 2, Life, UnitTuning.None, _ => (0, 0, 0), loot: true);
        ledger.Request(B, "other", 2, Life, UnitTuning.None, _ => (0, 0, 0));
        Assert.Equal([false, false, true, true], ledger.TakeSpawns().Select(o => o.ClearDrops));
    }

    [Fact]
    public void Loot_empty_order_without_loot()
    {
        var order = new SpawnOrder(1, A, "raid", 0, 0, 0, 600, Life.DueUtc, UnitTuning.None);
        Assert.False(order.Loot);
        Assert.True(order.ClearDrops);
    }

    // ---- D13 Hunt

    static HuntCandidate P(long key, float x, float z = 0, bool online = true, bool alive = true, bool territory = false, bool pvp = false) =>
        new(key, x, z, online, alive, territory, pvp);

    static AggroSeed Seed(long target) => new(target, 1f, 10f);

    static Dictionary<long, AggroSeed> Buffer(params AggroSeed[] entries) => entries.ToDictionary(e => e.Target);

    [Theory]
    [InlineData("dead")]
    [InlineData("offline")]
    [InlineData("out of range")]
    [InlineData("in territory")]
    [InlineData("pvp combat")]
    [InlineData("nan position")]
    public void Hunt_fails_when_ineligible_player_targeted(string kind)
    {
        var bad = kind switch
        {
            "dead" => P(2, 5, alive: false),
            "offline" => P(2, 5, online: false),
            "out of range" => P(2, 40.5f),
            "in territory" => P(2, 5, territory: true),
            "pvp combat" => P(2, 5, pvp: true),
            _ => P(2, float.NaN),
        };
        Assert.Equal([1L], HuntPlan.Targets([P(1, 20), bad], (0, 0), 40));
        Assert.Empty(HuntPlan.Targets([bad], (0, 0), 40));
    }

    [Fact]
    public void Hunt_passes_tally_counts_each_player_once()
    {
        // Session 1 (A66): a player standing on a castle plot read as "0 seeds kept" with no reason.
        var players = new[]
        {
            P(1, 20), P(2, 5, territory: true), P(3, 5, alive: false, territory: true), P(4, 5, pvp: true, territory: true),
            P(5, 5, pvp: true), P(6, 40.5f), P(7, float.NaN), P(8, 5, online: false),
        };
        var t = HuntPlan.Tally(players, (0, 0), 40);
        Assert.Equal(new HuntTally(8, 1, 3, 2, 1, 1, 0), t);
        Assert.Equal(t.Players, t.Targets + t.Dead + t.InTerritory + t.InPvpCombat + t.OutOfRange + t.OverCap);
        Assert.Equal("8 players read, 1 targeted; left out: 3 dead or unreadable, 2 in claimed territory, 1 in PvP combat, 1 out of range, 0 over the cap of 5",
            t.ToString());
        // six eligible, five targeted: the sixth is over the cap (Codex F2)
        Assert.Equal(new HuntTally(7, 5, 0, 0, 0, 1, 1), HuntPlan.Tally(Enumerable.Range(1, 7).Select(i => P(i, 70 - i * 5)).ToList(), (0, 0), 60));
        Assert.Equal(new HuntTally(0, 0, 0, 0, 0, 0, 0), HuntPlan.Tally([], (0, 0), 40));
    }

    [Fact]
    public void Hunt_fails_when_tally_counts_a_player_twice()
    {
        // one player with every reason at once is counted once, under the first (Review 32 F4)
        Assert.Equal(new HuntTally(1, 0, 1, 0, 0, 0, 0), HuntPlan.Tally([P(1, 90, alive: false, territory: true, pvp: true)], (0, 0), 40));
        Assert.Equal(new HuntTally(1, 0, 0, 1, 0, 0, 0), HuntPlan.Tally([P(1, 90, territory: true, pvp: true)], (0, 0), 40));
        Assert.Equal(new HuntTally(1, 0, 0, 0, 1, 0, 0), HuntPlan.Tally([P(1, 90, pvp: true)], (0, 0), 40));
    }

    [Fact]
    public void Hunt_fails_when_sixth_target_added()
    {
        var players = Enumerable.Range(1, 7).Select(i => P(i, 70 - i * 5)).ToList();
        var targets = HuntPlan.Targets(players, (0, 0), 60);
        Assert.Equal(HuntPlan.MaxTargets, targets.Count);
        Assert.Equal([7L, 6, 5, 4, 3], targets);
    }

    [Fact]
    public void Hunt_fails_when_order_not_by_distance()
    {
        Assert.Equal([2L, 3, 1], HuntPlan.Targets([P(1, 0, 30), P(2, -10, 0), P(3, 12, 12)], (0, 0), 60));
        Assert.Equal([4L, 9], HuntPlan.Targets([P(9, 10), P(4, -10)], (0, 0), 60));   // a tie goes by key
    }

    [Fact]
    public void Hunt_fails_when_duplicate_planned()
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(100, "raid", Seed(1));
        var (adds, removes) = seeds.Plan(100, [1, 2], new HashSet<long> { 1 });
        Assert.Equal([2L], adds);
        Assert.Empty(removes);
        seeds.Wrote(100, "raid", Seed(2));
        seeds.Wrote(100, "raid", Seed(2));
        Assert.Equal(2, seeds.CountFor("raid"));
        Assert.Empty(seeds.Plan(100, [1, 2], new HashSet<long> { 1, 2 }).Adds);
        var diff = HuntPlan.Diff([1], [1, 2]);
        Assert.Equal([2L], diff.Adds);
        Assert.Empty(diff.Removes);
    }

    [Fact]
    public void Hunt_fails_when_player_in_buffer_seeded()
    {
        var seeds = new HuntSeeds();
        var (adds, _) = seeds.Plan(100, [1, 2, 3], new HashSet<long> { 2 });   // 2 is the game's aggro entry
        Assert.Equal([1L, 3], adds);
    }

    [Fact]
    public void Hunt_fails_when_plan_for_ended_wave()
    {
        var life = new WaveLifecycle(new SpawnLedger(Limits), new HuntSeeds(), new TerritoryMaps());
        life.Maps.Built("raid", new HashSet<(int X, int Z)>());
        life.Seeds.Wrote(100, "raid", Seed(1));
        life.EventEnded("raid", DateTime.MaxValue);
        Assert.Null(life.Maps.ForHunt("raid"));                                // no map: the tick seeds nobody
        Assert.Empty(life.Seeds.SeededOn(100));
        Assert.Equal(0, life.Seeds.CountFor("raid"));
    }

    [Fact]
    public void Hunt_fails_when_stale_seed_kept()
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(100, "raid", Seed(1));
        seeds.Wrote(100, "raid", Seed(2));
        var (adds, removes) = seeds.Plan(100, [2], new HashSet<long> { 1, 2 });
        Assert.Empty(adds);
        Assert.Equal([1L], removes);
    }

    [Fact]
    public void Hunt_fails_when_game_entry_removed()
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(100, "raid", Seed(1));
        Assert.Equal((1, 0), seeds.Reconcile(100, Buffer(Seed(1), Seed(3))));    // 3 is the game's: never adopted
        Assert.Equal([1L], seeds.SeededOn(100));
        var (_, removes) = seeds.Plan(100, [], new HashSet<long> { 1, 3 });
        Assert.Equal([1L], removes);
    }

    [Fact]
    public void Hunt_fails_when_empty_targets_keep_a_seed()
    {
        var seeds = new HuntSeeds();
        foreach (var t in new long[] { 3, 1, 2 }) seeds.Wrote(100, "raid", Seed(t));
        var (adds, removes) = seeds.Plan(100, [], new HashSet<long> { 1, 2, 3 });
        Assert.Empty(adds);
        Assert.Equal([1L, 2, 3], removes);
    }

    [Fact]
    public void Hunt_fails_when_race_leaves_record_buffer_lacks()
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(100, "raid", Seed(1));
        var (adds, _) = seeds.Plan(100, [1, 2, 3], new HashSet<long> { 1 });
        Assert.Equal([2L, 3], adds);
        seeds.Wrote(100, "raid", Seed(2));                                    // written, then the target left the game
        // the write for 3 threw: nothing recorded for it
        var buffer = Buffer(Seed(1));
        Assert.Equal((1, 0), seeds.Reconcile(100, buffer));
        Assert.All(seeds.SeededOn(100), t => Assert.Contains(t, buffer.Keys));
        Assert.Equal([1L], seeds.SeededOn(100));
    }

    [Theory]
    [InlineData("damage")]
    [InlineData("weight")]
    [InlineData("entity")]
    public void Hunt_fails_when_replaced_entry_removed(string field)
    {
        var seeds = new HuntSeeds();
        seeds.Wrote(100, "raid", Seed(1));
        seeds.Wrote(100, "raid", Seed(2));
        var now = field switch
        {
            "damage" => Seed(1) with { DamageValue = 5f },
            "weight" => Seed(1) with { Weight = 3f },
            _ => Seed(1) with { Target = 77 },
        };
        Assert.Equal((1, 1), seeds.Reconcile(100, new Dictionary<long, AggroSeed> { [1] = now, [2] = Seed(2) }));
        Assert.Equal([2L], seeds.SeededOn(100));
        var (_, removes) = seeds.Plan(100, [], new HashSet<long> { 1, 2 });
        Assert.Equal([2L], removes);                                           // the game's entry for 1 stays
    }

    [Fact]
    public void Hunt_fails_when_tick_reads_other_map()
    {
        var maps = new TerritoryMaps();
        var first = new HashSet<(int X, int Z)> { (1, 1) };
        var latest = new HashSet<(int X, int Z)> { (2, 2) };
        var other = new HashSet<(int X, int Z)> { (3, 3) };
        maps.Built("raid", first);
        maps.Built("other", other);
        maps.Built("raid", latest);
        Assert.Same(latest, maps.ForHunt("raid"));
        Assert.Same(other, maps.ForHunt("other"));
        Assert.Null(maps.ForHunt("ghost"));
    }

    [Fact]
    public void Hunt_fails_when_seeds_while_latest_build_failed()
    {
        var maps = new TerritoryMaps();
        maps.Failed("raid");
        Assert.Null(maps.ForHunt("raid"));                                     // failed before any build
        var built = new HashSet<(int X, int Z)> { (1, 1) };
        maps.Built("raid", built);
        maps.Failed("raid");
        Assert.Null(maps.ForHunt("raid"));                                     // the kept map is not read while failed
        Assert.True(maps.Holds("raid"));
        var again = new HashSet<(int X, int Z)>();
        maps.Built("raid", again);
        Assert.Same(again, maps.ForHunt("raid"));
    }

    [Fact]
    public void Hunt_passes_tick_seeds_and_keeps()
    {
        var map = Territory.Build([(Territory.Block(30), Territory.Block(0))]).Blocks;
        var maps = new TerritoryMaps();
        maps.Built("raid", map);
        var at = new[] { (Key: 1L, X: 10f), (Key: 2L, X: 30f), (Key: 3L, X: -20f) };
        var players = at.Select(p => P(p.Key, p.X, territory: Territory.IsClaimed(maps.ForHunt("raid")!, p.X, 0))).ToList();
        var targets = HuntPlan.Targets(players, (0, 0), 40);
        Assert.Equal([1L, 3], targets);                                        // 2 stands in a claimed block

        var seeds = new HuntSeeds();
        var (adds, removes) = seeds.Plan(100, targets, new HashSet<long>());
        foreach (var t in adds) seeds.Wrote(100, "raid", Seed(t));
        Assert.Empty(removes);
        Assert.Equal((2, 0), seeds.Reconcile(100, Buffer(Seed(1), Seed(3))));
        var again = seeds.Plan(100, targets, new HashSet<long> { 1, 3 });
        Assert.Empty(again.Adds);
        Assert.Empty(again.Removes);
        Assert.Equal(5, HuntPlan.MaxTargets);
        Assert.Equal(5, HuntPlan.IntervalSeconds);
    }

    [Fact]
    public void Hunt_empty_no_players()
    {
        Assert.Empty(HuntPlan.Targets([], (0, 0), 60));
        var seeds = new HuntSeeds();
        var plan = seeds.Plan(100, [], new HashSet<long>());
        Assert.Empty(plan.Adds);
        Assert.Empty(plan.Removes);
        Assert.Equal((0, 0), seeds.Reconcile(100, Buffer()));
        Assert.Equal(0, seeds.Count);
    }

    // ---- D16 PlayerPick

    static PickCandidate Player(float x, float z, float y = 50, bool online = true, bool alive = true, bool pvp = false) => new(x, y, z, online, alive, pvp);

    static readonly Func<float, float, bool> Unclaimed = (_, _) => false;

    [Theory]
    [InlineData("offline")]
    [InlineData("dead")]
    [InlineData("pvp combat")]
    [InlineData("in territory")]
    [InlineData("nan position")]
    [InlineData("position beyond the map")]
    public void PlayerPick_fails_when_ineligible_player_picked(string kind)
    {
        var bad = kind switch
        {
            "offline" => Player(500, 500, online: false),
            "dead" => Player(500, 500, alive: false),
            "pvp combat" => Player(500, 500, pvp: true),
            "in territory" => Player(500, 500),
            "nan position" => Player(float.NaN, 500),
            _ => Player(10500, 500),
        };
        Func<float, float, bool> claimed = (x, z) => kind == "in territory" && x == 500 && z == 500;
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.Choose([bad], new ScriptedRandom(0, 0, 0), 20, 40, claimed, null).Outcome);
        var r = PlayerPick.Choose([bad, Player(-100, 0)], new ScriptedRandom(0.99, 0, 0), 20, 40, claimed, null);
        Assert.Equal(PickOutcome.Picked, r.Outcome);
        Assert.Equal((-80f, 50f, 0f), r.Centre);                               // the eligible player, at angle 0 and minDist
    }

    [Fact]
    public void PlayerPick_fails_when_distance_leaves_range()
    {
        var rng = new SystemRandom(new Random(11));
        for (var i = 0; i < 500; i++)
        {
            var r = PlayerPick.Choose([Player(1000, -2000, 77)], rng, 15, 45, Unclaimed, null);
            var d = MathF.Sqrt((r.Centre.X - 1000) * (r.Centre.X - 1000) + (r.Centre.Z + 2000) * (r.Centre.Z + 2000));
            Assert.InRange(d, 15 - 0.01f, 45 + 0.01f);
            Assert.Equal(77f, r.Centre.Y);                                     // the player's height
        }
        var far = PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0, 0.999999), 15, 45, Unclaimed, null);
        Assert.InRange(far.Centre.X, 44.99f, 45f);
    }

    [Fact]
    public void PlayerPick_fails_when_pick_not_uniform()
    {
        var players = new[] { Player(0, 0), Player(1000, 0), Player(2000, 0) };
        float Picked(double r) => PlayerPick.Choose(players, new ScriptedRandom(r, 0, 0), 20, 40, Unclaimed, null).Centre.X - 20;
        Assert.Equal([0f, 0f, 1000f, 1000f, 2000f, 2000f], new[] { 0.0, 0.33, 0.34, 0.66, 0.67, 0.9999 }.Select(Picked));
    }

    [Fact]
    public void PlayerPick_fails_when_queued_centre_changes()
    {
        var players = new List<PickCandidate> { Player(100, 100) };
        var queued = PlayerPick.Choose(players, new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, null);
        var centre = queued.Centre;
        players[0] = players[0] with { Alive = false };                         // the player dies after the wave is queued
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.Choose(players, new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, null).Outcome);
        Assert.Equal(centre, queued.Centre);
        Assert.Equal(WaveOutcome.Spawn, Gate(Facts(queued.Outcome, location: LocationType.AroundPlayer), [A]).Outcome);
    }

    [Fact]
    public void PlayerPick_fails_when_out_of_scope_player_picked()
    {
        Func<float, float, bool> east = (x, _) => x > 0;
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.Choose([Player(-500, 0)], new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, east).Outcome);
        var r = PlayerPick.Choose([Player(-500, 0), Player(500, 0)], new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, east);
        Assert.Equal((520f, 50f, 0f), r.Centre);
    }

    [Fact]
    public void PlayerPick_fails_when_out_of_scope_centre_used()
    {
        Func<float, float, bool> north = (x, z) => z > -1;                     // the player stands at z 0; the centre must too
        var r = PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0.75, 0), 20, 40, Unclaimed, north);
        // start angle 270 degrees (z = -20) is outside; the ring goes on 30 degrees at a time: 300, 330, then 0 is in scope
        var a = 1.5 * Math.PI + 3 * 2 * Math.PI / 12;
        Assert.Equal(PickOutcome.Picked, r.Outcome);
        Assert.Equal((float)(20 * Math.Cos(a)), r.Centre.X, 3);
        Assert.Equal((float)(20 * Math.Sin(a)), r.Centre.Z, 3);
        Assert.True(north(r.Centre.X, r.Centre.Z));

        Func<float, float, bool> onlyPlayers = (x, z) => MathF.Abs(z) < 0.5f && (MathF.Abs(x) < 0.5f || MathF.Abs(x - 1000) < 0.5f) || x > 900;
        var cornered = PlayerPick.Choose([Player(0, 0), Player(1000, 0)], new ScriptedRandom(0, 0, 0, 0, 0, 0), 20, 40, Unclaimed, onlyPlayers);
        Assert.Equal((1020f, 50f, 0f), cornered.Centre);                        // the first player has no in-scope centre: not usable
        Assert.Equal(PickOutcome.NoEligible,
            PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, (x, z) => MathF.Abs(x) < 0.5f && MathF.Abs(z) < 0.5f).Outcome);
    }

    [Fact]
    public void PlayerPick_fails_when_throwing_read_spawns()
    {
        Func<float, float, bool> throws = (_, _) => throw new InvalidOperationException("region index gone");
        var claimedThrows = PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0, 0), 20, 40, throws, null);
        var scopeThrows = PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0, 0), 20, 40, Unclaimed, throws);
        foreach (var r in new[] { claimedThrows, scopeThrows })
        {
            Assert.Equal(PickOutcome.QueryFailed, r.Outcome);
            Assert.Equal("region index gone", r.Error);
            var rolls = 0;
            var d = WaveGate.Decide(Facts(r.Outcome, location: LocationType.AroundPlayer), () => { rolls++; return [A]; }, 20, 0, 150);
            Assert.Equal((WaveOutcome.Skip, "wave 1 of raid skipped: player query failed"), (d.Outcome, d.Line));
            Assert.Equal(0, rolls);
        }
    }

    [Fact]
    public void PlayerPick_fails_when_claimed_first_angle_moves_centre()
    {
        var blocks = Territory.Build([(Territory.Block(20), Territory.Block(0))]).Blocks;   // a castle 20 m east of the player
        Func<float, float, bool> claimed = (x, z) => Territory.IsClaimed(blocks, x, z);
        var r = PlayerPick.Choose([Player(0, 0)], new ScriptedRandom(0, 0, 0), 20, 40, claimed, null);
        Assert.Equal((20f, 50f, 0f), r.Centre);                                // not moved to another angle
        var centreClaimed = Territory.IsClaimed(blocks, r.Centre.X, r.Centre.Z);
        Assert.True(centreClaimed);
        var d = Gate(Facts(r.Outcome, claimed: centreClaimed, location: LocationType.AroundPlayer), [A]);
        Assert.Equal((WaveOutcome.Skip, "wave 1 of raid skipped: centre in claimed territory"), (d.Outcome, d.Line));
    }

    [Fact]
    public void PlayerPick_passes_eligible_player_centre()
    {
        var r = PlayerPick.Choose([Player(100, 200, 33)], new ScriptedRandom(0, 0.25, 0.5), 20, 40, Unclaimed, null);
        Assert.Equal(PickOutcome.Picked, r.Outcome);
        Assert.Equal(100f, r.Centre.X, 3);                                    // 90 degrees, 30 m
        Assert.Equal(230f, r.Centre.Z, 3);
        Assert.Equal(33f, r.Centre.Y);
        Assert.Null(r.Error);
        var scoped = PlayerPick.Choose([Player(100, 200, 33)], new ScriptedRandom(0, 0.25, 0.5), 20, 40, Unclaimed, (_, _) => true);
        Assert.Equal(r, scoped);
    }

    [Fact]
    public void PlayerPick_empty_no_players()
    {
        var rng = new ScriptedRandom();
        var r = PlayerPick.Choose([], rng, 20, 40, Unclaimed, null);
        Assert.Same(PickResult.NoEligible, r);
        Assert.Equal(0, rng.Calls);
        var d = Gate(Facts(r.Outcome, location: LocationType.AroundPlayer), [A]);
        Assert.Equal((WaveOutcome.Skip, "wave 1 of raid skipped: no eligible player"), (d.Outcome, d.Line));
    }

    // ---- D17 Territory

    [Theory]
    [InlineData(0f, 640)]
    [InlineData(4.99f, 640)]
    [InlineData(5f, 641)]
    [InlineData(-0.01f, 639)]
    [InlineData(-5f, 639)]
    [InlineData(-5.01f, 638)]
    [InlineData(-3200f, 0)]
    [InlineData(3199.99f, 1279)]
    [InlineData(3200f, 1280)]
    [InlineData(-1234.56f, 393)]
    public void Territory_fails_when_block_conversion_differs(float v, int block) => Assert.Equal(block, Territory.Block(v));

    [Fact]
    public void Territory_fails_when_listed_block_point_free()
    {
        var blocks = Territory.Build([(641, 639)]).Blocks;
        Assert.True(Territory.IsClaimed(blocks, 5f, -5f));
        Assert.True(Territory.IsClaimed(blocks, 9.99f, -0.01f));
    }

    [Fact]
    public void Territory_fails_when_outside_point_claimed()
    {
        var blocks = Territory.Build([(641, 639)]).Blocks;
        Assert.False(Territory.IsClaimed(blocks, 4.99f, -5f));
        Assert.False(Territory.IsClaimed(blocks, 5f, 0f));
        Assert.False(Territory.IsClaimed(blocks, 10f, -5f));
    }

    [Fact]
    public void Territory_fails_when_out_of_range_block_kept()
    {
        var (set, ignored) = Territory.Build([(-1, 0), (1280, 5), (3, 1280), (3, -7), (10, 10), (0, 1279)]);
        Assert.Equal(new HashSet<(int X, int Z)> { (10, 10), (0, 1279) }, set);
        Assert.Equal(4, ignored);
    }

    [Fact]
    public void Territory_fails_when_allow_territory_wave_refused()
    {
        var d = Gate(Facts(claimed: true, allow: true), [A, A]);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal(2, d.Units.Sum(u => u.Count));
        Assert.Equal(WaveOutcome.Skip, Gate(Facts(claimed: true), [A]).Outcome);
    }

    [Theory]
    [InlineData(LocationType.Point, false, false)]
    [InlineData(LocationType.Admin, false, false)]
    [InlineData(LocationType.AroundPlayer, false, false)]
    [InlineData(LocationType.AroundPlayer, true, false)]
    [InlineData(LocationType.Point, true, true)]
    [InlineData(LocationType.Point, false, true)]
    public void Territory_fails_when_failed_map_spawns_wave_that_needs_it(LocationType location, bool allow, bool hunt)
    {
        var facts = Facts(allow: allow, location: location) with { MapFailed = true, Behaviour = hunt ? BehaviourType.Hunt : null };
        Assert.True(facts.NeedsMap);
        var d = Gate(facts, [A]);
        Assert.Equal((WaveOutcome.Skip, "wave 1 of raid skipped: territory unknown"), (d.Outcome, d.Line));
        Assert.Empty(d.Units);
    }

    [Fact]
    public void Territory_passes_castle_blocks_claimed()
    {
        var castle = new List<(int X, int Z)>();
        for (var bx = Territory.Block(-1250); bx <= Territory.Block(-1200); bx++)
            for (var bz = Territory.Block(-850); bz <= Territory.Block(-800); bz++) castle.Add((bx, bz));
        var (blocks, ignored) = Territory.Build(castle);
        Assert.Equal(0, ignored);
        Assert.True(Territory.IsClaimed(blocks, -1225.5f, -825.25f));
        Assert.True(Territory.IsClaimed(blocks, -1250f, -800f));
        Assert.False(Territory.IsClaimed(blocks, -1180f, -825f));
        Assert.False(Territory.IsClaimed(blocks, 1225f, 825f));
        var point = Facts(allow: true) with { MapFailed = true };
        Assert.False(point.NeedsMap);
        Assert.Equal(WaveOutcome.Spawn, Gate(point, [A]).Outcome);
    }

    [Fact]
    public void Territory_empty_block_set()
    {
        var (blocks, ignored) = Territory.Build([]);
        Assert.Empty(blocks);
        Assert.Equal(0, ignored);
        foreach (var (x, z) in new[] { (0f, 0f), (-3200f, -3200f), (3199f, 3199f), (-1234.5f, 800f) }) Assert.False(Territory.IsClaimed(blocks, x, z));
        var maps = new TerritoryMaps();
        maps.Built("raid", blocks);
        Assert.Same(blocks, maps.ForHunt("raid"));                             // an empty built map is not a failed one
        Assert.False(maps.AnyFailed);
    }
}
