using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D14: docs/RAPHAEL_INTEGRATION_CONTRACT.md (and the design doc's command table) describe
/// api 2 as built. Both files are copied to the test output, so a doc edit that breaks the contract fails here.</summary>
public class ContractDocTests
{
    static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", name));
    static string Contract => Read("RAPHAEL_INTEGRATION_CONTRACT.md");

    static Dictionary<string, (string Status, string Api)> Table()
    {
        var table = Regex.Match(Contract, @"(?ms)^### Tags and commands.*?(?=^#|\z)").Value;
        Assert.NotEmpty(table);
        return Regex.Matches(table, @"(?m)^\| `([^`]+)` \| (tag|command) \| ([^|]+?) \| ([^|]+?) \|")
            .ToDictionary(m => $"{m.Groups[2].Value} {m.Groups[1].Value}", m => (m.Groups[3].Value, m.Groups[4].Value));
    }

    static string Heading(string start)
    {
        var m = Regex.Match(Contract, $@"(?m)^#{{2,3}} {Regex.Escape(start)}.*$");
        Assert.True(m.Success, $"no heading starting {start}");
        return m.Value.TrimEnd();
    }

    [Fact]
    public void The_current_api_is_the_code_api()
    {
        var m = Regex.Match(Contract, @"\*\*Current api:\*\* (\d+)");
        Assert.True(m.Success);
        Assert.Equal(Wire.Api, int.Parse(m.Groups[1].Value));
        Assert.Equal(2, Wire.Api);
    }

    [Theory]
    [InlineData("tag version", "1")]
    [InlineData("tag event", "2")]
    [InlineData("tag def", "2")]
    [InlineData("tag end", "2")]
    [InlineData("tag err", "2")]
    [InlineData("tag ok", "2")]
    [InlineData("tag ev", "2")]
    [InlineData("command version", "1")]
    [InlineData("command status", "2")]
    [InlineData("command events", "2")]
    [InlineData("command sub", "2")]
    public void Every_built_tag_and_command_is_implemented_with_its_api(string row, string api)
    {
        Assert.True(Table().TryGetValue(row, out var cell), $"{row} missing from the Tags and commands table");
        Assert.Equal(("IMPLEMENTED", api), cell);
    }

    [Theory]
    [InlineData("tag me", "stats")]
    [InlineData("tag top", "stats")]
    [InlineData("tag zone", "defended-zones")]
    [InlineData("command me", "stats")]
    [InlineData("command top", "stats")]
    [InlineData("command zones", "defended-zones")]
    public void Later_reads_stay_planned_and_name_their_child(string row, string child)
    {
        Assert.True(Table().TryGetValue(row, out var cell), $"{row} missing from the Tags and commands table");
        Assert.Equal($"PLANNED ({child})", cell.Status);
    }

    [Theory]
    [InlineData("`status`", "IMPLEMENTED (api 2)")]
    [InlineData("`events`", "IMPLEMENTED (api 2)")]
    [InlineData("Push events", "IMPLEMENTED (api 2)")]
    [InlineData("4. Paging", "IMPLEMENTED (api 2)")]
    [InlineData("`me`", "PLANNED (stats)")]
    [InlineData("`top`", "PLANNED (stats)")]
    [InlineData("`zones`", "PLANNED (defended-zones)")]
    public void Section_headings_carry_their_status(string heading, string status) =>
        Assert.EndsWith(status, Heading(heading));

    [Fact]
    public void The_fairness_and_notready_rules_are_stated()
    {
        var flat = Regex.Replace(Contract, @"\s+", " ");
        Assert.Contains("a push never tells a subscriber more than chat or `.nyar status` tells every player", flat);
        Assert.Contains("`wave-warn` is pushed only when the chat warning would fire", flat);
        Assert.Contains("| `notready` | Reserved, never sent in api 2", flat);
    }

    [Theory]
    [InlineData("`.nyar api status")]
    [InlineData("`.nyar api events")]
    [InlineData("`.nyar api sub on")]
    public void The_design_doc_lists_the_new_commands(string command)
    {
        var table = Regex.Match(Read("NYARLATHOTEP_DESIGN.md"), @"(?ms)^## 6\. Commands.*?(?=^## |\z)").Value;
        Assert.Contains(command, table);
    }
}
