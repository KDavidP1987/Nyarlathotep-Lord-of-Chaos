using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
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

    /// <summary>At IsReady: a restart keeps no seed (D33).</summary>
    internal static void Initialize()
    {
        _seeds = new HuntSeeds();
        _units.Clear();
        _next = default;
        LastTargets = 0;
    }

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

        // The sweep and the player read fail every event's tick alike, never the scheduler's (D21; A61).
        List<IGrouping<string, KeyValuePair<long, (Entity Unit, string EventId, HuntTag Tag)>>> byEvent;
        List<PlayerRow> players;
        try
        {
            foreach (var gone in _units.Where(u => !u.Value.Unit.Exists()).Select(u => u.Key).ToList()) Forget(gone);
            byEvent = _units.GroupBy(u => u.Value.EventId).ToList();
            if (byEvent.Count == 0) { LastTargets = 0; return; }
            players = PlayerQuery.Read();
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
                int kept = 0, left = 0;
                var held = new HashSet<long>();
                foreach (var (key, (unit, _, tag)) in g.ToList())
                {
                    if (!unit.Exists()) { Forget(key); continue; }
                    var (k, l) = SeedUnit(key, unit, g.Key, tag, players, map, held);
                    kept += k;
                    left += l;
                }
                _tickTargets.UnionWith(held);                                // an event counts only once its whole tick held
                WalkCheck.Health.Recovered(SpawnFailure.HuntSeed, g.Key);
                if (Settings.VerboseLogging.Value)
                {
                    // Why whom (A66), at the first unit's wave centre: counts only (Security › Personal data). A failed
                    // diagnostic never reads as a failed seed.
                    string why;
                    try
                    {
                        var tag = g.First().Value.Tag;
                        why = map is null ? "no territory map, nobody targeted"
                            : HuntPlan.Tally(players.Select(p => Candidate(p, map)).ToList(), (tag.X, tag.Z), tag.Range).ToString();
                    }
                    catch { why = "counts unreadable"; }
                    Core.Log.LogInfo($"[nyar] hunt {g.Key}: {kept} seeds kept, {left} left to the game; at the first unit's centre: {why}");
                }
            }
            catch (Exception ex)
            {
                Failed(g.Key, ex);
            }
        }
        LastTargets = _tickTargets.Count;
    }

    static void Failed(string eventId, Exception ex)
    {
        if (WalkCheck.Health.Failing(SpawnFailure.HuntSeed, eventId)) Core.Log.LogWarning($"[nyar] hunt {eventId}: seed failed: {ex.Message}");
    }

    /// <summary>One unit's tick: reconcile the record with the buffer, then remove the seeds of players no longer targets
    /// and add the new targets'. Adds to <paramref name="held"/> each target the buffer holds afterwards (already there
    /// and kept, or written now). Returns the seeds kept and those left to the game.</summary>
    static (int Kept, int Left) SeedUnit(long key, Entity unit, string eventId, HuntTag tag, List<PlayerRow> players,
        IReadOnlySet<(int X, int Z)> map, HashSet<long> held)
    {
        if (!Core.EntityManager.HasBuffer<AggroBuffer>(unit)) { _seeds.ForgetUnit(key); return (0, 0); }
        var buffer = Core.EntityManager.GetBuffer<AggroBuffer>(unit);
        var inBuffer = BufferSeeds(buffer);
        var (kept, left) = _seeds.Reconcile(key, inBuffer);

        // No map (its latest build failed): no target, so every seed goes (fail closed, D13).
        var targets = map is null ? new List<long>()
            : HuntPlan.Targets(players.Select(p => Candidate(p, map)), (tag.X, tag.Z), tag.Range);
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
/// tick and never logged (Security › Personal data).</summary>
internal readonly record struct PlayerRow(Entity Character, long Key, float X, float Y, float Z, bool Alive, bool InPvpCombat);

/// <summary>The online players, read for an AroundPlayer pick or a Hunt tick. Read-only.</summary>
internal static class PlayerQuery
{
    static readonly PrefabGUID PvpCombat = new(PlayerPick.PvpCombatBuff);

    /// <summary>Each connected user's character with its position and state; a character that cannot be read (no
    /// Translation, a position that is not a number within the map, no Health) is left out (A60). Throws when the user
    /// query itself fails (D21: the wave is skipped, or the Hunt tick seeds nothing).</summary>
    internal static List<PlayerRow> Read()
    {
        var rows = new List<PlayerRow>();
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
        try
        {
            var users = query.ToComponentDataArray<User>(Allocator.Temp);
            try
            {
                foreach (var user in users)
                {
                    if (!user.IsConnected) continue;
                    var character = user.LocalCharacter._Entity;
                    if (!character.Exists() || !character.TryGetComponent<Translation>(out var t)) continue;
                    if (!PlayerPosition.Usable(t.Value.x, t.Value.y, t.Value.z) || !character.TryGetComponent<Health>(out var h)) continue;
                    var alive = !character.Has<Dead>() && float.IsFinite(h.Value) && h.Value > 0f;
                    rows.Add(new PlayerRow(character, HuntAction.KeyOf(character), t.Value.x, t.Value.y, t.Value.z, alive, InPvpCombat(character)));
                }
            }
            finally { users.Dispose(); }
        }
        finally { query.Dispose(); }
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
