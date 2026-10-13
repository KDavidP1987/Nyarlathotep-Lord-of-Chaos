using System.Text;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D3-D8 and D13: the heat, the private warnings, the 5 s gate, the focus-only waves and hunts, the
/// faction alert and the private lines, the end paths and the admin start, over Logic/Revenge.cs and the feed and pick it
/// joins.</summary>
public class RevengeTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 13, 20, 0);
    const string P1 = "p1";
    const string P2 = "p2";

    static string Trigger(int heat = 6, int window = 900, string warnAt = "[2, 4]", int vblood = 10, int cooldown = 5, string? scope = null,
        string factions = "[\"Faction_Bandits\"]") =>
        $"{{ \"type\": \"Revenge\", \"factions\": {factions}, \"heat\": {heat}, \"windowSeconds\": {window}, \"warnAt\": {warnAt}, " +
        $"\"vbloodHeat\": {vblood}, \"playerCooldownMinutes\": {cooldown}{(scope is null ? "" : $", \"scope\": {scope}")} }}";

    static string Event(string id = "rv", string? trigger = null, string? action = null) => Json.Event(id, trigger ?? Trigger(), action: action ?? Json.RevengeAction);

    static DefinitionSet Set(params string[] events)
    {
        var r = EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(r.FileError);
        Assert.All(r.Set.All, d => Assert.Null(d.DisabledReason));
        return r.Set;
    }

    static EventDefinition Def(string id = "rv", string? trigger = null, string? action = null) => Set(Event(id, trigger, action)).Find(id)!;

    static KillFacts Kill(string? player = P1, string faction = "Faction_Bandits", bool vblood = false, bool victimPlayer = false, bool minion = false,
        bool ours = false, bool self = false, string? owner = null, float? x = null) =>
        new(player, owner, victimPlayer, minion, ours, faction, x, x is null ? null : 0f, self, vblood);

    static RevengeTarget At(string id, float x = 100f, bool alive = true, bool pvp = false) => new(id, x, 0f, 0f, alive, pvp);

    static readonly Func<float, float, bool> Unclaimed = (_, _) => false;

    /// <summary>One counted kill of every startable Revenge definition through the feed, at <paramref name="at"/>.</summary>
    static void Died(PlayerTriggerFeed feed, DefinitionSet set, KillFacts kill, DateTime at, LogLines log, List<(string, string)>? told = null,
        Func<Pillar, bool>? pillarOn = null) =>
        feed.Died(_ => kill, set, new FakeRegions().RegionOf, _ => true, _ => false, at, log.Add, _ => { }, null,
            told is null ? null : (p, l) => told.Add((p, l)), pillarOn);

    /// <summary>One scan of <paramref name="heat"/> over <paramref name="players"/>, every attempt answered by
    /// <paramref name="result"/>; returns the players started, in order.</summary>
    static List<string> Scan(RevengeHeat heat, DefinitionSet set, IReadOnlyList<RevengeTarget> players, DateTime at, LogLines log,
        Func<PlayerFire, RevengeStartResult>? result = null, Func<string, bool>? active = null, Func<string, bool>? targetBusy = null,
        Func<float, float, bool>? claimed = null, bool mapDown = false)
    {
        var started = new List<string>();
        heat.Scan(PlayerTriggerFeed.RevengeStartable(set, null), () => players, mapDown ? null : claimed ?? Unclaimed, new FakeRegions().RegionOf,
            active ?? (_ => false), targetBusy ?? (_ => false), at, log.Add,
            f => { var r = result?.Invoke(f) ?? RevengeStartResult.Started; if (r == RevengeStartResult.Started) started.Add(f.PlayerId); return r; });
        return started;
    }

    // ---- D3 Heat

    [Fact]
    public void Heat_fails_when_an_excluded_kill_adds_heat()
    {
        var set = Set(Event());
        var d = set.Find("rv")!;
        foreach (var kill in new[]
                 {
                     Kill(faction: "Faction_Legion"), Kill(minion: true), Kill(ours: true), Kill(victimPlayer: true), Kill(self: true), Kill(player: null),
                 })
            Assert.False(KillRule.CountsHeat(kill, d, null), kill.ToString());
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        foreach (var kill in new[] { Kill(faction: "Faction_Legion"), Kill(minion: true), Kill(ours: true), Kill(victimPlayer: true), Kill(self: true) })
            Died(feed, set, kill, T0, log);
        Assert.Equal(0, feed.Heat.Rows);
        // a FactionKills definition never takes heat, and the kill rule of FactionKills never counts a Revenge definition
        var fk = Def("fk", "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }", Json.ValidAction);
        Assert.False(KillRule.CountsHeat(Kill(), fk, null));
        Assert.False(KillRule.Counts(Kill(), d, null));
        // with a regional trigger scope a kill outside the regions, or of unknown position, counts nothing
        var scoped = Def(trigger: Trigger(scope: "[\"CursedForest\"]"));
        Assert.True(KillRule.CountsHeat(Kill(x: 10f), scoped, new FakeRegions().RegionOf));
        Assert.False(KillRule.CountsHeat(Kill(x: -10f), scoped, new FakeRegions().RegionOf));
        Assert.False(KillRule.CountsHeat(Kill(), scoped, new FakeRegions().RegionOf));
    }

    [Fact]
    public void Heat_fails_when_a_familiar_kill_is_not_credited()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        Died(feed, set, Kill(player: null, owner: P1), T0, new LogLines());
        Assert.Equal(1, feed.Heat.HeatOf("rv", P1));
    }

    [Fact]
    public void Heat_fails_when_a_vblood_kill_adds_other_than_vblood_heat()
    {
        var set = Set(Event(trigger: Trigger(heat: 30)), Event("zero", Trigger(heat: 30, vblood: 0)));
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        Died(feed, set, Kill(vblood: true), T0, log);
        Assert.Equal(10, feed.Heat.HeatOf("rv", P1));
        Assert.Equal(0, feed.Heat.HeatOf("zero", P1));                       // vbloodHeat 0: a V Blood kill counts nothing
        Assert.Contains("revenge rv: vblood kill +10 heat", log.Lines);
        Assert.DoesNotContain(log.Lines, l => l.Contains("revenge zero", StringComparison.Ordinal));
        Died(feed, set, Kill(), T0, log);
        Assert.Equal(11, feed.Heat.HeatOf("rv", P1));
        Assert.Equal(1, feed.Heat.HeatOf("zero", P1));
    }

    [Fact]
    public void Heat_fails_when_a_point_outlives_its_window()
    {
        var d = Def(trigger: Trigger(heat: 30, window: 60));
        var heat = new RevengeHeat();
        heat.Add(d, P1, 1, T0);
        heat.Add(d, P1, 1, T0.AddSeconds(30));
        Assert.Equal(1, heat.Add(d, P1, 1, T0.AddSeconds(59)).Points);
        Assert.Equal(3, heat.HeatOf("rv", P1));
        heat.Prune([d], T0.AddSeconds(60));                                    // the first point leaves exactly at its window
        Assert.Equal(2, heat.HeatOf("rv", P1));
        heat.Prune([d], T0.AddSeconds(119).AddTicks(1));
        Assert.Equal(0, heat.HeatOf("rv", P1));
        Assert.Equal(0, heat.Rows);
    }

    [Fact]
    public void Heat_fails_when_heat_is_added_during_the_cooldown()
    {
        var set = Set(Event());
        var d = set.Find("rv")!;
        var heat = new RevengeHeat();
        heat.Add(d, P1, 6, T0);
        Assert.Equal([P1], Scan(heat, set, [At(P1)], T0, new LogLines()));
        Assert.True(heat.InCooldown("rv", P1, T0.AddMinutes(4)));
        Assert.Equal(0, heat.Add(d, P1, 1, T0.AddMinutes(4)).Points);
        Assert.Equal(0, heat.HeatOf("rv", P1));
        Assert.Equal(1, heat.Add(d, P1, 1, T0.AddMinutes(5)).Points);        // the 5 min cooldown is over
        // the cooldown lives outside the rows: an eviction of every row never lifts it
        heat.Add(d, P2, 6, T0);
        Scan(heat, set, [At(P2)], T0.AddMinutes(10), new LogLines());
        for (var i = 0; i < TriggerLimits.CountersPerDefinition + 5; i++) heat.Add(d, $"x{i}", 1, T0.AddMinutes(11));
        Assert.Equal(0, heat.Add(d, P2, 1, T0.AddMinutes(12)).Points);
    }

    [Fact]
    public void Heat_fails_when_the_201st_row_is_kept()
    {
        var d = Def(trigger: Trigger(heat: 30));
        var heat = new RevengeHeat();
        for (var i = 0; i <= TriggerLimits.CountersPerDefinition; i++) heat.Add(d, $"x{i}", 1, T0.AddSeconds(i));
        Assert.Equal(TriggerLimits.CountersPerDefinition, heat.RowsFor("rv"));
        Assert.Equal(0, heat.HeatOf("rv", "x0"));                              // the least recently updated row went
        Assert.Equal(1, heat.HeatOf("rv", $"x{TriggerLimits.CountersPerDefinition}"));
        // a due player's row is never the one dropped
        var due = Def("due", Trigger(heat: 3, warnAt: "[]"));
        var held = new RevengeHeat();
        held.Add(due, P1, 3, T0);
        for (var i = 0; i < TriggerLimits.CountersPerDefinition + 10; i++) held.Add(due, $"y{i}", 1, T0.AddSeconds(i + 1));
        Assert.True(held.IsDue("due", P1));
        Assert.Equal(TriggerLimits.CountersPerDefinition, held.RowsFor("due"));
        // the bound in all
        var all = new RevengeHeat();
        var defs = Enumerable.Range(0, 11).Select(n => Def($"d{n}", Trigger(heat: 30))).ToList();
        foreach (var def in defs)
            for (var i = 0; i < TriggerLimits.CountersPerDefinition; i++) all.Add(def, $"z{i}", 1, T0);
        Assert.Equal(TriggerLimits.CountersInAll, all.Rows);
    }

    [Fact]
    public void Heat_fails_when_heat_rows_and_kill_counters_evict_each_other()
    {
        var feed = new PlayerTriggerFeed();
        var fks = Enumerable.Range(0, 10).Select(n => Def($"fk{n}", "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 500, \"windowSeconds\": 3600 }",
            Json.ValidAction)).ToList();
        var rvs = Enumerable.Range(0, 10).Select(n => Def($"rv{n}", Trigger(heat: 500))).ToList();
        foreach (var fk in fks)
            for (var i = 0; i < TriggerLimits.CountersPerDefinition; i++) feed.Kills.Add(fk, $"k{i}", T0);
        foreach (var rv in rvs)
            for (var i = 0; i < TriggerLimits.CountersPerDefinition; i++) feed.Heat.Add(rv, $"h{i}", 1, T0);
        Assert.Equal(TriggerLimits.CountersInAll, feed.Kills.Counters);
        Assert.Equal(TriggerLimits.CountersInAll, feed.Heat.Rows);
        feed.Heat.Add(rvs[0], "extra", 1, T0.AddSeconds(1));
        feed.Kills.Add(fks[0], "extra", T0.AddSeconds(1));
        Assert.Equal(TriggerLimits.CountersInAll, feed.Kills.Counters);
        Assert.Equal(TriggerLimits.CountersInAll, feed.Heat.Rows);
        Assert.Equal(1, feed.Kills.KillsHeld("fk1", "k0"));                   // the other table's rows are all still there
        Assert.Equal(1, feed.Heat.HeatOf("rv1", "h0"));
    }

    [Fact]
    public void Heat_fails_when_a_disabled_definition_keeps_rows()
    {
        var set = Set(Event(), Event("other"));
        var feed = new PlayerTriggerFeed();
        Died(feed, set, Kill(), T0, new LogLines());
        Assert.Equal(1, feed.Heat.RowsFor("rv"));
        var disabled = new DefinitionSet(set.All.Select(d => d.Id == "rv" ? d with { Enabled = false } : d).ToList());
        feed.Scan(() => [], null, disabled, _ => false, T0.AddSeconds(5), _ => { }, _ => { });
        Assert.Equal(0, feed.Heat.RowsFor("rv"));
        Assert.Equal(1, feed.Heat.RowsFor("other"));
        // a death with no Revenge or FactionKills definition startable is not read
        var none = new DefinitionSet(set.All.Select(d => d with { Enabled = false }).ToList());
        var reads = 0;
        feed.Died(_ => { reads++; return Kill(); }, none, null, _ => true, _ => false, T0, _ => { }, _ => { });
        Assert.Equal(0, reads);
        Assert.False(PlayerTriggerFeed.WantsKills(none, _ => true));
        Assert.True(PlayerTriggerFeed.WantsKills(set, _ => true));
        Assert.False(PlayerTriggerFeed.WantsKills(set, t => t != TriggerType.Revenge));
    }

    [Fact]
    public void Heat_passes_kills_within_the_window()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        for (var i = 0; i < 5; i++) Died(feed, set, Kill(), T0.AddSeconds(i), log);
        Died(feed, set, Kill(P2), T0, log);
        Assert.Equal(5, feed.Heat.HeatOf("rv", P1));
        Assert.Equal(1, feed.Heat.HeatOf("rv", P2));                           // each player counts alone
        Assert.False(feed.Heat.IsDue("rv", P1));
        Died(feed, set, Kill(), T0.AddSeconds(6), log);
        Assert.True(feed.Heat.IsDue("rv", P1));
        Assert.Equal(1, log.Count("revenge rv due"));
    }

    [Fact]
    public void Heat_passes_an_edit_keeps_points_and_applies_the_new_heat()
    {
        var heat = new RevengeHeat();
        var before = Def(trigger: Trigger(heat: 10));
        for (var i = 0; i < 7; i++) heat.Add(before, P1, 1, T0);
        Assert.False(heat.IsDue("rv", P1));
        var after = Def(trigger: Trigger(heat: 6, factions: "[\"Faction_Bandits\", \"Faction_Legion\"]"));     // heat and factions edited
        heat.Prune([after], T0.AddSeconds(5));
        Assert.Equal(7, heat.HeatOf("rv", P1));                                // the row keeps its points
        Assert.True(heat.IsDue("rv", P1));                                     // and is due without a new kill
        heat.Prune([before], T0.AddSeconds(10));                               // raised again: no longer due
        Assert.False(heat.IsDue("rv", P1));
    }

    [Fact]
    public void Heat_empty_no_revenge_definition()
    {
        var feed = new PlayerTriggerFeed();
        var set = Set(Json.Event("manual"));
        Died(feed, set, Kill(), T0, new LogLines());
        feed.Scan(() => [], null, set, _ => false, T0, _ => { }, _ => { });
        Assert.Equal((0, 0, 0), (feed.Heat.Rows, feed.Heat.Due, feed.Heat.Cooldowns));
        Assert.Empty(feed.Health);
    }

    // ---- D4 Warnings

    [Fact]
    public void Warnings_fails_when_a_warning_reaches_another_player()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        var told = new List<(string, string)>();
        for (var i = 0; i < 4; i++) Died(feed, set, Kill(), T0.AddSeconds(i), new LogLines(), told);
        Died(feed, set, Kill(P2), T0, new LogLines(), told);
        Assert.Equal(2, told.Count);
        Assert.All(told, t => Assert.Equal(P1, t.Item1));
    }

    [Fact]
    public void Warnings_fails_when_a_warning_repeats_within_a_climb()
    {
        var d = Def(trigger: Trigger(heat: 30, warnAt: "[2, 4]"));
        var heat = new RevengeHeat();
        var warnings = Enumerable.Range(0, 8).Select(i => heat.Add(d, P1, 1, T0.AddSeconds(i)).Warning).ToList();
        Assert.Equal([0, 1, 0, 2, 0, 0, 0, 0], warnings);
    }

    [Fact]
    public void Warnings_fails_when_a_threshold_does_not_rearm()
    {
        var d = Def(trigger: Trigger(heat: 30, window: 60, warnAt: "[2, 4]"));
        var heat = new RevengeHeat();
        heat.Add(d, P1, 1, T0);
        heat.Add(d, P1, 1, T0.AddSeconds(30));                                 // warning 1
        heat.Prune([d], T0.AddSeconds(61));                                    // the first point leaves: heat 1, below 2
        Assert.Equal(1, heat.HeatOf("rv", P1));
        Assert.Equal(1, heat.Add(d, P1, 1, T0.AddSeconds(62)).Warning);        // re-armed: warning 1 again
        Assert.Equal(0, heat.Add(d, P1, 1, T0.AddSeconds(63)).Warning);
        Assert.Equal(2, heat.Add(d, P1, 1, T0.AddSeconds(64)).Warning);
    }

    [Fact]
    public void Warnings_fails_when_one_kill_sends_two_lines()
    {
        var set = Set(Event(trigger: Trigger(heat: 30, warnAt: "[2, 4]", vblood: 10)));
        var feed = new PlayerTriggerFeed();
        var told = new List<(string, string)>();
        var log = new LogLines();
        Died(feed, set, Kill(vblood: true), T0, log, told);                    // 10 points pass both thresholds
        Assert.Single(told);
        Assert.Equal(Messages.RevengeWarning(2, 2, "Bandits"), told[0].Item2);
        Assert.Equal(["revenge rv: vblood kill +10 heat", "revenge rv: warning 2 of 2"], log.Lines);
    }

    [Fact]
    public void Warnings_fails_when_a_line_holds_a_name_digits_or_angle()
    {
        var five = string.Join(", ", Enumerable.Range(0, 5).Select(i => new string((char)('A' + i), 30)));
        for (var k = 1; k <= 3; k++)
        {
            var line = Messages.RevengeWarning(k, 3, five);
            Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes);
            Assert.DoesNotMatch(@"\d\d|<", line);
            Assert.Contains(new string('E', 30), line);
            Assert.DoesNotContain("Zebulonrex", line);
        }
        Assert.Equal("The Bandits have noticed your killing.", Messages.RevengeWarning(1, 2, "Bandits"));
        Assert.Equal("The Bandits are gathering against you.", Messages.RevengeWarning(2, 2, "Bandits"));
    }

    [Fact]
    public void Warnings_fails_when_the_log_line_names_the_player()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        var log = new LogLines();
        const string id = "76000000000000042";
        for (var i = 0; i < 6; i++) Died(feed, set, Kill(id, x: 4321.7f), T0.AddSeconds(i), log, []);
        Assert.Equal(["revenge rv: warning 1 of 2", "revenge rv: warning 2 of 2", "revenge rv due"], log.Lines);
        Assert.All(log.Lines, l => Assert.DoesNotMatch(@"76000|4321|<", l));
    }

    [Fact]
    public void Warnings_passes_each_threshold_once_in_order()
    {
        var set = Set(Event(trigger: Trigger(heat: 10, warnAt: "[3, 6, 9]")));
        var feed = new PlayerTriggerFeed();
        var told = new List<(string, string)>();
        for (var i = 0; i < 9; i++) Died(feed, set, Kill(), T0.AddSeconds(i), new LogLines(), told);
        Assert.Equal([Messages.RevengeWarning(1, 3, "Bandits"), Messages.RevengeWarning(2, 3, "Bandits"), Messages.RevengeWarning(3, 3, "Bandits")],
            told.Select(t => t.Item2));
        Assert.Equal("The Bandits are almost ready to strike.", told[2].Item2);
    }

    [Fact]
    public void Warnings_empty_warn_at_none()
    {
        var set = Set(Event(trigger: Trigger(warnAt: "[]")));
        var feed = new PlayerTriggerFeed();
        var told = new List<(string, string)>();
        var log = new LogLines();
        for (var i = 0; i < 6; i++) Died(feed, set, Kill(), T0.AddSeconds(i), log, told);
        Assert.Empty(told);
        Assert.Equal(["revenge rv due"], log.Lines);                          // no warning, the player is still due
    }

    // ---- D5 Gate

    [Fact]
    public void Gate_fails_when_an_ineligible_player_starts()
    {
        var scoped = "[\"CursedForest\"]";
        var action = Json.RevengeAction.Replace("\"behaviour\"", "\"scope\": " + scoped + ", \"behaviour\"");
        var set = Set(Event(action: action));
        var d = set.Find("rv")!;
        foreach (var (players, claimed, mapDown, reason) in new (RevengeTarget[], Func<float, float, bool>?, bool, string)[]
                 {
                     ([], null, false, "ineligible"),                                       // offline
                     ([At(P1, alive: false)], null, false, "ineligible"),
                     ([At(P1, pvp: true)], null, false, "ineligible"),
                     ([At(P1, float.NaN)], null, false, "ineligible"),
                     ([At(P1, 20000f)], null, false, "ineligible"),
                     ([At(P1, -100f)], null, false, "ineligible"),                         // outside the regional action scope
                     ([At(P1)], null, true, "ineligible"),                                  // the territory map is down
                     ([At(P1)], (_, _) => true, false, "territory"),
                 })
        {
            var heat = new RevengeHeat();
            var log = new LogLines();
            heat.Add(d, P1, 6, T0);
            Assert.Empty(Scan(heat, set, players, T0, log, claimed: claimed, mapDown: mapDown));
            Assert.Equal([$"revenge rv: held ({reason})"], log.Lines);
            Assert.True(heat.IsDue("rv", P1));
            Assert.Equal(6, heat.HeatOf("rv", P1));
        }
    }

    [Fact]
    public void Gate_fails_when_the_controls_refusal_resets_or_is_not_held()
    {
        var set = Set(Event());
        var heat = new RevengeHeat();
        var log = new LogLines();
        heat.Add(set.Find("rv")!, P1, 6, T0);
        Assert.Empty(Scan(heat, set, [At(P1)], T0, log, _ => RevengeStartResult.Refused));
        Assert.Equal(["revenge rv: held (controls)"], log.Lines);
        Assert.True(heat.IsDue("rv", P1));
        Assert.Equal(6, heat.HeatOf("rv", P1));
        Assert.False(heat.InCooldown("rv", P1, T0));
        // the refusal classes: a purge cooldown, the pillar or the cap are the controls; a condition is not
        Assert.Equal(RevengeStartResult.Refused, RevengeRule.Classify(Outcome.Refused("pillar spawns is off", RefusalCode.Disabled, reason: Reasons.PillarOff)));
        Assert.Equal(RevengeStartResult.Refused, RevengeRule.Classify(Outcome.Refused(Precedence.PurgeCooldown, RefusalCode.Cooldown)));
        Assert.Equal(RevengeStartResult.ConditionsRefused, RevengeRule.Classify(Outcome.Refused("chance", RefusalCode.State, reason: Reasons.Condition)));
        Assert.Equal(RevengeStartResult.Started, RevengeRule.Classify(null));
    }

    [Fact]
    public void Gate_fails_when_a_busy_definition_or_target_starts()
    {
        var set = Set(Event(), Event("rv2"));
        var heat = new RevengeHeat();
        var log = new LogLines();
        heat.Add(set.Find("rv")!, P1, 6, T0);
        heat.Add(set.Find("rv")!, P2, 6, T0);
        heat.Add(set.Find("rv2")!, P1, 6, T0);
        // in one scan: P1 starts rv; P2 finds rv busy; rv2 finds P1 targeted
        Assert.Equal([P1], Scan(heat, set, [At(P1), At(P2)], T0, log));
        Assert.Equal(["revenge rv: held (busy)", "revenge rv2: held (target busy)"], log.Lines);
        // the running instances as the caller reports them
        var later = new LogLines();
        Assert.Empty(Scan(heat, set, [At(P1), At(P2)], T0.AddSeconds(5), later, active: id => id == "rv", targetBusy: p => p == P1));
        Assert.Empty(later.Lines);                                             // the same reasons: no new line
    }

    [Fact]
    public void Gate_fails_when_a_held_player_loses_heat()
    {
        var set = Set(Event(trigger: Trigger(window: 60)));
        var heat = new RevengeHeat();
        heat.Add(set.Find("rv")!, P1, 6, T0);
        for (var s = 0; s < 55; s += 5) Scan(heat, set, [At(P1)], T0.AddSeconds(s), new LogLines(), claimed: (_, _) => true);
        Assert.Equal(6, heat.HeatOf("rv", P1));
        Assert.True(heat.IsDue("rv", P1));
        Scan(heat, set, [At(P1)], T0.AddSeconds(60), new LogLines(), claimed: (_, _) => true);
        Assert.False(heat.IsDue("rv", P1));                                    // only the window takes it
    }

    [Fact]
    public void Gate_fails_when_the_hold_line_repeats()
    {
        var set = Set(Event());
        var heat = new RevengeHeat();
        var log = new LogLines();
        heat.Add(set.Find("rv")!, P1, 6, T0);
        Scan(heat, set, [At(P1)], T0, log, claimed: (_, _) => true);
        Scan(heat, set, [At(P1)], T0.AddSeconds(5), log, claimed: (_, _) => true);
        Scan(heat, set, [], T0.AddSeconds(10), log);
        Scan(heat, set, [], T0.AddSeconds(15), log);
        Scan(heat, set, [At(P1)], T0.AddSeconds(20), log, claimed: (_, _) => true);
        Assert.Equal(["revenge rv: held (territory)", "revenge rv: held (ineligible)", "revenge rv: held (territory)"], log.Lines);
    }

    [Fact]
    public void Gate_fails_when_heat_survives_a_start_or_a_conditions_refusal()
    {
        var set = Set(Event());
        var d = set.Find("rv")!;
        var heat = new RevengeHeat();
        heat.Add(d, P1, 7, T0);
        Scan(heat, set, [At(P1)], T0, new LogLines());
        Assert.Equal((0, false), (heat.HeatOf("rv", P1), heat.IsDue("rv", P1)));
        heat.Add(d, P2, 6, T0);
        Scan(heat, set, [At(P2)], T0, new LogLines(), _ => RevengeStartResult.ConditionsRefused);
        Assert.Equal((0, false), (heat.HeatOf("rv", P2), heat.IsDue("rv", P2)));
    }

    [Fact]
    public void Gate_fails_when_a_conditions_refusal_starts_a_cooldown()
    {
        var set = Set(Event());
        var d = set.Find("rv")!;
        var heat = new RevengeHeat();
        heat.Add(d, P1, 6, T0);
        Scan(heat, set, [At(P1)], T0, new LogLines(), _ => RevengeStartResult.ConditionsRefused);
        Assert.False(heat.InCooldown("rv", P1, T0));
        Assert.Equal(1, heat.Add(d, P1, 1, T0.AddSeconds(1)).Points);
        // a start does start it; a cooldown of 0 keeps no row
        heat.Add(d, P2, 6, T0);
        Scan(heat, set, [At(P2)], T0, new LogLines());
        Assert.True(heat.InCooldown("rv", P2, T0));
        var none = Set(Event(trigger: Trigger(cooldown: 0)));
        var free = new RevengeHeat();
        free.Add(none.Find("rv")!, P1, 6, T0);
        Scan(free, none, [At(P1)], T0, new LogLines());
        Assert.Equal(0, free.Cooldowns);
    }

    [Fact]
    public void Gate_fails_when_due_players_start_out_of_order()
    {
        var set = Set(Event("a"), Event("b"), Event("c"));
        var heat = new RevengeHeat();
        heat.Add(set.Find("c")!, "x", 6, T0);
        heat.Add(set.Find("a")!, "y", 6, T0.AddSeconds(1));
        heat.Add(set.Find("b")!, "z", 6, T0.AddSeconds(2));
        var order = new List<string>();
        heat.Scan(PlayerTriggerFeed.RevengeStartable(set, null), () => [At("z"), At("y"), At("x")], Unclaimed, null, _ => false, _ => false, T0.AddSeconds(5),
            _ => { }, f => { order.Add(f.Definition.Id); return RevengeStartResult.Started; });
        Assert.Equal(["c", "a", "b"], order);
    }

    [Fact]
    public void Gate_passes_an_eligible_due_player_starts()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        for (var i = 0; i < 6; i++) Died(feed, set, Kill(), T0.AddSeconds(i), new LogLines());
        var fires = new List<PlayerFire>();
        feed.Scan(() => [], null, set, _ => false, T0.AddSeconds(10), _ => { }, _ => { }, null,
            new RevengeScan(() => [At(P1, 250f)], Unclaimed, _ => false, f => { fires.Add(f); return RevengeStartResult.Started; }));
        var fire = Assert.Single(fires);
        Assert.Equal(("rv", "Revenge", P1, ((float, float)?)(250f, 0f)), (fire.Definition.Id, fire.Trigger, fire.PlayerId, fire.At));
        Assert.True(TriggerRouter.IsPlayerAction(TriggerType.Revenge));
        Assert.Equal(Hook.DeathEvent, HookSet.HookFor(TriggerType.Revenge));
    }

    [Fact]
    public void Gate_empty_nobody_due()
    {
        var set = Set(Event());
        var reads = 0;
        new RevengeHeat().Scan(PlayerTriggerFeed.RevengeStartable(set, null), () => { reads++; return []; }, Unclaimed, null, _ => false, _ => false, T0,
            _ => { }, _ => RevengeStartResult.Started);
        Assert.Equal(0, reads);                                                // nobody due: no player is read
    }

    // ---- D6 FocusOnly

    static PickCandidate P(float x, string id) => new(x, 0f, 0f, true, true, false, id);

    [Fact]
    public void FocusOnly_fails_when_a_wave_is_centred_on_another_player()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b"), P(2000, P1)];
        for (var r = 0.0; r < 1.0; r += 0.1)
        {
            var pick = PlayerPick.ChooseFocus(players, new ScriptedRandom(r, r, r), 20, 40, Unclaimed, null, P1);
            Assert.Equal(PickOutcome.Picked, pick.Outcome);
            Assert.InRange(pick.Centre.X, 2000 - 40, 2000 + 40);
        }
    }

    [Fact]
    public void FocusOnly_fails_when_another_player_is_a_hunt_target()
    {
        HuntCandidate[] players = [new(1, 5, 0, true, true, false, false), new(2, 10, 0, true, true, false, false), new(3, 20, 0, true, true, false, false)];
        var key = HuntPlan.FocusKey([(1L, "a"), (2L, "b"), (3L, P1)], P1);
        Assert.Equal(3L, key);
        Assert.Equal([3L], HuntPlan.TargetsFor(players, (0, 0), 45, true, key));
        Assert.Empty(HuntPlan.TargetsFor(players, (0, 0), 45, true, HuntPlan.FocusKey([(1L, "a")], P1)));        // the focus offline
        Assert.Empty(HuntPlan.TargetsFor([players[0], players[1], new(3, 20, 0, true, true, true, false)], (0, 0), 45, true, 3L));   // in territory
    }

    [Fact]
    public void FocusOnly_fails_when_an_ineligible_focus_spawns_or_skips()
    {
        PickCandidate[] others = [P(0, "a")];
        foreach (var focus in new[]
                 {
                     new PickCandidate(100, 0, 0, false, true, false, P1), new PickCandidate(100, 0, 0, true, false, false, P1),
                     new PickCandidate(float.NaN, 0, 0, true, true, false, P1), new PickCandidate(100, 0, 0, true, true, true, P1),
                 })
        {
            var pick = PlayerPick.ChooseFocus([.. others, focus], new ScriptedRandom(0.5, 0.5), 20, 40, Unclaimed, null, P1);
            Assert.Equal(PickOutcome.NoEligible, pick.Outcome);
            var facts = new WaveFacts(1, "rv", false, Location: LocationType.AroundPlayer, Behaviour: BehaviourType.Hunt, Pick: pick.Outcome, FocusOnly: true);
            var decision = WaveGate.Decide(facts, () => ["CHAR_Bandit_Thug"], 20, 0, 150);
            Assert.Equal((WaveOutcome.NoWave, (string?)null), (decision.Outcome, decision.Line));
        }
        // in claimed territory and outside a regional scope
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.ChooseFocus([P(100, P1)], new ScriptedRandom(0.5, 0.5), 20, 40, (_, _) => true, null, P1).Outcome);
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.ChooseFocus([P(100, P1)], new ScriptedRandom(0.5, 0.5), 20, 40, Unclaimed, (_, _) => false, P1).Outcome);
        // the map down still skips, as for every AroundPlayer wave (Failure 6.2)
        Assert.Equal(WaveOutcome.Skip, WaveGate.Decide(new WaveFacts(1, "rv", false, MapFailed: true, Location: LocationType.AroundPlayer, FocusOnly: true),
            () => ["CHAR_Bandit_Thug"], 20, 0, 150).Outcome);
        Assert.True(WaveFacts.IsFocusOnly(Def()));
    }

    [Fact]
    public void FocusOnly_fails_when_a_held_wave_releases_its_successor()
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Event()), FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        Assert.Null(engine.Start("rv", "Revenge", T0, new ControlState(false, true, Enum.GetValues<Pillar>().ToHashSet(), 0, 3), focus: P1));
        Assert.Equal(1, engine.NextWave("rv", T0)!.Wave);
        // the held wave is not reported: wave 1 stays the next wave, minutes later
        Assert.Equal(1, engine.NextWave("rv", T0.AddSeconds(150))!.Wave);
        engine.WaveSpawned("rv", T0.AddSeconds(150), 4);
        Assert.Equal(2, engine.NextWave("rv", T0.AddSeconds(150))!.Wave);
        // after the event's end no wave comes, held or not
        engine.Expire(T0.AddSeconds(600), 60);
        Assert.Null(engine.NextWave("rv", T0.AddSeconds(600)));
    }

    [Fact]
    public void FocusOnly_fails_when_a_non_revenge_pick_or_hunt_changes()
    {
        PickCandidate[] players = [P(0, "a"), P(1000, "b"), P(2000, P1)];
        var normal = PlayerPick.Choose(players, new ScriptedRandom(0.1, 0.5, 0.5), 20, 40, Unclaimed, null);
        Assert.InRange(normal.Centre.X, -40, 40);                              // the uniform pick, focus or not
        HuntCandidate[] hunt = [new(1, 5, 0, true, true, false, false), new(2, 10, 0, true, true, false, false)];
        Assert.Equal(HuntPlan.Targets(hunt, (0, 0), 45), HuntPlan.TargetsFor(hunt, (0, 0), 45, false, 2L));
        Assert.False(WaveFacts.IsFocusOnly(Def("fk", "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 3, \"windowSeconds\": 60 }",
            Json.RevengeAction)));
        Assert.Equal(WaveOutcome.Skip, WaveGate.Decide(new WaveFacts(1, "x", false, Location: LocationType.AroundPlayer, Pick: PickOutcome.NoEligible),
            () => ["CHAR_Bandit_Thug"], 20, 0, 150).Outcome);
    }

    [Fact]
    public void FocusOnly_passes_waves_and_hunts_the_focus()
    {
        PickCandidate[] players = [P(-500, "a"), P(500, P1)];
        var pick = PlayerPick.ChooseFocus(players, new ScriptedRandom(0.0, 0.0, 0.0), 20, 40, Unclaimed, null, P1);
        Assert.Equal((520f, 0f), (pick.Centre.X, pick.Centre.Z));
        var facts = new WaveFacts(1, "rv", false, Location: LocationType.AroundPlayer, Behaviour: BehaviourType.Hunt, Pick: pick.Outcome, FocusOnly: true);
        Assert.Equal(WaveOutcome.Spawn, WaveGate.Decide(facts, () => ["CHAR_Bandit_Thug"], 20, 0, 150).Outcome);
        HuntCandidate[] hunt = [new(1, 515, 0, true, true, false, false), new(2, 520, 0, true, true, false, false)];
        Assert.Equal([2L], HuntPlan.TargetsFor(hunt, (520, 0), 45, true, 2L));
    }

    [Fact]
    public void FocusOnly_empty_no_focus()
    {
        Assert.Equal(PickOutcome.NoEligible, PlayerPick.ChooseFocus([P(0, P1)], new ScriptedRandom(), 20, 40, Unclaimed, null, null).Outcome);
        Assert.Null(HuntPlan.FocusKey([(1L, P1)], null));
        Assert.Empty(HuntPlan.TargetsFor([new(1, 5, 0, true, true, false, false)], (0, 0), 45, true, null));
    }

    // ---- D7 Alert

    [Fact]
    public void Alert_fails_when_the_alert_names_or_locates_the_target()
    {
        var d = Def();
        var engine = new ActiveEvent(new RunningInstance(d, T0, T0.AddMinutes(15)), "Revenge", null, "76000000000000042");
        var lines = new List<string>();
        for (var pick = 0; pick < 4; pick++) lines.AddRange(RevengeRule.StartLines(d, false, pick, () => "banner").Select(l => l.Text));
        lines.Add(RevengeRule.StartPrivate(engine)!.Value.Line);
        Assert.All(lines, l => Assert.DoesNotMatch(@"76000|\d\d|<|Zebulonrex", l));
        Assert.Contains("The Bandits swear revenge on a vampire.", lines);
        Assert.Contains("The Bandits gather to hunt a vampire down.", lines);
    }

    [Fact]
    public void Alert_fails_when_a_line_exceeds_480_bytes()
    {
        var factions = "[" + string.Join(", ", Enumerable.Range(0, 5).Select(i => $"\"Faction_{new string((char)('A' + i), 22)}\"")) + "]";
        var units = new FakeUnits("CHAR_Bandit_Thug");
        foreach (var i in Enumerable.Range(0, 5)) units.Factions.Add($"Faction_{new string((char)('A' + i), 22)}");
        var d = Assert.Single(EventValidator.Parse(Json.File(Event(trigger: Trigger(factions: factions))), units, regions: FakeRegions.All()).Set.All);
        Assert.Null(d.DisabledReason);
        Assert.All(d.Trigger.Factions!, f => Assert.Equal(30, f.Length));
        var active = new ActiveEvent(new RunningInstance(d, T0, T0.AddMinutes(15)), "Revenge", null, P1);
        var lines = Enumerable.Range(0, 2).Select(p => RevengeRule.Alert(d, p))
            .Append(RevengeRule.StartPrivate(active)!.Value.Line).Append(RevengeRule.EndPrivate(active, RevengeEndPath.AdminStop)!.Value.Line)
            .Concat(Enumerable.Range(1, 3).Select(k => Messages.RevengeWarning(k, 3, RevengeRule.FactionText(d.Trigger)))).ToList();
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
        Assert.All(lines, l => Assert.Contains(new string('E', 22), l));        // no faction is cut off
    }

    [Fact]
    public void Alert_fails_when_the_start_line_reaches_another_player()
    {
        var d = Def();
        var active = new ActiveEvent(new RunningInstance(d, T0, T0.AddMinutes(15)), "Revenge", null, P1);
        var (to, line) = RevengeRule.StartPrivate(active)!.Value;
        Assert.Equal(P1, to);
        Assert.Equal("The Bandits come for you.", line);
        var told = new List<(string, string)>();
        new PlayerTriggerFeed().Tell((p, l) => told.Add((p, l)), to, line, _ => { });
        Assert.Equal([(P1, line)], told);
        // no other instance has a private start line
        var manual = Set(Json.Event("raid")).Find("raid")!;
        Assert.Null(RevengeRule.StartPrivate(new ActiveEvent(new RunningInstance(manual, T0, T0.AddMinutes(10)), "manual", null, P1)));
    }

    [Fact]
    public void Alert_passes_alert_and_private_start_line()
    {
        var d = Def();
        var off = RevengeRule.StartLines(d, false, 0, () => "The banner.");
        var on = RevengeRule.StartLines(d, true, 0, () => "The banner.");
        Assert.Equal(off, on);                                                 // whatever EventBanners says
        var line = Assert.Single(on);
        Assert.Equal((LineKind.Info, "rv", "The Bandits swear revenge on a vampire."), (line.Kind, line.EventId, line.Text));
        // any other definition keeps its banner rule
        var manual = Set(Json.Event("raid")).Find("raid")!;
        Assert.Equal(["The banner."], RevengeRule.StartLines(manual, true, 0, () => "The banner.").Select(l => l.Text));
        Assert.Empty(RevengeRule.StartLines(manual, false, 0, () => "The banner."));
    }

    [Fact]
    public void Alert_empty_one_faction()
    {
        var d = Def();
        Assert.Equal("Bandits", RevengeRule.FactionText(d.Trigger));
        Assert.Equal("Bandits, Legion", RevengeRule.FactionText(d.Trigger with { Factions = ["Faction_Bandits", "Faction_Legion"] }));
        Assert.Equal("", RevengeRule.FactionText(d.Trigger with { Factions = null }));
    }

    // ---- D8 EndPaths

    static (EventEngine Engine, EventCatalog Catalog) Running(string focus = P1)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Event()), FakeUnits.Default(), regions: FakeRegions.All()), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        Assert.Null(engine.Start("rv", "Revenge", T0, new ControlState(false, true, Enum.GetValues<Pillar>().ToHashSet(), 0, 3), focus: focus));
        return (engine, catalog);
    }

    [Fact]
    public void EndPaths_fails_when_a_line_path_sends_nothing_or_to_another_player()
    {
        var (engine, _) = Running();
        var active = engine.Find("rv")!;
        foreach (var path in new[] { RevengeEndPath.NaturalEnd, RevengeEndPath.AllWavesDefeated, RevengeEndPath.AdminStop })
            Assert.Equal((P1, "The Bandits' revenge is spent."), RevengeRule.EndPrivate(active, path));
        // the instances the engine ends carry their focus
        Assert.Equal(P1, Assert.Single(engine.Expire(T0.AddSeconds(600), 60)).Focus);
        var (stopped, _) = Running();
        Assert.Equal(P1, stopped.Cancel("rv")!.Focus);
    }

    [Fact]
    public void EndPaths_fails_when_a_silent_path_sends_a_line()
    {
        var (engine, _) = Running();
        var active = engine.Find("rv")!;
        var silent = new[] { RevengeEndPath.Purge, RevengeEndPath.Restart, RevengeEndPath.PillarOff, RevengeEndPath.Fault };
        foreach (var path in silent)
            Assert.Null(RevengeRule.EndPrivate(active, path));
        Assert.Equal([false, false, false, false], silent.Select(RevengeRule.SendsEndLine));
        Assert.Equal("revenge rv ended: fault", RevengeRule.EndedLine("rv", RevengeEndPath.Fault));
    }

    [Fact]
    public void EndPaths_fails_when_a_disable_or_invalid_reload_ends_the_instance()
    {
        var (engine, catalog) = Running();
        var disabled = EventValidator.Parse(Json.File(Event().Replace("\"enabled\": true", "\"enabled\": false")), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(catalog.Reload(disabled, FileStamp.Of(T0, [2])));
        Assert.NotNull(engine.Find("rv"));
        var invalid = EventValidator.Parse(Json.File(Event(trigger: Trigger(heat: 2))), FakeUnits.Default(), regions: FakeRegions.All());
        Assert.Null(catalog.Reload(invalid, FileStamp.Of(T0, [3])));
        var still = engine.Find("rv")!;
        Assert.Equal(6, still.Definition.Trigger.Heat);                       // the instance keeps the definition it started with
        Assert.Equal(P1, still.Focus);
    }

    [Fact]
    public void EndPaths_fails_when_heat_survives_a_purge_or_the_pillar_off()
    {
        var set = Set(Event(), Event("rv2"));
        var feed = new PlayerTriggerFeed();
        for (var i = 0; i < 6; i++) Died(feed, set, Kill(), T0.AddSeconds(i), new LogLines());
        Died(feed, set, Kill(P2), T0, new LogLines());
        feed.Scan(() => [], null, set, _ => false, T0.AddSeconds(10), _ => { }, _ => { }, null,
            new RevengeScan(() => [At(P1)], Unclaimed, _ => false, _ => RevengeStartResult.Started));
        Assert.True(feed.Heat.Cooldowns > 0 && feed.Heat.Rows > 0);
        feed.Heat.Clear();                                                     // the purge
        Assert.Equal((0, 0, 0), (feed.Heat.Rows, feed.Heat.Due, feed.Heat.Cooldowns));
        // the pillar off: the definitions it makes unstartable lose their rows; nothing is sent
        Died(feed, set, Kill(), T0.AddSeconds(20), new LogLines());
        var told = new List<(string, string)>();
        feed.Scan(() => [], null, set, _ => false, T0.AddSeconds(25), _ => { }, _ => { }, null,
            new RevengeScan(() => [At(P1)], Unclaimed, _ => false, _ => RevengeStartResult.Started, p => p != Pillar.Spawns));
        Assert.Equal(0, feed.Heat.Rows);
        Died(feed, set, Kill(), T0.AddSeconds(30), new LogLines(), told, p => p != Pillar.Spawns);
        Assert.Equal(0, feed.Heat.Rows);
        Assert.Empty(told);
        // the restart: a new feed holds nothing
        Assert.Equal(0, new PlayerTriggerFeed().Heat.Rows);
    }

    [Fact]
    public void EndPaths_fails_when_a_log_line_is_missing()
    {
        Assert.Equal(["revenge rv ended: natural end", "revenge rv ended: all waves defeated", "revenge rv ended: admin stop", "revenge rv ended: purge",
                "revenge rv ended: restart", "revenge rv ended: pillar off", "revenge rv ended: fault"],
            Enum.GetValues<RevengeEndPath>().Select(p => RevengeRule.EndedLine("rv", p)));
    }

    [Fact]
    public void EndPaths_passes_six_paths()
    {
        Assert.Equal(7, Enum.GetValues<RevengeEndPath>().Length);              // D8's six and the fault end (logged only)
        Assert.Equal(3, Enum.GetValues<RevengeEndPath>().Count(RevengeRule.SendsEndLine));
        var (engine, _) = Running();
        Assert.Equal(P1, Assert.Single(engine.CancelAll()).Focus);           // a purge ends it as every event
    }

    [Fact]
    public void EndPaths_empty_no_running_revenge()
    {
        var manual = Set(Json.Event("raid")).Find("raid")!;
        var active = new ActiveEvent(new RunningInstance(manual, T0, T0.AddMinutes(10)), "manual", null);
        Assert.All(Enum.GetValues<RevengeEndPath>(), p => Assert.Null(RevengeRule.EndPrivate(active, p)));
    }

    // ---- D13 AdminStart

    [Fact]
    public void AdminStart_fails_when_the_start_has_no_focus()
    {
        var d = Def();
        Assert.Equal("admin1", RevengeRule.AdminFocus(d, "admin1"));
        var (engine, _) = Running(RevengeRule.AdminFocus(d, "admin1")!);
        Assert.Equal("admin1", engine.Find("rv")!.Focus);
        Assert.Null(RevengeRule.AdminFocus(Set(Json.Event("raid")).Find("raid")!, "admin1"));     // no other start gains a focus
    }

    [Fact]
    public void AdminStart_fails_when_a_wave_is_centred_on_another_player()
    {
        PickCandidate[] players = [P(-500, P1), P(500, "admin1")];
        var pick = PlayerPick.ChooseFocus(players, new ScriptedRandom(0.0, 0.0, 0.0), 20, 40, Unclaimed, null, RevengeRule.AdminFocus(Def(), "admin1"));
        Assert.Equal(520f, pick.Centre.X);
    }

    [Fact]
    public void AdminStart_fails_when_a_players_heat_changes()
    {
        var set = Set(Event());
        var feed = new PlayerTriggerFeed();
        for (var i = 0; i < 4; i++) Died(feed, set, Kill(), T0.AddSeconds(i), new LogLines());
        for (var i = 0; i < 2; i++) Died(feed, set, Kill("admin1"), T0.AddSeconds(i), new LogLines());
        Running(RevengeRule.AdminFocus(set.Find("rv")!, "admin1")!);         // the admin start touches no heat
        Assert.Equal((4, 2), (feed.Heat.HeatOf("rv", P1), feed.Heat.HeatOf("rv", "admin1")));
        Assert.False(feed.Heat.InCooldown("rv", "admin1", T0));
    }

    [Fact]
    public void AdminStart_fails_when_a_non_admin_start_runs()
    {
        Assert.False(ActionTable.Allows(ActionKind.StartEvent, Actor.Player, true));
        Assert.True(ActionTable.Allows(ActionKind.StartEvent, Actor.Admin, true));
    }

    [Fact]
    public void AdminStart_passes_focused_on_the_admin()
    {
        var d = Def();
        var (engine, _) = Running(RevengeRule.AdminFocus(d, "admin1")!);
        var active = engine.Find("rv")!;
        Assert.Equal(("admin1", "The Bandits come for you."), RevengeRule.StartPrivate(active));
        Assert.Single(RevengeRule.StartLines(d, false, 0, () => "banner"));
    }

    [Fact]
    public void AdminStart_empty_no_revenge_definition()
    {
        var manual = Set(Json.Event("raid")).Find("raid")!;
        Assert.Null(RevengeRule.AdminFocus(manual, "admin1"));
        Assert.Matches(new Regex("^revenge: 0 heat rows, 0 due, 0 running$"), RevengeRule.CountsLine(0, 0, 0));
    }
}
