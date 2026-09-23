using Unity.Entities;

namespace Nyarlathotep.Services;

internal static class Leak
{
    public static void Run(Entity e) => Core.EntityManager.DestroyEntity(e);
}
