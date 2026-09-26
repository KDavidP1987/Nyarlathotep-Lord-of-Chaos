#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>What a SpellLevel value found on one of our buffs means (faction-empowerment D7).</summary>
public enum MarkerKind { None, Unit, Carrier }

/// <summary>The registered marker values (foundation Design › Data › Marker values, Business rules 7). A unit marker is
/// an inert AB_Consumable_PhysicalPowerPotion_T01_Buff on a unit we spawned; a carrier is the T02 buff that empowers a
/// native NPC (faction-empowerment D4). Each holds one of these values in SpellLevel.Level, so the boot sweep finds it
/// after a restart (spikes S2). Completeness is claimed over this list only.</summary>
public static class Markers
{
    /// <summary>AB_Consumable_PhysicalPowerPotion_T01_Buff, the marker buff prefab.</summary>
    public const int MarkerBuffGuid = -1954355403;

    /// <summary>AB_Consumable_PhysicalPowerPotion_T02_Buff, the empowerment carrier prefab (spikes S3).</summary>
    public const int CarrierBuffGuid = -1591883586;

    /// <summary>ASCII "NYAR": every unit SpawnTracker spawns (spikes S-4).</summary>
    public const int Unit = 1314472274;

    /// <summary>ASCII "NYAE": every empowerment carrier on a native NPC (faction-empowerment D7).</summary>
    public const int Carrier = 1313952069;

    /// <summary>Every value the boot sweep acts on: a Unit row despawns its unit, a Carrier row removes only its buff
    /// (SweepPlan).</summary>
    public static readonly IReadOnlyList<int> All = [Unit, Carrier];

    /// <summary>SpellLevel.Level is a float, so a marker is written as the float nearest its value and compared the
    /// same way (as the spikes did). At this magnitude a float step is 128 and the two values are 520 205 apart, so
    /// neither collides with the other or with a game SpellLevel, whose levels are small integers.</summary>
    public static MarkerKind KindOf(float spellLevel) =>
        (float)Unit == spellLevel ? MarkerKind.Unit
        : (float)Carrier == spellLevel ? MarkerKind.Carrier
        : MarkerKind.None;

    /// <summary>True for a unit marker only. A carrier sits on a native NPC, which is never ours to despawn, so it is
    /// not matched here (the boot sweep's despawn filter).</summary>
    public static bool IsOurs(float spellLevel) => KindOf(spellLevel) == MarkerKind.Unit;
}
