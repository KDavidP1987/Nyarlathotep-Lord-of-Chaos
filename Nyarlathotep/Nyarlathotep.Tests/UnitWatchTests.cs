using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The game side of unit watch over a real SpawnLedger (unit-watch D2-D11): positions, players, the walk check's
/// ground answer, claimed territory and regions are the test's; a move lands the unit where it was sent, and a removal
/// queues the unit on the ledger's staged despawn as SpawnTracker does. Players carry Hunt keys only, never a name.</summary>
sealed class WatchWorld(SpawnLedger? ledger = null) : IUnitWatchWorld
{
    public const string Prefab = "CHAR_Spider_Forest";

    public SpawnLedger Ledger { get; } = ledger ?? new(new LedgerLimits(300, 50, 1000, 5));
    public Dictionary<long, WatchUnit> At { get; } = [];
    public List<HuntCandidate> People { get; } = [];
    public Func<float, float, GroundReading?> GroundAt { get; set; } = (_, _) => new GroundReading(0f);
    public Func<float, float, byte, bool> Walk { get; set; } = (_, _, _) => true;
    public Func<float, float, bool> Claimed { get; set; } = (_, _) => false;
    public Func<float, float, string> Region { get; set; } = (_, _) => "FarbaneWoods";
    public int? Risen { get; set; }
    public long? Focus { get; set; }
    public WalkBudget Budget { get; set; } = new(1_000_000);
    public List<WatchMove> Moves { get; } = [];
    public List<long> Despawns { get; } = [];
    public HashSet<long> ThrowRead { get; } = [];
    public bool ThrowMove { get; set; }
    public bool ThrowDespawn { get; set; }
    public bool ThrowGround { get; set; }
    public int GroundCalls { get; private set; }
    public int WalkCalls { get; private set; }
    long _key = 5000;

    /// <summary>A unit of <paramref name="eventId"/> spawned at <paramref name="spawned"/> and tracked by the ledger, its
    /// anchor where it stands.</summary>
    public long Add(string? eventId, DateTime spawned, float x, float z, float y = 0f, int wave = 1, int group = 0)
    {
        Ledger.Request(Prefab, eventId, 1, new UnitLifetime(DateTime.MaxValue, 900), UnitTuning.None, _ => (x, y, z), wave: wave);
        var order = Ledger.TakeSpawns().Single();
        var key = ++_key;
        Assert.True(Ledger.Confirm(order, key, spawned));
        Put(key, x, z, y, group);
        return key;
    }

    public void Put(long key, float x, float z, float y = 0f, int group = 0) => At[key] = new WatchUnit(key, x, y, z, 10, group, (x, y, z, 10));
    public void MoveTo(long key, float x, float z) => At[key] = At[key] with { X = x, Z = z };
    public void Sink(long key, float y) => At[key] = At[key] with { Y = y };
    public void Aim(long key, float? distance, float range = 0f) => At[key] = At[key] with { TargetDistance = distance, AttackRange = range };
    public void Player(float x, float z, long key = 1, bool territory = false) => People.Add(new HuntCandidate(key, x, z, true, true, territory, false));

    /// <summary>One despawn batch (MaxDespawnsPerTick), the units leaving the world.</summary>
    public List<long> Drain()
    {
        var gone = Ledger.TakeDespawns().ToList();
        foreach (var k in gone) At.Remove(k);
        return gone;
    }

    public IEnumerable<TrackedUnit> Units => Ledger.Units;

    public WatchUnit? Read(TrackedUnit unit) =>
        ThrowRead.Contains(unit.Key) ? throw new InvalidOperationException("entity read failed") : At.TryGetValue(unit.Key, out var u) ? u : null;

    public IReadOnlyList<HuntCandidate> Players() => People;
    public long? FocusKey(ActiveEvent a) => Focus;

    public GroundReading? Ground(float x, float z)
    {
        GroundCalls++;
        if (ThrowGround) throw new InvalidOperationException("walk check down");
        return GroundAt(x, z);
    }

    public bool Walkable(float x, float z, byte level)
    {
        WalkCalls++;
        return Walk(x, z, level);
    }

    public float HeightOf(byte level) => (level - 10) * 2f;
    public string RegionOf(float x, float z) => Region(x, z);
    public bool IsClaimed(string eventId, float x, float z) => Claimed(x, z);
    public int? HordesRisen(ActiveEvent a) => Risen;

    public void Move(WatchMove move)
    {
        if (ThrowMove) throw new InvalidOperationException("teleport failed");
        Moves.Add(move);
        At[move.Key] = At[move.Key] with { X = move.X, Y = move.Y, Z = move.Z, Level = move.Level };
    }

    public bool Despawn(long key)
    {
        if (ThrowDespawn) throw new InvalidOperationException("despawn request failed");
        Despawns.Add(key);
        return Ledger.IsTracked(key) && Ledger.QueueDespawn(key);
    }
}

/// <summary>One watch phase over a <see cref="WatchWorld"/>, its log and errors, the running instances and a clock that
/// runs one check per <see cref="UnitWatch.CheckSeconds"/>.</summary>
sealed class WatchRig(WatchWorld? world = null)
{
    public static readonly DateTime T0 = Zones.Utc(2026, 10, 14, 20, 0);

    public WatchWorld W { get; } = world ?? new WatchWorld();
    public UnitWatchPhase P { get; } = new();
    public List<string> Log { get; } = [];
    public List<string> Errors { get; } = [];
    public WatchSettings S { get; set; } = WatchSettings.Default;
    public List<ActiveEvent> Active { get; } = [];
    public DateTime Now { get; set; } = T0;

    public void Check() => P.Run(Now, Active, W, S, Log.Add, Errors.Add);

    /// <summary>One check every 5 s for <paramref name="seconds"/>, the first 5 s from now; <paramref name="after"/> runs
    /// after each.</summary>
    public void For(int seconds, Action? after = null)
    {
        for (var t = UnitWatch.CheckSeconds; t <= seconds; t += UnitWatch.CheckSeconds)
        {
            Now = Now.AddSeconds(UnitWatch.CheckSeconds);
            Check();
            after?.Invoke();
        }
    }

    public Outcome Unstick(string id) => P.Unstick(id, true, Active.FirstOrDefault(a => a.Id == id), W, S, Now, Log.Add);

    public int Lines(string fragment) => Log.Count(l => l.Contains(fragment, StringComparison.Ordinal));
}

/// <summary>The events the watch tests run: a SpawnWaves event at a Point (500, 500), waveList and arena forms.</summary>
static class Watch
{
    public static EventDefinition Waves(string id = "uw", IReadOnlyList<WaveSpec>? list = null, Arena? arena = null, Growth? growth = null,
        Scope scope = default, bool allowTerritory = false) =>
        new(id, "Unit watch test", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 900,
            new SpawnWavesAction([new UnitEntry(WatchWorld.Prefab, 4)], list?.Count ?? 3, 60, 10, new Location(LocationType.Point, 500, 500), null) with
            {
                WaveList = list, Arena = arena, Growth = growth, Scope = scope, AllowTerritory = allowTerritory, Scoreboard = true,
            },
            Announce.None);

    public static ActiveEvent Run(EventDefinition d, DateTime started) =>
        new(new RunningInstance(d, started, started.AddSeconds(d.DurationSeconds)), "manual", null);

    /// <summary>Two waves; the second waits for the first's clear unless <paramref name="after"/> gives it a timer.</summary>
    public static IReadOnlyList<WaveSpec> Two(int? after = null) =>
        [new([new UnitEntry(WatchWorld.Prefab, 2)]), new([new UnitEntry(WatchWorld.Prefab, 2)], after)];

    /// <summary>A rig running <paramref name="d"/> (default: uw, the units form) from T0.</summary>
    public static (WatchRig R, ActiveEvent A) Rig(EventDefinition? d = null, WatchWorld? w = null)
    {
        var r = new WatchRig(w);
        var a = Run(d ?? Waves(), WatchRig.T0);
        r.Active.Add(a);
        return (r, a);
    }
}

/// <summary>unit-watch D2-D5 (the standing and off-ground rules, the fight read, the landing) over Logic/UnitWatch.cs; the
/// tries, removal, stall backstop, controls, end paths and lines are in UnitWatchTests.Tries.cs.</summary>
public partial class UnitWatchTests
{
    static readonly DateTime T0 = WatchRig.T0;
    static readonly DateTime Spawned = T0.AddSeconds(1);

    /// <summary>One uw unit at (500, 500) and one player <paramref name="playerAt"/> m east of it.</summary>
    static (WatchRig R, long Key) Standing(float? playerAt = 39f)
    {
        var (r, _) = Watch.Rig();
        var key = r.W.Add("uw", Spawned, 500, 500);
        if (playerAt is { } d) r.W.Player(500 + d, 500);
        return (r, key);
    }

    const string StandingLine = "unit-watch uw stuck: CHAR_Spider_Forest wave 1 (standing)";
    const string OffGroundLine = "unit-watch uw stuck: CHAR_Spider_Forest wave 1 (off-ground)";

    // ---- D2 Standing

    [Fact]
    public void Standing_fails_when_a_unit_standing_with_a_player_at_39_m_is_not_stuck()
    {
        var (r, key) = Standing(39f);
        r.For(90);                                                 // the window starts at the first check (5 s): 85 s so far
        Assert.Null(r.P.MarkOf(key));
        r.For(5);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));
        Assert.Equal(1, r.Lines(StandingLine));
    }

    [Fact]
    public void Standing_fails_when_a_unit_with_the_nearest_player_at_41_m_is_stuck()
    {
        var (r, key) = Standing(41f);
        r.For(600);
        Assert.Null(r.P.MarkOf(key));
        Assert.Empty(r.Log);
    }

    [Fact]
    public void Standing_fails_when_a_unit_that_fought_at_80_s_is_stuck_at_91_s()
    {
        var (r, key) = Standing(39f);
        r.For(85);                                                 // 80 s into the window
        Assert.True(r.P.Fights.Report(key, true, r.Now, r.W.Ledger.IsTracked));
        r.For(10);                                                 // 90 s after the window's start
        Assert.Null(r.P.MarkOf(key));
        r.For(80);
        Assert.Null(r.P.MarkOf(key));
        r.For(5);                                                  // 90 s after the fresh window (the check after the fight)
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));
    }

    [Fact]
    public void Standing_fails_when_a_unit_that_moved_5_5_m_is_stuck()
    {
        var (r, key) = Standing(39f);
        r.For(50);
        r.W.MoveTo(key, 505.5f, 500);
        r.For(50);
        Assert.Null(r.P.MarkOf(key));
        var (q, still) = Standing(39f);                            // 5 m is standing still
        q.For(50);
        q.W.MoveTo(still, 505f, 500);
        q.For(45);
        Assert.Equal(StuckReason.Standing, q.P.MarkOf(still));
    }

    [Fact]
    public void Standing_fails_when_a_unit_moved_by_a_jump_is_stuck()
    {
        var (r, key) = Standing(39f);
        r.For(60);
        r.W.MoveTo(key, 560f, 500);                                // a hidden jump or an apocalypse hop, 21 m from the player
        r.For(60);
        Assert.Null(r.P.MarkOf(key));
        r.For(35);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));       // a fresh window, then standing again
    }

    [Fact]
    public void Standing_fails_when_a_unit_holding_a_player_target_is_not_stuck()
    {
        var (r, key) = Standing(200f);
        r.W.Aim(key, 20f);                                         // its target 20 m away, beyond its reach: not a fight
        r.For(95);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));
        var (q, close) = Standing(200f);
        q.W.Aim(close, 4f);                                        // within 5 m: a fight, never standing
        q.For(300);
        Assert.Null(q.P.MarkOf(close));
    }

    [Fact]
    public void Standing_fails_when_a_roamer_or_horde_unit_is_skipped()
    {
        var r = new WatchRig();
        var apo = Apo.Active(Apo.Def(), T0);
        r.Active.Add(apo);
        var roamer = r.W.Add("apo", Spawned, 500, 500, wave: Apocalypse.RoamerWave(1));
        var horde = r.W.Add("apo", Spawned, 900, 500, wave: Apocalypse.HordeWave(1));
        r.W.Player(530, 500);
        r.W.Player(930, 500, key: 2);
        r.For(95);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(roamer));
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(horde));
        Assert.Equal(1, r.Lines("unit-watch apo stuck: CHAR_Spider_Forest wave 101 (standing)"));
        Assert.Equal(1, r.Lines("unit-watch apo stuck: CHAR_Spider_Forest wave 1 (standing)"));
    }

    [Fact]
    public void Standing_fails_when_a_spawn_or_earlier_instance_unit_is_checked()
    {
        var (r, key) = Standing(39f);
        var manual = r.W.Add(null, Spawned, 500, 510);             // `.nyar spawn`
        var earlier = r.W.Add("uw", T0.AddSeconds(-30), 505, 500); // an earlier instance's, left from a stop and start
        r.For(300);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));
        Assert.Null(r.P.MarkOf(manual));
        Assert.Null(r.P.MarkOf(earlier));
        Assert.Equal(1, r.P.Rows("uw"));
        Assert.Equal(1, r.Lines("stuck:"));
    }

    [Fact]
    public void Standing_passes_wave_unit_beside_a_player_marked_once()
    {
        var (r, key) = Standing(10f);
        r.For(150);
        Assert.Equal(StuckReason.Standing, r.P.MarkOf(key));
        Assert.Equal(1, r.Lines("stuck:"));
        Assert.Equal(1, r.Lines("waiting:"));                      // the player 10 m away: no move in view
        Assert.Empty(r.W.Moves);
    }

    [Fact]
    public void Standing_empty_no_player_no_mark()
    {
        var (r, key) = Standing(null);
        r.For(1200);
        Assert.Null(r.P.MarkOf(key));
        Assert.Empty(r.Log);
    }

    // ---- D3 OffGround

    static (WatchRig R, long Key) Sunk(float y, Func<float, float, GroundReading?>? ground = null)
    {
        var (r, key) = Standing(null);
        if (ground is not null) r.W.GroundAt = ground;
        r.W.Sink(key, y);
        return (r, key);
    }

    [Fact]
    public void OffGround_fails_when_below_min_y_or_far_below_ground_is_not_stuck()
    {
        var (r, key) = Sunk(-150f);                                // below WalkHeight.MinY
        r.For(15);
        Assert.Equal(1, r.Lines(OffGroundLine));
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
        var (q, deep) = Sunk(-15f);                                // 15 m below the lowest grounded level (0)
        q.For(15);
        Assert.Equal(1, q.Lines(OffGroundLine));
        Assert.Equal(StuckReason.OffGround, q.P.MarkOf(deep));
    }

    [Fact]
    public void OffGround_fails_when_no_grounded_level_is_not_stuck()
    {
        var (r, key) = Sunk(0f, (_, _) => new GroundReading(null));
        r.For(15);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
    }

    [Fact]
    public void OffGround_fails_when_one_reading_marks()
    {
        var (r, key) = Sunk(-150f);
        r.For(5);
        r.W.Sink(key, 0f);                                         // mid-fall: grounded again at the next check
        r.For(120);
        Assert.Null(r.P.MarkOf(key));
        Assert.Empty(r.Log);
    }

    [Fact]
    public void OffGround_fails_when_two_readings_5_s_apart_mark()
    {
        var (r, key) = Sunk(-150f);
        r.For(10);                                                 // readings at 5 s and 10 s
        Assert.Null(r.P.MarkOf(key));
        r.For(5);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
    }

    [Fact]
    public void OffGround_fails_when_grounded_at_a_neighbouring_level_marks()
    {
        var (below, a) = Sunk(0f, (_, _) => new GroundReading(-2f));   // the lowest level one step below the unit
        below.For(120);
        Assert.Null(below.P.MarkOf(a));
        var (above, b) = Sunk(0f, (_, _) => new GroundReading(2f));    // a level just above: 2 m under it is not 10 m
        above.For(120);
        Assert.Null(above.P.MarkOf(b));
        var (edge, c) = Sunk(0f, (_, _) => new GroundReading(10f));    // exactly 10 m below
        edge.For(120);
        Assert.Null(edge.P.MarkOf(c));
    }

    [Fact]
    public void OffGround_fails_when_a_unit_that_fought_is_marked()
    {
        var (r, key) = Sunk(-150f);
        Assert.True(r.P.Fights.Report(key, true, T0.AddSeconds(2), r.W.Ledger.IsTracked));
        r.For(90);
        Assert.Null(r.P.MarkOf(key));                              // the fight is under 90 s old
        r.For(15);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
    }

    [Fact]
    public void OffGround_fails_when_an_unanswered_or_throwing_read_marks()
    {
        var (r, key) = Sunk(-15f, (_, _) => null);                 // outside the walk data
        r.For(120);
        Assert.Null(r.P.MarkOf(key));
        var (q, thrown) = Sunk(-15f);
        q.W.ThrowGround = true;
        q.For(120);
        Assert.Null(q.P.MarkOf(thrown));
        Assert.Single(q.Errors);                                   // once per streak
        var (h, high) = Sunk(1500f);                               // a height outside the check's range
        h.For(120);
        Assert.Null(h.P.MarkOf(high));
        Assert.Equal(0, h.W.GroundCalls);
    }

    [Fact]
    public void OffGround_fails_when_a_roamer_or_horde_unit_is_skipped()
    {
        var r = new WatchRig();
        r.Active.Add(Apo.Active(Apo.Def(), T0));
        var roamer = r.W.Add("apo", Spawned, 500, 500, -150f, wave: Apocalypse.RoamerWave(2));
        var horde = r.W.Add("apo", Spawned, 900, 500, -150f, wave: Apocalypse.HordeWave(2));
        r.For(15);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(roamer));
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(horde));
    }

    [Fact]
    public void OffGround_passes_sunk_unit_marked_after_10_s()
    {
        var (r, key) = Sunk(-15f);
        r.W.Player(510, 500);                                      // whether or not a player is near
        r.For(20);
        Assert.Equal(StuckReason.OffGround, r.P.MarkOf(key));
        Assert.Equal(["unit-watch uw stuck: CHAR_Spider_Forest wave 1 (off-ground)", "unit-watch uw waiting: CHAR_Spider_Forest, a player near"], r.Log);
    }

    [Fact]
    public void OffGround_empty_grounded_units_never_read_off()
    {
        var (r, _) = Standing(null);
        for (var i = 0; i < 20; i++) r.W.Add("uw", Spawned, 600 + 20 * i, 500);
        r.For(600);
        Assert.Empty(r.Log);
        Assert.True(r.W.GroundCalls > 0);
    }

    // ---- D4 Fight

    [Fact]
    public void Fight_fails_when_a_hit_with_a_player_is_not_reported()
    {
        var book = new FightBook();
        Assert.True(book.Report(7, otherIsPlayer: true, T0, _ => true));       // our unit hit a player, or a player hit it
        Assert.True(book.Fought(7, T0));
        Assert.Equal(T0, book.Last(7));
        Assert.True(book.Report(7, true, T0.AddSeconds(30), _ => true));
        Assert.False(book.Fought(7, T0.AddSeconds(31)));
        Assert.True(book.Fought(7, T0.AddSeconds(30)));
    }

    [Fact]
    public void Fight_fails_when_a_hit_between_npcs_is_a_fight()
    {
        var book = new FightBook();
        Assert.False(book.Report(7, otherIsPlayer: false, T0, _ => true));     // another NPC or a structure
        Assert.False(book.Fought(7, T0));
        Assert.Equal(0, book.Rows);
    }

    [Fact]
    public void Fight_fails_when_an_untracked_unit_gets_a_row()
    {
        var book = new FightBook();
        Assert.False(book.Report(9, true, T0, _ => false));
        Assert.Equal(0, book.Rows);
        Assert.Null(book.Last(9));
    }

    [Fact]
    public void Fight_fails_when_a_row_outlives_its_unit()
    {
        var (r, key) = Standing(39f);
        var other = r.W.Add("uw", Spawned, 700, 500);
        Assert.True(r.P.Fights.Report(key, true, T0, r.W.Ledger.IsTracked));
        Assert.True(r.P.Fights.Report(other, true, T0, r.W.Ledger.IsTracked));
        r.W.Ledger.Forget(key);                                    // it left the ledger (lifetime, despawn, sweep)
        r.For(5);
        Assert.Null(r.P.Fights.Last(key));
        Assert.Equal(1, r.P.Fights.Rows);
        r.P.Died(other, "uw", 1, r.Now);
        Assert.Equal(0, r.P.Fights.Rows);
    }

    [Fact]
    public void Fight_fails_when_a_row_holds_a_player()
    {
        // the book's only state is a time per our unit's key: no name, player key or position can be held
        var fields = typeof(FightBook).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.All(fields, f => Assert.Equal(typeof(Dictionary<long, DateTime>), f.FieldType));
        Assert.All(typeof(FightBook).GetMethod(nameof(FightBook.Report))!.GetParameters(),
            p => Assert.DoesNotContain(p.ParameterType, new[] { typeof(string), typeof(HuntCandidate), typeof(float) }));
    }

    [Fact]
    public void Fight_fails_when_a_target_at_5_5_m_counts_as_fought()
    {
        var u = new WatchUnit(1, 0, 0, 0, 10, 0, (0, 0, 0, 10));
        Assert.False(UnitWatch.TargetClose(u with { TargetDistance = 5.5f }));
        Assert.True(UnitWatch.TargetClose(u with { TargetDistance = 5f }));
        Assert.False(UnitWatch.TargetClose(u));
        Assert.True(UnitWatch.TargetClose(u with { TargetDistance = 15f, AttackRange = 20f }));   // a ranged unit in its range (Review 2 F2)
        Assert.False(UnitWatch.TargetClose(u with { TargetDistance = 25f, AttackRange = 20f }));
    }

    [Fact]
    public void Fight_passes_hit_or_close_target_both_fight()
    {
        var (r, key) = Standing(39f);
        for (var i = 0; i < 6; i++)
        {
            r.For(60);
            r.P.Fights.Report(key, true, r.Now, r.W.Ledger.IsTracked);
        }
        Assert.Null(r.P.MarkOf(key));
        var (q, close) = Standing(39f);
        q.W.Aim(close, 3f);
        q.For(400);
        Assert.Null(q.P.MarkOf(close));
        Assert.Empty(q.Log);
    }

    [Fact]
    public void Fight_empty_no_hit_no_row()
    {
        var (r, key) = Standing(null);
        r.For(60);
        Assert.Equal(0, r.P.Fights.Rows);
        Assert.Null(r.P.Fights.Last(key));
    }

    // ---- D5 Landing

    static readonly WatchUnit Lost = new(1, 500, -150, 500, 10, 0, (520, 0, 500, 10));

    static LandingContext Ctx(IReadOnlyList<HuntCandidate>? online = null, IReadOnlyList<HuntCandidate>? eligible = null, IReadOnlyList<WatchUnit>? mates = null,
        Func<float, float, bool>? allowed = null, Func<float, float, byte, bool>? walk = null, WalkBudget? budget = null) =>
        new(online ?? [], eligible ?? online ?? [], mates ?? [], allowed ?? ((_, _) => true), walk ?? ((_, _, _) => true), l => (l - 10) * 2f,
            budget ?? new WalkBudget(1_000_000));

    static HuntCandidate P(float x, float z, long key = 1, bool territory = false) => new(key, x, z, true, true, territory, false);

    static float Dist(float ax, float az, float bx, float bz) => HuntingGrounds.Distance(ax, az, bx, bz);

    /// <summary>A rig whose one uw unit (500, 500) is marked by hand, with no player near.</summary>
    static (WatchRig R, ActiveEvent A, long Key) Marked(EventDefinition? d = null)
    {
        var (r, a) = Watch.Rig(d);
        var key = r.W.Add("uw", Spawned, 500, 500);
        r.Now = T0.AddSeconds(5);
        Assert.True(r.Unstick(a.Id).Ok);
        return (r, a, key);
    }

    [Fact]
    public void Landing_fails_when_a_unit_moves_with_a_player_at_99_m()
    {
        var (r, _, key) = Marked();
        r.W.Player(599, 500);
        r.For(60);
        Assert.Empty(r.W.Moves);
        Assert.Equal(1, r.Lines("waiting:"));
        r.W.People.Clear();
        r.W.Player(601, 500);
        r.For(5);
        Assert.Equal(key, Assert.Single(r.W.Moves).Key);
    }

    [Fact]
    public void Landing_fails_when_a_landing_is_within_50_m_of_a_player()
    {
        var near = P(525, 500, territory: true);                   // online, not eligible, 5 m from the anchor
        var (move, _) = LandingRule.Choose(Lost, Ctx(online: [near], eligible: []));
        Assert.Null(move);
        var far = P(575, 500, territory: true);                    // 55 m from the anchor: only spots 50 m or more from it
        var (m, _) = LandingRule.Choose(Lost, Ctx(online: [far], eligible: []));
        Assert.NotNull(m);
        Assert.True(Dist(m!.Value.X, m.Value.Z, 575, 500) >= 50f);
        Assert.Equal(LandingKind.Anchor, m.Value.Kind);
    }

    [Fact]
    public void Landing_fails_when_a_landing_is_in_territory_or_out_of_scope()
    {
        var (r, _, _) = Marked();
        r.W.Claimed = (x, _) => x < 600;                           // the anchor's ground is claimed
        r.For(5);
        Assert.Empty(r.W.Moves);
        Assert.Equal(1, r.Lines("try failed: CHAR_Spider_Forest try 1/2 (no landing)"));
        var (t, _, _) = Marked(Watch.Waves(allowTerritory: true));
        t.W.Claimed = (x, _) => x < 600;
        t.For(5);
        Assert.Single(t.W.Moves);
        var (s, _, _) = Marked(Watch.Waves(scope: new Scope(["FarbaneWoods"])));
        s.W.Region = (x, _) => x < 503 ? "FarbaneWoods" : "DunleyFarmlands";
        s.W.MoveTo(s.W.Ledger.Units.Single().Key, 300, 500);
        s.For(5);
        Assert.All(s.W.Moves, m => Assert.True(m.X < 503));
        Assert.Single(s.W.Moves);
    }

    [Fact]
    public void Landing_fails_when_a_landing_leaves_the_arena()
    {
        var (r, _, key) = Marked(Watch.Waves(arena: new Arena(20)));
        r.W.Put(key, 400, 500);                                    // pushed out of the arena, its anchor with it
        r.For(5);
        Assert.Empty(r.W.Moves);                                   // every anchor spot lies outside the 20 m circle
        var (q, _, inside) = Marked(Watch.Waves(arena: new Arena(20)));
        q.W.Put(inside, 512, 500);
        q.W.MoveTo(inside, 400, 500);
        q.For(5);
        var m = Assert.Single(q.W.Moves);
        Assert.True(Dist(m.X, m.Z, 500, 500) <= 20f);
    }

    [Fact]
    public void Landing_fails_when_a_landing_is_not_walkable()
    {
        var (m, _) = LandingRule.Choose(Lost, Ctx(walk: (x, z, l) => x > 524 && l == 10));
        Assert.NotNull(m);
        Assert.True(m!.Value.X > 524);
        Assert.Equal((byte)10, m.Value.Level);
        Assert.Null(LandingRule.Choose(Lost, Ctx(walk: (_, _, _) => false)).Move);
    }

    [Fact]
    public void Landing_fails_when_the_order_is_not_group_player_anchor()
    {
        var mate = new WatchUnit(2, 300, 0, 300, 11, 0, (300, 0, 300, 11));
        var hunter = P(800, 500);
        var g = LandingRule.Choose(Lost, Ctx(online: [hunter], mates: [mate])).Move!.Value;
        Assert.Equal(LandingKind.Group, g.Kind);
        Assert.True(Dist(g.X, g.Z, 300, 300) <= UnitWatch.GroupReach + 0.01f);
        Assert.Equal((byte)11, g.Level);                           // the mate's own level
        var p = LandingRule.Choose(Lost, Ctx(online: [hunter])).Move!.Value;
        Assert.Equal(LandingKind.Player, p.Kind);
        var a = LandingRule.Choose(Lost, Ctx()).Move!.Value;
        Assert.Equal(LandingKind.Anchor, a.Kind);
        Assert.True(Dist(a.X, a.Z, 520, 500) <= UnitWatch.AnchorReach + 0.01f);
        // a mate with no spot falls through to the player
        var blocked = LandingRule.Choose(Lost, Ctx(online: [hunter], mates: [mate], walk: (x, _, _) => x > 400)).Move!.Value;
        Assert.Equal(LandingKind.Player, blocked.Kind);
    }

    [Fact]
    public void Landing_fails_when_a_player_landing_misses_the_ring()
    {
        foreach (var at in new[] { (800f, 500f), (500f, 900f), (300f, 300f) })
        {
            var (m, _) = LandingRule.Choose(Lost, Ctx(online: [P(at.Item1, at.Item2)]));
            var d = Dist(m!.Value.X, m.Value.Z, at.Item1, at.Item2);
            Assert.Equal(LandingKind.Player, m.Value.Kind);
            Assert.InRange(d, 50f - 0.01f, 60f + 0.01f);
        }
        // the eligible player nearest the unit; a revenge instance aims at its focus player only
        var near = P(700, 500, 1);
        var farther = P(500, 800, 2);
        Assert.InRange(Dist(LandingRule.Choose(Lost, Ctx(online: [near, farther])).Move!.Value.X, LandingRule.Choose(Lost, Ctx(online: [near, farther])).Move!.Value.Z, 700, 500), 49.99f, 60.01f);
        var focus = LandingRule.Eligible([near, farther], Scope.Global, (_, _) => "x", 2, focusOnly: true);
        Assert.Equal(2, Assert.Single(focus).Key);
    }

    [Fact]
    public void Landing_fails_when_the_return_point_stays_behind()
    {
        var (r, _, key) = Marked();
        r.For(5);
        var m = Assert.Single(r.W.Moves);
        Assert.Equal((m.X, m.Y, m.Z), m.Return);
        Assert.Equal(m.X, r.W.At[key].X);
    }

    [Fact]
    public void Landing_fails_when_an_untracked_or_other_unit_moves()
    {
        var (r, _) = Watch.Rig();
        var ours = r.W.Add("uw", Spawned, 500, 500);
        var manual = r.W.Add(null, Spawned, 520, 500, -150f);
        var earlier = r.W.Add("uw", T0.AddSeconds(-30), 540, 500, -150f);
        var native = 9_999L;
        r.W.Put(native, 560, 500, -150f);                          // in the world, not in the ledger
        r.Now = T0.AddSeconds(5);
        var o = r.Unstick("uw");
        Assert.Equal("1", o.Field("marked"));
        r.For(120);
        Assert.All(r.W.Moves, m => Assert.Equal(ours, m.Key));
        Assert.DoesNotContain(manual, r.W.Moves.Select(m => m.Key));
        Assert.DoesNotContain(earlier, r.W.Despawns);
        Assert.DoesNotContain(native, r.W.Despawns);
    }

    [Fact]
    public void Landing_fails_when_a_check_moves_an_eleventh_unit()
    {
        var (r, _) = Watch.Rig();
        for (var i = 0; i < 12; i++) r.W.Add("uw", Spawned, 300 + 30 * i, 500);
        r.Now = T0.AddSeconds(5);
        Assert.Equal("12", r.Unstick("uw").Field("marked"));
        r.For(5);
        Assert.Equal(UnitWatch.MaxMoves, r.W.Moves.Count);
        r.For(5);
        Assert.Equal(12, r.W.Moves.Count);
    }

    [Fact]
    public void Landing_passes_group_landing_unseen()
    {
        var (r, _) = Watch.Rig();
        var lost = r.W.Add("uw", Spawned, 500, 500, -150f);
        var mate = r.W.Add("uw", Spawned, 300, 300);
        r.W.Player(800, 800);
        r.For(20);
        var m = Assert.Single(r.W.Moves);
        Assert.Equal(lost, m.Key);
        Assert.Equal(LandingKind.Group, m.Kind);
        Assert.True(Dist(m.X, m.Z, 300, 300) <= UnitWatch.GroupReach + 0.01f);
        Assert.Contains(r.Log, l => l.StartsWith("unit-watch uw recovered: CHAR_Spider_Forest try 1/2, ", StringComparison.Ordinal) && l.EndsWith(" m (group)", StringComparison.Ordinal));
        Assert.Null(r.P.MarkOf(mate));
    }

    [Fact]
    public void Landing_empty_nothing_marked_nothing_moved()
    {
        var (r, _) = Watch.Rig();
        for (var i = 0; i < 5; i++) r.W.Add("uw", Spawned, 300 + 30 * i, 500);
        r.For(300);
        Assert.Empty(r.W.Moves);
        Assert.Equal(0, r.W.WalkCalls);
    }
}
