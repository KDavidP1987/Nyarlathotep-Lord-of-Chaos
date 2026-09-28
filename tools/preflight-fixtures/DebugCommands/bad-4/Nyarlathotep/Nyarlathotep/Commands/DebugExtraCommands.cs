using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[CommandGroup("nyardev")]
internal static class DebugExtraCommands
{
    [Command("debug", usage: "walk [radius]", description: "A temporary probe.", adminOnly: true)]
    public static void Debug(ChatCommandContext ctx, string where = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply("probe");
    }
}
