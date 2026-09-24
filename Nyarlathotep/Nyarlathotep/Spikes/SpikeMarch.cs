using System.Collections;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Behaviours;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Nyarlathotep.Spikes;

/// <summary>
/// Spike S1 (docs/features/SIEGES.md): can units be made to walk to a point? Spawns CHAR_Bandit_Thug north of
/// the admin and tries one of three levers; a 1 Hz mover logs each unit's distance and behaviour state.
/// The anchor for variants 1 and 2 is a CHAR_Critter_Rat held still by zeroed AiMoveSpeeds (a spike unit
/// like the others, so sweep and clear see it).
/// </summary>
internal static class SpikeMarch
{
    public static readonly PrefabGUID Thug = new(-301730941);       // CHAR_Bandit_Thug (spikes S-5)
    public static readonly PrefabGUID AnchorUnit = new(-2072914343); // CHAR_Critter_Rat
    const int MarchLifetime = 600;
    const float AnchorStepMeters = 10f;                           // variant 2: 10 m per second
    const float ArrivedMeters = 5f;
    const int MaxSeconds = 180;

    sealed class Group
    {
        public int Id;
        public int Variant;
        public float3 Destination;
        public Entity Anchor;
        public Entity Target;      // variant 4: the admin
        public int Distance;
        public readonly List<Entity> Units = new();
    }

    static readonly List<Group> _groups = new();
    static int _nextId;
    static bool _stopRequested;

    public static IEnumerable<Entity> Anchors()
    {
        foreach (var g in _groups) if (g.Anchor.Exists()) yield return g.Anchor;
    }

    /// <summary>Stop every mover before clear queues its destroys (Design › States).</summary>
    public static void StopAll()
    {
        if (_groups.Count == 0) return;
        _stopRequested = true;
        Core.Log.LogInfo($"[nyar-spike] march movers stopped ({_groups.Count} group(s))");
        _groups.Clear();
    }

    public static string Start(Entity admin, int variant, int count, int distance)
    {
        var destination = admin.Read<Translation>().Value;
        var spawnPoint = destination + new float3(0, 0, distance);
        var group = new Group { Id = ++_nextId, Variant = variant, Destination = destination, Target = admin, Distance = distance };

        if (variant is 1 or 2)
        {
            var anchorAt = variant == 1 ? destination : spawnPoint;
            group.Anchor = SpikeUnits.Spawn(AnchorUnit, anchorAt, MarchLifetime, out var err);
            if (!group.Anchor.Exists()) return $"spike failed: anchor: {err}";
            HoldStill(group.Anchor);
            KeepEnabled(group.Anchor);
            Core.Log.LogInfo($"[nyar-spike] march g{group.Id}: anchor {group.Anchor.Index}:{group.Anchor.Version} held still");
        }

        // Spawn the whole group first, then apply the lever, so no unit is spawned while another already
        // follows the anchor (session 1 aborted inside `march 2` between the first and second spawn).
        for (int i = 0; i < count; i++)
        {
            var offset = new float3((i % 5 - 2) * 1.5f, 0, (i / 5) * 1.5f);
            var unit = SpikeUnits.Spawn(Thug, spawnPoint + offset, MarchLifetime, out var err);
            if (!unit.Exists()) { Core.Log.LogWarning($"[nyar-spike] march unit {i} not spawned: {err}"); continue; }
            group.Units.Add(unit);
            KeepEnabled(unit);
        }
        Core.Log.LogInfo($"[nyar-spike] march g{group.Id}: {group.Units.Count} spawned, applying variant {variant}");
        foreach (var unit in group.Units)
        {
            if (!unit.Exists()) { Core.Log.LogWarning($"[nyar-spike] march g{group.Id}: {unit.Index}:{unit.Version} gone before the lever"); continue; }
            var set = variant switch
            {
                1 or 2 => FollowAnchor(unit, group.Anchor),
                3 => LeashToDestination(unit, destination, distance),
                _ => Hunt(unit, admin, distance),
            };
            if (set) Core.Log.LogInfo($"[nyar-spike] march g{group.Id}: variant {variant} set on {unit.Index}:{unit.Version}");
        }

        _stopRequested = false;
        _groups.Add(group);
        Core.StartCoroutine(Mover(group));
        var anchorNote = group.Anchor.Exists() ? $", anchor {group.Anchor.Index}:{group.Anchor.Version} {AnchorUnit._Value}" : "";
        return $"march g{group.Id} variant {variant}: {group.Units.Count} units {distance} m north{anchorNote}";
    }

    /// <summary>Session 2: units spawned 100 m from any player were disabled, and DestroyWhenDisabled removed them
    /// within 5 s. Marching units stay enabled (DEV_REMINDERS #14; Bloodcraft FamiliarBindingSystem.cs:602);
    /// LifeTime still bounds them.</summary>
    internal static void KeepEnabled(Entity unit)
    {
        if (!unit.Has<CanPreventDisableWhenNoPlayersInRange>() && !unit.AddComponentSafe<CanPreventDisableWhenNoPlayersInRange>()) return;
        unit.Write(new CanPreventDisableWhenNoPlayersInRange { CanDisable = new ModifiableBool(false) });
    }

    static void HoldStill(Entity anchor)
    {
        if (!anchor.TryGetComponent<AiMoveSpeeds>(out var speeds)) return;
        speeds.Walk._Value = 0f;
        speeds.Run._Value = 0f;
        speeds.Circle._Value = 0f;
        speeds.Return._Value = 0f;
        anchor.Write(speeds);
    }

    static bool FollowAnchor(Entity unit, Entity anchor)
    {
        if (!anchor.Exists()) { Core.Log.LogWarning($"[nyar-spike] anchor gone before {unit.Index} could follow it"); return false; }
        if (!unit.TryGetComponent<Follower>(out var follower)) { Core.Log.LogWarning($"[nyar-spike] {unit.Index} has no Follower"); return false; }
        follower.Followed._Value = anchor;
        follower.ModeModifiable._Value = 0;   // as Bloodcraft FamiliarBindingSystem.cs:453-457 does for a set Followed
        unit.Write(follower);
        return true;
    }

    static bool LeashToDestination(Entity unit, float3 destination, int distance)
    {
        if (!unit.TryGetComponent<AggroConsumer>(out var aggro)) { Core.Log.LogWarning($"[nyar-spike] {unit.Index} has no AggroConsumer"); return false; }
        aggro.PreCombatPosition = destination;
        aggro.MaxDistanceFromPreCombatPosition = distance + 50;
        aggro.ProximityRadius = distance + 50;
        unit.Write(aggro);
        // Variant 3 sends the unit "home", where home is now the destination.
        if (unit.TryGetComponent<BehaviourTreeState>(out var state))
        {
            state.Value = GenericEnemyState.Return;
            unit.Write(state);
        }
        return true;
    }

    /// <summary>Variant 4 (spikes A5): an aggro chase onto the admin. The aggro and leash ranges are widened to
    /// cover the distance (TideOfWar SpawnForWar/Core.cs:389-412) and the admin is put in the unit's AggroBuffer
    /// (Bloodcraft Utilities/Familiars.cs:826-859), so the game's own combat AI walks the unit there.</summary>
    static bool Hunt(Entity unit, Entity target, int distance)
    {
        if (!target.Exists()) { Core.Log.LogWarning($"[nyar-spike] hunt target gone before {unit.Index}"); return false; }
        if (!unit.TryGetComponent<AggroConsumer>(out var aggro)) { Core.Log.LogWarning($"[nyar-spike] {unit.Index} has no AggroConsumer"); return false; }
        var reach = distance + 50;
        aggro.MaxDistanceFromPreCombatPosition = reach;
        aggro.ProximityRadius = reach;
        aggro.Active._Value = true;
        unit.Write(aggro);
        if (unit.TryGetComponent<AggroModifiers>(out var modifiers))
        {
            modifiers.CircleRadiusFactor = new ModifiableFloat(reach);
            modifiers.ConeRadiusFactor = new ModifiableFloat(reach);
            unit.Write(modifiers);
        }
        if (!Core.EntityManager.HasComponent<AggroBuffer>(unit)) { Core.Log.LogWarning($"[nyar-spike] {unit.Index} has no AggroBuffer"); return false; }
        var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
        for (int i = 0; i < buffer.Length; i++)
            if (buffer[i].Entity == target) return true;   // re-applied by the mover (A6): one entry per target
        buffer.Add(new AggroBuffer { Entity = target, DamageValue = 500f, Weight = 1f });
        return true;
    }

    static void DropTarget(Entity unit, Entity target)
    {
        if (!Core.EntityManager.HasComponent<AggroBuffer>(unit)) return;
        var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
        for (int i = buffer.Length - 1; i >= 0; i--)
            if (buffer[i].Entity == target) buffer.RemoveAt(i);
    }

    /// <summary>Spikes A6: at 100 m variant 4 units stayed Idle while at 30 m they chased. One line per second
    /// for the group's first unit tells apart "our ranges were reset after spawn" from "the aggro entry was
    /// pruned": the live AggroConsumer and AggroModifiers values and whether the target is still in AggroBuffer.</summary>
    static string HuntProbe(Entity unit, Entity target)
    {
        var a = unit.TryGetComponent<AggroConsumer>(out var aggro)
            ? $"prox={aggro.ProximityRadius:0} leash={aggro.MaxDistanceFromPreCombatPosition:0} active={aggro.Active._Value}"
            : "no AggroConsumer";
        var m = unit.TryGetComponent<AggroModifiers>(out var mods)
            ? $"circle={mods.CircleRadiusFactor._Value:0.##} cone={mods.ConeRadiusFactor._Value:0.##}"
            : "no AggroModifiers";
        int entries = 0;
        bool hasTarget = false;
        if (Core.EntityManager.HasComponent<AggroBuffer>(unit))
        {
            var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
            entries = buffer.Length;
            for (int i = 0; i < buffer.Length; i++) if (buffer[i].Entity == target) hasTarget = true;
        }
        return $"{unit.Index} {a} {m} aggro entries {entries} target {(hasTarget ? "yes" : "no")}";
    }

    static IEnumerator Mover(Group group)
    {
        for (int second = 1; second <= MaxSeconds; second++)
        {
            yield return new WaitForSeconds(1f);
            if (_stopRequested || !_groups.Contains(group)) yield break;
            try
            {
                if (group.Variant == 2 && group.Anchor.Exists())
                {
                    var at = group.Anchor.Read<Translation>().Value;
                    var toGo = group.Destination - at;
                    var len = math.length(toGo);
                    var next = len <= AnchorStepMeters ? group.Destination : at + toGo / len * AnchorStepMeters;
                    group.Anchor.Write(new Translation { Value = next });
                    if (group.Anchor.Has<LastTranslation>()) group.Anchor.Write(new LastTranslation { Value = next });
                }

                int alive = 0, arrived = 0;
                var parts = new List<string>();
                foreach (var u in group.Units)
                {
                    if (!u.Exists()) continue;
                    alive++;
                    var d = math.distance(u.Read<Translation>().Value, group.Destination);
                    if (d <= ArrivedMeters) arrived++;
                    var st = u.TryGetComponent<BehaviourTreeState>(out var s) ? s.Value.ToString() : "-";
                    parts.Add($"{u.Index} d={d:0.0} {st}");
                }
                Core.Log.LogInfo($"[nyar-spike] march g{group.Id} t={second}s alive {alive} arrived {arrived}: {string.Join(", ", parts)}");

                // Variant 4 (A6, A7): probe the first living unit, then re-apply the hunt to every unit. Session 6
                // showed the game prunes a target beyond about 86 m while the unit stays in Combat, so the
                // re-apply keys on the missing AggroBuffer entry (Hunt adds one only when absent), not on the state.
                if (group.Variant == 4)
                {
                    foreach (var u in group.Units)
                    {
                        if (!u.Exists()) continue;
                        Core.Log.LogInfo($"[nyar-spike] march g{group.Id} t={second}s probe {HuntProbe(u, group.Target)}");
                        break;
                    }
                    if (group.Target.Exists())
                    {
                        foreach (var u in group.Units)
                            if (u.Exists()) Hunt(u, group.Target, group.Distance);
                    }
                    else
                    {
                        // Step 3 Codex round 2: the admin entity is gone, so drop our entry for it and stop hunting.
                        foreach (var u in group.Units)
                            if (u.Exists()) DropTarget(u, group.Target);
                        Core.Log.LogInfo($"[nyar-spike] march g{group.Id}: target gone, hunt stopped");
                        _groups.Remove(group);
                        yield break;
                    }
                }
                if (alive == 0 || arrived == alive)
                {
                    Core.Log.LogInfo($"[nyar-spike] march g{group.Id} done after {second}s ({arrived}/{alive} arrived)");
                    _groups.Remove(group);
                    yield break;
                }
            }
            catch (System.Exception ex)
            {
                Core.Log.LogWarning($"[nyar-spike] spike failed: march mover g{group.Id}: {SpikeUnits.OneLine(ex)}");
                _groups.Remove(group);
                yield break;
            }
        }
        Core.Log.LogInfo($"[nyar-spike] march g{group.Id} timed out after {MaxSeconds}s");
        _groups.Remove(group);
    }
}
