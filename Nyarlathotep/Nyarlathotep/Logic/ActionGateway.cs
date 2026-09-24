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
}

/// <summary>Who asks (Design › Permissions): an admin in game, the operator's files (loaded at boot), the scheduler
/// and triggers, or a player.</summary>
public enum Actor { Admin, Operator, System, Player }

/// <summary>Marks a service method that changes events, units, files or chat. Test-CheckGatewayOnly in
/// tools/preflight.ps1 requires every call of such a method outside its own file to sit inside Gateway.Run (D11).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MutatingAttribute : Attribute { }

/// <summary>The grants: Admin gets every kind; Operator loads definitions; System starts and ends enabled
/// definitions only; Player gets nothing in this child (D10).</summary>
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
/// when the table allows, and logs every denial.</summary>
public sealed class ActionGateway(Action<string> log)
{
    public const string DeniedReply = "denied";

    /// <summary>The work's reply, or <see cref="DeniedReply"/> after logging "gateway: denied &lt;kind&gt; for
    /// &lt;actor&gt;". <paramref name="definitionEnabled"/> matters to System only: pass the definition's
    /// Startable flag for StartEvent and EndEvent.</summary>
    public string Run(ActionKind kind, Actor actor, Func<string> work, bool definitionEnabled = true)
    {
        if (!ActionTable.Allows(kind, actor, definitionEnabled))
        {
            log($"gateway: denied {kind} for {actor}");
            return DeniedReply;
        }
        return work();
    }
}
