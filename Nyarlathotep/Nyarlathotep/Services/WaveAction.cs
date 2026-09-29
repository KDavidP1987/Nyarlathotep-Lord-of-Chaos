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
/// wave, at the player's height (D16), never named or located in any line. A player-action start's focus player is
/// tried first (automation D13); with action.fanOut the wave picks up to maxInstances spaced players and spawns one group
/// around each, each with its own Hunt tag, the whole wave clamped once (automation D5, D6).</item>
/// <item>Every decided wave is reported once through EventEngine.WaveDecided, however many groups it spawned (automation
/// D17).</item>
/// <item>The territory map is built once per wave when the wave needs it (D17): a claimed ring point is blocked like an
/// out-of-scope one unless allowTerritory.</item>
/// <item>An AroundPlayer group has no anchor: its units stay where the game grounds them (design §9 D30, automation
/// A4). Any other wave's anchor (WavePlan.Anchor) lets SpawnTracker regroup a unit the game snaps onto another terrain level
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
        IReadOnlyList<GroupCentre> centres;
        if (location.Type == LocationType.AroundPlayer && !mapFailed)
        {
            var result = Pick(id, location, action.FanOut, active.Focus, claimed, inScope);
            pick = result.Outcome;
            centres = result.Centres.Select(c => new GroupCentre(c.X, c.Y, c.Z, map is not null && claimed(c.X, c.Z))).ToList();
        }
        else
        {
            var (cx, cy, cz) = WavePlan.Center(location, active.Origin);
            centres = [new GroupCentre(cx, cy, cz, map is not null && claimed(cx, cz))];
        }
        var facts = probe with { MapFailed = mapFailed, Pick = pick };
        var decision = WaveGate.DecideGroups(facts, centres, () => WaveRoll.Expand(action.Units, _rng), ledger.Limits.MaxPerWave,
            ledger.Occupied, ledger.Limits.MaxTracked);
        foreach (var line in decision.CapLines) Core.Log.LogWarning($"[nyar] event {id} wave {due.Wave}: {line}");

        var life = SpawnLedger.Lifetime(now, active.Instance.EndsUtc, action.UnitLifetimeSeconds,
            Settings.Limit(Limits.GraceSeconds), Settings.Limit(Limits.ManualSpawnLifetimeSeconds), SpawnTracker.DrainMargin());
        var tuning = SpawnTuning.TuningFrom(action.Modifiers);
        // A claimed ring point counts as blocked, like an out-of-scope one, unless allowTerritory (D17).
        Func<float, float, bool> allowed = map is null || action.AllowTerritory ? inScope
            : (x, z) => (inScope is null || inScope(x, z)) && !claimed(x, z);
        int total = 0, moved = 0, unchecked_ = 0;
        byte? level = null;
        WaveRun.Run(decision, skipped => Core.Log.LogInfo($"[nyar] {skipped}"), group =>
        {
            var (gx, gy, gz) = group.Centre;
            var groupTotal = group.Units.Sum(u => u.Count);
            var angle = _random.NextDouble() * 2 * Math.PI;
            var anchor = WavePlan.GroupAnchor(location, active.Origin);            // none for AroundPlayer (A4)
            HuntTag? hunt = action.Behaviour is { Type: BehaviourType.Hunt } b ? new HuntTag(gx, gz, b.Range) : null;   // one per group
            var first = 0;
            var check = WalkCheck.OpenWave(WavePlan.WalkY(location, group.Centre, active.Origin));   // walkable-spawns D3, A13
            try
            {
                foreach (var entry in group.Units)
                {
                    var queued = SpawnTracker.RequestWave(entry.Prefab, id, entry.Count, life, new float3(gx, gy, gz), action.Radius, first,
                        groupTotal, angle, anchor, check.Walk, allowed, tuning, action.Loot, hunt);
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
            level ??= check.Level;
            total += groupTotal;
#if DEBUG
            if (location.Type == LocationType.AroundPlayer) PhantomGroupLine(id, due.Wave, group);   // never a stale pick's (review F6)
#endif
        }, outcome => EventRuntime.Engine.WaveDecided(id, outcome));
        if (decision.Outcome != WaveOutcome.Spawn) return;
        var levelText = level is { } h ? $", walk h {h}" : "";
        var where = location.Type == LocationType.AroundPlayer ? $" {WaveLines.AroundPlayers(decision.Groups.Count)}" : "";
        Core.Log.LogInfo($"[nyar] event {id} wave {due.Wave}/{due.Waves}{where}: {total} units queued ({moved} moved, {unchecked_} unchecked), due in {(int)Math.Ceiling((life.DueUtc - now).TotalSeconds)}s, lifetime {life.LifetimeSeconds}s{levelText}");
    }

    /// <summary>The wave's AroundPlayer centres (D16; automation D5, D13): up to fanOut.maxInstances spaced players, one
    /// without fanOut, the focus player first when eligible. A throwing player read is a failed query, which skips the
    /// wave and opens the "player query failing" entry for the event, logged once per streak (D21, D30). In a Debug build,
    /// Debug.FaultInjection = phantoms:&lt;n&gt; adds phantom candidates (automation D29).</summary>
    static FanOutPick Pick(string id, Location location, FanOut fanOut, string focus, Func<float, float, bool> claimed,
        Func<float, float, bool> inScope)
    {
        FanOutPick result;
        try
        {
            var players = PlayerQuery.Read().Select(p => new PickCandidate(p.X, p.Y, p.Z, true, p.Alive, p.InPvpCombat, p.PlatformId)).ToList();
#if DEBUG
            players.AddRange(PlacePhantoms(players, claimed, inScope));
#endif
            result = PlayerPick.ChooseMany(players, _rng, location.MinDist, location.MaxDist, claimed, inScope,
                fanOut?.MaxInstances ?? 1, fanOut?.MinSpacing ?? 0, focus);
        }
        catch (Exception ex)
        {
            result = new FanOutPick(PickOutcome.QueryFailed, [], ex.Message);
        }
        if (result.Outcome == PickOutcome.QueryFailed)
        {
            if (WalkCheck.Health.Failing(SpawnFailure.PlayerQuery, id)) Core.Log.LogWarning($"[nyar] event {id}: player query failed: {result.Error}");
        }
        else WalkCheck.Health.Recovered(SpawnFailure.PlayerQuery, id);
        return result;
    }

#if DEBUG
    static readonly List<PickCandidate> _phantoms = new();

    /// <summary>automation D29: the phantom candidates of this pick, placed from the first eligible real player that passes
    /// the pick's own territory and scope test; "phantoms: &lt;placed&gt; of &lt;n&gt; placed" once per pick.</summary>
    static List<PickCandidate> PlacePhantoms(IReadOnlyList<PickCandidate> real, Func<float, float, bool> claimed,
        Func<float, float, bool> inScope)
    {
        _phantoms.Clear();
        if (Phantoms.Parse(Settings.FaultInjection?.Value) is not { } n) return new List<PickCandidate>();
        _phantoms.AddRange(Phantoms.Place(real, n, (x, z) => !claimed(x, z) && (inScope is null || inScope(x, z))));
        Core.Log.LogInfo($"[nyar] {Phantoms.PlacedLine(_phantoms.Count, n)}");
        return new List<PickCandidate>(_phantoms);
    }

    /// <summary>automation D7: a group whose centre lies within half a phantom step of a phantom logs its distance from
    /// that phantom, never a coordinate (D19).</summary>
    static void PhantomGroupLine(string id, int wave, WaveGroup group)
    {
        if (_phantoms.Count == 0) return;
        var (x, _, z) = group.Centre;
        var nearest = _phantoms.MinBy(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z));
        var metres = Math.Sqrt((nearest.X - x) * (nearest.X - x) + (nearest.Z - z) * (nearest.Z - z));
        if (metres <= Phantoms.Step / 2) Core.Log.LogInfo($"[nyar] {Phantoms.GroupLine(id, wave, group.Index + 1, metres)}");
    }
#endif
}
