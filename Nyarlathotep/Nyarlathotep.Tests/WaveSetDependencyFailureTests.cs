using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D13 and D18: the dependency failures of the wave sets, one control per category of
/// tools/preflight-checks.json › dependencySuites.wave-sets (kill-read, cleared-read). A failing read skips its death's
/// credit or holds its wave, logs once per streak, holds its health entry until a read succeeds and never throws, and a
/// throw never skips SpawnTracker.Died, a later death or the V Blood path.</summary>
public class WaveSetDependencyFailureTests
{
    static readonly DateTime T0 = new(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc);

    static readonly Scorer Chaos = new("1", "Chaos", false);

    static string WaveSet(string id, bool scoreboard) =>
        Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", " +
            "\"count\": 2 } ] }, { \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ], \"whenCleared\": true } ], \"radius\": 10, " +
            "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"scoreboard\": " + (scoreboard ? "true" : "false") + " }");

    static (EventEngine Engine, EventCatalog Catalog) Started(params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        foreach (var d in catalog.Current.All)
            Assert.Null(engine.Start(d.Id, "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
        return (engine, catalog);
    }

    /// <summary>One death as the patch sees it: who died and whether its reads throw.</summary>
    sealed record Death(string Name, bool ThrowSides = false, bool ThrowPlayers = false);

    /// <summary>The patch's order (Patches/DeathEventPatch) over <paramref name="deaths"/>, recording each step.</summary>
    static List<string> Run(IEnumerable<Death> deaths, ScoreFeed feed, EventEngine engine, Scoreboard board, LogLines log)
    {
        var calls = new List<string>();
        var unit = new OurUnit("ws", 1, T0);
        DeathPass.Run(deaths, d => feed.Before(() => d.ThrowSides ? throw new InvalidOperationException("ledger gone") : new ScoreSides(unit, null), log.Add),
            d => calls.Add("died " + d.Name),
            (d, _) => calls.Add("vblood " + d.Name),
            (d, sides) => feed.After(sides, _ => d.ThrowPlayers ? throw new InvalidOperationException("user gone") : new ScorePlayers(Chaos, null, false),
                engine.Find, board, false, log.Add));
        return calls;
    }

    // ---- kill-read (D13)

    [Fact]
    public void KillRead_fails_when_a_throwing_read_skips_died_or_a_later_death()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        var feed = new ScoreFeed();
        var board = new Scoreboard();
        var log = new LogLines();
        var calls = Run([new Death("a", ThrowSides: true), new Death("b", ThrowPlayers: true), new Death("c")], feed, engine, board, log);
        Assert.Equal(["died a", "vblood a", "died b", "vblood b", "died c", "vblood c"], calls);   // every death, every step
        Assert.Equal(["scoreboard: death skipped: ledger gone"], log.Lines);                        // once per streak
        Assert.Equal(1, board.Summary("ws").Kills);                                                  // c, the good read
        Assert.Empty(feed.Health);                                                                   // c ended the streak
    }

    [Fact]
    public void KillRead_fails_when_the_entry_outlives_a_good_read()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        var feed = new ScoreFeed();
        var log = new LogLines();
        Run([new Death("a", ThrowPlayers: true), new Death("b", ThrowSides: true)], feed, engine, new Scoreboard(), log);
        Assert.Equal([ScoreFeed.ReadFailing], feed.Health);
        Assert.Equal(["scoreboard: death skipped: user gone"], log.Lines);
        Run([new Death("c")], feed, engine, new Scoreboard(), log);
        Assert.Empty(feed.Health);
        Run([new Death("d", ThrowSides: true)], feed, engine, new Scoreboard(), log);
        Assert.Equal(2, log.Count("death skipped"));                                                // a new streak logs again
        feed.Idle();                                                                                 // no counting instance left
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void KillRead_fails_when_a_read_happens_with_no_scoreboard_event()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: false));
        Assert.False(Scoreboard.Wants(engine.Active));                                               // the patch reads nothing
        var players = 0;
        var feed = new ScoreFeed();
        var board = new Scoreboard();
        feed.After(new ScoreSides(new OurUnit("ws", 1, T0), null), _ => { players++; return new ScorePlayers(Chaos, null, false); },
            engine.Find, board, false, _ => { });
        Assert.Equal(0, players);                                                                    // not relevant: no player read
        feed.After(new ScoreSides(null, null), _ => { players++; return default; }, engine.Find, board, false, _ => { });
        Assert.Equal(0, players);                                                                    // a native death
        Assert.Equal(0, board.Events);
    }

    [Fact]
    public void KillRead_fails_when_an_edit_mid_run_stops_the_counting()
    {
        var (engine, catalog) = Started(WaveSet("ws", scoreboard: true));
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(WaveSet("ws", scoreboard: false)), FakeUnits.Default()), FileStamp.Of(T0, [2])));
        Assert.True(Scoreboard.Wants(engine.Active));                                                // the running instance's own definition
        var board = new Scoreboard();
        new ScoreFeed().After(new ScoreSides(new OurUnit("ws", 1, T0), null), _ => new ScorePlayers(Chaos, null, false), engine.Find, board,
            false, _ => { });
        Assert.Equal(1, board.Summary("ws").Kills);
    }

    [Fact]
    public void KillRead_passes_a_kill_and_a_death_credited()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        var feed = new ScoreFeed();
        var board = new Scoreboard();
        var unit = new OurUnit("ws", 1, T0);
        feed.After(feed.Before(() => new ScoreSides(unit, null), _ => { }), _ => new ScorePlayers(Chaos, null, false), engine.Find, board, false, _ => { });
        feed.After(feed.Before(() => new ScoreSides(null, null, unit), _ => { }), _ => new ScorePlayers(null, Chaos, false), engine.Find, board, false, _ => { });
        Assert.Equal((1, 1, 1), (board.Summary("ws").Players, board.Summary("ws").Kills, board.Summary("ws").Deaths));
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void KillRead_empty_no_death()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        var feed = new ScoreFeed();
        var log = new LogLines();
        Assert.Empty(Run([], feed, engine, new Scoreboard(), log));
        Assert.Empty(log.Lines);
        Assert.Empty(feed.Health);
    }

    // ---- cleared-read (D18)

    [Fact]
    public void ClearedRead_fails_when_a_throwing_read_starts_the_wave_or_logs_twice()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0, 2);
        var guard = new ClearedRead();
        var log = new LogLines();
        var cleared = guard.Guard((_, _) => throw new InvalidOperationException("ledger gone"), log.Add);
        for (var i = 1; i <= 3; i++) Assert.Null(engine.NextWave("ws", T0.AddSeconds(i), cleared));   // wave 2 held
        Assert.Empty(engine.Complete(T0.AddSeconds(4), 60, cleared));                                  // and the early end
        Assert.Equal(["event ws: cleared read failed: ledger gone"], log.Lines);
        Assert.Equal([ClearedRead.Failing], guard.Health);
    }

    [Fact]
    public void ClearedRead_passes_a_good_read_clears_the_entry()
    {
        var (engine, _) = Started(WaveSet("ws", scoreboard: true));
        engine.WaveDecided("ws", WaveOutcome.Spawn, T0, 2);
        var guard = new ClearedRead();
        var log = new LogLines();
        var fail = true;
        var cleared = guard.Guard((_, _) => fail ? throw new InvalidOperationException("x") : true, log.Add);
        Assert.Null(engine.NextWave("ws", T0.AddSeconds(1), cleared));
        fail = false;
        Assert.Equal(2, engine.NextWave("ws", T0.AddSeconds(2), cleared)!.Wave);
        Assert.Empty(guard.Health);
        fail = true;
        Assert.False(cleared("ws", 1));
        Assert.Equal(2, log.Count("cleared read failed"));                                             // the second streak
        Assert.Equal([ClearedRead.Failing], guard.Health);
    }

    [Fact]
    public void ClearedRead_empty_no_wave_waits_for_a_clear()
    {
        var guard = new ClearedRead();
        var log = new LogLines();
        var reads = 0;
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        Assert.Null(engine.Start("raid", "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
        Assert.Equal(1, engine.NextWave("raid", T0, guard.Guard((_, _) => { reads++; return false; }, log.Add))!.Wave);
        Assert.Equal(0, reads);                                                                        // the units form never reads it
        Assert.Empty(guard.Health);
    }
}
