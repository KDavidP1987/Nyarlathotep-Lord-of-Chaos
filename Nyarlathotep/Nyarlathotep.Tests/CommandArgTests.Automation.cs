using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D16 (the `.nyar event set` values of the new trigger fields and action.fanOut, each in D1, D4, D8
/// and D10's ranges) and D27 (every new chat line within 480 bytes at the maximum lengths of its fields, no markup).</summary>
public partial class CommandArgTests
{
    // ---- D16 Automation

    [Theory]
    [InlineData("trigger.type", "interval", CommandArgs.TriggerTypeRule)]
    [InlineData("trigger.type", "Hourly", CommandArgs.TriggerTypeRule)]
    [InlineData("trigger.minMinutes", "4", EventValidator.MinMinutesRule)]
    [InlineData("trigger.minMinutes", "1441", EventValidator.MinMinutesRule)]
    [InlineData("trigger.minMinutes", "+5", EventValidator.MinMinutesRule)]
    [InlineData("trigger.maxMinutes", "4", EventValidator.MaxMinutesRule)]
    [InlineData("trigger.maxMinutes", "90.5", EventValidator.MaxMinutesRule)]
    [InlineData("trigger.playerCooldownMinutes", "-1", EventValidator.PlayerCooldownRule)]
    [InlineData("trigger.playerCooldownMinutes", "1441", EventValidator.PlayerCooldownRule)]
    [InlineData("trigger.kills", "2", EventValidator.KillsRule)]
    [InlineData("trigger.kills", "501", EventValidator.KillsRule)]
    [InlineData("trigger.windowSeconds", "9", EventValidator.KillWindowRule)]
    [InlineData("trigger.windowSeconds", "3601", EventValidator.KillWindowRule)]
    [InlineData("trigger.shared", "yes", EventValidator.SharedRule)]
    [InlineData("trigger.shared", "True", EventValidator.SharedRule)]
    [InlineData("trigger.factions", "Faction_A,Faction_B,Faction_C,Faction_D,Faction_E,Faction_F", "trigger.factions must be 1-5 Faction_ names, comma separated")]
    [InlineData("trigger.factions", "Bandits", "trigger.factions names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters")]
    [InlineData("action.fanOut", "1 150", EventValidator.FanOutInstancesRule)]
    [InlineData("action.fanOut", "11 150", EventValidator.FanOutInstancesRule)]
    [InlineData("action.fanOut", "3 49", EventValidator.FanOutSpacingRule)]
    [InlineData("action.fanOut", "3 501", EventValidator.FanOutSpacingRule)]
    [InlineData("action.fanOut", "3", CommandArgs.FanOutRule)]
    [InlineData("action.fanOut", "3 150 2", CommandArgs.FanOutRule)]
    [InlineData("action.fanOut", "three 150", EventValidator.FanOutInstancesRule)]
    public void Automation_fails_when_value_out_of_range(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
    }

    [Theory]
    [InlineData("trigger.minMinutes", "Interval")]
    [InlineData("trigger.maxMinutes", "Interval")]
    [InlineData("trigger.playerCooldownMinutes", "RegionEntered")]
    [InlineData("trigger.factions", "FactionKills")]
    [InlineData("trigger.kills", "FactionKills")]
    [InlineData("trigger.windowSeconds", "FactionKills")]
    [InlineData("trigger.shared", "FactionKills")]
    public void Automation_passes_field_maps_to_its_type(string field, string type)
    {
        Assert.Equal(type, CommandArgs.TriggerTypeOf(field));
        Assert.Contains(field, CommandArgs.TriggerFields);
    }

    [Theory]
    [InlineData("trigger.type", "Interval", "Interval")]
    [InlineData("trigger.type", "RegionEntered", "RegionEntered")]
    [InlineData("trigger.type", "FactionKills", "FactionKills")]
    [InlineData("trigger.minMinutes", "5", "5")]
    [InlineData("trigger.maxMinutes", "1440", "1440")]
    [InlineData("trigger.playerCooldownMinutes", "0", "0")]
    [InlineData("trigger.playerCooldownMinutes", "1440", "1440")]
    [InlineData("trigger.kills", "3", "3")]
    [InlineData("trigger.kills", "500", "500")]
    [InlineData("trigger.windowSeconds", "10", "10")]
    [InlineData("trigger.windowSeconds", "3600", "3600")]
    [InlineData("trigger.shared", "true", "True")]
    [InlineData("trigger.shared", "false", "False")]
    [InlineData("action.fanOut", "2 50", "2 50")]
    [InlineData("action.fanOut", "10 500", "10 500")]
    [InlineData("action.fanOut", "none", "none")]
    public void Automation_passes_values_in_range(string field, string value, string shown)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.True(arg.Ok, arg.Error);
        Assert.Equal(shown, Convert.ToString(arg.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(CommandArgs.IsSettable(field));
        if (value == "none") Assert.Same(FieldRemoval.Instance, arg.Value);
    }

    [Theory]
    [InlineData("trigger.minMinutes")]
    [InlineData("trigger.maxMinutes")]
    [InlineData("trigger.playerCooldownMinutes")]
    [InlineData("trigger.factions")]
    [InlineData("trigger.kills")]
    [InlineData("trigger.windowSeconds")]
    [InlineData("trigger.shared")]
    [InlineData("action.fanOut")]
    public void Automation_empty_value(string field)
    {
        Assert.False(CommandArgs.SettableValue(field, null).Ok);
        Assert.False(CommandArgs.SettableValue(field, "").Ok);
    }

    // ---- D27 ChatBytes

    static readonly string MaxFaction = "Faction_" + new string('F', CommandArgs.MaxNameLength - "Faction_".Length);

    /// <summary>The widest definition of each new trigger type, with a fanOut at its widest.</summary>
    static IEnumerable<EventDefinition> AutomationDefinitions(string id)
    {
        var action = new SpawnWavesAction([new UnitEntry(MaxUnit, 50, 0.55)], 10, 600, 30, new Location(LocationType.AroundPlayer, 0, 0, null, 60, 80), 7200,
            Behaviour: new Behaviour(BehaviourType.Hunt, 60), FanOut: new FanOut(EventValidator.MaxFanOutInstances, EventValidator.MaxFanOutSpacing));
        EventDefinition Def(Trigger t) => new(id, new string('N', 40), true, Pillar.Spawns, t, new Conditions(), 7200, action, Announce.None);
        yield return Def(new Trigger(TriggerType.Interval, [], [], DayPhase.Night, [], MinMinutes: 1440, MaxMinutes: 1440));
        yield return Def(new Trigger(TriggerType.RegionEntered, [], [], DayPhase.Night, [], PlayerCooldownMinutes: 1440));
        yield return Def(new Trigger(TriggerType.FactionKills, [], [], DayPhase.Night, [],
            Factions: Enumerable.Range(0, EventValidator.MaxFactions).Select(i => MaxFaction[..^1] + i).ToList(), Kills: 500, WindowSeconds: 3600, Shared: true));
    }

    static IEnumerable<string> AutomationReasons() =>
    [
        EventValidator.MinMinutesRule, EventValidator.MaxMinutesRule, EventValidator.IntervalOrder, EventValidator.RegionEnteredScopeRule,
        EventValidator.PlayerCooldownRule, EventValidator.TriggerFactionsRule, EventValidator.KillsRule, EventValidator.KillWindowRule,
        EventValidator.SharedRule, EventValidator.FanOutLocation, EventValidator.FanOutInstancesRule, EventValidator.FanOutSpacingRule,
        CommandArgs.TriggerTypeRule, CommandArgs.FanOutRule, "trigger.factions must be 1-5 Faction_ names, comma separated",
        "trigger.minMinutes needs an Interval trigger", "trigger.playerCooldownMinutes needs a RegionEntered trigger", "trigger.shared needs a FactionKills trigger",
    ];

    /// <summary>Every new line of the child at its widest: the set replies (each with the longest reload reason), the
    /// reasons as `.nyar event list` shows them, `event info` of each new trigger type, the fan-out and phantom lines and
    /// the gate's throttled refusal.</summary>
    static List<string> AutomationLines(string id, DateTime now)
    {
        var reasons = AutomationReasons().ToList();
        var longest = reasons.OrderByDescending(r => Encoding.UTF8.GetByteCount(r)).First();
        var sets = new[]
        {
            ("trigger.type", "RegionEntered"), ("trigger.minMinutes", "1440"), ("trigger.maxMinutes", "1440"), ("trigger.playerCooldownMinutes", "1440"),
            // the reply echoes the typed value, so it is as long as the command chat accepted: five 40-character names
            ("trigger.factions", string.Join(",", Enumerable.Repeat(MaxFaction[..40], EventValidator.MaxFactions))), ("trigger.kills", "500"),
            ("trigger.windowSeconds", "3600"), ("trigger.shared", "false"), ("action.fanOut", "10 500"),
        };
        var lines = new List<string>();
        lines.AddRange(sets.Select(s => $"event {id} {s.Item1} = {s.Item2}; now disabled: {longest}"));
        lines.AddRange(reasons);
        lines.AddRange(reasons.Select(r => $"{id} {Readiness.Invalid}{r}"));
        foreach (var d in AutomationDefinitions(id)) lines.AddRange(EventLines.Info(d, null, now, now.AddMinutes(1440)));
        lines.Add(EventLines.NextStartLine(true, null, now)!);
        lines.Add(EventLines.NextStartLine(false, now.AddMinutes(1440), now)!);
        lines.Add(WaveLines.NoFreeSlot(int.MaxValue, id));
        lines.Add(WaveLines.AroundPlayers(EventValidator.MaxFanOutInstances));
        lines.Add(Phantoms.PlacedLine(Phantoms.Max, Phantoms.Max));
        lines.Add(Phantoms.GroupLine(id, int.MaxValue, int.MaxValue, float.MaxValue));
        var gate = new PlayerTriggerGate();
        var refusal = $"event {id} not started by FactionKills: {longest}";
        gate.Refused(id, refusal, now);
        gate.Refused(id, refusal, now.AddSeconds(1));
        lines.Add(gate.Refused(id, refusal, now.AddSeconds(61))!);
        return lines;
    }

    [Fact]
    public void ChatBytes_fails_when_automation_line_exceeds_480()
    {
        var now = DateTime.UtcNow;
        var planted = $"event {MaxId} trigger.factions = " + new string('F', 480);
        Assert.Equal([planted], OverLimit(AutomationLines(MaxId, now).Append(planted)));
        // five 96-character factions do not fit one line whole: the trigger is shortened to its count and the names
        // follow on "factions:" lines, none dropped
        var kills = AutomationDefinitions(MaxId).Single(d => d.Trigger.Type == TriggerType.FactionKills);
        Assert.Single(OverLimit([$"{MaxId} x {EventLines.Trigger(kills.Trigger)}"]));
        var info = EventLines.Info(kills, null, now);
        Assert.Empty(OverLimit(info));
        Assert.Contains(info, l => l.Contains("trigger factionkills 5 factions 500 in 3600s shared", StringComparison.Ordinal));
        Assert.All(kills.Trigger.Factions!, f => Assert.Single(info, l => l.StartsWith("factions: ", StringComparison.Ordinal)
            && l.Contains(FactionDenyList.ShortName(f), StringComparison.Ordinal)));
        Assert.Empty(OverLimit([EventLines.Line(kills, false, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3))]));
    }

    [Fact]
    public void ChatBytes_fails_when_automation_line_holds_markup()
    {
        var planted = "trigger.minMinutes must be <5-1440>";
        Assert.Equal([planted], Markup(AutomationLines(MaxId, DateTime.UtcNow).Append(planted)));
        Assert.Empty(Markup([CommandArgs.TriggerTypeRule, CommandArgs.FanOutRule]));
    }

    [Fact]
    public void ChatBytes_passes_automation_lines_at_maximum_lengths()
    {
        var now = DateTime.UtcNow;
        var lines = AutomationLines(MaxId, now);
        Assert.Contains(lines, l => l.Contains("trigger interval 1440-1440 min", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("trigger regionentered cooldown 1440 min", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(" 500 in 3600s shared", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("fanOut 10 players 500 m apart", StringComparison.Ordinal));
        Assert.Contains("next start in 1440 min", lines);
        Assert.Contains("next start: after the running instance ends", lines);
        Assert.Contains(lines, l => l.EndsWith("; 1 more since the last line", StringComparison.Ordinal));
        Assert.Empty(OverLimit(lines));
        Assert.Empty(Markup(lines));
    }

    [Fact]
    public void ChatBytes_empty_automation_fields()
    {
        var lines = AutomationLines("", DateTime.UtcNow);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.InRange(Encoding.UTF8.GetByteCount(l), 1, Wire.MaxBytes));
        Assert.Null(EventLines.NextStartLine(false, null, DateTime.UtcNow));
    }
}
