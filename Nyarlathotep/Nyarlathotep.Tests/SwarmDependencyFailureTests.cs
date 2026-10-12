using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D4: the arena check's dependency failure, the one category of tools/preflight-checks.json ›
/// dependencySuites.swarm (arena-read). A throwing position read or return write skips that unit and the rest of the
/// check runs; a failing check logs once per streak and holds "arena: check failing" until one of its event's checks runs
/// clean, the event ends or the server restarts (Review 2 F3); the phase never throws, so no later tick phase, despawn
/// batch or event end is skipped and the event's duration still ends it.</summary>
public class SwarmDependencyFailureTests
{
    static readonly DateTime T0 = new(2026, 10, 11, 20, 0, 0, DateTimeKind.Utc);

    static string ArenaEvent(string id) =>
        Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ], " +
            "\"waves\": 1, \"intervalSeconds\": 60, \"radius\": 8, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"arena\": { \"radius\": 25 } }");

    static EventEngine Started(params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        foreach (var d in catalog.Current.All)
            Assert.Null(engine.Start(d.Id, "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
        return engine;
    }

    /// <summary>The game side as the phase sees it: units at positions, with faults planted per unit or for the ledger.</summary>
    sealed class FakeWorld : IArenaWorld
    {
        public List<(TrackedUnit Unit, float X, float Z)> Placed { get; } = [];
        public HashSet<long> ReadThrows { get; } = [];
        public HashSet<long> WriteThrows { get; } = [];
        public bool LedgerThrows { get; set; }
        public List<long> Returned { get; } = [];
        public int Reads { get; private set; }

        public void Add(long key, string eventId, float x, float z) =>
            Placed.Add((new TrackedUnit(key, "CHAR_Bandit_Thug", eventId, T0.AddSeconds(1), 300, T0.AddMinutes(20)), x, z));

        public IEnumerable<TrackedUnit> Units => LedgerThrows ? throw new InvalidOperationException("ledger gone") : Placed.Select(p => p.Unit);

        public ArenaUnit? Read(TrackedUnit unit)
        {
            Reads++;
            if (ReadThrows.Contains(unit.Key)) throw new InvalidOperationException("translation gone");
            var p = Placed.First(q => q.Unit.Key == unit.Key);
            return new ArenaUnit(unit.Key, unit.EventId, unit.SpawnedUtc, p.X, 5f, p.Z);
        }

        public void Return(ArenaReturn r, (float X, float Z) centre, int arenaRadius, (float X, float Y, float Z)? anchor)
        {
            if (WriteThrows.Contains(r.Key)) throw new InvalidOperationException("write refused");
            Returned.Add(r.Key);
        }
    }

    static Func<double> Rng() => new Random(3).NextDouble;

    static IReadOnlyList<ArenaCheck> Tick(ArenaPhase phase, DateTime at, EventEngine engine, FakeWorld world, LogLines info, LogLines error) =>
        phase.Tick(at, engine.Active, world, Rng(), info.Add, error.Add);

    [Fact]
    public void ArenaRead_fails_when_one_throwing_unit_stops_the_others()
    {
        var engine = Started(ArenaEvent("sw"));
        var world = new FakeWorld();
        for (var k = 1; k <= 4; k++) world.Add(k, "sw", 0, 60);                   // all four stray
        world.ReadThrows.Add(1);
        world.WriteThrows.Add(2);
        var (info, error) = (new LogLines(), new LogLines());
        var checks = Tick(new ArenaPhase(), T0.AddSeconds(5), engine, world, info, error);
        Assert.Equal([3L, 4L], world.Returned);                                    // the others still returned
        Assert.Equal(["arena sw: returned 2, farthest 60 m"], info.Lines);          // the written ones only
        Assert.Equal(["arena sw: check failed: translation gone"], error.Lines);    // the first message, once
        Assert.Equal([3L, 4L], checks.Single().Returns.Select(r => r.Key));
    }

    [Fact]
    public void ArenaRead_fails_when_the_error_line_repeats_within_a_streak()
    {
        var engine = Started(ArenaEvent("sw"));
        var world = new FakeWorld { LedgerThrows = true };
        var phase = new ArenaPhase();
        var (info, error) = (new LogLines(), new LogLines());
        for (var i = 0; i < 4; i++) Tick(phase, T0.AddSeconds(5 * (i + 1)), engine, world, info, error);
        Assert.Equal(["arena sw: check failed: ledger gone"], error.Lines);         // once in a streak of four
        world.LedgerThrows = false;
        Tick(phase, T0.AddSeconds(25), engine, world, info, error);                // a clean check ends it
        world.LedgerThrows = true;
        Tick(phase, T0.AddSeconds(30), engine, world, info, error);
        Assert.Equal(2, error.Count("check failed"));                              // a new streak logs again
    }

    [Fact]
    public void ArenaRead_fails_when_the_entry_outlives_a_good_check()
    {
        var engine = Started(ArenaEvent("sw"));
        var world = new FakeWorld();
        world.Add(1, "sw", 0, 60);
        world.WriteThrows.Add(1);
        var phase = new ArenaPhase();
        var (info, error) = (new LogLines(), new LogLines());
        Tick(phase, T0.AddSeconds(5), engine, world, info, error);
        Assert.Equal([ArenaPhase.Failing], phase.Health);
        Tick(phase, T0.AddSeconds(7), engine, world, info, error);                 // inside the 5 s interval: no check, held
        Assert.Equal([ArenaPhase.Failing], phase.Health);
        world.WriteThrows.Clear();
        Tick(phase, T0.AddSeconds(10), engine, world, info, error);
        Assert.Empty(phase.Health);                                                // the good check clears it

        // Review 2 F3: the entry also clears at its event's end and at a restart.
        world.WriteThrows.Add(1);
        Tick(phase, T0.AddSeconds(15), engine, world, info, error);
        Assert.Equal([ArenaPhase.Failing], phase.Health);
        phase.EventEnded("sw");
        Assert.Empty(phase.Health);
        Tick(phase, T0.AddSeconds(20), engine, world, info, error);
        Assert.Equal([ArenaPhase.Failing], phase.Health);
        phase.Clear();                                                             // SpawnTracker.Initialize at a restart
        Assert.Empty(phase.Health);
        Tick(phase, T0.AddSeconds(20), engine, world, info, error);                // the next tick checks again at once
        Assert.Equal([ArenaPhase.Failing], phase.Health);
        engine.Cancel("sw");                                                       // an end path that did not call EventEnded
        Tick(phase, T0.AddSeconds(21), engine, world, info, error);
        Assert.Empty(phase.Health);                                                // no streak outlives its running instance
    }

    [Fact]
    public void ArenaRead_fails_when_a_throw_skips_a_later_phase_the_despawn_or_the_end()
    {
        var engine = Started(ArenaEvent("sw"), ArenaEvent("sw2"));
        var world = new FakeWorld();
        world.Add(1, "sw", 0, 60);
        world.Add(2, "sw2", 0, 60);
        world.ReadThrows.Add(1);
        var ledger = new SpawnLedger(new LedgerLimits(150, 20, 10, 5));
        ledger.Request("CHAR_Bandit_Thug", "sw", 1, new UnitLifetime(DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f));
        Assert.True(ledger.Confirm(ledger.TakeSpawns().Single(), 1, T0));
        var phase = new ArenaPhase();
        var (info, error) = (new LogLines(), new LogLines());

        // The phase never throws, so EventScheduler's later phases run; the events phase still ends the event at its
        // duration and the spawn phase still despawns its units.
        Assert.Null(Record.Exception(() => Tick(phase, T0.AddSeconds(5), engine, world, info, error)));
        Assert.Equal([2L], world.Returned);                                        // the other event's check ran
        world.LedgerThrows = true;
        Assert.Null(Record.Exception(() => Tick(phase, T0.AddSeconds(600), engine, world, info, error)));
        Assert.Equal(["sw", "sw2"], engine.Expire(T0.AddSeconds(601), 30).Select(e => e.Id).OrderBy(x => x));   // the duration still ends both
        ledger.EndEvent("sw", DateTime.MaxValue, true);
        Assert.Equal([1L], ledger.TakeDespawns());                                 // and the despawn batch runs
        Assert.Empty(Tick(phase, T0.AddSeconds(610), engine, world, info, error));  // no arena check for an ended event
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void ArenaRead_passes_a_clean_check_returns_and_clears()
    {
        var engine = Started(ArenaEvent("sw"));
        var world = new FakeWorld();
        world.Add(1, "sw", 0, 60);
        world.Add(2, "sw", 0, 10);                                                 // inside: left alone
        world.Add(3, "other", 0, 90);                                              // another event's: never moved
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new ArenaPhase();
        Tick(phase, T0.AddSeconds(5), engine, world, info, error);
        Assert.Equal([1L], world.Returned);
        Assert.Equal(2, world.Reads);                                              // only the instance's own units are read
        Assert.Equal(["arena sw: returned 1, farthest 60 m"], info.Lines);
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }

    [Fact]
    public void ArenaRead_empty_no_arena_event()
    {
        var engine = Started(Json.Event("raid"));
        var world = new FakeWorld();
        world.Add(1, "raid", 0, 900);
        var (info, error) = (new LogLines(), new LogLines());
        var phase = new ArenaPhase();
        Assert.Empty(Tick(phase, T0.AddSeconds(5), engine, world, info, error));
        Assert.Equal(0, world.Reads);
        Assert.Empty(world.Returned);
        Assert.Empty(info.Lines);
        Assert.Empty(error.Lines);
        Assert.Empty(phase.Health);
    }
}
