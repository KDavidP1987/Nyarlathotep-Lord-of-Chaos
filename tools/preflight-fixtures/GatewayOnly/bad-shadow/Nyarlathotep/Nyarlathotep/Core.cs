namespace Nyarlathotep;

/// <summary>Synthetic fixture (event-spawns D22): the deferred init runs the boot sweep as the System actor.</summary>
internal static class Core
{
    internal static void TryInitialize(string trigger)
    {
        try { Services.SpawnTracker.BootSweep(); }
        catch (Exception) { }
    }
}
