using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D18: `.nyar event set` for the new SpawnWaves keys, each a whole-file edit through the
/// event-library editor, refused with its rule (the file unchanged) when out of range, on an Empower definition, or as a
/// second level form.</summary>
public partial class AuthoringTests
{
    static JsonObject ActionOf(Library lib, string id) => Entry(lib.Text, id)["action"]!.AsObject();

    public static TheoryData<string, string, string> BadSpawnValues => new()
    {
        { "action.modifiers.level", "121", EventValidator.LevelRule },
        { "action.modifiers.levelDelta", "6", EventValidator.LevelDeltaRule },
        { "action.modifiers.levelDelta", "2.5", EventValidator.LevelDeltaRule },
        { "action.modifiers.maxHealth", "3.01", EventValidator.ModifierRule("maxHealth") },
        { "action.modifiers.power", "0.49", EventValidator.ModifierRule("power") },
        { "action.modifiers.moveSpeed", "1.234", EventValidator.ModifierRule("moveSpeed") },
        { "action.modifiers.attackSpeed", "fast", EventValidator.ModifierRule("attackSpeed") },
        { "action.units.1.chance", "0.04", CommandArgs.UnitChanceRule },
        { "action.units.1.chance", "1.01", CommandArgs.UnitChanceRule },
        { "action.units.1.chance", "none", CommandArgs.UnitChanceRule },
        { "action.units.2.chance", "0.5", "action.units has no entry 2" },
        { "action.units.11.chance", "0.5", "field action.units.11.chance is not settable; edit events.json and reload" },
        { "action.loot", "yes", "action.loot must be true or false" },
        { "action.allowTerritory", "none", "action.allowTerritory must be true or false" },
        { "action.behaviour", "hunt 9", EventValidator.HuntRangeRule },
        { "action.behaviour", "hunt 61", EventValidator.HuntRangeRule },
        { "action.behaviour", "guard", "unknown behaviour type guard" },
        { "action.behaviour", "Ambush 20", "unknown behaviour type ambush" },
        { "location", "aroundplayer 40 30", EventValidator.DistOrder },
        { "location", "aroundplayer 9 30", EventValidator.MinDistRule },
        { "location", "aroundplayer 20 81", EventValidator.MaxDistRule },
        { "action.spawnVisual", "on", "field action.spawnVisual is not settable; edit events.json and reload" },
    };

    [Theory]
    [MemberData(nameof(BadSpawnValues))]
    public void Spawns_fails_when_value_refused(string field, string value, string reply)
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, "raid", field, value)));
        Assert.Null(ActionOf(lib, "raid")["spawnVisual"]);
    }

    [Theory]
    [InlineData("action.modifiers.level", "30", "action.modifiers.levelDelta", "2")]
    [InlineData("action.modifiers.levelDelta", "-2", "action.modifiers.level", "60")]
    public void Spawns_fails_when_second_level_form(string first, string value, string second, string secondValue)
    {
        var lib = Lib(Json.Event("raid"));
        Assert.Equal($"event raid {first} = {value}", Set(lib, "raid", first, value));
        Unchanged(lib, () => Assert.Equal($"{EventValidator.BothLevels}: set {first} none first", Set(lib, "raid", second, secondValue)));
        Assert.Null(lib.Catalog.Current.Find("raid")!.DisabledReason);
    }

    [Theory]
    [InlineData("action.modifiers.maxHealth", "1.5", "action.modifiers.maxHealth is not an Empower field")]
    [InlineData("action.modifiers.level", "30", "action.modifiers.level is not an Empower field")]
    [InlineData("action.loot", "true", "action.loot is not an Empower field")]
    [InlineData("action.allowTerritory", "true", "action.allowTerritory is not an Empower field")]
    [InlineData("action.behaviour", "hunt 40", "action.behaviour is not an Empower field")]
    [InlineData("action.behaviour", "none", "action.behaviour is not an Empower field")]
    [InlineData("action.units.1.chance", "0.5", "action.units.<n>.chance is not an Empower field")]
    [InlineData("location", "aroundplayer 20 40", "location is a SpawnWaves field")]
    public void Spawns_fails_when_empower_definition(string field, string value, string reply)
    {
        var lib = Lib(Json.Event("raid"), Json.Empower("surge"));
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, "surge", field, value)));
    }

    [Fact]
    public void Spawns_passes_valid_set_changes_file()
    {
        var lib = Lib(Json.Event("raid"));
        string Changed(string field, string value, string reply)
        {
            var hash = lib.Hash;
            Assert.Equal(reply, Set(lib, "raid", field, value));                        // exact: a boolean reads true, never True (A50)
            Assert.NotEqual(hash, lib.Hash);
            return lib.Text;
        }
        SpawnWavesAction Loaded() => lib.Catalog.Current.Find("raid")!.Action!;

        Changed("action.modifiers.maxHealth", "1.5", "event raid action.modifiers.maxHealth = 1.5");
        Changed("action.modifiers.levelDelta", "-2", "event raid action.modifiers.levelDelta = -2");
        Assert.Equal("{\"maxHealth\":1.5,\"levelDelta\":-2}", ActionOf(lib, "raid")["modifiers"]!.ToJsonString());
        Assert.Equal(new SpawnModifiers(LevelDelta: -2, MaxHealth: 1.5), Loaded().Modifiers);
        Changed("action.modifiers.levelDelta", "none", "event raid action.modifiers.levelDelta = none");
        Assert.Equal("{\"maxHealth\":1.5}", ActionOf(lib, "raid")["modifiers"]!.ToJsonString());
        Changed("action.modifiers.maxHealth", "none", "event raid action.modifiers.maxHealth = none");
        Assert.Null(ActionOf(lib, "raid")["modifiers"]);                        // the emptied object goes too
        Assert.Null(Loaded().Modifiers);

        Changed("action.loot", "true", "event raid action.loot = true");
        Changed("action.allowTerritory", "true", "event raid action.allowTerritory = true");
        Assert.True((bool)ActionOf(lib, "raid")["loot"]!);
        Assert.True((Loaded().Loot, Loaded().AllowTerritory) == (true, true));
        Changed("action.loot", "false", "event raid action.loot = false");
        Assert.False((bool)ActionOf(lib, "raid")["loot"]!);

        Changed("action.behaviour", "hunt 40", "event raid action.behaviour = hunt 40");
        Assert.Equal("{\"type\":\"Hunt\",\"range\":40}", ActionOf(lib, "raid")["behaviour"]!.ToJsonString());
        Changed("action.units.1.chance", "0.5", "event raid action.units.1.chance = 0.5");
        Assert.Equal("{\"prefab\":\"CHAR_Bandit_Thug\",\"count\":5,\"chance\":0.5}", ActionOf(lib, "raid")["units"]![0]!.ToJsonString());
        Assert.Equal(0.5, Loaded().Units[0].Chance);
        Changed("location", "aroundplayer 20 40", "event raid action.location = aroundplayer 20 40");
        Assert.Equal("{\"type\":\"AroundPlayer\",\"minDist\":20,\"maxDist\":40}", ActionOf(lib, "raid")["location"]!.ToJsonString());
        Assert.Equal((LocationType.AroundPlayer, 20, 40), (Loaded().Location.Type, Loaded().Location.MinDist, Loaded().Location.MaxDist));
        Assert.Null(lib.Catalog.Current.Find("raid")!.DisabledReason);
        Assert.Equal(new Behaviour(BehaviourType.Hunt, 40), Loaded().Behaviour);

        Assert.Equal("event raid action.location = aroundplayer 20 50; now disabled: " + EventValidator.MaxDistOverRange,
            Set(lib, "raid", "location", "aroundplayer 20 50"));                  // a valid set the reload disables
        Changed("action.behaviour", "none", "event raid action.behaviour = none");
        Assert.Null(ActionOf(lib, "raid")["behaviour"]);
        Assert.Null(lib.Catalog.Current.Find("raid")!.DisabledReason);
    }

    [Fact]
    public void Spawns_empty_value()
    {
        var lib = Lib(Json.Event("raid"));
        foreach (var field in CommandArgs.SpawnKeyFields.Select(f => f.Replace("<n>", "1", StringComparison.Ordinal)).Append("location"))
            foreach (var value in new[] { "", " " })
                Unchanged(lib, () => Assert.False(CommandArgs.SettableValue(field, value).Ok, field));
        Unchanged(lib, () => Assert.Equal("value required", Set(lib, "raid", "action.loot", "")));
        Assert.Null(CommandForms.Check("event", ["set", "raid", "action.loot", ""], out var usage));
        Assert.Equal("usage: .nyar event set id field value", usage);
    }

    [Fact]
    public void Spawns_fails_when_refusal_names_wrong_argument()
    {
        // A50: a unit chance out of range is a bad value, not a bad field, although SettableFields lists only the
        // placeholder action.units.<n>.chance; a field that is not settable stays a bad field.
        var r = HumanReplyTests.Default();
        var bad = r.Flows.Set(r.Who, "raid", "action.units.1.chance", "0.01");
        Assert.Equal((RefusalCode.BadArg, "value"), (bad.Code!.Value, bad.Arg));
        var field = r.Flows.Set(r.Who, "raid", "action.nope", "1");
        Assert.Equal("field", field.Arg);
    }
}
