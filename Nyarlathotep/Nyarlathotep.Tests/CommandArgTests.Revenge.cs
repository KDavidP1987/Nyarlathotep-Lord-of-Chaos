using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D11: `trigger.type Revenge` writes the default revenge trigger; trigger.heat, trigger.warnAt and
/// trigger.vbloodHeat take exactly what the validator takes and refuse with its reason; trigger.factions,
/// trigger.windowSeconds and trigger.playerCooldownMinutes fit a Revenge trigger with its bounds and keep their own on the
/// other types; every reply fits 480 bytes; the §6 table lists the same fields.</summary>
public partial class CommandArgTests
{
    /// <summary>rv (Revenge, heat 6, warnAt 2,4), fk (FactionKills 20 in 300 s), raid (Manual), plain (Manual with the revenge action).</summary>
    static Library RvLib() => new(Json.File(
        Json.Event("rv", Json.RevengeTrigger, action: Json.RevengeAction),
        Json.Event("fk", "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }"),
        Json.Event("raid"),
        Json.Event("plain", action: Json.RevengeAction)), AuthoringTests.Units());

    static Trigger RvTrigger(Library lib, string id)
    {
        var d = lib.Catalog.Current.Find(id)!;
        Assert.True(d.Startable, d.DisabledReason);
        return d.Trigger;
    }

    static JsonObject RvJson(Library lib, string id) =>
        (JsonObject)((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id)["trigger"]!;

    static void RvRefused(Library lib, string id, string field, string value, string reason)
    {
        var hash = lib.Hash;
        var reply = AuthoringTests.Set(lib, id, field, value);
        Assert.Equal(reason, reply);
        Assert.Equal(hash, lib.Hash);                                                                   // nothing written
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes);
    }

    [Theory]
    [InlineData("trigger.heat", "2", EventValidator.HeatRule)]
    [InlineData("trigger.heat", "501", EventValidator.HeatRule)]
    [InlineData("trigger.heat", "30.5", EventValidator.HeatRule)]
    [InlineData("trigger.vbloodHeat", "-1", EventValidator.VBloodHeatRule)]
    [InlineData("trigger.vbloodHeat", "101", EventValidator.VBloodHeatRule)]
    [InlineData("trigger.warnAt", "0", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "500", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "20,10", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "10,10", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "1,2,3,4", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "a", EventValidator.WarnAtRule)]
    [InlineData("trigger.warnAt", "", EventValidator.WarnAtRule)]
    [InlineData("trigger.type", "revenge", CommandArgs.TriggerTypeRule)]
    public void Revenge_fails_when_a_field_accepts_a_value_the_validator_refuses(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
        // what the parse cannot see, the editor refuses with the validator's reason before writing
        var lib = RvLib();
        RvRefused(lib, "rv", "trigger.heat", "4", EventValidator.WarnAtRule);                          // warnAt 2,4 is not below heat 4
        RvRefused(lib, "rv", "trigger.warnAt", "2,6", EventValidator.WarnAtRule);                      // 6 is not below heat 6
        RvRefused(lib, "rv", "trigger.windowSeconds", "59", EventValidator.RevengeWindowRule);
        Assert.Equal(6, RvTrigger(lib, "rv").Heat);
    }

    [Fact]
    public void Revenge_fails_when_window_bounds_cross_trigger_types()
    {
        var lib = RvLib();
        Assert.StartsWith("event fk trigger.windowSeconds = 10", AuthoringTests.Set(lib, "fk", "trigger.windowSeconds", "10"));     // FactionKills: 10-3600
        Assert.Equal(10, RvTrigger(lib, "fk").WindowSeconds);
        RvRefused(lib, "rv", "trigger.windowSeconds", "10", EventValidator.RevengeWindowRule);                                  // Revenge: 60-3600
        Assert.StartsWith("event rv trigger.windowSeconds = 60", AuthoringTests.Set(lib, "rv", "trigger.windowSeconds", "60"));
        Assert.Equal(60, RvTrigger(lib, "rv").WindowSeconds);
        Assert.Equal(EventValidator.KillWindowRule, CommandArgs.SettableValue("trigger.windowSeconds", "3601").Error);
        // a field of one type is refused on the other
        RvRefused(lib, "fk", "trigger.heat", "30", "trigger.heat needs a Revenge trigger");
        RvRefused(lib, "fk", "trigger.warnAt", "none", "trigger.warnAt needs a Revenge trigger");
        RvRefused(lib, "fk", "trigger.vbloodHeat", "5", "trigger.vbloodHeat needs a Revenge trigger");
        RvRefused(lib, "rv", "trigger.kills", "30", "trigger.kills needs a FactionKills trigger");
        RvRefused(lib, "rv", "trigger.shared", "true", "trigger.shared needs a FactionKills trigger");
        RvRefused(lib, "raid", "trigger.heat", "30", "trigger.heat needs a Revenge trigger");
        // the shared fields fit both
        Assert.StartsWith("event rv trigger.factions = ", AuthoringTests.Set(lib, "rv", "trigger.factions", "Faction_Bandits,Faction_Legion"));
        Assert.Equal(["Faction_Bandits", "Faction_Legion"], RvTrigger(lib, "rv").Factions);
        Assert.StartsWith("event rv trigger.playerCooldownMinutes = 0", AuthoringTests.Set(lib, "rv", "trigger.playerCooldownMinutes", "0"));
        Assert.Equal(0, RvTrigger(lib, "rv").PlayerCooldownMinutes);
        Assert.Equal(["FactionKills", "Revenge"], CommandArgs.TriggerTypesOf("trigger.windowSeconds"));
        Assert.Equal(["RegionEntered", "Revenge"], CommandArgs.TriggerTypesOf("trigger.playerCooldownMinutes"));
        Assert.Equal(["Revenge"], CommandArgs.TriggerTypesOf("trigger.heat"));
        Assert.Equal(["FactionKills"], CommandArgs.TriggerTypesOf("trigger.kills"));
    }

    [Fact]
    public void Revenge_fails_when_a_reply_exceeds_480_bytes()
    {
        var lib = RvLib();
        var longFactions = string.Join(",", new[] { "Faction_Bandits", "Faction_Legion", "Faction_Undead", "Faction_Militia", "Faction_Gloomrot" });
        var replies = new List<string>
        {
            AuthoringTests.Set(lib, "raid", "trigger.type", "Revenge"),
            AuthoringTests.Set(lib, "rv", "trigger.factions", longFactions),
            AuthoringTests.Set(lib, "rv", "trigger.heat", "500"),
            AuthoringTests.Set(lib, "rv", "trigger.warnAt", "1,250,499"),
            AuthoringTests.Set(lib, "rv", "trigger.vbloodHeat", "100"),
            AuthoringTests.Set(lib, "rv", "trigger.windowSeconds", "3600"),
            AuthoringTests.Set(lib, "rv", "trigger.windowSeconds", "59"),
            AuthoringTests.Set(lib, "rv", "trigger.heat", "250"),
            AuthoringTests.Set(lib, "fk", "trigger.heat", "30"),
        };
        Assert.All(replies, r => Assert.True(Encoding.UTF8.GetByteCount(r) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(r)} bytes: {r}"));
        Assert.Equal([1, 250, 499], RvTrigger(lib, "rv").WarnAt);
    }

    [Fact]
    public void Revenge_fails_when_the_table_and_settable_fields_differ()
    {
        var design = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "NYARLATHOTEP_DESIGN.md"));
        foreach (var field in new[] { "trigger.heat", "trigger.warnAt", "trigger.vbloodHeat", "trigger.factions", "trigger.windowSeconds", "trigger.playerCooldownMinutes" })
        {
            Assert.Contains($"| `{field}` | trigger | admin |", design);
            Assert.Equal(("trigger", "admin"), CommandArgs.SettableFields[field]);
            Assert.Contains(field, CommandArgs.TriggerFields);
        }
        Assert.Contains("`trigger.type Revenge` gives Faction_Bandits, heat 30 in 900 s", design);
        Assert.Equal(CommandArgs.WarnAtField, "trigger.warnAt");
    }

    [Fact]
    public void Revenge_passes_default_trigger_and_each_field()
    {
        var lib = RvLib();
        Assert.StartsWith("event plain trigger.type = Revenge", AuthoringTests.Set(lib, "plain", "trigger.type", "Revenge"));
        var t = RvTrigger(lib, "plain");
        Assert.Equal((TriggerType.Revenge, 30, 900, 10, 30), (t.Type, t.Heat, t.WindowSeconds, t.VBloodHeat, t.PlayerCooldownMinutes));
        Assert.Equal(["Faction_Bandits"], t.Factions);
        Assert.Equal([10, 20], t.WarnAt);
        Assert.Equal(["type", "factions", "heat", "windowSeconds", "warnAt", "vbloodHeat", "playerCooldownMinutes"], RvJson(lib, "plain").Select(p => p.Key));
        // the raid's Point action is not one a Revenge trigger takes: the definition is disabled with the D2 reason
        Assert.StartsWith("event raid trigger.type = Revenge", AuthoringTests.Set(lib, "raid", "trigger.type", "Revenge"));
        Assert.Equal(EventValidator.RevengeNeedsAroundPlayer, lib.Catalog.Current.Find("raid")!.DisabledReason);

        Assert.StartsWith("event rv trigger.heat = 40", AuthoringTests.Set(lib, "rv", "trigger.heat", "40"));
        Assert.StartsWith("event rv trigger.warnAt = 10,20,30", AuthoringTests.Set(lib, "rv", "trigger.warnAt", "10,20,30"));
        Assert.StartsWith("event rv trigger.vbloodHeat = 0", AuthoringTests.Set(lib, "rv", "trigger.vbloodHeat", "0"));
        Assert.StartsWith("event rv trigger.windowSeconds = 3600", AuthoringTests.Set(lib, "rv", "trigger.windowSeconds", "3600"));
        var rv = RvTrigger(lib, "rv");
        Assert.Equal((40, 0, 3600), (rv.Heat, rv.VBloodHeat, rv.WindowSeconds));
        Assert.Equal([10, 20, 30], rv.WarnAt);
        Assert.StartsWith("event rv trigger.warnAt = none", AuthoringTests.Set(lib, "rv", "trigger.warnAt", "none"));
        Assert.Empty(RvTrigger(lib, "rv").WarnAt!);
        Assert.Empty((JsonArray)RvJson(lib, "rv")["warnAt"]!);
        Assert.StartsWith("event rv trigger.heat = 3", AuthoringTests.Set(lib, "rv", "trigger.heat", "3"));      // no warnings: any heat
        Assert.Equal(3, RvTrigger(lib, "rv").Heat);
    }

    [Fact]
    public void Revenge_empty_value()
    {
        // an empty value is refused with the field's rule, as trigger.kills (a missing argument is VCF's usage reply)
        Assert.Equal(EventValidator.HeatRule, CommandArgs.SettableValue("trigger.heat", "").Error);
        Assert.Equal(EventValidator.WarnAtRule, CommandArgs.SettableValue("trigger.warnAt", "").Error);
        Assert.Equal(EventValidator.VBloodHeatRule, CommandArgs.SettableValue("trigger.vbloodHeat", "").Error);
        Assert.Equal(EventValidator.KillsRule, CommandArgs.SettableValue("trigger.kills", "").Error);
    }
}
