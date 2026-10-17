#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>Where a destination wave stands (undead-hordes D6): marching, arrived within the radius, or stalled.</summary>
public enum WaveMarchState { Marching, Arrived, Stalled }

/// <summary>
/// One wave of a destination event on its way (undead-hordes D6): a front from the wave's spawn centre to the destination at
/// marchSpeed m/s by the apocalypse's march rule (<see cref="MarchRule"/>, unchanged): it holds while a player stands within
/// 60 m of it, and its stragglers hop forward only while no player has been within 100 m of the wave for 10 s. When the
/// front is within the destination's radius the wave has arrived; a front that cannot advance for 120 s while no player
/// holds it has stalled (time held by a player does not count). Either way the wave then takes the action's behaviour
/// where it stands and its march ends.
/// </summary>
public sealed class WaveMarch
{
    public static readonly TimeSpan StallAfter = TimeSpan.FromSeconds(120);

    public string EventId { get; }
    public int Wave { get; }
    public Front Front { get; }
    public int Radius { get; }
    public double Speed { get; }
    public DateTime StartedUtc { get; }
    public WaveMarchState State { get; private set; } = WaveMarchState.Marching;

    readonly MarchStall _stall = new();

    /// <summary>The seconds the front has been stuck (no walkable ground ahead) with no player holding it, in a row.</summary>
    public double StuckSeconds => _stall.StuckSeconds;

    public WaveMarch(string eventId, int wave, (float X, float Z) start, (float X, float Z) destination, int radius, double speed, DateTime startedUtc)
    {
        EventId = eventId;
        Wave = wave;
        Front = new Front(wave, start.X, start.Z, destination.X, destination.Z) { Kind = "wave" };
        Radius = radius;
        Speed = speed;
        StartedUtc = startedUtc;
    }

    /// <summary>A march of <paramref name="action"/>'s wave <paramref name="wave"/>, from its spawn centre to the resolved
    /// destination (an Admin destination is the starting admin's position, <paramref name="origin"/>); null without a
    /// destination, or with an Admin one and no origin.</summary>
    public static WaveMarch? Of(string eventId, SpawnWavesAction action, int wave, (float X, float Z) centre, (float X, float Y, float Z)? origin, DateTime utcNow)
    {
        if (action.Destination is not { } d) return null;
        (float X, float Z)? to = d.Location.Type == LocationType.Admin ? origin is { } o ? (o.X, o.Z) : null : (d.Location.X, d.Location.Z);
        return to is { } at ? new WaveMarch(eventId, wave, centre, at, d.Radius, action.MarchSpeed, utcNow) : null;
    }

    /// <summary>True when the wave's units take the action's behaviour: it arrived or stalled.</summary>
    public bool Released => State != WaveMarchState.Marching;

    /// <summary>One step over <paramref name="seconds"/>: the front moves, holds or crosses by <see cref="MarchRule.Step"/>
    /// (a hold on ground <paramref name="cross"/> could not read adds no stuck time);
    /// returns the arrival or stall line ("march &lt;event&gt; wave &lt;k&gt;: …", the service adds "[nyar] ") once, else null.
    /// <paramref name="held"/> is the march rule's held or crossed line. <paramref name="living"/> counts the wave's units
    /// for the arrival line.</summary>
    public string? Step(double seconds, IReadOnlyList<(float X, float Z)> players, Func<float, float, bool> walkable, DateTime utcNow, int living,
        out string? held, Func<float, float, bool?>? cross = null)
    {
        held = null;
        if (Released) return null;
        var unread = false;
        Func<float, float, bool?>? probe = cross is null ? null : (x, z) =>
        {
            var known = cross(x, z);
            if (known is null) unread = true;
            return known;
        };
        var step = MarchRule.Step(Front, Radius, Speed, seconds, players, walkable, utcNow, out held, probe);
        if (Apocalypse.Distance(Front.X, Front.Z, Front.GoalX, Front.GoalZ) <= Radius)
        {
            State = WaveMarchState.Arrived;
            return ArrivedLine(EventId, Wave, (int)Math.Round((utcNow - StartedUtc).TotalSeconds), living);
        }
        if (!_stall.Add(step, unread, seconds)) return null;
        State = WaveMarchState.Stalled;
        return StalledLine(EventId, Wave, (int)Math.Round(Apocalypse.Distance(Front.X, Front.Z, Front.GoalX, Front.GoalZ)));
    }

    /// <summary>"march &lt;event&gt; wave &lt;k&gt;: arrived after &lt;s&gt; s, &lt;n&gt; units".</summary>
    public static string ArrivedLine(string eventId, int wave, int seconds, int units) => $"march {eventId} wave {wave}: arrived after {seconds} s, {units} units";

    /// <summary>"march &lt;event&gt; wave &lt;k&gt;: stalled &lt;d&gt; m short of its destination" (A5): a distance, never a point,
    /// since a front starts at its spawn centre, which can be the starting admin's position.</summary>
    public static string StalledLine(string eventId, int wave, int metresShort) =>
        $"march {eventId} wave {wave}: stalled {metresShort} m short of its destination";
}

/// <summary>
/// A march's stall clock (undead-hordes D6, D11): the seconds its front is held by unwalkable ground with no player holding
/// it, in a row; 120 s of them is a stall. Ground the walk budget left unread adds nothing, a move clears it, and a player's
/// hold neither counts nor clears it.
/// </summary>
public sealed class MarchStall
{
    public double StuckSeconds { get; private set; }

    /// <summary>Counts one step; true once the front has stalled.</summary>
    public bool Add(MarchStep step, bool unread, double seconds)
    {
        switch (step)
        {
            case MarchStep.HeldNoGround when unread:
                return false;                                    // the ground was not read (the walk budget): no stuck time
            case MarchStep.HeldNoGround:
                StuckSeconds += Math.Max(0, seconds);
                return StuckSeconds >= WaveMarch.StallAfter.TotalSeconds;
            case MarchStep.Moved:
                StuckSeconds = 0;
                return false;
            default:
                return false;                                    // held by a player: neither counts nor clears the stuck time
        }
    }
}

/// <summary>
/// A per-unit pass bounded per tick (undead-hordes D18): each call takes at most <see cref="Bound"/> of the items (or the
/// call's own bound), in a round robin from where the last call stopped, so a pass over n items visits every one within
/// ceil(n / bound) calls and each call costs at most bound visits whatever n is. Several groups (the hordes, the hotspots)
/// share one tick's <see cref="PerPass"/> through <see cref="Share"/>. <see cref="Visits"/> counts every item taken.
/// </summary>
public sealed class RoundRobin(int bound)
{
    /// <summary>The march's hops, the roam's jumps and the unit watch's stall marks each take 500 a pass: at the 5 s move
    /// cadence every one of 1000 units is visited within 10 s (D18).</summary>
    public const int PerPass = 500;

    int _next;
    long? _lastKey;

    public int Bound { get; } = Math.Max(1, bound);
    public long Visits { get; private set; }

    public RoundRobin() : this(PerPass) { }

    /// <summary>A group's part of one tick's <see cref="PerPass"/>: all <paramref name="n"/> while the groups' <paramref name="total"/>
    /// fits, else its proportional share rounded up (at least 1). The shares sum to at most PerPass plus the number of groups,
    /// and each group is visited whole within ceil(total / PerPass) passes.</summary>
    public static int Share(int n, int total) =>
        total <= PerPass ? n : Math.Max(1, (int)Math.Ceiling((double)PerPass * n / total));

    /// <summary>At most <see cref="Bound"/> of <paramref name="items"/>, starting where the last call stopped (wrapping).</summary>
    public IReadOnlyList<T> Take<T>(IReadOnlyList<T> items) => Take(items, Bound);

    /// <summary>At most <paramref name="bound"/> of <paramref name="items"/> in the order of <paramref name="key"/>, starting
    /// after the last key the previous call took (wrapping): a unit that dies or joins shifts no one else's turn.</summary>
    public IReadOnlyList<T> Take<T>(IReadOnlyList<T> items, int bound, Func<T, long> key)
    {
        var n = items.Count;
        if (n == 0)
        {
            _lastKey = null;
            return [];
        }
        var order = items.OrderBy(key).ToList();
        var k = Math.Min(n, Math.Min(Bound, Math.Max(0, bound)));
        var start = 0;
        if (_lastKey is { } last)
        {
            int lo = 0, hi = n;                                  // the first key past the last one taken
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (key(order[mid]) > last) hi = mid; else lo = mid + 1;
            }
            start = lo % n;
        }
        var slice = new List<T>(k);
        for (var i = 0; i < k; i++) slice.Add(order[(start + i) % n]);
        if (k > 0) _lastKey = key(slice[^1]);
        Visits += k;
        return slice;
    }

    /// <summary>At most <paramref name="bound"/> (and <see cref="Bound"/>) of <paramref name="items"/>, from where the last call stopped.</summary>
    public IReadOnlyList<T> Take<T>(IReadOnlyList<T> items, int bound)
    {
        var n = items.Count;
        if (n == 0)
        {
            _next = 0;
            return [];
        }
        var k = Math.Min(n, Math.Min(Bound, Math.Max(0, bound)));
        var start = _next % n;
        var slice = new List<T>(k);
        for (var i = 0; i < k; i++) slice.Add(items[(start + i) % n]);
        _next = (start + k) % n;
        Visits += k;
        return slice;
    }
}
