#nullable enable
using System.Text.RegularExpressions;

namespace Nyarlathotep.Logic;

/// <summary>What a player-facing template may say (Security › Injection): the event's name, its faction, minutes
/// left, the wave and the wave count, a zone's name, and the action scope's region names (regions D9). It has no position
/// or radius field, so nothing rendered from it can carry one (foundation D15, Epic D16).</summary>
public sealed record MessageContext(string Event, string Faction, int Minutes, int Wave, int Waves, string Zone, string Region = MessageContext.World)
{
    /// <summary>{region} of a Global action scope.</summary>
    public const string World = "the world";

    /// <summary>{region}: the display names of a regional action scope joined by ", ", or "the world".</summary>
    public static string RegionOf(Scope scope) =>
        scope.IsGlobal ? World : string.Join(", ", scope.Regions.Select(RegionNames.Display));

    /// <summary>The context of <paramref name="def"/>. Its faction: for an Empower action the short names of its factions
    /// joined by ", " (Faction_Legion, Faction_Bandits → "Legion, Bandits", faction-empowerment D10); otherwise the
    /// second part of the first unit's prefab name (CHAR_Bandit_Thug → Bandit), or "-".</summary>
    public static MessageContext For(EventDefinition def, int minutes, int wave) => new(
        def.Name,
        def.Empower is { } empower
            ? string.Join(", ", empower.Factions.Select(FactionDenyList.ShortName))
            : FactionOf(def.Action?.Units.FirstOrDefault()?.Prefab),
        minutes,
        wave,
        def.Action?.Waves ?? 0,
        "-",
        RegionOf(def.Action?.Scope ?? def.Empower?.Scope ?? Scope.Global));

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

    /// <summary>The server-wide line of a revenge start (revenge D7): {faction} only, never the target or a place.</summary>
    public static readonly IReadOnlyList<string> RevengeAlertPool =
    [
        "The {faction} swear revenge on a vampire.",
        "The {faction} gather to hunt a vampire down.",
    ];

    /// <summary>The private warnings of revenge D4, warning k of n using the k-th line.</summary>
    static readonly IReadOnlyList<string> RevengeWarnings =
    [
        "The {faction} have noticed your killing.",
        "The {faction} are gathering against you.",
        "The {faction} are almost ready to strike.",
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
        [nameof(RevengeAlertPool)] = RevengeAlertPool,
    };

    /// <summary>The `.nyar` overview for anyone.</summary>
    public static string Overview(string version) =>
        $"Nyarlathotep, Lord of Chaos (v{version}) - server events: empowered factions, " +
        "sieges, defended zones, boss reinforcements, and spawn waves. Try .nyar status.";

    /// <summary>The commands lines of `.nyar` (D26): the ones the caller may run, as given, sorted, "commands: " first.
    /// A new line starts before a command that would take a line past <paramref name="maxBytes"/>, so no command is cut
    /// unless it alone is longer than a line (Codex 331bb3f F1, round 2).</summary>
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
        return [.. lines.Select(l => TextSink.CutToBytes(l, maxBytes))];   // a single command longer than a line is cut
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
            "region" => ctx.Region,
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

    /// <summary>The private warning <paramref name="k"/> of <paramref name="n"/> to the player whose heat climbs (revenge
    /// D4). <paramref name="factions"/> is the trigger's short faction names joined by ", " (RevengeRule.FactionText); the
    /// line names no player, holds no number and fits Wire.MaxBytes.</summary>
    public static string RevengeWarning(int k, int n, string factions) =>
        RevengeLine(RevengeWarnings[Math.Clamp(k, 1, Math.Min(n, RevengeWarnings.Count)) - 1], factions);

    /// <summary>The private line to the target when its revenge starts (revenge D7).</summary>
    public static string RevengeStart(string factions) => RevengeLine("The {faction} come for you.", factions);

    /// <summary>The private line to the target at a natural end, all waves defeated or an admin stop (revenge D8).</summary>
    public static string RevengeEnd(string factions) => RevengeLine("The {faction}' revenge is spent.", factions);

    static string RevengeLine(string template, string factions) =>
        TextSink.CutToBytes(Render(template, new MessageContext("-", TextSink.Clean(factions), 0, 0, 0, "-")), Wire.MaxBytes);

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

    /// <summary>The scoreboard of <paramref name="eventName"/> (wave-sets D10; design §9 D38): "&lt;event&gt; scoreboard: 1.
    /// &lt;name&gt; &lt;k&gt; kills, &lt;d&gt; deaths; 2. …; 3. …" and "&lt;p&gt; players, &lt;K&gt; kills, &lt;D&gt; deaths", or the one
    /// line "&lt;event&gt; scoreboard: no player scored". The only builder that takes a player's name (D12): each through
    /// TextSink.Name (20 characters, no angle brackets or control characters), the event name through TextSink.Clean;
    /// every line within Wire.MaxBytes.</summary>
    /// <remarks>For a `winner: KillDeath` event with at least one participant (swarm D6) one line follows: "&lt;event&gt;
    /// winner: &lt;name&gt;, K/D &lt;r&gt; (&lt;k&gt; kills, &lt;d&gt; deaths)", r to two decimals in the invariant culture, or
    /// "&lt;event&gt; winner: none" when no participant has a kill. With no participant only the "no player scored" line.</remarks>
    public static IReadOnlyList<string> ScoreboardLines(string eventName, ScoreSummary summary, WinnerRule winner = WinnerRule.None)
    {
        var name = TextSink.Clean(eventName);
        var head = $"{name} scoreboard: ";
        if (summary.Top.Count == 0) return [TextSink.CutToBytes(head + "no player scored", Wire.MaxBytes)];
        var ranks = summary.Top.Take(3).Select((r, i) => $"{i + 1}. {TextSink.Name(r.Name)} {r.Kills} kills, {r.Deaths} deaths");
        var lines = new List<string>
        {
            TextSink.CutToBytes(head + string.Join("; ", ranks), Wire.MaxBytes),
            $"{summary.Players} players, {summary.Kills} kills, {summary.Deaths} deaths",                // always plural, as D10 and D21 read
        };
        if (winner == WinnerRule.KillDeath)
            lines.Add(TextSink.CutToBytes(summary.Winner is { } w
                ? FormattableString.Invariant($"{name} winner: {TextSink.Name(w.Name)}, K/D {Scoreboard.Ratio(w):0.00} ({w.Kills} kills, {w.Deaths} deaths)")
                : $"{name} winner: none", Wire.MaxBytes));
        return lines;
    }

    static string Choose(IReadOnlyList<string> own, IReadOnlyList<string> pool, int pick)
    {
        var list = own.Count > 0 ? own : pool;
        return list[Math.Abs(pick % list.Count)];
    }

    static int MinutesLeft(DateTime endsUtc, DateTime utcNow) =>
        endsUtc > utcNow ? (int)Math.Ceiling((endsUtc - utcNow).TotalMinutes) : 0;
}
