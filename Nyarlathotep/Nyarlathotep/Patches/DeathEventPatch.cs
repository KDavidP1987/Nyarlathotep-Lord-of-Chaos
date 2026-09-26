using System;
using HarmonyLib;
using Nyarlathotep.Services;
using ProjectM;
using Unity.Collections;

namespace Nyarlathotep.Patches;

/// <summary>
/// Reads the frame's DeathEvents (DEV_REMINDERS #26): a dead unit of ours leaves the spawn ledger, and a V Blood kill
/// (Logic/DeathRule) raises VBloodKilled on the TriggerBus. The system runs only when something dies (DEV_REMINDERS #28), so nothing
/// ticks from here.
/// </summary>
[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
internal static class DeathEventPatch
{
    static readonly Logic.FailureStreak Faults = new();

    [HarmonyPostfix]
    public static void OnUpdate(DeathEventListenerSystem __instance)
    {
        if (!Core.IsReady) return;
        try
        {
            var deaths = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
            try
            {
                foreach (var death in deaths)
                {
                    SpawnTracker.Died(death.Died);
                    // faction-empowerment D13: a V Blood kill carries VBloodConsumeSource; a gate boss with VBloodUnit alone
                    // raises nothing (DEV_REMINDERS #26).
                    if (Logic.DeathRule.IsVBloodKill(death.Died.Has<VBloodConsumeSource>(), death.Died.Has<VBloodUnit>()))
                        TriggerBus.VBloodKilled(death.Died.GetPrefabGuid().GetPrefabName());
                }
            }
            finally
            {
                deaths.Dispose();
            }
            Faults.Ok();
        }
        catch (Exception ex)
        {
            // Once per failure streak: in a large fight this runs every frame.
            if (Faults.Fail()) Core.Log.LogError($"[nyar] death event read failed: {ex.Message}");
        }
    }
}
