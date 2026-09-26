#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nyarlathotep.Logic;

/// <summary>A live carrier as `debug here` reads it back (faction-empowerment D14): every field the recipe wrote, from the
/// carrier entity itself. <see cref="StripPresent"/> names the gameplay-event components still on it.</summary>
public sealed record NativeCarrier(
    string EventId,
    int LeftSeconds,
    string BuffType,
    int MaxStacks,
    bool IncreaseStacks,
    string EndAction,
    bool MarkOk,
    IReadOnlyList<string> StripPresent,
    IReadOnlyList<StatModifier> Mods);

/// <summary>One native NPC near the admin: its key, distance and current stats, and its carrier if it holds one.</summary>
public sealed record NativeRow(
    string Prefab,
    string Faction,
    float Distance,
    long Key,
    NativeCarrier? Carrier,
    int OtherStatBuffs,
    int Level,
    int Health,
    int MaxHealth,
    float PhysicalPower,
    float SpellPower,
    float AttackSpeed,
    float MoveSpeed);

public static partial class AdminLines
{
    /// <summary>The native lines of `debug here` (D14): at most <paramref name="max"/>, nearest first, ties by prefab then
    /// key; a carried row shows every carrier field, a plain one "carrier none".</summary>
    public static IReadOnlyList<string> Natives(IEnumerable<NativeRow> rows, int max) =>
        rows.OrderBy(r => r.Distance)
            .ThenBy(r => r.Prefab, StringComparer.Ordinal)
            .ThenBy(r => r.Key)
            .Take(max)
            .Select(Native)
            .ToList();

    public static string Native(NativeRow r)
    {
        var line = $"{r.Prefab} native {FactionDenyList.ShortName(r.Faction)} d {(int)MathF.Round(r.Distance)}m carrier ";
        if (r.Carrier is not { } c) line += "none";
        else
        {
            var strip = c.StripPresent.Count == 0 ? "ok" : "missing:" + string.Join(",", c.StripPresent);
            var mods = c.Mods.Count == 0 ? "-" : string.Join(",", c.Mods.Select(m => $"{m.Stat}:{m.Modification}:{F(m.Value)}"));
            line += $"{c.EventId} left {Math.Max(0, c.LeftSeconds)}s type {c.BuffType} stacks {c.MaxStacks} incr {c.IncreaseStacks} " +
                    $"end {c.EndAction} mark {(c.MarkOk ? "ok" : "bad")} strip {strip} mods {mods}";
        }
        return line + $" other stat buffs {r.OtherStatBuffs} lvl {r.Level} hp {r.Health}/{r.MaxHealth} pp {F(r.PhysicalPower)} " +
               $"sp {F(r.SpellPower)} aspd {F(r.AttackSpeed)} mspd {F(r.MoveSpeed)}";
    }

    static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
