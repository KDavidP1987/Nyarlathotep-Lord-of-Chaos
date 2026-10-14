using System.Text;
using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D12: `.nyar event unstick &lt;id&gt;` through the admin flow, the gateway and the watch phase marks every
/// live unit of the running instance that has not fought in the last StuckSeconds ("admin"), replies with its counts, and
/// refuses with one line, changing nothing, for an unknown id, an event not running, one with no live unit or the watch off;
/// docs/NYARLATHOTEP_DESIGN.md §6 lists the command.</summary>
public partial class EventAdminTests
{
    /// <summary>raid running in a rig whose `event unstick` reaches a watch over <c>W</c>.</summary>
    static (Rig R, WatchWorld W, ActiveEvent A) Unstickable()
    {
        var r = Default();
        var w = new WatchWorld();
        r.Ops.WatchWorld = w;
        Assert.True(r.Ops.OpStartEvent("raid", null).Ok);
        return (r, w, r.Ops.Engine.Find("raid")!);
    }

    /// <summary>An idle unit, one that hit a player 30 s ago and one holding a player 3 m away.</summary>
    static (long Idle, long Hit, long Close) Three(Rig r, WatchWorld w)
    {
        var idle = w.Add("raid", r.Ops.Now, 500, 500);
        var hit = w.Add("raid", r.Ops.Now, 520, 500);
        var close = w.Add("raid", r.Ops.Now, 540, 500);
        Assert.True(r.Ops.Watch.Fights.Report(hit, true, r.Ops.Now.AddSeconds(-30), w.Ledger.IsTracked));
        w.Aim(close, 3f);
        return (idle, hit, close);
    }

    static int WatchLines(Rig r) => r.Lib.Log.Count("unit-watch");

    static string DesignText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));

    /// <summary>What keeps design §6 from listing the command; empty when it does.</summary>
    internal static List<string> UnstickDesignProblems(string design)
    {
        var p = new List<string>();
        if (!design.Contains("| `.nyar event unstick <id>` | admin |", StringComparison.Ordinal)) p.Add("§6 has no unstick row");
        return p;
    }

    [Fact]
    public void UnitWatch_fails_when_a_fighting_unit_is_marked()
    {
        var (r, w, _) = Unstickable();
        var (_, hit, close) = Three(r, w);
        var o = r.Flows.Unstick(r.Who, "raid");
        Assert.Equal("2", o.Field("fighting"));
        Assert.Null(r.Ops.Watch.MarkOf(hit));
        Assert.Null(r.Ops.Watch.MarkOf(close));
    }

    [Fact]
    public void UnitWatch_fails_when_an_idle_unit_is_not_marked()
    {
        var (r, w, _) = Unstickable();
        var (idle, _, _) = Three(r, w);
        var o = r.Flows.Unstick(r.Who, "raid");
        Assert.Equal("1", o.Field("marked"));
        Assert.Equal(StuckReason.Admin, r.Ops.Watch.MarkOf(idle));
        Assert.Equal(1, r.Lib.Log.Count("unit-watch raid stuck: CHAR_Spider_Forest wave 1 (admin)"));
        // a second unstick finds it marked already: neither marked nor fighting
        Assert.Equal("unstick raid: 0 units marked, 2 fighting left alone", r.Flows.Unstick(r.Who, "raid").Human);
    }

    [Fact]
    public void UnitWatch_fails_when_a_reply_exceeds_480_bytes()
    {
        var (r, w, _) = Unstickable();
        for (var i = 0; i < 60; i++) w.Add("raid", r.Ops.Now, 100 + 10 * i, 500);
        var replies = new List<string> { r.Flows.Unstick(r.Who, "raid").Human, r.Flows.Unstick(r.Who, "nosuch").Human, r.Flows.Unstick(r.Who, "surge").Human,
            r.Flows.Unstick(r.Who, "").Human, r.Flows.Unstick(r.Who, "raid", "now").Human, UnitWatch.Off, UnitWatchLines.NoLiveUnit(new string('x', 64)) };
        Assert.All(replies, h => Assert.InRange(Encoding.UTF8.GetByteCount(h), 1, Wire.MaxBytes));
        Assert.Equal("unstick raid: 60 units marked, 0 fighting left alone", replies[0]);
    }

    [Fact]
    public void UnitWatch_fails_when_a_refusal_changes_anything()
    {
        var (r, w, _) = Unstickable();
        var o = r.Flows.Unstick(r.Who, "nosuch");
        Assert.Equal((false, RefusalCode.NotFound, "id", "unknown event nosuch"), (o.Ok, o.Code!.Value, o.Arg, o.Human));
        o = r.Flows.Unstick(r.Who, "surge");                       // defined, not running
        Assert.Equal((RefusalCode.State, Reasons.NotActive, "unstick surge: not running"), (o.Code!.Value, o.Reason, o.Human));
        o = r.Flows.Unstick(r.Who, "raid");                        // running, no live unit yet
        Assert.Equal((RefusalCode.State, "id", "unstick raid: no live unit"), (o.Code!.Value, o.Arg, o.Human));
        var (idle, _, _) = Three(r, w);
        r.Ops.WatchSettings = WatchSettings.Default with { Enabled = false };
        o = r.Flows.Unstick(r.Who, "raid");
        Assert.Equal((RefusalCode.Invalid, Reasons.WatchOff, UnitWatch.Off), (o.Code!.Value, o.Reason, o.Human));
        Assert.Null(r.Ops.Watch.MarkOf(idle));
        Assert.Equal(0, r.Ops.Watch.Rows("raid"));
        Assert.Equal(0, WatchLines(r));
        Assert.Empty(w.Moves);
    }

    [Fact]
    public void UnitWatch_fails_when_a_missing_id_or_surplus_word_runs()
    {
        var (r, w, _) = Unstickable();
        var (idle, _, _) = Three(r, w);
        foreach (var (id, extra, arg) in new[] { ("", "", "id"), ("raid", "now", "extra") })
        {
            var o = r.Flows.Unstick(r.Who, id, extra);
            Assert.Equal((RefusalCode.BadArg, arg, UnitWatchLines.Usage), (o.Code!.Value, o.Arg, o.Human));
        }
        Assert.Null(r.Ops.Watch.MarkOf(idle));
        Assert.DoesNotContain(r.Ops.Calls, c => c.StartsWith("unstick", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitWatch_fails_when_the_design_row_is_missing()
    {
        Assert.Empty(UnstickDesignProblems(DesignText));
        Assert.Equal(["§6 has no unstick row"], UnstickDesignProblems(DesignText.Replace("`.nyar event unstick <id>`", "`.nyar event unstuck <id>`", StringComparison.Ordinal)));
    }

    [Fact]
    public void UnitWatch_passes_unstick_marks_the_idle_units()
    {
        var (r, w, _) = Unstickable();
        Three(r, w);
        var o = r.Flows.Unstick(r.Who, "raid");
        Assert.True(o.Ok);
        Assert.Equal("unstick raid: 1 units marked, 2 fighting left alone", o.Human);
        Assert.Contains($"gate {ActionKind.UnstickUnits}", r.Ops.Calls);
        Assert.Contains("unstick raid", r.Ops.Calls);
        Assert.Equal(1, r.Lib.Log.Count("event unstick raid"));
        Assert.True(ActionTable.Allows(ActionKind.UnstickUnits, Actor.Admin, true));
        Assert.False(ActionTable.Allows(ActionKind.UnstickUnits, Actor.Player, true));
        Assert.False(ActionTable.Allows(ActionKind.UnstickUnits, Actor.System, true));
    }

    [Fact]
    public void UnitWatch_empty_all_fighting_marks_none()
    {
        var (r, w, _) = Unstickable();
        var (idle, _, _) = Three(r, w);
        w.Aim(idle, 4f);
        Assert.Equal("unstick raid: 0 units marked, 3 fighting left alone", r.Flows.Unstick(r.Who, "raid").Human);
        Assert.Equal(0, WatchLines(r));
    }
}
