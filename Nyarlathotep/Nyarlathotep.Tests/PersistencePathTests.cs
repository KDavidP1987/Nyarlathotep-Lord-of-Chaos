using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D7: Persistence resolves only the data files and their siblings under
/// BepInEx/config/Nyarlathotep/, and refuses rooted paths, .. segments and reparse points.</summary>
public class PersistencePathTests
{
    static readonly string Folder = Path.Combine(Path.GetTempPath(), "srv", "BepInEx", "config", DataPaths.FolderName);
    static bool NoReparse(string _) => false;

    [Fact]
    public void Exactly_the_four_data_files_and_their_siblings_resolve()
    {
        var expected = new[] { "events", "zones", "state", "stats" }
            .SelectMany(n => new[] { $"{n}.json", $"{n}.json.bak", $"{n}.json.tmp", $"{n}.json.corrupt" })
            .OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(expected, DataPaths.AllNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_enum_pair_resolves_inside_the_folder()
    {
        foreach (var f in Enum.GetValues<DataFile>())
            foreach (var v in Enum.GetValues<FileVariant>())
            {
                var path = Path.Combine(Folder, DataPaths.FileName(f, v));
                Assert.Null(DataPaths.Check(Folder, path, NoReparse));
                Assert.Equal(Folder, Path.GetDirectoryName(path));
            }
    }

    [Theory]
    [InlineData("other.json")]
    [InlineData("events.json.old")]
    [InlineData("EVENTS.JSON")]
    [InlineData("state.json.tmp.tmp")]
    [InlineData("")]
    public void Any_other_name_is_refused(string name) =>
        Assert.NotNull(DataPaths.Check(Folder, Path.Combine(Folder, name), NoReparse));

    [Fact]
    public void A_rooted_path_elsewhere_is_refused()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), "elsewhere", "events.json");
        Assert.Equal("path is outside the data folder", DataPaths.Check(Folder, elsewhere, NoReparse));
    }

    [Fact]
    public void A_nested_path_is_refused() =>
        Assert.Equal("path is outside the data folder", DataPaths.Check(Folder, Path.Combine(Folder, "sub", "events.json"), NoReparse));

    [Fact]
    public void A_dot_dot_segment_is_refused()
    {
        Assert.Equal("path has a .. segment", DataPaths.Check(Folder, Path.Combine(Folder, "..", "Nyarlathotep", "events.json"), NoReparse));
        Assert.Equal("path has a .. segment", DataPaths.Check(Path.Combine(Folder, "..", "Nyarlathotep"), Path.Combine(Folder, "events.json"), NoReparse));
    }

    [Fact]
    public void A_relative_folder_is_refused() =>
        Assert.Equal("data folder is not rooted", DataPaths.Check("Nyarlathotep", Path.Combine("Nyarlathotep", "events.json"), NoReparse));

    [Fact]
    public void A_reparse_point_folder_or_file_is_refused()
    {
        var path = Path.Combine(Folder, "state.json");
        Assert.Equal("data folder is a reparse point", DataPaths.Check(Folder, path, p => p == Folder));
        Assert.Equal("state.json is a reparse point", DataPaths.Check(Folder, path, p => p == path));
    }
}
