using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D17: a fanned-out wave is one wave of the event, so it sends one wave push whatever its group count,
/// and a fanned-out wave with no group spawned sends none.</summary>
public partial class PushTests
{
    static WaveFacts FanFacts() => new(1, "hunters", false, Location: LocationType.AroundPlayer, Pick: PickOutcome.Picked);

    static GroupCentre[] Centres(int n) => Enumerable.Range(0, n).Select(i => new GroupCentre(i * 200f, 0, 0, false)).ToArray();

    /// <summary>The wave action's report of a fanned-out wave: EventEngine.WaveDecided, once per wave.</summary>
    static void Report(EventEngine engine, FanOutDecision d, string id) => engine.WaveDecided(id, d.Outcome);

    static string Hunters => Json.Event("hunters", action: Json.ValidAction.Replace("{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }",
        "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }").TrimEnd('}') + ", \"fanOut\": { \"maxInstances\": 3, \"minSpacing\": 150 } }");

    [Fact]
    public void Automation_fails_when_fanned_out_wave_pushes_more_than_once()
    {
        var (e, _, hub) = Wired(Hunters);
        Assert.Null(e.Start("hunters", "manual", T0, Open()));
        var before = Texts(hub).Count;
        var d = WaveGate.DecideGroups(FanFacts(), Centres(3), () => ["CHAR_Bandit_Thug"], 20, 0, 150);
        Assert.Equal(3, d.Groups.Count);
        Report(e, d, "hunters");
        Assert.Equal(["[NYAR:ev] type=wave id=hunters secs=0 wave=1"], Texts(hub).Skip(before));
        // a report per group would push three times: the planted per-group report is caught
        var (planted, _, plantedHub) = Wired(Hunters);
        Assert.Null(planted.Start("hunters", "manual", T0, Open()));
        var start = Texts(plantedHub).Count;
        foreach (var _ in d.Groups) planted.WaveSpawned("hunters");
        Assert.NotEqual(1, Texts(plantedHub).Count - start);
    }

    /// <summary>WaveAction's reporting, read from its source (step 2 Codex F3; the service needs the game, so the check is
    /// structural): no WaveSpawned or WaveSkipped call of its own, exactly two EventEngine.WaveDecided calls (the skip
    /// path, which returns, and the spawn path), and none inside the loop over the groups.</summary>
    internal static List<string> WaveReportProblems(string source)
    {
        var problems = new List<string>();
        if (source.Contains("WaveSpawned(") || source.Contains("WaveSkipped(")) problems.Add("reports a wave around WaveDecided");
        var calls = System.Text.RegularExpressions.Regex.Matches(source, @"\bWaveDecided\(").Count;
        if (calls != 2) problems.Add($"{calls} WaveDecided calls, not 2");
        const string loop = "foreach (var group in decision.Groups)";
        var at = source.IndexOf(loop, StringComparison.Ordinal);
        if (at < 0) problems.Add("no loop over the groups");
        else
        {
            var open = source.IndexOf('{', at);
            var depth = 0;
            var end = open;
            for (; end < source.Length; end++)
            {
                if (source[end] == '{') depth++;
                else if (source[end] == '}' && --depth == 0) break;
            }
            if (source[open..end].Contains("WaveDecided(")) problems.Add("reports inside the group loop");
        }
        return problems;
    }

    static string WaveActionSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", "WaveAction.cs"));
    }

    [Fact]
    public void Automation_fails_when_wave_action_reports_per_group()
    {
        var source = WaveActionSource();
        Assert.Empty(WaveReportProblems(source));
        // planted: a report per group, a third report, and the pre-automation calls
        const string loop = "foreach (var group in decision.Groups)";
        var perGroup = source.Replace(loop + "\r\n        {", loop + "\r\n        {\r\n            EventRuntime.Engine.WaveDecided(id, decision.Outcome);")
                             .Replace(loop + "\n        {", loop + "\n        {\n            EventRuntime.Engine.WaveDecided(id, decision.Outcome);");
        Assert.Contains("reports inside the group loop", WaveReportProblems(perGroup));
        Assert.Contains("3 WaveDecided calls, not 2", WaveReportProblems(perGroup));
        Assert.Contains("reports a wave around WaveDecided", WaveReportProblems(source + "EventRuntime.Engine.WaveSpawned(id);"));
        Assert.Contains("1 WaveDecided calls, not 2", WaveReportProblems(source[..source.IndexOf("WaveDecided(", StringComparison.Ordinal)]
            + source[(source.IndexOf("WaveDecided(", StringComparison.Ordinal) + "WaveDecided(".Length)..]));
    }

    [Fact]
    public void Automation_passes_one_push_whatever_the_groups()
    {
        foreach (var n in new[] { 1, 2, 10 })
        {
            var (e, _, hub) = Wired(Hunters);
            Assert.Null(e.Start("hunters", "manual", T0, Open()));
            var before = Texts(hub).Count;
            Report(e, WaveGate.DecideGroups(FanFacts(), Centres(n), () => ["CHAR_Bandit_Thug"], 20, 0, 150), "hunters");
            Assert.Single(Texts(hub).Skip(before));
            Assert.Equal(1, e.Active.Single().WavesSpawned);
        }
        var row = ApiLines.Definitions(new DefinitionSet([Json.One(Hunters)]), new HashSet<string>()).Single();
        Assert.DoesNotContain("fanOut", row, StringComparison.OrdinalIgnoreCase);    // no new key on the def row
    }

    [Fact]
    public void Automation_empty_no_group_spawned()
    {
        var (e, _, hub) = Wired(Hunters);
        Assert.Null(e.Start("hunters", "manual", T0, Open()));
        var before = Texts(hub).Count;
        var claimed = Centres(3).Select(c => c with { Claimed = true }).ToArray();
        Report(e, WaveGate.DecideGroups(FanFacts(), claimed, () => ["CHAR_Bandit_Thug"], 20, 0, 150), "hunters");
        Report(e, WaveGate.DecideGroups(FanFacts(), Centres(3), () => [], 20, 0, 150), "hunters");
        Report(e, WaveGate.DecideGroups(FanFacts(), Centres(3), () => ["CHAR_Bandit_Thug"], 20, 150, 150), "hunters");
        Assert.Equal(before, Texts(hub).Count);
        Assert.Equal(3, e.Active.Single().WavesSkipped);
        // a blocked wave (NoWave) is neither counted nor pushed, as in 0.7.0 (round 2 F1)
        Report(e, WaveGate.DecideGroups(FanFacts() with { Blocked = true }, Centres(3), () => ["CHAR_Bandit_Thug"], 20, 0, 150), "hunters");
        Assert.Equal((3, 0), (e.Active.Single().WavesSkipped, e.Active.Single().WavesSpawned));
    }
}
