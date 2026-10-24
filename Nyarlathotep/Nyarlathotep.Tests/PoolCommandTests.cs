using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>horde-pools D8: `.nyar pool list|info|reload`, admin-only, every reply within Wire.MaxBytes: the list with
/// "seeded" or "library", a pool's members paged with the events that name it and its "mixes groups" warning, and the
/// reload's count with every event invalid now.</summary>
public class PoolCommandTests
{
    static (MemoryFileStore Fs, DataStore Store, LogLines Log) Disk()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        return (fs, new DataStore(fs, log.Add), log);
    }

    static PoolLibrary Load(MemoryFileStore fs, DataStore store, LogLines log)
    {
        var library = new PoolLibrary();
        library.Load(store, PoolLibraryTests.Units(), log.Add, log.Add);
        return library;
    }

    static string Names(IEnumerable<string> names) => "[ " + string.Join(", ", names.Select(n => "\"" + n + "\"")) + " ]";

    static void Fits(IEnumerable<string> lines)
    {
        foreach (var line in lines) Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes, line);
    }

    static IReadOnlyList<EventDefinition> Events(IPoolLibrary library, params string[] events) =>
        EventValidator.Parse(Json.File(events), PoolLibraryTests.Units(), regions: FakeRegions.All(), library: library).Set.All;

    static string Naming(string id, string pool) => Json.Event(id, action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"pool\": \"" + pool +
        "\", \"count\": 3 } ], \"waves\": 2, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 } }");

    static string CommandSource() =>
        File.ReadAllText(Path.Combine(ControlCaseTests.RepoRoot(), "Nyarlathotep", "Nyarlathotep", "Commands", "PoolCommands.cs"));

    [Fact]
    public void PoolCommands_fails_when_a_reply_overflows_a_non_admin_is_answered_or_an_event_goes_unnamed()
    {
        // the widest library: 64 pools of 24-character names, each 30 long members
        var all = FactionGroups.All.SelectMany(g => g.Names).OrderByDescending(n => n.Length).Take(30).ToList();
        var (fs, store, log) = Disk();
        fs.Put(DataFile.Pools, FileVariant.Main, PoolLibraryTests.File("{ " + string.Join(", ", Enumerable.Range(1, 64).Select(i => $"\"{new string('p', 21)}{i:000}\": " + Names(all))) + " }"));
        var wide = Load(fs, store, log);
        Assert.Equal(64, wide.List.Count);
        var ids = Enumerable.Range(1, 40).Select(i => $"event-with-a-long-name-{i:00}").ToList();
        var naming = Events(wide, ids.Select(id => Naming(id, wide.List[0].Name)).ToArray());
        for (var page = 1; page <= 8; page++) Fits(PoolLines.List(wide, page.ToString()));
        for (var page = 1; page <= 3; page++) Fits(PoolLines.Info(wide, wide.List[0].Name, page.ToString(), naming));
        Fits([PoolLines.Reload(64, null, ids), PoolLines.Reload(0, new string('x', 400), ids)]);
        Assert.Contains(PoolLines.Info(wide, wide.List[0].Name, null, naming), l => l.Contains("event-with-a-long-name-40", StringComparison.Ordinal));

        // admin-only, through VCF's adminOnly flag
        Assert.Contains("adminOnly: true", CommandSource());

        // a reload that removes a pool names every event invalid now
        var (fs2, store2, log2) = Disk();
        fs2.Put(DataFile.Pools, FileVariant.Main, PoolLibraryTests.File("{ \"elite\": " + Names([Apo.Deadeye]) + " }"));
        var library = Load(fs2, store2, log2);
        Assert.All(Events(library, Naming("a", "elite"), Naming("b", "elite")), d => Assert.True(d.Startable, d.DisabledReason));
        fs2.Put(DataFile.Pools, FileVariant.Main, PoolLibraryTests.File("{ \"other\": " + Names([Apo.Deadeye]) + " }"));
        var loaded = library.Load(store2, PoolLibraryTests.Units(), log2.Add, log2.Add);
        var invalid = Events(library, Naming("a", "elite"), Naming("b", "elite")).Where(d => d.DisabledReason is not null).Select(d => d.Id).ToList();
        Assert.Equal("pools: 1 loaded; events invalid now: a, b", PoolLines.Reload(loaded, library.Error, invalid));
        Assert.Equal("pools: 1 loaded; events invalid now: none", PoolLines.Reload(1, null, []));
        Assert.StartsWith("pools.json not loaded: line 1", PoolLines.Reload(0, "line 1 position 3", ["a"]));
        // events.json refused: the old set stands unchecked, so no "none" is claimed for it (code review F1)
        Assert.Equal("pools: 1 loaded; events.json not reloaded, its events are unchecked: events.json is not valid JSON",
            PoolLines.Reload(1, null, [], "events.json is not valid JSON"));
        Fits([PoolLines.Reload(1, new string('x', 400), [], new string('y', 600))]);
    }

    [Fact]
    public void PoolCommands_passes_list_and_info_show_the_library()
    {
        var (fs, store, log) = Disk();
        var library = Load(fs, store, log);
        var list = PoolLines.List(library, null);
        Assert.Equal("undead-units: 22 units, seeded", list[0]);
        Assert.Equal("page 1/2; .nyar pool list 2 for more", list[^1]);
        Assert.Equal(7, PoolLines.List(library, "2").Count - 1);
        var defs = Events(library, Naming("raid", "undead-units"), Naming("other", "spiders-units"));
        var info = PoolLines.Info(library, "undead-units", null, defs);
        Assert.Equal("pool undead-units: 22 units, seeded", info[0]);
        Assert.Equal("named by: raid", info[1]);
        Assert.Equal("  CHAR_Undead_SkeletonSoldier_Armored_Farbane", info[2]);
        Assert.Equal("members page 1/3; .nyar pool info undead-units 2 for more", info[^1]);

        var (fs2, store2, log2) = Disk();
        fs2.Put(DataFile.Pools, FileVariant.Main, PoolLibraryTests.File("{ \"mix\": " + Names(["CHAR_Undead_Guardian", Apo.Thug]) + " }"));
        var mixed = Load(fs2, store2, log2);
        Assert.Equal(["mix: 2 units, library"], PoolLines.List(mixed, null));
        Assert.Equal(["pool mix: 2 units, library", "pool mix mixes groups: undead, bandits", "named by: no event", "  CHAR_Undead_Guardian", "  " + Apo.Thug],
            PoolLines.Info(mixed, "mix", null, []));
    }

    [Fact]
    public void PoolCommands_empty_no_library_and_unknown_names_answer_plainly()
    {
        var (fs, store, log) = Disk();
        fs.Put(DataFile.Pools, FileVariant.Main, "{ nope");
        var broken = Load(fs, store, log);
        Assert.Equal([PoolLines.Unknown], PoolLines.List(broken, null));
        Assert.Equal([PoolLines.Unknown], PoolLines.Info(broken, "elite", null, []));
        var (fs2, store2, log2) = Disk();
        var library = Load(fs2, store2, log2);
        Assert.Equal(["unknown pool ghosts; .nyar pool list shows them"], PoolLines.Info(library, "ghosts", null, []));
        Assert.Equal(["unknown pool; .nyar pool list shows them"], PoolLines.Info(library, "<b>X</b>", null, []));
        Assert.Equal([PoolLines.PageTooLow], PoolLines.List(library, "0"));
        Assert.Equal(["no page 9; 2 pages"], PoolLines.List(library, "9"));
    }
}
