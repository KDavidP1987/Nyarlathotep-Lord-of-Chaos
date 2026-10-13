using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D9: heat, due players, warnings and cooldowns stay in memory keyed by platform id; no log line, push,
/// wire line, reply or health entry of this child carries the planted player's id, name or position; state.json gains
/// nothing; the four revenge builders are one family, checked together.</summary>
public partial class PrivacyTests
{
    static readonly string[] RevengeMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    /// <summary>Five faction names of 30 characters each, as the alert's worst case.</summary>
    static readonly string FiveLongFactions = string.Join(", ", Enumerable.Range(0, 5).Select(i => new string((char)('A' + i), 30)));

    static DefinitionSet RevengeSet()
    {
        var r = EventValidator.Parse(Json.File(Json.Event("rv", Json.RevengeTrigger, action: Json.RevengeAction)), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.Set.Find("rv")!.DisabledReason);
        return r.Set;
    }

    /// <summary>Every line of this child with the planted player as the killer and the target: the warning, due, held, start
    /// and end log lines, the private lines' text, the alert, info and counts, the def row, status and the start push, a
    /// failing kill read's line and the health entries.</summary>
    static List<string> RevengeLines()
    {
        var set = RevengeSet();
        var d = set.Find("rv")!;
        var id = PlantedId.ToString();
        var lines = new List<string>();
        var feed = new PlayerTriggerFeed();
        var kill = new KillFacts(id, null, false, false, false, "Faction_Bandits", 4321.7f, 8765.3f, false, false);
        for (var i = 0; i < 6; i++)
            feed.Died(_ => kill, set, null, _ => true, _ => false, Now.AddSeconds(i), lines.Add, _ => { }, null, (_, l) => lines.Add(l));
        RevengeTarget[] at = [new(id, 4321.7f, 0f, 8765.3f, true, false)];
        feed.Scan(() => [], null, set, _ => false, Now.AddSeconds(10), lines.Add, _ => { }, null,
            new RevengeScan(() => at, (_, _) => true, _ => false, _ => RevengeStartResult.Started));            // held (territory)
        feed.Scan(() => [], null, set, _ => false, Now.AddSeconds(15), lines.Add, _ => { }, null,
            new RevengeScan(() => at, (_, _) => false, _ => false, _ => RevengeStartResult.Started));           // starts
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "Revenge", null, id);
        lines.AddRange(RevengeRule.StartLines(d, true, 0, () => "banner").Select(l => l.Text));
        lines.Add(RevengeRule.StartPrivate(active)!.Value.Line);
        lines.AddRange(Enum.GetValues<RevengeEndPath>().Select(p => RevengeRule.EndPrivate(active, p)?.Line).OfType<string>());
        lines.AddRange(Enum.GetValues<RevengeEndPath>().Select(p => RevengeRule.EndedLine("rv", p)));
        lines.AddRange(EventLines.Info(d, active, Now, null, (feed.Heat.RowsFor("rv"), feed.Heat.DueFor("rv"))));
        lines.AddRange(EventLines.Info(d, null, Now));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], set, new Dictionary<string, int>(), true, Now));
        lines.AddRange(ApiLines.Definitions(set, new HashSet<string> { d.Id }));
        feed.Died(_ => throw new InvalidOperationException("entity gone"), set, null, _ => true, _ => false, Now.AddSeconds(20), lines.Add, _ => { });
        lines.AddRange(feed.Health);
        return lines;
    }

    [Fact]
    public void Revenge_fails_when_the_planted_id_or_name_reaches_a_line()
    {
        var lines = RevengeLines();
        Assert.Contains("revenge rv: warning 2 of 2", lines);
        Assert.Contains("revenge rv: held (territory)", lines);
        Assert.Contains(PlayerTriggerFeed.HeatFailing, lines);
        foreach (var line in lines)
            foreach (var mark in RevengeMarks) Assert.False(line.Contains(mark, StringComparison.OrdinalIgnoreCase), $"'{line}' carries {mark}");
        // the check sees a leak when one is there
        var leaked = $"revenge rv: warning 1 of 2 for {PlantedId}";
        Assert.Contains(RevengeMarks, m => leaked.Contains(m, StringComparison.Ordinal));
    }

    [Fact]
    public void Revenge_fails_when_state_gains_a_property()
    {
        Assert.Equal(["DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        // the heat has no state.json form: it is a Logic object with no serialisable property
        Assert.DoesNotContain(typeof(RevengeHeat).GetProperties(), p => p.CanWrite);
    }

    [Fact]
    public void Revenge_fails_when_a_builder_line_exceeds_480_bytes_or_holds_angle()
    {
        var lines = Messages.RevengeAlertPool.Select(t => Messages.Render(t, new MessageContext("-", FiveLongFactions, 0, 0, 0, "-")))
            .Concat(Enumerable.Range(1, 3).Select(k => Messages.RevengeWarning(k, 3, FiveLongFactions)))
            .Append(Messages.RevengeStart(FiveLongFactions)).Append(Messages.RevengeEnd(FiveLongFactions)).ToList();
        Assert.Equal(7, lines.Count);
        foreach (var line in lines)
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes, line);
            Assert.DoesNotContain("<", line);
            Assert.DoesNotContain(PlantedName, line);
            Assert.Contains(new string('E', 30), line);
        }
        // a faction text with markup is cleaned, not passed through
        Assert.DoesNotContain("<", Messages.RevengeStart("<color=red>Bandits</color>"));
    }

    [Fact]
    public void Revenge_fails_when_another_builder_renders_a_revenge_line()
    {
        string[] four = [nameof(Messages.RevengeAlertPool), nameof(Messages.RevengeWarning), nameof(Messages.RevengeStart), nameof(Messages.RevengeEnd)];
        var revenge = Cases.Where(c => four.Contains(c.Key)).SelectMany(c => c.Value()).ToHashSet();
        Assert.Equal(7, revenge.Count);
        // the pools and Render pass the alert pool through as every other pool; no other builder renders a revenge line
        var poolTemplates = Messages.RevengeAlertPool.ToHashSet();
        foreach (var (name, run) in Cases.Where(c => !four.Contains(c.Key)))
            foreach (var line in run())
            {
                if (name is nameof(Messages.Pools) or nameof(Messages.Render) && (poolTemplates.Contains(line) || line.Contains("revenge on a vampire")
                    || line.Contains("to hunt a vampire down"))) continue;
                Assert.False(line.Contains("revenge", StringComparison.OrdinalIgnoreCase) || line.Contains("come for you", StringComparison.Ordinal)
                    || line.Contains("your killing", StringComparison.Ordinal), $"Messages.{name} rendered '{line}'");
            }
    }

    [Fact]
    public void Revenge_passes_four_builders_with_five_long_factions()
    {
        Assert.Equal("The " + FiveLongFactions + " come for you.", Messages.RevengeStart(FiveLongFactions));
        Assert.Equal("The " + FiveLongFactions + "' revenge is spent.", Messages.RevengeEnd(FiveLongFactions));
        Assert.Equal("The Bandits swear revenge on a vampire.", Messages.Render(Messages.RevengeAlertPool[0], new MessageContext("-", "Bandits", 0, 0, 0, "-")));
    }

    [Fact]
    public void Revenge_empty_no_revenge_definition()
    {
        var feed = new PlayerTriggerFeed();
        var log = new List<string>();
        var set = new DefinitionSet([PlantedDefinition()]);
        feed.Died(_ => new KillFacts(PlantedId.ToString(), null, false, false, false, "Faction_Bandits", 4321.7f, 8765.3f, false, false), set, null,
            _ => true, _ => false, Now, log.Add, _ => { });
        Assert.Empty(log);
        Assert.Equal(0, feed.Heat.Rows);
        Assert.Empty(feed.Health);
    }
}
