using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D19: no new log line or reply carries a player's name, platform id or position: the march lines,
/// the cut lines, the spread from a pool and `event info`'s march line, with the planted player holding the march, killing
/// at its own position and starting an Admin-destination event. Step 3 adds the infestation's and the horde march's lines.</summary>
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

    [Fact]
    public void UndeadHordes_fails_when_a_name_id_or_position_reaches_a_line()
    {
        var lines = UndeadHordesLines();
        Assert.Contains("wave 1 held: no walkable ground ahead", lines);
        Assert.Contains(lines, l => l.StartsWith("march raid wave 1: stalled at ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("march raid wave 2: arrived after ", StringComparison.Ordinal));
        Assert.Contains("march: from (-1200.5, -800) to the camp (-1000, -800) at 2 m/s", lines);
        Assert.Contains(lines, l => l.Contains("to the camp (the starting admin)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("capped: ", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in UndeadHordesMarks) Assert.False(line.Contains(mark, StringComparison.OrdinalIgnoreCase), $"'{line}' carries {mark}");
        // the check sees a leak when one is there
        Assert.Contains(UndeadHordesMarks, m => WaveMarch.StalledLine("raid", 1, PlantedSpot.X, PlantedSpot.Z).Contains(m, StringComparison.Ordinal));
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
        Assert.Equal("march raid wave 1: stalled at 0, 0", line);
    }

    [Fact]
    public void UndeadHordes_empty_no_destination_no_march_line() =>
        Assert.DoesNotContain(EventLines.Info(Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default(), regions: FakeRegions.All()).Set.All),
            null, UdhT0), l => l.StartsWith("march:", StringComparison.Ordinal));
}
