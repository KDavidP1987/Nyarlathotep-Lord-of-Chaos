using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[type: CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [Command("events", usage: "[page]", description: "A second events command.", adminOnly: true)]
    public static void EventsAgain(ChatCommandContext ctx, string page = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
    }
}
