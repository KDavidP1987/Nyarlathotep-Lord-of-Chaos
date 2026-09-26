using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The unit and faction catalogs of the tests. The factions are real names from Reference Data/unit_index.tsv,
/// deny-listed ones included, so the validator's deny list is what refuses them.</summary>
sealed class FakeUnits(params string[] known) : IUnitCatalog, IFactionCatalog
{
    public static readonly string[] DefaultFactions =
    [
        "Faction_Bandits", "Faction_Legion", "Faction_Undead", "Faction_Militia", "Faction_ChurchOfLum", "Faction_Gloomrot",
        "Faction_Cursed", "Faction_Blackfangs", "Faction_Players", "Faction_Players_Castle_Prisoners", "Faction_Players_Mutant",
        "Faction_Players_Shapeshift_Human", "Faction_Traders_T01", "Faction_Traders_T02", "Faction_Critters",
        "Faction_World_Prisoners", "Faction_Ignored",
    ];

    readonly HashSet<string> _known = new(known, StringComparer.Ordinal);
    public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Factions { get; } = new(DefaultFactions, StringComparer.Ordinal);

    public bool IsKnown(string prefabName) => _known.Contains(prefabName) || UnitDenyList.IsDenied(prefabName);
    public bool IsDenied(string prefabName) => Denied.Contains(prefabName);
    bool IFactionCatalog.IsKnown(string factionName) => Factions.Contains(factionName);

    public static FakeUnits Default() =>
        new("CHAR_Bandit_Thug", "CHAR_Bandit_Deadeye", "CHAR_Undead_SkeletonSoldier_Base", "CHAR_Bandit_Tourok_VBlood", "Boss_Known");
}

static class Json
{
    /// <summary>An events.json v1 file holding <paramref name="events"/>.</summary>
    public static string File(params string[] events) =>
        "{ \"SchemaVersion\": 1, \"events\": [ " + string.Join(", ", events) + " ] }";

    public const string ValidAction =
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ], " +
        "\"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 } }";

    /// <summary>An Empower action on <paramref name="factions"/> (JSON array text) with <paramref name="stats"/>.</summary>
    public static string EmpowerAction(string factions = "[\"Faction_Bandits\"]", string stats = "{ \"physicalPower\": 1.3 }", string? extra = null) =>
        "\"action\": { \"type\": \"Empower\", \"factions\": " + factions + (extra is null ? "" : ", " + extra) + ", \"stats\": " + stats + " }";

    /// <summary>A valid, enabled Empower event under pillar empowerment.</summary>
    public static string Empower(string id = "surge", string? trigger = null, string? action = null, string? extra = null) =>
        Event(id, trigger, extra, action ?? EmpowerAction()).Replace("\"pillar\": \"spawns\"", "\"pillar\": \"empowerment\"");

    public static string Event(string id = "raid", string? trigger = null, string? extra = null, string? action = null) =>
        "{ \"id\": \"" + id + "\", \"name\": \"Bandit raid\", \"enabled\": true, \"pillar\": \"spawns\", " +
        "\"trigger\": " + (trigger ?? "{ \"type\": \"Manual\" }") + ", \"durationSeconds\": 600, " +
        (action ?? ValidAction) + (extra is null ? "" : ", " + extra) + " }";

    /// <summary>A single event built from raw key/value text, for field-level cases.</summary>
    public static EventDefinition One(string eventJson)
    {
        var r = EventValidator.Parse(File(eventJson), FakeUnits.Default());
        Assert.Null(r.FileError);
        return Assert.Single(r.Set.All);
    }
}

static class Zones
{
    /// <summary>A fixed zone with US-style DST (UTC−5, +1 h from the second Sunday of March 02:00 to the first
    /// Sunday of November 02:00), independent of the machine's time zone data.</summary>
    public static readonly TimeZoneInfo UsEastLike = TimeZoneInfo.CreateCustomTimeZone(
        "Test/UsEastLike", TimeSpan.FromHours(-5), "UsEastLike", "UsEastLike Standard", "UsEastLike Daylight",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2000, 1, 1), new DateTime(2099, 12, 31), TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
        ]);

    public static DateTime Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);
}
