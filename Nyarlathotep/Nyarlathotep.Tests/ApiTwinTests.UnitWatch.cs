using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D13: `.nyar api event unstick &lt;id&gt;` runs D12's flow and answers one line, `[NYAR:ok] cmd=event
/// verb=unstick id= marked= fighting=` or an err line with code state, badarg (id, extra) or invalid reason=watch_off (an
/// unknown id is notfound arg=id, as every event twin's); it is counted by the twin rate, logged with "api " and adds no
/// push kind; Wire.Api is 8.</summary>
public partial class ApiTwinTests
{
    static (Rig R, WatchWorld W) Watched(RateGate? gate = null)
    {
        var r = gate is null ? Default() : new Rig(Library.Of(Json.Event("raid"), Json.Empower("surge")), gate: gate);
        var w = new WatchWorld();
        r.Ops.WatchWorld = w;
        Assert.True(r.Ops.OpStartEvent("raid", null).Ok);
        return (r, w);
    }

    /// <summary>An idle unit and one that hit a player 30 s ago.</summary>
    static (long Idle, long Hit) Two(Rig r, WatchWorld w)
    {
        var idle = w.Add("raid", r.Ops.Now, 500, 500);
        var hit = w.Add("raid", r.Ops.Now, 520, 500);
        Assert.True(r.Ops.Watch.Fights.Report(hit, true, r.Ops.Now.AddSeconds(-30), w.Ledger.IsTracked));
        return (idle, hit);
    }

    sealed class PushRecorder : IPushSink
    {
        public List<string> Calls { get; } = [];
        public void EventStarted(RunningInstance instance) => Calls.Add("start");
        public void EventEnded(string id, string region = "-") => Calls.Add("end");
        public void Wave(string id, int wave) => Calls.Add("wave");
        public void Purged(int cooldownSeconds) => Calls.Add("purged");
        public void ConfigChanged() => Calls.Add("config");
    }

    [Fact]
    public void UnitWatch_fails_when_the_ok_line_differs()
    {
        var (r, w) = Watched();
        Two(r, w);
        var line = Event(r, "unstick", "raid");
        Assert.Equal("[NYAR:ok] cmd=event verb=unstick id=raid marked=1 fighting=1", line);
        Assert.Equal(["cmd", "verb", "id", "marked", "fighting"], Keys(line));
        Assert.Equal(1, r.Lib.Log.Count("api event unstick raid"));
    }

    [Fact]
    public void UnitWatch_fails_when_a_refusal_answers_ok_or_changes_a_unit()
    {
        var (r, w) = Watched();
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=state arg=id", Event(r, "unstick", "raid"));       // no live unit yet
        var (idle, _) = Two(r, w);
        var refusals = new[]
        {
            Event(r, "unstick", "nosuch"), Event(r, "unstick", "surge"), Event(r, "unstick"), Event(r, "unstick", "raid", "now"),
        };
        r.Ops.WatchSettings = WatchSettings.Default with { Enabled = false };
        var off = Event(r, "unstick", "raid");
        Assert.All(refusals.Append(off), l => Assert.StartsWith("[NYAR:err] cmd=event verb=unstick code=", l));
        Assert.Null(r.Ops.Watch.MarkOf(idle));
        Assert.Equal(0, r.Ops.Watch.AllRecords - r.Ops.Watch.Fights.Rows);
        Assert.Equal(0, r.Lib.Log.Count("unit-watch"));
    }

    [Fact]
    public void UnitWatch_fails_when_a_code_or_reason_differs()
    {
        var (r, w) = Watched();
        Two(r, w);
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=notfound arg=id", Event(r, "unstick", "nosuch"));
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=state arg=id reason=not_active", Event(r, "unstick", "surge"));
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=badarg arg=id", Event(r, "unstick"));
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=badarg arg=extra", Event(r, "unstick", "raid", "now"));
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=badarg arg=extra", Event(r, "unstick", "raid", "", "", "more"));
        r.Ops.WatchSettings = WatchSettings.Default with { Enabled = false };
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=invalid reason=watch_off", Event(r, "unstick", "raid"));
    }

    [Fact]
    public void UnitWatch_fails_when_the_twin_skips_the_rate()
    {
        var (r, w) = Watched(new RateGate());
        Two(r, w);
        var lines = Enumerable.Range(0, 6).Select(_ => Event(r, "unstick", "raid")).ToList();
        Assert.All(lines.Take(5), l => Assert.StartsWith("[NYAR:ok] cmd=event verb=unstick id=raid ", l));
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=ratelimit secs=1", lines[5]);
        Assert.Equal(5, r.Ops.Calls.Count(c => c == "unstick raid"));
    }

    [Fact]
    public void UnitWatch_fails_when_a_push_kind_is_added_or_the_api_is_not_8()
    {
        var (r, w) = Watched();
        Two(r, w);
        var push = new PushRecorder();
        r.Ops.Engine.Push = push;
        r.Lib.Catalog.Push = push;
        Assert.StartsWith("[NYAR:ok] ", Event(r, "unstick", "raid"));
        Assert.Empty(push.Calls);
        Assert.Equal(8, Wire.Api);
        Assert.Equal(["config-changed", "event-end", "event-start", "killswitch", "wave", "wave-warn"],
            typeof(PushLines).GetFields().Where(f => f.IsLiteral && f.Name.EndsWith("Type", StringComparison.Ordinal))
                .Select(f => (string)f.GetRawConstantValue()!).OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public void UnitWatch_passes_ok_line()
    {
        var (r, w) = Watched();
        var (idle, _) = Two(r, w);
        Assert.Equal("[NYAR:ok] cmd=event verb=unstick id=raid marked=1 fighting=1", Event(r, "unstick", "raid"));
        Assert.Equal(StuckReason.Admin, r.Ops.Watch.MarkOf(idle));
        Assert.Contains($"gate {ActionKind.UnstickUnits}", r.Ops.Calls);
    }

    [Fact]
    public void UnitWatch_empty_unwired_watch_refuses_with_state()
    {
        var r = Default();
        Assert.True(r.Ops.OpStartEvent("raid", null).Ok);
        Assert.Equal("[NYAR:err] cmd=event verb=unstick code=state arg=id", Event(r, "unstick", "raid"));
    }
}
