using System.Globalization;
using ProjectM;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), undead-hordes D24: an apocalypse spreads only on the death of a unit that is not the mod's
// own, and `.nyar spawn` units are tracked, so the spread test needs natives the mod never saw.
//   .devnative <prefabGuid> [count] [lifetime] [x z]  spawns count (default 3, at most 10) units of that prefab round the
//                          sender, or round (x, z) at the ground height of the sender's level when both are given,
//                          4 m apart (a sender who teleports away from natives in combat with them ends them, so
//                          horde-march.vrs spawns them at a point while Chaos waits off the route), with a LifeTime of lifetime seconds (default 300, at most 900) that destroys them,
//                          Age so it counts down, and DestroyWhenDisabled; log [DEV NATIVE] n=<count> x=<x> z=<z>
// They are never registered with the mod. The session's dev-snapshot restore is the backstop. Never in the mod.
static class DevNative
{
    static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    [Command("devnative", adminOnly: true)]
    public static void Spawn(ChatCommandContext ctx, int prefab, int count = 3, int lifetime = 300, float x = float.NaN, float z = float.NaN)
    {
        count = Math.Clamp(count, 1, 10);
        lifetime = Math.Clamp(lifetime, 30, 900);
        var p = DevCommands.Where(ctx);
        if (p is null) { ctx.Reply("devnative: no position"); return; }
        if (!float.IsNaN(x) && !float.IsNaN(z)) p = new float3(x, p.Value.y, z);
        var em = DevCommands.Em;
        ServerGameManager sgm = default;
        foreach (var w in World.s_AllWorlds)
            if (w.Name == "Server") sgm = w.GetExistingSystemManaged<ServerScriptMapper>().GetServerGameManager();
        var n = 0;
        for (var i = 0; i < count; i++)
        {
            var unit = sgm.InstantiateEntityImmediate(Entity.Null, new PrefabGUID(prefab));
            if (!em.Exists(unit)) continue;
            var at = p.Value + new float3(4f * (i - (count - 1) / 2f), 0, 4f);
            if (!em.HasComponent<LifeTime>(unit)) em.AddComponent<LifeTime>(unit);
            em.SetComponentData(unit, new LifeTime { Duration = lifetime, EndAction = LifeTimeEndAction.Destroy });
            if (!em.HasComponent<Age>(unit)) em.AddComponent<Age>(unit);
            if (!em.HasComponent<DestroyWhenDisabled>(unit)) em.AddComponent<DestroyWhenDisabled>(unit);
            if (em.HasComponent<Translation>(unit)) em.SetComponentData(unit, new Translation { Value = at });
            if (em.HasComponent<LastTranslation>(unit)) em.SetComponentData(unit, new LastTranslation { Value = at });
            n++;
        }
        Plugin.Logger.LogInfo($"[DEV NATIVE] n={n} x={F(p.Value.x)} z={F(p.Value.z)}");
        ctx.Reply($"devnative n={n}");
    }
}
