using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D17: the wire stays api 6 with its six push kinds, and the contract says that a Region
/// location, the region lock and the region hunt never reach the wire and that a hunt target is never named or located.</summary>
public partial class ContractDocTests
{
    internal const string HuntingGroundsSentence =
        "a Region location, the region lock and the region hunt never reach the wire, and a hunt target is never named or located.";

    /// <summary>What keeps the wire and its contract from describing hunting grounds; empty when they do.</summary>
    internal static List<string> HuntingGroundsProblems(string contract, int api, IEnumerable<string> pushKinds)
    {
        var p = new List<string>();
        if (api != 6) p.Add($"Wire.Api is {api}, not 6");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains(HuntingGroundsSentence, StringComparison.Ordinal))
            p.Add("the contract does not say a Region location, the region lock and the region hunt never reach the wire");
        return p;
    }

    [Fact]
    public void HuntingGrounds_passes_api_6_and_the_sentence() => Assert.Empty(HuntingGroundsProblems(Contract, Wire.Api, PushTypes()));

    [Fact]
    public void HuntingGrounds_fails_when_the_wire_or_the_contract_changes()
    {
        Assert.Equal(["Wire.Api is 7, not 6"], HuntingGroundsProblems(Contract, 7, PushTypes()));
        Assert.Equal(["the push kinds changed"], HuntingGroundsProblems(Contract, Wire.Api, PushTypes().Append("region-hunt")));
        Assert.Equal(["the contract does not say a Region location, the region lock and the region hunt never reach the wire"],
            HuntingGroundsProblems(Contract.Replace(HuntingGroundsSentence, ""), Wire.Api, PushTypes()));
    }

    [Fact]
    public void HuntingGrounds_empty_contract() => Assert.Single(HuntingGroundsProblems("", Wire.Api, PushTypes()));
}
