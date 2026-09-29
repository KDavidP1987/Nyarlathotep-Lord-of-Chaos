using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D6: the new SpawnWaves keys (modifiers, loot, behaviour, allowTerritory, units[].chance, the
/// AroundPlayer location), each failure disabling the event with one reason naming the field.</summary>
public partial class EventValidationTests
{
    static string Unit(string chance) => "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5, \"chance\": " + chance + " } ]";

    static string AroundPlayer(int min, int max) => "{ \"type\": \"AroundPlayer\", \"minDist\": " + min + ", \"maxDist\": " + max + " }";

    public static IEnumerable<object[]> SpawnKeysOutOfRange() =>
    [
        [Action(units: Unit("0.04")), EventValidator.ChanceRule],
        [Action(units: Unit("1.01")), EventValidator.ChanceRule],
        [Action(units: Unit("\"0.5\"")), EventValidator.ChanceRule],
        [Action(extra: "\"modifiers\": { \"levelDelta\": 6 }"), EventValidator.LevelDeltaRule],
        [Action(extra: "\"modifiers\": { \"levelDelta\": -6 }"), EventValidator.LevelDeltaRule],
        [Action(extra: "\"modifiers\": { \"levelDelta\": 2.5 }"), EventValidator.LevelDeltaRule],
        [Action(extra: "\"modifiers\": { \"level\": 121 }"), EventValidator.LevelRule],
        [Action(extra: "\"modifiers\": { \"level\": 30, \"levelDelta\": 2 }"), EventValidator.BothLevels],
        [Action(extra: "\"modifiers\": { \"maxHealth\": 0.49 }"), EventValidator.ModifierRule("maxHealth")],
        [Action(extra: "\"modifiers\": { \"power\": 3.01 }"), EventValidator.ModifierRule("power")],
        [Action(extra: "\"modifiers\": { \"moveSpeed\": 1.234 }"), EventValidator.ModifierRule("moveSpeed")],
        [Action(extra: "\"modifiers\": { \"attackSpeed\": \"1.5\" }"), EventValidator.ModifierRule("attackSpeed")],
        [Action(extra: "\"modifiers\": { }"), EventValidator.EmptyModifiers],
        [Action(extra: "\"loot\": \"true\""), "action.loot must be true or false"],
        [Action(extra: "\"allowTerritory\": 1"), "action.allowTerritory must be true or false"],
        [Action(extra: "\"behaviour\": { \"type\": \"Hunt\", \"range\": 9 }"), EventValidator.HuntRangeRule],
        [Action(extra: "\"behaviour\": { \"type\": \"Hunt\", \"range\": 61 }"), EventValidator.HuntRangeRule],
        [Action(extra: "\"behaviour\": { \"type\": \"Guard\", \"range\": 20 }"), EventValidator.UnknownBehaviour("Guard")],
        [Action(extra: "\"behaviour\": { \"type\": \"Ambush\" }"), EventValidator.UnknownBehaviour("Ambush")],
        [Action(extra: "\"behaviour\": { \"type\": \"Patrol\", \"range\": 20 }"), EventValidator.UnknownBehaviour("Patrol")],
        [Action(location: AroundPlayer(40, 30)), EventValidator.DistOrder],
        [Action(location: AroundPlayer(9, 30)), EventValidator.MinDistRule],
        [Action(location: AroundPlayer(20, 81)), EventValidator.MaxDistRule],
        [Action(location: AroundPlayer(20, 70), extra: "\"behaviour\": { \"type\": \"Hunt\", \"range\": 60 }"), EventValidator.MaxDistOverRange],
    ];

    [Theory]
    [MemberData(nameof(SpawnKeysOutOfRange))]
    public void Spawns_fails_when_key_out_of_range(string action, string reason)
    {
        var d = Json.One(Json.Event(action: action));
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
    }

    [Theory]
    [InlineData("\"spawnVisual\": true", "unknown field action.spawnVisual")]
    [InlineData("\"modifiers\": { \"armor\": 1.5 }", "unknown field action.modifiers.armor")]
    [InlineData("\"behaviour\": { \"type\": \"Hunt\", \"range\": 20, \"stealth\": true }", "unknown field action.behaviour.stealth")]
    public void Spawns_fails_when_unknown_key(string extra, string reason) =>
        Assert.Equal(reason, Json.One(Json.Event(action: Action(extra: extra))).DisabledReason);

    [Fact]
    public void Spawns_passes_every_new_key()
    {
        var d = Json.One(Json.Event(action: Action(
            units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5, \"chance\": 0.05 }, { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 2 } ]",
            location: AroundPlayer(10, 60),
            extra: "\"modifiers\": { \"levelDelta\": -5, \"maxHealth\": 0.5, \"power\": 3.0, \"moveSpeed\": 1.25, \"attackSpeed\": 1.5 }, " +
                   "\"loot\": true, \"behaviour\": { \"type\": \"Hunt\", \"range\": 60 }, \"allowTerritory\": true")));
        Assert.Null(d.DisabledReason);
        Assert.True(d.Startable);
        var a = d.Action!;
        Assert.Equal([0.05, 1.0], a.Units.Select(u => u.Chance));
        Assert.Equal(new SpawnModifiers(null, -5, 0.5, 3.0, 1.25, 1.5), a.Modifiers);
        Assert.True(a.Loot);
        Assert.True(a.AllowTerritory);
        Assert.Equal(new Behaviour(BehaviourType.Hunt, 60), a.Behaviour);
        Assert.Equal((LocationType.AroundPlayer, 10, 60), (a.Location.Type, a.Location.MinDist, a.Location.MaxDist));
        Assert.Equal(new SpawnModifiers(Level: 120), Json.One(Json.Event(action: Action(extra: "\"modifiers\": { \"level\": 120 }"))).Action!.Modifiers);
        Assert.False(Json.One(Json.Event(action: Action(extra: "\"loot\": false, \"allowTerritory\": false"))).Action!.Loot);
    }

    [Theory]
    [InlineData("{ \"type\": \"Manual\" }")]
    [InlineData("{ \"type\": \"Schedule\", \"days\": [\"Sat\"], \"times\": [\"20:00\"] }")]
    [InlineData("{ \"type\": \"GameTime\", \"phase\": \"night\" }")]
    [InlineData("{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }")]
    public void Spawns_passes_every_trigger_takes_aroundplayer(string trigger)
    {
        var d = Json.One(Json.Event(trigger: trigger, action: Action(location: AroundPlayer(20, 40))));
        Assert.Null(d.DisabledReason);
        Assert.Equal(LocationType.AroundPlayer, d.Action!.Location.Type);
    }

    [Fact]
    public void Spawns_passes_definition_without_new_keys_unchanged()
    {
        // a 0.6.0 definition: every key it had, none of the new ones
        var a = Json.One(Json.Event(action: Action(
            units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 }, { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 1 } ]",
            location: "{ \"type\": \"Point\", \"x\": -1200.5, \"y\": 42, \"z\": 800 }", extra: "\"unitLifetimeSeconds\": 300"))).Action!;
        var expected = new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 5), new UnitEntry("CHAR_Bandit_Deadeye", 1)], 3, 60, 10,
            new Location(LocationType.Point, -1200.5f, 800f, 42f), 300);
        Assert.Equal(expected.Units, a.Units);
        Assert.Equal(expected, a with { Units = expected.Units });
        Assert.Null(a.Modifiers);
        Assert.Null(a.Behaviour);
        Assert.False(a.Loot);
        Assert.False(a.AllowTerritory);
        Assert.Null(EventLines.SpawnKeys(a));
    }

    [Fact]
    public void Spawns_empty_modifiers_object()
    {
        Assert.Equal(EventValidator.EmptyModifiers, Json.One(Json.Event(action: Action(extra: "\"modifiers\": {}"))).DisabledReason);
        Assert.Equal("action.modifiers must name at least one modifier", EventValidator.EmptyModifiers);
    }
}
