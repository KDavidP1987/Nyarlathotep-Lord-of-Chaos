using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D1, D2 and A1: the waveList form of a SpawnWaves action and its scoreboard key, each failure
/// disabling the event with one reason naming the field.</summary>
public partial class EventValidationTests
{
    const string Thug = "{ \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 }";

    static string Wave(string units = "[ " + Thug + " ]", string? keys = null) =>
        "{ \"units\": " + units + (keys is null ? "" : ", " + keys) + " }";

    static string WaveListAction(string waveList, string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": " + waveList + ", \"radius\": 10, " +
        "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }" + (extra is null ? "" : ", " + extra) + " }";

    static string List(params string[] waves) => "[ " + string.Join(", ", waves) + " ]";

    static EventDefinition WaveListEvent(string waveList, string? extra = null) => Json.One(Json.Event(action: WaveListAction(waveList, extra)));

    public static IEnumerable<object[]> WaveListRefusals() =>
    [
        [WaveListAction("[]"), EventValidator.WaveListRule],
        [WaveListAction(List(Enumerable.Repeat(Wave(), 11).ToArray())), EventValidator.WaveListRule],
        [WaveListAction("{ }"), EventValidator.WaveListRule],
        [WaveListAction(List(Wave(), "3")), EventValidator.WaveRule(2)],
        [WaveListAction(List(Wave(keys: "\"afterSeconds\": 60"))), EventValidator.FirstWaveStart],
        [WaveListAction(List(Wave(keys: "\"whenCleared\": true"))), EventValidator.FirstWaveStart],
        [WaveListAction(List(Wave(), Wave(keys: "\"afterSeconds\": 9"))), EventValidator.AfterSecondsRule(2)],
        [WaveListAction(List(Wave(), Wave(keys: "\"afterSeconds\": 3601"))), EventValidator.AfterSecondsRule(2)],
        [WaveListAction(List(Wave(), Wave(keys: "\"whenCleared\": \"yes\""))), EventValidator.WhenClearedRule(2)],
        [WaveListAction(List(Wave(), Wave(keys: "\"whenCleared\": false"))), EventValidator.NoStartRule(2)],          // A1 (a)
        [WaveListAction(List(Wave(), Wave(units: "[]"))), "action.waveList.2.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }"],
        [WaveListAction(List(Wave(units: "[ " + string.Join(", ", Enumerable.Repeat(Thug, 11)) + " ]"))),
            "action.waveList.1.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }"],
        [WaveListAction(List(Wave(units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 51 } ]"))), "action.waveList.1.units.count must be 1-50"],
        [WaveListAction(List(Wave(units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 0 } ]"))), "action.waveList.1.units.count must be 1-50"],
        [WaveListAction(List(Wave(units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"chance\": 0.04 } ]"))),
            "action.waveList.1.units.chance must be a number 0.05-1.0"],
        [WaveListAction(List(Wave(units: "[ { \"prefab\": \"CHAR_Nobody\", \"count\": 1 } ]"))), "unknown unit CHAR_Nobody"],
        [WaveListAction(List(Wave(units: Entry("\"levelDelta\": 6")))), "action.waveList.1.units.1.modifiers.levelDelta must be -5..5"],
        [WaveListAction(List(Wave(units: Entry("\"level\": 121")))), "action.waveList.1.units.1.modifiers.level must be 1-120"],
        [WaveListAction(List(Wave(units: Entry("\"level\": 30, \"levelDelta\": 2")))), "action.waveList.1.units.1.modifiers takes level or levelDelta, not both"],
        [WaveListAction(List(Wave(units: Entry("\"maxHealth\": 0.49")))),
            "action.waveList.1.units.1.modifiers.maxHealth must be a number 0.5-3.0 with at most two decimals"],
        [WaveListAction(List(Wave(units: Entry("\"power\": 1.234")))),
            "action.waveList.1.units.1.modifiers.power must be a number 0.5-3.0 with at most two decimals"],
        [WaveListAction(List(Wave(units: Entry("")))), "action.waveList.1.units.1.modifiers must name at least one modifier"],
        [WaveListAction(List(Wave(keys: "\"delay\": 5"))), "unknown field action.waveList.1.delay"],
        [WaveListAction(List(Wave(), Wave(units: "[ " + Thug + ", { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"elite\": true } ]",
            keys: "\"whenCleared\": true"))), "unknown field action.waveList.2.units.2.elite"],
        [WaveListAction(List(Wave(units: Entry("\"armor\": 2")))), "unknown field action.waveList.1.units.1.modifiers.armor"],
    ];

    static string Entry(string modifiers) => "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"modifiers\": { " + modifiers + " } } ]";

    [Theory]
    [MemberData(nameof(WaveListRefusals))]
    public void WaveList_fails_when_a_key_is_out_of_range(string action, string reason)
    {
        var d = Json.One(Json.Event(action: action));
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(reason) <= 120, reason);
    }

    [Theory]
    [InlineData("\"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1 } ]")]
    [InlineData("\"waves\": 2")]
    [InlineData("\"intervalSeconds\": 60")]
    [InlineData("\"modifiers\": { \"power\": 1.5 }")]
    public void WaveList_fails_when_both_forms_load_together(string unitsFormKey) =>
        Assert.Equal(EventValidator.WaveListReplaces, WaveListEvent(List(Wave()), unitsFormKey).DisabledReason);

    [Fact]
    public void WaveList_passes_the_bounds_and_counts_its_waves()
    {
        var ten = WaveListEvent(List(new[] { Wave() }.Concat(Enumerable.Repeat(Wave(keys: "\"afterSeconds\": 10"), 9)).ToArray()));
        Assert.True(ten.Startable, ten.DisabledReason);
        Assert.Equal(10, ten.Action!.Waves);
        Assert.Equal(10, ten.Action.WaveList!.Count);

        var d = WaveListEvent(List(
            Wave(units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 50, \"chance\": 0.05, \"modifiers\": { \"level\": 120 } }, " +
                        "{ \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"modifiers\": { \"levelDelta\": -5, \"maxHealth\": 3.0, \"power\": 0.5 } } ]"),
            Wave(keys: "\"afterSeconds\": 3600"),
            Wave(keys: "\"whenCleared\": true"),
            Wave(keys: "\"afterSeconds\": 120, \"whenCleared\": false"),
            Wave(keys: "\"afterSeconds\": 60, \"whenCleared\": true"),
            Wave()));                                                                       // neither key: waits for the clear (D40)
        Assert.True(d.Startable, d.DisabledReason);
        var a = d.Action!;
        Assert.Equal(6, a.Waves);
        Assert.Null(a.Modifiers);
        Assert.Equal(0, a.IntervalSeconds);
        Assert.Equal(7, a.Units.Count);                                                     // every wave's entries, flattened
        var w1 = a.WaveList![0].Units;
        Assert.Equal(120, w1[0].Modifiers!.Level);
        Assert.Equal(-5, w1[1].Modifiers!.LevelDelta);
        Assert.Equal(3.0, w1[1].Modifiers!.MaxHealth);
        Assert.Equal("CHAR_Bandit_Thug", w1[1].Prefab);                                     // one prefab, two entries (D3)
        Assert.Null(a.WaveList[0].AfterSeconds);
        Assert.Equal(3600, a.WaveList[1].AfterSeconds);
        Assert.False(a.WaveList[1].WaitsForClear);
        Assert.True(a.WaveList[2].WaitsForClear);
        Assert.False(a.WaveList[3].WaitsForClear);
        Assert.True(a.WaveList[4].WaitsForClear);
        Assert.True(a.WaveList[5].WaitsForClear);
    }

    [Fact]
    public void WaveList_empty_no_waveList_key()
    {
        var a = Json.One(Json.Event()).Action!;
        Assert.Null(a.WaveList);
        Assert.False(a.Scoreboard);
        Assert.All(a.Units, u => Assert.Null(u.Modifiers));
        Assert.Equal("unknown field action.units.modifiers",
            Json.One(Json.Event(action: Action(units: Entry("\"power\": 1.5")))).DisabledReason);     // per-entry modifiers are waveList-only
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"true\"")]
    [InlineData("null")]
    public void ScoreboardKey_fails_when_not_a_boolean(string value)
    {
        Assert.Equal(EventValidator.ScoreboardRule, Json.One(Json.Event(action: Action(extra: "\"scoreboard\": " + value))).DisabledReason);
        Assert.Equal(EventValidator.ScoreboardRule, WaveListEvent(List(Wave()), "\"scoreboard\": " + value).DisabledReason);
    }

    [Fact]
    public void ScoreboardKey_passes_both_forms_and_defaults_off()
    {
        Assert.True(Json.One(Json.Event(action: Action(extra: "\"scoreboard\": true"))).Action!.Scoreboard);
        Assert.True(WaveListEvent(List(Wave()), "\"scoreboard\": true").Action!.Scoreboard);
        Assert.False(WaveListEvent(List(Wave()), "\"scoreboard\": false").Action!.Scoreboard);
    }

    [Fact]
    public void ScoreboardKey_empty_key_absent()
    {
        var absent = Json.One(Json.Event());
        Assert.False(absent.Action!.Scoreboard);

        // D2: an event without the key is never counted.
        var running = new ActiveEvent(new RunningInstance(absent, DateTime.UnixEpoch, DateTime.UnixEpoch.AddMinutes(10)), "manual", null);
        Assert.False(Scoreboard.Counts(running));
        Assert.False(Scoreboard.Wants([running]));
    }
}
