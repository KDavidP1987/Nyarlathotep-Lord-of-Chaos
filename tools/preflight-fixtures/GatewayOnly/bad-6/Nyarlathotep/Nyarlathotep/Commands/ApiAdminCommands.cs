using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>A twin that skips AdminFlows and the gateway (the plant of GatewayOnly/bad-6).</summary>
[CommandGroup("nyar api")]
internal static class ApiAdminCommands
{
    [Command("event", adminOnly: true)]
    public static void Event(ChatCommandContext ctx, string verb, string id)
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(AdminOps.Instance.OpStartEvent(id, null).Human);
    }
}
