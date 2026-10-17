using System.Globalization;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D16: every new range lives in Logic/Limits.cs, the rule text says the same range, a value at
/// either end is accepted, one past it is refused with that rule (the field and the range), and the apocalypse's raised
/// ranges reach the cfg ceilings: roamers.max MaxTrackedUnits', hordes.size MaxUnitsPerWave's; with the infestation's and
/// the horde march's rows (D8, D10), whose spread max and horde size reach the same ceilings.</summary>
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
        // step 3: the infestation's (D8) and the horde march's (D10) ranges
        static string I(string action) => Apo.Event(action: action);
        yield return [EventValidator.OriginsCountRule, HordeLimits.MinOrigins, HordeLimits.MaxOrigins, 1.0,
            (Func<double, string>)(v => I(Inf.Action(origins: Inf.Origins(count: (int)v))))];
        yield return [EventValidator.OriginNativesRule, HordeLimits.MinOriginNatives, HordeLimits.MaxOriginNatives, 1.0,
            (Func<double, string>)(v => I(Inf.Action(origins: Inf.Origins(minNatives: (int)v))))];
        yield return [EventValidator.OriginSpacingRule, HordeLimits.MinOriginSpacing, HordeLimits.MaxOriginSpacing, 1.0,
            (Func<double, string>)(v => I(Inf.Action(origins: Inf.Origins(minSpacing: (int)v))))];
        yield return [EventValidator.PerOriginRule, HordeLimits.MinPerOrigin, HordeLimits.MaxPerOrigin, 1.0,
            (Func<double, string>)(v => I(Inf.Action(origins: Inf.Origins(perOrigin: (int)v))))];
        yield return [EventValidator.HuntersRule("action"), HordeLimits.MinHunters, HordeLimits.MaxHunters, 1.0,
            (Func<double, string>)(v => I(Inf.Action(hunters: (int)v)))];
        yield return [EventValidator.DriftRule("action"), HordeLimits.MinDrift, HordeLimits.MaxDrift, 1.0,
            (Func<double, string>)(v => I(Inf.Action(drift: (int)v)))];
        yield return [EventValidator.SpreadMaxRule("action.spread"), HordeLimits.MinSpreadMax, HordeLimits.MaxSpreadMax, 1.0,
            (Func<double, string>)(v => I(Inf.Action(spread: Inf.Spread(cap: (int)v))))];
        yield return [EventValidator.SpreadMaxRule("action.growth"), HordeLimits.MinSpreadMax, HordeLimits.MaxSpreadMax, 1.0,
            (Func<double, string>)(v => I(Inf.March(growth: Inf.Spread(cap: (int)v))))];
        yield return [EventValidator.HuntersRule("action.infest"), HordeLimits.MinHunters, HordeLimits.MaxHunters, 1.0,
            (Func<double, string>)(v => I(Inf.March(infest: "{ \"hunters\": " + (int)v + ", \"driftRadius\": 300, \"spread\": " + Inf.Spread() + " }")))];
        yield return [EventValidator.DriftRule("action.infest"), HordeLimits.MinDrift, HordeLimits.MaxDrift, 1.0,
            (Func<double, string>)(v => I(Inf.March(infest: "{ \"hunters\": 60, \"driftRadius\": " + (int)v + ", \"spread\": " + Inf.Spread() + " }")))];
        yield return [EventValidator.StartDistanceRule, HordeLimits.MinStartDistance, HordeLimits.MaxStartDistance, 1.0,
            (Func<double, string>)(v => I(Inf.March(start: "{ \"type\": \"Hotspot\", \"minDistance\": " + (int)v + " }")))];
        yield return [EventValidator.HordeUnitsRule, HordeLimits.MinHorde, HordeLimits.MaxHorde, 1.0,
            (Func<double, string>)(v => I(Inf.March(units: "[ " + Apo.Unit(count: N(Math.Floor(v / 2))) + ", " + Apo.Unit(Apo.Thug, N(v - Math.Floor(v / 2))) + " ]")))];   // the total, each entry within its own 1-500
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
                     (EventValidator.OriginsCountRule, "action.origins.count"), (EventValidator.OriginNativesRule, "action.origins.minNatives"),
                     (EventValidator.OriginSpacingRule, "action.origins.minSpacing"), (EventValidator.PerOriginRule, "action.origins.perOrigin"),
                     (EventValidator.HuntersRule("action.infest"), "action.infest.hunters"), (EventValidator.DriftRule("action"), "action.driftRadius"),
                     (EventValidator.SpreadMaxRule("action.growth"), "action.growth.max"), (EventValidator.StartDistanceRule, "action.start.minDistance"),
                     (EventValidator.SpreadKillMaxRule("action.infest.spread"), "action.infest.spread.perKillMax"),
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
        Assert.Equal(HordeLimits.MaxSpreadMax, Limits.MaxTrackedUnits.Max);
        Assert.Equal(HordeLimits.MaxHorde, Limits.MaxUnitsPerWave.Max);
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
        Assert.Null(Reason(Apo.Event(action: Inf.Action())));
        Assert.Null(Reason(Apo.Event(action: Inf.March())));
    }
}
