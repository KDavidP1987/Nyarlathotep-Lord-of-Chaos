using Nyarlathotep.Logic;
using Nyarlathotep.Services;
using VampireCommandFramework;

namespace Nyarlathotep.Commands;

/// <summary>Planted (event-spawns D22, A16): a command calls a [Mutating] instance method through its receiver.</summary>
[CommandGroup("nyar")]
internal static class StatusCommands
{
    [Command("status", description: "Active events and their time left.")]
    public static void Status(ChatCommandContext ctx) => Persistence.Disk.Delete(DataFile.Events, FileVariant.Tmp);
}
