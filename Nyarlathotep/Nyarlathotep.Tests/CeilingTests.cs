using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D1: the raised ceilings (MaxUnitsPerWave 500, the per-tick budgets 40; MaxTrackedUnits kept at 500, A3) and
/// every check that used the old 500 and 50 reading them; the defaults unchanged.</summary>
public class CeilingTests
{
    static readonly DateTime T0 = new(2026, 10, 17, 20, 0, 0, DateTimeKind.Utc);

    static string Units(params int[] counts) =>
        "[ " + string.Join(", ", counts.Select((c, i) => "{ \"prefab\": \"" + (i % 2 == 0 ? "CHAR_Bandit_Thug" : "CHAR_Bandit_Deadeye") + "\", \"count\": " + c + " }")) + " ]";

    static string WaveAction(string units, string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, " +
        "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }" + (extra is null ? "" : ", " + extra) + " }";

    const string Pools = "\"pools\": { \"elite\": [ \"CHAR_Bandit_Thug\", \"CHAR_Bandit_Deadeye\" ] }";

    static EventDefinition Load(string action) => Json.One(Json.Event(action: action));

    static ApocalypseRecord Record(int roamers, int living) => new()
    {
        EventId = "apo",
        StartedUtc = T0,
        EndsUtc = T0.AddHours(1),
        Goal = new RecordPoint { X = 500, Y = 12, Z = 0 },
        Hotspots = [new RecordSpot { X = 0, Z = 0 }],
        HordesRisen = 1,
        Roamers = [roamers],
        Hordes = [new RecordHorde { Number = 1, FrontX = 0, FrontZ = 0, Living = living, LivingBosses = [] }],
    };

    [Fact]
    public void Ceilings_fails_when_a_ceiling_stays_at_50_or_20()
    {
        Assert.Equal(500, Limits.MaxTrackedUnits.Max);
        Assert.Equal(500, Limits.MaxUnitsPerWave.Max);
        Assert.Equal(40, Limits.MaxSpawnsPerTick.Max);
        Assert.Equal(40, Limits.MaxDespawnsPerTick.Max);
        Assert.Equal((500, (string?)null), Limits.MaxTrackedUnits.Clamp(500));
        Assert.Equal(500, Limits.MaxTrackedUnits.Clamp(501).Value);
        Assert.Equal(500, Limits.MaxUnitsPerWave.Clamp(501).Value);
    }

    [Fact]
    public void Ceilings_fails_when_a_default_changes()
    {
        Assert.Equal((500, 20, 10, 5),                                                        // horde-pools D2
            (Limits.MaxTrackedUnits.Default, Limits.MaxUnitsPerWave.Default, Limits.MaxSpawnsPerTick.Default, Limits.MaxDespawnsPerTick.Default));
    }

    [Fact]
    public void Ceilings_fails_when_a_check_refuses_a_value_the_ceiling_allows()
    {
        // a unit entry's count, in the units form and in a waveList, up to 500
        var units = Load(WaveAction(Units(500)));
        Assert.True(units.Startable, units.DisabledReason);
        var list = Load("\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": " + Units(500) + " } ], \"radius\": 10, " +
                        "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }");
        Assert.True(list.Startable, list.DisabledReason);
        // a pool wave's total, up to 500
        var pool = Load(WaveAction("[ { \"pool\": \"elite\", \"count\": 300 }, { \"pool\": \"elite\", \"count\": 200 } ]", Pools));
        Assert.True(pool.Startable, pool.DisabledReason);
        // growth to exactly 500 on the last wave (two entries, 5 a wave each over 3 waves: 480 + 20)
        var grown = Load(WaveAction(Units(200, 280), "\"growth\": { \"addUnits\": 5 }"));
        Assert.True(grown.Startable, grown.DisabledReason);
        // the chat fields, the units form and a wave's units
        Assert.True(CommandArgs.SettableValue("action.units", "CHAR_Bandit_Thug:500").Ok);
        Assert.True(CommandArgs.SettableValue("action.waveList.1.units", "CHAR_Bandit_Thug:500").Ok);
        Assert.True(CommandArgs.SettableValue("action.units", "pool:elite:500").Ok);
        // `.nyar spawn` up to the cfg's MaxUnitsPerWave
        Assert.True(CommandArgs.Count("500", 500).Ok);
        // the apocalypse record's living check up to MaxTrackedUnits' ceiling
        Assert.Null(Record(200, 300).Problem());
        Assert.Equal("the living counts exceed the tracked-unit cap", Record(200, 301).Problem());
    }

    [Fact]
    public void Ceilings_passes_one_past_each_ceiling_is_refused()
    {
        Assert.Equal("action.units.count must be 1-500", Load(WaveAction(Units(501))).DisabledReason);
        Assert.Equal(EventValidator.WaveSizeRule("action.units"),
            Load(WaveAction("[ { \"pool\": \"elite\", \"count\": 300 }, { \"pool\": \"elite\", \"count\": 201 } ]", Pools)).DisabledReason);
        Assert.Equal(EventValidator.GrowthLastWave, Load(WaveAction(Units(200, 281), "\"growth\": { \"addUnits\": 5 }")).DisabledReason);
        Assert.Equal("action.growth grows the last wave past 500 units", EventValidator.GrowthLastWave);
        Assert.False(CommandArgs.SettableValue("action.units", "CHAR_Bandit_Thug:501").Ok);
        Assert.False(CommandArgs.Count("501", 500).Ok);
    }

    [Fact]
    public void Ceilings_empty_a_cfg_value_below_the_minimum_takes_the_minimum()
    {
        Assert.Equal(1, Limits.MaxTrackedUnits.Clamp(0).Value);
        Assert.Equal(1, Limits.MaxSpawnsPerTick.Clamp(-5).Value);
    }

    // D4: the ceiling follows the load ladder's line in docs/features/UNDEAD_HORDES.md › Load test: the ladder's ceiling when
    // it is 500 or more, else 0.13.1's 500 unless the plan's Log has the owner's "· owner · ceiling <n> below 500". Below 1000
    // the Log also carries the owner's "· owner · told the ladder ceiling <n>".
    static readonly Regex LadderLine = new(@"load ladder: ([^\n]*)· ceiling (\d+)");
    static readonly Regex LadderStep = new(@"(\d+) (pass|fail)");

    internal static string? LadderProblem(string featureDoc, string plan, int trackedMax, int waveMax)
    {
        var lines = LadderLine.Matches(featureDoc);
        if (lines.Count != 1) return $"the feature doc has {lines.Count} load ladder lines, not 1";
        var n = int.Parse(lines[0].Groups[2].Value);
        // the ceiling is the last step that passed before the first failure (Codex step 1 F3)
        var passed = LadderStep.Matches(lines[0].Groups[1].Value).TakeWhile(s => s.Groups[2].Value == "pass").Select(s => int.Parse(s.Groups[1].Value)).LastOrDefault();
        if (passed != n) return $"the ladder line's ceiling {n} is not its last passing step {passed}";
        var at = plan.IndexOf("## Log", StringComparison.Ordinal);
        var log = at < 0 ? "" : plan[at..];
        if (n < 1000 && !log.Contains($"· owner · told the ladder ceiling {n}")) return $"no owner's line that they were told the ceiling {n}";
        var ceiling = n >= 500 || log.Contains($"· owner · ceiling {n} below 500") ? n : 500;
        if (trackedMax != ceiling) return $"MaxTrackedUnits' ceiling {trackedMax} is not {ceiling} (the ladder's {n})";
        if (waveMax != Math.Min(500, ceiling)) return $"MaxUnitsPerWave's ceiling {waveMax} is not {Math.Min(500, ceiling)}";
        return null;
    }

    static string Resource(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", name));

    const string Doc1000 = "## Load test\n\nload ladder: 100 pass, 250 pass, 500 pass, 750 pass, 1000 pass · ceiling 1000\n";
    const string Plan = "# plan\n\n## Log\n\n- 2026-10-17 · note · start\n";

    static string Ceiling(string steps) => Doc1000.Replace("100 pass, 250 pass, 500 pass, 750 pass, 1000 pass · ceiling 1000", steps);

    [Fact]
    public void Ladder_passes_the_code_matches_the_recorded_ladder()
    {
        Assert.Null(LadderProblem(Resource("UNDEAD_HORDES.md"), Resource("undead-hordes.md"), Limits.MaxTrackedUnits.Max, Limits.MaxUnitsPerWave.Max));
        Assert.Null(LadderProblem(Doc1000, Plan, 1000, 500));
    }

    [Fact]
    public void Ladder_fails_when_the_ceiling_differs_from_the_feature_doc()
    {
        var doc750 = Ceiling("100 pass, 250 pass, 500 pass, 750 pass, 1000 fail · ceiling 750");
        var told750 = Plan + "- 2026-10-17 · note · owner · told the ladder ceiling 750\n";
        Assert.Equal("MaxTrackedUnits' ceiling 1000 is not 750 (the ladder's 750)", LadderProblem(doc750, told750, 1000, 500));
        Assert.Equal("MaxUnitsPerWave's ceiling 400 is not 500", LadderProblem(Doc1000, Plan, 1000, 400));
        Assert.Equal("the feature doc has 2 load ladder lines, not 1", LadderProblem(Doc1000 + Doc1000, Plan, 1000, 500));
        // a ceiling the steps do not give
        Assert.Equal("the ladder line's ceiling 500 is not its last passing step 250",
            LadderProblem(Ceiling("100 pass, 250 pass, 500 fail (fight frame window 1 avg 51.7 ms) · ceiling 500"), Plan, 500, 500));
        Assert.Equal("the ladder line's ceiling 100 is not its last passing step 0", LadderProblem(Ceiling("100 fail · ceiling 100"), Plan, 500, 500));
        // below 1000 the owner is told the number
        Assert.Equal("no owner's line that they were told the ceiling 750", LadderProblem(doc750, Plan, 750, 500));
        Assert.Null(LadderProblem(doc750, told750, 750, 500));
    }

    [Fact]
    public void Ladder_empty_a_feature_doc_without_a_ladder_line()
    {
        Assert.Equal("the feature doc has 0 load ladder lines, not 1", LadderProblem("## Load test\n", Plan, 1000, 500));
    }

    [Fact]
    public void Ladder_fails_when_below_500_without_the_owners_line()
    {
        var doc250 = Ceiling("100 pass, 250 pass, 500 fail · ceiling 250");
        var told = Plan + "- 2026-10-17 · note · owner · told the ladder ceiling 250\n";
        // a ladder under 500 keeps 0.13.1's 500 unless the owner decides the lower one
        Assert.Null(LadderProblem(doc250, told, 500, 500));
        Assert.Equal("MaxTrackedUnits' ceiling 250 is not 500 (the ladder's 250)", LadderProblem(doc250, told, 250, 250));
        Assert.Null(LadderProblem(doc250, told + "- 2026-10-17 · note · owner · ceiling 250 below 500\n", 250, 250));
        // an owner's line outside the Log does not count
        Assert.Equal("no owner's line that they were told the ceiling 250",
            LadderProblem(doc250, "· owner · told the ladder ceiling 250\n" + Plan, 250, 250));
    }
}
