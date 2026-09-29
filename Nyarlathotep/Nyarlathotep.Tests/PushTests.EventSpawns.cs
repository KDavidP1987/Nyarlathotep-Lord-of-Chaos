using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D20: the new keys add no status field, push kind or api command; Wire.Api stays 5, a skipped wave
/// sends no wave push, and `api events` lists a definition with the new keys as before.</summary>
public partial class PushTests
{
    /// <summary><paramref name="d"/> with every event-spawns key set.</summary>
    static EventDefinition WithSpawnKeys(EventDefinition d) => d with
    {
        Action = d.Action! with
        {
            Units = [new UnitEntry("CHAR_Bandit_Thug", 5, 0.5)],
            Location = new Location(LocationType.AroundPlayer, 0, 0, null, 20, 40),
            Modifiers = new SpawnModifiers(LevelDelta: 2, MaxHealth: 1.5),
            Loot = true,
            Behaviour = new Behaviour(BehaviourType.Hunt, 40),
            AllowTerritory = true,
        },
    };

    /// <summary>The wire of one definition: its api events row, its status row while running, and its start, wave and end
    /// pushes.</summary>
    static List<string> WireOf(EventDefinition d)
    {
        var (hub, _, _) = New();
        var running = Running(d);
        hub.EventStarted(running.Instance);
        hub.Wave(d.Id, 1);
        hub.EventEnded(d.Id);
        var set = new DefinitionSet([d]);
        return
        [
            .. ApiLines.Definitions(set, new HashSet<string>()),
            .. ApiLines.Definitions(set, new HashSet<string> { d.Id }),
            .. ApiLines.Status([running], [], set, new Dictionary<string, int> { [d.Id] = 5 }, true, T0),
            .. Texts(hub),
        ];
    }

    /// <summary>A wave WaveGate decided, as the wave action reports it: only a spawned wave is counted and pushed.</summary>
    static void Report(PushHub hub, WaveDecision d, string id, int wave)
    {
        if (d.Outcome == WaveOutcome.Spawn) hub.Wave(id, wave);
    }

    [Theory]
    [InlineData("no eligible player")]
    [InlineData("centre claimed")]
    [InlineData("territory unknown")]
    [InlineData("player query failed")]
    [InlineData("zero rolled")]
    [InlineData("blocked")]
    public void Spawns_fails_when_skipped_wave_pushes(string skip)
    {
        var facts = new WaveFacts(2, "raid", skip == "blocked", MapFailed: skip == "territory unknown", Location: LocationType.AroundPlayer,
            Pick: skip switch { "no eligible player" => PickOutcome.NoEligible, "player query failed" => PickOutcome.QueryFailed, _ => PickOutcome.Picked },
            CentreClaimed: skip == "centre claimed");
        var d = WaveGate.Decide(facts, () => skip == "zero rolled" ? [] : ["CHAR_Bandit_Thug"], 20, 0, 150);
        Assert.NotEqual(WaveOutcome.Spawn, d.Outcome);
        Assert.Empty(d.Units);
        var (hub, _, _) = New();
        Report(hub, d, "raid", 2);
        Assert.Empty(Texts(hub));
        Assert.DoesNotContain("[NYAR:", d.Line ?? "");
    }

    /// <summary>A59: the engine counts a skipped wave without pushing it, so the next wave comes at its own time; A62: the
    /// status row's and `event info`'s spawned count leaves it out, while the push numbers the wave in the schedule.</summary>
    [Fact]
    public void Spawns_fails_when_skipped_wave_pushes_or_is_retried()
    {
        var (e, _, hub) = Wired(Json.Event("raid"));
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        var before = Texts(hub).Count;
        Assert.Equal(1, e.NextWave("raid", T0)!.Wave);
        e.WaveSkipped("raid");
        Assert.Equal(before, Texts(hub).Count);                                // no wave line
        Assert.Null(e.NextWave("raid", T0.AddSeconds(1)));                     // not retried the next second
        Assert.Equal(2, e.NextWave("raid", T0.AddSeconds(60))!.Wave);
        e.WaveSpawned("raid");
        Assert.Equal(before + 1, Texts(hub).Count);                            // a spawned wave still reports
        Assert.Contains(" wave=2", Texts(hub)[^1]);
        var active = e.Active.Single(a => a.Id == "raid");
        Assert.Equal((1, 1, 2), (active.WavesSpawned, active.WavesSkipped, active.WavesUsed));
        Assert.Contains(" wave=1/", ApiLines.Status([active], [], new DefinitionSet([active.Definition]), new Dictionary<string, int>(), true, T0.AddSeconds(61))[0]);
    }

    [Fact]
    public void Spawns_fails_when_row_carries_a_new_field()
    {
        var plain = Raid();
        var keyed = WithSpawnKeys(plain);
        Assert.Equal(WireOf(plain), WireOf(keyed));
        foreach (var line in WireOf(keyed))
            foreach (var word in new[] { "modifier", "loot", "behaviour", "hunt", "territory", "chance", "aroundplayer", "minDist" })
                Assert.DoesNotContain(word, line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Spawns_passes_api_5_and_rows_unchanged()
    {
        Assert.Equal(5, Wire.Api);
        var (hub, _, _) = New();
        var d = WaveGate.Decide(new WaveFacts(2, "raid", false, Location: LocationType.AroundPlayer, Pick: PickOutcome.Picked), () => ["CHAR_Bandit_Thug"], 20, 0, 150);
        Report(hub, d, "raid", 2);
        Assert.Equal(["[NYAR:ev] type=wave id=raid secs=0 wave=2"], Texts(hub));
        var row = ApiLines.Definitions(new DefinitionSet([WithSpawnKeys(Raid())]), new HashSet<string>()).Single();
        Assert.Equal("[NYAR:def] id=raid name=Bandit_raid enabled=1 trigger=manual action=waves duration=600 state=idle reason=- region=-", row);
    }

    [Fact]
    public void Spawns_empty_definition_without_new_keys()
    {
        var plain = Raid();
        Assert.Null(EventLines.SpawnKeys(plain.Action!));
        Assert.Equal(WireOf(plain), WireOf(plain with { Action = plain.Action! with { Modifiers = null, Behaviour = null, Loot = false, AllowTerritory = false } }));
    }
}
