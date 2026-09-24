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
            System.Linq.Enumerable.Where(LoadableTypes(), HasCommands),
            t => (t.Name, (System.Action)(() => CommandRegistry.RegisterCommandType(t))));
        Logic.CommandGroups.RegisterEach(groups, line => Log.LogWarning($"[nyar] {line}"));

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} loaded. Awaiting game data init.");
    }

    // Enumerated lazily inside RegisterEach's guarded loop; a type whose reflection fails is logged and skipped.
    static System.Collections.Generic.IEnumerable<System.Type> LoadableTypes()
    {
        try { return typeof(Plugin).Assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            PluginLog.LogWarning($"[nyar] some types could not be loaded ({ex.Message}); registering commands from the rest");
            return System.Linq.Enumerable.Where(ex.Types, t => t is not null);
        }
    }

    static bool HasCommands(System.Type type)
    {
        try
        {
            return System.Linq.Enumerable.Any(type.GetMethods(), m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<CommandAttribute>(m) is not null);
        }
        catch (System.Exception ex)
        {
            PluginLog.LogWarning($"[nyar] command discovery skipped {type.Name} ({ex.Message})");
            return false;
        }
    }

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        Core.StopTick();
        Services.Persistence.Shutdown();
        Harmony?.UnpatchSelf();
        return true;
    }
}
