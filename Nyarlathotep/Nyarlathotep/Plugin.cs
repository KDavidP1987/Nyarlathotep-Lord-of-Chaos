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

        // One registration per command class, so a class that fails is logged and the rest register (D9).
        var groups = System.Linq.Enumerable.Select(
            System.Linq.Enumerable.Where(typeof(Plugin).Assembly.GetTypes(), HasCommands),
            t => (t.Name, (System.Action)(() => CommandRegistry.RegisterCommandType(t))));
        Logic.CommandGroups.RegisterEach(groups, line => Log.LogWarning($"[nyar] {line}"));

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} loaded. Awaiting game data init.");
    }

    static bool HasCommands(System.Type type) =>
        System.Linq.Enumerable.Any(type.GetMethods(), m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<CommandAttribute>(m) is not null);

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        // TODO(foundation step 4): stop the scheduler coroutine before the flush.
        Services.Persistence.Shutdown();
        Harmony?.UnpatchSelf();
        return true;
    }
}
