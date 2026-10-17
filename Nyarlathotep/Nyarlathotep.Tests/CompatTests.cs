using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D21: every 0.13.1 template and the default fixtures validate and plan as 0.13.1 did. The golden,
/// Resources/compat/plans-0.13.1.txt, is CompatDigest run on a v0.13.1 worktree over the same frozen templates file.</summary>
public class CompatTests
{
    static string Compat(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "compat", file)).Replace("\r\n", "\n");

    static string Now() => CompatDigest.Of(CompatDigest.Definitions(Compat("templates-0.13.1.json")));

    /// <summary>The first line where <paramref name="now"/> leaves <paramref name="golden"/>, or null when they agree.</summary>
    internal static string? FirstDifference(string golden, string now)
    {
        var a = golden.Split('\n');
        var b = now.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : "(end)";
            var y = i < b.Length ? b[i] : "(end)";
            if (x != y) return $"line {i + 1}: 0.13.1 \"{x}\", now \"{y}\"";
        }
        return null;
    }

    [Fact]
    public void Compat_passes_every_0_13_1_definition_plans_as_before()
    {
        var golden = Compat("plans-0.13.1.txt");
        Assert.True(golden.Split('\n').Count(l => l.StartsWith("== ", StringComparison.Ordinal)) >= 30);   // the 34 templates and the fixtures
        Assert.Null(FirstDifference(golden, Now()));
    }

    [Fact]
    public void Compat_fails_when_a_definition_validates_or_plans_differently()
    {
        var golden = Compat("plans-0.13.1.txt");
        // a template disabled, a horde's units changed, a spread drawing another number
        Assert.NotNull(FirstDifference(golden, golden.Replace("startable=True", "startable=False")));
        var horde = golden.Split('\n').First(l => l.StartsWith("horde 1: ", StringComparison.Ordinal));
        Assert.NotNull(FirstDifference(golden, golden.Replace(horde, horde + "x")));
        var orders = golden.Split('\n').First(l => l.StartsWith("orders ", StringComparison.Ordinal));
        Assert.NotNull(FirstDifference(golden, golden.Replace(orders, orders[..^1])));
        // a SpawnWaves fixture without a destination has no march line and the 0.13.1 field values
        var raid = CompatDigest.Definitions(Compat("templates-0.13.1.json")).Single(d => d.Id == "fixture-raid").Action!;
        Assert.Null(raid.Destination);
        Assert.Equal(DestinationLimits.DefaultMarchSpeed, raid.MarchSpeed);
        Assert.Null(EventLines.MarchLine(raid));
    }

    [Fact]
    public void Compat_empty_no_definitions_give_an_empty_digest() => Assert.Equal("", CompatDigest.Of([]));
}
