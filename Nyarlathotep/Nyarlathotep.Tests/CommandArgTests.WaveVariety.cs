using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D10: `.nyar event set` of `action.pursuit`, `action.pools.&lt;name&gt;` (a list, +prefab, -prefab or
/// none), `action.bosses` and `action.waveList.&lt;n&gt;.bosses` with rolls, and the `pool:&lt;name&gt;:&lt;count&gt;` token in the
/// units fields: each takes exactly what the validator takes and refuses with its reason naming the field; a pool and the
/// entry naming it are set in either order to a valid, enabled definition (deferred Review 3 F1); a 30-member pool is
/// built by appends; every reply fits 480 bytes; the §6 table lists every field.</summary>
public partial class CommandArgTests
{
    /// <summary>31 catalog units of 40-character names: 30 of them would not fit one chat line.</summary>
    static readonly string[] LongUnits = Enumerable.Range(1, 31).Select(i => "CHAR_Long_" + new string('L', 28) + i.ToString("00")).ToArray();

    const string WvHunt = "\"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }";

    /// <summary>raid (units form, Point), hunt (Point, Hunt 30), ws (two waves), fanned (AroundPlayer, Hunt, fanOut), locked
    /// (Point, scope, regionLock, Hunt), apo (an Apocalypse).</summary>
    static Library WvLib()
    {
        var units = TemplateLibraryTests.Units(LongUnits.Concat(["CHAR_Militia_Leader_VBlood"]).ToArray());
        return new Library(Json.File(
            Json.Event("raid"),
            Json.Event("hunt", action: HgJson(HgPoint, WvHunt)),
            Json.Event("ws", action: WaveListJson),
            Json.Event("fanned", action: HgJson("{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }",
                WvHunt + ", \"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }")),
            Json.Event("locked", action: HgJson(HgPoint, WvHunt + ", \"scope\": [\"FarbaneWoods\"], \"regionLock\": true")),
            Apo.Event("apo")), units);
    }

    static EventDefinition WvFind(Library lib, string id) => lib.Catalog.Current.Find(id)!;

    static void WvRefused(Library lib, string id, string field, string value, string reason)
    {
        var hash = lib.Hash;
        var reply = AuthoringTests.Set(lib, id, field, value);
        Assert.StartsWith(reason, reply);
        Assert.Equal(hash, lib.Hash);                                                                   // nothing written
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes, reply);
    }

    static void Valid(Library lib, string id)
    {
        var d = WvFind(lib, id);
        Assert.True(d.Startable, d.DisabledReason);
        Assert.True(d.Enabled);
    }

    // ================= Pools =================

    [Fact]
    public void Pools_fails_when_the_pool_first_order_does_not_reach_a_valid_enabled_definition()
    {
        var lib = WvLib();
        var first = AuthoringTests.Set(lib, "raid", "action.pools.elite", "CHAR_Spider_Forest,CHAR_Spider_Melee");
        Assert.EndsWith("; now disabled: " + EventValidator.PoolUnused("elite"), first);               // the validator's reason, the field named
        Assert.False(WvFind(lib, "raid").Startable);
        AuthoringTests.Set(lib, "raid", "action.units", "pool:elite:5,CHAR_Bandit_Thug:2");
        Valid(lib, "raid");
        var a = WvFind(lib, "raid").Action!;
        Assert.Equal(["CHAR_Spider_Forest", "CHAR_Spider_Melee"], a.Pools!["elite"]);
        Assert.Equal([("elite", 5), (null, 2)], a.Units.Select(u => (u.Pool, u.Count)));

        // a wave's boss roll, pool first
        AuthoringTests.Set(lib, "ws", "action.pools.lords", "CHAR_Spider_Queen_VBlood,CHAR_Undead_BishopOfDeath_VBlood");
        Assert.False(WvFind(lib, "ws").Startable);
        AuthoringTests.Set(lib, "ws", "action.waveList.1.bosses", "pool:lords:1-2");
        Valid(lib, "ws");
        Assert.Equal(("lords", 1, 2), WvFind(lib, "ws").Action!.WaveList![0].Bosses!.Select(b => (b.Pool, b.Min, b.Max)).Single());
    }

    [Fact]
    public void Pools_fails_when_the_entry_first_order_does_not_reach_a_valid_enabled_definition()
    {
        var lib = WvLib();
        var first = AuthoringTests.Set(lib, "raid", "action.units", "pool:elite:5");
        Assert.EndsWith("; now disabled: " + EventValidator.NoPool("action.units", "elite"), first);
        AuthoringTests.Set(lib, "raid", "action.pools.elite", "CHAR_Spider_Forest");
        Valid(lib, "raid");

        AuthoringTests.Set(lib, "ws", "action.waveList.2.units", "pool:guard:3");
        Assert.EndsWith(EventValidator.NoPool("action.waveList.2.units.1", "guard"), WvFind(lib, "ws").DisabledReason!);
        AuthoringTests.Set(lib, "ws", "action.bosses", "CHAR_Spider_Queen_VBlood:1,pool:lords:1-1");
        AuthoringTests.Set(lib, "ws", "action.pools.guard", "CHAR_Militia_Light,CHAR_Militia_Crossbow");
        Assert.False(WvFind(lib, "ws").Startable);                                                      // lords still missing
        AuthoringTests.Set(lib, "ws", "action.pools.lords", "CHAR_Undead_BishopOfDeath_VBlood,CHAR_Militia_Leader_VBlood");
        Valid(lib, "ws");
        var a = WvFind(lib, "ws").Action!;
        Assert.Equal([false, true], a.Bosses!.Select(b => b.IsRoll));
        Assert.Equal("guard", a.WaveList![1].Units[0].Pool);
    }

    [Fact]
    public void Pools_fails_when_a_30_member_pool_cannot_be_built_by_appends()
    {
        var lib = WvLib();
        var line = "CHAR_Spider_Forest," + string.Join(",", LongUnits.Take(30));
        Assert.True(Encoding.UTF8.GetByteCount($".nyar event set raid action.pools.elite {line}") > 512);   // one chat line cannot hold it
        AuthoringTests.Set(lib, "raid", "action.units", "pool:elite:5");
        AuthoringTests.Set(lib, "raid", "action.pools.elite", LongUnits[0]);
        foreach (var u in LongUnits.Skip(1).Take(29))
        {
            var reply = AuthoringTests.Set(lib, "raid", "action.pools.elite", "+" + u);
            Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes, reply);
        }
        Valid(lib, "raid");
        Assert.Equal(LongUnits.Take(30), WvFind(lib, "raid").Action!.Pools!["elite"]);
        WvRefused(lib, "raid", "action.pools.elite", "+" + LongUnits[30], EventValidator.PoolMembersRule("elite"));   // a 31st
        AuthoringTests.Set(lib, "raid", "action.pools.elite", "-" + LongUnits[29]);
        WvRefused(lib, "raid", "action.pools.elite", "+" + LongUnits[0], EventValidator.PoolMembersRule("elite"));    // one already there
        Assert.Equal(29, WvFind(lib, "raid").Action!.Pools!["elite"].Count);
        WvRefused(lib, "raid", "action.pools.elite", "-" + LongUnits[30], "action.pools.elite has no unit " + LongUnits[30]);
        AuthoringTests.Set(lib, "raid", "action.pools.elite", "CHAR_Spider_Forest");
        WvRefused(lib, "raid", "action.pools.elite", "-CHAR_Spider_Forest", EventValidator.PoolMembersRule("elite"));   // never the last
        Valid(lib, "raid");
    }

    [Fact]
    public void Pools_fails_when_a_pool_still_named_is_removed()
    {
        var lib = WvLib();
        AuthoringTests.Set(lib, "ws", "action.pools.elite", "CHAR_Spider_Forest");
        AuthoringTests.Set(lib, "ws", "action.waveList.2.units", "CHAR_Bandit_Deadeye:2,pool:elite:3");
        Valid(lib, "ws");
        WvRefused(lib, "ws", "action.pools.elite", "none", EventValidator.NoPool("action.waveList.2.units.2", "elite"));
        AuthoringTests.Set(lib, "ws", "action.waveList.2.units", "CHAR_Bandit_Deadeye:2");
        Assert.Equal(EventValidator.PoolUnused("elite"), WvFind(lib, "ws").DisabledReason);
        AuthoringTests.Set(lib, "ws", "action.pools.elite", "none");
        Valid(lib, "ws");
        Assert.Null(WaveAction(lib, "ws")["pools"]);                                                      // the last pool takes the key with it
    }

    public static IEnumerable<object[]> PoolValueRefusals() =>
    [
        ["action.pools.elite", "", CommandArgs.ValueRequired],
        ["action.pools.Elite", "CHAR_Spider_Forest", EventValidator.PoolNameRule("Elite")],
        ["action.pools.e_1", "CHAR_Spider_Forest", EventValidator.PoolNameRule("e_1")],
        ["action.pools." + new string('a', 25), "CHAR_Spider_Forest", EventValidator.PoolNameRule(new string('a', 25))],
        ["action.pools.elite", "CHAR_Spider_Forest,CHAR_Spider_Forest", EventValidator.PoolMembersRule("elite")],
        ["action.pools.elite", string.Join(",", Enumerable.Range(1, 31).Select(i => $"CHAR_P{i}")), EventValidator.PoolMembersRule("elite")],
        ["action.pools.elite", "CHAR_A,,CHAR_B", EventValidator.PoolMembersRule("elite")],
        ["action.pools.elite", "Spider_Forest", CommandArgs.NameRule("action.pools.elite")],
        ["action.pools.elite", "+Spider_Forest", CommandArgs.NameRule("action.pools.elite")],
        ["action.pools.elite", "-", CommandArgs.NameRule("action.pools.elite")],
        ["action.bosses", "pool:lords:0-1", EventValidator.RollMinRule("action.bosses.1")],
        ["action.bosses", "pool:lords:6-6", EventValidator.RollMinRule("action.bosses.1")],
        ["action.bosses", "pool:lords:2-1", EventValidator.RollMaxRule("action.bosses.1")],
        ["action.bosses", "pool:lords:1-6", EventValidator.RollMaxRule("action.bosses.1")],
        ["action.bosses", "pool:lords:1", CommandArgs.BossEntryRule("action.bosses")],
        ["action.bosses", "pool:lords", CommandArgs.BossEntryRule("action.bosses")],
        ["action.bosses", "pool:lords:1-2:3", CommandArgs.BossEntryRule("action.bosses")],
        ["action.bosses", "pool:lords:a-b", CommandArgs.BossEntryRule("action.bosses")],
        ["action.bosses", "pool:lords:1-2-3", CommandArgs.BossEntryRule("action.bosses")],
        ["action.waveList.1.bosses", "CHAR_Spider_Queen_VBlood:1,pool:lords:-2", CommandArgs.BossEntryRule("action.waveList.1.bosses")],
        ["action.units", "pool:elite:40,CHAR_Bandit_Thug:11", EventValidator.WaveSizeRule("action.units")],
        ["action.waveList.1.units", "pool:elite:50,pool:guard:1", EventValidator.WaveSizeRule("action.waveList.1.units")],
        ["action.bosses", "CHAR_Spider_Queen_VBlood:1,pool:Lords:1-1", EventValidator.NoPool("action.bosses.2", "Lords")],
        ["action.bosses", "CHAR_Spider_Queen_VBlood:3,pool:lords:1-3", EventValidator.BossesMost("action.bosses")],
        ["action.waveList.1.bosses", "CHAR_Spider_Queen_VBlood:3,CHAR_Undead_BishopOfDeath_VBlood:3", EventValidator.BossesMost("action.waveList.1.bosses")],
        ["action.waveList.1.bosses", "CHAR_Spider_Queen_VBlood:4", EventValidator.BossCountRuleAt("action.waveList.1.bosses")],
        ["action.waveList.1.bosses", "CHAR_A,CHAR_B,CHAR_C,CHAR_D", EventValidator.BossesRuleAt("action.waveList.1.bosses")],
        ["action.waveList.1.bosses", "Spider_Queen", CommandArgs.NameRule("action.waveList.1.bosses")],
        ["action.units", "pool:Elite:5", EventValidator.NoPool("action.units", "Elite")],
        ["action.waveList.1.units", "pool:e_1:5", EventValidator.NoPool("action.waveList.1.units", "e_1")],
    ];

    [Theory]
    [MemberData(nameof(PoolValueRefusals))]
    public void Pools_fails_when_the_field_accepts_a_value_the_validator_refuses(string field, string value, string reason)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(reason, arg.Error);
        Assert.True(Encoding.UTF8.GetByteCount(reason) <= Wire.ReasonBytes, reason);
        if (reason != CommandArgs.ValueRequired) Assert.Contains(field.Split('.')[1], reason, StringComparison.Ordinal);   // the field named
    }

    [Theory]
    [InlineData("action.units", "pool:elite:51", "CHAR_Bandit_Thug:51")]
    [InlineData("action.units", "pool:elite:0", "CHAR_Bandit_Thug:0")]
    [InlineData("action.units", "pool:elite:x", "CHAR_Bandit_Thug:x")]
    [InlineData("action.waveList.1.units", "pool:elite:51", "CHAR_Bandit_Thug:51")]
    [InlineData("action.waveList.1.units", "pool:elite", "CHAR_Bandit_Thug:")]
    [InlineData("action.units", "pool:elite:2,pool:elite:3", "CHAR_Bandit_Thug:2,CHAR_Bandit_Thug:3")]
    public void Pools_fails_when_a_pool_token_count_differs_from_a_prefab_entrys(string field, string pool, string prefab)
    {
        var p = CommandArgs.SettableValue(field, pool);
        var q = CommandArgs.SettableValue(field, prefab);
        Assert.False(p.Ok);
        Assert.False(q.Ok);
        Assert.Equal(q.Error!.Replace("CHAR_Bandit_Thug", "pool:elite"), p.Error);
    }

    [Fact]
    public void Pools_fails_when_the_field_refuses_a_value_the_validator_takes()
    {
        var lib = WvLib();
        AuthoringTests.Set(lib, "ws", "action.pools.lords", string.Join(",", ["CHAR_Spider_Queen_VBlood", "CHAR_Undead_BishopOfDeath_VBlood",
            "CHAR_Militia_Leader_VBlood", .. LongUnits.Take(2)]));
        AuthoringTests.Set(lib, "ws", "action.waveList.1.bosses", "pool:lords:5-5");
        Valid(lib, "ws");
        AuthoringTests.Set(lib, "ws", "action.waveList.1.bosses", "CHAR_Spider_Queen_VBlood:2,pool:lords:1-3");          // 5 in all, 4 left in the pool
        Valid(lib, "ws");
        AuthoringTests.Set(lib, "ws", "action.waveList.2.units", "pool:lords:50");
        Valid(lib, "ws");
        AuthoringTests.Set(lib, "ws", "action.bosses", "CHAR_Bandit_Thug:3,CHAR_Bandit_Deadeye:3,CHAR_Spider_Forest:3");   // 0.12.0's 9 bosses
        Valid(lib, "ws");
        Assert.Equal(9, WvFind(lib, "ws").Action!.Bosses!.Sum(b => b.Count));
    }

    [Fact]
    public void Pools_fails_when_a_roll_reaches_an_apocalypse_or_both_final_lists_are_set()
    {
        var lib = WvLib();
        WvRefused(lib, "apo", "action.bosses", "pool:lords:1-1", EventValidator.BossRollApocalypse);
        WvRefused(lib, "apo", "action.pools.lords", "CHAR_Spider_Forest", "action.pools.lords is not an Apocalypse field");
        AuthoringTests.Set(lib, "ws", "action.waveList.2.bosses", "CHAR_Spider_Queen_VBlood:1");
        Valid(lib, "ws");
        WvRefused(lib, "ws", "action.bosses", "CHAR_Undead_BishopOfDeath_VBlood:1", EventValidator.BossesBothLast(2));
        AuthoringTests.Set(lib, "ws", "action.waveList.2.bosses", "none");
        AuthoringTests.Set(lib, "ws", "action.bosses", "CHAR_Undead_BishopOfDeath_VBlood:1");
        WvRefused(lib, "ws", "action.waveList.2.bosses", "CHAR_Spider_Queen_VBlood:1", EventValidator.BossesBothLast(2));
        Valid(lib, "ws");
    }

    [Fact]
    public void Pools_passes_replies_within_480_bytes_and_the_table()
    {
        var lib = WvLib();
        var replies = new[]
        {
            AuthoringTests.Set(lib, "raid", "action.pools.elite", string.Join(",", LongUnits.Take(10))),
            AuthoringTests.Set(lib, "raid", "action.units", "pool:elite:50"),
            AuthoringTests.Set(lib, "raid", "action.bosses", "pool:elite:1-5"),
            AuthoringTests.Set(lib, "raid", "action.pools." + new string('p', 24), "+" + LongUnits[30]),
        };
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(r)} bytes: {r}"));
        // the api twin's value is the stored list (the wire cuts it to 120 bytes, D11), never the "<n> units" echo
        var members = string.Join(",", LongUnits.Skip(10).Take(10));
        var edit = lib.Editor.Edit("raid", "action.pools.elite", CommandArgs.SettableValue("action.pools.elite", members).Value!, lib.Units);
        Assert.True(edit.Ok, edit.Human);
        Assert.EndsWith("= 10 units", edit.Human.Split(';')[0]);
        Assert.Equal(members, edit.Fields.Single(f => f.Key == "value").Value);
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        foreach (var field in new[] { CommandArgs.PursuitField, CommandArgs.PoolsField, "action.waveList.N.bosses" })
        {
            Assert.Equal(("spawn action", "admin"), CommandArgs.SettableFields[field]);
            Assert.Contains($"| `{field}` | spawn action | admin |", design);
        }
        Assert.Equal(CommandArgs.PoolsField, CommandArgs.TableName("action.pools.elite"));
        Assert.Equal("action.waveList.N.bosses", CommandArgs.TableName("action.waveList.3.bosses"));
    }

    [Fact]
    public void Pools_empty_value_required()
    {
        Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue("action.pools.elite", "").Error);
        Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue("action.waveList.1.bosses", "").Error);
    }

    // ================= Pursuit =================

    [Theory]
    [InlineData("14")]
    [InlineData("601")]
    [InlineData("60s")]
    [InlineData("-60")]
    [InlineData("60.5")]
    public void Pursuit_fails_when_the_field_accepts_a_value_the_validator_refuses(string value) =>
        WvRefused(WvLib(), "hunt", CommandArgs.PursuitField, value, EventValidator.PursuitAfterRule);

    [Fact]
    public void Pursuit_fails_when_the_field_refuses_a_value_the_validator_takes()
    {
        var lib = WvLib();
        foreach (var s in new[] { 15, 600, 60 })
        {
            Assert.Equal($"event hunt action.pursuit = {s}", AuthoringTests.Set(lib, "hunt", CommandArgs.PursuitField, s.ToString()));
            Valid(lib, "hunt");
            Assert.Equal(s, WvFind(lib, "hunt").Action!.Pursuit!.AfterSeconds);
        }
        Assert.Equal(60, (int)WaveAction(lib, "hunt")["pursuit"]!["afterSeconds"]!);
        AuthoringTests.Set(lib, "hunt", CommandArgs.PursuitField, "none");
        Assert.Null(WaveAction(lib, "hunt")["pursuit"]);
        Valid(lib, "hunt");
    }

    [Fact]
    public void Pursuit_fails_when_a_conflict_is_written()
    {
        var lib = WvLib();
        WvRefused(lib, "raid", CommandArgs.PursuitField, "60", EventValidator.PursuitHunt);
        WvRefused(lib, "fanned", CommandArgs.PursuitField, "60", EventValidator.PursuitFanOut);
        WvRefused(lib, "locked", CommandArgs.PursuitField, "60", EventValidator.PursuitRegionLock);
        WvRefused(lib, "apo", CommandArgs.PursuitField, "60", "action.pursuit is not an Apocalypse field");
        AuthoringTests.Set(lib, "hunt", CommandArgs.PursuitField, "60");
        WvRefused(lib, "hunt", "action.behaviour", "none", EventValidator.PursuitHunt);                  // the other order: Hunt removed under pursuit
        Valid(lib, "hunt");
    }

    [Fact]
    public void Pursuit_passes_set_and_none()
    {
        var lib = WvLib();
        Assert.Equal("event hunt action.pursuit = 90", AuthoringTests.Set(lib, "hunt", CommandArgs.PursuitField, "90"));
        Assert.Equal(90, WvFind(lib, "hunt").Action!.Pursuit!.AfterSeconds);
        AuthoringTests.Set(lib, "hunt", CommandArgs.PursuitField, "none");
        Assert.Null(WvFind(lib, "hunt").Action!.Pursuit);
    }

    [Fact]
    public void Pursuit_empty_value_required() =>
        Assert.Equal(CommandArgs.ValueRequired, CommandArgs.SettableValue(CommandArgs.PursuitField, "").Error);
}
