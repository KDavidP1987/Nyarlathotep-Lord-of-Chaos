using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>horde-pools D9: the wire is unchanged: Wire.Api stays 8 and no push kind is added; the contract says that a
/// pool name may name a library pool, that `allowTerritory` appears on the three horde types and that a pool entry may
/// carry `min`/`max`; and the Raphael handoff tells the editor to keep the keys it does not know.</summary>
public partial class ContractDocTests
{
    const int HordePoolsApi = 8;

    internal static readonly string[] HordePoolsSentences =
    [
        "a pool name in an event may name a server library pool from pools.json that the event's own `pools` does not hold",
        "`allowTerritory` (true or false) now appears on the Apocalypse, Infestation and HordeMarch actions",
        "a unit pool entry of a SpawnWaves or HordeMarch action may carry `min` and `max` instead of `count`",
    ];

    internal const string HordePoolsKeepKeys = "every key it does not show must survive unchanged";

    static string PoolsHandoff => Read("RAPHAEL_HANDOFF_HORDE_POOLS.md");

    /// <summary>What keeps the wire, the contract and the handoff from describing horde pools; empty when they do.</summary>
    internal static List<string> HordePoolsProblems(string contract, string handoff, int api, IEnumerable<string> pushKinds)
    {
        var p = new List<string>();
        if (api != HordePoolsApi) p.Add($"Wire.Api is {api}, not {HordePoolsApi}");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        foreach (var sentence in HordePoolsSentences)
            if (!contract.Contains(sentence, StringComparison.Ordinal)) p.Add($"the contract misses: {sentence}");
        if (!handoff.Contains(HordePoolsKeepKeys, StringComparison.Ordinal)) p.Add("the handoff misses the keep-unknown-keys rule");
        return p;
    }

    [Fact]
    public void HordePools_passes_api_8_the_sentences_and_the_handoff() =>
        Assert.Empty(HordePoolsProblems(Contract, PoolsHandoff, Wire.Api, PushTypes()));

    [Fact]
    public void HordePools_fails_when_the_api_changes_a_push_kind_is_added_or_a_sentence_is_missing()
    {
        Assert.Equal(["Wire.Api is 9, not 8"], HordePoolsProblems(Contract, PoolsHandoff, 9, PushTypes()));
        Assert.Equal(["the push kinds changed"], HordePoolsProblems(Contract, PoolsHandoff, Wire.Api, PushTypes().Append("pool")));
        foreach (var sentence in HordePoolsSentences)
            Assert.Equal([$"the contract misses: {sentence}"], HordePoolsProblems(Contract.Replace(sentence, ""), PoolsHandoff, Wire.Api, PushTypes()));
        Assert.Equal(["the handoff misses the keep-unknown-keys rule"],
            HordePoolsProblems(Contract, PoolsHandoff.Replace(HordePoolsKeepKeys, ""), Wire.Api, PushTypes()));
    }

    [Fact]
    public void HordePools_empty_contract_and_handoff() =>
        Assert.Equal(HordePoolsSentences.Length + 1, HordePoolsProblems("", "", HordePoolsApi, PushKinds).Count);
}
