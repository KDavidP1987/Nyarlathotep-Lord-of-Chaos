using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D6: repeated actions have one effect.</summary>
public class IdempotencyTests
{
    [Fact]
    public void Start_on_an_active_event_replies_already_active()
    {
        var g = new InstanceGuard();
        Assert.True(g.TryBegin("raid", out _));
        Assert.False(g.TryBegin("raid", out var reply));
        Assert.Equal("already active", reply);
        Assert.Equal(1, g.Count);
    }

    [Fact]
    public void Two_admins_starting_one_event_produce_one_instance()
    {
        var g = new InstanceGuard();
        var results = new[] { g.TryBegin("raid", out _), g.TryBegin("raid", out _) };
        Assert.Equal(1, results.Count(r => r));
        Assert.Single(g.Active);
    }

    [Fact]
    public void Stop_on_an_inactive_event_replies_not_active()
    {
        var g = new InstanceGuard();
        Assert.False(g.TryEnd("raid", out var reply));
        Assert.Equal("not active", reply);
        g.TryBegin("raid", out _);
        Assert.True(g.TryEnd("raid", out _));
        Assert.False(g.TryEnd("raid", out _));
    }

    [Fact]
    public void Second_purge_confirm_is_nothing_to_purge()
    {
        var p = new PurgeArming();
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        p.Arm(1, t);
        Assert.Equal(PurgeConfirmResult.Purge, p.Confirm(1, t.AddSeconds(5), anythingToPurge: true));
        Assert.Equal(PurgeConfirmResult.NothingToPurge, p.Confirm(1, t.AddSeconds(6), anythingToPurge: false));
    }

    [Fact]
    public void Confirm_without_arming_or_after_30_s_is_refused()
    {
        var p = new PurgeArming();
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        Assert.Equal(PurgeConfirmResult.NotArmed, p.Confirm(1, t, true));
        p.Arm(1, t);
        Assert.Equal(PurgeConfirmResult.NotArmed, p.Confirm(1, t.AddSeconds(31), true));
    }

    [Fact]
    public void Arming_is_per_admin_and_consumed_by_a_confirm()
    {
        var p = new PurgeArming();
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        p.Arm(1, t);
        Assert.Equal(PurgeConfirmResult.NotArmed, p.Confirm(2, t.AddSeconds(1), true));
        Assert.Equal(PurgeConfirmResult.Purge, p.Confirm(1, t.AddSeconds(2), true));
        Assert.Equal(PurgeConfirmResult.NotArmed, p.Confirm(1, t.AddSeconds(3), true));
    }

    [Fact]
    public void Reload_twice_gives_one_definition_set()
    {
        var text = Json.File(Json.Event("a"), Json.Event("b"));
        var first = EventValidator.Parse(text, FakeUnits.Default()).Set;
        var second = EventValidator.Parse(text, FakeUnits.Default()).Set;
        Assert.Equal(first.All.Select(d => d.Id), second.All.Select(d => d.Id));
        Assert.Equal(2, second.All.Count);
        Assert.Equal(first.All, second.All, (x, y) => x.Id == y.Id && x.DurationSeconds == y.DurationSeconds && x.Enabled == y.Enabled);
    }

    [Fact]
    public void A_trigger_from_the_same_source_within_5_s_fires_once()
    {
        var d = new TriggerDedupe();
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        Assert.True(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t));
        Assert.False(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t.AddSeconds(4.9)));
        Assert.True(d.ShouldFire("vblood:CHAR_Other", t.AddSeconds(1)));
        Assert.True(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t.AddSeconds(10)));
    }

    [Fact]
    public void A_reload_keeps_running_instances_on_their_snapshot()
    {
        var before = EventValidator.Parse(Json.File(Json.Event()), FakeUnits.Default()).Set;
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        var running = new RunningInstance(before.Find("raid")!, t, t.AddSeconds(600));

        var edited = Json.Event().Replace("\"durationSeconds\": 600", "\"durationSeconds\": 60");
        var after = EventValidator.Parse(Json.File(edited), FakeUnits.Default()).Set;

        Assert.Equal(60, after.Find("raid")!.DurationSeconds);
        Assert.Equal(600, running.Definition.DurationSeconds);
        Assert.Equal(t.AddSeconds(600), running.EndsUtc);
    }

    [Fact]
    public void Writes_refuse_when_the_file_changed_on_disk()
    {
        var loaded = Zones.Utc(2026, 9, 24, 20, 0);
        Assert.Null(StaleFile.CheckWritable(loaded, loaded));
        Assert.Equal("events.json changed on disk, run .nyar event reload first", StaleFile.CheckWritable(loaded, loaded.AddSeconds(1)));
        Assert.Equal(StaleFile.Refusal, StaleFile.CheckWritable(null, loaded));
    }
}
