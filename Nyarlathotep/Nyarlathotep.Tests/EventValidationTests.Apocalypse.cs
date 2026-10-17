using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D1: the `"type": "Apocalypse"` action, each key in its range, an unknown key named with its path, an
/// Admin goal only under a Manual trigger, the last horde before the end, and the 600-604800 s duration of this kind beside
/// every other kind's 30-7200 s; each refusal disables the event with one reason that names the field within 120 bytes.</summary>
public partial class EventValidationTests
{
    static EventDefinition ApoLoad(string? action = null, int duration = 3600, string? trigger = null)
    {
        var r = EventValidator.Parse(Json.File(Apo.Event("apo", trigger, action, duration)), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        return Assert.Single(r.Set.All);
    }

    static void ApoRefused(string action, string reason, int duration = 3600, string? trigger = null)
    {
        var d = ApoLoad(action, duration, trigger);
        Assert.False(d.Startable, action);
        Assert.Equal(reason, d.DisabledReason);
        Assert.True(Encoding.UTF8.GetByteCount(reason) <= 120, reason);
    }

    public static IEnumerable<object[]> ApocalypseRefusals() =>
    [
        // faction
        [Apo.Action(faction: "\"Bandits\""), EventValidator.ApocalypseFactionRule],
        [Apo.Action(faction: "\"Faction_Nobody\""), "unknown faction Faction_Nobody"],
        [Apo.Action(faction: "\"Faction_Players\""), "faction Faction_Players is deny-listed"],
        [Apo.Action(faction: "3"), EventValidator.ApocalypseFactionRule],
        // units, under ParseEntries and ParseModifiers
        [Apo.Action(units: "[ ]"), EventValidator.ApocalypseUnitsRule],
        [Apo.Action(units: "[ " + string.Join(", ", Enumerable.Repeat(Apo.Unit(), 11)) + " ]"), EventValidator.ApocalypseUnitsRule],
        [Apo.Action(units: "[ " + Apo.Unit(count: "0") + " ]"), "action.units.1.count must be 1-500"],
        [Apo.Action(units: "[ " + Apo.Unit(count: "501") + " ]"), "action.units.1.count must be 1-500"],
        [Apo.Action(units: "[ " + Apo.Unit(more: "\"chance\": 0.04") + " ]"), "action.units.1.chance must be a number 0.05-1.0"],
        [Apo.Action(units: "[ " + Apo.Unit(more: "\"modifiers\": { \"level\": 121 }") + " ]"), "action.units.1.modifiers.level must be 1-120"],
        [Apo.Action(units: "[ " + Apo.Unit(more: "\"modifiers\": { \"level\": 30, \"levelDelta\": 2 }") + " ]"), "action.units.1.modifiers takes level or levelDelta, not both"],
        [Apo.Action(units: "[ " + Apo.Unit(more: "\"modifiers\": { \"maxHealth\": 5.1 }") + " ]"), "action.units.1.modifiers.maxHealth must be a number 0.5-3.0 with at most two decimals"],
        [Apo.Action(units: "[ " + Apo.Unit("CHAR_Nobody") + " ]"), "unknown unit CHAR_Nobody"],
        // hotspots
        [Apo.Action(hotspots: "3"), EventValidator.HotspotsObject],
        [Apo.Action(hotspots: Apo.Hotspots(count: 0)), EventValidator.HotspotsCountRule],
        [Apo.Action(hotspots: Apo.Hotspots(count: 6)), EventValidator.HotspotsCountRule],
        [Apo.Action(hotspots: Apo.Hotspots(minNatives: 2)), EventValidator.MinNativesRule],
        [Apo.Action(hotspots: Apo.Hotspots(minNatives: 51)), EventValidator.MinNativesRule],
        [Apo.Action(hotspots: Apo.Hotspots(minSpacing: 99)), EventValidator.MinSpacingRule],
        [Apo.Action(hotspots: Apo.Hotspots(minSpacing: 3001)), EventValidator.MinSpacingRule],
        [Apo.Action(hotspots: "{ \"count\": 2, \"minNatives\": 3, \"minSpacing\": 200, \"cell\": 100 }"), "unknown field action.hotspots.cell"],
        // roamers
        [Apo.Action(roamers: "[ ]"), EventValidator.RoamersObject],
        [Apo.Action(roamers: Apo.Roamers(perHotspot: 0)), EventValidator.PerHotspotRule],
        [Apo.Action(roamers: Apo.Roamers(perHotspot: 11)), EventValidator.PerHotspotRule],
        [Apo.Action(roamers: Apo.Roamers(perKill: -1)), EventValidator.PerKillRule],
        [Apo.Action(roamers: Apo.Roamers(perKill: 3)), EventValidator.PerKillRule],
        [Apo.Action(roamers: Apo.Roamers(max: 0)), EventValidator.RoamersMaxRule],
        [Apo.Action(roamers: Apo.Roamers(max: 501)), EventValidator.RoamersMaxRule],
        [Apo.Action(roamers: Apo.Roamers(perMinute: 0)), EventValidator.PerMinuteRule],
        [Apo.Action(roamers: Apo.Roamers(perMinute: 31)), EventValidator.PerMinuteRule],
        [Apo.Action(roamers: Apo.Roamers(huntRange: 19)), EventValidator.RoamersHuntRangeRule],
        [Apo.Action(roamers: Apo.Roamers(huntRange: 61)), EventValidator.RoamersHuntRangeRule],
        // hordes
        [Apo.Action(hordes: "true"), EventValidator.HordesObject],
        [Apo.Action(hordes: Apo.Hordes(count: 0)), EventValidator.HordesCountRule],
        [Apo.Action(hordes: Apo.Hordes(count: 11)), EventValidator.HordesCountRule],
        [Apo.Action(hordes: Apo.Hordes(firstAfter: -1)), EventValidator.FirstAfterRule],
        [Apo.Action(hordes: Apo.Hordes(firstAfter: 1441)), EventValidator.FirstAfterRule],
        [Apo.Action(hordes: Apo.Hordes(every: 4)), EventValidator.EveryMinutesRule],
        [Apo.Action(hordes: Apo.Hordes(every: 1441)), EventValidator.EveryMinutesRule],
        [Apo.Action(hordes: Apo.Hordes(size: 4)), EventValidator.HordeSizeRule],
        [Apo.Action(hordes: Apo.Hordes(size: 501)), EventValidator.HordeSizeRule],
        [Apo.Action(hordes: Apo.Hordes(growth: -1)), EventValidator.HordeGrowthRule],
        [Apo.Action(hordes: Apo.Hordes(growth: 101)), EventValidator.HordeGrowthRule],
        // goal
        [Apo.Action(goal: "\"Brighthaven\""), EventValidator.GoalObject],
        [Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Region\" }")), EventValidator.GoalLocationRule],
        [Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": 1 }")), EventValidator.GoalLocationRule],
        [Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": 10001, \"z\": 0 }")), EventValidator.GoalCoordRule("x")],
        [Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": 0, \"z\": 0, \"y\": \"up\" }")), EventValidator.GoalCoordRule("y")],
        [Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": 0, \"z\": 0, \"w\": 1 }")), "unknown field action.goal.location.w"],
        [Apo.Action(goal: Apo.Goal(radius: 9)), EventValidator.GoalRadiusRule],
        [Apo.Action(goal: Apo.Goal(radius: 101)), EventValidator.GoalRadiusRule],
        [Apo.Action(goal: Apo.Goal(name: "")), EventValidator.GoalNameRule],
        [Apo.Action(goal: Apo.Goal(name: new string('a', 33))), EventValidator.GoalNameRule],
        [Apo.Action(goal: Apo.Goal(name: "<b>keep</b>")), EventValidator.GoalNameRule],
        [Apo.Action(goal: Apo.Goal(extra: "\"colour\": \"red\"")), "unknown field action.goal.colour"],
        // march, bosses (revenge D28), scoreboard, unknown key
        [Apo.Action(march: "0.49"), EventValidator.MarchSpeedRule],
        [Apo.Action(march: "5.01"), EventValidator.MarchSpeedRule],
        [Apo.Action(march: "\"fast\""), EventValidator.MarchSpeedRule],
        [Apo.Action(extra: "\"bosses\": [ ]"), EventValidator.BossesRule],
        [Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(count: "4") + " ]"), EventValidator.BossCountRule],
        [Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson("CHAR_Nobody_VBlood") + " ]"), "unknown unit CHAR_Nobody_VBlood"],
        [Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(more: "\"chance\": 0.5") + " ]"), "unknown field action.bosses.chance"],
        [Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(more: "\"modifiers\": { \"level\": 0 }") + " ]"), "action.bosses.1.modifiers.level must be 1-120"],
        [Apo.Action(extra: "\"bosses\": [ " + string.Join(", ", Enumerable.Repeat(Apo.BossJson(), 4)) + " ]"), EventValidator.BossesRule],
        [Apo.Action(extra: "\"scoreboard\": \"yes\""), EventValidator.ScoreboardRule],
        [Apo.Action(extra: "\"waves\": 3"), "unknown field action.waves"],
        [Apo.Action(extra: "\"scope\": [\"Atlantis\"]"), "unknown region Atlantis"],
    ];

    [Theory]
    [MemberData(nameof(ApocalypseRefusals))]
    public void Apocalypse_fails_when_a_key_is_out_of_range(string action, string reason) => ApoRefused(action, reason);

    [Fact]
    public void Apocalypse_fails_when_a_bound_is_off_by_one()
    {
        // each key at its two bounds loads (the refusals above are one past them)
        foreach (var action in new[]
                 {
                     Apo.Action(hotspots: Apo.Hotspots(count: 1, minNatives: 3, minSpacing: 100)),
                     Apo.Action(hotspots: Apo.Hotspots(count: 5, minNatives: 50, minSpacing: 3000)),
                     Apo.Action(roamers: Apo.Roamers(perHotspot: 1, perKill: 0, max: 1, perMinute: 1, huntRange: 20)),
                     Apo.Action(roamers: Apo.Roamers(perHotspot: 10, perKill: 2, max: 100, perMinute: 30, huntRange: 60)),
                     Apo.Action(hordes: Apo.Hordes(count: 1, firstAfter: 0, every: 5, size: 5, growth: 0)),
                     Apo.Action(goal: Apo.Goal(radius: 10, name: "a")),
                     Apo.Action(goal: Apo.Goal(radius: 100, name: new string('a', 32))),
                     Apo.Action(march: "0.5"), Apo.Action(march: "5.0"),
                     Apo.Action(units: "[ " + Apo.Unit(count: "50", more: "\"chance\": 0.05") + " ]"),
                     Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(count: "3") + " ]"),
                 })
        {
            var d = ApoLoad(action);
            Assert.True(d.Startable, d.DisabledReason + " " + action);
        }
        // hordes' upper bounds need a long run: 10 hordes from 1440 min every 1440 min rise in 14 days, so a 7-day run refuses it
        Assert.True(ApoLoad(Apo.Action(hordes: Apo.Hordes(count: 10, firstAfter: 0, every: 1119, size: 50, growth: 20)), 604800).Startable);
        Assert.True(ApoLoad(Apo.Action(hordes: Apo.Hordes(count: 1, firstAfter: 1440, every: 1440)), 604800).Startable);
    }

    [Fact]
    public void Apocalypse_fails_when_a_duration_leaves_its_kind()
    {
        Assert.True(ApoLoad(Apo.Action(hordes: Apo.Hordes(count: 1, firstAfter: 9)), 600).Startable);
        Assert.True(ApoLoad(Apo.Action(hordes: Apo.Hordes(count: 1, firstAfter: 0)), 604800).Startable);
        ApoRefused(Apo.Action(), EventValidator.ApocalypseDurationRule, 599);
        ApoRefused(Apo.Action(), EventValidator.ApocalypseDurationRule, 604801);
        ApoRefused(Apo.Action(), EventValidator.ApocalypseDurationRule, 30);
        // every other kind keeps 30-7200 s
        Assert.True(Json.One(Json.Event().Replace("\"durationSeconds\": 600", "\"durationSeconds\": 7200")).Startable);
        foreach (var other in new[] { "7201", "29", "604800" })
        {
            var d = Json.One(Json.Event().Replace("\"durationSeconds\": 600", $"\"durationSeconds\": {other}"));
            Assert.False(d.Startable);
            Assert.Equal(EventValidator.DurationRule, d.DisabledReason);
        }
        var empower = Json.One(Json.Empower().Replace("\"durationSeconds\": 600", "\"durationSeconds\": 7201"));
        Assert.Equal(EventValidator.DurationRule, empower.DisabledReason);
    }

    [Fact]
    public void Apocalypse_fails_when_the_last_horde_rises_at_or_after_the_end()
    {
        // 3 hordes from 1 min every 5 min: the last rises at 660 s
        ApoRefused(Apo.Action(hordes: Apo.Hordes(count: 3, firstAfter: 1, every: 5)), EventValidator.LastHordeLate, 660);
        Assert.True(ApoLoad(Apo.Action(hordes: Apo.Hordes(count: 3, firstAfter: 1, every: 5)), 661).Startable);
        ApoRefused(Apo.Action(hordes: Apo.Hordes(count: 1, firstAfter: 10)), EventValidator.LastHordeLate, 600);
        Assert.Equal(660, HordeSchedule.LastRiseSeconds(new ApocalypseHordes(3, 1, 5, 8, 0)));
    }

    public static TheoryData<string> NonManualTriggers() => new()
    {
        "{ \"type\": \"Schedule\", \"days\": [\"Fri\"], \"times\": [\"20:00\"] }",
        "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }",
        "{ \"type\": \"GameTime\", \"phase\": \"night\" }",
        "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }",
        "{ \"type\": \"RegionEntered\", \"playerCooldownMinutes\": 30, \"scope\": [\"FarbaneWoods\"] }",
        "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }",
        Json.RevengeTrigger,
    };

    /// <summary>Review 3 F4: an Admin goal is refused under every trigger kind but Manual, Revenge included (the goal is
    /// read first; with a Point goal a Revenge trigger is still refused, as revenge D2 needs SpawnWaves).</summary>
    [Theory]
    [MemberData(nameof(NonManualTriggers))]
    public void Apocalypse_fails_when_an_admin_goal_has_another_trigger(string trigger)
    {
        var admin = Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }"));
        var d = ApoLoad(admin, trigger: trigger);
        Assert.False(d.Startable, trigger);
        Assert.Equal(EventValidator.GoalAdminManual, d.DisabledReason);
        var point = ApoLoad(trigger: trigger);                                                                                      // a Point goal loads
        if (trigger.Contains("Revenge", StringComparison.Ordinal)) Assert.Equal(EventValidator.RevengeNeedsWaves, point.DisabledReason);
        else Assert.True(point.Startable, trigger);
        Assert.True(ApoLoad(admin).Startable);                                                                                      // Manual takes Admin
    }

    [Fact]
    public void Apocalypse_fails_when_an_unknown_key_is_named_without_its_path()
    {
        ApoRefused(Apo.Action(extra: "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }"), "unknown field action.location");
        ApoRefused(Apo.Action(roamers: "{ \"perHotspot\": 3, \"perKill\": 2, \"max\": 12, \"perMinute\": 4, \"huntRange\": 40, \"speed\": 2 }"), "unknown field action.roamers.speed");
        ApoRefused(Apo.Action(hordes: "{ \"count\": 2, \"firstAfterMinutes\": 1, \"everyMinutes\": 5, \"size\": 8, \"growth\": 4, \"bosses\": 1 }"), "unknown field action.hordes.bosses");
        ApoRefused(Apo.Action(units: "[ " + Apo.Unit(more: "\"modifiers\": { \"armour\": 2 }") + " ]"), "unknown field action.units.1.modifiers.armour");
    }

    [Fact]
    public void Apocalypse_passes_every_key_and_pillar_spawns()
    {
        var d = ApoLoad(Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson(more: "\"modifiers\": { \"level\": 60 }") + " ], \"scope\": [\"FarbaneWoods\"], \"scoreboard\": false"));
        Assert.True(d.Startable, d.DisabledReason);
        Assert.Null(d.Action);
        Assert.Null(d.Empower);
        var a = d.Apocalypse!;
        Assert.Equal("Faction_Undead", a.Faction);
        Assert.Equal(new ApocalypseHotspots(2, 3, 200), a.Hotspots);
        Assert.Equal(new ApocalypseRoamers(3, 2, 12, 4, 40), a.Roamers);
        Assert.Equal(new ApocalypseHordes(3, 1, 5, 8, 4), a.Hordes);
        Assert.Equal("Brighthaven", a.Goal.Name);
        Assert.Equal(20, a.Goal.Radius);
        Assert.Equal((LocationType.Point, 500f, 0f, (float?)12f), (a.Goal.Location.Type, a.Goal.Location.X, a.Goal.Location.Z, a.Goal.Location.Y));
        Assert.Equal(2.0, a.MarchSpeed);
        Assert.Equal(60, Assert.Single(a.Bosses!).Modifiers!.Level);
        Assert.False(a.Scoreboard);
        Assert.Equal(["FarbaneWoods"], a.Scope.Regions);
        Assert.True(ApoLoad().Apocalypse!.Scoreboard);                                        // absent = true
        Assert.Null(ApoLoad().Apocalypse!.Bosses);
        Assert.Equal(EventActionKind.Apocalypse, EventActions.ActionKindOf(d));
        // pillar empowerment refuses it
        var r = EventValidator.Parse(Json.File(Apo.Event("apo").Replace("\"pillar\": \"spawns\"", "\"pillar\": \"empowerment\"")), FakeUnits.Default());
        Assert.Equal(EventValidator.ApocalypsePillar, Assert.Single(r.Set.All).DisabledReason);
    }

    [Fact]
    public void Apocalypse_empty_optional_keys_absent()
    {
        var a = ApoLoad().Apocalypse!;
        Assert.Null(a.Bosses);
        Assert.True(a.Scoreboard);
        Assert.Equal(Scope.Global, a.Scope);
        Assert.Null(ApoLoad(Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": 500, \"z\": 0 }"))).Apocalypse!.Goal.Location.Y);
    }

    [Fact]
    public void Apocalypse_fails_when_a_reason_exceeds_120_bytes() =>
        Assert.All(EventValidator.ApocalypseReasons, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));
}
