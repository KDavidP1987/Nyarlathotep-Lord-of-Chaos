#nullable enable
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyarlathotep.Logic;

public enum SchemaMode { Current, Migrate, ReadOnly }

/// <summary>A file of a newer schema is loaded read-only and never written (a downgrade must not destroy it);
/// an older one is migrated in memory and written only by the next change (D8).</summary>
public static class SchemaPolicy
{
    public static SchemaMode Decide(int version, int current) =>
        version > current ? SchemaMode.ReadOnly : version < current ? SchemaMode.Migrate : SchemaMode.Current;
}

/// <summary>Logs the first failure of a streak only; a success ends the streak (D9).</summary>
public sealed class FailureStreak
{
    public int Count { get; private set; }

    /// <summary>True for the first failure of a streak, the one to log.</summary>
    public bool Fail() => ++Count == 1;

    public void Ok() => Count = 0;
}

/// <summary>The write protocol over <see cref="IFileStore"/>: a write goes to the .tmp and is promoted in one
/// step, so a crash leaves either the old or the new file, never a partial one (D8).</summary>
public sealed class DataStore(IFileStore fs, Action<string> log)
{
    public IFileStore Files => fs;

    /// <summary>Deletes a .tmp left by an interrupted write. Called once per file at load.</summary>
    public void CleanStaleTmp(DataFile file)
    {
        var name = DataPaths.FileName(file, FileVariant.Tmp);
        try
        {
            if (!fs.Exists(file, FileVariant.Tmp)) return;
            fs.Delete(file, FileVariant.Tmp);
            log($"removed stale {name}");
        }
        catch (Exception ex)
        {
            log($"could not remove stale {name}: {ex.Message}");
        }
    }

    /// <summary>Null on success; otherwise the error, with the main file untouched and the .tmp removed.</summary>
    public string? WriteAtomic(DataFile file, byte[] content, bool keepBackup) =>
        WriteThen(file, content, () => fs.Promote(file, keepBackup));

    /// <summary>As <see cref="WriteAtomic"/>, but fails instead of replacing a main file that exists.</summary>
    public string? WriteAtomicNew(DataFile file, byte[] content) =>
        WriteThen(file, content, () => fs.PromoteNew(file));

    string? WriteThen(DataFile file, byte[] content, Action promote)
    {
        try
        {
            fs.Write(file, FileVariant.Tmp, content);
            promote();
            return null;
        }
        catch (Exception ex)
        {
            try { if (fs.Exists(file, FileVariant.Tmp)) fs.Delete(file, FileVariant.Tmp); }
            catch (Exception) { /* the next load removes it */ }
            return ex.Message;
        }
    }
}

// ---------------------------------------------------------------- state.json v1

public sealed record StateInstance(string EventId, DateTime StartedUtc, DateTime EndsUtc, string Phase);

/// <summary>A tracked unit as the admin sees it; <see cref="EventId"/> is empty for a `.nyar spawn` unit.</summary>
public sealed record StateUnit(string EventId, string Prefab, float X, float Z, DateTime SpawnedUtc);

/// <summary>The last fired schedule occurrence: server-local "yyyy-MM-dd HH:mm" and its UTC instant (D4).</summary>
public sealed record LastFired(string Occurrence, DateTime Utc);

/// <summary>state.json v1 (Design › Data). Informational: the boot sweep never trusts it over the marker query.</summary>
public sealed class StateDocument
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("SchemaVersion")] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<StateInstance> Instances { get; set; } = [];
    public List<StateUnit> Units { get; set; } = [];
    public Dictionary<string, LastFired> LastFired { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Each event's last start, for conditions.cooldownMinutes across restarts.</summary>
    public Dictionary<string, DateTime> LastStart { get; set; } = new(StringComparer.Ordinal);
    public DateTime? PurgeUntilUtc { get; set; }

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, Options);

    /// <summary>Null when the bytes are not a state.json object with a SchemaVersion of 1 or higher.</summary>
    public static StateDocument? TryParse(byte[] bytes)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<StateDocument>(bytes, Options);
            if (doc is null || doc.SchemaVersion < 1) return null;
            doc.Instances ??= [];
            doc.Units ??= [];
            doc.LastFired ??= new(StringComparer.Ordinal);
            return doc;
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}

/// <summary>Owns state.json: loads it (a corrupt file is renamed .corrupt, replacing an earlier one, and an
/// empty state is used), and writes it at most once per second while it has changes, retrying a failed write
/// every second with one log line per failure streak (D8, D9).</summary>
public sealed class StateStore(DataStore store, Func<DateTime> utcNow, Action<string> log)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    readonly FailureStreak _failures = new();
    readonly FailureStreak _slow = new();
    DateTime? _lastAttempt;

    public StateDocument Document { get; private set; } = new();
    public bool ReadOnly { get; private set; }
    public bool Dirty { get; private set; }
    public int FailureCount => _failures.Count;

    public void Load()
    {
        store.CleanStaleTmp(DataFile.State);
        Document = new StateDocument();
        ReadOnly = false;
        Dirty = false;
        byte[]? bytes;
        try { bytes = store.Files.Read(DataFile.State, FileVariant.Main); }
        catch (Exception ex)
        {
            log($"state.json could not be read ({ex.Message}); starting from an empty state");
            return;
        }
        if (bytes is null) return;

        var doc = StateDocument.TryParse(bytes);
        if (doc is null)
        {
            try
            {
                store.Files.Rename(DataFile.State, FileVariant.Main, FileVariant.Corrupt);
                log("state.json is unparsable: renamed to state.json.corrupt; starting from an empty state");
            }
            catch (Exception ex)
            {
                log($"state.json is unparsable and could not be renamed ({ex.Message}); starting from an empty state");
            }
            return;
        }

        switch (SchemaPolicy.Decide(doc.SchemaVersion, StateDocument.CurrentSchemaVersion))
        {
            case SchemaMode.ReadOnly:
                ReadOnly = true;
                log($"state.json SchemaVersion {doc.SchemaVersion} is newer than {StateDocument.CurrentSchemaVersion}: loaded read-only, never written");
                break;
            case SchemaMode.Migrate:
                log($"state.json SchemaVersion {doc.SchemaVersion} migrated in memory; written with the next change");
                doc.SchemaVersion = StateDocument.CurrentSchemaVersion;
                break;
        }
        Document = doc;
    }

    public void MarkDirty() => Dirty = true;

    /// <summary>Called every scheduler tick; <paramref name="force"/> at shutdown ignores the interval.</summary>
    public void Flush(bool force = false)
    {
        if (ReadOnly || !Dirty) return;
        var start = utcNow();
        if (!force && _lastAttempt is { } last && start - last < MinInterval) return;
        _lastAttempt = start;

        byte[] bytes;
        try { bytes = Document.Serialize(); }
        catch (Exception ex)
        {
            if (_failures.Fail()) log($"state.json could not be serialised ({ex.Message}); state kept in memory, retrying every second");
            return;
        }

        var error = store.WriteAtomic(DataFile.State, bytes, keepBackup: false);
        var took = utcNow() - start;
        if (error is not null)
        {
            if (_failures.Fail()) log($"state.json write failed ({error}); state kept in memory, retrying every second");
            return;
        }
        _failures.Ok();
        Dirty = false;
        if (took > MinInterval) { if (_slow.Fail()) log($"state.json write slow ({(int)took.TotalMilliseconds} ms)"); }
        else _slow.Ok();
    }
}

// ---------------------------------------------------------------- events.json

/// <summary>Reads and writes events.json: a stale .tmp is removed at load, a newer schema loads read-only, an
/// admin edit writes only an unchanged file and keeps one .bak (D6, D8).</summary>
public sealed class EventsFile(DataStore store, Action<string> log)
{
    public bool ReadOnly { get; private set; }
    public int SchemaVersion { get; private set; }

    public bool Exists()
    {
        try { return store.Files.Exists(DataFile.Events, FileVariant.Main); }
        catch (Exception) { return false; }
    }

    public (LoadResult Result, FileStamp? Stamp) Load(IUnitCatalog units)
    {
        store.CleanStaleTmp(DataFile.Events);
        byte[]? bytes;
        DateTime writeUtc;
        try
        {
            bytes = store.Files.Read(DataFile.Events, FileVariant.Main);
            writeUtc = bytes is null ? default : store.Files.WriteUtc(DataFile.Events, FileVariant.Main);
        }
        catch (Exception ex)
        {
            return (new LoadResult { FileError = $"events.json could not be read: {ex.Message}" }, null);
        }
        if (bytes is null) return (new LoadResult { FileError = "events.json not found" }, null);

        var result = EventValidator.Parse(Decode(bytes), units);
        var stamp = FileStamp.Of(writeUtc, bytes);
        if (result.FileError is not null) return (result, stamp);

        SchemaVersion = result.SchemaVersion;
        ReadOnly = false;
        switch (SchemaPolicy.Decide(result.SchemaVersion, EventValidator.CurrentSchemaVersion))
        {
            case SchemaMode.ReadOnly:
                ReadOnly = true;
                log($"events.json SchemaVersion {result.SchemaVersion} is newer than {EventValidator.CurrentSchemaVersion}: loaded read-only, never written");
                break;
            case SchemaMode.Migrate:
                log($"events.json SchemaVersion {result.SchemaVersion} migrated in memory; written on the next admin edit");
                break;
        }
        return (result, stamp);
    }

    /// <summary>Writes the seed when there is no events.json. Null on success.</summary>
    public string? Seed(byte[] content)
    {
        if (Exists()) return "events.json already exists";
        return store.WriteAtomicNew(DataFile.Events, content);
    }

    /// <summary>An admin edit (`event set`, `enable`, `disable`): refused while read-only or when the file changed
    /// since <paramref name="loaded"/>; otherwise written with one .bak. Null on success, with the new stamp.</summary>
    public string? WriteEdit(byte[] content, FileStamp? loaded, out FileStamp? written)
    {
        written = null;
        if (ReadOnly) return $"events.json SchemaVersion {SchemaVersion} is newer than this version of Nyarlathotep; it is read-only";
        var stale = StaleFile.CheckWritable(loaded, CurrentStamp());
        if (stale is not null) return stale;
        var error = store.WriteAtomic(DataFile.Events, content, keepBackup: true);
        if (error is not null) return $"events.json write failed: {error}";
        written = CurrentStamp();
        return null;
    }

    public FileStamp? CurrentStamp()
    {
        try
        {
            var bytes = store.Files.Read(DataFile.Events, FileVariant.Main);
            return bytes is null ? null : FileStamp.Of(store.Files.WriteUtc(DataFile.Events, FileVariant.Main), bytes);
        }
        catch (Exception) { return null; }
    }

    static string Decode(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }
}
