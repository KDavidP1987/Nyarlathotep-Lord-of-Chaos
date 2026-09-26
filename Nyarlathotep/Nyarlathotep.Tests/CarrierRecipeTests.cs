using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D4: every value the service writes on a new carrier.</summary>
public class CarrierRecipeTests
{
    [Fact]
    public void The_recipe_holds_every_written_value()
    {
        var stats = new EmpowerStats(PhysicalPower: 1.3, MoveSpeed: 1.1);
        var r = CarrierRecipe.For(stats, 42.75);
        Assert.Equal("Replace", r.BuffType);
        Assert.Equal(1, r.MaxStacks);
        Assert.False(r.IncreaseStacks);
        Assert.Equal(Markers.Carrier, r.SpellLevel);
        Assert.Equal(MarkerKind.Carrier, Markers.KindOf(r.SpellLevel));
        Assert.Equal(42.75f, r.LifeTimeSeconds);
        Assert.Equal("Destroy", r.EndAction);
        Assert.Equal(["CreateGameplayEventsOnSpawn", "GameplayEventListeners", "RemoveBuffOnGameplayEvent",
            "RemoveBuffOnGameplayEventEntry", "DestroyOnGameplayEvent"], r.Strip);
        Assert.Equal(EmpowerStats.Modifiers(stats), r.Modifiers);
    }
}
