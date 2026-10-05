using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D3, D4, D6, D7 and D11: per-entry tuning, when a waveList wave starts, every schedule reader,
/// all waves defeated, and which end paths show the scoreboard.</summary>
public class WaveSetTests
{
    static readonly DateTime T0 = new(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc);

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    const string Thug = "{ \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 }";

    static string Wave(string? keys = null, string units = "[ " + Thug + " ]") =>
        "{ \"units\": " + units + (keys is null ? "" : ", " + keys) + " }";

    /// <summary>A Manual waveList event of 600 s.</summary>
    static string WaveListEvent(string id, string extra, params string[] waves) =>
        Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ " + string.Join(", ", waves) + " ], \"radius\": 10, " +
            "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }" + extra + " }");

    /// <summary>ws-three of the plan (D20): wave 2 after 120 s or when cleared, wave 3 when cleared.</summary>
    static string WsThree(string id = "ws-three", string extra = "") =>
        WaveListEvent(id, extra, Wave(), Wave("\"afterSeconds\": 120, \"whenCleared\": true"), Wave("\"whenCleared\": true"));

    static (EventEngine Engine, RecordingPush Push) Started(params string[] events)
    {
        var catalog = new EventCatalog();
        var r = EventValidator.Parse(Json.File(events), FakeUnits.Default());
        Assert.Null(r.FileError);
        Assert.All(r.Set.All, d => Assert.True(d.Startable, d.DisabledReason));
        Assert.Null(catalog.Reload(r, FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        var push = new RecordingPush();
        engine.Push = push;
        foreach (var d in r.Set.All) Assert.Null(engine.Start(d.Id, "manual", T0, Open()));
        return (engine, push);
    }

    sealed class RecordingPush : IPushSink
    {
        public readonly List<string> Ended = [];
        public void EventStarted(RunningInstance instance) { }
        public void EventEnded(string id, string region = "-") => Ended.Add(id);
        public void Wave(string id, int wave) { }
        public void Purged(int cooldownSeconds) { }
        public void ConfigChanged() { }
    }

    /// <summary>Which waves the test says are cleared in the ledger.</summary>
    sealed class Clears
    {
        public readonly HashSet<int> Waves = [];
        public bool Of(string id, int wave) => Waves.Contains(wave);
    }

    // ---- D3 EntryTuning ----

    static readonly SpawnModifiers Level30 = new(Level: 30);
    static readonly SpawnModifiers Delta3Hp = new(LevelDelta: 3, MaxHealth: 1.5);

    static void SameTuning(UnitTuning expected, UnitTuning actual)
    {
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Stats, actual.Stats);
    }

    [Fact]
    public void EntryTuning_fails_when_two_entries_of_one_prefab_merge()
    {
        UnitEntry[] wave = [new("CHAR_Bandit_Thug", 2, 1.0, Level30), new("CHAR_Bandit_Thug", 2, 1.0, Delta3Hp)];
        var picks = WaveRoll.ExpandEntries(wave, new ScriptedRandom());
        Assert.Equal([Level30, Level30, Delta3Hp, Delta3Hp], picks.Select(p => p.Modifiers));
        var grouped = WaveRoll.GroupPicks(picks);
        Assert.Equal(2, grouped.Count);                                         // never merged across modifiers
        Assert.Equal((2, Level30), (grouped[0].Count, grouped[0].Modifiers));
        Assert.Equal((2, Delta3Hp), (grouped[1].Count, grouped[1].Modifiers));

        var action = WaveListDefinition().Action!;
        SameTuning(SpawnTuning.TuningFrom(Level30), SpawnTuning.For(action, grouped[0]));
        SameTuning(SpawnTuning.TuningFrom(Delta3Hp), SpawnTuning.For(action, grouped[1]));
        Assert.NotEqual(SpawnTuning.For(action, grouped[0]).Level, SpawnTuning.For(action, grouped[1]).Level);
    }

    [Fact]
    public void EntryTuning_empty_entry_without_modifiers() =>
        Assert.True(SpawnTuning.For(WaveListDefinition().Action!, new UnitEntry("CHAR_Bandit_Thug", 1)).IsNone);

    [Fact]
    public void EntryTuning_passes_the_chance_roll_with_its_entry()
    {
        UnitEntry[] wave = [new("CHAR_Bandit_Thug", 2, 0.5, Level30), new("CHAR_Bandit_Deadeye", 1, 1.0, Delta3Hp)];
        var rng = new ScriptedRandom(0.9, 0.1);                                 // first copy lost, second kept
        var picks = WaveRoll.ExpandEntries(wave, rng);
        Assert.Equal(2, rng.Calls);                                             // the same draws as Expand
        Assert.Equal([("CHAR_Bandit_Thug", Level30), ("CHAR_Bandit_Deadeye", Delta3Hp)], picks.Select(p => (p.Prefab, p.Modifiers!)));
        Assert.Equal(WaveRoll.Expand(wave, new ScriptedRandom(0.9, 0.1)), picks.Select(p => p.Prefab));
    }

    [Fact]
    public void EntryTuning_fails_when_a_clamp_drops_an_entry_tuning()
    {
        UnitEntry[] picks = [.. Enumerable.Repeat(new UnitEntry("CHAR_Bandit_Thug", 1, 1.0, Level30), 3),
            .. Enumerable.Repeat(new UnitEntry("CHAR_Bandit_Thug", 1, 1.0, Delta3Hp), 3)];
        var d = WaveGate.DecideEntries(new WaveFacts(1, "ws", false, AllowTerritory: true), () => picks, maxPerWave: 4, occupied: 0, maxTracked: 150);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal([(3, Level30), (1, Delta3Hp)], d.Units.Select(u => (u.Count, u.Modifiers!)));
    }

    [Fact]
    public void EntryTuning_fails_when_the_deal_drops_an_entry_tuning()
    {
        var calls = 0;
        IReadOnlyList<UnitEntry> Roll() => ++calls == 1
            ? [new("CHAR_Bandit_Thug", 1, 1.0, Level30), new("CHAR_Bandit_Thug", 1, 1.0, Delta3Hp)]
            : [new("CHAR_Bandit_Deadeye", 1, 1.0, Delta3Hp), new("CHAR_Bandit_Deadeye", 1, 1.0, Level30)];
        GroupCentre[] centres = [new(0, 0, 0, false), new(100, 0, 0, false)];
        var d = WaveGate.DecideGroupEntries(new WaveFacts(1, "ws", false, Location: LocationType.AroundPlayer, Pick: PickOutcome.Picked, AllowTerritory: true),
            centres, Roll, maxPerWave: 3, occupied: 0, maxTracked: 150);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal([("CHAR_Bandit_Thug", Level30), ("CHAR_Bandit_Thug", Delta3Hp)], d.Groups[0].Units.Select(u => (u.Prefab, u.Modifiers!)));
        Assert.Equal([("CHAR_Bandit_Deadeye", Delta3Hp)], d.Groups[1].Units.Select(u => (u.Prefab, u.Modifiers!)));
    }

    [Fact]
    public void EntryTuning_passes_units_form_one_tuning_per_action()
    {
        var shared = new SpawnModifiers(Power: 1.5);
        var action = Json.One(Json.Event()).Action! with { Modifiers = shared };
        foreach (var u in action.Units) SameTuning(SpawnTuning.TuningFrom(shared), SpawnTuning.For(action, u));
        // 0.8.0's path: prefabs grouped without modifiers, Split keeping none.
        var d = WaveGate.Decide(new WaveFacts(1, "raid", false, AllowTerritory: true), () => ["CHAR_Bandit_Thug", "CHAR_Bandit_Thug"], 20, 0, 150);
        Assert.Equal([new UnitEntry("CHAR_Bandit_Thug", 2)], d.Units);
    }

    static EventDefinition WaveListDefinition() => Json.One(WsThree());

    // ---- D4 Schedule ----

    [Fact]
    public void Schedule_passes_the_earlier_of_after_and_cleared()
    {
        var (engine, _) = Started(WsThree());
        var clears = new Clears();
        Assert.Equal(1, engine.NextWave("ws-three", T0, clears.Of)!.Wave);
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0, 4);
        Assert.Null(engine.NextWave("ws-three", T0.AddSeconds(30), clears.Of));
        clears.Waves.Add(1);
        Assert.Equal(2, engine.NextWave("ws-three", T0.AddSeconds(30), clears.Of)!.Wave);      // cleared before 120 s

        var (late, _) = Started(WsThree());
        late.WaveDecided("ws-three", WaveOutcome.Spawn, T0, 4);
        Assert.Null(late.NextWave("ws-three", T0.AddSeconds(119), new Clears().Of));
        Assert.Equal(2, late.NextWave("ws-three", T0.AddSeconds(120), new Clears().Of)!.Wave);  // left alive: 120 s
    }

    [Fact]
    public void Schedule_passes_a_cleared_only_wave_only_after_the_clear()
    {
        var (engine, _) = Started(WaveListEvent("ws", "", Wave(), Wave()));       // wave 2: neither key, waits for the clear (D40)
        var clears = new Clears();
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0, 2);
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(500), clears.Of));
        Assert.Null(WaveSchedule.KnownAt(engine.Find("ws")!));
        clears.Waves.Add(1);
        Assert.Equal(2, engine.NextWave("ws", T0.AddSeconds(500), clears.Of)!.Wave);
    }

    [Fact]
    public void Schedule_passes_an_after_only_wave_without_the_clear()
    {
        var (engine, _) = Started(WaveListEvent("ws", "", Wave(), Wave("\"afterSeconds\": 60, \"whenCleared\": false")));
        var clears = new Clears { Waves = { 1 } };
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0.AddSeconds(5), 2);
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(64), clears.Of));
        Assert.Equal(2, engine.NextWave("ws", T0.AddSeconds(65), clears.Of)!.Wave);           // decision + 60 s
    }

    /// <summary>A3: the engine's cleared read counts only the running instance's units; one left by an earlier instance
    /// (a stop and start inside the grace) does not hold wave 1.</summary>
    [Fact]
    public void Schedule_fails_when_an_earlier_instance_unit_holds_a_wave()
    {
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 10));
        ledger.Request("CHAR_Bandit_Thug", "ws-three", 1, new UnitLifetime(DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f), wave: 1);
        Assert.True(ledger.Confirm(ledger.TakeSpawns().Single(), 5, T0.AddSeconds(-30)));   // the stopped instance's unit
        var (engine, _) = Started(WsThree());
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0, 4);
        Assert.Equal(2, engine.NextWave("ws-three", T0.AddSeconds(1), engine.ClearedBy(ledger))!.Wave);
        Assert.False(engine.ClearedBy(ledger)("other", 1));                    // no running instance: never cleared
    }

    [Fact]
    public void Schedule_passes_a_skipped_wave_cleared_at_its_decision()
    {
        var (engine, _) = Started(WsThree());
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 10));      // a skipped wave queued nothing
        bool Ledger(string id, int w) => ledger.WaveCleared(id, w, T0);
        engine.WaveDecided("ws-three", WaveOutcome.Skip, T0, 0);
        Assert.Equal(2, engine.NextWave("ws-three", T0.AddSeconds(1), Ledger)!.Wave);
        engine.WaveDecided("ws-three", WaveOutcome.ZeroRolled, T0.AddSeconds(1), 0);
        Assert.Equal(3, engine.NextWave("ws-three", T0.AddSeconds(2), Ledger)!.Wave);

        var (capped, _) = Started(WsThree());
        capped.WaveDecided("ws-three", WaveOutcome.Spawn, T0, queued: 0);         // the tracked cap full: nothing queued
        Assert.Equal(2, capped.NextWave("ws-three", T0.AddSeconds(1), Ledger)!.Wave);
    }

    [Fact]
    public void Schedule_fails_when_a_blocked_wave_releases_its_successor()
    {
        var (engine, _) = Started(WsThree());
        var clears = new Clears { Waves = { 1, 2 } };
        engine.WaveDecided("ws-three", WaveOutcome.NoWave, T0, 0);                 // blocked: not decided
        Assert.Empty(engine.Find("ws-three")!.Decided);
        Assert.Equal(1, engine.NextWave("ws-three", T0.AddSeconds(1), clears.Of)!.Wave);   // wave 1 is still the next
    }

    [Fact]
    public void Schedule_fails_when_a_wave_due_at_the_end_spawns()
    {
        var (engine, _) = Started(WaveListEvent("ws", "", Wave(), Wave("\"afterSeconds\": 600")));
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0, 2);
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(600), new Clears().Of));                 // the end is T0 + 600
        Assert.Null(UpcomingWave.Of(engine.Find("ws")!));
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(600), new Clears { Waves = { 1 } }.Of));
    }

    [Fact]
    public void Schedule_fails_when_the_units_form_changes()
    {
        var (engine, _) = Started(Json.Event());                               // 3 waves every 60 s
        var cleared = new Clears { Waves = { 1, 2, 3 } };
        Assert.Equal(1, engine.NextWave("raid", T0, cleared.Of)!.Wave);
        engine.WaveSpawned("raid");
        Assert.Null(engine.NextWave("raid", T0.AddSeconds(59), cleared.Of));     // a clear never brings it forward
        Assert.Equal(2, engine.NextWave("raid", T0.AddSeconds(60), cleared.Of)!.Wave);
        engine.WaveSkipped("raid");
        Assert.Equal(3, engine.NextWave("raid", T0.AddSeconds(120))!.Wave);
        Assert.Equal(T0.AddSeconds(120), WaveSchedule.KnownAt(engine.Find("raid")!));
    }

    [Fact]
    public void Schedule_empty_no_wave_left()
    {
        var (engine, _) = Started(WaveListEvent("ws", "", Wave()));
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0, 2);
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(100), new Clears { Waves = { 1 } }.Of));
        Assert.Null(WaveSchedule.KnownAt(engine.Find("ws")!));
        Assert.Null(engine.NextWave("nobody", T0));
    }

    // ---- D6 Readers ----

    [Fact]
    public void Readers_passes_the_warning_time_of_each_waveList_wave()
    {
        var (engine, _) = Started(WsThree());
        var a = engine.Find("ws-three")!;
        Assert.Equal((1, T0), UpcomingWave.Of(a));
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(2), 4);
        Assert.Equal((2, T0.AddSeconds(122)), UpcomingWave.Of(a));              // afterSeconds: a timed warning
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(40), 4);
        Assert.Null(UpcomingWave.Of(a));                                       // wave 3 waits for a clear: no warning
        Assert.Equal("2/3", WaveCount(a));
    }

    static string WaveCount(ActiveEvent a) =>
        ApiLines.Status([a], [], DefinitionSet.Empty, new Dictionary<string, int>(), false, T0).First().Split(' ')
            .First(t => t.StartsWith("wave=", StringComparison.Ordinal))["wave=".Length..];

    [Fact]
    public void Readers_fails_when_the_units_form_changes()
    {
        var (engine, _) = Started(Json.Event());
        var a = engine.Find("raid")!;
        engine.WaveSpawned("raid");
        Assert.Equal((2, T0.AddSeconds(60)), UpcomingWave.Of(a));
        Assert.Equal("1/3", WaveCount(a));
    }

    /// <summary>The source files that read <c>.IntervalSeconds</c> off a SpawnWavesAction; a member access on a type that
    /// declares its own constant IntervalSeconds (HuntPlan) is not one, nor is a comment.</summary>
    internal static IReadOnlyList<string> IntervalReaders(IReadOnlyList<(string Path, string Text)> files)
    {
        if (files.Count == 0) throw new InvalidOperationException("readers scan: 0 files");
        var constTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, text) in files)
            foreach (Match m in Regex.Matches(text, @"class\s+(\w+)[^{]*\{(?:(?!\bclass\b).)*?\bconst\s+int\s+IntervalSeconds\b", RegexOptions.Singleline))
                constTypes.Add(m.Groups[1].Value);
        var readers = new List<string>();
        foreach (var (path, text) in files)
        {
            var code = Regex.Replace(text, @"//[^\n]*", "");
            if (Regex.Matches(code, @"(\w+)[?!]?\.IntervalSeconds\b").Any(m => !constTypes.Contains(m.Groups[1].Value)))
                readers.Add(path);
        }
        return readers;
    }

    static readonly string[] ReaderAllowlist = ["Logic/Engine.cs"];

    static IReadOnlyList<(string Path, string Text)> ModSources()
    {
        var root = Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !f.StartsWith("obj/", StringComparison.Ordinal) && !f.StartsWith("bin/", StringComparison.Ordinal))
            .Select(f => (f, File.ReadAllText(Path.Combine(root, f))))
            .ToList();
    }

    [Fact]
    public void Readers_passes_only_the_schedule_reading_intervalSeconds()
    {
        var sources = ModSources();
        Assert.True(sources.Count > 50, $"readers scan: {sources.Count} files");
        Assert.Equal(ReaderAllowlist, IntervalReaders(sources));
    }

    [Fact]
    public void Readers_fails_when_a_planted_reader_is_added()
    {
        var sources = ModSources().ToList();
        sources.Add(("Logic/Planted.cs", "static class P { static int S(SpawnWavesAction a) => a.IntervalSeconds * 2; }"));
        sources.Add(("Services/Planted2.cs", "var t = def.Action?.IntervalSeconds;"));
        Assert.Equal(["Logic/Engine.cs", "Logic/Planted.cs", "Services/Planted2.cs"], IntervalReaders(sources));
        Assert.Empty(IntervalReaders([("Services/Hunt.cs", "public static class HuntPlan { public const int IntervalSeconds = 5; }\n" +
                                                            "x = now.AddSeconds(HuntPlan.IntervalSeconds); // a.IntervalSeconds")]));
    }

    [Fact]
    public void Readers_empty_scan_of_no_files() =>
        Assert.Equal("readers scan: 0 files", Assert.Throws<InvalidOperationException>(() => IntervalReaders([])).Message);

    // ---- D7 Victory ----

    [Fact]
    public void Victory_passes_the_last_wave_cleared()
    {
        var (engine, push) = Started(WsThree(extra: ", \"scoreboard\": true"));
        var clears = new Clears();
        for (var w = 1; w <= 3; w++) engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(w), 4);
        clears.Waves.UnionWith([1, 2]);
        Assert.Empty(engine.Complete(T0.AddSeconds(10), 60, clears.Of));       // wave 3 still has units
        clears.Waves.Add(3);
        var beaten = Assert.Single(engine.Complete(T0.AddSeconds(11), 60, clears.Of));
        Assert.Null(engine.Find("ws-three"));
        Assert.Equal(["ws-three"], push.Ended);                                // one event-end
        var cleanup = Assert.Single(engine.PendingCleanups);
        Assert.Equal(("ws-three", T0.AddSeconds(71)), (cleanup.EventId, cleanup.DueUtc));    // the same grace cleanup
        Assert.Equal("event ws-three ended: all waves defeated (3 of 3 waves)", ScoreboardRule.VictoryLine(beaten));
        Assert.Empty(engine.Complete(T0.AddSeconds(12), 60, clears.Of));        // never a second end
        Assert.Equal(["ws-three"], push.Ended);
    }

    [Fact]
    public void Victory_fails_when_an_earlier_wave_still_lives()
    {
        var (engine, _) = Started(WsThree());
        for (var w = 1; w <= 3; w++) engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(w), 4);
        Assert.Empty(engine.Complete(T0.AddSeconds(10), 60, new Clears { Waves = { 2, 3 } }.Of));   // A1 (b)
        Assert.Empty(engine.Complete(T0.AddSeconds(10), 60, new Clears { Waves = { 1, 2 } }.Of));
    }

    [Fact]
    public void Victory_fails_when_the_waves_are_not_all_decided()
    {
        var (engine, _) = Started(WsThree());
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0, 4);
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(1), 4);
        Assert.Empty(engine.Complete(T0.AddSeconds(10), 60, new Clears { Waves = { 1, 2, 3 } }.Of));
    }

    [Theory]
    [InlineData(WaveOutcome.Skip, -1)]
    [InlineData(WaveOutcome.ZeroRolled, -1)]
    [InlineData(WaveOutcome.Spawn, 0)]
    public void Victory_fails_when_a_wave_was_not_fought(WaveOutcome second, int queued)
    {
        var (engine, push) = Started(WsThree());
        var all = new Clears { Waves = { 1, 2, 3 } };
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0, 4);
        engine.WaveDecided("ws-three", second, T0.AddSeconds(1), queued);
        engine.WaveDecided("ws-three", WaveOutcome.Spawn, T0.AddSeconds(2), 4);
        Assert.Empty(engine.Complete(T0.AddSeconds(10), 60, all.Of));
        Assert.NotNull(engine.Find("ws-three"));
        Assert.Single(engine.Expire(T0.AddSeconds(600), 60));                 // it runs to its natural end
        Assert.Equal(["ws-three"], push.Ended);
    }

    [Fact]
    public void Victory_fails_when_the_skip_cascade_ends_the_event()
    {
        var (engine, _) = Started(WsThree());                                   // players gone: every wave skipped
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 10));
        bool Ledger(string id, int w) => ledger.WaveCleared(id, w, T0);
        var at = T0;
        while (engine.NextWave("ws-three", at, Ledger) is { } due)
        {
            engine.WaveDecided("ws-three", WaveOutcome.Skip, at, 0);
            at = at.AddSeconds(1);
        }
        Assert.Equal(3, engine.Find("ws-three")!.WavesSkipped);
        Assert.Empty(engine.Complete(at, 60, Ledger));
    }

    [Fact]
    public void Victory_fails_when_a_units_form_event_ends_early()
    {
        var (engine, _) = Started(Json.Event());
        for (var w = 0; w < 3; w++) engine.WaveSpawned("raid", T0.AddSeconds(60 * w), 5);
        Assert.Empty(engine.Complete(T0.AddSeconds(200), 60, new Clears { Waves = { 1, 2, 3 } }.Of));
        Assert.NotNull(engine.Find("raid"));
    }

    [Fact]
    public void Victory_empty_no_active_event()
    {
        var engine = new EventEngine(new EventCatalog());
        Assert.Empty(engine.Complete(T0, 60, (_, _) => true));
        Assert.Empty(engine.PendingCleanups);
    }

    // ---- D11 EndPaths ----

    static (ActiveEvent Ended, Scoreboard Board) Scored(bool scoreboard = true)
    {
        var (engine, _) = Started(WsThree(extra: scoreboard ? ", \"scoreboard\": true" : ""));
        var a = engine.Find("ws-three")!;
        var board = new Scoreboard();
        board.Kill("ws-three", new Scorer("1", "Chaos", false), includeAdmins: false);
        board.Death("ws-three", new Scorer("1", "Chaos", false), includeAdmins: false);
        return (a, board);
    }

    [Theory]
    [InlineData(EndPath.Natural)]
    [InlineData(EndPath.Victory)]
    [InlineData(EndPath.Stop)]
    public void EndPaths_passes_the_three_showing_paths(EndPath path)
    {
        var (a, board) = Scored();
        var end = ScoreboardRule.End(path, a, board);
        Assert.Equal(["Bandit raid scoreboard: 1. Chaos 1 kills, 1 deaths", "1 players, 1 kills, 1 deaths"], end.Chat);
        Assert.Equal("event ws-three scoreboard: 1 players, 1 kills, 1 deaths", end.Log);
        Assert.Equal(0, board.Events);                                           // the rows end with the event
    }

    [Theory]
    [InlineData(EndPath.Purge)]
    [InlineData(EndPath.Restart)]
    [InlineData(EndPath.PillarOff)]
    [InlineData(EndPath.Fault)]
    public void EndPaths_fails_when_a_silent_path_shows(EndPath path)
    {
        var (a, board) = Scored();
        var end = ScoreboardRule.End(path, a, board);
        Assert.Empty(end.Chat);
        Assert.Null(end.Log);
        Assert.Equal(0, board.Events);
    }

    [Fact]
    public void EndPaths_fails_when_an_event_without_the_key_shows()
    {
        var (a, board) = Scored(scoreboard: false);
        foreach (var path in Enum.GetValues<EndPath>())
            Assert.Same(ScoreboardEnd.None, ScoreboardRule.End(path, a, board));
    }

    [Fact]
    public void EndPaths_empty_no_player_scored()
    {
        var (engine, _) = Started(WsThree(extra: ", \"scoreboard\": true"));
        var end = ScoreboardRule.End(EndPath.Natural, engine.Find("ws-three")!, new Scoreboard());
        Assert.Equal(["Bandit raid scoreboard: no player scored"], end.Chat);
        Assert.Equal("event ws-three scoreboard: 0 players, 0 kills, 0 deaths", end.Log);
    }
}
