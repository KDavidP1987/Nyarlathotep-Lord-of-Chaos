using System.IO;
using System.Reflection;
using System.Text;
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

    /// <summary>Loads events.json and applies it. Returns the `.nyar event reload` reply: "reloaded: &lt;v&gt; valid,
    /// &lt;x&gt; disabled" or the file error (the last valid set stays, D23).</summary>
    [Mutating]
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
        var reply = $"reloaded: {Catalog.Current.All.Count - disabled} valid, {disabled} disabled";
        Core.Log.LogInfo($"[nyar] events: {reply}");                           // at boot and on every `.nyar event reload`
        Pusher.ConfigChanged();                                                // set, enable and disable succeed through here
        return reply;
    }

    /// <summary>`.nyar event set`, `enable` and `disable`: changes one field of event <paramref name="id"/> in events.json
    /// (Logic/EventsEditor), written only when the file is the one last loaded, keeping one .bak (Business rules 9, D6),
    /// then reloads. A running instance keeps its definition; the change applies to the next start.</summary>
    [Mutating]
    internal static string Edit(string id, string path, object value)
    {
        byte[] bytes;
        try { bytes = Persistence.Disk.Read(DataFile.Events, FileVariant.Main); }
        catch (Exception ex) { return $"events.json could not be read: {ex.Message}"; }
        if (bytes is null) return "events.json not found";
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var edited = EventsEditor.Apply(text, id, path, value, out var refusal);
        if (edited is null) return refusal;
        var error = Persistence.Events.WriteEdit(Encoding.UTF8.GetBytes(edited), Catalog.LoadedStamp, out _);
        if (error is not null) return error;

        var reload = Reload();
        var done = path == "enabled" ? $"event {id} {((bool)value ? "enabled" : "disabled")}" : $"event {id} {path} = {value}";
        Core.Log.LogInfo($"[nyar] {done}; {reload}");
        if (!reload.StartsWith("reloaded", StringComparison.Ordinal)) return $"{done}; {reload}";
        return Catalog.Current.Find(id)?.DisabledReason is { } reason ? $"{done}; now disabled: {reason}" : done;
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
    internal sealed class PrefabUnitCatalog : IUnitCatalog
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
