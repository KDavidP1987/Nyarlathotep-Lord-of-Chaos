#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The Revenge trigger's pure half (docs/dod/revenge.md D3-D9, D13): per-player heat from kills of listed factions, the
// private warnings, the due players and their 5 s gate, the cooldowns and the end paths. Logic/PlayerTriggers.cs feeds it
// from the guarded kill read and the scan; Services/TriggerBus drives both on the server main thread. Every row is keyed
// on the platform id and lives in memory only (D9): no row, id or position reaches state.json, a log line, a reply, a push
// or the wire.

/// <summary>Why a due player's revenge did not start on a scan (revenge D5), logged once per change of reason.</summary>
public enum RevengeHold { Territory, Busy, TargetBusy, Ineligible, Controls }

/// <summary>How a revenge start attempt went (revenge D5): it started; the definition's conditions refused it (the heat
/// is reset, no cooldown); or something else refused it (the controls: the player stays due, the heat untouched).</summary>
public enum RevengeStartResult { Started, ConditionsRefused, Refused }

/// <summary>The six ways a running Revenge instance ends (revenge D8). The first three send the target the end line.</summary>
public enum RevengeEndPath { NaturalEnd, AllWavesDefeated, AdminStop, Purge, Restart, PillarOff }

/// <summary>A player as the revenge gate sees it (revenge D5): the platform id, the position, whether alive and whether
/// in PvP combat. A player missing from the read is offline.</summary>
public readonly record struct RevengeTarget(string PlayerId, float X, float Y, float Z, bool Alive, bool InPvpCombat);

/// <summary>What one counted kill did to a player's heat (revenge D3, D4, D5): the points added (0 during the cooldown),
/// the warning to send (k of n, 0 for none), and whether the player became due with it.</summary>
public readonly record struct HeatChange(int Points, int Warning, int Warnings, bool BecameDue);

/// <summary>The revenge rules that need no state (revenge D3-D8, D13).</summary>
public static class RevengeRule
{
    public static bool IsRevenge(EventDefinition definition) => definition.Trigger.Type == TriggerType.Revenge;

    /// <summary>The points a counted kill adds (D3): vbloodHeat for a V Blood kill, otherwise 1.</summary>
    public static int Points(KillFacts kill, Trigger trigger) => kill.VictimVBlood ? trigger.VBloodHeat : 1;

    /// <summary>{faction} of the revenge lines (D4): the short names of the trigger's factions joined by ", ".</summary>
    public static string FactionText(Trigger trigger) => string.Join(", ", (trigger.Factions ?? []).Select(FactionDenyList.ShortName));

    /// <summary>The server-wide alert of a start (D7): RevengeAlertPool's <paramref name="pick"/>-th line with {faction},
    /// within Wire.MaxBytes; never the target or a place.</summary>
    public static string Alert(EventDefinition definition, int pick)
    {
        var pool = Messages.RevengeAlertPool;
        var line = Messages.Render(pool[Math.Abs(pick % pool.Count)],
            new MessageContext("-", TextSink.Clean(FactionText(definition.Trigger)), 0, 0, 0, "-"));
        return TextSink.CutToBytes(line, Wire.MaxBytes);
    }

    /// <summary>The server-wide lines a start queues (D7): for a Revenge definition the alert, as Info, whatever
    /// EventBanners says and never the start banner; for any other definition the start banner when
    /// <paramref name="eventBanners"/> is on, as before.</summary>
    public static IReadOnlyList<QueuedLine> StartLines(EventDefinition definition, bool eventBanners, int pick, Func<string> startBanner) =>
        IsRevenge(definition) ? [new QueuedLine(Alert(definition, pick), LineKind.Info, definition.Id)]
        : eventBanners ? [new QueuedLine(startBanner(), LineKind.Info, definition.Id)]
        : [];

    /// <summary>The private line of a revenge start (D7): to the instance's focus alone; none for any other instance.</summary>
    public static (string Player, string Line)? StartPrivate(ActiveEvent active) =>
        IsRevenge(active.Definition) && active.Focus is { Length: > 0 } focus
            ? (focus, Messages.RevengeStart(FactionText(active.Definition.Trigger))) : null;

    /// <summary>The private line of a revenge end (D8): to the focus alone on a natural end, all waves defeated or an admin
    /// stop; none on a purge, a restart or a pillar off, and none for any other instance.</summary>
    public static (string Player, string Line)? EndPrivate(ActiveEvent active, RevengeEndPath path) =>
        IsRevenge(active.Definition) && SendsEndLine(path) && active.Focus is { Length: > 0 } focus
            ? (focus, Messages.RevengeEnd(FactionText(active.Definition.Trigger))) : null;

    /// <summary>How a start attempt's refusal reads to the gate (D5): none started it, a conditions refusal resets the
    /// heat, anything else (the controls) holds the player due.</summary>
    public static RevengeStartResult Classify(Outcome? refusal) =>
        refusal is null ? RevengeStartResult.Started
        : refusal.Reason == Reasons.Condition ? RevengeStartResult.ConditionsRefused
        : RevengeStartResult.Refused;

    /// <summary>The focus of an admin's start (D13): the admin's own platform id for a Revenge definition, else none, as
    /// before. The start resets nobody's heat and sets no cooldown (Review-3 F4).</summary>
    public static string? AdminFocus(EventDefinition definition, string adminPlatformId) =>
        IsRevenge(definition) ? adminPlatformId : null;

    /// <summary>True when the end path sends the target RevengeEnd (D8): natural end, all waves defeated, admin stop.</summary>
    public static bool SendsEndLine(RevengeEndPath path) =>
        path is RevengeEndPath.NaturalEnd or RevengeEndPath.AllWavesDefeated or RevengeEndPath.AdminStop;

    public static string PathText(RevengeEndPath path) => path switch
    {
        RevengeEndPath.NaturalEnd => "natural end",
        RevengeEndPath.AllWavesDefeated => "all waves defeated",
        RevengeEndPath.AdminStop => "admin stop",
        RevengeEndPath.Purge => "purge",
        RevengeEndPath.Restart => "restart",
        _ => "pillar off",
    };

    public static string HoldText(RevengeHold hold) => hold switch
    {
        RevengeHold.Territory => "territory",
        RevengeHold.Busy => "busy",
        RevengeHold.TargetBusy => "target busy",
        RevengeHold.Ineligible => "ineligible",
        _ => "controls",
    };

    // The log lines (Failure & observability 12.2): the definition id and counts only, never a player.
    public static string WarningLine(string id, int k, int n) => $"revenge {id}: warning {k} of {n}";
    public static string VBloodLine(string id, int points) => $"revenge {id}: vblood kill +{points} heat";
    public static string DueLine(string id) => $"revenge {id} due";
    public static string HeldLine(string id, RevengeHold hold) => $"revenge {id}: held ({HoldText(hold)})";
    public static string EndedLine(string id, RevengeEndPath path) => $"revenge {id} ended: {PathText(path)}";

    /// <summary>The admin counts line of `.nyar event info` (D12): counts only.</summary>
    public static string CountsLine(int rows, int due, int running) => $"revenge: {rows} heat rows, {due} due, {running} running";
}

/// <summary>The heat of every startable Revenge definition (revenge D3, D4, D5, D9): per definition and player the points
/// of the counted kills within the last windowSeconds, the warning level reached in the current climb, the due players in
/// the order they became due, and the cooldowns. Heat rows are bounded at <see cref="TriggerLimits.CountersPerDefinition"/>
/// a definition and <see cref="TriggerLimits.CountersInAll"/> in all, a separate bound from FactionKills' KillWindows; the
/// least recently updated row that is not due goes first, and a due player's row is never dropped (Review-3 F3). Warning
/// levels and cooldowns live outside the rows, so an eviction never lifts a cooldown; cooldowns are bounded at
/// <see cref="TriggerLimits.CooldownRows"/>.</summary>
public sealed class RevengeHeat
{
    sealed class Row
    {
        public readonly Queue<(DateTime At, int Points)> Points = new();
        public int Sum;
        public DateTime Updated;
    }

    readonly Dictionary<string, Dictionary<string, Row>> _rows = new(StringComparer.Ordinal);
    readonly Dictionary<(string Definition, string Player), int> _warned = new();
    readonly Dictionary<(string Definition, string Player), DateTime> _cooldowns = new();
    readonly List<(string Definition, string Player)> _due = [];
    readonly Dictionary<(string Definition, string Player), RevengeHold> _held = new();

    public int Rows => _rows.Values.Sum(d => d.Count);
    public int RowsFor(string definitionId) => _rows.TryGetValue(definitionId, out var d) ? d.Count : 0;
    public int Due => _due.Count;
    public int DueFor(string definitionId) => _due.Count(d => d.Definition == definitionId);
    public int Cooldowns => _cooldowns.Count;
    public int HeatOf(string definitionId, string player) =>
        _rows.TryGetValue(definitionId, out var d) && d.TryGetValue(player, out var r) ? r.Sum : 0;
    public bool IsDue(string definitionId, string player) => _due.Contains((definitionId, player));
    public bool InCooldown(string definitionId, string player, DateTime utcNow) =>
        _cooldowns.TryGetValue((definitionId, player), out var until) && utcNow < until;

    /// <summary>One counted kill of <paramref name="definition"/> for <paramref name="player"/> worth
    /// <paramref name="points"/> (D3). Nothing is added during the player's cooldown or for 0 points. The warning is the
    /// highest threshold the heat now reaches when it passed one not yet sent in this climb (D4); the player becomes due at
    /// the definition's heat (D5).</summary>
    public HeatChange Add(EventDefinition definition, string player, int points, DateTime utcNow)
    {
        var t = definition.Trigger;
        var key = (definition.Id, player);
        var warnAt = t.WarnAt ?? [];
        if (points <= 0 || InCooldown(definition.Id, player, utcNow)) return new HeatChange(0, 0, warnAt.Count, false);
        if (!_rows.TryGetValue(definition.Id, out var rows))
            _rows[definition.Id] = rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        if (!rows.TryGetValue(player, out var row)) rows[player] = row = new Row();
        Expire(row, t, utcNow);
        row.Points.Enqueue((utcNow, points));
        row.Sum += points;
        row.Updated = utcNow;
        var warning = Rearm(key, warnAt, row.Sum, climb: true);
        var becameDue = false;
        if (row.Sum >= t.Heat && !_due.Contains(key))
        {
            _due.Add(key);
            becameDue = true;
        }
        Bound(definition.Id, player);
        return new HeatChange(points, warning, warnAt.Count, becameDue && IsDue(definition.Id, player));
    }

    /// <summary>Keeps the rows of <paramref name="startable"/> only (D3: a definition that leaves the startable set, by a
    /// disable, a reload, an invalid edit or its pillar off, loses its rows, warning levels and due entries; its cooldowns
    /// keep their own expiry). Each kept row loses the points that left the window, re-arms the warnings it fell below, and
    /// is due exactly while its heat is at or above the current trigger's heat, so an edit that lowers `heat` makes a hot
    /// player due without a new kill (Review-3 F6); an empty row goes.</summary>
    public void Prune(IReadOnlyList<EventDefinition> startable, DateTime utcNow)
    {
        var byId = startable.Where(RevengeRule.IsRevenge).ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var id in _rows.Keys.Where(id => !byId.ContainsKey(id)).ToList()) _rows.Remove(id);
        foreach (var (id, rows) in _rows.ToList())
        {
            var t = byId[id].Trigger;
            foreach (var (player, row) in rows.ToList())
            {
                Expire(row, t, utcNow);
                Rearm((id, player), t.WarnAt ?? [], row.Sum, climb: false);
                var due = _due.Contains((id, player));
                if (row.Sum >= t.Heat && !due) _due.Add((id, player));
                else if (row.Sum < t.Heat && due) Forget((id, player));
                if (row.Sum == 0) rows.Remove(player);
            }
            if (rows.Count == 0) _rows.Remove(id);
        }
        foreach (var key in _due.Where(k => HeatOf(k.Definition, k.Player) == 0).ToList()) Forget(key);
        foreach (var key in _warned.Keys.Where(k => HeatOf(k.Definition, k.Player) == 0).ToList()) _warned.Remove(key);
        foreach (var key in _cooldowns.Keys.Where(k => utcNow >= _cooldowns[k]).ToList()) _cooldowns.Remove(key);
    }

    /// <summary>One pass of the 5 s scan over the due players in the order they became due (D5). Each is held, with one
    /// line per change of reason, while it is offline, dead, at an unusable position, in PvP combat, outside a regional
    /// action scope or with the territory map down ("ineligible"), in claimed territory ("territory"), while the definition
    /// runs ("busy"), while another running revenge targets it ("target busy"), or while the controls refuse the start
    /// ("controls", the heat untouched). Otherwise <paramref name="start"/> is attempted, focused on that player: a start
    /// resets the heat and starts the cooldown; a conditions refusal resets the heat only. The players are read once, and
    /// only when someone is due.</summary>
    public void Scan(IReadOnlyList<EventDefinition> startable, Func<IReadOnlyList<RevengeTarget>> read, Func<float, float, bool>? isClaimed,
        Func<float, float, string>? regionOf, Func<string, bool> isActive, Func<string, bool> targetBusy, DateTime utcNow,
        Action<string> log, Func<PlayerFire, RevengeStartResult> start)
    {
        Prune(startable, utcNow);
        if (_due.Count == 0) return;
        var byId = startable.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var players = new Dictionary<string, RevengeTarget>(StringComparer.Ordinal);
        foreach (var p in read())
            if (!Phantoms.IsPhantom(p.PlayerId)) players.TryAdd(p.PlayerId, p);
        var startedDefinitions = new HashSet<string>(StringComparer.Ordinal);
        var startedTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in _due.ToList())
        {
            var def = byId[key.Definition];
            var hold = Hold(def, players.TryGetValue(key.Player, out var p) ? p : (RevengeTarget?)null, isClaimed, regionOf);
            if (hold is null && (startedDefinitions.Contains(def.Id) || isActive(def.Id))) hold = RevengeHold.Busy;
            if (hold is null && (startedTargets.Contains(key.Player) || targetBusy(key.Player))) hold = RevengeHold.TargetBusy;
            if (hold is null)
            {
                switch (start(new PlayerFire(def, nameof(TriggerType.Revenge), key.Player, (p.X, p.Z))))
                {
                    case RevengeStartResult.Started:
                        Reset(key);
                        if (def.Trigger.PlayerCooldownMinutes > 0) Cool(key, utcNow.AddMinutes(def.Trigger.PlayerCooldownMinutes));
                        startedDefinitions.Add(def.Id);
                        startedTargets.Add(key.Player);
                        continue;
                    case RevengeStartResult.ConditionsRefused:
                        Reset(key);
                        continue;
                    default:
                        hold = RevengeHold.Controls;
                        break;
                }
            }
            if (_held.TryGetValue(key, out var before) && before == hold) continue;
            _held[key] = hold.Value;
            log(RevengeRule.HeldLine(def.Id, hold.Value));
        }
    }

    /// <summary>The player's own reason to hold (D5), or null when eligible: PlayerPick's rule, the territory first.</summary>
    static RevengeHold? Hold(EventDefinition def, RevengeTarget? player, Func<float, float, bool>? isClaimed, Func<float, float, string>? regionOf)
    {
        if (player is not { } p || !p.Alive || p.InPvpCombat || !PlayerPosition.Usable(p.X, p.Y, p.Z) || isClaimed is null)
            return RevengeHold.Ineligible;
        if (isClaimed(p.X, p.Z)) return RevengeHold.Territory;
        var scope = def.Action?.Scope ?? Scope.Global;
        if (!scope.IsGlobal && (regionOf is null || !scope.Names(regionOf(p.X, p.Z)))) return RevengeHold.Ineligible;
        return null;
    }

    /// <summary>A purge or a boot (D8, D9): every heat row, warning level, due player and cooldown goes.</summary>
    public void Clear()
    {
        _rows.Clear();
        _warned.Clear();
        _cooldowns.Clear();
        _due.Clear();
        _held.Clear();
    }

    static void Expire(Row row, Trigger t, DateTime utcNow)
    {
        while (row.Points.Count > 0 && utcNow - row.Points.Peek().At >= TimeSpan.FromSeconds(t.WindowSeconds))
            row.Sum -= row.Points.Dequeue().Points;
    }

    /// <summary>The warning level of <paramref name="heat"/> is how many thresholds it reaches. A climb past the level
    /// already sent returns the new level (one line, the highest); a fall below re-arms the thresholds above.</summary>
    int Rearm((string, string) key, IReadOnlyList<int> warnAt, int heat, bool climb)
    {
        var level = warnAt.Count(w => heat >= w);
        var sent = _warned.TryGetValue(key, out var s) ? s : 0;
        if (level < sent || level > sent && climb)
        {
            if (level == 0) _warned.Remove(key);
            else _warned[key] = level;
        }
        return climb && level > sent ? level : 0;
    }

    void Reset((string Definition, string Player) key)
    {
        if (_rows.TryGetValue(key.Definition, out var rows))
        {
            rows.Remove(key.Player);
            if (rows.Count == 0) _rows.Remove(key.Definition);
        }
        _warned.Remove(key);
        Forget(key);
    }

    void Forget((string, string) key)
    {
        _due.Remove(key);
        _held.Remove(key);
    }

    void Cool((string, string) key, DateTime until)
    {
        _cooldowns[key] = until;
        while (_cooldowns.Count > TriggerLimits.CooldownRows)
            _cooldowns.Remove(_cooldowns.Where(c => !c.Key.Equals(key)).MinBy(c => c.Value).Key);
    }

    /// <summary>The bounds of D3 after a row of <paramref name="definitionId"/> and <paramref name="player"/> was updated:
    /// the least recently updated row that is not due and not this one goes; with none, this row itself goes.</summary>
    void Bound(string definitionId, string player)
    {
        var rows = _rows[definitionId];
        while (rows.Count > TriggerLimits.CountersPerDefinition)
            Drop(rows.Where(r => r.Key != player && !_due.Contains((definitionId, r.Key)))
                .Select(r => (Def: definitionId, Player: r.Key, r.Value.Updated)).ToList(), definitionId, player);
        while (Rows > TriggerLimits.CountersInAll)
            Drop(_rows.SelectMany(d => d.Value.Where(r => (d.Key != definitionId || r.Key != player) && !_due.Contains((d.Key, r.Key)))
                .Select(r => (Def: d.Key, Player: r.Key, r.Value.Updated))).ToList(), definitionId, player);
    }

    void Drop(List<(string Def, string Player, DateTime Updated)> candidates, string definitionId, string player)
    {
        var (def, who, _) = candidates.Count > 0 ? candidates.MinBy(c => c.Updated) : (definitionId, player, default);
        Reset((def, who));
    }
}
