using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// The Hunt behaviour (event-spawns D13). Every 5 s, for each live unit of a Hunt wave, the targets of Logic
/// HuntPlan.Targets around the wave's centre get one AggroBuffer entry each, written on our own spawned units only
/// (CLAUDE.md › Spawn &amp; buff safety). The seed record (Logic/HuntSeeds), not the buffer, says which entries are ours:
/// a recorded seed whose entry is gone is dropped, a recorded player's single entry stays ours whatever values the game
/// rewrote into it, one the game shares (more than one entry) is left to the game (A67), and a player the buffer
/// already holds is never seeded. The territory flag comes from the event's latest successful map
/// (TerritoryMap.Maps.ForHunt); while its latest build failed every seed goes and none is added (fail closed). A throw is
/// caught per event and logged "hunt &lt;id&gt;: seed failed" once per streak; the wave keeps its units (D21).
/// Hunting grounds (hunting-grounds D5, D7, S-9): while an event's region lock holds, no Hunt or RegionHunt seed goes to a
/// player standing in no scope region (Logic LockRule.SeedPool); a RegionHunt unit is seeded from its own position, on the
/// eligible players inside the scope regions within 60 m (Logic HuntRule.SeedTargets), not from its group's centre.
/// </summary>
internal static class HuntAction
{
    /// <summary>The seed's aggro value and weight (RESEARCH_NOTES › AggroBuffer: {Entity, DamageValue 500, Weight 1}
    /// makes a unit within about 60 m enter combat and walk to the target).</summary>
    const float SeedDamage = 500f;
    const float SeedWeight = 1f;

    static HuntSeeds _seeds = new();
    static readonly Dictionary<long, (Entity Unit, string EventId, HuntTag Tag)> _units = new();
    static DateTime _next;

    internal static HuntSeeds Seeds => _seeds;

    /// <summary>D24's "hunt targets: &lt;n&gt;": the distinct players HuntPlan.Targets chose across every Hunt unit at the
    /// latest tick, whether seeded then, kept or held by the game (Review 34 F3); 0 with no Hunt unit.</summary>
    internal static int LastTargets { get; private set; }
    static readonly HashSet<long> _tickTargets = new();

    /// <summary>The parts of the latest tick that ran (wave-sets A7), taken once by the scheduler; null when the tick did
    /// not run (no unit, or inside the 5 s interval).</summary>
    static HuntParts? _parts;
    static long _bufferTicks;

    internal static HuntParts? TakeParts()
    {
        var p = _parts;
        _parts = null;
        return p;
    }

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>At IsReady: a restart keeps no seed (D33).</summary>
    internal static void Initialize()
    {
        _seeds = new HuntSeeds();
        _units.Clear();
        _next = default;
        LastTargets = 0;
        _parts = null;
    }

    /// <summary>The distinct hunt centres of <paramref name="eventId"/>'s live Hunt units on x and z, one per wave group
    /// (wave-variety D8: the pursuit area of an AroundPlayer or Region run); memory only.</summary>
    internal static IReadOnlyList<(float X, float Z)> CentresOf(string eventId) =>
        _units.Values.Where(u => u.EventId == eventId).Select(u => (u.Tag.X, u.Tag.Z)).Distinct().ToList();

    /// <summary>SpawnTracker confirmed a unit of a Hunt wave.</summary>
    internal static void Track(long key, Entity unit, string eventId, HuntTag tag) => _units[key] = (unit, eventId, tag);

    /// <summary>A unit left the game (despawned, died or removed): its seeds go with it.</summary>
    internal static void Forget(long key)
    {
        _units.Remove(key);
        _seeds.ForgetUnit(key);
    }

    /// <summary>An end path of <paramref name="eventId"/> (natural end, stop, fault cancel): hunting stops with the event;
    /// its seeded entries stay on its units, which the despawn removes (Design › Data).</summary>
    internal static void EndEvent(string eventId)
    {
        foreach (var key in _units.Where(u => u.Value.EventId == eventId).Select(u => u.Key).ToList()) _units.Remove(key);
        _seeds.ForgetEvent(eventId);
        WalkCheck.Health.Recovered(SpawnFailure.HuntSeed, eventId);
    }

    /// <summary>`purge confirm`: every seed record goes.</summary>
    internal static void Clear()
    {
        foreach (var id in _units.Values.Select(u => u.EventId).Distinct().ToList()) WalkCheck.Health.Recovered(SpawnFailure.HuntSeed, id);
        _units.Clear();
        _seeds.Clear();
    }

    /// <summary>The scheduler's Hunt phase, every <see cref="HuntPlan.IntervalSeconds"/> s while a Hunt unit lives.</summary>
    [Mutating]
    internal static void Tick(DateTime now)
    {
        if (_units.Count == 0) { LastTargets = 0; return; }
        if (now < _next) return;
        _next = now.AddSeconds(HuntPlan.IntervalSeconds);
        _tickTargets.Clear();
        LastTargets = 0;                                                     // a throw past the per-event catch reads 0 (Review 35 F4)
        _parts = null;
        var netGcs = GC.CollectionCount(0);
        var il2cppGcs = Il2CppGcCount();
        long t0 = Stopwatch.GetTimestamp(), sweepEnd = t0, playersEnd = t0, verbose = 0;
        _bufferTicks = 0;

        // The sweep and the player read fail every event's tick alike, never the scheduler's (D21; A61).
        List<IGrouping<string, KeyValuePair<long, (Entity Unit, string EventId, HuntTag Tag)>>> byEvent;
        List<PlayerRow> players;
        try
        {
            foreach (var gone in _units.Where(u => !u.Value.Unit.Exists()).Select(u => u.Key).ToList()) Forget(gone);
            byEvent = _units.GroupBy(u => u.Value.EventId).ToList();
            if (byEvent.Count == 0) { LastTargets = 0; return; }
            sweepEnd = Stopwatch.GetTimestamp();
            players = PlayerQuery.Read();
            playersEnd = Stopwatch.GetTimestamp();
        }
        catch (Exception ex)
        {
            foreach (var id in _units.Values.Select(u => u.EventId).Distinct().ToList()) Failed(id, ex);
            LastTargets = 0;
            return;
        }

        foreach (var g in byEvent)
        {
            try
            {
                var map = TerritoryMap.Maps.ForHunt(g.Key);
                var targeting = Targeting(g.Key, players, map);
                int kept = 0, left = 0;
                var held = new HashSet<long>();
                foreach (var (key, (unit, _, tag)) in g.ToList())
                {
                    if (!unit.Exists()) { Forget(key); continue; }
                    var (k, l) = SeedUnit(key, unit, g.Key, tag, players, targeting, held);
                    kept += k;
                    left += l;
                }
                _tickTargets.UnionWith(held);                                // an event counts only once its whole tick held
                WalkCheck.Health.Recovered(SpawnFailure.HuntSeed, g.Key);
                if (Settings.VerboseLogging.Value)
                {
                    var verboseStart = Stopwatch.GetTimestamp();
                    // Why whom (A66), at the first unit's wave centre: counts only (Security › Personal data). A failed
                    // diagnostic never reads as a failed seed.
                    string why;
                    try
                    {
                        var (first, _, tag) = g.First().Value;
                        // a RegionHunt unit seeds from its own position over 100 m (hunting-grounds A4): counted the same way
                        why = map is null ? "no territory map, nobody targeted"
                            : targeting?.Pursuing == true && first.TryGetComponent<Translation>(out var self)   // wave-variety D8: its own position
                                ? HuntPlan.Tally(targeting.Pool.Where(p => !targeting.FocusOnly || p.Key == targeting.FocusKey).ToList(),
                                    (self.Value.x, self.Value.z), HuntingGrounds.SeedReach).ToString()
                            : targeting?.RegionHuntScope is { } scope && first.TryGetComponent<Translation>(out var at)
                                ? HuntPlan.Tally(HuntRule.EligibleInside(targeting.Pool, scope, targeting.RegionOf), (at.Value.x, at.Value.z), HuntingGrounds.SeedReach).ToString()
                                : HuntPlan.Tally(players.Select(p => Candidate(p, map)).ToList(), (tag.X, tag.Z), tag.Range).ToString();
                    }
                    catch { why = "counts unreadable"; }
                    Core.Log.LogInfo($"[nyar] hunt {g.Key}: {kept} seeds kept, {left} left to the game; at the first unit's centre: {why}");
                    verbose += Stopwatch.GetTimestamp() - verboseStart;
                }
            }
            catch (Exception ex)
            {
                Failed(g.Key, ex);
            }
        }
        LastTargets = _tickTargets.Count;
        var seed = Stopwatch.GetTimestamp() - playersEnd - verbose;
        _parts = new HuntParts(Ms(sweepEnd - t0), Ms(playersEnd - sweepEnd), Ms(seed), Ms(_bufferTicks), Ms(verbose),
            _units.Count, byEvent.Count, GC.CollectionCount(0) - netGcs, Il2CppGcDelta(il2cppGcs, Il2CppGcCount()));
    }

    /// <summary>The collections between two reads; -1 (logged "n/a") when either read failed, so an unreadable count
    /// never reads as 0 (step 2 review F1).</summary>
    internal static int Il2CppGcDelta(int start, int end) => start < 0 || end < 0 ? -1 : end - start;

    /// <summary>The game runtime's own collections (A7); -1 when it cannot be read.</summary>
    internal static int Il2CppGcCount()
    {
        try { return Il2CppSystem.GC.CollectionCount(0); }
        catch { return -1; }
    }

    static void Failed(string eventId, Exception ex)
    {
        if (WalkCheck.Health.Failing(SpawnFailure.HuntSeed, eventId)) Core.Log.LogWarning($"[nyar] hunt {eventId}: seed failed: {ex.Message}");
    }

    /// <summary>Whom one event's units may target this tick: null without a territory map (fail closed: every seed goes,
    /// D13); else the players as Hunt candidates, narrowed while the event's region lock holds to those inside its scope
    /// regions (hunting-grounds D5, S-9; none while the region index is unavailable), with the scope and region lookup a
    /// RegionHunt unit seeds by (hunting-grounds D7). Read from the running instance's own definition. A Revenge
    /// instance targets its focus player alone, resolved to a key from this tick's read (revenge D6). A pursuing run
    /// (wave-variety D8) seeds each unit from its own position within 100 m among the eligible players anywhere on the map
    /// (Pursuit.SeedTargets: RoamRule.Eligible, a focus-only run's focus player alone) and rehomes it to its nearest
    /// seeded player; the region phase's hunt moves it there by hidden jumps.</summary>
    static EventTargeting Targeting(string eventId, List<PlayerRow> players, IReadOnlySet<(int X, int Z)> map)
    {
        if (map is null) return null;
        var candidates = players.Select(p => Candidate(p, map));
        var active = EventRuntime.Engine.Active.FirstOrDefault(a => a.Id == eventId);
        // apocalypse D3, D5: its units march and roam, so each seeds from its own position over its tag's range among the
        // eligible players anywhere (RoamRule.Eligible), and is rehomed to its nearest seeded player
        if (active?.Definition.Apocalypse is not null)
            return new EventTargeting(candidates.Where(RoamRule.Eligible).ToList(), null, null, OwnPosition: true);
        var action = active?.Definition.Action;
        if (action is null) return new EventTargeting(candidates.ToList(), null, null);
        var focusOnly = WaveFacts.IsFocusOnly(active.Definition);
        var focusKey = focusOnly ? HuntPlan.FocusKey(players.Select(p => (p.Key, p.PlatformId)), active.Focus) : null;
        if (active.Pursuing) return new EventTargeting(candidates.ToList(), null, null, focusOnly, focusKey, OwnPosition: true, Pursuing: true);
        Func<float, float, string> regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : null;
        var pool = LockRule.SeedPool(action, candidates, regionOf).ToList();
        return action.Behaviour is { Type: BehaviourType.RegionHunt } && regionOf is not null
            ? new EventTargeting(pool, action.Scope, regionOf)
            : new EventTargeting(pool, null, null, focusOnly, focusKey);
    }

    /// <summary>One event's targeting for a tick (see <see cref="Targeting"/>): the candidate pool, for RegionHunt the
    /// scope and the region lookup its units are seeded by from their own positions, and for a Revenge instance its focus
    /// key (revenge D6).</summary>
    sealed record EventTargeting(List<HuntCandidate> Pool, Scope? RegionHuntScope, Func<float, float, string> RegionOf,
        bool FocusOnly = false, long? FocusKey = null, bool OwnPosition = false, bool Pursuing = false);

    /// <summary>The players to seed for one unit: a Hunt unit's within its range of its wave's centre (HuntPlan.Targets); a
    /// RegionHunt unit's within 100 m (A4) of its own position among the eligible players inside the scope regions
    /// (HuntRule.SeedTargets), none while its position cannot be read; none without a targeting (no territory map).</summary>
    static List<long> TargetsOf(Entity unit, HuntTag tag, EventTargeting targeting)
    {
        if (targeting is null) return new List<long>();
        if (targeting.Pursuing)                                              // wave-variety D8: map-wide, from its own position
            return unit.TryGetComponent<Translation>(out var self)
                ? Pursuit.SeedTargets(targeting.Pool, (self.Value.x, self.Value.z), targeting.FocusOnly, targeting.FocusKey) : new List<long>();
        if (targeting.OwnPosition)
            return unit.TryGetComponent<Translation>(out var own) ? HuntPlan.Targets(targeting.Pool, (own.Value.x, own.Value.z), tag.Range) : new List<long>();
        if (targeting.RegionHuntScope is not { } scope)
            return HuntPlan.TargetsFor(targeting.Pool, (tag.X, tag.Z), tag.Range, targeting.FocusOnly, targeting.FocusKey);
        return unit.TryGetComponent<Translation>(out var at)
            ? HuntRule.SeedTargets(targeting.Pool, scope, targeting.RegionOf, (at.Value.x, at.Value.z))
            : new List<long>();
    }

    /// <summary>One unit's tick: reconcile the record with the buffer, then remove the seeds of players no longer targets
    /// and add the new targets'. Adds to <paramref name="held"/> each target the buffer holds afterwards (already there
    /// and kept, or written now). Returns the seeds kept and those left to the game.</summary>
    static (int Kept, int Left) SeedUnit(long key, Entity unit, string eventId, HuntTag tag, List<PlayerRow> players,
        EventTargeting targeting, HashSet<long> held)
    {
        var bufferStart = Stopwatch.GetTimestamp();                          // HasBuffer/GetBuffer may wait on a job (A7)
        if (!Core.EntityManager.HasBuffer<AggroBuffer>(unit)) { _bufferTicks += Stopwatch.GetTimestamp() - bufferStart; _seeds.ForgetUnit(key); return (0, 0); }
        var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
        _bufferTicks += Stopwatch.GetTimestamp() - bufferStart;
        var inBuffer = BufferSeeds(buffer);
        var (kept, left) = _seeds.Reconcile(key, inBuffer);

        // No map (its latest build failed): no target, so every seed goes (fail closed, D13).
        var targets = TargetsOf(unit, tag, targeting);
        var (adds, removes) = _seeds.Plan(key, targets, inBuffer.Keys.ToList());

        // A seed is the player's only entry, whatever values the game gave it (A67); a shared player keeps every entry.
        foreach (var target in removes)
        {
            var entryTargets = new List<long>(buffer.Length);
            for (var i = 0; i < buffer.Length; i++) entryTargets.Add(KeyOf(buffer[i].Entity));
            var index = HuntBuffer.RemovalIndex(entryTargets, target);
            if (index >= 0) buffer.RemoveAt(index);
            _seeds.Removed(key, target);
        }
        foreach (var target in targets) if (inBuffer.ContainsKey(target) && !removes.Contains(target)) held.Add(target);
        if (targeting?.RegionHuntScope is not null || targeting?.OwnPosition == true) Rehome(unit, targets, targeting.Pool, players);
        foreach (var target in adds)
        {
            var player = players.FirstOrDefault(p => p.Key == target);
            if (!player.Character.Exists()) continue;
            buffer.Add(new AggroBuffer { Entity = player.Character, DamageValue = SeedDamage, Weight = SeedWeight });
            _seeds.Wrote(key, eventId, new AggroSeed(target, SeedDamage, SeedWeight));
            held.Add(target);                                                // counted once written (Review 36 F4)
        }
        return (kept, left);
    }

    /// <summary>hunting-grounds A5: a seeded RegionHunt unit's return point (AggroConsumer.PreCombatPosition and
    /// SpawnTransform.Position) goes to its nearest seeded player (HuntRule.Home), or the game drops a target beyond the
    /// unit's leash from the 50-60 m final ring. Written only when it moves 2 m or more; the unit itself is not moved.
    /// Our tracked units only (this service's own).</summary>
    static void Rehome(Entity unit, List<long> targets, List<HuntCandidate> pool, List<PlayerRow> players)
    {
        if (HuntRule.Home(targets, pool) is not { } home) return;
        var row = players.FirstOrDefault(p => p.Key == targets[0]);
        var to = new float3(home.X, row.Y, home.Z);
        if (unit.TryGetComponent<AggroConsumer>(out var aggro) && math.distance(aggro.PreCombatPosition.xz, to.xz) >= 2f)
        {
            aggro.PreCombatPosition = to;
            unit.Write(aggro);
        }
        if (unit.TryGetComponent<SpawnTransform>(out var spawn) && math.distance(spawn.Position.xz, to.xz) >= 2f)
        {
            spawn.Position = to;
            unit.Write(spawn);
        }
    }

    static HuntCandidate Candidate(PlayerRow p, IReadOnlySet<(int X, int Z)> map) =>
        new(p.Key, p.X, p.Z, true, p.Alive, Territory.IsClaimed(map, p.X, p.Z), p.InPvpCombat);

    /// <summary>The buffer's entries by target with their counts (Logic HuntBuffer.Read, A67).</summary>
    static Dictionary<long, AggroSeed> BufferSeeds(DynamicBuffer<AggroBuffer> buffer)
    {
        var entries = new List<AggroSeed>(buffer.Length);
        for (var i = 0; i < buffer.Length; i++) entries.Add(new AggroSeed(KeyOf(buffer[i].Entity), buffer[i].DamageValue, buffer[i].Weight));
        return HuntBuffer.Read(entries);
    }

    internal static long KeyOf(Entity e) => ((long)e.Index << 32) | (uint)e.Version;
}

/// <summary>An online player's character as the wave planners see it (event-spawns D13, D16); held in memory for one
/// tick and never logged (Security › Personal data). <see cref="PlatformId"/> matches a player across a relog for the
/// trigger rows and the focus (automation D5, D9, D13).</summary>
internal readonly record struct PlayerRow(Entity Character, long Key, float X, float Y, float Z, bool Alive, bool InPvpCombat, string PlatformId);

/// <summary>The online players, read for an AroundPlayer pick or a Hunt tick. Read-only.</summary>
internal static class PlayerQuery
{
    static readonly PrefabGUID PvpCombat = new(PlayerPick.PvpCombatBuff);
    static EntityQuery _users;
    static bool _made;

    /// <summary>The query over User, built once and kept for the server's life like TriggerBus's DayNightCycle query:
    /// the 5 s player scan reads it, and a new query per read cost up to 295 ms a tick (automation A6).</summary>
    static EntityQuery Users()
    {
        if (!_made)
        {
            _users = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
            _made = true;
        }
        return _users;
    }

    /// <summary>Each connected user's character with its position and state; a character that cannot be read (no
    /// Translation, a position that is not a number within the map, no Health) is left out (A60). Throws when the user
    /// query itself fails (D21: the wave is skipped, or the Hunt tick seeds nothing).</summary>
    internal static List<PlayerRow> Read()
    {
        var rows = new List<PlayerRow>();
        var users = Users().ToComponentDataArray<User>(Allocator.Temp);
        try
        {
            foreach (var user in users)
            {
                if (!user.IsConnected) continue;
                var character = user.LocalCharacter._Entity;
                if (!character.Exists() || !character.TryGetComponent<Translation>(out var t)) continue;
                if (!PlayerPosition.Usable(t.Value.x, t.Value.y, t.Value.z) || !character.TryGetComponent<Health>(out var h)) continue;
                var alive = !character.Has<Dead>() && float.IsFinite(h.Value) && h.Value > 0f;
                rows.Add(new PlayerRow(character, HuntAction.KeyOf(character), t.Value.x, t.Value.y, t.Value.z, alive, InPvpCombat(character),
                    user.PlatformId.ToString(CultureInfo.InvariantCulture)));
            }
        }
        finally { users.Dispose(); }
        return rows;
    }

    static bool InPvpCombat(Entity character)
    {
        if (!Core.EntityManager.HasBuffer<BuffBuffer>(character)) return false;
        var buffs = Core.EntityManager.GetBuffer<BuffBuffer>(character);
        for (var i = 0; i < buffs.Length; i++)
            if (buffs[i].PrefabGuid == PvpCombat) return true;
        return false;
    }
}
