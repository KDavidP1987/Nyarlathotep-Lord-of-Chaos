using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D9 over the real Services/EventRuntime.cs (step 2 review L4): every end path of a running
/// apocalypse reaches ApocalypseAction (Logic/ApocalypseHost.Ended, which removes the record), and a restart keeps the
/// apocalypse's record and cancels every other instance. A source check, as EndPathTests' RuntimeGaps: Services cannot be
/// compiled into this project (Epic S-21).</summary>
public partial class ApocalypseTests
{
    /// <summary>Each end path of EventRuntime.cs that misses its apocalypse call, named.</summary>
    internal static List<string> ApocalypseRuntimeGaps(string source)
    {
        source = source.Replace("\r\n", "\n");
        string Body(string signature) => EndPathTests.Body(source, signature);
        var gaps = new List<string>();
        if (!Body("static bool End(string id, string why, EndPath path").Contains("ApocalypseAction.Ended(id, ApocalypseEndOf(path), cancelled)", StringComparison.Ordinal))
            gaps.Add("stop");
        if (!source.Contains("EndPath.PillarOff => ApocalypseEnd.PillarOff", StringComparison.Ordinal)) gaps.Add("pillar off");
        if (!source.Contains("EndPath.Fault => ApocalypseEnd.Fault", StringComparison.Ordinal)) gaps.Add("fault");
        if (!Body("internal static Outcome Purge()").Contains("ApocalypseAction.Purged(apocalypseCancelled);", StringComparison.Ordinal)) gaps.Add("purge");
        if (!Body("internal static IReadOnlyList<string> EndPillar(Pillar pillar)").Contains("ApocalypseAction.PillarOff(pillar);", StringComparison.Ordinal))
            gaps.Add("pillar off in the drain wait");
        if (!Body("internal static void Tick(DateTime now)").Contains("ApocalypseAction.Ended(ended.Id, ApocalypseEnd.Survived, cancelled)", StringComparison.Ordinal))
            gaps.Add("survived");
        if (!Body("static void TickApocalypse(ActiveEvent active, DateTime now)").Contains("ApocalypseAction.Ended(ended.Id, EndRule.Of(step.Result), cancelled)", StringComparison.Ordinal))
            gaps.Add("won or lost");
        var init = Body("internal static void Initialize()");
        if (!init.Contains("ApocalypseAction.Boot(", StringComparison.Ordinal) || !init.Contains("instance.EventId == resuming", StringComparison.Ordinal))
            gaps.Add("restart keeps the record");
        if (!init.Contains("cancelled by restart", StringComparison.Ordinal) || !init.Contains("RemoveAll(i => i.EventId != resuming)", StringComparison.Ordinal))
            gaps.Add("restart cancels the others");
        return gaps;
    }

    [Fact]
    public void EndPaths_fails_when_the_runtime_skips_an_apocalypse_end_path()
    {
        var source = EndPathTests.RuntimeSource().Replace("\r\n", "\n");
        Assert.Empty(ApocalypseRuntimeGaps(source));
        foreach (var (call, gap) in new[]
                 {
                     ("ApocalypseAction.Ended(id, ApocalypseEndOf(path), cancelled)", "stop"),
                     ("EndPath.PillarOff => ApocalypseEnd.PillarOff", "pillar off"),
                     ("EndPath.Fault => ApocalypseEnd.Fault", "fault"),
                     ("ApocalypseAction.Purged(apocalypseCancelled);", "purge"),
                     ("ApocalypseAction.PillarOff(pillar);", "pillar off in the drain wait"),
                     ("ApocalypseAction.Ended(ended.Id, ApocalypseEnd.Survived, cancelled)", "survived"),
                     ("ApocalypseAction.Ended(ended.Id, EndRule.Of(step.Result), cancelled)", "won or lost"),
                     ("instance.EventId == resuming", "restart keeps the record"),
                     ("RemoveAll(i => i.EventId != resuming)", "restart cancels the others"),
                 })
        {
            Assert.Contains(call, source);
            Assert.Equal([gap], ApocalypseRuntimeGaps(source.Replace(call, "x")));
        }
        // a commented-out call is no call
        Assert.Equal(["purge"], ApocalypseRuntimeGaps(source.Replace("ApocalypseAction.Purged(apocalypseCancelled);", "// ApocalypseAction.Purged(apocalypseCancelled);")));
    }
}
