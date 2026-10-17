using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>login-notice D1, D2, D4: the text of the login notice, who gets it and when, and its contained failures.</summary>
public class LoginNoticeTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 17, 20, 0);

    static EventDefinition Def(string id, string name) => new(
        id, name, true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 600,
        new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 5)], 3, 60, 10, new Location(LocationType.Point, 0, 0), null),
        Announce.None);

    static RunningInstance Run(string id, string name, int minutesLeft) => new(Def(id, name), T0.AddMinutes(-1), T0.AddMinutes(minutesLeft));

    static string? NoOwn(RunningInstance _) => null;

    // ---- D1 the text ----

    [Fact]
    public void Text_fails_when_a_line_is_returned_with_nothing_running()
    {
        Assert.Null(Messages.LoginNotice([], T0, NoOwn));
    }

    [Fact]
    public void Text_fails_when_a_part_is_out_of_end_time_order_or_differs_from_its_status_line()
    {
        RunningInstance[] running = [Run("b", "Siege", 20), Run("a", "Bandit raid", 5), Run("c", "Night of the dead", 45)];
        string? Own(RunningInstance r) => r.Definition.Id == "c" ? "Night of the dead: horde 1/3, 20 % to Brighthaven, 12 roamers, ends in 45 min" : null;
        var line = Messages.LoginNotice(running, T0, Own)!;
        var status = Messages.Status(running, T0, Own);
        Assert.Equal($"Events in progress: {string.Join("; ", status)}. Type .nyar status for details.", line);
        Assert.Equal("Events in progress: Bandit raid: 5 min left; Siege: 20 min left; "
            + "Night of the dead: horde 1/3, 20 % to Brighthaven, 12 roamers, ends in 45 min. Type .nyar status for details.", line);
    }

    [Fact]
    public void Text_fails_when_a_line_passes_480_bytes_cuts_a_part_or_miscounts_the_rest()
    {
        var running = Enumerable.Range(1, 30).Select(i => Run($"e{i}", $"Event number {i:00} of the long night", i)).ToList();
        var line = Messages.LoginNotice(running, T0, NoOwn)!;
        Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes, $"{Encoding.UTF8.GetByteCount(line)} bytes");
        Assert.True(Encoding.UTF8.GetByteCount(line) <= 512);
        var status = Messages.Status(running, T0, NoOwn);
        var body = line["Events in progress: ".Length..^". Type .nyar status for details.".Length];
        var parts = body.Split("; ");
        var more = parts[^1];
        var kept = parts[..^1];
        Assert.Equal(status.Take(kept.Length), kept);                     // whole parts, in order, never one cut in half
        Assert.Equal($"… and {status.Count - kept.Length} more", more);
        // one more whole part would not have fitted
        var longer = $"Events in progress: {string.Join("; ", status.Take(kept.Length + 1))}; … and {status.Count - kept.Length - 1} more. Type .nyar status for details.";
        Assert.True(Encoding.UTF8.GetByteCount(longer) > Wire.MaxBytes);
    }

    [Fact]
    public void Text_passes_one_event()
    {
        Assert.Equal("Events in progress: Bandit raid: 5 min left. Type .nyar status for details.",
            Messages.LoginNotice([Run("a", "Bandit raid", 5)], T0, NoOwn));
    }

    [Fact]
    public void Text_empty_no_running_instance_no_line()
    {
        Assert.Null(Messages.LoginNotice(Array.Empty<RunningInstance>(), T0, _ => "own"));
    }

    // ---- D2 who gets it, when ----

    sealed class Rig
    {
        public DateTime Now = T0;
        public List<RunningInstance> Running = [];
        public readonly Dictionary<int, ulong> Holders = new();   // user entity → the platform id holding it, absent when disconnected
        public readonly List<(int User, string Text)> Sent = [];
        public readonly List<string> Info = [], Warn = [];
        public readonly FailureStreak SendFaults = new();
        public Func<int, string, bool>? FailSend;
        public Exception? ReadFault, HoldsFault, OwnFault;
        public bool On = true;
        public readonly LoginNotices<int> Notices;

        public Rig() => Notices = new LoginNotices<int>(() => Now, Info.Add, Warn.Add, SendFaults);

        public void Connect(int user, ulong id, bool on = true) { Holders[user] = id; Notices.Connected(user, id, on); }

        public void At(int seconds)
        {
            Now = T0.AddSeconds(seconds);
            Notices.Tick(On, (u, id) => HoldsFault is { } hf ? throw hf : Holders.TryGetValue(u, out var h) && h == id,
                () => ReadFault is { } f ? throw f : Running, r => OwnFault is { } of ? throw of : NoOwn(r),
                (u, text) =>
                {
                    if (FailSend?.Invoke(u, text) == true) throw new InvalidOperationException("send broke");
                    Sent.Add((u, text));
                });
        }
    }

    [Fact]
    public void Delivery_fails_when_a_notice_leaves_before_10_s()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);
        rig.At(9);
        Assert.Empty(rig.Sent);
        rig.At(10);
        Assert.Single(rig.Sent);
        Assert.Equal(["login notice sent: 1 events"], rig.Info);
    }

    [Fact]
    public void Delivery_fails_when_sent_with_nothing_running_at_its_due_time()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);
        rig.Running = [];          // the event ended between the connect and the due time
        rig.At(10);
        Assert.Empty(rig.Sent);
        Assert.Equal(0, rig.Notices.Pending);
    }

    [Fact]
    public void Delivery_fails_when_it_reaches_a_disconnected_or_replaced_user()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);
        rig.Connect(2, 200);
        rig.Holders.Remove(1);     // left
        rig.Holders[2] = 999;      // another player holds the user entity
        rig.At(10);
        Assert.Empty(rig.Sent);
    }

    [Fact]
    public void Delivery_fails_when_sent_twice_within_60_s_of_reconnects()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);
        rig.At(3);
        rig.Connect(1, 100);       // a reconnect before the first notice left
        rig.At(13);
        rig.At(30);
        rig.Connect(1, 100);       // 30 s after the first connect
        rig.At(40);
        Assert.Single(rig.Sent);
        rig.Now = T0.AddSeconds(89);
        rig.Notices.Connected(1, 100, true);   // 59 s after the previous connect: still quiet
        rig.At(99);
        Assert.Single(rig.Sent);
        rig.Now = T0.AddSeconds(149);
        rig.Notices.Connected(1, 100, true);   // 60 s after it: a new notice
        rig.At(159);
        Assert.Equal(2, rig.Sent.Count(s => s.User == 1));
    }

    [Fact]
    public void Delivery_fails_when_sent_for_an_enabled_event_that_is_not_running()
    {
        // enabled definitions are not read at all: only the running list counts
        var rig = new Rig();
        rig.Connect(1, 100);
        rig.At(10);
        Assert.Empty(rig.Sent);
        Assert.Empty(rig.Info);
    }

    [Fact]
    public void Delivery_fails_when_a_connect_with_nothing_running_uses_up_the_gate()
    {
        var rig = new Rig();
        rig.Connect(1, 100);
        rig.At(10);                // nothing running: nothing sent, the gate untouched (Review 1 F4)
        rig.Running = [Run("a", "Bandit raid", 5)];
        rig.Now = T0.AddSeconds(20);
        rig.Notices.Connected(1, 100, true);
        rig.At(30);
        Assert.Single(rig.Sent);
    }

    [Fact]
    public void Delivery_fails_when_it_skips_a_non_admin_or_stops_the_admin_degraded_notice()
    {
        // Services/Announcer.UserConnected queues the login notice for everyone, then asks DegradedGreeting with the
        // admin's own gate: both must hold for one admin connect (Codex step 3 F4).
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        var degradedGate = new LoginGate();
        rig.Connect(1, 100);                   // a player
        Assert.False(DegradedGreeting.ShouldQueue(false, () => 2, degradedGate, 100, T0));
        rig.Connect(2, 200);                   // an admin, with two systems degraded
        Assert.True(DegradedGreeting.ShouldQueue(true, () => 2, degradedGate, 200, T0));
        rig.At(10);
        Assert.Equal([1, 2], rig.Sent.Select(s => s.User).OrderBy(u => u));
        Assert.False(DegradedGreeting.ShouldQueue(true, () => 2, degradedGate, 200, T0.AddSeconds(30)));   // its own 60 s window
        Assert.True(DegradedGreeting.ShouldQueue(true, () => 2, degradedGate, 200, T0.AddSeconds(100)));
        Assert.False(DegradedGreeting.ShouldQueue(true, () => 0, degradedGate, 300, T0));                   // nothing degraded
        Assert.True(degradedGate.Allows(300, T0));                                                    // and the gate untouched
    }

    [Fact]
    public void Delivery_fails_when_a_failed_send_uses_up_the_gate()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)], FailSend = (_, _) => true };
        rig.Connect(1, 100);
        rig.At(10);                            // the send throws: nothing delivered
        Assert.Empty(rig.Sent);
        rig.FailSend = null;
        rig.Now = T0.AddSeconds(20);
        rig.Notices.Connected(1, 100, true);   // a reconnect inside 60 s still gets the notice
        rig.At(30);
        Assert.Single(rig.Sent);
    }

    [Fact]
    public void Delivery_fails_when_a_quick_rejoin_is_greeted_early_or_not_at_all()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);                   // t=0
        rig.Now = T0.AddSeconds(5);
        rig.Notices.Connected(1, 100, true);   // dropped and rejoined at t=5
        rig.At(10);
        Assert.Empty(rig.Sent);                // not 5 s into the new session
        rig.At(15);
        Assert.Single(rig.Sent);
        Assert.Equal(0, rig.Notices.Pending);
    }

    [Fact]
    public void Delivery_fails_when_a_notice_queued_before_the_switch_went_off_is_sent()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100);
        rig.On = false;
        rig.At(10);
        Assert.Empty(rig.Sent);
        Assert.Equal(0, rig.Notices.Pending);
    }

    [Fact]
    public void Delivery_fails_when_the_degraded_list_is_read_for_a_non_admin()
    {
        var reads = 0;
        Assert.False(DegradedGreeting.ShouldQueue(false, () => ++reads, new LoginGate(), 100, T0));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void Delivery_passes_the_switch_off_queues_nothing()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)] };
        rig.Connect(1, 100, on: false);
        Assert.Equal(0, rig.Notices.Pending);
        rig.At(10);
        Assert.Empty(rig.Sent);
    }

    [Fact]
    public void Delivery_empty_no_connect_no_read()
    {
        var rig = new Rig { ReadFault = new InvalidOperationException("must not be read") };
        rig.At(10);
        Assert.Empty(rig.Warn);
    }

    // ---- D4 faults ----

    [Fact]
    public void Faults_fails_when_a_throwing_send_stops_the_other_notices_or_logs_twice_in_a_streak()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)], FailSend = (u, _) => u != 3 };
        rig.Connect(1, 100);
        rig.Connect(2, 200);
        rig.Connect(3, 300);
        rig.At(10);                // must not throw
        Assert.Equal([3], rig.Sent.Select(s => s.User));
        Assert.Equal(["private message failed: send broke"], rig.Warn);
        Assert.Equal(0, rig.SendFaults.Count);   // the last send succeeded and ended the streak
    }

    [Fact]
    public void Faults_fails_when_a_throwing_read_sends_a_notice_or_logs_twice()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)], ReadFault = new InvalidOperationException("catalog broke") };
        rig.Connect(1, 100);
        rig.Connect(2, 200);
        rig.At(10);
        Assert.Empty(rig.Sent);
        Assert.Equal(["login notice failed: catalog broke"], rig.Warn);
    }

    [Fact]
    public void Faults_fails_when_a_throwing_holds_or_status_line_escapes_the_tick()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)], HoldsFault = new InvalidOperationException("entity broke") };
        rig.Connect(1, 100);
        rig.At(10);                            // must not throw
        rig.HoldsFault = null;
        rig.OwnFault = new InvalidOperationException("status broke");
        rig.Connect(2, 200);
        rig.At(20);                            // must not throw
        Assert.Empty(rig.Sent);
        Assert.Equal(["login notice failed: entity broke"], rig.Warn);   // one streak for both
        rig.OwnFault = null;
        rig.Connect(3, 300);
        rig.At(30);
        Assert.Equal([3], rig.Sent.Select(s => s.User));
    }

    [Fact]
    public void Faults_passes_a_good_read_ends_the_streak()
    {
        var rig = new Rig { Running = [Run("a", "Bandit raid", 5)], ReadFault = new InvalidOperationException("catalog broke") };
        rig.Connect(1, 100);
        rig.At(10);
        rig.ReadFault = null;
        rig.Connect(2, 200);
        rig.At(20);
        rig.ReadFault = new InvalidOperationException("catalog broke");
        rig.Connect(3, 300);
        rig.At(30);
        Assert.Equal([2], rig.Sent.Select(s => s.User));
        Assert.Equal(2, rig.Warn.Count);       // one per streak
    }

    [Fact]
    public void Faults_empty_nothing_due_nothing_logged()
    {
        var rig = new Rig { FailSend = (_, _) => true };
        rig.At(10);
        Assert.Empty(rig.Warn);
    }
}
