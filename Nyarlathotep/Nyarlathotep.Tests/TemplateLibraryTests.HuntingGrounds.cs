using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D16: spider-hunting-grounds, disabled and valid: a Region location over a regional scope, the
/// region lock and RegionHunt, the scoreboard on; the units form on a base wave of Faction_Spiders units only (no V Blood,
/// no `_Summon`, no Baneling), no wave over the default MaxUnitsPerWave of 20; fourteen templates in all since apocalypse D18. The shape is
/// read, not the numbers or the region (S-13).</summary>
public partial class TemplateLibraryTests
{
    static List<string> HuntingGroundsDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != TemplatesNow) diffs.Add($"{c.Templates.Count} templates, not {TemplatesNow}");
        var t = c.Find("spider-hunting-grounds");
        if (t is null) { diffs.Add("spider-hunting-grounds missing"); return diffs; }
        if (t.Invalid is { } why) diffs.Add($"invalid: {why}");
        var d = t.Definition;
        if (d.Enabled) diffs.Add("enabled");
        if (d.Action is not { } a) { diffs.Add("no wave action"); return diffs; }
        var waves = a.WaveList?.Select(w => w.Units).ToList() ?? [a.Units];
        foreach (var u in waves.SelectMany(w => w))
        {
            if (!SpiderFaction.Contains(u.Prefab)) diffs.Add($"{u.Prefab} is not a Faction_Spiders unit");
            if (u.Prefab.Contains("VBlood", StringComparison.OrdinalIgnoreCase) || u.Prefab.Contains("_Summon", StringComparison.Ordinal)
                || u.Prefab.Contains("Baneling", StringComparison.Ordinal))
                diffs.Add($"{u.Prefab} is a V Blood, Summon or Baneling prefab");
        }
        if (a.Location.Type != LocationType.Region) diffs.Add("not a Region location");
        if (a.Scope.IsGlobal) diffs.Add("no regional scope");
        if (!a.RegionLock) diffs.Add("no region lock");
        if (a.Behaviour is not { Type: BehaviourType.RegionHunt }) diffs.Add("no RegionHunt");
        if (!a.Scoreboard) diffs.Add("no scoreboard");
        if (waves.Any(w => w.Sum(u => u.Count) > 20)) diffs.Add("a wave exceeds the default MaxUnitsPerWave of 20");
        return diffs;
    }

    [Fact]
    public void HuntingGrounds_passes_spider_hunting_grounds_as_planned()
    {
        var c = Real();
        Assert.Empty(HuntingGroundsDifferences(c));
        var d = c.Find("spider-hunting-grounds")!.Definition;
        Assert.Equal("Spider hunting grounds", d.Name);
        Assert.Equal(1800, d.DurationSeconds);
        Assert.Equal(Pillar.Spawns, d.Pillar);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("invalid")]
    [InlineData("another faction")]
    [InlineData("vblood")]
    [InlineData("summon")]
    [InlineData("baneling")]
    [InlineData("no region location")]
    [InlineData("no lock")]
    [InlineData("no region hunt")]
    [InlineData("no scoreboard")]
    [InlineData("over 20")]
    [InlineData("fourteenth")]
    [InlineData("missing")]
    public void HuntingGrounds_fails_when_the_template_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == "spider-hunting-grounds");
        var action = (JsonObject)ev["action"]!;
        var units = (JsonArray)action["units"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "invalid": action["bogus"] = 1; break;
            case "another faction": units[0]!["prefab"] = "CHAR_Bandit_Thug"; break;
            case "vblood": units[0]!["prefab"] = "CHAR_Spider_Queen_VBlood"; break;
            case "summon": units[0]!["prefab"] = "CHAR_Spider_Melee_Summon"; break;
            case "baneling": units[0]!["prefab"] = "CHAR_Spider_Baneling"; break;
            case "no region location": action["location"] = new JsonObject { ["type"] = "Admin" }; break;
            case "no lock": action.Remove("regionLock"); break;
            case "no region hunt": action["behaviour"] = new JsonObject { ["type"] = "Hunt", ["range"] = 45 }; break;
            case "no scoreboard": action.Remove("scoreboard"); break;
            case "over 20": units[0]!["count"] = 17; break;          // 21 units in a wave
            case "fourteenth":
                var copy = JsonNode.Parse(ev.ToJsonString())!.AsObject();
                copy["id"] = "spider-hunting-grounds-2";
                events.Add(copy);
                break;
            default: events.Remove(ev); break;
        }
        // The fakes know every spider prefab, so a valid swap is caught by the shape rule and not only as unknown.
        var known = Units(SpiderFaction);
        Assert.NotEmpty(HuntingGroundsDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), known, known)));
    }

    [Fact]
    public void HuntingGrounds_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("spider-hunting-grounds missing",
            HuntingGroundsDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));
}
