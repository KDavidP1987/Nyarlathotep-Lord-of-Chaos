using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D14: bandit-revenge, disabled and valid: the default revenge trigger (Faction_Bandits, heat 30, window
/// 900 s, warnings at 10 and 20, vbloodHeat 10, cooldown 30 min), 900 s, a waveList of three Faction_Bandits tiers (4 at
/// the start, 6 and 8 when cleared or after 120 s, the last led by a CHAR_Bandit_Deadeye at maxHealth 2.0, levelDelta +0,
/// +1, +2), AroundPlayer 20-40 m, Hunt 45, the scoreboard on; thirteen templates then, one more than 0.11.0's twelve (fourteen since apocalypse D18).
/// revenge D31: spider-swarm and spider-hunting-grounds end on a CHAR_Spider_Queen_VBlood boss at or above the final wave's
/// level (spider-swarm's final wave: level 30 + 4 x 3 = 42; spider-hunting-grounds': 30).</summary>
public partial class TemplateLibraryTests
{
    const int TemplatesBefore = 12;                                             // v0.11.0's library

    static readonly string[] BanditFaction = ["CHAR_Bandit_Thug", "CHAR_Bandit_Hunter", "CHAR_Bandit_Deadeye"];

    static List<string> RevengeDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != TemplatesNow) diffs.Add($"{c.Templates.Count} templates, not {TemplatesNow}");
        var t = c.Find("bandit-revenge");
        if (t is null) { diffs.Add("bandit-revenge missing"); return diffs; }
        if (t.Invalid is { } why) diffs.Add($"invalid: {why}");
        var d = t.Definition;
        if (d.Enabled) diffs.Add("enabled");
        var tr = d.Trigger;
        if (tr.Type != TriggerType.Revenge) diffs.Add("not a Revenge trigger");
        if (!(tr.Factions ?? []).SequenceEqual(["Faction_Bandits"]) || tr.Heat != 30 || tr.WindowSeconds != 900
            || !(tr.WarnAt ?? []).SequenceEqual([10, 20]) || tr.VBloodHeat != 10 || tr.PlayerCooldownMinutes != 30)
            diffs.Add("not the default revenge trigger");
        if (d.DurationSeconds != 900) diffs.Add($"duration {d.DurationSeconds}");
        if (d.Action is not { } a) { diffs.Add("no wave action"); return diffs; }
        if (a.WaveList is not { } list) { diffs.Add("no waveList"); return diffs; }
        if (list.Count != 3) diffs.Add($"{list.Count} tiers");
        int[] sizes = [4, 6, 8];
        for (var i = 0; i < Math.Min(3, list.Count); i++)
        {
            var w = list[i];
            if (w.Units.Sum(u => u.Count) != sizes[i]) diffs.Add($"tier {i + 1} has {w.Units.Sum(u => u.Count)} units");
            if (i == 0 && (w.WhenCleared || w.AfterSeconds is not null and not 0)) diffs.Add("tier 1 does not start at once");
            if (i > 0 && (!w.WhenCleared || w.AfterSeconds != 120)) diffs.Add($"tier {i + 1} is not when cleared or after 120 s");
            if (w.Units.Any(u => (u.Modifiers?.LevelDelta ?? 0) != i)) diffs.Add($"tier {i + 1} is not levelDelta +{i}");
            if (w.Units.Any(u => !BanditFaction.Contains(u.Prefab))) diffs.Add($"tier {i + 1} has a unit outside Faction_Bandits");
        }
        if (list.Count == 3 && !list[2].Units.Any(u => u.Prefab == "CHAR_Bandit_Deadeye" && u.Count == 1 && u.Modifiers?.MaxHealth == 2.0))
            diffs.Add("tier 3 lacks its Deadeye leader at maxHealth 2.0");
        if (a.Location is not { Type: LocationType.AroundPlayer, MinDist: 20, MaxDist: 40 }) diffs.Add("not AroundPlayer 20-40 m");
        if (a.Behaviour is not { Type: BehaviourType.Hunt, Range: 45 }) diffs.Add("not Hunt 45");
        if (!a.Scoreboard) diffs.Add("no scoreboard");
        return diffs;
    }

    [Fact]
    public void Revenge_passes_bandit_revenge_as_planned()
    {
        var c = Real();
        Assert.Empty(RevengeDifferences(c));
        Assert.Equal(TemplatesNow, c.ValidCount);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("invalid")]
    [InlineData("two tiers")]
    [InlineData("tier size")]
    [InlineData("start rule")]
    [InlineData("no hunt")]
    [InlineData("no scoreboard")]
    [InlineData("no leader")]
    [InlineData("heat")]
    [InlineData("fourteenth")]
    [InlineData("missing")]
    public void Revenge_fails_when_the_template_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == "bandit-revenge");
        var action = (JsonObject)ev["action"]!;
        var list = (JsonArray)action["waveList"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "invalid": action["bogus"] = 1; break;
            case "two tiers": list.RemoveAt(2); break;
            case "tier size": list[1]!["units"]![0]!["count"] = 5; break;
            case "start rule": ((JsonObject)list[1]!).Remove("whenCleared"); break;
            case "no hunt": action.Remove("behaviour"); break;
            case "no scoreboard": action.Remove("scoreboard"); break;
            case "no leader": list[2]!["units"]![2]!["modifiers"]!["maxHealth"] = 1.5; break;
            case "heat": ev["trigger"]!["heat"] = 31; break;
            case "fourteenth":
                var copy = JsonNode.Parse(ev.ToJsonString())!.AsObject();
                copy["id"] = "bandit-revenge-2";
                events.Add(copy);
                break;
            default: events.Remove(ev); break;
        }
        Assert.NotEmpty(RevengeDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units())));
    }

    [Fact]
    public void Revenge_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("bandit-revenge missing",
            RevengeDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));

    // ---- D31 Bosses (templates)

    /// <summary>Each spider template's final-wave boss: Ungoro, one, at or above the final wave's level.</summary>
    static List<string> BossDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        foreach (var (id, finalLevel) in new[] { ("spider-swarm", 42), ("spider-hunting-grounds", 30) })
        {
            var t = c.Find(id);
            if (t is null) { diffs.Add($"{id} missing"); continue; }
            if (t.Invalid is { } why) diffs.Add($"{id} invalid: {why}");
            if (t.Definition.Action?.Bosses is not [{ Prefab: "CHAR_Spider_Queen_VBlood", Count: 1 } boss])
            {
                diffs.Add($"{id} does not end on one CHAR_Spider_Queen_VBlood");
                continue;
            }
            if ((boss.Modifiers?.Level ?? 0) < finalLevel) diffs.Add($"{id}'s boss is below level {finalLevel}");
        }
        return diffs;
    }

    [Fact]
    public void Bosses_passes_the_spider_templates_end_on_ungoro() => Assert.Empty(BossDifferences(Real()));

    [Fact]
    public void Bosses_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("spider-swarm missing", BossDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));

    [Theory]
    [InlineData("spider-swarm", "none")]
    [InlineData("spider-swarm", "low")]
    [InlineData("spider-hunting-grounds", "none")]
    [InlineData("spider-hunting-grounds", "two")]
    [InlineData("spider-hunting-grounds", "another")]
    public void Bosses_fails_when_a_template_loses_its_boss(string id, string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var ev = ((JsonArray)root["events"]!).OfType<JsonObject>().First(e => (string)e["id"]! == id);
        var action = (JsonObject)ev["action"]!;
        var boss = (JsonObject)((JsonArray)action["bosses"]!)[0]!;
        switch (mutation)
        {
            case "none": action.Remove("bosses"); break;
            case "low": boss["modifiers"]!["level"] = 40; break;
            case "two": boss["count"] = 2; break;
            default: boss["prefab"] = "CHAR_Spider_Broodmother"; break;
        }
        var known = Units(SpiderFaction);
        Assert.NotEmpty(BossDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), known, known)));
    }
}
