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
        public bool IsFree(float x, float z) { Calls++; return !blocked(x, z); }
        public bool IsGrounded(float x, float z) { Calls++; return true; }
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
        Assert.Equal(1, blocked.Calls);
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
