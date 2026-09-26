using System.Collections.Generic;
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
            // The boot load is the operator's file, so it runs as Operator (D10).
            Gateway.Run(ActionKind.LoadDefinitions, Actor.Operator, Reload);            // Reload logs its outcome
        }
        catch (Exception ex)
        {
            // No definitions is a safe state: nothing can start. The admin reloads once the cause is fixed.
            Core.Log.LogError($"[nyar] events.json load failed: {ex.Message}; no events loaded");
        }
    }

    /// <summary>The load and edit flows (Logic/DefinitionEditor) over the disk and the game's unit catalog.</summary>
    static DefinitionEditor Editor => new(Persistence.Events, Persistence.Disk, Catalog,
        line => Core.Log.LogInfo($"[nyar] {line}"), line => Core.Log.LogWarning($"[nyar] {line}"));

    /// <summary>Loads events.json and applies it. Returns the `.nyar event reload` reply: "reloaded: &lt;v&gt; valid,
    /// &lt;x&gt; disabled" or the file error (the last valid set stays, D23).</summary>
    [Mutating]
    internal static string Reload() => Editor.Reload(new PrefabUnitCatalog());

    /// <summary>`.nyar event set`, `enable` and `disable`: changes one field of event <paramref name="id"/> in events.json,
    /// written only when the file is the one last loaded, keeping one .bak (Business rules 9, D6), then reloads.</summary>
    [Mutating]
    internal static string Edit(string id, string path, object value) => Editor.Edit(id, path, value, new PrefabUnitCatalog());

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
    /// carrying DropInInventoryOnSpawn (docs/GAME_ASSETS.md › Do-not-spawn list). Faction names are the Faction_* prefab
    /// names of the prefab map, read once (faction-empowerment D1).</summary>
    internal sealed class PrefabUnitCatalog : IUnitCatalog, IFactionCatalog
    {
        static HashSet<string> _factions;

        bool IFactionCatalog.IsKnown(string factionName) =>
            factionName.StartsWith("Faction_", StringComparison.Ordinal) && Factions().Contains(factionName);

        static HashSet<string> Factions()
        {
            if (_factions is not null) return _factions;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in Core.PrefabCollectionSystem._PrefabGuidToEntityMap)
            {
                var name = entry.Key.GetPrefabName();
                if (name.StartsWith("Faction_", StringComparison.Ordinal)) names.Add(name);
            }
            return _factions = names;
        }

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
