using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>An in-memory <see cref="IFileStore"/> with the disk's semantics and switchable faults.</summary>
sealed class MemoryFileStore : IFileStore
{
    readonly Dictionary<(DataFile, FileVariant), (byte[] Content, DateTime Utc)> _files = [];

    public DateTime Now { get; set; } = Zones.Utc(2026, 9, 24, 20, 0);
    public List<string> Ops { get; } = [];

    public bool FailWrites { get; set; }
    public bool FailPromote { get; set; }
    public bool FailReads { get; set; }
    public bool FailRename { get; set; }
    /// <summary>A write writes the first half of the bytes, then throws (a crash mid-write).</summary>
    public bool PartialWrites { get; set; }
    /// <summary>Each write advances <see cref="Now"/> by this much (a slow disk).</summary>
    public TimeSpan WriteDelay { get; set; }
    /// <summary>Runs between the .tmp write and the promote (another writer, such as a hand edit, interleaving).</summary>
    public Action? BeforePromote { get; set; }

    public IEnumerable<(DataFile File, FileVariant Variant)> Keys => _files.Keys;

    public void Put(DataFile file, FileVariant variant, string text) =>
        _files[(file, variant)] = (System.Text.Encoding.UTF8.GetBytes(text), Now);

    public string? Text(DataFile file, FileVariant variant) =>
        _files.TryGetValue((file, variant), out var f) ? System.Text.Encoding.UTF8.GetString(f.Content) : null;

    public bool Exists(DataFile file, FileVariant variant) => _files.ContainsKey((file, variant));

    public byte[]? Read(DataFile file, FileVariant variant)
    {
        if (FailReads) throw new IOException("read failed");
        return _files.TryGetValue((file, variant), out var f) ? f.Content : null;
    }

    public DateTime WriteUtc(DataFile file, FileVariant variant) =>
        _files.TryGetValue((file, variant), out var f) ? f.Utc : DateTime.MinValue;

    public void Write(DataFile file, FileVariant variant, byte[] content)
    {
        Ops.Add($"write {DataPaths.FileName(file, variant)}");
        Now += WriteDelay;
        if (FailWrites) throw new IOException("disk full");
        if (PartialWrites)
        {
            _files[(file, variant)] = (content[..(content.Length / 2)], Now);
            throw new IOException("crashed mid-write");
        }
        _files[(file, variant)] = (content, Now);
    }

    public void Promote(DataFile file, bool keepBackup)
    {
        Ops.Add($"promote {DataPaths.FileName(file, FileVariant.Main)}{(keepBackup ? " +bak" : "")}");
        BeforePromote?.Invoke();
        if (FailPromote) throw new IOException("replace failed");
        var tmp = _files[(file, FileVariant.Tmp)];
        if (keepBackup && _files.TryGetValue((file, FileVariant.Main), out var old)) _files[(file, FileVariant.Bak)] = old;
        _files[(file, FileVariant.Main)] = tmp;
        _files.Remove((file, FileVariant.Tmp));
    }

    public void PromoteNew(DataFile file)
    {
        Ops.Add($"promote-new {DataPaths.FileName(file, FileVariant.Main)}");
        BeforePromote?.Invoke();
        if (FailPromote) throw new IOException("replace failed");
        if (_files.ContainsKey((file, FileVariant.Main))) throw new IOException("the file exists");
        _files[(file, FileVariant.Main)] = _files[(file, FileVariant.Tmp)];
        _files.Remove((file, FileVariant.Tmp));
    }

    public void Rename(DataFile file, FileVariant from, FileVariant to)
    {
        Ops.Add($"rename {DataPaths.FileName(file, from)} {DataPaths.FileName(file, to)}");
        if (FailRename) throw new IOException("rename failed");
        _files[(file, to)] = _files[(file, from)];
        _files.Remove((file, from));
    }

    public void Delete(DataFile file, FileVariant variant)
    {
        Ops.Add($"delete {DataPaths.FileName(file, variant)}");
        _files.Remove((file, variant));
    }
}

sealed class FakeHooks(params Hook[] failing) : IHookRegistry
{
    public List<Hook> Registered { get; } = [];

    public void Register(Hook hook)
    {
        if (failing.Contains(hook)) throw new MissingMethodException($"{hook} system not found");
        Registered.Add(hook);
    }
}

sealed class FakeUsers : IUserSource
{
    public List<ulong> Online { get; } = [1, 2, 3];
    public bool FailList { get; set; }
    public HashSet<ulong> Unreachable { get; } = [];
    public List<(ulong Id, string Text)> Sent { get; } = [];

    public IReadOnlyList<ulong> Connected() => FailList ? throw new InvalidOperationException("user query failed") : Online;

    public void Send(ulong platformId, string text)
    {
        if (Unreachable.Contains(platformId)) throw new InvalidOperationException("user entity gone");
        Sent.Add((platformId, text));
    }
}

/// <summary>A log sink that records every line.</summary>
sealed class LogLines
{
    public List<string> Lines { get; } = [];
    public void Add(string line) => Lines.Add(line);
    public int Count(string fragment) => Lines.Count(l => l.Contains(fragment, StringComparison.Ordinal));
}
