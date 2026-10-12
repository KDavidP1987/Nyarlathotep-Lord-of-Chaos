using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D3, D7 and D25: the arena rule that returns a running instance's strays, when the winner line shows,
/// and the growth generator whose list runs through the wave-sets machinery as a hand-written waveList does.</summary>
public class SwarmTests
{
    static readonly DateTime T0 = new(2026, 10, 11, 20, 0, 0, DateTimeKind.Utc);

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    const string Point = "{ \"type\": \"Point\", \"x\": 100, \"z\": 200 }";       // saved without a height
    const string Admin = "{ \"type\": \"Admin\" }";

    static string UnitsForm(string extra, string location = Point, string units = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ]", int waves = 2) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": " + waves + ", \"intervalSeconds\": 60, \"radius\": 8, " +
        "\"location\": " + location + ", " + extra + " }";

    static string WaveListForm(string extra) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ] } ], " +
        "\"radius\": 8, \"location\": " + Point + ", " + extra + " }";

    static EventDefinition Def(string id, string action)
    {
        var d = Json.One(Json.Event(id, action: action));
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    static ActiveEvent Running(EventDefinition d, (float X, float Y, float Z)? origin = null) =>
        new(new RunningInstance(d, T0, T0.AddMinutes(10)), "manual", origin);

    static ActiveEvent ArenaEvent(string id = "sw", int arena = 25, string? form = null) =>
        Running(Def(id, form ?? UnitsForm("\"arena\": { \"radius\": " + arena + " }")));

    static ArenaUnit At(long key, float x, float z, string? id = "sw", (float X, float Z)? target = null, DateTime? spawned = null) =>
        new(key, id, spawned ?? T0.AddSeconds(5), x, 3f, z, target);

    static float Walkable(float x, float z) => 42f;

    static Func<double> Rng(int seed = 7)
    {
        var r = new Random(seed);
        return r.NextDouble;
    }

    // ---- D3 Containment ----

    [Fact]
    public void Containment_fails_when_a_stray_stays_or_an_inner_unit_moves()
    {
        var a = ArenaEvent();                                                     // centre (100, 200), arena 25: strays past 35 m
        var check = Arena.Check(a, [At(1, 100, 200 + 35.5f), At(2, 100, 200 + 34.5f), At(3, 100 + 35f, 200)], Walkable, Rng())!;
        Assert.Equal([1L], check.Returns.Select(r => r.Key));                       // 35 m exactly is left alone
        Assert.Equal(35.5f, check.Farthest, 3);
    }

    [Fact]
    public void Containment_fails_when_a_return_lands_outside_action_radius()
    {
        var a = ArenaEvent();
        var units = Enumerable.Range(0, 200).Select(i => At(i, 100 + 50 * MathF.Cos(i), 200 + 50 * MathF.Sin(i))).ToList();
        var check = Arena.Check(a, units, Walkable, Rng())!;
        Assert.Equal(200, check.Returns.Count);
        Assert.All(check.Returns, r => Assert.True(Arena.Distance((100, 200), r.X, r.Z) <= 8.0001f, $"{r.X} {r.Z}"));
        Assert.Contains(check.Returns, r => Arena.Distance((100, 200), r.X, r.Z) > 4);   // spread over the disc, not piled at the centre
        Assert.Equal((100f + 8f, 200f), Arena.ReturnSpot((100, 200), 8, 0, 1));
        Assert.Equal((100f, 200f), Arena.ReturnSpot((100, 200), 8, 1.2, 0));
    }

    [Fact]
    public void Containment_fails_when_the_height_is_not_walkable()
    {
        var saved = Def("sw", UnitsForm("\"arena\": { \"radius\": 25 }", location: "{ \"type\": \"Point\", \"x\": 100, \"z\": 200, \"y\": 7 }"));
        foreach (var a in new[] { ArenaEvent(), Running(saved) })
        {
            var asked = new List<(float, float)>();
            var r = Arena.Check(a, [At(1, 100, 260)], (x, z) => { asked.Add((x, z)); return 42f; }, Rng())!.Returns.Single();
            Assert.Equal(42f, r.Y);                                               // never 0 nor the Point's saved 7
            Assert.Equal([(r.X, r.Z)], asked);                                    // the height of the return spot itself
        }
    }

    [Fact]
    public void Containment_fails_when_a_target_outside_is_kept()
    {
        var a = ArenaEvent();
        var check = Arena.Check(a,
            [At(1, 100, 260, target: (100, 300)), At(2, 100, 260, target: (110, 210)), At(3, 100, 260), At(4, 100, 260, target: (100, 225))],
            Walkable, Rng())!;
        Assert.Equal([true, false, true, false], check.Returns.Select(r => r.DropTarget));   // outside, inside, none, on the edge
    }

    [Fact]
    public void Containment_fails_when_a_form_is_missed()
    {
        var arena = "\"arena\": { \"radius\": 25 }";
        var forms = new[]
        {
            Running(Def("sw", UnitsForm(arena))),
            Running(Def("sw", WaveListForm(arena))),
            Running(Def("sw", UnitsForm(arena + ", \"growth\": { \"addUnits\": 1, \"addLevels\": 2 }", waves: 3))),
        };
        Assert.NotNull(forms[2].Definition.Action!.WaveList);
        foreach (var a in forms) Assert.Equal([1L], Arena.Check(a, [At(1, 100, 300)], Walkable, Rng())!.Returns.Select(r => r.Key));
    }

    [Fact]
    public void Containment_fails_when_another_units_move()
    {
        var a = ArenaEvent();
        var check = Arena.Check(a,
            [At(1, 100, 300, id: "other"), At(2, 100, 300, spawned: T0.AddSeconds(-1)), At(3, 100, 300, id: null), At(4, 100, 300, spawned: T0)],
            Walkable, Rng())!;
        Assert.Equal([4L], check.Returns.Select(r => r.Key));                       // another event, an earlier instance, `.nyar spawn`: never
        var plain = Running(Def("sw", UnitsForm("\"loot\": false")));
        Assert.Null(Arena.Check(plain, [At(5, 100, 900)], Walkable, Rng()));        // an event without arena: no check
        var checks = Arena.CheckAll([a, plain, ArenaEvent("sw2")], [At(6, 100, 300), At(7, 100, 300, id: "sw2")], Walkable, Rng());
        Assert.Equal([("sw", 6L), ("sw2", 7L)], checks.Select(c => (c.EventId, c.Returns.Single().Key)));
    }

    [Fact]
    public void Containment_fails_when_the_admin_centre_is_not_the_origin()
    {
        var d = Def("sw", UnitsForm("\"arena\": { \"radius\": 25 }", location: Admin));
        var a = Running(d, origin: (-500f, 9f, 700f));
        Assert.Equal((-500f, 700f), Arena.Centre(a));
        var check = Arena.Check(a, [At(1, -500, 736), At(2, -500, 734), At(3, 100, 200)], Walkable, Rng())!;
        Assert.Equal([1L, 3L], check.Returns.Select(r => r.Key));                   // the Point's 0, 0 is no centre here
        Assert.All(check.Returns, r => Assert.True(Arena.Distance((-500, 700), r.X, r.Z) <= 8.0001f));
        Assert.Null(Arena.Centre(Running(d)));                                    // no origin captured: no centre, no check
        Assert.Null(Arena.Check(Running(d), [At(4, 0, 0)], Walkable, Rng()));
    }

    [Fact]
    public void Containment_fails_when_the_log_line_names_a_player()
    {
        var d = Def("sw", UnitsForm("\"arena\": { \"radius\": 25 }", location: "{ \"type\": \"Point\", \"x\": 1000, \"z\": 2000 }"));
        var check = Arena.Check(Running(d), [At(1, 1000, 2048, target: (1000, 2090)), At(2, 1000, 2040)], Walkable, Rng())!;
        Assert.Equal("arena sw: returned 2, farthest 48 m", check.LogLine);
        foreach (var mark in new[] { "1000", "2048", "2090", "2000", "Chaos", "765" }) Assert.DoesNotContain(mark, check.LogLine!, StringComparison.Ordinal);
        Assert.Equal("arena sw: returned 1, farthest 36 m", Arena.LogLine("sw", 1, 35.6f));
    }

    [Fact]
    public void Containment_fails_when_a_point_without_height_returns_to_0()
    {
        // The service's walkable height (step 2): a Point saved without a height has no anchor, so the instance's own
        // grounded units inside the arena give it, never the 0 the Point spawns at.
        var a = ArenaEvent();
        ArenaUnit Y(long key, float z, float y, string id = "sw") => At(key, 100, z, id) with { Y = y };
        Assert.Equal(13f, Arena.ReturnHeight(a, [Y(1, 205, 12), Y(2, 210, 13), Y(3, 215, 14), Y(4, 260, 50), Y(5, 260, 51), Y(6, 210, 99, "other")]));
        Assert.Equal(50f, Arena.ReturnHeight(a, [Y(4, 260, 50), Y(5, 270, 51)]));   // every unit strays: theirs, not 0
        Assert.Equal(0f, Arena.ReturnHeight(a, []));                               // no unit: nothing strays either
        var saved = Running(Def("sw", UnitsForm("\"arena\": { \"radius\": 25 }", location: "{ \"type\": \"Point\", \"x\": 100, \"z\": 200, \"y\": 7 }")));
        Assert.Equal(7f, Arena.ReturnHeight(saved, [Y(1, 205, 12)]));             // the anchor the wave spawned and regroups at
        var admin = Running(Def("sw", UnitsForm("\"arena\": { \"radius\": 25 }", location: Admin)), origin: (-500f, 9f, 700f));
        Assert.Equal(9f, Arena.ReturnHeight(admin, [Y(1, 205, 12)]));             // the starting admin's height
    }

    [Fact]
    public void Containment_fails_when_a_target_inside_is_dropped()
    {
        Assert.True(Arena.DropsEntry((100, 200), 25, (100, 226)));                 // a player outside the arena
        Assert.False(Arena.DropsEntry((100, 200), 25, (100, 225)));                // on the edge: kept
        Assert.False(Arena.DropsEntry((100, 200), 25, (110, 210)));                // inside: the unit keeps fighting
        Assert.True(Arena.DropsEntry((100, 200), 25, null));                       // unreadable: dropped
    }

    [Fact]
    public void Containment_passes_check_every_behaviour_tick() => Assert.Equal(5, Arena.CheckSeconds);

    [Fact]
    public void Containment_empty_no_stray()
    {
        var check = Arena.Check(ArenaEvent(), [At(1, 100, 210)], Walkable, Rng())!;
        Assert.Empty(check.Returns);
        Assert.Null(check.LogLine);
        Assert.Empty(Arena.Check(ArenaEvent(), [], Walkable, Rng())!.Returns);
    }

    // ---- D7 EndPaths ----

    static string Scored(string extra) =>
        Json.Event("sw", action: UnitsForm("\"scoreboard\": true" + extra + ", \"growth\": { \"addUnits\": 1 }", waves: 3));

    static (ActiveEvent Ended, Scoreboard Board) Board(bool winner, bool kills = true)
    {
        var a = Running(Json.One(Scored(winner ? ", \"winner\": \"KillDeath\"" : "")));
        var board = new Scoreboard();
        var chaos = new Scorer("1", "Chaos", false);
        if (kills) { board.Kill("sw", chaos, false); board.Kill("sw", chaos, false); }
        board.Death("sw", chaos, false);
        return (a, board);
    }

    [Theory]
    [InlineData(EndPath.Natural)]
    [InlineData(EndPath.Victory)]
    [InlineData(EndPath.Stop)]
    public void EndPaths_passes_the_winner_on_the_three_showing_paths(EndPath path)
    {
        var (a, board) = Board(winner: true);
        var end = ScoreboardRule.End(path, a, board);
        Assert.Equal(["Bandit raid scoreboard: 1. Chaos 2 kills, 1 deaths", "1 players, 2 kills, 1 deaths",
                      "Bandit raid winner: Chaos, K/D 2.00 (2 kills, 1 deaths)"], end.Chat);
        Assert.Equal("event sw scoreboard: 1 players, 2 kills, 1 deaths, winner K/D 2.00", end.Log);
        var (none, deathsOnly) = Board(winner: true, kills: false);
        var empty = ScoreboardRule.End(path, none, deathsOnly);
        Assert.Equal("Bandit raid winner: none", empty.Chat[^1]);
        Assert.Equal("event sw scoreboard: 1 players, 0 kills, 1 deaths, no winner", empty.Log);
    }

    [Theory]
    [InlineData(EndPath.Purge)]
    [InlineData(EndPath.Restart)]
    [InlineData(EndPath.PillarOff)]
    [InlineData(EndPath.Fault)]
    public void EndPaths_fails_when_a_silent_path_queues_a_winner(EndPath path)
    {
        var (a, board) = Board(winner: true);
        Assert.Same(ScoreboardEnd.None, ScoreboardRule.End(path, a, board));
        Assert.Equal(0, board.Events);
    }

    [Fact]
    public void EndPaths_fails_when_the_log_line_carries_a_name()
    {
        foreach (var path in new[] { EndPath.Natural, EndPath.Victory, EndPath.Stop })
        {
            var (a, board) = Board(winner: true);
            var log = ScoreboardRule.End(path, a, board).Log!;
            Assert.DoesNotContain("Chaos", log, StringComparison.Ordinal);
        }
        var named = new ScoreSummary([new ScoreRow("Chaos", 3, 0, 1)], 1, 3, 0, new ScoreRow("Chaos", 3, 0, 1));
        Assert.Equal("event sw scoreboard: 1 players, 3 kills, 0 deaths, winner K/D 3.00", ScoreboardRule.LogLine("sw", named, WinnerRule.KillDeath));
    }

    [Fact]
    public void EndPaths_fails_when_a_keyless_log_line_changes()
    {
        var (a, board) = Board(winner: false);
        var end = ScoreboardRule.End(EndPath.Natural, a, board);
        Assert.Equal("event sw scoreboard: 1 players, 2 kills, 1 deaths", end.Log);
        Assert.Equal(["Bandit raid scoreboard: 1. Chaos 2 kills, 1 deaths", "1 players, 2 kills, 1 deaths"], end.Chat);
    }

    [Fact]
    public void EndPaths_empty_no_player_scored()
    {
        var a = Running(Json.One(Scored(", \"winner\": \"KillDeath\"")));
        var end = ScoreboardRule.End(EndPath.Natural, a, new Scoreboard());
        Assert.Equal(["Bandit raid scoreboard: no player scored"], end.Chat);
        Assert.Equal("event sw scoreboard: 0 players, 0 kills, 0 deaths, no winner", end.Log);
    }

    // ---- D25 Growth ----

    const string Thug = "CHAR_Bandit_Thug";
    const string Deadeye = "CHAR_Bandit_Deadeye";

    static IReadOnlyList<UnitEntry> Base => [new(Thug, 2), new(Deadeye, 1, 0.5)];

    /// <summary>A growth event and its hand-written waveList twin of the plan's equivalence fixture.</summary>
    static string GrowthEvent(string modifiers, string growth, int waves = 4) =>
        Json.Event("sw", action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"" + Thug + "\", \"count\": 2 }, " +
            "{ \"prefab\": \"" + Deadeye + "\", \"count\": 1, \"chance\": 0.5 } ], \"waves\": " + waves + ", \"intervalSeconds\": 90, " +
            (modifiers == "" ? "" : "\"modifiers\": { " + modifiers + " }, ") +
            "\"radius\": 8, \"location\": " + Point + ", \"scoreboard\": true, \"winner\": \"KillDeath\", \"growth\": { " + growth + " } }");

    static string HandWritten(Func<int, string> modifiersOf, int addUnits, bool whenCleared, int waves = 4)
    {
        string Entry(string prefab, int count, string chance, int n)
        {
            var m = modifiersOf(n);
            return "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + chance + (m == "" ? "" : ", \"modifiers\": { " + m + " }") + " }";
        }
        var list = Enumerable.Range(1, waves).Select(n =>
            "{ \"units\": [ " + Entry(Thug, 2 + (n - 1) * addUnits, "", n) + ", " + Entry(Deadeye, 1 + (n - 1) * addUnits, ", \"chance\": 0.5", n) + " ]" +
            (n == 1 ? "" : ", \"afterSeconds\": 90" + (whenCleared ? ", \"whenCleared\": true" : "")) + " }");
        return Json.Event("sw", action: "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ " + string.Join(", ", list) + " ], " +
            "\"radius\": 8, \"location\": " + Point + ", \"scoreboard\": true, \"winner\": \"KillDeath\" }");
    }

    static void SameList(IReadOnlyList<WaveSpec> expected, IReadOnlyList<WaveSpec> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Units, actual[i].Units);
            Assert.Equal((expected[i].AfterSeconds, expected[i].WhenCleared), (actual[i].AfterSeconds, actual[i].WhenCleared));
        }
    }

    public static IEnumerable<object[]> Fixtures() =>
    [
        ["\"level\": 30, \"maxHealth\": 1.5", "\"addUnits\": 2, \"addLevels\": 3, \"whenCleared\": true", 2, true,
            (Func<int, string>)(n => $"\"level\": {30 + 3 * (n - 1)}, \"maxHealth\": 1.5")],
        ["\"levelDelta\": -2, \"power\": 1.25", "\"addUnits\": 1, \"addLevels\": 2", 1, false,
            (Func<int, string>)(n => $"\"levelDelta\": {-2 + 2 * (n - 1)}, \"power\": 1.25")],
        ["", "\"addUnits\": 3, \"whenCleared\": true", 3, true, (Func<int, string>)(_ => "")],
        ["", "\"addLevels\": 1", 0, false, (Func<int, string>)(n => n == 1 ? "" : $"\"levelDelta\": {n - 1}")],
    ];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Growth_fails_when_a_generated_list_differs_from_its_hand_written_twin(string modifiers, string growth, int addUnits, bool whenCleared,
        Func<int, string> modifiersOf)
    {
        var grown = Json.One(GrowthEvent(modifiers, growth));
        var twin = Json.One(HandWritten(modifiersOf, addUnits, whenCleared));
        Assert.True(grown.Startable, grown.DisabledReason);
        Assert.True(twin.Startable, twin.DisabledReason);
        SameList(twin.Action!.WaveList!, grown.Action!.WaveList!);
        Assert.Equal(twin.Action.Waves, grown.Action.Waves);
        Assert.Equal(Script(HandWritten(modifiersOf, addUnits, whenCleared)), Script(GrowthEvent(modifiers, growth)));   // every decision, clear, victory, scoreboard
    }

    /// <summary>One scripted run of the event through the engine: each WaveSchedule decision, warning, cleared
    /// line, the victory and the scoreboard's chat and log, as text to compare between twins.</summary>
    static List<string> Script(string eventJson)
    {
        var catalog = new EventCatalog();
        var parsed = EventValidator.Parse(Json.File(eventJson), FakeUnits.Default());
        var d = Assert.Single(parsed.Set.All);
        Assert.Null(catalog.Reload(parsed, FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        Assert.Null(engine.Start("sw", "manual", T0, Open()));
        var a = engine.Find("sw")!;
        var cleared = new HashSet<int>();
        bool Of(string id, int w) => cleared.Contains(w);
        var log = new List<string>();
        var board = new Scoreboard();
        var at = T0;
        void Step(int seconds, params int[] clears)
        {
            at = at.AddSeconds(seconds);
            cleared.UnionWith(clears);
            foreach (var c in engine.NewlyCleared(Of)) log.Add($"{at:HH:mm:ss} cleared {c}");
            var due = engine.NextWave("sw", at, Of);
            log.Add($"{at:HH:mm:ss} next {due?.Wave.ToString() ?? "-"} upcoming {UpcomingWave.Of(a)?.ToString() ?? "-"} known {WaveSchedule.KnownAt(a)?.ToString("HH:mm:ss") ?? "-"}");
            if (due is not null)
            {
                var units = d.Action!.WaveList![due.Wave - 1].Units;
                var decision = WaveGate.DecideEntries(new WaveFacts(due.Wave, "sw", false, AllowTerritory: true), () => units, 20, 0, 150);
                log.Add($"wave {due.Wave}: {decision.Outcome} {string.Join(",", decision.Units.Select(u => $"{u.Prefab}x{u.Count}"))}");
                engine.WaveDecided("sw", decision.Outcome, at, decision.Units.Sum(u => u.Count));
                board.Kill("sw", new Scorer("1", "Chaos", false), false);
            }
            foreach (var beaten in engine.Complete(at, 60, Of))
            {
                log.Add(ScoreboardRule.VictoryLine(beaten));
                var end = ScoreboardRule.End(EndPath.Victory, beaten, board);
                log.AddRange(end.Chat);
                log.Add(end.Log!);
            }
        }
        Step(0);
        Step(30);
        Step(10, 1);          // cleared at 40 s: a whenCleared wave 2 starts, an after-only one waits for 90 s
        Step(50);
        Step(40, 2);
        Step(90);
        Step(90, 3);
        Step(90);
        Step(5, 4);
        Step(5);
        return log;
    }

    [Fact]
    public void Growth_fails_when_wave_one_differs_from_the_base_wave()
    {
        var mods = new SpawnModifiers(Level: 30, MaxHealth: 1.5);
        var list = Growth.Generate(Base, mods, 3, 90, new Growth(2, 3, true));
        Assert.Equal([new UnitEntry(Thug, 2, 1.0, mods), new UnitEntry(Deadeye, 1, 0.5, mods)], list[0].Units);
        Assert.Equal((null, false), (list[0].AfterSeconds, list[0].WhenCleared));
        var units = Json.One(Json.Event("raid")).Action! with { Modifiers = mods };   // the units form's tuning
        var grown = units with { WaveList = list };
        foreach (var i in Enumerable.Range(0, 2))
        {
            var expected = SpawnTuning.For(units, units.Units[0]);
            var actual = SpawnTuning.For(grown, list[0].Units[i]);
            Assert.Equal(expected.Level, actual.Level);
            Assert.Equal(expected.Stats, actual.Stats);
        }
        Assert.Null(Growth.Generate(Base, null, 2, 90, new Growth(1, 0, false))[0].Units[0].Modifiers);   // null stays null
    }

    [Fact]
    public void Growth_fails_when_a_step_is_off_by_one_wave()
    {
        var list = Growth.Generate(Base, new SpawnModifiers(Level: 30), 5, 120, new Growth(1, 3, true));
        Assert.Equal([2, 3, 4, 5, 6], list.Select(w => w.Units[0].Count));
        Assert.Equal([1, 2, 3, 4, 5], list.Select(w => w.Units[1].Count));
        Assert.Equal([30, 33, 36, 39, 42], list.Select(w => w.Units[0].Modifiers!.Level!.Value));
        Assert.All(list, w => Assert.Equal([1.0, 0.5], w.Units.Select(u => u.Chance)));            // the chance unchanged
        Assert.All(list.Skip(1), w => Assert.Equal((120, true), (w.AfterSeconds, w.WhenCleared)));
        Assert.All(Growth.Generate(Base, null, 3, 60, new Growth(1, 0, false)).Skip(1), w => Assert.Equal((60, false), (w.AfterSeconds, w.WhenCleared)));
    }

    [Fact]
    public void Growth_fails_when_a_level_passes_120()
    {
        var list = Growth.Generate(Base, new SpawnModifiers(Level: 110), 10, 60, new Growth(0, 5, false));
        Assert.Equal([110, 115, 120, 120, 120, 120, 120, 120, 120, 120], list.Select(w => w.Units[0].Modifiers!.Level!.Value));
        Assert.All(list, w => Assert.Null(w.Units[0].Modifiers!.LevelDelta));
    }

    [Fact]
    public void Growth_fails_when_a_grown_level_delta_resolves_outside_1_120()
    {
        var list = Growth.Generate(Base, new SpawnModifiers(LevelDelta: 3), 10, 60, new Growth(0, 5, false));
        Assert.Equal(48, list[^1].Units[0].Modifiers!.LevelDelta);                                  // 3 + 9·5, past what a waveList can hold
        var level = SpawnTuning.TuningFrom(list[^1].Units[0].Modifiers).Level!.Value;
        Assert.Equal(new LevelArg(true, 48), level);
        Assert.Equal(120, level.Resolve(100));                                                     // clamped at the spawn
        Assert.Equal(58, level.Resolve(10));
        var down = Growth.Generate(Base, new SpawnModifiers(LevelDelta: -5), 2, 60, new Growth(0, 1, false));
        Assert.Equal(1, SpawnTuning.TuningFrom(down[0].Units[0].Modifiers).Level!.Value.Resolve(3));   // never below 1
    }

    [Fact]
    public void Growth_fails_when_a_generated_wave_skips_the_clamp()
    {
        var d = Json.One(Json.Event("sw", action: UnitsForm("\"growth\": { \"addUnits\": 10 }", units: "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 20 } ]", waves: 3)));
        var last = d.Action!.WaveList![^1].Units;
        Assert.Equal(40, last.Sum(u => u.Count));
        var decision = WaveGate.DecideEntries(new WaveFacts(3, "sw", false, AllowTerritory: true), () => WaveRoll.ExpandEntries(last, new ScriptedRandom()), 20, 0, 150);
        Assert.Equal(20, decision.Units.Sum(u => u.Count));                                        // MaxUnitsPerWave holds, like any wave
    }

    [Fact]
    public void Growth_passes_the_last_wave_count() =>
        Assert.Equal(3 * 4 + 3 * 1 * 4, Growth.LastWaveUnits([new(Thug, 3), new(Deadeye, 1), new(Thug, 4), new(Deadeye, 4)], 4, 1));

    [Fact]
    public void Growth_empty_no_growth()
    {
        var d = Json.One(Json.Event("raid"));
        Assert.Null(d.Action!.WaveList);
        Assert.Null(d.Action.Growth);
        Assert.Equal(3, d.Action.Waves);
    }
}
