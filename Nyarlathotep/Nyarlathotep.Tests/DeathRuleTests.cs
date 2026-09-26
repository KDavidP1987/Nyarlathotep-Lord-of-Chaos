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
}
