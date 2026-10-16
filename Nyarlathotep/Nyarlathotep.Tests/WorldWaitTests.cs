using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>Epic A42: init waits for the world data a brand-new world creates after its startup trigger.</summary>
public class WorldWaitTests
{
    [Fact]
    public void World_data_present_initialises_at_once()
    {
        Assert.Equal(WorldWaitStep.Init, WorldWait.Decide(dayNight: true, regions: true, waitedSeconds: 0));
        Assert.Equal(WorldWaitStep.Init, WorldWait.Decide(true, true, 500));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Missing_world_data_waits_up_to_the_limit_then_initialises_without_it(bool dayNight, bool regions)
    {
        Assert.Equal(WorldWaitStep.Wait, WorldWait.Decide(dayNight, regions, 0));
        Assert.Equal(WorldWaitStep.Wait, WorldWait.Decide(dayNight, regions, WorldWait.LimitSeconds - 0.5));
        Assert.Equal(WorldWaitStep.InitWithout, WorldWait.Decide(dayNight, regions, WorldWait.LimitSeconds));
    }

    [Fact]
    public void The_missing_line_names_each_missing_part()
    {
        Assert.Equal("the day/night cycle, the region polygons", WorldWait.Missing(false, false));
        Assert.Equal("the region polygons", WorldWait.Missing(true, false));
        Assert.Equal("the day/night cycle", WorldWait.Missing(false, true));
        Assert.Equal("", WorldWait.Missing(true, true));
    }
}
