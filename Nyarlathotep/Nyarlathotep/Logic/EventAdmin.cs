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

        var node = ToNode(value);
        if (node is null) { error = $"{path} has an unsupported value"; return null; }

        var actionType = ev["action"] is JsonObject act && act["type"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null;
        if (path.StartsWith("trigger.", StringComparison.Ordinal))
            return SetTrigger(root, ev, path, node, out error);
        if (path is "action.factions" or "action.units" or "action.location")
        {
            // event-library D10, D11: the new action fields name their own action type.
            var need = path == "action.factions" ? "Empower" : "SpawnWaves";
            if (actionType != need)
            {
                error = path == "action.location" ? "location is a SpawnWaves field" : $"{path} is not {Article(actionType)} field";
                return null;
            }
            ((JsonObject)ev["action"]!)[path["action.".Length..]] = node;
            return root.ToJsonString(Write) + Environment.NewLine;
        }
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

    /// <summary>A command value as JSON: a scalar, a list of names (string[]), the units of action.units (UnitEntry[]) or
    /// a map point (PointArg). JsonNode writes each as a JSON string or number, never as raw text (event-library 10.2).</summary>
    static JsonNode? ToNode(object value) => value switch
    {
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        decimal m => JsonValue.Create(m),
        string s => JsonValue.Create(s),
        string[] list => new JsonArray(list.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        UnitEntry[] units => new JsonArray(units.Select(u => (JsonNode?)new JsonObject { ["prefab"] = u.Prefab, ["count"] = u.Count }).ToArray()),
        PointArg p => new JsonObject { ["type"] = "Point", ["x"] = p.X, ["y"] = p.Y, ["z"] = p.Z },
        _ => null,
    };

    static string Article(string? type) => type switch
    {
        null => "a missing",
        "Empower" => "an Empower",
        _ => $"a {type}",
    };

    /// <summary>trigger.type replaces the whole trigger with that type's default (Schedule Sat 20:00, GameTime night,
    /// VBloodKilled any, Manual), so no key of the old type stays; any other trigger field needs its trigger type
    /// (event-library D9).</summary>
    static string? SetTrigger(JsonNode root, JsonObject ev, string path, JsonNode node, out string? error)
    {
        error = null;
        if (path == "trigger.type")
        {
            ev["trigger"] = node.GetValue<string>() switch
            {
                "Schedule" => new JsonObject { ["type"] = "Schedule", ["days"] = new JsonArray("Sat"), ["times"] = new JsonArray("20:00") },
                "GameTime" => new JsonObject { ["type"] = "GameTime", ["phase"] = "night" },
                "VBloodKilled" => new JsonObject { ["type"] = "VBloodKilled", ["bosses"] = new JsonArray("any") },
                _ => new JsonObject { ["type"] = "Manual" },
            };
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        var need = CommandArgs.TriggerTypeOf(path);
        var trigger = ev["trigger"] as JsonObject;
        var type = trigger?["type"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null;
        if (need is null || trigger is null || type != need) { error = $"{path} needs a {need ?? "known"} trigger"; return null; }
        trigger[path["trigger.".Length..]] = node;
        return root.ToJsonString(Write) + Environment.NewLine;
    }
}

/// <summary>`.nyar event list` and `.nyar event info` (admin-only, Design › UX).</summary>
public static class EventLines
{
    public const int PageSize = 10;
    public const string NoEvents = "No events defined.";

    public static int Pages(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);

    /// <summary>The list's first line while the master switch is off (event-library D16).</summary>
    public const string MasterOff = "General.Enabled is off: nothing starts";

    /// <summary>"General.Enabled is off: nothing starts" while the master switch is off, "page p/n", then one line per
    /// event on that page, sorted by id (the set's order), each with its readiness under <paramref name="controls"/>,
    /// the ControlState an admin start would get (event-library D16).</summary>
    public static IReadOnlyList<string> List(DefinitionSet set, int page, IReadOnlyCollection<string> running, ControlState controls)
    {
        var lines = new List<string>();
        if (!controls.GeneralEnabled) lines.Add(MasterOff);
        if (set.All.Count == 0) { lines.Add(NoEvents); return lines; }
        lines.Add($"page {page}/{Pages(set.All.Count)}");
        foreach (var d in set.All.Skip((page - 1) * PageSize).Take(PageSize)) lines.Add(Line(d, running.Contains(d.Id), controls));
        return lines;
    }

    /// <summary>"&lt;id&gt; &lt;readiness&gt; &lt;pillar&gt; &lt;trigger&gt;[ RUNNING]", or "&lt;id&gt; invalid: &lt;reason&gt;"
    /// when the definition's own invalidity is the first blocker (event-library D16).</summary>
    public static string Line(EventDefinition d, bool running, ControlState controls)
    {
        var readiness = Readiness.Of(d, controls);
        return readiness.StartsWith(Readiness.Invalid, StringComparison.Ordinal)
            ? $"{d.Id} {readiness}"
            : Fit(tr => $"{d.Id} {readiness} {Lower(d.Pillar)} {tr}{(running ? " RUNNING" : "")}", d.Trigger, out _);
    }

    public static IReadOnlyList<string> Info(EventDefinition d, ActiveEvent? active, DateTime utcNow)
    {
        if (d.DisabledReason is { } reason) return [$"{d.Id} disabled: {reason}"];
        var c = d.Conditions;
        var lines = new List<string>
        {
            Fit(tr => $"{d.Id} \"{d.Name}\" {(d.Enabled ? "enabled" : "disabled")} pillar {Lower(d.Pillar)} trigger {tr} duration {d.DurationSeconds}s",
                d.Trigger, out var shortened),
        };
        if (shortened) lines.AddRange(BossLines(d.Trigger.Bosses));
        lines.Add($"conditions: minPlayers {c.MinPlayers}, cooldown {c.CooldownMinutes} min, chance {c.ChancePercent}%, " +
            $"window {(c.Window is { } w ? $"{w.From:HH\\:mm}-{w.To:HH\\:mm}" : "none")}, mode {Lower(c.Mode)}");
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
            var where = a.Location.Type == LocationType.Admin ? "at the admin"
                : FormattableString.Invariant($"at {a.Location.X:0.#} {a.Location.Z:0.#}") +
                  (a.Location.Y is { } h ? FormattableString.Invariant($" height {h:0.#}") : "");
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

    /// <summary>A line built around <paramref name="trigger"/>: the whole trigger when the line fits one chat message
    /// (Wire.MaxBytes), else a vbloodkilled trigger as "vbloodkilled &lt;n&gt; bosses", since AdminLines.Pack would cut
    /// the line (event-library A21). <paramref name="shortened"/> says the names were left out.</summary>
    public static string Fit(Func<string, string> build, Trigger trigger, out bool shortened)
    {
        var full = build(Trigger(trigger));
        shortened = trigger.Type == TriggerType.VBloodKilled && !Fits(full);
        return shortened ? build($"vbloodkilled {trigger.Bosses.Count} bosses") : full;
    }

    /// <summary>"bosses: a,b,…" lines, each within one chat message, together naming every boss once, in order.</summary>
    public static IReadOnlyList<string> BossLines(IReadOnlyList<string> bosses)
    {
        const string Head = "bosses: ";
        var lines = new List<string>();
        var current = Head;
        foreach (var boss in bosses)
        {
            var next = current.Length == Head.Length ? current + boss : current + "," + boss;
            if (!Fits(next) && current.Length > Head.Length) { lines.Add(current); next = Head + boss; }
            current = next;
        }
        if (current.Length > Head.Length) lines.Add(current);
        return lines;
    }

    static bool Fits(string line) => System.Text.Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes;

    static string Lower<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();
}

/// <summary>A definition's readiness (event-library D16, S-14): the first blocker <see cref="Precedence.StartBlocker"/>
/// reports for it, mapped one to one to a label. Readiness keeps no order of its own, so the column and a start refusal
/// always name the same cause.</summary>
public static class Readiness
{
    public const string Ready = "ready";
    public const string Purge = "off (purge)";
    public const string Mod = "off (mod)";
    public const string PillarOff = "off (pillar)";
    public const string Cap = "full (cap)";
    public const string Invalid = "invalid: ";
    public const string EventOff = "off (event)";

    public static string Of(EventDefinition d, ControlState controls) => Label(d, Precedence.StartBlocker(d, controls));

    /// <summary>The label of one StartBlocker reply for <paramref name="d"/>; an unknown reply throws, so a new
    /// blocker cannot go unlabelled.</summary>
    public static string Label(EventDefinition d, string? blocker)
    {
        if (blocker is null) return Ready;
        if (blocker == Precedence.PurgeCooldown) return Purge;
        if (blocker == "General.Enabled is false") return Mod;
        if (blocker == $"pillar {d.Pillar.ToString().ToLowerInvariant()} is off") return PillarOff;
        if (blocker == "skipped by MaxConcurrentEvents") return Cap;
        if (d.DisabledReason is { } reason && blocker == $"event {d.Id} is disabled: {reason}") return Invalid + reason;
        if (blocker == $"event {d.Id} is disabled") return EventOff;
        throw new ArgumentException($"no readiness label for the start blocker '{blocker}'", nameof(blocker));
    }
}
