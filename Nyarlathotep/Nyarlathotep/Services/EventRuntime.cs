using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// The running events (foundation D20, D21, D22, D25, D29; Design › States). Logic/EventEngine holds the instances
/// and their waves; this class applies the controls, the conditions of automatic starts, state.json's instance list,
/// the despawn of an event's units and the fault limit. Fifth in Core.TryInitialize: every instance state.json lists
/// from the last run is cancelled, and the boot marker sweep removes its units (D21).
/// </summary>
internal static class EventRuntime
{
    static readonly System.Random _random = new();
    static readonly List<string> _degraded = new();

    internal static EventEngine Engine { get; private set; } = new(EventStore.Catalog);

    /// <summary>What `.nyar status` shows admins as degraded: pillars whose event was cancelled after its faults
    /// (D25).</summary>
    internal static IReadOnlyList<string> Degraded => _degraded;

    internal static void Initialize()
    {
        Engine = new EventEngine(EventStore.Catalog, () => Persistence.State.Document.LastStart);
        _degraded.Clear();
        var doc = Persistence.State.Document;
        foreach (var instance in doc.Instances)
            Core.Log.LogInfo($"[nyar] event {instance.EventId} cancelled by restart (it was due to end {instance.EndsUtc:u})");
        if (doc.Instances.Count > 0)
        {
            doc.Instances.Clear();
            Persistence.State.MarkDirty();
        }
    }

    /// <summary>The controls in force now (Business rules 1).</summary>
    internal static ControlState Controls() => new(
        Persistence.State.Document.PurgeUntilUtc is { } until && until > DateTime.UtcNow,
        Settings.Enabled.Value,
        EnabledPillars(),
        Engine.Active.Count,
        Settings.Limit(Limits.MaxConcurrentEvents));

    static HashSet<Pillar> EnabledPillars()
    {
        var on = new HashSet<Pillar>();
        if (Settings.EmpowermentEnabled.Value) on.Add(Pillar.Empowerment);
        if (Settings.EventSpawnsEnabled.Value) on.Add(Pillar.Spawns);
        if (Settings.BossReinforcementsEnabled.Value) on.Add(Pillar.Boss);
        if (Settings.DefendedZonesEnabled.Value) on.Add(Pillar.Zones);
        if (Settings.SiegeWavesEnabled.Value) on.Add(Pillar.Sieges);
        return on;
    }

    /// <summary>Starts event <paramref name="id"/>. An admin's start (`.nyar event start`) meets the controls; an
    /// automatic one (System) also meets the definition's conditions, and one refused by a switched-off master or
    /// pillar switch logs nothing (Business rules 6). Logs "event &lt;id&gt; started by &lt;trigger&gt;" (D29).</summary>
    [Mutating]
    internal static string StartEvent(string id, string trigger, Actor actor, (float X, float Y, float Z)? origin)
    {
        var now = DateTime.UtcNow;
        var controls = Controls();
        var def = EventStore.Catalog.Current.Find(id);
        if (actor == Actor.System && def is not null)
        {
            if (!controls.GeneralEnabled || !controls.EnabledPillars.Contains(def.Pillar)) return "off";
            if (Engine.Find(id) is null)
            {
                var blocked = ConditionCheck.Blocker(def.Conditions, new ConditionContext(ConnectedPlayers(), ServerMode(),
                    TimeOnly.FromDateTime(DateTime.Now), now, Engine.LastStartUtc(id), _random.Next(1, 101)));
                if (blocked is not null)
                {
                    Core.Log.LogInfo($"[nyar] event {id} not started by {trigger}: {blocked}");
                    return blocked;
                }
            }
        }

        var refused = Engine.Start(id, trigger, now, controls, origin);
        if (refused is not null)
        {
            Core.Log.LogInfo($"[nyar] event {id} not started by {trigger}: {refused}");
            return refused;
        }
        var active = Engine.Find(id)!;
        Persistence.State.Document.Instances.Add(new StateInstance(id, active.Instance.StartedUtc, active.Instance.EndsUtc, "active"));
        Persistence.State.MarkDirty();
        Core.Log.LogInfo($"[nyar] event {id} started by {trigger} (ends {active.Instance.EndsUtc:u})");
        if (EventActions.ActionKindOf(active.Definition) == EventActionKind.Empower) EmpowerAction.StartCarriers(active);
        Announcer.EventStarted(active.Instance);
        return $"event {id} started";
    }

    /// <summary>`.nyar event stop`: ends the event now and queues its units for despawn.</summary>
    [Mutating]
    internal static string StopEvent(string id) =>
        End(id, "stopped") ? $"event {id} stopped" : "not active";

    /// <summary>The kill switch (D20): ends every event, cancels every waiting spawn, queues every tracked unit and starts
    /// the PurgeCooldownSeconds window, during which nothing starts or spawns.</summary>
    [Mutating]
    internal static string Purge()
    {
        var cooldown = Settings.Limit(Limits.PurgeCooldownSeconds);
        var events = Engine.CancelAll(cooldown);
        var (queued, cancelled) = SpawnTracker.PurgeUnits();
        EmpowerAction.StopAllCarriers();
        var doc = Persistence.State.Document;
        doc.Instances.Clear();
        doc.PurgeUntilUtc = DateTime.UtcNow.AddSeconds(cooldown);
        Persistence.State.MarkDirty();
        Announcer.Purged();
        Core.Log.LogWarning($"[nyar] purge: {events.Count} events ended, {queued} units queued, {cancelled} spawns cancelled, cooldown {cooldown}s");
        return AdminLines.Purged(events.Count, queued);
    }

    /// <summary>One scheduler tick for the events: ends the expired ones, queues the units of those past their grace,
    /// then runs each active event's wave step inside its own try/catch; three faults in a row cancel that event and mark
    /// its pillar degraded while the others keep running (D25).</summary>
    internal static void Tick(DateTime now)
    {
        foreach (var ended in Engine.Expire(now, Settings.Limit(Limits.GraceSeconds)))
        {
            RemoveInstance(ended.Id);
            if (EventActions.ActionKindOf(ended.Definition) == EventActionKind.Empower)
            {
                // Every carrier's LifeTime ends in this same second, so nothing is queued (D5).
                var carriers = EmpowerAction.CarriersOf(ended.Id);
                EmpowerAction.EndCarriers(ended.Id);
                Core.Log.LogInfo($"[nyar] event {ended.Id} ended ({carriers} carriers expire with it)");
            }
            else
            {
                // Its waiting orders go now: one spawned later would get a fresh LifeTime and outlive end + grace, and the
                // cleanup after the grace keeps waiting orders, which then belong to a restart of the same event.
                SpawnTracker.EndEventUnits(ended.Id, DateTime.MinValue);
                Core.Log.LogInfo($"[nyar] event {ended.Id} ended ({ended.WavesSpawned} of {ended.Definition.Action?.Waves ?? 0} waves)");
            }
            Announcer.EventEnded(ended.Definition);
        }
        foreach (var cleanup in Engine.DueCleanups(now))
        {
            var (queued, _) = SpawnTracker.EndEventUnits(cleanup.EventId, cleanup.SpawnedBefore, cancelOrders: false);
            if (queued > 0) Core.Log.LogInfo($"[nyar] event {cleanup.EventId}: {queued} units queued for despawn after the grace");
        }
        // Carrier removals first, within EmpowerBatchPerTick; the Empower events share what is left (D5).
        EmpowerAction.BeginCarrierTick();
        foreach (var active in Engine.Active.ToList())
        {
            try
            {
#if DEBUG
                if (Settings.FaultInjection.Value == active.Id) throw new InvalidOperationException("Debug.FaultInjection");
#endif
                switch (EventActions.ActionKindOf(active.Definition))
                {
                    case EventActionKind.Empower: EmpowerAction.TickCarriers(active, now); break;
                    case EventActionKind.Waves: WaveAction.QueueDueWave(active, now); break;
                }
                Engine.Healthy(active.Id);
            }
            catch (Exception ex)
            {
                var limit = Engine.Fault(active.Id);
                Core.Log.LogError($"[nyar] event {active.Id} tick failed ({active.Faults}/{EventEngine.FaultLimit}): {ex.Message}");
                if (!limit) continue;
                End(active.Id, $"cancelled after {EventEngine.FaultLimit} faults");
                var note = $"{active.Definition.Pillar.ToString().ToLowerInvariant()} (event {active.Id} faulted)";
                if (!_degraded.Contains(note)) _degraded.Add(note);
            }
        }
    }

    static bool End(string id, string why)
    {
        var ended = Engine.Cancel(id);
        if (ended is null) return false;
        RemoveInstance(id);
        if (EventActions.ActionKindOf(ended.Definition) == EventActionKind.Empower)
        {
            var carriers = EmpowerAction.CarriersOf(id);
            EmpowerAction.StopCarriers(id);
            Core.Log.LogWarning($"[nyar] empower {id}: {carriers} carriers queued for removal ({why})");
        }
        else
        {
            var (queued, cancelled) = SpawnTracker.EndEventUnits(id, DateTime.MaxValue);
            Core.Log.LogWarning($"[nyar] event {id} {why}: {queued} units queued, {cancelled} spawns cancelled");
        }
        Announcer.EventEnded(ended.Definition);
        return true;
    }

    static void RemoveInstance(string id)
    {
        if (Persistence.State.Document.Instances.RemoveAll(i => i.EventId == id) > 0) Persistence.State.MarkDirty();
    }

    /// <summary>Connected users, for conditions.minPlayers.</summary>
    static int ConnectedPlayers()
    {
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
        try
        {
            var users = query.ToComponentDataArray<User>(Allocator.Temp);
            try { return users.ToArray().Count(u => u.IsConnected); }
            finally { users.Dispose(); }
        }
        finally { query.Dispose(); }
    }

    static GameMode ServerMode() =>
        Core.ServerGameSettingsSystem._Settings.GameModeType == GameModeType.PvP ? GameMode.Pvp : GameMode.Pve;
}
