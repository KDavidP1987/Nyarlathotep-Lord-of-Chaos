using ProjectM;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>Planted (EntityWrites/bad-split): a GetBuffer local, spelled as UnitSetup.cs spells it, cleared in a later
/// statement, in a new file outside $DispatchedServices.</summary>
internal static class Stray
{
    internal static void Reset(Entity buff)
    {
        var mods = Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(buff);
        var count = mods.Length;
        if (count > 0)
            mods.Clear();
    }
}
