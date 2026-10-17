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
            // apocalypse D1: an Apocalypse runs 600-604800 s, every other kind keeps 30-7200 s; an Infestation and a
            // HordeMarch take the apocalypse's (undead-hordes D8, D10)
            var (minDuration, maxDuration, durationRule) = FamilyOf(e) is { } family
                ? (ApocalypseLimits.MinDurationSeconds, ApocalypseLimits.MaxDurationSeconds, family == ApocalypseType ? ApocalypseDurationRule : FamilyDurationRule(family))
                : (ApocalypseLimits.OtherMinDurationSeconds, ApocalypseLimits.OtherMaxDurationSeconds, DurationRule);
            var duration = Int(Required(e, "durationSeconds", durationRule), minDuration, maxDuration, durationRule);
            var (action, empower, apocalypse) = ParseAction(Required(e, "action", "action is required"), pillar, trigger, units, factions, regions,
                duration);
            RevengeAction(trigger, action);                              // revenge D2
            var announce = e.TryGetProperty("announce", out var a) ? ParseAnnounce(a) : Announce.None;
            return new EventDefinition(id, name.GetString()!, enabledEl.GetBoolean(), pillar, trigger, conditions, duration, action, announce,
                Empower: empower, Apocalypse: apocalypse);
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
    public const int MinHeat = 3;
    public const int MaxHeat = 500;
    public const int MinRevengeWindowSeconds = 60;
    public const int MaxWarnAt = 3;
    public const int MaxVBloodHeat = 100;
    public const int DefaultVBloodHeat = 10;
    public const string HeatRule = "trigger.heat must be 3-500";
    public const string RevengeWindowRule = "trigger.windowSeconds must be 60-3600";
    public const string WarnAtRule = "trigger.warnAt must be up to 3 ascending values below trigger.heat";
    public const string VBloodHeatRule = "trigger.vbloodHeat must be 0-100";
    public const string RevengeNeedsWaves = "a Revenge trigger needs a SpawnWaves action";
    public const string RevengeNeedsAroundPlayer = "a Revenge trigger needs location AroundPlayer";
    public const string RevengeNeedsHunt = "a Revenge trigger needs behaviour Hunt";
    public const string RevengeNoFanOut = "a Revenge trigger takes no fanOut";
    public const string RevengeNoTerritory = "a Revenge trigger never spawns in claimed territory";

    /// <summary>Every reason revenge D1 and D2 add, for the 120-byte check (the factions, cooldown, unknown-field and
    /// unknown-type reasons are the existing builders').</summary>
    public static readonly IReadOnlyList<string> RevengeReasons =
    [
        HeatRule, RevengeWindowRule, WarnAtRule, VBloodHeatRule, PlayerCooldownRule,
        RevengeNeedsWaves, RevengeNeedsAroundPlayer, RevengeNeedsHunt, RevengeNoFanOut, RevengeNoTerritory,
    ];

    /// <summary>`warnAt` (revenge D1): 0-3 integers, strictly ascending, each 1..heat−1.</summary>
    static List<int> WarnAtList(JsonElement v, int heat)
    {
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() > MaxWarnAt) throw new Fail(WarnAtRule);
        var list = new List<int>();
        foreach (var x in v.EnumerateArray())
        {
            var k = Int(x, 1, heat - 1, WarnAtRule);
            if (list.Count > 0 && k <= list[^1]) throw new Fail(WarnAtRule);
            list.Add(k);
        }
        return list;
    }

    /// <summary>The action rules of a Revenge trigger (revenge D2), checked after the action parsed: a SpawnWaves action,
    /// in either form, whose location is AroundPlayer and whose behaviour is Hunt, without fanOut or allowTerritory. Any
    /// other trigger type passes unchanged.</summary>
    static void RevengeAction(Trigger trigger, SpawnWavesAction? action)
    {
        if (trigger.Type != TriggerType.Revenge) return;
        if (action is null) throw new Fail(RevengeNeedsWaves);
        if (action.Location.Type != LocationType.AroundPlayer) throw new Fail(RevengeNeedsAroundPlayer);
        if (action.Behaviour is not { Type: BehaviourType.Hunt }) throw new Fail(RevengeNeedsHunt);
        if (action.FanOut is not null) throw new Fail(RevengeNoFanOut);
        if (action.AllowTerritory) throw new Fail(RevengeNoTerritory);
    }

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
            case "Revenge":
            {
                // revenge D1: per-player heat; `shared` is FactionKills' and is refused here as any other unknown key
                OnlyKeys(t, "trigger", "type", "factions", "heat", "windowSeconds", "warnAt", "vbloodHeat", "playerCooldownMinutes", "scope");
                var list = FactionNames(Required(t, "factions", TriggerFactionsRule), TriggerFactionsRule, factions);
                var heat = Int(Required(t, "heat", HeatRule), MinHeat, MaxHeat, HeatRule);
                var window = Int(Required(t, "windowSeconds", RevengeWindowRule), MinRevengeWindowSeconds, MaxKillWindowSeconds, RevengeWindowRule);
                var warnAt = t.TryGetProperty("warnAt", out var wa) ? WarnAtList(wa, heat) : [];
                var vblood = t.TryGetProperty("vbloodHeat", out var vb) ? Int(vb, 0, MaxVBloodHeat, VBloodHeatRule) : DefaultVBloodHeat;
                var cooldown = t.TryGetProperty("playerCooldownMinutes", out var pc)
                    ? Int(pc, 0, MaxPlayerCooldownMinutes, PlayerCooldownRule)
                    : DefaultPlayerCooldownMinutes;
                return new Trigger(TriggerType.Revenge, [], [], DayPhase.Night, [], PlayerCooldownMinutes: cooldown, Factions: list,
                    WindowSeconds: window, Heat: heat, WarnAt: warnAt, VBloodHeat: vblood);
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

    /// <summary>The action by its type, exactly one of the three set. The pairing rule (faction-empowerment D2) is checked
    /// on the type, before the action's own fields: pillar empowerment takes an Empower action and an Empower action
    /// needs pillar empowerment; an Apocalypse runs under the Spawns pillar switch (apocalypse S-6), so it needs pillar
    /// spawns.</summary>
    static (SpawnWavesAction? Waves, EmpowerAction? Empower, ApocalypseAction? Apocalypse) ParseAction(JsonElement a, Pillar pillar,
        Trigger trigger, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions, int durationSeconds)
    {
        if (a.ValueKind != JsonValueKind.Object) throw new Fail("action must be an object with a type");
        var type = a.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        switch (type)
        {
            case "SpawnWaves":
                if (pillar == Pillar.Empowerment) throw new Fail("pillar empowerment takes an Empower action");
                return (ParseSpawnWaves(a, trigger, units, regions), null, null);
            case "Empower":
                if (pillar != Pillar.Empowerment) throw new Fail("action Empower needs pillar empowerment");
                return (null, ParseEmpower(a, units, factions, regions), null);
            case ApocalypseType:
                if (pillar != Pillar.Spawns) throw new Fail(ApocalypsePillar);
                return (null, null, ParseApocalypse(a, trigger, units, factions, regions, durationSeconds));
            case InfestationType:
                if (pillar != Pillar.Spawns) throw new Fail(FamilyPillar(type));
                return (null, null, ParseInfestation(a, units, factions, regions));
            case HordeMarchType:
                if (pillar != Pillar.Spawns) throw new Fail(FamilyPillar(type));
                return (null, null, ParseHordeMarch(a, trigger, units, factions, regions));
            default:
                throw new Fail($"unknown action type{Shown(type)}");
        }
    }

    // ---------------------------------------------------------------- Apocalypse (apocalypse D1)

    public const string ApocalypseType = "Apocalypse";
    public const string DurationRule = "durationSeconds must be 30-7200";
    public const string ApocalypseDurationRule = "durationSeconds must be 600-604800 for an Apocalypse";
    public const string ApocalypsePillar = "action Apocalypse needs pillar spawns";
    public const string ApocalypseFactionRule = "action.faction must be a Faction_ name";
    /// <summary>A unit entry's count range: 1 to MaxUnitsPerWave's ceiling (undead-hordes D1).</summary>
    public static string CountRange => $"1-{Limits.MaxUnitsPerWave.Max}";
    public static string ApocalypseUnitsRule => $"action.units must be 1-10 entries {{ \"prefab\": CHAR_ name, \"count\": {CountRange} }}";
    public const string HotspotsObject = "action.hotspots must be an object { count, minNatives, minSpacing }";
    public const string HotspotsCountRule = "action.hotspots.count must be 1-5";
    public const string MinNativesRule = "action.hotspots.minNatives must be 3-50";
    public const string MinSpacingRule = "action.hotspots.minSpacing must be 100-3000";
    public const string RoamersObject = "action.roamers must be an object { perHotspot, perKill, max, perMinute, huntRange }";
    public const string KillDrawBoth = "action.roamers takes perKill or perKillMin and perKillMax, not both";
    public const string PerKillMinRule = "action.roamers.perKillMin must be 0-3";
    public const string PerKillMaxRule = "action.roamers.perKillMax must be perKillMin-3";
    public const string PerHotspotRule = "action.roamers.perHotspot must be 1-10";
    public const string PerKillRule = "action.roamers.perKill must be 0-2";
    public const string RoamersMaxRule = "action.roamers.max must be 1-500";
    public const string PerMinuteRule = "action.roamers.perMinute must be 1-30";
    public const string RoamersHuntRangeRule = "action.roamers.huntRange must be 20-60";
    public const string HordesObject = "action.hordes must be an object { count, firstAfterMinutes, everyMinutes, size, growth }";
    public const string HordesCountRule = "action.hordes.count must be 1-10";
    public const string FirstAfterRule = "action.hordes.firstAfterMinutes must be 0-1440";
    public const string EveryMinutesRule = "action.hordes.everyMinutes must be 5-1440";
    public const string HordeSizeRule = "action.hordes.size must be 5-500";
    public const string HordeGrowthRule = "action.hordes.growth must be 0-100";
    public const string LastHordeLate = "action.hordes: last horde rises at or after the duration";
    public const string GoalObject = "action.goal must be an object { location, radius, name }";
    public const string GoalLocationRule = "action.goal.location must be { \"type\": \"Point\", \"x\", \"z\", optional \"y\" } or { \"type\": \"Admin\" }";
    public const string GoalAdminManual = "action.goal.location Admin needs a Manual trigger";
    public const string GoalRadiusRule = "action.goal.radius must be 10-100";
    public const string GoalNameRule = "action.goal.name must be 1-32 characters, no angle brackets or control characters";
    public const string MarchSpeedRule = "action.marchSpeed must be a number 0.5-5.0";

    /// <summary>Every reason apocalypse D1 adds, for the 120-byte check (the unknown-field, unit, faction, scope, modifier
    /// and bosses reasons are the existing builders').</summary>
    public static readonly IReadOnlyList<string> ApocalypseReasons =
    [
        ApocalypseDurationRule, ApocalypsePillar, ApocalypseFactionRule, ApocalypseUnitsRule, HotspotsObject, HotspotsCountRule,
        MinNativesRule, MinSpacingRule, RoamersObject, PerHotspotRule, PerKillRule, KillDrawBoth, PerKillMinRule, PerKillMaxRule,
        RoamersMaxRule, PerMinuteRule, RoamersHuntRangeRule,
        HordesObject, HordesCountRule, FirstAfterRule, EveryMinutesRule, HordeSizeRule, HordeGrowthRule, LastHordeLate, GoalObject,
        GoalLocationRule, GoalAdminManual, GoalRadiusRule, GoalNameRule, MarchSpeedRule, GoalCoordRule("x"),
    ];

    public static string GoalCoordRule(string axis) => $"action.goal.location.{axis} must be a number within -10000..10000";

    /// <summary>The apocalypse-family type the event's action names (Apocalypse, Infestation, HordeMarch), or null; read
    /// before the duration so its bound applies.</summary>
    static string? FamilyOf(JsonElement e) =>
        e.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.Object && a.TryGetProperty("type", out var t)
        && t.ValueKind == JsonValueKind.String && IsFamily(t.GetString()) ? t.GetString() : null;

    /// <summary>True for the three types of the apocalypse family (undead-hordes D13).</summary>
    public static bool IsFamily(string? type) => type is ApocalypseType or InfestationType or HordeMarchType;

    // ---------------------------------------------------------------- Infestation and HordeMarch (undead-hordes D8, D10)

    public const string InfestationType = "Infestation";
    public const string HordeMarchType = "HordeMarch";
    public static string FamilyPillar(string type) => $"action {type} needs pillar spawns";
    public static string FamilyDurationRule(string type) => $"durationSeconds must be 600-604800 for {Article(type)}";
    static string Article(string type) => (type.StartsWith('I') ? "an " : "a ") + type;
    public const string OriginsObject = "action.origins must be an object { count, minNatives, minSpacing, perOrigin }";
    public const string OriginsCountRule = "action.origins.count must be 1-10";
    public const string OriginNativesRule = "action.origins.minNatives must be 1-20";
    public const string OriginSpacingRule = "action.origins.minSpacing must be 100-2000";
    public const string PerOriginRule = "action.origins.perOrigin must be 1-100";
    public const string InfestObject = "action.infest must be an object { hunters, driftRadius, spread }";
    public static string HuntersRule(string at) => $"{at}.hunters must be 0-100";
    public static string DriftRule(string at) => $"{at}.driftRadius must be 0-2000";
    public static string SpreadObject(string at) => $"{at} must be an object {{ perKillMin, perKillMax, max, perMinute }}";
    public static string SpreadKillMinRule(string at) => $"{at}.perKillMin must be 0-3";
    public static string SpreadKillMaxRule(string at) => $"{at}.perKillMax must be perKillMin-3";
    public static string SpreadMaxRule(string at) => $"{at}.max must be {HordeLimits.MinSpreadMax}-{HordeLimits.MaxSpreadMax}";
    public static string SpreadPerMinuteRule(string at) => $"{at}.perMinute must be {ApocalypseLimits.MinPerMinute}-{ApocalypseLimits.MaxPerMinute}";
    public static string HordeUnitsRule => $"action.units must total {HordeLimits.MinHorde}-{HordeLimits.MaxHorde} units for a HordeMarch";
    public const string StartRule = "action.start must be { \"type\": \"Point\", \"x\", \"z\" }, { \"type\": \"Admin\" } or { \"type\": \"Hotspot\", \"minDistance\" }";
    public const string StartAdminManual = "action.start Admin needs a Manual trigger";
    public const string StartDistanceRule = "action.start.minDistance must be 100-3000";
    public const string BothAdmin = "action.start and action.destination are not both Admin";
    public static string StartCoordRule(string axis) => $"action.start.{axis} must be a number within -10000..10000";

    /// <summary>Every reason undead-hordes D8 and D10 add, for the 120-byte check (the unit, pool, faction, scope, bosses
    /// and destination reasons are the existing builders').</summary>
    public static readonly IReadOnlyList<string> HordeReasons =
    [
        FamilyPillar(HordeMarchType), FamilyDurationRule(InfestationType), OriginsObject, OriginsCountRule, OriginNativesRule, OriginSpacingRule,
        PerOriginRule, InfestObject, HuntersRule("action.infest"), DriftRule("action.infest"), SpreadObject("action.infest.spread"),
        SpreadKillMinRule("action.infest.spread"), SpreadKillMaxRule("action.infest.spread"), SpreadMaxRule("action.infest.spread"),
        SpreadPerMinuteRule("action.infest.spread"), HordeUnitsRule, StartRule, StartAdminManual, StartDistanceRule, BothAdmin, StartCoordRule("x"),
    ];

    /// <summary>The faction, pools and units every apocalypse-family action opens with (apocalypse D1, undead-hordes D7).</summary>
    static (string Faction, IReadOnlyDictionary<string, IReadOnlyList<string>>? Pools, List<UnitEntry> Units) FamilyHead(JsonElement a, IUnitCatalog units,
        IFactionCatalog factions)
    {
        var faction = Str(Required(a, "faction", ApocalypseFactionRule), ApocalypseFactionRule);
        if (FactionDenyList.IsDenied(faction)) throw new Fail($"faction{Shown(faction)} is deny-listed");
        if (!faction.StartsWith("Faction_", StringComparison.Ordinal)) throw new Fail(ApocalypseFactionRule);
        if (!factions.IsKnown(faction)) throw new Fail($"unknown faction{Shown(faction)}");
        var pools = a.TryGetProperty("pools", out var po) ? ParsePools(po, units) : null;
        var list = ParseEntries(Required(a, "units", ApocalypseUnitsRule), "action.units", ApocalypseUnitsRule, units, withModifiers: true, pools, poolEntries: true);
        return (faction, pools, list);
    }

    /// <summary>A spread block at <paramref name="at"/> (undead-hordes D7): { perKillMin 0-3, perKillMax perKillMin-3, max
    /// 1 to MaxTrackedUnits' ceiling, perMinute 1-30, optional pool }, as roamers whose perHotspot is 0.</summary>
    static ApocalypseRoamers ParseSpread(JsonElement v, string at, IReadOnlyDictionary<string, IReadOnlyList<string>>? pools)
    {
        var s = Obj(v, SpreadObject(at));
        OnlyKeys(s, at, "perKillMin", "perKillMax", "pool", "max", "perMinute");
        var min = Int(Required(s, "perKillMin", SpreadKillMinRule(at)), ApocalypseLimits.MinKillDraw, ApocalypseLimits.MaxKillDraw, SpreadKillMinRule(at));
        var max = Int(Required(s, "perKillMax", SpreadKillMaxRule(at)), min, ApocalypseLimits.MaxKillDraw, SpreadKillMaxRule(at));
        var pool = s.TryGetProperty("pool", out var p) ? SpreadPool(p, at, pools) : null;
        return new ApocalypseRoamers(0, min,
            Int(Required(s, "max", SpreadMaxRule(at)), HordeLimits.MinSpreadMax, HordeLimits.MaxSpreadMax, SpreadMaxRule(at)),
            Int(Required(s, "perMinute", SpreadPerMinuteRule(at)), ApocalypseLimits.MinPerMinute, ApocalypseLimits.MaxPerMinute, SpreadPerMinuteRule(at)),
            HordeLimits.HuntRange, max == min ? null : max, pool);
    }

    /// <summary>The hunters share and drift radius at <paramref name="at"/> (undead-hordes D9).</summary>
    static InfestRule ParseInfestRule(JsonElement o, string at) => new(
        Int(Required(o, "hunters", HuntersRule(at)), HordeLimits.MinHunters, HordeLimits.MaxHunters, HuntersRule(at)),
        Int(Required(o, "driftRadius", DriftRule(at)), HordeLimits.MinDrift, HordeLimits.MaxDrift, DriftRule(at)));

    /// <summary>`"type": "Infestation"` (undead-hordes D8): faction, units (drawn by weight, as an apocalypse's), pools,
    /// origins, hunters, driftRadius and a spread block; scope and scoreboard as an apocalypse's.</summary>
    static ApocalypseAction ParseInfestation(JsonElement a, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        OnlyKeys(a, "action", "type", "faction", "units", "pools", "origins", "hunters", "driftRadius", "spread", "scope", "scoreboard");
        var (faction, pools, list) = FamilyHead(a, units, factions);
        var o = Obj(Required(a, "origins", OriginsObject), OriginsObject);
        OnlyKeys(o, "action.origins", "count", "minNatives", "minSpacing", "perOrigin");
        var origins = new ApocalypseHotspots(
            Int(Required(o, "count", OriginsCountRule), HordeLimits.MinOrigins, HordeLimits.MaxOrigins, OriginsCountRule),
            Int(Required(o, "minNatives", OriginNativesRule), HordeLimits.MinOriginNatives, HordeLimits.MaxOriginNatives, OriginNativesRule),
            Int(Required(o, "minSpacing", OriginSpacingRule), HordeLimits.MinOriginSpacing, HordeLimits.MaxOriginSpacing, OriginSpacingRule));
        var perOrigin = Int(Required(o, "perOrigin", PerOriginRule), HordeLimits.MinPerOrigin, HordeLimits.MaxPerOrigin, PerOriginRule);
        var infest = ParseInfestRule(a, "action");
        var spread = ParseSpread(Required(a, "spread", SpreadObject("action.spread")), "action.spread", pools) with { PerHotspot = perOrigin };
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        var scoreboard = !a.TryGetProperty("scoreboard", out var sb) || Bool(sb, ScoreboardRule);
        if (pools is not null) PoolsUsed(pools, spread.Pool is null ? list : [.. list, UnitEntry.FromPool(spread.Pool, 1)], null, null);
        return new ApocalypseAction(faction, list, origins, spread, new ApocalypseHordes(0, 0, ApocalypseLimits.MinEvery, 0, 0), ApocalypseGoal.None, 0,
            scope, scoreboard, null, pools, ApocalypseMode.Infestation, infest);
    }

    /// <summary>`"type": "HordeMarch"` (undead-hordes D10): faction, units whose counts total the horde (10 to
    /// MaxUnitsPerWave's ceiling), pools, bosses (fixed or rolled), start, destination, marchSpeed (default 2.0), a growth
    /// block and an infest block { hunters, driftRadius, spread }; an Admin start or destination only on a Manual trigger,
    /// never both.</summary>
    static ApocalypseAction ParseHordeMarch(JsonElement a, Trigger trigger, IUnitCatalog units, IFactionCatalog factions, IRegionCatalog regions)
    {
        OnlyKeys(a, "action", "type", "faction", "units", "pools", "bosses", "start", "destination", "marchSpeed", "growth", "infest", "scope", "scoreboard");
        var (faction, pools, list) = FamilyHead(a, units, factions);
        var total = list.Sum(u => (long)u.Count);
        if (total < HordeLimits.MinHorde || total > HordeLimits.MaxHorde) throw new Fail(HordeUnitsRule);
        var bosses = a.TryGetProperty("bosses", out var bo) ? ParseBossList(bo, "action.bosses", units, null, pools, waveLevel: false) : null;
        var start = ParseStart(Required(a, "start", StartRule), trigger);
        var destination = ParseDestination(Required(a, "destination", DestinationObject), trigger);
        if (start.Location?.Type == LocationType.Admin && destination.Location.Type == LocationType.Admin) throw new Fail(BothAdmin);
        var march = a.TryGetProperty("marchSpeed", out var ms)
            ? Number(ms, ApocalypseLimits.MinMarchSpeed, ApocalypseLimits.MaxMarchSpeed, MarchSpeedRule) : DestinationLimits.DefaultMarchSpeed;
        var growth = ParseSpread(Required(a, "growth", SpreadObject("action.growth")), "action.growth", pools);
        var i = Obj(Required(a, "infest", InfestObject), InfestObject);
        OnlyKeys(i, "action.infest", "hunters", "driftRadius", "spread");
        var infest = ParseInfestRule(i, "action.infest");
        var infestSpread = ParseSpread(Required(i, "spread", SpreadObject("action.infest.spread")), "action.infest.spread", pools);
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        var scoreboard = !a.TryGetProperty("scoreboard", out var sb) || Bool(sb, ScoreboardRule);
        if (pools is not null)
            PoolsUsed(pools, [.. list, .. new[] { growth.Pool, infestSpread.Pool }.OfType<string>().Select(p => UnitEntry.FromPool(p, 1))], null, bosses);
        return new ApocalypseAction(faction, list, new ApocalypseHotspots(1, HordeLimits.StartNatives, ApocalypseLimits.MinSpacing), growth,
            new ApocalypseHordes(1, 0, ApocalypseLimits.MinEvery, (int)total, 0), new ApocalypseGoal(destination.Location, destination.Radius, destination.Name),
            march, scope, scoreboard, null, pools, ApocalypseMode.HordeMarch, infest, new HordeMarchSpec(start, growth, infest, infestSpread, bosses));
    }

    /// <summary>`start` (undead-hordes D10): a Point (x, z, optional y), Admin (a Manual trigger only) or { "type":
    /// "Hotspot", "minDistance": 100-3000 }.</summary>
    static HordeStart ParseStart(JsonElement v, Trigger trigger)
    {
        var s = Obj(v, StartRule);
        if (s.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String && ty.GetString() == "Hotspot")
        {
            OnlyKeys(s, "action.start", "type", "minDistance");
            return new HordeStart(null, Int(Required(s, "minDistance", StartDistanceRule), HordeLimits.MinStartDistance, HordeLimits.MaxStartDistance, StartDistanceRule));
        }
        return new HordeStart(ParseTarget(s, "action.start", trigger, StartRule, StartAdminManual, StartCoordRule));
    }

    /// <summary>`"type": "Apocalypse"` (apocalypse D1): every key in its range, an unknown key named with its path, an Admin
    /// goal only with a Manual trigger, and no horde rising at or after the duration.</summary>
    static ApocalypseAction ParseApocalypse(JsonElement a, Trigger trigger, IUnitCatalog units, IFactionCatalog factions,
        IRegionCatalog regions, int durationSeconds)
    {
        OnlyKeys(a, "action", "type", "faction", "units", "hotspots", "roamers", "hordes", "goal", "marchSpeed", "bosses", "scope", "scoreboard", "pools");
        var faction = Str(Required(a, "faction", ApocalypseFactionRule), ApocalypseFactionRule);
        if (FactionDenyList.IsDenied(faction)) throw new Fail($"faction{Shown(faction)} is deny-listed");
        if (!faction.StartsWith("Faction_", StringComparison.Ordinal)) throw new Fail(ApocalypseFactionRule);
        if (!factions.IsKnown(faction)) throw new Fail($"unknown faction{Shown(faction)}");
        // undead-hordes D7: the SpawnWaves pool rules, pool entries in the units and a spread pool
        var pools = a.TryGetProperty("pools", out var po) ? ParsePools(po, units) : null;
        var list = ParseEntries(Required(a, "units", ApocalypseUnitsRule), "action.units", ApocalypseUnitsRule, units, withModifiers: true, pools, poolEntries: true);

        var h = Obj(Required(a, "hotspots", HotspotsObject), HotspotsObject);
        OnlyKeys(h, "action.hotspots", "count", "minNatives", "minSpacing");
        var hotspots = new ApocalypseHotspots(
            Int(Required(h, "count", HotspotsCountRule), ApocalypseLimits.MinHotspots, ApocalypseLimits.MaxHotspots, HotspotsCountRule),
            Int(Required(h, "minNatives", MinNativesRule), ApocalypseLimits.MinNatives, ApocalypseLimits.MaxNatives, MinNativesRule),
            Int(Required(h, "minSpacing", MinSpacingRule), ApocalypseLimits.MinSpacing, ApocalypseLimits.MaxSpacing, MinSpacingRule));

        var r = Obj(Required(a, "roamers", RoamersObject), RoamersObject);
        OnlyKeys(r, "action.roamers", "perHotspot", "perKill", "perKillMin", "perKillMax", "pool", "max", "perMinute", "huntRange");
        var (killMin, killMax) = KillDraw(r);
        var spreadPool = r.TryGetProperty("pool", out var sp) ? SpreadPool(sp, "action.roamers", pools) : null;
        var roamers = new ApocalypseRoamers(
            Int(Required(r, "perHotspot", PerHotspotRule), ApocalypseLimits.MinPerHotspot, ApocalypseLimits.MaxPerHotspot, PerHotspotRule),
            killMin,
            Int(Required(r, "max", RoamersMaxRule), ApocalypseLimits.MinRoamers, ApocalypseLimits.MaxRoamers, RoamersMaxRule),
            Int(Required(r, "perMinute", PerMinuteRule), ApocalypseLimits.MinPerMinute, ApocalypseLimits.MaxPerMinute, PerMinuteRule),
            Int(Required(r, "huntRange", RoamersHuntRangeRule), ApocalypseLimits.MinHuntRange, ApocalypseLimits.MaxHuntRange, RoamersHuntRangeRule),
            killMax == killMin ? null : killMax, spreadPool);

        var o = Obj(Required(a, "hordes", HordesObject), HordesObject);
        OnlyKeys(o, "action.hordes", "count", "firstAfterMinutes", "everyMinutes", "size", "growth");
        var hordes = new ApocalypseHordes(
            Int(Required(o, "count", HordesCountRule), ApocalypseLimits.MinHordes, ApocalypseLimits.MaxHordes, HordesCountRule),
            Int(Required(o, "firstAfterMinutes", FirstAfterRule), ApocalypseLimits.MinFirstAfter, ApocalypseLimits.MaxFirstAfter, FirstAfterRule),
            Int(Required(o, "everyMinutes", EveryMinutesRule), ApocalypseLimits.MinEvery, ApocalypseLimits.MaxEvery, EveryMinutesRule),
            Int(Required(o, "size", HordeSizeRule), ApocalypseLimits.MinSize, ApocalypseLimits.MaxSize, HordeSizeRule),
            Int(Required(o, "growth", HordeGrowthRule), ApocalypseLimits.MinGrowth, ApocalypseLimits.MaxGrowth, HordeGrowthRule));
        if (HordeSchedule.LastRiseSeconds(hordes) >= durationSeconds) throw new Fail(LastHordeLate);

        var goal = ParseGoal(Required(a, "goal", GoalObject), trigger);
        var march = Number(Required(a, "marchSpeed", MarchSpeedRule), ApocalypseLimits.MinMarchSpeed, ApocalypseLimits.MaxMarchSpeed, MarchSpeedRule);
        var bosses = a.TryGetProperty("bosses", out var bo) ? ParseBosses(bo, units, null, apocalypse: true) : null;
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        var scoreboard = !a.TryGetProperty("scoreboard", out var sb) || Bool(sb, ScoreboardRule);      // absent = true (D1)
        if (pools is not null) PoolsUsed(pools, spreadPool is null ? list : [.. list, UnitEntry.FromPool(spreadPool, 1)], null, null);
        return new ApocalypseAction(faction, list, hotspots, roamers, hordes, goal, march, scope, scoreboard, bosses, pools);
    }

    /// <summary>The draw per kill (undead-hordes D7): the 0.13.x `perKill` n (0-2) as n..n, or `perKillMin` 0-3 with
    /// `perKillMax` perKillMin-3; never both forms.</summary>
    static (int Min, int Max) KillDraw(JsonElement r)
    {
        var hasMin = r.TryGetProperty("perKillMin", out var lo);
        var hasMax = r.TryGetProperty("perKillMax", out var hi);
        if (r.TryGetProperty("perKill", out var one))
        {
            if (hasMin || hasMax) throw new Fail(KillDrawBoth);
            var n = Int(one, ApocalypseLimits.MinPerKill, ApocalypseLimits.MaxPerKill, PerKillRule);
            return (n, n);
        }
        if (!hasMin && !hasMax) throw new Fail(PerKillRule);
        if (!hasMin) throw new Fail(PerKillMinRule);
        if (!hasMax) throw new Fail(PerKillMaxRule);
        var min = Int(lo, ApocalypseLimits.MinKillDraw, ApocalypseLimits.MaxKillDraw, PerKillMinRule);
        return (min, Int(hi, min, ApocalypseLimits.MaxKillDraw, PerKillMaxRule));
    }

    /// <summary>A spread block's `pool` at <paramref name="at"/> (undead-hordes D7): the name of a pool the action defines.</summary>
    static string SpreadPool(JsonElement v, string at, IReadOnlyDictionary<string, IReadOnlyList<string>>? pools)
    {
        var name = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (name is null || pools is null || !pools.ContainsKey(name)) throw new Fail(NoPool(at, name));
        return name;
    }

    static JsonElement Obj(JsonElement v, string rule) => v.ValueKind == JsonValueKind.Object ? v : throw new Fail(rule);

    /// <summary>`goal` (apocalypse D1, S-10): a Point (x, z, optional y) or Admin location, Admin only with a Manual trigger,
    /// a radius of 10-100 m and a plain name of 1-32 characters.</summary>
    static ApocalypseGoal ParseGoal(JsonElement g, Trigger trigger)
    {
        Obj(g, GoalObject);
        OnlyKeys(g, "action.goal", "location", "radius", "name");
        var location = ParseTarget(Required(g, "location", GoalLocationRule), "action.goal.location", trigger, GoalLocationRule, GoalAdminManual, GoalCoordRule);
        var radius = Int(Required(g, "radius", GoalRadiusRule), ApocalypseLimits.MinGoalRadius, ApocalypseLimits.MaxGoalRadius, GoalRadiusRule);
        var name = Str(Required(g, "name", GoalNameRule), GoalNameRule);
        if (!IsPlainText(name, ApocalypseLimits.MaxGoalName)) throw new Fail(GoalNameRule);
        return new ApocalypseGoal(location, radius, name);
    }

    /// <summary>A goal's or a destination's location at <paramref name="at"/>: { "type": "Point", x, z, optional y } within
    /// ±10000, or { "type": "Admin" } on a Manual trigger only.</summary>
    static Location ParseTarget(JsonElement v, string at, Trigger trigger, string rule, string adminManual, Func<string, string> coordRule)
    {
        var l = Obj(v, rule);
        var type = l.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString() : null;
        switch (type)
        {
            case "Point":
                OnlyKeys(l, at, "type", "x", "y", "z");
                float? y = l.TryGetProperty("y", out var yv) ? TargetCoord(yv, coordRule("y")) : null;
                return new Location(LocationType.Point, TargetCoord(Required(l, "x", rule), coordRule("x")), TargetCoord(Required(l, "z", rule), coordRule("z")), y);
            case "Admin":
                OnlyKeys(l, at, "type");
                if (trigger.Type != TriggerType.Manual) throw new Fail(adminManual);
                return new Location(LocationType.Admin, 0, 0);
            default:
                throw new Fail(rule);
        }
    }

    static float TargetCoord(JsonElement v, string rule)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d) || double.IsNaN(d) || d < -10000 || d > 10000) throw new Fail(rule);
        return (float)d;
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
            "modifiers", "loot", "behaviour", "allowTerritory", "fanOut", "waveList", "scoreboard", "arena", "winner", "growth", "regionLock",
            "bosses", "pools", "pursuit", "destination", "marchSpeed");

        // wave-variety D1: the pools come first, so a unit entry or a boss roll can name one.
        var pools = a.TryGetProperty("pools", out var po) ? ParsePools(po, units) : null;

        IReadOnlyList<UnitEntry> list;
        IReadOnlyList<WaveSpec>? waveList = null;
        int waves, interval;
        SpawnModifiers? modifiers = null;
        if (a.TryGetProperty("waveList", out var wl))
        {
            // wave-sets D1: the waveList form replaces the units form's four keys.
            foreach (var key in UnitsFormKeys)
                if (a.TryGetProperty(key, out _)) throw new Fail(WaveListReplaces);
            waveList = ParseWaveList(wl, units, pools);
            list = waveList.SelectMany(w => w.Units).ToList();
            waves = waveList.Count;
            interval = 0;   // read only through WaveSchedule, which never reads it for a waveList (D6)
        }
        else
        {
            var unitsRule = $"action.units must be 1-10 entries {{ \"prefab\": CHAR_ name, \"count\": {CountRange} }}";
            list = ParseEntries(Required(a, "units", unitsRule), "action.units", unitsRule, units, withModifiers: false, pools, poolEntries: true);
            WaveSize(list, "action.units");
            waves = Int(Required(a, "waves", "action.waves must be 1-10"), 1, 10, "action.waves must be 1-10");
            interval = Int(Required(a, "intervalSeconds", "action.intervalSeconds must be 10-600"), 10, 600, "action.intervalSeconds must be 10-600");
            modifiers = a.TryGetProperty("modifiers", out var mo) ? ParseModifiers(mo) : null;
        }
        var radius = Int(Required(a, "radius", "action.radius must be 2-30"), 2, 30, "action.radius must be 2-30");
        var location = ParseLocation(Required(a, "location", "action.location is required"), trigger);
        int? lifetime = a.TryGetProperty("unitLifetimeSeconds", out var lt) ? Int(lt, 30, 7200, "action.unitLifetimeSeconds must be 30-7200") : null;
        var scope = a.TryGetProperty("scope", out var sc) ? ParseScope(sc, "action.scope", regions) : Scope.Global;
        // regions D6: a Point outside the scope is refused here; an Admin origin is checked at the start (ScopeGate).
        if (!scope.IsGlobal && location.Type == LocationType.Point && !scope.Names(regions.RegionOf(location.X, location.Z)))
            throw new Fail("action.location is outside action.scope");
        // hunting-grounds D1: a Region location spreads over the scope regions, so it needs some.
        if (location.Type == LocationType.Region && scope.IsGlobal) throw new Fail(RegionLocationScope);
        var loot = a.TryGetProperty("loot", out var lo) && Bool(lo, "action.loot must be true or false");
        var behaviour = a.TryGetProperty("behaviour", out var be) ? ParseBehaviour(be) : null;
        var allowTerritory = a.TryGetProperty("allowTerritory", out var at) && Bool(at, "action.allowTerritory must be true or false");
        if (behaviour is { Type: BehaviourType.Hunt } hunt && location.Type == LocationType.AroundPlayer && location.MaxDist > hunt.Range)
            throw new Fail(MaxDistOverRange);
        var fanOut = a.TryGetProperty("fanOut", out var fo) ? ParseFanOut(fo, location) : null;
        var scoreboard = a.TryGetProperty("scoreboard", out var sb) && Bool(sb, ScoreboardRule);
        var arena = a.TryGetProperty("arena", out var ar) ? ParseArena(ar, location, radius, behaviour) : null;
        var winner = a.TryGetProperty("winner", out var wi) ? ParseWinner(wi, scoreboard) : WinnerRule.None;
        var regionLock = a.TryGetProperty("regionLock", out var rl) && ParseRegionLock(rl, scope, arena);
        if (behaviour is { Type: BehaviourType.RegionHunt } && !regionLock) throw new Fail(RegionHuntLock);
        Growth? growth = null;
        if (a.TryGetProperty("growth", out var gr))
        {
            // swarm D24, D25: the units form's base wave grows into a generated list that everything after the parser reads.
            growth = ParseGrowth(gr, waveList is not null, list, waves);
            waveList = Growth.Generate(list, modifiers, waves, interval, growth);
        }
        var bosses = a.TryGetProperty("bosses", out var bo) ? ParseBossList(bo, "action.bosses", units, fanOut, pools, waveLevel: false) : null;
        WaveBosses(waveList, bosses, fanOut);
        var pursuit = a.TryGetProperty("pursuit", out var pu) ? ParsePursuit(pu, behaviour, fanOut, regionLock) : null;
        if (pools is not null) PoolsUsed(pools, list, waveList, bosses);
        // undead-hordes D5: a destination marches the waves from a Point or Admin start, none of them fanned out, locked,
        // in an arena or pursuing
        var destination = a.TryGetProperty("destination", out var de) ? ParseDestination(de, trigger) : null;
        var march = DestinationLimits.DefaultMarchSpeed;
        if (a.TryGetProperty("marchSpeed", out var ms))
        {
            if (destination is null) throw new Fail(MarchSpeedNeedsDestination);
            march = Number(ms, ApocalypseLimits.MinMarchSpeed, ApocalypseLimits.MaxMarchSpeed, MarchSpeedRule);
        }
        if (destination is not null)
        {
            if (location.Type is not (LocationType.Point or LocationType.Admin)) throw new Fail(DestinationStart);
            if (fanOut is not null) throw new Fail(DestinationTakesNo("fanOut"));
            if (regionLock) throw new Fail(DestinationTakesNo("regionLock"));
            if (arena is not null) throw new Fail(DestinationTakesNo("arena"));
            if (pursuit is not null) throw new Fail(DestinationTakesNo("pursuit"));
        }
        return new SpawnWavesAction(list, waves, interval, radius, location, lifetime, scope, modifiers, loot, behaviour, allowTerritory, fanOut,
            waveList, scoreboard, arena, winner, growth, regionLock, bosses, pools, pursuit, destination, march);
    }

    public const string DestinationObject = "action.destination must be an object { location, radius, name }";
    public const string DestinationLocationRule = "action.destination.location must be { \"type\": \"Point\", \"x\", \"z\", optional \"y\" } or { \"type\": \"Admin\" }";
    public const string DestinationAdminManual = "action.destination.location Admin needs a Manual trigger";
    public const string DestinationRadiusRule = "action.destination.radius must be 10-100";
    public const string DestinationNameRule = "action.destination.name must be 1-40 characters, no angle brackets or control characters";
    public const string DestinationStart = "action.destination needs an action.location of Point or Admin";
    public const string MarchSpeedNeedsDestination = "action.marchSpeed needs action.destination";

    /// <summary>"&lt;field&gt; needs action.destination": a destination's radius or name set before its location (undead-hordes D5).</summary>
    public static string NeedsDestination(string field) => $"{field} needs action.destination";
    public static string DestinationTakesNo(string key) => $"action.{key} takes no action.destination";
    public static string DestinationCoordRule(string axis) => $"action.destination.location.{axis} must be a number within -10000..10000";

    /// <summary>Every reason undead-hordes D5 adds, for the 120-byte check.</summary>
    public static readonly IReadOnlyList<string> DestinationReasons =
    [
        DestinationObject, DestinationLocationRule, DestinationAdminManual, DestinationRadiusRule, DestinationNameRule, DestinationStart,
        MarchSpeedNeedsDestination, DestinationTakesNo("regionLock"), DestinationCoordRule("x"),
    ];

    /// <summary>`destination` (undead-hordes D5): { "location": Point | Admin (a Manual trigger only), "radius": 10-100,
    /// "name": 1-40 plain characters }.</summary>
    static Destination ParseDestination(JsonElement g, Trigger trigger)
    {
        if (g.ValueKind != JsonValueKind.Object) throw new Fail(DestinationObject);
        OnlyKeys(g, "action.destination", "location", "radius", "name");
        var location = ParseTarget(Required(g, "location", DestinationLocationRule), "action.destination.location", trigger,
            DestinationLocationRule, DestinationAdminManual, DestinationCoordRule);
        var radius = Int(Required(g, "radius", DestinationRadiusRule), DestinationLimits.MinRadius, DestinationLimits.MaxRadius, DestinationRadiusRule);
        var name = Str(Required(g, "name", DestinationNameRule), DestinationNameRule);
        if (!IsPlainText(name, DestinationLimits.MaxName)) throw new Fail(DestinationNameRule);
        return new Destination(location, radius, name);
    }

    public const int MaxBosses = 3;
    public const int MaxBossCount = 3;
    public const string BossesRule = "action.bosses must hold 1-3 entries";
    public const string BossCountRule = "action.bosses count must be 1-3";
    public const string BossesFanOut = "action.bosses takes no fanOut";

    /// <summary>Every reason revenge D28 adds, for the 120-byte check (the unknown-field and unit-name reasons are the
    /// existing builders').</summary>
    public static readonly IReadOnlyList<string> BossReasons = [BossesRule, BossCountRule, BossesFanOut];

    /// <summary>`bosses` (revenge D28): 1-3 entries { "prefab", "count": 1-3, "modifiers"? }, a prefab checked as a units
    /// entry's and modifiers as a waveList entry's; never with fanOut, whose groups would each need their own boss.</summary>
    static IReadOnlyList<UnitEntry> ParseBosses(JsonElement b, IUnitCatalog units, FanOut? fanOut, bool apocalypse = false)
    {
        if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() is < 1 or > MaxBosses) throw new Fail(BossesRule);
        if (fanOut is not null) throw new Fail(BossesFanOut);
        var list = new List<UnitEntry>();
        var m = 0;
        foreach (var x in b.EnumerateArray())
        {
            m++;
            if (x.ValueKind != JsonValueKind.Object) throw new Fail(BossesRule);
            if (apocalypse && x.TryGetProperty("pool", out _)) throw new Fail(BossRollApocalypse);          // wave-variety D3
            OnlyKeys(x, "action.bosses", "prefab", "count", "modifiers");
            var prefab = Str(Required(x, "prefab", BossesRule), BossesRule);
            if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail($"unit{Shown(prefab)} is deny-listed");
            if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit{Shown(prefab)}");
            var count = Int(Required(x, "count", BossCountRule), 1, MaxBossCount, BossCountRule);
            var modifiers = x.TryGetProperty("modifiers", out var mo) ? ParseModifiers(mo, $"action.bosses.{m}.modifiers") : null;
            list.Add(new UnitEntry(prefab, count, 1.0, modifiers));
        }
        return list;
    }

    public const int MaxPools = 8;
    public const int MaxPoolMembers = 30;
    public const int MaxPoolName = 24;
    public const int MaxWaveBosses = 5;
    public const int MaxRollBosses = 5;
    public const int MinPursuitSeconds = 15;
    public const int MaxPursuitSeconds = 600;
    public const string PoolsObject = "action.pools must be an object";
    public const string PoolsCountRule = "action.pools must hold 1-8 pools";
    public const string BossRollApocalypse = "action.bosses: a pool roll needs a SpawnWaves action";
    public const string PursuitObject = "action.pursuit must be an object";
    public const string PursuitAfterRule = "action.pursuit.afterSeconds must be 15-600";
    public const string PursuitHunt = "action.pursuit needs behaviour Hunt";
    public const string PursuitFanOut = "action.pursuit takes no fanOut";
    public const string PursuitRegionLock = "action.pursuit takes no regionLock";
    static readonly Regex PoolNameRx = new("^[a-z0-9-]{1,24}$", RegexOptions.CultureInvariant);

    /// <summary>A pool name: 1-24 characters of a-z, 0-9 and "-" (wave-variety D1).</summary>
    public static bool IsPoolName(string? name) => name is not null && PoolNameRx.IsMatch(name);

    /// <summary>The wave-variety reasons (D1, D2, D3, D7), each naming its field; a builder's reason drops the echoed value
    /// when it would pass <see cref="Wire.ReasonBytes"/>.</summary>
    public static string PoolNameRule(string name) => Fit($"action.pools:{Shown(name)} is not a pool name", "action.pools: a name is not a pool name");
    public static string PoolMembersRule(string pool) => $"action.pools.{pool} must hold 1-30 distinct units";
    public static string PoolUnknownUnit(string pool, string prefab) => Fit($"action.pools.{pool}: unknown unit{Shown(prefab)}", $"action.pools.{pool}: unknown unit");
    public static string PoolDeniedUnit(string pool, string prefab) => Fit($"action.pools.{pool}: unit{Shown(prefab)} is deny-listed", $"action.pools.{pool}: a unit is deny-listed");
    public static string PoolUnused(string pool) => $"action.pools.{pool} is not used";
    public static string NoPool(string at, string? pool) => $"{at}.pool: no pool{(IsPoolName(pool) ? " " + pool : "")}";
    public static string PrefabOrPool(string at) => $"{at} needs prefab or pool, not both";
    public static string WaveSizeRule(string path) => $"{path} must hold at most {Limits.MaxUnitsPerWave.Max} units";
    public static string BossesRuleAt(string path) => $"{path} must hold 1-3 entries";
    public static string BossCountRuleAt(string path) => $"{path} count must be 1-3";
    public static string BossesMost(string path) => $"{path} must hold at most {MaxWaveBosses} bosses";
    public static string RollMinRule(string at) => $"{at}.min must be 1-{MaxRollBosses}";
    public static string RollMaxRule(string at) => $"{at}.max must be min-{MaxRollBosses}";
    public static string RollPastPool(string at, string pool) => $"{at}.max is larger than pool {pool}";
    public static string BossesBothLast(int n) => $"action.bosses and action.waveList.{n}.bosses both name the last wave";
    public static string WaveBossesFanOut(int n) => $"action.waveList.{n}.bosses takes no fanOut";

    /// <summary>The fixed wave-variety reasons, for the 120-byte check (the builders are checked at their longest).</summary>
    public static readonly IReadOnlyList<string> WaveVarietyReasons =
        [PoolsObject, PoolsCountRule, BossRollApocalypse, PursuitObject, PursuitAfterRule, PursuitHunt, PursuitFanOut, PursuitRegionLock];

    static string Fit(string reason, string shorter) =>
        System.Text.Encoding.UTF8.GetByteCount(reason) <= Wire.ReasonBytes ? reason : shorter;

    /// <summary>A unit entry's keys (wave-sets D1), and with a pool (wave-variety D2); the units form drops the last.</summary>
    static readonly string[] EntryKeys = ["prefab", "count", "chance", "modifiers"];
    static readonly string[] PoolEntryKeys = ["prefab", "count", "chance", "pool", "modifiers"];

    /// <summary>`pools` (wave-variety D1): an object of 1-8 pools, each name 1-24 of a-z, 0-9 and "-", each an array of
    /// 1-30 distinct known, not deny-listed CHAR_ names, kept in the file's order.</summary>
    static IReadOnlyDictionary<string, IReadOnlyList<string>> ParsePools(JsonElement p, IUnitCatalog units)
    {
        if (p.ValueKind != JsonValueKind.Object) throw new Fail(PoolsObject);
        if (p.EnumerateObject().Count() is < 1 or > MaxPools) throw new Fail(PoolsCountRule);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var pool in p.EnumerateObject())
        {
            if (!IsPoolName(pool.Name) || result.ContainsKey(pool.Name)) throw new Fail(PoolNameRule(pool.Name));
            var rule = PoolMembersRule(pool.Name);
            if (pool.Value.ValueKind != JsonValueKind.Array || pool.Value.GetArrayLength() is < 1 or > MaxPoolMembers) throw new Fail(rule);
            var members = new List<string>();
            foreach (var x in pool.Value.EnumerateArray())
            {
                var prefab = Str(x, rule);
                if (members.Contains(prefab)) throw new Fail(rule);
                if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail(PoolDeniedUnit(pool.Name, prefab));
                if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail(PoolUnknownUnit(pool.Name, prefab));
                members.Add(prefab);
            }
            result.Add(pool.Name, members);
        }
        return result;
    }

    /// <summary>The pool a unit entry or boss roll at <paramref name="at"/> names: exactly one of prefab and pool, and a
    /// pool the action defines (wave-variety D2).</summary>
    static string PoolOf(JsonElement x, string at, IReadOnlyDictionary<string, IReadOnlyList<string>>? pools)
    {
        if (x.TryGetProperty("prefab", out _) == x.TryGetProperty("pool", out var pv)) throw new Fail(PrefabOrPool(at));
        var name = pv.ValueKind == JsonValueKind.String ? pv.GetString() : null;
        if (name is null || pools is null || !pools.ContainsKey(name)) throw new Fail(NoPool(at, name));
        return name;
    }

    /// <summary>A wave holding a pool entry counts every entry against MaxUnitsPerWave's ceiling (wave-variety D2, undead-hordes D1); a
    /// wave of prefab entries only keeps 0.12.0's rules.</summary>
    static void WaveSize(IReadOnlyList<UnitEntry> entries, string path)
    {
        if (PastWaveSize(entries)) throw new Fail(WaveSizeRule(path));
    }

    /// <summary>Whether a wave's entries break <see cref="WaveSize"/>'s ceiling (the validator's and the chat field's test).</summary>
    public static bool PastWaveSize(IReadOnlyList<UnitEntry> entries) =>
        entries.Any(e => e.Pool is not null) && entries.Sum(e => e.Count) > Limits.MaxUnitsPerWave.Max;

    /// <summary>A bosses list at <paramref name="path"/> (wave-variety D3): 1-3 entries, each a fixed boss { prefab, count
    /// 1-3, modifiers? } or a roll { pool, min 1-5, max min-5, modifiers? }, at most 5 bosses in all (counts and maxima), a
    /// roll's max at most its pool's size less the list's fixed bosses from that pool and less the most each earlier roll
    /// can take from the members they share, so every roll always draws its count. The action-level list of fixed
    /// bosses only is revenge D28's, parsed by <see cref="ParseBosses"/> exactly as 0.12.0 does.</summary>
    static IReadOnlyList<BossRoll> ParseBossList(JsonElement b, string path, IUnitCatalog units, FanOut? fanOut,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? pools, bool waveLevel)
    {
        var anyRoll = b.ValueKind == JsonValueKind.Array && b.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("pool", out _));
        if (!waveLevel && !anyRoll) return ParseBosses(b, units, fanOut).Select(e => (BossRoll)e).ToList();
        if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() is < 1 or > MaxBosses) throw new Fail(BossesRuleAt(path));
        if (fanOut is not null) throw new Fail(BossesFanOut);
        var list = new List<BossRoll>();
        var k = 0;
        foreach (var x in b.EnumerateArray())
        {
            k++;
            var at = $"{path}.{k}";
            if (x.ValueKind != JsonValueKind.Object) throw new Fail(BossesRuleAt(path));
            SpawnModifiers? Modifiers() => x.TryGetProperty("modifiers", out var mo) ? ParseModifiers(mo, $"{at}.modifiers") : null;
            if (x.TryGetProperty("pool", out _) || !x.TryGetProperty("prefab", out _))
            {
                var pool = PoolOf(x, at, pools);
                OnlyKeys(x, path, "pool", "min", "max", "modifiers");
                var min = Int(Required(x, "min", RollMinRule(at)), 1, MaxRollBosses, RollMinRule(at));
                var max = Int(Required(x, "max", RollMaxRule(at)), min, MaxRollBosses, RollMaxRule(at));
                list.Add(BossRoll.Roll(pool, min, max, Modifiers()));
                continue;
            }
            OnlyKeys(x, path, "prefab", "count", "modifiers");
            var prefab = Str(x.GetProperty("prefab"), BossesRuleAt(path));
            if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail($"unit{Shown(prefab)} is deny-listed");
            if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit{Shown(prefab)}");
            var count = Int(Required(x, "count", BossCountRuleAt(path)), 1, MaxBossCount, BossCountRuleAt(path));
            list.Add(BossRoll.Fixed(prefab, count, Modifiers()));
        }
        if (list.Sum(e => e.MostBosses) > MaxWaveBosses) throw new Fail(BossesMost(path));
        var fixedBosses = list.Where(e => !e.IsRoll).Select(e => e.Prefab).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is not { Pool: { } name } roll) continue;
            var free = pools![name].Where(p => !fixedBosses.Contains(p)).ToHashSet(StringComparer.Ordinal);
            // The rolls draw in list order (Pools.Resolve), so the earlier rolls may take some of this one's members
            // (Codex wave-variety F1): what is left must still cover this roll's max, at the worst the earlier draws allow.
            var taken = MostTaken(list.Take(i).Where(e => e.IsRoll).Select(e => (e.Max, pools[e.Pool!])), free);
            if (roll.Max > free.Count - taken) throw new Fail(RollPastPool($"{path}.{i + 1}", name));
        }
        return list;
    }

    /// <summary>The most of <paramref name="members"/> that <paramref name="rolls"/>, each drawing up to Max distinct
    /// prefabs of its pool, can take together: a maximum matching of the rolls' draws to the members they share (a member
    /// shared by two earlier pools is taken once). At most 3 rolls of 5 draws over 30 members.</summary>
    static int MostTaken(IEnumerable<(int Max, IReadOnlyList<string> Pool)> rolls, IReadOnlySet<string> members)
    {
        var draws = rolls.SelectMany(r => Enumerable.Repeat(r.Pool.Where(members.Contains).ToList(), r.Max)).ToList();
        var holder = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Place(int d, HashSet<string> seen)
        {
            foreach (var m in draws[d])
            {
                if (!seen.Add(m)) continue;
                if (!holder.TryGetValue(m, out var other) || Place(other, seen))
                {
                    holder[m] = d;
                    return true;
                }
            }
            return false;
        }
        var taken = 0;
        for (var d = 0; d < draws.Count; d++)
            if (Place(d, [])) taken++;
        return taken;
    }

    /// <summary>The wave-level bosses' action rules (wave-variety D3): never beside fanOut, and the last wave's own list
    /// never beside the action-level one.</summary>
    static void WaveBosses(IReadOnlyList<WaveSpec>? waveList, IReadOnlyList<BossRoll>? bosses, FanOut? fanOut)
    {
        if (waveList is null) return;
        for (var n = 1; n <= waveList.Count; n++)
            if (waveList[n - 1].Bosses is not null && fanOut is not null) throw new Fail(WaveBossesFanOut(n));
        if (bosses is not null && waveList[^1].Bosses is not null) throw new Fail(BossesBothLast(waveList.Count));
    }

    /// <summary>Every pool is named by a unit entry or a boss roll (wave-variety D1), checked in the file's order.</summary>
    static void PoolsUsed(IReadOnlyDictionary<string, IReadOnlyList<string>> pools, IReadOnlyList<UnitEntry> units,
        IReadOnlyList<WaveSpec>? waveList, IReadOnlyList<BossRoll>? bosses)
    {
        var used = new HashSet<string>(units.Select(u => u.Pool).OfType<string>(), StringComparer.Ordinal);
        foreach (var w in waveList ?? [])
            used.UnionWith((w.Bosses ?? []).Select(b => b.Pool).OfType<string>());
        used.UnionWith((bosses ?? []).Select(b => b.Pool).OfType<string>());
        foreach (var name in pools.Keys)
            if (!used.Contains(name)) throw new Fail(PoolUnused(name));
    }

    /// <summary>`pursuit` (wave-variety D7): { "afterSeconds": 15-600 } on a Hunt action without fanOut or regionLock.</summary>
    static Pursuit ParsePursuit(JsonElement v, Behaviour? behaviour, FanOut? fanOut, bool regionLock)
    {
        if (v.ValueKind != JsonValueKind.Object) throw new Fail(PursuitObject);
        OnlyKeys(v, "action.pursuit", "afterSeconds");
        var after = Int(Required(v, "afterSeconds", PursuitAfterRule), MinPursuitSeconds, MaxPursuitSeconds, PursuitAfterRule);
        if (behaviour is not { Type: BehaviourType.Hunt }) throw new Fail(PursuitHunt);
        if (fanOut is not null) throw new Fail(PursuitFanOut);
        if (regionLock) throw new Fail(PursuitRegionLock);
        return new Pursuit(after);
    }

    public const string RegionLocationScope = "action.location Region needs action.scope regions";
    public const string RegionLockRule = "action.regionLock must be true or false";
    public const string RegionLockScope = "action.regionLock needs action.scope regions";
    public const string RegionLockArena = "action.regionLock and action.arena cannot both be set";
    public const string RegionHuntLock = "action.behaviour RegionHunt needs action.regionLock true";

    /// <summary>Every reason hunting-grounds D1, D2 and D3 add, for the 120-byte check (the unknown-field and
    /// unknown-type reasons are the existing builders').</summary>
    public static readonly IReadOnlyList<string> HuntingGroundsReasons =
        [RegionLocationScope, RegionLockRule, RegionLockScope, RegionLockArena, RegionHuntLock];

    /// <summary>`regionLock` (hunting-grounds D2): a boolean, absent meaning false; true needs a regional scope and no
    /// arena (swarm's circle and the region lock never combine, Business rules 5).</summary>
    static bool ParseRegionLock(JsonElement v, Scope scope, Arena? arena)
    {
        var locked = Bool(v, RegionLockRule);
        if (!locked) return false;
        if (scope.IsGlobal) throw new Fail(RegionLockScope);
        if (arena is not null) throw new Fail(RegionLockArena);
        return true;
    }

    public const string ArenaObject = "action.arena must be an object";
    public const string ArenaRadiusRule = "action.arena.radius must be 15-60";
    public const string ArenaLocation = "action.arena needs a Point location or an Admin location";
    public const string ArenaSpawnRadius = "action.radius must be at most action.arena.radius";
    public const string ArenaHuntRange = "action.behaviour.range must be at most action.arena.radius";
    public const string WinnerValue = "action.winner must be KillDeath";
    public const string WinnerScoreboard = "action.winner needs action.scoreboard true";
    public const int MaxGrowthUnits = 10;
    public const int MaxGrowthLevels = 5;
    public const string GrowthObject = "action.growth must be an object";
    public const string GrowthUnitsForm = "action.growth needs the units form, not action.waveList";
    public const string GrowthWaves = "action.growth needs action.waves 2-10";
    public const string GrowthAdds = "action.growth must add units or levels";
    public const string GrowthAddUnitsRule = "action.growth.addUnits must be 0-10";
    public const string GrowthAddLevelsRule = "action.growth.addLevels must be 0-5";
    public const string GrowthWhenClearedRule = "action.growth.whenCleared must be true or false";
    public static string GrowthLastWave => $"action.growth grows the last wave past {Limits.MaxUnitsPerWave.Max} units";

    /// <summary>Every reason swarm D1, D2 and D24 give, for the 120-byte check.</summary>
    public static readonly IReadOnlyList<string> SwarmReasons =
    [
        ArenaObject, ArenaRadiusRule, ArenaLocation, ArenaSpawnRadius, ArenaHuntRange, WinnerValue, WinnerScoreboard, GrowthObject,
        GrowthUnitsForm, GrowthWaves, GrowthAdds, GrowthAddUnitsRule, GrowthAddLevelsRule, GrowthWhenClearedRule, GrowthLastWave,
    ];

    /// <summary>`arena` (swarm D1): { "radius": 15-60 } with a Point or Admin location, the spawn radius and a Hunt range
    /// within it.</summary>
    static Arena ParseArena(JsonElement v, Location location, int radius, Behaviour? behaviour)
    {
        if (v.ValueKind != JsonValueKind.Object) throw new Fail(ArenaObject);
        OnlyKeys(v, "action.arena", "radius");
        var arena = new Arena(Int(Required(v, "radius", ArenaRadiusRule), Arena.MinRadius, Arena.MaxRadius, ArenaRadiusRule));
        if (location.Type is not (LocationType.Point or LocationType.Admin)) throw new Fail(ArenaLocation);
        if (radius > arena.Radius) throw new Fail(ArenaSpawnRadius);
        if (behaviour is { Type: BehaviourType.Hunt } hunt && hunt.Range > arena.Radius) throw new Fail(ArenaHuntRange);
        return arena;
    }

    /// <summary>`winner` (swarm D2): "KillDeath", spelled as the other enum values of events.json are, on an action with
    /// `scoreboard: true`.</summary>
    static WinnerRule ParseWinner(JsonElement v, bool scoreboard)
    {
        if (v.ValueKind != JsonValueKind.String || v.GetString() != nameof(WinnerRule.KillDeath)) throw new Fail(WinnerValue);
        if (!scoreboard) throw new Fail(WinnerScoreboard);
        return WinnerRule.KillDeath;
    }

    /// <summary>`growth` (swarm D24): { "addUnits": 0-10, "addLevels": 0-5, "whenCleared": bool } (absent 0, 0, false) on a
    /// units-form action of 2-10 waves, adding units or levels, its last wave at most Limits.MaxUnitsPerWave's ceiling.</summary>
    static Growth ParseGrowth(JsonElement v, bool waveList, IReadOnlyList<UnitEntry> baseWave, int waves)
    {
        if (v.ValueKind != JsonValueKind.Object) throw new Fail(GrowthObject);
        if (waveList) throw new Fail(GrowthUnitsForm);
        OnlyKeys(v, "action.growth", "addUnits", "addLevels", "whenCleared");
        var units = v.TryGetProperty("addUnits", out var au) ? Int(au, 0, MaxGrowthUnits, GrowthAddUnitsRule) : 0;
        var levels = v.TryGetProperty("addLevels", out var al) ? Int(al, 0, MaxGrowthLevels, GrowthAddLevelsRule) : 0;
        var cleared = v.TryGetProperty("whenCleared", out var wc) && Bool(wc, GrowthWhenClearedRule);
        if (waves < 2) throw new Fail(GrowthWaves);
        if (units == 0 && levels == 0) throw new Fail(GrowthAdds);
        if (Growth.LastWaveUnits(baseWave, waves, units) > Limits.MaxUnitsPerWave.Max) throw new Fail(GrowthLastWave);
        return new Growth(units, levels, cleared);
    }

    /// <summary>The units form's keys a waveList replaces (wave-sets D1).</summary>
    public static readonly IReadOnlyList<string> UnitsFormKeys = ["units", "waves", "intervalSeconds", "modifiers"];
    public const int MaxWaveListWaves = 10;
    public const int MinAfterSeconds = 10;
    public const int MaxAfterSeconds = 3600;
    public const string WaveListReplaces = "action.waveList replaces action.units, waves, intervalSeconds and modifiers";
    public const string WaveListRule = "action.waveList needs 1-10 waves";
    public const string ScoreboardRule = "action.scoreboard must be true or false";
    public const string FirstWaveStart = "action.waveList.1 starts with the event";
    public static string WaveRule(int n) => $"action.waveList.{n} must be an object {{ units, afterSeconds, whenCleared }}";
    public static string AfterSecondsRule(int n) => $"action.waveList.{n}.afterSeconds must be {MinAfterSeconds}-{MaxAfterSeconds}";
    public static string WhenClearedRule(int n) => $"action.waveList.{n}.whenCleared must be true or false";
    public static string NoStartRule(int n) => $"action.waveList.{n} with whenCleared false needs afterSeconds";

    /// <summary>`waveList` (wave-sets D1): 1-10 waves of { units, afterSeconds 10-3600, whenCleared }, wave 1 without a
    /// start key, each unit entry with an optional `modifiers` object under ParseModifiers' rules.</summary>
    static IReadOnlyList<WaveSpec> ParseWaveList(JsonElement wl, IUnitCatalog units, IReadOnlyDictionary<string, IReadOnlyList<string>>? pools)
    {
        if (wl.ValueKind != JsonValueKind.Array || wl.GetArrayLength() is < 1 or > MaxWaveListWaves) throw new Fail(WaveListRule);
        var result = new List<WaveSpec>();
        var n = 0;
        foreach (var w in wl.EnumerateArray())
        {
            n++;
            var path = $"action.waveList.{n}";
            if (w.ValueKind != JsonValueKind.Object) throw new Fail(WaveRule(n));
            OnlyKeys(w, path, "units", "afterSeconds", "whenCleared", "bosses");
            var unitsRule = $"{path}.units must be 1-10 entries {{ \"prefab\": CHAR_ name, \"count\": {CountRange} }}";
            var entries = ParseEntries(Required(w, "units", unitsRule), $"{path}.units", unitsRule, units, withModifiers: true, pools, poolEntries: true);
            WaveSize(entries, $"{path}.units");
            int? after = w.TryGetProperty("afterSeconds", out var af) ? Int(af, MinAfterSeconds, MaxAfterSeconds, AfterSecondsRule(n)) : null;
            bool? cleared = w.TryGetProperty("whenCleared", out var wc) ? Bool(wc, WhenClearedRule(n)) : null;
            if (n == 1 && (after is not null || cleared is not null)) throw new Fail(FirstWaveStart);
            if (cleared == false && after is null) throw new Fail(NoStartRule(n));
            // wave-variety D3: a wave's own bosses, fixed or rolled, under the 5-boss limit.
            var bosses = w.TryGetProperty("bosses", out var bo) ? ParseBossList(bo, $"{path}.bosses", units, null, pools, waveLevel: true) : null;
            result.Add(new WaveSpec(entries, after, cleared ?? false, bosses));
        }
        return result;
    }

    /// <summary>1-10 unit entries { prefab, count 1 to MaxUnitsPerWave's ceiling, chance 0.05-1.0 } at <paramref name="path"/>, and with
    /// <paramref name="withModifiers"/> an optional per-entry `modifiers` (wave-sets D1). With <paramref name="poolEntries"/>
    /// (a SpawnWaves action, wave-variety D2) an entry may name a `pool` of <paramref name="pools"/> instead of a prefab,
    /// under the same count, chance and modifier rules; an entry without the key reads as 0.12.0's.</summary>
    static List<UnitEntry> ParseEntries(JsonElement u, string path, string rule, IUnitCatalog units, bool withModifiers,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? pools = null, bool poolEntries = false)
    {
        if (u.ValueKind != JsonValueKind.Array || u.GetArrayLength() is < 1 or > 10) throw new Fail(rule);
        var list = new List<UnitEntry>();
        var m = 0;
        foreach (var x in u.EnumerateArray())
        {
            m++;
            if (x.ValueKind != JsonValueKind.Object) throw new Fail(rule);
            var keys = withModifiers ? (poolEntries ? PoolEntryKeys : EntryKeys) : poolEntries ? PoolEntryKeys[..^1] : EntryKeys[..^1];
            if (withModifiers) OnlyKeys(x, $"{path}.{m}", keys);
            else OnlyKeys(x, path, keys);
            var at = withModifiers ? $"{path}.{m}" : path;                                    // a wave's reason names the entry (review F6)
            string? pool = null, prefab = null;
            if (poolEntries && (x.TryGetProperty("pool", out _) || (pools is not null && !x.TryGetProperty("prefab", out _))))
                pool = PoolOf(x, at, pools);                                                  // wave-variety D2
            else
            {
                prefab = Str(Required(x, "prefab", rule), rule);
                if (UnitDenyList.IsDenied(prefab) || units.IsDenied(prefab)) throw new Fail($"unit{Shown(prefab)} is deny-listed");
                if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !units.IsKnown(prefab)) throw new Fail($"unknown unit{Shown(prefab)}");
            }
            var count = Int(Required(x, "count", rule), 1, Limits.MaxUnitsPerWave.Max, $"{at}.count must be {CountRange}");
            var chance = x.TryGetProperty("chance", out var ch) ? Number(ch, MinChance, 1.0, ChanceRule.Replace("action.units", at)) : 1.0;
            var modifiers = withModifiers && x.TryGetProperty("modifiers", out var mo) ? ParseModifiers(mo, $"{path}.{m}.modifiers") : null;
            list.Add(pool is null ? new UnitEntry(prefab!, count, chance, modifiers) : UnitEntry.FromPool(pool, count, chance, modifiers));
        }
        return list;
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
    static SpawnModifiers ParseModifiers(JsonElement m, string path = "action.modifiers")
    {
        // A per-unit modifiers of a waveList (wave-sets D1) names its own path in every reason.
        string At(string rule) => rule.Replace("action.modifiers", path);
        if (m.ValueKind != JsonValueKind.Object) throw new Fail(At("action.modifiers must be an object"));
        var r = new SpawnModifiers();
        var any = false;
        foreach (var p in m.EnumerateObject())
        {
            any = true;
            r = p.Name switch
            {
                "level" => r with { Level = Int(p.Value, 1, 120, At(LevelRule)) },
                "levelDelta" => r with { LevelDelta = Int(p.Value, -MaxLevelDelta, MaxLevelDelta, At(LevelDeltaRule)) },
                "maxHealth" => r with { MaxHealth = Multiplier(p.Value, p.Name, path) },
                "power" => r with { Power = Multiplier(p.Value, p.Name, path) },
                "moveSpeed" => r with { MoveSpeed = Multiplier(p.Value, p.Name, path) },
                "attackSpeed" => r with { AttackSpeed = Multiplier(p.Value, p.Name, path) },
                _ => throw new Fail(UnknownField(path, p.Name)),
            };
        }
        if (!any) throw new Fail(At(EmptyModifiers));
        if (r.Level is not null && r.LevelDelta is not null) throw new Fail(At(BothLevels));
        return r;
    }

    /// <summary>A multiplier of `modifiers`: a JSON number 0.5-3.0 with at most two decimals (S-8).</summary>
    static double Multiplier(JsonElement v, string key, string path)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var d) || d < (decimal)MinModifier || d > (decimal)MaxModifier
            || decimal.Round(d, 2) != d)
            throw new Fail(ModifierRule(key).Replace("action.modifiers", path));
        return (double)d;
    }

    /// <summary>`behaviour` (D6): { "type": "Hunt", "range": 10-60 } or { "type": "RegionHunt" } (hunting-grounds D3, its
    /// lock checked by the caller); any other type, Guard and Ambush included until child spawn-extras, is unknown.</summary>
    static Behaviour ParseBehaviour(JsonElement b)
    {
        if (b.ValueKind != JsonValueKind.Object) throw new Fail("action.behaviour must be { \"type\": \"Hunt\", \"range\": 10-60 }");
        var type = b.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString()! : "";
        if (type == nameof(BehaviourType.RegionHunt))
        {
            OnlyKeys(b, "action.behaviour", "type");
            return new Behaviour(BehaviourType.RegionHunt, 0);
        }
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

    /// <summary>The location rule, naming the four types (hunting-grounds D1).</summary>
    public const string LocationRule = "action.location must be { \"type\": \"Point\", \"x\": number, \"z\": number, optional \"y\": number }, { \"type\": \"Admin\" }, { \"type\": \"AroundPlayer\", \"minDist\": 10-60, \"maxDist\": 15-80 } or { \"type\": \"Region\" }";

    static Location ParseLocation(JsonElement l, Trigger trigger)
    {
        const string rule = LocationRule;
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
            case "Region":
                OnlyKeys(l, "action.location", "type");                 // hunting-grounds D1: its scope is checked by the caller
                return new Location(LocationType.Region, 0, 0);
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
