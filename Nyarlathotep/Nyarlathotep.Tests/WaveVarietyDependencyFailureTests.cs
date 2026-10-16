using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D12: the pursuit check's dependency failure, the one category of tools/preflight-checks.json ›
/// dependencySuites.wave-variety (pursuit-read). The check runs inside the region phase (RegionPhase.Tick: pursuit, then
/// the lock, then the hunt; Services/SpawnTracker.RegionTick prefixes its error lines with "[nyar] "). A throwing player
/// read, territory read of a player's position or wave-centre read fails that event's check alone: it logs "pursuit
/// &lt;id&gt;: check failed: &lt;message&gt;" once per streak, holds "pursuit: check failing" on the health line
/// (HealthMonitor reads SpawnTracker.RegionHealth, the region phase's) until a check of that event runs clean, the event
/// ends or the server restarts, and starts no pursuit; another event's check, the lock and the hunt still run and the phase
/// never throws. The runs stand at a Point (0, 0) with arena 30, Hunt 30 and pursuit after 20 s, "pa" around a player.</summary>
public class WaveVarietyDependencyFailureTests
{
    static readonly DateTime T0 = new(2026, 10, 16, 20, 0, 0, DateTimeKind.Utc);

    static EventDefinition Def(string id, LocationType location = LocationType.Point) =>
        new(id, "x", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 600,
            new SpawnWavesAction([new UnitEntry("CHAR_Spider_Forest", 5)], 3, 60, 10, new Location(location, 0, 0), null) with
            {
                Behaviour = new Behaviour(BehaviourType.Hunt, 30),
                Arena = location == LocationType.Point ? new Arena(30) : null,
                Pursuit = new Pursuit(20),
            },
            Announce.None);

    static ActiveEvent Run(string id, LocationType location = LocationType.Point, bool pursuing = false)
    {
        var a = new ActiveEvent(new RunningInstance(Def(id, location), T0, T0.AddMinutes(10)), "manual", null);
        a.Record(T0, fought: true);
        a.Pursuing = pursuing;
        return a;
    }

    /// <summary>The game side as the region phase sees it, with the pursuit check's reads faulted: the player read as a
    /// whole, the territory read of a player's position or the wave-centre read per event.</summary>
    sealed class FaultyWorld : IRegionWorld
    {
        public List<HuntCandidate> People { get; } = [];
        public Dictionary<long, RegionUnit> At { get; } = [];
        public List<TrackedUnit> Tracked { get; } = [];
        public List<RegionJump> Jumps { get; } = [];
        public bool ThrowPlayers { get; set; }
        public string? ThrowClaimedFor { get; set; }
        public string? ThrowCentresFor { get; set; }
        public int PlayerReads { get; private set; }
        public WalkBudget Budget { get; } = new(1_000_000);

        public void Add(long key, string eventId, float x, float z)
        {
            Tracked.Add(new TrackedUnit(key, "CHAR_Spider_Forest", eventId, T0.AddSeconds(5), 600, T0.AddMinutes(10), 1));
            At[key] = new RegionUnit(key, eventId, T0.AddSeconds(5), x, 20f, z, 10, 1, 0);
        }

        public IReadOnlyList<HuntCandidate> Players()
        {
            PlayerReads++;
            return ThrowPlayers ? throw new InvalidOperationException("player query failed") : People;
        }

        public bool IsClaimed(string eventId, float x, float z) =>
            ThrowClaimedFor == eventId ? throw new InvalidOperationException("territory read failed") : false;

        public IReadOnlyList<(float X, float Z)> Centres(ActiveEvent a) =>
            ThrowCentresFor == a.Id ? throw new InvalidOperationException("position read failed") : [(0f, 0f)];

        public long? FocusKey(ActiveEvent a) => null;
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
        public RegionPhase Phase { get; } = new();
        public FaultyWorld W { get; } = new();
        public LogLines Info { get; } = new();
        public LogLines Errors { get; } = new();

        public void Tick(int seconds, params ActiveEvent[] active) =>
            Phase.Tick(T0.AddSeconds(seconds), active, W, new Random(3).NextDouble, Info.Add, Errors.Add);
    }

    [Fact]
    public void PursuitRead_fails_when_a_throwing_territory_read_is_logged_twice_or_stops_another_events_check()
    {
        var r = new Rig();
        var (pv, pw) = (Run("pv"), Run("pw"));
        r.W.People.Add(P(1, 500, 0));                                                        // outside both areas
        r.W.ThrowClaimedFor = "pv";
        for (var s = 0; s <= 30; s += 5) r.Tick(s, pv, pw);
        Assert.Equal(["pursuit pv: check failed: territory read failed"], r.Errors.Lines);    // once in a streak
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        Assert.False(pv.Pursuing);                                                           // its check skipped, no pursuit
        Assert.True(pw.Pursuing);                                                            // the other event's check ran
        Assert.Equal(1, r.Info.Count("event pw pursuit: on"));
    }

    [Fact]
    public void PursuitRead_fails_when_a_throwing_position_read_escapes_or_stops_the_hunt()
    {
        var r = new Rig();
        var pa = Run("pa", LocationType.AroundPlayer);
        var pw = Run("pw", pursuing: true);                                                  // another run, already pursuing
        r.W.Add(2, "pw", 0, 0);
        r.W.People.Add(P(20, 3000, 3000));
        r.W.ThrowCentresFor = "pa";
        r.Tick(0, pa, pw);
        r.Tick(5, pa, pw);
        Assert.Equal(["pursuit pa: check failed: position read failed"], r.Errors.Lines);
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        Assert.NotEmpty(r.W.Jumps);                                                          // the hunt phase still ran
        Assert.Contains(r.Phase.Hunt.Targets("pw"), t => t.Value == 20L);
        Assert.Equal(1, r.Info.Count("event pw pursuit: group"));
    }

    [Fact]
    public void PursuitRead_fails_when_a_throwing_player_read_escapes_or_logs_twice()
    {
        var r = new Rig();
        var pv = Run("pv");
        r.W.ThrowPlayers = true;
        for (var s = 0; s <= 30; s += 5) r.Tick(s, pv);                                      // a throw escaping would fail the test here
        Assert.Equal(["pursuit pv: check failed: player query failed"], r.Errors.Lines);
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        Assert.False(pv.Pursuing);
    }

    [Fact]
    public void PursuitRead_fails_when_the_health_entry_outlives_a_clean_check_or_the_end()
    {
        var r = new Rig();
        var pv = Run("pv");
        r.W.People.Add(P(1, 5, 0));                                                          // in the area: pv waits on
        r.W.ThrowPlayers = true;
        r.Tick(0, pv);
        Assert.Equal([PursuitPhase.Failing], r.Phase.Health);
        r.W.ThrowPlayers = false;
        r.Tick(5, pv);                                                                       // a clean check ends the streak
        Assert.Empty(r.Phase.Health);
        r.W.ThrowPlayers = true;
        r.Tick(10, pv);
        Assert.Equal(2, r.Errors.Count("check failed"));                                     // a new streak logs again
        r.Phase.EventEnded("pv");                                                            // the event ends
        Assert.Empty(r.Phase.Health);
        r.Tick(15, pv);
        r.Phase.Clear();                                                                     // a restart or purge
        Assert.Empty(r.Phase.Health);
    }

    [Fact]
    public void PursuitRead_fails_when_the_health_line_does_not_read_the_region_phase()
    {
        static string Service(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
            return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", file)).Replace("\r\n", "\n");
        }
        var health = System.Text.RegularExpressions.Regex.Replace(Service("HealthMonitor.cs"), @"//[^\n]*", "");
        Assert.Contains(".Concat(SpawnTracker.RegionHealth)", health);
        Assert.Contains("internal static IReadOnlyList<string> RegionHealth => _region.Health;", Service("SpawnTracker.cs"));
        Assert.Contains("line => Core.Log.LogError($\"[nyar] {line}\")", EndPathTests.Body(Service("SpawnTracker.cs"), "internal static void RegionTick(DateTime now)"));
    }

    [Fact]
    public void PursuitRead_passes_good_read_ends_the_streak()
    {
        var r = new Rig();
        var pv = Run("pv");
        r.W.People.Add(P(1, 500, 0));
        r.W.ThrowPlayers = true;
        r.Tick(0, pv);
        r.W.ThrowPlayers = false;
        for (var s = 5; s <= 25; s += 5) r.Tick(s, pv);
        Assert.Empty(r.Phase.Health);
        Assert.Single(r.Errors.Lines);
        Assert.True(pv.Pursuing);                                                            // 20 s from the first decision
    }

    [Fact]
    public void PursuitRead_empty_no_pursuit_no_read()
    {
        var r = new Rig();
        var plain = new ActiveEvent(new RunningInstance(Def("pv") with { Action = Def("pv").Action! with { Pursuit = null, Arena = null } },
            T0, T0.AddMinutes(10)), "manual", null);
        plain.Record(T0, fought: true);
        r.Tick(0, plain);
        r.Tick(5, plain);
        Assert.Equal(0, r.W.PlayerReads);
        Assert.Empty(r.Phase.Health);
        Assert.Empty(r.Errors.Lines);
    }

    static HuntCandidate P(long key, float x, float z) => new(key, x, z, true, true, false, false);
}
