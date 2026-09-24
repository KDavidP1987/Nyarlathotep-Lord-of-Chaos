#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The runtime dependencies of foundation Interfaces › External. Each has a row in
/// <see cref="DependencyPolicy"/> and a fault case in Nyarlathotep.Tests DependencyFailureTests (D9).</summary>
public enum Dependency
{
    Disk,
    EventsJson,
    StateJson,
    CfgValues,
    HookDeathEvent,
    HookDayNight,
    HookUserConnect,
    ConnectedUsers,
    CommandRegistration,
}

/// <summary>What one dependency's failure is allowed to affect, and what the mod does about it.</summary>
public sealed record FailurePolicy(string Scope, string OnFailure);

public static class DependencyPolicy
{
    public static readonly IReadOnlyDictionary<Dependency, FailurePolicy> Rows = new Dictionary<Dependency, FailurePolicy>
    {
        [Dependency.Disk] = new("the file being written", "keep state in memory, retry once per second, one log line per failure streak"),
        [Dependency.EventsJson] = new("the bad event, or the whole file when it does not parse", "disable the event with its reason; an unparsable file keeps the last valid set"),
        [Dependency.StateJson] = new("state.json", "rename to state.json.corrupt, replacing an earlier one, and start from an empty state"),
        [Dependency.CfgValues] = new("the one key", "clamp to its range and log once at load"),
        [Dependency.HookDeathEvent] = new("pruning and VBloodKilled triggers", "mark the hook unavailable and disable its pillar"),
        [Dependency.HookDayNight] = new("GameTime triggers", "mark the hook unavailable and disable GameTime triggers"),
        [Dependency.HookUserConnect] = new("login notices", "mark the hook unavailable; admins see the degraded notice in status"),
        [Dependency.ConnectedUsers] = new("the one send", "skip that recipient, one log line per failure streak"),
        [Dependency.CommandRegistration] = new("the one command group", "log the group that failed; the rest register"),
    };
}
