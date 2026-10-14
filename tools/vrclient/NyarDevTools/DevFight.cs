using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Tiles;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NyarDevTools;

// DEV-ONLY (unit-watch step 1, the spike, D19). Never shipped, never referenced by the mod.
//   .devfight <seconds> [radius]  for that long, every candidate fight signal touching a living non-player unit within radius
//                          (default 40) of where the sender stood is logged, at most 3 lines a second per candidate:
//                          [DEV FIGHT] <candidate> unit=<prefab> hit=<player|npc|-> by=<player|npc|-> ground=<grounded|free|none|err> level=<n>
//                          candidates: deal (DealDamageSystem.DealDamage, DealDamageEvent Target/SpellSource),
//                          taken (StatChangeSystem.OnUpdate, DamageTakenEvent Entity/Source), apply (StatChangeSystem.ApplyStatChanges,
//                          StatChangeEvent Entity/Source, the DealDamageSystem reason), health (the unit's Health.Value fell between
//                          one-second samples). At the end: [DEV FIGHT] done deal=<n> taken=<n> apply=<n> health=<n>
//                          cost deal=<ms> taken=<ms> apply=<ms> (time inside each patch, averaged over the calls it ran)
//   .devstrand <prefabGuid> under <metres> [radius]  sinks the nearest living unit of that prefab within radius (default 30) of
//                          the sender by that many metres (Translation, LastTranslation); log [DEV STRAND] moved=<n> x y z ground=…
//   .devstrand <prefabGuid> mark <name> [radius]  puts it on a .devmark spot (a rock top); same log
//   .devground [mark]     the walk check's reading at the sender or the mark: [DEV GROUND] x y z level=<n> ground=…
static class Fight
{
    internal static bool Active => DateTime.UtcNow < _until;

    internal static EntityManager ServerEm
    {
        get
        {
            foreach (var w in World.s_AllWorlds) if (w.Name == "Server") return w.EntityManager;
            throw new InvalidOperationException("no Server world");
        }
    }
    static DateTime _until;
    static float3 _centre;
    static float _radius;
    static DateTime _finishAt = DateTime.MaxValue;
    static readonly Dictionary<string, int> Counts = new();
    static readonly Dictionary<string, (double ms, int ticks)> Cost = new();
    static readonly Dictionary<string, (long second, int lines)> Rate = new();
    static readonly Dictionary<Entity, float> LastHealth = new();
    static DateTime _nextSample;

    internal static void Start(float3 centre, float seconds, float radius)
    {
        _centre = centre; _radius = radius; _until = DateTime.UtcNow.AddSeconds(seconds);
        _finishAt = _until;
        Counts.Clear(); Cost.Clear(); Rate.Clear(); LastHealth.Clear();
    }

    /// <summary>Called from a patch on every server tick: writes the summary once the window has closed.</summary>
    internal static void MaybeFinish()
    {
        if (DateTime.UtcNow < _finishAt) return;
        _finishAt = DateTime.MaxValue;
        string C(string k) => Counts.TryGetValue(k, out var n) ? n.ToString(CultureInfo.InvariantCulture) : "0";
        string M(string k) => Cost.TryGetValue(k, out var c) && c.ticks > 0 ? (c.ms / c.ticks).ToString("0.000", CultureInfo.InvariantCulture) : "-";
        Plugin.Logger.LogInfo($"[DEV FIGHT] done deal={C("deal")} taken={C("taken")} apply={C("apply")} health={C("health")} cost deal={M("deal")} taken={M("taken")} apply={M("apply")}");
    }

    internal static void AddCost(string k, double ms)
    {
        var c = Cost.TryGetValue(k, out var v) ? v : (0, 0);
        Cost[k] = (c.ms + ms, c.ticks + 1);
    }

    static bool Near(EntityManager em, Entity u) =>
        em.Exists(u) && em.HasComponent<Translation>(u) && math.distance(em.GetComponentData<Translation>(u).Value.xz, _centre.xz) <= _radius;

    internal static bool IsUnit(EntityManager em, Entity e) =>
        em.Exists(e) && em.HasComponent<Health>(e) && em.HasComponent<UnitLevel>(e) && !em.HasComponent<PlayerCharacter>(e);

    /// <summary>player, npc or - for an entity that hit or was hit: a player character, else its owner chain (a spell's owner).</summary>
    internal static string Kind(EntityManager em, Entity e)
    {
        for (var i = 0; i < 3 && em.Exists(e); i++)
        {
            if (em.HasComponent<PlayerCharacter>(e)) return "player";
            if (em.HasComponent<UnitLevel>(e)) return "npc";
            if (!em.HasComponent<EntityOwner>(e)) break;
            e = em.GetComponentData<EntityOwner>(e).Owner;
        }
        return "-";
    }

    /// <summary>The unit end of a source: the source itself or its owner chain, when that is a non-player unit.</summary>
    internal static Entity UnitOf(EntityManager em, Entity e)
    {
        for (var i = 0; i < 3 && em.Exists(e); i++)
        {
            if (em.HasComponent<PlayerCharacter>(e)) return Entity.Null;
            if (IsUnit(em, e)) return e;
            if (!em.HasComponent<EntityOwner>(e)) break;
            e = em.GetComponentData<EntityOwner>(e).Owner;
        }
        return Entity.Null;
    }

    internal static void Signal(EntityManager em, string candidate, Entity target, Entity source)
    {
        var unit = IsUnit(em, target) ? target : UnitOf(em, source);
        if (unit == Entity.Null || !Near(em, unit)) return;
        Counts[candidate] = (Counts.TryGetValue(candidate, out var n) ? n : 0) + 1;
        var second = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        (long second, int lines) r = Rate.TryGetValue(candidate, out var rv) ? rv : (0L, 0);
        if (r.second != second) r = (second, 0);
        if (r.lines >= 3) { Rate[candidate] = r; return; }
        Rate[candidate] = (second, r.lines + 1);
        var hit = unit == target ? "npc" : Kind(em, target);
        var by = unit == target ? (source == Entity.Null ? "-" : Kind(em, source)) : "npc";
        var at = em.GetComponentData<Translation>(unit).Value;
        var (ground, level) = Ground.Read(em, at);
        Plugin.Logger.LogInfo($"[DEV FIGHT] {candidate} unit={em.GetComponentData<PrefabGUID>(unit).GuidHash} hit={hit} by={by} ground={ground} level={level}");
    }

    /// <summary>The health candidate: once a second, units within the radius whose Health.Value fell since the last sample.</summary>
    internal static void Sample(EntityManager em)
    {
        if (DateTime.UtcNow < _nextSample) return;
        _nextSample = DateTime.UtcNow.AddSeconds(1);
        var q = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<Health>(), ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<UnitLevel>() },
            None = new[] { ComponentType.ReadOnly<PlayerCharacter>(), ComponentType.ReadOnly<Dead>() },
        });
        var units = q.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var u in units)
            {
                if (!Near(em, u)) continue;
                var h = em.GetComponentData<Health>(u).Value;
                if (LastHealth.TryGetValue(u, out var before) && h < before - 0.01f) Signal(em, "health", u, Entity.Null);
                LastHealth[u] = h;
            }
        }
        finally { units.Dispose(); q.Dispose(); }
    }
}

static class Ground
{
    const float UnitRadius = 0.5f;
    static int TileIndex(float v) => (int)math.floor(v * 2) + 6400;

    static TileWorld? Live(EntityManager em)
    {
        var q = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<TileWorldSingleton>()) },
            Options = EntityQueryOptions.IncludeSystems,
        });
        try
        {
            var es = q.ToEntityArray(Allocator.Temp);
            try { return es.Length == 0 ? null : em.GetComponentData<TileWorldSingleton>(es[0]).GetTileWorld(); }
            finally { es.Dispose(); }
        }
        finally { q.Dispose(); }
    }

    /// <summary>grounded (grounded and free at the unit's level or a neighbour), free (free somewhere but not grounded),
    /// none, or err.</summary>
    internal static (string ground, string level) Read(EntityManager em, float3 at)
    {
        try
        {
            if (Live(em) is not { } world) return ("err", "-");
            var level = TileLayerUtility.GetHeightLevel(at.y);
            var polygons = TileCollisionHelper.CreateLinePolygon();
            try
            {
                var map = TileCollisionHelper.CreateMapData(polygons, world);
                var anyFree = false;
                foreach (var l in new[] { level, (byte)Math.Max(0, level - 1), (byte)Math.Min(255, level + 1) })
                {
                    var free = !TileMapCollisionMath.CheckStaticCircle(ref map, new float2(at.x, at.z), l, UnitRadius, MapCollisionFlags.CollideNormalMovement);
                    var grounded = world.GetIsGrounded(new int2(TileIndex(at.x), TileIndex(at.z)), l);
                    if (free && grounded) return ("grounded", level.ToString(CultureInfo.InvariantCulture));
                    anyFree |= free;
                }
                return (anyFree ? "free" : "none", level.ToString(CultureInfo.InvariantCulture));
            }
            finally { polygons.Dispose(); }
        }
        catch (Exception e) { return ("err:" + e.GetType().Name, "-"); }
    }
}

[HarmonyPatch]
static class FightPatches
{
    [HarmonyPatch(typeof(DealDamageSystem), nameof(DealDamageSystem.DealDamage))]
    [HarmonyPostfix]
    static void Deal(DealDamageSystem __instance)
    {
        if (!Fight.Active) return;
        var sw = Stopwatch.StartNew();
        try
        {
            var em = Fight.ServerEm;
            var events = __instance._Query.ToComponentDataArray<DealDamageEvent>(Allocator.Temp);
            try { foreach (var e in events) Fight.Signal(em, "deal", e.Target, e.SpellSource); }
            finally { events.Dispose(); }
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"[DEV FIGHT] deal failed: {e.GetType().Name}: {e.Message}"); }
        Fight.AddCost("deal", sw.Elapsed.TotalMilliseconds);
    }

    [HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.OnUpdate))]
    [HarmonyPrefix]
    static void Taken(StatChangeSystem __instance)
    {
        Fight.MaybeFinish();
        if (!Fight.Active) return;
        var sw = Stopwatch.StartNew();
        try
        {
            var em = __instance.EntityManager;
            Fight.Sample(em);
            var events = __instance._DamageTakenEventQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var ev in events)
                {
                    var d = em.GetComponentData<DamageTakenEvent>(ev);
                    Fight.Signal(em, "taken", d.Entity, d.Source);
                }
            }
            finally { events.Dispose(); }
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"[DEV FIGHT] taken failed: {e.GetType().Name}: {e.Message}"); }
        Fight.AddCost("taken", sw.Elapsed.TotalMilliseconds);
    }

    [HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.ApplyStatChanges))]
    [HarmonyPostfix]
    static void Apply(StatChangeSystem __instance, NativeArray<StatChangeEvent> statChanges)
    {
        if (!Fight.Active) return;
        var sw = Stopwatch.StartNew();
        try
        {
            var em = __instance.EntityManager;
            for (var i = 0; i < statChanges.Length; i++)
            {
                var s = statChanges[i];
                if (s.Reason != StatChangeReason.DealDamageSystem_0) continue;
                Fight.Signal(em, "apply", s.Entity, s.Source);
            }
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"[DEV FIGHT] apply failed: {e.GetType().Name}: {e.Message}"); }
        Fight.AddCost("apply", sw.Elapsed.TotalMilliseconds);
    }
}
