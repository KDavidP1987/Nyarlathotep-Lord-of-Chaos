using System.IO;
using BepInEx;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// The only file writer (Epic D7, foundation D7/D8/D17). Every access names a <see cref="DataFile"/> and a
/// <see cref="FileVariant"/>, never a path, and resolves to a constant file name directly inside
/// BepInEx/config/Nyarlathotep/, checked by <see cref="DataPaths.Check"/> (no .. segment, no reparse point).
/// The write protocol itself (.tmp then promote, one .bak, stale .tmp cleanup, .corrupt rotation, schema
/// handling, one state write per second) is Logic/DataStore, tested over a fake store.
/// </summary>
internal sealed class Persistence : IFileStore
{
    static readonly string Folder = Path.Combine(Paths.ConfigPath, "Nyarlathotep");

    internal static Persistence Disk { get; } = new();
    internal static DataStore Store { get; private set; }
    internal static StateStore State { get; private set; }
    internal static EventsFile Events { get; private set; }

    /// <summary>First in Core.TryInitialize: loads state.json and prepares events.json access. Never throws.</summary>
    internal static void Initialize()
    {
        Store = new DataStore(Disk, Log);
        State = new StateStore(Store, () => DateTime.UtcNow, Log);
        Events = new EventsFile(Store, Log);
        State.Load();
    }

    /// <summary>Plugin.Unload: one final state write, ignoring the one-second interval.</summary>
    internal static void Shutdown()
    {
        try { State?.Flush(force: true); }
        catch (Exception ex) { Core.Log.LogError($"[nyar] state flush at shutdown failed: {ex.Message}"); }
    }

    static void Log(string line) => Core.Log.LogInfo($"[nyar] {line}");

    // ---- IFileStore: the disk primitives

    public bool Exists(DataFile file, FileVariant variant)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, variant));
        Guard(path);
        return File.Exists(path);
    }

    public byte[] Read(DataFile file, FileVariant variant)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, variant));
        Guard(path);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public DateTime WriteUtc(DataFile file, FileVariant variant)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, variant));
        Guard(path);
        return File.GetLastWriteTimeUtc(path);
    }

    public void Write(DataFile file, FileVariant variant, byte[] content)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, DataPaths.FileName(file, variant));
        Guard(path);
        File.WriteAllBytes(path, content);
    }

    public void Promote(DataFile file, bool keepBackup)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, FileVariant.Main));
        var tmp = Path.Combine(Folder, DataPaths.FileName(file, FileVariant.Tmp));
        var bak = Path.Combine(Folder, DataPaths.FileName(file, FileVariant.Bak));
        Guard(path);
        Guard(tmp);
        Guard(bak);
        if (!File.Exists(path)) File.Move(tmp, path);
        else if (keepBackup) File.Replace(tmp, path, bak);
        else File.Move(tmp, path, true);
    }

    public void PromoteNew(DataFile file)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, FileVariant.Main));
        var tmp = Path.Combine(Folder, DataPaths.FileName(file, FileVariant.Tmp));
        Guard(path);
        Guard(tmp);
        File.Move(tmp, path);
    }

    public void Rename(DataFile file, FileVariant from, FileVariant to)
    {
        var source = Path.Combine(Folder, DataPaths.FileName(file, from));
        var target = Path.Combine(Folder, DataPaths.FileName(file, to));
        Guard(source);
        Guard(target);
        File.Move(source, target, true);
    }

    public void Delete(DataFile file, FileVariant variant)
    {
        var path = Path.Combine(Folder, DataPaths.FileName(file, variant));
        Guard(path);
        File.Delete(path);
    }

    static void Guard(string path)
    {
        var refusal = DataPaths.Check(Folder, path, IsReparsePoint);
        if (refusal is not null) throw new IOException(refusal);
    }

    // A path whose attributes cannot be read counts as a reparse point, so the check fails closed.
    static bool IsReparsePoint(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception) { return true; }
    }
}
