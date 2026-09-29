using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D19: no new line (the trigger texts, `event info`, the fan-out and phantom lines, the gate's
/// refusal) names or locates a player; the focus, region rows, cooldowns and kill counters live in memory only, so
/// state.json gains no per-player field.</summary>
public partial class PrivacyTests
{
    static readonly (float X, float Z) EnteredAt = (4321.7f, -8765.3f);
    static readonly string Focus = PlantedId.ToString();

    static IEnumerable<EventDefinition> AutomationPlanted()
    {
        var around = AroundPlayerDefinition();
        var action = around.Action! with { FanOut = new FanOut(3, 150) };
        yield return around with { Id = "tick", Trigger = new Trigger(TriggerType.Interval, [], [], DayPhase.Night, [], MinMinutes: 60, MaxMinutes: 90), Action = action };
        yield return around with
        {
            Id = "border", Action = action,
            Trigger = new Trigger(TriggerType.RegionEntered, [], [], DayPhase.Night, [], Scope: new Scope(["FarbaneWoods"]), PlayerCooldownMinutes: 30),
        };
        yield return around with
        {
            Id = "kills", Action = action,
            Trigger = new Trigger(TriggerType.FactionKills, [], [], DayPhase.Night, [], Factions: ["Faction_Bandits"], Kills: 20, WindowSeconds: 300),
        };
    }

    /// <summary>Every new line the child can produce for a player who entered a region or killed a unit at the planted
    /// position, with the planted id as the start's focus.</summary>
    static List<string> AutomationLines()
    {
        var lines = new List<string>();
        foreach (var d in AutomationPlanted())
        {
            var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), ApiLines.Trigger(d.Trigger.Type), null, Focus);
            lines.AddRange(EventLines.Info(d, active, Now, Now.AddMinutes(30)));
            lines.AddRange(EventLines.Info(d, null, Now, Now.AddMinutes(30)));
            lines.Add(PushLines.EventStart(active.Instance).Text);
            lines.AddRange(ApiLines.Status([active], [], new DefinitionSet([d]), new Dictionary<string, int>(), true, Now));
            lines.AddRange(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { d.Id }));
            lines.Add(new PlayerTriggerGate().Refused(d.Id, $"event {d.Id} not started by {d.Trigger.Type}: cooldown 30 min", Now)!);
        }
        lines.Add(WaveLines.AroundPlayers(3));
        lines.Add(WaveLines.NoFreeSlot(1, "tick"));
        lines.Add(Phantoms.PlacedLine(4, 4));
        lines.Add(Phantoms.GroupLine("tick", 1, 2, 12.4));
        return lines;
    }

    static readonly string[] EntryMarks = [PlantedName, Focus, "4321", "8765"];

    [Fact]
    public void Automation_passes_no_line_names_or_locates_a_player()
    {
        var lines = AutomationLines();
        Assert.Contains(lines, l => l.Contains("fanOut 3 players 150 m apart", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in EntryMarks) Assert.DoesNotContain(mark, line, StringComparison.OrdinalIgnoreCase);
        // the region an entry shows is the event's own scope, never the player's region
        Assert.Contains("trigger scope: FarbaneWoods", lines);
        // the phantom line builders take counts, ids and a distance only
        foreach (var m in typeof(Phantoms).GetMethods().Where(m => m.Name.EndsWith("Line", StringComparison.Ordinal)))
            Assert.All(m.GetParameters(), p => Assert.Contains(p.ParameterType, new[] { typeof(int), typeof(string), typeof(double) }));
    }

    [Fact]
    public void Automation_fails_when_a_line_names_the_player()
    {
        var leaked = $"event border started: {PlantedName} entered at {EnteredAt.X} {EnteredAt.Z}";
        Assert.Contains(EntryMarks, m => leaked.Contains(m, StringComparison.Ordinal));
        Assert.DoesNotContain(leaked, AutomationLines());
        // a phantom group line carries its distance only, not the phantom's position
        Assert.Equal("fanout tick wave 1: phantom group 2 12 m from its phantom", Phantoms.GroupLine("tick", 1, 2, 12.4));
    }

    [Fact]
    public void Automation_fails_when_state_gains_a_per_player_field()
    {
        Assert.Equal(["DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var doc = new StateDocument { NextInterval = new() { ["tick"] = Now.AddMinutes(30) } };
        doc.LastStart["border"] = Now;
        var text = Encoding.UTF8.GetString(doc.Serialize());
        foreach (var mark in EntryMarks) Assert.DoesNotContain(mark, text, StringComparison.Ordinal);
        // the per-player rows have no serialised form: none is a StateDocument member type
        foreach (var t in new[] { typeof(RegionEntries), typeof(KillWindows), typeof(PlayerTriggerGate) })
            Assert.DoesNotContain(typeof(StateDocument).GetProperties(), p => p.PropertyType == t);
    }

    [Fact]
    public void Automation_empty_no_focus()
    {
        var d = AutomationPlanted().First();
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "interval", null);
        Assert.Null(active.Focus);
        Assert.All(EventLines.Info(d, active, Now), l => Assert.All(EntryMarks, m => Assert.DoesNotContain(m, l, StringComparison.Ordinal)));
    }
}
