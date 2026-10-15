using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D6-D11 and D16: the two tries and the removal, a removed unit's wave, no kill credit, the stall
/// backstop, the switches and caps, the end paths and the one line per change.</summary>
public partial class UnitWatchTests
{
    /// <summary>A uw unit 20 m from its anchor (500, 500), 150 m under the map; <see cref="Keep"/> sinks it again after each
    /// check, as the in-game strand repeated after each recovery.</summary>
    static (WatchRig R, ActiveEvent A, long Key) Stranded(EventDefinition? d = null)
    {
        var (r, a) = Watch.Rig(d);
        var key = r.W.Add("uw", Spawned, 500, 500);
        r.W.MoveTo(key, 520, 500);
        r.W.Sink(key, -150f);
        return (r, a, key);
    }

    static Action Keep(WatchRig r, long key) => () =>
    {
        if (r.W.At.ContainsKey(key)) r.W.Sink(key, -150f);
    };

    static readonly string[] TwoTries =
    [
        "unit-watch uw stuck: CHAR_Spider_Forest wave 1 (off-ground)",
        "unit-watch uw recovered: CHAR_Spider_Forest try 1/2, 20 m (anchor)",
        "unit-watch uw try failed: CHAR_Spider_Forest try 1/2 (still stuck)",
        "unit-watch uw recovered: CHAR_Spider_Forest try 2/2, 0 m (anchor)",
        "unit-watch uw try failed: CHAR_Spider_Forest try 2/2 (still stuck)",
        "unit-watch uw removed: CHAR_Spider_Forest wave 1 after 2 tries, no kill credit",
    ];

    // ---- D6 Tries

    [Fact]
    public void Tries_passes_two_tries_then_removal()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        Assert.Equal(TwoTries, r.Log);
        Assert.Equal([key], r.W.Despawns);
        Assert.Equal(1, r.W.Ledger.PendingDespawns);
        Assert.Null(r.P.MarkOf(key));
    }

    [Fact]
    public void Tries_fails_when_a_third_try_is_made()
    {
        var (r, _, key) = Stranded();
        r.For(600, Keep(r, key));
        Assert.Equal(2, r.W.Moves.Count);
        Assert.DoesNotContain(r.Log, l => l.Contains("try 3", StringComparison.Ordinal));
        Assert.Single(r.W.Despawns);
    }

    [Fact]
    public void Tries_fails_when_one_failed_try_removes()
    {
        var (r, _, key) = Stranded();
        r.For(35, Keep(r, key));
        Assert.Equal(1, r.P.TriesOf(key));
        Assert.Empty(r.W.Despawns);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
    }

    [Fact]
    public void Tries_fails_when_a_failed_landing_or_seen_wait_is_not_counted()
    {
        var (r, _, key) = Stranded();
        r.W.Walk = (_, _, _) => false;
        r.For(20);
        Assert.Equal(1, r.P.TriesOf(key));
        Assert.Equal(1, r.Lines("try failed: CHAR_Spider_Forest try 1/2 (no landing)"));
        r.For(5);
        Assert.Equal(1, r.Lines("try failed: CHAR_Spider_Forest try 2/2 (no landing)"));
        Assert.Equal([key], r.W.Despawns);

        var (s, _, seen) = Marked();
        s.W.Player(580, 500);                                     // 80 m: no unseen moment comes
        s.For(85);
        Assert.Equal(0, s.P.TriesOf(seen));
        s.For(5);                                                  // StuckSeconds after the mark
        Assert.Equal(1, s.P.TriesOf(seen));
        Assert.Equal(1, s.Lines("try failed: CHAR_Spider_Forest try 1/2 (seen)"));
        Assert.Empty(s.W.Moves);
    }

    [Fact]
    public void Tries_fails_when_a_unit_that_fought_keeps_its_count()
    {
        var (r, _, key) = Marked();
        r.For(5);
        Assert.Single(r.W.Moves);
        Assert.Equal(1, r.P.TriesOf(key));
        r.P.Fights.Report(key, true, r.Now.AddSeconds(2), r.W.Ledger.IsTracked);
        r.For(5);
        Assert.Equal(0, r.P.TriesOf(key));
        Assert.Null(r.P.MarkOf(key));
        Assert.Equal(1, r.Lines("unit-watch uw cleared: CHAR_Spider_Forest (fought)"));
    }

    [Fact]
    public void Tries_fails_when_a_seen_marked_unit_that_fought_is_removed()
    {
        var (r, _, key) = Stranded();
        r.W.Player(600, 500);                                      // 80 m away: seen, so no move yet
        r.For(30, Keep(r, key));
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
        Assert.Empty(r.W.Moves);
        r.P.Fights.Report(key, true, r.Now.AddSeconds(2), r.W.Ledger.IsTracked);
        r.For(5, Keep(r, key));
        Assert.Null(r.P.MarkOf(key));
        Assert.Equal(1, r.Lines("unit-watch uw cleared: CHAR_Spider_Forest (fought)"));
        Assert.Empty(r.W.Despawns);
    }

    [Fact]
    public void Tries_fails_when_a_throwing_landing_halts_the_watch()
    {
        var (r, _, key) = Stranded();
        var other = r.W.Add("uw", Spawned, 900, 900);
        r.W.MoveTo(other, 920, 900);
        r.W.Sink(other, -150f);
        r.W.Walk = (_, _, _) => throw new InvalidOperationException("walk data missing");
        r.For(200, () => { Keep(r, key)(); if (r.W.At.ContainsKey(other)) r.W.Sink(other, -150f); });
        Assert.Contains(key, r.W.Despawns);                        // each throwing try counts, so both still leave
        Assert.Contains(other, r.W.Despawns);
        Assert.True(r.Lines("(no landing)") >= 4);
    }

    [Fact]
    public void Tries_fails_when_a_stall_mark_clears_by_walking()
    {
        var (r, _, key) = Marked();                                // marked by hand: cleared only by a fight
        r.For(5);
        var x = 500f;
        r.For(90, () => r.W.MoveTo(key, x += 20, 500));            // it walks, nobody near, no fight
        Assert.Equal(StuckReason.Admin, r.P.MarkOf(key));
        Assert.Equal(1, r.Lines("try failed: CHAR_Spider_Forest try 1/2 (still stuck)"));
        Assert.Equal(0, r.Lines("cleared:"));
    }

    [Fact]
    public void Tries_fails_when_a_removal_skips_the_despawn_budget()
    {
        var (r, _) = Watch.Rig();
        r.W.Walk = (_, _, _) => false;
        var keys = Enumerable.Range(0, 7).Select(i => r.W.Add("uw", Spawned, 300 + 30 * i, 500, -150f)).ToList();
        r.For(90);
        Assert.Equal(7, r.W.Despawns.Count);
        Assert.Equal(7, r.W.Ledger.Tracked);                      // queued, not destroyed: the despawn budget takes them
        Assert.Equal(7, r.W.Ledger.PendingDespawns);
        Assert.Equal(5, r.W.Drain().Count);                        // MaxDespawnsPerTick
        Assert.Equal(2, r.W.Drain().Count);
        Assert.All(keys, k => Assert.False(r.W.Ledger.IsTracked(k)));
    }

    [Fact]
    public void Tries_fails_when_a_removal_waits_past_stuck_seconds()
    {
        var (r, _, key) = Stranded();
        r.W.Walk = (_, _, _) => false;
        r.For(20);                                                 // marked at 15 s, try 1 found no landing at 20 s
        r.W.Player(600, 500);                                      // 80 m from the unit from now on
        r.For(85);                                                 // 105 s
        Assert.Equal(1, r.P.TriesOf(key));
        r.For(5);                                                  // 110 s: try 2 fails unseen, the removal waits
        Assert.Equal(2, r.P.TriesOf(key));
        r.For(85);                                                 // 195 s
        Assert.Empty(r.W.Despawns);
        r.For(5);                                                  // StuckSeconds after the removal began: removed in view
        Assert.Equal([key], r.W.Despawns);
        Assert.Equal("unit-watch uw removed: CHAR_Spider_Forest wave 1 after 2 tries, no kill credit (in view)", r.Log[^1]);
    }

    [Fact]
    public void Tries_fails_when_a_try_queues_a_spawn()
    {
        var (r, _, key) = Stranded();
        var most = 0;
        r.For(300, () =>
        {
            Keep(r, key)();
            Assert.Equal(0, r.W.Ledger.PendingSpawns);
            most = Math.Max(most, r.W.Ledger.Occupied);
        });
        Assert.Equal(1, most);
        r.W.Drain();
        Assert.Equal(0, r.W.Ledger.Occupied);
    }

    [Fact]
    public void Tries_empty_a_cleared_unit_starts_over()
    {
        var (r, _, key) = Stranded();
        r.For(15, Keep(r, key));
        r.For(5);                                                  // moved at 20 s, then it stays on the ground
        Assert.Equal(1, r.P.TriesOf(key));
        r.For(90);
        Assert.Equal(1, r.Lines("unit-watch uw cleared: CHAR_Spider_Forest (moving)"));
        Assert.Equal(0, r.P.TriesOf(key));
        Assert.Null(r.P.MarkOf(key));
        r.W.Sink(key, -150f);
        r.For(20, Keep(r, key));
        Assert.Equal(2, r.Lines("recovered: CHAR_Spider_Forest try 1/2"));
    }

    // ---- D7 Gone

    /// <summary>A waveList instance whose wave 1 was decided with two units: one the players kill, one stranded with no
    /// landing (removed at 25 s).</summary>
    static (WatchRig R, ActiveEvent A, long Killed, long Lost) Leftovers(IReadOnlyList<WaveSpec> list)
    {
        var (r, a) = Watch.Rig(Watch.Waves(list: list));
        a.Record(T0, fought: true);
        a.WavesSpawned = 1;
        var killed = r.W.Add("uw", Spawned, 300, 500);
        var lost = r.W.Add("uw", Spawned, 500, 500, -150f);
        r.W.Walk = (_, _, _) => false;
        r.W.Ledger.Forget(killed);
        r.W.At.Remove(killed);
        r.P.Died(killed, "uw", 1, T0.AddSeconds(2));
        return (r, a, killed, lost);
    }

    static Func<int, bool> ClearedOf(WatchRig r, ActiveEvent a) => k => r.W.Ledger.WaveCleared(a.Id, k, a.Instance.StartedUtc);

    [Fact]
    public void Gone_fails_when_the_next_wave_waits_for_a_removed_unit()
    {
        var (r, a, _, lost) = Leftovers(Watch.Two());
        r.For(25);
        Assert.Equal([lost], r.W.Despawns);
        Assert.Null(WaveSchedule.Due(a, r.Now, ClearedOf(r, a)));    // queued, still tracked until the despawn batch
        r.W.Drain();
        Assert.Equal(2, WaveSchedule.Due(a, r.Now, ClearedOf(r, a)));
    }

    [Fact]
    public void Gone_fails_when_the_last_wave_is_not_defeated()
    {
        var (r, a, _, _) = Leftovers([new([new UnitEntry(WatchWorld.Prefab, 2)])]);
        r.For(25);
        Assert.False(WaveSchedule.Defeated(a, ClearedOf(r, a)));
        r.W.Drain();
        Assert.True(WaveSchedule.Defeated(a, ClearedOf(r, a)));
    }

    /// <summary>apo with horde 1 risen and spawned (its roamers too), every unit placed on the ground.</summary>
    static (WatchRig R, ApoWorld Apo, ApocalypseRun Run, List<TrackedUnit> Units) Risen()
    {
        var apo = new ApoWorld();
        var r = new WatchRig(new WatchWorld(apo.Ledger));
        var a = Apo.Active(Apo.Def(), T0);
        r.Active.Add(a);
        var run = Apo.Run(a);
        var at = T0.AddMinutes(1).AddSeconds(5);
        Assert.NotNull(run.Rise(apo, at, out _));
        var units = apo.SpawnAll(run, at);
        var i = 0;
        foreach (var u in units) r.W.Put(u.Key, 200 + 20 * i++, 500);
        r.W.Risen = run.Risen;
        r.W.Walk = (_, _, _) => false;
        r.Now = at;
        return (r, apo, run, units);
    }

    [Fact]
    public void Gone_fails_when_a_removed_horde_unit_keeps_its_horde()
    {
        var (r, apo, run, units) = Risen();
        var horde = units.Where(u => u.Wave == Apocalypse.HordeWave(1)).Select(u => u.Key).ToList();
        foreach (var k in horde.Skip(1))
        {
            apo.Kill(run, k);
            r.W.At.Remove(k);
        }
        r.W.Sink(horde[0], -150f);
        Assert.Contains(run.Record(apo, [], r.Now).Hordes, h => h.Number == 1 && h.Living == 1);
        r.For(30);
        Assert.Equal([horde[0]], r.W.Despawns);
        r.W.Drain();
        Assert.True(ApocalypseOutcome.HordeCleared(apo.Ledger, "apo", 1, T0));
        Assert.DoesNotContain(run.Record(apo, [], r.Now).Hordes, h => h.Number == 1);
    }

    [Fact]
    public void Gone_fails_when_the_record_counts_a_removed_unit()
    {
        var (r, apo, run, units) = Risen();
        var roamer = units.First(u => Apocalypse.HotspotOfWave(u.Wave) is not null).Key;
        var before = run.RoamersPerHotspot(apo).Sum();
        var record = run.Record(apo, [], r.Now);
        r.W.Sink(roamer, -150f);
        r.For(30);
        r.W.Drain();
        Assert.Equal(before - 1, run.RoamersPerHotspot(apo).Sum());
        Assert.Equal(record.Roamers.Sum() - 1, run.Record(apo, [], r.Now).Roamers.Sum());
        // nothing else changed: every other unit is tracked and where it stood
        Assert.All(units.Where(u => u.Key != roamer), u => Assert.True(apo.Ledger.IsTracked(u.Key)));
        Assert.Empty(r.W.Moves);
    }

    [Fact]
    public void Gone_passes_removal_clears_the_wave()
    {
        var (r, a, _, lost) = Leftovers(Watch.Two());
        r.For(25);
        r.W.Drain();
        Assert.True(r.W.Ledger.WaveCleared("uw", 1, T0));
        Assert.False(r.W.Ledger.IsTracked(lost));
        Assert.Equal(2, WaveSchedule.Due(a, r.Now, ClearedOf(r, a)));
    }

    [Fact]
    public void Gone_empty_no_removal_wave_waits()
    {
        var (r, a, _, lost) = Leftovers(Watch.Two());
        r.W.Sink(lost, 0f);                                        // on the ground, nobody near: never stuck
        r.For(300);
        Assert.Empty(r.W.Despawns);
        Assert.Null(WaveSchedule.Due(a, r.Now, ClearedOf(r, a)));
    }

    // ---- D8 NoCredit

    static readonly Scorer Killer = new("76561190000000101", "Killer", false);

    [Fact]
    public void NoCredit_fails_when_a_removal_credits_a_kill()
    {
        var (r, a, key) = Stranded();
        var board = new Scoreboard();
        var feed = new ScoreFeed();
        r.For(55, Keep(r, key));
        Assert.Equal([key], r.W.Despawns);
        Assert.Equal(TwoTries[^1], r.Log[^1]);
        Assert.Equal([key], r.W.Drain());                          // a despawn: no death event, so the death pass never runs
        // A stray death event for the removed unit, run through the death pass as SpawnTracker runs it, credits nothing:
        // the removal took the unit out of the ledger, so the pass finds no event of ours to score.
        var sides = feed.Before(() => new ScoreSides(r.W.Ledger.EventOf(key), null), _ => { });
        r.W.Ledger.Forget(key);
        feed.After(sides, _ => new ScorePlayers(Killer, null, false), id => id == a.Id ? a : null, board, true, _ => { });
        feed.Idle();
        var summary = board.Summary(a.Id);
        Assert.Equal(0, summary.Kills);
        Assert.Equal(0, summary.Players);
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void NoCredit_fails_when_a_killed_marked_unit_loses_its_credit()
    {
        var (r, a, key) = Stranded();
        r.W.Player(600, 500);
        r.For(30, Keep(r, key));                                   // marked, waiting for an unseen moment
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
        var board = new Scoreboard();
        var feed = new ScoreFeed();
        var sides = feed.Before(() => new ScoreSides(r.W.Ledger.EventOf(key), null), _ => { });
        r.P.Died(key, a.Id, 1, r.Now);                             // the death pass: before SpawnTracker.Died
        r.W.Ledger.Forget(key);
        r.W.At.Remove(key);
        feed.After(sides, _ => new ScorePlayers(Killer, null, false), id => id == a.Id ? a : null, board, true, _ => { });
        Assert.Equal(1, board.Summary(a.Id).Kills);
        r.For(300);
        Assert.Empty(r.W.Despawns);
        Assert.Equal(0, r.Lines("removed:"));
    }

    [Fact]
    public void NoCredit_passes_removal_says_no_kill_credit()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        Assert.EndsWith("after 2 tries, no kill credit", r.Log[^1]);
    }

    [Fact]
    public void NoCredit_empty_no_removal_no_line()
    {
        var (r, _) = Standing(null);
        r.For(300);
        Assert.Equal(0, r.Lines("no kill credit"));
    }

    // ---- D9 Stall

    /// <summary>A waveList instance with wave 1 decided and <paramref name="n"/> of its units left, nobody near.</summary>
    static (WatchRig R, ActiveEvent A, List<long> Keys) Quiet(EventDefinition? d = null, int n = 4, bool decided = true)
    {
        var (r, a) = Watch.Rig(d ?? Watch.Waves(list: Watch.Two()));
        if (decided)
        {
            a.Record(T0, fought: true);
            a.WavesSpawned = 1;
        }
        var keys = Enumerable.Range(0, n).Select(i => r.W.Add(a.Id, Spawned, 300 + 30 * i, 500)).ToList();
        return (r, a, keys);
    }

    const string StallLine = "unit-watch uw stall: wave 1 quiet 10 min, 4 leftovers marked";

    [Fact]
    public void Stall_fails_when_it_fires_before_stall_minutes()
    {
        var (r, _, keys) = Quiet();
        r.For(600);                                                // the clock starts at the first check (5 s)
        Assert.Empty(r.Log);
        r.For(5);
        Assert.Equal(StallLine, r.Log[0]);
        Assert.Equal(4, r.Lines("(stall)"));
        Assert.All(keys, k => Assert.Equal(StuckReason.Stall, r.P.MarkOf(k)));
    }

    [Fact]
    public void Stall_fails_when_it_fires_after_a_death_or_a_fight()
    {
        var (r, a, keys) = Quiet();
        r.For(300);
        r.W.Ledger.Forget(keys[0]);
        r.W.At.Remove(keys[0]);
        r.P.Died(keys[0], a.Id, 1, r.Now);
        r.For(595);
        Assert.Empty(r.Log);
        r.For(5);
        Assert.Equal("unit-watch uw stall: wave 1 quiet 10 min, 3 leftovers marked", r.Log[0]);

        var (f, _, fought) = Quiet();
        f.For(300);
        f.P.Fights.Report(fought[1], true, f.Now, f.W.Ledger.IsTracked);
        f.For(600);
        Assert.Empty(f.Log);
        f.For(5);
        Assert.Equal(StallLine, f.Log[0]);
    }

    [Fact]
    public void Stall_fails_when_an_after_seconds_growth_or_units_wave_is_watched()
    {
        foreach (var d in new[]
                 {
                     Watch.Waves(list: Watch.Two(after: 60)), Watch.Waves(), Watch.Waves(list: Watch.Two(), growth: new Growth(1, 0, true)),
                     Watch.Waves(list: Watch.Two(), arena: new Arena(40)),
                 })
        {
            var (r, _, _) = Quiet(d);
            r.For(1500);
            Assert.Equal(0, r.Lines("stall:"));
        }
        // a wave with a timer after it is not watched, the last one is
        var (q, a, _) = Quiet(Watch.Waves(list: Watch.Two(after: 60)));
        a.Record(T0.AddSeconds(60), fought: true);
        a.WavesSpawned = 2;
        var last = q.W.Add(a.Id, Spawned, 900, 500, wave: 2);
        q.For(605);
        Assert.Equal(["unit-watch uw stall: wave 2 quiet 10 min, 1 leftovers marked", "unit-watch uw stuck: CHAR_Spider_Forest wave 2 (stall)"], q.Log);
        Assert.Equal(StuckReason.Stall, q.P.MarkOf(last));
    }

    [Fact]
    public void Stall_fails_when_an_apocalypse_fires_before_its_last_horde()
    {
        var r = new WatchRig();
        r.Active.Add(Apo.Active(Apo.Def(), T0));
        r.W.Add("apo", Spawned, 300, 500, wave: Apocalypse.HordeWave(1));
        r.W.Add("apo", Spawned, 330, 500, wave: Apocalypse.BossWave(1, 1));
        r.W.Risen = 2;                                             // of 3
        r.For(1500);
        Assert.Empty(r.Log);
        r.W.Risen = 3;
        r.For(605);
        Assert.Equal("unit-watch apo stall: wave 1 quiet 10 min, 2 leftovers marked", r.Log[0]);
    }

    [Fact]
    public void Stall_fails_when_zero_minutes_fires()
    {
        var (r, _, _) = Quiet();
        r.S = WatchSettings.Default with { StallMinutes = 0 };
        r.For(3600);
        Assert.Empty(r.Log);
    }

    [Fact]
    public void Stall_passes_quiet_wave_marked_at_ten_minutes()
    {
        var (r, _, _) = Quiet();
        r.For(610);
        Assert.Equal(StallLine, r.Log[0]);
        Assert.Equal(4, r.W.Moves.Count);                          // each marked leftover tried at the next check
    }

    [Fact]
    public void Stall_empty_no_decided_wave_no_clock()
    {
        var (r, _, _) = Quiet(decided: false);
        r.For(1500);
        Assert.Empty(r.Log);
    }

    // ---- D10 Controls

    [Fact]
    public void Controls_fails_when_watch_off_marks_moves_or_removes()
    {
        var (r, a, key) = Stranded();
        r.W.Add("uw", Spawned, 700, 500);
        r.W.Player(710, 500);
        a.Record(T0, fought: true);
        r.S = WatchSettings.Default with { Enabled = false };
        for (var t = 0; t < 300; t++)
        {
            r.Now = r.Now.AddSeconds(5);
            r.P.Tick(r.Now, r.Active, r.W, r.S, r.Log.Add, r.Errors.Add);
            Keep(r, key)();
        }
        r.Check();
        Assert.Empty(r.Log);
        Assert.Empty(r.W.Moves);
        Assert.Empty(r.W.Despawns);
        Assert.Equal(0, r.P.AllRecords);
        var o = r.Unstick("uw");
        Assert.False(o.Ok);
        Assert.Equal((RefusalCode.Invalid, Reasons.WatchOff, UnitWatch.Off), (o.Code!.Value, o.Reason, o.Human));
        Assert.Equal(0, r.P.AllRecords);
    }

    [Fact]
    public void Controls_fails_when_a_purge_or_pillar_off_leaves_a_row()
    {
        var (r, a, key) = Stranded(Watch.Waves(list: Watch.Two()));
        a.Record(T0, fought: true);
        r.P.Fights.Report(key, true, T0.AddSeconds(1), r.W.Ledger.IsTracked);
        r.For(125, Keep(r, key));                                  // marked at 105 s (the fight at 1 s), moved at 110 s, failed at 125 s
        Assert.Equal(1, r.P.TriesOf(key));
        Assert.True(r.P.AllRecords > 0);
        r.P.Clear();                                               // `.nyar purge confirm` and `.nyar pillar spawns off`
        r.Active.Clear();
        Assert.Equal(0, r.P.AllRecords);
        Assert.Equal(0, r.P.TriesOf(key));
        var lines = r.Log.Count;
        r.For(60, Keep(r, key));
        Assert.Equal(lines, r.Log.Count);
        Assert.Equal(0, r.P.AllRecords);
    }

    [Fact]
    public void Controls_fails_when_a_check_spends_a_121st_walk_check()
    {
        var (r, _) = Watch.Rig();
        for (var i = 0; i < 200; i++) r.W.Add("uw", Spawned, 100 + 5 * i, 500);
        r.For(5);
        Assert.Equal(120, r.W.GroundCalls);                           // S-11: 120 walk checks a check
        var (q, _) = Watch.Rig();
        q.W.Budget = new WalkBudget(10);                           // what the other movers left this tick
        for (var i = 0; i < 50; i++) q.W.Add("uw", Spawned, 100 + 5 * i, 500);
        q.For(5);
        Assert.Equal(5, q.W.GroundCalls);
        Assert.Equal(0, q.W.Budget.Left);
        var (m, _) = Watch.Rig();
        m.W.Walk = (_, _, _) => false;
        for (var i = 0; i < 30; i++) m.W.Add("uw", Spawned, 100 + 30 * i, 500, -150f);
        m.For(20);
        var before = m.W.GroundCalls + m.W.WalkCalls;
        m.For(5);
        Assert.InRange(m.W.GroundCalls + m.W.WalkCalls - before, 1, 120);
    }

    /// <summary>Codex pre-release M1: one off-ground read may examine many height levels; every probe is debited from the
    /// watch's 120-check ceiling (D10), only levels at or below the unit's reach are examined, its own level first (D3).</summary>
    [Fact]
    public void Controls_fails_when_a_ground_read_probes_past_the_walk_cap()
    {
        var (r, _) = Watch.Rig();
        r.W.GroundAt = (_, _) => new GroundReading(null);          // no grounded level: each read scans every level in reach
        for (var i = 0; i < 50; i++) r.W.Add("uw", Spawned, 100 + 5 * i, 500);
        r.For(5);
        Assert.Equal(UnitWatch.WalkCap, r.W.GroundCalls);          // 120 probes, not 50 units x 16 levels
        var (one, _) = Watch.Rig();
        one.W.Add("uw", Spawned, 100, 500);
        one.For(5);
        Assert.Equal(1, one.W.GroundCalls);                        // grounded at its own level: one probe
        var (deep, _) = Watch.Rig();
        deep.W.Add("uw", Spawned, 100, 500, -15f);
        deep.For(5);
        Assert.Equal(8, deep.W.GroundCalls);                       // levels 7..0 only: none above its reach (-5 m)
    }

    [Fact]
    public void Controls_fails_when_a_removal_skips_the_despawn_budget()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        Assert.Equal([key], r.W.Despawns);
        Assert.True(r.W.Ledger.IsTracked(key));                    // only the staged despawn destroys it
        Assert.Equal(1, r.W.Ledger.Occupied);                      // its MaxTrackedUnits slot is held until then
        r.W.Drain();
        Assert.Equal(0, r.W.Ledger.Occupied);
    }

    [Fact]
    public void Controls_fails_when_an_untracked_unit_is_touched()
    {
        var (r, _) = Watch.Rig();
        r.W.Put(9_001, 500, 500, -150f);                           // in the world, not in the ledger
        r.W.Player(510, 500);
        r.For(300);
        Assert.Empty(r.Log);
        Assert.Empty(r.W.Moves);
        Assert.Equal(0, r.P.Rows("uw"));
    }

    [Fact]
    public void Controls_passes_watch_within_its_caps()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        Assert.Equal(TwoTries, r.Log);
        Assert.True(r.W.GroundCalls + r.W.WalkCalls <= 11 * UnitWatch.WalkCap);
    }

    [Fact]
    public void Controls_empty_no_event_no_check()
    {
        var (r, _, _) = Stranded();
        r.Active.Clear();
        r.For(300);
        Assert.Empty(r.Log);
        Assert.Equal(0, r.W.GroundCalls);
        Assert.Equal(0, r.P.AllRecords);
    }

    // ---- D11 EndPaths

    [Fact]
    public void EndPaths_fails_when_a_record_outlives_the_end()
    {
        var (r, a, key) = Stranded(Watch.Waves(list: Watch.Two()));
        a.Record(T0, fought: true);
        var reader = r.W.Add("uw", Spawned, 800, 500);
        r.W.ThrowRead.Add(reader);
        r.P.Fights.Report(key, true, T0.AddSeconds(1), _ => true);
        r.For(125, Keep(r, key));
        Assert.True(r.P.AllRecords > 0);
        Assert.Equal([UnitWatchPhase.ReadFailing], r.P.Health);
        r.P.EventEnded("uw");                                      // stop, expiry, defeat, fault: every end path
        Assert.Equal(0, r.P.AllRecords);
        Assert.Empty(r.P.Health);
        Assert.Equal(default(WatchCounts), r.P.Counts(a));
    }

    [Fact]
    public void EndPaths_fails_when_a_line_follows_the_end()
    {
        var (r, _, key) = Stranded();
        r.W.Player(600, 500);
        r.For(30, Keep(r, key));                                   // marked and waiting
        r.Active.Clear();
        r.P.EventEnded("uw");
        var lines = r.Log.Count;
        r.W.People.Clear();
        r.For(600, Keep(r, key));
        Assert.Equal(lines, r.Log.Count);
        Assert.Empty(r.W.Moves);
        Assert.Empty(r.W.Despawns);
    }

    [Fact]
    public void EndPaths_fails_when_a_resumed_unit_keeps_its_row()
    {
        var (r, a, key) = Stranded();
        r.For(35, Keep(r, key));
        Assert.Equal(1, r.P.TriesOf(key));
        r.Active.Clear();
        r.P.EventEnded("uw");
        var again = Watch.Run(a.Definition, r.Now);                // started again: the old unit is an earlier instance's
        r.Active.Add(again);
        var fresh = r.W.Add("uw", r.Now.AddSeconds(1), 300, 500);
        r.For(60, Keep(r, key));
        Assert.Equal(0, r.P.TriesOf(key));
        Assert.Null(r.P.MarkOf(key));
        Assert.Equal(0, r.P.TriesOf(fresh));
        Assert.Equal(1, r.P.Rows("uw"));
        var restarted = new UnitWatchPhase();                      // a restart keeps nothing
        Assert.Equal(0, restarted.AllRecords);
    }

    [Fact]
    public void EndPaths_passes_stop_leaves_nothing()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        r.Active.Clear();
        r.P.EventEnded("uw");
        r.For(5);
        Assert.Equal(0, r.P.AllRecords);
    }

    [Fact]
    public void EndPaths_empty_no_event_no_record()
    {
        var p = new UnitWatchPhase();
        p.EventEnded("uw");
        p.Clear();
        Assert.Equal(0, p.AllRecords);
        Assert.Empty(p.Health);
    }

    // ---- D16 Lines

    static readonly Regex[] Shapes =
    [
        new(@"^unit-watch [a-z0-9-]+ stuck: CHAR_\w+ wave \d+ \((standing|off-ground|stall|admin|still)\)$"),
        new(@"^unit-watch [a-z0-9-]+ recovered: CHAR_\w+ try [12]/2, \d+ m \((group|player|anchor)\)$"),
        new(@"^unit-watch [a-z0-9-]+ try failed: CHAR_\w+ try [12]/2 \((no landing|seen|still stuck)\)$"),
        new(@"^unit-watch [a-z0-9-]+ removed: CHAR_\w+ wave \d+ after 2 tries, no kill credit( \(in view\))?$"),
        new(@"^unit-watch [a-z0-9-]+ cleared: CHAR_\w+ \((fought|moving)\)$"),
        new(@"^unit-watch [a-z0-9-]+ stall: wave \d+ quiet \d+ min, \d+ leftovers marked$"),
        new(@"^unit-watch [a-z0-9-]+ waiting: CHAR_\w+, a player near$"),
    ];

    /// <summary>Every line of every flow: two tries and a removal, a removal in view, a fight clear, a moving clear, a
    /// stall and a still mark (D34).</summary>
    static List<string> AllLines()
    {
        var lines = new List<string>();
        var (a, _, k1) = Stranded();
        a.For(55, Keep(a, k1));
        lines.AddRange(a.Log);
        var (b, _, k2) = Stranded();
        b.W.Walk = (_, _, _) => false;
        b.For(20);
        b.W.Player(600, 500);
        b.For(180);
        lines.AddRange(b.Log);
        var (c, _, k3) = Marked();
        c.For(5);
        c.P.Fights.Report(k3, true, c.Now, c.W.Ledger.IsTracked);
        c.For(5);
        lines.AddRange(c.Log);
        var (d, _, k4) = Stranded();
        d.For(15, Keep(d, k4));
        d.For(95);
        lines.AddRange(d.Log);
        var (e, _, _) = Quiet();
        e.For(605);
        lines.AddRange(e.Log);
        var (f, _) = AtSpawn(0f);                                  // D34: still at its spawn spot beside a player
        f.For(15);
        lines.AddRange(f.Log);
        Assert.NotEqual(0, k2);
        return lines;
    }

    [Fact]
    public void Lines_fails_when_a_change_logs_no_line_or_two()
    {
        var (r, _, key) = Stranded();
        var counts = new List<int>();
        r.For(55, () =>
        {
            Keep(r, key)();
            counts.Add(r.Log.Count);
        });
        Assert.Equal(TwoTries, r.Log);
        // 15 s mark, 20 s move, 35 s failure, 40 s move, 55 s failure and removal: one line per change
        Assert.Equal([0, 0, 1, 2, 2, 2, 3, 4, 4, 4, 6], counts);
    }

    [Fact]
    public void Lines_fails_when_a_quiet_check_logs_a_line()
    {
        var (r, _, key) = Stranded();
        r.For(55, Keep(r, key));
        var n = r.Log.Count;
        r.For(600, Keep(r, key));
        Assert.Equal(n, r.Log.Count);
    }

    [Fact]
    public void Lines_fails_when_the_waiting_line_repeats()
    {
        var (r, _, _) = Marked();
        r.W.Player(580, 500);
        r.For(85);
        Assert.Equal(1, r.Lines("waiting:"));
        r.For(5);                                                  // the wait ends with a failed try; a new wait, one line
        r.For(80);
        Assert.Equal(2, r.Lines("waiting:"));
    }

    [Fact]
    public void Lines_fails_when_a_shape_differs()
    {
        var lines = AllLines();
        Assert.All(lines, l => Assert.Single(Shapes, s => s.IsMatch(l)));
        Assert.All(Shapes, s => Assert.Contains(lines, l => s.IsMatch(l)));
    }

    [Fact]
    public void Lines_fails_when_an_in_view_removal_lacks_in_view()
    {
        var (r, _, key) = Stranded();
        r.W.Walk = (_, _, _) => false;
        r.For(20);
        r.W.Player(600, 500);
        r.For(180);
        Assert.EndsWith("no kill credit (in view)", r.Log[^1]);
        var (q, _, unseen) = Stranded();
        q.For(55, Keep(q, unseen));
        Assert.EndsWith("no kill credit", q.Log[^1]);
        Assert.Equal(key, r.W.Despawns.Single());
    }

    [Fact]
    public void Lines_passes_the_six_shapes()
    {
        var lines = AllLines();
        Assert.Contains("unit-watch uw cleared: CHAR_Spider_Forest (fought)", lines);
        Assert.Contains("unit-watch uw cleared: CHAR_Spider_Forest (moving)", lines);
        Assert.Contains(StallLine, lines);
        Assert.All(lines, l => Assert.StartsWith("unit-watch uw ", l));
    }

    [Fact]
    public void Lines_empty_a_quiet_check_logs_nothing()
    {
        var (r, _) = Watch.Rig();
        r.W.Add("uw", Spawned, 500, 500);
        r.For(600);
        Assert.Empty(r.Log);
        Assert.Empty(r.Errors);
    }
}
