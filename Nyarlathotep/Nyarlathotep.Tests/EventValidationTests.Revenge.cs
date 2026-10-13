using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D1 (the Revenge trigger's keys and bounds) and D2 (the action it needs): each failure disables the
/// definition with one reason naming the field, within 120 bytes; the other trigger types parse as in 0.11.0.</summary>
public partial class EventValidationTests
{
    const string HuntAround = "\"behaviour\": { \"type\": \"Hunt\", \"range\": 45 }";

    /// <summary>A Revenge trigger with <paramref name="keys"/> after its factions.</summary>
    static string RevengeTrigger(string keys) => "{ \"type\": \"Revenge\", \"factions\": [\"Faction_Bandits\"], " + keys + " }";

    static EventDefinition RevengeEvent(string trigger, string? action = null) => Mapped(Json.Event(trigger: trigger, action: action ?? Json.RevengeAction));

    static string? RevengeReason(string keys) => RevengeEvent(RevengeTrigger(keys)).DisabledReason;

    // ---- D1 Revenge

    [Theory]
    [InlineData("\"heat\": 2, \"windowSeconds\": 900", EventValidator.HeatRule)]
    [InlineData("\"heat\": 501, \"windowSeconds\": 900", EventValidator.HeatRule)]
    [InlineData("\"windowSeconds\": 900", EventValidator.HeatRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 59", EventValidator.RevengeWindowRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 3601", EventValidator.RevengeWindowRule)]
    [InlineData("\"heat\": 30", EventValidator.RevengeWindowRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"vbloodHeat\": -1", EventValidator.VBloodHeatRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"vbloodHeat\": 101", EventValidator.VBloodHeatRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"playerCooldownMinutes\": -1", EventValidator.PlayerCooldownRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"playerCooldownMinutes\": 1441", EventValidator.PlayerCooldownRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"warnAt\": [0]", EventValidator.WarnAtRule)]
    [InlineData("\"heat\": 30, \"windowSeconds\": 900, \"warnAt\": [30]", EventValidator.WarnAtRule)]
    public void Revenge_fails_when_a_bound_is_off_by_one(string keys, string reason) => Assert.Equal(reason, RevengeReason(keys));

    [Theory]
    [InlineData("[20, 10]")]
    [InlineData("[10, 10]")]
    [InlineData("[5, 10, 15, 20]")]
    [InlineData("10")]
    [InlineData("[\"10\"]")]
    public void Revenge_fails_when_warn_at_is_out_of_order(string warnAt) =>
        Assert.Equal(EventValidator.WarnAtRule, RevengeReason("\"heat\": 30, \"windowSeconds\": 900, \"warnAt\": " + warnAt));

    [Theory]
    [InlineData("\"shared\": true", "unknown field trigger.shared")]
    [InlineData("\"kills\": 20", "unknown field trigger.kills")]
    [InlineData("\"bogus\": 1", "unknown field trigger.bogus")]
    public void Revenge_fails_when_shared_or_an_unknown_key_loads(string key, string reason) =>
        Assert.Equal(reason, RevengeReason("\"heat\": 30, \"windowSeconds\": 900, " + key));

    [Fact]
    public void Revenge_fails_when_a_reason_exceeds_120_bytes()
    {
        Assert.All(EventValidator.RevengeReasons, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));
        // each reason names its field (D1) or the trigger (D2)
        Assert.All(EventValidator.RevengeReasons, r => Assert.True(r.StartsWith("trigger.", StringComparison.Ordinal) || r.StartsWith("a Revenge trigger", StringComparison.Ordinal), r));
        // factions keep FactionNames' rules and reasons
        Assert.Equal(EventValidator.TriggerFactionsRule, RevengeEvent("{ \"type\": \"Revenge\", \"heat\": 30, \"windowSeconds\": 900 }").DisabledReason);
        Assert.Equal("unknown faction Faction_Nobody", RevengeEvent(RevengeTrigger("\"heat\": 30, \"windowSeconds\": 900").Replace("Faction_Bandits", "Faction_Nobody")).DisabledReason);
    }

    [Fact]
    public void Revenge_passes_every_key_at_its_bounds()
    {
        var low = RevengeEvent(RevengeTrigger("\"heat\": 3, \"windowSeconds\": 60, \"warnAt\": [1, 2], \"vbloodHeat\": 0, \"playerCooldownMinutes\": 0, \"scope\": [\"FarbaneWoods\"]"));
        Assert.Null(low.DisabledReason);
        var t = low.Trigger;
        Assert.Equal((TriggerType.Revenge, 3, 60, 0, 0), (t.Type, t.Heat, t.WindowSeconds, t.VBloodHeat, t.PlayerCooldownMinutes));
        Assert.Equal([1, 2], t.WarnAt);
        Assert.True(t.Scope.Names("FarbaneWoods"));
        var high = RevengeEvent("{ \"type\": \"Revenge\", \"factions\": [\"Faction_Bandits\", \"Faction_Legion\", \"Faction_Undead\", \"Faction_Militia\", \"Faction_Gloomrot\"], " +
            "\"heat\": 500, \"windowSeconds\": 3600, \"warnAt\": [1, 250, 499], \"vbloodHeat\": 100, \"playerCooldownMinutes\": 1440 }");
        Assert.Null(high.DisabledReason);
        Assert.Equal((500, 3600, 100, 1440, 5), (high.Trigger.Heat, high.Trigger.WindowSeconds, high.Trigger.VBloodHeat, high.Trigger.PlayerCooldownMinutes, high.Trigger.Factions!.Count));
        Assert.Equal([1, 250, 499], high.Trigger.WarnAt);
    }

    [Fact]
    public void Revenge_empty_optional_keys_absent()
    {
        var d = RevengeEvent(RevengeTrigger("\"heat\": 30, \"windowSeconds\": 900"));
        Assert.Null(d.DisabledReason);
        Assert.Equal((10, 30), (d.Trigger.VBloodHeat, d.Trigger.PlayerCooldownMinutes));
        Assert.Empty(d.Trigger.WarnAt!);
        Assert.Equal(EventValidator.DefaultVBloodHeat, d.Trigger.VBloodHeat);
        Assert.Null(RevengeReason("\"heat\": 30, \"windowSeconds\": 900, \"warnAt\": []"));
    }

    // ---- D2 RevengeAction

    public static IEnumerable<object[]> RevengeActionRefusals() =>
    [
        [Json.EmpowerAction(), EventValidator.RevengeNeedsWaves],
        [Action(location: "{ \"type\": \"Point\", \"x\": 0, \"z\": 0 }", extra: HuntAround), EventValidator.RevengeNeedsAroundPlayer],
        [Action(location: AroundPlayer(20, 40)), EventValidator.RevengeNeedsHunt],
        [Action(location: AroundPlayer(20, 40), extra: HuntAround + ", \"fanOut\": { \"maxInstances\": 3, \"minSpacing\": 150 }"), EventValidator.RevengeNoFanOut],
        [Action(location: AroundPlayer(20, 40), extra: HuntAround + ", \"allowTerritory\": true"), EventValidator.RevengeNoTerritory],
    ];

    [Theory]
    [MemberData(nameof(RevengeActionRefusals))]
    public void RevengeAction_fails_when_a_combination_loads(string action, string reason)
    {
        var json = Json.Event(trigger: Json.RevengeTrigger, action: action);
        if (action.Contains("Empower", StringComparison.Ordinal)) json = json.Replace("\"pillar\": \"spawns\"", "\"pillar\": \"empowerment\"");
        Assert.Equal(reason, Mapped(json).DisabledReason);
    }

    [Fact]
    public void RevengeAction_passes_units_and_wave_list_forms()
    {
        Assert.Null(RevengeEvent(Json.RevengeTrigger).DisabledReason);
        var list = "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ] }, " +
            "{ \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 6 } ], \"afterSeconds\": 120, \"whenCleared\": true } ], \"radius\": 10, " +
            "\"location\": " + AroundPlayer(20, 40) + ", " + HuntAround + ", \"scoreboard\": true }";
        var d = RevengeEvent(Json.RevengeTrigger, list);
        Assert.Null(d.DisabledReason);
        Assert.Equal(2, d.Action!.WaveList!.Count);
    }

    [Fact]
    public void RevengeAction_passes_other_triggers_unchanged()
    {
        // the five combinations a Revenge trigger refuses load under every other trigger as before
        foreach (var row in RevengeActionRefusals().Where(r => !((string)r[0]).Contains("Empower", StringComparison.Ordinal)))
            foreach (var t in new[] { "{ \"type\": \"Manual\" }", "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }" })
            {
                var reason = Mapped(Json.Event(trigger: t, action: (string)row[0])).DisabledReason;
                Assert.False(reason is not null && reason.StartsWith("a Revenge trigger", StringComparison.Ordinal), reason);
            }
        Assert.Null(Mapped(Json.Event(trigger: "{ \"type\": \"Manual\" }", action: Action(location: AroundPlayer(20, 40)))).DisabledReason);
    }

    [Fact]
    public void RevengeAction_empty_no_action() =>
        Assert.Equal("action is required", Mapped(Json.Event(trigger: Json.RevengeTrigger, action: "\"announce\": null").Replace(", \"announce\": null", "")).DisabledReason);
}
