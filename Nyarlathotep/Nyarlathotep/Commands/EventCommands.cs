using System.Linq;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using Unity.Transforms;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar event list|info|start|stop|enable|disable|set|reload` (foundation Design › UX; D6, D22, D23, D29) and
/// `new|copy|delete` (event-library D6-D8; their forms checked by Logic's CommandForms, A7).
/// Admin-only; every mutation runs through the gateway (D10, D11). One command with a verb, as `.nyar debug here` is,
/// so VCF routes `.nyar event &lt;verb&gt;` here. A value with spaces is quoted: `.nyar event set raid name "Night raid"`.</summary>
[CommandGroup("nyar")]
internal static class EventCommands
{
    const string Verbs = "argument must be list, info, start, stop, enable, disable, set, reload, new, copy or delete";

    [Command("event", usage: "list [page] | info|start|stop|enable|disable <id> | set <id> <field> <value> | reload | new <id> <pillar> | copy <id> <newId> | delete <id> [confirm]",
        description: "List, inspect, start, stop, enable, disable, edit, reload, create, copy or delete event definitions.", adminOnly: true)]
    public static void Event(ChatCommandContext ctx, string verb = "", string id = "", string field = "", string value = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        if (CommandForms.Check("event", [verb, id, field, value], out var usage) is null && usage is not null) { ctx.Reply(usage); return; }
        var now = DateTime.UtcNow;
        var set = EventStore.Catalog.Current;
        switch (verb)
        {
            case "new":
                LogAdmin(ctx, $"event new {id} {field}");
                ctx.Reply(Gateway.Run(ActionKind.CreateEvent, Actor.Admin, () => EventStore.Author(text => Authoring.New(text, id, field))));
                return;
            case "copy":
                LogAdmin(ctx, $"event copy {id} {field}");
                ctx.Reply(Gateway.Run(ActionKind.CreateEvent, Actor.Admin, () => EventStore.Author(text => Authoring.Copy(text, id, field))));
                return;
            case "delete":
            {
                var confirm = field == "confirm";
                LogAdmin(ctx, confirm ? $"event delete {id} confirm" : $"event delete {id}");
                var admin = ctx.User.PlatformId;
                ctx.Reply(Gateway.Run(ActionKind.DeleteEvent, Actor.Admin, () => EventStore.DeleteDefinition(admin, id, confirm)));
                return;
            }
            case "list":
            {
                var page = CommandArgs.Page(id, EventLines.Pages(set.All.Count));
                if (page.Error is not null) { ctx.Reply(page.Error); return; }
                var running = EventRuntime.Engine.Active.Select(a => a.Id).ToHashSet();
                Reply(ctx, EventLines.List(set, page.Value, running, EventRuntime.Controls()));
                return;
            }
            case "reload":
                LogAdmin(ctx, "event reload");
                ctx.Reply(Gateway.Run(ActionKind.LoadDefinitions, Actor.Admin, EventStore.Reload));
                return;
            case "info" or "start" or "stop" or "enable" or "disable" or "set":
                break;
            default:
                ctx.Reply(Verbs);
                return;
        }

        var eventId = CommandArgs.EventId(id);
        if (eventId.Error is not null) { ctx.Reply(eventId.Error); return; }
        switch (verb)
        {
            case "info":
            {
                var active = EventRuntime.Engine.Find(id);
                var def = set.Find(id) ?? active?.Definition;
                if (def is null) { ctx.Reply($"unknown event {id}"); return; }
                Reply(ctx, EventLines.Info(def, active, now));
                return;
            }
            case "start":
            {
                (float, float, float)? origin = null;                  // only an Admin location spawns around the admin
                if (set.Find(id)?.Action?.Location.Type == LocationType.Admin)
                {
                    if (!ctx.Event.SenderCharacterEntity.TryGetComponent<Translation>(out var at)) { ctx.Reply("your position could not be read"); return; }
                    origin = (at.Value.x, at.Value.y, at.Value.z);
                }
                LogAdmin(ctx, $"event start {id}");
                ctx.Reply(Gateway.Run(ActionKind.StartEvent, Actor.Admin, () => EventRuntime.StartEvent(id, "manual", Actor.Admin, origin)));
                return;
            }
            case "stop":
                LogAdmin(ctx, $"event stop {id}");
                ctx.Reply(Gateway.Run(ActionKind.EndEvent, Actor.Admin, () => EventRuntime.StopEvent(id)));
                return;
            case "enable":
                LogAdmin(ctx, $"event enable {id}");
                ctx.Reply(Gateway.Run(ActionKind.EnableEvent, Actor.Admin, () => EventStore.Edit(id, "enabled", true)));
                return;
            case "disable":
                LogAdmin(ctx, $"event disable {id}");
                ctx.Reply(Gateway.Run(ActionKind.DisableEvent, Actor.Admin, () => EventStore.Edit(id, "enabled", false)));
                return;
            default:
            {
                var v = CommandArgs.SettableValue(field, value);
                if (v.Error is not null) { ctx.Reply(v.Error); return; }
                var newValue = v.Value;
                if (newValue is LocationHere)                                 // the admin's position, rounded to 0.1 (D11)
                {
                    var sender = ctx.Event.SenderCharacterEntity;
                    var p = LocationArg.FromContext(() =>
                        sender.TryGetComponent<Translation>(out var at) ? (at.Value.x, at.Value.z) : null);
                    if (p.Error is not null) { ctx.Reply(p.Error); return; }
                    newValue = p.Value;
                }
                LogAdmin(ctx, $"event set {id} {field} {value}");
                ctx.Reply(Gateway.Run(ActionKind.SetEventField, Actor.Admin, () => EventStore.Edit(id, field, newValue)));
                return;
            }
        }
    }

    internal static void Reply(ChatCommandContext ctx, System.Collections.Generic.IReadOnlyList<string> lines)
    {
        foreach (var message in AdminLines.Pack(lines)) ctx.Reply(message);      // a burst of replies loses lines (A8)
    }

    internal static void LogAdmin(ChatCommandContext ctx, string command) =>
        Core.Log.LogInfo($"[nyar] {AdminLines.AdminRan(ctx.Name, ctx.User.PlatformId, command)}");
}
