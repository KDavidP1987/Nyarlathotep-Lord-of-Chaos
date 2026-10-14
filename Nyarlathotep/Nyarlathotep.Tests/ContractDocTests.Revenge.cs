using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D15: `[NYAR:def]` and `[NYAR:tpl]` rows of a Revenge definition carry trigger=revenge; Wire.Api is the
/// previous release's plus one; the contract's §3 trigger list, its history table and its header note name `revenge` with
/// that api; no push kind is added.</summary>
public partial class ContractDocTests
{
    /// <summary>Wire.Api at the release before this child (hunting-grounds, 0.11.0).</summary>
    const int RevengePreviousApi = 6;

    /// <summary>The api revenge shipped in; a later child (unit-watch D13, api 8) may raise Wire.Api past it.</summary>
    const int RevengeApi = RevengePreviousApi + 1;

    static EventDefinition RevengeDefinition()
    {
        var r = EventValidator.Parse(Json.File(Json.Event("rv", Json.RevengeTrigger, action: Json.RevengeAction)), FakeUnits.Default(), regions: FakeRegions.All());
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        return d;
    }

    /// <summary>The rows a Revenge definition reaches the wire in: its def row and its tpl row.</summary>
    static IReadOnlyList<string> RevengeRows(EventDefinition d) =>
        [.. ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string>()), ApiLines.Tpl(new TemplateEntry(d, new JsonObject()))];

    /// <summary>What keeps the wire and its contract from describing revenge; empty when they do.</summary>
    internal static List<string> RevengeProblems(string contract, int api, IReadOnlyList<string> rows, IEnumerable<string> pushKinds)
    {
        var p = new List<string>();
        if (api < RevengeApi) p.Add($"Wire.Api is {api}, below {RevengeApi}");
        foreach (var row in rows.Where(r => !r.Contains(" trigger=revenge ", StringComparison.Ordinal))) p.Add($"a row carries another trigger: {row}");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains($"> **Current api:** {api}", StringComparison.Ordinal)) p.Add("the contract's header names another api");
        if (!contract.Contains($"`revenge` from api {RevengeApi}", StringComparison.Ordinal)) p.Add("the §3 trigger list misses revenge");
        if (!contract.Contains($"| {RevengeApi} | 0.12.0 (revenge) |", StringComparison.Ordinal)) p.Add("the change history misses the revenge row");
        if (!contract.Contains($"api {RevengeApi} ships with the `revenge` release", StringComparison.Ordinal)) p.Add("the header note misses revenge");
        return p;
    }

    [Fact]
    public void Revenge_passes_api_7_and_the_value()
    {
        var rows = RevengeRows(RevengeDefinition());
        Assert.Equal(2, rows.Count);
        Assert.StartsWith("[NYAR:def] ", rows[0]);
        Assert.StartsWith("[NYAR:tpl] ", rows[1]);
        Assert.Equal("revenge", ApiLines.Trigger(TriggerType.Revenge));
        Assert.Empty(RevengeProblems(Contract, Wire.Api, rows, PushTypes()));
    }

    [Fact]
    public void Revenge_fails_when_a_row_carries_another_value_or_the_contract_lags()
    {
        var rows = RevengeRows(RevengeDefinition());
        Assert.Equal(["Wire.Api is 6, below 7", "the contract's header names another api"],   // unit-watch D13: api 8 keeps revenge's rows
            RevengeProblems(Contract, 6, rows, PushTypes()));
        var other = rows.Select(r => r.Replace(" trigger=revenge ", " trigger=factionkills ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, RevengeProblems(Contract, Wire.Api, other, PushTypes()).Count);
        Assert.Equal(["the §3 trigger list misses revenge"],
            RevengeProblems(Contract.Replace("`revenge` from api 7", "", StringComparison.Ordinal), Wire.Api, rows, PushTypes()));
        Assert.Equal(["the change history misses the revenge row"],
            RevengeProblems(Contract.Replace("| 7 | 0.12.0 (revenge) |", "| 7 | 0.12.0 |", StringComparison.Ordinal), Wire.Api, rows, PushTypes()));
        // no heat, player or position reaches the rows
        Assert.All(rows, r => Assert.DoesNotMatch(@"heat|warnAt|player=|x=|z=", r));
    }

    [Fact]
    public void Revenge_fails_when_a_push_kind_is_added() =>
        Assert.Equal(["the push kinds changed"], RevengeProblems(Contract, Wire.Api, RevengeRows(RevengeDefinition()), PushTypes().Append("revenge")));

    [Fact]
    public void Revenge_empty_contract() =>
        Assert.Equal(4, RevengeProblems("", Wire.Api, RevengeRows(RevengeDefinition()), PushTypes()).Count);
}
