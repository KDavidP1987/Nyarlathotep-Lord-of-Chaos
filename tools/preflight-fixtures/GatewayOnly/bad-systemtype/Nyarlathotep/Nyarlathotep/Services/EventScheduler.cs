namespace Nyarlathotep.Services;

/// <summary>Planted (event-spawns D22, Codex step 1 F2): the scheduler calls a [Mutating] Tick of another service; only
/// SpawnTracker.Tick and EventRuntime.Tick are System-actor entry points, so Persistence.Tick fails.</summary>
internal static class EventScheduler
{
    internal static void Run()
    {
        Phase("spawn queues", SpawnTracker.Tick);
        Persistence.Tick();
    }

    static void Phase(string name, Action work) => work();
}
