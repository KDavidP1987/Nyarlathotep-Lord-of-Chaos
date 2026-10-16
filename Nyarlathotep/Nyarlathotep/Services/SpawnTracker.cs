using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Pathfinding;
using ProjectM.Shared;
using ProjectM.Tiles;
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
        _arena.Clear();                                                      // swarm D4: a restart keeps no arena streak
        _region.Clear();                                                     // hunting-grounds D12: nor a last inside spot, target or streak
        _meta.Clear();
        _groups.Clear();
        _watch.Clear();                                                      // unit-watch D11: a restart keeps no watch row, try, clock or streak
        _landed.Clear();
        _spawnSpot.Clear();
        _fight.Ok();
        HuntAction.Initialize();                                            // event-spawns D33: a restart keeps no seed
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
    /// (automation A7, D32). The result carries the queued units' moved, shortened, spot-only (A16) and unchecked counts, and each unchecked
    /// unit's reason (A11). <paramref name="wave"/> is the 1-based wave each order and unit carries (wave-sets D5).</summary>
    [Mutating]
    internal static (int Queued, int Moved, int Shortened, int SpotOnly, int Unchecked, List<UncheckedReason> Why) RequestWave(string prefab, string eventId, int count, UnitLifetime life,
        float3 center, float radius, int first, int total, double angle, (float X, float Y, float Z)? anchor, WaveWalk walk,
        Func<float, float, bool> allowed = null, UnitTuning tuning = null, bool loot = false, HuntTag? hunt = null, WalkReach? reach = null,
        int wave = 0)
    {
        var ring = new List<(float X, float Z)>(Math.Max(0, count));
        for (var i = 0; i < count; i++) ring.Add(SpawnLedger.Around(center.x, center.z, radius, first + i, total, angle));
        var reasons = new List<UncheckedReason?>(ring.Count);
        var points = WavePoints.Plan(ring, (center.x, center.z), radius, walk, allowed, reach, reasons);
        var result = _ledger.Request(prefab, eventId, count, life, tuning ?? UnitTuning.None, i => (points[i].X, center.y, points[i].Z), anchor, loot, hunt, wave);
        if (result.Skipped is not null) Core.Log.LogWarning($"[nyar] event {eventId} {prefab}: {result.Skipped}");
        var queued = points.Take(result.Queued).ToList();
        var (moved, unchecked_) = WavePoints.Counts(queued);
        var why = reasons.Take(result.Queued).OfType<UncheckedReason>().ToList();
        return (result.Queued, moved, WavePoints.Shortened(queued), WavePoints.SpotOnly(queued), unchecked_, why);
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

    /// <summary>The waiting orders of one event a purge would cancel (apocalypse D15).</summary>
    internal static int WaitingOrders(string eventId) => _ledger.Waiting(eventId);

    /// <summary>The event, wave and spawn time of a tracked unit (wave-sets D13), null for anything else; read before
    /// <see cref="Died"/> forgets it.</summary>
    internal static OurUnit? EventOf(Entity unit) => _ledger.EventOf(KeyOf(unit));

    /// <summary>A unit died (Patches/DeathEventPatch). A tracked one leaves the ledger; anything else is ignored. With
    /// VerboseLogging the line names <paramref name="killer"/> (wave-sets A6: units died in batches with no credit).</summary>
    internal static void Died(Entity unit, Entity killer = default)
    {
        var key = KeyOf(unit);
        var tracked = _ledger.Find(key);                                     // read before Forget, for the watch's stall clock
        if (!_ledger.Forget(key)) return;
        WatchDied(key, tracked);
        if (tracked is not null) Early(tracked, "died" + KillerText(killer));
        try { Left(key, Settings.VerboseLogging.Value ? "died" + KillerText(killer) : "died"); }
        finally { Departed(key, UnitDeparture.Died); Release(key); }
    }

    /// <summary>" (killer &lt;prefab&gt;[ player][, owner &lt;prefab&gt;[ player]])" for the died line (wave-sets A6), or
    /// " (killer unknown)"; never a name. Never throws.</summary>
    static string KillerText(Entity killer)
    {
        try
        {
            if (!killer.Exists()) return " (killer unknown)";
            static string One(Entity e) => e.GetPrefabGuid().GetPrefabName() + (e.Has<PlayerCharacter>() ? " player" : "");
            var text = " (killer " + One(killer);
            if (killer.TryGetComponent<EntityOwner>(out var owner) && owner.Owner.Exists()) text += ", owner " + One(owner.Owner);
            return text + ")";
        }
        catch { return " (killer unreadable)"; }
    }

    static readonly EarlyDepartureLog _early = new();

    /// <summary>The early-departure diagnostic (Logic/EarlyDepartureLog): logged whatever VerboseLogging says. Never throws.</summary>
    static void Early(TrackedUnit unit, string how)
    {
        try
        {
            if (_early.Line(unit, how, DateTime.UtcNow) is { } line) Core.Log.LogInfo($"[nyar] {line}");
        }
        catch { /* a diagnostic line never stops the release */ }
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
                ApocalypseAction.Settled(order.EventId, order.Wave, false);
                if (WalkCheck.Health.Failing(SpawnFailure.UnitSetup, streak)) Core.Log.LogWarning($"[nyar] spawn of {order.Prefab} failed: {error}");
                continue;
            }
            WalkCheck.Health.Recovered(SpawnFailure.UnitSetup, streak);
            var key = KeyOf(unit);
            if (!_ledger.Confirm(order, key, now))
            {
                Discard(unit);
                ApocalypseAction.Settled(order.EventId, order.Wave, false);
                continue;
            }
            ApocalypseAction.Settled(order.EventId, order.Wave, true);
            _entities[key] = unit;
            if (order.Hunt is { } hunt && order.EventId is { } huntEvent) HuntAction.Track(key, unit, huntEvent, hunt);
            if (order.EventId is { } ev) _meta[key] = new UnitMeta(order.Wave, GroupOf(ev, order), (order.X, order.Y, order.Z));   // S-14
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
        foreach (var goneUnit in _ledger.Units.Where(u => !_entities.TryGetValue(u.Key, out var e) || !e.Exists()).ToList())
        {
            var gone = goneUnit.Key;
            _ledger.Forget(gone);
            Early(goneUnit, "removed by the game");
            try { Left(gone, "removed by the game"); }
            finally { Departed(gone, UnitDeparture.Removed); Release(gone); }
        }

        // Units past their due time join the budgeted queue here; LifeTime stays the backstop (A21).
        var due = _ledger.QueueDue(now);
        if (due > 0) Core.Log.LogInfo($"[nyar] {due} units due for despawn");

        var despawns = _ledger.TakeDespawns();
        var destroyed = 0;
        var retried = 0;
        foreach (var key in despawns)
        {
            if (!_entities.TryGetValue(key, out var unit) || !unit.Exists()) { Departed(key, UnitDeparture.Despawned); Release(key); continue; }
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
                Departed(key, UnitDeparture.Despawned);
                Release(key);
            }
            else
            {
                // Still alive: back in the queue for a later tick, holding its MaxTrackedUnits slot, never dropped.
                _ledger.RetryDespawn(key);                                            // keeps its event and wave
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
                Landed(unit, to, regroup: true);                             // unit-watch D34: the still check starts here
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

    // ---- the arena phase (swarm D3, D4) ----

    static readonly ArenaPhase _arena = new();
    static readonly ArenaWorld _arenaWorld = new();

    /// <summary>The arena check's health entry ("arena: check failing", swarm D4), for HealthMonitor and `.nyar status`.</summary>
    internal static IReadOnlyList<string> ArenaHealth => _arena.Health;

    /// <summary>An end path of <paramref name="eventId"/>: its arena streak ends with it (swarm D4, Review 2 F3).</summary>
    internal static void ArenaEnded(string eventId) => _arena.EventEnded(eventId);

    /// <summary>The scheduler's arena phase (swarm D3): every Arena.CheckSeconds while a running instance has an arena,
    /// that instance's own strays are returned; the logic and its guards are Logic/ArenaPhase, which never throws.</summary>
    [Mutating]
    internal static void ArenaTick(DateTime now) =>
        _arena.Tick(now, EventRuntime.Engine.Active, _arenaWorld, _random.NextDouble,
            line => Core.Log.LogInfo($"[nyar] {line}"), line => Core.Log.LogError($"[nyar] {line}"));

    /// <summary>The game side of the arena check. Reads only the ledger's own units; writes only units in
    /// <see cref="_entities"/> (CLAUDE.md › Spawn &amp; buff safety), which Logic/ArenaPhase has already filtered to the
    /// running instance's own (Arena.Ours).</summary>
    sealed class ArenaWorld : IArenaWorld
    {
        public IEnumerable<TrackedUnit> Units => _ledger.Units;

        public ArenaUnit? Read(TrackedUnit t)
        {
            if (!_entities.TryGetValue(t.Key, out var unit) || !unit.Exists() || !unit.TryGetComponent<Translation>(out var pos)) return null;
            (float X, float Z)? target = null;
            // The game's current aggro target, when it has one with a position: a cheap read of one component.
            if (unit.TryGetComponent<AggroConsumer>(out var aggro) && aggro.AggroTarget._Entity is var e && e.Exists()
                && e.TryGetComponent<Translation>(out var tp))
                target = (tp.Value.x, tp.Value.z);
            return new ArenaUnit(t.Key, t.EventId, t.SpawnedUtc, pos.Value.x, pos.Value.y, pos.Value.z, target);
        }

        public void Return(ArenaReturn r, (float X, float Z) centre, int arenaRadius, (float X, float Y, float Z)? anchor)
        {
            if (!_entities.TryGetValue(r.Key, out var unit) || !unit.Exists()) return;
            // Targets first (Codex step 3 F3): a throw here leaves the unit outside, so the next check retries the whole
            // return instead of leaving a moved unit still set on a player outside.
            // Every entry on a player outside the arena goes, whatever the current target (code review F1); entries inside stay.
            DropTargets(r.Key, unit, centre, arenaRadius);
            var to = new float3(r.X, r.Y, r.Z);
            unit.Write(new Translation { Value = to });
            if (unit.Has<LastTranslation>()) unit.Write(new LastTranslation { Value = to });
            Landed(unit, to);                                                // unit-watch D34
            // The spot the AI walks back to after a fight, as the regroup and Bloodcraft's familiar return set it, so the
            // unit does not path back out to where it stood.
            if (unit.TryGetComponent<AggroConsumer>(out var aggro))
            {
                aggro.PreCombatPosition = to;
                unit.Write(aggro);
            }
            // The game snaps the unit to a terrain level near the anchor's height; one on another level is moved to the
            // anchor by the existing regroup check (A23). A Point stored without a height has no anchor and no regroup.
            if (anchor is { } a) _regroup.Add((r.Key, a, 0));
        }

        /// <summary>Removes the returned unit's AggroBuffer entries on players outside the arena, so it does not walk
        /// straight back out (swarm D3); entries on players inside, on NPCs and on unreadable entities stay, for the game
        /// to keep or clear (Codex step 3 F2). A removed entry HuntAction seeded leaves its seed
        /// record too, keeping the record consistent with the buffer (HuntSeeds.Removed). Our own unit only.</summary>
        static void DropTargets(long key, Entity unit, (float X, float Z) centre, int arenaRadius)
        {
            if (!Core.EntityManager.HasBuffer<AggroBuffer>(unit)) return;
            var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                var e = buffer[i].Entity;
                if (!e.Exists() || !e.Has<PlayerCharacter>() || !e.TryGetComponent<Translation>(out var t)) continue;
                (float X, float Z)? at = (t.Value.x, t.Value.z);
                if (!Arena.DropsEntry(centre, arenaRadius, at)) continue;
                buffer.RemoveAt(i);
                HuntAction.Seeds.Removed(key, KeyOf(e));
            }
        }
    }

    // ---- the region phase: the lock and the hunt (hunting-grounds D5-D9, D12) ----

    static readonly RegionPhase _region = new();
    static readonly RegionWorld _regionWorld = new();

    /// <summary>What a tracked unit of an event carries for the lock and the hunt (S-14): its group within the instance,
    /// told apart by the group's hunt centre (one per Point or Admin wave, one per AroundPlayer group, one per Region
    /// spot; 0 without a hunt), and its spawn point, the lock's first inside spot. Memory only; it leaves with the unit.</summary>
    readonly record struct UnitMeta(int Wave, int Group, (float X, float Y, float Z) Spawn);

    static readonly Dictionary<long, UnitMeta> _meta = new();
    static readonly Dictionary<(string EventId, int Wave, float X, float Z), int> _groups = new();
    static int _lastGroup;                                                   // only grows, so a number is never handed out twice

    /// <summary>The group number of an order (S-14): one per event, wave and hunt centre (WaveAction gives every group of a
    /// Hunt or RegionHunt wave its own centre); 0 for an order without a hunt tag.</summary>
    static int GroupOf(string eventId, SpawnOrder order)
    {
        if (order.Hunt is not { } tag) return 0;
        var key = (eventId, order.Wave, tag.X, tag.Z);
        if (!_groups.TryGetValue(key, out var group)) _groups[key] = group = ++_lastGroup;
        return group;
    }

    /// <summary>The two health entries ("region lock: check failing", "region hunt: move failing"; hunting-grounds D6, D9),
    /// each while failing, for HealthMonitor and `.nyar status`.</summary>
    internal static IReadOnlyList<string> RegionHealth => _region.Health;

    /// <summary>An end path of <paramref name="eventId"/> (D12): its last inside spots, targets, group numbers and streaks
    /// go with it, and its health entries clear.</summary>
    internal static void RegionEnded(string eventId)
    {
        _region.EventEnded(eventId);
        foreach (var key in _groups.Keys.Where(k => k.EventId == eventId).ToList()) _groups.Remove(key);
    }

    /// <summary>`purge confirm` (D12): every record of the region phase goes.</summary>
    internal static void RegionPurged()
    {
        _region.Clear();
        _groups.Clear();
    }

    /// <summary>The scheduler's region phase (hunting-grounds D5, D7, D8), right after the arena: every
    /// HuntingGrounds.CheckSeconds while a locked or RegionHunt instance runs, the lock returns that instance's own strays,
    /// then the hunt jumps its own far units toward their targets. The logic and its guards are Logic/RegionPhase, which
    /// never throws; the tick's walk probe is disposed here whatever happens.</summary>
    static RegionParts? _regionParts;

    /// <summary>The last region tick's parts, once (Session 2 diagnostic); null when no tick ran since.</summary>
    internal static RegionParts? TakeRegionParts()
    {
        var p = _regionParts;
        _regionParts = null;
        return p;
    }

    [Mutating]
    internal static void RegionTick(DateTime now)
    {
        _regionWorld.Begin();
        var (netGcs, il2cppGcs) = (GC.CollectionCount(0), HuntAction.Il2CppGcCount());
        try
        {
            _region.Hunt.Detail = Settings.VerboseLogging.Value ? line => Core.Log.LogInfo($"[nyar] {line}") : null;   // why units wait
            _region.Tick(now, EventRuntime.Engine.Active, _regionWorld, _random.NextDouble,
                line => Core.Log.LogInfo($"[nyar] {line}"), line => Core.Log.LogError($"[nyar] {line}"));
            if (Settings.VerboseLogging.Value && _regionWorld.WalkLine() is { } walk) Core.Log.LogInfo($"[nyar] {walk}");   // why landings fail
        }
        finally
        {
            try { _regionParts = _regionWorld.Parts(GC.CollectionCount(0) - netGcs, HuntAction.Il2CppGcDelta(il2cppGcs, HuntAction.Il2CppGcCount())); }
            catch { /* a diagnostic never breaks the tick */ }
            _regionWorld.End();
        }
    }

    /// <summary>A unit's height level: the game's own once it has snapped the unit (Height.ServerHeightLevel above 0,
    /// WavePlan.Step), else the level of its height; 0 for a height outside the map's range.</summary>
    static byte LevelOf(Entity unit, float y) =>
        unit.TryGetComponent<Height>(out var h) && h.ServerHeightLevel > 0 ? (byte)Math.Clamp((int)h.ServerHeightLevel, 0, 255)
        : WalkHeight.Problem(y) is null ? TileLayerUtility.GetHeightLevel(y)
        : (byte)0;

    /// <summary>Moves one of our units at once, without a walk (hunting-grounds Session 0, the mover's method): Translation
    /// and LastTranslation, AggroConsumer.PreCombatPosition (the spot the AI returns to after a fight) and
    /// SpawnTransform.Position (without it the unit walks home), its PathBuffer emptied and its Velocity zeroed (without
    /// that it keeps walking its old path). Only a unit in <see cref="_entities"/> reaches here.</summary>
    static void Move(Entity unit, float3 to)
    {
        unit.Write(new Translation { Value = to });
        if (unit.Has<LastTranslation>()) unit.Write(new LastTranslation { Value = to });
        Landed(unit, to);                                                    // unit-watch D34: hidden jump, hop, return, recovery
        if (unit.TryGetComponent<AggroConsumer>(out var aggro))
        {
            aggro.PreCombatPosition = to;
            unit.Write(aggro);
        }
        if (unit.TryGetComponent<SpawnTransform>(out var home))
        {
            home.Position = to;
            unit.Write(home);
        }
        if (Core.EntityManager.HasBuffer<PathBuffer>(unit)) Core.EntityManager.GetBuffer<PathBuffer>(unit).Clear();
        if (unit.Has<Velocity>()) unit.Write(new Velocity());
    }

    /// <summary>One tracked unit's position and level (apocalypse step 3), null when it is not ours or gone.</summary>
    internal static RegionUnit? ReadOwn(TrackedUnit t)
    {
        if (!_entities.TryGetValue(t.Key, out var unit) || !unit.Exists() || !unit.TryGetComponent<Translation>(out var pos)) return null;
        var p = pos.Value;
        // deconstructed, so this reader names no writing member of the class (preflight's entity-write check)
        var (group, home) = _meta.TryGetValue(t.Key, out var meta) && meta is var (_, g, at) ? (g, ((float, float, float)?)at) : (0, null);
        return new RegionUnit(t.Key, t.EventId, t.SpawnedUtc, p.x, p.y, p.z, LevelOf(unit, p.y), t.Wave, group, home);
    }

    /// <summary>The apocalypse's hop and roam jump (step-1 verdict "go — hop"): <see cref="Move"/> on a unit in
    /// <see cref="_entities"/> only; any other key is ignored. A throw reaches the caller's streak.</summary>
    [Mutating]
    internal static void MoveOwn(long key, float3 to)
    {
        if (!_entities.TryGetValue(key, out var unit) || !unit.Exists()) return;
        Move(unit, to);
    }

    /// <summary>The apocalypse's rolling lifetime (D13): the ledger's due time and the unit's LifeTime (its Age plus the new
    /// seconds, so it ends at the same moment), on a unit in <see cref="_entities"/> only and only when the ledger renews it
    /// (a unit queued for despawn is never renewed). A throw reaches the caller's streak.</summary>
    [Mutating]
    internal static void RenewOwn(long key, UnitLifetime life)
    {
        if (!_entities.TryGetValue(key, out var unit) || !unit.Exists() || !_ledger.Renew(key, life)) return;
        var age = unit.TryGetComponent<Age>(out var a) ? a.Value : 0f;
        if (unit.Has<LifeTime>()) unit.Write(new LifeTime { Duration = age + life.LifetimeSeconds, EndAction = LifeTimeEndAction.Destroy });
    }

    /// <summary>The game side of the lock and the hunt. Reads only the ledger's own units and writes only units in
    /// <see cref="_entities"/> (CLAUDE.md › Spawn &amp; buff safety), which Logic/LockPhase and HuntPhase have already
    /// filtered to the running instance's own (HuntingGrounds.Ours). The players are read once per tick and the walk
    /// probe is opened once per tick, at its first use, and disposed by <see cref="End"/>; nothing is kept past the tick.</summary>
    sealed class RegionWorld : IRegionWorld
    {
        WalkCheck.WaveCheck? _check;
        int _walkCalls, _noProbe, _notGrounded, _notFree, _moves;
        long _openTicks, _walkTicks, _playersTicks, _claimedTicks, _moveTicks;   // the Session 2 diagnostic's parts

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>This tick's parts so far (RegionTimingLog).</summary>
        internal RegionParts Parts(int netGcs, int il2cppGcs) =>
            new(Ms(_openTicks), Ms(_walkTicks), Ms(_playersTicks), Ms(_claimedTicks), Ms(_moveTicks), _walkCalls, _moves, netGcs, il2cppGcs);
        List<HuntCandidate> _players;
        string _playersFault;
        bool _playersRead;

        /// <summary>The start of a region tick: nothing of the last tick is reused.</summary>
        internal void Begin()
        {
            _check = null;
            _walkCalls = _noProbe = _notGrounded = _notFree = _moves = 0;
            _openTicks = _walkTicks = _playersTicks = _claimedTicks = _moveTicks = 0;
            _players = null;
            _playersFault = null;
            _playersRead = false;
        }

        /// <summary>The end of a region tick: the walk probe's native resources are freed and the players forgotten.</summary>
        internal void End()
        {
            var check = _check;
            Begin();
            try { check?.Resource?.Dispose(); }
            catch (Exception) { /* a failed dispose never breaks the tick (WaveAction does the same) */ }
        }

        public IEnumerable<TrackedUnit> Units => _ledger.Units;

        public RegionUnit? Read(TrackedUnit t)
        {
            if (!_entities.TryGetValue(t.Key, out var unit) || !unit.Exists() || !unit.TryGetComponent<Translation>(out var pos)) return null;
            var p = pos.Value;
            var known = _meta.TryGetValue(t.Key, out var meta);
            return new RegionUnit(t.Key, t.EventId, t.SpawnedUtc, p.x, p.y, p.z, LevelOf(unit, p.y), t.Wave, known ? meta.Group : 0,
                known ? meta.Spawn : null);
        }

        public string RegionOf(float x, float z) =>
            RegionMap.State.Available ? RegionMap.State.Index.RegionOf(x, z) : throw new InvalidOperationException("regions unavailable");

        /// <summary>Claimed by the event's kept map (TerritoryMaps.ForHunt); with no map, or after its latest build failed,
        /// every point counts as claimed, so no unit lands there (fail closed, as Hunt's seeds).</summary>
        public bool IsClaimed(string eventId, float x, float z)
        {
            var t0 = Stopwatch.GetTimestamp();
            try { return TerritoryMap.Maps.ForHunt(eventId) is not { } map || Territory.IsClaimed(map, x, z); }
            finally { _claimedTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Free and grounded at <paramref name="level"/> by the tick's walk probe; false when no probe opens (the
        /// tile world is unavailable), so a stray goes back to its last inside spot and no unit jumps. A throwing call
        /// throws, which the phase counts against that unit.</summary>
        public bool Walkable(float x, float z, byte level)
        {
            if (_check is null)
            {
                var o = Stopwatch.GetTimestamp();
                try { _check = WalkCheck.OpenSpots(); }
                finally { _openTicks += Stopwatch.GetTimestamp() - o; }
            }
            _walkCalls++;
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_check.Value.Walk.Probe is not { } probe) { _noProbe++; return false; }
                if (!probe.IsGrounded(x, z, level)) { _notGrounded++; return false; }
                if (!probe.IsFree(x, z, level)) { _notFree++; return false; }
                return true;
            }
            finally { _walkTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>The tick's walk checks (Debug.VerboseLogging): "region walk: &lt;n&gt; checks, &lt;g&gt; not grounded,
        /// &lt;f&gt; not free, &lt;p&gt; without a probe", counts only; null when none ran.</summary>
        internal string WalkLine() => _walkCalls == 0 ? null
            : $"region walk: {_walkCalls} checks, {_notGrounded} not grounded, {_notFree} not free, {_noProbe} without a probe";

        public float HeightOf(byte level) => WalkCheck.HeightOf(level);

        public WalkBudget Budget => WalkCheck.Budget;

        /// <summary>Every connected character (PlayerQuery.Read), read once per tick, memory only. The territory flag is
        /// left false: the hunt sets it per event by that event's kept map (IsClaimed, fail closed per event; Codex step 4
        /// F1). A failed read throws for every event of the tick (D9).</summary>
        public IReadOnlyList<HuntCandidate> Players()
        {
            if (!_playersRead)
            {
                _playersRead = true;
                var t0 = Stopwatch.GetTimestamp();
                try
                {
                    _players = PlayerQuery.Read().Select(p => new HuntCandidate(p.Key, p.X, p.Z, true, p.Alive, false, p.InPvpCombat)).ToList();
                }
                catch (Exception e) { _playersFault = e.Message; }
                finally { _playersTicks += Stopwatch.GetTimestamp() - t0; }
            }
            if (_playersFault is not null) throw new InvalidOperationException(_playersFault);
            return _players;
        }

        public void Return(RegionReturn r, Scope scope)
        {
            if (!_entities.TryGetValue(r.Key, out var unit) || !unit.Exists()) return;
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                // Targets first, as the arena (Codex swarm step 3 F3): a throw here leaves the unit outside for the next check.
                DropTargets(r.Key, unit, scope);
                Move(unit, new float3(r.X, r.Y, r.Z));
                _moves++;
            }
            finally { _moveTicks += Stopwatch.GetTimestamp() - t0; }
        }

        public void Jump(RegionJump j)
        {
            if (!_entities.TryGetValue(j.Key, out var unit) || !unit.Exists()) return;
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                Move(unit, new float3(j.X, j.Y, j.Z));
                _moves++;
            }
            finally { _moveTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Removes the returned unit's AggroBuffer entries on players standing in no scope region (D5,
        /// LockRule.DropsEntry), so it does not walk straight back out; entries on players inside, on NPCs and on unreadable
        /// entities stay, as the arena's (Codex swarm step 3 F2). A removed entry HuntAction seeded leaves its seed record
        /// too (HuntSeeds.Removed). Our own unit only.</summary>
        void DropTargets(long key, Entity unit, Scope scope)
        {
            if (!Core.EntityManager.HasBuffer<AggroBuffer>(unit)) return;
            var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                var e = buffer[i].Entity;
                if (!e.Exists() || !e.Has<PlayerCharacter>() || !e.TryGetComponent<Translation>(out var t)) continue;
                if (!LockRule.DropsEntry(scope, RegionOf, (t.Value.x, t.Value.z))) continue;
                buffer.RemoveAt(i);
                HuntAction.Seeds.Removed(key, KeyOf(e));
            }
        }
    }

    // ---- the watch phase: unit watch (unit-watch D2-D11, D18) ----

    static readonly UnitWatchPhase _watch = new();
    static readonly WatchWorld _watchWorld = new();
    static readonly FightRead _fight = new();

    /// <summary>The last time one of our movers put each unit down, and where (unit-watch D34): its spawn, a regroup, an
    /// arena or region return, a hidden jump, an apocalypse hop or roam jump, a recovery. Memory only; the row goes with
    /// the unit.</summary>
    static readonly Dictionary<long, (DateTime At, float X, float Z)> _landed = new();

    /// <summary>Session 1's measurement for D34's default (Debug.VerboseLogging only): each event unit's spawn spot until its
    /// first move of more than 0.5 m, logged once as "[nyar watch] first-move …".</summary>
    static readonly Dictionary<long, (DateTime At, float X, float Z)> _spawnSpot = new();

    /// <summary>A unit waiting longer than this for its first move is logged "after=none" and dropped.</summary>
    const int FirstMoveCapSeconds = 120;

    static void Landed(Entity unit, float3 to, bool spawn = false, bool regroup = false)
    {
        var key = KeyOf(unit);
        var at = DateTime.UtcNow;
        _landed[key] = (at, to.x, to.z);
        if (spawn && Settings.VerboseLogging.Value && Settings.UnitWatchEnabled.Value) _spawnSpot[key] = (at, to.x, to.z);
        else if (regroup && _spawnSpot.TryGetValue(key, out var was)) _spawnSpot[key] = (was.At, to.x, to.z);   // still the spawn
    }

    /// <summary>One verbose line per event unit at its first move of more than 0.5 m from its spawn spot, with the seconds
    /// since its spawn and whether an online player stood within 40 m then (unit-watch D34, Session 1's measurement): no
    /// name and no id. Runs every tick while units wait; never throws into the tick.</summary>
    static void FirstMoves(DateTime now)
    {
        if (_spawnSpot.Count == 0) return;
        try
        {
            foreach (var (key, spot) in _spawnSpot.ToList())
            {
                if (!_entities.TryGetValue(key, out var unit) || !unit.Exists() || !unit.TryGetComponent<Translation>(out var pos))
                {
                    _spawnSpot.Remove(key);
                    continue;
                }
                var age = (now - spot.At).TotalSeconds;
                var moved = math.distance(pos.Value.xz, new float2(spot.X, spot.Z)) > UnitWatch.StillReach;
                if (!moved && age < FirstMoveCapSeconds) continue;
                _spawnSpot.Remove(key);
                if (_ledger.Find(key) is not { EventId: { } eventId } t) continue;
                // A2: the measurement counts units trying to reach a player, the units D34 checks.
                var aimed = unit.TryGetComponent<AggroConsumer>(out var aggro) && aggro.AggroTarget._Entity is var e && e.Exists()
                    && e.Has<PlayerCharacter>();
                var after = moved ? age.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "none";
                Core.Log.LogInfo($"[nyar watch] first-move event={eventId} prefab={t.Prefab} after={after} player={(aimed ? "target" : "none")}");
            }
        }
        catch (Exception ex) { _spawnSpot.Clear(); Core.Log.LogWarning($"[nyar watch] first-move read failed: {ex.Message}"); }
    }

    /// <summary>The [UnitWatch] keys as loaded (D1, D34); BepInEx reads the cfg at boot.</summary>
    static WatchSettings WatchSettingsNow() =>
        new(Settings.UnitWatchEnabled.Value, Settings.Limit(Limits.StuckSeconds), Settings.Limit(Limits.StallMinutes),
            Settings.Limit(Limits.SpawnStillSeconds));

    /// <summary>The watch's health entries ("unit watch: read failing", "unit watch: move failing"; D18), the fight read's
    /// own read failure folded into the first, for HealthMonitor and `.nyar status`.</summary>
    internal static IReadOnlyList<string> WatchHealth => _watch.Health.Concat(_fight.Health).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>`event info`'s counts for a running instance (D15).</summary>
    internal static WatchCounts WatchCountsOf(ActiveEvent active) => _watch.Counts(active);

    /// <summary>An end path of <paramref name="eventId"/> (D11): its rows, tries, clocks and counts go; with no event left
    /// running the streaks and health entries go too.</summary>
    internal static void WatchEnded(string eventId)
    {
        _watch.EventEnded(eventId);
        if (EventRuntime.Engine.Active.Count == 0) _fight.Ok();
    }

    /// <summary>`purge confirm` (D10, D11): every row, try, clock, count and streak goes at once.</summary>
    internal static void WatchPurged()
    {
        _watch.Clear();
        _fight.Ok();
    }

    /// <summary>The death pass, inside <see cref="Died"/>: the unit's row goes and its wave's stall clock restarts (D8, D9);
    /// never throws, so nothing of the death pass is skipped (D18).</summary>
    static void WatchDied(long key, TrackedUnit tracked)
    {
        try { _watch.Died(key, tracked?.EventId, tracked?.Wave ?? 0, DateTime.UtcNow); }
        catch (Exception) { /* the watch never breaks the death pass */ }
    }

    /// <summary>`.nyar event unstick &lt;id&gt;` and its twin (D12, D13), through AdminOps inside the gateway: the phase marks
    /// the running instance's live units that have not fought; nothing is moved here (the next checks do).</summary>
    internal static Outcome Unstick(string id) =>
        _watch.Unstick(id, EventStore.Catalog.Current.Find(id) is not null, EventRuntime.Engine.Find(id), _watchWorld, WatchSettingsNow(),
            DateTime.UtcNow, line => Core.Log.LogInfo($"[nyar] {line}"));

    /// <summary>The scheduler's watch phase (D2-D10), after the arena, region and apocalypse phases: every
    /// UnitWatch.CheckSeconds while an event runs and [UnitWatch] Enabled is true; the logic and its guards are
    /// Logic/UnitWatchPhase, which never throws. The check's walk probe and players are dropped here whatever happens.</summary>
    [Mutating]
    internal static void WatchTick(DateTime now)
    {
        if (!Settings.UnitWatchEnabled.Value) _spawnSpot.Clear();             // off: the phase drops its rows below; no measurement either
        else if (Settings.VerboseLogging.Value) FirstMoves(now);                  // unit-watch D34: Session 1's measurement
        try
        {
            var active = EventRuntime.Engine.Active;
            if (active.Count == 0 || _ledger.Tracked == 0) _fight.Ok();          // the events ended or nothing is tracked: the fight streak too (D18)
            else _fight.Checked();
            _watch.Tick(now, active, _watchWorld, WatchSettingsNow(),
                line => Core.Log.LogInfo($"[nyar] {line}"), line => Core.Log.LogError($"[nyar] {line}"));
        }
        finally { _watchWorld.End(); }
    }

    /// <summary>The fight patch's gate (D4, D10): the watch is on, an event runs and the ledger tracks a unit. A cheap read,
    /// so the patch returns at once otherwise.</summary>
    internal static bool WatchesFights => Settings.UnitWatchEnabled.Value && _ledger.Tracked > 0 && EventRuntime.Engine.Active.Count > 0;

    /// <summary>The ledger key of an entity (the fight patch's lookup; never stored with anything of a player).</summary>
    internal static long KeyFor(Entity e) => KeyOf(e);

    /// <summary>One batch of the fight patch (D4, D18): Logic/FightRead keeps each tracked unit's last fight with a player
    /// in the phase's FightBook, guarded per row, logged once per streak.</summary>
    internal static void FightBatch(int count, Func<int, long?> read) =>
        _fight.Pass(count, read, _watch.Fights, IsTracked, DateTime.UtcNow, line => Core.Log.LogError($"[nyar] {line}"));

    /// <summary>The game side of the watch. Reads only the ledger's own units (through <see cref="_entities"/>, as
    /// <see cref="ReadOwn"/>), the players once per check and the walk check; writes only units in <see cref="_entities"/>
    /// (CLAUDE.md › Spawn &amp; buff safety), which Logic/UnitWatchPhase has already filtered to the running instance's own
    /// (UnitWatch.Watched); a removal is a request on the staged despawn queue, never a destroy here. The walk probe is
    /// opened at its first use in a check and disposed by <see cref="End"/>; the players are kept for that check only.</summary>
    sealed class WatchWorld : IUnitWatchWorld
    {
        WalkCheck.WaveCheck? _check;
        List<PlayerRow> _rows;

        /// <summary>The end of a check: the walk probe's native resources are freed and the players forgotten (D17).</summary>
        internal void End()
        {
            var check = _check;
            _check = null;
            _rows = null;
            try { check?.Resource?.Dispose(); }
            catch (Exception) { /* a failed dispose never breaks the tick */ }
        }

        IWalkProbe Probe()
        {
            _check ??= WalkCheck.OpenSpots();
            return _check.Value.Walk.Probe;
        }

        public IEnumerable<TrackedUnit> Units => _ledger.Units;

        /// <summary>The unit's position and level (as <see cref="ReadOwn"/>), its group, its anchor (its spawn spot; an
        /// apocalypse horde unit's front or a roamer's hotspot, at its spawn height) and, when its aggro target is a player,
        /// that player's distance on x and z (never the player), with its combat reach as the attack range. Null for a unit
        /// that is gone or dead.</summary>
        public WatchUnit? Read(TrackedUnit t)
        {
            if (!_entities.TryGetValue(t.Key, out var unit) || !unit.Exists() || unit.Has<Dead>() || !unit.TryGetComponent<Translation>(out var pos)) return null;
            var p = pos.Value;
            var known = _meta.TryGetValue(t.Key, out var meta);
            var (ax, ay, az) = known ? meta.Spawn : (p.x, p.y, p.z);
            if (t.EventId is { } eventId && ApocalypseAction.AnchorOf(eventId, t.Wave) is { } front) (ax, az) = (front.X, front.Z);
            float? target = null;
            if (unit.TryGetComponent<AggroConsumer>(out var aggro) && aggro.AggroTarget._Entity is var e && e.Exists() && e.Has<PlayerCharacter>()
                && e.TryGetComponent<Translation>(out var tp))
                target = math.distance(tp.Value.xz, p.xz);
            var range = unit.TryGetComponent<GenericCombatMovementData>(out var combat) && float.IsFinite(combat.MaxDistance) ? combat.MaxDistance : 0f;
            // Diagnostic (Session 1, A3: the game lifts a unit sunk by a dev write back to its level): a unit 5 m or more below its
            // anchor, under VerboseLogging.
            if (Settings.VerboseLogging.Value && ay - p.y >= 5f)
                Core.Log.LogInfo($"[nyar watch] low event={t.EventId} prefab={t.Prefab} y={p.y:0.0} anchor={ay:0.0} level={LevelOf(unit, p.y)}");
            return new WatchUnit(t.Key, p.x, p.y, p.z, LevelOf(unit, p.y), known ? meta.Group : 0, (ax, ay, az, LevelAt(ay)), target, range,
                _landed.TryGetValue(t.Key, out var landed) ? landed : null);
        }

        static byte LevelAt(float y) => WalkHeight.Problem(y) is null ? TileLayerUtility.GetHeightLevel(y) : (byte)0;

        /// <summary>Every connected character (PlayerQuery.Read), once per check, memory only (D17); in claimed territory
        /// when any running event's kept map claims the spot (Hunt's eligibility, D5).</summary>
        public IReadOnlyList<HuntCandidate> Players()
        {
            _rows = PlayerQuery.Read();
            var maps = EventRuntime.Engine.Active.Select(a => TerritoryMap.Maps.ForHunt(a.Id)).Where(m => m is not null).ToList();
            return _rows.Select(p => new HuntCandidate(p.Key, p.X, p.Z, true, p.Alive, maps.Any(m => Territory.IsClaimed(m, p.X, p.Z)), p.InPvpCombat)).ToList();
        }

        /// <summary>A revenge instance's focus player's Hunt key (Spawning's FocusKey join), from this check's players.</summary>
        public long? FocusKey(ActiveEvent a) => HuntPlan.FocusKey((_rows ?? PlayerQuery.Read()).Select(p => (p.Key, p.PlatformId)), a.Focus);

        /// <summary>One grounded probe at (x, z) on <paramref name="level"/> (D3): null when the walk check cannot answer (no
        /// tile world, off the tile grid). The watch scans the levels in Logic (UnitWatch.GroundScan), each probe from its
        /// walk budget (D10). A throwing call throws, which the phase counts as a failed read of that unit only.</summary>
        public bool? Grounded(float x, float z, byte level) =>
            WalkCheck.OnGrid(x, z) && Probe() is { } probe ? probe.IsGrounded(x, z, level) : null;

        /// <summary>Free and grounded at <paramref name="level"/>; throws without a walk check, so no unit lands on unread
        /// ground and the read failure shows (as the apocalypse's hop).</summary>
        public bool Walkable(float x, float z, byte level) =>
            Probe() is { } probe ? probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level)
            : throw new InvalidOperationException("walk check: none");

        public float HeightOf(byte level) => WalkCheck.HeightOf(level);

        public string RegionOf(float x, float z) =>
            RegionMap.State.Available ? RegionMap.State.Index.RegionOf(x, z) : throw new InvalidOperationException("regions unavailable");

        /// <summary>Claimed by the event's kept map; with none, or after its latest build failed, every point counts as
        /// claimed, so no unit lands there (fail closed, as the hunt's).</summary>
        public bool IsClaimed(string eventId, float x, float z) =>
            TerritoryMap.Maps.ForHunt(eventId) is not { } map || Territory.IsClaimed(map, x, z);

        public int? HordesRisen(ActiveEvent a) => ApocalypseAction.HordesRisen(a.Id);

        public WalkBudget Budget => WalkCheck.Budget;

        /// <summary>The hidden recovery (D5): <see cref="Move"/> on a unit in <see cref="_entities"/> only, as the region
        /// hunt's jump (Translation, LastTranslation, PreCombatPosition, SpawnTransform, its path and velocity).</summary>
        public void Move(WatchMove m)
        {
            if (!_entities.TryGetValue(m.Key, out var unit) || !unit.Exists()) return;
            SpawnTracker.Move(unit, new float3(m.X, m.Y, m.Z));
        }

        /// <summary>The removal (D6-D8): queued on the staged despawn like any despawn, within MaxDespawnsPerTick; no death
        /// event follows, so nothing a death raises is raised. False when the ledger no longer tracks it or it is queued.</summary>
        public bool Despawn(long key) => _entities.ContainsKey(key) && _ledger.IsTracked(key) && _ledger.QueueDespawn(key);
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

    /// <summary>The apocalypse's tally hears a tracked unit leave (D15), before <see cref="Release"/> forgets it. Never throws.</summary>
    static void Departed(long key, UnitDeparture how)
    {
        if (_stateUnits.TryGetValue(key, out var u)) ApocalypseAction.Left(u.EventId, u.SpawnedUtc, how);
    }

    static void Release(long key)
    {
        _entities.Remove(key);
        _meta.Remove(key);
        _landed.Remove(key);
        _spawnSpot.Remove(key);
        _watch.Gone(key);                                                    // unit-watch D11: a row dies with its unit
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
        Landed(unit, position, spawn: true);                                 // unit-watch D34: the spawn spot
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
            var key = KeyOf(unit);                                         // its put-down records die with it (unit-watch D34)
            _landed.Remove(key);
            _spawnSpot.Remove(key);
        }
        catch (Exception) { }
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
