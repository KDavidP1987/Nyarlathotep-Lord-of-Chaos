#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>How a spawned unit is tuned: a level (null keeps the prefab's) and Health and PhysicalPower
/// multipliers (foundation Design › UX, `.nyar spawn`).</summary>
public sealed record UnitTuning(LevelArg? Level, float Health, float Power)
{
    public static readonly UnitTuning None = new(null, 1f, 1f);
}

/// <summary>When a unit is due for the budgeted despawn, and the game LifeTime it carries as the backstop: its due time
/// plus the queue's drain time, counted from its spawn (Business rules 2, A16, A21).</summary>
public readonly record struct UnitLifetime(DateTime DueUtc, int LifetimeSeconds);

/// <summary>One unit waiting in the spawn queue: what, for which event (null for `.nyar spawn`), where, and for how
/// long. Its slot under MaxTrackedUnits is held from the request until it is confirmed or failed.</summary>
public sealed record SpawnOrder(long Ticket, string Prefab, string? EventId, float X, float Y, float Z, int LifetimeSeconds,
    DateTime DueUtc, UnitTuning Tuning);

/// <summary>A unit the ledger tracks. <see cref="Key"/> is the service's handle for the entity; at
/// <see cref="DueUtc"/> the ledger queues it for despawn (A21).</summary>
public sealed record TrackedUnit(long Key, string Prefab, string? EventId, DateTime SpawnedUtc, int LifetimeSeconds, DateTime DueUtc);

/// <summary>The limits the ledger enforces, read from the cfg (already clamped to their ceilings, D5).</summary>
public sealed record LedgerLimits(int MaxTracked, int MaxPerWave, int SpawnsPerTick, int DespawnsPerTick);

/// <summary>The outcome of a spawn request: how many units were queued, and the "skipped by &lt;cap&gt;" line when
/// fewer than asked (D16, D22).</summary>
public sealed record SpawnRequestResult(int Queued, string? Skipped);

/// <summary>
/// The registry of every unit Nyarlathotep spawned and the only source of truth for "every spawned unit"
/// (foundation Business rules 7, D16). Services/SpawnTracker drives it and does the game-side work; this class holds
/// no game type, so SpawnLedgerTests covers the caps, the queues and the budgets.
/// <list type="bullet">
/// <item>A request reserves its slots at once, so two requests for the last free slot are settled in arrival order
/// and the loser gets "skipped by MaxTrackedUnits".</item>
/// <item>Spawns and despawns leave their queues at most the per-tick budget at a time.</item>
/// <item>A despawn is released once: a key is queued at most once and leaves the ledger when released.</item>
/// <item>A purge queues what is tracked at that moment and cancels the waiting spawns; a unit spawned by a later
/// request is tracked but not in the draining batch.</item>
/// </list>
/// Every caller runs on the server main thread (Design › States › Concurrency), so there are no locks.
/// </summary>
public sealed class SpawnLedger(LedgerLimits limits)
{
    readonly Dictionary<long, TrackedUnit> _tracked = [];
    readonly Queue<SpawnOrder> _spawnQueue = new();
    readonly HashSet<long> _inFlight = [];
    readonly Queue<long> _despawnQueue = new();
    readonly HashSet<long> _queued = [];
    readonly HashSet<long> _survivors = [];
    long _nextTicket;

    public LedgerLimits Limits => limits;

    /// <summary>Units spawned and still tracked.</summary>
    public int Tracked => _tracked.Count;

    /// <summary>Slots held: tracked units, orders waiting or being spawned, and marked survivors waiting for their
    /// despawn. The MaxTrackedUnits cap counts these, so a boot with survivors cannot double the live count.</summary>
    public int Occupied => _tracked.Count + _spawnQueue.Count + _inFlight.Count + _survivors.Count;

    public int PendingSpawns => _spawnQueue.Count + _inFlight.Count;
    public int PendingDespawns => _despawnQueue.Count;
    public IReadOnlyCollection<TrackedUnit> Units => _tracked.Values;

    public bool IsTracked(long key) => _tracked.ContainsKey(key);
    public TrackedUnit? Find(long key) => _tracked.TryGetValue(key, out var u) ? u : null;

    /// <summary>Queues up to <paramref name="count"/> units, first clamped by MaxUnitsPerWave, then by the free
    /// MaxTrackedUnits slots. <paramref name="place"/> gives the position of the i-th unit.</summary>
    public SpawnRequestResult Request(string prefab, string? eventId, int count, UnitLifetime life, UnitTuning tuning,
        Func<int, (float X, float Y, float Z)> place)
    {
        if (count < 1) return new SpawnRequestResult(0, null);
        var n = count;
        string? skipped = null;
        if (n > limits.MaxPerWave)
        {
            skipped = $"skipped by MaxUnitsPerWave: {n - limits.MaxPerWave} of {count}";
            n = limits.MaxPerWave;
        }
        var free = Math.Max(0, limits.MaxTracked - Occupied);
        if (n > free)
        {
            skipped = $"skipped by MaxTrackedUnits: {count - free} of {count}";
            n = free;
        }
        for (var i = 0; i < n; i++)
        {
            var (x, y, z) = place(i);
            _spawnQueue.Enqueue(new SpawnOrder(++_nextTicket, prefab, eventId, x, y, z, life.LifetimeSeconds, life.DueUtc, tuning));
        }
        return new SpawnRequestResult(n, skipped);
    }

    /// <summary>The orders to spawn this tick, at most SpawnsPerTick. Each stays in flight (holding its slot) until
    /// <see cref="Confirm"/> or <see cref="Fail"/>.</summary>
    public IReadOnlyList<SpawnOrder> TakeSpawns()
    {
        var batch = new List<SpawnOrder>();
        while (batch.Count < limits.SpawnsPerTick && _spawnQueue.Count > 0)
        {
            var order = _spawnQueue.Dequeue();
            _inFlight.Add(order.Ticket);
            batch.Add(order);
        }
        return batch;
    }

    /// <summary>The order spawned as <paramref name="key"/>: its slot becomes a tracked unit. False when the order
    /// is not in flight or the key is already tracked; the caller then despawns the entity itself. SpawnTracker confirms
    /// or fails every order in the tick that takes it, so none is in flight when a purge or an event end runs.</summary>
    public bool Confirm(SpawnOrder order, long key, DateTime utcNow)
    {
        if (!_inFlight.Remove(order.Ticket)) return false;
        if (_tracked.ContainsKey(key)) return false;
        _tracked.Add(key, new TrackedUnit(key, order.Prefab, order.EventId, utcNow, order.LifetimeSeconds, order.DueUtc));
        return true;
    }

    /// <summary>The order could not be spawned: its slot is freed.</summary>
    public void Fail(SpawnOrder order) => _inFlight.Remove(order.Ticket);

    /// <summary>Queues a tracked unit, or a marked survivor the ledger never tracked (the boot sweep), for despawn.
    /// False when it is already queued.</summary>
    public bool QueueDespawn(long key)
    {
        if (!_queued.Add(key)) return false;
        if (!_tracked.ContainsKey(key)) _survivors.Add(key);
        _despawnQueue.Enqueue(key);
        return true;
    }

    /// <summary>Queues every tracked unit whose due time has come, earliest first, so a unit that ends on its own
    /// lifetime leaves through the despawn budget like an event's (A21). Returns how many were newly queued.</summary>
    public int QueueDue(DateTime utcNow)
    {
        var queued = 0;
        foreach (var u in _tracked.Values.Where(u => u.DueUtc <= utcNow && !_queued.Contains(u.Key)).OrderBy(u => u.DueUtc).ToList())
            if (QueueDespawn(u.Key)) queued++;
        return queued;
    }

    /// <summary>The keys to destroy this tick, at most DespawnsPerTick. Each leaves the ledger here, so it is never
    /// released twice.</summary>
    public IReadOnlyList<long> TakeDespawns()
    {
        var batch = new List<long>();
        while (batch.Count < limits.DespawnsPerTick && _despawnQueue.Count > 0)
        {
            var key = _despawnQueue.Dequeue();
            _queued.Remove(key);
            _tracked.Remove(key);
            _survivors.Remove(key);
            batch.Add(key);
        }
        return batch;
    }

    /// <summary>A tracked unit or queued survivor the game removed (died, expired, disabled): it leaves the ledger and
    /// any queue slot. False when the ledger did not hold it.</summary>
    public bool Forget(long key)
    {
        if (!_tracked.Remove(key) && !_survivors.Remove(key)) return false;
        _survivors.Remove(key);
        if (_queued.Remove(key))
        {
            var rest = _despawnQueue.Where(k => k != key).ToList();
            _despawnQueue.Clear();
            foreach (var k in rest) _despawnQueue.Enqueue(k);
        }
        return true;
    }

    /// <summary>The kill switch's unit half (D20): cancels every waiting spawn and queues every tracked unit that is
    /// not already queued. A purge during a drain adds only what the drain does not hold. Returns the number of units
    /// the drain will release (queued before plus newly queued) and the number of cancelled orders.</summary>
    public (int Queued, int Cancelled) Purge()
    {
        var cancelled = _spawnQueue.Count;
        _spawnQueue.Clear();
        foreach (var key in _tracked.Keys.ToList()) QueueDespawn(key);
        return (_despawnQueue.Count, cancelled);
    }

    /// <summary>An event's end (Business rules 2): queues its tracked units spawned before <paramref name="spawnedBefore"/>
    /// and, with <paramref name="cancelOrders"/>, cancels its waiting orders. Returns the units newly queued and the orders
    /// cancelled. The despawn after the grace passes false: a restart of the same event inside the grace owns the
    /// orders waiting then.</summary>
    public (int Queued, int Cancelled) EndEvent(string eventId, DateTime spawnedBefore, bool cancelOrders = true)
    {
        var cancelled = 0;
        if (cancelOrders && _spawnQueue.Count > 0)
        {
            var keep = _spawnQueue.Where(o => o.EventId != eventId).ToList();
            cancelled = _spawnQueue.Count - keep.Count;
            _spawnQueue.Clear();
            foreach (var o in keep) _spawnQueue.Enqueue(o);
        }
        var queued = 0;
        foreach (var u in _tracked.Values.Where(u => u.EventId == eventId && u.SpawnedUtc < spawnedBefore).ToList())
            if (QueueDespawn(u.Key)) queued++;
        return (queued, cancelled);
    }

    /// <summary>What a purge would still take: tracked units not yet queued for despawn, plus waiting orders. Units
    /// already draining are purged already, so a second `.nyar purge confirm` finds nothing (D20).</summary>
    public int Purgeable => _tracked.Keys.Count(k => !_queued.Contains(k)) + _spawnQueue.Count + _inFlight.Count;

    public bool AnythingToPurge => Purgeable > 0;

    /// <summary>Slack added to the drain time in <see cref="DrainMarginSeconds"/> (A16).</summary>
    public const int DrainSlackSeconds = 60;

    /// <summary>How long the budgeted queue needs, at worst, to despawn every tracked unit, plus
    /// <see cref="DrainSlackSeconds"/>: ceil(maxTracked / maxDespawnsPerTick) one-second ticks + 60 s (A16).</summary>
    public static int DrainMarginSeconds(int maxTracked, int maxDespawnsPerTick)
    {
        var perTick = Math.Max(1, maxDespawnsPerTick);
        return (Math.Max(0, maxTracked) + perTick - 1) / perTick + DrainSlackSeconds;
    }

    /// <summary>A unit's due time and LifeTime (Business rules 2): a `.nyar spawn` unit is due
    /// <paramref name="manualLifetimeSeconds"/> after the request; an event's unit at min(its own lifetime, event end +
    /// grace). The ledger queues it at that time for the budgeted despawn, and its LifeTime runs
    /// <paramref name="drainMarginSeconds"/> past it, so LifeTime is only the backstop if the mod stops (A16, A21).
    /// LifeTime is at least 1 s.</summary>
    public static UnitLifetime Lifetime(DateTime utcNow, DateTime? eventEndUtc, int? unitLifetimeSeconds, int graceSeconds,
        int manualLifetimeSeconds, int drainMarginSeconds)
    {
        var due = eventEndUtc is null
            ? utcNow.AddSeconds(manualLifetimeSeconds)
            : Precedence.UnitExpiryUtc(utcNow, unitLifetimeSeconds, eventEndUtc.Value, graceSeconds);
        var untilDue = Math.Max(0, (int)Math.Ceiling((due - utcNow).TotalSeconds));
        return new UnitLifetime(due, Math.Max(1, untilDue + drainMarginSeconds));
    }

    /// <summary>The i-th of <paramref name="count"/> positions spread evenly on a circle of <paramref name="radius"/>
    /// around (x, z), starting at <paramref name="startAngle"/> radians. One unit stands at the centre.</summary>
    public static (float X, float Z) Around(float x, float z, float radius, int i, int count, double startAngle)
    {
        if (count <= 1) return (x, z);
        var angle = startAngle + 2 * Math.PI * i / count;
        return (x + (float)(radius * Math.Cos(angle)), z + (float)(radius * Math.Sin(angle)));
    }
}
