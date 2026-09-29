namespace Nyarlathotep.Services;

/// <summary>Synthetic fixture (event-spawns D22): the scheduler's phases call the entry points as the System actor.</summary>
internal static class EventScheduler
{
    internal static void Run()
    {
        Phase("spawn queues", SpawnTracker.Tick);
    }

    static void Phase(string name, Action work) => work();
}
