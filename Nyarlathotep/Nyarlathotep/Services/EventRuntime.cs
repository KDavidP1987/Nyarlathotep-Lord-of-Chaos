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
        ScalingReads.Clear();                                                // scaling D12, D15: a restart keeps no decision or streak
        // apocalypse D12: the record's verdict; a resuming run waits behind the drain gate, every other instance is cancelled
        var resuming = ApocalypseAction.Boot(Engine, DateTime.UtcNow);
        var doc = Persistence.State.Document;
        foreach (var instance in doc.Instances)
        {
            if (instance.EventId == resuming)
            {
                Core.Log.LogInfo($"[nyar] event {instance.EventId} kept by restart: its apocalypse resumes after the drain");
                continue;
            }
            Core.Log.LogInfo($"[nyar] event {instance.EventId} cancelled by restart (it was due to end {instance.EndsUtc:u})");
            if (EventStore.Catalog.Current.Find(instance.EventId) is { } def && RevengeRule.IsRevenge(def))
                Core.Log.LogInfo($"[nyar] {RevengeRule.EndedLine(def.Id, RevengeEndPath.Restart)}");   // revenge D8: silent
        }
        if (doc.Instances.RemoveAll(i => i.EventId != resuming) > 0) Persistence.State.MarkDirty();
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

        var refused = Engine.Start(id, trigger, now, controls, origin, kill, focus, gate: ApocalypseAction.Prepare);
        if (refused is not null)
        {
            // a failed native read is logged by the host once per streak (apocalypse D21); every other refusal here
            if (!(def?.Apocalypse is not null && refused.Reason == Reasons.Read))
                refusal(AdminLines.StartRefusedLog(id, trigger, refused, actor == Actor.System, def?.Trigger.Scope ?? Scope.Global));
            // The admin is told how long the purge cooldown still runs (A7, D30).
            return AdminLines.StartRefused(refused, actor == Actor.Admin, Persistence.State.Document.PurgeUntilUtc, now);
        }
        var active = Engine.Find(id)!;
        Persistence.State.Document.Instances.Add(new StateInstance(id, active.Instance.StartedUtc, active.Instance.EndsUtc, "active"));
        Persistence.State.MarkDirty();
        Core.Log.LogInfo($"[nyar] event {id} started by {trigger} (ends {active.Instance.EndsUtc:u})");
        if (EventActions.ActionKindOf(active.Definition) == EventActionKind.Empower) EmpowerAction.StartCarriers(active);
        if (EventActions.ActionKindOf(active.Definition) == EventActionKind.Apocalypse && !ApocalypseAction.Begin(active, now))   // D3, D11
            Core.Log.LogWarning($"[nyar] apocalypse {id}: started without its run (begin failed); `.nyar event stop {id}` ends it");   // review L5
        Announcer.EventStarted(active.Instance);
        if (RevengeRule.StartPrivate(active) is { } start) TriggerBus.Tell(start.Player, start.Line);   // revenge D7
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
            RevengeEnded(e, RevengeEndPath.Purge);
        }
        TriggerBus.ClearRevenge();                                           // revenge D8: no heat outlives the purge
        HuntAction.Clear();                                                  // seeds and kept maps go too
        ApocalypseAction.Purged();                                           // apocalypse D9: the run, a pending resume and the record
        SpawnTracker.RegionPurged();                                         // hunting-grounds D12: every lock and hunt record
        SpawnTracker.WatchPurged();                                          // unit-watch D10, D11: every watch row, try, clock and streak
        ScalingReads.Clear();                                                // scaling D12, D15: every decision row and streak
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
            var cancelled = 0;
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
                (_, cancelled) = SpawnTracker.EndEventUnits(ended.Id, DateTime.MinValue);
                EndSpawnState(ended.Id);
                if (ended.Definition.Apocalypse is { } apo)
                    ApocalypseEnded(ended, ApocalypseResult.Survived, apo);   // D8: the duration ran out first
                else
                    Core.Log.LogInfo($"[nyar] event {ended.Id} ended ({ended.WavesSpawned} of {ended.Definition.Action?.Waves ?? 0} waves)");
            }
            if (ended.Definition.Apocalypse is null) Announcer.EventEnded(ended.Definition);
            EndScoreboard(EndPath.Natural, ended);
            if (ended.Definition.Apocalypse is not null) ApocalypseAction.Ended(ended.Id, ApocalypseEnd.Survived, cancelled);   // D15: cancelled orders count failed (review L1)
            RevengeEnded(ended, RevengeEndPath.NaturalEnd);
        }
        foreach (var (id, wave) in Engine.NewlyCleared(Cleared)) Core.Log.LogInfo($"[nyar] event {id} wave {wave} cleared");   // D20
        // All waves defeated (wave-sets D7): a natural end now, its grace cleanup scheduled by Engine.Complete.
        foreach (var beaten in Engine.Complete(now, Settings.Limit(Limits.GraceSeconds), Cleared))
        {
            RemoveInstance(beaten.Id);
            SpawnTracker.EndEventUnits(beaten.Id, DateTime.MinValue);
            EndSpawnState(beaten.Id);
            Core.Log.LogInfo($"[nyar] {ScoreboardRule.VictoryLine(beaten)}");
            Announcer.EventEnded(beaten.Definition);
            EndScoreboard(EndPath.Victory, beaten);
            RevengeEnded(beaten, RevengeEndPath.AllWavesDefeated);
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
            if (EventActions.ActionKindOf(active.Definition) == EventActionKind.Apocalypse) continue;   // its own phase, ApocalypseTick
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

    /// <summary>The scheduler's "apocalypse" phase, after the events phase (apocalypse step 3): a resume waiting behind the
    /// drain gate, under the live controls (D12), then the running apocalypse's second inside the same fault guard as every
    /// event's tick (D25).</summary>
    [Mutating]
    internal static void ApocalypseTick(DateTime now)
    {
        // apocalypse D12: a resume waiting behind the drain gate, under the live controls
        if (ApocalypseAction.PendingId is { } waiting)
        {
            var resumed = ApocalypseAction.Resume(Engine, Controls(), now);
            if (resumed is not null || ApocalypseAction.PendingId is null) RemoveInstance(waiting);   // refused or cancelled: its row goes
            if (resumed is not null)
            {
                Persistence.State.Document.Instances.Add(new StateInstance(resumed.Id, resumed.Instance.StartedUtc, resumed.Instance.EndsUtc, "active"));
                Persistence.State.MarkDirty();
            }
        }
        else if (Engine.ResumePending is null) DropStaleResume();
        foreach (var active in Engine.Active.Where(a => EventActions.ActionKindOf(a.Definition) == EventActionKind.Apocalypse).ToList())
            Guarded(active, () => TickApocalypse(active, now));
    }

    /// <summary>The apocalypse's tick inside its own try/catch, as <see cref="Tick"/>'s loop: three faults in a row cancel
    /// it and mark its pillar degraded while the others keep running (D25).</summary>
    static void Guarded(ActiveEvent active, Action work)
    {
        var cancelled = FaultGuard.Run(Engine, active, () =>
            {
#if DEBUG
                if (Settings.FaultInjection.Value == active.Id) throw new InvalidOperationException("Debug.FaultInjection");
#endif
                work();
            },
            line => Core.Log.LogError($"[nyar] {line}"),
            () => End(active.Id, $"cancelled after {EventEngine.FaultLimit} faults", EndPath.Fault));
        if (!cancelled) return;
        var note = $"{active.Definition.Pillar.ToString().ToLowerInvariant()} (event {active.Id} faulted)";
        if (!_degraded.Contains(note)) _degraded.Add(note);
    }

    /// <summary>An event's spawn state beyond its units (event-spawns D13, D30, D33): its hunt seeds, its kept territory
    /// map and its open failure streaks, the arena check's included (swarm D4), go with the event, and so do its region
    /// lock and hunt records and streaks (hunting-grounds D12) and its unit watch rows, tries, clocks and counts (unit-watch
    /// D11), and its scaling decision rows and read streak (scaling D12, D15). Every end path but a restart runs it; a restart
    /// starts with none (SpawnTracker.Initialize, ScalingReads.Clear).</summary>
    static void EndSpawnState(string id)
    {
        HuntAction.EndEvent(id);
        TerritoryMap.Forget(id);
        WalkCheck.Health.EventEnded(id);                                    // A71
        SpawnTracker.ArenaEnded(id);                                        // swarm D4: no arena streak outlives its event
        SpawnTracker.RegionEnded(id);                                       // hunting-grounds D12: nor a last inside spot, target or streak
        SpawnTracker.WatchEnded(id);                                        // unit-watch D11: nor a watch row, try, clock, count or streak
        ScalingReads.EventEnded(id);                                        // scaling D12, D15: nor a decision row or a read streak
    }

    /// <summary>`.nyar pillar &lt;name&gt; off` (event-library D14, S-7): ends every running event of that pillar through the
    /// stop path and returns their ids, in ordinal order.</summary>
    internal static IReadOnlyList<string> EndPillar(Pillar pillar)
    {
        var ended = Engine.Active.Where(a => a.Definition.Pillar == pillar).Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal).ToList()
            .Where(id => End(id, "ended (pillar off)", EndPath.PillarOff)).ToList();
        var waiting = ApocalypseAction.PendingId;
        ApocalypseAction.PillarOff(pillar);                                  // apocalypse D9: a resume in its drain wait too
        if (waiting is not null && ApocalypseAction.PendingId is null) RemoveInstance(waiting);
        TriggerBus.PruneRevenge(DateTime.UtcNow);                           // revenge D8: the pillar's heat goes now
        return ended;
    }

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
            if (ended.Definition.Apocalypse is not null) ApocalypseAction.Ended(id, ApocalypseEndOf(path), cancelled);   // D9
        }
        Announcer.EventEnded(ended.Definition);
        EndScoreboard(path, ended);
        if (path == EndPath.Stop) RevengeEnded(ended, RevengeEndPath.AdminStop);
        else if (path == EndPath.PillarOff) RevengeEnded(ended, RevengeEndPath.PillarOff);
        else if (path == EndPath.Fault) RevengeEnded(ended, RevengeEndPath.Fault);           // logged, silent to the player
        return true;
    }

    /// <summary>The apocalypse end of a cancel path (D9): stop, pillar off, fault; anything else is a stop.</summary>
    static ApocalypseEnd ApocalypseEndOf(EndPath path) => path switch
    {
        EndPath.PillarOff => ApocalypseEnd.PillarOff,
        EndPath.Fault => ApocalypseEnd.Fault,
        _ => ApocalypseEnd.Stop,
    };

    /// <summary>One second of a running apocalypse (D3-D8): a risen horde is a spawned wave (the status row's `wave`); won
    /// or lost ends it now, as a natural end with its grace cleanup, the outcome line and then the scoreboard (D8, D9).</summary>
    static void TickApocalypse(ActiveEvent active, DateTime now)
    {
        var step = ApocalypseAction.Tick(active, now);
        if (step.Rose is not null) Engine.WaveSpawned(active.Id, now, step.Queued);
        if (step.Result is not (ApocalypseResult.Won or ApocalypseResult.Lost)) return;
        var ended = Engine.Complete(active.Id, now, Settings.Limit(Limits.GraceSeconds));
        if (ended is null) return;
        RemoveInstance(ended.Id);
        var (_, cancelled) = SpawnTracker.EndEventUnits(ended.Id, DateTime.MinValue);
        EndSpawnState(ended.Id);
        ApocalypseEnded(ended, step.Result, ended.Definition.Apocalypse!);
        EndScoreboard(ApocalypseOutcome.PathOf(step.Result), ended);
        ApocalypseAction.Ended(ended.Id, EndRule.Of(step.Result), cancelled);   // D15: cancelled orders count failed (review L1)
    }

    /// <summary>The outcome line, logged and announced (D8): "event &lt;id&gt; ended: &lt;won|lost|survived&gt; (&lt;r&gt; of
    /// &lt;c&gt; hordes risen)"; it takes the end banner's place.</summary>
    static void ApocalypseEnded(ActiveEvent ended, ApocalypseResult result, Logic.ApocalypseAction apo)
    {
        Core.Log.LogInfo($"[nyar] {ApocalypseOutcome.Line(ended.Id, result, ended.WavesSpawned, apo.Hordes.Count)}");
        Announcer.Apocalypse(ended.Id, Messages.ApocalypseOutcome(ended.Definition.Name, result, apo.Goal.Name));
    }

    /// <summary>A record left with nothing to resume and no run (a run that ended while the write failed) goes (D9).</summary>
    static void DropStaleResume()
    {
        var doc = Persistence.State.Document;
        if (doc.Apocalypse is { } r && Engine.Find(r.EventId) is null)
        {
            doc.Apocalypse = null;
            Persistence.State.MarkDirty();
        }
    }

    /// <summary>A Revenge instance's end (revenge D8): one "revenge &lt;id&gt; ended: &lt;path&gt;" line, and the private
    /// end line to its focus on a natural end, all waves defeated or an admin stop. Nothing for any other instance.</summary>
    static void RevengeEnded(ActiveEvent ended, RevengeEndPath path)
    {
        if (!RevengeRule.IsRevenge(ended.Definition)) return;
        Core.Log.LogInfo($"[nyar] {RevengeRule.EndedLine(ended.Id, path)}");
        if (RevengeRule.EndPrivate(ended, path) is { } end) TriggerBus.Tell(end.Player, end.Line);
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
