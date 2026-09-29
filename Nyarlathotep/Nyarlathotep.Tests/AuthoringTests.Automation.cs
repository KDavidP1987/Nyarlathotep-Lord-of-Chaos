using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>automation D16: `.nyar event set` for the new trigger fields and action.fanOut, each a whole-file edit through
/// the event-library editor with one .bak and a reload; refused with its rule (the file unchanged) out of range, as a field
/// of another trigger type, or as a fanOut on an event whose location is not AroundPlayer; `event info` shows the next
/// start of an Interval definition.</summary>
public partial class AuthoringTests
{
    const string IntervalTrigger = "{ \"type\": \"Interval\", \"minMinutes\": 60, \"maxMinutes\": 90 }";
    const string KillsTrigger = "{ \"type\": \"FactionKills\", \"factions\": [\"Faction_Bandits\"], \"kills\": 20, \"windowSeconds\": 300 }";
    static readonly string AroundPlayerAction = Json.ValidAction.Replace("{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }", "{ \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }");

    static Library AutomationLib() => Lib(Json.Event("raid"), Json.Event("tick", trigger: IntervalTrigger), Json.Event("kills", trigger: KillsTrigger),
        Json.Empower("surge"));

    [Theory]
    [InlineData("tick", "trigger.minMinutes", "4", EventValidator.MinMinutesRule)]
    [InlineData("tick", "trigger.maxMinutes", "1441", EventValidator.MaxMinutesRule)]
    [InlineData("kills", "trigger.kills", "501", EventValidator.KillsRule)]
    [InlineData("kills", "trigger.windowSeconds", "9", EventValidator.KillWindowRule)]
    [InlineData("kills", "trigger.shared", "1", EventValidator.SharedRule)]
    [InlineData("kills", "trigger.factions", "Bandits", "trigger.factions names must be CHAR_ or Faction_ then A-Za-z0-9_, at most 96 characters")]
    [InlineData("raid", "trigger.type", "Hourly", CommandArgs.TriggerTypeRule)]
    [InlineData("raid", "action.fanOut", "1 150", EventValidator.FanOutInstancesRule)]
    public void Automation_fails_when_value_refused(string id, string field, string value, string reply)
    {
        var lib = AutomationLib();
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, id, field, value)));
    }

    [Theory]
    [InlineData("raid", "trigger.minMinutes", "30", "trigger.minMinutes needs an Interval trigger")]
    [InlineData("kills", "trigger.maxMinutes", "30", "trigger.maxMinutes needs an Interval trigger")]
    [InlineData("tick", "trigger.playerCooldownMinutes", "10", "trigger.playerCooldownMinutes needs a RegionEntered trigger")]
    [InlineData("tick", "trigger.kills", "10", "trigger.kills needs a FactionKills trigger")]
    [InlineData("tick", "trigger.factions", "Faction_Bandits", "trigger.factions needs a FactionKills trigger")]
    [InlineData("raid", "trigger.windowSeconds", "60", "trigger.windowSeconds needs a FactionKills trigger")]
    [InlineData("raid", "trigger.shared", "true", "trigger.shared needs a FactionKills trigger")]
    [InlineData("kills", "trigger.days", "Sat", "trigger.days needs a Schedule trigger")]
    public void Automation_fails_when_other_trigger_type(string id, string field, string value, string reply)
    {
        var lib = AutomationLib();
        Unchanged(lib, () => Assert.Equal(reply, Set(lib, id, field, value)));
    }

    [Fact]
    public void Automation_fails_when_fanout_on_point_or_empower()
    {
        // a fanned-out event cannot be moved off AroundPlayer either (round 2 F4)
        var fanned = Lib(Json.Event("hunt", action: AroundPlayerAction.TrimEnd('}') + ", \"fanOut\": { \"maxInstances\": 3, \"minSpacing\": 150 } }"));
        Unchanged(fanned, () => Assert.Equal($"{EventValidator.FanOutLocation}: set action.fanOut none first",
            Set(fanned, "hunt", "location", "here", (10f, 0f, 10f))));
        var lib = AutomationLib();
        Unchanged(lib, () => Assert.Equal(EventValidator.FanOutLocation, Set(lib, "raid", "action.fanOut", "3 150")));
        Unchanged(lib, () => Assert.Equal("action.fanOut is not an Empower field", Set(lib, "surge", "action.fanOut", "3 150")));
        Assert.Null(ActionOf(lib, "raid")["fanOut"]);
    }

    [Fact]
    public void Automation_passes_valid_set_changes_file()
    {
        var lib = AutomationLib();
        void Changed(string id, string field, string value, string reply)
        {
            var (hash, bak) = (lib.Hash, lib.Bak);
            Assert.Equal(reply, Set(lib, id, field, value));
            Assert.NotEqual(hash, lib.Hash);
            Assert.NotEqual(bak, lib.Bak);                                     // one .bak per write: the file before it
        }
        EventDefinition Loaded(string id) => lib.Catalog.Current.Find(id)!;

        Changed("tick", "trigger.minMinutes", "30", "event tick trigger.minMinutes = 30");
        Changed("tick", "trigger.maxMinutes", "45", "event tick trigger.maxMinutes = 45");
        Assert.Equal((30, 45), (Loaded("tick").Trigger.MinMinutes, Loaded("tick").Trigger.MaxMinutes));
        Assert.Equal("interval 30-45 min", EventLines.Trigger(Loaded("tick").Trigger));
        Assert.Equal("event tick trigger.minMinutes = 50; now disabled: " + EventValidator.IntervalOrder, Set(lib, "tick", "trigger.minMinutes", "50"));

        Changed("kills", "trigger.kills", "8", "event kills trigger.kills = 8");
        Changed("kills", "trigger.windowSeconds", "120", "event kills trigger.windowSeconds = 120");
        Changed("kills", "trigger.shared", "true", "event kills trigger.shared = true");
        Assert.Equal("factionkills Bandits 8 in 120s shared", EventLines.Trigger(Loaded("kills").Trigger));

        Assert.Equal("event raid action.location = aroundplayer 20 40", Set(lib, "raid", "location", "aroundplayer 20 40"));
        Changed("raid", "action.fanOut", "3 150", "event raid action.fanOut = 3 150");
        Assert.Equal("{\"maxInstances\":3,\"minSpacing\":150}", ActionOf(lib, "raid")["fanOut"]!.ToJsonString());
        Assert.Equal(new FanOut(3, 150), Loaded("raid").Action!.FanOut);
        Changed("raid", "action.fanOut", "none", "event raid action.fanOut = none");
        Assert.Null(ActionOf(lib, "raid")["fanOut"]);

        Changed("raid", "trigger.type", "Interval", "event raid trigger.type = Interval");
        Assert.Equal("interval 60-90 min", EventLines.Trigger(Loaded("raid").Trigger));
        Assert.Equal(["type", "minMinutes", "maxMinutes"], Entry(lib.Text, "raid")["trigger"]!.AsObject().Select(p => p.Key));
        Changed("raid", "trigger.type", "FactionKills", "event raid trigger.type = FactionKills");
        Assert.Equal("factionkills Bandits 20 in 300s", EventLines.Trigger(Loaded("raid").Trigger));
        // a RegionEntered trigger has no scope until one is set, so the reload disables it with its rule
        Assert.Equal("event raid trigger.type = RegionEntered; now disabled: " + EventValidator.RegionEnteredScopeRule,
            Set(lib, "raid", "trigger.type", "RegionEntered"));
        Assert.Equal(30, (int)Entry(lib.Text, "raid")["trigger"]!["playerCooldownMinutes"]!);
    }

    [Fact]
    public void Automation_passes_info_shows_next_start()
    {
        var lib = Lib(Json.Event("tick", trigger: IntervalTrigger, action: AroundPlayerAction));
        var d = lib.Catalog.Current.Find("tick")!;
        Assert.Null(d.DisabledReason);
        Assert.Contains("next start in 75 min", EventLines.Info(d, null, Now, Now.AddMinutes(74).AddSeconds(30)));
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "interval", null);
        Assert.Contains("next start: after the running instance ends", EventLines.Info(d, active, Now, null));
        Assert.Equal("next start in 0 min", EventLines.NextStartLine(false, Now.AddMinutes(-5), Now));
        Assert.Contains(EventLines.Info(d, null, Now), l => l.Contains("trigger interval 60-90 min", StringComparison.Ordinal));
        // a disabled definition never promises a next start, running or not (round 2 F5)
        Assert.DoesNotContain(EventLines.Info(d with { Enabled = false }, active, Now, null), l => l.StartsWith("next start", StringComparison.Ordinal));
        // another type never shows a next start, even with one passed
        var kills = Lib(Json.Event("kills", trigger: KillsTrigger)).Catalog.Current.Find("kills")!;
        Assert.DoesNotContain(EventLines.Info(kills, null, Now, Now.AddMinutes(5)), l => l.StartsWith("next start", StringComparison.Ordinal));
    }

    [Fact]
    public void Automation_empty_value()
    {
        var lib = AutomationLib();
        foreach (var field in new[] { "trigger.minMinutes", "trigger.kills", "trigger.factions", "trigger.shared" })
            Unchanged(lib, () => Assert.StartsWith(field + " must be", Set(lib, field == "trigger.minMinutes" ? "tick" : "kills", field, "")));
        Unchanged(lib, () => Assert.Equal("value required", Set(lib, "raid", "action.fanOut", "")));
        // an Interval definition with no next drawn yet shows no next-start line
        Assert.DoesNotContain(EventLines.Info(lib.Catalog.Current.Find("tick")!, null, Now), l => l.StartsWith("next start", StringComparison.Ordinal));
    }
}
