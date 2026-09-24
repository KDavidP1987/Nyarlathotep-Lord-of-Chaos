using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D5: events.json v1 validation.</summary>
public class EventValidationTests
{
    static LoadResult Load(string text) => EventValidator.Parse(text, FakeUnits.Default());

    [Fact]
    public void A_valid_event_loads_enabled()
    {
        var d = Json.One(Json.Event());
        Assert.Null(d.DisabledReason);
        Assert.True(d.Startable);
        Assert.Equal("raid", d.Id);
        Assert.Equal(Pillar.Spawns, d.Pillar);
        Assert.Equal(600, d.DurationSeconds);
        var a = Assert.IsType<SpawnWavesAction>(d.Action);
        Assert.Equal(3, a.Waves);
        Assert.Equal(LocationType.Point, a.Location.Type);
        Assert.Equal(-1200.5f, a.Location.X);
    }

    [Fact]
    public void All_trigger_types_and_optional_blocks_load()
    {
        var r = Load(Json.File(
            Json.Event("a", "{ \"type\": \"Schedule\", \"days\": [\"Sat\", \"Sun\"], \"times\": [\"20:00\", \"08:30\"] }"),
            Json.Event("b", "{ \"type\": \"GameTime\", \"phase\": \"night\" }"),
            Json.Event("c", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }",
                "\"conditions\": { \"minPlayers\": 2, \"cooldownMinutes\": 60, \"chancePercent\": 50, \"window\": { \"from\": \"18:00\", \"to\": \"23:00\" }, \"mode\": \"pve\" }, " +
                "\"announce\": { \"start\": [\"{event} begins: wave {wave} of {waves}\"], \"end\": [], \"warnings\": true }"),
            Json.Event("d", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"CHAR_Bandit_Tourok_VBlood\"] }")));
        Assert.Null(r.FileError);
        Assert.Empty(r.Log);
        Assert.Equal(4, r.Set.All.Count);
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], r.Set.Find("a")!.Trigger.Days);
        Assert.Equal(DayPhase.Night, r.Set.Find("b")!.Trigger.Phase);
        Assert.Equal(GameMode.Pve, r.Set.Find("c")!.Conditions.Mode);
        Assert.True(r.Set.Find("c")!.Announce.Warnings);
    }

    // Each invalid definition is disabled with exactly one reason that names its field.
    [Theory]
    [InlineData("\"bogus\": 1", "unknown field bogus")]
    [InlineData("\"announce\": { \"start\": [\"hi {player}\"] }", "unknown placeholder {player}")]
    [InlineData("\"announce\": { \"start\": [\"<color=red>hi</color>\"] }", "announce.start must be 0-5 lines of 1-200 characters, no < > or control characters")]
    [InlineData("\"announce\": { \"start\": [\"a\",\"b\",\"c\",\"d\",\"e\",\"f\"] }", "announce.start must be 0-5 lines of 1-200 characters, no < > or control characters")]
    [InlineData("\"announce\": { \"loud\": true }", "unknown field announce.loud")]
    [InlineData("\"conditions\": { \"minPlayers\": 101 }", "conditions.minPlayers must be 0-100")]
    [InlineData("\"conditions\": { \"chancePercent\": 0 }", "conditions.chancePercent must be 1-100")]
    [InlineData("\"conditions\": { \"cooldownMinutes\": 10081 }", "conditions.cooldownMinutes must be 0-10080")]
    [InlineData("\"conditions\": { \"mode\": \"arena\" }", "conditions.mode must be any, pve or pvp")]
    [InlineData("\"conditions\": { \"window\": { \"from\": \"25:00\", \"to\": \"23:00\" } }", "conditions.window must be { \"from\": \"HH:mm\", \"to\": \"HH:mm\" }")]
    public void Invalid_optional_fields_disable_the_event(string extra, string reason)
    {
        var d = Json.One(Json.Event(extra: extra));
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
    }

    [Theory]
    [InlineData("{ \"type\": \"Cron\" }", "unknown trigger type Cron")]
    [InlineData("{ \"type\": \"Schedule\", \"days\": [\"Funday\"], \"times\": [\"20:00\"] }", "trigger.days must be 1-7 of Mon Tue Wed Thu Fri Sat Sun")]
    [InlineData("{ \"type\": \"Schedule\", \"days\": [], \"times\": [\"20:00\"] }", "trigger.days must be 1-7 of Mon Tue Wed Thu Fri Sat Sun")]
    [InlineData("{ \"type\": \"Schedule\", \"days\": [\"Mon\"], \"times\": [\"8:00\"] }", "trigger.times must be 1-12 of HH:mm")]
    [InlineData("{ \"type\": \"GameTime\", \"phase\": \"dusk\" }", "trigger.phase must be day or night")]
    [InlineData("{ \"type\": \"VBloodKilled\", \"bosses\": [\"CHAR_Nobody\"] }", "unknown unit CHAR_Nobody")]
    [InlineData("{ \"type\": \"VBloodKilled\", \"bosses\": [\"Boss_Known\"] }", "unknown unit Boss_Known")]
    [InlineData("{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\", \"CHAR_Bandit_Tourok_VBlood\"] }", "trigger.bosses must be [\"any\"] or 1-20 CHAR_ names")]
    [InlineData("{ \"type\": \"Manual\", \"when\": 1 }", "unknown field trigger.when")]
    public void Invalid_triggers_disable_the_event(string trigger, string reason)
    {
        Assert.Equal(reason, Json.One(Json.Event(trigger: trigger)).DisabledReason);
    }

    static string Action(string units = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ]", string waves = "3", string interval = "60",
        string radius = "10", string location = "{ \"type\": \"Point\", \"x\": 0, \"z\": 0 }", string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": " + waves + ", \"intervalSeconds\": " + interval +
        ", \"radius\": " + radius + ", \"location\": " + location + (extra is null ? "" : ", " + extra) + " }";

    public static IEnumerable<object[]> InvalidActions() =>
    [
        ["\"action\": { \"type\": \"Empower\" }", "unknown action type Empower"],
        [Action(units: "[]"), "action.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }"],
        [Action(units: "[ { \"prefab\": \"CHAR_Nobody\", \"count\": 1 } ]"), "unknown unit CHAR_Nobody"],
        [Action(units: "[ { \"prefab\": \"CHAR_Mount_Horse_Vampire\", \"count\": 1 } ]"), "unit CHAR_Mount_Horse_Vampire is deny-listed"],
        [Action(units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 51 } ]"), "action.units.count must be 1-50"],
        [Action(waves: "0"), "action.waves must be 1-10"],
        [Action(waves: "11"), "action.waves must be 1-10"],
        [Action(interval: "9"), "action.intervalSeconds must be 10-600"],
        [Action(radius: "31"), "action.radius must be 2-30"],
        [Action(radius: "2.5"), "action.radius must be 2-30"],
        [Action(location: "{ \"type\": \"Point\", \"x\": 1e9, \"z\": 0 }"), "action.location.x must be a number within -10000..10000"],
        [Action(location: "{ \"type\": \"Zone\" }"), "action.location must be { \"type\": \"Point\", \"x\": number, \"z\": number } or { \"type\": \"Admin\" }"],
        [Action(extra: "\"unitLifetimeSeconds\": 10"), "action.unitLifetimeSeconds must be 30-7200"],
        [Action(extra: "\"loot\": true"), "unknown field action.loot"],
    ];

    [Theory]
    [MemberData(nameof(InvalidActions))]
    public void Invalid_actions_disable_the_event(string action, string reason)
    {
        Assert.Equal(reason, Json.One(Json.Event(action: action)).DisabledReason);
    }

    [Fact]
    public void A_denied_unit_from_the_catalog_is_refused()
    {
        var units = FakeUnits.Default();
        units.Denied.Add("CHAR_Bandit_Thug");
        var r = EventValidator.Parse(Json.File(Json.Event()), units);
        Assert.Equal("unit CHAR_Bandit_Thug is deny-listed", r.Set.All[0].DisabledReason);
    }

    [Fact]
    public void Admin_location_needs_a_manual_trigger()
    {
        var sched = "{ \"type\": \"Schedule\", \"days\": [\"Mon\"], \"times\": [\"20:00\"] }";
        var d = Json.One(Json.Event(trigger: sched, action: Action(location: "{ \"type\": \"Admin\" }")));
        Assert.Equal("action.location Admin needs a Manual trigger", d.DisabledReason);
        Assert.Null(Json.One(Json.Event(action: Action(location: "{ \"type\": \"Admin\" }"))).DisabledReason);
    }

    [Theory]
    [InlineData("{ \"id\": \"Raid!\", \"name\": \"x\" }", "#1", "id must be 1-32 of a-z 0-9 -")]
    [InlineData("{ \"id\": \"raid\", \"name\": \"\" }", "raid", "name must be 1-40 characters, no < > or control characters")]
    [InlineData("{ \"id\": \"raid\", \"name\": \"x\", \"enabled\": \"yes\" }", "raid", "enabled must be true or false")]
    [InlineData("{ \"id\": \"raid\", \"name\": \"x\", \"enabled\": true, \"pillar\": \"chaos\" }", "raid", "unknown pillar chaos")]
    [InlineData("{ \"id\": \"raid\", \"name\": \"x\", \"enabled\": true, \"pillar\": \"spawns\", \"trigger\": { \"type\": \"Manual\" }, \"durationSeconds\": 29 }", "raid", "durationSeconds must be 30-7200")]
    [InlineData("[1]", "#1", "an event must be an object")]
    public void Invalid_required_fields_disable_the_event(string json, string id, string reason)
    {
        var d = Json.One(json);
        Assert.Equal(id, d.Id);
        Assert.Equal(reason, d.DisabledReason);
    }

    [Fact]
    public void Duplicate_id_disables_the_second()
    {
        var r = Load(Json.File(Json.Event(), Json.Event()));
        Assert.Equal(2, r.Set.All.Count);
        Assert.Single(r.Set.All, d => d.DisabledReason is null);
        Assert.Single(r.Set.All, d => d.DisabledReason == "duplicate id");
        Assert.Null(r.Set.Find("raid")!.DisabledReason);
        Assert.Equal(["event raid: duplicate id"], r.Log);
    }

    [Fact]
    public void Invalid_events_leave_valid_ones_loaded_and_are_logged()
    {
        var r = Load(Json.File(Json.Event("good"), Json.Event("bad", extra: "\"bogus\": 1")));
        Assert.True(r.Set.Find("good")!.Startable);
        Assert.False(r.Set.Find("bad")!.Startable);
        Assert.Equal(["event bad: unknown field bogus"], r.Log);
    }

    [Fact]
    public void Unparsable_file_is_rejected_whole_with_line_and_position()
    {
        var r = Load("{ \"SchemaVersion\": 1,\n  \"events\": [ { \"id\": } ] }");
        Assert.Equal("events.json rejected: line 2 position 23", r.FileError);
        Assert.Empty(r.Set.All);
    }

    [Theory]
    [InlineData("[]", "events.json rejected: the top level must be an object { \"SchemaVersion\": 1, \"events\": [ ... ] }")]
    [InlineData("{ \"events\": [] }", "events.json rejected: SchemaVersion must be an integer 1 or higher")]
    [InlineData("{ \"SchemaVersion\": 0, \"events\": [] }", "events.json rejected: SchemaVersion must be an integer 1 or higher")]
    [InlineData("{ \"SchemaVersion\": 1 }", "events.json rejected: events must be an array")]
    [InlineData("{ \"SchemaVersion\": 1, \"events\": [], \"extra\": 1 }", "events.json rejected: unknown top-level field extra")]
    [InlineData("{ \"SchemaVersion\": 1, \"events\": [], }", "events.json rejected: line 1 position 37")]
    public void File_level_errors_reject_the_whole_file(string text, string error)
    {
        Assert.Equal(error, Load(text).FileError);
    }

    [Fact]
    public void More_than_200_definitions_are_rejected()
    {
        var events = Enumerable.Range(0, 201).Select(i => Json.Event($"e{i}")).ToArray();
        Assert.Equal("events.json rejected: 201 events, over the limit of 200", Load(Json.File(events)).FileError);
        Assert.Null(Load(Json.File(events.Take(200).ToArray())).FileError);
    }

    [Fact]
    public void A_file_over_1_MB_is_rejected()
    {
        var text = Json.File(Json.Event()) + new string(' ', EventValidator.MaxFileBytes);
        Assert.StartsWith("events.json is ", Load(text).FileError);
    }

    [Fact]
    public void A_newer_schema_version_is_reported()
    {
        Assert.Equal(2, Load("{ \"SchemaVersion\": 2, \"events\": [] }").SchemaVersion);
    }

    [Theory]
    [InlineData("{faction} rises for {minutes} minutes", null)]
    [InlineData("{event} in {zone}: wave {wave}/{waves}", null)]
    [InlineData("{x}", "x")]
    [InlineData("hello {Player}", "Player")]
    public void Placeholders_are_limited(string template, string? bad)
    {
        Assert.Equal(bad, EventValidator.UnknownPlaceholder(template));
    }
}
