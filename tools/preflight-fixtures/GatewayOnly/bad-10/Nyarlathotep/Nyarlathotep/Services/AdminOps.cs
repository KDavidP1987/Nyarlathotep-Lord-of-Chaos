using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>The game side of the admin flows (raphael-api-admin D11): a dispatched service, one call per member.</summary>
internal sealed class AdminOps : IAdminOps
{
    internal static readonly AdminOps Instance = new();

    [Mutating]
    public Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin) => Outcome.Done(EventStore.Reload());

    [Mutating]
    public Outcome OpStopEvent(string id) => Outcome.Done(EventStore.Reload());

    [Mutating]
    public Outcome OpReload() => Outcome.Done(EventStore.Reload());
}
