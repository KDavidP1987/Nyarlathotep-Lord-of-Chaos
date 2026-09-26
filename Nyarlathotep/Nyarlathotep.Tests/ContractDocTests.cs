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
        Assert.Equal(3, Wire.Api);   // faction-empowerment D11
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
    [InlineData("`status`", "IMPLEMENTED (api 3)")]
    [InlineData("`events`", "IMPLEMENTED (api 2)")]
    [InlineData("Push events", "IMPLEMENTED (api 2)")]
    [InlineData("4. Paging", "IMPLEMENTED (api 2)")]
    [InlineData("`me`", "PLANNED (stats)")]
    [InlineData("`top`", "PLANNED (stats)")]
    [InlineData("`zones`", "PLANNED (defended-zones)")]
    public void Section_headings_carry_their_status(string heading, string status) =>
        Assert.EndsWith(status, Heading(heading));

    /// <summary>faction-empowerment D11: §3 status documents the empower row's three values, and the change log lists api 3.</summary>
    [Fact]
    public void Status_documents_the_empower_row_and_the_change_log_lists_api_3()
    {
        var status = Regex.Match(Contract, @"(?ms)^### `status`.*?(?=^### )").Value;
        var flat = Regex.Replace(status, @"\s+", " ");
        Assert.Contains("An empower row (api 3) carries `faction=<names joined by ','>`", flat);
        Assert.Contains("its `wave` is `-`, and its admin `units` is the number of NPCs holding the event's empowerment", flat);
        Assert.Contains("kind=empower name=Legion_Surge state=active faction=Legion,Bandits left=1500 wave=- units=42", flat);
        var transport = Regex.Replace(Regex.Match(Contract, @"(?ms)^## 1\. Transport.*?(?=^## )").Value, @"\s+", " ");
        Assert.Contains("A value outside the set §3 lists for a key", transport);
        Assert.Contains("`kind=empower` has been listed since api 2 and is first sent in api 3", transport);
        var log = Regex.Match(Contract, @"(?ms)^## 9\. Change log.*").Value;
        Assert.Matches(@"(?m)^\| 3 \| 0\.4\.0 \(faction-empowerment\) \| `status` rows of `kind=empower`", log);
    }

    [Fact]
    public void The_fairness_and_notready_rules_are_stated()
    {
        var flat = Regex.Replace(Contract, @"\s+", " ");
        Assert.Contains("a push never tells a subscriber more than chat or `.nyar status` tells every player", flat);
        Assert.Contains("`wave-warn` is pushed only when the chat warning would fire", flat);
        Assert.Contains("| `notready` | Reserved, never sent (api 2 and 3)", flat);
    }

    /// <summary>The command forms of the design doc's § 6 table: each backticked form in a row's first cell, its words up
    /// to the first argument, with "a\|b" words expanded into each choice.</summary>
    static HashSet<string> DocumentedCommands()
    {
        var section = Regex.Match(Read("NYARLATHOTEP_DESIGN.md"), @"(?ms)^## 6\. Commands.*?(?=^## |\z)").Value;
        var forms = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match row in Regex.Matches(section, @"(?m)^\|((?:[^|\n\\]|\\.)*)\|"))
            foreach (Match code in Regex.Matches(row.Groups[1].Value, @"`(\.nyar[^`]*)`"))
            {
                var partial = new List<string> { "" };
                foreach (var word in code.Groups[1].Value.Replace("\\|", "|").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word[0] is '[' or '<' or '…') break;
                    partial = partial.SelectMany(p => word.Split('|').Select(c => p.Length == 0 ? c : $"{p} {c}")).ToList();
                }
                forms.UnionWith(partial);
            }
        return forms;
    }

    [Theory]
    [InlineData(".nyar api status")]
    [InlineData(".nyar api events")]
    [InlineData(".nyar api sub on")]
    [InlineData(".nyar api sub off")]
    public void The_design_doc_command_table_lists_the_new_commands(string command) =>
        Assert.Contains(command, DocumentedCommands());
}
