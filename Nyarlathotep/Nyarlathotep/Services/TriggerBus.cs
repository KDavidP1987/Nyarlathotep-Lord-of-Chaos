namespace Nyarlathotep.Services;

/// <summary>
/// Where game hooks report what happened (foundation Design › Startup: Services/TriggerBus). Step 4 raises only
/// VBloodKilled from Patches/DeathEventPatch and logs it; step 5 adds the Manual, Schedule and GameTime triggers, the
/// hook registry and the dispatch to EventRuntime.
/// </summary>
internal static class TriggerBus
{
    /// <summary>A V Blood died. <paramref name="prefab"/> is its prefab name.</summary>
    internal static void VBloodKilled(string prefab) =>
        Core.Log.LogInfo($"[nyar] trigger: VBloodKilled {prefab}");
}
