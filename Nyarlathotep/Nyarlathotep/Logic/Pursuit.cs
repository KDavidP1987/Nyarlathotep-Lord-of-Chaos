#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace Nyarlathotep.Logic;

/// <summary>
/// The pursuit rule (wave-variety D7, D8; design §9 D61). Pure: a run whose SpawnWaves action carries `pursuit` is checked
/// every <see cref="CheckSeconds"/> s by <see cref="PursuitPhase"/>; once no eligible player (RoamRule.Eligible: online,
/// alive, a usable position, not in claimed territory, not in PvP combat; a focus-only Revenge run's focus player alone)
/// has stood within its area of the wave centre for afterSeconds in a row, counted from the first wave's decision, the
/// run pursues until it ends (ActiveEvent.Pursuing): its arena lifts, the hunt phase (HuntingGrounds.cs HuntPhase) moves
/// its units after the eligible player nearest each group by hidden jumps anywhere outside claimed territory, and Hunt
/// seeds them within 100 m (<see cref="SeedTargets"/>). The area is the arena's radius, else the Hunt range, around the
/// wave centre: the Point or the starting admin's position, else each live wave's own centre (an AroundPlayer or Region
/// wave's). Lines carry no name and no position (D11).
/// </summary>
public sealed partial record Pursuit
{
    /// <summary>How often the check runs: the behaviour cadence.</summary>
    public const int CheckSeconds = HuntPlan.IntervalSeconds;

    /// <summary>The run's action pursues when it carries the key (validation keeps it to Hunt without fanOut or regionLock);
    /// a resumed instance never does (D9: a restart starts no run pursuing; only an apocalypse resumes, and it has no
    /// SpawnWaves action).</summary>
    public static bool Configured(ActiveEvent a) => a.Definition.Action is { Pursuit: not null } && a.Trigger != "resume";

    /// <summary>The area's radius: the arena's, else the Hunt range.</summary>
    public static int Radius(SpawnWavesAction action) => action.Arena?.Radius ?? action.Behaviour?.Range ?? 0;

    /// <summary>A player the run may chase: RoamRule.Eligible, and for a focus-only run its focus player alone (null
    /// targets nobody).</summary>
    public static bool Eligible(HuntCandidate p, bool focusOnly, long? focusKey) =>
        RoamRule.Eligible(p) && (!focusOnly || focusKey == p.Key);

    /// <summary>Whether an eligible player stands within <paramref name="radius"/> m on x and z of any of
    /// <paramref name="centres"/>.</summary>
    public static bool AnyoneInArea(IEnumerable<HuntCandidate> eligible, IReadOnlyList<(float X, float Z)> centres, int radius) =>
        eligible.Any(p => centres.Any(c => Arena.Distance(c, p.X, p.Z) <= radius));

    /// <summary>The players to seed for a pursuing unit at <paramref name="at"/>: the eligible players within the seed
    /// reach (100 m), nearest first, at most HuntPlan.MaxTargets, anywhere on the map (RoamRule.Seeds' rule, with the
    /// focus).</summary>
    public static List<long> SeedTargets(IEnumerable<HuntCandidate> players, (float X, float Z) at, bool focusOnly, long? focusKey) =>
        HuntPlan.Targets(players.Where(p => Eligible(p, focusOnly, focusKey)), at, HuntingGrounds.SeedReach);

    /// <summary>"event &lt;id&gt; pursuit: on, no player within &lt;r&gt; m for &lt;s&gt; s" (D8; the service adds "[nyar] ").</summary>
    public static string OnLine(string eventId, int radius, int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"event {eventId} pursuit: on, no player within {radius} m for {seconds} s");

    /// <summary>"event &lt;id&gt; pursuit: group &lt;wave&gt;.&lt;group&gt; on a player" (D8), at each target change.</summary>
    public static string GroupLine(string eventId, int wave, int group) =>
        string.Create(CultureInfo.InvariantCulture, $"event {eventId} pursuit: group {wave}.{group} on a player");

    /// <summary>"pursuit &lt;id&gt;: check failed: &lt;message&gt;" (D12), once per streak.</summary>
    /// <summary>"pursuit &lt;id&gt;: &lt;n&gt; players read, &lt;k&gt; eligible (&lt;t&gt; in claimed territory, &lt;v&gt; in PvP combat, &lt;o&gt;
    /// other), &lt;m&gt; within &lt;r&gt; m of &lt;c&gt; centres, quiet &lt;s&gt; s" (verbose, D8): counts only (D11).</summary>
    public static string DetailLine(string eventId, int read, int eligible, int territory, int pvp, int inArea, int radius, int centres,
        int quietSeconds) =>
        string.Create(CultureInfo.InvariantCulture,
            $"pursuit {eventId}: {read} players read, {eligible} eligible ({territory} in claimed territory, {pvp} in PvP combat, {read - eligible - territory - pvp} other), {inArea} within {radius} m of {centres} centres, quiet {quietSeconds} s");

    public static string FailedLine(string eventId, string message) => $"pursuit {eventId}: check failed: {message}";
}

/// <summary>The game side of the pursuit check (wave-variety D8, D12), implemented by Services/SpawnTracker: every member
/// may throw, which the check takes as a failure of that event's check only.</summary>
public interface IPursuitWorld
{
    /// <summary>Every connected character as Hunt sees it (PlayerQuery.Read), read once per tick for every run checked;
    /// memory only.</summary>
    IReadOnlyList<HuntCandidate> Players();

    /// <summary>The point lies in claimed territory by the event's kept map (TerritoryMaps.ForHunt); false without one.</summary>
    bool IsClaimed(string eventId, float x, float z);

    /// <summary>The Hunt key of a focus-only instance's focus player, or null.</summary>
    long? FocusKey(ActiveEvent a);

    /// <summary>The centres of the instance's live waves on x and z (the hunt tags' centres), for a location without a
    /// fixed centre (AroundPlayer, Region).</summary>
    IReadOnlyList<(float X, float Z)> Centres(ActiveEvent a);
}

/// <summary>One pursuit check of one run: whether it started pursuing at this check.</summary>
public sealed record PursuitCheck(string EventId, bool Started, bool InArea);

/// <summary>
/// The pursuit phase (wave-variety D8, D9, D12): every <see cref="Pursuit.CheckSeconds"/> s while a run with `pursuit`
/// runs and does not yet pursue, each such run's players are read and its quiet clock (from the first wave's decision;
/// a check that sees an eligible player in the area stops it, and the next check that sees none starts it again, so a run
/// never turns on before afterSeconds have passed since the player left) checked; past afterSeconds the run is set pursuing and logs its
/// line once. The area's centre is the location's fixed centre (Arena.LocationCentre), else the run's live wave centres,
/// else, while no wave of the run is alive, the live centres it last saw (so players standing at an AroundPlayer or Region
/// run between its waves still count). Records (clock, last centres, streak) live in memory for the run's life: every end
/// path calls <see cref="EventEnded"/> and a restart or a purge <see cref="Clear"/>, and a record whose run no longer runs
/// is dropped at the next tick. The players are read once per tick, at the first run checked. A throwing player or
/// position read skips that event's check alone, logs "pursuit &lt;id&gt;: check failed: &lt;message&gt;" once per streak
/// and holds <see cref="Failing"/> on the health line until one of its checks runs clean, the event ends or the server
/// restarts. Never throws.
/// </summary>
public sealed class PursuitPhase
{
    public const string Failing = "pursuit: check failing";

    readonly Dictionary<string, DateTime> _quietSince = new(StringComparer.Ordinal);
    readonly Dictionary<string, IReadOnlyList<(float X, float Z)>> _lastCentres = new(StringComparer.Ordinal);
    readonly SortedSet<string> _failing = new(StringComparer.Ordinal);
    bool _phaseFailing;
    DateTime _next;

    /// <summary>Verbose only (the service sets it while VerboseLogging is on): one line per check with its counts, why a
    /// run turns on or does not; no name and no position (D11).</summary>
    public Action<string>? Detail { get; set; }

    /// <summary>The health entry: one while any run's check, or the phase itself, is failing.</summary>
    public IReadOnlyList<string> Health => _failing.Count > 0 || _phaseFailing ? [Failing] : [];

    /// <summary>Every record held (quiet clocks, last centres and streaks), for the end-path checks.</summary>
    public int AllRecords => _quietSince.Count + _lastCentres.Count + _failing.Count;

    /// <summary>The quiet clock of a run whose last check saw an eligible player in its area: stopped until a check sees
    /// none.</summary>
    static readonly DateTime Seen = DateTime.MaxValue;

    /// <summary>When the run's quiet clock started, or null (none, or stopped by a player in the area).</summary>
    public DateTime? QuietSince(string eventId) => _quietSince.TryGetValue(eventId, out var t) && t != Seen ? t : null;

    /// <summary>True when any running instance needs the check.</summary>
    public static bool Needed(IEnumerable<ActiveEvent> active) => active.Any(a => Pursuit.Configured(a) && !a.Pursuing);

    /// <summary>An end path of <paramref name="eventId"/> (natural, all waves defeated, stop, purge, pillar off, fault): its
    /// clock and streak go with it; the instance's Pursuing flag ends with the instance.</summary>
    public void EventEnded(string eventId)
    {
        _quietSince.Remove(eventId);
        _lastCentres.Remove(eventId);
        _failing.Remove(eventId);
    }

    /// <summary>A restart or a purge: no clock, no streak, the next check on the next tick.</summary>
    public void Clear()
    {
        _quietSince.Clear();
        _lastCentres.Clear();
        _failing.Clear();
        _phaseFailing = false;
        _next = default;
    }

    /// <summary>One scheduler tick: records of runs no longer running are dropped first; then, when due, one check per run
    /// with `pursuit` not yet pursuing. Returns the checks that ran (for the tests); never throws.</summary>
    public IReadOnlyList<PursuitCheck> Tick(DateTime now, IReadOnlyCollection<ActiveEvent> active, IPursuitWorld world, Action<string> info,
        Action<string> error)
    {
        var checks = new List<PursuitCheck>();
        try
        {
            var waiting = active.Where(a => Pursuit.Configured(a) && !a.Pursuing).ToList();
            foreach (var id in _quietSince.Keys.Where(id => !waiting.Any(a => a.Id == id)).ToList()) _quietSince.Remove(id);
            foreach (var id in _lastCentres.Keys.Where(id => !waiting.Any(a => a.Id == id)).ToList()) _lastCentres.Remove(id);
            _failing.RemoveWhere(id => !waiting.Any(a => a.Id == id));
            if (waiting.Count == 0 || now < _next) return checks;
            _next = now.AddSeconds(Pursuit.CheckSeconds);
            IReadOnlyList<HuntCandidate>? read = null;
            string? readFault = null;
            IReadOnlyList<HuntCandidate> Players()                             // once per tick; a failed read fails each run's check
            {
                if (readFault is not null) throw new InvalidOperationException(readFault);
                if (read is not null) return read;
                try { return read = world.Players(); }
                catch (Exception e)
                {
                    readFault = e.Message;
                    throw;
                }
            }
            foreach (var a in waiting)
                if (Run(now, a, world, Players, info, error) is { } c) checks.Add(c);
            _phaseFailing = false;
        }
        catch (Exception e)
        {
            if (!_phaseFailing) Phases.Safe(error, Pursuit.FailedLine("phase", e.Message));
            _phaseFailing = true;
        }
        return checks;
    }

    /// <summary>One check of <paramref name="a"/> reading the players itself (the tests' entry).</summary>
    public PursuitCheck? Run(DateTime now, ActiveEvent a, IPursuitWorld world, Action<string> info, Action<string> error) =>
        Run(now, a, world, world.Players, info, error);

    /// <summary>One check of <paramref name="a"/> on the tick's <paramref name="players"/> read, with the event's own claimed
    /// check: nothing before its first wave's decision; null when it could not run (a failed read) or does not apply.
    /// Never throws.</summary>
    public PursuitCheck? Run(DateTime now, ActiveEvent a, IPursuitWorld world, Func<IReadOnlyList<HuntCandidate>> players, Action<string> info,
        Action<string> error)
    {
        string? fault = null;
        PursuitCheck? done = null;
        try
        {
            if (!Pursuit.Configured(a) || a.Pursuing || a.Definition.Action is not { Pursuit: { } pursuit } action || a.Decided.Count == 0)
                return null;
            if (!_quietSince.TryGetValue(a.Id, out var since)) _quietSince[a.Id] = since = a.Decided[0].DecidedUtc;
            var focusOnly = WaveFacts.IsFocusOnly(a.Definition);
            var focus = focusOnly ? world.FocusKey(a) : null;
            var read = players().Select(p => p with { InTerritory = p.InTerritory || world.IsClaimed(a.Id, p.X, p.Z) }).ToList();
            var eligible = read.Where(p => Pursuit.Eligible(p, focusOnly, focus)).ToList();
            IReadOnlyList<(float X, float Z)> centres;
            if (Arena.LocationCentre(a) is { } c) centres = [c];
            else
            {
                centres = world.Centres(a);
                if (centres.Count > 0) _lastCentres[a.Id] = centres.ToList();     // kept for the gaps between waves
                else if (_lastCentres.TryGetValue(a.Id, out var last)) centres = last;
            }
            var radius = Pursuit.Radius(action);
            var present = Pursuit.AnyoneInArea(eligible, centres, radius);
            if (!present && since == Seen) _quietSince[a.Id] = since = now;                    // the first check with nobody
            if (Detail is { } detail)
                Phases.Safe(detail, Pursuit.DetailLine(a.Id, read.Count, eligible.Count, read.Count(p => p.InTerritory),
                    read.Count(p => !p.InTerritory && p.InPvpCombat), eligible.Count(p => centres.Any(c => Arena.Distance(c, p.X, p.Z) <= radius)),
                    radius, centres.Count, present ? 0 : (int)(now - since).TotalSeconds));
            if (present)
            {
                _quietSince[a.Id] = Seen;
                done = new PursuitCheck(a.Id, false, true);
            }
            else if ((now - since).TotalSeconds >= pursuit.AfterSeconds)
            {
                a.Pursuing = true;
                _quietSince.Remove(a.Id);
                _lastCentres.Remove(a.Id);
                Phases.Safe(info, Pursuit.OnLine(a.Id, radius, (int)(now - since).TotalSeconds));
                done = new PursuitCheck(a.Id, true, false);
            }
            else done = new PursuitCheck(a.Id, false, false);
        }
        catch (Exception e) { fault = e.Message; }
        if (fault is null) _failing.Remove(a.Id);
        else if (_failing.Add(a.Id)) Phases.Safe(error, Pursuit.FailedLine(a.Id, fault));
        return done;
    }
}
