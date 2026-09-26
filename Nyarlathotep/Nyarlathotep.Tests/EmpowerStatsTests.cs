using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D4: the stat map.</summary>
public class EmpowerStatsTests
{
    [Fact]
    public void Each_stat_above_one_gives_MultiplyBaseAdd_of_multiplier_minus_one()
    {
        var m = EmpowerStats.Modifiers(new EmpowerStats(1.3, 1.5, 2.0, 1.15, 3.0));
        Assert.Equal(["PhysicalPower", "SpellPower", "MaxHealth", "PrimaryAttackSpeed", "AbilityAttackSpeed", "MovementSpeed"],
            m.Select(x => x.Stat));
        Assert.All(m, x => Assert.Equal("MultiplyBaseAdd", x.Modification));
        Assert.Equal([0.3, 0.5, 1.0, 0.15, 0.15, 2.0], m.Select(x => Math.Round(x.Value, 6)));
    }

    [Fact]
    public void A_stat_at_one_gives_no_entry()
    {
        Assert.Empty(EmpowerStats.Modifiers(new EmpowerStats()));
        var m = EmpowerStats.Modifiers(new EmpowerStats(AttackSpeed: 1.2));
        Assert.Equal(2, m.Count);
        Assert.Equal(["PrimaryAttackSpeed", "AbilityAttackSpeed"], m.Select(x => x.Stat));
        Assert.Equal("MaxHealth", Assert.Single(EmpowerStats.Modifiers(new EmpowerStats(MaxHealth: 1.01))).Stat);
    }
}
