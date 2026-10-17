#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>A wave's roll (wave-variety D5): its <see cref="Units"/> as the wave queues them, bosses first
/// (<see cref="Lead"/> of them, so the caps cut the other units first, design §9 D51), every pool entry replaced by the
/// prefabs it drew (count 1 each, the entry's chance and modifiers kept for WaveRoll's per-copy roll), every boss roll
/// by the bosses it drew. <see cref="Rolled"/> is true when the wave holds a pool entry or a boss roll;
/// <see cref="Bosses"/> are the wave's bosses, fixed then rolled, for the "rolled:" line.</summary>
public sealed record WaveDraw(IReadOnlyList<UnitEntry> Units, int Lead, bool Rolled, IReadOnlyList<UnitEntry> Bosses)
{
    /// <summary>The per-copy chance roll of the drawn units (event-spawns D8): a fanned-out wave calls it once per group,
    /// so its groups share one draw and each rolls its own chances.</summary>
    public IReadOnlyList<UnitEntry> Expand(IRandom rng) => WaveRoll.ExpandEntries(Units, rng);
}

/// <summary>
/// The pool roll (wave-variety D5). Services/WaveAction calls <see cref="Resolve"/> once at a wave's decision, before the
/// caps, and queues, counts and shows that draw; nothing re-rolls it. A wave without pool entries or boss rolls draws no
/// random number and gives 0.12.0's list and lead unchanged (the final wave's fixed bosses, then its units). The random source is injected, so
/// the tests use a fixed seed.
/// </summary>
public static class Pools
{
    /// <summary>The bosses wave <paramref name="wave"/> (1-based) adds: its own `bosses` (a waveList wave), else the
    /// action-level list on the final wave (revenge D28; validation never lets both name the last wave).</summary>
    public static IReadOnlyList<BossRoll>? BossesOf(SpawnWavesAction action, int wave)
    {
        if (action.WaveList is { Count: > 0 } list && list[Math.Clamp(wave, 1, list.Count) - 1].Bosses is { Count: > 0 } own) return own;
        return BossWave.IsFinal(action, wave) && action.Bosses is { Count: > 0 } last ? last : null;
    }

    /// <summary>Whether wave <paramref name="wave"/> holds a pool entry or a boss roll.</summary>
    public static bool Rolls(SpawnWavesAction action, int wave) =>
        action.UnitsOf(wave).Any(u => u.Pool is not null) || (BossesOf(action, wave) ?? []).Any(b => b.IsRoll);

    /// <summary>Every prefab wave <paramref name="wave"/> can spawn: its prefab entries, every member of the pools its pool
    /// entries and boss rolls name, and its fixed bosses (FactionGroups' per-wave rule, wave-variety D4).</summary>
    public static IEnumerable<string> Drawable(SpawnWavesAction action, int wave)
    {
        foreach (var u in action.UnitsOf(wave))
            foreach (var p in u.Pool is { } pool ? Members(action, pool) : [u.Prefab]) yield return p;
        foreach (var b in BossesOf(action, wave) ?? [])
            foreach (var p in b.Pool is { } pool ? Members(action, pool) : [b.Prefab]) yield return p;
    }

    static IReadOnlyList<string> Members(SpawnWavesAction action, string pool) =>
        action.Pools is { } pools && pools.TryGetValue(pool, out var members) ? members : [];

    /// <summary>Rolls wave <paramref name="wave"/>: fixed bosses in list order, then each boss roll's count drawn uniformly
    /// in min..max and that many distinct prefabs of its pool the wave does not already hold (fewer when the pool runs
    /// out), then the wave's own entries in order, each pool entry drawing its count uniformly from its pool with repeats.
    /// A boss without modifiers takes the wave's shared ones: the action's for the units form, the wave's
    /// first entry's for a waveList or a growth wave (revenge D29).</summary>
    public static WaveDraw Resolve(SpawnWavesAction action, int wave, IRandom rng)
    {
        var own = action.UnitsOf(wave);
        var shared = action.WaveList is null ? action.Modifiers : own.Count > 0 ? own[0].Modifiers : null;
        var bosses = new List<UnitEntry>();
        var rolled = false;
        var list = BossesOf(action, wave) ?? [];
        foreach (var b in list)
            if (!b.IsRoll) bosses.Add(new UnitEntry(b.Prefab, b.Count, 1.0, b.Modifiers ?? shared));
        foreach (var b in list)
        {
            if (b.Pool is not { } pool) continue;
            rolled = true;
            var count = b.Min + Index(rng, b.Max - b.Min + 1);
            var free = Members(action, pool).Where(p => bosses.All(h => h.Prefab != p)).ToList();
            for (var i = 0; i < count && free.Count > 0; i++)
            {
                var k = Index(rng, free.Count);
                bosses.Add(new UnitEntry(free[k], 1, 1.0, b.Modifiers ?? shared));
                free.RemoveAt(k);
            }
        }
        var units = new List<UnitEntry>(bosses);
        foreach (var u in own)
        {
            if (u.Pool is not { } pool)
            {
                units.Add(u);
                continue;
            }
            rolled = true;
            var members = Members(action, pool);
            for (var i = 0; i < u.Count && members.Count > 0; i++)
                units.Add(new UnitEntry(members[Index(rng, members.Count)], 1, u.Chance, u.Modifiers));
        }
        return new WaveDraw(units, bosses.Sum(b => b.Count), rolled, bosses);
    }

    /// <summary>A uniform index in 0..<paramref name="n"/>−1.</summary>
    internal static int Index(IRandom rng, int n) => Math.Min(n - 1, (int)(rng.NextDouble() * n));

    /// <summary>"event &lt;id&gt; wave &lt;n&gt;/&lt;N&gt; rolled: &lt;prefab&gt;x&lt;k&gt;, …[; bosses: &lt;prefab&gt;, …]" (wave-variety D6)
    /// within Wire.MaxBytes: the drawn units grouped by prefab in draw order, then every boss; a list too long ends
    /// "… and &lt;m&gt; more", the units' list cut before a boss is.</summary>
    public static string RolledLine(string eventId, int wave, int waves, WaveDraw draw)
    {
        var head = $"event {eventId} wave {wave}/{waves} rolled: ";
        var counts = new List<(string Prefab, int Count)>();
        foreach (var u in draw.Units.Skip(draw.Bosses.Count))
        {
            var i = counts.FindIndex(c => c.Prefab == u.Prefab);
            if (i < 0) counts.Add((u.Prefab, u.Count));
            else counts[i] = (u.Prefab, counts[i].Count + u.Count);
        }
        var units = counts.Select(c => $"{c.Prefab}x{c.Count}").ToList();
        var bosses = draw.Bosses.Select(b => b.Count > 1 ? $"{b.Prefab}x{b.Count}" : b.Prefab).ToList();
        string Line(int keepUnits, int keepBosses)
        {
            var text = head + Cut(units, keepUnits);
            if (bosses.Count > 0) text += "; bosses: " + Cut(bosses, keepBosses);
            return text;
        }
        for (var keep = units.Count; keep >= 0; keep--)
            if (Fits(Line(keep, bosses.Count))) return Line(keep, bosses.Count);
        for (var keep = bosses.Count - 1; keep >= 0; keep--)
            if (Fits(Line(0, keep))) return Line(0, keep);
        return head + $"{units.Count + bosses.Count} picks";
    }

    /// <summary>The first <paramref name="keep"/> items, comma separated, then "… and &lt;m&gt; more" for the rest.</summary>
    static string Cut(IReadOnlyList<string> items, int keep)
    {
        if (keep >= items.Count) return string.Join(", ", items);
        var more = $"… and {items.Count - keep} more";
        return keep == 0 ? more : string.Join(", ", items.Take(keep)) + " " + more;
    }

    static bool Fits(string line) => System.Text.Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes;
}
