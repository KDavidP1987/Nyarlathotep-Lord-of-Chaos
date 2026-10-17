using System.Text.RegularExpressions;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D14: the mod never decides who fights whom. No code in Logic/ or Services/ writes a unit's
/// FactionReference, Team, TeamReference or aggro category (EntityCategory, Aggroable); the undead fight whom the game
/// makes hostile to them. Reads are fine (EmpowerAction reads FactionReference and Team to choose units).</summary>
public class SourceTests
{
    static readonly string[] Guarded = ["FactionReference", "Team", "TeamReference", "EntityCategory", "Aggroable"];

    /// <summary>Every write of a guarded component in <paramref name="files"/>: a typed write or add
    /// (Write&lt;T&gt;, AddComponent&lt;T&gt;, SetComponentData&lt;T&gt;), a constructed value (new T { or new T(), or a
    /// variable read with TryGetComponent&lt;T&gt; that is assigned to or written back.</summary>
    internal static List<string> HostilityProblems(IEnumerable<(string Path, string Text)> files)
    {
        var problems = new List<string>();
        var types = string.Join("|", Guarded);
        var typed = new Regex($@"\b(Write|AddComponent|AddComponentData|SetComponentData|AddComponentSafe)<({types})>");
        var built = new Regex($@"\bnew\s+({types})\s*[{{(]");
        var read = new Regex($@"TryGetComponent<({types})>\(out\s+var\s+(\w+)\)");
        foreach (var (path, text) in files)
        {
            var lines = text.Split('\n');
            var vars = read.Matches(text).Select(m => m.Groups[2].Value).Distinct().ToList();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("///", StringComparison.Ordinal)) continue;
                var at = $"{path}:{i + 1}";
                if (typed.Match(line) is { Success: true } t) problems.Add($"{at}: {t.Value}");
                if (built.Match(line) is { Success: true } b) problems.Add($"{at}: {b.Value.TrimEnd('{', '(').Trim()}");
                foreach (var v in vars)
                    if (Regex.IsMatch(line, $@"\.Write\(\s*{v}\s*\)|\b{v}\.\w+(\.\w+)*\s*=[^=]"))
                        problems.Add($"{at}: writes {v}");
            }
        }
        return problems;
    }

    static List<(string Path, string Text)> Plugin()
    {
        var root = Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep");
        return [.. new[] { "Logic", "Services" }.SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories))
            .Select(f => (Path.GetRelativePath(root, f), File.ReadAllText(f)))];
    }

    [Fact]
    public void Hostility_fails_when_Logic_or_Services_writes_a_faction_team_or_aggro_category()
    {
        foreach (var plant in new[]
        {
            "unit.Write<FactionReference>(f);",
            "unit.Write(new FactionReference { FactionGuid = g });",
            "unit.AddComponent<Team>();",
            "var t = new TeamReference();",
            "if (unit.TryGetComponent<Team>(out var team)) { team.Value = 1; }",
            "if (unit.TryGetComponent<EntityCategory>(out var cat)) unit.Write(cat);",
            "unit.SetComponentData<Aggroable>(a);",
        })
            Assert.NotEmpty(HostilityProblems([("x.cs", plant)]));
        var files = Plugin();
        Assert.Contains(files, f => f.Path.EndsWith("EmpowerAction.cs", StringComparison.Ordinal) && f.Text.Contains("TryGetComponent<Team>", StringComparison.Ordinal));
        Assert.Empty(HostilityProblems(files));
    }

    [Fact]
    public void Hostility_passes_reads_and_comparisons() =>
        Assert.Empty(HostilityProblems([("x.cs", """
            if (unit.TryGetComponent<Team>(out var t) && teams.Contains(t.Value)) return true;
            var f = unit.Read<FactionReference>().FactionGuid;
            ComponentType.ReadOnly(Il2CppType.Of<FactionReference>());
            // unit.Write(new Team { Value = 1 }); a comment is not a write
            if (t.Value == 2) return false;
            """)]));

    [Fact]
    public void Hostility_empty_no_files_no_problems() => Assert.Empty(HostilityProblems([]));
}
