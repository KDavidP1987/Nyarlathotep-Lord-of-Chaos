using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar pillar list` and `.nyar pillar &lt;name&gt; on|off` (event-library D13, D14; Business rules 6).
/// Admin-only; a switch runs through the gateway as SetPillar (D17) and is saved to the cfg by BepInEx.</summary>
[CommandGroup("nyar")]
internal static class PillarCommands
{
    [Command("pillar", usage: "list | <name> on|off",
        description: "List the pillar switches, or turn one pillar on or off (saved to the cfg).", adminOnly: true)]
    public static void Pillars(ChatCommandContext ctx, string name = "", string state = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var form = CommandForms.Check("pillar", [name, state], out var usage);
        if (form is null) { ctx.Reply(usage ?? CommandForms.Library[^1].ChatUsage); return; }
        if (form.Words == "pillar list") { EventCommands.Reply(ctx, PillarSwitches.List()); return; }
        EventCommands.LogAdmin(ctx, $"pillar {name} {state}");
        EventCommands.Reply(ctx, Gateway.Run(ActionKind.SetPillar, Actor.Admin, () => PillarSwitches.SetPillar(name, state)).Split('\n'));
    }
}
