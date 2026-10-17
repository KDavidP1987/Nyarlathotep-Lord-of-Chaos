using System.Globalization;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D16: every new range lives in Logic/Limits.cs, the rule text says the same range, a value at
/// either end is accepted, one past it is refused with that rule (the field and the range), and the apocalypse's raised
/// ranges reach the cfg ceilings: roamers.max MaxTrackedUnits', hordes.size MaxUnitsPerWave's. Step 3 adds the
/// infestation's and the horde march's rows (D8, D10).</summary>
public class LimitsTests
{
    static string Spread(string min, string max, string roamersMax = "12") =>
        "{ \"perHotspot\": 3, \"perKillMin\": " + min + ", \"perKillMax\": " + max + ", \"max\": " + roamersMax + ", \"perMinute\": 4, \"huntRange\": 40 }";

    static string Apocalypse(string? roamers = null, string? hordes = null) => Apo.Event(action: Apo.Action(roamers: roamers, hordes: hordes));

    static string Destination(string radius = "20", string name = "\"the camp\"", string? march = null) =>
        Json.Event("raid", action: Json.ValidAction[..^2] + ", \"destination\": { \"location\": { \"type\": \"Point\", \"x\": -1000, \"z\": -800 }, \"radius\": "
            + radius + ", \"name\": " + name + " }" + (march is null ? "" : ", \"marchSpeed\": " + march) + " }");

    static string? Reason(string ev) =>
        Assert.Single(EventValidator.Parse(Json.File(ev), FakeUnits.Default(), regions: FakeRegions.All()).Set.All).DisabledReason;

    static string N(double v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>Each range: its rule, its Limits bounds, the event at a value, and the step one past each end.</summary>
    public static IEnumerable<object[]> Ranges()
    {
        yield return [EventValidator.PerKillMinRule, ApocalypseLimits.MinKillDraw, ApocalypseLimits.MaxKillDraw, 1.0,
            (Func<double, string>)(v => Apocalypse(Spread(N(v), "3")))];
        yield return [EventValidator.RoamersMaxRule, ApocalypseLimits.MinRoamers, ApocalypseLimits.MaxRoamers, 1.0,
            (Func<double, string>)(v => Apocalypse(Spread("0", "0", N(v))))];
        yield return [EventValidator.HordeSizeRule, ApocalypseLimits.MinSize, ApocalypseLimits.MaxSize, 1.0,
            (Func<double, string>)(v => Apocalypse(hordes: Apo.Hordes(1, 1, 5, (int)v, 0)))];
        yield return [EventValidator.HordeGrowthRule, ApocalypseLimits.MinGrowth, ApocalypseLimits.MaxGrowth, 1.0,
            (Func<double, string>)(v => Apocalypse(hordes: Apo.Hordes(2, 1, 5, 8, (int)v)))];
        yield return [EventValidator.DestinationRadiusRule, DestinationLimits.MinRadius, DestinationLimits.MaxRadius, 1.0,
            (Func<double, string>)(v => Destination(radius: N(v)))];
        yield return [EventValidator.MarchSpeedRule, ApocalypseLimits.MinMarchSpeed, ApocalypseLimits.MaxMarchSpeed, 0.1,
            (Func<double, string>)(v => Destination(march: N(v)))];
    }

    [Theory]
    [MemberData(nameof(Ranges))]
    public void Limits_fails_when_a_range_differs_from_limits(string rule, double min, double max, double step, Func<double, string> at)
    {
        string Shown(double v) => step < 1 ? v.ToString("0.0", CultureInfo.InvariantCulture) : N(v);
        Assert.Contains($"{Shown(min)}-{Shown(max)}", rule);
        Assert.Null(Reason(at(min)));
        Assert.Null(Reason(at(max)));
    }

    [Theory]
    [MemberData(nameof(Ranges))]
    public void Limits_fails_when_a_value_outside_is_accepted(string rule, double min, double max, double step, Func<double, string> at)
    {
        Assert.Equal(rule, Reason(at(Math.Round(min - step, 2))));
        Assert.Equal(rule, Reason(at(Math.Round(max + step, 2))));
    }

    [Fact]
    public void Limits_fails_when_a_reason_lacks_the_field_or_range()
    {
        Assert.Equal(EventValidator.PerKillMaxRule, Reason(Apocalypse(Spread("2", "1"))));         // max below min
        Assert.Equal(EventValidator.PerKillMaxRule, Reason(Apocalypse(Spread("0", "4"))));
        Assert.Equal(EventValidator.PerKillMinRule, Reason(Apocalypse(Spread("-1", "3"))));
        Assert.Equal(EventValidator.DestinationNameRule, Reason(Destination(name: "\"" + new string('a', DestinationLimits.MaxName + 1) + "\"")));
        Assert.Equal(EventValidator.DestinationNameRule, Reason(Destination(name: "\"\"")));
        Assert.Null(Reason(Destination(name: "\"" + new string('a', DestinationLimits.MaxName) + "\"")));
        foreach (var (rule, field) in new[]
                 {
                     (EventValidator.PerKillMinRule, "action.roamers.perKillMin"), (EventValidator.PerKillMaxRule, "action.roamers.perKillMax"),
                     (EventValidator.RoamersMaxRule, "action.roamers.max"), (EventValidator.HordeSizeRule, "action.hordes.size"),
                     (EventValidator.HordeGrowthRule, "action.hordes.growth"), (EventValidator.DestinationRadiusRule, "action.destination.radius"),
                     (EventValidator.DestinationNameRule, "action.destination.name"), (EventValidator.MarchSpeedRule, "action.marchSpeed"),
                 })
        {
            Assert.StartsWith(field + " must be ", rule);
            Assert.Matches(@"(\d+(\.\d+)?|perKillMin)-\d+(\.\d+)?", rule);
        }
    }

    [Fact]
    public void Limits_passes_the_raised_ranges_reach_the_cfg_ceilings()
    {
        Assert.Equal(ApocalypseLimits.MaxRoamers, Limits.MaxTrackedUnits.Max);
        Assert.Equal(ApocalypseLimits.MaxSize, Limits.MaxUnitsPerWave.Max);
        Assert.Equal(100, ApocalypseLimits.MaxGrowth);
        Assert.Equal(3, ApocalypseLimits.MaxKillDraw);
        Assert.Equal(DestinationLimits.DefaultMarchSpeed, Assert.Single(EventValidator.Parse(Json.File(Destination()), FakeUnits.Default(),
            regions: FakeRegions.All()).Set.All).Action!.MarchSpeed);
    }

    [Fact]
    public void Limits_empty_no_new_keys_no_new_reasons()
    {
        Assert.Null(Reason(Apo.Event()));
        Assert.Null(Reason(Json.Event("raid")));
    }
}
