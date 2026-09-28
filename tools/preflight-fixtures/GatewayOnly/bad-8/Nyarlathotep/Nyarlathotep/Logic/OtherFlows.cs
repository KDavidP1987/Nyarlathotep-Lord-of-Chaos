namespace Nyarlathotep.Logic;

/// <summary>Another Logic file holding an IAdminOps (the plant of GatewayOnly/bad-8, A2).</summary>
public sealed class OtherFlows(IAdminOps ops)
{
    public Outcome Reload() => ops.OpReload();
}
