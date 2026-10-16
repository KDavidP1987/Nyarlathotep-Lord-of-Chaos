using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D28: the final-wave `bosses` key of a SpawnWaves action, each failure disabling the event with one
/// reason that names the field and fits 120 bytes; an action without the key parses as in 0.11.0.</summary>
public partial class EventValidationTests
{
    const string Tourok = "CHAR_Bandit_Tourok_VBlood";

    static string BossesKey(string entries) => "\"bosses\": " + entries;

    static string Boss(string prefab = Tourok, string count = "1", string? more = null) =>
        "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + (more is null ? "" : ", " + more) + " }";

    public static IEnumerable<object[]> BossRefusals() =>
    [
        [Action(extra: BossesKey("[ ]")), EventValidator.BossesRule],
        [Action(extra: BossesKey("[ " + string.Join(", ", Enumerable.Repeat(Boss(), 4)) + " ]")), EventValidator.BossesRule],
        [Action(extra: BossesKey(Boss())), EventValidator.BossesRule],                                       // not a list
        [Action(extra: BossesKey("[ 1 ]")), EventValidator.BossesRule],
        [Action(extra: BossesKey("[ { \"count\": 1 } ]")), EventValidator.BossesRule],                       // no prefab
        [Action(extra: BossesKey("[ " + Boss(count: "0") + " ]")), EventValidator.BossCountRule],
        [Action(extra: BossesKey("[ " + Boss(count: "4") + " ]")), EventValidator.BossCountRule],
        [Action(extra: BossesKey("[ " + Boss(count: "1.5") + " ]")), EventValidator.BossCountRule],
        [Action(extra: BossesKey("[ { \"prefab\": \"" + Tourok + "\" } ]")), EventValidator.BossCountRule],
        [Action(extra: BossesKey("[ " + Boss(more: "\"chance\": 0.5") + " ]")), "unknown field action.bosses.chance"],
        [Action(extra: BossesKey("[ " + Boss("CHAR_Nobody_VBlood") + " ]")), "unknown unit CHAR_Nobody_VBlood"],
        [Action(extra: BossesKey("[ " + Boss(more: "\"modifiers\": { \"level\": 0 } ") + " ]")), "action.bosses.1.modifiers.level must be 1-120"],
        [FanOut("{ \"maxInstances\": 2, \"minSpacing\": 100 }, " + BossesKey("[ " + Boss() + " ]")), EventValidator.BossesFanOut],
    ];

    [Theory]
    [MemberData(nameof(BossRefusals))]
    public void Bosses_fails_when_a_key_is_out_of_range(string action, string reason) => Refused(action, reason);

    [Fact]
    public void Bosses_fails_when_a_reason_exceeds_120_bytes() =>
        Assert.All(EventValidator.BossReasons, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));

    [Fact]
    public void Bosses_passes_in_every_form()
    {
        var three = "[ " + Boss(count: "3", more: "\"modifiers\": { \"level\": 60 }") + ", " + Boss("CHAR_Bandit_Deadeye") + ", " + Boss() + " ]";
        var units = Json.One(Json.Event(action: Action(extra: BossesKey(three))));
        Assert.True(units.Startable, units.DisabledReason);
        Assert.Equal([(Tourok, 3), ("CHAR_Bandit_Deadeye", 1), (Tourok, 1)], units.Action!.Bosses!.Select(b => (b.Prefab, b.Count)));
        Assert.Equal(60, units.Action.Bosses![0].Modifiers!.Level);
        Assert.Null(units.Action.Bosses[1].Modifiers);

        var list = WaveListEvent(List(Wave(), Wave(keys: "\"whenCleared\": true")), BossesKey("[ " + Boss() + " ]"));
        Assert.True(list.Startable, list.DisabledReason);
        Assert.Single(list.Action!.Bosses!);

        var grown = Json.One(Json.Event(action: Action(waves: "3", extra: GrowthKey("\"addLevels\": 2") + ", " + BossesKey("[ " + Boss() + " ]"))));
        Assert.True(grown.Startable, grown.DisabledReason);
        Assert.Single(grown.Action!.Bosses!);

        var region = HgLoad(HgAction(RegionLoc, Keys(FarbaneScope, Locked, RegionHuntKey, BossesKey("[ " + Boss() + " ]"))));
        Assert.True(region.Startable, region.DisabledReason);
        Assert.Single(region.Action!.Bosses!);
    }

    [Fact]
    public void Bosses_empty_no_bosses_key()
    {
        var a = Json.One(Json.Event()).Action!;
        Assert.Null(a.Bosses);
        Assert.Equal(a.UnitsOf(a.Waves), Pools.Resolve(a, a.Waves, PoolTests.NoRoll).Units);                                    // the final wave unchanged
    }
}
