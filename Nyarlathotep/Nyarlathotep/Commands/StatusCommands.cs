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
        if (!Core.IsReady) { ctx.Reply("still loading"); return; }
        var running = EventStore.Catalog.Running;
        if (running.Count == 0) { ctx.Reply("No active events."); return; }
        var now = DateTime.UtcNow;
        foreach (var r in running)
        {
            var left = r.EndsUtc > now ? (int)Math.Ceiling((r.EndsUtc - now).TotalMinutes) : 0;
            ctx.Reply($"{r.Definition.Name}: {left} min left");
        }
    }
}
