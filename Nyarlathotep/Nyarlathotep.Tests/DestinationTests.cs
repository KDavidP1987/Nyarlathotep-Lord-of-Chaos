using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D5 (Schema: the destination and marchSpeed keys of a SpawnWaves action, their refusals, chat
/// fields and info line) and D6 (March: each wave's front moves to the destination by the apocalypse's march rule, holds
/// while a player is near, hops stragglers only unobserved, arrives within the radius or stalls after 120 s unheld, and
/// then takes the action's behaviour).</summary>
public class DestinationTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    const string Camp = "{ \"location\": { \"type\": \"Point\", \"x\": -1000, \"z\": -800 }, \"radius\": 20, \"name\": \"the camp\" }";

    /// <summary>Json.ValidAction (Point -1200.5, -800, three waves of five thugs) with <paramref name="extra"/> keys.</summary>
    static string Action(string extra) => Json.ValidAction[..^2] + ", " + extra + " }";

    static EventDefinition Def(string extra, string? trigger = null) =>
        Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid", trigger, action: Action(extra))), FakeUnits.Default(), regions: FakeRegions.All()).Set.All);

    static string? Refusal(string extra, string? trigger = null) => Def(extra, trigger).DisabledReason;

    static Library Lib(string extra = "\"destination\": " + Camp) => new(Json.File(Json.Event("raid", action: Action(extra)), Apo.Event()));

    static JsonObject ActionJson(Library lib) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == "raid")["action"]!;

    // ---- D5 Schema

    [Fact]
    public void Schema_passes_a_point_or_admin_destination_and_shows_the_march()
    {
        var d = Def("\"destination\": " + Camp + ", \"marchSpeed\": 3.5");
        Assert.True(d.Startable, d.DisabledReason);
        Assert.Equal(new Destination(new Location(LocationType.Point, -1000, -800), 20, "the camp"), d.Action!.Destination);
        Assert.Equal(3.5, d.Action.MarchSpeed);
        Assert.Contains("march: from (-1200.5, -800) to the camp (-1000, -800) at 3.5 m/s", EventLines.Info(d, null, T0));
        Assert.Equal(DestinationLimits.DefaultMarchSpeed, Def("\"destination\": " + Camp).Action!.MarchSpeed);   // 2.0 when absent
        var admin = Def("\"destination\": { \"location\": { \"type\": \"Admin\" }, \"radius\": 100, \"name\": \"" + new string('n', 40) + "\" }");
        Assert.True(admin.Startable, admin.DisabledReason);
        Assert.Equal("march: from (-1200.5, -800) to " + new string('n', 40) + " (the starting admin) at 2 m/s", EventLines.MarchLine(admin.Action!));
    }

    [Theory]
    [InlineData("\"destination\": []", EventValidator.DestinationObject)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Region\" }, \"radius\": 20, \"name\": \"c\" }", EventValidator.DestinationLocationRule)]
    [InlineData("\"destination\": { \"radius\": 20, \"name\": \"c\" }", EventValidator.DestinationLocationRule)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"radius\": 9, \"name\": \"c\" }", EventValidator.DestinationRadiusRule)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"radius\": 101, \"name\": \"c\" }", EventValidator.DestinationRadiusRule)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"radius\": 20, \"name\": \"\" }", EventValidator.DestinationNameRule)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"radius\": 20, \"name\": \"<b>c</b>\" }", EventValidator.DestinationNameRule)]
    [InlineData("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"radius\": 20, \"name\": \"c\", \"far\": 1 }", "unknown field action.destination.far")]
    [InlineData("\"marchSpeed\": 2.0", EventValidator.MarchSpeedNeedsDestination)]
    [InlineData("\"destination\": " + Camp + ", \"marchSpeed\": 0.4", EventValidator.MarchSpeedRule)]
    [InlineData("\"destination\": " + Camp + ", \"marchSpeed\": 5.01", EventValidator.MarchSpeedRule)]
    [InlineData("\"destination\": " + Camp + ", \"arena\": { \"radius\": 30 }", "action.arena takes no action.destination")]
    [InlineData("\"destination\": " + Camp + ", \"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }, \"pursuit\": { \"afterSeconds\": 60 }", "action.pursuit takes no action.destination")]
    [InlineData("\"destination\": " + Camp + ", \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }", EventValidator.FanOutLocation)]
    public void Schema_fails_when_a_refused_combination_is_accepted(string extra, string reason)
    {
        Assert.Equal(reason, Refusal(extra));
        Assert.Contains(reason.Split(' ')[0], reason);                                 // each reason names its field
    }

    [Fact]
    public void Schema_fails_when_a_valid_destination_is_refused_or_a_start_is_wrong()
    {
        Assert.Null(Refusal("\"destination\": " + Camp));
        Assert.Equal(EventValidator.DestinationCoordRule("x"),
            Refusal("\"destination\": { \"location\": { \"type\": \"Point\", \"x\": 10001, \"z\": 0 }, \"radius\": 20, \"name\": \"c\" }"));
        // an Admin destination needs a Manual trigger, as an Admin goal does
        Assert.Equal(EventValidator.DestinationAdminManual, Refusal("\"destination\": { \"location\": { \"type\": \"Admin\" }, \"radius\": 20, \"name\": \"c\" }",
            "{ \"type\": \"Schedule\", \"days\": [\"Sat\"], \"times\": [\"20:00\"] }"));
        // the start must be Point or Admin: an AroundPlayer start is refused
        Assert.Equal(EventValidator.DestinationStart, Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid", action: Action("\"destination\": " + Camp)
            .Replace("\"location\": { \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }", "\"location\": { \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }"))),
            FakeUnits.Default(), regions: FakeRegions.All()).Set.All).DisabledReason);
        // the chat editor sees the same conflicts on the file
        var locked = Json.File(Json.Event("raid", action: Action("\"destination\": " + Camp + ", \"regionLock\": true, \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }")));
        var conflicts = EventsEditor.SwarmConflicts(locked, "raid");
        Assert.Contains(EventValidator.DestinationTakesNo("regionLock"), conflicts);
        Assert.Contains(EventValidator.DestinationTakesNo("fanOut"), conflicts);
        Assert.All(EventValidator.DestinationReasons, r => Assert.True(System.Text.Encoding.UTF8.GetByteCount(r) <= 120, r));
    }

    [Fact]
    public void Schema_fails_when_a_chat_field_writes_another_field()
    {
        var lib = Lib();
        var before = ActionJson(lib).ToDictionary(kv => kv.Key, kv => kv.Value?.ToJsonString());
        Assert.StartsWith("event raid action.destination.radius = 50", AuthoringTests.Set(lib, "raid", "action.destination.radius", "50"));
        Assert.StartsWith("event raid action.destination.name = ", AuthoringTests.Set(lib, "raid", "action.destination.name", "Brighthaven"));
        Assert.StartsWith("event raid action.marchSpeed = 4.5", AuthoringTests.Set(lib, "raid", "action.marchSpeed", "4.5"));
        Assert.StartsWith("event raid action.destination = ", AuthoringTests.Set(lib, "raid", "action.destination", "here", (-900f, 12f, -700f)));
        var after = ActionJson(lib);
        var dest = (JsonObject)after["destination"]!;
        Assert.Equal(["location", "name", "radius"], dest.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal((50, "Brighthaven"), ((int)dest["radius"]!, (string)dest["name"]!));
        Assert.Equal("{\"type\":\"Point\",\"x\":-900.0,\"y\":12.0,\"z\":-700.0}", dest["location"]!.ToJsonString());
        Assert.Equal(4.5m, (decimal)after["marchSpeed"]!);
        Assert.Equal(before.Keys.Append("marchSpeed").OrderBy(k => k), after.Select(kv => kv.Key).OrderBy(k => k));
        foreach (var key in before.Keys.Where(k => k != "destination"))
            Assert.Equal(before[key], after[key]?.ToJsonString());                                 // nothing else moved
        var d = lib.Catalog.Current.Find("raid")!;
        Assert.True(d.Startable, d.DisabledReason);
        // refusals: out of range, not here, on another kind, marchSpeed with no destination
        Assert.Equal(EventValidator.DestinationRadiusRule, AuthoringTests.Set(lib, "raid", "action.destination.radius", "101"));
        Assert.Equal(EventValidator.DestinationNameRule, AuthoringTests.Set(lib, "raid", "action.destination.name", new string('n', 41)));
        Assert.Equal(CommandArgs.DestinationHereRule, AuthoringTests.Set(lib, "raid", "action.destination", "0 0"));
        Assert.Equal("action.destination.radius is not an Apocalypse field", AuthoringTests.Set(lib, "apo", "action.destination.radius", "50"));
        var plain = new Library(Json.File(Json.Event("raid")));
        Assert.Equal(EventValidator.MarchSpeedNeedsDestination, AuthoringTests.Set(plain, "raid", "action.marchSpeed", "3"));
        Assert.Equal(EventValidator.NeedsDestination("action.destination.radius"), AuthoringTests.Set(plain, "raid", "action.destination.radius", "30"));
        Assert.Equal(EventValidator.NeedsDestination("action.destination.name"), AuthoringTests.Set(plain, "raid", "action.destination.name", "camp"));
        Assert.DoesNotContain("destination", ActionJson(plain).ToJsonString());
        // none removes the destination and its march speed, and leaves the rest as it was
        Assert.StartsWith("event raid action.destination = ", AuthoringTests.Set(lib, "raid", "action.destination", "none"));
        var removed = ActionJson(lib);
        Assert.Null(removed["destination"]);
        Assert.Null(removed["marchSpeed"]);
        foreach (var key in before.Keys.Where(k => k != "destination")) Assert.Equal(before[key], removed[key]?.ToJsonString());
        Assert.True(lib.Catalog.Current.Find("raid")!.Startable);
        Assert.Null(EventLines.MarchLine(lib.Catalog.Current.Find("raid")!.Action!));
    }

    [Fact]
    public void Schema_empty_no_destination_no_march() =>
        Assert.Null(EventLines.MarchLine(Def("\"loot\": false").Action!));

    // ---- D6 March

    static readonly IReadOnlyList<(float X, float Z)> Nobody = [];
    static bool Ground(float x, float z) => true;
    static bool NoGround(float x, float z) => false;

    static WaveMarch March(float distance = 200, int radius = 20, double speed = 2.0) =>
        new("raid", 1, (0, 0), (distance, 0), radius, speed, T0);

    [Fact]
    public void March_fails_when_a_stall_counts_ground_it_could_not_read()
    {
        // the walk budget spent: every crossing probe is unread, so the hold adds no stuck time however long it lasts
        var m = March();
        m.Front.LastWatchedUtc = T0.AddMinutes(-5);
        for (var s = 1; s <= 300; s++) Assert.Null(m.Step(1, Nobody, NoGround, T0.AddSeconds(s), 4, out _, (_, _) => null));
        Assert.Equal(WaveMarchState.Marching, m.State);
        Assert.Equal(0, m.StuckSeconds);
        // read and unwalkable, it stalls at 120 s
        string? line = null;
        for (var s = 301; s <= 430 && line is null; s++) line = m.Step(1, Nobody, NoGround, T0.AddSeconds(s), 4, out _, (_, _) => false);
        Assert.Equal(WaveMarchState.Stalled, m.State);
    }

    [Fact]
    public void March_fails_when_a_front_moves_while_a_player_is_within_60_m()
    {
        var m = March();
        Assert.Null(m.Step(1, Nobody, Ground, T0.AddSeconds(1), 10, out _));
        Assert.Equal(2f, m.Front.X, 3);
        Assert.Null(m.Step(5, [(m.Front.X + 59, 0)], Ground, T0.AddSeconds(6), 10, out _));
        Assert.Equal(2f, m.Front.X, 3);                                                    // held
        Assert.Null(m.Step(5, [(m.Front.X + 61, 0)], Ground, T0.AddSeconds(11), 10, out _));
        Assert.Equal(12f, m.Front.X, 3);                                                   // 61 m away: it walks on
        // stragglers hop only unobserved, never with a player within 100 m of them
        Assert.Empty(MarchRule.Stragglers(m.Front, [(1L, -50f, 0f)], [(m.Front.X + 61, 0)], T0.AddSeconds(15)));   // watched 4 s ago
        Assert.Empty(MarchRule.Stragglers(m.Front, [(1L, -50f, 0f)], [(-50f, 99f)], T0.AddSeconds(30)));
        Assert.Equal([1L], MarchRule.Stragglers(m.Front, [(1L, -50f, 0f)], [(-50f, 101f)], T0.AddSeconds(30)));
    }

    [Fact]
    public void March_fails_when_arrival_does_not_switch_the_behaviour()
    {
        var m = March(distance: 100, radius: 20, speed: 2.0);
        string? line = null;
        var t = T0;
        for (var s = 0; s < 60 && line is null; s++)
        {
            t = t.AddSeconds(1);
            line = m.Step(1, Nobody, Ground, t, 12, out _);
        }
        Assert.Equal("march raid wave 1: arrived after 40 s, 12 units", line);             // 80 m at 2 m/s, within 20 m of 100
        Assert.Equal(WaveMarchState.Arrived, m.State);
        Assert.True(m.Released);
        Assert.Null(m.Step(1, Nobody, Ground, t.AddSeconds(1), 12, out _));                // once
        Assert.Equal(80f, m.Front.X, 3);                                                   // and no further
        // a start already within the radius arrives on its first step
        var near = new WaveMarch("raid", 2, (0, 0), (10, 0), 20, 2.0, T0);
        Assert.Equal("march raid wave 2: arrived after 1 s, 3 units", near.Step(1, Nobody, Ground, T0.AddSeconds(1), 3, out _));
    }

    [Fact]
    public void March_fails_when_a_stall_leaves_the_wave_without_orders()
    {
        var m = March();
        var t = T0;
        string? line = null;
        // held by a player for 200 s: no stall, however long
        for (var s = 0; s < 200; s++) Assert.Null(m.Step(1, [(m.Front.X + 10, 0)], NoGround, t = t.AddSeconds(1), 5, out _));
        Assert.Equal(0, m.StuckSeconds);
        // unheld with no ground ahead: 120 s, then stalled where it stands
        for (var s = 0; s < 130 && line is null; s++) line = m.Step(1, Nobody, NoGround, t = t.AddSeconds(1), 5, out _);
        Assert.Equal("march raid wave 1: stalled 200 m short of its destination", line);   // stuck where it started (A5: no point)
        Assert.Equal(WaveMarchState.Stalled, m.State);
        Assert.True(m.Released);
        // held time in between does not count, and a step forward clears the stuck time
        var w = March();
        t = T0;
        for (var s = 0; s < 100; s++) w.Step(1, Nobody, NoGround, t = t.AddSeconds(1), 5, out _);
        for (var s = 0; s < 50; s++) w.Step(1, [(w.Front.X + 10, 0)], NoGround, t = t.AddSeconds(1), 5, out _);
        Assert.Equal(100, w.StuckSeconds);
        Assert.False(w.Released);
        w.Step(1, Nobody, Ground, t = t.AddSeconds(1), 5, out _);
        Assert.Equal(0, w.StuckSeconds);
        Assert.Equal("wave 1 held: no walkable ground ahead", MarchRule.HeldLine(1, w.Front.Kind));
    }

    [Fact]
    public void March_passes_the_front_from_the_action_to_its_destination()
    {
        var point = Def("\"destination\": " + Camp + ", \"marchSpeed\": 4").Action!;
        var m = WaveMarch.Of("raid", point, 2, (-1200, -800), null, T0)!;
        Assert.Equal((-1200f, -800f, -1000f, -800f, 4.0, 20), (m.Front.X, m.Front.Z, m.Front.GoalX, m.Front.GoalZ, m.Speed, m.Radius));
        var admin = Def("\"destination\": { \"location\": { \"type\": \"Admin\" }, \"radius\": 30, \"name\": \"c\" }").Action!;
        Assert.Equal((50f, 60f), (WaveMarch.Of("raid", admin, 1, (0, 0), (50f, 3f, 60f), T0)!.Front.GoalX, WaveMarch.Of("raid", admin, 1, (0, 0), (50f, 3f, 60f), T0)!.Front.GoalZ));
        Assert.Null(WaveMarch.Of("raid", admin, 1, (0, 0), null, T0));                     // an Admin destination needs the admin's start
    }

    [Fact]
    public void March_empty_a_wave_without_a_destination_has_no_march() =>
        Assert.Null(WaveMarch.Of("raid", Def("\"loot\": false").Action!, 1, (0, 0), null, T0));
}
