using VampireCommandFramework;
using Cmd = VampireCommandFramework.CommandAttribute;

namespace Nyarlathotep.Commands;

[CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [Cmd("events", usage: "[page]", description: "A second events command.", adminOnly: true)]
    public static void EventsAgain(ChatCommandContext ctx, string page = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
    }
}
