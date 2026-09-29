using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[CommandGroup("nyar")]
internal static class RegionCommands
{
    [Command("region", usage: "list | here", description: "List the map's regions, or name the region you stand in.")]
    public static void Region(ChatCommandContext ctx, string verb = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
    }
}
