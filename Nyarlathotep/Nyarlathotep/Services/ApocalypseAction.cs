using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// The apocalypse's game side (docs/dod/apocalypse.md step 3). Logic/ApocalypseHost drives the run; this class reads the
/// natives for the hotspots (read only: the span between the native-read markers is scanned for any write, D2), the
/// players, our own units' positions and the walk check, and writes only units SpawnTracker tracks for the run: the hop of
/// a straggler and the roam jump (the step-1 verdict "go — hop": SpawnTracker.MoveOwn), and the rolling lifetime
/// (SpawnTracker.RenewOwn, D13). Every unit is queued through SpawnTracker's ledger (D14). Each public entry runs on the
/// server main thread; every entry but the tick never throws (Logic/EntryGuard, logged once per streak), while a tick's
/// throw reaches EventRuntime's fault guard (D9's fault cancel, step 3 code review M1). Its walk probe is opened at first
/// use and disposed when the entry returns.
/// </summary>
internal static class ApocalypseAction
{
    static ApocalypseHost _host = new();
    static readonly Game _game = new();
    static readonly EntryGuard _guard = new(line => Core.Log.LogError($"[nyar] {line}"));

    internal static ApocalypseHost Host => _host;

    /// <summary>The apocalypse's health entries (spread, march, roam move), for HealthMonitor and `.nyar status`.</summary>
    internal static IReadOnlyList<string> Health => _host.Health;

    /// <summary>The id a boot resume waits on behind the drain gate, or null (D12).</summary>
    internal static string PendingId => _host.Pending?.EventId;

    /// <summary>At boot, inside EventRuntime.Initialize after the engine is made: a new host, then the record's verdict
    /// (D12). Returns the id that waits to resume, or null. Never throws (D21 resume-read).</summary>
    internal static string Boot(EventEngine engine, DateTime now)
    {
        _host = new ApocalypseHost();
        return Run("boot", () => _host.Boot(() => Persistence.State.Document.Apocalypse, () => EventStore.Catalog.Current, engine, _game,
            Persistence.State, now));
    }

    /// <summary>Engine.Start's last gate for an apocalypse: its hotspots (D2), or the refusal; a throw refuses the start too
    /// (code review L5), so no instance ever starts without its run.</summary>
    internal static Outcome Prepare(EventDefinition def)
    {
        try { return _guard.Prepare(def.Id, () => _host.Prepare(def, _game)); }
        finally { _game.Close(); }
    }

    /// <summary>EventRuntime.StartEvent, once the instance started: the run begins (D3, D11). False when it did not (the
    /// caller logs it).</summary>
    internal static bool Begin(ActiveEvent active, DateTime now) =>
        Run("begin", () => _host.Begin(active, _game, Persistence.State, EventRuntime.Board, now));

    /// <summary>The apocalypse phase, per running apocalypse, inside EventRuntime.Guarded: not caught here, so a throw counts
    /// a fault and the FaultLimit-th in a row cancels the run (D9, D25; code review M1).</summary>
    internal static ApocalypseStep Tick(ActiveEvent active, DateTime now)
    {
        try { return _host.Tick(active, _game, Persistence.State, EventRuntime.Board, now); }
        finally { _game.Close(); }
    }

    /// <summary>The apocalypse phase, before the running events: a resume waiting on the drain gate (D12).</summary>
    internal static ActiveEvent Resume(EventEngine engine, ControlState controls, DateTime now) =>
        Run("resume", () => _host.Resume(engine, controls, _game, Persistence.State, EventRuntime.Board, now));

    /// <summary>Every end path of an apocalypse (D9): see ApocalypseHost.Ended.</summary>
    internal static void Ended(string id, ApocalypseEnd path, int cancelled = 0) =>
        Run("end", () => { _host.Ended(id, path, _game, Persistence.State, cancelled); return 0; });

    /// <summary>`.nyar purge confirm` (D9).</summary>
    internal static void Purged(int cancelled = 0) => Run("purge", () => { _host.Purged(_game, Persistence.State, cancelled); return 0; });

    /// <summary>`.nyar pillar &lt;p&gt; off` (D9): a pending resume of that pillar is cancelled.</summary>
    internal static void PillarOff(Pillar pillar) =>
        Run("pillar off", () => _host.PillarOff(pillar, EventStore.Catalog.Current, EventRuntime.Engine, _game, Persistence.State));

    /// <summary>The spread read of one death, before SpawnTracker.Died (D4, D21 spread-read).</summary>
    internal static SpreadRead? BeforeDeath(Entity killer, Entity died)
    {
        if (_host.Run is null) return null;
        return _host.Before(() => Patches.ScoreReader.Sides(killer, died), () =>
            died.TryGetComponent<Translation>(out var t) ? (t.Value.x, t.Value.z) : throw new InvalidOperationException("victim has no position"), _game);
    }

    /// <summary>After SpawnTracker.Died: the spread (D4).</summary>
    internal static void AfterDeath(SpreadRead? read)
    {
        if (read is null) return;
        Run("spread", () => { _host.After(read, _game, DateTime.UtcNow); return 0; });
    }

    /// <summary>SpawnTracker: an order settled (D15).</summary>
    internal static void Settled(string eventId, int wave, bool spawned)
    {
        try { _host.Settled(eventId, wave, spawned); }
        catch { /* the tally never breaks the spawn queue */ }
    }

    /// <summary>SpawnTracker: a unit left the game (D15).</summary>
    internal static void Left(string eventId, DateTime spawnedUtc, UnitDeparture how)
    {
        try { _host.Left(eventId, spawnedUtc, how); }
        catch { /* the tally never breaks the spawn queue */ }
    }

    /// <summary>`.nyar status`'s line of a running apocalypse, or null.</summary>
    internal static string StatusLine(RunningInstance instance)
    {
        try { return _host.StatusLine(instance, _game, DateTime.UtcNow); }
        catch { return null; }
    }

    /// <summary>`event info`'s run view, or null.</summary>
    internal static ApocalypseView View(string id) => Run("info", () => _host.View(id, _game));

    // ---- unit watch (unit-watch D5, D7, D9): reads of the running run, never a write. A unit the watch removes leaves
    // through SpawnTracker's staged despawn, so <see cref="Left"/> hears it as Despawned (the tally's despawn, no death),
    // and the record's living counts (ApocalypseRun.LivingOf over the ledger) drop it at that despawn like any despawned
    // unit; the record is never told about a removal in any other way, so a resume never rebuilds it (D7).

    /// <summary>The hordes the running apocalypse <paramref name="eventId"/> has raised (the stall backstop, D9); null when
    /// no run of that id runs.</summary>
    internal static int? HordesRisen(string eventId) => _host.Run is { } run && run.Id == eventId ? run.Risen : null;

    /// <summary>The watch's anchor x and z of an apocalypse unit (D5): a horde unit's or boss's horde front, a roamer's
    /// hotspot; null for any other unit or event.</summary>
    internal static (float X, float Z)? AnchorOf(string eventId, int wave)
    {
        if (_host.Run is not { } run || run.Id != eventId) return null;
        if (Apocalypse.HotspotOfWave(wave) is { } spot)
            return run.Hotspots.FirstOrDefault(h => h.Number == spot) is { } h ? (h.X, h.Z) : null;
        if (Apocalypse.HordeOfWave(wave) is { } horde && run.Fronts.TryGetValue(horde, out var front)) return (front.X, front.Z);
        return null;
    }

    /// <summary>An entry other than the tick: a throw is logged once per streak of that entry (Logic/EntryGuard) and gives
    /// the default.</summary>
    static T Run<T>(string entry, Func<T> work)
    {
        try { return _guard.Run(entry, work); }
        finally { _game.Close(); }
    }

    /// <summary>The game side of the host. Reads only; writes only through SpawnTracker on its tracked units.</summary>
    sealed class Game : IApocalypseGame
    {
        readonly SystemRandom _random = new(new System.Random());
        readonly Dictionary<(int X, int Z), byte?> _levels = new();
        WalkCheck.WaveCheck? _check;

        public SpawnLedger Ledger => SpawnTracker.Ledger;
        public IRandom Random => _random;
        public int GraceSeconds => Settings.Limit(Limits.GraceSeconds);
        public int DrainMarginSeconds => SpawnTracker.DrainMargin();
        public WalkBudget Budget => WalkCheck.Budget;
        public int LeftToDrain => SpawnTracker.Ledger.PendingDespawns;

        /// <summary>The entry's walk probe goes; the level cache is kept for the run's centres.</summary>
        internal void Close()
        {
            var check = _check;
            _check = null;
            try { check?.Resource?.Dispose(); }
            catch { /* a failed dispose never breaks the tick */ }
            if (_levels.Count > 512) _levels.Clear();
        }

        IWalkProbe Probe()
        {
            _check ??= WalkCheck.OpenSpots();
            return _check.Value.Walk.Probe;
        }

        /// <summary>Throws without a walk check (Codex cross-inspection M1): no hop, jump or front step lands on unread
        /// ground; the march-read category records the failure (D21), as hunting-grounds' mover does.</summary>
        public bool Walkable(float x, float z, byte level) =>
            Probe() is { } probe ? probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level)
            : throw new InvalidOperationException("walk check: none");

        public float HeightOf(byte level) => WalkCheck.HeightOf(level);

        /// <summary>The i-th of n spawn points within <paramref name="radius"/> of (x, z) on walkable ground: a ring point (a
        /// random point in the circle for one unit) at its centre's level or a neighbouring one, else the centre. The centre's
        /// level is scanned once (WalkCheck.SpotLevels) and kept. Never throws: an unreadable ground gives height 0, which
        /// the game corrects.</summary>
        public (float X, float Y, float Z) Place(float x, float z, float radius, int i, int n)
        {
            float px = x, pz = z;
            try
            {
                if (n <= 1)
                {
                    var a = _random.NextDouble() * 2 * Math.PI;
                    var r = radius * Math.Sqrt(_random.NextDouble());
                    px = x + (float)(r * Math.Cos(a));
                    pz = z + (float)(r * Math.Sin(a));
                }
                else (px, pz) = SpawnLedger.Around(x, z, radius, i, n, 0.0);
                if (CentreLevel(x, z) is not { } level) return (px, 0f, pz);
                foreach (var l in WalkLevels.Spot(level))
                    if (Budget.TryTake(2) && Walkable(px, pz, l)) return (px, HeightOf(l), pz);
                return (x, HeightOf(level), z);
            }
            catch
            {
                return (px, 0f, pz);
            }
        }

        byte? CentreLevel(float x, float z)
        {
            var key = ((int)MathF.Round(x), (int)MathF.Round(z));
            if (_levels.TryGetValue(key, out var known)) return known;
            byte? found = null;
            if (Probe() is { } probe)
                foreach (var level in WalkCheck.SpotLevels)
                {
                    if (!Budget.TryTake(2)) return null;                       // not cached: tried again next time
                    if (probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level)) { found = level; break; }
                }
            _levels[key] = found;
            return found;
        }

        // <native-read>
        /// <summary>The native NPCs of the map as the hotspot reader sees them (D2): read only, never a write, a buff, a move
        /// or a destroy (the native scan checks this span). A unit whose position cannot be read is left out.</summary>
        public IEnumerable<HotspotNative> Natives()
        {
            var natives = new List<HotspotNative>();
            var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<FactionReference>()),
                    ComponentType.ReadOnly(Il2CppType.Of<Health>()), ComponentType.ReadOnly(Il2CppType.Of<Translation>()),
                },
                None = new[] { ComponentType.ReadOnly(Il2CppType.Of<PlayerCharacter>()) },
                // the game disables an NPC with no player near; a hotspot far from every player is still a hotspot (session 1)
                Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag,
            });
            try
            {
                var units = query.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var unit in units)
                    {
                        var t = unit.Read<Translation>().Value;
                        if (!PlayerPosition.Usable(t.x, t.z)) continue;
                        var dead = unit.Has<Dead>() || (unit.TryGetComponent<Health>(out var h) && h.Value <= 0f);
                        var facts = new UnitFacts(
                            unit.GetPrefabGuid().GetPrefabName(),
                            unit.Read<FactionReference>().FactionGuid._Value.GetPrefabName(),
                            unit.Has<Prefab>(),
                            dead,
                            unit.Has<VBloodUnit>(),
                            SpawnTracker.IsOurs(unit),
                            unit.TryGetComponent<Follower>(out var f) && f.Followed._Value != Entity.Null,
                            null,
                            () => RegionMap.State.Available ? RegionMap.State.Index.RegionOf(t.x, t.z) : null);
                        natives.Add(new HotspotNative(facts, t.x, t.z));
                    }
                }
                finally { units.Dispose(); }
            }
            finally { query.Dispose(); }
            return natives;
        }
        // </native-read>

        public IReadOnlyList<HuntCandidate> Players() =>
            PlayerQuery.Read().Select(p => new HuntCandidate(p.Key, p.X, p.Z, true, p.Alive, false, p.InPvpCombat)).ToList();

        public RegionUnit? Read(TrackedUnit unit) => SpawnTracker.ReadOwn(unit);

        /// <summary>Claimed by the run's kept map (TerritoryMap.Maps.ForHunt); without one every point counts as claimed, so
        /// no roamer lands there and no player in it is chosen (fail closed, as Hunt's seeds).</summary>
        public bool IsClaimed(string eventId, float x, float z) =>
            TerritoryMap.Maps.ForHunt(eventId) is not { } map || Territory.IsClaimed(map, x, z);

        public void Move(RegionJump jump) => SpawnTracker.MoveOwn(jump.Key, new float3(jump.X, jump.Y, jump.Z));

        public void Renew(long key, UnitLifetime life) => SpawnTracker.RenewOwn(key, life);

        public string RegionName(float x, float z)
        {
            if (!RegionMap.State.Available) return null;
            var region = RegionMap.State.Index.RegionOf(x, z);
            return string.IsNullOrEmpty(region) || region == RegionNames.None ? null : RegionNames.Display(region);
        }

        public void MapTerritory(string eventId) => TerritoryMap.ForWave(eventId);

        public void ResumeCancelled(string eventId) => EventRuntime.EndSpawnState(eventId);

        public void Log(string line) => Core.Log.LogInfo($"[nyar] {line}");

        public void Warn(string line) => Core.Log.LogWarning($"[nyar] {line}");

        public void Announce(string eventId, string line) => Announcer.Apocalypse(eventId, line);

        /// <summary>The run's scaling source (scaling D5, D12): Services/ScalingReads, null without the block.</summary>
        public ScalingSource Scaling(ActiveEvent active) => ScalingReads.Source(active);    }
}
