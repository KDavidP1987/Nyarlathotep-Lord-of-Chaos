using System;
using System.Text;
using System.Text.RegularExpressions;
using Nyarlathotep.Config;
using Nyarlathotep.Spikes;
using Stunlock.Core;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>
/// `.nyar spike …` — the developer-only spike harness (docs/dod/spikes.md; deleted in Build step 8). Every
/// command is adminOnly (spikes D2). Refusals follow spikes Business rules 4: still loading, then
/// General.Enabled (march, tag and empower only), then the argument ranges, then a clear that is still
/// draining, then the shared 1 s cooldown (every command but clear), then the 30-alive limit.
/// </summary>
[CommandGroup("nyar spike")]
internal static class SpikeCommands
{
    const int CooldownMs = 1000;
    const int MaxReplyBytes = 480;
    static DateTime _lastRun = DateTime.MinValue;

    [Command("tag", adminOnly: true, usage: "<count 1-10> [lifetime 30-600] [keep 0-1]", description: "Spike: spawn and mark CHAR_Bandit_Thug around you.")]
    public static void Tag(ChatCommandContext ctx, int count, int lifetime = 600, int keep = 0) =>
        Run(ctx, $"tag {count} {lifetime} {keep}", needsEnabled: true, () =>
            Range("count", count, 1, 10) ?? Range("lifetime", lifetime, 30, SpikeUnits.MaxLifetime) ?? Range("keep", keep, 0, 1), count, () =>
        {
            var center = ctx.Event.SenderCharacterEntity.Read<Unity.Transforms.Translation>().Value;
            int spawned = 0, dontSave = 0;
            string lastError = null;
            for (int i = 0; i < count; i++)
            {
                var angle = i * (2 * Math.PI / count);
                var at = center + new Unity.Mathematics.float3((float)Math.Cos(angle) * 6f, 0, (float)Math.Sin(angle) * 6f);
                var e = SpikeUnits.Spawn(SpikeMarch.Thug, at, lifetime, out var err);
                if (!e.Exists()) { lastError = err; continue; }
                spawned++;
                // A9: keep=1 stops the unit being disabled (and so destroyed) when no player is near, e.g. at boot.
                if (keep == 1) SpikeMarch.KeepEnabled(e);
                // S2 (Build step 5): even-numbered units are also excluded from the save.
                if (i % 2 == 0 && e.AddComponentSafe<ProjectM.PersistenceV2.DontSaveEntity>()) dontSave++;
            }
            return $"tag: {spawned}/{count} spawned ({dontSave} DontSaveEntity{(keep == 1 ? ", kept enabled" : "")}), lifetime {lifetime}s, {SpikeUnits.AliveCount()} alive" +
                   (lastError is null ? "" : $"; last error: {lastError}");
        });

    [Command("march", adminOnly: true, usage: "<variant 1-4> [count 1-10, 1-9 with an anchor] [distance 20-200]", description: "Spike S1: spawn a group north of you and try to walk it to you.")]
    public static void March(ChatCommandContext ctx, int variant, int count = 5, int distance = 100) =>
        Run(ctx, $"march {variant} {count} {distance}", needsEnabled: true, () =>
            Range("variant", variant, 1, 4) ?? Retired(variant) ?? Range("count", count, 1, variant >= 3 ? 10 : 9) ?? Range("distance", distance, 20, 200),
            count + (variant is 1 or 2 ? 1 : 0),
            () => SpikeMarch.Start(ctx.Event.SenderCharacterEntity, variant, count, distance));

    [Command("empower", adminOnly: true, usage: "<seconds 10-600> [radius 1-30] [carrierGuid]", description: "Spike S3: apply a timed carrier buff to native NPCs near you.")]
    public static void Empower(ChatCommandContext ctx, int seconds, int radius = 10, int carrierGuid = -1591883586) =>
        Run(ctx, $"empower {seconds} {radius} {carrierGuid}", needsEnabled: true, () =>
            Range("seconds", seconds, 10, 600) ?? Range("radius", radius, 1, 30) ?? SpikeCarrier.CheckCarrier(new PrefabGUID(carrierGuid)),
            spawns: 0,
            () => SpikeCarrier.Empower(ctx.Event.SenderCharacterEntity, seconds, radius, new PrefabGUID(carrierGuid)));

    [Command("inspect", adminOnly: true, usage: "[radius 1-30]", description: "Spike S3: stats and carrier of the nearest native NPC.")]
    public static void Inspect(ChatCommandContext ctx, int radius = 10) =>
        Run(ctx, $"inspect {radius}", needsEnabled: false, () => Range("radius", radius, 1, 30), spawns: 0,
            () => SpikeCarrier.Inspect(ctx.Event.SenderCharacterEntity, radius));

    [Command("sweep", adminOnly: true, description: "Spike: audit every marked and listed spike unit.")]
    public static void Sweep(ChatCommandContext ctx) =>
        Run(ctx, "sweep", needsEnabled: false, () => null, spawns: 0, SpikeUnits.Sweep);

    [Command("clear", adminOnly: true, description: "Spike: destroy every spike unit, at most 5 per batch.")]
    public static void Clear(ChatCommandContext ctx)
    {
        try
        {
            if (!Core.IsReady) { Reply(ctx, "clear", "still loading"); return; }
            Reply(ctx, "clear", SpikeUnits.Clear());
        }
        catch (Exception ex) { Fail(ctx, "clear", ex); }
    }

    /// <summary>The shared refusal order for every spike command except clear.</summary>
    static void Run(ChatCommandContext ctx, string name, bool needsEnabled, Func<string> checkArgs, int spawns, Func<string> body)
    {
        try
        {
            if (!Core.IsReady) { Reply(ctx, name, "still loading"); return; }
            if (needsEnabled && !Settings.Enabled.Value) { Reply(ctx, name, "General.Enabled is false"); return; }
            var argError = checkArgs();
            if (argError is not null) { Reply(ctx, name, argError); return; }
            if (spawns > 0 && SpikeUnits.Draining) { Reply(ctx, name, $"clear in progress ({SpikeUnits.ClearLeft} left)"); return; }
            var sinceMs = (DateTime.UtcNow - _lastRun).TotalMilliseconds;
            if (sinceMs < CooldownMs) { Reply(ctx, name, $"spike cooldown ({CooldownMs - (int)sinceMs} ms)"); return; }
            _lastRun = DateTime.UtcNow;
            if (spawns > 0)
            {
                var alive = SpikeUnits.AliveCount();
                if (alive + spawns > SpikeUnits.MaxAlive) { Reply(ctx, name, $"spike limit {SpikeUnits.MaxAlive} ({alive} alive)"); return; }
            }
            Reply(ctx, name, body());
        }
        catch (Exception ex) { Fail(ctx, name, ex); }
    }

    /// <summary>Spikes A11: variants 1 and 2 link units to a rat anchor through Follower.Followed; both server aborts
    /// (sessions 1 and 3) involved that link, so they are refused before anything spawns.</summary>
    static string Retired(int variant) =>
        variant is 1 or 2 ? $"variant {variant} is retired (follow links aborted the server)" : null;

    static string Range(string arg, int value, int min, int max) =>
        value < min || value > max ? $"{arg} must be {min}-{max}" : null;

    static void Fail(ChatCommandContext ctx, string name, Exception ex)
    {
        var reason = Truncate($"{ex.GetType().Name}: {ex.Message}");   // one line in the log and the reply
        Core.Log.LogWarning($"[nyar-spike] spike failed: {name}: {reason}");
        ctx.Reply(Truncate($"spike failed: {reason}"));
    }

    static void Reply(ChatCommandContext ctx, string name, string result)
    {
        Core.Log.LogInfo($"[nyar-spike] {name} → {result}");
        ctx.Reply(Truncate(result));
    }

    /// <summary>One plain line of at most 480 bytes (chat is FixedString512Bytes; DEV_REMINDERS #30), with
    /// rich-text tags removed so an exception message cannot colour the reply.</summary>
    static string Truncate(string s)
    {
        s = Regex.Replace(s ?? "", "<[^>]*>", "").Replace('<', ' ').Replace('>', ' ').Replace('\n', ' ').Replace('\r', ' ');
        if (Encoding.UTF8.GetByteCount(s) <= MaxReplyBytes) return s;
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (Encoding.UTF8.GetByteCount(sb.ToString() + ch) > MaxReplyBytes - 3) break;
            sb.Append(ch);
        }
        return sb.Append("...").ToString();
    }
}
