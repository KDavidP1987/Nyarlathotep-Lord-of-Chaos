#nullable enable
using System;
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>How a wave unit's spawn point was found (walkable-spawns D2, D3): its own ring point, another point of the
/// search, the centre, or no checked point (all blocked, the budget spent, the check failed, or no height to check at).</summary>
public enum PointKind { Ring, Moved, Centre, Unchecked }

public readonly record struct PlacedPoint(float X, float Z, PointKind Kind);

/// <summary>The walkable-point search of one unit (walkable-spawns D2, A6).</summary>
public static class SpawnPoints
{
    /// <summary>Angles per ring of the search.</summary>
    public const int Angles = 12;

    /// <summary>The most isFree calls one search makes: the ring point, the 11 other ring angles, the 12 at half the
    /// radius and the centre.</summary>
    public const int MaxChecks = 1 + (Angles - 1) + Angles + 1;

    /// <summary>The ring point when free; else the 11 other angles of 12 on the same ring (starting from the ring point's
    /// angle), then the same 12 angles at half the radius, then the centre; all blocked gives the centre, unchecked. At
    /// radius 0 the centre is checked once.</summary>
    public static PlacedPoint Choose((float X, float Z) point, (float X, float Z) centre, float radius, Func<float, float, bool> isFree)
    {
        if (radius <= 0)
            return new PlacedPoint(centre.X, centre.Z, isFree(centre.X, centre.Z) ? PointKind.Centre : PointKind.Unchecked);
        if (isFree(point.X, point.Z)) return new PlacedPoint(point.X, point.Z, PointKind.Ring);

        var start = Math.Atan2(point.Z - centre.Z, point.X - centre.X);
        foreach (var (r, from) in new[] { (radius, 1), (radius / 2, 0) })
        {
            for (var k = from; k < Angles; k++)
            {
                var a = start + k * 2 * Math.PI / Angles;
                var x = centre.X + r * (float)Math.Cos(a);
                var z = centre.Z + r * (float)Math.Sin(a);
                if (isFree(x, z)) return new PlacedPoint(x, z, PointKind.Moved);
            }
        }
        return new PlacedPoint(centre.X, centre.Z, isFree(centre.X, centre.Z) ? PointKind.Centre : PointKind.Unchecked);
    }
}

/// <summary>The centre heights a wave's walk check reads at (walkable-spawns D5, A13): a height that is not a number, below
/// the ground or at <see cref="MaxY"/> and above is outside the map's range, and the wave falls open.</summary>
public static class WalkHeight
{
    public const float MaxY = 1000f;

    /// <summary>Why <paramref name="y"/> is not checked, or null when it is.</summary>
    public static string? Problem(float y) =>
        float.IsNaN(y) || float.IsInfinity(y) ? "height is not a number"
        : y < 0 || y >= MaxY ? "height out of range"
        : null;
}

/// <summary>The game's walk check at one height level (Services/WalkCheck). Either call may throw; the wave then
/// falls open (walkable-spawns D5).</summary>
public interface IWalkProbe
{
    bool IsFree(float x, float z);
    bool IsGrounded(float x, float z);
}

/// <summary>Game calls per server tick for wave placement (walkable-spawns D3, A3): 2,500, reset each tick. A point past
/// the budget keeps its ring point, unchecked, without calling the game.</summary>
public sealed class WalkBudget(int perTick = WalkBudget.PerTick)
{
    public const int PerTick = 2500;
    readonly int _perTick = perTick;
    int _left = perTick;

    public int Left => _left;
    public void Reset() => _left = _perTick;

    /// <summary>Takes <paramref name="calls"/> game calls; false (taking none) when fewer are left.</summary>
    public bool TryTake(int calls)
    {
        if (_left < calls) return false;
        _left -= calls;
        return true;
    }
}

/// <summary>One wave's walk state (walkable-spawns D5, A5): after a failed check the rest of the wave is treated as free
/// and grounded without calling the game; the next wave gets a new one and tries again.</summary>
public sealed class WaveWalk(IWalkProbe? probe, WalkBudget budget)
{
    /// <summary>The probe, or null when this wave has nothing to check (no map data, no height at the centre).</summary>
    public IWalkProbe? Probe { get; } = probe;
    public WalkBudget Budget { get; } = budget;

    /// <summary>The reason of the failure that opened this wave's fall-open, or null.</summary>
    public string? Failure { get; private set; }

    /// <summary>True once a check of this wave returned from the game; only such a wave closes a failure streak.</summary>
    public bool Answered { get; private set; }

    /// <summary>Opens the wave's fall-open with <paramref name="reason"/>; the first reason is kept.</summary>
    public void Fail(string reason) => Failure ??= reason;

    internal void Returned() => Answered = true;
}

/// <summary>A wave's unit points (walkable-spawns D3): exactly one per ring point, in ring order.</summary>
public static class WavePoints
{
    /// <summary>Plans one point per ring point with <see cref="SpawnPoints.Choose"/>, a point being walkable when free and
    /// grounded at the wave's height level; each game call takes one unit of the budget, and a blocked point skips its
    /// grounded call. A failed check opens <paramref name="walk"/>'s fall-open (the point and the
    /// rest of the wave keep their ring points, unchecked); a spent budget leaves the point unchecked at its ring point.
    /// With <paramref name="inScope"/> (regions D6, A1, A9) a point outside the action scope counts as blocked before any
    /// budget is spent, and an unchecked ring point outside it uses the centre instead, which the Point and Admin checks
    /// keep in scope.</summary>
    public static List<PlacedPoint> Plan(IReadOnlyList<(float X, float Z)> ring, (float X, float Z) centre, float radius, WaveWalk walk,
        Func<float, float, bool>? inScope = null)
    {
        var points = new List<PlacedPoint>(ring.Count);
        PlacedPoint Unchecked((float X, float Z) p) =>
            inScope is null || inScope(p.X, p.Z) ? new PlacedPoint(p.X, p.Z, PointKind.Unchecked) : new PlacedPoint(centre.X, centre.Z, PointKind.Unchecked);
        foreach (var p in ring)
        {
            if (walk.Probe is not { } probe || walk.Failure is not null)
            {
                points.Add(Unchecked(p));
                continue;
            }
            var spent = false;
            try
            {
                var chosen = SpawnPoints.Choose(p, centre, radius, (x, z) =>
                {
                    if (spent) return false;
                    if (float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z))
                        throw new ArgumentException("point is not a number");
                    if (inScope is not null && !inScope(x, z)) return false;           // before the budget (A1)
                    if (!walk.Budget.TryTake(1)) { spent = true; return false; }
                    var free = probe.IsFree(x, z);
                    walk.Returned();
                    if (!free) return false;
                    if (!walk.Budget.TryTake(1)) { spent = true; return false; }
                    return probe.IsGrounded(x, z);
                });
                points.Add(spent ? Unchecked(p) : chosen);
            }
            catch (Exception e)
            {
                walk.Fail(e.Message);
                points.Add(Unchecked(p));
            }
        }
        return points;
    }

    /// <summary>The scope check of a wave's points (regions D6): null for a Global scope, which then checks nothing; a
    /// regional scope without a region lookup holds no point (fails closed, so every point falls to the centre).</summary>
    public static Func<float, float, bool>? ScopeCheck(Scope scope, Func<float, float, string>? regionOf) =>
        scope.IsGlobal ? null
        : regionOf is null ? (_, _) => false
        : (x, z) => scope.Names(regionOf(x, z));

    /// <summary>The moved and unchecked counts of a plan, for the wave line (A2).</summary>
    public static (int Moved, int Unchecked) Counts(IEnumerable<PlacedPoint> points)
    {
        int moved = 0, unchecked_ = 0;
        foreach (var p in points)
        {
            if (p.Kind is PointKind.Moved or PointKind.Centre) moved++;
            else if (p.Kind == PointKind.Unchecked) unchecked_++;
        }
        return (moved, unchecked_);
    }
}

/// <summary>The walk check's failure streak and health entry (walkable-spawns D5, D6).</summary>
public sealed class SpawnHealth
{
    public const string Entry = "spawns: walk check unavailable";

    public bool Open { get; private set; }

    /// <summary>A wave's check failed: opens the streak; true when this opened it (log once per streak).</summary>
    public bool Failed()
    {
        if (Open) return false;
        Open = true;
        return true;
    }

    /// <summary>A wave's check answered: closes the streak.</summary>
    public void Answered() => Open = false;

    /// <summary>Records a planned wave's outcome: a failure logs "walk check unavailable: &lt;reason&gt;" once per streak;
    /// a wave with a check that returned closes the streak; a wave that called nothing (no height, the budget spent, no
    /// point) leaves it as it is (D5).</summary>
    public void Settle(WaveWalk walk, Action<string> log)
    {
        if (walk.Failure is { } reason)
        {
            if (Failed()) log($"walk check unavailable: {reason}");
        }
        else if (walk.Answered) Answered();
    }

    public IReadOnlyList<string> Entries => Open ? [Entry] : [];
}
