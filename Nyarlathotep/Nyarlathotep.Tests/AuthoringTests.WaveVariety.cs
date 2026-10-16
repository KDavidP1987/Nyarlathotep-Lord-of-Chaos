using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D6: `.nyar event info` and `.nyar template info` show each pool ("pools: &lt;name&gt; (&lt;k&gt; units)"
/// with its prefabs, wrapped onto further lines when long), a pool entry as "&lt;k&gt; from &lt;pool&gt;", a roll as
/// "&lt;min&gt;-&lt;max&gt; bosses from &lt;pool&gt;" and the pursuit rule; every line fits 480 bytes and nothing is lost.</summary>
public partial class AuthoringTests
{
    const string WvPoint = "{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }";

    /// <summary>A waveList event "wv": pool elite (spiders) and lords (V Bloods); wave 1 draws 5 from elite beside 2 forest
    /// spiders and rolls 1-2 lords, wave 2 draws 3 from elite; the final wave adds a fixed queen and Hunt 30 pursues after 60 s.</summary>
    static string WaveVarietyEvent(string id = "wv") => Json.Event(id, action:
        "\"action\": { \"type\": \"SpawnWaves\", \"pools\": { \"elite\": [ \"CHAR_Spider_Forest\", \"CHAR_Spider_Melee\", \"CHAR_Spider_Range\" ], " +
        "\"lords\": [ \"CHAR_Undead_BishopOfDeath_VBlood\", \"CHAR_Militia_Leader_VBlood\" ] }, \"waveList\": [ " +
        "{ \"units\": [ { \"pool\": \"elite\", \"count\": 5 }, { \"prefab\": \"CHAR_Spider_Forestling\", \"count\": 2 } ], " +
        "\"bosses\": [ { \"pool\": \"lords\", \"min\": 1, \"max\": 2 } ] }, " +
        "{ \"units\": [ { \"pool\": \"elite\", \"count\": 3, \"chance\": 0.5 } ], \"afterSeconds\": 60 } ], " +
        "\"radius\": 10, \"location\": " + WvPoint + ", \"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }, \"pursuit\": { \"afterSeconds\": 60 }, " +
        "\"bosses\": [ { \"prefab\": \"CHAR_Spider_Queen_VBlood\", \"count\": 1 } ] }");

    static FakeUnits WvUnits() => TemplateLibraryTests.Units("CHAR_Militia_Leader_VBlood");

    static EventDefinition WvLoaded(string text, FakeUnits units)
    {
        var r = EventValidator.Parse(Json.File(text), units, regions: FakeRegions.All());
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    [Fact]
    public void Pools_passes_event_info_lines()
    {
        var lines = EventLines.Info(WvLoaded(WaveVarietyEvent(), WvUnits()), null, Now);
        Assert.Contains("pools: elite (3 units): CHAR_Spider_Forest, CHAR_Spider_Melee, CHAR_Spider_Range", lines);
        Assert.Contains("pools: lords (2 units): CHAR_Undead_BishopOfDeath_VBlood, CHAR_Militia_Leader_VBlood", lines);
        Assert.Contains("wave 1: 7 units (5 from elite, CHAR_Spider_Forestling ×2), at start", lines);
        Assert.Contains("wave 2: 3 units (3 from elite chance 0.5), after 60 s", lines);
        Assert.Contains("wave 1 adds: 1-2 bosses from lords", lines);
        Assert.Contains("final wave adds: CHAR_Spider_Queen_VBlood x1", lines);
        Assert.Contains("pursuit: after 60 s with no player within 30 m", lines);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Pools_passes_the_units_form_and_the_action_level_roll()
    {
        var d = WvLoaded(Json.Event("wu", action:
            "\"action\": { \"type\": \"SpawnWaves\", \"pools\": { \"elite\": [ \"CHAR_Spider_Forest\", \"CHAR_Spider_Melee\" ], " +
            "\"lords\": [ \"CHAR_Spider_Queen_VBlood\", \"CHAR_Undead_BishopOfDeath_VBlood\" ] }, " +
            "\"units\": [ { \"pool\": \"elite\", \"count\": 4 }, { \"prefab\": \"CHAR_Spider_Range\", \"count\": 1 } ], \"waves\": 2, " +
            "\"intervalSeconds\": 60, \"radius\": 10, \"location\": " + WvPoint + ", " +
            "\"bosses\": [ { \"prefab\": \"CHAR_Militia_Leader_VBlood\", \"count\": 1 }, { \"pool\": \"lords\", \"min\": 1, \"max\": 2 } ] }"), WvUnits());
        var lines = EventLines.Info(d, null, Now);
        Assert.Contains(lines, l => l.StartsWith("action: 2 waves every 60s", StringComparison.Ordinal) && l.Contains("units 4 from elite, 1 CHAR_Spider_Range", StringComparison.Ordinal));
        Assert.Contains("final wave adds: CHAR_Militia_Leader_VBlood x1, 1-2 bosses from lords", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("pursuit:", StringComparison.Ordinal));
    }

    [Fact]
    public void Pools_fails_when_a_line_passes_480_bytes_or_a_pool_or_roll_is_missing()
    {
        // 8 pools of 30 names of 60 characters, ten waves each drawing from three pools and rolling from three: every line
        // within 480 bytes, every pool member, entry and roll shown
        var names = Enumerable.Range(0, 30).Select(i => "CHAR_" + new string((char)('A' + i % 26), 52) + i.ToString("00")).ToList();
        var units = TemplateLibraryTests.Units(names.ToArray());
        var pools = string.Join(", ", Enumerable.Range(1, 8).Select(p => $"\"pool-{p:00}-{new string('x', 16)}\": [ " + string.Join(", ", names.Select(n => "\"" + n + "\"")) + " ]"));
        string Pool(int p) => $"pool-{p:00}-{new string('x', 16)}";
        var waves = string.Join(", ", Enumerable.Range(1, 10).Select(w =>
            "{ \"units\": [ " + string.Join(", ", Enumerable.Range(0, 3).Select(k => $"{{ \"pool\": \"{Pool(1 + (w + k) % 8)}\", \"count\": 16, \"chance\": 0.55 }}")) + " ]" +
            ", \"bosses\": [ " + string.Join(", ", Enumerable.Range(0, 3).Select(k => $"{{ \"pool\": \"{Pool(1 + (w + k + 3) % 8)}\", \"min\": 1, \"max\": 1 }}")) + " ]" +
            (w == 1 ? "" : ", \"afterSeconds\": 3600, \"whenCleared\": true") + " }"));
        var d = WvLoaded(Json.Event("big", action: "\"action\": { \"type\": \"SpawnWaves\", \"pools\": { " + pools + " }, \"waveList\": [ " + waves + " ], " +
            "\"radius\": 10, \"location\": " + WvPoint + ", \"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }, \"pursuit\": { \"afterSeconds\": 600 } }"), units);
        var lines = EventLines.Info(d, null, Now);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        for (var p = 1; p <= 8; p++)
        {
            var head = $"pools: {Pool(p)} (30 units): ";
            var shown = lines.Where(l => l.StartsWith(head, StringComparison.Ordinal)).SelectMany(l => l[head.Length..].Split(", ")).ToList();
            Assert.Equal(names, shown);                                                       // wrapped, nothing lost, in order
        }
        for (var w = 1; w <= 10; w++)
        {
            Assert.Contains(lines, l => l.StartsWith($"wave {w}: 48 units", StringComparison.Ordinal));
            foreach (var k in Enumerable.Range(0, 3))
            {
                Assert.Contains(lines, l => l.Contains($"16 from {Pool(1 + (w + k) % 8)} chance 0.55", StringComparison.Ordinal));
                Assert.Contains(lines, l => l.StartsWith($"wave {w} adds: ", StringComparison.Ordinal)
                                            && l.Contains($"1-1 bosses from {Pool(1 + (w + k + 3) % 8)}", StringComparison.Ordinal));
            }
        }
        Assert.Contains("pursuit: after 600 s with no player within 30 m", lines);
    }

    [Fact]
    public void Pools_passes_template_info_lines()
    {
        var template = WaveVarietyEvent("wv-template").Replace("\"enabled\": true", "\"enabled\": false");
        var units = WvUnits();
        var catalog = TemplateCatalog.Load(TemplateLibraryTests.Bytes(Json.File(template)), units, units);
        var lines = TemplateLines.Info(catalog, "wv-template", Now);
        Assert.Contains("pools: elite (3 units): CHAR_Spider_Forest, CHAR_Spider_Melee, CHAR_Spider_Range", lines);
        Assert.Contains("wave 1 adds: 1-2 bosses from lords", lines);
        Assert.Contains("pursuit: after 60 s with no player within 30 m", lines);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Pools_empty_no_wave_variety_lines()
    {
        var d = Json.One(Json.Event("raid"));
        Assert.Empty(EventLines.WaveVarietyLines(d.Action!));
        Assert.DoesNotContain(EventLines.Info(d, null, Now), l => l.StartsWith("pools:", StringComparison.Ordinal) || l.StartsWith("pursuit:", StringComparison.Ordinal));
    }
}
