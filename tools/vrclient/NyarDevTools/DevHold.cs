using System.Reflection;
using HarmonyLib;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), unit-watch Session 1: the game's height correction lifts a unit sunk by `.devstrand under`
// back to a terrain level within 5 s (the mod's `[nyar watch] low` diagnostic never fired; ground read -5 m), so an
// off-ground unit cannot be made by one write. `.devstrand <guid> under <m>` now also holds the unit there: after every
// HeightCorrectionSystem update (StatChangeSystem's when that type is not found) the held unit's Translation and
// LastTranslation are written back to the strand spot, with its Height.ServerHeightLevel set to the spot's level, until its x or z moves more than 2 m from it (a recovery by the
// mod moved it) or HoldSeconds pass. Logs [DEV HOLD] released=<moved|time|gone> once per unit. Never in the mod.
static class StrandHold
{
    public const double HoldSeconds = 120;

    sealed record Held(Entity Unit, float3 Spot, DateTime Until);

    static readonly List<Held> _held = new();

    public static void Add(Entity unit, float3 spot)
    {
        _held.RemoveAll(h => h.Unit == unit);
        _held.Add(new Held(unit, spot, DateTime.UtcNow.AddSeconds(HoldSeconds)));
    }

    public static string Hook = "none";

    public static void Patch(Harmony harmony)
    {
        var type = AccessTools.AllTypes().FirstOrDefault(t => t.Name == "HeightCorrectionSystem")
            ?? typeof(ProjectM.Gameplay.Systems.StatChangeSystem);
        var method = AccessTools.Method(type, "OnUpdate");
        if (method is null) { Plugin.Logger.LogWarning($"[DEV HOLD] no OnUpdate on {type.FullName}"); return; }
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(StrandHold).GetMethod(nameof(After), BindingFlags.Static | BindingFlags.NonPublic)));
        Hook = type.Name;
        Plugin.Logger.LogInfo($"[DEV HOLD] hooked {type.FullName}.OnUpdate");
    }

    static void After()
    {
        if (_held.Count == 0) return;
        try
        {
            var em = DevCommands.Em;
            var now = DateTime.UtcNow;
            for (var i = _held.Count - 1; i >= 0; i--)
            {
                var h = _held[i];
                string? released = null;
                if (!em.Exists(h.Unit) || !em.HasComponent<Translation>(h.Unit)) released = "gone";
                else if (now >= h.Until) released = "time";
                else
                {
                    var at = em.GetComponentData<Translation>(h.Unit).Value;
                    if (math.distance(at.xz, h.Spot.xz) > 2f) released = "moved";
                    else
                    {
                        em.SetComponentData(h.Unit, new Translation { Value = h.Spot });
                        if (em.HasComponent<LastTranslation>(h.Unit)) em.SetComponentData(h.Unit, new LastTranslation { Value = h.Spot });
                        // the correction lifts it to its Height level's ground: its level goes with it (a sunk unit's own)
                        if (em.HasComponent<Height>(h.Unit))
                        {
                            var height = em.GetComponentData<Height>(h.Unit);
                            height.ServerHeightLevel = (byte)ProjectM.Tiles.TileLayerUtility.GetHeightLevel(h.Spot.y);
                            em.SetComponentData(h.Unit, height);
                        }
                    }
                }
                if (released is null) continue;
                _held.RemoveAt(i);
                Plugin.Logger.LogInfo($"[DEV HOLD] released={released}");
            }
        }
        catch (Exception ex)
        {
            _held.Clear();
            Plugin.Logger.LogWarning($"[DEV HOLD] failed: {ex.Message}");
        }
    }
}
