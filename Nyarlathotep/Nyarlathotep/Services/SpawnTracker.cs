using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// Every unit Nyarlathotep spawns goes through here (foundation D16, D27; CLAUDE.md › Spawn &amp; buff safety). The
/// bookkeeping is Logic/SpawnLedger; this class does the game side:
/// <list type="bullet">
/// <item>the spawn recipe (spikes S2, RESEARCH_NOTES › Spike contracts): InstantiateEntityImmediate, then Age = 0 so
/// LifeTime counts (A10), LifeTime, DestroyWhenDisabled, DontSaveEntity, a cleared DropTableBuffer and the unit marker,
/// every structural edit through EntityExtensions;</item>
/// <item>the spawn and despawn queues, drained once per tick within MaxSpawnsPerTick and MaxDespawnsPerTick;</item>
/// <item>the boot marker sweep, which queues every marked survivor of an earlier run for despawn (D21);</item>
/// <item>the tracked units in state.json, for the admin and the boot log.</item>
/// </list>
/// </summary>
internal static class SpawnTracker
{
    static readonly PrefabGUID MarkerBuff = new(Markers.MarkerBuffGuid);
    const float PlaceRadius = 3f;

    static SpawnLedger _ledger = new(new LedgerLimits(1, 1, 1, 1));
    static readonly Dictionary<long, Entity> _entities = new();
    static readonly Dictionary<long, StateUnit> _stateUnits = new();
    static readonly System.Random _random = new();
    static readonly FailureStreak _destroyFaults = new();

    internal static SpawnLedger Ledger => _ledger;

    static long KeyOf(Entity e) => ((long)e.Index << 32) | (uint)e.Version;

    /// <summary>Third in Core.TryInitialize, after EventStore: the ledger with the loaded limits.</summary>
    internal static void Initialize()
    {
        _ledger = new SpawnLedger(new LedgerLimits(
            Settings.Limit(Limits.MaxTrackedUnits),
            Settings.Limit(Limits.MaxUnitsPerWave),
            Settings.Limit(Limits.MaxSpawnsPerTick),
            Settings.Limit(Limits.MaxDespawnsPerTick)));
        _entities.Clear();
        _stateUnits.Clear();
    }

    /// <summary>Once at IsReady: every unit carrying one of our markers is a survivor of an earlier run (the ledger is
    /// empty at boot), so each is queued for despawn. state.json's unit list is informational and is cleared.</summary>
    internal static void BootSweep()
    {
        var marked = MarkedUnits();
        foreach (var unit in marked)
        {
            var key = KeyOf(unit);
            _entities[key] = unit;
            _ledger.QueueDespawn(key);
        }
        var listed = Persistence.State.Document.Units.Count;
        if (listed > 0)
        {
            Persistence.State.Document.Units.Clear();
            Persistence.State.MarkDirty();
        }
        Core.Log.LogInfo($"[nyar] boot sweep: {marked.Count} marked units queued for despawn ({listed} listed in state.json)");
    }

    /// <summary>`.nyar spawn`: queues <paramref name="count"/> units of <paramref name="prefab"/> around
    /// <paramref name="at"/>, each living ManualSpawnLifetimeSeconds. The reply is "spawned &lt;k&gt; &lt;unit&gt;",
    /// with "skipped by &lt;cap&gt;" when a cap cut it, or the control that blocked it.</summary>
    [Mutating]
    internal static string SpawnManual(string prefab, int count, UnitTuning tuning, float3 at)
    {
        if (!Settings.Enabled.Value) return "General.Enabled is false";
        if (Persistence.State.Document.PurgeUntilUtc is { } until && until > DateTime.UtcNow) return "purge cooldown active";
        var catalog = new EventStore.PrefabUnitCatalog();
        if (!prefab.StartsWith("CHAR_", StringComparison.Ordinal) || !catalog.IsKnown(prefab)) return "unit must be a known CHAR_ unit name";
        if (UnitDenyList.IsDenied(prefab) || catalog.IsDenied(prefab)) return $"unit {prefab} is deny-listed";

        var angle = _random.NextDouble() * 2 * Math.PI;
        var lifetime = SpawnLedger.LifetimeSeconds(DateTime.UtcNow, null, null, Settings.Limit(Limits.GraceSeconds),
            Settings.Limit(Limits.ManualSpawnLifetimeSeconds));
        var result = _ledger.Request(prefab, null, count, lifetime, tuning, i =>
        {
            var (x, z) = SpawnLedger.Around(at.x, at.z, PlaceRadius, i, count, angle);
            return (x, at.y, z);
        });
        if (result.Skipped is not null) Core.Log.LogWarning($"[nyar] spawn {prefab}: {result.Skipped}");
        return AdminLines.Spawned(result.Queued, prefab, result.Skipped);
    }

    /// <summary>The kill switch (D20): ends every running event, cancels waiting spawns, queues every tracked unit and
    /// starts the PurgeCooldownSeconds window. Step 5 moves the event half to EventRuntime.</summary>
    [Mutating]
    internal static string Purge()
    {
        var events = EventStore.Catalog.Running.Count;
        EventStore.Catalog.EndAll();
        var (queued, cancelled) = _ledger.Purge();
        var cooldown = Settings.Limit(Limits.PurgeCooldownSeconds);
        Persistence.State.Document.Instances.Clear();
        Persistence.State.Document.PurgeUntilUtc = DateTime.UtcNow.AddSeconds(cooldown);
        Persistence.State.MarkDirty();
        Core.Log.LogWarning($"[nyar] purge: {events} events ended, {queued} units queued, {cancelled} spawns cancelled, cooldown {cooldown}s");
        return AdminLines.Purged(events, queued);
    }

    /// <summary>A unit died (Patches/DeathEventPatch). A tracked one leaves the ledger; anything else is ignored.</summary>
    internal static void Died(Entity unit)
    {
        var key = KeyOf(unit);
        if (_ledger.Forget(key)) Release(key);
    }

    /// <summary>One tick (1 s): spawn a batch, prune units the game removed, despawn a batch, flush state.json.</summary>
    internal static void Tick()
    {
        var now = DateTime.UtcNow;

        var spawns = _ledger.TakeSpawns();
        var spawned = 0;
        foreach (var order in spawns)
        {
            Entity unit;
            string error;
            try { unit = Spawn(order, out error); }
            catch (Exception ex) { unit = Entity.Null; error = ex.Message; }
            if (!unit.Exists())
            {
                _ledger.Fail(order);
                Core.Log.LogWarning($"[nyar] spawn of {order.Prefab} failed: {error}");
                continue;
            }
            var key = KeyOf(unit);
            if (!_ledger.Confirm(order, key, now))
            {
                unit.DestroySafe();
                continue;
            }
            _entities[key] = unit;
            var stateUnit = new StateUnit(order.EventId ?? "manual", order.Prefab, order.X, order.Z, now);
            _stateUnits[key] = stateUnit;
            Persistence.State.Document.Units.Add(stateUnit);
            Persistence.State.MarkDirty();
            spawned++;
        }
        if (spawns.Count > 0)
            Core.Log.LogInfo($"[nyar] spawn batch: {spawned} of {spawns.Count} spawned, {_ledger.PendingSpawns} waiting");

        // Units the game removed on its own (LifeTime ran out, DestroyWhenDisabled) leave the ledger here.
        foreach (var gone in _ledger.Units.Where(u => !_entities.TryGetValue(u.Key, out var e) || !e.Exists()).Select(u => u.Key).ToList())
        {
            _ledger.Forget(gone);
            Release(gone);
        }

        var despawns = _ledger.TakeDespawns();
        var destroyed = 0;
        var retried = 0;
        foreach (var key in despawns)
        {
            if (!_entities.TryGetValue(key, out var unit) || !unit.Exists()) { Release(key); continue; }
            bool ok;
            try { ok = unit.DestroySafe(); }
            catch (Exception ex)
            {
                ok = false;
                if (_destroyFaults.Fail()) Core.Log.LogError($"[nyar] despawn failed: {ex.Message}; the unit stays queued");
            }
            if (ok)
            {
                _destroyFaults.Ok();
                destroyed++;
                Release(key);
            }
            else
            {
                // Still alive: back in the queue for a later tick, holding its MaxTrackedUnits slot, never dropped.
                _ledger.QueueDespawn(key);
                retried++;
            }
        }
        if (despawns.Count > 0)
            Core.Log.LogInfo($"[nyar] despawn batch: {destroyed} of {despawns.Count} destroyed, {retried} requeued, {_ledger.PendingDespawns} left");

        Persistence.State.Flush();
    }

    /// <summary>`.nyar debug here`: one line per tracked unit within <paramref name="radius"/> m of
    /// <paramref name="at"/>, nearest first.</summary>
    internal static IReadOnlyList<string> DebugHere(float3 at, int radius)
    {
        var lines = new List<(float Distance, string Line)>();
        var marked = MarkedUnits();
        foreach (var tracked in _ledger.Units)
        {
            if (!_entities.TryGetValue(tracked.Key, out var unit) || !unit.TryGetComponent<Translation>(out var pos)) continue;
            var distance = math.distance(pos.Value.xz, at.xz);
            if (distance > radius) continue;
            int? left = unit.TryGetComponent<LifeTime>(out var life)
                ? (int)(life.Duration - (unit.TryGetComponent<Age>(out var age) ? age.Value : 0f))
                : null;
            var level = unit.TryGetComponent<UnitLevel>(out var ul) ? ul.Level._Value : 0;
            var health = unit.TryGetComponent<Health>(out var h) ? h : default;
            var power = unit.TryGetComponent<UnitStats>(out var stats) ? stats.PhysicalPower._Value : 0f;
            var flag = marked.Contains(unit) ? "" : " UNMARKED";
            lines.Add((distance, AdminLines.DebugUnit(tracked.Prefab, tracked.EventId, left, level,
                (int)MathF.Round(health.Value), (int)MathF.Round(health.MaxHealth._Value), (int)MathF.Round(power)) + flag));
        }
        return AdminLines.DebugReport(lines.OrderBy(l => l.Distance).Select(l => l.Line).ToList(), radius);
    }

    static void Release(long key)
    {
        _entities.Remove(key);
        if (_stateUnits.Remove(key, out var stateUnit))
        {
            Persistence.State.Document.Units.Remove(stateUnit);
            Persistence.State.MarkDirty();
        }
    }

    /// <summary>Spawns one order with the full recipe. Returns Entity.Null (and why) when any step fails; a half-set
    /// unit is destroyed.</summary>
    static Entity Spawn(SpawnOrder order, out string error)
    {
        error = null;
        if (!Core.PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary.TryGetValue(order.Prefab, out var guid))
        {
            error = "unknown prefab";
            return Entity.Null;
        }
        var unit = Core.ServerGameManager.InstantiateEntityImmediate(Entity.Null, guid);
        if (!unit.Exists()) { error = "the game returned no entity"; return Entity.Null; }
        try { return Prepare(unit, order, out error); }
        catch
        {
            unit.DestroySafe();                 // never leave a half-set unit behind
            throw;
        }
    }

    static Entity Prepare(Entity unit, SpawnOrder order, out string error)
    {
        error = null;
        var position = new float3(order.X, order.Y, order.Z);
        if (unit.Has<Translation>()) unit.Write(new Translation { Value = position });
        if (unit.Has<LastTranslation>()) unit.Write(new LastTranslation { Value = position });

        if (!unit.AddComponentSafe<LifeTime>()) return Abandon(unit, "LifeTime could not be added", out error);
        unit.Write(new LifeTime { Duration = order.LifetimeSeconds, EndAction = LifeTimeEndAction.Destroy });
        // A10: an immediate spawn has no Age, and without it LifeTime never counts down.
        if (!unit.AddComponentSafe<Age>()) return Abandon(unit, "Age could not be added", out error);
        unit.Write(new Age { Value = 0f });
        if (!unit.AddComponentSafe<DestroyWhenDisabled>()) return Abandon(unit, "DestroyWhenDisabled could not be added", out error);
        if (!unit.AddComponentSafe<ProjectM.PersistenceV2.DontSaveEntity>()) return Abandon(unit, "DontSaveEntity could not be added", out error);
        if (unit.Has<DropTableBuffer>()) Core.EntityManager.GetBuffer<DropTableBuffer>(unit).Clear();

        UnitSetup.Apply(unit, order.Tuning);

        if (!TryMark(unit, out error)) return Abandon(unit, error, out error);
        return unit;
    }

    static Entity Abandon(Entity unit, string why, out string error)
    {
        error = why;
        unit.DestroySafe();
        return Entity.Null;
    }

    /// <summary>The unit marker: our own buff on the unit, made inert, living as long as the unit, carrying
    /// SpellLevel.Level = Markers.Unit so the boot sweep finds it after a restart (spikes S2).</summary>
    static bool TryMark(Entity unit, out string error)
    {
        error = null;
        if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(unit, unit, MarkerBuff, out Entity buff) || !buff.Exists())
        {
            error = "marker buff could not be applied";
            return false;
        }
        buff.RemoveComponentSafe<CreateGameplayEventsOnSpawn>();
        buff.RemoveComponentSafe<GameplayEventListeners>();
        buff.RemoveComponentSafe<RemoveBuffOnGameplayEvent>();
        buff.RemoveComponentSafe<RemoveBuffOnGameplayEventEntry>();
        buff.RemoveComponentSafe<DestroyOnGameplayEvent>();
        if (buff.Has<ModifyUnitStatBuff_DOTS>()) Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(buff).Clear();
        if (buff.Has<LifeTime>()) buff.Write(new LifeTime { Duration = 0f, EndAction = LifeTimeEndAction.None });
        if (!buff.AddComponentSafe<SpellLevel>()) { error = "SpellLevel could not be added to the marker"; return false; }
        buff.Write(new SpellLevel { Level = Markers.Unit });
        return true;
    }

    /// <summary>Units whose marker buff carries one of our values, from an IncludeDisabled | IncludeSpawnTag query (a
    /// buff made this frame still has SpawnTag).</summary>
    static HashSet<Entity> MarkedUnits()
    {
        var result = new HashSet<Entity>();
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(Il2CppType.Of<Buff>()), ComponentType.ReadOnly(Il2CppType.Of<SpellLevel>()) },
            Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag
        });
        try
        {
            var buffs = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buff in buffs)
                {
                    if (!Markers.IsOurs(buff.Read<SpellLevel>().Level)) continue;
                    var target = buff.Read<Buff>().Target;
                    if (target.Exists()) result.Add(target);
                }
            }
            finally
            {
                buffs.Dispose();
            }
        }
        finally
        {
            query.Dispose();
        }
        return result;
    }
}
