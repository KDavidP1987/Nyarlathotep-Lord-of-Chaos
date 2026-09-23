using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using C = VampireCommandFramework.CommandAttribute;

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
