using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D16: `.nyar event set` on an Apocalypse takes `action.goal here`, the goal's radius and name, the
/// hordes' count, everyMinutes and size, roamers.max, marchSpeed, `action.bosses` and durationSeconds, each within D1's
/// bounds and refused with the validator's reason; the fields are refused on every other kind; every reply fits 480
/// bytes; the §6 table lists the same fields (ContractDocTests compares the whole table).</summary>
public partial class CommandArgTests
{
    /// <summary>apo (an Apocalypse, 3 hordes from 1 min every 5 min, 1 h), raid (SpawnWaves), surge (Empower).</summary>
    static Library ApoLib() => new(Json.File(Apo.Event(), Json.Event("raid"), Json.Empower("surge")));

    static ApocalypseAction ApoAction(Library lib)
    {
        var d = lib.Catalog.Current.Find("apo")!;
        Assert.True(d.Startable, d.DisabledReason);
        return d.Apocalypse!;
    }

    static JsonObject ApoJson(Library lib) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == "apo")["action"]!;

    static void ApoRefused(Library lib, string id, string field, string value, string reason)
    {
        var hash = lib.Hash;
        var reply = AuthoringTests.Set(lib, id, field, value, (1f, 2f, 3f));
        Assert.Equal(reason, reply);
        Assert.Equal(hash, lib.Hash);                                                                   // nothing written
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes);
    }

    [Theory]
    [InlineData("action.goal", "there", CommandArgs.GoalHereRule)]
    [InlineData("action.goal.radius", "9", EventValidator.GoalRadiusRule)]
    [InlineData("action.goal.radius", "101", EventValidator.GoalRadiusRule)]
    [InlineData("action.goal.radius", "20.5", EventValidator.GoalRadiusRule)]
    [InlineData("action.goal.name", "a name of more than thirty-two chars", EventValidator.GoalNameRule)]
    [InlineData("action.goal.name", "<b>castle</b>", EventValidator.GoalNameRule)]
    [InlineData("action.hordes.count", "0", EventValidator.HordesCountRule)]
    [InlineData("action.hordes.count", "11", EventValidator.HordesCountRule)]
    [InlineData("action.hordes.everyMinutes", "4", EventValidator.EveryMinutesRule)]
    [InlineData("action.hordes.everyMinutes", "1441", EventValidator.EveryMinutesRule)]
    [InlineData("action.hordes.size", "4", EventValidator.HordeSizeRule)]
    [InlineData("action.hordes.size", "51", EventValidator.HordeSizeRule)]
    [InlineData("action.roamers.max", "0", EventValidator.RoamersMaxRule)]
    [InlineData("action.roamers.max", "101", EventValidator.RoamersMaxRule)]
    [InlineData("action.marchSpeed", "0.4", EventValidator.MarchSpeedRule)]
    [InlineData("action.marchSpeed", "5.01", EventValidator.MarchSpeedRule)]
    [InlineData("action.marchSpeed", "fast", EventValidator.MarchSpeedRule)]
    [InlineData("durationSeconds", "29", CommandArgs.DurationRule)]
    [InlineData("durationSeconds", "604801", CommandArgs.DurationRule)]
    public void Apocalypse_fails_when_a_field_accepts_a_value_the_validator_refuses(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
        Assert.StartsWith(field.Split(' ')[0], error);                         // every refusal names its field
        // what the parse cannot see, the editor refuses with the validator's reason before writing
        var lib = ApoLib();
        ApoRefused(lib, "apo", "durationSeconds", "599", EventValidator.ApocalypseDurationRule);
        ApoRefused(lib, "apo", "durationSeconds", "660", EventValidator.LastHordeLate);              // the last horde rises at 660 s
        ApoRefused(lib, "apo", "action.hordes.everyMinutes", "30", EventValidator.LastHordeLate);    // the last horde would rise at 61 min of 60
        ApoRefused(lib, "raid", "durationSeconds", "7201", EventValidator.DurationRule);
        Assert.Equal(3, ApoAction(lib).Hordes.Count);
    }

    [Fact]
    public void Apocalypse_fails_when_a_valid_bosses_value_is_refused()
    {
        var lib = ApoLib();
        Assert.StartsWith("event apo action.bosses = ", AuthoringTests.Set(lib, "apo", "action.bosses", Apo.Tourok + ":2"));
        Assert.Equal([(Apo.Tourok, 2)], ApoAction(lib).Bosses!.Select(b => (b.Prefab, b.Count)));
        Assert.StartsWith("event apo action.bosses = ", AuthoringTests.Set(lib, "apo", "action.bosses", "none"));
        Assert.Null(ApoAction(lib).Bosses);
        ApoRefused(lib, "apo", "action.bosses", "Bandit_Tourok", CommandArgs.NameRule(CommandArgs.BossesField));   // revenge D31's reasons
    }

    [Fact]
    public void Apocalypse_fails_when_goal_here_drops_the_height()
    {
        var lib = ApoLib();
        Assert.StartsWith("event apo action.goal = ", AuthoringTests.Set(lib, "apo", "action.goal", "here", (-1234.5f, 45.5f, 800f)));
        var location = ApoAction(lib).Goal.Location;
        Assert.Equal((LocationType.Point, -1234.5f, (float?)45.5f, 800f), (location.Type, location.X, location.Y, location.Z));
        var json = (JsonObject)ApoJson(lib)["goal"]!["location"]!;
        Assert.Equal(["type", "x", "y", "z"], json.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("Brighthaven", ApoAction(lib).Goal.Name);                 // the rest of the goal stays
        Assert.Equal(LocationArg.FromContext(() => null).Error, AuthoringTests.Set(lib, "apo", "action.goal", "here"));   // no position, nothing written
    }

    [Fact]
    public void Apocalypse_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = ApoLib();
        var replies = new List<string>
        {
            AuthoringTests.Set(lib, "apo", "action.goal", "here", (-10000f, -10000f, -10000f)),
            AuthoringTests.Set(lib, "apo", "action.goal.radius", "100"),
            AuthoringTests.Set(lib, "apo", "action.goal.name", new string('G', 32)),
            AuthoringTests.Set(lib, "apo", "durationSeconds", "604800"),
            AuthoringTests.Set(lib, "apo", "action.hordes.count", "10"),
            AuthoringTests.Set(lib, "apo", "action.hordes.everyMinutes", "1000"),
            AuthoringTests.Set(lib, "apo", "action.hordes.size", "50"),
            AuthoringTests.Set(lib, "apo", "action.roamers.max", "100"),
            AuthoringTests.Set(lib, "apo", "action.marchSpeed", "5.0"),
            AuthoringTests.Set(lib, "apo", "action.bosses", string.Join(",", Enumerable.Repeat(Apo.Tourok + ":3", 3))),
            AuthoringTests.Set(lib, "raid", "action.goal.radius", "50"),
            AuthoringTests.Set(lib, "apo", "action.hordes.everyMinutes", "4"),
        };
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(r)} bytes: {r}"));
        Assert.Equal((10, 1000, 50, 100, 5.0), (ApoAction(lib).Hordes.Count, ApoAction(lib).Hordes.EveryMinutes, ApoAction(lib).Hordes.Size, ApoAction(lib).Roamers.Max, ApoAction(lib).MarchSpeed));
    }

    [Fact]
    public void Apocalypse_fails_when_a_field_is_let_through_on_another_kind()
    {
        var lib = ApoLib();
        foreach (var (field, value) in new[] { ("action.goal.radius", "50"), ("action.hordes.size", "10"), ("action.marchSpeed", "1.0"), ("action.roamers.max", "5") })
        {
            ApoRefused(lib, "raid", field, value, $"{field} is not a SpawnWaves field");
            ApoRefused(lib, "surge", field, value, $"{field} is not an Empower field");
        }
        ApoRefused(lib, "raid", "action.goal", "here", "action.goal is not a SpawnWaves field");
        // a wave field is refused on an Apocalypse
        Assert.Equal("action.waves is not a field of an Apocalypse action", AuthoringTests.Set(lib, "apo", "action.waves", "3"));
    }

    [Fact]
    public void Apocalypse_fails_when_the_table_and_settable_fields_differ()
    {
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        foreach (var field in CommandArgs.ApocalypseFields)
        {
            Assert.Contains(field, CommandArgs.SettableFields.Keys);
            Assert.Contains($"`{field}", design);
        }
        Assert.Equal(8, CommandArgs.ApocalypseFields.Count);
        Assert.All(CommandArgs.ApocalypseFields, f => Assert.Equal(("apocalypse action", "admin"), CommandArgs.SettableFields[f]));
    }

    [Fact]
    public void Apocalypse_passes_each_field_at_its_bounds()
    {
        var lib = ApoLib();
        foreach (var (field, value) in new[]
                 {
                     ("action.goal.radius", "10"), ("action.goal.radius", "100"), ("action.goal.name", "the living"), ("action.hordes.count", "1"),
                     ("action.hordes.everyMinutes", "5"), ("action.hordes.size", "5"), ("action.hordes.size", "50"), ("action.roamers.max", "1"),
                     ("action.marchSpeed", "0.5"), ("durationSeconds", "600"), ("durationSeconds", "604800"),
                 })
            Assert.StartsWith($"event apo {field} = ", AuthoringTests.Set(lib, "apo", field, value));
        var a = ApoAction(lib);
        Assert.Equal((100, "the living", 1, 5, 50, 1, 0.5), (a.Goal.Radius, a.Goal.Name, a.Hordes.Count, a.Hordes.EveryMinutes, a.Hordes.Size, a.Roamers.Max, a.MarchSpeed));
        Assert.Equal(604800, lib.Catalog.Current.Find("apo")!.DurationSeconds);
    }

    [Fact]
    public void Apocalypse_empty_value_required()
    {
        foreach (var field in CommandArgs.ApocalypseFields)
            Assert.False(CommandArgs.SettableValue(field, null).Ok, field);
    }
}
