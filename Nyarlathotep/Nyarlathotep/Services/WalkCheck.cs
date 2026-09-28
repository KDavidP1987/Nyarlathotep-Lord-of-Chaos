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
/// temporary `.nyar debug walk` probe was removed in step 2 (D9).
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
        internal byte Level => _level;
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

        public bool IsFree(float x, float z) =>
            !TileMapCollisionMath.CheckStaticCircle(ref _map, new float2(x, z), _level, UnitRadius, MapCollisionFlags.CollideNormalMovement);

        public bool IsGrounded(float x, float z)
        {
            int tx = TileIndex(x), tz = TileIndex(z);
            if (tx < 0 || tz < 0) throw new ArgumentOutOfRangeException(nameof(x), "off the tile grid");   // A10
            return _world.GetIsGrounded(new int2(tx, tz), _level);
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
