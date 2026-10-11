using System.Reflection;
using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D8: the winner's name appears in the winner chat line and nowhere else; the arena line carries counts
/// and a distance, never a name or a player's position; state.json gains nothing.</summary>
public partial class PrivacyTests
{
    static readonly (float X, float Z) PlayerAt = (4321.7f, -8765.3f);

    static EventDefinition SwarmDefinition(WinnerRule winner = WinnerRule.KillDeath)
    {
        var baseWave = new[] { new UnitEntry("CHAR_Bandit_Thug", 3) };
        var growth = new Growth(1, 2, true);
        return PlantedDefinition("sw") with
        {
            Action = PlantedDefinition().Action! with
            {
                Units = baseWave, Waves = 3, Radius = 8, Scoreboard = true, Winner = winner, Arena = new Arena(30), Growth = growth,
                WaveList = Growth.Generate(baseWave, new SpawnModifiers(Level: 30), 3, 60, growth),
            },
        };
    }

    /// <summary>Every line of this child except the scoreboard's chat lines, with the planted player credited and an arena
    /// unit chasing them.</summary>
    static List<string> SwarmLines(out ScoreboardEnd shown, WinnerRule winner = WinnerRule.KillDeath)
    {
        var d = SwarmDefinition(winner);
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null);
        var board = new Scoreboard();
        board.Kill("sw", PlantedScorer, includeAdmins: false);
        board.Kill("sw", PlantedScorer, includeAdmins: false);
        var lines = new List<string>();
        lines.AddRange(EventLines.Info(d, active, Now));
        lines.AddRange(EventLines.Info(d, null, Now));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], new DefinitionSet([d]), new Dictionary<string, int>(), true, Now));
        lines.AddRange(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { d.Id }));
        lines.Add(ScoreboardRule.VictoryLine(active));
        var chaser = new ArenaUnit(1, "sw", Now.AddSeconds(5), PlayerAt.X, 0f, PlayerAt.Z, PlayerAt);
        lines.Add(Arena.Check(active, [chaser], (_, _) => 0f, () => 0.5)!.LogLine!);
        shown = ScoreboardRule.End(EndPath.Victory, active, board);
        lines.Add(shown.Log!);
        return lines;
    }

    static readonly string[] SwarmMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    [Fact]
    public void Swarm_passes_the_winner_name_only_in_its_chat_line()
    {
        var lines = SwarmLines(out var shown);
        Assert.Contains("event sw scoreboard: 1 players, 2 kills, 0 deaths, winner K/D 2.00", lines);
        Assert.Contains(lines, l => l.StartsWith("arena sw: returned 1, farthest ", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in SwarmMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        Assert.Equal($"Bandit raid winner: {PlantedName}, K/D 2.00 (2 kills, 0 deaths)", shown.Chat[^1]);
        Assert.All(shown.Chat, l => Assert.DoesNotContain(PlantedId.ToString(), l, StringComparison.Ordinal));
    }

    [Fact]
    public void Swarm_fails_when_a_name_reaches_a_line()
    {
        // code review F3: the planted marks swept over every line, not one made-up leak
        foreach (var line in SwarmLines(out _))
            foreach (var mark in SwarmMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        var log = ScoreboardRule.LogLine("sw", new ScoreSummary([], 1, 1, 0, new ScoreRow(PlantedName, 1, 0, 1)), WinnerRule.KillDeath);
        Assert.DoesNotContain(PlantedName, log, StringComparison.Ordinal);
    }

    /// <summary>The members of the Logic assembly that take a scoreboard type stay wave-sets' four: the winner adds no
    /// builder, and the arena rule takes units and positions, never a player.</summary>
    [Fact]
    public void Swarm_fails_when_another_builder_takes_a_player_name()
    {
        var scoreTypes = new[] { typeof(Scorer), typeof(ScoreRow), typeof(ScoreSummary), typeof(Scoreboard) };
        var takers = typeof(Messages).Assembly.GetTypes()
            .Where(t => t.Namespace == "Nyarlathotep.Logic" && !scoreTypes.Contains(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetParameters().Any(p => scoreTypes.Contains(p.ParameterType)))
                .Select(m => $"{t.Name}.{m.Name}"))
            .Distinct().OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(["Messages.ScoreboardLines", "ScoreRule.Credit", "ScoreboardRule.End", "ScoreboardRule.LogLine"], takers);
        var arenaStrings = typeof(Arena).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetParameters()).Where(p => p.ParameterType == typeof(string)).Select(p => p.Name);
        Assert.Equal(["eventId"], arenaStrings.Distinct());
    }

    [Fact]
    public void Swarm_fails_when_state_gains_a_property()
    {
        Assert.Equal(["DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var text = Encoding.UTF8.GetString(new StateDocument().Serialize());
        Assert.All(SwarmMarks, m => Assert.DoesNotContain(m, text, StringComparison.Ordinal));
    }

    [Fact]
    public void Swarm_empty_no_winner_key()
    {
        SwarmLines(out var shown, WinnerRule.None);
        Assert.Equal(2, shown.Chat.Count);
        Assert.DoesNotContain(shown.Chat, l => l.Contains(" winner: ", StringComparison.Ordinal));
    }
}
