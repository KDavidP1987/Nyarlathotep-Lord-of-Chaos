using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D11 (2026-10-15 note, G-Portal test): an admin's `.nyar event start` of an apocalypse whose goal is
/// Admin reads the admin's position as the goal; before the fix only a SpawnWaves Admin location read it, so every start
/// was refused "badarg location admin_location".</summary>
public class AdminFlowApocalypseTests
{
    static string AdminGoal(string id) => Apo.Event(id, action: Apo.Action(goal: Apo.Goal(location: "{ \"type\": \"Admin\" }")));

    [Fact]
    public void An_admin_goal_apocalypse_starts_at_the_admin()
    {
        var r = Of(AdminGoal("apo"));
        r.Position = (12f, 3f, -40f);
        var o = r.Flows.Start(r.Who, "apo");
        Assert.True(o.Ok, o.Human);
        Assert.Equal((12f, 3f, -40f), r.Ops.Engine.Find("apo")!.Origin);
    }

    [Fact]
    public void An_admin_goal_apocalypse_needs_a_readable_position()
    {
        var r = Of(AdminGoal("apo"));
        r.Position = null;
        var o = r.Flows.Start(r.Who, "apo");
        Assert.False(o.Ok);
        Assert.Equal(Reasons.NoPosition, o.Reason);
    }

    [Fact]
    public void A_point_goal_apocalypse_needs_no_position()
    {
        var r = Of(Apo.Event("apo"));
        r.Position = null;
        Assert.True(r.Flows.Start(r.Who, "apo").Ok);
    }

    [Fact]
    public void A_start_without_an_admin_names_who_can_start_it()
    {
        var r = Of(AdminGoal("apo"));
        var refused = r.Ops.Engine.Start("apo", "manual", r.Ops.Now, r.Ops.Controls());
        Assert.NotNull(refused);
        Assert.Equal(Reasons.AdminLocation, refused!.Reason);
        Assert.Contains("only an admin's .nyar event start can start it", refused.Human);
    }
}
