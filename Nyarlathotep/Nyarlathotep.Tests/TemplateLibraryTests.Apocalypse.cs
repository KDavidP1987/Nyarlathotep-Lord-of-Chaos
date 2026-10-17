using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D18: zombie-apocalypse, disabled and valid: Manual, pillar spawns, Faction_Undead, CHAR_Undead_* units at
/// levels 20-40 with one tough leader (maxHealth 2.0), one Faction_Undead V Blood boss, an Admin goal of radius 30 named
/// "the living", marchSpeed 2.0, 4 h, the scoreboard on. Retuned by undead-hordes D15 (Design › Templates): 5 hotspots,
/// 10 roamers each, 1-3 per kill from its troops pool, max 100, 20 a minute, 4 hordes from 3 min every 20 min of 30 + 15;
/// its worst case (100 roamers and all four hordes alive with the boss: 311) passes the default caps, which cut it and name
/// the cut (D12).
/// apocalypse D38: every shipped SpawnWaves and Apocalypse template keeps its units and bosses inside one allied group of
/// Logic/FactionGroups, and an Apocalypse template's group is its faction's.</summary>
public partial class TemplateLibraryTests
{
    const int TemplatesNow = TemplatesBefore + 2 + 6 + 14 + 2;                   // revenge's bandit-revenge, apocalypse's zombie-apocalypse, Epic A43's six wave templates, wave-variety's fourteen, undead-hordes' two

    static readonly string[] UndeadGroup =
    [
        "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane", "CHAR_Undead_Guardian",
        "CHAR_Undead_GhostMilitia_Light", "CHAR_Undead_GhostBanshee", "CHAR_Undead_BishopOfDeath_VBlood",
    ];

    /// <summary>The worst case of an Apocalypse action: every roamer alive and every horde alive with its bosses.</summary>
    internal static int WorstCase(ApocalypseAction a) =>
        a.Roamers.Max + Enumerable.Range(1, a.Hordes.Count).Sum(k => a.Hordes.Size + (k - 1) * a.Hordes.Growth) + (a.Bosses ?? []).Sum(b => b.Count);

    static List<string> ApocalypseDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != TemplatesNow) diffs.Add($"{c.Templates.Count} templates, not {TemplatesNow}");
        var t = c.Find("zombie-apocalypse");
        if (t is null) { diffs.Add("zombie-apocalypse missing"); return diffs; }
        if (t.Invalid is { } why) diffs.Add($"invalid: {why}");
        var d = t.Definition;
        if (d.Enabled) diffs.Add("enabled");
        if (d.Trigger.Type != TriggerType.Manual) diffs.Add("not Manual");
        if (d.Pillar != Pillar.Spawns) diffs.Add("not pillar spawns");
        if (d.DurationSeconds != 14400) diffs.Add($"duration {d.DurationSeconds}");
        if (d.Apocalypse is not { } a) { diffs.Add("no Apocalypse action"); return diffs; }
        if (a.Faction != "Faction_Undead") diffs.Add($"faction {a.Faction}");
        if (a.Units.Any(u => !u.Prefab.StartsWith("CHAR_Undead_", StringComparison.Ordinal))) diffs.Add("a unit is not CHAR_Undead_*");
        if (a.Units.Any(u => u.Modifiers?.Level is not (>= 20 and <= 40))) diffs.Add("a unit is outside levels 20-40");
        if (a.Units.Count(u => u.Modifiers?.MaxHealth == 2.0) != 1) diffs.Add("not one tough leader at maxHealth 2.0");
        if (a.Hotspots.Count != 5) diffs.Add($"{a.Hotspots.Count} hotspots");
        if (a.Roamers is not { PerHotspot: 10, PerKillMin: 1, PerKillMax: 3, Pool: "troops", Max: 100, PerMinute: 20 }) diffs.Add("roamers differ");
        if (a.Hordes is not { Count: 4, FirstAfterMinutes: 3, EveryMinutes: 20, Size: 30, Growth: 15 }) diffs.Add("hordes differ");
        if (a.Bosses is not [{ Count: 1 } boss] || !boss.Prefab.EndsWith("_VBlood", StringComparison.Ordinal) || !UndeadGroup.Contains(boss.Prefab))
            diffs.Add("lacks its one undead V Blood boss");
        if (a.Goal is not { Radius: 30, Name: "the living" } || a.Goal.Location.Type != LocationType.Admin) diffs.Add("goal differs");
        if (a.MarchSpeed != 2.0) diffs.Add($"marchSpeed {a.MarchSpeed}");
        if (!a.Scoreboard) diffs.Add("no scoreboard");
        // undead-hordes D15: the worst case passes the default caps, so a start at the default cfg names its cut (D12)
        if (WorstCase(a) <= Limits.MaxTrackedUnits.Default) diffs.Add($"worst case {WorstCase(a)} fits MaxTrackedUnits {Limits.MaxTrackedUnits.Default}: the retune is lost");
        return diffs;
    }

    [Fact]
    public void Apocalypse_passes_zombie_apocalypse_as_planned()
    {
        var c = Real();
        Assert.Empty(ApocalypseDifferences(c));
        Assert.Equal(TemplatesNow, c.ValidCount);
        Assert.Equal(311, WorstCase(c.Find("zombie-apocalypse")!.Definition.Apocalypse!));
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("invalid")]
    [InlineData("no boss")]
    [InlineData("roamers")]
    [InlineData("hordes")]
    [InlineData("no scoreboard")]
    [InlineData("no leader")]
    [InlineData("fifteenth")]
    [InlineData("missing")]
    public void Apocalypse_fails_when_the_template_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == "zombie-apocalypse");
        var action = (JsonObject)ev["action"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "invalid": action["bogus"] = 1; break;
            case "no boss": action.Remove("bosses"); break;
            case "roamers": action["roamers"]!["max"] = 30; break;               // 0.13.1's roamers
            case "hordes": action["hordes"]!["size"] = 7; break;                 // 0.13.1's horde
            case "no scoreboard": action["scoreboard"] = false; break;
            case "no leader": action["units"]![4]!["modifiers"]!["maxHealth"] = 1.5; break;
            case "fifteenth":
                var copy = JsonNode.Parse(ev.ToJsonString())!.AsObject();
                copy["id"] = "zombie-apocalypse-2";
                events.Add(copy);
                break;
            default: events.Remove(ev); break;
        }
        Assert.NotEmpty(ApocalypseDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units())));
    }

    [Fact]
    public void Apocalypse_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("zombie-apocalypse missing",
            ApocalypseDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));

    // ---- D38 Factions

    /// <summary>Every unit and boss prefab of one template, each wave's included, and its Apocalypse faction; null for a
    /// template that spawns nothing (Empower).</summary>
    static (List<string> Prefabs, string? Faction)? Spawned(EventDefinition d)
    {
        // an apocalypse-type action's pool members and a horde march's bosses count too (undead-hordes D14)
        if (d.Apocalypse is { } a)
            return ([.. a.Units.SelectMany(u => u.Pool is { } p ? a.Pools![p] : [u.Prefab]), .. (a.Bosses ?? []).Select(b => b.Prefab),
                .. (a.March?.Bosses ?? []).SelectMany(b => b.Pool is { } p ? a.Pools![p] : [b.Prefab])], a.Faction);
        if (d.Action is not { } w) return null;
        var prefabs = w.Units.Select(u => u.Prefab).Concat((w.WaveList ?? []).SelectMany(x => x.Units.Select(u => u.Prefab)))
            .Concat((w.Bosses ?? []).Select(b => b.Prefab)).ToList();
        return (prefabs, null);
    }

    internal static List<string> FactionProblems(TemplateCatalog c)
    {
        var problems = new List<string>();
        var checkedAny = 0;
        foreach (var t in c.Templates)
        {
            if (Spawned(t.Definition) is not { } s) continue;
            checkedAny++;
            var problem = t.Definition.Action is { } w                // design §9 D60 per wave, pool members included (wave-variety D4)
                ? FactionGroups.Problem(t.Id, w)
                : FactionGroups.Problem(t.Id, s.Prefabs, s.Faction);
            if (problem is { } p) problems.Add(p);
        }
        if (checkedAny == 0) problems.Add("no template spawns units");
        return problems;
    }

    [Fact]
    public void Factions_passes_every_template_in_one_allied_group()
    {
        var c = Real();
        Assert.Empty(FactionProblems(c));
        Assert.Equal(32, c.Templates.Count(t => Spawned(t.Definition) is not null));   // 29 SpawnWaves (Epic A43's six, wave-variety's fourteen), one Apocalypse, an Infestation and a HordeMarch
    }

    /// <summary>Design §9 D60: realm-gauntlet mixes groups across waves; it passes only while each wave is one group, each
    /// later wave waits for the last to be cleared with no timer, and its boss is of the last wave's group.</summary>
    [Theory]
    [InlineData("wave", "realm-gauntlet wave 2 mixes the undead and bandits groups")]
    [InlineData("after", "realm-gauntlet mixes groups but wave 3 does not wait for wave 2 to be cleared")]
    [InlineData("cleared", "realm-gauntlet mixes groups but wave 3 does not wait for wave 2 to be cleared")]
    [InlineData("boss", "realm-gauntlet wave 5 mixes the bandits and church groups")]
    public void Factions_fails_when_a_gauntlet_lets_two_groups_meet(string mutation, string expected)
    {
        var root = JsonNode.Parse(RealText)!;
        var action = ((JsonArray)root["events"]!).OfType<JsonObject>().First(e => (string)e["id"]! == "realm-gauntlet")["action"]!;
        var waves = (JsonArray)action["waveList"]!;
        switch (mutation)
        {
            case "wave": waves[1]!["units"]![0]!["prefab"] = "CHAR_Bandit_Thug"; break;
            case "after": waves[2]!["afterSeconds"] = 120; break;
            case "cleared": waves[2]!["whenCleared"] = false; waves[2]!["afterSeconds"] = 120; break;
            case "boss": action["bosses"]![0]!["prefab"] = "CHAR_Bandit_Deadeye"; break;
        }
        var problems = FactionProblems(TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units()));
        Assert.Contains(expected, problems);
    }

    [Theory]
    [InlineData("undead-rising", "CHAR_Bandit_Thug", "mixes the undead and bandits groups")]
    [InlineData("zombie-apocalypse", "CHAR_Undead_ShadowSoldier", "CHAR_Undead_ShadowSoldier is in no allied group")]
    [InlineData("zombie-apocalypse", "CHAR_Legion_Shadowkin_Lesser", "mixes the undead and legion groups")]   // in the legion group since wave-variety D14
    [InlineData("spider-swarm", "CHAR_Militia_Light", "mixes the spiders and militia groups")]
    public void Factions_fails_when_a_template_mixes_two_groups(string id, string prefab, string expected)
    {
        var root = JsonNode.Parse(RealText)!;
        var ev = ((JsonArray)root["events"]!).OfType<JsonObject>().First(e => (string)e["id"]! == id);
        ev["action"]!["units"]![0]!["prefab"] = prefab;
        var known = Units(prefab, "CHAR_Undead_GhostMilitia_Light", "CHAR_Undead_GhostBanshee", "CHAR_Undead_BishopOfDeath_VBlood");
        var problems = FactionProblems(TemplateCatalog.Load(Bytes(root.ToJsonString()), known, known));
        Assert.Contains(problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Factions_fails_when_an_apocalypse_is_not_of_its_factions_group()
    {
        var root = JsonNode.Parse(RealText)!;
        var ev = ((JsonArray)root["events"]!).OfType<JsonObject>().First(e => (string)e["id"]! == "zombie-apocalypse");
        ev["action"]!["faction"] = "Faction_Bandits";
        Assert.Contains("zombie-apocalypse: its units are not of Faction_Bandits's group", FactionProblems(TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units())));
        // and the boss counts: a bandit boss in an undead apocalypse mixes two groups
        Assert.Equal("z mixes the undead and bandits groups", FactionGroups.Problem("z", ["CHAR_Undead_Guardian", "CHAR_Bandit_Deadeye"], "Faction_Undead"));
    }

    /// <summary>Code review L4: a group holds its checked names exactly; an unchecked variant of one (a "_Summon" twin, a
    /// longer name sharing its start) belongs to no group until it is listed.</summary>
    [Theory]
    [InlineData("CHAR_Undead_Guardian_Summon")]
    [InlineData("CHAR_Spider_Forest_Summon")]
    [InlineData("CHAR_Bandit_Thug_Summon")]
    [InlineData("CHAR_Militia_Light_Farbane")]
    public void Factions_fails_when_an_unlisted_variant_joins_a_group(string variant)
    {
        Assert.Empty(FactionGroups.Of(variant));
        Assert.Equal($"z: {variant} is in no allied group", FactionGroups.Problem("z", [variant]));
        var listed = variant[..variant.LastIndexOf('_')];
        Assert.Single(FactionGroups.Of(listed));                               // the checked name itself is held
    }

    [Fact]
    public void Factions_empty_no_template_spawns_units() =>
        Assert.Equal(["no template spawns units"], FactionProblems(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));
}
