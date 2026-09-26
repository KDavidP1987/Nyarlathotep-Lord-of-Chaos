using System.Text;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D12: the wire grammar, and the handshake keys read from docs/RAPHAEL_INTEGRATION_CONTRACT.md
/// itself (copied to the test output), so a contract change without a code change fails here.</summary>
public class WireFormatTests
{
    static string Contract => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "RAPHAEL_INTEGRATION_CONTRACT.md"));

    static string Section(string heading)
    {
        var m = Regex.Match(Contract, $@"(?ms)^## {Regex.Escape(heading)}.*?(?=^## |\z)");
        Assert.True(m.Success, $"contract section {heading} not found");
        return m.Value;
    }

    /// <summary>The backticked first-column names of the section's table rows; a row may list several.</summary>
    static List<string> TableNames(string section) =>
        Regex.Matches(section, @"(?m)^\| `([^`]+)` \|").SelectMany(m => m.Groups[1].Value.Split(' ')).ToList();

    static readonly VersionInfo Sample = new(1, "0.2.0", true, false, true, false,
        false, false, false, false, false, false, false, false, false, false, false);

    static void AssertWellFormed(string line)
    {
        Assert.Matches(@"^\[NYAR:[a-z][a-z0-9-]*\]", line);
        Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(line)} bytes");
        Assert.DoesNotContain('<', line);
        Assert.DoesNotContain('>', line);
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        var tokens = line[(line.IndexOf(']') + 1)..].Split(' ', StringSplitOptions.None).Skip(1).ToList();
        foreach (var t in tokens)
        {
            var eq = t.IndexOf('=');
            Assert.True(eq > 0, $"token '{t}' is not key=value");
            var value = t[(eq + 1)..];
            Assert.NotEmpty(value);
            Assert.True(value.IndexOfAny(new[] { ' ', '=', ';', ':' }) < 0, $"value '{value}' holds a forbidden character");
        }
    }

    [Fact]
    public void The_version_line_carries_every_key_of_contract_section_2_in_order()
    {
        var expected = TableNames(Section("2. Handshake"));
        Assert.Contains("api", expected);
        Assert.Contains("annshare", expected);
        var line = Wire.Version(Sample);
        AssertWellFormed(line);
        Assert.StartsWith("[NYAR:version] ", line);
        var keys = line.Split(' ').Skip(1).Select(t => t[..t.IndexOf('=')]).ToList();
        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Ready_is_0_before_the_world_loads_and_1_after()
    {
        Assert.Contains(" ready=0 ", Wire.Version(Sample with { Ready = false }));
        Assert.Contains(" ready=1 ", Wire.Version(Sample with { Ready = true }));
        Assert.Contains(" api=3 ", Wire.Version(Sample with { Api = Wire.Api }));
        Assert.Equal(3, Wire.Api);   // raphael-api-core D4, faction-empowerment D11
        Assert.Contains(" plugin=0.2.0 ", Wire.Version(Sample));
    }

    [Fact]
    public void Every_error_code_is_one_of_the_contract_codes()
    {
        var codes = TableNames(Section("4. Paging"));
        Assert.Equal(Enum.GetValues<WireError>().Length, codes.Count);
        foreach (var e in Enum.GetValues<WireError>())
        {
            var line = Wire.Error("status", e, secs: 30, arg: "page");
            AssertWellFormed(line);
            var code = Regex.Match(line, " code=([^ ]+)").Groups[1].Value;
            Assert.Contains(code, codes);
        }
        Assert.Equal("[NYAR:err] cmd=events code=badarg arg=page", Wire.Error("events", WireError.BadArg, arg: "page"));
    }

    [Theory]
    [InlineData("Ashfall Raid", "Ashfall_Raid")]
    [InlineData("a=b;c:d", "abcd")]
    [InlineData("<color=red>x</color>\nnext", "colorredx/colornext")]
    [InlineData("", "-")]
    [InlineData("   ", "___")]
    public void Values_are_mapped_to_the_grammar(string raw, string wire)
    {
        var line = Wire.Line("event", ("name", raw));
        AssertWellFormed(line);
        Assert.Equal($"[NYAR:event] name={wire}", line);
    }

    [Fact]
    public void A_long_line_stops_at_480_bytes_without_cutting_a_token()
    {
        var tokens = Enumerable.Range(0, 60).Select(i => ($"k{i}", new string('v', 20))).ToArray();
        var line = Wire.Line("row", tokens);
        AssertWellFormed(line);
        Assert.True(Encoding.UTF8.GetByteCount(line) > Wire.MaxBytes - 25);
        Assert.EndsWith("=" + new string('v', 20), line);

        var wide = Wire.Line("row", ("name", new string('é', 400)));   // 2 bytes each: the token cannot fit
        AssertWellFormed(wide);
        Assert.Equal("[NYAR:row]", wide);
    }

    [Fact]
    public void A_required_line_throws_rather_than_drop_a_field()
    {
        var huge = new string('9', 470);
        Assert.Throws<ArgumentException>(() => Wire.Version(Sample with { Plugin = huge }));
        Assert.Throws<ArgumentException>(() => Wire.Error(huge, WireError.BadArg, arg: "page"));
        // The longest real values fit with every key present.
        var line = Wire.Version(Sample with { Plugin = "10.100.1000-beta.12345" });
        Assert.Equal(TableNames(Section("2. Handshake")).Count, line.Split(' ').Length - 1);
    }

    /// <summary>The part of the contract that documents each tag raphael-api-core builds: its examples are read there
    /// and nowhere else.</summary>
    static readonly Dictionary<string, string> DocumentedIn = new()
    {
        ["event"] = "### `status`", ["def"] = "### `events`", ["ev"] = "### Push events",
        ["end"] = "## 4.", ["err"] = "## 4.", ["ok"] = "## 4.",
    };

    /// <summary>From <paramref name="heading"/> to the next heading of its level or above, or the next rule.</summary>
    static string Part(string heading)
    {
        var stop = heading.StartsWith("### ") ? "#{2,3} " : "## ";
        var m = Regex.Match(Contract, $@"(?ms)^{Regex.Escape(heading)}.*?(?=^{stop}|^---|\z)");
        Assert.True(m.Success, $"contract part {heading} not found");
        return m.Value;
    }

    /// <summary>Every example line of the code blocks in the part that documents <paramref name="tag"/>.</summary>
    static List<string> Examples(string tag) =>
        Regex.Matches(Part(DocumentedIn[tag]), @"(?ms)^```\r?\n(.*?)^```").SelectMany(m => m.Groups[1].Value.Split('\n'))
            .Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith($"[NYAR:{tag}] ", StringComparison.Ordinal)).ToList();

    static Dictionary<string, string> Tokens(string line) =>
        line.Split(' ').Skip(1).ToDictionary(t => t[..t.IndexOf('=')], t => t[(t.IndexOf('=') + 1)..]);

    /// <summary>The builder's line for the example's values. Keys are read by name, so a builder that adds, drops or
    /// reorders a key produces a line that differs from the example.</summary>
    static string Rebuild(string tag, Dictionary<string, string> t)
    {
        int? Opt(string key) => t.TryGetValue(key, out var v) && v != "-" ? int.Parse(v) : null;
        switch (tag)
        {
            case "event":
                return Wire.Event(t["id"], t["kind"], t["name"], t["state"], t["faction"], int.Parse(t["left"]), t["wave"], Opt("units"));
            case "def":
                return Wire.Def(t["id"], t["name"], t["enabled"] == "1", t["trigger"], t["action"], int.Parse(t["duration"]), t["state"],
                    t["reason"] == "-" ? null : t["reason"]);
            case "end":
                if (!t.TryGetValue("page", out var page)) return Wire.End(t["cmd"], int.Parse(t["count"]));
                var parts = page.Split('/');
                return Wire.EndPaged(t["cmd"], int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(t["count"]));
            case "err":
                return Wire.Error(t["cmd"], Enum.Parse<WireError>(t["code"], ignoreCase: true), Opt("secs"), t.GetValueOrDefault("arg"));
            case "ok":
                // The only ok line of api 2 is the subscription's.
                return Wire.Ok(t["cmd"], ("on", t["on"]));
            case "ev":
                return Wire.Ev(t["type"], t["id"], int.Parse(t["secs"]), Opt("wave"));
            default:
                throw new ArgumentException(tag);
        }
    }

    [Theory]
    [InlineData("event")]
    [InlineData("def")]
    [InlineData("end")]
    [InlineData("err")]
    [InlineData("ok")]
    [InlineData("ev")]
    public void Every_contract_example_of_a_built_tag_is_what_the_builder_sends(string tag)
    {
        var examples = Examples(tag);
        Assert.NotEmpty(examples);
        foreach (var example in examples)
        {
            var built = Rebuild(tag, Tokens(example));
            AssertWellFormed(built);
            Assert.Equal(example, built);
        }
    }

    [Fact]
    public void Every_optional_form_is_documented()
    {
        var ends = Examples("end");
        Assert.Contains(ends, l => l.Contains(" page="));
        Assert.Contains(ends, l => !l.Contains(" page="));
        var errs = Examples("err");
        Assert.Contains(errs, l => l.Contains(" secs="));
        Assert.Contains(errs, l => l.Contains(" arg="));
        Assert.Contains(Examples("ev"), l => l.Contains(" wave="));
    }

    [Fact]
    public void An_error_with_both_optional_keys_sends_secs_before_arg()
    {
        Assert.Equal("[NYAR:err] cmd=top code=ratelimit secs=45 arg=page", Wire.Error("top", WireError.RateLimit, secs: 45, arg: "page"));
        Assert.Equal("[NYAR:ev] type=event-end id=ashfall secs=0", Wire.Ev("event-end", "ashfall", 0));
    }

    [Fact]
    public void A_long_multibyte_name_or_reason_is_cut_and_every_key_survives()
    {
        var id = new string('a', 32);
        var name = string.Concat(Enumerable.Repeat("\U0001D54F", 200));   // 200 four-byte characters
        var reason = name;

        var ev = Wire.Event(id, "waves", name, "active", "-", int.MaxValue, "999/999", int.MaxValue);
        AssertWellFormed(ev);
        Assert.Equal(["id", "kind", "name", "state", "faction", "left", "wave", "units"], Tokens(ev).Keys);
        Assert.Equal(Wire.NameBytes, Encoding.UTF8.GetByteCount(Tokens(ev)["name"]));

        var def = Wire.Def(id, name, true, "vbloodkilled", "waves", int.MaxValue, "disabled", reason);
        AssertWellFormed(def);
        Assert.Equal(["id", "name", "enabled", "trigger", "action", "duration", "state", "reason"], Tokens(def).Keys);
        Assert.Equal(Wire.ReasonBytes, Encoding.UTF8.GetByteCount(Tokens(def)["reason"]));
        Assert.DoesNotContain('�', def);   // never cut inside a character
    }

    [Fact]
    public void A_name_is_mapped_before_it_is_cut()
    {
        var def = Wire.Def("raid", "a=b c;d:e", true, "manual", "waves", 60, "idle", null);
        Assert.Contains(" name=ab_cde ", def);
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("bad tag")]
    [InlineData("")]
    public void A_bad_tag_or_key_is_a_code_error(string name)
    {
        Assert.Throws<ArgumentException>(() => Wire.Line(name));
        Assert.Throws<ArgumentException>(() => Wire.Line("row", (name, "v")));
    }
}
