namespace Nyarlathotep.Logic;

public sealed record ApiCommand(string Name, bool AdminOnly, string[] Parameters);

public static class ApiCommandTable
{
    public static readonly IReadOnlyList<ApiCommand> Commands =
    [
        new("version", false, []),
        new("status", false, []),
        new("events", true, ["page"]),
        new("sub", false, []),
    ];
}
