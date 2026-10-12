using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D15: `.nyar event info` shows the Region location, the region lock and RegionHunt beside the
/// existing scope line; every line fits 480 bytes; an event without the new keys shows 0.10.0's lines.</summary>
public partial class AuthoringTests
{
    static EventDefinition HgEvent(string location, string extra, string units = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ]")
    {
        var json = Json.Event("hg", action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": 3, " +
            "\"intervalSeconds\": 120, \"radius\": 10, \"location\": " + location + ", " + extra + " }");
        var r = EventValidator.Parse(Json.File(json), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    const string HgRegion = "{ \"type\": \"Region\" }";
    const string HgPoint = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";
    const string HgScope = "\"scope\": [\"FarbaneWoods\", \"CursedForest\"]";

    [Fact]
    public void HuntingGrounds_passes_location_lock_and_hunt_lines()
    {
        var lines = EventLines.Info(HgEvent(HgRegion, HgScope + ", \"regionLock\": true, \"behaviour\": { \"type\": \"RegionHunt\" }"), null, Now);
        Assert.Contains("action: 3 waves every 120s, radius 10, in the scope regions, units 4 CHAR_Bandit_Thug", lines);
        var at = lines.ToList().IndexOf("location: anywhere in the scope regions");
        Assert.True(at >= 0, string.Join(" | ", lines));
        Assert.Equal(["location: anywhere in the scope regions", "region lock: on",
                      "behaviour: hunts the nearest player in the regions, hidden jumps beyond 60 m",
                      "trigger scope: Global", "action scope: FarbaneWoods,CursedForest"], lines.Skip(at).Take(5));   // beside the scope line
        Assert.DoesNotContain(lines, l => l.Contains("hunt 0 m", StringComparison.Ordinal));

        var lockedPoint = EventLines.Info(HgEvent(HgPoint, HgScope + ", \"regionLock\": true, \"behaviour\": { \"type\": \"Hunt\", \"range\": 40 }"), null, Now);
        Assert.Contains("region lock: on", lockedPoint);
        Assert.Contains(lockedPoint, l => l.Contains("hunt 40 m", StringComparison.Ordinal));
        Assert.DoesNotContain(EventLines.RegionLocationLine, lockedPoint);
        Assert.DoesNotContain(EventLines.RegionHuntLine, lockedPoint);
        var regionOnly = EventLines.Info(HgEvent(HgRegion, HgScope), null, Now);
        Assert.Contains(EventLines.RegionLocationLine, regionOnly);
        Assert.DoesNotContain(EventLines.RegionLockLine, regionOnly);
    }

    [Fact]
    public void HuntingGrounds_fails_when_a_line_exceeds_480_bytes()
    {
        var all = "\"modifiers\": { \"levelDelta\": -5, \"maxHealth\": 2.75, \"power\": 2.75, \"moveSpeed\": 2.75, \"attackSpeed\": 2.75 }";
        var units = "[ " + string.Join(", ", Enumerable.Range(0, 10).Select(i => "{ \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"chance\": 0.55 }")) + " ]";
        var everyRegion = "\"scope\": [" + string.Join(", ", RegionNames.All.Select(n => $"\"{n}\"")) + "]";
        var d = HgEvent(HgRegion, all + ", " + everyRegion + ", \"regionLock\": true, \"behaviour\": { \"type\": \"RegionHunt\" }, \"unitLifetimeSeconds\": 7200", units);
        var lines = EventLines.Info(d, null, Now);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        Assert.Contains(EventLines.RegionLocationLine, lines);
        Assert.Contains(EventLines.RegionLockLine, lines);
        Assert.Contains(EventLines.RegionHuntLine, lines);
    }

    [Fact]
    public void HuntingGrounds_fails_when_a_keyless_events_info_changes()
    {
        var keyless = new[]
        {
            Json.One(Json.Event("raid")),
            HgEvent(HgPoint, HgScope),
            HgEvent("{ \"type\": \"Admin\" }", HgScope + ", \"regionLock\": false"),
            HgEvent("{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 30 }", "\"behaviour\": { \"type\": \"Hunt\", \"range\": 40 }"),
        };
        foreach (var d in keyless)
        {
            var lines = EventLines.Info(d, null, Now);
            Assert.DoesNotContain(lines, l => l.StartsWith("location:", StringComparison.Ordinal) || l.StartsWith("region lock", StringComparison.Ordinal)
                || l.StartsWith("behaviour:", StringComparison.Ordinal) || l.Contains("scope regions", StringComparison.Ordinal));
            Assert.Empty(EventLines.HuntingGroundsLines(d.Action!));
        }
        Assert.Contains("action: 3 waves every 60s, radius 10, at -1200.5 -800, units 5 CHAR_Bandit_Thug", EventLines.Info(keyless[0], null, Now));
        Assert.Contains("action: 3 waves every 120s, radius 10, at the admin, units 4 CHAR_Bandit_Thug", EventLines.Info(keyless[2], null, Now));
        Assert.Contains("action: 3 waves every 120s, radius 10, around a player 20-30 m, units 4 CHAR_Bandit_Thug", EventLines.Info(keyless[3], null, Now));
    }

    [Fact]
    public void HuntingGrounds_empty_no_hunting_grounds_keys()
    {
        var d = Json.One(Json.Event("raid"));
        Assert.Empty(EventLines.HuntingGroundsLines(d.Action!));
        Assert.False(d.Action!.RegionLock);
    }
}
