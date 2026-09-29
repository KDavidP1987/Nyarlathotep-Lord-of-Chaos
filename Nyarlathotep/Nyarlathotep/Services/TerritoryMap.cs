using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.CastleBuilding;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// Claimed castle territory, read once per wave (event-spawns D17, S-7). A territory is claimed when a CastleHeart
/// names it (CastleHeart.CastleTerritoryEntity, whatever the heart's state, decaying included); its
/// CastleTerritoryBlocks are the block coordinates Logic/Territory decides against. Read-only: it writes nothing in the
/// game. A map that cannot be built is null, and the wave that needs it is skipped "territory unknown" (fail closed);
/// each event keeps its latest successful build for its Hunt ticks (Logic/TerritoryMaps, A37, A39).
/// </summary>
internal static class TerritoryMap
{
    static TerritoryMaps _maps = new();

    internal static TerritoryMaps Maps => _maps;

    /// <summary>At IsReady: no kept map survives a restart (D33).</summary>
    internal static void Initialize() => _maps = new TerritoryMaps();

    /// <summary>Builds the claimed blocks for one wave of <paramref name="eventId"/> and keeps them for its Hunt ticks;
    /// null when the read failed, logged once per streak, with the "territory unknown" health entry open while any
    /// running event's latest build failed (D30, A44). Never throws.</summary>
    internal static IReadOnlySet<(int X, int Z)> ForWave(string eventId)
    {
        try
        {
            var (blocks, ignored) = Territory.Build(ReadBlocks());
            _maps.Built(eventId, blocks);
            if (ignored > 0 && Settings.VerboseLogging.Value)
                Core.Log.LogInfo($"[nyar] territory map: {blocks.Count} claimed blocks, {ignored} outside the map ignored");
            return blocks;
        }
        catch (Exception ex)
        {
            _maps.Failed(eventId);
            if (!WalkCheck.Health.TerritoryUnknown) Core.Log.LogWarning($"[nyar] territory map could not be built: {ex.Message}");
            return null;
        }
        finally
        {
            WalkCheck.Health.Territory(_maps.AnyFailed);
        }
    }

    /// <summary>An end path of <paramref name="eventId"/>: its kept map goes, and with it any "territory unknown" it held.</summary>
    internal static void Forget(string eventId)
    {
        _maps.Forget(eventId);
        WalkCheck.Health.Territory(_maps.AnyFailed);
    }

    /// <summary>`purge confirm`: every kept map goes.</summary>
    internal static void Clear()
    {
        _maps.Clear();
        WalkCheck.Health.Territory(false);
    }

    /// <summary>The block coordinates of every territory a castle heart names. Throws when the query fails.</summary>
    static List<(int X, int Z)> ReadBlocks()
    {
        var result = new List<(int X, int Z)>();
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<CastleHeart>()));
        try
        {
            var hearts = query.ToComponentDataArray<CastleHeart>(Allocator.Temp);
            try
            {
                foreach (var heart in hearts)
                {
                    var territory = heart.CastleTerritoryEntity;
                    if (!territory.Exists() || !Core.EntityManager.HasBuffer<CastleTerritoryBlocks>(territory)) continue;
                    var blocks = Core.EntityManager.GetBuffer<CastleTerritoryBlocks>(territory);
                    for (var i = 0; i < blocks.Length; i++) result.Add((blocks[i].BlockCoordinate.x, blocks[i].BlockCoordinate.y));
                }
            }
            finally { hearts.Dispose(); }
        }
        finally { query.Dispose(); }
        return result;
    }
}
