using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse step 3 code review (M1-M4, L1): the fault cancel of a throwing tick, a failed resume's queued
/// orders, the hop's watch rule, a resumed run's status and end, and the cancelled orders of an outcome. Driven through
/// Logic/ApocalypseHost over the dependency suite's rig, with source checks of Services/ where the wiring lives (Epic S-21).
/// </summary>
public partial class ApocalypseTests
{
    static string ServiceSource(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", file)).Replace("\r\n", "\n");
    }

    // ---- D9 (review M1): a tick's throw reaches the fault guard and cancels the run after FaultLimit

    [Fact]
    public void EndPaths_fails_when_a_repeating_tick_throw_is_swallowed()
    {
        // an uncategorised throw inside the host's tick (the rise's announcement) escapes it, to the caller's guard
        var first = ApocalypseDependencyFailureTests.Running();
        first.Game.AnnounceThrow = new InvalidOperationException("chat gone");
        Assert.Throws<InvalidOperationException>(() => first.Tick(T0.AddMinutes(1)));

        // a repeating one: FaultLimit faults in a row cancel the run through the fault path, which removes the record
        var rig = ApocalypseDependencyFailureTests.Running();
        var active = rig.Active;
        var errors = new List<string>();
        var cancels = 0;
        void Cancel()
        {
            cancels++;
            Assert.NotNull(rig.Engine.Cancel("apo"));
            rig.Host.Ended("apo", ApocalypseEnd.Fault, rig.Game, rig.State);
        }
        for (var s = 1; s <= EventEngine.FaultLimit; s++)
            Assert.Equal(s == EventEngine.FaultLimit,
                FaultGuard.Run(rig.Engine, active, () => throw new InvalidOperationException("tick broke"), errors.Add, Cancel));
        Assert.Equal(1, cancels);
        Assert.Null(rig.Engine.Find("apo"));
        Assert.Null(rig.Host.Run);
        Assert.Null(rig.State.Document.Apocalypse);
        Assert.Equal(Enumerable.Range(1, EventEngine.FaultLimit).Select(n => $"event apo tick failed ({n}/{EventEngine.FaultLimit}): tick broke"), errors);

        // a clean tick ends the streak: throw, throw, clean, throw cancels nothing
        var again = ApocalypseDependencyFailureTests.Running();
        var a2 = again.Active;
        var calls = 0;
        foreach (var throws in new[] { true, true, false, true })
            Assert.False(FaultGuard.Run(again.Engine, a2, () => { calls++; if (throws) throw new InvalidOperationException("x"); }, _ => { }, () => cancels++));
        Assert.Equal(4, calls);
        Assert.Equal(1, cancels);
        Assert.NotNull(again.Engine.Find("apo"));

        // every other entry logs once per streak, not every second
        var lines = new List<string>();
        var guard = new EntryGuard(lines.Add);
        for (var s = 0; s < 5; s++) Assert.Null(guard.Run<ActiveEvent>("resume", () => throw new InvalidOperationException("drain read broke")));
        Assert.Equal(["apocalypse: resume failed: drain read broke"], lines);
        Assert.Equal(7, guard.Run("resume", () => 7));
        Assert.Null(guard.Run<ActiveEvent>("resume", () => throw new InvalidOperationException("drain read broke")));
        Assert.Equal(2, lines.Count);                                          // a new streak, a new line

        // the wiring: ApocalypseAction.Tick has no catch of its own, and EventRuntime guards it with FaultGuard
        var action = ServiceSource("ApocalypseAction.cs");
        var tick = EndPathTests.Body(action, "internal static ApocalypseStep Tick(ActiveEvent active, DateTime now)");
        Assert.Contains("_host.Tick(", tick);
        Assert.DoesNotContain("catch", tick);
        Assert.DoesNotContain("Run(", tick);
        Assert.Contains("_guard.Run(entry, work)", EndPathTests.Body(action, "static T Run<T>(string entry, Func<T> work)"));
        Assert.Contains("FaultGuard.Run(Engine, active,", EndPathTests.Body(EndPathTests.RuntimeSource().Replace("\r\n", "\n"),
            "static void Guarded(ActiveEvent active, Action work)"));
    }

    // ---- D12 (review M2, M4): a failed resume ends its orders; a resumed run keeps its status line and its record's end

    [Fact]
    public void Resume_fails_when_a_failed_resume_leaves_orders_queued()
    {
        var (_, rig) = ApocalypseDependencyFailureTests.Restarted();
        rig.State.Document.Apocalypse!.Scores = [new RecordScore { PlatformId = "76561190000000001", Kills = 4, Deaths = 1, First = 1 }];
        Assert.Equal("apo", rig.Host.Boot(() => rig.State.Document.Apocalypse, () => Apo.Set(Apo.Event()), rig.Engine, rig.Game, rig.State,
            T0.AddMinutes(2)));
        rig.Game.PlaceThrow = new InvalidOperationException("ground unreadable");
        rig.Game.PlaceThrowAfter = 1;                                          // the first rebuilt unit is queued, then the throw
        Assert.Null(rig.Host.Resume(rig.Engine, Open(), rig.Game, rig.State, rig.Board, T0.AddMinutes(2)));
        Assert.Null(rig.Engine.Find("apo"));
        Assert.Null(rig.State.Document.Apocalypse);
        Assert.Equal(0, rig.Game.Ledger.PendingSpawns);
        Assert.Empty(rig.Game.Ledger.Units);
        Assert.Empty(rig.Game.Ledger.TakeSpawns());
        Assert.Empty(rig.Board.Export("apo"));                                // the restored rows go too (post-fix review L2)
    }

    [Fact]
    public void Resume_fails_when_a_resumed_run_shows_another_status_or_end()
    {
        var (_, rig) = ApocalypseDependencyFailureTests.Restarted();
        var ends = T0.AddMinutes(20);                                          // the record's end, not start + duration
        rig.State.Document.Apocalypse!.EndsUtc = ends;
        Assert.Equal("apo", rig.Host.Boot(() => rig.State.Document.Apocalypse, () => Apo.Set(Apo.Event()), rig.Engine, rig.Game, rig.State,
            T0.AddMinutes(2)));
        var now = T0.AddMinutes(2).AddSeconds(1);
        var resumed = rig.Host.Resume(rig.Engine, Open(), rig.Game, rig.State, rig.Board, now);
        Assert.NotNull(resumed);
        var running = Assert.Single(rig.Engine.Catalog.Running);
        Assert.Same(running, resumed!.Instance);                               // one instance: the catalog's
        Assert.Equal(ends, running.EndsUtc);
        var line = rig.Host.StatusLine(running, rig.Game, now);
        Assert.NotNull(line);
        Assert.StartsWith("Night of the dead: horde 1/3, ", line);
        Assert.EndsWith("ends in 18 min", line);
        Assert.Contains(line, Messages.Status(rig.Engine.Catalog.Running, now, i => rig.Host.StatusLine(i, rig.Game, now)));
        // another instance gets no apocalypse line
        Assert.Null(rig.Host.StatusLine(new RunningInstance(running.Definition with { Id = "other" }, T0, ends), rig.Game, now));
    }

    // ---- D6 (review M3): no hop for a unit a player stands near, and the watch time is fresh before the hop decision

    /// <summary>A run whose horde 1 rose and spawned, one of its units moved 300 m off the front; returns its key.</summary>
    static (ApocalypseDependencyFailureTests.Rig Rig, Front Front, long Far) HordeWithStraggler()
    {
        var rig = ApocalypseDependencyFailureTests.Running();
        Assert.Equal(1, rig.Tick(T0.AddMinutes(1)).Rose);
        rig.Game.SpawnAll(rig.Host, T0.AddMinutes(1));
        var front = rig.Host.Run!.Fronts[1];
        var far = rig.Game.Ledger.Units.First(u => Apocalypse.HordeOfWave(u.Wave) == 1).Key;
        rig.Game.Positions[far] = (front.X, front.Z + 300);
        return (rig, front, far);
    }

    [Fact]
    public void March_fails_when_a_unit_near_a_player_hops()
    {
        // control: unobserved, the straggler hops back to the front
        var (c, _, cFar) = HordeWithStraggler();
        c.Tick(T0.AddMinutes(1).AddSeconds(5));
        Assert.Contains(c.Game.Moves, m => m.Key == cFar);

        // a player 50 m from the straggler, 350 m from the front: the horde is watched, nothing hops
        var (rig, front, far) = HordeWithStraggler();
        rig.Game.Online.Add(new HuntCandidate(7, front.X, front.Z + 350, true, true, false, false));
        for (var s = 5; s <= 15; s += 5) rig.Tick(T0.AddMinutes(1).AddSeconds(s));
        Assert.DoesNotContain(rig.Game.Moves, m => m.Key == far);
        Assert.Equal(T0.AddMinutes(1).AddSeconds(15), front.LastWatchedUtc);

        // a player who arrives within 100 m of the front on a hop tick: the watch time is refreshed before the hop decision
        var (late, lateFront, lateFar) = HordeWithStraggler();
        late.Game.Online.Add(new HuntCandidate(7, lateFront.X - 80, lateFront.Z, true, true, false, false));
        late.Tick(T0.AddMinutes(1).AddSeconds(5));
        Assert.DoesNotContain(late.Game.Moves, m => m.Key == lateFar);

        // the rule itself: a unit with a player within 100 m is never a straggler
        var g = new Front(1, 0, 0, 500, 0);
        var units = new List<(long, float, float)> { (1, 0, 0), (2, 0, 200) };
        Assert.Equal([2L], MarchRule.Stragglers(g, units, [], T0));
        Assert.Empty(MarchRule.Stragglers(g, units, [(0f, 290f)], T0));
        MarchRule.Watch(g, [(0f, 290f)], units.Select(u => (u.Item2, u.Item3)), T0);
        Assert.Equal(T0, g.LastWatchedUtc);
        MarchRule.Watch(g, [(0f, 400f)], units.Select(u => (u.Item2, u.Item3)), T0.AddSeconds(30));
        Assert.Equal(T0, g.LastWatchedUtc);                                    // nobody within 100 m: unchanged
    }

    [Fact]
    public void March_fails_when_the_keep_up_line_counts_a_hopped_unit_where_it_stood()
    {
        // every unit of horde 1 300 m off the front, nobody near: the hop tick lands them all, and the keep-up line of that
        // tick counts them at their landings (session 1: it read the positions from before the hops)
        var rig = ApocalypseDependencyFailureTests.Running();
        Assert.Equal(1, rig.Tick(T0.AddMinutes(1)).Rose);
        rig.Game.SpawnAll(rig.Host, T0.AddMinutes(1));
        var front = rig.Host.Run!.Fronts[1];
        var horde = rig.Game.Ledger.Units.Where(u => Apocalypse.HordeOfWave(u.Wave) == 1).Select(u => u.Key).ToList();
        foreach (var k in horde) rig.Game.Positions[k] = (front.X, front.Z + 300);
        rig.Game.Logs.Clear();
        rig.Tick(T0.AddMinutes(1).AddSeconds(30));                             // a hop tick and a keep-up tick at once
        var keep = Assert.Single(rig.Game.Logs, l => l.Contains(" % units ", StringComparison.Ordinal));
        Assert.EndsWith($"units {horde.Count}/{horde.Count} within 30 m of the front", keep);
        Assert.Contains(rig.Game.Logs, l => l == $"apocalypse apo horde 1 hops: {horde.Count} of {horde.Count} stragglers moved since the last line");
    }

    [Fact]
    public void March_fails_when_a_hop_gives_up_after_one_unwalkable_point()
    {
        // only one point of the 15 m ring around the landing centre (7.5 m ahead of the front) is walkable: the hop still lands
        // there, within 25 m of the centre
        var front = new Front(1, 0, 0, 500, 0);
        Assert.Equal((7.5f, 0f), HopRule.Ahead(front));
        var unit = new RegionUnit(42, "apo", T0, 0, 0, 300, 3);
        var (gx, gz) = HopRule.Rings(7.5f, 0f, 0).ElementAt(1 + 8 + 3);
        bool Walkable(float x, float z, byte level) => Math.Abs(x - gx) < 0.5 && Math.Abs(z - gz) < 0.5;
        var hop = HopRule.Land(unit, front, 0, 1, Walkable, _ => 0f, new WalkBudget());
        Assert.NotNull(hop);
        Assert.True(Apocalypse.Distance(hop!.Value.X, hop.Value.Z, 7.5f, 0f) <= 25);
        // near the goal the centre is the goal itself, never past it
        Assert.Equal((500f, 0f), HopRule.Ahead(new Front(1, 0, 0, 500, 0) { X = 495 }));
        // nothing walkable within 25 m: no hop, and the budget bounds the search
        var budget = new WalkBudget(1000);
        Assert.Null(HopRule.Land(unit, front, 0, 1, (_, _, _) => false, _ => 0f, budget));
        Assert.True(budget.Left > 0);
    }

    [Fact]
    public void March_fails_when_a_front_holds_for_good_where_the_ground_climbs_two_levels()
    {
        // the next point is walkable only two levels above the horde's: the march goes on (session 1: held at 12 % for good)
        bool Walkable(float x, float z, byte level) => level == 5 && Math.Abs(x - 100) < 0.5 && Math.Abs(z) < 0.5;
        Assert.True(HopRule.Ground(100, 0, 3, Walkable, new WalkBudget()));
        // no ground at any level within reach: held
        Assert.False(HopRule.Ground(100, 0, 3, (_, _, _) => false, new WalkBudget()));
    }

    // ---- D6 (post-fix review M1, L1): a spent walk budget never crosses unread ground; a step reaching the goal lands on it

    [Fact]
    public void March_fails_when_a_spent_budget_crosses_unread_ground()
    {
        static bool Open(float x, float z, byte level) => true;
        // unobserved, the next point unwalkable and every crossing point unread (the budget spent): held, not crossed
        var f = FrontAt();
        Assert.Equal(MarchStep.HeldNoGround, MarchRule.Step(f, 20, 2.0, 1, [], (x, _) => x < 1, T0, out var line, (_, _) => null));
        Assert.Equal(0f, f.X, 3);
        Assert.Equal("horde 1 held: no walkable ground ahead", line);
        // the read: a spent budget is "unread" for a crossing and "there" for an ordinary step (a budget never holds a march)
        var spent = new WalkBudget(0);
        Assert.Null(HopRule.GroundRead(100, 0, 3, Open, spent));
        Assert.True(HopRule.Ground(100, 0, 3, Open, spent));
        Assert.Null(HopRule.GroundRead(100, 0, null, Open, new WalkBudget()));
        Assert.False(HopRule.GroundRead(100, 0, 3, (_, _, _) => false, new WalkBudget()));
        // stragglers stop searching below the fronts' reserve, so they cannot spend it
        var u = new RegionUnit(1, "apo", T0, 0, 0, 0, 3);
        var low = new WalkBudget(HopRule.FrontReserve - 1);
        Assert.Null(HopRule.Land(u, FrontAt(), 0, 1, Open, _ => 0f, low));
        Assert.Equal(HopRule.FrontReserve - 1, low.Left);
        Assert.NotNull(HopRule.Land(u, FrontAt(), 0, 1, Open, _ => 0f, new WalkBudget()));
    }

    [Fact]
    public void March_fails_when_a_goal_off_the_axis_is_reached_short()
    {
        // a goal off both axes: a step that reaches it lands on it exactly and reports AtGoal at once
        var f = new Front(1, -1462.5f, 1389.7f, 1055.1f, -979.7f);           // x + dx / left * left misses it by a float step
        Assert.Equal(MarchStep.AtGoal, MarchRule.Step(f, 20, 2.0, 10000, [], (_, _) => true, T0, out _));
        Assert.Equal(1055.1f, f.X);
        Assert.Equal(-979.7f, f.Z);
        // the crossing too: only the goal dry, it lands on the goal exactly
        var c = new Front(1, 300.1f, 160.2f, 333.3f, 177.7f);
        Assert.Equal(MarchStep.AtGoal, MarchRule.Step(c, 0, 2.0, 1, [], (x, z) => x == 333.3f && z == 177.7f, T0, out _));
        Assert.Equal(333.3f, c.X);
    }

    [Fact]
    public void March_fails_when_frozen_units_fall_behind_between_hops()
    {
        // the game disables NPCs with no player near, so between hops a unit stands still (session 1: 4-6 of 8 kept up);
        // ticked each second for two minutes of march, at least 80 % of the horde is within 30 m of the front at every tick
        var rig = ApocalypseDependencyFailureTests.Running();
        Assert.Equal(1, rig.Tick(T0.AddMinutes(1)).Rose);
        rig.Game.SpawnAll(rig.Host, T0.AddMinutes(1));
        var front = rig.Host.Run!.Fronts[1];
        var horde = rig.Game.Ledger.Units.Where(u => Apocalypse.HordeOfWave(u.Wave) == 1).Select(u => u.Key).ToList();
        var worst = 1.0;
        for (var s = 1; s <= 120; s++)
        {
            var now = T0.AddMinutes(1).AddSeconds(s);
            rig.Tick(now);
            if (s < 11 || front.Percent >= 100) continue;                    // the 10 s unobserved window first
            var near = horde.Count(k => Apocalypse.Distance(rig.Game.Positions[k].X, rig.Game.Positions[k].Z, front.X, front.Z) <= Apocalypse.KeepUpRange);
            worst = Math.Min(worst, near / (double)horde.Count);
        }
        Assert.True(worst >= Apocalypse.KeepUpShare, $"worst keep-up share {worst:0.00}");
    }

    // ---- D15 (review L1): an outcome's cancelled orders count as failed, not pending

    [Fact]
    public void Tally_fails_when_an_outcome_drops_the_cancelled_orders()
    {
        var rig = ApocalypseDependencyFailureTests.Running();
        Assert.Equal(1, rig.Tick(T0.AddMinutes(1)).Rose);                       // horde 1's 8 orders wait
        var pending = rig.Game.Ledger.PendingSpawns;
        Assert.True(pending >= 8);
        var (_, cancelled) = rig.Game.Ledger.EndEvent("apo", DateTime.MinValue);
        Assert.Equal(pending, cancelled);
        Assert.NotNull(rig.Engine.Complete("apo", T0.AddMinutes(1), 60));
        rig.Host.Ended("apo", ApocalypseEnd.Won, rig.Game, rig.State, cancelled);
        var tally = rig.Game.Logs.Last(l => l.StartsWith("apocalypse apo units: ", StringComparison.Ordinal));
        Assert.Matches(new Regex($@"= 0 pending \+ .* \+ {cancelled} failed$"), tally);

        // the wiring: both outcome paths of EventRuntime pass the cancelled count through
        var source = EndPathTests.RuntimeSource().Replace("\r\n", "\n");
        var expire = EndPathTests.Body(source, "internal static void Tick(DateTime now)");
        Assert.Contains("(_, cancelled) = SpawnTracker.EndEventUnits(ended.Id, DateTime.MinValue);", expire);
        Assert.Contains("ApocalypseAction.Ended(ended.Id, ApocalypseEnd.Survived, cancelled)", expire);
        var outcome = EndPathTests.Body(source, "static void TickApocalypse(ActiveEvent active, DateTime now)");
        Assert.Contains("var (_, cancelled) = SpawnTracker.EndEventUnits(ended.Id, DateTime.MinValue);", outcome);
        Assert.Contains("ApocalypseAction.Ended(ended.Id, EndRule.Of(step.Result), cancelled)", outcome);
    }
}
