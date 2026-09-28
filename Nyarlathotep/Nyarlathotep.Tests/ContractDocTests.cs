using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D14: docs/RAPHAEL_INTEGRATION_CONTRACT.md (and the design doc's command table) describe
/// api 2 as built. Both files are copied to the test output, so a doc edit that breaks the contract fails here.</summary>
public partial class ContractDocTests
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
        Assert.Equal(4, Wire.Api);   // faction-empowerment D11, raphael-api-admin D10
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
        Assert.Contains("| `notready` | Reserved, never sent (api 2 to 4)", flat);
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

    // ---- event-library D20: § 6 lists every declared form and settable field, and nothing else

    /// <summary>The cells of a markdown table row, split on unescaped pipes.</summary>
    static string[] Cells(string row) =>
        Regex.Split(row.Trim().Trim('|'), @"(?<!\\)\|").Select(c => c.Trim().Replace("\\|", "|")).ToArray();

    /// <summary>What differs between § 6 of <paramref name="doc"/> and the code: the command rows whose Child names
    /// event-library against CommandForms.Library (usage and who), and the settable-field table against
    /// CommandArgs.SettableFields (field, family, who), both ways. Empty when they agree.</summary>
    static List<string> LibraryDocDifferences(string doc)
    {
        var diffs = new List<string>();
        var section = Regex.Match(doc, @"(?ms)^## 6\. Commands.*?(?=^## |\z)").Value;
        var commands = Regex.Match(section, @"(?ms)^\| Command \|.*?(?=^\s*$|\z)").Value;
        var fields = Regex.Match(section, @"(?ms)^\| Field \| Family \| Who \|.*?(?=^\s*$|\z)").Value;
        if (commands.Length == 0) diffs.Add("no command table");
        if (fields.Length == 0) diffs.Add("no field table");

        var documented = new HashSet<(string, string)>();
        foreach (var row in commands.Split('\n').Skip(2).Where(r => r.StartsWith('|')))
        {
            var c = Cells(row);
            if (c.Length < 4 || !c[3].Contains("event-library", StringComparison.Ordinal)) continue;
            foreach (Match code in Regex.Matches(c[0], @"`(\.nyar[^`]*)`")) documented.Add((code.Groups[1].Value, c[1]));
        }
        var declared = CommandForms.Library.Select(f => (f.Usage, f.Who)).ToHashSet();
        foreach (var x in declared.Except(documented)) diffs.Add($"form {x.Item1} ({x.Item2}) missing from § 6");
        foreach (var x in documented.Except(declared)) diffs.Add($"§ 6 lists {x.Item1} ({x.Item2}), which the code does not declare");
        foreach (var f in CommandForms.Library.Where(f => f.Who != "admin")) diffs.Add($"form {f.Usage} is not admin");

        var docFields = fields.Split('\n').Skip(2).Where(r => r.StartsWith('|')).Select(Cells)
            .Where(c => c.Length >= 3).Select(c => (c[0].Trim('`'), c[1], c[2])).ToHashSet();
        var codeFields = CommandArgs.SettableFields.Select(kv => (kv.Key, kv.Value.Family, kv.Value.Who)).ToHashSet();
        foreach (var x in codeFields.Except(docFields)) diffs.Add($"field {x.Item1} ({x.Item2}, {x.Item3}) missing from § 6");
        foreach (var x in docFields.Except(codeFields)) diffs.Add($"§ 6 lists field {x.Item1} ({x.Item2}, {x.Item3}), which SettableFields does not have");
        return diffs;
    }

    [Fact]
    public void DesignDoc_passes_forms_and_fields_match() => Assert.Empty(LibraryDocDifferences(Read("NYARLATHOTEP_DESIGN.md")));

    [Theory]
    [InlineData("| `.nyar template info <template>` | admin |", "| `.nyar template info <template> <page>` | admin |", "missing from § 6")]
    [InlineData("| `.nyar pillar list` | admin |", "| `.nyar pillar list` | anyone |", "missing from § 6")]
    [InlineData("| `trigger.phase` | trigger | admin |", "| `trigger.phase` | definition | admin |", "missing from § 6")]
    [InlineData("| `location` | location | admin |", "| `location` | location | admin |\n| `action.location` | spawn action | admin |", "which SettableFields does not have")]
    [InlineData("| `.nyar event copy <id> <newId>` | admin | A disabled verbatim copy under a new id | event-library |\n", "", "missing from § 6")]
    [InlineData("| `.nyar pillar list` | admin |", "| `.nyar pillar list` | admin |\n| `.nyar pillar reset` | admin | x | event-library |\n| `.nyar pillar list2` | admin |", "which the code does not declare")]
    public void DesignDoc_fails_when_table_differs(string find, string replace, string problem)
    {
        var doc = Read("NYARLATHOTEP_DESIGN.md").Replace("\r\n", "\n");
        Assert.Contains(find, doc);
        var diffs = LibraryDocDifferences(doc.Replace(find, replace));
        Assert.Contains(diffs, d => d.Contains(problem, StringComparison.Ordinal));
    }

    [Fact]
    public void DesignDoc_empty_section() =>
        Assert.Equal(["no command table", "no field table"], LibraryDocDifferences("# nothing here").Take(2));
}
