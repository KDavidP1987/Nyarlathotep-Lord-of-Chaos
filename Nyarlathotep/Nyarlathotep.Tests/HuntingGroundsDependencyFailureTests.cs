using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D6 and D9: the region lock's and the region hunt's dependency failures, the two categories of
/// tools/preflight-checks.json › dependencySuites.hunting-grounds (region-lock, region-hunt). A throwing region lookup,
/// position read, landing check, return or jump write skips that unit and the rest of the check runs; a throwing player
/// read skips that event's whole hunt check, moving and seeding nothing; a failing check logs once per streak and holds
/// "region lock: check failing" or "region hunt: move failing" until a check of that event runs clean, the event ends or
/// the server restarts; the phase never throws, so no later tick phase, despawn batch or event end is skipped and the
/// event's duration still ends it. The map: FarbaneWoods covers x and z 0-1000, everything else lies in no scope region.</summary>
public class HuntingGroundsDependencyFailureTests
{
    static readonly DateTime T0 = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    static string MapOf(float x, float z) => x is >= 0 and <= 1000 && z is >= 0 and <= 1000 ? "FarbaneWoods" : "None";

    /// <summary>A Region-location event over FarbaneWoods with the lock, and RegionHunt when <paramref name="hunt"/>.</summary>
    static string RegionEvent(string id, bool hunt = true) =>
        Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ], " +
            "\"waves\": 1, \"intervalSeconds\": 60, \"radius\": 8, \"location\": { \"type\": \"Region\" }, \"scope\": [ \"FarbaneWoods\" ], " +
            "\"regionLock\": true" + (hunt ? ", \"behaviour\": { \"type\": \"RegionHunt\" }" : "") + " }");

    static EventEngine Started(params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        foreach (var d in catalog.Current.All)
            Assert.Null(engine.Start(d.Id, "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3, RegionOf: MapOf)));
        return engine;
    }

    /// <summary>The game side as the phases see it: units at positions and the players, with faults planted per unit, for
    /// the ledger or for the player read.</summary>
    sealed class FakeWorld : IRegionWorld
    {
        readonly Dictionary<long, RegionUnit> _at = [];
        readonly List<TrackedUnit> _tracked = [];
        public List<HuntCandidate> People { get; } = [];
        public HashSet<long> ReadThrows { get; } = [];
        public HashSet<long> WriteThrows { get; } = [];
        public bool LedgerThrows { get; set; }
        public bool PlayersThrow { get; set; }
        public List<long> Returned { get; } = [];
        public List<long> Jumped { get; } = [];
        public int Reads { get; private set; }
        public int PlayerReads { get; private set; }
        public WalkBudget Budget { get; } = new(1_000_000);
        public long? FocusKey(ActiveEvent a) => null;
        public IReadOnlyList<(float X, float Z)> Centres(ActiveEvent a) => [];

        /// <summary>A unit of <paramref name="eventId"/> at x, z, spawned at the centre of FarbaneWoods.</summary>
        public void Add(long key, string eventId, float x, float z, int group = 0)
        {
            var at = T0.AddSeconds(5);
            _tracked.Add(new TrackedUnit(key, "CHAR_Bandit_Thug", eventId, at, 600, at.AddMinutes(10), 1));
            _at[key] = new RegionUnit(key, eventId, at, x, 20f, z, 10, 1, group, (500f, 20f, 500f));
        }

        public IEnumerable<TrackedUnit> Units => LedgerThrows ? throw new InvalidOperationException("ledger gone") : _tracked;

        public RegionUnit? Read(TrackedUnit unit)
        {
            Reads++;
            if (ReadThrows.Contains(unit.Key)) throw new InvalidOperationException("translation gone");
            return _at.TryGetValue(unit.Key, out var u) ? u : null;
        }

        public string RegionOf(float x, float z) => MapOf(x, z);
        public bool IsClaimed(string eventId, float x, float z) => false;
        public bool Walkable(float x, float z, byte level) => true;
        public float HeightOf(byte level) => level * 2f;

        public IReadOnlyList<HuntCandidate> Players()
        {
            PlayerReads++;
            return PlayersThrow ? throw new InvalidOperationException("player query failed") : People;
        }

        public void Return(RegionReturn r, Scope scope)
        {
            if (WriteThrows.Contains(r.Key)) throw new InvalidOperationException("write refused");
            Returned.Add(r.Key);
            _at[r.Key] = _at[r.Key] with { X = r.X, Y = r.Y, Z = r.Z };
        }

        public void Jump(RegionJump j)
        {
            if (WriteThrows.Contains(j.Key)) throw new InvalidOperationException("write refused");
            Jumped.Add(j.Key);
            _at[j.Key] = _at[j.Key] with { X = j.X, Y = j.Y, Z = j.Z, Level = j.Level };
        }
    }

    static Func<double> Rng() => new Random(3).NextDouble;

    static HuntCandidate Far() => new(901, 900, 500, true, true, false, false);   // inside FarbaneWoods, 800 m from the units

    static IReadOnlyList<LockCheck> Lock(LockPhase phase, EventEngine engine, FakeWorld world, LogLines info, LogLines error) =>
        phase.Tick(T0, engine.Active, world, Rng(), info.Add, error.Add);

    static IReadOnlyList<HuntCheck> Hunt(HuntPhase phase, EventEngine engine, FakeWorld world, LogLines info, LogLines error) =>
        phase.Tick(engine.Active, world, info.Add, error.Add);

    /// <summary>Code review F5 as swarm's: the running list itself throws.</summary>
    sealed class ThrowingActive(IReadOnlyCollection<ActiveEvent> inner) : IReadOnlyCollection<ActiveEvent>
    {
        public bool Throws { get; set; } = true;
        public int Count => inner.Count;
        public IEnumerator<ActiveEvent> GetEnumerator() => Throws ? throw new InvalidOperationException("engine gone") : inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // ---- D6 Lock

    [Fact]
    public void Lock_fails_when_one_throwing_unit_stops_the_others()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld();
        for (var k = 1; k <= 4; k++) world.Add(k, "hg", -60, 500);                 // all four stray, 60 m outside
        world.ReadThrows.Add(1);
        world.WriteThrows.Add(2);
        var (info, error) = (new LogLines(), new LogLines());
        var checks = Lock(new LockPhase(), engine, world, info, error);
        Assert.Equal([3L, 4L], world.Returned);                                    // the others still returned
        Assert.Single(info.Lines);
        Assert.StartsWith("region lock hg: returned 2", info.Lines[0]);            // the written ones only
        Assert.Equal(["region lock hg: check failed: translation gone"], error.Lines);   // the first message, once
        Assert.Equal([3L, 4L], checks.Single().Returns.Select(r => r.Key));
    }

    [Fact]
    public void Lock_fails_when_the_error_line_repeats_within_a_streak()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld { LedgerThrows = true };
        var phase = new LockPhase();
        var (info, error) = (new LogLines(), new LogLines());
        for (var i = 0; i < 4; i++) Lock(phase, engine, world, info, error);
        Assert.Equal(["region lock hg: check failed: ledger gone"], error.Lines);   // once in a streak of four
        world.LedgerThrows = false;
        Lock(phase, engine, world, info, error);                                   // a clean check ends it
        world.LedgerThrows = true;
        Lock(phase, engine, world, info, error);
        Assert.Equal(2, error.Count("check failed"));                              // a new streak logs again
    }

    [Fact]
    public void Lock_fails_when_the_entry_outlives_a_clean_check_or_the_end()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld();
        world.Add(1, "hg", -60, 500);
        world.WriteThrows.Add(1);
        var phase = new LockPhase();
        var (info, error) = (new LogLines(), new LogLines());
        Lock(phase, engine, world, info, error);
        Assert.Equal([LockPhase.Failing], phase.Health);
        phase.Tick(T0, engine.Active, world, Rng(), info.Add, error.Add, due: false);   // no check due: held
        Assert.Equal([LockPhase.Failing], phase.Health);
        world.WriteThrows.Clear();
        Lock(phase, engine, world, info, error);
        Assert.Empty(phase.Health);                                                // the clean check clears it

        world.WriteThrows.Add(2);
        world.Add(2, "hg", -60, 400);                                              // a new stray whose write fails
        Lock(phase, engine, world, info, error);
        Assert.Equal([LockPhase.Failing], phase.Health);
        phase.EventEnded("hg");                                                    // every end path (D12)
        Assert.Empty(phase.Health);
        Lock(phase, engine, world, info, error);
        Assert.Equal([LockPhase.Failing], phase.Health);
        phase.Clear();                                                             // SpawnTracker.Initialize at a restart
        Assert.Empty(phase.Health);
        Lock(phase, engine, world, info, error);
        Assert.Equal([LockPhase.Failing], phase.Health);
        engine.Cancel("hg");                                                       // an end path that did not call EventEnded
        Lock(phase, engine, world, info, error);
        Assert.Empty(phase.Health);                                                // no streak outlives its running instance
    }

    [Fact]
    public void Lock_fails_when_a_throw_skips_a_later_phase_the_despawn_or_the_end()
    {
        var engine = Started(RegionEvent("hg", hunt: false), RegionEvent("hg2", hunt: false));
        var world = new FakeWorld();
        world.Add(1, "hg", -60, 500);
        world.Add(2, "hg2", -60, 500);
        world.ReadThrows.Add(1);
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 5));
        ledger.Request("CHAR_Bandit_Thug", "hg", 1, new UnitLifetime(DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f));
        Assert.True(ledger.Confirm(ledger.TakeSpawns().Single(), 1, T0));
        var phase = new RegionPhase();
        var (info, error) = (new LogLines(), new LogLines());

        // The phase never throws, so EventScheduler's later phases run; the events phase still ends the event at its
        // duration and the spawn phase still despawns its units.
        Assert.Null(Record.Exception(() => phase.Tick(T0.AddSeconds(5), engine.Active, world, Rng(), info.Add, error.Add)));
        Assert.Equal([2L], world.Returned);                                        // the other event's check ran
        world.LedgerThrows = true;
        Assert.Null(Record.Exception(() => phase.Tick(T0.AddSeconds(600), engine.Active, world, Rng(), info.Add, error.Add)));
        Assert.Equal(["hg", "hg2"], engine.Expire(T0.AddSeconds(601), 30).Select(e => e.Id).OrderBy(x => x));   // the duration ends both
        ledger.EndEvent("hg", DateTime.MaxValue, true);
        Assert.Equal([1L], ledger.TakeDespawns());                                 // and the despawn batch runs
        var (locks, hunts) = phase.Tick(T0.AddSeconds(610), engine.Active, world, Rng(), info.Add, error.Add);
        Assert.Empty(locks);                                                       // no check for an ended event
        Assert.Empty(hunts);
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Lock_fails_when_a_phase_fault_is_silent()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld();
        world.Add(1, "hg", -60, 500);
        var active = new ThrowingActive(engine.Active);
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new LockPhase();
        Assert.Empty(phase.Tick(T0, active, world, Rng(), info.Add, error.Add));
        Assert.Empty(phase.Tick(T0, active, world, Rng(), info.Add, error.Add));
        Assert.Equal(["region lock phase: check failed: engine gone"], error.Lines);   // once per streak
        Assert.Equal([LockPhase.Failing], phase.Health);
        active.Throws = false;
        Assert.Single(phase.Tick(T0, active, world, Rng(), info.Add, error.Add));
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Lock_passes_a_clean_check_returns_and_clears()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld();
        world.Add(1, "hg", -60, 500);
        world.Add(2, "hg", 300, 300);                                              // inside: left alone
        world.Add(3, "other", -90, 500);                                           // another event's: never moved
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new LockPhase();
        Lock(phase, engine, world, info, error);
        Assert.Equal([1L], world.Returned);
        Assert.Equal(2, world.Reads);                                              // only the instance's own units are read
        Assert.Single(info.Lines);
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Lock_empty_no_locked_event()
    {
        var engine = Started(Json.Event("raid"));
        var world = new FakeWorld();
        world.Add(1, "raid", -900, 500);
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new LockPhase();
        Assert.Empty(Lock(phase, engine, world, info, error));
        Assert.Equal(0, world.Reads);
        Assert.Empty(world.Returned);
        Assert.Empty(info.Lines);
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }

    // ---- D9 Hunt

    [Fact]
    public void Hunt_fails_when_a_failed_player_read_moves_or_seeds_a_unit()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.People.Add(Far());
        var phase = new HuntPhase();
        var (info, error) = (new LogLines(), new LogLines());
        Hunt(phase, engine, world, info, error);
        Assert.Equal([1L], world.Jumped);                                          // a clean check jumps and keeps a target
        Assert.NotEmpty(phase.Targets("hg"));

        world.PlayersThrow = true;
        var reads = world.Reads;
        var checks = Hunt(phase, engine, world, info, error);
        Assert.Empty(checks);                                                      // the whole check skipped
        Assert.Equal([1L], world.Jumped);                                          // nothing moved
        Assert.Empty(phase.Targets("hg"));                                         // and no target left to seed from
        Assert.Equal(reads, world.Reads);                                          // no unit read after the failed player read
        Assert.Equal(["region hunt hg: move failed: player query failed"], error.Lines);
        Assert.Equal([HuntPhase.Failing], phase.Health);
    }

    [Fact]
    public void Hunt_fails_when_one_throwing_unit_stops_the_others()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.Add(2, "hg", 100, 510);
        world.Add(3, "hg", 100, 520);
        world.ReadThrows.Add(1);
        world.WriteThrows.Add(2);
        world.People.Add(Far());
        var (info, error) = (new LogLines(), new LogLines());
        var checks = Hunt(new HuntPhase(), engine, world, info, error);
        Assert.Equal([3L], world.Jumped);                                          // the others still jumped
        Assert.Equal([3L], checks.Single().Jumps.Select(j => j.Key));
        Assert.Single(info.Lines);
        Assert.StartsWith("region hunt hg: 1 jumped", info.Lines[0]);
        Assert.Equal(["region hunt hg: move failed: translation gone"], error.Lines);   // the first message, once
    }

    [Fact]
    public void Hunt_fails_when_the_error_line_repeats_within_a_streak()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld { PlayersThrow = true };
        world.Add(1, "hg", 100, 500);
        var phase = new HuntPhase();
        var (info, error) = (new LogLines(), new LogLines());
        for (var i = 0; i < 4; i++) Hunt(phase, engine, world, info, error);
        Assert.Equal(["region hunt hg: move failed: player query failed"], error.Lines);   // once in a streak of four
        world.PlayersThrow = false;
        Hunt(phase, engine, world, info, error);                                   // a clean check ends it
        world.PlayersThrow = true;
        Hunt(phase, engine, world, info, error);
        Assert.Equal(2, error.Count("move failed"));                               // a new streak logs again
    }

    [Fact]
    public void Hunt_fails_when_the_entry_outlives_a_clean_check_or_the_end()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld { PlayersThrow = true };
        world.Add(1, "hg", 100, 500);
        var phase = new HuntPhase();
        var (info, error) = (new LogLines(), new LogLines());
        Hunt(phase, engine, world, info, error);
        Assert.Equal([HuntPhase.Failing], phase.Health);
        phase.Tick(engine.Active, world, info.Add, error.Add, due: false);         // no check due: held
        Assert.Equal([HuntPhase.Failing], phase.Health);
        world.PlayersThrow = false;
        Hunt(phase, engine, world, info, error);
        Assert.Empty(phase.Health);                                                // the clean check clears it

        world.PlayersThrow = true;
        Hunt(phase, engine, world, info, error);
        Assert.Equal([HuntPhase.Failing], phase.Health);
        phase.EventEnded("hg");                                                    // every end path (D12)
        Assert.Empty(phase.Health);
        Hunt(phase, engine, world, info, error);
        Assert.Equal([HuntPhase.Failing], phase.Health);
        phase.Clear();                                                             // SpawnTracker.Initialize at a restart
        Assert.Empty(phase.Health);
        Hunt(phase, engine, world, info, error);
        Assert.Equal([HuntPhase.Failing], phase.Health);
        engine.Cancel("hg");                                                       // an end path that did not call EventEnded
        Hunt(phase, engine, world, info, error);
        Assert.Empty(phase.Health);                                                // no streak outlives its running instance
    }

    [Fact]
    public void Hunt_fails_when_a_throw_skips_a_later_phase_the_despawn_or_the_end()
    {
        var engine = Started(RegionEvent("hg"), RegionEvent("hg2"));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.Add(2, "hg2", 100, 600);
        world.WriteThrows.Add(1);
        world.People.Add(Far());
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 5));
        ledger.Request("CHAR_Bandit_Thug", "hg", 1, new UnitLifetime(DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f));
        Assert.True(ledger.Confirm(ledger.TakeSpawns().Single(), 1, T0));
        var phase = new RegionPhase();
        var (info, error) = (new LogLines(), new LogLines());

        Assert.Null(Record.Exception(() => phase.Tick(T0.AddSeconds(5), engine.Active, world, Rng(), info.Add, error.Add)));
        Assert.Equal([2L], world.Jumped);                                          // the other event's hunt ran
        Assert.Equal([HuntPhase.Failing], phase.Health);
        world.PlayersThrow = true;
        Assert.Null(Record.Exception(() => phase.Tick(T0.AddSeconds(600), engine.Active, world, Rng(), info.Add, error.Add)));
        Assert.Equal(["hg", "hg2"], engine.Expire(T0.AddSeconds(601), 30).Select(e => e.Id).OrderBy(x => x));   // the duration ends both
        ledger.EndEvent("hg", DateTime.MaxValue, true);
        Assert.Equal([1L], ledger.TakeDespawns());                                 // and the despawn batch runs
        var (locks, hunts) = phase.Tick(T0.AddSeconds(610), engine.Active, world, Rng(), info.Add, error.Add);
        Assert.Empty(locks);
        Assert.Empty(hunts);                                                       // no hunt for an ended event
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Hunt_fails_when_a_phase_fault_is_silent()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.People.Add(Far());
        var active = new ThrowingActive(engine.Active);
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new HuntPhase();
        Assert.Empty(phase.Tick(active, world, info.Add, error.Add));
        Assert.Empty(phase.Tick(active, world, info.Add, error.Add));
        Assert.Equal(["region hunt phase: move failed: engine gone"], error.Lines);   // once per streak
        Assert.Equal([HuntPhase.Failing], phase.Health);
        active.Throws = false;
        Assert.Single(phase.Tick(active, world, info.Add, error.Add));
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Hunt_passes_a_clean_check_jumps_and_clears()
    {
        var engine = Started(RegionEvent("hg"));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.Add(2, "other", 100, 500);                                           // another event's: never moved
        world.People.Add(Far());
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new HuntPhase();
        var checks = Hunt(phase, engine, world, info, error);
        Assert.Equal([1L], world.Jumped);
        Assert.Equal(1, world.Reads);                                              // only the instance's own units are read
        Assert.Equal(1, world.PlayerReads);                                        // the players once for the check
        Assert.Single(checks);
        Assert.Single(info.Lines);
        Assert.DoesNotContain("901", info.Lines[0]);                               // no player key in the line (D13)
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void Hunt_empty_no_region_hunt_event()
    {
        var engine = Started(RegionEvent("hg", hunt: false));
        var world = new FakeWorld();
        world.Add(1, "hg", 100, 500);
        world.People.Add(Far());
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new HuntPhase();
        Assert.Empty(Hunt(phase, engine, world, info, error));
        Assert.Equal(0, world.PlayerReads);
        Assert.Equal(0, world.Reads);
        Assert.Empty(world.Jumped);
        Assert.Empty(info.Lines);
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }
}
