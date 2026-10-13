using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D12: `.nyar event info` of a Revenge definition shows its trigger on a line of its own and, for an
/// admin, the counts line; every line fits 480 bytes with five 30-character factions; `.nyar event new &lt;id&gt; revenge`
/// writes a disabled, valid revenge definition.</summary>
public partial class AuthoringTests
{
    static EventDefinition RvEvent(string trigger, FakeUnits? units = null)
    {
        var r = EventValidator.Parse(Json.File(Json.Event("rv", trigger, action: Json.RevengeAction)), units ?? FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    const string RvTriggerLine = "trigger: revenge Bandits heat 6 in 900 s, warn at 2,4, vblood +10, cooldown 30 min";

    [Fact]
    public void Revenge_passes_info_lines_and_new_definition()
    {
        var d = RvEvent(Json.RevengeTrigger);
        var lines = EventLines.Info(d, null, Now, null, (3, 1));
        Assert.Equal("rv \"Bandit raid\" enabled pillar spawns trigger revenge duration 600s", lines[0]);
        Assert.Equal(RvTriggerLine, lines[1]);
        Assert.Equal("revenge: 3 heat rows, 1 due, 0 running", lines[2]);
        Assert.Contains("action: 3 waves every 60s, radius 10, around a player 20-40 m, units 4 CHAR_Bandit_Thug", lines);
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "Revenge", null, "p1");
        Assert.Equal("revenge: 0 heat rows, 0 due, 1 running", EventLines.Info(d, active, Now, null, (0, 0))[2]);
        Assert.DoesNotContain(EventLines.Info(d, null, Now), l => l.StartsWith("revenge:", StringComparison.Ordinal));     // no counts: no counts line

        var lib = Lib(Json.Event("raid"));
        Assert.Equal("event rv-new created (disabled, spawns, revenge); set its fields with .nyar event set",
            lib.Write(t => Authoring.New(t, "rv-new", Authoring.RevengeKind)));
        var made = lib.Catalog.Current.Find("rv-new")!;
        Assert.Null(made.DisabledReason);
        Assert.False(made.Enabled);
        var t = made.Trigger;
        Assert.Equal((TriggerType.Revenge, 30, 900, 10, 30), (t.Type, t.Heat, t.WindowSeconds, t.VBloodHeat, t.PlayerCooldownMinutes));
        Assert.Equal([10, 20], t.WarnAt);
        var a = made.Action!;
        Assert.Equal((LocationType.AroundPlayer, 20, 40), (a.Location.Type, a.Location.MinDist, a.Location.MaxDist));
        Assert.Equal(new Behaviour(BehaviourType.Hunt, 45), a.Behaviour);
        Assert.Equal(["CHAR_Bandit_Thug", "CHAR_Bandit_Hunter"], a.Units.Select(u => u.Prefab));
        Assert.Contains("trigger: revenge Bandits heat 30 in 900 s, warn at 10,20, vblood +10, cooldown 30 min", EventLines.Info(made, null, Now));
    }

    [Fact]
    public void Revenge_fails_when_a_field_is_missing_from_the_line()
    {
        var line = EventLines.Info(RvEvent("{ \"type\": \"Revenge\", \"factions\": [\"Faction_Bandits\", \"Faction_Legion\"], \"heat\": 40, \"windowSeconds\": 600, " +
            "\"warnAt\": [10, 20, 30], \"vbloodHeat\": 7, \"playerCooldownMinutes\": 15 }"), null, Now)[1];
        Assert.Equal("trigger: revenge Bandits,Legion heat 40 in 600 s, warn at 10,20,30, vblood +7, cooldown 15 min", line);
        foreach (var part in new[] { "Bandits", "Legion", "heat 40", "in 600 s", "warn at 10,20,30", "vblood +7", "cooldown 15 min" })
            Assert.Contains(part, line);
    }

    [Fact]
    public void Revenge_fails_when_a_line_exceeds_480_bytes()
    {
        var names = Enumerable.Range(0, 5).Select(i => $"Faction_{new string((char)('A' + i), 22)}").ToList();
        var units = new FakeUnits("CHAR_Bandit_Thug");
        foreach (var n in names) units.Factions.Add(n);
        var d = RvEvent("{ \"type\": \"Revenge\", \"factions\": [" + string.Join(", ", names.Select(n => $"\"{n}\"")) + "], \"heat\": 500, " +
            "\"windowSeconds\": 3600, \"warnAt\": [100, 250, 499], \"vbloodHeat\": 100, \"playerCooldownMinutes\": 1440, " +
            "\"scope\": [" + string.Join(", ", RegionNames.All.Select(r => $"\"{r}\"")) + "] }", units);
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "Revenge", null, "p1");
        var lines = EventLines.Info(d, active, Now, null, (TriggerLimits.CountersInAll, TriggerLimits.CountersInAll));
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(l)} bytes: {l}"));
        var all = string.Join(" ", lines);
        Assert.All(names, n => Assert.Contains(FactionDenyList.ShortName(n), all));                     // every faction shown, on its own line if need be
        Assert.Contains("warn at 100,250,499", all);
    }

    [Fact]
    public void Revenge_fails_when_the_counts_line_shows_a_name_or_id()
    {
        var d = RvEvent(Json.RevengeTrigger);
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "Revenge", null, "76000000000000042");
        var counts = EventLines.Info(d, active, Now, null, (12, 2)).Single(l => l.StartsWith("revenge:", StringComparison.Ordinal));
        Assert.Equal("revenge: 12 heat rows, 2 due, 1 running", counts);
        Assert.Matches(@"^revenge: \d+ heat rows, \d+ due, [01] running$", counts);
        Assert.DoesNotContain(EventLines.Info(d, active, Now, null, (12, 2)), l => l.Contains("76000", StringComparison.Ordinal));
    }

    [Fact]
    public void Revenge_fails_when_the_new_definition_is_enabled_or_invalid()
    {
        var lib = Lib(Json.Event("raid"));
        lib.Write(t => Authoring.New(t, "rv-new", "revenge"));
        var made = lib.Catalog.Current.Find("rv-new")!;
        Assert.False(made.Enabled);
        Assert.Null(made.DisabledReason);
        Assert.Equal(Pillar.Spawns, made.Pillar);
        // a refused id and a duplicate write nothing
        Unchanged(lib, () => Assert.Equal("event rv-new already exists", lib.Write(t => Authoring.New(t, "rv-new", "revenge"))));
        Unchanged(lib, () => Assert.NotEqual("event Bad Id created", lib.Write(t => Authoring.New(t, "Bad Id", "revenge"))));
    }

    [Fact]
    public void Revenge_fails_when_a_faction_kills_info_changes()
    {
        var fk = RvEvent("{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }");
        var lines = EventLines.Info(fk, null, Now, null, (3, 1));
        Assert.Equal("rv \"Bandit raid\" enabled pillar spawns trigger factionkills Bandits 20 in 300s duration 600s", lines[0]);
        Assert.StartsWith("conditions: ", lines[1]);
        Assert.DoesNotContain(lines, l => l.StartsWith("revenge", StringComparison.Ordinal) || l.StartsWith("trigger:", StringComparison.Ordinal));
    }

    [Fact]
    public void Revenge_empty_warn_at_none() =>
        Assert.Equal("trigger: revenge Bandits heat 6 in 900 s, warn at none, vblood +10, cooldown 30 min",
            EventLines.Info(RvEvent(Json.RevengeTrigger.Replace("[2, 4]", "[]")), null, Now)[1]);
}
