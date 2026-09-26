using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D16: Logic/SpawnLedger, the registry Services/SpawnTracker drives.</summary>
public class SpawnLedgerTests
{
    static readonly DateTime Now = new(2026, 9, 24, 20, 0, 0, DateTimeKind.Utc);

    static SpawnLedger Ledger(int maxTracked = 150, int perWave = 20, int spawnsPerTick = 10, int despawnsPerTick = 5) =>
        new(new LedgerLimits(maxTracked, perWave, spawnsPerTick, despawnsPerTick));

    static SpawnRequestResult Ask(SpawnLedger l, int count, string? eventId = null) =>
        l.Request("CHAR_Bandit_Thug", eventId, count, new UnitLifetime(DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f));

    static SpawnRequestResult AskDue(SpawnLedger l, int count, DateTime dueUtc, string? eventId = null) =>
        l.Request("CHAR_Bandit_Thug", eventId, count, new UnitLifetime(dueUtc, 300), UnitTuning.None, _ => (0f, 0f, 0f));

    static long _nextKey = 1000;

    /// <summary>Spawns everything queued, tick by tick, confirming each order with a fresh key.</summary>
    static List<long> SpawnAll(SpawnLedger l)
    {
        var keys = new List<long>();
        for (var batch = l.TakeSpawns(); batch.Count > 0; batch = l.TakeSpawns())
            foreach (var order in batch)
            {
                var key = ++_nextKey;
                Assert.True(l.Confirm(order, key, Now));
                keys.Add(key);
            }
        return keys;
    }

    [Fact]
    public void A_request_past_MaxTrackedUnits_is_skipped_with_the_cap_named()
    {
        var l = Ledger(maxTracked: 5);
        Assert.Equal(new SpawnRequestResult(3, null), Ask(l, 3));
        var r = Ask(l, 4);
        Assert.Equal(2, r.Queued);
        Assert.Equal("skipped by MaxTrackedUnits: 2 of 4", r.Skipped);
        var full = Ask(l, 1);
        Assert.Equal(0, full.Queued);
        Assert.Equal("skipped by MaxTrackedUnits: 1 of 1", full.Skipped);
        SpawnAll(l);
        Assert.Equal(5, l.Tracked);
    }

    [Fact]
    public void A_request_past_MaxUnitsPerWave_is_skipped_with_the_cap_named()
    {
        var l = Ledger(perWave: 20);
        var r = Ask(l, 30);
        Assert.Equal(20, r.Queued);
        Assert.Equal("skipped by MaxUnitsPerWave: 10 of 30", r.Skipped);
        Assert.Equal(20, SpawnAll(l).Count);
    }

    [Fact]
    public void The_cap_counts_waiting_orders_so_it_is_never_exceeded()
    {
        var l = Ledger(maxTracked: 10, perWave: 50);
        for (var i = 0; i < 7; i++) Ask(l, 3);
        Assert.Equal(10, l.Occupied);
        SpawnAll(l);
        Assert.Equal(10, l.Tracked);
    }

    [Fact]
    public void The_last_free_slot_goes_to_exactly_one_of_two_racing_requests()
    {
        var l = Ledger(maxTracked: 4);
        Ask(l, 3);
        var first = Ask(l, 1);
        var second = Ask(l, 1);
        Assert.Equal(1, first.Queued);
        Assert.Null(first.Skipped);
        Assert.Equal(0, second.Queued);
        Assert.Equal("skipped by MaxTrackedUnits: 1 of 1", second.Skipped);
        Assert.Equal(4, SpawnAll(l).Count);
    }

    [Fact]
    public void Spawns_leave_the_queue_at_most_the_budget_per_tick()
    {
        var l = Ledger(perWave: 50, spawnsPerTick: 10);
        Ask(l, 25);
        var sizes = new List<int>();
        for (var batch = l.TakeSpawns(); batch.Count > 0; batch = l.TakeSpawns())
        {
            sizes.Add(batch.Count);
            foreach (var o in batch) l.Confirm(o, ++_nextKey, Now);
        }
        Assert.Equal([10, 10, 5], sizes);
    }

    [Fact]
    public void Despawns_leave_the_queue_at_most_the_budget_per_tick()
    {
        var l = Ledger(perWave: 50, despawnsPerTick: 5);
        Ask(l, 12);
        foreach (var k in SpawnAll(l)) l.QueueDespawn(k);
        var sizes = new List<int>();
        for (var batch = l.TakeDespawns(); batch.Count > 0; batch = l.TakeDespawns()) sizes.Add(batch.Count);
        Assert.Equal([5, 5, 2], sizes);
        Assert.Equal(0, l.Tracked);
    }

    [Fact]
    public void An_entry_is_never_released_twice()
    {
        var l = Ledger();
        Ask(l, 3);
        var keys = SpawnAll(l);
        Assert.True(l.QueueDespawn(keys[0]));
        Assert.False(l.QueueDespawn(keys[0]));
        l.Purge();
        l.Purge();
        var released = new List<long>();
        for (var batch = l.TakeDespawns(); batch.Count > 0; batch = l.TakeDespawns()) released.AddRange(batch);
        Assert.Equal(released.Count, released.Distinct().Count());
        Assert.Equal(keys.OrderBy(k => k), released.OrderBy(k => k));
        Assert.False(l.Forget(keys[0]));
    }

    [Fact]
    public void A_unit_spawned_mid_drain_stays_out_of_the_batch_but_is_tracked()
    {
        var l = Ledger(despawnsPerTick: 2);
        Ask(l, 4);
        var before = SpawnAll(l);
        l.Purge();
        var first = l.TakeDespawns();
        Ask(l, 1);
        var late = SpawnAll(l).Single();
        var rest = new List<long>();
        for (var batch = l.TakeDespawns(); batch.Count > 0; batch = l.TakeDespawns()) rest.AddRange(batch);
        Assert.DoesNotContain(late, first.Concat(rest));
        Assert.Equal(before.OrderBy(k => k), first.Concat(rest).OrderBy(k => k));
        Assert.True(l.IsTracked(late));
        Assert.Equal(1, l.Tracked);
    }

    [Fact]
    public void A_purge_mid_drain_absorbs_the_queue_without_duplicates()
    {
        var l = Ledger(perWave: 50, despawnsPerTick: 5);
        Ask(l, 12);
        var keys = SpawnAll(l);
        foreach (var k in keys.Take(8)) l.QueueDespawn(k);
        var first = l.TakeDespawns();
        var (queued, _) = l.Purge();
        Assert.Equal(7, queued);                                  // 3 left of the drain + 4 never queued
        var rest = new List<long>();
        for (var batch = l.TakeDespawns(); batch.Count > 0; batch = l.TakeDespawns()) rest.AddRange(batch);
        var all = first.Concat(rest).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(keys.OrderBy(k => k), all.OrderBy(k => k));
        Assert.Equal(0, l.Tracked);
    }

    [Fact]
    public void After_a_purge_a_second_purge_finds_nothing_while_the_queue_drains()
    {
        var l = Ledger(despawnsPerTick: 2);
        Ask(l, 5);
        SpawnAll(l);
        Ask(l, 2);
        Assert.Equal(7, l.Purgeable);
        l.Purge();
        Assert.Equal(5, l.PendingDespawns);
        Assert.Equal(0, l.Purgeable);
        Assert.False(l.AnythingToPurge);
        Assert.Equal((5, 0), l.Purge());
    }

    [Fact]
    public void A_purge_cancels_waiting_spawns_and_frees_their_slots()
    {
        var l = Ledger(maxTracked: 10, perWave: 50, spawnsPerTick: 3);
        Ask(l, 10);
        foreach (var o in l.TakeSpawns()) l.Confirm(o, ++_nextKey, Now);
        var (queued, cancelled) = l.Purge();
        Assert.Equal(3, queued);
        Assert.Equal(7, cancelled);
        Assert.Empty(l.TakeSpawns());
        while (l.TakeDespawns().Count > 0) { }
        Assert.Equal(0, l.Occupied);
        Assert.False(l.AnythingToPurge);
    }

    [Fact]
    public void An_order_cancelled_while_in_flight_is_not_tracked_and_a_failed_one_frees_its_slot()
    {
        var l = Ledger(maxTracked: 2);
        Ask(l, 2);
        var batch = l.TakeSpawns();
        l.Fail(batch[0]);
        Assert.Equal(1, l.Occupied);
        Assert.True(l.Confirm(batch[1], 7, Now));
        Assert.False(l.Confirm(batch[1], 8, Now));                // confirmed once only
        Assert.Equal(1, l.Tracked);
    }

    [Fact]
    public void A_dead_unit_is_forgotten_and_leaves_the_despawn_queue()
    {
        var l = Ledger();
        Ask(l, 3);
        var keys = SpawnAll(l);
        l.Purge();
        Assert.True(l.Forget(keys[1]));
        Assert.False(l.Forget(keys[1]));
        var released = l.TakeDespawns();
        Assert.Equal([keys[0], keys[2]], released);
    }

    [Fact]
    public void A_marked_survivor_can_be_queued_without_being_tracked()
    {
        var l = Ledger();
        Assert.True(l.QueueDespawn(42));
        Assert.False(l.QueueDespawn(42));
        Assert.Equal(0, l.Tracked);
        Assert.False(l.AnythingToPurge);                          // already draining: nothing left for a purge
        Assert.Equal([42L], l.TakeDespawns());
    }

    [Fact]
    public void Marked_survivors_hold_their_slots_until_they_are_despawned()
    {
        var l = Ledger(maxTracked: 5, despawnsPerTick: 2);
        for (long k = 1; k <= 4; k++) l.QueueDespawn(k);
        Assert.Equal(4, l.Occupied);
        var r = Ask(l, 3);
        Assert.Equal(1, r.Queued);
        Assert.Equal("skipped by MaxTrackedUnits: 2 of 3", r.Skipped);
        Assert.True(l.Forget(3));                                  // a survivor that died meanwhile frees its slot
        Assert.Equal(4, l.Occupied);
        while (l.TakeDespawns().Count > 0) { }
        SpawnAll(l);
        Assert.Equal(1, l.Occupied);
    }

    [Fact]
    public void A_despawn_that_failed_can_be_requeued_and_keeps_its_slot()
    {
        var l = Ledger(maxTracked: 2, despawnsPerTick: 5);
        Ask(l, 2);
        var keys = SpawnAll(l);
        l.Purge();
        var batch = l.TakeDespawns();
        Assert.Equal(0, l.Occupied);
        Assert.True(l.QueueDespawn(batch[0]));                   // its destroy failed: back in the queue
        Assert.Equal(1, l.Occupied);
        Assert.Equal(1, Ask(l, 2).Queued);
        Assert.Equal([batch[0]], l.TakeDespawns());
    }

    [Fact]
    public void LifeTime_is_the_due_time_plus_the_drain_margin_and_the_event_end_wins()
    {
        var end = Now.AddMinutes(10);
        var margin = SpawnLedger.DrainMarginSeconds(150, 5);                // 30 ticks + 60 s
        Assert.Equal(90, margin);
        Assert.Equal(new UnitLifetime(end.AddSeconds(30), 630 + 90), SpawnLedger.Lifetime(Now, end, null, 30, 300, margin));
        // A21: its own shorter lifetime decides the due time, and LifeTime still runs the margin past it
        Assert.Equal(new UnitLifetime(Now.AddSeconds(120), 120 + 90), SpawnLedger.Lifetime(Now, end, 120, 30, 300, margin));
        Assert.Equal(new UnitLifetime(end.AddSeconds(30), 630 + 90), SpawnLedger.Lifetime(Now, end, 7200, 30, 300, margin));
        Assert.Equal(new UnitLifetime(end.AddSeconds(30), 630 + 90), SpawnLedger.Lifetime(Now, end, 630, 30, 300, margin));  // a tie
        Assert.Equal(new UnitLifetime(end.AddSeconds(30), 90), SpawnLedger.Lifetime(end.AddMinutes(5), end, null, 30, 300, margin));
        // A21: a `.nyar spawn` unit is due after ManualSpawnLifetimeSeconds and gets the margin too
        Assert.Equal(new UnitLifetime(Now.AddSeconds(300), 300 + 90), SpawnLedger.Lifetime(Now, null, null, 30, 300, margin));
        Assert.Equal(1, SpawnLedger.Lifetime(end.AddMinutes(5), end, null, 30, 300, 0).LifetimeSeconds);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 30)]
    [InlineData(null, 7200)]
    [InlineData(600, null)]
    [InlineData(600, 30)]
    [InlineData(600, 3000)]
    public void Every_units_LifeTime_ends_after_its_due_time_and_the_full_drain(int? eventMinutesLeft, int? ownLifetime)
    {
        // A21: whichever of the event end, its own lifetime or the manual lifetime decides, the game's LifeTime is the
        // backstop: it ends no sooner than the due time plus a full queue's drain at the budget.
        foreach (var (maxTracked, perTick) in new[] { (1, 20), (150, 5), (500, 1) })
        {
            var margin = SpawnLedger.DrainMarginSeconds(maxTracked, perTick);
            DateTime? end = eventMinutesLeft is { } m ? Now.AddMinutes(m) : null;
            var life = SpawnLedger.Lifetime(Now, end, ownLifetime, 30, 300, margin);
            var drained = life.DueUtc.AddSeconds((maxTracked + perTick - 1) / perTick);
            Assert.True(Now.AddSeconds(life.LifetimeSeconds) > drained, $"{maxTracked}/{perTick}: LifeTime ends before the drain");
        }
    }

    [Fact]
    public void Units_past_their_due_time_are_queued_earliest_first_and_drain_at_the_budget()
    {
        // A21: 12 units of one spawn batch share a due time; they leave through TakeDespawns, 5 a tick, never at once.
        var l = Ledger(despawnsPerTick: 5);
        AskDue(l, 12, Now.AddSeconds(30), "raid");
        AskDue(l, 2, Now.AddSeconds(10));                          // manual units, due earlier
        AskDue(l, 3, Now.AddSeconds(90), "raid");                  // not due yet
        var keys = SpawnAll(l);
        Assert.Equal(0, l.QueueDue(Now.AddSeconds(9)));
        Assert.Equal(14, l.QueueDue(Now.AddSeconds(30)));          // both groups, due time inclusive
        Assert.Equal(0, l.QueueDue(Now.AddSeconds(32)));           // never queued twice
        var first = l.TakeDespawns();
        Assert.Equal(keys.Skip(12).Take(2).Concat(keys.Take(3)), first);   // the earliest due leave first
        var sizes = new List<int> { first.Count };
        for (var batch = l.TakeDespawns(); batch.Count > 0; batch = l.TakeDespawns()) sizes.Add(batch.Count);
        Assert.Equal([5, 5, 4], sizes);
        Assert.Equal(3, l.Tracked);                                // the three not yet due stay
        Assert.Equal(3, l.QueueDue(Now.AddSeconds(90)));
    }

    [Fact]
    public void A_unit_the_game_removed_is_not_queued_when_due()
    {
        var l = Ledger();
        AskDue(l, 2, Now.AddSeconds(10));
        var keys = SpawnAll(l);
        Assert.True(l.Forget(keys[0]));
        Assert.Equal(1, l.QueueDue(Now.AddSeconds(10)));
        Assert.Equal([keys[1]], l.TakeDespawns());
    }

    [Theory]
    [InlineData(1, 20, 61)]
    [InlineData(150, 5, 90)]
    [InlineData(151, 5, 91)]
    [InlineData(500, 1, 560)]
    [InlineData(500, 20, 85)]
    public void The_drain_margin_covers_a_full_queue_at_the_budget(int maxTracked, int perTick, int expected) =>
        Assert.Equal(expected, SpawnLedger.DrainMarginSeconds(maxTracked, perTick));

    [Fact]
    public void The_grace_cleanup_takes_the_end_ticks_units_and_leaves_a_restarts()
    {
        // A17 end to end: a unit of "raid" spawned in the tick it ended, then a restart inside the grace spawns another;
        // the cleanup, bounded at the restart's start by EventEngine, queues only the ended instance's unit.
        var end = Now.AddMinutes(10);
        var restart = end.AddSeconds(10);
        var l = Ledger();
        Ask(l, 1, "raid");
        var old = Assert.Single(l.TakeSpawns());
        Assert.True(l.Confirm(old, 1, end));                   // spawned at EndsUtc
        Ask(l, 1, "raid");
        var fresh = Assert.Single(l.TakeSpawns());
        Assert.True(l.Confirm(fresh, 2, restart.AddSeconds(1)));
        Assert.Equal((1, 0), l.EndEvent("raid", restart, cancelOrders: false));
        Assert.Equal([1L], l.TakeDespawns());
        Assert.True(l.IsTracked(2));
    }

    [Fact]
    public void An_event_unit_outlives_the_drain_of_a_full_queue()
    {
        // A16: a full ledger queued at end + grace and drained at the budget is empty before its units' LifeTime ends.
        var end = Now.AddMinutes(10);
        var l = Ledger(maxTracked: 12, perWave: 12, spawnsPerTick: 12, despawnsPerTick: 5);
        var lifetime = SpawnLedger.Lifetime(Now, end, null, 30, 300,
            SpawnLedger.DrainMarginSeconds(l.Limits.MaxTracked, l.Limits.DespawnsPerTick)).LifetimeSeconds;
        Assert.Equal(12, Ask(l, 12, "raid").Queued);
        Assert.Equal(12, SpawnAll(l).Count);
        Assert.Equal((12, 0), l.EndEvent("raid", DateTime.MaxValue));
        var ticks = 0;
        for (; l.PendingDespawns > 0; ticks++) Assert.True(l.TakeDespawns().Count <= l.Limits.DespawnsPerTick);
        Assert.Equal(3, ticks);
        Assert.True(end.AddSeconds(30 + ticks) < Now.AddSeconds(lifetime));
    }

    [Fact]
    public void Units_are_placed_on_a_circle_of_the_radius()
    {
        Assert.Equal((5f, 7f), SpawnLedger.Around(5, 7, 3, 0, 1, 0));
        for (var i = 0; i < 4; i++)
        {
            var (x, z) = SpawnLedger.Around(5, 7, 3, i, 4, 0.5);
            Assert.Equal(3.0, Math.Sqrt((x - 5) * (x - 5) + (z - 7) * (z - 7)), 3);
        }
    }

    [Fact]
    public void Admin_replies_name_the_cap_and_cut_debug_output_at_20()
    {
        Assert.Equal("spawned 3 CHAR_Bandit_Thug", AdminLines.Spawned(3, "CHAR_Bandit_Thug", null));
        Assert.Equal("skipped by MaxTrackedUnits: 1 of 1", AdminLines.Spawned(0, "CHAR_Bandit_Thug", "skipped by MaxTrackedUnits: 1 of 1"));
        Assert.Equal("spawned 2 X; skipped by MaxTrackedUnits: 1 of 3", AdminLines.Spawned(2, "X", "skipped by MaxTrackedUnits: 1 of 3"));
        var lines = Enumerable.Range(0, 23).Select(i => $"u{i}").ToList();
        var report = AdminLines.DebugReport(lines, 30);
        Assert.Equal(21, report.Count);
        Assert.Equal("+3 more", report[^1]);
        Assert.Equal(["no tracked units within 30 m"], AdminLines.DebugReport([], 30));
        Assert.Equal("CHAR_Bandit_Thug manual left 0s lvl 18 hp 150/150 pp 12", AdminLines.DebugUnit("CHAR_Bandit_Thug", null, -4, 18, 150, 150, 12));
        Assert.Equal("CHAR_Bandit_Thug raid left NONE lvl 18 hp 90/150 pp 12", AdminLines.DebugUnit("CHAR_Bandit_Thug", "raid", null, 18, 90, 150, 12));
        Assert.Equal("recipe ok", AdminLines.Recipe(true, true, true, false));
        Assert.Equal("recipe +DontSave", AdminLines.Recipe(true, true, true, true));
        Assert.Equal("recipe -LifeTime -Age -DestroyWhenDisabled", AdminLines.Recipe(false, false, false, false));
    }

    [Fact]
    public void Replies_are_packed_into_few_messages_under_the_byte_cap_without_splitting_a_line()
    {
        var lines = Enumerable.Range(0, 21).Select(i => $"CHAR_Bandit_Thug manual left {i}s lvl 18 hp 57/57 pp 14").ToList();
        var messages = AdminLines.Pack(lines);
        Assert.True(messages.Count < lines.Count);
        Assert.All(messages, m => Assert.True(System.Text.Encoding.UTF8.GetByteCount(m) <= 480));
        Assert.Equal(lines, messages.SelectMany(m => m.Split('\n')));
        Assert.Equal(["a\nb"], AdminLines.Pack(["a", "b"]));
        Assert.Empty(AdminLines.Pack([]));
        var huge = AdminLines.Pack([new string('\u00e9', 400)]).Single();   // 800 bytes, cut at a whole character
        Assert.Equal(240, huge.Length);
    }

    [Fact]
    public void The_unit_marker_is_registered()
    {
        Assert.Equal(1314472274, Markers.Unit);
        Assert.True(Markers.IsOurs((float)Markers.Unit));          // written into the float SpellLevel.Level
        Assert.False(Markers.IsOurs(1f));
        Assert.False(Markers.IsOurs(0f));
        Assert.NotEmpty(Markers.All);
    }
}
