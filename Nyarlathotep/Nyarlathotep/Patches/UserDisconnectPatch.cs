using System;
using HarmonyLib;
using Nyarlathotep.Services;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;

namespace Nyarlathotep.Patches;

/// <summary>
/// The disconnect hook (raphael-api-core D12; Interfaces › External: ServerBootstrapSystem.OnUserDisconnected): the
/// leaving user's push subscription ends. A Prefix, so the approved-user index still holds the connection; the user
/// is found as UserConnectPatch finds it. TriggerBus reports the UserDisconnect hook available once this patch is
/// applied; when it is not, the offline prune at the next push removes the entry instead (D5).
/// </summary>
[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
internal static class UserDisconnectPatch
{
    static readonly Logic.FailureStreak Faults = new();

    [HarmonyPrefix]
    public static void OnUserDisconnected(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        if (!Core.IsReady) return;
        try
        {
            var index = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
            var user = Core.EntityManager.GetComponentData<User>(__instance._ApprovedUsersLookup[index].UserEntity);
            Pusher.Disconnected(user.PlatformId);
            Faults.Ok();
        }
        catch (Exception ex)
        {
            if (Faults.Fail()) Core.Log.LogError($"[nyar] disconnect hook failed: {ex.Message}");
        }
    }
}
