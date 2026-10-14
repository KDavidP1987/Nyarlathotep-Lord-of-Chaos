using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D1: a SpawnWaves action (units form, waveList, growth) and an Apocalypse action take `"scaling": {
/// unitsPerPlayer 0-5, levelFromPlayers, levelOffset -20..20, levelPerWave 0-5 }`; each bound and key is refused with its
/// own reason within 120 bytes, as are a block that scales nothing, an offset or a per-wave step without levelFromPlayers,
/// and the block on an Empower action; a definition without it loads as in 0.13.0; the chat editor's writes and
/// `.nyar event reload` keep the block.</summary>
public partial class EventValidationTests
{
    static string ScalingBlock(string body) => "\"scaling\": " + body;

    /// <summary>The block on each action kind it belongs to: the units form, a waveList, a growth action and an apocalypse.</summary>
    static IEnumerable<EventDefinition> ScalingKinds(string body) =>
    [
        Json.One(Json.Event(action: Action(extra: ScalingBlock(body)))),
        Json.One(Json.Event(action: WaveListAction(List(Wave(), Wave(keys: "\"afterSeconds\": 60")), ScalingBlock(body)))),
        Json.One(Json.Event(action: Action(extra: GrowthKey("\"addUnits\": 1") + ", " + ScalingBlock(body)))),
        ApoLoad(Apo.Action(extra: ScalingBlock(body))),
    ];

    public static IEnumerable<object[]> ScalingRefusals() =>
    [
        ["3", EventValidator.ScalingObject],
        ["[ 1 ]", EventValidator.ScalingObject],
        ["{ \"unitsPerPlayer\": 6 }", EventValidator.UnitsPerPlayerRule],
        ["{ \"unitsPerPlayer\": -1 }", EventValidator.UnitsPerPlayerRule],
        ["{ \"unitsPerPlayer\": 1.5 }", EventValidator.UnitsPerPlayerRule],
        ["{ \"unitsPerPlayer\": \"2\" }", EventValidator.UnitsPerPlayerRule],
        ["{ \"levelFromPlayers\": true, \"levelOffset\": 21 }", EventValidator.LevelOffsetRule],
        ["{ \"levelFromPlayers\": true, \"levelOffset\": -21 }", EventValidator.LevelOffsetRule],
        ["{ \"levelFromPlayers\": true, \"levelPerWave\": 6 }", EventValidator.LevelPerWaveRule],
        ["{ \"levelFromPlayers\": true, \"levelPerWave\": -1 }", EventValidator.LevelPerWaveRule],
        ["{ \"levelFromPlayers\": \"yes\" }", EventValidator.LevelFromPlayersRule],
        ["{ \"levelFromPlayers\": 1 }", EventValidator.LevelFromPlayersRule],
        ["{ \"unitsPerPlayer\": 1, \"units\": 2 }", "unknown field action.scaling.units"],
        ["{ \"unitsPerPlayer\": 1, \"LevelOffset\": 2 }", "unknown field action.scaling.LevelOffset"],
    ];

    [Theory]
    [MemberData(nameof(ScalingRefusals))]
    public void Scaling_fails_when_a_bound_or_an_unknown_key_loads(string body, string reason)
    {
        foreach (var d in ScalingKinds(body))
        {
            Assert.False(d.Startable, body);
            Assert.Equal(reason, d.DisabledReason);
        }
        // the edges load
        Assert.All(ScalingKinds("{ \"unitsPerPlayer\": 5, \"levelFromPlayers\": true, \"levelOffset\": -20, \"levelPerWave\": 5 }"), d => Assert.True(d.Startable, d.DisabledReason));
        Assert.All(ScalingKinds("{ \"unitsPerPlayer\": 0, \"levelFromPlayers\": true, \"levelOffset\": 20, \"levelPerWave\": 0 }"), d => Assert.True(d.Startable, d.DisabledReason));
    }

    [Fact]
    public void Scaling_fails_when_a_reason_differs_or_exceeds_120_bytes()
    {
        Assert.Equal("action.scaling.unitsPerPlayer must be 0-5", EventValidator.UnitsPerPlayerRule);
        Assert.Equal("action.scaling.levelOffset must be -20..20", EventValidator.LevelOffsetRule);
        Assert.Equal("action.scaling.levelPerWave must be 0-5", EventValidator.LevelPerWaveRule);
        Assert.Equal("action.scaling.levelFromPlayers must be true or false", EventValidator.LevelFromPlayersRule);
        Assert.Equal("action.scaling must scale units or levels", EventValidator.ScalingNothing);
        Assert.Equal("action.scaling.levelOffset needs levelFromPlayers true", EventValidator.ScalingOffsetNeedsLevel);
        Assert.Equal("action.scaling needs a SpawnWaves or Apocalypse action", EventValidator.ScalingAction);
        Assert.All(EventValidator.ScalingReasons, r =>
        {
            Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r);
            Assert.StartsWith("action.scaling", r.Replace("unknown field ", "", StringComparison.Ordinal));     // each names the field
        });
        Assert.Equal(9, EventValidator.ScalingReasons.Count);
    }

    [Theory]
    [InlineData("{ }", EventValidator.ScalingNothing)]
    [InlineData("{ \"unitsPerPlayer\": 0, \"levelFromPlayers\": false }", EventValidator.ScalingNothing)]
    [InlineData("{ \"levelOffset\": 2 }", EventValidator.ScalingOffsetNeedsLevel)]
    [InlineData("{ \"unitsPerPlayer\": 1, \"levelPerWave\": 1 }", EventValidator.ScalingOffsetNeedsLevel)]
    [InlineData("{ \"unitsPerPlayer\": 1, \"levelFromPlayers\": false, \"levelOffset\": -1 }", EventValidator.ScalingOffsetNeedsLevel)]
    public void Scaling_fails_when_an_empty_or_offset_only_block_loads(string body, string reason) =>
        Assert.All(ScalingKinds(body), d => Assert.Equal((false, reason), (d.Startable, d.DisabledReason)));

    [Fact]
    public void Scaling_fails_when_an_empower_action_takes_the_block()
    {
        var d = Json.One(Json.Empower(action: Json.EmpowerAction(extra: ScalingBlock("{ \"unitsPerPlayer\": 2 }"))));
        Assert.False(d.Startable);
        Assert.Equal(EventValidator.ScalingAction, d.DisabledReason);
        // even a block the other kinds refuse gets this one reason
        Assert.Equal(EventValidator.ScalingAction, Json.One(Json.Empower(action: Json.EmpowerAction(extra: ScalingBlock("{ \"units\": 9 }")))).DisabledReason);
    }

    [Fact]
    public void Scaling_fails_when_a_definition_without_it_changes()
    {
        var plain = Json.One(Json.Event());
        Assert.True(plain.Startable);
        Assert.Null(plain.Action!.Scaling);
        Assert.Null(ApoLoad().Apocalypse!.Scaling);
        Assert.Null(WaveListEvent(List(Wave(), Wave(keys: "\"afterSeconds\": 60"))).Action!.Scaling);
        // the block changes nothing else of the definition
        var scaled = Json.One(Json.Event(action: Json.ValidAction[..^2] + ", " + ScalingBlock("{ \"unitsPerPlayer\": 2 }") + " }"));
        Assert.Equal(new Scaling(2), scaled.Action!.Scaling);
        var a = plain.Action;
        var b = scaled.Action with { Scaling = null, Units = a.Units };
        Assert.Equal(a, b);
        Assert.Equal(a.Units, scaled.Action.Units);
    }

    [Fact]
    public void Scaling_fails_when_the_editor_write_drops_or_alters_it()
    {
        const string body = "{ \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": -3, \"levelPerWave\": 1 }";
        var lib = AuthoringTests.Lib(Json.Event("raid", action: Action(extra: ScalingBlock(body))), Apo.Event());
        var want = new Scaling(2, true, -3, 1);
        Assert.Equal(want, lib.Catalog.Current.Find("raid")!.Action!.Scaling);
        Assert.StartsWith("event raid durationSeconds = 900", AuthoringTests.Set(lib, "raid", "durationSeconds", "900"));
        Assert.StartsWith("event raid name = ", AuthoringTests.Set(lib, "raid", "name", "Scaled"));
        Assert.Equal(want, lib.Catalog.Current.Find("raid")!.Action!.Scaling);
        var json = ((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == "raid")["action"]!["scaling"]!;
        Assert.Equal(JsonNode.Parse(body)!.ToJsonString(), json.ToJsonString());
        Assert.StartsWith("reloaded", lib.Editor.Reload(lib.Units).Human);                      // `.nyar event reload`
        Assert.Equal(want, lib.Catalog.Current.Find("raid")!.Action!.Scaling);
        // an apocalypse's block survives an edit of another field too
        Assert.StartsWith("event apo action.scaling.unitsPerPlayer = 3", AuthoringTests.Set(lib, "apo", "action.scaling.unitsPerPlayer", "3"));
        Assert.StartsWith("event apo action.hordes.size = 9", AuthoringTests.Set(lib, "apo", "action.hordes.size", "9"));
        Assert.Equal(new Scaling(3), lib.Catalog.Current.Find("apo")!.Apocalypse!.Scaling);
    }

    [Fact]
    public void Scaling_passes_every_key_in_range()
    {
        Assert.All(ScalingKinds("{ \"unitsPerPlayer\": 5, \"levelFromPlayers\": true, \"levelOffset\": -20, \"levelPerWave\": 5 }"),
            d => Assert.Equal(new Scaling(5, true, -20, 5), d.Action?.Scaling ?? d.Apocalypse!.Scaling));
    }

    [Fact]
    public void Scaling_empty_keys_take_their_defaults()
    {
        Assert.All(ScalingKinds("{ \"unitsPerPlayer\": 1 }"), d => Assert.Equal(new Scaling(1), d.Action?.Scaling ?? d.Apocalypse!.Scaling));
        Assert.All(ScalingKinds("{ \"levelFromPlayers\": true }"), d => Assert.Equal(new Scaling(0, true), d.Action?.Scaling ?? d.Apocalypse!.Scaling));
    }
}
