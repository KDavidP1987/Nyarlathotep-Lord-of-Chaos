using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D34 (A1, the owner's still-at-spawn check): SpawnStillSeconds after its spawn or a landing by one of
/// our movers, a watched unit that moved no more than 0.5 m on x and z from that spot, did not fight, and held a player as
/// its aggro target (A2), is stuck at once ("still"); a unit with no player target never marks by this check.</summary>
public partial class UnitWatchTests
{
    const string StillLine = "unit-watch uw stuck: CHAR_Spider_Forest wave 1 (still)";

    /// <summary>One uw unit spawned at (500, 500), now <paramref name="offset"/> m east of that spot, and a player
    /// <paramref name="playerAt"/> m west of the spot (null: none), held as its aggro target unless <paramref name="aim"/> is
    /// false.</summary>
    static (WatchRig R, long Key) AtSpawn(float offset, float? playerAt = 30f, bool aim = true)
    {
        var (r, _) = Watch.Rig();
        var key = r.W.Add("uw", Spawned, 500, 500);
        r.W.Landing(key, 500, 500, Spawned);
        r.W.MoveTo(key, 500 + offset, 500);
        if (playerAt is { } d)
        {
            r.W.Player(500 - d, 500);
            if (aim) r.W.Aim(key, d + offset);
        }
        return (r, key);
    }

    [Fact]
    public void Still_fails_when_a_unit_0_4_m_from_its_spawn_spot_holding_a_player_target_is_not_stuck()
    {
        var (r, key) = AtSpawn(0.4f);
        r.For(15);                                                 // 14 s after the spawn: past SpawnStillSeconds 10
        Assert.Equal(StuckReason.Still, r.P.MarkOf(key));
        Assert.Equal([StillLine], r.Log.Take(1));
    }

    [Fact]
    public void Still_fails_when_a_unit_that_moved_0_6_m_is_stuck()
    {
        var (r, key) = AtSpawn(0.6f);
        r.For(60);
        Assert.Null(r.P.MarkOf(key));
        Assert.Equal(0, r.Lines("(still)"));
    }

    [Fact]
    public void Still_fails_when_a_unit_checked_before_spawn_still_seconds_is_stuck()
    {
        var (r, key) = AtSpawn(0f);
        r.S = r.S with { SpawnStillSeconds = 30 };
        r.For(25);                                                 // 24 s after the spawn
        Assert.Null(r.P.MarkOf(key));
        r.For(10);                                                 // 34 s
        Assert.Equal(StuckReason.Still, r.P.MarkOf(key));
    }

    [Fact]
    public void Still_fails_when_a_unit_with_a_player_at_10_m_and_no_target_is_stuck()
    {
        var (r, key) = AtSpawn(0f, playerAt: 10f, aim: false);     // idle beside a player (Session 1)
        r.For(60);
        Assert.Null(r.P.MarkOf(key));
        var (q, aimed) = AtSpawn(0f, playerAt: null);
        q.W.Aim(aimed, 60f);                                       // a player target 60 m away, no player within 40 m
        q.For(15);
        Assert.Equal(StuckReason.Still, q.P.MarkOf(aimed));
    }

    [Fact]
    public void Still_fails_when_a_unit_that_fought_is_stuck()
    {
        var (r, key) = AtSpawn(0f);
        Assert.True(r.P.Fights.Report(key, true, Spawned.AddSeconds(2), r.W.Ledger.IsTracked));
        r.For(60);
        Assert.Null(r.P.MarkOf(key));
        var (q, close) = AtSpawn(0f);
        q.W.Aim(close, 4f);                                        // a target within 5 m is a fight (D4)
        q.For(60);
        Assert.Null(q.P.MarkOf(close));
    }

    [Fact]
    public void Still_fails_when_a_unit_put_down_by_a_jump_is_not_checked_from_its_landing()
    {
        var (r, key) = AtSpawn(3f);                                // walked off its spawn spot
        r.For(30);
        Assert.Null(r.P.MarkOf(key));
        r.W.Player(530, 500, key: 2);
        r.W.Landing(key, 540, 500, r.Now);                         // a hidden jump lands it 10 m east of the player
        r.W.MoveTo(key, 540.3f, 500);
        r.W.Aim(key, 10.3f);
        r.For(5);
        Assert.Null(r.P.MarkOf(key));                              // 5 s after the landing
        r.For(10);
        Assert.Equal(StuckReason.Still, r.P.MarkOf(key));
        var (q, walked) = AtSpawn(3f);                             // walking there is no landing: the check stays decided
        q.For(30);
        q.W.Player(530, 500, key: 2);
        q.W.MoveTo(walked, 540.3f, 500);
        q.For(30);
        Assert.Null(q.P.MarkOf(walked));
    }

    [Fact]
    public void Still_fails_when_a_default_outside_5_60_is_accepted()
    {
        Assert.Equal((10, 5, 60), (Limits.SpawnStillSeconds.Default, Limits.SpawnStillSeconds.Min, Limits.SpawnStillSeconds.Max));
        Assert.Equal(10, WatchSettings.Default.SpawnStillSeconds);
        Assert.Equal((5, "UnitWatch.SpawnStillSeconds=4 is below 5; using 5"), Limits.SpawnStillSeconds.Clamp(4));
        Assert.Equal((60, "UnitWatch.SpawnStillSeconds=61 is above its ceiling 60; using 60"), Limits.SpawnStillSeconds.Clamp(61));
        Assert.Contains(Limits.SpawnStillSeconds, Limits.All);
    }

    [Fact]
    public void Still_passes_unit_at_its_spawn_spot_beside_a_player_marked_still_once()
    {
        var (r, key) = AtSpawn(0f, playerAt: 10f);
        r.For(60);
        Assert.Equal(StuckReason.Still, r.P.MarkOf(key));
        Assert.Equal(1, r.Lines("stuck:"));
        Assert.Equal(StillLine, r.Log[0]);
        Assert.Equal(1, r.Lines("waiting:"));                      // the player in view: no move yet (D5)
        Assert.Empty(r.W.Moves);
    }

    [Fact]
    public void Still_empty_no_player_no_mark()
    {
        var (r, key) = AtSpawn(0f, playerAt: null);
        r.For(600);
        Assert.Null(r.P.MarkOf(key));
        Assert.Empty(r.Log);
    }
}
