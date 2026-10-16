#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        a.Definition.Action is not { Arena: not null } ? null : LocationCentre(a);

    /// <summary>The fixed wave centre of <paramref name="a"/>'s location on x and z: a Point's, or the starting admin's
    /// position for an Admin location; null for a location whose waves pick their own centres (AroundPlayer, Region) or
    /// an Admin start without an origin. The arena's centre and the pursuit area's (wave-variety D8) are this one.</summary>
    public static (float X, float Z)? LocationCentre(ActiveEvent a) =>
        a.Definition.Action?.Location switch
        {
            { Type: LocationType.Point } l => (l.X, l.Z),
            { Type: LocationType.Admin } => a.Origin is { } o ? (o.X, o.Z) : null,
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
    /// <paramref name="a"/> has no arena or no centre, or pursues (its arena is lifted, wave-variety D8).
    /// <paramref name="random"/> gives values in [0, 1).</summary>
    public static ArenaCheck? Check(ActiveEvent a, IEnumerable<ArenaUnit> units, Func<float, float, float> walkableHeight, Func<double> random)
    {
        if (a.Pursuing || Centre(a) is not { } centre || a.Definition.Action is not { Arena: { } arena } action) return null;
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

    /// <summary>The walkable height a return lands at (swarm D3, step 2): the spawn path's own anchor height (the starting
    /// admin's position for an Admin location, a Point's stored height: the height WavePlan.WalkY checks the wave at and the
    /// regroup moves to); for a Point stored without a height, which has no anchor and spawns at 0 for the game to ground,
    /// the median height of the instance's units standing inside the arena (radius + <see cref="Slack"/>), which the game
    /// has grounded, else of all its units; never 0 while any unit of the instance is known. 0 with no unit (then nothing
    /// strays and nothing is returned).</summary>
    public static float ReturnHeight(ActiveEvent a, IReadOnlyList<ArenaUnit> units)
    {
        if (a.Definition.Action is not { } action) return 0f;
        if (WavePlan.GroupAnchor(action.Location, a.Origin) is { } anchor) return anchor.Y;
        var ours = units.Where(u => Ours(u, a)).ToList();
        if (ours.Count == 0) return 0f;
        var inside = Centre(a) is { } c && action.Arena is { } arena
            ? ours.Where(u => !Strays(c, arena.Radius, u.X, u.Z)).ToList()
            : [];
        var from = inside.Count > 0 ? inside : ours;
        var ys = from.Select(u => u.Y).OrderBy(y => y).ToList();
        return ys[(ys.Count - 1) / 2];                                       // the lower median
    }

    /// <summary>Whether a returned unit's AggroBuffer entry is dropped (swarm D3, the target drop): its entity stands
    /// outside the arena (farther than arena.radius from the centre on x and z) or its position cannot be read. An entry
    /// on someone inside the arena stays, so a returned unit keeps fighting the players in it.</summary>
    public static bool DropsEntry((float X, float Z) centre, int arenaRadius, (float X, float Z)? at) =>
        at is not { } p || Distance(centre, p.X, p.Z) > arenaRadius;
}

/// <summary>The game side of one arena check (swarm D3, D4), implemented by Services/SpawnTracker over its ledger and
/// entities: every member may throw, which the check takes as a failure of that read or write only.</summary>
public interface IArenaWorld
{
    /// <summary>The ledger's tracked units (key, event, spawn time).</summary>
    IEnumerable<TrackedUnit> Units { get; }

    /// <summary>One unit's position and target; null when the unit is gone (died, despawned) since the ledger listed it.</summary>
    ArenaUnit? Read(TrackedUnit unit);

    /// <summary>Moves one of our units to <paramref name="r"/>'s spot, drops its targets outside the arena when
    /// <paramref name="r"/>.DropTarget, and hands it to the regroup check at <paramref name="anchor"/> when the location has
    /// one. A unit no longer there is left alone.</summary>
    void Return(ArenaReturn r, (float X, float Z) centre, int arenaRadius, (float X, float Y, float Z)? anchor);
}

/// <summary>
/// The arena phase (swarm D3, D4): every <see cref="Arena.CheckSeconds"/> s while a running instance has an arena, each such
/// instance's own live units (SpawnTracker's ledger, filtered by event id and instance start) are read, checked with
/// <see cref="Arena.Check"/> and the strays returned. Guarded per unit and per event: a throwing read or write skips that
/// unit and the rest of the check runs; a check that met any throw is a failing check, logged
/// "arena &lt;id&gt;: check failed: &lt;message&gt;" once per streak, holding <see cref="Failing"/> on the health line and in
/// `.nyar status` until one of that event's checks runs clean, the event ends or the server restarts. <see cref="Run"/>
/// never throws, so no later tick phase, despawn batch or event end is skipped because of it.
/// </summary>
public sealed class ArenaPhase
{
    public const string Failing = "arena: check failing";

    readonly SortedSet<string> _failing = new(StringComparer.Ordinal);
    /// <summary>A throw outside any one event's check (code review F5): logged once per streak and held on the health
    /// line like an event's, until a tick runs through.</summary>
    bool _phaseFailing;
    DateTime _next;

    /// <summary>The health entry: one while any arena event's check, or the phase itself, is failing.</summary>
    public IReadOnlyList<string> Health => _failing.Count > 0 || _phaseFailing ? [Failing] : [];

    public static string FailedLine(string eventId, string message) => $"arena {eventId}: check failed: {message}";

    /// <summary>An end path of <paramref name="eventId"/> (natural, all waves defeated, stop, pillar off, fault, purge): its
    /// streak and health entry end with it.</summary>
    public void EventEnded(string eventId) => _failing.Remove(eventId);

    /// <summary>A restart (Core's initialization) or a purge: no streak, no entry, the next check on the next tick.</summary>
    public void Clear()
    {
        _failing.Clear();
        _phaseFailing = false;
        _next = default;
    }

    /// <summary>The arena events among <paramref name="active"/>; a pursuing run's arena is lifted (wave-variety D8).</summary>
    public static bool HasArena(ActiveEvent a) => a.Definition.Action is { Arena: not null } && !a.Pursuing;

    /// <summary>One scheduler tick: nothing unless an arena event runs and <see cref="Arena.CheckSeconds"/> have passed since
    /// the last check; then one check per arena event. Streaks of events no longer running are dropped first. Returns the
    /// checks that ran (for the tests); never throws.</summary>
    public IReadOnlyList<ArenaCheck> Tick(DateTime now, IReadOnlyCollection<ActiveEvent> active, IArenaWorld world, Func<double> random,
        Action<string> info, Action<string> error)
    {
        var checks = new List<ArenaCheck>();
        try
        {
            _failing.RemoveWhere(id => !active.Any(a => a.Id == id && HasArena(a)));
            var arenas = active.Where(HasArena).ToList();
            if (arenas.Count == 0 || now < _next) return checks;
            _next = now.AddSeconds(Arena.CheckSeconds);
            foreach (var a in arenas)
                if (Run(a, world, random, info, error) is { } c) checks.Add(c);
            _phaseFailing = false;
        }
        catch (Exception e)
        {
            // the phase never throws (D4); a fault outside Run is logged once per streak and held on the health line
            if (!_phaseFailing) Safe(error, FailedLine("phase", e.Message));
            _phaseFailing = true;
        }
        return checks;
    }

    /// <summary>One check of <paramref name="a"/>. Returns the returns that were written (null when the check could not
    /// run); never throws.</summary>
    public ArenaCheck? Run(ActiveEvent a, IArenaWorld world, Func<double> random, Action<string> info, Action<string> error)
    {
        string? fault = null;
        ArenaCheck? done = null;
        try
        {
            var units = new List<ArenaUnit>();
            foreach (var t in world.Units.Where(t => t.EventId == a.Id && t.SpawnedUtc >= a.Instance.StartedUtc).ToList())
            {
                try { if (world.Read(t) is { } u) units.Add(u); }
                catch (Exception e) { fault ??= e.Message; }
            }
            var height = Arena.ReturnHeight(a, units);
            if (Arena.Check(a, units, (_, _) => height, random) is { } check && Arena.Centre(a) is { } centre
                && a.Definition.Action is { Arena: { } arena } action)
            {
                var anchor = WavePlan.GroupAnchor(action.Location, a.Origin);
                var written = new List<ArenaReturn>();
                foreach (var r in check.Returns)
                {
                    try
                    {
                        world.Return(r, centre, arena.Radius, anchor);
                        written.Add(r);
                    }
                    catch (Exception e) { fault ??= e.Message; }
                }
                done = new ArenaCheck(a.Id, written, written.Count == 0 ? 0f : written.Max(r => r.Distance));
                if (done.LogLine is { } line) Safe(info, line);
            }
        }
        catch (Exception e) { fault ??= e.Message; }
        if (fault is null) _failing.Remove(a.Id);
        else if (_failing.Add(a.Id)) Safe(error, FailedLine(a.Id, fault));
        return done;
    }

    static void Safe(Action<string> log, string line)
    {
        try { log(line); }
        catch (Exception) { /* a log line never breaks the check */ }
    }
}
