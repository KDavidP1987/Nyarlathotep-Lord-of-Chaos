#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The game side of the admin flows (raphael-api-admin D1, D3, A14). Services/AdminOps implements it with one
/// call per member; the tests fake it. Every `Op` member mutates and is reached only inside
/// <see cref="ActionGateway.Run{T}"/> (Test-CheckGatewayOnly, D11); the members without the prefix only read.</summary>
public interface IAdminOps
{
    /// <summary>The current definitions: the start flow reads whether the event spawns at the admin (A14).</summary>
    DefinitionSet Definitions { get; }

    /// <summary>The template catalogue, for `api templates`, `api template info` and the template use id check (D7).</summary>
    TemplateCatalog Templates { get; }

    /// <summary>A pillar's switch, for `api pillar list` (D7).</summary>
    bool PillarOn(Pillar pillar);

    /// <summary>The purge cooldown's end, the active events and the tracked units, for `api killswitch` (D7).</summary>
    (DateTime? PurgeUntilUtc, int Events, int Units) KillSwitch { get; }

    Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin);
    Outcome OpStopEvent(string id);
    Outcome OpPurge();

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
/// position, which returns null when the position cannot be read. <see cref="Api"/> marks a wire twin, whose admin log
/// line starts "api " (raphael-api-admin D3).</summary>
public sealed record AdminCaller(ulong Id, string Name, Func<(float X, float Y, float Z)?> Position, bool Api = false);

/// <summary>One flow per admin verb (raphael-api-admin D1, D3): the argument checks, the admin log line, the gateway
/// with the verb's kind from <see cref="Kinds"/>, then the <see cref="IAdminOps"/> call. The human commands call these
/// and reply <see cref="Outcome.Human"/>, which is 0.5.1's reply for the same input; the `.nyar api` twins call the
/// Api* dispatchers, which run the rate gate, their own argument checks and the same flow, and answer one wire line.</summary>
public sealed class AdminFlows(IAdminOps ops, ActionGateway gateway, PurgeArming purgeArming, Action<string> log, Func<DateTime> utcNow,
    RateGate? rateGate = null, Action<string>? warn = null)
{
    readonly RateGate _rate = rateGate ?? new RateGate();
    readonly Action<string> _warn = warn ?? log;

    /// <summary>The "cmd verb" keys whose last twin threw: a key logs its failure once until a run that does not throw (D12, A7).</summary>
    readonly HashSet<string> _failing = new(StringComparer.Ordinal);

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
            return Outcome.Refused(v.Error, RefusalCode.BadArg, CommandArgs.IsSettable(field) ? "value" : "field");
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
        if (PillarNames.TryParse(pillar, out _) && BadNewId(id, "id") is { } bad) return bad;     // an unknown pillar answers first, as 0.5.1
        LogAdmin(who, $"event new {id} {pillar}");
        return gateway.Run<Outcome>(Kinds["event new"], Actor.Admin, () => ops.OpAuthor(text => Authoring.New(text, id, pillar)), ActionGateway.Denied);
    }

    /// <summary>`event copy &lt;id&gt; &lt;newId&gt;` (event-library D7).</summary>
    public Outcome Copy(AdminCaller who, string id, string newId)
    {
        if (BadNewId(newId, "newId") is { } bad) return bad;
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
        // the id rule is checked after the catalogue, the template and its validity, as Authoring.TemplateUse orders them
        if (ops.Templates is { Error: null } catalog && catalog.Find(template) is { Invalid: null } && BadNewId(asId ?? template, "id") is { } bad) return bad;
        LogAdmin(who, asId is null ? $"template use {template}" : $"template use {template} as {asId}");
        return gateway.Run<Outcome>(Kinds["template use"], Actor.Admin, () => ops.OpUseTemplate(template, asId), ActionGateway.Denied);
    }

    /// <summary>`pillar &lt;name&gt; on|off` (event-library D14); the reply's lines are joined with "\n".</summary>
    public Outcome Pillar(AdminCaller who, string name, string state)
    {
        if (PillarNames.TryParse(name, out _) && state is not ("on" or "off")) return PillarCommand.UseOnOff;      // an unknown pillar answers first
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

    /// <summary>The id rule of a definition the flow would create (A13), checked before the admin log line (D3).</summary>
    static Outcome? BadNewId(string id, string arg) =>
        CommandArgs.EventId(id).Error is null ? null : Outcome.Refused(Authoring.IdRule, RefusalCode.BadArg, arg);

    void LogAdmin(AdminCaller who, string command) => log(AdminLines.AdminRan(who.Name, who.Id, who.Api ? $"api {command}" : command));

    // ---- the wire twins (raphael-api-admin D3-D7, D12): one line per twin ----

    /// <summary>The event twin's verbs; any other, list and info included, is badarg arg=verb.</summary>
    public static readonly IReadOnlyList<string> EventVerbs = ["start", "stop", "enable", "disable", "set", "reload", "new", "copy", "delete"];

    /// <summary>`.nyar api event &lt;verb&gt; …`: the rate gate, the verb, the argument count, then the verb's flow.</summary>
    public string ApiEvent(AdminCaller who, string verb, string id, string field, string value, string extra)
    {
        const string cmd = "event";
        if (!_rate.Admit(who.Id, utcNow())) return RateLimited(cmd, verb);
        if (!EventVerbs.Contains(verb)) return BadArg(cmd, verb, "verb");
        var bad = verb switch
        {
            "start" or "stop" or "enable" or "disable" => Need(("id", id)) ?? Surplus(field, value, extra),
            "set" => Need(("id", id), ("field", field), ("value", value)) ?? Surplus(extra),
            "reload" => Surplus(id, field, value, extra),
            "new" => Need(("id", id), ("pillar", field)) ?? Surplus(value, extra),
            // copy and delete: the human command keeps 0.5.1's reply for a malformed id (D1), the twin answers badarg (D3);
            // the argument count comes before the id and confirm checks, as for every verb
            "copy" => Need(("id", id), ("newId", field)) ?? Surplus(value, extra) ?? (BadId(id) is null ? null : "id"),
            _ => Need(("id", id)) ?? Surplus(value, extra) ?? (BadId(id) is null ? null : "id") ?? (field is "" or "confirm" ? null : "confirm"),
        };
        if (bad is not null) return BadArg(cmd, verb, bad);
        var api = who with { Api = true };
        return verb switch
        {
            "start" => Twin(cmd, verb, () => Start(api, id), _ => Wire.Done(cmd, verb, id)),
            "stop" => Twin(cmd, verb, () => Stop(api, id), _ => Wire.Done(cmd, verb, id)),
            "enable" or "disable" => Twin(cmd, verb, () => Enable(api, id, verb == "enable"), o => Wire.Done(cmd, verb, id, Keys(o, "changed"))),
            "set" => Twin(cmd, verb, () => Set(api, id, field, value), o => Wire.Done(cmd, verb, id, ("field", field), ("value", o.Field("value") ?? ""))),
            "reload" => Twin(cmd, verb, () => ReloadEvents(api), o => Wire.Done(cmd, verb, "-", Keys(o, "count"))),
            "new" => Twin(cmd, verb, () => New(api, id, field), o => Wire.Done(cmd, verb, id, Keys(o, "pillar"))),
            "copy" => Twin(cmd, verb, () => Copy(api, id, field), _ => Wire.Done(cmd, verb, field, ("from", id))),
            _ => Twin(cmd, verb, () => DeleteEvent(api, id, field == "confirm"), o => Wire.Done(cmd, verb, id, Keys(o, "confirm", "done"))),
        };
    }

    /// <summary>`.nyar api template use &lt;template&gt; [as &lt;id&gt;]` (a twin) or `.nyar api template info
    /// &lt;template&gt;` (a read, not rate-gated).</summary>
    public IReadOnlyList<string> ApiTemplate(AdminCaller who, string verb, string template, string asWord, string asId, string extra)
    {
        const string cmd = "template";
        if (verb == "info")
            return asWord.Length > 0 || asId.Length > 0 || extra.Length > 0
                ? [Wire.Error(cmd, WireError.BadArg, arg: "extra")]
                : ApiLines.TemplateInfo(ops.Templates, template);
        if (!_rate.Admit(who.Id, utcNow())) return [RateLimited(cmd, verb)];
        if (verb != "use") return [BadArg(cmd, verb, "verb")];
        var bad = Need(("template", template))
                  ?? (asWord is "" or "as" ? null : "extra")
                  ?? (asWord == "as" ? Need(("id", asId)) : Surplus(asId))
                  ?? Surplus(extra);
        if (bad is not null) return [BadArg(cmd, verb, bad)];
        var id = asWord == "as" ? asId : null;
        return [Twin(cmd, verb, () => TemplateUse(who with { Api = true }, template, id), _ => Wire.Done(cmd, verb, id ?? template, ("tpl", template)))];
    }

    /// <summary>`.nyar api templates [pillar] [page]` (a read).</summary>
    public IReadOnlyList<string> ApiTemplates(string first, string second, string extra) =>
        extra.Length > 0 ? [Wire.Error("templates", WireError.BadArg, arg: "extra")] : ApiLines.Templates(ops.Templates, first, second);

    /// <summary>`.nyar api pillar &lt;name&gt; on|off` (a twin, verb set) or `.nyar api pillar list` (a read).</summary>
    public IReadOnlyList<string> ApiPillar(AdminCaller who, string name, string state, string extra)
    {
        const string cmd = "pillar", verb = "set";
        if (name == "list")
            return state.Length > 0 || extra.Length > 0 ? [Wire.Error(cmd, WireError.BadArg, arg: "extra")] : ApiLines.Pillars(ops.PillarOn);
        if (!_rate.Admit(who.Id, utcNow())) return [RateLimited(cmd, verb)];
        var bad = Need(("pillar", name), ("state", state)) ?? Surplus(extra);
        if (bad is not null) return [BadArg(cmd, verb, bad)];
        var id = PillarNames.TryParse(name, out var p) ? PillarNames.Name(p) : name;
        return [Twin(cmd, verb, () => Pillar(who with { Api = true }, name, state), o => Wire.Done(cmd, verb, id, Keys(o, "on", "changed", "ended")))];
    }

    /// <summary>`.nyar api purge [confirm]`: the ask (verb ask) or the confirm (verb confirm).</summary>
    public string ApiPurge(AdminCaller who, string confirm, string extra)
    {
        const string cmd = "purge";
        var verb = confirm.Length == 0 ? "ask" : confirm;
        if (!_rate.Admit(who.Id, utcNow())) return RateLimited(cmd, verb);
        if (Surplus(extra) is { } bad) return BadArg(cmd, verb, bad);                // the argument count first, as for every twin
        if (confirm is not ("" or "confirm")) return BadArg(cmd, verb, "confirm");
        var api = who with { Api = true };
        return confirm.Length == 0
            ? Twin(cmd, verb, () => PurgeAsk(api), o => Wire.Done(cmd, verb, "-", Keys(o, "confirm")))
            : Twin(cmd, verb, () => PurgeConfirm(api), o => Wire.Done(cmd, verb, "-", Keys(o, "events", "units", "secs")));
    }

    /// <summary>`.nyar api killswitch` (a read).</summary>
    public IReadOnlyList<string> ApiKillSwitch(string extra)
    {
        if (extra.Length > 0) return [Wire.Error("killswitch", WireError.BadArg, arg: "extra")];
        var (until, events, units) = ops.KillSwitch;
        return ApiLines.KillSwitch(until, events, units, utcNow());
    }

    /// <summary>Runs a twin's flow and answers its one line: <paramref name="ok"/>'s on success, the refusal's code
    /// otherwise; a throw answers io internal and is logged once per failure streak of its "cmd verb" (D12, A7).</summary>
    string Twin(string cmd, string verb, Func<Outcome> run, Func<Outcome, string> ok)
    {
        var key = $"{cmd} {verb}";
        Outcome o;
        try { o = run(); }
        catch (Exception ex)
        {
            if (_failing.Add(key)) _warn($"api {key} failed: {ex.GetType().Name}");
            return Wire.Refusal(cmd, verb, WireError.Io, reason: Reasons.Internal);
        }
        _failing.Remove(key);
        return o.Ok ? ok(o) : Wire.Refusal(cmd, verb, Wire.Code(o.Code!.Value), o.Secs, o.Arg, o.Reason);
    }

    static string RateLimited(string cmd, string verb) => Wire.Refusal(cmd, verb, WireError.RateLimit, RateGate.RetrySeconds);

    static string BadArg(string cmd, string verb, string arg) => Wire.Refusal(cmd, verb, WireError.BadArg, arg: arg);

    /// <summary>The name of the first missing argument, or null.</summary>
    static string? Need(params (string Name, string Value)[] args)
    {
        foreach (var (name, value) in args) if (value.Length == 0) return name;
        return null;
    }

    /// <summary>"extra" when a word the verb does not take was given, or null.</summary>
    static string? Surplus(params string[] words) => words.Any(w => w.Length > 0) ? "extra" : null;

    /// <summary>The outcome's wire keys named, in this order, skipping those it lacks.</summary>
    static (string Key, string Value)[] Keys(Outcome o, params string[] keys) =>
        keys.Where(k => o.Field(k) is not null).Select(k => (k, o.Field(k)!)).ToArray();
}
