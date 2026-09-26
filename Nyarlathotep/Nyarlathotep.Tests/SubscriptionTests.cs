using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D5: `.nyar api sub on|off` keeps an in-memory set keyed by the caller's own id, at most
/// 128, ended by off, a disconnect or the offline prune; a push reaches only subscribed, connected ids; every change
/// logs the count and never an id.</summary>
public class SubscriptionTests
{
    const ulong Id = 424242424242;

    static (Subscriptions Subs, LogLines Log) New()
    {
        var log = new LogLines();
        return (new Subscriptions(log.Add), log);
    }

    [Fact]
    public void On_twice_leaves_one_entry_and_logs_one_change()
    {
        var (subs, log) = New();
        Assert.Equal("[NYAR:ok] cmd=sub on=1", subs.On(Id));
        Assert.Equal("[NYAR:ok] cmd=sub on=1", subs.On(Id));
        Assert.Equal(1, subs.Count);
        Assert.Equal(["push: 1 subscribed (on)"], log.Lines);
    }

    [Fact]
    public void Off_removes_the_entry_and_answers_on_0_either_way()
    {
        var (subs, log) = New();
        subs.On(Id);
        Assert.Equal("[NYAR:ok] cmd=sub on=0", subs.Off(Id));
        Assert.Equal("[NYAR:ok] cmd=sub on=0", subs.Off(Id));
        Assert.Equal(0, subs.Count);
        Assert.Equal(["push: 1 subscribed (on)", "push: 0 subscribed (off)"], log.Lines);
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("maybe", null)]
    [InlineData("", null)]
    [InlineData("On", null)]
    [InlineData("ON", null)]
    [InlineData(" on", null)]
    [InlineData("on ", null)]
    [InlineData("1", null)]
    [InlineData("true", null)]
    [InlineData(null, null)]
    public void Only_on_and_off_are_states(string? state, bool? parsed) =>
        Assert.Equal(parsed, Subscriptions.ParseState(state));

    [Fact]
    public void A_bad_state_is_the_badarg_line_of_the_contract() =>
        Assert.Equal("[NYAR:err] cmd=sub code=badarg arg=state", Wire.Error("sub", WireError.BadArg, arg: "state"));

    [Fact]
    public void The_129th_id_is_refused_and_adds_nothing()
    {
        var (subs, _) = New();
        for (ulong i = 1; i <= Subscriptions.Capacity; i++) Assert.StartsWith("[NYAR:ok]", subs.On(i));
        Assert.Equal("[NYAR:err] cmd=sub code=ratelimit", subs.On(Id));
        Assert.Equal(Subscriptions.Capacity, subs.Count);
        Assert.False(subs.Contains(Id));
        Assert.Equal("[NYAR:ok] cmd=sub on=1", subs.On(1));       // an id already in the set is still acknowledged
        subs.Off(1);
        Assert.Equal("[NYAR:ok] cmd=sub on=1", subs.On(Id));      // room again
    }

    [Fact]
    public void A_disconnect_removes_the_entry()
    {
        var (subs, log) = New();
        subs.On(Id);
        subs.Disconnected(Id);
        subs.Disconnected(Id);
        Assert.False(subs.Contains(Id));
        Assert.Equal(1, log.Count("push: 0 subscribed (disconnect)"));
    }

    [Fact]
    public void A_new_set_starts_empty() => Assert.Equal(0, New().Subs.Count);

    [Fact]
    public void A_push_reaches_only_subscribed_connected_ids_and_prunes_the_offline()
    {
        var (subs, log) = New();
        var users = new FakeUsers();                              // online: 1, 2, 3
        subs.On(1);
        subs.On(2);
        subs.On(Id);                                              // subscribed, then went offline unseen
        Assert.Equal(2, subs.Deliver(users, "[NYAR:ev] type=config-changed id=- secs=0"));
        Assert.Equal(new[] { 1UL, 2UL }, users.Sent.Select(s => s.Id).OrderBy(i => i));
        Assert.False(subs.Contains(Id));
        Assert.Equal(1, log.Count("push: 2 subscribed (offline)"));
        Assert.DoesNotContain(users.Sent, s => s.Id == 3);        // connected but not subscribed
    }

    [Fact]
    public void No_log_line_carries_an_id()
    {
        var (subs, log) = New();
        subs.On(Id);
        subs.Deliver(new FakeUsers(), "x");
        subs.On(Id);
        subs.Off(Id);
        subs.On(Id);
        subs.Disconnected(Id);
        Assert.NotEmpty(log.Lines);
        Assert.All(log.Lines, l => Assert.DoesNotContain("424242", l));
        Assert.All(log.Lines, l => Assert.Matches(@"^push: \d+ subscribed \((on|off|disconnect|offline)\)$", l));
    }

    [Fact]
    public void With_no_subscriber_the_user_list_is_not_read()
    {
        var (subs, _) = New();
        var users = new FakeUsers { FailList = true };
        Assert.Equal(0, subs.Deliver(users, "x"));                // would throw if read
    }
}
