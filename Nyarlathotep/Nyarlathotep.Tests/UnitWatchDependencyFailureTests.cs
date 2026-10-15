using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>A <see cref="WatchWorld"/> whose reads, moves and removal requests throw per unit or as a whole, as a failing
/// entity read, teleport or despawn queue would in Services/SpawnTracker's watch world (unit-watch D18).</summary>
sealed class FaultyWatchWorld(WatchWorld inner) : IUnitWatchWorld
{
    public WatchWorld Inner { get; } = inner;
    public HashSet<long> ThrowMoveFor { get; } = [];
    public HashSet<long> ThrowDespawnFor { get; } = [];
    public bool ThrowUnits { get; set; }
    public bool ThrowPlayers { get; set; }
    public int ReadCalls { get; private set; }

    public IEnumerable<TrackedUnit> Units => ThrowUnits ? throw new InvalidOperationException("ledger down") : Inner.Units;

    public WatchUnit? Read(TrackedUnit unit)
    {
        ReadCalls++;
        return Inner.Read(unit);
    }

    public IReadOnlyList<HuntCandidate> Players() => ThrowPlayers ? throw new InvalidOperationException("player query down") : Inner.Players();
    public long? FocusKey(ActiveEvent a) => Inner.FocusKey(a);
    public bool? Grounded(float x, float z, byte level) => Inner.Grounded(x, z, level);
    public bool Walkable(float x, float z, byte level) => Inner.Walkable(x, z, level);
    public float HeightOf(byte level) => Inner.HeightOf(level);
    public string RegionOf(float x, float z) => Inner.RegionOf(x, z);
    public bool IsClaimed(string eventId, float x, float z) => Inner.IsClaimed(eventId, x, z);
    public int? HordesRisen(ActiveEvent a) => Inner.HordesRisen(a);
    public WalkBudget Budget => Inner.Budget;

    public void Move(WatchMove move)
    {
        if (ThrowMoveFor.Contains(move.Key)) throw new InvalidOperationException("teleport failed");
        Inner.Move(move);
    }

    public bool Despawn(long key) => ThrowDespawnFor.Contains(key) ? throw new InvalidOperationException("despawn request failed") : Inner.Despawn(key);
}

/// <summary>
/// unit-watch D18 (Watch guarded), the dependency suite's two categories (D20): watch-read (the position read, the fight read
/// and the walk check's ground read) and watch-move (the recovery move and the removal request). A throw for one unit skips
/// or fails that unit only, a failed read never marks, each family logs once per streak and holds its health entry until a
/// check runs clean or the events end, and nothing escapes the phase or the fight read, so the host system, the death pass,
/// the later tick phases, the despawn batch and the event ends all run.
/// </summary>
public class UnitWatchDependencyFailureTests
{
    static readonly DateTime T0 = WatchRig.T0;
    static readonly DateTime Spawned = T0.AddSeconds(1);

    const string Recovered1 = "unit-watch uw recovered: CHAR_Spider_Forest try 1/2, 20 m (anchor)";
    const string NoLanding1 = "unit-watch uw try failed: CHAR_Spider_Forest try 1/2 (no landing)";
    const string ReadLine = "unit-watch: read failed: entity read failed";
    const string MoveLine = "unit-watch: move failed: teleport failed";
    const string DespawnLine = "unit-watch: move failed: despawn request failed";

    /// <summary>A uw rig over a faulty world.</summary>
    static (WatchRig R, FaultyWatchWorld F, ActiveEvent A) Rig()
    {
        var inner = new WatchWorld();
        var r = new WatchRig(inner);
        var a = Watch.Run(Watch.Waves(), T0);
        r.Active.Add(a);
        return (r, new FaultyWatchWorld(inner), a);
    }

    /// <summary>One check of the phase over the faulty world, every 5 s for <paramref name="seconds"/>.</summary>
    static void For(WatchRig r, FaultyWatchWorld f, int seconds, Action? after = null)
    {
        for (var t = UnitWatch.CheckSeconds; t <= seconds; t += UnitWatch.CheckSeconds)
        {
            r.Now = r.Now.AddSeconds(UnitWatch.CheckSeconds);
            r.P.Run(r.Now, r.Active, f, r.S, r.Log.Add, r.Errors.Add);
            after?.Invoke();
        }
    }

    /// <summary>A uw unit spawned at (<paramref name="x"/>, 500), standing 20 m east of it 150 m under the map.</summary>
    static long Strand(WatchRig r, float x)
    {
        var key = r.W.Add("uw", Spawned, x, 500);
        r.W.MoveTo(key, x + 20, 500);
        r.W.Sink(key, -150f);
        return key;
    }

    /// <summary>Sinks the stranded units again after each check, as the in-game strand repeated after each recovery.</summary>
    static Action Keep(WatchRig r, params long[] keys) => () =>
    {
        foreach (var k in keys)
            if (r.W.At.ContainsKey(k)) r.W.Sink(k, -150f);
    };

    // ---- watch-read

    [Fact]
    public void WatchRead_fails_when_one_throwing_unit_stops_the_check()
    {
        var (r, f, _) = Rig();
        var broken = Strand(r, 300);
        var other = Strand(r, 700);
        r.W.ThrowRead.Add(broken);                                 // the lower key: read first
        For(r, f, 20);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(other));
        Assert.Contains(r.W.Moves, m => m.Key == other);
        Assert.Contains(Recovered1, r.Log);
        Assert.Null(r.P.MarkOf(broken));
        Assert.Equal([ReadLine], r.Errors);
    }

    [Fact]
    public void WatchRead_fails_when_a_failed_read_marks_a_unit()
    {
        var (r, f, _) = Rig();
        var unread = r.W.Add("uw", Spawned, 500, 500);
        r.W.Player(510, 500);                                      // a player at 10 m: the standing rule would mark it at 90 s
        r.W.ThrowRead.Add(unread);
        var sunk = r.W.Add("uw", Spawned, 900, 900, y: -15f);      // 15 m below ground 0, but its ground read throws
        r.W.ThrowGround = true;
        For(r, f, 300);
        Assert.Null(r.P.MarkOf(unread));
        Assert.Null(r.P.MarkOf(sunk));
        Assert.Empty(r.Log);
        Assert.Empty(r.W.Moves);
        Assert.Empty(r.W.Despawns);
    }

    [Fact]
    public void WatchRead_fails_when_an_error_line_repeats_within_a_streak()
    {
        var (r, f, _) = Rig();
        r.W.ThrowRead.Add(r.W.Add("uw", Spawned, 500, 500));
        r.W.ThrowRead.Add(r.W.Add("uw", Spawned, 600, 500));
        For(r, f, 300);
        Assert.Equal([ReadLine], r.Errors);                        // 60 failing checks, two units each: one line

        var read = new FightRead();
        var book = new FightBook();
        var errors = new List<string>();
        for (var i = 0; i < 50; i++) read.Pass(3, _ => throw new InvalidOperationException("bad row"), book, _ => true, T0, errors.Add);
        Assert.Equal(["unit-watch: read failed: fight: bad row"], errors);
        for (var i = 0; i < 50; i++)                               // a fault on every other frame: still one line
        {
            read.Pass(1, _ => throw new InvalidOperationException("bad row"), book, _ => true, T0, errors.Add);
            read.Pass(1, _ => null, book, _ => true, T0, errors.Add);
        }
        Assert.Equal(["unit-watch: read failed: fight: bad row"], errors);
    }

    [Fact]
    public void WatchRead_fails_when_a_health_entry_outlives_a_clean_check_or_the_end()
    {
        var (r, f, _) = Rig();
        var key = r.W.Add("uw", Spawned, 500, 500);
        r.W.ThrowRead.Add(key);
        For(r, f, 10);
        Assert.Equal([UnitWatchPhase.ReadFailing], r.P.Health);
        r.W.ThrowRead.Clear();
        For(r, f, 5);                                              // a clean check
        Assert.Empty(r.P.Health);
        r.W.ThrowRead.Add(key);
        For(r, f, 5);
        Assert.Equal([UnitWatchPhase.ReadFailing], r.P.Health);
        r.Active.Clear();
        r.P.EventEnded("uw");                                      // the events' end
        Assert.Empty(r.P.Health);

        var read = new FightRead();
        read.Pass(1, _ => throw new InvalidOperationException("bad row"), new FightBook(), _ => true, T0, _ => { });
        Assert.Equal([UnitWatchPhase.ReadFailing], read.Health);
        read.Pass(1, _ => null, new FightBook(), _ => true, T0, _ => { });   // a clean batch alone keeps the streak
        Assert.Equal([UnitWatchPhase.ReadFailing], read.Health);
        read.Checked();                                            // a faulting batch since the last check: still failing
        Assert.Equal([UnitWatchPhase.ReadFailing], read.Health);
        read.Checked();                                            // a check with no fault since: the streak ends
        Assert.Empty(read.Health);
        read.Pass(1, _ => throw new InvalidOperationException("bad row"), new FightBook(), _ => true, T0, _ => { });
        read.Ok();                                                 // the events ended, a purge or the watch off
        Assert.Empty(read.Health);
    }

    [Fact]
    public void WatchRead_fails_when_a_throwing_fight_row_stops_the_batch_or_escapes()
    {
        const long A = 7001, B = 7002, Stranger = 9999;
        var read = new FightRead();
        var book = new FightBook();
        var errors = new List<string>();
        long?[] rows = [A, null, 0, B, Stranger];
        var steps = new List<string>();
        void Host()
        {
            steps.Add("host work");                                // the game system's own work runs before the postfix
            var kept = read.Pass(rows.Length, i => i == 2 ? throw new InvalidOperationException("bad row") : rows[i], book,
                k => k is A or B, T0, errors.Add);
            steps.Add($"kept {kept}");
            steps.Add("death pass");                               // SpawnTracker.Died, after the fight read
        }
        Host();
        Assert.Equal(["host work", "kept 2", "death pass"], steps);
        Assert.Equal(T0, book.Last(A));
        Assert.Equal(T0, book.Last(B));
        Assert.Null(book.Last(Stranger));
        Assert.Single(errors);

        // a throwing tracked lookup and a throwing error sink stay inside the pass too
        var quiet = new FightRead();
        var ex = Record.Exception(() => quiet.Pass(2, i => A, new FightBook(), _ => throw new InvalidOperationException("ledger down"), T0,
            _ => throw new InvalidOperationException("log down")));
        Assert.Null(ex);
        Assert.Equal([UnitWatchPhase.ReadFailing], quiet.Health);
    }

    [Fact]
    public void WatchRead_fails_when_a_throw_escapes_the_phase()
    {
        var (r, f, a) = Rig();
        var key = Strand(r, 500);
        var steps = new List<string>();
        f.ThrowUnits = true;
        for (var i = 0; i < 3; i++)
        {
            r.Now = r.Now.AddSeconds(UnitWatch.CheckSeconds);
            steps.Add("watch");
            r.P.Tick(r.Now, r.Active, f, r.S, r.Log.Add, r.Errors.Add);
            steps.Add("announcements");                            // the later tick phases still run
        }
        Assert.Equal(["watch", "announcements", "watch", "announcements", "watch", "announcements"], steps);
        Assert.Equal(["unit-watch: read failed: ledger down"], r.Errors);
        Assert.Equal([UnitWatchPhase.ReadFailing], r.P.Health);

        f.ThrowUnits = false;
        f.ThrowPlayers = true;
        var ex = Record.Exception(() => r.P.Run(r.Now.AddSeconds(5), r.Active, f, r.S, _ => throw new InvalidOperationException("log down"),
            _ => throw new InvalidOperationException("log down")));
        Assert.Null(ex);

        f.ThrowPlayers = false;
        Assert.True(r.W.Ledger.QueueDespawn(key));                 // the despawn batch and the event's end still run
        Assert.Equal([key], r.W.Drain());
        r.Active.Remove(a);
        r.P.EventEnded("uw");
        Assert.Equal(0, r.P.AllRecords);
        Assert.Empty(r.P.Health);
    }

    [Fact]
    public void WatchRead_passes_good_read_ends_the_streak()
    {
        var (r, f, _) = Rig();
        var key = r.W.Add("uw", Spawned, 500, 500);
        r.W.ThrowRead.Add(key);
        For(r, f, 15);
        r.W.ThrowRead.Clear();
        For(r, f, 5);
        Assert.Empty(r.P.Health);
        r.W.ThrowRead.Add(key);
        For(r, f, 15);
        Assert.Equal([ReadLine, ReadLine], r.Errors);              // a new streak logs its own line
    }

    [Fact]
    public void WatchRead_empty_no_event_no_read()
    {
        var (r, f, _) = Rig();
        r.W.ThrowRead.Add(r.W.Add("uw", Spawned, 500, 500));
        r.Active.Clear();
        For(r, f, 300);
        Assert.Equal(0, f.ReadCalls);
        Assert.Empty(r.Errors);
        Assert.Empty(r.P.Health);
        Assert.Equal(0, new FightRead().Pass(0, _ => throw new InvalidOperationException("never"), new FightBook(), _ => true, T0, _ => { }));
    }

    // ---- watch-move

    [Fact]
    public void WatchMove_fails_when_a_throwing_move_stops_the_others()
    {
        var (r, f, _) = Rig();
        var broken = Strand(r, 300);
        var other = Strand(r, 700);
        f.ThrowMoveFor.Add(broken);                                // the lower key: tried first
        For(r, f, 20, Keep(r, broken, other));
        Assert.Equal([other], r.W.Moves.Select(m => m.Key));
        Assert.Contains(NoLanding1, r.Log);
        Assert.Contains(Recovered1, r.Log);
        Assert.Equal([MoveLine], r.Errors);
    }

    [Fact]
    public void WatchMove_fails_when_a_throwing_move_is_not_a_failed_try()
    {
        var (r, f, _) = Rig();
        var key = Strand(r, 500);
        f.ThrowMoveFor.Add(key);
        For(r, f, 20, Keep(r, key));
        Assert.Equal(1, r.P.TriesOf(key));
        Assert.Equal(NoLanding1, r.Log[^1]);
        For(r, f, 5, Keep(r, key));                                // the second throw: try 2, then the removal
        Assert.Equal([key], r.W.Despawns);
        Assert.Equal("unit-watch uw removed: CHAR_Spider_Forest wave 1 after 2 tries, no kill credit", r.Log[^1]);
        Assert.Empty(r.W.Moves);
    }

    [Fact]
    public void WatchMove_fails_when_a_throwing_despawn_stops_the_others_or_removes()
    {
        var (r, f, _) = Rig();
        var broken = Strand(r, 300);
        var other = Strand(r, 700);
        f.ThrowDespawnFor.Add(broken);
        For(r, f, 55, Keep(r, broken, other));                     // both reach their removal
        Assert.Equal([other], r.W.Despawns);
        Assert.True(r.W.Ledger.IsTracked(broken));
        Assert.Equal(1, r.W.Ledger.PendingDespawns);
        Assert.Single(r.Log, l => l.Contains("removed:", StringComparison.Ordinal));
        Assert.Equal([DespawnLine], r.Errors);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(broken));   // still waiting for its removal, never dropped

        f.ThrowDespawnFor.Clear();
        For(r, f, 5, Keep(r, broken, other));                      // retried at the next check
        Assert.Equal([other, broken], r.W.Despawns);
        Assert.Equal(2, r.W.Ledger.PendingDespawns);
        Assert.Equal(2, r.Log.Count(l => l.Contains("removed:", StringComparison.Ordinal)));
    }

    [Fact]
    public void WatchMove_fails_when_an_error_line_repeats_within_a_streak()
    {
        var (r, f, _) = Rig();
        var a = Strand(r, 300);
        var b = Strand(r, 700);
        f.ThrowMoveFor.UnionWith([a, b]);
        For(r, f, 25, Keep(r, a, b));                              // two failing checks, two units each
        Assert.Equal(2, r.W.Despawns.Count);
        Assert.Equal([MoveLine], r.Errors);
    }

    [Fact]
    public void WatchMove_fails_when_a_health_entry_outlives_a_clean_check_or_the_end()
    {
        var (r, f, _) = Rig();
        var key = Strand(r, 500);
        f.ThrowMoveFor.Add(key);
        For(r, f, 20, Keep(r, key));
        Assert.Equal([UnitWatchPhase.MoveFailing], r.P.Health);
        r.Active.Clear();
        r.P.EventEnded("uw");
        Assert.Empty(r.P.Health);

        var (q, g, _) = Rig();
        var unit = Strand(q, 500);
        g.ThrowMoveFor.Add(unit);
        For(q, g, 20, Keep(q, unit));
        Assert.Equal([UnitWatchPhase.MoveFailing], q.P.Health);
        g.ThrowMoveFor.Clear();
        For(q, g, 5, Keep(q, unit));                               // the second try moves: a clean check
        Assert.Contains("unit-watch uw recovered: CHAR_Spider_Forest try 2/2, 20 m (anchor)", q.Log);
        Assert.Empty(q.P.Health);
    }

    [Fact]
    public void WatchMove_passes_good_move_ends_the_streak()
    {
        var (r, f, _) = Rig();
        var key = Strand(r, 500);
        For(r, f, 20, Keep(r, key));
        Assert.Equal([Recovered1], r.Log.Skip(1));
        Assert.Empty(r.Errors);
        Assert.Empty(r.P.Health);
        Assert.Equal([key], r.W.Moves.Select(m => m.Key));
    }

    [Fact]
    public void WatchMove_empty_nothing_marked_no_move()
    {
        var (r, f, _) = Rig();
        var key = r.W.Add("uw", Spawned, 500, 500);
        f.ThrowMoveFor.Add(key);
        f.ThrowDespawnFor.Add(key);
        For(r, f, 300);                                            // grounded, no player near: never marked
        Assert.Empty(r.Log);
        Assert.Empty(r.W.Moves);
        Assert.Empty(r.Errors);
        Assert.Empty(r.P.Health);
    }
}
