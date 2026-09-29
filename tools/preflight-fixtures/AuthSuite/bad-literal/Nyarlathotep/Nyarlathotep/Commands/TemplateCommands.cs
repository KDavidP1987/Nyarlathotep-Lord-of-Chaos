using System.Linq;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar template list|info|use` (event-library D4, D5; Design › UX). Admin-only; `use` writes events.json
/// through the gateway as CreateEvent (D17). The forms are checked by Logic's CommandForms (A7).</summary>
[CommandGroup("nyar")]
internal static class TemplateCommands
{
    [Command("template", usage: "list [pillar] [page] | info <template> | use <template> [as <id>]",
        description: "List, inspect or copy the starter event templates into events.json.", adminOnly: true)]
    public static void Template(ChatCommandContext ctx, string verb = "", string a = "", string b = "", string c = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var form = CommandForms.Check("template", [verb, a, b, c], out var usage);
        if (form is null) { ctx.Reply(usage ?? AdminLines.TemplateVerbs); return; }
        switch (form.Words)
        {
            case "template list":
            {
                var inEvents = EventStore.Catalog.Current.All.Select(d => d.Id).ToHashSet();
                EventCommands.Reply(ctx, TemplateLines.List(TemplateLibrary.Catalog, [a, b], inEvents));
                return;
            }
            case "template info":
                EventCommands.Reply(ctx, TemplateLines.Info(TemplateLibrary.Catalog, a, DateTime.UtcNow));
                return;
            default:
            {
                var asId = b == "as" ? c : null;
                ctx.Reply("Gateway.Flows.TemplateUse(" + a + ")");      // planted (Codex step 1 round 2 F2): the handler call is gone; only a string names it
                return;
            }
        }
    }
}
