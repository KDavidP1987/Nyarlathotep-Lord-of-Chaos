using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The fake game of the destination waves' march (undead-hordes D6, D18): a ledger, players, positions and ground.</summary>
sealed class MarchGame : IWaveMarchGame
{
    public SpawnLedger Ledger { get; } = new(new LedgerLimits(2000, 1000, 2000, 2000));
    public WalkBudget Budget { get; } = new();
    public List<(float X, float Z)> Online { get; } = [];
    public Dictionary<long, (float X, float Z)> Positions { get; } = [];
    public List<RegionJump> Moves { get; } = [];
    public List<string> Logs { get; } = [];
    public Func<float, float, byte, bool> Ground { get; set; } = (_, _, _) => true;
    long _key = 9000;

    public IReadOnlyList<(float X, float Z)> Players() => Online;

    public RegionUnit? Read(TrackedUnit unit)
    {
        var (x, z) = Positions.TryGetValue(unit.Key, out var p) ? p : (0f, 0f);
        return new RegionUnit(unit.Key, unit.EventId, unit.SpawnedUtc, x, 0, z, 1, unit.Wave);
    }

    public bool Walkable(float x, float z, byte level) => Ground(x, z, level);
    public float HeightOf(byte level) => level;

    public void Move(RegionJump jump)
    {
        Moves.Add(jump);
        Positions[jump.Key] = (jump.X, jump.Z);
    }

    public void Log(string line) => Logs.Add(line);

    /// <summary>Queues and confirms <paramref name="n"/> units of <paramref name="eventId"/>'s wave <paramref name="wave"/>
    /// round (x, z).</summary>
    public List<long> Spawn(string eventId, int wave, int n, float x, float z, DateTime at)
    {
        Ledger.Request("CHAR_Undead_SkeletonSoldier_Withered", eventId, n, new UnitLifetime(at.AddHours(1), 3600), UnitTuning.None,
            i => (x + i % 5, 0f, z + i / 5), wave: wave);
        var keys = new List<long>();
        for (var batch = Ledger.TakeSpawns(); batch.Count > 0; batch = Ledger.TakeSpawns())
            foreach (var o in batch)
            {
                var key = ++_key;
                Assert.True(Ledger.Confirm(o, key, at));
                Positions[key] = (o.X, o.Z);
                keys.Add(key);
            }
        return keys;
    }
}

public partial class DestinationTests
{
    static readonly HuntTag Tag = new(0, 0, 40);

    static (WaveMarches Host, MarchGame Game) Marching(int units = 5, float toX = 300)
    {
        var host = new WaveMarches();
        var game = new MarchGame();
        game.Spawn("raid", 1, units, 0, 0, T0);
        host.Begin(new WaveMarch("raid", 1, (0, 0), (toX, 0), 20, 2.0, T0));
        return (host, game);
    }

    static void Run(WaveMarches host, MarchGame game, int from, int to)
    {
        for (var s = from; s <= to; s++)
        {
            game.Budget.Reset();
            host.Tick(game, T0.AddSeconds(s));
        }
    }

    [Fact]
    public void March_fails_when_a_hop_happens_with_a_player_within_100_m()
    {
        var (host, game) = Marching();
        game.Online.Add((0, 80));                                               // 80 m off the units: they are watched
        Run(host, game, 0, 25);                                                 // under the 30 s that make it met
        Assert.Empty(game.Moves);
        Assert.InRange(host.Find("raid", 1)!.Front.X, 1, 36);                   // A6: the front waits for units no hop can bring
        game.Online.Clear();
        Run(host, game, 26, 60);                                                // 10 s unwatched, then the stragglers hop
        Assert.NotEmpty(game.Moves);
        var front = host.Find("raid", 1)!.Front;
        Assert.All(game.Moves, m => Assert.True(Apocalypse.Distance(m.X, m.Z, front.X, front.Z) <= 60));
    }

    [Fact]
    public void March_fails_when_a_player_holding_the_front_is_not_logged_or_does_not_hold_it()
    {
        var (host, game) = Marching();
        Run(host, game, 0, 10);
        var x = host.Find("raid", 1)!.Front.X;
        game.Online.Add((x + 40, 0));                                           // 40 m ahead of the front: it holds
        Run(host, game, 11, 35);                                                // under the 30 s that make it met
        Assert.Equal(x, host.Find("raid", 1)!.Front.X, 1);
        Assert.Single(game.Logs, l => l == "march raid wave 1: held by a player");
        game.Online.Clear();
        Run(host, game, 36, 40);
        Assert.True(host.Find("raid", 1)!.Front.X > x + 5);
        Assert.Single(game.Logs, l => l == "march raid wave 1: marching on");
    }

    [Fact]
    public void March_fails_when_a_seen_wave_runs_ahead_of_its_units_or_is_never_met()
    {
        var (host, game) = Marching();
        Run(host, game, 0, 100);                                                // unseen: the units hop with the front
        var m = host.Find("raid", 1)!;
        var x = m.Front.X;
        game.Online.Add((300, 0));                                              // a player waits at the destination
        Run(host, game, 101, 125);
        Assert.Equal(WaveMarchState.Marching, m.State);
        Assert.All(game.Positions.Values, p => Assert.True(Apocalypse.Distance(p.X, p.Z, m.Front.X, m.Front.Z) <= 45));  // never run ahead
        Run(host, game, 126, 140);
        Assert.Equal(WaveMarchState.Met, m.State);                              // 30 s seen: met where it stands, short of the mark
        Assert.Contains(game.Logs, l => l.StartsWith("march raid wave 1: met by a player after ", StringComparison.Ordinal) && l.EndsWith(" s, 5 units", StringComparison.Ordinal));
        Assert.DoesNotContain(game.Logs, l => l.Contains(": arrived after ", StringComparison.Ordinal));
        var tag = host.HuntTagOf("raid", 1, Tag);
        Assert.NotNull(tag);
        Assert.True(Apocalypse.Distance(tag!.Value.X, tag.Value.Z, m.Front.X, m.Front.Z) < 1);
        Assert.True(m.Front.X >= x && m.Front.X < 300 - 20);
    }

    [Fact]
    public void March_fails_when_a_seen_front_leaves_one_unit_behind()
    {
        // D6: a seen front waits for any unit it would leave more than 30 m behind, not only for 80 % of them (Codex step 4b F1)
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var front = new Front(1, 0, 0, 500, 0) { Kind = "wave" };
        front.LastWatchedUtc = now;
        var with = Enumerable.Repeat((0f, 0f), 9).ToList();
        Assert.False(MarchRule.WaitsForUnits(front, with, now));
        Assert.True(MarchRule.WaitsForUnits(front, [.. with, (-40f, 0f)], now));      // one of ten 40 m behind
        Assert.True(MarchRule.WaitsForUnits(front, [.. with, (0f, 40f)], now));       // level with it, 40 m off: a step leaves it
        Assert.False(MarchRule.WaitsForUnits(front, [.. with, (60f, 0f)], now));      // ahead: the front closes on it
        Assert.False(MarchRule.WaitsForUnits(front, [.. with, (-25f, 0f)], now));     // within 30 m
        front.LastWatchedUtc = now.AddSeconds(-11);                                    // unseen: hops bring it up instead
        Assert.False(MarchRule.WaitsForUnits(front, [.. with, (-40f, 0f)], now));
    }

    [Fact]
    public void March_fails_when_a_flickering_watch_holds_the_front_forever()
    {
        // code review step 4: a unit stuck 40 m behind, a player 90 m off who steps out of sight for 5 s in every 13; the
        // watch never lapses for 10 s, so no hop brings the unit up, and the met clock on the same watch ends the wait
        var (host, game) = Marching();
        var stuck = game.Positions.Keys.First();
        game.Positions[stuck] = (-40, 0);
        for (var s = 0; s <= 60; s++)
        {
            game.Online.Clear();
            if (s % 13 < 8) game.Online.Add((0, 90));
            game.Budget.Reset();
            host.Tick(game, T0.AddSeconds(s));
            if (host.Find("raid", 1)!.State == WaveMarchState.Met) break;
        }
        Assert.Equal(WaveMarchState.Met, host.Find("raid", 1)!.State);
        Assert.Contains(game.Logs, l => l.StartsWith("march raid wave 1: met by a player after 3", StringComparison.Ordinal));
        Assert.DoesNotContain(game.Moves, m => m.Key == stuck);                  // never hopped in view
    }

    [Fact]
    public void March_fails_when_arrival_does_not_hand_the_wave_its_hunt_at_the_destination()
    {
        var (host, game) = Marching();
        Assert.Null(host.HuntTagOf("raid", 1, Tag));                            // marching: not seeded
        Assert.Null(host.Seeded("raid", 1));                                    // nor logged as seeded
        Run(host, game, 0, 10);
        Assert.Equal((host.Find("raid", 1)!.Front.X, 0f), host.AnchorOf("raid", 1));   // the watch's anchor follows the front
        Run(host, game, 11, 160);
        Assert.Contains(game.Logs, l => l.StartsWith("march raid wave 1: arrived after ", StringComparison.Ordinal) && l.EndsWith(" s, 5 units", StringComparison.Ordinal));
        var tag = host.HuntTagOf("raid", 1, Tag);
        Assert.NotNull(tag);
        Assert.True(Apocalypse.Distance(tag!.Value.X, tag.Value.Z, 300, 0) <= 20);  // it hunts at the destination, at its own range
        Assert.Equal(40, tag.Value.Range);
        Assert.Null(host.AnchorOf("raid", 1));
        Assert.Equal(Tag, host.HuntTagOf("other", 1, Tag));                       // a wave without a march keeps its own tag
        Assert.Equal("march raid wave 1: first hunt seed", host.Seeded("raid", 1));   // D23's line, once a wave
        Assert.Null(host.Seeded("raid", 1));
        Assert.Null(host.Seeded("other", 1));
    }

    [Fact]
    public void March_fails_when_a_stalled_wave_is_left_without_its_hunt()
    {
        var (host, game) = Marching();
        game.Ground = (x, _, _) => x < 40;
        Run(host, game, 0, 200);
        var stalled = Assert.Single(game.Logs, l => l.Contains(": stalled ", StringComparison.Ordinal));
        Assert.EndsWith(" m short of its destination", stalled);
        Assert.Contains("march raid wave 1 held: no walkable ground ahead", game.Logs);
        var tag = host.HuntTagOf("raid", 1, Tag);
        Assert.NotNull(tag);
        Assert.True(tag!.Value.X < 100);                                         // where it stands, short of 300
    }

    [Fact]
    public void March_empty_a_wave_whose_units_are_gone_or_whose_event_ended_has_no_march()
    {
        var host = new WaveMarches();
        var game = new MarchGame();
        host.Begin(new WaveMarch("raid", 1, (0, 0), (300, 0), 20, 2.0, T0));    // its orders all failed: no unit, none waiting
        Run(host, game, 0, 20);
        Assert.Equal(1, host.Count);                                             // within the grace
        Assert.Equal(0f, host.Find("raid", 1)!.Front.X);                         // no unit: the front waits
        Run(host, game, 21, 31);
        Assert.Equal(0, host.Count);
        var (ended, g2) = Marching();
        Run(ended, g2, 0, 3);
        ended.Ended("raid");
        Assert.Equal(0, ended.Count);
        Assert.Equal(Tag, ended.HuntTagOf("raid", 1, Tag));
    }
}
