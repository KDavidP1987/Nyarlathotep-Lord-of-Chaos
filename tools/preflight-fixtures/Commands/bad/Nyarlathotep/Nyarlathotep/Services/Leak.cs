using VampireCommandFramework;

namespace Nyarlathotep.Services;

internal static class Leak
{
    [Command("leak", description: "planted: a command outside Commands/")]
    public static void Run(ChatCommandContext ctx) => ctx.Reply("leak");
}
