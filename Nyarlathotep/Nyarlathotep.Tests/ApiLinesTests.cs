using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D1 and D2: the `api status` and `api events` rows.</summary>
public class ApiLinesTests
{
    static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    static readonly SpawnWavesAction Action =
        new([new UnitEntry("CHAR_Bandit_Thug", 5)], 3, 60, 10, new Location(LocationType.Point, -1200.5f, -800f), null);

    static EventDefinition Def(string id, TriggerType trigger = TriggerType.Manual, bool enabled = true, string? reason = null,
        Pillar pillar = Pillar.Spawns, string name = "Bandit raid", bool noAction = false) =>
        new(id, name, enabled, pillar, new Trigger(trigger, [], [], DayPhase.Night, []), new Conditions(), 600,
            noAction ? null : Action, Announce.None, reason);

    static ActiveEvent Running(EventDefinition d, int waves, int secondsLeft) =>
        new(new RunningInstance(d, Now.AddSeconds(-30), Now.AddSeconds(secondsLeft)), "manual", null) { WavesSpawned = waves };

    static readonly string[] EventKeys = ["id", "kind", "name", "state", "faction", "left", "wave", "units"];
    static readonly string[] DefKeys = ["id", "name", "enabled", "trigger", "action", "duration", "state", "reason"];

    static List<string> Keys(string line) => line.Split(' ').Skip(1).Select(t => t[..t.IndexOf('=')]).ToList();

    static string Value(string line, string key) => Regex.Match(line, $" {key}=([^ ]+)").Groups[1].Value;

    static IReadOnlyList<string> Status(IEnumerable<ActiveEvent> active, IEnumerable<Cleanup> cleanups, DefinitionSet set,
        bool isAdmin, Dictionary<string, int>? units = null) =>
        ApiLines.Status(active, cleanups, set, units ?? new Dictionary<string, int>(), isAdmin, Now);

    [Fact]
    public void No_events_is_the_end_line_alone()
    {
        var reply = Status([], [], DefinitionSet.Empty, isAdmin: false);
        Assert.Equal(["[NYAR:end] cmd=status count=0"], reply);
    }

    [Fact]
    public void An_active_row_carries_the_contract_keys_in_order()
    {
        var d = Def("ashfall", name: "Ashfall Raid");
        var reply = Status([Running(d, 2, 412)], [], new DefinitionSet([d]), isAdmin: true, new() { ["ashfall"] = 18 });
        Assert.Equal("[NYAR:event] id=ashfall kind=waves name=Ashfall_Raid state=active faction=- left=412 wave=2/3 units=18", reply[0]);
        Assert.Equal(EventKeys, Keys(reply[0]));
        Assert.Equal("[NYAR:end] cmd=status count=1", reply[^1]);
    }

    [Fact]
    public void A_player_gets_no_unit_count()
    {
        var d = Def("ashfall");
        var reply = Status([Running(d, 1, 100)], [new Cleanup("old", Now, Now.AddSeconds(20))], new DefinitionSet([d]),
            isAdmin: false, new() { ["ashfall"] = 18, ["old"] = 4 });
        foreach (var row in reply.Take(reply.Count - 1)) Assert.Equal("-", Value(row, "units"));
    }

    [Fact]
    public void An_admin_with_no_tracked_units_sees_0()
    {
        var d = Def("ashfall");
        var reply = Status([Running(d, 0, 100)], [], new DefinitionSet([d]), isAdmin: true);
        Assert.Equal("0", Value(reply[0], "units"));
    }

    [Fact]
    public void An_ended_event_waiting_out_its_grace_has_an_ending_row()
    {
        var d = Def("ashfall", name: "Ashfall Raid");
        var cleanups = new[] { new Cleanup("ashfall", Now.AddSeconds(-5), Now.AddSeconds(40)), new Cleanup("ashfall", Now.AddSeconds(-2), Now.AddSeconds(55)) };
        var reply = Status([], cleanups, new DefinitionSet([d]), isAdmin: true, new() { ["ashfall"] = 7 });
        Assert.Equal(2, reply.Count);
        Assert.Equal("[NYAR:event] id=ashfall kind=waves name=Ashfall_Raid state=ending faction=- left=55 wave=- units=7", reply[0]);
        Assert.Equal("[NYAR:end] cmd=status count=1", reply[1]);
    }

    [Fact]
    public void An_event_active_again_shows_only_its_active_row()
    {
        var d = Def("ashfall");
        var reply = Status([Running(d, 1, 300)], [new Cleanup("ashfall", Now.AddSeconds(-5), Now.AddSeconds(40))],
            new DefinitionSet([d]), isAdmin: false);
        Assert.Equal(2, reply.Count);
        Assert.Equal("active", Value(reply[0], "state"));
    }

    [Fact]
    public void An_ending_row_whose_definition_was_removed_uses_its_id()
    {
        var reply = Status([], [new Cleanup("gone", Now, Now.AddSeconds(10))], DefinitionSet.Empty, isAdmin: false);
        Assert.Equal("[NYAR:event] id=gone kind=waves name=gone state=ending faction=- left=10 wave=- units=-", reply[0]);
    }

    [Fact]
    public void Rows_are_ordered_and_count_matches_the_rows_sent()
    {
        var a = Def("b-raid"); var b = Def("a-raid");
        var reply = Status([Running(a, 1, 50), Running(b, 1, 50)],
            [new Cleanup("z-old", Now, Now.AddSeconds(5)), new Cleanup("c-old", Now, Now.AddSeconds(5))],
            new DefinitionSet([a, b]), isAdmin: false);
        Assert.Equal(["a-raid", "b-raid", "c-old", "z-old"], reply.Take(4).Select(r => Value(r, "id")));
        Assert.Equal("[NYAR:end] cmd=status count=4", reply[^1]);
    }

    [Fact]
    public void A_past_due_time_is_0_seconds_left()
    {
        Assert.Equal(0, ApiLines.SecondsLeft(Now.AddSeconds(-3), Now));
        Assert.Equal(1, ApiLines.SecondsLeft(Now.AddMilliseconds(200), Now));
    }

    [Theory]
    [InlineData(Pillar.Empowerment, "empower")]
    [InlineData(Pillar.Spawns, "waves")]
    [InlineData(Pillar.Boss, "boss")]
    [InlineData(Pillar.Zones, "zone")]
    [InlineData(Pillar.Sieges, "siege")]
    public void Every_pillar_maps_to_a_contract_kind(Pillar pillar, string kind) => Assert.Equal(kind, ApiLines.Kind(pillar));

    [Fact]
    public void No_status_row_carries_a_position_or_a_player()
    {
        var d = Def("ashfall");
        var reply = Status([Running(d, 1, 100)], [new Cleanup("old", Now, Now.AddSeconds(20))], new DefinitionSet([d]), isAdmin: true);
        foreach (var row in reply)
            foreach (var k in Keys(row))
                Assert.DoesNotContain(k, new[] { "x", "y", "z", "r", "player", "steam", "owner" });
    }

    [Theory]
    [InlineData(TriggerType.Manual, "manual")]
    [InlineData(TriggerType.Schedule, "schedule")]
    [InlineData(TriggerType.GameTime, "ingame")]
    [InlineData(TriggerType.VBloodKilled, "vbloodkilled")]
    public void Every_trigger_maps_to_a_contract_name(TriggerType t, string name) => Assert.Equal(name, ApiLines.Trigger(t));

    [Fact]
    public void Definition_rows_carry_the_contract_keys_and_the_four_states()
    {
        var active = Def("a-active", TriggerType.Schedule);
        var disabled = Def("b-disabled", TriggerType.Schedule, reason: "units: CHAR_X is not a known unit");
        var off = Def("c-off", enabled: false);
        var scheduled = Def("d-scheduled", TriggerType.VBloodKilled);
        var idle = Def("e-idle");
        var rows = ApiLines.Definitions(new DefinitionSet([idle, scheduled, off, disabled, active]), new HashSet<string> { "a-active" });

        Assert.Equal(5, rows.Count);
        foreach (var r in rows) Assert.Equal(DefKeys, Keys(r));
        Assert.Equal(["active", "disabled", "disabled", "scheduled", "idle"], rows.Select(r => Value(r, "state")));
        Assert.Equal("[NYAR:def] id=b-disabled name=Bandit_raid enabled=1 trigger=schedule action=waves duration=600 state=disabled reason=units_CHAR_X_is_not_a_known_unit", rows[1]);
        Assert.Equal("[NYAR:def] id=c-off name=Bandit_raid enabled=0 trigger=manual action=waves duration=600 state=disabled reason=disabled", rows[2]);
        Assert.Equal("-", Value(rows[0], "reason"));
        Assert.Equal("-", Value(rows[4], "reason"));
    }

    [Fact]
    public void A_disabled_reason_never_holds_a_forbidden_character()
    {
        var d = Def("bad", reason: "location.x: must be a number; got \"a=b\" <b>here</b>\n");
        var row = Assert.Single(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string>()));
        var reason = Value(row, "reason");
        Assert.NotEqual("-", reason);
        Assert.True(reason.IndexOfAny([' ', '=', ';', ':', '<', '>', '\n']) < 0, reason);
    }

    [Fact]
    public void A_running_event_since_switched_off_is_active_with_no_reason()
    {
        var d = Def("raid", enabled: false);
        var row = Assert.Single(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { "raid" }));
        Assert.Equal("active", Value(row, "state"));
        Assert.Equal("-", Value(row, "reason"));
    }

    [Fact]
    public void A_duplicate_id_row_stays_disabled_while_the_first_runs()
    {
        var first = Def("raid");
        var dup = Def("raid", reason: "id: duplicate of an earlier event");
        var rows = ApiLines.Definitions(new DefinitionSet([first, dup]), new HashSet<string> { "raid" });
        Assert.Equal(["active", "disabled"], rows.Select(r => Value(r, "state")));
        Assert.Equal("id_duplicate_of_an_earlier_event", Value(rows[1], "reason"));
    }

    [Theory]
    [InlineData(Pillar.Spawns, "waves")]
    [InlineData(Pillar.Empowerment, "empower")]
    [InlineData(Pillar.Boss, "boss")]
    [InlineData(Pillar.Sieges, "siege")]
    [InlineData(Pillar.Zones, "waves")]
    public void A_definition_without_an_action_still_names_a_contract_action(Pillar pillar, string action)
    {
        var d = Def("broken", pillar: pillar, reason: "action: missing", noAction: true);
        var row = Assert.Single(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string>()));
        Assert.Equal($"[NYAR:def] id=broken name=Bandit_raid enabled=1 trigger=manual action={action} duration=600 state=disabled reason=action_missing", row);
    }

    [Fact]
    public void A_cleanup_already_due_has_no_ending_row()
    {
        var d = Def("ashfall");
        var set = new DefinitionSet([d]);
        Assert.Equal(["[NYAR:end] cmd=status count=0"], Status([], [new Cleanup("ashfall", Now.AddSeconds(-60), Now)], set, isAdmin: false));
        var mixed = Status([], [new Cleanup("ashfall", Now.AddSeconds(-60), Now.AddSeconds(30)), new Cleanup("ashfall", Now.AddSeconds(-90), Now.AddSeconds(-1))],
            set, isAdmin: false);
        Assert.Equal(2, mixed.Count);
        Assert.Equal("30", Value(mixed[0], "left"));
    }
}
