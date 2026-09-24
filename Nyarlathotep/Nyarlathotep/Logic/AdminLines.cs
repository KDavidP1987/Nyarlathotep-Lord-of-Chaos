#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>Replies only admins see (Design › UX: `.nyar spawn`, `.nyar purge`, `.nyar debug here`, the tracked
/// count in `.nyar status`). Kept apart from <see cref="Messages"/>, whose builders may never carry a position; these
/// carry none either, but `debug here` works from positions the service reads.</summary>
public static class AdminLines
{
    public const int DebugMaxLines = 20;
    public const string NothingToPurge = "nothing to purge";
    public const string NotArmed = "run .nyar purge first";

    public static string Spawned(int queued, string prefab, string? skipped) =>
        queued == 0 ? skipped ?? $"spawned 0 {prefab}"
        : skipped is null ? $"spawned {queued} {prefab}"
        : $"spawned {queued} {prefab}; {skipped}";

    public static string PurgePrompt(int events, int units) =>
        $"purge ends {events} events and despawns {units} units; run .nyar purge confirm within 30 s";

    public static string Purged(int events, int units) => $"purged: {events} events, {units} units queued";

    public static string Tracked(int tracked, int pendingSpawns, int pendingDespawns) =>
        $"tracked units: {tracked} (spawning {pendingSpawns}, despawning {pendingDespawns})";

    /// <summary>One `debug here` line: prefab, event (or "manual"), lifetime left (null: the unit has no LifeTime, a
    /// recipe fault shown as "left NONE"), level, Health and PhysicalPower.</summary>
    public static string DebugUnit(string prefab, string? eventId, int? leftSeconds, int level, int health, int maxHealth, int power) =>
        $"{prefab} {eventId ?? "manual"} left {(leftSeconds is { } s ? $"{Math.Max(0, s)}s" : "NONE")} lvl {level} hp {health}/{maxHealth} pp {power}";

    /// <summary>At most <see cref="DebugMaxLines"/> lines, then "+&lt;k&gt; more"; none → "no tracked units within
    /// &lt;r&gt; m".</summary>
    public static IReadOnlyList<string> DebugReport(IReadOnlyList<string> lines, int radius)
    {
        if (lines.Count == 0) return [$"no tracked units within {radius} m"];
        if (lines.Count <= DebugMaxLines) return lines;
        return [.. lines.Take(DebugMaxLines), $"+{lines.Count - DebugMaxLines} more"];
    }

    /// <summary>The log line for a mutating admin command (Security › Personal data): BepInEx overwrites the log on
    /// each boot, and audit records never quote it.</summary>
    public static string AdminRan(string name, ulong platformId, string command) =>
        $"admin {TextSink.Name(name)} ({platformId}) ran {command}";
}
