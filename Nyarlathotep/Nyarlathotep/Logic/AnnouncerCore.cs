#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The Announcer's rules without the game (foundation D13, Epic D42): when a wave warning fires, when the daily banner
// fires and what it names, the share limits, the login gate, the health clock, and the one-per-second queue of 20.
// Services/Announcer and Services/HealthMonitor drive these from the scheduler tick and the login hook.

/// <summary>A wave warning may be superseded by a later one; every other line is informational.</summary>
public enum LineKind { Info, Warning }

/// <summary>One server-wide line waiting to leave. <see cref="EventId"/> ties a warning to its event, so the event's
/// end takes its unsent warnings with it.</summary>
public sealed record QueuedLine(string Text, LineKind Kind, string? EventId = null);

/// <summary>Server-wide lines leave at most one per second from a queue of at most <see cref="Capacity"/>. A full queue
/// drops its oldest informational line, or, when every line is a wave warning, its oldest warning (the newer warning
/// supersedes it), and says which in a log line.</summary>
public sealed class AnnounceQueue(Action<string> log)
{
    public const int Capacity = 20;
    public static readonly TimeSpan Spacing = TimeSpan.FromSeconds(1);

    readonly List<QueuedLine> _lines = [];
    DateTime? _lastSent;

    public int Count => _lines.Count;

    public void Enqueue(QueuedLine line)
    {
        if (_lines.Count >= Capacity)
        {
            var i = _lines.FindIndex(l => l.Kind == LineKind.Info);
            if (i < 0) i = 0;
            var dropped = _lines[i];
            _lines.RemoveAt(i);
            log($"announce: queue full ({Capacity}); dropped the oldest {(dropped.Kind == LineKind.Info ? "informational line" : "wave warning")}: {dropped.Text}");
        }
        _lines.Add(line);
    }

    /// <summary>The next line to send, or null when the queue is empty or the last one left less than a second ago.</summary>
    public QueuedLine? Next(DateTime utcNow)
    {
        if (_lines.Count == 0) return null;
        if (_lastSent is { } last && utcNow - last < Spacing) return null;
        var line = _lines[0];
        _lines.RemoveAt(0);
        _lastSent = utcNow;
        return line;
    }

    /// <summary>Drops the unsent wave warnings of <paramref name="eventId"/> (its end or a purge).</summary>
    public int DropWarnings(string? eventId = null) =>
        _lines.RemoveAll(l => l.Kind == LineKind.Warning && (eventId is null || l.EventId == eventId));
}

/// <summary>Pre-wave warnings: each wave gets one warning at each configured offset. Offsets longer than the time left
/// when the wave is first seen are skipped; when a late tick passes two offsets at once, only the nearer fires.</summary>
public sealed class WarningClock(IReadOnlyList<int> offsets)
{
    readonly Dictionary<string, HashSet<int>> _handled = new(StringComparer.Ordinal);

    public IReadOnlyList<int> Offsets => offsets;

    /// <summary>The key of wave <paramref name="wave"/> of the instance started at <paramref name="startedUtc"/>, so a
    /// restart of the same event warns afresh.</summary>
    public static string Key(string eventId, DateTime startedUtc, int wave) => $"{eventId}|{startedUtc.Ticks}|{wave}";

    /// <summary>The offset to warn at now, or null. <paramref name="secondsLeft"/> is the whole seconds until the wave,
    /// rounded up.</summary>
    public int? Due(string key, int secondsLeft)
    {
        if (!_handled.TryGetValue(key, out var handled))
            _handled[key] = handled = offsets.Where(o => o > secondsLeft).ToHashSet();
        if (secondsLeft <= 0)
        {
            handled.UnionWith(offsets);
            return null;
        }
        var crossed = offsets.Where(o => o >= secondsLeft && !handled.Contains(o)).ToList();
        if (crossed.Count == 0) return null;
        handled.UnionWith(crossed);
        return crossed.Min();
    }

    /// <summary>Forgets every wave not in <paramref name="live"/> (ended events, spawned waves).</summary>
    public void Keep(IReadOnlyCollection<string> live)
    {
        foreach (var key in _handled.Keys.Where(k => !live.Contains(k)).ToList()) _handled.Remove(key);
    }

    public int Tracked => _handled.Count;
}

/// <summary>The upcoming wave of a running event, for its warnings: the next wave not yet spawned, when it is due before
/// the event's end. Wave 1 is due at the start, so only later waves are ever warned of.</summary>
public static class UpcomingWave
{
    public static (int Wave, DateTime AtUtc)? Of(ActiveEvent active)
    {
        if (active.Definition.Action is not { } action || active.WavesSpawned >= action.Waves) return null;
        var at = active.Instance.StartedUtc.AddSeconds((double)active.WavesSpawned * action.IntervalSeconds);
        if (at >= active.Instance.EndsUtc) return null;
        return (active.WavesSpawned + 1, at);
    }

    public static int SecondsLeft(DateTime atUtc, DateTime utcNow) => (int)Math.Ceiling((atUtc - utcNow).TotalSeconds);
}

/// <summary>The daily banner (A18, A19): once per server-local day at DailyBannerTime under D4's clock rules, its
/// occurrence key kept in state.json so a restart in the same minute does not fire it again; a missed day is never
/// replayed. Until the stats child adds the digest it names the enabled Schedule events still due later that day.</summary>
public static class DailyBanner
{
    /// <summary>The event names for the banner when it is due now, or null when it is not due. An empty list means it is
    /// due with nothing to name: the occurrence is spent and nothing is sent.</summary>
    public static IReadOnlyList<string>? Due(StateDocument state, TimeOnly time, DateTime utcNow, TimeZoneInfo zone, DefinitionSet set)
    {
        var key = Schedule.DailyDue(time, utcNow, zone, state.DailyBanner);
        if (key is null) return null;
        state.DailyBanner = key;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var minute = new TimeOnly(local.Hour, local.Minute);
        return set.All
            .Where(d => d.Startable && d.Trigger.Type == TriggerType.Schedule && d.Trigger.Days.Contains(local.DayOfWeek)
                        && d.Trigger.Times.Any(t => t > minute))
            .OrderBy(d => d.Trigger.Times.Where(t => t > minute).Min())
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => d.Name)
            .ToList();
    }
}

/// <summary>Player shares (Epic D42; the command comes with the stats child): a player waits ShareCooldownSeconds after
/// their last share, and the server passes at most ShareMaxPerMinute in any 60 s. Calls are processed one at a time on
/// the server's main thread, so of two shares competing for the last allowance the first processed passes.</summary>
public sealed class ShareLimiter(int cooldownSeconds, int maxPerMinute)
{
    static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    readonly Dictionary<ulong, DateTime> _last = new();
    readonly Queue<DateTime> _recent = new();

    /// <summary>Null when the share passes (and is counted), else the one-line reason.</summary>
    public string? TryShare(ulong player, DateTime utcNow)
    {
        if (_last.TryGetValue(player, out var last) && utcNow - last < TimeSpan.FromSeconds(cooldownSeconds))
            return $"share: wait {Math.Ceiling((last.AddSeconds(cooldownSeconds) - utcNow).TotalSeconds)} s";
        while (_recent.Count > 0 && utcNow - _recent.Peek() >= Minute) _recent.Dequeue();
        if (_recent.Count >= maxPerMinute)
            return $"share: the server allows {maxPerMinute} a minute; try again in {Math.Ceiling((_recent.Peek() + Minute - utcNow).TotalSeconds)} s";
        _last[player] = utcNow;
        _recent.Enqueue(utcNow);
        return null;
    }
}

/// <summary>The login message fires once per connect and not on a reconnect within <see cref="Quiet"/> of the previous
/// connect.</summary>
public sealed class LoginGate
{
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(60);
    readonly Dictionary<ulong, DateTime> _lastConnect = new();

    public bool ShouldGreet(ulong player, DateTime utcNow)
    {
        var greet = !_lastConnect.TryGetValue(player, out var last) || utcNow - last >= Quiet;
        _lastConnect[player] = utcNow;
        return greet;
    }
}

/// <summary>The health line (D31): due every <see cref="Interval"/>, the first one an interval after boot.</summary>
public sealed class HealthClock
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    DateTime? _next;

    public bool Due(DateTime utcNow)
    {
        _next ??= utcNow + Interval;
        if (utcNow < _next) return false;
        _next = utcNow + Interval;
        return true;
    }

    /// <summary>"nyar health: &lt;n&gt; events, &lt;m&gt; tracked, degraded: &lt;list&gt;", "none" for an empty list.</summary>
    public static string Line(int events, int tracked, IReadOnlyCollection<string> degraded) =>
        $"nyar health: {events} events, {tracked} tracked, degraded: {(degraded.Count == 0 ? "none" : string.Join(", ", degraded))}";
}
