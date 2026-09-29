#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The event engine's pure half (foundation build step 5): which definitions a trigger reaches, whether a
// definition's own conditions allow an automatic start, how a wave is sized, and the life of a running instance
// (waves due, faults, end, the despawn after the grace). Services/EventRuntime, TriggerBus and WaveAction drive it
// on the server main thread, so there are no locks.

/// <summary>Which action a definition runs (faction-empowerment D9). None only for a definition validation disabled.</summary>
public enum EventActionKind { None, Waves, Empower }

public static class EventActions
{
    /// <summary>Waves for a SpawnWaves action, Empower for an Empower action. EventRuntime dispatches on it.</summary>
    public static EventActionKind ActionKindOf(EventDefinition def) =>
        def.Empower is not null ? EventActionKind.Empower
        : def.Action is not null ? EventActionKind.Waves
        : EventActionKind.None;

    /// <summary>One empowerment per faction (faction-empowerment D6, Business rules 3): the reason
    /// <paramref name="def"/> may not start while <paramref name="active"/> run, or null. The first shared faction is
    /// named by its short name, else the first shared include unit; active events are checked in id order.</summary>
    public static string? EmpowerClash(EventDefinition def, IEnumerable<ActiveEvent> active)
    {
        if (def.Empower is not { } mine) return null;
        foreach (var a in active.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            if (a.Definition.Empower is not { } theirs) continue;
            var faction = mine.Factions.FirstOrDefault(f => theirs.Factions.Contains(f));
            if (faction is not null) return $"faction {FactionDenyList.ShortName(faction)} already empowered by {a.Id}";
            var unit = mine.IncludeUnits.FirstOrDefault(u => theirs.IncludeUnits.Contains(u));
            if (unit is not null) return $"unit {unit} already empowered by {a.Id}";
        }
        return null;
    }
}

/// <summary>What the conditions of a definition are checked against when a trigger fires.</summary>
public sealed record ConditionContext(int Players, GameMode ServerMode, TimeOnly LocalNow, DateTime UtcNow, DateTime? LastStartUtc, int Roll);

/// <summary>A definition's conditions (Design › Data): they gate automatic starts only; an admin's `.nyar event start`
/// is deliberate and meets the controls of <see cref="Precedence"/> alone.</summary>
public static class ConditionCheck
{
    /// <summary>Why the conditions refuse the start, or null. <see cref="ConditionContext.Roll"/> is 1–100.</summary>
    public static string? Blocker(Conditions c, ConditionContext x)
    {
        if (x.Players < c.MinPlayers) return $"needs {c.MinPlayers} players online, {x.Players} are";
        if (c.Mode != GameMode.Any && c.Mode != x.ServerMode) return $"runs on {c.Mode.ToString().ToLowerInvariant()} servers only";
        if (c.Window is { } w && !InWindow(w, x.LocalNow)) return $"outside its window {w.From:HH\\:mm}-{w.To:HH\\:mm}";
        if (c.CooldownMinutes > 0 && x.LastStartUtc is { } last && x.UtcNow - last < TimeSpan.FromMinutes(c.CooldownMinutes))
            return $"cooldown {c.CooldownMinutes} min";
        if (x.Roll > c.ChancePercent) return $"chance {c.ChancePercent}% not met";
        return null;
    }

    /// <summary>From inclusive, To exclusive; a window whose To is earlier crosses midnight; From == To is all day.</summary>
    public static bool InWindow(TimeWindow w, TimeOnly t) =>
        w.From == w.To || (w.From < w.To ? t >= w.From && t < w.To : t >= w.From || t < w.To);
}

/// <summary>Which definitions an automatic trigger reaches. <see cref="Candidates"/> is the one enabled check for every
/// trigger kind (foundation D40): a disabled definition is never returned, so it never starts and logs nothing.</summary>
public static class TriggerRouter
{
    public static IEnumerable<EventDefinition> Candidates(DefinitionSet set, TriggerType type) =>
        set.All.Where(d => d.Trigger.Type == type && d.Startable);

    /// <summary>Schedule definitions due this minute, with the occurrence key to record (D4).</summary>
    public static IReadOnlyList<(EventDefinition Definition, string Occurrence)> ScheduleDue(DefinitionSet set, DateTime utcNow,
        TimeZoneInfo zone, Func<string, string?> lastOccurrence)
    {
        var due = new List<(EventDefinition, string)>();
        foreach (var d in Candidates(set, TriggerType.Schedule))
        {
            var key = Schedule.Due(d.Trigger.Days, d.Trigger.Times, utcNow, zone, lastOccurrence(d.Id));
            if (key is not null) due.Add((d, key));
        }
        return due;
    }

    public static IReadOnlyList<EventDefinition> PhaseEntered(DefinitionSet set, DayPhase phase) =>
        Candidates(set, TriggerType.GameTime).Where(d => d.Trigger.Phase == phase).ToList();

    /// <summary>VBloodKilled definitions naming <paramref name="prefab"/> or "any" whose trigger scope holds the kill
    /// (regions D4): a Global one always, a scoped one only when <paramref name="kill"/> lies in a named region. A kill
    /// whose position is unknown reaches Global definitions only, and a Global definition reads no position.</summary>
    public static IReadOnlyList<EventDefinition> VBloodKilled(DefinitionSet set, string prefab, (float X, float Z)? kill = null,
        Func<float, float, string>? regionOf = null) =>
        Candidates(set, TriggerType.VBloodKilled)
            .Where(d => d.Trigger.Bosses.Any(b => b == "any" || string.Equals(b, prefab, StringComparison.Ordinal)))
            .Where(d => d.Trigger.Scope.IsGlobal || (kill is { } k && regionOf is not null && d.Trigger.Scope.Names(regionOf(k.X, k.Z))))
            .ToList();

    /// <summary>True when a VBloodKilled definition that <paramref name="prefab"/> reaches has a trigger scope, so the
    /// kill's position matters (regions D4).</summary>
    public static bool NeedsKillPosition(DefinitionSet set, string prefab) =>
        Candidates(set, TriggerType.VBloodKilled).Any(d => !d.Trigger.Scope.IsGlobal
            && d.Trigger.Bosses.Any(b => b == "any" || string.Equals(b, prefab, StringComparison.Ordinal)));

    /// <summary>The kill position for routing (regions D4, A38): <paramref name="read"/> runs only when a scoped
    /// definition matches the kill, so a Global-only kill reads no position; a throwing read is an unreadable position.</summary>
    public static (float X, float Z)? KillFor(DefinitionSet set, string prefab, Func<(float X, float Z)?> read)
    {
        if (!NeedsKillPosition(set, prefab)) return null;
        try { return read(); }
        catch (Exception) { return null; }
    }

    /// <summary>True when "vblood kill: position unreadable" is to be logged: the kill reached a scoped definition, its
    /// position is unknown, and this is the first such kill of the streak; a readable one ends the streak (regions D4).</summary>
    public static bool UnreadableKillLogs(DefinitionSet set, string prefab, (float X, float Z)? kill, FailureStreak streak)
    {
        if (!NeedsKillPosition(set, prefab)) return false;
        if (kill is not null) { streak.Ok(); return false; }
        return streak.Fail();
    }

    /// <summary>The FactionKills definitions a death counts for (automation D11): startable ones only, so with none the
    /// kill rule returns at once.</summary>
    public static IReadOnlyList<EventDefinition> FactionKills(DefinitionSet set, KillFacts kill, Func<float, float, string>? regionOf) =>
        Candidates(set, TriggerType.FactionKills).Where(d => KillRule.Counts(kill, d, regionOf)).ToList();

    /// <summary>True for the triggers a player's action fires (automation D13, D14): only their starts carry a focus and
    /// pass PlayerTriggerGate.</summary>
    public static bool IsPlayerAction(TriggerType type) => type is TriggerType.RegionEntered or TriggerType.FactionKills;
}

/// <summary>How one wave is sized (Business rules 1, D22): the whole wave is clamped by MaxUnitsPerWave, then by the free
/// MaxTrackedUnits slots, and the units left are given to the entries in order.</summary>
/// <summary>What SpawnTracker does with a unit at one regroup look (event-library A23).</summary>
public enum RegroupStep { Wait, Skip, Move }

public static class WavePlan
{
    /// <summary>Where a wave is centred: the starting admin's position for an Admin location, else the stored Point at
    /// its height, or 0 for a Point stored without one (event-library A20).</summary>
    public static (float X, float Y, float Z) Center(Location location, (float X, float Y, float Z)? origin) =>
        location.Type == LocationType.Admin && origin is { } o ? o : (location.X, location.Y ?? 0f, location.Z);

    /// <summary>How far above or below the centre's height a snapped unit may stand and still count as on the centre's
    /// terrain level (event-library A23): a plateau step is 5 m, a slope's rise within 1 m of a unit is well under 2.</summary>
    public const float RegroupTolerance = 2f;

    /// <summary>The point a wave's units regroup to when the game snaps one onto another terrain level (A23): the starting
    /// admin's position for an Admin location, a Point with a stored height, or null (a Point saved without a height, or
    /// an Admin location with no admin), which is never checked.</summary>
    public static (float X, float Y, float Z)? Anchor(Location location, (float X, float Y, float Z)? origin) =>
        location.Type == LocationType.Admin ? origin
        : location.Y is { } y ? (location.X, y, location.Z)
        : null;

    /// <summary>A group's regroup anchor (design §9 D30, automation A4): none for an AroundPlayer group, whose ring point
    /// takes the player's height and not its own ground's level, so its units stay where the game grounds them;
    /// <see cref="Anchor"/> otherwise.</summary>
    public static (float X, float Y, float Z)? GroupAnchor(Location location, (float X, float Y, float Z)? origin) =>
        location.Type == LocationType.AroundPlayer ? null : Anchor(location, origin);

    /// <summary>The height a group's walk check reads at (walkable-spawns D3): the player's height, which the group's
    /// centre carries, for an AroundPlayer group; the anchor's otherwise (none: nothing is checked).</summary>
    public static float? WalkY(Location location, (float X, float Y, float Z) centre, (float X, float Y, float Z)? origin) =>
        location.Type == LocationType.AroundPlayer ? centre.Y : Anchor(location, origin)?.Y;

    /// <summary>True when a unit the game snapped to height <paramref name="unitY"/> stands on another terrain level than
    /// its anchor at <paramref name="anchorY"/>, so it is moved to the anchor (A23).</summary>
    public static bool Regroup(float unitY, float anchorY) => MathF.Abs(unitY - anchorY) > RegroupTolerance;

    /// <summary>Looks at a unit on its spawn tick and on up to this many ticks after, waiting for the game's snap (A23).</summary>
    public const int RegroupTries = 4;

    /// <summary>How far from the anchor a regrouped unit lands, so a regrouped wave does not stack on one point (A23).</summary>
    public const float RegroupJitter = 1f;

    /// <summary>One regroup look at a unit (A23). <paramref name="level"/> is the game's Height.ServerHeightLevel, 0 until
    /// its HeightCorrectionSystem has run for the unit; <paramref name="tries"/> counts earlier looks. Wait while unsnapped
    /// and tries remain, Skip when unsnapped after the last try or on the anchor's level, Move otherwise.</summary>
    public static RegroupStep Step(int level, float unitY, float anchorY, int tries) =>
        level == 0 ? (tries + 1 < RegroupTries ? RegroupStep.Wait : RegroupStep.Skip)
        : Regroup(unitY, anchorY) ? RegroupStep.Move : RegroupStep.Skip;

    /// <summary>Where a regrouped unit goes: <paramref name="fraction"/> (0..1) of RegroupJitter from the anchor at
    /// <paramref name="angle"/>, at the anchor's height (A23).</summary>
    public static (float X, float Y, float Z) RegroupPoint((float X, float Y, float Z) anchor, double angle, double fraction)
    {
        var r = RegroupJitter * (float)Math.Clamp(fraction, 0, 1);
        return (anchor.X + r * (float)Math.Cos(angle), anchor.Y, anchor.Z + r * (float)Math.Sin(angle));
    }

    public static IReadOnlyList<UnitEntry> Split(IReadOnlyList<UnitEntry> units, int maxPerWave, int occupied, int maxTracked, List<string> log)
    {
        var requested = units.Sum(u => u.Count);
        var left = Precedence.WaveSize(requested, maxPerWave, occupied, maxTracked, log);
        var result = new List<UnitEntry>();
        foreach (var u in units)
        {
            if (left <= 0) break;
            var n = Math.Min(u.Count, left);
            result.Add(new UnitEntry(u.Prefab, n));
            left -= n;
        }
        return result;
    }
}

/// <summary>A running instance and its progress. <see cref="Origin"/> is the starting admin's position for an Admin
/// location, otherwise null.</summary>
public sealed class ActiveEvent(RunningInstance instance, string trigger, (float X, float Y, float Z)? origin, string? focus = null)
{
    public RunningInstance Instance { get; } = instance;
    public EventDefinition Definition => Instance.Definition;
    public string Id => Instance.Definition.Id;
    public string Trigger { get; } = trigger;
    public (float X, float Y, float Z)? Origin { get; } = origin;
    /// <summary>The platform id of the player whose action started the instance (automation D13): its AroundPlayer waves
    /// pick that player first when eligible. Memory only, never in state.json, a line, a reply, a push or the wire; null
    /// for every start that is not a player's action.</summary>
    public string? Focus { get; } = focus;
    /// <summary>Waves that spawned: the `wave=&lt;spawned&gt;/&lt;total&gt;` of the status row and `event info` (D20).</summary>
    public int WavesSpawned { get; internal set; }
    /// <summary>Waves skipped or rolled with no unit (event-spawns D29, A62).</summary>
    public int WavesSkipped { get; internal set; }
    /// <summary>The waves whose time has been used, spawned or skipped: the schedule counts these (A59, A62).</summary>
    public int WavesUsed => WavesSpawned + WavesSkipped;
    public int Faults { get; internal set; }
}

/// <summary>Wave <see cref="Wave"/> (1-based) of <see cref="Event"/> is due.</summary>
public sealed record WaveDue(ActiveEvent Event, int Wave, int Waves);

/// <summary>The units of an event that ended are queued for despawn at <see cref="DueUtc"/> (its end + GraceSeconds);
/// only units spawned before <see cref="SpawnedBefore"/> belong to that instance. It is unbounded until the same event
/// starts again inside the grace, and then that start's time, so the restart keeps its new units and a unit spawned in
/// the tick the event ended still belongs to the ended instance (A17).</summary>
public sealed record Cleanup(string EventId, DateTime SpawnedBefore, DateTime DueUtc);

/// <summary>The running events (D6, D16, D25, Business rules 2 and 8). A start reads the current definition set; the
/// instance keeps that definition until it ends, whatever a reload does. Each start's time goes into
/// <paramref name="lastStarts"/>, state.json's LastStart, so conditions.cooldownMinutes outlives a restart
/// (Design › Data).</summary>
public sealed class EventEngine(EventCatalog catalog, Func<IDictionary<string, DateTime>>? lastStarts = null)
{
    public const int FaultLimit = 3;

    readonly Dictionary<string, ActiveEvent> _active = new(StringComparer.Ordinal);
    readonly IDictionary<string, DateTime> _ownStarts = new Dictionary<string, DateTime>(StringComparer.Ordinal);
    IDictionary<string, DateTime> Starts => lastStarts?.Invoke() ?? _ownStarts;
    readonly List<Cleanup> _cleanups = [];

    public EventCatalog Catalog => catalog;

    /// <summary>Told of every start, end, wave and purge where it happens (raphael-api-core D6, A6); null reports
    /// nothing.</summary>
    public IPushSink? Push { get; set; }

    public IReadOnlyCollection<ActiveEvent> Active => _active.Values;
    public IReadOnlyList<Cleanup> PendingCleanups => _cleanups;
    public ActiveEvent? Find(string id) => _active.TryGetValue(id, out var a) ? a : null;
    public DateTime? LastStartUtc(string id) => Starts.TryGetValue(id, out var t) ? t : null;

    /// <summary>Starts the current definition <paramref name="id"/>. The reply on refusal, highest first: "unknown event",
    /// "already active", the controls of <see cref="Precedence.StartBlocker"/>, one empowerment per faction
    /// (<see cref="EventActions.EmpowerClash"/>), the Admin-location checks, then the trigger scope (regions D4, A18):
    /// a start carrying <paramref name="kill"/> (only the VBloodKilled route passes one) needs the kill in a named
    /// region, every other start needs an online player in one.</summary>
    public Outcome? Start(string id, string trigger, DateTime utcNow, ControlState controls, (float X, float Y, float Z)? origin = null,
        (float X, float Z)? kill = null, string? focus = null)
    {
        var def = catalog.Current.Find(id);
        if (def is null) return AdminLines.UnknownEvent(id);
        if (_active.ContainsKey(id)) return AdminLines.AlreadyActive(id);
        var blocker = Precedence.StartBlocker(def, controls);
        if (blocker is not null) return blocker;
        var clash = EventActions.EmpowerClash(def, _active.Values);
        if (clash is not null) return Outcome.Refused(clash, RefusalCode.State, "id", reason: Reasons.EmpowerClash);
        if (def.Action?.Location.Type == LocationType.Admin && origin is null)
            return Outcome.Refused($"event {id} spawns at the admin: start it with .nyar event start", RefusalCode.BadArg, "location", reason: Reasons.AdminLocation);
        var outside = ScopeGate.AdminOutside(def, origin, controls);
        if (outside is not null) return outside;
        var gate = ScopeGate.TriggerBlocker(def, controls, kill);
        if (gate is not null) return gate;
        var error = catalog.TryStart(id, utcNow, out var instance);
        if (error is not null) return error;
        _active[id] = new ActiveEvent(instance!, trigger, origin, focus);
        Starts[id] = utcNow;
        Push?.EventStarted(instance!);
        for (var i = 0; i < _cleanups.Count; i++)                   // the ended instance's units are all older (A17)
            if (_cleanups[i].EventId == id && _cleanups[i].SpawnedBefore > utcNow)
                _cleanups[i] = _cleanups[i] with { SpawnedBefore = utcNow };
        return null;
    }

    /// <summary>The next wave of <paramref name="id"/> when it is due: wave k (0-based) is due at start + k × interval,
    /// and a wave due at or after the event's end never comes.</summary>
    public WaveDue? NextWave(string id, DateTime utcNow)
    {
        if (!_active.TryGetValue(id, out var a) || a.Definition.Action is not { } action) return null;
        if (a.WavesUsed >= action.Waves) return null;
        var at = a.Instance.StartedUtc.AddSeconds((double)a.WavesUsed * action.IntervalSeconds);
        if (at > utcNow || at >= a.Instance.EndsUtc) return null;
        return new WaveDue(a, a.WavesUsed + 1, action.Waves);
    }

    /// <summary>A wave of <paramref name="id"/> was queued: counted, and reported with its number in the schedule.</summary>
    public void WaveSpawned(string id)
    {
        if (!_active.TryGetValue(id, out var a)) return;
        a.WavesSpawned++;
        Push?.Wave(id, a.WavesUsed);
    }

    /// <summary>The one report of a decided wave (automation D17): a spawned wave, however many groups it fanned out to, is
    /// counted and pushed once; NoWave (blocked) is neither counted nor used, as in 0.7.0; any other outcome is a skipped
    /// wave.</summary>
    public void WaveDecided(string id, WaveOutcome outcome)
    {
        if (outcome == WaveOutcome.NoWave) return;
        if (outcome == WaveOutcome.Spawn) WaveSpawned(id);
        else WaveSkipped(id);
    }

    /// <summary>A wave of <paramref name="id"/> was skipped or rolled no unit (event-spawns D29): its time is used, so the
    /// next wave comes at its own time, but it is not a spawned wave (the status row's `wave` stays spawned/total) and
    /// is not reported, since nothing spawned (D20; A59, A62).</summary>
    public void WaveSkipped(string id)
    {
        if (_active.TryGetValue(id, out var a)) a.WavesSkipped++;
    }

    /// <summary>A fault in the event's tick; true when it is the <see cref="FaultLimit"/>-th in a row, and the caller
    /// then cancels the event (D25).</summary>
    public bool Fault(string id) => _active.TryGetValue(id, out var a) && ++a.Faults >= FaultLimit;

    /// <summary>A tick without a fault ends the streak.</summary>
    public void Healthy(string id)
    {
        if (_active.TryGetValue(id, out var a)) a.Faults = 0;
    }

    /// <summary>Removes the events whose end has come and schedules their units' despawn after the grace
    /// (Business rules 2). Only a Waves event has units, so only it gets a cleanup: an Empower event has no ending phase
    /// (faction-empowerment D9).</summary>
    public IReadOnlyList<ActiveEvent> Expire(DateTime utcNow, int graceSeconds)
    {
        var ended = _active.Values.Where(a => a.Instance.EndsUtc <= utcNow).ToList();
        foreach (var a in ended)
        {
            Remove(a.Id);
            if (EventActions.ActionKindOf(a.Definition) == EventActionKind.Waves)
                _cleanups.Add(new Cleanup(a.Id, DateTime.MaxValue, a.Instance.EndsUtc.AddSeconds(graceSeconds)));
            Push?.EventEnded(a.Id, ApiLines.Region(a.Definition));
        }
        return ended;
    }

    /// <summary>The cleanups whose time has come; each is returned once.</summary>
    public IReadOnlyList<Cleanup> DueCleanups(DateTime utcNow)
    {
        var due = _cleanups.Where(c => c.DueUtc <= utcNow).ToList();
        foreach (var c in due) _cleanups.Remove(c);
        return due;
    }

    /// <summary>Ends <paramref name="id"/> now (stop, fault limit): the caller queues its units at once. Null when it is
    /// not active. A pending cleanup of an earlier instance stays.</summary>
    public ActiveEvent? Cancel(string id)
    {
        if (!_active.TryGetValue(id, out var a)) return null;
        Remove(id);
        Push?.EventEnded(id, ApiLines.Region(a.Definition));
        return a;
    }

    /// <summary>The purge: every event ends now and every pending cleanup is dropped, since the purge queues every
    /// tracked unit itself (D20). It is reported once as the purge with its <paramref name="cooldownSeconds"/>, not as
    /// one end per event.</summary>
    public IReadOnlyList<ActiveEvent> CancelAll(int cooldownSeconds = 0)
    {
        var all = _active.Values.ToList();
        foreach (var a in all) Remove(a.Id);
        _cleanups.Clear();
        Push?.Purged(cooldownSeconds);
        return all;
    }

    void Remove(string id)
    {
        _active.Remove(id);
        catalog.TryEnd(id);
    }
}

/// <summary>Debug.TimingLog (D24): the scheduler adds each tick's duration; once a minute has passed since the first
/// sample of the window it returns "tick timing: avg &lt;a&gt; ms, max &lt;m&gt; ms over &lt;n&gt; ticks" and starts a
/// new window. With the tick's phases, the closed window's <see cref="Slowest"/> is "slowest tick: &lt;t&gt; ms
/// (&lt;phase&gt; &lt;ms&gt; ms, …; outside phases &lt;r&gt; ms)", the top <see cref="SlowTickLog.MaxPhases"/> phases of its
/// slowest tick (event-spawns A70: a wave start's 206 ms tick below the slow-tick threshold could not be placed).</summary>
public sealed class TickTimer
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    public const double MinPhaseMs = 0.1;

    DateTime? _since;
    double _total;
    double _max;
    int _count;
    List<(string Phase, double Ms)> _maxPhases = new();

    /// <summary>The closed window's slowest tick by phase; null until a window closes, and after one closed without phases.</summary>
    public string? Slowest { get; private set; }

    public string? Add(double milliseconds, DateTime utcNow) => Add(milliseconds, utcNow, []);

    public string? Add(double milliseconds, DateTime utcNow, IReadOnlyList<(string Phase, double Ms)> phases)
    {
        _since ??= utcNow;
        _total += milliseconds;
        if (_count == 0 || milliseconds > _max) _maxPhases = phases.ToList();   // a copy: the caller reuses its list
        _max = Math.Max(_max, milliseconds);
        _count++;
        if (utcNow - _since.Value < Window) return null;
        var line = FormattableString.Invariant($"tick timing: avg {_total / _count:0.000} ms, max {_max:0.000} ms over {_count} ticks");
        Slowest = _maxPhases.Count == 0 ? null : SlowestLine(_max, _maxPhases);
        _since = null;
        _total = _max = 0;
        _count = 0;
        _maxPhases = new();
        return line;
    }

    static string SlowestLine(double totalMs, IReadOnlyList<(string Phase, double Ms)> phases)
    {
        var named = phases.Where(p => p.Ms >= MinPhaseMs).OrderByDescending(p => p.Ms).Take(SlowTickLog.MaxPhases)
            .Select(p => FormattableString.Invariant($"{p.Phase} {p.Ms:0.0} ms")).ToList();
        var outside = Math.Max(0, totalMs - phases.Sum(p => p.Ms));
        var slowest = named.Count > 0 ? string.Join(", ", named) : "no phase of 0.1 ms";
        return FormattableString.Invariant($"slowest tick: {totalMs:0.0} ms ({slowest}; outside phases {outside:0.0} ms)");
    }
}

/// <summary>The slow-tick warning (event-library D36, A25): a tick of <see cref="ThresholdMs"/> or more returns
/// "slow tick: &lt;t&gt; ms (&lt;phase&gt; &lt;ms&gt; ms, …; outside phases &lt;r&gt; ms)" naming its slowest phases, at most
/// one line per <see cref="Quiet"/>; the next line carries the count it held back. Independent of Debug.TimingLog, so a
/// stall on any server names the phase that waited (Session 6's 8.5 s tick could not be placed).</summary>
public sealed class SlowTickLog
{
    public const double ThresholdMs = 250;
    public const int MaxPhases = 3;
    public const double MinPhaseMs = 1;
    public static readonly TimeSpan Quiet = TimeSpan.FromMinutes(1);

    DateTime? _last;
    int _held;

    public string? Add(double totalMs, IReadOnlyList<(string Phase, double Ms)> phases, DateTime utcNow)
    {
        if (totalMs < ThresholdMs) return null;
        if (_last is { } last && utcNow >= last && utcNow - last < Quiet)   // a clock stepped back ends the quiet minute
        {
            _held++;
            return null;
        }
        _last = utcNow;
        var named = phases.Where(p => p.Ms >= MinPhaseMs).OrderByDescending(p => p.Ms).Take(MaxPhases)
            .Select(p => FormattableString.Invariant($"{p.Phase} {p.Ms:0} ms")).ToList();
        var outside = Math.Max(0, totalMs - phases.Sum(p => p.Ms));
        var slowest = named.Count > 0 ? string.Join(", ", named) : "no phase of 1 ms";
        var line = FormattableString.Invariant($"slow tick: {totalMs:0} ms ({slowest}; outside phases {outside:0} ms)");
        if (_held > 0) line += $"; {_held} more since the last line";
        _held = 0;
        return line;
    }
}
