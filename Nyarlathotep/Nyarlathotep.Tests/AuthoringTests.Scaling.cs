using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D15: `.nyar event info` of a definition with the block shows "scaling: …" and, while it runs, "scaling
/// now: …" from its latest decision (ScalingBook); a definition without the block shows no scaling line; the running line is
/// dropped at every end path; every line fits 480 bytes; the rest of `event info` is unchanged.</summary>
public partial class AuthoringTests
{
    static readonly DateTime ScT0 = Zones.Utc(2026, 10, 31, 20, 0);

    static string ScaledAction(string block) => Json.ValidAction[..^2] + ", \"scaling\": " + block + " }";

    static EventDefinition ScaledDef(string block, string id = "raid") => Json.One(Json.Event(id, action: ScaledAction(block)));

    /// <summary>D15's end paths in Services/EventRuntime.cs (step 3, Codex step 2 F2): each reaches EndSpawnState, whose
    /// ScalingReads.EventEnded drops the instance's decision rows and read streak; the purge (the kill switch) and a restart
    /// clear everything; `event info` asks for the running line only of a running instance. A source check, as
    /// ApocalypseTests' ApocalypseRuntimeGaps: Services cannot be compiled into this project (Epic S-21).</summary>
    static readonly (string Call, string Gap)[] ScalingEndCalls =
    [
        ("ScalingReads.EventEnded(id);", "every path (EndSpawnState)"),
        ("EndSpawnState(id);", "stop, pillar off, fault cancel (End)"),
        ("EndSpawnState(ended.Id);", "natural end, apocalypse survived, won or lost"),
        ("EndSpawnState(beaten.Id);", "all waves defeated"),
        ("EndSpawnState(e.Id);", "purge per event"),
        ("ScalingReads.Clear();", "purge and restart"),
        ("scaling: active is not null ? Services.ScalingReads.NowLine(active) : null", "event info"),
    ];

    /// <summary>Each end path of the runtime that keeps its scaling rows, named.</summary>
    internal static List<string> ScalingEndGaps(string runtime, string commands)
    {
        runtime = runtime.Replace("\r\n", "\n");
        string Body(string signature) => EndPathTests.Body(runtime, signature);
        var gaps = new List<string>();
        if (!Body("static void EndSpawnState(string id)").Contains("ScalingReads.EventEnded(id);", StringComparison.Ordinal)) gaps.Add("every path (EndSpawnState)");
        if (!Body("static bool End(string id, string why, EndPath path, bool announce = true)").Contains("EndSpawnState(id);", StringComparison.Ordinal)) gaps.Add("stop, pillar off, fault cancel (End)");
        var tick = Body("internal static void Tick(DateTime now)");
        if (!tick.Contains("EndSpawnState(ended.Id);", StringComparison.Ordinal)
            || !Body("static void TickApocalypse(ActiveEvent active, DateTime now)").Contains("EndSpawnState(ended.Id);", StringComparison.Ordinal))
            gaps.Add("natural end, apocalypse survived, won or lost");
        if (!tick.Contains("EndSpawnState(beaten.Id);", StringComparison.Ordinal)) gaps.Add("all waves defeated");
        var purge = Body("internal static Outcome Purge()");
        if (!purge.Contains("EndSpawnState(e.Id);", StringComparison.Ordinal)) gaps.Add("purge per event");
        if (!purge.Contains("ScalingReads.Clear();", StringComparison.Ordinal) || !Body("internal static void Initialize()").Contains("ScalingReads.Clear();", StringComparison.Ordinal))
            gaps.Add("purge and restart");
        if (!System.Text.RegularExpressions.Regex.Replace(commands.Replace("\r\n", "\n"), @"//[^\n]*", "")
                .Contains("scaling: active is not null ? Services.ScalingReads.NowLine(active) : null", StringComparison.Ordinal))
            gaps.Add("event info");
        return gaps;
    }

    static string CommandsSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Commands", "EventCommands.cs"));
    }

    /// <summary>The scaled waveList events "raid" and "other" started on the real engine, each with one recorded decision.</summary>
    static (EventEngine Engine, ScalingRuntime Rt, EventDefinition Def) RunningScaled()
    {
        var action = "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ] }, " +
                     "{ \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ], \"afterSeconds\": 60 } ], \"radius\": 10, " +
                     "\"location\": { \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }, \"scaling\": { \"unitsPerPlayer\": 2 } }";
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Json.Event("raid", action: action), Json.Event("other", action: action)),
            FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(ScT0, [1])));
        var engine = new EventEngine(catalog);
        var open = new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);
        Assert.Null(engine.Start("raid", "manual", ScT0, open));
        Assert.Null(engine.Start("other", "manual", ScT0, open with { ActiveEvents = 1 }));
        var rt = new ScalingRuntime();
        var rig = new ScalingRig();
        rig.Rows.AddRange([new(-1000, -800, true, true, false, 40), new(-1000, -800, true, true, false, 40)]);
        foreach (var id in new[] { "raid", "other" })
        {
            var a = engine.Find(id)!;
            rt.Wave(a, a.Definition.Action!, 1, rt.Read(a, a.Definition.Action!.Scaling, Scope.Global, rig, () => true, _ => { }), null, _ => { });
        }
        return (engine, rt, engine.Find("raid")!.Definition);
    }

    /// <summary>`event info` as Commands/EventCommands builds it: the running line only for a running instance.</summary>
    static IReadOnlyList<string> InfoOf(EventEngine engine, ScalingRuntime rt, EventDefinition def)
    {
        var active = engine.Find(def.Id);
        return EventLines.Info(def, active, ScT0, scaling: active is not null ? rt.NowLine(active) : null);
    }

    static bool ShowsNow(IReadOnlyList<string> lines) => lines.Any(l => l.StartsWith("scaling now:", StringComparison.Ordinal));

    [Fact]
    public void Scaling_fails_when_a_scaled_definition_lacks_the_line_or_a_value_is_wrong()
    {
        var full = EventLines.Info(ScaledDef("{ \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": -3, \"levelPerWave\": 1 }"), null, ScT0);
        Assert.Contains("scaling: +2 units a player beyond the first, level players' gear -3, +1 a wave", full);
        Assert.Contains("scaling: +3 units a player beyond the first, level own", EventLines.Info(ScaledDef("{ \"unitsPerPlayer\": 3 }"), null, ScT0));
        Assert.Contains("scaling: +0 units a player beyond the first, level players' gear +2, +0 a wave",
            EventLines.Info(ScaledDef("{ \"levelFromPlayers\": true, \"levelOffset\": 2 }"), null, ScT0));
        Assert.Contains("scaling: +0 units a player beyond the first, level players' gear +0, +0 a wave",
            EventLines.Info(ScaledDef("{ \"levelFromPlayers\": true }"), null, ScT0));
        Assert.Contains("scaling: +1 units a player beyond the first, level own",
            EventLines.Info(Apo.Def(Apo.Action(extra: "\"scaling\": { \"unitsPerPlayer\": 1 }")), null, ScT0));
        // the line sits after the action lines, before the trigger scope
        var at = full.ToList().FindIndex(l => l.StartsWith("scaling: ", StringComparison.Ordinal));
        Assert.Equal("trigger scope: Global", full[at + 1]);
    }

    [Fact]
    public void Scaling_fails_when_an_unscaled_definition_shows_one()
    {
        var def = Json.One(Json.Event("raid"));
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        Assert.DoesNotContain(EventLines.Info(def, null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.DoesNotContain(EventLines.Info(def, active, ScT0, scaling: "scaling now: 3 players, gear 40, level 42"),
            l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.DoesNotContain(EventLines.Info(Apo.Def(), null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
    }

    [Fact]
    public void Scaling_fails_when_the_running_line_outlives_an_end_path()
    {
        // the runtime: every end path reaches the drop, the purge and a restart clear, event info asks only while running
        var runtime = EndPathTests.RuntimeSource().Replace("\r\n", "\n");
        var commands = CommandsSource().Replace("\r\n", "\n");
        Assert.Empty(ScalingEndGaps(runtime, commands));
        foreach (var (call, gap) in ScalingEndCalls)
        {
            var inRuntime = runtime.Contains(call, StringComparison.Ordinal);
            Assert.True(inRuntime || commands.Contains(call, StringComparison.Ordinal), call);
            Assert.Contains(gap, inRuntime ? ScalingEndGaps(runtime.Replace(call, "x"), commands) : ScalingEndGaps(runtime, commands.Replace(call, "x")));
            Assert.Contains(gap, inRuntime                                                       // a commented-out call is no call
                ? ScalingEndGaps(runtime.Replace(call, "// " + call), commands)
                : ScalingEndGaps(runtime, commands.Replace(call, "// " + call)));
        }

        // the engine's own end paths, each followed by the runtime's drop (EndSpawnState's ScalingReads.EventEnded)
        var paths = new (string Name, Func<EventEngine, IReadOnlyList<ActiveEvent>> End)[]
        {
            ("natural end", e => e.Expire(ScT0.AddSeconds(600), 60).Where(a => a.Id == "raid").ToList()),
            ("all waves defeated", e =>
            {
                e.WaveDecided("raid", WaveOutcome.Spawn, ScT0.AddSeconds(1), 2);
                e.WaveDecided("raid", WaveOutcome.Spawn, ScT0.AddSeconds(61), 2);
                return e.Complete(ScT0.AddSeconds(90), 60, (id, _) => id == "raid");
            }),
            ("apocalypse won or lost (Engine.Complete of one id)", e => [e.Complete("raid", ScT0.AddSeconds(5), 60)!]),
            ("stop, pillar off, fault cancel (Engine.Cancel)", e => [e.Cancel("raid")!]),
        };
        foreach (var (name, end) in paths)
        {
            var (engine, rt, def) = RunningScaled();
            Assert.True(ShowsNow(InfoOf(engine, rt, def)), name);
            var ended = Assert.Single(end(engine));
            Assert.Equal("raid", ended.Id);
            Assert.False(ShowsNow(InfoOf(engine, rt, def)), name);                             // not running: no line
            rt.EventEnded(ended.Id);
            Assert.Null(rt.Book.Latest(ended));                                                 // and no row kept
            if (engine.Find("other") is { } other) Assert.NotNull(rt.Book.Latest(other));      // another event's stays
        }
        // the purge (the kill switch): every event ends, and the runtime clears everything
        {
            var (engine, rt, def) = RunningScaled();
            var all = engine.CancelAll(30);
            Assert.Equal(2, all.Count);
            Assert.False(ShowsNow(InfoOf(engine, rt, def)));
            foreach (var e in all) rt.EventEnded(e.Id);
            rt.Clear();
            Assert.Equal(0, rt.Book.Count);
        }
        // a restart: a cleared runtime; a new instance never shows the last one's decision
        {
            var (engine, rt, def) = RunningScaled();
            var before = engine.Find("raid")!;
            rt.Clear();
            Assert.Equal(0, rt.Book.Count);
            Assert.False(ShowsNow(InfoOf(engine, rt, def)));
            var later = new ActiveEvent(new RunningInstance(def, ScT0.AddHours(1), ScT0.AddHours(1).AddSeconds(600)), "manual", null);
            rt.Book.Record(before, new ScalingView(3, 40, null));
            Assert.Null(rt.Book.Latest(later));
        }
        // a stopped event shows no running line even when one is passed
        Assert.DoesNotContain(EventLines.Info(RunningScaled().Def, null, ScT0, scaling: "scaling now: 3 players, gear 40, level own"),
            l => l.StartsWith("scaling now", StringComparison.Ordinal));
    }

    [Fact]
    public void Scaling_fails_when_a_line_exceeds_480_bytes()
    {
        var id = new string('a', 32);
        var def = ScaledDef("{ \"unitsPerPlayer\": 5, \"levelFromPlayers\": true, \"levelOffset\": -20, \"levelPerWave\": 5 }", id);
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var lines = EventLines.Info(def, active, ScT0, scaling: Scaling.Lines.Now(new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue)));
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
        Assert.All(new[]
        {
            Scaling.Lines.Info(new Scaling(int.MaxValue, true, int.MinValue, int.MaxValue)),
            Scaling.Lines.Wave(id, int.MaxValue, new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue), int.MaxValue, int.MaxValue, Enumerable.Repeat(int.MaxValue, 10).ToArray()),
            Scaling.Lines.Horde(id, int.MaxValue, new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue), int.MaxValue, int.MaxValue),
            Scaling.Lines.Roamers(id, int.MaxValue, int.MaxValue, int.MaxValue),
        }, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Scaling_fails_when_another_line_of_event_info_changes()
    {
        var plain = Json.One(Json.Event("raid"));
        var scaled = ScaledDef("{ \"unitsPerPlayer\": 2 }");
        Assert.Equal(EventLines.Info(plain, null, ScT0), EventLines.Info(scaled, null, ScT0).Where(l => !l.StartsWith("scaling", StringComparison.Ordinal)));
        var a = new ActiveEvent(new RunningInstance(plain, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var b = new ActiveEvent(new RunningInstance(scaled, ScT0, ScT0.AddSeconds(600)), "manual", null);
        Assert.Equal(EventLines.Info(plain, a, ScT0.AddSeconds(30)),
            EventLines.Info(scaled, b, ScT0.AddSeconds(30), scaling: "scaling now: 3 players, gear 40, level own").Where(l => !l.StartsWith("scaling", StringComparison.Ordinal)));
        Assert.Equal(EventLines.Info(plain, null, ScT0).Count + 1, EventLines.Info(scaled, null, ScT0).Count);
    }

    [Fact]
    public void Scaling_passes_running_line()
    {
        var def = ScaledDef("{ \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": -1 }");
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var book = new ScalingBook();
        book.Record(active, new ScalingView(2, 30, 29));
        book.Record(active, Scaling.ForWave(def.Action!, "raid", 1, new ScalingRead(3, 40, [])).View!.Value);     // the latest wins
        var lines = EventLines.Info(def, active, ScT0.AddSeconds(30), scaling: Scaling.Lines.Now(book.Latest(active)!.Value));
        Assert.Contains("scaling now: 3 players, gear 40, level 39", lines);
        Assert.Equal("scaling now: 0 players, gear none, level own", Scaling.Lines.Now(new ScalingView(0, null, null)));
        Assert.True(lines.ToList().IndexOf("scaling now: 3 players, gear 40, level 39") > lines.ToList().FindIndex(l => l.StartsWith("scaling: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Scaling_empty_not_running_no_line()
    {
        var def = ScaledDef("{ \"unitsPerPlayer\": 2 }");
        Assert.Single(EventLines.Info(def, null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.Null(new ScalingBook().Latest(null));
    }
}
