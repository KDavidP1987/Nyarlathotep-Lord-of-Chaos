using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// The core SpawnWaves action (foundation A17, D20, D22). Each due wave is sized by Logic/WavePlan (MaxUnitsPerWave,
/// then the free MaxTrackedUnits slots, each clamp logged) and queued through SpawnTracker, which spawns it within
/// MaxSpawnsPerTick. A Point location spawns at its stored height (event-library A20; 0 for a Point saved without
/// one); an Admin location spawns around the admin who started the event. Each wave's anchor (WavePlan.Anchor) lets
/// SpawnTracker regroup a unit the game snaps onto another terrain level (A23). Units are due for despawn at the event's end +
/// GraceSeconds, or at their own unitLifetimeSeconds when shorter; an event-decided LifeTime runs the despawn queue's
/// drain time past that, as a backstop (Business rules 2, A16).
/// </summary>
internal static class WaveAction
{
    static readonly System.Random _random = new();

    /// <summary>Queues the next wave of <paramref name="active"/> when it is due. Throws on a game-side failure, which
    /// EventRuntime counts as a fault of this event (D25).</summary>
    [Mutating]
    internal static void QueueDueWave(ActiveEvent active, DateTime now)
    {
        if (EventRuntime.Engine.NextWave(active.Id, now) is not { } due) return;
        var action = active.Definition.Action!;
        var ledger = SpawnTracker.Ledger;
        var clamps = new System.Collections.Generic.List<string>();
        var plan = WavePlan.Split(action.Units, ledger.Limits.MaxPerWave, ledger.Occupied, ledger.Limits.MaxTracked, clamps);
        foreach (var line in clamps) Core.Log.LogWarning($"[nyar] event {active.Id} wave {due.Wave}: {line}");

        var (cx, cy, cz) = WavePlan.Center(action.Location, active.Origin);
        var center = new float3(cx, cy, cz);
        var life = SpawnLedger.Lifetime(now, active.Instance.EndsUtc, action.UnitLifetimeSeconds,
            Settings.Limit(Limits.GraceSeconds), Settings.Limit(Limits.ManualSpawnLifetimeSeconds), SpawnTracker.DrainMargin());
        var total = plan.Sum(u => u.Count);
        var angle = _random.NextDouble() * 2 * Math.PI;
        var anchor = WavePlan.Anchor(action.Location, active.Origin);
        var first = 0;
        int moved = 0, unchecked_ = 0;
        var check = WalkCheck.OpenWave(anchor?.Y);                          // walkable-spawns D3, A13
        var inScope = WavePoints.ScopeCheck(action.Scope, RegionMap.State.Available ? RegionMap.State.Index.RegionOf : null);   // regions D6
        try
        {
            foreach (var entry in plan)
            {
                var queued = SpawnTracker.RequestWave(entry.Prefab, active.Id, entry.Count, life, center, action.Radius, first, total, angle, anchor, check.Walk, inScope);
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
        EventRuntime.Engine.WaveSpawned(active.Id);
        var level = check.Level is { } h ? $", walk h {h}" : "";
        Core.Log.LogInfo($"[nyar] event {active.Id} wave {due.Wave}/{due.Waves}: {total} units queued ({moved} moved, {unchecked_} unchecked), due in {(int)Math.Ceiling((life.DueUtc - now).TotalSeconds)}s, lifetime {life.LifetimeSeconds}s{level}");
    }
    // planted (A55): a new [Mutating] method the floor does not list
    [Mutating]
    internal static void Seed() { }
}
