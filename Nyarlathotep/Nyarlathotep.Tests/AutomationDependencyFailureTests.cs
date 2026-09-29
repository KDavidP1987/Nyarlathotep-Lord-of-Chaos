using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D15: the dependency failures of the player-action triggers and the Interval clock, one control per
/// category of tools/preflight-checks.json › dependencySuites.automation (player-scan, kill-read, state-write). Each
/// failure skips its scan or death, logs once per streak, holds its health entry until a read succeeds and never
/// throws; garbage is refused where read.</summary>
public class AutomationDependencyFailureTests
{
    internal static readonly DateTime T0 = Zones.Utc(2026, 9, 29, 20, 0);

    static DefinitionSet Set(params string[] events) =>
        EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()).Set;

    // FakeRegions: x < 0 FarbaneWoods, x >= 0 CursedForest
    internal static readonly Func<float, float, string> RegionOf = new FakeRegions().RegionOf;

    const string Border = "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": 5 }";
    const string Reprisal = "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }";
    const string ScopedReprisal = "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60, \"scope\": [\"CursedForest\"] }";

    internal static readonly DefinitionSet Entered = Set(Json.Event("border", Border));
    static readonly DefinitionSet Kills = Set(Json.Event("reprisal", Reprisal));

    internal static ScanRow At(float x, string id = "p1") => new(id, x, 0, true);

    static KillFacts Kill(string faction = "Faction_Bandits", float? x = null, float? z = null) =>
        new("p1", null, false, false, false, faction, x, z);

    static bool AllHooks(TriggerType _) => true;

    /// <summary>Runs one scan and returns the fires.</summary>
    static List<PlayerFire> Scan(PlayerTriggerFeed feed, Func<IReadOnlyList<ScanRow>> read, DefinitionSet set, DateTime at, LogLines log,
        Func<float, float, string>? regionOf = null)
    {
        var fires = new List<PlayerFire>();
        feed.Scan(read, regionOf ?? RegionOf, set, _ => false, at, log.Add, fires.Add);
        return fires;
    }

    static List<PlayerFire> Die(PlayerTriggerFeed feed, Func<bool, KillFacts> read, DefinitionSet set, DateTime at, LogLines log,
        Func<TriggerType, bool>? allows = null)
    {
        var fires = new List<PlayerFire>();
        feed.Died(read, set, RegionOf, allows ?? AllHooks, _ => false, at, log.Add, fires.Add);
        return fires;
    }

    // ---- player-scan

    [Fact]
    public void PlayerScan_fails_when_throwing_scan_escapes_or_logs_twice()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        IReadOnlyList<ScanRow> Throw() => throw new InvalidOperationException("user query gone");
        for (var i = 0; i < 3; i++) Assert.Empty(Scan(feed, Throw, Entered, T0.AddSeconds(5 * i), log));
        Assert.Equal(["player triggers: scan failed: user query gone"], log.Lines);
        Assert.Equal([PlayerTriggerFeed.ScanFailing], feed.Health);
        // a throwing region read is a failed scan too
        var regionLog = new LogLines();
        var other = new PlayerTriggerFeed();
        Scan(other, () => [At(10)], Entered, T0, regionLog, (_, _) => throw new InvalidOperationException("index gone"));
        Assert.Equal(["player triggers: scan failed: index gone"], regionLog.Lines);
        Assert.Contains(PlayerTriggerFeed.ScanFailing, other.Health);
    }

    [Fact]
    public void PlayerScan_passes_good_scan_clears_entry_and_next_streak_logs()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        IReadOnlyList<ScanRow> Throw() => throw new InvalidOperationException("gone");
        Scan(feed, Throw, Entered, T0, log);
        Scan(feed, () => [At(-10)], Entered, T0.AddSeconds(5), log);
        Assert.Empty(feed.Health);                                              // the good scan ends the streak
        Scan(feed, Throw, Entered, T0.AddSeconds(10), log);
        Assert.Equal(2, log.Count("scan failed"));                              // a new streak logs again
        // the scan after a failure still sees an entry: rows of the last good scan are kept
        Assert.Single(Scan(feed, () => [At(10)], Entered, T0.AddSeconds(15), log));
    }

    [Fact]
    public void PlayerScan_fails_when_garbage_position_reaches_a_definition()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Scan(feed, () => [At(-10)], Entered, T0, log);
        Assert.Empty(Scan(feed, () => [new ScanRow("p1", float.NaN, 0, true)], Entered, T0.AddSeconds(5), log));
        Assert.Empty(Scan(feed, () => [new ScanRow("p1", 1e9f, 0, true)], Entered, T0.AddSeconds(10), log));
        Assert.Empty(log.Lines);
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void PlayerScan_fails_when_login_during_a_gap_is_an_entry()
    {
        // step 2 code review F1: a scan, a gap with no startable RegionEntered definition, then the player in scope
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Scan(feed, () => [At(-10)], Entered, T0, log);
        Scan(feed, () => [At(-10)], Set(Json.Event("manual")), T0.AddSeconds(5), log);
        Assert.Empty(Scan(feed, () => [At(10)], Entered, T0.AddSeconds(10), log));
        Assert.Empty(Scan(feed, () => [At(-10)], Entered, T0.AddSeconds(15), log));
        Assert.Single(Scan(feed, () => [At(10)], Entered, T0.AddSeconds(20), log));      // a real move still enters
    }

    [Fact]
    public void PlayerScan_empty_reads_no_player_without_a_definition()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        var reads = 0;
        Scan(feed, () => { reads++; return [At(10)]; }, Set(Json.Event("manual")), T0, log);
        Scan(feed, () => { reads++; return [At(10)]; }, Entered, T0, log);                   // a RegionEntered definition: one read
        Assert.Equal(1, reads);
        var unavailable = new PlayerTriggerFeed();
        unavailable.Scan(() => { reads++; return [At(10)]; }, null, Entered, _ => false, T0, log.Add, _ => { });
        Assert.Equal(1, reads);                                                 // regions unavailable: no read
        Assert.Empty(log.Lines);
    }

    [Fact]
    public void PlayerScan_passes_refusal_rows_follow_the_definitions()
    {
        // step 2 Codex F2: the gate's rows of a definition that left the startable set go on the next scan
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Assert.NotNull(feed.Gate.Refused("border", "event border not started by RegionEntered: x", T0));
        Assert.NotNull(feed.Gate.Refused("gone", "event gone not started by RegionEntered: x", T0));
        Scan(feed, () => [], Entered, T0.AddSeconds(5), log);
        Assert.Equal(1, feed.Gate.RefusalRows);
        Scan(feed, () => [], Set(Json.Event("manual")), T0.AddSeconds(10), log);
        Assert.Equal(0, feed.Gate.RefusalRows);
    }

    // ---- kill-read

    [Fact]
    public void KillRead_fails_when_throwing_read_escapes_or_logs_twice()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        KillFacts Throw(bool _) => throw new InvalidOperationException("owner unreadable");
        for (var i = 0; i < 4; i++) Assert.Empty(Die(feed, Throw, Kills, T0.AddSeconds(i), log));
        Assert.Equal(["faction kills: read failed: owner unreadable"], log.Lines);
        Assert.Equal([PlayerTriggerFeed.KillFailing], feed.Health);
        Assert.Equal(0, feed.Kills.Counters);                                   // the skipped deaths counted nothing
    }

    [Fact]
    public void KillRead_passes_good_read_clears_entry()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Die(feed, _ => throw new InvalidOperationException("x"), Kills, T0, log);
        Die(feed, _ => Kill(), Kills, T0.AddSeconds(1), log);
        Assert.Empty(feed.Health);
        Assert.Equal(1, feed.Kills.KillsHeld("reprisal", "p1"));
        Die(feed, _ => Kill(), Kills, T0.AddSeconds(2), log);
        Assert.Equal("reprisal", Assert.Single(Die(feed, _ => Kill(), Kills, T0.AddSeconds(3), log)).Definition.Id);
    }

    [Fact]
    public void KillRead_passes_entry_clears_when_no_definition_reads_kills()
    {
        // step 2 code review F2: the entry ends when no FactionKills definition is startable any more
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Die(feed, _ => throw new InvalidOperationException("x"), Kills, T0, log);
        Assert.Equal([PlayerTriggerFeed.KillFailing], feed.Health);
        Scan(feed, () => [], Kills, T0.AddSeconds(5), log);
        Assert.Equal([PlayerTriggerFeed.KillFailing], feed.Health);           // still startable: still failing
        Scan(feed, () => [], Set(Json.Event("manual")), T0.AddSeconds(10), log);
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void KillRead_fails_when_unavailable_hook_leaves_factionkills_on()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        var reads = 0;
        var hooks = new HookSet(new FailingRegistry(Hook.DeathEvent), log.Add);
        hooks.RegisterAll();
        for (var i = 0; i < 3; i++) Die(feed, _ => { reads++; return Kill(); }, Kills, T0.AddSeconds(i), log, hooks.AllowsTrigger);
        Assert.Equal(0, reads);
        Assert.False(PlayerTriggerFeed.WantsKills(Kills, hooks.AllowsTrigger));
        Assert.True(PlayerTriggerFeed.WantsKills(Kills, AllHooks));
    }

    [Fact]
    public void KillRead_fails_when_garbage_faction_or_position_counts()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        for (var i = 0; i < 5; i++) Die(feed, _ => Kill(faction: "PrefabGuid(-123)"), Kills, T0.AddSeconds(i), log);
        Assert.Equal(0, feed.Kills.Counters);                                   // an unnamed faction counts for nothing
        Assert.Null(PlayerTriggerFeed.Clean(Kill(faction: "PrefabGuid(-123)")).VictimFaction);   // refused where read (review F5)
        Assert.Equal("Faction_Bandits", PlayerTriggerFeed.Clean(Kill()).VictimFaction);
        var scoped = Set(Json.Event("scoped", ScopedReprisal));
        var positions = new List<bool>();
        for (var i = 0; i < 5; i++)
            Die(feed, needs => { positions.Add(needs); return Kill(x: float.NaN, z: 0); }, scoped, T0.AddSeconds(i), log);
        Assert.All(positions, Assert.True);                                     // a scoped definition asks for the position
        Assert.Equal(0, feed.Kills.Counters);                                   // an unusable one counts nowhere
        Assert.Empty(log.Lines);
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void KillRead_empty_no_death_no_read_no_streak()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        var reads = 0;
        Die(feed, _ => { reads++; return Kill(); }, Set(Json.Event("manual")), T0, log);
        Assert.Equal(0, reads);                                                 // no FactionKills definition: no read
        Assert.Empty(feed.Health);
        Assert.Empty(log.Lines);
    }

    // ---- state-write

    static readonly DefinitionSet Ticking = Set(Json.Event("tick", "{ \"type\": \"Interval\", \"minMinutes\": 5, \"maxMinutes\": 6 }"));

    /// <summary>A state.json over <paramref name="fs"/> whose NextInterval holds the first draw for "tick" (T0 + 5 min).</summary>
    static StateStore Drawn(MemoryFileStore fs, LogLines log)
    {
        var state = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        state.Load();
        Assert.Empty(IntervalClock.PollState(state, Ticking, _ => false, T0, new ScriptedRandom(0.0)));   // the production poll
        return state;
    }

    [Fact]
    public void StateWrite_fails_when_failed_flush_drops_the_next()
    {
        var fs = new MemoryFileStore { FailWrites = true };
        var log = new LogLines();
        var state = Drawn(fs, log);
        for (var i = 0; i < 3; i++) { fs.Now += TimeSpan.FromSeconds(2); state.Flush(); }
        Assert.Equal(1, log.Count("state.json write failed"));
        Assert.Equal(T0.AddMinutes(5), state.Document.NextInterval!["tick"]);  // kept in memory
        // the clock keeps using it: no redraw, and it fires when due
        Assert.Empty(IntervalClock.PollState(state, Ticking, _ => false, T0.AddMinutes(4), new ScriptedRandom(0.9)));
        Assert.Equal("tick", Assert.Single(IntervalClock.PollState(state, Ticking, _ => false, T0.AddMinutes(5), new ScriptedRandom(0.9))).Id);
    }

    [Fact]
    public void StateWrite_passes_recovered_flush_writes_the_next()
    {
        var fs = new MemoryFileStore { FailWrites = true };
        var log = new LogLines();
        var state = Drawn(fs, log);
        fs.Now += TimeSpan.FromSeconds(2);
        state.Flush();
        fs.FailWrites = false;
        fs.Now += TimeSpan.FromSeconds(2);
        state.Flush();
        Assert.False(state.Dirty);
        Assert.Contains("\"NextInterval\"", fs.Text(DataFile.State, FileVariant.Main));
        var reloaded = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        reloaded.Load();
        Assert.Equal(T0.AddMinutes(5), reloaded.Document.NextInterval!["tick"]);
        // a later draw (the start seen, then its end) is marked dirty by the same poll and written by the next flush
        IntervalClock.PollState(state, Ticking, _ => false, T0.AddMinutes(5), new ScriptedRandom(0.0));
        IntervalClock.PollState(state, Ticking, _ => true, T0.AddMinutes(6), new ScriptedRandom(0.0));
        IntervalClock.PollState(state, Ticking, _ => false, T0.AddMinutes(7), new ScriptedRandom(0.0));
        Assert.True(state.Dirty);
        fs.Now += TimeSpan.FromSeconds(2);
        state.Flush();
        reloaded.Load();
        Assert.Equal(state.Document.NextInterval!["tick"], reloaded.Document.NextInterval!["tick"]);
        Assert.NotEqual(T0.AddMinutes(5), reloaded.Document.NextInterval!["tick"]);
    }

    [Fact]
    public void StateWrite_empty_no_interval_no_key()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        state.Load();
        Assert.Empty(IntervalClock.PollState(state, Set(Json.Event("manual")), _ => false, T0, new ScriptedRandom()));
        Assert.False(state.Dirty);
        Assert.Null(state.SaveNow());
        Assert.DoesNotContain("NextInterval", fs.Text(DataFile.State, FileVariant.Main));
    }

    sealed class FailingRegistry(Hook failing) : IHookRegistry
    {
        public void Register(Hook hook)
        {
            if (hook == failing) throw new InvalidOperationException("not patched");
        }
    }
}
