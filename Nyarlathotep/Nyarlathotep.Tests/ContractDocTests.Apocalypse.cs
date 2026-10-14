using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D20: the wire is unchanged: Wire.Api stays 7, no push kind is added, an apocalypse's status row
/// counts hordes risen of hordes as `wave=`, every outcome sends one `event-end`, and the contract says so in its two
/// sentences.</summary>
public partial class ContractDocTests
{
    /// <summary>Wire.Api at the release before this child (revenge, 0.12.0); an apocalypse keeps it.</summary>
    const int ApocalypseApi = 7;

    const string ApocalypseNoChange = "An apocalypse (0.13.0, `\"type\": \"Apocalypse\"`) adds no tag, key, push kind or command: Wire.Api stays 7";
    const string ApocalypseStatusRow = "Its status row is `kind=waves` with `wave=<hordes risen>/<hordes>`, and every outcome (won, lost, survived) sends the usual `event-end`.";

    static readonly DateTime ApoT0 = Zones.Utc(2026, 10, 31, 20, 0);

    /// <summary>What keeps the wire and its contract from describing an apocalypse; empty when they do.</summary>
    internal static List<string> ApocalypseProblems(string contract, int api, IEnumerable<string> pushKinds, IReadOnlyList<string> status)
    {
        var p = new List<string>();
        if (api != ApocalypseApi) p.Add($"Wire.Api is {api}, not {ApocalypseApi}");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains(ApocalypseNoChange, StringComparison.Ordinal)) p.Add("the contract misses the no-change sentence");
        if (!contract.Contains(ApocalypseStatusRow, StringComparison.Ordinal)) p.Add("the contract misses the status sentence");
        if (!status.Any(l => l.Contains(" kind=waves ", StringComparison.Ordinal) && l.Contains(" wave=2/3", StringComparison.Ordinal)))
            p.Add("the status row does not count hordes");
        return p;
    }

    /// <summary>The status rows of apo running with 2 of its 3 hordes risen.</summary>
    static IReadOnlyList<string> ApocalypseStatus()
    {
        var set = Apo.Set(Apo.Event());
        var engine = ApocalypseTests.Engine(out _, Apo.Event());
        engine.ResumePending = "apo";
        Assert.Null(engine.Resume("apo", ApoT0, ApoT0.AddHours(1), (500, 12, 0), hordesRisen: 2));
        var active = engine.Find("apo")!;
        return ApiLines.Status([active], [], set, new Dictionary<string, int>(), true, ApoT0.AddMinutes(7));
    }

    [Fact]
    public void Apocalypse_passes_api_7_and_the_status_row() =>
        Assert.Empty(ApocalypseProblems(Contract, Wire.Api, PushTypes(), ApocalypseStatus()));

    [Fact]
    public void Apocalypse_fails_when_the_api_changes_or_the_contract_lacks_a_sentence()
    {
        Assert.Equal(["Wire.Api is 8, not 7"], ApocalypseProblems(Contract, 8, PushTypes(), ApocalypseStatus()));
        Assert.Equal(["the contract misses the no-change sentence"],
            ApocalypseProblems(Contract.Replace(ApocalypseNoChange, "", StringComparison.Ordinal), Wire.Api, PushTypes(), ApocalypseStatus()));
        Assert.Equal(["the contract misses the status sentence"],
            ApocalypseProblems(Contract.Replace(ApocalypseStatusRow, "", StringComparison.Ordinal), Wire.Api, PushTypes(), ApocalypseStatus()));
        Assert.Equal(["the status row does not count hordes"],
            ApocalypseProblems(Contract, Wire.Api, PushTypes(), ApocalypseStatus().Select(l => l.Replace(" wave=2/3", " wave=2/1", StringComparison.Ordinal)).ToList()));
        // nothing of the run reaches a row: no hotspot, front, goal or roamer
        Assert.All(ApocalypseStatus(), r => Assert.DoesNotMatch(@"hotspot|front|goal|roamer|x=|z=", r));
    }

    [Fact]
    public void Apocalypse_fails_when_a_push_kind_is_added() =>
        Assert.Equal(["the push kinds changed"], ApocalypseProblems(Contract, Wire.Api, PushTypes().Append("apocalypse"), ApocalypseStatus()));

    [Fact]
    public void Apocalypse_fails_when_an_outcome_sends_anything_but_one_event_end()
    {
        foreach (var outcome in new[] { "won", "lost", "survived" })
        {
            var engine = ApocalypseTests.Engine(out var push, Apo.Event());
            Assert.Null(engine.Start("apo", "manual", ApoT0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
            if (outcome == "survived") Assert.Single(engine.Expire(ApoT0.AddHours(1), 60));
            else Assert.NotNull(engine.Complete("apo", ApoT0.AddMinutes(20), 60));
            engine.Expire(ApoT0.AddHours(2), 60);
            Assert.Equal([PushLines.EventStartType, PushLines.EventEndType], push.Types);
        }
    }

    [Fact]
    public void Apocalypse_empty_contract() =>
        Assert.Equal(2, ApocalypseProblems("", Wire.Api, PushTypes(), ApocalypseStatus()).Count);
}
