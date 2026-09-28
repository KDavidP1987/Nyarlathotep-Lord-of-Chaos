using System.Text.RegularExpressions;
using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D3-D6: the `.nyar api` twins over the flows and the fakes. Each twin answers one line; it
/// reaches the fake op only after the gateway ran its verb's kind (the rig's gateway records "gate &lt;kind&gt;" into
/// Ops.Calls); its log line is the human command's with "api " in front; the rate gate runs before everything.</summary>
public class ApiTwinTests
{
    static string Event(Rig r, string verb, string id = "", string field = "", string value = "", string extra = "") =>
        r.Flows.ApiEvent(r.Who, verb, id, field, value, extra);

    static Rig Templated()
    {
        var r = TemplateRig();
        r.Ops.Templates = Templates(r.Lib);
        return r;
    }

    /// <summary>The keys of a wire line, in order.</summary>
    static List<string> Keys(string line) => line.Split(' ').Skip(1).Select(t => t[..t.IndexOf('=')]).ToList();

    static int Logged(Rig r) => r.Lib.Log.Count("ran ");

    // ---- D3 event twins

    [Fact]
    public void Event_passes_each_verb_ok_line()
    {
        var r = Default();
        Assert.Equal("[NYAR:ok] cmd=event verb=disable id=raid changed=1", Event(r, "disable", "raid"));
        Assert.Equal("[NYAR:ok] cmd=event verb=enable id=raid changed=1", Event(r, "enable", "raid"));
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=durationSeconds value=1200", Event(r, "set", "raid", "durationSeconds", "1200"));
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=name value=Night_raid", Event(r, "set", "raid", "name", "Night raid"));
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=location value=5.0,5.0", Event(r, "set", "raid", "location", "here"));
        Assert.Equal("[NYAR:ok] cmd=event verb=start id=raid", Event(r, "start", "raid"));
        Assert.Equal("[NYAR:ok] cmd=event verb=stop id=raid", Event(r, "stop", "raid"));
        Assert.Equal("[NYAR:ok] cmd=event verb=reload id=- count=2", Event(r, "reload"));
        Assert.Equal("[NYAR:ok] cmd=event verb=new id=fresh pillar=spawns", Event(r, "new", "fresh", "spawns"));
        Assert.Equal("[NYAR:ok] cmd=event verb=copy id=raid-2 from=raid", Event(r, "copy", "raid", "raid-2"));
        Assert.Equal("[NYAR:ok] cmd=event verb=delete id=raid-2 confirm=30", Event(r, "delete", "raid-2"));
        Assert.Equal("[NYAR:ok] cmd=event verb=delete id=raid-2 done=1", Event(r, "delete", "raid-2", "confirm"));
    }

    public static TheoryData<string, string[], string> GatedVerbs() => new()
    {
        { "start", ["raid"], "start raid" },
        { "stop", ["raid"], "stop raid" },
        { "disable", ["raid"], "edit raid enabled" },
        { "set", ["raid", "durationSeconds", "900"], "edit raid durationSeconds" },
        { "reload", [], "reload" },
        { "new", ["fresh", "spawns"], "author" },
        { "copy", ["raid", "raid-2"], "author" },
        { "delete", ["surge"], "delete surge" },
    };

    [Theory]
    [MemberData(nameof(GatedVerbs))]
    public void Event_passes_gateway_kind_before_op(string verb, string[] args, string op)
    {
        var r = Default();
        if (verb == "stop") r.Flows.Start(r.Who, "raid");
        r.Ops.Calls.Clear();
        var a = args.Concat(Enumerable.Repeat("", 3)).ToArray();
        Assert.StartsWith("[NYAR:ok] ", Event(r, verb, a[0], a[1], a[2]));
        var kind = AdminFlows.Kinds[$"event {verb}"];
        var at = r.Ops.Calls.IndexOf(op);
        Assert.True(at > 0, string.Join(" | ", r.Ops.Calls));
        Assert.Equal($"gate {kind}", r.Ops.Calls[at - 1]);
        // the human command runs the same kind: one table, one flow
        var human = Default();
        if (verb == "stop") human.Flows.Start(human.Who, "raid");
        human.Ops.Calls.Clear();
        _ = verb switch
        {
            "start" => human.Flows.Start(human.Who, "raid"),
            "stop" => human.Flows.Stop(human.Who, "raid"),
            "disable" => human.Flows.Enable(human.Who, "raid", false),
            "set" => human.Flows.Set(human.Who, "raid", "durationSeconds", "900"),
            "reload" => human.Flows.ReloadEvents(human.Who),
            "new" => human.Flows.New(human.Who, "fresh", "spawns"),
            "copy" => human.Flows.Copy(human.Who, "raid", "raid-2"),
            _ => human.Flows.DeleteEvent(human.Who, "surge", false),
        };
        Assert.Equal(r.Ops.Calls, human.Ops.Calls);
    }

    [Fact]
    public void Event_passes_log_line_prefixed()
    {
        var r = Default();
        Event(r, "start", "raid");
        r.Flows.Stop(r.Who, "raid");
        Assert.Contains(r.Lib.Log.Lines, l => l.EndsWith("ran api event start raid", StringComparison.Ordinal));
        Assert.Contains(r.Lib.Log.Lines, l => l.EndsWith("ran event stop raid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("list", "list")]
    [InlineData("info", "info")]
    [InlineData("launch", "launch")]
    [InlineData("START", "START")]
    public void Event_fails_when_verb_unknown(string verb, string echoed)
    {
        var r = Default();
        Assert.Equal($"[NYAR:err] cmd=event verb={echoed} code=badarg arg=verb", Event(r, verb, "raid"));
        Assert.Equal(0, Logged(r));
    }

    [Theory]
    [InlineData("start", "", "", "", "", "id")]
    [InlineData("stop", "raid", "x", "", "", "extra")]
    [InlineData("enable", "raid", "", "", "x", "extra")]
    [InlineData("set", "raid", "", "", "", "field")]
    [InlineData("set", "raid", "durationSeconds", "", "", "value")]
    [InlineData("set", "raid", "durationSeconds", "900", "x", "extra")]
    [InlineData("set", "raid", "nofield", "1", "", "field")]
    [InlineData("set", "raid", "durationSeconds", "5", "", "value")]
    [InlineData("reload", "raid", "", "", "", "extra")]
    [InlineData("new", "fresh", "", "", "", "pillar")]
    [InlineData("new", "Bad Id", "spawns", "", "", "id")]
    [InlineData("copy", "raid", "", "", "", "newId")]
    [InlineData("copy", "raid", "Bad!", "", "", "newId")]
    [InlineData("copy", "Bad!", "fresh", "", "", "id")]
    [InlineData("delete", "raid", "maybe", "", "", "confirm")]
    [InlineData("delete", "raid", "confirm", "x", "", "extra")]
    [InlineData("delete", "Bad!", "", "", "", "id")]
    [InlineData("delete", "Bad!", "confirm", "", "", "id")]
    [InlineData("start", "Bad!", "", "", "", "id")]
    [InlineData("copy", "Bad!", "fresh", "x", "", "extra")]                 // the argument count before the id check
    [InlineData("delete", "Bad!", "confirm", "x", "", "extra")]
    [InlineData("delete", "raid", "maybe", "x", "", "extra")]
    public void Event_fails_when_argument_bad(string verb, string id, string field, string value, string extra, string arg)
    {
        var r = Default();
        r.Ops.Calls.Clear();
        var line = Event(r, verb, id, field, value, extra);
        Assert.Equal($"[NYAR:err] cmd=event verb={verb} code=badarg arg={arg}", line);
        Assert.Equal(0, Logged(r));                                      // a badarg never reaches the log callback
        Assert.Empty(r.Ops.Calls);
    }

    [Fact]
    public void Event_fails_when_human_badarg_would_log()
    {
        var r = Templated();
        Assert.Equal(RefusalCode.BadArg, r.Flows.New(r.Who, "Bad Id", "spawns").Code);
        Assert.Equal(RefusalCode.BadArg, r.Flows.Copy(r.Who, "raid", "Bad!").Code);
        Assert.Equal(RefusalCode.BadArg, r.Flows.TemplateUse(r.Who, "bandit-ambush", "Bad!").Code);
        Assert.Equal(RefusalCode.BadArg, r.Flows.Pillar(r.Who, "spawns", "maybe").Code);
        Assert.Equal(0, Logged(r));
        // an unknown pillar answers first, and is logged as in 0.5.1
        Assert.Equal((RefusalCode.NotFound, "pillar"), (r.Flows.New(r.Who, "Bad Id", "nope").Code!.Value, r.Flows.New(r.Who, "Bad Id", "nope").Arg));
        Assert.Equal(RefusalCode.NotFound, r.Flows.Pillar(r.Who, "nope", "maybe").Code);
    }

    [Fact]
    public void Event_fails_when_refused()
    {
        var r = Default();
        Assert.Equal("[NYAR:err] cmd=event verb=start code=notfound arg=id", Event(r, "start", "ghost"));
        Assert.Equal("[NYAR:ok] cmd=event verb=start id=raid", Event(r, "start", "raid"));
        var twice = Event(r, "start", "raid");
        Assert.Equal("[NYAR:err] cmd=event verb=start code=state arg=id reason=already_active", twice);
        Assert.Equal(["cmd", "verb", "code", "arg", "reason"], Keys(twice));
        Assert.Equal("[NYAR:err] cmd=event verb=delete code=state arg=id reason=running", Event(r, "delete", "raid"));
        Assert.Equal("[NYAR:err] cmd=event verb=new code=exists arg=id", Event(r, "new", "raid", "spawns"));
        Assert.Equal("[NYAR:err] cmd=event verb=new code=notfound arg=pillar", Event(r, "new", "fresh", "nope"));
    }

    [Fact]
    public void Event_empty_arguments()
    {
        var r = Default();
        Assert.Equal("[NYAR:err] cmd=event verb=- code=badarg arg=verb", Event(r, ""));
        Assert.Equal(0, Logged(r));
    }

    // ---- D4 template, pillar and purge twins

    [Fact]
    public void TemplatePillarPurge_passes_ok_lines()
    {
        var r = Templated();
        Assert.Equal(["[NYAR:ok] cmd=template verb=use id=bandit-ambush tpl=bandit-ambush"], r.Flows.ApiTemplate(r.Who, "use", "bandit-ambush", "", "", ""));
        Assert.Equal(["[NYAR:ok] cmd=template verb=use id=ambush-2 tpl=bandit-ambush"], r.Flows.ApiTemplate(r.Who, "use", "bandit-ambush", "as", "ambush-2", ""));
        Assert.Equal(["[NYAR:ok] cmd=pillar verb=set id=boss on=0 changed=1"], r.Flows.ApiPillar(r.Who, "boss", "off", ""));
        r.Flows.Start(r.Who, "raid");
        Assert.Equal(["[NYAR:ok] cmd=pillar verb=set id=spawns on=0 changed=1 ended=1"], r.Flows.ApiPillar(r.Who, "spawns", "off", ""));
        Assert.Equal(["[NYAR:ok] cmd=pillar verb=set id=spawns on=1 changed=1"], r.Flows.ApiPillar(r.Who, "spawns", "on", ""));
        r.Ops.PurgeableUnits = 3;
        Assert.Equal("[NYAR:ok] cmd=purge verb=ask id=- confirm=30", r.Flows.ApiPurge(r.Who, "", ""));
        Assert.Equal("[NYAR:ok] cmd=purge verb=confirm id=- events=0 units=3 secs=300", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.Contains(r.Lib.Log.Lines, l => l.EndsWith("ran api template use bandit-ambush as ambush-2", StringComparison.Ordinal));
        Assert.Contains(r.Lib.Log.Lines, l => l.EndsWith("ran api pillar spawns off", StringComparison.Ordinal));
        Assert.Contains(r.Lib.Log.Lines, l => l.EndsWith("ran api purge confirm", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Lib.Log.Lines, l => l.EndsWith("ran api purge", StringComparison.Ordinal));     // the ask logs nothing
    }

    [Theory]
    [InlineData("template", "list", "", "", "", "", "[NYAR:err] cmd=template verb=list code=badarg arg=verb")]
    [InlineData("template", "use", "", "", "", "", "[NYAR:err] cmd=template verb=use code=badarg arg=template")]
    [InlineData("template", "use", "bandit-ambush", "like", "", "", "[NYAR:err] cmd=template verb=use code=badarg arg=extra")]
    [InlineData("template", "use", "bandit-ambush", "as", "", "", "[NYAR:err] cmd=template verb=use code=badarg arg=id")]
    [InlineData("template", "use", "bandit-ambush", "as", "x", "y", "[NYAR:err] cmd=template verb=use code=badarg arg=extra")]
    [InlineData("template", "use", "bandit-ambush", "as", "Bad!", "", "[NYAR:err] cmd=template verb=use code=badarg arg=id")]
    [InlineData("template", "use", "ghost", "", "", "", "[NYAR:err] cmd=template verb=use code=notfound arg=template")]
    [InlineData("pillar", "", "", "", "", "", "[NYAR:err] cmd=pillar verb=set code=badarg arg=pillar")]
    [InlineData("pillar", "spawns", "", "", "", "", "[NYAR:err] cmd=pillar verb=set code=badarg arg=state")]
    [InlineData("pillar", "spawns", "maybe", "", "", "", "[NYAR:err] cmd=pillar verb=set code=badarg arg=state")]
    [InlineData("pillar", "spawns", "on", "x", "", "", "[NYAR:err] cmd=pillar verb=set code=badarg arg=extra")]
    [InlineData("pillar", "nope", "on", "", "", "", "[NYAR:err] cmd=pillar verb=set code=notfound arg=pillar")]
    [InlineData("purge", "maybe", "", "", "", "", "[NYAR:err] cmd=purge verb=maybe code=badarg arg=confirm")]
    [InlineData("purge", "confirm", "x", "", "", "", "[NYAR:err] cmd=purge verb=confirm code=badarg arg=extra")]
    [InlineData("purge", "maybe", "x", "", "", "", "[NYAR:err] cmd=purge verb=maybe code=badarg arg=extra")]
    public void TemplatePillarPurge_fails_when_argument_bad(string cmd, string a, string b, string c, string d, string e, string expected)
    {
        var r = Templated();
        r.Ops.Calls.Clear();
        var lines = cmd switch
        {
            "template" => r.Flows.ApiTemplate(r.Who, a, b, c, d, e),
            "pillar" => r.Flows.ApiPillar(r.Who, a, b, c),
            _ => [r.Flows.ApiPurge(r.Who, a, b)],
        };
        Assert.Equal([expected], lines);
        if (!expected.Contains("code=badarg", StringComparison.Ordinal)) return;
        Assert.Equal(0, Logged(r));                                      // a badarg never reaches the log callback
        Assert.Empty(r.Ops.Calls);
    }

    [Fact]
    public void TemplatePillarPurge_fails_when_pillar_save_fails()
    {
        var r = Default();
        r.Ops.Pillars.WriteThenThrow = true;
        var line = Assert.Single(r.Flows.ApiPillar(r.Who, "boss", "off", ""));
        Assert.StartsWith("[NYAR:err] cmd=pillar verb=set code=io reason=save", line);
        r.Ops.Pillars.WriteThenThrow = false;
        r.Ops.Pillars.TruncateThenThrow = true;
        Assert.StartsWith("[NYAR:err] cmd=pillar verb=set code=io", Assert.Single(r.Flows.ApiPillar(r.Who, "empowerment", "off", "")));
    }

    [Fact]
    public void TemplatePillarPurge_fails_when_purge_confirm_lacks_cooldown()
    {
        var r = Default();
        r.Ops.PurgeableUnits = 2;
        r.Ops.CooldownSeconds = 120;
        r.Flows.ApiPurge(r.Who, "", "");
        var line = r.Flows.ApiPurge(r.Who, "confirm", "");
        Assert.Equal(["cmd", "verb", "id", "events", "units", "secs"], Keys(line));
        Assert.EndsWith(" secs=120", line);
    }

    [Fact]
    public void TemplatePillarPurge_empty_nothing_to_purge()
    {
        var r = Default();
        Assert.Equal("[NYAR:err] cmd=purge verb=ask code=state reason=nothing_to_purge", r.Flows.ApiPurge(r.Who, "", ""));
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=state reason=nothing_to_purge", r.Flows.ApiPurge(r.Who, "confirm", ""));
    }

    // ---- D5 idempotency and two-step confirm

    [Fact]
    public void Idempotency_passes_held_enable_writes_nothing()
    {
        var r = Default();
        var (hash, bak, notices) = (r.Lib.Hash, r.Lib.Bak, r.Lib.ConfigChanged);
        Assert.Equal("[NYAR:ok] cmd=event verb=enable id=raid changed=0", Event(r, "enable", "raid"));
        Assert.Equal("event raid enabled", r.Flows.Enable(r.Who, "raid", true).Human);              // 0.5.1's reply, unchanged
        Assert.Equal((hash, bak, notices), (r.Lib.Hash, r.Lib.Bak, r.Lib.ConfigChanged));
        Assert.Equal("[NYAR:ok] cmd=event verb=disable id=raid changed=1", Event(r, "disable", "raid"));
        (hash, notices) = (r.Lib.Hash, r.Lib.ConfigChanged);
        Assert.Equal("[NYAR:ok] cmd=event verb=disable id=raid changed=0", Event(r, "disable", "raid"));
        Assert.Equal((hash, notices), (r.Lib.Hash, r.Lib.ConfigChanged));
    }

    [Fact]
    public void Idempotency_passes_held_pillar_writes_nothing()
    {
        var r = Default();
        var (saves, file) = (r.Ops.Pillars.Saves, string.Join("\n", r.Ops.Pillars.File));
        Assert.Equal(["[NYAR:ok] cmd=pillar verb=set id=spawns on=1 changed=0"], r.Flows.ApiPillar(r.Who, "spawns", "on", ""));
        Assert.Equal((saves, file), (r.Ops.Pillars.Saves, string.Join("\n", r.Ops.Pillars.File)));
    }

    [Fact]
    public void Idempotency_passes_held_off_ends_running()
    {
        var r = Default();
        Assert.True(r.Flows.Start(r.Who, "raid").Ok);
        r.Ops.Pillars.Set(Pillar.Spawns, false);                      // an operator switched it off by hand
        var saves = r.Ops.Pillars.Saves;
        Assert.Equal(["[NYAR:ok] cmd=pillar verb=set id=spawns on=0 changed=0 ended=1"], r.Flows.ApiPillar(r.Who, "spawns", "off", ""));
        Assert.Null(r.Ops.Engine.Find("raid"));
        Assert.Equal(saves, r.Ops.Pillars.Saves);
    }

    [Fact]
    public void Idempotency_passes_set_to_held_value_writes()
    {
        var r = Default();
        var notices = r.Lib.ConfigChanged;
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=durationSeconds value=600", Event(r, "set", "raid", "durationSeconds", "600"));
        Assert.Equal(notices + 1, r.Lib.ConfigChanged);                // 0.5.1 wrote and pushed here too (A5)
        Assert.NotNull(r.Lib.Bak);
    }

    [Fact]
    public void Idempotency_fails_when_start_or_stop_repeats()
    {
        var r = Default();
        Assert.Equal("[NYAR:err] cmd=event verb=stop code=state arg=id reason=not_active", Event(r, "stop", "raid"));
        Event(r, "start", "raid");
        Assert.Equal("[NYAR:err] cmd=event verb=start code=state arg=id reason=already_active", Event(r, "start", "raid"));
    }

    [Fact]
    public void Idempotency_fails_when_confirm_is_not_the_askers()
    {
        var r = Default();
        var other = r.Who with { Id = 8, Name = "Other" };
        Event(r, "delete", "surge");
        Assert.Equal("[NYAR:err] cmd=event verb=delete code=confirm", r.Flows.ApiEvent(other, "delete", "surge", "confirm", "", ""));
        r.Lib.Fs.Now = r.Lib.Fs.Now.AddSeconds(31);
        Assert.Equal("[NYAR:err] cmd=event verb=delete code=confirm", Event(r, "delete", "surge", "confirm"));
        Event(r, "delete", "surge");
        Assert.Equal("[NYAR:ok] cmd=event verb=delete id=surge done=1", Event(r, "delete", "surge", "confirm"));
        Assert.Equal("[NYAR:err] cmd=event verb=delete code=notfound arg=id", Event(r, "delete", "surge", "confirm"));   // A15: fires once
        r.Ops.PurgeableUnits = 4;
        r.Flows.ApiPurge(r.Who, "", "");
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=confirm", r.Flows.ApiPurge(other, "confirm", ""));
        Assert.StartsWith("[NYAR:ok] cmd=purge verb=confirm ", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=state reason=nothing_to_purge", r.Flows.ApiPurge(r.Who, "confirm", ""));
    }

    [Fact]
    public void Idempotency_passes_human_ask_twin_confirm()
    {
        var r = Default();
        r.Ops.PurgeableUnits = 1;
        Assert.True(r.Flows.PurgeAsk(r.Who).Ok);
        Assert.StartsWith("[NYAR:ok] cmd=purge verb=confirm ", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.True(r.Flows.DeleteEvent(r.Who, "surge", false).Ok);
        Assert.Equal("[NYAR:ok] cmd=event verb=delete id=surge done=1", Event(r, "delete", "surge", "confirm"));
    }

    [Fact]
    public void Idempotency_passes_two_admins_confirm_in_one_second()
    {
        var r = Default();
        var other = r.Who with { Id = 8, Name = "Other" };
        r.Ops.PurgeableUnits = 5;
        r.Flows.ApiPurge(r.Who, "", "");
        r.Flows.ApiPurge(other, "", "");
        Assert.StartsWith("[NYAR:ok] cmd=purge verb=confirm id=- events=0 units=5 ", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=state reason=nothing_to_purge", r.Flows.ApiPurge(other, "confirm", ""));
        Assert.Single(r.Ops.Calls, c => c == "purge");
    }

    [Fact]
    public void Idempotency_empty_confirm_without_ask()
    {
        var r = Default();
        r.Ops.PurgeableUnits = 1;
        Assert.Equal("[NYAR:err] cmd=purge verb=confirm code=confirm", r.Flows.ApiPurge(r.Who, "confirm", ""));
        Assert.Equal("[NYAR:err] cmd=event verb=delete code=confirm", Event(r, "delete", "raid", "confirm"));
    }

    // ---- D6 the rate gate's place

    static Rig Gated() => new(Library.Of(Json.Event("raid"), Json.Empower("surge")), gate: new RateGate());

    [Fact]
    public void RateOrder_fails_when_sixth_twin_runs()
    {
        var r = Gated();
        Event(r, "start", "raid");                    // ok
        Event(r, "start", "raid");                    // refused
        Event(r, "launch");                           // unknown verb
        Event(r, "start");                            // badarg
        r.Flows.ApiPurge(r.Who, "maybe", "");         // badarg
        var (calls, logged) = (r.Ops.Calls.Count, Logged(r));
        Assert.Equal("[NYAR:err] cmd=event verb=stop code=ratelimit secs=1", Event(r, "stop", "raid"));
        Assert.Equal("[NYAR:err] cmd=event verb=- code=ratelimit secs=1", Event(r, ""));
        Assert.Equal(["[NYAR:err] cmd=pillar verb=set code=ratelimit secs=1"], r.Flows.ApiPillar(r.Who, "spawns", "off", ""));
        Assert.Equal(["[NYAR:err] cmd=template verb=use code=ratelimit secs=1"], r.Flows.ApiTemplate(r.Who, "use", "x", "", "", ""));
        Assert.Equal("[NYAR:err] cmd=purge verb=ask code=ratelimit secs=1", r.Flows.ApiPurge(r.Who, "", ""));
        Assert.Equal((calls, logged), (r.Ops.Calls.Count, Logged(r)));
        Assert.NotNull(r.Ops.Engine.Find("raid"));                                         // the rate-refused stop did nothing
    }

    [Fact]
    public void RateOrder_passes_reads_and_human_not_counted()
    {
        var r = Gated();
        for (var i = 0; i < 10; i++)
        {
            r.Flows.ApiTemplates("", "", "");
            r.Flows.ApiTemplate(r.Who, "info", "x", "", "", "");
            r.Flows.ApiPillar(r.Who, "list", "", "");
            r.Flows.ApiKillSwitch("");
            r.Flows.Enable(r.Who, "raid", true);
        }
        for (var i = 0; i < 5; i++) Assert.DoesNotContain("ratelimit", Event(r, "enable", "raid"));
        Assert.Contains("code=ratelimit", Event(r, "enable", "raid"));
    }

    [Fact]
    public void RateOrder_passes_one_second_later_and_per_admin()
    {
        var r = Gated();
        var other = r.Who with { Id = 8, Name = "Other" };
        for (var i = 0; i < 5; i++) Event(r, "enable", "raid");
        Assert.Contains("code=ratelimit", Event(r, "enable", "raid"));
        Assert.DoesNotContain("ratelimit", r.Flows.ApiEvent(other, "enable", "raid", "", "", ""));
        r.Lib.Fs.Now = r.Lib.Fs.Now.AddSeconds(1);
        Assert.Equal("[NYAR:ok] cmd=event verb=enable id=raid changed=0", Event(r, "enable", "raid"));
    }

    [Fact]
    public void RateOrder_empty_first_twin()
    {
        var r = Gated();
        Assert.Equal("[NYAR:ok] cmd=event verb=enable id=raid changed=0", Event(r, "enable", "raid"));
    }

    // ---- every twin line is one line of the grammar

    [Fact]
    public void Event_passes_one_grammar_line_each()
    {
        var r = Templated();
        var lines = new List<string>
        {
            Event(r, "start", "raid"), Event(r, "set", "raid", "name", "a=b c;d:e"), Event(r, "enable", "ghost"), Event(r, "set", "raid", "x", "y"),
            r.Flows.ApiPurge(r.Who, "", ""),
        };
        lines.AddRange(r.Flows.ApiTemplate(r.Who, "use", "undead-nightfall", "", "", ""));
        lines.AddRange(r.Flows.ApiPillar(r.Who, "zones", "on", ""));
        foreach (var line in lines)
        {
            Assert.Matches(@"^\[NYAR:(ok|err)\] cmd=[a-z]+ verb=\S+ ", line);
            Assert.DoesNotContain('\n', line);
            Assert.All(line.Split(' ').Skip(1), t => Assert.Matches(@"^[a-z]+=[^\s=;:<>]+$", t));
        }
    }
}
