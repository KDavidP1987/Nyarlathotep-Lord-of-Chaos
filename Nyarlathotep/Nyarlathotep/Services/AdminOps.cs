using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// The game side of the admin flows (raphael-api-admin D1, D3, D11): one call per member into EventRuntime,
/// EventStore, TemplateLibrary, PillarSwitches and SpawnTracker. No member decides anything (the OutcomeReturns check:
/// one expression, no branch); the decisions live in Logic/AdminFlows, where the tests reach them.
/// </summary>
internal sealed class AdminOps : IAdminOps
{
    internal static readonly AdminOps Instance = new();

    public DefinitionSet Definitions => EventStore.Catalog.Current;

    public TemplateCatalog Templates => TemplateLibrary.Catalog;

    public bool PillarOn(Pillar pillar) => PillarSwitches.On(pillar);

    public (DateTime? PurgeUntilUtc, int Events, int Units) KillSwitch =>
        (Persistence.State.Document.PurgeUntilUtc, EventRuntime.Engine.Active.Count, SpawnTracker.Ledger.Units.Count);

    [Mutating]
    public Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin, string focus = null) =>
        EventRuntime.StartEvent(id, "manual", Actor.Admin, origin, focus: focus);

    [Mutating]
    public Outcome OpStopEvent(string id) => EventRuntime.StopEvent(id);

    [Mutating]
    public Outcome OpPurge() => EventRuntime.Purge();

    public (int Events, int Units) OpPurgeCounts() => (EventRuntime.Engine.Active.Count, SpawnTracker.Ledger.Purgeable);

    [Mutating]
    public Outcome OpEdit(string id, string path, object value) => EventStore.Edit(id, path, value);

    [Mutating]
    public Outcome OpReload() => EventStore.Reload();

    [Mutating]
    public Outcome OpAuthor(Func<string, EditPlan> plan) => EventStore.Author(plan);

    [Mutating]
    public Outcome OpDelete(ulong adminId, string id, bool confirm) => EventStore.DeleteDefinition(adminId, id, confirm);

    [Mutating]
    public Outcome OpUseTemplate(string template, string asId) => TemplateLibrary.UseTemplate(template, asId);

    [Mutating]
    public Outcome OpSetPillar(string name, string state) => PillarSwitches.SetPillar(name, state);

    /// <summary>`.nyar event unstick &lt;id&gt;` and its twin (unit-watch D12, D13): marks the running instance's units that
    /// have not fought; the watch phase moves them.</summary>
    [Mutating]
    public Outcome OpUnstick(string id) => SpawnTracker.Unstick(id);
}
