using VampireCommandFramework;

namespace Nyarlathotep.Patches;

[CommandGroupAttribute("nyar")]
internal static class HiddenPatch
{
    [CommandAttribute("hidden", adminOnly: true, description: "An admin command in the suffixed attribute form, outside Commands/.")]
    public static void Hidden(ChatCommandContext ctx) => ctx.Reply("hidden");
}
