#nullable enable
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

    /// <summary>`event set` whitelist (foundation S-10): field → validator of the new value. Whether the field fits the
    /// event's action type is checked on the file by EventsEditor.</summary>
    public static Arg<object> SettableValue(string field, string? value)
    {
        if (StatFields.Contains(field)) return Stat(field, value);
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
