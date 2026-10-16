#nullable enable

namespace Nyarlathotep.Logic;

/// <summary>Diagnostic (2026-10-15, G-Portal test, Raphael feedback item 3): on that server wave units left the ledger within
/// seconds of spawning ("wave cleared" with no player kill, `units=0` in api status), and the only line saying how a unit
/// left needs VerboseLogging. This names, whatever VerboseLogging says, each unit of an event that leaves within
/// <see cref="WindowSeconds"/> of its spawn and how: "died" with its killer's prefab, or "removed by the game". At most
/// <see cref="PerMinute"/> lines a minute; the next line logged counts the ones left out. Never a name or a position.</summary>
public sealed class EarlyDepartureLog
{
    public const int WindowSeconds = 60;
    public const int PerMinute = 20;

    DateTime _minuteStart = DateTime.MinValue;
    int _sent;
    int _skipped;

    /// <summary>The line for <paramref name="unit"/> leaving at <paramref name="utcNow"/> by <paramref name="how"/>, or null
    /// when it is not an event's unit, lived past the window, or the minute's lines are spent.</summary>
    public string? Line(TrackedUnit unit, string how, DateTime utcNow)
    {
        if (unit.EventId is null) return null;
        var age = (utcNow - unit.SpawnedUtc).TotalSeconds;
        if (age < 0 || age > WindowSeconds) return null;
        if (utcNow - _minuteStart >= TimeSpan.FromMinutes(1))
        {
            _minuteStart = utcNow;
            _sent = 0;
        }
        if (_sent >= PerMinute)
        {
            _skipped++;
            return null;
        }
        _sent++;
        var more = _skipped > 0 ? $" ({_skipped} more not logged)" : "";
        _skipped = 0;
        var wave = unit.Wave > 0 ? $" wave {unit.Wave}" : "";
        return $"early departure: {unit.Prefab} of {unit.EventId}{wave} after {age:0} s: {how}{more}";
    }
}
