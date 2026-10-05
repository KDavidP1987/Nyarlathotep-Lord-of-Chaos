using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-sets D17: the wire stays api 6 with its six push kinds; all waves defeated sends the existing event-end;
/// the contract says what a wave set's `wave=` counts and that names never reach the wire.</summary>
public partial class ContractDocTests
{
    internal const string WaveCountSentence = "A wave-set event's (0.9.0, `action.waveList`) total is the number of waves in its list.";
    internal const string NamesSentence = "player names never reach the wire, and an event that ends with all its waves defeated sends the usual `event-end`.";

    /// <summary>What keeps a contract text from describing wave sets; empty when it does.</summary>
    internal static List<string> WaveSetProblems(string contract)
    {
        var p = new List<string>();
        if (!contract.Contains(WaveCountSentence, StringComparison.Ordinal)) p.Add("the contract does not say what a wave set's wave= counts");
        if (!contract.Contains(NamesSentence, StringComparison.Ordinal)) p.Add("the contract does not say names never reach the wire");
        return p;
    }

    [Fact]
    public void WaveSets_passes_api_6_and_the_two_sentences()
    {
        Assert.Equal(6, Wire.Api);
        Assert.Empty(WaveSetProblems(Contract));
        Assert.Equal(["config-changed", "event-end", "event-start", "killswitch", "wave", "wave-warn"],
            typeof(PushLines).GetFields().Where(f => f.IsLiteral && f.Name.EndsWith("Type", StringComparison.Ordinal))
                .Select(f => (string)f.GetRawConstantValue()!).OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public void WaveSets_fails_when_contract_lacks_a_sentence()
    {
        Assert.Contains("the contract does not say what a wave set's wave= counts", WaveSetProblems(Contract.Replace(WaveCountSentence, "")));
        Assert.Contains("the contract does not say names never reach the wire", WaveSetProblems(Contract.Replace(NamesSentence, "")));
    }

    [Fact]
    public void WaveSets_fails_when_the_victory_end_sends_more_than_one_push()
    {
        var catalog = new EventCatalog();
        var file = Json.File(Json.Event("ws", action: "\"action\": { \"type\": \"SpawnWaves\", \"waveList\": [ { \"units\": [ { \"prefab\": " +
            "\"CHAR_Bandit_Thug\", \"count\": 1 } ] } ], \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }"));
        var t0 = new DateTime(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc);
        Assert.Null(catalog.Reload(EventValidator.Parse(file, FakeUnits.Default()), FileStamp.Of(t0, [1])));
        var push = new TypeRecorder();
        var engine = new EventEngine(catalog) { Push = push };
        Assert.Null(engine.Start("ws", "manual", t0, new ControlState(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3)));
        engine.WaveDecided("ws", WaveOutcome.Spawn, t0, 1);
        Assert.Single(engine.Complete(t0.AddSeconds(5), 60, (_, _) => true));
        Assert.Empty(engine.Complete(t0.AddSeconds(6), 60, (_, _) => true));
        Assert.Equal([PushLines.EventStartType, PushLines.WaveType, PushLines.EventEndType], push.Types);
    }

    sealed class TypeRecorder : IPushSink
    {
        public readonly List<string> Types = [];
        public void EventStarted(RunningInstance instance) => Types.Add(PushLines.EventStartType);
        public void EventEnded(string id, string region = "-") => Types.Add(PushLines.EventEndType);
        public void Wave(string id, int wave) => Types.Add(PushLines.WaveType);
        public void Purged(int cooldownSeconds) => Types.Add(PushLines.KillswitchType);
        public void ConfigChanged() => Types.Add(PushLines.ConfigChangedType);
    }

    [Fact]
    public void WaveSets_empty_contract() => Assert.Equal(2, WaveSetProblems("").Count);
}
