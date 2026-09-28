using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [Command("event", usage: "<verb> [id]", description: "Event admin actions as machine-readable lines.")]
    public static void Event(ChatCommandContext ctx, string verb = "", string id = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
    }
}
