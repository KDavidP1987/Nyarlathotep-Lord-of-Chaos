using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D5: a wave is cleared once it was decided and no order and no tracked unit of that event and wave
/// remain, whatever removed them.</summary>
public partial class SpawnLedgerTests
{
    static SpawnRequestResult AskWave(SpawnLedger l, string eventId, int wave, int count, DateTime? due = null) =>
        l.Request("CHAR_Bandit_Thug", eventId, count, new UnitLifetime(due ?? DateTime.MaxValue, 300), UnitTuning.None, _ => (0f, 0f, 0f), wave: wave);

    /// <summary>The engine's view (WaveSchedule.Cleared): a decided wave of ws with the ledger's answer.</summary>
    static bool Cleared(SpawnLedger l, int wave, int decided = 3)
    {
        var def = Json.One(Json.Event("ws"));
        var a = new ActiveEvent(new RunningInstance(def, Now, Now.AddMinutes(10)), "manual", null);
        for (var i = 0; i < decided; i++) a.Record(Now, true);
        return WaveSchedule.Cleared(a, wave, w => l.WaveCleared("ws", w));
    }

    [Fact]
    public void WaveCleared_fails_when_a_pending_order_or_a_live_unit_remains()
    {
        var l = Ledger();
        AskWave(l, "ws", 1, 2);
        Assert.False(l.WaveCleared("ws", 1));                                    // waiting
        var batch = l.TakeSpawns();
        Assert.False(l.WaveCleared("ws", 1));                                    // in flight
        Assert.True(l.Confirm(batch[0], 1, Now));
        l.Fail(batch[1]);                                                        // a spawn that failed
        Assert.False(l.WaveCleared("ws", 1));                                    // one unit lives
        Assert.Equal(("ws", 1), l.EventOf(1));
        Assert.True(l.Forget(1));                                                // death
        Assert.True(l.WaveCleared("ws", 1));
        Assert.Null(l.EventOf(1));
    }

    [Fact]
    public void WaveCleared_passes_every_removal_path()
    {
        // despawn through the budget
        var l = Ledger();
        AskWave(l, "ws", 1, 1);
        var key = SpawnAll(l).Single();
        Assert.True(l.QueueDespawn(key));
        Assert.False(l.WaveCleared("ws", 1));                                    // queued is not gone
        Assert.Equal([key], l.TakeDespawns());
        Assert.True(l.WaveCleared("ws", 1));

        // the unit lifetime
        l = Ledger();
        AskWave(l, "ws", 1, 1, due: Now.AddSeconds(30));
        SpawnAll(l);
        Assert.Equal(1, l.QueueDue(Now.AddSeconds(30)));
        l.TakeDespawns();
        Assert.True(l.WaveCleared("ws", 1));

        // removed by the game
        l = Ledger();
        AskWave(l, "ws", 1, 1);
        Assert.True(l.Forget(SpawnAll(l).Single()));
        Assert.True(l.WaveCleared("ws", 1));

        // the event's end: waiting orders cancelled, tracked units drained
        l = Ledger(spawnsPerTick: 1);
        AskWave(l, "ws", 1, 3);
        Assert.True(l.Confirm(l.TakeSpawns().Single(), 77, Now));
        Assert.Equal((1, 2), l.EndEvent("ws", DateTime.MaxValue));
        while (l.PendingDespawns > 0) l.TakeDespawns();
        Assert.True(l.WaveCleared("ws", 1));
    }

    [Fact]
    public void WaveCleared_fails_when_another_event_or_wave_counts()
    {
        var l = Ledger();
        AskWave(l, "ws", 2, 1);
        AskWave(l, "other", 1, 1);
        AskWave(l, null!, 0, 1);                                                 // a .nyar spawn unit
        SpawnAll(l);
        Assert.True(l.WaveCleared("ws", 1));
        Assert.False(l.WaveCleared("ws", 2));
        Assert.False(l.WaveCleared("other", 1));
    }

    [Fact]
    public void WaveCleared_fails_when_an_undecided_wave_reads_cleared()
    {
        var l = Ledger();
        Assert.True(l.WaveCleared("ws", 3));                                     // the ledger alone knows no decision
        Assert.False(Cleared(l, 3, decided: 2));
        Assert.True(Cleared(l, 3, decided: 3));
        Assert.False(Cleared(l, 0));
    }

    [Fact]
    public void WaveCleared_empty_no_order_ever()
    {
        var l = Ledger();
        Assert.True(Cleared(l, 1, decided: 1));                                  // a wave that queued nothing is cleared at once
        Assert.Null(l.EventOf(12345));
    }
}
