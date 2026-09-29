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

/// <summary>What the validator needs to know about factions (faction-empowerment D1): the game's Faction_* prefab
/// names. The service layer backs it with PrefabCollectionSystem; tests back it with a fixed set.</summary>
public interface IFactionCatalog
{
    bool IsKnown(string factionName);
}

/// <summary>A catalog that knows no faction: every Empower definition checked against it is disabled.</summary>
public sealed class NoFactions : IFactionCatalog
{
    public static readonly NoFactions Instance = new();
    public bool IsKnown(string factionName) => false;
}

/// <summary>Factions no Empower action may name or reach (faction-empowerment D1, D3, Business rules 4): players and
/// their servants and derived factions (Faction_Players*), traders (Faction_Traders*), critters, prisoners and
/// Faction_Ignored. The prefixes cover the derived factions of Reference Data/unit_index.tsv
/// (Faction_Players_Castle_Prisoners, Faction_Players_Mutant, Faction_Players_Shapeshift_Human, Faction_Traders_T01,
/// Faction_Traders_T02).</summary>
public static class FactionDenyList
{
    static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "Faction_Critters",
        "Faction_World_Prisoners",
        "Faction_Ignored",
    };

    public static bool IsDenied(string faction) =>
        Exact.Contains(faction)
        || faction.StartsWith("Faction_Players", StringComparison.Ordinal)
        || faction.StartsWith("Faction_Traders", StringComparison.Ordinal);

    /// <summary>"Faction_Legion" → "Legion"; a name without the prefix is returned unchanged.</summary>
    public static string ShortName(string faction) =>
        faction.StartsWith("Faction_", StringComparison.Ordinal) ? faction["Faction_".Length..] : faction;
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
        new HashSet<string>(StringComparer.Ordinal) { "faction", "minutes", "event", "zone", "wave", "waves", "region" };

    static readonly Regex IdRx = new("^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant);
    static readonly Regex PlaceholderRx = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    static readonly HashSet<string> EventKeys = new(StringComparer.Ordinal)
    { "id", "name", "enabled", "pillar", "trigger", "conditions", "durationSeconds", "action", "announce" };

    /// <summary>The keys an event object may carry (event-library D1's template check).</summary>
    public static IReadOnlySet<string> EventKeyNames => EventKeys;

    /// <summary>Parses events.json. <paramref name="factions"/> checks Empower factions; when null, a unit catalog that
    /// is also an <see cref="IFactionCatalog"/> serves, otherwise no faction is known. <paramref name="regions"/> checks
    /// scopes the same way (regions D3, D7): when none is known, a regional scope disables its definition.</summary>
    public static LoadResult Parse(string text, IUnitCatalog units, IFactionCatalog? factions = null, IRegionCatalog? regions = null)
    {
        factions ??= units as IFactionCatalog ?? NoFactions.Instance;
        regions ??= units as IRegionCatalog ?? NoRegions.Instance;
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
                var def = ParseEvent(e, index, units, factions, regions);
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

    static EventDefinition ParseEvent(JsonElement e, int index, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        var fallbackId = $"#{index}";
        if (e.ValueKind != JsonValueKind.Object)
            return Disabled(fallbackId, "an event must be an object");

        var id = e.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : null;
        var reportId = id is not null && IdRx.IsMatch(id) ? id : fallbackId;
        try
        {
            foreach (var p in e.EnumerateObject())
                if (!EventKeys.Contains(p.Name)) throw new Fail(UnknownField("", p.Name));

            if (id is null || !IdRx.IsMatch(id)) throw new Fail("id must be 1-32 of a-z 0-9 -");
            var name = Required(e, "name", "name must be 1-40 characters, no angle brackets or control characters");
            if (name.ValueKind != JsonValueKind.String || !IsPlainText(name.GetString()!, 40))
                throw new Fail("name must be 1-40 characters, no angle brackets or control characters");
            var enabledEl = Required(e, "enabled", "enabled must be true or false");
            if (enabledEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new Fail("enabled must be true or false");
            var pillar = ParsePillar(Required(e, "pillar", "pillar is required"));
            var trigger = ParseTrigger(Required(e, "trigger", "trigger is required"), units, factions, regions);
            var conditions = e.TryGetProperty("conditions", out var c) ? ParseConditions(c) : new Conditions();
            var duration = Int(Required(e, "durationSeconds", "durationSeconds must be 30-7200"), 30, 7200, "durationSeconds must be 30-7200");
            var (action, empower) = ParseAction(Required(e, "action", "action is required"), pillar, trigger, units, factions, regions);
            var announce = e.TryGetProperty("announce", out var a) ? ParseAnnounce(a) : Announce.None;
            return new EventDefinition(id, name.GetString()!, enabledEl.GetBoolean(), pillar, trigger, conditions, duration, action, announce,
                Empower: empower);
        }
        catch (Fail f)
        {
            // Keep the pillar and name when they parse, so readiness follows the definition's own pillar switch
            // (event-library A6, Business rules 7).
            Pillar? pillar = null;
            try { if (e.TryGetProperty("pillar", out var pe)) pillar = ParsePillar(pe); }
            catch (Fail) { }
            var name = e.TryGetProperty("name", out var ne) && ne.ValueKind == JsonValueKind.String && IsPlainText(ne.GetString()!, 40) ? ne.GetString() : null;
            return Disabled(reportId, f.Message, pillar, name);
        }
    }

    static EventDefinition Disabled(string id, string reason, Pillar? pillar = null, string? name = null) =>
        new(id, name ?? id, false, pillar ?? Pillar.Spawns, Trigger.Manual(), new Conditions(), 30, null, Announce.None, reason);

    static JsonElement Required(JsonElement obj, string key, string rule) =>
        obj.TryGetProperty(key, out var v) ? v : throw new Fail(rule);

    static void OnlyKeys(JsonElement obj, string prefix, params string[] keys)
    {
        foreach (var p in obj.EnumerateObject())
            if (Array.IndexOf(keys, p.Name) < 0) throw new Fail(UnknownField(prefix, p.Name));
    }

    static int Int(JsonElement v, int min, int max, string rule)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var i) || i < min || i > max) throw new Fail(rule);
        return i;
    }

    static string Str(JsonElement v, string rule) =>
        v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new Fail(rule);

    /// <summary>1..max characters, no '&lt;', '&gt;' or control characters.</summary>
    /// <summary>"unknown field &lt;prefix&gt;.&lt;name&gt;"; a name longer than 32 characters or not plain text is left out
    /// ("unknown field in &lt;prefix&gt;"), so the reason stays within one chat line (event-spawns D32, A53).</summary>
    public static string UnknownField(string prefix, string name) =>
        IsPlainText(name, 32) ? $"unknown field {(prefix.Length == 0 ? name : $"{prefix}.{name}")}"
        : prefix.Length == 0 ? "unknown field" : $"unknown field in {prefix}";

    /// <summary>" &lt;value&gt;" for a reason that echoes the admin's input, or "" when the value is longer than a name may
    /// be (96) or not plain text, so no reason outgrows one chat line or carries rich text (event-spawns D32, A53).</summary>
    public static string Shown(string? value) => value is not null && IsPlainText(value, CommandArgs.MaxNameLength) ? " " + value : "";

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
            _ => throw new Fail($"unknown pillar{Shown(s)}"),
        };
    }

    /// <summary>The trigger by its type, then its optional scope (regions D3): every trigger type takes one, and
    /// RegionEntered needs one that names regions (automation D8).</summary>
    static Trigger ParseTrigger(JsonElement t, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        var trigger = ParseTriggerType(t, units, factions);
        if (t.TryGetProperty("scope", out var s)) trigger = trigger with { Scope = ParseScope(s, "trigger.scope", regions) };
        if (trigger.Type == TriggerType.RegionEntered && trigger.Scope.IsGlobal) throw new Fail(RegionEnteredScopeRule);
        return trigger;
    }

    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 1440;
    public const int MaxPlayerCooldownMinutes = 1440;
    public const int DefaultPlayerCooldownMinutes = 30;
    public const int MinKills = 3;
    public const int MaxKills = 500;
    public const int MinKillWindowSeconds = 10;
    public const int MaxKillWindowSeconds = 3600;
    public const string MinMinutesRule = "trigger.minMinutes must be 5-1440";
    public const string MaxMinutesRule = "trigger.maxMinutes must be 5-1440";
    public const string IntervalOrder = "trigger.minMinutes must be at most trigger.maxMinutes";
    public const string RegionEnteredScopeRule = "trigger.scope must name regions for RegionEntered";
    public const string PlayerCooldownRule = "trigger.playerCooldownMinutes must be 0-1440";
    public const string TriggerFactionsRule = "trigger.factions must be 1-5 faction names";
    public const string KillsRule = "trigger.kills must be 3-500";
    public const string KillWindowRule = "trigger.windowSeconds must be 10-3600";
    public const string SharedRule = "trigger.shared must be true or false";

    /// <summary>1-5 distinct factions, each not deny-listed and known to <paramref name="factions"/>; the Empower action's
    /// rules and reasons (faction-empowerment D1), under <paramref name="rule"/>.</summary>
    static List<string> FactionNames(JsonElement f, string rule, IFactionCatalog factions)
    {
        if (f.ValueKind != JsonValueKind.Array || f.GetArrayLength() is < 1 or > MaxFactions) throw new Fail(rule);
        var list = new List<string>();
        foreach (var x in f.EnumerateArray())
        {
            var name = Str(x, rule);
            if (list.Contains(name)) throw new Fail(rule);
            if (FactionDenyList.IsDenied(name)) throw new Fail($"faction{Shown(name)} is deny-listed");
            if (!name.StartsWith("Faction_", StringComparison.Ordinal) || !factions.IsKnown(name)) throw new Fail($"unknown faction{Shown(name)}");
            list.Add(name);
        }
        return list;
    }

    /// <summary>`scope` (regions D3, A3, A15): "Global" or 1 to <see cref="RegionNames.Count"/> distinct region names,
    /// matched case-insensitively and kept in the game's spelling. A regional scope needs the region index (D7's
    /// "regions unavailable" first), then a polygon for every name.</summary>
    static Scope ParseScope(JsonElement v, string field, IRegionCatalog regions)
    {
        var rule = $"{field} must be Global or 1-{RegionNames.Count} region names";
        if (v.ValueKind == JsonValueKind.String)
            return string.Equals(v.GetString(), "Global", StringComparison.OrdinalIgnoreCase) ? Scope.Global : throw new Fail(rule);
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() < 1 || v.GetArrayLength() > RegionNames.Count) throw new Fail(rule);
        var names = new List<string>();
        foreach (var x in v.EnumerateArray())
        {
            var name = Str(x, rule);
            if (!RegionNames.TryCanonical(name, out var canonical)) throw new Fail($"unknown region{Shown(name)}");
            if (names.Contains(canonical)) throw new Fail(rule);
            names.Add(canonical);
        }
        if (!regions.Available) throw new Fail("regions unavailable");
        foreach (var n in names)
            if (!regions.OnMap(n)) throw new Fail($"region {n} is not on the map");
        return new Scope(names);
    }

    static Trigger ParseTriggerType(JsonElement t, IUnitCatalog units, IFactionCatalog factions)
    {
        if (t.ValueKind != JsonValueKind.Object) throw new Fail("trigger must be an object with a type");
        var type = t.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        switch (type)
        {
            case "Manual":
                OnlyKeys(t, "trigger", "type", "scope");
                return Trigger.Manual();
            case "Schedule":
            {
                OnlyKeys(t, "trigger", "type", "days", "times", "scope");
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
                OnlyKeys(t, "trigger", "type", "phase", "scope");
                var p = Str(Required(t, "phase", "trigger.phase must be day or night"), "trigger.phase must be day or night");
                var phase = p switch { "day" => DayPhase.Day, "night" => DayPhase.Night, _ => throw new Fail("trigger.phase must be day or night") };
                return new Trigger(TriggerType.GameTime, [], [], phase, []);
            }
            case "VBloodKilled":
            {
                OnlyKeys(t, "trigger", "type", "bosses", "scope");
                const string rule = "trigger.bosses must be [\"any\"] or 1-20 CHAR_ names";
                var b = Required(t, "bosses", rule);
                if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() is < 1 or > 20) throw new Fail(rule);
                var bosses = new List<string>();
                foreach (var x in b.EnumerateArray()) bosses.Add(Str(x, rule));
                if (bosses.Contains("any") && bosses.Count > 1) throw new Fail(rule);
                foreach (var boss in bosses)
                    if (boss != "any" && (!boss.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(boss))) throw new Fail($"unknown unit{Shown(boss)}");
                return new Trigger(TriggerType.VBloodKilled, [], [], DayPhase.Night, bosses);
            }
            case "Interval":
            {
                OnlyKeys(t, "trigger", "type", "minMinutes", "maxMinutes", "scope");
                var min = Int(Required(t, "minMinutes", MinMinutesRule), MinIntervalMinutes, MaxIntervalMinutes, MinMinutesRule);
                var max = Int(Required(t, "maxMinutes", MaxMinutesRule), MinIntervalMinutes, MaxIntervalMinutes, MaxMinutesRule);
                if (min > max) throw new Fail(IntervalOrder);
                return new Trigger(TriggerType.Interval, [], [], DayPhase.Night, [], MinMinutes: min, MaxMinutes: max);
            }
            case "RegionEntered":
            {
                OnlyKeys(t, "trigger", "type", "scope", "playerCooldownMinutes");
                if (!t.TryGetProperty("scope", out _)) throw new Fail(RegionEnteredScopeRule);
                var cooldown = t.TryGetProperty("playerCooldownMinutes", out var pc)
                    ? Int(pc, 0, MaxPlayerCooldownMinutes, PlayerCooldownRule)
                    : DefaultPlayerCooldownMinutes;
                return new Trigger(TriggerType.RegionEntered, [], [], DayPhase.Night, [], PlayerCooldownMinutes: cooldown);
            }
            case "FactionKills":
            {
                OnlyKeys(t, "trigger", "type", "factions", "kills", "windowSeconds", "shared", "scope");
                var list = FactionNames(Required(t, "factions", TriggerFactionsRule), TriggerFactionsRule, factions);
                var kills = Int(Required(t, "kills", KillsRule), MinKills, MaxKills, KillsRule);
                var window = Int(Required(t, "windowSeconds", KillWindowRule), MinKillWindowSeconds, MaxKillWindowSeconds, KillWindowRule);
                var shared = t.TryGetProperty("shared", out var sh) && Bool(sh, SharedRule);
                return new Trigger(TriggerType.FactionKills, [], [], DayPhase.Night, [], Factions: list, Kills: kills, WindowSeconds: window, Shared: shared);
            }
            default:
                throw new Fail($"unknown trigger type{Shown(type)}");
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

    /// <summary>The action by its type, exactly one of the pair set. The pairing rule (faction-empowerment D2) is checked
    /// on the type, before the action's own fields: pillar empowerment takes an Empower action and an Empower action
    /// needs pillar empowerment.</summary>
    static (SpawnWavesAction? Waves, EmpowerAction? Empower) ParseAction(JsonElement a, Pillar pillar, Trigger trigger,
        IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        if (a.ValueKind != JsonValueKind.Object) throw new Fail("action must be an object with a type");
        var type = a.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        switch (type)
        {
            case "SpawnWaves":
                if (pillar == Pillar.Empowerment) throw new Fail("pillar empowerment takes an Empower action");
                return (ParseSpawnWaves(a, trigger, units, regions), null);
            case "Empower":
                if (pillar != Pillar.Empowerment) throw new Fail("action Empower needs pillar empowerment");
                return (null, ParseEmpower(a, units, factions, regions));
            default:
                throw new Fail($"unknown action type{Shown(type)}");
        }
    }

    public const int MaxFactions = 5;
    public const int MaxUnitList = 20;
    public const double MinStat = 1.0;
    public const double MaxStat = 3.0;

    /// <summary>The stat keys of an Empower action, in the order they are listed and applied.</summary>
    public static readonly IReadOnlyList<string> StatKeys = ["physicalPower", "spellPower", "maxHealth", "attackSpeed", "moveSpeed"];

    static EmpowerAction ParseEmpower(JsonElement a, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        OnlyKeys(a, "action", "type", "factions", "includeUnits", "excludeUnits", "includeVBloods", "stats", "scope");

        const string factionsRule = "action.factions must be 1-5 distinct Faction_ names";
        var factionList = FactionNames(Required(a, "factions", factionsRule), factionsRule, factions);

        var include = UnitList(a, "includeUnits", units, refuseDenied: true);
        var exclude = UnitList(a, "excludeUnits", units, refuseDenied: false);

        var vbloods = false;
        if (a.TryGetProperty("includeVBloods", out var vb))
        {
            if (vb.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new Fail("action.includeVBloods must be true or false");
            vbloods = vb.GetBoolean();
        }

        const string statsRule = "action.stats must be an object of physicalPower, spellPower, maxHealth, attackSpeed, moveSpeed";
        var st = Required(a, "stats", statsRule);
        if (st.ValueKind != JsonValueKind.Object) throw new Fail(statsRule);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in st.EnumerateObject())
        {
            if (!StatKeys.Contains(p.Name)) throw new Fail(UnknownField("action.stats", p.Name));
            if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetDouble(out var v) || double.IsNaN(v) || v < MinStat || v > MaxStat)
                throw new Fail($"action.stats.{p.Name} must be a number 1.0-3.0");
            values[p.Name] = v;
        }
        if (!values.Values.Any(v => v > MinStat)) throw new Fail("action.stats must raise at least one stat above 1.0");
        double Get(string key) => values.TryGetValue(key, out var v) ? v : MinStat;
        var stats = new EmpowerStats(Get("physicalPower"), Get("spellPower"), Get("maxHealth"), Get("attackSpeed"), Get("moveSpeed"));
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        return new EmpowerAction(factionList, include, exclude, vbloods, stats, scope);
    }

    /// <summary>includeUnits or excludeUnits: 0-20 distinct known CHAR_ names, default []; includeUnits also refuses
    /// deny-listed units.</summary>
    static IReadOnlyList<string> UnitList(JsonElement a, string key, IUnitCatalog units, bool refuseDenied)
    {
        if (!a.TryGetProperty(key, out var v)) return [];
        var rule = $"action.{key} must be 0-20 distinct CHAR_ names";
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() > MaxUnitList) throw new Fail(rule);
        var list = new List<string>();
        foreach (var x in v.EnumerateArray())
        {
            var prefab = Str(x, rule);
            if (list.Contains(prefab)) throw new Fail(rule);
            if (refuseDenied && (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab))) throw new Fail($"unit{Shown(prefab)} is deny-listed");
            if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit{Shown(prefab)}");
            list.Add(prefab);
        }
        return list;
    }

    static SpawnWavesAction ParseSpawnWaves(JsonElement a, Trigger trigger, IUnitCatalog units, IRegionCatalog regions)
    {
        OnlyKeys(a, "action", "type", "units", "waves", "intervalSeconds", "radius", "location", "unitLifetimeSeconds", "scope",
            "modifiers", "loot", "behaviour", "allowTerritory", "fanOut");

        const string unitsRule = "action.units must be 1-10 entries { \"prefab\": CHAR_ name, \"count\": 1-50 }";
        var u = Required(a, "units", unitsRule);
        if (u.ValueKind != JsonValueKind.Array || u.GetArrayLength() is < 1 or > 10) throw new Fail(unitsRule);
        var list = new List<UnitEntry>();
        foreach (var x in u.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.Object) throw new Fail(unitsRule);
            OnlyKeys(x, "action.units", "prefab", "count", "chance");
            var prefab = Str(Required(x, "prefab", unitsRule), unitsRule);
            if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail($"unit{Shown(prefab)} is deny-listed");
            if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit{Shown(prefab)}");
            var count = Int(Required(x, "count", unitsRule), 1, 50, "action.units.count must be 1-50");
            var chance = x.TryGetProperty("chance", out var ch) ? Number(ch, MinChance, 1.0, ChanceRule) : 1.0;
            list.Add(new UnitEntry(prefab, count, chance));
        }
        var waves = Int(Required(a, "waves", "action.waves must be 1-10"), 1, 10, "action.waves must be 1-10");
        var interval = Int(Required(a, "intervalSeconds", "action.intervalSeconds must be 10-600"), 10, 600, "action.intervalSeconds must be 10-600");
        var radius = Int(Required(a, "radius", "action.radius must be 2-30"), 2, 30, "action.radius must be 2-30");
        var location = ParseLocation(Required(a, "location", "action.location is required"), trigger);
        int? lifetime = a.TryGetProperty("unitLifetimeSeconds", out var lt) ? Int(lt, 30, 7200, "action.unitLifetimeSeconds must be 30-7200") : null;
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        // regions D6: a Point outside the scope is refused here; an Admin origin is checked at the start (ScopeGate).
        if (!scope.IsGlobal && location.Type == LocationType.Point && !scope.Names(regions.RegionOf(location.X, location.Z)))
            throw new Fail("action.location is outside action.scope");
        var modifiers = a.TryGetProperty("modifiers", out var mo) ? ParseModifiers(mo) : null;
        var loot = a.TryGetProperty("loot", out var lo) && Bool(lo, "action.loot must be true or false");
        var behaviour = a.TryGetProperty("behaviour", out var be) ? ParseBehaviour(be) : null;
        var allowTerritory = a.TryGetProperty("allowTerritory", out var at) && Bool(at, "action.allowTerritory must be true or false");
        if (behaviour is { Type: BehaviourType.Hunt } hunt && location.Type == LocationType.AroundPlayer && location.MaxDist > hunt.Range)
            throw new Fail(MaxDistOverRange);
        var fanOut = a.TryGetProperty("fanOut", out var fo) ? ParseFanOut(fo, location) : null;
        return new SpawnWavesAction(list, waves, interval, radius, location, lifetime, scope, modifiers, loot, behaviour, allowTerritory, fanOut);
    }

    public const int MinFanOutInstances = 2;
    public const int MaxFanOutInstances = 10;
    public const int MinFanOutSpacing = 50;
    public const int MaxFanOutSpacing = 500;
    public const string FanOutObject = "action.fanOut must be an object";
    public const string FanOutLocation = "action.fanOut needs an AroundPlayer location";
    public const string FanOutInstancesRule = "action.fanOut.maxInstances must be 2-10";
    public const string FanOutSpacingRule = "action.fanOut.minSpacing must be 50-500";

    /// <summary>`fanOut` (automation D4): { "maxInstances": 2-10, "minSpacing": 50-500 }, only with an AroundPlayer location.</summary>
    static FanOut ParseFanOut(JsonElement f, Location location)
    {
        if (f.ValueKind != JsonValueKind.Object) throw new Fail(FanOutObject);
        if (location.Type != LocationType.AroundPlayer) throw new Fail(FanOutLocation);
        OnlyKeys(f, "action.fanOut", "maxInstances", "minSpacing");
        var n = Int(Required(f, "maxInstances", FanOutInstancesRule), MinFanOutInstances, MaxFanOutInstances, FanOutInstancesRule);
        var spacing = Int(Required(f, "minSpacing", FanOutSpacingRule), MinFanOutSpacing, MaxFanOutSpacing, FanOutSpacingRule);
        return new FanOut(n, spacing);
    }

    public const double MinChance = 0.05;
    public const string ChanceRule = "action.units.chance must be a number 0.05-1.0";
    public const double MinModifier = 0.5;
    public const double MaxModifier = 3.0;
    public const int MaxLevelDelta = 5;
    public const int MinHuntRange = 10;
    public const int MaxHuntRange = 60;
    public const string EmptyModifiers = "action.modifiers must name at least one modifier";
    public const string BothLevels = "action.modifiers takes level or levelDelta, not both";
    public const string LevelRule = "action.modifiers.level must be 1-120";
    public const string LevelDeltaRule = "action.modifiers.levelDelta must be -5..5";
    public const string HuntRangeRule = "action.behaviour.range must be 10-60";
    public const string MaxDistOverRange = "action.location.maxDist must be at most behaviour.range for Hunt";
    public const string MinDistRule = "action.location.minDist must be 10-60";
    public const string MaxDistRule = "action.location.maxDist must be 15-80";
    public const string DistOrder = "action.location.minDist must be less than maxDist";

    /// <summary>The multiplier keys of `modifiers`, in the order they are listed and applied (D6, D9).</summary>
    public static readonly IReadOnlyList<string> ModifierKeys = ["maxHealth", "power", "moveSpeed", "attackSpeed"];

    public static string ModifierRule(string key) => $"action.modifiers.{key} must be a number 0.5-3.0 with at most two decimals";
    /// <summary>"unknown behaviour type &lt;t&gt;"; a type longer than 32 characters or not plain text is left out, so the
    /// reason stays within one chat line (D32).</summary>
    public static string UnknownBehaviour(string type) =>
        type.Length <= 32 && IsPlainText(type, 32) ? $"unknown behaviour type {type}" : "unknown behaviour type";

    /// <summary>`modifiers` (D6): level 1-120 or levelDelta -5..5, never both, and multipliers 0.5-3.0 with at most two
    /// decimals; an empty object is refused.</summary>
    static SpawnModifiers ParseModifiers(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) throw new Fail("action.modifiers must be an object");
        var r = new SpawnModifiers();
        var any = false;
        foreach (var p in m.EnumerateObject())
        {
            any = true;
            r = p.Name switch
            {
                "level" => r with { Level = Int(p.Value, 1, 120, LevelRule) },
                "levelDelta" => r with { LevelDelta = Int(p.Value, -MaxLevelDelta, MaxLevelDelta, LevelDeltaRule) },
                "maxHealth" => r with { MaxHealth = Multiplier(p.Value, p.Name) },
                "power" => r with { Power = Multiplier(p.Value, p.Name) },
                "moveSpeed" => r with { MoveSpeed = Multiplier(p.Value, p.Name) },
                "attackSpeed" => r with { AttackSpeed = Multiplier(p.Value, p.Name) },
                _ => throw new Fail(UnknownField("action.modifiers", p.Name)),
            };
        }
        if (!any) throw new Fail(EmptyModifiers);
        if (r.Level is not null && r.LevelDelta is not null) throw new Fail(BothLevels);
        return r;
    }

    /// <summary>A multiplier of `modifiers`: a JSON number 0.5-3.0 with at most two decimals (S-8).</summary>
    static double Multiplier(JsonElement v, string key)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var d) || d < (decimal)MinModifier || d > (decimal)MaxModifier
            || decimal.Round(d, 2) != d)
            throw new Fail(ModifierRule(key));
        return (double)d;
    }

    /// <summary>`behaviour` (D6): { "type": "Hunt", "range": 10-60 }; any other type, Guard and Ambush included until
    /// child spawn-extras, is unknown.</summary>
    static Behaviour ParseBehaviour(JsonElement b)
    {
        if (b.ValueKind != JsonValueKind.Object) throw new Fail("action.behaviour must be { \"type\": \"Hunt\", \"range\": 10-60 }");
        var type = b.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        if (type != "Hunt") throw new Fail(UnknownBehaviour(type));
        OnlyKeys(b, "action.behaviour", "type", "range");
        return new Behaviour(BehaviourType.Hunt, Int(Required(b, "range", HuntRangeRule), MinHuntRange, MaxHuntRange, HuntRangeRule));
    }

    static bool Bool(JsonElement v, string rule) =>
        v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new Fail(rule);

    static double Number(JsonElement v, double min, double max, string rule)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d) || double.IsNaN(d) || d < min || d > max) throw new Fail(rule);
        return d;
    }

    static Location ParseLocation(JsonElement l, Trigger trigger)
    {
        const string rule = "action.location must be { \"type\": \"Point\", \"x\": number, \"z\": number, optional \"y\": number }, { \"type\": \"Admin\" } or { \"type\": \"AroundPlayer\", \"minDist\": 10-60, \"maxDist\": 15-80 }";
        if (l.ValueKind != JsonValueKind.Object) throw new Fail(rule);
        var type = l.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString() : null;
        switch (type)
        {
            case "Point":
                OnlyKeys(l, "action.location", "type", "x", "y", "z");
                float? y = l.TryGetProperty("y", out var yv) ? Coord(yv, "y") : null;   // absent: height 0 (A20)
                return new Location(LocationType.Point, Coord(Required(l, "x", rule), "x"), Coord(Required(l, "z", rule), "z"), y);
            case "Admin":
                OnlyKeys(l, "action.location", "type");
                if (trigger.Type != TriggerType.Manual) throw new Fail("action.location Admin needs a Manual trigger");
                return new Location(LocationType.Admin, 0, 0);
            case "AroundPlayer":
            {
                OnlyKeys(l, "action.location", "type", "minDist", "maxDist");
                var min = Int(Required(l, "minDist", MinDistRule), 10, 60, MinDistRule);
                var max = Int(Required(l, "maxDist", MaxDistRule), 15, 80, MaxDistRule);
                if (min >= max) throw new Fail(DistOrder);
                return new Location(LocationType.AroundPlayer, 0, 0, null, min, max);   // allowed with every trigger (D6)
            }
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
        var rule = $"{field} must be 0-5 lines of 1-200 characters, no angle brackets or control characters";
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
