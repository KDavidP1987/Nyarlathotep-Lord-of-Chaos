#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// EventDefinition v1 (docs/dod/foundation.md › Design › Data). Everything under Logic/ is pure C#: no Unity,
// Il2Cpp or BepInEx types, so Nyarlathotep.Tests can compile these files directly.

public enum Pillar { Empowerment, Spawns, Boss, Zones, Sieges }

public enum TriggerType { Manual, Schedule, GameTime, VBloodKilled }

public enum DayPhase { Day, Night }

public enum GameMode { Any, Pve, Pvp }

public enum LocationType { Point, Admin }

public sealed record Trigger(
    TriggerType Type,
    IReadOnlyList<DayOfWeek> Days,
    IReadOnlyList<TimeOnly> Times,
    DayPhase Phase,
    IReadOnlyList<string> Bosses)
{
    public static Trigger Manual() => new(TriggerType.Manual, [], [], DayPhase.Night, []);
}

public sealed record TimeWindow(TimeOnly From, TimeOnly To);

public sealed record Conditions(
    int MinPlayers = 0,
    int CooldownMinutes = 0,
    int ChancePercent = 100,
    TimeWindow? Window = null,
    GameMode Mode = GameMode.Any);

public sealed record UnitEntry(string Prefab, int Count);

public sealed record Location(LocationType Type, float X, float Z);

public sealed record SpawnWavesAction(
    IReadOnlyList<UnitEntry> Units,
    int Waves,
    int IntervalSeconds,
    int Radius,
    Location Location,
    int? UnitLifetimeSeconds);

public sealed record Announce(IReadOnlyList<string> Start, IReadOnlyList<string> End, bool Warnings)
{
    public static readonly Announce None = new([], [], false);
}

/// <summary>One definition as loaded. <see cref="DisabledReason"/> is set when validation disabled it;
/// the definition is then kept for `.nyar event list` but never started.</summary>
public sealed record EventDefinition(
    string Id,
    string Name,
    bool Enabled,
    Pillar Pillar,
    Trigger Trigger,
    Conditions Conditions,
    int DurationSeconds,
    SpawnWavesAction? Action,
    Announce Announce,
    string? DisabledReason = null)
{
    public bool Startable => Enabled && DisabledReason is null;
}

/// <summary>The immutable set produced by one load. A reload builds a new set; running instances keep the
/// definition object they started with (D6).</summary>
public sealed class DefinitionSet
{
    public static readonly DefinitionSet Empty = new([]);

    readonly Dictionary<string, EventDefinition> _byId = new(StringComparer.Ordinal);

    public DefinitionSet(IEnumerable<EventDefinition> definitions)
    {
        // A duplicate id stays listed (disabled by validation) but Find returns the first.
        var list = definitions.ToList();
        foreach (var d in list) _byId.TryAdd(d.Id, d);
        All = list.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<EventDefinition> All { get; }

    public EventDefinition? Find(string id) => _byId.TryGetValue(id, out var d) ? d : null;
}
