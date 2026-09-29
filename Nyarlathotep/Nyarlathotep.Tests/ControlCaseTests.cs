using System.Reflection;
using System.Text.RegularExpressions;

namespace Nyarlathotep.Tests;

/// <summary>event-library D31, walkable-spawns D9: the control lists parsed from every plan of ControlCases.Plans (each
/// copied to the test output) and ControlCases.Table agree both ways, keyed by plan slug and D-id, every named method exists as a [Fact] or [Theory] of its class, every named
/// fixture under tools/ exists, and every method of the twelve classes that follows the naming forms is named by a row.</summary>
public class ControlCaseTests
{
    static readonly Regex Form = new(@"^(?<name>[A-Za-z0-9]+)_(?<kind>fails_when|passes|empty)_(?<input>.+)$");

    static string PlanText(string slug) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", $"{slug}.md"));

    /// <summary>A plan's audit record as copied to the test output, or null when it was not copied.</summary>
    static string? AuditText(string slug)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "audits", $"{slug}.md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>The mod's csproj Version.</summary>
    static string ModVersion() =>
        Regex.Match(File.ReadAllText(Path.Combine(RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Nyarlathotep.csproj")), "<Version>([^<]+)</Version>").Groups[1].Value;

    /// <summary>What is wrong with the pending controls (event-spawns D22, A22, A29): a pending key with a row, one the plan
    /// does not list as a control, a missing audit record, one whose step's post-audit heading ("### Step n" under
    /// "## Post-audit") exists, and a step-4 one once the Version is the plan's release.</summary>
    internal static List<string> PendingProblems(IReadOnlyList<PendingControl> pending, IReadOnlyList<ControlRow> table,
        IReadOnlyList<string> planIds, Func<string, string?> auditOf, string version)
    {
        var p = new List<string>();
        foreach (var c in pending)
        {
            if (table.Any(r => r.Key == c.Key)) p.Add($"pending {c.Key} has a row");
            if (!planIds.Contains(c.Key)) p.Add($"pending {c.Key} is not a control of the plan");
            var audit = auditOf(c.Plan);
            if (audit is null) { p.Add($"audit file missing: docs/audits/{c.Plan}.md"); continue; }
            var post = Regex.Match(audit.Replace("\r\n", "\n"), @"(?ms)^## Post-audit\s*$(.*?)(?=^## |\z)").Groups[1].Value;
            if (Regex.IsMatch(post, $@"(?m)^### Step {c.Step}\b")) p.Add($"pending {c.Key} outlived step {c.Step}'s post-audit");
            if (c.Step == 4 && version == c.Release) p.Add($"pending {c.Key} is still pending at release {c.Release}");
        }
        return p.Distinct().ToList();
    }

    /// <summary>The keys ("&lt;slug&gt; &lt;D-id&gt;") of every listed plan's controls, plan by plan.</summary>
    static List<string> AllPlanKeys() => ControlCases.Plans.SelectMany(slug => PlanControls(PlanText(slug)).Select(id => $"{slug} {id}")).ToList();

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
        foreach (var id in planIds.Distinct().Where(id => table.All(r => r.Key != id) && ControlCases.Pending.All(c => c.Key != id)))
            p.Add($"control {id} has no row");
        foreach (var r in table.Where(r => !planIds.Contains(r.Key))) p.Add($"row {r.Name} names {r.Key}, which the plan does not list as a control");

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
        Assert.Equal(
            ["D1", "D2", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D12", "D13", "D14", "D16", "D17", "D18", "D19", "D20", "D24",
             "D27", "D28", "D29", "D31", "D30", "D32", "D33", "D34", "D36"],
            PlanControls(PlanText(ControlCases.EventLibrary)));
        Assert.Equal(["D2", "D3", "D5", "D6", "D7", "D9", "D10", "D11", "D12"], PlanControls(PlanText(ControlCases.WalkableSpawns)));
        Assert.Equal(["D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D12", "D14", "D15", "D16"],
            PlanControls(PlanText(ControlCases.RaphaelApiAdmin)));
        Assert.Equal(["D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D13", "D14", "D15", "D16"],
            PlanControls(PlanText(ControlCases.Regions)));
        Assert.Equal(["D6", "D8", "D9", "D11", "D13", "D16", "D17", "D18", "D19", "D20", "D21", "D22", "D24", "D25", "D26", "D27", "D29", "D30", "D31",
             "D32", "D33", "D34"],
            PlanControls(PlanText(ControlCases.EventSpawns)));
        Assert.Empty(Problems(ControlCases.Table, AllPlanKeys(), TestMethods, RepoRoot()));
        Assert.Empty(PendingProblems(ControlCases.Pending, ControlCases.Table, AllPlanKeys(), AuditText, ModVersion()));
    }

    static readonly PendingControl D19 = ControlCases.Pending.Single(c => c.Key == "event-spawns D19");
    static readonly PendingControl D25 = ControlCases.Pending.Single(c => c.Key == "event-spawns D25");
    const string CleanAudit = "# Audit\n\n## Pre-audit\n\n### Step 1 · x\n\n## Post-audit\n\n### Step 1 · x\n";

    [Fact]
    public void ControlCases_fails_when_pending_key_has_a_row()
    {
        var table = ControlCases.Table.Append(new ControlRow("D19", "Failures", "test", "DependencyFailureTests", ["x"], ["y"], ["z"], ControlCases.EventSpawns)).ToList();
        Assert.Contains("pending event-spawns D19 has a row", PendingProblems([D19], table, AllPlanKeys(), _ => CleanAudit, "0.6.0"));
    }

    [Fact]
    public void ControlCases_fails_when_pending_key_is_no_control() =>
        Assert.Contains("pending event-spawns D99 is not a control of the plan",
            PendingProblems([D19 with { Control = "D99" }], ControlCases.Table, AllPlanKeys(), _ => CleanAudit, "0.6.0"));

    [Fact]
    public void ControlCases_fails_when_pending_after_its_post_audit()
    {
        var audit = CleanAudit + "\n### Step 3 · 2026-09-30 · abc\n";
        Assert.Contains("pending event-spawns D19 outlived step 3's post-audit", PendingProblems([D19], ControlCases.Table, AllPlanKeys(), _ => audit, "0.6.0"));
        Assert.Empty(PendingProblems([D19], ControlCases.Table, AllPlanKeys(), _ => "# Audit\n\n## Pre-audit\n\n### Step 3 · x\n\n## Post-audit\n", "0.6.0"));
    }

    [Fact]
    public void ControlCases_fails_when_audit_missing() =>
        Assert.Contains("audit file missing: docs/audits/event-spawns.md", PendingProblems([D19], ControlCases.Table, AllPlanKeys(), _ => null, "0.6.0"));

    [Fact]
    public void ControlCases_fails_when_pending_at_release()
    {
        Assert.Contains("pending event-spawns D25 is still pending at release 0.7.0", PendingProblems([D25], ControlCases.Table, AllPlanKeys(), _ => CleanAudit, "0.7.0"));
        Assert.Empty(PendingProblems([D25], ControlCases.Table, AllPlanKeys(), _ => CleanAudit, "0.6.0"));
    }

    [Theory]
    [InlineData("drop a control", "control event-library D13 has no row")]
    [InlineData("unknown control", "row Ghost names event-library D99, which the plan does not list as a control")]
    [InlineData("manual control", "row Manual names event-library D3, which the plan does not list as a control")]
    [InlineData("drop a walkable control", "control walkable-spawns D5 has no row")]
    [InlineData("row under the other plan", "row WavePoints names event-library D3, which the plan does not list as a control")]
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
        var ids = AllPlanKeys();
        Func<string, IReadOnlyList<string>?> methods = TestMethods;
        int At(string name) => table.FindIndex(r => r.Name == name);
        switch (plant)
        {
            case "drop a control": table.RemoveAt(At("PillarMap")); break;
            case "unknown control": table.Add(table[0] with { Control = "D99", Name = "Ghost" }); break;
            case "manual control": table.Add(table[0] with { Control = "D3", Name = "Manual" }); break;
            case "drop a walkable control": table.RemoveAt(At("WalkCheck")); break;
            case "row under the other plan": table[At("WavePoints")] = table[At("WavePoints")] with { Plan = ControlCases.EventLibrary }; break;
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
