using System.Reflection;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D16: no message, log line or api row names or locates the picked player. The pick takes no
/// identity and returns only a centre; every line of an AroundPlayer wave says "around a player".</summary>
public partial class PrivacyTests
{
    static readonly (float X, float Y, float Z) PickedAt = (4321.7f, 55.5f, -8765.3f);

    static readonly string[] PlayerMarks = [PlantedName, PlantedId.ToString(), "4321", "8765", "55.5"];

    static EventDefinition AroundPlayerDefinition() => PlantedDefinition("hunt") with
    {
        Action = PlantedDefinition().Action! with
        {
            Location = new Location(LocationType.AroundPlayer, 0, 0, null, 20, 40),
            Behaviour = new Behaviour(BehaviourType.Hunt, 40),
            Radius = 10,
        },
    };

    /// <summary>Every line an AroundPlayer wave of the picked player can produce: the planners' skip and roll lines, the
    /// event's info, its api row and pushes, and its announcement.</summary>
    static List<string> PickLines(EventDefinition def, PickResult pick)
    {
        var active = new ActiveEvent(new RunningInstance(def, Now, Now.AddMinutes(10)), "schedule", null);
        var lines = new List<string>
        {
            WaveLines.ZeroRolled(1, def.Id), WaveLines.NoEligiblePlayer(1, def.Id), WaveLines.CentreClaimed(1, def.Id),
            WaveLines.TerritoryUnknown(1, def.Id), WaveLines.PlayerQueryFailed(1, def.Id), WaveLines.AroundAPlayer,
            PushLines.EventStart(active.Instance).Text, PushLines.Wave(def.Id, 1).Text,
            Messages.Render("The {event} hunts wave {wave} of {waves}.", MessageContext.For(def, 5, 1)),
        };
        var facts = new WaveFacts(1, def.Id, false, Location: LocationType.AroundPlayer, Pick: pick.Outcome);
        if (WaveGate.Decide(facts, () => ["CHAR_Bandit_Thug"], 20, 0, 150).Line is { } line) lines.Add(line);
        lines.AddRange(EventLines.Info(def, active, Now));
        lines.AddRange(ApiLines.Definitions(new DefinitionSet([def]), new HashSet<string> { def.Id }));
        return lines;
    }

    static PickResult PickPlanted() =>
        PlayerPick.Choose([new PickCandidate(PickedAt.X, PickedAt.Y, PickedAt.Z, true, true, false)], new SystemRandom(new Random(3)), 20, 40,
            (_, _) => false, null);

    [Fact]
    public void PlayerPick_passes_no_line_names_or_locates_the_player()
    {
        var pick = PickPlanted();
        Assert.Equal(PickOutcome.Picked, pick.Outcome);
        var lines = PickLines(AroundPlayerDefinition(), pick);
        Assert.Contains(lines, l => l.Contains("around a player 20-40 m", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in PlayerMarks) Assert.DoesNotContain(mark, line, StringComparison.OrdinalIgnoreCase);

        // the pick takes no identity and returns none; the line builders take no position
        Assert.Equal(["Alive", "InPvpCombat", "Online", "X", "Y", "Z"], typeof(PickCandidate).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(["Centre", "Error", "Outcome"], typeof(PickResult).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        foreach (var m in typeof(WaveLines).GetMethods(BindingFlags.Public | BindingFlags.Static))
            Assert.Equal([typeof(int), typeof(string)], m.GetParameters().Select(p => p.ParameterType));
        // the Hunt line's tally holds counts only (A66, Review 32 F5)
        Assert.All(typeof(HuntTally).GetProperties(), p => Assert.Equal(typeof(int), p.PropertyType));
    }

    [Fact]
    public void PlayerPick_fails_when_a_line_names_the_player()
    {
        var leaked = $"wave 1 of hunt: around {PlantedName} at {PickedAt.X} {PickedAt.Z}";
        Assert.Contains(PlayerMarks, m => leaked.Contains(m, StringComparison.Ordinal));
        Assert.DoesNotContain(leaked, PickLines(AroundPlayerDefinition(), PickPlanted()));
        Assert.Equal("around a player", WaveLines.AroundAPlayer);
    }

    [Fact]
    public void PlayerPick_empty_no_players()
    {
        var none = PlayerPick.Choose([], new SystemRandom(new Random(3)), 20, 40, (_, _) => false, null);
        var lines = PickLines(AroundPlayerDefinition(), none);
        Assert.Contains("wave 1 of hunt skipped: no eligible player", lines);
        Assert.All(lines, l => Assert.All(PlayerMarks, m => Assert.DoesNotContain(m, l, StringComparison.OrdinalIgnoreCase)));
    }
}
