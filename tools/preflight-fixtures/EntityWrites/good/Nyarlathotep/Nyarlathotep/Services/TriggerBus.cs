using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Logic;
using ProjectM;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// Where triggers become starts (foundation D29, D40; Design › Startup: fourth in Core.TryInitialize). The hooks are
/// registered through Logic/IHookRegistry, each on its own, so an unavailable one disables only the triggers that need
/// it (D9, D31). Each second the scheduler asks for the Schedule minute and the day/night edge; the DeathEvent patch
/// reports a dead V Blood. Logic/TriggerRouter picks the enabled definitions a trigger reaches, a trigger from the same
/// source within 5 s fires once (D6), and every start runs as System through the gateway (D10, D11).
/// </summary>
internal static class TriggerBus
{
    static HookSet _hooks = new(new Registry(), _ => { });
    static readonly TriggerDedupe _dedupe = new();
    static PhaseSampler _phases = NewSampler();
    static EntityQuery _dayNight;

    internal static HookSet Hooks => _hooks;

    internal static void Initialize()
    {
        _phases = NewSampler();
        _hooks = new HookSet(new Registry(), line => Core.Log.LogWarning($"[nyar] {line}"));
        _hooks.RegisterAll();
        var down = _hooks.Unavailable.Count == 0 ? "all hooks available" : $"unavailable: {string.Join(", ", _hooks.Unavailable)}";
        Core.Log.LogInfo($"[nyar] triggers: {down}");
    }

    /// <summary>Each scheduler tick: Schedule definitions due this server-local minute (the occurrence is recorded in
    /// state.json whether or not the start is allowed, so it is never retried or replayed, D4), then the day/night edge.</summary>
    internal static void Poll(DateTime now)
    {
        var fired = Persistence.State.Document.LastFired;
        var due = TriggerTick.Collect(EventStore.Catalog.Current, now, TimeZoneInfo.Local,
            id => fired.TryGetValue(id, out var last) ? last.Occurrence : null, _hooks.IsAvailable(Hook.DayNight) ? _phases : null);
        foreach (var (def, occurrence) in due.Scheduled)
        {
            fired[def.Id] = new LastFired(occurrence, now);
            Persistence.State.MarkDirty();
            Fire(def, $"Schedule {occurrence}");
        }

        if (due.Entered is not { } phase) return;
        Core.Log.LogInfo($"[nyar] trigger: GameTime {phase.ToString().ToLowerInvariant()} began");
        foreach (var def in due.PhaseStarts)
            if (_dedupe.ShouldFire($"{def.Id}|GameTime {phase}", now)) Fire(def, $"GameTime {phase.ToString().ToLowerInvariant()}");
    }

    /// <summary>The day and night read through Logic's PhaseSampler (event-library A5, D19): a read that throws logs once
    /// per failure streak and starts no GameTime event, and the tick's Schedule starts still fire.</summary>
    // DayNightCycle.TimeOfDay (A14): the plan named DayNightCycleExtensions.IsDay, which the pinned assemblies lack.
    static PhaseSampler NewSampler() => new(() => _dayNight.GetSingleton<DayNightCycle>().TimeOfDay == TimeOfDay.Day,
        line => Core.Log.LogError($"[nyar] {line}"));

    static readonly FailureStreak _killPositionFaults = new();

    /// <summary>A V Blood died (Patches/DeathEventPatch). <paramref name="prefab"/> is its prefab name and
    /// <paramref name="kill"/> its x/z, null when unreadable: then only Global definitions start, and "vblood kill:
    /// position unreadable" is logged once per streak when a scoped one was reached (regions D4).</summary>
    internal static void VBloodKilled(string prefab, Func<(float X, float Z)?> readKill)
    {
        Core.Log.LogInfo($"[nyar] trigger: VBloodKilled {prefab}");
        if (!_hooks.AllowsTrigger(TriggerType.VBloodKilled)) return;
        var now = DateTime.UtcNow;
        var set = EventStore.Catalog.Current;
        var kill = TriggerRouter.KillFor(set, prefab, readKill);                  // read only for a scoped definition (A38)
        if (TriggerRouter.UnreadableKillLogs(set, prefab, kill, _killPositionFaults)) Core.Log.LogWarning("[nyar] vblood kill: position unreadable");
        var regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : (Func<float, float, string>)null;
        foreach (var def in TriggerRouter.VBloodKilled(set, prefab, kill, regionOf))
            if (_dedupe.ShouldFire($"{def.Id}|VBloodKilled {prefab}", now))
                Fire(def, $"VBloodKilled {prefab}", def.Trigger.Scope.IsGlobal ? null : kill);
    }

    static void Fire(EventDefinition def, string trigger, (float X, float Z)? kill = null) =>
        Gateway.Run(ActionKind.StartEvent, Actor.System, () => EventRuntime.StartEvent(def.Id, trigger, Actor.System, null, kill), def.Startable);

    /// <summary>A hook is available when what it attaches to exists: the DeathEvent patch applied, the DayNightCycle
    /// singleton present, the ServerBootstrapSystem connect and disconnect patches (Patches/UserConnectPatch, UserDisconnectPatch) applied. In a Debug build,
    /// Debug.FaultInjection = hook:&lt;name&gt; makes that hook report unavailable (D31).</summary>
    sealed class Registry : IHookRegistry
    {
        public void Register(Hook hook)
        {
#if DEBUG
            var fault = Config.Settings.FaultInjection?.Value ?? "";
            if (fault == $"hook:{hook}" || fault == $"hook:{SystemName(hook)}") throw new InvalidOperationException("Debug.FaultInjection");
#endif
            switch (hook)
            {
                case Hook.DeathEvent:
                    if (!Plugin.Harmony.GetPatchedMethods().Any(m => m.DeclaringType == typeof(DeathEventListenerSystem)))
                        throw new InvalidOperationException("DeathEventListenerSystem.OnUpdate is not patched");
                    break;
                case Hook.DayNight:
                    _dayNight = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<DayNightCycle>()));
                    if (_dayNight.CalculateEntityCount() != 1) throw new InvalidOperationException("DayNightCycle singleton not found");
                    break;
                case Hook.UserConnect:
                    RequirePatched(nameof(ServerBootstrapSystem.OnUserConnected));
                    break;
                case Hook.UserDisconnect:
                    RequirePatched(nameof(ServerBootstrapSystem.OnUserDisconnected));
                    break;
            }
        }

        // Each login hook checks its own method, so one applied patch never reports the other available.
        static void RequirePatched(string method)
        {
            if (!Plugin.Harmony.GetPatchedMethods().Any(m => m.DeclaringType == typeof(ServerBootstrapSystem) && m.Name == method))
                throw new InvalidOperationException($"ServerBootstrapSystem.{method} is not patched");
        }

        static string SystemName(Hook hook) => hook switch
        {
            Hook.DeathEvent => nameof(DeathEventListenerSystem),
            Hook.DayNight => nameof(DayNightCycle),
            _ => nameof(ServerBootstrapSystem),
        };
    }
}
