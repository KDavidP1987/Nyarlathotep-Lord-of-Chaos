#nullable enable
using System.Text;

namespace Nyarlathotep.Logic;

/// <summary>The definition load and the admin edits without the game (foundation D6, D23; raphael-api-core A7).
/// Services/EventStore runs them with the game's unit catalog and the disk; the tests run them over an in-memory file
/// store. A load reaches the catalog only through <see cref="EventCatalog.Reload"/>, so only an applied load reports
/// config-changed: a refused, stale, read-only, unreadable or unwritable edit returns before it.</summary>
public sealed class DefinitionEditor(EventsFile file, IFileStore files, EventCatalog catalog, Action<string> info, Action<string> warn)
{
    /// <summary>Loads events.json and applies it. Returns the `.nyar event reload` reply: "reloaded: &lt;v&gt; valid,
    /// &lt;x&gt; disabled" or the file error (the last valid set stays, D23).</summary>
    public string Reload(IUnitCatalog units)
    {
        var (result, stamp) = file.Load(units);
        var error = result.FileError ?? catalog.Reload(result, stamp!);   // a file error always comes with no applied set
        if (error is not null)
        {
            warn($"{error}; the last valid set stays ({catalog.Current.All.Count} events)");
            return error;
        }
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
        byte[]? bytes;
        try { bytes = files.Read(DataFile.Events, FileVariant.Main); }
        catch (Exception ex) { return $"events.json could not be read: {ex.Message}"; }
        if (bytes is null) return "events.json not found";
        var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        var edited = EventsEditor.Apply(text, id, path, value, out var refusal);
        if (edited is null) return refusal ?? $"event {id}: edit refused";
        var error = file.WriteEdit(Encoding.UTF8.GetBytes(edited), catalog.LoadedStamp, out _);
        if (error is not null) return error;

        var reload = Reload(units);
        var done = path == "enabled" ? $"event {id} {((bool)value ? "enabled" : "disabled")}" : $"event {id} {path} = {Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}";
        info($"{done}; {reload}");
        if (!reload.StartsWith("reloaded", StringComparison.Ordinal)) return $"{done}; {reload}";
        return catalog.Current.Find(id)?.DisabledReason is { } reason ? $"{done}; now disabled: {reason}" : done;
    }
}
