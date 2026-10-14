#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nyarlathotep.Logic;

// Unit watch (docs/dod/unit-watch.md, design §9 D54): every live unit of a running event instance is watched every 5 s; a
// unit that stands still beside a player (D2) or stays far under the ground (D3) is marked stuck, moved back unseen (D5),
// and after 2 failed tries removed through the staged despawn with no kill credit (D6-D8); a quiet waits-for-clear wave's
// leftovers are marked by the stall backstop (D9), and `.nyar event unstick` marks by hand (D12). Everything here is pure:
// Services implement IUnitWatchWorld over SpawnTracker's own entities, as hunting-grounds' IRegionWorld.

/// <summary>The numbers of unit watch, each with its source (docs/dod/unit-watch.md › Assumptions).</summary>
public static class UnitWatch
{
    /// <summary>Recovery tries before a removal (design §9 D54); not configurable (D1).</summary>
    public const int Tries = 2;
    /// <summary>How often the watch checks: the existing behaviour cadence (HuntPlan.IntervalSeconds).</summary>
    public const int CheckSeconds = HuntPlan.IntervalSeconds;
    /// <summary>A unit that moved no more than this on x and z over StuckSeconds has stood still (S-4).</summary>
    public const float StandStill = 5f;
    /// <summary>Standing counts only while an online player stands this near, or the unit holds a player target (S-5).</summary>
    public const float PlayerReach = 40f;
    /// <summary>A unit that moved no more than this on x and z from its spawn or landing spot over SpawnStillSeconds has not
    /// moved at all (D34, A1).</summary>
    public const float StillReach = 0.5f;
    /// <summary>Two off-ground readings this many seconds apart mark a unit (S-6).</summary>
    public const int OffGroundSeconds = 10;
    /// <summary>A unit this far below the lowest grounded level at its x and z is off the ground (S-12's fallback, the
    /// spike's "off-ground verdict: not usable").</summary>
    public const float BelowGround = 10f;
    /// <summary>A player target this near on x and z counts as a fight (S-9's fallback clause; a ranged unit's own attack
    /// range when longer, Review 2 F2).</summary>
    public const float FightReach = 5f;
    /// <summary>No unit is moved while an online player stands this near (hunting-grounds' watch range, S-10).</summary>
    public const float WatchRange = HuntingGrounds.WatchRange;
    /// <summary>Every landing stands at least this far from every online player (hunting-grounds S-10).</summary>
    public const float LandingDistance = HuntingGrounds.LandingDistance;
    /// <summary>A group landing lies within this of a group mate, an anchor landing within this of the anchor (S-7).</summary>
    public const float GroupReach = 10f;
    public const float AnchorReach = 10f;
    /// <summary>Units moved in one check across all events (S-11).</summary>
    public const int MaxMoves = 10;
    /// <summary>Walk checks in one check across all events (S-11), each two <see cref="WalkBudget"/> calls.</summary>
    public const int WalkCap = 120;
    public const int WalkCapCalls = 2 * WalkCap;

    /// <summary>The refusal of `unstick` with the watch off (D12, D13: invalid watch_off).</summary>
    public const string Off = "unit watch is off: [UnitWatch] Enabled";

    /// <summary>The tracked unit is watched for <paramref name="a"/>: its event id, spawned at or after the instance's start
    /// (wave-sets A3). `.nyar spawn` units and an earlier instance's units never are (D2).</summary>
    public static bool Watched(TrackedUnit t, ActiveEvent a) => HuntingGrounds.Ours(t, a);

    /// <summary>The unit holds a player target within its reach: 5 m on x and z, or its attack range when longer (D4).</summary>
    public static bool TargetClose(WatchUnit u) => u.TargetDistance is { } d && d <= Math.Max(FightReach, u.AttackRange);

    /// <summary>The nearest online player with a usable position, or null.</summary>
    public static float? Nearest(IEnumerable<HuntCandidate> online, float x, float z) => JumpRule.Nearest(online, x, z);
}

/// <summary>The cfg keys as loaded (D1, D34): the switch, the stuck time, the backstop's quiet minutes (0: off) and the
/// still-at-spawn time.</summary>
public sealed record WatchSettings(bool Enabled, int StuckSeconds, int StallMinutes, int SpawnStillSeconds = 10)
{
    public static readonly WatchSettings Default =
        new(true, Limits.StuckSeconds.Default, Limits.StallMinutes.Default, Limits.SpawnStillSeconds.Default);
    public TimeSpan Stuck => TimeSpan.FromSeconds(StuckSeconds);
    public TimeSpan SpawnStill => TimeSpan.FromSeconds(SpawnStillSeconds);
}

/// <summary>Why a unit was marked stuck (D16).</summary>
public enum StuckReason { Standing, OffGround, Stall, Admin, Still }

/// <summary>Which landing a recovery used (D5, S-7).</summary>
public enum LandingKind { Group, Player, Anchor }

/// <summary>Why a recovery try failed (D6, D16).</summary>
public enum TryFailure { NoLanding, Seen, StillStuck }

/// <summary>Why a marked unit was cleared (D6, D16).</summary>
public enum ClearReason { Fought, Moving }

/// <summary>A live tracked unit as the watch reads it: its ledger key, position, walk-check height level, group within its
/// wave, its anchor (spawn spot, regroup anchor, arena centre, a horde's front, a roamer's hotspot) with the level there,
/// and, when it holds a player as its aggro target, that player's distance on x and z (never the player) and its own
/// attack range; and the last time one of our movers put it down (its spawn, a hidden jump, an apocalypse hop, a region or
/// arena return, a regroup, a recovery) with the spot, when the game side knows it (D34).</summary>
public readonly record struct WatchUnit(long Key, float X, float Y, float Z, byte Level, int Group,
    (float X, float Y, float Z, byte Level) Anchor, float? TargetDistance = null, float AttackRange = 0f,
    (DateTime At, float X, float Z)? PutDown = null);

/// <summary>The walk check's answer at a unit's x and z (D3, S-12's fallback): the height of the lowest grounded level
/// there, or null when no level is grounded at all.</summary>
public readonly record struct GroundReading(float? LowestY);

/// <summary>One recovery move (D5): the landing, its level, the landing kind, how far the unit moved (its own distance,
/// never a player's) and its new return point (PreCombatPosition), which follows it.</summary>
public readonly record struct WatchMove(long Key, float X, float Y, float Z, byte Level, LandingKind Kind, float Distance,
    (float X, float Y, float Z) Return);

/// <summary>The counts `event info` shows for a running instance (D15): units stuck now, recoveries and removals since the
/// instance started.</summary>
public readonly record struct WatchCounts(int StuckNow, int Recovered, int Removed);

/// <summary>
/// The game side of the watch (D2-D10), implemented by Services over SpawnTracker's ledger and its own entities. Every
/// member may throw, which the phase takes as a failure of that read or write only (D18).
/// </summary>
public interface IUnitWatchWorld
{
    /// <summary>The ledger's tracked units (key, prefab, event, spawn time, wave).</summary>
    IEnumerable<TrackedUnit> Units { get; }

    /// <summary>One unit's position, level, group, anchor and player target; null when it is gone since the ledger listed it.</summary>
    WatchUnit? Read(TrackedUnit unit);

    /// <summary>Every connected character as Hunt sees it (PlayerQuery.Read): read once per check, memory only (D17).</summary>
    IReadOnlyList<HuntCandidate> Players();

    /// <summary>The Hunt key of a revenge instance's focus player, or null (Spawning's FocusKey join).</summary>
    long? FocusKey(ActiveEvent a);

    /// <summary>The walk check's lowest grounded level at (x, z) (one walk check, D3); null when it cannot answer there.</summary>
    GroundReading? Ground(float x, float z);

    /// <summary>The walk check at a point and level: free and grounded (two game calls).</summary>
    bool Walkable(float x, float z, byte level);

    /// <summary>The height a unit stands at on a walk-check level.</summary>
    float HeightOf(byte level);

    /// <summary>The region at a point (RegionIndex.RegionOf).</summary>
    string RegionOf(float x, float z);

    /// <summary>The point lies in claimed territory by the event's kept map (TerritoryMaps.ForHunt).</summary>
    bool IsClaimed(string eventId, float x, float z);

    /// <summary>The hordes an apocalypse instance has raised (ApocalypseRun.Risen); null for any other instance.</summary>
    int? HordesRisen(ActiveEvent a);

    /// <summary>The tick's walk-check budget the other movers left (WalkBudget.PerTick, D10).</summary>
    WalkBudget Budget { get; }

    /// <summary>Moves one of our units to the landing, its LastTranslation and PreCombatPosition with it (D5).</summary>
    void Move(WatchMove move);

    /// <summary>Queues one of our units on SpawnTracker's staged despawn (MaxDespawnsPerTick, D6); false when the ledger no
    /// longer holds it or it is already queued. Never a kill: no death event follows (D8).</summary>
    bool Despawn(long key);
}

/// <summary>
/// The fight read (D4, the spike's verdict): the apply signal (StatChangeSystem.ApplyStatChanges, reason DealDamageSystem)
/// reports each hit between a tracked unit and anyone; only a hit by or on a player is kept, as the hit's time per tracked
/// unit. Memory only, never a player's name, key or position; a row dies with its unit. A unit with no row has not fought.
/// </summary>
public sealed class FightBook
{
    readonly Dictionary<long, DateTime> _last = new();

    /// <summary>The rows held (one per tracked unit that fought a player).</summary>
    public int Rows => _last.Count;

    /// <summary>A hit between tracked unit <paramref name="key"/> and another side at <paramref name="at"/>: kept when the
    /// other side is a player and the unit is tracked; a hit between NPCs or on a structure is not a fight. True when kept.</summary>
    public bool Report(long key, bool otherIsPlayer, DateTime at, Func<long, bool> tracked)
    {
        if (!otherIsPlayer || !tracked(key)) return false;
        if (!_last.TryGetValue(key, out var was) || at > was) _last[key] = at;
        return true;
    }

    /// <summary>The unit's last fight with a player, or null.</summary>
    public DateTime? Last(long key) => _last.TryGetValue(key, out var at) ? at : null;

    /// <summary>The unit hit a player, or was hit by one, at or after <paramref name="since"/>.</summary>
    public bool Fought(long key, DateTime since) => _last.TryGetValue(key, out var at) && at >= since;

    public void Forget(long key) => _last.Remove(key);

    /// <summary>Drops the rows of units that left the ledger.</summary>
    public void Keep(Func<long, bool> tracked)
    {
        foreach (var key in _last.Keys.Where(k => !tracked(k)).ToList()) _last.Remove(key);
    }

    public void Clear() => _last.Clear();
}

/// <summary>
/// The landing of a recovery (D5, S-7): in order, within 10 m of a live, not-stuck unit of its own group, else 50-60 m from
/// the eligible player nearest the unit, else within 10 m of its anchor. Every landing stands 50 m or more from every online
/// player, passes <see cref="LandingContext.Allowed"/> (claimed territory, scope regions, the arena circle) and is free and
/// grounded by the walk check, two calls a level from the budget.
/// </summary>
public static class LandingRule
{
    static readonly (float R, double Deg)[] GroupRing = [(5f, 0), (5f, 120), (5f, 240), (10f, 60), (10f, 180), (10f, 300)];
    static readonly (float R, double Deg)[] PlayerRing = [(55f, 0), (55f, 20), (55f, -20), (50f, 0), (60f, 0), (55f, 40), (55f, -40)];
    static readonly (float R, double Deg)[] AnchorRing = [(0f, 0), (5f, 0), (5f, 90), (5f, 180), (5f, 270), (10f, 45), (10f, 135), (10f, 225), (10f, 315)];
    /// <summary>Group mates a landing is tried beside, nearest first.</summary>
    public const int MatesTried = 3;

    /// <summary>The landing of <paramref name="u"/>, or null when none holds; <c>Spent</c> when the budget ran out first
    /// (the unit waits for the next check, no try spent).</summary>
    public static (WatchMove? Move, bool Spent) Choose(WatchUnit u, LandingContext c)
    {
        foreach (var mate in c.Mates.OrderBy(m => HuntingGrounds.Distance(m.X, m.Z, u.X, u.Z)).ThenBy(m => m.Key).Take(MatesTried))
        {
            var found = Try(u, c, LandingKind.Group, Around((mate.X, mate.Z), 0, GroupRing), [mate.Level], out var spent);
            if (found is not null || spent) return (found, spent);
        }
        var anchorLevels = WalkLevels.Spot(u.Anchor.Level).ToList();
        if (c.Eligible.Where(p => PlayerPosition.Usable(p.X, p.Z))
                .OrderBy(p => HuntingGrounds.Distance(p.X, p.Z, u.X, u.Z)).ThenBy(p => p.Key).FirstOrDefault() is { Online: true } target)
        {
            var toward = Math.Atan2(u.Z - target.Z, u.X - target.X);
            var found = Try(u, c, LandingKind.Player, Around((target.X, target.Z), toward, PlayerRing), anchorLevels, out var spent);
            if (found is not null || spent) return (found, spent);
        }
        var home = Try(u, c, LandingKind.Anchor, Around((u.Anchor.X, u.Anchor.Z), 0, AnchorRing), anchorLevels, out var budget);
        return (home, budget);
    }

    static IEnumerable<(float X, float Z)> Around((float X, float Z) at, double baseAngle, (float R, double Deg)[] ring)
    {
        foreach (var (r, deg) in ring)
        {
            var angle = baseAngle + deg * Math.PI / 180;
            yield return ((float)(at.X + r * Math.Cos(angle)), (float)(at.Z + r * Math.Sin(angle)));
        }
    }

    static WatchMove? Try(WatchUnit u, LandingContext c, LandingKind kind, IEnumerable<(float X, float Z)> spots, IReadOnlyList<byte> levels, out bool spent)
    {
        spent = false;
        foreach (var (x, z) in spots)
        {
            if (!Clear(c.Online, x, z) || !c.Allowed(x, z)) continue;
            foreach (var level in levels)
            {
                if (!c.Budget.TryTake(2))
                {
                    spent = true;
                    return null;
                }
                if (!c.Walkable(x, z, level)) continue;
                var y = c.HeightOf(level);
                return new WatchMove(u.Key, x, y, z, level, kind, HuntingGrounds.Distance(u.X, u.Z, x, z), (x, y, z));
            }
        }
        return null;
    }

    /// <summary>The spot stands at least <see cref="UnitWatch.LandingDistance"/> from every online player.</summary>
    public static bool Clear(IEnumerable<HuntCandidate> online, float x, float z) =>
        UnitWatch.Nearest(online, x, z) is not { } d || d >= UnitWatch.LandingDistance;

    /// <summary>The players a player landing may aim at (D5): Hunt's eligibility (online, alive, usable position, outside
    /// claimed territory, not in PvP combat) inside the scope regions of a scoped event; for a revenge instance its focus
    /// player alone (revenge D6).</summary>
    public static List<HuntCandidate> Eligible(IEnumerable<HuntCandidate> players, Scope scope, Func<float, float, string> regionOf, long? focus, bool focusOnly) =>
        players.Where(p => p.Online && p.Alive && !p.InTerritory && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Z)
                           && (scope.IsGlobal || scope.Names(regionOf(p.X, p.Z)))
                           && (!focusOnly || focus is { } k && p.Key == k))
            .ToList();
}

/// <summary>What one landing choice reads (D5): the online players, the eligible ones, the unit's live, not-stuck group
/// mates, the event's own rules for a spot (claimed territory unless allowTerritory, the scope regions of a scoped event,
/// the arena circle of a swarm), the walk check, each level's height and the check's walk budget.</summary>
public sealed record LandingContext(
    IReadOnlyList<HuntCandidate> Online,
    IReadOnlyList<HuntCandidate> Eligible,
    IReadOnlyList<WatchUnit> Mates,
    Func<float, float, bool> Allowed,
    Func<float, float, byte, bool> Walkable,
    Func<byte, float> HeightOf,
    WalkBudget Budget);

/// <summary>
/// Which waves the stall backstop watches (D9, S-13, Review 2 F3): a waveList wave that was decided and queued units, when
/// it is the last wave or the next wave's afterSeconds is null (Engine's own rule: that wave starts only on the clear); and
/// an apocalypse's risen hordes, bosses included, once its last horde has risen. The units form, growth and swarm waves
/// always have a timer and are never watched.
/// </summary>
public static class StallClock
{
    /// <summary>The watched waves of <paramref name="a"/> (1-based waveList waves, or horde numbers).</summary>
    public static IEnumerable<int> Waves(ActiveEvent a, int? hordesRisen)
    {
        if (a.Definition.Apocalypse is { } apo)
        {
            if (hordesRisen is { } risen && risen >= apo.Hordes.Count)
                for (var k = 1; k <= risen; k++) yield return k;
            yield break;
        }
        if (a.Definition.Action is not { WaveList: { } list, Growth: null, Arena: null }) yield break;
        for (var k = 1; k <= a.Decided.Count && k <= list.Count; k++)
            if (a.Decided[k - 1].Fought && (k == list.Count || list[k].AfterSeconds is null)) yield return k;
    }

    /// <summary>The ledger waves of watched wave <paramref name="k"/>: the wave itself, or a horde's units and its bosses.</summary>
    public static IReadOnlyList<int> LedgerWaves(ActiveEvent a, int k) =>
        a.Definition.Apocalypse is null ? [k]
        : [Apocalypse.HordeWave(k), .. Enumerable.Range(1, EventValidator.MaxBosses).Select(j => Apocalypse.BossWave(k, j))];
}

/// <summary>The watch's lines (D16, D15, D12): the service adds "[nyar] " to the log lines. Prefab names, wave numbers,
/// counts and our units' own move distances only, never a player (D17).</summary>
public static class UnitWatchLines
{
    public static string Reason(StuckReason r) => r switch
    {
        StuckReason.Standing => "standing",
        StuckReason.OffGround => "off-ground",
        StuckReason.Stall => "stall",
        StuckReason.Still => "still",
        _ => "admin",
    };

    static string Kind(LandingKind k) => k switch { LandingKind.Group => "group", LandingKind.Player => "player", _ => "anchor" };

    static string Failure(TryFailure f) => f switch { TryFailure.NoLanding => "no landing", TryFailure.Seen => "seen", _ => "still stuck" };

    public static string Stuck(string id, string prefab, int wave, StuckReason reason) => $"unit-watch {id} stuck: {prefab} wave {wave} ({Reason(reason)})";

    public static string Recovered(string id, string prefab, int attempt, float metres, LandingKind kind) =>
        string.Create(CultureInfo.InvariantCulture, $"unit-watch {id} recovered: {prefab} try {attempt}/{UnitWatch.Tries}, {metres:0} m ({Kind(kind)})");

    public static string TryFailed(string id, string prefab, int attempt, TryFailure failure) =>
        $"unit-watch {id} try failed: {prefab} try {attempt}/{UnitWatch.Tries} ({Failure(failure)})";

    public static string Removed(string id, string prefab, int wave, bool inView) =>
        $"unit-watch {id} removed: {prefab} wave {wave} after {UnitWatch.Tries} tries, no kill credit{(inView ? " (in view)" : "")}";

    public static string Cleared(string id, string prefab, ClearReason reason) =>
        $"unit-watch {id} cleared: {prefab} ({(reason == ClearReason.Fought ? "fought" : "moving")})";

    public static string Stall(string id, int wave, int minutes, int marked) => $"unit-watch {id} stall: wave {wave} quiet {minutes} min, {marked} leftovers marked";

    public static string Waiting(string id, string prefab) => $"unit-watch {id} waiting: {prefab}, a player near";

    public static string ReadFailed(string message) => $"unit-watch: read failed: {message}";

    public static string MoveFailed(string message) => $"unit-watch: move failed: {message}";

    /// <summary>`event info`'s line for a running instance (D15).</summary>
    public static string Info(bool enabled, WatchCounts c) =>
        enabled ? $"unit watch: {c.StuckNow} stuck now, {c.Recovered} recovered, {c.Removed} removed" : "unit watch: off";

    /// <summary>`unstick`'s reply (D12).</summary>
    public static string Unstuck(string id, int marked, int fighting) => $"unstick {id}: {marked} units marked, {fighting} fighting left alone";

    /// <summary>The usage line of a missing id or a surplus word (D12; plain words, chat eats angle brackets).</summary>
    public const string Usage = "usage: .nyar event unstick id";

    public static string NotRunning(string id) => $"unstick {id}: not running";

    public static string NoLiveUnit(string id) => $"unstick {id}: no live unit";
}

/// <summary>
/// The "watch" tick phase (D2-D11, D16): every <see cref="UnitWatch.CheckSeconds"/> s while an event runs and the watch is
/// on, each running instance's live units are read, the marked ones tried (or removed) first and the rest checked against
/// the standing and off-ground rules, then the stall backstop. Guarded per unit: a throwing read skips that unit and is never
/// a mark; a throwing move is a failed try for that unit only; each failure family logs once per streak and holds its health
/// entry until a check runs clean, the events end or the server restarts (D18). Never throws.
/// </summary>
public sealed class UnitWatchPhase
{
    public const string ReadFailing = "unit watch: read failing";
    public const string MoveFailing = "unit watch: move failing";

    sealed class Row(long key, string eventId, DateTime instance, string prefab, int wave)
    {
        public long Key { get; } = key;
        public string EventId { get; } = eventId;
        public DateTime Instance { get; } = instance;
        public string Prefab { get; } = prefab;
        public int Wave { get; } = wave;
        public DateTime WindowStart;
        public float WindowX, WindowZ;
        public DateTime? OffSince;
        public DateTime? GroundRead;
        public DateTime? LastFight;
        public float StillX, StillZ;
        public DateTime StillSince;
        public bool StillNear;
        public bool StillDone;
        public StuckReason? Mark;
        public DateTime MarkedUtc;
        public DateTime TryClock;
        public DateTime? MovedUtc;
        public int Try;
        public bool Waiting;
        public bool Removing;
        public DateTime RemoveSince;
        public bool Removed;
    }

    sealed class Clock(IReadOnlyList<int> waves, DateTime since)
    {
        public IReadOnlyList<int> Waves { get; } = waves;
        public DateTime Since = since;
    }

    readonly Dictionary<long, Row> _rows = new();
    readonly Dictionary<(string Event, int Wave), Clock> _clocks = new();
    readonly Dictionary<(string Event, DateTime Instance), (int Recovered, int Removed)> _counts = new();
    readonly FailureStreak _read = new();
    readonly FailureStreak _move = new();
    bool _phaseFailing;
    DateTime _next;

    /// <summary>The fight read's rows (D4).</summary>
    public FightBook Fights { get; } = new();

    /// <summary>The two health entries, each while failing (D18).</summary>
    public IReadOnlyList<string> Health =>
        [.. _read.Count > 0 || _phaseFailing ? new[] { ReadFailing } : [], .. _move.Count > 0 ? new[] { MoveFailing } : []];

    /// <summary>Every row, clock, count and streak held, for the end-path checks (D11).</summary>
    public int AllRecords => _rows.Count + _clocks.Count + _counts.Count + Fights.Rows + _read.Count + _move.Count;

    /// <summary>The rows held for <paramref name="eventId"/>.</summary>
    public int Rows(string eventId) => _rows.Values.Count(r => r.EventId == eventId);

    /// <summary>The unit's tries so far, 0 without a row.</summary>
    public int TriesOf(long key) => _rows.TryGetValue(key, out var r) ? r.Try : 0;

    /// <summary>The unit's mark, or null.</summary>
    public StuckReason? MarkOf(long key) => _rows.TryGetValue(key, out var r) && !r.Removed ? r.Mark : null;

    /// <summary>`event info`'s counts for a running instance (D15).</summary>
    public WatchCounts Counts(ActiveEvent a)
    {
        var stuck = _rows.Values.Count(r => r.EventId == a.Id && r.Instance == a.Instance.StartedUtc && r.Mark is not null && !r.Removed);
        var (recovered, removed) = _counts.TryGetValue((a.Id, a.Instance.StartedUtc), out var c) ? c : (0, 0);
        return new WatchCounts(stuck, recovered, removed);
    }

    /// <summary>Every end path of <paramref name="eventId"/> (D11): its rows, tries, clocks and counts go.</summary>
    public void EventEnded(string eventId)
    {
        foreach (var key in _rows.Values.Where(r => r.EventId == eventId).Select(r => r.Key).ToList())
        {
            _rows.Remove(key);
            Fights.Forget(key);
        }
        foreach (var k in _clocks.Keys.Where(k => k.Event == eventId).ToList()) _clocks.Remove(k);
        foreach (var k in _counts.Keys.Where(k => k.Event == eventId).ToList()) _counts.Remove(k);
        if (_rows.Count == 0)
        {
            _read.Ok();
            _move.Ok();
            _phaseFailing = false;
        }
    }

    /// <summary>A purge, the spawns pillar off, the watch turned off or a restart (D10, D11): everything goes.</summary>
    public void Clear()
    {
        _rows.Clear();
        _clocks.Clear();
        _counts.Clear();
        Fights.Clear();
        _read.Ok();
        _move.Ok();
        _phaseFailing = false;
        _next = default;
    }

    /// <summary>A unit of ours died (the death pass, before SpawnTracker.Died): its row goes, a removal it waited for is
    /// dropped (a player's kill keeps its credit, D8), and the stall clock of its wave restarts (D9).</summary>
    public void Died(long key, string? eventId, int wave, DateTime at)
    {
        _rows.Remove(key);
        Fights.Forget(key);
        if (eventId is null) return;
        foreach (var (k, clock) in _clocks)
            if (k.Event == eventId && clock.Waves.Contains(wave) && at > clock.Since) clock.Since = at;
    }

    /// <summary>A unit left the ledger otherwise (despawn, lifetime, the boot sweep): its row goes (D11).</summary>
    public void Gone(long key)
    {
        _rows.Remove(key);
        Fights.Forget(key);
    }

    /// <summary>Whether a check is due at <paramref name="now"/> (and the next one scheduled).</summary>
    internal bool Due(DateTime now)
    {
        if (now < _next) return false;
        _next = now.AddSeconds(UnitWatch.CheckSeconds);
        return true;
    }

    /// <summary>One scheduler tick: with the watch off everything is dropped (D10); else one check when due. Never throws.</summary>
    public void Tick(DateTime now, IReadOnlyCollection<ActiveEvent> active, IUnitWatchWorld world, WatchSettings settings, Action<string> info, Action<string> error)
    {
        try
        {
            if (!settings.Enabled)
            {
                Clear();
                return;
            }
            if (!Due(now)) return;
        }
        catch (Exception) { return; }
        Run(now, active, world, settings, info, error);
    }

    /// <summary>One check (D2-D9). Never throws.</summary>
    public void Run(DateTime now, IReadOnlyCollection<ActiveEvent> active, IUnitWatchWorld world, WatchSettings settings, Action<string> info, Action<string> error)
    {
        var check = new Check(this, now, world, settings, info);
        try
        {
            if (!settings.Enabled)
            {
                Clear();
                return;
            }
            foreach (var r in _rows.Values.Where(r => !active.Any(a => a.Id == r.EventId && a.Instance.StartedUtc == r.Instance)).ToList())
                _rows.Remove(r.Key);
            foreach (var k in _clocks.Keys.Where(k => active.All(a => a.Id != k.Event)).ToList()) _clocks.Remove(k);
            foreach (var k in _counts.Keys.Where(k => !active.Any(a => a.Id == k.Event && a.Instance.StartedUtc == k.Instance)).ToList()) _counts.Remove(k);
            if (active.Count == 0)
            {
                Fights.Clear();
                _read.Ok();
                _move.Ok();
                _phaseFailing = false;
                return;
            }
            check.Run(active);
            _phaseFailing = false;
        }
        catch (Exception e)
        {
            if (!_phaseFailing) Phases.Safe(error, UnitWatchLines.ReadFailed($"phase: {e.Message}"));
            _phaseFailing = true;
            return;
        }
        if (check.ReadFault is { } read) { if (_read.Fail()) Phases.Safe(error, UnitWatchLines.ReadFailed(read)); }
        else _read.Ok();
        if (check.MoveFault is { } move) { if (_move.Fail()) Phases.Safe(error, UnitWatchLines.MoveFailed(move)); }
        else _move.Ok();
    }

    /// <summary>`.nyar event unstick &lt;id&gt;` (D12): marks every live unit of the running instance that has not fought in
    /// the last StuckSeconds as stuck ("admin"); a unit already marked is not marked twice. Refused, changing nothing, with
    /// the watch off, for an unknown id, an event not running or one with no live unit.</summary>
    public Outcome Unstick(string id, bool known, ActiveEvent? a, IUnitWatchWorld world, WatchSettings settings, DateTime now, Action<string> info)
    {
        if (!settings.Enabled) return Outcome.Refused(UnitWatch.Off, RefusalCode.Invalid, reason: Reasons.WatchOff);
        if (!known && a is null) return AdminLines.UnknownEvent(id);
        if (a is null) return Outcome.Refused(UnitWatchLines.NotRunning(id), RefusalCode.State, "id", reason: Reasons.NotActive);
        var units = world.Units.Where(t => UnitWatch.Watched(t, a)).OrderBy(t => t.Key).ToList();
        var reads = new List<(TrackedUnit T, WatchUnit U)>();
        foreach (var t in units)
        {
            try { if (world.Read(t) is { } u) reads.Add((t, u)); }
            catch (Exception) { /* an unread unit is left alone */ }
        }
        if (reads.Count == 0) return Outcome.Refused(UnitWatchLines.NoLiveUnit(id), RefusalCode.State, "id");
        var since = now - settings.Stuck;
        int marked = 0, fighting = 0;
        foreach (var (t, u) in reads)
        {
            _rows.TryGetValue(t.Key, out var row);
            if (UnitWatch.TargetClose(u) || Fights.Fought(t.Key, since) || row?.LastFight >= since)
            {
                fighting++;
                continue;
            }
            row ??= NewRow(a, t, u, now);
            if (row.Mark is not null || row.Removed) continue;
            Mark(row, StuckReason.Admin, now, info);
            marked++;
        }
        var reply = UnitWatchLines.Unstuck(id, marked, fighting);
        return Outcome.Done(reply, ("marked", marked.ToString(CultureInfo.InvariantCulture)), ("fighting", fighting.ToString(CultureInfo.InvariantCulture)));
    }

    Row NewRow(ActiveEvent a, TrackedUnit t, WatchUnit? u, DateTime now)
    {
        var row = new Row(t.Key, a.Id, a.Instance.StartedUtc, t.Prefab, t.Wave) { WindowStart = now, WindowX = u?.X ?? 0, WindowZ = u?.Z ?? 0 };
        // D34: the still check starts at the spawn (where the game side knows the spot, else the first read) or at the
        // latest landing of one of our movers
        if (u?.PutDown is { } p) StillFrom(row, p.X, p.Z, p.At);
        else StillFrom(row, u?.X ?? 0, u?.Z ?? 0, t.SpawnedUtc);
        if (u is null) row.StillDone = true;
        _rows[t.Key] = row;
        return row;
    }

    void Mark(Row row, StuckReason reason, DateTime now, Action<string> info)
    {
        row.Mark = reason;
        row.MarkedUtc = now;
        row.TryClock = now;
        row.MovedUtc = null;
        row.Waiting = false;
        Phases.Safe(info, UnitWatchLines.Stuck(row.EventId, row.Prefab, row.Wave, reason));
    }

    static void StillFrom(Row row, float x, float z, DateTime since)
    {
        row.StillX = x;
        row.StillZ = z;
        row.StillSince = since;
        row.StillNear = false;
        row.StillDone = false;
    }

    static void Fresh(Row row, WatchUnit u, DateTime now)
    {
        row.WindowStart = now;
        row.WindowX = u.X;
        row.WindowZ = u.Z;
    }

    void Count(Row row, bool recovered)
    {
        var key = (row.EventId, row.Instance);
        var (r, x) = _counts.TryGetValue(key, out var c) ? c : (0, 0);
        _counts[key] = recovered ? (r + 1, x) : (r, x + 1);
    }

    /// <summary>One check's state: the reads, the budgets and the first fault of each family.</summary>
    sealed class Check(UnitWatchPhase phase, DateTime now, IUnitWatchWorld w, WatchSettings s, Action<string> info)
    {
        readonly Dictionary<long, (ActiveEvent A, TrackedUnit T, WatchUnit U)> _reads = new();
        List<HuntCandidate> _online = [];
        IReadOnlyList<HuntCandidate> _players = [];
        WalkBudget _budget = null!;
        readonly JumpAllowance _moves = new(UnitWatch.MaxMoves);
        public string? ReadFault { get; private set; }
        public string? MoveFault { get; private set; }

        void ReadFailed(Exception e) => ReadFault ??= e.Message;
        void MoveFailed(Exception e) => MoveFault ??= e.Message;

        public void Run(IReadOnlyCollection<ActiveEvent> active)
        {
            List<TrackedUnit> tracked;
            try
            {
                tracked = w.Units.ToList();
                _players = w.Players();
            }
            catch (Exception e)
            {
                ReadFailed(e);
                return;
            }
            var keys = tracked.Select(t => t.Key).ToHashSet();
            foreach (var key in phase._rows.Keys.Where(k => !keys.Contains(k)).ToList()) phase._rows.Remove(key);
            phase.Fights.Keep(keys.Contains);
            _online = _players.Where(p => p.Online && PlayerPosition.Usable(p.X, p.Z)).ToList();
            _budget = new WalkBudget(UnitWatch.WalkCapCalls, w.Budget);

            foreach (var a in active)
                foreach (var t in tracked.Where(t => UnitWatch.Watched(t, a)).OrderBy(t => t.Key))
                {
                    WatchUnit? read;
                    try { read = w.Read(t); }
                    catch (Exception e)
                    {
                        ReadFailed(e);
                        continue;
                    }
                    if (read is not { } u) continue;
                    _reads[t.Key] = (a, t, u);
                    if (!phase._rows.TryGetValue(t.Key, out var row)) row = phase.NewRow(a, t, u, now);
                    if (UnitWatch.TargetClose(u)) row.LastFight = now;
                    if (phase.Fights.Last(t.Key) is { } hit && (row.LastFight is null || hit > row.LastFight)) row.LastFight = hit;
                }

            // the marked units first, oldest mark first (D6), then the rest, the longest unread ground first (S-11)
            foreach (var row in phase._rows.Values.Where(r => r.Mark is not null && !r.Removed && _reads.ContainsKey(r.Key))
                         .OrderBy(r => r.MarkedUtc).ThenBy(r => r.Key).ToList())
                Marked(row, _reads[row.Key]);
            foreach (var row in phase._rows.Values.Where(r => r.Mark is null && _reads.ContainsKey(r.Key))
                         .OrderBy(r => r.GroundRead ?? DateTime.MinValue).ThenBy(r => r.Key).ToList())
                Watch(row, _reads[row.Key].U);

            foreach (var a in active) Stall(a, tracked);
        }

        /// <summary>A unit trying to reach a player (A2, design §9 D56): only a unit holding a player as its aggro target can be
        /// stuck by standing or still; an idle unit near a player is not stuck, however near.</summary>
        bool TargetsPlayer(WatchUnit u) => u.TargetDistance is not null;

        bool Seen(WatchUnit u) => UnitWatch.Nearest(_online, u.X, u.Z) is { } d && d <= UnitWatch.WatchRange;

        /// <summary>The standing window (D2): a fight since its start, a move of more than 5 m (whoever moved the unit) or a
        /// moment with no player target starts it afresh.</summary>
        void Window(Row row, WatchUnit u)
        {
            if (row.LastFight is { } f && f >= row.WindowStart) Fresh(row, u, now);
            else if (HuntingGrounds.Distance(row.WindowX, row.WindowZ, u.X, u.Z) > UnitWatch.StandStill) Fresh(row, u, now);
            else if (!TargetsPlayer(u)) Fresh(row, u, now);
        }

        /// <summary>One off-ground reading (D3, S-12's fallback): true below WalkHeight.MinY, more than 10 m below the lowest
        /// grounded level at the unit's x and z, or with no grounded level there; null when there is no reading (a spent
        /// budget, a height outside the check's range, an unanswered or throwing read).</summary>
        bool? OffGround(Row row, WatchUnit u, out bool failed)
        {
            failed = false;
            if (u.Y < WalkHeight.MinY) return true;
            if (WalkHeight.Problem(u.Y) is not null || !_budget.TryTake(2)) return null;
            row.GroundRead = now;
            GroundReading? g;
            try { g = w.Ground(u.X, u.Z); }
            catch (Exception e)
            {
                ReadFailed(e);
                failed = true;
                return null;
            }
            if (g is not { } reading) return null;
            return reading.LowestY is not { } low || low - u.Y > UnitWatch.BelowGround;
        }

        /// <summary>Feeds the off-ground clock; true when two readings 10 s apart found the unit off the ground.</summary>
        bool OffGroundStuck(Row row, WatchUnit u, out bool failed)
        {
            var off = OffGround(row, u, out failed);
            if (failed) return false;
            var foughtLately = row.LastFight is { } f && now - f < s.Stuck;
            if (off == false || foughtLately)
            {
                row.OffSince = null;
                return false;
            }
            if (off != true) return false;
            row.OffSince ??= now;
            return now - row.OffSince.Value >= TimeSpan.FromSeconds(UnitWatch.OffGroundSeconds);
        }

        /// <summary>The still-at-spawn check (D34, A1): SpawnStillSeconds after the unit's spawn or its latest landing by one
        /// of our movers, a unit that moved no more than 0.5 m on x and z from that spot, did not fight, and held a player
        /// target at some check in that time (A2), is stuck at once. Decided once per spot; a newer
        /// landing starts it again from the landing.</summary>
        bool Still(Row row, WatchUnit u)
        {
            if (u.PutDown is { } p && p.At > row.StillSince) StillFrom(row, p.X, p.Z, p.At);
            if (row.StillDone) return false;
            if (HuntingGrounds.Distance(row.StillX, row.StillZ, u.X, u.Z) > UnitWatch.StillReach
                || row.LastFight is { } f && f >= row.StillSince)
            {
                row.StillDone = true;
                return false;
            }
            if (TargetsPlayer(u)) row.StillNear = true;
            if (now - row.StillSince < s.SpawnStill) return false;
            row.StillDone = true;
            return row.StillNear;
        }

        void Watch(Row row, WatchUnit u)
        {
            Window(row, u);
            if (OffGroundStuck(row, u, out var failed))
            {
                phase.Mark(row, StuckReason.OffGround, now, info);
                return;
            }
            if (failed) return;
            if (Still(row, u))
            {
                phase.Mark(row, StuckReason.Still, now, info);
                return;
            }
            if (now - row.WindowStart >= s.Stuck) phase.Mark(row, StuckReason.Standing, now, info);
        }

        void Marked(Row row, (ActiveEvent A, TrackedUnit T, WatchUnit U) r)
        {
            var u = r.U;
            if (row.LastFight is { } fought && fought >= row.MarkedUtc)
            {
                Clear(row, u, ClearReason.Fought);                        // a fight beats every mark, a removal's wait too (D6)
                return;
            }
            if (row.Removing)
            {
                Removal(row, u);
                return;
            }
            if (row.MovedUtc is { } moved)
            {
                if (row.LastFight is { } f && f >= moved)
                {
                    Clear(row, u, ClearReason.Fought);
                    return;
                }
                if (row.Mark is StuckReason.Standing or StuckReason.OffGround or StuckReason.Still)
                {
                    Window(row, u);
                    if (OffGroundStuck(row, u, out var failed))
                    {
                        Fail(row, u, TryFailure.StillStuck, counted: true);
                        return;
                    }
                    if (failed) return;
                    if (Still(row, u))
                    {
                        Fail(row, u, TryFailure.StillStuck, counted: true);   // still at its landing beside a player (D34)
                        return;
                    }
                    if (now - moved < s.Stuck) return;
                    if (row.WindowStart <= moved) Fail(row, u, TryFailure.StillStuck, counted: true);
                    else Clear(row, u, ClearReason.Moving);
                }
                else if (now - moved >= s.Stuck) Fail(row, u, TryFailure.StillStuck, counted: true);   // a stall or admin mark clears only by a fight
                return;
            }
            if (Seen(u))
            {
                if (now - row.TryClock >= s.Stuck)
                {
                    Fail(row, u, TryFailure.Seen, counted: false);
                    return;
                }
                if (!row.Waiting) Phases.Safe(info, UnitWatchLines.Waiting(row.EventId, row.Prefab));
                row.Waiting = true;
                return;
            }
            row.Waiting = false;
            if (_moves.Left == 0) return;                                 // S-11: the rest wait for the next check
            WatchMove? move;
            bool spent;
            try { (move, spent) = LandingRule.Choose(u, Context(r.A, r.T, u)); }
            catch (Exception e)
            {
                ReadFailed(e);                                            // that unit's try only, never the whole check (D18)
                Fail(row, u, TryFailure.NoLanding, counted: false);
                return;
            }
            if (spent) return;
            if (move is not { } m)
            {
                Fail(row, u, TryFailure.NoLanding, counted: false);
                return;
            }
            _moves.Take();
            try { w.Move(m); }
            catch (Exception e)
            {
                MoveFailed(e);
                Fail(row, u, TryFailure.NoLanding, counted: false);
                return;
            }
            row.Try++;
            phase.Count(row, recovered: true);
            Phases.Safe(info, UnitWatchLines.Recovered(row.EventId, row.Prefab, row.Try, m.Distance, m.Kind));
            row.MovedUtc = now;
            row.OffSince = null;
            StillFrom(row, m.X, m.Z, now);                                // D34: checked again from the landing
            row.WindowStart = now;
            row.WindowX = m.X;
            row.WindowZ = m.Z;
        }

        /// <summary>A failed try (D6): <paramref name="counted"/> when the try was already counted at its move; after the
        /// second, the removal starts.</summary>
        void Fail(Row row, WatchUnit u, TryFailure failure, bool counted)
        {
            if (!counted) row.Try++;
            Phases.Safe(info, UnitWatchLines.TryFailed(row.EventId, row.Prefab, row.Try, failure));
            row.MovedUtc = null;
            row.OffSince = null;
            row.TryClock = now;
            row.Waiting = false;
            if (row.Try < UnitWatch.Tries) return;
            row.Removing = true;
            row.RemoveSince = now;
            Removal(row, u);
        }

        /// <summary>The removal (D6-D8, S-10): waits at most StuckSeconds for no player within 100 m, then is queued on the
        /// staged despawn even in view. A throwing request is retried at the next check (Review 2 F6).</summary>
        void Removal(Row row, WatchUnit u)
        {
            var seen = Seen(u);
            if (seen && now - row.RemoveSince < s.Stuck)
            {
                if (!row.Waiting) Phases.Safe(info, UnitWatchLines.Waiting(row.EventId, row.Prefab));
                row.Waiting = true;
                return;
            }
            bool queued;
            try { queued = w.Despawn(row.Key); }
            catch (Exception e)
            {
                MoveFailed(e);
                return;
            }
            row.Removed = true;
            row.Waiting = false;
            if (!queued) return;                                          // already leaving the ledger
            phase.Count(row, recovered: false);
            Phases.Safe(info, UnitWatchLines.Removed(row.EventId, row.Prefab, row.Wave, seen));
        }

        void Clear(Row row, WatchUnit u, ClearReason reason)
        {
            Phases.Safe(info, UnitWatchLines.Cleared(row.EventId, row.Prefab, reason));
            row.Mark = null;
            row.Removing = false;
            row.MovedUtc = null;
            row.OffSince = null;
            row.Waiting = false;
            row.Try = 0;                                                  // cleared, it starts over with no tries (D6)
            Fresh(row, u, now);
        }

        LandingContext Context(ActiveEvent a, TrackedUnit t, WatchUnit u)
        {
            var action = a.Definition.Action;
            var scope = action?.Scope ?? a.Definition.Apocalypse?.Scope ?? Scope.Global;
            var allowTerritory = action?.AllowTerritory ?? false;
            var centre = Arena.Centre(a);
            var radius = action?.Arena?.Radius ?? 0;
            bool Allowed(float x, float z) =>
                (allowTerritory || !w.IsClaimed(a.Id, x, z))
                && (scope.IsGlobal || scope.Names(w.RegionOf(x, z)))
                && (centre is not { } c || Arena.Distance(c, x, z) <= radius);
            var focusOnly = RevengeRule.IsRevenge(a.Definition);
            var eligible = LandingRule.Eligible(_players, scope, w.RegionOf, focusOnly ? w.FocusKey(a) : null, focusOnly);
            var mates = _reads.Values
                .Where(o => o.A == a && o.T.Wave == t.Wave && o.U.Group == u.Group && o.T.Key != t.Key
                            && phase._rows.TryGetValue(o.T.Key, out var mr) && mr.Mark is null)
                .Select(o => o.U).ToList();
            return new LandingContext(_online, eligible, mates, Allowed, w.Walkable, w.HeightOf, _budget);
        }

        /// <summary>The stall backstop of one instance (D9): a watched wave with no death and no fight of its leftovers for
        /// StallMinutes marks every leftover not yet marked ("stall"); the clock restarts at a death, a fight and each firing.</summary>
        void Stall(ActiveEvent a, List<TrackedUnit> tracked)
        {
            int? risen = null;
            if (a.Definition.Apocalypse is not null)
            {
                try { risen = w.HordesRisen(a); }
                catch (Exception e)
                {
                    ReadFailed(e);
                    return;
                }
            }
            var waves = s.StallMinutes > 0 ? StallClock.Waves(a, risen).ToList() : [];
            foreach (var k in phase._clocks.Keys.Where(k => k.Event == a.Id && !waves.Contains(k.Wave)).ToList()) phase._clocks.Remove(k);
            foreach (var k in waves)
            {
                var ledgerWaves = StallClock.LedgerWaves(a, k);
                var leftovers = tracked.Where(t => UnitWatch.Watched(t, a) && ledgerWaves.Contains(t.Wave)).OrderBy(t => t.Key).ToList();
                if (leftovers.Count == 0)
                {
                    phase._clocks.Remove((a.Id, k));
                    continue;
                }
                if (!phase._clocks.TryGetValue((a.Id, k), out var clock))
                {
                    phase._clocks[(a.Id, k)] = new Clock(ledgerWaves, now);
                    continue;
                }
                if (leftovers.Any(t => phase._rows.TryGetValue(t.Key, out var r) && r.LastFight >= clock.Since)) clock.Since = now;
                if (now - clock.Since < TimeSpan.FromMinutes(s.StallMinutes)) continue;
                var marking = leftovers
                    .Select(t => phase._rows.TryGetValue(t.Key, out var r) ? r : phase.NewRow(a, t, _reads.TryGetValue(t.Key, out var o) ? o.U : null, now))
                    .Where(r => r.Mark is null && !r.Removed).ToList();
                Phases.Safe(info, UnitWatchLines.Stall(a.Id, k, s.StallMinutes, marking.Count));
                foreach (var r in marking) phase.Mark(r, StuckReason.Stall, now, info);
                clock.Since = now;
            }
        }
    }
}
