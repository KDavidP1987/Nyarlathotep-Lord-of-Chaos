using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D29: WaveGate.Decide gives each wave one outcome in one order: a control blocker, territory
/// unknown (when the wave needs the map), no eligible player or a failed player query, a claimed centre unless
/// allowTerritory, the chance roll, then the caps. allowTerritory lifts only the claimed-centre skip.</summary>
public class WavePrecedenceTests
{
    const string A = "CHAR_Bandit_Thug";

    sealed class Roll(int units)
    {
        public int Calls;
        public IReadOnlyList<string> Next() { Calls++; return Enumerable.Repeat(A, units).ToList(); }
    }

    static WaveFacts Facts(bool blocked = false, bool mapFailed = false, LocationType location = LocationType.Point, bool hunt = false,
        PickOutcome? pick = null, bool claimed = false, bool allow = false) =>
        new(3, "raid", blocked, mapFailed, location, hunt ? BehaviourType.Hunt : null, pick, claimed, allow);

    static (WaveDecision Decision, int Rolls) Decide(WaveFacts facts, int rolled = 5, int occupied = 0, int maxPerWave = 20, int maxTracked = 150)
    {
        var roll = new Roll(rolled);
        return (WaveGate.Decide(facts, roll.Next, maxPerWave, occupied, maxTracked), roll.Calls);
    }

    const string Unknown = "wave 3 of raid skipped: territory unknown";
    const string NoPlayer = "wave 3 of raid skipped: no eligible player";
    const string QueryFailed = "wave 3 of raid skipped: player query failed";
    const string Claimed = "wave 3 of raid skipped: centre in claimed territory";
    const string Zero = "wave 3 of raid: 0 units rolled";

    [Fact]
    public void WavePrecedence_fails_when_blocked_reaches_roll()
    {
        var (d, rolls) = Decide(Facts(blocked: true, mapFailed: true, location: LocationType.AroundPlayer, hunt: true, pick: PickOutcome.NoEligible, claimed: true));
        Assert.Equal(WaveOutcome.NoWave, d.Outcome);
        Assert.Null(d.Line);
        Assert.Empty(d.Units);
        Assert.Equal(0, rolls);
        var (plain, plainRolls) = Decide(Facts(blocked: true, mapFailed: true));
        Assert.True(Facts(mapFailed: true).NeedsMap);
        Assert.Equal((WaveOutcome.NoWave, (string?)null), (plain.Outcome, plain.Line));        // not "territory unknown"
        Assert.Equal(0, plainRolls);
    }

    [Fact]
    public void WavePrecedence_fails_when_player_skip_before_territory_unknown()
    {
        var (d, rolls) = Decide(Facts(mapFailed: true, location: LocationType.AroundPlayer, pick: PickOutcome.NoEligible, claimed: true));
        Assert.Equal((WaveOutcome.Skip, Unknown), (d.Outcome, d.Line));
        Assert.Equal(0, rolls);
    }

    [Fact]
    public void WavePrecedence_fails_when_claimed_before_player_skip()
    {
        var (d, _) = Decide(Facts(location: LocationType.AroundPlayer, pick: PickOutcome.NoEligible, claimed: true));
        Assert.Equal((WaveOutcome.Skip, NoPlayer), (d.Outcome, d.Line));
    }

    [Fact]
    public void WavePrecedence_fails_when_claimed_before_query_failed()
    {
        var (d, _) = Decide(Facts(location: LocationType.AroundPlayer, pick: PickOutcome.QueryFailed, claimed: true));
        Assert.Equal((WaveOutcome.Skip, QueryFailed), (d.Outcome, d.Line));
    }

    [Fact]
    public void WavePrecedence_fails_when_roll_before_claimed()
    {
        var (d, rolls) = Decide(Facts(claimed: true), rolled: 0);
        Assert.Equal((WaveOutcome.Skip, Claimed), (d.Outcome, d.Line));
        Assert.Equal(0, rolls);
    }

    [Fact]
    public void WavePrecedence_fails_when_caps_before_zero_roll()
    {
        var (d, rolls) = Decide(Facts(), rolled: 0, occupied: 150);
        Assert.Equal((WaveOutcome.ZeroRolled, Zero), (d.Outcome, d.Line));
        Assert.Empty(d.CapLines);
        Assert.Equal(1, rolls);
    }

    [Fact]
    public void WavePrecedence_fails_when_allow_territory_lifts_a_cap()
    {
        var (d, _) = Decide(Facts(claimed: true, allow: true), rolled: 30);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal(20, d.Units.Sum(u => u.Count));
        Assert.Equal(["clamped by MaxUnitsPerWave: 30 -> 20"], d.CapLines);
        var (full, _) = Decide(Facts(allow: true), rolled: 5, occupied: 148);
        Assert.Equal(2, full.Units.Sum(u => u.Count));
        Assert.Equal(["skipped by MaxTrackedUnits: 3 of 5"], full.CapLines);
    }

    [Theory]
    [InlineData(PickOutcome.NoEligible, NoPlayer)]
    [InlineData(PickOutcome.QueryFailed, QueryFailed)]
    public void WavePrecedence_fails_when_allow_territory_lifts_player_skip(PickOutcome pick, string line)
    {
        var (d, rolls) = Decide(Facts(location: LocationType.AroundPlayer, pick: pick, allow: true));
        Assert.Equal((WaveOutcome.Skip, line), (d.Outcome, d.Line));
        Assert.Equal(0, rolls);
    }

    [Fact]
    public void WavePrecedence_fails_when_allow_territory_lifts_unknown_on_aroundplayer()
    {
        var (d, rolls) = Decide(Facts(mapFailed: true, location: LocationType.AroundPlayer, pick: PickOutcome.Picked, allow: true));
        Assert.Equal((WaveOutcome.Skip, Unknown), (d.Outcome, d.Line));
        Assert.Equal(0, rolls);
    }

    [Theory]
    [InlineData(LocationType.Point)]
    [InlineData(LocationType.Admin)]
    [InlineData(LocationType.AroundPlayer)]
    public void WavePrecedence_fails_when_allow_territory_lifts_unknown_on_hunt(LocationType location)
    {
        var (d, rolls) = Decide(Facts(mapFailed: true, location: location, hunt: true, pick: PickOutcome.Picked, allow: true));
        Assert.Equal((WaveOutcome.Skip, Unknown), (d.Outcome, d.Line));
        Assert.Equal(0, rolls);
    }

    [Theory]
    [InlineData(LocationType.Point)]
    [InlineData(LocationType.Admin)]
    public void WavePrecedence_fails_when_point_with_allow_territory_and_failed_map_skipped(LocationType location)
    {
        var facts = Facts(mapFailed: true, location: location, claimed: true, allow: true);
        Assert.False(facts.NeedsMap);
        var (d, rolls) = Decide(facts);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Null(d.Line);
        Assert.Equal(5, d.Units.Sum(u => u.Count));
        Assert.Equal(1, rolls);
    }

    public static TheoryData<string> Skips => new() { "blocked", "territory unknown", "no eligible player", "player query failed", "centre claimed" };

    [Theory]
    [MemberData(nameof(Skips))]
    public void WavePrecedence_fails_when_skipped_wave_rolls(string skip)
    {
        var facts = skip switch
        {
            "blocked" => Facts(blocked: true),
            "territory unknown" => Facts(mapFailed: true),
            "no eligible player" => Facts(location: LocationType.AroundPlayer, pick: PickOutcome.NoEligible),
            "player query failed" => Facts(location: LocationType.AroundPlayer, pick: PickOutcome.QueryFailed),
            _ => Facts(claimed: true),
        };
        var (d, rolls) = Decide(facts);
        Assert.NotEqual(WaveOutcome.Spawn, d.Outcome);
        Assert.Empty(d.Units);
        Assert.Empty(d.CapLines);
        Assert.Equal(0, rolls);
    }

    [Fact]
    public void WavePrecedence_passes_stated_order()
    {
        // every rule true at once, then lifted one at a time: each step shows the next rule's outcome
        var f = Facts(blocked: true, mapFailed: true, location: LocationType.AroundPlayer, pick: PickOutcome.NoEligible, claimed: true);
        var steps = new List<(WaveOutcome, string?)>();
        void Step(WaveFacts facts, int rolled) { var d = Decide(facts, rolled).Decision; steps.Add((d.Outcome, d.Line ?? d.CapLines.SingleOrDefault())); }
        Step(f, 0);
        Step(f = f with { Blocked = false }, 0);
        Step(f = f with { MapFailed = false }, 0);
        Step(f = f with { Pick = PickOutcome.Picked }, 0);
        Step(f = f with { CentreClaimed = false }, 0);
        Step(f, 25);
        Assert.Equal(
        [
            (WaveOutcome.NoWave, null), (WaveOutcome.Skip, Unknown), (WaveOutcome.Skip, NoPlayer), (WaveOutcome.Skip, Claimed),
            (WaveOutcome.ZeroRolled, Zero), (WaveOutcome.Spawn, "clamped by MaxUnitsPerWave: 25 -> 20"),
        ], steps);
    }

    [Fact]
    public void WavePrecedence_passes_allow_territory_lifts_only_territory_skip()
    {
        var (d, rolls) = Decide(Facts(claimed: true, allow: true));
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal(1, rolls);
        var (around, _) = Decide(Facts(location: LocationType.AroundPlayer, pick: PickOutcome.Picked, claimed: true, allow: true));
        Assert.Equal(WaveOutcome.Spawn, around.Outcome);
        var (zero, _) = Decide(Facts(claimed: true, allow: true), rolled: 0);
        Assert.Equal((WaveOutcome.ZeroRolled, Zero), (zero.Outcome, zero.Line));
    }

    [Fact]
    public void WavePrecedence_empty_nothing_blocked_roll_zero()
    {
        var (d, rolls) = Decide(Facts(), rolled: 0);
        Assert.Equal((WaveOutcome.ZeroRolled, Zero), (d.Outcome, d.Line));
        Assert.Empty(d.Units);
        Assert.Empty(d.CapLines);
        Assert.Equal(1, rolls);
    }
}
