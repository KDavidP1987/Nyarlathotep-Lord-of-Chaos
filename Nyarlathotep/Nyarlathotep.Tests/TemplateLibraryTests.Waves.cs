using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>Epic A43: the six faction wave templates (owner request 2026-10-15). Each is a Manual, disabled SpawnWaves
/// waveList at the admin, held in a 40 m arena, hunting, with the scoreboard and a K/D winner, its waves growing and its
/// last wave led by one V Blood; realm-gauntlet sends one faction per wave and starts each only when the last is cleared.</summary>
public partial class TemplateLibraryTests
{
    /// <summary>Every unit the wave templates name, for the test catalogue (each is in Reference Data/unit_index.tsv).</summary>
    internal static readonly string[] WavePrefabs =
    [
        "CHAR_Militia_Hound", "CHAR_Militia_Guard", "CHAR_Militia_Longbowman", "CHAR_Militia_Torchbearer", "CHAR_Militia_Heavy",
        "CHAR_Militia_Nun", "CHAR_Militia_Devoted", "CHAR_Militia_Bomber",
        "CHAR_ChurchOfLight_Footman", "CHAR_ChurchOfLight_Archer", "CHAR_ChurchOfLight_Rifleman", "CHAR_ChurchOfLight_Cleric",
        "CHAR_ChurchOfLight_Knight_Shield", "CHAR_ChurchOfLight_Knight_2H", "CHAR_ChurchOfLight_Priest", "CHAR_ChurchOfLight_Paladin",
        "CHAR_ChurchOfLight_Lightweaver", "CHAR_ChurchOfLight_CardinalAide",
        "CHAR_Legion_Shadowkin", "CHAR_Legion_Assassin", "CHAR_Legion_Vargulf", "CHAR_Legion_BloodProphet", "CHAR_Legion_NightMaiden",
        "CHAR_Legion_Dreadhorn", "CHAR_Legion_Gargoyle", "CHAR_Legion_Nightmare", "CHAR_Legion_Guardian_DraculaMinion",
        "CHAR_Vampire_BloodKnight_VBlood",
        "CHAR_Gloomrot_Technician", "CHAR_Gloomrot_Tazer", "CHAR_Gloomrot_Railgunner", "CHAR_Gloomrot_Pyro", "CHAR_Gloomrot_Batoon",
        "CHAR_Gloomrot_SentryOfficer", "CHAR_Gloomrot_TractorBeamer", "CHAR_Gloomrot_SpiderTank_Driller", "CHAR_Gloomrot_Voltage_VBlood",
        "CHAR_Cursed_Wolf", "CHAR_Cursed_ToadSpitter", "CHAR_Cursed_MonsterToad", "CHAR_Cursed_Nightlurker",
        "CHAR_Cursed_Bear_Standard", "CHAR_Cursed_WormTerror", "CHAR_Cursed_Witch", "CHAR_Cursed_Wolf_Spirit", "CHAR_Cursed_Bear_Spirit",
        "CHAR_Cursed_Witch_VBlood",
    ];

    /// <summary>Each wave template: its id, the unit total of each wave and its last wave's boss.</summary>
    static readonly (string Id, int[] Waves, string Boss, bool AfterSeconds)[] WaveTemplates =
    [
        ("militia-muster", [6, 9, 9, 11, 11], "CHAR_Militia_Leader_VBlood", true),
        ("silverlight-crusade", [7, 9, 9, 11, 10], "CHAR_ChurchOfLight_Paladin_VBlood", true),
        ("legion-incursion", [6, 8, 8, 8], "CHAR_Vampire_BloodKnight_VBlood", true),
        ("gloomrot-assault", [6, 8, 9, 11], "CHAR_Gloomrot_Voltage_VBlood", true),
        ("cursed-night", [7, 8, 9, 9], "CHAR_Cursed_Witch_VBlood", true),
        ("realm-gauntlet", [8, 8, 9, 9, 8], "CHAR_ChurchOfLight_Overseer_VBlood", false),
    ];

    static List<string> WaveDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        foreach (var (id, waves, boss, after) in WaveTemplates)
        {
            var t = c.Find(id);
            if (t is null) { diffs.Add($"{id} missing"); continue; }
            if (t.Invalid is { } why) { diffs.Add($"{id} invalid: {why}"); continue; }
            var d = t.Definition;
            if (d.Enabled) diffs.Add($"{id} ships enabled");
            if (d.Pillar != Pillar.Spawns || d.Trigger.Type != TriggerType.Manual) diffs.Add($"{id} is not a Manual spawns event");
            if (d.Action is not { WaveList: { } list } a) { diffs.Add($"{id} has no waveList"); continue; }
            var sizes = list.Select(w => w.Units.Sum(u => u.Count)).ToArray();
            if (!sizes.SequenceEqual(waves)) diffs.Add($"{id} waves {string.Join(",", sizes)}, not {string.Join(",", waves)}");
            if (sizes.Any(s => s > 20)) diffs.Add($"{id} has a wave over the default MaxUnitsPerWave");
            if (list.Skip(1).Any(w => !w.WhenCleared || (w.AfterSeconds is not null) != after))
                diffs.Add($"{id}'s later waves do not start as planned");
            if (a.Location.Type != LocationType.Admin || a.Arena?.Radius != 40 || a.Behaviour?.Type != BehaviourType.Hunt)
                diffs.Add($"{id} is not at the admin in a 40 m arena, hunting");
            if (!a.Scoreboard || a.Winner != WinnerRule.KillDeath) diffs.Add($"{id} lacks the scoreboard or its winner");
            if (a.Bosses is not [{ Count: 1 } b] || b.Prefab != boss) diffs.Add($"{id} does not end on one {boss}");
            if (d.Announce.Start.Count == 0 || d.Announce.End.Count == 0) diffs.Add($"{id} lacks a start or end line");
        }
        return diffs;
    }

    [Fact]
    public void WaveTemplates_passes_the_six_faction_templates() => Assert.Empty(WaveDifferences(Real()));

    [Fact]
    public void WaveTemplates_empty_no_templates() =>
        Assert.Contains("realm-gauntlet missing", WaveDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));

    [Fact]
    public void WaveTemplates_fails_when_a_unit_is_refused()
    {
        var units = Units();
        units.Denied.Add("CHAR_Legion_Shadowkin");
        var c = TemplateCatalog.Load(Bytes(RealText), units, units);
        Assert.Contains(WaveDifferences(c), d => d.StartsWith("legion-incursion invalid: ", StringComparison.Ordinal));
    }
}
