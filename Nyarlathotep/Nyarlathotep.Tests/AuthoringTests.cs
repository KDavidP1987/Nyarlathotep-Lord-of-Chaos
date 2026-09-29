using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D6 (event new), D7 (copy), D8 (delete with confirm), D9 (trigger fields), D10 (action fields),
/// D11 (location here) and D12 (every chat write equals a file edit plus a reload).</summary>
public partial class AuthoringTests
{
    internal static readonly DateTime Now = Zones.Utc(2026, 9, 26, 12, 0);

    internal static FakeUnits Units() => TemplateLibraryTests.Units("CHAR_Bandit_Deadeye", "CHAR_Undead_SkeletonSoldier_Base", "Boss_Known");

    internal static Library Lib(params string[] events) => new(Json.File(events), Units());

    const string Schedule = "{ \"type\": \"Schedule\", \"days\": [\"Sat\"], \"times\": [\"20:00\"] }";

    /// <summary>`.nyar event set` as the command runs it: the value is checked first, then `location here` reads the
    /// position, then the editor writes.</summary>
    internal static string Set(Library lib, string id, string field, string value, (float X, float Y, float Z)? at = null)
    {
        var arg = CommandArgs.SettableValue(field, value);
        if (!arg.Ok) return arg.Error!;
        var v = arg.Value;
        if (v is LocationHere)
        {
            var p = LocationArg.FromContext(() => at);
            if (!p.Ok) return p.Error!;
            v = p.Value;
        }
        return lib.Editor.Edit(id, field, v, lib.Units).Human;
    }

    static JsonObject Entry(string text, string id) =>
        ((JsonArray)JsonNode.Parse(text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id);

    /// <summary>Asserts a refused command changed nothing: events.json, its .bak and the config-changed count.</summary>
    static void Unchanged(Library lib, Action act)
    {
        var (hash, bak, changed, ops) = (lib.Hash, lib.Bak, lib.ConfigChanged, lib.Fs.Ops.Count(o => o.StartsWith("write", StringComparison.Ordinal)));
        act();
        Assert.Equal(hash, lib.Hash);
        Assert.Equal(bak, lib.Bak);
        Assert.Equal(changed, lib.ConfigChanged);
        Assert.Equal(ops, lib.Fs.Ops.Count(o => o.StartsWith("write", StringComparison.Ordinal)));
    }

    // ---- D6 New

    [Theory]
    [InlineData("empowerment")]
    [InlineData("spawns")]
    [InlineData("boss")]
    public void New_passes_skeleton(string pillar)
    {
        var lib = Lib(Json.Event("raid"));
        Assert.Equal($"event my-surge created (disabled, {pillar}); set its fields with .nyar event set", lib.Write(t => Authoring.New(t, "my-surge", pillar)));
        var d = lib.Catalog.Current.Find("my-surge")!;
        Assert.Null(d.DisabledReason);
        Assert.False(d.Enabled);
        Assert.Equal("my-surge", d.Name);
        Assert.Equal(TriggerType.Manual, d.Trigger.Type);
        Assert.Equal(600, d.DurationSeconds);
        if (pillar == "empowerment")
        {
            Assert.Equal(["Faction_Bandits"], d.Empower!.Factions);
            Assert.Equal(1.2, d.Empower.Stats.PhysicalPower);
            Assert.Null(d.Action);
        }
        else
        {
            var a = d.Action!;
            Assert.Equal(("CHAR_Bandit_Thug", 3), (a.Units.Single().Prefab, a.Units.Single().Count));
            Assert.Equal((1, 60, 8, LocationType.Admin), (a.Waves, a.IntervalSeconds, a.Radius, a.Location.Type));
            Assert.Null(d.Empower);
        }
    }

    [Theory]
    [InlineData("my-surge", "dusk", "unknown pillar dusk; use empowerment, spawns, boss, zones or sieges")]
    [InlineData("raid", "spawns", "event raid already exists")]
    [InlineData("My_Surge", "spawns", "id must be 1-32 of a-z 0-9 -")]
    public void New_fails_when_refused(string id, string pillar, string reply)
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal(reply, lib.Write(t => Authoring.New(t, id, pillar))));
    }

    [Fact]
    public void New_empty_events_file()
    {
        var lib = Lib();
        Assert.StartsWith("event first created", lib.Write(t => Authoring.New(t, "first", "spawns")));
        Assert.Single(lib.Catalog.Current.All);
    }

    // ---- D7 Copy

    [Fact]
    public void Copy_passes_verbatim_disabled()
    {
        var lib = Lib(Json.Event("raid", trigger: Schedule, extra: "\"conditions\": { \"cooldownMinutes\": 30 }"));
        Assert.Equal("event raid copied to raid-2 (disabled)", lib.Write(t => Authoring.Copy(t, "raid", "raid-2")));
        var src = Entry(lib.Text, "raid");
        var copy = Entry(lib.Text, "raid-2");
        Assert.False((bool)copy["enabled"]!);
        copy["id"] = "raid";
        copy["enabled"] = true;
        Assert.Equal(src.ToJsonString(), copy.ToJsonString());
    }

    [Fact]
    public void Copy_passes_invalid_source_keeps_reason()
    {
        var lib = Lib(Json.Event("raid", action: Json.ValidAction.Replace("CHAR_Bandit_Thug", "CHAR_Nobody")));
        var reason = lib.Catalog.Current.Find("raid")!.DisabledReason;
        Assert.NotNull(reason);
        Assert.StartsWith("event raid copied to raid-2 (disabled); now disabled: ", lib.Write(t => Authoring.Copy(t, "raid", "raid-2")));
        Assert.Equal(reason, lib.Catalog.Current.Find("raid-2")!.DisabledReason);
    }

    [Theory]
    [InlineData("nope", "raid-2", "unknown event nope")]
    [InlineData("raid", "other", "event other already exists")]
    [InlineData("raid", "Raid 2", "id must be 1-32 of a-z 0-9 -")]
    public void Copy_fails_when_refused(string id, string newId, string reply)
    {
        var lib = Lib(Json.Event("raid"), Json.Event("other"));
        Unchanged(lib, () => Assert.Equal(reply, lib.Write(t => Authoring.Copy(t, id, newId))));
    }

    [Fact]
    public void Copy_empty_events_file()
    {
        var lib = Lib();
        Unchanged(lib, () => Assert.Equal("unknown event raid", lib.Write(t => Authoring.Copy(t, "raid", "raid-2"))));
    }

    // ---- D8 DeleteArming

    sealed class Deletion
    {
        public Library Lib { get; }
        public DeleteArming Arming { get; } = new();
        public EventDeleter Deleter { get; }

        public Deletion(params string[] events)
        {
            Lib = AuthoringTests.Lib(events);
            Lib.State.Document.LastStart["raid"] = Now.AddHours(-1);
            Lib.State.Document.LastStart["other"] = Now.AddHours(-2);
            Deleter = new EventDeleter(Arming, Lib.Editor, Lib.Catalog, id => Lib.Catalog.Running.Any(r => r.Definition.Id == id), Lib.State, Lib.Log.Add);
        }

        public string Confirm(ulong admin, string id, DateTime at) => Deleter.Confirm(admin, id, at, Lib.Units).Human;
        public string? StateText => Lib.Fs.Text(DataFile.State, FileVariant.Main);
    }

    [Fact]
    public void DeleteArming_passes_confirm_within_30s()
    {
        var x = new Deletion(Json.Event("raid"), Json.Event("other"));
        var hash = x.Lib.Hash;
        Assert.Equal("delete raid? run .nyar event delete raid confirm within 30 s", x.Deleter.Request(1, "raid", Now).Human);
        Assert.Equal(hash, x.Lib.Hash);                                               // the first call writes nothing
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file)", x.Confirm(1, "raid", Now.AddSeconds(30)));
        Assert.Null(x.Lib.Catalog.Current.Find("raid"));
        Assert.NotNull(x.Lib.Catalog.Current.Find("other"));
        Assert.False(x.Lib.State.Document.LastStart.ContainsKey("raid"));
        Assert.Contains("\"other\"", x.StateText);
        Assert.DoesNotContain("\"raid\"", x.StateText);
        Assert.Equal(1, x.Lib.ConfigChanged);
        Assert.Contains("\"id\": \"raid\"", x.Lib.Bak);
    }

    [Theory]
    [InlineData(1, "raid", 31)]
    [InlineData(2, "raid", 5)]
    [InlineData(1, "other", 5)]
    public void DeleteArming_fails_when_confirm_not_armed(ulong admin, string id, int seconds)
    {
        var x = new Deletion(Json.Event("raid"), Json.Event("other"));
        x.Deleter.Request(1, "raid", Now);
        Unchanged(x.Lib, () => Assert.Equal($"no delete pending for {id}", x.Confirm(admin, id, Now.AddSeconds(seconds))));
        Assert.True(x.Lib.State.Document.LastStart.ContainsKey(id));
    }

    [Fact]
    public void DeleteArming_fails_when_running()
    {
        var x = new Deletion(Json.Event("raid"));
        Assert.Null(x.Lib.Catalog.TryStart("raid", Now, out _));
        Assert.Equal("event raid is running; stop it first", x.Deleter.Request(1, "raid", Now).Human);
        Assert.Equal(0, x.Arming.Count);                                              // arms nothing
        x.Lib.Catalog.TryEnd("raid");

        // Armed, then started: the confirm is refused and the arming kept, so a stop and a confirm inside 30 s delete.
        x.Deleter.Request(1, "raid", Now);
        x.Lib.Catalog.TryStart("raid", Now.AddSeconds(5), out _);
        Unchanged(x.Lib, () => Assert.Equal("event raid is running; stop it first", x.Confirm(1, "raid", Now.AddSeconds(10))));
        Assert.True(x.Arming.IsArmed(1, "raid", Now.AddSeconds(10)));
        x.Lib.Catalog.TryEnd("raid");
        Assert.StartsWith("event raid deleted", x.Confirm(1, "raid", Now.AddSeconds(20)));
    }

    [Fact]
    public void DeleteArming_fails_when_deleted_by_another_admin()
    {
        var x = new Deletion(Json.Event("raid"));
        x.Deleter.Request(1, "raid", Now);
        x.Deleter.Request(2, "raid", Now.AddSeconds(1));
        Assert.StartsWith("event raid deleted", x.Confirm(2, "raid", Now.AddSeconds(2)));
        Unchanged(x.Lib, () => Assert.Equal("unknown event raid", x.Confirm(1, "raid", Now.AddSeconds(3))));
    }

    [Fact]
    public void DeleteArming_empty_no_pending_delete()
    {
        var x = new Deletion(Json.Event("raid"));
        Unchanged(x.Lib, () => Assert.Equal("no delete pending for raid", x.Confirm(1, "raid", Now)));
        Assert.Equal("unknown event nope", x.Deleter.Request(1, "nope", Now).Human);
        Assert.Equal(0, x.Arming.Count);
    }

    // ---- D9 TriggerFields

    [Fact]
    public void TriggerFields_passes_each_type()
    {
        var lib = Lib(Json.Event("raid"));
        Assert.Equal("event raid trigger.type = Schedule", Set(lib, "raid", "trigger.type", "Schedule"));
        Assert.Equal("schedule Sat 20:00", EventLines.Trigger(lib.Catalog.Current.Find("raid")!.Trigger));
        Assert.Equal("event raid trigger.days = Sat,Sun", Set(lib, "raid", "trigger.days", "Sat,Sun"));
        Assert.Equal("event raid trigger.times = 08:00,20:00", Set(lib, "raid", "trigger.times", "08:00,20:00"));
        Assert.Equal("schedule Sat,Sun 08:00,20:00", EventLines.Trigger(lib.Catalog.Current.Find("raid")!.Trigger));

        Set(lib, "raid", "trigger.type", "GameTime");
        var trigger = Entry(lib.Text, "raid")["trigger"]!.AsObject();
        Assert.Equal(["type", "phase"], trigger.Select(p => p.Key));                 // no key of the old type stays
        Assert.Equal("event raid trigger.phase = day", Set(lib, "raid", "trigger.phase", "day"));

        Set(lib, "raid", "trigger.type", "VBloodKilled");
        Assert.Equal("vbloodkilled any", EventLines.Trigger(lib.Catalog.Current.Find("raid")!.Trigger));
        Assert.Equal("event raid trigger.bosses = CHAR_Bandit_Tourok_VBlood", Set(lib, "raid", "trigger.bosses", "CHAR_Bandit_Tourok_VBlood"));
        Set(lib, "raid", "trigger.type", "Manual");
        Assert.Equal(["type"], Entry(lib.Text, "raid")["trigger"]!.AsObject().Select(p => p.Key));
    }

    [Theory]
    [InlineData("trigger.days", "Sat Sun", "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated")]
    [InlineData("trigger.days", "\"Sat\"", "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated")]
    [InlineData("trigger.days", "Sat,{", "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated")]
    [InlineData("trigger.days", "Sat\u0001", "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated")]
    [InlineData("trigger.days", "Sun,Mon,Tue,Wed,Thu,Fri,Sat,Sun", "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated")]
    [InlineData("trigger.times", "00:00,01:00,02:00,03:00,04:00,05:00,06:00,07:00,08:00,09:00,10:00,11:00,12:00", "trigger.times must be 1-12 of HH:mm, comma separated")]
    [InlineData("trigger.times", "24:00", "trigger.times must be 1-12 of HH:mm, comma separated")]
    [InlineData("trigger.phase", "dusk", "trigger.phase must be day or night")]
    [InlineData("trigger.type", "Hourly", CommandArgs.TriggerTypeRule)]
    [InlineData("trigger.bosses", "CHAR_A1,CHAR_A2,CHAR_A3,CHAR_A4,CHAR_A5,CHAR_A6,CHAR_A7,CHAR_A8,CHAR_A9,CHAR_A10,CHAR_A11,CHAR_A12,CHAR_A13,CHAR_A14,CHAR_A15,CHAR_A16,CHAR_A17,CHAR_A18,CHAR_A19,CHAR_A20,CHAR_A21", "trigger.bosses must be any or 1-20 CHAR_ names, comma separated")]
    [InlineData("trigger.bosses", "CHAR_Bandit-Tourok", "trigger.bosses names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters")]
    public void TriggerFields_fails_when_value_is_bad(string field, string value, string reply)
    {
        var lib = Lib(Json.Event("raid", trigger: Schedule), Json.Event("night", trigger: "{ \"type\": \"GameTime\", \"phase\": \"night\" }"),
            Json.Event("kill", trigger: "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }"));
        var id = field switch { "trigger.phase" => "night", "trigger.bosses" => "kill", _ => "raid" };
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, id, field, value)));
    }

    [Theory]
    [InlineData("trigger.days", "Sat,Sat", "Sat")]
    public void TriggerFields_fails_when_duplicate_day(string field, string value, string twice) => Duplicate(field, value, twice, "raid");

    [Theory]
    [InlineData("trigger.times", "20:00,20:00", "20:00")]
    public void TriggerFields_fails_when_duplicate_time(string field, string value, string twice) => Duplicate(field, value, twice, "raid");

    [Theory]
    [InlineData("trigger.bosses", "CHAR_Bandit_Tourok_VBlood,CHAR_Bandit_Tourok_VBlood", "CHAR_Bandit_Tourok_VBlood")]
    public void TriggerFields_fails_when_duplicate_boss(string field, string value, string twice) => Duplicate(field, value, twice, "kill");

    static void Duplicate(string field, string value, string twice, string id)
    {
        var lib = Lib(Json.Event("raid", trigger: Schedule), Json.Event("kill", trigger: "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }"),
            Json.Empower("surge"));
        Unchanged(lib, () => Assert.Equal($"{field} lists {twice} twice; nothing written", Set(lib, id, field, value)));
    }

    [Theory]
    [InlineData("trigger.days", "Sun", "trigger.days needs a Schedule trigger")]
    [InlineData("trigger.times", "08:00", "trigger.times needs a Schedule trigger")]
    [InlineData("trigger.phase", "day", "trigger.phase needs a GameTime trigger")]
    [InlineData("trigger.bosses", "any", "trigger.bosses needs a VBloodKilled trigger")]
    public void TriggerFields_fails_when_other_trigger_type(string field, string value, string reply)
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, "raid", field, value)));
    }

    [Fact]
    public void TriggerFields_empty_value()
    {
        var lib = Lib(Json.Event("raid", trigger: Schedule));
        foreach (var f in CommandArgs.TriggerFields) Unchanged(lib, () => Assert.NotNull(CommandArgs.SettableValue(f, "").Error));
        Unchanged(lib, () => Assert.StartsWith("trigger.days must be", Set(lib, "raid", "trigger.days", "")));
    }

    // ---- D10 ActionFields

    [Fact]
    public void ActionFields_passes_factions_and_units()
    {
        var lib = Lib(Json.Event("raid"), Json.Empower("surge"));
        Assert.Equal("event surge action.factions = Faction_Legion,Faction_Undead", Set(lib, "surge", "action.factions", "Faction_Legion,Faction_Undead"));
        Assert.Equal(["Faction_Legion", "Faction_Undead"], lib.Catalog.Current.Find("surge")!.Empower!.Factions);
        Assert.Equal("event raid action.units = CHAR_Bandit_Thug:3,CHAR_Bandit_Deadeye:1", Set(lib, "raid", "action.units", "CHAR_Bandit_Thug:3,CHAR_Bandit_Deadeye"));
        Assert.Equal([("CHAR_Bandit_Thug", 3), ("CHAR_Bandit_Deadeye", 1)], lib.Catalog.Current.Find("raid")!.Action!.Units.Select(u => (u.Prefab, u.Count)));
        Assert.Contains("\"count\": 1", lib.Text);                                     // a count-less entry is written as 1
    }

    [Fact]
    public void ActionFields_passes_unknown_name_disabled_by_reload()
    {
        var lib = Lib(Json.Event("raid"));
        Assert.StartsWith("event raid action.units = CHAR_Nobody:2; now disabled: ", Set(lib, "raid", "action.units", "CHAR_Nobody:2"));
    }

    static string Long(string prefix) => prefix + new string('a', 400 - prefix.Length);

    public static TheoryData<string, string, string, string> BadActionValues => new()
    {
        { "surge", "action.factions", "Faction_A,Faction_B,Faction_C,Faction_D,Faction_E,Faction_F", "action.factions must be 1-5 Faction_ names, comma separated" },
        { "raid", "action.units", string.Join(",", Enumerable.Range(1, 11).Select(i => $"CHAR_U{i}")), "action.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated" },
        { "raid", "action.units", "CHAR_Bandit_Thug:0", "action.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated" },
        { "raid", "action.units", "CHAR_Bandit_Thug:51", "action.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated" },
        { "raid", "action.units", "CHAR_Bandit-Thug", "action.units names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters" },
        { "raid", "action.units", Long("CHAR_"), "action.units names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters" },
        { "surge", "action.factions", Long("Faction_"), "action.factions names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters" },
        { "surge", "action.units", "CHAR_Bandit_Thug", "action.units is not an Empower field" },
        { "raid", "action.factions", "Faction_Legion", "action.factions is not a SpawnWaves field" },
    };

    [Theory]
    [MemberData(nameof(BadActionValues))]
    public void ActionFields_fails_when_value_is_bad(string id, string field, string value, string reply)
    {
        var lib = Lib(Json.Event("raid"), Json.Empower("surge"));
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, id, field, value)));
    }

    [Theory]
    [InlineData("CHAR_Bandit_Thug:3,CHAR_Bandit_Thug:2", "CHAR_Bandit_Thug")]
    [InlineData("CHAR_Bandit_Thug,CHAR_Bandit_Thug", "CHAR_Bandit_Thug")]
    public void ActionFields_fails_when_duplicate_unit(string value, string twice)
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal($"action.units lists {twice} twice; nothing written", Set(lib, "raid", "action.units", value)));
    }

    [Fact]
    public void ActionFields_fails_when_duplicate_faction()
    {
        var lib = Lib(Json.Empower("surge"));
        Unchanged(lib, () => Assert.Equal("action.factions lists Faction_Legion twice; nothing written", Set(lib, "surge", "action.factions", "Faction_Legion,Faction_Legion")));
    }

    [Fact]
    public void ActionFields_empty_value()
    {
        var lib = Lib(Json.Event("raid"), Json.Empower("surge"));
        Unchanged(lib, () => Assert.StartsWith("action.units must be", Set(lib, "raid", "action.units", "")));
        Unchanged(lib, () => Assert.StartsWith("action.factions must be", Set(lib, "surge", "action.factions", "")));
    }

    // ---- D11 Location

    [Fact]
    public void Location_passes_point_rounded()
    {
        var lib = Lib(Json.Event("raid", action: Json.ValidAction.Replace("{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }", "{ \"type\": \"Admin\" }")));
        Assert.Equal(LocationType.Admin, lib.Catalog.Current.Find("raid")!.Action!.Location.Type);
        Assert.Equal("event raid action.location = Point -1234.6, 800.0 at height 45.7", Set(lib, "raid", "location", "here", (-1234.56f, 45.66f, 800.04f)));
        var loc = Entry(lib.Text, "raid")["action"]!["location"]!.AsObject();
        Assert.Equal("{\"type\":\"Point\",\"x\":-1234.6,\"y\":45.7,\"z\":800.0}", loc.ToJsonString());
        var l = lib.Catalog.Current.Find("raid")!.Action!.Location;
        Assert.Equal((LocationType.Point, -1234.6, 45.7, 800.0), (l.Type, Math.Round((double)l.X, 1), Math.Round((double)l.Y!.Value, 1), Math.Round((double)l.Z, 1)));
        Assert.Contains("at -1234.6 800 height 45.7", string.Join("\n", EventLines.Info(lib.Catalog.Current.Find("raid")!, null, Now)));
        Assert.Equal("event raid action.location = Point 10000.0, -10000.0 at height -10000.0", Set(lib, "raid", "location", "here", (10000f, -10000f, -10000f)));
    }

    [Theory]
    [InlineData("raid", 10000.1f, 0f, 0f, "location here must be within -10000..10000")]
    [InlineData("raid", 0f, 0f, -20000f, "location here must be within -10000..10000")]
    [InlineData("raid", float.NaN, 0f, 0f, "location here must be within -10000..10000")]
    [InlineData("raid", 0f, 10000.1f, 0f, "location here must be within -10000..10000")]
    [InlineData("raid", 0f, float.PositiveInfinity, 0f, "location here must be within -10000..10000")]
    [InlineData("surge", 5f, 5f, 5f, "location is a SpawnWaves field")]
    public void Location_fails_when_refused(string id, float x, float y, float z, string reply)
    {
        var lib = Lib(Json.Event("raid"), Json.Empower("surge"));
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, id, "location", "here", (x, y, z))));
        Assert.Null(Entry(lib.Text, "surge")["action"]!["location"]);
    }

    [Fact]
    public void Location_empty_no_character()
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal("location here needs your character in the world", Set(lib, "raid", "location", "here", null)));
        Unchanged(lib, () => Assert.Equal("location takes here or aroundplayer MIN MAX", Set(lib, "raid", "location", "there")));
    }

    // ---- D12 Equivalence

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Everything a definition set shows: each definition's info lines, enabled flag and disabled reason.</summary>
    internal static string Snapshot(DefinitionSet set) =>
        string.Join("\n", set.All.Select(d => $"{d.Id}|{d.Enabled}|{d.DisabledReason}|{string.Join("/", EventLines.Info(d, null, Now))}"));

    /// <summary>The seven write kinds, each as the command and as the hand edit of the same JSON, built here from the
    /// file with JsonNode, not by the planners.</summary>
    public static TheoryData<string> Kinds => new() { "template use", "new", "copy", "delete", "set trigger", "set action", "location" };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Equivalence_passes_write_equals_hand_edit(string kind)
    {
        var lib = Lib(Json.Event("raid", trigger: Schedule), Json.Empower("surge"));
        var before = JsonNode.Parse(lib.Text)!;
        var events = (JsonArray)before["events"]!;
        JsonObject Ev(string id) => events.OfType<JsonObject>().Single(e => (string)e["id"]! == id);
        string reply;
        switch (kind)
        {
            case "template use":
                reply = lib.Write(t => Authoring.TemplateUse(t, TemplateLibraryTests.Real(), "undead-rising", null));
                events.Add(JsonNode.Parse(TemplateLibraryTests.Real().Find("undead-rising")!.Json.ToJsonString()));
                break;
            case "new":
                reply = lib.Write(t => Authoring.New(t, "fresh", "spawns"));
                events.Add(JsonNode.Parse("{ \"id\": \"fresh\", \"name\": \"fresh\", \"enabled\": false, \"pillar\": \"spawns\", \"trigger\": { \"type\": \"Manual\" }, " +
                    "\"durationSeconds\": 600, \"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 3 } ], " +
                    "\"waves\": 1, \"intervalSeconds\": 60, \"radius\": 8, \"location\": { \"type\": \"Admin\" } } }"));
                break;
            case "copy":
                reply = lib.Write(t => Authoring.Copy(t, "surge", "surge-2"));
                var copy = JsonNode.Parse(Ev("surge").ToJsonString())!.AsObject();
                copy["id"] = "surge-2";
                copy["enabled"] = false;
                events.Add(copy);
                break;
            case "delete":
                reply = lib.Write(t => Authoring.Delete(t, "raid"));
                events.Remove(Ev("raid"));
                break;
            case "set trigger":
                reply = Set(lib, "raid", "trigger.days", "Sun,Wed");
                Ev("raid")["trigger"]!["days"] = new JsonArray("Sun", "Wed");
                break;
            case "set action":
                reply = Set(lib, "raid", "action.units", "CHAR_Bandit_Deadeye:4");
                Ev("raid")["action"]!["units"] = JsonNode.Parse("[ { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 4 } ]");
                break;
            default:
                reply = Set(lib, "raid", "location", "here", (12.34f, 5.66f, -56.78f));
                Ev("raid")["action"]!["location"] = JsonNode.Parse("{ \"type\": \"Point\", \"x\": 12.3, \"y\": 5.7, \"z\": -56.8 }");
                break;
        }
        Assert.DoesNotContain("refused", reply);
        Assert.Equal(1, lib.ConfigChanged);
        Assert.NotNull(lib.Bak);
        Assert.Equal(Snapshot(lib.HandEdit(before.ToJsonString(Indented))), Snapshot(lib.Catalog.Current));
    }

    [Fact]
    public void Equivalence_passes_two_admin_sequential_set()
    {
        var lib = Lib(Json.Event("raid"));
        Assert.Equal("event raid name = First admin", Set(lib, "raid", "name", "First admin"));      // admin A
        Assert.Equal("event raid durationSeconds = 900", Set(lib, "raid", "durationSeconds", "900")); // admin B, the stamp A's reload produced
        var d = lib.Catalog.Current.Find("raid")!;
        Assert.Equal(("First admin", 900), (d.Name, d.DurationSeconds));
        Assert.Equal(2, lib.ConfigChanged);
    }

    [Fact]
    public void Equivalence_passes_running_instance_keeps_end_time()
    {
        var lib = Lib(Json.Event("raid"));
        Assert.Null(lib.Catalog.TryStart("raid", Now, out var running));
        Set(lib, "raid", "durationSeconds", "60");
        lib.Write(t => Authoring.Copy(t, "raid", "raid-2"));
        Assert.Equal(Now.AddSeconds(600), lib.Catalog.Running.Single().EndsUtc);
        Assert.Same(running, lib.Catalog.Running.Single());
        Assert.Equal(60, lib.Catalog.Current.Find("raid")!.DurationSeconds);
    }

    [Fact]
    public void Equivalence_fails_when_file_changed_on_disk()
    {
        var lib = Lib(Json.Event("raid"));
        lib.Fs.Now = lib.Fs.Now.AddMinutes(1);
        lib.Fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("raid"), Json.Event("hand")));
        Unchanged(lib, () => Assert.Equal("events.json changed on disk, run .nyar event reload first", lib.Write(t => Authoring.New(t, "x", "spawns"))));
        Unchanged(lib, () => Assert.Equal("events.json changed on disk, run .nyar event reload first", Set(lib, "raid", "durationSeconds", "90")));
    }

    [Theory]
    [InlineData("target deleted")]
    [InlineData("new id added")]
    [InlineData("root not an object")]
    public void Equivalence_fails_when_stale_file_alters_the_plan(string edit)
    {
        var lib = Lib(Json.Event("raid"));
        lib.Fs.Now = lib.Fs.Now.AddMinutes(1);
        var text = edit switch
        {
            "target deleted" => Json.File(),
            "new id added" => Json.File(Json.Event("raid"), Json.Event("raid-2")),
            _ => "[]",
        };
        lib.Fs.Put(DataFile.Events, FileVariant.Main, text);
        const string stale = "events.json changed on disk, run .nyar event reload first";
        Unchanged(lib, () => Assert.Equal(stale, Set(lib, "raid", "durationSeconds", "90")));
        Unchanged(lib, () => Assert.Equal(stale, lib.Write(t => Authoring.Copy(t, "raid", "raid-2"))));
    }

    [Fact]
    public void Equivalence_fails_when_newer_schema()
    {
        var lib = new Library(Json.File(Json.Event("raid")).Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2"), Units());
        Unchanged(lib, () => Assert.Equal("events.json SchemaVersion 2 is newer than this version of Nyarlathotep; it is read-only",
            lib.Write(t => Authoring.New(t, "x", "spawns"))));
    }

    [Fact]
    public void Equivalence_fails_when_over_1_mb()
    {
        // 180 definitions whose over-long names disable them but keep them in the file, padded to just under 1 MB.
        string[] Events(int nameLength) =>
            Enumerable.Range(0, 180).Select(i => Json.Event($"e{i:000}").Replace("\"Bandit raid\"", $"\"{new string('n', nameLength)}\"")).ToArray();
        var bare = System.Text.Encoding.UTF8.GetByteCount(Json.File(Events(0)));
        var events = Events((1_040_000 - bare) / 180);
        var lib = new Library(Json.File(events), Units());
        Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(lib.Text), 1_000_000, EventValidator.MaxFileBytes);
        Unchanged(lib, () => Assert.Equal("events.json would exceed 1 MB; nothing written", lib.Write(t => Authoring.Copy(t, "e000", "e-copy"))));
    }

    [Fact]
    public void Equivalence_empty_refused_plan()
    {
        var lib = Lib(Json.Event("raid"));
        Unchanged(lib, () => Assert.Equal("no", lib.Write(_ => EditPlan.Refuse(Outcome.Refused("no", RefusalCode.Invalid)))));
        Assert.Null(lib.Bak);
    }
}
