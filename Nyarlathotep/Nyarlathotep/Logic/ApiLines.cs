#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The rows of the api reads (docs/RAPHAEL_INTEGRATION_CONTRACT.md §3; raphael-api-core D1, D2). Nothing here
/// carries a position: no row has a coordinate, radius or player key, and the unit count goes to admins only.</summary>
public static class ApiLines
{
    /// <summary>The contract's kind for a pillar.</summary>
    public static string Kind(Pillar pillar) => pillar switch
    {
        Pillar.Empowerment => "empower",
        Pillar.Spawns => "waves",
        Pillar.Boss => "boss",
        Pillar.Zones => "zone",
        _ => "siege",
    };

    /// <summary>The contract's action name for a definition: its pillar's action, so a definition that validation
    /// disabled for a missing action still names one of the contract's values.</summary>
    public static string ActionName(Pillar pillar) => pillar switch
    {
        Pillar.Empowerment => "empower",
        Pillar.Boss => "boss",
        Pillar.Sieges => "siege",
        _ => "waves",
    };

    /// <summary>The contract's trigger name.</summary>
    public static string Trigger(TriggerType type) => type switch
    {
        TriggerType.Schedule => "schedule",
        TriggerType.GameTime => "ingame",
        TriggerType.VBloodKilled => "vbloodkilled",
        _ => "manual",
    };

    /// <summary>`api status`: one row per active event, then one per event whose units still wait out the grace
    /// (state=ending), then `[NYAR:end] cmd=status count=`. <paramref name="unitsByEvent"/> counts, per event id, the
    /// tracked units of a waves event and the NPCs holding an Empower event's carrier; the count is shown only when
    /// <paramref name="isAdmin"/>. An Empower row (api 3, faction-empowerment D11) carries its factions and wave=-.</summary>
    public static IReadOnlyList<string> Status(IEnumerable<ActiveEvent> active, IEnumerable<Cleanup> cleanups, DefinitionSet current,
        IReadOnlyDictionary<string, int> unitsByEvent, bool isAdmin, DateTime utcNow)
    {
        var rows = new List<string>();
        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in active.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            activeIds.Add(a.Id);
            var def = a.Definition;
            rows.Add(def.Empower is { } emp
                ? Wire.Event(a.Id, "empower", def.Name, "active", Factions(emp), SecondsLeft(a.Instance.EndsUtc, utcNow), "-", Units(a.Id))
                : Wire.Event(a.Id, Kind(def.Pillar), def.Name, "active", "-", SecondsLeft(a.Instance.EndsUtc, utcNow),
                    $"{a.WavesSpawned}/{def.Action?.Waves ?? 0}", Units(a.Id)));
        }
        // One ending row per event id, for its latest cleanup still in the future; an id that is active again shows
        // only its active row, and a cleanup already due (removed on this tick) shows none.
        foreach (var c in cleanups.Where(c => c.DueUtc > utcNow && !activeIds.Contains(c.EventId)).GroupBy(c => c.EventId)
                     .Select(g => g.OrderByDescending(c => c.DueUtc).First()).OrderBy(c => c.EventId, StringComparer.Ordinal))
        {
            var def = current.Find(c.EventId);
            rows.Add(Wire.Event(c.EventId, def is null ? "waves" : Kind(def.Pillar), def?.Name ?? c.EventId, "ending", "-",
                SecondsLeft(c.DueUtc, utcNow), "-", Units(c.EventId)));
        }
        rows.Add(Wire.End("status", rows.Count));
        return rows;

        int? Units(string id) => isAdmin ? (unitsByEvent.TryGetValue(id, out var n) ? n : 0) : null;
    }

    /// <summary>The faction value of an Empower row: each faction without its "Faction_" prefix, as a wire value, joined
    /// by ',' (e.g. Legion,Bandits).</summary>
    public static string Factions(EmpowerAction action) =>
        string.Join(",", action.Factions.Select(f => TextSink.WireValue(f.StartsWith("Faction_", StringComparison.Ordinal) ? f[8..] : f)));

    /// <summary>`api events`: one row per definition of <paramref name="set"/>, in id order. reason is sent only with
    /// state=disabled: the validation error, or "disabled" for a definition switched off.</summary>
    public static IReadOnlyList<string> Definitions(DefinitionSet set, IReadOnlySet<string> activeIds) =>
        set.All.Select(d =>
        {
            var state = State(set, d, activeIds);
            return Wire.Def(d.Id, d.Name, d.Enabled, Trigger(d.Trigger.Type), ActionName(d.Pillar), d.DurationSeconds, state,
                state == "disabled" ? d.DisabledReason ?? "disabled" : null);
        }).ToList();

    /// <summary>active while the event runs, else disabled when it is not startable, else scheduled when its trigger is
    /// automatic, else idle. Only the definition <see cref="DefinitionSet.Find"/> returns can be the running one: a
    /// duplicate of its id (which validation disabled) stays disabled.</summary>
    public static string State(DefinitionSet set, EventDefinition d, IReadOnlySet<string> activeIds) =>
        activeIds.Contains(d.Id) && ReferenceEquals(set.Find(d.Id), d) ? "active"
        : !d.Startable ? "disabled"
        : d.Trigger.Type != TriggerType.Manual ? "scheduled"
        : "idle";

    // ---- the admin reads (raphael-api-admin D7): adminOnly, never rate-gated, changing nothing

    /// <summary>One `[NYAR:tpl]` row: the template's id, pillar name, trigger, duration and name as its summary.</summary>
    public static string Tpl(TemplateEntry t) =>
        Wire.Tpl(t.Id, PillarNames.Name(t.Definition.Pillar), Trigger(t.Definition.Trigger.Type), t.Definition.DurationSeconds, t.Definition.Name);

    /// <summary>`api templates [pillar] [page]`: the rows of one page, filtered by pillar name when given, then
    /// `[NYAR:end] cmd=templates page= count=`. One word is a page when it is digits, else a pillar; two words are a
    /// pillar then a page. An unknown pillar is notfound, an unavailable catalogue io read (A11), a bad page badarg.</summary>
    public static IReadOnlyList<string> Templates(TemplateCatalog catalog, string first, string second)
    {
        const string cmd = "templates";
        if (catalog.Error is not null) return [Wire.Error(cmd, WireError.Io, reason: Reasons.Read)];
        string? pillarWord = null, pageWord = null;
        if (second.Length > 0) { pillarWord = first; pageWord = second; }
        else if (first.Length > 0) { if (first.All(c => c is >= '0' and <= '9')) pageWord = first; else pillarWord = first; }
        Pillar? pillar = null;
        if (pillarWord is not null)
        {
            if (!PillarNames.TryParse(pillarWord, out var p)) return [Wire.Error(cmd, WireError.NotFound, arg: "pillar")];
            pillar = p;
        }
        var rows = catalog.Templates.Where(t => pillar is null || t.Definition.Pillar == pillar).Select(Tpl).ToList();
        return Paging.Reply(cmd, rows, pageWord);
    }

    /// <summary>`api template info &lt;template&gt;`: its row, then `[NYAR:end] cmd=template count=1`.</summary>
    public static IReadOnlyList<string> TemplateInfo(TemplateCatalog catalog, string id)
    {
        const string cmd = "template";
        if (catalog.Error is not null) return [Wire.Error(cmd, WireError.Io, reason: Reasons.Read)];
        if (id.Length == 0) return [Wire.Error(cmd, WireError.BadArg, arg: "template")];
        return catalog.Find(id) is { } t ? [Tpl(t), Wire.End(cmd, 1)] : [Wire.Error(cmd, WireError.NotFound, arg: "template")];
    }

    /// <summary>`api pillar list`: one `[NYAR:pillar]` row per pillar in PillarNames order, then the end line.</summary>
    public static IReadOnlyList<string> Pillars(Func<Pillar, bool> on)
    {
        var rows = PillarNames.All.Select(p => Wire.Pillar(p.Name, on(p.Pillar))).ToList();
        rows.Add(Wire.End("pillar", rows.Count));
        return rows;
    }

    /// <summary>`api killswitch`: on=1 and the seconds left while the purge cooldown runs, the active events and the
    /// tracked units, then the end line.</summary>
    public static IReadOnlyList<string> KillSwitch(DateTime? purgeUntilUtc, int events, int units, DateTime utcNow)
    {
        var secs = purgeUntilUtc is { } until ? SecondsLeft(until, utcNow) : 0;
        return [Wire.Ks(secs > 0, secs, events, units), Wire.End("killswitch", 1)];
    }

    /// <summary>Whole seconds until <paramref name="atUtc"/>, rounded up, never below 0.</summary>
    public static int SecondsLeft(DateTime atUtc, DateTime utcNow) => Math.Max(0, (int)Math.Ceiling((atUtc - utcNow).TotalSeconds));
}
