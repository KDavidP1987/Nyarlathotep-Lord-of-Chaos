#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The registered marker values (foundation Design › Data › Marker values, Business rules 7). A marker is
/// an inert AB_Consumable_PhysicalPowerPotion_T01_Buff on the unit whose SpellLevel.Level holds one of these values,
/// so the boot sweep finds our units after a restart (spikes S2). Completeness is claimed over this list only.</summary>
public static class Markers
{
    /// <summary>AB_Consumable_PhysicalPowerPotion_T01_Buff, the marker buff prefab.</summary>
    public const int MarkerBuffGuid = -1954355403;

    /// <summary>ASCII "NYAR": every unit SpawnTracker spawns (spikes S-4).</summary>
    public const int Unit = 1314472274;

    /// <summary>Every value the boot sweep removes. The faction-empowerment child adds its carrier value here.</summary>
    public static readonly IReadOnlyList<int> All = [Unit];

    /// <summary>SpellLevel.Level is a float, so a marker is written as the float nearest its value and compared the
    /// same way (as the spikes did). At this magnitude a float step is 128, so a game SpellLevel would have to be
    /// above 1.3 billion to collide; the game's own levels are small integers.</summary>
    public static bool IsOurs(float spellLevel) => All.Any(v => (float)v == spellLevel);
}
