using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation's planners: D2 IntervalClock, D5 FanOutPick, D6 FanOutCaps, D9 RegionEntries, D11 KillWindows,
/// D13 Focus, D14 TriggerRules, D29 Phantoms and D31 StateNextInterval.</summary>
public class AutomationTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 9, 29, 20, 0);

    static DefinitionSet Set(params string[] events) =>
        EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()).Set;

    const string Interval = "{ \"type\": \"Interval\", \"minMinutes\": 5, \"maxMinutes\": 6 }";

    static string Disabled(string eventJson) => eventJson.Replace("\"enabled\": true", "\"enabled\": false");

    // ---- D2 IntervalClock

    [Fact]
    public void IntervalClock_fails_when_active_keeps_next()
    {
        var step = IntervalClock.Poll(5, 6, T0.AddMinutes(3), active: true, T0, new ScriptedRandom());
        Assert.Equal(new IntervalClock.Step(false, null), step);
        Assert.Equal(new IntervalClock.Step(false, null), IntervalClock.Poll(5, 6, T0.AddMinutes(-1), active: true, T0, new ScriptedRandom()));
    }

    [Fact]
    public void IntervalClock_fails_when_absent_next_fires()
    {
        var step = IntervalClock.Poll(5, 6, null, active: false, T0, new ScriptedRandom(0.0));
        Assert.False(step.Fire);
        Assert.Equal(T0.AddMinutes(5), step.Next);
    }

    [Fact]
    public void IntervalClock_fails_when_due_fires_twice()
    {
        var due = IntervalClock.Poll(5, 6, T0, active: false, T0, new ScriptedRandom());
        Assert.Equal(new IntervalClock.Step(true, null), due);
        var after = IntervalClock.Poll(5, 6, due.Next, active: false, T0, new ScriptedRandom(0.5));     // the next poll draws, never fires
        Assert.False(after.Fire);
        Assert.NotNull(after.Next);
    }

    [Fact]
    public void IntervalClock_fails_when_next_beyond_max_kept()
    {
        var shortened = IntervalClock.Poll(5, 6, T0.AddMinutes(90), active: false, T0, new ScriptedRandom(0.0));
        Assert.Equal(T0.AddMinutes(5), shortened.Next);
        var inRange = IntervalClock.Poll(5, 6, T0.AddMinutes(4), active: false, T0, new ScriptedRandom());       // kept, no draw (7.3)
        Assert.Equal(T0.AddMinutes(4), inRange.Next);
    }

    [Fact]
    public void IntervalClock_fails_when_load_keeps_removed_or_non_interval()
    {
        var set = Set(Json.Event("tick", Interval), Json.Event("manual"), Disabled(Json.Event("off", Interval)));
        var stored = new Dictionary<string, DateTime> { ["tick"] = T0.AddMinutes(3), ["manual"] = T0.AddMinutes(3), ["off"] = T0.AddMinutes(3), ["gone"] = T0.AddMinutes(3) };
        Assert.Equal(["tick"], IntervalClock.Load(stored, set, T0, new ScriptedRandom()).Keys);
    }

    [Fact]
    public void IntervalClock_fails_when_load_keeps_next_at_or_before_boot()
    {
        var set = Set(Json.Event("tick", Interval), Json.Event("tock", Interval));
        var stored = new Dictionary<string, DateTime> { ["tick"] = T0, ["tock"] = T0.AddHours(-5) };
        var loaded = IntervalClock.Load(stored, set, T0, new ScriptedRandom(0.0, 0.999999));
        Assert.Equal(T0.AddMinutes(5), loaded["tick"]);                  // redrawn from the boot, never replayed
        Assert.Equal(T0.AddMinutes(6), loaded["tock"]);
    }

    [Fact]
    public void IntervalClock_passes_draw_within_range()
    {
        foreach (var r in new[] { 0.0, 0.25, 0.5, 0.999999 })
        {
            var next = IntervalClock.Draw(5, 6, T0, new ScriptedRandom(r));
            Assert.InRange(next, T0.AddMinutes(5), T0.AddMinutes(6));
            Assert.Equal(0, next.Millisecond);                           // whole seconds
        }
        Assert.Equal(T0.AddMinutes(30), IntervalClock.Draw(30, 30, T0, new ScriptedRandom(0.7)));     // a fixed period
    }

    [Fact]
    public void IntervalClock_passes_tick_prunes_and_fires()
    {
        var set = Set(Json.Event("due", Interval), Json.Event("wait", Interval), Json.Event("running", Interval));
        var nexts = new Dictionary<string, DateTime> { ["due"] = T0.AddSeconds(-1), ["wait"] = T0.AddMinutes(2), ["running"] = T0.AddMinutes(2), ["removed"] = T0 };
        var (due, changed) = IntervalClock.PollAll(set, nexts, id => id == "running", T0, new ScriptedRandom());
        Assert.Equal(["due"], due.Select(d => d.Id));
        Assert.True(changed);
        Assert.Equal(["wait"], nexts.Keys);                              // fired, active and removed ids hold no next
        var (again, unchanged) = IntervalClock.PollAll(set, nexts, id => id is "running" or "due", T0, new ScriptedRandom());
        Assert.Empty(again);
        Assert.False(unchanged);
    }

    [Fact]
    public void IntervalClock_empty_no_definition()
    {
        var nexts = new Dictionary<string, DateTime>();
        var (due, changed) = IntervalClock.PollAll(Set(Json.Event("manual")), nexts, _ => false, T0, new ScriptedRandom());
        Assert.Empty(due);
        Assert.False(changed);
        Assert.Empty(nexts);
        Assert.Empty(IntervalClock.Load(null, Set(Json.Event("tick", Interval)), T0, new ScriptedRandom()));
    }

    // ---- D5 FanOutPick

    static PickCandidate P(float x, string id = "", float z = 0, bool online = true, bool alive = true, bool pvp = false) =>
        new(x, 0, z, online, alive, pvp, id);

    static readonly Func<float, float, bool> Unclaimed = (_, _) => false;

    /// <summary>The player each centre belongs to: every centre lies exactly 20 m from its player (minDist = maxDist = 20).</summary>
    static List<PickCandidate> Owners(IReadOnlyList<PickCandidate> players, FanOutPick pick) =>
        pick.Centres.Select(c => players.Single(p => Math.Abs(Math.Sqrt((c.X - p.X) * (c.X - p.X) + (c.Z - p.Z) * (c.Z - p.Z)) - 20) < 0.01)).ToList();

    static ScriptedRandom Draws(int n, double value = 0.0) => new(Enumerable.Repeat(value, n).ToArray());

    [Fact]
    public void FanOutPick_fails_when_picks_closer_than_spacing()
    {
        PickCandidate[] players = [P(0, "a"), P(100, "b"), P(1000, "c"), P(1120, "d"), P(3000, "e")];
        var pick = PlayerPick.ChooseMany(players, Draws(30), 20, 20, Unclaimed, null, 5, 150, null);
        var owners = Owners(players, pick);
        Assert.Equal(3, owners.Count);                                    // a, c, e: b and d stand within 150 m of one picked
        foreach (var a in owners)
            foreach (var b in owners.Where(o => o != a))
                Assert.True(Math.Abs(a.X - b.X) >= 150);
    }

    [Fact]
    public void FanOutPick_fails_when_ineligible_or_twice()
    {
        PickCandidate[] players = [P(0, "off", online: false), P(1000, "dead", alive: false), P(2000, "pvp", pvp: true), P(float.NaN, "nan"),
            P(4000, "claimed"), P(6000, "ok")];
        var pick = PlayerPick.ChooseMany(players, Draws(30), 20, 20, (x, _) => x is > 3900 and < 4100, null, 5, 50, null);
        Assert.Equal(["ok"], Owners(players, pick).Select(p => p.PlatformId));
        var two = PlayerPick.ChooseMany([P(0, "a"), P(1000, "b")], Draws(30), 20, 20, Unclaimed, null, 5, 50, null);
        Assert.Equal(2, Owners([P(0, "a"), P(1000, "b")], two).Distinct().Count());
    }

    [Fact]
    public void FanOutPick_fails_when_more_than_max()
    {
        var players = Enumerable.Range(0, 8).Select(i => P(i * 500f, $"p{i}")).ToList();
        var pick = PlayerPick.ChooseMany(players, Draws(40), 20, 20, Unclaimed, null, 3, 150, null);
        Assert.Equal(3, pick.Centres.Count);
        Assert.Equal(PickOutcome.Picked, pick.Outcome);
    }

    [Fact]
    public void FanOutPick_fails_when_focus_not_first()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b"), P(2000, "focus")];
        var pick = PlayerPick.ChooseMany(players, Draws(20), 20, 20, Unclaimed, null, 3, 150, "focus");
        Assert.Equal("focus", Owners(players, pick)[0].PlatformId);
        Assert.Equal(3, pick.Centres.Count);
    }

    [Fact]
    public void FanOutPick_passes_one_centre_equals_choose()
    {
        PickCandidate[] players = [P(0, "a"), P(700, "b", 300), P(-900, "c", -40)];
        double[] draws = [0.62, 0.31, 0.77];
        var one = new ScriptedRandom(draws);
        var many = new ScriptedRandom(draws);
        var choose = PlayerPick.Choose(players, one, 20, 40, Unclaimed, null);
        var pick = PlayerPick.ChooseMany(players, many, 20, 40, Unclaimed, null, 1, 150, null);
        Assert.Equal(choose.Centre, Assert.Single(pick.Centres));
        Assert.Equal(one.Calls, many.Calls);                             // the same draws, consumed alike
    }

    [Fact]
    public void FanOutPick_passes_spaced_players_in_scope_only()
    {
        PickCandidate[] players = [P(-500, "out"), P(500, "in1"), P(1500, "in2")];
        var pick = PlayerPick.ChooseMany(players, Draws(30), 20, 20, Unclaimed, (x, _) => x >= 0, 5, 150, null);
        Assert.Equal(["in1", "in2"], Owners(players, pick).Select(p => p.PlatformId).OrderBy(x => x));
        Assert.All(pick.Centres, c => Assert.True(c.X >= 0));
    }

    [Fact]
    public void FanOutPick_empty_no_players()
    {
        var pick = PlayerPick.ChooseMany([], new ScriptedRandom(), 20, 40, Unclaimed, null, 3, 150, null);
        Assert.Equal(PickOutcome.NoEligible, pick.Outcome);
        Assert.Empty(pick.Centres);
        var failed = PlayerPick.ChooseMany([P(0)], new ScriptedRandom(), 20, 40, (_, _) => throw new InvalidOperationException("map"), null, 3, 150, null);
        Assert.Equal((PickOutcome.QueryFailed, "map"), (failed.Outcome, failed.Error));
    }

    // ---- D6 FanOutCaps

    static WaveFacts Facts(bool allowTerritory = false) =>
        new(2, "hunt", false, Location: LocationType.AroundPlayer, Behaviour: BehaviourType.Hunt, Pick: PickOutcome.Picked, AllowTerritory: allowTerritory);

    static GroupCentre G(float x, bool claimed = false) => new(x, 0, 0, claimed);

    static Func<IReadOnlyList<string>> Rolls(params int[] counts)
    {
        var i = 0;
        return () => Enumerable.Range(0, counts[i++]).Select(k => k % 2 == 0 ? "CHAR_Bandit_Thug" : "CHAR_Bandit_Deadeye").ToList();
    }

    static int Total(FanOutDecision d) => d.Groups.Sum(g => g.Units.Sum(u => u.Count));

    [Fact]
    public void FanOutCaps_fails_when_groups_exceed_caps()
    {
        var five = Enumerable.Range(0, 5).Select(i => G(i * 200f)).ToList();
        var d = WaveGate.DecideGroups(Facts(), five, Rolls(8, 8, 8, 8, 8), 20, 0, 150);
        Assert.Equal(20, Total(d));
        Assert.All(d.Groups, g => Assert.Equal(4, g.Units.Sum(u => u.Count)));                  // 5 groups of 8 under 20: 4 each
        Assert.Contains("clamped by MaxUnitsPerWave: 40 -> 20", string.Join("\n", d.CapLines));
        var full = WaveGate.DecideGroups(Facts(), five, Rolls(8, 8, 8, 8, 8), 20, 143, 150);   // 7 free slots
        Assert.Equal(7, Total(full));
        Assert.Equal(2, full.CapLines.Count);
    }

    [Fact]
    public void FanOutCaps_fails_when_deal_not_round_robin()
    {
        var d = WaveGate.DecideGroups(Facts(), [G(0), G(200), G(400)], Rolls(1, 5, 5), 7, 0, 150);
        Assert.Equal([1, 3, 3], d.Groups.Select(g => g.Units.Sum(u => u.Count)));
        Assert.Equal([0, 1, 2], d.Groups.Select(g => g.Index));
        var tight = WaveGate.DecideGroups(Facts(), [G(0), G(200), G(400)], Rolls(2, 2, 2), 2, 0, 150);
        Assert.Equal([0, 1], tight.Groups.Select(g => g.Index));        // pick order first; a group dealt 0 is dropped
    }

    [Fact]
    public void FanOutCaps_fails_when_claimed_skips_another()
    {
        var d = WaveGate.DecideGroups(Facts(), [G(0), G(200, claimed: true), G(400)], Rolls(3, 3), 20, 0, 150);
        Assert.Equal(WaveOutcome.Spawn, d.Outcome);
        Assert.Equal([0, 2], d.Groups.Select(g => g.Index));
        var allowed = WaveGate.DecideGroups(Facts(allowTerritory: true), [G(0), G(200, claimed: true), G(400)], Rolls(3, 3, 3), 20, 0, 150);
        Assert.Equal([0, 1, 2], allowed.Groups.Select(g => g.Index));
    }

    [Fact]
    public void FanOutCaps_fails_when_no_group_spawns()
    {
        var zero = WaveGate.DecideGroups(Facts(), [G(0), G(200)], Rolls(0, 0), 20, 0, 150);
        Assert.Equal((WaveOutcome.ZeroRolled, "wave 2 of hunt: 0 units rolled"), (zero.Outcome, zero.Line));
        var noSlot = WaveGate.DecideGroups(Facts(), [G(0), G(200)], Rolls(3, 3), 20, 150, 150);
        Assert.Equal((WaveOutcome.Skip, "wave 2 of hunt skipped: no free unit slot"), (noSlot.Outcome, noSlot.Line));   // A3
        Assert.Empty(noSlot.Groups);
        // a picked wave with no centre spawns nothing, never a group at the origin (step 1 code review F8)
        var none = WaveGate.DecideGroups(Facts(), [], Rolls(3), 20, 0, 150);
        Assert.Equal((WaveOutcome.Skip, "wave 2 of hunt skipped: no eligible player"), (none.Outcome, none.Line));
        Assert.Empty(none.Groups);
    }

    [Fact]
    public void FanOutCaps_fails_when_skip_reason_not_first_group()
    {
        var claimedFirst = WaveGate.DecideGroups(Facts(), [G(0, claimed: true), G(200)], Rolls(0), 20, 0, 150);
        Assert.Equal((WaveOutcome.Skip, "wave 2 of hunt skipped: centre in claimed territory"), (claimedFirst.Outcome, claimedFirst.Line));
        var zeroFirst = WaveGate.DecideGroups(Facts(), [G(0), G(200, claimed: true)], Rolls(0), 20, 0, 150);
        Assert.Equal((WaveOutcome.Skip, "wave 2 of hunt skipped: 0 units rolled"), (zeroFirst.Outcome, zeroFirst.Line));   // D6's form
    }

    [Fact]
    public void FanOutCaps_passes_one_centre_equals_decide()
    {
        foreach (var (facts, centre, count) in new[] { (Facts(), G(0), 5), (Facts(), G(0, claimed: true), 5), (Facts(), G(0), 0),
                     (Facts() with { Pick = PickOutcome.NoEligible }, G(0), 5), (Facts() with { MapFailed = true }, G(0), 5), (Facts() with { Blocked = true }, G(0), 5) })
        {
            var one = WaveGate.Decide(facts with { CentreClaimed = centre.Claimed }, Rolls(count), 3, 0, 150);
            var groups = WaveGate.DecideGroups(facts, [centre], Rolls(count), 3, 0, 150);
            Assert.Equal((one.Outcome, one.Line), (groups.Outcome, groups.Line));
            Assert.Equal(one.CapLines, groups.CapLines);
            Assert.Equal(one.Units, groups.Groups.SelectMany(g => g.Units));
        }
    }

    [Fact]
    public void FanOutCaps_passes_line_counts_groups_only()
    {
        Assert.Equal("around 3 players", WaveLines.AroundPlayers(3));
        Assert.Equal(WaveLines.AroundAPlayer, WaveLines.AroundPlayers(1));
        var d = WaveGate.DecideGroups(Facts(), [G(1234.5f), G(-4321.5f)], Rolls(2, 2), 20, 0, 150);
        Assert.All(d.CapLines.Append(d.Line ?? ""), l => Assert.DoesNotContain("1234", l));
    }

    [Fact]
    public void FanOutCaps_empty_no_eligible_player()
    {
        var d = WaveGate.DecideGroups(Facts() with { Pick = PickOutcome.NoEligible }, [], Rolls(), 20, 0, 150);
        Assert.Equal((WaveOutcome.Skip, "wave 2 of hunt skipped: no eligible player"), (d.Outcome, d.Line));
        Assert.Empty(d.Groups);
    }

    // ---- D9 RegionEntries (FakeRegions: x < 0 FarbaneWoods, x >= 0 CursedForest)

    static readonly Func<float, float, string> RegionOf = new FakeRegions().RegionOf;

    const string Border = "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": 5 }";

    static List<EventDefinition> Entered(params string[] triggers) =>
        TriggerRouter.Candidates(Set(triggers.Select((t, i) => Json.Event($"e{i}", t)).ToArray()), TriggerType.RegionEntered).ToList();

    static ScanRow At(float x, string id = "p1", bool alive = true) => new(id, x, 0, alive);

    [Fact]
    public void RegionEntries_fails_when_first_sighting_counts()
    {
        var entries = new RegionEntries();
        Assert.Empty(entries.Scan([At(10)], RegionOf, Entered(Border), T0));                  // a login inside the region
        Assert.Empty(entries.Scan([At(20)], RegionOf, Entered(Border), T0.AddSeconds(5)));
    }

    [Fact]
    public void RegionEntries_fails_when_move_within_or_out_of_scope_counts()
    {
        var both = "{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\", \"FarbaneWoods\"] }";
        var entries = new RegionEntries();
        entries.Scan([At(-10)], RegionOf, Entered(both), T0);
        Assert.Empty(entries.Scan([At(10)], RegionOf, Entered(both), T0.AddSeconds(5)));      // between two in-scope regions
        var outward = new RegionEntries();
        outward.Scan([At(10)], RegionOf, Entered(Border), T0);
        Assert.Empty(outward.Scan([At(-10)], RegionOf, Entered(Border), T0.AddSeconds(5)));   // out of the scope
    }

    [Fact]
    public void RegionEntries_fails_when_dead_player_enters()
    {
        var entries = new RegionEntries();
        entries.Scan([At(-10)], RegionOf, Entered(Border), T0);
        Assert.Empty(entries.Scan([At(10, alive: false)], RegionOf, Entered(Border), T0.AddSeconds(5)));
        Assert.Empty(entries.Scan([At(10)], RegionOf, Entered(Border), T0.AddSeconds(10)));   // the dead move updated the row
    }

    [Fact]
    public void RegionEntries_fails_when_inside_cooldown()
    {
        var entries = new RegionEntries();
        var defs = Entered(Border);
        entries.Scan([At(-10)], RegionOf, defs, T0);
        var first = Assert.Single(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(5)));
        entries.Attempted(first.Definition, "p1", T0.AddSeconds(5));
        entries.Scan([At(-10)], RegionOf, defs, T0.AddSeconds(10));
        Assert.Empty(entries.Scan([At(10)], RegionOf, defs, T0.AddMinutes(4)));               // within 5 min
        entries.Scan([At(-10)], RegionOf, defs, T0.AddMinutes(5));
        Assert.Single(entries.Scan([At(10)], RegionOf, defs, T0.AddMinutes(5).AddSeconds(10)));
    }

    [Fact]
    public void RegionEntries_fails_when_refused_entry_skips_cooldown()
    {
        // Attempted is called for every entry that reached a start attempt, refused or not; an entry the gate dropped for an
        // active event never reaches it and keeps no cooldown
        var entries = new RegionEntries();
        var defs = Entered(Border);
        entries.Scan([At(-10)], RegionOf, defs, T0);
        entries.Attempted(Assert.Single(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(5))).Definition, "p1", T0.AddSeconds(5));
        Assert.Equal(1, entries.CooldownRows);
        entries.Scan([At(-10)], RegionOf, defs, T0.AddSeconds(10));
        Assert.Empty(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(15)));
        // a disable, reload or regions unavailable drops the definition from a scan but keeps the cooldown (round 2 F2)
        entries.Scan([At(-10)], RegionOf, [], T0.AddSeconds(20));
        Assert.Equal(1, entries.CooldownRows);
        Assert.Empty(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(25)));
    }

    [Fact]
    public void RegionEntries_fails_when_relog_resets_cooldown()
    {
        var entries = new RegionEntries();
        var defs = Entered(Border);
        entries.Scan([At(-10)], RegionOf, defs, T0);
        entries.Attempted(Assert.Single(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(5))).Definition, "p1", T0.AddSeconds(5));
        entries.Scan([], RegionOf, defs, T0.AddSeconds(10));                                   // logged out
        entries.Scan([At(-10)], RegionOf, defs, T0.AddSeconds(15));                            // back, outside
        Assert.Empty(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(20)));               // same platform id: still cooling down
    }

    [Fact]
    public void RegionEntries_fails_when_rows_exceed_bound()
    {
        var entries = new RegionEntries();
        var def = Entered("{ \"type\": \"RegionEntered\", \"scope\": [\"CursedForest\"], \"playerCooldownMinutes\": 1440 }").Single();
        for (var i = 0; i < TriggerLimits.CooldownRows + 5; i++) entries.Attempted(def, $"p{i}", T0.AddSeconds(i));
        Assert.Equal(TriggerLimits.CooldownRows, entries.CooldownRows);
    }

    [Fact]
    public void RegionEntries_fails_when_absent_keeps_row()
    {
        var entries = new RegionEntries();
        entries.Scan([At(-10, "a"), At(-10, "b")], RegionOf, Entered(Border), T0);
        Assert.Equal(2, entries.RegionRows);
        entries.Scan([At(-10, "a")], RegionOf, Entered(Border), T0.AddSeconds(5));
        Assert.Equal(1, entries.RegionRows);
        entries.Scan([At(float.NaN, "a")], RegionOf, Entered(Border), T0.AddSeconds(10));     // an unusable position gives no region
        Assert.Equal(0, entries.RegionRows);
    }

    [Fact]
    public void RegionEntries_passes_gap_to_scope_is_entry()
    {
        var entries = new RegionEntries();
        Func<float, float, string> withGap = (x, z) => x > 1000 ? RegionNames.None : RegionOf(x, z);
        entries.Scan([At(2000)], withGap, Entered(Border), T0);                                // an unmapped gap is a known region
        Assert.Single(entries.Scan([At(10)], withGap, Entered(Border), T0.AddSeconds(5)));
    }

    [Fact]
    public void RegionEntries_passes_respawn_into_scope_is_entry()
    {
        var entries = new RegionEntries();
        entries.Scan([At(-10)], RegionOf, Entered(Border), T0);
        entries.Scan([At(-12, alive: false)], RegionOf, Entered(Border), T0.AddSeconds(5));   // died outside
        Assert.Single(entries.Scan([At(300)], RegionOf, Entered(Border), T0.AddSeconds(10))); // respawned at a coffin inside
    }

    [Fact]
    public void RegionEntries_empty_no_players()
    {
        var entries = new RegionEntries();
        Assert.Empty(entries.Scan([], RegionOf, Entered(Border), T0));
        Assert.Empty(entries.Scan([], RegionOf, [], T0));
        Assert.Equal((0, 0), (entries.RegionRows, entries.CooldownRows));
    }

    // ---- D11 KillWindows

    const string Reprisal = "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }";

    static EventDefinition KillDef(string trigger = Reprisal, string id = "reprisal") => Set(Json.Event(id, trigger)).Find(id)!;

    static KillFacts Kill(string? killer = "p1", string? owner = null, string faction = "Faction_Bandits", bool player = false, bool minion = false,
        bool ours = false, float? x = null) => new(killer, owner, player, minion, ours, faction, x, x is null ? null : 0);

    [Fact]
    public void KillWindows_fails_when_non_player_or_servant_kill_counts()
    {
        var def = KillDef();
        Assert.False(KillRule.Counts(Kill(killer: null), def, null));                          // an environment death
        Assert.False(KillRule.Counts(Kill(killer: null, owner: null), def, null));             // a castle servant or structure: no player owner
        Assert.False(KillRule.Counts(Kill(player: true), def, null));                          // a player victim, the killer included
        Assert.False(KillRule.Counts(Kill(owner: "p1", killer: null) with { VictimIsKiller = true }, def, null));   // an owned unit killing itself
    }

    [Fact]
    public void KillWindows_fails_when_our_unit_or_minion_counts()
    {
        var def = KillDef();
        Assert.False(KillRule.Counts(Kill(ours: true), def, null));
        Assert.False(KillRule.Counts(Kill(minion: true), def, null));
    }

    [Fact]
    public void KillWindows_fails_when_other_faction_or_out_of_scope()
    {
        Assert.False(KillRule.Counts(Kill(faction: "Faction_Legion"), KillDef(), null));
        Assert.False(KillRule.Counts(Kill(faction: null!), KillDef(), null));                  // a faction guid with no name
        var scoped = KillDef("{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60, \"scope\": [\"CursedForest\"] }");
        Assert.False(KillRule.Counts(Kill(x: -50), scoped, RegionOf));
        Assert.False(KillRule.Counts(Kill(), scoped, RegionOf));                               // an unknown position counts nothing there
        Assert.True(KillRule.Counts(Kill(x: 50), scoped, RegionOf));
        Assert.False(KillRule.Counts(Kill(), KillDef(Interval, "tick"), null));      // not a FactionKills trigger
    }

    [Fact]
    public void KillWindows_fails_when_old_kills_count()
    {
        var windows = new KillWindows();
        var def = KillDef();
        Assert.False(windows.Add(def, "p1", T0));
        Assert.False(windows.Add(def, "p1", T0.AddSeconds(30)));
        Assert.False(windows.Add(def, "p1", T0.AddSeconds(61)));                                // the first kill left the window
        Assert.True(windows.Add(def, "p1", T0.AddSeconds(62)));
        // a counter whose kills all left the window is dropped by the scan's prune, not only by a later kill
        Assert.False(windows.Add(def, "p2", T0));
        windows.Prune([def], T0.AddSeconds(60));
        Assert.Equal(1, windows.CountersFor(def.Id));
        windows.Prune([def], T0.AddSeconds(61));
        Assert.Equal(0, windows.Counters);
        Assert.False(windows.Add(def, "p3", T0));
        windows.Prune([], T0);                                                                  // the definition left the set
        Assert.Equal(0, windows.Counters);
    }

    [Fact]
    public void KillWindows_fails_when_fires_twice()
    {
        var windows = new KillWindows();
        var def = KillDef();
        var fired = Enumerable.Range(0, 5).Count(i => windows.Add(def, "p1", T0.AddSeconds(i)));
        Assert.Equal(1, fired);
        Assert.Equal(2, windows.KillsHeld(def.Id, "p1"));
    }

    [Fact]
    public void KillWindows_fails_when_dropped_fire_keeps_counter()
    {
        // the gate drops a fire for an active event after the counter fired: the counter is empty whatever the gate does
        var windows = new KillWindows();
        var def = KillDef();
        for (var i = 0; i < 3; i++) windows.Add(def, "p1", T0.AddSeconds(i));
        Assert.Equal(GateStep.Drop, new PlayerTriggerGate().Admit(def.Id, "p1", active: true, T0));
        Assert.Equal(0, windows.KillsHeld(def.Id, "p1"));
        Assert.Equal(0, windows.Counters);
    }

    [Fact]
    public void KillWindows_fails_when_shared_counts_per_player()
    {
        var shared = KillDef("{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60, \"shared\": true }");
        var windows = new KillWindows();
        Assert.False(windows.Add(shared, KillRule.CounterKey(shared, "p1"), T0));
        Assert.False(windows.Add(shared, KillRule.CounterKey(shared, "p2"), T0));
        Assert.True(windows.Add(shared, KillRule.CounterKey(shared, "p3"), T0));
        Assert.Equal("p1", KillRule.CounterKey(KillDef(), "p1"));
    }

    [Fact]
    public void KillWindows_fails_when_bounds_exceeded()
    {
        var windows = new KillWindows();
        var def = KillDef();
        for (var i = 0; i < TriggerLimits.CountersPerDefinition + 10; i++) windows.Add(def, $"p{i}", T0.AddSeconds(i % 50));
        Assert.Equal(TriggerLimits.CountersPerDefinition, windows.CountersFor(def.Id));
        var all = new KillWindows();
        for (var d = 0; d < 11; d++)
        {
            var many = KillDef(Reprisal, $"r{d}");
            for (var i = 0; i < TriggerLimits.CountersPerDefinition; i++) all.Add(many, $"p{i}", T0.AddSeconds(d * 200 + i));
        }
        Assert.Equal(TriggerLimits.CountersInAll, all.Counters);
        Assert.Equal(0, all.CountersFor("r0"));                                                // the least recently updated went first
        Assert.True(windows.KillsHeld(def.Id, $"p{TriggerLimits.CountersPerDefinition + 9}") <= def.Trigger.Kills);
    }

    [Fact]
    public void KillWindows_passes_owner_kill_counts()
    {
        var def = KillDef();
        Assert.True(KillRule.Counts(Kill(), def, null));
        var familiar = Kill(killer: null, owner: "p1");
        Assert.True(KillRule.Counts(familiar, def, null));
        Assert.Equal("p1", familiar.Player);
    }

    [Fact]
    public void KillWindows_passes_router_reaches_startable_only()
    {
        var set = Set(Json.Event("on", Reprisal), Disabled(Json.Event("off", Reprisal)), Json.Event("legion",
            "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Legion\"], \"kills\": 3, \"windowSeconds\": 60 }"));
        Assert.Equal(["on"], TriggerRouter.FactionKills(set, Kill(), null).Select(d => d.Id));
        var windows = new KillWindows();
        windows.Add(set.Find("on")!, "p1", T0);
        windows.Keep([]);
        Assert.Equal(0, windows.Counters);
    }

    [Fact]
    public void KillWindows_empty_no_deaths()
    {
        var windows = new KillWindows();
        Assert.Equal(0, windows.Counters);
        Assert.Empty(TriggerRouter.FactionKills(Set(Json.Event("manual")), Kill(), null));
    }

    // ---- D14 TriggerRules

    [Fact]
    public void TriggerRules_fails_when_disabled_definition_fires()
    {
        var set = Set(Disabled(Json.Event("off", Border)), Json.Event("bad", Border, extra: "\"bogus\": 1"), Disabled(Json.Event("koff", Reprisal)));
        Assert.Empty(TriggerRouter.Candidates(set, TriggerType.RegionEntered));
        Assert.Empty(TriggerRouter.FactionKills(set, Kill(), null));
        Assert.Empty(IntervalClock.PollAll(Set(Disabled(Json.Event("ioff", Interval))), new Dictionary<string, DateTime> { ["ioff"] = T0 }, _ => false, T0, new ScriptedRandom()).Due);
    }

    [Fact]
    public void TriggerRules_fails_when_active_event_reaches_conditions()
    {
        var gate = new PlayerTriggerGate();
        var rolls = 0;
        foreach (var player in new[] { "p1", "p2", "p3" })
            if (gate.Admit("border", player, active: true, T0) == GateStep.Attempt) rolls++;
        Assert.Equal(0, rolls);                                            // dropped before any start attempt and its chance roll
        Assert.Equal(GateStep.Attempt, gate.Admit("border", "p1", active: false, T0));   // a drop set no dedupe key
    }

    [Fact]
    public void TriggerRules_fails_when_two_refusals_in_60s_log_twice()
    {
        var gate = new PlayerTriggerGate();
        Assert.Equal("event border not started by RegionEntered: cooldown 30 min", gate.Refused("border", "event border not started by RegionEntered: cooldown 30 min", T0));
        Assert.Null(gate.Refused("border", "event border not started by RegionEntered: cooldown 30 min", T0.AddSeconds(59)));
        Assert.NotNull(gate.Refused("other", "event other not started by FactionKills: chance 50% not met", T0.AddSeconds(10)));   // per definition
    }

    [Fact]
    public void TriggerRules_fails_when_held_count_lost()
    {
        var gate = new PlayerTriggerGate();
        gate.Refused("border", "x", T0);
        gate.Refused("border", "x", T0.AddSeconds(10));
        gate.Refused("border", "x", T0.AddSeconds(20));
        Assert.Equal("x; 2 more since the last line", gate.Refused("border", "x", T0.AddSeconds(60)));
        Assert.Null(gate.Refused("border", "x", T0.AddSeconds(61)));
        Assert.Equal("x; 1 more since the last line", gate.Refused("border", "x", T0.AddSeconds(125)));
    }

    [Fact]
    public void TriggerRules_fails_when_cooldown_drop_sets_dedupe_key()
    {
        var entries = new RegionEntries();
        var gate = new PlayerTriggerGate();
        var defs = Entered(Border);
        entries.Scan([At(-10)], RegionOf, defs, T0);
        var entry = Assert.Single(entries.Scan([At(10)], RegionOf, defs, T0.AddSeconds(1)));
        Assert.Equal(GateStep.Attempt, gate.Admit(entry.Definition.Id, "p1", false, T0.AddSeconds(1)));
        entries.Attempted(entry.Definition, "p1", T0.AddSeconds(1));
        entries.Scan([At(-10, "p2")], RegionOf, defs, T0.AddSeconds(1));
        Assert.DoesNotContain(entries.Scan([At(-10), At(10, "p2")], RegionOf, defs, T0.AddSeconds(2)), e => e.PlayerId == "p1");
        Assert.Equal(GateStep.Attempt, gate.Admit(entry.Definition.Id, "p2", false, T0.AddSeconds(2)));     // p2's key is its own
    }

    [Fact]
    public void TriggerRules_fails_when_dedupe_lets_twice_in_5s()
    {
        var gate = new PlayerTriggerGate();
        Assert.Equal(GateStep.Attempt, gate.Admit("reprisal", "p1", false, T0));
        Assert.Equal(GateStep.Drop, gate.Admit("reprisal", "p1", false, T0.AddSeconds(4)));
        Assert.Equal(GateStep.Attempt, gate.Admit("reprisal", "p2", false, T0.AddSeconds(4)));
        Assert.Equal(GateStep.Attempt, gate.Admit("border", "p1", false, T0.AddSeconds(4)));
        Assert.Equal(GateStep.Attempt, gate.Admit("reprisal", "p1", false, T0.AddSeconds(5)));
    }

    [Fact]
    public void TriggerRules_passes_one_line_and_held_count()
    {
        var gate = new PlayerTriggerGate();
        var lines = new[] { 0, 20, 40, 70 }.Select(s => gate.Refused("border", "refused", T0.AddSeconds(s))).Where(l => l is not null).ToList();
        Assert.Equal(["refused", "refused; 2 more since the last line"], lines);
        Assert.True(TriggerRouter.IsPlayerAction(TriggerType.RegionEntered));
        Assert.True(TriggerRouter.IsPlayerAction(TriggerType.FactionKills));
        Assert.False(TriggerRouter.IsPlayerAction(TriggerType.Interval));
    }

    [Fact]
    public void TriggerRules_empty_no_trigger()
    {
        var gate = new PlayerTriggerGate();
        gate.Clear();
        Assert.Equal(GateStep.Attempt, gate.Admit("border", "p1", false, T0));
    }

    // ---- D13 Focus

    static EventEngine Engine(params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        return new EventEngine(catalog);
    }

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    const string Hunt = "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 5 } ], " +
        "\"waves\": 3, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 } }";

    [Fact]
    public void Focus_fails_when_eligible_focus_not_first()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b"), P(2000, "killer")];
        for (var r = 0.0; r < 1.0; r += 0.2)
        {
            var pick = PlayerPick.ChooseMany(players, Draws(20, r), 20, 20, Unclaimed, null, 2, 150, "killer");
            Assert.Equal("killer", Owners(players, pick)[0].PlatformId);
        }
    }

    [Fact]
    public void Focus_fails_when_ineligible_focus_skips_wave()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "killer", pvp: true)];
        var pick = PlayerPick.ChooseMany(players, Draws(20), 20, 20, Unclaimed, null, 1, 150, "killer");
        Assert.Equal(PickOutcome.Picked, pick.Outcome);
        Assert.Equal(["a"], Owners(players, pick).Select(p => p.PlatformId));
        var gone = PlayerPick.ChooseMany([P(0, "a")], Draws(20), 20, 20, Unclaimed, null, 1, 150, "left-the-server");
        Assert.Equal(PickOutcome.Picked, gone.Outcome);
    }

    [Fact]
    public void Focus_fails_when_other_trigger_carries_focus()
    {
        var engine = Engine(Json.Event("hunt", action: Hunt), Json.Event("tick", Interval, action: Hunt));
        Assert.Null(engine.Start("hunt", "manual", T0, Open(), origin: null));
        Assert.Null(engine.Start("tick", "Interval", T0, Open()));
        Assert.All(engine.Active, a => Assert.Null(a.Focus));
        foreach (var t in new[] { TriggerType.Manual, TriggerType.Schedule, TriggerType.GameTime, TriggerType.VBloodKilled, TriggerType.Interval })
            Assert.False(TriggerRouter.IsPlayerAction(t));
    }

    [Fact]
    public void Focus_fails_when_focus_reaches_state_or_line()
    {
        const string platformId = "91234567";
        var starts = new Dictionary<string, DateTime>();
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Json.Event("hunt", Border, action: Hunt)), FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog, () => starts);
        var controls = Open() with { PlayerPositions = () => [(10f, 0f)], RegionOf = RegionOf };
        Assert.Null(engine.Start("hunt", "RegionEntered", T0, controls, focus: platformId));
        var active = engine.Find("hunt")!;
        Assert.Equal(platformId, active.Focus);
        var state = new StateDocument { LastStart = starts, NextInterval = new() { ["tick"] = T0 } };
        Assert.DoesNotContain(platformId, Encoding.UTF8.GetString(state.Serialize()));
        var lines = EventLines.Info(active.Definition, active, T0)
            .Concat(ApiLines.Status(engine.Active, engine.PendingCleanups, catalog.Current, new Dictionary<string, int>(), true, T0));
        Assert.All(lines, l => Assert.DoesNotContain(platformId, l));
    }

    [Fact]
    public void Focus_passes_focus_first()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b"), P(2000, "entered")];
        var pick = PlayerPick.ChooseMany(players, Draws(20, 0.0), 20, 20, Unclaimed, null, 1, 150, "entered");
        Assert.Equal(["entered"], Owners(players, pick).Select(p => p.PlatformId));
    }

    [Fact]
    public void Focus_empty_no_focus()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b")];
        var plain = PlayerPick.ChooseMany(players, Draws(20, 0.0), 20, 20, Unclaimed, null, 1, 150, null);
        var empty = PlayerPick.ChooseMany(players, Draws(20, 0.0), 20, 20, Unclaimed, null, 1, 150, "");
        Assert.Equal(plain.Centres, empty.Centres);
        Assert.Equal(["a"], Owners(players, plain).Select(p => p.PlatformId));
    }

    // ---- D29 Phantoms

    [Theory]
    [InlineData("phantoms:0")]
    [InlineData("phantoms:10")]
    [InlineData("phantoms:-1")]
    [InlineData("phantoms:x")]
    [InlineData("phantom:4")]
    [InlineData("hook:DeathEvent")]
    public void Phantoms_fails_when_value_outside_1_9(string value) => Assert.Null(Phantoms.Parse(value));

    [Fact]
    public void Phantoms_fails_when_no_eligible_real_player()
    {
        Assert.Empty(Phantoms.Place([], 4));
        Assert.Empty(Phantoms.Place([P(0, "dead", alive: false), P(100, "pvp", pvp: true), P(float.NaN, "nan")], 4));
        // the anchor passes the pick's own territory and scope test (D5): a player in claimed territory is skipped
        var placed = Phantoms.Place([P(0, "castle"), P(1000, "field")], 2, (x, _) => x > 500);
        Assert.Equal([1200f, 1400f], placed.Select(p => p.X));
        Assert.Empty(Phantoms.Place([P(0, "castle")], 2, (_, _) => false));
    }

    [Fact]
    public void Phantoms_fails_when_unusable_kept()
    {
        var placed = Phantoms.Place([P(9500, "edge")], 4);                                     // 9700 fits, 9900 fits, 10100 is off the map
        Assert.Equal([9700f, 9900f], placed.Select(p => p.X));
        Assert.Equal("phantoms: 2 of 4 placed", Phantoms.PlacedLine(placed.Count, 4));
    }

    [Fact]
    public void Phantoms_fails_when_phantom_reaches_scan_or_hunt()
    {
        var phantom = Phantoms.Place([P(-500, "real")], 1).Single();
        var entries = new RegionEntries();
        entries.Scan([new ScanRow(phantom.PlatformId, -10, 0, true)], RegionOf, Entered(Border), T0);
        Assert.Empty(entries.Scan([new ScanRow(phantom.PlatformId, 10, 0, true)], RegionOf, Entered(Border), T0.AddSeconds(5)));
        Assert.Equal(0, entries.RegionRows);
        Assert.DoesNotContain(typeof(HuntCandidate).GetProperties(), p => p.PropertyType == typeof(string));      // a hunt target has no platform id
    }

    [Fact]
    public void Phantoms_passes_four_at_200m_steps()
    {
        Assert.Equal(4, Phantoms.Parse("phantoms:4"));
        Assert.Equal(9, Phantoms.Parse("phantoms:9"));
        var placed = Phantoms.Place([P(100, "off", online: false), new PickCandidate(-300, 12, 40, true, true, false, "real")], 4);
        Assert.Equal([-100f, 100f, 300f, 500f], placed.Select(p => p.X));
        Assert.All(placed, p => Assert.Equal((12f, 40f, true), (p.Y, p.Z, Phantoms.IsPhantom(p.PlatformId))));
        Assert.Equal("fanout hunt wave 2: phantom group 3 18 m from its phantom", Phantoms.GroupLine("hunt", 2, 3, 17.6));
    }

    [Fact]
    public void Phantoms_empty_value()
    {
        Assert.Null(Phantoms.Parse(""));
        Assert.Null(Phantoms.Parse(null));
        Assert.Null(Phantoms.Parse("phantoms:"));
    }

    // ---- D31 StateNextInterval

    static StateDocument Parse(string json) => StateDocument.TryParse(Encoding.UTF8.GetBytes(json))!;

    [Fact]
    public void StateNextInterval_fails_when_bad_entry_drops_others()
    {
        var doc = Parse("{ \"SchemaVersion\": 1, \"NextInterval\": { \"a\": \"2026-09-29T20:05:00Z\", \"b\": 5, \"c\": \"soon\", " +
                        "\"d\": \"2026-09-29T20:05:00\", \"e\": \"2026-09-29T20:06:00Z\" } }");
        Assert.Equal(["a", "e"], doc.NextInterval!.Keys.OrderBy(k => k));
        Assert.Equal(DateTimeKind.Utc, doc.NextInterval["a"].Kind);
        Assert.NotNull(Parse("{ \"SchemaVersion\": 1, \"NextInterval\": [1, 2] }"));           // not an object: no entries, the file loads
    }

    [Fact]
    public void StateNextInterval_fails_when_empty_gains_key()
    {
        foreach (var doc in new[] { new StateDocument(), new StateDocument { NextInterval = new() } })
            Assert.DoesNotContain("NextInterval", Encoding.UTF8.GetString(doc.Serialize()));
    }

    [Fact]
    public void StateNextInterval_passes_fixture_round_trip()
    {
        // writing an empty NextInterval leaves it out without changing the document (step 1 code review F1)
        var held = new Dictionary<string, DateTime>();
        var empty = new StateDocument { NextInterval = held };
        Assert.DoesNotContain("NextInterval", Encoding.UTF8.GetString(empty.Serialize()));
        Assert.Same(held, empty.NextInterval);
        held["tick"] = T0;
        Assert.Contains("\"NextInterval\"", Encoding.UTF8.GetString(empty.Serialize()));
        // an unspecified time is written as the UTC it already holds, never shifted by the server's offset (round 2 F7)
        var unspecified = new StateDocument { NextInterval = new() { ["tick"] = DateTime.SpecifyKind(T0, DateTimeKind.Unspecified) } };
        Assert.Equal(T0, StateDocument.TryParse(unspecified.Serialize())!.NextInterval!["tick"]);

        var doc = new StateDocument { NextInterval = new() { ["tick"] = T0.AddMinutes(75) } };
        var text = Encoding.UTF8.GetString(doc.Serialize());
        Assert.Contains("\"NextInterval\": {", text);
        Assert.Contains("\"tick\": \"2026-09-29T21:15:00Z\"", text);
        var back = StateDocument.TryParse(doc.Serialize())!;
        Assert.Equal(T0.AddMinutes(75), back.NextInterval!["tick"]);
        Assert.Equal(1, back.SchemaVersion);
    }

    [Fact]
    public void StateNextInterval_empty_no_key()
    {
        var doc = Parse("{ \"SchemaVersion\": 1, \"LastStart\": {} }");
        Assert.Null(doc.NextInterval);
        Assert.Empty(IntervalClock.Load(doc.NextInterval, Set(Json.Event("tick", Interval)), T0, new ScriptedRandom()));
    }
}
