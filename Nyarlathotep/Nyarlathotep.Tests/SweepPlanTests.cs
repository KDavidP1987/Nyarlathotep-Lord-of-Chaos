using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D7: the boot sweep's rows split into units to despawn and carriers to remove.</summary>
public class SweepPlanTests
{
    [Fact]
    public void Unit_rows_despawn_their_unit_and_carrier_rows_remove_only_the_buff()
    {
        var plan = SweepPlan.From(
        [
            new MarkerRow(Markers.Unit, Buff: 11, Target: 1),
            new MarkerRow(Markers.Carrier, Buff: 22, Target: 2),
            new MarkerRow(Markers.Unit, Buff: 12, Target: 1),       // a second marker on the same unit
            new MarkerRow(Markers.Carrier, Buff: 23, Target: 3),
            new MarkerRow(5f, Buff: 33, Target: 4),                 // a game SpellLevel
            new MarkerRow(Markers.Unit + 1000, Buff: 34, Target: 5),
        ]);
        Assert.Equal([1L], plan.Despawn);
        Assert.Equal([22L, 23L], plan.RemoveCarriers);
        Assert.DoesNotContain(2L, plan.Despawn);
        Assert.DoesNotContain(3L, plan.Despawn);
    }

    [Fact]
    public void An_empty_input_yields_nothing()
    {
        var plan = SweepPlan.From([]);
        Assert.Empty(plan.Despawn);
        Assert.Empty(plan.RemoveCarriers);
    }

    [Fact]
    public void Markers_tell_units_from_carriers()
    {
        Assert.Equal(1313952069, Markers.Carrier);
        Assert.Equal([Markers.Unit, Markers.Carrier], Markers.All);
        Assert.Equal(MarkerKind.Unit, Markers.KindOf(Markers.Unit));
        Assert.Equal(MarkerKind.Carrier, Markers.KindOf(Markers.Carrier));
        Assert.Equal(MarkerKind.None, Markers.KindOf(1f));
        Assert.True(Markers.IsOurs(Markers.Unit));
        Assert.False(Markers.IsOurs(Markers.Carrier));              // the despawn filter never matches a carrier
    }
}
