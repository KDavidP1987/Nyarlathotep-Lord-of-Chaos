using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D19: no new log line or reply carries a player's name, platform id or position: the march lines,
/// the cut lines, the spread from a pool and `event info`'s march line, with the planted player holding the march, killing
/// at its own position and starting an Admin-destination event; and the infestation's and the horde march's lines (rise,
/// arrival, spread, status and info) with the planted player as the starting admin, the hunted player and the killer.</summary>
public partial class PrivacyTests
{
    /// <summary>The planted player stands at (4321.7, 8765.3).</summary>
    static readonly string[] UndeadHordesMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    static readonly DateTime UdhT0 = Zones.Utc(2026, 10, 31, 20, 0);
    static readonly (float X, float Z) PlantedSpot = (4321.7f, 8765.3f);

    static EventDefinition Marching(string destination) =>
        Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid", action: Json.ValidAction[..^2] + ", \"destination\": " + destination + " }")),
            FakeUnits.Default(), regions: FakeRegions.All()).Set.All);

    static List<string> UndeadHordesLines()
    {
        var lines = new List<string>();
        // a Point destination: the planted player holds the front, then it stalls, then another wave arrives
        var point = Marching("{ \"location\": { \"type\": \"Point\", \"x\": -1000, \"z\": -800 }, \"radius\": 20, \"name\": \"the camp\" }");
        var held = new WaveMarch("raid", 1, (-1200.5f, -800), (-1000, -800), 20, 2.0, UdhT0);
        held.Step(1, [(-1190f, -800f)], (_, _) => true, UdhT0.AddSeconds(1), 5, out var holdLine);
        lines.Add(holdLine ?? "");
        for (var s = 2; s <= 200; s++)
            if (held.Step(1, [PlantedSpot], (_, _) => false, UdhT0.AddSeconds(s), 5, out var h) is { } l) lines.Add(l);
            else if (h is not null) lines.Add(h);
        var arrive = new WaveMarch("raid", 2, (-1030, -800), (-1000, -800), 20, 2.0, UdhT0);
        for (var s = 1; s <= 10; s++)
            if (arrive.Step(1, [PlantedSpot], (_, _) => true, UdhT0.AddSeconds(s), 5, out _) is { } l) lines.Add(l);
        lines.AddRange(EventLines.Info(point, null, UdhT0));
        // an Admin destination: the planted player starts it, so the destination is their position; info never prints it
        var admin = Marching("{ \"location\": { \"type\": \"Admin\" }, \"radius\": 20, \"name\": \"the camp\" }");
        var origin = (PlantedSpot.X, 3f, PlantedSpot.Z);
        var active = new ActiveEvent(new RunningInstance(admin, UdhT0, UdhT0.AddSeconds(admin.DurationSeconds)), "manual", origin);
        lines.AddRange(EventLines.Info(admin, active, UdhT0.AddMinutes(1)));
        // the cuts, the start reply and a spread from a pool at the planted player's death spot
        var cut = Precedence.StartCut(point, 3, 0, 500)!;
        lines.Add(cut.LogLine("raid"));
        lines.Add(AdminLines.Started("raid", cut).Human);
        var apo = Apo.Def(Apo.Action(roamers: "{ \"perHotspot\": 3, \"perKillMin\": 1, \"perKillMax\": 3, \"max\": 12, \"perMinute\": 4, \"huntRange\": 40, \"pool\": \"elite\" }",
            extra: "\"pools\": { \"elite\": [ \"" + Apo.Deadeye + "\" ] }"));
        var run = Apo.Run(Apo.Active(apo, UdhT0));
        var w = new ApoWorld(maxTracked: 8);
        lines.AddRange(run.TopUp(w, UdhT0));
        for (var i = 0; i < 5; i++)
        {
            if (run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, UdhT0)), PlantedSpot, UdhT0.AddSeconds(i), out var c) is { } l) lines.Add(l);
            if (c is not null) lines.Add(c.LogLine("apo"));
        }
        lines.AddRange(EventLines.Info(apo, run.Active, UdhT0.AddSeconds(5)));
        return lines;
    }

    /// <summary>The log, banner, status and info lines of an infestation and a horde march whose destination is the planted
    /// player's position (they start it), who stands among the units, is hunted and kills them where they stand.</summary>
    static List<string> FamilyLines()
    {
        var lines = new List<string>();
        var open = new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);
        var origin = (PlantedSpot.X, 3f, PlantedSpot.Z);
        foreach (var action in new[]
        {
            Inf.Action(),
            Inf.March(start: "{ \"type\": \"Point\", \"x\": 4300, \"z\": 8700 }",
                destination: "{ \"location\": { \"type\": \"Admin\" }, \"radius\": 30, \"name\": \"the living\" }"),
        })
        {
            var rig = ApocalypseDependencyFailureTests.Make(action);
            Assert.Null(rig.Engine.Start("apo", "manual", UdhT0, open, origin, gate: d => rig.Host.Prepare(d, rig.Game, origin)));
            Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, UdhT0));
            rig.Game.Online.Add(Inf.Player(PlantedSpot.X, PlantedSpot.Z, (long)PlantedId));
            for (var s = 0; s <= 90; s += 5)
            {
                var now = UdhT0.AddSeconds(s);
                rig.Game.Budget.Reset();
                rig.Tick(now);
                rig.Game.SpawnAll(rig.Host, now);
                var wave = rig.Host.Run!.Mode == ApocalypseMode.Infestation ? Apocalypse.RoamerWave(1) : Apocalypse.HordeWave(1);
                rig.Host.After(new SpreadRead(Inf.KillBy(wave, UdhT0), PlantedSpot), rig.Game, now);
                lines.Add(rig.Host.StatusLine(rig.Active.Instance, rig.Game, now) ?? "");
                // Codex step 3 re-check: event info's run lines while the front nears the Admin destination
                lines.AddRange(EventLines.Info(rig.Active.Definition, rig.Active, now, apocalypse: rig.Host.Run!.View(rig.Game, (_, _) => "Farbane Woods")));
            }
            lines.AddRange(rig.Game.Logs);
            lines.AddRange(rig.Game.Announced);
            lines.AddRange(EventLines.Info(rig.Active.Definition, rig.Active, UdhT0.AddMinutes(2)));
        }
        lines.Add(ApocalypseHost.ArrivedLine("apo", "the living", 40, 11));
        // Codex step 3 F2: a horde that rises at the planted admin's spot (an Admin start) and stalls there at once
        var stuck = ApocalypseDependencyFailureTests.Make(Inf.March(start: "{ \"type\": \"Admin\" }"));
        Assert.Null(stuck.Engine.Start("apo", "manual", UdhT0, open, origin, gate: d => stuck.Host.Prepare(d, stuck.Game, origin)));
        Assert.True(stuck.Host.Begin(stuck.Active, stuck.Game, stuck.State, stuck.Board, UdhT0));
        stuck.Game.Ground = (_, _, _) => false;
        for (var s = 0; s <= 200 && stuck.Host.Run!.Arrived is false; s++)
        {
            stuck.Game.Budget.Reset();
            stuck.Tick(UdhT0.AddSeconds(s));
            if (s == 0) stuck.Game.SpawnAll(stuck.Host, UdhT0);
            if (s == 10)                                                       // code review step 3: event info's run lines too
                lines.AddRange(EventLines.Info(stuck.Active.Definition, stuck.Active, UdhT0.AddSeconds(s),
                    apocalypse: stuck.Host.Run!.View(stuck.Game, (_, _) => "Farbane Woods")));
        }
        Assert.True(stuck.Host.Run!.Arrived);
        lines.AddRange(stuck.Game.Logs);
        return lines;
    }

    [Fact]
    public void UndeadHordes_fails_when_an_infestation_or_horde_march_line_carries_a_name_id_or_position()
    {
        var lines = FamilyLines();
        Assert.Contains(lines, l => l.StartsWith("The dead rise in ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("horde apo: 10 units, 1 bosses, from ", StringComparison.Ordinal));
        Assert.Contains("The horde has reached the living!", lines);
        Assert.Contains(lines, l => l.StartsWith("horde apo: arrived at the living after ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("horde apo: stalled ", StringComparison.Ordinal) && l.EndsWith(" m short of Brighthaven; infesting there", StringComparison.Ordinal));
        Assert.Contains("hotspot 1: the starting admin", lines);
        Assert.Contains(lines, l => l.StartsWith("horde 1 front: ", StringComparison.Ordinal));
        // A5: a front that starts or ends at the admin is shown by its progress only; near the end its point is theirs
        Assert.DoesNotContain(lines, l => l.Contains(" front at ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("undead,", StringComparison.Ordinal) && l.EndsWith(" places", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("destination: the living, radius 30, at the starting admin", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in UndeadHordesMarks) Assert.False(line.Contains(mark, StringComparison.OrdinalIgnoreCase), $"'{line}' carries {mark}");
        // the check sees a leak when one is there: 0.14's first stall line, at the planted spot, named it (A5)
        Assert.Contains(UndeadHordesMarks, m => FormattableString.Invariant($"horde apo: stalled at {PlantedSpot.X:0}, {PlantedSpot.Z:0}").Contains(m, StringComparison.Ordinal));
    }

    [Fact]
    public void UndeadHordes_fails_when_a_name_id_or_position_reaches_a_line()
    {
        var lines = UndeadHordesLines();
        Assert.Contains("wave 1 held: no walkable ground ahead", lines);
        Assert.Contains(lines, l => l.StartsWith("march raid wave 1: stalled ", StringComparison.Ordinal) && l.EndsWith(" m short of its destination", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("march raid wave 2: arrived after ", StringComparison.Ordinal));
        Assert.Contains("march: from (-1200.5, -800) to the camp (-1000, -800) at 2 m/s", lines);
        Assert.Contains(lines, l => l.Contains("to the camp (the starting admin)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("capped: ", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in UndeadHordesMarks) Assert.False(line.Contains(mark, StringComparison.OrdinalIgnoreCase), $"'{line}' carries {mark}");
        // the check sees a leak when one is there
        Assert.Contains(UndeadHordesMarks, m => FormattableString.Invariant($"march raid wave 1: stalled at {PlantedSpot.X:0}, {PlantedSpot.Z:0}").Contains(m, StringComparison.Ordinal));
    }

    /// <summary>Codex step 2 F1, by reference to raphael-api-admin D15 (Twins_passes_location_here_value_only): `here`
    /// stores the admin's own position as a map point and echoes it as the value to that admin only, as `location here`
    /// and `goal here` do; the reply carries no name or id.</summary>
    [Fact]
    public void UndeadHordes_passes_destination_here_echoes_the_value_like_location_here()
    {
        var (r, who) = PlantedRig();
        r.Position = (4321.5f, 7f, -4321.5f);
        var line = r.Flows.ApiEvent(who, "set", "raid", "action.destination", "here", "");
        Assert.DoesNotContain(PlantedName, line);
        Assert.DoesNotContain(PlantedId.ToString(), line);
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=action.destination value=4321.5,-4321.5", line);
    }

    [Fact]
    public void UndeadHordes_passes_a_stall_names_the_front_never_the_player()
    {
        var m = new WaveMarch("raid", 1, (0, 0), (500, 0), 20, 2.0, UdhT0);
        string? line = null;
        for (var s = 1; s <= 130 && line is null; s++) line = m.Step(1, [PlantedSpot], (_, _) => false, UdhT0.AddSeconds(s), 4, out _);
        Assert.Equal("march raid wave 1: stalled 500 m short of its destination", line);
    }

    [Fact]
    public void UndeadHordes_empty_no_destination_no_march_line() =>
        Assert.DoesNotContain(EventLines.Info(Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default(), regions: FakeRegions.All()).Set.All),
            null, UdhT0), l => l.StartsWith("march:", StringComparison.Ordinal));
}
