using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D10: every ActionKind × actor against the table of Design › Permissions.</summary>
public class AuthorizationTests
{
    // The expected grants, written out independently of ActionTable: Admin everything, Operator the file load,
    // System start and end of enabled definitions, Player nothing in this child.
    static bool Expected(ActionKind kind, Actor actor, bool enabled) => actor switch
    {
        Actor.Admin => true,
        Actor.Operator => kind == ActionKind.LoadDefinitions,
        Actor.System => enabled && kind is ActionKind.StartEvent or ActionKind.EndEvent,
        _ => false,
    };

    public static TheoryData<ActionKind, Actor, bool> Matrix()
    {
        var data = new TheoryData<ActionKind, Actor, bool>();
        foreach (var k in Enum.GetValues<ActionKind>())
            foreach (var a in Enum.GetValues<Actor>())
                foreach (var enabled in new[] { true, false })
                    data.Add(k, a, enabled);
        return data;
    }

    [Fact]
    public void Every_action_kind_has_a_row_and_the_enum_is_not_empty()
    {
        var kinds = Enum.GetValues<ActionKind>();
        Assert.NotEmpty(kinds);
        foreach (var k in kinds) Assert.True(ActionTable.Grants.ContainsKey(k), $"{k} has no row");
        Assert.Equal(kinds.Length, ActionTable.Grants.Count);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void The_table_grants_exactly_the_expected_cells(ActionKind kind, Actor actor, bool enabled)
    {
        Assert.Equal(Expected(kind, actor, enabled), ActionTable.Allows(kind, actor, enabled));
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void The_gateway_runs_allowed_work_and_logs_every_denial(ActionKind kind, Actor actor, bool enabled)
    {
        var log = new LogLines();
        var ran = 0;
        var reply = new ActionGateway(log.Add).Run(kind, actor, () => { ran++; return "done"; }, enabled);
        if (Expected(kind, actor, enabled))
        {
            Assert.Equal("done", reply);
            Assert.Equal(1, ran);
            Assert.Empty(log.Lines);
        }
        else
        {
            Assert.Equal(ActionGateway.DeniedReply, reply);
            Assert.Equal(0, ran);
            Assert.Equal(new[] { $"gateway: denied {kind} for {actor}" }, log.Lines);
        }
    }

    [Fact]
    public void System_never_starts_a_disabled_definition()
    {
        var log = new LogLines();
        var ran = false;
        new ActionGateway(log.Add).Run(ActionKind.StartEvent, Actor.System, () => { ran = true; return ""; }, definitionEnabled: false);
        Assert.False(ran);
        Assert.Equal(1, log.Count("gateway: denied StartEvent for System"));
    }
}
