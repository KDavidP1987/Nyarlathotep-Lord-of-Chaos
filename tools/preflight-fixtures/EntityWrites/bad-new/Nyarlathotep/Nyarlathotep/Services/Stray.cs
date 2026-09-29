using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>Planted (EntityWrites/bad-new): a new service file, not in $DispatchedServices, that writes an entity.</summary>
internal static class Stray
{
    internal static bool Remove(Entity unit) => unit.DestroySafe();
}
