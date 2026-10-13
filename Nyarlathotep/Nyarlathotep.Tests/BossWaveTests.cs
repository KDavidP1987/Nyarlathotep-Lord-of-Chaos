using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D29 (design §9 D51): an action's bosses join its final wave only, in the units, waveList and growth
/// forms and at every location; the caps cut the wave's other units before a boss and keep the bosses in list order; a
/// boss without modifiers takes the final wave's.</summary>
public class BossWaveTests
{
    const string Tourok = "CHAR_Bandit_Tourok_VBlood", Deadeye = "CHAR_Bandit_Deadeye", Thug = "CHAR_Bandit_Thug";
    const string Point = "{ \"type\": \"Point\", \"x\": 0, \"z\": 0 }";

    static readonly IRandom Rng = new SystemRandom(new Random(7));

    static string Bosses(string entries = "{ \"prefab\": \"" + Tourok + "\", \"count\": 1 }") => "\"bosses\": [ " + entries + " ]";

    static SpawnWavesAction UnitsForm(string? extra = null, string location = Point) => Json.One(Json.Event(action:
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"" + Thug + "\", \"count\": 5 } ], \"waves\": 3, " +
        "\"intervalSeconds\": 60, \"radius\": 10, \"location\": " + location + ", \"modifiers\": { \"level\": 20 }, " + (extra ?? Bosses()) + " }")).Action!;

    static SpawnWavesAction WaveListForm(string? extra = null) => Json.One(Json.Event(action:
        "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ " +
        "{ \"units\": [ { \"prefab\": \"" + Thug + "\", \"count\": 3, \"modifiers\": { \"level\": 25 } } ] }, " +
        "{ \"units\": [ { \"prefab\": \"" + Deadeye + "\", \"count\": 3, \"modifiers\": { \"level\": 30 } } ], \"whenCleared\": true } ], " +
        "\"radius\": 10, \"location\": " + Point + ", " + (extra ?? Bosses()) + " }")).Action!;

    static SpawnWavesAction GrowthForm(string? extra = null) => Json.One(Json.Event(action:
        "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"" + Thug + "\", \"count\": 4 } ], \"waves\": 3, " +
        "\"intervalSeconds\": 60, \"radius\": 10, \"location\": " + Point + ", \"modifiers\": { \"level\": 30 }, " +
        "\"growth\": { \"addUnits\": 2, \"addLevels\": 4 }, " + (extra ?? Bosses()) + " }")).Action!;

    public static IEnumerable<object[]> Forms() => [["units"], ["waveList"], ["growth"]];

    static SpawnWavesAction Form(string form, string? extra = null) => form switch
    {
        "units" => UnitsForm(extra),
        "waveList" => WaveListForm(extra),
        _ => GrowthForm(extra),
    };

    static WaveDecision Decide(SpawnWavesAction a, int wave, int maxPerWave = 50, int occupied = 0, int maxTracked = 150,
        LocationType location = LocationType.Point) =>
        WaveGate.DecideEntries(new WaveFacts(wave, "bw", false, Location: location, AllowTerritory: true,
                Pick: location == LocationType.AroundPlayer ? PickOutcome.Picked : null),
            () => WaveRoll.ExpandEntries(BossWave.Units(a, wave), Rng), maxPerWave, occupied, maxTracked);

    static List<string> Prefabs(IEnumerable<UnitEntry> units) => units.SelectMany(u => Enumerable.Repeat(u.Prefab, u.Count)).ToList();

    [Theory]
    [MemberData(nameof(Forms))]
    public void Fails_when_a_boss_spawns_in_an_earlier_wave_or_the_final_wave_lacks_it(string form)
    {
        var a = Form(form);
        for (var w = 1; w < a.Waves; w++)
        {
            Assert.DoesNotContain(Tourok, Prefabs(Decide(a, w).Units));
            Assert.Equal(a.UnitsOf(w), BossWave.Units(a, w));
        }
        var last = Prefabs(Decide(a, a.Waves).Units);
        Assert.Equal(Tourok, last[0]);                                                     // the boss first ...
        Assert.Equal(a.UnitsOf(a.Waves).Sum(u => u.Count) + 1, last.Count);                // ... beside the wave's own units
        Assert.Equal(1, BossWave.Lead(a, a.Waves));
        Assert.Equal(0, BossWave.Lead(a, 1));
    }

    [Theory]
    [InlineData(LocationType.Point)]
    [InlineData(LocationType.Admin)]
    [InlineData(LocationType.AroundPlayer)]
    public void Fails_when_a_location_drops_the_boss(LocationType location)
    {
        var a = UnitsForm();
        var d = Decide(a, a.Waves, location: location);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal([Tourok, Thug, Thug, Thug, Thug, Thug], Prefabs(d.Units));
    }

    [Fact]
    public void Fails_when_a_clamp_drops_a_boss_before_the_waves_other_units_or_breaks_the_boss_order()
    {
        var a = UnitsForm(Bosses("{ \"prefab\": \"" + Tourok + "\", \"count\": 2 }, { \"prefab\": \"" + Deadeye + "\", \"count\": 1 }"));
        Assert.Equal([Tourok, Tourok, Deadeye, Thug], Prefabs(Decide(a, 3, maxPerWave: 4).Units));        // MaxUnitsPerWave
        Assert.Equal([Tourok, Tourok], Prefabs(Decide(a, 3, occupied: 148).Units));                         // MaxTrackedUnits
        Assert.Equal([Tourok], Prefabs(Decide(a, 3, maxPerWave: 1).Units));                                 // the first in list order
    }

    [Fact]
    public void Fails_when_a_clamp_to_zero_room_spawns_a_boss_or_stops_the_wave_differently()
    {
        var with = UnitsForm();
        var without = with with { Bosses = null };
        var full = Decide(with, 3, occupied: 150);
        var plain = Decide(without, 3, occupied: 150);
        Assert.Empty(Prefabs(full.Units));
        Assert.Equal(plain.Outcome, full.Outcome);                                          // decided as a fully clamped wave
        Assert.Equal(plain.CapLines.Count, full.CapLines.Count);
    }

    [Fact]
    public void Fails_when_a_region_wave_loses_its_boss_to_a_group_without_a_spot()
    {
        var spot = new RegionSpot(300, 24, 300, 12, "FarbaneWoods");
        IReadOnlyList<UnitEntry> Roll() => [new UnitEntry(Tourok, 1, 1.0, null), .. Enumerable.Repeat(new UnitEntry(Thug, 1), 9)];
        var facts = new WaveFacts(3, "bw", false, Location: LocationType.Region, AllowTerritory: true);

        var both = RegionWave.Decide(facts, Roll, 50, 0, 150, _ => [spot, spot], lead: 1);
        Assert.Equal(Tourok, both.Groups[0].Units[0].Prefab);                               // the first group's first unit
        Assert.Equal(10, both.Groups.Sum(g => g.Units.Sum(u => u.Count)));

        var second = RegionWave.Decide(facts, Roll, 50, 0, 150, _ => [null, spot], lead: 1);
        var group = Assert.Single(second.Groups);
        Assert.Equal(1, group.Index);
        Assert.Equal(Tourok, group.Units[0].Prefab);                                       // traded into the group with a spot
        Assert.Equal(5, group.Units.Sum(u => u.Count));

        var unled = RegionWave.Decide(facts, Roll, 50, 0, 150, _ => [null, spot]);          // no lead: the deal as before
        Assert.DoesNotContain(Tourok, Prefabs(Assert.Single(unled.Groups).Units));
    }

    [Fact]
    public void Fails_when_a_boss_without_modifiers_does_not_take_the_final_waves()
    {
        static int? Level(SpawnWavesAction a, UnitEntry e) => SpawnTuning.For(a, e).Level is { IsOffset: false } l ? l.Value : null;

        var units = UnitsForm();
        Assert.Equal((int?)20, Level(units, BossWave.Units(units, 3)[0]));                    // the action's modifiers

        var listed = WaveListForm();
        Assert.Equal((int?)30, Level(listed, BossWave.Units(listed, 2)[0]));                  // the final wave's first entry

        var grown = GrowthForm();
        Assert.Equal((int?)38, Level(grown, BossWave.Units(grown, 3)[0]));                    // 30 + 2 × 4: growth's added levels

        var own = UnitsForm(Bosses("{ \"prefab\": \"" + Tourok + "\", \"count\": 1, \"modifiers\": { \"level\": 60 } }"));
        Assert.Equal((int?)60, Level(own, BossWave.Units(own, 3)[0]));                        // its own
        Assert.Equal((int?)20, Level(own, BossWave.Units(own, 3)[1]));                        // the wave's units keep theirs
    }

    [Fact]
    public void Fails_when_a_boss_is_not_counted_for_the_clear()
    {
        // The final wave's decision carries the bosses, so WaveAction queues them under that wave's number and
        // SpawnLedger.WaveCleared waits for them as for the wave's other units.
        var a = WaveListForm();
        var d = Decide(a, 2);
        Assert.Contains(Tourok, Prefabs(d.Units));
        Assert.Equal(4, Prefabs(d.Units).Count);
    }
}
