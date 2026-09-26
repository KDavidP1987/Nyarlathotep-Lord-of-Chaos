using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace Nyarlathotep;

/// <summary>
/// IL2CPP-safe entity helpers. Mirrors the proven Faust/Uriel/Beelzebub extension patterns
/// (Exists guard before every EntityManager call).
/// </summary>
internal static class EntityExtensions
{
    public static bool Exists(this Entity entity) =>
        entity != Entity.Null && Core.EntityManager.Exists(entity);

    public static bool Has<T>(this Entity entity) =>
        entity.Exists() && Core.EntityManager.HasComponent<T>(entity);

    public static T Read<T>(this Entity entity) where T : unmanaged =>
        Core.EntityManager.GetComponentData<T>(entity);

    public static void Write<T>(this Entity entity, T componentData) where T : unmanaged =>
        Core.EntityManager.SetComponentData(entity, componentData);

    public static bool TryGetComponent<T>(this Entity entity, out T component) where T : unmanaged
    {
        if (!entity.Exists() || !Core.EntityManager.HasComponent<T>(entity))
        {
            component = default;
            return false;
        }
        component = Core.EntityManager.GetComponentData<T>(entity);
        return true;
    }

    // ---- Structural edits: the only place the mod may add or remove components, add buffers or destroy
    //      entities (Epic D6, spikes D3). Each refuses a missing entity and a Prefab entity, because a
    //      structural edit on a prefab changes every future instance (DEV_REMINDERS #22). ----

    public static bool AddComponentSafe<T>(this Entity entity)
    {
        if (!entity.Exists() || entity.Has<Prefab>()) { LogRefusal("AddComponent", typeof(T).Name, entity); return false; }
        if (Core.EntityManager.HasComponent<T>(entity)) return true;
        return Core.EntityManager.AddComponent<T>(entity);
    }

    public static bool RemoveComponentSafe<T>(this Entity entity)
    {
        if (!entity.Exists() || entity.Has<Prefab>()) { LogRefusal("RemoveComponent", typeof(T).Name, entity); return false; }
        if (!Core.EntityManager.HasComponent<T>(entity)) return true;
        return Core.EntityManager.RemoveComponent<T>(entity);
    }

    public static bool AddBufferSafe<T>(this Entity entity) where T : unmanaged
    {
        if (!entity.Exists() || entity.Has<Prefab>()) { LogRefusal("AddBuffer", typeof(T).Name, entity); return false; }
        if (Core.EntityManager.HasComponent<T>(entity)) return true;
        Core.EntityManager.AddBuffer<T>(entity);
        return true;
    }

    /// <summary>Deferred destroy (stamps DestroyTag); never destroys twice (DEV_REMINDERS #9).</summary>
    public static bool DestroySafe(this Entity entity)
    {
        if (!entity.Exists() || entity.Has<Prefab>()) { LogRefusal("Destroy", "-", entity); return false; }
        if (Core.EntityManager.HasComponent<DestroyTag>(entity)) return true;
        DestroyUtility.Destroy(Core.EntityManager, entity);
        return true;
    }

    /// <summary>The only way a carrier buff is removed (faction-empowerment D8): the game's own buff removal, a deferred
    /// destroy with DestroyDebugReason.TryRemoveBuff, so the buff systems run its removal and the unit's stats recompute.
    /// Never removes twice.</summary>
    public static bool RemoveBuffSafe(this Entity buff)
    {
        if (!buff.Exists()) { LogRefusal("RemoveBuff", "-", buff); return false; }
        if (Core.EntityManager.HasComponent<DestroyTag>(buff)) return true;
        DestroyUtility.Destroy(Core.EntityManager, buff, DestroyDebugReason.TryRemoveBuff);
        return true;
    }

    static void LogRefusal(string op, string type, Entity entity) =>
        Core.Log.LogWarning($"[nyar] refused {op}<{type}> on {entity.Index}:{entity.Version}: " +
                            (entity.Exists() ? "prefab entity" : "missing entity"));

    public static ulong GetSteamId(this Entity playerCharacter)
    {
        if (playerCharacter.TryGetComponent<PlayerCharacter>(out var pc)
            && pc.UserEntity.TryGetComponent<User>(out var user))
        {
            return user.PlatformId;
        }
        return 0;
    }

    public static PrefabGUID GetPrefabGuid(this Entity entity) =>
        entity.TryGetComponent<PrefabGUID>(out var g) ? g : default;

    /// <summary>Resolve a prefab GUID to its dev name via the runtime prefab lookup map.</summary>
    public static string GetPrefabName(this PrefabGUID prefabGuid)
    {
        try
        {
            var map = Core.PrefabCollectionSystem._PrefabLookupMap;
            if (map.GuidToEntityMap.ContainsKey(prefabGuid))
                return map.GetName(prefabGuid);
        }
        catch { /* lookup map shape can vary; raw hash fallback below */ }
        return $"PrefabGuid({prefabGuid._Value})";
    }
}
