#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The controls in force when an event is asked to start.</summary>
public sealed record ControlState(
    bool PurgeCooldownActive,
    bool GeneralEnabled,
    IReadOnlySet<Pillar> EnabledPillars,
    int ActiveEvents,
    int MaxConcurrentEvents);

/// <summary>Control precedence (foundation Business rules 1–2, D3): purge &gt; General.Enabled &gt; pillar switch
/// &gt; Limits caps &gt; the definition. There is no exception path: nothing here takes an actor, so an admin's
/// manual start meets the same controls as a schedule.</summary>
public static class Precedence
{
    /// <summary>The reason the start is refused, highest control first, or null when it may start.</summary>
    public static string? StartBlocker(EventDefinition def, ControlState s)
    {
        if (s.PurgeCooldownActive) return "purge cooldown active";
        if (!s.GeneralEnabled) return "General.Enabled is false";
        if (!s.EnabledPillars.Contains(def.Pillar)) return $"pillar {def.Pillar.ToString().ToLowerInvariant()} is off";
        if (s.ActiveEvents >= s.MaxConcurrentEvents) return "skipped by MaxConcurrentEvents";
        if (def.DisabledReason is not null) return $"event {def.Id} is disabled: {def.DisabledReason}";
        if (!def.Enabled) return $"event {def.Id} is disabled";
        return null;
    }

    /// <summary>How many of <paramref name="requested"/> units a wave may spawn: first clamped by MaxUnitsPerWave,
    /// then by the free MaxTrackedUnits slots. Each clamp adds its log line.</summary>
    public static int WaveSize(int requested, int maxPerWave, int trackedNow, int maxTracked, List<string> log)
    {
        var n = requested;
        if (n > maxPerWave)
        {
            log.Add($"clamped by MaxUnitsPerWave: {requested} -> {maxPerWave}");
            n = maxPerWave;
        }
        var free = Math.Max(0, maxTracked - trackedNow);
        if (n > free)
        {
            log.Add($"skipped by MaxTrackedUnits: {n - free} of {n}");
            n = free;
        }
        return n;
    }

    /// <summary>When a unit spawned at <paramref name="spawnUtc"/> must be gone: min(its own lifetime, event end +
    /// grace). The event end always wins.</summary>
    public static DateTime UnitExpiryUtc(DateTime spawnUtc, int? unitLifetimeSeconds, DateTime eventEndUtc, int graceSeconds)
    {
        var byEvent = eventEndUtc.AddSeconds(graceSeconds);
        if (unitLifetimeSeconds is null) return byEvent;
        var own = spawnUtc.AddSeconds(unitLifetimeSeconds.Value);
        return own < byEvent ? own : byEvent;
    }
}
