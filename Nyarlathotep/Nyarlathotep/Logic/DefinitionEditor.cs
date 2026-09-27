#nullable enable
using System.Text;

namespace Nyarlathotep.Logic;

/// <summary>What a chat write does to events.json (event-library D12): the new file text and the reply once it is
/// applied, or the refusal. <see cref="CheckId"/> is the definition whose disabled reason, if the reload gives it one,
/// the reply appends ("; now disabled: &lt;reason&gt;").</summary>
public readonly record struct EditPlan(string? Text, string? Refusal, string Done, string? CheckId)
{
    public static EditPlan Refuse(string reply) => new(null, reply, "", null);
}

/// <summary>The definition load and the admin edits without the game (foundation D6, D23; raphael-api-core A7;
/// event-library D12, D19). Services/EventStore runs them with the game's unit catalog and the disk; the tests run them
/// over an in-memory file store. A load reaches the catalog only through <see cref="EventCatalog.Reload"/>, so only an
/// applied load reports config-changed: a refused, stale, read-only, unreadable or unwritable edit returns before it.</summary>
public sealed class DefinitionEditor(EventsFile file, IFileStore files, EventCatalog catalog, Action<string> info, Action<string> warn)
{
    /// <summary>The refusal of a write that would pass foundation's MaxFileBytes (event-library D12).</summary>
    public const string TooLarge = "events.json would exceed 1 MB; nothing written";

    /// <summary>Loads events.json and applies it. Returns the `.nyar event reload` reply: "reloaded: &lt;v&gt; valid,
    /// &lt;x&gt; disabled" or the file error (the last valid set stays, D23). An applied load ends an uncertain write's
    /// degraded entry (event-library D32).</summary>
    public string Reload(IUnitCatalog units)
    {
        var (result, stamp) = file.Load(units);
        var error = result.FileError ?? catalog.Reload(result, stamp!);   // a file error always comes with no applied set
        if (error is not null)
        {
            warn($"{error}; the last valid set stays ({catalog.Current.All.Count} events)");
            return error;
        }
        file.WriteUncertain = false;
        foreach (var line in result.Log) warn(line);
        var disabled = result.Log.Count;
        var reply = $"reloaded: {catalog.Current.All.Count - disabled} valid, {disabled} disabled";
        info($"events: {reply}");                                          // at boot and on every `.nyar event reload`
        return reply;
    }

    /// <summary>`.nyar event set`, `enable` and `disable`: changes one field of event <paramref name="id"/> in events.json
    /// (Logic/EventsEditor), written only when the file is the one last loaded, keeping one .bak (Business rules 9, D6),
    /// then reloads. A running instance keeps its definition; the change applies to the next start.</summary>
    public string Edit(string id, string path, object value, IUnitCatalog units)
    {
        if (value is PointArg) path = "action.location";
        var shown = value switch
        {
            string[] list => string.Join(",", list),
            UnitEntry[] u => string.Join(",", u.Select(e => $"{e.Prefab}:{e.Count}")),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
        };
        var done = path == "enabled" ? $"event {id} {((bool)value ? "enabled" : "disabled")}" : $"event {id} {path} = {shown}";
        return Write(text =>
        {
            var edited = EventsEditor.Apply(text, id, path, value, out var refusal);
            return edited is null ? EditPlan.Refuse(refusal ?? $"event {id}: edit refused") : new EditPlan(edited, null, done, id);
        }, units);
    }

    /// <summary>Every chat write of events.json (event-library D12): reads the file, lets <paramref name="plan"/> edit its
    /// text, refuses a result over 1 MB, writes it with <see cref="EventsFile.WriteEdit(byte[], FileStamp?, out FileStamp?, out bool)"/>
    /// (the stale and newer-schema refusals, one .bak), then reloads, so exactly one config-changed follows an applied
    /// write. A refused plan writes nothing. A write whose Promote threw is uncertain: the file is read back and memory
    /// follows it (D19).</summary>
    public string Write(Func<string, EditPlan> plan, IUnitCatalog units)
    {
        byte[]? bytes;
        try { bytes = files.Read(DataFile.Events, FileVariant.Main); }
        catch (Exception ex) { return $"events.json could not be read: {ex.Message}"; }
        if (bytes is null) return "events.json not found";
        if (file.Writable(catalog.LoadedStamp) is { } refusal) return refusal;     // stale or read-only: never planned
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        EditPlan p;
        try { p = plan(text); }
        catch (Exception ex) { return $"edit refused: {ex.Message}"; }
        if (p.Text is null) return p.Refusal ?? "edit refused";
        var content = Encoding.UTF8.GetBytes(p.Text);
        if (content.Length > EventValidator.MaxFileBytes) return TooLarge;
        var error = file.WriteEdit(content, catalog.LoadedStamp, out _, out var uncertain);
        if (uncertain) return Uncertain(error ?? "unknown", units);
        if (error is not null) return error;

        var reload = Reload(units);
        info($"{p.Done}; {reload}");
        if (!reload.StartsWith("reloaded", StringComparison.Ordinal)) return $"{p.Done}; {reload}";
        return p.CheckId is { } id && catalog.Current.Find(id)?.DisabledReason is { } reason ? $"{p.Done}; now disabled: {reason}" : p.Done;
    }

    /// <summary>A Promote threw, so the main file may be the old or the new version (event-library D19): the file is read
    /// back and memory follows it, applied (one config-changed) only when it differs from what was loaded. The reply
    /// names the count the file now holds.</summary>
    string Uncertain(string reason, IUnitCatalog units)
    {
        var (result, stamp) = file.Load(units);
        if (result.FileError is not null || stamp is null)
        {
            file.WriteUncertain = true;
            warn($"events.json write uncertain ({reason}): could not read it back: {result.FileError}");
            return $"write uncertain: events.json could not be read back: {result.FileError}";
        }
        if (stamp != catalog.LoadedStamp) catalog.Reload(result, stamp);
        file.WriteUncertain = true;
        var n = result.Set.All.Count;
        warn($"events.json write uncertain ({reason}): reloaded {n} definitions from disk");
        return $"write uncertain: file now holds {n} definitions";
    }
}
