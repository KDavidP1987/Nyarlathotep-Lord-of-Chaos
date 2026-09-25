using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nyarlathotep.Logic;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>
/// Top-level `.nyar` command (no subcommand): the overview, then the commands the caller may run (D26), read from this
/// assembly's [Command] attributes so the list cannot drift from what VCF registers. Lives outside any
/// [CommandGroup] so VCF resolves bare `.nyar` here while `.nyar &lt;subcommand&gt;` still routes into the groups.
/// </summary>
internal static class RootCommands
{
    [Command("nyar", description: "Nyarlathotep overview and the commands you may run.")]
    public static void Nyar(ChatCommandContext ctx)
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(Messages.Overview(MyPluginInfo.PLUGIN_VERSION));
        foreach (var line in Messages.CommandList(All.Where(c => ctx.IsAdmin || !c.AdminOnly).Select(c => c.Text))) ctx.Reply(line);
    }

    static List<(string Text, bool AdminOnly)> _all;

    static List<(string Text, bool AdminOnly)> All => _all ??= typeof(RootCommands).Assembly.GetTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Select(m => (Group: t.GetCustomAttribute<CommandGroupAttribute>()?.Name, Command: m.GetCustomAttribute<CommandAttribute>())))
        .Where(x => x.Command is not null)
        .Select(x => ("." + (x.Group is null ? x.Command.Name : $"{x.Group} {x.Command.Name}"), x.Command.AdminOnly))
        .ToList();
}
