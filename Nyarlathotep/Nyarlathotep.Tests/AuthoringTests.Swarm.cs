using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D10: `.nyar event info` of an arena event shows its arena and the winner on the scoreboard line; of a
/// growth event, the growth line and one wave-sets line per generated wave; every line fits 480 bytes; an event without
/// the three keys shows 0.9.0's lines.</summary>
public partial class AuthoringTests
{
    static EventDefinition SwarmEvent(string extra, string location = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }",
        string units = "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 4 } ]", int waves = 3)
    {
        var d = Json.One(Json.Event("sw", action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": " + units + ", \"waves\": " + waves +
            ", \"intervalSeconds\": 120, \"radius\": 10, \"location\": " + location + ", " + extra + " }"));
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    [Fact]
    public void Swarm_passes_arena_winner_and_growth_lines()
    {
        var point = EventLines.Info(SwarmEvent("\"arena\": { \"radius\": 40 }, \"scoreboard\": true, \"winner\": \"KillDeath\""), null, Now);
        Assert.Contains("arena: 40 m around the spot", point);
        Assert.Contains("scoreboard: on, winner K/D", point);
        var admin = EventLines.Info(SwarmEvent("\"arena\": { \"radius\": 40 }", location: "{ \"type\": \"Admin\" }"), null, Now);
        Assert.Contains("arena: 40 m around the starting admin", admin);
        Assert.DoesNotContain(admin, l => l.StartsWith("scoreboard", StringComparison.Ordinal));

        var grown = EventLines.Info(SwarmEvent("\"modifiers\": { \"level\": 30 }, \"scoreboard\": true, \"winner\": \"KillDeath\", " +
            "\"growth\": { \"addUnits\": 1, \"addLevels\": 3, \"whenCleared\": true }"), null, Now);
        var at = grown.ToList().IndexOf("growth: +1 units per entry and +3 levels per wave, every 120s or when cleared");
        Assert.True(at >= 0, string.Join(" | ", grown));
        Assert.Equal(["wave 1: 4 units (CHAR_Bandit_Thug ×4 L30), at start",
                      "wave 2: 5 units (CHAR_Bandit_Thug ×5 L33), after 120 s or when cleared",
                      "wave 3: 6 units (CHAR_Bandit_Thug ×6 L36), after 120 s or when cleared"], grown.Skip(at + 1).Take(3));
        Assert.Contains("scoreboard: on, winner K/D", grown);
        var timed = EventLines.Info(SwarmEvent("\"growth\": { \"addUnits\": 2 }", waves: 2), null, Now);
        Assert.Contains("growth: +2 units per entry and +0 levels per wave, every 120s", timed);
        Assert.Contains("wave 2: 6 units (CHAR_Bandit_Thug ×6), after 120 s", timed);
    }

    [Fact]
    public void Swarm_fails_when_a_line_exceeds_480_bytes()
    {
        var all = "\"modifiers\": { \"levelDelta\": -5, \"maxHealth\": 2.75, \"power\": 2.75, \"moveSpeed\": 2.75, \"attackSpeed\": 2.75 }";
        var units = "[ " + string.Join(", ", Enumerable.Range(0, 10).Select(i => "{ \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 1, \"chance\": 0.55 }")) + " ]";
        var d = SwarmEvent(all + ", \"arena\": { \"radius\": 60 }, \"scoreboard\": true, \"winner\": \"KillDeath\", " +
            "\"growth\": { \"addUnits\": 4, \"addLevels\": 5, \"whenCleared\": true }", units: units, waves: 2);
        var lines = EventLines.Info(d, null, Now);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        Assert.Contains(lines, l => l.StartsWith("growth: ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("arena: ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("wave 2: 50 units", StringComparison.Ordinal));
    }

    [Fact]
    public void Swarm_fails_when_a_keyless_events_info_changes()
    {
        var units = EventLines.Info(Json.One(Json.Event("raid")), null, Now);
        Assert.Contains("action: 3 waves every 60s, radius 10, at -1200.5 -800, units 5 CHAR_Bandit_Thug", units);
        Assert.DoesNotContain(units, l => l.StartsWith("arena", StringComparison.Ordinal) || l.StartsWith("growth", StringComparison.Ordinal)
            || l.StartsWith("scoreboard", StringComparison.Ordinal) || l.StartsWith("wave ", StringComparison.Ordinal));
        var scored = EventLines.Info(SwarmEvent("\"scoreboard\": true"), null, Now);
        Assert.Contains(scored, l => l.EndsWith("scoreboard on", StringComparison.Ordinal));                 // the units form's 0.9.0 key
        var modifiers = EventLines.Info(SwarmEvent("\"modifiers\": { \"power\": 1.5 }"), null, Now);
        Assert.Contains("spawn: modifiers power x1.5", modifiers);                                          // a units-form spawn line keeps its modifiers
    }

    [Fact]
    public void Swarm_empty_no_swarm_keys()
    {
        var d = Json.One(Json.Event("raid"));
        Assert.Null(EventLines.Info(d, null, Now).FirstOrDefault(l => l.StartsWith("arena", StringComparison.Ordinal)));
        Assert.Equal("scoreboard: off", EventLines.ScoreboardLine(d.Action!));
    }
}
