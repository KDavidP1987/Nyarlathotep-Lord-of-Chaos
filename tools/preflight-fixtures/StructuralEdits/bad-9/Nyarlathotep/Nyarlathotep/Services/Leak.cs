using ProjectM;
using Unity.Entities;

namespace Nyarlathotep.Services;

internal static class Leak
{
    public static void Run(Entity e) => DestroyUtility.Destroy(Core.EntityManager, e);
}
