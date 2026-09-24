using ProjectM;
using Unity.Entities;

namespace Nyarlathotep.Services;

internal static class Leak
{
    static EntityManager Em(int world, int unused) => Core.EntityManager;

    public static void Run(Entity e) => DestroyUtility.Destroy(Em(Pick(1), 2), e);

    static int Pick(int n) => n;
}
