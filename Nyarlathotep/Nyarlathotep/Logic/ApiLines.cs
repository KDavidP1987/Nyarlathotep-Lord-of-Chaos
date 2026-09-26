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

    /// <summary>The contract's trigger name.</summary>
    public static string Trigger(TriggerType type) => type switch
    {
        TriggerType.Schedule => "schedule",
        TriggerType.GameTime => "ingame",
        TriggerType.VBloodKilled => "vbloodkilled",
        _ => "manual",
    };

    /// <summary>`api status`: one row per active event, then one per event whose units still wait out the grace
    /// (state=ending), then `[NYAR:end] cmd=status count=`. <paramref name="unitsByEvent"/> counts tracked units per
    /// event id; the count is shown only when <paramref name="isAdmin"/>.</summary>
    public static IReadOnlyList<string> Status(IEnumerable<ActiveEvent> active, IEnumerable<Cleanup> cleanups, DefinitionSet current,
        IReadOnlyDictionary<string, int> unitsByEvent, bool isAdmin, DateTime utcNow)
    {
        var rows = new List<string>();
        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in active.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            activeIds.Add(a.Id);
            var def = a.Definition;
            rows.Add(Wire.Event(a.Id, Kind(def.Pillar), def.Name, "active", "-", SecondsLeft(a.Instance.EndsUtc, utcNow),
                $"{a.WavesSpawned}/{def.Action?.Waves ?? 0}", Units(a.Id)));
        }
        // One ending row per event id, for its latest cleanup; an id that is active again shows only its active row.
        foreach (var c in cleanups.Where(c => !activeIds.Contains(c.EventId)).GroupBy(c => c.EventId)
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

    /// <summary>`api events`: one row per definition of <paramref name="set"/>, in id order. state is active while the
    /// event runs, else disabled when it is not startable, else scheduled when its trigger is automatic, else idle.</summary>
    public static IReadOnlyList<string> Definitions(DefinitionSet set, IReadOnlySet<string> activeIds) =>
        set.All.Select(d => Wire.Def(d.Id, d.Name, d.Enabled, Trigger(d.Trigger.Type), d.Action is null ? "-" : "waves",
            d.DurationSeconds, State(d, activeIds), d.Startable ? null : d.DisabledReason ?? "disabled")).ToList();

    public static string State(EventDefinition d, IReadOnlySet<string> activeIds) =>
        activeIds.Contains(d.Id) ? "active"
        : !d.Startable ? "disabled"
        : d.Trigger.Type != TriggerType.Manual ? "scheduled"
        : "idle";

    /// <summary>Whole seconds until <paramref name="atUtc"/>, rounded up, never below 0.</summary>
    public static int SecondsLeft(DateTime atUtc, DateTime utcNow) => Math.Max(0, (int)Math.Ceiling((atUtc - utcNow).TotalSeconds));
}
