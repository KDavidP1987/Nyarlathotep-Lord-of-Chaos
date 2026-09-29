using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>regions D14: no region line, read or push carries a player's position, name or id; `region=` names only
/// an event's configured scope, and `region here` answers the region's name alone.</summary>
public partial class PrivacyTests
{
    /// <summary>A scoped definition started at the planted position, and every region line, read and push over it.</summary>
    static List<string> RegionLinesOver(EventDefinition def)
    {
        var active = new ActiveEvent(new RunningInstance(def, Now, Now.AddMinutes(10)), "manual", (PlantedCoord, PlantedCoord, PlantedCoord));
        var set = new DefinitionSet([def]);
        var lines = new List<string>();
        lines.AddRange(RegionLines.List(set));
        lines.Add(RegionLines.Here(true, (_, _) => "CursedForest", PlantedCoord, PlantedCoord));
        lines.Add(RegionLines.Here(true, (_, _) => RegionNames.None, PlantedCoord, PlantedCoord));
        lines.AddRange(ApiLines.Regions([active]));
        lines.AddRange(ApiLines.Definitions(set, new HashSet<string>()));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.Add(PushLines.EventEnd(def.Id, ApiLines.Region(def)).Text);
        lines.Add(AdminLines.StartRefusedLog(def.Id, "schedule", Outcome.Refused("x", RefusalCode.State, reason: Reasons.NoPlayerInRegion), true,
            def.Action!.Scope));
        lines.AddRange(EventLines.Info(def, null, Now));
        lines.Add(Messages.Render("Raiders in {region}.", MessageContext.For(def, 5, 1)));
        return lines;
    }

    /// <summary>The planted definition at an Admin location: its own configured point would show in `event info`
    /// by design, so only a player position (the planted origin, kill or `here` point) could put 12345 in a line.</summary>
    static EventDefinition AtAdmin(string id, Scope scope) =>
        PlantedDefinition(id) with { Action = PlantedDefinition().Action! with { Location = new Location(LocationType.Admin, 0, 0), Scope = scope } };

    static EventDefinition PlantedScoped() =>
        AtAdmin("raid", new Scope(["CursedForest", "FarbaneWoods"])) with { Trigger = Trigger.Manual() with { Scope = new Scope(["CursedForest"]) } };

    [Fact]
    public void Region_passes_no_position_name_or_id()
    {
        var lines = RegionLinesOver(PlantedScoped());
        Assert.True(lines.Count > 20);
        Assert.Contains(lines, l => l.Contains("CursedForest", StringComparison.Ordinal));
        foreach (var line in lines)
        {
            Assert.DoesNotContain("12345", line);
            Assert.DoesNotContain(PlantedId.ToString(), line);
            Assert.DoesNotContain(PlantedName, line, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Region_fails_when_a_line_carries_the_position()
    {
        // the check itself: a line that did carry the planted position is caught
        var leaked = $"[NYAR:region] id=CursedForest events=1 x={PlantedCoord}";
        Assert.Contains("12345", leaked);
        Assert.DoesNotContain(RegionLinesOver(PlantedScoped()), l => l == leaked);
    }

    [Fact]
    public void Region_empty_global_event()
    {
        var def = AtAdmin("g", Scope.Global);
        Assert.All(RegionLinesOver(def), l => Assert.DoesNotContain("12345", l));
        Assert.Equal("-", ApiLines.Region(def));
        Assert.Equal("global: 1 events", RegionLines.List(new DefinitionSet([def]))[^1]);
    }
}
