using System.Linq;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using Unity.Transforms;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar event list|info|start|stop|enable|disable|set|reload` (foundation Design › UX; D6, D22, D23, D29).
/// Admin-only; every mutation runs through the gateway (D10, D11). One command with a verb, as `.nyar debug here` is,
/// so VCF routes `.nyar event &lt;verb&gt;` here. A value with spaces is quoted: `.nyar event set raid name "Night raid"`.</summary>
[CommandGroup("nyar")]
internal static class EventCommands
{
    const string Verbs = "argument must be list, info, start, stop, enable, disable, set or reload";

    [Command("event", usage: "list [page] | info|start|stop|enable|disable <id> | set <id> <field> <value> | reload",
        description: "List, inspect, start, stop, enable, disable, edit or reload event definitions.", adminOnly: true)]
    public static void Event(ChatCommandContext ctx, string verb = "", string id = "", string field = "", string value = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var now = DateTime.UtcNow;
        var set = EventStore.Catalog.Current;
        switch (verb)
        {
            case "list":
            {
                var page = CommandArgs.Page(id, EventLines.Pages(set.All.Count));
                if (page.Error is not null) { ctx.Reply(page.Error); return; }
                var running = EventRuntime.Engine.Active.Select(a => a.Id).ToHashSet();
                Reply(ctx, EventLines.List(set, page.Value, running));
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
                LogAdmin(ctx, $"event set {id} {field} {value}");
                ctx.Reply(Gateway.Run(ActionKind.SetEventField, Actor.Admin, () => EventStore.Edit(id, field, v.Value)));
                return;
            }
        }
    }

    static void Reply(ChatCommandContext ctx, System.Collections.Generic.IReadOnlyList<string> lines)
    {
        foreach (var message in AdminLines.Pack(lines)) ctx.Reply(message);      // a burst of replies loses lines (A8)
    }

    static void LogAdmin(ChatCommandContext ctx, string command) =>
        Core.Log.LogInfo($"[nyar] {AdminLines.AdminRan(ctx.Name, ctx.User.PlatformId, command)}");
}
