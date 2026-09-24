using Nyarlathotep.Logic;
using ProjectM;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// Tunes a unit SpawnTracker has just spawned (foundation D27). Direct stat writes are allowed here only because the
/// unit is ours and will be despawned (CLAUDE.md › Spawn &amp; buff safety); SpawnTracker is the only caller.
/// </summary>
internal static class UnitSetup
{
    /// <summary>Make it fight, then the level. Health and PhysicalPower ride on the marker buff (<see cref="StatModifiers"/>),
    /// because the game recalculates both from the level after a spawn (A7). Returns a short note of what changed, for the
    /// verbose log. [Mutating], so a call from anywhere but the dispatched services fails the gateway check.</summary>
    [Mutating]
    internal static string Apply(Entity unit, UnitTuning tuning)
    {
        // Make it fight: never disabled away from players (so DestroyWhenDisabled does not remove it within seconds,
        // spikes S2), and its aggro switched on.
        if (unit.AddComponentSafe<CanPreventDisableWhenNoPlayersInRange>())
            unit.Write(new CanPreventDisableWhenNoPlayersInRange { CanDisable = new ModifiableBool(false) });
        if (unit.TryGetComponent<AggroConsumer>(out var aggro))
        {
            aggro.Active = new ModifiableBool(true);
            unit.Write(aggro);
        }

        var level = -1;
        if (tuning.Level is { } wanted && unit.TryGetComponent<UnitLevel>(out var unitLevel))
        {
            level = wanted.Resolve(unitLevel.Level._Value);
            unitLevel.Level._Value = level;
            unit.Write(unitLevel);
        }

        return $"level {(level < 0 ? "prefab" : level.ToString())}, hp x{tuning.Health:0.##}, power x{tuning.Power:0.##}";
    }

    /// <summary>Writes the Health and PhysicalPower multipliers into <paramref name="markerBuff"/>'s stat modifiers
    /// (MultiplyBaseAdd, value multiplier - 1), replacing the buff prefab's own. The modifiers live and die with the
    /// unit's marker, the mechanism spikes S3 proved on the carrier (A7). False when the buffer is missing and cannot
    /// be added.</summary>
    [Mutating]
    internal static bool StatModifiers(Entity markerBuff, UnitTuning tuning)
    {
        if (!markerBuff.Has<ModifyUnitStatBuff_DOTS>() && !markerBuff.AddBufferSafe<ModifyUnitStatBuff_DOTS>()) return false;
        var mods = Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(markerBuff);
        mods.Clear();
        if (tuning.Health != 1f) mods.Add(Modifier(UnitStatType.MaxHealth, tuning.Health - 1f));
        if (tuning.Power != 1f) mods.Add(Modifier(UnitStatType.PhysicalPower, tuning.Power - 1f));
        return true;
    }

    static ModifyUnitStatBuff_DOTS Modifier(UnitStatType stat, float value) => new()
    {
        StatType = stat,
        ModificationType = ModificationType.MultiplyBaseAdd,
        Value = value,
        Modifier = 1,
        IncreaseByStacks = false,
        ValueByStacks = 0,
        Priority = 0,
        Id = ModificationIDs.Create().NewModificationId(),
    };
}
