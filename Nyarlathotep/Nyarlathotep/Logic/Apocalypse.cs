#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Nyarlathotep.Logic;

// The apocalypse's pure half (docs/dod/apocalypse.md D2-D15, D36): hotspots among a faction's natives, roamers that hunt
// map-wide and spread on kills, hordes that rise, grow and march on a goal, the outcome, the end paths, the resume record's
// rules, the rolling unit lifetime and the run's unit tally. Services/ApocalypseAction (step 3) reads the game and drives
// it on the server main thread, so nothing here locks; every unit goes through SpawnLedger (D14).

/// <summary>The apocalypse's fixed numbers (apocalypse D2-D13).</summary>
public static class Apocalypse
{
    /// <summary>Natives are grouped into square cells of this side, in metres (D2).</summary>
    public const float CellSize = 100f;
    /// <summary>A roamer spawns within this many metres of its hotspot (D3).</summary>
    public const float RoamerSpawnRadius = 15f;
    /// <summary>A spread roamer spawns within this many metres of its victim (D4).</summary>
    public const float SpreadRadius = 3f;
    /// <summary>The spread's window: at most perMinute spread units queued in any 60 s (D4).</summary>
    public static readonly TimeSpan SpreadWindow = TimeSpan.FromSeconds(60);
    /// <summary>A front holds while a player stands within this many metres of it (D6).</summary>
    public const float HoldRange = 60f;
    /// <summary>No hold applies within goal.radius + this many metres of the goal (D6).</summary>
    public const float GoalHoldMargin = 60f;
    /// <summary>Lost needs a living unit within goal.radius + this many metres of the goal (D8).</summary>
    public const float LostMargin = 30f;
    /// <summary>A unit keeps up with its front within this many metres of it (D6).</summary>
    public const float KeepUpRange = 30f;
    /// <summary>The share of a horde's living units that keeps up with its front while unobserved (D6).</summary>
    public const double KeepUpShare = 0.8;

    /// <summary>How far from the front a unit stands before a hop moves it (D6's mechanism): half the keep-up range, so a
    /// unit the game leaves standing still (no player near) is re-hopped long before it falls 30 m back (session 1: 4-6 of 8
    /// kept up when units were re-hopped only past 30 m).</summary>
    public const float HopGap = 15f;

    /// <summary>How far ahead an unobserved front looks for walkable ground before it holds (D6, A12), in steps of
    /// <see cref="CrossStep"/> m: a straight line to the goal crosses rivers and cliffs.</summary>
    public const float CrossReach = 120f;
    public const float CrossStep = 15f;
    /// <summary>A horde is unobserved after no player stood within <see cref="WatchRange"/> m of it for this long (D6).</summary>
    public static readonly TimeSpan Unobserved = TimeSpan.FromSeconds(10);
    public const float WatchRange = 100f;
    /// <summary>A horde that queued no unit is tried again after this long (D5).</summary>
    public static readonly TimeSpan RiseRetry = TimeSpan.FromSeconds(60);
    /// <summary>The march's announced steps, in percent (D7); 100 % is the loss (D8), announced by its outcome line.</summary>
    public static readonly IReadOnlyList<int> ProgressSteps = [25, 50, 75];
    /// <summary>The record is written each 30 s of a march and each 60 s otherwise, at most once a second (D11).</summary>
    public static readonly TimeSpan RecordMarching = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RecordQuiet = TimeSpan.FromSeconds(60);
    /// <summary>The resume waits at most this long for the boot sweep's despawns (D12).</summary>
    public static readonly TimeSpan DrainWait = TimeSpan.FromMinutes(5);
    /// <summary>A living unit's due time and LifeTime are renewed when less than this is left (D13).</summary>
    public static readonly TimeSpan RenewBelow = TimeSpan.FromMinutes(90);
    /// <summary>The ledger wave of a horde's regular units is its number; a roamer's is this plus its hotspot's number; a
    /// boss's is <see cref="BossWaveBase"/> + horde × 10 + its `bosses` entry's number (D11, D36).</summary>
    public const int RoamerWaveBase = 100;
    public const int BossWaveBase = 200;
    /// <summary>A horde march's growth units (undead-hordes D10) are ledger wave this plus the horde's number: horde units,
    /// counted apart so the growth block's max and the record read them.</summary>
    public const int GrowthWaveBase = 50;
    /// <summary>The most hotspots or origins a run holds (an infestation's 10, undead-hordes D8), and the most boss slots a
    /// horde holds (a horde march's resolved bosses, D10; an apocalypse's 3 entries).</summary>
    public const int MaxSpots = HordeLimits.MaxOrigins;
    public const int MaxBossSlots = EventValidator.MaxWaveBosses;

    public static int RoamerWave(int hotspot) => RoamerWaveBase + hotspot;
    public static int HordeWave(int horde) => horde;
    public static int GrowthWave(int horde) => GrowthWaveBase + horde;
    public static int BossWave(int horde, int entry) => BossWaveBase + horde * 10 + entry;

    /// <summary>The hotspot (1-based) a roamer's ledger wave names, or null.</summary>
    public static int? HotspotOfWave(int wave) => wave is > RoamerWaveBase and <= RoamerWaveBase + MaxSpots ? wave - RoamerWaveBase : null;

    /// <summary>The horde (1-based) a horde unit's, growth unit's or boss's ledger wave names, or null for a roamer.</summary>
    public static int? HordeOfWave(int wave) =>
        wave is >= 1 and <= ApocalypseLimits.MaxHordes ? wave
        : wave is > GrowthWaveBase and <= GrowthWaveBase + ApocalypseLimits.MaxHordes ? wave - GrowthWaveBase
        : wave > BossWaveBase ? (wave - BossWaveBase) / 10 is var k and >= 1 and <= ApocalypseLimits.MaxHordes ? k : null
        : null;

    /// <summary>The `bosses` entry or boss slot (1-based) a boss's ledger wave names, or null.</summary>
    public static int? BossEntryOfWave(int wave) => wave > BossWaveBase && (wave - BossWaveBase) % 10 is var j and >= 1 and <= MaxBossSlots ? j : null;

    /// <summary>The goal fixed at the start (D11, S-10): a Point goal's coordinates (height 0 when none is stored), or an
    /// Admin goal's starting admin position, read once; null for an Admin goal without one (the start is refused).</summary>
    public static (float X, float Y, float Z)? ResolveGoal(ApocalypseGoal goal, (float X, float Y, float Z)? origin) =>
        goal.Location.Type == LocationType.Admin ? origin : (goal.Location.X, goal.Location.Y ?? 0f, goal.Location.Z);

    /// <summary>A horde march's Point or Admin start (undead-hordes D10), resolved as the goal is; null for a Hotspot start.</summary>
    public static (float X, float Z)? ResolveStart(HordeStart start, (float X, float Y, float Z)? origin) =>
        start.Location is not { } l ? null : l.Type == LocationType.Admin ? origin is { } o ? (o.X, o.Z) : null : (l.X, l.Z);

    public static float Distance(float ax, float az, float bx, float bz) => HuntingGrounds.Distance(ax, az, bx, bz);

    /// <summary>The faction's short name in the lines (Faction_Undead → Undead).</summary>
    public static string FactionText(string faction) => FactionDenyList.ShortName(faction);

    /// <summary>A unit of the running apocalypse <paramref name="a"/>: its event's, spawned since this instance started.</summary>
    public static bool Ours(TrackedUnit u, ActiveEvent a) => u.EventId == a.Id && u.SpawnedUtc >= a.Instance.StartedUtc;
}

/// <summary>One native NPC as the hotspot reader sees it (D2): its eligibility facts and its x and z.</summary>
public readonly record struct HotspotNative(UnitFacts Facts, float X, float Z);

/// <summary>A hotspot (D2): its number (1-based), the mean x and z of its cell's natives, the native count and its cell.</summary>
public sealed record Hotspot(int Number, float X, float Z, int Natives, int CellX, int CellZ);

/// <summary>What <see cref="Hotspots.Pick"/> found: the hotspots, or the refusal of the start; and the line to log.</summary>
public sealed record HotspotPick(IReadOnlyList<Hotspot> Spots, string? Refusal, string Line);

/// <summary>
/// The hotspots of a run (D2, Business rules 2): the faction's eligible natives (Logic/Empowerment.cs Eligibility: not a
/// V Blood, not a prisoner or non-combatant, not a unit of ours, the faction's, inside the action's scope) are grouped into
/// 100 m cells; cells with at least minNatives are ranked by count (ties by cell x, then cell z), and up to `count` are taken
/// at least minSpacing apart, each at the mean position of its cell's natives. Found once per run. The reader only reads:
/// the span between the native-read markers is scanned by <see cref="NativeScan"/> for any entity write.
/// </summary>
public static class Hotspots
{
    /// <summary>"no hotspot: fewer than &lt;n&gt; &lt;faction&gt; natives in one place in scope" (D2).</summary>
    public static string NoHotspot(int minNatives, string faction) =>
        $"no hotspot: fewer than {minNatives} {Apocalypse.FactionText(faction)} natives in one place in scope";

    /// <summary>"no hotspot: native read failed: &lt;message&gt;" (D21).</summary>
    public static string ReadFailed(string message) => $"no hotspot: native read failed: {message}";

    /// <summary>An infestation with no origin (undead-hordes D8): "no &lt;faction&gt; natives found".</summary>
    public static string NoNatives(string faction) => $"no {Apocalypse.FactionText(faction)} natives found";

    /// <summary>A horde march's Hotspot start with none far enough (undead-hordes D10): "no &lt;faction&gt; natives found at
    /// least &lt;minDistance&gt; m away".</summary>
    public static string NoStart(string faction, int minDistance) => $"no {Apocalypse.FactionText(faction)} natives found at least {minDistance} m away";

    // <native-read>
    /// <summary>The eligible natives of <paramref name="action"/> by 100 m cell; a throwing read gives its refusal.</summary>
    static Dictionary<(int X, int Z), List<(float X, float Z)>>? Cells(string eventId, ApocalypseAction action, Func<IEnumerable<HotspotNative>> read,
        out HotspotPick? failed)
    {
        failed = null;
        var rule = new EmpowerAction([action.Faction], [], [], false, new EmpowerStats(), action.Scope);
        var cells = new Dictionary<(int X, int Z), List<(float X, float Z)>>();
        try
        {
            foreach (var n in read())
            {
                if (!Eligibility.Decide(n.Facts with { CarrierOf = null }, rule).Apply) continue;
                var cell = ((int)MathF.Floor(n.X / Apocalypse.CellSize), (int)MathF.Floor(n.Z / Apocalypse.CellSize));
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = [];
                list.Add((n.X, n.Z));
            }
        }
        catch (Exception e)
        {
            var reason = ReadFailed(e.Message);
            failed = new HotspotPick([], reason, $"apocalypse {eventId}: {reason}");
            return null;
        }
        return cells;
    }

    /// <summary>The hotspots of <paramref name="action"/> over the natives <paramref name="read"/> returns. A throwing read
    /// refuses the start with <see cref="ReadFailed"/>; fewer than one hotspot with <see cref="NoHotspot"/> (an
    /// infestation's origins with <see cref="NoNatives"/>).</summary>
    public static HotspotPick Pick(string eventId, ApocalypseAction action, Func<IEnumerable<HotspotNative>> read)
    {
        if (Cells(eventId, action, read, out var failed) is not { } cells) return failed!;
        var ranked = cells.Where(c => c.Value.Count >= action.Hotspots.MinNatives)
            .OrderByDescending(c => c.Value.Count).ThenBy(c => c.Key.X).ThenBy(c => c.Key.Z);
        var spots = new List<Hotspot>();
        foreach (var (cell, natives) in ranked)
        {
            if (spots.Count >= action.Hotspots.Count) break;
            var x = (float)natives.Average(p => (double)p.X);
            var z = (float)natives.Average(p => (double)p.Z);
            if (spots.Any(s => Apocalypse.Distance(s.X, s.Z, x, z) < action.Hotspots.MinSpacing)) continue;
            spots.Add(new Hotspot(spots.Count + 1, x, z, natives.Count, cell.X, cell.Z));
        }
        if (spots.Count == 0)
        {
            var none = action.Mode == ApocalypseMode.Infestation ? NoNatives(action.Faction) : NoHotspot(action.Hotspots.MinNatives, action.Faction);
            return new HotspotPick([], none, $"apocalypse {eventId}: {none}");
        }
        return new HotspotPick(spots, null, Line(eventId, spots));
    }

    /// <summary>A horde march's Hotspot start (undead-hordes D10): of the cells holding at least minNatives eligible natives,
    /// the one whose natives' mean lies nearest <paramref name="destination"/> at least start.minDistance m from it (ties by
    /// cell x, then z); none refuses the start with <see cref="NoStart"/>.</summary>
    public static HotspotPick PickStart(string eventId, ApocalypseAction action, (float X, float Z) destination, Func<IEnumerable<HotspotNative>> read)
    {
        if (Cells(eventId, action, read, out var failed) is not { } cells) return failed!;
        var min = action.March?.Start.MinDistance ?? 0;
        var best = cells.Where(c => c.Value.Count >= action.Hotspots.MinNatives)
            .Select(c => (Cell: c.Key, X: (float)c.Value.Average(p => (double)p.X), Z: (float)c.Value.Average(p => (double)p.Z), Natives: c.Value.Count))
            .Select(c => (c.Cell, c.X, c.Z, c.Natives, Far: Apocalypse.Distance(c.X, c.Z, destination.X, destination.Z)))
            .Where(c => c.Far >= min)
            .OrderBy(c => c.Far).ThenBy(c => c.Cell.X).ThenBy(c => c.Cell.Z)
            .Select(c => new Hotspot(1, c.X, c.Z, c.Natives, c.Cell.X, c.Cell.Z)).FirstOrDefault();
        if (best is null)
        {
            var none = NoStart(action.Faction, min);
            return new HotspotPick([], none, $"apocalypse {eventId}: {none}");
        }
        return new HotspotPick([best], null, Line(eventId, [best]));
    }
    // </native-read>

    /// <summary>"apocalypse &lt;id&gt; hotspots: &lt;n&gt; (&lt;natives&gt; at &lt;x&gt; &lt;z&gt;, …)" (D25): map locations only.</summary>
    public static string Line(string eventId, IReadOnlyList<Hotspot> spots) =>
        $"apocalypse {eventId} hotspots: {spots.Count} (" +
        string.Join(", ", spots.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Natives} at {s.X:0} {s.Z:0}"))) + ")";
}

/// <summary>
/// The native scan (D2): the code between "// &lt;native-read&gt;" and "// &lt;/native-read&gt;" in every source file must
/// call no EntityExtensions write, add, remove or destroy, so the hotspot read never writes, buffs, moves or destroys a
/// native. An empty set of marked files is a failure ("native scan: 0 files"), never a pass.
/// </summary>
public static class NativeScan
{
    public const string Open = "// <native-read>";
    public const string Close = "// </native-read>";
    public const string NoFiles = "native scan: 0 files";

    static readonly Regex Write = new(@"\.\s*Write\s*[(<]|AddComponentSafe|RemoveComponentSafe|AddBufferSafe|DestroySafe|RemoveBuffSafe|SetComponentData|DestroyEntity",
        RegexOptions.CultureInvariant);

    /// <summary>The problems of <paramref name="files"/> (path, text): each write call inside a marked span, an unclosed
    /// span, or <see cref="NoFiles"/> when no file has a span. Empty when the reader only reads.</summary>
    public static List<string> Problems(IEnumerable<(string Path, string Text)> files)
    {
        var problems = new List<string>();
        var marked = 0;
        foreach (var (path, text) in files)
        {
            var at = text.IndexOf(Open, StringComparison.Ordinal);
            if (at < 0) continue;
            marked++;
            while (at >= 0)
            {
                var end = text.IndexOf(Close, at, StringComparison.Ordinal);
                if (end < 0) { problems.Add($"{path}: {Open} is not closed"); break; }
                var span = text[at..end];
                foreach (Match m in Write.Matches(span)) problems.Add($"{path}: native read writes ({m.Value.Trim()})");
                at = text.IndexOf(Open, end, StringComparison.Ordinal);
            }
        }
        if (marked == 0) problems.Add(NoFiles);
        return problems;
    }
}

/// <summary>
/// The roamers' spawns (D3, D4, Business rules 3, 4): at the start and at each horde rise every hotspot is topped up to
/// perHotspot living roamers, never past roamers.max alive nor the free MaxTrackedUnits slots, hotspot 1 first; a spread
/// roamer counts towards the hotspot nearest its spawn point. Each unit is drawn from `units` by weight (count × chance).
/// </summary>
public static class RoamerPlan
{
    /// <summary>How many roamers each hotspot gets now: perHotspot − its living roamers, within roamers.max − the living
    /// total and the free tracked slots, in hotspot order.</summary>
    public static int[] TopUp(int perHotspot, IReadOnlyList<int> livingPerHotspot, int max, int freeTracked)
    {
        var plan = new int[livingPerHotspot.Count];
        var room = Math.Min(max - livingPerHotspot.Sum(), freeTracked);
        for (var h = 0; h < plan.Length && room > 0; h++)
        {
            var n = Math.Min(Math.Max(0, perHotspot - livingPerHotspot[h]), room);
            plan[h] = n;
            room -= n;
        }
        return plan;
    }

    /// <summary>The hotspot (1-based) nearest (x, z); ties go to the lower number (D4).</summary>
    public static int NearestHotspot(IReadOnlyList<Hotspot> hotspots, float x, float z)
    {
        var best = hotspots[0];
        foreach (var h in hotspots)
            if (Apocalypse.Distance(h.X, h.Z, x, z) < Apocalypse.Distance(best.X, best.Z, x, z)) best = h;
        return best.Number;
    }

    /// <summary>One unit of <paramref name="action"/> (undead-hordes D7): an entry of its units by weight count × chance; a
    /// pool entry then names one member of its pool, drawn uniformly, with the entry's chance and modifiers.</summary>
    public static UnitEntry Draw(ApocalypseAction action, IRandom rng) => Member(action, Draw(action.Units, rng), rng);

    /// <summary>One spread unit (undead-hordes D7): a member of roamers.pool drawn uniformly, with the modifiers of the first
    /// units entry naming that pool; without a pool, <see cref="Draw(ApocalypseAction, IRandom)"/>.</summary>
    public static UnitEntry Spread(ApocalypseAction action, IRandom rng) => Spread(action, action.Roamers, rng);

    /// <summary><see cref="Spread(ApocalypseAction, IRandom)"/> from the spread block <paramref name="block"/> (a horde
    /// march's growth or infest spread, undead-hordes D10).</summary>
    public static UnitEntry Spread(ApocalypseAction action, ApocalypseRoamers block, IRandom rng)
    {
        if (block.Pool is not { } pool) return Draw(action, rng);
        var named = action.Units.FirstOrDefault(u => u.Pool == pool);
        return Member(action, UnitEntry.FromPool(pool, 1, 1.0, named?.Modifiers), rng);
    }

    // validation gives every pool at least one member and every pool entry a pool the action defines
    static UnitEntry Member(ApocalypseAction action, UnitEntry e, IRandom rng)
    {
        if (e.Pool is not { } pool) return e;
        var members = action.Pools![pool];
        return new UnitEntry(members[Pools.Index(rng, members.Count)], 1, e.Chance, e.Modifiers);
    }

    /// <summary>One draw from <paramref name="units"/> by weight count × chance.</summary>
    public static UnitEntry Draw(IReadOnlyList<UnitEntry> units, IRandom rng)
    {
        var total = units.Sum(u => u.Count * u.Chance);
        var roll = rng.NextDouble() * total;
        foreach (var u in units)
        {
            roll -= u.Count * u.Chance;
            if (roll < 0) return u;
        }
        return units[^1];
    }

    /// <summary>"apocalypse &lt;id&gt; roamers: +&lt;k&gt; at hotspot &lt;h&gt;" (12.2).</summary>
    public static string Line(string eventId, int queued, int hotspot) => $"apocalypse {eventId} roamers: +{queued} at hotspot {hotspot}";
}

/// <summary>
/// Map-wide roaming (D3, A2; design §9 D49): each hotspot's roamers go after the eligible player nearest the mean position
/// of those roamers, anywhere on the map (online, alive, a usable position, not in claimed territory, not in PvP combat;
/// ties by key), with no scope or region bound. They move by hunting-grounds' JumpRule over our own units only, a landing
/// allowed anywhere outside claimed territory; within the seed reach of that player aggro is seeded and Hunt takes over.
/// With no eligible player they stay where they stand.
/// </summary>
public static class RoamRule
{
    public static bool Eligible(HuntCandidate p) =>
        p.Online && p.Alive && !p.InTerritory && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Z);

    /// <summary>The target of a group at <paramref name="mean"/>: the nearest eligible player anywhere, ties by key; null
    /// with none.</summary>
    public static HuntCandidate? Target(IEnumerable<HuntCandidate> players, (float X, float Z) mean) =>
        players.Where(Eligible).OrderBy(p => Apocalypse.Distance(p.X, p.Z, mean.X, mean.Z)).ThenBy(p => p.Key)
            .Select(p => (HuntCandidate?)p).FirstOrDefault();

    /// <summary>One roamer's move toward <paramref name="target"/>: JumpRule's plan (a hidden jump only while no online
    /// player stands within 100 m, landing 50 m or more from every online player and on walkable ground) with landings
    /// allowed wherever <paramref name="claimed"/> says the point is not claimed territory.</summary>
    public static (JumpKind Kind, RegionJump? Jump) Plan(RegionUnit u, (float X, float Z) target, IReadOnlyList<HuntCandidate> online,
        Func<float, float, bool> claimed, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf, WalkBudget budget,
        WaitTally? tally = null) =>
        JumpRule.Plan(u, target, online, (x, z) => !claimed(x, z), walkable, heightOf, budget, tally);

    /// <summary>The players to seed for a roamer at <paramref name="at"/>: eligible players within the seed reach (100 m,
    /// hunting-grounds A4), nearest first, at most HuntPlan.MaxTargets; anywhere on the map.</summary>
    public static List<long> Seeds(IEnumerable<HuntCandidate> players, (float X, float Z) at) =>
        HuntPlan.Targets(players.Where(Eligible), at, HuntingGrounds.SeedReach);

    /// <summary>"apocalypse &lt;id&gt; roam: hotspot &lt;h&gt; on a player" (D25): no name and no position (D19).</summary>
    public static string Line(string eventId, int hotspot) => $"apocalypse {eventId} roam: hotspot {hotspot} on a player";
}

/// <summary>Logs the roam line once per hotspot and target change (D3, step 3): a hotspot whose target is a new player
/// returns its line, the same target or none returns null.</summary>
public sealed class RoamTargets
{
    readonly Dictionary<int, long> _target = [];

    public string? Seen(string eventId, int hotspot, long? target)
    {
        if (target is not { } key) { _target.Remove(hotspot); return null; }
        if (_target.TryGetValue(hotspot, out var was) && was == key) return null;
        _target[hotspot] = key;
        return RoamRule.Line(eventId, hotspot);
    }
}

/// <summary>
/// Who hunts in an infestation (undead-hordes D9): the hunters' share of the living units, rounded down, hunts players; the
/// rest drift. A unit seen for the first time (a spread unit, a rebuilt one, every unit at the rise) joins the hunters while
/// their share is not met, the drifters otherwise, in key order. When deaths move the share (Codex step 3 F1), the fewest
/// units change role: the lowest-keyed drifters join the hunters, or the highest-keyed hunters drift.
/// </summary>
public sealed class InfestRoles
{
    readonly Dictionary<long, bool> _hunter = [];

    /// <summary>The hunters' share of <paramref name="living"/> units at <paramref name="percent"/>, rounded down.</summary>
    public static int Share(int percent, int living) => (int)((long)living * Math.Clamp(percent, 0, 100) / 100);

    public int Hunters => _hunter.Values.Count(h => h);

    public bool IsHunter(long key) => _hunter.TryGetValue(key, out var h) && h;

    /// <summary>Forgets the units no longer living, then gives every new one of <paramref name="living"/> its role.</summary>
    public void Assign(IReadOnlyCollection<long> living, int percent)
    {
        var alive = living as ISet<long> ?? new HashSet<long>(living);
        foreach (var gone in _hunter.Keys.Where(k => !alive.Contains(k)).ToList()) _hunter.Remove(gone);
        var hunters = Hunters;
        var share = Share(percent, living.Count);
        foreach (var key in living.Where(k => !_hunter.ContainsKey(k)).OrderBy(k => k))
        {
            var hunts = hunters < share;
            _hunter[key] = hunts;
            if (hunts) hunters++;
        }
        if (hunters < share)
            foreach (var key in _hunter.Where(r => !r.Value).Select(r => r.Key).OrderBy(k => k).Take(share - hunters).ToList()) _hunter[key] = true;
        else if (hunters > share)
            foreach (var key in _hunter.Where(r => r.Value).Select(r => r.Key).OrderByDescending(k => k).Take(hunters - share).ToList()) _hunter[key] = false;
    }

    public void Clear() => _hunter.Clear();
}

/// <summary>An infestation's hunters' moves over a minute (undead-hordes step 4, a diagnostic, counts only):
/// "apocalypse &lt;id&gt; infest moves: &lt;h&gt; hunters, &lt;t&gt; origins on a player; &lt;j&gt; jumps, &lt;r&gt; in reach,
/// &lt;w&gt; waiting, &lt;n&gt; with no target (… landings tried, … near a player, … claimed, … not walkable, … out of
/// budget)", once a minute while the run infests.</summary>
public sealed class InfestTally
{
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(1);
    public WaitTally Waits { get; private set; } = new();
    int _jumps, _reach, _waiting, _untargeted;
    DateTime? _since;

    /// <summary>One hunter's plan this pass; null when its origin has no target.</summary>
    public void Count(JumpKind? kind)
    {
        switch (kind)
        {
            case JumpKind.Jump: _jumps++; break;
            case JumpKind.InReach: _reach++; break;
            case JumpKind.Waiting: _waiting++; break;
            default: _untargeted++; break;
        }
    }

    /// <summary>True once a minute has passed since the first pass counted (or the last line).</summary>
    public bool Due(DateTime utcNow)
    {
        _since ??= utcNow;
        return utcNow - _since.Value >= Every;
    }

    /// <summary>The minute's line; resets the counts.</summary>
    public string Line(string eventId, int hunters, int targeted, DateTime utcNow)
    {
        var w = Waits;
        var line = $"apocalypse {eventId} infest moves: {hunters} hunters, {targeted} origins on a player; {_jumps} jumps, {_reach} in reach, " +
                   $"{_waiting} waiting, {_untargeted} with no target ({w.Candidates} landings tried, {w.Watched} watched, {w.NearPlayer} near a player, " +
                   $"{w.NotAllowed} claimed, {w.NotWalkable} not walkable, {w.Budget} out of budget)";
        _jumps = _reach = _waiting = _untargeted = 0;
        Waits = new WaitTally();
        _since = utcNow;
        return line;
    }
}

/// <summary>
/// An infestation's moves (undead-hordes D9): a hunter goes after the eligible player nearest its origin's hunters, anywhere,
/// by the roam's hidden jumps (RoamRule.Plan: only while no player is within 100 m, landing 50 m or more from every player); a
/// drifter hops outward from its origin by the same hidden jumps toward the ring driftRadius m out on its own bearing, never
/// landing past that ring. Units in view walk under the game's AI.
/// </summary>
public static class InfestMove
{
    /// <summary>The point a drifter heads for: on the ring <paramref name="radius"/> m from its origin, along the unit's
    /// bearing from the origin, or a bearing from its key when it stands on the origin.</summary>
    public static (float X, float Z) DriftTarget((float X, float Z) origin, RegionUnit u, int radius)
    {
        var d = Apocalypse.Distance(origin.X, origin.Z, u.X, u.Z);
        double dx, dz;
        if (d >= 1f) { dx = (u.X - origin.X) / d; dz = (u.Z - origin.Z) / d; }
        else
        {
            var a = u.Key * 2.399963229728653;                       // the golden angle: keys spread round the ring
            dx = Math.Cos(a);
            dz = Math.Sin(a);
        }
        return ((float)(origin.X + dx * radius), (float)(origin.Z + dz * radius));
    }

    /// <summary>One drifter's plan: none with a radius of 0; else JumpRule's toward <see cref="DriftTarget"/> with landings
    /// allowed only within <paramref name="radius"/> m of the origin and outside claimed territory.</summary>
    public static (JumpKind Kind, RegionJump? Jump) Drift(RegionUnit u, (float X, float Z) origin, int radius, IReadOnlyList<HuntCandidate> online,
        Func<float, float, bool> claimed, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf, WalkBudget budget)
    {
        if (radius <= 0) return (JumpKind.InReach, null);
        return JumpRule.Plan(u, DriftTarget(origin, u, radius), online,
            (x, z) => !claimed(x, z) && Apocalypse.Distance(origin.X, origin.Z, x, z) <= radius, walkable, heightOf, budget);
    }
}

/// <summary>
/// The spread (D4, Business rules 4; design §9 D44): a victim, player or NPC not ours, whose killer resolves (the unit,
/// else its EntityOwner, as the scoreboard does) to a living unit of the running apocalypse spawned since its start queues
/// a uniform draw of perKillMin..perKillMax roamers (undead-hordes D7; a 0.13.x perKill n is n..n) within 3 m of the
/// victim; none while live roamers are at roamers.max, while perMinute spread units were queued in the last 60 s, or while
/// MaxTrackedUnits is full.
/// </summary>
public sealed class SpreadGate
{
    readonly Queue<DateTime> _queued = new();

    /// <summary>True when the death spreads for <paramref name="apocalypse"/>: the victim is no unit of ours and the killing
    /// unit is the running apocalypse's (a roamer, a horde unit or a boss) spawned since it started.</summary>
    public static bool Qualifies(ScoreSides sides, ActiveEvent apocalypse) =>
        apocalypse.Definition.Apocalypse is not null && sides.Victim is null
        && sides.Killing is { } k && k.EventId == apocalypse.Id && k.SpawnedUtc >= apocalypse.Instance.StartedUtc;

    /// <summary>Spread units queued in the 60 s before <paramref name="utcNow"/>.</summary>
    public int InWindow(DateTime utcNow)
    {
        while (_queued.Count > 0 && utcNow - _queued.Peek() >= Apocalypse.SpreadWindow) _queued.Dequeue();
        return _queued.Count;
    }

    /// <summary>One kill's draw (undead-hordes D7): uniform in perKillMin..perKillMax; a fixed draw (0.13.x's perKill) takes
    /// no random number, so a 0.13.x run draws its roamers as before (D21).</summary>
    public static int Draw(ApocalypseRoamers roamers, IRandom rng) =>
        roamers.PerKillMin == roamers.PerKillMax ? roamers.PerKillMin : roamers.PerKillMin + Pools.Index(rng, roamers.PerKillMax - roamers.PerKillMin + 1);

    /// <summary>How many of <paramref name="drawn"/> spread roamers one qualifying kill may queue now: min(drawn, roamers.max
    /// − live roamers, perMinute − the window, the free tracked slots), at least 0.</summary>
    public int Allow(DateTime utcNow, ApocalypseRoamers roamers, int liveRoamers, int freeTracked, int drawn) =>
        Math.Max(0, Math.Min(Math.Min(drawn, roamers.Max - liveRoamers), Math.Min(roamers.PerMinute - InWindow(utcNow), freeTracked)));

    /// <summary>Counts <paramref name="n"/> spread units queued at <paramref name="utcNow"/> in the window.</summary>
    public void Queued(DateTime utcNow, int n)
    {
        for (var i = 0; i < n; i++) _queued.Enqueue(utcNow);
    }

    /// <summary>A resumed run's window count from its record (D11, D12), counted as queued at <paramref name="utcNow"/>.</summary>
    public void Restore(DateTime utcNow, int count) => Queued(utcNow, Math.Max(0, count));

    /// <summary>"apocalypse &lt;id&gt; spread: +&lt;k&gt; (&lt;live&gt;/&lt;max&gt;, &lt;w&gt;/&lt;perMinute&gt; this minute)" (12.2).</summary>
    public static string Line(string eventId, int queued, int live, int max, int window, int perMinute) =>
        $"apocalypse {eventId} spread: +{queued} ({live}/{max}, {window}/{perMinute} this minute)";
}

/// <summary>
/// The hordes' clock and size (D5, D36, Business rules 5): horde k (1..count) rises at start + firstAfterMinutes + (k − 1) ×
/// everyMinutes at hotspot ((k − 1) mod hotspots) + 1 with size + (k − 1) × growth units, clamped by MaxUnitsPerWave and the
/// free MaxTrackedUnits slots; the last horde also queues the action's bosses, a clamp cutting its regular units before
/// its bosses (revenge D29's order). A horde that queued no unit has not risen and is tried again every 60 s; a rise due at
/// or after the run's end never comes.
/// </summary>
public static class HordeSchedule
{
    /// <summary>Seconds from the start to the last horde's rise: firstAfterMinutes × 60 + (count − 1) × everyMinutes × 60 (D1).</summary>
    public static long LastRiseSeconds(ApocalypseHordes h) => h.FirstAfterMinutes * 60L + (h.Count - 1) * (long)h.EveryMinutes * 60L;

    public static DateTime DueAt(DateTime startedUtc, ApocalypseHordes h, int k) =>
        startedUtc.AddMinutes(h.FirstAfterMinutes + (double)(k - 1) * h.EveryMinutes);

    public static int HotspotOf(int k, int hotspots) => (k - 1) % Math.Max(1, hotspots) + 1;

    public static int Size(ApocalypseHordes h, int k) => h.Size + (k - 1) * h.Growth;

    /// <summary>The horde due at <paramref name="utcNow"/>, or null: the next one (risen + 1) when its time has come, before
    /// the end, and at least 60 s after the last try that queued nothing.</summary>
    public static int? Due(DateTime startedUtc, DateTime endsUtc, ApocalypseHordes h, int risen, DateTime? lastTryUtc, DateTime utcNow)
    {
        var k = risen + 1;
        if (k > h.Count) return null;
        var at = DueAt(startedUtc, h, k);
        if (at >= endsUtc || utcNow < at || utcNow >= endsUtc) return null;
        if (lastTryUtc is { } tried && utcNow - tried < Apocalypse.RiseRetry) return null;
        return k;
    }

    /// <summary>The units horde <paramref name="k"/> queues: its bosses first when it is the last horde (each entry's own
    /// modifiers), then size + (k − 1) × growth regular units drawn by weight; the whole clamped by MaxUnitsPerWave, then
    /// by the free tracked slots, cutting regular units before bosses (revenge D29). Each clamp adds its log line.</summary>
    public static IReadOnlyList<(UnitEntry Unit, int Entry)> Units(ApocalypseAction action, int k, int maxPerWave, int occupied, int maxTracked,
        IRandom rng, List<string> log)
    {
        var bosses = k == action.Hordes.Count && action.Bosses is { } b
            ? b.Select((e, i) => (Unit: e, Entry: i + 1)).ToList()
            : [];
        var regular = Size(action.Hordes, k);
        var bossCount = bosses.Sum(x => x.Unit.Count);
        var room = Precedence.WaveSize(regular + bossCount, maxPerWave, occupied, maxTracked, log);
        var result = new List<(UnitEntry, int)>();
        foreach (var (unit, entry) in bosses)
        {
            if (room <= 0) break;
            var n = Math.Min(unit.Count, room);
            result.Add((unit with { Count = n }, entry));
            room -= n;
        }
        for (var i = 0; i < Math.Min(regular, room); i++) result.Add((RoamerPlan.Draw(action, rng) with { Count = 1 }, 0));
        return result;
    }

    /// <summary>"&lt;n&gt; units of horde &lt;k&gt; of &lt;c&gt;"-style counts for the rise line.</summary>
    public static int Count(IReadOnlyList<(UnitEntry Unit, int Entry)> units) => units.Sum(u => u.Unit.Count);

    /// <summary>A horde march's bosses (undead-hordes D10), one slot each: fixed bosses in list order, then each roll's count
    /// drawn uniformly in min..max and that many distinct members of its pool the list does not already hold, as
    /// wave-variety's roll does (Pools.Resolve).</summary>
    public static IReadOnlyList<UnitEntry> MarchBosses(ApocalypseAction action, IRandom rng)
    {
        var list = action.March?.Bosses ?? [];
        var bosses = list.Where(b => !b.IsRoll).Select(b => new UnitEntry(b.Prefab, b.Count, 1.0, b.Modifiers)).ToList();
        foreach (var b in list)
        {
            if (b.Pool is not { } pool || action.Pools?.GetValueOrDefault(pool) is not { } members) continue;
            var count = b.Min + Pools.Index(rng, b.Max - b.Min + 1);
            var free = members.Where(p => bosses.All(h => h.Prefab != p)).ToList();
            for (var i = 0; i < count && free.Count > 0; i++)
            {
                var k = Pools.Index(rng, free.Count);
                bosses.Add(new UnitEntry(free[k], 1, 1.0, b.Modifiers));
                free.RemoveAt(k);
            }
        }
        return bosses;
    }

    /// <summary>The units a horde march's horde queues (undead-hordes D10): its resolved <paramref name="bosses"/> first, slot
    /// j as entry j, then each units entry's count, a pool entry drawing each copy uniformly from its pool and every copy
    /// kept with its entry's chance; the whole clamped by MaxUnitsPerWave, then by the free tracked slots, cutting regular
    /// units before bosses (revenge D29). <paramref name="asked"/> is the count before the clamp.</summary>
    public static IReadOnlyList<(UnitEntry Unit, int Entry)> MarchUnits(ApocalypseAction action, IReadOnlyList<UnitEntry> bosses, int maxPerWave, int occupied,
        int maxTracked, IRandom rng, List<string> log, out int asked)
    {
        var regular = new List<UnitEntry>();
        foreach (var e in action.Units)
            for (var i = 0; i < e.Count; i++)
            {
                if (e.Chance < 1.0 && rng.NextDouble() >= e.Chance) continue;
                var prefab = e.Pool is { } pool && action.Pools?.GetValueOrDefault(pool) is { Count: > 0 } members ? members[Pools.Index(rng, members.Count)] : e.Prefab;
                regular.Add(new UnitEntry(prefab, 1, 1.0, e.Modifiers));
            }
        asked = regular.Count + bosses.Sum(b => b.Count);
        var room = Precedence.WaveSize(asked, maxPerWave, occupied, maxTracked, log);
        var result = new List<(UnitEntry, int)>();
        for (var j = 0; j < bosses.Count && room > 0; j++)
        {
            var n = Math.Min(bosses[j].Count, room);
            result.Add((bosses[j] with { Count = n }, j + 1));
            room -= n;
        }
        foreach (var u in regular.Take(Math.Max(0, room))) result.Add((u, 0));
        return result;
    }
}

/// <summary>A risen horde's front (D6): the point moving from its hotspot to the goal at marchSpeed m/s.</summary>
public sealed class Front(int horde, float originX, float originZ, float goalX, float goalZ)
{
    public int Horde { get; } = horde;

    /// <summary>"horde", or "wave" for a destination wave's front (undead-hordes D6): the word of its held and crossed lines.</summary>
    public string Kind { get; init; } = "horde";
    public float OriginX { get; } = originX;
    public float OriginZ { get; } = originZ;
    public float GoalX { get; } = goalX;
    public float GoalZ { get; } = goalZ;
    public float X { get; internal set; } = originX;
    public float Z { get; internal set; } = originZ;

    /// <summary>The share of the origin-to-goal distance covered, 0..1 (D6).</summary>
    public double Progress
    {
        get
        {
            // in double: a float division puts 100 m of 500 at 19.99 %, a step short of its announcement (D7)
            double total = Apocalypse.Distance(OriginX, OriginZ, GoalX, GoalZ);
            return total <= 0 ? 1 : Math.Clamp(1 - Apocalypse.Distance(X, Z, GoalX, GoalZ) / total, 0, 1);
        }
    }

    public int Percent => (int)Math.Floor(Progress * 100 + 1e-9);

    /// <summary>The last time a player stood within 100 m of the horde (D6's unobserved rule); null when never.</summary>
    public DateTime? LastWatchedUtc { get; internal set; }

    internal readonly HashSet<int> Announced = [];

    /// <summary>The prefab of each `bosses` entry this horde raised, by entry index (D11, D36): set when the last horde
    /// rises and kept from the record at a resume, so a later edit of `hordes.count` never drops a living boss.</summary>
    public List<string> BossPrefabs { get; internal set; } = [];
    internal bool HeldNoGround;

    /// <summary>A front of a resumed run at its recorded point (D12).</summary>
    public static Front At(int horde, float originX, float originZ, float goalX, float goalZ, float x, float z) =>
        new(horde, originX, originZ, goalX, goalZ) { X = x, Z = z };
}

/// <summary>What one march step did (D6).</summary>
public enum MarchStep { Moved, HeldByPlayer, HeldNoGround, AtGoal }

/// <summary>
/// The march (D6, Business rules 6): each risen, living horde's front advances along the straight line to the goal at
/// marchSpeed m/s, never past it; it holds while any player stands within 60 m of it, except within goal.radius + 60 m of
/// the goal, where nothing holds it; a front whose next point has no walkable ground holds and logs "horde &lt;k&gt; held:
/// no walkable ground ahead" once per hold. Whenever no player has been within 100 m of the horde for 10 s, at least 80 %
/// of its living units stand within 30 m of the front (the hop moves the stragglers, by the step-1 verdict's mechanism).
/// </summary>
public static class MarchRule
{
    /// <summary>One step of <paramref name="front"/> over <paramref name="seconds"/>. <paramref name="players"/> are the
    /// online players' x and z; <paramref name="walkable"/> says whether the next point has walkable ground within 30 m.
    /// <paramref name="line"/> is the held line, once per hold. <paramref name="cross"/> probes the crossing's points: null
    /// when the ground could not be read (the tick's walk budget spent), which ends the crossing and holds the front, so an
    /// unread point is never crossed to (code review M1); without it the crossing probes with <paramref name="walkable"/>.</summary>
    public static MarchStep Step(Front front, int goalRadius, double speed, double seconds, IReadOnlyList<(float X, float Z)> players,
        Func<float, float, bool> walkable, DateTime utcNow, out string? line, Func<float, float, bool?>? cross = null)
    {
        line = null;
        if (players.Any(p => Apocalypse.Distance(p.X, p.Z, front.X, front.Z) <= Apocalypse.WatchRange)) front.LastWatchedUtc = utcNow;
        var left = Apocalypse.Distance(front.X, front.Z, front.GoalX, front.GoalZ);
        if (left <= 0) return MarchStep.AtGoal;
        var nearGoal = left <= goalRadius + Apocalypse.GoalHoldMargin;
        if (!nearGoal && players.Any(p => Apocalypse.Distance(p.X, p.Z, front.X, front.Z) <= Apocalypse.HoldRange)) return MarchStep.HeldByPlayer;
        var step = (float)Math.Min(speed * Math.Max(0, seconds), left);
        var (nx, nz) = Toward(front, left, step);
        if (!walkable(nx, nz))
        {
            // unobserved, the front crosses to the next walkable point on its line within 120 m, never past the goal (A12)
            if (IsUnobserved(front, utcNow))
            {
                var reach = Math.Min(step + Apocalypse.CrossReach, left);
                for (var d = step + Apocalypse.CrossStep; d - Apocalypse.CrossStep < reach - 1e-3; d += Apocalypse.CrossStep)
                {
                    var far = Math.Min(d, reach);
                    var (cx, cz) = Toward(front, left, far);
                    var known = cross is null ? walkable(cx, cz) : cross(cx, cz);
                    if (known is null) break;
                    if (known == false) continue;
                    line = CrossedLine(front.Horde, far, front.Kind);
                    front.HeldNoGround = false;
                    front.X = cx;
                    front.Z = cz;
                    return cx == front.GoalX && cz == front.GoalZ ? MarchStep.AtGoal : MarchStep.Moved;
                }
            }
            if (!front.HeldNoGround) line = HeldLine(front.Horde, front.Kind);
            front.HeldNoGround = true;
            return MarchStep.HeldNoGround;
        }
        front.HeldNoGround = false;
        front.X = nx;
        front.Z = nz;
        return nx == front.GoalX && nz == front.GoalZ ? MarchStep.AtGoal : MarchStep.Moved;
    }

    /// <summary>The point <paramref name="metres"/> along the front's line to the goal, <paramref name="left"/> m away; the
    /// goal itself from 1 mm short of it, so a step that reaches the goal lands on it exactly (code review L1: a goal off
    /// the axis left a float gap and took ticks more).</summary>
    static (float X, float Z) Toward(Front front, float left, float metres) =>
        metres >= left - 1e-3f ? (front.GoalX, front.GoalZ)
            : (front.X + (front.GoalX - front.X) / left * metres, front.Z + (front.GoalZ - front.Z) / left * metres);

    /// <summary>"horde &lt;k&gt; held: no walkable ground ahead" (D6).</summary>
    public static string HeldLine(int horde, string kind = "horde") => $"{kind} {horde} held: no walkable ground ahead";

    /// <summary>"horde &lt;k&gt; crossed &lt;d&gt; m of unwalkable ground" (D6, A12).</summary>
    public static string CrossedLine(int horde, float metres, string kind = "horde") => $"{kind} {horde} crossed {metres:0} m of unwalkable ground";

    /// <summary>The 25 % steps <paramref name="front"/> crossed since the last call, each once (D7).</summary>
    public static IReadOnlyList<int> NewSteps(Front front)
    {
        var crossed = new List<int>();
        foreach (var s in Apocalypse.ProgressSteps)
            if (front.Percent >= s && front.Announced.Add(s)) crossed.Add(s);
        return crossed;
    }

    /// <summary>True when no player has been within 100 m of the horde for 10 s (D6): the keep-up rule applies.</summary>
    public static bool IsUnobserved(Front front, DateTime utcNow) =>
        front.LastWatchedUtc is not { } seen || utcNow - seen >= Apocalypse.Unobserved;

    /// <summary>How many of <paramref name="units"/> stand within 30 m of the front, of how many (D6, D26).</summary>
    public static (int Near, int Of) KeepUp(Front front, IReadOnlyCollection<(float X, float Z)> units) =>
        (units.Count(u => Apocalypse.Distance(u.X, u.Z, front.X, front.Z) <= Apocalypse.KeepUpRange), units.Count);

    /// <summary>True when at least 80 % of the living units keep up (none living keeps up trivially).</summary>
    public static bool KeptUp((int Near, int Of) k) => k.Of == 0 || k.Near >= Math.Ceiling(Apocalypse.KeepUpShare * k.Of - 1e-9);

    /// <summary>True when a player stands within 100 m of the front or a unit this tick (<see cref="Watch"/> ran first):
    /// the met clock's sighting (undead-hordes A6), without <see cref="IsUnobserved"/>'s 10 s tail.</summary>
    public static bool SeenNow(Front front, DateTime utcNow) => front.LastWatchedUtc == utcNow;

    /// <summary>True when a destination wave's or a horde march's front waits for its units (undead-hordes A6, D6): they
    /// are watched, so no hop brings them up, and any one of them stands more than 30 m from the front where a step
    /// towards the goal would not bring it closer (behind the front or level with it). A front never runs ahead of units a
    /// player can see; the met clock ends the wait after 30 s in view.</summary>
    public static bool WaitsForUnits(Front front, IReadOnlyCollection<(float X, float Z)> units, DateTime utcNow) =>
        units.Count > 0 && !IsUnobserved(front, utcNow) && units.Any(u => LeftBehind(front, u));

    /// <summary>True when <paramref name="u"/> stands more than 30 m from the front and not ahead of it along the march.</summary>
    public static bool LeftBehind(Front front, (float X, float Z) u) =>
        Apocalypse.Distance(u.X, u.Z, front.X, front.Z) > Apocalypse.KeepUpRange
        && (u.X - front.X) * (front.GoalX - front.X) + (u.Z - front.Z) * (front.GoalZ - front.Z) <= 0;

    /// <summary>Refreshes the front's watch time when any player stands within 100 m of the horde: of its front or of one of
    /// its living <paramref name="units"/> (D6). Called before the hop decision (code review M3).</summary>
    public static void Watch(Front front, IReadOnlyList<(float X, float Z)> players, IEnumerable<(float X, float Z)> units, DateTime utcNow)
    {
        if (players.Count == 0) return;
        bool Near(float x, float z) => players.Any(p => Apocalypse.Distance(p.X, p.Z, x, z) <= Apocalypse.WatchRange);
        if (Near(front.X, front.Z) || units.Any(u => Near(u.X, u.Z))) front.LastWatchedUtc = utcNow;
    }

    /// <summary>The units more than <see cref="Apocalypse.HopGap"/> (15 m) from the front, the ones a hop moves while the horde is unobserved; none while it is
    /// observed, and never one with a player within 100 m of it (D6, S-9: our own units only, never while a player is
    /// near).</summary>
    public static IReadOnlyList<long> Stragglers(Front front, IEnumerable<(long Key, float X, float Z)> units, IReadOnlyList<(float X, float Z)> players,
        DateTime utcNow) =>
        !IsUnobserved(front, utcNow) ? []
        : units.Where(u => Apocalypse.Distance(u.X, u.Z, front.X, front.Z) > Apocalypse.HopGap
                           && !players.Any(p => Apocalypse.Distance(p.X, p.Z, u.X, u.Z) <= Apocalypse.WatchRange))
            .Select(u => u.Key).ToList();

    /// <summary>"horde &lt;k&gt; front &lt;p&gt; % units &lt;n&gt;/&lt;m&gt; within 30 m of the front" (D6, D26), every 30 s of a march.</summary>
    public static string KeepUpLine(int horde, int percent, (int Near, int Of) k) =>
        $"horde {horde} front {percent} % units {k.Near}/{k.Of} within 30 m of the front";
}

/// <summary>How an apocalypse ended (D8): won, lost, survived, or not yet.</summary>
public enum ApocalypseResult { None, Won, Lost, Survived }

/// <summary>A living horde's state for the outcome (D8): its front and its living units' x and z.</summary>
public sealed record HordeState(int Horde, Front Front, IReadOnlyList<(float X, float Z)> Living);

/// <summary>
/// The outcome (D8, Business rules 7; design §9 D44): lost when a horde's front is within goal.radius and at least one of its
/// living units stands within goal.radius + 30 m of the goal; won when every horde of hordes.count has risen and none has a
/// living unit or a pending order (a living boss keeps its horde alive, D36); survived when the duration ends first. Lost
/// beats won on the same tick, and won beats survived.
/// </summary>
public static class ApocalypseOutcome
{
    public static ApocalypseResult Decide(ApocalypseAction action, (float X, float Z) goal, IReadOnlyList<HordeState> living, int risen,
        Func<int, bool> hordeCleared, DateTime utcNow, DateTime endsUtc)
    {
        var r = action.Goal.Radius;
        foreach (var h in living)
            if (Apocalypse.Distance(h.Front.X, h.Front.Z, goal.X, goal.Z) <= r
                && h.Living.Any(u => Apocalypse.Distance(u.X, u.Z, goal.X, goal.Z) <= r + Apocalypse.LostMargin))
                return ApocalypseResult.Lost;
        if (risen >= action.Hordes.Count && Enumerable.Range(1, action.Hordes.Count).All(hordeCleared)) return ApocalypseResult.Won;
        if (utcNow >= endsUtc) return ApocalypseResult.Survived;
        return ApocalypseResult.None;
    }

    /// <summary>"event &lt;id&gt; ended: &lt;won|lost|survived&gt; (&lt;r&gt; of &lt;c&gt; hordes risen)" (D8); an infestation's
    /// "won (cleared)" or "survived", a horde march's "won (horde destroyed)" or "survived" (undead-hordes D9, D11).</summary>
    public static string Line(string eventId, ApocalypseResult result, int risen, int count, ApocalypseMode mode = ApocalypseMode.Apocalypse) =>
        $"event {eventId} ended: {result.ToString().ToLowerInvariant()}" + (mode, result) switch
        {
            (ApocalypseMode.Apocalypse, _) => $" ({risen} of {count} hordes risen)",
            (ApocalypseMode.Infestation, ApocalypseResult.Won) => " (cleared)",
            (ApocalypseMode.HordeMarch, ApocalypseResult.Won) => " (horde destroyed)",
            _ => "",
        };

    /// <summary>The end path the scoreboard reads: won is Victory, lost Defeat, survived Natural.</summary>
    public static EndPath PathOf(ApocalypseResult result) => result switch
    {
        ApocalypseResult.Won => EndPath.Victory,
        ApocalypseResult.Lost => EndPath.Defeat,
        _ => EndPath.Natural,
    };

    /// <summary>A horde is cleared when no order and no unit of it remains, its bosses included (D8, D36): the ledger's
    /// WaveCleared over its regular wave and each boss entry's wave, counting only units spawned since the start.</summary>
    public static bool HordeCleared(SpawnLedger ledger, string eventId, int horde, DateTime startedUtc) =>
        ledger.WaveCleared(eventId, Apocalypse.HordeWave(horde), startedUtc)
        && ledger.WaveCleared(eventId, Apocalypse.GrowthWave(horde), startedUtc)
        && Enumerable.Range(1, Apocalypse.MaxBossSlots).All(j => ledger.WaveCleared(eventId, Apocalypse.BossWave(horde, j), startedUtc));
}

/// <summary>Every way an apocalypse ends or is touched (D8, D9; Business rules 11's "every end path").</summary>
public enum ApocalypseEnd { Won, Lost, Survived, Stop, Purge, PillarOff, Fault, Restart, Disable }

/// <summary>
/// The end paths (D9): won, lost and survived show their outcome line and then the scoreboard; an admin stop the scoreboard
/// and no outcome line; purge, pillar off and a fault cancel neither. Each of those removes the resume record, as does a
/// purge or pillar off during a resume's drain wait (which then rebuilds nothing). A restart keeps the record and resumes;
/// `event disable` does not end the live run, but its definition is not resumed after a restart (D12).
/// </summary>
public static class EndRule
{
    public static bool EndsRun(ApocalypseEnd path) => path is not (ApocalypseEnd.Restart or ApocalypseEnd.Disable);

    public static bool RemovesRecord(ApocalypseEnd path) => path is not (ApocalypseEnd.Restart or ApocalypseEnd.Disable);

    public static bool ShowsScoreboard(ApocalypseEnd path) => path is ApocalypseEnd.Won or ApocalypseEnd.Lost or ApocalypseEnd.Survived or ApocalypseEnd.Stop;

    public static bool ShowsOutcome(ApocalypseEnd path) => path is ApocalypseEnd.Won or ApocalypseEnd.Lost or ApocalypseEnd.Survived;

    /// <summary>The scoreboard's end path for an end that ends the run.</summary>
    public static EndPath PathOf(ApocalypseEnd path) => path switch
    {
        ApocalypseEnd.Won => EndPath.Victory,
        ApocalypseEnd.Lost => EndPath.Defeat,
        ApocalypseEnd.Survived => EndPath.Natural,
        ApocalypseEnd.Stop => EndPath.Stop,
        ApocalypseEnd.Purge => EndPath.Purge,
        ApocalypseEnd.PillarOff => EndPath.PillarOff,
        ApocalypseEnd.Fault => EndPath.Fault,
        _ => EndPath.Restart,
    };

    public static ApocalypseEnd Of(ApocalypseResult result) => result switch
    {
        ApocalypseResult.Won => ApocalypseEnd.Won,
        ApocalypseResult.Lost => ApocalypseEnd.Lost,
        _ => ApocalypseEnd.Survived,
    };

    /// <summary>Every other event kind ends at a restart (Epic S-11); only an apocalypse's record survives one.</summary>
    public static bool Resumes(EventDefinition def) => def.Apocalypse is not null;
}

/// <summary>A resume's verdict (D12).</summary>
public enum ResumeVerdict { Resume, Gone, Disabled, Changed, EndedWhileDown }

/// <summary>What the drain gate says at one look (D12).</summary>
public enum DrainStep { Wait, Proceed, ProceedWithWarning, Cancelled }

/// <summary>One unit group to rebuild at a resume (D12): <see cref="Count"/> units of <see cref="Prefab"/> (null: drawn
/// from `units` by weight) with <see cref="Modifiers"/>, in ledger wave <see cref="Wave"/>, around (x, z).</summary>
public sealed record RebuildOrder(int Wave, string? Prefab, int Count, SpawnModifiers? Modifiers, float X, float Z);

/// <summary>
/// The resume (D12, Business rules 8; design §9 D37, D45): a record resumes only when its definition still exists, is
/// startable, is still an Apocalypse with at least as many hordes as had risen and every recorded boss prefab among its
/// `bosses` (Review 7 F2), and its EndsUtc lies ahead; otherwise "apocalypse &lt;id&gt; not resumed: &lt;reason&gt;" and the
/// record goes (an EndsUtc already past ends the run as survived in the log, with no unit spawned). The rebuild waits for the
/// boot sweep's despawns up to 5 min, then proceeds with one warning.
/// </summary>
public static class ResumeRule
{
    public static ResumeVerdict Verdict(ApocalypseRecord record, DefinitionSet set, DateTime utcNow)
    {
        var def = set.Find(record.EventId);
        if (def is null) return ResumeVerdict.Gone;
        if (!def.Startable) return ResumeVerdict.Disabled;
        if (Changed(record, def)) return ResumeVerdict.Changed;
        if (record.EndsUtc <= utcNow) return ResumeVerdict.EndedWhileDown;
        return ResumeVerdict.Resume;
    }

    /// <summary>The definition changed kind or mode (undead-hordes D17), has fewer hordes than had risen, or lacks a
    /// recorded boss prefab (D12): a horde march's among its fixed bosses or its rolls' pools.</summary>
    public static bool Changed(ApocalypseRecord record, EventDefinition def)
    {
        if (def.Apocalypse is not { } a) return true;
        if (record.ModeOf != a.Mode) return true;
        if (a.Hordes.Count < record.HordesRisen) return true;
        return record.Hordes.SelectMany(h => h.LivingBosses).Any(b => b.Living > 0 && BossEntry(a, b.Prefab, -1) is null && !RolledBoss(a, b.Prefab));
    }

    /// <summary>The bosses entry of <paramref name="prefab"/>: entry <paramref name="index"/> when it names it (two entries may
    /// share a prefab with different modifiers, review L2), else the first that does; a horde march's fixed bosses.</summary>
    static UnitEntry? BossEntry(ApocalypseAction a, string prefab, int index)
    {
        IReadOnlyList<UnitEntry> list = a.Mode == ApocalypseMode.HordeMarch
            ? (a.March?.Bosses ?? []).Where(b => !b.IsRoll).Select(b => new UnitEntry(b.Prefab, b.Count, 1.0, b.Modifiers)).ToList()
            : a.Bosses ?? [];
        return index >= 0 && index < list.Count && list[index].Prefab == prefab ? list[index] : list.FirstOrDefault(e => e.Prefab == prefab);
    }

    /// <summary>A horde march's roll that can draw <paramref name="prefab"/>: its pool holds it.</summary>
    static BossRoll? RollOf(ApocalypseAction a, string prefab) =>
        (a.March?.Bosses ?? []).FirstOrDefault(b => b.Pool is { } p && a.Pools?.GetValueOrDefault(p)?.Contains(prefab) == true);

    static bool RolledBoss(ApocalypseAction a, string prefab) => a.Mode == ApocalypseMode.HordeMarch && RollOf(a, prefab) is not null;

    public static string Reason(ResumeVerdict v) => v switch
    {
        ResumeVerdict.Gone => "definition gone",
        ResumeVerdict.Disabled => "disabled",
        ResumeVerdict.Changed => "changed",
        ResumeVerdict.EndedWhileDown => "ended while down",
        _ => "resumed",
    };

    /// <summary>"apocalypse &lt;id&gt; not resumed: &lt;reason&gt;" (D12).</summary>
    public static string NotResumedLine(string eventId, ResumeVerdict v) => $"apocalypse {eventId} not resumed: {Reason(v)}";

    /// <summary>"event &lt;id&gt; resumed after restart (horde &lt;r&gt;/&lt;c&gt;, &lt;u&gt; units to rebuild)" (D12).</summary>
    public static string ResumedLine(string eventId, int risen, int count, int units) =>
        $"event {eventId} resumed after restart (horde {risen}/{count}, {units} units to rebuild)";

    /// <summary>"apocalypse &lt;id&gt;: boot sweep not drained after 5 min (&lt;n&gt; left); resuming" (D12).</summary>
    public static string DrainWarning(string eventId, int left) => $"apocalypse {eventId}: boot sweep not drained after 5 min ({left} left); resuming";

    /// <summary>The drain gate: cancelled by a purge or pillar off during the wait (nothing is rebuilt, D9); proceed once the
    /// sweep's despawns have drained; proceed with the warning 5 min after the sweep; wait otherwise.</summary>
    public static DrainStep Drain(DateTime sweptUtc, DateTime utcNow, int leftToDrain, bool cancelled) =>
        cancelled ? DrainStep.Cancelled
        : leftToDrain <= 0 ? DrainStep.Proceed
        : utcNow - sweptUtc >= Apocalypse.DrainWait ? DrainStep.ProceedWithWarning
        : DrainStep.Wait;

    /// <summary>The units to rebuild (D12, D36): each hotspot's living roamers at its hotspot, each living horde's living
    /// count at its front, each `bosses` entry's living count as the recorded prefab with the definition's modifiers for
    /// it, and the rest as regular units. Never more than the record holds.</summary>
    public static IReadOnlyList<RebuildOrder> Rebuild(ApocalypseRecord record, ApocalypseAction action)
    {
        var orders = new List<RebuildOrder>();
        // a horde march's spread units infest round its arrival point (undead-hordes D17)
        var arrival = record.Arrived && record.Arrival is { } p ? (p.X, p.Z) : ((float X, float Z)?)null;
        for (var h = 0; h < record.Hotspots.Count; h++)
            if (record.Roamers[h] > 0)
            {
                var (x, z) = arrival ?? (record.Hotspots[h].X, record.Hotspots[h].Z);
                orders.Add(new RebuildOrder(Apocalypse.RoamerWave(h + 1), null, record.Roamers[h], null, x, z));
            }
        foreach (var horde in record.Hordes.OrderBy(x => x.Number))
        {
            var bosses = 0;
            for (var j = 0; j < horde.LivingBosses.Count; j++)
            {
                var b = horde.LivingBosses[j];
                if (b.Living <= 0) continue;
                var modifiers = BossEntry(action, b.Prefab, j)?.Modifiers ?? (action.Mode == ApocalypseMode.HordeMarch ? RollOf(action, b.Prefab)?.Modifiers : null);
                orders.Add(new RebuildOrder(Apocalypse.BossWave(horde.Number, j + 1), b.Prefab, b.Living, modifiers, horde.FrontX, horde.FrontZ));
                bosses += b.Living;
            }
            var growth = Math.Clamp(horde.Growth, 0, horde.Living - bosses);
            if (growth > 0) orders.Add(new RebuildOrder(Apocalypse.GrowthWave(horde.Number), null, growth, null, horde.FrontX, horde.FrontZ));
            var regular = horde.Living - bosses - growth;
            if (regular > 0) orders.Add(new RebuildOrder(Apocalypse.HordeWave(horde.Number), null, regular, null, horde.FrontX, horde.FrontZ));
        }
        return orders;
    }

    /// <summary>The hordes missed while the server was down (D12, S-17): each horde after the risen ones whose time has come
    /// before <paramref name="utcNow"/> and before the end, in order, each once.</summary>
    public static IReadOnlyList<int> CatchUp(ApocalypseRecord record, ApocalypseAction action, DateTime utcNow)
    {
        var missed = new List<int>();
        for (var k = record.HordesRisen + 1; k <= action.Hordes.Count; k++)
        {
            var at = HordeSchedule.DueAt(record.StartedUtc, action.Hordes, k);
            if (at > utcNow || at >= record.EndsUtc) break;
            missed.Add(k);
        }
        return missed;
    }
}

/// <summary>
/// The rolling unit lifetime (D13, Business rules 9): a roamer's or horde unit's due time is at most 2 h ahead (Limits
/// ApocalypseUnitLifetimeSeconds) and its LifeTime that plus the drain margin; while the run lives, a unit with less than
/// 90 min left is renewed to now + 2 h, so a run of days keeps its units and a stopped mod leaves none for more than about
/// 2 h. A unit of an ended run is never renewed.
/// </summary>
public static class RollingLifetime
{
    /// <summary>A new unit's due time and LifeTime: min(now + 2 h, the run's end + grace), and that plus the drain margin.</summary>
    public static UnitLifetime Of(DateTime utcNow, DateTime endsUtc, int graceSeconds, int drainMarginSeconds) =>
        SpawnLedger.Lifetime(utcNow, endsUtc, Limits.ApocalypseUnitLifetimeSeconds, graceSeconds, 0, drainMarginSeconds);

    /// <summary>The renewed due time and LifeTime of <paramref name="unit"/>, or null when it needs none (more than 90 min
    /// left) or the run has ended (<paramref name="runLives"/> false).</summary>
    public static UnitLifetime? Renew(TrackedUnit unit, bool runLives, DateTime utcNow, DateTime endsUtc, int graceSeconds, int drainMarginSeconds)
    {
        if (!runLives || unit.DueUtc - utcNow >= Apocalypse.RenewBelow) return null;
        var life = Of(utcNow, endsUtc, graceSeconds, drainMarginSeconds);
        return life.DueUtc > unit.DueUtc ? life : null;
    }

    /// <summary>"apocalypse &lt;id&gt; renewed &lt;n&gt; lifetimes" (D13, D30).</summary>
    public static string Line(string eventId, int renewed) => $"apocalypse {eventId} renewed {renewed} lifetimes";
}

/// <summary>
/// The run's unit tally (D15; Review 3 F3: per boot). Every unit the ledger queued for the run is exactly one of: pending,
/// alive (the ledger's tracked units of the run, read, never counted here), died, despawned, removed by the game or failed
/// to spawn (a cancelled order included). It restarts at a resume: the units from before the restart are accounted by the
/// boot sweep's line, and the rebuild's units are queued into the new tally.
/// </summary>
public sealed class ApocalypseTally
{
    public int Queued { get; private set; }
    public int Confirmed { get; private set; }
    public int Died { get; private set; }
    public int Despawned { get; private set; }
    public int Removed { get; private set; }
    public int Failed { get; private set; }

    public void Add(int queued) => Queued += Math.Max(0, queued);
    public void Spawned() => Confirmed++;
    public void FailedToSpawn(int n = 1) => Failed += Math.Max(0, n);
    public void Death() => Died++;
    public void Despawn() => Despawned++;
    public void Removal() => Removed++;

    public int Pending => Queued - Confirmed - Failed;

    /// <summary>The units queued that no bucket holds, with <paramref name="alive"/> read from the ledger: 0 when every unit
    /// is accounted (D15).</summary>
    public int Unaccounted(int alive) => Queued - (Pending + alive + Died + Despawned + Removed + Failed);

    /// <summary>"apocalypse &lt;id&gt; units: &lt;q&gt; queued = &lt;p&gt; pending + &lt;a&gt; alive + &lt;d&gt; died + &lt;s&gt; despawned + &lt;g&gt;
    /// removed + &lt;f&gt; failed" (D15).</summary>
    public string Line(string eventId, int alive) =>
        $"apocalypse {eventId} units: {Queued} queued = {Pending} pending + {alive} alive + {Died} died + {Despawned} despawned + {Removed} removed + {Failed} failed";
}

/// <summary>When the record is written (D11): at once at a rise or a defeat, each 30 s of a march and each 60 s otherwise,
/// never twice within a second (DataStore's rule).</summary>
public static class RecordClock
{
    public static bool Due(DateTime? lastUtc, DateTime utcNow, bool marching, bool forced)
    {
        if (lastUtc is not { } last) return true;
        var since = utcNow - last;
        if (since < StateStore.MinInterval) return false;
        return forced || since >= (marching ? Apocalypse.RecordMarching : Apocalypse.RecordQuiet);
    }
}

/// <summary>The game side of a run (step 3 implements it over SpawnTracker, PlayerQuery and the walk check): every member
/// may throw, which the service guards.</summary>
public interface IApocalypseWorld
{
    /// <summary>The ledger every roamer and horde unit is queued through (D14).</summary>
    SpawnLedger Ledger { get; }
    IRandom Random { get; }
    /// <summary>The i-th of n spawn points on walkable ground within <paramref name="radius"/> m of (x, z).</summary>
    (float X, float Y, float Z) Place(float x, float z, float radius, int i, int n);
    int GraceSeconds { get; }
    int DrainMarginSeconds { get; }
}

/// <summary>A rise of horde k (D5, D7): its number, hotspot, queued count (<see cref="Bosses"/> of them bosses), and the
/// clamp lines.</summary>
public sealed record HordeRise(int Horde, int Hotspot, int Queued, IReadOnlyList<string> Log, CapCut? Cut = null, int Bosses = 0);

/// <summary>
/// One running apocalypse's state (D3-D8, D11, D12, D15, D36): hotspots, the resolved goal, hordes risen, fronts, the spread
/// window and the tally, with each spawn path (start top-up, rise top-up, spread, horde rise, horde retry, resume rebuild)
/// queuing through <see cref="IApocalypseWorld.Ledger"/> under the run's event id and its ledger waves.
/// </summary>
public sealed class ApocalypseRun
{
    public ActiveEvent Active { get; }
    public ApocalypseAction Action { get; }
    public IReadOnlyList<Hotspot> Hotspots { get; }
    public (float X, float Y, float Z) Goal { get; }
    public int Risen { get; private set; }
    public DateTime? LastTryUtc { get; private set; }
    public SpreadGate Spread { get; } = new();
    public ApocalypseTally Tally { get; } = new();
    readonly Dictionary<int, Front> _fronts = [];
    /// <summary>The fronts of the risen hordes, by horde number.</summary>
    public IReadOnlyDictionary<int, Front> Fronts => _fronts;
    /// <summary>The top-ups done since the run (or its resume) began: the start's or the resume's, and one per rise.</summary>
    public int TopUps { get; private set; }

    /// <summary>True once a unit of the run was seen alive or confirmed spawned: an infestation or a horde march is won only
    /// after that (code review step 3).</summary>
    bool _lived;

    public string Id => Active.Id;

    /// <summary>The kind of run (undead-hordes D8, D10): an apocalypse, an infestation or a horde march.</summary>
    public ApocalypseMode Mode => Action.Mode;

    /// <summary>A horde march that reached its destination or stalled, and where (undead-hordes D11): it infests there.</summary>
    public bool Arrived { get; private set; }
    public (float X, float Z)? ArrivedAt { get; private set; }

    /// <summary>The infestation's origins (undead-hordes D9): an infestation's hotspots, a horde march's arrival point once it
    /// arrived, else none.</summary>
    public IReadOnlyList<Hotspot> Origins => Mode == ApocalypseMode.Infestation ? Hotspots
        : ArrivedAt is { } at ? [new Hotspot(1, at.X, at.Z, 0, (int)MathF.Floor(at.X / Apocalypse.CellSize), (int)MathF.Floor(at.Z / Apocalypse.CellSize))] : [];

    /// <summary>True while the units move by the infestation's rule (an infestation, or a horde march after arrival).</summary>
    public bool Infesting => Mode == ApocalypseMode.Infestation || (Mode == ApocalypseMode.HordeMarch && Arrived);

    /// <summary>The infest rule now: an infestation's, a horde march's infest block; null for an apocalypse.</summary>
    public InfestRule? Infest => Mode == ApocalypseMode.HordeMarch ? Action.March!.Infest : Action.Infest;

    /// <summary>The spread block a qualifying kill draws from now (undead-hordes D7, D10): the roamers' (an apocalypse's or an
    /// infestation's spread), a horde march's growth before arrival and its infest spread after.</summary>
    public ApocalypseRoamers SpreadBlock => Mode != ApocalypseMode.HordeMarch ? Action.Roamers : Arrived ? Action.March!.InfestSpread : Action.March!.Growth;

    /// <summary>Who hunts and who drifts while the run infests (undead-hordes D9).</summary>
    public InfestRoles Roles { get; } = new();

    public ApocalypseRun(ActiveEvent active, IReadOnlyList<Hotspot> hotspots, (float X, float Y, float Z) goal)
    {
        Active = active;
        Action = active.Definition.Apocalypse ?? throw new ArgumentException("not an apocalypse", nameof(active));
        Hotspots = hotspots;
        Goal = goal;
    }

    /// <summary>A horde march's horde reached its destination, or stalled, at (x, z) (undead-hordes D11): from now on it is
    /// an infestation with that point as its one origin. False when it had already arrived.</summary>
    public bool Arrive(float x, float z)
    {
        if (Mode != ApocalypseMode.HordeMarch || Arrived) return false;
        Arrived = true;
        ArrivedAt = (x, z);
        return true;
    }

    /// <summary>The run's living or waiting units of every wave: its tracked units spawned since the start and its orders not
    /// yet confirmed (the infestation's and the horde's living count, undead-hordes D7, D10).</summary>
    public int Living(IApocalypseWorld w) => w.Ledger.Units.Count(u => Apocalypse.Ours(u, Active)) + _pending.Values.Sum();

    UnitLifetime Life(IApocalypseWorld w, DateTime utcNow) => RollingLifetime.Of(utcNow, Active.Instance.EndsUtc, w.GraceSeconds, w.DrainMarginSeconds);

    int Queue(IApocalypseWorld w, UnitEntry unit, int count, int wave, float x, float z, float radius, DateTime utcNow)
    {
        if (count <= 0) return 0;
        // Each unit hunts from its own position (Services/HuntAction, step 3): a roamer over the seed reach (100 m), a horde
        // unit over huntRange; the tag's centre names its group (SpawnTracker S-14).
        var hunt = new HuntTag(x, z, Apocalypse.HotspotOfWave(wave) is not null ? HuntingGrounds.SeedReach : Action.Roamers.HuntRange);
        var r = w.Ledger.Request(unit.Prefab, Id, count, Life(w, utcNow), SpawnTuning.TuningFrom(unit.Modifiers), i => w.Place(x, z, radius, i, count),
            hunt: hunt, wave: wave);
        Tally.Add(r.Queued);
        if (r.Queued > 0) _pending[wave] = PendingOf(wave) + r.Queued;
        return r.Queued;
    }

    // The orders queued and not yet confirmed or failed, per ledger wave: the ledger lists only confirmed units, so a top-up
    // between a request and its confirmation would otherwise queue the same roamers twice.
    readonly Dictionary<int, int> _pending = [];

    int PendingOf(int wave) => _pending.TryGetValue(wave, out var n) ? n : 0;

    /// <summary>An order of the run spawned (SpawnTracker's confirmation, step 3): no longer pending, alive in the ledger.</summary>
    public void Spawned(int wave)
    {
        if (PendingOf(wave) > 0) _pending[wave] = PendingOf(wave) - 1;
        Tally.Spawned();
    }

    /// <summary><paramref name="n"/> orders of the run failed to spawn or were cancelled by an end (D15).</summary>
    public void FailedToSpawn(int wave, int n = 1)
    {
        _pending[wave] = Math.Max(0, PendingOf(wave) - n);
        Tally.FailedToSpawn(n);
    }

    /// <summary>The run's living or waiting units of ledger wave <paramref name="wave"/>: its tracked units spawned since
    /// the start, and its orders not yet confirmed.</summary>
    public int LivingOf(IApocalypseWorld w, int wave) => w.Ledger.Units.Count(u => u.Wave == wave && Apocalypse.Ours(u, Active)) + PendingOf(wave);

    /// <summary>The living roamers per hotspot (spread roamers counted at their nearest hotspot, D3, D4).</summary>
    public int[] RoamersPerHotspot(IApocalypseWorld w) =>
        Hotspots.Select(h => LivingOf(w, Apocalypse.RoamerWave(h.Number))).ToArray();

    int FreeTracked(IApocalypseWorld w) => Math.Max(0, w.Ledger.Limits.MaxTracked - w.Ledger.Occupied);

    /// <summary>Tops every hotspot up to perHotspot living roamers (D3); returns one line per hotspot that got any.</summary>
    public IReadOnlyList<string> TopUp(IApocalypseWorld w, DateTime utcNow)
    {
        TopUps++;
        var lines = new List<string>();
        var plan = RoamerPlan.TopUp(Action.Roamers.PerHotspot, RoamersPerHotspot(w), Action.Roamers.Max, FreeTracked(w));
        for (var h = 0; h < plan.Length; h++)
        {
            var queued = 0;
            for (var i = 0; i < plan[h]; i++)
                queued += Queue(w, RoamerPlan.Draw(Action, w.Random), 1, Apocalypse.RoamerWave(Hotspots[h].Number), Hotspots[h].X, Hotspots[h].Z,
                    Apocalypse.RoamerSpawnRadius, utcNow);
            if (queued > 0) lines.Add(RoamerPlan.Line(Id, queued, Hotspots[h].Number));
        }
        return lines;
    }

    /// <summary>The spread of one death (D4, undead-hordes D7): for a qualifying kill, a draw of perKillMin..perKillMax roamers
    /// from the spread pool within 3 m of the victim, each joining the hotspot nearest its spawn point; returns the spread
    /// line, or null when nothing spread.</summary>
    public string? SpreadOn(IApocalypseWorld w, ScoreSides sides, (float X, float Z) victim, DateTime utcNow) => SpreadOn(w, sides, victim, utcNow, out _);

    /// <summary><see cref="SpreadOn(IApocalypseWorld, ScoreSides, (float X, float Z), DateTime)"/>, with the draw's cut by
    /// the free MaxTrackedUnits slots (undead-hordes D12) in <paramref name="cut"/>: of what roamers.max and the minute
    /// limit allowed, so raising the cfg would queue them.</summary>
    public string? SpreadOn(IApocalypseWorld w, ScoreSides sides, (float X, float Z) victim, DateTime utcNow, out CapCut? cut)
    {
        cut = null;
        if (!SpreadGate.Qualifies(sides, Active)) return null;
        // the block's max bounds an apocalypse's live roamers, and every other mode's living count (undead-hordes D7, D10)
        var block = SpreadBlock;
        var live = Mode == ApocalypseMode.Apocalypse ? RoamersPerHotspot(w).Sum() : Living(w);
        var drawn = SpreadGate.Draw(block, w.Random);
        var free = FreeTracked(w);
        var n = Spread.Allow(utcNow, block, live, free, drawn);
        var allowed = Spread.Allow(utcNow, block, live, int.MaxValue, drawn);
        if (free < allowed) cut = Active.LastCut = new CapCut(n, allowed, "MaxTrackedUnits", w.Ledger.Limits.MaxTracked);
        if (n <= 0) return null;
        // a horde march's growth joins its horde before arrival, and its spread infests round the arrival point after
        var wave = Mode != ApocalypseMode.HordeMarch ? Apocalypse.RoamerWave(RoamerPlan.NearestHotspot(Hotspots, victim.X, victim.Z))
            : Arrived ? Apocalypse.RoamerWave(1) : Apocalypse.GrowthWave(1);
        var queued = 0;
        for (var i = 0; i < n; i++)
            queued += Queue(w, RoamerPlan.Spread(Action, block, w.Random), 1, wave, victim.X, victim.Z, Apocalypse.SpreadRadius, utcNow);
        Spread.Queued(utcNow, queued);
        return queued == 0 ? null
            : SpreadGate.Line(Id, queued, live + queued, block.Max, Spread.InWindow(utcNow), block.PerMinute);
    }

    /// <summary>The horde due now, risen (D5, D36): its units queued at its hotspot (its bosses as their own ledger waves);
    /// a horde that queued none has not risen and waits 60 s. A rise tops the roamers up once (D3) unless
    /// <paramref name="topUp"/> is false (a resume's catch-up rises top up once, after the rebuild, D12).</summary>
    public HordeRise? Rise(IApocalypseWorld w, DateTime utcNow, out IReadOnlyList<string> roamerLines, bool topUp = true)
    {
        roamerLines = [];
        if (Mode == ApocalypseMode.Infestation)
        {
            // an infestation rises by its top-up at the start; one that queued nothing (the caps full) tries again each
            // minute until it queues (code review step 3)
            if (Tally.Queued == 0 && (LastTryUtc is not { } tried || utcNow - tried >= Apocalypse.RiseRetry))
                roamerLines = InfestTopUp(w, utcNow);
            return null;
        }
        var k = HordeSchedule.Due(Active.Instance.StartedUtc, Active.Instance.EndsUtc, Action.Hordes, Risen, LastTryUtc, utcNow);
        return k is { } due ? RiseHorde(w, due, utcNow, out roamerLines, topUp) : null;
    }

    /// <summary>An infestation's top-up, at the start and on each retry: the attempt is recorded so a top-up that queued
    /// none waits a minute before the next (code review step 3).</summary>
    public IReadOnlyList<string> InfestTopUp(IApocalypseWorld w, DateTime utcNow)
    {
        LastTryUtc = utcNow;
        return TopUp(w, utcNow);
    }

    HordeRise? RiseHorde(IApocalypseWorld w, int k, DateTime utcNow, out IReadOnlyList<string> roamerLines, bool topUp)
    {
        roamerLines = [];
        var hotspot = Hotspots[HordeSchedule.HotspotOf(k, Hotspots.Count) - 1];
        var log = new List<string>();
        int asked;
        IReadOnlyList<(UnitEntry Unit, int Entry)> units;
        List<string> slots;
        if (Mode == ApocalypseMode.HordeMarch)
        {
            // undead-hordes D10: the entries' own counts and the bosses rolled once, each boss its own slot
            var bosses = HordeSchedule.MarchBosses(Action, w.Random);
            units = HordeSchedule.MarchUnits(Action, bosses, w.Ledger.Limits.MaxPerWave, w.Ledger.Occupied, w.Ledger.Limits.MaxTracked, w.Random, log, out asked);
            slots = bosses.Select(b => b.Prefab).ToList();
        }
        else
        {
            asked = HordeSchedule.Size(Action.Hordes, k) + (k == Action.Hordes.Count ? (Action.Bosses ?? []).Sum(b => b.Count) : 0);
            units = HordeSchedule.Units(Action, k, w.Ledger.Limits.MaxPerWave, w.Ledger.Occupied, w.Ledger.Limits.MaxTracked, w.Random, log);
            slots = units.Any(u => u.Entry > 0) ? (Action.Bosses ?? []).Select(b => b.Prefab).ToList() : [];
        }
        var cut = Precedence.Cut(asked, w.Ledger.Limits.MaxPerWave, w.Ledger.Occupied, w.Ledger.Limits.MaxTracked);   // undead-hordes D12
        if (cut is not null) Active.LastCut = cut;
        var (queued, bossesQueued) = (0, 0);
        foreach (var (unit, entry) in units)
        {
            var n = Queue(w, unit, unit.Count, entry == 0 ? Apocalypse.HordeWave(k) : Apocalypse.BossWave(k, entry), hotspot.X, hotspot.Z,
                Apocalypse.RoamerSpawnRadius, utcNow);
            queued += n;
            if (entry > 0) bossesQueued += n;
        }
        if (queued == 0)
        {
            LastTryUtc = utcNow;                          // D5: a full cap never fakes a risen (or defeated) horde
            return null;
        }
        Risen = k;
        LastTryUtc = null;
        _fronts[k] = new Front(k, hotspot.X, hotspot.Z, Goal.X, Goal.Z) { BossPrefabs = slots };
        if (topUp && Mode == ApocalypseMode.Apocalypse) roamerLines = TopUp(w, utcNow);
        return new HordeRise(k, hotspot.Number, queued, log, cut, bossesQueued);
    }

    /// <summary>True when horde <paramref name="horde"/> has risen and has no unit and no order left, bosses included.</summary>
    public bool Cleared(IApocalypseWorld w, int horde) =>
        horde <= Risen && ApocalypseOutcome.HordeCleared(w.Ledger, Id, horde, Active.Instance.StartedUtc);

    /// <summary>The run's outcome now (D8) over <paramref name="living"/> units' positions by horde.</summary>
    public ApocalypseResult Outcome(IApocalypseWorld w, Func<int, IReadOnlyList<(float X, float Z)>> living, DateTime utcNow)
    {
        if (Mode != ApocalypseMode.Apocalypse)
        {
            // undead-hordes D9, D11: won once every unit has died (an infestation's after its rise, a horde march's after its
            // horde rose) with no order pending; survived when the duration ends first; never lost
            // code review step 3: a rise whose orders all failed, or a top-up that queued none, is no win
            var alive = Living(w);
            if (Tally.Confirmed > 0 || w.Ledger.Units.Any(u => Apocalypse.Ours(u, Active))) _lived = true;   // spawned, not merely queued
            var rose = Mode == ApocalypseMode.Infestation ? Tally.Queued > 0 : Risen >= 1;
            if (rose && _lived && alive == 0) return ApocalypseResult.Won;
            return utcNow >= Active.Instance.EndsUtc ? ApocalypseResult.Survived : ApocalypseResult.None;
        }
        var states = _fronts.Values.Where(f => !Cleared(w, f.Horde)).Select(f => new HordeState(f.Horde, f, living(f.Horde))).ToList();
        return ApocalypseOutcome.Decide(Action, (Goal.X, Goal.Z), states, Risen, k => Cleared(w, k), utcNow, Active.Instance.EndsUtc);
    }

    /// <summary>The resume record now (D11): the resolved goal, hotspots, hordes risen, each living horde's front, living
    /// count and living bosses per entry (with its prefab), the living roamers per hotspot, the spread window and
    /// <paramref name="scores"/>.</summary>
    public ApocalypseRecord Record(IApocalypseWorld w, IReadOnlyList<ApocalypseScore> scores, DateTime utcNow)
    {
        var record = new ApocalypseRecord
        {
            EventId = Id,
            StartedUtc = Active.Instance.StartedUtc,
            EndsUtc = Active.Instance.EndsUtc,
            Goal = new RecordPoint { X = Goal.X, Y = Goal.Y, Z = Goal.Z },
            Hotspots = Hotspots.Select(h => new RecordSpot { X = h.X, Z = h.Z }).ToList(),
            HordesRisen = Risen,
            Roamers = RoamersPerHotspot(w).ToList(),
            SpreadWindow = Spread.InWindow(utcNow),
            Scores = scores.Select(s => new RecordScore { PlatformId = s.PlatformId, Kills = s.Kills, Deaths = s.Deaths, First = s.First }).ToList(),
            Mode = Mode == ApocalypseMode.Apocalypse ? null : Mode.ToString(),          // undead-hordes D17; an apocalypse's stays 0.13's
            Arrived = Arrived,
            Arrival = ArrivedAt is { } at ? new RecordPoint { X = at.X, Z = at.Z } : null,
        };
        foreach (var f in _fronts.Values.OrderBy(f => f.Horde))
        {
            var livingBosses = f.BossPrefabs.Select((prefab, j) => new RecordBoss { Prefab = prefab, Living = LivingOf(w, Apocalypse.BossWave(f.Horde, j + 1)) }).ToList();
            var growth = LivingOf(w, Apocalypse.GrowthWave(f.Horde));
            var living = LivingOf(w, Apocalypse.HordeWave(f.Horde)) + growth + livingBosses.Sum(b => b.Living);
            if (living == 0) continue;
            record.Hordes.Add(new RecordHorde { Number = f.Horde, FrontX = f.X, FrontZ = f.Z, Living = living, LivingBosses = livingBosses, Growth = growth });
        }
        return record;
    }

    /// <summary>A run resumed from <paramref name="record"/> (D12): its hotspots, goal, hordes risen, fronts (from each
    /// living horde's hotspot to the recorded goal, at the recorded point) and spread window; nothing is queued yet.</summary>
    public static ApocalypseRun Resumed(ActiveEvent active, ApocalypseRecord record, DateTime utcNow)
    {
        var spots = record.Hotspots.Select((h, i) => new Hotspot(i + 1, h.X, h.Z, 0, (int)MathF.Floor(h.X / Apocalypse.CellSize), (int)MathF.Floor(h.Z / Apocalypse.CellSize))).ToList();
        var run = new ApocalypseRun(active, spots, (record.Goal!.X, record.Goal.Y, record.Goal.Z)) { Risen = record.HordesRisen };
        if (record.Arrived && record.Arrival is { } at) run.Arrive(at.X, at.Z);                     // undead-hordes D17
        foreach (var h in record.Hordes)
        {
            var origin = spots[HordeSchedule.HotspotOf(h.Number, spots.Count) - 1];
            var front = Front.At(h.Number, origin.X, origin.Z, record.Goal.X, record.Goal.Z, h.FrontX, h.FrontZ);
            foreach (var s in Apocalypse.ProgressSteps) if (front.Percent >= s) front.Announced.Add(s);      // a step is announced once (D7)
            front.BossPrefabs = h.LivingBosses.Select(b => b.Prefab).ToList();
            run._fronts[h.Number] = front;
        }
        run.Spread.Restore(utcNow, record.SpreadWindow);
        if (run.Mode == ApocalypseMode.Infestation) run.LastTryUtc = utcNow;    // the resume counts as a try: the next waits a minute
        return run;
    }

    /// <summary>The rebuild of a resumed run (D12), once the drain gate proceeds: every <see cref="ResumeRule.Rebuild"/>
    /// order queued through the ledger, then each missed horde risen in order (each once, none topping up), then one roamer
    /// top-up. Returns the units the rebuild queued, the catch-up rises and the top-up's lines.</summary>
    public (int Rebuilt, IReadOnlyList<HordeRise> CatchUp, IReadOnlyList<string> TopUpLines) Rebuild(IApocalypseWorld w, ApocalypseRecord record, DateTime utcNow)
    {
        var rebuilt = 0;
        foreach (var o in ResumeRule.Rebuild(record, Action))
        {
            if (o.Prefab is { } prefab) rebuilt += Queue(w, new UnitEntry(prefab, o.Count, 1.0, o.Modifiers), o.Count, o.Wave, o.X, o.Z, Apocalypse.RoamerSpawnRadius, utcNow);
            else
                for (var i = 0; i < o.Count; i++)
                {
                    var one = Queue(w, RebuildUnit(o.Wave, w.Random) with { Count = 1 }, 1, o.Wave, o.X, o.Z, Apocalypse.RoamerSpawnRadius, utcNow);
                    if (one == 0) break;                                       // the caps refused it: the rest would be refused too
                    rebuilt += one;
                }
        }
        var rises = new List<HordeRise>();
        foreach (var k in ResumeRule.CatchUp(record, Action, utcNow))
        {
            if (k != Risen + 1) break;
            if (RiseHorde(w, k, utcNow, out _, topUp: false) is { } rise) rises.Add(rise);
            else break;
        }
        // the roamers top up once after the catch-up rises, never when none rose: a plain resume rebuilds exactly the
        // recorded units (D12; session 2 rebuilt 21 and topped up 1 more)
        IReadOnlyList<string> lines = rises.Count > 0 ? TopUp(w, utcNow) : [];
        return (rebuilt, rises, lines);
    }

    /// <summary>A rebuilt unit of no recorded prefab (D12; undead-hordes D17): a horde march's growth unit from its growth
    /// block and its spread unit from its infest block, any other drawn from `units` by weight, as 0.13 does.</summary>
    UnitEntry RebuildUnit(int wave, IRandom rng) =>
        Mode != ApocalypseMode.HordeMarch ? RoamerPlan.Draw(Action, rng)
        : wave > Apocalypse.GrowthWaveBase && wave <= Apocalypse.GrowthWaveBase + ApocalypseLimits.MaxHordes ? RoamerPlan.Spread(Action, Action.March!.Growth, rng)
        : Apocalypse.HotspotOfWave(wave) is not null ? RoamerPlan.Spread(Action, Action.March!.InfestSpread, rng)
        : RoamerPlan.Draw(Action, rng);

    /// <summary>The admin view of the live run for `event info` (D17): each hotspot's region and x, z, each living horde's
    /// front x, z and progress, and the tally line.</summary>
    public ApocalypseView View(IApocalypseWorld w, Func<float, float, string> regionOf) =>
        new(Hotspots.Select(h => (h.Number, regionOf(h.X, h.Z), h.X, h.Z)).ToList(),
            _fronts.Values.Where(f => !Cleared(w, f.Horde)).OrderBy(f => f.Horde).Select(f => (f.Horde, f.X, f.Z, f.Percent)).ToList(),
            Risen, Tally.Line(Id, w.Ledger.Units.Count(u => Apocalypse.Ours(u, Active))), RoamersPerHotspot(w).Sum())
        {
            Living = Living(w),
            Places = Origins.Count,
            ToGoal = Mode == ApocalypseMode.HordeMarch && !Arrived && _fronts.Values.FirstOrDefault() is { } f
                ? (int)Math.Round(Apocalypse.Distance(f.X, f.Z, Goal.X, Goal.Z)) : null,
            Arrived = Arrived,
            AdminStart = Action.March?.Start.Location?.Type == LocationType.Admin,
            AdminGoal = Action.Goal.Location.Type == LocationType.Admin,
        };
}

/// <summary>What `event info` and `.nyar status` show of a live apocalypse (D17): hotspots with their region and x, z
/// (admins only), the living fronts with their progress, hordes risen, the tally line and the living roamers.</summary>
public sealed record ApocalypseView(
    IReadOnlyList<(int Number, string Region, float X, float Z)> Hotspots,
    IReadOnlyList<(int Horde, float X, float Z, int Percent)> Fronts,
    int Risen,
    string TallyLine,
    int Roamers)
{
    /// <summary>The farthest living front's progress, the status line's percent (0 with none marching).</summary>
    public int Percent => Fronts.Count == 0 ? 0 : Fronts.Max(f => f.Percent);

    /// <summary>The run's living count, its infestation's places, a marching horde's metres left to its destination (null
    /// once it arrived, or for another mode) and whether it arrived (undead-hordes D20).</summary>
    public int Living { get; init; }
    public int Places { get; init; }
    public int? ToGoal { get; init; }
    public bool Arrived { get; init; }

    /// <summary>A horde march that rose at the starting admin's position: `event info` names neither it nor its front's
    /// point (A5).</summary>
    public bool AdminStart { get; init; }

    /// <summary>A run marching on the starting admin's position: `event info` names no front's point, which ends there (A5).</summary>
    public bool AdminGoal { get; init; }
}
