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
/// LifeTime counts (A10), LifeTime, DestroyWhenDisabled, a cleared DropTableBuffer and the unit marker, every structural
/// edit through EntityExtensions; a unit saves normally with its children, and the boot sweep despawns it (A9);</item>
/// <item>the spawn and despawn queues, drained once per tick within MaxSpawnsPerTick and MaxDespawnsPerTick;</item>
/// <item>the boot marker sweep, which queues every marked survivor of an earlier run for despawn (D21);</item>
/// <item>the tracked units in state.json, for the admin and the boot log;</item>
/// <item>the regroup check (event-library A23): once the game has snapped a unit to the terrain level under its ring
/// point, a unit on another level than its order's anchor is moved once to within 1 m of the anchor.</item>
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
    static readonly List<(long Key, (float X, float Y, float Z) Anchor, int Tries)> _regroup = new();

    internal static SpawnLedger Ledger => _ledger;

    /// <summary>True for a unit we spawned or found marked at boot: never a native NPC to empower (faction-empowerment D3).</summary>
    internal static bool IsTracked(long key) => _entities.ContainsKey(key);

    static long KeyOf(Entity e) => ((long)e.Index << 32) | (uint)e.Version;

    /// <summary>True for one of our units, read before <see cref="Died"/> forgets it (automation D11): in the ledger, or
    /// carrying our unit marker (never a carrier, Markers.IsOurs). Read-only; throws on a failed read, which the kill
    /// feed counts as a kill read failure.</summary>
    internal static bool IsOurs(Entity unit)
    {
        if (IsTracked(KeyOf(unit))) return true;
        if (!unit.Exists() || !Core.EntityManager.HasBuffer<BuffBuffer>(unit)) return false;
        var buffs = Core.EntityManager.GetBuffer<BuffBuffer>(unit);
        for (var i = 0; i < buffs.Length; i++)
            if (buffs[i].PrefabGuid == MarkerBuff && buffs[i].Entity.TryGetComponent<SpellLevel>(out var level) && Markers.IsOurs(level.Level))
                return true;
        return false;
    }

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
        _regroup.Clear();
        HuntAction.Initialize();                                             // event-spawns D33: a restart keeps no seed
        TerritoryMap.Initialize();                                           // and no kept map
    }

    /// <summary>Once at IsReady, through Logic/SweepPlan (faction-empowerment D7): every unit carrying our unit marker is a
    /// survivor of an earlier run (the ledger is empty at boot), so each is queued for despawn; every carrier buff is queued
    /// for removal with EmpowerAction, and its native unit is never touched. state.json's unit list is informational and is
    /// cleared.</summary>
    [Mutating]
    internal static void BootSweep()
    {
        var rows = MarkerRows();
        var plan = SweepPlan.From(rows.Select(r => new MarkerRow(r.Level, KeyOf(r.Buff), KeyOf(r.Target))));
        var byKey = rows.Select(r => r.Target).Where(t => t.Exists()).GroupBy(KeyOf).ToDictionary(g => g.Key, g => g.First());
        var queued = 0;
        var found = 0;
        foreach (var key in plan.Despawn)
        {
            if (!byKey.TryGetValue(key, out var unit)) continue;
            found++;
            _entities[key] = unit;
            if (_ledger.QueueDespawn(key)) queued++;
        }
        var carriers = EmpowerAction.QueueBootCarriers(plan.RemoveCarriers);
        var listed = Persistence.State.Document.Units.Count;
        if (listed > 0)
        {
            Persistence.State.Document.Units.Clear();
            Persistence.State.MarkDirty();
        }
        Core.Log.LogInfo($"[nyar] boot marker sweep: {found} found, {queued} queued for despawn ({listed} listed in state.json)");
        Core.Log.LogInfo($"[nyar] boot carrier sweep: {plan.RemoveCarriers.Count} found, {carriers} queued for removal");
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
        var life = SpawnLedger.Lifetime(DateTime.UtcNow, null, null, Settings.Limit(Limits.GraceSeconds),
            Settings.Limit(Limits.ManualSpawnLifetimeSeconds), DrainMargin());
        var result = _ledger.Request(prefab, null, count, life, tuning, i =>
        {
            var (x, z) = SpawnLedger.Around(at.x, at.z, PlaceRadius, i, count, angle);
            return (x, at.y, z);
        }, (at.x, at.y, at.z));
        if (result.Skipped is not null) Core.Log.LogWarning($"[nyar] spawn {prefab}: {result.Skipped}");
        return AdminLines.Spawned(result.Queued, prefab, result.Skipped);
    }

    /// <summary>A wave's units for event <paramref name="eventId"/>: <paramref name="count"/> of them at places
    /// <paramref name="first"/>.. of <paramref name="total"/> on a circle of <paramref name="radius"/> around
    /// <paramref name="center"/>. WaveAction has sized the wave already, so the ledger's own caps only guard. Each ring
    /// point is moved onto walkable ground by <see cref="WavePoints.Plan"/> with the wave's <paramref name="walk"/>
    /// (walkable-spawns D3), a point failing <paramref name="allowed"/> (out of scope, or claimed, D17) counting as blocked;
    /// each unit carries the event's <paramref name="tuning"/> (D9), <paramref name="loot"/> (D11) and
    /// <paramref name="hunt"/> tag (D13); with <paramref name="reach"/> a point needs a walkable line from the reach origin
    /// (automation A7, D32). The result carries the queued units' moved, shortened and unchecked counts.</summary>
    [Mutating]
    internal static (int Queued, int Moved, int Shortened, int Unchecked) RequestWave(string prefab, string eventId, int count, UnitLifetime life,
        float3 center, float radius, int first, int total, double angle, (float X, float Y, float Z)? anchor, WaveWalk walk,
        Func<float, float, bool> allowed = null, UnitTuning tuning = null, bool loot = false, HuntTag? hunt = null, WalkReach? reach = null)
    {
        var ring = new List<(float X, float Z)>(Math.Max(0, count));
        for (var i = 0; i < count; i++) ring.Add(SpawnLedger.Around(center.x, center.z, radius, first + i, total, angle));
        var points = WavePoints.Plan(ring, (center.x, center.z), radius, walk, allowed, reach);
        var result = _ledger.Request(prefab, eventId, count, life, tuning ?? UnitTuning.None, i => (points[i].X, center.y, points[i].Z), anchor, loot, hunt);
        if (result.Skipped is not null) Core.Log.LogWarning($"[nyar] event {eventId} {prefab}: {result.Skipped}");
        var queued = points.Take(result.Queued).ToList();
        var (moved, unchecked_) = WavePoints.Counts(queued);
        return (result.Queued, moved, WavePoints.Shortened(queued), unchecked_);
    }

    /// <summary>The despawn queue's worst-case drain time at the current caps, added to every unit's LifeTime (A16, A21).</summary>
    internal static int DrainMargin() =>
        SpawnLedger.DrainMarginSeconds(Settings.Limit(Limits.MaxTrackedUnits), Settings.Limit(Limits.MaxDespawnsPerTick));

    /// <summary>An event ended (stop, faults, or its grace ran out): its units spawned before <paramref name="spawnedBefore"/>
    /// are queued for despawn and, on a stop or fault, its waiting orders are cancelled (Business rules 2).</summary>
    [Mutating]
    internal static (int Queued, int Cancelled) EndEventUnits(string eventId, DateTime spawnedBefore, bool cancelOrders = true) =>
        _ledger.EndEvent(eventId, spawnedBefore, cancelOrders);

    /// <summary>The kill switch's unit half (D20): cancels waiting spawns and queues every tracked unit. EventRuntime.Purge
    /// ends the events and starts the cooldown.</summary>
    [Mutating]
    internal static (int Queued, int Cancelled) PurgeUnits() => _ledger.Purge();

    /// <summary>A unit died (Patches/DeathEventPatch). A tracked one leaves the ledger; anything else is ignored.</summary>
    internal static void Died(Entity unit)
    {
        var key = KeyOf(unit);
        if (!_ledger.Forget(key)) return;
        try { Left(key, "died"); }
        finally { Release(key); }
    }

    /// <summary>A66: with VerboseLogging, a tracked unit leaving the ledger outside a despawn says so. Never throws.</summary>
    static void Left(long key, string how)
    {
        try
        {
            if (Settings.VerboseLogging.Value && _stateUnits.TryGetValue(key, out var u)) Core.Log.LogInfo($"[nyar] {u.Prefab} of {u.EventId} {how}");
        }
        catch { /* a diagnostic line never stops the release */ }
    }

    /// <summary>The scheduler's first phase each second: spawn a batch, prune units the game removed, despawn a batch.
    /// EventScheduler flushes state.json at the end of the tick.</summary>
    [Mutating]
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
            // A failed recipe discards the unit (Abandon) and logs once per streak and event (event-spawns D21, D30); the
            // units already spawned stay tracked and on their due time.
            var streak = order.EventId ?? "manual";
            if (!unit.Exists())
            {
                _ledger.Fail(order);
                if (WalkCheck.Health.Failing(SpawnFailure.UnitSetup, streak)) Core.Log.LogWarning($"[nyar] spawn of {order.Prefab} failed: {error}");
                continue;
            }
            WalkCheck.Health.Recovered(SpawnFailure.UnitSetup, streak);
            var key = KeyOf(unit);
            if (!_ledger.Confirm(order, key, now))
            {
                Discard(unit);
                continue;
            }
            _entities[key] = unit;
            if (order.Hunt is { } hunt && order.EventId is { } huntEvent) HuntAction.Track(key, unit, huntEvent, hunt);
            var stateUnit = new StateUnit(order.EventId ?? "manual", order.Prefab, order.X, order.Z, now);
            _stateUnits[key] = stateUnit;
            Persistence.State.Document.Units.Add(stateUnit);
            Persistence.State.MarkDirty();
            spawned++;
            if (Settings.VerboseLogging.Value)
                Core.Log.LogInfo($"[nyar] spawned {order.Prefab} for {order.EventId ?? "manual"} (lifetime {order.LifetimeSeconds}s)");
            if (order.Anchor is { } a) _regroup.Add((key, a, 0));
        }
        if (spawns.Count > 0)
            Core.Log.LogInfo($"[nyar] spawn batch: {spawned} of {spawns.Count} spawned, {_ledger.PendingSpawns} waiting");
        Regroup();

        // Units the game removed on its own (LifeTime ran out, DestroyWhenDisabled) leave the ledger here.
        foreach (var gone in _ledger.Units.Where(u => !_entities.TryGetValue(u.Key, out var e) || !e.Exists()).Select(u => u.Key).ToList())
        {
            _ledger.Forget(gone);
            try { Left(gone, "removed by the game"); }
            finally { Release(gone); }
        }

        // Units past their due time join the budgeted queue here; LifeTime stays the backstop (A21).
        var due = _ledger.QueueDue(now);
        if (due > 0) Core.Log.LogInfo($"[nyar] {due} units due for despawn");

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
                if (Settings.VerboseLogging.Value && _stateUnits.TryGetValue(key, out var gone))
                    Core.Log.LogInfo($"[nyar] despawned {gone.Prefab} of {gone.EventId}");
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
    }

    /// <summary>A23: each spawned unit with an anchor, once the game's HeightCorrectionSystem has given it a terrain level
    /// (Height.ServerHeightLevel above 0, which it is not in its first frame; WavePlan.Step), is checked once against its
    /// anchor; a unit on another level is moved to WavePlan.RegroupPoint, a random point within 1 m of the anchor, where
    /// the game snaps it to the anchor's level, and its AggroConsumer.PreCombatPosition follows. Only tracked units are
    /// written (CLAUDE.md › Spawn &amp; buff safety). A failure skips that unit and never reaches the tick.</summary>
    static void Regroup()
    {
        for (var i = _regroup.Count - 1; i >= 0; i--)
        {
            var (key, anchor, tries) = _regroup[i];
            _regroup.RemoveAt(i);
            try
            {
                if (!_entities.TryGetValue(key, out var unit) || !unit.Exists()) continue;
                if (!unit.TryGetComponent<Height>(out var height) || !unit.TryGetComponent<Translation>(out var pos)) continue;
                var step = WavePlan.Step(height.ServerHeightLevel, pos.Value.y, anchor.Y, tries);
                if (step == RegroupStep.Wait) _regroup.Add((key, anchor, tries + 1));
                if (step != RegroupStep.Move) continue;
                var (tx, ty, tz) = WavePlan.RegroupPoint(anchor, _random.NextDouble() * 2 * Math.PI, _random.NextDouble());
                var to = new float3(tx, ty, tz);
                unit.Write(new Translation { Value = to });
                if (unit.Has<LastTranslation>()) unit.Write(new LastTranslation { Value = to });
                // The spot the AI walks back to after a fight, as Bloodcraft's familiar return does, so it does not path
                // back to the ring point on the other level.
                if (unit.TryGetComponent<AggroConsumer>(out var aggro))
                {
                    aggro.PreCombatPosition = to;
                    unit.Write(aggro);
                }
                if (Settings.VerboseLogging.Value)
                    Core.Log.LogInfo($"[nyar] regrouped {(_stateUnits.TryGetValue(key, out var su) ? su.Prefab : "unit")} from height {pos.Value.y:0.0} to its centre at height {anchor.Y:0.0}");
            }
            catch (Exception ex)
            {
                Core.Log.LogWarning($"[nyar] regroup check failed: {ex.Message}");
            }
        }
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
            var hasStats = unit.TryGetComponent<UnitStats>(out var stats);
            var power = hasStats ? stats.PhysicalPower._Value : 0f;
            var spell = hasStats ? stats.SpellPower._Value : 0f;
            var attack = unit.TryGetComponent<AbilityBar_Shared>(out var bar) ? bar.PrimaryAttackSpeed._Value : 0f;
            var move = unit.TryGetComponent<Movement>(out var movement) ? movement.Speed._Value : 0f;
            var flag = marked.Contains(unit) ? "" : " UNMARKED";
            var recipe = AdminLines.Recipe(unit.Has<LifeTime>(), unit.Has<Age>(), unit.Has<DestroyWhenDisabled>(),
                unit.Has<ProjectM.PersistenceV2.DontSaveEntity>());
            lines.Add((distance, AdminLines.DebugUnit(tracked.Prefab, tracked.EventId, left, level,
                (int)MathF.Round(health.Value), (int)MathF.Round(health.MaxHealth._Value), (int)MathF.Round(power),
                (int)MathF.Round(spell), move, attack, ((int)MathF.Round(pos.Value.x), (int)MathF.Round(pos.Value.z))) + " " + recipe + flag));
        }
        var report = AdminLines.DebugReport(lines.OrderBy(l => l.Distance).Select(l => l.Line).ToList(), radius);
        // faction-empowerment D14: then at most 10 native NPCs, read back from the live unit and its carrier.
        return [.. report, .. EmpowerAction.DebugNatives(at, radius, 10)];
    }

    static void Release(long key)
    {
        _entities.Remove(key);
        HuntAction.Forget(key);
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
            Discard(unit);                      // never leave a half-set unit behind
            throw;
        }
    }

    static Entity Prepare(Entity unit, SpawnOrder order, out string error)
    {
        error = null;
        // A22: the marker (the boot sweep's record) comes first and the LifeTime backstop is attempted even when marking
        // failed, so a unit that fails any step, and then fails to be destroyed, keeps at least one of the two. The
        // marker's stat modifiers and UnitSetup's level land in this frame, before the buff systems read either.
        var marked = TryMark(unit, order.Tuning, out var markError);
        var timed = TryTime(unit, order.LifetimeSeconds, out var timeError);
        if (!marked) return Abandon(unit, markError, out error);
        if (!timed) return Abandon(unit, timeError, out error);

        var position = new float3(order.X, order.Y, order.Z);
        if (unit.Has<Translation>()) unit.Write(new Translation { Value = position });
        if (unit.Has<LastTranslation>()) unit.Write(new LastTranslation { Value = position });
        if (!unit.AddComponentSafe<DestroyWhenDisabled>()) return Abandon(unit, "DestroyWhenDisabled could not be added", out error);
        // No DontSaveEntity (A9): it kept the unit out of the save but not its child entities, which came back as orphans.
        // The drop table goes unless the event keeps its loot (event-spawns D11).
        var before = unit.Has<DropTableBuffer>() ? Core.EntityManager.GetBuffer<DropTableBuffer>(unit).Length : 0;
        if (order.ClearDrops && unit.Has<DropTableBuffer>()) Core.EntityManager.GetBuffer<DropTableBuffer>(unit).Clear();
        if (Settings.VerboseLogging.Value)
        {
            var after = unit.Has<DropTableBuffer>() ? Core.EntityManager.GetBuffer<DropTableBuffer>(unit).Length : 0;
            Core.Log.LogInfo($"[nyar] drops {order.Prefab}: {before} before, {after} after setup");
        }

        UnitSetup.Apply(unit, order.Tuning);
        return unit;
    }

    /// <summary>The LifeTime backstop: LifeTime with Destroy at its end, and Age, without which it never counts down (A10).
    /// Never throws.</summary>
    static bool TryTime(Entity unit, int lifetimeSeconds, out string error)
    {
        error = null;
        try
        {
            if (!unit.AddComponentSafe<LifeTime>()) { error = "LifeTime could not be added"; return false; }
            unit.Write(new LifeTime { Duration = lifetimeSeconds, EndAction = LifeTimeEndAction.Destroy });
            if (!unit.AddComponentSafe<Age>()) { error = "Age could not be added"; return false; }
            unit.Write(new Age { Value = 0f });
            return true;
        }
        catch (Exception ex)
        {
            error = $"LifeTime could not be set: {ex.Message}";
            return false;
        }
    }

    static Entity Abandon(Entity unit, string why, out string error)
    {
        error = why;
        Discard(unit);
        return Entity.Null;
    }

    /// <summary>Destroys a unit that failed its recipe or lost its order. Never throws. A unit that cannot be
    /// destroyed now is queued as a survivor (holding its MaxTrackedUnits slot) and retried each tick, so no spawned
    /// entity is ever left outside the ledger.</summary>
    static void Discard(Entity unit)
    {
        try
        {
            if (!unit.Exists() || unit.DestroySafe()) return;
        }
        catch (Exception ex)
        {
            if (_destroyFaults.Fail()) Core.Log.LogError($"[nyar] discarding a failed spawn failed: {ex.Message}; queued for despawn");
        }
        try
        {
            if (!unit.Exists()) return;
            var key = KeyOf(unit);
            _entities[key] = unit;
            _ledger.QueueDespawn(key);
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[nyar] a failed spawn could not be queued for despawn: {ex.Message}; its LifeTime or the boot sweep removes it");
        }
    }

    /// <summary>The unit marker: our own buff on the unit, made inert, living as long as the unit, carrying
    /// SpellLevel.Level = Markers.Unit so the boot sweep finds it after a restart (spikes S2), and the unit's Health and
    /// PhysicalPower multipliers (A7).</summary>
    static bool TryMark(Entity unit, UnitTuning tuning, out string error)
    {
        error = null;
        try
        {
            if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(unit, unit, MarkerBuff, out Entity buff) || !buff.Exists())
            {
                error = "marker buff could not be applied";
                return false;
            }
            // The sweep's record first (A22), so a marker that fails a later step still makes the unit findable.
            if (!buff.AddComponentSafe<SpellLevel>()) { error = "SpellLevel could not be added to the marker"; return false; }
            buff.Write(new SpellLevel { Level = Markers.Unit });
            buff.RemoveComponentSafe<CreateGameplayEventsOnSpawn>();
            buff.RemoveComponentSafe<GameplayEventListeners>();
            buff.RemoveComponentSafe<RemoveBuffOnGameplayEvent>();
            buff.RemoveComponentSafe<RemoveBuffOnGameplayEventEntry>();
            buff.RemoveComponentSafe<DestroyOnGameplayEvent>();
            // The potion's own stat bonus is replaced by the requested multipliers (none by default), A7.
            if (!UnitSetup.StatModifiers(buff, tuning)) { error = "stat modifiers could not be set on the marker"; return false; }
            if (buff.Has<LifeTime>()) buff.Write(new LifeTime { Duration = 0f, EndAction = LifeTimeEndAction.None });
            return true;
        }
        catch (Exception ex)
        {
            error = $"marker could not be set: {ex.Message}";
            return false;
        }
    }

    /// <summary>Units whose marker buff carries our unit marker (never a carrier, Markers.IsOurs).</summary>
    static HashSet<Entity> MarkedUnits() =>
        MarkerRows().Where(r => Markers.IsOurs(r.Level) && r.Target.Exists()).Select(r => r.Target).ToHashSet();

    /// <summary>Every buff whose SpellLevel is one of Markers.All, with its target, from an IncludeDisabled |
    /// IncludeSpawnTag query (a buff made this frame still has SpawnTag).</summary>
    static List<(float Level, Entity Buff, Entity Target)> MarkerRows()
    {
        var result = new List<(float, Entity, Entity)>();
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
                    var level = buff.Read<SpellLevel>().Level;
                    if (Markers.KindOf(level) == MarkerKind.None) continue;
                    result.Add((level, buff, buff.Read<Buff>().Target));
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
