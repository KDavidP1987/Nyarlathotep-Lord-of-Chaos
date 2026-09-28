#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The game side of the admin flows (raphael-api-admin D1, D3, A14). Services/AdminOps implements it with one
/// call per member; the tests fake it. Every `Op` member mutates and is reached only inside
/// <see cref="ActionGateway.Run{T}"/> (Test-CheckGatewayOnly, D11); <see cref="Definitions"/> only reads.</summary>
public interface IAdminOps
{
    /// <summary>The current definitions: the start flow reads whether the event spawns at the admin (A14).</summary>
    DefinitionSet Definitions { get; }

    Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin);
    Outcome OpStopEvent(string id);
    string OpPurge();

    /// <summary>The running events and the purgeable units, read inside the purge flows' gateway call (A14).</summary>
    (int Events, int Units) OpPurgeCounts();

    Outcome OpEdit(string id, string path, object value);
    Outcome OpReload();
    Outcome OpAuthor(Func<string, EditPlan> plan);
    Outcome OpDelete(ulong adminId, string id, bool confirm);
    Outcome OpUseTemplate(string template, string? asId);
    Outcome OpSetPillar(string name, string state);
}

/// <summary>Who runs an admin flow: the admin's SteamID and name (for the admin log line) and a reader of their
/// position, which returns null when the position cannot be read.</summary>
public sealed record AdminCaller(ulong Id, string Name, Func<(float X, float Y, float Z)?> Position);

/// <summary>One flow per admin verb (raphael-api-admin D1, D3): the argument checks, the admin log line, the gateway
/// with the verb's kind from <see cref="Kinds"/>, then the <see cref="IAdminOps"/> call. The human commands call these
/// and reply <see cref="Outcome.Human"/>, which is 0.5.1's reply for the same input.</summary>
public sealed class AdminFlows(IAdminOps ops, ActionGateway gateway, PurgeArming purgeArming, Action<string> log, Func<DateTime> utcNow)
{
    /// <summary>The gateway row of each admin verb, the one table a twin and its human command share (D3).</summary>
    public static readonly IReadOnlyDictionary<string, ActionKind> Kinds = new Dictionary<string, ActionKind>(StringComparer.Ordinal)
    {
        ["event start"] = ActionKind.StartEvent,
        ["event stop"] = ActionKind.EndEvent,
        ["event enable"] = ActionKind.EnableEvent,
        ["event disable"] = ActionKind.DisableEvent,
        ["event set"] = ActionKind.SetEventField,
        ["event reload"] = ActionKind.LoadDefinitions,
        ["event new"] = ActionKind.CreateEvent,
        ["event copy"] = ActionKind.CreateEvent,
        ["event delete"] = ActionKind.DeleteEvent,
        ["template use"] = ActionKind.CreateEvent,
        ["pillar set"] = ActionKind.SetPillar,
        ["purge ask"] = ActionKind.Purge,
        ["purge confirm"] = ActionKind.PurgeConfirm,
    };

    /// <summary>`event start &lt;id&gt;`: an event whose location is Admin spawns around the admin, whose position must
    /// be readable (Business rules 3: badarg location no_position).</summary>
    public Outcome Start(AdminCaller who, string id)
    {
        if (BadId(id) is { } bad) return bad;
        (float X, float Y, float Z)? origin = null;
        if (ops.Definitions.Find(id)?.Action?.Location.Type == LocationType.Admin)
        {
            origin = who.Position();
            if (origin is null) return AdminLines.PositionUnread;
        }
        LogAdmin(who, $"event start {id}");
        return gateway.Run<Outcome>(Kinds["event start"], Actor.Admin, () => ops.OpStartEvent(id, origin), ActionGateway.Denied);
    }

    public Outcome Stop(AdminCaller who, string id)
    {
        if (BadId(id) is { } bad) return bad;
        LogAdmin(who, $"event stop {id}");
        return gateway.Run<Outcome>(Kinds["event stop"], Actor.Admin, () => ops.OpStopEvent(id), ActionGateway.Denied);
    }

    /// <summary>`event enable|disable &lt;id&gt;`.</summary>
    public Outcome Enable(AdminCaller who, string id, bool on)
    {
        if (BadId(id) is { } bad) return bad;
        var verb = on ? "enable" : "disable";
        LogAdmin(who, $"event {verb} {id}");
        return gateway.Run<Outcome>(Kinds[$"event {verb}"], Actor.Admin, () => ops.OpEdit(id, "enabled", on), ActionGateway.Denied);
    }

    /// <summary>`event set &lt;id&gt; &lt;field&gt; &lt;value&gt;`; `location here` reads the admin's position (event-library D11).</summary>
    public Outcome Set(AdminCaller who, string id, string field, string value)
    {
        if (BadId(id) is { } bad) return bad;
        var v = CommandArgs.SettableValue(field, value);
        if (v.Error is not null)
            return Outcome.Refused(v.Error, RefusalCode.BadArg, CommandArgs.SettableFields.ContainsKey(field) ? "value" : "field");
        var newValue = v.Value;
        if (newValue is LocationHere)                                 // the admin's position, rounded to 0.1 (D11)
        {
            var p = LocationArg.FromContext(who.Position);
            if (p.Error is not null) return Outcome.Refused(p.Error, RefusalCode.BadArg, "value");
            newValue = p.Value;
        }
        LogAdmin(who, $"event set {id} {field} {value}");
        return gateway.Run<Outcome>(Kinds["event set"], Actor.Admin, () => ops.OpEdit(id, field, newValue), ActionGateway.Denied);
    }

    public Outcome ReloadEvents(AdminCaller who)
    {
        LogAdmin(who, "event reload");
        return gateway.Run<Outcome>(Kinds["event reload"], Actor.Admin, () => ops.OpReload(), ActionGateway.Denied);
    }

    /// <summary>`event new &lt;id&gt; &lt;pillar&gt;` (event-library D6).</summary>
    public Outcome New(AdminCaller who, string id, string pillar)
    {
        LogAdmin(who, $"event new {id} {pillar}");
        return gateway.Run<Outcome>(Kinds["event new"], Actor.Admin, () => ops.OpAuthor(text => Authoring.New(text, id, pillar)), ActionGateway.Denied);
    }

    /// <summary>`event copy &lt;id&gt; &lt;newId&gt;` (event-library D7).</summary>
    public Outcome Copy(AdminCaller who, string id, string newId)
    {
        LogAdmin(who, $"event copy {id} {newId}");
        return gateway.Run<Outcome>(Kinds["event copy"], Actor.Admin, () => ops.OpAuthor(text => Authoring.Copy(text, id, newId)), ActionGateway.Denied);
    }

    /// <summary>`event delete &lt;id&gt; [confirm]` (event-library D8).</summary>
    public Outcome DeleteEvent(AdminCaller who, string id, bool confirm)
    {
        LogAdmin(who, confirm ? $"event delete {id} confirm" : $"event delete {id}");
        return gateway.Run<Outcome>(Kinds["event delete"], Actor.Admin, () => ops.OpDelete(who.Id, id, confirm), ActionGateway.Denied);
    }

    /// <summary>`template use &lt;template&gt; [as &lt;id&gt;]` (event-library D5).</summary>
    public Outcome TemplateUse(AdminCaller who, string template, string? asId)
    {
        LogAdmin(who, asId is null ? $"template use {template}" : $"template use {template} as {asId}");
        return gateway.Run<Outcome>(Kinds["template use"], Actor.Admin, () => ops.OpUseTemplate(template, asId), ActionGateway.Denied);
    }

    /// <summary>`pillar &lt;name&gt; on|off` (event-library D14); the reply's lines are joined with "\n".</summary>
    public Outcome Pillar(AdminCaller who, string name, string state)
    {
        LogAdmin(who, $"pillar {name} {state}");
        return gateway.Run<Outcome>(Kinds["pillar set"], Actor.Admin, () => ops.OpSetPillar(name, state), ActionGateway.Denied);
    }

    /// <summary>`purge`: arms the caller's confirm when something is left to purge; logs nothing, as 0.5.1.</summary>
    public Outcome PurgeAsk(AdminCaller who) =>
        gateway.Run<Outcome>(Kinds["purge ask"], Actor.Admin, () =>
        {
            var (events, units) = ops.OpPurgeCounts();
            if (events == 0 && units == 0) return AdminLines.NothingToPurgeOutcome;
            purgeArming.Arm(who.Id, utcNow());
            return AdminLines.PurgeAsked(events, units);
        }, ActionGateway.Denied);

    /// <summary>`purge confirm`: fires within 30 s of the same admin's ask (PurgeArming's order).</summary>
    public Outcome PurgeConfirm(AdminCaller who)
    {
        LogAdmin(who, "purge confirm");
        return gateway.Run<Outcome>(Kinds["purge confirm"], Actor.Admin, () =>
        {
            var (events, units) = ops.OpPurgeCounts();
            return purgeArming.Confirm(who.Id, utcNow(), events > 0 || units > 0) switch
            {
                PurgeConfirmResult.Purge => ops.OpPurge(),
                PurgeConfirmResult.NothingToPurge => AdminLines.NothingToPurgeOutcome,
                _ => AdminLines.NotArmedOutcome,
            };
        }, ActionGateway.Denied);
    }

    static Outcome? BadId(string id) =>
        CommandArgs.EventId(id).Error is { } error ? Outcome.Refused(error, RefusalCode.BadArg, "id") : null;

    void LogAdmin(AdminCaller who, string command) => log(AdminLines.AdminRan(who.Name, who.Id, command));
}
