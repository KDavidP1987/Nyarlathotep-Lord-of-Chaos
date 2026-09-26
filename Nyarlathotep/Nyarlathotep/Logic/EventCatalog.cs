#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The loaded definitions and the running instances (foundation D6). A reload swaps in a new
/// <see cref="DefinitionSet"/> only when the file loaded; running instances keep the definition object they
/// started with, and a start always reads the current set. Services/EventStore and EventRuntime drive it.</summary>
public sealed class EventCatalog
{
    readonly Dictionary<string, RunningInstance> _running = new(StringComparer.Ordinal);

    public DefinitionSet Current { get; private set; } = DefinitionSet.Empty;
    public FileStamp? LoadedStamp { get; private set; }
    public IReadOnlyCollection<RunningInstance> Running => _running.Values;

    /// <summary>Told of every applied load (raphael-api-core D6, A6): `.nyar event reload`, set, enable and disable
    /// all apply through <see cref="Reload"/>. A rejected file reports nothing.</summary>
    public IPushSink? Push { get; set; }

    /// <summary>Applies a load. A rejected file keeps the last valid set and returns its error.</summary>
    public string? Reload(LoadResult result, FileStamp stamp)
    {
        if (result.FileError is not null) return result.FileError;
        Current = result.Set;
        LoadedStamp = stamp;
        Push?.ConfigChanged();
        return null;
    }

    /// <summary>Starts the current definition <paramref name="id"/>: "unknown event", "already active" or null.</summary>
    public string? TryStart(string id, DateTime utcNow, out RunningInstance? instance)
    {
        instance = null;
        var def = Current.Find(id);
        if (def is null) return $"unknown event {id}";
        if (_running.ContainsKey(id)) return "already active";
        instance = new RunningInstance(def, utcNow, utcNow.AddSeconds(def.DurationSeconds));
        _running.Add(id, instance);
        return null;
    }

    public string? TryEnd(string id) => _running.Remove(id) ? null : "not active";

    public void EndAll() => _running.Clear();
}
