namespace Nyarlathotep.Logic;

/// <summary>The game side of the admin flows; its declarations are not accesses (A1).</summary>
public interface IAdminOps
{
    Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin);
    Outcome OpStopEvent(string id);
    Outcome OpReload();
}

/// <summary>Each flow reaches IAdminOps only inside ActionGateway.Run&lt;T&gt; (raphael-api-admin D11). A literal
/// naming ".OpReload(" is text, not a call.</summary>
public sealed class AdminFlows(IAdminOps ops, ActionGateway gateway)
{
    public Outcome Start(string id)
    {
        var note = $"start {id} via ops.OpStartEvent(";
        return gateway.Run<Outcome>(ActionKind.StartEvent, Actor.Admin, () => ops.OpStartEvent(id, null), ActionGateway.Denied);
    }

    public Outcome Stop(string id) =>
        gateway.Run<Outcome>(ActionKind.EndEvent, Actor.Admin, () => Pick<Outcome, string>(id, ops.OpStopEvent(id)), ActionGateway.Denied);

    static Outcome Pick<TOut, TIn>(TIn _, TOut o) => (Outcome)(object)o!;

    public Outcome ReloadEvents() =>
        gateway.Run<Outcome>(ActionKind.LoadDefinitions, Actor.Admin, () =>
        {
            // ops.OpReload() in a comment is not a call either
            return ops.OpReload();
        }, ActionGateway.Denied);
}
