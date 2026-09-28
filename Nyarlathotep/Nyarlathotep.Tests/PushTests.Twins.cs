using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D9: the start, stop, purge and pillar-off twins queue the event pushes their human commands
/// queue, and nothing when refused.</summary>
public partial class PushTests
{
    static List<string> TwinQueued(Rig r) => r.Lib.Hub.Queue.Lines.Select(l => l.Text).ToList();

    static List<string> Run(Action<Rig> act)
    {
        var r = Default();
        var before = TwinQueued(r).Count;
        act(r);
        return TwinQueued(r).Skip(before).ToList();
    }

    [Fact]
    public void Twins_passes_start_stop_purge_as_human()
    {
        var twin = Run(r =>
        {
            r.Flows.ApiEvent(r.Who, "start", "raid", "", "", "");
            r.Flows.ApiEvent(r.Who, "stop", "raid", "", "", "");
            r.Flows.ApiEvent(r.Who, "start", "raid", "", "", "");
            r.Ops.PurgeableUnits = 2;
            r.Flows.ApiPurge(r.Who, "", "");
            r.Flows.ApiPurge(r.Who, "confirm", "");
        });
        var human = Run(r =>
        {
            r.Flows.Start(r.Who, "raid");
            r.Flows.Stop(r.Who, "raid");
            r.Flows.Start(r.Who, "raid");
            r.Ops.PurgeableUnits = 2;
            r.Flows.PurgeAsk(r.Who);
            r.Flows.PurgeConfirm(r.Who);
        });
        Assert.Equal(human, twin);
        Assert.Contains(twin, l => l.Contains("type=event-start id=raid", StringComparison.Ordinal));
        Assert.Contains(twin, l => l.Contains("type=event-end id=raid", StringComparison.Ordinal));
        Assert.Contains(twin, l => l.Contains("type=killswitch", StringComparison.Ordinal));
    }

    [Fact]
    public void Twins_passes_pillar_off_ends_running()
    {
        var changed = Run(r => { r.Flows.Start(r.Who, "raid"); r.Flows.ApiPillar(r.Who, "spawns", "off", ""); });
        Assert.Contains(changed, l => l.Contains("type=event-end id=raid", StringComparison.Ordinal));
        var held = Run(r =>
        {
            r.Flows.Start(r.Who, "raid");
            r.Ops.Pillars.Set(Pillar.Spawns, false);                                     // switched off by hand: changed=0
            r.Flows.ApiPillar(r.Who, "spawns", "off", "");
        });
        Assert.Contains(held, l => l.Contains("type=event-end id=raid", StringComparison.Ordinal));
        Assert.DoesNotContain(held, l => l.Contains("type=config-changed", StringComparison.Ordinal));
    }

    [Fact]
    public void Twins_fails_when_refused()
    {
        Assert.Empty(Run(r => r.Flows.ApiEvent(r.Who, "stop", "raid", "", "", "")));                    // not active
        Assert.Empty(Run(r => r.Flows.ApiEvent(r.Who, "start", "ghost", "", "", "")));                  // notfound
        Assert.Empty(Run(r => { r.Ops.PurgeableUnits = 1; r.Flows.ApiPurge(r.Who, "confirm", ""); }));  // not asked
    }

    [Fact]
    public void Twins_empty_nothing_to_purge() => Assert.Empty(Run(r => r.Flows.ApiPurge(r.Who, "", "")));
}
