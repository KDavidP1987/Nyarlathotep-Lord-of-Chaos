using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>The plugin's one <see cref="ActionGateway"/> (foundation D10, D11). Commands, patches and triggers wrap
/// every call of a [Mutating] service method in <see cref="Run"/>; Test-CheckGatewayOnly enforces it.</summary>
internal static class Gateway
{
    static readonly ActionGateway Instance = new(line => Core.Log.LogWarning($"[nyar] {line}"));

    /// <summary>The admin flows over this gateway and the game (raphael-api-admin D3); the human commands and the twins
    /// call these. The purge arming lives here, one for both.</summary>
    internal static readonly AdminFlows Flows = new(AdminOps.Instance, Instance, new PurgeArming(),
        line => Core.Log.LogInfo($"[nyar] {line}"), () => DateTime.UtcNow, new RateGate(), line => Core.Log.LogWarning($"[nyar] {line}"));

    internal static string Run(ActionKind kind, Actor actor, Func<string> work, bool definitionEnabled = true) =>
        Instance.Run(kind, actor, work, definitionEnabled);

    internal static Outcome Run(ActionKind kind, Actor actor, Func<Outcome> work, bool definitionEnabled = true) =>
        Instance.Run(kind, actor, work, definitionEnabled);
}
