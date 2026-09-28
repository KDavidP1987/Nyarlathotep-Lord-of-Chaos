using System.Collections.Generic;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>The admin wire twins and reads of api 4 (raphael-api-admin D3-D7, D11; docs/RAPHAEL_INTEGRATION_CONTRACT.md
/// §5a): `.nyar api event|template|pillar|purge …` answer one `[NYAR:ok]` or `[NYAR:err]` line each, and
/// `templates`, `template info`, `pillar list` and `killswitch` answer rows. Each method is a shim over
/// <see cref="Gateway.Flows"/>, where the rate gate, the argument checks and the flows live (Logic, where the tests reach
/// them). Every one is admin-only and answers "still loading" before the world is ready. The trailing `extra` parameter
/// catches one surplus word (badarg arg=extra); VCF answers two or more with its own line. Logic/ApiCommandTable.cs
/// copies these signatures (the WireContract check compares them).</summary>
[CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [Command("event", usage: "<verb> [id] [field] [value]", description: "Event admin actions as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void Event(ChatCommandContext ctx, string verb = "", string id = "", string field = "", string value = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(Gateway.Flows.ApiEvent(EventCommands.Caller(ctx), verb, id, field, value, extra));
    }

    [Command("template", usage: "use <template> [as <id>] | info <template>", description: "Template use and info as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void Template(ChatCommandContext ctx, string verb = "", string template = "", string asWord = "", string asId = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        Reply(ctx, Gateway.Flows.ApiTemplate(EventCommands.Caller(ctx), verb, template, asWord, asId, extra));
    }

    [Command("templates", usage: "[pillar] [page]", description: "The template catalogue as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void Templates(ChatCommandContext ctx, string pillar = "", string page = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        Reply(ctx, Gateway.Flows.ApiTemplates(pillar, page, extra));
    }

    [Command("pillar", usage: "list | <name> on|off", description: "Pillar switches as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void Pillar(ChatCommandContext ctx, string name = "", string state = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        Reply(ctx, Gateway.Flows.ApiPillar(EventCommands.Caller(ctx), name, state, extra));
    }

    [Command("purge", usage: "[confirm]", description: "The kill switch as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void PurgeCommand(ChatCommandContext ctx, string confirm = "", string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(Gateway.Flows.ApiPurge(EventCommands.Caller(ctx), confirm, extra));
    }

    [Command("killswitch", description: "The kill switch state as a machine-readable line (for the Raphael client).", adminOnly: true)]
    public static void KillSwitch(ChatCommandContext ctx, string extra = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        Reply(ctx, Gateway.Flows.ApiKillSwitch(extra));
    }

    static void Reply(ChatCommandContext ctx, IReadOnlyList<string> lines)
    {
        foreach (var line in lines) ctx.Reply(line);
    }
}
