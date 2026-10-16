using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D4: FactionGroups.Problem counts every member of every pool a wave can draw (its pool entries and
/// its boss rolls) as a prefab of that wave, so a pool mixing two allied groups is refused for a single-group template,
/// and a template of several groups (design §9 D60, realm-gauntlet's rule) keeps each wave's pools in that wave's group.</summary>
public class FactionGroupsTests
{
    const string Guardian = "CHAR_Undead_Guardian", Banshee = "CHAR_Undead_GhostBanshee", Bishop = "CHAR_Undead_BishopOfDeath_VBlood";
    const string Light = "CHAR_Militia_Light", Crossbow = "CHAR_Militia_Crossbow", Thug = "CHAR_Bandit_Thug";

    static SpawnWavesAction UnitsForm(Dictionary<string, IReadOnlyList<string>> pools, IReadOnlyList<UnitEntry> units, IReadOnlyList<BossRoll>? bosses = null) =>
        new SpawnWavesAction(units, 3, 60, 10, new Location(LocationType.Point, 0, 0), null) with { Pools = pools, Bosses = bosses };

    static SpawnWavesAction WaveList(Dictionary<string, IReadOnlyList<string>> pools, params WaveSpec[] waves) =>
        new SpawnWavesAction(waves.SelectMany(w => w.Units).ToList(), waves.Length, 0, 10, new Location(LocationType.Point, 0, 0), null) with
        {
            WaveList = waves, Pools = pools,
        };

    static UnitEntry From(string pool, int count = 3) => UnitEntry.FromPool(pool, count);

    [Fact]
    public void Pools_fails_when_a_pool_mixing_two_groups_passes()
    {
        var mixed = UnitsForm(new() { ["horde"] = [Guardian, Light] }, [From("horde")]);
        Assert.Equal("t mixes the undead and militia groups", FactionGroups.Problem("t", mixed));
        var rolled = UnitsForm(new() { ["horde"] = [Guardian, Banshee], ["lords"] = [Bishop, Light] }, [From("horde")], [BossRoll.Roll("lords", 1, 1)]);
        Assert.Equal("t mixes the undead and militia groups", FactionGroups.Problem("t", rolled));             // a boss roll's pool counts too
        var beside = UnitsForm(new() { ["horde"] = [Guardian, Banshee] }, [From("horde"), new UnitEntry(Thug, 2)]);
        Assert.Equal("t mixes the undead and bandits groups", FactionGroups.Problem("t", beside));             // a prefab entry beside the pool
    }

    [Fact]
    public void Pools_fails_when_a_pool_member_in_no_group_passes()
    {
        var a = UnitsForm(new() { ["horde"] = [Guardian, "CHAR_Pool_Nobody"] }, [From("horde")]);
        Assert.Equal("t: CHAR_Pool_Nobody is in no allied group", FactionGroups.Problem("t", a));
        var boss = UnitsForm(new() { ["horde"] = [Guardian], ["lords"] = ["CHAR_Lord_Nobody"] }, [From("horde")], [BossRoll.Roll("lords", 1, 1)]);
        Assert.Equal("t: CHAR_Lord_Nobody is in no allied group", FactionGroups.Problem("t", boss));
    }

    [Fact]
    public void Pools_fails_when_a_wave_drawing_from_another_groups_pool_passes_realm_gauntlets_rule()
    {
        var pools = new Dictionary<string, IReadOnlyList<string>> { ["dead"] = [Guardian, Banshee], ["guard"] = [Light, Crossbow] };
        var good = WaveList(pools,
            new WaveSpec([From("dead")]),
            new WaveSpec([From("guard")], null, true));
        Assert.Null(FactionGroups.Problem("realm-gauntlet", good));                                             // each wave one group, cleared between
        var crossed = WaveList(pools,
            new WaveSpec([From("dead")]),
            new WaveSpec([new UnitEntry(Light, 2), From("dead")], null, true));
        Assert.Equal("realm-gauntlet wave 2 mixes the undead and militia groups", FactionGroups.Problem("realm-gauntlet", crossed));
        var bossed = WaveList(new() { ["dead"] = [Guardian, Banshee], ["lords"] = [Bishop] },
            new WaveSpec([From("dead")]),
            new WaveSpec([new UnitEntry(Light, 2)], null, true, [BossRoll.Roll("lords", 1, 1)]));
        Assert.Equal("realm-gauntlet wave 2 mixes the undead and militia groups", FactionGroups.Problem("realm-gauntlet", bossed));
        var timed = WaveList(pools,
            new WaveSpec([From("dead")]),
            new WaveSpec([From("guard")], 60, false));
        Assert.Equal("realm-gauntlet mixes groups but wave 2 does not wait for wave 1 to be cleared", FactionGroups.Problem("realm-gauntlet", timed));
    }

    [Fact]
    public void Pools_passes_one_group_and_a_prefab_only_action()
    {
        var a = UnitsForm(new() { ["horde"] = [Guardian, Banshee], ["lords"] = [Bishop] }, [From("horde")], [BossRoll.Roll("lords", 1, 1)]);
        Assert.Null(FactionGroups.Problem("t", a));
        // a prefab-only action keeps 0.12.0's answers: the final wave's bosses spawn with it and count in its group
        var waves = new[] { new WaveSpec([new UnitEntry(Guardian, 2)]), new WaveSpec([new UnitEntry(Light, 2)], null, true) };
        Assert.Null(FactionGroups.Problem("t", WaveList([], waves) with { Pools = null }));
        Assert.Equal("t wave 2 mixes the undead and militia groups",
            FactionGroups.Problem("t", WaveList([], waves) with { Pools = null, Bosses = [BossRoll.Fixed(Bishop, 1)] }));
        Assert.Null(FactionGroups.Problem("t", WaveList([], waves) with { Pools = null, Bosses = [BossRoll.Fixed("CHAR_Militia_Leader_VBlood", 1)] }));
    }

    [Fact]
    public void Pools_empty_no_pools() =>
        Assert.Null(FactionGroups.Problem("t", UnitsForm([], [new UnitEntry(Guardian, 2)]) with { Pools = null }));
}
