using VampireCommandFramework;

namespace Nyarlathotep.Services;

internal static class Hidden
{
    [Command("wipe", adminOnly: true, description: "An admin command declared outside Commands/.")]
    public static void Wipe(ChatCommandContext ctx) => ctx.Reply("wiped");
}
