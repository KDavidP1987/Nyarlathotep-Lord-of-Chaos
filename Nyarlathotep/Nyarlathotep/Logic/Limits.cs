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

public static partial class Limits
{
    /// <summary>The ceilings of the first four follow the undead-hordes load ladder (D1, D4; Epic Business rules 9, A48): it
    /// passed 250 units and failed 500 (about 19 fps with a player among them), and the owner kept 0.13.1's 500 (A3).</summary>
    public static readonly IntLimit MaxTrackedUnits = new("Limits", "MaxTrackedUnits", 150, 1, 500);
    public static readonly IntLimit MaxUnitsPerWave = new("Limits", "MaxUnitsPerWave", 20, 1, 500);
    public static readonly IntLimit MaxConcurrentEvents = new("Limits", "MaxConcurrentEvents", 3, 1, 10);
    public static readonly IntLimit MaxSpawnsPerTick = new("Limits", "MaxSpawnsPerTick", 10, 1, 40);
    public static readonly IntLimit MaxDespawnsPerTick = new("Limits", "MaxDespawnsPerTick", 5, 1, 40);
    public static readonly IntLimit GraceSeconds = new("Limits", "GraceSeconds", 30, 0, 600);
    public static readonly IntLimit PurgeCooldownSeconds = new("Limits", "PurgeCooldownSeconds", 60, 0, 3600);
    public static readonly IntLimit ManualSpawnLifetimeSeconds = new("Limits", "ManualSpawnLifetimeSeconds", 300, 30, 3600);
    /// <summary>Carrier operations per tick, applies and removals together, removals first (faction-empowerment D5).</summary>
    public static readonly IntLimit EmpowerBatchPerTick = new("Limits", "EmpowerBatchPerTick", 200, 50, 1000);
    /// <summary>Scoreboard rows per event (wave-sets D8): a credit for a further player adds to the totals only. The
    /// bound of TriggerLimits.CountersPerDefinition, far beyond any server this mod targets.</summary>
    public const int ScoreboardPlayers = 200;
    public static readonly IntLimit ShareCooldownSeconds = new("Announcements", "ShareCooldownSeconds", 300, 10, 86400);
    public static readonly IntLimit ShareMaxPerMinute = new("Announcements", "ShareMaxPerMinute", 3, 1, 20);
    /// <summary>[UnitWatch] (unit-watch D1, design §9 D54): how long a unit stands, or stays off the ground, before the
    /// watch marks it stuck; each recovery try waits at most this long for an unseen moment (S-10).</summary>
    public static readonly IntLimit StuckSeconds = new("UnitWatch", "StuckSeconds", 90, 30, 600);
    /// <summary>[UnitWatch]: the quiet minutes before the stall backstop marks a waits-for-clear wave's leftovers (D9); 0
    /// turns the backstop off.</summary>
    public static readonly IntLimit StallMinutes = new("UnitWatch", "StallMinutes", 10, 0, 60);
    /// <summary>[UnitWatch] (unit-watch D34, A1): seconds after a spawn or a landing by one of our movers within which a
    /// unit with a player near must have moved more than 0.5 m, or it is stuck at once ("still").</summary>
    public static readonly IntLimit SpawnStillSeconds = new("UnitWatch", "SpawnStillSeconds", 10, 5, 60);

    public static readonly IReadOnlyList<IntLimit> All =
    [
        MaxTrackedUnits, MaxUnitsPerWave, MaxConcurrentEvents, MaxSpawnsPerTick, MaxDespawnsPerTick,
        GraceSeconds, PurgeCooldownSeconds, ManualSpawnLifetimeSeconds, EmpowerBatchPerTick, ShareCooldownSeconds, ShareMaxPerMinute,
        StuckSeconds, StallMinutes, SpawnStillSeconds,
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

/// <summary>The bounds of the player-action triggers (automation D9, D11, D14).</summary>
public static class TriggerLimits
{
    /// <summary>The player scan runs every 5 s, in the tick phase "player triggers" (D9).</summary>
    public const int ScanSeconds = 5;
    /// <summary>A refused player-action start logs at most once per definition per 60 s (D14).</summary>
    public const int RefusalQuietSeconds = 60;
    /// <summary>One player's second trigger of one definition within 5 s starts nothing (Epic Business rules 8, D14).</summary>
    public const int DedupeSeconds = 5;
    /// <summary>Kill counters per definition, and over all definitions; the least recently updated goes first (D11).</summary>
    public const int CountersPerDefinition = 200;
    public const int CountersInAll = 2000;
    /// <summary>RegionEntered cooldown rows over all definitions; the stalest goes first (D9).</summary>
    public const int CooldownRows = 2000;
}

/// <summary>The bounds of a wave destination (undead-hordes D5, D16): its radius and name, and the march speed, the
/// apocalypse's.</summary>
public static class DestinationLimits
{
    public const int MinRadius = 10, MaxRadius = 100;
    public const int MaxName = 40;
    public const double DefaultMarchSpeed = 2.0;
}

/// <summary>The bounds of an Apocalypse action (apocalypse D1, D13): each key's range, inclusive, and the rolling unit
/// lifetime. Every other event kind keeps its duration of 30-7200 s.</summary>
public static class ApocalypseLimits
{
    public const int MinDurationSeconds = 600;
    public const int MaxDurationSeconds = 604800;                       // 7 days
    public const int OtherMinDurationSeconds = 30;
    public const int OtherMaxDurationSeconds = 7200;
    /// <summary>No roamer or horde unit carries a LifeTime more than this (plus the drain margin) ahead; the ceiling of
    /// every other event's unitLifetimeSeconds (D13).</summary>
    public const int UnitLifetimeSeconds = 7200;
    public const int MaxUnits = 10;
    public const int MinHotspots = 1, MaxHotspots = 5;
    public const int MinNatives = 3, MaxNatives = 50;
    public const int MinSpacing = 100, MaxSpacing = 3000;
    public const int MinPerHotspot = 1, MaxPerHotspot = 10;
    public const int MinPerKill = 0, MaxPerKill = 2;
    /// <summary>A spread block's draw per kill, perKillMin..perKillMax (undead-hordes D7, D16).</summary>
    public const int MinKillDraw = 0, MaxKillDraw = 3;
    /// <summary>roamers.max reaches MaxTrackedUnits' ceiling (undead-hordes D16; LimitsTests pins the two equal).</summary>
    public const int MinRoamers = 1, MaxRoamers = 500;
    public const int MinPerMinute = 1, MaxPerMinute = 30;
    public const int MinHuntRange = 20, MaxHuntRange = 60;
    public const int MinHordes = 1, MaxHordes = 10;
    public const int MinFirstAfter = 0, MaxFirstAfter = 1440;
    public const int MinEvery = 5, MaxEvery = 1440;
    /// <summary>A horde's size reaches MaxUnitsPerWave's ceiling and its growth 100 (undead-hordes D16; LimitsTests pins
    /// the size to the ceiling).</summary>
    public const int MinSize = 5, MaxSize = 500;
    public const int MinGrowth = 0, MaxGrowth = 100;
    public const int MinGoalRadius = 10, MaxGoalRadius = 100;
    public const int MaxGoalName = 32;
    public const double MinMarchSpeed = 0.5, MaxMarchSpeed = 5.0;
}

public static partial class Limits
{
    /// <summary>The rolling LifeTime of an apocalypse's units (apocalypse D13).</summary>
    public const int ApocalypseUnitLifetimeSeconds = ApocalypseLimits.UnitLifetimeSeconds;
}
