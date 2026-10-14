using System.Diagnostics;
using System.Globalization;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), scaling spike (docs/dod/scaling.md step 1, D19): what the game reports as a player's gear level.
//   .devgear               log [DEV GEAR] armor=<a> spell=<s> weapon=<w> sum=<x> full=<GetFullLevel> floor=<f> round=<r> admin=<User.IsAdmin> ms=<t>
//                          for the sender's own character (no names)
//   .devgear units <r>     log [DEV GEAR] unit=<prefab guid> vblood=<yes|no> lvl=<n> for each living unit within r metres
static class GearCommands
{
    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    [Command("devgear", adminOnly: true)]
    public static void DevGear(ChatCommandContext ctx, string what = "", float radius = 20f)
    {
        var em = DevCommands.Em;
        if (what == "units")
        {
            var me = DevCommands.Where(ctx);
            if (me is null) { ctx.Reply("devgear: no position"); return; }
            var query = DevCommands.LivingUnits(em);
            var units = query.ToEntityArray(Allocator.Temp);
            var n = 0;
            try
            {
                foreach (var u in units)
                {
                    if (!em.Exists(u) || !em.HasComponent<Translation>(u) || !em.HasComponent<PrefabGUID>(u) || !em.HasComponent<UnitLevel>(u)) continue;
                    if (math.distance(em.GetComponentData<Translation>(u).Value.xz, me.Value.xz) > radius) continue;
                    var guid = em.GetComponentData<PrefabGUID>(u).GuidHash;
                    var vblood = em.HasComponent<VBloodUnit>(u) ? "yes" : "no";
                    var lvl = em.GetComponentData<UnitLevel>(u).Level._Value;
                    Plugin.Logger.LogInfo($"[DEV GEAR] unit={guid} vblood={vblood} lvl={lvl}");
                    n++;
                }
            }
            finally { units.Dispose(); query.Dispose(); }
            Plugin.Logger.LogInfo($"[DEV GEAR] units n={n} radius={radius.ToString("0", CultureInfo.InvariantCulture)}");
            ctx.Reply($"devgear units n={n}");
            return;
        }
        // timed as the mod's per-player read will be: the User (admin flag, character), the character's position and its Equipment
        var sw = Stopwatch.StartNew();
        var user = ctx.Event.SenderUserEntity;
        var admin = em.Exists(user) && em.HasComponent<User>(user) ? em.GetComponentData<User>(user).IsAdmin.ToString().ToLowerInvariant() : "unreadable";
        var c = em.Exists(user) && em.HasComponent<User>(user) ? em.GetComponentData<User>(user).LocalCharacter._Entity : ctx.Event.SenderCharacterEntity;
        if (!em.Exists(c) || !em.HasComponent<Equipment>(c) || !em.HasComponent<Translation>(c)) { ctx.Reply("devgear: no equipment"); return; }
        var pos = em.GetComponentData<Translation>(c).Value;
        var eq = em.GetComponentData<Equipment>(c);
        float armor = eq.ArmorLevel, spell = eq.SpellLevel, weapon = eq.WeaponLevel;
        var full = eq.GetFullLevel();
        sw.Stop();
        _ = pos;
        var sum = armor + spell + weapon;
        var line = $"armor={F(armor)} spell={F(spell)} weapon={F(weapon)} sum={F(sum)} full={F(full)} floor={(int)math.floor(sum)} round={(int)math.round(sum)} admin={admin} ms={sw.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)}";
        Plugin.Logger.LogInfo($"[DEV GEAR] {line}");
        ctx.Reply($"devgear {line}");
    }
}
