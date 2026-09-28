namespace Nyarlathotep.Commands;

[VampireCommandFramework.CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [VampireCommandFramework.Command("events", usage: "[page]", description: "A second events command.", adminOnly: true)]
    public static void EventsAgain(VampireCommandFramework.ChatCommandContext ctx, string page = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
    }
}
