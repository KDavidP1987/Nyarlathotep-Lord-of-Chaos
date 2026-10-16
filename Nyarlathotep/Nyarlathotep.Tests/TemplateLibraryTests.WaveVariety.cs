using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D15, D16: the seven timed and seven onslaught templates, disabled and valid, each at an Admin
/// location with arena 40, Hunt 40, the scoreboard, the K/D winner and pursuit after 60 s; every later wave on a fixed beat
/// of 60-90 s with no whenCleared; no wave over the default MaxUnitsPerWave of 20 and no run over MaxTrackedUnits 150 with
/// every wave left alive (an onslaught at its roll maxima, Review 3 F3); one allied group each (D4, D14). An onslaught
/// draws from its faction's pools, rolls exactly one boss in its middle wave and 1-2 in its final one (1-1 when its boss
/// pool holds one boss).</summary>
public partial class TemplateLibraryTests
{
    static readonly string[] Timed =
    [
        "spider-swarm-timed", "graveyard-waves-timed", "militia-muster-timed", "silverlight-crusade-timed", "legion-incursion-timed",
        "gloomrot-assault-timed", "cursed-night-timed",
    ];

    static readonly (string Id, string Group)[] Onslaughts =
    [
        ("spider-onslaught", "spiders"), ("undead-onslaught", "undead"), ("militia-onslaught", "militia"), ("silverlight-onslaught", "church"),
        ("legion-onslaught", "legion"), ("gloomrot-onslaught", "gloomrot"), ("cursed-onslaught", "cursed"),
    ];

    /// <summary>The shape every wave-variety template shares; the action, or null when there is none to read.</summary>
    static SpawnWavesAction? VarietyShape(TemplateCatalog c, string id, List<string> diffs)
    {
        var t = c.Find(id);
        if (t is null) { diffs.Add($"{id} missing"); return null; }
        if (t.Invalid is { } why) diffs.Add($"{id} invalid: {why}");
        if (t.Definition.Enabled) diffs.Add($"{id} enabled");
        if (t.Definition.Action is not { WaveList: { Count: > 1 } list } a) { diffs.Add($"{id} has no wave list"); return null; }
        if (a.Location.Type != LocationType.Admin) diffs.Add($"{id} not at an Admin location");
        if (a.Arena is not { Radius: 40 }) diffs.Add($"{id} has no arena of 40");
        if (a.Behaviour is not { Type: BehaviourType.Hunt, Range: 40 }) diffs.Add($"{id} has no Hunt of 40");
        if (!a.Scoreboard || a.Winner != WinnerRule.KillDeath) diffs.Add($"{id} has no scoreboard and K/D winner");
        if (a.Pursuit is not { AfterSeconds: 60 }) diffs.Add($"{id} has no pursuit after 60 s");
        for (var i = 1; i < list.Count; i++)
        {
            if (list[i].WhenCleared) diffs.Add($"{id} wave {i + 1} waits for a clear");
            if (list[i].AfterSeconds is not (>= 60 and <= 90)) diffs.Add($"{id} wave {i + 1} is not on a 60-90 s beat");
        }
        var run = 0;
        for (var w = 1; w <= list.Count; w++)
        {
            var size = a.UnitsOf(w).Sum(u => u.Count);
            if (size > 20) diffs.Add($"{id} wave {w} passes 20 units");
            run += size + (Pools.BossesOf(a, w) ?? []).Sum(b => b.IsRoll ? b.Max : b.Count);
        }
        if (run > 150) diffs.Add($"{id} run passes 150 units ({run})");
        if (FactionGroups.Problem(id, a) is { } p) diffs.Add(p);
        return a;
    }

    static List<string> TimedDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        foreach (var id in Timed)
            if (VarietyShape(c, id, diffs) is { } a && a.Pools is not null) diffs.Add($"{id} draws from pools");
        return diffs;
    }

    static List<string> OnslaughtDifferences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        foreach (var (id, group) in Onslaughts)
        {
            if (VarietyShape(c, id, diffs) is not { WaveList: { } list } a) continue;
            if (a.Pools is not { } pools || !pools.ContainsKey("lords")) { diffs.Add($"{id} has no unit and boss pools"); continue; }
            foreach (var (name, members) in pools)
                foreach (var m in members.Where(m => FactionGroups.Of(m).All(g => g.Name != group)))
                    diffs.Add($"{id} pool {name}: {m} is not in the {group} group");
            if (list.SelectMany(w => w.Units).Any(u => u.Pool is null)) diffs.Add($"{id} has a unit outside its pools");
            var mid = (list.Count + 1) / 2;
            var finalMax = Math.Min(2, pools["lords"].Count);
            for (var w = 1; w <= list.Count; w++)
            {
                var bosses = Pools.BossesOf(a, w) ?? [];
                var want = w == mid ? (1, 1) : w == list.Count ? (1, finalMax) : ((int, int)?)null;
                if (want is null) { if (bosses.Count > 0) diffs.Add($"{id} wave {w} has bosses"); continue; }
                if (bosses is not [{ IsRoll: true, Pool: "lords" } r]) { diffs.Add($"{id} wave {w} has no boss roll"); continue; }
                if ((r.Min, r.Max) != want) diffs.Add($"{id} wave {w} rolls {r.Min}-{r.Max} bosses, not {want.Value.Item1}-{want.Value.Item2}");
            }
        }
        return diffs;
    }

    /// <summary>The real file with <paramref name="edit"/> applied to template <paramref name="id"/>'s JSON.</summary>
    static TemplateCatalog Edited(string id, Action<JsonObject, JsonArray> edit)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        edit(events.OfType<JsonObject>().First(e => (string)e["id"]! == id), events);
        return TemplateCatalog.Load(Bytes(root.ToJsonString()), Units("CHAR_Pool_Nobody"), Units("CHAR_Pool_Nobody"));
    }

    static JsonArray WavesOf(JsonObject e) => (JsonArray)e["action"]!["waveList"]!;

    [Fact]
    public void Timed_passes_the_seven_templates()
    {
        Assert.Empty(TimedDifferences(Real()));
        var a = Real().Find("militia-muster-timed")!.Definition.Action!;
        Assert.Equal(Real().Find("militia-muster")!.Definition.Action!.WaveList!.Select(w => w.Units.Sum(u => u.Count)),
            a.WaveList!.Select(w => w.Units.Sum(u => u.Count)));                                                    // the base waves, on a beat
    }

    [Theory]
    [InlineData("missing", "militia-muster-timed missing")]
    [InlineData("enabled", "militia-muster-timed enabled")]
    [InlineData("cleared", "militia-muster-timed wave 2 waits for a clear")]
    [InlineData("wave", "militia-muster-timed wave 1 passes 20 units")]
    [InlineData("run", "militia-muster-timed run passes 150 units")]
    [InlineData("pursuit", "militia-muster-timed has no pursuit after 60 s")]
    [InlineData("mix", "militia-muster-timed wave 1 mixes the")]
    public void Timed_fails_when_a_template_breaks_the_rule(string mutation, string problem)
    {
        var c = Edited("militia-muster-timed", (e, all) =>
        {
            switch (mutation)
            {
                case "missing": all.Remove(e); break;
                case "enabled": e["enabled"] = true; break;
                case "cleared": WavesOf(e)[1]!["whenCleared"] = true; break;
                case "wave": WavesOf(e)[0]!["units"]![0]!["count"] = 21; break;
                case "run": foreach (var w in WavesOf(e)) w!["units"]![0]!["count"] = 35; break;
                case "pursuit": ((JsonObject)e["action"]!).Remove("pursuit"); break;
                case "mix": WavesOf(e)[0]!["units"]![0]!["prefab"] = "CHAR_Bandit_Thug"; break;
            }
        });
        Assert.Contains(TimedDifferences(c), d => d.StartsWith(problem, StringComparison.Ordinal));
    }

    [Fact]
    public void Timed_empty_no_timed_template_in_an_empty_library() =>
        Assert.Equal(Timed.Length, TimedDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())).Count);

    [Fact]
    public void Onslaught_passes_the_seven_templates()
    {
        Assert.Empty(OnslaughtDifferences(Real()));
        var a = Real().Find("undead-onslaught")!.Definition.Action!;
        Assert.True(a.Pools!["lords"].Count >= 2);                                                                     // two distinct bosses can roll
        Assert.Equal((1, 1), (Real().Find("spider-onslaught")!.Definition.Action!.WaveList![^1].Bosses![0].Min,
            Real().Find("spider-onslaught")!.Definition.Action!.WaveList![^1].Bosses![0].Max));                         // one boss in the spider pool
    }

    [Theory]
    [InlineData("missing", "undead-onslaught missing")]
    [InlineData("enabled", "undead-onslaught enabled")]
    [InlineData("wave", "undead-onslaught wave 1 passes 20 units")]
    [InlineData("midroll", "undead-onslaught wave 2 has no boss roll")]
    [InlineData("finalroll", "undead-onslaught wave 3 rolls 1-3 bosses, not 1-2")]
    [InlineData("member", "undead-onslaught pool troops: CHAR_Bandit_Thug is not in the undead group")]
    [InlineData("cleared", "undead-onslaught wave 3 waits for a clear")]
    public void Onslaught_fails_when_a_template_breaks_the_rule(string mutation, string problem)
    {
        var c = Edited("undead-onslaught", (e, all) =>
        {
            switch (mutation)
            {
                case "missing": all.Remove(e); break;
                case "enabled": e["enabled"] = true; break;
                case "wave": WavesOf(e)[0]!["units"]![0]!["count"] = 21; break;
                case "midroll": ((JsonObject)WavesOf(e)[1]!).Remove("bosses"); break;
                case "finalroll": WavesOf(e)[2]!["bosses"]![0]!["max"] = 3; break;
                case "member": ((JsonArray)e["action"]!["pools"]!["troops"]!).Add("CHAR_Bandit_Thug"); break;
                case "cleared": WavesOf(e)[2]!["whenCleared"] = true; break;
            }
        });
        Assert.Contains(OnslaughtDifferences(c), d => d.StartsWith(problem, StringComparison.Ordinal));
    }

    [Fact]
    public void Onslaught_empty_no_onslaught_in_an_empty_library() =>
        Assert.Equal(Onslaughts.Length, OnslaughtDifferences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())).Count);
}
