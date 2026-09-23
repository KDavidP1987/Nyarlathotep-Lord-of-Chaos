using System;
using HarmonyLib;
using ProjectM;

namespace Nyarlathotep.Patches;

[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
internal static class ExamplePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        var ready = Core.IsReady;
        try
        {
            Core.Log.LogInfo("example");
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"ExamplePatch failed: {ex}");
        }
    }
}
