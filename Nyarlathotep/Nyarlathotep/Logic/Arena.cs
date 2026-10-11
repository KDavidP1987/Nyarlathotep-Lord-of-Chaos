#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace Nyarlathotep.Logic;

/// <summary>A live tracked unit as the arena check reads it (swarm D3): its ledger key, its event (null for a `.nyar spawn`
/// unit), its spawn time, its position and, when it has one, its combat or seeded target's position on x and z.</summary>
public readonly record struct ArenaUnit(long Key, string? EventId, DateTime SpawnedUtc, float X, float Y, float Z, (float X, float Z)? Target = null);

/// <summary>Where one stray goes (swarm D3): the return spot on x and z within action.radius of the centre, at the walkable
/// height there, whether its target is dropped (a player outside the arena, or none known), and how far out it stood.</summary>
public readonly record struct ArenaReturn(long Key, float X, float Y, float Z, bool DropTarget, float Distance);

/// <summary>One arena check of one running instance (swarm D3): the strays it returns and the farthest of them; a check that
/// returned nothing has no log line.</summary>
public sealed record ArenaCheck(string EventId, IReadOnlyList<ArenaReturn> Returns, float Farthest)
{
    public string? LogLine => Returns.Count == 0 ? null : Arena.LogLine(EventId, Returns.Count, Farthest);
}

/// <summary>
/// The containment rule (swarm D3, Business rules 1-2, S-5, S-13). Pure: Services/EventScheduler runs it every
/// <see cref="CheckSeconds"/> s over SpawnTracker's live units and writes the returns. The centre is the Point location's x
/// and z, or the starting admin's position captured at the start (ActiveEvent.Origin) for an Admin location; never a
/// Point's saved height. A unit of the running instance (its event id, spawned at or after Instance.StartedUtc, the
/// boundary wave-sets A3 drew) farther than radius + <see cref="Slack"/> m on x and z is returned to a spot within
/// action.radius of the centre, at the spawn path's walkable height there. Units of another event, of an earlier
/// instance, of `.nyar spawn` and of an event without `arena` are never moved.
/// </summary>
public sealed partial record Arena
{
    public const int MinRadius = 15;
    public const int MaxRadius = 60;
    /// <summary>The slack past the arena's edge before a unit counts as a stray (S-13): it finishes a swing at the edge.</summary>
    public const float Slack = 10f;
    /// <summary>How often the check runs: the existing behaviour cadence.</summary>
    public const int CheckSeconds = HuntPlan.IntervalSeconds;

    /// <summary>The centre of <paramref name="a"/>'s arena on x and z, or null when it has no arena or no centre (an Admin
    /// start without an origin, an AroundPlayer location, which validation refuses).</summary>
    public static (float X, float Z)? Centre(ActiveEvent a) =>
        a.Definition.Action is not { Arena: not null } action ? null
        : action.Location.Type switch
        {
            LocationType.Point => (action.Location.X, action.Location.Z),
            LocationType.Admin => a.Origin is { } o ? (o.X, o.Z) : null,
            _ => null,
        };

    /// <summary>The distance on x and z.</summary>
    public static float Distance((float X, float Z) centre, float x, float z)
    {
        var dx = x - centre.X;
        var dz = z - centre.Z;
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>A unit at <paramref name="x"/>, <paramref name="z"/> is a stray: farther than radius + <see cref="Slack"/>
    /// from the centre. One at or inside that distance is left alone.</summary>
    public static bool Strays((float X, float Z) centre, int radius, float x, float z) => Distance(centre, x, z) > radius + Slack;

    /// <summary>The unit belongs to the running instance <paramref name="a"/>: its event id, spawned at or after its start.</summary>
    public static bool Ours(ArenaUnit u, ActiveEvent a) => u.EventId == a.Id && u.SpawnedUtc >= a.Instance.StartedUtc;

    /// <summary>A spot within <paramref name="actionRadius"/> of the centre: <paramref name="fraction"/> (0..1) picks the
    /// distance, uniform over the disc, and <paramref name="angle"/> the direction.</summary>
    public static (float X, float Z) ReturnSpot((float X, float Z) centre, int actionRadius, double angle, double fraction)
    {
        var r = actionRadius * Math.Sqrt(Math.Clamp(fraction, 0, 1));
        return ((float)(centre.X + r * Math.Cos(angle)), (float)(centre.Z + r * Math.Sin(angle)));
    }

    /// <summary>One check of <paramref name="a"/>: every unit of its running instance that strays, with its return spot at
    /// <paramref name="walkableHeight"/> (x, z) and its target dropped unless that target stands inside the arena. Null when
    /// <paramref name="a"/> has no arena or no centre. <paramref name="random"/> gives values in [0, 1).</summary>
    public static ArenaCheck? Check(ActiveEvent a, IEnumerable<ArenaUnit> units, Func<float, float, float> walkableHeight, Func<double> random)
    {
        if (Centre(a) is not { } centre || a.Definition.Action is not { Arena: { } arena } action) return null;
        var returns = new List<ArenaReturn>();
        var farthest = 0f;
        foreach (var u in units)
        {
            if (!Ours(u, a) || !Strays(centre, arena.Radius, u.X, u.Z)) continue;
            var d = Distance(centre, u.X, u.Z);
            var (x, z) = ReturnSpot(centre, action.Radius, random() * 2 * Math.PI, random());
            var keep = u.Target is { } t && Distance(centre, t.X, t.Z) <= arena.Radius;
            returns.Add(new ArenaReturn(u.Key, x, walkableHeight(x, z), z, !keep, d));
            farthest = Math.Max(farthest, d);
        }
        return new ArenaCheck(a.Id, returns, farthest);
    }

    /// <summary>The checks of every running arena event, each over its own instance's units only.</summary>
    public static IReadOnlyList<ArenaCheck> CheckAll(IEnumerable<ActiveEvent> active, IReadOnlyList<ArenaUnit> units,
        Func<float, float, float> walkableHeight, Func<double> random)
    {
        var checks = new List<ArenaCheck>();
        foreach (var a in active)
            if (Check(a, units, walkableHeight, random) is { } c) checks.Add(c);
        return checks;
    }

    /// <summary>"arena &lt;id&gt;: returned &lt;n&gt;, farthest &lt;d&gt; m" (the service adds "[nyar] "): counts and a
    /// distance only, never a name or a position.</summary>
    public static string LogLine(string eventId, int returned, float farthest) =>
        string.Create(CultureInfo.InvariantCulture, $"arena {eventId}: returned {returned}, farthest {farthest:0} m");
}
