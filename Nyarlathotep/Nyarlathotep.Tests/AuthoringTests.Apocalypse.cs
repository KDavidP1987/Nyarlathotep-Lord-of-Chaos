using System.Text;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D17: `.nyar status`'s player line for a running apocalypse carries no position; `.nyar event info`
/// shows admins the definition's lines and, while it runs, each hotspot's region and x, z, each living horde's front and
/// progress, and the tally line; every line fits 480 bytes.</summary>
public partial class AuthoringTests
{
    static readonly DateTime ApoT0 = Zones.Utc(2026, 10, 31, 20, 0);

    /// <summary>A run of apo with both hotspots topped up and hordes 1 and 2 risen, horde 1 marched 100 m.</summary>
    static (EventDefinition Def, ActiveEvent Active, ApocalypseView View) ApoRunning(string? action = null)
    {
        var def = Apo.Def(action);
        var active = Apo.Active(def, ApoT0);
        var w = new ApoWorld();
        var run = Apo.Run(active);
        run.TopUp(w, ApoT0);
        run.Rise(w, ApoT0.AddMinutes(1), out _);
        run.Rise(w, ApoT0.AddMinutes(6), out _);
        w.SpawnAll(run, ApoT0.AddMinutes(6));
        MarchRule.Step(run.Fronts[1], def.Apocalypse!.Goal.Radius, 2.0, 50, [], (_, _) => true, ApoT0.AddMinutes(7), out _);
        return (def, active, run.View(w, (x, _) => x < 500 ? "Farbane Woods" : ""));
    }

    [Fact]
    public void Apocalypse_fails_when_the_player_line_carries_a_coordinate()
    {
        var (def, _, view) = ApoRunning();
        var line = Messages.ApocalypseStatus(def.Name, view.Risen, def.Apocalypse!.Hordes.Count, view.Percent, def.Apocalypse.Goal.Name, view.Roamers, 53);
        Assert.Equal("Night of the dead: horde 2/3, 20 % to Brighthaven, 6 roamers, ends in 53 min", line);
        Assert.DoesNotMatch(new Regex(@"\d{3,}"), line);                         // no x, z or height of a hotspot, a front or the goal
        Assert.DoesNotContain("1000", line);
        Assert.DoesNotContain("500", line);
    }

    [Fact]
    public void Apocalypse_fails_when_the_admin_info_misses_a_hotspot_or_a_front()
    {
        var (def, active, view) = ApoRunning();
        var lines = EventLines.Info(def, active, ApoT0.AddMinutes(7), apocalypse: view);
        Assert.Contains("hotspot 1: Farbane Woods at 0 0", lines);
        Assert.Contains("hotspot 2: the wilds at 1000 0", lines);
        Assert.Contains("horde 1 front at 100 0, 20 %", lines);
        Assert.Contains("horde 2 front at 1000 0, 0 %", lines);
        Assert.Contains("roamers: 6 living", lines);
        Assert.Contains(lines, l => l.StartsWith("apocalypse apo units: ", StringComparison.Ordinal));
        // a stopped run shows only the definition
        Assert.DoesNotContain(EventLines.Info(def, null, ApoT0), l => l.StartsWith("hotspot ", StringComparison.Ordinal) || l.Contains(" front at ", StringComparison.Ordinal));
    }

    [Fact]
    public void Apocalypse_fails_when_a_line_exceeds_480_bytes()
    {
        var units = "[ " + string.Join(", ", Enumerable.Range(0, 10).Select(_ => Apo.Unit(Apo.Zombie, "50", "\"chance\": 0.55, \"modifiers\": { \"level\": 120, \"maxHealth\": 2.75, \"power\": 2.75, \"moveSpeed\": 2.75, \"attackSpeed\": 2.75 }"))) + " ]";
        var bosses = "\"bosses\": [ " + string.Join(", ", Enumerable.Repeat(Apo.BossJson(count: "3"), 3)) + " ]";
        var big = Apo.Action(units: units, hotspots: Apo.Hotspots(5, 50, 3000), roamers: Apo.Roamers(10, 2, 100, 30, 60),
            hordes: Apo.Hordes(2, 1, 5, 50, 20), goal: Apo.Goal(location: "{ \"type\": \"Point\", \"x\": -10000, \"z\": -10000, \"y\": -10000 }", radius: 100, name: new string('G', 32)),
            extra: bosses);
        var (def, active, view) = ApoRunning(big);
        var lines = EventLines.Info(def, active, ApoT0.AddMinutes(7), apocalypse: view);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        Assert.Contains("goal: " + new string('G', 32) + ", radius 100, at -10000 -10000 height -10000", lines);
        var region = new string('R', 600);
        Assert.All(EventLines.ApocalypseRunLines(view with { Hotspots = [(1, region, -10000f, -10000f)] }), l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes));
        Assert.True(Encoding.UTF8.GetByteCount(Messages.ApocalypseStatus(new string('N', 64), 10, 10, 100, new string('G', 32), 100, 10080)) <= Wire.MaxBytes);
    }

    [Fact]
    public void Apocalypse_passes_definition_lines()
    {
        var def = Apo.Def(Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson() + " ]"));
        var lines = EventLines.Info(def, null, ApoT0);
        Assert.Equal("apo \"Night of the dead\" enabled pillar spawns trigger manual duration 3600s", lines[0]);
        Assert.Contains("action: apocalypse of the Undead, march 2 m/s", lines);
        Assert.Contains("hotspots: 2, at least 3 natives each, 200 m apart", lines);
        Assert.Contains("roamers: 3 per hotspot, +2 per kill, max 12, 4 spread per minute, hunt range 40", lines);
        Assert.Contains("hordes: 3, first after 1 min, every 5 min, size 8 +4 each", lines);
        Assert.Contains("goal: Brighthaven, radius 20, at 500 0 height 12", lines);
        Assert.Contains($"last horde adds: {Apo.Tourok} x1", lines);
        Assert.Contains("scoreboard: on", lines);
        Assert.Contains("goal: Brighthaven, radius 20, at the starting admin",
            EventLines.Info(Apo.Def(Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }"))), null, ApoT0));
    }

    [Fact]
    public void Apocalypse_empty_no_bosses_no_line()
    {
        var lines = EventLines.Info(Apo.Def(), null, ApoT0);
        Assert.DoesNotContain(lines, l => l.StartsWith("last horde adds", StringComparison.Ordinal));
        Assert.Equal(0, new ApocalypseView([], [], 0, "", 0).Percent);
    }
}
