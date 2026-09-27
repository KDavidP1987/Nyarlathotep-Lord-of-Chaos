#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nyarlathotep.Logic;

/// <summary>One template of the catalogue: its definition as the validator reads it (with its disabled reason, if the
/// live catalogs refuse it) and its JSON object as shipped, which `.nyar template use` copies (event-library D1, D5).</summary>
public sealed record TemplateEntry(EventDefinition Definition, JsonObject Json)
{
    public string Id => Definition.Id;
    public string? Invalid => Definition.DisabledReason;
}

/// <summary>The built-in template catalogue (event-library D1, D3; Business rules 1): Resources/templates.json, embedded
/// in the DLL and read from it only; nothing writes it. It has the events.json v1 shape, so a template copies into
/// events.json without conversion. A catalogue that is missing, does not parse, or is of another SchemaVersion is
/// unavailable (<see cref="Error"/>); a valid one with no templates is available and empty.</summary>
public sealed class TemplateCatalog
{
    public const string ResourceName = "Nyarlathotep.Resources.templates.json";
    public const int SchemaVersion = 1;

    TemplateCatalog(IReadOnlyList<TemplateEntry> templates, string? error)
    {
        Templates = templates;
        Error = error;
    }

    public static readonly TemplateCatalog NotLoaded = new([], "not loaded");

    /// <summary>The templates in catalogue order (the file's).</summary>
    public IReadOnlyList<TemplateEntry> Templates { get; }

    /// <summary>Why the catalogue is unavailable, or null.</summary>
    public string? Error { get; }

    public int ValidCount => Templates.Count(t => t.Invalid is null);

    public TemplateEntry? Find(string id) => Templates.FirstOrDefault(t => t.Id == id);

    public static TemplateCatalog Unavailable(string reason) => new([], reason);

    /// <summary>Parses the catalogue through EventValidator.Parse against the given catalogs. Each template is also
    /// validated on its own, so the catalogue order and each template's reason survive; an id used twice disables the
    /// second with "duplicate id".</summary>
    public static TemplateCatalog Load(byte[]? bytes, IUnitCatalog units, IFactionCatalog factions)
    {
        if (bytes is null) return Unavailable("embedded templates.json missing");
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var whole = EventValidator.Parse(text, units, factions);
        if (whole.FileError is not null) return Unavailable(whole.FileError);
        if (whole.SchemaVersion != SchemaVersion) return Unavailable($"templates.json SchemaVersion {whole.SchemaVersion} is not {SchemaVersion}");

        JsonArray events;
        try { events = (JsonArray)JsonNode.Parse(text)!["events"]!; }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or NullReferenceException) { return Unavailable($"templates.json: {ex.Message}"); }

        var entries = new List<TemplateEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in events)
        {
            if (node is not JsonObject obj) return Unavailable("templates.json: every template must be an object");
            var one = EventValidator.Parse($"{{\"SchemaVersion\": 1, \"events\": [{obj.ToJsonString()}]}}", units, factions);
            if (one.FileError is not null) return Unavailable(one.FileError);
            var def = one.Set.All[0];
            if (!seen.Add(def.Id) && def.DisabledReason is null) def = def with { DisabledReason = "duplicate id" };
            entries.Add(new TemplateEntry(def, obj));
        }
        return new TemplateCatalog(entries, null);
    }

    /// <summary>The boot load (event-library D3, D19), Services/TemplateLibrary's one call: a load that cannot be used,
    /// or that throws, logs "template catalogue unavailable: &lt;reason&gt;" once and leaves the catalogue unavailable;
    /// otherwise each invalid template logs "template &lt;id&gt; invalid: &lt;reason&gt;" and the load ends with
    /// "templates: &lt;v&gt;/&lt;n&gt; valid".</summary>
    public static TemplateCatalog Boot(byte[]? bytes, IUnitCatalog units, IFactionCatalog factions, Action<string> info, Action<string> warn)
    {
        TemplateCatalog catalog;
        try { catalog = Load(bytes, units, factions); }
        catch (Exception ex) { catalog = Unavailable(ex.Message); }
        if (catalog.Error is { } error)
        {
            warn($"template catalogue unavailable: {error}");
            return catalog;
        }
        foreach (var t in catalog.Templates.Where(t => t.Invalid is not null)) warn($"template {t.Id} invalid: {t.Invalid}");
        info($"templates: {catalog.ValidCount}/{catalog.Templates.Count} valid");
        return catalog;
    }

    /// <summary>What the shipped catalogue must never do (event-library D1, D18): ship a template enabled, carry a key
    /// outside the event keys, use an id twice or an id of Resources/events.default.json, or be invalid under the
    /// catalogs it was loaded with. Empty when the catalogue is sound; an unavailable catalogue is one problem.</summary>
    public static IReadOnlyList<string> Problems(TemplateCatalog catalog, IEnumerable<string> defaultIds)
    {
        if (catalog.Error is { } error) return [$"catalogue unavailable: {error}"];
        var problems = new List<string>();
        var defaults = new HashSet<string>(defaultIds, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in catalog.Templates)
        {
            if (t.Json["enabled"] is not JsonValue en || !en.TryGetValue<bool>(out var enabled) || enabled) problems.Add($"template {t.Id} does not ship \"enabled\": false");
            foreach (var key in t.Json.Select(p => p.Key).Where(k => !EventValidator.EventKeyNames.Contains(k))) problems.Add($"template {t.Id} has key {key}");
            if (!seen.Add(t.Id)) problems.Add($"template id {t.Id} is used twice");
            if (defaults.Contains(t.Id)) problems.Add($"template id {t.Id} is an events.default.json id");
            if (t.Invalid is { } reason) problems.Add($"template {t.Id} invalid: {reason}");
        }
        return problems;
    }
}

/// <summary>The template commands' chat replies (event-library D4, D19): human lines with their own formatter, never
/// the api's Logic/Paging.cs Reply and its `[NYAR:end]` line, though they share its page size.</summary>
public static class TemplateLines
{
    public const string NoTemplates = "no templates";
    public const string CatalogueUnavailable = "template catalogue unavailable";
    public const string PageTooLow = "page must be 1 or more";

    public static string UnknownTemplate(string id) => $"unknown template {id}; .nyar template list shows them";

    /// <summary>`.nyar template list [pillar] [page]`: a word of digits is a page, any other word a pillar, which comes
    /// first; ten templates a page in catalogue order, with a footer naming the next page's command.</summary>
    public static IReadOnlyList<string> List(TemplateCatalog catalog, IReadOnlyList<string> words, IReadOnlyCollection<string> inEvents)
    {
        if (catalog.Error is not null) return [CatalogueUnavailable];
        var w = words.Where(x => !string.IsNullOrEmpty(x)).ToList();
        string? pillarWord = null;
        string? pageWord = null;
        if (w.Count == 1) { if (IsDigits(w[0])) pageWord = w[0]; else pillarWord = w[0]; }
        else if (w.Count == 2)
        {
            if (IsDigits(w[0]) || !IsDigits(w[1])) return [CommandForms.Library.First(f => f.Words == "template list").ChatUsage];
            pillarWord = w[0];
            pageWord = w[1];
        }
        else if (w.Count > 2) return [CommandForms.Library.First(f => f.Words == "template list").ChatUsage];

        Pillar? pillar = null;
        if (pillarWord is not null)
        {
            if (!PillarNames.TryParse(pillarWord, out var p)) return [PillarNames.Unknown(pillarWord)];
            pillar = p;
        }
        var page = 1;
        if (pageWord is not null)
        {
            if (!int.TryParse(pageWord, NumberStyles.None, CultureInfo.InvariantCulture, out page)) page = int.MaxValue;
            if (page < 1) return [PageTooLow];
        }

        var shown = catalog.Templates.Where(t => pillar is null || t.Definition.Pillar == pillar).ToList();
        if (shown.Count == 0) return [pillar is { } none ? $"no templates for pillar {PillarNames.Name(none)}" : NoTemplates];
        var pages = Paging.Pages(shown.Count);
        if (page > pages) return [$"no page {pageWord}; {pages} pages"];

        var lines = shown.Skip((page - 1) * Paging.PageSize).Take(Paging.PageSize).Select(t => Line(t, inEvents.Contains(t.Id))).ToList();
        if (pages > 1)
            lines.Add(page < pages
                ? $"page {page}/{pages}; .nyar template list {(pillar is { } p2 ? PillarNames.Name(p2) + " " : "")}{page + 1} for more"
                : $"page {pages}/{pages}");
        return lines;
    }

    /// <summary>"&lt;id&gt; &lt;pillar&gt; &lt;trigger&gt; "&lt;name&gt;"", marked " (in events.json)" when events.json holds
    /// that id and " invalid: &lt;reason&gt;" when the live catalogs refuse it.</summary>
    public static string Line(TemplateEntry t, bool inEvents)
    {
        var d = t.Definition;
        return $"{d.Id} {PillarNames.Name(d.Pillar)} {EventLines.Trigger(d.Trigger)} \"{d.Name}\"" +
               (inEvents ? " (in events.json)" : "") +
               (t.Invalid is { } reason ? $" invalid: {reason}" : "");
    }

    /// <summary>`.nyar template info &lt;id&gt;`: EventLines.Info of the template.</summary>
    public static IReadOnlyList<string> Info(TemplateCatalog catalog, string id, DateTime utcNow)
    {
        if (catalog.Error is not null) return [CatalogueUnavailable];
        return catalog.Find(id) is { } t ? EventLines.Info(t.Definition, null, utcNow) : [UnknownTemplate(id)];
    }

    static bool IsDigits(string s) => s.Length > 0 && s.All(c => c is >= '0' and <= '9');
}

/// <summary>The library's entries of the degraded list (event-library D32): a catalogue that failed to load, until the
/// next boot, and an uncertain events.json write, until the next clean write or applied load.</summary>
public static class LibraryHealth
{
    public const string CatalogueUnavailable = "template catalogue unavailable";
    public const string WriteUncertain = "events.json write uncertain";

    public static IReadOnlyList<string> Entries(string? catalogueError, bool writeUncertain)
    {
        var entries = new List<string>();
        if (catalogueError is not null) entries.Add(CatalogueUnavailable);
        if (writeUncertain) entries.Add(WriteUncertain);
        return entries;
    }
}
