#nullable enable
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nyarlathotep.Logic;

/// <summary>The planners of the chat writes that add or remove a definition (event-library D5-D8, Business rules 3-5):
/// each takes events.json's text and returns the <see cref="EditPlan"/> DefinitionEditor.Write applies, so every one is
/// a file edit plus a reload. A new definition is always written `"enabled": false`.</summary>
public static class Authoring
{
    static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Limit => $"events.json holds {EventValidator.MaxDefinitions} definitions, the limit";
    public const string IdRule = "id must be 1-32 of a-z 0-9 -";

    /// <summary>`.nyar template use &lt;t&gt; [as &lt;id&gt;]` (D5): a disabled copy of template t under id (t by
    /// default), its name kept.</summary>
    public static EditPlan TemplateUse(string text, TemplateCatalog catalog, string t, string? asId)
    {
        if (catalog.Error is not null) return EditPlan.Refuse(Outcome.Refused(TemplateLines.CatalogueUnavailable, RefusalCode.Io, reason: Reasons.Read));
        var entry = catalog.Find(t);
        if (entry is null) return EditPlan.Refuse(Outcome.Refused(TemplateLines.UnknownTemplate(t), RefusalCode.NotFound, "template"));
        if (entry.Invalid is { } reason)
            return EditPlan.Refuse(Outcome.Refused($"template {t} is invalid: {reason}", RefusalCode.Invalid, "template", reason: Reasons.Template));
        var id = asId ?? t;
        if (CommandArgs.EventId(id).Error is not null) return EditPlan.Refuse(BadId("id"));
        var copy = Clone(entry.Json);
        copy["id"] = id;
        copy["enabled"] = false;
        return Append(text, copy, id,
            $"template {t} added as {id} (disabled); .nyar event enable {id} to arm it",
            $"event {id} already exists; use .nyar template use {t} as new-id", "id", ("tpl", t));
    }

    /// <summary>`.nyar event new &lt;id&gt; &lt;pillar&gt;` (D6): a disabled skeleton named after its id, Manual, 600 s;
    /// an Empower on Faction_Bandits with physicalPower 1.2 for empowerment, else CHAR_Bandit_Thug × 3 in 1 wave around
    /// the admin.</summary>
    public static EditPlan New(string text, string id, string pillarName)
    {
        if (!PillarNames.TryParse(pillarName, out var pillar)) return EditPlan.Refuse(Outcome.Refused(PillarNames.Unknown(pillarName), RefusalCode.NotFound, "pillar"));
        if (CommandArgs.EventId(id).Error is not null) return EditPlan.Refuse(BadId("id"));
        var name = PillarNames.Name(pillar);
        var action = pillar == Pillar.Empowerment
            ? new JsonObject
            {
                ["type"] = "Empower",
                ["factions"] = new JsonArray("Faction_Bandits"),
                ["stats"] = new JsonObject { ["physicalPower"] = 1.2m },
            }
            : new JsonObject
            {
                ["type"] = "SpawnWaves",
                ["units"] = new JsonArray(new JsonObject { ["prefab"] = "CHAR_Bandit_Thug", ["count"] = 3 }),
                ["waves"] = 1,
                ["intervalSeconds"] = 60,
                ["radius"] = 8,
                ["location"] = new JsonObject { ["type"] = "Admin" },
            };
        var def = new JsonObject
        {
            ["id"] = id,
            ["name"] = id,
            ["enabled"] = false,
            ["pillar"] = name,
            ["trigger"] = new JsonObject { ["type"] = "Manual" },
            ["durationSeconds"] = 600,
            ["action"] = action,
        };
        return Append(text, def, id, $"event {id} created (disabled, {name}); set its fields with .nyar event set", $"event {id} already exists", "id",
            ("pillar", name));
    }

    /// <summary>`.nyar event copy &lt;id&gt; &lt;newId&gt;` (D7): the source's JSON verbatim under newId, disabled. An
    /// invalid source is copied as it is; the reload re-derives its reason for the copy.</summary>
    public static EditPlan Copy(string text, string id, string newId)
    {
        if (CommandArgs.EventId(newId).Error is not null) return EditPlan.Refuse(BadId("newId"));
        if (!TryParse(text, out _, out var events, out var error)) return EditPlan.Refuse(error!);
        var source = events.OfType<JsonObject>().FirstOrDefault(e => IdOf(e) == id);
        if (source is null) return EditPlan.Refuse(AdminLines.UnknownEvent(id));
        var copy = Clone(source);
        copy["id"] = newId;
        copy["enabled"] = false;
        return Append(text, copy, newId, $"event {id} copied to {newId} (disabled)", $"event {newId} already exists", "newId", ("from", id));
    }

    /// <summary>The delete of D8, after its confirm: every definition with that id leaves the file.</summary>
    public static EditPlan Delete(string text, string id)
    {
        if (!TryParse(text, out var root, out var events, out var error)) return EditPlan.Refuse(error!);
        var doomed = events.OfType<JsonObject>().Where(e => IdOf(e) == id).ToList();
        if (doomed.Count == 0) return EditPlan.Refuse(AdminLines.UnknownEvent(id));
        foreach (var d in doomed) events.Remove(d);
        return new EditPlan(root!.ToJsonString(Write) + Environment.NewLine, null, Deleted(id), null, ("done", "1"));
    }

    public static string Deleted(string id) => $"event {id} deleted (events.json.bak keeps the previous file)";

    /// <summary>Appends <paramref name="def"/> unless its id exists (exists, arg <paramref name="idArg"/>) or the file
    /// already holds the 200 definitions of foundation's MaxDefinitions.</summary>
    static EditPlan Append(string text, JsonObject def, string id, string done, string exists, string idArg, params (string Key, string Value)[] fields)
    {
        if (!TryParse(text, out var root, out var events, out var error)) return EditPlan.Refuse(error!);
        if (events.OfType<JsonObject>().Any(e => IdOf(e) == id)) return EditPlan.Refuse(Outcome.Refused(exists, RefusalCode.Exists, idArg));
        if (events.Count >= EventValidator.MaxDefinitions) return EditPlan.Refuse(Outcome.Refused(Limit, RefusalCode.Full, reason: Reasons.Count));
        events.Add(def);
        return new EditPlan(root!.ToJsonString(Write) + Environment.NewLine, null, done, id, fields);
    }

    /// <summary>The id rule's refusal (A13: badarg, arg id or newId).</summary>
    static Outcome BadId(string arg) => Outcome.Refused(IdRule, RefusalCode.BadArg, arg);

    static bool TryParse(string text, out JsonNode? root, out JsonArray events, out Outcome? error)
    {
        events = [];
        error = null;
        try { root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { root = null; error = FileErrors.Refusal("events.json does not parse; fix it and run .nyar event reload"); return false; }
        if (root?["events"] is not JsonArray arr) { error = FileErrors.Refusal("events.json has no events array"); return false; }
        events = arr;
        return true;
    }

    /// <summary>A detached copy (System.Text.Json of net6 has no DeepClone).</summary>
    static JsonObject Clone(JsonObject o) => (JsonObject)JsonNode.Parse(o.ToJsonString())!;

    static string? IdOf(JsonObject e) => e["id"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>`.nyar event delete &lt;id&gt; [confirm]` (event-library D8, D19; Business rules 5): the first call arms a
/// delete for 30 s and writes nothing; the same admin's confirm writes events.json without the definition, then drops
/// its cooldown row and writes state.json. A running event is never deleted. When the state.json write fails the
/// definition stays deleted and the row leaves the file at the next successful save.</summary>
public sealed class EventDeleter(DeleteArming arming, DefinitionEditor editor, EventCatalog catalog, Func<string, bool> isRunning,
    StateStore state, Action<string> log)
{
    public const string RowLeft = "cooldown row left, cleared on the next save";
    public const string RowKept = "state.json is read-only; its cooldown row is kept";

    public static string Running(string id) => $"event {id} is running; stop it first";
    public static string NotPending(string id) => $"no delete pending for {id}";

    /// <summary>`.nyar event delete &lt;id&gt;`: arms and prompts; writes nothing.</summary>
    public Outcome Request(ulong adminId, string id, DateTime utcNow)
    {
        if (catalog.Current.Find(id) is null) return AdminLines.UnknownEvent(id);
        if (isRunning(id)) return RunningRefusal(id);
        arming.Arm(adminId, id, utcNow);
        return Outcome.Done($"delete {id}? run .nyar event delete {id} confirm within 30 s", ("confirm", "30"));
    }

    static Outcome RunningRefusal(string id) => Outcome.Refused(Running(id), RefusalCode.State, "id", reason: Reasons.Running);

    /// <summary>`.nyar event delete &lt;id&gt; confirm`: looks the event up first, then needs this admin's arming of this
    /// id within 30 s; a running event is refused and the arming kept, so a stop and a confirm inside the window delete.</summary>
    public Outcome Confirm(ulong adminId, string id, DateTime utcNow, IUnitCatalog units)
    {
        if (catalog.Current.Find(id) is null) { arming.Disarm(adminId, id); return AdminLines.UnknownEvent(id); }
        if (!arming.IsArmed(adminId, id, utcNow)) return Outcome.Refused(NotPending(id), RefusalCode.Confirm);
        if (isRunning(id)) return RunningRefusal(id);
        arming.Disarm(adminId, id);
        var reply = editor.Write(text => Authoring.Delete(text, id), units);
        if (catalog.Current.Find(id) is not null) return reply;          // refused or not written: nothing else changes

        if (!state.Document.LastStart.ContainsKey(id)) return reply;
        if (state.ReadOnly)                                              // never written: the row stays, in memory too
        {
            log($"delete {id}: {RowKept}");
            return reply.WithHuman($"{reply.Human}; {RowKept}");
        }
        state.Document.LastStart.Remove(id);
        var error = state.SaveNow();
        if (error is null) return reply;
        log($"delete {id}: {RowLeft} ({error})");
        return reply.WithHuman($"{reply.Human}; {RowLeft}");
    }
}
