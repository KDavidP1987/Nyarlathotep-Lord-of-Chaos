#nullable enable
using System;
using System.Collections.Generic;
using ProjectM;                                  // planted (EntityWrites/bad-logicusing): Logic/ holds no game code

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

/// <summary>The failure classes of event-spawns D21 that hold a health entry per event while their streak is open (D30).</summary>
public enum SpawnFailure { UnitSetup, HuntSeed, PlayerQuery }

/// <summary>The spawn health entries (walkable-spawns D5, D6; event-spawns D30): the walk check's failure streak,
/// "territory unknown" from a skipped wave until a map builds again, and one entry per open D21 streak and event.</summary>
public sealed class SpawnHealth
{
    public const string Entry = "spawns: walk check unavailable";
    public const string TerritoryEntry = "spawns: territory unknown";

    readonly SortedSet<(SpawnFailure Class, string Id)> _failing = new();

    public bool Open { get; private set; }
    public bool TerritoryUnknown { get; private set; }

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

    /// <summary>A wave was skipped because the territory map could not be built (event-spawns D17); true when this
    /// opened the entry.</summary>
    public bool TerritoryFailed()
    {
        if (TerritoryUnknown) return false;
        TerritoryUnknown = true;
        return true;
    }

    /// <summary>A territory map was built: clears the entry.</summary>
    public void TerritoryBuilt() => TerritoryUnknown = false;

    /// <summary>Sets the "territory unknown" entry from TerritoryMaps.AnyFailed (A44): it lasts while a running event's
    /// latest build failed and clears at that event's next build or end path; true when this opened it.</summary>
    public bool Territory(bool anyFailed)
    {
        var opened = anyFailed && !TerritoryUnknown;
        TerritoryUnknown = anyFailed;
        return opened;
    }

    /// <summary>A D21 failure of <paramref name="cls"/> for event <paramref name="id"/>; true when this opened the streak
    /// (log once per streak).</summary>
    public bool Failing(SpawnFailure cls, string id) => _failing.Add((cls, id));

    /// <summary>The class worked again for the event (or the event ended): closes its streak.</summary>
    public void Recovered(SpawnFailure cls, string id) => _failing.Remove((cls, id));

    public static string FailingEntry(SpawnFailure cls, string id) => cls switch
    {
        SpawnFailure.UnitSetup => $"spawns: unit setup failing ({id})",
        SpawnFailure.HuntSeed => $"spawns: hunt seed failing ({id})",
        _ => $"spawns: player query failing ({id})",
    };

    public IReadOnlyList<string> Entries =>
        (Open ? [Entry] : Array.Empty<string>())
        .Concat(TerritoryUnknown ? [TerritoryEntry] : Array.Empty<string>())
        .Concat(_failing.Select(f => FailingEntry(f.Class, f.Id)))
        .ToList();
}

/// <summary>The random source of the wave planners (event-spawns D8, D16): a double in [0, 1). Tests inject a fixed
/// sequence; the services wrap System.Random.</summary>
public interface IRandom
{
    double NextDouble();
}

public sealed class SystemRandom(Random random) : IRandom
{
    public double NextDouble() => random.NextDouble();
}

/// <summary>The lines of a wave's planners (event-spawns D8, D16, D17). None names or locates a player (D16).</summary>
public static class WaveLines
{
    public static string ZeroRolled(int wave, string id) => $"wave {wave} of {id}: 0 units rolled";
    public static string NoEligiblePlayer(int wave, string id) => $"wave {wave} of {id} skipped: no eligible player";
    public static string CentreClaimed(int wave, string id) => $"wave {wave} of {id} skipped: centre in claimed territory";
    public static string TerritoryUnknown(int wave, string id) => $"wave {wave} of {id} skipped: territory unknown";
    public static string PlayerQueryFailed(int wave, string id) => $"wave {wave} of {id} skipped: player query failed";
    public const string AroundAPlayer = "around a player";
}

/// <summary>The per-copy chance roll of a wave (event-spawns D8).</summary>
public static class WaveRoll
{
    /// <summary>Rolls each copy of each entry on its own against the entry's chance and returns the prefabs to spawn in
    /// entry order; a chance of 1.0 keeps every copy without a roll.</summary>
    public static List<string> Expand(IReadOnlyList<UnitEntry> units, IRandom rng)
    {
        var result = new List<string>();
        foreach (var u in units)
            for (var i = 0; i < u.Count; i++)
                if (u.Chance >= 1.0 || rng.NextDouble() < u.Chance) result.Add(u.Prefab);
        return result;
    }

    /// <summary>The rolled prefabs as entries, one per run of the same prefab, for WavePlan.Split's clamps (D8).</summary>
    public static List<UnitEntry> Group(IReadOnlyList<string> prefabs)
    {
        var result = new List<UnitEntry>();
        foreach (var p in prefabs)
        {
            if (result.Count > 0 && result[^1].Prefab == p) result[^1] = result[^1] with { Count = result[^1].Count + 1 };
            else result.Add(new UnitEntry(p, 1));
        }
        return result;
    }
}

/// <summary>The unit tuning of a wave's modifiers and of `.nyar spawn` (event-spawns D9).</summary>
public static class SpawnTuning
{
    /// <summary>The tuning every unit of an event with <paramref name="modifiers"/> gets: level, or levelDelta resolved on
    /// the unit's prefab level (LevelArg clamps it to 1-120), and one modifier of multiplier − 1 per stat the multiplier
    /// changes; no modifiers gives <see cref="UnitTuning.None"/>.</summary>
    public static UnitTuning TuningFrom(SpawnModifiers? modifiers) =>
        modifiers is null ? UnitTuning.None
        : TuningFrom(modifiers.Level is { } l ? new LevelArg(false, l) : modifiers.LevelDelta is { } d ? new LevelArg(true, d) : null,
            modifiers.MaxHealth, modifiers.Power, modifiers.MoveSpeed, modifiers.AttackSpeed);

    /// <summary>maxHealth → MaxHealth; power → PhysicalPower and SpellPower; moveSpeed → MovementSpeed; attackSpeed →
    /// PrimaryAttackSpeed and AbilityAttackSpeed; a multiplier of 1.0 gives no entry.</summary>
    public static UnitTuning TuningFrom(LevelArg? level, double maxHealth = 1.0, double power = 1.0, double moveSpeed = 1.0, double attackSpeed = 1.0)
    {
        var stats = new List<StatScale>();
        void Add(double multiplier, params TuningStat[] targets)
        {
            if (multiplier == 1.0) return;
            foreach (var t in targets) stats.Add(new StatScale(t, (float)(multiplier - 1.0)));
        }
        Add(maxHealth, TuningStat.MaxHealth);
        Add(power, TuningStat.PhysicalPower, TuningStat.SpellPower);
        Add(moveSpeed, TuningStat.MovementSpeed);
        Add(attackSpeed, TuningStat.PrimaryAttackSpeed, TuningStat.AbilityAttackSpeed);
        return level is null && stats.Count == 0 ? UnitTuning.None : new UnitTuning(level, stats);
    }
}

/// <summary>A player position is read only when it is a number within the map's ±10000 (event-spawns D21); any other
/// value makes the player ineligible.</summary>
public static class PlayerPosition
{
    public const float Limit = 10000f;

    public static bool Usable(float x, float z) =>
        float.IsFinite(x) && float.IsFinite(z) && MathF.Abs(x) <= Limit && MathF.Abs(z) <= Limit;
}

/// <summary>Claimed castle territory as block coordinates (event-spawns D17, S-7): block = floor((floor(v × 2) + 6400) /
/// 10) per axis, KindredCommands' CastleTerritoryService conversion; the map holds blocks 0-1279.</summary>
public static class Territory
{
    public const int MapBlocks = 1280;

    public static int Block(float v) => (int)Math.Floor((Math.Floor(v * 2.0) + 6400) / 10.0);

    public static bool IsClaimed(IReadOnlySet<(int X, int Z)> blocks, float x, float z) => blocks.Contains((Block(x), Block(z)));

    /// <summary>The claimed set of one wave from the blocks of every territory a castle heart names; a block outside
    /// 0-1279 is ignored and counted (D21).</summary>
    public static (HashSet<(int X, int Z)> Blocks, int Ignored) Build(IEnumerable<(int X, int Z)> blocks)
    {
        var set = new HashSet<(int X, int Z)>();
        var ignored = 0;
        foreach (var b in blocks)
        {
            if (b.X is < 0 or >= MapBlocks || b.Z is < 0 or >= MapBlocks) { ignored++; continue; }
            set.Add(b);
        }
        return (set, ignored);
    }
}

/// <summary>A player as the AroundPlayer pick sees it (event-spawns D16): a position, whether online and alive, and
/// whether in PvP combat (Buff_InCombat_PvPVampire). The pick never returns who it chose.</summary>
public readonly record struct PickCandidate(float X, float Y, float Z, bool Online, bool Alive, bool InPvpCombat);

public enum PickOutcome { Picked, NoEligible, QueryFailed }

/// <summary>A wave's AroundPlayer centre, or why there is none. It holds no player identity (D16).</summary>
public sealed record PickResult(PickOutcome Outcome, (float X, float Y, float Z) Centre, string? Error = null)
{
    public static readonly PickResult NoEligible = new(PickOutcome.NoEligible, default);
}

/// <summary>The AroundPlayer pick (event-spawns D16, A8, A11, A13): once per wave, uniformly among eligible players.</summary>
public static class PlayerPick
{
    /// <summary>Eligible: online, alive, a usable position, not in PvP combat, not in claimed territory and, with a
    /// regional scope (<paramref name="inScope"/> not null), inside it. The centre is at a random angle and a distance in
    /// minDist..maxDist from the player, at the player's height; with a scope an out-of-scope centre is retried at the 11
    /// other angles of 12 (SpawnPoints' order) at the same distance, and a player without an in-scope centre is not usable.
    /// A claimed centre is not retried: WaveGate skips it (A13). A throwing territory or region read is a failed query.</summary>
    public static PickResult Choose(IReadOnlyList<PickCandidate> players, IRandom rng, int minDist, int maxDist,
        Func<float, float, bool> isClaimed, Func<float, float, bool>? inScope)
    {
        try
        {
            var pool = players.Where(p => p.Online && p.Alive && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Z)
                && !isClaimed(p.X, p.Z) && (inScope is null || inScope(p.X, p.Z))).ToList();
            while (pool.Count > 0)
            {
                var i = Math.Min(pool.Count - 1, (int)(rng.NextDouble() * pool.Count));
                var p = pool[i];
                var start = rng.NextDouble() * 2 * Math.PI;
                var dist = minDist + rng.NextDouble() * (maxDist - minDist);
                for (var k = 0; k < SpawnPoints.Angles; k++)
                {
                    var a = start + k * 2 * Math.PI / SpawnPoints.Angles;
                    var x = p.X + (float)(dist * Math.Cos(a));
                    var z = p.Z + (float)(dist * Math.Sin(a));
                    if (inScope is null || inScope(x, z)) return new PickResult(PickOutcome.Picked, (x, p.Y, z));
                }
                pool.RemoveAt(i);                                              // no in-scope centre: not usable
            }
            return PickResult.NoEligible;
        }
        catch (Exception e)
        {
            return new PickResult(PickOutcome.QueryFailed, default, e.Message);
        }
    }
}

/// <summary>A player as a Hunt tick sees it (event-spawns D13): its key (the service's handle), position, and the flags
/// that make it ineligible.</summary>
public readonly record struct HuntCandidate(long Key, float X, float Z, bool Online, bool Alive, bool InTerritory, bool InPvpCombat);

/// <summary>The AggroBuffer entry HuntAction wrote for a target (event-spawns D13, A12).</summary>
public readonly record struct AggroSeed(long Target, float DamageValue, float Weight);

/// <summary>Hunt's target plan (event-spawns D13).</summary>
public static class HuntPlan
{
    public const int MaxTargets = 5;
    public const int IntervalSeconds = 5;

    /// <summary>Online, alive players with a usable position, outside claimed territory and not in PvP combat, within
    /// <paramref name="range"/> metres of the wave centre, nearest first (ties by key), at most 5.</summary>
    public static List<long> Targets(IEnumerable<HuntCandidate> players, (float X, float Z) centre, int range) =>
        players.Where(p => p.Online && p.Alive && !p.InTerritory && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Z))
            .Select(p => (p.Key, D: Distance(p.X, p.Z, centre)))
            .Where(p => p.D <= range)
            .OrderBy(p => p.D).ThenBy(p => p.Key)
            .Take(MaxTargets)
            .Select(p => p.Key)
            .ToList();

    /// <summary>The seeds to add (targets not yet seeded) and to remove (seeded players no longer targets).</summary>
    public static (List<long> Adds, List<long> Removes) Diff(IReadOnlyCollection<long> seeded, IReadOnlyList<long> targets) =>
        (targets.Where(t => !seeded.Contains(t)).ToList(), seeded.Where(s => !targets.Contains(s)).OrderBy(s => s).ToList());

    static double Distance(float x, float z, (float X, float Z) c) => Math.Sqrt((x - c.X) * (double)(x - c.X) + (z - c.Z) * (double)(z - c.Z));
}

/// <summary>HuntAction's seed record, keyed by unit and target (event-spawns D13, A6, A12): the record, not the
/// AggroBuffer, says which entries are HuntAction's. An entry is never adopted from the buffer; an entry whose Entity,
/// DamageValue or Weight differs from what HuntAction wrote is the game's.</summary>
public sealed class HuntSeeds
{
    readonly Dictionary<long, (string EventId, Dictionary<long, AggroSeed> Seeds)> _byUnit = new();

    public int Count => _byUnit.Values.Sum(u => u.Seeds.Count);
    public int CountFor(string eventId) => _byUnit.Values.Where(u => u.EventId == eventId).Sum(u => u.Seeds.Count);
    public IReadOnlyCollection<long> SeededOn(long unit) => _byUnit.TryGetValue(unit, out var u) ? u.Seeds.Keys : [];

    /// <summary>Checks the record of <paramref name="unit"/> against its AggroBuffer, keyed by target: a record entry the
    /// buffer does not hold is dropped (a partial write, or the game removed it); one whose fields differ is the game's:
    /// dropped, the entry left. Returns the seeds kept and those left to the game.</summary>
    public (int Kept, int LeftToGame) Reconcile(long unit, IReadOnlyDictionary<long, AggroSeed> buffer)
    {
        if (!_byUnit.TryGetValue(unit, out var u)) return (0, 0);
        var left = 0;
        foreach (var (target, written) in u.Seeds.ToList())
        {
            if (!buffer.TryGetValue(target, out var now)) { u.Seeds.Remove(target); continue; }
            if (now != written) { u.Seeds.Remove(target); left++; }
        }
        return (u.Seeds.Count, left);
    }

    /// <summary>One tick's plan for <paramref name="unit"/>: add a seed for each target that is neither recorded nor
    /// already in the buffer (that entry stays the game's); remove each recorded seed whose player is no longer a target.</summary>
    public (List<long> Adds, List<long> Removes) Plan(long unit, IReadOnlyList<long> targets, IReadOnlyCollection<long> inBuffer)
    {
        var (adds, removes) = HuntPlan.Diff(SeededOn(unit), targets);
        return (adds.Where(t => !inBuffer.Contains(t)).ToList(), removes);
    }

    /// <summary>A seed HuntAction wrote; recorded only after the write, so a write that failed leaves no record.</summary>
    public void Wrote(long unit, string eventId, AggroSeed seed)
    {
        if (!_byUnit.TryGetValue(unit, out var u)) _byUnit[unit] = u = (eventId, new Dictionary<long, AggroSeed>());
        u.Seeds[seed.Target] = seed;
    }

    public void Removed(long unit, long target)
    {
        if (_byUnit.TryGetValue(unit, out var u)) u.Seeds.Remove(target);
    }

    public void ForgetUnit(long unit) => _byUnit.Remove(unit);

    public void ForgetEvent(string eventId)
    {
        foreach (var unit in _byUnit.Where(u => u.Value.EventId == eventId).Select(u => u.Key).ToList()) _byUnit.Remove(unit);
    }

    public void Clear() => _byUnit.Clear();
}

/// <summary>Each event's kept territory map (event-spawns D13, D17, A37, A39): the latest successful build decides its
/// Hunt ticks; while the event's latest build failed, <see cref="ForHunt"/> gives null and HuntAction seeds nobody.
/// Every end path drops the event's entry (D33).</summary>
public sealed class TerritoryMaps
{
    readonly Dictionary<string, (IReadOnlySet<(int X, int Z)>? Map, bool Failed)> _byEvent = new(StringComparer.Ordinal);

    public int Count => _byEvent.Count;

    /// <summary>A wave of <paramref name="eventId"/> built its map: it replaces the kept one and clears a failure.</summary>
    public void Built(string eventId, IReadOnlySet<(int X, int Z)> map) => _byEvent[eventId] = (map, false);

    /// <summary>A wave's build failed: the event's Hunt seeding stops until a build succeeds (fail closed).</summary>
    public void Failed(string eventId) =>
        _byEvent[eventId] = (_byEvent.TryGetValue(eventId, out var kept) ? kept.Map : null, true);

    /// <summary>The map a Hunt tick of the event reads, or null when it has none or its latest build failed.</summary>
    public IReadOnlySet<(int X, int Z)>? ForHunt(string eventId) =>
        _byEvent.TryGetValue(eventId, out var kept) && !kept.Failed ? kept.Map : null;

    public bool Holds(string eventId) => _byEvent.ContainsKey(eventId);

    /// <summary>True while any event's latest build failed: the "territory unknown" health entry (D30, A44).</summary>
    public bool AnyFailed => _byEvent.Values.Any(v => v.Failed);

    public void Forget(string eventId) => _byEvent.Remove(eventId);

    public void Clear() => _byEvent.Clear();
}

public enum WaveOutcome { NoWave, Skip, ZeroRolled, Spawn }

/// <summary>One wave's decision and its log line (event-spawns D29); <see cref="Units"/> are the entries to spawn after
/// the caps.</summary>
public sealed record WaveDecision(WaveOutcome Outcome, string? Line, IReadOnlyList<UnitEntry> Units, IReadOnlyList<string> CapLines);

/// <summary>What a wave's gate needs to know about the wave (event-spawns D29, A27): <see cref="MapFailed"/> says the
/// territory map could not be built, and the location type and behaviour say whether the wave needs it.</summary>
public sealed record WaveFacts(
    int Wave,
    string EventId,
    bool Blocked,
    bool MapFailed = false,
    LocationType Location = LocationType.Point,
    BehaviourType? Behaviour = null,
    PickOutcome? Pick = null,
    bool CentreClaimed = false,
    bool AllowTerritory = false)
{
    /// <summary>AroundPlayer needs the map for its player pick and Hunt for its target filter, whatever allowTerritory
    /// says; any other wave needs it only for the claimed-centre skip, which allowTerritory lifts (A27).</summary>
    public bool NeedsMap => Location == LocationType.AroundPlayer || Behaviour == BehaviourType.Hunt || !AllowTerritory;
}

/// <summary>The one order of a wave's rules (event-spawns D29, A26, A27): a control blocker or an ended event; territory
/// unknown when the wave needs the map (<see cref="WaveFacts.NeedsMap"/>); no eligible player (or a failed player query) for AroundPlayer; a claimed centre
/// unless allowTerritory; the chance roll; then WavePlan.Split's MaxUnitsPerWave and MaxTrackedUnits. WaveAction takes
/// the outcome and decides nothing itself.</summary>
public static class WaveGate
{
    public static WaveDecision Decide(WaveFacts f, Func<IReadOnlyList<string>> roll, int maxPerWave, int occupied, int maxTracked)
    {
        if (f.Blocked) return new(WaveOutcome.NoWave, null, [], []);
        if (f.MapFailed && f.NeedsMap) return new(WaveOutcome.Skip, WaveLines.TerritoryUnknown(f.Wave, f.EventId), [], []);
        if (f.Pick is PickOutcome.NoEligible) return new(WaveOutcome.Skip, WaveLines.NoEligiblePlayer(f.Wave, f.EventId), [], []);
        if (f.Pick is PickOutcome.QueryFailed) return new(WaveOutcome.Skip, WaveLines.PlayerQueryFailed(f.Wave, f.EventId), [], []);
        if (f.CentreClaimed && !f.AllowTerritory) return new(WaveOutcome.Skip, WaveLines.CentreClaimed(f.Wave, f.EventId), [], []);
        var rolled = roll();
        if (rolled.Count == 0) return new(WaveOutcome.ZeroRolled, WaveLines.ZeroRolled(f.Wave, f.EventId), [], []);
        var caps = new List<string>();
        var units = WavePlan.Split(WaveRoll.Group(rolled), maxPerWave, occupied, maxTracked, caps);
        return new(WaveOutcome.Spawn, null, units, caps);
    }
}

/// <summary>An event's units, hunt seeds and kept territory map, ended together (event-spawns D33, A39): every end path
/// empties all three for the event and leaves every other event's state as it is.</summary>
public sealed class WaveLifecycle(SpawnLedger ledger, HuntSeeds seeds, TerritoryMaps maps)
{
    public SpawnLedger Ledger { get; } = ledger;
    public HuntSeeds Seeds { get; } = seeds;
    public TerritoryMaps Maps { get; } = maps;

    public int TrackedFor(string eventId) => Ledger.Units.Count(u => u.EventId == eventId);

    /// <summary>Natural end, `event stop` and a fault cancel: the event's units are queued for the despawn drain and its
    /// seeds and kept map dropped at once, so hunting stops with the event.</summary>
    public (int Queued, int Cancelled) EventEnded(string eventId, DateTime spawnedBefore, bool cancelOrders = true)
    {
        Seeds.ForgetEvent(eventId);
        Maps.Forget(eventId);
        return Ledger.EndEvent(eventId, spawnedBefore, cancelOrders);
    }

    /// <summary>`purge confirm`: every unit queued, every seed and kept map dropped.</summary>
    public (int Queued, int Cancelled) Purged()
    {
        Seeds.Clear();
        Maps.Clear();
        return Ledger.Purge();
    }

    /// <summary>A unit left the game (despawned or died): its seeds go with it.</summary>
    public void UnitGone(long key)
    {
        Ledger.Forget(key);
        Seeds.ForgetUnit(key);
    }

    /// <summary>A restart: a new ledger (the boot sweep queues the survivors), an empty seed set and no kept map.</summary>
    public static WaveLifecycle Restart(LedgerLimits limits) => new(new SpawnLedger(limits), new HuntSeeds(), new TerritoryMaps());
}
