using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D9: a twin queues exactly the config-changed pushes its human command queues (one per
/// applied events.json write, one per pillar switch that changes the cfg), and a refusal or changed=0 queues none.</summary>
public partial class ConfigChangedTests
{
    static List<string> Queued(Rig r) => r.Lib.Hub.Queue.Lines.Select(l => l.Text).ToList();

    /// <summary>Runs one action on a fresh templated rig and returns what it queued.</summary>
    static List<string> After(Action<Rig> act)
    {
        var r = TemplateRig();
        r.Ops.Templates = Templates(r.Lib);
        var before = Queued(r).Count;
        act(r);
        return Queued(r).Skip(before).ToList();
    }

    static readonly Dictionary<string, (Action<Rig> Twin, Action<Rig> Human)> Pairs = new()
    {
        ["set"] = (r => r.Flows.ApiEvent(r.Who, "set", "raid", "durationSeconds", "900", ""), r => r.Flows.Set(r.Who, "raid", "durationSeconds", "900")),
        ["disable"] = (r => r.Flows.ApiEvent(r.Who, "disable", "raid", "", "", ""), r => r.Flows.Enable(r.Who, "raid", false)),
        ["reload"] = (r => r.Flows.ApiEvent(r.Who, "reload", "", "", "", ""), r => r.Flows.ReloadEvents(r.Who)),
        ["new"] = (r => r.Flows.ApiEvent(r.Who, "new", "fresh", "spawns", "", ""), r => r.Flows.New(r.Who, "fresh", "spawns")),
        ["copy"] = (r => r.Flows.ApiEvent(r.Who, "copy", "raid", "raid-2", "", ""), r => r.Flows.Copy(r.Who, "raid", "raid-2")),
        ["template use"] = (r => r.Flows.ApiTemplate(r.Who, "use", "bandit-ambush", "", "", ""), r => r.Flows.TemplateUse(r.Who, "bandit-ambush", null)),
        ["delete confirm"] = (r => { r.Flows.ApiEvent(r.Who, "delete", "raid", "", "", ""); r.Flows.ApiEvent(r.Who, "delete", "raid", "confirm", "", ""); },
            r => { r.Flows.DeleteEvent(r.Who, "raid", false); r.Flows.DeleteEvent(r.Who, "raid", true); }),
        ["pillar"] = (r => r.Flows.ApiPillar(r.Who, "boss", "off", ""), r => r.Flows.Pillar(r.Who, "boss", "off")),
    };

    [Theory]
    [InlineData("set")]
    [InlineData("disable")]
    [InlineData("reload")]
    [InlineData("new")]
    [InlineData("copy")]
    [InlineData("template use")]
    [InlineData("delete confirm")]
    [InlineData("pillar")]
    public void Twins_passes_same_pushes_as_human(string action)
    {
        var twinQueued = After(Pairs[action].Twin);
        Assert.Equal([ConfigChanged], twinQueued);
        Assert.Equal(twinQueued, After(Pairs[action].Human));
    }

    [Fact]
    public void Twins_fails_when_refused_or_unchanged()
    {
        Assert.Empty(After(r => r.Flows.ApiEvent(r.Who, "enable", "raid", "", "", "")));                 // changed=0
        Assert.Empty(After(r => r.Flows.ApiPillar(r.Who, "spawns", "on", "")));                         // changed=0, nothing running
        Assert.Empty(After(r => r.Flows.ApiEvent(r.Who, "set", "ghost", "durationSeconds", "900", ""))); // notfound
        Assert.Empty(After(r => r.Flows.ApiEvent(r.Who, "new", "raid", "spawns", "", "")));              // exists
        Assert.Empty(After(r => { r.Lib.Fs.FailWrites = true; r.Flows.ApiEvent(r.Who, "disable", "raid", "", "", ""); }));   // save failed
        Assert.Empty(After(r => { r.Ops.Pillars.WriteThenThrow = true; r.Flows.ApiPillar(r.Who, "boss", "off", ""); }));   // cfg save failed
    }

    [Fact]
    public void Twins_passes_changed_pillar_one_notice()
    {
        Assert.Equal([ConfigChanged], After(r => r.Flows.ApiPillar(r.Who, "zones", "off", "")));
        Assert.Equal([ConfigChanged], After(r => r.Flows.Pillar(r.Who, "zones", "off")));
    }

    [Fact]
    public void Twins_empty_reads_queue_nothing()
    {
        Assert.Empty(After(r =>
        {
            r.Flows.ApiTemplates("", "", "");
            r.Flows.ApiTemplate(r.Who, "info", "bandit-ambush", "", "", "");
            r.Flows.ApiPillar(r.Who, "list", "", "");
            r.Flows.ApiKillSwitch("");
        }));
    }
}
