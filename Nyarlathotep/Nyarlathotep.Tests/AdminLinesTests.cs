using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D14: `.nyar debug here` lists at most 10 native NPCs, nearest first, each read back from
/// its live carrier, or "carrier none" for a plain NPC.</summary>
public class AdminLinesTests
{
    static NativeRow Row(string prefab, float d, long key, NativeCarrier? carrier = null) =>
        new(prefab, "Faction_Bandits", d, key, carrier, 0, 20, 300, 400, 25.5f, 10f, 1f, 4.5f);

    static NativeCarrier Carrier(IReadOnlyList<string>? strip = null, IReadOnlyList<StatModifier>? mods = null) =>
        new("surge", 42, "Replace", 1, false, "Destroy", true, strip ?? [],
            mods ?? EmpowerStats.Modifiers(new EmpowerStats(PhysicalPower: 1.3, AttackSpeed: 1.15)));

    [Fact]
    public void Eleven_rows_give_ten_lines_nearest_first()
    {
        var rows = Enumerable.Range(1, 11).Select(i => Row("CHAR_Bandit_Thug", 12 - i, i)).ToList();
        var lines = AdminLines.Natives(rows, 10);
        Assert.Equal(10, lines.Count);
        var distances = lines.Select(l => int.Parse(l.Split(" d ")[1].Split('m')[0])).ToList();
        Assert.Equal(distances.OrderBy(x => x), distances);
        Assert.DoesNotContain(lines, l => l.Contains(" d 11m"));
    }

    [Fact]
    public void A_distance_tie_is_broken_by_prefab_then_key()
    {
        var lines = AdminLines.Natives([Row("CHAR_Bandit_Thug", 5, 9), Row("CHAR_Bandit_Archer", 5, 7), Row("CHAR_Bandit_Thug", 5, 3)], 10);
        Assert.StartsWith("CHAR_Bandit_Archer", lines[0]);
        Assert.Equal(AdminLines.Native(Row("CHAR_Bandit_Thug", 5, 3)), lines[1]);
        Assert.Equal(3, lines.Count);
        var byKey = AdminLines.Natives([Row("CHAR_X", 5, 9, Carrier()), Row("CHAR_X", 5, 3)], 1);
        Assert.Contains("carrier none", byKey[0]);                        // key 3 before key 9
    }

    [Fact]
    public void A_carried_row_shows_every_carrier_field_and_its_mods_in_buffer_order()
    {
        var line = AdminLines.Native(Row("CHAR_Bandit_Thug", 4.4f, 1, Carrier()));
        Assert.Equal("CHAR_Bandit_Thug native Bandits d 4m carrier surge left 42s type Replace stacks 1 incr False end Destroy mark ok strip ok " +
                     "mods PhysicalPower:MultiplyBaseAdd:0.3,PrimaryAttackSpeed:MultiplyBaseAdd:0.15,AbilityAttackSpeed:MultiplyBaseAdd:0.15 " +
                     "other stat buffs 0 lvl 20 hp 300/400 pp 25.5 sp 10 aspd 1 mspd 4.5", line);
        var reversed = AdminLines.Native(Row("CHAR_Bandit_Thug", 4, 1, Carrier(mods:
            [new StatModifier("MaxHealth", "MultiplyBaseAdd", 0.5), new StatModifier("PhysicalPower", "Add", 2)])));
        Assert.Contains("mods MaxHealth:MultiplyBaseAdd:0.5,PhysicalPower:Add:2 ", reversed);
    }

    [Fact]
    public void A_stripped_component_still_present_is_named()
    {
        var line = AdminLines.Native(Row("CHAR_Bandit_Thug", 1, 1, Carrier(strip: ["GameplayEventListeners", "DestroyOnGameplayEvent"])));
        Assert.Contains(" strip missing:GameplayEventListeners,DestroyOnGameplayEvent ", line);
        Assert.Contains(" mark ok ", line);
        Assert.Contains(" mark bad ", AdminLines.Native(Row("CHAR_Bandit_Thug", 1, 1, Carrier() with { MarkOk = false })));
    }

    [Fact]
    public void A_plain_row_shows_carrier_none_and_no_carrier_fields()
    {
        var line = AdminLines.Native(Row("CHAR_Bandit_Thug", 2, 1));
        Assert.Equal("CHAR_Bandit_Thug native Bandits d 2m carrier none other stat buffs 0 lvl 20 hp 300/400 pp 25.5 sp 10 aspd 1 mspd 4.5", line);
    }

    [Fact]
    public void No_rows_give_no_lines()
    {
        Assert.Empty(AdminLines.Natives([], 10));
    }
}
