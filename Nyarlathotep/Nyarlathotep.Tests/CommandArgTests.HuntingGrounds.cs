using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D14: `location region`, `action.regionLock` and `action.behaviour regionhunt` take exactly what
/// the validator takes and refuse with its reason; the cross-key conflicts (a Global scope under a Region location or a
/// lock, an arena on a locked event, the lock removed under RegionHunt, fanOut beside a Region location) are refused
/// before anything is written; every reply fits 480 bytes; the §6 table lists the new values.</summary>
public partial class CommandArgTests
{
    const string HgPoint = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";

    static string HgJson(string location, string extra) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ], \"waves\": 3, " +
        "\"intervalSeconds\": 60, \"radius\": 10, \"location\": " + location + (extra == "" ? "" : ", " + extra) + " }";

    /// <summary>raid (Point, Global), scoped (Point, FarbaneWoods), region (Region location), locked (Point, lock),
    /// hunter (Region, lock, RegionHunt), arena (Point, scope, arena 30), fanned (AroundPlayer, scope, fanOut).</summary>
    static Library HgLib()
    {
        var units = AuthoringTests.Units();
        units.Regions = FakeRegions.All();
        return new Library(Json.File(
            Json.Event("raid"),
            Json.Event("scoped", action: HgJson(HgPoint, "\"scope\": [\"FarbaneWoods\"]")),
            Json.Event("region", action: HgJson("{ \"type\": \"Region\" }", "\"scope\": [\"FarbaneWoods\"]")),
            Json.Event("locked", action: HgJson(HgPoint, "\"scope\": [\"FarbaneWoods\"], \"regionLock\": true")),
            Json.Event("hunter", action: HgJson("{ \"type\": \"Region\" }", "\"scope\": [\"FarbaneWoods\"], \"regionLock\": true, \"behaviour\": { \"type\": \"RegionHunt\" }")),
            Json.Event("arena", action: HgJson(HgPoint, "\"scope\": [\"FarbaneWoods\"], \"arena\": { \"radius\": 30 }")),
            Json.Event("fanned", action: HgJson("{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }",
                "\"scope\": [\"FarbaneWoods\"], \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }"))), units);
    }

    static EventDefinition HgLoaded(Library lib, string id)
    {
        var d = lib.Catalog.Current.Find(id)!;
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    static JsonObject HgAction(Library lib, string id) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id)["action"]!;

    static void HgRefused(Library lib, string id, string field, string value, string reason)
    {
        var hash = lib.Hash;
        var reply = AuthoringTests.Set(lib, id, field, value);
        Assert.Equal(reason, reply);
        Assert.Equal(hash, lib.Hash);                                                                   // nothing written
        Assert.True(lib.Catalog.Current.Find(id)!.Startable);
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes);
    }

    [Theory]
    [InlineData("location", "regions", CommandArgs.LocationSetRule)]
    [InlineData("location", "region FarbaneWoods", CommandArgs.LocationSetRule)]
    [InlineData("action.regionLock", "yes", EventValidator.RegionLockRule)]
    [InlineData("action.regionLock", "1", EventValidator.RegionLockRule)]
    [InlineData("action.regionLock", "True", EventValidator.RegionLockRule)]
    [InlineData("action.behaviour", "regionhunt 30", CommandArgs.BehaviourRule)]
    [InlineData("action.behaviour", "regionhunt 1 2", CommandArgs.BehaviourRule)]
    public void HuntingGrounds_fails_when_value_out_of_range(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
        Assert.Contains(field == "location" ? "location" : field, error);                               // the reason names the field
    }

    [Fact]
    public void HuntingGrounds_passes_values_the_validator_loads()
    {
        Assert.IsType<RegionArg>(CommandArgs.SettableValue("location", "region").Value);
        Assert.IsType<RegionArg>(CommandArgs.SettableValue("location", "REGION").Value);
        Assert.IsType<RegionHuntArg>(CommandArgs.SettableValue("action.behaviour", "regionhunt").Value);
        Assert.Equal(true, CommandArgs.SettableValue("action.regionLock", "true").Value);
        Assert.Equal(false, CommandArgs.SettableValue("action.regionLock", "false").Value);
        Assert.IsType<FieldRemoval>(CommandArgs.SettableValue("action.regionLock", "none").Value);

        var lib = HgLib();
        Assert.StartsWith("event scoped action.location = ", AuthoringTests.Set(lib, "scoped", "location", "region"));
        Assert.Equal(LocationType.Region, HgLoaded(lib, "scoped").Action!.Location.Type);
        Assert.Equal("Region", (string?)HgAction(lib, "scoped")["location"]!["type"]);
        Assert.StartsWith("event scoped action.regionLock = true", AuthoringTests.Set(lib, "scoped", "action.regionLock", "true"));
        Assert.True(HgLoaded(lib, "scoped").Action!.RegionLock);
        Assert.StartsWith("event scoped action.behaviour = ", AuthoringTests.Set(lib, "scoped", "action.behaviour", "regionhunt"));
        Assert.Equal(new Behaviour(BehaviourType.RegionHunt, 0), HgLoaded(lib, "scoped").Action!.Behaviour);
        Assert.Equal("RegionHunt", (string?)HgAction(lib, "scoped")["behaviour"]!["type"]);
        Assert.StartsWith("event scoped action.behaviour = none", AuthoringTests.Set(lib, "scoped", "action.behaviour", "none"));
        Assert.StartsWith("event scoped action.regionLock = false", AuthoringTests.Set(lib, "scoped", "action.regionLock", "false"));
        Assert.False(HgLoaded(lib, "scoped").Action!.RegionLock);
        Assert.StartsWith("event scoped action.regionLock = none", AuthoringTests.Set(lib, "scoped", "action.regionLock", "none"));
        Assert.Null(HgAction(lib, "scoped")["regionLock"]);
        Assert.StartsWith("event locked action.regionLock = true", AuthoringTests.Set(lib, "locked", "action.regionLock", "true"));   // an Admin, AroundPlayer
        Assert.StartsWith("event fanned action.regionLock = true", AuthoringTests.Set(lib, "fanned", "action.regionLock", "true"));   // or Point lock alike
        Assert.True(HgLoaded(lib, "fanned").Action!.RegionLock);
        Assert.StartsWith("event raid action.regionLock = false", AuthoringTests.Set(lib, "raid", "action.regionLock", "false"));     // false needs no scope
    }

    [Fact]
    public void HuntingGrounds_fails_when_a_scope_goes_global_under_a_region_or_lock()
    {
        var lib = HgLib();
        HgRefused(lib, "region", "action.scope", "Global", EventValidator.RegionLocationScope);
        HgRefused(lib, "locked", "action.scope", "Global", EventValidator.RegionLockScope);
        HgRefused(lib, "raid", "location", "region", EventValidator.RegionLocationScope);
        HgRefused(lib, "raid", "action.regionLock", "true", EventValidator.RegionLockScope);
        Assert.StartsWith("event region action.scope = ", AuthoringTests.Set(lib, "region", "action.scope", "FarbaneWoods,CursedForest"));
        Assert.Equal(["FarbaneWoods", "CursedForest"], HgLoaded(lib, "region").Action!.Scope.Regions);
    }

    [Fact]
    public void HuntingGrounds_fails_when_an_arena_is_set_on_a_locked_event()
    {
        var lib = HgLib();
        HgRefused(lib, "locked", "action.arena", "30", EventValidator.RegionLockArena);
        HgRefused(lib, "arena", "action.regionLock", "true", EventValidator.RegionLockArena);
        HgRefused(lib, "arena", "location", "region", EventValidator.ArenaLocation);
        HgRefused(lib, "region", "action.arena", "30", EventValidator.ArenaLocation);
        Assert.StartsWith("event arena action.regionLock = false", AuthoringTests.Set(lib, "arena", "action.regionLock", "false"));
    }

    [Fact]
    public void HuntingGrounds_fails_when_the_lock_is_removed_under_region_hunt()
    {
        var lib = HgLib();
        HgRefused(lib, "hunter", "action.regionLock", "false", EventValidator.RegionHuntLock);
        HgRefused(lib, "hunter", "action.regionLock", "none", EventValidator.RegionHuntLock);
        HgRefused(lib, "scoped", "action.behaviour", "regionhunt", EventValidator.RegionHuntLock);
        Assert.StartsWith("event hunter action.behaviour = hunt 40", AuthoringTests.Set(lib, "hunter", "action.behaviour", "hunt 40"));
        Assert.StartsWith("event hunter action.regionLock = none", AuthoringTests.Set(lib, "hunter", "action.regionLock", "none"));
    }

    [Fact]
    public void HuntingGrounds_fails_when_fan_out_meets_a_region_location()
    {
        var lib = HgLib();
        HgRefused(lib, "region", "action.fanOut", "2 100", EventValidator.FanOutLocation);
        HgRefused(lib, "fanned", "location", "region", EventValidator.FanOutLocation + ": set action.fanOut none first");
        Assert.NotNull(HgAction(lib, "fanned")["fanOut"]);
    }

    [Fact]
    public void HuntingGrounds_passes_location_replaced_as_today()
    {
        var lib = HgLib();
        Assert.StartsWith("event region action.location = Point ", AuthoringTests.Set(lib, "region", "location", "here", (-5f, 1f, 6f)));
        Assert.Equal(LocationType.Point, HgLoaded(lib, "region").Action!.Location.Type);
        AuthoringTests.Set(lib, "region", "location", "region");
        Assert.StartsWith("event region action.location = ", AuthoringTests.Set(lib, "region", "location", "aroundplayer 20 40"));
        var around = HgLoaded(lib, "region").Action!.Location;
        Assert.Equal((LocationType.AroundPlayer, 20, 40), (around.Type, around.MinDist, around.MaxDist));
        Assert.Equal(["type", "minDist", "maxDist"], ((JsonObject)HgAction(lib, "region")["location"]!).Select(p => p.Key));   // no key of the old one left
    }

    [Fact]
    public void HuntingGrounds_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = HgLib();
        var replies = new List<string>
        {
            AuthoringTests.Set(lib, "raid", "location", "region"),
            AuthoringTests.Set(lib, "raid", "action.regionLock", "true"),
            AuthoringTests.Set(lib, "hunter", "action.regionLock", "none"),
            AuthoringTests.Set(lib, "locked", "action.arena", "60"),
            AuthoringTests.Set(lib, "fanned", "location", "region"),
            AuthoringTests.Set(lib, "region", "action.scope", string.Join(",", RegionNames.All)),
            AuthoringTests.Set(lib, "scoped", "action.behaviour", "regionhunt 5"),
            AuthoringTests.Set(lib, "scoped", "location", "region"),
        };
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(r)} bytes: {r}"));
    }

    [Fact]
    public void HuntingGrounds_fails_when_the_table_and_settable_fields_differ()
    {
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        Assert.Contains("| `action.regionLock` | spawn action | admin |", design);
        Assert.Equal(("spawn action", "admin"), CommandArgs.SettableFields[CommandArgs.RegionLockField]);
        Assert.Equal("action.regionLock", CommandArgs.RegionLockField);
        foreach (var value in new[] { "`location` also takes `region`", "`action.regionLock` takes `true`, `false` or `none`", "`action.behaviour` also takes `regionhunt`" })
            Assert.Contains(value, design);
    }

    [Fact]
    public void HuntingGrounds_empty_value()
    {
        Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue("action.regionLock", "").Error);
        Assert.False(CommandArgs.SettableValue("location", "").Ok);
        Assert.False(CommandArgs.SettableValue("action.behaviour", "").Ok);
    }
}
