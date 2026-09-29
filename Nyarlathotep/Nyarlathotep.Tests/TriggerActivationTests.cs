using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D40 (A13): for every automatic trigger kind an enabled definition is reached by its trigger and a
/// disabled twin is not; the enabled check is TriggerRouter.Candidates, shared by every kind.</summary>
public partial class TriggerActivationTests
{
    // Friday 2026-09-25 20:00 UTC.
    static readonly DateTime Now = Zones.Utc(2026, 9, 25, 20, 0);

    static readonly Dictionary<TriggerType, (string Trigger, Func<DefinitionSet, IEnumerable<string>> Fire)> Cases = new()
    {
        [TriggerType.Schedule] = ("{ \"type\": \"Schedule\", \"days\": [\"Fri\"], \"times\": [\"20:00\"] }",
            set => TriggerRouter.ScheduleDue(set, Now, TimeZoneInfo.Utc, _ => null).Select(x => x.Definition.Id)),
        [TriggerType.GameTime] = ("{ \"type\": \"GameTime\", \"phase\": \"night\" }",
            set => TriggerRouter.PhaseEntered(set, DayPhase.Night).Select(d => d.Id)),
        [TriggerType.VBloodKilled] = ("{ \"type\": \"VBloodKilled\", \"bosses\": [\"CHAR_Bandit_Tourok_VBlood\"] }",
            set => TriggerRouter.VBloodKilled(set, "CHAR_Bandit_Tourok_VBlood").Select(d => d.Id)),
        // automation D14: every stored next is due, so each Interval candidate fires
        [TriggerType.Interval] = ("{ \"type\": \"Interval\", \"minMinutes\": 5, \"maxMinutes\": 6 }",
            set =>
            {
                var nexts = set.All.ToDictionary(d => d.Id, _ => Now.AddSeconds(-1));
                return IntervalClock.Tick(set, nexts, _ => false, Now, new ScriptedRandom()).Due.Select(d => d.Id);
            }),
        // a player walks from FarbaneWoods (x < 0) into CursedForest (x >= 0)
        [TriggerType.RegionEntered] = ("{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"] }",
            set =>
            {
                var entries = new RegionEntries();
                var defs = TriggerRouter.Candidates(set, TriggerType.RegionEntered).ToList();
                var regions = new FakeRegions();
                entries.Scan([new ScanRow("p1", -10f, 0f, true)], regions.RegionOf, defs, Now);
                return entries.Scan([new ScanRow("p1", 10f, 0f, true)], regions.RegionOf, defs, Now).Select(e => e.Definition.Id);
            }),
        [TriggerType.FactionKills] = ("{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }",
            set => TriggerRouter.FactionKills(set, new KillFacts("p1", null, false, false, false, "Faction_Bandits"), null).Select(d => d.Id)),
    };

    static DefinitionSet Twins(string trigger) =>
        EventValidator.Parse(Json.File(
            Json.Event("on", trigger),
            Json.Event("off", trigger).Replace("\"enabled\": true", "\"enabled\": false"),
            Json.Event("broken", trigger, extra: "\"bogus\": 1")), FakeUnits.Default(), regions: FakeRegions.All()).Set;

    [Fact]
    public void Every_automatic_trigger_kind_has_a_case()
    {
        var automatic = Enum.GetValues<TriggerType>().Where(t => t != TriggerType.Manual).ToHashSet();
        Assert.NotEmpty(automatic);
        Assert.Equal(automatic, Cases.Keys.ToHashSet());
    }

    [Theory]
    [InlineData(TriggerType.Schedule)]
    [InlineData(TriggerType.GameTime)]
    [InlineData(TriggerType.VBloodKilled)]
    [InlineData(TriggerType.Interval)]
    [InlineData(TriggerType.RegionEntered)]
    [InlineData(TriggerType.FactionKills)]
    public void Enabled_fires_and_disabled_or_invalid_twins_do_not(TriggerType type)
    {
        var (trigger, fire) = Cases[type];
        var set = Twins(trigger);
        Assert.False(set.Find("off")!.Startable);
        Assert.NotNull(set.Find("broken")!.DisabledReason);
        Assert.Equal(["on"], fire(set).ToList());
    }

    [Fact]
    public void Triggers_reach_only_their_own_kind_and_match()
    {
        var set = EventValidator.Parse(Json.File(
            Json.Event("sched", Cases[TriggerType.Schedule].Trigger),
            Json.Event("night", Cases[TriggerType.GameTime].Trigger),
            Json.Event("day", "{ \"type\": \"GameTime\", \"phase\": \"day\" }"),
            Json.Event("boss", Cases[TriggerType.VBloodKilled].Trigger),
            Json.Event("anyboss", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }"),
            Json.Event("manual")), FakeUnits.Default()).Set;
        Assert.Equal(["sched"], TriggerRouter.ScheduleDue(set, Now, TimeZoneInfo.Utc, _ => null).Select(x => x.Definition.Id));
        Assert.Empty(TriggerRouter.ScheduleDue(set, Now, TimeZoneInfo.Utc, _ => "2026-09-25 20:00"));
        Assert.Equal(["night"], TriggerRouter.PhaseEntered(set, DayPhase.Night).Select(d => d.Id));
        Assert.Equal(["day"], TriggerRouter.PhaseEntered(set, DayPhase.Day).Select(d => d.Id));
        Assert.Equal(["anyboss", "boss"], TriggerRouter.VBloodKilled(set, "CHAR_Bandit_Tourok_VBlood").Select(d => d.Id));
        Assert.Equal(["anyboss"], TriggerRouter.VBloodKilled(set, "CHAR_Other_VBlood").Select(d => d.Id));
    }

    // regions D4: the kill filter. x < 0 is CursedForest, x >= 0 FarbaneWoods.
    static string RegionOfX(float x, float z) => x < 0 ? "CursedForest" : "FarbaneWoods";

    static DefinitionSet Regional(params string[] events) =>
        EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()).Set;

    const string Tourok = "CHAR_Bandit_Tourok_VBlood";
    const string ScopedKill = "{ \"type\": \"VBloodKilled\", \"bosses\": [\"CHAR_Bandit_Tourok_VBlood\"], \"scope\": [\"CursedForest\"] }";

    [Fact]
    public void Region_passes_kill_inside_regions()
    {
        var set = Regional(Json.Event("scoped", ScopedKill), Json.Event("global", Cases[TriggerType.VBloodKilled].Trigger));
        Assert.Equal(["global", "scoped"], TriggerRouter.VBloodKilled(set, Tourok, (-5f, 0f), RegionOfX).Select(d => d.Id).OrderBy(x => x));
        Assert.Equal(["global"], TriggerRouter.VBloodKilled(set, Tourok, (5f, 0f), RegionOfX).Select(d => d.Id));
        Assert.Equal(["global"], TriggerRouter.VBloodKilled(set, Tourok, null, RegionOfX).Select(d => d.Id));     // unreadable
        Assert.Equal(["global"], TriggerRouter.VBloodKilled(set, Tourok, (-5f, 0f), null).Select(d => d.Id));    // no lookup
    }

    [Fact]
    public void Region_empty_global_trigger()
    {
        var set = Regional(Json.Event("global", Cases[TriggerType.VBloodKilled].Trigger));
        var reads = 0;
        Assert.Equal(["global"], TriggerRouter.VBloodKilled(set, Tourok, (5f, 0f), (x, z) => { reads++; return "FarbaneWoods"; }).Select(d => d.Id));
        Assert.Equal(0, reads);
        Assert.False(TriggerRouter.NeedsKillPosition(set, Tourok));
    }

    [Fact]
    public void Region_fails_when_kill_position_unreadable()
    {
        var set = Regional(Json.Event("scoped", ScopedKill));
        var streak = new FailureStreak();
        Assert.True(TriggerRouter.UnreadableKillLogs(set, Tourok, null, streak));
        Assert.False(TriggerRouter.UnreadableKillLogs(set, Tourok, null, streak));             // same streak
        Assert.False(TriggerRouter.UnreadableKillLogs(set, Tourok, (1f, 1f), streak));         // readable: the streak ends
        Assert.True(TriggerRouter.UnreadableKillLogs(set, Tourok, null, streak));
        Assert.False(TriggerRouter.UnreadableKillLogs(set, "CHAR_Other_VBlood", null, new FailureStreak()));   // reaches no scoped one
        var global = Regional(Json.Event("global", Cases[TriggerType.VBloodKilled].Trigger));
        Assert.False(TriggerRouter.UnreadableKillLogs(global, Tourok, null, new FailureStreak()));
    }

    [Fact]
    public void Region_passes_schedule_due_once()
    {
        // The scope is checked at the start (EngineTests ScopeGate); the slot is recorded before it, so a skipped
        // occurrence is not due again in the same minute.
        var set = Regional(Json.Event("sched", Cases[TriggerType.Schedule].Trigger.Replace(" }", ", \"scope\": [\"CursedForest\"] }")));
        var due = TriggerRouter.ScheduleDue(set, Now, TimeZoneInfo.Utc, _ => null).ToList();
        var occurrence = Assert.Single(due).Occurrence;
        Assert.Empty(TriggerRouter.ScheduleDue(set, Now, TimeZoneInfo.Utc, _ => occurrence));
    }

    // ---- A38: a Global-only kill reads no position; the patch passes a reader, not a value

    [Fact]
    public void Region_empty_global_kill_reads_no_position()
    {
        var global = EventValidator.Parse(Json.File(Json.Event("g", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"] }")),
            FakeUnits.Default(), regions: FakeRegions.All()).Set;
        var reads = 0;
        Assert.Null(TriggerRouter.KillFor(global, "CHAR_Bandit_Stalker_VBlood", () => { reads++; return (1f, 1f); }));
        Assert.Equal(0, reads);
        var scoped = EventValidator.Parse(Json.File(Json.Event("s", "{ \"type\": \"VBloodKilled\", \"bosses\": [\"any\"], \"scope\": [\"CursedForest\"] }")),
            FakeUnits.Default(), regions: FakeRegions.All()).Set;
        Assert.Equal((1f, 1f), TriggerRouter.KillFor(scoped, "CHAR_Bandit_Stalker_VBlood", () => { reads++; return (1f, 1f); }));
        Assert.Equal(1, reads);
        Assert.Null(TriggerRouter.KillFor(scoped, "CHAR_Bandit_Stalker_VBlood", () => throw new InvalidOperationException("gone")));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        var patch = File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Patches", "DeathEventPatch.cs"));
        Assert.Contains("TriggerBus.VBloodKilled(died.GetPrefabGuid().GetPrefabName(), () => KillPosition(died));", patch);
        var bus = File.ReadAllText(Path.Combine(dir.FullName, "Nyarlathotep", "Nyarlathotep", "Services", "TriggerBus.cs"));
        Assert.Contains("var kill = TriggerRouter.KillFor(set, prefab, readKill);", bus);
    }
}
