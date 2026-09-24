using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D3: purge &gt; General.Enabled &gt; pillar switch &gt; Limits caps &gt; definition.</summary>
public class ControlPrecedenceTests
{
    static readonly Pillar[] AllPillars = [Pillar.Empowerment, Pillar.Spawns, Pillar.Boss, Pillar.Zones, Pillar.Sieges];

    // The five controls in precedence order, and the reply each gives when it alone blocks.
    static readonly string[] Expected =
    [
        "purge cooldown active",
        "General.Enabled is false",
        "pillar spawns is off",
        "skipped by MaxConcurrentEvents",
        "event raid is disabled",
    ];

    public static IEnumerable<object[]> Matrix()
    {
        // Every combination of the five controls blocking or not: 32 cases.
        for (var mask = 0; mask < 32; mask++) yield return [mask];
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Highest_blocking_control_wins(int mask)
    {
        bool On(int bit) => (mask & (1 << bit)) != 0;
        var def = Json.One(Json.Event()) with { Enabled = !On(4) };
        var state = new ControlState(
            PurgeCooldownActive: On(0),
            GeneralEnabled: !On(1),
            EnabledPillars: On(2) ? AllPillars.Where(p => p != Pillar.Spawns).ToHashSet() : AllPillars.ToHashSet(),
            ActiveEvents: On(3) ? 3 : 0,
            MaxConcurrentEvents: 3);

        var blocker = Precedence.StartBlocker(def, state);

        var first = Enumerable.Range(0, 5).FirstOrDefault(On, -1);
        if (first < 0) Assert.Null(blocker);
        else Assert.Equal(Expected[first], blocker);
    }

    [Fact]
    public void Matrix_is_not_empty() => Assert.Equal(32, Matrix().Count());

    [Fact]
    public void A_manual_admin_start_gets_no_exception_from_caps()
    {
        // The resolver takes no actor: a Manual-trigger definition is refused by the cap like any other.
        var def = Json.One(Json.Event());
        Assert.Equal(TriggerType.Manual, def.Trigger.Type);
        var full = new ControlState(false, true, AllPillars.ToHashSet(), 3, 3);
        Assert.Equal("skipped by MaxConcurrentEvents", Precedence.StartBlocker(def, full));
    }

    [Fact]
    public void Validation_disabled_definition_reports_its_reason()
    {
        var def = Json.One(Json.Event(extra: "\"bogus\": 1"));
        var open = new ControlState(false, true, AllPillars.ToHashSet(), 0, 3);
        Assert.Equal("event raid is disabled: unknown field bogus", Precedence.StartBlocker(def, open));
    }

    [Fact]
    public void Wave_is_clamped_by_per_wave_cap_then_by_free_slots()
    {
        var log = new List<string>();
        Assert.Equal(5, Precedence.WaveSize(20, 5, 0, 8, log));
        Assert.Equal(3, Precedence.WaveSize(20, 5, 5, 8, log));
        Assert.Equal(0, Precedence.WaveSize(20, 5, 8, 8, log));
        Assert.Contains(log, l => l.StartsWith("clamped by MaxUnitsPerWave"));
        Assert.Contains(log, l => l.StartsWith("skipped by MaxTrackedUnits"));
    }

    [Fact]
    public void Wave_within_caps_logs_nothing()
    {
        var log = new List<string>();
        Assert.Equal(4, Precedence.WaveSize(4, 20, 0, 150, log));
        Assert.Empty(log);
    }

    [Fact]
    public void Event_end_plus_grace_beats_a_longer_unit_lifetime()
    {
        var spawn = Zones.Utc(2026, 9, 24, 20, 0);
        var end = spawn.AddMinutes(10);
        Assert.Equal(end.AddSeconds(30), Precedence.UnitExpiryUtc(spawn, 7200, end, 30));
    }

    [Fact]
    public void A_shorter_unit_lifetime_is_kept()
    {
        var spawn = Zones.Utc(2026, 9, 24, 20, 0);
        var end = spawn.AddMinutes(10);
        Assert.Equal(spawn.AddSeconds(60), Precedence.UnitExpiryUtc(spawn, 60, end, 30));
    }

    [Fact]
    public void No_unit_lifetime_means_event_end_plus_grace()
    {
        var spawn = Zones.Utc(2026, 9, 24, 20, 0);
        var end = spawn.AddMinutes(10);
        Assert.Equal(end.AddSeconds(30), Precedence.UnitExpiryUtc(spawn, null, end, 30));
    }
}
