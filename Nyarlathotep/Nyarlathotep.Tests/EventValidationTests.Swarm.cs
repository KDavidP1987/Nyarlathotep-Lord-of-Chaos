using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D1, D2 and D24: the arena, winner and growth keys of a SpawnWaves action, each failure disabling the
/// event with one reason that names the field and fits 120 bytes; an event without them parses as in 0.9.0.</summary>
public partial class EventValidationTests
{
    const string SwarmAdmin = "{ \"type\": \"Admin\" }";

    static string SwarmAround(int min, int max) => "{ \"type\": \"AroundPlayer\", \"minDist\": " + min + ", \"maxDist\": " + max + " }";

    static string ArenaKey(string radius) => "\"arena\": { \"radius\": " + radius + " }";

    static string Hunt(int range) => "\"behaviour\": { \"type\": \"Hunt\", \"range\": " + range + " }";

    static string GrowthKey(string body) => "\"growth\": { " + body + " }";

    static void Refused(string action, string reason)
    {
        var d = Json.One(Json.Event(action: action));
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
        Assert.True(Encoding.UTF8.GetByteCount(reason) <= 120, reason);
    }

    // ---- D1 Arena ----

    public static IEnumerable<object[]> ArenaRefusals() =>
    [
        [Action(extra: ArenaKey("14")), EventValidator.ArenaRadiusRule],
        [Action(extra: ArenaKey("61")), EventValidator.ArenaRadiusRule],
        [Action(extra: ArenaKey("20.5")), EventValidator.ArenaRadiusRule],
        [Action(extra: ArenaKey("\"20\"")), EventValidator.ArenaRadiusRule],
        [Action(extra: "\"arena\": { }"), EventValidator.ArenaRadiusRule],
        [Action(extra: "\"arena\": 30"), EventValidator.ArenaObject],
        [Action(extra: "\"arena\": [ 30 ]"), EventValidator.ArenaObject],
        [Action(extra: "\"arena\": { \"radius\": 30, \"height\": 5 }"), "unknown field action.arena.height"],
        [Action(location: SwarmAround(10, 20), extra: ArenaKey("30")), EventValidator.ArenaLocation],
        [Action(radius: "16", extra: ArenaKey("15")), EventValidator.ArenaSpawnRadius],
        [Action(extra: ArenaKey("20") + ", " + Hunt(21)), EventValidator.ArenaHuntRange],
        [WaveListAction(List(Wave()), ArenaKey("61")), EventValidator.ArenaRadiusRule],
        [WaveListAction(List(Wave()), ArenaKey("20")).Replace("\"radius\": 10,", "\"radius\": 25,"), EventValidator.ArenaSpawnRadius],
    ];

    [Theory]
    [MemberData(nameof(ArenaRefusals))]
    public void Arena_fails_when_a_key_is_out_of_range(string action, string reason) => Refused(action, reason);

    [Fact]
    public void Arena_fails_when_a_reason_exceeds_120_bytes() =>
        Assert.All(EventValidator.SwarmReasons, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));

    [Fact]
    public void Arena_passes_the_bounds_in_both_forms()
    {
        Assert.Equal(15, Json.One(Json.Event(action: Action(radius: "15", extra: ArenaKey("15")))).Action!.Arena!.Radius);
        Assert.Equal(60, Json.One(Json.Event(action: Action(extra: ArenaKey("60") + ", " + Hunt(60)))).Action!.Arena!.Radius);
        Assert.Equal(20, Json.One(Json.Event(action: Action(extra: ArenaKey("20") + ", " + Hunt(20)))).Action!.Arena!.Radius);
        var admin = Json.One(Json.Event(action: Action(location: SwarmAdmin, extra: ArenaKey("40"))));
        Assert.True(admin.Startable, admin.DisabledReason);
        Assert.Equal(new Arena(40), admin.Action!.Arena);
        var list = WaveListEvent(List(Wave()), ArenaKey("25"));
        Assert.True(list.Startable, list.DisabledReason);
        Assert.Equal(new Arena(25), list.Action!.Arena);
    }

    [Fact]
    public void Arena_empty_no_arena_key()
    {
        var d = Json.One(Json.Event());
        Assert.True(d.Startable);
        var a = d.Action!;
        Assert.Null(a.Arena);
        Assert.Equal(WinnerRule.None, a.Winner);
        Assert.Null(a.Growth);
        Assert.Null(a.WaveList);
        Assert.Equal((3, 60, 10, false), (a.Waves, a.IntervalSeconds, a.Radius, a.Scoreboard));
        Assert.Equal([new UnitEntry("CHAR_Bandit_Thug", 5)], a.Units);
        Assert.Equal(a, a with { Arena = null, Winner = WinnerRule.None, Growth = null });   // the record's defaults
    }

    // ---- D2 WinnerKey ----

    public static IEnumerable<object[]> WinnerRefusals() =>
    [
        [Action(extra: "\"scoreboard\": true, \"winner\": \"Kills\""), EventValidator.WinnerValue],
        [Action(extra: "\"scoreboard\": true, \"winner\": \"killdeath\""), EventValidator.WinnerValue],
        [Action(extra: "\"scoreboard\": true, \"winner\": true"), EventValidator.WinnerValue],
        [Action(extra: "\"scoreboard\": true, \"winner\": 1"), EventValidator.WinnerValue],
        [Action(extra: "\"winner\": \"KillDeath\""), EventValidator.WinnerScoreboard],
        [Action(extra: "\"scoreboard\": false, \"winner\": \"KillDeath\""), EventValidator.WinnerScoreboard],
        [WaveListAction(List(Wave()), "\"winner\": \"KillDeath\""), EventValidator.WinnerScoreboard],
    ];

    [Theory]
    [MemberData(nameof(WinnerRefusals))]
    public void WinnerKey_fails_when_another_value_or_no_scoreboard(string action, string reason) => Refused(action, reason);

    [Fact]
    public void WinnerKey_passes_both_forms_with_the_scoreboard()
    {
        Assert.Equal(WinnerRule.KillDeath, Json.One(Json.Event(action: Action(extra: "\"scoreboard\": true, \"winner\": \"KillDeath\""))).Action!.Winner);
        Assert.Equal(WinnerRule.KillDeath, WaveListEvent(List(Wave()), "\"scoreboard\": true, \"winner\": \"KillDeath\"").Action!.Winner);
    }

    [Fact]
    public void WinnerKey_empty_key_absent()
    {
        Assert.Equal(WinnerRule.None, Json.One(Json.Event(action: Action(extra: "\"scoreboard\": true"))).Action!.Winner);
        Assert.Equal(WinnerRule.None, WaveListEvent(List(Wave()), "\"scoreboard\": true").Action!.Winner);
    }

    // ---- D24 Growth ----

    static string SwarmUnits(params int[] counts) =>
        "[ " + string.Join(", ", counts.Select((c, i) => "{ \"prefab\": \"" + (i % 2 == 0 ? "CHAR_Bandit_Thug" : "CHAR_Bandit_Deadeye") + "\", \"count\": " + c + " }")) + " ]";

    public static IEnumerable<object[]> GrowthRefusals() =>
    [
        [WaveListAction(List(Wave(), Wave(keys: "\"afterSeconds\": 60")), GrowthKey("\"addUnits\": 1")), EventValidator.GrowthUnitsForm],
        [Action(waves: "1", extra: GrowthKey("\"addUnits\": 1")), EventValidator.GrowthWaves],
        [Action(extra: GrowthKey("")), EventValidator.GrowthAdds],
        [Action(extra: GrowthKey("\"addUnits\": 0, \"addLevels\": 0, \"whenCleared\": true")), EventValidator.GrowthAdds],
        [Action(extra: GrowthKey("\"addUnits\": 11")), EventValidator.GrowthAddUnitsRule],
        [Action(extra: GrowthKey("\"addUnits\": -1")), EventValidator.GrowthAddUnitsRule],
        [Action(extra: GrowthKey("\"addUnits\": 1.5")), EventValidator.GrowthAddUnitsRule],
        [Action(extra: GrowthKey("\"addLevels\": 6")), EventValidator.GrowthAddLevelsRule],
        [Action(extra: GrowthKey("\"addLevels\": \"2\"")), EventValidator.GrowthAddLevelsRule],
        [Action(extra: GrowthKey("\"addUnits\": 1, \"whenCleared\": \"yes\"")), EventValidator.GrowthWhenClearedRule],
        [Action(units: SwarmUnits(200, 281), waves: "3", extra: GrowthKey("\"addUnits\": 5")), EventValidator.GrowthLastWave],   // 481 + 20 = 501
        [Action(extra: "\"growth\": 2"), EventValidator.GrowthObject],
        [Action(extra: "\"growth\": [ ]"), EventValidator.GrowthObject],
        [Action(extra: GrowthKey("\"addUnits\": 1, \"addWaves\": 2")), "unknown field action.growth.addWaves"],
    ];

    [Theory]
    [MemberData(nameof(GrowthRefusals))]
    public void Growth_fails_when_a_key_is_out_of_range(string action, string reason) => Refused(action, reason);

    [Fact]
    public void Growth_passes_the_bounds_and_keeps_the_base()
    {
        var top = Json.One(Json.Event(action: Action(units: SwarmUnits(200, 280), waves: "3", extra: GrowthKey("\"addUnits\": 5, \"addLevels\": 5"))));
        Assert.True(top.Startable, top.DisabledReason);                                                  // 480 + 20 = 500: the ceiling loads
        Assert.Equal(new Growth(5, 5, false), top.Action!.Growth);
        Assert.Equal(500, top.Action.WaveList![^1].Units.Sum(u => u.Count));
        var ten = Json.One(Json.Event(action: Action(units: SwarmUnits(1), waves: "10", extra: GrowthKey("\"addUnits\": 0, \"addLevels\": 1, \"whenCleared\": true"))));
        Assert.True(ten.Startable, ten.DisabledReason);
        Assert.Equal(new Growth(0, 1, true), ten.Action!.Growth);
        Assert.Equal(10, ten.Action.WaveList!.Count);
        var two = Json.One(Json.Event(action: Action(waves: "2", extra: GrowthKey("\"addUnits\": 10"))));
        Assert.True(two.Startable, two.DisabledReason);
        var a = two.Action!;
        Assert.Equal([new UnitEntry("CHAR_Bandit_Thug", 5)], a.Units);                                  // units, waves, interval: the base
        Assert.Equal((2, 60), (a.Waves, a.IntervalSeconds));
        Assert.Equal([5, 15], a.WaveList!.Select(w => w.Units.Single().Count));
    }

    [Fact]
    public void Growth_empty_no_growth_key()
    {
        var a = Json.One(Json.Event()).Action!;
        Assert.Null(a.Growth);
        Assert.Null(a.WaveList);                                                                       // no generated list
        Assert.Equal(3, a.Waves);
    }
}
