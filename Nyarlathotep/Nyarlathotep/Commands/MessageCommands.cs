using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>`.nyar announce &lt;text&gt;` (foundation Design › UX; D14, D30). Admin-only; the broadcast runs through the
/// gateway (D10, D11). VCF 0.10 has no remainder argument, so up to 16 words arrive as separate arguments and are
/// joined; longer text is quoted and arrives as one argument (Codex step 6 F1).</summary>
[CommandGroup("nyar")]
internal static class MessageCommands
{
    [Command("announce", usage: "<text> (quote text of more than 16 words)", description: "Broadcast a line to every connected player.", adminOnly: true)]
    public static void Announce(ChatCommandContext ctx, string w1 = "", string w2 = "", string w3 = "", string w4 = "",
        string w5 = "", string w6 = "", string w7 = "", string w8 = "", string w9 = "", string w10 = "", string w11 = "",
        string w12 = "", string w13 = "", string w14 = "", string w15 = "", string w16 = "")
    {
        if (!Core.IsReady) { ctx.Reply(Messages.StillLoading); return; }
        var raw = string.Join(" ", new[] { w1, w2, w3, w4, w5, w6, w7, w8, w9, w10, w11, w12, w13, w14, w15, w16 }.Where(w => w.Length > 0));
        var text = TextSink.Announcement(raw, out var error);
        if (text is null) { ctx.Reply(error!); return; }
        Core.Log.LogInfo($"[nyar] {AdminLines.AdminRan(ctx.Name, ctx.User.PlatformId, $"announce {text}")}");
        ctx.Reply(Gateway.Run(ActionKind.Announce, Actor.Admin, () => Announcer.AdminAnnounce(text)));
    }
}

/// <summary>`.nyar api version` (foundation D12, D32; docs/RAPHAEL_INTEGRATION_CONTRACT.md §2): the handshake line
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
}
