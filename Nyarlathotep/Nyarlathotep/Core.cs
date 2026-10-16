using System;
using System.Collections;
using Il2CppInterop.Runtime;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using ProjectM.Terrain;
using Unity.Entities;
using UnityEngine;

namespace Nyarlathotep;

/// <summary>
/// Deferred-initialization hub. V Rising's ECS systems (TypeManager, PrefabCollectionSystem)
/// are NOT ready at Plugin.Load — anything that touches Il2CppType.Of&lt;T&gt; or prefab data
/// must wait until the server world exists and prefabs are populated. Patches call
/// <see cref="TryInitialize"/>; it no-ops until the world is actually ready.
/// (Pattern proven in Faust / Uriel / Beelzebub / KindredCommands.)
/// </summary>
internal static class Core
{
    public static World Server { get; private set; }
    public static EntityManager EntityManager { get; private set; }
    public static PrefabCollectionSystem PrefabCollectionSystem { get; private set; }
    public static ServerScriptMapper ServerScriptMapper { get; private set; }
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; private set; }
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static double ServerTime => ServerGameManager.ServerTime;

    public static ManualLogSource Log => Plugin.PluginLog;
    public static bool IsReady { get; private set; }

    static bool _initInProgress;
    static int _initAttempts;

    internal static void TryInitialize(string trigger)
    {
        if (IsReady || _initInProgress) return;
        _initInProgress = true;
        _initAttempts++;
        try
        {
            var server = FindServerWorld();
            if (server is null)
            {
                if (_initAttempts == 1)
                    Log.LogInfo($"Nyarlathotep init ({trigger}): Server world not yet present; will retry.");
                return;
            }

            var prefabSystem = server.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabSystem is null || prefabSystem.SpawnableNameToPrefabGuidDictionary.Count == 0)
            {
                if (_initAttempts == 1)
                    Log.LogInfo($"Nyarlathotep init ({trigger}): PrefabCollectionSystem not yet populated; will retry.");
                return;
            }

            if (!WorldDataReady(server, trigger)) return;

            Server = server;
            EntityManager = server.EntityManager;
            PrefabCollectionSystem = prefabSystem;
            ServerScriptMapper = server.GetExistingSystemManaged<ServerScriptMapper>();
            ServerGameSettingsSystem = server.GetExistingSystemManaged<ServerGameSettingsSystem>();

            // Services in dependency order (docs/dod/foundation.md › Design › States › Startup and shutdown):
            // Persistence → RegionMap → EventStore → SpawnTracker → EmpowerAction → TriggerBus → EventRuntime → Announcer → Pusher → HealthMonitor
            // → EventScheduler.
            Services.Persistence.Initialize();
            Services.RegionMap.Initialize();                              // before the definitions are applied (regions D7)
            Services.EventStore.Initialize();
            Services.TemplateLibrary.Initialize();
            Services.SpawnTracker.Initialize();
            Services.EmpowerAction.Initialize();
            Services.TriggerBus.Initialize();
            Services.EventRuntime.Initialize();
            Services.Announcer.Initialize();
            Services.Pusher.Initialize();
            Services.HealthMonitor.Initialize();
            // The scheduler starts before the sweep, so a sweep that throws never leaves the queues without a tick; it
            // does nothing until IsReady.
            Services.EventScheduler.Start();
            try { Services.SpawnTracker.BootSweep(); }
            catch (System.Exception ex) { Log.LogError($"[nyar] boot sweep failed: {ex.Message}; marked survivors expire on their own LifeTime"); }

            IsReady = true;
            Log.LogInfo($"Nyarlathotep initialized via {trigger} (attempt #{_initAttempts}). Prefab map has {prefabSystem.SpawnableNameToPrefabGuidDictionary.Count} entries.");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Nyarlathotep init ({trigger}) FAILED on attempt #{_initAttempts}: {ex}");
        }
        finally
        {
            _initInProgress = false;
        }
    }

    static DateTime? _worldWaitSince;
    static bool _worldWaitRetrying;

    /// <summary>Epic A42: true once the DayNightCycle singleton and at least one region polygon exist, or once
    /// <see cref="Logic.WorldWait.LimitSeconds"/> have passed (then init goes on without them, degraded as before). While
    /// waiting, a coroutine retries <see cref="TryInitialize"/> each second, since a brand-new world fires its one
    /// startup trigger before that data is there.</summary>
    static bool WorldDataReady(World server, string trigger)
    {
        var em = server.EntityManager;
        var dayNight = Count(em, ComponentType.ReadOnly(Il2CppType.Of<DayNightCycle>()), EntityQueryOptions.Default) == 1;    // TriggerBus's DayNight hook test
        var regions = Count(em, ComponentType.ReadOnly(Il2CppType.Of<WorldRegionPolygon>()), EntityQueryOptions.IncludeDisabled) > 0;   // RegionMap's read
        var now = DateTime.UtcNow;
        var waited = _worldWaitSince is { } since ? (now - since).TotalSeconds : 0;
        var missing = Logic.WorldWait.Missing(dayNight, regions);
        switch (Logic.WorldWait.Decide(dayNight, regions, waited))
        {
            case Logic.WorldWaitStep.Init:
                if (_worldWaitSince is not null) Log.LogInfo($"[nyar] world data ready after {waited:0} s");
                return true;
            case Logic.WorldWaitStep.InitWithout:
                Log.LogWarning($"[nyar] world data still missing after {Logic.WorldWait.LimitSeconds} s ({missing}); initialising without it");
                return true;
            default:
                if (_worldWaitSince is null)
                {
                    _worldWaitSince = now;
                    Log.LogInfo($"[nyar] init ({trigger}): world data not loaded yet ({missing}); retrying each second for up to {Logic.WorldWait.LimitSeconds} s");
                }
                if (!_worldWaitRetrying)
                {
                    _worldWaitRetrying = true;
                    StartCoroutine(RetryUntilReady());
                }
                return false;
        }
    }

    static IEnumerator RetryUntilReady()
    {
        var wait = new WaitForSeconds(1f);
        while (!IsReady)
        {
            yield return wait;
            TryInitialize("world data wait");
        }
    }

    static int Count(EntityManager em, ComponentType type, EntityQueryOptions options)
    {
        var query = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { type }, Options = options });
        try { return query.CalculateEntityCount(); }
        finally { query.Dispose(); }
    }

    static World FindServerWorld()
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == "Server") return world;
        }
        return null;
    }

    // ---- Coroutine host: a persistent GameObject whose MonoBehaviour drives Unity coroutines on the
    //      server's main thread (the reliable way to run periodic work). Reuses ProjectM's
    //      IgnorePhysicsDebugSystem so no custom Il2Cpp type needs registering. (Pattern from Bloodcraft / Faust.) ----
    static MonoBehaviour _monoBehaviour;

    static MonoBehaviour MonoBehaviour
    {
        get
        {
            if (_monoBehaviour == null)
            {
                _monoBehaviour = new GameObject("Nyarlathotep").AddComponent<IgnorePhysicsDebugSystem>();
                UnityEngine.Object.DontDestroyOnLoad(_monoBehaviour.gameObject);
            }
            return _monoBehaviour;
        }
    }

    /// <summary>Run a managed coroutine on the server's main thread (wrapped for Il2Cpp).</summary>
    public static Coroutine StartCoroutine(IEnumerator routine) =>
        MonoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());

    /// <summary>Stops a coroutine started by <see cref="StartCoroutine"/>; nothing when the host is gone.</summary>
    public static void StopCoroutine(Coroutine routine)
    {
        if (_monoBehaviour != null) _monoBehaviour.StopCoroutine(routine);
    }
}
