using VampireCommandFramework;

namespace Nyarlathotep.Commands;

// Synthetic (the real EventCommands.cs arrives in foundation step 5): one admin-only command in a [CommandGroup].
[CommandGroup("nyar event")]
internal static class EventCommands
{
    [Command("reload", adminOnly: true, description: "Reload events.json.")]
    public static void Reload(ChatCommandContext ctx) => ctx.Reply("reloaded");
}
