using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>The plugin's one <see cref="ActionGateway"/> (foundation D10, D11). Commands, patches and triggers wrap
/// every call of a [Mutating] service method in <see cref="Run"/>; Test-CheckGatewayOnly enforces it.</summary>
internal static class Gateway
{
    static readonly ActionGateway Instance = new(line => Core.Log.LogWarning($"[nyar] {line}"));

    internal static string Run(ActionKind kind, Actor actor, Func<string> work, bool definitionEnabled = true) =>
        Instance.Run(kind, actor, work, definitionEnabled);
}
