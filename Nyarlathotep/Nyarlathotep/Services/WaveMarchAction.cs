using System.Collections.Generic;
using System.Linq;
using Nyarlathotep.Logic;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// The destination waves' game side (undead-hordes D6). Logic/WaveMarches drives the marches; this class reads the players,
/// our own units' positions and the walk check, and writes only units SpawnTracker tracks: the hop of a straggler
/// (SpawnTracker.MoveOwn, as the apocalypse's hops). Memory only: a restart cancels a SpawnWaves event, so no march is kept.
/// Each entry runs on the server main thread; the tick's throw is logged once per streak and the next tick tries again.
/// </summary>
internal static class WaveMarchAction
{
    static readonly WaveMarches _marches = new();
    static readonly Game _game = new();
    static readonly EntryGuard _guard = new(line => Core.Log.LogWarning($"[nyar] {line}"));

    internal static WaveMarches Marches => _marches;

    /// <summary>WaveAction: a destination wave was queued from <paramref name="centre"/>.</summary>
    internal static void Begin(ActiveEvent active, int wave, (float X, float Z) centre, DateTime now)
    {
        if (active.Definition.Action is not { Destination: not null } action) return;
        if (WaveMarch.Of(active.Id, action, wave, centre, active.Origin, now) is { } march) _marches.Begin(march);
    }

    /// <summary>The scheduler's march phase, each second while a march runs.</summary>
    [Mutating]
    internal static void Tick(DateTime now)
    {
        if (_marches.Count == 0) return;
        try { _guard.Run("march", () => { _marches.Tick(_game, now); return 0; }); }
        finally { _game.Close(); }
    }

    /// <summary>HuntAction: the hunt tag of a destination wave's unit (none while it marches).</summary>
    internal static HuntTag? HuntTagOf(string eventId, int wave, HuntTag tag) => _marches.HuntTagOf(eventId, wave, tag);

    /// <summary>HuntAction: a destination wave's unit got a seed; the first one is logged.</summary>
    internal static void Seeded(string eventId, int wave)
    {
        if (_marches.Seeded(eventId, wave) is { } line) Core.Log.LogInfo($"[nyar] {line}");
    }

    /// <summary>The unit watch's anchor of a marching wave's unit: its front.</summary>
    internal static (float X, float Z)? AnchorOf(string eventId, int wave) => _marches.AnchorOf(eventId, wave);

    internal static void Ended(string eventId) => _marches.Ended(eventId);

    internal static void Purged() => _marches.Clear();

    sealed class Game : IWaveMarchGame
    {
        WalkCheck.WaveCheck? _check;

        public SpawnLedger Ledger => SpawnTracker.Ledger;
        public WalkBudget Budget => WalkCheck.Budget;

        internal void Close()
        {
            var check = _check;
            _check = null;
            try { check?.Resource?.Dispose(); }
            catch { /* a failed dispose never breaks the tick */ }
        }

        public IReadOnlyList<(float X, float Z)> Players() =>
            PlayerQuery.Read().Where(p => PlayerPosition.Usable(p.X, p.Z)).Select(p => (p.X, p.Z)).ToList();

        public RegionUnit? Read(TrackedUnit unit) => SpawnTracker.ReadOwn(unit);

        /// <summary>Throws without a walk check, so no hop or front step lands on unread ground.</summary>
        public bool Walkable(float x, float z, byte level)
        {
            _check ??= WalkCheck.OpenSpots();
            return _check.Value.Walk.Probe is { } probe ? probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level)
                : throw new InvalidOperationException("walk check: none");
        }

        public float HeightOf(byte level) => WalkCheck.HeightOf(level);

        public void Move(RegionJump jump) => SpawnTracker.MoveOwn(jump.Key, new float3(jump.X, jump.Y, jump.Z));

        public void Log(string line) => Core.Log.LogInfo($"[nyar] {line}");
    }
}
