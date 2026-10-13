using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D13: a V Blood kill needs VBloodConsumeSource.</summary>
public class DeathRuleTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]       // a gate boss: VBloodUnit alone
    [InlineData(false, false, false)]
    public void Only_a_consume_source_makes_a_V_Blood_kill(bool consume, bool vbloodUnit, bool expected) =>
        Assert.Equal(expected, DeathRule.IsVBloodKill(consume, vbloodUnit));

    /// <summary>A player character carries VBloodConsumeSource as well; its death is never a V Blood kill, so an "any"
    /// VBloodKilled trigger never fires on a player's death.</summary>
    [Fact]
    public void Fails_when_a_players_death_counts_as_a_V_Blood_kill() =>
        Assert.False(DeathRule.IsVBloodKill(hasConsumeSource: true, hasVBloodUnit: false, isPlayer: true));
}
