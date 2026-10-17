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
        Run(host, game, 0, 40);
        Assert.Empty(game.Moves);
        Assert.True(host.Find("raid", 1)!.Front.X > 30);                        // the front walks on: no player within 60 m of it
        game.Online.Clear();
        Run(host, game, 41, 60);                                                // 10 s unwatched, then the stragglers hop
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
        Run(host, game, 11, 40);
        Assert.Equal(x, host.Find("raid", 1)!.Front.X, 1);
        Assert.Single(game.Logs, l => l == "march raid wave 1: held by a player");
        game.Online.Clear();
        Run(host, game, 41, 45);
        Assert.True(host.Find("raid", 1)!.Front.X > x + 5);
        Assert.Single(game.Logs, l => l == "march raid wave 1: marching on");
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
