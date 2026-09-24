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
        Assert.Contains(" api=1 ", Wire.Version(Sample));
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
