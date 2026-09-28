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
    /// <summary>The promote replaces the main file, then throws (event-library D19: an uncertain write).</summary>
    public bool ThrowAfterPromote { get; set; }
    /// <summary>Writes of these files fail, others succeed (event-library D19: a state.json write failing after the
    /// events.json write of a delete succeeded).</summary>
    public HashSet<DataFile> FailWritesOf { get; } = [];

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
        if (FailWrites || FailWritesOf.Contains(file)) throw new IOException("disk full");
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
        if (ThrowAfterPromote) throw new IOException("replaced, then the handle failed");
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

/// <summary>A cfg in memory for `.nyar pillar` (event-library D14, D19): <see cref="File"/> is the text on disk, one
/// "Key = true|false" line per [Pillars] key plus other lines, and the entries are what BepInEx holds in memory. Reload
/// reads the file into memory (a missing or unparsable key reads as off); Set writes memory and then only that key's
/// line, as SaveOnConfigSet does. Faults: a throwing reload, and a save that truncates, or writes the line, then throws.</summary>
sealed class FakePillarStore : IPillarStore
{
    public List<string> File { get; } =
    [
        "## Settings file was created by plugin Nyarlathotep",
        "[General]", "Enabled = true",
        "[Pillars]",
        "FactionEmpowerment = false", "SiegeWaves = false", "DefendedZones = false", "BossReinforcements = false", "EventSpawns = false",
        "[Debug]", "VerboseLogging = false",
    ];

    readonly Dictionary<Pillar, bool> _memory = Enum.GetValues<Pillar>().ToDictionary(p => p, _ => false);

    public bool GeneralEnabled { get; set; } = true;
    public bool FailReload { get; set; }
    /// <summary>Every reload after the first Set throws: a failed save whose read-back fails too.</summary>
    public bool FailReloadAfterSet { get; set; }
    public bool TruncateThenThrow { get; set; }
    public bool WriteThenThrow { get; set; }
    public int Saves { get; private set; }
    public int Reloads { get; private set; }
    public List<string> Ops { get; } = [];

    public bool Get(Pillar pillar) => _memory[pillar];

    public void Reload()
    {
        Ops.Add("reload");
        if (FailReload || (FailReloadAfterSet && Ops.Any(o => o.StartsWith("set ", StringComparison.Ordinal)))) throw new IOException("cfg locked");
        Reloads++;
        foreach (var (_, pillar, key) in PillarNames.All)
        {
            var line = File.FirstOrDefault(l => l.StartsWith(key + " = ", StringComparison.Ordinal));
            _memory[pillar] = line is not null && bool.TryParse(line[(key.Length + 3)..], out var v) && v;
        }
    }

    public void Set(Pillar pillar, bool on)
    {
        Ops.Add($"set {pillar} {on}");
        _memory[pillar] = on;
        if (TruncateThenThrow)
        {
            File.RemoveRange(1, File.Count - 1);
            throw new IOException("disk full mid-save");
        }
        var key = PillarNames.Key(pillar);
        var i = File.FindIndex(l => l.StartsWith(key + " = ", StringComparison.Ordinal));
        File[i] = $"{key} = {(on ? "true" : "false")}";
        if (WriteThenThrow) throw new IOException("save reported a failure after writing");
        Saves++;
    }

    /// <summary>An operator's hand edit saved to disk (not yet in memory).</summary>
    public void HandEdit(string key, string value)
    {
        var i = File.FindIndex(l => l.StartsWith(key + " = ", StringComparison.Ordinal));
        File[i] = $"{key} = {value}";
    }
}

/// <summary>events.json, its catalog and editor over a <see cref="MemoryFileStore"/>, loaded once (the boot load), for
/// the chat-write tests of event-library.</summary>
sealed class Library
{
    public MemoryFileStore Fs { get; } = new();
    public LogLines Log { get; } = new();
    public EventCatalog Catalog { get; } = new();
    public EventsFile Events { get; }
    public DefinitionEditor Editor { get; }
    public StateStore State { get; }
    public FakeUnits Units { get; }
    public PushHub Hub { get; }

    public Library(string events, FakeUnits? units = null)
    {
        Units = units ?? FakeUnits.Default();
        Fs.Put(DataFile.Events, FileVariant.Main, events);
        Events = new EventsFile(new DataStore(Fs, Log.Add), Log.Add);
        Editor = new DefinitionEditor(Events, Fs, Catalog, Log.Add, Log.Add);
        State = new StateStore(new DataStore(Fs, Log.Add), () => Fs.Now, Log.Add);
        Assert.StartsWith("reloaded", Editor.Reload(Units).Human);
        Hub = new PushHub(new FakeUsers(), [60], Log.Add);
        Catalog.Push = new CountingSink(this);
    }

    public static Library Of(params string[] events) => new(Json.File(events));

    public string Text => Fs.Text(DataFile.Events, FileVariant.Main)!;
    public string? Bak => Fs.Text(DataFile.Events, FileVariant.Bak);
    public string Hash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Text)));
    /// <summary>The config-changed notices raised (the hub's queue coalesces pending ones, so it is counted at the sink).</summary>
    public int ConfigChanged { get; private set; }

    sealed class CountingSink(Library lib) : IPushSink
    {
        public void EventStarted(RunningInstance instance) => lib.Hub.EventStarted(instance);
        public void EventEnded(string id) => lib.Hub.EventEnded(id);
        public void Wave(string id, int wave) => lib.Hub.Wave(id, wave);
        public void Purged(int cooldownSeconds) => lib.Hub.Purged(cooldownSeconds);
        public void ConfigChanged()
        {
            lib.ConfigChanged++;
            lib.Hub.ConfigChanged();
        }
    }

    public string Write(Func<string, EditPlan> plan) => Editor.Write(plan, Units).Human;

    /// <summary>The set a hand edit of <paramref name="text"/> plus `.nyar event reload` gives.</summary>
    public DefinitionSet HandEdit(string text)
    {
        var other = new Library(text, Units);
        return other.Catalog.Current;
    }
}

/// <summary>The game side of the admin flows over the in-memory stores (raphael-api-admin D1, D3): each member drives the
/// real Logic path Services/AdminOps reaches (EventEngine, DefinitionEditor, Authoring, EventDeleter, PillarCommand),
/// mapped as EventRuntime maps an admin's start, stop and purge. Calls lists every member called, Definitions aside.</summary>
sealed class FakeAdminOps : IAdminOps
{
    public Library Lib { get; }
    public EventEngine Engine { get; }
    public FakePillarStore Pillars { get; } = new();
    public PillarCommand PillarCommand { get; }
    public TemplateCatalog Templates { get; set; } = TemplateCatalog.NotLoaded;
    public DeleteArming DeleteArming { get; } = new();
    public DateTime? PurgeUntilUtc { get; set; }
    public int MaxConcurrent { get; set; } = 5;
    public int PurgeableUnits { get; set; }
    public int CooldownSeconds { get; set; } = 300;
    public List<string> Calls { get; } = [];

    /// <summary>A call whose record starts with this text throws (raphael-api-admin D12's failing op); null throws nothing.</summary>
    public string? ThrowOn { get; set; }

    /// <summary>A call whose record starts with this text throws after it changed state (raphael-api-admin A16: an
    /// announcement or carrier step failing after the event started); null throws nothing.</summary>
    public string? ThrowAfterOn { get; set; }

    void Call(string call)
    {
        Calls.Add(call);
        if (ThrowOn is { } prefix && call.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException($"fake {call} failed");
    }

    T After<T>(string call, T result) =>
        ThrowAfterOn is { } prefix && call.StartsWith(prefix, StringComparison.Ordinal)
            ? throw new InvalidOperationException($"fake {call} failed after it ran")
            : result;

    public FakeAdminOps(Library lib)
    {
        Lib = lib;
        Engine = new EventEngine(lib.Catalog) { Push = lib.Catalog.Push };      // start, stop and purge push as EventRuntime's engine does
        PillarCommand = new PillarCommand(Pillars, p => Engine.Active.Where(a => a.Definition.Pillar == p).Select(a => a.Id)
            .OrderBy(x => x, StringComparer.Ordinal).ToList().Where(id => Engine.Cancel(id) is not null).ToList(), lib.Log.Add,
            () => lib.Catalog.Push?.ConfigChanged());
    }

    public DateTime Now => Lib.Fs.Now;

    public ControlState Controls() => new(PurgeUntilUtc is { } until && until > Now, Pillars.GeneralEnabled,
        PillarNames.All.Where(p => Pillars.Get(p.Pillar)).Select(p => p.Pillar).ToHashSet(), Engine.Active.Count, MaxConcurrent);

    public DefinitionSet Definitions => Lib.Catalog.Current;

    /// <summary>The tracked units `api killswitch` reports.</summary>
    public int TrackedUnits { get; set; }

    public bool PillarOn(Pillar pillar) => Pillars.Get(pillar);

    public (DateTime? PurgeUntilUtc, int Events, int Units) KillSwitch => (PurgeUntilUtc, Engine.Active.Count, TrackedUnits);

    public Outcome OpStartEvent(string id, (float X, float Y, float Z)? origin)
    {
        Call($"start {id}");
        var refused = Engine.Start(id, "manual", Now, Controls(), origin);
        return After($"start {id}", refused is null ? AdminLines.Started(id) : AdminLines.StartRefused(refused, true, PurgeUntilUtc, Now));
    }

    public Outcome OpStopEvent(string id)
    {
        Call($"stop {id}");
        return Engine.Cancel(id) is null ? AdminLines.NotActive(id) : AdminLines.Stopped(id);
    }

    public Outcome OpPurge()
    {
        Call("purge");
        var ended = Engine.CancelAll(CooldownSeconds);
        var units = PurgeableUnits;
        PurgeableUnits = 0;
        PurgeUntilUtc = Now.AddSeconds(CooldownSeconds);
        return After("purge", AdminLines.PurgeDone(ended.Count, units, CooldownSeconds));
    }

    public (int Events, int Units) OpPurgeCounts()
    {
        Call("purge counts");
        return (Engine.Active.Count, PurgeableUnits);
    }

    public Outcome OpEdit(string id, string path, object value)
    {
        Call($"edit {id} {path}");
        return Lib.Editor.Edit(id, path, value, Lib.Units);
    }

    public Outcome OpReload()
    {
        Call("reload");
        return Lib.Editor.Reload(Lib.Units);
    }

    public Outcome OpAuthor(Func<string, EditPlan> plan)
    {
        Call("author");
        return Lib.Editor.Write(plan, Lib.Units);
    }

    public Outcome OpDelete(ulong adminId, string id, bool confirm)
    {
        Call(confirm ? $"delete {id} confirm" : $"delete {id}");
        var deleter = new EventDeleter(DeleteArming, Lib.Editor, Lib.Catalog, x => Engine.Find(x) is not null, Lib.State, Lib.Log.Add);
        return confirm ? deleter.Confirm(adminId, id, Now, Lib.Units) : deleter.Request(adminId, id, Now);
    }

    public Outcome OpUseTemplate(string template, string? asId)
    {
        Call($"template {template}");
        return Lib.Editor.Write(text => Authoring.TemplateUse(text, Templates, template, asId), Lib.Units);
    }

    public Outcome OpSetPillar(string name, string state)
    {
        Call($"pillar {name} {state}");
        return After($"pillar {name} {state}", PillarCommand.Switch(name, state));
    }
}
