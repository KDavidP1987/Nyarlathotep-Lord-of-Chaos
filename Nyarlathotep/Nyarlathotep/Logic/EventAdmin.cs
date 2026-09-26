#nullable enable
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nyarlathotep.Logic;

/// <summary>The edit behind `.nyar event set`, `enable` and `disable` (Design › UX, S-10): one field of the first event
/// with that id changes, and everything else in the file is kept. The file is re-indented; the previous version stays
/// as events.json.bak. The caller validates the value (CommandArgs.SettableValue) and reloads after the write, so an
/// edit that makes the event invalid disables it with its reason like any other (D23).</summary>
public static class EventsEditor
{
    static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        // Names may hold non-ASCII letters; '<' and '>' never pass validation, so relaxed escaping stays safe here.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The new file text, or null with the reply line in <paramref name="error"/>. <paramref name="path"/> is
    /// "enabled", "name", "durationSeconds", "conditions.&lt;key&gt;", "action.&lt;key&gt;" or "action.stats.&lt;stat&gt;";
    /// a missing conditions or stats object is created, a missing action is an error. A field of the other action type
    /// is refused, and so is a stat set that would leave no stat above 1.0 (faction-empowerment D12).</summary>
    public static string? Apply(string text, string id, string path, object value, out string? error)
    {
        error = null;
        JsonNode? root;
        try { root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { error = "events.json does not parse; fix it and run .nyar event reload"; return null; }

        if (root?["events"] is not JsonArray events) { error = "events.json has no events array"; return null; }
        var ev = events.OfType<JsonObject>().FirstOrDefault(e => e["id"] is JsonValue v && v.TryGetValue<string>(out var s) && s == id);
        if (ev is null) { error = $"unknown event {id}"; return null; }

        var node = value switch
        {
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            decimal m => JsonValue.Create(m),
            string s => (JsonNode?)JsonValue.Create(s),
            _ => null,
        };
        if (node is null) { error = $"{path} has an unsupported value"; return null; }

        var actionType = ev["action"] is JsonObject act && act["type"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null;
        var isStat = path.StartsWith("action.stats.", StringComparison.Ordinal);
        if (isStat && actionType != "Empower") { error = $"{path} is not a field of a {actionType ?? "missing"} action"; return null; }
        if (CommandArgs.WaveFields.Contains(path) && actionType == "Empower") { error = $"{path} is not a field of an Empower action"; return null; }

        var parts = path.Split('.');
        JsonObject target = ev;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (target[parts[i]] is not JsonObject child)
            {
                if (parts[i] is not ("conditions" or "stats")) { error = $"event {id} has no {parts[i]}"; return null; }
                child = new JsonObject();
                target[parts[i]] = child;
            }
            target = child;
        }
        target[parts[^1]] = node;

        if (isStat && !target.Any(p => p.Value is JsonValue v && v.TryGetValue<decimal>(out var m) && m > 1.0m))
        {
            error = "action.stats must raise at least one stat above 1.0";
            return null;
        }
        return root.ToJsonString(Write) + Environment.NewLine;
    }
}

/// <summary>`.nyar event list` and `.nyar event info` (admin-only, Design › UX).</summary>
public static class EventLines
{
    public const int PageSize = 10;
    public const string NoEvents = "No events defined.";

    public static int Pages(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);

    /// <summary>"page p/n" then one line per event on that page, sorted by id (the set's order).</summary>
    public static IReadOnlyList<string> List(DefinitionSet set, int page, IReadOnlyCollection<string> running)
    {
        if (set.All.Count == 0) return [NoEvents];
        var lines = new List<string> { $"page {page}/{Pages(set.All.Count)}" };
        foreach (var d in set.All.Skip((page - 1) * PageSize).Take(PageSize)) lines.Add(Line(d, running.Contains(d.Id)));
        return lines;
    }

    public static string Line(EventDefinition d, bool running) =>
        d.DisabledReason is { } reason
            ? $"{d.Id} disabled: {reason}"
            : $"{d.Id} {(d.Enabled ? "enabled" : "disabled")} {Lower(d.Pillar)} {Trigger(d.Trigger)}{(running ? " RUNNING" : "")}";

    public static IReadOnlyList<string> Info(EventDefinition d, ActiveEvent? active, DateTime utcNow)
    {
        if (d.DisabledReason is { } reason) return [$"{d.Id} disabled: {reason}"];
        var c = d.Conditions;
        var lines = new List<string>
        {
            $"{d.Id} \"{d.Name}\" {(d.Enabled ? "enabled" : "disabled")} pillar {Lower(d.Pillar)} trigger {Trigger(d.Trigger)} duration {d.DurationSeconds}s",
            $"conditions: minPlayers {c.MinPlayers}, cooldown {c.CooldownMinutes} min, chance {c.ChancePercent}%, " +
            $"window {(c.Window is { } w ? $"{w.From:HH\\:mm}-{w.To:HH\\:mm}" : "none")}, mode {Lower(c.Mode)}",
        };
        if (d.Empower is { } emp)
        {
            var s = emp.Stats;
            var raised = new (string Name, double Value)[]
                {
                    ("physicalPower", s.PhysicalPower), ("spellPower", s.SpellPower), ("maxHealth", s.MaxHealth),
                    ("attackSpeed", s.AttackSpeed), ("moveSpeed", s.MoveSpeed),
                }
                .Where(x => x.Value > 1.0)
                .Select(x => FormattableString.Invariant($"{x.Name} x{x.Value:0.##}"));
            lines.Add("action: empower " + string.Join(", ", emp.Factions.Select(FactionDenyList.ShortName)) +
                (emp.IncludeUnits.Count > 0 ? $", also {string.Join(", ", emp.IncludeUnits)}" : "") +
                (emp.ExcludeUnits.Count > 0 ? $", not {string.Join(", ", emp.ExcludeUnits)}" : "") +
                (emp.IncludeVBloods ? ", V Bloods included" : "") +
                ": " + string.Join(", ", raised));
        }
        if (d.Action is { } a)
        {
            var where = a.Location.Type == LocationType.Admin ? "at the admin" : FormattableString.Invariant($"at {a.Location.X:0.#} {a.Location.Z:0.#}");
            lines.Add($"action: {a.Waves} waves every {a.IntervalSeconds}s, radius {a.Radius}, {where}, units " +
                string.Join(", ", a.Units.Select(u => $"{u.Count} {u.Prefab}")) +
                (a.UnitLifetimeSeconds is { } l ? $", unit lifetime {l}s" : ""));
        }
        lines.Add(active is null
            ? "not running"
            : $"running: started by {active.Trigger}, {Math.Max(0, (int)Math.Ceiling((active.Instance.EndsUtc - utcNow).TotalSeconds))}s left" +
              (d.Empower is null ? $", wave {active.WavesSpawned}/{d.Action?.Waves ?? 0}" : ""));
        return lines;
    }

    public static string Trigger(Trigger t) => t.Type switch
    {
        TriggerType.Schedule => $"schedule {string.Join(",", t.Days.Select(d => d.ToString()[..3]))} {string.Join(",", t.Times.Select(x => x.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)))}",
        TriggerType.GameTime => $"gametime {Lower(t.Phase)}",
        TriggerType.VBloodKilled => $"vbloodkilled {string.Join(",", t.Bosses)}",
        _ => "manual",
    };

    static string Lower<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();
}
