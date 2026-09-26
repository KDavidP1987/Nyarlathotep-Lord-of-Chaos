using System.Collections.Generic;
using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>The hidden `.nyar api …` commands Raphael sends (docs/RAPHAEL_INTEGRATION_CONTRACT.md; raphael-api-core).
/// `.nyar api version` (foundation D12, D32; docs/RAPHAEL_INTEGRATION_CONTRACT.md §2): the handshake line
/// Raphael reads. Anyone may run it; `admin` is the server's view of the caller, for UI gating only. Before the world
/// is ready it replies "still loading" like every command (D39), which Raphael treats as no answer and probes again,
/// so every line it does get carries ready=1.</summary>
[CommandGroup("nyar api")]
internal static class ApiCommands
{
    [Command("version", description: "The machine-readable handshake line (for the Raphael client).")]
    public static void Version(ChatCommandContext ctx)
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        ctx.Reply(Wire.Version(new VersionInfo(
            Wire.Api, MyPluginInfo.PLUGIN_VERSION, Ready: true, ctx.IsAdmin,
            Settings.Enabled.Value,
            Persistence.State.Document.PurgeUntilUtc is { } until && until > DateTime.UtcNow,
            Settings.EmpowermentEnabled.Value, Settings.EventSpawnsEnabled.Value, Settings.BossReinforcementsEnabled.Value,
            Settings.DefendedZonesEnabled.Value, Settings.SiegeWavesEnabled.Value, Stats: false,
            Settings.WaveWarnings.Value, Settings.EventBanners.Value, Settings.DailyBanner.Value,
            Settings.LoginStats.Value, Settings.PlayerShare.Value)));
    }

    /// <summary>`.nyar api status` (raphael-api-core D1): the `[NYAR:event]` rows then `[NYAR:end] cmd=status`, each
    /// its own reply. Anyone may run it; the unit count goes to admins only. It answers with General.Enabled off too.</summary>
    [Command("status", description: "Active events as machine-readable lines (for the Raphael client).")]
    public static void Status(ChatCommandContext ctx)
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var units = ctx.IsAdmin
            ? SpawnTracker.Ledger.Units.Where(u => u.EventId is not null).GroupBy(u => u.EventId!)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
            : new Dictionary<string, int>();
        foreach (var line in ApiLines.Status(EventRuntime.Engine.Active, EventRuntime.Engine.PendingCleanups,
                     EventStore.Catalog.Current, units, ctx.IsAdmin, DateTime.UtcNow))
            ctx.Reply(line);
    }

    /// <summary>`.nyar api events [page]` (raphael-api-core D2, D3): the `[NYAR:def]` rows of one page, then
    /// `[NYAR:end] cmd=events page= count=`; a bad page is `[NYAR:err] cmd=events code=badarg arg=page`.</summary>
    [Command("events", usage: "[page]", description: "Event definitions as machine-readable lines (for the Raphael client).", adminOnly: true)]
    public static void Events(ChatCommandContext ctx, string page = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var active = new HashSet<string>(EventRuntime.Engine.Active.Select(a => a.Id), StringComparer.Ordinal);
        var rows = ApiLines.Definitions(EventStore.Catalog.Current, active);
        foreach (var line in Paging.Reply("events", rows, page)) ctx.Reply(line);
    }

    /// <summary>`.nyar api sub on|off` (raphael-api-core D5, D7): the caller's own push subscription, keyed by the
    /// caller's SteamID; no argument names another player. Replies `[NYAR:ok] cmd=sub on=1|0`, or
    /// `[NYAR:err] cmd=sub code=badarg arg=state` for any other argument.</summary>
    [Command("sub", usage: "<on|off>", description: "Push lines on or off for you (for the Raphael client).")]
    public static void Sub(ChatCommandContext ctx, string state = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var id = ctx.User.PlatformId;
        var actor = ctx.IsAdmin ? Actor.Admin : Actor.Player;
        ctx.Reply(Subscriptions.ParseState(state) switch
        {
            true => Gateway.Run(ActionKind.Subscribe, actor, () => Pusher.Subscribe(id)),
            false => Gateway.Run(ActionKind.Subscribe, actor, () => Pusher.Unsubscribe(id)),
            null => Wire.Error("sub", WireError.BadArg, arg: "state"),
        });
    }
}
