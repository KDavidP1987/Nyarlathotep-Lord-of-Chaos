using System.Globalization;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient): admin commands that let an automated in-game test put the character at a known spot.
//   .devwhere              reply + log  [DEV WHERE] x=<x> y=<y> z=<z>
//   .devtp <x> <z> [y]     teleport (the game's own admin teleport event); log [DEV TP] ...
//   .devmark <name>        remember the current spot in BepInEx/config/NyarDevTools/marks.json
//   .devgo <name>          teleport to a remembered spot;  .devmarks lists them
// Positions are logged on purpose: this plugin runs only on the local dev server and is never shipped.
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    internal static ManualLogSource Logger;

    public override void Load()
    {
        if (Application.productName != "VRisingServer") return;
        Logger = Log;
        CommandRegistry.RegisterAll();
        Log.LogInfo("NyarDevTools loaded: .devwhere .devtp .devmark .devgo .devmarks");
    }

    public override bool Unload() { CommandRegistry.UnregisterAssembly(); return true; }
}

static class DevCommands
{
    static readonly string MarksPath = Path.Combine(Paths.ConfigPath, "NyarDevTools", "marks.json");
    static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    static EntityManager Em
    {
        get
        {
            foreach (var w in World.s_AllWorlds) if (w.Name == "Server") return w.EntityManager;
            throw new InvalidOperationException("no Server world");
        }
    }

    static float3? Where(ChatCommandContext ctx)
    {
        var em = Em;
        var c = ctx.Event.SenderCharacterEntity;
        if (!em.Exists(c) || !em.HasComponent<Translation>(c)) return null;
        return em.GetComponentData<Translation>(c).Value;
    }

    static void Teleport(ChatCommandContext ctx, float3 to, string label)
    {
        var em = Em;
        var e = em.CreateEntity(ComponentType.ReadWrite<FromCharacter>(), ComponentType.ReadWrite<PlayerTeleportDebugEvent>());
        em.SetComponentData(e, new FromCharacter { User = ctx.Event.SenderUserEntity, Character = ctx.Event.SenderCharacterEntity });
        em.SetComponentData(e, new PlayerTeleportDebugEvent { Position = to, Target = PlayerTeleportDebugEvent.TeleportTarget.Self });
        Plugin.Logger.LogInfo($"[DEV TP] {label} x={F(to.x)} y={F(to.y)} z={F(to.z)}");
        ctx.Reply($"devtp {label} x={F(to.x)} y={F(to.y)} z={F(to.z)}");
    }

    static Dictionary<string, float[]> LoadMarks()
    {
        try { return File.Exists(MarksPath) ? JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(MarksPath)) ?? new() : new(); }
        catch { return new(); }
    }

    [Command("devwhere", adminOnly: true)]
    public static void DevWhere(ChatCommandContext ctx)
    {
        var p = Where(ctx);
        if (p is null) { ctx.Reply("devwhere: no position"); return; }
        var v = p.Value;
        Plugin.Logger.LogInfo($"[DEV WHERE] x={F(v.x)} y={F(v.y)} z={F(v.z)}");
        ctx.Reply($"devwhere x={F(v.x)} y={F(v.y)} z={F(v.z)}");
    }

    [Command("devtp", adminOnly: true)]
    public static void DevTp(ChatCommandContext ctx, float x, float z, float y = float.NaN)
    {
        var p = Where(ctx);
        Teleport(ctx, new float3(x, float.IsNaN(y) ? (p?.y ?? 0f) : y, z), "to");
    }

    [Command("devmark", adminOnly: true)]
    public static void DevMark(ChatCommandContext ctx, string name)
    {
        var p = Where(ctx);
        if (p is null) { ctx.Reply("devmark: no position"); return; }
        var marks = LoadMarks();
        marks[name] = new[] { p.Value.x, p.Value.y, p.Value.z };
        Directory.CreateDirectory(Path.GetDirectoryName(MarksPath)!);
        File.WriteAllText(MarksPath, JsonSerializer.Serialize(marks, new JsonSerializerOptions { WriteIndented = true }));
        Plugin.Logger.LogInfo($"[DEV MARK] {name} x={F(p.Value.x)} y={F(p.Value.y)} z={F(p.Value.z)}");
        ctx.Reply($"devmark {name} saved");
    }

    [Command("devgo", adminOnly: true)]
    public static void DevGo(ChatCommandContext ctx, string name)
    {
        if (!LoadMarks().TryGetValue(name, out var m) || m.Length != 3) { ctx.Reply($"devgo: no mark {name}"); return; }
        Teleport(ctx, new float3(m[0], m[1], m[2]), name);
    }

    [Command("devmarks", adminOnly: true)]
    public static void DevMarks(ChatCommandContext ctx)
    {
        var marks = LoadMarks();
        ctx.Reply(marks.Count == 0 ? "devmarks: none" : "devmarks: " + string.Join(", ", marks.Keys.OrderBy(k => k)));
    }
}
