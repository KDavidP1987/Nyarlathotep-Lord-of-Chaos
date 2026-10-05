using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D15: `.nyar event info` of a waveList event shows one line per wave with its units, each entry's
/// level and multipliers and its start rule, and the scoreboard switch; every line fits 480 bytes.</summary>
public partial class AuthoringTests
{
    static EventDefinition WaveSet(params WaveSpec[] waves) => Json.One(Json.Event("ws")) is var d
        ? d with { Action = d.Action! with { Units = waves.SelectMany(w => w.Units).ToList(), Waves = waves.Length, IntervalSeconds = 0, WaveList = waves, Scoreboard = true } }
        : d;

    [Fact]
    public void WaveSets_passes_one_line_per_wave()
    {
        var d = WaveSet(
            new WaveSpec([new UnitEntry("CHAR_Bandit_Thug", 6, 1.0, new SpawnModifiers(Level: 30)),
                          new UnitEntry("CHAR_Bandit_Thug", 6, 1.0, new SpawnModifiers(LevelDelta: 3, MaxHealth: 1.5))]),
            new WaveSpec([new UnitEntry("CHAR_Bandit_Deadeye", 8, 0.5, new SpawnModifiers(LevelDelta: -2, Power: 1.25, MoveSpeed: 0.8, AttackSpeed: 2))], 120, true),
            new WaveSpec([new UnitEntry("CHAR_Bandit_Thug", 8)], null, true),
            new WaveSpec([new UnitEntry("CHAR_Bandit_Thug", 1)], 300));
        var lines = EventLines.Info(d, null, Now);
        Assert.Contains("action: 4 waves, radius 10, at -1200.5 -800", lines.First(l => l.StartsWith("action:", StringComparison.Ordinal)));
        Assert.Contains("wave 1: 12 units (CHAR_Bandit_Thug ×6 L30, CHAR_Bandit_Thug ×6 L+3 hp×1.5), at start", lines);
        Assert.Contains("wave 2: 8 units (CHAR_Bandit_Deadeye ×8 L-2 pw×1.25 mv×0.8 as×2 chance 0.5), after 120 s or when cleared", lines);
        Assert.Contains("wave 3: 8 units (CHAR_Bandit_Thug ×8), when cleared", lines);
        Assert.Contains("wave 4: 1 units (CHAR_Bandit_Thug ×1), after 300 s", lines);
        Assert.Contains("scoreboard: on", lines);
        Assert.Contains("scoreboard: off", EventLines.Info(d with { Action = d.Action! with { Scoreboard = false } }, null, Now));
    }

    [Fact]
    public void WaveSets_fails_when_a_line_exceeds_480_bytes()
    {
        var all = new SpawnModifiers(LevelDelta: -5, MaxHealth: 2.75, Power: 2.75, MoveSpeed: 2.75, AttackSpeed: 2.75);
        var entries = Enumerable.Range(0, 10).Select(i => new UnitEntry("CHAR_" + new string((char)('A' + i), 59), 50, 0.55, all)).ToList();
        var d = WaveSet(Enumerable.Range(0, 10).Select(i => new WaveSpec(entries, i == 0 ? null : 3600, i > 0)).ToArray());
        var lines = EventLines.Info(d, null, Now);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        Assert.Contains("wave 10: 500 units, after 3600 s or when cleared, units below", lines);
        foreach (var e in entries) Assert.Contains(lines, l => l.Contains(EventLines.EntryText(e), StringComparison.Ordinal));   // nothing lost
    }

    [Fact]
    public void WaveSets_fails_when_the_units_form_info_changes()
    {
        var lines = EventLines.Info(Json.One(Json.Event("raid")), null, Now);
        Assert.Contains("action: 3 waves every 60s, radius 10, at -1200.5 -800, units 5 CHAR_Bandit_Thug", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("wave ", StringComparison.Ordinal) || l.StartsWith("scoreboard", StringComparison.Ordinal));
    }

    [Fact]
    public void WaveSets_empty_entry_without_modifiers()
    {
        Assert.Equal("CHAR_Bandit_Thug ×3", EventLines.EntryText(new UnitEntry("CHAR_Bandit_Thug", 3)));
        Assert.Equal("CHAR_Bandit_Thug ×3", EventLines.EntryText(new UnitEntry("CHAR_Bandit_Thug", 3, 1.0, new SpawnModifiers())));
    }
}
