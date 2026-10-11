using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D9: the six `.nyar event set` fields of a swarm, each taking exactly what the validator takes and
/// refusing with its reason; the cross-key conflicts refused before anything is written; growth never converted;
/// every reply within 480 bytes; the §6 table the same as SettableFields.</summary>
public partial class CommandArgTests
{
    const string SwarmPoint = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";

    static string SwarmJson(string extra) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ], \"waves\": 3, " +
        "\"intervalSeconds\": 60, \"radius\": 10, \"location\": " + SwarmPoint + (extra == "" ? "" : ", " + extra) + " }";

    /// <summary>raid (plain units form), arena (arena 25, Hunt 25), scored (scoreboard and winner), grown (growth), ws (a
    /// waveList), around (an AroundPlayer location).</summary>
    static Library SwarmLib() => AuthoringTests.Lib(
        Json.Event("raid"),
        Json.Event("arena", action: SwarmJson("\"arena\": { \"radius\": 25 }, \"behaviour\": { \"type\": \"Hunt\", \"range\": 25 }")),
        Json.Event("scored", action: SwarmJson("\"scoreboard\": true, \"winner\": \"KillDeath\"")),
        Json.Event("grown", action: SwarmJson("\"growth\": { \"addUnits\": 1, \"addLevels\": 2 }")),
        Json.Event("ws", action: WaveListJson),
        Json.Event("around", action: Json.ValidAction.Replace(SwarmPoint, "{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }")));

    static void Refused(Library lib, string id, string field, string value, string reason)
    {
        var hash = lib.Hash;
        Assert.Equal(reason, AuthoringTests.Set(lib, id, field, value));
        Assert.Equal(hash, lib.Hash);                                                                   // nothing written
        Assert.True(lib.Catalog.Current.Find(id)!.Startable);
    }

    static EventDefinition Loaded(Library lib, string id)
    {
        var d = lib.Catalog.Current.Find(id)!;
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    [Theory]
    [InlineData("action.arena", "14", EventValidator.ArenaRadiusRule)]
    [InlineData("action.arena", "61", EventValidator.ArenaRadiusRule)]
    [InlineData("action.arena", "20.5", EventValidator.ArenaRadiusRule)]
    [InlineData("action.arena", "+20", EventValidator.ArenaRadiusRule)]
    [InlineData("action.winner", "kills", EventValidator.WinnerValue)]
    [InlineData("action.winner", "true", EventValidator.WinnerValue)]
    [InlineData("action.growth", "remove", CommandArgs.GrowthNoneRule)]
    [InlineData("action.growth", "1", CommandArgs.GrowthNoneRule)]
    [InlineData("action.growth.addUnits", "11", EventValidator.GrowthAddUnitsRule)]
    [InlineData("action.growth.addUnits", "-1", EventValidator.GrowthAddUnitsRule)]
    [InlineData("action.growth.addUnits", "none", EventValidator.GrowthAddUnitsRule)]
    [InlineData("action.growth.addLevels", "6", EventValidator.GrowthAddLevelsRule)]
    [InlineData("action.growth.addLevels", "1.5", EventValidator.GrowthAddLevelsRule)]
    [InlineData("action.growth.whenCleared", "yes", EventValidator.GrowthWhenClearedRule)]
    [InlineData("action.growth.whenCleared", "True", EventValidator.GrowthWhenClearedRule)]
    public void Swarm_fails_when_value_out_of_range(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
    }

    [Fact]
    public void Swarm_passes_values_in_range()
    {
        Assert.Equal(15, CommandArgs.SettableValue("action.arena", "15").Value);
        Assert.Equal(60, CommandArgs.SettableValue("action.arena", "60").Value);
        foreach (var v in new[] { "KillDeath", "killdeath", "KILLDEATH" }) Assert.Equal("KillDeath", CommandArgs.SettableValue("action.winner", v).Value);
        foreach (var f in new[] { "action.arena", "action.winner", "action.growth" }) Assert.IsType<FieldRemoval>(CommandArgs.SettableValue(f, "none").Value);
        Assert.Equal(0, CommandArgs.SettableValue("action.growth.addUnits", "0").Value);
        Assert.Equal(10, CommandArgs.SettableValue("action.growth.addUnits", "10").Value);
        Assert.Equal(5, CommandArgs.SettableValue("action.growth.addLevels", "5").Value);
        Assert.Equal(false, CommandArgs.SettableValue("action.growth.whenCleared", "false").Value);

        // what the field takes, the validator loads
        var lib = SwarmLib();
        Assert.StartsWith("event raid action.arena = 15", AuthoringTests.Set(lib, "raid", "action.arena", "15"));
        Assert.Equal(new Arena(15), Loaded(lib, "raid").Action!.Arena);
        AuthoringTests.Set(lib, "raid", "action.arena", "60");
        Assert.Equal(new Arena(60), Loaded(lib, "raid").Action!.Arena);
        AuthoringTests.Set(lib, "raid", "action.scoreboard", "true");
        AuthoringTests.Set(lib, "raid", "action.winner", "killdeath");
        Assert.Equal("KillDeath", (string?)WaveAction(lib, "raid")["winner"]);
        Assert.Equal(WinnerRule.KillDeath, Loaded(lib, "raid").Action!.Winner);
        AuthoringTests.Set(lib, "raid", "action.winner", "none");
        AuthoringTests.Set(lib, "raid", "action.arena", "none");
        Assert.Null(WaveAction(lib, "raid")["winner"]);
        Assert.Null(WaveAction(lib, "raid")["arena"]);
        Assert.Equal((null, WinnerRule.None), (Loaded(lib, "raid").Action!.Arena, Loaded(lib, "raid").Action!.Winner));
        AuthoringTests.Set(lib, "grown", "action.growth.addUnits", "10");                           // 5 + 2·10 = 25 units
        AuthoringTests.Set(lib, "grown", "action.growth.addLevels", "5");
        AuthoringTests.Set(lib, "grown", "action.growth.whenCleared", "true");
        Assert.Equal(new Growth(10, 5, true), Loaded(lib, "grown").Action!.Growth);
        AuthoringTests.Set(lib, "grown", "action.growth.addUnits", "0");
        Assert.Equal(new Growth(0, 5, true), Loaded(lib, "grown").Action!.Growth);
    }

    [Fact]
    public void Swarm_fails_when_an_arena_meets_an_around_player_location()
    {
        var lib = SwarmLib();
        Refused(lib, "around", "action.arena", "30", EventValidator.ArenaLocation);
        Refused(lib, "arena", "location", "aroundplayer 10 20", EventValidator.ArenaLocation);
        Assert.StartsWith("event arena action.location = Point ", AuthoringTests.Set(lib, "arena", "location", "here", (5f, 1f, 6f)));   // a Point stays fine
    }

    [Fact]
    public void Swarm_fails_when_a_spawn_radius_or_hunt_range_widens_past_the_arena()
    {
        var lib = SwarmLib();
        Refused(lib, "arena", "action.radius", "26", EventValidator.ArenaSpawnRadius);
        Refused(lib, "arena", "action.behaviour", "hunt 26", EventValidator.ArenaHuntRange);
        Refused(lib, "arena", "action.arena", "24", EventValidator.ArenaHuntRange);                     // the arena shrunk under the Hunt
        Assert.StartsWith("event arena action.radius = 25", AuthoringTests.Set(lib, "arena", "action.radius", "25"));
        Assert.StartsWith("event raid action.arena = 15", AuthoringTests.Set(lib, "raid", "action.arena", "15"));
        Refused(lib, "raid", "action.radius", "16", EventValidator.ArenaSpawnRadius);
    }

    [Fact]
    public void Swarm_fails_when_a_winner_lacks_the_scoreboard()
    {
        var lib = SwarmLib();
        Refused(lib, "raid", "action.winner", "killdeath", EventValidator.WinnerScoreboard);
        Refused(lib, "scored", "action.scoreboard", "false", EventValidator.WinnerScoreboard);
        Assert.StartsWith("event scored action.winner = none", AuthoringTests.Set(lib, "scored", "action.winner", "none"));
        Assert.StartsWith("event scored action.scoreboard = false", AuthoringTests.Set(lib, "scored", "action.scoreboard", "false"));
        Refused(lib, "ws", "action.winner", "killdeath", EventValidator.WinnerScoreboard);
    }

    [Fact]
    public void Swarm_fails_when_growth_meets_a_wave_list_or_one_wave()
    {
        var lib = SwarmLib();
        Refused(lib, "ws", "action.growth.addUnits", "1", EventValidator.GrowthUnitsForm);
        Refused(lib, "grown", "action.waves", "1", EventValidator.GrowthWaves);
        Refused(lib, "grown", "action.waveList.1.units", "CHAR_Bandit_Thug:4", EventValidator.GrowthUnitsForm + ": set action.growth none first");
        Refused(lib, "grown", "action.waveList.1.units.1.level", "30", EventValidator.GrowthUnitsForm + ": set action.growth none first");
        Assert.Null(WaveAction(lib, "grown")["waveList"]);                                           // never converted
        Assert.StartsWith("event grown action.growth.addLevels = 0", AuthoringTests.Set(lib, "grown", "action.growth.addLevels", "0"));
        Refused(lib, "grown", "action.growth.addUnits", "0", EventValidator.GrowthAdds);
        Refused(lib, "grown", "action.units", "CHAR_Bandit_Thug:50", EventValidator.GrowthLastWave);   // 50 + 2·1 = 52
        Refused(lib, "raid", "action.growth.whenCleared", "true", EventValidator.GrowthAdds);           // created adding nothing
    }

    [Fact]
    public void Swarm_passes_growth_created_and_removed()
    {
        var lib = SwarmLib();
        var before = WaveAction(lib, "raid").ToJsonString();
        Assert.StartsWith("event raid action.growth.addUnits = 2", AuthoringTests.Set(lib, "raid", "action.growth.addUnits", "2"));
        Assert.Equal(2, (int)WaveAction(lib, "raid")["growth"]!["addUnits"]!);
        var grown = Loaded(lib, "raid").Action!;
        Assert.Equal(new Growth(2, 0, false), grown.Growth);
        Assert.Equal([5, 7, 9], grown.WaveList!.Select(w => w.Units.Single().Count));
        Assert.StartsWith("event raid action.growth = none", AuthoringTests.Set(lib, "raid", "action.growth", "none"));
        Assert.Equal(before, WaveAction(lib, "raid").ToJsonString());                           // the units form as it was
        Assert.Null(Loaded(lib, "raid").Action!.WaveList);
    }

    /// <summary>Code review F1: a file already holding two swarm conflicts takes an edit that removes one (the other is
    /// not the edit's) and refuses an edit that adds a third, whichever conflict the validator names first.</summary>
    [Fact]
    public void Swarm_fails_when_an_old_conflict_blocks_a_fix_or_hides_a_new_one()
    {
        var lib = AuthoringTests.Lib(Json.Event("both", action: Json.ValidAction
            .Replace(SwarmPoint, "{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }")
            .TrimEnd().TrimEnd('}') + ", \"arena\": { \"radius\": 25 }, \"winner\": \"KillDeath\" }"));
        Assert.Equal(2, EventsEditor.SwarmConflicts(lib.Text, "both").Count);
        Assert.StartsWith("event both action.arena = none", AuthoringTests.Set(lib, "both", "action.arena", "none"));
        Assert.Equal([EventValidator.WinnerScoreboard], EventsEditor.SwarmConflicts(lib.Text, "both"));
        var hash = lib.Hash;
        Assert.Equal(EventValidator.GrowthAdds, AuthoringTests.Set(lib, "both", "action.growth.whenCleared", "true"));
        Assert.Equal(hash, lib.Hash);
    }

    /// <summary>Code review F4: none of the six swarm fields can make a long reply today (each echoes a short value or a
    /// fixed reason); the cap is checked so a later, longer reply is caught.</summary>
    [Fact]
    public void Swarm_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = SwarmLib();
        var replies = new List<string>
        {
            AuthoringTests.Set(lib, "grown", "action.waveList.1.units", string.Join(",", Enumerable.Repeat("CHAR_" + new string('A', 59) + ":50", 10))),
            AuthoringTests.Set(lib, "around", "action.arena", "60"),
            AuthoringTests.Set(lib, "raid", "action.arena", "30"),
            AuthoringTests.Set(lib, "grown", "action.growth.addUnits", "10"),
            AuthoringTests.Set(lib, "ws", "action.growth.addLevels", "5"),
        };
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(r)} bytes: {r}"));
    }

    [Fact]
    public void Swarm_fails_when_the_table_and_settable_fields_differ()
    {
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        Assert.Equal(["action.arena", "action.winner", "action.growth", "action.growth.addUnits", "action.growth.addLevels", "action.growth.whenCleared"],
            CommandArgs.SwarmFields);
        foreach (var f in CommandArgs.SwarmFields)
        {
            Assert.Contains($"| `{f}` | spawn action | admin |", design);
            Assert.Equal(("spawn action", "admin"), CommandArgs.SettableFields[f]);
        }
    }

    [Fact]
    public void Swarm_empty_value()
    {
        foreach (var f in CommandArgs.SwarmFields)
            Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue(f, "").Error);
    }
}
