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

    /// <summary>`debug walk [radius]` (walkable-spawns D7): absent 0.5 m, else 0.1–5 with at most two decimals, '.' as
    /// the separator (invariant culture); a second argument is refused like a bad radius.</summary>
    public static Arg<float> WalkRadius(string? text, string? extra = null)
    {
        const string bad = "radius must be 0.1-5";
        if (!string.IsNullOrEmpty(extra)) return Arg<float>.Bad(bad);
        if (string.IsNullOrEmpty(text)) return Arg<float>.Of(0.5f);
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var r)) return Arg<float>.Bad(bad);
        if (r < 0.1m || r > 5m || decimal.Round(r, 2) != r) return Arg<float>.Bad(bad);
        return Arg<float>.Of((float)r);
    }

    /// <summary>The settable stat fields of an Empower action (faction-empowerment D12).</summary>
    public static readonly IReadOnlyList<string> StatFields = EventValidator.StatKeys.Select(k => "action.stats." + k).ToList();

    /// <summary>The settable fields of a SpawnWaves action, refused on an Empower definition.</summary>
    public static readonly IReadOnlyList<string> WaveFields = ["action.waves", "action.intervalSeconds", "action.radius"];

    /// <summary>The settable modifier fields of a SpawnWaves action (event-spawns D18): the two level forms, then the
    /// multipliers of EventValidator.ModifierKeys.</summary>
    public static readonly IReadOnlyList<string> ModifierFields =
        new[] { "level", "levelDelta" }.Concat(EventValidator.ModifierKeys).Select(k => "action.modifiers." + k).ToList();

    public const string UnitChanceField = "action.units.N.chance";

    /// <summary>Every event-spawns field (D18), all of a SpawnWaves action; `action.units.N.chance` stands for
    /// one field per unit entry, n 1-10.</summary>
    public static readonly IReadOnlyList<string> SpawnKeyFields =
        ModifierFields.Concat(["action.loot", "action.allowTerritory", "action.behaviour", UnitChanceField]).ToList();

    /// <summary>The entry number n (1-10) of an `action.units.N.chance` field, or null for any other name.</summary>
    public static int? UnitChanceIndex(string field)
    {
        const string head = "action.units.", tail = ".chance";
        if (field.Length <= head.Length + tail.Length || !field.StartsWith(head, StringComparison.Ordinal) || !field.EndsWith(tail, StringComparison.Ordinal))
            return null;
        var n = field.AsSpan(head.Length, field.Length - head.Length - tail.Length);
        return n.Length is 1 or 2 && n[0] != '0' && int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i is >= 1 and <= 10
            ? i : null;
    }

    /// <summary>The table name of <paramref name="field"/>: `action.units.N.chance` for a unit's chance, the
    /// `action.waveList.N…` name of a wave-list field (wave-sets D14), else the field itself.</summary>
    public static string TableName(string field) =>
        UnitChanceIndex(field) is not null ? UnitChanceField
        : WaveListPath(field) is { } w ? w.Table
        : PoolOf(field) is not null ? PoolsField
        : field;

    /// <summary>`action.pursuit` (wave-variety D10): 15-600 (afterSeconds) or none.</summary>
    public const string PursuitField = "action.pursuit";

    /// <summary>`action.pools.&lt;name&gt;` (wave-variety D10): a list of prefabs, +prefab, -prefab or none; NAME stands for
    /// one field per pool name.</summary>
    public const string PoolsField = "action.pools.NAME";

    /// <summary>The pool name of an `action.pools.&lt;name&gt;` field (any text without a dot, checked by the value parse),
    /// or null for any other name.</summary>
    public static string? PoolOf(string field)
    {
        const string head = "action.pools.";
        return field.Length > head.Length && field.StartsWith(head, StringComparison.Ordinal) && field.IndexOf('.', head.Length) < 0
            ? field[head.Length..] : null;
    }

    /// <summary>`action.scoreboard` (wave-sets D2, D14): true or false.</summary>
    public const string ScoreboardField = "action.scoreboard";

    /// <summary>The wave-list fields (wave-sets D14) by table name: a wave's removal, its units, its two start keys and
    /// one modifier per unit entry, N and M 1-10.</summary>
    public static readonly IReadOnlyList<string> WaveListFields =
        new[] { "action.waveList.N", "action.waveList.N.units", "action.waveList.N.afterSeconds", "action.waveList.N.whenCleared", "action.waveList.N.bosses" }
            .Concat(new[] { "level", "levelDelta" }.Concat(EventValidator.ModifierKeys).Select(k => "action.waveList.N.units.M." + k))
            .ToList();

    static readonly string[] WaveModifierKeys = ["level", "levelDelta", "maxHealth", "power", "moveSpeed", "attackSpeed"];

    /// <summary>A wave-list field split into its wave <c>N</c>, its key (null: the wave itself), its unit entry <c>M</c>
    /// and modifier, with its table name; null for any other name. N and M are 1-10 without a leading zero.</summary>
    public static WaveListField? WaveListPath(string field)
    {
        const string head = "action.waveList.";
        if (!field.StartsWith(head, StringComparison.Ordinal)) return null;
        var parts = field[head.Length..].Split('.');
        if (Index(parts[0]) is not { } n) return null;
        if (parts.Length == 1) return new WaveListField(n, null, null, null, "action.waveList.N");
        if (parts.Length == 2 && parts[1] is "units" or "afterSeconds" or "whenCleared" or "bosses")
            return new WaveListField(n, parts[1], null, null, $"action.waveList.N.{parts[1]}");
        if (parts.Length == 4 && parts[1] == "units" && Index(parts[2]) is { } m && Array.IndexOf(WaveModifierKeys, parts[3]) >= 0)
            return new WaveListField(n, "units", m, parts[3], $"action.waveList.N.units.M.{parts[3]}");
        return null;

        static int? Index(string t) =>
            t.Length is 1 or 2 && t[0] != '0' && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i is >= 1 and <= 10 ? i : null;
    }

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
        foreach (var f in WaveFields.Append("action.units").Concat(SpawnKeyFields)) d[f] = ("spawn action", "admin");
        d["location"] = ("location", "admin");
        d["trigger.scope"] = ("trigger", "admin");                  // regions D9, A28: every trigger type
        d["action.scope"] = ("action", "admin");                    // either action type
        d[FanOutField] = ("spawn action", "admin");                 // automation D16
        d[ScoreboardField] = ("spawn action", "admin");             // wave-sets D14
        foreach (var f in WaveListFields) d[f] = ("spawn action", "admin");
        foreach (var f in SwarmFields) d[f] = ("spawn action", "admin");      // swarm D9
        d[RegionLockField] = ("spawn action", "admin");             // hunting-grounds D14
        d[BossesField] = ("spawn action", "admin");                 // revenge D31; an Apocalypse's too (apocalypse D16)
        d[PursuitField] = ("spawn action", "admin");                // wave-variety D10
        d[PoolsField] = ("spawn action", "admin");
        foreach (var f in ApocalypseFields) d[f] = ("apocalypse action", "admin");     // apocalypse D16
        foreach (var f in DestinationFields) d[f] = ("spawn action", "admin");         // undead-hordes D5
        foreach (var f in HordeFields) d[f] = ("apocalypse action", "admin");          // undead-hordes D20
        return d;
    }

    /// <summary>`action.start` (undead-hordes D20): here only, the admin's x, y and z as a Point start;
    /// `action.start.minDistance` makes it a Hotspot start that far from the destination.</summary>
    public const string StartField = "action.start";
    public const string StartDistanceField = "action.start.minDistance";
    public const string StartHereRule = "action.start takes here (your position)";

    static IEnumerable<string> SpreadFields(string at) => new[] { "perKillMin", "perKillMax", "max", "perMinute", "pool" }.Select(k => $"{at}.{k}");

    /// <summary>The Infestation and HordeMarch fields (undead-hordes D20): an infestation's origins, hunters, driftRadius and
    /// spread block; a horde march's start, growth block and infest block. The destination fields and `action.marchSpeed`
    /// fit a HordeMarch too, and `action.bosses` (rolls allowed).</summary>
    public static readonly IReadOnlyList<string> HordeFields =
    [
        StartField, StartDistanceField,
        "action.origins.count", "action.origins.minNatives", "action.origins.minSpacing", "action.origins.perOrigin",
        "action.hunters", "action.driftRadius", .. SpreadFields("action.spread"),
        .. SpreadFields("action.growth"),
        "action.infest.hunters", "action.infest.driftRadius", .. SpreadFields("action.infest.spread"),
    ];

    /// <summary>A HordeFields value, each in its Limits range and refused with the validator's own reason; a spread block's
    /// `pool` takes a pool name or none. Whether the pool exists, and perKillMax against perKillMin, are the validator's.</summary>
    static Arg<object> HordeValue(string field, string? value)
    {
        static Arg<object> IntIn(string v, int min, int max, string rule) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max ? Arg<object>.Of(i) : Arg<object>.Bad(rule);
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        var dot = field.LastIndexOf('.');
        var (at, key) = (field[..dot], field[(dot + 1)..]);
        return (at, key) switch
        {
            ("action", "start") => value == "here" ? Arg<object>.Of(LocationHere.Instance) : Arg<object>.Bad(StartHereRule),
            ("action.start", _) => IntIn(value, HordeLimits.MinStartDistance, HordeLimits.MaxStartDistance, EventValidator.StartDistanceRule),
            ("action.origins", "count") => IntIn(value, HordeLimits.MinOrigins, HordeLimits.MaxOrigins, EventValidator.OriginsCountRule),
            ("action.origins", "minNatives") => IntIn(value, HordeLimits.MinOriginNatives, HordeLimits.MaxOriginNatives, EventValidator.OriginNativesRule),
            ("action.origins", "minSpacing") => IntIn(value, HordeLimits.MinOriginSpacing, HordeLimits.MaxOriginSpacing, EventValidator.OriginSpacingRule),
            ("action.origins", _) => IntIn(value, HordeLimits.MinPerOrigin, HordeLimits.MaxPerOrigin, EventValidator.PerOriginRule),
            (_, "hunters") => IntIn(value, HordeLimits.MinHunters, HordeLimits.MaxHunters, EventValidator.HuntersRule(at)),
            (_, "driftRadius") => IntIn(value, HordeLimits.MinDrift, HordeLimits.MaxDrift, EventValidator.DriftRule(at)),
            (_, "perKillMin") => IntIn(value, ApocalypseLimits.MinKillDraw, ApocalypseLimits.MaxKillDraw, EventValidator.SpreadKillMinRule(at)),
            (_, "perKillMax") => IntIn(value, ApocalypseLimits.MinKillDraw, ApocalypseLimits.MaxKillDraw, EventValidator.SpreadKillMaxRule(at)),
            (_, "max") => IntIn(value, HordeLimits.MinSpreadMax, HordeLimits.MaxSpreadMax, EventValidator.SpreadMaxRule(at)),
            (_, "perMinute") => IntIn(value, ApocalypseLimits.MinPerMinute, ApocalypseLimits.MaxPerMinute, EventValidator.SpreadPerMinuteRule(at)),
            _ => value == "none" ? Arg<object>.Of(FieldRemoval.Instance)
                : EventValidator.IsPoolName(value) ? Arg<object>.Of(value) : Arg<object>.Bad(EventValidator.NoPool(at, value)),
        };
    }

    /// <summary>`action.destination` (undead-hordes D5): here only, the admin's x, y and z as a Point destination.</summary>
    public const string DestinationField = "action.destination";
    public const string DestinationHereRule = "action.destination takes here (your position) or none";
    public const string MarchSpeedField = "action.marchSpeed";

    /// <summary>A SpawnWaves action's destination fields (undead-hordes D5); `action.marchSpeed` is the apocalypse's field and
    /// fits a SpawnWaves action too.</summary>
    public static readonly IReadOnlyList<string> DestinationFields = [DestinationField, "action.destination.radius", "action.destination.name"];

    static Arg<object> DestinationValue(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        return field switch
        {
            DestinationField => value switch
            {
                "here" => Arg<object>.Of(LocationHere.Instance),
                "none" => Arg<object>.Of(FieldRemoval.Instance),
                _ => Arg<object>.Bad(DestinationHereRule),
            },
            "action.destination.radius" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var r)
                                           && r >= DestinationLimits.MinRadius && r <= DestinationLimits.MaxRadius
                ? Arg<object>.Of(r) : Arg<object>.Bad(EventValidator.DestinationRadiusRule),
            _ => EventValidator.IsPlainText(value, DestinationLimits.MaxName) ? Arg<object>.Of(value) : Arg<object>.Bad(EventValidator.DestinationNameRule),
        };
    }

    /// <summary>`action.goal` (apocalypse D16): here only, the admin's x, y and z as a Point goal.</summary>
    public const string GoalField = "action.goal";
    public const string GoalHereRule = "action.goal takes here (your position)";

    /// <summary>The eight Apocalypse fields (apocalypse D16); `action.bosses` and `durationSeconds` are shared with other
    /// kinds. Every other Apocalypse key is edited in events.json and loaded with `.nyar event reload`.</summary>
    public static readonly IReadOnlyList<string> ApocalypseFields =
    [
        GoalField, "action.goal.radius", "action.goal.name", "action.hordes.count", "action.hordes.everyMinutes", "action.hordes.size",
        "action.roamers.max", MarchSpeedField,
    ];

    /// <summary>`durationSeconds` takes 30-604800 here; EventsEditor holds it to the event's kind on the file (apocalypse D1:
    /// 600-604800 for an Apocalypse, 30-7200 for every other kind).</summary>
    public const string DurationRule = "durationSeconds must be 30-7200 (600-604800 for an Apocalypse)";

    /// <summary>An Apocalypse field's value (apocalypse D16), each in D1's range and refused with the validator's own reason.</summary>
    static Arg<object> ApocalypseValue(string field, string? value)
    {
        static Arg<object> IntIn(string? v, int min, int max, string rule) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max ? Arg<object>.Of(i) : Arg<object>.Bad(rule);
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        return field switch
        {
            GoalField => value == "here" ? Arg<object>.Of(LocationHere.Instance) : Arg<object>.Bad(GoalHereRule),
            "action.goal.radius" => IntIn(value, ApocalypseLimits.MinGoalRadius, ApocalypseLimits.MaxGoalRadius, EventValidator.GoalRadiusRule),
            "action.goal.name" => EventValidator.IsPlainText(value, ApocalypseLimits.MaxGoalName) ? Arg<object>.Of(value) : Arg<object>.Bad(EventValidator.GoalNameRule),
            "action.hordes.count" => IntIn(value, ApocalypseLimits.MinHordes, ApocalypseLimits.MaxHordes, EventValidator.HordesCountRule),
            "action.hordes.everyMinutes" => IntIn(value, ApocalypseLimits.MinEvery, ApocalypseLimits.MaxEvery, EventValidator.EveryMinutesRule),
            "action.hordes.size" => IntIn(value, ApocalypseLimits.MinSize, ApocalypseLimits.MaxSize, EventValidator.HordeSizeRule),
            "action.roamers.max" => IntIn(value, ApocalypseLimits.MinRoamers, ApocalypseLimits.MaxRoamers, EventValidator.RoamersMaxRule),
            _ => Decimal2(value, (decimal)ApocalypseLimits.MinMarchSpeed, (decimal)ApocalypseLimits.MaxMarchSpeed) is { } m
                ? Arg<object>.Of(m) : Arg<object>.Bad(EventValidator.MarchSpeedRule),
        };
    }

    /// <summary>`action.bosses` (revenge D31): CHAR_name or CHAR_name:count entries, or none.</summary>
    public const string BossesField = "action.bosses";

    /// <summary>`action.bosses`'s value: 1-3 entries, each count 1-3 (default 1), refused with the validator's reasons
    /// (revenge D28); none removes the key. A list of fixed bosses only is revenge D31's UnitEntry[]; one holding a roll
    /// (wave-variety D10) is BossRoll[].</summary>
    static Arg<object> BossesValue(string? value) => BossList(BossesField, value, waveLevel: false);

    /// <summary>A bosses list at <paramref name="field"/> (wave-variety D3, D10): 1-3 entries `CHAR_name[:count]` (count 1-3)
    /// or `pool:&lt;name&gt;:&lt;min&gt;-&lt;max&gt;` (1 ≤ min ≤ max ≤ 5), at most 5 bosses in all for a wave's own list or a list
    /// holding a roll, refused with the validator's reasons; none removes the key. Whether the pool exists and is large
    /// enough is the validator's, on reload.</summary>
    static Arg<object> BossList(string field, string? value, bool waveLevel)
    {
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        if (value == "none") return Arg<object>.Of(FieldRemoval.Instance);
        var parts = CommaList(value, 10);
        if (parts is null || parts.Length > EventValidator.MaxBosses) return Arg<object>.Bad(EventValidator.BossesRuleAt(field));
        var entries = new List<BossRoll>();
        for (var k = 1; k <= parts.Length; k++)
        {
            var part = parts[k - 1];
            var at = $"{field}.{k}";
            if (part.StartsWith(PoolToken, StringComparison.Ordinal))
            {
                var bits = part.Split(':');
                var range = bits.Length == 3 ? bits[2].Split('-') : [];
                if (range.Length != 2 || !int.TryParse(range[0], NumberStyles.None, CultureInfo.InvariantCulture, out var min)
                    || !int.TryParse(range[1], NumberStyles.None, CultureInfo.InvariantCulture, out var max))
                    return Arg<object>.Bad(BossEntryRule(field));                // the token's shape, before its parts
                if (!EventValidator.IsPoolName(bits[1])) return Arg<object>.Bad(EventValidator.NoPool(at, bits[1]));
                if (min is < 1 or > EventValidator.MaxRollBosses) return Arg<object>.Bad(EventValidator.RollMinRule(at));
                if (max < min || max > EventValidator.MaxRollBosses) return Arg<object>.Bad(EventValidator.RollMaxRule(at));
                entries.Add(BossRoll.Roll(bits[1], min, max));
                continue;
            }
            var colon = part.IndexOf(':');
            var name = colon < 0 ? part : part[..colon];
            var count = 1;
            if (colon >= 0 && !(int.TryParse(part.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count)
                    && count is >= 1 and <= EventValidator.MaxBossCount))
                return Arg<object>.Bad(EventValidator.BossCountRuleAt(field));
            if (!IsNameValue(name, "CHAR_")) return Arg<object>.Bad(NameRule(field));
            entries.Add(BossRoll.Fixed(name, count));
        }
        var rolls = entries.Any(e => e.IsRoll);
        if ((waveLevel || rolls) && entries.Sum(e => e.MostBosses) > EventValidator.MaxWaveBosses) return Arg<object>.Bad(EventValidator.BossesMost(field));
        return rolls || waveLevel ? Arg<object>.Of(entries.ToArray()) : Arg<object>.Of(entries.Select(e => new UnitEntry(e.Prefab, e.Count)).ToArray());
    }

    /// <summary>A bosses list entry of the wrong shape (wave-variety D10): a `pool:` token without exactly a name and a
    /// min-max of two whole numbers.</summary>
    public static string BossEntryRule(string field) => $"{field} entries must be <prefab>:<count> or pool:<name>:<min>-<max>";

    /// <summary>The token of a pool in a units list or a boss roll: `pool:&lt;name&gt;:…` (wave-variety D10).</summary>
    public const string PoolToken = "pool:";

    /// <summary>A `pool:&lt;name&gt;:&lt;count&gt;` token of a units list (wave-variety D10): count 1 to MaxUnitsPerWave's ceiling; null when
    /// <paramref name="part"/> is no pool token, else the entry or the refusal.</summary>
    static Arg<object>? PoolEntry(string field, string part, string rule)
    {
        if (!part.StartsWith(PoolToken, StringComparison.Ordinal)) return null;
        var bits = part.Split(':');
        if (bits.Length != 3) return Arg<object>.Bad(rule);
        if (!EventValidator.IsPoolName(bits[1])) return Arg<object>.Bad(EventValidator.NoPool(field, bits[1]));
        return int.TryParse(bits[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 1 && count <= Limits.MaxUnitsPerWave.Max
            ? Arg<object>.Of(UnitEntry.FromPool(bits[1], count)) : Arg<object>.Bad(rule);
    }

    /// <summary>`action.pursuit`'s value (wave-variety D10): afterSeconds 15-600, or none.</summary>
    static Arg<object> PursuitValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Arg<object>.Bad(ValueRequired)
        : value == "none" ? Arg<object>.Of(FieldRemoval.Instance)
        : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s >= EventValidator.MinPursuitSeconds && s <= EventValidator.MaxPursuitSeconds
            ? Arg<object>.Of(new PursuitArg(s)) : Arg<object>.Bad(EventValidator.PursuitAfterRule);

    /// <summary>`action.pools.&lt;name&gt;`'s value (wave-variety D10): 1-30 distinct CHAR_ names (a list longer than one chat
    /// line is built by appends), `+CHAR_name` or `-CHAR_name` for one member, or none; refused with the validator's
    /// reasons. Whether the file's pool takes the append, or may be removed, is EventsEditor's, on the file.</summary>
    static Arg<object> PoolValue(string field, string pool, string? value)
    {
        if (!EventValidator.IsPoolName(pool)) return Arg<object>.Bad(EventValidator.PoolNameRule(pool));
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        if (value == "none") return Arg<object>.Of(FieldRemoval.Instance);
        if (value[0] is '+' or '-')
            return IsNameValue(value[1..], "CHAR_") ? Arg<object>.Of(new PoolEdit(value[0] == '+', value[1..])) : Arg<object>.Bad(NameRule(field));
        var parts = CommaList(value, EventValidator.MaxPoolMembers);
        if (parts is null || Repeated(parts) is not null) return Arg<object>.Bad(EventValidator.PoolMembersRule(pool));
        if (parts.Any(p => !IsNameValue(p, "CHAR_"))) return Arg<object>.Bad(NameRule(field));
        return Arg<object>.Of(parts);
    }

    /// <summary>`action.regionLock` (hunting-grounds D14): true, false or none (absent and false are the same).</summary>
    public const string RegionLockField = "action.regionLock";

    /// <summary>`action.regionLock`'s value, refused with the validator's reason (hunting-grounds D2).</summary>
    static Arg<object> RegionLockValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Arg<object>.Bad(ValueRequired)
        : value switch
        {
            "true" => Arg<object>.Of(true),
            "false" => Arg<object>.Of(false),
            "none" => Arg<object>.Of(FieldRemoval.Instance),
            _ => Arg<object>.Bad(EventValidator.RegionLockRule),
        };

    /// <summary>`action.arena` (swarm D9): 15-60 or none.</summary>
    public const string ArenaField = "action.arena";
    /// <summary>`action.winner` (swarm D9): killdeath (any case, written KillDeath) or none.</summary>
    public const string WinnerField = "action.winner";
    /// <summary>`action.growth` (swarm D9): none only, which removes the growth and leaves the units form as it was.</summary>
    public const string GrowthField = "action.growth";
    public const string GrowthNoneRule = "action.growth takes none (it removes the growth)";

    /// <summary>The growth keys (swarm D9): a field on an event without growth creates the object.</summary>
    public static readonly IReadOnlyList<string> GrowthFields = ["action.growth.addUnits", "action.growth.addLevels", "action.growth.whenCleared"];

    /// <summary>The six swarm fields (swarm D9).</summary>
    public static readonly IReadOnlyList<string> SwarmFields = [ArenaField, WinnerField, GrowthField, .. GrowthFields];

    /// <summary>A swarm field's value (swarm D9), each refused with the validator's own reason (D1, D2, D24).</summary>
    static Arg<object> SwarmValue(string field, string value)
    {
        static Arg<object> IntIn(string v, int min, int max, string rule) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max ? Arg<object>.Of(i) : Arg<object>.Bad(rule);
        if (value == "none" && field is ArenaField or WinnerField or GrowthField) return Arg<object>.Of(FieldRemoval.Instance);
        return field switch
        {
            ArenaField => IntIn(value, Arena.MinRadius, Arena.MaxRadius, EventValidator.ArenaRadiusRule),
            WinnerField => string.Equals(value, nameof(WinnerRule.KillDeath), StringComparison.OrdinalIgnoreCase)
                ? Arg<object>.Of(nameof(WinnerRule.KillDeath)) : Arg<object>.Bad(EventValidator.WinnerValue),
            GrowthField => Arg<object>.Bad(GrowthNoneRule),
            "action.growth.addUnits" => IntIn(value, 0, EventValidator.MaxGrowthUnits, EventValidator.GrowthAddUnitsRule),
            "action.growth.addLevels" => IntIn(value, 0, EventValidator.MaxGrowthLevels, EventValidator.GrowthAddLevelsRule),
            _ => value switch
            {
                "true" => Arg<object>.Of(true),
                "false" => Arg<object>.Of(false),
                _ => Arg<object>.Bad(EventValidator.GrowthWhenClearedRule),
            },
        };
    }

    /// <summary>The trigger fields (event-library D9); each but trigger.type needs its trigger type.</summary>
    public static readonly IReadOnlyList<string> TriggerFields =
    [
        "trigger.type", "trigger.days", "trigger.times", "trigger.phase", "trigger.bosses",
        "trigger.minMinutes", "trigger.maxMinutes", "trigger.playerCooldownMinutes",                     // automation D16
        "trigger.factions", "trigger.kills", "trigger.windowSeconds", "trigger.shared",
        "trigger.heat", "trigger.warnAt", "trigger.vbloodHeat",                                           // revenge D11
    ];

    /// <summary>action.fanOut (automation D16): none, or MAX SPACING.</summary>
    public const string FanOutField = "action.fanOut";
    public const string FanOutRule = "action.fanOut takes none or MAX SPACING";

    /// <summary>The trigger type a trigger field belongs to; trigger.type belongs to every type.</summary>
    public static string? TriggerTypeOf(string field) => field switch
    {
        "trigger.days" or "trigger.times" => "Schedule",
        "trigger.phase" => "GameTime",
        "trigger.bosses" => "VBloodKilled",
        "trigger.minMinutes" or "trigger.maxMinutes" => "Interval",
        "trigger.playerCooldownMinutes" => "RegionEntered",
        "trigger.factions" or "trigger.kills" or "trigger.windowSeconds" or "trigger.shared" => "FactionKills",
        "trigger.heat" or "trigger.warnAt" or "trigger.vbloodHeat" => "Revenge",
        _ => null,
    };

    /// <summary>Every trigger type a trigger field fits (revenge D11): <see cref="TriggerTypeOf"/>'s, and Revenge for the
    /// keys it shares (trigger.factions, trigger.windowSeconds, trigger.playerCooldownMinutes). The refusal names
    /// <see cref="TriggerTypeOf"/>'s type, as before.</summary>
    public static IReadOnlyList<string> TriggerTypesOf(string field) => field switch
    {
        "trigger.factions" or "trigger.windowSeconds" => ["FactionKills", "Revenge"],
        "trigger.playerCooldownMinutes" => ["RegionEntered", "Revenge"],
        _ => TriggerTypeOf(field) is { } type ? [type] : [],
    };

    /// <summary>trigger.warnAt (revenge D11): none, or 1-3 comma separated integers, strictly ascending, each at least 1
    /// and below the highest heat; the bound by the trigger's own heat is EventsEditor's, on the file.</summary>
    public const string WarnAtField = "trigger.warnAt";

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

    public const string TriggerTypeRule = "trigger.type must be Manual, Schedule, GameTime, VBloodKilled, Interval, RegionEntered, FactionKills or Revenge";

    static Arg<object> TriggerValue(string field, string? value)
    {
        static Arg<object> IntIn(string? v, int min, int max, string rule) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max ? Arg<object>.Of(i) : Arg<object>.Bad(rule);

        switch (field)
        {
            case "trigger.type":
                return value is "Manual" or "Schedule" or "GameTime" or "VBloodKilled" or "Interval" or "RegionEntered" or "FactionKills" or "Revenge"
                    ? Arg<object>.Of(value)
                    : Arg<object>.Bad(TriggerTypeRule);
            case "trigger.minMinutes":
                return IntIn(value, EventValidator.MinIntervalMinutes, EventValidator.MaxIntervalMinutes, EventValidator.MinMinutesRule);
            case "trigger.maxMinutes":
                return IntIn(value, EventValidator.MinIntervalMinutes, EventValidator.MaxIntervalMinutes, EventValidator.MaxMinutesRule);
            case "trigger.playerCooldownMinutes":
                return IntIn(value, 0, EventValidator.MaxPlayerCooldownMinutes, EventValidator.PlayerCooldownRule);
            case "trigger.kills":
                return IntIn(value, EventValidator.MinKills, EventValidator.MaxKills, EventValidator.KillsRule);
            case "trigger.windowSeconds":
                return IntIn(value, EventValidator.MinKillWindowSeconds, EventValidator.MaxKillWindowSeconds, EventValidator.KillWindowRule);
            case "trigger.shared":
                return value switch
                {
                    "true" => Arg<object>.Of(true),
                    "false" => Arg<object>.Of(false),
                    _ => Arg<object>.Bad(EventValidator.SharedRule),
                };
            case "trigger.factions":
                return Factions(field, value);
            case "trigger.heat":
                return IntIn(value, EventValidator.MinHeat, EventValidator.MaxHeat, EventValidator.HeatRule);
            case "trigger.vbloodHeat":
                return IntIn(value, 0, EventValidator.MaxVBloodHeat, EventValidator.VBloodHeatRule);
            case WarnAtField:
            {
                if (value == "none") return Arg<object>.Of(Array.Empty<int>());
                var parts = CommaList(value, EventValidator.MaxWarnAt);
                var list = new List<int>();
                foreach (var part in parts ?? [])
                {
                    if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var k) || k < 1 || k >= EventValidator.MaxHeat
                        || list.Count > 0 && k <= list[^1])
                        return Arg<object>.Bad(EventValidator.WarnAtRule);
                    list.Add(k);
                }
                return parts is null ? Arg<object>.Bad(EventValidator.WarnAtRule) : Arg<object>.Of(list.ToArray());
            }
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
            case "trigger.bosses":
            {
                const string rule = "trigger.bosses must be any or 1-20 CHAR_ names, comma separated";
                if (value == "any") return Arg<object>.Of(new[] { "any" });
                var parts = CommaList(value, 20);
                if (parts is null) return Arg<object>.Bad(rule);
                if (parts.Any(b => !IsNameValue(b, "CHAR_"))) return Arg<object>.Bad(NameRule(field));
                if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
                return Arg<object>.Of(parts);
            }
            default:
                return Arg<object>.Bad($"field {field} is not settable; edit events.json and reload");
        }
    }

    /// <summary>action.fanOut (automation D16): "none" removes it, else MAX SPACING in D4's ranges.</summary>
    static Arg<object> FanOutValue(string? value)
    {
        if (value == "none") return Arg<object>.Of(FieldRemoval.Instance);
        var parts = (value ?? "").Split(' ');
        if (parts.Length != 2) return Arg<object>.Bad(FanOutRule);
        if (!(int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var max)
              && max >= EventValidator.MinFanOutInstances && max <= EventValidator.MaxFanOutInstances))
            return Arg<object>.Bad(EventValidator.FanOutInstancesRule);
        if (!(int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var spacing)
              && spacing >= EventValidator.MinFanOutSpacing && spacing <= EventValidator.MaxFanOutSpacing))
            return Arg<object>.Bad(EventValidator.FanOutSpacingRule);
        return Arg<object>.Of(new FanOutArg(max, spacing));
    }

    /// <summary>action.factions: 1-5 distinct Faction_ names (event-library D10).</summary>
    static Arg<object> Factions(string field, string? value)
    {
        var parts = CommaList(value, EventValidator.MaxFactions);
        if (parts is null && field == "trigger.factions") return Arg<object>.Bad(EventValidator.TriggerFactionsRule);     // D10's rule (automation D16)
        if (parts is null) return Arg<object>.Bad($"{field} must be 1-{EventValidator.MaxFactions} Faction_ names, comma separated");
        if (parts.Any(f => !IsNameValue(f, "Faction_"))) return Arg<object>.Bad(NameRule(field));
        if (Repeated(parts) is { } twice) return Arg<object>.Bad(Twice(field, twice));
        return Arg<object>.Of(parts);
    }

    /// <summary>action.units: 1-10 entries CHAR_&lt;name&gt;[:&lt;count&gt;], count 1 to MaxUnitsPerWave's ceiling (default 1), each name once
    /// (event-library D10, S-8).</summary>
    static Arg<object> Units(string field, string? value)
    {
        var rule = $"action.units must be 1-10 entries CHAR_name or CHAR_name:count, count {EventValidator.CountRange}, comma separated";
        var parts = CommaList(value, 10);
        if (parts is null) return Arg<object>.Bad(rule);
        var entries = new List<UnitEntry>();
        foreach (var part in parts)
        {
            if (PoolEntry(field, part, rule) is { } pool)                        // wave-variety D10
            {
                if (!pool.Ok) return pool;
                entries.Add((UnitEntry)pool.Value!);
                continue;
            }
            var colon = part.IndexOf(':');
            var name = colon < 0 ? part : part[..colon];
            var count = 1;
            if (colon >= 0 && !(int.TryParse(part.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 1 && count <= Limits.MaxUnitsPerWave.Max))
                return Arg<object>.Bad(rule);
            if (!IsNameValue(name, "CHAR_")) return Arg<object>.Bad(NameRule(field));
            entries.Add(new UnitEntry(name, count));
        }
        if (Repeated(entries.Select(e => e.Pool is { } p ? PoolToken + p : e.Prefab)) is { } twice) return Arg<object>.Bad(Twice(field, twice));
        if (EventValidator.PastWaveSize(entries)) return Arg<object>.Bad(EventValidator.WaveSizeRule(field));   // wave-variety D2
        return Arg<object>.Of(entries.ToArray());
    }

    /// <summary>A scope value (regions D9, A28): "Global" (any case) or 1 to <see cref="RegionNames.Count"/> distinct names
    /// of <see cref="RegionNames"/> joined by commas, stored in the game's spelling; whether the map holds them is the
    /// validator's, on reload.</summary>
    static Arg<object> ScopeValue(string field, string? value)
    {
        var rule = $"{field} must be Global or 1-{RegionNames.Count} region names";
        if (string.IsNullOrEmpty(value)) return Arg<object>.Bad(rule);
        if (string.Equals(value, "Global", StringComparison.OrdinalIgnoreCase)) return Arg<object>.Of("Global");
        var parts = value.Split(',');
        if (parts.Length > RegionNames.Count) return Arg<object>.Bad(rule);
        var names = new List<string>();
        foreach (var part in parts)
        {
            if (part.Length == 0) return Arg<object>.Bad(rule);
            if (!RegionNames.TryCanonical(part, out var name)) return Arg<object>.Bad($"unknown region {part}");
            names.Add(name);
        }
        if (Repeated(names) is { } twice) return Arg<object>.Bad(Twice(field, twice));
        return Arg<object>.Of(names.ToArray());
    }

    /// <summary>True when `event set` takes <paramref name="field"/>: a name of the table, or a unit's chance
    /// `action.units.N.chance` with n 1-10 (the table's placeholder itself is no field).</summary>
    public static bool IsSettable(string field) =>
        field != UnitChanceField && field != PoolsField
        && !(field.StartsWith("action.waveList.", StringComparison.Ordinal) && field.Contains(".N", StringComparison.Ordinal))
        && SettableFields.ContainsKey(TableName(field));

    /// <summary>`location here`, `location aroundplayer &lt;minDist&gt; &lt;maxDist&gt;` (event-library D11, event-spawns
    /// D18), each distance in D6's range and minDist below maxDist, or `location region` (hunting-grounds D14).</summary>
    static Arg<object> LocationValue(string? value)
    {
        const string rule = LocationSetRule;
        if (value == "here") return Arg<object>.Of(LocationHere.Instance);
        if (string.Equals(value, "region", StringComparison.OrdinalIgnoreCase)) return Arg<object>.Of(RegionArg.Instance);
        var parts = (value ?? "").Split(' ');
        if (parts.Length != 3 || !string.Equals(parts[0], "aroundplayer", StringComparison.OrdinalIgnoreCase)) return Arg<object>.Bad(rule);
        if (!(int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var min) && min is >= 10 and <= 60))
            return Arg<object>.Bad(EventValidator.MinDistRule);
        if (!(int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var max) && max is >= 15 and <= 80))
            return Arg<object>.Bad(EventValidator.MaxDistRule);
        return min < max ? Arg<object>.Of(new AroundPlayerArg(min, max)) : Arg<object>.Bad(EventValidator.DistOrder);
    }

    /// <summary>A decimal in [min, max] with at most two decimals, '.' as the separator; null otherwise.</summary>
    static decimal? Decimal2(string? text, decimal min, decimal max)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 6 || text[0] == '.' || text[^1] == '.') return null;
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m)) return null;
        return m >= min && m <= max && decimal.Round(m, 2) == m ? m : null;
    }

    public const string LocationSetRule = "location takes here, aroundplayer MIN MAX or region";
    public const string BehaviourRule = "action.behaviour takes none, hunt RANGE or regionhunt";
    public const string ValueRequired = "value required";
    public const string UnitChanceRule = "action.units.N.chance must be 0.05-1.0 with at most two decimals";

    /// <summary>The event-spawns fields (D18), each value in D6's range: a modifier (or none), loot and allowTerritory
    /// true or false, behaviour none or hunt &lt;range&gt; (any other type is "unknown behaviour type"), and a unit's
    /// chance 0.05-1.0. Whether level and levelDelta end up together is EventsEditor's, on the file.</summary>
    static Arg<object> SpawnKeyValue(string field, string? value)
    {
        if (UnitChanceIndex(field) is not null)
            return Decimal2(value, (decimal)EventValidator.MinChance, 1.0m) is { } c ? Arg<object>.Of(c) : Arg<object>.Bad(UnitChanceRule);
        if (value == "none" && field is not ("action.loot" or "action.allowTerritory")) return Arg<object>.Of(FieldRemoval.Instance);
        switch (field)
        {
            case "action.loot" or "action.allowTerritory":
                return value switch
                {
                    "true" => Arg<object>.Of(true),
                    "false" => Arg<object>.Of(false),
                    _ => Arg<object>.Bad($"{field} must be true or false"),
                };
            case "action.behaviour":
            {
                var parts = (value ?? "").Split(' ');
                if (parts[0].Length == 0 || parts.Length > 2) return Arg<object>.Bad(BehaviourRule);
                if (string.Equals(parts[0], "regionhunt", StringComparison.OrdinalIgnoreCase))     // hunting-grounds D14: no range
                    return parts.Length == 1 ? Arg<object>.Of(RegionHuntArg.Instance) : Arg<object>.Bad(BehaviourRule);
                if (!string.Equals(parts[0], "hunt", StringComparison.OrdinalIgnoreCase))
                    return Arg<object>.Bad(IsNameValue("CHAR_" + parts[0], "CHAR_") ? EventValidator.UnknownBehaviour(parts[0].ToLowerInvariant()) : BehaviourRule);
                return parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var r)
                       && r >= EventValidator.MinHuntRange && r <= EventValidator.MaxHuntRange
                    ? Arg<object>.Of(new BehaviourArg(r)) : Arg<object>.Bad(EventValidator.HuntRangeRule);
            }
            case "action.modifiers.level":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 120
                    ? Arg<object>.Of(level) : Arg<object>.Bad(EventValidator.LevelRule);
            case "action.modifiers.levelDelta":
                return value is { Length: <= 2 } && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var delta)
                       && Math.Abs(delta) <= EventValidator.MaxLevelDelta
                    ? Arg<object>.Of(delta) : Arg<object>.Bad(EventValidator.LevelDeltaRule);
            default:        // a multiplier
                return Decimal2(value, (decimal)EventValidator.MinModifier, (decimal)EventValidator.MaxModifier) is { } m
                    ? Arg<object>.Of(m) : Arg<object>.Bad(EventValidator.ModifierRule(field["action.modifiers.".Length..]));
        }
    }

    /// <summary>A wave-list field's value (wave-sets D14), each in D1's range and with the validator's own reason:
    /// `action.waveList.N none`; units as action.units' entries, a prefab allowed twice (two entries of one prefab may
    /// carry different modifiers); afterSeconds 10-3600 or none; whenCleared true or false; a unit's modifier in D6's range
    /// or none.</summary>
    static Arg<object> WaveListValue(string field, WaveListField w, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Arg<object>.Bad(ValueRequired);
        var path = $"action.waveList.{w.Wave}";
        switch (w.Key)
        {
            case null:
                return value == "none" ? Arg<object>.Of(FieldRemoval.Instance) : Arg<object>.Bad($"{path} takes none (removes the wave)");
            case "afterSeconds":
                if (value == "none") return Arg<object>.Of(FieldRemoval.Instance);
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var after)
                       && after >= EventValidator.MinAfterSeconds && after <= EventValidator.MaxAfterSeconds
                    ? Arg<object>.Of(after) : Arg<object>.Bad(EventValidator.AfterSecondsRule(w.Wave));
            case "whenCleared":
                return value switch { "true" => Arg<object>.Of(true), "false" => Arg<object>.Of(false), _ => Arg<object>.Bad(EventValidator.WhenClearedRule(w.Wave)) };
            case "bosses":
                return BossList(field, value, waveLevel: true);                 // wave-variety D10
        }
        if (w.Modifier is null) return WaveUnits(field, value);
        var at = $"{path}.units.{w.Unit}.modifiers";
        if (value == "none") return Arg<object>.Of(FieldRemoval.Instance);
        switch (w.Modifier)
        {
            case "level":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 120
                    ? Arg<object>.Of(level) : Arg<object>.Bad(EventValidator.LevelRule.Replace("action.modifiers", at));
            case "levelDelta":
                return value is { Length: <= 2 } && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var delta)
                       && Math.Abs(delta) <= EventValidator.MaxLevelDelta
                    ? Arg<object>.Of(delta) : Arg<object>.Bad(EventValidator.LevelDeltaRule.Replace("action.modifiers", at));
            default:
                return Decimal2(value, (decimal)EventValidator.MinModifier, (decimal)EventValidator.MaxModifier) is { } m
                    ? Arg<object>.Of(m) : Arg<object>.Bad(EventValidator.ModifierRule(w.Modifier).Replace("action.modifiers", at));
        }
    }

    /// <summary>A wave's units: 1-10 entries CHAR_&lt;name&gt;[:&lt;count&gt;], count 1 to MaxUnitsPerWave's ceiling (default 1), a prefab allowed in
    /// more than one entry (wave-sets D3).</summary>
    static Arg<object> WaveUnits(string field, string value)
    {
        var rule = $"{field} must be 1-10 entries CHAR_name or CHAR_name:count, count {EventValidator.CountRange}, comma separated";
        var parts = CommaList(value, 10);
        if (parts is null) return Arg<object>.Bad(rule);
        var entries = new List<UnitEntry>();
        foreach (var part in parts)
        {
            if (PoolEntry(field, part, rule) is { } pool)                        // wave-variety D10
            {
                if (!pool.Ok) return pool;
                entries.Add((UnitEntry)pool.Value!);
                continue;
            }
            var colon = part.IndexOf(':');
            var name = colon < 0 ? part : part[..colon];
            var count = 1;
            if (colon >= 0 && !(int.TryParse(part.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 1 && count <= Limits.MaxUnitsPerWave.Max))
                return Arg<object>.Bad(rule);
            if (!IsNameValue(name, "CHAR_")) return Arg<object>.Bad(NameRule(field));
            entries.Add(new UnitEntry(name, count));
        }
        if (EventValidator.PastWaveSize(entries)) return Arg<object>.Bad(EventValidator.WaveSizeRule(field));   // wave-variety D2
        return Arg<object>.Of(entries.ToArray());
    }

    /// <summary>`event set` whitelist (foundation S-10, event-library D9, D10, D11): field → validator of the new value.
    /// Only the character set and shape are checked here, before any write; name knowledge is the validator's, on
    /// reload. Whether the field fits the event's trigger or action type is checked on the file by EventsEditor.</summary>
    public static Arg<object> SettableValue(string field, string? value)
    {
        if (!IsSettable(field)) return Arg<object>.Bad($"field {field} is not settable; edit events.json and reload");
        if ((SpawnKeyFields.Contains(TableName(field)) || field is "location" or FanOutField) && string.IsNullOrWhiteSpace(value))
            return Arg<object>.Bad(ValueRequired);                   // event-spawns D18's empty input
        if (SwarmFields.Contains(field)) return string.IsNullOrWhiteSpace(value) ? Arg<object>.Bad(ValueRequired) : SwarmValue(field, value);
        if (field == FanOutField) return FanOutValue(value);
        if (field == RegionLockField) return RegionLockValue(value);
        if (field == BossesField) return BossesValue(value);
        if (field == PursuitField) return PursuitValue(value);
        if (PoolOf(field) is { } pool) return PoolValue(field, pool, value);
        if (ApocalypseFields.Contains(field)) return ApocalypseValue(field, value);
        if (DestinationFields.Contains(field)) return DestinationValue(field, value);
        if (HordeFields.Contains(field)) return HordeValue(field, value);                 // undead-hordes D20
        if (field == ScoreboardField)
            return value switch { "true" => Arg<object>.Of(true), "false" => Arg<object>.Of(false), _ => Arg<object>.Bad(EventValidator.ScoreboardRule) };
        if (WaveListPath(field) is { } wave) return WaveListValue(field, wave, value);
        if (SpawnKeyFields.Contains(TableName(field))) return SpawnKeyValue(field, value);
        if (StatFields.Contains(field)) return Stat(field, value);
        if (TriggerFields.Contains(field)) return TriggerValue(field, value);
        if (field == "action.factions") return Factions(field, value);
        if (field == "action.units") return Units(field, value);
        if (field is "trigger.scope" or "action.scope") return ScopeValue(field, value);
        if (field == "location") return LocationValue(value);
        static Arg<object> IntIn(string f, string? v, int min, int max) =>
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= min && i <= max
                ? Arg<object>.Of(i)
                : Arg<object>.Bad($"{f} must be {min}-{max}");

        return field switch
        {
            "name" => value is not null && EventValidator.IsPlainText(value, 40)
                ? Arg<object>.Of(value)
                : Arg<object>.Bad("name must be 1-40 characters, no angle brackets or control characters"),
            "durationSeconds" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s >= ApocalypseLimits.OtherMinDurationSeconds && s <= ApocalypseLimits.MaxDurationSeconds
                ? Arg<object>.Of(s) : Arg<object>.Bad(DurationRule),
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

/// <summary>A wave-list field (wave-sets D14): wave <see cref="Wave"/>, its <see cref="Key"/> (units, afterSeconds,
/// whenCleared; null for the wave itself), and for a unit's modifier the entry <see cref="Unit"/> and the
/// <see cref="Modifier"/>; <see cref="Table"/> is its name in the field table.</summary>
public sealed record WaveListField(int Wave, string? Key, int? Unit, string? Modifier, string Table);

/// <summary>The value "none" of an event-spawns field (event-spawns D18): the key is removed from the action, and a
/// modifiers object left empty goes with it.</summary>
public sealed class FieldRemoval
{
    public static readonly FieldRemoval Instance = new();
    FieldRemoval() { }
    public override string ToString() => "none";
}

/// <summary>`action.pursuit &lt;afterSeconds&gt;` (wave-variety D10): written as action.pursuit { "afterSeconds": s }.</summary>
public readonly record struct PursuitArg(int AfterSeconds)
{
    public override string ToString() => AfterSeconds.ToString(CultureInfo.InvariantCulture);
}

/// <summary>`action.pools.&lt;name&gt; +&lt;prefab&gt;` or `-&lt;prefab&gt;` (wave-variety D10): one member added or removed.</summary>
public readonly record struct PoolEdit(bool Add, string Prefab)
{
    public override string ToString() => (Add ? "+" : "-") + Prefab;
}

/// <summary>`action.behaviour hunt &lt;range&gt;` (event-spawns D18): written as { "type": "Hunt", "range": r }.</summary>
public readonly record struct BehaviourArg(int Range)
{
    public override string ToString() => FormattableString.Invariant($"hunt {Range}");
}

/// <summary>`action.behaviour regionhunt` (hunting-grounds D14): written as { "type": "RegionHunt" }.</summary>
public sealed class RegionHuntArg
{
    public static readonly RegionHuntArg Instance = new();
    RegionHuntArg() { }
    public override string ToString() => "regionhunt";
}

/// <summary>`location region` (hunting-grounds D14): written as action.location { "type": "Region" }.</summary>
public sealed class RegionArg
{
    public static readonly RegionArg Instance = new();
    RegionArg() { }
    public override string ToString() => "region";
}

/// <summary>`action.fanOut &lt;maxInstances&gt; &lt;minSpacing&gt;` (automation D16): written as action.fanOut
/// { "maxInstances", "minSpacing" }.</summary>
public readonly record struct FanOutArg(int MaxInstances, int MinSpacing)
{
    public override string ToString() => FormattableString.Invariant($"{MaxInstances} {MinSpacing}");
}

/// <summary>`location aroundplayer &lt;minDist&gt; &lt;maxDist&gt;` (event-spawns D18): written as action.location
/// { "type": "AroundPlayer", "minDist", "maxDist" }.</summary>
public readonly record struct AroundPlayerArg(int MinDist, int MaxDist)
{
    public override string ToString() => FormattableString.Invariant($"aroundplayer {MinDist} {MaxDist}");
}

/// <summary>The value of `.nyar event set &lt;id&gt; location here`: the command reads the admin's position and passes
/// it through <see cref="LocationArg.FromPosition"/> (event-library D11).</summary>
public sealed class LocationHere
{
    public static readonly LocationHere Instance = new();
    LocationHere() { }
}

/// <summary>A map point for action.location, x, y (the height, A20) and z rounded to 0.1 (event-library D11).</summary>
public readonly record struct PointArg(decimal X, decimal Y, decimal Z)
{
    public override string ToString() => FormattableString.Invariant($"Point {X}, {Z} at height {Y}");
}

public static class LocationArg
{
    public const decimal Bound = 10000m;
    public const string OutOfBounds = "location here must be within -10000..10000";
    public const string NoCharacter = "location here needs your character in the world";

    /// <summary>The admin's position as a point, each axis rounded to 0.1; a position beyond ±10000 is refused.</summary>
    public static Arg<PointArg> FromPosition(float x, float y, float z)
    {
        float[] axes = [x, y, z];
        if (axes.Any(a => !float.IsFinite(a) || Math.Abs(a) > (float)Bound)) return Arg<PointArg>.Bad(OutOfBounds);
        var (px, py, pz) = (Round(x), Round(y), Round(z));
        if (Math.Abs(px) > Bound || Math.Abs(py) > Bound || Math.Abs(pz) > Bound) return Arg<PointArg>.Bad(OutOfBounds);
        return Arg<PointArg>.Of(new PointArg(px, py, pz));
    }

    // + 0.0m gives every value one decimal place, so 10000 reads "10000.0" like 800.0 (the scale of a sum is the larger).
    static decimal Round(float v) => Math.Round((decimal)v, 1, MidpointRounding.AwayFromZero) + 0.0m;

    /// <summary>`location here` from a context that may have no character or position (the server console, or a
    /// character not yet in the world): <paramref name="read"/> returns null, or throws, and the reply is
    /// <see cref="NoCharacter"/> (event-library D19).</summary>
    public static Arg<PointArg> FromContext(Func<(float X, float Y, float Z)?> read)
    {
        (float X, float Y, float Z)? at;
        try { at = read(); }
        catch (Exception) { at = null; }
        return at is { } p ? FromPosition(p.X, p.Y, p.Z) : Arg<PointArg>.Bad(NoCharacter);
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
