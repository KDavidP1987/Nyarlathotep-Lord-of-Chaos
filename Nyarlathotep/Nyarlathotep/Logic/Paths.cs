#nullable enable
using System.Collections.Generic;
using System.IO;

namespace Nyarlathotep.Logic;

/// <summary>The data files of BepInEx/config/Nyarlathotep/ (Design › Data). Persistence takes these enums,
/// never a name or a path, so no caller can spell another file (D7, D17 file-write fence).</summary>
public enum DataFile { Events, Zones, State, Stats }

/// <summary>A data file itself, or one of its siblings.</summary>
public enum FileVariant { Main, Bak, Tmp, Corrupt }

/// <summary>Constant file names and the one path check every Persistence access passes (D7).</summary>
public static class DataPaths
{
    public const string FolderName = "Nyarlathotep";

    public static string FileName(DataFile file, FileVariant variant)
    {
        var main = file switch
        {
            DataFile.Events => "events.json",
            DataFile.Zones => "zones.json",
            DataFile.State => "state.json",
            DataFile.Stats => "stats.json",
            _ => throw new ArgumentOutOfRangeException(nameof(file)),
        };
        return variant switch
        {
            FileVariant.Main => main,
            FileVariant.Bak => main + ".bak",
            FileVariant.Tmp => main + ".tmp",
            FileVariant.Corrupt => main + ".corrupt",
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
    }

    /// <summary>Every name Persistence may resolve: the four data files and their .bak, .tmp and .corrupt siblings.</summary>
    public static IReadOnlyCollection<string> AllNames { get; } = BuildAll();

    static HashSet<string> BuildAll()
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Enum.GetValues<DataFile>())
            foreach (var v in Enum.GetValues<FileVariant>())
                all.Add(FileName(f, v));
        return all;
    }

    /// <summary>Null when <paramref name="path"/> is one of <see cref="AllNames"/> directly inside
    /// <paramref name="folder"/> and neither is a reparse point (a junction or symbolic link could send a write
    /// elsewhere); otherwise the refusal.</summary>
    public static string? Check(string folder, string path, Func<string, bool> isReparsePoint)
    {
        if (!Path.IsPathRooted(folder)) return "data folder is not rooted";
        if (HasDotDot(folder) || HasDotDot(path)) return "path has a .. segment";
        var dir = Path.GetDirectoryName(path);
        if (dir is null || !string.Equals(Trim(dir), Trim(folder), StringComparison.OrdinalIgnoreCase))
            return "path is outside the data folder";
        var name = Path.GetFileName(path);
        if (!AllNames.Contains(name)) return $"{name} is not a data file";
        if (isReparsePoint(folder)) return "data folder is a reparse point";
        if (isReparsePoint(path)) return $"{name} is a reparse point";
        return null;
    }

    static string Trim(string p) => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static bool HasDotDot(string p)
    {
        foreach (var seg in p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (seg == "..") return true;
        return false;
    }
}
