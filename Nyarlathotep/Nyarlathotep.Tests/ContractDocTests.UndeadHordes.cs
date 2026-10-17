using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D19: the wire is unchanged: Wire.Api stays 8, no push kind is added, a destination event's
/// status row is `kind=waves` and no status, definition or push row carries its destination, its march speed or the
/// spread pool, and the contract has the sentence for the two new types and the destination. Step 3 adds the
/// infestation's and the horde march's rows.</summary>
public partial class ContractDocTests
{
    /// <summary>Wire.Api before this child (unit-watch's api 8); undead hordes adds nothing past it.</summary>
    const int UndeadHordesApi = 8;

    internal const string UndeadHordesSentence = "an infestation or a horde march reports like an apocalypse, a status row `kind=waves`, and its " +
        "origins, fronts and destination, like a wave's destination, reach no row, push or read beyond what api 8 already sends admins.";

    static readonly string[] UndeadHordesMarks = ["destination", "the camp", "-1000", "-800", "marchSpeed", "march", "perKillMin", "elite"];

    /// <summary>What keeps the wire and its contract from describing undead hordes; empty when they do.</summary>
    internal static List<string> UndeadHordesProblems(string contract, int api, IEnumerable<string> pushKinds, IEnumerable<string> rows)
    {
        var p = new List<string>();
        if (api != UndeadHordesApi) p.Add($"Wire.Api is {api}, not {UndeadHordesApi}");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains(UndeadHordesSentence, StringComparison.Ordinal)) p.Add("the contract misses the undead-hordes sentence");
        foreach (var row in rows)
            if (UndeadHordesMarks.FirstOrDefault(m => row.Contains(m, StringComparison.Ordinal)) is { } mark) p.Add($"a row carries {mark}: {row}");
        return p;
    }

    /// <summary>A running destination event (marching at 3 m/s to "the camp" at (-1000, -800)) and a running apocalypse
    /// spreading 1-3 from a pool: their status, definition and start rows.</summary>
    static IEnumerable<string> UndeadHordesRows()
    {
        var march = Assert.Single(EventValidator.Parse(Json.File(Json.Event("raid", action: Json.ValidAction[..^2] +
            ", \"destination\": { \"location\": { \"type\": \"Point\", \"x\": -1000, \"z\": -800 }, \"radius\": 20, \"name\": \"the camp\" }, \"marchSpeed\": 3.0 }")),
            FakeUnits.Default(), regions: FakeRegions.All()).Set.All);
        Assert.True(march.Startable, march.DisabledReason);
        var apo = Apo.Def(Apo.Action(roamers: "{ \"perHotspot\": 3, \"perKillMin\": 1, \"perKillMax\": 3, \"max\": 12, \"perMinute\": 4, \"huntRange\": 40, \"pool\": \"elite\" }",
            extra: "\"pools\": { \"elite\": [ \"" + Apo.Deadeye + "\" ] }"));
        var t = Zones.Utc(2026, 10, 31, 20, 0);
        var running = new[] { march, apo }.Select(d => new ActiveEvent(new RunningInstance(d, t, t.AddSeconds(d.DurationSeconds)), "manual", null)).ToList();
        var set = new DefinitionSet([march, apo]);
        var status = ApiLines.Status(running, [], set, new Dictionary<string, int>(), true, t.AddMinutes(1)).ToList();
        Assert.Contains(status, r => r.Contains("id=raid", StringComparison.Ordinal) && r.Contains("kind=waves", StringComparison.Ordinal));
        return status.Concat(ApiLines.Definitions(set, new HashSet<string> { march.Id, apo.Id })).Concat(running.Select(a => PushLines.EventStart(a.Instance).Text));
    }

    [Fact]
    public void UndeadHordes_passes_api_8_and_the_sentence() =>
        Assert.Empty(UndeadHordesProblems(Contract, Wire.Api, PushTypes(), UndeadHordesRows()));

    [Fact]
    public void UndeadHordes_fails_when_the_api_changes_a_push_kind_is_added_or_the_contract_lacks_the_sentence()
    {
        Assert.Equal(["Wire.Api is 9, not 8"], UndeadHordesProblems(Contract, 9, PushTypes(), []));
        Assert.Equal(["the push kinds changed"], UndeadHordesProblems(Contract, Wire.Api, PushTypes().Append("march"), []));
        Assert.Equal(["the contract misses the undead-hordes sentence"], UndeadHordesProblems(Contract.Replace(UndeadHordesSentence, ""), Wire.Api, PushTypes(), []));
        Assert.Single(UndeadHordesProblems(Contract, Wire.Api, PushTypes(), ["[NYAR:def] id=raid destination=the camp"]));
    }

    [Fact]
    public void UndeadHordes_empty_contract() => Assert.Single(UndeadHordesProblems("", UndeadHordesApi, PushKinds, []));
}
