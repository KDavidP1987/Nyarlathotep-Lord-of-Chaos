using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The infestation and horde-march definitions of the tests (undead-hordes D8, D10): events.json text for every
/// block, on Apo's units and natives.</summary>
static class Inf
{
    public static string Spread(int min = 1, int max = 3, int cap = 30, int perMinute = 10, string? pool = null) =>
        $"{{ \"perKillMin\": {min}, \"perKillMax\": {max}, \"max\": {cap}, \"perMinute\": {perMinute}" + (pool is null ? "" : $", \"pool\": \"{pool}\"") + " }";

    public static string Origins(int count = 2, int minNatives = 3, int minSpacing = 200, int perOrigin = 4) =>
        $"{{ \"count\": {count}, \"minNatives\": {minNatives}, \"minSpacing\": {minSpacing}, \"perOrigin\": {perOrigin} }}";

    /// <summary>An Infestation action: Faction_Undead, two units, 2 origins of 4, hunters 50 %, drift 400 m, spread 1-3 per
    /// kill, max 30, 10 a minute.</summary>
    public static string Action(string? origins = null, int hunters = 50, int drift = 400, string? spread = null, string? extra = null) =>
        "\"action\": { \"type\": \"Infestation\", \"faction\": \"Faction_Undead\", \"units\": [ " + Apo.Unit() + ", " + Apo.Unit(Apo.Thug, "1") + " ], " +
        "\"origins\": " + (origins ?? Origins()) + ", \"hunters\": " + hunters + ", \"driftRadius\": " + drift + ", \"spread\": " + (spread ?? Spread()) +
        (extra is null ? "" : ", " + extra) + " }";

    public const string PointStart = "{ \"type\": \"Point\", \"x\": 0, \"z\": 0 }";
    public const string Destination = "{ \"location\": { \"type\": \"Point\", \"x\": 500, \"z\": 0, \"y\": 12 }, \"radius\": 20, \"name\": \"Brighthaven\" }";

    /// <summary>A HordeMarch action: Faction_Undead, 8 + 2 units, one fixed boss, a Point start at (0, 0), a destination at
    /// (500, 0) of radius 20, 2 m/s, growth 1-3 (max 30), infest hunters 60 %, drift 300 m, spread 1-3 (max 40).</summary>
    public static string March(string start = PointStart, string destination = Destination, string? units = null, string? growth = null,
        string? infest = null, string? extra = null) =>
        "\"action\": { \"type\": \"HordeMarch\", \"faction\": \"Faction_Undead\", \"units\": " + (units ?? "[ " + Apo.Unit(count: "8") + ", " + Apo.Unit(Apo.Thug, "2") + " ]") +
        ", \"bosses\": [ " + Apo.BossJson() + " ], \"start\": " + start + ", \"destination\": " + destination + ", \"marchSpeed\": 2.0" +
        ", \"growth\": " + (growth ?? Spread()) + ", \"infest\": " + (infest ?? "{ \"hunters\": 60, \"driftRadius\": 300, \"spread\": " + Spread(cap: 40) + " }") +
        (extra is null ? "" : ", " + extra) + " }";

    public static IEnumerable<HotspotNative> Cluster(float x, float z, int n) =>
        Enumerable.Range(0, n).Select(i => new HotspotNative(new UnitFacts(Apo.Zombie, "Faction_Undead", false, false, false, false, false, null,
            () => "FarbaneWoods"), x + i, z + i));

    public static ScoreSides KillBy(int wave, DateTime spawned) => new(null, new OurUnit("apo", wave, spawned));

    public static HuntCandidate Player(float x, float z, long key = 1) => new(key, x, z, true, true, false, false);
}

/// <summary>undead-hordes D8, D9, D13, D17, D20: the infestation's rise at its origins, its hunters and drifters and spread,
/// one apocalypse-type event at a time, the resume and the chat fields, over Logic/ApocalypseHost and the engine it drives.</summary>
public class InfestationTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);

    static ApocalypseDependencyFailureTests.Rig Running(string? action = null)
    {
        var rig = ApocalypseDependencyFailureTests.Make(action ?? Inf.Action());
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        rig.Game.SpawnAll(rig.Host, T0);
        return rig;
    }

    static List<TrackedUnit> Ours(ApocalypseDependencyFailureTests.Rig rig) => rig.Game.Ledger.Units.Where(u => Apocalypse.Ours(u, rig.Active)).ToList();

    // ---- D8 Rise

    [Fact]
    public void Spread_fails_when_the_moves_line_misses_a_count_or_names_a_position()
    {
        // step 4's diagnostic: the hunters' plans over a minute, counts only, then reset
        var tally = new InfestTally();
        Assert.False(tally.Due(T0));
        tally.Count(JumpKind.Jump);
        tally.Count(JumpKind.Jump);
        tally.Count(JumpKind.Waiting);
        tally.Count(JumpKind.InReach);
        tally.Count(null);
        tally.Waits.Candidates = 7;
        tally.Waits.NotWalkable = 5;
        Assert.False(tally.Due(T0.AddSeconds(59)));
        Assert.True(tally.Due(T0.AddSeconds(60)));
        Assert.Equal("apocalypse inf infest moves: 15 hunters, 2 origins on a player; 2 jumps, 1 in reach, 1 waiting, 1 with no target "
            + "(7 landings tried, 0 watched, 0 near a player, 0 claimed, 5 not walkable, 0 out of budget)", tally.Line("inf", 15, 2, T0.AddSeconds(60)));
        Assert.False(tally.Due(T0.AddSeconds(100)));
        Assert.Equal("apocalypse inf infest moves: 15 hunters, 2 origins on a player; 0 jumps, 0 in reach, 0 waiting, 0 with no target "
            + "(0 landings tried, 0 watched, 0 near a player, 0 claimed, 0 not walkable, 0 out of budget)", tally.Line("inf", 15, 2, T0.AddSeconds(120)));
    }

    [Fact]
    public void Rise_fails_when_an_origin_is_not_a_hotspot_or_gets_other_than_perOrigin()
    {
        var rig = Running();
        var run = rig.Host.Run!;
        Assert.Equal(ApocalypseMode.Infestation, run.Mode);
        // the origins are the hotspot pick over the natives: the clusters at (0, 0) and (1000, 0)
        Assert.Equal([(0f + 2f, 0f + 2f), (1000f + 1.5f, 0f + 1.5f)], run.Origins.Select(o => (o.X, o.Z)));
        Assert.Equal(run.Hotspots, run.Origins);
        foreach (var o in run.Origins)
            Assert.Equal(4, rig.Game.Ledger.Units.Count(u => u.Wave == Apocalypse.RoamerWave(o.Number)));
        Assert.Equal(8, Ours(rig).Count);
        Assert.Empty(run.Fronts);                                                  // no horde
        Assert.Equal(["The dead rise in 2 places: Night of the dead."], rig.Game.Announced);
        Assert.True(ApocalypseHost.OwnStartBanner(rig.Active.Definition));             // step 4: in place of the generic start banner
        Assert.False(ApocalypseHost.OwnStartBanner(Apo.Def()));
        Assert.Null(rig.Tick(T0.AddMinutes(30)).Rose);                             // and none ever rises
    }

    [Fact]
    public void Rise_fails_when_the_max_is_passed()
    {
        var rig = Running(Inf.Action(origins: Inf.Origins(perOrigin: 10), spread: Inf.Spread(cap: 15)));
        Assert.Equal(15, Ours(rig).Count);                                         // 2 x 10 asked, the max holds 15
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.RoamerWave(1), T0), (5, 5)), rig.Game, T0.AddSeconds(5));
        rig.Game.SpawnAll(rig.Host, T0.AddSeconds(5));
        Assert.Equal(15, Ours(rig).Count);                                         // a kill at the max spreads nothing
    }

    [Fact]
    public void Rise_fails_when_a_start_without_origins_runs()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Inf.Action());
        rig.Game.NativeRead = () => Inf.Cluster(0, 0, 2);                         // fewer than minNatives 3
        var refused = rig.Start(T0);
        Assert.Equal("no Undead natives found", refused!.Human);
        Assert.Null(rig.Engine.Find("apo"));
        Assert.Null(rig.Host.Run);
    }

    [Fact]
    public void Rise_passes_ten_origins_each_topped_up_once()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Inf.Action(origins: Inf.Origins(count: 10, perOrigin: 2), spread: Inf.Spread(cap: 100)));
        rig.Game.NativeRead = () => Enumerable.Range(0, 10).SelectMany(i => Inf.Cluster(i * 500, 0, 3));
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        rig.Game.SpawnAll(rig.Host, T0);
        Assert.Equal(10, rig.Host.Run!.Origins.Count);
        Assert.Equal(20, Ours(rig).Count);
        Assert.Contains("The dead rise in 10 places: Night of the dead.", rig.Game.Announced);
    }

    [Fact]
    public void Rise_empty_no_natives_no_run()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Inf.Action());
        rig.Game.NativeRead = () => [];
        Assert.NotNull(rig.Start(T0));
        Assert.Null(rig.Host.Run);
        Assert.Empty(rig.Game.Ledger.Units);
    }

    // ---- D9 Spread

    [Fact]
    public void Spread_fails_when_the_hunter_share_is_off_by_more_than_one_unit()
    {
        var roles = new InfestRoles();
        roles.Assign(Enumerable.Range(1, 10).Select(i => (long)i).ToList(), 50);
        Assert.Equal(5, roles.Hunters);
        Assert.All(Enumerable.Range(1, 5), k => Assert.True(roles.IsHunter(k)));         // the first keys hunt
        roles.Assign(Enumerable.Range(1, 13).Select(i => (long)i).ToList(), 50);         // 3 spread units join: 6 of 13
        Assert.Equal(6, roles.Hunters);
        Assert.True(roles.IsHunter(11));
        Assert.False(roles.IsHunter(12));                                                // the share met, the rest drift
        roles.Assign(Enumerable.Range(6, 8).Select(i => (long)i).ToList(), 50);          // 5 hunters died: 4 of 8, the fewest changed
        Assert.Equal(4, roles.Hunters);
        Assert.Equal([6, 7, 8, 11], Enumerable.Range(6, 8).Where(k => roles.IsHunter(k)));   // 11 kept; 6, 7, 8 joined
        roles.Assign([7, 8, 11, 12], 50);                                                // drifters died: 2 of 4, the highest hunters drift
        Assert.Equal(2, roles.Hunters);
        Assert.Equal([7, 8], new long[] { 7, 8, 11, 12 }.Where(roles.IsHunter));
        // code review step 3: the run's living units, read or not, hold the roles; an unread unit keeps its own
        var rig = Running(Inf.Action(origins: Inf.Origins(perOrigin: 10)));
        rig.Game.Online.Add(Inf.Player(5000, 5000));
        rig.Tick(T0.AddSeconds(5));
        var keys = Ours(rig).Select(u => u.Key).ToList();
        var run = rig.Host.Run!;
        var before = keys.ToDictionary(k => k, run.Roles.IsHunter);
        rig.Game.Unread = u => u.Key % 2 == 0;
        for (var s = 10; s <= 20; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.Equal(before, keys.ToDictionary(k => k, run.Roles.IsHunter));
        Assert.Equal(InfestRoles.Share(50, keys.Count), run.Roles.Hunters);
        for (var n = 1; n <= 40; n++)                                                    // any churn: within the share exactly
        {
            roles.Assign(Enumerable.Range(n, n % 7 + 3).Select(i => (long)i).ToList(), 37);
            Assert.Equal(InfestRoles.Share(37, n % 7 + 3), roles.Hunters);
        }
        Assert.Equal(1, InfestRoles.Share(50, 3));
        Assert.Equal(0, InfestRoles.Share(0, 300));
        Assert.Equal(300, InfestRoles.Share(100, 300));
    }

    [Fact]
    public void Spread_fails_when_a_jump_or_hop_happens_with_a_player_within_100_m()
    {
        var rig = Running();
        rig.Game.Online.Add(Inf.Player(60, 0));                                         // within 100 m of origin 1's units
        rig.Tick(T0.AddSeconds(5));
        var near = Ours(rig).Where(u => u.Wave == Apocalypse.RoamerWave(1)).Select(u => u.Key).ToHashSet();
        Assert.DoesNotContain(rig.Game.Moves, m => near.Contains(m.Key));
        Assert.All(rig.Game.Moves, m => Assert.True(Apocalypse.Distance(m.X, m.Z, 60, 0) >= 50));   // landings 50 m or more from every player
    }

    [Fact]
    public void Spread_fails_when_a_drifter_passes_driftRadius()
    {
        var rig = Running(Inf.Action(drift: 100));
        rig.Game.Online.Add(Inf.Player(5000, 5000));
        var run = rig.Host.Run!;
        for (var s = 5; s <= 300; s += 5) rig.Tick(T0.AddSeconds(s));
        var drifters = Ours(rig).Where(u => !run.Roles.IsHunter(u.Key)).ToList();
        var hunters = Ours(rig).Where(u => run.Roles.IsHunter(u.Key)).ToList();
        Assert.Equal(4, hunters.Count);                                                    // 50 % of 8
        Assert.All(drifters, u =>
        {
            var origin = run.Origins[Apocalypse.HotspotOfWave(u.Wave)!.Value - 1];
            var (x, z) = rig.Game.Positions[u.Key];
            Assert.True(Apocalypse.Distance(origin.X, origin.Z, x, z) <= 100.5f, $"{u.Key} drifted past 100 m");
        });
        Assert.Contains(rig.Game.Moves, m => drifters.Any(d => d.Key == m.Key));          // they did move outward
        // the hunters close on the player by hidden jumps
        Assert.All(hunters, u => Assert.True(Apocalypse.Distance(rig.Game.Positions[u.Key].X, rig.Game.Positions[u.Key].Z, 5000, 5000) < 7000));
        Assert.Contains(rig.Game.Moves, m => hunters.Any(h => h.Key == m.Key));
        // a drift plan never lands past the ring, whatever the unit's bearing
        var u0 = new RegionUnit(7, "apo", T0, 95, 0, 0, 1, Apocalypse.RoamerWave(1));
        var (kind, jump) = InfestMove.Drift(u0, (0, 0), 100, [], (_, _) => false, (_, _, _) => true, l => l, new WalkBudget());
        Assert.True(kind == JumpKind.InReach || jump is { } j && Apocalypse.Distance(0, 0, j.X, j.Z) <= 100);
        Assert.Equal((JumpKind.InReach, null), InfestMove.Drift(u0, (0, 0), 0, [], (_, _) => false, (_, _, _) => true, l => l, new WalkBudget()));
    }

    [Fact]
    public void Spread_fails_when_the_outcome_differs()
    {
        var rig = Running();
        Assert.Equal(ApocalypseResult.None, rig.Tick(T0.AddSeconds(1)).Result);
        foreach (var u in Ours(rig)) Assert.True(rig.Game.Ledger.Forget(u.Key));
        Assert.Equal(ApocalypseResult.Won, rig.Tick(T0.AddSeconds(2)).Result);           // cleared
        Assert.Equal("event apo ended: won (cleared)", ApocalypseOutcome.Line("apo", ApocalypseResult.Won, 0, 0, ApocalypseMode.Infestation));
        var held = Running();
        Assert.Equal(ApocalypseResult.None, held.Tick(T0.AddSeconds(3599)).Result);
        Assert.Equal(ApocalypseResult.Survived, held.Host.Run!.Outcome(held.Game, _ => [], T0.AddSeconds(3600)));   // the clock ran out with units alive (the end path ends it)
        Assert.Equal("event apo ended: survived", ApocalypseOutcome.Line("apo", ApocalypseResult.Survived, 0, 0, ApocalypseMode.Infestation));
        // a spread order still pending holds the win
        var pending = Running();
        pending.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.RoamerWave(1), T0), (5, 5)), pending.Game, T0.AddSeconds(5));
        foreach (var u in pending.Game.Ledger.Units.Where(u => Apocalypse.Ours(u, pending.Active)).ToList()) pending.Game.Ledger.Forget(u.Key);
        Assert.Equal(ApocalypseResult.None, pending.Tick(T0.AddSeconds(6)).Result);
        // code review step 3: caps full at the start queue nothing, which is no win; the top-up tries again each minute
        var made = ApocalypseDependencyFailureTests.Make(Inf.Action());
        var blocked = made with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(0, 50, 1000, 1000)), NativeRead = made.Game.NativeRead } };
        Assert.Null(blocked.Start(T0));
        Assert.True(blocked.Host.Begin(blocked.Active, blocked.Game, blocked.State, blocked.Board, T0));
        var freed = blocked with { Game = new ApoGame { NativeRead = made.Game.NativeRead } };   // the caps free at once
        Assert.Equal(ApocalypseResult.None, freed.Tick(T0.AddSeconds(1)).Result);
        Assert.Equal(0, freed.Host.Run!.Tally.Queued);                                        // the start's try counts: no retry yet
        Assert.Equal(ApocalypseResult.None, freed.Tick(T0.AddSeconds(30)).Result);
        Assert.Equal(0, freed.Host.Run!.Tally.Queued);                                        // within the minute: no retry yet
        Assert.Equal(ApocalypseResult.None, freed.Tick(T0.AddSeconds(62)).Result);
        Assert.Equal(8, freed.Host.Run!.Tally.Queued);                                        // 2 origins of 4
        Assert.Contains(freed.Game.Logs, l => l.StartsWith("apocalypse apo roamers: +4 at hotspot 1", StringComparison.Ordinal));
        // every order failed: still no win
        var failed = freed.Game.Ledger.TakeSpawns();
        foreach (var o in failed) { freed.Game.Ledger.Fail(o); freed.Host.Settled(o.EventId, o.Wave, false); }
        Assert.Equal(ApocalypseResult.None, freed.Tick(T0.AddSeconds(63)).Result);
    }

    [Fact]
    public void Spread_fails_when_the_unexplained_share_of_the_living_count_is_over_0()
    {
        var rig = Running();
        var run = rig.Host.Run!;
        for (var i = 0; i < 4; i++)
            rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.RoamerWave(2), T0), (1000, 0)), rig.Game, T0.AddSeconds(10 + i));
        var spread = run.LivingOf(rig.Game, Apocalypse.RoamerWave(2)) - 4;
        Assert.InRange(spread, 4, 12);                                                    // 1-3 a kill
        Assert.Equal(8 + spread, run.Living(rig.Game));                                   // origins' 8 and the spread, nothing else
        rig.Game.SpawnAll(rig.Host, T0.AddSeconds(20));
        Assert.Equal(8 + spread, Ours(rig).Count);
        Assert.Equal(run.Tally.Queued, run.Living(rig.Game));
    }

    [Fact]
    public void Spread_passes_kills_draw_into_the_nearest_origin()
    {
        var rig = Running();
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.RoamerWave(1), T0), (990, 5)), rig.Game, T0.AddSeconds(10));
        Assert.True(rig.Host.Run!.LivingOf(rig.Game, Apocalypse.RoamerWave(2)) > 4);       // the victim fell by origin 2
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("apocalypse apo spread: +", StringComparison.Ordinal));
    }

    [Fact]
    public void Spread_empty_no_player_no_hunt()
    {
        var rig = Running(Inf.Action(drift: 0));
        for (var s = 5; s <= 60; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.Empty(rig.Game.Moves);                                                      // nobody to hunt, nowhere to drift
    }

    // ---- D13 Clash

    [Fact]
    public void Clash_fails_when_two_of_the_family_run_together_or_a_destination_wave_is_refused()
    {
        var engine = ApocalypseTests.Engine(out _, Apo.Event("inf", action: Inf.Action()).Replace("Night of the dead", "Undead infestation"),
            Apo.Event("apo"), Apo.Event("march", action: Inf.March()).Replace("Night of the dead", "Undead horde march"),
            Json.Event("raid", action: Json.ValidAction[..^2] + ", \"destination\": " + Inf.Destination + " }"));
        var open = new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 5);
        Assert.Null(engine.Start("inf", "manual", T0, open));
        var refused = engine.Start("apo", "manual", T0, open with { ActiveEvents = 1 });
        Assert.Equal("an apocalypse-type event is running: Undead infestation", refused!.Human);
        Assert.Equal(Reasons.AlreadyActive, refused.Reason);
        Assert.Equal("an apocalypse-type event is running: Undead infestation", engine.Start("march", "manual", T0, open with { ActiveEvents = 1 })!.Human);
        Assert.Null(engine.Start("raid", "manual", T0, open with { ActiveEvents = 1 }));   // a destination wave runs beside it
        Assert.Equal(["inf", "raid"], engine.Active.Select(a => a.Id).OrderBy(i => i));
        // a pending resume holds the slot, named by its definition
        var resuming = ApocalypseTests.Engine(out _, Apo.Event("inf", action: Inf.Action()).Replace("Night of the dead", "Undead infestation"), Apo.Event("apo"));
        resuming.ResumePending = "inf";
        Assert.Equal("an apocalypse-type event is running: Undead infestation", resuming.Start("apo", "manual", T0, open)!.Human);
    }

    [Fact]
    public void Clash_passes_one_after_another()
    {
        var engine = ApocalypseTests.Engine(out _, Apo.Event("inf", action: Inf.Action()), Apo.Event("apo"));
        var open = new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 5);
        Assert.Null(engine.Start("inf", "manual", T0, open));
        Assert.NotNull(engine.Cancel("inf"));
        Assert.Null(engine.Start("apo", "manual", T0.AddMinutes(1), open));
    }

    [Fact]
    public void Clash_empty_nothing_running_nothing_refused() =>
        Assert.Null(EventActions.ApocalypseClash(Apo.Def(Inf.Action()), [], null));

    // ---- D17 Resume

    [Fact]
    public void Resume_fails_when_a_resumed_run_loses_its_mode_or_rebuilds_a_different_count()
    {
        var rig = Running();
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.RoamerWave(1), T0), (5, 5)), rig.Game, T0.AddSeconds(10));
        rig.Game.SpawnAll(rig.Host, T0.AddSeconds(10));
        var run = rig.Host.Run!;
        var record = run.Record(rig.Game, [], T0.AddSeconds(20));
        Assert.Equal("Infestation", record.Mode);
        Assert.Null(record.Problem());
        var back = StateDocument.TryParse(new StateDocument { Apocalypse = record }.Serialize())!.Apocalypse!;
        Assert.Null(back.Unreadable);
        Assert.Equal(ApocalypseMode.Infestation, back.ModeOf);
        Assert.Equal(record.Roamers, back.Roamers);
        Assert.False(ResumeRule.Changed(back, rig.Active.Definition));
        var resumed = ApocalypseRun.Resumed(rig.Active, back, T0.AddMinutes(5));
        var w = new ApoWorld();
        var (rebuilt, rises, lines) = resumed.Rebuild(w, back, T0.AddMinutes(5));
        Assert.Equal(record.Roamers.Sum(), rebuilt);                                    // exactly the recorded units
        Assert.Empty(rises);
        Assert.Empty(lines);
        Assert.Equal(run.Living(rig.Game), resumed.Living(w));
        // Codex step 3 re-check: a record of no unit (the caps full at the start) resumes, then waits the minute to top up
        var none = run.Record(new ApoWorld(), [], T0.AddSeconds(20));
        Assert.Equal(0, none.Roamers.Sum());
        var empty = ApocalypseRun.Resumed(rig.Active, none, T0.AddMinutes(5));
        var room = new ApoWorld();
        Assert.Equal(0, empty.Rebuild(room, none, T0.AddMinutes(5)).Rebuilt);
        Assert.Null(empty.Rise(room, T0.AddMinutes(5).AddSeconds(10), out _));
        Assert.Equal(0, empty.Tally.Queued);
        empty.Rise(room, T0.AddMinutes(6).AddSeconds(1), out _);
        Assert.True(empty.Tally.Queued > 0);
        // a definition whose mode changed does not resume the record
        Assert.True(ResumeRule.Changed(back, Apo.Def()));
    }

    [Fact]
    public void Resume_fails_when_an_over_ceiling_record_is_accepted()
    {
        var record = Running().Host.Run!.Record(new ApoWorld(), [], T0);
        foreach (var odd in new[] { "7", "Infestation, HordeMarch", "Apocalypse", "infestation" })   // code review step 3: names only
        {
            var named = Running().Host.Run!.Record(new ApoWorld(), [], T0);
            named.Mode = odd;
            Assert.Equal("mode must be Infestation or HordeMarch", named.Problem());
            Assert.Equal(ApocalypseMode.Apocalypse, named.ModeOf);
        }
        record.Roamers = [Limits.MaxTrackedUnits.Max, 1];
        Assert.Equal(ApocalypseRecord.OverCap, record.Problem());
        var back = StateDocument.TryParse(new StateDocument { Apocalypse = record }.Serialize())!;
        Assert.Equal(ApocalypseRecord.OverCap, back.Apocalypse!.Unreadable);
        Assert.Equal("apo", back.Apocalypse.EventId);
        var files = new MemoryFileStore();
        var log = new LogLines();
        files.Put(DataFile.State, FileVariant.Main, System.Text.Encoding.UTF8.GetString(new StateDocument { Apocalypse = record }.Serialize()));
        var reread = new StateStore(new DataStore(files, log.Add), () => files.Now, log.Add);
        reread.Load();
        Assert.Null(reread.Document.Apocalypse);
        Assert.Contains("event apo ended: survived (the living counts exceed the tracked-unit cap)", log.Lines);
        Assert.Contains(log.Lines, l => l.Contains(ApocalypseRecord.UnreadableLine(ApocalypseRecord.OverCap), StringComparison.Ordinal));
        // a mode the code does not know is refused, never read as an apocalypse
        record.Roamers = [1, 1];
        record.Mode = "Plague";
        Assert.Equal("mode must be Infestation or HordeMarch", record.Problem());
    }

    [Fact]
    public void Resume_passes_ten_origins_in_the_record()
    {
        var spots = Enumerable.Range(1, 10).Select(i => new Hotspot(i, i * 500, 0, 3, i * 5, 0)).ToList();
        var run = new ApocalypseRun(Apo.Active(Apo.Def(Inf.Action(origins: Inf.Origins(count: 10))), T0), spots, (0, 0, 0));
        var record = run.Record(new ApoWorld(), [], T0);
        Assert.Equal(10, record.Hotspots.Count);
        Assert.Null(record.Problem());
        record.Mode = null;                                                             // an apocalypse keeps 1-5
        Assert.Equal("hotspots must hold 1-5 entries", record.Problem());
    }

    [Fact]
    public void Resume_empty_an_apocalypse_record_writes_no_mode()
    {
        var record = Apo.Run(Apo.Active(Apo.Def(), T0)).Record(new ApoWorld(), [], T0);
        Assert.Null(record.Mode);
        Assert.False(record.Arrived);
        Assert.Null(record.Arrival);
        Assert.DoesNotContain("\"mode\":\"", System.Text.Encoding.UTF8.GetString(new StateDocument { Apocalypse = record }.Serialize()));
    }

    // ---- D20 Chat

    static JsonObject ActionOf(Library lib, string id) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id)["action"]!;

    [Fact]
    public void Chat_fails_when_a_field_cannot_be_set_or_writes_another_field()
    {
        var lib = new Library(Json.File(Apo.Event("inf", action: Inf.Action(spread: Inf.Spread(pool: "troops"), extra: "\"pools\": { \"troops\": [ \"" + Apo.Zombie + "\" ] }")
            .Replace("\"units\": [ " + Apo.Unit() + ", " + Apo.Unit(Apo.Thug, "1") + " ]", "\"units\": [ { \"pool\": \"troops\", \"count\": 3 } ]"))));
        Assert.True(lib.Catalog.Current.Find("inf")!.Startable, lib.Catalog.Current.Find("inf")!.DisabledReason);
        var sets = new (string Field, string Value, string Path, string Json)[]
        {
            ("action.origins.count", "5", "origins.count", "5"),
            ("action.origins.minNatives", "4", "origins.minNatives", "4"),
            ("action.origins.minSpacing", "300", "origins.minSpacing", "300"),
            ("action.origins.perOrigin", "12", "origins.perOrigin", "12"),
            ("action.hunters", "70", "hunters", "70"),
            ("action.driftRadius", "250", "driftRadius", "250"),
            ("action.spread.perKillMin", "2", "spread.perKillMin", "2"),
            ("action.spread.perKillMax", "3", "spread.perKillMax", "3"),
            ("action.spread.max", "200", "spread.max", "200"),
            ("action.spread.perMinute", "25", "spread.perMinute", "25"),
        };
        foreach (var (field, value, path, json) in sets)
        {
            var before = JsonNode.Parse(ActionOf(lib, "inf").ToJsonString())!.AsObject();
            Assert.StartsWith($"event inf {field} = {value}", AuthoringTests.Set(lib, "inf", field, value));
            var after = ActionOf(lib, "inf");
            var parts = path.Split('.');
            JsonNode node = after;
            foreach (var p in parts) node = node[p]!;
            Assert.Equal(json, node.ToJsonString());
            // nothing else moved
            JsonNode was = before;
            foreach (var p in parts[..^1]) was = was[p]!;
            ((JsonObject)was)[parts[^1]] = JsonNode.Parse(json);
            Assert.Equal(before.ToJsonString(), after.ToJsonString());
            Assert.True(lib.Catalog.Current.Find("inf")!.Startable, field);
        }
        Assert.StartsWith("event inf action.spread.pool = none", AuthoringTests.Set(lib, "inf", "action.spread.pool", "none"));
        Assert.Null(ActionOf(lib, "inf")["spread"]!["pool"]);
        // refusals: out of range with the validator's reason, a field of the other type, an Apocalypse field
        Assert.Equal(EventValidator.OriginsCountRule, AuthoringTests.Set(lib, "inf", "action.origins.count", "11"));
        Assert.Equal(EventValidator.HuntersRule("action"), AuthoringTests.Set(lib, "inf", "action.hunters", "101"));
        Assert.Equal(EventValidator.SpreadMaxRule("action.spread"), AuthoringTests.Set(lib, "inf", "action.spread.max", "0"));
        Assert.Equal("action.growth.max is not an Infestation field", AuthoringTests.Set(lib, "inf", "action.growth.max", "40"));
        Assert.Equal("action.hordes.size is not an Infestation field", AuthoringTests.Set(lib, "inf", "action.hordes.size", "20"));
        Assert.Equal("action.waves is not a field of an Infestation action", AuthoringTests.Set(lib, "inf", "action.waves", "3"));
        Assert.Equal("action.hunters is not a SpawnWaves field", AuthoringTests.Set(new Library(Json.File(Json.Event("raid"))), "raid", "action.hunters", "20"));
        // every field goes through the admin's event set
        Assert.All(CommandArgs.HordeFields, f => Assert.Equal(("apocalypse action", "admin"), CommandArgs.SettableFields[f]));
    }

    [Fact]
    public void Chat_fails_when_a_status_or_info_line_differs()
    {
        var rig = Running();
        var status = rig.Host.StatusLine(rig.Active.Instance, rig.Game, T0.AddMinutes(1));
        Assert.Equal("Night of the dead: 8 undead, 2 places", status);
        var info = EventLines.Info(rig.Active.Definition, rig.Active, T0.AddMinutes(1), apocalypse: rig.Host.Run!.View(rig.Game, (_, _) => "Farbane Woods"));
        Assert.Contains("action: infestation of the Undead", info);
        Assert.Contains("origins: 2, at least 3 natives each, 200 m apart, 4 units each", info);
        Assert.Contains("hunters: 50 %, drift radius 400 m", info);
        Assert.Contains("spread: +1-3 per kill, max 30, 10 per minute", info);
        Assert.Contains(info, l => l.StartsWith("running: started by manual, ", StringComparison.Ordinal) && l.EndsWith(", 8 units", StringComparison.Ordinal));
        Assert.DoesNotContain(info, l => l.StartsWith("hordes:", StringComparison.Ordinal) || l.StartsWith("goal:", StringComparison.Ordinal));
        Assert.All(info, l => Assert.True(System.Text.Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Chat_passes_the_duration_of_the_family()
    {
        var lib = new Library(Json.File(Apo.Event("inf", action: Inf.Action())));
        Assert.StartsWith("event inf durationSeconds = 86400", AuthoringTests.Set(lib, "inf", "durationSeconds", "86400"));
        Assert.Equal(EventValidator.FamilyDurationRule(EventValidator.InfestationType), AuthoringTests.Set(lib, "inf", "durationSeconds", "300"));
        Assert.Equal("durationSeconds must be 600-604800 for an Infestation", EventValidator.FamilyDurationRule(EventValidator.InfestationType));
    }

    [Fact]
    public void Chat_empty_a_definition_without_the_block_shows_none()
    {
        var info = EventLines.Info(Apo.Def(), null, T0);
        Assert.DoesNotContain(info, l => l.StartsWith("origins:", StringComparison.Ordinal) || l.StartsWith("hunters:", StringComparison.Ordinal));
    }
}
