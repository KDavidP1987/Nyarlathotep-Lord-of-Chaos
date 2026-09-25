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
    static DayNightEdges _edges = new();
    static EntityQuery _dayNight;
    static readonly FailureStreak _dayNightFaults = new();

    internal static HookSet Hooks => _hooks;

    internal static void Initialize()
    {
        _edges = new DayNightEdges();
        _hooks = new HookSet(new Registry(), line => Core.Log.LogWarning($"[nyar] {line}"));
        _hooks.RegisterAll();
        var down = _hooks.Unavailable.Count == 0 ? "all hooks available" : $"unavailable: {string.Join(", ", _hooks.Unavailable)}";
        Core.Log.LogInfo($"[nyar] triggers: {down}");
    }

    /// <summary>Each scheduler tick: Schedule definitions due this server-local minute (the occurrence is recorded in
    /// state.json whether or not the start is allowed, so it is never retried or replayed, D4), then the day/night edge.</summary>
    internal static void Tick(DateTime now)
    {
        var set = EventStore.Catalog.Current;
        var fired = Persistence.State.Document.LastFired;
        foreach (var (def, occurrence) in TriggerRouter.ScheduleDue(set, now, TimeZoneInfo.Local,
                     id => fired.TryGetValue(id, out var last) ? last.Occurrence : null))
        {
            fired[def.Id] = new LastFired(occurrence, now);
            Persistence.State.MarkDirty();
            Fire(def, $"Schedule {occurrence}");
        }

        if (!_hooks.IsAvailable(Hook.DayNight)) return;
        bool isDay;
        try
        {
            // DayNightCycle.TimeOfDay (A14): the plan named DayNightCycleExtensions.IsDay, which the pinned assemblies lack.
            isDay = _dayNight.GetSingleton<DayNightCycle>().TimeOfDay == TimeOfDay.Day;
            _dayNightFaults.Ok();
        }
        catch (Exception ex)
        {
            if (_dayNightFaults.Fail()) Core.Log.LogError($"[nyar] day/night read failed: {ex.Message}; GameTime triggers wait");
            return;
        }
        if (_edges.Sample(isDay) is not { } phase) return;
        Core.Log.LogInfo($"[nyar] trigger: GameTime {phase.ToString().ToLowerInvariant()} began");
        foreach (var def in TriggerRouter.PhaseEntered(set, phase))
            if (_dedupe.ShouldFire($"{def.Id}|GameTime {phase}", now)) Fire(def, $"GameTime {phase.ToString().ToLowerInvariant()}");
    }

    /// <summary>A V Blood died (Patches/DeathEventPatch). <paramref name="prefab"/> is its prefab name.</summary>
    internal static void VBloodKilled(string prefab)
    {
        Core.Log.LogInfo($"[nyar] trigger: VBloodKilled {prefab}");
        if (!_hooks.AllowsTrigger(TriggerType.VBloodKilled)) return;
        var now = DateTime.UtcNow;
        foreach (var def in TriggerRouter.VBloodKilled(EventStore.Catalog.Current, prefab))
            if (_dedupe.ShouldFire($"{def.Id}|VBloodKilled {prefab}", now)) Fire(def, $"VBloodKilled {prefab}");
    }

    static void Fire(EventDefinition def, string trigger) =>
        Gateway.Run(ActionKind.StartEvent, Actor.System, () => EventRuntime.StartEvent(def.Id, trigger, Actor.System, null), def.Startable);

    /// <summary>A hook is available when what it attaches to exists: the DeathEvent patch applied, the DayNightCycle
    /// singleton present, the ServerBootstrapSystem login patch applied (it comes with the Announcer in step 6; until then
    /// the hook reports unavailable). In a Debug build,
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
                    if (!Plugin.Harmony.GetPatchedMethods().Any(m => m.DeclaringType == typeof(ServerBootstrapSystem)))
                        throw new InvalidOperationException("ServerBootstrapSystem.OnUserConnected is not patched");
                    break;
            }
        }

        static string SystemName(Hook hook) => hook switch
        {
            Hook.DeathEvent => nameof(DeathEventListenerSystem),
            Hook.DayNight => nameof(DayNightCycle),
            _ => nameof(ServerBootstrapSystem),
        };
    }
}
