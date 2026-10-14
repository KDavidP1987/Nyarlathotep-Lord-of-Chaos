using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D15: `.nyar event info` of a running event shows "unit watch: &lt;s&gt; stuck now, &lt;r&gt; recovered,
/// &lt;x&gt; removed" (the instance's counts from the watch phase), "unit watch: off" with Enabled false, and nothing for an
/// event that is not running; every line fits 480 bytes and the other lines are unchanged.</summary>
public partial class AuthoringTests
{
    /// <summary>uw running with one unit stranded under the map, kept there after every check for <paramref name="seconds"/>.</summary>
    static (WatchRig R, ActiveEvent A) Watching(int seconds)
    {
        var (r, a) = Watch.Rig();
        var key = r.W.Add("uw", WatchRig.T0.AddSeconds(1), 520, 500, -150f);
        r.W.Put(key, 500, 500, -150f);
        r.W.MoveTo(key, 520, 500);
        r.For(seconds, () =>
        {
            if (r.W.At.ContainsKey(key)) r.W.Sink(key, -150f);
        });
        return (r, a);
    }

    static IReadOnlyList<string> InfoOf(WatchRig r, ActiveEvent? a, bool enabled = true) =>
        EventLines.Info(r.Active[0].Definition, a, r.Now, watch: UnitWatchLines.Info(enabled, a is null ? default : r.P.Counts(a)));

    [Fact]
    public void UnitWatch_fails_when_a_running_event_lacks_the_line_or_a_count_is_wrong()
    {
        var (r, a) = Watching(20);                                 // marked at 15 s, moved at 20 s
        Assert.Contains("unit watch: 1 stuck now, 1 recovered, 0 removed", InfoOf(r, a));
        r.For(35, () => { foreach (var k in r.W.At.Keys.ToList()) r.W.Sink(k, -150f); });
        Assert.Contains("unit watch: 0 stuck now, 2 recovered, 1 removed", InfoOf(r, a));
        var again = Watch.Run(a.Definition, r.Now);                // a new instance counts from its own start
        Assert.Equal(default(WatchCounts), r.P.Counts(again));
    }

    [Fact]
    public void UnitWatch_fails_when_an_idle_event_shows_one()
    {
        var (r, _) = Watching(20);
        Assert.DoesNotContain(InfoOf(r, null), l => l.StartsWith("unit watch", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitWatch_fails_when_a_line_exceeds_480_bytes()
    {
        var (r, a) = Watching(20);
        var big = UnitWatchLines.Info(true, new WatchCounts(500, 999_999, 999_999));
        var lines = EventLines.Info(a.Definition, a, r.Now, watch: big);
        Assert.Contains(big, lines);
        Assert.All(lines, l => Assert.InRange(Encoding.UTF8.GetByteCount(l), 1, Wire.MaxBytes));
    }

    [Fact]
    public void UnitWatch_fails_when_watch_off_shows_counts()
    {
        var (r, a) = Watching(20);
        var lines = InfoOf(r, a, enabled: false);
        Assert.Contains("unit watch: off", lines);
        Assert.DoesNotContain(lines, l => l.Contains("stuck now", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitWatch_fails_when_another_line_changes()
    {
        var (r, a) = Watching(20);
        var without = EventLines.Info(a.Definition, a, r.Now);
        var with = InfoOf(r, a);
        Assert.Equal(without.Count + 1, with.Count);
        Assert.Equal(without, with.Where(l => !l.StartsWith("unit watch:", StringComparison.Ordinal)));
        Assert.Equal(without, EventLines.Info(a.Definition, a, r.Now, watch: null));
    }

    [Fact]
    public void UnitWatch_passes_running_line()
    {
        var (r, a) = Watching(55);
        Assert.Equal("unit watch: 0 stuck now, 2 recovered, 1 removed", InfoOf(r, a)[^1]);
    }

    [Fact]
    public void UnitWatch_empty_not_running_no_line()
    {
        var d = Watch.Waves();
        Assert.Equal(EventLines.Info(d, null, WatchRig.T0), EventLines.Info(d, null, WatchRig.T0, watch: "unit watch: 0 stuck now, 0 recovered, 0 removed"));
    }
}
