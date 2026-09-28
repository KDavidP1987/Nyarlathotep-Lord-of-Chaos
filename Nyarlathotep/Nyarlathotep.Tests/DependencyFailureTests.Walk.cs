using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>walkable-spawns D5: a walk check that throws, has no tile world or gets a height outside the map's range falls
/// open for the rest of its wave, logs once per failure streak, and the next wave checks again.</summary>
public partial class DependencyFailureTests
{
    sealed class WalkProbe(bool throws) : IWalkProbe
    {
        public int Calls;
        public bool IsFree(float x, float z) { Calls++; return throws ? throw new InvalidOperationException("singleton: none") : true; }
        public bool IsGrounded(float x, float z) { Calls++; return true; }
    }

    static readonly List<(float X, float Z)> WalkRing = [(10, 0), (0, 10), (-10, 0), (0, -10)];

    static (List<PlacedPoint> Points, WaveWalk Walk) PlanWave(IWalkProbe? probe, SpawnHealth health, LogLines log)
    {
        var walk = new WaveWalk(probe, new WalkBudget());
        var points = WavePoints.Plan(WalkRing, (0, 0), 10, walk);
        health.Settle(walk, log.Add);
        return (points, walk);
    }

    static void WalkCheckFault()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        var probe = new WalkProbe(throws: true);
        var (points, _) = PlanWave(probe, health, log);
        Assert.Equal(WalkRing.Count, points.Count);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(["walk check unavailable: singleton: none"], log.Lines);
        Assert.Equal([SpawnHealth.Entry], health.Entries);
    }

    [Fact]
    public void WalkCheck_fails_when_check_throws()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        var probe = new WalkProbe(throws: true);
        var (points, walk) = PlanWave(probe, health, log);

        Assert.Equal(WalkRing.Count, points.Count);                            // the wave goes on, no unit dropped
        for (var i = 0; i < WalkRing.Count; i++) Assert.Equal(new PlacedPoint(WalkRing[i].X, WalkRing[i].Z, PointKind.Unchecked), points[i]);
        Assert.Equal(1, probe.Calls);                                          // the rest of the wave calls nothing
        Assert.Equal("singleton: none", walk.Failure);
        Assert.Equal(["walk check unavailable: singleton: none"], log.Lines);
        Assert.True(health.Open);
    }

    [Fact]
    public void WalkCheck_fails_when_streak_repeats()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        for (var wave = 0; wave < 3; wave++) PlanWave(new WalkProbe(throws: true), health, log);
        Assert.Single(log.Lines);
        Assert.Equal([SpawnHealth.Entry], health.Entries);
    }

    [Fact]
    public void WalkCheck_passes_recovered_check_used_again()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        PlanWave(new WalkProbe(throws: true), health, log);
        var good = new WalkProbe(throws: false);
        var (points, walk) = PlanWave(good, health, log);

        Assert.Null(walk.Failure);
        Assert.Equal(2 * WalkRing.Count, good.Calls);                          // every point checked again
        Assert.All(points, p => Assert.Equal(PointKind.Ring, p.Kind));
        Assert.False(health.Open);
        Assert.Empty(health.Entries);

        PlanWave(new WalkProbe(throws: true), health, log);                    // a new streak logs again
        Assert.Equal(2, log.Count("walk check unavailable"));
    }

    [Fact]
    public void WalkCheck_fails_when_unanswered_wave_closes_streak()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        PlanWave(new WalkProbe(throws: true), health, log);
        var spent = new WaveWalk(new WalkProbe(throws: false), new WalkBudget(0));    // the tick's budget already spent
        WavePoints.Plan(WalkRing, (0, 0), 10, spent);
        health.Settle(spent, log.Add);
        var empty = new WaveWalk(new WalkProbe(throws: false), new WalkBudget());     // a wave with no point
        WavePoints.Plan([], (0, 0), 10, empty);
        health.Settle(empty, log.Add);
        Assert.True(health.Open);                                              // nothing answered: the streak stays open
        Assert.Single(log.Lines);
    }

    [Fact]
    public void WalkCheck_fails_when_tile_world_missing()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        var walk = new WaveWalk(null, new WalkBudget(0));                      // as WalkCheck.OpenWave: no probe, failure recorded
        walk.Fail("singleton: none");
        var points = WavePoints.Plan(WalkRing, (0, 0), 10, walk);
        health.Settle(walk, log.Add);
        Assert.Equal(WalkRing.Count, points.Count);
        Assert.All(points, p => Assert.Equal(PointKind.Unchecked, p.Kind));
        Assert.Equal(["walk check unavailable: singleton: none"], log.Lines);
    }

    [Theory]
    [InlineData(float.NaN, "height is not a number")]
    [InlineData(float.PositiveInfinity, "height is not a number")]
    [InlineData(-0.5f, "height out of range")]
    [InlineData(WalkHeight.MaxY, "height out of range")]
    public void WalkCheck_fails_when_height_out_of_range(float y, string problem) => Assert.Equal(problem, WalkHeight.Problem(y));

    [Theory]
    [InlineData(0f)]
    [InlineData(52.3f)]
    [InlineData(999.9f)]
    public void WalkCheck_passes_height_in_range(float y) => Assert.Null(WalkHeight.Problem(y));

    [Fact]
    public void WalkCheck_empty_no_height()
    {
        var (health, log) = (new SpawnHealth(), new LogLines());
        var (points, walk) = PlanWave(null, health, log);                      // a Point stored without a height (A13)
        Assert.All(points, p => Assert.Equal(PointKind.Unchecked, p.Kind));
        Assert.Null(walk.Failure);
        Assert.Empty(log.Lines);
        Assert.False(health.Open);

        PlanWave(new WalkProbe(throws: true), health, log);
        PlanWave(null, health, log);                                           // an unchecked wave leaves the streak open
        Assert.True(health.Open);
    }
}
