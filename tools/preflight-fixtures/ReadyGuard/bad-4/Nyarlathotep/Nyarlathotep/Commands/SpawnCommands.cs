using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using Unity.Transforms;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar spawn`, `.nyar purge [confirm]` and `.nyar debug here [radius]` (foundation Design › UX; D20,
/// D27). Admin-only; every mutation runs through the gateway (D10, D11).</summary>
[CommandGroup("nyar")]
internal static class SpawnCommands
{
    static readonly PurgeArming Arming = new();

    [Command("spawn", usage: "<unit> [count] [level|+n|-n] [hp] [power]", description: "Spawn tracked units around you.", adminOnly: true)]
    public static void Spawn(ChatCommandContext ctx, string unit, string count = "", string level = "", string hp = "", string power = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); }
        var n = CommandArgs.Count(count, Settings.Limit(Limits.MaxUnitsPerWave));
        var lv = CommandArgs.Level(level);
        var hpx = CommandArgs.Multiplier("hp", hp);
        var ppx = CommandArgs.Multiplier("power", power);
        var error = n.Error ?? lv.Error ?? hpx.Error ?? ppx.Error;
        if (error is not null) { ctx.Reply(error); return; }
        if (!ctx.Event.SenderCharacterEntity.TryGetComponent<Translation>(out var at)) { ctx.Reply("your position could not be read"); return; }

        LogAdmin(ctx, $"spawn {unit} {n.Value} {level} {hp} {power}".TrimEnd());
        var tuning = new UnitTuning(lv.Value, hpx.Value, ppx.Value);
        ctx.Reply(Gateway.Run(ActionKind.Spawn, Actor.Admin, () => SpawnTracker.SpawnManual(unit, n.Value, tuning, at.Value)));
    }

    [Command("purge", usage: "[confirm]", description: "End every event and despawn every tracked unit (asks to confirm).", adminOnly: true)]
    public static void PurgeCommand(ChatCommandContext ctx, string confirm = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var admin = ctx.User.PlatformId;
        var now = DateTime.UtcNow;
        var events = EventRuntime.Engine.Active.Count;
        var units = SpawnTracker.Ledger.Purgeable;
        switch (confirm)
        {
            case "":
                ctx.Reply(Gateway.Run(ActionKind.Purge, Actor.Admin, () =>
                {
                    if (events == 0 && units == 0) return AdminLines.NothingToPurge;
                    Arming.Arm(admin, now);
                    return AdminLines.PurgePrompt(events, units);
                }));
                return;
            case "confirm":
                LogAdmin(ctx, "purge confirm");
                ctx.Reply(Gateway.Run(ActionKind.PurgeConfirm, Actor.Admin, () =>
                    Arming.Confirm(admin, now, events > 0 || units > 0) switch
                    {
                        PurgeConfirmResult.Purge => EventRuntime.Purge(),
                        PurgeConfirmResult.NothingToPurge => AdminLines.NothingToPurge,
                        _ => AdminLines.NotArmed,
                    }));
                return;
            default:
                ctx.Reply("argument must be confirm or nothing");
                return;
        }
    }

    [Command("debug", usage: "here [radius]", description: "List tracked units near you with lifetime, level and stats.", adminOnly: true)]
    public static void Debug(ChatCommandContext ctx, string where = "", string radius = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        if (where != "here") { ctx.Reply("argument must be here"); return; }
        var r = CommandArgs.Radius(radius);
        if (r.Error is not null) { ctx.Reply(r.Error); return; }
        if (!ctx.Event.SenderCharacterEntity.TryGetComponent<Translation>(out var at)) { ctx.Reply("your position could not be read"); return; }
        var lines = SpawnTracker.DebugHere(at.Value, r.Value);
        foreach (var message in AdminLines.Pack(lines)) ctx.Reply(message);      // a burst of replies loses lines (A8)
        foreach (var line in lines) Core.Log.LogInfo($"[nyar] debug: {line}");  // no position in the line; kept for the test record
    }

    static void LogAdmin(ChatCommandContext ctx, string command) =>
        Core.Log.LogInfo($"[nyar] {AdminLines.AdminRan(ctx.Name, ctx.User.PlatformId, command)}");
}
