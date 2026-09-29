#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>Replies only admins see (Design › UX: `.nyar spawn`, `.nyar purge`, `.nyar debug here`, the tracked
/// count in `.nyar status`). Kept apart from <see cref="Messages"/>, whose builders may never carry a position; these
/// carry none either, but `debug here` works from positions the service reads.</summary>
public static partial class AdminLines
{
    public const int DebugMaxLines = 20;
    public const string NothingToPurge = "nothing to purge";
    public const string NotArmed = "run .nyar purge first";

    /// <summary>"unknown event &lt;id&gt;" (raphael-api-admin D1, D2: notfound, arg id).</summary>
    public static Outcome UnknownEvent(string id) => Outcome.Refused($"unknown event {id}", RefusalCode.NotFound, "id");

    /// <summary>"already active": the engine's one-instance rule (D5: state, already_active).</summary>
    public static Outcome AlreadyActive(string id) => Outcome.Refused("already active", RefusalCode.State, "id", reason: Reasons.AlreadyActive);

    /// <summary>"not active" (D5: state, not_active).</summary>
    public static Outcome NotActive(string id) => Outcome.Refused("not active", RefusalCode.State, "id", reason: Reasons.NotActive);

    /// <summary>`.nyar event`'s reply to a verb it does not know.</summary>
    public const string EventVerbs = "argument must be list, info, start, stop, enable, disable, set, reload, new, copy or delete";

    /// <summary>`.nyar template`'s reply to a verb it does not know.</summary>
    public const string TemplateVerbs = "argument must be list, info or use";

    /// <summary>`.nyar purge`'s reply to an argument other than confirm.</summary>
    public const string PurgeArgs = "argument must be confirm or nothing";

    /// <summary>A start or spawn that needs the admin's position when it cannot be read (Business rules 3: badarg
    /// location no_position).</summary>
    public const string NoPosition = "your position could not be read";

    public static readonly Outcome PositionUnread = Outcome.Refused(NoPosition, RefusalCode.BadArg, "location", reason: Reasons.NoPosition);

    /// <summary>"event &lt;id&gt; started" (D29).</summary>
    public static Outcome Started(string id) => Outcome.Done($"event {id} started");

    /// <summary>"event &lt;id&gt; stopped".</summary>
    public static Outcome Stopped(string id) => Outcome.Done($"event {id} stopped");

    /// <summary>An automatic start while the master or the event's pillar switch is off: "off", logged by no one
    /// (foundation Business rules 6; raphael-api-admin Business rules 3: disabled general).</summary>
    public static readonly Outcome SystemOff = Outcome.Refused("off", RefusalCode.Disabled, reason: Reasons.General);

    /// <summary>An automatic start a condition blocks (players, mode, window, cooldown, chance): state condition.</summary>
    public static Outcome ConditionBlocked(string blocker) => Outcome.Refused(blocker, RefusalCode.State, reason: Reasons.Condition);

    /// <summary>The engine's refusal of a start as its caller sees it: an admin refused by the purge cooldown is told
    /// the seconds left (A7, D30), in the text and in secs; any other refusal is the engine's own.</summary>
    public static Outcome StartRefused(Outcome refused, bool admin, DateTime? purgeUntilUtc, DateTime nowUtc)
    {
        if (!admin || refused.Code != RefusalCode.Cooldown || CooldownLeft(purgeUntilUtc, nowUtc) is not { } text) return refused;
        return Outcome.Refused(text, RefusalCode.Cooldown, secs: SecondsLeft(purgeUntilUtc!.Value, nowUtc));
    }

    /// <summary>The log line of a refused start (regions D4, A19, A23): a System start refused because no player is in the
    /// event's regions is a skipped occurrence; every other refusal, an admin's included, keeps the generic line.</summary>
    public static string StartRefusedLog(string id, string trigger, Outcome refused, bool system, Scope scope) =>
        system && refused.Reason == Reasons.NoPlayerInRegion
            ? $"event {id}: skipped, no player in {string.Join(", ", scope.Regions)}"
            : $"event {id} not started by {trigger}: {refused.Human}";

    public static readonly Outcome NothingToPurgeOutcome = Outcome.Refused(NothingToPurge, RefusalCode.State, reason: Reasons.NothingToPurge);
    public static readonly Outcome NotArmedOutcome = Outcome.Refused(NotArmed, RefusalCode.Confirm);

    /// <summary>The purge ask's prompt; the confirm window is 30 s.</summary>
    public static Outcome PurgeAsked(int events, int units) =>
        Outcome.Done(PurgePrompt(events, units), ("confirm", ((int)PurgeArming.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>The purge's reply, with the counts and the cooldown it started.</summary>
    public static Outcome PurgeDone(int events, int units, int cooldownSeconds) =>
        Outcome.Done(Purged(events, units), ("events", Invariant(events)), ("units", Invariant(units)), ("secs", Invariant(cooldownSeconds)));

    static string Invariant(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static int SecondsLeft(DateTime untilUtc, DateTime nowUtc) => (int)Math.Ceiling((untilUtc - nowUtc).TotalSeconds);

    public static string Spawned(int queued, string prefab, string? skipped) =>
        queued == 0 ? skipped ?? $"spawned 0 {prefab}"
        : skipped is null ? $"spawned {queued} {prefab}"
        : $"spawned {queued} {prefab}; {skipped}";

    public static string PurgePrompt(int events, int units) =>
        $"purge ends {events} events and despawns {units} units; run .nyar purge confirm within 30 s";

    public static string Purged(int events, int units) => $"purged: {events} events, {units} units queued";

    /// <summary>The manual start reply while the purge cooldown runs (A7, D30): "purge cooldown active (&lt;n&gt; s left)",
    /// n the whole seconds to <paramref name="untilUtc"/> rounded up; null once the cooldown is over. The StartBlocker
    /// label stays "purge cooldown active".</summary>
    public static string? CooldownLeft(DateTime? untilUtc, DateTime nowUtc) =>
        untilUtc is { } until && until > nowUtc
            ? $"purge cooldown active ({SecondsLeft(until, nowUtc)} s left)"
            : null;

    /// <summary>The private line an admin gets on connecting while something is degraded (D31).</summary>
    public static string DegradedNotice(IReadOnlyCollection<string> degraded) =>
        $"nyar: degraded: {string.Join(", ", degraded)} (see .nyar status and the server log)";

    /// <summary>The sources a `debug walk` reading names (walkable-spawns D1, A8, A10): the live tile world in world
    /// metres or in the tile grid.</summary>
    public static readonly IReadOnlyList<string> WalkSources = ["singleton world", "singleton tile"];

    /// <summary>The `debug walk` reply when no source answered (walkable-spawns D1, D5).</summary>
    public static string WalkUnavailable(string reason) => $"walk check unavailable: {reason}";

    /// <summary>The `debug walk` reply (walkable-spawns D1, A4): the admin's own x and z, the height level, the radius,
    /// the verdict and the grounded read, and the source that answered. It goes to the admin only (D11).</summary>
    public static string WalkReply(float x, float z, int heightLevel, float radius, bool free, bool grounded, string source) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"walk {x:0.0} {z:0.0} h {heightLevel} r {radius:0.00}: {(free ? "free" : "blocked")} grounded {(grounded ? "yes" : "no")} ({source})");

    public static string Tracked(int tracked, int pendingSpawns, int pendingDespawns) =>
        $"tracked units: {tracked} (spawning {pendingSpawns}, despawning {pendingDespawns})";

    /// <summary>One `debug here` line: prefab, event (or "manual"), lifetime left (null: the unit has no LifeTime, a
    /// recipe fault shown as "left NONE"), level, Health and PhysicalPower.</summary>
    public static string DebugUnit(string prefab, string? eventId, int? leftSeconds, int level, int health, int maxHealth, int power) =>
        $"{prefab} {eventId ?? "manual"} left {(leftSeconds is { } s ? $"{Math.Max(0, s)}s" : "NONE")} lvl {level} hp {health}/{maxHealth} pp {power}";

    /// <summary>The unit line with the readings of event-spawns D10: SpellPower, MovementSpeed and the primary attack
    /// speed, and the unit's x/z, e.g. "… pp 12 sp 10 ms 4.5 as 1 at 120,-340" (a unit's place, never a player's).</summary>
    public static string DebugUnit(string prefab, string? eventId, int? leftSeconds, int level, int health, int maxHealth, int power,
        int spellPower, float moveSpeed, float attackSpeed, (int X, int Z) at) =>
        DebugUnit(prefab, eventId, leftSeconds, level, health, maxHealth, power)
        + FormattableString.Invariant($" sp {spellPower} ms {moveSpeed:0.##} as {attackSpeed:0.##} at {at.X},{at.Z}");

    /// <summary>The spawn recipe as `debug here` sees it (D27): "recipe ok" when the unit has LifeTime, Age and
    /// DestroyWhenDisabled and no DontSaveEntity (A9), otherwise "recipe" and each fault, e.g. "recipe -Age +DontSave".</summary>
    public static string Recipe(bool lifeTime, bool age, bool destroyWhenDisabled, bool dontSave)
    {
        var faults = new List<string>();
        if (!lifeTime) faults.Add("-LifeTime");
        if (!age) faults.Add("-Age");
        if (!destroyWhenDisabled) faults.Add("-DestroyWhenDisabled");
        if (dontSave) faults.Add("+DontSave");
        return faults.Count == 0 ? "recipe ok" : "recipe " + string.Join(" ", faults);
    }

    /// <summary>At most <see cref="DebugMaxLines"/> lines, then "+&lt;k&gt; more"; none → "no tracked units within
    /// &lt;r&gt; m".</summary>
    public static IReadOnlyList<string> DebugReport(IReadOnlyList<string> lines, int radius)
    {
        if (lines.Count == 0) return [$"no tracked units within {radius} m"];
        if (lines.Count <= DebugMaxLines) return lines;
        return [.. lines.Take(DebugMaxLines), $"+{lines.Count - DebugMaxLines} more"];
    }

    /// <summary>Joins <paramref name="lines"/> with newlines into as few messages of at most <paramref name="maxBytes"/>
    /// UTF-8 bytes as fit, never splitting a line; a single line longer than the cap is cut at a whole character.
    /// Chat drops a burst of separate replies (A8).</summary>
    public static IReadOnlyList<string> Pack(IReadOnlyList<string> lines, int maxBytes = Wire.MaxBytes)
    {
        var messages = new List<string>();
        var current = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var raw in lines)
        {
            var line = Cut(raw, maxBytes);
            var size = System.Text.Encoding.UTF8.GetByteCount(line);
            if (current.Length > 0 && bytes + 1 + size > maxBytes)
            {
                messages.Add(current.ToString());
                current.Clear();
                bytes = 0;
            }
            if (current.Length > 0) { current.Append('\n'); bytes++; }
            current.Append(line);
            bytes += size;
        }
        if (current.Length > 0) messages.Add(current.ToString());
        return messages;
    }

    static string Cut(string line, int maxBytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(line) <= maxBytes) return line;
        var sb = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var r in line.EnumerateRunes())
        {
            if (bytes + r.Utf8SequenceLength > maxBytes) break;
            sb.Append(r.ToString());
            bytes += r.Utf8SequenceLength;
        }
        return sb.ToString();
    }

    /// <summary>The log line for a mutating admin command (Security › Personal data): BepInEx overwrites the log on
    /// each boot, and audit records never quote it.</summary>
    public static string AdminRan(string name, ulong platformId, string command) =>
        $"admin {TextSink.Name(name)} ({platformId}) ran {command}";
}
