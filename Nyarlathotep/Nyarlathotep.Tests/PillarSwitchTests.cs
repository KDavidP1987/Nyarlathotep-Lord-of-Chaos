using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D13 (the pillar name map against Config/Settings.cs), D14 (`.nyar pillar list` and
/// `.nyar pillar &lt;name&gt; on|off`, the reload first, the ordering against starts) and D19's cfg-save failures.</summary>
public class PillarSwitchTests
{
    static readonly DateTime Now = AuthoringTests.Now;

    /// <summary>Every `Bind("Pillars", "&lt;key&gt;"` of the real Config/Settings.cs, copied to the test output as text.</summary>
    static IReadOnlyList<string> BindKeys(string text) =>
        Regex.Matches(text, "Bind\\(\\s*\"Pillars\"\\s*,\\s*\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();

    static string SettingsText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "Settings.cs.txt"));

    static IReadOnlyList<(string Name, string Key)> Map => PillarNames.All.Select(x => (x.Name, x.Key)).ToList();

    // ---- D13 PillarMap

    [Fact]
    public void PillarMap_passes_settings_keys()
    {
        var keys = BindKeys(SettingsText);
        Assert.Equal(5, keys.Count);
        Assert.Empty(PillarNames.Problems(Map, keys));
        Assert.Equal(
            [("empowerment", "FactionEmpowerment"), ("spawns", "EventSpawns"), ("boss", "BossReinforcements"), ("zones", "DefendedZones"), ("sieges", "SiegeWaves")],
            Map);
    }

    [Theory]
    [InlineData("key without name", "[Pillars] Invasions has no chat name")]
    [InlineData("name without key", "pillar sieges maps to SiegeWaves, which Settings.cs does not bind")]
    [InlineData("shared key", "zones and sieges share [Pillars] DefendedZones")]
    public void PillarMap_fails_when_map_and_keys_differ(string plant, string problem)
    {
        var keys = BindKeys(SettingsText).ToList();
        var map = Map.ToList();
        switch (plant)
        {
            case "key without name": keys.Add("Invasions"); break;
            case "name without key": keys.Remove("SiegeWaves"); break;
            default: map[4] = ("sieges", "DefendedZones"); break;
        }
        Assert.Contains(problem, PillarNames.Problems(map, keys));
    }

    [Fact]
    public void PillarMap_empty_settings_text() =>
        Assert.Contains("Settings.cs binds no [Pillars] key", PillarNames.Problems(Map, BindKeys("")));

    // ---- D14 PillarCommand

    sealed class Rig
    {
        public FakePillarStore Store { get; } = new();
        public LogLines Log { get; } = new();
        public Library Lib { get; } = AuthoringTests.Lib(Json.Event("raid"), Json.Empower("surge"), Json.Event("raid-2"));
        public PillarCommand Command { get; }

        public Rig()
        {
            Command = new PillarCommand(Store, EndEvents, Log.Add);
        }

        /// <summary>The S-7 stop path: ends every running event of that pillar and names it.</summary>
        IReadOnlyList<string> EndEvents(Pillar pillar)
        {
            var ids = Lib.Catalog.Running.Where(r => r.Definition.Pillar == pillar).Select(r => r.Definition.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var id in ids) Lib.Catalog.TryEnd(id);
            return ids;
        }

        public ControlState Controls() =>
            new(false, Store.GeneralEnabled, PillarNames.All.Where(p => Store.Get(p.Pillar)).Select(p => p.Pillar).ToHashSet(), Lib.Catalog.Running.Count, 5);

        /// <summary>A start as EventRuntime makes it: the controls first, then the catalog.</summary>
        public string? Start(string id) =>
            (Precedence.StartBlocker(Lib.Catalog.Current.Find(id)!, Controls()) ?? Lib.Catalog.TryStart(id, Now, out _))?.Human;
    }

    [Fact]
    public void PillarCommand_passes_list()
    {
        var r = new Rig();
        r.Store.Reload();
        Assert.Equal(
            ["empowerment off (Pillars.FactionEmpowerment)", "spawns off (Pillars.EventSpawns)", "boss off (Pillars.BossReinforcements)",
             "zones off (Pillars.DefendedZones)", "sieges off (Pillars.SiegeWaves)"],
            r.Command.List());
        r.Store.GeneralEnabled = false;
        Assert.Equal("General.Enabled is off: nothing starts", r.Command.List()[0]);
        Assert.Equal(6, r.Command.List().Count);
    }

    [Fact]
    public void PillarCommand_passes_switch_on_and_off()
    {
        var r = new Rig();
        Assert.Equal(["pillar spawns on (saved to cfg)"], r.Command.Switch("spawns", "on").Human.Split('\n'));
        Assert.Equal(["reload", "set Spawns True"], r.Store.Ops);                      // reload before the set
        Assert.True(r.Store.Get(Pillar.Spawns));
        Assert.Contains("EventSpawns = true", r.Store.File);
        Assert.Equal(1, r.Store.Saves);

        r.Command.Switch("empowerment", "on");
        Assert.Null(r.Start("raid"));
        Assert.Null(r.Start("raid-2"));
        Assert.Null(r.Start("surge"));
        Assert.Equal(["pillar spawns off (saved to cfg)", "event raid ended (pillar off)", "event raid-2 ended (pillar off)"], r.Command.Switch("spawns", "off").Human.Split('\n'));
        Assert.Equal(["surge"], r.Lib.Catalog.Running.Select(x => x.Definition.Id));   // another pillar's event runs on
        Assert.Equal(1, r.Log.Count("event raid ended (pillar off)"));
    }

    [Fact]
    public void PillarCommand_passes_reload_keeps_hand_edit()
    {
        var r = new Rig();
        r.Store.Reload();
        var before = r.Store.File.ToList();
        r.Store.HandEdit("VerboseLogging", "true");                                   // saved to disk, not yet in memory
        r.Store.HandEdit("SiegeWaves", "true");
        r.Command.Switch("spawns", "on");
        Assert.Contains("VerboseLogging = true", r.Store.File);
        Assert.True(r.Store.Get(Pillar.Sieges));                                       // the reload read the hand edit
        var changed = r.Store.File.Where((l, i) => l != before[i]).ToList();
        Assert.Equal(["SiegeWaves = true", "EventSpawns = true", "VerboseLogging = true"], changed);
    }

    [Fact]
    public void PillarCommand_passes_ordering_start_before_off()
    {
        var r = new Rig();
        r.Command.Switch("spawns", "on");
        Assert.Null(r.Start("raid"));                                                  // dispatched first: runs
        Assert.Contains("event raid ended (pillar off)", r.Command.Switch("spawns", "off").Human.Split('\n'));
        Assert.Empty(r.Lib.Catalog.Running);
    }

    [Fact]
    public void PillarCommand_fails_when_off_processed_before_start()
    {
        var r = new Rig();
        r.Command.Switch("spawns", "on");
        r.Command.Switch("spawns", "off");
        Assert.Equal("pillar spawns is off", r.Start("raid"));
        Assert.Empty(r.Lib.Catalog.Running);
        Assert.Equal(Readiness.PillarOff, Readiness.Of(r.Lib.Catalog.Current.Find("raid")!, r.Controls()));
        Assert.StartsWith("raid off (pillar) spawns manual", EventLines.Line(r.Lib.Catalog.Current.Find("raid")!, false, r.Controls()));
    }

    [Theory]
    [InlineData("spawns", "off", "pillar spawns already off")]
    [InlineData("dusk", "on", "unknown pillar dusk; use empowerment, spawns, boss, zones or sieges")]
    [InlineData("spawns", "maybe", "use on or off")]
    public void PillarCommand_fails_when_no_change(string name, string state, string reply)
    {
        var r = new Rig();
        var file = r.Store.File.ToList();
        Assert.Equal([reply], r.Command.Switch(name, state).Human.Split('\n'));
        Assert.Equal(0, r.Store.Saves);
        Assert.Equal(file, r.Store.File);
        Assert.DoesNotContain(r.Store.Ops, o => o.StartsWith("set", StringComparison.Ordinal));
    }

    [Fact]
    public void PillarCommand_fails_when_hand_edit_already_set_it()
    {
        var r = new Rig();
        r.Store.HandEdit("EventSpawns", "true");
        Assert.Equal(["pillar spawns already on"], r.Command.Switch("spawns", "on").Human.Split('\n'));
        Assert.Equal(0, r.Store.Saves);
    }

    [Fact]
    public void PillarCommand_passes_off_after_hand_edit_ends_running()
    {
        var r = new Rig();
        r.Command.Switch("spawns", "on");
        Assert.Null(r.Start("raid"));
        r.Store.HandEdit("EventSpawns", "false");                                       // the operator turned it off on disk
        var saves = r.Store.Saves;
        Assert.Equal(["pillar spawns already off", "event raid ended (pillar off)"], r.Command.Switch("spawns", "off").Human.Split('\n'));
        Assert.Empty(r.Lib.Catalog.Running);
        Assert.Equal(saves, r.Store.Saves);                                              // nothing saved
    }

    [Fact]
    public void PillarCommand_fails_when_reload_throws()
    {
        var r = new Rig();
        r.Store.FailReload = true;
        var file = r.Store.File.ToList();
        Assert.Equal(["pillar spawns not changed: could not read the cfg: cfg locked"], r.Command.Switch("spawns", "on").Human.Split('\n'));
        Assert.Equal(["reload"], r.Store.Ops);
        Assert.Equal(file, r.Store.File);
        Assert.False(r.Store.Get(Pillar.Spawns));
    }

    [Fact]
    public void PillarCommand_empty_nothing_running()
    {
        var r = new Rig();
        r.Command.Switch("boss", "on");
        Assert.Equal(["pillar boss off (saved to cfg)"], r.Command.Switch("boss", "off").Human.Split('\n'));
    }

    // ---- D19 SaveFailure (the cfg-save category of D33)

    [Fact]
    public void SaveFailure_fails_when_save_truncates_then_throws()
    {
        var r = new Rig();
        r.Store.TruncateThenThrow = true;
        Assert.Equal(["pillar spawns could not be saved; the file says off"], r.Command.Switch("spawns", "on").Human.Split('\n'));
        Assert.False(r.Store.Get(Pillar.Spawns));                                      // memory follows the file
        Assert.Equal(1, r.Log.Count("pillar spawns: cfg save failed (disk full mid-save); the file says off"));
    }

    [Fact]
    public void SaveFailure_passes_line_written_then_throws()
    {
        var r = new Rig();
        r.Store.WriteThenThrow = true;
        Assert.Equal(["pillar spawns could not be saved; the file says on"], r.Command.Switch("spawns", "on").Human.Split('\n'));
        Assert.True(r.Store.Get(Pillar.Spawns));
        Assert.Contains("EventSpawns = true", r.Store.File);
    }

    [Fact]
    public void SaveFailure_passes_off_ends_events_when_file_says_off()
    {
        var r = new Rig();
        r.Command.Switch("spawns", "on");
        Assert.Null(r.Start("raid"));
        r.Store.WriteThenThrow = true;
        Assert.Equal(["pillar spawns could not be saved; the file says off", "event raid ended (pillar off)"], r.Command.Switch("spawns", "off").Human.Split('\n'));
        Assert.Empty(r.Lib.Catalog.Running);
    }

    [Theory]
    [InlineData("## Settings file\n[General]\nEnabled = true\n")]                     // the key is gone (a truncated save)
    [InlineData("[Pillars]\nEventSpawns = maybe\n")]                                   // a garbled value
    [InlineData("[General]\nEventSpawns = true\n")]                                    // the key under another section
    public void SaveFailure_fails_when_file_lacks_or_garbles_key(string cfg) =>
        Assert.False(PillarCfg.Read(cfg).ContainsKey("EventSpawns"));

    [Fact]
    public void SaveFailure_passes_file_values_read()
    {
        var read = PillarCfg.Read("[General]\r\nEnabled = true\r\n\r\n[Pillars]\r\n# Setting type: Boolean\r\nEventSpawns = true\r\nSiegeWaves = False\r\n[Debug]\r\nVerboseLogging = true\r\n");
        Assert.Equal(new Dictionary<string, bool> { ["EventSpawns"] = true, ["SiegeWaves"] = false }, read);
    }

    [Fact]
    public void SaveFailure_empty_file_after_truncate()
    {
        var r = new Rig();
        r.Command.Switch("spawns", "on");
        r.Store.TruncateThenThrow = true;
        Assert.Equal(["pillar empowerment could not be saved; the file says off"], r.Command.Switch("empowerment", "on").Human.Split('\n'));
        Assert.All(PillarNames.All, p => Assert.False(r.Store.Get(p.Pillar)));         // a missing key reads as its default, off
    }
}
