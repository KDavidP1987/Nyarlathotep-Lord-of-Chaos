using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Nyarlathotep.Spikes;

/// <summary>
/// Spike harness (docs/dod/spikes.md, removed in Build step 8). Spawns and marks throwaway units, keeps the
/// in-memory list for this boot, audits both sets (sweep) and destroys them through a staged queue (clear).
/// Every structural edit goes through the Prefab-refusing helpers in EntityExtensions.cs (spikes D3).
/// </summary>
internal static class SpikeUnits
{
    public const int MarkerLevel = 1314472274;              // ASCII "NYAR" (spikes S-4)
    public static readonly PrefabGUID MarkerBuff = new(-1954355403);   // AB_Consumable_PhysicalPowerPotion_T01_Buff
    public const int MaxAlive = 30;
    public const int MaxLifetime = 600;
    const int ClearPerBatch = 5;                             // at most 5 destroys in any one frame (DEV_REMINDERS #8)
    const float ClearBatchSeconds = 0.25f;                   // slow enough for a second `clear` to land mid-drain (D5)

    static readonly List<Entity> _spawned = new();
    static readonly Queue<Entity> _clearQueue = new();
    static bool _draining;

    public static bool Draining => _draining;
    public static int ClearLeft => _clearQueue.Count;

    /// <summary>Spawn one unit and, in the same frame, set position, LifeTime, DestroyWhenDisabled, no drops
    /// and the marker. Returns Entity.Null (and a reason) when any step fails; a half-set unit is destroyed.</summary>
    public static Entity Spawn(PrefabGUID unit, float3 position, int lifetime, out string error)
    {
        error = null;
        var entity = Core.ServerGameManager.InstantiateEntityImmediate(Entity.Null, unit);
        if (!entity.Exists()) { error = $"spawn of {unit._Value} returned no entity"; return Entity.Null; }

        if (entity.Has<Translation>()) entity.Write(new Translation { Value = position });
        if (entity.Has<LastTranslation>()) entity.Write(new LastTranslation { Value = position });

        if (!entity.AddComponentSafe<LifeTime>()) { error = "LifeTime could not be added"; entity.DestroySafe(); return Entity.Null; }
        entity.Write(new LifeTime { Duration = lifetime, EndAction = LifeTimeEndAction.Destroy });

        if (!entity.AddComponentSafe<DestroyWhenDisabled>()) { error = "DestroyWhenDisabled could not be added"; entity.DestroySafe(); return Entity.Null; }

        if (entity.Has<DropTableBuffer>()) Core.EntityManager.GetBuffer<DropTableBuffer>(entity).Clear();

        if (!TryMark(entity, out error)) { entity.DestroySafe(); return Entity.Null; }

        _spawned.Add(entity);
        Core.Log.LogInfo($"[nyar-spike] spawned {entity.Index}:{entity.Version} {unit.GetPrefabName()} lifetime {lifetime}s");
        return entity;
    }

    /// <summary>The marker: our own buff on the unit, made inert, living as long as the unit, carrying
    /// SpellLevel.Level = MarkerLevel so a query can find it after a restart (the S2 question).</summary>
    static bool TryMark(Entity unit, out string error)
    {
        error = null;
        if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(unit, unit, MarkerBuff, out Entity buff) || !buff.Exists())
        {
            error = "marker buff could not be applied";
            return false;
        }
        buff.RemoveComponentSafe<CreateGameplayEventsOnSpawn>();
        buff.RemoveComponentSafe<GameplayEventListeners>();
        buff.RemoveComponentSafe<RemoveBuffOnGameplayEvent>();
        buff.RemoveComponentSafe<RemoveBuffOnGameplayEventEntry>();
        buff.RemoveComponentSafe<DestroyOnGameplayEvent>();
        if (buff.Has<ModifyUnitStatBuff_DOTS>()) Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(buff).Clear();
        if (buff.Has<LifeTime>()) buff.Write(new LifeTime { Duration = 0f, EndAction = LifeTimeEndAction.None });
        if (!buff.AddComponentSafe<SpellLevel>()) { error = "SpellLevel could not be added to the marker"; return false; }
        buff.Write(new SpellLevel { Level = MarkerLevel });
        return true;
    }

    /// <summary>Units whose marker buff carries our level, from an IncludeDisabled | IncludeSpawnTag query (a buff made this frame still has SpawnTag) (Business rules 5).</summary>
    public static HashSet<Entity> MarkedUnits()
    {
        var result = new HashSet<Entity>();
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<Buff>()), ComponentType.ReadOnly(Il2CppType.Of<SpellLevel>()) },
            Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag
        });
        var buffs = query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var buff in buffs)
            {
                if (buff.Read<SpellLevel>().Level != MarkerLevel) continue;
                var target = buff.Read<Buff>().Target;
                if (target.Exists()) result.Add(target);
            }
        }
        finally
        {
            buffs.Dispose();
            query.Dispose();
        }
        return result;
    }

    public static HashSet<Entity> ListedUnits()
    {
        _spawned.RemoveAll(e => !e.Exists());
        return new HashSet<Entity>(_spawned);
    }

    public static int AliveCount()
    {
        var all = ListedUnits();
        all.UnionWith(MarkedUnits());
        return all.Count;
    }

    public static bool HasMarker(Entity unit) => MarkedUnits().Contains(unit);

    /// <summary>"Type: message" on one line, for every "[nyar-spike] spike failed:" log record (spikes D12).</summary>
    public static string OneLine(System.Exception ex) =>
        $"{ex.GetType().Name}: {ex.Message}".Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>The D5 audit: both sets, and for each unit the four checks.</summary>
    public static string Sweep()
    {
        var marked = MarkedUnits();
        var listed = ListedUnits();
        var all = new HashSet<Entity>(listed);
        all.UnionWith(marked);
        var faults = new List<string>();
        foreach (var e in all.OrderBy(x => x.Index))
        {
            var id = $"{e.Index}:{e.Version}";
            if (!marked.Contains(e)) faults.Add($"{id} no marker");
            if (!listed.Contains(e)) faults.Add($"{id} not listed");
            if (!e.TryGetComponent<LifeTime>(out var life)) faults.Add($"{id} no LifeTime");
            else if (life.Duration > MaxLifetime || life.EndAction != LifeTimeEndAction.Destroy) faults.Add($"{id} LifeTime {life.Duration:0}s {life.EndAction}");
            if (!e.Has<DestroyWhenDisabled>()) faults.Add($"{id} no DestroyWhenDisabled");
        }
        foreach (var f in faults) Core.Log.LogInfo($"[nyar-spike] sweep fault: {f}");
        var head = $"sweep: marked {marked.Count}, listed {listed.Count}, faults {faults.Count}";
        return faults.Count == 0 ? head : head + ": " + string.Join("; ", faults);
    }

    /// <summary>Queue every spike unit and drain at most 5 per batch. Returns the reply line.</summary>
    public static string Clear()
    {
        if (_draining) return $"clear in progress ({_clearQueue.Count} left)";
        SpikeMarch.StopAll();
        var all = ListedUnits();
        all.UnionWith(MarkedUnits());
        all.UnionWith(SpikeMarch.Anchors());
        if (all.Count == 0) return "nothing to clear";
        foreach (var e in all) _clearQueue.Enqueue(e);
        _draining = true;
        Core.StartCoroutine(Drain());
        return $"clear: {all.Count} queued, {ClearPerBatch} per batch";
    }

    static IEnumerator Drain()
    {
        int batch = 0;
        while (_clearQueue.Count > 0)
        {
            int destroyed = 0;
            for (int i = 0; i < ClearPerBatch && _clearQueue.Count > 0; i++)
            {
                var e = _clearQueue.Dequeue();
                try
                {
                    if (e.Exists() && e.DestroySafe()) destroyed++;
                }
                catch (System.Exception ex)
                {
                    Core.Log.LogWarning($"[nyar-spike] spike failed: clear {e.Index}:{e.Version}: {OneLine(ex)}");
                }
            }
            batch++;
            Core.Log.LogInfo($"[nyar-spike] clear batch {batch}: destroyed {destroyed}, {_clearQueue.Count} left");
            yield return new WaitForSeconds(ClearBatchSeconds);
        }
        _spawned.RemoveAll(e => !e.Exists());
        _draining = false;
        Core.Log.LogInfo("[nyar-spike] clear done");
    }
}
