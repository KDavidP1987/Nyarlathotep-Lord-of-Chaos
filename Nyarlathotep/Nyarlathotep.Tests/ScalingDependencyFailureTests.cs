using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>
/// scaling D12 (Reads guarded), the dependency suite's two categories (D13): player-count (the player read with its admin
/// flag) and gear-read (the Equipment read and the V Blood read). A throwing player read leaves the decision unscaled at its
/// configured counts and levels (never more units), logs "scaling &lt;event&gt;: player read failed: &lt;message&gt;" once per
/// streak and holds "scaling: player read failing" until a clean read, the event's end or a restart; one unreadable gear
/// level leaves that player out of the average only; a throwing V Blood read keeps the configured level; nothing escapes into
/// the wave, the apocalypse tick or an end. Driven through Logic/Scaling's ScalingRuntime (Services/ScalingReads holds the
/// server's one) and Logic/ApocalypseHost over the real engine and ledger; the Services side, which this project cannot
/// compile (Epic S-21), by a source check of its guards and calls.
/// </summary>
public class ScalingDependencyFailureTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    const string Broke = "player read broke";
    const string FailedSc = "scaling sc: player read failed: InvalidOperationException";
    const string Block2 = "\"scaling\": { \"unitsPerPlayer\": 2, \"levelFromPlayers\": true }";

    static ScalingPlayer P(double? gear = 40, bool admin = false) => new(-1000, -800, true, true, admin, gear);

    static ScalingRig Three(string? throws = null)
    {
        var rig = new ScalingRig { Throw = throws };
        rig.Rows.AddRange([P(), P(), P()]);
        return rig;
    }

    static string Waves(string? extra = Block2) =>
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 3 } ], \"waves\": 3, " +
        "\"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }" + (extra is null ? "" : ", " + extra) + " }";

    static EventDefinition Def(string? extra = Block2) => ScalingTests.Def(Waves(extra));

    static ActiveEvent Active(EventDefinition def, string id = "sc") =>
        new(new RunningInstance(def with { Id = id }, T0, T0.AddSeconds(600)), "manual", null);

    static IReadOnlyList<int> Rolled(ScaledWave wave) =>
        WaveGate.DecideGroupEntries(new WaveFacts(1, "sc", false), [new GroupCentre(0, 0, 0, false)], i => WaveRoll.ExpandEntries(wave.Entries(i), new StepRandom()),
            50, 0, 500).Groups.Select(g => g.Units.Sum(u => u.Count)).ToList();

    // ---- player-count: the player read and its admin flag

    [Fact]
    public void PlayerCount_fails_when_a_throwing_read_stops_or_enlarges_a_wave()
    {
        var def = Def();
        var active = Active(def);
        var rt = new ScalingRuntime();
        var warns = new List<string>();
        var logs = new List<string>();
        var rig = Three(Broke);
        var read = rt.Read(active, def.Action!.Scaling, def.Action.Scope, rig, () => true, warns.Add);   // never throws
        Assert.Null(read);
        Assert.Equal(1, rig.Reads);
        var wave = rt.Wave(active, def.Action, 1, read, null, logs.Add);
        Assert.Equal([3], Rolled(wave));                                                   // the configured wave, never more
        Assert.Empty(logs);                                                                 // no scaling line for an unscaled decision
        Assert.Null(rt.NowLine(active));
        Assert.False(SpawnTuning.For(def.Action, wave.Entries(0)[0], read, 1).Scaled);     // the configured level
        // the same read clean scales the wave: 3 + 2 × 2
        rig.Throw = null;
        var clean = rt.Read(active, def.Action.Scaling, def.Action.Scope, rig, () => true, warns.Add);
        Assert.Equal([7], Rolled(rt.Wave(active, def.Action, 1, clean, null, logs.Add)));
        Assert.Single(logs);
        // an event without the block reads nothing at all
        var plain = Def(null);
        Assert.Null(rt.Read(Active(plain, "plain"), plain.Action!.Scaling, plain.Action.Scope, rig, () => true, warns.Add));
        Assert.Equal(2, rig.Reads);
    }

    [Fact]
    public void PlayerCount_fails_when_the_error_line_repeats_within_a_streak()
    {
        var def = Def();
        var rt = new ScalingRuntime();
        var warns = new List<string>();
        var rig = Three(Broke);
        for (var i = 0; i < 3; i++) rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);
        Assert.Equal([FailedSc], warns);
        rig.Throw = null;
        rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);   // the streak ends
        rig.Throw = Broke;
        rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);   // a new one logs again
        Assert.Equal([FailedSc, FailedSc], warns);
        // each event has its own streak; the health line has one entry however many fail
        rt.Read(Active(def, "other"), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);
        Assert.Equal("scaling other: player read failed: InvalidOperationException", warns[^1]);
        Assert.Equal([ScalingGuard.Entry], rt.Health);
        Assert.Equal("scaling: player read failing", ScalingGuard.Entry);
        // a throwing log sink never reaches the decision
        var quiet = new ScalingRuntime();
        Assert.Null(quiet.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, _ => throw new InvalidOperationException("log down")));
    }

    [Fact]
    public void PlayerCount_fails_when_the_health_entry_outlives_a_clean_read_or_the_end()
    {
        var def = Def();
        var rt = new ScalingRuntime();
        var rig = Three(Broke);
        void Fail(string id = "sc") { rig.Throw = Broke; rt.Read(Active(def, id), def.Action!.Scaling, Scope.Global, rig, () => true, _ => { }); }
        Fail();
        Assert.Equal([ScalingGuard.Entry], rt.Health);
        rig.Throw = null;
        rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, _ => { });
        Assert.Empty(rt.Health);                                                           // a clean read
        Fail();
        rt.EventEnded("other");
        Assert.Equal([ScalingGuard.Entry], rt.Health);                                     // another event's end keeps it
        rt.EventEnded("sc");
        Assert.Empty(rt.Health);                                                           // the event's end
        Fail();
        Fail("other");
        rt.EventEnded("sc");
        Assert.Equal([ScalingGuard.Entry], rt.Health);                                     // "other" still fails
        rt.Clear();
        Assert.Empty(rt.Health);                                                           // a restart or a purge
    }

    [Fact]
    public void PlayerCount_fails_when_a_throw_skips_an_apocalypse_phase_or_an_end()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Apo.Action(extra: Block2));
        var rt = new ScalingRuntime();
        var reader = Three(Broke);
        var flag = new Func<bool>(() => throw new InvalidOperationException("cfg down"));    // a throwing IncludeAdmins read too
        rig.Game.Scale = a => rt.Source(a, reader, flag, rig.Game.Warn);
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        Assert.Equal(6, rig.Game.Ledger.TakeSpawns().Count);                               // 2 hotspots × 3 roamers, as configured
        var rose = rig.Tick(T0.AddMinutes(1));
        Assert.Equal((1, 8), (rose.Rose!.Value, rose.Queued));                              // horde 1 at its configured 8
        Assert.Equal([FailedScOf("apo")], rig.Game.Warns.Where(w => w.StartsWith("scaling", StringComparison.Ordinal)));   // once for the streak
        Assert.DoesNotContain(rig.Game.Logs, l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.Equal([ScalingGuard.Entry], rt.Health);
        // the later parts of the tick and the end still run
        for (var s = 1; s <= 5; s++) Assert.Equal(ApocalypseResult.None, rig.Tick(T0.AddMinutes(1).AddSeconds(s)).Result);
        rig.Host.Ended("apo", ApocalypseEnd.Stop, rig.Game, rig.State);
        rt.EventEnded("apo");
        Assert.Null(rig.Host.Run);
        Assert.Empty(rt.Health);
        // a clean reader scales the next run's start: 3 + 2 × 2 a hotspot (the flag still throwing counts no admin)
        reader.Throw = null;
        var next = ApocalypseDependencyFailureTests.Make(Apo.Action(extra: Block2));
        next.Game.Scale = a => rt.Source(a, reader, flag, next.Game.Warn);
        Assert.Null(next.Start(T0));
        Assert.True(next.Host.Begin(next.Active, next.Game, next.State, next.Board, T0));
        Assert.Equal(14, next.Game.Ledger.TakeSpawns().Count);
    }

    static string FailedScOf(string id) => $"scaling {id}: player read failed: InvalidOperationException";

    [Fact]
    public void PlayerCount_fails_when_an_admin_read_counts_or_drops_a_player()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(admin: true), P(), P()]);
        Assert.Equal(2, Scaling.Read(rig, Scope.Global, false).Players);                  // an admin left out with IncludeAdmins false
        Assert.Equal(3, Scaling.Read(rig, Scope.Global, true).Players);
        // an admin flag that cannot be read throws out of the reader (Services/ScalingReads IsAdmin), so the whole decision
        // goes unscaled: never an admin counted as a player, never more units
        var failing = new ScalingRig { Throw = "admin flag unreadable" };
        failing.Rows.AddRange([P(admin: true), P(), P()]);
        Assert.Null(new ScalingRuntime().Read(Active(Def()), Def().Action!.Scaling, Scope.Global, failing, () => false, _ => { }));
        var source = ServicesSource();
        Assert.Empty(ServicesGaps(source));
        foreach (var (file, call, gap) in Plants.Where(p => p.Gap.Contains("admin", StringComparison.Ordinal)))
            Assert.Equal([gap], ServicesGaps(new Dictionary<string, string>(source) { [file] = source[file].Replace(call, "x") }));
        // an admin read that swallows its failure (and so counts the admin) is caught
        var swallowed = source["ScalingReads"].Replace("return user.Read<User>().IsAdmin;", "try { return user.Read<User>().IsAdmin; } catch { return false; }");
        Assert.Equal(["admin read failure swallowed"], ServicesGaps(new Dictionary<string, string>(source) { ["ScalingReads"] = swallowed }));
        // the shared player query reads nothing new for scaling (D18)
        var shared = source["HuntAction"] + "\n bool A(User u) => u.IsAdmin;";
        Assert.Equal(["player query reads scaling's fields"], ServicesGaps(new Dictionary<string, string>(source) { ["HuntAction"] = shared }));
    }

    [Fact]
    public void PlayerCount_passes_good_read_ends_the_streak()
    {
        var def = Def();
        var rt = new ScalingRuntime();
        var warns = new List<string>();
        var rig = Three(Broke);
        rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);
        rig.Throw = null;
        var read = rt.Read(Active(def), def.Action!.Scaling, Scope.Global, rig, () => true, warns.Add);
        Assert.Equal((3, (int?)40), (read!.Players, read.Gear));
        Assert.Empty(rt.Health);
        Assert.Equal([FailedSc], warns);
    }

    [Fact]
    public void PlayerCount_empty_no_block_no_read()
    {
        var plain = Def(null);
        var rt = new ScalingRuntime();
        var rig = Three(Broke);
        var flagReads = 0;                                                                 // D18: no setting read either
        Assert.Null(rt.Read(Active(plain), plain.Action!.Scaling, Scope.Global, rig, () => ++flagReads > 0, _ => throw new InvalidOperationException("no line")));
        Assert.Equal((0, 0), (rig.Reads, flagReads));
        Assert.Empty(rt.Health);
        Assert.Null(rt.Source(Apo.Active(Apo.Def(), T0), rig, () => true, _ => { }));          // an apocalypse without the block: no source
        Assert.Equal(0, rig.Reads);
    }

    // ---- gear-read: the Equipment read and the V Blood read

    [Fact]
    public void GearRead_fails_when_one_unreadable_gear_drops_other_players()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(30.9), P(null), P(double.NaN), P(double.PositiveInfinity), P(50)]);
        var read = Scaling.Read(rig, Scope.Global, true);
        Assert.Equal(5, read.Players);                                                     // the unreadable ones still count
        Assert.Equal(40, read.Gear);                                                       // (30 + 50) / 2
        rig.Rows.Clear();
        rig.Rows.AddRange([P(null), P(null)]);
        Assert.Equal((2, (int?)null), (Scaling.Read(rig, Scope.Global, true).Players, Scaling.Read(rig, Scope.Global, true).Gear));
        // no average: the level stays as configured, the count still scales
        var def = Def();
        var noGear = Scaling.Read(rig, Scope.Global, true);
        Assert.False(SpawnTuning.For(def.Action!, def.Action!.Units[0], noGear, 1).Scaled);
        Assert.Equal([5], Rolled(Scaling.ForWave(def.Action, "sc", 1, noGear)));
    }

    [Fact]
    public void GearRead_passes_readable_gear_averages()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([P(38.7), P(41), P(44.2)]);
        Assert.Equal(41, Scaling.Read(rig, Scope.Global, true).Gear);                      // 38, 41, 44
    }

    [Fact]
    public void GearRead_empty_no_readable_gear_no_average()
    {
        var rig = new ScalingRig();
        Assert.Null(Scaling.Read(rig, Scope.Global, true).Gear);
        rig.Rows.Add(P(null));
        Assert.Null(Scaling.Read(rig, Scope.Global, true).Gear);
    }

    [Fact]
    public void GearRead_fails_when_a_throwing_vblood_read_scales_the_unit()
    {
        var def = Def();
        var rig = new ScalingRig();
        rig.Rows.Add(P(44));
        var scaled = SpawnTuning.For(def.Action!, def.Action!.Units[0], Scaling.Read(rig, Scope.Global, true), 1);
        Assert.True(scaled.Scaled);
        Assert.Equal(new LevelArg(false, 44), Scaling.LevelAtSetup(scaled, () => false));
        Assert.Null(Scaling.LevelAtSetup(scaled, () => throw new InvalidOperationException("prefab read failed")));   // the prefab's own level
        var own = Scaling.Tune(SpawnTuning.TuningFrom(new SpawnModifiers(LevelDelta: 2)), def.Action.Scaling, 44, new SpawnModifiers(LevelDelta: 2), 1);
        Assert.Equal(new LevelArg(true, 2), Scaling.LevelAtSetup(own, () => throw new InvalidOperationException("prefab read failed")));
        // an unscaled unit never asks
        var asked = false;
        var plain = SpawnTuning.TuningFrom(new SpawnModifiers(Level: 30));
        Assert.Equal(new LevelArg(false, 30), Scaling.LevelAtSetup(plain, () => { asked = true; throw new InvalidOperationException("never"); }));
        Assert.False(asked);
    }

    [Fact]
    public void GearRead_fails_when_the_services_gear_or_vblood_read_is_unguarded()
    {
        var source = ServicesSource();
        Assert.Empty(ServicesGaps(source));
        foreach (var (file, call, gap) in Plants)
        {
            Assert.Contains(call, source[file]);
            var planted = new Dictionary<string, string>(source) { [file] = source[file].Replace(call, "x") };
            Assert.Equal([gap], ServicesGaps(planted));
        }
    }

    // ---- the Services side (source check, as EndPathTests' RuntimeGaps)

    static readonly (string File, string Call, string Gap)[] Plants =
    [
        ("ScalingReads", "IsAdmin(p.Character), GearLevel(p.Character)", "reader drops admin or gear"),
        ("ScalingReads", "return user.Read<User>().IsAdmin;", "admin read missing"),
        ("ScalingReads", "catch { return null; }", "gear read unguarded"),
        ("UnitSetup", "Scaling.LevelAtSetup(tuning, () => ScalingReads.IsVBlood(unit))", "setup without the V Blood guard"),
        ("ScalingReads", "State.Read(active, action.Scaling, action.Scope, _reader, IncludeAdmins, Warn)", "wave read unguarded or eager"),
        ("ScalingReads", "State.Source(active, _reader, IncludeAdmins, Warn)", "apocalypse source unguarded or eager"),
        ("WaveAction", "var read = ScalingReads.Read(active, action);", "wave without a read"),
        ("WaveAction", "SpawnTuning.For(action, entry, read, due.Wave)", "wave without the scaled level"),
        ("WaveAction", "i => WaveRoll.ExpandEntries(scaled.Entries(i), _rng)", "groups without their own entries"),
        ("ApocalypseAction", "public ScalingSource Scaling(ActiveEvent active) => ScalingReads.Source(active);", "apocalypse without a source"),
        ("ApocalypseAction", "public void ResumeCancelled(string eventId) => ScalingReads.EventEnded(eventId);", "failed resume keeps its rows"),
        ("HealthMonitor", ".Concat(ScalingReads.Health)", "no health entry"),
    ];

    static Dictionary<string, string> ServicesSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return new[] { "HuntAction", "UnitSetup", "ScalingReads", "WaveAction", "ApocalypseAction", "HealthMonitor" }.ToDictionary(f => f, f =>
            System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", f + ".cs")).Replace("\r\n", "\n"),
                @"^\s*//[^\n]*", "", System.Text.RegularExpressions.RegexOptions.Multiline));
    }

    /// <summary>Each guard or call of the Services side that is missing, named.</summary>
    static List<string> ServicesGaps(Dictionary<string, string> source)
    {
        var gaps = Plants.Where(p => !source[p.File].Contains(p.Call, StringComparison.Ordinal)).Select(p => p.Gap).ToList();
        // D18: PlayerQuery, shared by Hunt, the picks, revenge and the unit watch, reads nothing new for scaling
        if (source["HuntAction"].Contains("Equipment", StringComparison.Ordinal) || source["HuntAction"].Contains(".IsAdmin", StringComparison.Ordinal))
            gaps.Add("player query reads scaling's fields");
        // D12: an admin flag that cannot be read throws out of the reader, never becomes false
        var reads = source["ScalingReads"];
        var at = reads.IndexOf("static bool IsAdmin(Entity character)", StringComparison.Ordinal);
        var end = at < 0 ? -1 : reads.IndexOf("\n        }\n", at, StringComparison.Ordinal);
        if (at >= 0 && end > at && reads[at..end].Contains("catch", StringComparison.Ordinal)) gaps.Add("admin read failure swallowed");
        return gaps;
    }
}
