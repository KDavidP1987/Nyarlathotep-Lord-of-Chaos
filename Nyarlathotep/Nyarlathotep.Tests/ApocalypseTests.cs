using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The apocalypse definitions and the game side of the tests (apocalypse D1-D15, D36): events.json text for
/// every key, and a world whose ledger confirms, kills and despawns units on demand.</summary>
static class Apo
{
    public const string Zombie = "CHAR_Undead_SkeletonSoldier_Base";
    public const string Thug = "CHAR_Bandit_Thug";
    public const string Tourok = "CHAR_Bandit_Tourok_VBlood";
    public const string Deadeye = "CHAR_Bandit_Deadeye";
    public const string GoalName = "Brighthaven";

    public static string Unit(string prefab = Zombie, string count = "3", string? more = null) =>
        "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + (more is null ? "" : ", " + more) + " }";

    public static string BossJson(string prefab = Tourok, string count = "1", string? more = null) =>
        "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + (more is null ? "" : ", " + more) + " }";

    public static string Hotspots(int count = 2, int minNatives = 3, int minSpacing = 200) =>
        $"{{ \"count\": {count}, \"minNatives\": {minNatives}, \"minSpacing\": {minSpacing} }}";

    public static string Roamers(int perHotspot = 3, int perKill = 2, int max = 12, int perMinute = 4, int huntRange = 40) =>
        $"{{ \"perHotspot\": {perHotspot}, \"perKill\": {perKill}, \"max\": {max}, \"perMinute\": {perMinute}, \"huntRange\": {huntRange} }}";

    public static string Hordes(int count = 3, int firstAfter = 1, int every = 5, int size = 8, int growth = 4) =>
        $"{{ \"count\": {count}, \"firstAfterMinutes\": {firstAfter}, \"everyMinutes\": {every}, \"size\": {size}, \"growth\": {growth} }}";

    public static string Goal(string location = "{ \"type\": \"Point\", \"x\": 500, \"z\": 0, \"y\": 12 }", int radius = 20, string name = GoalName, string? extra = null) =>
        "{ \"location\": " + location + ", \"radius\": " + radius + ", \"name\": \"" + name + "\"" + (extra is null ? "" : ", " + extra) + " }";

    /// <summary>An Apocalypse action: Faction_Undead, two units, 2 hotspots, 3 roamers each, 3 hordes from 1 min every
    /// 5 min of 8 + 4, a Point goal at (500, 0) height 12, radius 20, marching at 2 m/s.</summary>
    public static string Action(string faction = "\"Faction_Undead\"", string? units = null, string? hotspots = null, string? roamers = null,
        string? hordes = null, string? goal = null, string march = "2.0", string? extra = null) =>
        "\"action\": { \"type\": \"Apocalypse\", \"faction\": " + faction + ", \"units\": " + (units ?? "[ " + Unit() + ", " + Unit(Thug, "1") + " ]") +
        ", \"hotspots\": " + (hotspots ?? Hotspots()) + ", \"roamers\": " + (roamers ?? Roamers()) + ", \"hordes\": " + (hordes ?? Hordes()) +
        ", \"goal\": " + (goal ?? Goal()) + ", \"marchSpeed\": " + march + (extra is null ? "" : ", " + extra) + " }";

    public static string Event(string id = "apo", string? trigger = null, string? action = null, int duration = 3600) =>
        Json.Event(id, trigger, action: action ?? Action()).Replace("\"durationSeconds\": 600", $"\"durationSeconds\": {duration}")
            .Replace("\"name\": \"Bandit raid\"", "\"name\": \"Night of the dead\"");

    public static DefinitionSet Set(params string[] events)
    {
        var r = EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        Assert.All(r.Set.All, d => Assert.True(d.Startable, d.DisabledReason));
        return r.Set;
    }

    public static EventDefinition Def(string? action = null, int duration = 3600, string id = "apo") => Set(Event(id, null, action, duration)).Find(id)!;

    public static ActiveEvent Active(EventDefinition d, DateTime started, (float X, float Y, float Z)? origin = null) =>
        new(new RunningInstance(d, started, started.AddSeconds(d.DurationSeconds)), "manual", origin);

    public static readonly IReadOnlyList<Hotspot> TwoSpots = [new(1, 0, 0, 10, 0, 0), new(2, 1000, 0, 8, 10, 0)];

    public static ApocalypseRun Run(ActiveEvent a, IReadOnlyList<Hotspot>? spots = null) => new(a, spots ?? TwoSpots, (500f, 12f, 0f));
}

/// <summary>A deterministic random source that walks [0, 1) in steps of 0.37.</summary>
sealed class StepRandom : IRandom
{
    double _v;
    public double NextDouble() => _v = (_v + 0.37) % 1.0;
}

/// <summary>The game side of a run: a ledger with the given caps, a placement on a circle of the asked radius, and helpers
/// that confirm every order (as SpawnTracker does), kill a unit or despawn it, each feeding the run's tally.</summary>
sealed class ApoWorld(int maxTracked = 200, int maxPerWave = 50) : IApocalypseWorld
{
    public SpawnLedger Ledger { get; } = new(new LedgerLimits(maxTracked, maxPerWave, 1000, 1000));
    public IRandom Random { get; set; } = new StepRandom();
    public int GraceSeconds => 60;
    public int DrainMarginSeconds => 120;
    public List<(float X, float Z, float Radius)> Placed { get; } = [];
    long _key = 1000;

    public (float X, float Y, float Z) Place(float x, float z, float radius, int i, int n)
    {
        var (px, pz) = SpawnLedger.Around(x, z, radius, i, n, 0.3);
        Placed.Add((x, z, radius));
        return (px, 0, pz);
    }

    /// <summary>Confirms every waiting order at <paramref name="at"/>; returns the new units.</summary>
    public List<TrackedUnit> SpawnAll(ApocalypseRun run, DateTime at)
    {
        var spawned = new List<TrackedUnit>();
        for (var batch = Ledger.TakeSpawns(); batch.Count > 0; batch = Ledger.TakeSpawns())
            foreach (var o in batch)
            {
                var key = ++_key;
                Assert.True(Ledger.Confirm(o, key, at));
                run.Spawned(o.Wave);
                spawned.Add(Ledger.Find(key)!);
            }
        return spawned;
    }

    /// <summary>Fails every waiting order (the game refused the spawn).</summary>
    public int FailAll(ApocalypseRun run)
    {
        var n = 0;
        for (var batch = Ledger.TakeSpawns(); batch.Count > 0; batch = Ledger.TakeSpawns())
            foreach (var o in batch) { Ledger.Fail(o); run.FailedToSpawn(o.Wave); n++; }
        return n;
    }

    public void Kill(ApocalypseRun run, long key) { Assert.True(Ledger.Forget(key)); run.Tally.Death(); }

    public void Despawn(ApocalypseRun run, long key)
    {
        Ledger.QueueDespawn(key);
        Assert.Contains(key, Ledger.TakeDespawns());
        run.Tally.Despawn();
    }

    public void Remove(ApocalypseRun run, long key) { Assert.True(Ledger.Forget(key)); run.Tally.Removal(); }

    public int Alive(ApocalypseRun run) => Ledger.Units.Count(u => Apocalypse.Ours(u, run.Active));

    public List<TrackedUnit> OfWave(ApocalypseRun run, int wave) => Ledger.Units.Where(u => u.Wave == wave && Apocalypse.Ours(u, run.Active)).ToList();
}

/// <summary>apocalypse D2-D9, D12-D15, D36: hotspots and the native scan, roamers and their map-wide hunt, the spread, the
/// hordes' clock and size, the march, the player lines, the outcomes, every end path, the resume, the rolling lifetime,
/// the caps, the unit tally and the last horde's bosses, over Logic/Apocalypse.cs and the engine it drives.</summary>
public partial class ApocalypseTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);

    static ControlState Open(int active = 0, int max = 3) => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), active, max);

    static HotspotNative Native(float x, float z, string faction = "Faction_Undead", bool ours = false, bool vblood = false, string region = "FarbaneWoods") =>
        new(new UnitFacts(Apo.Zombie, faction, false, false, vblood, ours, false, null, () => region), x, z);

    static IEnumerable<HotspotNative> Cluster(float x, float z, int n, string faction = "Faction_Undead", string region = "FarbaneWoods") =>
        Enumerable.Range(0, n).Select(i => Native(x + i, z + i, faction, region: region));

    // ---- D2 Hotspots

    [Fact]
    public void Hotspots_fails_when_a_small_cell_or_a_close_pair_is_picked()
    {
        var action = Apo.Def().Apocalypse!;                                     // 2 hotspots, minNatives 3, minSpacing 200
        var natives = Cluster(150, 10, 6)                                       // cell (1, 0): 6, the best
            .Concat(Cluster(50, 10, 5))                                         // cell (0, 0): 5, but ~100 m from the first
            .Concat(Cluster(1050, 10, 3))                                       // cell (10, 0): 3, far enough
            .Concat(Cluster(2050, 10, 2))                                       // cell (20, 0): 2, under minNatives
            .ToList();
        var pick = Hotspots.Pick("apo", action, () => natives);
        Assert.Null(pick.Refusal);
        Assert.Equal([(1, 0, 6), (10, 0, 3)], pick.Spots.Select(s => (s.CellX, s.CellZ, s.Natives)));
        Assert.Equal(152.5f, pick.Spots[0].X, 3);                               // the mean of its natives
        Assert.Equal(12.5f, pick.Spots[0].Z, 3);
        Assert.All(pick.Spots.SelectMany(a => pick.Spots.Where(b => b != a), (a, b) => Apocalypse.Distance(a.X, a.Z, b.X, b.Z)),
            d => Assert.True(d >= action.Hotspots.MinSpacing));
        Assert.Equal("apocalypse apo hotspots: 2 (6 at 153 13, 3 at 1051 11)", pick.Line);
        // ties by cell x, then z
        var tie = Hotspots.Pick("apo", action, () => Cluster(1050, 1050, 4).Concat(Cluster(1050, 50, 4)).Concat(Cluster(50, 1050, 4)).ToList());
        Assert.Equal([(0, 10), (10, 0)], tie.Spots.Select(s => (s.CellX, s.CellZ)));
    }

    [Fact]
    public void Hotspots_fails_when_an_outside_native_or_ours_counts()
    {
        var scoped = Apo.Def(Apo.Action(extra: "\"scope\": [\"FarbaneWoods\"]")).Apocalypse!;
        var natives = Cluster(10, 10, 2).Concat(Cluster(20, 20, 4, region: "CursedForest"))           // 2 inside, 4 outside the scope
            .Append(Native(30, 30, ours: true)).Append(Native(31, 31, vblood: true))                    // ours and a V Blood
            .Concat(Cluster(40, 40, 3, faction: "Faction_Bandits"))                                      // another faction
            .ToList();
        var pick = Hotspots.Pick("apo", scoped, () => natives);
        Assert.Empty(pick.Spots);
        Assert.Equal("no hotspot: fewer than 3 Undead natives in one place in scope", pick.Refusal);
        // the same natives count once they are in scope, the faction's and not ours
        Assert.Single(Hotspots.Pick("apo", scoped, () => natives.Append(Native(12, 12))).Spots);
        Assert.Single(Hotspots.Pick("apo", Apo.Def().Apocalypse!, () => natives).Spots);                 // Global: the 6 count
    }

    [Fact]
    public void Hotspots_empty_world_refuses_the_start()
    {
        var action = Apo.Def().Apocalypse!;
        var none = Hotspots.Pick("apo", action, () => []);
        Assert.Equal(Hotspots.NoHotspot(3, "Faction_Undead"), none.Refusal);
        Assert.Equal("apocalypse apo: no hotspot: fewer than 3 Undead natives in one place in scope", none.Line);
        var thrown = Hotspots.Pick("apo", action, () => throw new InvalidOperationException("query broke"));
        Assert.Equal("no hotspot: native read failed: query broke", thrown.Refusal);
        Assert.Empty(thrown.Spots);
    }

    static string Source() => File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Logic", "Apocalypse.cs"));

    [Fact]
    public void Hotspots_fails_when_the_scan_misses_a_planted_write()
    {
        var source = Source();
        Assert.Contains(NativeScan.Open, source);
        Assert.Empty(NativeScan.Problems([("Apocalypse.cs", source)]));
        var reader = source.IndexOf(NativeScan.Open, StringComparison.Ordinal);
        foreach (var write in new[] { "entity.Write(health);", "entity.Write<Health>(h);", "entity.AddComponentSafe<Buff>();", "entity.DestroySafe();", "entity.RemoveBuffSafe(b);" })
        {
            var planted = source.Insert(reader + NativeScan.Open.Length, "\n        " + write);
            Assert.NotEmpty(NativeScan.Problems([("Apocalypse.cs", planted)]));
        }
        Assert.Contains("is not closed", Assert.Single(NativeScan.Problems([("x.cs", "// <native-read>\n read();")])));
        // step 3: every source file of the plugin, the game-side reader of Services/ApocalypseAction.cs marked among them
        var plugin = Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep");
        var all = Directory.GetFiles(plugin, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (Path.GetRelativePath(plugin, f), File.ReadAllText(f))).ToList();
        var action = all.Single(f => f.Item1 == Path.Combine("Services", "ApocalypseAction.cs"));
        Assert.Contains(NativeScan.Open, action.Item2);
        Assert.Empty(NativeScan.Problems(all));
        var at = action.Item2.IndexOf(NativeScan.Open, StringComparison.Ordinal) + NativeScan.Open.Length;
        Assert.NotEmpty(NativeScan.Problems([(action.Item1, action.Item2.Insert(at, "\n            unit.Write(new Health());"))]));
    }

    [Fact]
    public void Hotspots_empty_scan_set_fails() =>
        Assert.Equal([NativeScan.NoFiles], NativeScan.Problems([]));

    // ---- D3 Roamers

    [Fact]
    public void Roamers_fails_when_a_top_up_passes_perHotspot_or_max()
    {
        Assert.Equal([2, 0, 3], RoamerPlan.TopUp(3, [1, 3, 0], 12, 100));
        Assert.Equal([1, 0, 0], RoamerPlan.TopUp(3, [1, 3, 0], 5, 100));          // max 5 with 4 alive: one more
        Assert.Equal([2, 0, 1], RoamerPlan.TopUp(3, [1, 3, 0], 12, 3));           // the free tracked slots
        Assert.Equal([0, 0], RoamerPlan.TopUp(3, [4, 5], 12, 100));               // over perHotspot already: none, never negative
        Assert.Equal([3, 3], RoamerPlan.TopUp(3, [0, 0], 12, 100));               // no hotspot skipped

        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Equal(["apocalypse apo roamers: +3 at hotspot 1", "apocalypse apo roamers: +3 at hotspot 2"], run.TopUp(w, T0));
        Assert.Equal(6, w.Ledger.PendingSpawns);                                    // every roamer is a ledger order
        Assert.Empty(run.TopUp(w, T0));                                             // the orders count before they spawn
        var units = w.SpawnAll(run, T0);
        Assert.Equal([3, 3], units.GroupBy(u => u.Wave).OrderBy(g => g.Key).Select(g => g.Count()));
        Assert.Equal([101, 102], units.Select(u => u.Wave).Distinct().OrderBy(x => x));
        Assert.All(w.Placed, p => Assert.Equal(Apocalypse.RoamerSpawnRadius, p.Radius));
        w.Kill(run, units.First(u => u.Wave == 101).Key);
        Assert.Equal(["apocalypse apo roamers: +1 at hotspot 1"], run.TopUp(w, T0.AddSeconds(5)));
    }

    [Fact]
    public void Roamers_fails_when_a_spread_roamer_is_left_out_of_its_nearest_hotspot()
    {
        var w = new ApoWorld();
        var a = Apo.Active(Apo.Def(), T0);
        var run = Apo.Run(a);
        run.TopUp(w, T0);
        var units = w.SpawnAll(run, T0);
        // a roamer of hotspot 1 kills a player near hotspot 2: the spread roamers join hotspot 2
        var killer = new OurUnit("apo", Apocalypse.RoamerWave(1), T0);
        Assert.NotNull(run.SpreadOn(w, new ScoreSides(null, killer), (980, 5), T0.AddSeconds(1)));
        Assert.Equal(2, RoamerPlan.NearestHotspot(Apo.TwoSpots, 980, 5));
        w.SpawnAll(run, T0.AddSeconds(1));
        Assert.Equal([3, 5], run.RoamersPerHotspot(w));
        // hotspot 2 is over perHotspot, so a top-up after a hotspot-2 death adds nothing there
        w.Kill(run, units.First(u => u.Wave == 102).Key);
        Assert.Empty(run.TopUp(w, T0.AddSeconds(2)));
        Assert.Equal(1, RoamerPlan.NearestHotspot(Apo.TwoSpots, 499, 0));          // ties go to the lower number
        Assert.Equal(1, RoamerPlan.NearestHotspot(Apo.TwoSpots, 500, 0));
    }

    static HuntCandidate Player(long key, float x, float z = 0, bool online = true, bool alive = true, bool territory = false, bool pvp = false) =>
        new(key, x, z, online, alive, territory, pvp);

    [Fact]
    public void Roamers_fails_when_a_target_is_other_than_the_nearest_eligible()
    {
        var players = new[]
        {
            Player(1, 10, online: false), Player(2, 20, territory: true), Player(3, 30, pvp: true), Player(4, 40, alive: false),
            Player(9, 5000), Player(7, 4000), Player(8, -4000),
        };
        Assert.Equal(7, RoamRule.Target(players, (0, 0))!.Value.Key);              // 4000 m off: no range, no scope
        Assert.Equal(7, RoamRule.Target(players.Append(Player(6, 4000, 0, territory: true)), (0, 0))!.Value.Key);
        Assert.Equal(7, RoamRule.Target([Player(8, -4000), Player(7, 4000)], (0, 0))!.Value.Key);    // ties by key
        Assert.Null(RoamRule.Target(players.Take(4), (0, 0)));                      // none eligible: they stay
        Assert.False(RoamRule.Eligible(Player(5, float.NaN)));
        // seeds: eligible players within 100 m only
        Assert.Equal([12L], RoamRule.Seeds([Player(12, 50), Player(13, 150), Player(14, 20, territory: true)], (0, 0)));
        var seen = new RoamTargets();
        Assert.Equal("apocalypse apo roam: hotspot 1 on a player", seen.Seen("apo", 1, 7));
        Assert.Null(seen.Seen("apo", 1, 7));
        Assert.NotNull(seen.Seen("apo", 1, 8));
        Assert.Null(seen.Seen("apo", 1, null));
        Assert.NotNull(seen.Seen("apo", 1, 8));
    }

    [Fact]
    public void Roamers_fails_when_a_jump_breaks_a_landing_rule()
    {
        var u = new RegionUnit(1, "apo", T0, 0, 0, 0, 2);
        Func<float, float, byte, bool> ground = (_, _, _) => true;
        Func<byte, float> height = _ => 0f;
        Func<float, float, bool> nowhere = (_, _) => false;
        // a player within 100 m of the unit: it waits
        Assert.Equal(JumpKind.Waiting, RoamRule.Plan(u, (1000, 0), [Player(1, 60)], nowhere, ground, height, new WalkBudget(1000)).Kind);
        // no player near: it jumps, never into claimed territory, never within 50 m of a player, never onto unwalkable ground
        var (kind, jump) = RoamRule.Plan(u, (1000, 0), [Player(1, 3000)], nowhere, ground, height, new WalkBudget(1000));
        Assert.Equal(JumpKind.Jump, kind);
        Func<float, float, bool> claimedAhead = (x, _) => x > 5;
        var claimed = RoamRule.Plan(u, (1000, 0), [], claimedAhead, ground, height, new WalkBudget(1000));
        Assert.True(claimed.Jump is null || claimed.Jump.Value.X <= 5, "landed in claimed territory");
        var near = RoamRule.Plan(u, (1000, 0), [Player(2, jump!.Value.X + 10, jump.Value.Z)], nowhere, ground, height, new WalkBudget(1000));
        Assert.True(near.Jump is null || Apocalypse.Distance(near.Jump.Value.X, near.Jump.Value.Z, jump.Value.X + 10, jump.Value.Z) >= HuntingGrounds.LandingDistance);
        Assert.NotEqual(JumpKind.Jump, RoamRule.Plan(u, (1000, 0), [], nowhere, (_, _, _) => false, height, new WalkBudget(1000)).Kind);
        // within reach of its target it hunts (no jump)
        Assert.Equal(JumpKind.InReach, RoamRule.Plan(u, (40, 0), [], nowhere, ground, height, new WalkBudget(1000)).Kind);
    }

    // ---- D4 Spread

    [Fact]
    public void Spread_fails_when_a_native_or_another_events_kill_spreads()
    {
        var a = Apo.Active(Apo.Def(), T0);
        Assert.True(SpreadGate.Qualifies(new ScoreSides(null, new OurUnit("apo", 101, T0)), a));                 // a roamer
        Assert.True(SpreadGate.Qualifies(new ScoreSides(null, new OurUnit("apo", 2, T0.AddMinutes(6))), a));     // a horde unit
        Assert.True(SpreadGate.Qualifies(new ScoreSides(null, new OurUnit("apo", 231, T0)), a));                 // a boss
        Assert.True(SpreadGate.Qualifies(new ScoreSides(null, null, new OurUnit("apo", 101, T0)), a));           // through its EntityOwner
        Assert.False(SpreadGate.Qualifies(new ScoreSides(null, null), a));                                       // a native killer
        Assert.False(SpreadGate.Qualifies(new ScoreSides(null, new OurUnit("raid", 1, T0)), a));                 // another event's
        Assert.False(SpreadGate.Qualifies(new ScoreSides(null, new OurUnit("apo", 101, T0.AddSeconds(-1))), a)); // an earlier instance's
        Assert.False(SpreadGate.Qualifies(new ScoreSides(new OurUnit("apo", 101, T0), new OurUnit("apo", 101, T0)), a));   // a victim of ours
        var w = new ApoWorld();
        var run = Apo.Run(a);
        Assert.Null(run.SpreadOn(w, new ScoreSides(null, null), (0, 0), T0));
        Assert.Equal(0, w.Ledger.PendingSpawns);
        Assert.Equal("apocalypse apo spread: +2 (2/12, 2/4 this minute)", run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, T0)), (5, 5), T0));
        Assert.All(w.Placed, p => Assert.Equal(Apocalypse.SpreadRadius, p.Radius));
    }

    [Fact]
    public void Spread_fails_when_the_window_or_max_lets_more_through()
    {
        var a = Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(max: 100, perMinute: 4))), T0);
        var w = new ApoWorld();
        var run = Apo.Run(a);
        var kill = new ScoreSides(null, new OurUnit("apo", 101, T0));
        for (var i = 0; i < 1000; i++) run.SpreadOn(w, kill, (i, 0), T0.AddMilliseconds(i * 50));
        Assert.Equal(4, w.Ledger.PendingSpawns);                                 // 1000 kills in one minute: perMinute units
        Assert.NotNull(run.SpreadOn(w, kill, (0, 0), T0.AddSeconds(61)));       // the window moved on
        Assert.Equal(6, w.Ledger.PendingSpawns);
        // roamers at max: none
        var capped = Apo.Run(Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perHotspot: 3, max: 6, perMinute: 30))), T0));
        var w2 = new ApoWorld();
        capped.TopUp(w2, T0);
        Assert.Null(capped.SpreadOn(w2, kill, (0, 0), T0));
        Assert.Equal(6, w2.Ledger.PendingSpawns);
        Assert.Equal(0, new SpreadGate().Allow(T0, new ApocalypseRoamers(3, 2, 10, 30, 40), 9, 0));     // tracked full: none
        Assert.Equal(1, new SpreadGate().Allow(T0, new ApocalypseRoamers(3, 2, 10, 30, 40), 9, 50));    // one under max
    }

    [Fact]
    public void Spread_fails_when_a_chain_escapes_a_cap()
    {
        var a = Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perKill: 2, max: 10, perMinute: 6))), T0);
        var w = new ApoWorld();
        var run = Apo.Run(a);
        var at = T0;
        // each spread roamer kills at once and spreads in turn
        for (var i = 0; i < 50; i++)
        {
            at = at.AddSeconds(2);
            foreach (var u in w.SpawnAll(run, at)) run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", u.Wave, u.SpawnedUtc)), (u.Key, 0), at);
            if (i == 0) run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, T0)), (0, 0), at);
            Assert.True(run.RoamersPerHotspot(w).Sum() <= 10);
            Assert.True(run.Spread.InWindow(at) <= 6);
        }
    }

    // ---- D5 Hordes

    [Fact]
    public void Hordes_fails_when_a_horde_rises_at_another_time_or_hotspot()
    {
        var h = Apo.Def().Apocalypse!.Hordes;                                    // 3 from 1 min every 5 min
        Assert.Equal([T0.AddMinutes(1), T0.AddMinutes(6), T0.AddMinutes(11)], Enumerable.Range(1, 3).Select(k => HordeSchedule.DueAt(T0, h, k)));
        Assert.Equal([1, 2, 1, 2, 1], Enumerable.Range(1, 5).Select(k => HordeSchedule.HotspotOf(k, 2)));
        Assert.Equal([1, 2, 3, 1], Enumerable.Range(1, 4).Select(k => HordeSchedule.HotspotOf(k, 3)));
        var end = T0.AddHours(1);
        Assert.Null(HordeSchedule.Due(T0, end, h, 0, null, T0.AddSeconds(59)));
        Assert.Equal(1, HordeSchedule.Due(T0, end, h, 0, null, T0.AddMinutes(1)));
        Assert.Null(HordeSchedule.Due(T0, end, h, 1, null, T0.AddMinutes(5)));
        Assert.Equal(2, HordeSchedule.Due(T0, end, h, 1, null, T0.AddMinutes(6)));
        Assert.Null(HordeSchedule.Due(T0, end, h, 3, null, T0.AddMinutes(30)));  // all risen

        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Null(run.Rise(w, T0.AddSeconds(30), out _));
        var rise = run.Rise(w, T0.AddMinutes(1), out var roamerLines)!;
        Assert.Equal((1, 1, 8), (rise.Horde, rise.Hotspot, rise.Queued));
        Assert.Equal(2, roamerLines.Count);                                     // a rise tops the roamers up
        Assert.All(w.Placed.Where(p => p.X == 0 && p.Radius == Apocalypse.RoamerSpawnRadius), p => Assert.Equal(0, p.Z));
        Assert.Equal((2, 1), (run.Rise(w, T0.AddMinutes(6), out _)!.Horde, run.Rise(w, T0.AddMinutes(11), out _)!.Hotspot));
    }

    [Fact]
    public void Hordes_fails_when_a_size_ignores_growth_or_a_clamp()
    {
        var a = Apo.Def().Apocalypse!;
        Assert.Equal([8, 12, 16], Enumerable.Range(1, 3).Select(k => HordeSchedule.Size(a.Hordes, k)));
        var log = new List<string>();
        Assert.Equal(16, HordeSchedule.Count(HordeSchedule.Units(a, 3, 50, 0, 200, new StepRandom(), log)));
        Assert.Empty(log);
        Assert.Equal(10, HordeSchedule.Count(HordeSchedule.Units(a, 3, 10, 0, 200, new StepRandom(), log)));
        Assert.Equal("clamped by MaxUnitsPerWave: 16 -> 10", Assert.Single(log));
        log.Clear();
        Assert.Equal(5, HordeSchedule.Count(HordeSchedule.Units(a, 3, 50, 195, 200, new StepRandom(), log)));
        Assert.Equal("skipped by MaxTrackedUnits: 11 of 16", Assert.Single(log));
        // units drawn by weight: 3 zombies to 1 thug
        var drawn = HordeSchedule.Units(a, 3, 50, 0, 200, new StepRandom(), []).Select(u => u.Unit.Prefab).ToList();
        Assert.True(drawn.Count(p => p == Apo.Zombie) > drawn.Count(p => p == Apo.Thug));
    }

    [Fact]
    public void Hordes_fails_when_a_zero_queued_horde_counts_as_risen()
    {
        var w = new ApoWorld(maxTracked: 0);
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Null(run.Rise(w, T0.AddMinutes(1), out _));
        Assert.Equal(0, run.Risen);
        Assert.Empty(run.Fronts);
        Assert.Equal(T0.AddMinutes(1), run.LastTryUtc);
        Assert.False(run.Cleared(w, 1));                                         // never fought, never defeated
        Assert.Equal(ApocalypseResult.None, run.Outcome(w, _ => [], T0.AddMinutes(2)));
        Assert.Null(HordeSchedule.Due(T0, T0.AddHours(1), run.Action.Hordes, 0, run.LastTryUtc, T0.AddMinutes(1).AddSeconds(59)));
        Assert.Equal(1, HordeSchedule.Due(T0, T0.AddHours(1), run.Action.Hordes, 0, run.LastTryUtc, T0.AddMinutes(2)));
    }

    [Fact]
    public void Hordes_fails_when_a_rise_past_the_end_spawns()
    {
        var h = new ApocalypseHordes(3, 1, 5, 8, 0);
        var end = T0.AddMinutes(11);                                             // horde 3 is due exactly at the end
        Assert.Equal(2, HordeSchedule.Due(T0, end, h, 1, null, T0.AddMinutes(10)));
        Assert.Null(HordeSchedule.Due(T0, end, h, 2, null, T0.AddMinutes(11)));
        Assert.Null(HordeSchedule.Due(T0, end, h, 2, null, T0.AddMinutes(30)));
        Assert.Null(HordeSchedule.Due(T0, T0.AddMinutes(20), h, 1, null, T0.AddMinutes(20)));   // the run's end passed
    }

    // ---- D6 March

    static Front FrontAt(float x = 0) => new(1, x, 0, 500, 0);
    static readonly Func<float, float, bool> Walkable = (_, _) => true;

    [Fact]
    public void March_fails_when_the_front_moves_at_another_speed_or_overshoots()
    {
        var f = FrontAt();
        Assert.Equal(MarchStep.Moved, MarchRule.Step(f, 20, 2.0, 10, [], Walkable, T0, out var line));
        Assert.Null(line);
        Assert.Equal(20f, f.X, 3);
        Assert.Equal(4, f.Percent);
        Assert.Equal(MarchStep.AtGoal, MarchRule.Step(f, 20, 2.0, 10000, [], Walkable, T0, out _));
        Assert.Equal(500f, f.X, 3);                                              // never past the goal
        Assert.Equal(100, f.Percent);
        Assert.Equal(MarchStep.AtGoal, MarchRule.Step(f, 20, 2.0, 1, [], Walkable, T0, out _));
        Assert.Equal(500f, f.X, 3);
    }

    [Fact]
    public void March_fails_when_a_player_holds_it_wrongly()
    {
        var f = FrontAt(100);
        Assert.Equal(MarchStep.HeldByPlayer, MarchRule.Step(f, 20, 2.0, 10, [(150f, 0f)], Walkable, T0, out _));
        Assert.Equal(100f, f.X);
        Assert.Equal(MarchStep.Moved, MarchRule.Step(f, 20, 2.0, 10, [(161f, 0f)], Walkable, T0, out _));    // 61 m: no hold
        Assert.Equal(120f, f.X, 3);
        // within goal.radius + 60 m of the goal nothing holds it: a player at the goal cannot block the loss
        var g = FrontAt(421);                                                    // 79 m from the goal, radius 20 + 60 = 80
        Assert.Equal(MarchStep.Moved, MarchRule.Step(g, 20, 2.0, 1, [(500f, 0f), (425f, 0f)], Walkable, T0, out _));
        var h = FrontAt(419);                                                    // 81 m: held
        Assert.Equal(MarchStep.HeldByPlayer, MarchRule.Step(h, 20, 2.0, 1, [(425f, 0f)], Walkable, T0, out _));
    }

    [Fact]
    public void March_fails_when_the_held_line_repeats_within_one_hold()
    {
        var f = FrontAt();
        var lines = new List<string?>();
        foreach (var walk in new[] { false, false, false, true, false })
        {
            MarchRule.Step(f, 20, 2.0, 1, [], (_, _) => walk, T0, out var line);
            lines.Add(line);
        }
        Assert.Equal(["horde 1 held: no walkable ground ahead", null, null, null, "horde 1 held: no walkable ground ahead"], lines);
        Assert.Equal(2f, f.X, 3);                                               // moved once only
    }

    [Fact]
    public void March_fails_when_steps_repeat_or_stragglers_move_while_watched()
    {
        var f = FrontAt();
        MarchRule.Step(f, 20, 2.0, 70, [], Walkable, T0, out _);               // 140 m: 28 %
        Assert.Equal([25], MarchRule.NewSteps(f));
        Assert.Empty(MarchRule.NewSteps(f));
        MarchRule.Step(f, 20, 2.0, 100, [], Walkable, T0, out _);              // 340 m: 68 %
        Assert.Equal([50], MarchRule.NewSteps(f));
        MarchRule.Step(f, 20, 2.0, 100, [], Walkable, T0, out _);              // at the goal
        Assert.Equal([75], MarchRule.NewSteps(f));                              // 100 % is the loss's own line
        // the keep-up rule: 80 % within 30 m of the front while unobserved
        var g = FrontAt(100);
        Assert.True(MarchRule.IsUnobserved(g, T0));
        var units = new List<(long, float, float)> { (1, 100, 0), (2, 110, 0), (3, 200, 0) };
        Assert.Equal([3L], MarchRule.Stragglers(g, units, [], T0));
        MarchRule.Step(g, 20, 2.0, 0, [(150f, 0f)], Walkable, T0, out _);      // a player 50 m away watches it
        Assert.Empty(MarchRule.Stragglers(g, units, [], T0.AddSeconds(9)));
        Assert.Equal([3L], MarchRule.Stragglers(g, units, [], T0.AddSeconds(10)));
        Assert.True(MarchRule.KeptUp((4, 5)));
        Assert.False(MarchRule.KeptUp((3, 5)));
        Assert.Equal("horde 1 front 20 % units 4/5 within 30 m of the front", MarchRule.KeepUpLine(1, 20, (4, 5)));
        Assert.Equal((2, 3), MarchRule.KeepUp(g, [(100f, 0f), (129f, 0f), (131f, 0f)]));
    }

    // ---- D7 Lines

    [Fact]
    public void Lines_fails_when_a_line_is_long_or_misses_a_name()
    {
        var name = new string('N', 60) + "<b>";                                 // a 64-byte event name with markup
        var region = RegionNames.All.Select(RegionNames.Display).OrderByDescending(r => Encoding.UTF8.GetByteCount(r)).First();
        var goal = new string('G', 32);
        var lines = new[]
        {
            Messages.ApocalypseRise(name, 50, region, goal), Messages.ApocalypseRise(name, 50, null, goal),
            Messages.ApocalypseProgress(name, 75, goal), Messages.ApocalypseDefeat(name, 10, 10),
            Messages.ApocalypseOutcome(name, ApocalypseResult.Lost, goal), Messages.ApocalypseStatus(name, 10, 10, 99, goal, 100, 10080),
        };
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
        Assert.All(lines, l => Assert.DoesNotContain("<", l));
        Assert.Contains(region, lines[0]);
        Assert.Contains("rises in the wilds and marches on", lines[1]);
        Assert.All(lines.Where(l => !l.Contains("destroyed", StringComparison.Ordinal)), l => Assert.Contains(goal, l));
        Assert.Equal("Night: a horde of 8 rises in Farbane Woods and marches on Brighthaven", Messages.ApocalypseRise("Night", 8, "Farbane Woods", "Brighthaven"));
        Assert.Equal("Night: the horde is 25 % of the way to Brighthaven", Messages.ApocalypseProgress("Night", 25, "Brighthaven"));
        Assert.Equal("Night: horde 2 of 3 destroyed", Messages.ApocalypseDefeat("Night", 2, 3));
        Assert.Equal("Night: the living have won", Messages.ApocalypseOutcome("Night", ApocalypseResult.Won, "Brighthaven"));
        Assert.Equal("Night: Brighthaven has fallen", Messages.ApocalypseOutcome("Night", ApocalypseResult.Lost, "Brighthaven"));
        Assert.Equal("Night: the living held out", Messages.ApocalypseOutcome("Night", ApocalypseResult.Survived, "Brighthaven"));
        // a 25 % step is announced once, also after a resume at the recorded front
        var f = Front.At(1, 0, 0, 500, 0, 130, 0);
        var resumed = ApocalypseRun.Resumed(Apo.Active(Apo.Def(), T0), Record(fronts: [(1, 130f, 0f, 4, [])], risen: 1), T0.AddMinutes(2));
        Assert.Empty(MarchRule.NewSteps(resumed.Fronts[1]));
        Assert.Equal([25], MarchRule.NewSteps(f));
    }

    // ---- D8 Outcomes

    static HordeState State(int horde, float frontX, params float[] unitsX) =>
        new(horde, Front.At(horde, 0, 0, 500, 0, frontX, 0), unitsX.Select(x => (x, 0f)).ToList());

    [Fact]
    public void Outcomes_fails_when_won_or_lost_is_read_wrongly()
    {
        var a = Apo.Def().Apocalypse!;                                           // 3 hordes, goal (500, 0) radius 20
        var goal = (500f, 0f);
        var end = T0.AddHours(1);
        Assert.Equal(ApocalypseResult.None, ApocalypseOutcome.Decide(a, goal, [], 2, _ => true, T0, end));           // a horde unrisen
        Assert.Equal(ApocalypseResult.None, ApocalypseOutcome.Decide(a, goal, [State(3, 100, 100)], 3, k => k != 3, T0, end));   // one alive
        Assert.Equal(ApocalypseResult.Won, ApocalypseOutcome.Decide(a, goal, [], 3, _ => true, T0, end));
        Assert.Equal(ApocalypseResult.Lost, ApocalypseOutcome.Decide(a, goal, [State(1, 485, 455)], 1, _ => false, T0, end));   // 45 m < 20 + 30
        Assert.Equal(ApocalypseResult.None, ApocalypseOutcome.Decide(a, goal, [State(1, 485, 440)], 1, _ => false, T0, end));   // no unit at the goal
        Assert.Equal(ApocalypseResult.None, ApocalypseOutcome.Decide(a, goal, [State(1, 470, 495)], 1, _ => false, T0, end));   // front not in
        // lost beats won on one tick, won beats survived
        Assert.Equal(ApocalypseResult.Lost, ApocalypseOutcome.Decide(a, goal, [State(3, 500, 500)], 3, _ => true, end, end));
        Assert.Equal(ApocalypseResult.Won, ApocalypseOutcome.Decide(a, goal, [], 3, _ => true, end, end));
        Assert.Equal(ApocalypseResult.Survived, ApocalypseOutcome.Decide(a, goal, [State(2, 100, 100)], 2, _ => false, end, end));
        Assert.Equal("event apo ended: won (3 of 3 hordes risen)", ApocalypseOutcome.Line("apo", ApocalypseResult.Won, 3, 3));
        Assert.Equal("event apo ended: lost (1 of 3 hordes risen)", ApocalypseOutcome.Line("apo", ApocalypseResult.Lost, 1, 3));
        Assert.Equal("event apo ended: survived (2 of 3 hordes risen)", ApocalypseOutcome.Line("apo", ApocalypseResult.Survived, 2, 3));
    }

    [Fact]
    public void Outcomes_fails_when_a_run_reads_won_while_a_horde_lives()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 2))), T0));
        run.Rise(w, T0.AddMinutes(1), out _);
        w.SpawnAll(run, T0.AddMinutes(1));
        foreach (var u in w.OfWave(run, 1)) w.Kill(run, u.Key);
        Assert.True(run.Cleared(w, 1));
        Assert.Equal(ApocalypseResult.None, run.Outcome(w, _ => [], T0.AddMinutes(2)));       // horde 2 unrisen
        run.Rise(w, T0.AddMinutes(6), out _);
        Assert.Equal(ApocalypseResult.None, run.Outcome(w, _ => [], T0.AddMinutes(6)));       // its orders wait
        w.SpawnAll(run, T0.AddMinutes(6));
        var last = w.OfWave(run, 2);
        foreach (var u in last.Skip(1)) w.Kill(run, u.Key);
        Assert.Equal(ApocalypseResult.None, run.Outcome(w, _ => [(0f, 0f)], T0.AddMinutes(7)));   // one alive
        w.Kill(run, last[0].Key);
        Assert.Equal(ApocalypseResult.Won, run.Outcome(w, _ => [], T0.AddMinutes(7)));
    }

    internal sealed class Pushes : IPushSink
    {
        public readonly List<string> Types = [];
        public void EventStarted(RunningInstance instance) => Types.Add(PushLines.EventStartType);
        public void EventEnded(string id, string region = "-") => Types.Add(PushLines.EventEndType);
        public void Wave(string id, int wave) => Types.Add(PushLines.WaveType);
        public void Purged(int cooldownSeconds) => Types.Add(PushLines.KillswitchType);
        public void ConfigChanged() => Types.Add(PushLines.ConfigChangedType);
    }

    internal static EventEngine Engine(out Pushes push, params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(T0, [1])));
        push = new Pushes();
        return new EventEngine(catalog) { Push = push };
    }

    [Fact]
    public void Outcomes_fails_when_an_outcome_sends_other_than_one_event_end()
    {
        var engine = Engine(out var push, Apo.Event());
        Assert.Null(engine.Start("apo", "manual", T0, Open()));
        var ended = engine.Complete("apo", T0.AddMinutes(20), 60)!;
        Assert.Equal("apo", ended.Id);
        Assert.Null(engine.Complete("apo", T0.AddMinutes(20), 60));
        Assert.Equal([PushLines.EventStartType, PushLines.EventEndType], push.Types);
        // the grace cleanup takes every unit of the run, whatever its wave
        var cleanup = Assert.Single(engine.PendingCleanups);
        Assert.Equal(("apo", DateTime.MaxValue, T0.AddMinutes(21)), (cleanup.EventId, cleanup.SpawnedBefore, cleanup.DueUtc));
        // survived: the natural end, one push and the same cleanup
        var e2 = Engine(out var push2, Apo.Event());
        Assert.Null(e2.Start("apo", "manual", T0, Open()));
        Assert.Single(e2.Expire(T0.AddHours(1), 60));
        Assert.Empty(e2.Expire(T0.AddHours(1).AddSeconds(1), 60));
        Assert.Equal([PushLines.EventStartType, PushLines.EventEndType], push2.Types);
        Assert.Single(e2.PendingCleanups);
        // the ledger's end takes roamers, horde units and bosses alike
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1), extra: "\"bosses\": [ " + Apo.BossJson() + " ]")), T0));
        run.TopUp(w, T0);
        run.Rise(w, T0.AddMinutes(1), out _);
        var all = w.SpawnAll(run, T0.AddMinutes(1));
        Assert.Contains(all, u => u.Wave == Apocalypse.BossWave(1, 1));
        Assert.Equal((all.Count, 0), w.Ledger.EndEvent("apo", cleanup.SpawnedBefore));
    }

    // ---- D9 EndPaths

    [Fact]
    public void EndPaths_fails_when_a_path_keeps_the_record_or_shows_the_wrong_lines()
    {
        foreach (var path in Enum.GetValues<ApocalypseEnd>())
        {
            var ends = path is not (ApocalypseEnd.Restart or ApocalypseEnd.Disable);
            Assert.Equal(ends, EndRule.EndsRun(path));
            Assert.Equal(ends, EndRule.RemovesRecord(path));
            Assert.Equal(path is ApocalypseEnd.Won or ApocalypseEnd.Lost or ApocalypseEnd.Survived or ApocalypseEnd.Stop, EndRule.ShowsScoreboard(path));
            Assert.Equal(path is ApocalypseEnd.Won or ApocalypseEnd.Lost or ApocalypseEnd.Survived, EndRule.ShowsOutcome(path));
        }
        // the scoreboard rule agrees on every path that ends the run
        var a = Apo.Active(Apo.Def(), T0);
        foreach (var path in Enum.GetValues<ApocalypseEnd>().Where(EndRule.EndsRun))
            Assert.Equal(EndRule.ShowsScoreboard(path), ScoreboardRule.Shows(EndRule.PathOf(path), a));
        Assert.False(ScoreboardRule.Shows(EndPath.Restart, a));
        Assert.Equal(EndPath.Defeat, ApocalypseOutcome.PathOf(ApocalypseResult.Lost));
        Assert.Equal(ApocalypseEnd.Lost, EndRule.Of(ApocalypseResult.Lost));
        Assert.Equal(ApocalypseEnd.Survived, EndRule.Of(ApocalypseResult.Survived));
        var board = new Scoreboard();
        board.Kill("apo", new Scorer("p1", "Chaos", false), false);
        Assert.NotEmpty(ScoreboardRule.End(EndPath.Stop, a, board).Chat);
        Assert.Empty(ScoreboardRule.End(EndPath.Purge, a, board).Chat);
    }

    [Fact]
    public void EndPaths_fails_when_a_drain_wait_purge_rebuilds_or_disable_resumes()
    {
        Assert.Equal(DrainStep.Cancelled, ResumeRule.Drain(T0, T0.AddMinutes(1), 12, cancelled: true));
        Assert.Equal(DrainStep.Cancelled, ResumeRule.Drain(T0, T0.AddMinutes(9), 0, cancelled: true));
        // a disabled definition's record is not resumed after a restart, but disabling never ends the live run
        var record = Record();
        Assert.False(EndRule.EndsRun(ApocalypseEnd.Disable));
        var disabled = Apo.Set(Apo.Event()).All.Select(d => d with { Enabled = false }).ToList();
        Assert.Equal(ResumeVerdict.Disabled, ResumeRule.Verdict(record, new DefinitionSet(disabled), T0.AddMinutes(5)));
        // a restart cancels every other kind and keeps an apocalypse
        Assert.True(EndRule.Resumes(Apo.Def()));
        Assert.False(EndRule.Resumes(Json.One(Json.Event())));
        Assert.False(EndRule.Resumes(Json.One(Json.Empower())));
    }

    // ---- D12 Resume

    static ApocalypseRecord Record(string id = "apo", int risen = 1, IReadOnlyList<(int Horde, float X, float Z, int Living, (string Prefab, int Living)[] Bosses)>? fronts = null,
        int[]? roamers = null, DateTime? started = null, DateTime? ends = null, (float X, float Y, float Z)? goal = null)
    {
        var r = new ApocalypseRecord
        {
            EventId = id,
            StartedUtc = started ?? T0,
            EndsUtc = ends ?? (started ?? T0).AddHours(1),
            Goal = new RecordPoint { X = goal?.X ?? 500, Y = goal?.Y ?? 12, Z = goal?.Z ?? 0 },
            Hotspots = [new RecordSpot { X = 0, Z = 0 }, new RecordSpot { X = 1000, Z = 0 }],
            HordesRisen = risen,
            Roamers = (roamers ?? [3, 2]).ToList(),
            SpreadWindow = 2,
            Scores = [new RecordScore { PlatformId = "76561190000000001", Kills = 4, Deaths = 1, First = 1 }],
        };
        foreach (var (horde, x, z, living, bosses) in fronts ?? [(1, 120f, 0f, 6, [])])
            r.Hordes.Add(new RecordHorde { Number = horde, FrontX = x, FrontZ = z, Living = living, LivingBosses = bosses.Select(b => new RecordBoss { Prefab = b.Prefab, Living = b.Living }).ToList() });
        Assert.Null(r.Problem());
        return r;
    }

    [Fact]
    public void Resume_fails_when_a_changed_or_gone_definition_resumes()
    {
        var record = Record(risen: 2, fronts: [(2, 100f, 0f, 3, [])]);
        var now = T0.AddMinutes(10);
        Assert.Equal(ResumeVerdict.Resume, ResumeRule.Verdict(record, Apo.Set(Apo.Event()), now));
        Assert.Equal(ResumeVerdict.Gone, ResumeRule.Verdict(record, Apo.Set(Apo.Event("other")), now));
        Assert.Equal(ResumeVerdict.Changed, ResumeRule.Verdict(record, Apo.Set(Json.Event("apo")), now));                                       // not an apocalypse
        Assert.Equal(ResumeVerdict.Changed, ResumeRule.Verdict(record, Apo.Set(Apo.Event(action: Apo.Action(hordes: Apo.Hordes(count: 1)))), now));   // fewer hordes
        Assert.Equal(ResumeVerdict.EndedWhileDown, ResumeRule.Verdict(record, Apo.Set(Apo.Event()), T0.AddHours(1)));
        // Review 7 F2: a living boss whose prefab the definition no longer lists is a change
        var boss = Record(risen: 3, fronts: [(3, 100f, 0f, 3, [(Apo.Tourok, 1)])]);
        Assert.Equal(ResumeVerdict.Resume, ResumeRule.Verdict(boss, Apo.Set(Apo.Event(action: Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson() + " ]"))), now));
        Assert.Equal(ResumeVerdict.Changed, ResumeRule.Verdict(boss, Apo.Set(Apo.Event(action: Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(Apo.Deadeye) + " ]"))), now));
        Assert.Equal(ResumeVerdict.Changed, ResumeRule.Verdict(boss, Apo.Set(Apo.Event()), now));
        Assert.Equal("apocalypse apo not resumed: changed", ResumeRule.NotResumedLine("apo", ResumeVerdict.Changed));
        Assert.Equal(["definition gone", "disabled", "changed", "ended while down"],
            new[] { ResumeVerdict.Gone, ResumeVerdict.Disabled, ResumeVerdict.Changed, ResumeVerdict.EndedWhileDown }.Select(ResumeRule.Reason));
        // a past EndsUtc ends the run as survived in the log; nothing is rebuilt
        Assert.Equal("event apo ended: survived (2 of 3 hordes risen)", ApocalypseOutcome.Line("apo", ApocalypseResult.Survived, record.HordesRisen, 3));
    }

    [Fact]
    public void Resume_fails_when_an_admin_goal_run_loses_its_goal()
    {
        var admin = Apo.Event(action: Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }")));
        var engine = Engine(out var push, admin);
        var record = Record(goal: (-321.5f, 7f, 88f));
        engine.ResumePending = "apo";
        Assert.Null(engine.Resume("apo", record.StartedUtc, record.EndsUtc, (record.Goal!.X, record.Goal.Y, record.Goal.Z), record.HordesRisen));
        var active = engine.Find("apo")!;
        Assert.Equal((-321.5f, 7f, 88f), active.Origin);
        Assert.Equal((T0, T0.AddHours(1)), (active.Instance.StartedUtc, active.Instance.EndsUtc));
        Assert.Equal(1, active.WavesSpawned);
        Assert.Null(engine.ResumePending);
        Assert.Empty(push.Types);                                                // the run never ended: no start push
        var run = ApocalypseRun.Resumed(active, record, T0.AddMinutes(10));
        Assert.Equal((-321.5f, 7f, 88f), run.Goal);
        Assert.Equal((-321.5f, 88f), (run.Fronts[1].GoalX, run.Fronts[1].GoalZ));
        Assert.Equal(120f, run.Fronts[1].X);
        // an Admin goal resolves from the starting admin at the start only
        var goal = Apo.Def(Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }"))).Apocalypse!.Goal;
        Assert.Null(Apocalypse.ResolveGoal(goal, null));
        Assert.Equal((1f, 2f, 3f), Apocalypse.ResolveGoal(goal, (1, 2, 3)));
        Assert.Equal((500f, 12f, 0f), Apocalypse.ResolveGoal(Apo.Def().Apocalypse!.Goal, (1, 2, 3)));
    }

    [Fact]
    public void Resume_fails_when_the_rebuild_skips_the_drain_gate()
    {
        Assert.Equal(DrainStep.Wait, ResumeRule.Drain(T0, T0.AddMinutes(4).AddSeconds(59), 3, false));
        Assert.Equal(DrainStep.Proceed, ResumeRule.Drain(T0, T0.AddSeconds(5), 0, false));
        Assert.Equal(DrainStep.ProceedWithWarning, ResumeRule.Drain(T0, T0.AddMinutes(5), 3, false));
        Assert.Equal("apocalypse apo: boot sweep not drained after 5 min (3 left); resuming", ResumeRule.DrainWarning("apo", 3));
        Assert.Equal("event apo resumed after restart (horde 2/3, 14 units to rebuild)", ResumeRule.ResumedLine("apo", 2, 3, 14));
    }

    [Fact]
    public void Resume_fails_when_missed_hordes_or_the_top_up_go_wrong()
    {
        var def = Apo.Def();                                                    // hordes at +1, +6, +11 min
        var record = Record(risen: 1, fronts: [(1, 120f, 0f, 6, [])], roamers: [3, 2]);
        var now = T0.AddMinutes(12);
        Assert.Equal([2, 3], ResumeRule.CatchUp(record, def.Apocalypse!, now));
        Assert.Equal([2], ResumeRule.CatchUp(record, def.Apocalypse!, T0.AddMinutes(7)));
        var w = new ApoWorld();
        var run = ApocalypseRun.Resumed(Apo.Active(def, T0), record, now);
        Assert.Equal(0, run.TopUps);
        Assert.Equal(0, w.Ledger.PendingSpawns);                                 // nothing before the gate proceeds
        var (rebuilt, rises, topUp) = run.Rebuild(w, record, now);
        Assert.Equal(11, rebuilt);                                               // 3 + 2 roamers + 6 of horde 1, no more
        Assert.Equal([2, 3], rises.Select(r => r.Horde));
        Assert.Equal(3, run.Risen);
        Assert.Equal(1, run.TopUps);                                             // once, after the rebuild
        Assert.Equal(["apocalypse apo roamers: +1 at hotspot 2"], topUp);
        Assert.Null(run.Rise(w, now, out _));                                    // each rises once
        var units = w.SpawnAll(run, now);
        Assert.Equal(new[] { (1, 6), (2, 12), (3, 16), (101, 3), (102, 3) }, units.GroupBy(u => u.Wave).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())));
        // each hotspot's roamers, spread ones included, rebuild at that hotspot; each horde at its front
        var orders = ResumeRule.Rebuild(record, def.Apocalypse!);
        Assert.Equal([(101, 0f, 3), (102, 1000f, 2), (1, 120f, 6)], orders.Select(o => (o.Wave, o.X, o.Count)));
        Assert.Equal(record.Roamers.Sum() + record.Hordes.Sum(h => h.Living), orders.Sum(o => o.Count));
        // the scoreboard comes back as it was saved
        var board = new Scoreboard();
        board.Restore("apo", record.Scores.Select(s => new ApocalypseScore(s.PlatformId, s.Kills, s.Deaths, s.First)));
        Assert.Equal([new ApocalypseScore("76561190000000001", 4, 1, 1)], board.Export("apo"));
        Assert.Equal(2, run.Spread.InWindow(now));
    }

    [Fact]
    public void Resume_fails_when_a_pending_resume_lets_a_second_run_start()
    {
        var engine = Engine(out _, Apo.Event(), Apo.Event("apo2", "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }"));
        engine.ResumePending = "apo";
        Assert.Equal(AdminLines.AlreadyActive("apo").Human, engine.Start("apo", "schedule", T0, Open())!.Human);
        Assert.Equal("an apocalypse is already running: apo", engine.Start("apo2", "interval", T0, Open())!.Human);
        Assert.Empty(engine.Active);
        Assert.Null(engine.Resume("apo", T0, T0.AddHours(1), (500, 12, 0)));
        Assert.Equal("an apocalypse is already running: apo", engine.Start("apo2", "interval", T0, Open())!.Human);
    }

    [Fact]
    public void Resume_fails_when_a_purge_in_the_drain_wait_keeps_the_slot()
    {
        var engine = Engine(out _, Apo.Event(), Apo.Event("apo2", "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }"));
        engine.ResumePending = "apo";
        engine.CancelAll(240);
        Assert.Null(engine.ResumePending);
        Assert.Null(engine.Start("apo2", "interval", T0, Open()));
    }

    [Fact]
    public void Resume_fails_when_a_pillar_or_master_switch_turned_off_while_down_is_ignored()
    {
        var engine = Engine(out _, Apo.Event());
        engine.ResumePending = "apo";
        var noSpawns = new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>().Where(p => p != Pillar.Spawns)), 0, 3);
        Assert.Equal("pillar spawns is off", engine.Resume("apo", T0, T0.AddHours(1), (500, 12, 0), 0, noSpawns)!.Human);
        Assert.Null(engine.ResumePending);
        Assert.Empty(engine.Active);
        var masterOff = Open() with { GeneralEnabled = false };
        Assert.Equal("General.Enabled is false", engine.Resume("apo", T0, T0.AddHours(1), (500, 12, 0), 0, masterOff)!.Human);
        Assert.Empty(engine.Active);
        // a purge cooldown or a full MaxConcurrentEvents never refuses a run that never ended
        Assert.Null(engine.Resume("apo", T0, T0.AddHours(1), (500, 12, 0), 0, Open(active: 3, max: 3) with { PurgeCooldownActive = true }));
        Assert.Single(engine.Active);
    }

    // ---- D13 Lifetime

    [Fact]
    public void Lifetime_fails_when_a_unit_lives_past_two_hours_or_is_missed()
    {
        var ends = T0.AddDays(7);
        var life = RollingLifetime.Of(T0, ends, 60, 120);
        Assert.Equal(T0.AddSeconds(7200), life.DueUtc);
        Assert.Equal(7200 + 120, life.LifetimeSeconds);
        var shortRun = RollingLifetime.Of(T0, T0.AddMinutes(30), 60, 120);
        Assert.Equal(T0.AddMinutes(31), shortRun.DueUtc);                       // the run's end + grace comes first
        // every queued unit of a run carries at most 2 h plus the margin
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(duration: 604800, action: Apo.Action(hordes: Apo.Hordes(count: 1))), T0));
        run.TopUp(w, T0);
        run.Rise(w, T0.AddMinutes(1), out _);
        Assert.All(w.Ledger.TakeSpawns(), o => Assert.True(o.LifetimeSeconds <= Limits.ApocalypseUnitLifetimeSeconds + w.DrainMarginSeconds));
        // renewed when less than 90 min is left, never for an ended run, never needlessly
        var unit = new TrackedUnit(1, Apo.Zombie, "apo", T0, life.LifetimeSeconds, life.DueUtc);
        Assert.Null(RollingLifetime.Renew(unit, true, T0.AddMinutes(29), ends, 60, 120));
        Assert.Equal(T0.AddMinutes(31).AddHours(2), RollingLifetime.Renew(unit, true, T0.AddMinutes(31), ends, 60, 120)!.Value.DueUtc);
        Assert.Null(RollingLifetime.Renew(unit, false, T0.AddMinutes(31), ends, 60, 120));
        // a run of days: checked every 10 min, the unit never comes due
        var u = unit;
        for (var at = T0; at < T0.AddDays(3); at = at.AddMinutes(10))
        {
            Assert.True(u.DueUtc > at, $"expired at {at:O}");
            if (RollingLifetime.Renew(u, true, at, ends, 60, 120) is { } next) u = u with { DueUtc = next.DueUtc, LifetimeSeconds = next.LifetimeSeconds };
            Assert.True(u.DueUtc <= at.AddSeconds(7200));
        }
        Assert.Equal("apocalypse apo renewed 12 lifetimes", RollingLifetime.Line("apo", 12));
    }

    // ---- D14 Caps

    [Fact]
    public void Caps_fails_when_a_unit_escapes_the_ledger_or_a_cap()
    {
        var w = new ApoWorld(maxTracked: 18, maxPerWave: 10);
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perHotspot: 5, max: 10))), T0));
        run.TopUp(w, T0);
        var rise = run.Rise(w, T0.AddMinutes(1), out _)!;
        Assert.Equal(8, rise.Queued);                                            // 8 asked, within MaxUnitsPerWave 10
        Assert.True(w.Ledger.Occupied <= 18);
        Assert.Equal(run.Tally.Queued, w.Ledger.Occupied);                       // every unit of the run is a ledger order
        w.SpawnAll(run, T0.AddMinutes(1));
        Assert.Null(run.Rise(w, T0.AddMinutes(6), out _));                       // the ledger is full: horde 2 waits
        Assert.Equal(18, w.Ledger.Tracked);
        var big = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(size: 50, growth: 20))), T0));
        var w2 = new ApoWorld(maxTracked: 500, maxPerWave: 30);
        Assert.Equal(30, big.Rise(w2, T0.AddMinutes(1), out _)!.Queued);         // a horde never passes MaxUnitsPerWave
        Assert.Equal(30 + 6, w2.Ledger.PendingSpawns);                           // and its rise tops up 3 roamers at each hotspot
        // one apocalypse at a time; it counts as a concurrent event; a second start of the same id gets 0.8.0's reply
        var engine = Engine(out _, Apo.Event(), Apo.Event("apo2"), Json.Event("raid"));
        Assert.Null(engine.Start("apo", "manual", T0, Open()));
        Assert.Equal(AdminLines.AlreadyActive("apo").Human, engine.Start("apo", "manual", T0, Open(1))!.Human);
        Assert.Equal(EventActions.ApocalypseRunning("apo"), engine.Start("apo2", "manual", T0, Open(1))!.Human);
        Assert.NotNull(engine.Start("raid", "manual", T0, Open(active: engine.Active.Count, max: 1)));
        Assert.Null(engine.Start("raid", "manual", T0, Open(active: engine.Active.Count, max: 2)));
    }

    // ---- D15 Tally

    [Fact]
    public void Tally_fails_when_a_unit_is_unaccounted()
    {
        var def = Apo.Def();
        var w = new ApoWorld(maxTracked: 60);
        var run = Apo.Run(Apo.Active(def, T0));
        void Check(ApocalypseRun r) => Assert.Equal(0, r.Tally.Unaccounted(w.Alive(r)));
        run.TopUp(w, T0);
        Check(run);
        run.Rise(w, T0.AddMinutes(1), out _);
        Check(run);
        var units = w.SpawnAll(run, T0.AddMinutes(1));
        Check(run);
        w.Kill(run, units[0].Key);
        w.Despawn(run, units[1].Key);
        w.Remove(run, units[2].Key);
        Check(run);
        run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, T0)), (5, 5), T0.AddMinutes(2));
        Assert.Equal(2, w.FailAll(run));                                          // the spread's spawns fail
        Check(run);
        run.Rise(w, T0.AddMinutes(6), out _);
        var (_, cancelled) = w.Ledger.EndEvent("apo", DateTime.MaxValue);       // a cancelled order is a failed one
        run.FailedToSpawn(2, cancelled);
        Check(run);
        Assert.Equal($"apocalypse apo units: {run.Tally.Queued} queued = 0 pending + {w.Alive(run)} alive + 1 died + 1 despawned + 1 removed + {2 + cancelled} failed",
            run.Tally.Line("apo", w.Alive(run)));
        // per boot (Review 3 F3): a resume starts a new tally over the rebuilt units
        var record = Record(risen: 2, fronts: [(2, 100f, 0f, 4, [])], roamers: [1, 1]);
        var resumed = ApocalypseRun.Resumed(Apo.Active(def, T0), record, T0.AddMinutes(8));
        var w2 = new ApoWorld();
        resumed.Rebuild(w2, record, T0.AddMinutes(8));
        Assert.Equal(0, resumed.Tally.Unaccounted(w2.Ledger.Units.Count));
        foreach (var u in w2.SpawnAll(resumed, T0.AddMinutes(8)).Take(3)) w2.Kill(resumed, u.Key);
        Assert.Equal(0, resumed.Tally.Unaccounted(w2.Alive(resumed)));
        Assert.NotEqual(0, new ApocalypseTally { }.Unaccounted(1));             // a unit no bucket holds is caught
    }

    // ---- D36 Bosses

    [Fact]
    public void Bosses_fails_when_a_boss_rises_early_or_a_clamp_drops_it_first()
    {
        var a = Apo.Def(Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(count: "2", more: "\"modifiers\": { \"level\": 70 }") + ", " + Apo.BossJson(Apo.Deadeye) + " ]")).Apocalypse!;
        Assert.DoesNotContain(HordeSchedule.Units(a, 1, 50, 0, 200, new StepRandom(), []), u => u.Entry != 0);
        Assert.DoesNotContain(HordeSchedule.Units(a, 2, 50, 0, 200, new StepRandom(), []), u => u.Entry != 0);
        var last = HordeSchedule.Units(a, 3, 50, 0, 200, new StepRandom(), []);
        Assert.Equal([(Apo.Tourok, 2, 1), (Apo.Deadeye, 1, 2)], last.Where(u => u.Entry != 0).Select(u => (u.Unit.Prefab, u.Unit.Count, u.Entry)));
        Assert.Equal(70, last[0].Unit.Modifiers!.Level);
        Assert.Equal(16 + 3, HordeSchedule.Count(last));
        // a clamp cuts the regular units first
        var clamped = HordeSchedule.Units(a, 3, 4, 0, 200, new StepRandom(), []);
        Assert.Equal([1, 2, 0], clamped.Select(u => u.Entry).Distinct());
        Assert.Equal(4, HordeSchedule.Count(clamped));
        Assert.Equal(3, clamped.Where(u => u.Entry != 0).Sum(u => u.Unit.Count));
        Assert.Equal(2, HordeSchedule.Count(HordeSchedule.Units(a, 3, 2, 0, 200, new StepRandom(), [])));
        Assert.All(HordeSchedule.Units(a, 3, 2, 0, 200, new StepRandom(), []), u => Assert.Equal(1, u.Entry));
        // the wave codec keeps a boss in its horde
        Assert.Equal(232, Apocalypse.BossWave(3, 2));
        Assert.Equal((3, 2), (Apocalypse.HordeOfWave(232)!.Value, Apocalypse.BossEntryOfWave(232)!.Value));
        Assert.Equal((3, (int?)null), (Apocalypse.HordeOfWave(3)!.Value, Apocalypse.BossEntryOfWave(3)));
        Assert.Equal(2, Apocalypse.HotspotOfWave(102));
        Assert.Null(Apocalypse.HordeOfWave(102));
    }

    [Fact]
    public void Bosses_fails_when_a_run_ends_won_while_a_boss_lives_or_a_resume_misses_one()
    {
        var def = Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1), extra: "\"bosses\": [ " + Apo.BossJson(more: "\"modifiers\": { \"level\": 70 }") + ", " + Apo.BossJson(Apo.Deadeye) + " ]"));
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(def, T0));
        run.Rise(w, T0.AddMinutes(1), out _);
        var units = w.SpawnAll(run, T0.AddMinutes(1));
        foreach (var u in units.Where(u => u.Wave == 1 || u.Wave == Apocalypse.BossWave(1, 1))) w.Kill(run, u.Key);
        Assert.False(run.Cleared(w, 1));                                         // the Deadeye boss lives
        Assert.Equal(ApocalypseResult.None, run.Outcome(w, _ => [(0f, 0f)], T0.AddMinutes(2)));
        // the record holds one living count per entry, the dead one at 0
        var record = run.Record(w, [], T0.AddMinutes(2));
        var horde = Assert.Single(record.Hordes);
        Assert.Equal([(Apo.Tourok, 0), (Apo.Deadeye, 1)], horde.LivingBosses.Select(b => (b.Prefab, b.Living)));
        Assert.Equal(1, horde.Living);
        // the resume rebuilds the living boss as its prefab with its entry's modifiers, never the dead one
        var orders = ResumeRule.Rebuild(record, def.Apocalypse!);
        var boss = Assert.Single(orders, o => o.Prefab is not null);
        Assert.Equal((Apocalypse.BossWave(1, 2), Apo.Deadeye, 1), (boss.Wave, boss.Prefab, boss.Count));
        var two = Record(risen: 1, fronts: [(1, 50f, 0f, 5, [(Apo.Tourok, 1), (Apo.Deadeye, 0)])]);
        var rebuilt = ResumeRule.Rebuild(two, def.Apocalypse!);
        Assert.Equal([(Apocalypse.BossWave(1, 1), Apo.Tourok, 1), (1, null, 4)], rebuilt.Where(o => o.Wave < 100 || o.Wave > 200).Select(o => (o.Wave, o.Prefab, o.Count)));
        Assert.Equal(70, rebuilt.First(o => o.Prefab == Apo.Tourok).Modifiers!.Level);
        // killing it clears the horde: won
        w.Kill(run, units.Single(u => u.Wave == Apocalypse.BossWave(1, 2)).Key);
        Assert.Equal(ApocalypseResult.Won, run.Outcome(w, _ => [], T0.AddMinutes(3)));
    }

    [Fact]
    public void Bosses_fails_when_a_grown_hordes_count_drops_a_living_boss_from_the_record()
    {
        // 3 hordes risen, the last one's boss alive; the admin raised hordes.count to 4 while the server was down
        var def = Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 4), extra: "\"bosses\": [ " + Apo.BossJson() + " ]"));
        var record = Record(risen: 3, fronts: [(3, 100f, 0f, 3, [(Apo.Tourok, 1)])], roamers: [0, 0]);
        var now = T0.AddMinutes(12);                                             // horde 4 is due at +16
        var w = new ApoWorld();
        var run = ApocalypseRun.Resumed(Apo.Active(def, T0), record, now);
        run.Rebuild(w, record, now);
        w.SpawnAll(run, now);
        var again = run.Record(w, [], now);
        var horde = Assert.Single(again.Hordes);
        Assert.Equal((3, 3), (horde.Number, horde.Living));
        Assert.Equal([(Apo.Tourok, 1)], horde.LivingBosses.Select(b => (b.Prefab, b.Living)));
    }

    [Fact]
    public void Bosses_fails_when_two_entries_of_one_prefab_rebuild_with_the_wrong_modifiers()
    {
        var def = Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1), extra: "\"bosses\": [ " + Apo.BossJson(more: "\"modifiers\": { \"level\": 70 }")
            + ", " + Apo.BossJson(more: "\"modifiers\": { \"level\": 40 }") + " ]"));
        var record = Record(risen: 1, fronts: [(1, 50f, 0f, 1, [(Apo.Tourok, 0), (Apo.Tourok, 1)])]);
        var boss = Assert.Single(ResumeRule.Rebuild(record, def.Apocalypse!), o => o.Prefab is not null);
        Assert.Equal((Apocalypse.BossWave(1, 2), 40), (boss.Wave, boss.Modifiers!.Level));
    }

    [Fact]
    public void Resume_fails_when_a_record_without_its_goal_key_resumes_to_the_origin()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Record());
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        Assert.True(node.Remove("Goal") || node.Remove("goal"));
        var back = System.Text.Json.JsonSerializer.Deserialize<ApocalypseRecord>(node.ToJsonString())!;
        Assert.Equal("goal missing", back.Problem());
    }

    // ---- the good and empty case of each control

    [Fact]
    public void Hotspots_passes_one_cluster_one_hotspot()
    {
        var pick = Hotspots.Pick("apo", Apo.Def().Apocalypse!, () => Cluster(10, 20, 3).ToList());
        var spot = Assert.Single(pick.Spots);
        Assert.Equal((1, 11f, 21f, 3), (spot.Number, spot.X, spot.Z, spot.Natives));
    }

    [Fact]
    public void Roamers_passes_top_up_and_hunt_the_nearest()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Equal(2, run.TopUp(w, T0).Count);
        Assert.Equal([3, 3], run.RoamersPerHotspot(w));
        Assert.Equal(2, RoamRule.Target([Player(1, 900), Player(2, 100)], (0, 0))!.Value.Key);
    }

    [Fact]
    public void Roamers_empty_no_player_they_stay()
    {
        Assert.Null(RoamRule.Target([], (0, 0)));
        Assert.Empty(RoamRule.Seeds([], (0, 0)));
        Assert.Null(new RoamTargets().Seen("apo", 1, null));
    }

    [Fact]
    public void Spread_passes_a_roamer_kill_spreads_at_the_victim()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.NotNull(run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 2, T0.AddMinutes(1))), (40, 0), T0));
        Assert.Equal(2, w.Ledger.PendingSpawns);
        Assert.All(w.Placed, p => Assert.Equal((40f, 0f), (p.X, p.Z)));
    }

    [Fact]
    public void Spread_empty_perKill_zero_spreads_nothing()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perKill: 0))), T0));
        Assert.Null(run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, T0)), (0, 0), T0));
        Assert.Equal(0, w.Ledger.PendingSpawns);
    }

    [Fact]
    public void Hordes_passes_three_hordes_on_time()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        var sizes = new[] { 1, 6, 11 }.Select(m => run.Rise(w, T0.AddMinutes(m), out _)!.Queued).ToList();
        Assert.Equal([8, 12, 16], sizes);
        Assert.Equal(3, run.Risen);
    }

    [Fact]
    public void Hordes_empty_no_horde_before_its_time()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Null(run.Rise(w, T0, out var lines));
        Assert.Empty(lines);
        Assert.Equal(0, w.Ledger.PendingSpawns);
    }

    [Fact]
    public void March_passes_the_front_reaches_the_goal()
    {
        var f = FrontAt();
        var steps = 0;
        while (MarchRule.Step(f, 20, 2.0, 10, [], Walkable, T0, out _) != MarchStep.AtGoal) steps++;
        Assert.Equal(24, steps);                                                // 500 m at 20 m a step
        Assert.Equal(100, f.Percent);
    }

    [Fact]
    public void March_empty_no_time_no_move()
    {
        var f = FrontAt();
        Assert.Equal(MarchStep.Moved, MarchRule.Step(f, 20, 2.0, 0, [], Walkable, T0, out _));
        Assert.Equal((0f, 0), (f.X, f.Percent));
        Assert.Empty(MarchRule.NewSteps(f));
    }

    [Fact]
    public void Lines_passes_the_four_lines() =>
        Assert.Equal(["N: a horde of 8 rises in Farbane Woods and marches on G", "N: the horde is 50 % of the way to G", "N: horde 1 of 2 destroyed", "N: the living have won"],
            [Messages.ApocalypseRise("N", 8, "Farbane Woods", "G"), Messages.ApocalypseProgress("N", 50, "G"), Messages.ApocalypseDefeat("N", 1, 2),
             Messages.ApocalypseOutcome("N", ApocalypseResult.Won, "G")]);

    [Fact]
    public void Lines_empty_no_region_the_wilds() =>
        Assert.Equal($"N: a horde of 8 rises in {Messages.Wilds} and marches on G", Messages.ApocalypseRise("N", 8, null, "G"));

    [Fact]
    public void Outcomes_passes_won_after_every_horde()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1))), T0));
        run.Rise(w, T0.AddMinutes(1), out _);
        foreach (var u in w.SpawnAll(run, T0.AddMinutes(1)).Where(u => u.Wave == 1)) w.Kill(run, u.Key);
        Assert.Equal(ApocalypseResult.Won, run.Outcome(w, _ => [], T0.AddMinutes(2)));
    }

    [Fact]
    public void Outcomes_empty_none_before_any_rise() =>
        Assert.Equal(ApocalypseResult.None, Apo.Run(Apo.Active(Apo.Def(), T0)).Outcome(new ApoWorld(), _ => [], T0.AddSeconds(30)));

    [Fact]
    public void EndPaths_passes_stop_shows_the_scoreboard_only()
    {
        Assert.True(EndRule.ShowsScoreboard(ApocalypseEnd.Stop));
        Assert.False(EndRule.ShowsOutcome(ApocalypseEnd.Stop));
        Assert.True(EndRule.RemovesRecord(ApocalypseEnd.Stop));
    }

    [Fact]
    public void EndPaths_empty_restart_keeps_the_record()
    {
        Assert.False(EndRule.EndsRun(ApocalypseEnd.Restart));
        Assert.False(EndRule.RemovesRecord(ApocalypseEnd.Restart));
        Assert.False(EndRule.ShowsScoreboard(ApocalypseEnd.Restart));
    }

    [Fact]
    public void Resume_passes_a_running_record_resumes()
    {
        var record = Record();
        Assert.Equal(ResumeVerdict.Resume, ResumeRule.Verdict(record, Apo.Set(Apo.Event()), T0.AddMinutes(3)));
        var run = ApocalypseRun.Resumed(Apo.Active(Apo.Def(), T0), record, T0.AddMinutes(3));
        Assert.Equal((1, 120f), (run.Risen, run.Fronts[1].X));
    }

    [Fact]
    public void Resume_empty_no_missed_horde() =>
        Assert.Empty(ResumeRule.CatchUp(Record(), Apo.Def().Apocalypse!, T0.AddMinutes(3)));

    [Fact]
    public void Lifetime_passes_renewed_while_the_run_lives()
    {
        var unit = new TrackedUnit(1, Apo.Zombie, "apo", T0, 7320, T0.AddHours(2));
        Assert.Equal(T0.AddHours(3), RollingLifetime.Renew(unit, true, T0.AddHours(1), T0.AddDays(2), 60, 120)!.Value.DueUtc);
    }

    [Fact]
    public void Lifetime_empty_an_ended_run_is_not_renewed() =>
        Assert.Null(RollingLifetime.Renew(new TrackedUnit(1, Apo.Zombie, "apo", T0, 7320, T0.AddMinutes(1)), false, T0, T0.AddDays(2), 60, 120));

    [Fact]
    public void Caps_passes_one_apocalypse_within_the_caps()
    {
        var engine = Engine(out _, Apo.Event());
        Assert.Null(engine.Start("apo", "manual", T0, Open()));
        Assert.Single(engine.Active);
    }

    [Fact]
    public void Caps_empty_no_room_no_order()
    {
        var w = new ApoWorld(maxTracked: 0);
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.Empty(run.TopUp(w, T0));
        Assert.Equal(0, w.Ledger.Occupied);
    }

    [Fact]
    public void Tally_passes_every_bucket_balances()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        run.TopUp(w, T0);
        var units = w.SpawnAll(run, T0);
        w.Kill(run, units[0].Key);
        Assert.Equal("apocalypse apo units: 6 queued = 0 pending + 5 alive + 1 died + 0 despawned + 0 removed + 0 failed", run.Tally.Line("apo", w.Alive(run)));
    }

    [Fact]
    public void Tally_empty_a_new_run_is_all_zero() =>
        Assert.Equal("apocalypse apo units: 0 queued = 0 pending + 0 alive + 0 died + 0 despawned + 0 removed + 0 failed", new ApocalypseTally().Line("apo", 0));

    [Fact]
    public void Bosses_passes_the_last_horde_brings_its_bosses()
    {
        var w = new ApoWorld();
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 1), extra: "\"bosses\": [ " + Apo.BossJson() + " ]")), T0));
        Assert.Equal(9, run.Rise(w, T0.AddMinutes(1), out _)!.Queued);
        Assert.Single(w.SpawnAll(run, T0.AddMinutes(1)), u => u.Prefab == Apo.Tourok && u.Wave == Apocalypse.BossWave(1, 1));
    }

    [Fact]
    public void Bosses_empty_no_bosses_key()
    {
        var a = Apo.Def().Apocalypse!;
        Assert.Null(a.Bosses);
        Assert.All(HordeSchedule.Units(a, 3, 50, 0, 200, new StepRandom(), []), u => Assert.Equal(0, u.Entry));
    }
}
