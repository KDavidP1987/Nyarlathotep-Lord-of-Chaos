using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D10: the apocalypse's scoreboard by wave-sets D8's rules (roamers, horde units and bosses are its
/// units), IncludeAdmins, the lines at won, lost, survived and stop only, and the counts carried across a restart.</summary>
public partial class ScoreboardTests
{
    static readonly DateTime ApoStart = new(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc);

    static ActiveEvent ApoRun(string? action = null) => Apo.Active(Apo.Def(action), ApoStart);

    static Func<string, ActiveEvent?> Running(ActiveEvent a) => id => id == a.Id ? a : null;

    static OurUnit Roamer(int hotspot = 1) => new("apo", Apocalypse.RoamerWave(hotspot), ApoStart.AddSeconds(5));

    [Fact]
    public void Apocalypse_fails_when_a_roamer_horde_or_boss_kill_is_not_credited()
    {
        var a = ApoRun();
        var b = new Scoreboard();
        var player = new ScorePlayers(P("1"), null, false);
        foreach (var unit in new[] { Roamer(), Roamer(2), new OurUnit("apo", 2, ApoStart.AddMinutes(6)), new OurUnit("apo", Apocalypse.BossWave(3, 1), ApoStart.AddMinutes(11)) })
        {
            Assert.True(ScoreRule.Relevant(new ScoreSides(unit, null), Running(a)));
            ScoreRule.Credit(new ScoreSides(unit, null), player, Running(a), b, false);
        }
        ScoreRule.Credit(new ScoreSides(null, Roamer()), new ScorePlayers(null, P("2"), false), Running(a), b, false);   // a player dies to a roamer
        var s = b.Summary("apo");
        Assert.Equal((2, 4, 1), (s.Players, s.Kills, s.Deaths));
        Assert.True(Scoreboard.Counts(a));                                      // an absent `scoreboard` key is true
        Assert.False(Scoreboard.Counts(ApoRun(Apo.Action(extra: "\"scoreboard\": false"))));
    }

    [Fact]
    public void Apocalypse_fails_when_a_native_or_an_earlier_instance_counts()
    {
        var a = ApoRun();
        var b = new Scoreboard();
        var player = new ScorePlayers(P("1"), P("1"), false);
        Assert.False(ScoreRule.Relevant(new ScoreSides(null, null), Running(a)));                                       // a native
        ScoreRule.Credit(new ScoreSides(null, null), player, Running(a), b, false);
        ScoreRule.Credit(new ScoreSides(new OurUnit("apo", 101, ApoStart.AddSeconds(-1)), null), player, Running(a), b, false);   // the last run's
        ScoreRule.Credit(new ScoreSides(new OurUnit("raid", 1, ApoStart), null), player, Running(a), b, false);         // another event's
        Assert.Equal(0, b.Events);
    }

    [Fact]
    public void Apocalypse_fails_when_an_admin_counts_with_the_key_false()
    {
        var a = ApoRun();
        var b = new Scoreboard();
        ScoreRule.Credit(new ScoreSides(Roamer(), null), new ScorePlayers(P("9", "Admin", admin: true), null, false), Running(a), b, false);
        Assert.Equal(0, b.Events);
        ScoreRule.Credit(new ScoreSides(Roamer(), null), new ScorePlayers(P("9", "Admin", admin: true), null, false), Running(a), b, true);
        Assert.Equal(1, b.Summary("apo").Kills);
    }

    [Fact]
    public void Apocalypse_fails_when_a_silent_path_shows_the_lines()
    {
        var a = ApoRun();
        foreach (var path in Enum.GetValues<EndPath>())
        {
            var b = new Scoreboard();
            b.Kill("apo", P("1"), false);
            var end = ScoreboardRule.End(path, a, b);
            var shows = path is EndPath.Natural or EndPath.Victory or EndPath.Defeat or EndPath.Stop;
            Assert.Equal(shows, end.Chat.Count > 0);
            Assert.Equal(0, b.Events);                                          // the rows end on every path
        }
    }

    [Fact]
    public void Apocalypse_fails_when_restored_counts_differ_from_those_saved()
    {
        var b = new Scoreboard();
        b.Kill("apo", P("1", "Chaos"), false);
        b.Kill("apo", P("2", "Nyx"), false);
        b.Death("apo", P("1", "Chaos"), false);
        b.Kill("apo", P("2", "Nyx"), false);
        var saved = b.Export("apo");
        Assert.Equal([new ApocalypseScore("1", 1, 1, saved[0].First), new ApocalypseScore("2", 2, 0, saved[1].First)], saved);
        var after = new Scoreboard();
        after.Restore("apo", saved);
        Assert.Equal(saved, after.Export("apo"));
        Assert.Equal((2, 3, 1), (after.Summary("apo").Players, after.Summary("apo").Kills, after.Summary("apo").Deaths));
        after.Names("apo", id => id == "1" ? "Chaos" : null);
        Assert.Equal([Scoreboard.UnknownName, "Chaos"], after.Summary("apo").Top.Select(r => r.Name));
        // a credit after the resume adds to the row and numbers after every restored one
        after.Kill("apo", P("3", "Late"), false);
        Assert.True(after.Export("apo")[^1].First > saved.Max(s => s.First));
    }

    [Fact]
    public void Apocalypse_passes_kills_and_deaths_ranked()
    {
        var a = ApoRun();
        var b = new Scoreboard();
        for (var i = 0; i < 3; i++) ScoreRule.Credit(new ScoreSides(Roamer(), null), new ScorePlayers(P("1"), null, false), Running(a), b, false);
        ScoreRule.Credit(new ScoreSides(Roamer(), null), new ScorePlayers(null, null, false, Owner: P("2")), Running(a), b, false);   // a familiar's kill
        Assert.Equal(["Player1", "Player2"], b.Summary("apo").Top.Select(r => r.Name));
    }

    [Fact]
    public void Apocalypse_empty_no_credit_no_participant()
    {
        var b = new Scoreboard();
        Assert.Empty(b.Export("apo"));
        Assert.Equal(0, b.Summary("apo").Players);
        b.Restore("apo", []);
        Assert.Empty(b.Export("apo"));
    }
}
