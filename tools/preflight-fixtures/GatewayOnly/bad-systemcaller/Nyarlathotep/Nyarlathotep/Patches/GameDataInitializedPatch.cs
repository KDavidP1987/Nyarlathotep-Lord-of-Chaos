using Nyarlathotep.Logic;
using Nyarlathotep.Services;

namespace Nyarlathotep.Patches;

/// <summary>Planted (event-spawns D22): a patch is a System-actor caller, but only for the tick and boot entry points;
/// Persistence.Delete is not one.</summary>
internal static class GameDataInitializedPatch
{
    public static void OneShotInit() => Persistence.Disk.Delete(DataFile.Events, FileVariant.Tmp);
}
