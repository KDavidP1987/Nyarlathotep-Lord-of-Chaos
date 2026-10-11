using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D5 and D6 (S-6): the kill-to-death winner, chosen over every row, and its one chat line.</summary>
public partial class ScoreboardTests
{
    static void Give(Scoreboard b, string id, int kills, int deaths, string name = "", bool admin = false, bool includeAdmins = false)
    {
        for (var i = 0; i < kills; i++) b.Kill("ws", P(id, name, admin), includeAdmins);
        for (var i = 0; i < deaths; i++) b.Death("ws", P(id, name, admin), includeAdmins);
    }

    // ---- D5 Winner ----

    [Fact]
    public void Winner_fails_when_the_best_ratio_fourth_by_kills_is_not_chosen()
    {
        var b = new Scoreboard();
        Give(b, "1", 12, 6);                                                    // 2.00
        Give(b, "2", 10, 4);                                                    // 2.50
        Give(b, "3", 9, 9);                                                     // 1.00
        Give(b, "4", 8, 1);                                                     // 8.00, fourth by kills
        var s = b.Summary("ws");
        Assert.DoesNotContain(s.Top, r => r.Name == "Player4");
        Assert.Equal("Player4", s.Winner!.Name);
    }

    [Fact]
    public void Winner_fails_when_zero_deaths_divides_by_zero_or_outranks()
    {
        var b = new Scoreboard();
        Give(b, "1", 3, 0);                                                     // 3 ÷ max(1, 0) = 3.00
        Give(b, "2", 7, 2);                                                     // 3.50
        Assert.Equal("Player2", b.Summary("ws").Winner!.Name);
        Assert.Equal(3.0, Scoreboard.Ratio(new ScoreRow("x", 3, 0, 1)));
        var only = new Scoreboard();
        Give(only, "1", 1, 0);
        Assert.Equal(1.0, Scoreboard.Ratio(only.Summary("ws").Winner!));
    }

    [Fact]
    public void Winner_fails_when_a_tie_breaks_the_wrong_way()
    {
        // equal ratio: more kills wins
        Assert.Equal("B", Scoreboard.Winner([new ScoreRow("A", 2, 1, 1), new ScoreRow("B", 4, 2, 2)])!.Name);
        // equal ratio and kills: fewer deaths (0 and 1 both divide by 1)
        Assert.Equal("B", Scoreboard.Winner([new ScoreRow("A", 3, 1, 1), new ScoreRow("B", 3, 0, 2)])!.Name);
        // all equal: the earlier first credit
        Assert.Equal("A", Scoreboard.Winner([new ScoreRow("B", 3, 1, 2), new ScoreRow("A", 3, 1, 1)])!.Name);
        // exact ratios: 2/3 against 0.6666 never ties by rounding
        Assert.Equal("A", Scoreboard.Winner([new ScoreRow("A", 2, 3, 2), new ScoreRow("B", 6666, 10000, 1)])!.Name);
    }

    [Fact]
    public void Winner_fails_when_a_dropped_admin_or_a_player_without_a_kill_wins()
    {
        var b = new Scoreboard();
        Give(b, "a", 50, 0, "Admin", admin: true);                              // dropped while IncludeAdmins is false
        Give(b, "1", 1, 5);
        Give(b, "2", 0, 0);
        b.Death("ws", P("3"), false);                                             // a death alone: a participant, no kill
        var s = b.Summary("ws");
        Assert.Equal("Player1", s.Winner!.Name);
        var admins = new Scoreboard();
        Give(admins, "a", 5, 0, "Admin", admin: true, includeAdmins: true);     // with the key true it is a row like any
        Assert.Equal("Admin", admins.Summary("ws").Winner!.Name);
    }

    [Fact]
    public void Winner_fails_when_the_201st_player_wins()
    {
        var b = new Scoreboard();
        for (var i = 1; i <= Limits.ScoreboardPlayers; i++) b.Kill("ws", P(i.ToString()), false);
        Give(b, "late", 30, 0);                                                 // past the cap: the totals only
        var s = b.Summary("ws");
        Assert.Equal(Limits.ScoreboardPlayers + 30, s.Kills);
        Assert.NotEqual("Playerlate", s.Winner!.Name);
        Assert.Equal("Player1", s.Winner.Name);                                   // all 1.00: the earliest credit
    }

    [Fact]
    public void Winner_passes_the_winner_through_end()
    {
        var b = new Scoreboard();
        Give(b, "1", 4, 1);
        Assert.Equal(new ScoreRow("Player1", 4, 1, b.Summary("ws").Winner!.First), b.End("ws").Winner);
    }

    [Fact]
    public void Winner_empty_no_kill()
    {
        var b = new Scoreboard();
        b.Death("ws", P("1"), false);
        Assert.Null(b.Summary("ws").Winner);
        Assert.Null(new Scoreboard().Summary("ws").Winner);
        Assert.Null(Scoreboard.Winner([]));
    }

    // ---- D6 WinnerLines ----

    static ScoreSummary WithWinner(ScoreRow? winner, params ScoreRow[] top) =>
        new(top, top.Length, top.Sum(r => r.Kills), top.Sum(r => r.Deaths), winner);

    [Fact]
    public void WinnerLines_passes_the_line_after_the_wave_sets_lines()
    {
        var chaos = new ScoreRow("Chaos", 12, 4, 1);
        var lines = Messages.ScoreboardLines("Spider swarm", WithWinner(chaos, chaos), WinnerRule.KillDeath);
        Assert.Equal(["Spider swarm scoreboard: 1. Chaos 12 kills, 4 deaths", "1 players, 12 kills, 4 deaths",
                      "Spider swarm winner: Chaos, K/D 3.00 (12 kills, 4 deaths)"], lines);
    }

    [Fact]
    public void WinnerLines_fails_when_the_ratio_prints_a_comma_or_a_third_decimal()
    {
        var prior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var r = new ScoreRow("Chaos", 2, 3, 1);
            var line = Messages.ScoreboardLines("raid", WithWinner(r, r), WinnerRule.KillDeath)[^1];
            Assert.Equal("raid winner: Chaos, K/D 0.67 (2 kills, 3 deaths)", line);
            var r3 = new ScoreRow("Chaos", 3, 0, 1);
            Assert.Equal("event ws scoreboard: 1 players, 3 kills, 0 deaths, winner K/D 3.00",
                ScoreboardRule.LogLine("ws", WithWinner(r3, r3), WinnerRule.KillDeath));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void WinnerLines_fails_when_a_line_exceeds_480_bytes()
    {
        var name = new string('Ж', 20);
        var w = new ScoreRow(name, 99999, 99999, 1);
        var lines = Messages.ScoreboardLines(new string('É', 32), WithWinner(w, w, w, w), WinnerRule.KillDeath);   // a 64-byte event name
        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.True(Bytes(l) <= Wire.MaxBytes, $"{Bytes(l)} bytes"));
        Assert.Contains(name, lines[2]);
    }

    [Fact]
    public void WinnerLines_fails_when_a_name_keeps_markup()
    {
        var w = new ScoreRow("<color=red>Evil</color>\u0007", 1, 0, 1);
        var line = Messages.ScoreboardLines("<b>raid</b>\n", WithWinner(w, w), WinnerRule.KillDeath)[^1];
        Assert.DoesNotContain("<", line);
        Assert.DoesNotContain(">", line);
        Assert.DoesNotContain(line, c => char.IsControl(c));
        Assert.StartsWith("braid/b winner: color=redEvil/color, K/D 1.00", line);      // TextSink drops the brackets and the bell
    }

    [Fact]
    public void WinnerLines_fails_when_the_empty_case_prints_a_winner_line()
    {
        Assert.Equal(["Bandit raid scoreboard: no player scored"],
            Messages.ScoreboardLines("Bandit raid", new ScoreSummary([], 0, 0, 0), WinnerRule.KillDeath));
        var dead = new ScoreRow("Chaos", 0, 2, 1);
        Assert.Equal("Bandit raid winner: none", Messages.ScoreboardLines("Bandit raid", WithWinner(null, dead), WinnerRule.KillDeath)[^1]);
    }

    [Fact]
    public void WinnerLines_fails_when_a_keyless_events_lines_change()
    {
        var chaos = new ScoreRow("Chaos", 12, 1, 1);
        var s = WithWinner(chaos, chaos);
        Assert.Equal(["Bandit raid scoreboard: 1. Chaos 12 kills, 1 deaths", "1 players, 12 kills, 1 deaths"], Messages.ScoreboardLines("Bandit raid", s));
        Assert.Equal(Messages.ScoreboardLines("Bandit raid", s), Messages.ScoreboardLines("Bandit raid", s, WinnerRule.None));
        Assert.Equal("event ws scoreboard: 1 players, 12 kills, 1 deaths", ScoreboardRule.LogLine("ws", s));
    }

    [Fact]
    public void WinnerLines_empty_no_participant() =>
        Assert.Single(Messages.ScoreboardLines("raid", new ScoreSummary([], 0, 0, 0, null), WinnerRule.KillDeath));
}
