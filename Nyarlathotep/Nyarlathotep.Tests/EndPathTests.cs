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

    /// <summary>Two events' D21 streaks and `.nyar spawn`'s "manual" one, open.</summary>
    static SpawnHealth Failing()
    {
        var h = new SpawnHealth();
        foreach (var id in new[] { "raid", "other" })
            foreach (var cls in Enum.GetValues<SpawnFailure>()) h.Failing(cls, id);
        h.Failing(SpawnFailure.UnitSetup, "manual");
        return h;
    }

    [Fact]
    public void EndPaths_fails_when_purge_leaves_a_streak_open()
    {
        var h = Failing();
        Assert.True(h.Failed());                                                  // the walk check's, not an event's
        h.Purged();
        Assert.Equal([SpawnHealth.Entry], h.Entries);
        Assert.True(h.Failing(SpawnFailure.HuntSeed, "raid"));                     // a new streak opens and logs again
    }

    [Fact]
    public void EndPaths_fails_when_event_end_leaves_its_streaks()
    {
        var h = Failing();
        h.EventEnded("raid");
        Assert.DoesNotContain(h.Entries, e => e.Contains("(raid)", StringComparison.Ordinal));
        Assert.Equal(4, h.Entries.Count);                                         // "other"'s three and "manual" stay
        Assert.Contains(SpawnHealth.FailingEntry(SpawnFailure.UnitSetup, "manual"), h.Entries);
    }

    /// <summary>The body of the member of Services/EventRuntime.cs that <paramref name="signature"/> opens, to its closing
    /// brace at four spaces; empty when the member is missing.</summary>
    internal static string Body(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0) return "";
        var open = source.IndexOf("\n    {\n", at, StringComparison.Ordinal);
        var close = open < 0 ? -1 : source.IndexOf("\n    }\n", open, StringComparison.Ordinal);
        // A commented-out call is no call (Review 36 F1).
        return close < 0 ? "" : System.Text.RegularExpressions.Regex.Replace(source[open..close], @"//[^\n]*|/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>Every end path of EventRuntime that does not reach its streak close (A71, Review 34 F1): the purge closes
    /// every streak, EndSpawnState the event's, and the stop and the natural end run EndSpawnState.</summary>
    static List<string> RuntimeGaps(string source)
    {
        source = source.Replace("\r\n", "\n");
        var gaps = new List<string>();
        if (!Body(source, "internal static Outcome Purge()").Contains("Health.Purged()", StringComparison.Ordinal)) gaps.Add("Purge");
        if (!Body(source, "static void EndSpawnState(string id)").Contains("Health.EventEnded(id)", StringComparison.Ordinal)) gaps.Add("EndSpawnState");
        if (!Body(source, "static bool End(string id, string why, EndPath path)").Contains("EndSpawnState(id)", StringComparison.Ordinal)) gaps.Add("End");
        if (!Body(source, "internal static void Tick(DateTime now)").Contains("EndSpawnState(ended.Id)", StringComparison.Ordinal)) gaps.Add("Tick");
        return gaps;
    }

    internal static string RuntimeSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", "EventRuntime.cs"));
    }

    [Theory]
    [InlineData("WalkCheck.Health.Purged();", "Purge")]
    [InlineData("WalkCheck.Health.EventEnded(id);", "EndSpawnState")]
    [InlineData("EndSpawnState(id);", "End")]
    [InlineData("EndSpawnState(ended.Id);", "Tick")]
    public void EndPaths_fails_when_runtime_skips_a_streak_close(string call, string gap)
    {
        var source = RuntimeSource().Replace("\r\n", "\n");
        Assert.Empty(RuntimeGaps(source));
        Assert.Contains(call, source);
        Assert.Equal([gap], RuntimeGaps(source.Replace(call, "")));
        Assert.Equal([gap], RuntimeGaps(source.Replace(call, "// " + call)));
        Assert.Equal([gap], RuntimeGaps(source.Replace(call, "/* " + call + " */")));
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
