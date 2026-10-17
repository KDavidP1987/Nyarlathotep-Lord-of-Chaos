using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D10, D11, D17, D20, D33: the horde march's rise at its start, its growth on the way, its arrival
/// or stall and the infestation after, the resume before and after arrival, the chat fields, and the unit watch leaving
/// both new types alone, over Logic/ApocalypseHost and the engine it drives.</summary>
public class HordeMarchTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    static readonly ControlState Open = new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    static ApocalypseDependencyFailureTests.Rig Started(string? action = null, (float X, float Y, float Z)? origin = null)
    {
        var rig = ApocalypseDependencyFailureTests.Make(action ?? Inf.March());
        Assert.Null(rig.Engine.Start("apo", "manual", T0, Open, origin, gate: d => rig.Host.Prepare(d, rig.Game, origin)));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        return rig;
    }

    /// <summary>A started march whose horde rose on its first tick and spawned.</summary>
    static ApocalypseDependencyFailureTests.Rig Risen(string? action = null, (float X, float Y, float Z)? origin = null)
    {
        var rig = Started(action, origin);
        Assert.Equal(1, rig.Tick(T0).Rose);
        rig.Game.SpawnAll(rig.Host, T0);
        return rig;
    }

    static List<TrackedUnit> Ours(ApocalypseDependencyFailureTests.Rig rig) => rig.Game.Ledger.Units.Where(u => Apocalypse.Ours(u, rig.Active)).ToList();

    static string Start(int minDistance) => $"{{ \"type\": \"Hotspot\", \"minDistance\": {minDistance} }}";

    // ---- D10 Rise

    [Fact]
    public void Rise_fails_when_the_horde_total_differs_from_its_entries_and_rolls()
    {
        var rig = Started();
        Assert.Empty(rig.Game.Ledger.Units);                                       // nothing tops up at the start
        var step = rig.Tick(T0);
        Assert.Equal(11, step.Queued);                                              // 8 + 2 and the boss
        rig.Game.SpawnAll(rig.Host, T0);
        Assert.Equal(8, Ours(rig).Count(u => u.Prefab == Apo.Zombie));
        Assert.Equal(2, Ours(rig).Count(u => u.Prefab == Apo.Thug));
        Assert.Single(Ours(rig), u => u.Prefab == Apo.Tourok && u.Wave == Apocalypse.BossWave(1, 1));
        Assert.Contains("horde apo: 10 units, 1 bosses, from Farbane Woods to Brighthaven", rig.Game.Logs);
        Assert.Contains(rig.Game.Announced, l => l.Contains("a horde of 11 rises in Farbane Woods and marches on Brighthaven", StringComparison.Ordinal));
        Assert.Null(rig.Tick(T0.AddMinutes(30)).Rose);                              // one horde only
        // a roll of 1-2 distinct bosses from a pool adds one slot per boss
        var pooled = Risen(Inf.March(extra: "\"pools\": { \"lords\": [ \"" + Apo.Tourok + "\", \"" + Apo.Deadeye + "\" ] }")
            .Replace("\"bosses\": [ " + Apo.BossJson() + " ]", "\"bosses\": [ { \"pool\": \"lords\", \"min\": 2, \"max\": 2 } ]"));
        Assert.Equal([Apo.Deadeye, Apo.Tourok], Ours(pooled).Where(u => u.Wave > 200).Select(u => u.Prefab).OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(12, Ours(pooled).Count);
        // code review step 3: the start's forecast counts a roll at its most; the rise line counts what was queued (the
        // schedule trims a unit, never the boss, to fit MaxTrackedUnits 10)
        Assert.Equal(new CapCut(11, 12, "MaxUnitsPerWave", 11), Precedence.StartCut(pooled.Active.Definition, 11, 0, 500));
        var tight = Started();
        var ten = tight with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(10, 50, 1000, 1000)) } };
        Assert.Equal(10, ten.Tick(T0).Queued);
        Assert.Contains("horde apo: 9 units, 1 bosses, from Farbane Woods to Brighthaven", ten.Game.Logs);
    }

    [Fact]
    public void Rise_fails_when_a_hotspot_start_is_nearer_than_minDistance_or_not_the_nearest_qualifying_one()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Inf.March(start: Start(300)));
        // the destination is (500, 0): 100 m (too near), 500 m, 1000 m away; and 4 natives at 350 m (too few)
        rig.Game.NativeRead = () => [.. Inf.Cluster(400, 0, 5), .. Inf.Cluster(0, 0, 5), .. Inf.Cluster(-500, 0, 6), .. Inf.Cluster(850, 0, 4)];
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        var start = Assert.Single(rig.Host.Run!.Hotspots);
        Assert.Equal((2f, 2f), (start.X, start.Z));
        Assert.True(Apocalypse.Distance(start.X, start.Z, 500, 0) >= 300);
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("apocalypse apo hotspots: 1 (5 at 2 2)", StringComparison.Ordinal));
        // none far enough: refused
        var none = ApocalypseDependencyFailureTests.Make(Inf.March(start: Start(2000)));
        none.Game.NativeRead = () => [.. Inf.Cluster(400, 0, 5), .. Inf.Cluster(0, 0, 5)];
        Assert.Equal("no Undead natives found at least 2000 m away", none.Start(T0)!.Human);
        Assert.Null(none.Engine.Find("apo"));
    }

    [Fact]
    public void Rise_fails_when_an_admin_destination_runs_without_an_admins_start()
    {
        var admin = Inf.March(destination: "{ \"location\": { \"type\": \"Admin\" }, \"radius\": 30, \"name\": \"the living\" }");
        var rig = ApocalypseDependencyFailureTests.Make(admin);
        var refused = rig.Engine.Start("apo", "schedule", T0, Open);
        Assert.Equal(Reasons.AdminLocation, refused!.Reason);
        Assert.Null(rig.Engine.Find("apo"));
        // an admin's start: the destination is where the admin stood
        var started = Risen(admin, (300f, 5f, 40f));
        Assert.Equal((300f, 5f, 40f), started.Host.Run!.Goal);
        Assert.Equal((300f, 40f), (started.Host.Run.Fronts[1].GoalX, started.Host.Run.Fronts[1].GoalZ));
        // an Admin start the same way, and never both Admin
        var adminStart = Inf.March(start: "{ \"type\": \"Admin\" }");
        Assert.Equal(Reasons.AdminLocation, ApocalypseDependencyFailureTests.Make(adminStart).Engine.Start("apo", "manual", T0, Open)!.Reason);
        var fromAdmin = Risen(adminStart, (-200f, 0f, 0f));
        Assert.Equal((-200f, 0f), (fromAdmin.Host.Run!.Hotspots[0].X, fromAdmin.Host.Run.Hotspots[0].Z));
        var both = Json.One(Apo.Event(action: Inf.March(start: "{ \"type\": \"Admin\" }",
            destination: "{ \"location\": { \"type\": \"Admin\" }, \"radius\": 30, \"name\": \"the living\" }")));
        Assert.Equal(EventValidator.BothAdmin, both.DisabledReason);
        var scheduled = Json.One(Apo.Event(action: adminStart, trigger: "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }"));
        Assert.Equal(EventValidator.StartAdminManual, scheduled.DisabledReason);
    }

    [Fact]
    public void Rise_fails_when_growth_passes_its_max_or_does_not_join_the_front()
    {
        var rig = Risen(Inf.March(growth: Inf.Spread(min: 3, max: 3, cap: 13)));
        var run = rig.Host.Run!;
        Assert.Equal(11, run.Living(rig.Game));
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.HordeWave(1), T0), (10, 0)), rig.Game, T0.AddSeconds(5));
        Assert.Equal(13, run.Living(rig.Game));                                   // 3 drawn, 2 fit the max of 13
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.HordeWave(1), T0), (10, 0)), rig.Game, T0.AddSeconds(6));
        Assert.Equal(13, run.Living(rig.Game));
        rig.Game.SpawnAll(rig.Host, T0.AddSeconds(6));
        var growth = Ours(rig).Where(u => u.Wave == Apocalypse.GrowthWave(1)).ToList();
        Assert.Equal(2, growth.Count);
        Assert.All(growth, u => Assert.Equal(1, Apocalypse.HordeOfWave(u.Wave)));    // they march with horde 1
        Assert.Null(Apocalypse.HotspotOfWave(Apocalypse.GrowthWave(1)));
        // the unexplained share: the rise, the growth and the boss, nothing else
        Assert.Equal(10 + 2 + 1, Ours(rig).Count);
        Assert.Equal(run.Tally.Queued, run.Living(rig.Game));
    }

    [Fact]
    public void Rise_passes_a_template_sized_horde_under_raised_caps()
    {
        var units = "[ " + Apo.Unit(count: "160") + ", " + Apo.Unit(Apo.Thug, "32") + ", " + Apo.Unit(Apo.Deadeye, "8") + " ]";
        var rig = ApocalypseDependencyFailureTests.Make(Inf.March(units: units));
        var big = rig with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(500, 500, 1000, 1000)), NativeRead = rig.Game.NativeRead } };
        Assert.Null(big.Engine.Start("apo", "manual", T0, Open, gate: d => big.Host.Prepare(d, big.Game)));
        Assert.True(big.Host.Begin(big.Active, big.Game, big.State, big.Board, T0));
        Assert.Equal(201, big.Tick(T0).Queued);
        Assert.Contains("horde apo: 200 units, 1 bosses, from Farbane Woods to Brighthaven", big.Game.Logs);
    }

    [Fact]
    public void Rise_empty_a_horde_cut_to_nothing_waits()
    {
        var rig = Started();
        var full = rig with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(0, 50, 1000, 1000)) } };
        Assert.Null(full.Tick(T0).Rose);
        Assert.Empty(full.Host.Run!.Fronts);
    }

    // ---- D11 Arrival

    [Fact]
    public void Arrival_fails_when_arrival_does_not_switch_the_rule_or_the_infest_spread_is_not_used()
    {
        var rig = Risen(Inf.March(start: "{ \"type\": \"Point\", \"x\": 440, \"z\": 0 }"));
        var run = rig.Host.Run!;
        Assert.False(run.Infesting);
        Assert.Equal(Apocalypse.GrowthWave(1), Wave(rig));                        // before arrival a kill grows the horde
        for (var s = 1; s <= 40 && !run.Arrived; s++) rig.Tick(T0.AddSeconds(s));
        Assert.True(run.Arrived);
        Assert.True(run.Infesting);
        Assert.Equal((500f, 0f), run.ArrivedAt);
        Assert.Equal([(500f, 0f)], run.Origins.Select(o => (o.X, o.Z)));
        Assert.Contains("The horde has reached Brighthaven!", rig.Game.Announced);
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("horde apo: arrived at Brighthaven after ", StringComparison.Ordinal) && l.EndsWith(" units; infesting", StringComparison.Ordinal));
        Assert.Same(run.Action.March!.InfestSpread, run.SpreadBlock);
        Assert.Equal(Apocalypse.RoamerWave(1), Wave(rig));                        // after it the infest spread
        // the infestation's rule: hunters and drifters, the front no longer marches
        rig.Game.Online.Add(Inf.Player(5000, 5000));
        var front = (run.Fronts[1].X, run.Fronts[1].Z);
        for (var s = 45; s <= 120; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.Equal(front, (run.Fronts[1].X, run.Fronts[1].Z));
        Assert.True(run.Roles.Hunters >= InfestRoles.Share(60, Ours(rig).Count) - 1);
        Assert.NotEmpty(rig.Game.Moves);
        Assert.Single(rig.Game.Announced, l => l == "The horde has reached Brighthaven!");   // once

        static int Wave(ApocalypseDependencyFailureTests.Rig r)
        {
            var before = r.Game.Ledger.PendingSpawns;
            r.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.HordeWave(1), T0), (480, 0)), r.Game, T0.AddMinutes(1));
            var order = r.Game.Ledger.TakeSpawns();
            Assert.NotEmpty(order);
            foreach (var o in order) { r.Game.Ledger.Fail(o); r.Host.Settled(o.EventId, o.Wave, false); }
            return order[0].Wave;
        }
    }

    [Fact]
    public void Arrival_fails_when_a_stall_does_not_switch_the_rule()
    {
        var rig = Risen();
        rig.Game.Ground = (x, _, _) => x < 40;                                    // no ground past 40 m
        var run = rig.Host.Run!;
        var s = 1;
        for (; s <= 400 && !run.Arrived; s++)
        {
            rig.Game.Budget.Reset();
            rig.Tick(T0.AddSeconds(s));
        }
        Assert.True(run.Arrived);
        Assert.InRange(s, 120, 200);                                              // 120 s held by the ground
        var (x, z) = run.ArrivedAt!.Value;
        Assert.True(x < 100, $"stalled at {x}");                               // short of the 500 m goal, near the edge
        Assert.Contains(ApocalypseHost.StalledLine("apo", (int)Math.Round(Apocalypse.Distance(x, z, 500, 0)), "Brighthaven"), rig.Game.Logs);
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("horde apo: stalled 4", StringComparison.Ordinal) && l.EndsWith(" m short of Brighthaven; infesting there", StringComparison.Ordinal));
        Assert.DoesNotContain(rig.Game.Announced, l => l.Contains("has reached", StringComparison.Ordinal));
        Assert.Equal([(x, z)], run.Origins.Select(o => (o.X, o.Z)));
        Assert.True(run.Infesting);

    }

    [Fact]
    public void Arrival_fails_when_an_outcome_differs()
    {
        var rig = Risen();
        Assert.Equal(ApocalypseResult.None, rig.Tick(T0.AddSeconds(1)).Result);
        foreach (var u in Ours(rig)) rig.Game.Ledger.Forget(u.Key);
        Assert.Equal(ApocalypseResult.Won, rig.Tick(T0.AddSeconds(2)).Result);    // destroyed before arrival
        Assert.Equal("event apo ended: won (horde destroyed)", ApocalypseOutcome.Line("apo", ApocalypseResult.Won, 1, 1, ApocalypseMode.HordeMarch));
        var before = Started();
        Assert.Equal(ApocalypseResult.None, before.Host.Run!.Outcome(before.Game, _ => [], T0));   // nothing rose yet: not won
        Assert.Equal(ApocalypseResult.Survived, before.Host.Run.Outcome(before.Game, _ => [], T0.AddHours(1)));
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("horde apo: 10 units", StringComparison.Ordinal));
        // code review step 3: a horde whose every order failed is no win
        var lost = Started();
        Assert.Equal(11, lost.Tick(T0).Queued);
        foreach (var o in lost.Game.Ledger.TakeSpawns()) { lost.Game.Ledger.Fail(o); lost.Host.Settled(o.EventId, o.Wave, false); }
        Assert.Equal(ApocalypseResult.None, lost.Tick(T0.AddSeconds(1)).Result);
        Assert.Equal(ApocalypseResult.None, lost.Tick(T0.AddMinutes(5)).Result);
    }

    [Fact]
    public void Arrival_passes_kills_after_arrival_count_on_the_scoreboard()
    {
        var rig = Risen(Inf.March(start: "{ \"type\": \"Point\", \"x\": 490, \"z\": 0 }"));
        rig.Tick(T0.AddSeconds(1));
        Assert.True(rig.Host.Run!.Arrived);
        Assert.True(SpreadGate.Qualifies(Inf.KillBy(Apocalypse.RoamerWave(1), T0), rig.Active));
    }

    [Fact]
    public void Arrival_empty_an_apocalypse_never_arrives()
    {
        var run = Apo.Run(Apo.Active(Apo.Def(), T0));
        Assert.False(run.Arrive(500, 0));
        Assert.False(run.Infesting);
        Assert.Empty(run.Origins);
        Assert.Null(run.Infest);
    }

    // ---- D17 Resume

    [Fact]
    public void Resume_fails_when_a_resumed_run_loses_its_front_or_arrival()
    {
        var rig = Risen();
        rig.Host.After(new SpreadRead(Inf.KillBy(Apocalypse.HordeWave(1), T0), (10, 0)), rig.Game, T0.AddSeconds(2));
        rig.Game.SpawnAll(rig.Host, T0.AddSeconds(2));
        for (var s = 3; s <= 20; s++) rig.Tick(T0.AddSeconds(s));
        var run = rig.Host.Run!;
        var record = run.Record(rig.Game, [], T0.AddSeconds(20));
        Assert.Equal("HordeMarch", record.Mode);
        Assert.False(record.Arrived);
        var h = Assert.Single(record.Hordes);
        Assert.Equal((run.Fronts[1].X, run.Fronts[1].Z), (h.FrontX, h.FrontZ));
        Assert.Equal(run.LivingOf(rig.Game, Apocalypse.GrowthWave(1)), h.Growth);
        Assert.Equal(run.Living(rig.Game), h.Living);
        var back = StateDocument.TryParse(new StateDocument { Apocalypse = record }.Serialize())!.Apocalypse!;
        Assert.Null(back.Unreadable);
        var resumed = ApocalypseRun.Resumed(rig.Active, back, T0.AddMinutes(1));
        Assert.Equal(ApocalypseMode.HordeMarch, resumed.Mode);
        Assert.Equal((h.FrontX, h.FrontZ), (resumed.Fronts[1].X, resumed.Fronts[1].Z));
        var w = new ApoWorld();
        Assert.Equal(h.Living, resumed.Rebuild(w, back, T0.AddMinutes(1)).Rebuilt);
        Assert.Equal(h.Growth, w.Ledger.PendingSpawns > 0 ? resumed.LivingOf(w, Apocalypse.GrowthWave(1)) : -1);
        // after arrival: the arrival point comes back and the run infests from it
        run.Arrive(500, 0);
        var arrived = StateDocument.TryParse(new StateDocument { Apocalypse = run.Record(rig.Game, [], T0.AddMinutes(2)) }.Serialize())!.Apocalypse!;
        Assert.True(arrived.Arrived);
        Assert.Equal((500f, 0f), (arrived.Arrival!.X, arrived.Arrival.Z));
        var again = ApocalypseRun.Resumed(rig.Active, arrived, T0.AddMinutes(3));
        Assert.True(again.Arrived);
        Assert.True(again.Infesting);
        Assert.Equal([(500f, 0f)], again.Origins.Select(o => (o.X, o.Z)));
        // a record that says arrived without its point is refused
        arrived.Arrival = null;
        Assert.Equal("arrival missing", arrived.Problem());
    }

    [Fact]
    public void Resume_passes_growth_within_the_living_count()
    {
        var record = Risen().Host.Run!.Record(new ApoWorld(), [], T0);
        record.Hordes.Add(new RecordHorde { Number = 1, Living = 3, Growth = 4 });
        Assert.Equal("a horde's growth is out of range", record.Problem());
    }

    [Fact]
    public void Resume_empty_a_march_with_no_horde_records_none()
    {
        var record = Started().Host.Run!.Record(new ApoWorld(), [], T0);
        Assert.Empty(record.Hordes);
        Assert.Null(record.Problem());
    }

    // ---- D20 Chat

    static JsonObject ActionOf(Library lib) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == "hm")["action"]!;

    [Fact]
    public void Chat_fails_when_a_field_cannot_be_set_or_writes_another_field()
    {
        var lib = new Library(Json.File(Apo.Event("hm", action: Inf.March())));
        Assert.True(lib.Catalog.Current.Find("hm")!.Startable, lib.Catalog.Current.Find("hm")!.DisabledReason);
        var sets = new (string Field, string Value, string Path, string Json)[]
        {
            ("action.growth.perKillMin", "2", "growth.perKillMin", "2"),
            ("action.growth.max", "200", "growth.max", "200"),
            ("action.growth.perMinute", "15", "growth.perMinute", "15"),
            ("action.infest.hunters", "40", "infest.hunters", "40"),
            ("action.infest.driftRadius", "600", "infest.driftRadius", "600"),
            ("action.infest.spread.perKillMax", "2", "infest.spread.perKillMax", "2"),
            ("action.infest.spread.max", "250", "infest.spread.max", "250"),
            ("action.destination.radius", "40", "destination.radius", "40"),
            ("action.destination.name", "the square", "destination.name", "\"the square\""),
            ("action.marchSpeed", "3.5", "marchSpeed", "3.5"),
        };
        foreach (var (field, value, path, json) in sets)
        {
            var before = JsonNode.Parse(ActionOf(lib).ToJsonString())!.AsObject();
            Assert.StartsWith($"event hm {field} = {value}", AuthoringTests.Set(lib, "hm", field, value));
            var parts = path.Split('.');
            JsonNode was = before;
            foreach (var p in parts[..^1]) was = was[p]!;
            ((JsonObject)was)[parts[^1]] = JsonNode.Parse(json);
            Assert.Equal(before.ToJsonString(), ActionOf(lib).ToJsonString());                    // that key and nothing else
            Assert.True(lib.Catalog.Current.Find("hm")!.Startable, field);
        }
        Assert.StartsWith("event hm action.start.minDistance = 400", AuthoringTests.Set(lib, "hm", "action.start.minDistance", "400"));
        Assert.Equal("{\"type\":\"Hotspot\",\"minDistance\":400}", ActionOf(lib)["start"]!.ToJsonString());
        Assert.StartsWith("event hm action.start = ", AuthoringTests.Set(lib, "hm", "action.start", "here", (-50f, 3f, 25f)));
        Assert.Equal("{\"type\":\"Point\",\"x\":-50.0,\"y\":3.0,\"z\":25.0}", ActionOf(lib)["start"]!.ToJsonString());
        Assert.StartsWith("event hm action.bosses = ", AuthoringTests.Set(lib, "hm", "action.bosses", "pool:lords:1-2"));
        Assert.True(lib.Catalog.Current.Find("hm")!.DisabledReason is { } why && why.Contains("no pool", StringComparison.Ordinal));   // the roll waits for its pool
        Assert.StartsWith("event hm action.bosses = ", AuthoringTests.Set(lib, "hm", "action.bosses", Apo.Tourok + ":1"));
        Assert.True(lib.Catalog.Current.Find("hm")!.Startable);
        // refusals
        Assert.Equal(EventValidator.DestinationObject, AuthoringTests.Set(lib, "hm", "action.destination", "none"));
        Assert.Equal(EventValidator.StartDistanceRule, AuthoringTests.Set(lib, "hm", "action.start.minDistance", "50"));
        Assert.Equal(CommandArgs.StartHereRule, AuthoringTests.Set(lib, "hm", "action.start", "0 0"));
        Assert.Equal(EventValidator.SpreadKillMaxRule("action.infest.spread"), AuthoringTests.Set(lib, "hm", "action.infest.spread.perKillMax", "4"));
        Assert.Equal("action.origins.count is not a HordeMarch field", AuthoringTests.Set(lib, "hm", "action.origins.count", "3"));
        Assert.Equal("action.goal.radius is not a HordeMarch field", AuthoringTests.Set(lib, "hm", "action.goal.radius", "30"));
        Assert.Equal("action.start.minDistance is not an Apocalypse field", AuthoringTests.Set(new Library(Json.File(Apo.Event("apo"))), "apo", "action.start.minDistance", "400"));
    }

    [Fact]
    public void Chat_fails_when_a_status_or_info_line_differs()
    {
        var gathering = Started();
        Assert.Equal("Night of the dead: the horde is gathering", gathering.Host.StatusLine(gathering.Active.Instance, gathering.Game, T0));   // code review step 3
        var rig = Risen();
        Assert.Equal("Night of the dead: horde 11 units, 500 m to Brighthaven", rig.Host.StatusLine(rig.Active.Instance, rig.Game, T0.AddSeconds(1)));
        rig.Host.Run!.Arrive(500, 0);
        Assert.Equal("Night of the dead: horde 11 units, arrived", rig.Host.StatusLine(rig.Active.Instance, rig.Game, T0.AddSeconds(2)));
        var info = EventLines.Info(rig.Active.Definition, null, T0);
        Assert.Contains("action: horde march of the Undead, march 2 m/s", info);
        Assert.Contains("start: at 0 0", info);
        Assert.Contains("destination: Brighthaven, radius 20, at 500 0 height 12", info);
        Assert.Contains("growth: +1-3 per kill, max 30, 10 per minute", info);
        Assert.Contains("infest: hunters 60 %, drift radius 300 m, spread +1-3 per kill, max 40, 10 per minute", info);
        Assert.Contains($"horde of 10: {Apo.Zombie} ×8, {Apo.Thug} ×2", info);
        Assert.Contains(info, l => l.StartsWith($"horde adds: {Apo.Tourok}", StringComparison.Ordinal));
        var hotspot = EventLines.Info(Apo.Def(Inf.March(start: Start(300))), null, T0);
        Assert.Contains("start: the natives' hotspot nearest the destination, at least 300 m away", hotspot);
        Assert.All(info, l => Assert.True(System.Text.Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Chat_passes_a_template_copy()
    {
        var c = TemplateLibraryTests.Real();
        Assert.Null(c.Find("undead-horde-march")!.Invalid);
        var info = EventLines.Info(c.Find("undead-horde-march")!.Definition, null, T0);
        Assert.Contains(info, l => l.StartsWith("horde adds: CHAR_Undead_", StringComparison.Ordinal));
        Assert.Contains("destination: the living, radius 30, at the starting admin", info);
    }

    [Fact]
    public void Chat_empty_an_apocalypse_shows_no_march_lines()
    {
        var info = EventLines.Info(Apo.Def(), null, T0);
        Assert.DoesNotContain(info, l => l.StartsWith("start:", StringComparison.Ordinal) || l.StartsWith("infest:", StringComparison.Ordinal));
    }

    // ---- D33 Stall

    [Fact]
    public void Stall_fails_when_an_infestation_or_a_horde_march_has_a_watched_wave()
    {
        foreach (var action in new[] { Inf.Action(), Inf.March() })
        {
            var active = Apo.Active(Apo.Def(action), T0);
            Assert.Empty(StallClock.Waves(active, 1));
            Assert.Empty(StallClock.Waves(active, 10));
        }
        Assert.Equal([1, 2, 3], StallClock.Waves(Apo.Active(Apo.Def(), T0), 3));               // the apocalypse unchanged (D21)
    }

    [Fact]
    public void Stall_passes_an_apocalypse_watches_its_growth_slots_too()
    {
        var a = Apo.Active(Apo.Def(), T0);
        Assert.Contains(Apocalypse.BossWave(1, 1), StallClock.LedgerWaves(a, 1));
        Assert.Equal([1], StallClock.LedgerWaves(Apo.Active(Json.One(Json.Event("raid")), T0), 1));
    }

    [Fact]
    public void Stall_empty_a_quiet_unrisen_apocalypse_has_none() =>
        Assert.Empty(StallClock.Waves(Apo.Active(Apo.Def(), T0), 0));
}
