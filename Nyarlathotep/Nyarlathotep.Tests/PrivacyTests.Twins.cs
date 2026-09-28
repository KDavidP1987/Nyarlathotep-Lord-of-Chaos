using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D15: no twin or admin read line carries the admin's id or name, and no coordinate other
/// than the `location here` value the admin set.</summary>
public partial class PrivacyTests
{
    const ulong PlantedId = 99887766554433;
    const string PlantedName = "Zebulonrex";

    /// <summary>Every twin and read, run by an admin with the planted id, name and position; the lines they answered.</summary>
    static List<string> TwinLines(Rig r, AdminCaller who)
    {
        var lines = new List<string>
        {
            r.Flows.ApiEvent(who, "start", "raid", "", "", ""), r.Flows.ApiEvent(who, "start", "raid", "", "", ""),
            r.Flows.ApiEvent(who, "stop", "raid", "", "", ""), r.Flows.ApiEvent(who, "disable", "raid", "", "", ""),
            r.Flows.ApiEvent(who, "enable", "raid", "", "", ""), r.Flows.ApiEvent(who, "set", "raid", "durationSeconds", "900", ""),
            r.Flows.ApiEvent(who, "reload", "", "", "", ""), r.Flows.ApiEvent(who, "new", "fresh", "spawns", "", ""),
            r.Flows.ApiEvent(who, "copy", "raid", "raid-2", "", ""), r.Flows.ApiEvent(who, "delete", "raid-2", "", "", ""),
            r.Flows.ApiEvent(who, "delete", "raid-2", "confirm", "", ""), r.Flows.ApiEvent(who, "launch", "", "", "", ""),
            r.Flows.ApiPurge(who, "", ""), r.Flows.ApiPurge(who, "confirm", ""),
        };
        lines.AddRange(r.Flows.ApiTemplate(who, "use", "bandit-ambush", "", "", ""));
        lines.AddRange(r.Flows.ApiTemplate(who, "info", "bandit-ambush", "", "", ""));
        lines.AddRange(r.Flows.ApiTemplates("", "", ""));
        lines.AddRange(r.Flows.ApiPillar(who, "boss", "off", ""));
        lines.AddRange(r.Flows.ApiPillar(who, "list", "", ""));
        lines.AddRange(r.Flows.ApiKillSwitch(""));
        return lines;
    }

    static (Rig Rig, AdminCaller Who) PlantedRig()
    {
        var r = TemplateRig();
        r.Ops.Templates = Templates(r.Lib);
        r.Ops.PurgeableUnits = 2;
        r.Position = (PlantedCoord, PlantedCoord, PlantedCoord);
        return (r, new AdminCaller(PlantedId, PlantedName, () => r.Position));
    }

    [Fact]
    public void Twins_passes_no_id_name_or_position()
    {
        var (r, who) = PlantedRig();
        var lines = TwinLines(r, who);
        Assert.True(lines.Count > 20);
        foreach (var line in lines)
        {
            Assert.DoesNotContain(PlantedId.ToString(), line);
            Assert.DoesNotContain(PlantedName, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("12345", line);
        }
    }

    [Fact]
    public void Twins_passes_location_here_value_only()
    {
        var (r, who) = PlantedRig();
        r.Position = (4321.5f, 7f, -4321.5f);                                            // within the ±10000 bound
        Assert.Equal("[NYAR:ok] cmd=event verb=set id=raid field=location value=4321.5,-4321.5",
            r.Flows.ApiEvent(who, "set", "raid", "location", "here", ""));
    }

    [Fact]
    public void Twins_fails_when_a_line_carries_the_id()
    {
        // the check itself: a line that did carry the planted id or name is caught
        var leaked = $"[NYAR:ok] cmd=event verb=start id={PlantedId}";
        Assert.Contains(PlantedId.ToString(), leaked);
        var (r, who) = PlantedRig();
        Assert.DoesNotContain(TwinLines(r, who), l => l == leaked);
        Assert.Contains(r.Lib.Log.Lines, l => l.Contains(PlantedId.ToString(), StringComparison.Ordinal));   // the admin log keeps its form (D15)
    }

    [Fact]
    public void Twins_empty_no_events()
    {
        var r = new Rig(Library.Of());
        var who = new AdminCaller(PlantedId, PlantedName, () => null);
        foreach (var line in new[] { r.Flows.ApiEvent(who, "reload", "", "", "", ""), r.Flows.ApiPurge(who, "", "") }
                     .Concat(r.Flows.ApiPillar(who, "list", "", "")).Concat(r.Flows.ApiKillSwitch("")))
        {
            Assert.DoesNotContain(PlantedId.ToString(), line);
            Assert.DoesNotContain(PlantedName, line);
        }
    }
}
