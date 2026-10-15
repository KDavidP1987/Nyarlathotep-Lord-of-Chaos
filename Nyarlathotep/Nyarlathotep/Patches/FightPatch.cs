using System;
using HarmonyLib;
using Nyarlathotep.Services;
using ProjectM;
using ProjectM.Gameplay.Systems;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Patches;

/// <summary>
/// The fight read of unit watch (unit-watch D4, D18; the spike's verdict, docs/features/UNIT_WATCH.md › Spike): after the
/// game applies a frame's stat changes, each row with the DealDamageSystem reason is one hit, its Entity the one hit and
/// its Source the attacker. A hit between a unit SpawnTracker tracks and a player (the player character itself, or an
/// entity a player owns, such as a projectile, up to three owner steps) is that unit's fight, kept as its last fight time
/// in the watch's FightBook; a hit between NPCs or on a structure is not. Read-only: no entity is written, and nothing of a
/// player is kept. A postfix, so the system's own work has already run; it returns at once when [UnitWatch] Enabled is
/// false, no event runs or nothing is tracked, and Logic/FightRead guards every row, so a throw never leaves the patch and
/// is logged once per streak (D18).
/// </summary>
[HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.ApplyStatChanges))]
internal static class FightPatch
{
    static readonly Logic.FailureStreak Faults = new();

    [HarmonyPostfix]
    public static void ApplyStatChanges(NativeArray<StatChangeEvent> statChanges)
    {
        if (!Core.IsReady) return;
        try
        {
            if (!SpawnTracker.WatchesFights || statChanges.Length == 0) return;
            var rows = statChanges;
            SpawnTracker.FightBatch(rows.Length, i => FightOf(rows[i]));
            Faults.Ok();
        }
        catch (Exception ex)
        {
            // FightBatch never throws; this catches the gate and the batch set-up, once per streak (D18)
            if (Faults.Fail()) Core.Log.LogError($"[nyar] {Logic.UnitWatchLines.ReadFailed($"fight: {ex.Message}")}");
        }
    }

    /// <summary>The tracked unit in a fight with a player in this row, or null: a hit on a tracked unit by a player, or a
    /// hit on a player by a tracked unit (or by an entity a tracked unit owns). Ledger lookups first, component reads only
    /// when a tracked unit or a player is in the row.</summary>
    static long? FightOf(StatChangeEvent row)
    {
        if (row.Reason != StatChangeReason.DealDamageSystem_0) return null;
        var hit = row.Entity;
        var hitKey = SpawnTracker.KeyFor(hit);
        if (SpawnTracker.IsTracked(hitKey)) return ByPlayer(row.Source) ? hitKey : null;
        if (!hit.Exists() || !hit.Has<PlayerCharacter>()) return null;
        return TrackedOf(row.Source);
    }

    /// <summary>The source is a player character or an entity a player owns, up to three owner links (Logic/OwnerChain).</summary>
    static bool ByPlayer(Entity source) =>
        Logic.OwnerChain.Find(source, e => e.Exists(), e => e.Has<PlayerCharacter>() ? true : null, Owner) is not null;

    /// <summary>The tracked unit that is the source or owns it, up to three owner links, or null (a player's own entity is
    /// none).</summary>
    static long? TrackedOf(Entity source) =>
        Logic.OwnerChain.Find(source, e => e.Exists(), e => SpawnTracker.IsTracked(SpawnTracker.KeyFor(e)) ? true : e.Has<PlayerCharacter>() ? false : null, Owner)
            is { } unit ? SpawnTracker.KeyFor(unit) : null;

    static bool Owner(Entity entity, out Entity owner)
    {
        var has = entity.TryGetComponent<EntityOwner>(out var o);
        owner = has ? o.Owner : default;
        return has;
    }
}
