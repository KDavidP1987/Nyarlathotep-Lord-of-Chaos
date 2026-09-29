using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D30 (A44): "spawns: territory unknown" while any event's latest map build failed, and one entry per
/// open D21 streak and event, in the plan's Failure & observability wording.</summary>
public partial class HealthTests
{
    [Theory]
    [InlineData(null, "spawns: territory unknown")]
    [InlineData(SpawnFailure.UnitSetup, "spawns: unit setup failing (raid)")]
    [InlineData(SpawnFailure.HuntSeed, "spawns: hunt seed failing (raid)")]
    [InlineData(SpawnFailure.PlayerQuery, "spawns: player query failing (raid)")]
    public void Spawns_fails_when_streak_open(SpawnFailure? cls, string entry)
    {
        var health = new SpawnHealth();
        if (cls is { } c)
        {
            Assert.True(health.Failing(c, "raid"));
            Assert.False(health.Failing(c, "raid"));                           // once per streak
        }
        else
        {
            var maps = new TerritoryMaps();
            maps.Failed("raid");
            Assert.True(health.Territory(maps.AnyFailed));
            Assert.False(health.Territory(maps.AnyFailed));
        }
        Assert.Equal([entry], health.Entries);
    }

    [Fact]
    public void Spawns_fails_when_ended_event_keeps_territory_unknown()
    {
        var health = new SpawnHealth();
        var life = new WaveLifecycle(new SpawnLedger(new LedgerLimits(150, 20, 10, 10)), new HuntSeeds(), new TerritoryMaps());
        life.Maps.Built("raid", new HashSet<(int X, int Z)>());
        life.Maps.Failed("raid");                                              // the event's latest build failed
        health.Territory(life.Maps.AnyFailed);
        Assert.Equal(["spawns: territory unknown"], health.Entries);
        life.EventEnded("raid", DateTime.MaxValue);
        health.Territory(life.Maps.AnyFailed);
        Assert.Empty(health.Entries);

        life.Maps.Failed("siege");
        health.Territory(life.Maps.AnyFailed);
        life.Purged();
        health.Territory(life.Maps.AnyFailed);
        Assert.Empty(health.Entries);
    }

    [Fact]
    public void Spawns_passes_recovered_check_clears_entry()
    {
        var health = new SpawnHealth();
        var maps = new TerritoryMaps();
        maps.Failed("raid");
        maps.Failed("siege");
        health.Territory(maps.AnyFailed);
        foreach (var c in Enum.GetValues<SpawnFailure>()) health.Failing(c, "raid");
        Assert.Equal(4, health.Entries.Count);

        maps.Built("raid", new HashSet<(int X, int Z)>());
        health.Territory(maps.AnyFailed);
        Assert.Contains("spawns: territory unknown", health.Entries);          // siege's latest build still failed
        maps.Forget("siege");
        health.Territory(maps.AnyFailed);
        Assert.DoesNotContain("spawns: territory unknown", health.Entries);
        foreach (var c in Enum.GetValues<SpawnFailure>()) health.Recovered(c, "raid");
        Assert.Empty(health.Entries);
        Assert.True(health.Failing(SpawnFailure.HuntSeed, "raid"));            // a new streak opens again
    }

    [Fact]
    public void Spawns_passes_text_equals_table()
    {
        var health = new SpawnHealth();
        health.Failed();
        health.Territory(true);
        health.Failing(SpawnFailure.PlayerQuery, "siege");
        health.Failing(SpawnFailure.UnitSetup, "raid");
        health.Failing(SpawnFailure.HuntSeed, "raid");
        health.Failing(SpawnFailure.HuntSeed, "hunt");
        Assert.Equal(
        [
            "spawns: walk check unavailable",
            "spawns: territory unknown",
            "spawns: unit setup failing (raid)",
            "spawns: hunt seed failing (hunt)",
            "spawns: hunt seed failing (raid)",
            "spawns: player query failing (siege)",
        ], health.Entries);
        Assert.Equal("spawns: territory unknown", SpawnHealth.TerritoryEntry);
    }

    [Fact]
    public void Spawns_empty_no_open_streak()
    {
        var health = new SpawnHealth();
        health.Recovered(SpawnFailure.UnitSetup, "raid");
        Assert.False(health.Territory(new TerritoryMaps().AnyFailed));
        Assert.False(health.TerritoryUnknown);
        Assert.Empty(health.Entries);
    }
}
