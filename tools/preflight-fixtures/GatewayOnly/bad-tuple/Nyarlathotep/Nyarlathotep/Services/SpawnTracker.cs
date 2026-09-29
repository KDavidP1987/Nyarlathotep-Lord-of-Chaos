namespace Nyarlathotep.Services;

/// <summary>Synthetic fixture (event-spawns D22): the tick and boot entry points the System actor calls.</summary>
internal static class SpawnTracker
{
    [Mutating]
    internal static void Tick() { }

    [Mutating]
    internal static void BootSweep() { }

    [Mutating]
    internal static (int Removed, int Queued) PurgeUnits() => (0, 0);      // planted (Review 25 F1): a tuple-returning writer, as the real one
}
