using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D1, D2 and D3: the Region location, `regionLock` and the RegionHunt behaviour of a SpawnWaves
/// action, each failure disabling the event with one reason that names the field and fits 120 bytes; a definition without
/// the new keys parses exactly as in 0.10.0.</summary>
public partial class EventValidationTests
{
    const string RegionLoc = "{ \"type\": \"Region\" }";
    const string FarbaneScope = "\"scope\": [\"FarbaneWoods\"]";
    const string Locked = "\"regionLock\": true";
    const string RegionHuntKey = "\"behaviour\": { \"type\": \"RegionHunt\" }";
    const string PointInFarbane = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";

    /// <summary>A SpawnWaves action at <paramref name="location"/> (a Point in FarbaneWoods by default) with
    /// <paramref name="extra"/> keys.</summary>
    static string HgAction(string location = PointInFarbane, string? extra = null) => Action(location: location, extra: extra);

    static string Keys(params string[] keys) => string.Join(", ", keys);

    /// <summary>The event loaded with every region on the map (or <paramref name="regions"/>).</summary>
    static EventDefinition HgLoad(string action, string? trigger = null, IRegionCatalog? regions = null) =>
        Scoped(Json.Event(action: action, trigger: trigger), regions ?? FakeRegions.All());

    static void HgRefused(string action, string reason, IRegionCatalog? regions = null)
    {
        var d = HgLoad(action, regions: regions);
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
        Assert.True(Encoding.UTF8.GetByteCount(reason) <= 120, reason);
    }

    static readonly string[] HgTriggers =
    [
        "{ \"type\": \"Manual\" }",
        "{ \"type\": \"Schedule\", \"days\": [\"Sat\"], \"times\": [\"20:00\"] }",
        "{ \"type\": \"GameTime\", \"phase\": \"night\" }",
        "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }",
        "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }",
        "{ \"type\": \"RegionEntered\", \"playerCooldownMinutes\": 30, \"scope\": [\"FarbaneWoods\"] }",
        "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }",
    ];

    // ---- D1 RegionLocation ----

    public static IEnumerable<object[]> RegionLocationRefusals() =>
    [
        [HgAction(RegionLoc), EventValidator.RegionLocationScope],                                            // no scope
        [HgAction(RegionLoc, "\"scope\": \"Global\""), EventValidator.RegionLocationScope],
        [HgAction("{ \"type\": \"Region\", \"name\": \"FarbaneWoods\" }", FarbaneScope), "unknown field action.location.name"],
        [HgAction("{ \"type\": \"Region\", \"x\": 1 }", FarbaneScope), "unknown field action.location.x"],
        [HgAction(RegionLoc, Keys(FarbaneScope, "\"arena\": { \"radius\": 20 }")), EventValidator.ArenaLocation],
        [HgAction(RegionLoc, Keys(FarbaneScope, "\"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 50 }")), EventValidator.FanOutLocation],
        [HgAction("{ \"type\": \"Zone\" }", FarbaneScope), EventValidator.LocationRule],
    ];

    [Theory]
    [MemberData(nameof(RegionLocationRefusals))]
    public void RegionLocation_fails_when_a_key_is_refused(string action, string reason)
    {
        var d = HgLoad(action);
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
        if (reason != EventValidator.LocationRule) Assert.True(Encoding.UTF8.GetByteCount(reason) <= 120, reason);
        else foreach (var type in new[] { "Point", "Admin", "AroundPlayer", "Region" }) Assert.Contains($"\\\"type\\\": \\\"{type}\\\"".Replace("\\", ""), reason);
    }

    [Fact]
    public void RegionLocation_fails_when_the_region_index_is_unavailable()
    {
        HgRefused(HgAction(RegionLoc, FarbaneScope), "regions unavailable", NoRegions.Instance);
        Assert.Equal("regions unavailable", Json.One(Json.Event(action: HgAction(RegionLoc, FarbaneScope))).DisabledReason);   // no catalog at all
    }

    [Fact]
    public void RegionLocation_fails_when_a_reason_exceeds_120_bytes() =>
        Assert.All(EventValidator.HuntingGroundsReasons.Concat([EventValidator.ArenaLocation, EventValidator.FanOutLocation, "regions unavailable",
            EventValidator.UnknownField("action.location", "abcdefghijklmnopqrstuvwxyz012345")]),
            r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));

    /// <summary>A definition without the new keys loads exactly as before them: no lock, no Region location, no RegionHunt,
    /// and the same action as a parse that never reads them (every 0.10.0 form).</summary>
    [Fact]
    public void RegionLocation_fails_when_a_keyless_definition_parses_differently()
    {
        var forms = new[]
        {
            HgAction(),
            HgAction("{ \"type\": \"Admin\" }"),
            HgAction("{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }", "\"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }"),
            HgAction(extra: Keys(FarbaneScope, "\"arena\": { \"radius\": 30 }", "\"scoreboard\": true", "\"winner\": \"KillDeath\"")),
            WaveListAction(List(Wave())),
        };
        foreach (var form in forms)
        {
            var d = HgLoad(form);
            Assert.True(d.Startable, d.DisabledReason);
            var a = d.Action!;
            Assert.False(a.RegionLock);
            Assert.NotEqual(LocationType.Region, a.Location.Type);
            Assert.NotEqual(BehaviourType.RegionHunt, a.Behaviour?.Type);
            Assert.Equal(a with { RegionLock = false }, a);
        }
        Assert.False(new SpawnWavesAction([], 1, 10, 2, new Location(LocationType.Point, 0, 0), null).RegionLock);   // positional callers: false
    }

    [Fact]
    public void RegionLocation_passes_every_form_and_trigger()
    {
        foreach (var trigger in HgTriggers)
        {
            var d = HgLoad(HgAction(RegionLoc, FarbaneScope), trigger);
            Assert.True(d.Startable, $"{trigger}: {d.DisabledReason}");
            Assert.Equal(LocationType.Region, d.Action!.Location.Type);
            Assert.Equal(["FarbaneWoods"], d.Action.Scope.Regions);
        }
        var list = HgLoad(WaveListAction(List(Wave()), FarbaneScope).Replace("{ \"type\": \"Point\", \"x\": 0, \"z\": 0 }", RegionLoc));
        Assert.True(list.Startable, list.DisabledReason);
        Assert.Equal(LocationType.Region, list.Action!.Location.Type);
        var grown = HgLoad(HgAction(RegionLoc, Keys("\"scope\": [\"FarbaneWoods\", \"CursedForest\"]", "\"growth\": { \"addUnits\": 1 }")));
        Assert.True(grown.Startable, grown.DisabledReason);
        Assert.Equal(3, grown.Action!.WaveList!.Count);
    }

    [Fact]
    public void RegionLocation_empty_no_scope_index()
    {
        // an empty catalog (no region on the map) is the index unavailable: the Region event waits, a Global one runs
        var empty = new FakeRegions();
        HgRefused(HgAction(RegionLoc, FarbaneScope), "regions unavailable", empty);
        Assert.True(HgLoad(HgAction(), regions: empty).Startable);
    }

    // ---- D2 RegionLock ----

    public static IEnumerable<object[]> RegionLockRefusals() =>
    [
        [HgAction(extra: Keys(FarbaneScope, "\"regionLock\": \"true\"")), EventValidator.RegionLockRule],
        [HgAction(extra: Keys(FarbaneScope, "\"regionLock\": 1")), EventValidator.RegionLockRule],
        [HgAction(extra: Keys(FarbaneScope, "\"regionLock\": null")), EventValidator.RegionLockRule],
        [HgAction(extra: Locked), EventValidator.RegionLockScope],
        [HgAction(extra: Keys("\"scope\": \"Global\"", Locked)), EventValidator.RegionLockScope],
        [HgAction("{ \"type\": \"Admin\" }", Locked), EventValidator.RegionLockScope],
        [HgAction(extra: Keys(FarbaneScope, Locked, "\"arena\": { \"radius\": 30 }")), EventValidator.RegionLockArena],
        [HgAction("{ \"type\": \"Admin\" }", Keys(FarbaneScope, Locked, "\"arena\": { \"radius\": 30 }")), EventValidator.RegionLockArena],
    ];

    [Theory]
    [MemberData(nameof(RegionLockRefusals))]
    public void RegionLock_fails_when_a_value_or_place_is_refused(string action, string reason) => HgRefused(action, reason);

    [Fact]
    public void RegionLock_fails_when_the_region_index_is_unavailable() =>
        HgRefused(HgAction(extra: Keys(FarbaneScope, Locked)), "regions unavailable", NoRegions.Instance);

    [Fact]
    public void RegionLock_fails_when_false_parses_differently_from_absent()
    {
        var absent = HgLoad(HgAction(extra: FarbaneScope));
        var off = HgLoad(HgAction(extra: Keys(FarbaneScope, "\"regionLock\": false")));
        Assert.True(off.Startable, off.DisabledReason);
        Assert.Equal(absent.Action!.Units, off.Action!.Units);
        Assert.Equal(absent.Action.Scope.Regions, off.Action.Scope.Regions);
        Assert.Equal(absent.Action with { Units = off.Action.Units, Scope = off.Action.Scope }, off.Action);
        var globalOff = HgLoad(HgAction(extra: "\"regionLock\": false"));                      // false needs no scope
        Assert.True(globalOff.Startable, globalOff.DisabledReason);
        Assert.False(globalOff.Action!.RegionLock);
    }

    [Fact]
    public void RegionLock_passes_on_all_four_locations()
    {
        var locations = new[]
        {
            PointInFarbane, "{ \"type\": \"Admin\" }", "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 30 }", RegionLoc,
        };
        foreach (var location in locations)
        {
            var d = HgLoad(HgAction(location, Keys(FarbaneScope, Locked)));
            Assert.True(d.Startable, $"{location}: {d.DisabledReason}");
            Assert.True(d.Action!.RegionLock);
        }
    }

    [Fact]
    public void RegionLock_empty_key_absent()
    {
        var d = HgLoad(HgAction(extra: FarbaneScope));
        Assert.True(d.Startable);
        Assert.False(d.Action!.RegionLock);
    }

    // ---- D3 RegionHunt ----

    public static IEnumerable<object[]> RegionHuntRefusals() =>
    [
        [HgAction(RegionLoc, Keys(FarbaneScope, RegionHuntKey)), EventValidator.RegionHuntLock],
        [HgAction(extra: Keys(FarbaneScope, RegionHuntKey, "\"regionLock\": false")), EventValidator.RegionHuntLock],
        [HgAction(extra: Keys(FarbaneScope, Locked, "\"behaviour\": { \"type\": \"RegionHunt\", \"range\": 30 }")), "unknown field action.behaviour.range"],
        [HgAction(extra: Keys(FarbaneScope, Locked, "\"behaviour\": { \"type\": \"RegionHunt\", \"speed\": 2 }")), "unknown field action.behaviour.speed"],
        [HgAction(extra: Keys(FarbaneScope, Locked, "\"behaviour\": { \"type\": \"regionhunt\" }")), "unknown behaviour type regionhunt"],
        [HgAction(extra: Keys(FarbaneScope, Locked, "\"behaviour\": { \"type\": \"Roam\" }")), "unknown behaviour type Roam"],
    ];

    [Theory]
    [MemberData(nameof(RegionHuntRefusals))]
    public void RegionHunt_fails_when_a_key_is_refused(string action, string reason) => HgRefused(action, reason);

    [Fact]
    public void RegionHunt_fails_when_hunt_changes_or_an_unknown_type_loads()
    {
        var hunt = HgLoad(HgAction(extra: "\"behaviour\": { \"type\": \"Hunt\", \"range\": 40 }"));
        Assert.True(hunt.Startable, hunt.DisabledReason);
        Assert.Equal(new Behaviour(BehaviourType.Hunt, 40), hunt.Action!.Behaviour);
        Assert.Equal(EventValidator.HuntRangeRule, HgLoad(HgAction(extra: "\"behaviour\": { \"type\": \"Hunt\" }")).DisabledReason);
        Assert.Equal(EventValidator.HuntRangeRule, HgLoad(HgAction(extra: "\"behaviour\": { \"type\": \"Hunt\", \"range\": 61 }")).DisabledReason);
        var locked = HgLoad(HgAction(extra: Keys(FarbaneScope, Locked, "\"behaviour\": { \"type\": \"Hunt\", \"range\": 40 }")));
        Assert.True(locked.Startable, locked.DisabledReason);                                       // Hunt under the lock stays Hunt
        Assert.Equal("unknown behaviour type Guard", HgLoad(HgAction(extra: "\"behaviour\": { \"type\": \"Guard\" }")).DisabledReason);
    }

    [Fact]
    public void RegionHunt_passes_under_the_lock()
    {
        foreach (var location in new[] { PointInFarbane, "{ \"type\": \"Admin\" }", "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 70 }", RegionLoc })
        {
            var d = HgLoad(HgAction(location, Keys(FarbaneScope, Locked, RegionHuntKey)));
            Assert.True(d.Startable, $"{location}: {d.DisabledReason}");
            Assert.Equal(new Behaviour(BehaviourType.RegionHunt, 0), d.Action!.Behaviour);
        }
        Assert.All(EventValidator.HuntingGroundsReasons, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= 120, r));
    }

    [Fact]
    public void RegionHunt_empty_no_behaviour()
    {
        var d = HgLoad(HgAction(RegionLoc, Keys(FarbaneScope, Locked)));
        Assert.True(d.Startable, d.DisabledReason);
        Assert.Null(d.Action!.Behaviour);
    }
}
