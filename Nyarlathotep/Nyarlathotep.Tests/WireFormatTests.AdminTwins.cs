using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D8: the twin and read lines against contract §3 and §5a's examples, key by key and in
/// order, and the grammar at the longest values.</summary>
public partial class WireFormatTests
{
    /// <summary>Where each api 4 shape's examples live: the twins' ok and err in §5a, the reads' rows in §3.</summary>
    static readonly Dictionary<string, string> AdminDocumentedIn = new()
    {
        ["ok"] = "## 5a.", ["err"] = "## 5a.", ["tpl"] = "### `templates`", ["pillar"] = "### `pillar list`", ["ks"] = "### `killswitch`",
    };

    static List<string> AdminExamples(string tag) =>
        System.Text.RegularExpressions.Regex.Matches(Part(AdminDocumentedIn[tag]), @"(?ms)^```\r?\n(.*?)^```")
            .SelectMany(m => m.Groups[1].Value.Split('\n')).Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith($"[NYAR:{tag}] ", StringComparison.Ordinal)).ToList();

    /// <summary>The api 4 builder's line for an example's values, keys read by name.</summary>
    static string RebuildAdmin(string tag, Dictionary<string, string> t)
    {
        int? Opt(string key) => t.TryGetValue(key, out var v) ? int.Parse(v) : null;
        var fixedKeys = new[] { "cmd", "verb", "id" };
        return tag switch
        {
            "ok" => Wire.Done(t["cmd"], t["verb"], t["id"], t.Where(kv => !fixedKeys.Contains(kv.Key)).Select(kv => (kv.Key, kv.Value)).ToArray()),
            "err" => Wire.Refusal(t["cmd"], t["verb"], Enum.Parse<WireError>(t["code"], ignoreCase: true), Opt("secs"), t.GetValueOrDefault("arg"),
                t.GetValueOrDefault("reason")),
            "tpl" => Wire.Tpl(t["id"], t["pillar"], t["trigger"], int.Parse(t["duration"]), t["summary"]),
            "pillar" => Wire.Pillar(t["id"], t["on"] == "1"),
            "ks" => Wire.Ks(t["on"] == "1", int.Parse(t["secs"]), int.Parse(t["events"]), int.Parse(t["units"])),
            _ => throw new ArgumentException(tag),
        };
    }

    /// <summary>The keys the builder of a twin's ok line sends for each verb, in order (§5a's table).</summary>
    static readonly Dictionary<string, string[]> OkKeys = new()
    {
        ["event start"] = [], ["event stop"] = [], ["event enable"] = ["changed"], ["event disable"] = ["changed"],
        ["event set"] = ["field", "value"], ["event reload"] = ["count"], ["event new"] = ["pillar"], ["event copy"] = ["from"],
        ["event delete"] = ["confirm", "done"], ["template use"] = ["tpl"], ["pillar set"] = ["on", "changed", "ended"],
        ["purge ask"] = ["confirm"], ["purge confirm"] = ["events", "units", "secs"],
    };

    [Theory]
    [InlineData("ok")]
    [InlineData("err")]
    [InlineData("tpl")]
    [InlineData("pillar")]
    [InlineData("ks")]
    public void AdminTwins_passes_contract_examples(string tag)
    {
        var examples = AdminExamples(tag);
        Assert.NotEmpty(examples);
        foreach (var example in examples)
        {
            var built = RebuildAdmin(tag, Tokens(example));
            AssertWellFormed(built);
            Assert.Equal(example, built);
            if (tag == "ok")
            {
                var t = Tokens(example);
                var keys = t.Keys.Skip(3).ToArray();
                var verb = $"{t["cmd"]} {t["verb"]}";
                Assert.True(OkKeys.ContainsKey(verb), $"no ok keys for {verb}");
                Assert.Equal(OkKeys[verb].Where(keys.Contains), keys);          // the example's keys are the verb's, in order
            }
        }
    }

    [Fact]
    public void AdminTwins_fails_when_key_order_differs()
    {
        var example = AdminExamples("ks")[0];
        var parts = example.Split(' ');
        (parts[1], parts[2]) = (parts[2], parts[1]);
        var swapped = string.Join(' ', parts);
        Assert.NotEqual(swapped, RebuildAdmin("ks", Tokens(swapped)));
        var err = "[NYAR:err] cmd=event verb=start arg=id code=state";
        Assert.NotEqual(err, RebuildAdmin("err", Tokens(err)));
    }

    [Fact]
    public void AdminTwins_passes_longest_values_cut()
    {
        var id = new string('a', 32);
        var wide = string.Concat(Enumerable.Repeat("\U0001D54F", 200));   // 200 four-byte characters
        var ok = Wire.Done("event", "set", id, ("field", "action.stats.physicalPower"), ("value", wide));
        AssertWellFormed(ok);
        Assert.Equal(Wire.ValueBytes, Encoding.UTF8.GetByteCount(Tokens(ok)["value"]));
        Assert.DoesNotContain('�', ok);

        var err = Wire.Refusal("event", wide, WireError.Invalid, 999_999, "field", wide);
        AssertWellFormed(err);
        Assert.Equal(["cmd", "verb", "code", "secs", "arg", "reason"], Tokens(err).Keys);
        Assert.Equal(Wire.ValueBytes, Encoding.UTF8.GetByteCount(Tokens(err)["verb"]));
        Assert.Equal(Wire.ValueBytes, Encoding.UTF8.GetByteCount(Tokens(err)["reason"]));

        var longest = TemplateLibraryTests.Real().Templates.MaxBy(t => Encoding.UTF8.GetByteCount(t.Definition.Name))!;
        AssertWellFormed(ApiLines.Tpl(longest));
        var tpl = Wire.Tpl(id, "empowerment", "vbloodkilled", int.MaxValue, wide);
        AssertWellFormed(tpl);
        Assert.Equal(Wire.ValueBytes, Encoding.UTF8.GetByteCount(Tokens(tpl)["summary"]));
    }

    [Fact]
    public void AdminTwins_fails_when_forbidden_character_given()
    {
        var ok = Wire.Done("event", "set", "raid", ("field", "name"), ("value", "a=b c;d:e<b>x</b>\nnext"));
        AssertWellFormed(ok);
        Assert.EndsWith(" value=ab_cdebx/bnext", ok);
        var err = Wire.Refusal("event", "la unch=", WireError.BadArg, arg: "verb");
        AssertWellFormed(err);
        Assert.Equal("[NYAR:err] cmd=event verb=la_unch code=badarg arg=verb", err);
    }

    [Fact]
    public void AdminTwins_empty_value()
    {
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=name value=-", Wire.Done("event", "set", "raid", ("field", "name"), ("value", "")));
        Assert.Equal("[NYAR:err] cmd=event verb=- code=badarg arg=verb", Wire.Refusal("event", "", WireError.BadArg, arg: "verb"));
    }
}
