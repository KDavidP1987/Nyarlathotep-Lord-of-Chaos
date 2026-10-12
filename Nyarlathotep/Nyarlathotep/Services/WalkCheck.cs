#nullable enable
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM.Tiles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// Reads the game's static tile collision for wave placement (walkable-spawns D3, D5). Read-only: per wave it builds map
/// data over the server's live tile world and asks whether a unit's circle at a point collides for normal movement, in
/// world metres (the source Session 1 found go, A8, A12), and whether the point's tile is grounded at the wave centre's
/// height level (A13). Only a created live singleton is read, and grounded only where the tile index is not negative: a
/// default TileWorld or a negative index could fault in native code, where no managed catch helps (A10). Step 1's
/// temporary `.nyar debug walk` probe was removed in step 2 (D9). Hunting grounds reads it too, at a Region spot's scanned
/// levels and at the lock's and the hunt's landings (hunting-grounds D4, D5, D8), always within the tick's budget.
/// </summary>
internal static class WalkCheck
{
    /// <summary>Placement's game calls this tick (walkable-spawns D3, A3), reset by the scheduler's spawn phase.</summary>
    internal static readonly WalkBudget Budget = new();

    /// <summary>The failure streak behind "spawns: walk check unavailable" (D5, D6).</summary>
    internal static readonly SpawnHealth Health = new();

    /// <summary>A unit's circle for the walk check, in metres (Business rules 3).</summary>
    internal const float UnitRadius = 0.5f;

    /// <summary>One wave's walk check (D3, A3, A13): the world-metres source Session 1 found go (A8, A12), its map data
    /// made once for the wave, at the height level of <paramref name="centreY"/>. A centre without a height checks
    /// nothing (its units are unchecked, as in 0.5.0); a missing tile world opens the wave's fall-open (D5). Dispose
    /// the result after planning.</summary>
    internal static WaveCheck OpenWave(float? centreY)
    {
        if (centreY is not { } y) return new WaveCheck(new WaveWalk(null, Budget), null, null);
        WaveProbe probe;
        try
        {
            if (WalkHeight.Problem(y) is { } problem) throw new ArgumentException(problem);
            if (LiveTileWorld() is not { } live) throw new InvalidOperationException("singleton: none");
            if (!Created(live)) throw new InvalidOperationException("singleton: not created");
            probe = new WaveProbe(live, TileLayerUtility.GetHeightLevel(y));
        }
        catch (Exception e)
        {
            var failed = new WaveWalk(null, Budget);
            failed.Fail(e.Message);                                            // recorded even when no check runs (review F1)
            return new WaveCheck(failed, null, null);
        }
        return new WaveCheck(new WaveWalk(probe, Budget), probe, probe.Level);
    }

    /// <summary>The walk check of a Region wave's spot draw and of the region phase's landings (hunting-grounds D4, D5, D8):
    /// opened at height 0, since the level is passed with every call. Dispose the result after use.</summary>
    internal static WaveCheck OpenSpots() => OpenWave(0f);

    /// <summary>The height of a walk-check level (the game's TileLayerUtility.GetHeightForLevel): a Region spot's and a
    /// landing's height (hunting-grounds D4, D8).</summary>
    internal static float HeightOf(byte level) => TileLayerUtility.GetHeightForLevel(level);

    /// <summary>The lowest and highest heights a Region spot is looked for at, and the most levels scanned per try
    /// (hunting-grounds D4, S-12): two game calls a level, so a try costs at most 2 × <see cref="SpotLevelCap"/> calls of
    /// the tick's budget.</summary>
    internal const float SpotLowY = -20f, SpotHighY = 60f;
    internal const int SpotLevelCap = 24;

    static List<byte>? _spotLevels;

    /// <summary>The height levels a Region spot is scanned at, nearest the ground level of height 0 first (the level the
    /// map's lowlands stand on, walkable-spawns Session 2 "walk h 10" at y 0), then alternately up and down to
    /// <see cref="SpotHighY"/> and <see cref="SpotLowY"/>, each level once, at most <see cref="SpotLevelCap"/>. Built once
    /// from the game's own height-to-level mapping.</summary>
    internal static IReadOnlyList<byte> SpotLevels => _spotLevels ??= BuildSpotLevels();

    static List<byte> BuildSpotLevels()
    {
        var levels = new List<byte>();
        for (var d = 0f; d <= Math.Max(SpotHighY, -SpotLowY) && levels.Count < SpotLevelCap; d += 0.5f)
        {
            if (d <= SpotHighY) Add(TileLayerUtility.GetHeightLevel(d));
            if (d > 0 && -d >= SpotLowY) Add(TileLayerUtility.GetHeightLevel(-d));
        }
        return levels;

        void Add(byte level)
        {
            if (levels.Count < SpotLevelCap && !levels.Contains(level)) levels.Add(level);
        }
    }

    /// <summary>A wave's walk state, the probe's native resources to dispose after planning, and the height level read
    /// (null when nothing is checked).</summary>
    internal readonly record struct WaveCheck(WaveWalk Walk, IDisposable? Resource, byte? Level);

    /// <summary>True when the tile world's native containers exist, so a default or not yet built TileWorld never reaches
    /// native code (A10; Codex cross-inspection step 2 F1).</summary>
    static bool Created(TileWorld world) => world.ChunkAllocation.IsCreated && world.WorldCells.IsCreated && world.WorldCells.Length > 0;

    /// <summary>Records a planned wave's outcome in the failure streak (D5).</summary>
    internal static void Settle(WaveWalk walk) => Health.Settle(walk, line => Core.Log.LogWarning($"[nyar] {line}"));

    /// <summary>The live tile world's collision at one height level, for one wave.</summary>
    sealed class WaveProbe : IWalkProbe, IDisposable
    {
        readonly TileWorld _world;
        readonly byte _level;
        public byte Level => _level;
        TileMapCollisionMath.TilePolygons _polygons;
        TileMapCollisionMath.MapData _map;
        bool _disposed;

        internal WaveProbe(TileWorld world, byte level)
        {
            _world = world;
            _level = level;
            _polygons = TileCollisionHelper.CreateLinePolygon();
            try { _map = TileCollisionHelper.CreateMapData(_polygons, world); }
            catch { _polygons.Dispose(); throw; }                              // native arrays (A10)
        }

        /// <summary>At a given height level (automation A13): the level is passed through to both calls.</summary>
        public bool IsFree(float x, float z, byte level) =>
            !TileMapCollisionMath.CheckStaticCircle(ref _map, new float2(x, z), level, UnitRadius, MapCollisionFlags.CollideNormalMovement);

        public bool IsGrounded(float x, float z, byte level)
        {
            int tx = TileIndex(x), tz = TileIndex(z);
            if (tx < 0 || tz < 0) throw new ArgumentOutOfRangeException(nameof(x), "off the tile grid");   // A10
            return _world.GetIsGrounded(new int2(tx, tz), level);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _polygons.Dispose();
        }
    }

    /// <summary>World metres to a tile index (two tiles per metre, offset 6400; KindredCommands' conversion).</summary>
    static int TileIndex(float v) => (int)math.floor(v * 2) + 6400;

    /// <summary>The game keeps TileWorldSingleton on a system entity, which a default query leaves out (A11): the game's
    /// own singleton lookup first, then a query that includes system entities.</summary>
    static TileWorld? LiveTileWorld()
    {
        Entity mapped;
        try { mapped = Core.ServerScriptMapper.GetSingletonEntity<TileWorldSingleton>(); }
        catch (Exception) { mapped = Entity.Null; }                        // no unique singleton: try the query
        if (mapped.Has<TileWorldSingleton>())
            return Core.EntityManager.GetComponentData<TileWorldSingleton>(mapped).GetTileWorld();

        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<TileWorldSingleton>()) },
            Options = EntityQueryOptions.IncludeSystems
        });
        try
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0) return null;
                return Core.EntityManager.GetComponentData<TileWorldSingleton>(entities[0]).GetTileWorld();
            }
            finally { entities.Dispose(); }
        }
        finally { query.Dispose(); }
    }
}
