using System.Reflection;
using System.Text.RegularExpressions;

namespace Nyarlathotep.Tests;

/// <summary>event-library D31: the control list parsed from docs/dod/event-library.md (copied to the test output) and
/// ControlCases.Table agree both ways, every named method exists as a [Fact] or [Theory] of its class, every named
/// fixture under tools/ exists, and every method of the twelve classes that follows the naming forms is named by a row.</summary>
public class ControlCaseTests
{
    static readonly Regex Form = new(@"^(?<name>[A-Za-z0-9]+)_(?<kind>fails_when|passes|empty)_(?<input>.+)$");

    static string PlanText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "event-library.md"));

    /// <summary>The control ids of a plan: every `## Definition of Done` item whose evidence is test: or cmd: with a
    /// `(fails when: …)` clause.</summary>
    internal static List<string> PlanControls(string plan)
    {
        var dod = Regex.Match(plan, @"(?ms)^## Definition of Done\s*$(.*?)(?=^## |\z)").Groups[1].Value;
        return Regex.Matches(dod, @"(?m)^- \[[ x]\] (D\d+) · .*? · (?:test|cmd): .*\(fails when: .*$")
            .Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>The test methods of a class of this assembly, or null when there is no such class.</summary>
    static IReadOnlyList<string>? TestMethods(string cls)
    {
        var type = typeof(ControlCaseTests).Assembly.GetType($"Nyarlathotep.Tests.{cls}");
        return type?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<FactAttribute>().Any())                     // TheoryAttribute derives from FactAttribute
            .Select(m => m.Name).ToList();
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("no repository root above the test output");
    }

    /// <summary>Every way the table and the plan disagree; empty when they agree.</summary>
    internal static List<string> Problems(IReadOnlyList<ControlRow> table, IReadOnlyList<string> planIds, Func<string, IReadOnlyList<string>?> methodsOf,
        string repoRoot)
    {
        var p = new List<string>();
        if (planIds.Count == 0) p.Add("the plan lists no control id");
        foreach (var id in planIds.Distinct().Where(id => table.All(r => r.Control != id))) p.Add($"control {id} has no row");
        foreach (var r in table.Where(r => !planIds.Contains(r.Control))) p.Add($"row {r.Name} names {r.Control}, which the plan does not list as a control");

        var named = new HashSet<(string Class, string Method)>();
        foreach (var r in table)
        {
            if (r.Bad.Length == 0 || r.Good.Length == 0 || r.Empty.Length == 0) p.Add($"row {r.Name} lacks its bad, good or empty case");
            if (r.Kind == "cmd")
            {
                foreach (var path in r.Bad.Concat(r.Good).Concat(r.Empty).Where(x => x.StartsWith("tools/", StringComparison.Ordinal)))
                    if (!File.Exists(Path.Combine(repoRoot, path)) && !Directory.Exists(Path.Combine(repoRoot, path))) p.Add($"fixture {path} does not exist");
                continue;
            }
            var methods = methodsOf(r.Class);
            if (methods is null) { p.Add($"class {r.Class} does not exist"); continue; }
            foreach (var (list, kind) in new[] { (r.Bad, "fails_when"), (r.Good, "passes"), (r.Empty, "empty") })
                foreach (var m in list)
                {
                    named.Add((r.Class, m));
                    var f = Form.Match(m);
                    if (!f.Success || f.Groups["kind"].Value != kind || f.Groups["name"].Value != r.Name) p.Add($"{r.Class}.{m} is not named {r.Name}_{kind}_<input>");
                    if (!methods.Contains(m)) p.Add($"{r.Class}.{m} is not a [Fact] or [Theory]");
                }
        }

        foreach (var cls in ControlCases.NewClasses.Concat(ControlCases.ExistingClasses))
        {
            var methods = methodsOf(cls);
            if (methods is null) { p.Add($"class {cls} does not exist"); continue; }
            if (table.All(r => r.Class != cls)) p.Add($"class {cls} has no row");
            foreach (var m in methods)
            {
                if (Form.IsMatch(m)) { if (!named.Contains((cls, m))) p.Add($"{cls}.{m} is named by no row"); }
                else if (ControlCases.NewClasses.Contains(cls)) p.Add($"{cls}.{m} follows none of the three forms");
            }
        }
        return p;
    }

    // ---- D31 ControlCases

    [Fact]
    public void ControlCases_passes_plan_and_table_agree()
    {
        var ids = PlanControls(PlanText);
        Assert.Equal(
            ["D1", "D2", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D12", "D13", "D14", "D16", "D17", "D18", "D19", "D20", "D24",
             "D27", "D28", "D29", "D31", "D30", "D32", "D33", "D34", "D36"],
            ids);
        Assert.Empty(Problems(ControlCases.Table, ids, TestMethods, RepoRoot()));
    }

    [Theory]
    [InlineData("drop a control", "control D13 has no row")]
    [InlineData("unknown control", "row Ghost names D99, which the plan does not list as a control")]
    [InlineData("manual control", "row Manual names D3, which the plan does not list as a control")]
    [InlineData("no empty case", "row PillarMap lacks its bad, good or empty case")]
    [InlineData("missing method", "PillarSwitchTests.PillarMap_passes_nothing is not a [Fact] or [Theory]")]
    [InlineData("bad empty name", "PillarSwitchTests.PillarMap_empty_ is not named PillarMap_empty_<input>")]
    [InlineData("missing fixture", "fixture tools/preflight-fixtures/Nowhere/bad does not exist")]
    [InlineData("unnamed method", "PillarSwitchTests.PillarMap_fails_when_map_and_keys_differ is named by no row")]
    [InlineData("missing class", "class GhostTests does not exist")]
    [InlineData("class without row", "class ReadinessTests has no row")]
    [InlineData("method off form", "ReadinessTests.Lists_page_one follows none of the three forms")]
    public void ControlCases_fails_when_table_breaks_a_rule(string plant, string problem)
    {
        var table = ControlCases.Table.ToList();
        var ids = PlanControls(PlanText);
        Func<string, IReadOnlyList<string>?> methods = TestMethods;
        int At(string name) => table.FindIndex(r => r.Name == name);
        switch (plant)
        {
            case "drop a control": table.RemoveAt(At("PillarMap")); break;
            case "unknown control": table.Add(table[0] with { Control = "D99", Name = "Ghost" }); break;
            case "manual control": table.Add(table[0] with { Control = "D3", Name = "Manual" }); break;
            case "no empty case": table[At("PillarMap")] = table[At("PillarMap")] with { Empty = [] }; break;
            case "missing method": table[At("PillarMap")] = table[At("PillarMap")] with { Good = ["PillarMap_passes_nothing"] }; break;
            case "bad empty name": table[At("PillarMap")] = table[At("PillarMap")] with { Empty = ["PillarMap_empty_"] }; break;
            case "missing fixture": table.Add(new ControlRow("D18", "Nowhere", "cmd", "x", ["tools/preflight-fixtures/Nowhere/bad"], ["g"], ["e"])); break;
            case "unnamed method": table[At("PillarMap")] = table[At("PillarMap")] with { Bad = ["PillarMap_fails_when_nothing"] }; break;
            case "missing class": table[At("PillarMap")] = table[At("PillarMap")] with { Class = "GhostTests" }; break;
            case "class without row": table.RemoveAll(r => r.Class == "ReadinessTests"); break;
            default: methods = cls => cls == "ReadinessTests" ? TestMethods(cls)!.Append("Lists_page_one").ToList() : TestMethods(cls); break;
        }
        Assert.Contains(problem, Problems(table, ids, methods, RepoRoot()));
    }

    [Fact]
    public void ControlCases_empty_plan_without_controls()
    {
        var ids = PlanControls("# a plan\n\n## Definition of Done\n- [ ] D1 · **Manual only** seen in game · manual: Session 1\n");
        Assert.Empty(ids);
        Assert.Contains("the plan lists no control id", Problems(ControlCases.Table, ids, TestMethods, RepoRoot()));
    }
}
