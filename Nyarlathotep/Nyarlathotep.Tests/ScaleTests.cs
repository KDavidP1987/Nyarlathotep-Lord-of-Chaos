using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D18: the per-unit passes are bounded per tick. The round robin takes at most 500 a pass and visits
/// every one of 1000 units within two 5 s passes; the apocalypse host's hops and roam go through it with no search inside
/// the per-unit loop, so a step's work grows linearly from 100 to 1000 units; the unit watch marks at most 500 stalled
/// units a tick and the rest on the next.</summary>
public class ScaleTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    static IEnumerable<HotspotNative> Cluster(float x, float z, int n) =>
        Enumerable.Range(0, n).Select(i => new HotspotNative(new UnitFacts(Apo.Zombie, "Faction_Undead", false, false, false, false, false, null,
            () => "FarbaneWoods"), x + i, z + i));

    /// <summary>A running apocalypse with <paramref name="hordes"/> hordes of <paramref name="size"/> risen and spawned (one
    /// every 5 min from minute 1), the ledger sized for 1000 units and 500 a wave.</summary>
    static (ApoGame Game, ApocalypseHost Host, Func<DateTime, ApocalypseStep> Tick, DateTime Now) Hordes(int hordes, int size)
    {
        var files = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(files, log.Add), () => files.Now, log.Add);
        var game = new ApoGame
        {
            Ledger = new SpawnLedger(new LedgerLimits(1100, 500, 2000, 2000)),
            NativeRead = () => [.. Cluster(0, 0, 5), .. Cluster(1000, 0, 4)],
        };
        game.Online.Add(new HuntCandidate(7, 3000, 3000, true, true, false, false));   // far from all: the roamers jump toward it
        var host = new ApocalypseHost();
        var engine = ApocalypseTests.Engine(out _, Apo.Event(action: Apo.Action(hordes: Apo.Hordes(hordes, 1, 5, size, 0))));
        var board = new Scoreboard();
        Assert.Null(engine.Start("apo", "manual", T0, Open(), gate: d => host.Prepare(d, game)));
        var active = engine.Find("apo")!;
        Assert.True(host.Begin(active, game, state, board, T0));
        game.SpawnAll(host, T0);
        ApocalypseStep Tick(DateTime at) => host.Tick(active, game, state, board, at);
        var now = T0;
        for (var k = 1; k <= hordes; k++)
        {
            now = T0.AddMinutes(1 + 5 * (k - 1));
            Assert.Equal(k, Tick(now).Rose);
            game.SpawnAll(host, now);
        }
        return (game, host, Tick, now);
    }

    /// <summary>The unit reads and the round-robin visits of 20 one-second ticks (four move passes) after the hordes rose,
    /// with the most hop and roam visits one tick made.</summary>
    static (long Ops, long Hops, long Roam) Work(int hordes, int size)
    {
        var (game, host, tick, now) = Hordes(hordes, size);
        Assert.Equal(hordes * size, game.Ledger.Units.Count(u => Apocalypse.HordeOfWave(u.Wave) > 0));
        var (reads, visits) = (game.Reads, host.Visits);
        long hops = 0, roam = 0;
        for (var s = 1; s <= 20; s++)
        {
            var (h, r) = (host.HopVisits, host.RoamVisits);
            tick(now.AddSeconds(s));
            hops = Math.Max(hops, host.HopVisits - h);
            roam = Math.Max(roam, host.RoamVisits - r);
        }
        return (game.Reads - reads + host.Visits - visits, hops, roam);
    }

    /// <summary>Passes every 5 s for 60 s over groups of <paramref name="sizes"/> sharing one tick's bound, as the host's
    /// hops and roam do; asserts each pass's bound and returns the longest any unit waited.</summary>
    static int LongestWait(params int[] sizes)
    {
        var groups = sizes.Select(n => (Turns: new RoundRobin(), Units: Enumerable.Range(0, n).ToList())).ToList();
        var total = sizes.Sum();
        var last = new Dictionary<(int, int), int>();
        var longest = 0;
        for (var t = 0; t <= 60; t += 5)
        {
            var visited = 0;
            for (var g = 0; g < groups.Count; g++)
                foreach (var u in groups[g].Turns.Take(groups[g].Units, RoundRobin.Share(groups[g].Units.Count, total)))
                {
                    visited++;
                    if (last.TryGetValue((g, u), out var seen)) longest = Math.Max(longest, t - seen);
                    last[(g, u)] = t;
                }
            Assert.InRange(visited, 1, RoundRobin.PerPass + groups.Count);
        }
        Assert.Equal(total, last.Count);
        return longest;
    }

    [Fact]
    public void Scale_fails_when_a_tick_visits_more_than_its_bound()
    {
        var turns = new RoundRobin();
        var units = Enumerable.Range(0, 1000).ToList();
        for (var pass = 0; pass < 10; pass++) Assert.Equal(RoundRobin.PerPass, turns.Take(units).Count);
        Assert.Equal(10 * RoundRobin.PerPass, turns.Visits);
        Assert.Equal(7, new RoundRobin(7).Take(units).Count);

        // groups share the tick's bound: at most PerPass plus one a group, however many groups
        Assert.Equal(250, RoundRobin.Share(500, 1000));
        Assert.Equal(100, RoundRobin.Share(100, 300));                       // all of them while the total fits
        Assert.True(Enumerable.Range(1, 10).Select(_ => RoundRobin.Share(100, 1000)).Sum() <= RoundRobin.PerPass + 10);
        LongestWait(400, 300, 200, 100);

        // the host: one move pass of two hordes of 500 (1000 units) hops at most 500 of them, and the roam at most 500
        var (_, hops, roam) = Work(2, 500);
        Assert.InRange(hops, 1, RoundRobin.PerPass + 2);
        Assert.InRange(roam, 1, RoundRobin.PerPass + 2);
    }

    [Fact]
    public void Scale_fails_when_a_unit_waits_more_than_10_s_at_1000_units()
    {
        var turns = new RoundRobin();
        var units = Enumerable.Range(0, 1000).ToList();
        var last = new Dictionary<int, int>();
        var cadence = (int)ApocalypseHost.MoveEvery.TotalSeconds;
        Assert.Equal(5, cadence);
        for (var t = 0; t <= 60; t += cadence)
            foreach (var u in turns.Take(units))
            {
                if (last.TryGetValue(u, out var seen)) Assert.True(t - seen <= 10, $"unit {u} waited {t - seen} s");
                last[u] = t;
            }
        Assert.Equal(1000, last.Count);
        Assert.All(last.Values, t => Assert.True(t >= 50, "every unit visited in the last 10 s"));
        // 1000 units in groups sharing the bound, even or lopsided
        Assert.True(LongestWait(500, 500) <= 10);
        Assert.True(LongestWait(900, 100) <= 10);
        Assert.True(LongestWait(200, 200, 200, 200, 200) <= 10);

        // deaths before the cursor shift no one past their turn: the keyed round robin resumes after the last key taken
        var keyed = new RoundRobin();
        var living = Enumerable.Range(0, 1000).Select(i => (long)i).ToList();
        var seenAt = living.ToDictionary(k => k, _ => 0);
        for (var t = 5; t <= 120; t += 5)
        {
            living.RemoveRange(0, 10);                                                      // ten die each pass, all before the cursor
            foreach (var k in keyed.Take(living, RoundRobin.PerPass, k => k))
            {
                Assert.True(t - seenAt[k] <= 10, $"unit {k} waited {t - seenAt[k]} s");
                seenAt[k] = t;
            }
        }
    }

    [Fact]
    public void Scale_fails_when_a_step_grows_faster_than_linearly()
    {
        // the load ladder's sizes: 100, 250, 500 and 1000 (two hordes of 500, MaxUnitsPerWave's ceiling)
        var per = new[] { (1, 100), (1, 250), (1, 500), (2, 500) }.Select(h => (N: h.Item1 * h.Item2, Ops: Work(h.Item1, h.Item2).Ops)).ToList();
        var unit = per[0].Ops / (double)per[0].N;
        foreach (var (n, ops) in per)
            Assert.True(ops / (double)n <= unit * 1.25, $"{n} units: {ops} operations, {ops / (double)n:0.00} a unit against {unit:0.00} at 100");
        Assert.True(per[^1].Ops > per[0].Ops * 5, "the work grows with the units");
    }

    [Fact]
    public void Scale_fails_when_a_search_sits_inside_the_per_unit_loop()
    {
        var host = File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Logic", "ApocalypseHost.cs"));
        Assert.DoesNotContain("FindIndex(", host);
        Assert.DoesNotContain(".IndexOf(", host);
        Assert.Contains("HopTurns(front.Horde).Take(horde, RoundRobin.Share(horde.Count, hordeUnits), u => u.Key)", host);
        Assert.Contains("RoamTurns(spot.Number).Take(roamers, RoundRobin.Share(roamers.Count, roaming), r => r.Key)", host);
        Assert.Contains("_hopTurns.Clear();", host);                                  // a new run starts its turns afresh
        Assert.Contains("units.ToLookup(", host);
    }

    [Fact]
    public void Scale_fails_when_a_stall_marks_more_than_its_bound_in_one_tick()
    {
        var w = new WatchWorld(new SpawnLedger(new LedgerLimits(1000, 1000, 1000, 5)));
        var (r, a) = Watch.Rig(Watch.Waves(list: Watch.Two()), w);
        a.Record(WatchRig.T0, fought: true);
        a.WavesSpawned = 1;
        for (var i = 0; i < 700; i++) r.W.Add(a.Id, WatchRig.T0, 300 + i, 500);
        r.For(605);
        Assert.Equal("unit-watch uw stall: wave 1 quiet 10 min, 500 leftovers marked", r.Log.First(l => l.Contains("stall:")));
        Assert.Equal(RoundRobin.PerPass, r.P.StallMarks);
        r.For(5);                                                          // the rest at the next check
        Assert.Equal(700, r.P.StallMarks);
        Assert.Equal(["unit-watch uw stall: wave 1 quiet 10 min, 500 leftovers marked", "unit-watch uw stall: wave 1 quiet 10 min, 200 leftovers marked"],
            r.Log.Where(l => l.Contains("stall:")));
        r.For(1200);
        Assert.Equal(700, r.P.StallMarks);                                 // marked once each

        // two events quiet at once share the tick's bound: 400 + 400 marks 500, then 300
        var w2 = new WatchWorld(new SpawnLedger(new LedgerLimits(1000, 1000, 1000, 5)));
        var (q, first) = Watch.Rig(Watch.Waves(list: Watch.Two()), w2);
        var second = Watch.Run(Watch.Waves("uw2", list: Watch.Two()), WatchRig.T0);
        q.Active.Add(second);
        foreach (var e in new[] { first, second })
        {
            e.Record(WatchRig.T0, fought: true);
            e.WavesSpawned = 1;
            for (var i = 0; i < 400; i++) q.W.Add(e.Id, WatchRig.T0, 300 + i, 500);
        }
        q.For(605);
        Assert.Equal(RoundRobin.PerPass, q.P.StallMarks);
        q.For(5);
        Assert.Equal(800, q.P.StallMarks);
    }

    [Fact]
    public void Scale_passes_a_list_under_the_bound_whole_each_pass()
    {
        var turns = new RoundRobin();
        var units = Enumerable.Range(0, 300).ToList();
        Assert.Equal(units, turns.Take(units));
        Assert.Equal(units, turns.Take(units));
        var three = new RoundRobin(3);
        Assert.Equal([0, 1, 2], three.Take(new[] { 0, 1, 2, 3 }));
        Assert.Equal([3, 0, 1], three.Take(new[] { 0, 1, 2, 3 }));     // it wraps where the last pass stopped
    }

    [Fact]
    public void Scale_empty_no_units_no_visits()
    {
        var turns = new RoundRobin();
        Assert.Empty(turns.Take(Array.Empty<int>()));
        Assert.Equal(0, turns.Visits);
        Assert.Equal(Enumerable.Range(0, 5), turns.Take(Enumerable.Range(0, 5).ToList()));
    }
}
