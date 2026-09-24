using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D5: cfg values above the hard ceilings clamp with a log line.</summary>
public class ConfigClampTests
{
    [Theory]
    [InlineData("MaxTrackedUnits", 500)]
    [InlineData("MaxUnitsPerWave", 50)]
    [InlineData("MaxConcurrentEvents", 10)]
    [InlineData("MaxDespawnsPerTick", 20)]
    [InlineData("MaxSpawnsPerTick", 20)]
    public void Epic_ceilings_are_the_code_ceilings(string key, int ceiling)
    {
        Assert.Equal(ceiling, Limits.All.Single(l => l.Key == key).Max);
    }

    [Fact]
    public void Every_limit_default_is_inside_its_range()
    {
        Assert.NotEmpty(Limits.All);
        foreach (var l in Limits.All) Assert.InRange(l.Default, l.Min, l.Max);
    }

    [Fact]
    public void Above_the_ceiling_clamps_and_logs()
    {
        var (v, log) = Limits.MaxTrackedUnits.Clamp(9000);
        Assert.Equal(500, v);
        Assert.Equal("Limits.MaxTrackedUnits=9000 is above its ceiling 500; using 500", log);
    }

    [Fact]
    public void Below_the_minimum_clamps_and_logs()
    {
        var (v, log) = Limits.MaxUnitsPerWave.Clamp(0);
        Assert.Equal(1, v);
        Assert.NotNull(log);
    }

    [Fact]
    public void In_range_values_pass_silently()
    {
        foreach (var l in Limits.All)
        {
            Assert.Equal((l.Default, (string?)null), l.Clamp(l.Default));
            Assert.Equal((l.Max, (string?)null), l.Clamp(l.Max));
            Assert.Equal((l.Min, (string?)null), l.Clamp(l.Min));
        }
    }

    [Theory]
    [InlineData("300,60,10", new[] { 300, 60, 10 }, false)]
    [InlineData("10, 60,300,60", new[] { 300, 60, 10 }, false)]
    [InlineData("1,60,99999", new[] { 60 }, true)]
    [InlineData("600,500,400,300,200,100", new[] { 600, 500, 400, 300, 200 }, true)]
    [InlineData("", new[] { 300, 60, 10 }, true)]
    [InlineData("soon", new[] { 300, 60, 10 }, true)]
    public void Warning_offsets_are_bounded(string text, int[] expected, bool logs)
    {
        var (offsets, log) = Limits.ParseWarningOffsets(text);
        Assert.Equal(expected, offsets);
        Assert.Equal(logs, log is not null);
    }

    [Theory]
    [InlineData("20:00", 20, 0, false)]
    [InlineData("07:05", 7, 5, false)]
    [InlineData("7:05", 20, 0, true)]
    [InlineData("24:00", 20, 0, true)]
    [InlineData(null, 20, 0, true)]
    public void Daily_banner_time_is_strict_hh_mm(string? text, int h, int m, bool logs)
    {
        var (t, log) = Limits.ParseDailyBannerTime(text);
        Assert.Equal(new TimeOnly(h, m), t);
        Assert.Equal(logs, log is not null);
    }
}
