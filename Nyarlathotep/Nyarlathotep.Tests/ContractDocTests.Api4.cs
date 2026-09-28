using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D10: the contract at api 4, its §10 without this child's rows, the refusal rule worded as
/// Business rules 2 in §5a and §4's io row, the design doc's § 6 rows and the handoff's 0.5.2 line.</summary>
public partial class ContractDocTests
{
    /// <summary>What keeps a contract text from describing api 4 as built; empty when it does.</summary>
    internal static List<string> Api4Problems(string contract, string handoff, string design)
    {
        var p = new List<string>();
        var current = Regex.Match(contract, @"\*\*Current api:\*\* (\d+)");
        if (!current.Success || int.Parse(current.Groups[1].Value) != Wire.Api) p.Add("the current api is not the code's");
        var table = Regex.Match(contract, @"(?ms)^### Tags and commands.*?(?=^#|\z)").Value;
        if (table.Length == 0) p.Add("no Tags and commands table");
        foreach (var row in new[] { "tpl | tag", "pillar | tag", "ks | tag", "event | command", "template | command", "templates | command",
                     "pillar | command", "purge | command", "killswitch | command" })
        {
            var parts = row.Split(" | ");
            if (!Regex.IsMatch(table, $@"(?m)^\| `{parts[0]}` \| {parts[1]} \| IMPLEMENTED \| 4 \|")) p.Add($"{parts[1]} {parts[0]} is not IMPLEMENTED (api 4)");
        }
        var codes = Regex.Match(contract, @"(?ms)^## 4\. .*?(?=^## )").Value;
        foreach (var e in Enum.GetValues<WireError>())
            if (!Regex.IsMatch(codes, $@"(?m)^\| `{e.ToString().ToLowerInvariant()}` \|")) p.Add($"§4 has no row for {e}");
        var ten = Regex.Match(contract, @"(?ms)^## 10\. .*").Value;
        if (Regex.IsMatch(ten, @"(?m)^\| raphael-api-admin \|")) p.Add("§10 still lists a raphael-api-admin row");
        if (Regex.IsMatch(ten, @"(?m)^### 10\.[13] ")) p.Add("§10 still holds §10.1 or §10.3");
        var twins = Regex.Match(contract, @"(?ms)^## 5a\. Admin action twins — IMPLEMENTED \(api 4\).*?(?=^## )").Value;
        if (twins.Length == 0) p.Add("no §5a Admin action twins — IMPLEMENTED (api 4)");
        var flatTwins = Regex.Replace(twins, @"\s+", " ");
        if (!flatTwins.Contains("A refusal** changes nothing on the server except what a file now holds after a failed save", StringComparison.Ordinal)
            || !flatTwins.Contains("and the events a pillar `off` ends", StringComparison.Ordinal))
            p.Add("§5a's refusal rule lacks Business rules 2's exceptions");
        if (!flatTwins.Contains("at most 5 twins per admin per second", StringComparison.Ordinal)) p.Add("§5a lacks the rate");
        if (!flatTwins.Contains("possibly after it changed state, so the action may be partly or fully applied", StringComparison.Ordinal))
            p.Add("§5a does not say an internal failure may leave the action applied");
        foreach (var reason in Reasons.All)
            if (!flatTwins.Contains($"`{reason}`", StringComparison.Ordinal)) p.Add($"§5a's reason list lacks {reason}");
        var io = Regex.Match(codes, @"(?m)^\| `io` \|.*$").Value;
        if (!io.Contains("changes nothing on the server except what a file now holds after a failed save and the events a pillar `off` ends", StringComparison.Ordinal))
            p.Add("§4's io row lacks Business rules 2's exceptions");
        if (!io.Contains("`internal` may leave the action partly or fully applied; re-read state", StringComparison.Ordinal))
            p.Add("§4's io row does not say internal may leave the action applied");
        if (Regex.IsMatch(contract, @"changes nothing\.?\s*(?:\||$)", RegexOptions.Multiline)) p.Add("a line still says a refusal changes nothing, with no exception");
        var five = Regex.Match(contract, @"(?ms)^## 5\. .*?(?=^\| Panel action)").Value;
        if (!Regex.Replace(five, @"\s+", " ").Contains("When the handshake reports `api>=4`, Raphael sends the admin actions as their §5a wire twins", StringComparison.Ordinal))
            p.Add("§5 does not send api 4 clients to the §5a twins");
        var log = Regex.Match(contract, @"(?ms)^## 9\. Change log.*?(?=^---|^## 10)").Value;
        if (!Regex.IsMatch(log, @"(?m)^\| 4 \| 0\.5\.2 \(raphael-api-admin\) \|")) p.Add("§9 lacks the api 4 row");
        var lands = Regex.Match(handoff, @"(?ms)^## When api 4 lands.*?(?=^## )").Value;
        if (!lands.Contains("api 4 ships in Nyarlathotep 0.5.2", StringComparison.Ordinal)) p.Add("the handoff lacks the 0.5.2 line");
        if (Regex.IsMatch(handoff, @"api 3\*{0,2}\)? today|today \(\*{0,2}api 3|What exists today \(api 3", RegexOptions.IgnoreCase))
            p.Add("the handoff still calls api 3 current");
        if (!Regex.Replace(handoff, @"\s+", " ").Contains("Nothing is rolled back, so re-read state", StringComparison.Ordinal))
            p.Add("the handoff lacks the internal rule");
        foreach (var twin in new[] { ".nyar api event <verb>", ".nyar api template use", ".nyar api pillar <name> on|off", ".nyar api purge [confirm]" })
            if (!lands.Contains(twin, StringComparison.Ordinal)) p.Add($"the handoff does not name {twin}");
        var section6 = Regex.Match(design, @"(?ms)^## 6\. Commands.*?(?=^## |\z)").Value;
        foreach (var form in new[] { "`.nyar api event <verb> …`", "`.nyar api template use <template> [as <id>]`", "`.nyar api pillar <name> on\\|off`",
                     "`.nyar api purge [confirm]`", "`.nyar api templates [pillar] [page]`", "`.nyar api template info <template>`", "`.nyar api pillar list`",
                     "`.nyar api killswitch`" })
            if (!section6.Contains(form, StringComparison.Ordinal)) p.Add($"design § 6 lacks {form}");
        return p;
    }

    static string Handoff => Read("RAPHAEL_HANDOFF_API4.md");
    static string Design => Read("NYARLATHOTEP_DESIGN.md");

    [Fact]
    public void ContractApi4_passes_contract_handoff_and_design() => Assert.Empty(Api4Problems(Contract, Handoff, Design));

    [Theory]
    [InlineData("**Current api:** 4", "**Current api:** 3", "the current api is not the code's")]
    [InlineData("| `ks` | tag | IMPLEMENTED | 4 |", "| `ks` | tag | PLANNED (raphael-api-admin) | — |", "tag ks is not IMPLEMENTED (api 4)")]
    [InlineData("| `event` | command | IMPLEMENTED | 4 |", "| `event` | command | PLANNED (raphael-api-admin) | — |", "command event is not IMPLEMENTED (api 4)")]
    [InlineData("| `limit` |", "| `lim` |", "§4 has no row for Limit")]
    [InlineData("| regions | 0.6.0 |", "| raphael-api-admin | 0.5.2 | twins |\n| regions | 0.6.0 |", "§10 still lists a raphael-api-admin row")]
    [InlineData("and the events a pillar `off` ends.", "and nothing else.", "§5a's refusal rule lacks Business rules 2's exceptions")]
    [InlineData("after a failed save and the events a pillar `off` ends (§5a). `internal`", "(§5a). Nothing changed. `internal`", "§4's io row lacks Business rules 2's exceptions")]
    [InlineData("`nothing_to_purge`,", "", "§5a's reason list lacks nothing_to_purge")]
    [InlineData("threw, possibly after it changed state", "threw before it changed state", "§5a does not say an internal failure may leave the action applied")]
    [InlineData("`internal` may leave the action partly or fully applied; re-read state (§5a). ", "", "§4's io row does not say internal may leave the action applied")]
    [InlineData("When the handshake reports `api>=4`, Raphael sends the admin actions as their §5a wire twins", "Raphael changes things by sending the human commands",
        "§5 does not send api 4 clients to the §5a twins")]
    public void ContractApi4_fails_when_contract_regresses(string find, string replace, string problem)
    {
        var contract = Contract.Replace("\r\n", "\n");
        Assert.Contains(find, contract);
        Assert.Contains(problem, Api4Problems(contract.Replace(find, replace), Handoff, Design));
    }

    [Fact]
    public void ContractApi4_fails_when_handoff_or_design_lags()
    {
        Assert.Contains("the handoff lacks the 0.5.2 line", Api4Problems(Contract, Handoff.Replace("api 4 ships in Nyarlathotep 0.5.2", "api 4 is planned"), Design));
        Assert.Contains("the handoff still calls api 3 current", Api4Problems(Contract, Handoff + "\nthe wire Nyarlathotep ships today (**api 3**)", Design));
        Assert.Contains("the handoff still calls api 3 current", Api4Problems(Contract, Handoff + "\n## What exists today (api 3, Nyarlathotep 0.5.0)", Design));
        Assert.Contains("the handoff lacks the internal rule", Api4Problems(Contract, Handoff.Replace("Nothing is rolled back, so re-read state", "Try again"), Design));
        Assert.Contains("design § 6 lacks `.nyar api killswitch`", Api4Problems(Contract, Handoff, Design.Replace("`.nyar api killswitch`", "`.nyar api ks`")));
    }

    [Fact]
    public void ContractApi4_empty_contract()
    {
        var p = Api4Problems("", "", "");
        Assert.Contains("the current api is not the code's", p);
        Assert.Contains("no Tags and commands table", p);
        Assert.Contains("no §5a Admin action twins — IMPLEMENTED (api 4)", p);
    }

    [Theory]
    [InlineData(".nyar api event")]
    [InlineData(".nyar api template use")]
    [InlineData(".nyar api template info")]
    [InlineData(".nyar api templates")]
    [InlineData(".nyar api pillar list")]
    [InlineData(".nyar api pillar")]
    [InlineData(".nyar api purge")]
    [InlineData(".nyar api killswitch")]
    public void The_design_doc_command_table_lists_the_api_4_commands(string command) =>
        Assert.Contains(command, DocumentedCommands());
}
