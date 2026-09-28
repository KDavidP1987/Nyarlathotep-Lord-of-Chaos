#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>Why an admin action was refused (raphael-api-admin D2, Business rules 3). The first seven are contract §4's
/// codes; the rest are the twins' (contract §4 and §5a, S-3). Each maps one to one to <see cref="WireError"/> (Wire.Code).</summary>
public enum RefusalCode
{
    NotReady, NoAccess, Disabled, NotFound, BadArg, RateLimit, Cooldown,
    Exists, State, Invalid, Full, Io, Confirm, Limit,
}

/// <summary>The closed list of refusal reasons (raphael-api-admin D2): wire-safe snake_case words.
/// <see cref="Outcome.Refused"/> refuses any other.</summary>
public static class Reasons
{
    public const string General = "general";
    public const string PillarOff = "pillar_off";
    public const string MaxConcurrent = "max_concurrent";
    public const string Disabled = "disabled";
    public const string AlreadyActive = "already_active";
    public const string EmpowerClash = "empower_clash";
    public const string AdminLocation = "admin_location";
    public const string NoPosition = "no_position";
    public const string Condition = "condition";
    public const string NotActive = "not_active";
    public const string Field = "field";
    public const string Value = "value";
    public const string Trigger = "trigger";
    public const string Stats = "stats";
    public const string Read = "read";
    public const string Parse = "parse";
    public const string Stale = "stale";
    public const string ReadOnly = "read_only";
    public const string Size = "size";
    public const string WriteUncertain = "write_uncertain";
    public const string Count = "count";
    public const string Running = "running";
    public const string Save = "save";
    public const string NothingToPurge = "nothing_to_purge";
    public const string Internal = "internal";
    public const string Template = "template";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        General, PillarOff, MaxConcurrent, Disabled, AlreadyActive, EmpowerClash, AdminLocation, NoPosition, Condition,
        NotActive, Field, Value, Trigger, Stats, Read, Parse, Stale, ReadOnly, Size, WriteUncertain, Count, Running, Save,
        NothingToPurge, Internal, Template,
    };
}

/// <summary>What an admin action did (raphael-api-admin D1): the human reply, which is 0.5.1's text, and for a refusal
/// its code, argument, seconds and reason, for the wire twins. <see cref="Fields"/> are a success's wire keys
/// (changed, count, …). The two factories are the only constructors, so no refusal exists without a code.</summary>
public sealed class Outcome
{
    public bool Ok { get; }
    public string Human { get; }
    public RefusalCode? Code { get; }
    public string? Arg { get; }
    public int? Secs { get; }
    public string? Reason { get; }
    public IReadOnlyList<(string Key, string Value)> Fields { get; }

    Outcome(bool ok, string human, RefusalCode? code, string? arg, int? secs, string? reason, IReadOnlyList<(string, string)> fields)
    {
        Ok = ok;
        Human = human;
        Code = code;
        Arg = arg;
        Secs = secs;
        Reason = reason;
        Fields = fields;
    }

    /// <summary>A success with its human reply and wire keys.</summary>
    public static Outcome Done(string human, params (string Key, string Value)[] fields)
    {
        if (string.IsNullOrEmpty(human)) throw new ArgumentException("an outcome needs its human reply", nameof(human));
        return new Outcome(true, human, null, null, null, null, fields);
    }

    /// <summary>A refusal with its human reply and code; <paramref name="reason"/>, when given, is one of
    /// <see cref="Reasons.All"/>.</summary>
    public static Outcome Refused(string human, RefusalCode code, string? arg = null, int? secs = null, string? reason = null)
    {
        if (string.IsNullOrEmpty(human)) throw new ArgumentException("an outcome needs its human reply", nameof(human));
        if (reason is not null && !Reasons.All.Contains(reason)) throw new ArgumentException($"reason {reason} is not in Reasons", nameof(reason));
        return new Outcome(false, human, code, arg, secs, reason, []);
    }

    /// <summary>The same outcome with another human reply (a reply that appends to the one it wraps).</summary>
    public Outcome WithHuman(string human) =>
        Ok ? Done(human, [.. Fields]) : Refused(human, Code!.Value, Arg, Secs, Reason);

    /// <summary>The value of wire key <paramref name="key"/>, or null.</summary>
    public string? Field(string key)
    {
        foreach (var (k, v) in Fields) if (k == key) return v;
        return null;
    }

    public override string ToString() => Human;
}

/// <summary>The io reason of an events.json error text (raphael-api-admin D2, Business rules 3): the file layer
/// (Logic/DataStore, Validation, StaleFile) reports errors as text, and each text family maps to one reason.</summary>
public static class FileErrors
{
    public static string Reason(string error)
    {
        if (error == StaleFile.Refusal) return Reasons.Stale;
        if (error.EndsWith("it is read-only", StringComparison.Ordinal)) return Reasons.ReadOnly;
        if (error.StartsWith("events.json write failed", StringComparison.Ordinal) || error.StartsWith(EventsFile.WriteFailed, StringComparison.Ordinal))
            return Reasons.Save;
        if (error.StartsWith("events.json rejected", StringComparison.Ordinal) || error.StartsWith("events.json is ", StringComparison.Ordinal)
            || error.StartsWith("events.json does not parse", StringComparison.Ordinal) || error == "events.json has no events array")
            return Reasons.Parse;
        return Reasons.Read;                                    // could not be read, not found
    }

    /// <summary>An events.json error as a coded refusal.</summary>
    public static Outcome Refusal(string error) => Outcome.Refused(error, RefusalCode.Io, reason: Reason(error));
}
