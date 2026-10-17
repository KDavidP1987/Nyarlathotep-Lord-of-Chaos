using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D15: undead-infestation and undead-horde-march ship disabled and Manual with the numbers of the
/// plan's Design › Templates, each validates, `.nyar template use` copies it, a start at the default cfg names its cut
/// (D12), and the template tables list them. D14: their units stay inside the undead allied group.</summary>
public partial class TemplateTests
{
    static readonly string[] Troops =
    [
        "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane", "CHAR_Undead_SkeletonApprentice",
        "CHAR_Undead_UndyingGhoul", "CHAR_Undead_Priest", "CHAR_Undead_Ghoul_Armored_Farmlands",
    ];
    static readonly string[] Elite = ["CHAR_Undead_Guardian", "CHAR_Undead_SkeletonMage", "CHAR_Undead_SkeletonGolem"];
    static readonly string[] Strong = ["CHAR_Undead_GhostBanshee", "CHAR_Undead_GhostGuardian"];

    static readonly string[] UndeadHordeIds = ["undead-infestation", "undead-horde-march", "zombie-apocalypse"];

    /// <summary>What differs from Design › Templates (the zombie retune is TemplateLibraryTests.ApocalypseDifferences').</summary>
    static List<string> UndeadHordeDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        foreach (var id in UndeadHordeIds)
        {
            var t = c.Find(id);
            if (t is null) { diffs.Add($"{id} missing"); continue; }
            if (t.Invalid is { } why) diffs.Add($"{id} invalid: {why}");
            if (t.Definition.Enabled) diffs.Add($"{id} enabled");
            if (t.Definition.Trigger.Type != TriggerType.Manual) diffs.Add($"{id} not Manual");
            if (t.Definition.Apocalypse?.Faction != "Faction_Undead") diffs.Add($"{id} not Faction_Undead");
        }
        if (c.Find("undead-infestation")?.Definition is { Apocalypse: { } inf } infDef)
        {
            if (infDef.DurationSeconds != 14400) diffs.Add($"infestation duration {infDef.DurationSeconds}");
            if (inf.Mode != ApocalypseMode.Infestation) diffs.Add("infestation is not an Infestation");
            diffs.AddRange(PoolDifferences("infestation", inf));
            if (!Units(inf, ("troops", 8, 20), ("elite", 2, 40), ("strong", 1, 60))) diffs.Add("infestation units differ");
            if (inf.Hotspots is not { Count: 5, MinNatives: 5, MinSpacing: 300 }) diffs.Add("origins differ");
            if (inf.Roamers is not { PerHotspot: 10, PerKillMin: 1, PerKillMax: 3, Pool: "troops", Max: 300, PerMinute: 20 }) diffs.Add("infestation spread differs");
            if (inf.Infest is not { Hunters: 50, DriftRadius: 400 }) diffs.Add("hunters or drift differ");
        }
        if (c.Find("undead-horde-march")?.Definition is { Apocalypse: { } m } mDef)
        {
            if (mDef.DurationSeconds != 3600) diffs.Add($"march duration {mDef.DurationSeconds}");
            if (m.Mode != ApocalypseMode.HordeMarch || m.March is not { } spec) { diffs.Add("march is not a HordeMarch"); return diffs; }
            diffs.AddRange(PoolDifferences("march", m));
            if (!Units(m, ("troops", 160, 20), ("elite", 32, 40), ("strong", 8, 60))) diffs.Add("march units differ");
            if (spec.Bosses is not [{ Prefab: "CHAR_Undead_BishopOfDeath_VBlood", Count: 1, Pool: null } boss] || boss.Modifiers?.Level != 50)
                diffs.Add("march boss differs");
            if (spec.Start is not { IsHotspot: true, MinDistance: 300 }) diffs.Add("march start differs");
            if (m.Goal is not { Radius: 30, Name: "the living" } || m.Goal.Location.Type != LocationType.Admin) diffs.Add("march destination differs");
            if (m.MarchSpeed != 2.0) diffs.Add($"marchSpeed {m.MarchSpeed}");
            if (spec.Growth is not { PerKillMin: 1, PerKillMax: 3, Pool: "troops", Max: 400 }) diffs.Add("growth differs");
            if (spec.Infest is not { Hunters: 60, DriftRadius: 500 }) diffs.Add("infest differs");
            if (spec.InfestSpread is not { PerKillMin: 1, PerKillMax: 3, Pool: "troops", Max: 400, PerMinute: 20 }) diffs.Add("infest spread differs");
        }
        return diffs;
    }

    static IEnumerable<string> PoolDifferences(string what, ApocalypseAction a)
    {
        foreach (var (name, want) in new[] { ("troops", Troops), ("elite", Elite), ("strong", Strong) })
            if (a.Pools?.GetValueOrDefault(name) is not { } got || !got.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(want.OrderBy(p => p, StringComparer.Ordinal)))
                yield return $"{what} pool {name} differs";
    }

    static bool Units(ApocalypseAction a, params (string Pool, int Count, int Level)[] want) =>
        a.Units.Select(u => (u.Pool, u.Count, u.Modifiers?.Level)).SequenceEqual(want.Select(w => ((string?)w.Pool, w.Count, (int?)w.Level)));

    /// <summary>The template tables that must list both new templates: the Thunderstore README's table, the root README's
    /// feature row, and EVENT_LIBRARY.md's template list.</summary>
    static List<string> TableDifferences(IReadOnlyDictionary<string, string> docs)
    {
        var diffs = new List<string>();
        foreach (var (doc, text) in docs)
            foreach (var id in new[] { "undead-infestation", "undead-horde-march" })
                if (!text.Contains($"`{id}`", StringComparison.Ordinal)) diffs.Add($"{doc} lacks {id}");
        return diffs;
    }

    static Dictionary<string, string> Docs()
    {
        var root = ControlCaseTests.RepoRoot();
        return new[] { "README.md", Path.Combine("Nyarlathotep", "Nyarlathotep", "README.md"), Path.Combine("docs", "features", "EVENT_LIBRARY.md") }
            .ToDictionary(p => p, p => File.ReadAllText(Path.Combine(root, p)));
    }

    // ---- D15 UndeadHordes

    [Fact]
    public void UndeadHordes_passes_the_templates_as_designed()
    {
        var c = TemplateLibraryTests.Real();
        Assert.Empty(UndeadHordeDifferences(c));
        Assert.Empty(TableDifferences(Docs()));
        foreach (var id in UndeadHordeIds)
        {
            // `.nyar template use` copies it, valid and disabled
            var lib = new Library(Json.File(Json.Event("raid")), TemplateLibraryTests.Units());
            Assert.Equal($"template {id} added as {id} (disabled); .nyar event enable {id} to arm it", lib.Write(t => Authoring.TemplateUse(t, c, id, null)));
            var copy = lib.Catalog.Current.Find(id)!;
            Assert.Null(copy.DisabledReason);
            Assert.False(copy.Enabled);
            // a start at the default cfg runs, cut, and names the cut (D12)
            var cut = Precedence.StartCut(copy, Limits.MaxUnitsPerWave.Default, 0, Limits.MaxTrackedUnits.Default);
            Assert.NotNull(cut);
            Assert.StartsWith($"event {id} started; capped: ", AdminLines.Started(id, cut).Human);
            Assert.EndsWith("; raise it in the cfg", AdminLines.Started(id, cut).Human);
        }
    }

    [Theory]
    [InlineData("undead-infestation", "enabled")]
    [InlineData("undead-infestation", "trigger")]
    [InlineData("undead-infestation", "hunters")]
    [InlineData("undead-infestation", "origins")]
    [InlineData("undead-infestation", "spread")]
    [InlineData("undead-infestation", "units")]
    [InlineData("undead-infestation", "invalid")]
    [InlineData("undead-infestation", "missing")]
    [InlineData("undead-horde-march", "enabled")]
    [InlineData("undead-horde-march", "start")]
    [InlineData("undead-horde-march", "growth")]
    [InlineData("undead-horde-march", "infest")]
    [InlineData("undead-horde-march", "boss")]
    [InlineData("undead-horde-march", "units")]
    [InlineData("undead-horde-march", "pool")]
    [InlineData("undead-horde-march", "missing")]
    public void UndeadHordes_fails_when_a_template_differs(string id, string mutation)
    {
        var root = JsonNode.Parse(TemplateLibraryTests.RealText)!;
        var events = (JsonArray)root["events"]!;
        var ev = events.OfType<JsonObject>().First(e => (string)e["id"]! == id);
        var action = (JsonObject)ev["action"]!;
        switch (mutation)
        {
            case "enabled": ev["enabled"] = true; break;
            case "trigger": ev["trigger"] = JsonNode.Parse("{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }"); break;
            case "hunters": action["hunters"] = 60; break;
            case "origins": action["origins"]!["perOrigin"] = 11; break;
            case "spread": action["spread"]!["max"] = 100; break;
            case "units": action["units"]![0]!["count"] = 7; break;
            case "invalid": action["bogus"] = 1; break;
            case "start": action["start"]!["minDistance"] = 500; break;
            case "growth": action["growth"]!["max"] = 200; break;
            case "infest": action["infest"]!["hunters"] = 50; break;
            case "boss": action["bosses"]![0]!["modifiers"]!["level"] = 60; break;
            case "pool": ((JsonArray)action["pools"]!["strong"]!).RemoveAt(1); break;
            default: events.Remove(ev); break;
        }
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes(root.ToJsonString()), TemplateLibraryTests.Units(), TemplateLibraryTests.Units());
        Assert.NotEmpty(UndeadHordeDifferences(c));
    }

    [Theory]
    [InlineData("README.md", "undead-infestation")]
    [InlineData("README.md", "undead-horde-march")]
    [InlineData("Nyarlathotep/Nyarlathotep/README.md", "undead-horde-march")]
    [InlineData("docs/features/EVENT_LIBRARY.md", "undead-infestation")]
    public void UndeadHordes_fails_when_a_table_lacks_a_template(string doc, string id)
    {
        var docs = Docs();
        var key = docs.Keys.Single(k => k.Replace('\\', '/') == doc);
        docs[key] = docs[key].Replace($"`{id}`", "`gone`", StringComparison.Ordinal);
        Assert.Contains($"{key} lacks {id}", TableDifferences(docs));
    }

    [Fact]
    public void UndeadHordes_empty_an_empty_catalogue_misses_all_three()
    {
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes("{\"SchemaVersion\":1,\"events\":[]}"), TemplateLibraryTests.Units(), TemplateLibraryTests.Units());
        Assert.Equal(["undead-infestation missing", "undead-horde-march missing", "zombie-apocalypse missing"], UndeadHordeDifferences(c));
    }

    // ---- D14 Groups

    [Fact]
    public void Groups_passes_the_new_templates_inside_the_undead_group()
    {
        var c = TemplateLibraryTests.Real();
        Assert.Empty(TemplateLibraryTests.FactionProblems(c));
        foreach (var id in new[] { "undead-infestation", "undead-horde-march" })
        {
            var a = c.Find(id)!.Definition.Apocalypse!;
            var prefabs = a.Pools!.Values.SelectMany(p => p).Concat(a.March?.Bosses?.Select(b => b.Prefab) ?? []).ToList();
            Assert.NotEmpty(prefabs);
            Assert.Null(FactionGroups.Problem(id, prefabs, a.Faction));
        }
    }

    [Theory]
    [InlineData("undead-infestation", "troops", "CHAR_Bandit_Thug")]
    [InlineData("undead-horde-march", "elite", "CHAR_Militia_Guard")]
    public void Groups_fails_when_a_new_template_mixes_groups(string id, string pool, string stranger)
    {
        var root = JsonNode.Parse(TemplateLibraryTests.RealText)!;
        var ev = ((JsonArray)root["events"]!).OfType<JsonObject>().First(e => (string)e["id"]! == id);
        ((JsonArray)ev["action"]!["pools"]![pool]!).Add(stranger);
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes(root.ToJsonString()), TemplateLibraryTests.Units(stranger), TemplateLibraryTests.Units(stranger));
        Assert.Contains(TemplateLibraryTests.FactionProblems(c), p => p.StartsWith(id, StringComparison.Ordinal));
    }

    [Fact]
    public void Groups_empty_no_prefabs_no_problem() => Assert.Null(FactionGroups.Problem("z", [], "Faction_Undead"));
}
