using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[CommandGroup(GroupName)]
internal static class MarchCommands
{
    private const string GroupName = "sp" + "ike";

    [Command("go")]
    public static void Go(ChatCommandContext ctx) => ctx.Reply("go");
}
