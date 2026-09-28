using Nyarlathotep.Logic;
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
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(Messages.Overview(MyPluginInfo.PLUGIN_VERSION));
    }
}
