#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// Duplicate-action rules (foundation Business rules 3, 8, 9; D6). Every caller runs on the server main thread,
// so these are plain collections: the races they settle are orderings of calls, not threads.

/// <summary>One active instance per definition.</summary>
public sealed class InstanceGuard
{
    readonly HashSet<string> _active = new(StringComparer.Ordinal);

    public bool IsActive(string id) => _active.Contains(id);
    public int Count => _active.Count;
    public IReadOnlyCollection<string> Active => _active;

    /// <summary>False, with "already active", when the definition already runs.</summary>
    public bool TryBegin(string id, out string? reply)
    {
        if (_active.Add(id)) { reply = null; return true; }
        reply = "already active";
        return false;
    }

    /// <summary>False, with "not active", when there is nothing to end.</summary>
    public bool TryEnd(string id, out string? reply)
    {
        if (_active.Remove(id)) { reply = null; return true; }
        reply = "not active";
        return false;
    }

    public void Clear() => _active.Clear();
}

/// <summary>A trigger from the same source within the window fires once.</summary>
public sealed class TriggerDedupe(TimeSpan window)
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    readonly Dictionary<string, DateTime> _last = new(StringComparer.Ordinal);

    public TriggerDedupe() : this(DefaultWindow) { }

    public bool ShouldFire(string sourceKey, DateTime utcNow)
    {
        if (_last.TryGetValue(sourceKey, out var at) && utcNow - at < window && utcNow >= at) return false;
        _last[sourceKey] = utcNow;
        if (_last.Count > 256) Prune(utcNow);
        return true;
    }

    void Prune(DateTime utcNow)
    {
        foreach (var k in _last.Where(kv => utcNow - kv.Value >= window).Select(kv => kv.Key).ToList()) _last.Remove(k);
    }
}

/// <summary>`event set`, `enable` and `disable` write events.json only when the file is the one last loaded.</summary>
public static class StaleFile
{
    public const string Refusal = "events.json changed on disk, run .nyar event reload first";

    public static string? CheckWritable(DateTime? loadedWriteUtc, DateTime? currentWriteUtc) =>
        loadedWriteUtc is not null && loadedWriteUtc == currentWriteUtc ? null : Refusal;
}

public enum PurgeConfirmResult { Purge, NothingToPurge, NotArmed }

/// <summary>`.nyar purge` arms, `.nyar purge confirm` within 30 s by the same admin fires. A confirm consumes
/// the arming, so a second confirm is "nothing to purge" (after a purge there is nothing left) or "run .nyar
/// purge first".</summary>
public sealed class PurgeArming
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    readonly Dictionary<ulong, DateTime> _armedAt = [];

    public void Arm(ulong adminId, DateTime utcNow) => _armedAt[adminId] = utcNow;

    public PurgeConfirmResult Confirm(ulong adminId, DateTime utcNow, bool anythingToPurge)
    {
        var armed = _armedAt.TryGetValue(adminId, out var at) && utcNow >= at && utcNow - at <= Window;
        _armedAt.Remove(adminId);
        if (!anythingToPurge) return PurgeConfirmResult.NothingToPurge;
        return armed ? PurgeConfirmResult.Purge : PurgeConfirmResult.NotArmed;
    }
}

/// <summary>A running instance holds the definition it started with; a reload never changes it (D6).</summary>
public sealed record RunningInstance(EventDefinition Definition, DateTime StartedUtc, DateTime EndsUtc);
