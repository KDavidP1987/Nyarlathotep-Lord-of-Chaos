#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>A player as the scoreboard sees one credit (wave-sets D8, D9): the platform id it is counted under (memory
/// only, D12), the character name the chat line shows, and whether the player was an admin at the credit.</summary>
public readonly record struct Scorer(string PlatformId, string Name, bool IsAdmin);

/// <summary>One participant's row: kills of the event's units, deaths to them, and the order of the first credit, which
/// breaks a tie of kills and deaths (wave-sets D8).</summary>
public sealed record ScoreRow(string Name, int Kills, int Deaths, long First);

/// <summary>An event's scoreboard as shown (wave-sets D10, D11): the top participants in rank order and the totals.
/// <see cref="Players"/> counts the rows; a credit beyond <see cref="Limits.ScoreboardPlayers"/> players adds to the
/// totals only.</summary>
public sealed record ScoreSummary(IReadOnlyList<ScoreRow> Top, int Players, int Kills, int Deaths);

/// <summary>
/// The per-event scoreboard (wave-sets D8, D9; design §9 D38, D39). In memory only: rows end with the event (every end
/// path calls <see cref="End"/>, a restart starts a new one) and nothing reaches state.json, a log line, a push or the
/// wire (D12). A kill is an event unit killed by a player, the player's familiar or summon credited to the player (the
/// caller resolves the owner, as KillReader does); a death is a player killed by one of the event's units. An admin's
/// credit is dropped, row and totals, unless <c>includeAdmins</c>. At most <see cref="Limits.ScoreboardPlayers"/> rows per
/// event. Every caller runs on the server main thread.
/// </summary>
public sealed class Scoreboard
{
    sealed class EventScore
    {
        public readonly Dictionary<string, ScoreRow> Rows = new(StringComparer.Ordinal);
        public int Kills;
        public int Deaths;
    }

    readonly Dictionary<string, EventScore> _byEvent = new(StringComparer.Ordinal);
    long _sequence;

    /// <summary>True while a running instance whose own definition has `scoreboard: true` runs (wave-sets D13, review F3):
    /// read from the running instances, never the catalog, so an edit mid-run changes nothing until the next start.</summary>
    public static bool Wants(IEnumerable<ActiveEvent> active)
    {
        foreach (var a in active) if (Counts(a)) return true;
        return false;
    }

    /// <summary>The running instance keeps a scoreboard: its own definition's SpawnWaves action has `scoreboard: true`.</summary>
    public static bool Counts(ActiveEvent a) => a.Definition.Action is { Scoreboard: true };

    /// <summary>A kill of one of <paramref name="eventId"/>'s units by <paramref name="player"/>.</summary>
    public void Kill(string eventId, Scorer player, bool includeAdmins) => Credit(eventId, player, includeAdmins, kill: true);

    /// <summary><paramref name="player"/> killed by one of <paramref name="eventId"/>'s units.</summary>
    public void Death(string eventId, Scorer player, bool includeAdmins) => Credit(eventId, player, includeAdmins, kill: false);

    void Credit(string eventId, Scorer player, bool includeAdmins, bool kill)
    {
        if (player.IsAdmin && !includeAdmins) return;                           // D9: neither a row nor the totals
        if (!_byEvent.TryGetValue(eventId, out var score)) _byEvent[eventId] = score = new EventScore();
        if (kill) score.Kills++; else score.Deaths++;
        if (score.Rows.TryGetValue(player.PlatformId, out var row))
            score.Rows[player.PlatformId] = row with { Name = player.Name, Kills = row.Kills + (kill ? 1 : 0), Deaths = row.Deaths + (kill ? 0 : 1) };
        else if (score.Rows.Count < Limits.ScoreboardPlayers)
            score.Rows[player.PlatformId] = new ScoreRow(player.Name, kill ? 1 : 0, kill ? 0 : 1, ++_sequence);
    }

    /// <summary>The summary of <paramref name="eventId"/> with its top <paramref name="top"/> rows: kills descending, then
    /// deaths ascending, then the earlier first credit (D39). An event with no credit has no participant.</summary>
    public ScoreSummary Summary(string eventId, int top = 3)
    {
        if (!_byEvent.TryGetValue(eventId, out var score)) return new ScoreSummary([], 0, 0, 0);
        var ranked = score.Rows.Values.OrderByDescending(r => r.Kills).ThenBy(r => r.Deaths).ThenBy(r => r.First).Take(top).ToList();
        return new ScoreSummary(ranked, score.Rows.Count, score.Kills, score.Deaths);
    }

    /// <summary>Ends <paramref name="eventId"/>'s rows and returns its summary (every end path, shown or not).</summary>
    public ScoreSummary End(string eventId, int top = 3)
    {
        var summary = Summary(eventId, top);
        _byEvent.Remove(eventId);
        return summary;
    }

    /// <summary>Drops the rows of every event not in <paramref name="live"/>, so no row outlives its instance.</summary>
    public void Keep(IReadOnlyCollection<string> live)
    {
        foreach (var id in _byEvent.Keys.Where(k => !live.Contains(k)).ToList()) _byEvent.Remove(id);
    }

    /// <summary>The purge and a restart: every row goes.</summary>
    public void Clear() => _byEvent.Clear();

    /// <summary>Events with at least one credit.</summary>
    public int Events => _byEvent.Count;
}

/// <summary>How a running instance ended (wave-sets D11): its natural end at its duration, all waves defeated, an admin's
/// `.nyar event stop`, the purge, a restart, its pillar switched off, or the fault limit.</summary>
public enum EndPath { Natural, Victory, Stop, Purge, Restart, PillarOff, Fault }

/// <summary>When the scoreboard is shown (wave-sets D11; design §9 D41, S-6, S-7): at the natural end, all waves
/// defeated and an admin stop of an event with `scoreboard: true`, whatever EventBanners says; never at a purge, a
/// restart, a pillar switched off or a fault cancel.</summary>
public static class ScoreboardRule
{
    public static bool Shows(EndPath path, ActiveEvent ended) =>
        Scoreboard.Counts(ended) && path is EndPath.Natural or EndPath.Victory or EndPath.Stop;

    /// <summary>The end of <paramref name="ended"/> by <paramref name="path"/> (D11): its rows end on every path; when
    /// <see cref="Shows"/>, the chat lines to queue as Info lines and the log line, else none.</summary>
    public static ScoreboardEnd End(EndPath path, ActiveEvent ended, Scoreboard board)
    {
        var summary = board.End(ended.Id);
        return Shows(path, ended)
            ? new ScoreboardEnd(Messages.ScoreboardLines(ended.Definition.Name, summary), LogLine(ended.Id, summary))
            : ScoreboardEnd.None;
    }

    /// <summary>"event &lt;id&gt; scoreboard: &lt;p&gt; players, &lt;K&gt; kills, &lt;D&gt; deaths": counts only, never a name (D12).</summary>
    public static string LogLine(string eventId, ScoreSummary s) =>
        $"event {eventId} scoreboard: {s.Players} players, {s.Kills} kills, {s.Deaths} deaths";

    /// <summary>"event &lt;id&gt; ended: all waves defeated (&lt;s&gt; of &lt;n&gt; waves)" (wave-sets D7).</summary>
    public static string VictoryLine(ActiveEvent ended) =>
        $"event {ended.Id} ended: all waves defeated ({ended.WavesSpawned} of {ended.Definition.Action?.Waves ?? 0} waves)";
}

/// <summary>What an end shows (wave-sets D11): the scoreboard's chat lines and its log line, or nothing.</summary>
public sealed record ScoreboardEnd(IReadOnlyList<string> Chat, string? Log)
{
    public static readonly ScoreboardEnd None = new([], null);
}
