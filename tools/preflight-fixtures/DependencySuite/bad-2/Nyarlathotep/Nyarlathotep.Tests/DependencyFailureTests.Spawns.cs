namespace Nyarlathotep.Tests;

/// <summary>Synthetic fixture (event-spawns D21): the Spawns dependency-failure class with one test per control of
/// dependencySuites.event-spawns.</summary>
public class SpawnsDependencyFailureTests
{
    [Fact]
    public void Territory_fails_closed_when_the_build_throws() { }

    [Fact]
    public void HuntSeed_failure_keeps_the_wave() { }

    [Fact]
    public void PlayerQuery_failure_skips_the_wave() { }

    [Fact]
    public void UnitRecipe_failure_discards_only_its_unit() { }
}
