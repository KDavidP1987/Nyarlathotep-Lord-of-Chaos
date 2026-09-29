using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D1 (Interval), D4 (fanOut), D8 (RegionEntered) and D10 (FactionKills): each failure disables the
/// definition with one reason naming the field; the other trigger types parse as in 0.7.0.</summary>
public partial class EventValidationTests
{
    /// <summary>One definition parsed against every region of the map, so a trigger scope can name them.</summary>
    static EventDefinition Mapped(string eventJson)
    {
        var r = EventValidator.Parse(Json.File(eventJson), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        return Assert.Single(r.Set.All);
    }

    static string Reason(string trigger) => Mapped(Json.Event(trigger: trigger)).DisabledReason!;

    // ---- D1 Interval

    [Theory]
    [InlineData("{ \"type\": \"Interval\", \"maxMinutes\": 60 }", EventValidator.MinMinutesRule)]
    [InlineData("{ \"type\": \"Interval\", \"minMinutes\": 60 }", EventValidator.MaxMinutesRule)]
    [InlineData("{ \"type\": \"Interval\", \"minMinutes\": 4, \"maxMinutes\": 60 }", EventValidator.MinMinutesRule)]
    [InlineData("{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 1441 }", EventValidator.MaxMinutesRule)]
    [InlineData("{ \"type\": \"Interval\", \"minMinutes\": \"60\", \"maxMinutes\": 90 }", EventValidator.MinMinutesRule)]
    [InlineData("{ \"type\": \"Interval\", \"minMinutes\": 60.5, \"maxMinutes\": 90 }", EventValidator.MinMinutesRule)]
    public void IntervalTrigger_fails_when_value_missing_or_out_of_range(string trigger, string reason) => Assert.Equal(reason, Reason(trigger));

    [Fact]
    public void IntervalTrigger_fails_when_min_above_max() =>
        Assert.Equal(EventValidator.IntervalOrder, Reason("{ \"type\": \"Interval\", \"minMinutes\": 91, \"maxMinutes\": 90 }"));

    [Fact]
    public void IntervalTrigger_fails_when_unknown_key() =>
        Assert.Equal("unknown field trigger.days", Reason("{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90, \"days\": [\"Sat\"] }"));

    [Fact]
    public void IntervalTrigger_passes_bounds_and_scope()
    {
        var d = Mapped(Json.Event(trigger: "{ \"type\": \"Interval\", \"minMinutes\": 5, \"maxMinutes\": 1440, \"scope\": [\"FarbaneWoods\"] }"));
        Assert.Null(d.DisabledReason);
        Assert.Equal((TriggerType.Interval, 5, 1440), (d.Trigger.Type, d.Trigger.MinMinutes, d.Trigger.MaxMinutes));
        Assert.True(d.Trigger.Scope.Names("FarbaneWoods"));
        var fixedPeriod = Mapped(Json.Event(trigger: "{ \"type\": \"Interval\", \"minMinutes\": 30, \"maxMinutes\": 30 }"));
        Assert.Null(fixedPeriod.DisabledReason);
        // the 0.7.0 trigger types parse as before
        foreach (var t in new[] { "{ \"type\": \"Manual\" }", "{ \"type\": \"GameTime\", \"phase\": \"night\" }", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }" })
            Assert.Null(Mapped(Json.Event(trigger: t)).DisabledReason);
    }

    [Fact]
    public void IntervalTrigger_empty_trigger_without_values() =>
        Assert.Equal(EventValidator.MinMinutesRule, Reason("{ \"type\": \"Interval\" }"));

    // ---- D4 fanOut

    static string FanOut(string fanOut, string? location = null) =>
        Action(location: location ?? AroundPlayer(20, 40), extra: "\"fanOut\": " + fanOut);

    [Theory]
    [InlineData("{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }")]
    [InlineData("{ \"type\": \"Admin\" }")]
    public void FanOutKey_fails_when_location_not_aroundplayer(string location) =>
        Assert.Equal(EventValidator.FanOutLocation, Json.One(Json.Event(action: FanOut("{ \"maxInstances\": 3, \"minSpacing\": 150 }", location))).DisabledReason);

    [Theory]
    [InlineData("{ \"maxInstances\": 1, \"minSpacing\": 150 }", EventValidator.FanOutInstancesRule)]
    [InlineData("{ \"maxInstances\": 11, \"minSpacing\": 150 }", EventValidator.FanOutInstancesRule)]
    [InlineData("{ \"minSpacing\": 150 }", EventValidator.FanOutInstancesRule)]
    [InlineData("{ \"maxInstances\": 3, \"minSpacing\": 49 }", EventValidator.FanOutSpacingRule)]
    [InlineData("{ \"maxInstances\": 3, \"minSpacing\": 501 }", EventValidator.FanOutSpacingRule)]
    [InlineData("{ \"maxInstances\": 3 }", EventValidator.FanOutSpacingRule)]
    [InlineData("{ \"maxInstances\": 3, \"minSpacing\": 150, \"groups\": 2 }", "unknown field action.fanOut.groups")]
    [InlineData("3", EventValidator.FanOutObject)]
    [InlineData("[3, 150]", EventValidator.FanOutObject)]
    public void FanOutKey_fails_when_value_bad(string fanOut, string reason) =>
        Assert.Equal(reason, Json.One(Json.Event(action: FanOut(fanOut))).DisabledReason);

    [Fact]
    public void FanOutKey_passes_valid_fanout()
    {
        var low = Json.One(Json.Event(action: FanOut("{ \"maxInstances\": 2, \"minSpacing\": 50 }")));
        var high = Json.One(Json.Event(action: FanOut("{ \"maxInstances\": 10, \"minSpacing\": 500 }")));
        Assert.Null(low.DisabledReason);
        Assert.Equal(new FanOut(2, 50), low.Action!.FanOut);
        Assert.Equal(new FanOut(10, 500), high.Action!.FanOut);
        Assert.Null(Json.One(Json.Event(action: Action(location: AroundPlayer(20, 40)))).Action!.FanOut);     // absent: one centre
    }

    [Fact]
    public void FanOutKey_empty_fanout_object() =>
        Assert.Equal(EventValidator.FanOutInstancesRule, Json.One(Json.Event(action: FanOut("{ }"))).DisabledReason);

    // ---- D8 RegionEntered

    [Theory]
    [InlineData("{ \"type\": \"RegionEntered\" }")]
    [InlineData("{ \"type\": \"RegionEntered\", \"scope\": \"Global\" }")]
    [InlineData("{ \"type\": \"RegionEntered\", \"scope\": \"global\", \"playerCooldownMinutes\": 5 }")]
    public void RegionEnteredTrigger_fails_when_scope_missing_or_global(string trigger) =>
        Assert.Equal(EventValidator.RegionEnteredScopeRule, Reason(trigger));

    [Theory]
    [InlineData("-1")]
    [InlineData("1441")]
    [InlineData("\"30\"")]
    public void RegionEnteredTrigger_fails_when_cooldown_out_of_range(string cooldown) =>
        Assert.Equal(EventValidator.PlayerCooldownRule,
            Reason("{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": " + cooldown + " }"));

    [Fact]
    public void RegionEnteredTrigger_fails_when_unknown_key() =>
        Assert.Equal("unknown field trigger.minMinutes", Reason("{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"minMinutes\": 5 }"));

    [Fact]
    public void RegionEnteredTrigger_passes_one_region_and_default_cooldown()
    {
        var d = Mapped(Json.Event(trigger: "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"] }"));
        Assert.Null(d.DisabledReason);
        Assert.Equal((TriggerType.RegionEntered, 30), (d.Trigger.Type, d.Trigger.PlayerCooldownMinutes));
        Assert.True(d.Trigger.Scope.Names("CursedForest"));
        Assert.Equal(0, Mapped(Json.Event(trigger: "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": 0 }")).Trigger.PlayerCooldownMinutes);
        Assert.Equal(1440, Mapped(Json.Event(trigger: "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": 1440 }")).Trigger.PlayerCooldownMinutes);
    }

    [Fact]
    public void RegionEnteredTrigger_empty_scope_array()
    {
        var reason = Reason("{ \"type\": \"RegionEntered\", \"scope\": [] }");
        Assert.StartsWith("trigger.scope", reason);
        Assert.NotEqual(EventValidator.RegionEnteredScopeRule, reason);
    }

    // ---- D10 FactionKills

    static string Kills(string factions = "[\"Faction_Bandits\"]", string kills = "20", string window = "300", string? extra = null) =>
        "{ \"type\": \"FactionKills\", \"factions\": " + factions + ", \"kills\": " + kills + ", \"windowSeconds\": " + window +
        (extra is null ? "" : ", " + extra) + " }";

    [Theory]
    [InlineData("\"Faction_Bandits\"", EventValidator.TriggerFactionsRule)]
    [InlineData("[\"Faction_Bandits\", \"Faction_Legion\", \"Faction_Undead\", \"Faction_Militia\", \"Faction_Gloomrot\", \"Faction_Cursed\"]", EventValidator.TriggerFactionsRule)]
    [InlineData("[\"Faction_Bandits\", \"Faction_Bandits\"]", EventValidator.TriggerFactionsRule)]
    [InlineData("[\"Faction_Nowhere\"]", "unknown faction Faction_Nowhere")]
    [InlineData("[\"Faction_Players\"]", "faction Faction_Players is deny-listed")]
    [InlineData("[\"Faction_Critters\"]", "faction Faction_Critters is deny-listed")]
    public void FactionKillsTrigger_fails_when_factions_bad(string factions, string reason) =>
        Assert.Equal(reason, Reason(Kills(factions)));

    [Theory]
    [InlineData("2", "300", EventValidator.KillsRule)]
    [InlineData("501", "300", EventValidator.KillsRule)]
    [InlineData("20", "9", EventValidator.KillWindowRule)]
    [InlineData("20", "3601", EventValidator.KillWindowRule)]
    public void FactionKillsTrigger_fails_when_kills_or_window_out_of_range(string kills, string window, string reason) =>
        Assert.Equal(reason, Reason(Kills(kills: kills, window: window)));

    [Theory]
    [InlineData("\"shared\": 1", EventValidator.SharedRule)]
    [InlineData("\"shared\": \"true\"", EventValidator.SharedRule)]
    [InlineData("\"bosses\": [\"any\"]", "unknown field trigger.bosses")]
    public void FactionKillsTrigger_fails_when_shared_or_key_bad(string extra, string reason) =>
        Assert.Equal(reason, Reason(Kills(extra: extra)));

    [Fact]
    public void FactionKillsTrigger_passes_bandits_and_bounds()
    {
        var d = Mapped(Json.Event(trigger: Kills(extra: "\"shared\": true, \"scope\": [\"FarbaneWoods\"]")));
        Assert.Null(d.DisabledReason);
        Assert.Equal(["Faction_Bandits"], d.Trigger.Factions);
        Assert.Equal((20, 300, true), (d.Trigger.Kills, d.Trigger.WindowSeconds, d.Trigger.Shared));
        var smallest = Mapped(Json.Event(trigger: Kills(kills: "3", window: "10")));
        Assert.Equal((3, 10, false), (smallest.Trigger.Kills, smallest.Trigger.WindowSeconds, smallest.Trigger.Shared));
        Assert.Null(Mapped(Json.Event(trigger: Kills("[\"Faction_Bandits\", \"Faction_Legion\", \"Faction_Undead\", \"Faction_Militia\", \"Faction_Gloomrot\"]", "500", "3600"))).DisabledReason);
    }

    [Fact]
    public void FactionKillsTrigger_empty_factions_array() =>
        Assert.Equal(EventValidator.TriggerFactionsRule, Reason(Kills("[]")));
}
