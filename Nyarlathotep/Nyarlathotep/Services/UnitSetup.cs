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
    /// <summary>Make it fight, then the level, Health and PhysicalPower. Returns a short note of what changed, for the
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

        if (tuning.Health != 1f && unit.TryGetComponent<Health>(out var health))
        {
            health.MaxHealth._Value *= tuning.Health;
            health.Value = health.MaxHealth._Value;
            unit.Write(health);
        }

        if (tuning.Power != 1f && unit.TryGetComponent<UnitStats>(out var stats))
        {
            stats.PhysicalPower._Value *= tuning.Power;
            unit.Write(stats);
        }

        return $"level {(level < 0 ? "prefab" : level.ToString())}, hp x{tuning.Health:0.##}, power x{tuning.Power:0.##}";
    }
}
