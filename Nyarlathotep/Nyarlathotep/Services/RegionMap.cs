using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM.Terrain;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// The map's regions (regions D2, D7; second in Core.TryInitialize, before EventStore applies the definitions). Reads
/// every WorldRegionPolygon entity (disabled included), its PolygonBounds and its WorldRegionPolygonVertex buffer
/// into Logic/RegionIndex, once per boot; `.nyar event reload` retries while the index is unavailable. Read-only: it
/// never writes a component. The polygons read are kept, read-only, for a Region location's spot draw (hunting-grounds
/// D4, Logic/SpotDraw).
/// </summary>
internal static class RegionMap
{
    internal static RegionState State { get; } = new();

    static IReadOnlyList<RegionPolygon> _polygons = [];

    /// <summary>The polygons of the latest read, read-only, for a Region location's spot draw (hunting-grounds D4), which
    /// keeps the scope regions' own (SpotDraw.InScope); none while the index is unavailable.</summary>
    internal static IReadOnlyList<RegionPolygon> Polygons => State.Available ? _polygons : [];

    /// <summary>Never throws: a failed build leaves the index empty and regional definitions disabled (D7).</summary>
    internal static void Initialize() => State.Build(ReadAndKeep, GameNames, Info, Warn);

    /// <summary>At `.nyar event reload`, before the definitions are parsed (D7).</summary>
    internal static void Retry() => State.Retry(ReadAndKeep, GameNames, Info, Warn);

    /// <summary>Reads the polygons and keeps them for <see cref="Polygons"/>; a throwing read keeps none (the index is then
    /// unavailable too).</summary>
    static List<RegionPolygon> ReadAndKeep()
    {
        _polygons = [];
        var read = ReadPolygons();
        _polygons = read.AsReadOnly();
        return read;
    }

    static void Info(string line) => Core.Log.LogInfo($"[nyar] {line}");
    static void Warn(string line) => Core.Log.LogWarning($"[nyar] {line}");

    static IEnumerable<string> GameNames() => Enum.GetNames(typeof(WorldRegionType));

    static List<RegionPolygon> ReadPolygons()
    {
        var polygons = new List<RegionPolygon>();
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<WorldRegionPolygon>()), ComponentType.ReadOnly(Il2CppType.Of<WorldRegionPolygonVertex>()) },
            Options = EntityQueryOptions.IncludeDisabled
        });
        try
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in entities)
                {
                    var polygon = entity.Read<WorldRegionPolygon>();
                    var buffer = Core.EntityManager.GetBuffer<WorldRegionPolygonVertex>(entity, true);   // read-only
                    var vertices = new List<(float X, float Z)>(buffer.Length);
                    for (var i = 0; i < buffer.Length; i++) vertices.Add((buffer[i].VertexPos.x, buffer[i].VertexPos.y));
                    var box = polygon.PolygonBounds;
                    polygons.Add(new RegionPolygon(polygon.WorldRegion.ToString(), box.Min.x, box.Min.z, box.Max.x, box.Max.z, vertices));
                }
            }
            finally { entities.Dispose(); }
        }
        finally { query.Dispose(); }
        return polygons;
    }
}
