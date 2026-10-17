#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Nyarlathotep.Logic;

/// <summary>What the destination waves' march reads and writes in the game (undead-hordes D6): the ledger, the players, our
/// own units' positions, the walk check and the hop of a straggler. Writes only units the ledger tracks.</summary>
public interface IWaveMarchGame
{
    SpawnLedger Ledger { get; }
    WalkBudget Budget { get; }
    IReadOnlyList<(float X, float Z)> Players();
    RegionUnit? Read(TrackedUnit unit);
    bool Walkable(float x, float z, byte level);
    float HeightOf(byte level);
    void Move(RegionJump jump);
    void Log(string line);
}

/// <summary>
/// The marches of every destination wave (undead-hordes D6, D18). Each second each marching wave's front steps by
/// <see cref="WaveMarch.Step"/>; every <see cref="HopEvery"/> its stragglers more than 15 m from the front hop ahead of it,
/// only while no player has been within 100 m of the wave for 10 s (<see cref="MarchRule.Stragglers"/>), one tick's hops
/// bounded by <see cref="RoundRobin.PerPass"/> over all waves. A wave's Hunt units are not seeded while it marches; once it
/// arrives or stalls they hunt round the point where its front stopped (<see cref="HuntTagOf"/>). A wave whose units are
/// all gone, with none waiting to spawn, is dropped; an event's end drops its waves.
/// </summary>
public sealed class WaveMarches
{
    /// <summary>The hop interval, the hunt's (HuntPlan.IntervalSeconds) as the apocalypse's hops.</summary>
    public static readonly TimeSpan HopEvery = TimeSpan.FromSeconds(HuntPlan.IntervalSeconds);

    /// <summary>A wave with no unit read yet, none waiting, is dropped after this (its orders all failed).</summary>
    public static readonly TimeSpan EmptyGrace = TimeSpan.FromSeconds(30);

    readonly Dictionary<(string EventId, int Wave), WaveMarch> _marches = new();
    readonly Dictionary<(string EventId, int Wave), RoundRobin> _hopTurns = new();   // one per wave: a key order per unit set
    DateTime? _last;
    DateTime _nextHop;

    public int Count => _marches.Count;

    /// <summary>The hop visits so far (ScaleTests).</summary>
    public long HopVisits => _hopTurns.Values.Sum(r => r.Visits);

    public WaveMarch? Find(string eventId, int wave) => _marches.TryGetValue((eventId, wave), out var m) ? m : null;

    /// <summary>A destination wave was queued: its march starts now.</summary>
    public void Begin(WaveMarch march) => _marches[(march.EventId, march.Wave)] = march;

    /// <summary>The hunt tag of a unit of <paramref name="eventId"/>'s wave <paramref name="wave"/>: its own without a march;
    /// none while the wave marches (it is not seeded); the arrival or stall point once released.</summary>
    public HuntTag? HuntTagOf(string eventId, int wave, HuntTag tag) =>
        Find(eventId, wave) is not { } m ? tag : m.Released ? tag with { X = m.Front.X, Z = m.Front.Z } : null;

    /// <summary>"march &lt;event&gt; wave &lt;k&gt;: first hunt seed" the first time a released wave's unit is seeded (D23's
    /// evidence), else null; a wave without a march has none.</summary>
    public string? Seeded(string eventId, int wave)
    {
        if (Find(eventId, wave) is not { Released: true } m || !_seeded.Add((eventId, wave))) return null;
        return $"march {eventId} wave {wave}: first hunt seed";
    }

    readonly HashSet<(string EventId, int Wave)> _seeded = new();

    /// <summary>The unit watch's anchor of a marching wave's unit: its front; null for any other unit (D6).</summary>
    public (float X, float Z)? AnchorOf(string eventId, int wave) =>
        Find(eventId, wave) is { Released: false } m ? (m.Front.X, m.Front.Z) : null;

    /// <summary>An end path of <paramref name="eventId"/>: its marches go.</summary>
    public void Ended(string eventId)
    {
        foreach (var key in _marches.Keys.Where(k => k.EventId == eventId).ToList()) Drop(key);
    }

    /// <summary>`purge confirm`: every march goes.</summary>
    public void Clear()
    {
        _marches.Clear();
        _hopTurns.Clear();
        _seeded.Clear();
        _last = null;
    }

    void Drop((string EventId, int Wave) key)
    {
        _marches.Remove(key);
        _hopTurns.Remove(key);
        _seeded.Remove(key);
    }

    /// <summary>One second of every march: the lines it logged ("march &lt;event&gt; wave &lt;k&gt;: …"), already logged
    /// through <see cref="IWaveMarchGame.Log"/>.</summary>
    public void Tick(IWaveMarchGame game, DateTime utcNow)
    {
        if (_marches.Count == 0) { _last = null; return; }
        var seconds = _last is { } last ? Math.Clamp((utcNow - last).TotalSeconds, 0, 5) : 1;
        _last = utcNow;
        var marching = _marches.Values.Where(m => !m.Released).ToList();
        var byWave = game.Ledger.Units.Where(u => u.EventId is not null && _marches.ContainsKey((u.EventId, u.Wave)))
            .ToLookup(u => (u.EventId!, u.Wave));
        foreach (var key in _marches.Keys.Where(k => !byWave.Contains(k) && game.Ledger.Waiting(k.EventId) == 0
                     && utcNow - _marches[k].StartedUtc >= EmptyGrace).ToList())
            Drop(key);                                                                         // every unit gone, none to come
        if (marching.Count == 0) return;
        var players = game.Players();
        var hopDue = utcNow >= _nextHop;
        if (hopDue) _nextHop = utcNow + HopEvery;
        var reads = marching.ToDictionary(m => m, m => byWave[(m.EventId, m.Wave)].Select(game.Read).OfType<RegionUnit>().ToList());
        var total = reads.Values.Sum(u => u.Count);
        foreach (var m in marching)
        {
            if (!_marches.ContainsKey((m.EventId, m.Wave))) continue;
            var units = reads[m];
            if (units.Count == 0) continue;                                                    // not spawned yet: the front waits
            MarchRule.Watch(m.Front, players, units.Select(u => (u.X, u.Z)), utcNow);
            if (hopDue)
            {
                // D18: the wave's share of one tick's bound, by unit key
                var turns = _hopTurns.TryGetValue((m.EventId, m.Wave), out var r) ? r : _hopTurns[(m.EventId, m.Wave)] = new RoundRobin();
                var slice = turns.Take(units, RoundRobin.Share(units.Count, total), u => u.Key);
                var stragglers = MarchRule.Stragglers(m.Front, slice.Select(u => (u.Key, u.X, u.Z)), players, utcNow);
                var index = new Dictionary<long, int>(units.Count);
                for (var j = 0; j < units.Count; j++) index[units[j].Key] = j;
                for (var i = 0; i < stragglers.Count; i++)
                {
                    var at = index[stragglers[i]];
                    if (HopRule.Land(units[at], m.Front, i, stragglers.Count, game.Walkable, game.HeightOf, game.Budget) is not { } hop) continue;
                    game.Move(hop);
                    units[at] = units[at] with { X = hop.X, Y = hop.Y, Z = hop.Z, Level = hop.Level };
                }
            }
            byte? level = units.MinBy(u => Apocalypse.Distance(u.X, u.Z, m.Front.X, m.Front.Z)).Level;
            var line = m.Step(seconds, players, (x, z) => HopRule.Ground(x, z, level, game.Walkable, game.Budget), utcNow, units.Count,
                out var held, (x, z) => HopRule.GroundRead(x, z, level, game.Walkable, game.Budget), units.Select(u => (u.X, u.Z)).ToList());
            if (held is not null) game.Log($"march {m.EventId} {held}");
            if (m.HoldLine is not null) game.Log(m.HoldLine);
            if (line is not null) game.Log(line);
        }
    }
}
