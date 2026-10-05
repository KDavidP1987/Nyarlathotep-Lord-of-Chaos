#nullable enable
using System.Text;

namespace Nyarlathotep.Logic;

/// <summary>What a chat write does to events.json (event-library D12): the new file text and the reply once it is
/// applied, or the refusal. <see cref="CheckId"/> is the definition whose disabled reason, if the reload gives it one,
/// the reply appends ("; now disabled: &lt;reason&gt;").</summary>
public readonly record struct EditPlan(string? Text, Outcome? Refusal, string Done, string? CheckId, params (string Key, string Value)[] Fields)
{
    public static EditPlan Refuse(Outcome refusal) => new(null, refusal, "", null);

    /// <summary>The file already holds the change (raphael-api-admin D5): nothing is written or reloaded, and the reply
    /// is the applied one's, so the human text stays 0.5.1's.</summary>
    public static EditPlan Hold(string done, string checkId, params (string Key, string Value)[] fields) => new(null, null, done, checkId, fields);

    public bool Held => Text is null && Refusal is null && CheckId is not null;
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
    public Outcome Reload(IUnitCatalog units)
    {
        var (result, stamp) = file.Load(units);
        var error = result.FileError ?? catalog.Reload(result, stamp!);   // a file error always comes with no applied set
        if (error is not null)
        {
            warn($"{error}; the last valid set stays ({catalog.Current.All.Count} events)");
            return FileErrors.Refusal(error);
        }
        file.WriteUncertain = false;
        foreach (var line in result.Log) warn(line);
        var disabled = result.Log.Count;
        var reply = $"reloaded: {catalog.Current.All.Count - disabled} valid, {disabled} disabled";
        info($"events: {reply}");                                          // at boot and on every `.nyar event reload`
        return Outcome.Done(reply, ("count", catalog.Current.All.Count.ToString()));
    }

    /// <summary>`.nyar event set`, `enable` and `disable`: changes one field of event <paramref name="id"/> in events.json
    /// (Logic/EventsEditor), written only when the file is the one last loaded, keeping one .bak (Business rules 9, D6),
    /// then reloads. A running instance keeps its definition; the change applies to the next start.</summary>
    /// <summary>An edit EventsEditor.Apply refused without saying why (a fallback: Apply always says).</summary>
    public static Outcome EditRefused(string id) => Outcome.Refused($"event {id}: edit refused", RefusalCode.Invalid, "field", reason: Reasons.Field);

    public Outcome Edit(string id, string path, object value, IUnitCatalog units)
    {
        if (value is PointArg or AroundPlayerArg) path = "action.location";      // one field name for every location form (A50)
        var shown = value switch
        {
            string[] list => string.Join(",", list),
            UnitEntry[] u => string.Join(",", u.Select(e => $"{e.Prefab}:{e.Count}")),
            bool b => b ? "true" : "false",                                           // as typed and as events.json spells it (A50)
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
        };
        var done = path == "enabled" ? $"event {id} {((bool)value ? "enabled" : "disabled")}" : $"event {id} {path} = {shown}";
        // a location's wire value is "x,z" with one decimal (contract §5a); the human text keeps its own form
        var wireValue = value is PointArg pt ? FormattableString.Invariant($"{pt.X:0.0},{pt.Z:0.0}") : shown ?? "";
        (string, string)[] fields = path == "enabled" ? [("changed", "1")] : [("field", path), ("value", wireValue)];
        return Write(text =>
        {
            // enable and disable on the state the file holds write nothing and answer changed=0 (raphael-api-admin D5)
            if (path == "enabled" && EventsEditor.Holds(text, id, (bool)value)) return EditPlan.Hold(done, id, ("changed", "0"));
            var edited = EventsEditor.Apply(text, id, path, value, out var refusal, out var note);
            return edited is null
                ? EditPlan.Refuse(refusal ?? EditRefused(id))
                : new EditPlan(edited, null, WaveSetReply(done, note, path, value), id, fields);
        }, units);
    }

    /// <summary>The reply of a wave-list edit (wave-sets D14): the conversion's note appended, and a units list too long
    /// for one chat line shown as its entry count, so the reply stays within Wire.MaxBytes. Any other reply is
    /// <paramref name="done"/>.</summary>
    static string WaveSetReply(string done, string? note, string path, object value)
    {
        if (CommandArgs.WaveListPath(path) is null) return done;
        if (note is not null) done += $"; {note}";
        if (System.Text.Encoding.UTF8.GetByteCount(done) <= Wire.MaxBytes || value is not UnitEntry[] units) return done;
        var head = done[..done.IndexOf(" = ", StringComparison.Ordinal)];
        return $"{head} = {units.Length} entries, {units.Sum(u => u.Count)} units" + (note is null ? "" : $"; {note}");
    }

    /// <summary>Every chat write of events.json (event-library D12): reads the file, lets <paramref name="plan"/> edit its
    /// text, refuses a result over 1 MB, writes it with <see cref="EventsFile.WriteEdit(byte[], FileStamp?, out FileStamp?, out bool)"/>
    /// (the stale and newer-schema refusals, one .bak), then reloads, so exactly one config-changed follows an applied
    /// write. A refused plan writes nothing. A write whose Promote threw is uncertain: the file is read back and memory
    /// follows it (D19).</summary>
    public Outcome Write(Func<string, EditPlan> plan, IUnitCatalog units)
    {
        byte[]? bytes;
        try { bytes = files.Read(DataFile.Events, FileVariant.Main); }
        catch (Exception ex) { return FileErrors.Refusal($"events.json could not be read: {ex.Message}"); }
        if (bytes is null) return FileErrors.Refusal("events.json not found");
        if (file.Writable(catalog.LoadedStamp) is { } refusal) return FileErrors.Refusal(refusal);     // stale or read-only: never planned
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        EditPlan p;
        try { p = plan(text); }
        catch (Exception ex) { return Outcome.Refused($"edit refused: {ex.Message}", RefusalCode.Io, reason: Reasons.Internal); }
        if (p.Held) return Applied(p);
        if (p.Text is null) return p.Refusal ?? Outcome.Refused("edit refused", RefusalCode.Io, reason: Reasons.Internal);
        var content = Encoding.UTF8.GetBytes(p.Text);
        if (content.Length > EventValidator.MaxFileBytes) return Outcome.Refused(TooLarge, RefusalCode.Full, reason: Reasons.Size);
        var error = file.WriteEdit(content, catalog.LoadedStamp, out _, out var uncertain);
        if (uncertain) return Uncertain(error ?? "unknown", units);
        if (error is not null) return FileErrors.Refusal(error);

        var reload = Reload(units);
        info($"{p.Done}; {reload.Human}");
        if (!reload.Ok) return reload.WithHuman($"{p.Done}; {reload.Human}");
        return Applied(p);
    }

    /// <summary>The reply of a plan the file now holds, with the checked definition's disabled reason appended.</summary>
    Outcome Applied(EditPlan p) =>
        p.CheckId is { } id && catalog.Current.Find(id)?.DisabledReason is { } reason
            ? Outcome.Done($"{p.Done}; now disabled: {reason}", p.Fields)
            : Outcome.Done(p.Done, p.Fields);

    /// <summary>A Promote threw, so the main file may be the old or the new version (event-library D19): the file is read
    /// back and memory follows it, applied (one config-changed) only when it differs from what was loaded. The reply
    /// names the count the file now holds.</summary>
    Outcome Uncertain(string reason, IUnitCatalog units)
    {
        var (result, stamp) = file.Load(units);
        if (result.FileError is not null || stamp is null)
        {
            file.WriteUncertain = true;
            warn($"events.json write uncertain ({reason}): could not read it back: {result.FileError}");
            return Outcome.Refused($"write uncertain: events.json could not be read back: {result.FileError}", RefusalCode.Io, reason: Reasons.WriteUncertain);
        }
        if (stamp != catalog.LoadedStamp) catalog.Reload(result, stamp);
        file.WriteUncertain = true;
        var n = result.Set.All.Count;
        warn($"events.json write uncertain ({reason}): reloaded {n} definitions from disk");
        return Outcome.Refused($"write uncertain: file now holds {n} definitions", RefusalCode.Io, reason: Reasons.WriteUncertain);
    }
}
