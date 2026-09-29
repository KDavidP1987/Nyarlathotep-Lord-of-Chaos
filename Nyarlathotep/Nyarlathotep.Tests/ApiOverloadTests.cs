using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D11 (F6): VCF registers one overload per argument count, so no two `.nyar api` commands may
/// accept the same first word with the same count. The table is Logic/ApiCommandTable.cs, which preflight's WireContract
/// check holds equal to the command classes.</summary>
public class ApiOverloadTests
{
    [Fact]
    public void ApiOverload_passes_real_table()
    {
        Assert.Empty(ApiCommandTable.Overlaps(ApiCommandTable.Commands));
        var counts = ApiCommandTable.Overloads(ApiCommandTable.Commands).Where(o => o.Word == "event").Select(o => o.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5], counts);
        Assert.All(ApiCommandTable.Commands.Where(c => c.Name is not ("version" or "status" or "sub" or "regions")), c => Assert.True(c.AdminOnly, c.Name));
        Assert.All(ApiCommandTable.Commands.Where(c => c.Name is "event" or "template" or "templates" or "pillar" or "purge" or "killswitch"),
            c => Assert.Equal("extra", c.Parameters[^1]));
    }

    [Fact]
    public void ApiOverload_fails_when_two_commands_share_a_count()
    {
        var table = ApiCommandTable.Commands.Append(new ApiCommand("pillar", true, ["name"])).ToList();
        Assert.Equal(["pillar with 0 arguments", "pillar with 1 arguments"], ApiCommandTable.Overlaps(table));
    }

    [Fact]
    public void ApiOverload_empty_table() => Assert.Empty(ApiCommandTable.Overlaps([]));
}
