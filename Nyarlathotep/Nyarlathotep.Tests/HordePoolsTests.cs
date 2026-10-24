using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>horde-pools D1, D3, D4: a horde of the three horde types (HordeMarch, Infestation, Apocalypse) is held by the
/// free MaxTrackedUnits slots alone, never by MaxUnitsPerWave; with `action.allowTerritory` off no target, move, spawn or
/// start lies on claimed castle ground and a march stops at its edge; with it on, all of them may.</summary>
public class HordePoolsTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    static readonly ControlState Open = new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);
    const string Allow = "\"allowTerritory\": true";

    /// <summary>The claimed blocks covering x0..x1, z0..z1.</summary>
    static HashSet<(int X, int Z)> Box(float x0, float x1, float z0, float z1)
    {
        var blocks = new HashSet<(int X, int Z)>();
        for (var x = Territory.Block(x0); x <= Territory.Block(x1); x++)
            for (var z = Territory.Block(z0); z <= Territory.Block(z1); z++) blocks.Add((x, z));
        return blocks;
    }

    static ApocalypseDependencyFailureTests.Rig Started(string action)
    {
        var rig = ApocalypseDependencyFailureTests.Make(action);
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        return rig;
    }

    static ApocalypseDependencyFailureTests.Rig Running(string? action = null)
    {
        var rig = Started(action ?? Apo.Action());
        rig.Game.SpawnAll(rig.Host, T0);
        return rig;
    }

    /// <summary>A horde march held at the border of a castle claiming 300-700 m on its way: when it arrives, and where.</summary>
    static (int Seconds, (float X, float Z) At, ApocalypseDependencyFailureTests.Rig Rig) MarchInto(bool allow)
    {
        var rig = Started(Inf.March(extra: allow ? Allow : null));
        rig.Game.Claimed = Box(300, 700, -200, 200);
        Assert.Equal(1, rig.Tick(T0).Rose);
        rig.Game.SpawnAll(rig.Host, T0);
        var run = rig.Host.Run!;
        var s = 1;
        for (; s <= 900 && !run.Arrived; s++)
        {
            rig.Game.Budget.Reset();
            rig.Tick(T0.AddSeconds(s));
        }
        Assert.True(run.Arrived);
        return (s, run.ArrivedAt!.Value, rig);
    }

    static string? Reason(string ev) =>
        Assert.Single(EventValidator.Parse(Json.File(ev), FakeUnits.Default(), regions: FakeRegions.All()).Set.All).DisabledReason;

    static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine([ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", .. path])).Replace("\r\n", "\n");

    // ---- D1 Caps

    [Fact]
    public void Caps_fails_when_a_horde_is_held_by_the_wave_cap_or_passes_the_tracked_slots()
    {
        // a HordeMarch of 200 with MaxUnitsPerWave 20 and 500 free slots queues all 200 and its boss
        var rig = Started(Inf.March(units: "[ " + Apo.Unit(count: "200") + " ]"));
        var wide = rig with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(500, 20, 1000, 1000)) } };
        Assert.Equal(201, wide.Tick(T0).Queued);
        Assert.Equal(201, wide.Game.Ledger.Occupied);                                // every unit a ledger order
        // past the free tracked slots it stops there
        var held = Started(Inf.March(units: "[ " + Apo.Unit(count: "200") + " ]"));
        var fifty = held with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(50, 20, 1000, 1000)) } };
        Assert.Equal(50, fifty.Tick(T0).Queued);
        Assert.Equal(50, fifty.Game.Ledger.Occupied);
        // an Apocalypse's horde of 100 rises whole under MaxUnitsPerWave 20, and only the slots hold it
        var apo = Apo.Def(Apo.Action(hordes: Apo.Hordes(3, 1, 5, 100, 0)));
        var open = new ApoWorld(maxTracked: 500, maxPerWave: 20);
        Assert.Equal(100, Apo.Run(Apo.Active(apo, T0)).Rise(open, T0.AddMinutes(1), out _)!.Queued);
        Assert.Equal(100 + 6, open.Ledger.PendingSpawns);                           // and 3 roamers at each hotspot
        var tight = new ApoWorld(maxTracked: 30, maxPerWave: 20);
        Assert.Equal(30, Apo.Run(Apo.Active(apo, T0)).Rise(tight, T0.AddMinutes(1), out _)!.Queued);
        Assert.True(tight.Ledger.Occupied <= 30);
        // an Infestation's rise of 2 x 4 under MaxUnitsPerWave 1
        var inf = Apo.Def(Inf.Action());
        Assert.Null(Precedence.StartCut(inf, 1, 0, 500));
        // a SpawnWaves wave is still held by MaxUnitsPerWave, through the ledger and the start's forecast
        var ledger = new SpawnLedger(new LedgerLimits(500, 20, 1000, 1000));
        Assert.Equal(20, ledger.Request("CHAR_Bandit_Thug", "raid", 50, new UnitLifetime(T0.AddMinutes(5), 400), UnitTuning.None, _ => (0, 0, 0)).Queued);
        var facts = new WaveFacts(1, "raid", false, AllowTerritory: true);
        IReadOnlyList<UnitEntry> Five() => [.. Enumerable.Range(0, 5).Select(_ => new UnitEntry("CHAR_Bandit_Thug", 1))];
        Assert.Equal(new CapCut(3, 5, "MaxUnitsPerWave", 3), WaveGate.DecideGroupEntries(facts, [new GroupCentre(0, 0, 0, false)], Five, 3, 0, 500).Cut);
        // a horde type's cut names MaxTrackedUnits, never MaxUnitsPerWave
        Assert.Equal(new CapCut(50, 201, "MaxTrackedUnits", 50), Precedence.StartCut(Apo.Def(Inf.March(units: "[ " + Apo.Unit(count: "200") + " ]")), 1, 0, 50));
        Assert.Equal(new CapCut(50, 100, "MaxTrackedUnits", 50), Precedence.StartCut(apo, 1, 0, 50));
        Assert.Equal(new CapCut(5, 8, "MaxTrackedUnits", 5), Precedence.StartCut(inf, 1, 0, 5));
    }

    [Fact]
    public void Caps_passes_a_horde_within_both_caps_uncut()
    {
        var rig = Started(Inf.March(units: "[ " + Apo.Unit(count: "200") + " ]"));
        var wide = rig with { Game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(500, 500, 1000, 1000)) } };
        Assert.Equal(201, wide.Tick(T0).Queued);
        Assert.Null(Precedence.StartCut(rig.Active.Definition, 500, 0, 500));
    }

    [Fact]
    public void Caps_empty_a_full_ledger_raises_no_horde()
    {
        var w = new ApoWorld(maxTracked: 1, maxPerWave: 20);
        w.Ledger.Request(Apo.Zombie, "other", 1, new UnitLifetime(T0.AddMinutes(5), 400), UnitTuning.None, _ => (0, 0, 0));
        var apo = Apo.Def(Apo.Action(hordes: Apo.Hordes(3, 1, 5, 100, 0)));
        Assert.Null(Apo.Run(Apo.Active(apo, T0)).Rise(w, T0.AddMinutes(1), out _));
        Assert.Equal(new CapCut(0, 100, "MaxTrackedUnits", 1), Precedence.StartCut(apo, 20, 1, 1));
    }

    // ---- D3 TerritoryOff

    [Fact]
    public void TerritoryOff_fails_when_a_castle_is_targeted_entered_or_started_in()
    {
        // a Point start on claimed ground is refused, and the switch on accepts it
        var claimedStart = ApocalypseDependencyFailureTests.Make(Inf.March());
        claimedStart.Game.Claimed = Box(-20, 20, -20, 20);
        Assert.Equal(ApocalypseHost.ClaimedStart, claimedStart.Start(T0)?.Human);
        var allowed = ApocalypseDependencyFailureTests.Make(Inf.March(extra: Allow));
        allowed.Game.Claimed = Box(-20, 20, -20, 20);
        Assert.Null(allowed.Start(T0));
        // a claimed hotspot is skipped and counted; none left refuses
        var skip = ApocalypseDependencyFailureTests.Make();
        skip.Game.Claimed = Box(-20, 20, -20, 20);
        Assert.Null(skip.Start(T0));
        Assert.Contains(ApocalypseHost.SkippedLine("apo", 1), skip.Game.Logs);
        Assert.True(skip.Host.Begin(skip.Active, skip.Game, skip.State, skip.Board, T0));
        Assert.Equal([(1, 1000f)], skip.Host.Run!.Hotspots.Select(h => (h.Number, MathF.Round(h.X / 10) * 10)));
        var none = ApocalypseDependencyFailureTests.Make();
        none.Game.Claimed = Box(-20, 1020, -20, 20);
        Assert.Equal(ApocalypseHost.NoUnclaimedHotspot, none.Start(T0)?.Human);
        // no spawn lands on a claimed block
        var w = new ApoWorld(maxTracked: 500) { Claimed = (x, _) => x > 0 };
        Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(3, 1, 5, 40, 0))), T0)).Rise(w, T0.AddMinutes(1), out _);
        var orders = w.Ledger.TakeSpawns();
        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.True(o.X <= 0, $"spawned at {o.X}"));
        // a roamer never targets a player in a castle
        var rig = Running();
        rig.Game.Claimed = Box(250, 350, -50, 50);
        rig.Game.Online.Add(Inf.Player(300, 0));
        for (var s = 5; s <= 30; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.DoesNotContain(rig.Game.Logs, l => l.Contains("on a player", StringComparison.Ordinal));
        Assert.DoesNotContain(rig.Game.Moves, m => Territory.IsClaimed(rig.Game.Claimed, m.X, m.Z));
        // a march stops at the edge and counts as arrived there, never stepping onto claimed ground
        var (seconds, at, march) = MarchInto(false);
        Assert.True(at.X < 300, $"arrived at {at.X}");
        Assert.True(seconds >= 120, $"arrived after {seconds} s");
        Assert.DoesNotContain(march.Game.Moves, m => Territory.IsClaimed(march.Game.Claimed!, m.X, m.Z));
        // the hunt layer reads the same switch: off keeps a castle player out of the apocalypse family's candidates
        var hunt = Source("Services", "HuntAction.cs");
        Assert.Contains("apocalypse.AllowTerritory ? candidates.Select(c => c with { InTerritory = false }) : candidates", hunt);
        Assert.Contains("return new EventTargeting((apocalypse.AllowTerritory", hunt);
    }

    // ---- D4 TerritoryOn

    [Fact]
    public void TerritoryOn_fails_when_a_castle_stays_closed_with_the_switch_on()
    {
        // spawns land on claimed blocks
        var w = new ApoWorld(maxTracked: 500) { Claimed = (x, _) => x > 0 };
        Apo.Run(Apo.Active(Apo.Def(Apo.Action(hordes: Apo.Hordes(3, 1, 5, 40, 0), extra: Allow)), T0)).Rise(w, T0.AddMinutes(1), out _);
        var orders = w.Ledger.TakeSpawns();
        Assert.Equal(40 + 6, orders.Count);                                       // the horde and 3 roamers at each hotspot
        Assert.Contains(orders, o => o.X > 0);
        // a player in a castle is the roam's target
        var rig = Running(Apo.Action(extra: Allow));
        rig.Game.Claimed = Box(250, 350, -50, 50);
        rig.Game.Online.Add(Inf.Player(300, 0));
        for (var s = 5; s <= 30; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.Contains(rig.Game.Logs, l => l.Contains("on a player", StringComparison.Ordinal));
        // a march walks into the castle to its destination
        var (_, at, _) = MarchInto(true);
        Assert.True(Apocalypse.Distance(at.X, at.Z, 500, 0) <= 40, $"arrived at {at.X}");
        // the hunt layer seeds castle players and the unit watch's landings may be claimed
        Assert.Contains("apocalypse.AllowTerritory ? candidates.Select(c => c with { InTerritory = false })", Source("Services", "HuntAction.cs"));
        Assert.Contains("action?.AllowTerritory ?? a.Definition.Apocalypse?.AllowTerritory ?? false", Source("Logic", "UnitWatch.cs"));
        // events.json: a bool on the three types, anything else refused
        foreach (var action in new[] { Apo.Action(extra: Allow), Inf.Action(extra: Allow), Inf.March(extra: Allow) })
            Assert.True(Apo.Def(action).Apocalypse!.AllowTerritory);
        Assert.False(Apo.Def(Inf.March()).Apocalypse!.AllowTerritory);
        foreach (var action in new[] { Apo.Action(extra: "\"allowTerritory\": 1"), Inf.Action(extra: "\"allowTerritory\": \"yes\""), Inf.March(extra: "\"allowTerritory\": null") })
            Assert.Equal("action.allowTerritory must be true or false", Reason(Apo.Event(action: action)));
        // chat: true and false on each type (none refused, as on SpawnWaves; A1)
        foreach (var action in new[] { Apo.Action(), Inf.Action(), Inf.March() })
        {
            var lib = new Library(Json.File(Apo.Event("hm", action: action)));
            Assert.StartsWith("event hm action.allowTerritory = true", AuthoringTests.Set(lib, "hm", "action.allowTerritory", "true"));
            Assert.True(lib.Catalog.Current.Find("hm")!.Apocalypse!.AllowTerritory);
            Assert.StartsWith("event hm action.allowTerritory = false", AuthoringTests.Set(lib, "hm", "action.allowTerritory", "false"));
            Assert.False(lib.Catalog.Current.Find("hm")!.Apocalypse!.AllowTerritory);
            Assert.Equal("action.allowTerritory must be true or false", AuthoringTests.Set(lib, "hm", "action.allowTerritory", "none"));
        }
        // event info names the switch only when on
        Assert.Contains("claimed territory allowed", EventLines.Info(Apo.Def(Inf.March(extra: Allow)), null, T0));
        Assert.DoesNotContain("claimed territory allowed", EventLines.Info(Apo.Def(Inf.March()), null, T0));
    }

    [Fact]
    public void TerritoryOff_passes_a_march_with_no_castle_on_its_way()
    {
        var rig = Started(Inf.March());
        Assert.Equal(1, rig.Tick(T0).Rose);
        rig.Game.SpawnAll(rig.Host, T0);
        var run = rig.Host.Run!;
        for (var s = 1; s <= 900 && !run.Arrived; s++)
        {
            rig.Game.Budget.Reset();
            rig.Tick(T0.AddSeconds(s));
        }
        Assert.True(run.Arrived);
        Assert.True(Apocalypse.Distance(run.ArrivedAt!.Value.X, run.ArrivedAt.Value.Z, 500, 0) <= 40);
    }

    [Fact]
    public void TerritoryOff_empty_no_claimed_ground_skips_nothing()
    {
        var rig = ApocalypseDependencyFailureTests.Make();
        Assert.Null(rig.Start(T0));
        Assert.DoesNotContain(rig.Game.Logs, l => l.Contains("skipped inside a castle", StringComparison.Ordinal));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        Assert.Equal(2, rig.Host.Run!.Hotspots.Count);
    }

    [Fact]
    public void TerritoryOn_passes_a_claimed_hotspot_kept_with_the_switch_on()
    {
        var rig = ApocalypseDependencyFailureTests.Make(Apo.Action(extra: Allow));
        rig.Game.Claimed = Box(-20, 20, -20, 20);
        Assert.Null(rig.Start(T0));
        Assert.DoesNotContain(rig.Game.Logs, l => l.Contains("skipped inside a castle", StringComparison.Ordinal));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        Assert.Equal(2, rig.Host.Run!.Hotspots.Count);
    }

    [Fact]
    public void TerritoryOn_empty_an_unread_map_leaves_the_start_to_the_runs_own_map()
    {
        // the start's read failed: the start goes on, and the run's map (fail closed) keeps its units off every point
        var rig = ApocalypseDependencyFailureTests.Make(Inf.March());
        rig.Game.Claimed = null;
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Game.IsClaimed("apo", 0, 0));
    }
}
