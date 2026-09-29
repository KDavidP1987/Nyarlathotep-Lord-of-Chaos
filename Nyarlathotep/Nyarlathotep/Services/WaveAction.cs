using System.Collections.Generic;
using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// The core SpawnWaves action (foundation A17, D20, D22; event-spawns D29). Each due wave goes through Logic/WaveGate in
/// its one order: territory unknown when the wave needs the map, no eligible player (or a failed player query) for
/// AroundPlayer, a claimed centre unless allowTerritory, the per-copy chance roll, then MaxUnitsPerWave and the free
/// MaxTrackedUnits slots (each clamp logged). A skipped or empty wave is counted and not reported (D20). A spawned wave is
/// queued through SpawnTracker, which spawns it within MaxSpawnsPerTick with the event's modifiers (D9), loot (D11) and
/// Hunt tag (D13).
/// <list type="bullet">
/// <item>A Point spawns at its stored height (event-library A20; 0 for a Point saved without one); an Admin location
/// around the admin who started the event; an AroundPlayer location around a player Logic/PlayerPick chose once for the
/// wave, at the player's height (D16), never named or located in any line.</item>
/// <item>The territory map is built once per wave when the wave needs it (D17): a claimed ring point is blocked like an
/// out-of-scope one unless allowTerritory.</item>
/// <item>Each wave's anchor (WavePlan.Anchor) lets SpawnTracker regroup a unit the game snaps onto another terrain level
/// (A23). Units are due for despawn at the event's end + GraceSeconds, or at their own unitLifetimeSeconds when shorter;
/// an event-decided LifeTime runs the despawn queue's drain time past that, as a backstop (Business rules 2, A16).</item>
/// </list>
/// </summary>
internal static class WaveAction
{
    static readonly System.Random _random = new();
    static readonly IRandom _rng = new SystemRandom(_random);

    /// <summary>Queues the next wave of <paramref name="active"/> when it is due. Throws on a game-side failure, which
    /// EventRuntime counts as a fault of this event (D25).</summary>
    [Mutating]
    internal static void QueueDueWave(ActiveEvent active, DateTime now)
    {
        if (EventRuntime.Engine.NextWave(active.Id, now) is not { } due) return;
        var id = active.Id;
        var action = active.Definition.Action!;
        var location = action.Location;
        var ledger = SpawnTracker.Ledger;
        Func<float, float, string> regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : null;
        var inScope = WavePoints.ScopeCheck(action.Scope, regionOf);   // regions D6

        var probe = new WaveFacts(due.Wave, id, false, Location: location.Type, Behaviour: action.Behaviour?.Type,
            AllowTerritory: action.AllowTerritory);
        var map = probe.NeedsMap ? TerritoryMap.ForWave(id) : null;
        var mapFailed = probe.NeedsMap && map is null;
        Func<float, float, bool> claimed = map is null ? (_, _) => false : (x, z) => Territory.IsClaimed(map, x, z);

        PickOutcome? pick = null;
        var (cx, cy, cz) = WavePlan.Center(location, active.Origin);
        if (location.Type == LocationType.AroundPlayer && !mapFailed)
        {
            var result = Pick(id, location, claimed, inScope);
            pick = result.Outcome;
            (cx, cy, cz) = result.Centre;
        }
        var facts = probe with
        {
            MapFailed = mapFailed,
            Pick = pick,
            CentreClaimed = map is not null && (pick is null or PickOutcome.Picked) && claimed(cx, cz),
        };
        var decision = WaveGate.Decide(facts, () => WaveRoll.Expand(action.Units, _rng), ledger.Limits.MaxPerWave, ledger.Occupied, ledger.Limits.MaxTracked);
        foreach (var line in decision.CapLines) Core.Log.LogWarning($"[nyar] event {id} wave {due.Wave}: {line}");
        if (decision.Outcome == WaveOutcome.NoWave) return;
        if (decision.Outcome != WaveOutcome.Spawn)
        {
            Core.Log.LogInfo($"[nyar] {decision.Line}");
            EventRuntime.Engine.WaveSkipped(id);
            return;
        }

        var center = new float3(cx, cy, cz);
        var life = SpawnLedger.Lifetime(now, active.Instance.EndsUtc, action.UnitLifetimeSeconds,
            Settings.Limit(Limits.GraceSeconds), Settings.Limit(Limits.ManualSpawnLifetimeSeconds), SpawnTracker.DrainMargin());
        var total = decision.Units.Sum(u => u.Count);
        var angle = _random.NextDouble() * 2 * Math.PI;
        var anchor = location.Type == LocationType.AroundPlayer ? (cx, cy, cz) : WavePlan.Anchor(location, active.Origin);
        var tuning = SpawnTuning.TuningFrom(action.Modifiers);
        HuntTag? hunt = action.Behaviour is { Type: BehaviourType.Hunt } b ? new HuntTag(cx, cz, b.Range) : null;
        // A claimed ring point counts as blocked, like an out-of-scope one, unless allowTerritory (D17).
        Func<float, float, bool> allowed = map is null || action.AllowTerritory ? inScope
            : (x, z) => (inScope is null || inScope(x, z)) && !claimed(x, z);
        var first = 0;
        int moved = 0, unchecked_ = 0;
        var check = WalkCheck.OpenWave(anchor?.Y);                          // walkable-spawns D3, A13
        try
        {
            foreach (var entry in decision.Units)
            {
                var queued = SpawnTracker.RequestWave(entry.Prefab, id, entry.Count, life, center, action.Radius, first, total, angle, anchor,
                    check.Walk, allowed, tuning, action.Loot, hunt);
                moved += queued.Moved;
                unchecked_ += queued.Unchecked;
                first += entry.Count;
            }
        }
        finally
        {
            try { check.Resource?.Dispose(); }
            catch (Exception e) { check.Walk.Fail($"dispose: {e.GetType().Name}"); }    // never stops the wave (D5; Codex F4)
        }
        WalkCheck.Settle(check.Walk);
        EventRuntime.Engine.WaveSpawned(id);
        var level = check.Level is { } h ? $", walk h {h}" : "";
        var where = location.Type == LocationType.AroundPlayer ? $" {WaveLines.AroundAPlayer}" : "";
        Core.Log.LogInfo($"[nyar] event {id} wave {due.Wave}/{due.Waves}{where}: {total} units queued ({moved} moved, {unchecked_} unchecked), due in {(int)Math.Ceiling((life.DueUtc - now).TotalSeconds)}s, lifetime {life.LifetimeSeconds}s{level}");
    }

    /// <summary>The wave's AroundPlayer centre (D16). A throwing player read is a failed query, which skips the wave and
    /// opens the "player query failing" entry for the event, logged once per streak (D21, D30).</summary>
    static PickResult Pick(string id, Location location, Func<float, float, bool> claimed, Func<float, float, bool> inScope)
    {
        PickResult result;
        try
        {
            var players = PlayerQuery.Read().Select(p => new PickCandidate(p.X, p.Y, p.Z, true, p.Alive, p.InPvpCombat)).ToList();
            result = PlayerPick.Choose(players, _rng, location.MinDist, location.MaxDist, claimed, inScope);
        }
        catch (Exception ex)
        {
            result = new PickResult(PickOutcome.QueryFailed, default, ex.Message);
        }
        if (result.Outcome == PickOutcome.QueryFailed)
        {
            if (WalkCheck.Health.Failing(SpawnFailure.PlayerQuery, id)) Core.Log.LogWarning($"[nyar] event {id}: player query failed: {result.Error}");
        }
        else WalkCheck.Health.Recovered(SpawnFailure.PlayerQuery, id);
        return result;
    }
}
