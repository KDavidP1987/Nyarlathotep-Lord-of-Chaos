using System.Reflection;
using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D12: a player's name appears in the scoreboard's chat lines and nowhere else; the platform id
/// stays in the Scoreboard's memory; state.json gains nothing.</summary>
public partial class PrivacyTests
{
    static readonly Scorer PlantedScorer = new(PlantedId.ToString(), PlantedName, false);

    static EventDefinition WaveSetDefinition() => PlantedDefinition("ws") with
    {
        Action = PlantedDefinition().Action! with
        {
            Units = [new UnitEntry("CHAR_Bandit_Thug", 2, 1.0, new SpawnModifiers(Level: 30))],
            Waves = 2, IntervalSeconds = 0, Scoreboard = true,
            WaveList = [new WaveSpec([new UnitEntry("CHAR_Bandit_Thug", 2, 1.0, new SpawnModifiers(Level: 30))]),
                        new WaveSpec([new UnitEntry("CHAR_Bandit_Thug", 2)], 120, true)],
        },
    };

    /// <summary>Every line of this child except the scoreboard's chat lines, with the planted player credited.</summary>
    static List<string> WaveSetLines(out ScoreboardEnd shown)
    {
        var d = WaveSetDefinition();
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null);
        var board = new Scoreboard();
        board.Kill("ws", PlantedScorer, includeAdmins: false);
        board.Death("ws", PlantedScorer, includeAdmins: false);
        var lines = new List<string>();
        lines.AddRange(EventLines.Info(d, active, Now));
        lines.AddRange(EventLines.Info(d, null, Now));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], new DefinitionSet([d]), new Dictionary<string, int>(), true, Now));
        lines.AddRange(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { d.Id }));
        lines.Add(ScoreboardRule.VictoryLine(active));
        shown = ScoreboardRule.End(EndPath.Victory, active, board);
        lines.Add(shown.Log!);
        return lines;
    }

    static readonly string[] ScoreMarks = [PlantedName, PlantedId.ToString()];

    [Fact]
    public void WaveSets_passes_the_name_only_on_the_scoreboard()
    {
        var lines = WaveSetLines(out var shown);
        Assert.Contains("event ws scoreboard: 1 players, 1 kills, 1 deaths", lines);
        foreach (var line in lines)
            foreach (var mark in ScoreMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        Assert.Contains(shown.Chat, l => l.Contains(PlantedName, StringComparison.Ordinal));          // the one place it shows
        Assert.All(shown.Chat, l => Assert.DoesNotContain(PlantedId.ToString(), l, StringComparison.Ordinal));
    }

    [Fact]
    public void WaveSets_fails_when_a_name_reaches_a_line()
    {
        var leaked = $"event ws scoreboard: 1. {PlantedName} ({PlantedId}) 1 kill";
        Assert.All(ScoreMarks, m => Assert.Contains(m, leaked, StringComparison.Ordinal));
        Assert.DoesNotContain(leaked, WaveSetLines(out _));
    }

    /// <summary>The members of the Logic assembly that take a scoreboard type: the scoreboard itself, its rule (counts
    /// only) and the one chat builder.</summary>
    [Fact]
    public void WaveSets_fails_when_another_builder_takes_a_player_name()
    {
        var scoreTypes = new[] { typeof(Scorer), typeof(ScoreRow), typeof(ScoreSummary), typeof(Scoreboard) };
        var takers = typeof(Messages).Assembly.GetTypes()
            .Where(t => t.Namespace == "Nyarlathotep.Logic" && !scoreTypes.Contains(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetParameters().Any(p => scoreTypes.Contains(p.ParameterType)))
                .Select(m => $"{t.Name}.{m.Name}"))
            .Distinct().OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(["Messages.ScoreboardLines", "ScoreRule.Credit", "ScoreboardRule.End", "ScoreboardRule.LogLine"], takers);
        var log = ScoreboardRule.LogLine("ws", new ScoreSummary([new ScoreRow(PlantedName, 1, 0, 1)], 1, 1, 0));
        Assert.DoesNotContain(PlantedName, log, StringComparison.Ordinal);
    }

    [Fact]
    public void WaveSets_fails_when_state_gains_a_property()
    {
        Assert.Equal(["DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(StateDocument).GetProperties(), p => p.PropertyType == typeof(Scoreboard));
        var text = Encoding.UTF8.GetString(new StateDocument().Serialize());
        Assert.All(ScoreMarks, m => Assert.DoesNotContain(m, text, StringComparison.Ordinal));
    }

    /// <summary>A scoreboard line in the announce queue (Services/Announcer.Scoreboard queues it Named): neither the
    /// full queue's drop line nor the verbose "announced" line (QueuedLine.LogText) shows its text.</summary>
    [Fact]
    public void WaveSets_fails_when_a_queued_scoreboard_line_reaches_the_log()
    {
        var log = new List<string>();
        var queue = new AnnounceQueue(log.Add);
        var line = $"ws scoreboard: 1. {PlantedName} 1 kills, 0 deaths";
        queue.Enqueue(new QueuedLine(line, LineKind.Info, "ws", Named: true), Now);
        for (var i = 0; i < AnnounceQueue.Capacity; i++) queue.Enqueue(new QueuedLine("filler " + i, LineKind.Info), Now);
        Assert.Contains(log, l => l.Contains("dropped the oldest informational line", StringComparison.Ordinal));
        Assert.All(log, l => Assert.DoesNotContain(PlantedName, l, StringComparison.Ordinal));
        Assert.DoesNotContain(PlantedName, new QueuedLine(line, LineKind.Info, Named: true).LogText, StringComparison.Ordinal);
        Assert.Equal("filler", new QueuedLine("filler", LineKind.Info).LogText);
        var announcer = File.ReadAllText(Path.Combine(RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Services", "Announcer.cs"));
        Assert.Contains("Named: true", announcer);
        Assert.DoesNotContain("{line.Text}", announcer);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void WaveSets_empty_no_scoreboard_key()
    {
        var d = WaveSetDefinition() with { Action = WaveSetDefinition().Action! with { Scoreboard = false } };
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null);
        var board = new Scoreboard();
        board.Kill("ws", PlantedScorer, false);
        Assert.Same(ScoreboardEnd.None, ScoreboardRule.End(EndPath.Natural, active, board));
    }
}
