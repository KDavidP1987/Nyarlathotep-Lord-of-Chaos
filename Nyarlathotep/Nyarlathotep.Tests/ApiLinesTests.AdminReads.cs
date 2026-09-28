using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D7: the admin reads `templates`, `template info`, `pillar list` and `killswitch`, with the
/// keys of contract §3 in order.</summary>
public partial class ApiLinesTests
{
    static readonly string[] TplKeys = ["id", "pillar", "trigger", "duration", "summary"];

    static TemplateCatalog Catalogue() => TemplateLibraryTests.Real();

    [Fact]
    public void AdminReads_passes_templates_rows()
    {
        var catalog = Catalogue();
        var lines = ApiLines.Templates(catalog, "", "");
        Assert.Equal(catalog.Templates.Count + 1, lines.Count);
        Assert.All(lines.SkipLast(1), l => Assert.Equal(TplKeys, Keys(l)));
        Assert.Equal($"[NYAR:end] cmd=templates page=1/1 count={catalog.Templates.Count}", lines[^1]);
        var first = catalog.Templates[0];
        Assert.Equal(Wire.Tpl(first.Id, PillarNames.Name(first.Definition.Pillar), ApiLines.Trigger(first.Definition.Trigger.Type),
            first.Definition.DurationSeconds, first.Definition.Name), lines[0]);
        Assert.Equal(lines, ApiLines.Templates(catalog, "1", ""));

        var spawns = catalog.Templates.Count(t => t.Definition.Pillar == Pillar.Spawns);
        var filtered = ApiLines.Templates(catalog, "spawns", "");
        Assert.Equal(spawns, filtered.Count - 1);
        Assert.All(filtered.SkipLast(1), l => Assert.Equal("spawns", Value(l, "pillar")));
        Assert.Equal(filtered, ApiLines.Templates(catalog, "spawns", "1"));
    }

    [Fact]
    public void AdminReads_passes_template_info()
    {
        var catalog = Catalogue();
        var t = catalog.Templates[^1];
        Assert.Equal([ApiLines.Tpl(t), "[NYAR:end] cmd=template count=1"], ApiLines.TemplateInfo(catalog, t.Id));
    }

    [Fact]
    public void AdminReads_passes_pillar_list_in_order()
    {
        var on = new HashSet<Pillar> { Pillar.Spawns, Pillar.Sieges };
        Assert.Equal(
        [
            "[NYAR:pillar] id=empowerment on=0", "[NYAR:pillar] id=spawns on=1", "[NYAR:pillar] id=boss on=0",
            "[NYAR:pillar] id=zones on=0", "[NYAR:pillar] id=sieges on=1", "[NYAR:end] cmd=pillar count=5",
        ], ApiLines.Pillars(on.Contains));
    }

    [Fact]
    public void AdminReads_passes_killswitch_during_cooldown()
    {
        Assert.Equal(["[NYAR:ks] on=1 secs=240 events=2 units=12", "[NYAR:end] cmd=killswitch count=1"],
            ApiLines.KillSwitch(Now.AddSeconds(239.2), 2, 12, Now));
    }

    [Fact]
    public void AdminReads_fails_when_cooldown_is_over()
    {
        Assert.Equal("[NYAR:ks] on=0 secs=0 events=0 units=3", ApiLines.KillSwitch(Now, 0, 3, Now)[0]);
        Assert.Equal("[NYAR:ks] on=0 secs=0 events=0 units=3", ApiLines.KillSwitch(Now.AddSeconds(-5), 0, 3, Now)[0]);
    }

    [Fact]
    public void AdminReads_fails_when_catalogue_unavailable()
    {
        var down = TemplateCatalog.Unavailable("templates.json is missing");
        Assert.Equal(["[NYAR:err] cmd=templates code=io reason=read"], ApiLines.Templates(down, "", ""));
        Assert.Equal(["[NYAR:err] cmd=templates code=io reason=read"], ApiLines.Templates(down, "spawns", "2"));
        Assert.Equal(["[NYAR:err] cmd=template code=io reason=read"], ApiLines.TemplateInfo(down, "bandit-ambush"));
    }

    [Fact]
    public void AdminReads_fails_when_filter_or_page_is_bad()
    {
        var catalog = Catalogue();
        Assert.Equal(["[NYAR:err] cmd=templates code=notfound arg=pillar"], ApiLines.Templates(catalog, "nope", ""));
        Assert.Equal(["[NYAR:err] cmd=templates code=notfound arg=pillar"], ApiLines.Templates(catalog, "nope", "1"));
        Assert.Equal(["[NYAR:err] cmd=templates code=badarg arg=page"], ApiLines.Templates(catalog, "spawns", "0"));
        Assert.Equal([$"[NYAR:end] cmd=templates page=9/1 count={catalog.Templates.Count}"], ApiLines.Templates(catalog, "9", ""));
        Assert.Equal(["[NYAR:err] cmd=template code=notfound arg=template"], ApiLines.TemplateInfo(catalog, "ghost"));
        Assert.Equal(["[NYAR:err] cmd=template code=badarg arg=template"], ApiLines.TemplateInfo(catalog, ""));
    }

    [Fact]
    public void AdminReads_empty_no_templates_no_events()
    {
        var none = TemplateCatalog.Load(TemplateLibraryTests.Bytes("{ \"SchemaVersion\": 1, \"events\": [] }"), FakeUnits.Default(), FakeUnits.Default());
        Assert.Null(none.Error);
        Assert.Equal(["[NYAR:end] cmd=templates page=1/1 count=0"], ApiLines.Templates(none, "", ""));
        Assert.Equal(["[NYAR:ks] on=0 secs=0 events=0 units=0", "[NYAR:end] cmd=killswitch count=1"], ApiLines.KillSwitch(null, 0, 0, Now));
    }

    [Fact]
    public void AdminReads_passes_through_the_flows()
    {
        var r = HumanReplyTests.TemplateRig();
        r.Ops.Templates = Catalogue();
        r.Ops.TrackedUnits = 7;
        Assert.Equal(ApiLines.Templates(r.Ops.Templates, "boss", ""), r.Flows.ApiTemplates("boss", "", ""));
        Assert.Equal(["[NYAR:err] cmd=templates code=badarg arg=extra"], r.Flows.ApiTemplates("boss", "1", "x"));
        Assert.Equal(ApiLines.TemplateInfo(r.Ops.Templates, "bandit-ambush"), r.Flows.ApiTemplate(r.Who, "info", "bandit-ambush", "", "", ""));
        Assert.Equal(["[NYAR:err] cmd=template code=badarg arg=extra"], r.Flows.ApiTemplate(r.Who, "info", "bandit-ambush", "x", "", ""));
        Assert.Equal(6, r.Flows.ApiPillar(r.Who, "list", "", "").Count);
        Assert.Equal(["[NYAR:err] cmd=pillar code=badarg arg=extra"], r.Flows.ApiPillar(r.Who, "list", "x", ""));
        Assert.Equal("[NYAR:ks] on=0 secs=0 events=0 units=7", r.Flows.ApiKillSwitch("")[0]);
        Assert.Equal(["[NYAR:err] cmd=killswitch code=badarg arg=extra"], r.Flows.ApiKillSwitch("x"));
        Assert.Equal(0, r.Lib.Log.Count("ran "));                                                   // reads log nothing
    }
}
