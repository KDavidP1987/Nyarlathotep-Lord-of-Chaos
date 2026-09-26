using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D12: `.nyar event set &lt;id&gt; action.stats.&lt;stat&gt; &lt;value&gt;` takes 1.0-3.0 with at
/// most two decimals, invariant culture.</summary>
public partial class CommandArgTests
{
    [Theory]
    [InlineData("action.stats.physicalPower", "1.3", 1.3)]
    [InlineData("action.stats.spellPower", "1", 1.0)]
    [InlineData("action.stats.maxHealth", "3.0", 3.0)]
    [InlineData("action.stats.attackSpeed", "1.15", 1.15)]
    [InlineData("action.stats.moveSpeed", "2.5", 2.5)]
    public void Stat_fields_take_a_decimal(string field, string text, double value)
    {
        var a = CommandArgs.SettableValue(field, text);
        Assert.Null(a.Error);
        Assert.Equal((decimal)value, Assert.IsType<decimal>(a.Value));
    }

    [Theory]
    [InlineData("3.5")]
    [InlineData("3.01")]
    [InlineData("0.9")]
    [InlineData("1,2")]
    [InlineData("abc")]
    [InlineData("1.234")]
    [InlineData("-1.5")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("")]
    [InlineData(null)]
    public void Stat_fields_refuse_everything_else(string? text) =>
        Assert.Equal("action.stats.physicalPower must be 1.0-3.0 with at most two decimals",
            CommandArgs.SettableValue("action.stats.physicalPower", text).Error);

    [Fact]
    public void Only_the_five_stats_are_settable() =>
        Assert.Equal("field action.stats.visual is not settable; edit events.json and reload",
            CommandArgs.SettableValue("action.stats.visual", "1.5").Error);
}
