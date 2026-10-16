using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D1, D2, D3 and D7: a SpawnWaves action's `pools`, its pool entries, its boss rolls (a wave's own
/// `bosses` and the action-level list) and `pursuit`, each failure disabling the event with one reason that names the
/// field and fits 120 bytes; an event without the keys parses as 0.12.0 did.</summary>
public partial class EventValidationTests
{
    /// <summary>31 catalog units for the pool bounds, beside the default catalog's.</summary>
    static readonly string[] PoolUnits = Enumerable.Range(1, 31).Select(i => $"CHAR_Pool_{i:00}").ToArray();

    static FakeUnits PoolCatalog() =>
        new(new[] { "CHAR_Bandit_Thug", "CHAR_Bandit_Deadeye", "CHAR_Undead_SkeletonSoldier_Base", "CHAR_Bandit_Tourok_VBlood", "CHAR_Pool_20" }
            .Concat(PoolUnits).ToArray()) { Regions = FakeRegions.All() };

    static EventDefinition PoolOne(string action)
    {
        var r = EventValidator.Parse(Json.File(Json.Event("wv", action: action)), PoolCatalog(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        return Assert.Single(r.Set.All);
    }

    static string Members(int n, int from = 1) => "[ " + string.Join(", ", PoolUnits.Skip(from - 1).Take(n).Select(p => "\"" + p + "\"")) + " ]";

    const string PoolPoint = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";

    /// <summary>A units-form action with <paramref name="pools"/> (JSON object text, or null for none) and
    /// <paramref name="units"/>.</summary>
    static string PoolUnitsAction(string? pools, string units = "[ { \"pool\": \"elite\", \"count\": 5 } ]", string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\"" + (pools is null ? "" : ", \"pools\": " + pools) + ", \"units\": " + units +
        ", \"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, \"location\": " + PoolPoint + (extra is null ? "" : ", " + extra) + " }";

    /// <summary>A waveList action of <paramref name="waves"/> (JSON wave objects) with <paramref name="pools"/>.</summary>
    static string PoolWaveAction(string? pools, string waves, string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\"" + (pools is null ? "" : ", \"pools\": " + pools) + ", \"waveList\": [ " + waves +
        " ], \"radius\": 10, \"location\": " + PoolPoint + (extra is null ? "" : ", " + extra) + " }";

    const string Elite = "{ \"elite\": " + "[ \"CHAR_Pool_01\", \"CHAR_Pool_02\", \"CHAR_Pool_03\" ] }";
    const string Bosspool = "{ \"elite\": [ \"CHAR_Pool_01\", \"CHAR_Pool_02\", \"CHAR_Pool_03\" ], " +
                            "\"lords\": [ \"CHAR_Pool_11\", \"CHAR_Pool_12\", \"CHAR_Pool_13\", \"CHAR_Pool_14\", \"CHAR_Pool_15\" ] }";
    // elite and a pool sharing CHAR_Pool_03 with it (Codex wave-variety F1: rolls draw in order, from what is left)
    const string Overlap = "{ \"elite\": [ \"CHAR_Pool_01\", \"CHAR_Pool_02\", \"CHAR_Pool_03\" ], \"mixed\": [ \"CHAR_Pool_03\", \"CHAR_Pool_11\" ] }";
    // three pools sharing CHAR_Pool_01 (Codex wave-variety re-check): two earlier rolls can take it only once
    const string Shared = "{ \"elite\": [ \"CHAR_Pool_01\", \"CHAR_Pool_02\", \"CHAR_Pool_03\" ], \"a\": [ \"CHAR_Pool_01\", \"CHAR_Pool_11\" ], " +
                          "\"b\": [ \"CHAR_Pool_01\", \"CHAR_Pool_12\" ], \"c\": [ \"CHAR_Pool_01\", \"CHAR_Pool_13\", \"CHAR_Pool_14\" ] }";

    static string PWave(string units = "[ { \"pool\": \"elite\", \"count\": 2 } ]", string? keys = null) =>
        "{ \"units\": " + units + (keys is null ? "" : ", " + keys) + " }";

    static void Refused(EventDefinition d, string reason, string? names = null)
    {
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
        Assert.True(Encoding.UTF8.GetByteCount(d.DisabledReason!) <= Wire.ReasonBytes, d.DisabledReason);
        if (names is not null) Assert.Contains(names, d.DisabledReason, StringComparison.Ordinal);
    }

    static EventDefinition Loads(string action)
    {
        var d = PoolOne(action);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    // ================= D1 Pools =================

    public static IEnumerable<object[]> PoolRefusals() =>
    [
        [PoolUnitsAction("[ \"CHAR_Pool_01\" ]"), EventValidator.PoolsObject, "action.pools"],
        [PoolUnitsAction("{ }"), EventValidator.PoolsCountRule, "action.pools"],
        [PoolUnitsAction("{ " + string.Join(", ", Enumerable.Range(1, 9).Select(i => $"\"p{i}\": [ \"CHAR_Pool_{i:00}\" ]")) + " }",
            "[ " + string.Join(", ", Enumerable.Range(1, 9).Select(i => $"{{ \"pool\": \"p{i}\", \"count\": 1 }}").Take(9)) + " ]"),
            EventValidator.PoolsCountRule, "action.pools"],
        [PoolUnitsAction("{ \"Elite\": [ \"CHAR_Pool_01\" ] }"), EventValidator.PoolNameRule("Elite"), "Elite"],
        [PoolUnitsAction("{ \"elite_1\": [ \"CHAR_Pool_01\" ] }"), EventValidator.PoolNameRule("elite_1"), "elite_1"],
        [PoolUnitsAction("{ \"" + new string('a', 25) + "\": [ \"CHAR_Pool_01\" ] }"), EventValidator.PoolNameRule(new string('a', 25)), "aaaa"],
        [PoolUnitsAction("{ \"\": [ \"CHAR_Pool_01\" ] }"), EventValidator.PoolNameRule(""), "action.pools"],
        [PoolUnitsAction("{ \"elite\": [ ] }"), EventValidator.PoolMembersRule("elite"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": " + Members(31) + " }"), EventValidator.PoolMembersRule("elite"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": [ \"CHAR_Pool_01\", \"CHAR_Pool_01\" ] }"), EventValidator.PoolMembersRule("elite"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": \"CHAR_Pool_01\" }"), EventValidator.PoolMembersRule("elite"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": [ 3 ] }"), EventValidator.PoolMembersRule("elite"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": [ \"CHAR_Pool_01\", \"CHAR_Nobody\" ] }"), EventValidator.PoolUnknownUnit("elite", "CHAR_Nobody"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": [ \"Pool_01\" ] }"), EventValidator.PoolUnknownUnit("elite", "Pool_01"), "action.pools.elite"],
        [PoolUnitsAction("{ \"elite\": [ \"CHAR_Pool_01\" ], \"spare\": [ \"CHAR_Pool_02\" ] }"), EventValidator.PoolUnused("spare"), "action.pools.spare"],
        [PoolUnitsAction("{ \"elite\": [ \"CHAR_Pool_01\" ] }", "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ]"),
            EventValidator.PoolUnused("elite"), "action.pools.elite"],
    ];

    [Theory]
    [MemberData(nameof(PoolRefusals))]
    public void Pools_fails_when_a_key_is_out_of_range(string action, string reason, string names) => Refused(PoolOne(action), reason, names);

    [Fact]
    public void Pools_fails_when_a_bound_is_off_by_one()
    {
        var eight = Loads(PoolUnitsAction("{ " + string.Join(", ", Enumerable.Range(1, 8).Select(i => $"\"p{i}\": [ \"CHAR_Pool_{i:00}\" ]")) + " }",
            "[ " + string.Join(", ", Enumerable.Range(1, 8).Select(i => $"{{ \"pool\": \"p{i}\", \"count\": 1 }}")) + " ]"));
        Assert.Equal(8, eight.Action!.Pools!.Count);
        var thirty = Loads(PoolUnitsAction("{ \"elite\": " + Members(30) + " }"));
        Assert.Equal(PoolUnits.Take(30), thirty.Action!.Pools!["elite"]);                              // kept in the file's order
        var one = Loads(PoolUnitsAction("{ \"e\": [ \"CHAR_Pool_01\" ] }", "[ { \"pool\": \"e\", \"count\": 1 } ]"));
        Assert.Equal(["CHAR_Pool_01"], one.Action!.Pools!["e"]);
        var name = new string('a', 22) + "-9";                                                         // 24 characters
        Loads(PoolUnitsAction("{ \"" + name + "\": [ \"CHAR_Pool_01\" ] }", "[ { \"pool\": \"" + name + "\", \"count\": 1 } ]"));
    }

    [Fact]
    public void Pools_fails_when_a_reason_exceeds_120_bytes_or_drops_the_pool()
    {
        var longest = new string('z', 24);
        var prefab = "CHAR_" + new string('Q', 120);
        foreach (var reason in EventValidator.WaveVarietyReasons
                     .Concat([EventValidator.PoolNameRule(new string('x', 200)), EventValidator.PoolMembersRule(longest),
                         EventValidator.PoolUnknownUnit(longest, prefab), EventValidator.PoolDeniedUnit(longest, prefab), EventValidator.PoolUnused(longest),
                         EventValidator.NoPool("action.waveList.10.units.10", longest), EventValidator.PrefabOrPool("action.waveList.10.units.10"),
                         EventValidator.WaveSizeRule("action.waveList.10.units"), EventValidator.BossesMost("action.waveList.10.bosses"),
                         EventValidator.RollMinRule("action.waveList.10.bosses.3"), EventValidator.RollMaxRule("action.waveList.10.bosses.3"),
                         EventValidator.RollPastPool("action.waveList.10.bosses.3", longest), EventValidator.BossesBothLast(10),
                         EventValidator.WaveBossesFanOut(10)]))
            Assert.True(Encoding.UTF8.GetByteCount(reason) <= Wire.ReasonBytes, reason);
        Assert.Contains("action.pools." + longest, EventValidator.PoolUnknownUnit(longest, prefab), StringComparison.Ordinal);   // the value drops, never the pool
        Assert.Contains("action.pools." + longest, EventValidator.PoolDeniedUnit(longest, prefab), StringComparison.Ordinal);
    }

    [Fact]
    public void Pools_fails_when_a_deny_listed_member_loads()
    {
        var catalog = PoolCatalog();
        catalog.Denied.Add("CHAR_Pool_02");
        var r = EventValidator.Parse(Json.File(Json.Event("wv", action: PoolUnitsAction(Elite))), catalog, regions: FakeRegions.All());
        Refused(Assert.Single(r.Set.All), EventValidator.PoolDeniedUnit("elite", "CHAR_Pool_02"), "action.pools.elite");
    }

    [Fact]
    public void Pools_fails_when_an_event_without_the_key_parses_differently()
    {
        foreach (var d in new[] { Json.One(Json.Event("raid")), PoolOne(Json.ValidAction), PoolOne(PoolWaveAction(null, PWave("[ " + Thug + " ]"))) })
        {
            Assert.True(d.Startable, d.DisabledReason);
            Assert.Null(d.Action!.Pools);
            Assert.Null(d.Action.Pursuit);
            Assert.All(d.Action.Units, u => Assert.Null(u.Pool));
            Assert.All(d.Action.WaveList ?? [], w => Assert.Null(w.Bosses));
            for (var n = 1; n <= d.Action.Waves; n++) Assert.False(Pools.Rolls(d.Action, n));
        }
        // the 0.12.0 refusals of an entry without a prefab, on an action without pools, are unchanged
        Refused(PoolOne(PoolUnitsAction(null, "[ { \"count\": 5 } ]")), "action.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }");
        Refused(PoolOne(PoolUnitsAction(null, "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5, \"elite\": 1 } ]")), "unknown field action.units.elite");
    }

    [Fact]
    public void Pools_passes_the_file_order_and_the_entries()
    {
        var d = Loads(PoolWaveAction(Bosspool, PWave("[ { \"pool\": \"elite\", \"count\": 4, \"chance\": 0.5, \"modifiers\": { \"level\": 30 } }, " + Thug + " ]",
            "\"bosses\": [ { \"pool\": \"lords\", \"min\": 1, \"max\": 2 } ]")));
        Assert.Equal(["elite", "lords"], d.Action!.Pools!.Keys);
        var e = d.Action.WaveList![0].Units[0];
        Assert.Equal(("elite", "", 4, 0.5, 30), (e.Pool, e.Prefab, e.Count, e.Chance, e.Modifiers!.Level));
        Assert.Equal("lords", Assert.Single(d.Action.WaveList[0].Bosses!).Pool);
    }

    [Fact]
    public void Pools_empty_no_pools_key()
    {
        var d = PoolOne(PoolUnitsAction(null, "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ]"));
        Assert.True(d.Startable);
        Assert.Null(d.Action!.Pools);
    }

    // ================= D2 PoolEntries =================

    public static IEnumerable<object[]> PoolEntryRefusals() =>
    [
        [PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ]"), EventValidator.PrefabOrPool("action.units")],
        [PoolUnitsAction(Elite, "[ { \"count\": 5 } ]"), EventValidator.PrefabOrPool("action.units")],
        [PoolUnitsAction(Elite, "[ { \"pool\": \"ghost\", \"count\": 5 } ]"), EventValidator.NoPool("action.units", "ghost")],
        [PoolUnitsAction(null, "[ { \"pool\": \"elite\", \"count\": 5 } ]"), EventValidator.NoPool("action.units", "elite")],
        [PoolUnitsAction(Elite, "[ { \"pool\": 3, \"count\": 5 } ]"), EventValidator.NoPool("action.units", null)],
        [PoolWaveAction(Elite, PWave("[ { \"pool\": \"elite\", \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ]")),
            EventValidator.PrefabOrPool("action.waveList.1.units.1")],
        [PoolWaveAction(Elite, PWave() + ", " + PWave("[ " + Thug + ", { \"pool\": \"ghost\", \"count\": 2 } ]", "\"whenCleared\": true")),
            EventValidator.NoPool("action.waveList.2.units.2", "ghost")],
        [PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 30 }, { \"pool\": \"elite\", \"count\": 21 } ]"), EventValidator.WaveSizeRule("action.units")],
        [PoolWaveAction(Elite, PWave("[ { \"pool\": \"elite\", \"count\": 50 }, " + Thug.Replace("2 }", "1 }") + " ]")),
            EventValidator.WaveSizeRule("action.waveList.1.units")],
    ];

    [Theory]
    [MemberData(nameof(PoolEntryRefusals))]
    public void PoolEntries_fails_when_an_entry_breaks_the_rule(string action, string reason) => Refused(PoolOne(action), reason);

    [Theory]
    [InlineData("\"count\": 0", "count")]
    [InlineData("\"count\": 51", "count")]
    [InlineData("\"count\": 5, \"chance\": 0.04", "chance")]
    [InlineData("\"count\": 5, \"chance\": 1.01", "chance")]
    [InlineData("\"count\": 5, \"modifiers\": { \"level\": 121 }", "modifiers")]
    [InlineData("\"count\": 5, \"modifiers\": { \"power\": 1.234 }", "modifiers")]
    [InlineData("\"count\": 5, \"modifiers\": { }", "modifiers")]
    public void PoolEntries_fails_when_a_bound_differs_from_a_prefab_entry(string keys, string field)
    {
        // the same keys on a prefab entry and on a pool entry give the same reason, in the units form (no modifiers there)
        // and in a wave
        if (field != "modifiers")
        {
            var prefab = PoolOne(PoolUnitsAction(Elite, "[ { \"prefab\": \"CHAR_Bandit_Thug\", " + keys + " }, { \"pool\": \"elite\", \"count\": 1 } ]"));
            var pool = PoolOne(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", " + keys + " } ]"));
            Assert.False(pool.Startable);
            Assert.Equal(prefab.DisabledReason, pool.DisabledReason);
        }
        var wPrefab = PoolOne(PoolWaveAction(Elite, PWave("[ { \"prefab\": \"CHAR_Bandit_Thug\", " + keys + " }, { \"pool\": \"elite\", \"count\": 1 } ]")));
        var wPool = PoolOne(PoolWaveAction(Elite, PWave("[ { \"pool\": \"elite\", " + keys + " }, { \"pool\": \"elite\", \"count\": 1 } ]")));
        Assert.False(wPool.Startable);
        Assert.Equal(wPrefab.DisabledReason, wPool.DisabledReason);
        Assert.Contains(field, wPool.DisabledReason, StringComparison.Ordinal);
    }

    [Fact]
    public void PoolEntries_fails_when_growth_skips_a_pool_entry()
    {
        var d = Loads(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 4 }, { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ]",
            "\"growth\": { \"addUnits\": 3 }"));
        var list = d.Action!.WaveList!;
        Assert.Equal([(("elite"), 4), ("elite", 7), ("elite", 10)], list.Select(w => (w.Units[0].Pool!, w.Units[0].Count)));
        Assert.Equal([2, 5, 8], list.Select(w => w.Units[1].Count));
        Assert.All(list, w => Assert.Null(w.Units[1].Pool));
    }

    [Fact]
    public void PoolEntries_fails_when_a_51_unit_wave_of_pool_entries_loads()
    {
        Loads(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 30 }, { \"pool\": \"elite\", \"count\": 20 } ]"));        // 50 loads
        Refused(PoolOne(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 30 }, { \"pool\": \"elite\", \"count\": 21 } ]")),
            EventValidator.WaveSizeRule("action.units"));
        // a wave of prefab entries only keeps 0.12.0's rules (each entry 1-50)
        Loads(PoolUnitsAction(null, "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 50 }, { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 50 } ]"));
    }

    [Fact]
    public void PoolEntries_passes_a_pool_entry_in_both_forms()
    {
        var u = Loads(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 5, \"chance\": 0.5 } ]")).Action!.Units.Single();
        Assert.Equal(("elite", 5, 0.5), (u.Pool, u.Count, u.Chance));
        var w = Loads(PoolWaveAction(Elite, PWave())).Action!.WaveList![0].Units.Single();
        Assert.Equal(("elite", 2), (w.Pool, w.Count));
    }

    [Fact]
    public void PoolEntries_empty_no_pool_key_reads_as_a_prefab_entry()
    {
        var u = Loads(PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 1 }, { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ]")).Action!.Units[1];
        Assert.Equal(("CHAR_Bandit_Thug", (string?)null, 4), (u.Prefab, u.Pool, u.Count));
    }

    // ================= D3 BossRolls =================

    static string Roll(string pool, int min, int max, string? more = null) =>
        "{ \"pool\": \"" + pool + "\", \"min\": " + min + ", \"max\": " + max + (more is null ? "" : ", " + more) + " }";

    static string Fixed(string prefab, int count) => "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + " }";

    static string BossWaves(string bosses, string? lastKeys = null, string? extra = null, string? pools = Bosspool) =>
        PoolWaveAction(pools, PWave("[ " + Thug + ", { \"pool\": \"elite\", \"count\": 1 } ]", "\"bosses\": [ " + bosses + " ]") + ", " +
            PWave("[ " + Thug + " ]", "\"whenCleared\": true" + (lastKeys is null ? "" : ", " + lastKeys)), extra);

    public static IEnumerable<object[]> BossRollRefusals() =>
    [
        [BossWaves(Roll("lords", 0, 1)), EventValidator.RollMinRule("action.waveList.1.bosses.1")],
        [BossWaves(Roll("lords", 6, 6)), EventValidator.RollMinRule("action.waveList.1.bosses.1")],
        [BossWaves(Roll("lords", 3, 2)), EventValidator.RollMaxRule("action.waveList.1.bosses.1")],
        [BossWaves(Roll("lords", 1, 6)), EventValidator.RollMaxRule("action.waveList.1.bosses.1")],
        [BossWaves("{ \"pool\": \"lords\", \"max\": 2 }"), EventValidator.RollMinRule("action.waveList.1.bosses.1")],
        [BossWaves("{ \"pool\": \"lords\", \"min\": 1 }"), EventValidator.RollMaxRule("action.waveList.1.bosses.1")],
        [BossWaves(Roll("ghost", 1, 1)), EventValidator.NoPool("action.waveList.1.bosses.1", "ghost")],
        [BossWaves(Roll("lords", 1, 1, "\"count\": 1")), "unknown field action.waveList.1.bosses.count"],
        [BossWaves(Roll("lords", 1, 1, "\"prefab\": \"CHAR_Pool_11\"")), EventValidator.PrefabOrPool("action.waveList.1.bosses.1")],
        [BossWaves(Fixed("CHAR_Pool_11", 0)), EventValidator.BossCountRuleAt("action.waveList.1.bosses")],
        [BossWaves(Fixed("CHAR_Pool_11", 4)), EventValidator.BossCountRuleAt("action.waveList.1.bosses")],
        [BossWaves(string.Join(", ", Enumerable.Repeat(Fixed("CHAR_Pool_11", 1), 4))), EventValidator.BossesRuleAt("action.waveList.1.bosses")],
        [BossWaves(Fixed("CHAR_Nobody", 1)), "unknown unit CHAR_Nobody"],
        [BossWaves(Fixed("CHAR_Pool_11", 3) + ", " + Roll("lords", 1, 3)), EventValidator.BossesMost("action.waveList.1.bosses")],
        [BossWaves(Fixed("CHAR_Pool_11", 3) + ", " + Fixed("CHAR_Pool_12", 3)), EventValidator.BossesMost("action.waveList.1.bosses")],
        [BossWaves(Roll("elite", 1, 4)), EventValidator.RollPastPool("action.waveList.1.bosses.1", "elite")],
        [BossWaves(Fixed("CHAR_Pool_01", 1) + ", " + Roll("elite", 1, 3)), EventValidator.RollPastPool("action.waveList.1.bosses.2", "elite")],
        [BossWaves(Roll("elite", 2, 2) + ", " + Roll("elite", 2, 2), pools: Elite), EventValidator.RollPastPool("action.waveList.1.bosses.2", "elite")],
        [BossWaves(Roll("elite", 1, 3) + ", " + Roll("mixed", 1, 2), pools: Overlap), EventValidator.RollPastPool("action.waveList.1.bosses.2", "mixed")],
        [BossWaves(Roll("a", 1, 1) + ", " + Roll("b", 1, 1) + ", " + Roll("c", 3, 3), pools: Shared), EventValidator.RollPastPool("action.waveList.1.bosses.3", "c")],
        [BossWaves(Roll("elite", 1, 1), "\"bosses\": [ " + Fixed("CHAR_Pool_11", 1) + " ]", "\"bosses\": [ " + Fixed("CHAR_Bandit_Tourok_VBlood", 1) + " ]"),
            EventValidator.BossesBothLast(2)],
        [PoolUnitsAction(Bosspool, "[ { \"pool\": \"elite\", \"count\": 2 } ]", "\"bosses\": [ " + Fixed("CHAR_Pool_01", 3) + ", " + Roll("lords", 1, 3) + " ]"),
            EventValidator.BossesMost("action.bosses")],
        [PoolUnitsAction(Bosspool, "[ { \"pool\": \"elite\", \"count\": 2 } ]", "\"bosses\": [ " + Roll("lords", 1, 6) + " ]"),
            EventValidator.RollMaxRule("action.bosses.1")],
        [PoolUnitsAction(Elite, "[ { \"pool\": \"elite\", \"count\": 2 } ]", "\"bosses\": [ " + Roll("elite", 1, 4) + " ]"),
            EventValidator.RollPastPool("action.bosses.1", "elite")],
    ];

    [Theory]
    [MemberData(nameof(BossRollRefusals))]
    public void BossRolls_fails_when_a_key_is_out_of_range(string action, string reason) => Refused(PoolOne(action), reason);

    [Fact]
    public void BossRolls_fails_when_a_bound_is_off_by_one()
    {
        var d = Loads(BossWaves(Roll("lords", 1, 5)));
        var roll = Assert.Single(d.Action!.WaveList![0].Bosses!);
        Assert.Equal(("lords", 1, 5, true), (roll.Pool, roll.Min, roll.Max, roll.IsRoll));
        Loads(BossWaves(Roll("lords", 5, 5)));
        Loads(BossWaves(Fixed("CHAR_Pool_11", 3) + ", " + Roll("lords", 1, 2)));                     // 5 bosses in all
        Loads(BossWaves(Roll("elite", 1, 3), pools: Elite));                                                        // max = the pool's size
        Loads(BossWaves(Fixed("CHAR_Pool_01", 1) + ", " + Roll("elite", 1, 2), pools: Elite));                       // the size less the fixed one
        Loads(BossWaves(Fixed("CHAR_Pool_11", 1) + ", " + Roll("elite", 1, 3), pools: Elite));                       // a fixed boss of another pool takes nothing
        Loads(BossWaves(Roll("elite", 1, 1), "\"bosses\": [ " + Fixed("CHAR_Pool_11", 1) + ", " + Roll("lords", 1, 4) + " ]"));
        Loads(BossWaves(Roll("elite", 1, 1) + ", " + Roll("elite", 2, 2), pools: Elite));                                         // 1 + 2 = the pool's 3
        Loads(BossWaves(Roll("elite", 1, 3) + ", " + Roll("mixed", 1, 1), pools: Overlap));                          // elite may take CHAR_Pool_03, CHAR_Pool_11 is left
        Loads(BossWaves(Roll("a", 1, 1) + ", " + Roll("b", 1, 1) + ", " + Roll("c", 2, 2), pools: Shared));          // a and b take CHAR_Pool_01 once at most
    }

    [Fact]
    public void BossRolls_fails_when_an_accepted_roll_draws_fewer_than_its_count()
    {
        // Codex wave-variety F1: every list the validator accepts gives each roll its count, whatever the draws
        foreach (var (bosses, pools, want) in new[] { (Roll("elite", 1, 1) + ", " + Roll("elite", 2, 2), Elite, 3),
                     (Roll("elite", 3, 3) + ", " + Roll("mixed", 1, 1), Overlap, 4), (Roll("mixed", 1, 1) + ", " + Roll("elite", 2, 2), Overlap, 3),
                     (Roll("a", 1, 1) + ", " + Roll("b", 1, 1) + ", " + Roll("c", 2, 2), Shared, 4) })
        {
            var d = Loads(BossWaves(bosses, pools: pools));
            for (var seed = 0; seed < 200; seed++)
            {
                var draw = Pools.Resolve(d.Action!, 1, new SystemRandom(new Random(seed)));
                Assert.Equal(want, draw.Bosses.Count);
                Assert.Equal(want, draw.Bosses.Select(b => b.Prefab).Distinct().Count());
            }
        }
    }

    [Fact]
    public void BossRolls_fails_when_a_0_12_0_fixed_list_is_refused_or_parses_differently()
    {
        // revenge D28's action-level list: 3 entries of count 3 (9 bosses) loads; its reasons are unchanged
        var nine = Loads(PoolUnitsAction(null, "[ " + Thug + " ]", "\"bosses\": [ " + Fixed("CHAR_Bandit_Tourok_VBlood", 3) + ", " +
            Fixed("CHAR_Bandit_Deadeye", 3) + ", { \"prefab\": \"CHAR_Pool_20\", \"count\": 3, \"modifiers\": { \"level\": 60 } } ]"));
        Assert.Equal([("CHAR_Bandit_Tourok_VBlood", 3, (int?)null), ("CHAR_Bandit_Deadeye", 3, null), ("CHAR_Pool_20", 3, 60)],
            nine.Action!.Bosses!.Select(b => (b.Prefab, b.Count, b.Modifiers?.Level)));
        Assert.All(nine.Action.Bosses!, b => Assert.False(b.IsRoll));
        Assert.Equal(9, Pools.Resolve(nine.Action, nine.Action.Waves, PoolTests.NoRoll).Lead);
        Refused(PoolOne(PoolUnitsAction(null, "[ " + Thug + " ]", "\"bosses\": [ " + string.Join(", ", Enumerable.Repeat(Fixed("CHAR_Pool_20", 1), 4)) + " ]")),
            EventValidator.BossesRule);
        Refused(PoolOne(PoolUnitsAction(null, "[ " + Thug + " ]", "\"bosses\": [ " + Fixed("CHAR_Pool_20", 4) + " ]")), EventValidator.BossCountRule);
        Refused(PoolOne(PoolUnitsAction(null, "[ " + Thug + " ]", "\"bosses\": [ { \"prefab\": \"CHAR_Pool_20\", \"count\": 1, \"min\": 1 } ]")),
            "unknown field action.bosses.min");
    }

    [Fact]
    public void BossRolls_fails_when_a_roll_loads_on_an_apocalypse()
    {
        var r = EventValidator.Parse(Json.File(Apo.Event(action: Apo.Action(extra: "\"bosses\": [ " + Roll("lords", 1, 2) + " ]"))),
            FakeUnits.Default(), regions: FakeRegions.All());
        Refused(Assert.Single(r.Set.All), EventValidator.BossRollApocalypse, "action.bosses");
        var ok = EventValidator.Parse(Json.File(Apo.Event(action: Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson() + " ]"))),
            FakeUnits.Default(), regions: FakeRegions.All());
        Assert.True(Assert.Single(ok.Set.All).Startable);
    }

    [Fact]
    public void BossRolls_fails_when_a_wave_list_beside_fanOut_loads()
    {
        var fanned = "\"action\": { \"type\": \"SpawnWaves\", \"pools\": " + Bosspool + ", \"waveList\": [ " +
            PWave("[ { \"pool\": \"elite\", \"count\": 2 } ]", "\"bosses\": [ " + Roll("lords", 1, 1) + " ]") + " ], \"radius\": 10, " +
            "\"location\": { \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }, \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 } }";
        Refused(PoolOne(fanned), EventValidator.WaveBossesFanOut(1));
    }

    [Fact]
    public void BossRolls_passes_the_action_level_roll()
    {
        var d = Loads(PoolUnitsAction(Bosspool, "[ { \"pool\": \"elite\", \"count\": 2 } ]",
            "\"bosses\": [ " + Fixed("CHAR_Bandit_Tourok_VBlood", 2) + ", " + Roll("lords", 2, 3, "\"modifiers\": { \"level\": 70 }") + " ]"));
        Assert.Equal([false, true], d.Action!.Bosses!.Select(b => b.IsRoll));
        Assert.Equal(70, d.Action.Bosses![1].Modifiers!.Level);
        Assert.Equal(5, d.Action.Bosses.Sum(b => b.MostBosses));
    }

    [Fact]
    public void BossRolls_empty_no_wave_bosses() => Assert.Null(Loads(BossWaves(Roll("lords", 1, 1))).Action!.WaveList![1].Bosses);

    // ================= D7 Pursuit =================

    const string HuntPoint = "\"behaviour\": { \"type\": \"Hunt\", \"range\": 40 }";

    static string PursuitAction(string pursuit, string extra = HuntPoint, string location = PoolPoint) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ " + Thug + " ], \"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, " +
        "\"location\": " + location + ", " + extra + ", \"pursuit\": " + pursuit + " }";

    public static IEnumerable<object[]> PursuitRefusals() =>
    [
        [PursuitAction("{ \"afterSeconds\": 14 }"), EventValidator.PursuitAfterRule],
        [PursuitAction("{ \"afterSeconds\": 601 }"), EventValidator.PursuitAfterRule],
        [PursuitAction("{ \"afterSeconds\": \"60\" }"), EventValidator.PursuitAfterRule],
        [PursuitAction("{ \"afterSeconds\": 60.5 }"), EventValidator.PursuitAfterRule],
        [PursuitAction("{ }"), EventValidator.PursuitAfterRule],
        [PursuitAction("60"), EventValidator.PursuitObject],
        [PursuitAction("true"), EventValidator.PursuitObject],
        [PursuitAction("{ \"afterSeconds\": 60, \"range\": 50 }"), "unknown field action.pursuit.range"],
        [PursuitAction("{ \"afterSeconds\": 60 }", "\"loot\": true"), EventValidator.PursuitHunt],
        [PursuitAction("{ \"afterSeconds\": 60 }", "\"scope\": [\"FarbaneWoods\"], \"regionLock\": true, \"behaviour\": { \"type\": \"RegionHunt\" }",
            "{ \"type\": \"Region\" }"), EventValidator.PursuitHunt],
        [PursuitAction("{ \"afterSeconds\": 60 }", HuntPoint + ", \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }",
            "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }"), EventValidator.PursuitFanOut],
        [PursuitAction("{ \"afterSeconds\": 60 }", HuntPoint + ", \"scope\": [\"FarbaneWoods\"], \"regionLock\": true"), EventValidator.PursuitRegionLock],
    ];

    [Theory]
    [MemberData(nameof(PursuitRefusals))]
    public void Pursuit_fails_when_a_key_is_out_of_range(string action, string reason) => Refused(PoolOne(action), reason, "action.pursuit");

    [Fact]
    public void Pursuit_fails_when_a_bound_is_off_by_one()
    {
        Assert.Equal(15, Loads(PursuitAction("{ \"afterSeconds\": 15 }")).Action!.Pursuit!.AfterSeconds);
        Assert.Equal(600, Loads(PursuitAction("{ \"afterSeconds\": 600 }")).Action!.Pursuit!.AfterSeconds);
        // beside an arena, a scope and regionLock false it loads
        var d = Loads(PursuitAction("{ \"afterSeconds\": 60 }", HuntPoint + ", \"arena\": { \"radius\": 40 }, \"scope\": [\"FarbaneWoods\"], \"regionLock\": false"));
        Assert.Equal(40, Pursuit.Radius(d.Action!));
        Assert.Equal(40, Pursuit.Radius(Loads(PursuitAction("{ \"afterSeconds\": 60 }")).Action!));   // the Hunt range without an arena
    }

    [Fact]
    public void Pursuit_fails_when_an_event_without_the_key_parses_differently()
    {
        var d = Loads(PursuitAction("{ \"afterSeconds\": 60 }").Replace(", \"pursuit\": { \"afterSeconds\": 60 }", ""));
        Assert.Null(d.Action!.Pursuit);
        Assert.Equal(BehaviourType.Hunt, d.Action.Behaviour!.Type);
    }

    [Fact]
    public void Pursuit_passes_hunt_at_a_point()
    {
        var d = Loads(PursuitAction("{ \"afterSeconds\": 60 }"));
        Assert.Equal(60, d.Action!.Pursuit!.AfterSeconds);
        Assert.Equal(BehaviourType.Hunt, d.Action.Behaviour!.Type);
    }

    [Fact]
    public void Pursuit_empty_no_behaviour_no_pursuit() => Assert.Null(Json.One(Json.Event("raid")).Action!.Pursuit);
}
