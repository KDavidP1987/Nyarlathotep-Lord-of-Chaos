using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D10: {faction} of an Empower event is its factions' short names joined by ", ".</summary>
public partial class AnnouncerTests
{
    [Fact]
    public void Faction_of_an_Empower_event_is_its_short_names()
    {
        var d = Json.One(Json.Empower("surge", action: Json.EmpowerAction("[\"Faction_Legion\", \"Faction_Bandits\"]"),
            extra: "\"announce\": { \"start\": [\"The {faction} rise: {event}.\"], \"end\": [\"The {faction} fall back.\"] }"));
        Assert.Equal("Legion, Bandits", MessageContext.For(d, 10, 1).Faction);
        Assert.Equal("The Legion, Bandits rise: Bandit raid.", Messages.StartBanner(d, 10, 0));
        Assert.Equal("The Legion, Bandits fall back.", Messages.EndBanner(d, 0));
    }

    [Fact]
    public void Faction_of_a_SpawnWaves_event_keeps_the_first_unit_rule()
    {
        var d = Json.One(Json.Event("raid"));
        Assert.Equal("Bandit", MessageContext.For(d, 10, 1).Faction);
        Assert.Equal("Undead", MessageContext.For(Json.One(Json.Event("r2", action:
            "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Undead_SkeletonSoldier_Base\", \"count\": 1 } ], " +
            "\"waves\": 1, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }")), 0, 1).Faction);
    }
}
