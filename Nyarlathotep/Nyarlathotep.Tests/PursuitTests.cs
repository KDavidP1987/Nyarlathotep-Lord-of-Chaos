using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D8, D9 and D12's Logic: a run with `pursuit` checks every 5 s whether an eligible player stands
/// within its area of the wave centre; after afterSeconds without one, counted from the first wave's decision, it pursues
/// until it ends: its arena lifts, the hunt phase moves every unit of the run (later waves too) after the eligible player
/// nearest its group, a focus-only Revenge run after its focus player alone, unit-watch lands its units anywhere outside
/// claimed territory and its stall backstop skips it; every end path drops its records; a failing read skips that event's
/// check alone, logged once per streak. The run "pv" stands at a Point (0, 0) with arena 30, Hunt 30 and pursuit after 20 s.</summary>
public class PursuitTests
{
    static readonly DateTime T0 = new(2026, 10, 16, 20, 0, 0, DateTimeKind.Utc);

    static EventDefinition Def(string id = "pv", int after = 20, int? arena = 30, bool revenge = false, LocationType location = LocationType.Point) =>
        new(id, "x", true, Pillar.Spawns, revenge ? Trigger.Manual() with { Type = TriggerType.Revenge } : Trigger.Manual(), new Conditions(), 600,
            new SpawnWavesAction([new UnitEntry("CHAR_Spider_Forest", 5)], 3, 60, 10, new Location(location, 0, 0), null) with
            {
                Behaviour = new Behaviour(BehaviourType.Hunt, 30),
                Arena = arena is { } r ? new Arena(r) : null,
                Pursuit = new Pursuit(after),
            },
            Announce.None);

    static ActiveEvent Run(EventDefinition? d = null, string trigger = "manual", bool decided = true, (float X, float Y, float Z)? origin = null)
    {
        var a = new ActiveEvent(new RunningInstance(d ?? Def(), T0, T0.AddMinutes(10)), trigger, origin);
        if (decided) a.Record(T0, fought: true);
        return a;
    }

    static HuntCandidate P(long key, float x, float z, bool online = true, bool alive = true, bool territory = false, bool pvp = false) =>
        new(key, x, z, online, alive, territory, pvp);

    /// <summary>The game side of the pursuit check and of the hunt phase: players, claimed territory, a focus, live centres
    /// and the run's units; everything off the map is the region "None".</summary>
    sealed class World : IRegionWorld, IPursuitWorld
    {
        public List<HuntCandidate> People { get; } = [];
        public List<TrackedUnit> Tracked { get; } = [];
        public Dictionary<long, RegionUnit> At { get; } = [];
        public List<RegionJump> Jumps { get; } = [];
        public List<(float X, float Z)> LiveCentres { get; } = [];
        public Func<float, float, bool> Claimed { get; set; } = (_, _) => false;
        public long? Focus { get; set; }
        public bool ThrowPlayers { get; set; }
        public string? ThrowFor { get; set; }
        public int PlayerReads { get; private set; }
        public WalkBudget Budget { get; } = new(1_000_000);

        public void Add(long key, float x, float z, string eventId = "pv", int wave = 1, int group = 0)
        {
            Tracked.Add(new TrackedUnit(key, "CHAR_Spider_Forest", eventId, T0.AddSeconds(5), 600, T0.AddMinutes(10), wave));
            At[key] = new RegionUnit(key, eventId, T0.AddSeconds(5), x, 20f, z, 10, wave, group);
        }

        public IReadOnlyList<HuntCandidate> Players()
        {
            PlayerReads++;
            if (ThrowPlayers) throw new InvalidOperationException("player query failed");
            return People;
        }

        public bool IsClaimed(string eventId, float x, float z) =>
            ThrowFor == eventId ? throw new InvalidOperationException("territory read failed") : Claimed(x, z);

        public long? FocusKey(ActiveEvent a) => Focus;
        public IReadOnlyList<(float X, float Z)> Centres(ActiveEvent a) => LiveCentres;
        public IEnumerable<TrackedUnit> Units => Tracked;
        public RegionUnit? Read(TrackedUnit unit) => At.TryGetValue(unit.Key, out var u) ? u : null;
        public string RegionOf(float x, float z) => "None";
        public bool Walkable(float x, float z, byte level) => true;
        public float HeightOf(byte level) => level * 2f;
        public void Return(RegionReturn r, Scope scope) => At[r.Key] = At[r.Key] with { X = r.X, Z = r.Z };

        public void Jump(RegionJump j)
        {
            Jumps.Add(j);
            At[j.Key] = At[j.Key] with { X = j.X, Y = j.Y, Z = j.Z, Level = j.Level };
        }
    }

    sealed class Rig
    {
        public PursuitPhase Phase { get; } = new();
        public World W { get; } = new();
        public List<string> Info { get; } = [];
        public List<string> Errors { get; } = [];

        public IReadOnlyList<PursuitCheck> Tick(DateTime now, params ActiveEvent[] active) => Phase.Tick(now, active, W, Info.Add, Errors.Add);

        /// <summary>One tick every 5 s from <paramref name="from"/> to <paramref name="to"/> seconds after T0; the first
        /// tick at which a run started pursuing, in seconds, or null.</summary>
        public int? Until(int from, int to, params ActiveEvent[] active)
        {
            for (var s = from; s <= to; s += Pursuit.CheckSeconds)
                if (Tick(T0.AddSeconds(s), active).Any(c => c.Started)) return s;
            return null;
        }
    }

    // ================= D8 Start =================

    [Fact]
    public void Pursue_fails_when_pursuit_starts_before_after_seconds()
    {
        var r = new Rig();
        var a = Run();
        r.W.People.Add(P(1, 200, 0));                                                        // 200 m off, outside the 30 m area
        Assert.Equal(20, r.Until(0, 60, a));
        Assert.True(a.Pursuing);
        Assert.Equal(["event pv pursuit: on, no player within 30 m for 20 s"], r.Info);
        Assert.Empty(r.Tick(T0.AddSeconds(65), a));                                          // pursuing: no more checks, no second line
        Assert.Single(r.Info);
        Assert.False(PursuitPhase.Needed([a]));
    }

    [Fact]
    public void Pursue_passes_a_run_left_alone_chases_its_player()
    {
        var r = new Rig();
        var a = Run();
        r.W.People.Add(P(20, 3000, 3000));
        Assert.Equal(20, r.Until(0, 30, a));
        r.W.Add(1, 0, 0);
        var info = new List<string>();
        Assert.Equal(20L, Hunt(r.W, info, a)[(1, 0)]);
        Assert.NotEmpty(r.W.Jumps);
        Assert.Contains("event pv pursuit: group 1.0 on a player", info);
        Assert.False(ArenaPhase.HasArena(a));
    }

    [Fact]
    public void Pursue_empty_no_player_anywhere_units_stay()
    {
        var a = Run();
        a.Pursuing = true;
        var w = new World();
        w.Add(1, 0, 0);
        w.People.AddRange([P(10, 500, 0, territory: true), P(11, 600, 0, pvp: true)]);
        Assert.Empty(Hunt(w, null, a));
        Assert.Empty(w.Jumps);
        Assert.Equal((0f, 0f), (w.At[1].X, w.At[1].Z));
    }

    [Fact]
    public void Pursue_fails_when_pursuit_starts_with_an_eligible_player_in_the_area()
    {
        var r = new Rig();
        var a = Run();
        r.W.People.Add(P(1, 25, 0));                                                         // within the arena's 30 m
        Assert.Null(r.Until(0, 60, a));
        Assert.Equal(T0.AddSeconds(60), r.Phase.QuietSince("pv"));                           // reset at each check
        r.W.People[0] = P(1, 31, 0);                                                         // steps out at 60 s
        Assert.Equal(80, r.Until(65, 120, a));                                               // 20 s in a row after the last presence
    }

    [Fact]
    public void Pursue_fails_when_an_ineligible_player_in_the_area_holds_it()
    {
        foreach (var p in new[] { P(1, 5, 0, territory: true), P(2, 5, 0, pvp: true), P(3, 5, 0, alive: false), P(4, 5, 0, online: false), P(5, float.NaN, 0) })
        {
            var r = new Rig();
            var a = Run();
            r.W.People.Add(p);
            Assert.Equal(20, r.Until(0, 30, a));
        }
        var claimed = new Rig();                                                             // claimed by the event's own map
        claimed.W.Claimed = (x, _) => x < 10;
        claimed.W.People.Add(P(6, 5, 0));
        Assert.Equal(20, claimed.Until(0, 30, Run()));
    }

    [Fact]
    public void Pursue_fails_when_a_player_at_the_last_wave_centre_between_waves_is_not_counted()
    {
        // an AroundPlayer run between waves: no unit alive, so no live centre; the centre last seen still counts
        var r = new Rig();
        var a = Run(Def(location: LocationType.AroundPlayer, arena: null));
        r.W.LiveCentres.Add((500f, 500f));
        r.W.People.Add(P(1, 510, 500));
        Assert.Null(r.Until(0, 10, a));
        r.W.LiveCentres.Clear();                                                             // the wave is dead; the player stays
        Assert.Null(r.Until(15, 60, a));
        Assert.Equal(T0.AddSeconds(60), r.Phase.QuietSince("pv"));
        Assert.Equal(2, r.Phase.AllRecords);                                                 // the clock and the last centres
        r.W.People[0] = P(1, 3000, 3000);                                                    // walks away
        Assert.Equal(80, r.Until(65, 120, a));
        Assert.Equal(0, r.Phase.AllRecords);                                                 // pursuing: nothing kept
        // the last centres go with an end path
        var b = Run(Def(location: LocationType.AroundPlayer, arena: null));
        var q = new Rig();
        q.W.LiveCentres.Add((500f, 500f));
        q.Tick(T0, b);
        Assert.Equal(2, q.Phase.AllRecords);
        q.Phase.EventEnded("pv");
        Assert.Equal(0, q.Phase.AllRecords);
    }

    [Fact]
    public void Pursue_fails_when_the_players_are_read_more_than_once_a_tick()
    {
        var r = new Rig();
        var runs = new[] { Run(Def("pa")), Run(Def("pb")), Run(Def("pc")) };
        r.W.People.Add(P(1, 200, 0));
        r.Tick(T0, runs);
        Assert.Equal(1, r.W.PlayerReads);
        r.W.ThrowPlayers = true;                                                              // a failed read fails each run, read once
        r.Tick(T0.AddSeconds(5), runs);
        Assert.Equal(2, r.W.PlayerReads);
        Assert.Equal(["pursuit pa: check failed: player query failed", "pursuit pb: check failed: player query failed",
            "pursuit pc: check failed: player query failed"], r.Errors);
    }

    [Fact]
    public void Pursue_fails_when_the_clock_starts_before_the_first_waves_decision()
    {
        var r = new Rig();
        var a = Run(decided: false);
        Assert.Null(r.Until(0, 60, a));
        Assert.Null(r.Phase.QuietSince("pv"));
        a.Record(T0.AddSeconds(60), fought: true);
        Assert.Equal(80, r.Until(65, 120, a));
    }

    [Fact]
    public void Pursue_fails_when_the_area_is_not_the_arena_else_the_hunt_range_around_the_centre()
    {
        var noArena = Run(Def(arena: null));
        var r = new Rig();
        r.W.People.Add(P(1, 29, 0));                                                         // the Hunt range, 30 m
        Assert.Null(r.Until(0, 30, noArena));
        // an Admin location: the starting admin's position is the centre
        var admin = Run(Def(location: LocationType.Admin), origin: (1000f, 0f, 1000f));
        var q = new Rig();
        q.W.People.Add(P(1, 1010, 1000));
        Assert.Null(q.Until(0, 30, admin));
        q.W.People[0] = P(1, 10, 0);                                                         // at (0, 0): far from the admin's spot
        Assert.Equal(50, q.Until(35, 60, admin));                                            // 20 s after the last presence at 30 s
        // an AroundPlayer run: each live wave's own centre
        var around = Run(Def(location: LocationType.AroundPlayer, arena: null));
        var s = new Rig();
        s.W.LiveCentres.Add((500, 500));
        s.W.People.Add(P(1, 505, 500));
        Assert.Null(s.Until(0, 30, around));
        s.W.LiveCentres[0] = (2000, 2000);
        Assert.Equal(50, s.Until(35, 60, around));
    }

    // ================= D8 Targets =================

    static Dictionary<(int, int), long> Hunt(World w, List<string>? info = null, params ActiveEvent[] active)
    {
        var phase = new HuntPhase();
        phase.Tick(active, w, (info ?? []).Add, _ => { });
        return phase.Targets(active[0].Id).ToDictionary(t => t.Key, t => t.Value);
    }

    [Fact]
    public void Pursue_fails_when_a_player_in_claimed_territory_or_PvP_combat_is_targeted()
    {
        var a = Run();
        a.Pursuing = true;
        var w = new World();
        w.Add(1, 0, 0);
        w.People.AddRange([P(10, 500, 0, territory: true), P(11, 600, 0, pvp: true), P(12, 700, 0, alive: false), P(13, 800, 0, online: false),
            P(20, 3000, 3000)]);                                                             // off every region: pursuit is map-wide
        Assert.Equal(20L, Hunt(w, null, a)[(1, 0)]);
        var j = Assert.Single(w.Jumps);                                                      // a hidden jump toward them
        Assert.True(Arena.Distance((3000, 3000), j.X, j.Z) < Arena.Distance((3000, 3000), 0, 0));
        w.People.Add(P(14, 900, 0));
        w.Claimed = (x, _) => x is > 850 and < 950;                                          // claimed by the event's own map
        Assert.Equal(20L, Hunt(w, null, a)[(1, 0)]);
        Assert.Equal([20L], Pursuit.SeedTargets([P(20, 3000, 3000), P(10, 3010, 3000, territory: true)], (2950, 3000), false, null));
        Assert.Empty(Pursuit.SeedTargets([P(20, 3000, 3000)], (2800, 3000), false, null));  // beyond the 100 m seed reach
    }

    [Fact]
    public void Pursue_fails_when_a_landing_is_in_claimed_territory()
    {
        var a = Run();
        a.Pursuing = true;
        var w = new World { Claimed = (x, _) => x > 3020 };                                  // the player stands just outside it
        w.Add(1, 0, 0);
        w.People.Add(P(20, 3000, 3000));
        var phase = new HuntPhase();
        for (var i = 0; i < 20; i++) phase.Tick([a], w, _ => { }, _ => { });
        Assert.NotEmpty(w.Jumps);
        Assert.All(w.Jumps, j => Assert.False(w.Claimed(j.X, j.Z), $"{j.X} {j.Z}"));
    }

    [Fact]
    public void Pursue_fails_when_a_revenge_run_targets_another_player_than_its_focus()
    {
        var a = Run(Def(revenge: true));
        var r = new Rig();
        r.W.Focus = 7;
        r.W.People.AddRange([P(8, 5, 0), P(7, 500, 0)]);                                     // another player in the area holds nothing
        Assert.Equal(20, r.Until(0, 30, a));
        var w = r.W;
        w.Add(1, 0, 0);
        Assert.Equal(7L, Hunt(w, null, a)[(1, 0)]);                                          // the nearer player 8 is never chased
        w.Focus = null;                                                                      // no focus: nobody
        Assert.Empty(Hunt(w, null, a));
        var held = Run(Def("rv2", revenge: true));
        var q = new Rig();
        q.W.Focus = 7;
        q.W.People.Add(P(7, 5, 0));
        Assert.Null(q.Until(0, 30, held));                                                   // the focus in the area holds it
    }

    [Fact]
    public void Pursue_fails_when_a_later_wave_does_not_pursue()
    {
        var a = Run();
        var w = new World();
        w.Add(1, 0, 0);
        w.People.Add(P(20, 3000, 3000));
        Assert.Empty(Hunt(w, null, a));                                                      // not yet pursuing: Hunt alone moves nothing
        a.Pursuing = true;
        Assert.True(HuntPhase.Hunted(a));
        Assert.True(RegionPhase.Needed([a]));
        w.Add(2, 100, 0, wave: 2);                                                           // a later wave spawns
        var targets = Hunt(w, null, a);
        Assert.Equal(20L, targets[(1, 0)]);
        Assert.Equal(20L, targets[(2, 0)]);
    }

    [Fact]
    public void Pursue_fails_when_a_group_line_is_missing_or_repeated()
    {
        var a = Run();
        a.Pursuing = true;
        var w = new World();
        w.Add(1, 0, 0);
        w.People.Add(P(20, 3000, 3000));
        var info = new List<string>();
        var phase = new HuntPhase();
        phase.Tick([a], w, info.Add, _ => { });
        phase.Tick([a], w, info.Add, _ => { });
        Assert.Equal(["event pv pursuit: group 1.0 on a player"], info.Where(l => l.Contains("pursuit", StringComparison.Ordinal)));
        w.People.Add(P(21, w.At[1].X, w.At[1].Z + 10));                                    // a nearer player: the target changes
        phase.Tick([a], w, info.Add, _ => { });
        Assert.Equal(2, info.Count(l => l == "event pv pursuit: group 1.0 on a player"));
    }

    // ================= D8 Lifted =================

    [Fact]
    public void Pursue_fails_when_the_arena_still_returns_a_pursuing_unit()
    {
        var a = Run();
        var stray = new ArenaUnit(1, "pv", T0, 80, 0, 0);
        Assert.Single(Arena.Check(a, [stray], (_, _) => 0f, () => 0.5)!.Returns);
        Assert.True(ArenaPhase.HasArena(a));
        a.Pursuing = true;
        Assert.Null(Arena.Check(a, [stray], (_, _) => 0f, () => 0.5));
        Assert.False(ArenaPhase.HasArena(a));
    }

    [Fact]
    public void Pursue_fails_when_a_unit_watch_landing_of_a_pursuing_unit_is_confined_to_the_arena()
    {
        static (WatchRig R, ActiveEvent A, long Key) Out(bool pursuing)
        {
            var (r, a) = Watch.Rig(Watch.Waves(arena: new Arena(20)));
            a.Pursuing = pursuing;
            var key = r.W.Add("uw", WatchRig.T0.AddSeconds(1), 500, 500);
            r.Now = WatchRig.T0.AddSeconds(5);
            Assert.True(r.Unstick(a.Id).Ok);
            r.W.Put(key, 400, 500);                                                          // pushed out of the arena, its anchor with it
            r.For(5);
            return (r, a, key);
        }
        Assert.Empty(Out(false).R.W.Moves);                                                  // confined: every anchor spot is outside
        var (q, _, key) = Out(true);
        var m = Assert.Single(q.W.Moves);
        Assert.Equal(key, m.Key);
        Assert.True(Arena.Distance((500, 500), m.X, m.Z) > 20f);
        // still never in claimed territory, whatever allowTerritory says
        var (t, ta) = Watch.Rig(Watch.Waves(arena: new Arena(20), allowTerritory: true));
        ta.Pursuing = true;
        t.W.Claimed = (_, _) => true;
        t.W.Add("uw", WatchRig.T0.AddSeconds(1), 500, 500);
        t.Now = WatchRig.T0.AddSeconds(5);
        Assert.True(t.Unstick(ta.Id).Ok);
        t.For(5);
        Assert.Empty(t.W.Moves);
    }

    [Fact]
    public void Pursue_fails_when_a_waiting_pursuer_is_stall_marked()
    {
        static WatchRig Quiet(bool pursuing)
        {
            var (r, a) = Watch.Rig(Watch.Waves(list: Watch.Two()));
            a.Record(WatchRig.T0, fought: true);
            a.WavesSpawned = 1;
            a.Pursuing = pursuing;
            for (var i = 0; i < 4; i++) r.W.Add(a.Id, WatchRig.T0.AddSeconds(1), 300 + 30 * i, 500);
            r.For(660);
            return r;
        }
        Assert.Equal(1, Quiet(false).Lines(" stall: "));
        var p = Quiet(true);
        Assert.Equal(0, p.Lines(" stall: "));
        Assert.Equal(0, p.Lines("(stall)"));
    }

    // ================= D8 Lines =================

    [Fact]
    public void Pursue_fails_when_a_line_carries_a_name_or_a_position()
    {
        Assert.Equal("event pv pursuit: on, no player within 30 m for 20 s", Pursuit.OnLine("pv", 30, 20));
        Assert.Equal("event pv pursuit: group 2.1 on a player", Pursuit.GroupLine("pv", 2, 1));
        Assert.Equal("pursuit pv: check failed: boom", Pursuit.FailedLine("pv", "boom"));
        // the builders take an event id, counts and a message; never a player
        var parameters = typeof(Pursuit).GetMethods().Where(m => m.Name.EndsWith("Line", StringComparison.Ordinal)).SelectMany(m => m.GetParameters()).ToList();
        Assert.All(parameters, p => Assert.True(p.ParameterType == typeof(string) || p.ParameterType == typeof(int), p.Name));
    }

    // ================= D9 EndPaths =================

    [Fact]
    public void EndPaths_fails_when_an_end_path_keeps_a_pursuit_record()
    {
        var r = new Rig();
        var a = Run();
        var b = Run(Def("pw"));
        r.W.People.Add(P(1, 5, 0));
        r.Tick(T0, a, b);
        Assert.Equal(2, r.Phase.AllRecords);
        r.Phase.EventEnded("pv");                                                            // stop, all waves defeated, natural end, fault
        Assert.Null(r.Phase.QuietSince("pv"));
        Assert.Equal(1, r.Phase.AllRecords);
        r.Tick(T0.AddSeconds(5));                                                            // b ended without a call: pruned at the next tick
        Assert.Equal(0, r.Phase.AllRecords);
        r.Tick(T0.AddSeconds(10), a, b);
        r.W.ThrowPlayers = true;
        r.Tick(T0.AddSeconds(15), a, b);
        Assert.NotEmpty(r.Phase.Health);
        r.Phase.Clear();                                                                     // purge, pillar off, restart
        Assert.Equal(0, r.Phase.AllRecords);
        Assert.Empty(r.Phase.Health);
    }

    [Fact]
    public void EndPaths_fails_when_a_jump_follows_an_end()
    {
        var a = Run();
        a.Pursuing = true;
        var w = new World();
        w.Add(1, 0, 0);
        w.People.Add(P(20, 3000, 3000));
        var phase = new HuntPhase();
        phase.Tick([a], w, _ => { }, _ => { });
        var jumps = w.Jumps.Count;
        Assert.NotEmpty(phase.Targets("pv"));
        phase.Tick([], w, _ => { }, _ => { });                                               // the run ended: nothing runs it
        Assert.Equal(jumps, w.Jumps.Count);
        Assert.Empty(phase.Targets("pv"));
    }

    [Fact]
    public void EndPaths_passes_stop_drops_the_clock_and_the_targets()
    {
        var r = new Rig();
        var a = Run();
        r.W.People.Add(P(1, 500, 0));
        r.Tick(T0, a);
        Assert.NotNull(r.Phase.QuietSince("pv"));
        r.Phase.EventEnded("pv");
        Assert.Equal(0, r.Phase.AllRecords);
        Assert.Empty(r.Tick(T0.AddSeconds(5)));
        Assert.Empty(r.Info);
    }

    [Fact]
    public void EndPaths_empty_no_run_no_record() => Assert.Equal(0, new Rig().Phase.AllRecords);

    [Fact]
    public void EndPaths_fails_when_a_resumed_boot_starts_pursuing()
    {
        var r = new Rig();
        var a = Run(trigger: "resume");
        r.W.People.Add(P(1, 500, 0));
        Assert.False(Pursuit.Configured(a));
        Assert.False(PursuitPhase.Needed([a]));
        Assert.Null(r.Until(0, 120, a));
        Assert.False(a.Pursuing);
        Assert.Equal(0, r.W.PlayerReads);
        Assert.False(Run(Def() with { Action = Def().Action! with { Pursuit = null } }).Pursuing);
        Assert.False(Pursuit.Configured(Run(Def() with { Action = Def().Action! with { Pursuit = null } })));
    }

    // ================= D12 CheckFailed =================

    [Fact]
    public void CheckFailed_fails_when_a_throwing_read_is_logged_twice_or_stops_another_events_check()
    {
        var r = new Rig();
        var a = Run();
        var b = Run(Def("pw"));
        r.W.ThrowFor = "pv";                                                                 // pv's territory read throws
        r.W.People.Add(P(1, 500, 0));
        for (var s = 0; s <= 30; s += 5) r.Tick(T0.AddSeconds(s), a, b);
        Assert.Equal(["pursuit pv: check failed: territory read failed"], r.Errors);         // once per streak
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        Assert.True(b.Pursuing);                                                             // the other event's check ran
        Assert.False(a.Pursuing);
        r.W.ThrowFor = null;                                                                 // a clean check ends the streak
        r.W.People[0] = P(1, 5, 0);                                                          // (in the area, so pv waits on)
        r.Tick(T0.AddSeconds(35), a);
        Assert.Empty(r.Phase.Health);
        r.W.ThrowFor = "pv";
        r.Tick(T0.AddSeconds(40), a);
        Assert.Equal(2, r.Errors.Count);                                                     // a new streak logs again
    }

    [Fact]
    public void CheckFailed_fails_when_a_throwing_player_read_escapes()
    {
        var r = new Rig();
        var a = Run();
        r.W.ThrowPlayers = true;
        var checks = r.Tick(T0, a);
        Assert.Empty(checks);
        Assert.Equal(["pursuit pv: check failed: player query failed"], r.Errors);
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        Assert.False(a.Pursuing);
    }

    [Fact]
    public void CheckFailed_passes_good_read_ends_the_streak()
    {
        var r = new Rig();
        var a = Run();
        r.W.People.Add(P(1, 5, 0));
        r.W.ThrowPlayers = true;
        r.Tick(T0, a);
        Assert.NotEmpty(r.Phase.Health);
        r.W.ThrowPlayers = false;
        Assert.Single(r.Tick(T0.AddSeconds(5), a));
        Assert.Empty(r.Phase.Health);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void CheckFailed_empty_no_pursuit_no_read()
    {
        var r = new Rig();
        var plain = Run(Def() with { Action = Def().Action! with { Pursuit = null } });
        Assert.Empty(r.Tick(T0, plain));
        Assert.Equal(0, r.W.PlayerReads);
        Assert.Empty(r.Phase.Health);
    }
}
