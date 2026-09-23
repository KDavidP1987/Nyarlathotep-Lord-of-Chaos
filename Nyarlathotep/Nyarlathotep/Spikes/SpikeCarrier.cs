using System.Collections.Generic;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Nyarlathotep.Spikes;

/// <summary>
/// Spike S3 (docs/features/FACTION_EMPOWERMENT.md): does a timed carrier buff empower a native NPC and revert on
/// its own? Native NPCs are changed only through the carrier, never by direct stat writes (spikes Business
/// rules 2). The carrier must be a buff prefab that already has LifeTime (DEV_REMINDERS #17).
/// </summary>
internal static class SpikeCarrier
{
    public static readonly PrefabGUID DefaultCarrier = new(-1591883586);   // AB_Consumable_PhysicalPowerPotion_T02_Buff
    static PrefabGUID _lastCarrier = DefaultCarrier;

    static readonly string[] DenyPrefixes = { "CHAR_Mount_Horse_Vampire", "CHAR_Mount_Horse_Gloomrot", "CarriagePrisonerRelease", "MicroPOI" };

    /// <summary>Null when the carrier is usable; otherwise the refusal reply.</summary>
    public static string CheckCarrier(PrefabGUID guid)
    {
        if (!Core.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(guid, out var prefab) || !prefab.Exists())
            return $"carrier {guid._Value}: unknown prefab";
        var name = guid.GetPrefabName();
        foreach (var p in DenyPrefixes)
            if (name.StartsWith(p)) return $"carrier {name}: on the do-not-spawn list";
        if (prefab.Has<DropInInventoryOnSpawn>()) return $"carrier {name}: on the do-not-spawn list (DropInInventoryOnSpawn)";
        if (!prefab.Has<Buff>()) return $"prefab {name} lacks Buff";
        if (!prefab.Has<LifeTime>()) return $"prefab {name} lacks LifeTime";
        return null;
    }

    public static string Empower(Entity admin, int seconds, int radius, PrefabGUID carrier)
    {
        var center = admin.Read<Translation>().Value;
        var targets = NativeNpcs(center, radius);
        if (targets.Count == 0) return $"empower: no native NPC within {radius} m";
        int applied = 0;
        foreach (var npc in targets)
        {
            if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(npc, npc, carrier, out Entity buff) || !buff.Exists())
            {
                Core.Log.LogWarning($"[nyar-spike] empower: carrier not applied to {npc.Index}:{npc.Version}");
                continue;
            }
            buff.RemoveComponentSafe<CreateGameplayEventsOnSpawn>();
            buff.RemoveComponentSafe<GameplayEventListeners>();
            buff.RemoveComponentSafe<RemoveBuffOnGameplayEvent>();
            buff.RemoveComponentSafe<RemoveBuffOnGameplayEventEntry>();
            buff.RemoveComponentSafe<DestroyOnGameplayEvent>();
            buff.Write(new LifeTime { Duration = seconds, EndAction = LifeTimeEndAction.Destroy });
            if (!buff.Has<ModifyUnitStatBuff_DOTS>() && !buff.AddBufferSafe<ModifyUnitStatBuff_DOTS>()) continue;
            var stats = Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(buff);
            stats.Clear();
            stats.Add(Modifier(UnitStatType.PhysicalPower, 0.5f));   // ×1.5
            stats.Add(Modifier(UnitStatType.MaxHealth, 1.0f));       // ×2
            applied++;
            Core.Log.LogInfo($"[nyar-spike] empowered {npc.Index}:{npc.Version} {npc.GetPrefabGuid().GetPrefabName()} with {carrier.GetPrefabName()} for {seconds}s");
        }
        _lastCarrier = carrier;
        return $"empower: {applied}/{targets.Count} native NPCs within {radius} m, {carrier.GetPrefabName()} for {seconds}s";
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

    public static string Inspect(Entity admin, int radius)
    {
        var center = admin.Read<Translation>().Value;
        Entity nearest = Entity.Null;
        float best = float.MaxValue;
        foreach (var npc in NativeNpcs(center, radius))
        {
            var d = math.distance(center, npc.Read<Translation>().Value);
            if (d < best) { best = d; nearest = npc; }
        }
        if (!nearest.Exists()) return $"inspect: no native NPC within {radius} m";

        var s = nearest.Read<UnitStats>();
        var h = nearest.Read<Health>();
        var carrier = FindCarrier(nearest, _lastCarrier);
        var carrierNote = "carrier none";
        if (carrier.Exists())
        {
            var life = carrier.Read<LifeTime>();
            var age = carrier.TryGetComponent<Age>(out var a) ? a.Value : 0f;
            carrierNote = $"carrier {_lastCarrier.GetPrefabName()} left {math.max(0f, life.Duration - age):0}s";
        }
        return $"inspect {nearest.Index}:{nearest.Version} {nearest.GetPrefabGuid().GetPrefabName()} {best:0.0} m: " +
               $"PhysicalPower {s.PhysicalPower._Value:0.##} MaxHealth {h.MaxHealth._Value:0.#} Health {h.Value:0.#} {carrierNote}";
    }

    static Entity FindCarrier(Entity target, PrefabGUID carrier)
    {
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<Buff>()), ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<LifeTime>()) },
            Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag
        });
        var buffs = query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var b in buffs)
                if (b.Read<Buff>().Target == target && b.Read<PrefabGUID>() == carrier) return b;
        }
        finally
        {
            buffs.Dispose();
            query.Dispose();
        }
        return Entity.Null;
    }

    /// <summary>Native NPC: not marked by us, not a player, not in Faction_Players*, not a V Blood, not a Prefab
    /// (the query excludes prefabs), not being destroyed.</summary>
    static List<Entity> NativeNpcs(float3 center, int radius)
    {
        var result = new List<Entity>();
        var marked = SpikeUnits.MarkedUnits();
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<FactionReference>()),
                ComponentType.ReadOnly(Il2CppType.Of<Health>()), ComponentType.ReadOnly(Il2CppType.Of<UnitStats>()),
                ComponentType.ReadOnly(Il2CppType.Of<Translation>()),
            },
            None = new[] { ComponentType.ReadOnly(Il2CppType.Of<PlayerCharacter>()), ComponentType.ReadOnly(Il2CppType.Of<VBloodUnit>()) },
        });
        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var e in entities)
            {
                if (marked.Contains(e) || e.Has<DestroyTag>()) continue;
                if (math.distance(center, e.Read<Translation>().Value) > radius) continue;
                var faction = e.Read<FactionReference>().FactionGuid._Value;
                if (faction.GetPrefabName().StartsWith("Faction_Players")) continue;
                result.Add(e);
            }
        }
        finally
        {
            entities.Dispose();
            query.Dispose();
        }
        return result;
    }
}
