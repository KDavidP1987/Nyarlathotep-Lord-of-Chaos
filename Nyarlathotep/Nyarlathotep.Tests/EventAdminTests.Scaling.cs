using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D14: `.nyar event set &lt;id&gt; action.scaling.&lt;key&gt; &lt;value&gt;` sets one key (creating the
/// block), `action.scaling none` removes it, each through D1's rules and reasons; a value out of range, a set that leaves a
/// block D1 refuses, or a field on an Empower definition changes nothing; one value only; every reply within 480 bytes;
/// docs/NYARLATHOTEP_DESIGN.md §6 lists the five fields.</summary>
public partial class EventAdminTests
{
    /// <summary>raid (SpawnWaves without the block), scaled (SpawnWaves with { unitsPerPlayer 2 }), apo (an Apocalypse) and
    /// surge (Empower).</summary>
    static Library ScalingLib() => AuthoringTests.Lib(Json.Event("raid"),
        Json.Event("scaled", action: Json.ValidAction[..^2] + ", \"scaling\": { \"unitsPerPlayer\": 2 } }"), Apo.Event(), Json.Empower("surge"));

    static Scaling? ScalingOf(Library lib, string id) =>
        lib.Catalog.Current.Find(id) is { } d ? d.Action?.Scaling ?? d.Apocalypse?.Scaling : null;

    static JsonNode? ScalingJson(Library lib, string id) =>
        ((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id)["action"]?["scaling"];

    /// <summary>Asserts the set is refused with <paramref name="reason"/> and writes nothing.</summary>
    static void ScalingRefused(Library lib, string id, string field, string value, string reason)
    {
        var (hash, bak, changed) = (lib.Hash, lib.Bak, lib.ConfigChanged);
        var reply = AuthoringTests.Set(lib, id, field, value);
        Assert.Equal(reason, reply);
        Assert.Equal((hash, bak, changed), (lib.Hash, lib.Bak, lib.ConfigChanged));
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes);
    }

    static readonly string[] ScalingRows =
    [
        "| `action.scaling` | action | admin |", "| `action.scaling.unitsPerPlayer` | action | admin |", "| `action.scaling.levelFromPlayers` | action | admin |",
        "| `action.scaling.levelOffset` | action | admin |", "| `action.scaling.levelPerWave` | action | admin |",
    ];

    /// <summary>What keeps design §6 from listing the scaling fields; empty when it does.</summary>
    internal static List<string> ScalingDesignProblems(string design)
    {
        var d = design.Replace("\r\n", "\n", StringComparison.Ordinal);
        return ScalingRows.Where(r => !d.Contains(r, StringComparison.Ordinal)).Select(r => $"§6 misses {r.Split('`')[1]}").ToList();
    }

    [Theory]
    [InlineData("action.scaling.unitsPerPlayer", "6", EventValidator.UnitsPerPlayerRule)]
    [InlineData("action.scaling.unitsPerPlayer", "-1", EventValidator.UnitsPerPlayerRule)]
    [InlineData("action.scaling.unitsPerPlayer", "1.5", EventValidator.UnitsPerPlayerRule)]
    [InlineData("action.scaling.levelOffset", "21", EventValidator.LevelOffsetRule)]
    [InlineData("action.scaling.levelOffset", "-21", EventValidator.LevelOffsetRule)]
    [InlineData("action.scaling.levelPerWave", "6", EventValidator.LevelPerWaveRule)]
    [InlineData("action.scaling.levelPerWave", "-1", EventValidator.LevelPerWaveRule)]
    [InlineData("action.scaling.levelFromPlayers", "yes", EventValidator.LevelFromPlayersRule)]
    [InlineData("action.scaling.levelFromPlayers", "1", EventValidator.LevelFromPlayersRule)]
    [InlineData("action.scaling", "2", CommandArgs.ScalingNoneRule)]
    public void Scaling_fails_when_a_field_outside_its_range_is_stored(string field, string value, string reason)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(reason, arg.Error);
        var lib = ScalingLib();
        foreach (var id in new[] { "raid", "scaled", "apo" }) ScalingRefused(lib, id, field, value, reason);
        Assert.Equal(new Scaling(2), ScalingOf(lib, "scaled"));
    }

    [Fact]
    public void Scaling_fails_when_an_empower_definition_takes_a_field()
    {
        var lib = ScalingLib();
        foreach (var (field, value) in new[] { ("action.scaling.unitsPerPlayer", "2"), ("action.scaling.levelFromPlayers", "true"), ("action.scaling", "none") })
            ScalingRefused(lib, "surge", field, value, EventValidator.ScalingAction);
    }

    [Fact]
    public void Scaling_fails_when_a_set_leaves_a_block_the_validator_refuses()
    {
        var lib = ScalingLib();
        ScalingRefused(lib, "raid", "action.scaling.levelOffset", "2", EventValidator.ScalingOffsetNeedsLevel);   // the new block lacks levelFromPlayers
        ScalingRefused(lib, "raid", "action.scaling.levelFromPlayers", "false", EventValidator.ScalingNothing);
        ScalingRefused(lib, "raid", "action.scaling.unitsPerPlayer", "0", EventValidator.ScalingNothing);
        ScalingRefused(lib, "scaled", "action.scaling.unitsPerPlayer", "0", EventValidator.ScalingNothing);
        ScalingRefused(lib, "scaled", "action.scaling.levelPerWave", "1", EventValidator.ScalingOffsetNeedsLevel);
        Assert.Null(ScalingOf(lib, "raid"));
        Assert.Null(ScalingJson(lib, "raid"));
    }

    [Fact]
    public void Scaling_fails_when_none_leaves_the_block()
    {
        var lib = ScalingLib();
        Assert.Equal("event scaled action.scaling = none", AuthoringTests.Set(lib, "scaled", "action.scaling", "none"));
        Assert.Null(ScalingOf(lib, "scaled"));
        Assert.Null(ScalingJson(lib, "scaled"));
        Assert.True(lib.Catalog.Current.Find("scaled")!.Startable);
    }

    [Fact]
    public void Scaling_fails_when_a_missing_or_surplus_argument_changes_anything()
    {
        foreach (var field in CommandArgs.ScalingFields)
        {
            Assert.Null(CommandForms.Check("event", ["set", "scaled", field, ""], out var usage));
            Assert.Equal("usage: .nyar event set id field value", usage);
            Assert.Null(CommandForms.Check("event", ["set", "scaled", field, "2", "3"], out var surplus));
            Assert.Equal("usage: .nyar event set id field value", surplus);
            Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue(field, "").Error);
        }
        // the twin: a missing value or a surplus word is badarg and changes nothing
        var r = HumanReplyTests.Of(Json.Event("raid"));
        var hash = r.Lib.Hash;
        Assert.Equal("[NYAR:err] cmd=event verb=set code=badarg arg=value", r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.unitsPerPlayer", "", ""));
        Assert.Equal("[NYAR:err] cmd=event verb=set code=badarg arg=extra", r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.unitsPerPlayer", "2", "3"));
        Assert.Equal(hash, r.Lib.Hash);
    }

    [Fact]
    public void Scaling_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = ScalingLib();
        var replies = new List<string>
        {
            AuthoringTests.Set(lib, "raid", "action.scaling.unitsPerPlayer", "5"),
            AuthoringTests.Set(lib, "raid", "action.scaling.levelFromPlayers", "true"),
            AuthoringTests.Set(lib, "raid", "action.scaling.levelOffset", "-20"),
            AuthoringTests.Set(lib, "raid", "action.scaling.levelPerWave", "5"),
            AuthoringTests.Set(lib, "raid", "action.scaling", "none"),
            AuthoringTests.Set(lib, "surge", "action.scaling.unitsPerPlayer", "5"),
        };
        replies.AddRange(EventValidator.ScalingReasons);
        replies.Add(CommandArgs.ScalingNoneRule);
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, r));
    }

    [Fact]
    public void Scaling_fails_when_the_design_rows_are_missing()
    {
        Assert.Empty(ScalingDesignProblems(DesignText));
        Assert.Equal(["§6 misses action.scaling.levelOffset"], ScalingDesignProblems(DesignText.Replace("| `action.scaling.levelOffset` | action | admin |", "", StringComparison.Ordinal)));
        Assert.Equal(5, ScalingDesignProblems("").Count);
        // the §6 table and SettableFields name the same five fields
        Assert.Equal(CommandArgs.ScalingFields, ScalingRows.Select(r => r.Split('`')[1]));
        Assert.All(CommandArgs.ScalingFields, f => Assert.Equal(("action", "admin"), CommandArgs.SettableFields[f]));
    }

    [Fact]
    public void Scaling_passes_set_creates_the_block()
    {
        var lib = ScalingLib();
        Assert.Equal("event raid action.scaling.unitsPerPlayer = 2", AuthoringTests.Set(lib, "raid", "action.scaling.unitsPerPlayer", "2"));
        Assert.Equal("event raid action.scaling.levelFromPlayers = true", AuthoringTests.Set(lib, "raid", "action.scaling.levelFromPlayers", "true"));
        Assert.Equal("event raid action.scaling.levelOffset = -3", AuthoringTests.Set(lib, "raid", "action.scaling.levelOffset", "-3"));
        Assert.Equal("event raid action.scaling.levelPerWave = 1", AuthoringTests.Set(lib, "raid", "action.scaling.levelPerWave", "1"));
        Assert.Equal(new Scaling(2, true, -3, 1), ScalingOf(lib, "raid"));
        Assert.Equal("event apo action.scaling.levelFromPlayers = true", AuthoringTests.Set(lib, "apo", "action.scaling.levelFromPlayers", "true"));
        Assert.Equal(new Scaling(0, true), ScalingOf(lib, "apo"));
    }

    [Fact]
    public void Scaling_empty_value_gets_value_required()
    {
        var lib = ScalingLib();
        foreach (var field in CommandArgs.ScalingFields)
            ScalingRefused(lib, "scaled", field, "", CommandArgs.ValueRequired);
    }
}
