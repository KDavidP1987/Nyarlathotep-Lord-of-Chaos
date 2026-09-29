using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>walkable-spawns D6: the walk check's health entry, which HealthMonitor.Degraded adds to the health line, the
/// admin status and the admin login notice.</summary>
public partial class HealthTests
{
    [Fact]
    public void WalkHealth_fails_when_streak_open()
    {
        var health = new SpawnHealth();
        Assert.True(health.Failed());
        Assert.False(health.Failed());
        Assert.Equal(["spawns: walk check unavailable"], health.Entries);
    }

    [Fact]
    public void WalkHealth_passes_recovered_check_clears_entry()
    {
        var health = new SpawnHealth();
        health.Failed();
        health.Answered();
        Assert.Empty(health.Entries);
        Assert.True(health.Failed());                                          // a new streak opens again
    }

    [Fact]
    public void WalkHealth_empty_healthy()
    {
        var health = new SpawnHealth();
        Assert.False(health.Open);
        Assert.Empty(health.Entries);
    }
}
