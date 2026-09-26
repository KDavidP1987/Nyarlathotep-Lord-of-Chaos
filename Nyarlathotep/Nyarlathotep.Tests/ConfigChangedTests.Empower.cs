using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D12: a stat set changes the file and pushes config-changed; a field of the other action
/// type, or a set that leaves no stat above 1.0, is refused with its reason and pushes nothing.</summary>
public partial class ConfigChangedTests
{
    static Setup Mixed() => new(Json.File(Json.Event("raid"), Json.Empower("surge")));

    [Fact]
    public void A_stat_set_changes_the_file_and_pushes_config_changed()
    {
        var s = Mixed();
        Assert.Equal("event surge action.stats.maxHealth = 1.5", s.Editor.Edit("surge", "action.stats.maxHealth", 1.5m, FakeUnits.Default()));
        Assert.Equal(new EmpowerStats(PhysicalPower: 1.3, MaxHealth: 1.5), s.Catalog.Current.Find("surge")!.Empower!.Stats);
        Assert.Contains("\"maxHealth\": 1.5", System.Text.Encoding.UTF8.GetString(s.Fs.Read(DataFile.Events, FileVariant.Main)!));
        Assert.Equal([ConfigChanged], s.Pushed());
    }

    [Theory]
    [InlineData("surge", "action.waves", 2, "action.waves is not a field of an Empower action")]
    [InlineData("surge", "action.intervalSeconds", 60, "action.intervalSeconds is not a field of an Empower action")]
    [InlineData("surge", "action.radius", 10, "action.radius is not a field of an Empower action")]
    [InlineData("raid", "action.stats.physicalPower", 1.5, "action.stats.physicalPower is not a field of a SpawnWaves action")]
    [InlineData("surge", "action.stats.physicalPower", 1.0, "action.stats must raise at least one stat above 1.0")]
    public void A_refused_stat_or_wave_set_changes_nothing_and_pushes_nothing(string id, string path, object value, string reason)
    {
        var s = Mixed();
        var before = s.Fs.Read(DataFile.Events, FileVariant.Main);
        if (value is double d) value = (decimal)d;
        Assert.Equal(reason, s.Editor.Edit(id, path, value, FakeUnits.Default()));
        Assert.Equal(before, s.Fs.Read(DataFile.Events, FileVariant.Main));
        Assert.Empty(s.Pushed());
    }
}
