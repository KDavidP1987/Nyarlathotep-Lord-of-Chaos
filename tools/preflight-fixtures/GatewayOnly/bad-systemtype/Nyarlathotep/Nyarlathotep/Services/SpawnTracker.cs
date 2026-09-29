namespace Nyarlathotep.Services;

/// <summary>Synthetic fixture (event-spawns D22): the tick and boot entry points the System actor calls.</summary>
internal static class SpawnTracker
{
    [Mutating]
    internal static void Tick() { }

    [Mutating]
    internal static void BootSweep() { }
}
