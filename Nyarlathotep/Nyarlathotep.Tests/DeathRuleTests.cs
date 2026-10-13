using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D13: a V Blood kill needs VBloodConsumeSource.</summary>
public class DeathRuleTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]       // a gate boss: VBloodUnit alone
    [InlineData(false, false, false)]
    public void Only_a_consume_source_makes_a_V_Blood_kill(bool consume, bool vbloodUnit, bool expected) =>
        Assert.Equal(expected, DeathRule.IsVBloodKill(consume, vbloodUnit, isPlayer: false, ours: false));

    /// <summary>A player character carries VBloodConsumeSource as well; its death is never a V Blood kill, so an "any"
    /// VBloodKilled trigger never fires on a player's death.</summary>
    [Fact]
    public void Fails_when_a_players_death_counts_as_a_V_Blood_kill() =>
        Assert.False(DeathRule.IsVBloodKill(hasConsumeSource: true, hasVBloodUnit: false, isPlayer: true, ours: false));

    /// <summary>revenge D30: a unit an event or `.nyar spawn` spawned raises no VBloodKilled; a native V Blood still does.</summary>
    [Fact]
    public void EventUnits_fails_when_a_tracked_V_Blood_raises_VBloodKilled()
    {
        Assert.False(DeathRule.IsVBloodKill(hasConsumeSource: true, hasVBloodUnit: true, isPlayer: false, ours: true));
        Assert.True(DeathRule.IsVBloodKill(hasConsumeSource: true, hasVBloodUnit: true, isPlayer: false, ours: false));
    }

    /// <summary>revenge D30, Review 4 F1: the ledger read comes before SpawnTracker.Died forgets the victim, in the
    /// DeathPass order the patch runs, so a tracked boss raises nothing even when no kill trigger wants the kill read.</summary>
    [Fact]
    public void EventUnits_fails_when_the_ours_read_comes_after_the_ledger_forgets()
    {
        var ledger = new HashSet<string> { "boss" };
        var raised = new List<string>();
        var deaths = new[] { ("boss", true), ("native", true), ("player", true) };
        DeathPass.Run(deaths,
            d => VBloodPass.Before(d.Item2, isPlayer: d.Item1 == "player", () => ledger.Contains(d.Item1)),
            d => ledger.Remove(d.Item1),
            (d, read) => { if (VBloodPass.Raises(read)) raised.Add(d.Item1); },
            (_, _) => { });
        Assert.Equal(["native"], raised);
    }

    /// <summary>revenge D30, Review 4 F2: a failed ours read fails open (the V Blood counts as native, D10); a death
    /// without VBloodConsumeSource never reads the ledger.</summary>
    [Fact]
    public void EventUnits_fails_when_a_failed_read_drops_the_trigger()
    {
        var read = VBloodPass.Before(true, false, () => throw new InvalidOperationException("ledger"));
        Assert.True(VBloodPass.Raises(read));
        Assert.NotNull(read.Fault);
        var reads = 0;
        Assert.False(VBloodPass.Raises(VBloodPass.Before(false, false, () => { reads++; return false; })));
        Assert.False(VBloodPass.Raises(VBloodPass.Before(true, true, () => { reads++; return false; })));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void EventUnits_passes_a_native_V_Blood()
    {
        Assert.True(VBloodPass.Raises(VBloodPass.Before(true, false, () => false)));
    }

    [Fact]
    public void EventUnits_empty_a_death_without_a_consume_source() =>
        Assert.False(VBloodPass.Raises(VBloodPass.Before(false, false, () => false)));
}
