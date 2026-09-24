using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

sealed class FakeUnits(params string[] known) : IUnitCatalog
{
    readonly HashSet<string> _known = new(known, StringComparer.Ordinal);
    public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);

    public bool IsKnown(string prefabName) => _known.Contains(prefabName) || UnitDenyList.IsDenied(prefabName);
    public bool IsDenied(string prefabName) => Denied.Contains(prefabName);

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
