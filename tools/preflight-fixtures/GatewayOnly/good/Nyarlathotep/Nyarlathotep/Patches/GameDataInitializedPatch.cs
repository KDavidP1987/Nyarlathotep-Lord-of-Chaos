using System;
using HarmonyLib;
using ProjectM;

namespace Nyarlathotep.Patches;

/// <summary>
/// Fires Core.TryInitialize once the server's persistence load completes —
/// the earliest point where the Server world + PrefabCollectionSystem are
/// reliably populated. TryInitialize is idempotent, so repeat OnUpdate calls
/// after IsReady are no-ops. (Same trigger Faust/Uriel/Beelzebub use.)
/// This is the one patch that runs before Core.IsReady; its guard is the inverse
/// (return once ready) and, like every patch, it never lets an exception escape.
/// </summary>
[HarmonyPatch(typeof(SpawnTeamSystem_OnPersistenceLoad), nameof(SpawnTeamSystem_OnPersistenceLoad.OnUpdate))]
internal static class GameDataInitializedPatch
{
    [HarmonyPostfix]
    public static void OneShotInit()
    {
        if (Core.IsReady) return;
        try
        {
            Core.TryInitialize(nameof(GameDataInitializedPatch));
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"GameDataInitializedPatch failed: {ex}");
        }
    }
}

/// <summary>
/// The second init trigger (foundation A5): a brand-new world has no save to load, so
/// SpawnTeamSystem_OnPersistenceLoad never runs and the mod stayed uninitialised until a restart.
/// LoadPersistenceSystemV2.SetLoadState(SuccessfulStartup) fires on every startup, new world or not
/// (the signal BloodyCore and RaidForge use). TryInitialize is idempotent, so whichever fires first wins.
/// </summary>
[HarmonyPatch(typeof(LoadPersistenceSystemV2), nameof(LoadPersistenceSystemV2.SetLoadState))]
internal static class ServerStartupPatch
{
    [HarmonyPostfix]
    public static void OnLoadState(ServerStartupState.State loadState)
    {
        if (Core.IsReady) return;
        try
        {
            if (loadState == ServerStartupState.State.SuccessfulStartup)
                Core.TryInitialize(nameof(ServerStartupPatch));
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"ServerStartupPatch failed: {ex}");
        }
    }
}
