using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation A6: what the player scan writes with Debug.VerboseLogging on. Not a D15 dependency control.</summary>
public class AutomationLoggingTests
{
    static class D
    {
        internal static DateTime T0 => AutomationDependencyFailureTests.T0;
        internal static Func<float, float, string> RegionOf => AutomationDependencyFailureTests.RegionOf;
        internal static DefinitionSet Entered => AutomationDependencyFailureTests.Entered;
        internal static ScanRow At(float x) => AutomationDependencyFailureTests.At(x);
    }

    [Fact]
    public void Verbose_scan_line_only_on_change_or_entry()
    {
        // automation A6: the verbose scan line is written when its counts change or an entry is found, not every scan
        var feed = new PlayerTriggerFeed();
        var lines = new List<string>();
        void Run(DateTime at, params ScanRow[] rows) => feed.Scan(() => rows, D.RegionOf, D.Entered, _ => false, at, _ => { }, _ => { }, lines.Add);
        Run(D.T0);
        Run(D.T0.AddSeconds(5));
        Run(D.T0.AddSeconds(10), D.At(-10));
        Run(D.T0.AddSeconds(15), D.At(-10));
        Run(D.T0.AddSeconds(20), D.At(10));                                         // an entry
        Run(D.T0.AddSeconds(25), D.At(10));
        Assert.Equal(["player triggers: 0 players, 0 entries", "player triggers: 1 players, 0 entries",
            "player triggers: 1 players, 1 entries", "player triggers: 1 players, 0 entries"], lines);
        // a scan with no definition resets it: the next scan logs again
        feed.Scan(() => [], D.RegionOf, DefinitionSet.Empty, _ => false, D.T0.AddSeconds(30), _ => { }, _ => { }, lines.Add);
        Run(D.T0.AddSeconds(35), D.At(10));
        Assert.Equal("player triggers: 1 players, 0 entries", lines[^1]);
        Assert.Equal(5, lines.Count);
    }
}
