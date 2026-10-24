#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nyarlathotep.Logic;

/// <summary>The server's pool library as the validator reads it (horde-pools D6): named pools any event may link. A unit
/// catalog that is also an <see cref="IPoolLibrary"/> serves it; otherwise there is none.</summary>
public interface IPoolLibrary
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> LibraryPools { get; }
}

/// <summary>One pool of the library (horde-pools D5, D8): <see cref="Seeded"/> while it is exactly as the seed wrote it,
/// <see cref="Groups"/> the allied groups its members belong to.</summary>
public sealed record LibraryPool(string Name, IReadOnlyList<string> Members, bool Seeded, IReadOnlyList<string> Groups)
{
    public bool Mixes => Groups.Count > 1;
}

/// <summary>
/// pools.json (horde-pools D5): pools of units kept apart from the events, like templates, that any event names by
/// name. Seeded once, at a load that finds no file, with each allied group's units and bosses (FactionGroups); never
/// written again, so the operator owns it. The file is { "SchemaVersion": 1, "pools": { name: [CHAR_ names] } }: 1-64
/// pools under the event pool rules. A file that cannot be read or breaks a rule loads no pool and is left as it is: the
/// one "pools.json not loaded: &lt;reason&gt;" line and the "pools unknown" health entry say so, and an event naming a
/// library pool is invalid until the file is fixed and reloaded.
/// </summary>
public sealed class PoolLibrary : IPoolLibrary
{
    public const string HealthEntry = "pools unknown";
    public const int CurrentSchemaVersion = 1;
    public const int MaxPools = 64;
    public const int MaxFileBytes = 256 * 1024;
    public const string UnitsSuffix = "-units";
    public const string BossesSuffix = "-bosses";

    static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> None = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> LibraryPools { get; private set; } = None;
    public IReadOnlyList<LibraryPool> List { get; private set; } = [];

    /// <summary>Why the last load failed, or null; while set, <see cref="Entries"/> holds the health entry.</summary>
    public string? Error { get; private set; }

    public IReadOnlyList<string> Entries => Error is null ? [] : [HealthEntry];

    public LibraryPool? Find(string name) => List.FirstOrDefault(p => p.Name == name);

    /// <summary>The 17 seeded pools: `&lt;group&gt;-units` and `&lt;group&gt;-bosses` for each allied group in the table's order,
    /// the bosses half from <see cref="FactionGroups.Bosses"/>; a member this server does not know or denies is left out
    /// (<paramref name="dropped"/> names it), and a pool left empty is not written.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Seed(IUnitCatalog units, List<string>? dropped = null)
    {
        var seed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var g in FactionGroups.All)
            foreach (var (suffix, bosses) in new[] { (UnitsSuffix, false), (BossesSuffix, true) })
            {
                var name = g.Name + suffix;
                var members = new List<string>();
                foreach (var p in g.Names.Where(n => FactionGroups.Bosses.Contains(n) == bosses))
                {
                    if (units.IsKnown(p) && !units.IsDenied(p) && !UnitDenyList.IsDenied(p)) members.Add(p);
                    else dropped?.Add($"{name} {p}");
                }
                if (members.Count > 0) seed.Add(name, members);
            }
        return seed;
    }

    /// <summary>The file's bytes for <paramref name="pools"/>, indented, in their order.</summary>
    public static byte[] Bytes(IReadOnlyDictionary<string, IReadOnlyList<string>> pools)
    {
        var body = new JsonObject();
        foreach (var (name, members) in pools) body[name] = new JsonArray(members.Select(m => (JsonNode?)JsonValue.Create(m)).ToArray());
        var root = new JsonObject { ["SchemaVersion"] = CurrentSchemaVersion, ["pools"] = body };
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    /// <summary>The pools of <paramref name="text"/>, or the reason the file loads none.</summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<string>>? Pools, string? Error) Parse(string text, IUnitCatalog units)
    {
        var bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > MaxFileBytes) return (null, $"{bytes} bytes, over the {MaxFileBytes} byte limit");
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException ex)
        {
            return (null, $"line {(ex.LineNumber ?? 0) + 1} position {(ex.BytePositionInLine ?? 0) + 1}");
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, "the top level must be an object { \"SchemaVersion\": 1, \"pools\": { ... } }");
            foreach (var p in root.EnumerateObject())
                if (p.Name is not ("SchemaVersion" or "pools")) return (null, $"unknown top-level field {Short(p.Name)}");
            if (!root.TryGetProperty("SchemaVersion", out var sv) || sv.ValueKind != JsonValueKind.Number || !sv.TryGetInt32(out var schema) || schema < 1)
                return (null, "SchemaVersion must be an integer 1 or higher");
            if (!root.TryGetProperty("pools", out var pools) || pools.ValueKind != JsonValueKind.Object) return (null, "pools must be an object");
            if (pools.EnumerateObject().Count() is < 1 or > MaxPools) return (null, $"pools must hold 1-{MaxPools} pools");
            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var pool in pools.EnumerateObject())
            {
                if (!EventValidator.IsPoolName(pool.Name) || result.ContainsKey(pool.Name)) return (null, $"{Short(pool.Name)} is not a pool name");
                var rule = $"pools.{pool.Name} must hold 1-{EventValidator.MaxPoolMembers} distinct units";
                if (pool.Value.ValueKind != JsonValueKind.Array || pool.Value.GetArrayLength() is < 1 or > EventValidator.MaxPoolMembers) return (null, rule);
                var members = new List<string>();
                foreach (var x in pool.Value.EnumerateArray())
                {
                    var prefab = x.ValueKind == JsonValueKind.String ? x.GetString()! : null;
                    if (prefab is null || members.Contains(prefab)) return (null, rule);
                    if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) return (null, $"pools.{pool.Name}: unit {Short(prefab)} is deny-listed");
                    if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) return (null, $"pools.{pool.Name}: unknown unit {Short(prefab)}");
                    members.Add(prefab);
                }
                result.Add(pool.Name, members);
            }
            return (result, null);
        }
    }

    /// <summary>Seeds pools.json when there is none, then loads it (boot and `.nyar pool reload`). Returns the pools now
    /// loaded; on a failure the library is empty and <see cref="Error"/> says why. Never writes a file that exists.</summary>
    public int Load(DataStore store, IUnitCatalog units, Action<string> info, Action<string> warn)
    {
        // Anything unforeseen empties the library too, so a reload never keeps the pools it replaced (Codex horde-pools F1).
        try { return LoadFile(store, units, info, warn); }
        catch (Exception ex) { return Fail($"could not be loaded: {ex.Message}", warn); }
    }

    int LoadFile(DataStore store, IUnitCatalog units, Action<string> info, Action<string> warn)
    {
        store.CleanStaleTmp(DataFile.Pools);
        bool exists;
        try { exists = store.Files.Exists(DataFile.Pools, FileVariant.Main); }
        catch (Exception ex) { return Fail($"could not be read: {ex.Message}", warn); }
        if (!exists)
        {
            var dropped = new List<string>();
            var seed = Seed(units, dropped);
            foreach (var d in dropped) warn($"pools.json seed: {d} left out (unknown or deny-listed here)");
            var error = store.WriteAtomicNew(DataFile.Pools, Bytes(seed));
            if (error is null) info($"first run: seeded pools.json with {seed.Count} pools");
            else warn($"pools.json not seeded: {error}");
        }
        byte[]? bytes;
        try { bytes = store.Files.Read(DataFile.Pools, FileVariant.Main); }
        catch (Exception ex) { return Fail($"could not be read: {ex.Message}", warn); }
        if (bytes is null) return Fail("not found", warn);
        var (pools, reason) = Parse(Decode(bytes), units);
        if (pools is null) return Fail(reason!, warn);

        var seeded = Seed(units);
        var list = pools.Select(p => new LibraryPool(p.Key, p.Value,
            seeded.TryGetValue(p.Key, out var s) && s.SequenceEqual(p.Value, StringComparer.Ordinal), FactionGroups.GroupsOf(p.Value))).ToList();
        LibraryPools = pools;
        List = list;
        Error = null;
        info($"pools: {list.Count} loaded ({list.Count(p => p.Seeded)} seeded)");
        foreach (var p in list.Where(p => p.Mixes)) warn(MixesLine(p));
        return list.Count;
    }

    /// <summary>"pool &lt;name&gt; mixes groups: &lt;a&gt;, &lt;b&gt;" (horde-pools D5): its members' allied groups would fight each other.</summary>
    public static string MixesLine(LibraryPool p) => $"pool {p.Name} mixes groups: {string.Join(", ", p.Groups)}";

    int Fail(string reason, Action<string> warn)
    {
        LibraryPools = None;
        List = [];
        Error = reason;
        warn($"pools.json not loaded: {reason}");
        return 0;
    }

    static string Decode(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) : Encoding.UTF8.GetString(bytes);

    /// <summary>A name the file gave, cut to 40 characters so a reason stays one chat line.</summary>
    static string Short(string name) => name.Length <= 40 ? name : name[..40] + "…";
}

/// <summary>A unit catalog with the library beside it, for a validation that is handed both (horde-pools D6).</summary>
public sealed class LinkedCatalog(IUnitCatalog units, IReadOnlyDictionary<string, IReadOnlyList<string>> library) : IUnitCatalog, IPoolLibrary
{
    public bool IsKnown(string prefabName) => units.IsKnown(prefabName);
    public bool IsDenied(string prefabName) => units.IsDenied(prefabName);
    public IReadOnlyDictionary<string, IReadOnlyList<string>> LibraryPools => library;
}

/// <summary>The `.nyar pool` replies (horde-pools D8), each line within Wire.MaxBytes: pool and event names and counts only.</summary>
public static class PoolLines
{
    public const string Verbs = "argument must be list [page], info <pool> [page] or reload";
    public const string Unknown = "pools unknown: pools.json is not loaded; fix it, then .nyar pool reload";
    public const string NoPools = "no pools";
    public const string PageTooLow = "page must be 1 or more";

    public static string UnknownPool(string name) =>
        $"unknown pool{(EventValidator.IsPoolName(name) ? " " + name : "")}; .nyar pool list shows them";

    /// <summary>"&lt;name&gt;: &lt;n&gt; units, seeded|library", ten a page in the file's order, with a footer naming the next page.</summary>
    public static IReadOnlyList<string> List(PoolLibrary library, string? pageWord)
    {
        if (library.Error is not null) return [Unknown];
        if (library.List.Count == 0) return [NoPools];
        if (Page(pageWord, out var page) is { } bad) return [bad];
        var pages = Paging.Pages(library.List.Count);
        if (page > pages) return [$"no page {pageWord}; {pages} pages"];
        var lines = library.List.Skip((page - 1) * Paging.PageSize).Take(Paging.PageSize)
            .Select(p => $"{p.Name}: {p.Members.Count} units, {(p.Seeded ? "seeded" : "library")}").ToList();
        if (pages > 1) lines.Add(page < pages ? $"page {page}/{pages}; .nyar pool list {page + 1} for more" : $"page {pages}/{pages}");
        return lines;
    }

    /// <summary>The pool's head line, its "mixes groups" warning, the events that name it, then its members, ten a page.</summary>
    public static IReadOnlyList<string> Info(PoolLibrary library, string name, string? pageWord, IEnumerable<EventDefinition> definitions)
    {
        if (library.Error is not null) return [Unknown];
        if (library.Find(name) is not { } pool) return [UnknownPool(name)];
        if (Page(pageWord, out var page) is { } bad) return [bad];
        var pages = Paging.Pages(pool.Members.Count);
        if (page > pages) return [$"no page {pageWord}; {pages} pages"];
        var lines = new List<string> { $"pool {pool.Name}: {pool.Members.Count} units, {(pool.Seeded ? "seeded" : "library")}" };
        if (pool.Mixes) lines.Add(PoolLibrary.MixesLine(pool));
        var naming = definitions.Where(d => (d.Action?.Linked ?? d.Apocalypse?.Linked)?.Contains(name) == true).Select(d => d.Id).ToList();
        lines.AddRange(Packed("named by: ", naming.Count == 0 ? ["no event"] : naming));
        lines.AddRange(pool.Members.Skip((page - 1) * Paging.PageSize).Take(Paging.PageSize).Select(m => "  " + m));
        if (pages > 1) lines.Add(page < pages ? $"members page {page}/{pages}; .nyar pool info {pool.Name} {page + 1} for more" : $"members page {pages}/{pages}");
        return lines;
    }

    /// <summary>"pools: &lt;n&gt; loaded; events invalid now: &lt;ids or none&gt;", or the file's failure in place of the count,
    /// cut to Wire.MaxBytes with "… and &lt;m&gt; more". When events.json was <paramref name="refused"/>, the old set still
    /// stands unchecked against the new pools, so the reply says that in place of the list.</summary>
    public static string Reload(int loaded, string? error, IReadOnlyList<string> invalid, string? refused = null)
    {
        var pools = error is null ? $"pools: {loaded} loaded" : TextSink.CutToBytes($"pools.json not loaded: {error}", 200);
        if (refused is not null) return TextSink.CutToBytes($"{pools}; events.json not reloaded, its events are unchecked: {refused}", Wire.MaxBytes);
        var head = pools + "; events invalid now: ";
        if (invalid.Count == 0) return head + "none";
        for (var keep = invalid.Count; keep > 0; keep--)
        {
            var line = head + string.Join(", ", invalid.Take(keep)) + (keep < invalid.Count ? $" … and {invalid.Count - keep} more" : "");
            if (Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes) return line;
        }
        return head + $"{invalid.Count} events";
    }

    static string? Page(string? word, out int page)
    {
        page = 1;
        if (string.IsNullOrEmpty(word)) return null;
        if (!int.TryParse(word, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out page)) page = int.MaxValue;
        return page < 1 ? PageTooLow : null;
    }

    static IEnumerable<string> Packed(string prefix, IReadOnlyList<string> items)
    {
        var line = new StringBuilder(prefix);
        var first = true;
        foreach (var item in items)
        {
            var add = (first ? "" : ", ") + item;
            if (!first && Encoding.UTF8.GetByteCount(line.ToString()) + Encoding.UTF8.GetByteCount(add) > Wire.MaxBytes)
            {
                yield return line.ToString();
                line.Clear().Append(prefix);
                add = item;
            }
            line.Append(add);
            first = false;
        }
        yield return line.ToString();
    }
}
