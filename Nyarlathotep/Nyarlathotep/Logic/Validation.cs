#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nyarlathotep.Logic;

/// <summary>What the validator needs to know about unit prefabs. The service layer backs it with
/// PrefabCollectionSystem; tests back it with a fixed set.</summary>
public interface IUnitCatalog
{
    bool IsKnown(string prefabName);
    bool IsDenied(string prefabName);
}

/// <summary>GAME_ASSETS.md › Do-not-spawn list, the name-based part. Admins can extend it, never shrink it.
/// Component-based exclusions (DropInInventoryOnSpawn, BehaviourTreeInstance.Immobile) are checked by the
/// service-side catalog.</summary>
public static class UnitDenyList
{
    static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "CHAR_Mount_Horse_Vampire",
        "CHAR_Mount_Horse_Gloomrot",
    };

    public static bool IsDenied(string name) =>
        Exact.Contains(name)
        || name.Contains("CarriagePrisonerRelease", StringComparison.Ordinal)
        || name.Contains("MicroPOI", StringComparison.Ordinal);
}

public sealed class LoadResult
{
    /// <summary>Set when the whole file is rejected; <see cref="Set"/> is then empty.</summary>
    public string? FileError { get; init; }
    public int SchemaVersion { get; init; }
    public DefinitionSet Set { get; init; } = DefinitionSet.Empty;
    /// <summary>One line per disabled event: "event &lt;id&gt;: &lt;reason&gt;".</summary>
    public IReadOnlyList<string> Log { get; init; } = [];
}

/// <summary>events.json v1 validation (foundation D5, Design › Data). A file that does not parse, or breaks a
/// file-level bound, is rejected whole; in a file that parses, each invalid event is kept but disabled with one
/// reason naming its field.</summary>
public static class EventValidator
{
    public const int MaxFileBytes = 1_048_576;
    public const int MaxDefinitions = 200;
    public const int CurrentSchemaVersion = 1;

    public static readonly IReadOnlySet<string> AllowedPlaceholders =
        new HashSet<string>(StringComparer.Ordinal) { "faction", "minutes", "event", "zone", "wave", "waves" };

    static readonly Regex IdRx = new("^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant);
    static readonly Regex PlaceholderRx = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    static readonly HashSet<string> EventKeys = new(StringComparer.Ordinal)
    { "id", "name", "enabled", "pillar", "trigger", "conditions", "durationSeconds", "action", "announce" };

    public static LoadResult Parse(string text, IUnitCatalog units)
    {
        var bytes = System.Text.Encoding.UTF8.GetByteCount(text);
        if (bytes > MaxFileBytes)
            return new LoadResult { FileError = $"events.json is {bytes} bytes, over the {MaxFileBytes} byte limit" };

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException ex)
        {
            return new LoadResult { FileError = $"events.json rejected: line {(ex.LineNumber ?? 0) + 1} position {(ex.BytePositionInLine ?? 0) + 1}" };
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new LoadResult { FileError = "events.json rejected: the top level must be an object { \"SchemaVersion\": 1, \"events\": [ ... ] }" };
            foreach (var p in root.EnumerateObject())
            {
                if (p.Name is not ("SchemaVersion" or "events"))
                    return new LoadResult { FileError = $"events.json rejected: unknown top-level field {p.Name}" };
            }
            if (!root.TryGetProperty("SchemaVersion", out var sv) || sv.ValueKind != JsonValueKind.Number || !sv.TryGetInt32(out var schema) || schema < 1)
                return new LoadResult { FileError = "events.json rejected: SchemaVersion must be an integer 1 or higher" };
            if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
                return new LoadResult { FileError = "events.json rejected: events must be an array" };
            if (events.GetArrayLength() > MaxDefinitions)
                return new LoadResult { FileError = $"events.json rejected: {events.GetArrayLength()} events, over the limit of {MaxDefinitions}" };

            var defs = new List<EventDefinition>();
            var log = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var e in events.EnumerateArray())
            {
                index++;
                var def = ParseEvent(e, index, units);
                if (def.DisabledReason is null && !seen.Add(def.Id))
                    def = def with { DisabledReason = "duplicate id" };
                else if (def.DisabledReason is not null && IdRx.IsMatch(def.Id))
                    seen.Add(def.Id);
                if (def.DisabledReason is not null) log.Add($"event {def.Id}: {def.DisabledReason}");
                defs.Add(def);
            }
            return new LoadResult { SchemaVersion = schema, Set = new DefinitionSet(defs), Log = log };
        }
    }

    sealed class Fail(string reason) : Exception(reason);

    static EventDefinition ParseEvent(JsonElement e, int index, IUnitCatalog units)
    {
        var fallbackId = $"#{index}";
        if (e.ValueKind != JsonValueKind.Object)
            return Disabled(fallbackId, "an event must be an object");

        var id = e.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : null;
        var reportId = id is not null && IdRx.IsMatch(id) ? id : fallbackId;
        try
        {
            foreach (var p in e.EnumerateObject())
                if (!EventKeys.Contains(p.Name)) throw new Fail($"unknown field {p.Name}");

            if (id is null || !IdRx.IsMatch(id)) throw new Fail("id must be 1-32 of a-z 0-9 -");
            var name = Required(e, "name", "name must be 1-40 characters, no < > or control characters");
            if (name.ValueKind != JsonValueKind.String || !IsPlainText(name.GetString()!, 40))
                throw new Fail("name must be 1-40 characters, no < > or control characters");
            var enabledEl = Required(e, "enabled", "enabled must be true or false");
            if (enabledEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new Fail("enabled must be true or false");
            var pillar = ParsePillar(Required(e, "pillar", "pillar is required"));
            var trigger = ParseTrigger(Required(e, "trigger", "trigger is required"), units);
            var conditions = e.TryGetProperty("conditions", out var c) ? ParseConditions(c) : new Conditions();
            var duration = Int(Required(e, "durationSeconds", "durationSeconds must be 30-7200"), 30, 7200, "durationSeconds must be 30-7200");
            var action = ParseAction(Required(e, "action", "action is required"), trigger, units);
            var announce = e.TryGetProperty("announce", out var a) ? ParseAnnounce(a) : Announce.None;
            return new EventDefinition(id, name.GetString()!, enabledEl.GetBoolean(), pillar, trigger, conditions, duration, action, announce);
        }
        catch (Fail f)
        {
            return Disabled(reportId, f.Message);
        }
    }

    static EventDefinition Disabled(string id, string reason) =>
        new(id, id, false, Pillar.Spawns, Trigger.Manual(), new Conditions(), 30, null, Announce.None, reason);

    static JsonElement Required(JsonElement obj, string key, string rule) =>
        obj.TryGetProperty(key, out var v) ? v : throw new Fail(rule);

    static void OnlyKeys(JsonElement obj, string prefix, params string[] keys)
    {
        foreach (var p in obj.EnumerateObject())
            if (Array.IndexOf(keys, p.Name) < 0) throw new Fail($"unknown field {prefix}.{p.Name}");
    }

    static int Int(JsonElement v, int min, int max, string rule)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var i) || i < min || i > max) throw new Fail(rule);
        return i;
    }

    static string Str(JsonElement v, string rule) =>
        v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new Fail(rule);

    /// <summary>1..max characters, no '&lt;', '&gt;' or control characters.</summary>
    public static bool IsPlainText(string s, int max)
    {
        if (s.Length < 1 || s.Length > max) return false;
        foreach (var ch in s)
            if (ch is '<' or '>' || char.IsControl(ch)) return false;
        return true;
    }

    static Pillar ParsePillar(JsonElement v)
    {
        var s = v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        return s switch
        {
            "empowerment" => Pillar.Empowerment,
            "spawns" => Pillar.Spawns,
            "boss" => Pillar.Boss,
            "zones" => Pillar.Zones,
            "sieges" => Pillar.Sieges,
            _ => throw new Fail($"unknown pillar {s}"),
        };
    }

    static Trigger ParseTrigger(JsonElement t, IUnitCatalog units)
    {
        if (t.ValueKind != JsonValueKind.Object) throw new Fail("trigger must be an object with a type");
        var type = t.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        switch (type)
        {
            case "Manual":
                OnlyKeys(t, "trigger", "type");
                return Trigger.Manual();
            case "Schedule":
            {
                OnlyKeys(t, "trigger", "type", "days", "times");
                const string daysRule = "trigger.days must be 1-7 of Mon Tue Wed Thu Fri Sat Sun";
                const string timesRule = "trigger.times must be 1-12 of HH:mm";
                var days = new List<DayOfWeek>();
                var d = Required(t, "days", daysRule);
                if (d.ValueKind != JsonValueKind.Array || d.GetArrayLength() is < 1 or > 7) throw new Fail(daysRule);
                foreach (var x in d.EnumerateArray())
                {
                    var i = Array.IndexOf(DayNames, x.ValueKind == JsonValueKind.String ? x.GetString() : null);
                    if (i < 0 || days.Contains((DayOfWeek)i)) throw new Fail(daysRule);
                    days.Add((DayOfWeek)i);
                }
                var times = new List<TimeOnly>();
                var tm = Required(t, "times", timesRule);
                if (tm.ValueKind != JsonValueKind.Array || tm.GetArrayLength() is < 1 or > 12) throw new Fail(timesRule);
                foreach (var x in tm.EnumerateArray())
                {
                    if (!TimeFormat.TryParseHhMm(x.ValueKind == JsonValueKind.String ? x.GetString() : null, out var time) || times.Contains(time))
                        throw new Fail(timesRule);
                    times.Add(time);
                }
                return new Trigger(TriggerType.Schedule, days, times, DayPhase.Night, []);
            }
            case "GameTime":
            {
                OnlyKeys(t, "trigger", "type", "phase");
                var p = Str(Required(t, "phase", "trigger.phase must be day or night"), "trigger.phase must be day or night");
                var phase = p switch { "day" => DayPhase.Day, "night" => DayPhase.Night, _ => throw new Fail("trigger.phase must be day or night") };
                return new Trigger(TriggerType.GameTime, [], [], phase, []);
            }
            case "VBloodKilled":
            {
                OnlyKeys(t, "trigger", "type", "bosses");
                const string rule = "trigger.bosses must be [\"any\"] or 1-20 CHAR_ names";
                var b = Required(t, "bosses", rule);
                if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() is < 1 or > 20) throw new Fail(rule);
                var bosses = new List<string>();
                foreach (var x in b.EnumerateArray()) bosses.Add(Str(x, rule));
                if (bosses.Contains("any") && bosses.Count > 1) throw new Fail(rule);
                foreach (var boss in bosses)
                    if (boss != "any" && (!boss.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(boss))) throw new Fail($"unknown unit {boss}");
                return new Trigger(TriggerType.VBloodKilled, [], [], DayPhase.Night, bosses);
            }
            default:
                throw new Fail($"unknown trigger type {type}");
        }
    }

    static Conditions ParseConditions(JsonElement c)
    {
        if (c.ValueKind != JsonValueKind.Object) throw new Fail("conditions must be an object");
        OnlyKeys(c, "conditions", "minPlayers", "cooldownMinutes", "chancePercent", "window", "mode");
        var r = new Conditions();
        if (c.TryGetProperty("minPlayers", out var v)) r = r with { MinPlayers = Int(v, 0, 100, "conditions.minPlayers must be 0-100") };
        if (c.TryGetProperty("cooldownMinutes", out v)) r = r with { CooldownMinutes = Int(v, 0, 10080, "conditions.cooldownMinutes must be 0-10080") };
        if (c.TryGetProperty("chancePercent", out v)) r = r with { ChancePercent = Int(v, 1, 100, "conditions.chancePercent must be 1-100") };
        if (c.TryGetProperty("window", out v))
        {
            const string rule = "conditions.window must be { \"from\": \"HH:mm\", \"to\": \"HH:mm\" }";
            if (v.ValueKind != JsonValueKind.Object) throw new Fail(rule);
            OnlyKeys(v, "conditions.window", "from", "to");
            if (!TimeFormat.TryParseHhMm(Str(Required(v, "from", rule), rule), out var from)) throw new Fail(rule);
            if (!TimeFormat.TryParseHhMm(Str(Required(v, "to", rule), rule), out var to)) throw new Fail(rule);
            r = r with { Window = new TimeWindow(from, to) };
        }
        if (c.TryGetProperty("mode", out v))
        {
            r = r with
            {
                Mode = Str(v, "conditions.mode must be any, pve or pvp") switch
                {
                    "any" => GameMode.Any,
                    "pve" => GameMode.Pve,
                    "pvp" => GameMode.Pvp,
                    _ => throw new Fail("conditions.mode must be any, pve or pvp"),
                },
            };
        }
        return r;
    }

    static SpawnWavesAction ParseAction(JsonElement a, Trigger trigger, IUnitCatalog units)
    {
        if (a.ValueKind != JsonValueKind.Object) throw new Fail("action must be an object with a type");
        var type = a.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        if (type != "SpawnWaves") throw new Fail($"unknown action type {type}");
        OnlyKeys(a, "action", "type", "units", "waves", "intervalSeconds", "radius", "location", "unitLifetimeSeconds");

        const string unitsRule = "action.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }";
        var u = Required(a, "units", unitsRule);
        if (u.ValueKind != JsonValueKind.Array || u.GetArrayLength() is < 1 or > 10) throw new Fail(unitsRule);
        var list = new List<UnitEntry>();
        foreach (var x in u.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.Object) throw new Fail(unitsRule);
            OnlyKeys(x, "action.units", "prefab", "count");
            var prefab = Str(Required(x, "prefab", unitsRule), unitsRule);
            if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail($"unit {prefab} is deny-listed");
            if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit {prefab}");
            list.Add(new UnitEntry(prefab, Int(Required(x, "count", unitsRule), 1, 50, "action.units.count must be 1-50")));
        }
        var waves = Int(Required(a, "waves", "action.waves must be 1-10"), 1, 10, "action.waves must be 1-10");
        var interval = Int(Required(a, "intervalSeconds", "action.intervalSeconds must be 10-600"), 10, 600, "action.intervalSeconds must be 10-600");
        var radius = Int(Required(a, "radius", "action.radius must be 2-30"), 2, 30, "action.radius must be 2-30");
        var location = ParseLocation(Required(a, "location", "action.location is required"), trigger);
        int? lifetime = a.TryGetProperty("unitLifetimeSeconds", out var lt) ? Int(lt, 30, 7200, "action.unitLifetimeSeconds must be 30-7200") : null;
        return new SpawnWavesAction(list, waves, interval, radius, location, lifetime);
    }

    static Location ParseLocation(JsonElement l, Trigger trigger)
    {
        const string rule = "action.location must be { \"type\": \"Point\", \"x\": number, \"z\": number } or { \"type\": \"Admin\" }";
        if (l.ValueKind != JsonValueKind.Object) throw new Fail(rule);
        var type = l.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString() : null;
        switch (type)
        {
            case "Point":
                OnlyKeys(l, "action.location", "type", "x", "z");
                return new Location(LocationType.Point, Coord(Required(l, "x", rule), "x"), Coord(Required(l, "z", rule), "z"));
            case "Admin":
                OnlyKeys(l, "action.location", "type");
                if (trigger.Type != TriggerType.Manual) throw new Fail("action.location Admin needs a Manual trigger");
                return new Location(LocationType.Admin, 0, 0);
            default:
                throw new Fail(rule);
        }
    }

    static float Coord(JsonElement v, string axis)
    {
        var rule = $"action.location.{axis} must be a number within -10000..10000";
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d) || double.IsNaN(d) || d < -10000 || d > 10000) throw new Fail(rule);
        return (float)d;
    }

    static Announce ParseAnnounce(JsonElement a)
    {
        if (a.ValueKind != JsonValueKind.Object) throw new Fail("announce must be an object");
        OnlyKeys(a, "announce", "start", "end", "warnings");
        var start = a.TryGetProperty("start", out var s) ? Lines(s, "announce.start") : [];
        var end = a.TryGetProperty("end", out var e) ? Lines(e, "announce.end") : [];
        var warnings = false;
        if (a.TryGetProperty("warnings", out var w))
        {
            if (w.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new Fail("announce.warnings must be true or false");
            warnings = w.GetBoolean();
        }
        return new Announce(start, end, warnings);
    }

    static IReadOnlyList<string> Lines(JsonElement v, string field)
    {
        var rule = $"{field} must be 0-5 lines of 1-200 characters, no < > or control characters";
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() > 5) throw new Fail(rule);
        var list = new List<string>();
        foreach (var x in v.EnumerateArray())
        {
            var line = Str(x, rule);
            if (!IsPlainText(line, 200)) throw new Fail(rule);
            var bad = UnknownPlaceholder(line);
            if (bad is not null) throw new Fail($"unknown placeholder {{{bad}}}");
            list.Add(line);
        }
        return list;
    }

    /// <summary>The first placeholder in <paramref name="template"/> outside Security › Injection, or null.</summary>
    public static string? UnknownPlaceholder(string template)
    {
        foreach (Match m in PlaceholderRx.Matches(template))
            if (!AllowedPlaceholders.Contains(m.Groups[1].Value)) return m.Groups[1].Value;
        return null;
    }
}
