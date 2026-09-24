using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar status` (foundation UX table, D28). Read-only: anyone may run it.</summary>
[CommandGroup("nyar")]
internal static class StatusCommands
{
    [Command("status", description: "Active events and their time left.")]
    public static void Status(ChatCommandContext ctx)
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var value = 1;
        ctx.Reply(Gateway.Run(ActionKind.Announce, Actor.Admin, () => $"{value}("));
        EventStore.Reload();
        foreach (var line in Messages.Status(EventStore.Catalog.Running, DateTime.UtcNow)) ctx.Reply(line);
    }
}
