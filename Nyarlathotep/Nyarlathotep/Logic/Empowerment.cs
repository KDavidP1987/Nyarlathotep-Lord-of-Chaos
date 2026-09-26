#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// Faction empowerment's pure half (docs/dod/faction-empowerment.md D3–D5, D7, D13, D20): who gets a carrier, what the
// carrier holds, and the ledger that plans every carrier operation within the per-tick budget. Services/EmpowerAction
// reads the game and performs the operations through ICarrierOps; commands, the scheduler tick and the DeathEvent
// postfix all run on the server main thread (S-10), so nothing here locks.

/// <summary>What the service reads from one unit at its turn in a sweep (D3). <see cref="IsOurs"/> is a unit marker
/// (our own spawn); <see cref="OwnedByPlayer"/> comes from <see cref="Ownership.Decide"/>; <see cref="CarrierOf"/> is the
/// event whose carrier the unit already holds, or null.</summary>
public sealed record UnitFacts(
    string Prefab,
    string Faction,
    bool IsPrefab,
    bool IsDead,
    bool HasVBloodUnit,
    bool IsOurs,
    bool OwnedByPlayer,
    string? CarrierOf = null);

/// <summary>Apply, or skip with one of <see cref="Eligibility.SkipReasons"/>.</summary>
public readonly record struct Eligible(bool Apply, string? Skip)
{
    public static readonly Eligible Yes = new(true, null);
    public static Eligible No(string reason) => new(false, reason);
}

/// <summary>Who is empowered (D3, Business rules 4), decided per unit from its facts at sweep time.</summary>
public static class Eligibility
{
    public static readonly IReadOnlyList<string> SkipReasons =
        ["prefab", "dead", "ours", "owned", "denied", "other", "excluded", "vblood", "carried"];

    /// <summary>The checks run in the order of <see cref="SkipReasons"/>, so the deny list wins over includeUnits and a
    /// player-owned unit is never reached by a faction or a name.</summary>
    public static Eligible Decide(UnitFacts u, EmpowerAction a)
    {
        if (u.IsPrefab) return Eligible.No("prefab");
        if (u.IsDead) return Eligible.No("dead");
        if (u.IsOurs) return Eligible.No("ours");
        if (u.OwnedByPlayer) return Eligible.No("owned");
        if (FactionDenyList.IsDenied(u.Faction)) return Eligible.No("denied");
        if (!a.Factions.Contains(u.Faction) && !a.IncludeUnits.Contains(u.Prefab)) return Eligible.No("other");
        if (a.ExcludeUnits.Contains(u.Prefab)) return Eligible.No("excluded");
        if (u.HasVBloodUnit && !a.IncludeVBloods) return Eligible.No("vblood");
        if (u.CarrierOf is not null) return Eligible.No("carried");
        return Eligible.Yes;
    }
}

/// <summary>How one ownership component of a unit resolved: absent, leading to no player, leading to a player
/// character or a player's servant (or, for Team, a player team), or present but unreadable.</summary>
public enum OwnerLink { Absent, NotPlayer, Player, Unresolvable }

/// <summary>The game components that say a player owns a unit (D3): Follower.Followed, EntityOwner.Owner and Team. No
/// mod contract is declared (Interfaces › External), so Bloodcraft familiars and KindredCommands units are recognised
/// only through these.</summary>
public sealed record OwnershipFacts(OwnerLink Follower, OwnerLink EntityOwner, OwnerLink Team);

public static class Ownership
{
    /// <summary>Owned when any link leads to a player, or is present but unresolvable: absent, renamed or malformed
    /// data fails closed (D3, D20).</summary>
    public static bool Decide(OwnershipFacts f) => Owned(f.Follower) || Owned(f.EntityOwner) || Owned(f.Team);

    static bool Owned(OwnerLink link) => link is OwnerLink.Player or OwnerLink.Unresolvable;
}

/// <summary>One entry of a carrier's stat buffer: the game's UnitStatType and ModificationType names and the value.</summary>
public sealed record StatModifier(string Stat, string Modification, double Value);

public sealed partial record EmpowerStats
{
    /// <summary>One entry per stat above 1.0, each MultiplyBaseAdd with value = multiplier − 1 (D4). attackSpeed gives
    /// PrimaryAttackSpeed and AbilityAttackSpeed.</summary>
    public static IReadOnlyList<StatModifier> Modifiers(EmpowerStats stats)
    {
        var list = new List<StatModifier>();
        void Add(double multiplier, params string[] statTypes)
        {
            if (multiplier <= 1.0) return;
            foreach (var t in statTypes) list.Add(new StatModifier(t, "MultiplyBaseAdd", multiplier - 1.0));
        }
        Add(stats.PhysicalPower, "PhysicalPower");
        Add(stats.SpellPower, "SpellPower");
        Add(stats.MaxHealth, "MaxHealth");
        Add(stats.AttackSpeed, "PrimaryAttackSpeed", "AbilityAttackSpeed");
        Add(stats.MoveSpeed, "MovementSpeed");
        return list;
    }
}

/// <summary>Every value the service writes on a new carrier (D4, Business rules 1). Enum values are the game's member
/// names (ProjectM.BuffType, LifeTimeEndAction, UnitStatType, ModificationType), resolved by the service.</summary>
public sealed record CarrierRecipe(
    string BuffType,
    int MaxStacks,
    bool IncreaseStacks,
    int SpellLevel,
    float LifeTimeSeconds,
    string EndAction,
    IReadOnlyList<string> Strip,
    IReadOnlyList<StatModifier> Modifiers)
{
    /// <summary>The gameplay-event components removed from the potion buff, so nothing but its LifeTime ends it.</summary>
    public static readonly IReadOnlyList<string> StripComponents =
    [
        "CreateGameplayEventsOnSpawn",
        "GameplayEventListeners",
        "RemoveBuffOnGameplayEvent",
        "RemoveBuffOnGameplayEventEntry",
        "DestroyOnGameplayEvent",
    ];

    public static CarrierRecipe For(EmpowerStats stats, double secondsLeft) =>
        new("Replace", 1, false, Markers.Carrier, (float)secondsLeft, "Destroy", StripComponents, EmpowerStats.Modifiers(stats));
}

/// <summary>An event's query result: the units to visit (PrefabGUID + FactionReference + Health + UnitStats, Business
/// rules 9) and the faction's entity count over PrefabGUID + FactionReference alone, for the "query n of m" line.</summary>
public sealed record SweepQuery(IReadOnlyList<long> Units, int FactionEntities);

/// <summary>The game side of the ledger (D20). Keys are the service's entity handles. An Apply is staged Create → Mark →
/// Lifetime (Buff type, stacks and LifeTime) → Strip → Modifiers, each stage taking the recipe and nothing else.</summary>
public interface ICarrierOps
{
    /// <summary>A throw is the event's tick fault (Epic D25).</summary>
    SweepQuery Query(EmpowerAction action);
    /// <summary>The unit's facts, or null when it no longer exists.</summary>
    UnitFacts? Facts(long unit);
    bool Exists(long buff);
    long Create(long unit, CarrierRecipe recipe);
    void Mark(long buff, CarrierRecipe recipe);
    void Lifetime(long buff, CarrierRecipe recipe);
    void Strip(long buff, CarrierRecipe recipe);
    void Modifiers(long buff, CarrierRecipe recipe);
    /// <summary>EntityExtensions.RemoveBuffSafe (D8).</summary>
    void Remove(long buff);
    /// <summary>The fallback: LifeTime.Duration set to the carrier's age, so the game destroys it next frame (S-7).</summary>
    void Expire(long buff);
}

/// <summary>Holds the carriers by unit and event and plans every carrier operation (D5, D20). Each tick the service calls
/// <see cref="BeginTick"/> (removals first) and then <see cref="TickEvent"/> for every active Empower event inside that
/// event's try/catch; the two share one budget of EmpowerBatchPerTick units.</summary>
public sealed class CarrierLedger(ICarrierOps ops, Action<string> log)
{
    public static readonly TimeSpan ResweepInterval = TimeSpan.FromSeconds(15);

    /// <summary>A Remove that throws is retried on this many later ticks before the expiry fallback.</summary>
    public const int RemoveRetries = 3;

    /// <summary>No carrier is applied with less time than this left, so none outlives its event.</summary>
    public const double MinSecondsLeft = 1.0;

    sealed class EventState(string id, EmpowerAction action, DateTime endsUtc, DateTime firstSweepUtc)
    {
        public string Id { get; } = id;
        public EmpowerAction Action { get; } = action;
        public DateTime EndsUtc { get; } = endsUtc;
        public DateTime NextSweepUtc { get; set; } = firstSweepUtc;
        public Queue<long>? Sweep { get; set; }
        public int Sweeps { get; set; }
        public int Applied { get; set; }
        public int Failed { get; set; }
        public Dictionary<string, int> Skips { get; } = new(StringComparer.Ordinal);
        public bool FailStreak { get; set; }
    }

    sealed class Carrier(string eventId, long unit, long buff)
    {
        public string EventId { get; } = eventId;
        public long Unit { get; } = unit;
        public long Buff { get; } = buff;
        public bool Pending { get; set; } = true;
    }

    sealed class StopTally(string eventId, int pending)
    {
        public string EventId { get; } = eventId;
        public int Pending { get; set; } = pending;
        public int Removed { get; set; }
        public int Left { get; set; }
    }

    sealed class Removal(long buff, StopTally? tally)
    {
        public long Buff { get; } = buff;
        public StopTally? Tally { get; } = tally;
        public int Failures { get; set; }
    }

    readonly Dictionary<string, EventState> _events = new(StringComparer.Ordinal);
    readonly Dictionary<long, Carrier> _byUnit = [];
    readonly LinkedList<Removal> _removals = new();
    int _remaining;

    public bool IsActive(string eventId) => _events.ContainsKey(eventId);

    /// <summary>Units holding a carrier of <paramref name="eventId"/> that was applied in full (D11's admin units).</summary>
    public int CountFor(string eventId) => _byUnit.Values.Count(c => c.EventId == eventId && !c.Pending);

    public int Carriers => _byUnit.Count;
    public int PendingRemovals => _removals.Count;
    public bool SweepInProgress(string eventId) => _events.TryGetValue(eventId, out var e) && e.Sweep is not null;
    public string? CarrierOf(long unit) => _byUnit.TryGetValue(unit, out var c) ? c.EventId : null;

    /// <summary>An Empower event started: its first sweep is due now.</summary>
    public void Start(string eventId, EmpowerAction action, DateTime endsUtc, DateTime utcNow) =>
        _events[eventId] = new EventState(eventId, action, endsUtc, utcNow);

    /// <summary>The natural end: the queued sweep is dropped and nothing is queued, since every carrier's LifeTime ends in
    /// the same second (D5).</summary>
    public void End(string eventId)
    {
        _events.Remove(eventId);
        foreach (var c in _byUnit.Values.Where(c => c.EventId == eventId).ToList()) _byUnit.Remove(c.Unit);
    }

    /// <summary>Stop, fault cancel and purge: the queued sweep is dropped (no later tick applies for it) and every carrier
    /// of the event is queued for removal. The line "empower &lt;id&gt; stopped: &lt;n&gt; removed, &lt;f&gt; left to
    /// expire" is logged when the last of them is done.</summary>
    public void Stop(string eventId)
    {
        _events.Remove(eventId);
        var carriers = _byUnit.Values.Where(c => c.EventId == eventId).ToList();
        var tally = new StopTally(eventId, carriers.Count);
        foreach (var c in carriers)
        {
            _byUnit.Remove(c.Unit);
            _removals.AddLast(new Removal(c.Buff, tally));
        }
        if (carriers.Count == 0) Stopped(tally);
    }

    /// <summary>Every event stops (purge).</summary>
    public void StopAll()
    {
        foreach (var id in _events.Keys.Concat(_byUnit.Values.Select(c => c.EventId)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList())
            Stop(id);
    }

    /// <summary>Carriers found by the boot sweep (D7): each is queued for removal, never its unit.</summary>
    public void QueueBootRemovals(IEnumerable<long> buffs)
    {
        foreach (var b in buffs) _removals.AddLast(new Removal(b, null));
    }

    /// <summary>Starts a tick with <paramref name="budget"/> operations and spends them on removals first (D5). A removal
    /// whose buff no longer exists is dropped without an operation; a Remove that throws is retried on the next tick up
    /// to <see cref="RemoveRetries"/> times, then the expiry fallback is tried once, and only if that throws too is the
    /// carrier left to its LifeTime (D20).</summary>
    public void BeginTick(int budget)
    {
        _remaining = budget;
        var retry = new List<Removal>();
        while (_remaining > 0 && _removals.First is { } node)
        {
            var r = node.Value;
            _removals.RemoveFirst();
            if (!ops.Exists(r.Buff))
            {
                Done(r, removed: true);
                continue;
            }
            _remaining--;
            if (r.Failures > RemoveRetries)
            {
                try
                {
                    ops.Expire(r.Buff);
                    Done(r, removed: true);
                }
                catch (Exception ex)
                {
                    log($"empower {r.Tally?.EventId ?? "boot"}: carrier removal failed, left to expire: {ex.Message}");
                    Done(r, removed: false);
                }
                continue;
            }
            try
            {
                ops.Remove(r.Buff);
                Done(r, removed: true);
            }
            catch (Exception)
            {
                r.Failures++;
                retry.Add(r);
            }
        }
        for (var i = retry.Count - 1; i >= 0; i--) _removals.AddFirst(retry[i]);
    }

    /// <summary>The event's share of this tick (D5): a sweep is queued at the start and again 15 s after each sweep
    /// starts, never while one is unfinished; it visits at most the budget left by <see cref="BeginTick"/>. Each Apply
    /// gets LifeTime = the seconds until the event's end, and none is made with less than <see cref="MinSecondsLeft"/>
    /// left. A query that throws propagates to the caller's per-event try/catch with nothing changed.</summary>
    public void TickEvent(string eventId, DateTime utcNow)
    {
        if (!_events.TryGetValue(eventId, out var e)) return;
        if (e.Sweep is null)
        {
            if (utcNow < e.NextSweepUtc || _remaining <= 0) return;
            Prune(eventId);
            var q = ops.Query(e.Action);
            if (e.Sweeps == 0) log($"empower {eventId}: query {q.Units.Count} of {q.FactionEntities} faction entities");
            e.Sweep = new Queue<long>(q.Units);
            e.NextSweepUtc = utcNow + ResweepInterval;
            e.Applied = 0;
            e.Failed = 0;
            e.Skips.Clear();
        }

        while (e.Sweep.Count > 0 && _remaining > 0)
        {
            var secondsLeft = (e.EndsUtc - utcNow).TotalSeconds;
            if (secondsLeft < MinSecondsLeft)
            {
                e.Sweep.Clear();
                break;
            }
            var unit = e.Sweep.Dequeue();
            _remaining--;
            var facts = ops.Facts(unit);
            if (facts is null) continue;                          // gone since the query: dropped without an operation
            var decision = Eligibility.Decide(facts with { CarrierOf = CarrierOf(unit) ?? facts.CarrierOf }, e.Action);
            if (!decision.Apply)
            {
                e.Skips[decision.Skip!] = e.Skips.GetValueOrDefault(decision.Skip!) + 1;
                continue;
            }
            Apply(e, unit, CarrierRecipe.For(e.Action.Stats, secondsLeft));
        }

        if (e.Sweep.Count == 0)
        {
            if (e.Sweeps == 0 || e.Applied > 0 || e.Failed > 0) log(SweepLine(e));
            e.Sweeps++;
            e.Sweep = null;
        }
    }

    /// <summary>The staged Apply (D20): the carrier is recorded as pending at create; a throw after create queues it for
    /// removal and never leaves it tracked, a throw at create leaves nothing. Either way the unit is skipped and the
    /// failure is logged once per streak.</summary>
    void Apply(EventState e, long unit, CarrierRecipe recipe)
    {
        long buff;
        try
        {
            buff = ops.Create(unit, recipe);
        }
        catch (Exception ex)
        {
            Failed(e, ex);
            return;
        }
        var carrier = new Carrier(e.Id, unit, buff);
        _byUnit[unit] = carrier;
        try
        {
            ops.Mark(buff, recipe);
            ops.Lifetime(buff, recipe);
            ops.Strip(buff, recipe);
            ops.Modifiers(buff, recipe);
        }
        catch (Exception ex)
        {
            _byUnit.Remove(unit);
            _removals.AddLast(new Removal(buff, null));
            Failed(e, ex);
            return;
        }
        carrier.Pending = false;
        e.Applied++;
        e.FailStreak = false;
    }

    void Failed(EventState e, Exception ex)
    {
        e.Failed++;
        if (e.FailStreak) return;
        e.FailStreak = true;
        log($"empower {e.Id}: apply failed: {ex.Message}");
    }

    /// <summary>Drops the event's carriers whose buff is gone (the unit died or the game removed it).</summary>
    void Prune(string eventId)
    {
        foreach (var c in _byUnit.Values.Where(c => c.EventId == eventId && !ops.Exists(c.Buff)).ToList()) _byUnit.Remove(c.Unit);
    }

    void Done(Removal r, bool removed)
    {
        if (r.Tally is not { } t) return;
        t.Pending--;
        if (removed) t.Removed++;
        else t.Left++;
        if (t.Pending == 0) Stopped(t);
    }

    void Stopped(StopTally t) => log($"empower {t.EventId} stopped: {t.Removed} removed, {t.Left} left to expire");

    static string SweepLine(EventState e)
    {
        var skipped = e.Skips.Values.Sum();
        var reasons = string.Join(" ", Eligibility.SkipReasons.Where(e.Skips.ContainsKey).Select(r => $"{r} {e.Skips[r]}"));
        return $"empower {e.Id}: sweep {e.Applied} applied, {skipped} skipped" +
               (skipped > 0 ? $" ({reasons})" : "") +
               (e.Failed > 0 ? $", {e.Failed} failed" : "");
    }
}

/// <summary>One buff found by the boot query: its SpellLevel value, the buff and its target.</summary>
public sealed record MarkerRow(float Level, long Buff, long Target);

/// <summary>What the boot sweep does with the rows it found (D7): Unit rows despawn their unit, Carrier rows remove only
/// the buff and never touch its target, and any other value is ignored.</summary>
public sealed record SweepPlan(IReadOnlyList<long> Despawn, IReadOnlyList<long> RemoveCarriers)
{
    public static SweepPlan From(IEnumerable<MarkerRow> rows)
    {
        var despawn = new List<long>();
        var carriers = new List<long>();
        foreach (var r in rows)
        {
            switch (Markers.KindOf(r.Level))
            {
                case MarkerKind.Unit:
                    if (!despawn.Contains(r.Target)) despawn.Add(r.Target);
                    break;
                case MarkerKind.Carrier:
                    if (!carriers.Contains(r.Buff)) carriers.Add(r.Buff);
                    break;
            }
        }
        return new SweepPlan(despawn, carriers);
    }
}

/// <summary>The VBloodKilled trigger (D13): a V Blood kill is a death whose entity carries VBloodConsumeSource
/// (DEV_REMINDERS #26). A gate boss carries VBloodUnit alone and raises nothing.</summary>
public static class DeathRule
{
    public static bool IsVBloodKill(bool hasConsumeSource, bool hasVBloodUnit) => hasConsumeSource;
}
