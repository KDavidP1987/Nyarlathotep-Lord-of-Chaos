#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace Nyarlathotep.Logic;

/// <summary>The outcome of parsing one command argument: a value, or the one reply line
/// "&lt;arg&gt; must be &lt;rule&gt;" (foundation D5, Design › UX › Commands).</summary>
public readonly record struct Arg<T>(bool Ok, T Value, string? Error)
{
    public static Arg<T> Of(T value) => new(true, value, null);
    public static Arg<T> Bad(string error) => new(false, default!, error);
}

/// <summary>A level argument: absolute 1–120, or an offset +n/−n (n 0–30) from the prefab's own level.</summary>
public readonly record struct LevelArg(bool IsOffset, int Value)
{
    public int Resolve(int prefabLevel) => IsOffset ? Math.Clamp(prefabLevel + Value, 1, 120) : Value;
}

public static class CommandArgs
{
    public static Arg<int> Arity(int given, int min, int max) =>
        given >= min && given <= max ? Arg<int>.Of(given)
        : Arg<int>.Bad(min == max ? $"arguments must be {min}" : $"arguments must be {min}-{max}");

    public static Arg<string> EventId(string? text) =>
        text is not null && text.Length is >= 1 and <= 32 && text.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            ? Arg<string>.Of(text)
            : Arg<string>.Bad("id must be 1-32 of a-z 0-9 -");

    /// <summary>`event list [page]`: default 1; must be within 1..pages (pages ≥ 1).</summary>
    public static Arg<int> Page(string? text, int pages)
    {
        pages = Math.Max(1, pages);
        if (string.IsNullOrEmpty(text)) return Arg<int>.Of(1);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var p) && p >= 1 && p <= pages
            ? Arg<int>.Of(p)
            : Arg<int>.Bad($"page must be 1-{pages}");
    }

    public static Arg<int> Count(string? text, int maxPerWave)
    {
        if (string.IsNullOrEmpty(text)) return Arg<int>.Of(1);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var c) && c >= 1 && c <= maxPerWave
            ? Arg<int>.Of(c)
            : Arg<int>.Bad($"count must be 1-{maxPerWave}");
    }

    /// <summary>null = the prefab's own level.</summary>
    public static Arg<LevelArg?> Level(string? text)
    {
        const string rule = "level must be 1-120 or +n/-n with n 0-30";
        if (string.IsNullOrEmpty(text)) return Arg<LevelArg?>.Of(null);
        if (text[0] is '+' or '-')
        {
            if (text.Length > 1 && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 30)
                return Arg<LevelArg?>.Of(new LevelArg(true, text[0] == '-' ? -n : n));
            return Arg<LevelArg?>.Bad(rule);
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var abs) && abs is >= 1 and <= 120
            ? Arg<LevelArg?>.Of(new LevelArg(false, abs))
            : Arg<LevelArg?>.Bad(rule);
    }

    /// <summary>hp and power multipliers: 0.1–10.0, default 1.0.</summary>
    public static Arg<float> Multiplier(string name, string? text)
    {
        if (string.IsNullOrEmpty(text)) return Arg<float>.Of(1f);
        return float.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m) && m >= 0.1f && m <= 10f
            ? Arg<float>.Of(m)
            : Arg<float>.Bad($"{name} must be 0.1-10.0");
    }

    /// <summary>`debug here [radius]`: 5–100 m, default 30.</summary>
    public static Arg<int> Radius(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Arg<int>.Of(30);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var r) && r is >= 5 and <= 100
            ? Arg<int>.Of(r)
            : Arg<int>.Bad("radius must be 5-100");
    }

    /// <summary>The settable stat fields of an Empower action (faction-empowerment D12).</summary>
    public static readonly IReadOnlyList<string> StatFields = EventValidator.StatKeys.Select(k => "action.stats." + k).ToList();

    /// <summary>The settable fields of a SpawnWaves action, refused on an Empower definition.</summary>
    public static readonly IReadOnlyList<string> WaveFields = ["action.waves", "action.intervalSeconds", "action.radius"];

    /// <summary>A stat multiplier: 1.0–3.0 with at most two decimals, '.' as the separator (invariant culture). The value
    /// is a decimal, so 1.3 is written to events.json as 1.3.</summary>
    public static Arg<object> Stat(string field, string? text)
    {
        var rule = $"{field} must be 1.0-3.0 with at most two decimals";
        if (string.IsNullOrEmpty(text) || text.Length > 6) return Arg<object>.Bad(rule);
        var dot = text.IndexOf('.');
        if (dot == 0 || (dot > 0 && (text.Length - dot - 1 is < 1 or > 2))) return Arg<object>.Bad(rule);
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m)) return Arg<object>.Bad(rule);
        return m is >= 1.0m and <= 3.0m ? Arg<object>.Of(m) : Arg<object>.Bad(rule);
    }

    /// <summary>Every field `.nyar event set` takes, with its family and who may set it (event-library D20): the code
    /// side of the design doc's field table (§6), compared both ways by ContractDocTests. <see cref="SettableValue"/>
    /// accepts exactly these names.</summary>
    public static IReadOnlyDictionary<string, (string Family, string Who)> SettableFields => _settable ??= BuildSettableFields();

    static IReadOnlyDictionary<string, (string Family, string Who)>? _settable;   // built on first use: it reads the lists below

    static IReadOnlyDictionary<string, (string Family, string Who)> BuildSettableFields()
    {
        var d = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var f in new[] { "name", "durationSeconds", "conditions.minPlayers", "conditions.cooldownMinutes", "conditions.chancePercent" })
            d[f] = ("definition", "admin");
        foreach (var f in TriggerFields) d[f] = ("trigger", "admin");
        foreach (var f in StatFields.Append("action.factions")) d[f] = ("empower action", "admin");
        foreach (var f in WaveFields.Append("action.units")) d[f] = ("spawn action", "admin");
        d["location"] = ("location", "admin");
        return d;
    }

    /// <summary>The trigger fields (event-library D9); each but trigger.type needs its trigger type.</summary>
    public static readonly IReadOnlyList<string> TriggerFields = ["trigger.type", "trigger.days", "trigger.times", "trigger.phase", "trigger.bosses"];

    /// <summary>The trigger type a trigger field belongs to; trigger.type belongs to every type.</summary>
    public static string? TriggerTypeOf(string field) => field switch
    {
        "trigger.days" or "trigger.times" => "Schedule",
        "trigger.phase" => "GameTime",
        "trigger.bosses" => "VBloodKilled",
        _ => null,
    };

    static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    public const int MaxNameLength = 96;

    /// <summary>The name rule of every unit, boss and faction value (event-library D10): its prefix (CHAR_ or
    /// Faction_), then only A-Za-z0-9_, at most 96 characters in all.</summary>
    public static bool IsNameValue(string name, string prefix)
    {
        if (name.Length <= prefix.Length || name.Length > MaxNameLength || !name.StartsWith(prefix, StringComparison.Ordinal)) return false;
        foreach (var ch in name)
            if (!(ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')) return false;
        return true;
    }

    public static string NameRule(string field) => $"{field} names must be CHAR_ or Faction_ then A-Za-z0-9_, at most {MaxNameLength} characters";

    /// <summary>"&lt;field&gt; lists &lt;value&gt; twice; nothing written" (event-library D9, D10).</summary>
    public static string Twice(string field, string value) => $"{field} lists {value} twice; nothing written";

    /// <summary>A comma list of 1..max non-empty entries, each of printable ASCII other than space, quotes, braces,
    /// brackets and angle brackets; null when the shape is wrong.</summary>
    static string[]? CommaList(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 4000) return null;
        foreach (var ch in value)
            if (ch <= ' ' || ch > '~' || ch is '"' or '\'' or '{' or '}' or '[' or ']' or '<' or '>' or '\\') return null;
        var parts = value.Split(',');
        if (parts.Length > max || parts.Any(p => p.Length == 0)) return null;
        return parts;
    }

    /// <summary>The first entry listed twice, or null.</summary>
    static string? Repeated(IEnumerable<string> keys)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in keys) if (!seen.Add(k)) return k;
        return null;
    }

    static Arg<object> TriggerValue(string field, string? value)
    {
        switch (field)
        {
            case "trigger.type":
                return value is "Manual" or "Schedule" or "GameTime" or "VBloodKilled"
                    ? Arg<object>.Of(value)
                    : Arg<object>.Bad("trigger.type must be Manual, Schedule, GameTime or VBloodKilled");
            case "trigger.phase":
                return value is "day" or "night" ? Arg<object>.Of(value) : Arg<object>.Bad("trigger.phase must be day or night");
            case "trigger.days":
            {
                const string rule = "trigger.days must be 1-7 of Sun Mon Tue Wed Thu Fri Sat, comma separated";
                var parts = CommaList(value, 7);
                if (parts is null || parts.Any(d => Array.IndexOf(DayNames, d) < 0)) return Arg<object>.Bad(rule);
                if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
                return Arg<object>.Of(parts);
            }
            case "trigger.times":
            {
                const string rule = "trigger.times must be 1-12 of HH:mm, comma separated";
                var parts = CommaList(value, 12);
                if (parts is null || parts.Any(t => !TimeFormat.TryParseHhMm(t, out _))) return Arg<object>.Bad(rule);
                if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
                return Arg<object>.Of(parts);
            }
            default:    // trigger.bosses
            {
                const string rule = "trigger.bosses must be any or 1-20 CHAR_ names, comma separated";
                if (value == "any") return Arg<object>.Of(new[] { "any" });
                var parts = CommaList(value, 20);
                if (parts is null) return Arg<object>.Bad(rule);
                if (parts.Any(b => !IsNameValue(b, "CHAR_"))) return Arg<object>.Bad(NameRule(field));
                if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
                return Arg<object>.Of(parts);
            }
        }
    }

    /// <summary>action.factions: 1-5 distinct Faction_ names (event-library D10).</summary>
    static Arg<object> Factions(string field, string? value)
    {
        var parts = CommaList(value, EventValidator.MaxFactions);
        if (parts is null) return Arg<object>.Bad($"{field} must be 1-{EventValidator.MaxFactions} Faction_ names, comma separated");
        if (parts.Any(f => !IsNameValue(f, "Faction_"))) return Arg<object>.Bad(NameRule(field));
        if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
        return Arg<object>.Of(parts);
    }

    /// <summary>action.units: 1-10 entries CHAR_&lt;name&gt;[:&lt;count&gt;], count 1-50 (default 1), each name once
    /// (event-library D10, S-8).</summary>
    static Arg<object> Units(string field, string? value)
    {
        const string rule = "action.units must be 1-10 entries CHAR_name or CHAR_name:count, count 1-50, comma separated";
        var parts = CommaList(value, 10);
        if (parts is null) return Arg<object>.Bad(rule);
        var entries = new List<UnitEntry>();
        foreach (var part in parts)
        {
            var colon = part.IndexOf(':');
            var name = colon < 0 ? part : part[..colon];
            var count = 1;
            if (colon >= 0 && !(int.TryParse(part.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) && count is >= 1 and <= 50))
                return Arg<object>.Bad(rule);
            if (!IsNameValue(name, "CHAR_")) return Arg<object>.Bad(NameRule(field));
            entries.Add(new UnitEntry(name, count));
        }
        if (Repeated(entries.Select(e => e.Prefab)) is { } twice) return Arg<object>.Bad(Twice(field, twice));
        return Arg<object>.Of(entries.ToArray());
    }

    /// <summary>`event set` whitelist (foundation S-10, event-library D9, D10, D11): field → validator of the new value.
    /// Only the character set and shape are checked here, before any write; name knowledge is the validator's, on
    /// reload. Whether the field fits the event's trigger or action type is checked on the file by EventsEditor.</summary>
    public static Arg<object> SettableValue(string field, string? value)
    {
        if (!SettableFields.ContainsKey(field)) return Arg<object>.Bad($"field {field} is not settable; edit events.json and reload");
        if (StatFields.Contains(field)) return Stat(field, value);
        if (TriggerFields.Contains(field)) return TriggerValue(field, value);
        if (field == "action.factions") return Factions(field, value);
        if (field == "action.units") return Units(field, value);
        if (field == "location") return value == "here" ? Arg<object>.Of(LocationHere.Instance) : Arg<object>.Bad("location takes here: .nyar event set id location here");
        static Arg<object> IntIn(string f, string? v, int min, int max) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max
                ? Arg<object>.Of(i)
                : Arg<object>.Bad($"{f} must be {min}-{max}");

        return field switch
        {
            "name" => value is not null && EventValidator.IsPlainText(value, 40)
                ? Arg<object>.Of(value)
                : Arg<object>.Bad("name must be 1-40 characters, no angle brackets or control characters"),
            "durationSeconds" => IntIn(field, value, 30, 7200),
            "conditions.minPlayers" => IntIn(field, value, 0, 100),
            "conditions.cooldownMinutes" => IntIn(field, value, 0, 10080),
            "conditions.chancePercent" => IntIn(field, value, 1, 100),
            "action.waves" => IntIn(field, value, 1, 10),
            "action.intervalSeconds" => IntIn(field, value, 10, 600),
            "action.radius" => IntIn(field, value, 2, 30),
            _ => Arg<object>.Bad($"field {field} is not settable; edit events.json and reload"),
        };
    }
}

/// <summary>The value of `.nyar event set &lt;id&gt; location here`: the command reads the admin's position and passes
/// it through <see cref="LocationArg.FromPosition"/> (event-library D11).</summary>
public sealed class LocationHere
{
    public static readonly LocationHere Instance = new();
    LocationHere() { }
}

/// <summary>A map point for action.location, x and z rounded to 0.1 (event-library D11).</summary>
public readonly record struct PointArg(decimal X, decimal Z)
{
    public override string ToString() => FormattableString.Invariant($"Point {X}, {Z}");
}

public static class LocationArg
{
    public const decimal Bound = 10000m;
    public const string OutOfBounds = "location here must be within -10000..10000";
    public const string NoCharacter = "location here needs your character in the world";

    /// <summary>The admin's position as a point, each axis rounded to 0.1; a position beyond ±10000 is refused.</summary>
    public static Arg<PointArg> FromPosition(float x, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z) || Math.Abs(x) > (float)Bound || Math.Abs(z) > (float)Bound)
            return Arg<PointArg>.Bad(OutOfBounds);
        // + 0.0m gives every value one decimal place, so 10000 reads "10000.0" like 800.0 (the scale of a sum is the larger).
        var px = Math.Round((decimal)x, 1, MidpointRounding.AwayFromZero) + 0.0m;
        var pz = Math.Round((decimal)z, 1, MidpointRounding.AwayFromZero) + 0.0m;
        if (Math.Abs(px) > Bound || Math.Abs(pz) > Bound) return Arg<PointArg>.Bad(OutOfBounds);
        return Arg<PointArg>.Of(new PointArg(px, pz));
    }

    /// <summary>`location here` from a context that may have no character or position (the server console, or a
    /// character not yet in the world): <paramref name="read"/> returns null, or throws, and the reply is
    /// <see cref="NoCharacter"/> (event-library D19).</summary>
    public static Arg<PointArg> FromContext(Func<(float X, float Z)?> read)
    {
        (float X, float Z)? at;
        try { at = read(); }
        catch (Exception) { at = null; }
        return at is { } p ? FromPosition(p.X, p.Z) : Arg<PointArg>.Bad(NoCharacter);
    }
}

/// <summary>One declared command form (event-library D20): its words, its §6 usage (with &lt;argument&gt; names), who may
/// run it, and how many arguments follow the words.</summary>
public sealed record CommandForm(string Words, string Usage, string Who, int MinArgs, int MaxArgs, string? Fixed = null)
{
    /// <summary>The usage with plain words for the arguments: chat eats angle brackets (profile note 11.2).</summary>
    public string ChatUsage => "usage: " + Usage.Replace("<", "").Replace(">", "");
}

/// <summary>The command forms this child declares (event-library D20, A7). VCF binds each `.nyar &lt;group&gt;` to one
/// method with optional trailing arguments, so a missing argument reaches the method; <see cref="Check"/> gives such a
/// form its usage reply before anything reaches the gateway.</summary>
public static class CommandForms
{
    public static readonly IReadOnlyList<CommandForm> Library =
    [
        new("template list", ".nyar template list [pillar] [page]", "admin", 0, 2),
        new("template info", ".nyar template info <template>", "admin", 1, 1),
        new("template use", ".nyar template use <template> [as <id>]", "admin", 1, 3, Fixed: "as"),
        new("event new", ".nyar event new <id> <pillar>", "admin", 2, 2),
        new("event copy", ".nyar event copy <id> <newId>", "admin", 2, 2),
        new("event delete", ".nyar event delete <id> [confirm]", "admin", 1, 2, Fixed: "confirm"),
        new("event set", ".nyar event set <id> <field> <value>", "admin", 3, 3),
        new("pillar list", ".nyar pillar list", "admin", 0, 0),
        new("pillar", ".nyar pillar <name> on|off", "admin", 2, 2),
    ];

    /// <summary>The form of <paramref name="group"/> <paramref name="words"/> (the verb and its arguments, VCF's empty
    /// defaults dropped from the end), or null with the usage reply in <paramref name="usage"/>. Null and no usage when
    /// the words are no form of this child (the caller's own verbs).</summary>
    public static CommandForm? Check(string group, IReadOnlyList<string> words, out string? usage)
    {
        usage = null;
        var w = words.ToList();
        while (w.Count > 0 && string.IsNullOrEmpty(w[^1])) w.RemoveAt(w.Count - 1);
        if (w.Count == 0) return null;
        var form = Library.FirstOrDefault(f => f.Words == $"{group} {w[0]}")
                   ?? (group == "pillar" && w[0] != "list" ? Library.First(f => f.Words == "pillar") : null);
        if (form is null) return null;
        var args = form.Words == "pillar" ? w : w.Skip(1).ToList();
        var ok = args.Count >= form.MinArgs && args.Count <= form.MaxArgs && args.All(a => a.Length > 0);
        if (ok && form.Fixed is { } word)
        {
            // template use t [as id]: 1 or 3 arguments, the second "as"; event delete id [confirm]: the second "confirm".
            ok = form.Words == "template use" ? args.Count == 1 || (args.Count == 3 && args[1] == word) : args.Count == 1 || args[1] == word;
        }
        if (ok) return form;
        usage = form.ChatUsage;
        return null;
    }
}
