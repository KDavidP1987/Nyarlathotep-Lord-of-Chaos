using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar pool list|info|reload` (horde-pools D8): the pool library of pools.json. Admin-only; list and info read,
/// reload re-reads the file and revalidates the events through the gateway as LoadDefinitions.</summary>
[CommandGroup("nyar")]
internal static class PoolCommands
{
    [Command("pool", usage: "list [page] | info POOL [page] | reload",
        description: "List or inspect the pool library of pools.json, or reload it.", adminOnly: true)]
    public static void Pool(ChatCommandContext ctx, string verb = "", string a = "", string b = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        switch (verb)
        {
            case "list" when b.Length == 0:
                EventCommands.Reply(ctx, PoolLines.List(EventStore.Library, a));
                return;
            case "info" when a.Length > 0:
                EventCommands.Reply(ctx, PoolLines.Info(EventStore.Library, a, b, EventStore.Catalog.Current.All));
                return;
            case "reload" when a.Length == 0:
                ctx.Reply(Gateway.Run(ActionKind.LoadDefinitions, Actor.Admin, EventStore.ReloadPools));
                return;
            default:
                ctx.Reply(PoolLines.Verbs);
                return;
        }
    }
}
