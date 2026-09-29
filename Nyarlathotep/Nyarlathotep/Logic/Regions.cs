#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Nyarlathotep.Logic;

// Regional scope (regions D1-D3, D7; design §9 D21). The game's map regions are WorldRegionType polygons; this file
// holds the names, the scope a trigger or action carries, and the point-in-polygon index. Pure C#: Services/RegionMap
// reads the polygons from the game and hands them to RegionState.

/// <summary>The region names a scope may use: WorldRegionType's names except None and Other (regions D2, A16), in
/// the enum's order at 1.1.12. Services/RegionMap checks them against the game at boot.</summary>
public static class RegionNames
{
    /// <summary>What <see cref="RegionIndex.RegionOf"/> answers for a point in no region.</summary>
    public const string None = "None";

    /// <summary>The WorldRegionType names that are not map regions a scope can name.</summary>
    public static readonly IReadOnlyList<string> NotRegions = Array.AsReadOnly(new[] { "None", "Other" });

    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[]
    {
        "StartCave", "FarbaneWoods", "DunleyFarmlands", "CursedForest", "HallowedMountains",
        "SilverlightHills", "Gloomrot_South", "Gloomrot_North", "RuinsOfMortium", "Strongblade",
    });

    public static int Count => All.Count;

    /// <summary>The game's spelling of <paramref name="name"/>, matched case-insensitively (D3).</summary>
    public static bool TryCanonical(string? name, out string canonical)
    {
        foreach (var n in All)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) { canonical = n; return true; }
        canonical = "";
        return false;
    }

    /// <summary>"FarbaneWoods" → "Farbane Woods", "Gloomrot_South" → "Gloomrot South": the CamelCase name split at
    /// capitals, with `_` as a space (D8).</summary>
    public static string Display(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (ch == '_') { if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' '); continue; }
            if (char.IsUpper(ch) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>The difference between the game's WorldRegionType names (except <see cref="NotRegions"/>) and
    /// <see cref="All"/>: "+Name" for a name only the game has, "-Name" for one only this list has; empty when equal.</summary>
    public static IReadOnlyList<string> Differ(IEnumerable<string> gameNames)
    {
        var game = gameNames.Where(n => !NotRegions.Contains(n)).ToList();
        var diff = new List<string>();
        foreach (var n in game) if (!All.Contains(n)) diff.Add("+" + n);
        foreach (var n in All) if (!game.Contains(n)) diff.Add("-" + n);
        return diff;
    }
}

/// <summary>Where a trigger or action applies (D3): Global (the default, and <c>default(Scope)</c>) or 1 to
/// <see cref="RegionNames.Count"/> region names in the game's spelling.</summary>
public readonly record struct Scope
{
    readonly IReadOnlyList<string>? _regions;

    /// <summary>Copies <paramref name="regions"/>, so a later change to the caller's list cannot change the value.</summary>
    public Scope(IReadOnlyList<string> regions) => _regions = regions.Count == 0 ? null : Array.AsReadOnly(regions.ToArray());

    public static Scope Global => default;

    public bool IsGlobal => _regions is null;

    public IReadOnlyList<string> Regions => _regions ?? [];

    /// <summary>True when <paramref name="region"/> is one of the named regions; never true for Global, which callers
    /// test first so that a Global scope reads no position (Business rules 3).</summary>
    public bool Names(string region) => _regions is not null && _regions.Contains(region);

    /// <summary>"Global", or the names joined by ",".</summary>
    public override string ToString() => IsGlobal ? "Global" : string.Join(",", Regions);

    public bool Equals(Scope other) => Regions.SequenceEqual(other.Regions);

    public override int GetHashCode()
    {
        var h = 17;
        foreach (var r in Regions) h = h * 31 + StringComparer.Ordinal.GetHashCode(r);
        return h;
    }
}

/// <summary>What the validator needs to know about the map (D3, D7, A3, A15): whether the region index is built, and
/// which regions it holds a polygon for.</summary>
public interface IRegionCatalog
{
    bool Available { get; }
    bool OnMap(string region);
}

/// <summary>No index: every definition with a regional scope is disabled with "regions unavailable" (D7).</summary>
public sealed class NoRegions : IRegionCatalog
{
    public static readonly NoRegions Instance = new();
    public bool Available => false;
    public bool OnMap(string region) => false;
}

/// <summary>One polygon as read from the game: its region name, its axis-aligned box on x/z and its vertices.</summary>
public sealed record RegionPolygon(string Region, float MinX, float MinZ, float MaxX, float MaxZ, IReadOnlyList<(float X, float Z)> Vertices);

/// <summary>The polygons of the map regions (D1). <see cref="RegionOf"/> runs the box test first, then the even-odd
/// crossing test; the first polygon in index order that contains the point wins. A polygon with fewer than 3 vertices
/// or a non-finite coordinate is dropped; one tagged outside <see cref="RegionNames.All"/> (None, Other) is left out
/// as untagged (A16). Both are counted.</summary>
public sealed class RegionIndex : IRegionCatalog
{
    public static readonly RegionIndex Empty = Build([]);

    readonly List<Entry> _polygons;
    readonly HashSet<string> _regions;

    sealed record Entry(string Region, float MinX, float MinZ, float MaxX, float MaxZ, float[] Xs, float[] Zs);

    RegionIndex(List<Entry> polygons, int dropped, int untagged)
    {
        _polygons = polygons;
        _regions = new HashSet<string>(polygons.Select(p => p.Region), StringComparer.Ordinal);
        Dropped = dropped;
        Untagged = untagged;
    }

    public static RegionIndex Build(IEnumerable<RegionPolygon> polygons)
    {
        var list = new List<Entry>();
        int dropped = 0, untagged = 0;
        foreach (var p in polygons)
        {
            if (!RegionNames.All.Contains(p.Region)) { untagged++; continue; }
            if (p.Vertices.Count < 3 || !Finite(p.MinX, p.MinZ, p.MaxX, p.MaxZ) || p.Vertices.Any(v => !Finite(v.X, v.Z)))
            {
                dropped++;
                continue;
            }
            list.Add(new Entry(p.Region, p.MinX, p.MinZ, p.MaxX, p.MaxZ, p.Vertices.Select(v => v.X).ToArray(), p.Vertices.Select(v => v.Z).ToArray()));
        }
        return new RegionIndex(list, dropped, untagged);
    }

    static bool Finite(params float[] values) => values.All(float.IsFinite);

    public int PolygonCount => _polygons.Count;
    public int Dropped { get; }
    public int Untagged { get; }

    /// <summary>The regions holding at least one polygon, in <see cref="RegionNames.All"/> order.</summary>
    public IReadOnlyList<string> Regions => RegionNames.All.Where(_regions.Contains).ToList();

    public bool Available => _polygons.Count > 0;
    public bool OnMap(string region) => _regions.Contains(region);

    /// <summary>Box tests and polygon tests run so far, for the cost check (D15).</summary>
    public long BoxTests { get; private set; }
    public long PolygonTests { get; private set; }

    /// <summary>The region containing (<paramref name="x"/>, <paramref name="z"/>), or <see cref="RegionNames.None"/>.</summary>
    public string RegionOf(float x, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z)) return RegionNames.None;
        foreach (var p in _polygons)
        {
            BoxTests++;
            if (x < p.MinX || x > p.MaxX || z < p.MinZ || z > p.MaxZ) continue;
            PolygonTests++;
            if (Contains(p.Xs, p.Zs, x, z)) return p.Region;
        }
        return RegionNames.None;
    }

    /// <summary>Even-odd crossing test on a ray toward +x. An edge counts when exactly one of its ends lies above z (the
    /// half-open rule), so a vertex on the ray is counted once, never twice.</summary>
    static bool Contains(float[] xs, float[] zs, float x, float z)
    {
        var inside = false;
        for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
        {
            if ((zs[i] > z) == (zs[j] > z)) continue;
            var crossX = (xs[j] - xs[i]) * (z - zs[i]) / (zs[j] - zs[i]) + xs[i];
            if (x < crossX) inside = !inside;
        }
        return inside;
    }
}

/// <summary>The built index and its health (D2, D7): built once at boot, before the definitions are applied, and
/// rebuilt at `.nyar event reload` while it is unavailable. A build that finds no polygon, or throws, leaves the
/// index empty, so every regional definition is disabled with "regions unavailable" and Global ones run.</summary>
public sealed class RegionState : IRegionCatalog
{
    public const string HealthEntry = "regions: unavailable";

    public RegionIndex Index { get; private set; } = RegionIndex.Empty;

    public bool Available => Index.Available;
    public bool OnMap(string region) => Index.OnMap(region);

    /// <summary>The health entry while the index is unavailable (D7).</summary>
    public IReadOnlyList<string> Entries => Available ? [] : [HealthEntry];

    /// <summary>Builds the index from <paramref name="read"/> and logs the boot line, or the reason it is unavailable;
    /// then compares <paramref name="gameNames"/> with <see cref="RegionNames.All"/>. Never throws: the index is set
    /// before anything is logged, and a log sink that throws loses only its line.</summary>
    public void Build(Func<IEnumerable<RegionPolygon>> read, Func<IEnumerable<string>> gameNames, Action<string> info, Action<string> warn)
    {
        string line;
        bool ok;
        try
        {
            var built = RegionIndex.Build(read().ToList());
            Index = built;
            ok = built.Available;
            line = ok ? BootLine(built) : $"regions unavailable: no region polygons ({built.Dropped} dropped, {built.Untagged} untagged)";
        }
        catch (Exception ex)
        {
            Index = RegionIndex.Empty;
            ok = false;
            line = $"regions unavailable: {ex.GetType().Name}: {ex.Message}";
        }
        Log(ok ? info : warn, line);

        string? names;
        try
        {
            var diff = RegionNames.Differ(gameNames());
            names = diff.Count > 0 ? $"regions: names differ from the game: {string.Join(", ", diff)}" : null;
        }
        catch (Exception ex)
        {
            names = $"regions: names differ from the game: unreadable ({ex.GetType().Name})";
        }
        if (names is not null) Log(warn, names);
    }

    static void Log(Action<string> sink, string line)
    {
        try { sink(line); }
        catch (Exception) { /* a failing log sink never changes the index (Build never throws) */ }
    }

    /// <summary>At `.nyar event reload`: rebuilds only while the index is unavailable (D7).</summary>
    public void Retry(Func<IEnumerable<RegionPolygon>> read, Func<IEnumerable<string>> gameNames, Action<string> info, Action<string> warn)
    {
        if (!Available) Build(read, gameNames, info, warn);
    }

    /// <summary>"regions: &lt;p&gt; polygons, &lt;r&gt; regions (&lt;names&gt;); &lt;k&gt; untagged, &lt;d&gt; dropped" (D2, A16).</summary>
    public static string BootLine(RegionIndex index) =>
        $"regions: {index.PolygonCount} polygons, {index.Regions.Count} regions ({string.Join(", ", index.Regions)}); {index.Untagged} untagged, {index.Dropped} dropped";
}
