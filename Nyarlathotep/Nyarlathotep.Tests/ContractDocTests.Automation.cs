using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D17: the contract at api 6 lists the three new trigger values and the api 6 change-log row; each new
/// trigger type renders as its own value, never manual; the child adds no tag, push kind or command.</summary>
public partial class ContractDocTests
{
    static readonly (TriggerType Type, string Wire)[] AutomationTriggers =
        [(TriggerType.Interval, "interval"), (TriggerType.RegionEntered, "regionentered"), (TriggerType.FactionKills, "factionkills")];

    /// <summary>What keeps a contract text from describing api 6 as built; empty when it does.</summary>
    internal static List<string> AutomationProblems(string contract)
    {
        var p = new List<string>();
        var current = Regex.Match(contract, @"\*\*Current api:\*\* (\d+)");
        if (!current.Success || int.Parse(current.Groups[1].Value) != 6) p.Add("the current api is not 6");
        var values = Regex.Match(contract, @"(?m)^- `trigger` ∈ (.*(?:\r?\n  .*)*)").Groups[1].Value;
        foreach (var (_, wire) in AutomationTriggers)
            if (!Regex.IsMatch(values, $@"(?<![a-z]){wire}(?![a-z])")) p.Add($"the trigger values lack {wire}");
        if (!Regex.IsMatch(contract, @"(?m)^\| 6 \| 0\.8\.0 \(automation\) \|")) p.Add("the change log has no api 6 row");
        if (!Regex.IsMatch(contract, @"(?m)^## 10\. api 7 and later — PLANNED")) p.Add("§10 is not api 7 and later");
        return p;
    }

    [Fact]
    public void Automation_fails_when_contract_lacks_value()
    {
        foreach (var (_, wire) in AutomationTriggers)
            Assert.Contains($"the trigger values lack {wire}", AutomationProblems(Regex.Replace(Contract, $@"(?<![a-z]){wire}(?![a-z])", "x")));
        Assert.Contains("the change log has no api 6 row", AutomationProblems(Contract.Replace("| 6 | 0.8.0 (automation) |", "| 6 | 0.8.0 |")));
        Assert.Contains("the current api is not 6", AutomationProblems(Contract.Replace("**Current api:** 6", "**Current api:** 5")));
    }

    [Fact]
    public void Automation_fails_when_trigger_renders_as_manual()
    {
        foreach (var (type, wire) in AutomationTriggers) Assert.Equal(wire, ApiLines.Trigger(type));
        // every trigger type has a value of its own, so none falls through to manual
        var all = Enum.GetValues<TriggerType>().Select(ApiLines.Trigger).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void Automation_passes_api_6_and_values()
    {
        Assert.Equal(6, Wire.Api);
        Assert.Empty(AutomationProblems(Contract));
        // no tag, key, push kind or command is added: no row of the table is new at api 6
        Assert.DoesNotContain(Table(), kv => kv.Value.Api.Trim() == "6");
    }

    [Fact]
    public void Automation_empty_contract() =>
        Assert.Equal(["the current api is not 6", "the trigger values lack interval", "the trigger values lack regionentered",
            "the trigger values lack factionkills", "the change log has no api 6 row", "§10 is not api 7 and later"], AutomationProblems(""));
}
