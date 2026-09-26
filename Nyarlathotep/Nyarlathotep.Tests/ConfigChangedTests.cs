using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D6 (A7): `.nyar event reload`, `set`, `enable` and `disable` push config-changed exactly
/// when they apply a load, and never on a failure. Logic/DefinitionEditor is the flow Services/EventStore runs, here
/// over an in-memory events.json.</summary>
public partial class ConfigChangedTests
{
    const string ConfigChanged = "[NYAR:ev] type=config-changed id=- secs=0";

    sealed class Setup
    {
        public MemoryFileStore Fs { get; } = new();
        public LogLines Log { get; } = new();
        public EventCatalog Catalog { get; } = new();
        public PushHub Hub { get; }
        public EventsFile File { get; }
        public DefinitionEditor Editor { get; }

        public Setup(string events)
        {
            Fs.Put(DataFile.Events, FileVariant.Main, events);
            File = new EventsFile(new DataStore(Fs, Log.Add), Log.Add);
            Editor = new DefinitionEditor(File, Fs, Catalog, Log.Add, Log.Add);
            Assert.StartsWith("reloaded", Editor.Reload(FakeUnits.Default()));   // the boot load, before the hub is attached
            Hub = new PushHub(new FakeUsers(), [60], Log.Add);
            Catalog.Push = Hub;
        }

        public List<string> Pushed() => Hub.Queue.Lines.Select(l => l.Text).ToList();
    }

    static Setup New() => new(Json.File(Json.Event("raid")));

    [Fact]
    public void The_boot_load_pushes_nothing() => Assert.Empty(New().Pushed());

    [Fact]
    public void A_reload_pushes_config_changed()
    {
        var s = New();
        Assert.StartsWith("reloaded", s.Editor.Reload(FakeUnits.Default()));
        Assert.Equal([ConfigChanged], s.Pushed());
    }

    [Fact]
    public void A_reload_of_a_broken_or_unreadable_file_pushes_nothing()
    {
        var s = New();
        s.Fs.Put(DataFile.Events, FileVariant.Main, "{ broken");
        Assert.StartsWith("events.json rejected", s.Editor.Reload(FakeUnits.Default()));
        s.Fs.FailReads = true;
        Assert.StartsWith("events.json could not be read", s.Editor.Reload(FakeUnits.Default()));
        s.Fs.FailReads = false;
        s.Fs.Delete(DataFile.Events, FileVariant.Main);
        Assert.Equal("events.json not found", s.Editor.Reload(FakeUnits.Default()));
        Assert.Empty(s.Pushed());
    }

    [Theory]
    [InlineData("enabled", false)]
    [InlineData("enabled", true)]
    [InlineData("durationSeconds", 900)]
    [InlineData("name", "Ashfall raid")]
    public void An_applied_edit_pushes_config_changed_once(string path, object value)
    {
        var s = new Setup(Json.File(Json.Event("raid").Replace("\"enabled\": true", "\"enabled\": " + (value is true ? "false" : "true"))));
        Assert.StartsWith("event raid", s.Editor.Edit("raid", path, value, FakeUnits.Default()));
        Assert.Equal([ConfigChanged], s.Pushed());
    }

    [Fact]
    public void A_refused_edit_pushes_nothing()
    {
        var s = New();
        Assert.DoesNotContain("reloaded", s.Editor.Edit("nope", "enabled", true, FakeUnits.Default()));       // unknown id
        Assert.Equal("durationSeconds has an unsupported value",
            s.Editor.Edit("raid", "durationSeconds", new object(), FakeUnits.Default()));                     // a value it cannot write
        s.Fs.Put(DataFile.Events, FileVariant.Main, "{ broken");                                               // broken since the load
        Assert.StartsWith("events.json does not parse", s.Editor.Edit("raid", "enabled", false, FakeUnits.Default()));
        Assert.Empty(s.Pushed());
    }

    [Fact]
    public void An_edit_of_a_file_changed_since_its_load_pushes_nothing()
    {
        var s = New();
        s.Fs.Now = s.Fs.Now.AddMinutes(1);
        s.Fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("raid"), Json.Event("siege")));   // a hand edit
        var reply = s.Editor.Edit("raid", "enabled", false, FakeUnits.Default());
        Assert.DoesNotContain("reloaded", reply);
        Assert.Empty(s.Pushed());
        Assert.Contains("siege", s.Fs.Text(DataFile.Events, FileVariant.Main));   // the hand edit is kept
    }

    [Fact]
    public void An_edit_that_cannot_read_or_write_pushes_nothing()
    {
        var s = New();
        s.Fs.FailWrites = true;
        Assert.StartsWith("events.json write failed", s.Editor.Edit("raid", "enabled", false, FakeUnits.Default()));
        s.Fs.FailWrites = false;
        s.Fs.FailReads = true;
        Assert.StartsWith("events.json could not be read", s.Editor.Edit("raid", "enabled", false, FakeUnits.Default()));
        s.Fs.FailReads = false;
        s.Fs.Delete(DataFile.Events, FileVariant.Main);
        Assert.Equal("events.json not found", s.Editor.Edit("raid", "enabled", false, FakeUnits.Default()));
        Assert.Empty(s.Pushed());
    }

    [Fact]
    public void An_edit_of_a_newer_read_only_schema_pushes_nothing()
    {
        var s = new Setup("{ \"SchemaVersion\": 99, \"events\": [ " + Json.Event("raid") + " ] }");
        Assert.Contains("read-only", s.Editor.Edit("raid", "enabled", false, FakeUnits.Default()));
        Assert.Empty(s.Pushed());
    }
}
