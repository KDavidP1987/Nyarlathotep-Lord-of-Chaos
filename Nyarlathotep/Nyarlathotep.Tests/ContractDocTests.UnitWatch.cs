using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D14: the contract's §5 lists `.nyar event unstick &lt;id&gt;`, §5a its twin row and refusals, §9 the
/// api row whose number is Wire.Api, and one sentence says the watch's lines never reach the wire and no line names or
/// locates a player; design §6 lists the command and the three [UnitWatch] keys.</summary>
public partial class ContractDocTests
{
    const string UnstickHuman = "| Unstick a running event's units | `.nyar event unstick <id>` | admin |";
    const string UnstickTwin = "| `.nyar api event unstick <id>` | `[NYAR:ok] cmd=event verb=unstick id=<id> marked=<n> fighting=<f>` (api 8) |";
    const string UnstickRefusals = "It refuses\n  with `code=notfound arg=id` for an unknown id, `code=state arg=id reason=not_active` when the event is not running,\n  `code=state arg=id` when it has no live unit, and `code=invalid reason=watch_off`";
    const string UnstickSentence = "The watch's lines never reach the wire, and no line names or locates a player.";

    /// <summary>What keeps the contract and the design from describing unit watch at api <paramref name="api"/>; empty when
    /// they do.</summary>
    internal static List<string> UnitWatchProblems(string contract, string design, int api)
    {
        var p = new List<string>();
        var c = contract.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!c.Contains(UnstickHuman, StringComparison.Ordinal)) p.Add("§5 misses the human command");
        if (!c.Contains(UnstickTwin, StringComparison.Ordinal)) p.Add("§5a misses the twin row");
        if (!c.Contains(UnstickRefusals, StringComparison.Ordinal)) p.Add("§5a misses the refusals");
        if (!Regex.IsMatch(c, @"from api 8, `watch_off`")) p.Add("the reasons miss watch_off");
        if (!c.Contains(UnstickSentence, StringComparison.Ordinal)) p.Add("the no-wire sentence is missing");
        var row = Regex.Match(c, @"(?m)^\| (\d+) \| 0\.13\.0 \(unit-watch\) \| The twin `event unstick` \(§5a\) and the reason `watch_off`; no new tag, key or push kind\. \|$");
        if (!row.Success) p.Add("§9 misses the unit-watch row");
        else if (int.Parse(row.Groups[1].Value) != api) p.Add($"§9's unit-watch row is api {row.Groups[1].Value}, not {api}");
        var d = design.Replace("\r\n", "\n", StringComparison.Ordinal);
        var six = Regex.Match(d, @"(?m)^\| `\.nyar event unstick <id>` \| admin \| (.*) \| unit-watch \|$");
        if (!six.Success) p.Add("design §6 misses the command");
        else
            foreach (var key in new[] { "`[UnitWatch]`", "`Enabled` (true)", "`StuckSeconds` (90, 30-600)", "`StallMinutes` (10, 0-60" })
                if (!six.Groups[1].Value.Contains(key, StringComparison.Ordinal)) p.Add($"design §6 misses {key}");
        return p;
    }

    [Fact]
    public void UnitWatch_fails_when_a_row_or_the_sentence_is_missing()
    {
        Assert.Equal(["§5 misses the human command"], UnitWatchProblems(Contract.Replace(UnstickHuman, "", StringComparison.Ordinal), Design, Wire.Api));
        Assert.Equal(["§5a misses the twin row"], UnitWatchProblems(Contract.Replace(UnstickTwin, "", StringComparison.Ordinal), Design, Wire.Api));
        Assert.Equal(["the no-wire sentence is missing"], UnitWatchProblems(Contract.Replace(UnstickSentence, "", StringComparison.Ordinal), Design, Wire.Api));
        Assert.Equal(["the reasons miss watch_off"], UnitWatchProblems(Contract.Replace("from api 8, `watch_off`", "", StringComparison.Ordinal), Design, Wire.Api));
        Assert.Equal(["§9 misses the unit-watch row"],
            UnitWatchProblems(Contract.Replace("| 0.13.0 (unit-watch) |", "| 0.13.0 |", StringComparison.Ordinal), Design, Wire.Api));
        Assert.Equal(["§5a misses the refusals"],
            UnitWatchProblems(Contract.Replace("`code=invalid reason=watch_off` when", "`code=state` when", StringComparison.Ordinal), Design, Wire.Api));
    }

    [Fact]
    public void UnitWatch_fails_when_the_api_differs()
    {
        Assert.Equal(["§9's unit-watch row is api 8, not 9"], UnitWatchProblems(Contract, Design, 9));
        Assert.Equal(8, Wire.Api);
        Assert.Contains("> **Current api:** 8", Contract, StringComparison.Ordinal);
    }

    [Fact]
    public void UnitWatch_fails_when_the_design_rows_differ()
    {
        Assert.Equal(["design §6 misses the command"], UnitWatchProblems(Contract, Design.Replace("| `.nyar event unstick <id>` | admin |", "| `.nyar event unstick` | admin |", StringComparison.Ordinal), Wire.Api));
        Assert.Equal(["design §6 misses `StuckSeconds` (90, 30-600)"],
            UnitWatchProblems(Contract, Design.Replace("`StuckSeconds` (90, 30-600)", "`StuckSeconds` (60, 30-600)", StringComparison.Ordinal), Wire.Api));
        Assert.Equal(["design §6 misses `Enabled` (true)"], UnitWatchProblems(Contract, Design.Replace("`Enabled` (true)", "`Enabled` (false)", StringComparison.Ordinal), Wire.Api));
    }

    [Fact]
    public void UnitWatch_passes_contract_and_design() => Assert.Empty(UnitWatchProblems(Contract, Design, Wire.Api));

    [Fact]
    public void UnitWatch_empty_documents() => Assert.Equal(7, UnitWatchProblems("", "", Wire.Api).Count);
}
