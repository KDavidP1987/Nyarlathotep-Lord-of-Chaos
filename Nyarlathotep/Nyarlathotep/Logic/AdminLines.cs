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

    /// <summary>The private line an admin gets on connecting while something is degraded (D31).</summary>
    public static string DegradedNotice(IReadOnlyCollection<string> degraded) =>
        $"nyar: degraded: {string.Join(", ", degraded)} (see .nyar status and the server log)";

    public static string Tracked(int tracked, int pendingSpawns, int pendingDespawns) =>
        $"tracked units: {tracked} (spawning {pendingSpawns}, despawning {pendingDespawns})";

    /// <summary>One `debug here` line: prefab, event (or "manual"), lifetime left (null: the unit has no LifeTime, a
    /// recipe fault shown as "left NONE"), level, Health and PhysicalPower.</summary>
    public static string DebugUnit(string prefab, string? eventId, int? leftSeconds, int level, int health, int maxHealth, int power) =>
        $"{prefab} {eventId ?? "manual"} left {(leftSeconds is { } s ? $"{Math.Max(0, s)}s" : "NONE")} lvl {level} hp {health}/{maxHealth} pp {power}";

    /// <summary>The spawn recipe as `debug here` sees it (D27): "recipe ok" when the unit has LifeTime, Age and
    /// DestroyWhenDisabled and no DontSaveEntity (A9), otherwise "recipe" and each fault, e.g. "recipe -Age +DontSave".</summary>
    public static string Recipe(bool lifeTime, bool age, bool destroyWhenDisabled, bool dontSave)
    {
        var faults = new List<string>();
        if (!lifeTime) faults.Add("-LifeTime");
        if (!age) faults.Add("-Age");
        if (!destroyWhenDisabled) faults.Add("-DestroyWhenDisabled");
        if (dontSave) faults.Add("+DontSave");
        return faults.Count == 0 ? "recipe ok" : "recipe " + string.Join(" ", faults);
    }

    /// <summary>At most <see cref="DebugMaxLines"/> lines, then "+&lt;k&gt; more"; none → "no tracked units within
    /// &lt;r&gt; m".</summary>
    public static IReadOnlyList<string> DebugReport(IReadOnlyList<string> lines, int radius)
    {
        if (lines.Count == 0) return [$"no tracked units within {radius} m"];
        if (lines.Count <= DebugMaxLines) return lines;
        return [.. lines.Take(DebugMaxLines), $"+{lines.Count - DebugMaxLines} more"];
    }

    /// <summary>Joins <paramref name="lines"/> with newlines into as few messages of at most <paramref name="maxBytes"/>
    /// UTF-8 bytes as fit, never splitting a line; a single line longer than the cap is cut at a whole character.
    /// Chat drops a burst of separate replies (A8).</summary>
    public static IReadOnlyList<string> Pack(IReadOnlyList<string> lines, int maxBytes = Wire.MaxBytes)
    {
        var messages = new List<string>();
        var current = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var raw in lines)
        {
            var line = Cut(raw, maxBytes);
            var size = System.Text.Encoding.UTF8.GetByteCount(line);
            if (current.Length > 0 && bytes + 1 + size > maxBytes)
            {
                messages.Add(current.ToString());
                current.Clear();
                bytes = 0;
            }
            if (current.Length > 0) { current.Append('\n'); bytes++; }
            current.Append(line);
            bytes += size;
        }
        if (current.Length > 0) messages.Add(current.ToString());
        return messages;
    }

    static string Cut(string line, int maxBytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(line) <= maxBytes) return line;
        var sb = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var r in line.EnumerateRunes())
        {
            if (bytes + r.Utf8SequenceLength > maxBytes) break;
            sb.Append(r.ToString());
            bytes += r.Utf8SequenceLength;
        }
        return sb.ToString();
    }

    /// <summary>The log line for a mutating admin command (Security › Personal data): BepInEx overwrites the log on
    /// each boot, and audit records never quote it.</summary>
    public static string AdminRan(string name, ulong platformId, string command) =>
        $"admin {TextSink.Name(name)} ({platformId}) ran {command}";
}
