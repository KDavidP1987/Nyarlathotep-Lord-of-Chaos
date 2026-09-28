#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The admin twins' rate (raphael-api-admin D6, S-4): at most <see cref="PerSecond"/> twins per admin SteamID in
/// any 1-second window, sliding. Every admitted twin counts, whatever it answers; a refused one does not. The gate holds
/// at most <see cref="Capacity"/> admins: when full, admins idle for a second or more are pruned, and a newcomer to a
/// gate still full of active admins is refused. Memory only: a restart clears it. Reads and human commands never reach
/// it. Runs on the server main thread, like every command. Wall-clock time: a clock stepped back forgets the admin's
/// window rather than refusing them until the clock catches up.</summary>
public sealed class RateGate(int perSecond = RateGate.PerSecond, int capacity = RateGate.Capacity)
{
    public const int PerSecond = 5;
    public const int Capacity = 64;
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    /// <summary>The seconds a refused twin is told to wait (`code=ratelimit secs=1`).</summary>
    public const int RetrySeconds = 1;

    readonly Dictionary<ulong, Queue<DateTime>> _admitted = new();

    /// <summary>The admins the gate holds now.</summary>
    public int Count => _admitted.Count;

    /// <summary>True, and counted, when <paramref name="adminId"/> has had fewer than <see cref="PerSecond"/> twins
    /// admitted in the second before <paramref name="utcNow"/>.</summary>
    public bool Admit(ulong adminId, DateTime utcNow)
    {
        if (!_admitted.TryGetValue(adminId, out var times))
        {
            if (_admitted.Count >= capacity) Prune(utcNow);
            if (_admitted.Count >= capacity) return false;
            times = new Queue<DateTime>();
            _admitted[adminId] = times;
        }
        if (times.Count > 0 && times.Last() > utcNow) times.Clear();      // the clock stepped back: forget, never lock out
        while (times.Count > 0 && utcNow - times.Peek() >= Window) times.Dequeue();
        if (times.Count >= perSecond) return false;
        times.Enqueue(utcNow);
        return true;
    }

    void Prune(DateTime utcNow)
    {
        foreach (var id in _admitted.Where(kv => kv.Value.Count == 0 || utcNow - kv.Value.Last() >= Window || kv.Value.Last() > utcNow).Select(kv => kv.Key).ToList())
            _admitted.Remove(id);
    }
}
