using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D33 (A39, A42, A44): every end path leaves the event's ledger units (after the despawn drain),
/// hunt seeds and kept territory map empty, and leaves every other event's state as it was.</summary>
public class EndPathTests
{
    static readonly DateTime T0 = new(2026, 9, 29, 20, 0, 0, DateTimeKind.Utc);
    static readonly LedgerLimits Limits = new(150, 20, 10, 10);
    static readonly UnitLifetime Life = new(T0.AddMinutes(10), 900);
    const long Player = 7;

    static readonly HashSet<(int X, int Z)> RaidMap = [(640, 640)];
    static readonly HashSet<(int X, int Z)> OtherMap = [(700, 700)];

    /// <summary>Spawns <paramref name="count"/> units of <paramref name="id"/> as keys from <paramref name="firstKey"/>, each
    /// seeded on the shared player, and keeps the event's map.</summary>
    static void Spawn(WaveLifecycle life, string id, long firstKey, int count, HashSet<(int X, int Z)> map)
    {
        life.Ledger.Request("CHAR_Bandit_Thug", id, count, Life, UnitTuning.None, _ => (0, 0, 0));
        var key = firstKey;
        foreach (var order in life.Ledger.TakeSpawns())
        {
            Assert.True(life.Ledger.Confirm(order, key, T0));
            life.Seeds.Wrote(key, id, new AggroSeed(Player, 1f, 10f));
            key++;
        }
        life.Maps.Built(id, map);
    }

    static WaveLifecycle Running()
    {
        var life = new WaveLifecycle(new SpawnLedger(Limits), new HuntSeeds(), new TerritoryMaps());
        Spawn(life, "raid", 1, 3, RaidMap);
        Spawn(life, "other", 101, 2, OtherMap);
        return life;
    }

    /// <summary>The budgeted despawn: every queued unit leaves the game and the lifecycle hears of it.</summary>
    static void Drain(WaveLifecycle life)
    {
        while (life.Ledger.PendingDespawns > 0)
            foreach (var key in life.Ledger.TakeDespawns()) life.UnitGone(key);
    }

    static void Empty(WaveLifecycle life, string id)
    {
        Assert.Equal(0, life.TrackedFor(id));
        Assert.Equal(0, life.Seeds.CountFor(id));
        Assert.False(life.Maps.Holds(id));
        Assert.Null(life.Maps.ForHunt(id));
    }

    static void Untouched(WaveLifecycle life)
    {
        Assert.Equal(2, life.TrackedFor("other"));
        Assert.Equal(2, life.Seeds.CountFor("other"));
        Assert.Equal([Player], life.Seeds.SeededOn(101));
        Assert.True(life.Maps.Holds("other"));
        Assert.Same(OtherMap, life.Maps.ForHunt("other"));
    }

    [Fact]
    public void EndPaths_fails_when_natural_end_leaves_state()
    {
        var life = Running();
        Assert.Equal((3, 0), life.EventEnded("raid", T0.AddSeconds(1), cancelOrders: false));   // the despawn after GraceSeconds
        Assert.Equal(0, life.Seeds.CountFor("raid"));
        Assert.False(life.Maps.Holds("raid"));
        Drain(life);
        Empty(life, "raid");
        Untouched(life);
    }

    [Fact]
    public void EndPaths_fails_when_event_stop_leaves_state()
    {
        var life = Running();
        life.Ledger.Request("CHAR_Bandit_Thug", "raid", 4, Life, UnitTuning.None, _ => (0, 0, 0));   // a wave still waiting
        Assert.Equal((3, 4), life.EventEnded("raid", DateTime.MaxValue));
        Assert.False(life.Maps.Holds("raid"));
        Drain(life);
        Empty(life, "raid");
        Assert.Equal(0, life.Ledger.PendingSpawns);
        Untouched(life);
    }

    [Fact]
    public void EndPaths_fails_when_fault_cancel_leaves_state()
    {
        var life = Running();
        life.Maps.Failed("raid");
        Assert.True(life.Maps.AnyFailed);
        life.EventEnded("raid", DateTime.MaxValue);                             // the third fault cancels the event (Epic D25)
        Assert.False(life.Maps.Holds("raid"));
        Assert.False(life.Maps.AnyFailed);
        Drain(life);
        Empty(life, "raid");
        Untouched(life);
    }

    [Fact]
    public void EndPaths_fails_when_purge_leaves_state()
    {
        var life = Running();
        life.Maps.Failed("other");
        Assert.Equal((5, 0), life.Purged());
        Assert.Equal(0, life.Seeds.Count);
        Assert.Equal(0, life.Maps.Count);
        Assert.False(life.Maps.AnyFailed);
        Drain(life);
        Empty(life, "raid");
        Empty(life, "other");
        Assert.Equal(0, life.Ledger.Occupied);
    }

    [Fact]
    public void EndPaths_fails_when_restart_keeps_state()
    {
        var before = Running();
        var survivors = before.Ledger.Units.Select(u => u.Key).ToList();
        var life = WaveLifecycle.Restart(Limits);
        Assert.Equal(0, life.Seeds.Count);
        Assert.Equal(0, life.Maps.Count);
        Assert.False(life.Maps.Holds("raid"));
        Assert.False(life.Maps.Holds("other"));
        foreach (var key in survivors) Assert.True(life.Ledger.QueueDespawn(key));   // the boot marker sweep
        Assert.Equal(survivors.Count, life.Ledger.Occupied);
        Drain(life);
        Empty(life, "raid");
        Empty(life, "other");
        Assert.Equal(0, life.Ledger.Occupied);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("natural end")]
    [InlineData("fault cancel")]
    public void EndPaths_fails_when_stopping_one_touches_the_other(string path)
    {
        var life = Running();                                                   // two events hunting the same player
        if (path == "fault cancel") life.Maps.Failed("raid");
        var (map, holds) = (life.Maps.ForHunt("other"), life.Maps.Holds("other"));
        var rows = life.Ledger.Units.Where(u => u.EventId == "other").OrderBy(u => u.Key).ToList();
        life.EventEnded("raid", path == "natural end" ? T0.AddSeconds(1) : DateTime.MaxValue, cancelOrders: path != "natural end");
        Drain(life);
        Empty(life, "raid");
        Untouched(life);
        Assert.Same(map, life.Maps.ForHunt("other"));
        Assert.Equal(holds, life.Maps.Holds("other"));
        Assert.Equal(rows, life.Ledger.Units.Where(u => u.EventId == "other").OrderBy(u => u.Key));
        Assert.False(life.Maps.AnyFailed);
    }

    [Theory]
    [InlineData("natural end")]
    [InlineData("stop")]
    [InlineData("fault cancel")]
    [InlineData("purge")]
    [InlineData("restart")]
    public void EndPaths_passes_every_path_empties_all_three(string path)
    {
        var life = Running();
        life = path switch
        {
            "purge" => Purge(life),
            "restart" => WaveLifecycle.Restart(Limits),
            _ => End(life, path),
        };
        Drain(life);
        Empty(life, "raid");
        if (path is "purge" or "restart") Empty(life, "other");
        else Untouched(life);

        static WaveLifecycle Purge(WaveLifecycle l) { l.Purged(); return l; }
        static WaveLifecycle End(WaveLifecycle l, string p)
        {
            l.EventEnded("raid", p == "natural end" ? T0.AddSeconds(1) : DateTime.MaxValue, cancelOrders: p != "natural end");
            return l;
        }
    }

    [Fact]
    public void EndPaths_empty_event_without_units()
    {
        var life = Running();
        Assert.Equal(0, life.TrackedFor("quiet"));
        Assert.Equal(0, life.Seeds.CountFor("quiet"));
        Assert.False(life.Maps.Holds("quiet"));
        Assert.Equal((0, 0), life.EventEnded("quiet", DateTime.MaxValue));
        Empty(life, "quiet");
        Assert.Equal(3, life.TrackedFor("raid"));
        Untouched(life);
    }
}
