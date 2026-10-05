using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D16: graveyard-waves, disabled and valid: three waves of undead (wave 1 at the start, wave 2 when
/// cleared or after 120 s, wave 3 when cleared), levels and stats rising per wave, a boss-like last entry with maxHealth
/// 2.0, AroundPlayer 20-40 m with Hunt 45, the scoreboard on; ten templates in all.</summary>
public partial class TemplateLibraryTests
{
    static List<string> WaveSetDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != 10) diffs.Add($"{c.Templates.Count} templates, not 10");
        var t = c.Find("graveyard-waves");
        if (t is null) { diffs.Add("graveyard-waves missing"); return diffs; }
        if (t.Invalid is { } why) diffs.Add($"invalid: {why}");
        var d = t.Definition;
        if (d.Enabled) diffs.Add("enabled");
        if (d.Action is not { WaveList: { } list } a) { diffs.Add("no wave list"); return diffs; }
        var rules = list.Select((w, i) => WaveSchedule.StartRule(w, i + 1)).ToList();
        if (!rules.SequenceEqual(["at start", "after 120 s or when cleared", "when cleared"])) diffs.Add("start rules " + string.Join("; ", rules));
        if (!a.Scoreboard) diffs.Add("no scoreboard");
        if ($"{a.Location.Type} {a.Location.MinDist}-{a.Location.MaxDist}" != "AroundPlayer 20-40" || a.Behaviour?.Range != 45)
            diffs.Add("not AroundPlayer 20-40 with Hunt 45");
        var deltas = list.Select(w => w.Units[0].Modifiers?.LevelDelta ?? 0).ToList();
        if (!deltas.Zip(deltas.Skip(1)).All(p => p.Second > p.First)) diffs.Add("levels do not rise per wave");
        if (list[^1].Units[^1].Modifiers?.MaxHealth != 2.0) diffs.Add("the last entry is not maxHealth 2.0");
        if (list.Any(w => w.Units.Sum(u => u.Count) > 20)) diffs.Add("a wave exceeds the default MaxUnitsPerWave of 20");
        return diffs;
    }

    [Fact]
    public void WaveSets_passes_graveyard_waves_as_planned()
    {
        Assert.Empty(WaveSetDifferences(Real()));
        Assert.Contains(EventLines.Info(Real().Find("graveyard-waves")!.Definition, null, DateTime.UtcNow), l => l.StartsWith("wave 3: 7 units", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("invalid")]
    [InlineData("two waves")]
    [InlineData("start rule")]
    [InlineData("no scoreboard")]
    [InlineData("missing")]
    public void WaveSets_fails_when_the_template_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == "graveyard-waves");
        var list = (JsonArray)ev["action"]!["waveList"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "invalid": list[0]!["units"]![0]!["count"] = 51; break;
            case "two waves": list.RemoveAt(2); break;
            case "start rule": ((JsonObject)list[1]!).Remove("afterSeconds"); break;
            case "no scoreboard": ((JsonObject)ev["action"]!).Remove("scoreboard"); break;
            default: events.Remove(ev); break;
        }
        Assert.NotEmpty(WaveSetDifferences(TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units())));
    }

    [Fact]
    public void WaveSets_empty_none_in_an_empty_catalogue() =>
        Assert.Contains("graveyard-waves missing", WaveSetDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));
}
