using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D12: a cut by MaxUnitsPerWave or MaxTrackedUnits (a horde, a rise, a destination event's wave or
/// a spread draw) is named by the cap that set it, in the admin's start reply, `event info` and one log line; a player's
/// announcement never names a cfg key.</summary>
public class CapNoticeTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);
    const string Camp = "{ \"location\": { \"type\": \"Point\", \"x\": -1000, \"z\": -800 }, \"radius\": 20, \"name\": \"the camp\" }";

    static EventDefinition Destination() =>
        Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid", action: Json.ValidAction[..^2] + ", \"destination\": " + Camp + " }")),
            FakeUnits.Default(), regions: FakeRegions.All()).Set.All);

    static EventDefinition BigHordes(int size = 60) => Apo.Def(Apo.Action(hordes: Apo.Hordes(3, 1, 5, size, 0)));

    [Fact]
    public void Cap_fails_when_a_cut_is_silent()
    {
        // a horde rise: 60 asked, 50 a wave
        var d = BigHordes();
        var active = Apo.Active(d, T0);
        var run = Apo.Run(active);
        var w = new ApoWorld(maxTracked: 500, maxPerWave: 50);
        var rise = run.Rise(w, T0.AddMinutes(1), out _)!;
        Assert.Equal(new CapCut(50, 60, "MaxUnitsPerWave", 50), rise.Cut);
        Assert.Equal(rise.Cut, active.LastCut);
        Assert.Contains("capped: 50 of 60 by MaxUnitsPerWave 50; raise it in the cfg", EventLines.Info(d, active, T0.AddMinutes(1)));
        Assert.Equal("apo capped: 50 of 60 by MaxUnitsPerWave 50", rise.Cut!.LogLine("apo"));

        // the host logs the rise's cut once
        var game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(500, 50, 1000, 1000)), NativeRead = () => Natives() };
        var (host, tick) = Host(game, BigHordes());
        tick(T0.AddMinutes(1));
        Assert.Single(game.Logs, "apo capped: 50 of 60 by MaxUnitsPerWave 50");

        // a spread draw cut by the free slots: every cut has its line
        var full = Apo.Run(Apo.Active(Apo.Def(), T0));
        var tight = new ApoWorld(maxTracked: 1);
        var kill = new ScoreSides(null, new OurUnit("apo", 101, T0));
        full.SpreadOn(tight, kill, (0, 0), T0, out var cut);
        Assert.Equal(new CapCut(1, 2, "MaxTrackedUnits", 1), cut);
        full.SpreadOn(tight, kill, (0, 0), T0.AddSeconds(10), out var again);
        Assert.Equal(new CapCut(0, 2, "MaxTrackedUnits", 1), again);
        Assert.Equal(again, full.Active.LastCut);
        // asked is what roamers.max and the minute allowed: 1 left under max 4, none free, so 0 of 1
        var capped = Apo.Run(Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perHotspot: 1, perKill: 2, max: 3))), T0));
        var room = new ApoWorld(maxTracked: 2);
        capped.TopUp(room, T0);
        room.SpawnAll(capped, T0);
        capped.SpreadOn(room, kill, (0, 0), T0, out var one);
        Assert.Equal(new CapCut(0, 1, "MaxTrackedUnits", 2), one);
        // a draw cut only by roamers.max names no cfg key
        var loose = Apo.Run(Apo.Active(Apo.Def(Apo.Action(roamers: Apo.Roamers(perHotspot: 1, perKill: 2, max: 3))), T0));
        var wide = new ApoWorld(maxTracked: 500);
        loose.TopUp(wide, T0);
        wide.SpawnAll(loose, T0);
        loose.SpreadOn(wide, kill, (0, 0), T0, out var none);
        Assert.Null(none);

        // a destination event's wave: the decision that clamps it carries the cut, and WaveAction logs and keeps it
        var facts = new WaveFacts(1, "raid", false, AllowTerritory: true);
        IReadOnlyList<UnitEntry> Five() => [.. Enumerable.Range(0, 5).Select(_ => new UnitEntry("CHAR_Bandit_Thug", 1))];
        var wave = WaveGate.DecideGroupEntries(facts, [new GroupCentre(-1200.5f, 0, -800, false)], Five, 3, 0, 500);
        Assert.Equal(new CapCut(3, 5, "MaxUnitsPerWave", 3), wave.Cut);
        Assert.Equal(new CapCut(2, 5, "MaxTrackedUnits", 500), WaveGate.DecideGroupEntries(facts, [new GroupCentre(-1200.5f, 0, -800, false)], Five, 50, 498, 500).Cut);
        var two = WaveGate.DecideGroupEntries(facts, [new GroupCentre(0, 0, 0, false), new GroupCentre(100, 0, 0, false)], Five, 6, 0, 500);
        Assert.Equal(new CapCut(6, 10, "MaxUnitsPerWave", 6), two.Cut);
        var waveAction = File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Services", "WaveAction.cs")).Replace("\r\n", "\n");
        Assert.Contains("if (action.Destination is not null && decision.Cut is { } cut)", waveAction);
        Assert.Contains("active.LastCut = cut;\n            Core.Log.LogInfo($\"[nyar] {cut.LogLine(id)}\");", waveAction);

        // the start: an apocalypse's largest horde and a destination event's first wave
        Assert.Equal(new CapCut(50, 60, "MaxUnitsPerWave", 50), Precedence.StartCut(d, 50, 0, 500));
        Assert.Equal(new CapCut(3, 5, "MaxUnitsPerWave", 3), Precedence.StartCut(Destination(), 3, 0, 500));
        Assert.Equal("event raid started; capped: 3 of 5 by MaxUnitsPerWave 3; raise it in the cfg",
            AdminLines.Started("raid", Precedence.StartCut(Destination(), 3, 0, 500)).Human);
    }

    [Fact]
    public void Cap_fails_when_an_infestation_names_a_cap_its_rise_never_applies()
    {
        // 3 origins of 10: the rise queues all 30 under a MaxUnitsPerWave of 20 (RoamerPlan.TopUp has no wave cap); the
        // dev server's start said "capped: 20 of 30 by MaxUnitsPerWave 20" while 30 spawned
        var inf = Apo.Def(Inf.Action(origins: Inf.Origins(count: 3, perOrigin: 10), spread: Inf.Spread(cap: 200)));
        Assert.Null(Precedence.StartCut(inf, 20, 0, 300));
        // the free slots before the start cut it, named by MaxTrackedUnits
        Assert.Equal(new CapCut(12, 30, "MaxTrackedUnits", 300), Precedence.StartCut(inf, 20, 288, 300));
        // the rise is held to its own max first, which no cfg key names
        var small = Apo.Def(Inf.Action(origins: Inf.Origins(count: 3, perOrigin: 10), spread: Inf.Spread(cap: 25)));
        Assert.Null(Precedence.StartCut(small, 20, 275, 300));
        Assert.Equal(new CapCut(24, 25, "MaxTrackedUnits", 300), Precedence.StartCut(small, 20, 276, 300));
        // EventRuntime measures the free slots before the start queues its own units
        var runtime = File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Services", "EventRuntime.cs"));
        Assert.True(runtime.IndexOf("var occupied = SpawnTracker.Ledger.Occupied;", StringComparison.Ordinal) is var at and >= 0
            && at < runtime.IndexOf("var refused = Engine.Start(", StringComparison.Ordinal));
        Assert.Contains("Precedence.StartCut(active.Definition, ledger.Limits.MaxPerWave, occupied, ledger.Limits.MaxTracked)", runtime);
    }

    [Fact]
    public void Cap_fails_when_it_names_the_wrong_key_or_value()
    {
        Assert.Equal(new CapCut(50, 80, "MaxUnitsPerWave", 50), Precedence.Cut(80, 50, 0, 500));
        Assert.Equal(new CapCut(30, 80, "MaxTrackedUnits", 200), Precedence.Cut(80, 50, 170, 200));     // 30 free of 200
        Assert.Equal(new CapCut(0, 10, "MaxTrackedUnits", 200), Precedence.Cut(10, 50, 200, 200));
        Assert.Equal(new CapCut(40, 60, "MaxUnitsPerWave", 40), Precedence.Cut(60, 40, 150, 200));      // 50 free: the wave cap cuts first
        Assert.Equal("capped: 30 of 80 by MaxTrackedUnits 200", Precedence.Cut(80, 50, 170, 200)!.Text);
    }

    [Fact]
    public void Cap_fails_when_a_cut_reaches_a_player()
    {
        var game = new ApoGame { Ledger = new SpawnLedger(new LedgerLimits(500, 50, 1000, 1000)), NativeRead = () => Natives() };
        var (_, tick) = Host(game, BigHordes());
        for (var s = 0; s <= 600; s += 5) tick(T0.AddMinutes(1).AddSeconds(s));
        Assert.Contains(game.Logs, l => l.Contains("capped:"));
        Assert.NotEmpty(game.Announced);
        Assert.DoesNotContain(game.Announced, l => l.Contains("Max") || l.Contains("capped") || l.Contains("cfg"));
        // the start reply names the cut only for an admin; the player's start banner comes from Messages, which has no cfg key
        var runtime = EndPathTests.RuntimeSource().Replace("\r\n", "\n");
        Assert.Contains("AdminLines.Started(id, actor == Actor.Admin ? cut : null)", runtime);
        // the start's forecast is the reply only: the cut's one log line comes from the wave or horde it cuts
        var start = EndPathTests.Body(runtime, "internal static Outcome StartEvent(");
        Assert.DoesNotContain("cut.LogLine", start);
        Assert.DoesNotContain("LastCut", start);
        var messages = File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Logic", "Messages.cs"));
        Assert.DoesNotContain("MaxUnitsPerWave", messages);
        Assert.DoesNotContain("MaxTrackedUnits", messages);
    }

    [Fact]
    public void Cap_passes_an_uncut_start_says_only_started()
    {
        Assert.Equal("event apo started", AdminLines.Started("apo", Precedence.StartCut(Apo.Def(), 50, 0, 500)).Human);
        Assert.Equal("event raid started", AdminLines.Started("raid", Precedence.StartCut(Destination(), 50, 0, 500)).Human);
    }

    [Fact]
    public void Cap_empty_no_cut_no_line()
    {
        Assert.Null(Precedence.Cut(10, 50, 0, 500));
        Assert.Null(Precedence.Cut(0, 50, 500, 500));
        Assert.Null(Precedence.StartCut(Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default(), regions: FakeRegions.All()).Set.All), 1, 0, 500));
        var facts = new WaveFacts(1, "raid", false, AllowTerritory: true);
        Assert.Null(WaveGate.DecideGroupEntries(facts, [new GroupCentre(0, 0, 0, false)], () => [new UnitEntry("CHAR_Bandit_Thug", 1)], 3, 0, 500).Cut);          // neither an apocalypse nor a destination event
        var d = Apo.Def();
        var active = Apo.Active(d, T0);
        Apo.Run(active).Rise(new ApoWorld(), T0.AddMinutes(1), out _);
        Assert.Null(active.LastCut);
        Assert.DoesNotContain(EventLines.Info(d, active, T0.AddMinutes(1)), l => l.Contains("capped"));
    }

    static IEnumerable<HotspotNative> Natives() =>
        Enumerable.Range(0, 9).Select(i => new HotspotNative(new UnitFacts(Apo.Zombie, "Faction_Undead", false, false, false, false, false, null,
            () => "FarbaneWoods"), i < 5 ? i : 1000 + i, i));

    static (ApocalypseHost Host, Func<DateTime, ApocalypseStep> Tick) Host(ApoGame game, EventDefinition d)
    {
        var files = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(files, log.Add), () => files.Now, log.Add);
        var host = new ApocalypseHost();
        var engine = ApocalypseTests.Engine(out _, Apo.Event(action: Apo.Action(hordes: Apo.Hordes(3, 1, 5, d.Apocalypse!.Hordes.Size, 0))));
        var board = new Scoreboard();
        Assert.Null(engine.Start("apo", "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3),
            gate: x => host.Prepare(x, game)));
        var active = engine.Find("apo")!;
        Assert.True(host.Begin(active, game, state, board, T0));
        game.SpawnAll(host, T0);
        return (host, at =>
        {
            var step = host.Tick(active, game, state, board, at);
            game.SpawnAll(host, at);
            return step;
        });
    }
}
