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
/// totals only. <see cref="Winner"/> is the best kill-to-death ratio over every row (swarm D5), null when no row holds a
/// kill; only a `winner: KillDeath` event shows it.</summary>
public sealed record ScoreSummary(IReadOnlyList<ScoreRow> Top, int Players, int Kills, int Deaths, ScoreRow? Winner = null);

/// <summary>
/// The per-event scoreboard (wave-sets D8, D9; design §9 D38, D39). In memory only: rows end with the event (every end
/// path calls <see cref="End"/>, a restart starts a new one) and nothing reaches state.json, a log line, a push or the
/// wire (D12), but for an apocalypse's rows, whose platform ids and counts (no name) ride in its resume record
/// (<see cref="Export"/>, <see cref="Restore"/>; apocalypse D10, D11). A kill is an event unit killed by a player, the player's familiar or summon credited to the player (the
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

    /// <summary>The running instance keeps a scoreboard: its own definition's SpawnWaves or Apocalypse action has
    /// `scoreboard: true` (an Apocalypse's absent key is true, apocalypse D1, D10).</summary>
    public static bool Counts(ActiveEvent a) => a.Definition.Action is { Scoreboard: true } || a.Definition.Apocalypse is { Scoreboard: true };

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
        return new ScoreSummary(ranked, score.Rows.Count, score.Kills, score.Deaths, Winner(score.Rows.Values));
    }

    /// <summary>The winner of <paramref name="rows"/> (swarm D5, S-6), chosen over every row, not only the top 3: among the
    /// rows with at least one kill, the highest kills ÷ max(1, deaths), then more kills, then fewer deaths, then the earlier
    /// first credit; null when no row holds a kill. A dropped admin (wave-sets D9) and a player past the 200-row cap have
    /// no row, so neither can win. The ratios are compared as exact cross products, never as floating point.</summary>
    public static ScoreRow? Winner(IEnumerable<ScoreRow> rows)
    {
        ScoreRow? best = null;
        foreach (var r in rows)
            if (r.Kills > 0 && (best is null || Better(r, best))) best = r;
        return best;
    }

    static bool Better(ScoreRow a, ScoreRow b)
    {
        var ratio = ((long)a.Kills * Math.Max(1, b.Deaths)).CompareTo((long)b.Kills * Math.Max(1, a.Deaths));
        if (ratio != 0) return ratio > 0;
        if (a.Kills != b.Kills) return a.Kills > b.Kills;
        if (a.Deaths != b.Deaths) return a.Deaths < b.Deaths;
        return a.First < b.First;
    }

    /// <summary>The ratio a winner line and the log show: kills ÷ max(1, deaths).</summary>
    public static double Ratio(ScoreRow r) => (double)r.Kills / Math.Max(1, r.Deaths);

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

    /// <summary>The rows of <paramref name="eventId"/> as an apocalypse's record keeps them (apocalypse D10, D11; design §9
    /// D45): platform id, kills, deaths and first credit, never a name; ordered by first credit.</summary>
    public IReadOnlyList<ApocalypseScore> Export(string eventId) =>
        _byEvent.TryGetValue(eventId, out var score)
            ? score.Rows.OrderBy(kv => kv.Value.First).Select(kv => new ApocalypseScore(kv.Key, kv.Value.Kills, kv.Value.Deaths, kv.Value.First)).ToList()
            : [];

    /// <summary>The rows of a resumed apocalypse (apocalypse D12): each row back under its platform id with its counts and
    /// first credit and no name (read at show time, <see cref="Names"/>); the totals are the rows' sums. At most
    /// <see cref="Limits.ScoreboardPlayers"/> rows; a later first credit numbers after every restored one.</summary>
    public void Restore(string eventId, IEnumerable<ApocalypseScore> rows)
    {
        var score = new EventScore();
        foreach (var r in rows.OrderBy(r => r.First))
        {
            if (score.Rows.Count >= Limits.ScoreboardPlayers || string.IsNullOrEmpty(r.PlatformId) || score.Rows.ContainsKey(r.PlatformId)) continue;
            var kills = Math.Max(0, r.Kills);
            var deaths = Math.Max(0, r.Deaths);
            score.Rows[r.PlatformId] = new ScoreRow("", kills, deaths, r.First);
            score.Kills += kills;
            score.Deaths += deaths;
            _sequence = Math.Max(_sequence, r.First);
        }
        _byEvent[eventId] = score;
    }

    /// <summary>Fills the empty names of <paramref name="eventId"/>'s restored rows from <paramref name="nameOf"/> (platform
    /// id → the character's name, null when unknown) at show time (design §9 D45); a row whose name stays unknown shows
    /// "a vampire".</summary>
    public void Names(string eventId, Func<string, string?> nameOf)
    {
        if (!_byEvent.TryGetValue(eventId, out var score)) return;
        foreach (var (id, row) in score.Rows.Where(kv => kv.Value.Name.Length == 0).ToList())
            score.Rows[id] = row with { Name = nameOf(id) is { Length: > 0 } n ? n : UnknownName };
    }

    /// <summary>The name a restored row shows when the game no longer knows its player.</summary>
    public const string UnknownName = "a vampire";
}

/// <summary>One scoreboard row in an apocalypse's record (apocalypse D11, D19; design §9 D45): the platform id it is
/// counted under, kills, deaths and the order of the first credit; no name and no position.</summary>
public sealed record ApocalypseScore(string PlatformId, int Kills, int Deaths, long First);

/// <summary>How a running instance ended (wave-sets D11): its natural end at its duration, all waves defeated, an admin's
/// `.nyar event stop`, the purge, a restart, its pillar switched off, the fault limit, or an apocalypse's goal fallen
/// (<see cref="Defeat"/>, apocalypse D8: lost). An apocalypse's won is Victory and its survived Natural.</summary>
public enum EndPath { Natural, Victory, Stop, Purge, Restart, PillarOff, Fault, Defeat }

/// <summary>When the scoreboard is shown (wave-sets D11; design §9 D41, S-6, S-7): at the natural end, all waves
/// defeated and an admin stop of an event with `scoreboard: true`, whatever EventBanners says, and an apocalypse's lost
/// (apocalypse D10); never at a purge, a restart, a pillar switched off or a fault cancel.</summary>
public static class ScoreboardRule
{
    public static bool Shows(EndPath path, ActiveEvent ended) =>
        Scoreboard.Counts(ended) && path is EndPath.Natural or EndPath.Victory or EndPath.Stop or EndPath.Defeat;

    /// <summary>The end of <paramref name="ended"/> by <paramref name="path"/> (D11): its rows end on every path; when
    /// <see cref="Shows"/>, the chat lines to queue as Info lines and the log line, else none. The winner rule is the
    /// running instance's own definition's (swarm D7), so an edit mid-run changes nothing until the next start.</summary>
    public static ScoreboardEnd End(EndPath path, ActiveEvent ended, Scoreboard board)
    {
        var summary = board.End(ended.Id);
        var winner = ended.Definition.Action?.Winner ?? WinnerRule.None;
        return Shows(path, ended)
            ? new ScoreboardEnd(Messages.ScoreboardLines(ended.Definition.Name, summary, winner), LogLine(ended.Id, summary, winner))
            : ScoreboardEnd.None;
    }

    /// <summary>"event &lt;id&gt; scoreboard: &lt;p&gt; players, &lt;K&gt; kills, &lt;D&gt; deaths": counts only, never a name (D12);
    /// for a `winner: KillDeath` event ", winner K/D &lt;r&gt;" or ", no winner" follows (swarm D7), the ratio only.</summary>
    public static string LogLine(string eventId, ScoreSummary s, WinnerRule winner = WinnerRule.None) =>
        $"event {eventId} scoreboard: {s.Players} players, {s.Kills} kills, {s.Deaths} deaths" + (winner != WinnerRule.KillDeath ? ""
            : s.Winner is { } w ? FormattableString.Invariant($", winner K/D {Scoreboard.Ratio(w):0.00}") : ", no winner");

    /// <summary>"event &lt;id&gt; ended: all waves defeated (&lt;s&gt; of &lt;n&gt; waves)" (wave-sets D7).</summary>
    public static string VictoryLine(ActiveEvent ended) =>
        $"event {ended.Id} ended: all waves defeated ({ended.WavesSpawned} of {ended.Definition.Action?.Waves ?? 0} waves)";
}

/// <summary>What an end shows (wave-sets D11): the scoreboard's chat lines and its log line, or nothing.</summary>
public sealed record ScoreboardEnd(IReadOnlyList<string> Chat, string? Log)
{
    public static readonly ScoreboardEnd None = new([], null);
}
