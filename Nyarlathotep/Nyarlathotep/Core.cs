using System.Collections;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
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

            Server = server;
            EntityManager = server.EntityManager;
            PrefabCollectionSystem = prefabSystem;
            ServerScriptMapper = server.GetExistingSystemManaged<ServerScriptMapper>();
            ServerGameSettingsSystem = server.GetExistingSystemManaged<ServerGameSettingsSystem>();

            // Services in dependency order (docs/dod/foundation.md › Design › States › Startup and shutdown):
            // Persistence → EventStore → SpawnTracker → TriggerBus → EventRuntime → Announcer → HealthMonitor
            // → EventScheduler. Later build steps add the rest.
            Services.Persistence.Initialize();
            Services.EventStore.Initialize();
            Services.SpawnTracker.Initialize();

            IsReady = true;
            Log.LogInfo($"Nyarlathotep initialized via {trigger} (attempt #{_initAttempts}). Prefab map has {prefabSystem.SpawnableNameToPrefabGuidDictionary.Count} entries.");

            Services.SpawnTracker.BootSweep();
            // Temporary 1 s tick for the spawn and despawn queues; step 5's EventScheduler replaces it.
            _tick = StartCoroutine(TickLoop());
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

    static Coroutine _tick;
    static readonly Logic.FailureStreak _tickFaults = new();

    static IEnumerator TickLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            try
            {
                Services.SpawnTracker.Tick();
                _tickFaults.Ok();
            }
            catch (System.Exception ex)
            {
                if (_tickFaults.Fail()) Log.LogError($"[nyar] tick failed: {ex.Message}; retrying every second");
            }
        }
    }

    /// <summary>Plugin.Unload: stop the tick before the final state flush.</summary>
    internal static void StopTick()
    {
        if (_tick is not null && _monoBehaviour != null) _monoBehaviour.StopCoroutine(_tick);
        _tick = null;
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
}
