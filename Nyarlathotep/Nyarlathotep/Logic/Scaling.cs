#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nyarlathotep.Logic;

// Scaling (docs/dod/scaling.md, design §9 D57): an event whose SpawnWaves or Apocalypse action carries `scaling` counts the
// players before each wave, fanOut group, horde rise and roamer top-up (D2), adds unitsPerPlayer units a counted player
// beyond the first to each entry (D3-D5), and with levelFromPlayers gives each unit that is not a V Blood the players'
// average gear level plus the offset and the per-wave step (D6-D8). Everything here is pure: Services implement
// IScalingReader over PlayerQuery, User.IsAdmin, the character's Equipment and the prefab's VBloodUnit (step 3).

/// <summary>One player as <see cref="IScalingReader.Players"/> reads him for one decision (D2, D6): his x and z, whether he
/// is online, alive (not Dead, health above 0) and an admin, and his gear level, the sum of his Equipment's ArmorLevel,
/// SpellLevel and WeaponLevel, null when it could not be read. Held for the decision only, never logged or saved (D11).</summary>
public readonly record struct ScalingPlayer(float X, float Z, bool Online, bool Alive, bool IsAdmin, double? Gear);

/// <summary>The game side of scaling (step 3 implements it): every member may throw, which the service guards (D12).</summary>
public interface IScalingReader
{
    /// <summary>The players now, read afresh at every call; never cached between decisions (D2).</summary>
    IReadOnlyList<ScalingPlayer> Players();

    /// <summary>True when (x, z) lies in one of the regional <paramref name="scope"/>'s regions; never asked for Global.</summary>
    bool InScope(Scope scope, float x, float z);

    /// <summary>True when <paramref name="prefab"/> carries VBloodUnit (D8).</summary>
    bool IsVBlood(string prefab);
}

/// <summary>One decision's count (D2) and average gear level (D6), with the counted players for a fanOut wave's groups
/// (D4). Lives as long as the decision.</summary>
public sealed record ScalingRead(int Players, int? Gear, IReadOnlyList<ScalingPlayer> Counted)
{
    /// <summary>No counted player: base counts and configured levels (Design › States 7.1).</summary>
    public static readonly ScalingRead Nobody = new(0, null, []);
}

/// <summary>What `event info` shows of an instance's latest decision (D15): the count, the average and the level the line
/// names. Holds no player.</summary>
public readonly record struct ScalingView(int Players, int? Gear, int? Level);

/// <summary>A SpawnWaves wave's scaled entries (D3, D4, D10): the entries of group <c>i</c> (the single group's for a wave
/// without fanOut), the groups' counts for a fanOut wave, the decision's line (null for an unscaled event) and the view
/// `event info` keeps (D15).</summary>
public sealed record ScaledWave(Func<int, IReadOnlyList<UnitEntry>> Entries, IReadOnlyList<int> Groups, string? Line, ScalingView? View);

/// <summary>What an apocalypse decision reads under scaling (D2, D5, D10, D15): <see cref="Read"/>, called afresh by each
/// horde rise and each roamer top-up (null when the read failed: the decision goes on unscaled, D12), and the book that
/// keeps the latest decision and the roamers' last logged count.</summary>
public sealed record ScalingSource(Func<ScalingRead?> Read, ScalingBook Book);

public sealed partial record Scaling
{
    /// <summary>The highest level a unit takes (LevelArg's range, design §9 D57).</summary>
    public const int MinLevel = 1, MaxLevel = 120;

    /// <summary>The most extra units one decision asks for: far above any cap (MaxUnitsPerWave, MaxTrackedUnits), so the
    /// caps still decide, and low enough that no count built on it wraps.</summary>
    public const int MaxExtra = 100_000;

    /// <summary>The units each entry adds for <paramref name="players"/> counted players: u × max(0, n − 1) (D3, D5),
    /// saturating at <see cref="MaxExtra"/> (never negative).</summary>
    public int Extra(int players) => (int)Math.Clamp((long)Math.Max(0, UnitsPerPlayer) * Math.Max(0L, players - 1L), 0L, MaxExtra);

    /// <summary>The counted players (D2, S-9): online, alive, at a usable position, inside a regional scope (every such
    /// player for Global), an admin only with <paramref name="includeAdmins"/>; claimed territory and PvP combat do not
    /// matter.</summary>
    public static IReadOnlyList<ScalingPlayer> Count(IReadOnlyList<ScalingPlayer> players, Scope scope, Func<float, float, bool> inScope, bool includeAdmins) =>
        players.Where(p => p.Online && p.Alive && PlayerPosition.Usable(p.X, p.Z) && (includeAdmins || !p.IsAdmin)
                           && (scope.IsGlobal || inScope(p.X, p.Z))).ToList();

    /// <summary>The average gear level (D6, S-6): each readable level rounded down, their mean rounded half up; null when
    /// none is readable.</summary>
    public static int? Average(IEnumerable<double?> gear)
    {
        var levels = gear.Where(g => g is { } v && double.IsFinite(v)).Select(g => (long)Math.Floor(g!.Value)).ToList();
        if (levels.Count == 0) return null;
        return (int)Math.Floor((2.0 * levels.Sum() + levels.Count) / (2.0 * levels.Count));
    }

    /// <summary>One decision's read (D2, D6): the players read afresh, counted, and averaged. Throws when the reader throws.</summary>
    public static ScalingRead Read(IScalingReader reader, Scope scope, bool includeAdmins)
    {
        var counted = Count(reader.Players(), scope, (x, z) => reader.InScope(scope, x, z), includeAdmins);
        return new ScalingRead(counted.Count, Average(counted.Select(p => p.Gear)), counted);
    }

    /// <summary><see cref="Read"/>, or null with <paramref name="error"/> when the read throws: the decision then goes on
    /// unscaled (D2, D12).</summary>
    public static ScalingRead? TryRead(IScalingReader reader, Scope scope, bool includeAdmins, out string? error)
    {
        error = null;
        try { return Read(reader, scope, includeAdmins); }
        catch (Exception e) { error = e.Message; return null; }
    }

    /// <summary>Each entry with <paramref name="extra"/> more copies, each copy still rolled against its chance (D3).</summary>
    public static IReadOnlyList<UnitEntry> ScaleEntries(IReadOnlyList<UnitEntry> entries, int extra) =>
        extra <= 0 ? entries : entries.Select(e => e with { Count = (int)Math.Min(int.MaxValue, (long)e.Count + Math.Min(extra, MaxExtra)) }).ToList();

    /// <summary>Per fanOut group, the counted players within <paramref name="minSpacing"/> m (x and z, as PlayerPick's
    /// spacing) of the player its centre was picked for, that player included when he is counted (D4, S-10).</summary>
    public static int[] ScaleGroups(IReadOnlyList<(float X, float Z)> origins, IReadOnlyList<ScalingPlayer> counted, int minSpacing) =>
        origins.Select(o => counted.Count(p => Planar(o.X, o.Z, p.X, p.Z) <= minSpacing)).ToArray();

    static double Planar(float ax, float az, float bx, float bz) => Math.Sqrt((ax - bx) * (double)(ax - bx) + (az - bz) * (double)(az - bz));

    /// <summary>A horde's extra regular units: u × max(0, n − 1) for the whole horde, not per pool entry (D5; Review 1 F1).</summary>
    public int HordeExtra(int players) => Extra(players);

    /// <summary>A roamer top-up's targets (D5): perHotspot + extra, and roamers.max + extra × the hotspot count, never past
    /// ApocalypseLimits.MaxRoamers (the free tracked slots are RoamerPlan.TopUp's).</summary>
    public static (int PerHotspot, int Max) RoamerTargets(ApocalypseRoamers roamers, int hotspots, int extra) =>
        extra <= 0 ? (roamers.PerHotspot, roamers.Max)
        : ((int)Math.Min(int.MaxValue, (long)roamers.PerHotspot + Math.Min(extra, MaxExtra)),
           (int)Math.Min((long)roamers.Max + (long)Math.Min(extra, MaxExtra) * Math.Max(1, hotspots), Math.Max(roamers.Max, ApocalypseLimits.MaxRoamers)));

    /// <summary>The scaled level (D7) of a unit with <paramref name="modifiers"/> in wave (or horde) <paramref name="wave"/>,
    /// 1 for a roamer: clamp(A + levelOffset + levelDelta + (w − 1) × levelPerWave, 1, 120); null when levelFromPlayers is
    /// false, there is no average, or the modifiers set an absolute level (S-7), so the configured tuning stays.</summary>
    public int? Level(int? average, SpawnModifiers? modifiers, int wave)
    {
        if (!LevelFromPlayers || average is not { } a || modifiers?.Level is not null) return null;
        return (int)Math.Clamp((long)a + LevelOffset + (modifiers?.LevelDelta ?? 0) + (Math.Max(1L, wave) - 1L) * LevelPerWave, MinLevel, MaxLevel);
    }

    /// <summary>The level a decision's line and `event info` name (D10, D15): <see cref="Level"/> without an entry's delta.</summary>
    public int? LineLevel(int? average, int wave) => Level(average, null, wave);

    /// <summary>A unit's tuning under scaling (D7): <paramref name="configured"/> with the scaled level in place of its own,
    /// the own one kept for a V Blood (D8); <paramref name="configured"/> itself when nothing scales the level.</summary>
    public static UnitTuning Tune(UnitTuning configured, Scaling? block, int? average, SpawnModifiers? modifiers, int wave) =>
        block?.Level(average, modifiers, wave) is { } level
            ? configured with { Level = new LevelArg(false, level), Own = configured.Level, Scaled = true }
            : configured;

    /// <summary>The level a unit takes at its setup (D8, D12): a scaled tuning's level unless the unit is a V Blood, which
    /// keeps its own (the prefab's, or its entry's level or levelDelta); a throwing V Blood read keeps the own level too.</summary>
    public static LevelArg? LevelAtSetup(UnitTuning tuning, Func<bool> isVBlood)
    {
        if (!tuning.Scaled) return tuning.Level;
        try { return isVBlood() ? tuning.Own : tuning.Level; }
        catch (Exception) { return tuning.Own; }
    }

    /// <summary>Wave <paramref name="wave"/> of <paramref name="action"/> under scaling (D3, D4, D10): its entries (bosses
    /// never multiplied), per fanOut group when <paramref name="origins"/> names the groups' players, and the decision's line
    /// and view; an action without the block, or a failed read (null), rolls exactly as before and logs nothing.</summary>
    public static ScaledWave ForWave(SpawnWavesAction action, string eventId, int wave, ScalingRead? read, IReadOnlyList<(float X, float Z)>? origins = null)
    {
        if (action.Scaling is not { } block || read is null) return new(_ => BossWave.Units(action, wave), [], null, null);
        var view = new ScalingView(read.Players, read.Gear, block.LineLevel(read.Gear, wave));
        var baseCount = BossWave.Units(action, wave).Sum(e => e.Count);
        if (action.FanOut is { } fan && origins is { Count: > 0 })
        {
            var groups = ScaleGroups(origins, read.Counted, fan.MinSpacing);
            IReadOnlyList<UnitEntry> Group(int i) => BossWave.Units(action, wave, block.Extra(i >= 0 && i < groups.Length ? groups[i] : 0));
            var scaled = groups.Select((_, i) => Group(i).Sum(e => e.Count)).Sum();
            return new(Group, groups, Lines.Wave(eventId, wave, view, baseCount * groups.Length, scaled, groups), view);
        }
        var entries = BossWave.Units(action, wave, block.Extra(read.Players));
        return new(_ => entries, [], Lines.Wave(eventId, wave, view, baseCount, entries.Sum(e => e.Count)), view);
    }

    /// <summary>The decision lines (D10) and `event info` lines (D15); the service adds "[nyar] " to the log lines. Counts and
    /// the average only: never a player's name, platform id, position or own gear level (D11).</summary>
    public static class Lines
    {
        static string Gear(int? gear) => gear?.ToString(CultureInfo.InvariantCulture) ?? "none";
        static string Lvl(int? level) => level?.ToString(CultureInfo.InvariantCulture) ?? "own";
        static string Signed(int n) => n < 0 ? n.ToString(CultureInfo.InvariantCulture) : "+" + n.ToString(CultureInfo.InvariantCulture);

        /// <summary>"scaling &lt;event&gt; wave &lt;k&gt;: players &lt;n&gt;, gear &lt;A|none&gt;, level &lt;L|own&gt;, units
        /// &lt;base&gt; -&gt; &lt;scaled&gt;", a fanOut wave adding ", groups &lt;c1&gt;/&lt;c2&gt;/…".</summary>
        public static string Wave(string eventId, int wave, ScalingView v, int before, int after, IReadOnlyList<int>? groups = null) =>
            $"scaling {eventId} wave {wave}: {Head(v)}, units {before} -> {after}" + (groups is { Count: > 0 } ? $", groups {string.Join("/", groups)}" : "");

        /// <summary>"scaling &lt;event&gt; horde &lt;k&gt;: players &lt;n&gt;, gear …, level …, units &lt;base&gt; -&gt; &lt;scaled&gt;".</summary>
        public static string Horde(string eventId, int horde, ScalingView v, int before, int after) =>
            $"scaling {eventId} horde {horde}: {Head(v)}, units {before} -> {after}";

        /// <summary>"scaling &lt;event&gt; roamers: players &lt;n&gt;, per hotspot &lt;p&gt;, max &lt;m&gt;".</summary>
        public static string Roamers(string eventId, int players, int perHotspot, int max) =>
            $"scaling {eventId} roamers: players {players}, per hotspot {perHotspot}, max {max}";

        static string Head(ScalingView v) => $"players {v.Players}, gear {Gear(v.Gear)}, level {Lvl(v.Level)}";

        /// <summary>`event info`'s definition line: "scaling: +&lt;u&gt; units a player beyond the first, level &lt;players'
        /// gear +&lt;o&gt;, +&lt;p&gt; a wave|own&gt;".</summary>
        public static string Info(Scaling s) =>
            $"scaling: +{s.UnitsPerPlayer} units a player beyond the first, level " +
            (s.LevelFromPlayers ? $"players' gear {Signed(s.LevelOffset)}, +{s.LevelPerWave} a wave" : "own");

        /// <summary>`event info`'s running line: "scaling now: &lt;n&gt; players, gear &lt;A|none&gt;, level &lt;L|own&gt;".</summary>
        public static string Now(ScalingView v) => $"scaling now: {v.Players} players, gear {Gear(v.Gear)}, level {Lvl(v.Level)}";
    }
}

/// <summary>The latest scaling decision of each running instance (D15) and the roamer count each last logged (D10), keyed
/// by event and instance start, so a new instance starts over. Memory only: dropped at every end path
/// (<see cref="EventEnded"/>) and on a restart (<see cref="Clear"/>); never saved (D11).</summary>
public sealed class ScalingBook
{
    readonly Dictionary<(string Event, DateTime Started), ScalingView> _latest = [];
    readonly Dictionary<(string Event, DateTime Started), int> _roamers = [];

    static (string, DateTime) Key(ActiveEvent a) => (a.Id, a.Instance.StartedUtc);

    /// <summary>Keeps <paramref name="view"/> as <paramref name="active"/>'s latest decision.</summary>
    public void Record(ActiveEvent active, ScalingView view) => _latest[Key(active)] = view;

    /// <summary>The latest decision of the running instance <paramref name="active"/>, or null (not running, none yet, or
    /// an earlier instance's).</summary>
    public ScalingView? Latest(ActiveEvent? active) => active is not null && _latest.TryGetValue(Key(active), out var v) ? v : null;

    /// <summary>True when a top-up of <paramref name="active"/> counts <paramref name="players"/> for the first time or a
    /// number other than the last logged one (D10), which it then keeps.</summary>
    public bool RoamersChanged(ActiveEvent active, int players)
    {
        var key = Key(active);
        if (_roamers.TryGetValue(key, out var last) && last == players) return false;
        _roamers[key] = players;
        return true;
    }

    /// <summary>Drops every row of <paramref name="eventId"/>: each of D15's end paths calls it.</summary>
    public void EventEnded(string eventId)
    {
        foreach (var k in _latest.Keys.Where(k => k.Event == eventId).ToList()) _latest.Remove(k);
        foreach (var k in _roamers.Keys.Where(k => k.Event == eventId).ToList()) _roamers.Remove(k);
    }

    /// <summary>Drops everything (a restart).</summary>
    public void Clear()
    {
        _latest.Clear();
        _roamers.Clear();
    }

    /// <summary>The rows held, for the end-path tests.</summary>
    public int Count => _latest.Count + _roamers.Count;
}
