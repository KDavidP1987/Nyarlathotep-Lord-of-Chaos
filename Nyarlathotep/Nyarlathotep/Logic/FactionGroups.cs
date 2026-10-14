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
/// killed its twin on spawn) and every Faction_Legion unit (Legion shadows attacked the undead).
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
        new("militia", "Faction_Militia", ["CHAR_Militia_Crossbow", "CHAR_Militia_Light"]),
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
}
