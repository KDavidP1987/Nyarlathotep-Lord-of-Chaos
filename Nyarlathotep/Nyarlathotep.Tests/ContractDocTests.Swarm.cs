using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>swarm D12: the wire stays api 6 with its six push kinds, and the contract says that an event's arena and
/// winner never reach the wire and that names never do.</summary>
public partial class ContractDocTests
{
    internal const string SwarmSentence = "an event's arena and winner never reach the wire, and names never do.";

    static readonly string[] PushKinds = ["config-changed", "event-end", "event-start", "killswitch", "wave", "wave-warn"];

    /// <summary>What keeps the wire and its contract from describing a swarm; empty when they do.</summary>
    internal static List<string> SwarmProblems(string contract, int api, IEnumerable<string> pushKinds)
    {
        var p = new List<string>();
        if (api < 6) p.Add($"Wire.Api is {api}, below 6");              // revenge D15 moved the api to 7; this child added no wire change
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains(SwarmSentence, StringComparison.Ordinal)) p.Add("the contract does not say an arena, a winner and names never reach the wire");
        return p;
    }

    static IEnumerable<string> PushTypes() =>
        typeof(PushLines).GetFields().Where(f => f.IsLiteral && f.Name.EndsWith("Type", StringComparison.Ordinal)).Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Swarm_passes_api_6_and_the_sentence() => Assert.Empty(SwarmProblems(Contract, Wire.Api, PushTypes()));

    [Fact]
    public void Swarm_fails_when_the_wire_or_the_contract_changes()
    {
        Assert.Equal(["Wire.Api is 5, below 6"], SwarmProblems(Contract, 5, PushTypes()));
        Assert.Equal(["the push kinds changed"], SwarmProblems(Contract, Wire.Api, PushTypes().Append("winner")));
        Assert.Equal(["the contract does not say an arena, a winner and names never reach the wire"],
            SwarmProblems(Contract.Replace(SwarmSentence, ""), Wire.Api, PushTypes()));
    }

    [Fact]
    public void Swarm_empty_contract() => Assert.Single(SwarmProblems("", Wire.Api, PushTypes()));
}
