using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D14: the `.nyar event set` fields of a wave set, each value in D1's range with the validator's own
/// reason; the one-way conversion of a units-form event; appending and removing waves; every reply within 480 bytes; the
/// §6 table the same as SettableFields.</summary>
public partial class CommandArgTests
{
    const string WaveListJson =
        "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ] }, " +
        "{ \"units\": [ { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 3, \"modifiers\": { \"level\": 30 } } ], \"whenCleared\": true } ], " +
        "\"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }";

    static Library WaveLib() => AuthoringTests.Lib(
        Json.Event("raid", action: Json.ValidAction.TrimEnd('}') + ", \"modifiers\": { \"power\": 1.5 } }"),
        Json.Event("ws", action: WaveListJson),
        Json.Empower("surge"));

    static JsonObject WaveAction(Library lib, string id) =>
        JsonNode.Parse(lib.Text)!["events"]!.AsArray().First(e => (string?)e!["id"] == id)!["action"]!.AsObject();

    [Theory]
    [InlineData("action.scoreboard", "yes", EventValidator.ScoreboardRule)]
    [InlineData("action.scoreboard", "True", EventValidator.ScoreboardRule)]
    [InlineData("action.waveList.2.afterSeconds", "9", "action.waveList.2.afterSeconds must be 10-3600")]
    [InlineData("action.waveList.2.afterSeconds", "3601", "action.waveList.2.afterSeconds must be 10-3600")]
    [InlineData("action.waveList.2.afterSeconds", "+60", "action.waveList.2.afterSeconds must be 10-3600")]
    [InlineData("action.waveList.2.whenCleared", "1", "action.waveList.2.whenCleared must be true or false")]
    [InlineData("action.waveList.1.units.1.level", "121", "action.waveList.1.units.1.modifiers.level must be 1-120")]
    [InlineData("action.waveList.1.units.1.levelDelta", "6", "action.waveList.1.units.1.modifiers.levelDelta must be -5..5")]
    [InlineData("action.waveList.1.units.1.maxHealth", "3.01",
        "action.waveList.1.units.1.modifiers.maxHealth must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.waveList.1.units.1.power", "1.234",
        "action.waveList.1.units.1.modifiers.power must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.waveList.1.units", "CHAR_Bandit_Thug:51",
        "action.waveList.1.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated")]
    [InlineData("action.waveList.1.units", "CHAR_Bandit_Thug:0",
        "action.waveList.1.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated")]
    [InlineData("action.waveList.1", "remove", "action.waveList.1 takes none (removes the wave)")]
    public void WaveSets_fails_when_value_out_of_range(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
    }

    [Theory]
    [InlineData("action.waveList.0.units")]
    [InlineData("action.waveList.11.units")]
    [InlineData("action.waveList.01.units")]
    [InlineData("action.waveList.N.units")]
    [InlineData("action.waveList.1.units.M.level")]
    [InlineData("action.waveList.1.units.1.armor")]
    [InlineData("action.waveList.1.delay")]
    public void WaveSets_fails_when_field_not_settable(string field) => Assert.False(CommandArgs.IsSettable(field));

    [Fact]
    public void WaveSets_passes_values_in_range()
    {
        Assert.Equal(true, CommandArgs.SettableValue("action.scoreboard", "true").Value);
        Assert.Equal(10, CommandArgs.SettableValue("action.waveList.2.afterSeconds", "10").Value);
        Assert.Equal(3600, CommandArgs.SettableValue("action.waveList.10.afterSeconds", "3600").Value);
        Assert.IsType<FieldRemoval>(CommandArgs.SettableValue("action.waveList.2.afterSeconds", "none").Value);
        Assert.Equal(-5, CommandArgs.SettableValue("action.waveList.1.units.10.levelDelta", "-5").Value);
        var units = Assert.IsType<UnitEntry[]>(CommandArgs.SettableValue("action.waveList.1.units", "CHAR_Bandit_Thug:50,CHAR_Bandit_Thug").Value);
        Assert.Equal([("CHAR_Bandit_Thug", 50), ("CHAR_Bandit_Thug", 1)], units.Select(u => (u.Prefab, u.Count)));   // a prefab may repeat
        Assert.True(CommandArgs.IsSettable("action.waveList.10.units.10.attackSpeed"));
    }

    [Fact]
    public void WaveSets_passes_conversion_of_a_units_form_event()
    {
        var lib = WaveLib();
        var reply = AuthoringTests.Set(lib, "raid", "action.waveList.1.units", "CHAR_Bandit_Thug:4,CHAR_Bandit_Deadeye:2");
        Assert.EndsWith("; converted to a wave list; removed action.units, action.waves, action.intervalSeconds, action.modifiers", reply);
        var action = WaveAction(lib, "raid");
        foreach (var key in EventValidator.UnitsFormKeys) Assert.Null(action[key]);
        var entries = action["waveList"]!.AsArray().Single()!["units"]!.AsArray();
        Assert.All(entries, e => Assert.Equal(1.5, (double)e!["modifiers"]!["power"]!));                    // the action's modifiers on each
        var loaded = lib.Catalog.Current.Find("raid")!;
        Assert.True(loaded.Startable, loaded.DisabledReason);
        Assert.Equal(1, loaded.Action!.Waves);
    }

    [Theory]
    [InlineData("action.waveList.2.units", "CHAR_Bandit_Thug")]
    [InlineData("action.waveList.1.afterSeconds", "60")]
    [InlineData("action.waveList.1.units.1.level", "30")]
    public void WaveSets_fails_when_a_conversion_takes_another_field(string field, string value)
    {
        var lib = WaveLib();
        var hash = lib.Hash;
        Assert.Equal("a units-form event takes action.waveList.1.units first (it converts the event)", AuthoringTests.Set(lib, "raid", field, value));
        Assert.Equal(hash, lib.Hash);
    }

    /// <summary>Review F1: a units-form field on a wave-list event is refused before anything is written; it would only
    /// disable the event, and chat could not remove it again.</summary>
    [Theory]
    [InlineData("action.waves", "3")]
    [InlineData("action.intervalSeconds", "60")]
    [InlineData("action.units", "CHAR_Bandit_Thug:2")]
    [InlineData("action.modifiers.power", "1.5")]
    [InlineData("action.units.1.chance", "0.5")]
    public void WaveSets_fails_when_a_units_form_field_meets_a_wave_list(string field, string value)
    {
        var lib = WaveLib();
        var hash = lib.Hash;
        Assert.Equal(EventsEditor.WaveListTakesWaves, AuthoringTests.Set(lib, "ws", field, value));
        Assert.Equal(hash, lib.Hash);
        Assert.True(lib.Catalog.Current.Find("ws")!.Startable);
    }

    [Fact]
    public void WaveSets_passes_append_and_remove_waves()
    {
        var lib = WaveLib();
        Assert.StartsWith("event ws action.waveList.3.units = ", AuthoringTests.Set(lib, "ws", "action.waveList.3.units", "CHAR_Bandit_Thug:5"));
        Assert.Equal(3, lib.Catalog.Current.Find("ws")!.Action!.Waves);
        Assert.True(lib.Catalog.Current.Find("ws")!.Action!.WaveList![2].WaitsForClear);                  // neither key: when cleared
        Assert.Equal("action.waveList has 3 waves; the next one is 4 (set its units first)",
            AuthoringTests.Set(lib, "ws", "action.waveList.5.units", "CHAR_Bandit_Thug"));
        Assert.Equal("action.waveList has 3 waves; the next one is 4 (set its units first)",
            AuthoringTests.Set(lib, "ws", "action.waveList.4.afterSeconds", "60"));
        Assert.Equal("event ws action.waveList.1 = none", AuthoringTests.Set(lib, "ws", "action.waveList.1", "none"));
        var first = WaveAction(lib, "ws")["waveList"]!.AsArray()[0]!.AsObject();
        Assert.Null(first["whenCleared"]);                                                             // the new wave 1 loses its start keys
        Assert.True(lib.Catalog.Current.Find("ws")!.Startable);
        AuthoringTests.Set(lib, "ws", "action.waveList.2", "none");
        Assert.Equal("action.waveList keeps at least one wave", AuthoringTests.Set(lib, "ws", "action.waveList.1", "none"));
    }

    [Fact]
    public void WaveSets_fails_when_a_start_key_breaks_a_rule()
    {
        var lib = WaveLib();
        Assert.Equal(EventValidator.FirstWaveStart, AuthoringTests.Set(lib, "ws", "action.waveList.1.whenCleared", "true"));
        Assert.Equal(EventValidator.NoStartRule(2), AuthoringTests.Set(lib, "ws", "action.waveList.2.whenCleared", "false"));    // A1 (a)
        Assert.StartsWith("event ws action.waveList.2.afterSeconds = 60", AuthoringTests.Set(lib, "ws", "action.waveList.2.afterSeconds", "60"));
        Assert.StartsWith("event ws action.waveList.2.whenCleared = false", AuthoringTests.Set(lib, "ws", "action.waveList.2.whenCleared", "false"));
        Assert.Equal("action.scoreboard is not an Empower field", AuthoringTests.Set(lib, "surge", "action.scoreboard", "true"));
    }

    [Fact]
    public void WaveSets_passes_a_unit_modifier()
    {
        var lib = WaveLib();
        Assert.Equal("action.waveList.2.units.1.modifiers takes level or levelDelta, not both: set action.waveList.2.units.1.level none first",
            AuthoringTests.Set(lib, "ws", "action.waveList.2.units.1.levelDelta", "3"));
        AuthoringTests.Set(lib, "ws", "action.waveList.2.units.1.level", "none");
        Assert.Null(WaveAction(lib, "ws")["waveList"]![1]!["units"]![0]!["modifiers"]);                     // an empty object is removed
        Assert.StartsWith("event ws action.waveList.2.units.1.levelDelta = 3", AuthoringTests.Set(lib, "ws", "action.waveList.2.units.1.levelDelta", "3"));
        Assert.Equal(3, lib.Catalog.Current.Find("ws")!.Action!.WaveList![1].Units[0].Modifiers!.LevelDelta);
        Assert.Equal("action.waveList.2.units has no entry 2", AuthoringTests.Set(lib, "ws", "action.waveList.2.units.2.power", "1.5"));
    }

    [Fact]
    public void WaveSets_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = WaveLib();
        var units = string.Join(",", Enumerable.Repeat("CHAR_" + new string('A', 59) + ":50", 10));       // ten 64-character prefabs
        var reply = AuthoringTests.Set(lib, "raid", "action.waveList.1.units", units);
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(reply)} bytes: {reply}");
        Assert.Contains("10 entries, 500 units", reply);
    }

    [Fact]
    public void WaveSets_fails_when_the_table_and_settable_fields_differ()
    {
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        foreach (var f in CommandArgs.WaveListFields.Append(CommandArgs.ScoreboardField))
        {
            Assert.Contains($"| `{f}` | spawn action | admin |", design);
            Assert.True(CommandArgs.SettableFields.ContainsKey(f), f);
        }
        Assert.Equal(11, CommandArgs.WaveListFields.Count + 1);
    }

    [Fact]
    public void WaveSets_empty_value()
    {
        foreach (var f in new[] { "action.scoreboard", "action.waveList.1", "action.waveList.1.units", "action.waveList.2.afterSeconds" })
            Assert.False(CommandArgs.SettableValue(f, "").Ok);
    }
}
