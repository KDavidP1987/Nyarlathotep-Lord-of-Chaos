#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>A cfg integer with its default and its hard range (foundation Design › UX › cfg keys; Epic rule 9
/// for the ceilings). Config/Settings.cs binds each one and clamps the loaded value through <see cref="Clamp"/>.</summary>
public sealed record IntLimit(string Section, string Key, int Default, int Min, int Max)
{
    public string Name => $"{Section}.{Key}";

    /// <summary>The value to use, and a log line when the configured one was out of range.</summary>
    public (int Value, string? Log) Clamp(int configured)
    {
        if (configured < Min) return (Min, $"{Name}={configured} is below {Min}; using {Min}");
        if (configured > Max) return (Max, $"{Name}={configured} is above its ceiling {Max}; using {Max}");
        return (configured, null);
    }
}

public static class Limits
{
    public static readonly IntLimit MaxTrackedUnits = new("Limits", "MaxTrackedUnits", 150, 1, 500);
    public static readonly IntLimit MaxUnitsPerWave = new("Limits", "MaxUnitsPerWave", 20, 1, 50);
    public static readonly IntLimit MaxConcurrentEvents = new("Limits", "MaxConcurrentEvents", 3, 1, 10);
    public static readonly IntLimit MaxSpawnsPerTick = new("Limits", "MaxSpawnsPerTick", 10, 1, 20);
    public static readonly IntLimit MaxDespawnsPerTick = new("Limits", "MaxDespawnsPerTick", 5, 1, 20);
    public static readonly IntLimit GraceSeconds = new("Limits", "GraceSeconds", 30, 0, 600);
    public static readonly IntLimit PurgeCooldownSeconds = new("Limits", "PurgeCooldownSeconds", 60, 0, 3600);
    public static readonly IntLimit ManualSpawnLifetimeSeconds = new("Limits", "ManualSpawnLifetimeSeconds", 300, 30, 3600);
    /// <summary>Carrier operations per tick, applies and removals together, removals first (faction-empowerment D5).</summary>
    public static readonly IntLimit EmpowerBatchPerTick = new("Limits", "EmpowerBatchPerTick", 200, 50, 1000);
    public static readonly IntLimit ShareCooldownSeconds = new("Announcements", "ShareCooldownSeconds", 300, 10, 86400);
    public static readonly IntLimit ShareMaxPerMinute = new("Announcements", "ShareMaxPerMinute", 3, 1, 20);

    public static readonly IReadOnlyList<IntLimit> All =
    [
        MaxTrackedUnits, MaxUnitsPerWave, MaxConcurrentEvents, MaxSpawnsPerTick, MaxDespawnsPerTick,
        GraceSeconds, PurgeCooldownSeconds, ManualSpawnLifetimeSeconds, EmpowerBatchPerTick, ShareCooldownSeconds, ShareMaxPerMinute,
    ];

    /// <summary>Announcements.WarningOffsets: 1–5 integers of 5–3600 s, deduplicated, sorted descending.
    /// Values out of range are dropped with a log line; an empty result falls back to the default.</summary>
    public static readonly IReadOnlyList<int> DefaultWarningOffsets = [300, 60, 10];

    public static (IReadOnlyList<int> Offsets, string? Log) ParseWarningOffsets(string? text)
    {
        var kept = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        var dropped = new List<string>();
        foreach (var part in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var v) && v is >= 5 and <= 3600) kept.Add(v);
            else dropped.Add(part);
        }
        var list = kept.Take(5).ToList();
        if (kept.Count > 5) dropped.AddRange(kept.Skip(5).Select(v => v.ToString()));
        if (list.Count == 0)
            return (DefaultWarningOffsets, $"Announcements.WarningOffsets '{text}' has no value in 5-3600; using 300,60,10");
        return (list, dropped.Count > 0 ? $"Announcements.WarningOffsets: dropped {string.Join(",", dropped)} (1-5 values, each 5-3600)" : null);
    }

    /// <summary>Announcements.DailyBannerTime: HH:mm, server-local; invalid → 20:00 with a log line.</summary>
    public static (TimeOnly Time, string? Log) ParseDailyBannerTime(string? text)
    {
        if (TimeFormat.TryParseHhMm(text, out var t)) return (t, null);
        return (new TimeOnly(20, 0), $"Announcements.DailyBannerTime '{text}' is not HH:mm; using 20:00");
    }
}

public static class TimeFormat
{
    /// <summary>Strict HH:mm, 00:00–23:59.</summary>
    public static bool TryParseHhMm(string? text, out TimeOnly time)
    {
        time = default;
        if (text is null || text.Length != 5 || text[2] != ':') return false;
        if (!int.TryParse(text.AsSpan(0, 2), System.Globalization.NumberStyles.None, null, out var h)) return false;
        if (!int.TryParse(text.AsSpan(3, 2), System.Globalization.NumberStyles.None, null, out var m)) return false;
        if (h > 23 || m > 59) return false;
        time = new TimeOnly(h, m);
        return true;
    }
}
