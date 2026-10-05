using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D8, D9 and D10 (design §9 D38, D39): what the scoreboard counts, admins left out, and its chat
/// lines. The kill and death sides resolve their player before they credit (KillReader's rule, D13); these cases give the
/// resolved player.</summary>
public class ScoreboardTests
{
    static Scorer P(string id, string name = "", bool admin = false) => new(id, name == "" ? "Player" + id : name, admin);

    // ---- D8 Counting ----

    [Fact]
    public void Counting_passes_kills_and_deaths_per_event()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("1"), false);
        b.Kill("ws", P("1"), false);
        b.Death("ws", P("1"), false);
        b.Kill("ws", P("2"), false);
        b.Kill("other", P("1"), false);                                          // another event's unit
        var s = b.Summary("ws");
        Assert.Equal((2, 3, 1), (s.Players, s.Kills, s.Deaths));
        Assert.Equal([("Player1", 2, 1), ("Player2", 1, 0)], s.Top.Select(r => (r.Name, r.Kills, r.Deaths)));
        Assert.Equal(1, b.Summary("other").Kills);
    }

    [Fact]
    public void Counting_passes_ties_by_fewer_deaths_then_the_earlier_credit()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("late"), false);                                          // first credit: late, then early, then clean
        b.Death("ws", P("late"), false);
        b.Kill("ws", P("early"), false);
        b.Death("ws", P("early"), false);
        b.Kill("ws", P("clean"), false);
        b.Death("ws", P("deadonly"), false);
        Assert.Equal(["Playerclean", "Playerlate", "Playerearly"], b.Summary("ws").Top.Select(r => r.Name));
        Assert.Equal(4, b.Summary("ws", top: 10).Players);                        // a death alone makes a participant
        Assert.Equal("Playerdeadonly", b.Summary("ws", top: 10).Top[^1].Name);
    }

    [Fact]
    public void Counting_passes_the_name_at_the_latest_credit()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("1", "Old"), false);
        b.Kill("ws", P("1", "Renamed"), false);
        Assert.Equal(("Renamed", 2), (b.Summary("ws").Top[0].Name, b.Summary("ws").Top[0].Kills));
    }

    static readonly DateTime T0 = new(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>ws (scoreboard on) and plain (off), both running since T0.</summary>
    static EventEngine Running()
    {
        static string Ev(string id, bool on) => Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": " +
            "[ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 2 } ] } ], \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, " +
            "\"z\": 0 }, \"scoreboard\": " + (on ? "true" : "false") + " }");
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Ev("ws", true), Ev("plain", false)), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        foreach (var id in new[] { "ws", "plain" })
            Assert.Null(engine.Start(id, "manual", T0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
        return engine;
    }

    static readonly OurUnit WsUnit = new("ws", 1, T0.AddSeconds(5));

    static ScoreSummary Credit(ScoreSides sides, ScorePlayers players)
    {
        var engine = Running();
        var b = new Scoreboard();
        ScoreRule.Credit(sides, players, engine.Find, b, includeAdmins: false);
        return b.Summary("ws");
    }

    [Fact]
    public void Counting_fails_when_a_native_or_another_events_unit_credits()
    {
        Assert.Equal(0, Credit(new ScoreSides(null, null), new ScorePlayers(P("1"), null, false)).Kills);                   // a native unit
        Assert.Equal(0, Credit(new ScoreSides(WsUnit with { EventId = "plain" }, null), new ScorePlayers(P("1"), null, false)).Kills);
        Assert.Equal(0, Credit(new ScoreSides(WsUnit with { EventId = "gone" }, null), new ScorePlayers(P("1"), null, false)).Kills);
        Assert.Equal(0, Credit(new ScoreSides(WsUnit, null), new ScorePlayers(null, null, false)).Kills);                   // an NPC's kill
        Assert.Equal(0, Credit(new ScoreSides(WsUnit, WsUnit), new ScorePlayers(null, null, true)).Kills);                  // killed itself
        Assert.Equal(0, Credit(new ScoreSides(null, null), new ScorePlayers(null, P("1"), false)).Deaths);                  // a native killer
        Assert.Equal(0, Credit(new ScoreSides(null, WsUnit with { EventId = "plain" }), new ScorePlayers(null, P("1"), false)).Deaths);
    }

    [Fact]
    public void Counting_fails_when_a_familiar_or_summon_kill_is_not_credited()
    {
        var familiar = Credit(new ScoreSides(WsUnit, null), new ScorePlayers(null, null, false, Owner: P("1")));
        Assert.Equal(("Player1", 1), (familiar.Top.Single().Name, familiar.Kills));
        var summon = Credit(new ScoreSides(WsUnit, null), new ScorePlayers(null, null, false, Followed: P("2")));
        Assert.Equal(("Player2", 1), (summon.Top.Single().Name, summon.Kills));
        var own = Credit(new ScoreSides(WsUnit, null), new ScorePlayers(P("3"), null, false, Owner: P("4")));
        Assert.Equal("Player3", own.Top.Single().Name);                                                                     // the killer first
    }

    [Fact]
    public void Counting_fails_when_a_death_to_a_projectile_or_summon_is_not_credited()
    {
        var bolt = Credit(new ScoreSides(null, null, WsUnit), new ScorePlayers(null, P("1"), false));                       // the owner is ours
        Assert.Equal(("Player1", 1), (bolt.Top.Single().Name, bolt.Deaths));
        Assert.Equal(1, Credit(new ScoreSides(null, WsUnit), new ScorePlayers(null, P("1"), false)).Deaths);              // the unit itself
    }

    [Fact]
    public void Counting_fails_when_an_earlier_instance_unit_credits()
    {
        var old = WsUnit with { SpawnedUtc = T0.AddSeconds(-30) };                                                           // A3
        Assert.Equal(0, Credit(new ScoreSides(old, null), new ScorePlayers(P("1"), null, false)).Kills);
        Assert.Equal(0, Credit(new ScoreSides(null, old), new ScorePlayers(null, P("1"), false)).Deaths);
        Assert.False(ScoreRule.Relevant(new ScoreSides(old, old), Running().Find));
    }

    [Fact]
    public void Counting_fails_when_the_201st_player_gets_a_row()
    {
        var b = new Scoreboard();
        for (var i = 1; i <= Limits.ScoreboardPlayers + 1; i++) b.Kill("ws", P(i.ToString()), false);
        b.Kill("ws", P("1"), false);                                              // a counted player still gains
        var s = b.Summary("ws", top: 300);
        Assert.Equal(Limits.ScoreboardPlayers, s.Players);
        Assert.Equal(Limits.ScoreboardPlayers + 2, s.Kills);                      // the 201st adds to the totals only
        Assert.DoesNotContain(s.Top, r => r.Name == "Player201");
        Assert.Equal(2, s.Top.First(r => r.Name == "Player1").Kills);
    }

    [Fact]
    public void Counting_fails_when_rows_outlive_the_event()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("1"), false);
        b.Kill("other", P("1"), false);
        Assert.Equal(1, b.End("ws").Kills);
        Assert.Equal(0, b.Summary("ws").Players);
        b.Keep(["live"]);                                                         // "other" is no running instance
        Assert.Equal(0, b.Events);
        b.Kill("ws", P("1"), false);
        b.Clear();
        Assert.Equal(0, b.Events);
    }

    [Fact]
    public void Counting_empty_no_credit()
    {
        var s = new Scoreboard().Summary("ws");
        Assert.Equal((0, 0, 0), (s.Players, s.Kills, s.Deaths));
        Assert.Empty(s.Top);
    }

    // ---- D9 Admins ----

    [Fact]
    public void Admins_fails_when_an_admin_counts_with_the_key_false()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("a", "Admin", admin: true), includeAdmins: false);
        b.Death("ws", P("a", "Admin", admin: true), includeAdmins: false);
        b.Kill("ws", P("1"), includeAdmins: false);
        var s = b.Summary("ws");
        Assert.Equal((1, 1, 0), (s.Players, s.Kills, s.Deaths));                  // neither a row nor the totals
        Assert.DoesNotContain(s.Top, r => r.Name == "Admin");
    }

    [Fact]
    public void Admins_passes_an_admin_with_the_key_true()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("a", "Admin", admin: true), includeAdmins: true);
        Assert.Equal(("Admin", 1), (b.Summary("ws").Top[0].Name, b.Summary("ws").Kills));
    }

    [Fact]
    public void Admins_passes_the_cfg_default_false()
    {
        var settings = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "Settings.cs.txt"));
        Assert.Matches(@"Bind\(\s*""Scoreboard""\s*,\s*""IncludeAdmins""\s*,\s*false\b", settings);
    }

    [Fact]
    public void Admins_empty_only_admins()
    {
        var b = new Scoreboard();
        b.Kill("ws", P("a", admin: true), includeAdmins: false);
        Assert.Equal(0, b.Events);
        Assert.Equal(["Bandit raid scoreboard: no player scored"], Messages.ScoreboardLines("Bandit raid", b.Summary("ws")));
    }

    // ---- D10 Lines ----

    static int Bytes(string s) => Encoding.UTF8.GetByteCount(s);

    [Fact]
    public void Lines_passes_the_top_three_and_the_totals()
    {
        var s = new ScoreSummary([new("Chaos", 12, 1, 1), new("Mira", 7, 0, 2), new("Lux", 1, 3, 3)], 5, 25, 6);
        Assert.Equal(
            ["Bandit raid scoreboard: 1. Chaos 12 kills, 1 deaths; 2. Mira 7 kills, 0 deaths; 3. Lux 1 kills, 3 deaths",
             "5 players, 25 kills, 6 deaths"],
            Messages.ScoreboardLines("Bandit raid", s));
    }

    [Fact]
    public void Lines_fails_when_a_line_exceeds_480_bytes()
    {
        var name = new string('Ж', 20);                                           // 20 characters, 40 bytes each
        var s = new ScoreSummary([new(name, 99999, 99999, 1), new(name, 99999, 99999, 2), new(name, 99999, 99999, 3)], 200, 999999, 999999);
        var lines = Messages.ScoreboardLines(new string('É', 64), s);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.True(Bytes(l) <= Wire.MaxBytes, $"{Bytes(l)} bytes"));
    }

    [Fact]
    public void Lines_fails_when_a_fourth_name_appears()
    {
        var rows = Enumerable.Range(1, 5).Select(i => new ScoreRow("P" + i, 10 - i, 0, i)).ToList();
        var line = Messages.ScoreboardLines("raid", new ScoreSummary(rows, 5, 35, 0))[0];
        Assert.Contains("3. P3", line);
        Assert.DoesNotContain("P4", line);
    }

    [Fact]
    public void Lines_fails_when_a_name_keeps_markup()
    {
        var s = new ScoreSummary([new("<color=red>Evil</color>\u0007" + new string('x', 30), 1, 0, 1)], 1, 1, 0);
        var line = Messages.ScoreboardLines("<b>raid</b>\n", s)[0];
        Assert.DoesNotContain("<", line);
        Assert.DoesNotContain(">", line);
        Assert.DoesNotContain(line, c => char.IsControl(c));
        var shown = line[(line.IndexOf("1. ", StringComparison.Ordinal) + 3)..line.IndexOf(" 1 kills", StringComparison.Ordinal)];
        Assert.True(shown.Length <= 20, shown);
    }

    [Fact]
    public void Lines_empty_no_player_scored()
    {
        var lines = Messages.ScoreboardLines("Bandit raid", new ScoreSummary([], 0, 0, 0));
        Assert.Equal(["Bandit raid scoreboard: no player scored"], lines);
        Assert.DoesNotContain("1.", lines[0]);
    }
}
