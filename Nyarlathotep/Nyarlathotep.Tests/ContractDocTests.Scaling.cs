using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D16: the scaling fields go over the wire through the existing `event set` twin, answering
/// `[NYAR:ok] cmd=event verb=set id=&lt;id&gt; field=action.scaling.&lt;key&gt; value=&lt;stored value&gt;` or the
/// existing `invalid` and `badarg` codes; no tag, key, push kind or command is added and Wire.Api stays 8; the contract's §5a
/// lists the fields with one sentence that scaling's counts and gear never reach the wire; design §6 lists them.</summary>
public partial class ContractDocTests
{
    const string ScalingSentence = "Scaling's player counts and gear levels never reach the wire.";
    const string ScalingTwinReply = "`[NYAR:ok] cmd=event verb=set id=<id> field=action.scaling.<key> value=<stored value>`";
    const string ScalingTwinRefusals = "`code=badarg arg=value` for a value out of range and `code=invalid arg=field reason=field` for a set the validator refuses";

    static readonly string[] ScalingContractFields =
        ["`action.scaling.unitsPerPlayer` (0-5)", "`action.scaling.levelFromPlayers` (`true` or `false`)", "`action.scaling.levelOffset` (-20..20)",
         "`action.scaling.levelPerWave` (0-5)", "`action.scaling none`"];

    /// <summary>The api commands and event verbs at 0.13.0; scaling adds none.</summary>
    static readonly string[] ApiWords = ["event", "events", "killswitch", "pillar", "purge", "regions", "status", "sub", "template", "templates", "version"];
    static readonly string[] EventTwinVerbs = ["start", "stop", "enable", "disable", "set", "reload", "new", "copy", "delete", "unstick"];

    /// <summary>The twin's replies for one set of each field over raid, then a refused value and a block D1 refuses.</summary>
    static List<string> ScalingTwin()
    {
        var r = HumanReplyTests.Default();
        return
        [
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.unitsPerPlayer", "2", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.levelFromPlayers", "true", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.levelOffset", "-3", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.levelPerWave", "1", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling", "none", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.unitsPerPlayer", "9", ""),
            r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.levelOffset", "2", ""),
            r.Flows.ApiEvent(r.Who, "set", "surge", "action.scaling.unitsPerPlayer", "2", ""),
        ];
    }

    static readonly string[] ScalingTwinWant =
    [
        "[NYAR:ok] cmd=event verb=set id=raid field=action.scaling.unitsPerPlayer value=2",
        "[NYAR:ok] cmd=event verb=set id=raid field=action.scaling.levelFromPlayers value=true",
        "[NYAR:ok] cmd=event verb=set id=raid field=action.scaling.levelOffset value=-3",
        "[NYAR:ok] cmd=event verb=set id=raid field=action.scaling.levelPerWave value=1",
        "[NYAR:ok] cmd=event verb=set id=raid field=action.scaling value=none",
        "[NYAR:err] cmd=event verb=set code=badarg arg=value",
        "[NYAR:err] cmd=event verb=set code=invalid arg=field reason=field",
        "[NYAR:err] cmd=event verb=set code=invalid arg=field reason=field",
    ];

    /// <summary>What keeps the wire and the documents from describing scaling; empty when they do.</summary>
    internal static List<string> ScalingProblems(string contract, string design, int api, IEnumerable<string> pushKinds, IReadOnlyList<string> twin,
        IEnumerable<string> apiWords)
    {
        var p = new List<string>();
        var c = System.Text.RegularExpressions.Regex.Replace(contract, @"\s+", " ");
        foreach (var f in ScalingContractFields.Where(f => !c.Contains(f, StringComparison.Ordinal))) p.Add($"§5a misses {f}");
        if (!c.Contains(ScalingTwinReply, StringComparison.Ordinal)) p.Add("§5a misses the twin reply");
        if (!c.Contains(ScalingTwinRefusals, StringComparison.Ordinal)) p.Add("§5a misses the twin refusals");
        if (!c.Contains(ScalingSentence, StringComparison.Ordinal)) p.Add("the no-wire sentence is missing");
        if (System.Text.RegularExpressions.Regex.IsMatch(contract, @"(?m)^\| \d+ \| 0\.14\.0")) p.Add("§9 gained an api row");
        p.AddRange(EventAdminTests.ScalingDesignProblems(design));
        if (api != 8) p.Add($"Wire.Api is {api}, not 8");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!apiWords.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(ApiWords)) p.Add("the api commands changed");
        if (!twin.SequenceEqual(ScalingTwinWant)) p.Add("a twin reply differs");
        return p;
    }

    static IEnumerable<string> ApiNames() => ApiCommandTable.Commands.Select(c => c.Name);

    [Fact]
    public void Scaling_fails_when_a_field_is_missing_from_5a_or_6()
    {
        Assert.Equal(["§5a misses `action.scaling.levelOffset` (-20..20)"],
            ScalingProblems(Contract.Replace("`action.scaling.levelOffset` (-20..20)", "`action.scaling.levelOffset`", StringComparison.Ordinal), Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));
        Assert.Equal(["§6 misses action.scaling.unitsPerPlayer"],
            ScalingProblems(Contract, Design.Replace("| `action.scaling.unitsPerPlayer` | action | admin |", "", StringComparison.Ordinal), Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));
        Assert.Equal(["§5a misses the twin reply"],
            ScalingProblems(Contract.Replace("field=action.scaling.<key>", "field=<field>", StringComparison.Ordinal), Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));
    }

    [Fact]
    public void Scaling_fails_when_a_twin_reply_differs()
    {
        var twin = ScalingTwin();
        Assert.Equal(ScalingTwinWant, twin);
        Assert.Equal(["§5a misses the twin refusals"], ScalingProblems(Contract.Replace("`code=invalid arg=field reason=field` for a set", "`code=invalid` for a set",
            StringComparison.Ordinal), Design, Wire.Api, PushTypes(), twin, ApiNames()));
        Assert.Equal(["a twin reply differs"], ScalingProblems(Contract, Design, Wire.Api, PushTypes(), twin.Select(l => l.Replace("value=2", "value=two", StringComparison.Ordinal)).ToList(), ApiNames()));
    }

    [Fact]
    public void Scaling_fails_when_the_api_differs()
    {
        Assert.Equal(8, Wire.Api);
        Assert.Equal(["Wire.Api is 9, not 8"], ScalingProblems(Contract, Design, 9, PushTypes(), ScalingTwin(), ApiNames()));
        Assert.Equal(["§9 gained an api row"], ScalingProblems(Contract + "\n| 9 | 0.14.0 (scaling) | scaling fields |\n", Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));
    }

    [Fact]
    public void Scaling_fails_when_a_tag_key_push_kind_or_command_is_added()
    {
        Assert.Equal(["the push kinds changed"], ScalingProblems(Contract, Design, Wire.Api, PushTypes().Append("scaling"), ScalingTwin(), ApiNames()));
        Assert.Equal(["the api commands changed"], ScalingProblems(Contract, Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames().Append("scaling")));
        Assert.Equal(EventTwinVerbs, AdminFlows.EventVerbs);
        // no wire line of the twin carries a key past id, field and value
        Assert.All(ScalingTwin().Where(l => l.StartsWith("[NYAR:ok]", StringComparison.Ordinal)), l =>
            Assert.Equal(["cmd", "verb", "id", "field", "value"], l.Split(' ').Skip(1).Select(kv => kv.Split('=')[0])));
    }

    [Fact]
    public void Scaling_fails_when_the_sentence_is_missing() =>
        Assert.Equal(["the no-wire sentence is missing"],
            ScalingProblems(Contract.Replace(ScalingSentence, "", StringComparison.Ordinal), Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));

    [Fact]
    public void Scaling_passes_contract_and_design() => Assert.Empty(ScalingProblems(Contract, Design, Wire.Api, PushTypes(), ScalingTwin(), ApiNames()));

    [Fact]
    public void Scaling_empty_documents() => Assert.Equal(13, ScalingProblems("", "", Wire.Api, PushTypes(), ScalingTwin(), ApiNames()).Count);
}
