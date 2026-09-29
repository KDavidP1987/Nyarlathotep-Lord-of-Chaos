using System.Linq;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using Unity.Transforms;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar event list|info|start|stop|enable|disable|set|reload` (foundation Design › UX; D6, D22, D23, D29) and
/// `new|copy|delete` (event-library D6-D8; their forms checked by Logic's CommandForms, A7).
/// Admin-only; every mutation runs through Logic/AdminFlows and the gateway (D10, D11; raphael-api-admin D3), and the
/// reply is the flow's Human text. One command with a verb, as `.nyar debug here` is,
/// so VCF routes `.nyar event &lt;verb&gt;` here. A value with spaces is quoted: `.nyar event set raid name "Night raid"`.</summary>
[CommandGroup("nyar")]
internal static class EventCommands
{
    [Command("event", usage: "list [page] | info|start|stop|enable|disable <id> | set <id> <field> <value> | reload | new <id> <pillar> | copy <id> <newId> | delete <id> [confirm]",
        description: "List, inspect, start, stop, enable, disable, edit, reload, create, copy or delete event definitions.", adminOnly: true)]
    public static void Event(ChatCommandContext ctx, string verb = "", string id = "", string field = "", string value = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        if (CommandForms.Check("event", [verb, id, field, value], out var usage) is null && usage is not null) { ctx.Reply(usage); return; }
        var now = DateTime.UtcNow;
        var set = EventStore.Catalog.Current;
        var flows = Gateway.Flows;
        var who = Caller(ctx);
        switch (verb)
        {
            case "new":
                ctx.Reply(flows.New(who, id, field).Human);
                return;
            case "copy":
                ctx.Reply(flows.Copy(who, id, field).Human);
                return;
            case "delete":
                ctx.Reply(flows.DeleteEvent(who, id, field == "confirm").Human);
                return;
            case "start":
                ctx.Reply(flows.Start(who, id).Human);
                return;
            case "stop":
                ctx.Reply(flows.Stop(who, id).Human);
                return;
            case "enable" or "disable":
                ctx.Reply(flows.Enable(who, id, verb == "enable").Human);
                return;
            case "set":
                ctx.Reply(flows.Set(who, id, field, value).Human);
                return;
            case "list":
            {
                var page = CommandArgs.Page(id, EventLines.Pages(set.All.Count));
                if (page.Error is not null) { ctx.Reply(page.Error); return; }
                var running = EventRuntime.Engine.Active.Select(a => a.Id).ToHashSet();
                Reply(ctx, EventLines.List(set, page.Value, running, EventRuntime.Controls()));
                return;
            }
            case "reload":
                ctx.Reply(flows.ReloadEvents(who).Human);
                return;
            case "info":
            {
                var eventId = CommandArgs.EventId(id);
                if (eventId.Error is not null) { ctx.Reply(eventId.Error); return; }
                var active = EventRuntime.Engine.Find(id);
                var def = set.Find(id) ?? active?.Definition;
                if (def is null) { ctx.Reply(AdminLines.UnknownEvent(id).Human); return; }
                Reply(ctx, EventLines.Info(def, active, now));
                return;
            }
            default:
                ctx.Reply(AdminLines.EventVerbs);
                return;
        }
    }

    /// <summary>The admin a flow runs for: SteamID, name, and a reader of their position (null when it cannot be read).</summary>
    internal static AdminCaller Caller(ChatCommandContext ctx)
    {
        var sender = ctx.Event.SenderCharacterEntity;
        return new AdminCaller(ctx.User.PlatformId, ctx.Name,
            () => sender.TryGetComponent<Translation>(out var at) ? (at.Value.x, at.Value.y, at.Value.z) : null);
    }

    internal static void Reply(ChatCommandContext ctx, System.Collections.Generic.IReadOnlyList<string> lines)
    {
        foreach (var message in AdminLines.Pack(lines)) ctx.Reply(message);      // a burst of replies loses lines (A8)
    }

    internal static void LogAdmin(ChatCommandContext ctx, string command) =>
        Core.Log.LogInfo($"[nyar] {AdminLines.AdminRan(ctx.Name, ctx.User.PlatformId, command)}");
}
