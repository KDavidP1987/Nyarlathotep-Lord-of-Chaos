using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D6: each transition queues one `[NYAR:ev]` line; wave-warn follows the chat warning's
/// gates (S-1); config-changed collapses; the queue holds 50 and drops the oldest with one log line per overflow
/// streak; a tick sends at most 5 lines to each connected subscriber; no push line carries a coordinate.</summary>
public class PushTests
{
    static readonly DateTime T0 = new(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
    static readonly int[] Offsets = [300, 60, 10];

    static EventDefinition Raid(string id = "raid", bool warnings = true) => new(
        id, "Bandit raid", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 600,
        new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 5)], 3, 120, 10, new Location(LocationType.Point, -1520.5f, -460f), null),
        new Announce([], [], warnings));

    static ActiveEvent Running(EventDefinition def, int wavesSpawned = 1) =>
        new(new RunningInstance(def, T0, T0.AddSeconds(600)), "manual", (-1520.5f, 0f, -460f)) { WavesSpawned = wavesSpawned };

    static (PushHub Hub, FakeUsers Users, LogLines Log) New()
    {
        var log = new LogLines();
        var users = new FakeUsers();
        return (new PushHub(users, Offsets, log.Add), users, log);
    }

    static List<string> Texts(PushHub hub) => hub.Queue.Lines.Select(l => l.Text).ToList();

    // ---- one line per transition ----

    [Fact]
    public void Each_transition_queues_one_line()
    {
        var (hub, _, _) = New();
        hub.EventStarted(Running(Raid()).Instance);
        hub.Wave("raid", 2);
        hub.EventEnded("raid");
        hub.Purged(300);
        hub.ConfigChanged();
        Assert.Equal(
        [
            "[NYAR:ev] type=event-start id=raid secs=600",
            "[NYAR:ev] type=wave id=raid secs=0 wave=2",
            "[NYAR:ev] type=event-end id=raid secs=0",
            "[NYAR:ev] type=killswitch id=- secs=300",
            "[NYAR:ev] type=config-changed id=- secs=0",
        ], Texts(hub));
    }

    [Fact]
    public void A_waiting_config_changed_absorbs_a_new_one()
    {
        var (hub, _, _) = New();
        hub.ConfigChanged();
        hub.Wave("raid", 1);
        hub.ConfigChanged();
        Assert.Equal(2, hub.Queue.Count);
        Assert.Single(hub.Queue.Lines, l => l.Type == PushLines.ConfigChangedType);
        hub.Queue.Take();
        hub.ConfigChanged();                                      // sent, so the next one queues
        Assert.Equal(1, hub.Queue.Count);
    }

    // ---- the queue ----

    [Fact]
    public void The_51st_line_drops_the_oldest_with_one_log_line_per_streak()
    {
        var (hub, _, log) = New();
        for (var i = 1; i <= 60; i++) hub.Wave("raid", i);
        Assert.Equal(PushQueue.Capacity, hub.Queue.Count);
        Assert.Equal("[NYAR:ev] type=wave id=raid secs=0 wave=11", hub.Queue.Lines[0].Text);
        Assert.Equal(1, log.Count("push queue full, oldest dropped"));

        hub.Queue.Take();                                         // the streak ends when lines leave
        for (var i = 61; i <= 70; i++) hub.Wave("raid", i);
        Assert.Equal(2, log.Count("push queue full, oldest dropped"));
    }

    [Fact]
    public void A_tick_sends_at_most_5_lines_to_each_of_128_subscribers()
    {
        var (hub, users, _) = New();
        users.Online.Clear();
        for (ulong id = 1; id <= Subscriptions.Capacity; id++)
        {
            users.Online.Add(id);
            hub.Subscribe(id);
        }
        for (var i = 1; i <= 12; i++) hub.Wave("raid", i);
        Assert.Equal(640, hub.Tick(T0, [], waveWarnings: false));
        Assert.Equal(640, users.Sent.Count);
        Assert.Equal(5, users.Sent.Select(s => s.Text).Distinct().Count());
        Assert.Equal(7, hub.Queue.Count);
        Assert.Equal("[NYAR:ev] type=wave id=raid secs=0 wave=1", users.Sent[0].Text);   // oldest first
    }

    [Fact]
    public void Nothing_reaches_an_unsubscribed_player()
    {
        var (hub, users, _) = New();
        hub.Subscribe(2);
        hub.Wave("raid", 1);
        hub.Tick(T0, [], waveWarnings: false);
        Assert.Equal(new[] { 2UL }, users.Sent.Select(s => s.Id));
    }

    // ---- wave-warn under S-1 ----

    /// <summary>Ticks once a second from T0 past the second wave (due T0+120); what was pushed is on the recipients.</summary>
    static void TickPastWave2(PushHub hub, ActiveEvent active, bool waveWarnings)
    {
        for (var s = 0; s <= 125; s++) hub.Tick(T0.AddSeconds(s), [active], waveWarnings);
    }

    [Fact]
    public void Wave_warn_is_pushed_once_per_offset_when_the_chat_warning_would_fire()
    {
        var (hub, users, _) = New();
        hub.Subscribe(1);
        TickPastWave2(hub, Running(Raid()), waveWarnings: true);
        Assert.Equal(["[NYAR:ev] type=wave-warn id=raid secs=60 wave=2", "[NYAR:ev] type=wave-warn id=raid secs=10 wave=2"],
            users.Sent.Select(s => s.Text));
    }

    [Fact]
    public void Wave_warn_is_not_pushed_with_WaveWarnings_off()
    {
        var (hub, users, _) = New();
        hub.Subscribe(1);
        TickPastWave2(hub, Running(Raid()), waveWarnings: false);
        Assert.Empty(users.Sent);
    }

    [Fact]
    public void Wave_warn_is_not_pushed_when_the_definition_turns_warnings_off()
    {
        var (hub, users, _) = New();
        hub.Subscribe(1);
        TickPastWave2(hub, Running(Raid(warnings: false)), waveWarnings: true);
        Assert.Empty(users.Sent);
    }

    [Fact]
    public void An_event_end_drops_its_waiting_wave_warn_and_a_purge_drops_every_one()
    {
        var (hub, _, _) = New();
        hub.Queue.Enqueue(PushLines.WaveWarn("raid", 2, 60));
        hub.Queue.Enqueue(PushLines.WaveWarn("siege", 2, 60));
        hub.EventEnded("raid");
        Assert.Equal(["[NYAR:ev] type=wave-warn id=siege secs=60 wave=2", "[NYAR:ev] type=event-end id=raid secs=0"], Texts(hub));
        hub.Purged(300);
        Assert.Equal(["[NYAR:ev] type=event-end id=raid secs=0", "[NYAR:ev] type=killswitch id=- secs=300"], Texts(hub));
    }

    // ---- the transitions report themselves (A6): the real engine and catalog, the hub as their sink ----

    static ControlState Open(bool purge = false) => new(purge, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    static (EventEngine Engine, EventCatalog Catalog, PushHub Hub) Wired(params string[] events)
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(events), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var hub = New().Hub;
        var engine = new EventEngine(catalog) { Push = hub };
        catalog.Push = hub;
        return (engine, catalog, hub);
    }

    [Fact]
    public void A_start_a_wave_a_stop_and_an_expiry_each_push_once()
    {
        var (e, _, hub) = Wired(Json.Event("raid"), Json.Event("siege"));
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        Assert.Null(e.Start("siege", "manual", T0, Open()));
        e.WaveSpawned("raid");
        e.WaveSpawned("raid");
        Assert.NotNull(e.Cancel("siege"));                         // `.nyar event stop`, or the fault limit
        Assert.Single(e.Expire(T0.AddSeconds(600), 30));
        Assert.Equal(
        [
            "[NYAR:ev] type=event-start id=raid secs=600",
            "[NYAR:ev] type=event-start id=siege secs=600",
            "[NYAR:ev] type=wave id=raid secs=0 wave=1",
            "[NYAR:ev] type=wave id=raid secs=0 wave=2",
            "[NYAR:ev] type=event-end id=siege secs=0",
            "[NYAR:ev] type=event-end id=raid secs=0",
        ], Texts(hub));
    }

    [Fact]
    public void A_refused_start_or_an_end_of_nothing_pushes_nothing()
    {
        var (e, _, hub) = Wired(Json.Event("raid"));
        Assert.NotNull(e.Start("nope", "manual", T0, Open()));        // unknown
        Assert.NotNull(e.Start("raid", "manual", T0, Open(purge: true)));   // the purge cooldown
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        Assert.NotNull(e.Start("raid", "manual", T0, Open()));        // already active
        e.WaveSpawned("nope");
        Assert.Null(e.Cancel("nope"));
        Assert.Empty(e.Expire(T0.AddSeconds(10), 30));
        Assert.Equal(["[NYAR:ev] type=event-start id=raid secs=600"], Texts(hub));
    }

    [Fact]
    public void A_purge_pushes_the_killswitch_once_and_no_end_per_event()
    {
        var (e, _, hub) = Wired(Json.Event("raid"), Json.Event("siege"));
        e.Start("raid", "manual", T0, Open());
        e.Start("siege", "manual", T0, Open());
        hub.Queue.Take(10);
        Assert.Equal(2, e.CancelAll(300).Count);
        Assert.Equal(["[NYAR:ev] type=killswitch id=- secs=300"], Texts(hub));
    }

    [Fact]
    public void An_applied_load_pushes_config_changed_and_a_rejected_file_pushes_nothing()
    {
        var (_, catalog, hub) = Wired(Json.Event("raid"));
        Assert.NotNull(catalog.Reload(EventValidator.Parse("{ broken", FakeUnits.Default()), FileStamp.Of(T0, [2])));
        Assert.Empty(hub.Queue.Lines);
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default()), FileStamp.Of(T0, [3])));
        Assert.Equal(["[NYAR:ev] type=config-changed id=- secs=0"], Texts(hub));
    }

    [Fact]
    public void A_catalog_or_engine_without_a_sink_pushes_nothing_and_does_not_fail()
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(Json.Event("raid")), FakeUnits.Default()), FileStamp.Of(T0, [1])));
        var e = new EventEngine(catalog);
        Assert.Null(e.Start("raid", "manual", T0, Open()));
        e.WaveSpawned("raid");
        Assert.NotNull(e.Cancel("raid"));
        Assert.Empty(e.CancelAll(300));
    }

    [Fact]
    public void Draining_the_queue_below_its_cap_ends_the_overflow_streak()
    {
        var (hub, _, log) = New();
        for (var i = 1; i <= 50; i++) hub.Queue.Enqueue(PushLines.WaveWarn("raid", i, 60));
        hub.Wave("siege", 1);                                     // overflow 1
        hub.EventEnded("raid");                                   // drops raid's 49 waiting warnings
        for (var i = 1; i <= 60; i++) hub.Wave("siege", i + 1);  // overflow 2
        Assert.Equal(2, log.Count("push queue full, oldest dropped"));
    }

    [Fact]
    public void A_fault_in_the_warnings_never_stops_the_send()
    {
        var (hub, users, log) = New();
        hub.Subscribe(1);
        hub.ConfigChanged();
        Assert.Equal(1, hub.Tick(T0, [null!], waveWarnings: true));
        Assert.Equal(["[NYAR:ev] type=config-changed id=- secs=0"], users.Sent.Select(s => s.Text));
        Assert.Equal(1, log.Count("push: warnings failed"));
    }

    // ---- privacy ----

    [Fact]
    public void No_push_line_carries_a_coordinate_or_another_key()
    {
        var (hub, _, _) = New();
        var active = Running(Raid());
        hub.EventStarted(active.Instance);
        hub.Wave("raid", 1);
        hub.EventEnded("raid");
        hub.Purged(300);
        hub.ConfigChanged();
        hub.Queue.Enqueue(PushLines.WaveWarn("raid", 2, 60));
        Assert.Equal(6, hub.Queue.Count);
        foreach (var line in Texts(hub))
        {
            Assert.StartsWith("[NYAR:ev] ", line);
            var keys = line.Split(' ').Skip(1).Select(t => t[..t.IndexOf('=')]).ToList();
            Assert.Equal(["type", "id", "secs"], keys.Take(3));
            Assert.All(keys.Skip(3), k => Assert.Equal("wave", k));
            Assert.DoesNotMatch(new Regex("1520|460"), line);
        }
    }
}
