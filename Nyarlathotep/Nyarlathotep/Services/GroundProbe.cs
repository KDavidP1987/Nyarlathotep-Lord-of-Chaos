using System.Collections.Generic;
using ProjectM;
using ProjectM.Shared;
using Unity.Entities;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// event-library A22 spike, log only: what the game does with a spawned unit's height. Only under Debug.VerboseLogging;
/// it reads components and writes nothing. For each unit SpawnTracker spawns it logs, at spawn and about 1 s and 5 s
/// later, the unit's Translation y against its ring point's y, the game's Height component (Value, LastPosition y,
/// ServerHeightLevel; HeightCorrectionSystem keeps it), and whether the unit has SnapToHeight or FallToHeight (the
/// components HeightCorrectionSystem snaps or drops to that height). Turning VerboseLogging off drops the pending passes.
/// Removed once A22's placement is built.
/// </summary>
internal static class GroundProbe
{
    const int MaxWatched = 64;
    static readonly List<(Entity Unit, string Prefab, float PlannedY, DateTime Due, int Pass)> _watched = new();

    /// <summary>Called by SpawnTracker.Tick right after a unit's recipe; logs pass 0 and schedules passes 1 and 2. Past
    /// MaxWatched, pass 0 says the unit is not followed.</summary>
    internal static void Spawned(Entity unit, string prefab, float plannedY, DateTime now)
    {
        try
        {
            if (!Config.Settings.VerboseLogging.Value) return;
            var followed = _watched.Count < MaxWatched;
            Log(unit, prefab, plannedY, 0, followed ? "" : " (not followed: 64 units watched)");
            if (followed) _watched.Add((unit, prefab, plannedY, now.AddSeconds(1), 1));
        }
        catch (Exception ex) { Core.Log.LogWarning($"[nyar] ground probe failed: {ex.Message}"); }
    }

    /// <summary>Called once per SpawnTracker.Tick: logs each watched unit whose next pass is due.</summary>
    internal static void Tick(DateTime now)
    {
        if (_watched.Count == 0) return;
        try
        {
            if (!Config.Settings.VerboseLogging.Value) { _watched.Clear(); return; }
            for (var i = _watched.Count - 1; i >= 0; i--)
            {
                var w = _watched[i];
                if (w.Due > now) continue;
                _watched.RemoveAt(i);
                if (!w.Unit.Exists()) continue;
                Log(w.Unit, w.Prefab, w.PlannedY, w.Pass, "");
                if (w.Pass == 1) _watched.Add((w.Unit, w.Prefab, w.PlannedY, now.AddSeconds(4), 2));
            }
        }
        catch (Exception ex)
        {
            _watched.Clear();
            Core.Log.LogWarning($"[nyar] ground probe failed: {ex.Message}");
        }
    }

    static void Log(Entity unit, string prefab, float plannedY, int pass, string note)
    {
        var at = unit.TryGetComponent<Translation>(out var t) ? $"{t.Value.x:0.0} {t.Value.y:0.00} {t.Value.z:0.0}" : "no Translation";
        var height = unit.TryGetComponent<Height>(out var h)
            ? $"height {h.Value:0.00} last y {h.LastPosition.y:0.00} level {h.ServerHeightLevel}"
            : "no Height";
        var fall = unit.TryGetComponent<FallToHeight>(out var f) ? $"FallToHeight grounded {f.IsGrounded}" : "no FallToHeight";
        var snap = unit.Has<SnapToHeight>() ? "SnapToHeight" : "no SnapToHeight";
        Core.Log.LogInfo($"[nyar] ground probe {prefab} pass {pass}: at {at} (planned y {plannedY:0.00}); {height}; {snap}; {fall}{note}");
    }
}
