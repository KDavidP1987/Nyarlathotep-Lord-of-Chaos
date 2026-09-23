using VampireCommandFramework;

namespace Nyarlathotep.Commands;

[System.Obsolete, VampireCommandFramework.CommandGroupAttribute(name: "Spike")]
internal static class MarchCommands
{
    [Command("go")]
    public static void Go(ChatCommandContext ctx) => ctx.Reply("go");
}
