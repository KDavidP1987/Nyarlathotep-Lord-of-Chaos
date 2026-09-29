using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>Planted (event-spawns D22): a command calls the System-actor entry point SpawnTracker.Tick directly.</summary>
[CommandGroup("nyar")]
internal static class StatusCommands
{
    [Command("status", description: "Active events and their time left.")]
    public static void Status(ChatCommandContext ctx) => SpawnTracker.Tick();
}
