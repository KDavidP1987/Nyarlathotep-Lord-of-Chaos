using System.Reflection;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>horde-pools D11: the pool library's log lines and the `.nyar pool` replies carry pool names, unit names, event
/// ids and counts, never a player: with the planted admin running the commands and an event started by them at their
/// position, no line carries the planted name, id or position, and no builder takes a player-shaped parameter.</summary>
public partial class PrivacyTests
{
    static readonly string[] HordePoolsMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    /// <summary>The string parameters the pool builders may take: names an admin wrote in pools.json or events.json, a
    /// page word and a load or refusal reason. A player's name would arrive as a new one.</summary>
    static readonly string[] PoolStringParameters = ["error", "name", "pageWord", "refused", "text"];

    static List<string> HordePoolsLines()
    {
        var lines = new List<string>();
        var fs = new MemoryFileStore();
        var store = new DataStore(fs, lines.Add);
        var library = new PoolLibrary();
        library.Load(store, PoolLibraryTests.Units(), lines.Add, lines.Add);                // the seed
        var defs = EventValidator.Parse(Json.File(Json.Event("raid", action: "\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"pool\": \"undead-units\", " +
            "\"min\": 2, \"max\": 4 } ], \"waves\": 2, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Admin\" } }")),
            PoolLibraryTests.Units(), regions: FakeRegions.All(), library: library).Set.All;
        // the planted admin starts it at their position; the pool replies read the definitions, never the running instance
        var active = new ActiveEvent(new RunningInstance(defs[0], Now, Now.AddMinutes(10)), "manual", (4321.7f, 3f, 8765.3f));
        Assert.Equal("raid", active.Instance.Definition.Id);
        lines.AddRange(PoolLines.List(library, null));
        lines.AddRange(PoolLines.Info(library, "undead-units", null, defs));
        lines.Add(PoolLines.Reload(library.List.Count, library.Error, ["raid"]));
        fs.Put(DataFile.Pools, FileVariant.Main, PoolLibraryTests.File("{ \"mix\": [ \"CHAR_Undead_Guardian\", \"" + Apo.Thug + "\" ] }"));
        library.Load(store, PoolLibraryTests.Units(), lines.Add, lines.Add);                // a mixed pool
        fs.Put(DataFile.Pools, FileVariant.Main, "{ nope");
        library.Load(store, PoolLibraryTests.Units(), lines.Add, lines.Add);                // a broken file
        lines.Add(PoolLines.Reload(0, library.Error, []));
        return lines;
    }

    [Fact]
    public void HordePools_passes_pool_names_units_and_event_ids_only()
    {
        var lines = HordePoolsLines();
        Assert.Contains(lines, l => l.StartsWith("first run: seeded pools.json with 17 pools", StringComparison.Ordinal));
        Assert.Contains("named by: raid", lines);
        Assert.Contains("pool mix mixes groups: undead, bandits", lines);
        Assert.Contains(lines, l => l.StartsWith("pools.json not loaded: ", StringComparison.Ordinal));
        foreach (var line in lines)
            foreach (var mark in HordePoolsMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void HordePools_fails_when_a_name_id_or_position_reaches_a_line()
    {
        foreach (var line in HordePoolsLines())
            foreach (var mark in HordePoolsMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        // the builders take a library, pool and event names, counts and definitions, never a player or a position
        var parameters = new[] { typeof(PoolLines), typeof(PoolLibrary) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName).SelectMany(m => m.GetParameters()).ToList();
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(ulong) || p.ParameterType == typeof(ActiveEvent) || p.ParameterType == typeof(Location)
            || p.ParameterType == typeof((float, float, float)) || p.ParameterType == typeof(AdminCaller));
        Assert.Equal(PoolStringParameters, parameters.Where(p => p.ParameterType == typeof(string)).Select(p => p.Name!).Distinct().OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void HordePools_empty_no_library_no_pool_line()
    {
        var library = new PoolLibrary();
        Assert.Empty(library.Entries);
        Assert.Equal([PoolLines.NoPools], PoolLines.List(library, null));
        Assert.Equal([PoolLines.UnknownPool("elite")], PoolLines.Info(library, "elite", null, []));
    }
}
