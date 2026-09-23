using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using Nyarlathotep.Config;
using VampireCommandFramework;

namespace Nyarlathotep;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    internal static Harmony Harmony;
    internal static ManualLogSource PluginLog;
    internal static Plugin Instance { get; private set; }

    public override void Load()
    {
        // Nyarlathotep is server-only: never initialize on a game client.
        if (Application.productName != "VRisingServer") return;

        Instance = this;
        PluginLog = Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} loading...");

        Settings.Initialize(Config);

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        int patchCount = System.Linq.Enumerable.Count(Harmony.GetPatchedMethods());
        Log.LogInfo($"Harmony patches applied: {patchCount} method(s) patched.");

        CommandRegistry.RegisterAll();

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} loaded. Awaiting game data init.");
    }

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        // TODO(foundation): end active events, despawn tracked units, and flush persistence here
        // (see docs/NYARLATHOTEP_DESIGN.md §"Lifecycle & cleanup").
        Harmony?.UnpatchSelf();
        return true;
    }
}
