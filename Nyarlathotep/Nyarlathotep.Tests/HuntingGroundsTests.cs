using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D4, D5, D7, D8, D10 and D12: a Region wave spread over its scope regions, the region lock that
/// brings an event's own units back inside, the hunt targets inside the regions and the hidden jumps toward them, the
/// ambush that picks only players inside, and the end paths that leave no record. The map: FarbaneWoods covers x and z
/// 0-1000, DunleyFarmlands x 1000-2000 (z 0-1000), everything else lies in no scope region.</summary>
public class HuntingGroundsTests
{
    static readonly DateTime T0 = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);
    const string Home = "FarbaneWoods", Other = "DunleyFarmlands";
    static readonly Scope HomeScope = new([Home]);
    static readonly Scope BothScope = new([Home, Other]);

    static string MapOf(float x, float z) =>
        z is < 0 or > 1000 ? "None" : x is >= 0 and <= 1000 ? Home : x is > 1000 and <= 2000 ? Other : "None";

    static RegionPolygon Rect(string region, float x0, float z0, float x1, float z1) =>
        new(region, x0, z0, x1, z1, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]);

    static readonly RegionPolygon HomeRect = Rect(Home, 0, 0, 1000, 1000);
    static readonly RegionPolygon OtherRect = Rect(Other, 1000, 0, 1250, 1000);   // a quarter of Home's area

    sealed class Probe(Func<float, float, byte, bool>? grounded = null, Func<float, float, byte, bool>? free = null, bool throws = false) : IWalkProbe
    {
        public int Calls { get; private set; }
        public byte Level => 10;

        public bool IsGrounded(float x, float z, byte level)
        {
            Calls++;
            if (throws) throw new InvalidOperationException("walk check down");
            return grounded?.Invoke(x, z, level) ?? true;
        }

        public bool IsFree(float x, float z, byte level)
        {
            Calls++;
            return free?.Invoke(x, z, level) ?? true;
        }
    }

    static readonly byte[] Levels = [10, 11, 12];
    static float Height(byte level) => level * 2f;

    static SpotWorld Spots(IReadOnlyList<RegionPolygon> polygons, Scope scope, IWalkProbe? probe = null, Func<float, float, bool>? claimed = null,
        WalkBudget? budget = null) =>
        new(polygons, scope, MapOf, claimed, new WaveWalk(probe ?? new Probe(), budget ?? new WalkBudget(1_000_000)), Levels, Height);

    static IRandom Rng(int seed) => new SystemRandom(new Random(seed));

    static List<UnitEntry> Units(int n, string prefab = "CHAR_Spider_Forest") => Enumerable.Repeat(new UnitEntry(prefab, 1), n).ToList();

    static WaveFacts RegionFacts(int wave = 1, PickOutcome? pick = null, bool allow = true) =>
        new(wave, "hg", false, Location: LocationType.Region, Pick: pick, AllowTerritory: allow);

    // ---- events and the fake world

    static readonly Location PointLoc = new(LocationType.Point, 500, 500);

    static EventDefinition Def(string id, Location? location = null, bool locked = true, BehaviourType? behaviour = null, Scope? scope = null) =>
        new(id, "x", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 600,
            new SpawnWavesAction([new UnitEntry("CHAR_Spider_Forest", 5)], 3, 60, 10, location ?? PointLoc, null) with
            {
                Scope = scope ?? HomeScope,
                RegionLock = locked,
                Behaviour = behaviour is { } b ? new Behaviour(b, b == BehaviourType.Hunt ? 40 : 0) : null,
            },
            Announce.None);

    static ActiveEvent Running(EventDefinition d, DateTime? started = null) =>
        new(new RunningInstance(d, started ?? T0, (started ?? T0).AddMinutes(10)), "manual", null);

    static ActiveEvent Hunting(string id = "hg") => Running(Def(id, behaviour: BehaviourType.RegionHunt));

    sealed class World : IRegionWorld
    {
        public List<TrackedUnit> Tracked { get; } = [];
        public Dictionary<long, RegionUnit> At { get; } = [];
        public List<HuntCandidate> People { get; } = [];
        public Dictionary<long, List<(float X, float Z)?>> Entries { get; } = [];
        public List<RegionReturn> Returns { get; } = [];
        public List<RegionJump> Jumps { get; } = [];
        public HashSet<long> ThrowRead { get; } = [];
        public Func<float, float, bool> Claimed { get; set; } = (_, _) => false;
        public Func<float, float, byte, bool> Walk { get; set; } = (_, _, _) => true;
        public bool ThrowPlayers { get; set; }
        public int PlayerReads { get; private set; }
        public WalkBudget Budget { get; set; } = new(1_000_000);

        public void Add(long key, float x, float z, string? eventId = "hg", DateTime? spawned = null, int wave = 1, int group = 0,
            (float X, float Y, float Z)? spawn = null, byte level = 10)
        {
            var at = spawned ?? T0.AddSeconds(5);
            Tracked.Add(new TrackedUnit(key, "CHAR_Spider_Forest", eventId, at, 600, at.AddMinutes(10), wave));
            At[key] = new RegionUnit(key, eventId, at, x, 20f, z, level, wave, group, spawn);
        }

        public void Move(long key, float x, float z) => At[key] = At[key] with { X = x, Z = z };

        public IEnumerable<TrackedUnit> Units => Tracked;

        public RegionUnit? Read(TrackedUnit unit) =>
            ThrowRead.Contains(unit.Key) ? throw new InvalidOperationException("entity read failed")
            : At.TryGetValue(unit.Key, out var u) ? u : null;

        public string RegionOf(float x, float z) => MapOf(x, z);
        public bool IsClaimed(string eventId, float x, float z) => Claimed(x, z);
        public bool Walkable(float x, float z, byte level) => Walk(x, z, level);
        public float HeightOf(byte level) => Height(level);

        public IReadOnlyList<HuntCandidate> Players()
        {
            PlayerReads++;
            if (ThrowPlayers) throw new InvalidOperationException("player query failed");
            return People;
        }

        public void Return(RegionReturn r, Scope scope)
        {
            Returns.Add(r);
            At[r.Key] = At[r.Key] with { X = r.X, Y = r.Y, Z = r.Z };
            if (Entries.TryGetValue(r.Key, out var e)) e.RemoveAll(p => LockRule.DropsEntry(scope, MapOf, p));
        }

        public void Jump(RegionJump j)
        {
            Jumps.Add(j);
            At[j.Key] = At[j.Key] with { X = j.X, Y = j.Y, Z = j.Z, Level = j.Level };
        }
    }

    static HuntCandidate Player(long key, float x, float z, bool online = true, bool alive = true, bool territory = false, bool pvp = false) =>
        new(key, x, z, online, alive, territory, pvp);

    static Func<double> Doubles(int seed)
    {
        var r = new Random(seed);
        return r.NextDouble;
    }

    // ================= D4 Spread =================

    [Fact]
    public void Spread_fails_when_a_spot_leaves_the_scope_or_enters_territory()
    {
        var wide = Rect(Home, 0, 0, 1100, 1000);                                      // its box runs 100 m into DunleyFarmlands
        var claimed = (Func<float, float, bool>)((x, _) => x < 500);
        var all = new List<RegionSpot>();
        for (var seed = 0; seed < 50; seed++)
            all.AddRange(SpotDraw.Draw(6, Spots([wide, OtherRect], HomeScope, claimed: claimed), Rng(seed)));
        Assert.True(all.Count > 200, $"{all.Count} spots");
        Assert.All(all, s =>
        {
            Assert.Equal(Home, MapOf(s.X, s.Z));
            Assert.Equal(Home, s.Region);
            Assert.False(claimed(s.X, s.Z), $"{s.X} {s.Z} claimed");
        });
        var allowed = Enumerable.Range(0, 20).SelectMany(seed => SpotDraw.Draw(6, Spots([wide], HomeScope), Rng(seed))).ToList();
        Assert.Contains(allowed, s => s.X < 500);                                     // allowTerritory (no claimed test): anywhere inside
    }

    [Fact]
    public void Spread_fails_when_area_does_not_weight_the_draw()
    {
        int home = 0, other = 0;
        var rng = Rng(11);
        for (var i = 0; i < 1000; i++)
            foreach (var s in SpotDraw.Draw(1, Spots([OtherRect, HomeRect], BothScope), rng))
                if (s.Region == Home) home++; else if (s.Region == Other) other++;
        Assert.Equal(1000, home + other);
        Assert.True(home > 3.3 * other && home < 4.8 * other, $"home {home}, other {other}");   // four times the area: about 800 to 200
        Assert.Equal(1_000_000d, SpotDraw.Area(HomeRect));
        Assert.Equal(250_000d, SpotDraw.Area(OtherRect));
    }

    [Fact]
    public void Spread_fails_when_two_spots_stand_closer_than_30_m()
    {
        var tiny = Rect(Home, 100, 100, 160, 160);
        for (var seed = 0; seed < 40; seed++)
        {
            foreach (var spots in new[] { SpotDraw.Draw(10, Spots([tiny], HomeScope), Rng(seed)), SpotDraw.Draw(25, Spots([HomeRect], HomeScope), Rng(seed)) })
                for (var i = 0; i < spots.Count; i++)
                    for (var j = i + 1; j < spots.Count; j++)
                        Assert.True(HuntingGrounds.Distance(spots[i].X, spots[i].Z, spots[j].X, spots[j].Z) >= HuntingGrounds.SpotSpacing,
                            $"seed {seed}: spots {i} and {j}");
            Assert.True(SpotDraw.Draw(10, Spots([tiny], HomeScope), Rng(seed)).Count <= 5);   // a 60 m square holds few 30 m-spaced spots
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(11, 3)]
    [InlineData(23, 5)]
    [InlineData(50, 10)]
    public void Spread_fails_when_a_group_exceeds_5_units(int units, int groups)
    {
        var mixed = new List<UnitEntry> { new("CHAR_A", units - units / 2, 1, new SpawnModifiers(LevelDelta: 2)), new("CHAR_B", units / 2) };
        var split = RegionWave.Split(mixed.Where(u => u.Count > 0).ToList());
        Assert.Equal(groups, split.Count);
        Assert.All(split, g => Assert.InRange(g.Sum(u => u.Count), 1, HuntingGrounds.GroupUnits));
        Assert.Equal(units, split.Sum(g => g.Sum(u => u.Count)));
        Assert.All(split.SelectMany(g => g).Where(u => u.Prefab == "CHAR_A"), u => Assert.Equal(2, u.Modifiers!.LevelDelta));   // modifiers kept
        Assert.True(split.Max(g => g.Sum(u => u.Count)) - split.Min(g => g.Sum(u => u.Count)) <= 1);                         // dealt round robin
    }

    [Fact]
    public void Spread_fails_when_a_spot_has_no_free_grounded_tile()
    {
        var probe = new Probe(grounded: (_, _, level) => level == 12, free: (x, _, _) => x > 500);
        var spots = Enumerable.Range(0, 20).SelectMany(seed => SpotDraw.Draw(4, Spots([HomeRect], HomeScope, probe), Rng(seed))).ToList();
        Assert.NotEmpty(spots);
        Assert.All(spots, s =>
        {
            Assert.Equal((byte)12, s.Level);
            Assert.Equal(24f, s.Y);                                                   // the level's height: the group's walk height
            Assert.True(s.X > 500);
        });
        Assert.Empty(SpotDraw.Draw(4, Spots([HomeRect], HomeScope, new Probe(free: (_, _, _) => false)), Rng(1)));
        Assert.Empty(SpotDraw.Draw(4, Spots([HomeRect], HomeScope, new Probe(throws: true)), Rng(1)));   // a failed check: no spot, not an unchecked one
        var noProbe = new SpotWorld([HomeRect], HomeScope, MapOf, null, new WaveWalk(null, new WalkBudget()), Levels, Height);
        Assert.Empty(SpotDraw.Draw(4, noProbe, Rng(1)));
        var budget = new WalkBudget(5);                                              // two calls a level: two levels, then spent
        var counted = new Probe(grounded: (_, _, _) => false);
        Assert.Empty(SpotDraw.Draw(4, Spots([HomeRect], HomeScope, counted, budget: budget), Rng(1)));
        Assert.Equal(2, counted.Calls);                                              // only the calls the budget paid for
        Assert.Equal(1, budget.Left);
    }

    [Fact]
    public void Spread_fails_when_a_wave_without_a_spot_spawns_or_is_not_skipped()
    {
        var drawn = 0;
        var none = RegionWave.Decide(RegionFacts(2), () => Units(12), 20, 0, 150, g => { drawn = g; return []; });
        Assert.Equal(3, drawn);
        Assert.Equal(WaveOutcome.Skip, none.Outcome);
        Assert.Equal("wave 2 of hg skipped: no walkable spot in the regions", none.Line);
        Assert.Empty(none.Groups);
        var queued = new List<WaveGroup>();
        var reports = new List<WaveOutcome>();
        var skipped = new List<string>();
        WaveRun.Run(none, skipped.Add, queued.Add, reports.Add);
        Assert.Empty(queued);
        Assert.Equal([WaveOutcome.Skip], reports);
        Assert.Equal(["wave 2 of hg skipped: no walkable spot in the regions"], skipped);

        var one = RegionWave.Decide(RegionFacts(), () => Units(12), 20, 0, 150, _ => [new RegionSpot(300, 24, 300, 12, Home)]);
        Assert.Equal(WaveOutcome.Spawn, one.Outcome);
        var g = Assert.Single(one.Groups);                                            // the groups without a spot are not queued
        Assert.Equal((300f, 24f, 300f), g.Centre);
        Assert.Equal(4, g.Units.Sum(u => u.Count));
    }

    [Fact]
    public void Spread_fails_when_the_clamp_runs_per_group()
    {
        var capped = RegionWave.Decide(RegionFacts(), () => Units(12), 10, 0, 150, n => Enumerable.Range(0, n).Select(i => new RegionSpot(100 + 40 * i, 0, 100, 10, Home)).ToList());
        Assert.Equal(10, capped.Groups.Sum(g => g.Units.Sum(u => u.Count)));       // 12 rolled, MaxUnitsPerWave 10, once for the wave
        Assert.Equal(2, capped.Groups.Count);
        Assert.Single(capped.CapLines);
        var slots = RegionWave.Decide(RegionFacts(), () => Units(12), 20, 145, 150, n => Enumerable.Range(0, n).Select(i => new RegionSpot(100 + 40 * i, 0, 100, 10, Home)).ToList());
        Assert.Equal(5, slots.Groups.Sum(g => g.Units.Sum(u => u.Count)));          // 5 free MaxTrackedUnits slots
        Assert.Single(slots.Groups);
    }

    [Fact]
    public void Spread_fails_when_a_player_position_is_read()
    {
        var types = typeof(SpotWorld).GetProperties().Select(p => p.PropertyType)
            .Concat(typeof(RegionWave).GetMethods().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
            .Concat(typeof(SpotDraw).GetMethods().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType)).ToList();
        Assert.DoesNotContain(types, t => t.ToString().Contains("HuntCandidate") || t.ToString().Contains("PickCandidate") || t.ToString().Contains("FanOutPick"));
        var picked = RegionWave.Decide(RegionFacts(pick: PickOutcome.NoEligible), () => Units(5), 20, 0, 150, _ => [new RegionSpot(1, 2, 3, 10, Home)]);
        Assert.Equal(WaveOutcome.Spawn, picked.Outcome);                              // a pick outcome is not read for a Region wave
    }

    [Fact]
    public void Spread_passes_groups_over_the_regions()
    {
        var world = Spots([HomeRect, OtherRect], BothScope);
        var d = RegionWave.Decide(RegionFacts(), () => Units(12), 20, 0, 150, n => SpotDraw.Draw(n, world, Rng(3)));
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal(3, d.Groups.Count);
        Assert.Equal([0, 1, 2], d.Groups.Select(g => g.Index));
        Assert.All(d.Groups, g => Assert.True(BothScope.Names(MapOf(g.Centre.X, g.Centre.Z))));
        Assert.Equal("in 3 groups over FarbaneWoods, DunleyFarmlands", RegionWave.Where(3, BothScope));
        Assert.Equal("in 1 group over FarbaneWoods", RegionWave.Where(1, HomeScope));
        Assert.Equal("event hg wave 2 group 1: spot 123.5 456 h 11 in FarbaneWoods", RegionWave.SpotLine("hg", 2, 1, new RegionSpot(123.46f, 22, 456f, 11, Home)));
    }

    [Fact]
    public void Spread_empty_no_unit_rolled()
    {
        var drawn = false;
        var d = RegionWave.Decide(RegionFacts(), () => [], 20, 0, 150, _ => { drawn = true; return []; });
        Assert.Equal(WaveOutcome.ZeroRolled, d.Outcome);
        Assert.False(drawn);
        Assert.Empty(SpotDraw.Draw(0, Spots([HomeRect], HomeScope), Rng(1)));
        Assert.Empty(SpotDraw.Draw(3, Spots([], HomeScope), Rng(1)));
    }

    // ================= D5 Lock =================

    static LockPhase Checked(World w, params ActiveEvent[] active)
    {
        var phase = new LockPhase();
        phase.Tick(T0, active, w, Doubles(1), _ => { }, _ => { });
        return phase;
    }

    [Fact]
    public void Lock_fails_when_the_slack_is_off()
    {
        var a = Running(Def("hg"));
        var w = new World();
        w.Add(1, 0, 500);
        w.Add(2, 0, 600);
        var phase = Checked(w, a);                                                     // both recorded inside, at x 0
        w.Move(1, -10.5f, 500);
        w.Move(2, -9.5f, 600);
        var check = phase.Run(a, w, Doubles(2), _ => { }, _ => { })!;
        Assert.Equal([1L], check.Returns.Select(r => r.Key));
        Assert.Equal(10.5f, check.Farthest, 3);
        Assert.True(LockRule.Strays(false, 10.5f, 0, new InsideSpot(0, 0, 0, 10)));
        Assert.False(LockRule.Strays(false, 9.5f, 0, new InsideSpot(0, 0, 0, 10)));
        Assert.False(LockRule.Strays(true, 500, 0, new InsideSpot(0, 0, 0, 10)));
    }

    [Fact]
    public void Lock_fails_when_a_return_lands_outside_or_far()
    {
        var last = new InsideSpot(2, 20, 500, 10);                                    // 2 m inside the edge
        bool Inside(float x, float z) => HuntingGrounds.Inside(HomeScope, MapOf, x, z);
        for (var seed = 0; seed < 200; seed++)
        {
            var (x, y, z) = LockRule.ReturnSpot(last, Inside, (_, _, _) => true, new WalkBudget(), Doubles(seed));
            Assert.True(Inside(x, z), $"seed {seed}: {x} {z}");
            Assert.True(HuntingGrounds.Distance(x, z, last.X, last.Z) <= HuntingGrounds.Slack + 0.001f);
            Assert.Equal(20f, y);
        }
        Assert.Equal((2f, 20f, 500f), LockRule.ReturnSpot(last, Inside, (_, _, _) => false, new WalkBudget(), Doubles(1)));   // none walkable: the spot itself
        Assert.Equal((2f, 20f, 500f), LockRule.ReturnSpot(last, Inside, (_, _, _) => true, new WalkBudget(0), Doubles(1)));  // budget spent: the spot itself
        var asked = new List<byte>();
        LockRule.ReturnSpot(last, Inside, (_, _, l) => { asked.Add(l); return false; }, new WalkBudget(), Doubles(1));
        Assert.All(asked, l => Assert.Equal((byte)10, l));                            // at the spot's own level

        var a = Running(Def("hg"));
        var w = new World();
        w.Add(1, 300, 300, spawn: (300, 20, 300));
        w.Move(1, -80, 300);                                                          // never seen inside: its spawn point is the spot
        var check = new LockPhase().Run(a, w, Doubles(3), _ => { }, _ => { })!;
        var r = Assert.Single(check.Returns);
        Assert.True(HuntingGrounds.Distance(r.X, r.Z, 300, 300) <= HuntingGrounds.Slack + 0.001f);
        Assert.Equal(380f, r.Distance, 3);
    }

    [Fact]
    public void Lock_fails_when_an_entry_outside_is_kept()
    {
        var a = Running(Def("hg"));
        var w = new World();
        w.Add(1, 100, 100);
        w.Entries[1] = [(-200, 500), (300, 300), null, (1500, 500)];
        var phase = Checked(w, a);
        w.Move(1, -50, 100);
        phase.Run(a, w, Doubles(2), _ => { }, _ => { });
        Assert.Single(w.Returns);
        Assert.Equal([(300f, 300f)], w.Entries[1].Select(e => e!.Value));            // only the player inside stays
        Assert.False(LockRule.DropsEntry(BothScope, MapOf, (1500, 500)));
        Assert.True(LockRule.DropsEntry(HomeScope, MapOf, (1500, 500)));
        Assert.True(LockRule.DropsEntry(HomeScope, MapOf, null));
    }

    [Fact]
    public void Lock_fails_when_a_seed_goes_outside()
    {
        var players = new[] { Player(1, 300, 300), Player(2, -20, 300), Player(3, 1500, 300) };
        var locked = Def("hg", behaviour: BehaviourType.Hunt).Action!;
        Assert.Equal([1L], LockRule.SeedPool(locked, players, MapOf).Select(p => p.Key));
        Assert.Empty(LockRule.SeedPool(locked, players, null));                       // no region lookup: no seed at all
        Assert.Equal([1L, 2L, 3L], LockRule.SeedPool(locked with { RegionLock = false }, players, MapOf).Select(p => p.Key));   // without the lock: unchanged
        Assert.Equal([1L, 3L], LockRule.SeedPool(locked with { Scope = BothScope }, players, MapOf).Select(p => p.Key));
        Assert.Equal([1L], HuntRule.SeedTargets([Player(2, -5, 300), Player(1, 30, 300)], HomeScope, MapOf, (5, 300)));   // RegionHunt: the nearer one is outside
    }

    [Theory]
    [InlineData("Point")]
    [InlineData("Admin")]
    [InlineData("AroundPlayer")]
    [InlineData("Region")]
    public void Lock_fails_when_a_location_is_missed(string type)
    {
        var location = type switch
        {
            "Point" => PointLoc,
            "Admin" => new Location(LocationType.Admin, 0, 0),
            "AroundPlayer" => new Location(LocationType.AroundPlayer, 0, 0, null, 20, 40),
            _ => new Location(LocationType.Region, 0, 0),
        };
        var a = Running(Def("hg", location));
        var w = new World();
        w.Add(1, 500, 990);
        var phase = Checked(w, a);
        w.Move(1, 500, 1030);
        var check = phase.Run(a, w, Doubles(2), _ => { }, _ => { })!;
        Assert.Equal(40f, Assert.Single(check.Returns).Distance, 3);
        Assert.Equal(Home, MapOf(w.At[1].X, w.At[1].Z));
    }

    [Fact]
    public void Lock_fails_when_another_units_move()
    {
        var a = Running(Def("hg"));
        var open = Running(Def("open", locked: false));
        var w = new World();
        w.Add(1, -100, 500, spawn: (10, 0, 500));                                     // ours, out by 110 m: returned
        w.Add(2, -100, 500, eventId: "other", spawn: (10, 0, 500));                   // another event
        w.Add(3, -100, 500, spawned: T0.AddSeconds(-30), spawn: (10, 0, 500));        // an earlier instance of hg
        w.Add(4, -100, 500, eventId: null, spawn: (10, 0, 500));                      // `.nyar spawn`
        w.Add(5, -100, 500, eventId: "open", spawn: (10, 0, 500));                    // an event without the lock
        new LockPhase().Tick(T0, [a, open], w, Doubles(1), _ => { }, _ => { });
        Assert.Equal([1L], w.Returns.Select(r => r.Key));
        Assert.False(HuntingGrounds.Locked(open));
        Assert.False(HuntingGrounds.Locked(Running(Def("g", scope: Scope.Global))));
    }

    [Fact]
    public void Lock_fails_when_the_line_names_or_locates()
    {
        var a = Running(Def("hg"));
        var w = new World();
        w.People.Add(Player(87654321, 432.1f, 876.5f));
        w.Add(1, 432.1f, 876.5f, spawn: (432.1f, 0, 876.5f));
        w.Add(2, 432.1f, 876.5f, spawn: (432.1f, 0, 876.5f));
        w.Move(1, -40, 876.5f);
        w.Move(2, -30, 876.5f);
        var info = new List<string>();
        new LockPhase().Run(a, w, Doubles(1), info.Add, _ => { });
        var line = Assert.Single(info);
        Assert.Matches(@"^region lock hg: returned 2, farthest 472 m out$", line);
        Assert.DoesNotContain("432", line);
        Assert.DoesNotContain("876", line);
        Assert.DoesNotContain("87654321", line);
        Assert.Equal(0, w.PlayerReads);                                               // the lock reads no player
        Assert.Equal("region lock hg: check failed: boom", LockRule.FailedLine("hg", "boom"));
    }

    [Fact]
    public void Lock_passes_check_every_5_s()
    {
        var a = Running(Def("hg"));
        var w = new World();
        w.Add(1, -100, 500, spawn: (10, 0, 500));
        var phase = new RegionPhase();
        Assert.Single(phase.Tick(T0, [a], w, Doubles(1), _ => { }, _ => { }).Locks);
        w.Move(1, -100, 500);
        Assert.Empty(phase.Tick(T0.AddSeconds(2), [a], w, Doubles(1), _ => { }, _ => { }).Locks);    // not yet due
        Assert.Single(w.Returns);
        Assert.Single(phase.Tick(T0.AddSeconds(5), [a], w, Doubles(1), _ => { }, _ => { }).Locks);
        Assert.Equal(2, w.Returns.Count);
        Assert.Equal(5, HuntingGrounds.CheckSeconds);
        Assert.True(RegionPhase.Needed([a]));
        Assert.False(RegionPhase.Needed([Running(Def("open", locked: false))]));
    }

    [Fact]
    public void Lock_empty_no_live_unit()
    {
        var a = Running(Def("hg"));
        var w = new World();
        var info = new List<string>();
        var phase = new LockPhase();
        var check = phase.Run(a, w, Doubles(1), info.Add, _ => { })!;
        Assert.Empty(check.Returns);
        Assert.Null(check.LogLine);
        Assert.Empty(info);
        Assert.Equal(0, phase.Records("hg"));
        Assert.Empty(phase.Health);
    }

    // ================= D7 HuntTargets =================

    static HuntPhase Hunted(World w, params ActiveEvent[] active)
    {
        var phase = new HuntPhase();
        phase.Tick(active, w, _ => { }, _ => { });
        return phase;
    }

    [Fact]
    public void HuntTargets_fails_when_an_ineligible_player_is_targeted_or_seeded()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 500);
        w.People.AddRange([
            Player(10, -5, 500),                                                      // outside the regions, nearest of all
            Player(11, 505, 500, territory: true),
            Player(12, 495, 500, pvp: true),
            Player(13, 500, 505, alive: false),
            Player(14, 500, 495, online: false),
            Player(15, float.NaN, 500),
            Player(20, 540, 500),                                                     // the only eligible one
        ]);
        Assert.Equal(20L, Hunted(w, a).Targets("hg")[(1, 0)]);
        Assert.Equal([20L], HuntRule.SeedTargets(w.People, HomeScope, MapOf, (500, 500)));
        Assert.Equal([20L], HuntRule.EligibleInside(w.People, HomeScope, MapOf).Select(p => p.Key));
    }

    [Fact]
    public void HuntTargets_fails_when_a_farther_player_wins()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 100, 100);
        w.Add(2, 120, 100);                                                           // the group's mean: (110, 100)
        w.People.AddRange([Player(5, 400, 100), Player(6, 200, 100), Player(7, 110, 900)]);
        Assert.Equal(6L, Hunted(w, a).Targets("hg")[(1, 0)]);
        Assert.Equal([6L, 5L], HuntRule.SeedTargets([Player(5, 150, 100), Player(6, 120, 100)], HomeScope, MapOf, (110, 100)).Take(2));   // nearest first
    }

    [Fact]
    public void HuntTargets_fails_when_a_tie_breaks_other_than_by_key()
    {
        var eligible = new[] { Player(9, 200, 100), Player(3, 0, 100), Player(5, 100, 200) };
        Assert.Equal(3L, HuntRule.Target(eligible, (100, 100))!.Value.Key);
        var a = Hunting();
        var w = new World();
        w.Add(1, 100, 100);
        w.People.AddRange(eligible);
        Assert.Equal(3L, Hunted(w, a).Targets("hg")[(1, 0)]);
    }

    [Fact]
    public void HuntTargets_fails_when_a_group_without_a_player_moves()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 500);
        w.People.AddRange([Player(1, -300, 500), Player(2, 500, 1400), Player(3, 600, 500, territory: true)]);
        var info = new List<string>();
        var phase = new HuntPhase();
        var check = Assert.Single(phase.Tick([a], w, info.Add, _ => { }));
        Assert.Empty(w.Jumps);
        Assert.Empty(phase.Targets("hg"));
        Assert.Equal(0, check.Waiting + check.InReach);
        Assert.Empty(info);
        Assert.Empty(HuntRule.SeedTargets(w.People, HomeScope, MapOf, (500, 500)));
    }

    [Fact]
    public void HuntTargets_fails_when_a_target_is_kept_past_its_check()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 500);
        w.People.AddRange([Player(1, 520, 500), Player(2, 700, 500)]);
        var phase = Hunted(w, a);
        Assert.Equal(1L, phase.Targets("hg")[(1, 0)]);
        w.People[0] = Player(1, -10, 500);                                            // left the regions
        phase.Tick([a], w, _ => { }, _ => { });
        Assert.Equal(2L, phase.Targets("hg")[(1, 0)]);
        w.People[1] = Player(2, 700, 500, alive: false);                               // died
        phase.Tick([a], w, _ => { }, _ => { });
        Assert.Empty(phase.Targets("hg"));
        w.People[1] = Player(2, 700, 500);
        phase.Tick([a], w, _ => { }, _ => { });
        Assert.Single(phase.Targets("hg"));
        w.ThrowPlayers = true;                                                        // a failed read keeps no stale target
        phase.Tick([a], w, _ => { }, _ => { });
        Assert.Empty(phase.Targets("hg"));
    }

    [Fact]
    public void HuntTargets_fails_when_a_group_is_split_or_merged()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 100, 100, wave: 1, group: 0);
        w.Add(2, 110, 100, wave: 1, group: 0);
        w.Add(3, 900, 100, wave: 1, group: 1);
        w.Add(4, 500, 900, wave: 2, group: 0);
        w.People.AddRange([Player(31, 150, 100), Player(32, 850, 100), Player(33, 500, 850)]);
        var targets = Hunted(w, a).Targets("hg");
        Assert.Equal(3, targets.Count);
        Assert.Equal(31L, targets[(1, 0)]);
        Assert.Equal(32L, targets[(1, 1)]);
        Assert.Equal(33L, targets[(2, 0)]);
        Assert.Equal([(1, 0), (1, 1), (2, 0)], HuntRule.Groups(w.At.Values).Select(g => g.Key));
    }

    [Fact]
    public void HuntTargets_passes_each_group_its_nearest()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 100, 100, group: 0);
        w.Add(2, 1500, 500, group: 1);
        w.People.AddRange([Player(1, 120, 100), Player(2, 990, 500)]);
        var phase = Hunted(w, Running(Def("hg", behaviour: BehaviourType.RegionHunt, scope: BothScope)));
        Assert.Equal(1L, phase.Targets("hg")[(1, 0)]);
        Assert.Equal(2L, phase.Targets("hg")[(1, 1)]);
        Assert.Equal(1, w.PlayerReads);                                               // one read per event per check
        Assert.False(HuntingGrounds.Hunts(Running(Def("h", behaviour: BehaviourType.Hunt))));
        Assert.True(HuntingGrounds.Hunts(a));
    }

    [Fact]
    public void HuntTargets_empty_no_player_inside()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 500);
        var phase = Hunted(w, a);
        Assert.Empty(phase.Targets("hg"));
        Assert.Empty(w.Jumps);
        Assert.Null(HuntRule.Target([], (0, 0)));
    }

    // ================= D8 Jumps =================

    static (JumpKind Kind, RegionJump? Jump) Plan(RegionUnit u, (float X, float Z) target, IReadOnlyList<HuntCandidate> online,
        Func<float, float, bool>? claimed = null, Func<float, float, byte, bool>? walkable = null, WalkBudget? budget = null) =>
        JumpRule.Plan(u, target, online, (x, z) => HuntingGrounds.Inside(HomeScope, MapOf, x, z) && !(claimed?.Invoke(x, z) ?? false),
            walkable ?? ((_, _, _) => true), Height, budget ?? new WalkBudget());

    static RegionUnit Unit(float x, float z, long key = 1, byte level = 10) => new(key, "hg", T0.AddSeconds(5), x, 20, z, level, 1, 0);

    [Fact]
    public void Jumps_fails_when_a_player_at_99_m_is_ignored()
    {
        var u = Unit(500, 500);
        var target = Player(1, 500, 800);
        Assert.Equal(JumpKind.Waiting, Plan(u, (500, 800), [target, Player(2, 401, 500, pvp: true)]).Kind);       // 99 m, not eligible: still watched
        Assert.Equal(JumpKind.Waiting, Plan(u, (500, 800), [target, Player(3, 500 - 99, 500, territory: true)]).Kind);
        Assert.Equal(JumpKind.Jump, Plan(u, (500, 800), [target, Player(2, 399, 500, pvp: true)]).Kind);          // 101 m
        Assert.Equal(JumpKind.Jump, Plan(u, (500, 800), [target, Player(4, 450, 500, online: false)]).Kind);       // offline: not watching
    }

    [Fact]
    public void Jumps_passes_wait_tally_names_each_reason()
    {
        var u = Unit(500, 200);
        var online = new[] { Player(1, 500, 800) };
        var claimed = new WaitTally();
        Assert.Equal(JumpKind.Waiting, JumpRule.Plan(u, (500, 800), online, (_, _) => false, (_, _, _) => true, Height, new WalkBudget(), claimed).Kind);
        Assert.Equal(15, claimed.Candidates);
        Assert.Equal(15, claimed.NotAllowed);
        var blocked = new WaitTally();
        JumpRule.Plan(u, (500, 800), online, (_, _) => true, (_, _, _) => false, Height, new WalkBudget(), blocked);
        Assert.Equal(15, blocked.NotWalkable);
        var watched = new WaitTally();
        JumpRule.Plan(u, (500, 800), [Player(1, 500, 800), Player(2, 500, 260)], (_, _) => true, (_, _, _) => true, Height, new WalkBudget(), watched);
        Assert.Equal(1, watched.Watched);
        var line = claimed.Line("hg", 1);
        Assert.Equal("region hunt hg: waiting 1: 0 watched; of 15 landings tried 0 near a player, 15 outside the regions or claimed, 0 not walkable, 0 out of budget", line);
    }

    [Fact]
    public void Jumps_fails_when_a_free_unit_does_not_jump()
    {
        var (kind, jump) = Plan(Unit(500, 200), (500, 800), [Player(1, 500, 800), Player(2, 900, 200)]);
        Assert.Equal(JumpKind.Jump, kind);
        Assert.Equal(500f, jump!.Value.X, 3);
        Assert.Equal(225f, jump.Value.Z, 3);                                          // straight along the line, 25 m
        Assert.Equal((byte)10, jump.Value.Level);
        Assert.Equal(20f, jump.Value.Y);
    }

    [Fact]
    public void Jumps_fails_when_a_step_passes_25_m()
    {
        var rng = new Random(5);
        for (var i = 0; i < 300; i++)
        {
            var ux = (float)(100 + rng.NextDouble() * 800);
            var uz = (float)(100 + rng.NextDouble() * 300);
            var tz = uz + 130 + (float)(rng.NextDouble() * 400);
            if (tz > 990) continue;
            var blocked = rng.Next(3);
            var (kind, jump) = Plan(Unit(ux, uz), (ux, tz), [Player(1, ux, tz)], walkable: (x, z, _) => blocked == 0 || (int)(x + z) % 3 != 0);
            if (kind != JumpKind.Jump) continue;
            Assert.True(HuntingGrounds.Distance(ux, uz, jump!.Value.X, jump.Value.Z) <= HuntingGrounds.Step + 0.01f, $"case {i}");
        }
        Assert.All(JumpRule.Candidates((0, 0), (0, 400)), c => Assert.False(c.Final));
    }

    [Fact]
    public void Jumps_fails_when_a_final_lands_outside_50_60_m()
    {
        foreach (var d in new[] { 61f, 80f, 100f, 124f, 125f })
        {
            var candidates = JumpRule.Candidates((500, 300), (500, 300 + d)).ToList();
            Assert.All(candidates, c =>
            {
                Assert.True(c.Final);
                Assert.InRange(HuntingGrounds.Distance(c.X, c.Z, 500, 300 + d), 49.99f, 60.01f);
            });
            var (kind, jump) = Plan(Unit(500, 300), (500, 300 + d), [Player(1, 500, 300 + d)]);
            if (d <= HuntingGrounds.WatchRange) Assert.Equal(JumpKind.Waiting, kind);   // the target itself watches within 100 m
            else Assert.InRange(HuntingGrounds.Distance(jump!.Value.X, jump.Value.Z, 500, 300 + d), 50f, 60f);
        }
        var far = Plan(Unit(500, 300), (500, 424), [Player(1, 500, 424)]);
        Assert.Equal(JumpKind.Jump, far.Kind);                                        // 124 m: 99 m after a step, so the final ring
        Assert.InRange(HuntingGrounds.Distance(far.Jump!.Value.X, far.Jump.Value.Z, 500, 424), 50f, 60f);
        Assert.Equal(55f, HuntingGrounds.Distance(far.Jump.Value.X, far.Jump.Value.Z, 500, 424), 2);
        Assert.False(JumpRule.Candidates((500, 300), (500, 426)).First().Final);      // 126 m: a step
    }

    [Fact]
    public void Jumps_fails_when_a_landing_breaks_a_rule()
    {
        var rng = new Random(17);
        var jumped = 0;
        for (var i = 0; i < 2000; i++)
        {
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            var u = Unit(R(-100, 1100), R(-100, 1100), level: (byte)rng.Next(5, 15));
            var target = (X: R(0, 1000), Z: R(0, 1000));
            var players = new List<HuntCandidate> { Player(1, target.X, target.Z) };
            for (var k = rng.Next(4); k > 0; k--) players.Add(Player(10 + k, R(-200, 1200), R(-200, 1200), online: rng.Next(4) > 0, pvp: rng.Next(2) == 0));
            var cx = R(0, 1000);
            bool Claimed(float x, float z) => Math.Abs(x - cx) < 60;
            bool Walk(float x, float z, byte level) => ((int)x + (int)z + level) % 4 != 0;
            var (kind, jump) = Plan(u, target, players, Claimed, Walk);
            if (kind != JumpKind.Jump) continue;
            jumped++;
            var j = jump!.Value;
            Assert.True(HuntingGrounds.Inside(HomeScope, MapOf, j.X, j.Z), $"case {i}: outside");
            Assert.False(Claimed(j.X, j.Z), $"case {i}: claimed");
            Assert.True(Walk(j.X, j.Z, j.Level), $"case {i}: not walkable");
            Assert.Contains(j.Level, WalkLevels.Spot(u.Level).Concat(WalkLevels.Wide(u.Level)));
            Assert.Equal(Height(j.Level), j.Y);
            foreach (var p in players.Where(p => p.Online))
            {
                Assert.True(HuntingGrounds.Distance(p.X, p.Z, j.X, j.Z) >= HuntingGrounds.LandingDistance, $"case {i}: landed near a player");
                Assert.True(HuntingGrounds.Distance(p.X, p.Z, u.X, u.Z) > HuntingGrounds.WatchRange, $"case {i}: jumped while watched");
            }
            Assert.True(HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z) > HuntingGrounds.Reach);
        }
        Assert.True(jumped > 100, $"{jumped} jumps");
        Assert.Equal(JumpKind.Waiting, Plan(Unit(500, 100), (500, 900), [Player(1, 500, 900)], budget: new WalkBudget(1)).Kind);   // budget spent: waits
    }

    [Fact]
    public void Jumps_fails_when_a_hillside_unit_waits()
    {
        var hill = Unit(500, 200, level: 13);                                       // A1: Session 2's units at h 13 over ground at h 10
        foreach (var ground in new byte[] { 10, 11, 15, 16 })
        {
            var (kind, jump) = Plan(hill, (500, 800), [Player(1, 500, 800)], walkable: (_, _, l) => l == ground);
            Assert.Equal(JumpKind.Jump, kind);
            Assert.Equal(ground, jump!.Value.Level);
            Assert.Equal(Height(ground), jump.Value.Y);
        }
        foreach (var ground in new byte[] { 9, 17 })                                  // four levels away: still waits
            Assert.Equal(JumpKind.Waiting, Plan(hill, (500, 800), [Player(1, 500, 800)], walkable: (_, _, l) => l == ground).Kind);
        var near = Plan(hill, (500, 800), [Player(1, 500, 800)], walkable: (_, z, l) => l == 12 ? z < 220 : l == 10);
        Assert.Equal((byte)12, near.Jump!.Value.Level);                               // a neighbouring level anywhere beats a wide one
        var wide = Plan(hill, (500, 800), [Player(1, 500, 800)], walkable: (x, z, l) => l == 10 && Math.Abs(x - 500) > 1);
        Assert.Equal(JumpKind.Jump, wide.Kind);                                       // the wide pass skips the straight step only
        Assert.True(HuntingGrounds.Distance(500, 200, wide.Jump!.Value.X, wide.Jump.Value.Z) <= HuntingGrounds.Step + 0.01f);
        Assert.Equal(JumpKind.Waiting, Plan(hill, (500, 800), [Player(1, 500, 800)], walkable: (_, _, l) => l == 10,
            budget: new WalkBudget(90)).Kind);                                        // the neighbouring pass spends it: waits
    }

    [Fact]
    public void Jumps_fails_when_a_stuck_unit_spends_the_budget()
    {
        var a = Hunting();
        var w = new World { Walk = (x, _, _) => x > 400 };
        w.Add(1, 100, 100);                                                           // no landing near x 100: 130 calls each
        w.Add(2, 120, 100);
        w.Add(3, 600, 100);
        w.People.Add(Player(9, 500, 900));
        var phase = new HuntPhase();
        w.Budget = new WalkBudget(260);
        var first = Assert.Single(phase.Tick([a], w, _ => { }, _ => { }));
        Assert.Empty(first.Jumps);                                                    // 1 and 2 spent it, 3 cut off
        w.Budget = new WalkBudget(260);
        var second = Assert.Single(phase.Tick([a], w, _ => { }, _ => { }));
        Assert.Equal([3L], second.Jumps.Select(j => j.Key));                          // the stuck units plan last
        Assert.Equal(2, second.Waiting);
        phase.EventEnded("hg");
        Assert.Equal(0, phase.AllRecords);
    }

    [Fact]
    public void Jumps_fails_when_more_than_20_jump()
    {
        var a = Hunting("a");
        var b = Hunting("b");
        var w = new World();
        for (var i = 0; i < 15; i++) w.Add(i, 100 + 20 * i, 100, eventId: "a");
        for (var i = 0; i < 15; i++) w.Add(100 + i, 100 + 20 * i, 120, eventId: "b");
        w.People.Add(Player(1, 500, 900));
        var checks = new HuntPhase().Tick([a, b], w, _ => { }, _ => { });
        Assert.Equal(20, w.Jumps.Count);
        Assert.Equal(15, checks[0].Jumps.Count);
        Assert.Equal(5, checks[1].Jumps.Count);
        Assert.Equal(10, checks[1].Waiting);
        var next = new HuntPhase().Tick([a, b], w, _ => { }, _ => { });                // the next check has 20 again
        Assert.Equal(20, next.Sum(c => c.Jumps.Count));
    }

    [Fact]
    public void Jumps_fails_when_another_units_move()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 100);
        w.Add(2, 520, 100, eventId: "other");
        w.Add(3, 540, 100, eventId: null);
        w.Add(4, 560, 100, spawned: T0.AddMinutes(-20));
        w.People.Add(Player(9, 500, 900));
        new HuntPhase().Tick([a, Running(Def("other", behaviour: BehaviourType.Hunt))], w, _ => { }, _ => { });
        Assert.Equal([1L], w.Jumps.Select(j => j.Key));
    }

    [Fact]
    public void Jumps_fails_when_the_line_names_or_locates()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 432.1f, 100);
        w.Add(2, 100, 500);                                                          // same group, far from every player: jumps
        w.Add(3, 777, 860, group: 1);
        w.People.Add(Player(87654321, 777.7f, 876.5f));
        var info = new List<string>();
        new HuntPhase().Tick([a], w, info.Add, _ => { });
        var line = Assert.Single(info);
        Assert.Matches(@"^region hunt hg: \d+ jumped, \d+ waiting, \d+ in reach, landed \d+ m$", line);
        Assert.DoesNotContain("87654321", line);
        Assert.DoesNotContain("777", line);
        Assert.DoesNotContain("876", line);
        Assert.DoesNotContain("432", line);
        Assert.Contains("1 in reach", line);
        Assert.Equal("region hunt hg: move failed: boom", JumpRule.FailedLine("hg", "boom"));
        Assert.Equal("region hunt hg: 1 jumped, 0 waiting, 0 in reach, landed 9999 m", JumpRule.LogLine("hg", 1, 0, 0, float.MaxValue));
    }

    [Fact]
    public void Jumps_passes_closing_in_unseen()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 50);
        var player = Player(1, 500, 900);
        w.People.Add(player);
        var phase = new HuntPhase();
        for (var check = 0; check < 60 && HuntingGrounds.Distance(w.At[1].X, w.At[1].Z, player.X, player.Z) > HuntingGrounds.Reach; check++)
        {
            var before = w.At[1];
            phase.Tick([a], w, _ => { }, _ => { });
            var after = w.At[1];
            if (before == after) continue;
            Assert.True(HuntingGrounds.Distance(after.X, after.Z, player.X, player.Z) >= HuntingGrounds.LandingDistance);
            Assert.True(HuntingGrounds.Distance(before.X, before.Z, player.X, player.Z) > HuntingGrounds.WatchRange);
        }
        var d = HuntingGrounds.Distance(w.At[1].X, w.At[1].Z, player.X, player.Z);
        Assert.InRange(d, 50f, 60f);                                                   // in Hunt's reach, unseen: the game takes over
        Assert.Equal(JumpKind.InReach, Plan(w.At[1], (player.X, player.Z), [player]).Kind);
    }

    [Fact]
    public void Jumps_empty_no_target()
    {
        var a = Hunting();
        var w = new World();
        w.Add(1, 500, 100);
        var info = new List<string>();
        var check = Assert.Single(new HuntPhase().Tick([a], w, info.Add, _ => { }));
        Assert.Empty(check.Jumps);
        Assert.Null(check.LogLine);
        Assert.Empty(info);
        Assert.Empty(JumpRule.Candidates((5, 5), (5, 5)));
    }

    // ================= D10 Ambush =================

    static bool InHome(float x, float z) => HuntingGrounds.Inside(HomeScope, MapOf, x, z);

    static PickCandidate Pc(float x, float z, string id) => new(x, 10, z, true, true, false, id);

    [Fact]
    public void Ambush_fails_when_a_player_outside_is_picked()
    {
        var outside = new[] { Pc(-5, 500, "out1"), Pc(500, 1003, "out2") };                 // ring points around them lie inside
        for (var seed = 0; seed < 20; seed++)
        {
            Assert.Equal(PickOutcome.NoEligible, PlayerPick.Choose(outside, Rng(seed), 20, 40, (_, _) => false, InHome).Outcome);
            var single = PlayerPick.Choose([.. outside, Pc(500, 500, "in")], Rng(seed), 20, 40, (_, _) => false, InHome);
            Assert.Equal(PickOutcome.Picked, single.Outcome);
            Assert.InRange(HuntingGrounds.Distance(single.Centre.X, single.Centre.Z, 500, 500), 20f, 40f);
            var many = PlayerPick.ChooseMany([.. outside, Pc(200, 200, "in1"), Pc(800, 800, "in2")], Rng(seed), 20, 40, (_, _) => false, InHome, 4, 50, "out1");
            Assert.Equal(2, many.Centres.Count);                                       // the outside focus is not tried
            Assert.All(many.Origins, o => Assert.True(InHome(o.X, o.Z)));
        }
    }

    [Fact]
    public void Ambush_fails_when_no_player_inside_spawns_a_unit()
    {
        var pick = PlayerPick.Choose([Pc(-5, 500, "out")], Rng(1), 20, 40, (_, _) => false, InHome);
        var facts = new WaveFacts(1, "hg", false, Location: LocationType.AroundPlayer, Pick: pick.Outcome);
        var d = WaveGate.DecideEntries(facts, () => Units(5), 20, 0, 150);
        Assert.Equal(WaveOutcome.Skip, d.Outcome);
        Assert.Empty(d.Units);
        var fan = PlayerPick.ChooseMany([Pc(-5, 500, "out")], Rng(1), 20, 40, (_, _) => false, InHome, 3, 50, null);
        var groups = WaveGate.DecideGroupEntries(facts with { Pick = fan.Outcome }, [], () => Units(5), 20, 0, 150);
        Assert.Equal(WaveOutcome.Skip, groups.Outcome);
        Assert.Empty(groups.Groups);
    }

    [Fact]
    public void Ambush_passes_a_player_inside()
    {
        var many = PlayerPick.ChooseMany([Pc(200, 200, "in1"), Pc(800, 800, "in2"), Pc(-50, 500, "out")], Rng(4), 20, 40, (_, _) => false, InHome, 3, 50, "in2");
        Assert.Equal(PickOutcome.Picked, many.Outcome);
        Assert.Equal((800f, 800f), many.Origins[0]);                                 // the focus inside goes first
        Assert.Equal(2, many.Centres.Count);
    }

    [Fact]
    public void Ambush_empty_no_player()
    {
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.Choose([], Rng(1), 20, 40, (_, _) => false, InHome).Outcome);
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.ChooseMany([], Rng(1), 20, 40, (_, _) => false, InHome, 3, 50, null).Outcome);
    }

    // ================= D12 EndPaths =================

    /// <summary>A region phase holding records, targets and a failing streak for "hg" (and records for "keep").</summary>
    static (RegionPhase Phase, World World, ActiveEvent Hg, ActiveEvent Keep) Loaded()
    {
        var hg = Hunting();
        var keep = Hunting("keep");
        var w = new World();
        w.Add(1, 300, 300);
        w.Add(2, 320, 300);
        w.Add(3, 600, 600, eventId: "keep");
        w.Add(4, 700, 700);
        w.ThrowRead.Add(4);                                                           // a failing streak for hg
        w.People.Add(Player(1, 500, 900));
        var phase = new RegionPhase();
        phase.Tick(T0, [hg, keep], w, Doubles(1), _ => { }, _ => { });
        Assert.Equal(2, phase.Lock.Records("hg"));
        Assert.NotEmpty(phase.Hunt.Targets("hg"));
        Assert.Equal([LockPhase.Failing, HuntPhase.Failing], phase.Health);
        w.ThrowRead.Clear();
        return (phase, w, hg, keep);
    }

    [Theory]
    [InlineData("natural end")]
    [InlineData("all waves defeated")]
    [InlineData("event stop")]
    [InlineData("fault cancel")]
    [InlineData("purge confirm")]
    [InlineData("pillar spawns off")]
    [InlineData("restart")]
    [InlineData("no longer running")]
    public void EndPaths_fails_when_an_end_path_leaves_a_record(string path)
    {
        var (phase, w, hg, keep) = Loaded();
        switch (path)
        {
            case "purge confirm" or "pillar spawns off": phase.Clear(); break;        // every event ends at once
            case "restart": phase = new RegionPhase(); break;                         // memory only
            case "no longer running": phase.Tick(T0.AddSeconds(1), [keep], w, Doubles(1), _ => { }, _ => { }); break;
            default: phase.EventEnded("hg"); break;
        }
        Assert.Equal(0, phase.Lock.Records("hg"));
        Assert.Empty(phase.Hunt.Targets("hg"));
        Assert.Empty(phase.Health);
        if (path is "purge confirm" or "pillar spawns off" or "restart")
        {
            Assert.Equal(0, phase.Lock.AllRecords);
            Assert.Equal(0, phase.Hunt.AllRecords);
        }
    }

    [Fact]
    public void EndPaths_fails_when_an_ended_event_moves_or_logs()
    {
        var (phase, w, _, keep) = Loaded();
        phase.EventEnded("hg");
        w.Move(1, -200, 300);                                                         // a stray of the ended event
        w.Move(2, 320, 100);                                                          // and one that could jump
        w.Returns.Clear();
        w.Jumps.Clear();
        var lines = new List<string>();
        phase.Tick(T0.AddSeconds(5), [keep], w, Doubles(1), lines.Add, lines.Add);
        Assert.DoesNotContain(w.Returns, r => r.Key is 1 or 2 or 4);
        Assert.DoesNotContain(w.Jumps, j => j.Key is 1 or 2 or 4);
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"\bhg\b"));
    }

    [Fact]
    public void EndPaths_passes_a_running_event_keeps_its_records()
    {
        var (phase, _, _, _) = Loaded();
        phase.EventEnded("keep");
        Assert.Equal(2, phase.Lock.Records("hg"));
        Assert.NotEmpty(phase.Hunt.Targets("hg"));
        Assert.Equal(0, phase.Lock.Records("keep"));
        Assert.Empty(phase.Hunt.Targets("keep"));
    }

    [Fact]
    public void EndPaths_empty_no_event()
    {
        var phase = new RegionPhase();
        var w = new World();
        w.Add(1, -100, 300, spawn: (10, 0, 300));
        var lines = new List<string>();
        var (locks, hunts) = phase.Tick(T0, [], w, Doubles(1), lines.Add, lines.Add);
        Assert.Empty(locks);
        Assert.Empty(hunts);
        Assert.Empty(w.Returns);
        Assert.Equal(0, w.PlayerReads);
        Assert.Empty(lines);
        phase.EventEnded("ghost");
        phase.Clear();
        Assert.Empty(phase.Health);
        Assert.Equal(0, phase.Lock.AllRecords + phase.Hunt.AllRecords);
    }
}
