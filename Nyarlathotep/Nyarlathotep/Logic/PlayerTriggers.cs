#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

// The player-action triggers' pure half (docs/dod/automation.md D9, D11, D13, D14, D29): which region entries and which
// kills fire a definition, and the gate every such trigger passes before a start. Services/TriggerBus drives it on the
// server main thread. Every row is keyed on the User's platform id, which survives a relog, and lives in memory only
// (D19): no row, id or position reaches state.json, a log line, a chat reply, a push or the wire.

/// <summary>One player of one scan (D9): the platform id, the position and whether alive. Phantom players never appear.</summary>
public readonly record struct ScanRow(string PlayerId, float X, float Z, bool Alive);

/// <summary>A region entry that fires <see cref="Definition"/> for <see cref="PlayerId"/> at the entry position.</summary>
public sealed record RegionEntry(EventDefinition Definition, string PlayerId, float X, float Z);

/// <summary>Region entry detection (automation D9, Business rules 3). An entry is a live player whose region in the previous
/// scan was known and differs from its region now, the region now named by a RegionEntered definition's scope and the
/// previous one not. RegionNames.None, an unmapped gap, is a known region outside every scope; only an id missing from the
/// previous scan is unknown, so a login or the first scan after boot makes no entry. A dead player makes no entry and its
/// row is updated, so a respawn or a waygate trip into the scope from a known region outside it is an entry, by design.
/// Per definition and player, an entry within playerCooldownMinutes of the last one that reached a start attempt is
/// dropped; those rows outlive the player's presence, bounded at <see cref="TriggerLimits.CooldownRows"/>.</summary>
public sealed class RegionEntries
{
    readonly Dictionary<string, string> _region = new(StringComparer.Ordinal);
    readonly Dictionary<(string Definition, string Player), (DateTime Last, DateTime Until)> _attempts = new();

    public int RegionRows => _region.Count;
    public int CooldownRows => _attempts.Count;

    /// <summary>One scan of <paramref name="players"/> against the startable RegionEntered <paramref name="definitions"/>:
    /// the entries outside their cooldown, in scan order then definition order. Every present player's region row is
    /// updated and an absent one's dropped.</summary>
    public IReadOnlyList<RegionEntry> Scan(IReadOnlyList<ScanRow> players, Func<float, float, string> regionOf,
        IReadOnlyList<EventDefinition> definitions, DateTime utcNow)
    {
        Expire(utcNow);
        var entries = new List<RegionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in players)
        {
            if (Phantoms.IsPhantom(p.PlayerId) || !seen.Add(p.PlayerId)) continue;
            if (!PlayerPosition.Usable(p.X, p.Z)) { seen.Remove(p.PlayerId); continue; }
            var now = regionOf(p.X, p.Z);
            if (p.Alive && _region.TryGetValue(p.PlayerId, out var before) && before != now)
                foreach (var d in definitions)
                {
                    if (!d.Trigger.Scope.Names(now) || d.Trigger.Scope.Names(before)) continue;
                    if (_attempts.TryGetValue((d.Id, p.PlayerId), out var row) && utcNow - row.Last < TimeSpan.FromMinutes(d.Trigger.PlayerCooldownMinutes))
                        continue;
                    entries.Add(new RegionEntry(d, p.PlayerId, p.X, p.Z));
                }
            _region[p.PlayerId] = now;
        }
        foreach (var gone in _region.Keys.Where(id => !seen.Contains(id)).ToList()) _region.Remove(gone);
        return entries;
    }

    /// <summary>An entry reached a start attempt, whether the conditions allowed the start or not: its cooldown starts
    /// now, so walking in and out cannot re-roll a chance condition. A cooldown of 0 keeps no row.</summary>
    public void Attempted(EventDefinition definition, string playerId, DateTime utcNow)
    {
        if (definition.Trigger.PlayerCooldownMinutes <= 0) return;
        _attempts[(definition.Id, playerId)] = (utcNow, utcNow.AddMinutes(definition.Trigger.PlayerCooldownMinutes));
        while (_attempts.Count > TriggerLimits.CooldownRows)
            _attempts.Remove(_attempts.MinBy(a => a.Value.Last).Key);
    }

    /// <summary>Drops the cooldown rows that have run out (Design › Data): a row outlives a disable, a reload or regions
    /// unavailable, so re-enabling a definition never lifts a player's cooldown (step 1 code review round 2 F2).</summary>
    void Expire(DateTime utcNow)
    {
        foreach (var key in _attempts.Keys.ToList())
            if (utcNow >= _attempts[key].Until) _attempts.Remove(key);
    }

    /// <summary>A restart: nothing is kept.</summary>
    public void Clear()
    {
        _region.Clear();
        _attempts.Clear();
    }
}

/// <summary>A death as the kill rule sees it (automation D11). <see cref="KillerPlayer"/> is the platform id of the killer
/// when it is a player character, and <see cref="OwnerPlayer"/> that of the player character its EntityOwner names (a
/// familiar, summon or projectile); a castle servant or structure, whose owner is no player character, has neither.
/// <see cref="VictimOurs"/> is read before SpawnTracker.Died forgets the unit. Position null when unread or unreadable.</summary>
public readonly record struct KillFacts(
    string? KillerPlayer,
    string? OwnerPlayer,
    bool VictimIsPlayer,
    bool VictimMinion,
    bool VictimOurs,
    string? VictimFaction,
    float? X = null,
    float? Z = null,
    bool VictimIsKiller = false)
{
    /// <summary>The player the kill counts for, or null.</summary>
    public string? Player => KillerPlayer ?? OwnerPlayer;
}

/// <summary>The FactionKills rule (automation D11, Business rules 4).</summary>
public static class KillRule
{
    /// <summary>True when the death counts for <paramref name="definition"/>: a player killed it (directly or through what it
    /// owns), the victim is not a player, a minion, one of our units or the killer itself, its faction is listed, and with a regional scope
    /// the victim stands in a named region (an unknown position counts nothing there).</summary>
    public static bool Counts(KillFacts kill, EventDefinition definition, Func<float, float, string>? regionOf)
    {
        var t = definition.Trigger;
        if (t.Type != TriggerType.FactionKills || kill.Player is null) return false;
        if (kill.VictimIsPlayer || kill.VictimMinion || kill.VictimOurs || kill.VictimIsKiller) return false;
        if (kill.VictimFaction is null || !(t.Factions ?? []).Contains(kill.VictimFaction, StringComparer.Ordinal)) return false;
        if (t.Scope.IsGlobal) return true;
        return kill.X is { } x && kill.Z is { } z && regionOf is not null && t.Scope.Names(regionOf(x, z));
    }

    /// <summary>The counter a kill goes to: the killer's platform id, or one counter for every player with shared.</summary>
    public static string CounterKey(EventDefinition definition, string player) => definition.Trigger.Shared ? SharedCounter : player;

    public const string SharedCounter = "*";
}

/// <summary>The FactionKills counters (automation D11): per definition and counter key, the times of the kills within the
/// last windowSeconds. A counter holding `kills` of them fires and is emptied, also when the gate then drops the fire for
/// an active event, so no burst of starts follows the end. Bounded at `kills` times per counter,
/// <see cref="TriggerLimits.CountersPerDefinition"/> counters per definition and <see cref="TriggerLimits.CountersInAll"/>
/// in all, the least recently updated counter dropped first.</summary>
public sealed class KillWindows
{
    readonly Dictionary<string, Dictionary<string, Counter>> _byDefinition = new(StringComparer.Ordinal);

    sealed class Counter
    {
        public readonly Queue<DateTime> Kills = new();
        public DateTime Updated;
    }

    public int Counters => _byDefinition.Values.Sum(d => d.Count);
    public int CountersFor(string definitionId) => _byDefinition.TryGetValue(definitionId, out var d) ? d.Count : 0;
    public int KillsHeld(string definitionId, string counterKey) =>
        _byDefinition.TryGetValue(definitionId, out var d) && d.TryGetValue(counterKey, out var c) ? c.Kills.Count : 0;

    /// <summary>Counts one kill; true when the counter reached `kills` within the window, and it is then emptied.</summary>
    public bool Add(EventDefinition definition, string counterKey, DateTime utcNow)
    {
        var t = definition.Trigger;
        if (!_byDefinition.TryGetValue(definition.Id, out var counters))
            _byDefinition[definition.Id] = counters = new Dictionary<string, Counter>(StringComparer.Ordinal);
        if (!counters.TryGetValue(counterKey, out var c)) counters[counterKey] = c = new Counter();
        c.Updated = utcNow;
        while (c.Kills.Count > 0 && utcNow - c.Kills.Peek() > TimeSpan.FromSeconds(t.WindowSeconds)) c.Kills.Dequeue();
        c.Kills.Enqueue(utcNow);
        while (c.Kills.Count > t.Kills) c.Kills.Dequeue();
        if (c.Kills.Count >= t.Kills)
        {
            counters.Remove(counterKey);
            if (counters.Count == 0) _byDefinition.Remove(definition.Id);
            return true;
        }
        while (counters.Count > TriggerLimits.CountersPerDefinition)
            counters.Remove(counters.Where(x => x.Key != counterKey).MinBy(x => x.Value.Updated).Key);
        while (Counters > TriggerLimits.CountersInAll)
        {
            var stalest = _byDefinition
                .SelectMany(d => d.Value.Where(x => d.Key != definition.Id || x.Key != counterKey).Select(x => (Def: d.Key, x.Key, x.Value.Updated)))
                .MinBy(x => x.Updated);
            _byDefinition[stalest.Def].Remove(stalest.Key);
            if (_byDefinition[stalest.Def].Count == 0) _byDefinition.Remove(stalest.Def);
        }
        return false;
    }

    /// <summary>Drops every counter whose newest kill left its definition's window (the Design › Data row), and the
    /// counters of definitions not in <paramref name="definitions"/>; TriggerBus calls it on each scan.</summary>
    public void Prune(IEnumerable<EventDefinition> definitions, DateTime utcNow)
    {
        var windows = definitions.ToDictionary(d => d.Id, d => TimeSpan.FromSeconds(d.Trigger.WindowSeconds), StringComparer.Ordinal);
        foreach (var (id, counters) in _byDefinition.ToList())
        {
            if (!windows.TryGetValue(id, out var window)) { _byDefinition.Remove(id); continue; }
            foreach (var (key, c) in counters.ToList())
                if (c.Kills.Count == 0 || utcNow - c.Kills.Last() > window) counters.Remove(key);
            if (counters.Count == 0) _byDefinition.Remove(id);
        }
    }

    /// <summary>Drops the counters of every definition that left the startable FactionKills set.</summary>
    public void Keep(IEnumerable<string> startableIds)
    {
        var keep = new HashSet<string>(startableIds, StringComparer.Ordinal);
        foreach (var id in _byDefinition.Keys.Where(id => !keep.Contains(id)).ToList()) _byDefinition.Remove(id);
    }

    public void Clear() => _byDefinition.Clear();
}

public enum GateStep { Drop, Attempt }

/// <summary>The gate every player-action trigger passes (automation D14, Business rules 8): a trigger reaching an active
/// event is dropped before any start attempt, with no line, so it never reaches the conditions or their chance roll; one
/// player's second trigger of one definition within 5 s is dropped; everything else is a start attempt through
/// EventRuntime.StartEvent. A refused player-action start logs at most once per definition per 60 s, the next line ending
/// "; &lt;n&gt; more since the last line".</summary>
public sealed class PlayerTriggerGate
{
    readonly Dictionary<(string Definition, string Player), DateTime> _dedupe = new();
    readonly Dictionary<string, (DateTime Last, int Held)> _refusals = new(StringComparer.Ordinal);

    /// <summary>Drop or attempt. An attempt sets the dedupe key; a drop sets nothing.</summary>
    public GateStep Admit(string definitionId, string playerId, bool active, DateTime utcNow)
    {
        if (active) return GateStep.Drop;
        foreach (var key in _dedupe.Where(k => utcNow - k.Value >= TimeSpan.FromSeconds(TriggerLimits.DedupeSeconds)).Select(k => k.Key).ToList())
            _dedupe.Remove(key);
        if (_dedupe.ContainsKey((definitionId, playerId))) return GateStep.Drop;
        _dedupe[(definitionId, playerId)] = utcNow;
        return GateStep.Attempt;
    }

    /// <summary>The line to log for a refused start of <paramref name="definitionId"/>, or null while the definition's
    /// quiet minute runs (the refusal is then counted and named by the next line).</summary>
    public string? Refused(string definitionId, string line, DateTime utcNow)
    {
        if (_refusals.TryGetValue(definitionId, out var r) && utcNow >= r.Last && utcNow - r.Last < TimeSpan.FromSeconds(TriggerLimits.RefusalQuietSeconds))
        {
            _refusals[definitionId] = (r.Last, r.Held + 1);
            return null;
        }
        var held = _refusals.TryGetValue(definitionId, out var before) ? before.Held : 0;
        _refusals[definitionId] = (utcNow, 0);
        return held > 0 ? $"{line}; {held} more since the last line" : line;
    }

    public void Clear()
    {
        _dedupe.Clear();
        _refusals.Clear();
    }
}

/// <summary>Phantom players for the fan-out session (automation D29), Debug builds only: Debug.FaultInjection =
/// phantoms:&lt;n&gt; (n 1-9) adds n AroundPlayer candidates at 200 m steps along +x from the first eligible real player,
/// at its height. A position PlayerPosition.Usable refuses is skipped. They are never Hunt targets, never in a region scan
/// or a kill counter: their ids carry <see cref="Prefix"/>, which those skip.</summary>
public static class Phantoms
{
    public const string Setting = "phantoms:";
    public const string Prefix = "phantom:";
    public const float Step = 200f;
    public const int Max = 9;

    public static bool IsPhantom(string? playerId) => playerId?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>n for "phantoms:&lt;n&gt;" with n 1-9, else null.</summary>
    public static int? Parse(string? faultInjection)
    {
        if (faultInjection is null || !faultInjection.StartsWith(Setting, StringComparison.Ordinal)) return null;
        var text = faultInjection[Setting.Length..];
        return text.Length == 1 && text[0] is >= '1' and <= '9' ? text[0] - '0' : null;
    }

    /// <summary>The phantoms for one pick: none without an eligible real player (online, alive, not in PvP combat, a usable
    /// position, and, as the pick itself requires (D5), <paramref name="allowed"/>: outside claimed territory and inside the
    /// action scope); else up to <paramref name="n"/>, the i-th at +i × 200 m on x, those at an unusable position skipped.</summary>
    public static IReadOnlyList<PickCandidate> Place(IReadOnlyList<PickCandidate> real, int n, Func<float, float, bool>? allowed = null)
    {
        var first = real.ToList().FindIndex(p => p.Online && p.Alive && !p.InPvpCombat && PlayerPosition.Usable(p.X, p.Y, p.Z) && !IsPhantom(p.PlatformId)
            && (allowed is null || allowed(p.X, p.Z)));
        if (first < 0) return [];
        var anchor = real[first];
        var placed = new List<PickCandidate>();
        for (var i = 1; i <= n; i++)
        {
            var x = anchor.X + i * Step;
            if (!PlayerPosition.Usable(x, anchor.Y, anchor.Z)) continue;
            placed.Add(new PickCandidate(x, anchor.Y, anchor.Z, true, true, false, $"{Prefix}{i}"));
        }
        return placed;
    }

    /// <summary>"phantoms: &lt;placed&gt; of &lt;n&gt; placed", logged once per pick (D7, D29).</summary>
    public static string PlacedLine(int placed, int n) => $"phantoms: {placed} of {n} placed";

    /// <summary>The Debug-only verbose line of a phantom group: its distance from its phantom, never a coordinate (D7, D19).</summary>
    public static string GroupLine(string eventId, int wave, int group, double metres) =>
        FormattableString.Invariant($"fanout {eventId} wave {wave}: phantom group {group} {metres:0} m from its phantom");
}

/// <summary>A player-action start to attempt (automation D13, D14): the definition, the trigger's name for the log, the
/// triggering player's platform id (the focus, memory only) and the entry or kill position the start's scope check reads
/// (null for a Global kill, whose position is not read).</summary>
public sealed record PlayerFire(EventDefinition Definition, string Trigger, string PlayerId, (float X, float Z)? At);

/// <summary>The player-action triggers as Services/TriggerBus drives them (automation D9, D11, D14, D15). The scan and the
/// kill read each run inside their own guard: a throw skips that scan or that death, logs once per failure streak and
/// holds a health entry until a read succeeds. Garbage is refused where read: a position PlayerPosition.Usable refuses
/// gives no region, and a faction that is no Faction_ name counts for no definition. Every attempt goes to
/// <c>start</c> at once, so a second trigger in the same scan finds the event active and is dropped (Design › States 7.2).</summary>
public sealed class PlayerTriggerFeed
{
    public const string ScanFailing = "triggers: player scan failing";
    public const string KillFailing = "triggers: kill read failing";

    readonly FailureStreak _scanFaults = new();
    readonly FailureStreak _killFaults = new();

    public RegionEntries Entries { get; } = new();
    public KillWindows Kills { get; } = new();
    public PlayerTriggerGate Gate { get; } = new();

    /// <summary>The health entries (D15), shown in the health line, `.nyar status` and the admin login notice.</summary>
    public IReadOnlyList<string> Health =>
        (_scanFaults.Count > 0 ? [ScanFailing] : Array.Empty<string>()).Concat(_killFaults.Count > 0 ? [KillFailing] : []).ToList();

    /// <summary>One 5 s scan. Prunes the kill counters against the startable FactionKills set; then, with a startable
    /// RegionEntered definition and the regions available, reads the players once and turns each entry that passes the
    /// cooldown and the gate into a start attempt, whose cooldown starts then. With none, no player is read.</summary>
    public void Scan(Func<IReadOnlyList<ScanRow>> read, Func<float, float, string>? regionOf, DefinitionSet set,
        Func<string, bool> isActive, DateTime utcNow, Action<string> log, Action<PlayerFire> start, Action<string>? verbose = null)
    {
        Kills.Prune(TriggerRouter.Candidates(set, TriggerType.FactionKills), utcNow);
        var definitions = TriggerRouter.Candidates(set, TriggerType.RegionEntered).ToList();
        if (definitions.Count == 0 || regionOf is null)
        {
            _scanFaults.Ok();                                                   // nothing is scanned, so nothing is failing
            return;
        }
        IReadOnlyList<RegionEntry> entries;
        int players;
        try
        {
            var rows = read();
            players = rows.Count;
            entries = Entries.Scan(rows, regionOf, definitions, utcNow);
        }
        catch (Exception ex)
        {
            if (_scanFaults.Fail()) log($"player triggers: scan failed: {ex.Message}");
            return;
        }
        _scanFaults.Ok();
        verbose?.Invoke($"player triggers: {players} players, {entries.Count} entries");
        foreach (var e in entries)
        {
            if (Gate.Admit(e.Definition.Id, e.PlayerId, isActive(e.Definition.Id), utcNow) != GateStep.Attempt) continue;
            Entries.Attempted(e.Definition, e.PlayerId, utcNow);
            start(new PlayerFire(e.Definition, nameof(TriggerType.RegionEntered), e.PlayerId, (e.X, e.Z)));
        }
    }

    /// <summary>True when a death is worth reading: the DeathEvent hook is available and a FactionKills definition is
    /// startable. Otherwise the kill rule returns at once (D11, Performance).</summary>
    public static bool WantsKills(DefinitionSet set, Func<TriggerType, bool> allows) =>
        allows(TriggerType.FactionKills) && TriggerRouter.Candidates(set, TriggerType.FactionKills).Any();

    /// <summary>One death. <paramref name="read"/> gets whether the position is needed (a scoped FactionKills definition is
    /// startable) and returns the kill; a throwing read or region lookup skips this death. Each definition the kill counts
    /// for adds it to its counter; a counter that reaches its kills is emptied and, past the gate, becomes a start attempt
    /// focused on the killing player.</summary>
    public void Died(Func<bool, KillFacts> read, DefinitionSet set, Func<float, float, string>? regionOf, Func<TriggerType, bool> allows,
        Func<string, bool> isActive, DateTime utcNow, Action<string> log, Action<PlayerFire> start, Action<string>? verbose = null)
    {
        if (!WantsKills(set, allows)) return;
        var definitions = TriggerRouter.Candidates(set, TriggerType.FactionKills).ToList();
        KillFacts kill;
        List<EventDefinition> counted;
        try
        {
            kill = Clean(read(definitions.Any(d => !d.Trigger.Scope.IsGlobal)));
            counted = definitions.Where(d => KillRule.Counts(kill, d, regionOf)).ToList();
        }
        catch (Exception ex)
        {
            if (_killFaults.Fail()) log($"faction kills: read failed: {ex.Message}");
            return;
        }
        _killFaults.Ok();
        if (kill.Player is not { } player) return;
        foreach (var d in counted)
        {
            if (!Kills.Add(d, KillRule.CounterKey(d, player), utcNow)) continue;
            verbose?.Invoke($"faction kills: {d.Id} reached {d.Trigger.Kills}");
            if (Gate.Admit(d.Id, player, isActive(d.Id), utcNow) != GateStep.Attempt) continue;
            start(new PlayerFire(d, nameof(TriggerType.FactionKills), player,
                kill.X is { } x && kill.Z is { } z ? (x, z) : null));
        }
    }

    /// <summary>Garbage refused where read (D15): an unusable position becomes unknown, a faction that is no Faction_ name
    /// (an unnamed guid reads as "PrefabGuid(n)") becomes none.</summary>
    static KillFacts Clean(KillFacts kill)
    {
        if (kill.X is not { } x || kill.Z is not { } z || !PlayerPosition.Usable(x, z)) kill = kill with { X = null, Z = null };
        if (kill.VictimFaction is { } f && !f.StartsWith("Faction_", StringComparison.Ordinal)) kill = kill with { VictimFaction = null };
        return kill;
    }

    /// <summary>A restart: nothing is kept.</summary>
    public void Clear()
    {
        Entries.Clear();
        Kills.Clear();
        Gate.Clear();
        _scanFaults.Ok();
        _killFaults.Ok();
    }
}
