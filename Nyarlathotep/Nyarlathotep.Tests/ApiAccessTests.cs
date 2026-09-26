using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D7 (row cases): every status and definition row built for a player and for an admin.
/// Each row carries exactly the keys the contract lists for its tag, so a key added under any name fails; a player's
/// row carries units=-.
/// `api events` itself is admin-only through VCF (preflight commands check and -AuthSuite). The push cases hold every
/// push line and sub reply to its keys, and a push to subscribed, connected players only.</summary>
public class ApiAccessTests
{
    static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    // The contract's key sets (§3 status, §3 events, §4), in order.
    static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["event"] = ["id", "kind", "name", "state", "faction", "left", "wave", "units"],
        ["def"] = ["id", "name", "enabled", "trigger", "action", "duration", "state", "reason"],
        ["end"] = ["cmd", "count"],
    };

    static void AssertExactKeys(string row)
    {
        var tag = row[6..row.IndexOf(']')];
        Assert.True(Allowed.TryGetValue(tag, out var keys), $"unexpected tag {tag}");
        Assert.Equal(keys, Keys(row));
    }

    static EventDefinition Def(string id, Pillar pillar, TriggerType trigger, bool enabled, string? reason) =>
        new(id, $"Event {id}", enabled, pillar, new Trigger(trigger, [], [], DayPhase.Night, []), new Conditions(), 900,
            new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 5)], 3, 60, 25, new Location(LocationType.Point, -1520.5f, -460f), null),
            Announce.None, reason);

    static (List<ActiveEvent> Active, List<Cleanup> Cleanups, DefinitionSet Set, Dictionary<string, int> Units) World()
    {
        var defs = new List<EventDefinition>();
        foreach (var p in Enum.GetValues<Pillar>())
            foreach (var t in Enum.GetValues<TriggerType>())
                defs.Add(Def($"{p}-{t}".ToLowerInvariant(), p, t, enabled: t != TriggerType.GameTime,
                    reason: t == TriggerType.VBloodKilled ? "trigger.bosses: empty" : null));
        var set = new DefinitionSet(defs);
        var active = defs.Take(4)
            .Select(d => new ActiveEvent(new RunningInstance(d, Now.AddSeconds(-10), Now.AddSeconds(300)), "manual", (1f, 2f, 3f))).ToList();
        var cleanups = defs.Skip(4).Take(3).Select(d => new Cleanup(d.Id, Now, Now.AddSeconds(45))).ToList();
        var units = defs.ToDictionary(d => d.Id, _ => 12);
        return (active, cleanups, set, units);
    }

    static IEnumerable<string> Keys(string line) => line.Split(' ').Skip(1).Select(t => t[..t.IndexOf('=')]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_status_row_carries_a_position_or_a_player_and_units_follow_the_role(bool isAdmin)
    {
        var w = World();
        var rows = ApiLines.Status(w.Active, w.Cleanups, w.Set, w.Units, isAdmin, Now);
        Assert.Equal(8, rows.Count);
        foreach (var row in rows)
        {
            AssertExactKeys(row);
            Assert.DoesNotContain("1520", row);
            if (!row.StartsWith("[NYAR:event]")) continue;
            Assert.Equal(isAdmin ? "12" : "-", Regex.Match(row, " units=([^ ]+)").Groups[1].Value);
        }
    }

    /// <summary>D7 (push cases): a push line is the same for a player and an admin, so none may carry units, a
    /// coordinate, a radius or a player; each is exactly type, id, secs and, for the wave types, wave. The sub replies
    /// carry cmd and on, or the err keys.</summary>
    [Fact]
    public void Every_push_line_and_sub_reply_carries_exactly_its_contract_keys()
    {
        var log = new LogLines();
        var hub = new PushHub(new FakeUsers(), [60], log.Add);
        var w = World();
        hub.EventStarted(w.Active[0].Instance);
        hub.Wave(w.Active[0].Id, 2);
        hub.EventEnded(w.Active[1].Id);
        hub.Purged(300);
        hub.ConfigChanged();
        hub.Queue.Enqueue(PushLines.WaveWarn(w.Active[0].Id, 2, 60));   // after the purge, which drops waiting warnings
        Assert.Equal(6, hub.Queue.Count);
        foreach (var line in hub.Queue.Lines.Select(l => l.Text))
        {
            var withWave = line.Contains("type=wave", StringComparison.Ordinal);
            Assert.Equal(withWave ? ["type", "id", "secs", "wave"] : ["type", "id", "secs"], Keys(line));
            Assert.DoesNotContain("1520", line);
        }
        Assert.Equal(["cmd", "on"], Keys(hub.Subscribe(7)));
        Assert.Equal(["cmd", "on"], Keys(hub.Unsubscribe(7)));
        Assert.Equal(["cmd", "code", "arg"], Keys(Wire.Error("sub", WireError.BadArg, arg: "state")));
    }

    /// <summary>D7: Subscriptions.Deliver reaches only ids that are both subscribed and connected.</summary>
    [Fact]
    public void A_push_reaches_only_subscribed_connected_players()
    {
        var users = new FakeUsers();                              // online: 1, 2, 3
        var hub = new PushHub(users, [60], _ => { });
        hub.Subscribe(1);
        hub.Subscribe(9);                                         // subscribed, not connected
        hub.ConfigChanged();
        hub.Tick(Now, [], waveWarnings: false);
        Assert.Equal(new[] { 1UL }, users.Sent.Select(s => s.Id));
    }

    [Fact]
    public void No_definition_row_carries_a_position_radius_or_unit_list()
    {
        var w = World();
        var rows = ApiLines.Definitions(w.Set, new HashSet<string>(w.Active.Select(a => a.Id)));
        Assert.Equal(w.Set.All.Count, rows.Count);
        foreach (var row in rows)
        {
            AssertExactKeys(row);
            Assert.DoesNotContain("1520", row);
            Assert.DoesNotContain("CHAR_", row);
        }
    }
}
