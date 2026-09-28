#nullable enable

namespace Nyarlathotep.Logic;

/// <summary>Every mutating operation of this child (foundation D10, Epic D36). A new mutation is a new member here
/// and a new row in <see cref="ActionTable"/>; AuthorizationTests fails on a member without one.</summary>
public enum ActionKind
{
    StartEvent,
    EndEvent,
    EnableEvent,
    DisableEvent,
    SetEventField,
    LoadDefinitions,
    Spawn,
    Purge,
    PurgeConfirm,
    Announce,
    /// <summary>`.nyar api sub on|off`: changes only the caller's own push subscription (raphael-api-core D5).</summary>
    Subscribe,
    /// <summary>`.nyar template use`, `.nyar event new` and `copy`: a new, disabled definition (event-library D17).</summary>
    CreateEvent,
    /// <summary>`.nyar event delete &lt;id&gt; confirm` (event-library D8, D17).</summary>
    DeleteEvent,
    /// <summary>`.nyar pillar &lt;name&gt; on|off`, saved to the cfg (event-library D14, D17).</summary>
    SetPillar,
}

/// <summary>Who asks (Design › Permissions): an admin in game, the operator's files (loaded at boot), the scheduler
/// and triggers, or a player.</summary>
public enum Actor { Admin, Operator, System, Player }

/// <summary>Marks a service method that changes events, units, files or chat. Test-CheckGatewayOnly in
/// tools/preflight.ps1 requires every call of such a method outside its own file to sit inside Gateway.Run (D11).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MutatingAttribute : Attribute { }

/// <summary>The grants: Admin gets every kind; Operator loads definitions; System starts and ends enabled
/// definitions only; Player gets only Subscribe, its own push subscription (foundation D10; raphael-api-core D7).</summary>
public static class ActionTable
{
    public static readonly IReadOnlyDictionary<ActionKind, IReadOnlySet<Actor>> Grants = new Dictionary<ActionKind, IReadOnlySet<Actor>>
    {
        [ActionKind.StartEvent] = Set(Actor.Admin, Actor.System),
        [ActionKind.EndEvent] = Set(Actor.Admin, Actor.System),
        [ActionKind.EnableEvent] = Set(Actor.Admin),
        [ActionKind.DisableEvent] = Set(Actor.Admin),
        [ActionKind.SetEventField] = Set(Actor.Admin),
        [ActionKind.LoadDefinitions] = Set(Actor.Admin, Actor.Operator),
        [ActionKind.Spawn] = Set(Actor.Admin),
        [ActionKind.Purge] = Set(Actor.Admin),
        [ActionKind.PurgeConfirm] = Set(Actor.Admin),
        [ActionKind.Announce] = Set(Actor.Admin),
        [ActionKind.Subscribe] = Set(Actor.Admin, Actor.Player),
        [ActionKind.CreateEvent] = Set(Actor.Admin),
        [ActionKind.DeleteEvent] = Set(Actor.Admin),
        [ActionKind.SetPillar] = Set(Actor.Admin),
    };

    /// <summary>True when <paramref name="actor"/> may run <paramref name="kind"/>. System's grant holds only for an
    /// enabled definition; a kind without a row is denied to everyone.</summary>
    public static bool Allows(ActionKind kind, Actor actor, bool definitionEnabled)
    {
        if (!Grants.TryGetValue(kind, out var actors) || !actors.Contains(actor)) return false;
        return actor != Actor.System || definitionEnabled;
    }

    static IReadOnlySet<Actor> Set(params Actor[] actors) => new HashSet<Actor>(actors);
}

/// <summary>The one door for mutations (D10, D11). The caller passes the work as a delegate; the gateway runs it only
/// when the table allows, and logs every denial. <paramref name="entered"/>, when given, sees each kind and actor as a
/// call enters (the tests record it to prove the flows reach IAdminOps through the gateway, raphael-api-admin D3).</summary>
public sealed class ActionGateway(Action<string> log, Action<ActionKind, Actor>? entered = null)
{
    public const string DeniedReply = "denied";

    /// <summary>The work's reply, or <see cref="DeniedReply"/> after logging "gateway: denied &lt;kind&gt; for
    /// &lt;actor&gt;". <paramref name="definitionEnabled"/> matters to System only: pass the definition's
    /// Startable flag for StartEvent and EndEvent.</summary>
    public string Run(ActionKind kind, Actor actor, Func<string> work, bool definitionEnabled = true) =>
        Run(kind, actor, work, () => DeniedReply, definitionEnabled);

    /// <summary>The work's outcome, or <see cref="Denied"/> (raphael-api-admin D1: noaccess).</summary>
    public Outcome Run(ActionKind kind, Actor actor, Func<Outcome> work, bool definitionEnabled = true) =>
        Run(kind, actor, work, Denied, definitionEnabled);

    /// <summary>The one door with any result type: <paramref name="denied"/> answers a kind the table refuses
    /// (raphael-api-admin D1, D11: the admin flows reach IAdminOps only inside this call).</summary>
    public T Run<T>(ActionKind kind, Actor actor, Func<T> work, Func<T> denied, bool definitionEnabled = true)
    {
        entered?.Invoke(kind, actor);
        if (!ActionTable.Allows(kind, actor, definitionEnabled))
        {
            log($"gateway: denied {kind} for {actor}");
            return denied();
        }
        return work();
    }

    /// <summary>The denial as an outcome: "denied", code noaccess (Business rules 3).</summary>
    public static Outcome Denied() => Outcome.Refused(DeniedReply, RefusalCode.NoAccess);
}
