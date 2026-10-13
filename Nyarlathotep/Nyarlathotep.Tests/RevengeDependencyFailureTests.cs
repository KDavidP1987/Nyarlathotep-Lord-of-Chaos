using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D10 and D16: the revenge kill feed's and private lines' dependency failures, the two categories of
/// tools/preflight-checks.json › dependencySuites.revenge (heat-read, private-send). A throw in the shared kill read is
/// owned by the FactionKills streak (one line, never a second revenge line); a throwing heat update logs "revenge: kill
/// skipped" once per streak; either holds "revenge: kill read failing" while a Revenge definition is startable and until a
/// read succeeds; nothing the feed throws reaches the death pass, so SpawnTracker.Died, the V Blood path and the scoreboard
/// run for this and every later death; a throwing private send logs once per streak and stops nothing.</summary>
public class RevengeDependencyFailureTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 13, 20, 0);
    const string P1 = "p1";

    static string Trigger(string? scope = null) =>
        "{ \"type\": \"Revenge\", \"factions\": [\"Faction_Bandits\"], \"heat\": 3, \"windowSeconds\": 900, \"warnAt\": [1], " +
        $"\"vbloodHeat\": 10, \"playerCooldownMinutes\": 5{(scope is null ? "" : $", \"scope\": {scope}")} }}";

    const string FactionKills = "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }";

    static DefinitionSet Set(params string[] events)
    {
        var r = EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        Assert.All(r.Set.All, d => Assert.Null(d.DisabledReason));
        return r.Set;
    }

    static DefinitionSet Revenge(string? scope = null) => Set(Json.Event("rv", Trigger(scope), action: Json.RevengeAction));

    static DefinitionSet KillsOnly() => Set(Json.Event("fk", FactionKills, action: Json.ValidAction));

    static KillFacts Kill(float? x = null) => new(P1, null, false, false, false, "Faction_Bandits", x, x is null ? null : 0f);

    static void Died(PlayerTriggerFeed feed, DefinitionSet set, Func<bool, KillFacts> read, LogLines log, DateTime at,
        Func<float, float, string>? regionOf = null, Action<string, string>? tell = null) =>
        feed.Died(read, set, regionOf ?? new FakeRegions().RegionOf, _ => true, _ => false, at, log.Add, _ => { }, null, tell);

    static KillFacts Throws(bool _) => throw new InvalidOperationException("victim read: ledger gone");

    // ---- heat-read (D10)

    [Fact]
    public void HeatRead_fails_when_a_throw_skips_the_death_pass()
    {
        var set = Revenge();
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        var order = new List<string>();
        DeathPass.Run(new[] { "a", "b" }, d => d, d => order.Add($"died {d}"), (d, _) =>
        {
            order.Add($"vblood {d}");
            Died(feed, set, d == "a" ? Throws : _ => Kill(), log, T0);
            order.Add($"fed {d}");
        }, (d, _) => order.Add($"score {d}"));
        Assert.Equal(["died a", "vblood a", "fed a", "score a", "died b", "vblood b", "fed b", "score b"], order);
        Assert.Equal(1, feed.Heat.HeatOf("rv", P1));                          // the later death still counts
    }

    [Fact]
    public void HeatRead_fails_when_one_throwing_read_prints_more_than_one_line()
    {
        var set = Set(Json.Event("rv", Trigger(), action: Json.RevengeAction), Json.Event("fk", FactionKills, action: Json.ValidAction));
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Died(feed, set, Throws, log, T0);
        Assert.Equal(["faction kills: read failed: victim read: ledger gone"], log.Lines);
        Died(feed, set, Throws, log, T0.AddSeconds(1));                     // the same streak: no second line
        Assert.Single(log.Lines);
        Assert.Contains(PlayerTriggerFeed.HeatFailing, feed.Health);
        Assert.Contains(PlayerTriggerFeed.KillFailing, feed.Health);
    }

    [Fact]
    public void HeatRead_fails_when_a_throwing_heat_update_escapes_or_repeats()
    {
        var set = Revenge("[\"CursedForest\"]");
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Func<float, float, string> broken = (_, _) => throw new InvalidOperationException("region index gone");
        Died(feed, set, _ => Kill(10f), log, T0, broken);
        Died(feed, set, _ => Kill(10f), log, T0.AddSeconds(1), broken);
        Assert.Equal(["revenge: kill skipped: region index gone"], log.Lines);
        Assert.Equal([PlayerTriggerFeed.HeatFailing], feed.Health);
        Died(feed, set, _ => Kill(10f), log, T0.AddSeconds(2));               // a good read clears the entry
        Assert.Empty(feed.Health);
        Assert.Equal(1, feed.Heat.HeatOf("rv", P1));
        Died(feed, set, _ => Kill(10f), log, T0.AddSeconds(3), broken);      // a new streak logs again
        Assert.Equal(2, log.Lines.Count(l => l.StartsWith("revenge: kill skipped", StringComparison.Ordinal)));
    }

    [Fact]
    public void HeatRead_fails_when_the_entry_shows_without_a_revenge_definition()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Died(feed, KillsOnly(), Throws, log, T0);
        Assert.Equal([PlayerTriggerFeed.KillFailing], feed.Health);         // FactionKills' own entry, never the revenge one
    }

    [Fact]
    public void HeatRead_passes_good_read_clears_the_entry()
    {
        var set = Revenge();
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Died(feed, set, Throws, log, T0);
        Assert.Contains(PlayerTriggerFeed.HeatFailing, feed.Health);
        Died(feed, set, _ => Kill(), log, T0.AddSeconds(1));
        Assert.Empty(feed.Health);
    }

    [Fact]
    public void HeatRead_empty_reads_no_death_without_a_definition()
    {
        var feed = new PlayerTriggerFeed();
        var reads = 0;
        Died(feed, Set(Json.Event("raid")), _ => { reads++; return Kill(); }, new LogLines(), T0);
        Assert.Equal(0, reads);
        Assert.False(PlayerTriggerFeed.WantsKills(Set(Json.Event("raid")), _ => true));
        Assert.True(PlayerTriggerFeed.WantsKills(Revenge(), _ => true));
        Assert.False(PlayerTriggerFeed.WantsKills(Revenge(), t => t != TriggerType.Revenge));   // the hook down: nothing read
    }

    // ---- private-send (D10)

    [Fact]
    public void PrivateSend_fails_when_a_failed_send_stops_the_heat_or_repeats()
    {
        var set = Revenge();
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Action<string, string> broken = (_, _) => throw new InvalidOperationException("user gone");
        Died(feed, set, _ => Kill(), log, T0, tell: broken);                 // warning 1 of 1: the send throws
        Died(feed, set, _ => Kill(), log, T0.AddSeconds(1), tell: broken);
        Died(feed, set, _ => Kill(), log, T0.AddSeconds(2), tell: broken);   // due
        Assert.Equal(3, feed.Heat.HeatOf("rv", P1));
        Assert.True(feed.Heat.IsDue("rv", P1));
        Assert.Single(log.Lines, l => l == "revenge: private line failed: user gone");
        Assert.Contains("revenge rv due", log.Lines);
    }

    [Fact]
    public void PrivateSend_fails_when_a_failed_send_throws_into_the_start()
    {
        // EventRuntime.StartEvent sends the start line through this guarded Tell after the instance started (Services):
        // a throwing send must return normally and log once per streak
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        feed.Tell((_, _) => throw new InvalidOperationException("user gone"), P1, Messages.RevengeStart("Bandits"), log.Add);
        feed.Tell((_, _) => throw new InvalidOperationException("user gone"), P1, Messages.RevengeEnd("Bandits"), log.Add);
        Assert.Equal(["revenge: private line failed: user gone"], log.Lines);
    }

    [Fact]
    public void PrivateSend_passes_good_send_ends_the_streak()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        feed.Tell((_, _) => throw new InvalidOperationException("user gone"), P1, Messages.RevengeStart("Bandits"), log.Add);
        var sent = new List<(string, string)>();
        feed.Tell((p, l) => sent.Add((p, l)), P1, Messages.RevengeStart("Bandits"), log.Add);   // a good send ends the streak
        feed.Tell((_, _) => throw new InvalidOperationException("user gone"), P1, Messages.RevengeEnd("Bandits"), log.Add);
        Assert.Equal([(P1, Messages.RevengeStart("Bandits"))], sent);
        Assert.Equal(2, log.Lines.Count);                                    // the streak after the good send logs again
    }

    [Fact]
    public void PrivateSend_empty_no_tell_no_line()
    {
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Died(feed, Revenge(), _ => Kill(), log, T0);                           // warning 1 of 1 with no sender: logged only
        Assert.Equal(["revenge rv: warning 1 of 1"], log.Lines);
    }
}
