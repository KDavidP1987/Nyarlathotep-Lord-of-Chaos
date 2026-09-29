namespace Nyarlathotep.Logic;

/// <summary>One `.nyar api` command as VampireCommandFramework registers it: its word, whether it is admin-only, and its
/// parameters after the context, all optional (raphael-api-admin D11).</summary>
public sealed record ApiCommand(string Name, bool AdminOnly, string[] Parameters);

/// <summary>A copy of every `[CommandGroup("nyar api")]` command's signature (Commands/ApiCommands.cs and
/// Commands/ApiAdminCommands.cs). tools/preflight.ps1's WireContract check fails when the commands and this table differ,
/// so ApiOverloadTests can list VCF's overloads without loading the game (raphael-api-admin D11).</summary>
public static class ApiCommandTable
{
    public static readonly IReadOnlyList<ApiCommand> Commands =
    [
        new("version", false, []),
        new("status", false, []),
        new("events", true, ["page"]),
        new("sub", false, ["state"]),
        new("event", true, ["verb", "id", "field", "value", "extra"]),
        new("template", true, ["verb", "template", "asWord", "asId", "extra"]),
        new("templates", true, ["pillar", "page", "extra"]),
        new("pillar", true, ["name", "state", "extra"]),
        new("purge", true, ["confirm", "extra"]),
        new("killswitch", true, ["extra"]),
        new("regions", false, ["page"]),
    ];

    /// <summary>The (word, argument count) pairs VCF registers: one per count from 0 to the parameter count, since every
    /// parameter is optional.</summary>
    public static IEnumerable<(string Word, int Count)> Overloads(IEnumerable<ApiCommand> table) =>
        table.SelectMany(c => Enumerable.Range(0, c.Parameters.Length + 1).Select(n => (c.Name, n)));

    /// <summary>Every (word, count) that two commands of the group both accept; empty when VCF can route each one.</summary>
    public static List<string> Overlaps(IEnumerable<ApiCommand> table) =>
        Overloads(table).GroupBy(o => o, o => o).Where(g => g.Count() > 1).Select(g => $"{g.Key.Word} with {g.Key.Count} arguments").ToList();
}
