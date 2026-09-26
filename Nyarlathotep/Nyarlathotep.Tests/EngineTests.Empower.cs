using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D6 (one empowerment per faction, after the controls) and D9 (dispatch by action; an
/// Empower event has no cleanup).</summary>
public partial class EngineTests
{
    static string Emp(string id, string factions, string? extra = null) =>
        Json.Empower(id, action: Json.EmpowerAction(factions, extra: extra));

    [Fact]
    public void Two_Empower_events_never_share_a_faction()
    {
        var e = Engine(Emp("a", "[\"Faction_Legion\", \"Faction_Bandits\"]"), Emp("b", "[\"Faction_Undead\", \"Faction_Bandits\"]"),
            Emp("c", "[\"Faction_Undead\"]"));
        Assert.Null(e.Start("a", "manual", T0, Open()));
        Assert.Equal("faction Bandits already empowered by a", e.Start("b", "manual", T0, Open(active: 1)));
        Assert.Null(e.Start("c", "manual", T0, Open(active: 1)));             // disjoint: allowed
        Assert.Equal(["a", "c"], e.Active.Select(x => x.Id).OrderBy(x => x));
    }

    [Fact]
    public void Two_Empower_events_never_share_an_include_unit()
    {
        var e = Engine(Emp("a", "[\"Faction_Legion\"]", "\"includeUnits\": [\"CHAR_Bandit_Thug\"]"),
            Emp("b", "[\"Faction_Undead\"]", "\"includeUnits\": [\"CHAR_Bandit_Deadeye\", \"CHAR_Bandit_Thug\"]"));
        Assert.Null(e.Start("a", "manual", T0, Open()));
        Assert.Equal("unit CHAR_Bandit_Thug already empowered by a", e.Start("b", "manual", T0, Open(active: 1)));
    }

    [Fact]
    public void The_faction_refusal_comes_after_every_higher_control()
    {
        var e = Engine(Emp("a", "[\"Faction_Bandits\"]"), Emp("b", "[\"Faction_Bandits\"]"), Json.Event("raid"));
        Assert.Null(e.Start("a", "manual", T0, Open()));
        Assert.Equal("purge cooldown active", e.Start("b", "manual", T0, Open(active: 1, purge: true)));
        Assert.Equal("General.Enabled is false", e.Start("b", "manual", T0, new ControlState(false, false, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 1, 3)));
        Assert.Equal("pillar empowerment is off", e.Start("b", "manual", T0, new ControlState(false, true, new HashSet<Pillar> { Pillar.Spawns }, 1, 3)));
        Assert.Equal("skipped by MaxConcurrentEvents", e.Start("b", "manual", T0, Open(active: 1, max: 1)));
        Assert.Equal("faction Bandits already empowered by a", e.Start("b", "manual", T0, Open(active: 1)));
        Assert.Null(e.Start("raid", "manual", T0, Open(active: 1)));          // a spawn event is not affected
    }

    [Fact]
    public void A_disabled_Empower_definition_is_refused_before_the_faction_rule()
    {
        var e = Engine(Emp("a", "[\"Faction_Bandits\"]"),
            Emp("b", "[\"Faction_Bandits\"]").Replace("\"enabled\": true", "\"enabled\": false"));
        Assert.Null(e.Start("a", "manual", T0, Open()));
        Assert.Equal("event b is disabled", e.Start("b", "manual", T0, Open(active: 1)));
    }

    [Fact]
    public void The_same_faction_may_be_empowered_again_after_the_first_ends()
    {
        var e = Engine(Emp("a", "[\"Faction_Bandits\"]"), Emp("b", "[\"Faction_Bandits\"]"));
        Assert.Null(e.Start("a", "manual", T0, Open()));
        Assert.NotNull(e.Cancel("a"));
        Assert.Null(e.Start("b", "manual", T0, Open()));
    }

    [Fact]
    public void ActionKindOf_maps_each_action_to_its_own_kind()
    {
        var set = EventValidator.Parse(Json.File(Json.Event("raid"), Emp("surge", "[\"Faction_Bandits\"]"), Json.Event("broken", extra: "\"bogus\": 1")),
            FakeUnits.Default()).Set;
        Assert.Equal(EventActionKind.Waves, EventActions.ActionKindOf(set.Find("raid")!));
        Assert.Equal(EventActionKind.Empower, EventActions.ActionKindOf(set.Find("surge")!));
        Assert.Equal(EventActionKind.None, EventActions.ActionKindOf(set.Find("broken")!));
    }

    [Fact]
    public void Only_a_Waves_event_gets_a_cleanup_at_its_end()
    {
        var e = Engine(Emp("surge", "[\"Faction_Bandits\"]"), Json.Event("raid"));
        e.Start("surge", "manual", T0, Open());
        e.Start("raid", "manual", T0, Open(active: 1));
        Assert.Null(e.NextWave("surge", T0));                                 // no waves for an Empower event
        var ended = e.Expire(T0.AddSeconds(600), 30);
        Assert.Equal(["raid", "surge"], ended.Select(a => a.Id).OrderBy(x => x));
        Assert.Equal("raid", Assert.Single(e.PendingCleanups).EventId);
    }

    [Fact]
    public void Event_info_describes_an_Empower_action()
    {
        var d = Json.One(Json.Empower("surge", action: Json.EmpowerAction("[\"Faction_Legion\", \"Faction_Bandits\"]",
            "{ \"physicalPower\": 1.5, \"attackSpeed\": 1.15 }", "\"excludeUnits\": [\"CHAR_Bandit_Deadeye\"]")));
        var lines = EventLines.Info(d, null, T0);
        Assert.Contains("action: empower Legion, Bandits, not CHAR_Bandit_Deadeye: physicalPower x1.5, attackSpeed x1.15", lines);
        var e = Engine(Json.Empower("surge"));
        e.Start("surge", "manual", T0, Open());
        Assert.Equal("running: started by manual, 600s left", EventLines.Info(e.Find("surge")!.Definition, e.Find("surge"), T0).Last());
    }
}
