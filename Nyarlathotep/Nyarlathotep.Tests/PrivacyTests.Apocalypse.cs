using System.Reflection;
using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D19: a player's name appears only on the scoreboard; no log line, push, reply, health entry or
/// state.json field of this child carries the planted player's name, id or position; the record holds platform ids, counts
/// and map locations only (the resolved goal fixed at the start); state.json gains exactly `Apocalypse`.</summary>
public partial class PrivacyTests
{
    /// <summary>The planted player stands at (4321.7, 8765.3): the roamers' target, holding the march, killing and dying.</summary>
    static readonly string[] ApocalypseMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    static readonly DateTime ApoT0 = Zones.Utc(2026, 10, 31, 20, 0);

    /// <summary>Every line of this child's Logic with the planted player in play, the run, its record and its world.</summary>
    static List<string> ApocalypseLines(out ApocalypseRecord record, out ApocalypseRun run)
    {
        var def = Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 2), extra: "\"bosses\": [ " + Apo.BossJson() + " ]"));
        var set = Apo.Set(Apo.Event(action: Apo.Action(hordes: Apo.Hordes(count: 2), extra: "\"bosses\": [ " + Apo.BossJson() + " ]")));
        var active = Apo.Active(def, ApoT0);
        var w = new ApoWorld();
        run = Apo.Run(active);
        var lines = new List<string>();
        var player = new HuntCandidate((long)(PlantedId % 1000000), 4321.7f, 8765.3f, true, true, false, false);
        var natives = Enumerable.Range(0, 6).Select(i => new HotspotNative(new UnitFacts(Apo.Zombie, "Faction_Undead", false, false, false, false, false, null, () => "FarbaneWoods"), i, i));
        lines.Add(Hotspots.Pick("apo", def.Apocalypse!, () => natives).Line);
        lines.Add(Hotspots.Pick("apo", def.Apocalypse!, () => throw new InvalidOperationException("query broke")).Line);
        lines.AddRange(run.TopUp(w, ApoT0));
        var roam = new RoamTargets();
        lines.Add(roam.Seen("apo", 1, RoamRule.Target([player], (0, 0))?.Key)!);
        lines.Add(run.SpreadOn(w, new ScoreSides(null, new OurUnit("apo", 101, ApoT0)), (4321.7f, 8765.3f), ApoT0.AddSeconds(1))!);
        var rise = run.Rise(w, ApoT0.AddMinutes(1), out var roamers)!;
        lines.AddRange(rise.Log);
        lines.AddRange(roamers);
        w.SpawnAll(run, ApoT0.AddMinutes(1));
        MarchRule.Step(run.Fronts[1], 20, 2.0, 1, [(4321.7f, 8765.3f)], (_, _) => false, ApoT0.AddMinutes(2), out var held);
        lines.Add(held!);
        lines.Add(MarchRule.KeepUpLine(1, run.Fronts[1].Percent, MarchRule.KeepUp(run.Fronts[1], [(4321.7f, 8765.3f)])));
        lines.Add(run.Tally.Line("apo", w.Alive(run)));
        lines.Add(RollingLifetime.Line("apo", 3));
        foreach (var r in Enum.GetValues<ApocalypseResult>().Where(r => r != ApocalypseResult.None))
        {
            lines.Add(ApocalypseOutcome.Line("apo", r, 1, 2));
            lines.Add(Messages.ApocalypseOutcome(def.Name, r, def.Apocalypse!.Goal.Name));
        }
        lines.Add(Messages.ApocalypseRise(def.Name, rise.Queued, "Farbane Woods", def.Apocalypse!.Goal.Name));
        lines.Add(Messages.ApocalypseProgress(def.Name, 25, def.Apocalypse.Goal.Name));
        lines.Add(Messages.ApocalypseDefeat(def.Name, 1, 2));
        lines.Add(Messages.ApocalypseStatus(def.Name, 1, 2, 4, def.Apocalypse.Goal.Name, 6, 50));
        record = run.Record(w, [new ApocalypseScore(PlantedId.ToString(), 3, 1, 1)], ApoT0.AddMinutes(2));
        lines.AddRange(Enum.GetValues<ResumeVerdict>().Select(v => ResumeRule.NotResumedLine("apo", v)));
        lines.Add(ResumeRule.ResumedLine("apo", 1, 2, 9));
        lines.Add(ResumeRule.DrainWarning("apo", 2));
        lines.Add(ApocalypseRecord.UnreadableLine("not an object"));
        lines.AddRange(EventLines.Info(def, active, ApoT0.AddMinutes(2), apocalypse: run.View(w, (_, _) => "Farbane Woods")));
        lines.AddRange(EventLines.Info(def, null, ApoT0));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], set, new Dictionary<string, int>(), true, ApoT0));
        lines.AddRange(ApiLines.Definitions(set, new HashSet<string> { def.Id }));
        return lines;
    }

    [Fact]
    public void Apocalypse_fails_when_a_planted_name_reaches_a_line_or_the_record()
    {
        var lines = ApocalypseLines(out var record, out _);
        Assert.Contains("apocalypse apo roam: hotspot 1 on a player", lines);
        Assert.Contains("horde 1 held: no walkable ground ahead", lines);
        foreach (var line in lines)
            foreach (var mark in ApocalypseMarks) Assert.False(line.Contains(mark, StringComparison.OrdinalIgnoreCase), $"'{line}' carries {mark}");
        var json = Encoding.UTF8.GetString(new StateDocument { Apocalypse = record }.Serialize());
        Assert.DoesNotContain(PlantedName, json);
        Assert.DoesNotContain("4321", json);
        Assert.DoesNotContain("8765", json);
        Assert.Contains(PlantedId.ToString(), json);                            // the platform id, as design §9 D45 allows
        // the check sees a leak when one is there
        Assert.Contains(ApocalypseMarks, m => $"apocalypse apo roam: hotspot 1 on {PlantedName}".Contains(m, StringComparison.Ordinal));
    }

    [Fact]
    public void Apocalypse_fails_when_the_goal_follows_a_player_after_the_start()
    {
        // an Admin goal is the admin's position read once at the start, then a fixed map location
        var def = Apo.Def(Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }")));
        var origin = (X: 100f, Y: 5f, Z: 200f);
        var goal = Apocalypse.ResolveGoal(def.Apocalypse!.Goal, origin)!.Value;
        var run = new ApocalypseRun(Apo.Active(def, ApoT0, origin), Apo.TwoSpots, goal);
        var w = new ApoWorld();
        run.Rise(w, ApoT0.AddMinutes(1), out _);
        MarchRule.Step(run.Fronts[1], 20, 2.0, 10, [(4321.7f, 8765.3f)], (_, _) => true, ApoT0.AddMinutes(2), out _);   // the admin walks off
        var record = run.Record(w, [], ApoT0.AddMinutes(2));
        Assert.Equal((100f, 5f, 200f), (record.Goal!.X, record.Goal.Y, record.Goal.Z));
        Assert.Equal((100f, 200f), (run.Fronts[1].GoalX, run.Fronts[1].GoalZ));
        // nothing can move the goal after the start: no setter on the run, the front's goal or the view
        Assert.All(new[] { typeof(ApocalypseRun).GetProperty(nameof(ApocalypseRun.Goal))!, typeof(Front).GetProperty(nameof(Front.GoalX))!, typeof(Front).GetProperty(nameof(Front.GoalZ))! },
            p => Assert.Null(p.GetSetMethod(nonPublic: true)));
    }

    [Fact]
    public void Apocalypse_fails_when_state_gains_any_other_property()
    {
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        // the record's fields: ids, counts, times and map locations only
        Assert.Equal(["EndsUtc", "EventId", "Goal", "Hordes", "HordesRisen", "Hotspots", "Roamers", "Scores", "SpreadWindow", "StartedUtc", "Unreadable"],
            typeof(ApocalypseRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(["Deaths", "First", "Kills", "PlatformId"], typeof(RecordScore).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(["FrontX", "FrontZ", "Living", "LivingBosses", "Number"], typeof(RecordHorde).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Apocalypse_passes_names_only_on_the_scoreboard()
    {
        var a = Apo.Active(Apo.Def(), ApoT0);
        var b = new Scoreboard();
        b.Kill("apo", new Scorer(PlantedId.ToString(), PlantedName, false), false);
        var end = ScoreboardRule.End(EndPath.Victory, a, b);
        Assert.Contains(end.Chat, l => l.Contains(PlantedName, StringComparison.Ordinal));
        Assert.DoesNotContain(PlantedName, end.Log!);
    }

    [Fact]
    public void Apocalypse_empty_no_apocalypse_no_line()
    {
        var set = Apo.Set(Json.Event("raid"));
        Assert.Null(set.Find("raid")!.Apocalypse);
        Assert.DoesNotContain(EventLines.Info(set.Find("raid")!, null, ApoT0), l => l.Contains("hotspot", StringComparison.Ordinal));
        Assert.DoesNotContain("Apocalypse", Encoding.UTF8.GetString(new StateDocument().Serialize()));
    }
}
