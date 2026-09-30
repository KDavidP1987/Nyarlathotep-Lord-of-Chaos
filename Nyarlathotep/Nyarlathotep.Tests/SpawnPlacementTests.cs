using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D32 (A7, design §9 D31): a checked spawn point needs a walkable straight line from its reach
/// origin, the picked player of an AroundPlayer group; a blocked line gives its farthest walkable sample.</summary>
public class SpawnPlacementTests
{
    /// <summary>A probe over a not-walkable predicate (blocked for IsFree), counting its calls.</summary>
    sealed class LineProbe(Func<float, float, bool> blocked) : IWalkProbe
    {
        public int Calls;
        public byte Level => 10;
        public bool IsFree(float x, float z, byte level) { Calls++; return !blocked(x, z); }
        public bool IsGrounded(float x, float z, byte level) { Calls++; return true; }
    }

    static readonly (float X, float Z) Player = (0, 0);
    static readonly (float X, float Z) Centre = (30, 0);                        // an AroundPlayer group centre 30 m out
    static readonly WalkReach Reach = new(Player.X, Player.Z, 20);

    static List<(float X, float Z)> Ring(int n) =>
        Enumerable.Range(0, n).Select(i => SpawnLedgerRing(i, n)).ToList();

    static (float X, float Z) SpawnLedgerRing(int i, int n) =>
        (Centre.X + 10 * MathF.Cos(i * 2 * MathF.PI / n), Centre.Z + 10 * MathF.Sin(i * 2 * MathF.PI / n));

    static List<PlacedPoint> Plan(Func<float, float, bool> blocked, WalkReach? reach, out LineProbe probe, WalkBudget? budget = null)
    {
        probe = new LineProbe(blocked);
        return WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(probe, budget ?? new WalkBudget()), null, reach);
    }

    static float Dist((float X, float Z) a, PlacedPoint p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Z - p.Z) * (a.Z - p.Z));

    /// <summary>True when every sample of the line from the player to the point is walkable.</summary>
    static bool Walkable(PlacedPoint p, Func<float, float, bool> blocked) =>
        WalkLine.Samples(Player, (p.X, p.Z)).All(s => !blocked(s.X, s.Z));

    [Fact]
    public void WalkLine_fails_when_point_behind_a_wall_accepted()
    {
        // a wall across every line at x 18-19, nearer than minDist: nothing past it, each unit at its line's farthest sample
        bool Wall(float x, float z) => x is >= 18 and < 19;
        var points = Plan(Wall, Reach, out _);
        Assert.All(points, p => Assert.True(p.X < 18, $"{p} is behind the wall"));
        Assert.All(points, p => Assert.Equal(PointKind.Shortened, p.Kind));
        Assert.All(points, p => Assert.True(Walkable(p, Wall)));
    }

    [Fact]
    public void WalkLine_fails_when_point_across_water_accepted()
    {
        // a pond band x 24-27 between the player and the group: a unit stands on the player's bank or has a dry line
        bool Water(float x, float z) => x is >= 24 and < 27 && MathF.Abs(z) < 60;
        var points = Plan(Water, Reach, out _);
        Assert.All(points, p => Assert.True(Walkable(p, Water), $"{p} has no dry line"));
        Assert.All(points, p => Assert.True(p.X < 24, $"{p} is across the water"));
    }

    [Fact]
    public void WalkLine_fails_when_point_on_enclosed_island_accepted()
    {
        // the group centre stands on an island: walkable within 12 m of it, a moat from 12 to 14 m
        bool Moat(float x, float z)
        {
            var d = MathF.Sqrt((x - Centre.X) * (x - Centre.X) + (z - Centre.Z) * (z - Centre.Z));
            return d is >= 12 and < 14;
        }
        var points = Plan(Moat, Reach, out _);
        Assert.All(points, p => Assert.True(Dist(Centre, p) >= 14, $"{p} is on the island"));
        Assert.All(points, p => Assert.True(Walkable(p, Moat)));
    }

    [Fact]
    public void WalkLine_fails_when_shortened_under_min_dist_or_past_its_block()
    {
        // a wall at x 35-36: lines to points past it stop at about 34 m, which is at least minDist 20
        bool Wall(float x, float z) => x is >= 35 and < 36;
        var points = Plan(Wall, Reach, out _);
        var shortened = points.Where(p => p.Kind == PointKind.Shortened).ToList();
        Assert.NotEmpty(shortened);
        Assert.All(shortened, p => Assert.InRange(Dist(Player, p), 20, 35));
        Assert.All(points, p => Assert.True(Walkable(p, Wall)));
        Assert.Equal(shortened.Count, WavePoints.Shortened(points));
    }

    [Fact]
    public void WalkLine_fails_when_budget_overspent()
    {
        // the budget runs out inside the first unit's line: it and the rest stay unchecked at their ring points, and a
        // part-checked line never gives a Shortened point
        var budget = new WalkBudget(30);
        var points = Plan((_, _) => false, Reach, out var probe, budget);
        Assert.Equal(30, probe.Calls);
        Assert.Equal(Ring(5).Select(r => new PlacedPoint(r.X, r.Z, PointKind.Unchecked)), points);
        bool Wall(float x, float z) => x is >= 18 and < 19;
        var walled = Plan(Wall, Reach, out var walledProbe, new WalkBudget(30));
        Assert.Equal(30, walledProbe.Calls);
        Assert.DoesNotContain(walled, p => p.Kind == PointKind.Shortened);
        // review round 2 F3: the budget ends a line past minDist (about 25 m of a wall at 35 m): still unchecked
        bool Far(float x, float z) => x is >= 35 and < 36;
        var far = Plan(Far, Reach, out var farProbe, new WalkBudget(50));
        Assert.Equal(50, farProbe.Calls);
        Assert.Equal(Ring(5).Select(r => new PlacedPoint(r.X, r.Z, PointKind.Unchecked)), far);
    }

    [Fact]
    public void WalkLine_fails_when_free_answer_reused_in_its_tile()
    {
        // Codex round 1 F3: (10.1, 0) is free, (10.4, 0) in the same half-metre tile is not; a free answer is never reused
        bool Post(float x, float z) => MathF.Abs(x - 10.4f) < 0.05f && MathF.Abs(z) < 0.05f;
        var probe = new LineProbe(Post);
        var walk = new WaveWalk(probe, new WalkBudget());
        var reach = new WalkReach(0, 0, 0);
        var first = WavePoints.Plan([(10.1f, 0)], (10.1f, 0), 0, walk, null, reach);
        var second = WavePoints.Plan([(10.4f, 0)], (10.4f, 0), 0, walk, null, new WalkReach(10.4f, 0, 0));
        Assert.Equal(PointKind.Centre, Assert.Single(first).Kind);
        Assert.Equal(PointKind.Unchecked, Assert.Single(second).Kind);
    }

    [Fact]
    public void WalkLine_fails_when_centre_at_its_origin_unchecked()
    {
        // Codex round 1 F1: a Point wave's reach origin is its centre; a radius-0 wave checks the centre as before
        var free = WavePoints.Plan([(5, 5)], (5, 5), 0, new WaveWalk(new LineProbe((_, _) => false), new WalkBudget()), null, new WalkReach(5, 5, 0));
        Assert.Equal(new PlacedPoint(5, 5, PointKind.Centre), Assert.Single(free));
        var blocked = new LineProbe((_, _) => true);
        var none = WavePoints.Plan([(5, 5)], (5, 5), 0, new WaveWalk(blocked, new WalkBudget()), null, new WalkReach(5, 5, 0));
        Assert.Equal(new PlacedPoint(5, 5, PointKind.Unchecked), Assert.Single(none));
        Assert.Equal(5, blocked.Calls);                                     // A13: the origin's level search, one call per level
    }

    [Fact]
    public void WalkLine_fails_when_out_of_scope_line_end_drops_the_line()
    {
        // review round 2 F1: the scope leaves out a band x 16-25 and a wall stands at x 18 inside it; each in-scope
        // target's line ends out of scope, so it keeps its farthest in-scope sample
        bool Wall(float x, float z) => x is >= 18 and < 19;
        var probe = new LineProbe(Wall);
        var points = WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(probe, new WalkBudget()), (x, _) => x < 16 || x >= 25, Reach);
        Assert.All(points, p => Assert.Equal(PointKind.Shortened, p.Kind));
        Assert.All(points, p => Assert.InRange(p.X, 14, 16));
    }

    [Fact]
    public void WalkLine_fails_when_unchecked_reason_missing_or_wrong()
    {
        // A11: each unchecked point names why; a checked one names nothing
        List<UncheckedReason?> Why(WalkReach? reach, WaveWalk walk)
        {
            var reasons = new List<UncheckedReason?>();
            WavePoints.Plan(Ring(5), Centre, 10, walk, null, reach, reasons);
            return reasons;
        }
        var open = Why(Reach, new WaveWalk(new LineProbe((_, _) => false), new WalkBudget()));
        Assert.Equal(5, open.Count);
        Assert.All(open, r => Assert.Null(r));
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoCheck, 5), Why(Reach, new WaveWalk(null, new WalkBudget())));
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.Budget, 5),
            Why(Reach, new WaveWalk(new LineProbe((_, _) => false), new WalkBudget(30))));
        // every sample blocked, the player's spot too: no ground at the player (A13), searched once
        var boxed = new LineProbe((_, _) => true);
        var walk = new WaveWalk(boxed, new WalkBudget());
        var reasons = Why(Reach, walk);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoGround, 5), reasons);
        Assert.True(walk.OriginSearched);
        Assert.Null(walk.OriginLevel);
        Assert.Equal(": 5 no ground at the player", WavePoints.UncheckedText(reasons.OfType<UncheckedReason>(), walk.PlayerLevel));
        // the player's spot free, everything else blocked: no walkable line, with the player's level
        var fenced = new WaveWalk(new LineProbe((x, z) => x != Player.X || z != Player.Z), new WalkBudget());
        var fencedWhy = Why(Reach, fenced);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoLine, 5), fencedWhy);
        Assert.Equal((byte?)10, fenced.PlayerLevel);
        Assert.Equal(": 5 no walkable line; the player's level 10", WavePoints.UncheckedText(fencedWhy.OfType<UncheckedReason>(), fenced.PlayerLevel));
        // without a reach nothing free is "no free spot", and no level is searched
        var spotWalk = new WaveWalk(new LineProbe((_, _) => true), new WalkBudget());
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoFreeSpot, 5), Why(null, spotWalk));
        Assert.False(spotWalk.OriginSearched);
        Assert.Equal(": 1 no check, 2 budget", WavePoints.UncheckedText([UncheckedReason.Budget, UncheckedReason.NoCheck, UncheckedReason.Budget], null));
        Assert.Equal("", WavePoints.UncheckedText([], 10));
        Assert.Contains("unchecked{WavePoints.UncheckedText(why, decision.Groups.Count == 1 ? playerLevel : null)}), due in", PushTests.WaveActionSource());
        // review F4a: with a reach but every candidate out of scope no line is walked: no free spot
        var scoped = new List<UncheckedReason?>();
        WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(new LineProbe((_, _) => false), new WalkBudget()), (_, _) => false, Reach, scoped);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoFreeSpot, 5), scoped);
        // review F4b: the origin's level is searched once a wave, not per point
        var atSpot = 0;
        var spotProbe = new LineProbe((x, z) => { if (x == Player.X && z == Player.Z) { atSpot++; return false; } return true; });
        var once = new WaveWalk(spotProbe, new WalkBudget());
        WavePoints.Plan(Ring(5), Centre, 10, once, null, Reach);
        Assert.Equal(1, atSpot);                                            // IsFree once at level h; IsGrounded does not use the predicate
        Assert.True(once.NoLine);
        // review F4c: a reach at the wave centre (a Point wave) raises no survey
        var point = new WaveWalk(new LineProbe((_, _) => true), new WalkBudget());
        WavePoints.Plan(Ring(5), Centre, 10, point, null, new WalkReach(Centre.X, Centre.Z, 0));
        Assert.False(point.NoLine);
        // A13 review F1: a Point wave whose centre stands on a prop keeps its lines from the wave's level
        var prop = new List<UncheckedReason?>();
        var propWalk = new WaveWalk(new LineProbe((x, z) => x == Centre.X && z == Centre.Z), new WalkBudget());
        var propPoints = WavePoints.Plan(Ring(5), Centre, 10, propWalk, null, new WalkReach(Centre.X, Centre.Z, 0), prop);
        Assert.Null(propWalk.OriginLevel);                                  // A16: no nearby start for a wave centre
        Assert.Null(propWalk.OriginAt);
        Assert.All(propPoints, p => Assert.Equal(PointKind.Ring, p.Kind));
        Assert.All(prop, r => Assert.Null(r));
        // round 2: a Point wave walled in around its centre gives no player's level to the wave line
        var pointWall = new WaveWalk(new LineProbe((x, z) => x != Centre.X || z != Centre.Z), new WalkBudget());
        var pointWhy = new List<UncheckedReason?>();
        WavePoints.Plan(Ring(5), Centre, 10, pointWall, null, new WalkReach(Centre.X, Centre.Z, 0), pointWhy);
        Assert.Equal((byte?)10, pointWall.OriginLevel);
        Assert.Null(pointWall.PlayerLevel);
        Assert.DoesNotContain("player", WavePoints.UncheckedText(new[] { UncheckedReason.NoLine }, pointWall.PlayerLevel));
        // A13 review F2: every candidate out of scope walks no line and searches no level: no free spot, no survey
        var outside = new WaveWalk(new LineProbe((_, _) => true), new WalkBudget());
        var outsideWhy = new List<UncheckedReason?>();
        WavePoints.Plan(Ring(5), Centre, 10, outside, (_, _) => false, Reach, outsideWhy);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoFreeSpot, 5), outsideWhy);
        Assert.False(outside.OriginSearched);
        Assert.False(outside.NoLine);
        // review F4d: a throwing probe keeps one reason per point, each "no check"
        var thrown = new List<UncheckedReason?>();
        var planned = WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(new ThrowingProbe(), new WalkBudget()), null, Reach, thrown);
        Assert.Equal(planned.Count, thrown.Count);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoCheck, 5), thrown);
    }

    /// <summary>A terrain with a ground height level per spot (A13): a level is grounded only at the spot's own level, free
    /// unless blocked; the wave's level is <paramref name="h"/>.</summary>
    sealed class LevelProbe(Func<float, float, int> ground, byte h = 10, Func<float, float, bool>? blocked = null) : IWalkProbe
    {
        public int Calls;
        public byte Level => h;
        public bool IsFree(float x, float z, byte level) { Calls++; return blocked is null || !blocked(x, z); }
        public bool IsGrounded(float x, float z, byte level) { Calls++; return ground(x, z) == level; }
    }

    static List<PlacedPoint> PlanLevels(LevelProbe probe, WalkBudget? budget = null) =>
        WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(probe, budget ?? new WalkBudget()), null, Reach);

    [Fact]
    public void WalkLine_fails_when_slope_refused()
    {
        // A13: the ground climbs one level every 3 m toward the group: every line is walked whole, level by level
        var slope = new LevelProbe((x, _) => 10 + (int)MathF.Floor(MathF.Max(x, 0) / 3));
        Assert.Equal(Ring(5).Select(r => new PlacedPoint(r.X, r.Z, PointKind.Ring)), PlanLevels(slope));
        // and the budget holds on a slope, the level tries included
        var tight = new LevelProbe((x, _) => 10 + (int)MathF.Floor(MathF.Max(x, 0) / 3));
        PlanLevels(tight, new WalkBudget(40));
        Assert.Equal(40, tight.Calls);
    }

    [Fact]
    public void WalkLine_fails_when_two_level_step_walked()
    {
        // a cliff: level 10 below x 15, level 12 from it; no line climbs it, each unit stays below the cliff
        var cliff = new LevelProbe((x, _) => x < 15 ? 10 : 12);
        var points = PlanLevels(cliff);
        Assert.All(points, p => Assert.True(p.X < 15, $"{p} is above the cliff"));
        Assert.All(points, p => Assert.Equal(PointKind.Shortened, p.Kind));
        // one level up is a slope, walked
        var step = new LevelProbe((x, _) => x < 15 ? 10 : 11);
        Assert.All(PlanLevels(step), p => Assert.Equal(PointKind.Ring, p.Kind));
    }

    [Fact]
    public void WalkLine_fails_when_player_by_an_edge_not_moved()
    {
        // A16: the unit circle at the player's spot touches the water's edge; ground 1 m north starts the lines, whole
        var calls = new Dictionary<(float, float), int>();
        var probe = new LineProbe((x, z) => { calls[(x, z)] = calls.GetValueOrDefault((x, z)) + 1; return x * x + z * z < 0.3f; });
        var walk = new WaveWalk(probe, new WalkBudget());
        var reasons = new List<UncheckedReason?>();
        var points = WavePoints.Plan(Ring(5), Centre, 10, walk, null, Reach, reasons);
        Assert.Equal((0f, 1f), walk.OriginAt);
        Assert.Equal((byte?)10, walk.PlayerLevel);
        Assert.All(points, p => Assert.Equal(PointKind.Ring, p.Kind));
        Assert.All(reasons, r => Assert.Null(r));
        Assert.Equal(5, calls[(0f, 0f)]);                                   // the spot's five levels
        Assert.False(walk.NoLine);
        // a rock beside the player, east of the spot and below z 0.3: lines from the player hit it, lines from the start clear it
        var rock = new WaveWalk(new LineProbe((x, z) => x * x + z * z < 0.3f || x is > 0.6f and < 1.4f && z is > -3 and < 0.3f), new WalkBudget());
        var rockPoints = WavePoints.Plan(Ring(5), Centre, 10, rock, null, Reach);
        Assert.Equal((0f, 1f), rock.OriginAt);
        Assert.All(rockPoints, p => Assert.Equal(PointKind.Ring, p.Kind));
        // nothing walkable within 3 m: at most 5 + 24 x 5 blocked calls for the search
        var near = 0;
        var boxed = new LineProbe((x, z) => { var inBox = x * x + z * z < 12.5f; if (inBox) near++; return inBox; });
        var boxedWalk = new WaveWalk(boxed, new WalkBudget());
        WavePoints.Plan(Ring(5), Centre, 10, boxedWalk, null, Reach);
        Assert.True(boxedWalk.OriginSearched);
        Assert.Null(boxedWalk.OriginAt);
        Assert.Equal(5 * (1 + 8 * WalkOrigin.Reach), near);
        // an open field keeps the player's own spot and its calls (4.2)
        var openCalls = new Dictionary<(float, float), int>();
        var open = new WaveWalk(new LineProbe((x, z) => { if (x * x + z * z < 12.5f) openCalls[(x, z)] = openCalls.GetValueOrDefault((x, z)) + 1; return false; }), new WalkBudget());
        WavePoints.Plan(Ring(5), Centre, 10, open, null, Reach);
        Assert.Equal((0f, 0f), open.OriginAt);
        Assert.Equal(1, openCalls[(0f, 0f)]);                               // one free call at h, no other level
        // every line runs east to the ring; a nearby start (N, S, W, …) tried on open ground would probe x <= 0
        Assert.All(openCalls.Keys, k => Assert.True(k == (0f, 0f) || k.Item1 > 0, "a nearby start tried on open ground"));
    }

    [Fact]
    public void WalkLine_fails_when_bridge_deck_spawns_in_water()
    {
        // A16: the player on a bridge deck over water to x 25: no start within 3 m, so each point is checked alone, never in the water
        var walk = new WaveWalk(new LineProbe((x, _) => x < 25), new WalkBudget());
        var reasons = new List<UncheckedReason?>();
        var points = WavePoints.Plan(Ring(5), Centre, 10, walk, null, Reach, reasons);
        Assert.Null(walk.OriginLevel);
        Assert.All(points, p => Assert.True(p.X >= 25, $"{p.Kind} in the water"));
        Assert.All(points, p => Assert.Equal(PointKind.SpotOnly, p.Kind));
        Assert.All(reasons, r => Assert.Null(r));
        Assert.True(walk.NoLine);                                           // the survey still runs
        Assert.Equal(5, WavePoints.SpotOnly(points));
        Assert.Equal(", 5 spot only (no ground at the player)", WavePoints.SpotOnlyText(5));
        Assert.Equal("", WavePoints.SpotOnlyText(0));
        Assert.Contains("({moved} moved, {shortened} shortened{WavePoints.SpotOnlyText(spotOnly)}, {unchecked_} unchecked", PushTests.WaveActionSource());
        // a spot the check also refuses stays "no ground at the player"
        var all = new List<UncheckedReason?>();
        WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(new LineProbe((_, _) => true), new WalkBudget()), null, Reach, all);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoGround, 5), all);
        // spot only uses h, h-1 or h+1 and no line: ground at level 9 beyond the deck is taken, level 12 is not
        var low = WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(new LevelProbe((x, _) => x < 25 ? 14 : 9, 10), new WalkBudget()), null, Reach);
        Assert.All(low, p => Assert.Equal(PointKind.SpotOnly, p.Kind));
        var high = WavePoints.Plan(Ring(5), Centre, 10, new WaveWalk(new LevelProbe((x, _) => x < 25 ? 14 : 12, 10), new WalkBudget()), null, Reach);
        Assert.All(high, p => Assert.Equal(PointKind.Unchecked, p.Kind));
    }

    [Fact]
    public void WalkLine_fails_when_player_off_level_not_found()
    {
        // the player stands on flat ground at level 12 while the wave's level is 10: the level is found, the ring kept
        foreach (var at in new[] { 8, 9, 11, 12 })
        {
            var flat = new LevelProbe((_, _) => at, 10);
            var walk = new WaveWalk(flat, new WalkBudget());
            var points = WavePoints.Plan(Ring(5), Centre, 10, walk, null, Reach);
            Assert.Equal((byte?)at, walk.OriginLevel);
            Assert.All(points, p => Assert.Equal(PointKind.Ring, p.Kind));
        }
        // three levels off is out of the search: no ground at the player
        var far = new WaveWalk(new LevelProbe((_, _) => 13, 10), new WalkBudget());
        var reasons = new List<UncheckedReason?>();
        WavePoints.Plan(Ring(5), Centre, 10, far, null, Reach, reasons);
        Assert.Equal(Enumerable.Repeat<UncheckedReason?>(UncheckedReason.NoGround, 5), reasons);
        Assert.Equal(new byte[] { 10, 9, 11, 8, 12 }, WalkLevels.Near(10));
        Assert.Equal(new byte[] { 0, 1, 2 }, WalkLevels.Near(0));
        Assert.Equal(new byte[] { 10, 9, 11 }, WalkLevels.Spot(10));          // A16 review: spot only never h±2, at the edges too
        Assert.Equal(new byte[] { 0, 1 }, WalkLevels.Spot(0));
        Assert.Equal(new byte[] { 255, 254 }, WalkLevels.Spot(255));
    }

    [Fact]
    public void WalkSurvey_fails_when_level_not_followed()
    {
        // A13: north the ground climbs a level every 3 m, south a two-level cliff at 4 m; the player stands at level 11
        var probe = new LevelProbe((_, z) => z >= 0 ? 11 + (int)MathF.Floor(z / 3) : z > -3.5f ? 11 : 13, 10);
        var line = WalkSurvey.Line("au-here", 1, (0, 0), new WaveWalk(probe, new WalkBudget(0)), new WalkBudget(5000));
        Assert.StartsWith("walk survey au-here wave 1: spot level 11; N 30 m clear @21; ", line);
        Assert.Contains("; S 3 m not grounded @11; ", line);
        var none = WalkSurvey.Line("au-here", 1, (0, 0), new WaveWalk(new LevelProbe((_, _) => 14, 10), new WalkBudget(0)), new WalkBudget(5000));
        Assert.StartsWith("walk survey au-here wave 1: spot no level within 3 m (the player's spot: 10 not grounded, 9 not grounded, "
            + "11 not grounded, 8 not grounded, 12 not grounded); N 0 m not grounded @10", none);
    }

    sealed class ThrowingProbe : IWalkProbe
    {
        public byte Level => 10;
        public bool IsFree(float x, float z, byte level) => throw new InvalidOperationException("native");
        public bool IsGrounded(float x, float z, byte level) => throw new InvalidOperationException("native");
    }

    /// <summary>A survey probe: blocked and ungrounded predicates, counting its calls.</summary>
    sealed class SurveyProbe(Func<float, float, bool> blocked, Func<float, float, bool> ungrounded) : IWalkProbe
    {
        public int Calls;
        public byte Level => 10;
        public bool IsFree(float x, float z, byte level) { Calls++; return !blocked(x, z); }
        public bool IsGrounded(float x, float z, byte level) { Calls++; return !ungrounded(x, z); }
    }

    static string Survey(Func<float, float, bool> blocked, Func<float, float, bool> ungrounded, (float X, float Z) at, out SurveyProbe probe,
        int budget = WalkBudget.PerTick)
    {
        probe = new SurveyProbe(blocked, ungrounded);
        return WalkSurvey.Line("au-here", 1, at, new WaveWalk(probe, new WalkBudget(0)), new WalkBudget(budget));
    }

    [Fact]
    public void WalkSurvey_fails_when_distance_past_first_failing_sample()
    {
        // a wall from z 5.5 (N stops after 5 m), no ground from x 3.5 (E after 3 m; NE's fifth sample at x 3.54)
        var line = Survey((_, z) => z >= 5.5f, (x, _) => x >= 3.5f, (0, 0), out _);
        Assert.StartsWith("walk survey au-here wave 1: spot level 10; ", line);
        Assert.Contains("; N 5 m blocked @10; NE 4 m not grounded @10; E 3 m not grounded @10; ", line);
        Assert.Contains("; S 30 m clear @10; ", line);
        Assert.EndsWith("; NW 7 m blocked @10", line);
    }

    [Fact]
    public void WalkSurvey_fails_when_wrong_reason()
    {
        var line = Survey((_, _) => true, (_, _) => true, (0, 0), out _);
        Assert.Contains("spot no level within 3 m (the player's spot: 10 blocked, 9 blocked, 11 blocked, 8 blocked, 12 blocked); "
            + "N 0 m blocked @10; NE 0 m blocked @10", line);
        var water = Survey((_, _) => false, (x, z) => x * x + z * z > 0.25f, (0, 0), out _);
        Assert.Contains("spot level 10; N 0 m not grounded @10", water);
    }

    [Fact]
    public void WalkSurvey_fails_when_budget_overspent()
    {
        // two calls for the spot, then four samples of N at two calls each: the eleventh call is refused
        var line = Survey((_, _) => false, (_, _) => false, (0, 0), out var probe, 10);
        Assert.Equal(10, probe.Calls);
        Assert.Equal("walk survey au-here wave 1: spot level 10; N budget", line);
        Assert.Equal("walk survey au-here wave 1: budget", Survey((_, _) => false, (_, _) => false, (0, 0), out var none, 1));
        Assert.Equal(1, none.Calls);
        // review F2: by default the survey has its own budget and never takes the wave's
        var wave = new WalkBudget(5);
        var own = new SurveyProbe((_, _) => false, (_, _) => false);
        WalkSurvey.Line("au-here", 1, (0, 0), new WaveWalk(own, wave));
        Assert.Equal(5, wave.Left);
        Assert.Equal(2 + 8 * 30 * 2, own.Calls);
        // A13 review F4: a player one level off h on open ground is surveyed whole within the survey's budget
        var off = new LevelProbe((_, _) => 9, 10);
        var offLine = WalkSurvey.Line("au-here", 1, (0, 0), new WaveWalk(off, new WalkBudget(0)));
        Assert.EndsWith("NW 30 m clear @9", offLine);
        Assert.True(off.Calls <= WalkSurvey.Calls);
    }

    [Fact]
    public void WalkSurvey_fails_when_moved_start_not_shown()
    {
        // A16: the unit circle at the player's spot touches an edge; the survey starts 1 m north and names the spot's answers
        var line = Survey((x, z) => x * x + z * z < 0.3f, (_, _) => false, (0, 0), out _);
        Assert.StartsWith("walk survey au-here wave 1: spot moved 1 m N, level 10 (the player's spot: 10 blocked, 9 blocked, 11 blocked, "
            + "8 blocked, 12 blocked); N 30 m clear @10; ", line);
        Assert.Contains("; S 0 m blocked @10; ", line);                    // from the moved start, back across the player's spot
        Assert.True(WalkSurvey.Calls >= 5 * 2 * 25 + 8 * 30 * 2);          // the start's search and an open field
    }

    [Fact]
    public void WalkSurvey_fails_when_line_holds_a_coordinate()
    {
        var line = Survey((_, z) => z >= -8759.8f, (_, _) => false, (4321.7f, -8765.3f), out _);
        Assert.DoesNotMatch(@"\d{3,}", line);                             // no coordinate, rounded or not
        Assert.Contains("N 5 m blocked", line);
    }

    [Fact]
    public void WalkSurvey_passes_open_field_clear()
    {
        var line = Survey((_, _) => false, (_, _) => false, (0, 0), out var probe);
        Assert.Equal("walk survey au-here wave 1: spot level 10; " + string.Join("; ",
            new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }.Select(n => $"{n} 30 m clear @10")), line);
        Assert.Equal(2 + 8 * 30 * 2, probe.Calls);
    }

    [Fact]
    public void WalkSurvey_empty_no_check()
    {
        Assert.Equal("walk survey au-here wave 1: no check", WalkSurvey.Line("au-here", 1, (0, 0), new WaveWalk(null, new WalkBudget())));
        // review F1: a wave whose check failed is not called again
        var failed = new WaveWalk(new SurveyProbe((_, _) => false, (_, _) => false), new WalkBudget());
        failed.Fail("native");
        Assert.Equal("walk survey au-here wave 1: no check", WalkSurvey.Line("au-here", 1, (0, 0), failed));
        Assert.Equal(0, ((SurveyProbe)failed.Probe!).Calls);
        // the wave calls it only after a line found nothing, with VerboseLogging on, before the probe's dispose
        var source = PushTests.WaveActionSource();
        Assert.Contains("if (Settings.VerboseLogging.Value && check.Walk.NoLine && !surveyed && reach is { } r)", source);
        Assert.Contains("surveyed = true;", source);                        // A12: one survey a wave, 482 calls at most
        Assert.True(source.IndexOf("var surveyed = false;", StringComparison.Ordinal) is var at and >= 0
            && at < source.IndexOf("WaveRun.Run(", StringComparison.Ordinal));      // declared once a wave, outside the group callback (Codex round 3)
        Assert.True(source.IndexOf("WalkSurvey.Line(", StringComparison.Ordinal) < source.IndexOf("check.Resource?.Dispose()", StringComparison.Ordinal));
    }

    [Fact]
    public void WalkLine_passes_planning_split_timed()
    {
        // A14: with TimingLog the wave line splits its planning into open, plan and survey, in invariant milliseconds
        Assert.Equal(" (open 1.5 ms, plan 230.0 ms, survey 0.0 ms)", WavePoints.TimingText(15, 2300, 0, 10_000));
        var source = PushTests.WaveActionSource();
        Assert.Contains("{(Settings.TimingLog.Value ? WavePoints.TimingText(open, plan, survey, Stopwatch.Frequency) : \"\")}", source);
        Assert.True(source.IndexOf("open += t1 - t0;", StringComparison.Ordinal) < source.IndexOf("plan += t2 - t1;", StringComparison.Ordinal));
        Assert.Contains("open += Stopwatch.GetTimestamp() - t3;", source);  // the dispose counts as open
        Assert.Contains("playerLevel ??= check.Walk.PlayerLevel;", source);  // round 2: never a Point centre's level
        // A13 review F5: the game probe answers every level itself; nothing falls back to a default
        var walkCheck = File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(),
            "Nyarlathotep", "Nyarlathotep", "Services", "WalkCheck.cs"));
        Assert.Contains("public byte Level => _level;", walkCheck);
        Assert.Contains("public bool IsFree(float x, float z, byte level) =>", walkCheck);
        Assert.Contains("public bool IsGrounded(float x, float z, byte level)", walkCheck);
        Assert.All(typeof(IWalkProbe).GetMethods(), m => Assert.True(m.IsAbstract, m.Name));   // Codex round 2: no default bodies
    }

    [Fact]
    public void WalkLine_passes_open_field_unchanged()
    {
        var points = Plan((_, _) => false, Reach, out var probe);
        Assert.Equal(Ring(5).Select(r => new PlacedPoint(r.X, r.Z, PointKind.Ring)), points);
        // the lines share their tiles near the player: the grounded answers are reused, the circle answers are not
        var samples = Ring(5).Sum(r => WalkLine.Samples(Player, r).Count());
        Assert.True(probe.Calls < 2 * samples, $"{probe.Calls} calls for {samples} samples");
        Assert.True(probe.Calls >= samples);
    }

    [Fact]
    public void WalkLine_passes_reach_origin_per_location()
    {
        var around = new Location(LocationType.AroundPlayer, 0, 0, null, 20, 40);
        Assert.Equal(new WalkReach(1, 2, 20), WavePlan.Reach(around, (30, 0), (1, 2)));
        Assert.Null(WavePlan.Reach(around, (30, 0), null));
        Assert.Equal(new WalkReach(30, 5, 0), WavePlan.Reach(new Location(LocationType.Point, 30, 5), (30, 5), (1, 2)));
        // the pick's origins never print (review F6): a record's ToString shows the list's type, no coordinate
        var pick = new FanOutPick(PickOutcome.Picked, [(1, 2, 3)]) { Origins = [(4321.7f, -8765.3f)] };
        Assert.DoesNotContain("4321", pick.ToString());
        // WaveAction passes the reach of each group (A7)
        var source = PushTests.WaveActionSource();
        Assert.Contains("WavePlan.Reach(location, (gx, gz), group.Index < origins.Count ? origins[group.Index] : null)", source);
        Assert.Contains("action.Loot, hunt, reach);", source);
    }

    [Fact]
    public void WalkLine_empty_reach_checks_only_the_spot()
    {
        // without a reach the planner checks the spot alone, two calls per point, as before A7
        bool Wall(float x, float z) => x is >= 18 and < 19;
        var points = Plan(Wall, null, out var probe);
        Assert.Equal(Ring(5).Select(r => new PlacedPoint(r.X, r.Z, PointKind.Ring)), points);
        Assert.Equal(10, probe.Calls);
        Assert.Empty(WalkLine.Samples(Player, Player));
    }
}
