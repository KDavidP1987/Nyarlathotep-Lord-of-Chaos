using System;
using HarmonyLib;
using Nyarlathotep.Services;
using ProjectM;
using Stunlock.Network;

namespace Nyarlathotep.Patches;

/// <summary>
/// The login hook (foundation D31, Interfaces › External: ServerBootstrapSystem.OnUserConnected): hands the connecting
/// user to the Announcer for the admin degraded notice. The user is found as KindredCommands'
/// Patches/PlayerConnectivityPatches.cs does, through the approved-user index of the connection. TriggerBus reports
/// the UserConnect hook available once this patch is applied.
/// </summary>
[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
internal static class UserConnectPatch
{
    static readonly Logic.FailureStreak Faults = new();

    [HarmonyPostfix]
    public static void OnUserConnected(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        if (!Core.IsReady) return;
        try
        {
            var index = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
            Announcer.UserConnected(__instance._ApprovedUsersLookup[index].UserEntity);
            Faults.Ok();
        }
        catch (Exception ex)
        {
            if (Faults.Fail()) Core.Log.LogError($"[nyar] login hook failed: {ex.Message}");
        }
    }
}
