using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D1 (the catalogue file) and D2 (the six starter templates). The real Resources/templates.json
/// is copied to the test output; the unit and faction names the fakes know are written out here from Reference
/// Data/unit_index.tsv (S-4, S-5), independently of the file.</summary>
public class TemplateLibraryTests
{
    static readonly string[] BanditVBloods =
    [
        "CHAR_Bandit_Bomber_VBlood", "CHAR_Bandit_Chaosarrow_VBlood", "CHAR_Bandit_Fisherman_VBlood", "CHAR_Bandit_Foreman_VBlood",
        "CHAR_Bandit_Frostarrow_VBlood", "CHAR_Bandit_Stalker_VBlood", "CHAR_Bandit_StoneBreaker_VBlood", "CHAR_Bandit_Tourok_VBlood",
    ];

    static readonly string[] MilitiaVBloods =
    [
        "CHAR_ChurchOfLight_Sommelier_VBlood", "CHAR_Militia_BishopOfDunley_VBlood", "CHAR_Militia_Glassblower_VBlood",
        "CHAR_Militia_Guard_VBlood", "CHAR_Militia_Hound_VBlood", "CHAR_Militia_HoundMaster_VBlood", "CHAR_Militia_Leader_VBlood",
        "CHAR_Militia_Longbowman_LightArrow_Vblood", "CHAR_Militia_Nun_VBlood", "CHAR_Militia_Scribe_VBlood",
        "CHAR_ChurchOfLight_Overseer_VBlood", "CHAR_Militia_Fabian_VBlood", "CHAR_Villager_Tailor_VBlood",
        "CHAR_ChurchOfLight_Cardinal_VBlood", "CHAR_ChurchOfLight_Paladin_VBlood",
    ];

    /// <summary>The test catalog fakes: every unit S-4 and S-5 name, and the default factions.</summary>
    internal static FakeUnits Units(params string[] extra) => new(
        new[] { "CHAR_Bandit_Thug", "CHAR_Bandit_Hunter", "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane" }
            .Concat(BanditVBloods).Concat(MilitiaVBloods).Concat(extra).ToArray());

    internal static string RealText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "templates.json"));
    internal static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);
    internal static TemplateCatalog Real() => TemplateCatalog.Load(Bytes(RealText), Units(), Units());

    static IEnumerable<string> DefaultIds()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "events.default.json"));
        return EventValidator.Parse(text, FakeUnits.Default()).Set.All.Select(d => d.Id);
    }

    // ---- D1 TemplateCatalogue

    [Fact]
    public void TemplateCatalogue_passes_real_file()
    {
        var c = Real();
        Assert.Null(c.Error);
        Assert.Equal(6, c.Templates.Count);
        Assert.Empty(TemplateCatalog.Problems(c, DefaultIds()));
        Assert.Matches("^\\{\\s*\"SchemaVersion\": 1,", RealText);                 // the key spelled as the validator needs it
        Assert.Equal(6, c.ValidCount);
    }

    [Theory]
    [InlineData("enabled", "template legion-weekend-surge does not ship \"enabled\": false")]
    [InlineData("extra key", "template legion-weekend-surge has key visual")]
    [InlineData("duplicate id", "template id legion-weekend-surge is used twice")]
    [InlineData("default id", "template id example-spawns is an events.default.json id")]
    [InlineData("invalid", "template legion-weekend-surge invalid: unknown faction Faction_Legion")]
    public void TemplateCatalogue_fails_when_template_breaks_a_rule(string plant, string problem)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        var first = (JsonObject)events[0]!;
        var units = Units();
        switch (plant)
        {
            case "enabled": first["enabled"] = true; break;
            case "extra key": first["visual"] = "Buff_X"; break;
            case "duplicate id": ((JsonObject)events[1]!)["id"] = "legion-weekend-surge"; break;
            case "default id": ((JsonObject)events[4]!)["id"] = "example-spawns"; break;
            default: units.Factions.Remove("Faction_Legion"); break;
        }
        var c = TemplateCatalog.Load(Bytes(root.ToJsonString()), units, units);
        Assert.Contains(problem, TemplateCatalog.Problems(c, DefaultIds()));
    }

    [Theory]
    [InlineData("{\"SchemaVersion\": 2, \"events\": []}", "templates.json SchemaVersion 2 is not 1")]
    [InlineData("{\"schemaVersion\": 1, \"events\": []}", "events.json rejected: unknown top-level field schemaVersion")]
    [InlineData("[]", "events.json rejected: the top level must be an object")]
    [InlineData("{ broken", "events.json rejected: line 1")]
    public void TemplateCatalogue_fails_when_file_is_broken(string text, string error)
    {
        var c = TemplateCatalog.Load(Bytes(text), Units(), Units());
        Assert.StartsWith(error, c.Error);
        Assert.Empty(c.Templates);
        Assert.NotEmpty(TemplateCatalog.Problems(c, []));
    }

    [Fact]
    public void TemplateCatalogue_empty_zero_templates()
    {
        var c = TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units());
        Assert.Null(c.Error);                                                        // available, not unavailable
        Assert.Empty(c.Templates);
        Assert.Equal("embedded templates.json missing", TemplateCatalog.Load(null, Units(), Units()).Error);
    }

    // ---- D2 StarterTemplates

    /// <summary>Business rules 2 as the test's own table: id → pillar, trigger, cooldown, duration, action.</summary>
    static readonly (string Id, string Pillar, string Trigger, int Cooldown, int Duration, string Action)[] Expected =
    [
        ("legion-weekend-surge", "empowerment", "schedule Sat 20:00", 0, 1800, "empower Faction_Legion: physicalPower 1.5, maxHealth 1.5"),
        ("bandit-vengeance", "empowerment", "vbloodkilled " + string.Join(",", BanditVBloods), 30, 600, "empower Faction_Bandits: physicalPower 1.3, attackSpeed 1.3"),
        ("undead-nightfall", "empowerment", "gametime night", 0, 1200, "empower Faction_Undead: physicalPower 1.25, spellPower 1.25"),
        ("militia-crackdown", "empowerment", "vbloodkilled " + string.Join(",", MilitiaVBloods), 30, 900, "empower Faction_Militia,Faction_ChurchOfLum: maxHealth 1.3"),
        ("bandit-ambush", "spawns", "manual", 0, 600, "waves CHAR_Bandit_Thug:4,CHAR_Bandit_Hunter:2 x3 every 60s radius 10 at Admin"),
        ("undead-rising", "spawns", "manual", 0, 600, "waves CHAR_Undead_SkeletonSoldier_Armored_Farbane:5,CHAR_Undead_ArmoredSkeletonCrossbow_Farbane:2 x2 every 90s radius 12 at Admin"),
    ];

    static string Describe(EventDefinition d)
    {
        if (d.Empower is { } e)
        {
            var s = e.Stats;
            var stats = new (string, double)[] { ("physicalPower", s.PhysicalPower), ("spellPower", s.SpellPower), ("maxHealth", s.MaxHealth), ("attackSpeed", s.AttackSpeed), ("moveSpeed", s.MoveSpeed) }
                .Where(x => x.Item2 > 1.0).Select(x => FormattableString.Invariant($"{x.Item1} {x.Item2}"));
            return $"empower {string.Join(",", e.Factions)}: {string.Join(", ", stats)}";
        }
        var a = d.Action!;
        return $"waves {string.Join(",", a.Units.Select(u => $"{u.Prefab}:{u.Count}"))} x{a.Waves} every {a.IntervalSeconds}s radius {a.Radius} at {a.Location.Type}";
    }

    /// <summary>Every difference between a catalogue and <see cref="Expected"/>; empty when they agree.</summary>
    static List<string> Differences(TemplateCatalog c)
    {
        var diffs = new List<string>();
        if (c.Templates.Count != Expected.Length) diffs.Add($"{c.Templates.Count} templates, not {Expected.Length}");
        foreach (var e in Expected)
        {
            var t = c.Find(e.Id);
            if (t is null) { diffs.Add($"{e.Id} missing"); continue; }
            var d = t.Definition;
            var got = (d.Id, PillarNames.Name(d.Pillar), EventLines.Trigger(d.Trigger), d.Conditions.CooldownMinutes, d.DurationSeconds, Describe(d));
            if (got != e) diffs.Add($"{e.Id}: {got} is not {e}");
            if (d.Conditions.MinPlayers != 0) diffs.Add($"{e.Id} has minPlayers");
            if (d.Trigger.Bosses.Count > 20) diffs.Add($"{e.Id} has {d.Trigger.Bosses.Count} bosses");
            if (d.Announce.Start.Count == 0 || d.Announce.End.Count == 0) diffs.Add($"{e.Id} lacks a start or end line");
        }
        return diffs;
    }

    [Fact]
    public void StarterTemplates_passes_six_as_business_rules() => Assert.Empty(Differences(Real()));

    [Theory]
    [InlineData("duration")]
    [InlineData("missing")]
    [InlineData("seventh")]
    [InlineData("days")]
    [InlineData("phase")]
    [InlineData("bosses")]
    [InlineData("factions")]
    [InlineData("stats")]
    [InlineData("units")]
    [InlineData("waves")]
    [InlineData("interval")]
    [InlineData("radius")]
    [InlineData("location")]
    [InlineData("cooldown")]
    [InlineData("pillar")]
    [InlineData("trigger type")]
    [InlineData("times")]
    public void StarterTemplates_fails_when_a_field_differs(string mutation)
    {
        var root = JsonNode.Parse(RealText)!;
        var events = (JsonArray)root["events"]!;
        JsonObject Ev(string id) => events.OfType<JsonObject>().First(e => (string)e["id"]! == id);
        switch (mutation)
        {
            case "duration": Ev("undead-nightfall")["durationSeconds"] = 1300; break;
            case "missing": events.Remove(Ev("undead-rising")); break;
            case "seventh": events.Add(JsonNode.Parse(Ev("bandit-ambush").ToJsonString()!)!.AsObject().Also(o => o["id"] = "bandit-ambush-2")); break;
            case "days": Ev("legion-weekend-surge")["trigger"]!["days"] = new JsonArray("Sun"); break;
            case "phase": Ev("undead-nightfall")["trigger"]!["phase"] = "day"; break;
            case "bosses": ((JsonArray)Ev("bandit-vengeance")["trigger"]!["bosses"]!).RemoveAt(0); break;
            case "factions": ((JsonArray)Ev("militia-crackdown")["action"]!["factions"]!).RemoveAt(1); break;
            case "stats": Ev("legion-weekend-surge")["action"]!["stats"]!["maxHealth"] = 1.4; break;
            case "units": Ev("bandit-ambush")["action"]!["units"]![1]!["count"] = 3; break;
            case "waves": Ev("undead-rising")["action"]!["waves"] = 3; break;
            case "interval": Ev("bandit-ambush")["action"]!["intervalSeconds"] = 90; break;
            case "radius": Ev("undead-rising")["action"]!["radius"] = 10; break;
            case "location": Ev("bandit-ambush")["action"]!["location"] = JsonNode.Parse("{\"type\":\"Point\",\"x\":0,\"z\":0}"); break;
            case "cooldown": Ev("militia-crackdown")["conditions"]!["cooldownMinutes"] = 60; break;
            case "trigger type": Ev("undead-nightfall")["trigger"] = JsonNode.Parse(@"{""type"":""Manual""}"); break;
            case "times": Ev("legion-weekend-surge")["trigger"]!["times"] = new JsonArray("21:00"); break;
            default: Ev("bandit-ambush")["pillar"] = "boss"; break;
        }
        var c = TemplateCatalog.Load(Bytes(root.ToJsonString()), Units(), Units());
        Assert.NotEmpty(Differences(c));
    }

    [Fact]
    public void StarterTemplates_empty_no_templates() =>
        Assert.Contains("0 templates, not 6", Differences(TemplateCatalog.Load(Bytes("{\"SchemaVersion\":1,\"events\":[]}"), Units(), Units())));
}

static class JsonNodeExtensions
{
    public static T Also<T>(this T value, Action<T> act)
    {
        act(value);
        return value;
    }
}
