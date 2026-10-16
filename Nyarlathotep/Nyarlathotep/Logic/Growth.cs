#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>
/// The wave generator of a units-form action with `growth` (swarm D25; design §9 D42). Pure: the parser calls
/// <see cref="Generate"/> once per load and stores the list as SpawnWavesAction.WaveList, so WaveSchedule, WaveCleared,
/// the all-waves-defeated victory, the scoreboard and the MaxUnitsPerWave and MaxTrackedUnits clamps run on it as on a
/// hand-written waveList. The list lives in memory only and is never written to events.json: the file keeps
/// units + growth.
/// </summary>
public sealed partial record Growth
{
    /// <summary>The highest absolute level a generated entry gets (LevelArg's ceiling).</summary>
    public const int MaxLevel = 120;

    /// <summary>The count of every unit of wave <paramref name="waves"/> (the last), for validation's 50-unit ceiling.</summary>
    public static int LastWaveUnits(IReadOnlyList<UnitEntry> baseWave, int waves, int addUnits) =>
        baseWave.Sum(u => u.Count + (waves - 1) * addUnits);

    /// <summary>Waves 1 to <paramref name="waves"/> from <paramref name="baseWave"/>. Wave n holds each base entry with
    /// count + (n−1)·AddUnits and its chance unchanged, carrying <paramref name="modifiers"/> with the level raised by
    /// (n−1)·AddLevels: an absolute level capped at <see cref="MaxLevel"/>, a levelDelta or no level as levelDelta +
    /// (n−1)·AddLevels, which LevelArg resolves and clamps to 1-120 at the spawn. Wave 1 is the base wave with the
    /// action's modifiers on each entry, so SpawnTuning gives the units form's tuning; waves 2-n start
    /// <paramref name="intervalSeconds"/> after the previous one's decision, or at its clear as well with WhenCleared.</summary>
    public static IReadOnlyList<WaveSpec> Generate(IReadOnlyList<UnitEntry> baseWave, SpawnModifiers? modifiers, int waves, int intervalSeconds,
        Growth growth)
    {
        var list = new List<WaveSpec>(waves);
        for (var n = 1; n <= waves; n++)
        {
            var step = n - 1;
            var entryModifiers = Raise(modifiers, step * growth.AddLevels);
            var entries = baseWave.Select(u => u with { Count = u.Count + step * growth.AddUnits, Modifiers = entryModifiers }).ToList();   // a pool entry stays one (wave-variety D2)
            list.Add(n == 1 ? new WaveSpec(entries) : new WaveSpec(entries, intervalSeconds, growth.WhenCleared));
        }
        return list;
    }

    /// <summary><paramref name="modifiers"/> with its level raised by <paramref name="levels"/>; unchanged at 0.</summary>
    static SpawnModifiers? Raise(SpawnModifiers? modifiers, int levels)
    {
        if (levels == 0) return modifiers;
        if (modifiers is null) return new SpawnModifiers(LevelDelta: levels);
        return modifiers.Level is { } level
            ? modifiers with { Level = Math.Min(MaxLevel, level + levels) }
            : modifiers with { LevelDelta = (modifiers.LevelDelta ?? 0) + levels };
    }
}
