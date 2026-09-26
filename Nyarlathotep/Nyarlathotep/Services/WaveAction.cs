using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using Unity.Mathematics;

namespace Nyarlathotep.Services;

/// <summary>
/// The core SpawnWaves action (foundation A17, D20, D22). Each due wave is sized by Logic/WavePlan (MaxUnitsPerWave,
/// then the free MaxTrackedUnits slots, each clamp logged) and queued through SpawnTracker, which spawns it within
/// MaxSpawnsPerTick. A Point location spawns at ground height 0 (the world's plane, as TideOfWar does); an Admin
/// location spawns around the admin who started the event. Units are due for despawn at the event's end +
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

        var center = action.Location.Type == LocationType.Admin && active.Origin is { } o
            ? new float3(o.X, o.Y, o.Z)
            : new float3(action.Location.X, 0f, action.Location.Z);
        var life = SpawnLedger.Lifetime(now, active.Instance.EndsUtc, action.UnitLifetimeSeconds,
            Settings.Limit(Limits.GraceSeconds), Settings.Limit(Limits.ManualSpawnLifetimeSeconds), SpawnTracker.DrainMargin());
        var total = plan.Sum(u => u.Count);
        var angle = _random.NextDouble() * 2 * Math.PI;
        var first = 0;
        foreach (var entry in plan)
        {
            SpawnTracker.RequestWave(entry.Prefab, active.Id, entry.Count, life, center, action.Radius, first, total, angle);
            first += entry.Count;
        }
        EventRuntime.Engine.WaveSpawned(active.Id);
        Pusher.Wave(active.Id, due.Wave);
        Core.Log.LogInfo($"[nyar] event {active.Id} wave {due.Wave}/{due.Waves}: {total} units queued, due in {(int)Math.Ceiling((life.DueUtc - now).TotalSeconds)}s, lifetime {life.LifetimeSeconds}s");
    }
}
