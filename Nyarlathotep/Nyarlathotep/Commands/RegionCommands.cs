using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using Unity.Transforms;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar region list|here` (regions D8): the map's regions with their event counts, and the admin's own region.
/// Admin-only; pure reads that change nothing and log nothing, and `here` answers the admin alone (D14).</summary>
[CommandGroup("nyar")]
internal static class RegionCommands
{
    [Command("region", usage: "list | here",
        description: "List the map's regions with their event counts, or name the region you stand in.", adminOnly: true)]
    public static void Region(ChatCommandContext ctx, string verb = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        switch (verb.ToLowerInvariant())
        {
            case "list":
                EventCommands.Reply(ctx, RegionLines.List(EventStore.Catalog.Current));
                return;
            case "here":
                // availability first, then a guarded read of the sender's own position (A34)
                ctx.Reply(RegionLines.HereReply(RegionMap.State.Available, () =>
                    ctx.Event.SenderCharacterEntity.TryGetComponent<Translation>(out var t) ? (t.Value.x, t.Value.z) : null,
                    RegionMap.State.RegionOf));
                return;
            default:
                ctx.Reply(RegionLines.BadVerb);
                return;
        }
    }
}
