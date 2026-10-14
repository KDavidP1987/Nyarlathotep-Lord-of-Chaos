using System.Globalization;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), the pathing probe (design §9 D50, docs/RESEARCH_NOTES.md › Pathing levers, 2026-10-14):
//   .devwalkto <prefabGuid> <mark> [buffGuid] [seconds] [radius] [max]
//       puts the game's own "walk to a position" buff (default AB_Blackfang_Valyr_WalkToPos_WalkToPosBuff 131220609) on
//       up to max (default 5) living units of that prefab within radius (default 30) of the sender, with the buff's
//       Translation set to the mark, its lifetime to seconds (default 120), re-solving every second and its AmplifyBuff
//       removed; log [DEV WALKTO] put=<n> buff=<guid> mark=<name> d=<mean metres to the mark>
//       Progress is read with `.devunits <prefabGuid> <radius> <mark>`. Never in the mod; dev server only.
static class WalkToCommands
{
    static World ServerWorld()
    {
        foreach (var w in World.s_AllWorlds) if (w.Name == "Server") return w;
        throw new InvalidOperationException("no Server world");
    }

    static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    [Command("devwalkto", adminOnly: true)]
    public static void DevWalkTo(ChatCommandContext ctx, int prefab, string mark, int buff = 131220609, float seconds = 120f,
        float radius = 30f, int max = 5)
    {
        if (!DevCommands.LoadMarks().TryGetValue(mark, out var m) || m.Length != 3) { ctx.Reply($"devwalkto: no mark {mark}"); return; }
        var target = new float3(m[0], m[1], m[2]);
        var me = DevCommands.Where(ctx);
        if (me is null) { ctx.Reply("devwalkto: no position"); return; }
        var em = DevCommands.Em;
        var des = ServerWorld().GetExistingSystemManaged<DebugEventsSystem>();
        var query = DevCommands.LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        int put = 0, noBuff = 0;
        var sum = 0f;
        try
        {
            foreach (var u in units)
            {
                if (put >= max) break;
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                if (math.distance(em.GetComponentData<Translation>(u).Value.xz, me.Value.xz) > radius) continue;
                var guid = new PrefabGUID(buff);
                des.ApplyBuff(new FromCharacter { User = ctx.Event.SenderUserEntity, Character = u }, new ApplyBuffDebugEvent { BuffPrefabGUID = guid });
                if (!BuffUtility.TryGetBuff(em, u, guid, out var b)) { noBuff++; continue; }
                if (em.HasComponent<Translation>(b)) em.SetComponentData(b, new Translation { Value = target });
                if (em.HasComponent<LifeTime>(b)) { var lt = em.GetComponentData<LifeTime>(b); lt.Duration = seconds; em.SetComponentData(b, lt); }
                if (em.HasComponent<MoveTowardsPositionBuff>(b))
                {
                    var mt = em.GetComponentData<MoveTowardsPositionBuff>(b);
                    mt.RecalculationFrequency = 1f;
                    mt.NextRecalculationTime = 0;
                    em.SetComponentData(b, mt);
                }
                if (em.HasComponent<AmplifyBuff>(b)) em.RemoveComponent<AmplifyBuff>(b);
                if (em.HasComponent<SpellTarget>(b)) { var st = em.GetComponentData<SpellTarget>(b); st.Target = Entity.Null; em.SetComponentData(b, st); }
                sum += math.distance(em.GetComponentData<Translation>(u).Value.xz, target.xz);
                put++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        var mean = put > 0 ? sum / put : 0f;
        Plugin.Logger.LogInfo($"[DEV WALKTO] put={put} nobuff={noBuff} buff={buff} mark={mark} d={F(mean)}");
        ctx.Reply($"devwalkto put={put} d={F(mean)}");
    }
}
