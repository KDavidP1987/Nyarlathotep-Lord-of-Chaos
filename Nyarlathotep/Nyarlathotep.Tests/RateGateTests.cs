using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D6 (S-4): at most 5 twins per admin in any sliding second; a refused twin does not count;
/// the gate holds at most 64 admins, prunes idle ones when full, and refuses a newcomer to a gate full of active ones.</summary>
public class RateGateTests
{
    static readonly DateTime T0 = new(2026, 9, 28, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RateGate_passes_five_in_a_second()
    {
        var gate = new RateGate();
        for (var i = 0; i < 5; i++) Assert.True(gate.Admit(7, T0.AddMilliseconds(i * 100)));
    }

    [Fact]
    public void RateGate_fails_when_sixth_within_a_second()
    {
        var gate = new RateGate();
        for (var i = 0; i < 5; i++) gate.Admit(7, T0.AddMilliseconds(i * 100));
        Assert.False(gate.Admit(7, T0.AddMilliseconds(999)));
        Assert.False(gate.Admit(7, T0.AddMilliseconds(999)));          // a refused twin does not count, and does not free a slot
    }

    [Fact]
    public void RateGate_passes_sliding_window()
    {
        var gate = new RateGate();
        for (var i = 0; i < 5; i++) gate.Admit(7, T0.AddMilliseconds(i * 100));
        Assert.True(gate.Admit(7, T0.AddSeconds(1)));                  // the first left the window at exactly 1 s
        Assert.False(gate.Admit(7, T0.AddMilliseconds(1050)));
        Assert.True(gate.Admit(7, T0.AddMilliseconds(1100)));
    }

    [Fact]
    public void RateGate_passes_admins_apart()
    {
        var gate = new RateGate();
        for (var i = 0; i < 5; i++) gate.Admit(7, T0);
        Assert.False(gate.Admit(7, T0));
        Assert.True(gate.Admit(8, T0));
    }

    [Fact]
    public void RateGate_fails_when_gate_full_of_active_admins()
    {
        var gate = new RateGate();
        for (ulong id = 1; id <= RateGate.Capacity; id++) Assert.True(gate.Admit(id, T0));
        Assert.False(gate.Admit(1000, T0.AddMilliseconds(500)));
        Assert.Equal(RateGate.Capacity, gate.Count);
        Assert.True(gate.Admit(1, T0.AddMilliseconds(500)));          // a held admin still runs
    }

    [Fact]
    public void RateGate_passes_idle_admins_pruned()
    {
        var gate = new RateGate();
        for (ulong id = 1; id <= RateGate.Capacity; id++) gate.Admit(id, T0);
        Assert.True(gate.Admit(1000, T0.AddSeconds(1)));
        Assert.Equal(1, gate.Count);
        for (ulong id = 1; id <= 200; id++) gate.Admit(id, T0.AddSeconds(1));
        Assert.True(gate.Count <= RateGate.Capacity);
    }

    [Fact]
    public void RateGate_passes_clock_stepped_back()
    {
        var gate = new RateGate();
        for (var i = 0; i < 5; i++) Assert.True(gate.Admit(7, T0));
        Assert.False(gate.Admit(7, T0.AddMilliseconds(500)));
        Assert.True(gate.Admit(7, T0.AddSeconds(-30)));                  // an NTP step back does not lock the admin out
        for (var i = 0; i < 4; i++) Assert.True(gate.Admit(7, T0.AddSeconds(-30)));
        Assert.False(gate.Admit(7, T0.AddSeconds(-30)));                 // and the limit still holds on the new clock
    }

    [Fact]
    public void RateGate_empty_no_prior_twins()
    {
        var gate = new RateGate();
        Assert.Equal(0, gate.Count);
        Assert.True(gate.Admit(7, T0));
        Assert.Equal(1, gate.Count);
    }
}
