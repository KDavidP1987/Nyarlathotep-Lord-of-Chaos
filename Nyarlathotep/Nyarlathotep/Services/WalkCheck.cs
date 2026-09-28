using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM.Tiles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// Reads the game's static tile collision (walkable-spawns D1). Read-only: it builds map data over the server's live
/// tile world and asks whether a circle at a point collides for normal movement, and whether the point's tile is
/// grounded at a height level. The metadata does not say whether the circle test takes world metres or the tile grid
/// (KindredCommands Helper.ConvertPosToTileGrid: floor(x·2) + 6400), so the probe reads both spaces (A8). Only the live
/// singleton is read, and grounded only in tile space, where the index is never negative: a default TileWorld or a
/// negative tile index could fault in native code, where no managed catch helps (A10). Step 1 ships it behind the
/// temporary `.nyar debug walk` verb only; the Session 1 readings decide whether step 2 uses it (S-3).
/// </summary>
internal static class WalkCheck
{
    internal readonly record struct Reading(bool Free, bool Grounded, string Source);

    /// <summary>One reading per space of <see cref="AdminLines.WalkSources"/>, or none with the reason.</summary>
    internal static List<Reading> Probe(float x, float z, byte heightLevel, float radius, out string reason)
    {
        reason = "";
        var readings = new List<Reading>();
        TileWorld tileWorld;
        try
        {
            if (LiveTileWorld() is not { } live) { reason = "singleton: none"; return readings; }
            tileWorld = live;
        }
        catch (Exception e) { reason = $"singleton: {e.GetType().Name}"; return readings; }

        if (TileIndex(x) < 0 || TileIndex(z) < 0) { reason = "grounded: off the tile grid"; return readings; }
        bool grounded;
        try { grounded = tileWorld.GetIsGrounded(new int2(TileIndex(x), TileIndex(z)), heightLevel); }
        catch (Exception e) { reason = $"grounded: {e.GetType().Name}"; return readings; }

        try
        {
            var polygons = TileCollisionHelper.CreateLinePolygon();
            try
            {
                foreach (var space in new[] { "world", "tile" })
                {
                    try
                    {
                        var map = TileCollisionHelper.CreateMapData(polygons, tileWorld);
                        var (px, pz, r) = space == "world" ? (x, z, radius) : (x * 2 + 6400, z * 2 + 6400, radius * 2);
                        var blocked = TileMapCollisionMath.CheckStaticCircle(ref map, new float2(px, pz), heightLevel, r,
                            MapCollisionFlags.CollideNormalMovement);
                        readings.Add(new Reading(!blocked, grounded, $"singleton {space}"));
                    }
                    catch (Exception e)
                    {
                        reason = (reason == "" ? "" : reason + "; ") + $"singleton {space}: {e.GetType().Name}";
                    }
                }
            }
            finally { polygons.Dispose(); }                                // native arrays (Codex cross-inspection F1)
        }
        catch (Exception e)
        {
            reason = (reason == "" ? "" : reason + "; ") + $"polygons: {e.GetType().Name}";
        }
        return readings;
    }

    /// <summary>World metres to a tile index (two tiles per metre, offset 6400; KindredCommands' conversion).</summary>
    static int TileIndex(float v) => (int)math.floor(v * 2) + 6400;

    static TileWorld? LiveTileWorld()
    {
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<TileWorldSingleton>()));
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
