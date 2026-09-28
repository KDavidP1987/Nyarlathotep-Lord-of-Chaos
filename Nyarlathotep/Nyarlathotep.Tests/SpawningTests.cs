using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>walkable-spawns D2 (SpawnPoints, the walkable-point search) and D3 (WavePoints, a wave's points under the
/// per-tick budget).</summary>
public class SpawningTests
{
    const float Eps = 1e-3f;

    static bool Near((float X, float Z) a, (float X, float Z) b) => MathF.Abs(a.X - b.X) < Eps && MathF.Abs(a.Z - b.Z) < Eps;

    static float Dist((float X, float Z) a, (float X, float Z) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>A probe over a set of blocked or not-grounded predicates, counting its calls; it can throw from a call on.</summary>
    sealed class FakeProbe(Func<float, float, bool>? blocked = null, Func<float, float, bool>? floating = null) : IWalkProbe
    {
        public int Calls;
        public int ThrowFrom = int.MaxValue;
        public List<(float X, float Z)> Checked { get; } = [];

        public bool IsFree(float x, float z)
        {
            if (++Calls >= ThrowFrom) throw new InvalidOperationException("tile world gone");
            Checked.Add((x, z));
            return !(blocked?.Invoke(x, z) ?? false);
        }

        public bool IsGrounded(float x, float z)
        {
            if (++Calls >= ThrowFrom) throw new InvalidOperationException("tile world gone");
            return !(floating?.Invoke(x, z) ?? false);
        }
    }

    static List<(float X, float Z)> Ring(int n, float radius, (float X, float Z) c) =>
        Enumerable.Range(0, n).Select(i => (c.X + radius * MathF.Cos(i * 2 * MathF.PI / n), c.Z + radius * MathF.Sin(i * 2 * MathF.PI / n))).ToList();

    // ---- D2 SpawnPoints

    [Fact]
    public void SpawnPoints_passes_free_ring_point_kept()
    {
        var calls = 0;
        var p = SpawnPoints.Choose((10, 0), (0, 0), 10, (_, _) => { calls++; return true; });
        Assert.Equal(new PlacedPoint(10, 0, PointKind.Ring), p);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SpawnPoints_passes_search_order()
    {
        var tried = new List<(float X, float Z)>();
        var centre = (X: 5f, Z: -3f);
        var point = (X: 5f, Z: 7f);                                            // at 90°, radius 10
        SpawnPoints.Choose(point, centre, 10, (x, z) => { tried.Add((x, z)); return false; });

        Assert.Equal(SpawnPoints.MaxChecks, tried.Count);
        Assert.Equal(25, tried.Count);
        Assert.True(Near(point, tried[0]));
        for (var k = 1; k < 12; k++)                                           // the 11 other angles on the ring
        {
            var a = Math.PI / 2 + k * 2 * Math.PI / 12;
            Assert.True(Near(((float)(centre.X + 10 * Math.Cos(a)), (float)(centre.Z + 10 * Math.Sin(a))), tried[k]), $"ring angle {k}");
        }
        for (var k = 0; k < 12; k++)                                           // the 12 angles at half the radius
        {
            var a = Math.PI / 2 + k * 2 * Math.PI / 12;
            Assert.True(Near(((float)(centre.X + 5 * Math.Cos(a)), (float)(centre.Z + 5 * Math.Sin(a))), tried[12 + k]), $"half angle {k}");
        }
        Assert.True(Near(centre, tried[24]));
        Assert.All(tried, t => Assert.True(Dist(t, centre) <= 10 + Eps, "a point outside the radius was tried"));
    }

    [Fact]
    public void SpawnPoints_passes_first_free_in_order()
    {
        var calls = 0;
        var p = SpawnPoints.Choose((10, 0), (0, 0), 10, (_, _) => ++calls == 3);
        Assert.Equal(PointKind.Moved, p.Kind);
        var a = 2 * 2 * Math.PI / 12;                                          // the third try: the ring's second other angle
        Assert.True(Near(((float)(10 * Math.Cos(a)), (float)(10 * Math.Sin(a))), (p.X, p.Z)));
        Assert.Equal(3, calls);
    }

    [Fact]
    public void SpawnPoints_passes_centre_when_only_centre_free()
    {
        var p = SpawnPoints.Choose((10, 0), (1, 2), 10, (x, z) => Near((x, z), (1, 2)));
        Assert.Equal(new PlacedPoint(1, 2, PointKind.Centre), p);
    }

    [Fact]
    public void SpawnPoints_fails_when_all_blocked()
    {
        var calls = 0;
        var p = SpawnPoints.Choose((10, 0), (1, 2), 10, (_, _) => { calls++; return false; });
        Assert.Equal(new PlacedPoint(1, 2, PointKind.Unchecked), p);
        Assert.Equal(25, calls);
    }

    [Fact]
    public void SpawnPoints_fails_when_blocked_point_kept()
    {
        var p = SpawnPoints.Choose((10, 0), (0, 0), 10, (x, z) => !Near((x, z), (10, 0)));
        Assert.NotEqual(PointKind.Ring, p.Kind);
        Assert.False(Near((p.X, p.Z), (10, 0)));
    }

    [Theory]
    [InlineData(true, PointKind.Centre)]
    [InlineData(false, PointKind.Unchecked)]
    public void SpawnPoints_empty_radius_zero(bool free, PointKind kind)
    {
        var calls = 0;
        var p = SpawnPoints.Choose((3, 4), (3, 4), 0, (_, _) => { calls++; return free; });
        Assert.Equal(new PlacedPoint(3, 4, kind), p);
        Assert.Equal(1, calls);
    }

    // ---- D3 WavePoints

    [Fact]
    public void WavePoints_passes_one_point_per_unit_in_order()
    {
        var ring = Ring(8, 10, (0, 0));
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(new FakeProbe(), new WalkBudget()));
        Assert.Equal(ring.Count, points.Count);
        for (var i = 0; i < ring.Count; i++) Assert.Equal(new PlacedPoint(ring[i].X, ring[i].Z, PointKind.Ring), points[i]);
    }

    [Fact]
    public void WavePoints_passes_blocked_ring_point_moved()
    {
        var ring = Ring(4, 10, (0, 0));
        var water = ring[1];
        var probe = new FakeProbe(blocked: (x, z) => Dist((x, z), water) < 3);
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(probe, new WalkBudget()));
        Assert.Equal(4, points.Count);
        Assert.Equal(PointKind.Moved, points[1].Kind);
        Assert.True(Dist((points[1].X, points[1].Z), water) >= 3);
        Assert.Equal(PointKind.Ring, points[0].Kind);
        Assert.Equal(PointKind.Ring, points[2].Kind);
    }

    [Fact]
    public void WavePoints_fails_when_not_grounded()
    {
        var ring = Ring(4, 10, (0, 0));
        var cliffTop = ring[2];
        var probe = new FakeProbe(floating: (x, z) => Near((x, z), cliffTop));
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(probe, new WalkBudget()));
        Assert.Equal(PointKind.Moved, points[2].Kind);
        Assert.False(Near((points[2].X, points[2].Z), cliffTop));
    }

    [Fact]
    public void WavePoints_fails_when_budget_spent()
    {
        var ring = Ring(5, 10, (0, 0));
        var probe = new FakeProbe();
        var budget = new WalkBudget(6);                                        // three checks of two calls
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(probe, budget));

        Assert.Equal(5, points.Count);
        Assert.Equal(6, probe.Calls);
        Assert.Equal(0, budget.Left);
        for (var i = 0; i < 3; i++) Assert.Equal(PointKind.Ring, points[i].Kind);
        for (var i = 3; i < 5; i++) Assert.Equal(new PlacedPoint(ring[i].X, ring[i].Z, PointKind.Unchecked), points[i]);

        budget.Reset();                                                        // the next tick
        Assert.Equal(6, budget.Left);
    }

    [Fact]
    public void WavePoints_fails_when_budget_spent_mid_search()
    {
        var ring = Ring(2, 10, (0, 0));
        var probe = new FakeProbe(blocked: (_, _) => true);
        var budget = new WalkBudget(10);                                       // ten calls; a blocked point skips its grounded call
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(probe, budget));
        Assert.Equal(2, points.Count);
        Assert.Equal(new PlacedPoint(ring[0].X, ring[0].Z, PointKind.Unchecked), points[0]);   // keeps its ring point
        Assert.Equal(new PlacedPoint(ring[1].X, ring[1].Z, PointKind.Unchecked), points[1]);
        Assert.Equal(10, probe.Calls);                                         // ten free calls, all within the first search
        Assert.Equal(0, budget.Left);
    }

    [Fact]
    public void WavePoints_fails_when_check_throws()
    {
        var ring = Ring(6, 10, (0, 0));
        var probe = new FakeProbe { ThrowFrom = 5 };                           // the third point's first call
        var walk = new WaveWalk(probe, new WalkBudget());
        var points = WavePoints.Plan(ring, (0, 0), 10, walk);

        Assert.Equal(6, points.Count);
        Assert.Equal("tile world gone", walk.Failure);
        Assert.Equal(5, probe.Calls);                                          // nothing after the failure calls the game
        for (var i = 0; i < 2; i++) Assert.Equal(PointKind.Ring, points[i].Kind);
        for (var i = 2; i < 6; i++) Assert.Equal(new PlacedPoint(ring[i].X, ring[i].Z, PointKind.Unchecked), points[i]);
    }

    [Fact]
    public void WavePoints_fails_when_point_is_nan()
    {
        var ring = new List<(float X, float Z)> { (float.NaN, 0), (10, 0) };
        var probe = new FakeProbe();
        var walk = new WaveWalk(probe, new WalkBudget());
        var points = WavePoints.Plan(ring, (0, 0), 10, walk);
        Assert.Equal(2, points.Count);
        Assert.NotNull(walk.Failure);
        Assert.Equal(0, probe.Calls);
        Assert.All(points, p => Assert.Equal(PointKind.Unchecked, p.Kind));
    }

    [Fact]
    public void WavePoints_passes_counts_match_kinds()
    {
        var points = new[]
        {
            new PlacedPoint(0, 0, PointKind.Ring), new PlacedPoint(0, 0, PointKind.Moved), new PlacedPoint(0, 0, PointKind.Moved),
            new PlacedPoint(0, 0, PointKind.Centre), new PlacedPoint(0, 0, PointKind.Unchecked),
        };
        Assert.Equal((3, 1), WavePoints.Counts(points));
        Assert.Equal((0, 0), WavePoints.Counts([]));
    }

    [Fact]
    public void WavePoints_empty_no_probe()
    {
        var ring = Ring(3, 10, (0, 0));
        var budget = new WalkBudget();
        var points = WavePoints.Plan(ring, (0, 0), 10, new WaveWalk(null, budget));
        Assert.Equal(3, points.Count);
        for (var i = 0; i < 3; i++) Assert.Equal(new PlacedPoint(ring[i].X, ring[i].Z, PointKind.Unchecked), points[i]);
        Assert.Equal(WalkBudget.PerTick, budget.Left);
        Assert.Empty(WavePoints.Plan([], (0, 0), 10, new WaveWalk(new FakeProbe(), budget)));
    }
}
