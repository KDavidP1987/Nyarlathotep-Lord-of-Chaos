using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>
/// Top-level `.nyar` command (no subcommand) prints a short overview. Lives outside any
/// [CommandGroup] so VCF resolves bare `.nyar` here while `.nyar &lt;subcommand&gt;` still routes
/// into the feature groups.
/// </summary>
internal static class RootCommands
{
    [Command("nyar", description: "Nyarlathotep overview — what the mod does and how to get help.")]
    public static void Nyar(ChatCommandContext ctx)
    {
        ctx.Reply(
            $"Nyarlathotep, Lord of Chaos (v{MyPluginInfo.PLUGIN_VERSION}) - server events: empowered factions, " +
            "sieges, defended zones, boss reinforcements, and spawn waves. " +
            (Core.IsReady ? "Ready." : "Waiting for the server world to finish loading."));
    }
}
