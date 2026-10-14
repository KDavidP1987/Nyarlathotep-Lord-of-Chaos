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
/// server main thread and never throws; its walk probe is opened at first use and disposed when the entry returns.
/// </summary>
internal static class ApocalypseAction
{
    static ApocalypseHost _host = new();
    static readonly Game _game = new();

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
        return Run(() => _host.Boot(() => Persistence.State.Document.Apocalypse, () => EventStore.Catalog.Current, engine, _game,
            Persistence.State, now));
    }

    /// <summary>Engine.Start's last gate for an apocalypse: its hotspots (D2), or the refusal.</summary>
    internal static Outcome Prepare(EventDefinition def) => Run(() => _host.Prepare(def, _game));

    /// <summary>EventRuntime.StartEvent, once the instance started: the run begins (D3, D11).</summary>
    internal static void Begin(ActiveEvent active, DateTime now) =>
        Run(() => _host.Begin(active, _game, Persistence.State, EventRuntime.Board, now));

    /// <summary>The events phase, per running apocalypse (inside EventRuntime's per-event try/catch).</summary>
    internal static ApocalypseStep Tick(ActiveEvent active, DateTime now) =>
        Run(() => _host.Tick(active, _game, Persistence.State, EventRuntime.Board, now)) ?? ApocalypseStep.None;

    /// <summary>The events phase, before the running events: a resume waiting on the drain gate (D12).</summary>
    internal static ActiveEvent Resume(EventEngine engine, ControlState controls, DateTime now) =>
        Run(() => _host.Resume(engine, controls, _game, Persistence.State, EventRuntime.Board, now));

    /// <summary>Every end path of an apocalypse (D9): see ApocalypseHost.Ended.</summary>
    internal static void Ended(string id, ApocalypseEnd path, int cancelled = 0) =>
        Run(() => { _host.Ended(id, path, _game, Persistence.State, cancelled); return 0; });

    /// <summary>`.nyar purge confirm` (D9).</summary>
    internal static void Purged() => Run(() => { _host.Purged(_game, Persistence.State); return 0; });

    /// <summary>`.nyar pillar &lt;p&gt; off` (D9): a pending resume of that pillar is cancelled.</summary>
    internal static void PillarOff(Pillar pillar) =>
        Run(() => _host.PillarOff(pillar, EventStore.Catalog.Current, EventRuntime.Engine, _game, Persistence.State));

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
        Run(() => { _host.After(read, _game, DateTime.UtcNow); return 0; });
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
    internal static ApocalypseView View(string id) => Run(() => _host.View(id, _game));

    static T Run<T>(Func<T> work)
    {
        try { return work(); }
        catch (Exception ex)
        {
            Core.Log.LogError($"[nyar] apocalypse: {ex.Message}");
            return default;
        }
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

        public bool Walkable(float x, float z, byte level) =>
            Probe() is not { } probe || (probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level));

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

        public void Log(string line) => Core.Log.LogInfo($"[nyar] {line}");

        public void Warn(string line) => Core.Log.LogWarning($"[nyar] {line}");

        public void Announce(string eventId, string line) => Announcer.Apocalypse(eventId, line);
    }
}
