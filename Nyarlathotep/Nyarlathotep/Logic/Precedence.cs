#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>A cut by a cfg cap (undead-hordes D12): <see cref="Queued"/> of <see cref="Asked"/> units by <see cref="Key"/>
/// at <see cref="Value"/>. Admins see it in the start reply, `event info` and the log; a player never sees a cfg key.</summary>
public sealed record CapCut(int Queued, int Asked, string Key, int Value)
{
    public string Text => $"capped: {Queued} of {Asked} by {Key} {Value}";

    /// <summary>"&lt;event&gt; capped: &lt;n&gt; of &lt;m&gt; by &lt;key&gt; &lt;value&gt;", the log line without "[nyar] ".</summary>
    public string LogLine(string eventId) => $"{eventId} {Text}";

    /// <summary>"capped: &lt;n&gt; of &lt;m&gt; by &lt;key&gt; &lt;value&gt;; raise it in the cfg", the admin's line.</summary>
    public string InfoLine => Text + "; raise it in the cfg";
}

/// <summary>The controls in force when an event is asked to start. <see cref="PlayerPositions"/> reads the online
/// players' x/z and <see cref="RegionOf"/> names a point's region (regions D4, A11, A20, A26); both are read only for a
/// definition with a scope, and a null one refuses a scoped start (fails closed).</summary>
public sealed record ControlState(
    bool PurgeCooldownActive,
    bool GeneralEnabled,
    IReadOnlySet<Pillar> EnabledPillars,
    int ActiveEvents,
    int MaxConcurrentEvents,
    Func<IReadOnlyList<(float X, float Z)>>? PlayerPositions = null,
    Func<float, float, string>? RegionOf = null);

/// <summary>Control precedence (foundation Business rules 1–2, D3): purge &gt; General.Enabled &gt; pillar switch
/// &gt; Limits caps &gt; the definition. There is no exception path: nothing here takes an actor, so an admin's
/// manual start meets the same controls as a schedule.</summary>
public static class Precedence
{
    /// <summary>The purge blocker's label; EventRuntime matches it to add the seconds left (faction-empowerment D30).</summary>
    public const string PurgeCooldown = "purge cooldown active";

    /// <summary>The refusal of the start, highest control first, or null when it may start; each carries its code and
    /// reason (raphael-api-admin Business rules 3). The purge refusal's seconds are added by EventRuntime.</summary>
    public static Outcome? StartBlocker(EventDefinition def, ControlState s)
    {
        if (s.PurgeCooldownActive) return Outcome.Refused(PurgeCooldown, RefusalCode.Cooldown);
        if (!s.GeneralEnabled) return Outcome.Refused("General.Enabled is false", RefusalCode.Disabled, reason: Reasons.General);
        if (!s.EnabledPillars.Contains(def.Pillar))
            return Outcome.Refused($"pillar {def.Pillar.ToString().ToLowerInvariant()} is off", RefusalCode.Disabled, "pillar", reason: Reasons.PillarOff);
        if (s.ActiveEvents >= s.MaxConcurrentEvents) return Outcome.Refused("skipped by MaxConcurrentEvents", RefusalCode.Limit, reason: Reasons.MaxConcurrent);
        if (def.DisabledReason is not null)
            return Outcome.Refused($"event {def.Id} is disabled: {def.DisabledReason}", RefusalCode.State, "id", reason: Reasons.Disabled);
        if (!def.Enabled) return Outcome.Refused($"event {def.Id} is disabled", RefusalCode.State, "id", reason: Reasons.Disabled);
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

    /// <summary>The cut WaveSize makes of <paramref name="requested"/> units (undead-hordes D12), named by the cap that set
    /// the final size: MaxTrackedUnits when the free slots are fewer than the wave cap allows, else MaxUnitsPerWave; null
    /// when nothing is cut.</summary>
    public static CapCut? Cut(int requested, int maxPerWave, int occupied, int maxTracked)
    {
        var n = WaveSize(requested, maxPerWave, occupied, maxTracked, []);
        if (n >= requested) return null;
        return Math.Max(0, maxTracked - occupied) < Math.Min(requested, maxPerWave)
            ? new CapCut(n, requested, "MaxTrackedUnits", maxTracked)
            : new CapCut(n, requested, "MaxUnitsPerWave", maxPerWave);
    }

    /// <summary>The cut an admin start names (undead-hordes D12): a destination event's first wave (its entries and fixed
    /// bosses) or an apocalypse's largest horde (the last, with its bosses) against the caps now; null for any other
    /// event or when nothing is cut.</summary>
    public static CapCut? StartCut(EventDefinition d, int maxPerWave, int occupied, int maxTracked)
    {
        if (d.Apocalypse is { } apo)
            return Cut(HordeSchedule.Size(apo.Hordes, apo.Hordes.Count) + (apo.Bosses ?? []).Sum(b => b.Count), maxPerWave, occupied, maxTracked);
        if (d.Action is not { Destination: not null } a) return null;
        var first = a.UnitsOf(1).Sum(u => u.Count) + (Pools.BossesOf(a, 1) ?? []).Where(b => !b.IsRoll).Sum(b => b.Count);
        return Cut(first, maxPerWave, occupied, maxTracked);
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
