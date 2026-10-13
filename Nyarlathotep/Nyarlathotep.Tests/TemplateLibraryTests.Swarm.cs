using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D11: spider-swarm, disabled and valid: an Admin location with an arena, Hunt, the scoreboard and the
/// K/D winner; the units form with growth on a base wave of Faction_Spiders units only (no V Blood, no `_Summon`, no
/// Baneling), so the generator makes each wave larger than the one before and none over the default MaxUnitsPerWave of
/// 20; thirteen templates in all since revenge D14. The shape is read, not the numbers (S-10).</summary>
public partial class TemplateLibraryTests
{
    /// <summary>Every Faction_Spiders unit of Reference Data/unit_index.tsv, written out here independently of the file.</summary>
    static readonly string[] SpiderFaction =
    [
        "CHAR_Spider_Baneling", "CHAR_Spider_Baneling_Summon", "CHAR_Spider_Broodmother", "CHAR_Spider_Forest", "CHAR_Spider_Forestling",
        "CHAR_Spider_Melee", "CHAR_Spider_Melee_GateBoss_Summon", "CHAR_Spider_Melee_Summon", "CHAR_Spider_Queen_VBlood", "CHAR_Spider_Range",
        "CHAR_Spider_Range_Summon", "CHAR_Spider_Spiderling", "CHAR_Spider_Spiderling_VerminNest", "CHAR_Spiderling_Summon",
    ];

    static List<string> SwarmDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != 13) diffs.Add($"{c.Templates.Count} templates, not 13");
        var t = c.Find("spider-swarm");
        if (t is null) { diffs.Add("spider-swarm missing"); return diffs; }
        if (t.Invalid is { } why) diffs.Add($"invalid: {why}");
        var d = t.Definition;
        if (d.Enabled) diffs.Add("enabled");
        if (d.Action is not { } a) { diffs.Add("no wave action"); return diffs; }
        foreach (var u in a.Units)
        {
            if (!SpiderFaction.Contains(u.Prefab)) diffs.Add($"{u.Prefab} is not a Faction_Spiders unit");
            if (u.Prefab.Contains("VBlood", StringComparison.OrdinalIgnoreCase) || u.Prefab.Contains("_Summon", StringComparison.Ordinal)
                || u.Prefab.Contains("Baneling", StringComparison.Ordinal))
                diffs.Add($"{u.Prefab} is a V Blood, Summon or Baneling prefab");
        }
        if (a.Location.Type != LocationType.Admin) diffs.Add("not an Admin location");
        if (a.Arena is null) diffs.Add("no arena");
        if (!a.Scoreboard) diffs.Add("no scoreboard");
        if (a.Winner != WinnerRule.KillDeath) diffs.Add("no K/D winner");
        if (a.Behaviour is not { Type: BehaviourType.Hunt }) diffs.Add("no Hunt");
        if (a.Growth is null || a.WaveList is not { } list) { diffs.Add("no growth"); return diffs; }
        var sizes = list.Select(w => w.Units.Sum(u => u.Count)).ToList();
        if (!sizes.Zip(sizes.Skip(1)).All(p => p.Second > p.First)) diffs.Add("a generated wave is not larger than the one before: " + string.Join(",", sizes));
        if (sizes.Any(s => s > 20)) diffs.Add("a generated wave exceeds the default MaxUnitsPerWave of 20: " + string.Join(",", sizes));
        return diffs;
    }

    [Fact]
    public void Swarm_passes_spider_swarm_as_planned()
    {
        var c = Real();
        Assert.Empty(SwarmDifferences(c));
        var list = c.Find("spider-swarm")!.Definition.Action!.WaveList!;
        var info = EventLines.Info(c.Find("spider-swarm")!.Definition, null, DateTime.UtcNow);
        Assert.Contains(info, l => l.StartsWith($"wave {list.Count}: {list[^1].Units.Sum(u => u.Count)} units", StringComparison.Ordinal));
        Assert.Contains(info, l => l.StartsWith("arena: ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("invalid")]
    [InlineData("another faction")]
    [InlineData("vblood")]
    [InlineData("summon")]
    [InlineData("baneling")]
    [InlineData("no growth")]
    [InlineData("not larger")]
    [InlineData("over 20")]
    [InlineData("no arena")]
    [InlineData("no scoreboard")]
    [InlineData("no winner")]
    [InlineData("missing")]
    public void Swarm_fails_when_the_template_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == "spider-swarm");
        var action = (JsonObject)ev["action"]!;
        var units = (JsonArray)action["units"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "invalid": action["arena"]!["radius"] = 5; break;
            case "another faction": units[0]!["prefab"] = "CHAR_Bandit_Thug"; break;
            case "vblood": units[0]!["prefab"] = "CHAR_Spider_Queen_VBlood"; break;
            case "summon": units[0]!["prefab"] = "CHAR_Spider_Melee_Summon"; break;
            case "baneling": units[0]!["prefab"] = "CHAR_Spider_Baneling"; break;
            case "no growth": action.Remove("growth"); break;
            case "not larger": action["growth"]!["addUnits"] = 0; break;
            case "over 20": action["growth"]!["addUnits"] = 2; break;
            case "no arena": action.Remove("arena"); break;
            case "no scoreboard": action.Remove("winner"); action.Remove("scoreboard"); break;
            case "no winner": action.Remove("winner"); break;
            default: events.Remove(ev); break;
        }
        // The fakes know every spider prefab, so a valid swap is caught by the shape rule and not only as unknown.
        var known = Units(SpiderFaction);
        Assert.NotEmpty(SwarmDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), known, known)));
    }

    [Fact]
    public void Swarm_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("spider-swarm missing", SwarmDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));
}
