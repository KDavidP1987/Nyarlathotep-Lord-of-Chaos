#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// EventDefinition v1 (docs/dod/foundation.md › Design › Data). Everything under Logic/ is pure C#: no Unity,
// Il2Cpp or BepInEx types, so Nyarlathotep.Tests can compile these files directly.

public enum Pillar { Empowerment, Spawns, Boss, Zones, Sieges }

/// <summary>How a definition starts. Interval, RegionEntered and FactionKills are automation's (docs/dod/automation.md D1, D8,
/// D10): a random clock, a player walking into a region, and kills of listed factions.</summary>
public enum TriggerType { Manual, Schedule, GameTime, VBloodKilled, Interval, RegionEntered, FactionKills }

public enum DayPhase { Day, Night }

public enum GameMode { Any, Pve, Pvp }

public enum LocationType { Point, Admin, AroundPlayer }

/// <summary>A trigger. Each type reads only its own fields: Interval <see cref="MinMinutes"/>..<see cref="MaxMinutes"/>;
/// RegionEntered <see cref="PlayerCooldownMinutes"/> and its <see cref="Scope"/>; FactionKills <see cref="Factions"/>,
/// <see cref="Kills"/> within <see cref="WindowSeconds"/>, per player or <see cref="Shared"/> (automation D1, D8, D10).</summary>
public sealed record Trigger(
    TriggerType Type,
    IReadOnlyList<DayOfWeek> Days,
    IReadOnlyList<TimeOnly> Times,
    DayPhase Phase,
    IReadOnlyList<string> Bosses,
    Scope Scope = default,
    int MinMinutes = 0,
    int MaxMinutes = 0,
    int PlayerCooldownMinutes = 0,
    IReadOnlyList<string>? Factions = null,
    int Kills = 0,
    int WindowSeconds = 0,
    bool Shared = false)
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

/// <summary>One unit entry of a wave; each of its <see cref="Count"/> copies spawns with probability
/// <see cref="Chance"/> (event-spawns D8), 1.0 when absent. <see cref="Modifiers"/> is set only on an entry of a
/// waveList wave (wave-sets D1, D3): that entry's own level and multipliers.</summary>
public sealed record UnitEntry(string Prefab, int Count, double Chance = 1.0, SpawnModifiers? Modifiers = null);

/// <summary>One wave of a waveList (wave-sets D1, D4): its own units, and for wave 2 onwards when it starts: after
/// <see cref="AfterSeconds"/> from the previous wave's decision, when the previous wave is cleared
/// (<see cref="WhenCleared"/>), whichever comes first, or when cleared with neither key set (design §9 D40).</summary>
public sealed record WaveSpec(IReadOnlyList<UnitEntry> Units, int? AfterSeconds = null, bool WhenCleared = false)
{
    /// <summary>The wave starts when the previous one is cleared: whenCleared, or neither key set.</summary>
    public bool WaitsForClear => WhenCleared || AfterSeconds is null;
}

/// <summary>Where a SpawnWaves action spawns. A Point's Y is the height (event-library A20); a Point stored without one
/// spawns at height 0, as before. An AroundPlayer centre is <see cref="MinDist"/>..<see cref="MaxDist"/> metres from a
/// random eligible player, picked per wave (event-spawns D16).</summary>
public sealed record Location(LocationType Type, float X, float Z, float? Y = null, int MinDist = 0, int MaxDist = 0);

/// <summary>A wave's unit modifiers (event-spawns D6, D9): an absolute <see cref="Level"/> or a <see cref="LevelDelta"/>
/// on the prefab's level (never both), and stat multipliers 0.5-3.0, 1.0 meaning unchanged.</summary>
public sealed record SpawnModifiers(
    int? Level = null,
    int? LevelDelta = null,
    double MaxHealth = 1.0,
    double Power = 1.0,
    double MoveSpeed = 1.0,
    double AttackSpeed = 1.0);

public enum BehaviourType { Hunt }

/// <summary>A wave's behaviour (event-spawns D13): Hunt seeds aggro on players within <see cref="Range"/> metres of the
/// wave centre.</summary>
public sealed record Behaviour(BehaviourType Type, int Range);

/// <summary>An AroundPlayer wave spread over up to <see cref="MaxInstances"/> players, each at least
/// <see cref="MinSpacing"/> metres from the others (automation D4, D5).</summary>
public sealed record FanOut(int MaxInstances, int MinSpacing);

public sealed record SpawnWavesAction(
    IReadOnlyList<UnitEntry> Units,
    int Waves,
    int IntervalSeconds,
    int Radius,
    Location Location,
    int? UnitLifetimeSeconds,
    Scope Scope = default,
    SpawnModifiers? Modifiers = null,
    bool Loot = false,
    Behaviour? Behaviour = null,
    bool AllowTerritory = false,
    FanOut? FanOut = null,
    IReadOnlyList<WaveSpec>? WaveList = null,
    bool Scoreboard = false)
{
    /// <summary>The units of wave <paramref name="wave"/> (1-based): its waveList entry's, or <see cref="Units"/> for
    /// every wave of the units form.</summary>
    public IReadOnlyList<UnitEntry> UnitsOf(int wave) => WaveList is { } list ? list[Math.Clamp(wave, 1, list.Count) - 1].Units : Units;
}

/// <summary>The Empower action's multipliers (faction-empowerment S-1): each 1.0–3.0 of the base value, 1.0 meaning
/// unchanged, at least one above 1.0.</summary>
public sealed partial record EmpowerStats(
    double PhysicalPower = 1.0,
    double SpellPower = 1.0,
    double MaxHealth = 1.0,
    double AttackSpeed = 1.0,
    double MoveSpeed = 1.0);

/// <summary>An Empower action (faction-empowerment D1): every NPC of <see cref="Factions"/>, plus the units named in
/// <see cref="IncludeUnits"/>, minus <see cref="ExcludeUnits"/>, gets a carrier buff with <see cref="Stats"/> while the
/// event runs. V Bloods only with <see cref="IncludeVBloods"/>. <see cref="Scope"/> limits it to the NPCs in the named
/// regions (regions D5).</summary>
public sealed record EmpowerAction(
    IReadOnlyList<string> Factions,
    IReadOnlyList<string> IncludeUnits,
    IReadOnlyList<string> ExcludeUnits,
    bool IncludeVBloods,
    EmpowerStats Stats,
    Scope Scope = default);

public sealed record Announce(IReadOnlyList<string> Start, IReadOnlyList<string> End, bool Warnings)
{
    public static readonly Announce None = new([], [], false);
}

/// <summary>One definition as loaded. <see cref="DisabledReason"/> is set when validation disabled it;
/// the definition is then kept for `.nyar event list` but never started. A valid definition carries exactly one action:
/// a SpawnWaves <see cref="Action"/> or an <see cref="Empower"/> (faction-empowerment D1, D2).</summary>
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
    string? DisabledReason = null,
    EmpowerAction? Empower = null)
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
