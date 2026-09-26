using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D1 (the Empower action's schema) and D2 (the pillar and action pair).</summary>
public partial class EventValidationTests
{
    [Fact]
    public void A_valid_Empower_definition_loads_enabled_with_its_defaults()
    {
        var d = Json.One(Json.Empower("surge", action: Json.EmpowerAction(
            "[\"Faction_Legion\", \"Faction_Bandits\"]",
            "{ \"physicalPower\": 1.5, \"maxHealth\": 1.25, \"attackSpeed\": 1, \"moveSpeed\": 3 }")));
        Assert.Null(d.DisabledReason);
        Assert.True(d.Startable);
        Assert.Equal(Pillar.Empowerment, d.Pillar);
        Assert.Null(d.Action);
        var e = Assert.IsType<EmpowerAction>(d.Empower);
        Assert.Equal(["Faction_Legion", "Faction_Bandits"], e.Factions);
        Assert.Empty(e.IncludeUnits);
        Assert.Empty(e.ExcludeUnits);
        Assert.False(e.IncludeVBloods);
        Assert.Equal(new EmpowerStats(1.5, 1.0, 1.25, 1.0, 3.0), e.Stats);
    }

    [Fact]
    public void An_Empower_definition_with_every_key_loads()
    {
        var d = Json.One(Json.Empower("surge", action: Json.EmpowerAction(extra:
            "\"includeUnits\": [\"CHAR_Undead_SkeletonSoldier_Base\"], \"excludeUnits\": [\"CHAR_Bandit_Deadeye\"], \"includeVBloods\": true")));
        Assert.Null(d.DisabledReason);
        var e = d.Empower!;
        Assert.Equal(["CHAR_Undead_SkeletonSoldier_Base"], e.IncludeUnits);
        Assert.Equal(["CHAR_Bandit_Deadeye"], e.ExcludeUnits);
        Assert.True(e.IncludeVBloods);
    }

    static string TwentyOne => "[" + string.Join(", ", Enumerable.Repeat("\"CHAR_Bandit_Thug\"", 21)) + "]";

    public static IEnumerable<object[]> InvalidEmpower() =>
    [
        // factions
        [Json.EmpowerAction("[\"Faction_Nobody\"]"), "unknown faction Faction_Nobody"],
        [Json.EmpowerAction("[\"Bandits\"]"), "unknown faction Bandits"],
        [Json.EmpowerAction("[\"Faction_Players\"]"), "faction Faction_Players is deny-listed"],
        [Json.EmpowerAction("[\"Faction_Players_Mutant\"]"), "faction Faction_Players_Mutant is deny-listed"],
        [Json.EmpowerAction("[\"Faction_Traders_T01\"]"), "faction Faction_Traders_T01 is deny-listed"],
        [Json.EmpowerAction("[\"Faction_Critters\"]"), "faction Faction_Critters is deny-listed"],
        [Json.EmpowerAction("[\"Faction_World_Prisoners\"]"), "faction Faction_World_Prisoners is deny-listed"],
        [Json.EmpowerAction("[\"Faction_Ignored\"]"), "faction Faction_Ignored is deny-listed"],
        [Json.EmpowerAction("[\"Faction_Bandits\", \"Faction_Bandits\"]"), "action.factions must be 1-5 distinct Faction_ names"],
        [Json.EmpowerAction("[\"Faction_Bandits\", \"Faction_Legion\", \"Faction_Undead\", \"Faction_Militia\", \"Faction_ChurchOfLum\", \"Faction_Gloomrot\"]"),
            "action.factions must be 1-5 distinct Faction_ names"],
        [Json.EmpowerAction("[]"), "action.factions must be 1-5 distinct Faction_ names"],
        [Json.EmpowerAction("\"Faction_Bandits\""), "action.factions must be 1-5 distinct Faction_ names"],
        [Json.EmpowerAction("[1]"), "action.factions must be 1-5 distinct Faction_ names"],
        // includeUnits and excludeUnits
        [Json.EmpowerAction(extra: "\"includeUnits\": [\"CHAR_Nobody\"]"), "unknown unit CHAR_Nobody"],
        [Json.EmpowerAction(extra: "\"includeUnits\": [\"CHAR_Mount_Horse_Vampire\"]"), "unit CHAR_Mount_Horse_Vampire is deny-listed"],
        [Json.EmpowerAction(extra: "\"includeUnits\": \"CHAR_Bandit_Thug\""), "action.includeUnits must be 0-20 distinct CHAR_ names"],
        [Json.EmpowerAction(extra: "\"includeUnits\": [\"CHAR_Bandit_Thug\", \"CHAR_Bandit_Thug\"]"), "action.includeUnits must be 0-20 distinct CHAR_ names"],
        [Json.EmpowerAction(extra: "\"includeUnits\": " + TwentyOne), "action.includeUnits must be 0-20 distinct CHAR_ names"],
        [Json.EmpowerAction(extra: "\"excludeUnits\": [\"CHAR_Nobody\"]"), "unknown unit CHAR_Nobody"],
        [Json.EmpowerAction(extra: "\"excludeUnits\": [\"CHAR_Bandit_Thug\", \"CHAR_Bandit_Thug\"]"), "action.excludeUnits must be 0-20 distinct CHAR_ names"],
        [Json.EmpowerAction(extra: "\"excludeUnits\": " + TwentyOne), "action.excludeUnits must be 0-20 distinct CHAR_ names"],
        // includeVBloods
        [Json.EmpowerAction(extra: "\"includeVBloods\": \"true\""), "action.includeVBloods must be true or false"],
        [Json.EmpowerAction(extra: "\"includeVBloods\": 1"), "action.includeVBloods must be true or false"],
        // stats
        [Json.EmpowerAction(stats: "{ \"physicalPower\": 0.9 }"), "action.stats.physicalPower must be a number 1.0-3.0"],
        [Json.EmpowerAction(stats: "{ \"maxHealth\": 3.01 }"), "action.stats.maxHealth must be a number 1.0-3.0"],
        [Json.EmpowerAction(stats: "{ \"spellPower\": \"1.5\" }"), "action.stats.spellPower must be a number 1.0-3.0"],
        [Json.EmpowerAction(stats: "{ \"physicalPower\": 1.0, \"moveSpeed\": 1 }"), "action.stats must raise at least one stat above 1.0"],
        [Json.EmpowerAction(stats: "{ }"), "action.stats must raise at least one stat above 1.0"],
        [Json.EmpowerAction(stats: "{ \"visual\": 1.5 }"), "unknown field action.stats.visual"],
        [Json.EmpowerAction(stats: "{ \"armor\": 1.5 }"), "unknown field action.stats.armor"],
        [Json.EmpowerAction(stats: "[1.5]"), "action.stats must be an object of physicalPower, spellPower, maxHealth, attackSpeed, moveSpeed"],
        ["\"action\": { \"type\": \"Empower\", \"factions\": [\"Faction_Bandits\"] }",
            "action.stats must be an object of physicalPower, spellPower, maxHealth, attackSpeed, moveSpeed"],
        ["\"action\": { \"type\": \"Empower\", \"stats\": { \"physicalPower\": 1.3 } }", "action.factions must be 1-5 distinct Faction_ names"],
        // keys
        [Json.EmpowerAction(extra: "\"visual\": \"aura\""), "unknown field action.visual"],
        [Json.EmpowerAction(extra: "\"units\": []"), "unknown field action.units"],
    ];

    [Theory]
    [MemberData(nameof(InvalidEmpower))]
    public void Invalid_Empower_actions_disable_the_event(string action, string reason)
    {
        var d = Json.One(Json.Empower("surge", action: action));
        Assert.False(d.Startable);
        Assert.Equal(reason, d.DisabledReason);
    }

    [Fact]
    public void Empower_needs_a_faction_catalog()
    {
        var units = new NoFactionUnits();
        var r = EventValidator.Parse(Json.File(Json.Empower()), units);
        Assert.Equal("unknown faction Faction_Bandits", Assert.Single(r.Set.All).DisabledReason);
        r = EventValidator.Parse(Json.File(Json.Empower()), units, FakeUnits.Default());
        Assert.Null(Assert.Single(r.Set.All).DisabledReason);
    }

    sealed class NoFactionUnits : IUnitCatalog
    {
        public bool IsKnown(string prefabName) => true;
        public bool IsDenied(string prefabName) => false;
    }

    [Fact]
    public void Pillar_and_action_are_paired()
    {
        var r = Load(Json.File(
            Json.Event("waves-under-empowerment").Replace("\"pillar\": \"spawns\"", "\"pillar\": \"empowerment\""),
            Json.Event("empower-under-spawns", action: Json.EmpowerAction()),
            Json.Event("empower-under-boss", action: Json.EmpowerAction()).Replace("\"pillar\": \"spawns\"", "\"pillar\": \"boss\""),
            Json.Empower("ok"),
            Json.Event("waves-ok")));
        Assert.Equal("pillar empowerment takes an Empower action", r.Set.Find("waves-under-empowerment")!.DisabledReason);
        Assert.Equal("action Empower needs pillar empowerment", r.Set.Find("empower-under-spawns")!.DisabledReason);
        Assert.Equal("action Empower needs pillar empowerment", r.Set.Find("empower-under-boss")!.DisabledReason);
        Assert.True(r.Set.Find("ok")!.Startable);
        Assert.True(r.Set.Find("waves-ok")!.Startable);
    }
}
