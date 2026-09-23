using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[CommandGroup("nyar spike")]
internal static class MarchCommands
{
    [Command("go")]
    public static void Go(ChatCommandContext ctx) => ctx.Reply("go");
}
