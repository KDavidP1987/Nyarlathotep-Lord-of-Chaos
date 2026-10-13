using System.Globalization;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ProjectM;
using ProjectM.Behaviours;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
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
//   .devkill <prefabGuid> [radius]  kill the living units of that one prefab within radius (default 30) with the
//                          sender's character as the killer, so a kill feed credits the player; log [DEV KILL] n=<count>.
//                          The prefab is required: an open query also killed castle and world units (Session 1).
//   .devdie <prefabGuid> [radius]  the sender is killed by the nearest living unit of that prefab within radius, so a
//                          kill feed reads that unit as the killer; log [DEV DIE] found=<0|1> distance=<m>
//   .devhop <prefabGuid> <dx> <dz> [radius] [mark] [home]  move the living units of that prefab within radius (default 30) of
//                          the sender, or of the named mark, by dx and dz (Translation, LastTranslation, PreCombatPosition;
//                          height kept for the game to snap; home true also moves SpawnTransform.Position, the spot an
//                          idle unit walks back to); with a mark, the mark moves along so the next hop follows
//                          the units while the sender stays away (hunting-grounds D20, Review 1 F1); log [DEV HOP] moved=<n>
//   .devjump <prefabGuid> <targetMark> [step] [stopAt] [radius] [clearPath]  every living unit of that prefab within radius (default
//                          400) of the target mark jumps step metres (default 25) straight toward it, never closer than
//                          stopAt (default 55); Translation, LastTranslation, PreCombatPosition and SpawnTransform (clearPath true also
//                          empties the unit's PathBuffer and zeroes its Velocity); log
//                          [DEV JUMP] jumped=<n> arrived=<k> (arrived: within stopAt + 5 m) farthest=<m>
//   .devunits <prefabGuid> [radius] [mark]  log each living unit of that prefab within radius (default 60) of the sender
//                          or the mark: [DEV UNIT] x y z state=<BehaviourTreeState> target=<0|1> pre=<PreCombatPosition>
//                          home=<SpawnTransform.Position>
//   .devwalk <prefabGuid> <targetMark> [step] [radius] [max]  every living unit of that prefab within radius (default 400)
//                          of the mark gets its return point (SpawnTransform.Position, PreCombatPosition) step metres (default
//                          40) ahead toward the mark, Translation untouched, so the game's pathing walks it (apocalypse D24's
//                          pathing probe); log [DEV WALK] set=<n> arrived=<k> (within 30 m) farthest=<m> stall=<s> (longest
//                          time a unit has gone without getting 2 m closer)
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
        Log.LogInfo("NyarDevTools loaded: .devwhere .devtp .devmark .devgo .devmarks .devkill .devdie .devhop .devjump .devwalk .devunits");
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

    // disabled: also the units of chunks far from every player (devunits' worldwide scouting)
    static EntityQuery LivingUnits(EntityManager em, bool disabled = false) => em.CreateEntityQuery(new EntityQueryDesc
    {
        All = new[] { ComponentType.ReadOnly<Health>(), ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<UnitLevel>(),
            ComponentType.ReadOnly<PrefabGUID>() },
        None = new[] { ComponentType.ReadOnly<PlayerCharacter>(), ComponentType.ReadOnly<Dead>() },
        Options = disabled ? EntityQueryOptions.IncludeDisabled : EntityQueryOptions.Default,
    });

    [Command("devdie", adminOnly: true)]
    public static void DevDie(ChatCommandContext ctx, int prefab, float radius = 30f)
    {
        var p = Where(ctx);
        if (p is null) { ctx.Reply("devdie: no position"); return; }
        var em = Em;
        var query = LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        var best = Entity.Null;
        var bestD = float.MaxValue;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var d = math.distance(em.GetComponentData<Translation>(u).Value, p.Value);
                if (d <= radius && d < bestD) { best = u; bestD = d; }
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        if (best == Entity.Null) { Plugin.Logger.LogInfo("[DEV DIE] found=0"); ctx.Reply("devdie: no unit near"); return; }
        Plugin.Logger.LogInfo($"[DEV DIE] found=1 distance={F(bestD)}");
        StatChangeUtility.KillEntity(em, ctx.Event.SenderCharacterEntity, best, 0.0, StatChangeReason.Default, true);
    }

    [Command("devkill", adminOnly: true)]
    public static void DevKill(ChatCommandContext ctx, int prefab, float radius = 30f, int max = 0)
    {
        var p = Where(ctx);
        if (p is null) { ctx.Reply("devkill: no position"); return; }
        var em = Em;
        var killer = ctx.Event.SenderCharacterEntity;
        var query = LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        var n = 0;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab) continue;
                if (math.distance(em.GetComponentData<Translation>(u).Value, p.Value) > radius) continue;
                if (em.GetComponentData<Health>(u).IsDead) continue;
                if (max > 0 && n >= max) break;                                   // the nearest are not chosen: any max units in range
                StatChangeUtility.KillEntity(em, u, killer, 0.0, StatChangeReason.Default, true);
                n++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        Plugin.Logger.LogInfo($"[DEV KILL] n={n} prefab={prefab} radius={F(radius)}");
        ctx.Reply($"devkill n={n}");
    }

    [Command("devhop", adminOnly: true)]
    public static void DevHop(ChatCommandContext ctx, int prefab, float dx, float dz, float radius = 30f, string mark = "", bool home = false)
    {
        float3 from;
        Dictionary<string, float[]> marks = null;
        if (mark.Length > 0)
        {
            marks = LoadMarks();
            if (!marks.TryGetValue(mark, out var m) || m.Length != 3) { ctx.Reply($"devhop: no mark {mark}"); return; }
            from = new float3(m[0], m[1], m[2]);
        }
        else
        {
            var p = Where(ctx);
            if (p is null) { ctx.Reply("devhop: no position"); return; }
            from = p.Value;
        }
        var em = Em;
        var query = LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        var n = 0;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var at = em.GetComponentData<Translation>(u).Value;
                if (math.distance(at.xz, from.xz) > radius) continue;
                var to = new float3(at.x + dx, at.y, at.z + dz);
                em.SetComponentData(u, new Translation { Value = to });
                if (em.HasComponent<LastTranslation>(u)) em.SetComponentData(u, new LastTranslation { Value = to });
                if (em.HasComponent<AggroConsumer>(u))
                {
                    var a = em.GetComponentData<AggroConsumer>(u);
                    a.PreCombatPosition = to;
                    em.SetComponentData(u, a);
                }
                if (home && em.HasComponent<SpawnTransform>(u))
                {
                    var st = em.GetComponentData<SpawnTransform>(u);
                    st.Position = to;
                    em.SetComponentData(u, st);
                }
                n++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        if (marks is not null)
        {
            marks[mark] = new[] { from.x + dx, from.y, from.z + dz };
            File.WriteAllText(MarksPath, JsonSerializer.Serialize(marks, new JsonSerializerOptions { WriteIndented = true }));
        }
        Plugin.Logger.LogInfo($"[DEV HOP] moved={n} prefab={prefab} dx={F(dx)} dz={F(dz)} from={(mark.Length > 0 ? mark : "sender")} x={F(from.x + dx)} z={F(from.z + dz)}");
        ctx.Reply($"devhop moved={n}");
    }

    [Command("devunits", adminOnly: true)]
    public static void DevUnits(ChatCommandContext ctx, int prefab, float radius = 60f, string mark = "")
    {
        float3 from;
        if (mark.Length > 0)
        {
            if (!LoadMarks().TryGetValue(mark, out var m) || m.Length != 3) { ctx.Reply($"devunits: no mark {mark}"); return; }
            from = new float3(m[0], m[1], m[2]);
        }
        else
        {
            var p = Where(ctx);
            if (p is null) { ctx.Reply("devunits: no position"); return; }
            from = p.Value;
        }
        var em = Em;
        var query = LivingUnits(em, true);
        var units = query.ToEntityArray(Allocator.Temp);
        var n = 0;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var at = em.GetComponentData<Translation>(u).Value;
                if (math.distance(at.xz, from.xz) > radius) continue;
                var state = em.HasComponent<BehaviourTreeState>(u) ? em.GetComponentData<BehaviourTreeState>(u).Value.ToString() : "-";
                var target = 0;
                var pre = "-";
                if (em.HasComponent<AggroConsumer>(u))
                {
                    var a = em.GetComponentData<AggroConsumer>(u);
                    target = a.AggroTarget._Entity != Entity.Null ? 1 : 0;
                    pre = $"{F(a.PreCombatPosition.x)},{F(a.PreCombatPosition.z)}";
                }
                var home = "-";
                if (em.HasComponent<SpawnTransform>(u)) { var st = em.GetComponentData<SpawnTransform>(u).Position; home = $"{F(st.x)},{F(st.z)}"; }
                Plugin.Logger.LogInfo($"[DEV UNIT] {n} x={F(at.x)} y={F(at.y)} z={F(at.z)} state={state} target={target} pre={pre} home={home}");
                n++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        Plugin.Logger.LogInfo($"[DEV UNITS] n={n} prefab={prefab} radius={F(radius)}");
        ctx.Reply($"devunits n={n}");
    }

    [Command("devjump", adminOnly: true)]
    public static void DevJump(ChatCommandContext ctx, int prefab, string targetMark, float step = 25f, float stopAt = 55f, float radius = 400f, bool clearPath = false)
    {
        if (!LoadMarks().TryGetValue(targetMark, out var m) || m.Length != 3) { ctx.Reply($"devjump: no mark {targetMark}"); return; }
        var target = new float3(m[0], m[1], m[2]);
        var em = Em;
        var query = LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        int jumped = 0, arrived = 0;
        var farthest = 0f;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var at = em.GetComponentData<Translation>(u).Value;
                var d = math.distance(at.xz, target.xz);
                if (d > radius) continue;
                farthest = math.max(farthest, d);
                if (d <= stopAt + 5f) { arrived++; continue; }
                var dir = math.normalize(target.xz - at.xz);
                var move = math.min(step, d - stopAt);
                var to = new float3(at.x + dir.x * move, at.y, at.z + dir.y * move);
                em.SetComponentData(u, new Translation { Value = to });
                if (em.HasComponent<LastTranslation>(u)) em.SetComponentData(u, new LastTranslation { Value = to });
                if (em.HasComponent<AggroConsumer>(u)) { var a = em.GetComponentData<AggroConsumer>(u); a.PreCombatPosition = to; em.SetComponentData(u, a); }
                if (em.HasComponent<SpawnTransform>(u)) { var st = em.GetComponentData<SpawnTransform>(u); st.Position = to; em.SetComponentData(u, st); }
                if (clearPath)
                {
                    if (em.HasBuffer<ProjectM.Pathfinding.PathBuffer>(u)) em.GetBuffer<ProjectM.Pathfinding.PathBuffer>(u).Clear();
                    if (em.HasComponent<Velocity>(u)) em.SetComponentData(u, new Velocity());
                }
                jumped++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        Plugin.Logger.LogInfo($"[DEV JUMP] jumped={jumped} arrived={arrived} farthest={F(farthest)} target={targetMark}");
        ctx.Reply($"devjump jumped={jumped} arrived={arrived}");
    }

    // per unit: its best distance to the walk target so far and when it last got 2 m closer (the stall clock)
    static readonly Dictionary<Entity, (float best, DateTime at)> WalkProgress = new();

    [Command("devwalk", adminOnly: true)]
    public static void DevWalk(ChatCommandContext ctx, int prefab, string targetMark, float step = 40f, float radius = 400f, int max = 0)
    {
        if (!LoadMarks().TryGetValue(targetMark, out var m) || m.Length != 3) { ctx.Reply($"devwalk: no mark {targetMark}"); return; }
        var target = new float3(m[0], m[1], m[2]);
        var em = Em;
        var query = LivingUnits(em);
        var units = query.ToEntityArray(Allocator.Temp);
        int set = 0, arrived = 0;
        var farthest = 0f;
        var stall = 0.0;
        var now = DateTime.UtcNow;
        try
        {
            foreach (var u in units)
            {
                if (em.GetComponentData<PrefabGUID>(u).GuidHash != prefab || em.GetComponentData<Health>(u).IsDead) continue;
                var at = em.GetComponentData<Translation>(u).Value;
                var d = math.distance(at.xz, target.xz);
                if (d > radius) continue;
                if (max > 0 && set + arrived >= max) break;
                farthest = math.max(farthest, d);
                if (!WalkProgress.TryGetValue(u, out var pr) || d < pr.best - 2f) WalkProgress[u] = pr = (d, now);
                stall = Math.Max(stall, (now - pr.at).TotalSeconds);
                if (d <= 30f) { arrived++; continue; }
                // the return point goes at most step metres ahead of where the unit stands; Translation is left alone,
                // so the game's own pathing walks the unit there (hunting-grounds spike variant 1: units walk home)
                var dir = math.normalize(target.xz - at.xz);
                var ahead = math.min(step, d);
                var home = new float3(at.x + dir.x * ahead, at.y, at.z + dir.y * ahead);
                if (em.HasComponent<SpawnTransform>(u)) { var st = em.GetComponentData<SpawnTransform>(u); st.Position = home; em.SetComponentData(u, st); }
                if (em.HasComponent<AggroConsumer>(u)) { var a = em.GetComponentData<AggroConsumer>(u); a.PreCombatPosition = home; em.SetComponentData(u, a); }
                set++;
            }
        }
        finally { units.Dispose(); query.Dispose(); }
        Plugin.Logger.LogInfo($"[DEV WALK] set={set} arrived={arrived} farthest={F(farthest)} stall={F((float)stall)} target={targetMark}");
        ctx.Reply($"devwalk set={set} arrived={arrived} farthest={F(farthest)}");
    }
}
