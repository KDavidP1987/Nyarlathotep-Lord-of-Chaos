#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nyarlathotep.Logic;

// Hunting grounds (docs/dod/hunting-grounds.md): a Region location spreads a wave over its scope regions (D4), a region lock
// brings an event's own units back inside them (D5) and a region hunt sends each group after the nearest eligible player
// inside them, by hidden jumps beyond the game's reach (D7, D8). Everything here is pure: Services implement IRegionWorld
// over SpawnTracker's own entities, as swarm's IArenaWorld (Logic/Arena.cs).

/// <summary>The numbers of hunting grounds, each with its source (docs/dod/hunting-grounds.md › Assumptions).</summary>
public static class HuntingGrounds
{
    /// <summary>A Region wave's group holds at most this many units (S-12).</summary>
    public const int GroupUnits = 5;
    /// <summary>Two spots of one wave stand at least this far apart on x and z (S-12).</summary>
    public const float SpotSpacing = 30f;
    /// <summary>Candidate spots a group tries before it is not spawned (S-12).</summary>
    public const int SpotTries = 8;
    /// <summary>Bounding-box draws for one point inside a polygon; a draw that finds none is a used try.</summary>
    public const int PolygonDraws = 32;
    /// <summary>The slack past a unit's last inside spot before it counts as a stray, and the return's reach (S-8).</summary>
    public const float Slack = 10f;
    /// <summary>Candidate return spots around the last inside spot before the spot itself is used (D5).</summary>
    public const int ReturnTries = 8;
    /// <summary>The longest jump but the final one (S-10).</summary>
    public const float Step = 25f;
    /// <summary>A final jump lands this far from the target, inside Hunt's reach (S-10).</summary>
    public const float FinalMin = 50f;
    public const float FinalMax = 60f;
    /// <summary>Every landing stands at least this far from every online player (S-10).</summary>
    public const float LandingDistance = 50f;
    /// <summary>No unit jumps while an online player stands within this distance of it (design §9 D50).</summary>
    public const float WatchRange = 100f;
    /// <summary>Hunt's reach (Validation MaxHuntRange, SIEGES.md › S1): within it the game's own pathing and aggro take over.</summary>
    public const int Reach = EventValidator.MaxHuntRange;
    /// <summary>A RegionHunt unit's seed range (A4): the watch range, so every unit that may no longer jump (a player within
    /// 100 m) is seeded and the game's pathing walks the last metres from the 50-60 m final ring.</summary>
    public const int SeedReach = (int)WatchRange;
    /// <summary>Units that jump in one check across all events (S-11).</summary>
    public const int MaxJumps = 20;
    /// <summary>Walk checks the hunt spends in one check across all events (A3): about 80 ms at Session 2's 0.4 ms a check.
    /// A walk check takes two budget calls (<see cref="WalkCapCalls"/>).</summary>
    public const int WalkCap = 200;
    /// <summary><see cref="WalkCap"/> in <see cref="WalkBudget"/> calls: every walk check takes two.</summary>
    public const int WalkCapCalls = 2 * WalkCap;
    /// <summary>How far beside the unit it rejoins a stuck unit lands (A3).</summary>
    public const float RejoinOffset = 3f;
    /// <summary>How often the lock and the hunt run: the existing behaviour cadence (HuntPlan.IntervalSeconds).</summary>
    public const int CheckSeconds = HuntPlan.IntervalSeconds;

    /// <summary>The distance on x and z.</summary>
    public static float Distance(float ax, float az, float bx, float bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return (float)Math.Sqrt(dx * (double)dx + dz * (double)dz);
    }

    /// <summary>The point stands in one of <paramref name="scope"/>'s regions.</summary>
    public static bool Inside(Scope scope, Func<float, float, string> regionOf, float x, float z) => !scope.IsGlobal && scope.Names(regionOf(x, z));

    /// <summary>A running instance whose definition carries `regionLock: true` (D2, D5).</summary>
    public static bool Locked(ActiveEvent a) => a.Definition.Action is { RegionLock: true, Scope.IsGlobal: false };

    /// <summary>A running instance whose definition hunts by RegionHunt, which validation allows only under the lock (D3, D7).</summary>
    public static bool Hunts(ActiveEvent a) => Locked(a) && a.Definition.Action is { Behaviour.Type: BehaviourType.RegionHunt };

    /// <summary>The tracked unit belongs to the running instance <paramref name="a"/>: its event id, spawned at or after the
    /// instance's start (wave-sets A3). `.nyar spawn` units (no event) and an earlier instance's units never do.</summary>
    public static bool Ours(TrackedUnit t, ActiveEvent a) => t.EventId == a.Id && t.SpawnedUtc >= a.Instance.StartedUtc;
}

/// <summary>A spot of a Region wave (D4): x and z inside a scope region, the walk-check height level found there and its
/// height, which are the group's centre, walk height and regroup anchor.</summary>
public readonly record struct RegionSpot(float X, float Y, float Z, byte Level, string Region);

/// <summary>What one Region wave's spot draw reads (D4): the scope regions' polygons, the region index, the claimed-territory
/// test (null when allowTerritory or no map is needed), the wave's walk check, the height levels to scan at a spot, nearest
/// first, and each level's height.</summary>
public sealed record SpotWorld(
    IReadOnlyList<RegionPolygon> Polygons,
    Scope Scope,
    Func<float, float, string> RegionOf,
    Func<float, float, bool>? Claimed,
    WaveWalk Walk,
    IReadOnlyList<byte> Levels,
    Func<byte, float> HeightOf);

/// <summary>
/// The spot draw of a Region location (D4, Business rules 1, S-12). A spot is drawn uniformly by area over the scope
/// regions' polygons (a polygon picked by its area, then a uniform point inside it), and kept when it lies inside a scope
/// region by the index, outside claimed territory unless allowTerritory, at least <see cref="HuntingGrounds.SpotSpacing"/>
/// m from every spot already kept for the wave, and on a free, grounded walk-check tile at one of the scanned height
/// levels. Each group gets up to <see cref="HuntingGrounds.SpotTries"/> tries; a group without a spot is not spawned. The
/// walk check's calls come out of the tick's WalkBudget; once it is spent, or the check fails, no further spot is drawn.
/// No player position is read.
/// </summary>
public static class SpotDraw
{
    /// <summary>A polygon's area on x and z (the shoelace formula).</summary>
    public static double Area(RegionPolygon p)
    {
        double sum = 0;
        var v = p.Vertices;
        for (int i = 0, j = v.Count - 1; i < v.Count; j = i++) sum += (v[j].X + (double)v[i].X) * (v[j].Z - (double)v[i].Z);
        return Math.Abs(sum) / 2;
    }

    /// <summary>The even-odd test of RegionIndex on one polygon.</summary>
    public static bool Contains(RegionPolygon p, float x, float z)
    {
        var v = p.Vertices;
        var inside = false;
        for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
        {
            if ((v[i].Z > z) == (v[j].Z > z)) continue;
            var crossX = (v[j].X - v[i].X) * (z - v[i].Z) / (v[j].Z - v[i].Z) + v[i].X;
            if (x < crossX) inside = !inside;
        }
        return inside;
    }

    /// <summary>The scope regions' polygons with a positive area, in their order.</summary>
    public static List<RegionPolygon> InScope(IEnumerable<RegionPolygon> polygons, Scope scope) =>
        polygons.Where(p => scope.Names(p.Region) && p.Vertices.Count >= 3 && Area(p) > 0).ToList();

    /// <summary>One point drawn uniformly by area over <paramref name="polygons"/>: a polygon by its share of the total area,
    /// then a point of its box until one falls inside it; null when <see cref="HuntingGrounds.PolygonDraws"/> draws miss.</summary>
    public static (float X, float Z)? Point(IReadOnlyList<RegionPolygon> polygons, IReadOnlyList<double> areas, IRandom rng)
    {
        if (polygons.Count == 0) return null;
        var total = areas.Sum();
        if (!(total > 0)) return null;
        var pick = rng.NextDouble() * total;
        var k = 0;
        for (; k < polygons.Count - 1; k++)
        {
            if (pick < areas[k]) break;
            pick -= areas[k];
        }
        var p = polygons[k];
        for (var i = 0; i < HuntingGrounds.PolygonDraws; i++)
        {
            var x = (float)(p.MinX + rng.NextDouble() * (p.MaxX - p.MinX));
            var z = (float)(p.MinZ + rng.NextDouble() * (p.MaxZ - p.MinZ));
            if (Contains(p, x, z)) return (x, z);
        }
        return null;
    }

    /// <summary>Why a level scan stopped without a level.</summary>
    public enum ScanStop { None, Budget, Failed }

    /// <summary>The first of <paramref name="levels"/> at which (<paramref name="x"/>, <paramref name="z"/>) is grounded and
    /// free, two walk-check calls a level from the budget; null with the reason when the budget runs out or a call throws
    /// (the failure is recorded on the wave's walk), or with <see cref="ScanStop.None"/> when no level holds.</summary>
    public static byte? Level(float x, float z, WaveWalk walk, IReadOnlyList<byte> levels, out ScanStop stop)
    {
        stop = ScanStop.None;
        if (walk.Probe is not { } probe || walk.Failure is not null)
        {
            stop = ScanStop.Failed;
            return null;
        }
        foreach (var level in levels)
        {
            if (!walk.Budget.TryTake(2))
            {
                stop = ScanStop.Budget;
                return null;
            }
            try
            {
                if (probe.IsGrounded(x, z, level) && probe.IsFree(x, z, level)) return level;
            }
            catch (Exception e)
            {
                walk.Fail($"spot: {e.Message}");
                stop = ScanStop.Failed;
                return null;
            }
        }
        return null;
    }

    /// <summary>The spots of one Region wave, one entry per group by its index: the group's spot, or null when it found
    /// none within its tries (or the walk check stopped), so a failed group is itself the one skipped (D4; Codex step 4
    /// F2).</summary>
    public static List<RegionSpot?> Draw(int groups, SpotWorld w, IRandom rng)
    {
        var spots = Enumerable.Repeat<RegionSpot?>(null, groups).ToList();
        var polygons = InScope(w.Polygons, w.Scope);
        var areas = polygons.Select(Area).ToList();
        for (var g = 0; g < groups; g++)
        {
            for (var t = 0; t < HuntingGrounds.SpotTries; t++)
            {
                if (Point(polygons, areas, rng) is not { } at) continue;
                var region = w.RegionOf(at.X, at.Z);
                if (!w.Scope.Names(region)) continue;
                if (w.Claimed is { } claimed && claimed(at.X, at.Z)) continue;
                if (spots.Any(s => s is { } o && HuntingGrounds.Distance(o.X, o.Z, at.X, at.Z) < HuntingGrounds.SpotSpacing)) continue;
                var level = Level(at.X, at.Z, w.Walk, w.Levels, out var stop);
                if (stop != ScanStop.None) return spots;                       // no game calls left this tick, or a failed check
                if (level is not { } l) continue;
                spots[g] = new RegionSpot(at.X, w.HeightOf(l), at.Z, l, region);
                break;
            }
        }
        return spots;
    }
}

/// <summary>
/// A Region location's wave decision (D4, Business rules 1): WaveGate's rules in their order (an ended event, territory
/// unknown, the chance roll), the whole wave clamped once by MaxUnitsPerWave and the free MaxTrackedUnits slots, then the
/// allowed units split into ⌈n/5⌉ groups of at most <see cref="HuntingGrounds.GroupUnits"/>, dealt one at a time in roll
/// order, each group placed at its own spot. A group without a spot is not spawned and its units are not queued; a wave
/// without any spot is skipped "no walkable spot in the regions". No player is picked.
/// </summary>
public static class RegionWave
{
    /// <summary>The clamped wave's units split into groups of at most 5, dealt round robin so each group mixes the wave's
    /// entries, each group's picks grouped back into entries (their modifiers kept, wave-sets D3).</summary>
    public static List<List<UnitEntry>> Split(IReadOnlyList<UnitEntry> units) =>
        Deal(units, 0).Select(g => WaveRoll.GroupPicks(g.Select(p => p.Pick).ToList())).ToList();

    /// <summary>The round-robin deal of <see cref="Split"/>, each pick marked when it is one of the first
    /// <paramref name="lead"/> (a final wave's bosses, revenge D29).</summary>
    static List<List<(UnitEntry Pick, bool Lead)>> Deal(IReadOnlyList<UnitEntry> units, int lead)
    {
        var picks = units.SelectMany(u => Enumerable.Repeat(new UnitEntry(u.Prefab, 1, 1.0, u.Modifiers), u.Count)).ToList();
        var count = (picks.Count + HuntingGrounds.GroupUnits - 1) / HuntingGrounds.GroupUnits;
        var groups = Enumerable.Range(0, count).Select(_ => new List<(UnitEntry, bool)>()).ToList();
        for (var i = 0; i < picks.Count; i++) groups[i % count].Add((picks[i], i < lead));
        return groups;
    }

    /// <summary>revenge D29 (Review 4 F4): a lead pick (a boss) dealt to a group without a spot trades places with a
    /// regular pick of the first group that has a spot, so a boss is lost only when every spotted group holds bosses
    /// alone.</summary>
    static void KeepLead(List<List<(UnitEntry Pick, bool Lead)>> groups, IReadOnlyList<RegionSpot?> spots)
    {
        bool Spotted(int g) => g < spots.Count && spots[g] is not null;
        for (var g = 0; g < groups.Count; g++)
        {
            if (Spotted(g)) continue;
            for (var i = 0; i < groups[g].Count; i++)
            {
                if (!groups[g][i].Lead) continue;
                for (var h = 0; h < groups.Count; h++)
                {
                    if (!Spotted(h)) continue;
                    var j = groups[h].FindLastIndex(p => !p.Lead);
                    if (j < 0) continue;
                    (groups[g][i], groups[h][j]) = (groups[h][j], groups[g][i]);
                    break;
                }
            }
        }
        foreach (var group in groups)
        {
            var ordered = group.Where(p => p.Lead).Concat(group.Where(p => !p.Lead)).ToList();   // a group's bosses first, order kept
            group.Clear();
            group.AddRange(ordered);
        }
    }

    /// <summary>The decision of one Region wave. <paramref name="draw"/> gets the group count and returns one entry per
    /// group by its index, null for a group without a spot; it is called only for a wave that spawns. Each group spawns at
    /// its own spot, never another group's (Codex step 4 F2).</summary>
    /// <remarks><paramref name="lead"/>: the number of units at the head of the roll that must not be lost to a missing
    /// spot (a final wave's bosses, revenge D29; <see cref="KeepLead"/>).</remarks>
    public static FanOutDecision Decide(WaveFacts facts, Func<IReadOnlyList<UnitEntry>> roll, int maxPerWave, int occupied, int maxTracked,
        Func<int, IReadOnlyList<RegionSpot?>> draw, int lead = 0)
    {
        var one = WaveGate.DecideEntries(facts with { Pick = null, CentreClaimed = false }, roll, maxPerWave, occupied, maxTracked);
        if (one.Outcome != WaveOutcome.Spawn) return new FanOutDecision(one.Outcome, one.Line, [], one.CapLines);
        var dealt = Deal(one.Units, lead);
        var spots = draw(dealt.Count);
        if (lead > 0) KeepLead(dealt, spots);
        var groups = dealt.Select(g => WaveRoll.GroupPicks(g.Select(p => p.Pick).ToList())).ToList();
        var result = new List<WaveGroup>();
        for (var i = 0; i < groups.Count && i < spots.Count; i++)
            if (spots[i] is { } s) result.Add(new WaveGroup(i, (s.X, s.Y, s.Z), groups[i]));
        if (result.Count == 0) return new FanOutDecision(WaveOutcome.Skip, WaveLines.NoRegionSpot(facts.Wave, facts.EventId), [], one.CapLines);
        return new FanOutDecision(WaveOutcome.Spawn, null, result, one.CapLines);
    }

    /// <summary>"in &lt;g&gt; groups over &lt;regions&gt;" of a Region wave's spawn line (D4).</summary>
    public static string Where(int groups, Scope scope) => WaveLines.OverRegions(groups, string.Join(", ", scope.Regions));

    /// <summary>The verbose line of one Region group (D21): our units' spot, its height level and region, never a
    /// player's position (the service adds "[nyar] ").</summary>
    public static string SpotLine(string id, int wave, int group, RegionSpot spot) =>
        string.Create(CultureInfo.InvariantCulture, $"event {id} wave {wave} group {group}: spot {spot.X:0.#} {spot.Z:0.#} h {spot.Level} in {spot.Region}");
}

/// <summary>A live tracked unit as the lock and the hunt read it: its ledger key, event, spawn time, position, the game's
/// height level (0 until snapped), its wave and group within the instance (the hunt's group, S-14), and where it spawned.</summary>
public readonly record struct RegionUnit(long Key, string? EventId, DateTime SpawnedUtc, float X, float Y, float Z, byte Level,
    int Wave = 0, int Group = 0, (float X, float Y, float Z)? Spawn = null);

/// <summary>A unit's last spot inside a scope region (D5): recorded at each lock check, its spawn point at first.</summary>
public readonly record struct InsideSpot(float X, float Y, float Z, byte Level);

/// <summary>Where one stray goes (D5): a walkable spot inside a scope region within 10 m of its last inside spot (the spot
/// itself when none is found), and how far from that spot it stood.</summary>
public readonly record struct RegionReturn(long Key, float X, float Y, float Z, float Distance);

/// <summary>One lock check of one running instance (D5): the returns written and the farthest; no line without a return.</summary>
public sealed record LockCheck(string EventId, IReadOnlyList<RegionReturn> Returns, float Farthest)
{
    public string? LogLine => Returns.Count == 0 ? null : LockRule.LogLine(EventId, Returns.Count, Farthest);
}

/// <summary>One jump of one unit (D8): the landing, its height and level, and its distance from the nearest online player.</summary>
public readonly record struct RegionJump(long Key, float X, float Y, float Z, byte Level, float Clearance);

/// <summary>One hunt check of one running instance (D7, D8): the jumps written, the units waiting and in reach; a line only
/// when a unit jumped.</summary>
public sealed record HuntCheck(string EventId, IReadOnlyList<RegionJump> Jumps, int Waiting, int InReach)
{
    public string? LogLine => Jumps.Count == 0 ? null : JumpRule.LogLine(EventId, Jumps.Count, Waiting, InReach, Jumps.Min(j => j.Clearance));
}

/// <summary>
/// The game side of the lock, the hunt and the pursuit check (D5-D9; wave-variety D8), implemented by Services over
/// SpawnTracker's ledger and its own entities. The players, the claimed check, the focus and the live centres are
/// IPursuitWorld's: the players are read once per region tick, memory only, and their territory flag is the phase's to
/// set per event by that event's own map, never one event's map for another (Codex step 4 F1). Every member may throw,
/// which the phases take as a failure of that read or write only.
/// </summary>
public interface IRegionWorld : IPursuitWorld
{
    /// <summary>The ledger's tracked units (key, event, spawn time, wave).</summary>
    IEnumerable<TrackedUnit> Units { get; }

    /// <summary>One unit's position, level, group and spawn point; null when it is gone since the ledger listed it.</summary>
    RegionUnit? Read(TrackedUnit unit);

    /// <summary>The region at a point (RegionIndex.RegionOf).</summary>
    string RegionOf(float x, float z);

    /// <summary>The walk check at a point and level: free and grounded (two game calls).</summary>
    bool Walkable(float x, float z, byte level);

    /// <summary>The height a unit stands at on a walk-check level.</summary>
    float HeightOf(byte level);

    /// <summary>The tick's walk-check budget (WalkBudget.PerTick).</summary>
    WalkBudget Budget { get; }

    /// <summary>Moves one of our units to <paramref name="r"/>'s spot, its PreCombatPosition with it, and drops its
    /// AggroBuffer entries (with their seed records) on players standing in no region of <paramref name="scope"/>
    /// (<see cref="LockRule.DropsEntry"/>). A unit no longer there is left alone.</summary>
    void Return(RegionReturn r, Scope scope);

    /// <summary>Moves one of our units to <paramref name="j"/>'s landing, its PreCombatPosition with it.</summary>
    void Jump(RegionJump j);
}

/// <summary>
/// The lock rule (D5, Business rules 2, S-8, S-9): a unit of the running instance standing in no scope region and more than
/// <see cref="HuntingGrounds.Slack"/> m on x and z from its last inside spot is a stray, returned to a walkable spot inside a
/// scope region within 10 m of that spot (the spot itself when none is found), dropping its entries on players outside the
/// regions. While the lock holds no Hunt or RegionHunt seed is written on a player outside the regions.
/// </summary>
public static class LockRule
{
    /// <summary>A unit at (<paramref name="x"/>, <paramref name="z"/>), <paramref name="inside"/> or not, strays from
    /// <paramref name="last"/>: outside and more than the slack away. Inside, or out by 10 m or less, it is left alone.</summary>
    public static bool Strays(bool inside, float x, float z, InsideSpot last) =>
        !inside && HuntingGrounds.Distance(x, z, last.X, last.Z) > HuntingGrounds.Slack;

    /// <summary>The return spot near <paramref name="last"/>: up to <see cref="HuntingGrounds.ReturnTries"/> points within
    /// 10 m (uniform over the disc), the first inside a scope region and walkable at the spot's level while the budget
    /// lasts; else the last inside spot itself.</summary>
    public static (float X, float Y, float Z) ReturnSpot(InsideSpot last, Func<float, float, bool> inside, Func<float, float, byte, bool> walkable,
        WalkBudget budget, Func<double> random)
    {
        for (var i = 0; i < HuntingGrounds.ReturnTries; i++)
        {
            var angle = random() * 2 * Math.PI;
            var r = HuntingGrounds.Slack * Math.Sqrt(Math.Clamp(random(), 0, 1));
            var x = (float)(last.X + r * Math.Cos(angle));
            var z = (float)(last.Z + r * Math.Sin(angle));
            if (!inside(x, z)) continue;
            if (!budget.TryTake(2)) break;
            if (walkable(x, z, last.Level)) return (x, last.Y, z);
        }
        return (last.X, last.Y, last.Z);
    }

    /// <summary>Whether a returned unit's AggroBuffer entry is dropped (D5): its entity stands in no scope region, or its
    /// position cannot be read. An entry on a player inside the regions stays.</summary>
    public static bool DropsEntry(Scope scope, Func<float, float, string> regionOf, (float X, float Z)? at) =>
        at is not { } p || !HuntingGrounds.Inside(scope, regionOf, p.X, p.Z);

    /// <summary>The players a Hunt or RegionHunt seed may go to (D5, S-9): every player for an event without the lock, only
    /// the players inside a scope region for a locked one (none when the region lookup is unavailable).</summary>
    public static IEnumerable<HuntCandidate> SeedPool(SpawnWavesAction action, IEnumerable<HuntCandidate> players, Func<float, float, string>? regionOf) =>
        !action.RegionLock ? players
        : regionOf is null ? []
        : players.Where(p => HuntingGrounds.Inside(action.Scope, regionOf, p.X, p.Z));

    /// <summary>"region lock &lt;id&gt;: returned &lt;n&gt;, farthest &lt;d&gt; m out" (the service adds "[nyar] "): counts and a
    /// distance only, never a name or a position.</summary>
    public static string LogLine(string eventId, int returned, float farthest) =>
        string.Create(CultureInfo.InvariantCulture, $"region lock {eventId}: returned {returned}, farthest {farthest:0} m out");

    public static string FailedLine(string eventId, string message) => $"region lock {eventId}: check failed: {message}";
}

/// <summary>
/// The hunt target rule (D7, Business rules 3, S-14, S-16): each group of the running instance (one per Point or Admin
/// wave, one per AroundPlayer group, one per Region spot) takes the eligible player nearest to its live units' mean x and z
/// among the players inside a scope region, ties by key; eligible is Hunt's rule. A unit is seeded on the eligible players
/// inside the regions within 60 m of it, nearest first, at most HuntPlan.MaxTargets.
/// </summary>
public static class HuntRule
{
    /// <summary>Hunt's rule (online, alive, usable position, not in claimed territory, not in PvP combat) and inside a scope
    /// region.</summary>
    public static bool Eligible(HuntCandidate p, Scope scope, Func<float, float, string> regionOf) =>
        p.Online && p.Alive && !p.InTerritory && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Z)
        && HuntingGrounds.Inside(scope, regionOf, p.X, p.Z);

    public static List<HuntCandidate> EligibleInside(IEnumerable<HuntCandidate> players, Scope scope, Func<float, float, string> regionOf) =>
        players.Where(p => Eligible(p, scope, regionOf)).ToList();

    /// <summary>The mean x and z of a group's live units.</summary>
    public static (float X, float Z) Mean(IReadOnlyCollection<RegionUnit> units) =>
        ((float)units.Average(u => (double)u.X), (float)units.Average(u => (double)u.Z));

    /// <summary>The target of a group at <paramref name="mean"/>: the nearest of <paramref name="eligible"/>, ties by key;
    /// null when there is none.</summary>
    public static HuntCandidate? Target(IReadOnlyList<HuntCandidate> eligible, (float X, float Z) mean) =>
        eligible.Count == 0 ? null
        : eligible.OrderBy(p => HuntingGrounds.Distance(p.X, p.Z, mean.X, mean.Z)).ThenBy(p => p.Key).First();

    /// <summary>The players to seed for a unit at <paramref name="at"/>: the eligible players inside the regions within 100 m
    /// (<see cref="HuntingGrounds.SeedReach"/>, A4), nearest first (ties by key), at most HuntPlan.MaxTargets (D7).</summary>
    public static List<long> SeedTargets(IEnumerable<HuntCandidate> players, Scope scope, Func<float, float, string> regionOf, (float X, float Z) at) =>
        HuntPlan.Targets(EligibleInside(players, scope, regionOf), at, HuntingGrounds.SeedReach);

    /// <summary>A seeded RegionHunt unit's new return point (A5): the position of its nearest seeded player (the first of
    /// <paramref name="targets"/>, which <see cref="SeedTargets"/> orders nearest first), so the player stands inside the
    /// unit's leash (a forest spider's MaxDistanceFromPreCombatPosition is 45 m, the final ring 50-60 m); that player is
    /// eligible, so the point lies inside the regions and outside claimed territory. Null with no seeded player: the
    /// unit keeps its return point.</summary>
    public static (float X, float Z)? Home(IReadOnlyList<long> targets, IEnumerable<HuntCandidate> players)
    {
        if (targets.Count == 0) return null;
        foreach (var p in players) if (p.Key == targets[0]) return (p.X, p.Z);
        return null;
    }

    /// <summary>The instance's groups: its units by wave and group.</summary>
    public static IEnumerable<IGrouping<(int Wave, int Group), RegionUnit>> Groups(IEnumerable<RegionUnit> units) =>
        units.GroupBy(u => (u.Wave, u.Group)).OrderBy(g => g.Key.Wave).ThenBy(g => g.Key.Group);
}

/// <summary>What one unit does at a hunt check (D8).</summary>
public enum JumpKind { InReach, Waiting, Jump }

/// <summary>Why the units of one hunt check waited (a diagnostic, counts only): a player within 100 m, and over the
/// candidate landings tried, those too near a player, outside the regions or claimed, not walkable, or a spent budget.</summary>
public sealed class WaitTally
{
    public int Watched, NearPlayer, NotAllowed, NotWalkable, Budget, Candidates;

    /// <summary>The first few waiting units' walk-check level, height and spot (our own units, as D21's spot line; never a
    /// player).</summary>
    public readonly List<string> Units = new();

    public void Waited(RegionUnit u)
    {
        if (Units.Count < 3) Units.Add(string.Create(CultureInfo.InvariantCulture, $"h {u.Level} y {u.Y:0.#} at {u.X:0} {u.Z:0}"));
    }

    /// <summary>"region hunt &lt;id&gt;: waiting &lt;n&gt;: …" (the service adds "[nyar] "), counts only.</summary>
    public string Line(string eventId, int waiting) =>
        $"region hunt {eventId}: waiting {waiting}: {Watched} watched; of {Candidates} landings tried {NearPlayer} near a player, {NotAllowed} outside the regions or claimed, {NotWalkable} not walkable, {Budget} out of budget{(Units.Count > 0 ? "; units " + string.Join(", ", Units) : "")}";
}

/// <summary>
/// The jump rule (D8, Business rules 4, S-10, S-11, S-17). A unit whose target stands more than 60 m away jumps toward it
/// only while no online player, eligible or not, stands within 100 m of it: at most 25 m along the line (or a little to its
/// side), and a jump that would end within 100 m of the target lands instead 50-60 m from the target. A landing lies inside
/// a scope region, outside claimed territory, at least 50 m from every online player, and is free and grounded at the
/// unit's height level or a neighbouring one, else (A1) two or three levels from it on its first five open landings,
/// else (A2) a crossing of 35-55 m over ground no step can cross, or the unit waits. Between 60 and 100 m nothing moves it (the target itself is an online player within 100 m).
/// </summary>
public static class JumpRule
{
    static readonly double[] StepTurns = [0, 15, -15, 30, -30];
    static readonly float[] StepLengths = [HuntingGrounds.Step, 20f, 15f];
    static readonly double[] FinalTurns = [0, 10, -10, 20, -20];
    static readonly float[] FinalDistances = [55f, 50f, 60f, 52.5f, 57.5f];

    /// <summary>The nearest online player with a usable position, or null.</summary>
    public static float? Nearest(IEnumerable<HuntCandidate> online, float x, float z)
    {
        float? best = null;
        foreach (var p in online)
        {
            if (!p.Online || !PlayerPosition.Usable(p.X, p.Z)) continue;
            var d = HuntingGrounds.Distance(p.X, p.Z, x, z);
            if (best is null || d < best) best = d;
        }
        return best;
    }

    /// <summary>The candidate landings of a unit at <paramref name="u"/> hunting <paramref name="target"/>, in the order
    /// they are tried: the final ring 50-60 m from the target when a straight step would end within 100 m of it, else steps
    /// of at most 25 m toward it.</summary>
    public static IEnumerable<(float X, float Z, bool Final)> Candidates((float X, float Z) u, (float X, float Z) target)
    {
        var d = HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z);
        if (d <= 0) yield break;
        var ux = (target.X - u.X) / d;
        var uz = (target.Z - u.Z) / d;
        if (d - HuntingGrounds.Step <= HuntingGrounds.WatchRange)
        {
            foreach (var dist in FinalDistances)
                foreach (var turn in FinalTurns)
                {
                    var (rx, rz) = Turn(-ux, -uz, turn);                      // from the target back toward the unit
                    yield return (target.X + rx * dist, target.Z + rz * dist, true);
                }
            yield break;
        }
        foreach (var len in StepLengths)
            foreach (var turn in StepTurns)
            {
                var (rx, rz) = Turn(ux, uz, turn);
                yield return (u.X + rx * len, u.Z + rz * len, false);
            }
    }

    static (float X, float Z) Turn(float x, float z, double degrees)
    {
        var a = degrees * Math.PI / 180;
        var (c, s) = (Math.Cos(a), Math.Sin(a));
        return ((float)(x * c - z * s), (float)(x * s + z * c));
    }

    /// <summary>One unit's plan at a check: in reach (60 m or less from its target), waiting, or a jump to a landing that
    /// passes every test. <paramref name="online"/> is every connected character read; <paramref name="allowed"/> says a
    /// point lies inside a scope region and outside claimed territory; the walk check takes two calls a level from
    /// <paramref name="budget"/> (spent: the unit waits).</summary>
    public static (JumpKind Kind, RegionJump? Jump) Plan(RegionUnit u, (float X, float Z) target, IReadOnlyList<HuntCandidate> online,
        Func<float, float, bool> allowed, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf, WalkBudget budget,
        WaitTally? tally = null)
    {
        if (HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z) <= HuntingGrounds.Reach) return (JumpKind.InReach, null);
        if (Nearest(online, u.X, u.Z) is { } near && near <= HuntingGrounds.WatchRange)
        {
            if (tally is not null) tally.Watched++;
            return (JumpKind.Waiting, null);
        }
        var open = new List<(float X, float Z, float Clearance)>();
        foreach (var (x, z, _) in Candidates((u.X, u.Z), target))
        {
            if (tally is not null) tally.Candidates++;
            var clearance = Nearest(online, x, z) ?? float.MaxValue;
            if (clearance < HuntingGrounds.LandingDistance) { if (tally is not null) tally.NearPlayer++; continue; }
            if (!allowed(x, z)) { if (tally is not null) tally.NotAllowed++; continue; }
            open.Add((x, z, clearance));
            foreach (var level in WalkLevels.Spot(u.Level))
            {
                if (!budget.TryTake(2)) { if (tally is not null) tally.Budget++; return (JumpKind.Waiting, null); }
                if (walkable(x, z, level)) return (JumpKind.Jump, new RegionJump(u.Key, x, heightOf(level), z, level, clearance));
            }
            if (tally is not null) tally.NotWalkable++;
        }
        // A1: a unit on a hillside (Session 2: half of hg-timing's units waited at h 13 over ground two or three levels
        // down) tries its first WideCandidates open landings again at the wide levels, within three of its own.
        foreach (var (x, z, clearance) in open.Take(WideCandidates))
            foreach (var level in WalkLevels.Wide(u.Level))
            {
                if (!budget.TryTake(2)) { if (tally is not null) tally.Budget++; return (JumpKind.Waiting, null); }
                if (walkable(x, z, level)) return (JumpKind.Jump, new RegionJump(u.Key, x, heightOf(level), z, level, clearance));
            }
        // A2: ground no step can cross (Session 2: a ravine 35-45 m wide east of the pond, its walls never grounded) is
        // crossed by a longer jump along the line, never one ending within 100 m of the target (the final ring's job).
        foreach (var (x, z) in Crossings((u.X, u.Z), target))
        {
            var clearance = Nearest(online, x, z) ?? float.MaxValue;
            if (clearance < HuntingGrounds.LandingDistance || !allowed(x, z)) continue;
            foreach (var level in WalkLevels.Spot(u.Level))
            {
                if (!budget.TryTake(2)) { if (tally is not null) tally.Budget++; return (JumpKind.Waiting, null); }
                if (walkable(x, z, level)) return (JumpKind.Jump, new RegionJump(u.Key, x, heightOf(level), z, level, clearance));
            }
        }
        return (JumpKind.Waiting, null);
    }

    /// <summary>How many open landings the wide pass tries again (A1): at most 5 × 4 levels × 2 calls a unit.</summary>
    public const int WideCandidates = 5;

    static readonly (float X, float Z)[] RejoinOffsets =
        [(HuntingGrounds.RejoinOffset, 0), (-HuntingGrounds.RejoinOffset, 0), (0, HuntingGrounds.RejoinOffset), (0, -HuntingGrounds.RejoinOffset)];

    /// <summary>A3: the rejoin of a unit that found no landing at an earlier check, its first try at the next one:
    /// a landing 3 m beside the anchor nearest <paramref name="target"/> among <paramref name="anchors"/> (units of the same
    /// instance that found a landing or are in reach at this check) that stands nearer the target than the unit, by the
    /// landing rules (no online player within 100 m of the unit, 50 m or more from every online player, allowed, free and
    /// grounded at the anchor's level or a neighbouring one); null when none passes.</summary>
    public static RegionJump? Rejoin(RegionUnit u, (float X, float Z) target, IReadOnlyList<(float X, float Z, byte Level)> anchors,
        IReadOnlyList<HuntCandidate> online, Func<float, float, bool> allowed, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf,
        WalkBudget budget)
    {
        if (Nearest(online, u.X, u.Z) is { } near && near <= HuntingGrounds.WatchRange) return null;
        var own = HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z);
        var nearer = anchors.Where(a => HuntingGrounds.Distance(a.X, a.Z, target.X, target.Z) < own)
            .OrderBy(a => HuntingGrounds.Distance(a.X, a.Z, target.X, target.Z)).ToList();
        if (nearer.Count == 0) return null;
        var anchor = nearer[0];
        foreach (var (dx, dz) in RejoinOffsets)
        {
            var (x, z) = (anchor.X + dx, anchor.Z + dz);
            if (HuntingGrounds.Distance(x, z, target.X, target.Z) >= own) continue;   // the landing too is nearer the target (re-review F3)
            var clearance = Nearest(online, x, z) ?? float.MaxValue;
            if (clearance < HuntingGrounds.LandingDistance || !allowed(x, z)) continue;
            foreach (var level in WalkLevels.Spot(anchor.Level))
            {
                if (!budget.TryTake(2)) return null;
                if (walkable(x, z, level)) return new RegionJump(u.Key, x, heightOf(level), z, level, clearance);
            }
        }
        return null;
    }

    static readonly float[] CrossLengths = [35f, 45f, 55f];
    static readonly double[] CrossTurns = [0, 15, -15];

    /// <summary>The crossing landings of a unit at <paramref name="u"/> hunting <paramref name="target"/> (A2), tried only
    /// after every landing of 25 m or less failed: 35, 45 and 55 m along the line and 15° to either side, each only when
    /// it ends more than 100 m from the target; none while the final ring applies.</summary>
    public static IEnumerable<(float X, float Z)> Crossings((float X, float Z) u, (float X, float Z) target)
    {
        var d = HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z);
        if (d - HuntingGrounds.Step <= HuntingGrounds.WatchRange) yield break;
        var ux = (target.X - u.X) / d;
        var uz = (target.Z - u.Z) / d;
        foreach (var len in CrossLengths)
            foreach (var turn in CrossTurns)
            {
                var (rx, rz) = Turn(ux, uz, turn);
                var (x, z) = (u.X + rx * len, u.Z + rz * len);
                if (HuntingGrounds.Distance(x, z, target.X, target.Z) > HuntingGrounds.WatchRange) yield return (x, z);
            }
    }

    /// <summary>"region hunt &lt;id&gt;: &lt;j&gt; jumped, &lt;w&gt; waiting, &lt;r&gt; in reach, landed &lt;d&gt; m" (the service
    /// adds "[nyar] "); d is the smallest distance between a landing and an online player (Review 1 F4). Counts and one
    /// distance only, never a name or a position.</summary>
    public static string LogLine(string eventId, int jumped, int waiting, int inReach, float landed) =>
        string.Create(CultureInfo.InvariantCulture, $"region hunt {eventId}: {jumped} jumped, {waiting} waiting, {inReach} in reach, landed {Math.Min(landed, 9999f):0} m");

    public static string FailedLine(string eventId, string message) => $"region hunt {eventId}: move failed: {message}";
}

/// <summary>
/// The lock phase (D5, D6): every <see cref="HuntingGrounds.CheckSeconds"/> s while a locked instance runs, each such
/// instance's own live units are read, their last inside spots recorded and the strays returned. Guarded per unit and per
/// event: a throwing region lookup, read or return skips that unit and the rest runs; a check that met any throw logs
/// "region lock &lt;id&gt;: check failed: &lt;message&gt;" once per streak and holds <see cref="Failing"/> on the health line
/// until a check of that event runs clean, the event ends or the server restarts. Never throws.
/// </summary>
public sealed class LockPhase
{
    public const string Failing = "region lock: check failing";

    readonly Dictionary<string, Dictionary<long, InsideSpot>> _inside = new(StringComparer.Ordinal);
    readonly SortedSet<string> _failing = new(StringComparer.Ordinal);
    bool _phaseFailing;
    DateTime _next;

    public IReadOnlyList<string> Health => _failing.Count > 0 || _phaseFailing ? [Failing] : [];

    /// <summary>The last inside spots held for <paramref name="eventId"/> (memory only).</summary>
    public int Records(string eventId) => _inside.TryGetValue(eventId, out var r) ? r.Count : 0;

    /// <summary>Every record held, for the end-path checks.</summary>
    public int AllRecords => _inside.Values.Sum(r => r.Count) + _failing.Count;

    public void EventEnded(string eventId)
    {
        _inside.Remove(eventId);
        _failing.Remove(eventId);
    }

    public void Clear()
    {
        _inside.Clear();
        _failing.Clear();
        _phaseFailing = false;
        _next = default;
    }

    /// <summary>One scheduler tick: records and streaks of instances no longer locked and running are dropped first; then,
    /// when <see cref="HuntingGrounds.CheckSeconds"/> have passed, one check per locked instance.</summary>
    public IReadOnlyList<LockCheck> Tick(DateTime now, IReadOnlyCollection<ActiveEvent> active, IRegionWorld world, Func<double> random,
        Action<string> info, Action<string> error, bool due = true)
    {
        var checks = new List<LockCheck>();
        try
        {
            var locked = active.Where(HuntingGrounds.Locked).ToList();
            foreach (var id in _inside.Keys.Where(id => !locked.Any(a => a.Id == id)).ToList()) _inside.Remove(id);
            _failing.RemoveWhere(id => !locked.Any(a => a.Id == id));
            if (locked.Count == 0 || !due) return checks;
            foreach (var a in locked)
                if (Run(a, world, random, info, error) is { } c) checks.Add(c);
            _phaseFailing = false;
        }
        catch (Exception e)
        {
            if (!_phaseFailing) Phases.Safe(error, LockRule.FailedLine("phase", e.Message));
            _phaseFailing = true;
        }
        return checks;
    }

    /// <summary>Whether a check is due at <paramref name="now"/> (and the next one scheduled).</summary>
    internal bool Due(DateTime now)
    {
        if (now < _next) return false;
        _next = now.AddSeconds(HuntingGrounds.CheckSeconds);
        return true;
    }

    /// <summary>One check of <paramref name="a"/>; the returns written, or null when it could not run. Never throws.</summary>
    public LockCheck? Run(ActiveEvent a, IRegionWorld world, Func<double> random, Action<string> info, Action<string> error)
    {
        string? fault = null;
        LockCheck? done = null;
        try
        {
            if (a.Definition.Action is not { RegionLock: true } action) return null;
            var scope = action.Scope;
            // A6: claimed castle ground is never a return spot, unless the admin allowed territory (no map is then built)
            Func<float, float, bool> castle = action.AllowTerritory ? (_, _) => false : (x, z) => world.IsClaimed(a.Id, x, z);
            if (!_inside.TryGetValue(a.Id, out var records)) _inside[a.Id] = records = new Dictionary<long, InsideSpot>();
            var seen = new HashSet<long>();
            var written = new List<RegionReturn>();
            foreach (var t in world.Units.Where(t => HuntingGrounds.Ours(t, a)).ToList())
            {
                try
                {
                    if (world.Read(t) is not { } u) continue;
                    seen.Add(u.Key);
                    var inside = HuntingGrounds.Inside(scope, world.RegionOf, u.X, u.Z);
                    if (inside)
                    {
                        // never a spot in a claimed castle: a return is put down there unseen (A6, the owner 2026-10-13)
                        if (!castle(u.X, u.Z)) records[u.Key] = new InsideSpot(u.X, u.Y, u.Z, u.Level);
                        continue;
                    }
                    if (!records.TryGetValue(u.Key, out var last))
                    {
                        if (u.Spawn is not { } s) continue;                     // nothing known inside yet: left alone
                        records[u.Key] = last = new InsideSpot(s.X, s.Y, s.Z, u.Level);
                    }
                    if (!LockRule.Strays(false, u.X, u.Z, last)) continue;
                    var d = HuntingGrounds.Distance(u.X, u.Z, last.X, last.Z);
                    var (x, y, z) = LockRule.ReturnSpot(last,
                        (px, pz) => HuntingGrounds.Inside(scope, world.RegionOf, px, pz) && !castle(px, pz), world.Walkable,
                        world.Budget, random);
                    if (castle(x, z))
                    {
                        // the last spot itself was claimed since it was recorded (a heart placed mid-event; review 2 F5): the
                        // spawn point instead, or no return at all while that is claimed too
                        if (u.Spawn is not { } home || castle(home.X, home.Z)) continue;
                        (x, y, z) = home;
                    }
                    var r = new RegionReturn(u.Key, x, y, z, d);
                    world.Return(r, scope);
                    written.Add(r);
                }
                catch (Exception e) { fault ??= e.Message; }
            }
            foreach (var gone in records.Keys.Where(k => !seen.Contains(k)).ToList()) records.Remove(gone);   // died or despawned
            done = new LockCheck(a.Id, written, written.Count == 0 ? 0f : written.Max(r => r.Distance));
            if (done.LogLine is { } line) Phases.Safe(info, line);
        }
        catch (Exception e) { fault ??= e.Message; }
        if (fault is null) _failing.Remove(a.Id);
        else if (_failing.Add(a.Id)) Phases.Safe(error, LockRule.FailedLine(a.Id, fault));
        return done;
    }
}

/// <summary>
/// The hunt phase (D7, D8, D9): every <see cref="HuntingGrounds.CheckSeconds"/> s while a RegionHunt instance runs, the
/// players are read once for each such instance (a throwing read skips that event's whole check: nothing moves), each
/// group's target is chosen again, and its units in reach, waiting or jumping are counted; at most
/// <see cref="HuntingGrounds.MaxJumps"/> units jump per tick across all events. A throwing unit read, landing check or jump
/// write skips that unit. A check that met any throw logs "region hunt &lt;id&gt;: move failed: &lt;message&gt;" once per
/// streak and holds <see cref="Failing"/> until a check of that event runs clean, the event ends or the server restarts.
/// The targets live in memory for one check. Never throws.
/// </summary>
public sealed class HuntPhase
{
    public const string Failing = "region hunt: move failing";

    readonly Dictionary<string, Dictionary<(int Wave, int Group), long>> _targets = new(StringComparer.Ordinal);
    readonly SortedSet<string> _failing = new(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<long, int>> _waited = new(StringComparer.Ordinal);   // checks without a landing (A1, A3)
    int _turn;                                                                  // which event plans first this check (A3)
    bool _phaseFailing;

    public IReadOnlyList<string> Health => _failing.Count > 0 || _phaseFailing ? [Failing] : [];

    /// <summary>Where the "waiting" diagnostic goes (Debug.VerboseLogging), or null for none.</summary>
    public Action<string>? Detail { get; set; }

    /// <summary>The target key of each group of <paramref name="eventId"/> at its last check (memory only).</summary>
    public IReadOnlyDictionary<(int Wave, int Group), long> Targets(string eventId) =>
        _targets.TryGetValue(eventId, out var t) ? t : new Dictionary<(int, int), long>();

    public int AllRecords => _targets.Values.Sum(t => t.Count) + _failing.Count + _waited.Values.Sum(w => w.Count);

    /// <summary>The checks without a landing recorded for <paramref name="key"/> of <paramref name="eventId"/> (A3), for the tests.</summary>
    public int FailedChecks(string eventId, long key) => _waited.TryGetValue(eventId, out var w) && w.TryGetValue(key, out var n) ? n : 0;

    public void EventEnded(string eventId)
    {
        _targets.Remove(eventId);
        _failing.Remove(eventId);
        _waited.Remove(eventId);
    }

    public void Clear()
    {
        _targets.Clear();
        _failing.Clear();
        _waited.Clear();
        _phaseFailing = false;
    }

    public IReadOnlyList<HuntCheck> Tick(IReadOnlyCollection<ActiveEvent> active, IRegionWorld world, Action<string> info, Action<string> error,
        bool due = true)
    {
        var checks = new List<HuntCheck>();
        try
        {
            var hunting = active.Where(Hunted).ToList();
            foreach (var id in _targets.Keys.Where(id => !hunting.Any(a => a.Id == id)).ToList()) _targets.Remove(id);
            foreach (var id in _waited.Keys.Where(id => !hunting.Any(a => a.Id == id)).ToList()) _waited.Remove(id);
            _failing.RemoveWhere(id => !hunting.Any(a => a.Id == id));
            if (hunting.Count == 0 || !due) return checks;
            var jumps = new JumpAllowance(HuntingGrounds.MaxJumps);
            var walk = new WalkBudget(HuntingGrounds.WalkCapCalls, world.Budget);    // A3: at most 200 walk checks a check
            var first = _turn++ % hunting.Count;                                // the events take turns at the cap (A3)
            var runs = new List<EventRun>();
            foreach (var a in hunting.Skip(first).Concat(hunting.Take(first)))
                if (Prepare(a, world) is { } r) runs.Add(r);
            // every event's units with no failed check plan before any event's stuck units, so a stuck unit never spends
            // the walk budget of a unit that can jump, in its own event or another (A3; step 3 re-review F1)
            foreach (var r in runs) PlanUnits(r, stuck: false, world, jumps, walk);
            foreach (var r in runs) PlanUnits(r, stuck: true, world, jumps, walk);
            foreach (var r in runs)
                if (Finish(r, info, error) is { } c) checks.Add(c);
            _phaseFailing = false;
        }
        catch (Exception e)
        {
            if (!_phaseFailing) Phases.Safe(error, JumpRule.FailedLine("phase", e.Message));
            _phaseFailing = true;
        }
        return checks;
    }

    /// <summary>One check of <paramref name="a"/> alone under the tick's shared jump allowance (its fresh units, then its
    /// stuck ones); null when it could not run (a failed player read). Never throws.</summary>
    public HuntCheck? Run(ActiveEvent a, IRegionWorld world, JumpAllowance jumps, Action<string> info, Action<string> error,
        WalkBudget? walk = null)
    {
        walk ??= new WalkBudget(HuntingGrounds.WalkCapCalls, world.Budget);
        if (Prepare(a, world) is not { } r) return null;
        PlanUnits(r, stuck: false, world, jumps, walk);
        PlanUnits(r, stuck: true, world, jumps, walk);
        return Finish(r, info, error);
    }

    /// <summary>One event's check in progress: its players, plans, counts and records until <see cref="Finish"/>.</summary>
    sealed class EventRun(ActiveEvent a)
    {
        public ActiveEvent Event { get; } = a;
        public string? Fault;
        public bool Skipped;
        public IReadOnlyList<HuntCandidate> Players = [];
        public Func<float, float, bool> Allowed = (_, _) => false;
        public readonly List<(RegionUnit Unit, (float X, float Z) Target)> Plans = [];
        public readonly Dictionary<(int, int), long> Targets = [];
        public readonly List<RegionJump> Written = [];
        public int Waiting, InReach;
        public readonly WaitTally Tally = new();
        public Dictionary<long, int> WaitedBefore = [];
        public readonly Dictionary<long, int> WaitedNow = [];
        public readonly List<(float X, float Z, byte Level)> Anchors = [];   // units that found a landing or are in reach (A3)
        public int Failed(long key) => WaitedBefore.TryGetValue(key, out var n) ? n : 0;
    }

    /// <summary>The instances the phase moves: RegionHunt ones and pursuing runs (wave-variety D8).</summary>
    public static bool Hunted(ActiveEvent a) => HuntingGrounds.Hunts(a) || a.Pursuing;

    /// <summary>Reads <paramref name="a"/>'s players and units and gives each unit its group's target; null when the
    /// event does not hunt. A pursuing run's eligible players are the roamers' (RoamRule.Eligible) anywhere on the map, a
    /// focus-only run's its focus player alone, and its landings anywhere outside claimed territory (wave-variety D8). A
    /// failed player read leaves a skipped run that <see cref="Finish"/> reports. Never throws.</summary>
    EventRun? Prepare(ActiveEvent a, IRegionWorld world)
    {
        var r = new EventRun(a);
        try
        {
            if (a.Definition.Action is not { } action || !Hunted(a)) return null;
            var pursuing = !HuntingGrounds.Hunts(a);
            var scope = action.Scope;
            // claimed territory by this event's own map: one event's missing map never vetoes another's targets (Codex step 4 F1)
            try { r.Players = world.Players().Select(p => p with { InTerritory = p.InTerritory || world.IsClaimed(a.Id, p.X, p.Z) }).ToList(); }
            catch (Exception e)
            {
                _targets.Remove(a.Id);                                          // no stale target survives a failed read
                r.Fault = e.Message;
                r.Skipped = true;
                return r;
            }
            var focusOnly = pursuing && WaveFacts.IsFocusOnly(a.Definition);
            var focus = focusOnly ? world.FocusKey(a) : null;
            var eligible = new List<HuntCandidate>();
            foreach (var p in r.Players)
            {
                try { if (pursuing ? Pursuit.Eligible(p, focusOnly, focus) : HuntRule.Eligible(p, scope, world.RegionOf)) eligible.Add(p); }
                catch (Exception e) { r.Fault ??= e.Message; }
            }
            var units = new List<RegionUnit>();
            foreach (var t in world.Units.Where(t => HuntingGrounds.Ours(t, a)).ToList())
            {
                try { if (world.Read(t) is { } u) units.Add(u); }
                catch (Exception e) { r.Fault ??= e.Message; }
            }
            r.WaitedBefore = _waited.TryGetValue(a.Id, out var w) ? w : new Dictionary<long, int>();
            r.Allowed = pursuing ? (x, z) => !world.IsClaimed(a.Id, x, z)
                : (x, z) => HuntingGrounds.Inside(scope, world.RegionOf, x, z) && !world.IsClaimed(a.Id, x, z);
            foreach (var group in HuntRule.Groups(units))
            {
                if (HuntRule.Target(eligible, HuntRule.Mean(group.OrderBy(u => u.Key).ToList())) is not { } target) continue;   // no target: no jump, no seed
                r.Targets[group.Key] = target.Key;
                foreach (var u in group) r.Plans.Add((u, (target.X, target.Z)));
            }
        }
        catch (Exception e)
        {
            r.Fault ??= e.Message;
            r.Skipped = true;
        }
        return r;
    }

    /// <summary>Plans <paramref name="r"/>'s units with no failed check (<paramref name="stuck"/> false, by key) or its
    /// units that failed at an earlier check (true, fewest failures first): units that cannot jump never spend the walk
    /// budget of those that can (A1; step 3 review F1), and a unit cut off by the cap keeps its count, so the cap rotates
    /// (A3). Never throws.</summary>
    void PlanUnits(EventRun r, bool stuck, IRegionWorld world, JumpAllowance jumps, WalkBudget walk)
    {
        if (r.Skipped) return;
        var batch = r.Plans.Where(p => r.Failed(p.Unit.Key) > 0 == stuck).OrderBy(p => r.Failed(p.Unit.Key)).ThenBy(p => p.Unit.Key).ToList();
        foreach (var (u, target) in batch)
        {
            try
            {
                var before = r.Failed(u.Key);
                if (jumps.Left == 0 && HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z) > HuntingGrounds.Reach)
                {
                    r.Waiting++;                                                // the tick's 20 jumps are spent: no plan, no mark (F2)
                    if (before > 0) r.WaitedNow[u.Key] = before;
                    r.Tally.Waited(u);
                    continue;
                }
                (JumpKind Kind, RegionJump? Jump) planned;
                var streak = 0;
                if (before > 0 && HuntingGrounds.Distance(u.X, u.Z, target.X, target.Z) > HuntingGrounds.Reach
                    && JumpRule.Rejoin(u, target, r.Anchors, r.Players, r.Allowed, world.Walkable, world.HeightOf, walk) is { } j0)
                    planned = (JumpKind.Jump, j0);                              // A3: no landing last time, it rejoins a nearer unit first
                else
                {
                    var (spent, watched) = (r.Tally.Budget, r.Tally.Watched);
                    planned = JumpRule.Plan(u, target, r.Players, r.Allowed, world.Walkable, world.HeightOf, walk, r.Tally);
                    var waits = planned.Kind == JumpKind.Waiting;
                    var cutOff = waits && r.Tally.Budget != spent;
                    var seen = waits && r.Tally.Watched != watched;
                    // found no landing: one more; cut off by the cap or watched by a player: the count stands (re-review F2)
                    streak = waits && !cutOff && !seen ? before + 1 : waits ? before : 0;
                }
                var (kind, jump) = planned;
                if (kind == JumpKind.InReach)
                {
                    r.InReach++;
                    r.Anchors.Add((u.X, u.Z, u.Level));
                }
                else if (kind == JumpKind.Jump && jump is { } j && jumps.Take())
                {
                    world.Jump(j);
                    r.Written.Add(j);
                    if (before == 0) r.Anchors.Add((j.X, j.Z, j.Level));
                }
                else
                {
                    r.Waiting++;
                    if (streak > 0) r.WaitedNow[u.Key] = streak;
                    r.Tally.Waited(u);
                }
            }
            catch (Exception e) { r.Fault ??= e.Message; }
        }
    }

    /// <summary>Keeps <paramref name="r"/>'s targets and counts, writes its lines and its failure state; null for a
    /// skipped run. Never throws.</summary>
    HuntCheck? Finish(EventRun r, Action<string> info, Action<string> error)
    {
        var id = r.Event.Id;
        HuntCheck? done = null;
        if (!r.Skipped)
        {
            try
            {
                if (r.Event.Pursuing && !HuntingGrounds.Hunts(r.Event))
                {
                    var before = _targets.TryGetValue(id, out var was) ? was : new Dictionary<(int, int), long>();
                    foreach (var (group, key) in r.Targets.OrderBy(t => t.Key.Item1).ThenBy(t => t.Key.Item2))
                        if (!before.TryGetValue(group, out var old) || old != key) Phases.Safe(info, Pursuit.GroupLine(id, group.Item1, group.Item2));
                }
                _targets[id] = r.Targets;
                _waited[id] = r.WaitedNow;
                done = new HuntCheck(id, r.Written, r.Waiting, r.InReach);
                if (done.LogLine is { } line) Phases.Safe(info, line);
                if (r.Waiting > 0 && Detail is { } detail) Phases.Safe(detail, r.Tally.Line(id, r.Waiting));
            }
            catch (Exception e) { r.Fault ??= e.Message; }
        }
        if (r.Fault is null) _failing.Remove(id);
        else if (_failing.Add(id)) Phases.Safe(error, JumpRule.FailedLine(id, r.Fault));
        return done;
    }

}

/// <summary>The jumps left in one tick across all events (D8, S-11).</summary>
public sealed class JumpAllowance(int max)
{
    int _left = max;
    public int Left => _left;

    public bool Take()
    {
        if (_left <= 0) return false;
        _left--;
        return true;
    }
}

/// <summary>
/// The "region" tick phase (D5-D9, D12; wave-variety D8, D9): the pursuit check, then the lock, then the hunt, every
/// <see cref="HuntingGrounds.CheckSeconds"/> s while a locked or RegionHunt instance runs, a run waits to pursue or a run
/// pursues. The pursuit check runs first, so a run that starts pursuing is hunted at the same tick. One end-path entry
/// drops all three phases' records for an event (Services/EventRuntime.EndSpawnState → SpawnTracker.RegionEnded); a
/// restart or a purge clears all three. Never throws.
/// </summary>
public sealed class RegionPhase
{
    public LockPhase Lock { get; } = new();
    public HuntPhase Hunt { get; } = new();
    public PursuitPhase Pursuit { get; } = new();

    /// <summary>The health entries (D6, D9; wave-variety D12's "pursuit: check failing"), each while failing.</summary>
    public IReadOnlyList<string> Health => Lock.Health.Concat(Hunt.Health).Concat(Pursuit.Health).ToList();

    /// <summary>Every end path of <paramref name="eventId"/>: its last inside spots, targets, pursuit clock and centres
    /// and streaks go (D12; wave-variety D9).</summary>
    public void EventEnded(string eventId)
    {
        Lock.EventEnded(eventId);
        Hunt.EventEnded(eventId);
        Pursuit.EventEnded(eventId);
    }

    public void Clear()
    {
        Lock.Clear();
        Hunt.Clear();
        Pursuit.Clear();
    }

    /// <summary>True when any running instance needs the lock and hunt passes: a lock, a region hunt or a pursuing run
    /// (wave-variety D8). A run still waiting to pursue needs only the pursuit check, which has its own gate.</summary>
    public static bool Needed(IEnumerable<ActiveEvent> active) =>
        active.Any(a => HuntingGrounds.Locked(a) || a.Pursuing);

    /// <summary>One scheduler tick: the pursuit check, then the lock's and the hunt's checks when due; a run the check
    /// has just turned pursuing is hunted in the same tick, whatever the lock clock's grid; never throws.</summary>
    public (IReadOnlyList<LockCheck> Locks, IReadOnlyList<HuntCheck> Hunts) Tick(DateTime now, IReadOnlyCollection<ActiveEvent> active,
        IRegionWorld world, Func<double> random, Action<string> info, Action<string> error)
    {
        var started = false;
        try
        {
            if (Pursuit.AllRecords > 0 || PursuitPhase.Needed(active))
            {
                var before = active.Count(a => a.Pursuing);
                Pursuit.Tick(now, active, world, info, error);
                started = active.Count(a => a.Pursuing) > before;
            }
        }
        catch (Exception) { /* never throws */ }
        var due = false;
        try { due = Needed(active) && Lock.Due(now); }
        catch (Exception) { /* never throws */ }
        var locks = Lock.Tick(now, active, world, random, info, error, due);
        var hunts = Hunt.Tick(active, world, info, error, due || started);
        return (locks, hunts);
    }
}

static class Phases
{
    internal static void Safe(Action<string> log, string line)
    {
        try { log(line); }
        catch (Exception) { /* a log line never breaks a check */ }
    }
}
