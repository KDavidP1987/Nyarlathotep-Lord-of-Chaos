#nullable enable
using System.Collections.Generic;
using System.Text;
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

    /// <summary>True when the first event with that id holds `"enabled": <paramref name="on"/>` (raphael-api-admin D5); false
    /// when the text does not parse or the event or its flag is missing, so Apply gives the refusal.</summary>
    public static bool Holds(string text, string id, bool on)
    {
        try
        {
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var ev = (root?["events"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(e => e["id"] is JsonValue v && v.TryGetValue<string>(out var s) && s == id);
            return ev?["enabled"] is JsonValue flag && flag.TryGetValue<bool>(out var held) && held == on;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>The new file text, or null with the reply line in <paramref name="error"/>. <paramref name="path"/> is
    /// "enabled", "name", "durationSeconds", "conditions.&lt;key&gt;", "action.&lt;key&gt;" or "action.stats.&lt;stat&gt;";
    /// a missing conditions or stats object is created, a missing action is an error. A field of the other action type
    /// is refused, and so is a stat set that would leave no stat above 1.0 (faction-empowerment D12).</summary>
    public static string? Apply(string text, string id, string path, object value, out Outcome? error) =>
        Apply(text, id, path, value, out error, out _);

    /// <summary><see cref="Apply(string, string, string, object, out Outcome?)"/> with <paramref name="note"/>, the
    /// text a wave-list conversion adds to the reply (wave-sets D14: the keys it removed).</summary>
    public static string? Apply(string text, string id, string path, object value, out Outcome? error, out string? note)
    {
        error = null;
        note = null;
        JsonNode? root;
        try { root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { error = FileErrors.Refusal("events.json does not parse; fix it and run .nyar event reload"); return null; }

        if (root?["events"] is not JsonArray events) { error = FileErrors.Refusal("events.json has no events array"); return null; }
        var ev = events.OfType<JsonObject>().FirstOrDefault(e => e["id"] is JsonValue v && v.TryGetValue<string>(out var s) && s == id);
        if (ev is null) { error = AdminLines.UnknownEvent(id); return null; }

        var actionType = ev["action"] is JsonObject act && act["type"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null;
        if (path == "location" && value is AroundPlayerArg) path = "action.location";     // event-spawns D18
        if (CommandArgs.SpawnKeyFields.Contains(CommandArgs.TableName(path)) || path == CommandArgs.FanOutField)
            return SetSpawnKey(root, ev, actionType, path, value, out error);
        if (path == CommandArgs.ScoreboardField || CommandArgs.WaveListPath(path) is not null)
            return SetWaveList(root, ev, actionType, path, value, out error, out note);

        if (path == "action.location" && value is PointArg && ev["action"] is JsonObject fanned && fanned["fanOut"] is not null)
        {
            error = Invalid($"{EventValidator.FanOutLocation}: set action.fanOut none first", Reasons.Field);   // automation D16
            return null;
        }
        var node = ToNode(value);
        if (node is null) { error = Invalid($"{path} has an unsupported value", Reasons.Value); return null; }
        if (path.StartsWith("trigger.", StringComparison.Ordinal))
            return SetTrigger(root, ev, path, node, out error);
        if (path is "action.factions" or "action.units" or "action.location")
        {
            // event-library D10, D11: the new action fields name their own action type.
            var need = path == "action.factions" ? "Empower" : "SpawnWaves";
            if (actionType != need)
            {
                error = Invalid(path == "action.location" ? "location is a SpawnWaves field" : $"{path} is not {Article(actionType)} field", Reasons.Field);
                return null;
            }
            var action = (JsonObject)ev["action"]!;
            if (path == "action.units") KeepChances(action["units"] as JsonArray, (JsonArray)node);
            action[path["action.".Length..]] = node;
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        var isStat = path.StartsWith("action.stats.", StringComparison.Ordinal);
        if (isStat && actionType != "Empower") { error = Invalid($"{path} is not a field of a {actionType ?? "missing"} action", Reasons.Field); return null; }
        if (CommandArgs.WaveFields.Contains(path) && actionType == "Empower") { error = Invalid($"{path} is not a field of an Empower action", Reasons.Field); return null; }

        var parts = path.Split('.');
        JsonObject target = ev;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (target[parts[i]] is not JsonObject child)
            {
                if (parts[i] is not ("conditions" or "stats")) { error = Outcome.Refused($"event {id} has no {parts[i]}", RefusalCode.NotFound, "field"); return null; }
                child = new JsonObject();
                target[parts[i]] = child;
            }
            target = child;
        }
        target[parts[^1]] = node;

        if (isStat && !target.Any(p => p.Value is JsonValue v && v.TryGetValue<decimal>(out var m) && m > 1.0m))
        {
            error = Invalid("action.stats must raise at least one stat above 1.0", Reasons.Stats);
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
        AroundPlayerArg a => new JsonObject { ["type"] = "AroundPlayer", ["minDist"] = a.MinDist, ["maxDist"] = a.MaxDist },
        BehaviourArg b => new JsonObject { ["type"] = "Hunt", ["range"] = b.Range },
        FanOutArg f => new JsonObject { ["maxInstances"] = f.MaxInstances, ["minSpacing"] = f.MinSpacing },
        _ => null,
    };

    /// <summary>An event-spawns field (D18) of a SpawnWaves action, refused on any other: a unit's chance on an existing
    /// entry; a modifier into action.modifiers (created when missing; "none" removes the key, and the object once it is
    /// empty), never level beside levelDelta; loot, allowTerritory and behaviour on the action ("none" removes it).</summary>
    static string? SetSpawnKey(JsonNode root, JsonObject ev, string? actionType, string path, object value, out Outcome? error)
    {
        error = null;
        if (actionType != "SpawnWaves" || ev["action"] is not JsonObject action)
        {
            error = Invalid($"{CommandArgs.TableName(path)} is not {Article(actionType)} field", Reasons.Field);
            return null;
        }
        if (CommandArgs.UnitChanceIndex(path) is { } n)
        {
            if (action["units"] is not JsonArray units || n > units.Count || units[n - 1] is not JsonObject unit)
            {
                error = Invalid($"action.units has no entry {n}", Reasons.Field);
                return null;
            }
            unit["chance"] = ToNode(value);
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        var key = path["action.".Length..];
        if (path == CommandArgs.FanOutField && value is not FieldRemoval
            && !(action["location"] is JsonObject loc && loc["type"] is JsonValue lt && lt.TryGetValue<string>(out var locType) && locType == "AroundPlayer"))
        {
            error = Invalid(EventValidator.FanOutLocation, Reasons.Field);          // automation D16
            return null;
        }
        if (key.StartsWith("modifiers.", StringComparison.Ordinal))
        {
            var name = key["modifiers.".Length..];
            var mods = action["modifiers"] as JsonObject;
            if (value is FieldRemoval)
            {
                mods?.Remove(name);
                if (mods is { Count: 0 }) action.Remove("modifiers");         // an empty modifiers object is refused (D6)
                return root.ToJsonString(Write) + Environment.NewLine;
            }
            var other = name switch { "level" => "levelDelta", "levelDelta" => "level", _ => null };
            if (other is not null && mods?[other] is not null)
            {
                error = Invalid($"{EventValidator.BothLevels}: set action.modifiers.{other} none first", Reasons.Value);
                return null;
            }
            if (mods is null) action["modifiers"] = mods = new JsonObject();
            mods[name] = ToNode(value);
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        if (value is FieldRemoval) action.Remove(key);
        else action[key] = ToNode(value);
        return root.ToJsonString(Write) + Environment.NewLine;
    }

    /// <summary>The wave-sets fields (D14) of a SpawnWaves action, refused on any other. `action.scoreboard` is set on the
    /// action. On a units-form action only `action.waveList.1.units` is taken: it converts the action, its units becoming
    /// wave 1 with the action's modifiers copied onto each entry, and units, waves, intervalSeconds and modifiers removed
    /// and named in <paramref name="note"/>. On a waveList: wave n up to its length + 1 (n = length + 1 appends a wave and
    /// takes only its units); `action.waveList.N none` removes the wave (never the last one, and a new wave 1 loses its
    /// start keys); wave 1 takes no start key and a wave never ends with whenCleared false and no afterSeconds (D1, A1);
    /// a unit's modifier follows SetSpawnKey's rules on that entry's modifiers.</summary>
    static string? SetWaveList(JsonNode root, JsonObject ev, string? actionType, string path, object value, out Outcome? error, out string? note)
    {
        error = null;
        note = null;
        string Done() => root.ToJsonString(Write) + Environment.NewLine;
        if (actionType != "SpawnWaves" || ev["action"] is not JsonObject action)
        {
            error = Invalid($"{CommandArgs.TableName(path)} is not {Article(actionType)} field", Reasons.Field);
            return null;
        }
        if (path == CommandArgs.ScoreboardField)
        {
            action["scoreboard"] = ToNode(value);
            return Done();
        }
        var f = CommandArgs.WaveListPath(path)!;
        if (action["waveList"] is not JsonArray list)
        {
            if (f.Wave != 1 || f.Key != "units" || f.Unit is not null)
            {
                error = Invalid("a units-form event takes action.waveList.1.units first (it converts the event)", Reasons.Field);
                return null;
            }
            var units = (JsonArray)ToNode(value)!;
            if (action["modifiers"] is JsonObject shared)
                foreach (var entry in units.OfType<JsonObject>()) entry["modifiers"] = JsonNode.Parse(shared.ToJsonString());
            var dropped = EventValidator.UnitsFormKeys.Where(k => action[k] is not null).ToList();
            foreach (var k in dropped) action.Remove(k);
            action["waveList"] = new JsonArray(new JsonObject { ["units"] = units });
            note = dropped.Count == 0 ? "converted to a wave list"
                : $"converted to a wave list; removed {string.Join(", ", dropped.Select(k => "action." + k))}";
            return Done();
        }
        if (f.Wave > list.Count + 1 || (f.Wave == list.Count + 1 && !(f.Key == "units" && f.Unit is null)))
        {
            error = Invalid($"action.waveList has {list.Count} waves; the next one is {list.Count + 1} (set its units first)", Reasons.Field);
            return null;
        }
        if (f.Key is null)
        {
            if (list.Count == 1)
            {
                error = Invalid("action.waveList keeps at least one wave", Reasons.Field);
                return null;
            }
            list.RemoveAt(f.Wave - 1);
            if (list[0] is JsonObject first) { first.Remove("afterSeconds"); first.Remove("whenCleared"); }
            return Done();
        }
        JsonObject wave;
        if (f.Wave == list.Count + 1) list.Add(wave = new JsonObject());
        else if (list[f.Wave - 1] is JsonObject w) wave = w;
        else { error = Invalid($"action.waveList.{f.Wave} is not an object", Reasons.Field); return null; }
        switch (f.Key)
        {
            case "units" when f.Unit is null:
                wave["units"] = ToNode(value);
                return Done();
            case "afterSeconds" or "whenCleared":
                if (f.Wave == 1) { error = Invalid(EventValidator.FirstWaveStart, Reasons.Field); return null; }
                if (value is FieldRemoval) wave.Remove(f.Key);
                else wave[f.Key] = ToNode(value);
                if (wave["whenCleared"] is JsonValue wc && wc.TryGetValue<bool>(out var c) && !c && wave["afterSeconds"] is null)
                {
                    error = Invalid(EventValidator.NoStartRule(f.Wave), Reasons.Value);
                    return null;
                }
                return Done();
        }
        if (wave["units"] is not JsonArray entries || f.Unit > entries.Count || entries[f.Unit!.Value - 1] is not JsonObject unit)
        {
            error = Invalid($"action.waveList.{f.Wave}.units has no entry {f.Unit}", Reasons.Field);
            return null;
        }
        var mods = unit["modifiers"] as JsonObject;
        var name = f.Modifier!;
        if (value is FieldRemoval)
        {
            mods?.Remove(name);
            if (mods is { Count: 0 }) unit.Remove("modifiers");               // an empty modifiers object is refused (D1)
            return Done();
        }
        var other = name switch { "level" => "levelDelta", "levelDelta" => "level", _ => null };
        if (other is not null && mods?[other] is not null)
        {
            var at = $"action.waveList.{f.Wave}.units.{f.Unit}.modifiers";
            error = Invalid($"{EventValidator.BothLevels.Replace("action.modifiers", at)}: set {at.Replace(".modifiers", "")}.{other} none first", Reasons.Value);
            return null;
        }
        if (mods is null) unit["modifiers"] = mods = new JsonObject();
        mods[name] = ToNode(value);
        return Done();
    }

    /// <summary>A new action.units list keeps the chance of each prefab the old list gave one (event-spawns D18), so
    /// resetting the units does not silently drop a chance.</summary>
    static void KeepChances(JsonArray? old, JsonArray now)
    {
        if (old is null) return;
        foreach (var entry in now.OfType<JsonObject>())
        {
            var prefab = entry["prefab"]?.GetValue<string>();
            var was = old.OfType<JsonObject>().FirstOrDefault(o => o["prefab"] is JsonValue v && v.TryGetValue<string>(out var s) && s == prefab);
            if (was?["chance"] is JsonNode chance) entry["chance"] = JsonNode.Parse(chance.ToJsonString());      // net6 has no DeepClone
        }
    }

    /// <summary>A value the field refuses (raphael-api-admin Business rules 3: invalid, arg field).</summary>
    static Outcome Invalid(string human, string reason) => Outcome.Refused(human, RefusalCode.Invalid, "field", reason: reason);

    static string Article(string? type) => type switch
    {
        null => "a missing",
        "Empower" => "an Empower",
        _ => $"a {type}",
    };

    /// <summary>trigger.type replaces the whole trigger with that type's default (Schedule Sat 20:00, GameTime night,
    /// VBloodKilled any, Interval 60-90 min, RegionEntered with a 30 min cooldown and no scope yet, FactionKills
    /// Faction_Bandits 20 in 300 s, Manual), so no key of the old type stays, the scope included (regions D9); trigger.scope fits
    /// every type; any other trigger field needs its trigger type (event-library D9).</summary>
    static string? SetTrigger(JsonNode root, JsonObject ev, string path, JsonNode node, out Outcome? error)
    {
        error = null;
        if (path == "trigger.type")
        {
            ev["trigger"] = node.GetValue<string>() switch
            {
                "Schedule" => new JsonObject { ["type"] = "Schedule", ["days"] = new JsonArray("Sat"), ["times"] = new JsonArray("20:00") },
                "GameTime" => new JsonObject { ["type"] = "GameTime", ["phase"] = "night" },
                "VBloodKilled" => new JsonObject { ["type"] = "VBloodKilled", ["bosses"] = new JsonArray("any") },
                "Interval" => new JsonObject { ["type"] = "Interval", ["minMinutes"] = 60, ["maxMinutes"] = 90 },     // automation D16
                "RegionEntered" => new JsonObject { ["type"] = "RegionEntered", ["playerCooldownMinutes"] = EventValidator.DefaultPlayerCooldownMinutes },
                "FactionKills" => new JsonObject
                {
                    ["type"] = "FactionKills", ["factions"] = new JsonArray("Faction_Bandits"), ["kills"] = 20, ["windowSeconds"] = 300,
                },
                _ => new JsonObject { ["type"] = "Manual" },
            };
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        var trigger = ev["trigger"] as JsonObject;
        if (path == "trigger.scope")                                   // regions D9: every trigger type takes a scope
        {
            if (trigger is null) { error = Invalid($"{path} needs a trigger", Reasons.Trigger); return null; }
            trigger["scope"] = node;
            return root.ToJsonString(Write) + Environment.NewLine;
        }
        var need = CommandArgs.TriggerTypeOf(path);
        var type = trigger?["type"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null;
        if (need is null || trigger is null || type != need) { error = Invalid($"{path} needs {(need is "Interval" ? "an" : "a")} {need ?? "known"} trigger", Reasons.Trigger); return null; }
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
            : Fit(tr => $"{d.Id} {readiness} {Lower(d.Pillar)} {tr}{RegionSuffix(d)}{(running ? " RUNNING" : "")}", d.Trigger, out _);
    }

    /// <summary>" [names]" for an event with a regional trigger or action scope, the names of both in order, once each;
    /// empty for a Global one (regions D9).</summary>
    public static string RegionSuffix(EventDefinition d)
    {
        var names = RegionLines.ScopeOf(d).SelectMany(s => s.Regions).Distinct(StringComparer.Ordinal).ToList();
        return names.Count == 0 ? "" : $" [{string.Join(",", names)}]";
    }

    /// <summary>`event info`. <paramref name="nextInterval"/> is an Interval definition's stored next start (automation D16):
    /// "next start in &lt;m&gt; min" while it waits, "next start: after the running instance ends" while it runs.</summary>
    public static IReadOnlyList<string> Info(EventDefinition d, ActiveEvent? active, DateTime utcNow, DateTime? nextInterval = null)
    {
        if (d.DisabledReason is { } reason) return [$"{d.Id} disabled: {reason}"];
        var c = d.Conditions;
        var lines = new List<string>
        {
            Fit(tr => $"{d.Id} \"{d.Name}\" {(d.Enabled ? "enabled" : "disabled")} pillar {Lower(d.Pillar)} trigger {tr} duration {d.DurationSeconds}s",
                d.Trigger, out var shortened),
        };
        if (shortened) lines.AddRange(d.Trigger.Type == TriggerType.FactionKills ? FactionLines(d.Trigger.Factions ?? []) : BossLines(d.Trigger.Bosses));
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
            var where = a.Location.Type switch
            {
                LocationType.Admin => "at the admin",
                LocationType.AroundPlayer => $"around a player {a.Location.MinDist}-{a.Location.MaxDist} m",     // event-spawns D16
                _ => FormattableString.Invariant($"at {a.Location.X:0.#} {a.Location.Z:0.#}") +
                     (a.Location.Y is { } h ? FormattableString.Invariant($" height {h:0.#}") : ""),
            };
            var life = a.UnitLifetimeSeconds is { } l ? $", unit lifetime {l}s" : "";
            if (a.WaveList is { } list)
            {
                lines.Add($"action: {a.Waves} waves, radius {a.Radius}, {where}{life}");
                lines.AddRange(WaveLines(list));                                                 // wave-sets D15
                lines.Add($"scoreboard: {(a.Scoreboard ? "on" : "off")}");
            }
            else
            {
                var head = $"action: {a.Waves} waves every {WaveSchedule.IntervalOf(a)}s, radius {a.Radius}, {where}";
                var units = a.Units.Select(u => $"{u.Count} {u.Prefab}").ToList();
                var one = $"{head}, units {string.Join(", ", units)}{life}";
                if (Encoding.UTF8.GetByteCount(one) <= Wire.MaxBytes) lines.Add(one);
                else
                {
                    // Ten long prefab names overflow one chat line (event-spawns D32): the units follow on lines of their own.
                    lines.Add($"{head}{life}, units below");
                    lines.AddRange(PackList("units: ", units));
                }
            }
            if (SpawnKeys(a) is { } keys) lines.Add(keys);
        }
        lines.Add($"trigger scope: {d.Trigger.Scope}");                 // regions D9
        lines.Add($"action scope: {d.Action?.Scope ?? d.Empower?.Scope ?? Scope.Global}");
        lines.Add(active is null
            ? "not running"
            : $"running: started by {active.Trigger}, {Math.Max(0, (int)Math.Ceiling((active.Instance.EndsUtc - utcNow).TotalSeconds))}s left" +
              (d.Empower is null ? $", wave {active.WavesSpawned}/{d.Action?.Waves ?? 0}" : ""));
        if (d.Trigger.Type == TriggerType.Interval && d.Enabled && NextStartLine(active is not null, nextInterval, utcNow) is { } next) lines.Add(next);
        return lines;
    }

    /// <summary>One line per waveList wave (wave-sets D15): "wave &lt;n&gt;: &lt;u&gt; units (&lt;prefab&gt; x&lt;c&gt;[ L&lt;level&gt;|L±&lt;d&gt;][ hp
    /// x&lt;m&gt;][ pw x&lt;m&gt;][ mv x&lt;m&gt;][ as x&lt;m&gt;][ chance &lt;c&gt;], …), &lt;start rule&gt;". A wave whose line would pass
    /// Wire.MaxBytes gets "wave &lt;n&gt;: &lt;u&gt; units, &lt;start rule&gt;, units below" and its entries packed on lines of
    /// their own, as the units form does (event-spawns D32).</summary>
    public static IEnumerable<string> WaveLines(IReadOnlyList<WaveSpec> list)
    {
        for (var n = 1; n <= list.Count; n++)
        {
            var w = list[n - 1];
            var total = w.Units.Sum(u => u.Count);
            var rule = WaveSchedule.StartRule(w, n);
            var entries = w.Units.Select(EntryText).ToList();
            var one = $"wave {n}: {total} units ({string.Join(", ", entries)}), {rule}";
            if (Fits(one)) { yield return one; continue; }
            yield return $"wave {n}: {total} units, {rule}, units below";
            foreach (var line in PackList($"wave {n} units: ", entries)) yield return line;
        }
    }

    /// <summary>"&lt;prefab&gt; ×&lt;count&gt;" and the entry's level ("L30", "L+3"), multipliers ("hp×1.5") and chance
    /// (wave-sets D15).</summary>
    public static string EntryText(UnitEntry u)
    {
        var b = new StringBuilder($"{u.Prefab} ×{u.Count}");
        if (u.Modifiers is { } m)
        {
            if (m.Level is { } lv) b.Append(FormattableString.Invariant($" L{lv}"));
            if (m.LevelDelta is { } d) b.Append(FormattableString.Invariant($" L{(d >= 0 ? "+" : "")}{d}"));
            foreach (var (name, value) in new[] { ("hp", m.MaxHealth), ("pw", m.Power), ("mv", m.MoveSpeed), ("as", m.AttackSpeed) })
                if (value != 1.0) b.Append(FormattableString.Invariant($" {name}×{value:0.##}"));
        }
        if (u.Chance < 1.0) b.Append(FormattableString.Invariant($" chance {u.Chance:0.######}"));
        return b.ToString();
    }

    /// <summary>The Interval line of `event info` (automation D16): null while no next is drawn yet (the first poll after an
    /// enable or a reload draws it).</summary>
    public static string? NextStartLine(bool running, DateTime? next, DateTime utcNow) =>
        running ? "next start: after the running instance ends"
        : next is { } n ? $"next start in {Math.Max(0, (int)Math.Ceiling((n - utcNow).TotalMinutes))} min"
        : null;

    /// <summary><paramref name="prefix"/> and the items joined by ", ", on as few lines of at most Wire.MaxBytes UTF-8 bytes
    /// as fit, never splitting an item (each item is at most a 96-character prefab name and its count).</summary>
    static IEnumerable<string> PackList(string prefix, IReadOnlyList<string> items)
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

    /// <summary>"spawn: modifiers level 30, maxHealth x1.5; chance #2 0.5; loot on; hunt 40 m; claimed territory allowed" for the
    /// event-spawns keys a SpawnWaves action sets (event-spawns D18, UX 11.1); null when it sets none.</summary>
    public static string? SpawnKeys(SpawnWavesAction a)
    {
        var parts = new List<string>();
        if (a.Modifiers is { } m)
        {
            var mods = new List<string>();
            if (m.Level is { } lv) mods.Add(FormattableString.Invariant($"level {lv}"));
            if (m.LevelDelta is { } d) mods.Add(FormattableString.Invariant($"levelDelta {(d > 0 ? "+" : "")}{d}"));
            foreach (var (name, value) in new[] { ("maxHealth", m.MaxHealth), ("power", m.Power), ("moveSpeed", m.MoveSpeed), ("attackSpeed", m.AttackSpeed) })
                if (value != 1.0) mods.Add(FormattableString.Invariant($"{name} x{value:0.##}"));
            if (mods.Count > 0) parts.Add("modifiers " + string.Join(", ", mods));
        }
        // A waveList shows each entry's chance on its wave line (wave-sets D15); "#n" names action.units entries only.
        var chances = a.WaveList is not null ? [] : a.Units.Select((u, i) => (u.Chance, N: i + 1)).Where(u => u.Chance < 1.0)
            .Select(u => FormattableString.Invariant($"#{u.N} {u.Chance:0.######}")).ToList();   // the file may hold more than 2 decimals
        if (chances.Count > 0) parts.Add("chance " + string.Join(", ", chances));      // units by entry number, as `event set` names them
        if (a.Loot) parts.Add("loot on");
        if (a.Behaviour is { Type: BehaviourType.Hunt } hunt) parts.Add($"hunt {hunt.Range} m");
        if (a.AllowTerritory) parts.Add("claimed territory allowed");
        if (a.FanOut is { } fan) parts.Add($"fanOut {fan.MaxInstances} players {fan.MinSpacing} m apart");      // automation D16
        if (a.Scoreboard && a.WaveList is null) parts.Add("scoreboard on");      // a waveList shows its own line (wave-sets D15)
        return parts.Count == 0 ? null : "spawn: " + string.Join("; ", parts);
    }

    public static string Trigger(Trigger t) => t.Type switch
    {
        TriggerType.Schedule => $"schedule {string.Join(",", t.Days.Select(d => d.ToString()[..3]))} {string.Join(",", t.Times.Select(x => x.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)))}",
        TriggerType.GameTime => $"gametime {Lower(t.Phase)}",
        TriggerType.VBloodKilled => $"vbloodkilled {string.Join(",", t.Bosses)}",
        TriggerType.Interval => $"interval {t.MinMinutes}-{t.MaxMinutes} min",
        TriggerType.RegionEntered => $"regionentered cooldown {t.PlayerCooldownMinutes} min",
        TriggerType.FactionKills => $"factionkills {string.Join(",", (t.Factions ?? []).Select(FactionDenyList.ShortName))} " +
                                    $"{t.Kills} in {t.WindowSeconds}s{(t.Shared ? " shared" : "")}",
        _ => "manual",
    };

    /// <summary>A line built around <paramref name="trigger"/>: the whole trigger when the line fits one chat message
    /// (Wire.MaxBytes), else a vbloodkilled trigger as "vbloodkilled &lt;n&gt; bosses" (event-library A21) and a
    /// factionkills trigger as "factionkills &lt;n&gt; factions &lt;k&gt; in &lt;w&gt;s" (automation D27), since
    /// AdminLines.Pack would cut the line. <paramref name="shortened"/> says the names were left out.</summary>
    public static string Fit(Func<string, string> build, Trigger trigger, out bool shortened)
    {
        var full = build(Trigger(trigger));
        shortened = trigger.Type is TriggerType.VBloodKilled or TriggerType.FactionKills && !Fits(full);
        if (!shortened) return full;
        return build(trigger.Type == TriggerType.VBloodKilled
            ? $"vbloodkilled {trigger.Bosses.Count} bosses"
            : $"factionkills {(trigger.Factions ?? []).Count} factions {trigger.Kills} in {trigger.WindowSeconds}s{(trigger.Shared ? " shared" : "")}");
    }

    /// <summary>"bosses: a,b,…" lines, each within one chat message, together naming every boss once, in order.</summary>
    public static IReadOnlyList<string> BossLines(IReadOnlyList<string> bosses) => NameLines("bosses: ", bosses);

    /// <summary>"factions: a,b,…" lines of short faction names, as BossLines (automation D27).</summary>
    public static IReadOnlyList<string> FactionLines(IReadOnlyList<string> factions) =>
        NameLines("factions: ", factions.Select(FactionDenyList.ShortName).ToList());

    static IReadOnlyList<string> NameLines(string head, IReadOnlyList<string> names)
    {
        var lines = new List<string>();
        var current = head;
        foreach (var name in names)
        {
            var next = current.Length == head.Length ? current + name : current + "," + name;
            if (!Fits(next) && current.Length > head.Length) { lines.Add(current); next = head + name; }
            current = next;
        }
        if (current.Length > head.Length) lines.Add(current);
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

    public static string Of(EventDefinition d, ControlState controls) => Label(d, Precedence.StartBlocker(d, controls)?.Human);

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
