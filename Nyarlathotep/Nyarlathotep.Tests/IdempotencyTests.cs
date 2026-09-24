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

    static FileStamp Stamp(string text, int second = 0) =>
        FileStamp.Of(Zones.Utc(2026, 9, 24, 20, 0).AddSeconds(second), System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Reload_twice_gives_one_definition_set()
    {
        var text = Json.File(Json.Event("a"), Json.Event("b"));
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(text, FakeUnits.Default()), Stamp(text)));
        var first = catalog.Current;
        Assert.Null(catalog.Reload(EventValidator.Parse(text, FakeUnits.Default()), Stamp(text)));
        Assert.Equal(2, catalog.Current.All.Count);
        Assert.Equal(first.All.Select(d => d.Id), catalog.Current.All.Select(d => d.Id));
    }

    [Fact]
    public void A_rejected_reload_keeps_the_last_valid_set()
    {
        var text = Json.File(Json.Event("a"));
        var catalog = new EventCatalog();
        catalog.Reload(EventValidator.Parse(text, FakeUnits.Default()), Stamp(text));
        var error = catalog.Reload(EventValidator.Parse("{ broken", FakeUnits.Default()), Stamp("{ broken", 5));
        Assert.StartsWith("events.json rejected: line 1", error);
        Assert.NotNull(catalog.Current.Find("a"));
        Assert.Equal(Stamp(text), catalog.LoadedStamp);
    }

    [Fact]
    public void A_trigger_from_the_same_source_within_5_s_fires_once()
    {
        var d = new TriggerDedupe();
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        Assert.True(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t));
        Assert.False(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t.AddSeconds(4.9)));
        Assert.False(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t.AddSeconds(5)));
        Assert.True(d.ShouldFire("vblood:CHAR_Other", t.AddSeconds(1)));
        Assert.True(d.ShouldFire("vblood:CHAR_Bandit_Tourok_VBlood", t.AddSeconds(5.1)));
    }

    [Fact]
    public void A_reload_keeps_running_instances_on_their_snapshot()
    {
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        var text = Json.File(Json.Event());
        var catalog = new EventCatalog();
        catalog.Reload(EventValidator.Parse(text, FakeUnits.Default()), Stamp(text));
        Assert.Null(catalog.TryStart("raid", t, out var running));

        var edited = Json.File(Json.Event().Replace("\"durationSeconds\": 600", "\"durationSeconds\": 60"));
        Assert.Null(catalog.Reload(EventValidator.Parse(edited, FakeUnits.Default()), Stamp(edited, 1)));

        Assert.Equal(60, catalog.Current.Find("raid")!.DurationSeconds);
        var still = Assert.Single(catalog.Running);
        Assert.Same(running, still);
        Assert.Equal(600, still.Definition.DurationSeconds);
        Assert.Equal(t.AddSeconds(600), still.EndsUtc);
        Assert.Equal("already active", catalog.TryStart("raid", t.AddSeconds(2), out _));

        Assert.Null(catalog.TryEnd("raid"));
        Assert.Null(catalog.TryStart("raid", t.AddSeconds(3), out var next));
        Assert.Equal(60, next!.Definition.DurationSeconds);
    }

    [Fact]
    public void Catalog_start_and_stop_replies()
    {
        var text = Json.File(Json.Event());
        var catalog = new EventCatalog();
        catalog.Reload(EventValidator.Parse(text, FakeUnits.Default()), Stamp(text));
        var t = Zones.Utc(2026, 9, 24, 20, 0);
        Assert.Equal("unknown event nope", catalog.TryStart("nope", t, out _));
        Assert.Equal("not active", catalog.TryEnd("raid"));
        Assert.Null(catalog.TryStart("raid", t, out _));
        Assert.Equal("already active", catalog.TryStart("raid", t, out _));
        Assert.Single(catalog.Running);
    }

    [Fact]
    public void Writes_refuse_when_the_file_changed_on_disk()
    {
        var text = Json.File(Json.Event());
        var loaded = Stamp(text);
        Assert.Null(StaleFile.CheckWritable(loaded, Stamp(text)));
        Assert.Equal("events.json changed on disk, run .nyar event reload first", StaleFile.CheckWritable(loaded, Stamp(text, 1)));
        Assert.Equal(StaleFile.Refusal, StaleFile.CheckWritable(null, loaded));
    }

    [Fact]
    public void An_edit_that_keeps_the_write_time_is_still_refused()
    {
        var text = Json.File(Json.Event());
        var sameLength = text.Replace("Bandit raid", "Bandit_raid");
        Assert.Equal(text.Length, sameLength.Length);
        Assert.Equal(StaleFile.Refusal, StaleFile.CheckWritable(Stamp(text), Stamp(sameLength)));
    }
}
