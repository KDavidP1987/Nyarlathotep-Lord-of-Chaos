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
    };

    static DefinitionSet Twins(string trigger) =>
        EventValidator.Parse(Json.File(
            Json.Event("on", trigger),
            Json.Event("off", trigger).Replace("\"enabled\": true", "\"enabled\": false"),
            Json.Event("broken", trigger, extra: "\"bogus\": 1")), FakeUnits.Default()).Set;

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
}
