using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D7 (row cases): every status and definition row built for a player and for an admin.
/// A player's row never carries a unit count, and no row for anyone carries a coordinate, radius or player key.
/// `api events` itself is admin-only through VCF (preflight commands check and -AuthSuite).</summary>
public class ApiAccessTests
{
    static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    static readonly string[] Forbidden = ["x", "y", "z", "r", "radius", "pos", "player", "steam", "steamid", "owner", "clan"];

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
            foreach (var k in Keys(row)) Assert.DoesNotContain(k, Forbidden);
            Assert.DoesNotContain("1520", row);
            if (!row.StartsWith("[NYAR:event]")) continue;
            Assert.Equal(isAdmin ? "12" : "-", Regex.Match(row, " units=([^ ]+)").Groups[1].Value);
        }
    }

    [Fact]
    public void No_definition_row_carries_a_position_radius_or_unit_list()
    {
        var w = World();
        var rows = ApiLines.Definitions(w.Set, new HashSet<string>(w.Active.Select(a => a.Id)));
        Assert.Equal(w.Set.All.Count, rows.Count);
        foreach (var row in rows)
        {
            foreach (var k in Keys(row)) Assert.DoesNotContain(k, Forbidden);
            Assert.DoesNotContain("1520", row);
            Assert.DoesNotContain("CHAR_", row);
        }
    }
}
