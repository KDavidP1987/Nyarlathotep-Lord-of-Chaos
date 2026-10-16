using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D5 and D6's roll line: Logic/Pools.cs rolls a wave once at its decision, before the caps: each pool
/// entry draws its count uniformly with repeats, each boss roll a count in min..max and that many distinct prefabs its wave
/// does not already hold; the bosses lead the wave's list so the caps cut its other units first; a wave without pools or
/// rolls draws no random number and gives 0.12.0's list. The random source is a fixed seed.</summary>
public class PoolTests
{
    const string Thug = "CHAR_Bandit_Thug", Tourok = "CHAR_Bandit_Tourok_VBlood";
    static readonly string[] Elite = ["CHAR_Pool_01", "CHAR_Pool_02", "CHAR_Pool_03"];
    static readonly string[] Lords = ["CHAR_Lord_01", "CHAR_Lord_02", "CHAR_Lord_03", "CHAR_Lord_04", "CHAR_Lord_05"];

    static FakeUnits Catalog() => new(new[] { Thug, Tourok, "CHAR_Bandit_Deadeye" }.Concat(Elite).Concat(Lords).ToArray());

    static string Names(IEnumerable<string> names) => "[ " + string.Join(", ", names.Select(n => "\"" + n + "\"")) + " ]";

    static readonly string PoolsJson = "{ \"elite\": " + Names(Elite) + ", \"lords\": " + Names(Lords) + " }";

    /// <summary>A two-wave list: wave 1 a pool entry of elite beside 2 thugs and <paramref name="bosses"/> (JSON entries, or
    /// none), wave 2 one thug, cleared.</summary>
    static SpawnWavesAction Action(string? bosses = null, string firstUnits = "{ \"pool\": \"elite\", \"count\": 6 }, { \"prefab\": \"" + Thug + "\", \"count\": 2 }",
        string? last = null)
    {
        var pools = (bosses + last).Contains("lords", StringComparison.Ordinal) ? PoolsJson : "{ \"elite\": " + Names(Elite) + " }";
        var json = "\"action\": { \"type\": \"SpawnWaves\", \"pools\": " + pools + ", \"waveList\": [ { \"units\": [ " + firstUnits + " ]" +
            (bosses is null ? "" : ", \"bosses\": [ " + bosses + " ]") + " }, { \"units\": [ { \"prefab\": \"" + Thug + "\", \"count\": 1 } ], " +
            "\"whenCleared\": true } ], \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }" +
            (last is null ? "" : ", \"bosses\": [ " + last + " ]") + " }";
        var r = EventValidator.Parse(Json.File(Json.Event("wv", action: json)), Catalog());
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d.Action!;
    }

    static string Roll(string pool, int min, int max) => "{ \"pool\": \"" + pool + "\", \"min\": " + min + ", \"max\": " + max + " }";
    static string Fixed(string prefab, int count = 1) => "{ \"prefab\": \"" + prefab + "\", \"count\": " + count + " }";

    static IRandom Seeded(int seed) => new SystemRandom(new Random(seed));

    /// <summary>A source for a wave without pool entries or boss rolls, which draws no random number.</summary>
    internal static readonly IRandom NoRoll = new SystemRandom(new Random(0));

    static List<string> Prefabs(IEnumerable<UnitEntry> units) => units.SelectMany(u => Enumerable.Repeat(u.Prefab, u.Count)).ToList();

    /// <summary>Counts the random numbers a roll draws.</summary>
    sealed class CountingRandom(IRandom inner) : IRandom
    {
        public int Draws { get; private set; }

        public double NextDouble()
        {
            Draws++;
            return inner.NextDouble();
        }
    }

    // ================= D5 Roll =================

    [Fact]
    public void Roll_fails_when_a_roll_repeats_a_boss_or_draws_one_its_wave_holds()
    {
        var a = Action(Fixed(Lords[0]) + ", " + Roll("lords", 1, 4));
        var rng = Seeded(11);
        for (var i = 0; i < 10_000; i++)
        {
            var draw = Pools.Resolve(a, 1, rng);
            var bosses = draw.Bosses.Select(b => b.Prefab).ToList();
            Assert.Equal(Lords[0], bosses[0]);                                                  // the fixed boss first
            Assert.Equal(bosses.Count, bosses.Distinct().Count());                              // never a repeat, never the fixed one again
            Assert.InRange(bosses.Count - 1, 1, 4);
            Assert.All(bosses, b => Assert.Contains(b, Lords));
        }
        // a fixed entry's own count asks for copies; the roll still never draws that prefab
        var copies = Action(Fixed(Lords[1], 2) + ", " + Roll("lords", 1, 3));
        for (var i = 0; i < 1_000; i++)
        {
            var draw = Pools.Resolve(copies, 1, rng);
            Assert.Equal(2, draw.Bosses[0].Count);
            Assert.DoesNotContain(Lords[1], draw.Bosses.Skip(1).Select(b => b.Prefab));
        }
    }

    [Fact]
    public void Roll_fails_when_a_member_is_missed_over_10000_rolls_or_a_count_leaves_min_max()
    {
        var a = Action(Roll("lords", 2, 4));
        var rng = Seeded(5);
        var units = new HashSet<string>();
        var bosses = new HashSet<string>();
        var counts = new HashSet<int>();
        for (var i = 0; i < 10_000; i++)
        {
            var draw = Pools.Resolve(a, 1, rng);
            Assert.True(draw.Rolled);
            var drawn = draw.Units.Skip(draw.Bosses.Count).ToList();
            Assert.Equal(8, drawn.Sum(u => u.Count));                                            // 6 from the pool and 2 thugs
            Assert.Equal(6, drawn.Count(u => Elite.Contains(u.Prefab)));                          // count 1 each, with repeats
            units.UnionWith(drawn.Where(u => u.Prefab != Thug).Select(u => u.Prefab));
            bosses.UnionWith(draw.Bosses.Select(b => b.Prefab));
            counts.Add(draw.Bosses.Count);
            Assert.InRange(draw.Bosses.Count, 2, 4);
        }
        Assert.Equal(Elite.OrderBy(p => p), units.OrderBy(p => p));
        Assert.Equal(Lords.OrderBy(p => p), bosses.OrderBy(p => p));
        Assert.Equal([2, 3, 4], counts.OrderBy(c => c));
    }

    [Fact]
    public void Roll_fails_when_a_pool_entry_loses_its_chance_or_modifiers()
    {
        var a = Action(firstUnits: "{ \"pool\": \"elite\", \"count\": 3, \"chance\": 0.5, \"modifiers\": { \"level\": 33 } }");
        var draw = Pools.Resolve(a, 1, Seeded(1));
        Assert.Equal(3, draw.Units.Count);
        Assert.All(draw.Units, u => Assert.Equal((1, 0.5, (int?)33, (string?)null), (u.Count, u.Chance, u.Modifiers?.Level, u.Pool)));
        // the per-copy chance roll: with every chance draw at 0.9 none spawns, at 0.1 all do
        Assert.Empty(draw.Expand(new ScriptedRandom(0.9, 0.9, 0.9)));
        Assert.Equal(3, draw.Expand(new ScriptedRandom(0.1, 0.1, 0.1)).Sum(u => u.Count));
    }

    [Fact]
    public void Roll_fails_when_a_wave_is_re_rolled_after_its_decision()
    {
        var a = Action(Roll("lords", 1, 3));
        var draw = Pools.Resolve(a, 1, Seeded(3));
        var first = Prefabs(draw.Expand(Seeded(100)));
        var rng = Seeded(4);
        for (var i = 0; i < 50; i++)
            Assert.Equal(first, Prefabs(draw.Expand(rng)));                                    // every group, every call: the one draw
        Assert.Equal(Prefabs(draw.Units), first);
    }

    [Fact]
    public void Roll_fails_when_a_cap_cuts_a_boss_before_a_unit()
    {
        var a = Action(Fixed(Tourok) + ", " + Roll("lords", 2, 2));
        var rng = Seeded(9);
        for (var i = 0; i < 200; i++)
        {
            var draw = Pools.Resolve(a, 1, rng);
            Assert.Equal(3, draw.Lead);
            var bosses = draw.Bosses.Select(b => b.Prefab).ToList();
            WaveDecision Decide(int maxPerWave, int occupied = 0) =>
                WaveGate.DecideEntries(new WaveFacts(1, "wv", false, Location: LocationType.Point, AllowTerritory: true),
                    () => draw.Expand(rng), maxPerWave, occupied, 150);
            Assert.Equal(bosses, Prefabs(Decide(3).Units));                                      // MaxUnitsPerWave: the bosses stay
            Assert.Equal(bosses.Take(2), Prefabs(Decide(2).Units));                              // the first in list order
            Assert.Equal(bosses.Concat([Prefabs(draw.Units)[3]]), Prefabs(Decide(50, occupied: 146).Units));   // MaxTrackedUnits
            Assert.Equal(11, Prefabs(Decide(50).Units).Count);
            // a Region wave keeps the lead: every boss in the group with a spot
            var spot = new RegionSpot(300, 24, 300, 12, "FarbaneWoods");
            var region = RegionWave.Decide(new WaveFacts(1, "wv", false, Location: LocationType.Region, AllowTerritory: true), () => draw.Expand(rng),
                50, 0, 150, _ => [null, spot], lead: draw.Lead);
            var kept = Assert.Single(region.Groups).Units.Select(u => u.Prefab).ToList();
            Assert.All(bosses, b => Assert.Contains(b, kept));                                   // every boss traded into it
        }
    }

    [Fact]
    public void Roll_fails_when_a_queued_entry_of_a_pool_wave_has_an_empty_prefab()
    {
        // the wave decision queues the roll (Services/WaveAction: Pools.Resolve, draw.Expand, draw.Lead): every queued entry
        // names a prefab, a wave's own bosses and the action-level roll lead their waves, and BossWave keeps no second list
        var a = Action(Fixed(Tourok) + ", " + Roll("lords", 1, 2), last: Roll("lords", 1, 1));
        var rng = Seeded(4);
        var spot = new RegionSpot(300, 24, 300, 12, "FarbaneWoods");
        for (var w = 1; w <= 2; w++)
            for (var i = 0; i < 100; i++)
            {
                var draw = Pools.Resolve(a, w, rng);
                var point = WaveGate.DecideGroupEntries(new WaveFacts(w, "wv", false, Location: LocationType.Point, AllowTerritory: true),
                    [new GroupCentre(0, 0, 0, false)], () => draw.Expand(rng), 50, 0, 150);
                var region = RegionWave.Decide(new WaveFacts(w, "wv", false, Location: LocationType.Region, AllowTerritory: true), () => draw.Expand(rng),
                    50, 0, 150, n => Enumerable.Repeat<RegionSpot?>(spot, n).ToList(), lead: draw.Lead);
                Assert.All(point.Groups.Concat(region.Groups).SelectMany(g => g.Units), u => Assert.StartsWith("CHAR_", u.Prefab, StringComparison.Ordinal));
                Assert.Equal(w == 1 ? [Tourok] : [], draw.Bosses.Where(b => b.Prefab == Tourok).Select(b => b.Prefab));
                Assert.InRange(draw.Lead, w == 1 ? 2 : 1, w == 1 ? 3 : 1);
                Assert.Equal(draw.Bosses.Select(b => b.Prefab), Prefabs(Assert.Single(point.Groups).Units).Take(draw.Lead));
            }
        Assert.DoesNotContain(typeof(BossWave).GetMethods(), m => m.Name is "Units" or "Lead");
    }

    [Fact]
    public void Roll_fails_when_the_action_level_roll_joins_another_wave_than_the_last()
    {
        var a = Action(last: Roll("lords", 1, 2));
        var rng = Seeded(2);
        Assert.Empty(Pools.Resolve(a, 1, rng).Bosses);
        var last = Pools.Resolve(a, 2, rng);
        Assert.InRange(last.Bosses.Count, 1, 2);
        Assert.Equal(last.Bosses.Count, last.Lead);
        Assert.True(last.Rolled);
        Assert.Equal(Thug, last.Units[^1].Prefab);
    }

    [Fact]
    public void Roll_empty_no_pools_no_random_number_and_BossWaves_list()
    {
        var plain = Json.One(Json.Event("raid", action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"" + Thug +
            "\", \"count\": 5 } ], \"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, " +
            "\"modifiers\": { \"level\": 20 }, \"bosses\": [ { \"prefab\": \"" + Tourok + "\", \"count\": 2 } ] }")).Action!;
        for (var w = 1; w <= 3; w++)
        {
            var rng = new CountingRandom(Seeded(1));
            var draw = Pools.Resolve(plain, w, rng);
            Assert.Equal(0, rng.Draws);
            Assert.False(draw.Rolled);
            Assert.Equal(w == 3 ? [new UnitEntry(Tourok, 2, 1.0, plain.Modifiers), .. plain.Units] : plain.Units, draw.Units);   // 0.12.0's list
            Assert.Equal(w == 3 ? 2 : 0, draw.Lead);
        }
    }

    [Fact]
    public void Roll_passes_one_seeded_draw()
    {
        var a = Action(Fixed(Tourok) + ", " + Roll("lords", 1, 2));
        var one = Pools.Resolve(a, 1, Seeded(42));
        var two = Pools.Resolve(a, 1, Seeded(42));
        Assert.Equal(Prefabs(one.Units), Prefabs(two.Units));                                      // the seed decides the draw
        Assert.Equal(Tourok, one.Units[0].Prefab);
        Assert.Equal(one.Bosses.Count, one.Lead);
        Assert.Equal(one.Lead + 8, Prefabs(one.Units).Count);
    }

    // ================= D6 the "rolled:" line =================

    [Fact]
    public void RolledLine_passes_units_grouped_then_bosses()
    {
        var bosses = new List<UnitEntry> { new(Tourok, 2), new(Lords[1], 1) };
        var draw = new WaveDraw([.. bosses, new(Elite[0], 1), new(Elite[1], 1), new(Elite[0], 1), new(Thug, 2)], 3, true, bosses);
        Assert.Equal($"event wv wave 2/3 rolled: {Elite[0]}x2, {Elite[1]}x1, {Thug}x2; bosses: {Tourok}x2, {Lords[1]}", Pools.RolledLine("wv", 2, 3, draw));
    }

    [Fact]
    public void RolledLine_fails_when_a_line_passes_480_bytes_or_omits_a_boss()
    {
        var draw = Pools.Resolve(Action(Fixed(Tourok) + ", " + Roll("lords", 2, 2)), 1, Seeded(7));
        var line = Pools.RolledLine("wv", 1, 2, draw);
        Assert.StartsWith("event wv wave 1/2 rolled: ", line);
        foreach (var b in draw.Bosses) Assert.Contains(b.Prefab, line, StringComparison.Ordinal);
        Assert.Contains("; bosses: " + Tourok, line, StringComparison.Ordinal);
        Assert.Contains($"{Thug}x2", line, StringComparison.Ordinal);

        // a long roll: 40 distinct long prefabs and five long bosses end "… and <m> more", the units cut before a boss
        var units = Enumerable.Range(0, 40).Select(i => new UnitEntry("CHAR_" + new string((char)('A' + i % 26), 40) + i, 1)).ToList();
        var bosses = Enumerable.Range(0, 5).Select(i => new UnitEntry("CHAR_Boss_" + new string('B', 40) + i, 1)).ToList();
        var big = new WaveDraw([.. bosses, .. units], 5, true, bosses);
        var cut = Pools.RolledLine("wv", 10, 10, big);
        Assert.True(Encoding.UTF8.GetByteCount(cut) <= Wire.MaxBytes, cut);
        Assert.Matches(@"… and \d+ more; bosses: ", cut);
        foreach (var b in bosses) Assert.Contains(b.Prefab, cut, StringComparison.Ordinal);
        var huge = new WaveDraw([.. units], 0, true, []);
        Assert.True(Encoding.UTF8.GetByteCount(Pools.RolledLine("wv", 1, 1, huge)) <= Wire.MaxBytes);
        Assert.EndsWith("more", Pools.RolledLine("wv", 1, 1, huge));
    }

    [Fact]
    public void RolledLine_empty_no_bosses_no_bosses_part()
    {
        var draw = Pools.Resolve(Action(), 1, Seeded(1));
        Assert.Empty(draw.Bosses);
        Assert.Equal(0, draw.Lead);
        Assert.DoesNotContain("bosses", Pools.RolledLine("wv", 1, 2, draw), StringComparison.Ordinal);
    }
}
