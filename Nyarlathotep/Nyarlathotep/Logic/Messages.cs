#nullable enable
using System.Text.RegularExpressions;

namespace Nyarlathotep.Logic;

/// <summary>What a player-facing template may say (Security › Injection): the event's name, its faction, minutes
/// left, the wave and the wave count, and a zone's name. It has no position or radius field, so nothing rendered from
/// it can carry one (foundation D15, Epic D16).</summary>
public sealed record MessageContext(string Event, string Faction, int Minutes, int Wave, int Waves, string Zone)
{
    /// <summary>The context of <paramref name="def"/>: its faction is the second part of its first unit's prefab name
    /// (CHAR_Bandit_Thug → Bandit), or "-".</summary>
    public static MessageContext For(EventDefinition def, int minutes, int wave) => new(
        def.Name,
        FactionOf(def.Action?.Units.FirstOrDefault()?.Prefab),
        minutes,
        wave,
        def.Action?.Waves ?? 0,
        "-");

    static string FactionOf(string? prefab)
    {
        var parts = (prefab ?? "").Split('_');
        return parts.Length > 1 && parts[0] == "CHAR" && parts[1].Length > 0 ? parts[1] : "-";
    }
}

/// <summary>Every non-admin reply and announcement of this child, and the default message pools (foundation D15).
/// A builder here never takes a position, radius or coordinate; PrivacyTests enumerates the public static members
/// by reflection and fails on a new one without a planted-coordinate case. Admin-only lines that do show positions
/// (`.nyar debug here`) are built elsewhere.</summary>
public static class Messages
{
    public const string StillLoading = "still loading";
    public const string NoActiveEvents = "No active events.";

    static readonly Regex PlaceholderRx = new(@"\{([a-z]+)\}", RegexOptions.CultureInvariant);

    /// <summary>Default pools; every template uses only <see cref="EventValidator.AllowedPlaceholders"/>.</summary>
    public static readonly IReadOnlyList<string> EventStartPool =
    [
        "The {event} begins.",
        "The {faction} stir: {event} has begun.",
    ];

    public static readonly IReadOnlyList<string> EventEndPool =
    [
        "The {event} is over.",
        "The {faction} fall quiet. {event} has ended.",
    ];

    public static readonly IReadOnlyList<string> WaveWarningPool =
    [
        "Wave {wave} of {waves} of the {event} arrives in {minutes} min.",
    ];

    /// <summary>A warning less than a minute ahead, where "in {minutes} min" would overstate it.</summary>
    public static readonly IReadOnlyList<string> WaveImminentPool =
    [
        "Wave {wave} of {waves} of the {event} is almost here.",
    ];

    public static readonly IReadOnlyList<string> DailyBannerPool =
    [
        "Tonight: {event}.",
    ];

    /// <summary>Most event names one daily banner lists; the rest are counted.</summary>
    public const int DailyBannerMaxNames = 5;

    /// <summary>Every pool, by name, for the template checks.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Pools() => new Dictionary<string, IReadOnlyList<string>>
    {
        [nameof(EventStartPool)] = EventStartPool,
        [nameof(EventEndPool)] = EventEndPool,
        [nameof(WaveWarningPool)] = WaveWarningPool,
        [nameof(WaveImminentPool)] = WaveImminentPool,
        [nameof(DailyBannerPool)] = DailyBannerPool,
    };

    /// <summary>The `.nyar` overview for anyone.</summary>
    public static string Overview(string version) =>
        $"Nyarlathotep, Lord of Chaos (v{version}) - server events: empowered factions, " +
        "sieges, defended zones, boss reinforcements, and spawn waves. Try .nyar status.";

    /// <summary>The commands lines of `.nyar` (D26): the ones the caller may run, as given, sorted, "commands: " first.
    /// A new line starts before a command that would take a line past <paramref name="maxBytes"/>, so no command is cut
    /// (Codex 331bb3f F1).</summary>
    public static IReadOnlyList<string> CommandList(IEnumerable<string> commands, int maxBytes = Wire.MaxBytes)
    {
        var lines = new List<string>();
        var line = "commands:";
        var empty = true;
        foreach (var c in commands.OrderBy(c => c, StringComparer.Ordinal))
        {
            var next = empty ? $"{line} {c}" : $"{line}, {c}";
            if (!empty && System.Text.Encoding.UTF8.GetByteCount(next + ",") > maxBytes)
            {
                lines.Add(line + ",");
                next = c;
            }
            line = next;
            empty = false;
        }
        lines.Add(line);
        return lines;
    }

    /// <summary>`.nyar status` for anyone: one line per running event, name and minutes left, or "No active
    /// events.".</summary>
    public static IReadOnlyList<string> Status(IEnumerable<RunningInstance> running, DateTime utcNow)
    {
        var lines = running
            .OrderBy(r => r.EndsUtc)
            .Select(r => $"{r.Definition.Name}: {MinutesLeft(r.EndsUtc, utcNow)} min left")
            .ToList();
        return lines.Count == 0 ? [NoActiveEvents] : lines;
    }

    /// <summary>An announcement from <paramref name="template"/>; an unknown placeholder is left as written (the
    /// validator refuses such templates at load, D5).</summary>
    public static string Render(string template, MessageContext ctx) =>
        PlaceholderRx.Replace(template, m => m.Groups[1].Value switch
        {
            "event" => ctx.Event,
            "faction" => ctx.Faction,
            "minutes" => ctx.Minutes.ToString(),
            "wave" => ctx.Wave.ToString(),
            "waves" => ctx.Waves.ToString(),
            "zone" => ctx.Zone,
            _ => m.Value,
        });

    /// <summary>The start banner of <paramref name="def"/>: its own first start line, else the pool's
    /// <paramref name="pick"/>-th.</summary>
    public static string StartBanner(EventDefinition def, int minutes, int pick) =>
        Render(Choose(def.Announce.Start, EventStartPool, pick), MessageContext.For(def, minutes, 1));

    /// <summary>The end banner of <paramref name="def"/>, as <see cref="StartBanner"/>.</summary>
    public static string EndBanner(EventDefinition def, int pick) =>
        Render(Choose(def.Announce.End, EventEndPool, pick), MessageContext.For(def, 0, def.Action?.Waves ?? 0));

    /// <summary>The warning before wave <paramref name="wave"/>, <paramref name="secondsUntil"/> ahead: in whole
    /// minutes, rounded up, from a minute ahead; "almost here" under a minute.</summary>
    public static string WaveWarning(EventDefinition def, int wave, int secondsUntil, int pick) =>
        Render(Choose([], secondsUntil >= 60 ? WaveWarningPool : WaveImminentPool, pick),
            MessageContext.For(def, (secondsUntil + 59) / 60, wave));

    /// <summary>The daily banner naming <paramref name="names"/> (A19): at most <see cref="DailyBannerMaxNames"/>, fewer
    /// when the line would pass <see cref="Wire.MaxBytes"/>, then "and &lt;k&gt; more".</summary>
    public static string DailyBannerText(IReadOnlyList<string> names, int pick)
    {
        var template = Choose([], DailyBannerPool, pick);
        for (var n = Math.Min(names.Count, DailyBannerMaxNames); ; n--)
        {
            var shown = string.Join(", ", names.Take(n));
            if (names.Count > n) shown += n == 0 ? $"{names.Count} events" : $" and {names.Count - n} more";
            var line = Render(template, new MessageContext(shown, "-", 0, 0, 0, "-"));
            if (n == 0 || System.Text.Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes) return line;
        }
    }

    static string Choose(IReadOnlyList<string> own, IReadOnlyList<string> pool, int pick)
    {
        var list = own.Count > 0 ? own : pool;
        return list[Math.Abs(pick % list.Count)];
    }

    static int MinutesLeft(DateTime endsUtc, DateTime utcNow) =>
        endsUtc > utcNow ? (int)Math.Ceiling((endsUtc - utcNow).TotalMinutes) : 0;
}
