using HarmonyLib;
using ProjectM;

namespace Nyarlathotep.Patches;

/// <summary>
/// Fires Core.TryInitialize once the server's persistence load completes —
/// the earliest point where the Server world + PrefabCollectionSystem are
/// reliably populated. TryInitialize is idempotent, so repeat OnUpdate calls
/// after IsReady are no-ops. (Same trigger Faust/Uriel/Beelzebub use.)
/// </summary>
[HarmonyPatch(typeof(SpawnTeamSystem_OnPersistenceLoad), nameof(SpawnTeamSystem_OnPersistenceLoad.OnUpdate))]
internal static class GameDataInitializedPatch
{
    [HarmonyPostfix]
    public static void OneShotInit()
    {
        Core.TryInitialize(nameof(GameDataInitializedPatch));
    }
}
