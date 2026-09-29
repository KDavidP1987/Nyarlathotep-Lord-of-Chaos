namespace Nyarlathotep.Services;

/// <summary>The scheduler as the real tree has it: SpawnTracker.Tick, a System-actor entry point.</summary>
internal static class EventScheduler
{
    internal static void Run()
    {
        Phase("spawn queues", SpawnTracker.Tick);
    }

    static void Phase(string name, Action work) => work();
}
