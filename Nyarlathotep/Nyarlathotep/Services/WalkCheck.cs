using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM.Tiles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// Reads the game's static tile collision (walkable-spawns D1). Read-only: it builds map data over the server's tile
/// world and asks whether a circle at a point collides for normal movement, and whether the point's tile is grounded
/// at a height level. The metadata does not say whether the calls take world metres or the tile grid (KindredCommands
/// Helper.ConvertPosToTileGrid: floor(x·2) + 6400), so the probe reads both spaces (A8). Step 1 ships it behind the
/// temporary `.nyar debug walk` verb only; the Session 1 readings decide whether step 2 uses it (S-3).
/// </summary>
internal static class WalkCheck
{
    internal readonly record struct Reading(bool Free, bool Grounded, string Source);

    /// <summary>One reading per space of <see cref="AdminLines.WalkSources"/> from the live tile world. XPRising's
    /// empty `new TileWorld()` is tried only when the singleton is missing, since a default struct may hold no data.
    /// Empty with the reason when every source failed.</summary>
    internal static List<Reading> Probe(float x, float z, byte heightLevel, float radius, out string reason)
    {
        reason = "";
        var readings = new List<Reading>();
        TileWorld? live = null;
        try { live = LiveTileWorld(); }
        catch (Exception e) { reason = $"singleton: {e.GetType().Name}"; }
        var tileWorld = live ?? new TileWorld();
        var prefix = live is null ? "empty" : "singleton";
        if (live is null && reason == "") reason = "singleton: none";
        foreach (var space in new[] { "world", "tile" })
        {
            try
            {
                var map = TileCollisionHelper.CreateMapData(TileCollisionHelper.CreateLinePolygon(), tileWorld);
                var (px, pz, r) = space == "world" ? (x, z, radius) : (TileX(x), TileX(z), radius * 2);
                var blocked = TileMapCollisionMath.CheckStaticCircle(ref map, new float2(px, pz), heightLevel, r,
                    MapCollisionFlags.CollideNormalMovement);
                var grounded = tileWorld.GetIsGrounded(new int2((int)math.floor(px), (int)math.floor(pz)), heightLevel);
                readings.Add(new Reading(!blocked, grounded, $"{prefix} {space}"));
            }
            catch (Exception e)
            {
                reason = (reason == "" ? "" : reason + "; ") + $"{prefix} {space}: {e.GetType().Name}";
            }
        }
        return readings;
    }

    /// <summary>World metres to the tile grid (two tiles per metre, offset 6400).</summary>
    static float TileX(float v) => math.floor(v * 2) + 6400;

    static TileWorld? LiveTileWorld()
    {
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<TileWorldSingleton>()));
        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            if (entities.Length == 0) return null;
            return Core.EntityManager.GetComponentData<TileWorldSingleton>(entities[0]).GetTileWorld();
        }
        finally { entities.Dispose(); query.Dispose(); }
    }
}
