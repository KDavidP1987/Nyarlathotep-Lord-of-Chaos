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
