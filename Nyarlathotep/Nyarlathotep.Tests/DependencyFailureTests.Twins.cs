using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D12: a twin whose op throws answers one `code=io reason=internal` line and warns once per
/// failure streak; a file that cannot be saved answers code=io with its reason; memory follows what the file holds.</summary>
public partial class DependencyFailureTests
{
    static string Twin(Rig r, string verb, string id = "", string field = "", string value = "") => r.Flows.ApiEvent(r.Who, verb, id, field, value, "");

    [Theory]
    [InlineData("start", "raid", "", "", "start")]
    [InlineData("disable", "raid", "", "", "edit")]
    [InlineData("reload", "", "", "", "reload")]
    [InlineData("new", "fresh", "spawns", "", "author")]
    [InlineData("delete", "surge", "", "", "delete")]
    public void Twin_fails_when_op_throws(string verb, string id, string field, string value, string throwOn)
    {
        var r = Default();
        r.Ops.ThrowOn = throwOn;
        Assert.Equal($"[NYAR:err] cmd=event verb={verb} code=io reason=internal", Twin(r, verb, id, field, value));
        Assert.Equal([$"api event {verb} failed: InvalidOperationException"], r.Warns);
    }

    [Fact]
    public void Twin_fails_when_other_twin_op_throws()
    {
        var r = TemplateRig();
        r.Ops.Templates = Templates(r.Lib);
        r.Ops.ThrowOn = "template";
        Assert.Equal(["[NYAR:err] cmd=template verb=use code=io reason=internal"], r.Flows.ApiTemplate(r.Who, "use", "bandit-ambush", "", "", ""));
        r.Ops.ThrowOn = "pillar";
        Assert.Equal(["[NYAR:err] cmd=pillar verb=set code=io reason=internal"], r.Flows.ApiPillar(r.Who, "boss", "off", ""));
        r.Ops.ThrowOn = "purge";
        r.Ops.PurgeableUnits = 1;
        Assert.Equal("[NYAR:err] cmd=purge verb=ask code=io reason=internal", r.Flows.ApiPurge(r.Who, "", ""));
        Assert.Equal(["api template use failed: InvalidOperationException", "api pillar set failed: InvalidOperationException",
            "api purge ask failed: InvalidOperationException"], r.Warns);
    }

    [Fact]
    public void Twin_fails_when_streak_repeats()
    {
        var r = Default();
        r.Ops.ThrowOn = "start";
        for (var i = 0; i < 3; i++) Assert.EndsWith("code=io reason=internal", Twin(r, "start", "raid"));
        Assert.Single(r.Warns);                                                          // one line per streak
        Assert.Equal("[NYAR:err] cmd=event verb=stop code=state arg=id reason=not_active", Twin(r, "stop", "raid"));   // another command's run…
        Assert.EndsWith("code=io reason=internal", Twin(r, "start", "raid"));
        Assert.Single(r.Warns);                                                          // …does not end start's streak
    }

    [Fact]
    public void Twin_passes_throw_after_clean_run_logged_again()
    {
        var r = Default();
        r.Ops.ThrowOn = "start";
        Twin(r, "start", "raid");
        r.Ops.ThrowOn = null;
        Assert.Equal("[NYAR:ok] cmd=event verb=start id=raid", Twin(r, "start", "raid"));           // the streak ends here
        r.Ops.ThrowOn = "start";
        Twin(r, "start", "raid");
        Assert.Equal(2, r.Warns.Count);
    }

    [Fact]
    public void Twin_fails_when_save_fails()
    {
        var r = Default();
        var text = r.Lib.Text;
        r.Lib.Fs.FailWrites = true;
        var line = Twin(r, "disable", "raid");
        Assert.StartsWith("[NYAR:err] cmd=event verb=disable code=io ", line);
        Assert.Matches(@" reason=(save|read_only|write_uncertain)$", line);
        Assert.Equal(text, r.Lib.Text);
        Assert.True(r.Lib.Catalog.Current.Find("raid")!.Enabled);                         // memory still follows the file
        Assert.Empty(r.Warns);                                                           // a refused save is not an internal failure
    }

    [Fact]
    public void Twin_passes_memory_equals_file_after_refused_write()
    {
        var r = Default();
        r.Lib.Fs.ThrowAfterPromote = true;                                               // the file is replaced, then the write throws
        var line = Twin(r, "disable", "raid");
        Assert.StartsWith("[NYAR:", line);
        Assert.DoesNotContain('\n', line);
        var onDisk = new Library(r.Lib.Text).Catalog.Current.Find("raid")!.Enabled;
        Assert.Equal(onDisk, r.Lib.Catalog.Current.Find("raid")!.Enabled);
    }

    [Fact]
    public void Twin_passes_throw_after_change_is_internal()
    {
        // A16: the op changed state, then threw; the twin answers internal and nothing is rolled back
        var r = Default();
        r.Ops.ThrowAfterOn = "start";
        Assert.Equal("[NYAR:err] cmd=event verb=start code=io reason=internal", Twin(r, "start", "raid"));
        Assert.NotNull(r.Ops.Engine.Find("raid"));                                     // the event is running: re-read state
        Assert.Equal(["api event start failed: InvalidOperationException"], r.Warns);
        r.Ops.ThrowAfterOn = "purge";
        r.Ops.PurgeableUnits = 2;
        r.Flows.ApiPurge(r.Who, "", "");
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=io reason=internal", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.Null(r.Ops.Engine.Find("raid"));                                        // the purge ran
        Assert.NotNull(r.Ops.PurgeUntilUtc);
        r.Ops.ThrowAfterOn = "pillar";
        Assert.Equal(["[NYAR:err] cmd=pillar verb=set code=io reason=internal"], r.Flows.ApiPillar(r.Who, "boss", "off", ""));
        Assert.False(r.Ops.Pillars.Get(Pillar.Boss));                                  // the switch was saved
    }

    [Fact]
    public void Twin_empty_clean_run_warns_nothing()
    {
        var r = Default();
        Assert.Equal("[NYAR:ok] cmd=event verb=start id=raid", Twin(r, "start", "raid"));
        Assert.Empty(r.Warns);
    }
}
