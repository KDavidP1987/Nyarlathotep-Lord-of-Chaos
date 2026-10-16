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
/// killed its twin on spawn) and the Lesser Legion shadows (they attacked the undead). The militia additions and the
/// church, legion, gloomrot and cursed groups are the faction wave templates' (design §9 D60), each checked by
/// tools/vrclient/scenarios/faction-mix-waves.vrs.
/// </summary>
public static class FactionGroups
{
    public static readonly IReadOnlyList<FactionGroup> All =
    [
        new("undead", "Faction_Undead",
        [
            "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane", "CHAR_Undead_Guardian",
            "CHAR_Undead_GhostMilitia_Light", "CHAR_Undead_GhostBanshee", "CHAR_Undead_BishopOfDeath_VBlood",
        ]),
        new("spiders", "Faction_Spiders",
            ["CHAR_Spider_Forest", "CHAR_Spider_Melee", "CHAR_Spider_Range", "CHAR_Spider_Forestling", "CHAR_Spider_Queen_VBlood"]),
        new("bandits", "Faction_Bandits", ["CHAR_Bandit_Thug", "CHAR_Bandit_Hunter", "CHAR_Bandit_Deadeye"]),
        new("militia", "Faction_Militia",
        [
            "CHAR_Militia_Crossbow", "CHAR_Militia_Light", "CHAR_Militia_Hound", "CHAR_Militia_Guard", "CHAR_Militia_Longbowman",
            "CHAR_Militia_Torchbearer", "CHAR_Militia_Heavy", "CHAR_Militia_Nun", "CHAR_Militia_Devoted", "CHAR_Militia_Bomber",
            "CHAR_Militia_Leader_VBlood",
        ]),
        new("church", "Faction_ChurchOfLum",
        [
            "CHAR_ChurchOfLight_Footman", "CHAR_ChurchOfLight_Archer", "CHAR_ChurchOfLight_Rifleman", "CHAR_ChurchOfLight_Cleric",
            "CHAR_ChurchOfLight_Knight_Shield", "CHAR_ChurchOfLight_Knight_2H", "CHAR_ChurchOfLight_Priest", "CHAR_ChurchOfLight_Paladin",
            "CHAR_ChurchOfLight_Lightweaver", "CHAR_ChurchOfLight_CardinalAide", "CHAR_ChurchOfLight_Paladin_VBlood",
            "CHAR_ChurchOfLight_Overseer_VBlood",
        ]),
        new("legion", "Faction_Legion",
        [
            "CHAR_Legion_Shadowkin", "CHAR_Legion_Assassin", "CHAR_Legion_Vargulf", "CHAR_Legion_BloodProphet", "CHAR_Legion_NightMaiden",
            "CHAR_Legion_Dreadhorn", "CHAR_Legion_Gargoyle", "CHAR_Legion_Nightmare", "CHAR_Legion_Guardian_DraculaMinion",
            "CHAR_Vampire_BloodKnight_VBlood",
        ]),
        new("gloomrot", "Faction_Gloomrot",
        [
            "CHAR_Gloomrot_Technician", "CHAR_Gloomrot_Tazer", "CHAR_Gloomrot_Railgunner", "CHAR_Gloomrot_Pyro", "CHAR_Gloomrot_Batoon",
            "CHAR_Gloomrot_SentryOfficer", "CHAR_Gloomrot_TractorBeamer", "CHAR_Gloomrot_SpiderTank_Driller", "CHAR_Gloomrot_Voltage_VBlood",
        ]),
        new("cursed", "Faction_Cursed",
        [
            "CHAR_Cursed_Wolf", "CHAR_Cursed_ToadSpitter", "CHAR_Cursed_MonsterToad", "CHAR_Cursed_Nightlurker",
            "CHAR_Cursed_Bear_Standard", "CHAR_Cursed_WormTerror", "CHAR_Cursed_Witch", "CHAR_Cursed_Wolf_Spirit", "CHAR_Cursed_Bear_Spirit",
            "CHAR_Cursed_Witch_VBlood",
        ]),
    ];

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

    /// <summary>A SpawnWaves template's problem under design §9 D60: one allied group as <see cref="Problem(string, IEnumerable{string}, string?)"/>
    /// says, or, for a template that mixes groups, null only when each wave is one group, every later wave waits for the
    /// last to be cleared with no afterSeconds (so two groups are never alive together), and its bosses are of the last
    /// wave's group (they spawn with it).</summary>
    public static string? Problem(string templateId, IReadOnlyList<WaveSpec> waves, IEnumerable<string> bosses)
    {
        var bossList = bosses.ToList();
        var whole = Problem(templateId, waves.SelectMany(w => w.Units.Select(u => u.Prefab)).Concat(bossList));
        if (whole is null || !whole.Contains(" mixes the ", StringComparison.Ordinal)) return whole;
        for (var i = 0; i < waves.Count; i++)
        {
            var wave = waves[i].Units.Select(u => u.Prefab).Concat(i == waves.Count - 1 ? bossList : []).ToList();
            if (Problem($"{templateId} wave {i + 1}", wave) is { } p) return p;
            if (i > 0 && (!waves[i].WhenCleared || waves[i].AfterSeconds is not null))
                return $"{templateId} mixes groups but wave {i + 1} does not wait for wave {i} to be cleared";
        }
        return null;
    }
}
