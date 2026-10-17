#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// EventDefinition v1 (docs/dod/foundation.md › Design › Data). Everything under Logic/ is pure C#: no Unity,
// Il2Cpp or BepInEx types, so Nyarlathotep.Tests can compile these files directly.

public enum Pillar { Empowerment, Spawns, Boss, Zones, Sieges }

/// <summary>How a definition starts. Interval, RegionEntered and FactionKills are automation's (docs/dod/automation.md D1, D8,
/// D10): a random clock, a player walking into a region, and kills of listed factions. Revenge (revenge D1) is per-player
/// heat from kills of listed factions, which starts an ambush around that player (Logic/Revenge.cs).</summary>
public enum TriggerType { Manual, Schedule, GameTime, VBloodKilled, Interval, RegionEntered, FactionKills, Revenge }

public enum DayPhase { Day, Night }

public enum GameMode { Any, Pve, Pvp }

/// <summary>Region (hunting-grounds D1, D4) spreads each wave over the action's scope regions; Logic/HuntingGrounds.cs
/// draws its spots.</summary>
public enum LocationType { Point, Admin, AroundPlayer, Region }

/// <summary>A trigger. Each type reads only its own fields: Interval <see cref="MinMinutes"/>..<see cref="MaxMinutes"/>;
/// RegionEntered <see cref="PlayerCooldownMinutes"/> and its <see cref="Scope"/>; FactionKills <see cref="Factions"/>,
/// <see cref="Kills"/> within <see cref="WindowSeconds"/>, per player or <see cref="Shared"/> (automation D1, D8, D10); Revenge
/// <see cref="Factions"/>, <see cref="Heat"/> within <see cref="WindowSeconds"/>, <see cref="WarnAt"/>, <see cref="VBloodHeat"/>
/// and <see cref="PlayerCooldownMinutes"/> (revenge D1).</summary>
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
    bool Shared = false,
    int Heat = 0,
    IReadOnlyList<int>? WarnAt = null,
    int VBloodHeat = 0)
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
/// waveList wave (wave-sets D1, D3): that entry's own level and multipliers. A pool entry (wave-variety D2) names
/// <see cref="Pool"/> and has an empty <see cref="Prefab"/>: Logic/Pools.cs draws its count from the pool at the wave's
/// decision, so everything after the roll sees prefab entries only.</summary>
public sealed record UnitEntry(string Prefab, int Count, double Chance = 1.0, SpawnModifiers? Modifiers = null, string? Pool = null)
{
    /// <summary>A pool entry of <paramref name="count"/> draws from <paramref name="pool"/>.</summary>
    public static UnitEntry FromPool(string pool, int count, double chance = 1.0, SpawnModifiers? modifiers = null) =>
        new("", count, chance, modifiers, pool);
}

/// <summary>One boss of a wave (wave-variety D3): a fixed boss, <see cref="Prefab"/> times <see cref="Count"/> (revenge
/// D28's entry), or a roll of <see cref="Min"/>..<see cref="Max"/> distinct bosses from <see cref="Pool"/>, whose
/// <see cref="Prefab"/> is empty and <see cref="Count"/> 0. A fixed entry converts from a UnitEntry, as 0.12.0 stored it.</summary>
public sealed record BossRoll(string Prefab, string? Pool, int Count, int Min, int Max, SpawnModifiers? Modifiers = null)
{
    public bool IsRoll => Pool is not null;

    /// <summary>The most bosses this entry can add: a fixed count, or a roll's maximum.</summary>
    public int MostBosses => IsRoll ? Max : Count;

    public static BossRoll Fixed(string prefab, int count, SpawnModifiers? modifiers = null) => new(prefab, null, count, 0, 0, modifiers);

    public static BossRoll Roll(string pool, int min, int max, SpawnModifiers? modifiers = null) => new("", pool, 0, min, max, modifiers);

    public static implicit operator BossRoll(UnitEntry e) => Fixed(e.Prefab, e.Count, e.Modifiers);
}

/// <summary>One wave of a waveList (wave-sets D1, D4): its own units, and for wave 2 onwards when it starts: after
/// <see cref="AfterSeconds"/> from the previous wave's decision, when the previous wave is cleared
/// (<see cref="WhenCleared"/>), whichever comes first, or when cleared with neither key set (design §9 D40).
/// <see cref="Bosses"/> are the wave's own bosses (wave-variety D3), null without the key.</summary>
public sealed record WaveSpec(IReadOnlyList<UnitEntry> Units, int? AfterSeconds = null, bool WhenCleared = false,
    IReadOnlyList<BossRoll>? Bosses = null)
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

public enum BehaviourType { Hunt, RegionHunt }

/// <summary>A wave's behaviour (event-spawns D13): Hunt seeds aggro on players within <see cref="Range"/> metres of the
/// wave centre. RegionHunt (hunting-grounds D3, D7, D8) carries no range: each group goes after the nearest eligible
/// player inside the scope regions (Logic/HuntingGrounds.cs); its <see cref="Range"/> is 0.</summary>
public sealed record Behaviour(BehaviourType Type, int Range);

/// <summary>An AroundPlayer wave spread over up to <see cref="MaxInstances"/> players, each at least
/// <see cref="MinSpacing"/> metres from the others (automation D4, D5).</summary>
public sealed record FanOut(int MaxInstances, int MinSpacing);

/// <summary>The circle a SpawnWaves action's units are kept in (swarm D1, D3): <see cref="Radius"/> 15-60 m around the
/// Point location's x and z, or the starting admin's position for an Admin location. The rule is Logic/Arena.cs.</summary>
public sealed partial record Arena(int Radius);

/// <summary>Who an event with a scoreboard names as its winner (swarm D2, D5): nobody, or the best kill-to-death ratio.</summary>
public enum WinnerRule { None, KillDeath }

/// <summary>How a units-form action's waves grow from its one base wave (swarm D24, D25; design §9 D42): each later wave
/// adds <see cref="AddUnits"/> to every entry's count and <see cref="AddLevels"/> to its level, and starts after
/// intervalSeconds or, with <see cref="WhenCleared"/>, when the previous wave is cleared as well. The generator is
/// Logic/Growth.cs.</summary>
public sealed partial record Growth(int AddUnits, int AddLevels, bool WhenCleared);

/// <summary>A SpawnWaves action. <see cref="WaveList"/> is the waveList form's list (wave-sets D1) or, for a units-form
/// action with <see cref="Growth"/>, the list Growth.Generate made from the base wave (swarm D25): <see cref="Units"/>,
/// <see cref="Waves"/>, <see cref="IntervalSeconds"/> and <see cref="Modifiers"/> then stay the base the file holds, and
/// everything after the parser reads the generated list. <see cref="Arena"/> and <see cref="Winner"/> are swarm D1 and
/// D2. <see cref="RegionLock"/> keeps the units inside the scope regions (hunting-grounds D2, D5); false is 0.10.0's
/// behaviour. <see cref="Bosses"/> join the final wave only (revenge D28, D29; design §9 D51), null without the key.
/// <see cref="Pools"/> are the named unit pools its pool entries and boss rolls draw from, and <see cref="Pursuit"/> lets a
/// run chase players who left its area (wave-variety D1, D7), each null without the key.</summary>
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
    bool Scoreboard = false,
    Arena? Arena = null,
    WinnerRule Winner = WinnerRule.None,
    Growth? Growth = null,
    bool RegionLock = false,
    IReadOnlyList<BossRoll>? Bosses = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Pools = null,
    Pursuit? Pursuit = null,
    Destination? Destination = null,
    double MarchSpeed = DestinationLimits.DefaultMarchSpeed)
{
    /// <summary>The units of wave <paramref name="wave"/> (1-based): its waveList entry's, or <see cref="Units"/> for
    /// every wave of the units form.</summary>
    public IReadOnlyList<UnitEntry> UnitsOf(int wave) => WaveList is { } list ? list[Math.Clamp(wave, 1, list.Count) - 1].Units : Units;
}

/// <summary>Where a destination event's waves march (undead-hordes D5): from the action's location at MarchSpeed m/s to
/// <see cref="Location"/> (Point, or Admin: the starting admin's position); within <see cref="Radius"/> m of it a wave takes
/// the action's behaviour. <see cref="Name"/> is shown to players.</summary>
public sealed record Destination(Location Location, int Radius, string Name);

/// <summary>A SpawnWaves run's pursuit (wave-variety D7, D8): once no eligible player has stood in its area for
/// <see cref="AfterSeconds"/> (15-600) in a row, its units go after players anywhere. The rule is Logic/Pursuit.cs.</summary>
public sealed partial record Pursuit(int AfterSeconds);

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

/// <summary>Where an apocalypse's natives cluster (apocalypse D2): up to <see cref="Count"/> 100 m cells of at least
/// <see cref="MinNatives"/> eligible natives, each at least <see cref="MinSpacing"/> metres from the others.</summary>
public sealed record ApocalypseHotspots(int Count, int MinNatives, int MinSpacing);

/// <summary>An apocalypse's roamers (apocalypse D3, D4): <see cref="PerHotspot"/> topped up at each hotspot,
/// <see cref="PerKill"/>..<see cref="PerKillMax"/> spread on each qualifying kill, at most <see cref="Max"/> alive and
/// <see cref="PerMinute"/> spread in any 60 s. <see cref="HuntRange"/> is the Hunt behaviour's range once the mover has
/// seeded aggro (Review 5 F2). A 0.13.x `perKill: n` is n..n; `perKillMin`/`perKillMax` and <see cref="Pool"/> are
/// undead-hordes D7: the spread draws from that pool, else from the units as before.</summary>
public sealed record ApocalypseRoamers(int PerHotspot, int PerKill, int Max, int PerMinute, int HuntRange, int? KillMax = null, string? Pool = null)
{
    public int PerKillMin => PerKill;
    public int PerKillMax => KillMax ?? PerKill;
}

/// <summary>An apocalypse's hordes (apocalypse D5): <see cref="Count"/> hordes, the first <see cref="FirstAfterMinutes"/>
/// after the start and one every <see cref="EveryMinutes"/> after it, horde k holding <see cref="Size"/> + (k − 1) ×
/// <see cref="Growth"/> units.</summary>
public sealed record ApocalypseHordes(int Count, int FirstAfterMinutes, int EveryMinutes, int Size, int Growth);

/// <summary>Where the hordes march (apocalypse D6, D8): a Point location, or Admin (the starting admin's position, read
/// once at the start; Manual trigger only). A front within <see cref="Radius"/> m loses the run; <see cref="Name"/> is the
/// place's name in the chat lines. A HordeMarch's destination is its goal (undead-hordes D10); an Infestation has none.</summary>
public sealed record ApocalypseGoal(Location Location, int Radius, string Name)
{
    /// <summary>An Infestation's goal: none; nothing reads it.</summary>
    public static readonly ApocalypseGoal None = new(new Location(LocationType.Point, 0, 0), 0, "");
}

/// <summary>The apocalypse family (undead-hordes D8, D10, D13): one engine, three kinds of run. An Infestation rises at the
/// hotspots (its origins) and spreads; a HordeMarch rises as one horde, marches to its destination and infests there.</summary>
public enum ApocalypseMode { Apocalypse, Infestation, HordeMarch }

/// <summary>How an infestation's units move (undead-hordes D9): <see cref="Hunters"/> percent of them hunt the nearest
/// eligible player anywhere, the rest drift up to <see cref="DriftRadius"/> m from their origin.</summary>
public sealed record InfestRule(int Hunters, int DriftRadius);

/// <summary>Where a horde march rises (undead-hordes D10): a Point or the starting admin's position (<see cref="Location"/>),
/// or the faction-native hotspot nearest the destination at least <see cref="MinDistance"/> m from it (Location null).</summary>
public sealed record HordeStart(Location? Location, int MinDistance = 0)
{
    public bool IsHotspot => Location is null;
}

/// <summary>A HordeMarch's own parts (undead-hordes D10, D11): its start; its growth (a spread block whose units join the
/// horde); after arrival its infest rule and spread block; its bosses, fixed or rolled, which join the horde at the rise.</summary>
public sealed record HordeMarchSpec(HordeStart Start, ApocalypseRoamers Growth, InfestRule Infest, ApocalypseRoamers InfestSpread,
    IReadOnlyList<BossRoll>? Bosses = null);

/// <summary>An Apocalypse action (apocalypse D1): hotspots found among <see cref="Faction"/>'s natives in
/// <see cref="Scope"/>, roamers and hordes drawn from <see cref="Units"/> by weight (count) and chance, hordes marching to
/// <see cref="Goal"/> at <see cref="MarchSpeed"/> m/s; <see cref="Bosses"/> join the last horde only (D36), null without
/// the key. Its rules are Logic/Apocalypse.cs. An Infestation (undead-hordes D8) holds its origins as the hotspots, its
/// perOrigin as roamers.perHotspot, its spread block as the roamers' draw, max and minute limit, no hordes and
/// <see cref="Infest"/>; a HordeMarch (D10) one horde of its units' counts, its destination as the goal and
/// <see cref="March"/>.</summary>
public sealed record ApocalypseAction(
    string Faction,
    IReadOnlyList<UnitEntry> Units,
    ApocalypseHotspots Hotspots,
    ApocalypseRoamers Roamers,
    ApocalypseHordes Hordes,
    ApocalypseGoal Goal,
    double MarchSpeed,
    Scope Scope = default,
    bool Scoreboard = true,
    IReadOnlyList<UnitEntry>? Bosses = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Pools = null,
    ApocalypseMode Mode = ApocalypseMode.Apocalypse,
    InfestRule? Infest = null,
    HordeMarchSpec? March = null)
{
    /// <summary>The JSON type of the action: Apocalypse, Infestation or HordeMarch.</summary>
    public string Type => Mode.ToString();
}

public sealed record Announce(IReadOnlyList<string> Start, IReadOnlyList<string> End, bool Warnings)
{
    public static readonly Announce None = new([], [], false);
}

/// <summary>One definition as loaded. <see cref="DisabledReason"/> is set when validation disabled it;
/// the definition is then kept for `.nyar event list` but never started. A valid definition carries exactly one action:
/// a SpawnWaves <see cref="Action"/>, an <see cref="Empower"/> (faction-empowerment D1, D2) or an
/// <see cref="Apocalypse"/> (apocalypse D1).</summary>
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
    EmpowerAction? Empower = null,
    ApocalypseAction? Apocalypse = null)
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
