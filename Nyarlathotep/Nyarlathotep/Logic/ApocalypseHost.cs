#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace Nyarlathotep.Logic;

// The apocalypse's driver (docs/dod/apocalypse.md D3-D15, D21; step 3): the one run, its tick (rise, march, hop, roam,
// renewals, defeats, outcome, record), the spread feed, every end path's record removal and the boot resume behind the
// drain gate. Services/ApocalypseAction implements IApocalypseGame over SpawnTracker, PlayerQuery and the walk check and
// calls in on the server main thread; nothing here throws into a tick: each game call sits in the try/catch of its
// dependency category (native-read, spread-read, march-read, resume-read, state-write), logged once per streak.

/// <summary>The game side of the apocalypse (step 3). Every member may throw; <see cref="ApocalypseHost"/> guards each by
/// its D21 category. Writes reach our own tracked units only (<see cref="Move"/>, <see cref="Renew"/>).</summary>
public interface IApocalypseGame : IApocalypseWorld
{
    /// <summary>The native NPCs the hotspot reader sees (D2): read only.</summary>
    IEnumerable<HotspotNative> Natives();

    /// <summary>Every connected character, memory only (the march, the hold and the roam).</summary>
    IReadOnlyList<HuntCandidate> Players();

    /// <summary>One tracked unit's position and level; null when it is gone since the ledger listed it.</summary>
    RegionUnit? Read(TrackedUnit unit);

    /// <summary>The point lies in claimed territory by the run's kept map (fail closed: true without one).</summary>
    bool IsClaimed(string eventId, float x, float z);

    /// <summary>Free and grounded at <paramref name="level"/>.</summary>
    bool Walkable(float x, float z, byte level);

    float HeightOf(byte level);

    WalkBudget Budget { get; }

    /// <summary>Moves one of our own units to the jump's landing at once (the step-1 verdict "go — hop"): Translation,
    /// LastTranslation, its return point, its path cleared and its velocity zeroed.</summary>
    void Move(RegionJump jump);

    /// <summary>A tracked unit's new due time and LifeTime (D13), through SpawnTracker.</summary>
    void Renew(long key, UnitLifetime life);

    /// <summary>The display name of the region at a point, null in none.</summary>
    string? RegionName(float x, float z);

    /// <summary>Builds the run's claimed-territory map for its hunt and roam (TerritoryMap.ForWave); never throws.</summary>
    void MapTerritory(string eventId);

    /// <summary>A resume that failed after the engine resumed the run cancels it outside EventRuntime's end paths: the
    /// event's spawn state beyond its units goes here (hunt seeds, territory map, failure streaks, region and unit-watch
    /// rows; EventRuntime.EndSpawnState). Never throws.</summary>
    void ResumeCancelled(string eventId);

    /// <summary>The boot sweep's despawns still queued (the drain gate, D12).</summary>
    int LeftToDrain { get; }

    void Log(string line);
    void Warn(string line);

    /// <summary>Queues a player line (AnnounceQueue, Info, as the scoreboard does, D7).</summary>
    void Announce(string eventId, string line);
}

/// <summary>What one apocalypse tick did: the outcome (won or lost end the run; survived is the engine's expiry) and the
/// horde that rose, with its queued count, for the engine's wave count (D20).</summary>
public sealed record ApocalypseStep(ApocalypseResult Result, int? Rose = null, int Queued = 0)
{
    public static readonly ApocalypseStep None = new(ApocalypseResult.None);
}

/// <summary>One death's spread read, taken before SpawnTracker.Died (D4): its ledger sides and the victim's x and z.</summary>
public readonly record struct SpreadRead(ScoreSides Sides, (float X, float Z) Victim);

/// <summary>
/// The parts of one apocalypse step, timed (D29's tick budget; Session 3 logged a 2.3 s "slow tick" inside the apocalypse
/// phase with no part named): a step of <see cref="SlowTickLog.ThresholdMs"/> or more gives "apocalypse &lt;id&gt; slow step:
/// &lt;t&gt; ms (&lt;part&gt; &lt;ms&gt; ms, …; units &lt;n&gt;, walk calls &lt;c&gt;)", its parts slowest first, so the next one names
/// where the time went.
/// </summary>
public sealed class StepParts
{
    /// <summary>The timestamp source and its ticks per second (a test sets a fake).</summary>
    public Func<long> Clock { get; set; } = System.Diagnostics.Stopwatch.GetTimestamp;
    public long Frequency { get; set; } = System.Diagnostics.Stopwatch.Frequency;

    readonly Dictionary<string, double> _ms = [];
    long _start, _mark;

    public void Start()
    {
        _ms.Clear();
        _start = _mark = Clock();
    }

    /// <summary>Adds the time since the last mark to <paramref name="part"/>.</summary>
    public void Mark(string part)
    {
        var now = Clock();
        _ms[part] = (_ms.TryGetValue(part, out var was) ? was : 0) + (now - _mark) * 1000.0 / Frequency;
        _mark = now;
    }

    /// <summary>The slow-step line, or null below the threshold.</summary>
    public string? Line(string eventId, int units, int walkCalls)
    {
        var total = (Clock() - _start) * 1000.0 / Frequency;
        if (total < SlowTickLog.ThresholdMs) return null;
        var parts = string.Join(", ", _ms.Where(p => p.Value >= SlowTickLog.MinPhaseMs).OrderByDescending(p => p.Value)
            .Select(p => FormattableString.Invariant($"{p.Key} {p.Value:0} ms")));
        if (parts.Length == 0) parts = "no part of 1 ms";
        return FormattableString.Invariant($"apocalypse {eventId} slow step: {total:0} ms ({parts}; units {units}, walk calls {walkCalls})");
    }
}

/// <summary>A straggler's hop landing (D6, the step-1 verdict "go — hop"): a walkable point within
/// <see cref="Radius"/> m of a spot <see cref="Lead"/> m ahead of the front, at the unit's own level or a neighbouring one,
/// then the wide levels.</summary>
public static class HopRule
{
    public const float Radius = 8f;

    /// <summary>How far ahead of the front a landing is centred, towards the goal and never past it: the front walks on until
    /// the next hop, so units landed a little ahead stay within 30 m of it longest (session 1).</summary>
    public const float Lead = 7.5f;

    /// <summary>The landing centre: <see cref="Lead"/> m from the front towards the goal, or the goal when it is nearer.</summary>
    public static (float X, float Z) Ahead(Front front)
    {
        var left = Apocalypse.Distance(front.X, front.Z, front.GoalX, front.GoalZ);
        if (left <= Lead) return (front.GoalX, front.GoalZ);
        var t = Lead / left;
        return (front.X + (front.GoalX - front.X) * t, front.Z + (front.GoalZ - front.Z) * t);
    }

    /// <summary>The walk calls a tick keeps for the fronts' ground checks and crossings: a straggler's landing search stops
    /// below it, so many unlandable stragglers cannot spend the budget the march reads its ground with (code review M1).</summary>
    public const int FrontReserve = 700;

    public static RegionJump? Land(RegionUnit u, Front front, int i, int n, Func<float, float, byte, bool> walkable, Func<byte, float> heightOf,
        WalkBudget budget)
    {
        if (budget.Left < FrontReserve) return null;
        // the unit's own slot first, at every level; then the front point and rings of 8 points out to 25 m at the unit's
        // level and its neighbours (session 1: one point per unit left 9 of 16 hops unlanded on rough ground)
        var (cx, cz) = Ahead(front);
        var (x, z) = SpawnLedger.Around(cx, cz, Radius, i, n, 0.0);
        foreach (var level in WalkLevels.Spot(u.Level).Concat(WalkLevels.Wide(u.Level)))
        {
            if (!budget.TryTake(2)) return null;
            if (walkable(x, z, level)) return new RegionJump(u.Key, x, heightOf(level), z, level, float.MaxValue);
        }
        foreach (var (px, pz) in Rings(cx, cz, i))
            foreach (var level in WalkLevels.Spot(u.Level))
            {
                if (budget.Left < FrontReserve || !budget.TryTake(2)) return null;
                if (walkable(px, pz, level)) return new RegionJump(u.Key, px, heightOf(level), pz, level, float.MaxValue);
            }
        return null;
    }

    /// <summary>The fallback landings around a front: the point itself, then 8 points at 5, 15 and 25 m, each ring turned by
    /// the unit's index so units spread over the ring.</summary>
    public static IEnumerable<(float X, float Z)> Rings(float x, float z, int i)
    {
        yield return (x, z);
        foreach (var r in new[] { 5f, 15f, 25f })
            for (var k = 0; k < 8; k++)
                yield return SpawnLedger.Around(x, z, r, (k + i) % 8, 8, 0.0);
    }

    /// <summary>The front's next point has walkable ground within 30 m (D6): the point itself, then four points 15 m and
    /// four 30 m off, at <paramref name="level"/> and its neighbours. With no level known (no living unit read) or the
    /// tick's walk budget spent the ground is taken as there, so a budget never holds a march.</summary>
    public static bool Ground(float x, float z, byte? level, Func<float, float, byte, bool> walkable, WalkBudget budget) =>
        level is null || GroundRead(x, z, level, walkable, budget) != false;

    /// <summary><see cref="Ground"/> as read: null when it could not be read (no level, or the budget spent before an
    /// answer), which a crossing treats as no ground to cross to (code review M1).</summary>
    public static bool? GroundRead(float x, float z, byte? level, Func<float, float, byte, bool> walkable, WalkBudget budget)
    {
        if (level is not { } h) return null;
        foreach (var r in new[] { 0f, 15f, 30f })
            for (var k = 0; k < (r == 0 ? 1 : 4); k++)
            {
                var (px, pz) = r == 0 ? (x, z) : SpawnLedger.Around(x, z, r, k, 4, 0.0);
                foreach (var level2 in WalkLevels.Spot(h))
                {
                    if (!budget.TryTake(2)) return null;
                    if (walkable(px, pz, level2)) return true;
                }
            }
        // a slope that climbs past the neighbouring levels: the wide levels at the point itself (session 1: a horde held
        // for good at 12 % where the ground rose); hops land the units there the same way
        foreach (var level3 in WalkLevels.Wide(h))
        {
            if (!budget.TryTake(2)) return null;
            if (walkable(x, z, level3)) return true;
        }
        return false;
    }
}

/// <summary>
/// The running apocalypse and its resume (D3-D15, D21). One run at a time (D14). Every member returns normally: a game call
/// that throws is caught in its category, logged once per streak, and its health entry (spread, march, roam move) shows
/// until a call of that category succeeds.
/// </summary>
public sealed class ApocalypseHost
{
    public const string SpreadFailing = "apocalypse: spread read failing";
    public const string MarchFailing = "apocalypse: march read failing";
    public const string RoamFailing = "apocalypse: roam move failing";

    /// <summary>Hops, roam moves and the keep-up line every 5 s (the hunt's interval); the keep-up line each 30 s of a march
    /// (D6, D26); renewals each 60 s (D13).</summary>
    public static readonly TimeSpan MoveEvery = TimeSpan.FromSeconds(HuntingGrounds.CheckSeconds);
    public static readonly TimeSpan KeepUpEvery = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RenewEvery = TimeSpan.FromSeconds(60);
    /// <summary>A march step never covers more than this, so a held tick (a failed read) is never made up later (D21).</summary>
    public const double MaxStepSeconds = 2.0;

    readonly FailureStreak _native = new(), _spread = new(), _march = new(), _roam = new(), _resume = new(), _record = new(), _renew = new(), _rise = new();
    ApocalypseRun? _run;
    (string Id, IReadOnlyList<Hotspot> Spots, string Line)? _prepared;
    RoamTargets _roamTargets = new();
    readonly HashSet<int> _defeated = [];
    readonly Dictionary<int, DateTime> _keepUp = [];
    readonly Dictionary<int, (int Moved, int Stragglers)> _hops = [];
    DateTime? _lastMarch, _lastMove, _lastRenew, _lastRecord;
    ApocalypseRecord? _pending;
    DateTime _sweptUtc;
    bool _drainWarned;

    /// <summary>The running apocalypse, null with none.</summary>
    public ApocalypseRun? Run => _run;

    /// <summary>The record a boot resume waits on behind the drain gate, null with none (D12).</summary>
    public ApocalypseRecord? Pending => _pending;

    public IReadOnlyList<string> Health
    {
        get
        {
            var h = new List<string>();
            if (_spread.Count > 0) h.Add(SpreadFailing);
            if (_march.Count > 0) h.Add(MarchFailing);
            if (_roam.Count > 0) h.Add(RoamFailing);
            return h;
        }
    }

    void Reset(ApocalypseRun? run)
    {
        _run = run;
        _roamTargets = new RoamTargets();
        _defeated.Clear();
        _keepUp.Clear();
        _hops.Clear();
        _lastMarch = _lastMove = _lastRenew = _lastRecord = null;
        _march.Ok();
        _roam.Ok();
        _spread.Ok();
    }

    // ---- the start (D2, D21 native-read)

    /// <summary>The hotspots of <paramref name="def"/>, read just before its instance starts (Engine.Start's last gate):
    /// null to go on, or the refusal. A throwing native read refuses with "no hotspot: native read failed: &lt;message&gt;",
    /// logged once per streak; fewer than one hotspot with "no hotspot: …", logged. Kept for <see cref="Begin"/>.</summary>
    public Outcome? Prepare(EventDefinition def, IApocalypseGame game)
    {
        _prepared = null;
        if (def.Apocalypse is not { } action) return null;
        var pick = Hotspots.Pick(def.Id, action, game.Natives);
        if (pick.Refusal is { } refusal)
        {
            var failed = refusal.StartsWith(Hotspots.ReadFailed(""), StringComparison.Ordinal);
            if (failed) { if (_native.Fail()) Say(game.Warn, pick.Line); }
            else { _native.Ok(); Say(game.Log, pick.Line); }
            return Outcome.Refused(refusal, RefusalCode.State, "id", reason: failed ? Reasons.Read : Reasons.Condition);
        }
        _native.Ok();
        _prepared = (def.Id, pick.Spots, pick.Line);
        return null;
    }

    /// <summary>The run of <paramref name="active"/>, started with the hotspots <see cref="Prepare"/> found: its goal fixed
    /// now (an Admin goal at the starting admin), its territory map built, every hotspot topped up (D3) and the record
    /// written (D11). False when nothing was prepared for it.</summary>
    public bool Begin(ActiveEvent active, IApocalypseGame game, StateStore state, Scoreboard board, DateTime now)
    {
        if (_prepared is not { } p || p.Id != active.Id || active.Definition.Apocalypse is not { } action) return false;
        _prepared = null;
        var goal = Apocalypse.ResolveGoal(action.Goal, active.Origin) ?? (0f, 0f, 0f);
        Reset(new ApocalypseRun(active, p.Spots, goal));
        Say(game.Log, p.Line);
        Guard(_record, game, "start", () =>
        {
            game.MapTerritory(active.Id);
            foreach (var line in _run!.TopUp(game, now)) Say(game.Log, line);
        });
        WriteRecord(game, state, board, now);
        return true;
    }

    // ---- the tick

    /// <summary>One second of the run of <paramref name="active"/> (D3-D8, D11, D13): the due horde rises, the fronts march
    /// (holding for players, D6), every 5 s the stragglers hop while unobserved and the roamers roam map-wide, every 60 s the
    /// lifetimes roll, defeats are announced, and the outcome is decided; the record is written by its clock.</summary>
    public ApocalypseStep Tick(ActiveEvent active, IApocalypseGame game, StateStore state, Scoreboard board, DateTime now)
    {
        if (_run is not { } run || !ReferenceEquals(run.Active, active)) return ApocalypseStep.None;
        Parts.Start();
        var budgetLeft = Safe(game, () => game.Budget.Left);
        _stepUnits = 0;
        try { return Step(run, active, game, state, board, now); }
        finally
        {
            var walkCalls = Math.Max(0, budgetLeft - Safe(game, () => game.Budget.Left));
            if (Parts.Line(run.Id, _stepUnits, walkCalls) is { } slow) Say(game.Log, slow);
        }
    }

    /// <summary>The timed parts of the last step (D29).</summary>
    public StepParts Parts { get; } = new();
    int _stepUnits;

    ApocalypseStep Step(ApocalypseRun run, ActiveEvent active, IApocalypseGame game, StateStore state, Scoreboard board, DateTime now)
    {
        var action = run.Action;
        var forced = false;
        int? rose = null;
        var queued = 0;

        (HordeRise, IReadOnlyList<string>)? rise = null;
        try
        {
            if (run.Rise(game, now, out var roamerLines) is { } risen) rise = (risen, roamerLines);
            _rise.Ok();
        }
        catch (Exception e) { if (_rise.Fail()) Say(game.Warn, $"apocalypse: rise skipped: {e.Message}"); }
        Parts.Mark("rise");
        if (rise is { } done)
        {
            var (r, roamerLines) = done;
            Safe(game, () => { game.MapTerritory(run.Id); return 0; });
            Say(game.Log, string.Create(CultureInfo.InvariantCulture, $"apocalypse {run.Id} horde {r.Horde}/{action.Hordes.Count} rose at hotspot {r.Hotspot}: {r.Queued} units"));
            foreach (var l in r.Log) Say(game.Log, $"apocalypse {run.Id} horde {r.Horde}: {l}");
            foreach (var l in roamerLines) Say(game.Log, l);
            Say(game.Log, run.Tally.Line(run.Id, Alive(game, run)));
            var spot = run.Hotspots[r.Hotspot - 1];
            var region = Safe(game, () => game.RegionName(spot.X, spot.Z));
            game.Announce(run.Id, Messages.ApocalypseRise(active.Definition.Name, r.Queued, region, action.Goal.Name));
            forced = true;
            rose = r.Horde;
            queued = r.Queued;
        }

        // the march-read category: the players and the run's units, then the hops, then the fronts
        var seconds = _lastMarch is { } last ? Math.Clamp((now - last).TotalSeconds, 0, MaxStepSeconds) : 0;
        _lastMarch = now;
        var moveDue = _lastMove is not { } lm || now - lm >= MoveEvery;
        if (moveDue) _lastMove = now;
        IReadOnlyList<HuntCandidate>? players = null;
        List<RegionUnit> units = [];
        var marching = false;
        try
        {
            players = game.Players();
            foreach (var u in game.Ledger.Units.Where(u => Apocalypse.Ours(u, active)).ToList())
                if (game.Read(u) is { } read) units.Add(read);
            _stepUnits = units.Count;
            Parts.Mark("read");
            var online = players.Where(p => p.Online && PlayerPosition.Usable(p.X, p.Z)).Select(p => (p.X, p.Z)).ToList();
            foreach (var front in run.Fronts.Values.Where(f => !_defeated.Contains(f.Horde)).ToList())
            {
                var horde = units.Where(u => Apocalypse.HordeOfWave(u.Wave) == front.Horde).ToList();
                // the watch time first, from the front and every living unit of the horde, so no hop is decided on a stale
                // one (code review M3)
                MarchRule.Watch(front, online, horde.Select(u => (u.X, u.Z)), now);
                if (moveDue)
                {
                    var stragglers = MarchRule.Stragglers(front, horde.Select(u => (u.Key, u.X, u.Z)), online, now);
                    var moved = 0;
                    for (var i = 0; i < stragglers.Count; i++)
                    {
                        var at = horde.FindIndex(h => h.Key == stragglers[i]);
                        var u = horde[at];
                        if (HopRule.Land(u, front, i, stragglers.Count, game.Walkable, game.HeightOf, game.Budget) is not { } hop) continue;
                        game.Move(hop);
                        moved++;
                        // the keep-up count reads the unit where it landed, not where it stood before the hop (session 1)
                        horde[at] = u with { X = hop.X, Y = hop.Y, Z = hop.Z, Level = hop.Level };
                    }
                    var sum = _hops.TryGetValue(front.Horde, out var h0) ? h0 : (Moved: 0, Stragglers: 0);
                    _hops[front.Horde] = (sum.Moved + moved, sum.Stragglers + stragglers.Count);
                    Parts.Mark("hops");
                }
                byte? level = horde.Count == 0 ? null : horde.OrderBy(u => Apocalypse.Distance(u.X, u.Z, front.X, front.Z)).First().Level;
                var step = MarchRule.Step(front, action.Goal.Radius, action.MarchSpeed, seconds, online,
                    (x, z) => HopRule.Ground(x, z, level, game.Walkable, game.Budget), now, out var held,
                    (x, z) => HopRule.GroundRead(x, z, level, game.Walkable, game.Budget));
                if (held is not null) Say(game.Log, $"apocalypse {run.Id} {held}");
                if (step == MarchStep.Moved) marching = true;
                var crossed = MarchRule.NewSteps(front);
                foreach (var pct in crossed) game.Announce(run.Id, Messages.ApocalypseProgress(active.Definition.Name, pct, action.Goal.Name));
                if (step is MarchStep.Moved or MarchStep.HeldNoGround && MarchRule.IsUnobserved(front, now)
                    && (crossed.Count > 0 || !_keepUp.TryGetValue(front.Horde, out var kept) || now - kept >= KeepUpEvery))
                {
                    _keepUp[front.Horde] = now;
                    Say(game.Log, $"apocalypse {run.Id} " + MarchRule.KeepUpLine(front.Horde, front.Percent, MarchRule.KeepUp(front, horde.Select(u => (u.X, u.Z)).ToList())));
                    var hops = _hops.TryGetValue(front.Horde, out var hp) ? hp : (Moved: 0, Stragglers: 0);
                    _hops[front.Horde] = (0, 0);
                    Say(game.Log, $"apocalypse {run.Id} horde {front.Horde} hops: {hops.Moved} of {hops.Stragglers} stragglers moved since the last line");
                }
                Parts.Mark("march");
            }
            _march.Ok();
        }
        catch (Exception e)
        {
            players = null;
            if (_march.Fail()) Say(game.Warn, $"apocalypse: march skipped: {e.Message}");
        }

        if (moveDue && players is not null) Roam(run, game, players, units);
        Parts.Mark("roam");

        if (_lastRenew is not { } lr || now - lr >= RenewEvery)
        {
            _lastRenew = now;
            Renew(run, game, now);
        }
        Parts.Mark("renew");

        // defeats (D7): a risen horde with nothing left, bosses included, once
        foreach (var k in run.Fronts.Keys.OrderBy(k => k).ToList())
        {
            if (_defeated.Contains(k) || !Safe(game, () => run.Cleared(game, k))) continue;
            _defeated.Add(k);
            Say(game.Log, $"apocalypse {run.Id} horde {k} of {action.Hordes.Count} destroyed");
            game.Announce(run.Id, Messages.ApocalypseDefeat(active.Definition.Name, k, action.Hordes.Count));
            forced = true;
        }

        var living = units.GroupBy(u => Apocalypse.HordeOfWave(u.Wave) ?? 0).ToDictionary(g => g.Key, g => (IReadOnlyList<(float X, float Z)>)g.Select(u => (u.X, u.Z)).ToList());
        var result = Safe(game, () => run.Outcome(game, k => living.TryGetValue(k, out var l) ? l : [], now));
        Parts.Mark("outcome");
        if (result is ApocalypseResult.Won or ApocalypseResult.Lost) return new ApocalypseStep(result, rose, queued);
        if (RecordClock.Due(_lastRecord, now, marching, forced)) WriteRecord(game, state, board, now);
        Parts.Mark("record");
        return new ApocalypseStep(ApocalypseResult.None, rose, queued);
    }

    /// <summary>The roam (D3, A2): each hotspot's roamers go after the eligible player nearest their mean position, anywhere
    /// outside claimed territory, by JumpRule's plan; the roam line once per hotspot and target change. A throwing move
    /// leaves the roamers where they stand, logs once per streak and holds <see cref="RoamFailing"/> until a move succeeds.</summary>
    void Roam(ApocalypseRun run, IApocalypseGame game, IReadOnlyList<HuntCandidate> players, IReadOnlyList<RegionUnit> units)
    {
        try
        {
            var candidates = players.Select(p => p with { InTerritory = game.IsClaimed(run.Id, p.X, p.Z) }).ToList();
            foreach (var spot in run.Hotspots)
            {
                var roamers = units.Where(u => Apocalypse.HotspotOfWave(u.Wave) == spot.Number).ToList();
                if (roamers.Count == 0) { _roamTargets.Seen(run.Id, spot.Number, null); continue; }
                var mean = ((float)roamers.Average(u => (double)u.X), (float)roamers.Average(u => (double)u.Z));
                var target = RoamRule.Target(candidates, mean);
                if (_roamTargets.Seen(run.Id, spot.Number, target?.Key) is { } line) Say(game.Log, line);
                if (target is not { } t) continue;
                foreach (var u in roamers)
                {
                    var (kind, jump) = RoamRule.Plan(u, (t.X, t.Z), players, (x, z) => game.IsClaimed(run.Id, x, z), game.Walkable, game.HeightOf, game.Budget);
                    if (kind == JumpKind.Jump && jump is { } j) game.Move(j);
                }
            }
            _roam.Ok();
        }
        catch (Exception e)
        {
            if (_roam.Fail()) Say(game.Warn, $"apocalypse: roam move skipped: {e.Message}");
        }
    }

    /// <summary>The rolling lifetime (D13): every living unit of the run with less than 90 min left is renewed to now + 2 h
    /// (never past the run's end + grace); one line when any was.</summary>
    void Renew(ApocalypseRun run, IApocalypseGame game, DateTime now)
    {
        try
        {
            var renewed = 0;
            foreach (var u in game.Ledger.Units.Where(u => Apocalypse.Ours(u, run.Active)).ToList())
                if (RollingLifetime.Renew(u, true, now, run.Active.Instance.EndsUtc, game.GraceSeconds, game.DrainMarginSeconds) is { } life)
                {
                    game.Renew(u.Key, life);
                    renewed++;
                }
            if (renewed > 0) Say(game.Log, RollingLifetime.Line(run.Id, renewed));
            _renew.Ok();
        }
        catch (Exception e)
        {
            if (_renew.Fail()) Say(game.Warn, $"apocalypse: renewal skipped: {e.Message}");
        }
    }

    /// <summary>The record now (D11), into state.json through Persistence's dirty flag: a failed write is the store's own
    /// streak, the run goes on (D21 state-write); a throw building it is logged once per streak.</summary>
    void WriteRecord(IApocalypseGame game, StateStore state, Scoreboard board, DateTime now)
    {
        if (_run is not { } run) return;
        try
        {
            state.Document.Apocalypse = run.Record(game, board.Export(run.Id), now);
            state.MarkDirty();
            _lastRecord = now;
            _record.Ok();
        }
        catch (Exception e)
        {
            if (_record.Fail()) Say(game.Warn, $"apocalypse: record write skipped: {e.Message}");
        }
    }

    static int Alive(IApocalypseWorld w, ApocalypseRun run) => w.Ledger.Units.Count(u => Apocalypse.Ours(u, run.Active));

    // ---- the spread (D4, D21 spread-read)

    /// <summary>The spread read of one death, before SpawnTracker.Died: null with no run, or when the read throws (logged
    /// "apocalypse: spread skipped: &lt;message&gt;" once per streak, <see cref="SpreadFailing"/> held).</summary>
    public SpreadRead? Before(Func<ScoreSides> sides, Func<(float X, float Z)> victim, IApocalypseGame game)
    {
        if (_run is null) return null;
        try { return new SpreadRead(sides(), victim()); }
        catch (Exception e) { SpreadFailed(game, e); return null; }
    }

    /// <summary>After SpawnTracker.Died: a qualifying kill queues its spread roamers (D4). A good read ends the streak.</summary>
    public void After(SpreadRead? read, IApocalypseGame game, DateTime now)
    {
        if (read is not { } r || _run is not { } run) return;
        try
        {
            if (run.SpreadOn(game, r.Sides, r.Victim, now) is { } line) Say(game.Log, line);
            _spread.Ok();
        }
        catch (Exception e) { SpreadFailed(game, e); }
    }

    void SpreadFailed(IApocalypseGame game, Exception e)
    {
        if (_spread.Fail()) Say(game.Warn, $"apocalypse: spread skipped: {e.Message}");
    }

    // ---- the tally (D15): SpawnTracker's confirmations, failures and departures

    /// <summary>An order of <paramref name="eventId"/> settled: spawned or failed.</summary>
    public void Settled(string? eventId, int wave, bool spawned)
    {
        if (_run is not { } run || eventId != run.Id) return;
        if (spawned) run.Spawned(wave);
        else run.FailedToSpawn(wave);
    }

    /// <summary>A unit of <paramref name="eventId"/> spawned at <paramref name="spawnedUtc"/> left the game.</summary>
    public void Left(string? eventId, DateTime spawnedUtc, UnitDeparture how)
    {
        if (_run is not { } run || eventId != run.Id || spawnedUtc < run.Active.Instance.StartedUtc) return;
        switch (how)
        {
            case UnitDeparture.Died: run.Tally.Death(); break;
            case UnitDeparture.Despawned: run.Tally.Despawn(); break;
            default: run.Tally.Removal(); break;
        }
    }

    // ---- the end paths (D8, D9)

    /// <summary>Every end path of <paramref name="id"/>: an end that ends the run drops it and logs its tally line (D15);
    /// every path but restart and disable removes the resume record (D9), a pending resume's included, so a purge or pillar
    /// off in the drain wait rebuilds nothing. <paramref name="cancelled"/> orders an end cancelled count as failed.</summary>
    public void Ended(string id, ApocalypseEnd path, IApocalypseGame game, StateStore state, int cancelled = 0)
    {
        if (!EndRule.EndsRun(path)) return;
        if (_run is { } run && run.Id == id)
        {
            if (cancelled > 0) run.Tally.FailedToSpawn(cancelled);
            Say(game.Log, run.Tally.Line(id, Safe(game, () => Alive(game, run))));
            Reset(null);
        }
        if (_pending is { } p && p.EventId == id)
        {
            _pending = null;
            Say(game.Log, $"apocalypse {id} not resumed: {path.ToString().ToLowerInvariant()} in the drain wait");
        }
        if (EndRule.RemovesRecord(path) && state.Document.Apocalypse is { } record && record.EventId == id)
        {
            state.Document.Apocalypse = null;
            state.MarkDirty();
        }
    }

    /// <summary>`.nyar purge confirm` (D9): whatever runs or waits to resume goes, and so does the record.</summary>
    public void Purged(IApocalypseGame game, StateStore state)
    {
        if (_run is { } run) Ended(run.Id, ApocalypseEnd.Purge, game, state);
        if (_pending is { } p) Ended(p.EventId, ApocalypseEnd.Purge, game, state);
        if (state.Document.Apocalypse is not null)
        {
            state.Document.Apocalypse = null;
            state.MarkDirty();
        }
    }

    /// <summary>`.nyar pillar &lt;p&gt; off` during a resume's drain wait (D9): a pending resume of that pillar is cancelled,
    /// its record removed and its slot freed; true when one was.</summary>
    public bool PillarOff(Pillar pillar, DefinitionSet set, EventEngine engine, IApocalypseGame game, StateStore state)
    {
        if (_pending is not { } p || set.Find(p.EventId) is not { } def || def.Pillar != pillar) return false;
        if (engine.ResumePending == p.EventId) engine.ResumePending = null;
        Ended(p.EventId, ApocalypseEnd.PillarOff, game, state);
        return true;
    }

    // ---- the resume (D12, D21 resume-read)

    /// <summary>At boot, after every other instance was cancelled: the record (<paramref name="read"/>) resumes when
    /// ResumeRule.Verdict says so, holding the one-apocalypse slot behind the drain gate; any other verdict logs
    /// "apocalypse &lt;id&gt; not resumed: &lt;reason&gt;" (an EndsUtc past also "event &lt;id&gt; ended: survived …") and
    /// removes the record. A throwing read logs "not resumed: resume read failed: …" and returns, so every other boot step
    /// runs. Returns the id that waits to resume, or null.</summary>
    public string? Boot(Func<ApocalypseRecord?> read, Func<DefinitionSet> set, EventEngine engine, IApocalypseGame game, StateStore state, DateTime now)
    {
        Reset(null);
        _pending = null;
        _drainWarned = false;
        string? id = null;
        try
        {
            var record = read();
            if (record is null) { _resume.Ok(); return null; }
            id = record.EventId;
            var defs = set();
            var verdict = ResumeRule.Verdict(record, defs, now);
            _resume.Ok();
            if (verdict != ResumeVerdict.Resume)
            {
                Say(game.Log, ResumeRule.NotResumedLine(id, verdict));
                if (verdict == ResumeVerdict.EndedWhileDown)
                    Say(game.Log, ApocalypseOutcome.Line(id, ApocalypseResult.Survived, record.HordesRisen, defs.Find(id)?.Apocalypse?.Hordes.Count ?? record.HordesRisen));
                state.Document.Apocalypse = null;
                state.MarkDirty();
                return null;
            }
            _pending = record;
            _sweptUtc = now;
            engine.ResumePending = id;
            return id;
        }
        catch (Exception e)
        {
            if (_resume.Fail()) Say(game.Warn, $"apocalypse {id ?? "-"} not resumed: resume read failed: {e.Message}");
            _pending = null;
            if (id is not null && engine.ResumePending == id) engine.ResumePending = null;
            try
            {
                state.Document.Apocalypse = null;
                state.MarkDirty();
            }
            catch { /* the record goes with the next write */ }
            return null;
        }
    }

    /// <summary>Each tick while a resume waits (D12): the drain gate; once it proceeds (with its one warning after 5 min),
    /// the engine resumes the instance under the live <paramref name="controls"/> (a switch turned off while down refuses
    /// it, logged, the record removed), the scoreboard comes back, the units are rebuilt through the ledger, missed hordes
    /// rise once each and the roamers are topped up once. Returns the resumed instance, or null.</summary>
    public ActiveEvent? Resume(EventEngine engine, ControlState controls, IApocalypseGame game, StateStore state, Scoreboard board, DateTime now)
    {
        if (_pending is not { } p) return null;
        var step = ResumeRule.Drain(_sweptUtc, now, Safe(game, () => game.LeftToDrain), cancelled: engine.ResumePending != p.EventId);
        if (step == DrainStep.Wait) return null;
        _pending = null;
        if (step == DrainStep.Cancelled)
        {
            Say(game.Log, $"apocalypse {p.EventId} not resumed: cancelled in the drain wait");
            RemoveRecord(state, p.EventId);
            return null;
        }
        if (step == DrainStep.ProceedWithWarning && !_drainWarned)
        {
            _drainWarned = true;
            Say(game.Warn, ResumeRule.DrainWarning(p.EventId, Safe(game, () => game.LeftToDrain)));
        }
        try
        {
            var refused = engine.Resume(p.EventId, p.StartedUtc, p.EndsUtc, (p.Goal!.X, p.Goal.Y, p.Goal.Z), p.HordesRisen, controls);
            if (refused is not null)
            {
                Say(game.Log, $"apocalypse {p.EventId} not resumed: {refused.Human}");
                RemoveRecord(state, p.EventId);
                return null;
            }
            var active = engine.Find(p.EventId)!;
            board.Restore(p.EventId, p.Scores.Select(s => new ApocalypseScore(s.PlatformId, s.Kills, s.Deaths, s.First)));
            var run = ApocalypseRun.Resumed(active, p, now);
            Reset(run);
            foreach (var k in Enumerable.Range(1, p.HordesRisen).Where(k => p.Hordes.All(h => h.Number != k))) _defeated.Add(k);   // destroyed before the restart
            Safe(game, () => { game.MapTerritory(p.EventId); return 0; });
            var (rebuilt, rises, topUp) = run.Rebuild(game, p, now);
            Say(game.Log, ResumeRule.ResumedLine(p.EventId, p.HordesRisen, run.Action.Hordes.Count, rebuilt));
            foreach (var r in rises)
            {
                engine.WaveSpawned(p.EventId, now, r.Queued);
                Say(game.Log, string.Create(CultureInfo.InvariantCulture, $"apocalypse {p.EventId} horde {r.Horde}/{run.Action.Hordes.Count} rose at hotspot {r.Hotspot}: {r.Queued} units (missed while down)"));
                var spot = run.Hotspots[r.Hotspot - 1];
                game.Announce(p.EventId, Messages.ApocalypseRise(active.Definition.Name, r.Queued, Safe(game, () => game.RegionName(spot.X, spot.Z)), run.Action.Goal.Name));
            }
            foreach (var line in topUp) Say(game.Log, line);
            WriteRecord(game, state, board, now);
            return active;
        }
        catch (Exception e)
        {
            if (_resume.Fail()) Say(game.Warn, $"apocalypse {p.EventId} not resumed: resume read failed: {e.Message}");
            if (engine.Find(p.EventId) is not null) engine.Cancel(p.EventId);
            // the rebuild's, catch-up's and top-up's orders go with it, and any unit already spawned (code review M2)
            Safe(game, () => game.Ledger.EndEvent(p.EventId, DateTime.MaxValue));
            // and the restored scoreboard rows, so a later run of the same id starts from zero (code review L2)
            board.End(p.EventId);
            // and its spawn state beyond the units, as every other end path drops it (hunt seeds, territory map, streaks, watch rows)
            Safe(game, () => { game.ResumeCancelled(p.EventId); return 0; });
            Reset(null);
            RemoveRecord(state, p.EventId);
            return null;
        }
    }

    static void RemoveRecord(StateStore state, string id)
    {
        if (state.Document.Apocalypse is { } r && r.EventId == id)
        {
            state.Document.Apocalypse = null;
            state.MarkDirty();
        }
    }

    // ---- what admins and players see (D17)

    /// <summary>`.nyar status`'s line for the running apocalypse (no position), or null for any other instance. Matched by
    /// event id, so a resumed run (its instance rebuilt by Engine.Resume) keeps its line (code review M4).</summary>
    public string? StatusLine(RunningInstance instance, IApocalypseWorld w, DateTime now)
    {
        if (_run is not { } run || run.Id != instance.Definition.Id) return null;
        var view = run.View(w, (_, _) => "");
        var minutes = instance.EndsUtc > now ? (int)Math.Ceiling((instance.EndsUtc - now).TotalMinutes) : 0;
        return Messages.ApocalypseStatus(instance.Definition.Name, view.Risen, run.Action.Hordes.Count, view.Percent, run.Action.Goal.Name, view.Roamers, minutes);
    }

    /// <summary>`event info`'s run view of <paramref name="id"/> (admins only), or null when it is not the running one.</summary>
    public ApocalypseView? View(string id, IApocalypseGame game) =>
        _run is { } run && run.Id == id ? run.View(game, (x, z) => Safe(game, () => game.RegionName(x, z)) ?? Messages.Wilds) : null;

    // ---- guards

    static void Say(Action<string> sink, string line)
    {
        try { sink(line); }
        catch { /* a log line never breaks the run */ }
    }

    /// <summary>A read outside the five categories (a ledger count, a region name): its default when it throws.</summary>
    static T? Safe<T>(IApocalypseGame game, Func<T> read)
    {
        try { return read(); }
        catch { return default; }
    }

    static void Guard(FailureStreak streak, IApocalypseGame game, string what, Action work)
    {
        try { work(); streak.Ok(); }
        catch (Exception e) { if (streak.Fail()) Say(game.Warn, $"apocalypse: {what} skipped: {e.Message}"); }
    }
}

/// <summary>How a tracked unit left the game (D15's buckets).</summary>
public enum UnitDeparture { Died, Despawned, Removed }

/// <summary>
/// The guard of Services/ApocalypseAction's entry points other than the tick (step 3 code review M1, L5): a throw that
/// escapes the host is logged "apocalypse: &lt;entry&gt; failed: &lt;message&gt;" once per streak of that entry (a success
/// of the same entry ends it), never every second, and the entry returns its fallback. The tick is not guarded here: its
/// throw reaches EventRuntime's fault guard (<see cref="FaultGuard"/>), so D9's fault cancel counts it.
/// </summary>
public sealed class EntryGuard(Action<string> error)
{
    readonly Dictionary<string, FailureStreak> _streaks = new(StringComparer.Ordinal);

    FailureStreak Streak(string entry)
    {
        if (!_streaks.TryGetValue(entry, out var s)) _streaks[entry] = s = new FailureStreak();
        return s;
    }

    void Say(string line)
    {
        try { error(line); }
        catch { /* a log line never breaks an entry */ }
    }

    /// <summary><paramref name="work"/>'s result, or <paramref name="fallback"/> when it throws (logged once per streak).</summary>
    public T? Run<T>(string entry, Func<T> work, T? fallback = default)
    {
        var streak = Streak(entry);
        try
        {
            var result = work();
            streak.Ok();
            return result;
        }
        catch (Exception e)
        {
            if (streak.Fail()) Say($"apocalypse: {entry} failed: {e.Message}");
            return fallback;
        }
    }

    /// <summary>"no hotspot: prepare failed: &lt;message&gt;" (L5).</summary>
    public static string PrepareFailed(string message) => $"no hotspot: prepare failed: {message}";

    /// <summary>Engine.Start's apocalypse gate (L5): <paramref name="work"/>'s verdict, or, when it throws, a refusal of the
    /// start (<see cref="PrepareFailed"/>, reason read, so the start log does not repeat it) logged once per streak; a throw
    /// never lets the start through without its run.</summary>
    public Outcome? Prepare(string id, Func<Outcome?> work)
    {
        var streak = Streak("prepare");
        try
        {
            var verdict = work();
            streak.Ok();
            return verdict;
        }
        catch (Exception e)
        {
            var human = PrepareFailed(e.Message);
            if (streak.Fail()) Say($"apocalypse {id}: {human}");
            return Outcome.Refused(human, RefusalCode.State, "id", reason: Reasons.Read);
        }
    }
}

/// <summary>An event's tick inside its fault guard (foundation D25; apocalypse D9's fault cancel, step 3 code review M1):
/// a throw counts a fault and logs "event &lt;id&gt; tick failed (&lt;n&gt;/&lt;limit&gt;): &lt;message&gt;"; the
/// <see cref="EventEngine.FaultLimit"/>-th fault in a row calls <c>cancel</c>; a clean tick ends the streak.</summary>
public static class FaultGuard
{
    /// <summary>True when this tick's fault cancelled the event.</summary>
    public static bool Run(EventEngine engine, ActiveEvent active, Action work, Action<string> error, Action cancel)
    {
        try
        {
            work();
            engine.Healthy(active.Id);
            return false;
        }
        catch (Exception ex)
        {
            var limit = engine.Fault(active.Id);
            error($"event {active.Id} tick failed ({active.Faults}/{EventEngine.FaultLimit}): {ex.Message}");
            if (!limit) return false;
            cancel();
            return true;
        }
    }
}
