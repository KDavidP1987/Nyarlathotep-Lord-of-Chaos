using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D3: who is empowered, decided per unit from its facts; Ownership fails closed.</summary>
public class EmpowerEligibilityTests
{
    static readonly EmpowerAction Bandits = new(["Faction_Bandits"], [], [], false, new EmpowerStats(PhysicalPower: 1.3));

    static readonly UnitFacts Thug = new("CHAR_Bandit_Thug", "Faction_Bandits", IsPrefab: false, IsDead: false,
        HasVBloodUnit: false, IsOurs: false, OwnedByPlayer: false, CarrierOf: null);

    [Fact]
    public void A_plain_faction_unit_is_applied() => Assert.Equal(Eligible.Yes, Eligibility.Decide(Thug, Bandits));

    public static TheoryData<string, UnitFacts> Flipped() => new()
    {
        { "prefab", Thug with { IsPrefab = true } },
        { "dead", Thug with { IsDead = true } },
        { "ours", Thug with { IsOurs = true } },
        { "owned", Thug with { OwnedByPlayer = true } },
        { "other", Thug with { Faction = "Faction_Legion" } },
        { "vblood", Thug with { HasVBloodUnit = true } },
        { "carried", Thug with { CarrierOf = "other-event" } },
        { "denied", Thug with { Faction = "Faction_Players" } },
    };

    [Theory]
    [MemberData(nameof(Flipped))]
    public void Each_fact_flipped_from_the_apply_case_skips(string reason, UnitFacts facts) =>
        Assert.Equal(Eligible.No(reason), Eligibility.Decide(facts, Bandits));

    [Fact]
    public void Every_skip_reason_has_a_case() =>
        Assert.Equal(Eligibility.SkipReasons.OrderBy(x => x), Flipped().Select(r => (string)r[0]).Append("excluded").OrderBy(x => x));

    [Fact]
    public void Include_and_exclude_lists()
    {
        var a = Bandits with { IncludeUnits = ["CHAR_Undead_SkeletonSoldier_Base"], ExcludeUnits = ["CHAR_Bandit_Deadeye"] };
        var skeleton = Thug with { Prefab = "CHAR_Undead_SkeletonSoldier_Base", Faction = "Faction_Undead" };
        Assert.Equal(Eligible.Yes, Eligibility.Decide(skeleton, a));                                     // another faction, named
        Assert.Equal(Eligible.No("excluded"), Eligibility.Decide(Thug with { Prefab = "CHAR_Bandit_Deadeye" }, a));
        Assert.Equal(Eligible.No("other"), Eligibility.Decide(skeleton with { Prefab = "CHAR_Undead_Other" }, a));
    }

    [Fact]
    public void V_Bloods_only_with_includeVBloods() =>
        Assert.Equal(Eligible.Yes, Eligibility.Decide(Thug with { HasVBloodUnit = true }, Bandits with { IncludeVBloods = true }));

    [Theory]
    [InlineData("Faction_Players")]
    [InlineData("Faction_Players_Castle_Prisoners")]
    [InlineData("Faction_Players_Mutant")]
    [InlineData("Faction_Players_Shapeshift_Human")]
    [InlineData("Faction_Traders_T01")]
    [InlineData("Faction_Traders_T02")]
    [InlineData("Faction_Critters")]
    [InlineData("Faction_World_Prisoners")]
    [InlineData("Faction_Ignored")]
    public void Deny_listed_factions_are_denied_even_when_named_in_includeUnits(string faction)
    {
        var unit = Thug with { Prefab = "CHAR_Named", Faction = faction };
        Assert.Equal(Eligible.No("denied"), Eligibility.Decide(unit, Bandits with { IncludeUnits = ["CHAR_Named"] }));
        Assert.Equal(Eligible.No("denied"), Eligibility.Decide(unit, Bandits with { Factions = [faction] }));
        Assert.True(FactionDenyList.IsDenied(faction));
    }

    [Fact]
    public void A_derived_player_faction_is_refused_by_validation()
    {
        var d = Json.One(Json.Empower(action: Json.EmpowerAction("[\"Faction_Players_Mutant\"]")));
        Assert.Equal("faction Faction_Players_Mutant is deny-listed", d.DisabledReason);
    }

    [Theory]
    [InlineData("Faction_Bandits")]
    [InlineData("Faction_Legion")]
    [InlineData("Faction_ChurchOfLum")]
    public void Combat_factions_are_not_denied(string faction) => Assert.False(FactionDenyList.IsDenied(faction));

    [Fact]
    public void Ownership_is_owned_when_any_link_leads_to_a_player_or_cannot_be_resolved()
    {
        var none = new OwnershipFacts(OwnerLink.Absent, OwnerLink.Absent, OwnerLink.NotPlayer);
        Assert.False(Ownership.Decide(none));
        Assert.False(Ownership.Decide(none with { Follower = OwnerLink.NotPlayer, EntityOwner = OwnerLink.NotPlayer }));
        foreach (var link in new[] { OwnerLink.Player, OwnerLink.Unresolvable })
        {
            Assert.True(Ownership.Decide(none with { Follower = link }));
            Assert.True(Ownership.Decide(none with { EntityOwner = link }));
            Assert.True(Ownership.Decide(none with { Team = link }));
        }
    }
}
