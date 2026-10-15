using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The game side of scaling in the tests (scaling D2, D6, D8): the players a read returns, the regional scope's
/// test (FakeRegions' FarbaneWoods, x &lt; 0), the V Blood prefabs, a read counter and a planted throw.</summary>
sealed class ScalingRig : IScalingReader
{
    public List<ScalingPlayer> Rows { get; } = [];
    public Func<float, float, bool> Inside { get; set; } = (x, _) => x < 0;
    public HashSet<string> VBloods { get; } = new(StringComparer.Ordinal) { "CHAR_Bandit_Tourok_VBlood" };
    public int Reads { get; private set; }
    public string? Throw { get; set; }

    public IReadOnlyList<ScalingPlayer> Players()
    {
        Reads++;
        if (Throw is { } message) throw new InvalidOperationException(message);
        return Rows.ToList();
    }

    public bool InScope(Scope scope, float x, float z) => Inside(x, z);

    /// <summary>A source over this rig for an apocalypse run: a fresh read at every call (admins counted).</summary>
    public ScalingSource Source(ScalingBook? book = null, bool includeAdmins = true) =>
        new(() => Scaling.TryRead(this, Scope.Global, includeAdmins, out _), book ?? new ScalingBook());
}

/// <summary>A random source that counts its draws and always returns <see cref="Value"/>.</summary>
sealed class CountingRandom(double value) : IRandom
{
    public double Value { get; set; } = value;
    public int Calls { get; private set; }
    public double NextDouble() { Calls++; return Value; }
}

/// <summary>scaling D2-D10 over Logic/Scaling.cs, Logic/Spawning.cs and Logic/Apocalypse.cs: who is counted, the extra units
/// of a wave, a fanOut group, a horde and a roamer top-up, the average gear level, the scaled level, V Bloods keeping theirs,
/// the caps and switches winning, and the one line per decision.</summary>
public class ScalingTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    const string Thug = "CHAR_Bandit_Thug", Deadeye = "CHAR_Bandit_Deadeye", Tourok = "CHAR_Bandit_Tourok_VBlood";
    const string PointLoc = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";
    const string AroundLoc = "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }";
    const string TwoEntries = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 3 }, { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 2, \"chance\": 0.5 } ]";
    const string Thugs5 = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ]";
    const string BossKey = "\"bosses\": [ { \"prefab\": \"CHAR_Bandit_Tourok_VBlood\", \"count\": 1, \"modifiers\": { \"levelDelta\": 2 } } ]";

    static ScalingPlayer P(float x = -1000, float z = -800, bool online = true, bool alive = true, bool admin = false, double? gear = 40) =>
        new(x, z, online, alive, admin, gear);

    static string Block(int units = 2, bool level = false, int offset = 0, int perWave = 0) =>
        $"\"scaling\": {{ \"unitsPerPlayer\": {units}, \"levelFromPlayers\": {(level ? "true" : "false")}, \"levelOffset\": {offset}, \"levelPerWave\": {perWave} }}";

    static string Keys(params string?[] keys) => string.Join(", ", keys.Where(k => k is not null));

    static string WavesAction(string? extra = null, string units = TwoEntries, string waves = "3", string location = PointLoc) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": " + waves + ", \"intervalSeconds\": 60, \"radius\": 10, " +
        "\"location\": " + location + (extra is null ? "" : ", " + extra) + " }";

    static string ListAction(string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ] }, " +
        "{ \"units\": [ { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 1, \"modifiers\": { \"levelDelta\": 1 } } ], \"afterSeconds\": 60 } ], " +
        "\"radius\": 10, \"location\": " + PointLoc + (extra is null ? "" : ", " + extra) + " }";

    /// <summary>A revenge event's action (Json.RevengeAction) with <paramref name="extra"/> keys.</summary>
    static string RevengeAction(string extra) => Json.RevengeAction[..^2] + ", " + extra + " }";

    internal static EventDefinition Def(string action, string? trigger = null, string id = "sc")
    {
        var r = EventValidator.Parse(Json.File(Json.Event(id, trigger, action: action)), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    static SpawnWavesAction Waves(string action, string? trigger = null) => Def(action, trigger).Action!;

    static int WaveCount(SpawnWavesAction a) => a.WaveList?.Count ?? a.Waves;

    static ScalingRead Read(int players, int? gear = 40) => new(players, gear, Enumerable.Range(0, players).Select(_ => P()).ToList());

    static ControlState Open(int active = 0, int max = 3) => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), active, max);

    /// <summary>Every kind of SpawnWaves wave D3 names, each with unitsPerPlayer 2.</summary>
    static IEnumerable<(string Kind, SpawnWavesAction Action)> Kinds() =>
    [
        ("units", Waves(WavesAction(Block()))),
        ("waveList", Waves(ListAction(Block()))),
        ("growth", Waves(WavesAction(Keys("\"growth\": { \"addUnits\": 1, \"addLevels\": 2 }", Block())))),
        ("revenge", Waves(RevengeAction(Block()), Json.RevengeTrigger)),
        ("region", Waves(WavesAction(Keys("\"scope\": [\"FarbaneWoods\"]", Block()), units: Thugs5, location: "{ \"type\": \"Region\" }"))),
    ];

    // ---- D2 Players

    [Fact]
    public void Players_fails_when_a_dead_offline_or_unusable_player_counts()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(online: false), P(alive: false), P(float.NaN, 0), P(-20000, 0), P(-500, float.PositiveInfinity), P(-10, -10)]);
        var read = Scaling.Read(rig, Scope.Global, includeAdmins: true);
        Assert.Equal(2, read.Players);
        Assert.Equal(2, read.Counted.Count);
        Assert.All(read.Counted, p => Assert.True(p.Online && p.Alive && PlayerPosition.Usable(p.X, p.Z)));
    }

    [Fact]
    public void Players_fails_when_a_regional_scope_counts_an_outsider_or_global_misses_one()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(-1000, 0), P(1000, 0), P(2000, 50)]);
        Assert.Equal(1, Scaling.Read(rig, new Scope(["FarbaneWoods"]), true).Players);
        rig.Inside = (_, _) => throw new InvalidOperationException("a Global scope reads no region");
        Assert.Equal(3, Scaling.Read(rig, Scope.Global, true).Players);
    }

    [Fact]
    public void Players_fails_when_an_admin_counts_against_include_admins()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(admin: true), P()]);
        Assert.Equal(1, Scaling.Read(rig, Scope.Global, includeAdmins: false).Players);
        Assert.Equal(2, Scaling.Read(rig, Scope.Global, includeAdmins: true).Players);
    }

    [Fact]
    public void Players_fails_when_claimed_territory_or_pvp_combat_leaves_a_player_out()
    {
        // a player's row holds no territory or combat flag, so nothing in the count can leave him out for either (S-9)
        Assert.Equal(["Alive", "Gear", "IsAdmin", "Online", "X", "Z"], typeof(ScalingPlayer).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(-1200, -800), P(-1200, -800), P(-1201, -801)]);   // two at a castle's heart, one beside them fighting
        Assert.Equal(3, Scaling.Read(rig, new Scope(["FarbaneWoods"]), true).Players);
    }

    [Fact]
    public void Players_fails_when_a_count_is_reused_or_a_decision_skips_its_read()
    {
        var rig = new ScalingRig();
        rig.Rows.Add(P());
        Assert.Equal(1, Scaling.Read(rig, Scope.Global, true).Players);
        rig.Rows.AddRange([P(), P()]);
        Assert.Equal(3, Scaling.Read(rig, Scope.Global, true).Players);
        Assert.Equal(2, rig.Reads);
        // an apocalypse: each top-up reads once, each rise once and its top-up once more, each with what is there now
        var def = Apo.Def(Apo.Action(extra: Block()));
        var run = Apo.Run(Apo.Active(def, T0));
        var w = new ApoWorld();
        var scale = rig.Source();
        var reads = rig.Reads;
        var lines = run.TopUp(w, T0, scale);
        Assert.Equal(reads + 1, rig.Reads);
        Assert.Equal("scaling apo roamers: players 3, per hotspot 7, max 20", lines[0]);
        rig.Rows.RemoveAt(0);
        var rise = run.Rise(w, T0.AddMinutes(1), out var topUp, scale: scale)!;
        Assert.Equal(reads + 3, rig.Reads);
        Assert.Equal("scaling apo horde 1: players 2, gear 40, level own, units 8 -> 10", rise.Scaling);
        Assert.Equal("scaling apo roamers: players 2, per hotspot 5, max 16", topUp[0]);
        rig.Rows.Clear();
        var second = run.Rise(w, T0.AddMinutes(6), out _, topUp: false, scale: scale)!;
        Assert.Equal(reads + 4, rig.Reads);
        Assert.Equal("scaling apo horde 2: players 0, gear none, level own, units 12 -> 12", second.Scaling);
    }

    [Fact]
    public void Players_fails_when_a_throwing_read_scales_the_decision()
    {
        var rig = new ScalingRig { Throw = "player read broke" };
        rig.Rows.AddRange([P(), P(), P()]);
        Assert.Null(Scaling.TryRead(rig, Scope.Global, true, out var error));
        Assert.Equal("InvalidOperationException", error);                          // the type only, never the message (D11)
        var action = Waves(WavesAction(Block()));
        var unscaled = Scaling.ForWave(action, "sc", 1, null);
        Assert.Equal(action.Units.Select(e => e.Count), unscaled.Entries(0).Select(e => e.Count));
        Assert.Null(unscaled.Line);
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block())), T0));
        var w = new ApoWorld();
        var rise = run.Rise(w, T0.AddMinutes(1), out var topUp, scale: rig.Source())!;
        Assert.Equal(8, rise.Queued);                                              // the configured horde, never more
        Assert.Null(rise.Scaling);
        Assert.DoesNotContain(topUp, l => l.StartsWith("scaling ", StringComparison.Ordinal));
    }

    [Fact]
    public void Players_passes_three_counted_players()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(gear: 30), P(-900, -700, gear: 41), P(-800, -600, admin: true, gear: 50)]);
        var read = Scaling.Read(rig, new Scope(["FarbaneWoods"]), true);
        Assert.Equal((3, 40), (read.Players, read.Gear!.Value));
    }

    [Fact]
    public void Players_empty_no_player_no_extra()
    {
        var read = Scaling.Read(new ScalingRig(), Scope.Global, true);
        Assert.Equal((0, (int?)null, 0), (read.Players, read.Gear, read.Counted.Count));
        var action = Waves(WavesAction(Block()));
        Assert.Equal(action.Units.Select(e => e.Count), Scaling.ForWave(action, "sc", 1, read).Entries(0).Select(e => e.Count));
        Assert.Equal(0, new Scaling(5).Extra(0));
    }

    // ---- D3 Counts

    [Fact]
    public void Counts_fails_when_three_players_and_u2_do_not_add_four_copies_to_each_entry()
    {
        foreach (var (kind, action) in Kinds())
            for (var wave = 1; wave <= WaveCount(action); wave++)
            {
                var own = action.UnitsOf(wave);
                var scaled = Scaling.ForWave(action, "sc", wave, Read(3)).Entries(0);
                Assert.True(own.Select(e => e.Count + 4).SequenceEqual(scaled.Select(e => e.Count)), $"{kind} wave {wave}");
                Assert.Equal(own.Select(e => (e.Prefab, e.Chance, e.Modifiers)), scaled.Select(e => (e.Prefab, e.Chance, e.Modifiers)));
            }
        // a Region wave is split after the scaled roll: 5 + 4 thugs in groups of at most 5
        var region = Kinds().Single(k => k.Kind == "region").Action;
        var spot = new RegionSpot(-1000, 0, -800, 1, "FarbaneWoods");
        var d = RegionWave.Decide(new WaveFacts(1, "sc", false, Location: LocationType.Region),
            () => WaveRoll.ExpandEntries(Scaling.ForWave(region, "sc", 1, Read(3)).Entries(0), new StepRandom()), 50, 0, 200,
            n => Enumerable.Repeat((RegionSpot?)spot, n).ToList());
        Assert.Equal([5, 4], d.Groups.Select(g => g.Units.Sum(u => u.Count)));
    }

    [Fact]
    public void Counts_fails_when_a_boss_entry_gains_copies()
    {
        var action = Waves(WavesAction(Keys(BossKey, Block())));
        var last = Scaling.ForWave(action, "sc", 3, Read(3)).Entries(0);
        Assert.Equal((Tourok, 1), (last[0].Prefab, last[0].Count));
        Assert.Equal([7, 6], last.Skip(1).Select(e => e.Count));
        Assert.DoesNotContain(Scaling.ForWave(action, "sc", 1, Read(3)).Entries(0), e => e.Prefab == Tourok);
        Assert.Equal("scaling sc wave 3: players 3, gear 40, level own, units 6 -> 14", Scaling.ForWave(action, "sc", 3, Read(3)).Line);
    }

    [Fact]
    public void Counts_fails_when_an_extra_copy_skips_its_chance_roll()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block())), "sc", 1, Read(3)).Entries(0);   // 7 thugs at 1.0, 6 deadeyes at 0.5
        var miss = new CountingRandom(0.99);
        Assert.Equal(Enumerable.Repeat(Thug, 7), WaveRoll.ExpandEntries(entries, miss).Select(e => e.Prefab));
        Assert.Equal(6, miss.Calls);                                                // every deadeye copy rolled, the extra four too
        var hit = new CountingRandom(0.1);
        Assert.Equal(6, WaveRoll.ExpandEntries(entries, hit).Count(e => e.Prefab == Deadeye));
    }

    [Fact]
    public void Counts_fails_when_one_player_adds_a_copy()
    {
        var action = Waves(WavesAction(Block()));
        foreach (var n in new[] { 0, 1 })
            Assert.Equal([3, 2], Scaling.ForWave(action, "sc", 1, Read(n)).Entries(0).Select(e => e.Count));
        var levelsOnly = Waves(WavesAction(Block(0, level: true)));
        Assert.Equal([3, 2], Scaling.ForWave(levelsOnly, "sc", 1, Read(9)).Entries(0).Select(e => e.Count));
    }

    [Fact]
    public void Counts_fails_when_a_scaled_roll_passes_a_cap_without_its_line()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block(), units: Thugs5)), "sc", 1, Read(3)).Entries(0);   // 9 thugs
        var facts = new WaveFacts(1, "sc", false);
        var wave = WaveGate.DecideEntries(facts, () => WaveRoll.ExpandEntries(entries, new StepRandom()), 6, 0, 200);
        Assert.Equal(6, wave.Units.Sum(u => u.Count));
        Assert.Equal(["clamped by MaxUnitsPerWave: 9 -> 6"], wave.CapLines);
        var full = WaveGate.DecideEntries(facts, () => WaveRoll.ExpandEntries(entries, new StepRandom()), 50, 196, 200);
        Assert.Equal(4, full.Units.Sum(u => u.Count));
        Assert.Equal(["skipped by MaxTrackedUnits: 5 of 9"], full.CapLines);
    }

    [Fact]
    public void Counts_passes_scaled_wave_rolls_thirteen()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block())), "sc", 1, Read(3)).Entries(0);
        var wave = WaveGate.DecideEntries(new WaveFacts(1, "sc", false), () => WaveRoll.ExpandEntries(entries, new CountingRandom(0.1)), 50, 0, 200);
        Assert.Equal(13, wave.Units.Sum(u => u.Count));
        Assert.Empty(wave.CapLines);
    }

    [Fact]
    public void Counts_empty_no_block_rolls_as_before()
    {
        var action = Waves(WavesAction());
        var wave = Scaling.ForWave(action, "sc", 1, Read(9));
        Assert.Equal(BossWave.Units(action, 1), wave.Entries(0));
        Assert.Null(wave.Line);
        Assert.Null(wave.View);
    }

    // ---- D4 FanOut

    /// <summary>A fanOut wave of 2 thugs, minSpacing 100, unitsPerPlayer 1; players A (0, 0) and B (50, 0) together, C
    /// (1000, 0) alone with D 150 m off and an admin 20 m off; the groups' centres were picked for A and C.</summary>
    static (SpawnWavesAction Action, List<ScalingPlayer> Rows, (float X, float Z)[] Origins) FanOutWave(float dx = 1150)
    {
        var action = Waves(WavesAction(Keys("\"fanOut\": { \"maxInstances\": 3, \"minSpacing\": 100 }", Block(1)),
            units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ]", location: AroundLoc));
        return (action, [P(0, 0), P(50, 0), P(1000, 0), P(dx, 0), P(1020, 0, admin: true)], [(0, 0), (1000, 0)]);
    }

    static ScaledWave FanOutScaled(float dx = 1150, bool includeAdmins = false)
    {
        var (action, rows, origins) = FanOutWave(dx);
        var rig = new ScalingRig();
        rig.Rows.AddRange(rows);
        return Scaling.ForWave(action, "sc", 1, Scaling.Read(rig, Scope.Global, includeAdmins), origins);
    }

    [Fact]
    public void FanOut_fails_when_a_group_with_two_players_gets_no_extra_copy()
    {
        var wave = FanOutScaled();
        Assert.Equal(2, wave.Groups[0]);
        Assert.Equal(3, wave.Entries(0).Single().Count);
    }

    [Fact]
    public void FanOut_fails_when_a_lone_player_group_gets_one()
    {
        var wave = FanOutScaled();
        Assert.Equal(1, wave.Groups[1]);
        Assert.Equal(2, wave.Entries(1).Single().Count);
    }

    [Fact]
    public void FanOut_fails_when_a_player_beyond_min_spacing_is_counted()
    {
        Assert.Equal([2, 1], FanOutScaled(1150).Groups);
        Assert.Equal([2, 2], FanOutScaled(1090).Groups);                            // D within 100 m of C
        Assert.Equal([2, 1], FanOutScaled(1101).Groups);                            // 101 m: out
        // the admin 20 m from C counts only with IncludeAdmins true (D2's filter; Review 1 F8)
        Assert.Equal([2, 2], FanOutScaled(1150, includeAdmins: true).Groups);
    }

    [Fact]
    public void FanOut_fails_when_the_scaled_groups_skip_the_single_clamp_or_the_deal()
    {
        var wave = FanOutScaled();                                                   // 3 + 2 rolled
        var facts = new WaveFacts(1, "sc", false, Location: LocationType.AroundPlayer, Pick: PickOutcome.Picked);
        GroupCentre[] centres = [new(0, 0, 0, false), new(1000, 0, 0, false)];
        var d = WaveGate.DecideGroupEntries(facts, centres, i => WaveRoll.ExpandEntries(wave.Entries(i), new StepRandom()), 4, 0, 200);
        Assert.Equal(["clamped by MaxUnitsPerWave: 5 -> 4"], d.CapLines);            // one clamp over both groups
        Assert.Equal([2, 2], d.Groups.Select(g => g.Units.Sum(u => u.Count)));       // dealt round robin, not 3 + 1
        var open = WaveGate.DecideGroupEntries(facts, centres, i => WaveRoll.ExpandEntries(wave.Entries(i), new StepRandom()), 50, 0, 200);
        Assert.Equal([3, 2], open.Groups.Select(g => g.Units.Sum(u => u.Count)));
        Assert.Empty(open.CapLines);
    }

    [Fact]
    public void FanOut_passes_groups_line()
    {
        Assert.Equal("scaling sc wave 1: players 4, gear 40, level own, units 4 -> 5, groups 2/1", FanOutScaled().Line);
    }

    [Fact]
    public void FanOut_empty_no_origin_one_group()
    {
        var (action, rows, _) = FanOutWave();
        var rig = new ScalingRig();
        rig.Rows.AddRange(rows);
        var wave = Scaling.ForWave(action, "sc", 1, Scaling.Read(rig, Scope.Global, false), []);
        Assert.Empty(wave.Groups);
        Assert.Equal(2 + 3, wave.Entries(0).Single().Count);                         // the wave's whole count: 4 players
        Assert.Equal("scaling sc wave 1: players 4, gear 40, level own, units 2 -> 5", wave.Line);
    }

    // ---- D5 Apocalypse

    [Fact]
    public void Apocalypse_fails_when_three_players_and_u2_do_not_add_four_regular_units()
    {
        var a = Apo.Def(Apo.Action(extra: Block())).Apocalypse!;                       // size 8 + 4 a horde
        Assert.Equal(12, HordeSchedule.Count(HordeSchedule.Units(a, 1, 50, 0, 200, new StepRandom(), [], a.Scaling!.HordeExtra(3))));
        Assert.Equal(16, HordeSchedule.Count(HordeSchedule.Units(a, 2, 50, 0, 200, new StepRandom(), [], a.Scaling.HordeExtra(3))));
        Assert.Equal(4, a.Scaling.HordeExtra(3));                                      // the whole horde's, not per pool entry (Review 1 F1)
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(), P()]);
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block())), T0));
        var w = new ApoWorld();
        Assert.Equal(12, run.Rise(w, T0.AddMinutes(1), out _, topUp: false, scale: rig.Source())!.Queued);
        Assert.Equal(12, w.Ledger.PendingSpawns);
    }

    [Fact]
    public void Apocalypse_fails_when_a_boss_is_multiplied_or_cut_first()
    {
        var a = Apo.Def(Apo.Action(extra: Keys("\"bosses\": [ " + Apo.BossJson() + " ]", Block()))).Apocalypse!;
        var last = HordeSchedule.Units(a, 3, 50, 0, 200, new StepRandom(), [], 4);
        Assert.Equal(1, last.Where(u => u.Entry > 0).Sum(u => u.Unit.Count));
        Assert.Equal(20, last.Where(u => u.Entry == 0).Sum(u => u.Unit.Count));
        var log = new List<string>();
        var clamped = HordeSchedule.Units(a, 3, 3, 0, 200, new StepRandom(), log, 4);
        Assert.Equal(1, clamped.Where(u => u.Entry > 0).Sum(u => u.Unit.Count));      // the boss kept, regular units cut
        Assert.Equal(2, clamped.Where(u => u.Entry == 0).Sum(u => u.Unit.Count));
        Assert.Equal(["clamped by MaxUnitsPerWave: 21 -> 3"], log);
    }

    [Fact]
    public void Apocalypse_fails_when_a_top_up_target_is_not_raised_or_passes_its_caps()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(), P()]);
        var def = Apo.Def(Apo.Action(extra: Block()));                                  // perHotspot 3, max 12, 2 hotspots
        var run = Apo.Run(Apo.Active(def, T0));
        var w = new ApoWorld();
        var lines = run.TopUp(w, T0, rig.Source());
        Assert.Equal(14, w.Ledger.PendingSpawns);                                        // 7 a hotspot, within max 20
        Assert.Equal(["scaling apo roamers: players 3, per hotspot 7, max 20", "apocalypse apo roamers: +7 at hotspot 1", "apocalypse apo roamers: +7 at hotspot 2"], lines);
        Assert.Equal((30, 100), Scaling.RoamerTargets(new ApocalypseRoamers(10, 2, 100, 30, 60), 5, 20));   // never past MaxRoamers
        Assert.Equal((13, ApocalypseLimits.MaxRoamers), Scaling.RoamerTargets(new ApocalypseRoamers(3, 2, 90, 4, 40), 2, 10));
        var tight = new ApoWorld(maxTracked: 9);
        Apo.Run(Apo.Active(def, T0)).TopUp(tight, T0, rig.Source());
        Assert.Equal(9, tight.Ledger.PendingSpawns);                                     // the free tracked slots win
    }

    [Fact]
    public void Apocalypse_fails_when_a_spread_queues_more_than_before()
    {
        string? Spread(string action)
        {
            var a = Apo.Active(Apo.Def(action), T0);
            return Apo.Run(a).SpreadOn(new ApoWorld(), new ScoreSides(null, new OurUnit("apo", 101, T0)), (5, 5), T0);
        }
        Assert.Equal("apocalypse apo spread: +2 (2/12, 2/4 this minute)", Spread(Apo.Action(extra: Block(5))));
        Assert.Equal(Spread(Apo.Action()), Spread(Apo.Action(extra: Block(5))));
    }

    static ApocalypseRecord Record(int risen, int living, int[] roamers)
    {
        var r = new ApocalypseRecord
        {
            EventId = "apo",
            StartedUtc = T0,
            EndsUtc = T0.AddHours(1),
            Goal = new RecordPoint { X = 500, Y = 12, Z = 0 },
            Hotspots = [new RecordSpot { X = 0, Z = 0 }, new RecordSpot { X = 1000, Z = 0 }],
            HordesRisen = risen,
            Roamers = roamers.ToList(),
            SpreadWindow = 0,
        };
        r.Hordes.Add(new RecordHorde { Number = 1, FrontX = 120, FrontZ = 0, Living = living });
        Assert.Null(r.Problem());
        return r;
    }

    [Fact]
    public void Apocalypse_fails_when_a_resume_rebuilds_more_than_its_record()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(), P()]);
        var def = Apo.Def(Apo.Action(extra: Block(2, level: true, offset: 2, perWave: 1)));
        var record = Record(1, 6, [3, 2]);
        var plain = ApocalypseRun.Resumed(Apo.Active(def, T0), record, T0.AddMinutes(3));
        var w = new ApoWorld();
        var (rebuilt, rises, topUp) = plain.Rebuild(w, record, T0.AddMinutes(3), rig.Source());
        Assert.Equal(11, rebuilt);                                                       // the record's 3 + 2 roamers and 6 of horde 1
        Assert.Empty(rises);
        Assert.Empty(topUp);
        Assert.All(w.Ledger.TakeSpawns(), o => Assert.False(o.Tuning.Scaled));         // count and level unscaled again (Review 1 F6)
        // a missed horde after the resume is scaled, the rebuild still the record's
        var late = ApocalypseRun.Resumed(Apo.Active(def, T0), record, T0.AddMinutes(7));
        var w2 = new ApoWorld();
        var (rebuilt2, rises2, topUp2) = late.Rebuild(w2, record, T0.AddMinutes(7), rig.Source());
        Assert.Equal(11, rebuilt2);
        Assert.Equal(16, rises2.Single().Queued);                                        // 12 + 4
        Assert.Equal("scaling apo horde 2: players 3, gear 40, level 43, units 12 -> 16", rises2.Single().Scaling);
        Assert.StartsWith("scaling apo roamers: players 3, ", topUp2[0]);
    }

    [Fact]
    public void Apocalypse_passes_horde_and_roamer_lines()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(), P()]);
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block())), T0));
        var rise = run.Rise(new ApoWorld(), T0.AddMinutes(1), out var topUp, scale: rig.Source())!;
        Assert.Equal("scaling apo horde 1: players 3, gear 40, level own, units 8 -> 12", rise.Scaling);
        Assert.Equal("scaling apo roamers: players 3, per hotspot 7, max 20", topUp[0]);
    }

    [Fact]
    public void Apocalypse_empty_no_block_reads_nothing()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P(), P()]);
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        var w = new ApoWorld();
        var rise = run.Rise(w, T0.AddMinutes(1), out var topUp, scale: rig.Source())!;
        Assert.Equal((8, (string?)null), (rise.Queued, rise.Scaling));
        Assert.DoesNotContain(topUp, l => l.StartsWith("scaling ", StringComparison.Ordinal));
        Assert.Equal(0, rig.Reads);
    }

    // ---- D6 Gear

    [Fact]
    public void Gear_fails_when_30_9_41_and_50_do_not_average_40() => Assert.Equal(40, Scaling.Average([30.9, 41, 50]));

    [Fact]
    public void Gear_fails_when_40_5_does_not_round_to_41()
    {
        Assert.Equal(41, Scaling.Average([40, 41]));                                     // mean 40.5 rounds half up
        Assert.Equal(40, Scaling.Average([40.99]));                                      // each level rounded down first
        Assert.Equal(40, Scaling.Average([40, 40, 41]));                                 // 40.33
        Assert.Equal(41, Scaling.Average([40, 41, 41]));                                 // 40.67
    }

    [Fact]
    public void Gear_fails_when_an_unreadable_player_enters_the_average_or_leaves_the_count()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(gear: 30), P(gear: null), P(gear: double.NaN), P(gear: 50)]);
        var read = Scaling.Read(rig, Scope.Global, true);
        Assert.Equal(4, read.Players);
        Assert.Equal(40, read.Gear);
    }

    [Fact]
    public void Gear_fails_when_an_empty_set_gives_a_number()
    {
        Assert.Null(Scaling.Average([]));
        Assert.Null(Scaling.Average([null, double.NaN]));
        var rig = new ScalingRig();
        rig.Rows.Add(P(gear: null));
        var read = Scaling.Read(rig, Scope.Global, true);
        Assert.Equal((1, (int?)null), (read.Players, read.Gear));
    }

    [Fact]
    public void Gear_passes_floor_then_half_up() => Assert.Equal(45, Scaling.Average([44.9, 45.9]));

    [Fact]
    public void Gear_empty_none_in_the_line()
    {
        var action = Waves(WavesAction(Block(1, level: true)));
        Assert.Equal("scaling sc wave 1: players 2, gear none, level own, units 5 -> 7", Scaling.ForWave(action, "sc", 1, Read(2, null)).Line);
    }

    // ---- D7 Level

    static readonly Scaling LevelBlock = new(0, true, 2, 3);

    [Fact]
    public void Level_fails_when_average_40_offset_2_delta_1_and_per_wave_3_do_not_give_43_and_49()
    {
        Assert.Equal(43, LevelBlock.Level(40, new SpawnModifiers(LevelDelta: 1), 1));
        Assert.Equal(49, LevelBlock.Level(40, new SpawnModifiers(LevelDelta: 1), 3));
        // through the wave's tuning: a waveList entry with levelDelta 1
        var action = Waves(ListAction(Block(0, level: true, offset: 2, perWave: 3)));
        var entry = action.UnitsOf(2).Single();
        Assert.Equal(new LevelArg(false, 43), SpawnTuning.For(action, entry, Read(1), 1).Level);
        Assert.Equal(new LevelArg(false, 49), SpawnTuning.For(action, entry, Read(1), 3).Level);
    }

    [Fact]
    public void Level_fails_when_a_horde_or_a_roamer_takes_another_wave_number()
    {
        var rig = new ScalingRig();
        rig.Rows.Add(P());
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block(0, level: true, offset: 2, perWave: 3))), T0));
        var w = new ApoWorld();
        run.TopUp(w, T0, rig.Source());
        Assert.All(w.Ledger.TakeSpawns(), o => Assert.Equal(new LevelArg(false, 42), o.Tuning.Level));            // a roamer: wave 1
        run.Rise(w, T0.AddMinutes(1), out _, topUp: false, scale: rig.Source());
        Assert.All(w.Ledger.TakeSpawns(), o => Assert.Equal(new LevelArg(false, 42), o.Tuning.Level));            // horde 1
        run.Rise(w, T0.AddMinutes(6), out _, topUp: false, scale: rig.Source());
        Assert.All(w.Ledger.TakeSpawns(), o => Assert.Equal(new LevelArg(false, 45), o.Tuning.Level));            // horde 2
    }

    [Fact]
    public void Level_fails_when_the_result_leaves_1_to_120()
    {
        Assert.Equal(120, new Scaling(0, true, 20, 5).Level(118, null, 5));
        Assert.Equal(1, new Scaling(0, true, -20, 0).Level(3, new SpawnModifiers(LevelDelta: -5), 1));
    }

    [Fact]
    public void Level_fails_when_an_absolute_level_is_replaced()
    {
        Assert.Null(LevelBlock.Level(40, new SpawnModifiers(Level: 30), 1));
        var configured = SpawnTuning.TuningFrom(new SpawnModifiers(Level: 30, MaxHealth: 1.5));
        var tuned = Scaling.Tune(configured, LevelBlock, 40, new SpawnModifiers(Level: 30, MaxHealth: 1.5), 2);
        Assert.Same(configured, tuned);
        // the units form's action-wide absolute level, and a boss's own, stay
        var action = Waves(WavesAction(Keys("\"modifiers\": { \"level\": 33 }", Block(0, level: true))));
        Assert.Equal(new LevelArg(false, 33), SpawnTuning.For(action, action.Units[0], Read(2), 1).Level);
    }

    [Fact]
    public void Level_fails_when_growth_added_levels_are_lost()
    {
        var action = Waves(WavesAction(Keys("\"growth\": { \"addUnits\": 1, \"addLevels\": 2 }", Block(0, level: true, offset: 2, perWave: 3))));
        var entry = action.UnitsOf(3)[0];
        Assert.Equal(4, entry.Modifiers!.LevelDelta);
        Assert.Equal(new LevelArg(false, 40 + 2 + 4 + 2 * 3), SpawnTuning.For(action, entry, Read(1), 3).Level);
    }

    [Fact]
    public void Level_fails_when_another_modifier_changes()
    {
        var mods = new SpawnModifiers(LevelDelta: 1, MaxHealth: 1.5, Power: 1.25, MoveSpeed: 1.1, AttackSpeed: 1.2);
        var configured = SpawnTuning.TuningFrom(mods);
        var tuned = Scaling.Tune(configured, LevelBlock, 40, mods, 1);
        Assert.Equal(configured.Stats, tuned.Stats);
        Assert.Equal(new LevelArg(false, 43), tuned.Level);
    }

    [Fact]
    public void Level_fails_when_average_none_changes_a_level()
    {
        var mods = new SpawnModifiers(LevelDelta: 1);
        Assert.Null(LevelBlock.Level(null, mods, 1));
        Assert.Null(new Scaling(2).Level(40, mods, 1));                                      // levelFromPlayers false
        var configured = SpawnTuning.TuningFrom(mods);
        Assert.Same(configured, Scaling.Tune(configured, LevelBlock, null, mods, 1));
        Assert.Same(configured, Scaling.Tune(configured, null, 40, mods, 1));
    }

    [Fact]
    public void Level_passes_scaled_tuning()
    {
        var tuned = Scaling.Tune(UnitTuning.None, LevelBlock, 40, null, 2);
        Assert.Equal((new LevelArg(false, 45), true, (LevelArg?)null), (tuned.Level!.Value, tuned.Scaled, tuned.Own));
    }

    [Fact]
    public void Level_empty_no_modifiers_no_average()
    {
        Assert.Same(UnitTuning.None, Scaling.Tune(UnitTuning.None, LevelBlock, null, null, 1));
        Assert.Null(LevelBlock.LineLevel(null, 1));
    }

    // ---- D8 VBlood

    [Fact]
    public void VBlood_fails_when_a_vblood_unit_gets_the_scaled_level()
    {
        var rig = new ScalingRig();
        var action = Waves(WavesAction(Keys(BossKey, Block(1, level: true, offset: 2))));
        var last = Scaling.ForWave(action, "sc", 3, Read(1)).Entries(0);
        var boss = SpawnTuning.For(action, last[0], Read(1), 3);
        Assert.True(boss.Scaled);
        Assert.Equal(new LevelArg(true, 2), Scaling.LevelAtSetup(boss, () => rig.VBloods.Contains(last[0].Prefab)));      // its own levelDelta
        // a horde's boss keeps its own level too
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1), extra: Keys("\"bosses\": [ " + Apo.BossJson() + " ]", Block(0, level: true)))), T0));
        var w = new ApoWorld();
        rig.Rows.Add(P());
        run.Rise(w, T0.AddMinutes(1), out _, topUp: false, scale: rig.Source());
        var orders = w.Ledger.TakeSpawns();
        var bossOrder = orders.Single(o => o.Prefab == Tourok);
        Assert.Null(Scaling.LevelAtSetup(bossOrder.Tuning, () => rig.VBloods.Contains(bossOrder.Prefab)));                  // the prefab's level
        Assert.All(orders.Where(o => o.Prefab != Tourok), o => Assert.Equal(new LevelArg(false, 40), Scaling.LevelAtSetup(o.Tuning, () => rig.VBloods.Contains(o.Prefab))));
    }

    [Fact]
    public void VBlood_fails_when_a_vblood_loses_its_entry_level()
    {
        var absolute = SpawnTuning.TuningFrom(new SpawnModifiers(Level: 60));
        Assert.Same(absolute, Scaling.Tune(absolute, LevelBlock, 40, new SpawnModifiers(Level: 60), 1));             // S-7: never scaled
        var delta = Scaling.Tune(SpawnTuning.TuningFrom(new SpawnModifiers(LevelDelta: 3)), LevelBlock, 40, new SpawnModifiers(LevelDelta: 3), 1);
        Assert.Equal(new LevelArg(true, 3), Scaling.LevelAtSetup(delta, () => true));
        Assert.Equal(new LevelArg(true, 3), Scaling.LevelAtSetup(delta, () => throw new InvalidOperationException("no VBloodUnit read")));   // D12
    }

    [Fact]
    public void VBlood_fails_when_a_non_vblood_keeps_its_prefab_level()
    {
        var rig = new ScalingRig();
        var action = Waves(WavesAction(Keys(BossKey, Block(1, level: true, offset: 2))));
        var last = Scaling.ForWave(action, "sc", 3, Read(1)).Entries(0);
        var thug = SpawnTuning.For(action, last.First(e => e.Prefab == Thug), Read(1), 3);
        Assert.Equal(new LevelArg(false, 42), Scaling.LevelAtSetup(thug, () => rig.VBloods.Contains(Thug)));
    }

    [Fact]
    public void VBlood_passes_boss_count_follows_its_entry()
    {
        var action = Waves(WavesAction(Keys(BossKey, Block(5, level: true))));
        Assert.Equal(1, Scaling.ForWave(action, "sc", 3, Read(9)).Entries(0).Where(e => e.Prefab == Tourok).Sum(e => e.Count));
    }

    [Fact]
    public void VBlood_empty_unscaled_tuning_reads_no_flag()
    {
        var tuning = SpawnTuning.TuningFrom(new SpawnModifiers(LevelDelta: 2));
        Assert.Equal(new LevelArg(true, 2), Scaling.LevelAtSetup(tuning, () => throw new InvalidOperationException("never read")));
    }

    // ---- D9 Caps

    [Fact]
    public void Caps_fails_when_nine_players_and_u5_pass_the_caps()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block(5), units: Thugs5)), "sc", 1, Read(9)).Entries(0);
        Assert.Equal(45, entries.Sum(e => e.Count));
        var wave = WaveGate.DecideEntries(new WaveFacts(1, "sc", false), () => WaveRoll.ExpandEntries(entries, new StepRandom()), 20, 190, 200);
        Assert.Equal(10, wave.Units.Sum(u => u.Count));
        Assert.Equal(["clamped by MaxUnitsPerWave: 45 -> 20", "skipped by MaxTrackedUnits: 10 of 20"], wave.CapLines);
        // a horde of 8 + 40 meets the same caps
        var rig = new ScalingRig();
        rig.Rows.AddRange(Enumerable.Range(0, 9).Select(_ => P()));
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block(5))), T0));
        var w = new ApoWorld(maxTracked: 200, maxPerWave: 20);
        var rise = run.Rise(w, T0.AddMinutes(1), out _, topUp: false, scale: rig.Source())!;
        Assert.Equal(20, rise.Queued);
        Assert.Equal(["clamped by MaxUnitsPerWave: 48 -> 20"], rise.Log);
        var tight = new ApoWorld(maxTracked: 15, maxPerWave: 50);
        Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block(5))), T0)).TopUp(tight, T0, rig.Source());
        Assert.Equal(15, tight.Ledger.Occupied);
    }

    [Fact]
    public void Caps_fails_when_huge_inputs_wrap_past_the_caps()
    {
        // Codex step 2: counts and levels saturate instead of wrapping negative, so the caps and the clamp still decide
        var block = new Scaling(UnitsPerPlayer: 5, LevelFromPlayers: true, LevelOffset: 20, LevelPerWave: 5);
        Assert.Equal(Scaling.MaxExtra, block.Extra(int.MaxValue));
        Assert.Equal(0, block.Extra(int.MinValue));
        var entries = Scaling.ScaleEntries([new UnitEntry("CHAR_Bandit_Thug", int.MaxValue - 1, 1.0, null)], Scaling.MaxExtra);
        Assert.Equal(int.MaxValue, entries[0].Count);
        var (per, max) = Scaling.RoamerTargets(new ApocalypseRoamers(3, 2, 90, 4, 40), int.MaxValue, Scaling.MaxExtra);
        Assert.True(per > 0 && max > 0 && max <= ApocalypseLimits.MaxRoamers);
        Assert.Equal(Scaling.MaxLevel, block.Level(int.MaxValue, null, int.MaxValue));
        Assert.Equal(Scaling.MinLevel, (block with { LevelOffset = -20, LevelPerWave = 0 }).Level(int.MinValue, null, 1));
    }

    [Fact]
    public void Caps_fails_when_a_scaled_unit_bypasses_the_budget_or_the_ledger()
    {
        var action = Waves(WavesAction(Block(2, level: true), units: Thugs5));
        var entries = Scaling.ForWave(action, "sc", 1, Read(3)).Entries(0);
        var wave = WaveGate.DecideEntries(new WaveFacts(1, "sc", false), () => WaveRoll.ExpandEntries(entries, new StepRandom()), 50, 0, 200);
        var ledger = new SpawnLedger(new LedgerLimits(200, 50, 4, 4));
        foreach (var u in wave.Units)
            ledger.Request(u.Prefab, "sc", u.Count, new UnitLifetime(T0.AddMinutes(10), 600), SpawnTuning.For(action, u, Read(3), 1), i => (i, 0, 0), wave: 1);
        Assert.Equal(9, ledger.Occupied);
        var batches = new List<int>();
        for (var b = ledger.TakeSpawns(); b.Count > 0; b = ledger.TakeSpawns())
        {
            batches.Add(b.Count);
            Assert.All(b, o => Assert.Equal(("sc", 1, true), (o.EventId, o.Wave, o.Tuning.Scaled)));
        }
        Assert.Equal([4, 4, 1], batches);                                                    // MaxSpawnsPerTick a tick
    }

    [Fact]
    public void Caps_fails_when_a_purge_or_pillar_off_leaves_a_scaled_wave_running()
    {
        var engine = ApocalypseTests.Engine(out _, Json.Event("sc", action: WavesAction(Block(5))), Json.Event("raw", action: WavesAction()));
        // the controls refuse a scaled start exactly as an unscaled one
        var refusals = new[]
        {
            new ControlState(true, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3),
            new ControlState(false, false, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3),
            new ControlState(false, true, new HashSet<Pillar>(), 0, 3),
            Open(3, 3),
        };
        foreach (var c in refusals)
        {
            var scaled = engine.Start("sc", "manual", T0, c);
            Assert.NotNull(scaled);
            Assert.Equal(engine.Start("raw", "manual", T0, c)!.Human.Replace("raw", "sc", StringComparison.Ordinal), scaled.Human);
        }
        Assert.Null(engine.Start("sc", "manual", T0, Open()));
        Assert.Single(engine.CancelAll(300));                                                // a purge ends it
        Assert.Null(engine.Find("sc"));
        Assert.Null(engine.NextWave("sc", T0.AddMinutes(5)));
        Assert.Null(engine.Start("sc", "manual", T0.AddMinutes(10), Open()));
        Assert.NotNull(engine.Cancel("sc"));                                                 // a pillar off ends it
        Assert.Null(engine.Find("sc"));
    }

    [Fact]
    public void Caps_fails_when_a_load_time_ceiling_changes()
    {
        Assert.Equal(50, Limits.MaxUnitsPerWave.Max);
        // 10 thugs + 4 waves of 10 more: 50 in the last wave loads, 51 does not, scaled or not
        string Growth(int count, string? block) => WavesAction(Keys("\"growth\": { \"addUnits\": 10 }", block),
            units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": " + count + " } ]", waves: "5");
        Assert.True(Json.One(Json.Event(action: Growth(10, Block(5)))).Startable);
        Assert.Equal(EventValidator.GrowthLastWave, Json.One(Json.Event(action: Growth(11, Block(5)))).DisabledReason);
        Assert.Equal(EventValidator.GrowthLastWave, Json.One(Json.Event(action: Growth(11, null))).DisabledReason);
        Assert.Equal(50, LastWaveUnits(Waves(Growth(10, Block(5)))));
    }

    static int LastWaveUnits(SpawnWavesAction a) => a.UnitsOf(WaveCount(a)).Sum(e => e.Count);

    [Fact]
    public void Caps_passes_scaled_wave_within_caps()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block(5), units: Thugs5)), "sc", 1, Read(2)).Entries(0);
        var wave = WaveGate.DecideEntries(new WaveFacts(1, "sc", false), () => WaveRoll.ExpandEntries(entries, new StepRandom()), 50, 0, 200);
        Assert.Equal(10, wave.Units.Sum(u => u.Count));
        Assert.Empty(wave.CapLines);
    }

    [Fact]
    public void Caps_empty_full_ledger_queues_nothing()
    {
        var entries = Scaling.ForWave(Waves(WavesAction(Block(5), units: Thugs5)), "sc", 1, Read(9)).Entries(0);
        var wave = WaveGate.DecideEntries(new WaveFacts(1, "sc", false), () => WaveRoll.ExpandEntries(entries, new StepRandom()), 50, 200, 200);
        Assert.Equal(0, wave.Units.Sum(u => u.Count));
    }

    // ---- D10 Lines

    static readonly Regex DecisionLine = new(
        @"^scaling [a-z0-9-]+ (wave|horde) \d+: players \d+, gear (\d+|none), level (\d+|own), units \d+ -> \d+(, groups \d+(/\d+)*)?$", RegexOptions.CultureInvariant);

    static readonly Regex RoamersLine = new(@"^scaling [a-z0-9-]+ roamers: players \d+, per hotspot \d+, max \d+$", RegexOptions.CultureInvariant);

    [Fact]
    public void Lines_fails_when_a_scaled_decision_logs_no_line_or_two()
    {
        var wave = Scaling.ForWave(Waves(WavesAction(Block(2, level: true))), "sc", 2, Read(3));
        Assert.Matches(DecisionLine, wave.Line);
        Assert.Matches(DecisionLine, FanOutScaled().Line);
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P()]);
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block())), T0));
        var rise = run.Rise(new ApoWorld(), T0.AddMinutes(1), out var topUp, scale: rig.Source())!;
        Assert.Matches(DecisionLine, rise.Scaling);
        Assert.DoesNotContain(rise.Log, l => l.StartsWith("scaling ", StringComparison.Ordinal));      // one line, not two
        Assert.Single(topUp, l => l.StartsWith("scaling ", StringComparison.Ordinal));
    }

    [Fact]
    public void Lines_fails_when_an_unchanged_top_up_logs_one()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P()]);
        var book = new ScalingBook();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: Block())), T0));
        var w = new ApoWorld();
        Assert.Single(run.TopUp(w, T0, rig.Source(book)), l => l.StartsWith("scaling ", StringComparison.Ordinal));
        Assert.DoesNotContain(run.TopUp(w, T0.AddSeconds(30), rig.Source(book)), l => l.StartsWith("scaling ", StringComparison.Ordinal));
        rig.Rows.Add(P());
        Assert.Equal("scaling apo roamers: players 3, per hotspot 7, max 20", run.TopUp(w, T0.AddSeconds(60), rig.Source(book))[0]);
    }

    [Fact]
    public void Lines_fails_when_an_unscaled_event_logs_one()
    {
        Assert.Null(Scaling.ForWave(Waves(WavesAction()), "sc", 1, Read(3)).Line);
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(), P()]);
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        var w = new ApoWorld();
        Assert.DoesNotContain(run.TopUp(w, T0, rig.Source()), l => l.StartsWith("scaling ", StringComparison.Ordinal));
        Assert.Null(run.Rise(w, T0.AddMinutes(1), out _, scale: rig.Source())!.Scaling);
    }

    [Fact]
    public void Lines_fails_when_a_shape_differs()
    {
        Assert.Equal("scaling sc wave 2: players 3, gear 40, level 43, units 5 -> 13",
            Scaling.ForWave(Waves(WavesAction(Block(2, level: true, offset: 2, perWave: 1))), "sc", 2, Read(3)).Line);
        Assert.Equal("scaling sc wave 1: players 0, gear none, level own, units 5 -> 5", Scaling.ForWave(Waves(WavesAction(Block(2, level: true))), "sc", 1, Read(0, null)).Line);
        Assert.Equal("scaling sc horde 4: players 2, gear 33, level 30, units 9 -> 12", Scaling.Lines.Horde("sc", 4, new ScalingView(2, 33, 30), 9, 12));
        Assert.Equal("scaling sc roamers: players 2, per hotspot 5, max 16", Scaling.Lines.Roamers("sc", 2, 5, 16));
        Assert.Equal("scaling sc wave 1: players 4, gear 40, level own, units 4 -> 5, groups 2/1/1",
            Scaling.Lines.Wave("sc", 1, new ScalingView(4, 40, null), 4, 5, [2, 1, 1]));
    }

    [Fact]
    public void Lines_passes_the_three_shapes()
    {
        Assert.Matches(DecisionLine, Scaling.Lines.Wave("ambush-2", 10, new ScalingView(100, 120, 120), 50, 99));
        Assert.Matches(DecisionLine, Scaling.Lines.Horde("apo", 10, new ScalingView(1, null, null), 50, 50));
        Assert.Matches(RoamersLine, Scaling.Lines.Roamers("apo", 9, 10, 100));
    }

    [Fact]
    public void Lines_empty_unscaled_logs_nothing()
    {
        var wave = Scaling.ForWave(Waves(WavesAction(Block())), "sc", 1, null);
        Assert.Null(wave.Line);
        Assert.Null(wave.View);
    }
}
