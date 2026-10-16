using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D11: the wire is unchanged: Wire.Api stays 8, no push kind is added, no status, definition or
/// template row carries pools, rolls or pursuit state, and the contract says the new fields are settable through the
/// admin-only `event set` twin like every settable field and that nothing else carries them in 0.13.0.</summary>
public partial class ContractDocTests
{
    /// <summary>Wire.Api before this child (unit-watch's api 8); wave variety adds nothing past it.</summary>
    const int WaveVarietyApi = 8;

    internal const string WaveVarietySentence = "the new fields are settable through the admin-only `event set` api twin like every settable field " +
        "(the stored value cut to 120 bytes, as today), and no row, push or read carries pools, rolls or pursuit state in 0.13.0.";

    /// <summary>A running wave-variety event: pools, a pool entry, a wave's boss roll, an action-level roll and pursuit.</summary>
    static (EventDefinition D, ActiveEvent A) WaveVarietyRun()
    {
        var units = TemplateLibraryTests.Units("CHAR_Militia_Leader_VBlood");
        var r = EventValidator.Parse(Json.File(Json.Event("wv", action:
            "\"action\": { \"type\": \"SpawnWaves\", \"pools\": { \"swarmers\": [ \"CHAR_Spider_Forest\", \"CHAR_Spider_Melee\" ], " +
            "\"warlords\": [ \"CHAR_Undead_BishopOfDeath_VBlood\", \"CHAR_Militia_Leader_VBlood\" ] }, \"waveList\": [ " +
            "{ \"units\": [ { \"pool\": \"swarmers\", \"count\": 4 } ], \"bosses\": [ { \"pool\": \"warlords\", \"min\": 1, \"max\": 1 } ] }, " +
            "{ \"units\": [ { \"prefab\": \"CHAR_Spider_Range\", \"count\": 2 } ], \"afterSeconds\": 60 } ], \"radius\": 10, " +
            "\"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"behaviour\": { \"type\": \"Hunt\", \"range\": 30 }, " +
            "\"pursuit\": { \"afterSeconds\": 45 } }")), units, regions: FakeRegions.All());
        var d = Assert.Single(r.Set.All);
        Assert.True(d.Startable, d.DisabledReason);
        var a = new ActiveEvent(new RunningInstance(d, Zones.Utc(2026, 10, 16, 20, 0), Zones.Utc(2026, 10, 16, 20, 10)), "manual", null) { Pursuing = true };
        return (d, a);
    }

    static readonly string[] WaveVarietyMarks = ["swarmers", "warlords", "pool", "pursuit", "CHAR_Spider_Melee", "CHAR_Militia_Leader_VBlood"];

    /// <summary>What keeps the wire and its contract from describing wave variety; empty when they do.</summary>
    internal static List<string> WaveVarietyProblems(string contract, int api, IEnumerable<string> pushKinds, IEnumerable<string> rows)
    {
        var p = new List<string>();
        if (api != WaveVarietyApi) p.Add($"Wire.Api is {api}, not {WaveVarietyApi}");
        if (!pushKinds.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(PushKinds)) p.Add("the push kinds changed");
        if (!contract.Contains(WaveVarietySentence, StringComparison.Ordinal)) p.Add("the contract misses the wave-variety sentence");
        foreach (var row in rows)
            if (WaveVarietyMarks.FirstOrDefault(m => row.Contains(m, StringComparison.Ordinal)) is { } mark) p.Add($"a row carries {mark}: {row}");
        return p;
    }

    static IEnumerable<string> WaveVarietyRows()
    {
        var (d, a) = WaveVarietyRun();
        var set = new DefinitionSet([d]);
        return ApiLines.Status([a], [], set, new Dictionary<string, int>(), true, Zones.Utc(2026, 10, 16, 20, 5))
            .Concat(ApiLines.Definitions(set, new HashSet<string> { d.Id }))
            .Append(PushLines.EventStart(a.Instance).Text);
    }

    [Fact]
    public void WaveVariety_passes_api_8_and_the_sentence() =>
        Assert.Empty(WaveVarietyProblems(Contract, Wire.Api, PushTypes(), WaveVarietyRows()));

    [Fact]
    public void WaveVariety_fails_when_the_api_changes_a_push_kind_is_added_or_the_contract_lacks_the_sentence()
    {
        Assert.Equal(["Wire.Api is 9, not 8"], WaveVarietyProblems(Contract, 9, PushTypes(), []));
        Assert.Equal(["the push kinds changed"], WaveVarietyProblems(Contract, Wire.Api, PushTypes().Append("pursuit"), []));
        Assert.Equal(["the contract misses the wave-variety sentence"], WaveVarietyProblems(Contract.Replace(WaveVarietySentence, ""), Wire.Api, PushTypes(), []));
        Assert.Single(WaveVarietyProblems(Contract, Wire.Api, PushTypes(), ["[NYAR:def] id=wv pools=swarmers"]));
    }

    [Fact]
    public void WaveVariety_empty_contract() => Assert.Single(WaveVarietyProblems("", WaveVarietyApi, PushKinds, []));
}
