using Unity.Entities;

namespace Nyarlathotep.Services;

internal static class Leak
{
    public static void Run(Entity e) { var url = "https://example.invalid"; Core.EntityManager.DestroyEntity(e); }
}
