using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

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

    static readonly ClearedRead _clearedRead = new();

    /// <summary>The running instances' scoreboards (wave-sets D8), in memory only; every end path ends its rows.</summary>
    internal static Scoreboard Board { get; } = new();

    /// <summary>The scoreboard's kill feed (wave-sets D13), driven by Patches/DeathEventPatch.</summary>
    internal static ScoreFeed Feed { get; } = new();

    /// <summary>The waveList schedule's cleared read (wave-sets D4, D5, D18): the ledger's WaveCleared for each running
    /// instance's own units (A3), guarded, so a throwing read holds the wave and logs once per streak.</summary>
    internal static Func<string, int, bool> Cleared =>
        _clearedRead.Guard(Engine.ClearedBy(SpawnTracker.Ledger), line => Core.Log.LogWarning($"[nyar] {line}"));

    /// <summary>The wave sets' health entries (HealthMonitor, `.nyar status`): a failing kill read or cleared read.</summary>
    internal static IEnumerable<string> WaveSetHealth => Feed.Health.Concat(_clearedRead.Health);

    /// <summary>What `.nyar status` shows admins as degraded: pillars whose event was cancelled after its faults
    /// (D25).</summary>
    internal static IReadOnlyList<string> Degraded => _degraded;

    internal static void Initialize()
    {
        Engine = new EventEngine(EventStore.Catalog, () => Persistence.State.Document.LastStart);
        _degraded.Clear();
        Board.Clear();                                                       // a restart shows no scoreboard (wave-sets D11)
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
        Settings.Limit(Limits.MaxConcurrentEvents),
        OnlinePositions,
        RegionMap.State.Available ? RegionMap.State.Index.RegionOf : null);

    /// <summary>The online players' x/z (regions D4), read only when a scoped start asks; through
    /// PositionReader.Collect (A34), so a failing query yields none and a player whose character cannot be read is left
    /// out while the others count. Never throws; never logged or sent anywhere.</summary>
    static IReadOnlyList<(float X, float Z)> OnlinePositions() => PositionReader.Collect(ConnectedCharacters, CharacterPosition);

    static IReadOnlyList<Entity> ConnectedCharacters()
    {
        var characters = new List<Entity>();
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
        try
        {
            var users = query.ToComponentDataArray<User>(Allocator.Temp);
            try
            {
                foreach (var user in users)
                    if (user.IsConnected) characters.Add(user.LocalCharacter._Entity);
            }
            finally { users.Dispose(); }
        }
        finally { query.Dispose(); }
        return characters;
    }

    static (float X, float Z)? CharacterPosition(Entity character) =>
        character.Exists() && character.TryGetComponent<Translation>(out var t) ? (t.Value.x, t.Value.z) : null;

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
    /// pillar switch logs nothing (Business rules 6). Logs "event &lt;id&gt; started by &lt;trigger&gt;" (D29). A
    /// player-action start (automation D13, D14) passes its <paramref name="focus"/> player and <paramref name="quiet"/>,
    /// which takes each refusal line in place of the log, so PlayerTriggerGate throttles it.</summary>
    [Mutating]
    internal static Outcome StartEvent(string id, string trigger, Actor actor, (float X, float Y, float Z)? origin,
        (float X, float Z)? kill = null, string focus = null, Action<string> quiet = null)
    {
        Action<string> refusal = quiet ?? (line => Core.Log.LogInfo($"[nyar] {line}"));
        var now = DateTime.UtcNow;
        var controls = Controls();
        var def = EventStore.Catalog.Current.Find(id);
        if (actor == Actor.System && def is not null)
        {
            if (!controls.GeneralEnabled || !controls.EnabledPillars.Contains(def.Pillar)) return AdminLines.SystemOff;
            if (Engine.Find(id) is null)
            {
                var blocked = ConditionCheck.Blocker(def.Conditions, new ConditionContext(ConnectedPlayers(), ServerMode(),
                    TimeOnly.FromDateTime(DateTime.Now), now, Engine.LastStartUtc(id), _random.Next(1, 101)));
                if (blocked is not null)
                {
                    refusal($"event {id} not started by {trigger}: {blocked}");
                    return AdminLines.ConditionBlocked(blocked);
                }
            }
        }

        var refused = Engine.Start(id, trigger, now, controls, origin, kill, focus);
        if (refused is not null)
        {
            refusal(AdminLines.StartRefusedLog(id, trigger, refused, actor == Actor.System, def?.Trigger.Scope ?? Scope.Global));
            // The admin is told how long the purge cooldown still runs (A7, D30).
            return AdminLines.StartRefused(refused, actor == Actor.Admin, Persistence.State.Document.PurgeUntilUtc, now);
        }
        var active = Engine.Find(id)!;
        Persistence.State.Document.Instances.Add(new StateInstance(id, active.Instance.StartedUtc, active.Instance.EndsUtc, "active"));
        Persistence.State.MarkDirty();
        Core.Log.LogInfo($"[nyar] event {id} started by {trigger} (ends {active.Instance.EndsUtc:u})");
        if (EventActions.ActionKindOf(active.Definition) == EventActionKind.Empower) EmpowerAction.StartCarriers(active);
        Announcer.EventStarted(active.Instance);
        return AdminLines.Started(id);
    }

    /// <summary>`.nyar event stop`: ends the event now and queues its units for despawn.</summary>
    [Mutating]
    internal static Outcome StopEvent(string id) =>
        End(id, "stopped", EndPath.Stop) ? AdminLines.Stopped(id) : AdminLines.NotActive(id);

    /// <summary>The kill switch (D20): ends every event, cancels every waiting spawn, queues every tracked unit and starts
    /// the PurgeCooldownSeconds window, during which nothing starts or spawns.</summary>
    [Mutating]
    internal static Outcome Purge()
    {
        var cooldown = Settings.Limit(Limits.PurgeCooldownSeconds);
        var events = Engine.CancelAll(cooldown);
        var (queued, cancelled) = SpawnTracker.PurgeUnits();
        foreach (var e in events)
        {
            EndSpawnState(e.Id);                                             // event-spawns D33
            ScoreboardRule.End(EndPath.Purge, e, Board);                     // its rows go, nothing shown (wave-sets D11)
        }
        HuntAction.Clear();                                                  // seeds and kept maps go too
        TerritoryMap.Clear();
        WalkCheck.Health.Purged();                                           // every streak, `.nyar spawn`'s "manual" too (A61, A71)
        EmpowerAction.StopAllCarriers();
        var doc = Persistence.State.Document;
        doc.Instances.Clear();
        doc.PurgeUntilUtc = DateTime.UtcNow.AddSeconds(cooldown);
        Persistence.State.MarkDirty();
        Announcer.Purged();
        Core.Log.LogWarning($"[nyar] purge: {events.Count} events ended, {queued} units queued, {cancelled} spawns cancelled, cooldown {cooldown}s");
        return AdminLines.PurgeDone(events.Count, queued, cooldown);
    }

    /// <summary>One scheduler tick for the events: ends the expired ones, queues the units of those past their grace,
    /// then runs each active event's wave step inside its own try/catch; three faults in a row cancel that event and mark
    /// its pillar degraded while the others keep running (D25).</summary>
    [Mutating]
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
                EndSpawnState(ended.Id);
                Core.Log.LogInfo($"[nyar] event {ended.Id} ended ({ended.WavesSpawned} of {ended.Definition.Action?.Waves ?? 0} waves)");
            }
            Announcer.EventEnded(ended.Definition);
            EndScoreboard(EndPath.Natural, ended);
        }
        // All waves defeated (wave-sets D7): a natural end now, its grace cleanup scheduled by Engine.Complete.
        foreach (var beaten in Engine.Complete(now, Settings.Limit(Limits.GraceSeconds), Cleared))
        {
            RemoveInstance(beaten.Id);
            SpawnTracker.EndEventUnits(beaten.Id, DateTime.MinValue);
            EndSpawnState(beaten.Id);
            Core.Log.LogInfo($"[nyar] {ScoreboardRule.VictoryLine(beaten)}");
            Announcer.EventEnded(beaten.Definition);
            EndScoreboard(EndPath.Victory, beaten);
        }
        Board.Keep(Engine.Active.Select(a => a.Id).ToHashSet());             // no row outlives its instance (D8)
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
                End(active.Id, $"cancelled after {EventEngine.FaultLimit} faults", EndPath.Fault);
                var note = $"{active.Definition.Pillar.ToString().ToLowerInvariant()} (event {active.Id} faulted)";
                if (!_degraded.Contains(note)) _degraded.Add(note);
            }
        }
    }

    /// <summary>An event's spawn state beyond its units (event-spawns D13, D30, D33): its hunt seeds, its kept territory
    /// map and its open failure streaks go with the event.</summary>
    static void EndSpawnState(string id)
    {
        HuntAction.EndEvent(id);
        TerritoryMap.Forget(id);
        WalkCheck.Health.EventEnded(id);                                    // A71
    }

    /// <summary>`.nyar pillar &lt;name&gt; off` (event-library D14, S-7): ends every running event of that pillar through the
    /// stop path and returns their ids, in ordinal order.</summary>
    internal static IReadOnlyList<string> EndPillar(Pillar pillar) =>
        Engine.Active.Where(a => a.Definition.Pillar == pillar).Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal).ToList()
            .Where(id => End(id, "ended (pillar off)", EndPath.PillarOff)).ToList();

    static bool End(string id, string why, EndPath path)
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
            EndSpawnState(id);
            Core.Log.LogWarning($"[nyar] event {id} {why}: {queued} units queued, {cancelled} spawns cancelled");
        }
        Announcer.EventEnded(ended.Definition);
        EndScoreboard(path, ended);
        return true;
    }

    /// <summary>Ends <paramref name="ended"/>'s scoreboard on <paramref name="path"/> (wave-sets D11): shown after the end
    /// banner at the natural end, all waves defeated and an admin stop, its log line counts only; silent otherwise.</summary>
    static void EndScoreboard(EndPath path, ActiveEvent ended)
    {
        var shown = ScoreboardRule.End(path, ended, Board);
        if (shown.Log is not { } log) return;
        Core.Log.LogInfo($"[nyar] {log}");
        Announcer.Scoreboard(ended.Id, shown.Chat);
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
