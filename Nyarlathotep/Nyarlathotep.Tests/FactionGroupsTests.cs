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

    // ---- D14: the groups are exactly what docs/GAME_ASSETS.md › Faction compatibility records allied

    static readonly Dictionary<string, string> GroupOfLabel = new()
    {
        ["Undead"] = "undead", ["Spiders"] = "spiders", ["Bandits"] = "bandits", ["Militia"] = "militia", ["Church of Light"] = "church",
        ["Legion"] = "legion", ["Gloomrot"] = "gloomrot", ["Cursed"] = "cursed", ["Blackfangs"] = "blackfangs",
    };

    /// <summary>The Faction compatibility section's table rows as (group id, units, allied) and its "Left out" units.</summary>
    internal static (List<(string Group, HashSet<string> Units, bool Allied)> Rows, HashSet<string> LeftOut) Recorded(string doc)
    {
        var start = doc.IndexOf("## Faction compatibility", StringComparison.Ordinal);
        var end = doc.IndexOf("\n## ", start + 3, StringComparison.Ordinal);
        var section = doc[start..(end < 0 ? doc.Length : end)];
        var rows = new List<(string, HashSet<string>, bool)>();
        var leftOut = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in section.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            var names = System.Text.RegularExpressions.Regex.Matches(line, @"`(CHAR_\w+)`").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            if (line.StartsWith("- **Left out:", StringComparison.Ordinal)) leftOut.UnionWith(names);
            if (!line.StartsWith("| ", StringComparison.Ordinal) || line.StartsWith("| Group", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
                continue;
            var cells = line.Split('|');
            var label = cells[1].Trim();
            var paren = label.IndexOf(" (", StringComparison.Ordinal);
            rows.Add((GroupOfLabel[paren < 0 ? label : label[..paren]], names, cells[^2].Trim().StartsWith("allied", StringComparison.Ordinal)));
        }
        return (rows, leftOut);
    }

    static string Assets => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "GAME_ASSETS.md"));

    static string? Mismatch(IReadOnlyList<FactionGroup> groups, string doc)
    {
        var (rows, leftOut) = Recorded(doc);
        foreach (var g in groups)
        {
            var allied = rows.Where(r => r.Group == g.Name && r.Allied).SelectMany(r => r.Units).ToHashSet(StringComparer.Ordinal);
            var hostile = rows.Where(r => r.Group == g.Name && !r.Allied).SelectMany(r => r.Units).ToHashSet(StringComparer.Ordinal);
            if (g.Names.FirstOrDefault(p => !allied.Contains(p) || hostile.Contains(p) || leftOut.Contains(p)) is { } extra)
                return $"{g.Name} holds {extra}, not recorded allied";
            if (allied.FirstOrDefault(p => !g.Names.Contains(p)) is { } missing) return $"{g.Name} misses {missing}, recorded allied";
        }
        return rows.Select(r => r.Group).Distinct().FirstOrDefault(id => groups.All(g => g.Name != id)) is { } none ? $"no group {none}" : null;
    }

    [Fact]
    public void Recorded_passes_the_recorded_rows()
    {
        Assert.Null(Mismatch(FactionGroups.All, Assets));
        Assert.Contains(FactionGroups.All, g => g.Name == "blackfangs" && g.Faction == "Faction_Blackfangs");
    }

    [Fact]
    public void Recorded_fails_when_a_group_holds_a_unit_not_recorded_allied()
    {
        var extra = FactionGroups.All.Select(g => g.Name == "undead" ? g with { Names = [.. g.Names, "CHAR_Undead_ShadowSoldier"] } : g).ToList();
        Assert.Equal("undead holds CHAR_Undead_ShadowSoldier, not recorded allied", Mismatch(extra, Assets));
        var hostile = FactionGroups.All.Select(g => g.Name == "undead" ? g with { Names = [.. g.Names, "CHAR_Undead_ZealousCultist_Ghost"] } : g).ToList();
        Assert.Equal("undead holds CHAR_Undead_ZealousCultist_Ghost, not recorded allied", Mismatch(hostile, Assets));
    }

    [Fact]
    public void Recorded_fails_when_a_group_misses_a_recorded_unit()
    {
        var less = FactionGroups.All.Select(g => g.Name == "blackfangs" ? g with { Names = g.Names.Where(p => p != "CHAR_Blackfang_Lucie_VBlood").ToList() } : g).ToList();
        Assert.Equal("blackfangs misses CHAR_Blackfang_Lucie_VBlood, recorded allied", Mismatch(less, Assets));
    }

    [Fact]
    public void Recorded_empty_no_left_out_unit_in_a_group()
    {
        var (_, leftOut) = Recorded(Assets);
        Assert.Contains("CHAR_Undead_ZealousCultist_Ghost", leftOut);
        Assert.DoesNotContain(FactionGroups.All, g => g.Names.Any(leftOut.Contains));
    }
}
