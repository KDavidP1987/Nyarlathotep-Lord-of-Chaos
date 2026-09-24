using System.IO;
using System.Reflection;
using Nyarlathotep.Logic;
using ProjectM;

namespace Nyarlathotep.Services;

/// <summary>
/// The loaded event definitions (foundation D6, D23, D28). Seeds events.json from the embedded
/// Resources/events.default.json on first run (every event disabled), validates through Logic/EventValidator,
/// and swaps the set on reload; running instances keep the definition they started with (EventCatalog).
/// </summary>
internal static class EventStore
{
    const string SeedResource = "Nyarlathotep.Resources.events.default.json";

    internal static EventCatalog Catalog { get; } = new();

    /// <summary>Second in Core.TryInitialize, after Persistence. Never throws.</summary>
    internal static void Initialize()
    {
        try
        {
            if (!Persistence.Events.Exists()) Seed();
            var reply = Reload();
            Core.Log.LogInfo($"[nyar] events: {reply}");
        }
        catch (Exception ex)
        {
            // No definitions is a safe state: nothing can start. The admin reloads once the cause is fixed.
            Core.Log.LogError($"[nyar] events.json load failed: {ex.Message}; no events loaded");
        }
    }

    /// <summary>Loads events.json and applies it. Returns the `.nyar event reload` reply: "reloaded: &lt;v&gt; valid,
    /// &lt;x&gt; disabled" or the file error (the last valid set stays, D23).</summary>
    internal static string Reload()
    {
        var (result, stamp) = Persistence.Events.Load(new PrefabUnitCatalog());
        var error = Catalog.Reload(result, stamp);
        if (error is not null)
        {
            Core.Log.LogWarning($"[nyar] {error}; the last valid set stays ({Catalog.Current.All.Count} events)");
            return error;
        }
        foreach (var line in result.Log) Core.Log.LogWarning($"[nyar] {line}");
        var disabled = result.Log.Count;
        return $"reloaded: {Catalog.Current.All.Count - disabled} valid, {disabled} disabled";
    }

    static void Seed()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SeedResource);
            if (stream is null)
            {
                Core.Log.LogError($"[nyar] embedded {SeedResource} missing; events.json not seeded");
                return;
            }
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var error = Persistence.Events.Seed(buffer.ToArray());
            if (error is null) Core.Log.LogInfo("[nyar] first run: seeded events.json from the default templates (every event disabled)");
            else Core.Log.LogWarning($"[nyar] events.json not seeded: {error}");
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[nyar] seeding events.json failed: {ex.Message}");
        }
    }

    /// <summary>Unit names from PrefabCollectionSystem; denied are the name-based do-not-spawn list plus prefabs
    /// carrying DropInInventoryOnSpawn (docs/GAME_ASSETS.md › Do-not-spawn list).</summary>
    sealed class PrefabUnitCatalog : IUnitCatalog
    {
        public bool IsKnown(string prefabName) =>
            Core.PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary.ContainsKey(prefabName);

        public bool IsDenied(string prefabName)
        {
            if (UnitDenyList.IsDenied(prefabName)) return true;
            if (!Core.PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary.TryGetValue(prefabName, out var guid)) return false;
            if (!Core.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(guid, out var prefab)) return false;
            return Core.EntityManager.HasComponent<DropInInventoryOnSpawn>(prefab);
        }
    }
}
