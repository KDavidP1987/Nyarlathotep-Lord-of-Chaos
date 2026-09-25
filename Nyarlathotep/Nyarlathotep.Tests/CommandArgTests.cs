using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D5: command arguments of wrong arity, type or range get one line "&lt;arg&gt; must be &lt;rule&gt;".</summary>
public class CommandArgTests
{
    [Theory]
    [InlineData(0, 1, 1, "arguments must be 1")]
    [InlineData(4, 1, 3, "arguments must be 1-3")]
    public void Wrong_arity_is_refused(int given, int min, int max, string error)
    {
        var a = CommandArgs.Arity(given, min, max);
        Assert.False(a.Ok);
        Assert.Equal(error, a.Error);
        Assert.True(CommandArgs.Arity(min, min, max).Ok);
    }

    [Theory]
    [InlineData("raid-1", true)]
    [InlineData("Raid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("abcdefghijabcdefghijabcdefghij123", false)]
    public void Event_ids(string? text, bool ok)
    {
        var a = CommandArgs.EventId(text);
        Assert.Equal(ok, a.Ok);
        if (!ok) Assert.Equal("id must be 1-32 of a-z 0-9 -", a.Error);
    }

    [Theory]
    [InlineData(null, 3, 1, null)]
    [InlineData("2", 3, 2, null)]
    [InlineData("4", 3, 0, "page must be 1-3")]
    [InlineData("0", 3, 0, "page must be 1-3")]
    [InlineData("x", 3, 0, "page must be 1-3")]
    [InlineData("1", 0, 1, null)]
    public void Pages(string? text, int pages, int value, string? error)
    {
        var a = CommandArgs.Page(text, pages);
        Assert.Equal(error, a.Error);
        if (error is null) Assert.Equal(value, a.Value);
    }

    [Theory]
    [InlineData(null, 1, null)]
    [InlineData("20", 20, null)]
    [InlineData("21", 0, "count must be 1-20")]
    [InlineData("0", 0, "count must be 1-20")]
    [InlineData("-3", 0, "count must be 1-20")]
    [InlineData("2.5", 0, "count must be 1-20")]
    public void Counts(string? text, int value, string? error)
    {
        var a = CommandArgs.Count(text, 20);
        Assert.Equal(error, a.Error);
        if (error is null) Assert.Equal(value, a.Value);
    }

    [Theory]
    [InlineData("+2", true, 2)]
    [InlineData("-30", true, -30)]
    [InlineData("+0", true, 0)]
    [InlineData("120", false, 120)]
    [InlineData("1", false, 1)]
    public void Valid_levels(string text, bool offset, int value)
    {
        var a = CommandArgs.Level(text);
        Assert.True(a.Ok);
        Assert.Equal(new LevelArg(offset, value), a.Value);
    }

    [Theory]
    [InlineData("+31")]
    [InlineData("121")]
    [InlineData("0")]
    [InlineData("+")]
    [InlineData("ten")]
    public void Invalid_levels(string text)
    {
        Assert.Equal("level must be 1-120 or +n/-n with n 0-30", CommandArgs.Level(text).Error);
    }

    [Fact]
    public void Level_offsets_resolve_within_1_to_120()
    {
        Assert.Equal(52, new LevelArg(true, 2).Resolve(50));
        Assert.Equal(1, new LevelArg(true, -30).Resolve(10));
        Assert.Equal(120, new LevelArg(true, 30).Resolve(100));
        Assert.Equal(40, new LevelArg(false, 40).Resolve(90));
        Assert.Null(CommandArgs.Level(null).Value);
    }

    [Theory]
    [InlineData(null, 1f, null)]
    [InlineData("1.5", 1.5f, null)]
    [InlineData("0.1", 0.1f, null)]
    [InlineData("10", 10f, null)]
    [InlineData("0.05", 0f, "hp must be 0.1-10.0")]
    [InlineData("11", 0f, "hp must be 0.1-10.0")]
    [InlineData("1,5", 0f, "hp must be 0.1-10.0")]
    public void Multipliers(string? text, float value, string? error)
    {
        var a = CommandArgs.Multiplier("hp", text);
        Assert.Equal(error, a.Error);
        if (error is null) Assert.Equal(value, a.Value);
    }

    [Theory]
    [InlineData(null, 30, null)]
    [InlineData("5", 5, null)]
    [InlineData("100", 100, null)]
    [InlineData("4", 0, "radius must be 5-100")]
    [InlineData("101", 0, "radius must be 5-100")]
    public void Debug_radius(string? text, int value, string? error)
    {
        var a = CommandArgs.Radius(text);
        Assert.Equal(error, a.Error);
        if (error is null) Assert.Equal(value, a.Value);
    }

    [Theory]
    [InlineData("durationSeconds", "600", null)]
    [InlineData("durationSeconds", "29", "durationSeconds must be 30-7200")]
    [InlineData("action.waves", "11", "action.waves must be 1-10")]
    [InlineData("action.radius", "2", null)]
    [InlineData("conditions.chancePercent", "0", "conditions.chancePercent must be 1-100")]
    [InlineData("name", "Night raid", null)]
    [InlineData("name", "<b>x</b>", "name must be 1-40 characters, no angle brackets or control characters")]
    [InlineData("trigger.type", "Manual", "field trigger.type is not settable; edit events.json and reload")]
    public void Settable_fields(string field, string value, string? error)
    {
        Assert.Equal(error, CommandArgs.SettableValue(field, value).Error);
    }
}
