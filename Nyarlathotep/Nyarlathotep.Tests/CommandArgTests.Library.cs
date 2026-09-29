using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D9 and D10 (the character set, shape and name rule of the new field values, before any write)
/// and D20 (SettableFields is exactly what SettableValue accepts; a malformed command form gets its usage reply).</summary>
public partial class CommandArgTests
{
    // ---- D9 TriggerFields: shape before any write

    [Theory]
    [InlineData("trigger.days", "Sat Sun")]
    [InlineData("trigger.days", "Sat,\"Sun\"")]
    [InlineData("trigger.days", "Sat,'Sun'")]
    [InlineData("trigger.days", "{Sat}")]
    [InlineData("trigger.times", "[20:00]")]
    [InlineData("trigger.times", "20:00\n")]
    [InlineData("trigger.times", "20:00\\")]
    [InlineData("trigger.bosses", "CHAR_<b>")]
    [InlineData("trigger.bosses", "CHAR_Bossé")]
    [InlineData("trigger.phase", "night ")]
    public void TriggerFields_fails_when_characters_are_bad(string field, string value)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.StartsWith(field, arg.Error);
    }

    [Theory]
    [InlineData("trigger.days", "Sun,Mon,Tue,Wed,Thu,Fri,Sat")]
    [InlineData("trigger.times", "00:00,23:59")]
    [InlineData("trigger.phase", "night")]
    [InlineData("trigger.bosses", "any")]
    [InlineData("trigger.type", "VBloodKilled")]
    public void TriggerFields_passes_well_formed(string field, string value) => Assert.True(CommandArgs.SettableValue(field, value).Ok);

    [Theory]
    [InlineData("trigger.days", "Sat,,Sun")]
    [InlineData("trigger.times", ",20:00")]
    [InlineData("trigger.bosses", "CHAR_A,")]
    public void TriggerFields_empty_list_entry(string field, string value) => Assert.False(CommandArgs.SettableValue(field, value).Ok);

    // ---- D10 ActionFields: the 96-character name rule

    [Theory]
    [InlineData("action.units", 97, "CHAR_")]
    [InlineData("action.factions", 97, "Faction_")]
    [InlineData("action.units", 5, "CHAR_")]
    [InlineData("action.factions", 8, "Faction_")]
    public void ActionFields_fails_when_name_breaks_rule(string field, int length, string prefix)
    {
        var name = prefix + new string('a', length - prefix.Length);
        Assert.Equal(CommandArgs.NameRule(field), CommandArgs.SettableValue(field, name).Error);
    }

    [Theory]
    [InlineData("action.units", "CHAR_")]
    [InlineData("action.factions", "Faction_")]
    public void ActionFields_passes_96_character_name(string field, string prefix) =>
        Assert.True(CommandArgs.SettableValue(field, prefix + new string('a', 96 - prefix.Length)).Ok);

    [Theory]
    [InlineData("action.units", "CHAR_A,,CHAR_B")]
    [InlineData("action.factions", "Faction_Legion,")]
    public void ActionFields_empty_list_entry(string field, string value) => Assert.False(CommandArgs.SettableValue(field, value).Ok);

    // ---- D20 SettableFields

    /// <summary>A well-formed value of each family, for the acceptance direction.</summary>
    static string ValidValue(string field) => field switch
    {
        "name" => "Raid",
        "trigger.type" => "Manual",
        "trigger.days" => "Sat",
        "trigger.times" => "20:00",
        "trigger.phase" => "day",
        "trigger.bosses" => "any",
        "trigger.playerCooldownMinutes" => "30",
        "trigger.factions" => "Faction_Bandits",
        "trigger.kills" => "20",
        "trigger.windowSeconds" => "300",
        "trigger.shared" => "true",
        "action.fanOut" => "3 150",
        "action.factions" => "Faction_Legion",
        "action.units" => "CHAR_Bandit_Thug:2",
        "location" => "here",
        "action.loot" or "action.allowTerritory" => "true",
        "action.behaviour" => "hunt 40",
        "action.modifiers.level" => "30",
        "action.modifiers.levelDelta" => "-2",
        _ when field.StartsWith("action.modifiers.", StringComparison.Ordinal) => "1.5",
        _ when field.StartsWith("action.units.", StringComparison.Ordinal) => "0.5",
        "trigger.scope" or "action.scope" => "Global",
        _ when field.StartsWith("action.stats.", StringComparison.Ordinal) => "1.5",
        "conditions.chancePercent" => "50",
        "action.waves" or "action.radius" => "3",
        _ => "60",
    };

    [Fact]
    public void SettableFields_passes_every_table_name()
    {
        Assert.Equal(41, CommandArgs.SettableFields.Count);      // automation D16: 7 trigger fields and action.fanOut
        foreach (var (name, (family, who)) in CommandArgs.SettableFields)
        {
            var field = name.Replace(".N.", ".1.", StringComparison.Ordinal);        // the table's unit-chance placeholder
            Assert.True(CommandArgs.SettableValue(field, ValidValue(field)).Ok, field);
            Assert.Contains(family, new[] { "definition", "trigger", "empower action", "spawn action", "location", "action" });
            Assert.Equal("admin", who);
        }
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("id")]
    [InlineData("pillar")]
    [InlineData("action.type")]
    [InlineData("action.location")]
    [InlineData("action.includeUnits")]
    [InlineData("conditions.window")]
    [InlineData("announce.start")]
    public void SettableFields_fails_when_name_outside_table(string field)
    {
        Assert.False(CommandArgs.SettableFields.ContainsKey(field));
        Assert.Equal($"field {field} is not settable; edit events.json and reload", CommandArgs.SettableValue(field, "x").Error);
    }

    [Fact]
    public void SettableFields_empty_field_name() =>
        Assert.Equal("field  is not settable; edit events.json and reload", CommandArgs.SettableValue("", "x").Error);

    // ---- D20 CommandForms: a malformed form gets its usage reply before the gateway

    [Theory]
    [InlineData("template", "use t as", "usage: .nyar template use template [as id]")]
    [InlineData("template", "use t for x", "usage: .nyar template use template [as id]")]
    [InlineData("template", "info a b", "usage: .nyar template info template")]
    [InlineData("template", "list spawns 2 3", "usage: .nyar template list [pillar] [page]")]
    [InlineData("event", "set raid name", "usage: .nyar event set id field value")]
    [InlineData("event", "new raid", "usage: .nyar event new id pillar")]
    [InlineData("event", "copy raid", "usage: .nyar event copy id newId")]
    [InlineData("event", "copy raid raid-2 x", "usage: .nyar event copy id newId")]
    [InlineData("event", "new raid spawns x", "usage: .nyar event new id pillar")]
    [InlineData("event", "delete raid now", "usage: .nyar event delete id [confirm]")]
    [InlineData("pillar", "spawns", "usage: .nyar pillar name on|off")]
    [InlineData("pillar", "list all", "usage: .nyar pillar list")]
    public void CommandForms_fails_when_form_is_malformed(string group, string words, string usage)
    {
        Assert.Null(CommandForms.Check(group, words.Split(' '), out var reply));
        Assert.Equal(usage, reply);
        Assert.DoesNotContain("<", reply!);
    }

    [Theory]
    [InlineData("template", "list", "template list")]
    [InlineData("template", "list spawns 2", "template list")]
    [InlineData("template", "use t as ambush-2", "template use")]
    [InlineData("event", "delete raid confirm", "event delete")]
    [InlineData("event", "set raid location here", "event set")]
    [InlineData("pillar", "spawns off", "pillar")]
    [InlineData("pillar", "list", "pillar list")]
    public void CommandForms_passes_each_declared_form(string group, string words, string form)
    {
        Assert.Equal(form, CommandForms.Check(group, words.Split(' '), out var reply)?.Words);
        Assert.Null(reply);
    }

    [Fact]
    public void CommandForms_empty_arguments()
    {
        Assert.Null(CommandForms.Check("template", ["info", "", ""], out var reply));      // VCF's empty defaults dropped
        Assert.Equal("usage: .nyar template info template", reply);
        Assert.Equal("template list", CommandForms.Check("template", ["list", "", ""], out _)!.Words);
        Assert.Null(CommandForms.Check("event", ["start", "raid"], out var other));          // foundation's own verb
        Assert.Null(other);
    }
}
