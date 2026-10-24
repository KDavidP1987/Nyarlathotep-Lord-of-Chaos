#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyarlathotep.Logic;

/// <summary>One allied group (apocalypse D37): units of these prefab families stood together in game without fighting
/// (docs/GAME_ASSETS.md › Faction compatibility), and <see cref="Faction"/> is the faction an Apocalypse template of this
/// group names.</summary>
public sealed record FactionGroup(string Name, string Faction, IReadOnlyList<string> Names)
{
    /// <summary>The prefab belongs to this group: it is exactly one of its names. A variant (a "_Summon" or "_Farbane" twin)
    /// joins only when it is listed itself, after a mix run showed it allied (step 3 code review L4).</summary>
    public bool Holds(string prefab) => Names.Contains(prefab, StringComparer.Ordinal);
}

/// <summary>
/// The allied groups a shipped template's units and bosses must stay inside (apocalypse D38), from the in-game faction
/// mixes of D37 (tools/vrclient/scenarios/faction-mix.vrs, 2026-10-13). Each group lists the unit names checked allied, so
/// a new unit joins a group only after a mix run shows it allied. Left out on purpose: CHAR_Undead_ShadowSoldier (it
/// killed its twin on spawn) and CHAR_Undead_ZealousCultist_Ghost (it dies on its own seconds after spawning). The militia
/// additions and the church, legion, gloomrot and cursed groups are the faction wave templates' (design §9 D60), each
/// checked by tools/vrclient/scenarios/faction-mix-waves.vrs; every faction's other combat units and V Bloods, and the
/// blackfangs group, joined after tools/vrclient/scenarios/faction-mix-wide.vrs (wave-variety D13, D14). The Lesser Legion
/// shadows stand with the Legion but attacked the undead: groups are never alive together in one wave (D60).
/// </summary>
public static class FactionGroups
{
    public static readonly IReadOnlyList<FactionGroup> All =
    [
        new("undead", "Faction_Undead",
        [
            "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane",
            "CHAR_Undead_Guardian", "CHAR_Undead_GhostMilitia_Light", "CHAR_Undead_GhostBanshee",
            "CHAR_Undead_BishopOfDeath_VBlood", "CHAR_Cultist_Pyromancer", "CHAR_Cultist_Slicer",
            "CHAR_Undead_ArmoredSkeletonCrossbow_Dunley", "CHAR_Undead_Assassin", "CHAR_Undead_FlyingSkull",
            "CHAR_Undead_GhostAssassin", "CHAR_Undead_GhostGuardian", "CHAR_Undead_GhostMilitia_Crossbow",
            "CHAR_Undead_Ghoul_Armored_Farmlands", "CHAR_Undead_Necromancer", "CHAR_Undead_Priest",
            "CHAR_Undead_SkeletonApprentice", "CHAR_Undead_SkeletonGolem", "CHAR_Undead_SkeletonMage",
            "CHAR_Undead_SkeletonSoldier_Armored_Dunley", "CHAR_Undead_SkeletonSoldier_Infiltrator",
            "CHAR_Undead_UndyingGhoul", "CHAR_Undead_ArenaChampion_VBlood", "CHAR_Undead_BishopOfShadows_VBlood",
            "CHAR_Undead_Infiltrator_VBlood", "CHAR_Undead_Leader_Vblood", "CHAR_Undead_Priest_VBlood",
            "CHAR_Undead_ZealousCultist_VBlood",
        ]),
        new("spiders", "Faction_Spiders",
        [
            "CHAR_Spider_Forest", "CHAR_Spider_Melee", "CHAR_Spider_Range", "CHAR_Spider_Forestling",
            "CHAR_Spider_Queen_VBlood", "CHAR_Spider_Broodmother", "CHAR_Spider_Spiderling",
        ]),
        new("bandits", "Faction_Bandits",
        [
            "CHAR_Bandit_Thug", "CHAR_Bandit_Hunter", "CHAR_Bandit_Deadeye",
        ]),
        new("militia", "Faction_Militia",
        [
            "CHAR_Militia_Crossbow", "CHAR_Militia_Light", "CHAR_Militia_Hound", "CHAR_Militia_Guard",
            "CHAR_Militia_Longbowman", "CHAR_Militia_Torchbearer", "CHAR_Militia_Heavy", "CHAR_Militia_Nun",
            "CHAR_Militia_Devoted", "CHAR_Militia_Bomber", "CHAR_Militia_Leader_VBlood", "CHAR_Militia_BellRinger",
            "CHAR_Militia_Horseman", "CHAR_Militia_Rider", "CHAR_ChurchOfLight_Sommelier_VBlood",
            "CHAR_Militia_BishopOfDunley_VBlood", "CHAR_Militia_Glassblower_VBlood", "CHAR_Militia_Guard_VBlood",
            "CHAR_Militia_HoundMaster_VBlood", "CHAR_Militia_Hound_VBlood", "CHAR_Militia_Longbowman_LightArrow_Vblood",
            "CHAR_Militia_Nun_VBlood", "CHAR_Militia_Scribe_VBlood",
        ]),
        new("church", "Faction_ChurchOfLum",
        [
            "CHAR_ChurchOfLight_Footman", "CHAR_ChurchOfLight_Archer", "CHAR_ChurchOfLight_Rifleman",
            "CHAR_ChurchOfLight_Cleric", "CHAR_ChurchOfLight_Knight_Shield", "CHAR_ChurchOfLight_Knight_2H",
            "CHAR_ChurchOfLight_Priest", "CHAR_ChurchOfLight_Paladin", "CHAR_ChurchOfLight_Lightweaver",
            "CHAR_ChurchOfLight_CardinalAide", "CHAR_ChurchOfLight_Paladin_VBlood", "CHAR_ChurchOfLight_Overseer_VBlood",
            "CHAR_ChurchOfLight_SlaveMaster_Enforcer", "CHAR_ChurchOfLight_SlaveMaster_Sentry",
            "CHAR_ChurchOfLight_Cardinal_VBlood", "CHAR_Militia_Fabian_VBlood", "CHAR_Villager_Tailor_VBlood",
        ]),
        new("legion", "Faction_Legion",
        [
            "CHAR_Legion_Shadowkin", "CHAR_Legion_Assassin", "CHAR_Legion_Vargulf", "CHAR_Legion_BloodProphet",
            "CHAR_Legion_NightMaiden", "CHAR_Legion_Dreadhorn", "CHAR_Legion_Gargoyle", "CHAR_Legion_Nightmare",
            "CHAR_Legion_Guardian_DraculaMinion", "CHAR_Vampire_BloodKnight_VBlood", "CHAR_Legion_BloodProphet_Lesser",
            "CHAR_Legion_DreadHorn_Lesser", "CHAR_Legion_Gargoyle_Styx", "CHAR_Legion_Guardian_DraculaMinion_Lesser",
            "CHAR_Legion_NightMaiden_Lesser", "CHAR_Legion_Nightmare_Lesser", "CHAR_Legion_Shadowkin_Lesser",
            "CHAR_Legion_Vargulf_Lesser", "CHAR_Vampire_CrimsonIronMaiden", "CHAR_BatVampire_VBlood",
            "CHAR_Vampire_HighLord_VBlood", "CHAR_Vampire_IceRanger_VBlood",
        ]),
        new("gloomrot", "Faction_Gloomrot",
        [
            "CHAR_Gloomrot_Technician", "CHAR_Gloomrot_Tazer", "CHAR_Gloomrot_Railgunner", "CHAR_Gloomrot_Pyro",
            "CHAR_Gloomrot_Batoon", "CHAR_Gloomrot_SentryOfficer", "CHAR_Gloomrot_TractorBeamer",
            "CHAR_Gloomrot_SpiderTank_Driller", "CHAR_Gloomrot_Voltage_VBlood", "CHAR_Gloomrot_AceIncinerator",
            "CHAR_Gloomrot_SpiderTank_Gattler", "CHAR_Gloomrot_SpiderTank_LightningRod", "CHAR_Gloomrot_SpiderTank_Zapper",
            "CHAR_Gloomrot_Iva_VBlood", "CHAR_Gloomrot_Purifier_VBlood", "CHAR_Gloomrot_RailgunSergeant_VBlood",
            "CHAR_Gloomrot_TheProfessor_VBlood",
        ]),
        new("cursed", "Faction_Cursed",
        [
            "CHAR_Cursed_Wolf", "CHAR_Cursed_ToadSpitter", "CHAR_Cursed_MonsterToad", "CHAR_Cursed_Nightlurker",
            "CHAR_Cursed_Bear_Standard", "CHAR_Cursed_WormTerror", "CHAR_Cursed_Witch", "CHAR_Cursed_Wolf_Spirit",
            "CHAR_Cursed_Bear_Spirit", "CHAR_Cursed_Witch_VBlood", "CHAR_Scarecrow", "CHAR_Cursed_MountainBeast_VBlood",
            "CHAR_Cursed_ToadKing_VBlood", "CHAR_Undead_CursedSmith_VBlood", "CHAR_Villager_CursedWanderer_VBlood",
            "CHAR_Winter_Yeti_VBlood",
        ]),
        new("blackfangs", "Faction_Blackfangs",
        [
            "CHAR_Blackfang_Alchemist", "CHAR_Blackfang_DartFlinger", "CHAR_Blackfang_Lurker", "CHAR_Blackfang_Sentinel",
            "CHAR_Blackfang_Striker", "CHAR_Blackfang_Venomblade", "CHAR_Blackfang_Viper", "CHAR_Blackfang_WoodCarver",
            "CHAR_GoldGolem", "CHAR_Blackfang_CarverBoss_VBlood", "CHAR_Blackfang_Lucie_VBlood",
            "CHAR_Blackfang_Valyr_VBlood",
        ]),
    ];

    /// <summary>The V Bloods of <see cref="All"/> (horde-pools D5): the bosses half of each group's seeded pools, compiled
    /// because Reference Data is not on a server; PoolLibraryTests compares it with unit_index.tsv's V Blood flag. No
    /// bandit V Blood is in its allied group, so the seed has no bandits-bosses.</summary>
    public static readonly IReadOnlySet<string> Bosses = new HashSet<string>(StringComparer.Ordinal)
    {
        "CHAR_Undead_BishopOfDeath_VBlood", "CHAR_Undead_ArenaChampion_VBlood", "CHAR_Undead_BishopOfShadows_VBlood",
        "CHAR_Undead_Infiltrator_VBlood", "CHAR_Undead_Leader_Vblood", "CHAR_Undead_Priest_VBlood", "CHAR_Undead_ZealousCultist_VBlood",
        "CHAR_Spider_Queen_VBlood",
        "CHAR_Militia_Leader_VBlood", "CHAR_ChurchOfLight_Sommelier_VBlood", "CHAR_Militia_BishopOfDunley_VBlood",
        "CHAR_Militia_Glassblower_VBlood", "CHAR_Militia_Guard_VBlood", "CHAR_Militia_HoundMaster_VBlood", "CHAR_Militia_Hound_VBlood",
        "CHAR_Militia_Longbowman_LightArrow_Vblood", "CHAR_Militia_Nun_VBlood", "CHAR_Militia_Scribe_VBlood",
        "CHAR_ChurchOfLight_Paladin_VBlood", "CHAR_ChurchOfLight_Overseer_VBlood", "CHAR_ChurchOfLight_Cardinal_VBlood",
        "CHAR_Militia_Fabian_VBlood", "CHAR_Villager_Tailor_VBlood",
        "CHAR_Vampire_BloodKnight_VBlood", "CHAR_BatVampire_VBlood", "CHAR_Vampire_HighLord_VBlood", "CHAR_Vampire_IceRanger_VBlood",
        "CHAR_Gloomrot_Voltage_VBlood", "CHAR_Gloomrot_Iva_VBlood", "CHAR_Gloomrot_Purifier_VBlood", "CHAR_Gloomrot_RailgunSergeant_VBlood",
        "CHAR_Gloomrot_TheProfessor_VBlood",
        "CHAR_Cursed_Witch_VBlood", "CHAR_Cursed_MountainBeast_VBlood", "CHAR_Cursed_ToadKing_VBlood", "CHAR_Undead_CursedSmith_VBlood",
        "CHAR_Villager_CursedWanderer_VBlood", "CHAR_Winter_Yeti_VBlood",
        "CHAR_Blackfang_CarverBoss_VBlood", "CHAR_Blackfang_Lucie_VBlood", "CHAR_Blackfang_Valyr_VBlood",
    };

    /// <summary>The names of the groups whose members <paramref name="members"/> holds, in the table's order (a pool of
    /// the library "mixes groups" when there are two or more, horde-pools D5).</summary>
    public static IReadOnlyList<string> GroupsOf(IEnumerable<string> members)
    {
        var list = members.ToList();
        return All.Where(g => list.Any(g.Holds)).Select(g => g.Name).ToList();
    }

    /// <summary>The groups <paramref name="prefab"/> belongs to (one for a checked unit, none for an unchecked one).</summary>
    public static IReadOnlyList<FactionGroup> Of(string prefab) => All.Where(g => g.Holds(prefab)).ToList();

    /// <summary>The group of a faction name, or null.</summary>
    public static FactionGroup? ForFaction(string faction) => All.FirstOrDefault(g => g.Faction == faction);

    /// <summary>Why the prefabs of one template (its units and bosses, every wave's) are not one allied group, or null: a
    /// prefab in no group, two groups mixed, or an Apocalypse <paramref name="faction"/> whose group is not theirs.</summary>
    public static string? Problem(string templateId, IEnumerable<string> prefabs, string? faction = null)
    {
        var list = prefabs.Distinct(StringComparer.Ordinal).ToList();
        foreach (var p in list)
            if (Of(p).Count != 1) return $"{templateId}: {p} is in {(Of(p).Count == 0 ? "no allied group" : "more than one group")}";
        var groups = All.Where(g => list.Any(g.Holds)).Select(g => g.Name).ToList();          // in the table's order
        if (groups.Count > 1) return $"{templateId} mixes the {string.Join(" and ", groups)} groups";
        if (faction is not null)
        {
            if (ForFaction(faction) is not { } own) return $"{templateId}: {faction} has no allied group";
            if (groups.Count == 1 && groups[0] != own.Name) return $"{templateId}: its units are not of {faction}'s group";
        }
        return null;
    }

    /// <summary>A SpawnWaves template's problem (design §9 D60; wave-variety D4): one allied group as
    /// <see cref="Problem(string, IEnumerable{string}, string?)"/> says, or, for a waveList that mixes groups, null only when
    /// each wave is one group, every later wave waits for the last to be cleared with no afterSeconds (so two groups are
    /// never alive together), and each wave's bosses are of its group (they spawn with it). A wave's prefabs are every one
    /// it can spawn (Pools.Drawable): its prefab entries, every member of each pool its pool entries and boss rolls draw
    /// from, and its fixed bosses; a units-form action is one group throughout.</summary>
    public static string? Problem(string templateId, SpawnWavesAction action) =>
        Problem(templateId, Enumerable.Range(1, action.Waves).Select(n => Pools.Drawable(action, n).ToList()).ToList(), action.WaveList);

    /// <summary>The one-group rule over the prefabs of every wave, then design §9 D60's per-wave rule for a mixed list.</summary>
    static string? Problem(string templateId, IReadOnlyList<List<string>> perWave, IReadOnlyList<WaveSpec>? waves)
    {
        var whole = Problem(templateId, perWave.SelectMany(w => w));
        if (whole is null || !whole.Contains(" mixes the ", StringComparison.Ordinal) || waves is null) return whole;
        for (var i = 0; i < perWave.Count; i++)
        {
            if (Problem($"{templateId} wave {i + 1}", perWave[i]) is { } p) return p;
            if (i > 0 && (!waves[i].WhenCleared || waves[i].AfterSeconds is not null))
                return $"{templateId} mixes groups but wave {i + 1} does not wait for wave {i} to be cleared";
        }
        return null;
    }
}
