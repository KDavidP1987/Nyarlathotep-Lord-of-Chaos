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

    public static int RoamerWave(int hotspot) => RoamerWaveBase + hotspot;
    public static int HordeWave(int horde) => horde;
    public static int BossWave(int horde, int entry) => BossWaveBase + horde * 10 + entry;

    /// <summary>The hotspot (1-based) a roamer's ledger wave names, or null.</summary>
    public static int? HotspotOfWave(int wave) => wave is > RoamerWaveBase and <= RoamerWaveBase + ApocalypseLimits.MaxHotspots ? wave - RoamerWaveBase : null;

    /// <summary>The horde (1-based) a horde unit's or boss's ledger wave names, or null for a roamer.</summary>
    public static int? HordeOfWave(int wave) =>
        wave is >= 1 and <= ApocalypseLimits.MaxHordes ? wave
        : wave > BossWaveBase ? (wave - BossWaveBase) / 10 is var k and >= 1 and <= ApocalypseLimits.MaxHordes ? k : null
        : null;

    /// <summary>The `bosses` entry (1-based) a boss's ledger wave names, or null.</summary>
    public static int? BossEntryOfWave(int wave) => wave > BossWaveBase && (wave - BossWaveBase) % 10 is var j and >= 1 and <= EventValidator.MaxBosses ? j : null;

    /// <summary>The goal fixed at the start (D11, S-10): a Point goal's coordinates (height 0 when none is stored), or an
    /// Admin goal's starting admin position, read once; null for an Admin goal without one (the start is refused).</summary>
    public static (float X, float Y, float Z)? ResolveGoal(ApocalypseGoal goal, (float X, float Y, float Z)? origin) =>
        goal.Location.Type == LocationType.Admin ? origin : (goal.Location.X, goal.Location.Y ?? 0f, goal.Location.Z);

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

    // <native-read>
    /// <summary>The hotspots of <paramref name="action"/> over the natives <paramref name="read"/> returns. A throwing read
    /// refuses the start with <see cref="ReadFailed"/>; fewer than one hotspot with <see cref="NoHotspot"/>.</summary>
    public static HotspotPick Pick(string eventId, ApocalypseAction action, Func<IEnumerable<HotspotNative>> read)
    {
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
            var failed = ReadFailed(e.Message);
            return new HotspotPick([], failed, $"apocalypse {eventId}: {failed}");
        }
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
            var none = NoHotspot(action.Hotspots.MinNatives, action.Faction);
            return new HotspotPick([], none, $"apocalypse {eventId}: {none}");
        }
        return new HotspotPick(spots, null, Line(eventId, spots));
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
        Func<float, float, bool> claimed, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf, WalkBudget budget) =>
        JumpRule.Plan(u, target, online, (x, z) => !claimed(x, z), walkable, heightOf, budget);

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
/// The spread (D4, Business rules 4; design §9 D44): a victim, player or NPC not ours, whose killer resolves (the unit,
/// else its EntityOwner, as the scoreboard does) to a living unit of the running apocalypse spawned since its start queues
/// perKill roamers within 3 m of the victim; none while live roamers are at roamers.max, while perMinute spread units were
/// queued in the last 60 s, or while MaxTrackedUnits is full.
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

    /// <summary>How many spread roamers one qualifying kill may queue now: min(perKill, roamers.max − live roamers, perMinute
    /// − the window, the free tracked slots), at least 0.</summary>
    public int Allow(DateTime utcNow, ApocalypseRoamers roamers, int liveRoamers, int freeTracked) =>
        Math.Max(0, Math.Min(Math.Min(roamers.PerKill, roamers.Max - liveRoamers), Math.Min(roamers.PerMinute - InWindow(utcNow), freeTracked)));

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
        for (var i = 0; i < Math.Min(regular, room); i++) result.Add((RoamerPlan.Draw(action.Units, rng) with { Count = 1 }, 0));
        return result;
    }

    /// <summary>"&lt;n&gt; units of horde &lt;k&gt; of &lt;c&gt;"-style counts for the rise line.</summary>
    public static int Count(IReadOnlyList<(UnitEntry Unit, int Entry)> units) => units.Sum(u => u.Unit.Count);
}

/// <summary>A risen horde's front (D6): the point moving from its hotspot to the goal at marchSpeed m/s.</summary>
public sealed class Front(int horde, float originX, float originZ, float goalX, float goalZ)
{
    public int Horde { get; } = horde;
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
    /// <paramref name="line"/> is the held line, once per hold.</summary>
    public static MarchStep Step(Front front, int goalRadius, double speed, double seconds, IReadOnlyList<(float X, float Z)> players,
        Func<float, float, bool> walkable, DateTime utcNow, out string? line)
    {
        line = null;
        if (players.Any(p => Apocalypse.Distance(p.X, p.Z, front.X, front.Z) <= Apocalypse.WatchRange)) front.LastWatchedUtc = utcNow;
        var left = Apocalypse.Distance(front.X, front.Z, front.GoalX, front.GoalZ);
        if (left <= 0) return MarchStep.AtGoal;
        var nearGoal = left <= goalRadius + Apocalypse.GoalHoldMargin;
        if (!nearGoal && players.Any(p => Apocalypse.Distance(p.X, p.Z, front.X, front.Z) <= Apocalypse.HoldRange)) return MarchStep.HeldByPlayer;
        var step = (float)Math.Min(speed * Math.Max(0, seconds), left);
        var nx = front.X + (front.GoalX - front.X) / left * step;
        var nz = front.Z + (front.GoalZ - front.Z) / left * step;
        if (!walkable(nx, nz))
        {
            if (!front.HeldNoGround) line = HeldLine(front.Horde);
            front.HeldNoGround = true;
            return MarchStep.HeldNoGround;
        }
        front.HeldNoGround = false;
        front.X = nx;
        front.Z = nz;
        return Apocalypse.Distance(nx, nz, front.GoalX, front.GoalZ) <= 0 ? MarchStep.AtGoal : MarchStep.Moved;
    }

    /// <summary>"horde &lt;k&gt; held: no walkable ground ahead" (D6).</summary>
    public static string HeldLine(int horde) => $"horde {horde} held: no walkable ground ahead";

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

    /// <summary>The units more than 30 m from the front, the ones a hop moves while the horde is unobserved; none while it is
    /// observed (D6, S-9: our own units only, never while a player is near).</summary>
    public static IReadOnlyList<long> Stragglers(Front front, IEnumerable<(long Key, float X, float Z)> units, DateTime utcNow) =>
        !IsUnobserved(front, utcNow) ? []
        : units.Where(u => Apocalypse.Distance(u.X, u.Z, front.X, front.Z) > Apocalypse.KeepUpRange).Select(u => u.Key).ToList();

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

    /// <summary>"event &lt;id&gt; ended: &lt;won|lost|survived&gt; (&lt;r&gt; of &lt;c&gt; hordes risen)" (D8).</summary>
    public static string Line(string eventId, ApocalypseResult result, int risen, int count) =>
        $"event {eventId} ended: {result.ToString().ToLowerInvariant()} ({risen} of {count} hordes risen)";

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
        && Enumerable.Range(1, EventValidator.MaxBosses).All(j => ledger.WaveCleared(eventId, Apocalypse.BossWave(horde, j), startedUtc));
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

    /// <summary>The definition changed kind, has fewer hordes than had risen, or lacks a recorded boss prefab (D12).</summary>
    public static bool Changed(ApocalypseRecord record, EventDefinition def)
    {
        if (def.Apocalypse is not { } a) return true;
        if (a.Hordes.Count < record.HordesRisen) return true;
        var bosses = a.Bosses ?? [];
        return record.Hordes.SelectMany(h => h.LivingBosses).Any(b => b.Living > 0 && bosses.All(e => e.Prefab != b.Prefab));
    }

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
        for (var h = 0; h < record.Hotspots.Count; h++)
            if (record.Roamers[h] > 0)
                orders.Add(new RebuildOrder(Apocalypse.RoamerWave(h + 1), null, record.Roamers[h], null, record.Hotspots[h].X, record.Hotspots[h].Z));
        foreach (var horde in record.Hordes.OrderBy(x => x.Number))
        {
            var bosses = 0;
            for (var j = 0; j < horde.LivingBosses.Count; j++)
            {
                var b = horde.LivingBosses[j];
                if (b.Living <= 0) continue;
                // by entry index first: two entries may share a prefab with different modifiers (review L2)
                var entry = action.Bosses is { } list && j < list.Count && list[j].Prefab == b.Prefab ? list[j]
                    : action.Bosses?.FirstOrDefault(e => e.Prefab == b.Prefab);
                orders.Add(new RebuildOrder(Apocalypse.BossWave(horde.Number, j + 1), b.Prefab, b.Living, entry?.Modifiers, horde.FrontX, horde.FrontZ));
                bosses += b.Living;
            }
            var regular = horde.Living - bosses;
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

/// <summary>A rise of horde k (D5, D7): its number, hotspot, queued count, and the clamp lines.</summary>
public sealed record HordeRise(int Horde, int Hotspot, int Queued, IReadOnlyList<string> Log);

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

    public string Id => Active.Id;

    public ApocalypseRun(ActiveEvent active, IReadOnlyList<Hotspot> hotspots, (float X, float Y, float Z) goal)
    {
        Active = active;
        Action = active.Definition.Apocalypse ?? throw new ArgumentException("not an apocalypse", nameof(active));
        Hotspots = hotspots;
        Goal = goal;
    }

    UnitLifetime Life(IApocalypseWorld w, DateTime utcNow) => RollingLifetime.Of(utcNow, Active.Instance.EndsUtc, w.GraceSeconds, w.DrainMarginSeconds);

    int Queue(IApocalypseWorld w, UnitEntry unit, int count, int wave, float x, float z, float radius, DateTime utcNow)
    {
        if (count <= 0) return 0;
        var r = w.Ledger.Request(unit.Prefab, Id, count, Life(w, utcNow), SpawnTuning.TuningFrom(unit.Modifiers), i => w.Place(x, z, radius, i, count), wave: wave);
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
                queued += Queue(w, RoamerPlan.Draw(Action.Units, w.Random), 1, Apocalypse.RoamerWave(Hotspots[h].Number), Hotspots[h].X, Hotspots[h].Z,
                    Apocalypse.RoamerSpawnRadius, utcNow);
            if (queued > 0) lines.Add(RoamerPlan.Line(Id, queued, Hotspots[h].Number));
        }
        return lines;
    }

    /// <summary>The spread of one death (D4): for a qualifying kill, up to perKill roamers within 3 m of the victim, each
    /// joining the hotspot nearest its spawn point; returns the spread line, or null when nothing spread.</summary>
    public string? SpreadOn(IApocalypseWorld w, ScoreSides sides, (float X, float Z) victim, DateTime utcNow)
    {
        if (!SpreadGate.Qualifies(sides, Active)) return null;
        var live = RoamersPerHotspot(w).Sum();
        var n = Spread.Allow(utcNow, Action.Roamers, live, FreeTracked(w));
        if (n <= 0) return null;
        var hotspot = RoamerPlan.NearestHotspot(Hotspots, victim.X, victim.Z);
        var queued = 0;
        for (var i = 0; i < n; i++)
            queued += Queue(w, RoamerPlan.Draw(Action.Units, w.Random), 1, Apocalypse.RoamerWave(hotspot), victim.X, victim.Z, Apocalypse.SpreadRadius, utcNow);
        Spread.Queued(utcNow, queued);
        return queued == 0 ? null
            : SpreadGate.Line(Id, queued, live + queued, Action.Roamers.Max, Spread.InWindow(utcNow), Action.Roamers.PerMinute);
    }

    /// <summary>The horde due now, risen (D5, D36): its units queued at its hotspot (its bosses as their own ledger waves);
    /// a horde that queued none has not risen and waits 60 s. A rise tops the roamers up once (D3) unless
    /// <paramref name="topUp"/> is false (a resume's catch-up rises top up once, after the rebuild, D12).</summary>
    public HordeRise? Rise(IApocalypseWorld w, DateTime utcNow, out IReadOnlyList<string> roamerLines, bool topUp = true)
    {
        roamerLines = [];
        var k = HordeSchedule.Due(Active.Instance.StartedUtc, Active.Instance.EndsUtc, Action.Hordes, Risen, LastTryUtc, utcNow);
        return k is { } due ? RiseHorde(w, due, utcNow, out roamerLines, topUp) : null;
    }

    HordeRise? RiseHorde(IApocalypseWorld w, int k, DateTime utcNow, out IReadOnlyList<string> roamerLines, bool topUp)
    {
        roamerLines = [];
        var hotspot = Hotspots[HordeSchedule.HotspotOf(k, Hotspots.Count) - 1];
        var log = new List<string>();
        var units = HordeSchedule.Units(Action, k, w.Ledger.Limits.MaxPerWave, w.Ledger.Occupied, w.Ledger.Limits.MaxTracked, w.Random, log);
        var queued = 0;
        foreach (var (unit, entry) in units)
            queued += Queue(w, unit, unit.Count, entry == 0 ? Apocalypse.HordeWave(k) : Apocalypse.BossWave(k, entry), hotspot.X, hotspot.Z,
                Apocalypse.RoamerSpawnRadius, utcNow);
        if (queued == 0)
        {
            LastTryUtc = utcNow;                          // D5: a full cap never fakes a risen (or defeated) horde
            return null;
        }
        Risen = k;
        LastTryUtc = null;
        _fronts[k] = new Front(k, hotspot.X, hotspot.Z, Goal.X, Goal.Z)
        {
            BossPrefabs = units.Any(u => u.Entry > 0) ? (Action.Bosses ?? []).Select(b => b.Prefab).ToList() : [],
        };
        if (topUp) roamerLines = TopUp(w, utcNow);
        return new HordeRise(k, hotspot.Number, queued, log);
    }

    /// <summary>True when horde <paramref name="horde"/> has risen and has no unit and no order left, bosses included.</summary>
    public bool Cleared(IApocalypseWorld w, int horde) =>
        horde <= Risen && ApocalypseOutcome.HordeCleared(w.Ledger, Id, horde, Active.Instance.StartedUtc);

    /// <summary>The run's outcome now (D8) over <paramref name="living"/> units' positions by horde.</summary>
    public ApocalypseResult Outcome(IApocalypseWorld w, Func<int, IReadOnlyList<(float X, float Z)>> living, DateTime utcNow)
    {
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
        };
        foreach (var f in _fronts.Values.OrderBy(f => f.Horde))
        {
            var livingBosses = f.BossPrefabs.Select((prefab, j) => new RecordBoss { Prefab = prefab, Living = LivingOf(w, Apocalypse.BossWave(f.Horde, j + 1)) }).ToList();
            var living = LivingOf(w, Apocalypse.HordeWave(f.Horde)) + livingBosses.Sum(b => b.Living);
            if (living == 0) continue;
            record.Hordes.Add(new RecordHorde { Number = f.Horde, FrontX = f.X, FrontZ = f.Z, Living = living, LivingBosses = livingBosses });
        }
        return record;
    }

    /// <summary>A run resumed from <paramref name="record"/> (D12): its hotspots, goal, hordes risen, fronts (from each
    /// living horde's hotspot to the recorded goal, at the recorded point) and spread window; nothing is queued yet.</summary>
    public static ApocalypseRun Resumed(ActiveEvent active, ApocalypseRecord record, DateTime utcNow)
    {
        var spots = record.Hotspots.Select((h, i) => new Hotspot(i + 1, h.X, h.Z, 0, (int)MathF.Floor(h.X / Apocalypse.CellSize), (int)MathF.Floor(h.Z / Apocalypse.CellSize))).ToList();
        var run = new ApocalypseRun(active, spots, (record.Goal!.X, record.Goal.Y, record.Goal.Z)) { Risen = record.HordesRisen };
        foreach (var h in record.Hordes)
        {
            var origin = spots[HordeSchedule.HotspotOf(h.Number, spots.Count) - 1];
            var front = Front.At(h.Number, origin.X, origin.Z, record.Goal.X, record.Goal.Z, h.FrontX, h.FrontZ);
            foreach (var s in Apocalypse.ProgressSteps) if (front.Percent >= s) front.Announced.Add(s);      // a step is announced once (D7)
            front.BossPrefabs = h.LivingBosses.Select(b => b.Prefab).ToList();
            run._fronts[h.Number] = front;
        }
        run.Spread.Restore(utcNow, record.SpreadWindow);
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
                    rebuilt += Queue(w, RoamerPlan.Draw(Action.Units, w.Random) with { Count = 1 }, 1, o.Wave, o.X, o.Z, Apocalypse.RoamerSpawnRadius, utcNow);
        }
        var rises = new List<HordeRise>();
        foreach (var k in ResumeRule.CatchUp(record, Action, utcNow))
        {
            if (k != Risen + 1) break;
            if (RiseHorde(w, k, utcNow, out _, topUp: false) is { } rise) rises.Add(rise);
            else break;
        }
        var lines = TopUp(w, utcNow);
        return (rebuilt, rises, lines);
    }

    /// <summary>The admin view of the live run for `event info` (D17): each hotspot's region and x, z, each living horde's
    /// front x, z and progress, and the tally line.</summary>
    public ApocalypseView View(IApocalypseWorld w, Func<float, float, string> regionOf) =>
        new(Hotspots.Select(h => (h.Number, regionOf(h.X, h.Z), h.X, h.Z)).ToList(),
            _fronts.Values.Where(f => !Cleared(w, f.Horde)).OrderBy(f => f.Horde).Select(f => (f.Horde, f.X, f.Z, f.Percent)).ToList(),
            Risen, Tally.Line(Id, w.Ledger.Units.Count(u => Apocalypse.Ours(u, Active))), RoamersPerHotspot(w).Sum());
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
}
