using System.Globalization;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), unit-watch A2 (design §9 D56, owner 2026-10-14): the standing and still checks mark only a
// unit holding a player as its aggro target, and NPCs ignore an invulnerable Chaos, so a test forces the target:
//   .devaggro <prefabGuid> [radius] [max]
//       adds an AggroBuffer entry on the sender's character (DamageValue 500, Weight 1, as HuntAction seeds one) to the max
//       (default 1) living units of that prefab nearest the sender within radius (default 30), and logs
//       [DEV AGGRO] put=<n> held=<units whose AggroTarget is the sender now> d=<metres of the nearest>
//   .devaggroread <prefabGuid> [radius]
//       logs [DEV AGGRO READ] n=<units> held=<units whose AggroTarget is the sender> other=<another target> none=<no target>
//       so a scenario sees whether the game kept the forced target or dropped it.
// Never in the mod; dev server only.
static class AggroCommands
{
    static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    static List<(Entity U, float D)> Near(EntityManager em, int prefab, float3 me, float radius)
    {
        var query = DevCommands.LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        var found = new List<(Entity U, float D)>();
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var d = math.distance(em.GetComponentData<Translation>(u).Value.xz, me.xz);
                if (d <= radius) found.Add((u, d));
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        found.Sort((a, b) => a.D.CompareTo(b.D));
        return found;
    }

    /// <summary>0: holds the character; 1: another target; 2: none.</summary>
    static int TargetOf(EntityManager em, Entity u, Entity character)
    {
        if (!em.HasComponent<AggroConsumer>(u)) return 2;
        var t = em.GetComponentData<AggroConsumer>(u).AggroTarget._Entity;
        if (t == Entity.Null || !em.Exists(t)) return 2;
        return t == character ? 0 : 1;
    }

    [Command("devaggro", adminOnly: true)]
    public static void DevAggro(ChatCommandContext ctx, int prefab, float radius = 30f, int max = 1)
    {
        var me = DevCommands.Where(ctx);
        if (me is null) { ctx.Reply("devaggro: no position"); return; }
        var em = DevCommands.Em;
        var character = ctx.Event.SenderCharacterEntity;
        var found = Near(em, prefab, me.Value, radius);
        int put = 0, held = 0;
        foreach (var (u, _) in found.Take(max))
        {
            if (!em.HasBuffer<AggroBuffer>(u)) continue;
            var buffer = em.GetBuffer<AggroBuffer>(u);
            var has = false;
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].Entity == character)
                {
                    var e = buffer[i];
                    e.DamageValue = 500f;
                    e.Weight = 1f;
                    buffer[i] = e;
                    has = true;
                }
            if (!has) buffer.Add(new AggroBuffer { Entity = character, DamageValue = 500f, Weight = 1f });
            if (em.HasComponent<AggroConsumer>(u))
            {
                var a = em.GetComponentData<AggroConsumer>(u);
                a.Active._Value = true;
                em.SetComponentData(u, a);
            }
            put++;
            if (TargetOf(em, u, character) == 0) held++;
        }
        var near = found.Count > 0 ? found[0].D : 0f;
        Plugin.Logger.LogInfo($"[DEV AGGRO] put={put} held={held} d={F(near)}");
        ctx.Reply($"devaggro put={put} held={held}");
    }

    [Command("devaggroread", adminOnly: true)]
    public static void DevAggroRead(ChatCommandContext ctx, int prefab, float radius = 30f)
    {
        var me = DevCommands.Where(ctx);
        if (me is null) { ctx.Reply("devaggroread: no position"); return; }
        var em = DevCommands.Em;
        var character = ctx.Event.SenderCharacterEntity;
        int held = 0, other = 0, none = 0;
        var found = Near(em, prefab, me.Value, radius);
        foreach (var (u, _) in found)
            switch (TargetOf(em, u, character))
            {
                case 0: held++; break;
                case 1: other++; break;
                default: none++; break;
            }
        Plugin.Logger.LogInfo($"[DEV AGGRO READ] n={found.Count} held={held} other={other} none={none}");
        ctx.Reply($"devaggroread n={found.Count} held={held}");
    }
}
