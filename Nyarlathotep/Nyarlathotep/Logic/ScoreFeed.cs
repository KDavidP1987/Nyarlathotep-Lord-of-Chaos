#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>A unit of ours as the ledger knows it (SpawnLedger.EventOf): its event, wave and spawn time, which tells the
/// running instance's units from an earlier instance's (A3).</summary>
public readonly record struct OurUnit(string EventId, int Wave, DateTime SpawnedUtc);

/// <summary>What the ledger says about one death, read before SpawnTracker.Died forgets the victim (wave-sets D13): the
/// victim as one of our units, the killer as one, and the unit the killer's EntityOwner names (the unit behind a
/// projectile or a summon).</summary>
public readonly record struct ScoreSides(OurUnit? Victim, OurUnit? KillerUnit, OurUnit? KillerOwnerUnit = null)
{
    /// <summary>The killing unit, resolved through its EntityOwner as the kill side is (D8).</summary>
    public OurUnit? Killing => KillerUnit ?? KillerOwnerUnit;
}

/// <summary>The players of one death, read after SpawnTracker.Died only when <see cref="ScoreRule.Relevant"/>: the killer
/// as a player, its EntityOwner as one (a familiar's), the player that owner follows (a familiar's summon), the victim
/// as a player, and whether the unit killed itself.</summary>
public readonly record struct ScorePlayers(Scorer? Killer, Scorer? VictimPlayer, bool SelfKill, Scorer? Owner = null, Scorer? Followed = null)
{
    /// <summary>The credited player by KillReader's rule (D8): the killer, else its owner, else the player the owner follows.</summary>
    public Scorer? Credited => Killer ?? Owner ?? Followed;
}

/// <summary>Which credit one death gives (wave-sets D8; design §9 D39). A unit counts for a running instance whose own
/// definition has `scoreboard: true` (<see cref="Scoreboard.Counts"/>) only when it spawned since that instance started
/// (A3).</summary>
public static class ScoreRule
{
    static ActiveEvent? Counting(OurUnit? unit, Func<string, ActiveEvent?> running) =>
        unit is { } u && running(u.EventId) is { } a && Scoreboard.Counts(a) && u.SpawnedUtc >= a.Instance.StartedUtc ? a : null;

    /// <summary>True when the death can credit anything: its victim or its killer is a counting instance's unit. Only
    /// then are the players read.</summary>
    public static bool Relevant(ScoreSides sides, Func<string, ActiveEvent?> running) =>
        Counting(sides.Victim, running) is not null || Counting(sides.Killing, running) is not null;

    /// <summary>Credits the death to <paramref name="board"/>: a kill when the victim is a counting instance's unit and the
    /// killer resolved to a player (a unit killing itself credits nothing); else a death when the victim is a player and
    /// the killer a counting instance's unit. Anything else (a native unit, another event's, an earlier instance's)
    /// credits nothing.</summary>
    public static void Credit(ScoreSides sides, ScorePlayers players, Func<string, ActiveEvent?> running, Scoreboard board, bool includeAdmins)
    {
        if (Counting(sides.Victim, running) is { } killed)
        {
            if (players.Credited is { } k && !players.SelfKill) board.Kill(killed.Id, k, includeAdmins);
            return;
        }
        if (players.VictimPlayer is { } v && Counting(sides.Killing, running) is { } by) board.Death(by.Id, v, includeAdmins);
    }
}

/// <summary>
/// The scoreboard's kill feed (wave-sets D13), driven by Patches/DeathEventPatch through <see cref="DeathPass"/>: per
/// death, <see cref="Before"/> reads the ledger before SpawnTracker.Died and <see cref="After"/> reads the players and
/// credits after it. Neither throws: a failing read skips that death's credit, logs "scoreboard: death skipped:
/// &lt;message&gt;" once per streak and holds <see cref="ReadFailing"/> on the health line until a read succeeds.
/// </summary>
public sealed class ScoreFeed
{
    public const string ReadFailing = "scoreboard: kill read failing";

    readonly FailureStreak _streak = new();

    /// <summary>The health entry while reads fail (HealthMonitor, `.nyar status`).</summary>
    public IReadOnlyList<string> Health => _streak.Count > 0 ? [ReadFailing] : [];

    /// <summary>The ledger read of one death, before SpawnTracker.Died; null when it failed.</summary>
    public ScoreSides? Before(Func<ScoreSides> read, Action<string> log)
    {
        try { return read(); }
        catch (Exception e) { Failed(e, log); return null; }
    }

    /// <summary>After SpawnTracker.Died: reads the players of a relevant death and credits it. A death no counting
    /// instance owns reads nothing and ends a streak, as a good read does.</summary>
    public void After(ScoreSides? sides, Func<ScoreSides, ScorePlayers> players, Func<string, ActiveEvent?> running, Scoreboard board,
        bool includeAdmins, Action<string> log)
    {
        if (sides is not { } s) return;                                          // its read failed: already logged
        try
        {
            if (ScoreRule.Relevant(s, running)) ScoreRule.Credit(s, players(s), running, board, includeAdmins);
            _streak.Ok();
        }
        catch (Exception e) { Failed(e, log); }
    }

    /// <summary>No counting instance runs: the streak and its entry end, nothing is read (D13).</summary>
    public void Idle() => _streak.Ok();

    void Failed(Exception e, Action<string> log)
    {
        if (_streak.Fail()) log($"scoreboard: death skipped: {e.Message}");
    }
}

/// <summary>The order of one frame's deaths (Patches/DeathEventPatch; Epic D8, automation D11, wave-sets D13): per death,
/// <c>before</c> (the ledger reads), SpawnTracker.Died, <c>rest</c> (the V Blood path and the kill feed) and
/// <c>after</c> (the scoreboard). Each step but <c>died</c> guards itself, so a throw in a read never skips Died for this
/// or a later death.</summary>
public static class DeathPass
{
    public static void Run<T, TRead>(IEnumerable<T> deaths, Func<T, TRead> before, Action<T> died, Action<T, TRead> rest, Action<T, TRead> after)
    {
        foreach (var death in deaths)
        {
            var read = before(death);
            died(death);
            rest(death, read);
            after(death, read);
        }
    }
}

/// <summary>The cleared read of a waveList schedule (wave-sets D18 cleared-read): a throwing ledger read is taken as not
/// cleared, which holds the next wave and the early end, logs "event &lt;id&gt;: cleared read failed: &lt;message&gt;"
/// once per streak and holds <see cref="Failing"/> until a read succeeds.</summary>
public sealed class ClearedRead
{
    public const string Failing = "wave sets: cleared read failing";

    readonly FailureStreak _streak = new();

    public IReadOnlyList<string> Health => _streak.Count > 0 ? [Failing] : [];

    public Func<string, int, bool> Guard(Func<string, int, bool> read, Action<string> log) => (id, wave) =>
    {
        try
        {
            var cleared = read(id, wave);
            _streak.Ok();
            return cleared;
        }
        catch (Exception e)
        {
            if (_streak.Fail()) log($"event {id}: cleared read failed: {e.Message}");
            return false;
        }
    };
}
