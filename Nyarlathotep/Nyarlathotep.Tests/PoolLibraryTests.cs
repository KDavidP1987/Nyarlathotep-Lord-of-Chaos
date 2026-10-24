using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>horde-pools D5, D6, D7: pools.json, seeded once with each allied group's units and bosses and never written
/// again, loading 1-64 pools under the event pool rules or none with one line and a health entry; an event names a pool
/// of its own first, then the library's, and keeps what it resolved while it runs; a counted pool entry rolls min..max.</summary>
public class PoolLibraryTests
{
    const string Thug = Apo.Thug, Deadeye = Apo.Deadeye, Zombie = Apo.Zombie, Tourok = Apo.Tourok;

    static readonly string[] Seeded =
    [
        "undead-units", "undead-bosses", "spiders-units", "spiders-bosses", "bandits-units", "militia-units", "militia-bosses",
        "church-units", "church-bosses", "legion-units", "legion-bosses", "gloomrot-units", "gloomrot-bosses", "cursed-units",
        "cursed-bosses", "blackfangs-units", "blackfangs-bosses",
    ];

    /// <summary>Every allied-group name, the default catalog's units and two test pools' members.</summary>
    internal static FakeUnits Units() =>
        new(FactionGroups.All.SelectMany(g => g.Names).Concat([Thug, Deadeye, Zombie, Tourok, "Boss_Known"]).Distinct().ToArray()) { Regions = FakeRegions.All() };

    static (MemoryFileStore Fs, DataStore Store, LogLines Log) Disk()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        return (fs, new DataStore(fs, log.Add), log);
    }

    static PoolLibrary Loaded(string text, FakeUnits? units = null)
    {
        var (fs, store, log) = Disk();
        fs.Put(DataFile.Pools, FileVariant.Main, text);
        var library = new PoolLibrary();
        library.Load(store, units ?? Units(), log.Add, log.Add);
        return library;
    }

    internal static string File(string pools) => "{ \"SchemaVersion\": 1, \"pools\": " + pools + " }";

    sealed class BrokenUnits : IUnitCatalog
    {
        public bool IsKnown(string prefabName) => throw new InvalidOperationException("catalog broke");
        public bool IsDenied(string prefabName) => throw new InvalidOperationException("catalog broke");
    }

    static string Names(params string[] names) => "[ " + string.Join(", ", names.Select(n => "\"" + n + "\"")) + " ]";

    /// <summary>A library of "elite" (Deadeye) and "mix" (a skeleton and a thug: undead and bandits).</summary>
    static PoolLibrary Library(string? pools = null) =>
        Loaded(File(pools ?? "{ \"elite\": " + Names(Deadeye) + ", \"mix\": " + Names("CHAR_Undead_Guardian", Thug) + " }"));

    static EventDefinition Parse(string ev, IPoolLibrary? library) =>
        Assert.Single(EventValidator.Parse(Json.File(ev), Units(), regions: FakeRegions.All(), library: library).Set.All);

    static string Waves(string units, string? pools = null, string? extra = null) =>
        "\"action\": { \"type\": \"SpawnWaves\"" + (pools is null ? "" : ", \"pools\": " + pools) + ", \"units\": " + units +
        ", \"waves\": 2, \"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }" + (extra is null ? "" : ", " + extra) + " }";

    static string PoolUnit(string pool, string count = "\"count\": 4") => "{ \"pool\": \"" + pool + "\", " + count + " }";

    static IRandom Seeded1(int seed) => new SystemRandom(new Random(seed));

    // ================= D5 File =================

    [Fact]
    public void File_fails_when_the_seed_or_a_load_breaks_the_file_rules()
    {
        // a missing file is seeded with the 17 pools, each member in its own group's half, no bandits-bosses
        var (fs, store, log) = Disk();
        var library = new PoolLibrary();
        Assert.Equal(17, library.Load(store, Units(), log.Add, log.Add));
        Assert.Equal(Seeded, library.List.Select(p => p.Name));
        Assert.All(library.List, p => Assert.True(p.Seeded, p.Name));
        foreach (var p in library.List)
        {
            var group = p.Name[..p.Name.LastIndexOf('-')];
            var bosses = p.Name.EndsWith(PoolLibrary.BossesSuffix, StringComparison.Ordinal);
            foreach (var m in p.Members)
            {
                Assert.Equal(group, Assert.Single(FactionGroups.Of(m)).Name);
                Assert.Equal(bosses, FactionGroups.Bosses.Contains(m));
            }
        }
        Assert.Contains("first run: seeded pools.json with 17 pools", log.Lines);
        Assert.Contains("pools: 17 loaded (17 seeded)", log.Lines);
        Assert.Empty(library.Entries);
        // the seed loads under the release rules (Review 2 F4): every pool 1-30 known members
        var (again, error) = PoolLibrary.Parse(fs.Text(DataFile.Pools, FileVariant.Main)!, Units());
        Assert.Null(error);
        Assert.Equal(17, again!.Count);

        // the bosses half is unit_index.tsv's V Blood flag, where Reference Data is here
        var index = Path.Combine(ControlCaseTests.RepoRoot(), "Reference Data", "unit_index.tsv");
        if (System.IO.File.Exists(index))
        {
            var rows = System.IO.File.ReadAllLines(index).Skip(1).Select(l => l.Split('\t')).ToDictionary(c => c[1], c => c[4] == "Y");
            foreach (var name in FactionGroups.All.SelectMany(g => g.Names))
                Assert.True(rows[name] == FactionGroups.Bosses.Contains(name), name);
        }
        Assert.All(FactionGroups.Bosses, b => Assert.Single(FactionGroups.Of(b)));

        // an existing file is never overwritten: a second load and a load of an edited file write nothing
        var edited = File("{ \"elite\": " + Names(Deadeye) + " }");
        fs.Put(DataFile.Pools, FileVariant.Main, edited);
        Assert.Equal(1, library.Load(store, Units(), log.Add, log.Add));
        Assert.Equal(edited, fs.Text(DataFile.Pools, FileVariant.Main));
        Assert.Equal("library", library.List.Single().Seeded ? "seeded" : "library");

        // an invalid file loads no pool, stays as it is, logs one line and opens the health entry
        string[] invalid =
        [
            "{ nope",
            File("{}"),
            File("{ \"elite\": [] }"),
            File("{ \"Elite\": " + Names(Deadeye) + " }"),
            File("{ \"elite\": " + Names(Deadeye, Deadeye) + " }"),
            File("{ \"elite\": " + Names("CHAR_Nobody_Here") + " }"),
            File("{ \"elite\": " + Names("CHAR_Mount_Horse_Vampire") + " }"),
            File("{ " + string.Join(", ", Enumerable.Range(1, 65).Select(i => $"\"p{i}\": " + Names(Deadeye))) + " }"),
            File("{ \"elite\": " + Names(Enumerable.Range(0, 31).Select(i => FactionGroups.All.SelectMany(g => g.Names).ElementAt(i)).ToArray()) + " }"),
            "{ \"SchemaVersion\": 1, \"pools\": { \"elite\": " + Names(Deadeye) + " }, \"extra\": 1 }",
        ];
        foreach (var text in invalid)
        {
            var (fs2, store2, log2) = Disk();
            fs2.Put(DataFile.Pools, FileVariant.Main, text);
            var bad = new PoolLibrary();
            Assert.Equal(0, bad.Load(store2, Units(), log2.Add, log2.Add));
            Assert.Empty(bad.LibraryPools);
            Assert.Equal(text, fs2.Text(DataFile.Pools, FileVariant.Main));
            Assert.Equal(1, log2.Count("pools.json not loaded: "));
            Assert.Equal([PoolLibrary.HealthEntry], bad.Entries);
        }

        // an unforeseen failure mid-load empties a library loaded before, so a reload never keeps stale pools (Codex F1)
        var (fs4, store4, log4) = Disk();
        var stale = new PoolLibrary();
        Assert.Equal(17, stale.Load(store4, Units(), log4.Add, log4.Add));
        Assert.Equal(0, stale.Load(store4, new BrokenUnits(), log4.Add, log4.Add));
        Assert.Empty(stale.LibraryPools);
        Assert.Empty(stale.List);
        Assert.Equal([PoolLibrary.HealthEntry], stale.Entries);
        Assert.Contains("pools.json not loaded: could not be loaded: catalog broke", log4.Lines);

        // a pool spanning two groups loads with its warning
        var (fs3, store3, log3) = Disk();
        fs3.Put(DataFile.Pools, FileVariant.Main, File("{ \"mix\": " + Names("CHAR_Undead_Guardian", Thug) + " }"));
        new PoolLibrary().Load(store3, Units(), log3.Add, log3.Add);
        Assert.Contains("pool mix mixes groups: undead, bandits", log3.Lines);
    }

    [Fact]
    public void File_passes_an_edited_library_loads_and_reloads_unchanged()
    {
        var library = Library();
        Assert.Equal(["elite", "mix"], library.List.Select(p => p.Name));
        Assert.All(library.List, p => Assert.False(p.Seeded));
        Assert.True(library.Find("mix")!.Mixes);
        Assert.Equal([Deadeye], library.LibraryPools["elite"]);
        Assert.Equal(Encoding.UTF8.GetString(PoolLibrary.Bytes(PoolLibrary.Seed(Units()))),
            Encoding.UTF8.GetString(PoolLibrary.Bytes(PoolLibrary.Parse(Encoding.UTF8.GetString(PoolLibrary.Bytes(PoolLibrary.Seed(Units()))), Units()).Pools!)));
    }

    [Fact]
    public void File_empty_a_member_unknown_here_is_left_out_of_the_seed()
    {
        // Review 2 F1: a seeded member this server does not know is dropped with a warning; its pool keeps the rest
        var units = Units();
        units.Denied.Add("CHAR_Spider_Queen_VBlood");
        var (fs, store, log) = Disk();
        var library = new PoolLibrary();
        Assert.Equal(16, library.Load(store, units, log.Add, log.Add));
        Assert.DoesNotContain("spiders-bosses", library.List.Select(p => p.Name));
        Assert.Contains("pools.json seed: spiders-bosses CHAR_Spider_Queen_VBlood left out (unknown or deny-listed here)", log.Lines);
        Assert.Null(library.Error);
    }

    // ================= D6 Resolve =================

    [Fact]
    public void Resolve_fails_when_a_library_pool_beats_an_own_pool_or_escapes_a_check()
    {
        var library = Library();
        // the event's own pool of the same name wins
        var own = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]", "{ \"elite\": " + Names(Thug) + " }")), library);
        Assert.True(own.Startable, own.DisabledReason);
        Assert.Equal([Thug], own.Action!.Pools!["elite"]);
        Assert.Null(own.Action.Linked);
        // and its checks read the own members: a roll of 2 fits the own pool of 2, not the library's pool of 1
        var ownRoll = Parse(Json.Event("w", action: Waves("[ { \"prefab\": \"" + Tourok + "\", \"count\": 2 } ]", "{ \"elite\": " + Names(Thug, Deadeye) + " }",
            extra: "\"bosses\": [ { \"pool\": \"elite\", \"min\": 2, \"max\": 2 } ]")), library);
        Assert.True(ownRoll.Startable, ownRoll.DisabledReason);
        Assert.Null(ownRoll.Action!.Linked);
        // a library-only name is accepted and kept with the definition, a name in neither is refused
        var linked = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]")), library);
        Assert.True(linked.Startable, linked.DisabledReason);
        Assert.Equal([Deadeye], linked.Action!.Pools!["elite"]);
        Assert.Equal(["elite"], linked.Action.Linked);
        Assert.Equal("action.units.pool: no pool ghosts", Parse(Json.Event("w", action: Waves("[ " + PoolUnit("ghosts") + " ]")), library).DisabledReason);
        Assert.Equal("action.units.pool: no pool elite", Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]")), null).DisabledReason);
        // a boss roll, an apocalypse's spread pool and a march's growth pool link the library too
        var rolled = Parse(Json.Event("w", action: Waves("[ { \"prefab\": \"" + Thug + "\", \"count\": 2 } ]", extra: "\"bosses\": [ { \"pool\": \"elite\", \"min\": 1, \"max\": 1 } ]")), library);
        Assert.True(rolled.Startable, rolled.DisabledReason);
        Assert.Equal(["elite"], rolled.Action!.Linked);
        var apo = Parse(Apo.Event(action: Apo.Action(roamers: "{ \"perHotspot\": 3, \"perKill\": 1, \"max\": 12, \"perMinute\": 4, \"huntRange\": 40, \"pool\": \"elite\" }")), library);
        Assert.True(apo.Startable, apo.DisabledReason);
        Assert.Equal(["elite"], apo.Apocalypse!.Linked);
        var march = Parse(Apo.Event(action: Inf.March(growth: Inf.Spread(pool: "elite"))), library);
        Assert.True(march.Startable, march.DisabledReason);
        Assert.Equal(["elite"], march.Apocalypse!.Linked);
        // the mixing check reads a library pool's members as it reads an own pool's
        var mixed = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("mix") + " ]")), library);
        Assert.Equal("w mixes the undead and bandits groups", FactionGroups.Problem("w", mixed.Action!));
        // a running event keeps what it resolved: a reload that changes elite does not change its draw
        var reloaded = Library("{ \"elite\": " + Names(Tourok) + " }");
        Assert.Equal([Tourok], Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]")), reloaded).Action!.Pools!["elite"]);
        Assert.All(Pools.Resolve(linked.Action, 1, Seeded1(3)).Units, u => Assert.Equal(Deadeye, u.Prefab));
        // a resumed apocalypse whose library pool vanished is disabled, so the resume refuses it (Review 2 F3)
        var gone = Parse(Apo.Event(action: Apo.Action(roamers: "{ \"perHotspot\": 3, \"perKill\": 1, \"max\": 12, \"perMinute\": 4, \"huntRange\": 40, \"pool\": \"elite\" }")),
            Library("{ \"other\": " + Names(Deadeye) + " }"));
        Assert.Equal(ResumeVerdict.Disabled, ResumeRule.Verdict(ApocalypseTests.Record(), new DefinitionSet([gone]), Zones.Utc(2026, 10, 31, 20, 5)));
    }

    [Fact]
    public void Resolve_passes_every_template_validates_and_draws_as_before()
    {
        var library = new PoolLibrary();
        var (_, store, log) = Disk();
        library.Load(store, TemplateLibraryTests.Units(), log.Add, log.Add);
        var plain = EventValidator.Parse(TemplateLibraryTests.RealText, TemplateLibraryTests.Units(), regions: FakeRegions.All()).Set.All;
        var linked = EventValidator.Parse(TemplateLibraryTests.RealText, TemplateLibraryTests.Units(), regions: FakeRegions.All(), library: library).Set.All;
        Assert.Equal(plain.Count, linked.Count);
        for (var i = 0; i < plain.Count; i++)
        {
            Assert.Equal(plain[i].DisabledReason, linked[i].DisabledReason);
            Assert.Null(linked[i].Action?.Linked ?? linked[i].Apocalypse?.Linked);
            if (plain[i].Action is not { } a) continue;
            for (var w = 1; w <= a.Waves; w++)
                Assert.Equal(Pools.Resolve(a, w, Seeded1(w)).Units, Pools.Resolve(linked[i].Action!, w, Seeded1(w)).Units);
        }
    }

    [Fact]
    public void Resolve_empty_no_library_leaves_the_events_as_they_were()
    {
        var d = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]", "{ \"elite\": " + Names(Thug) + " }")), new PoolLibrary());
        Assert.True(d.Startable);
        Assert.Null(d.Action!.Linked);
        Assert.Equal(["elite"], d.Action.Pools!.Keys);
    }

    // ================= D7 Count =================

    [Fact]
    public void Count_fails_when_a_range_is_refused_misread_or_drawn_outside_its_bounds()
    {
        var library = Library();
        static string Range(string pool, int min, int max) => PoolUnit(pool, $"\"min\": {min}, \"max\": {max}");
        // a roll stays in min..max and reaches both ends
        var d = Parse(Json.Event("w", action: Waves("[ " + Range("elite", 2, 5) + " ]")), library);
        Assert.True(d.Startable, d.DisabledReason);
        var seen = Enumerable.Range(0, 200).Select(s => Pools.Resolve(d.Action!, 1, Seeded1(s)).Units.Count).ToHashSet();
        Assert.Equal([2, 3, 4, 5], seen.OrderBy(n => n));
        // both forms, max below min, a range on a prefab entry and the bounds are refused
        Assert.Equal("action.units takes count or min and max, not both",
            Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite", "\"count\": 3, \"min\": 1, \"max\": 2") + " ]")), library).DisabledReason);
        Assert.Equal("action.units.max must be min-500", Parse(Json.Event("w", action: Waves("[ " + Range("elite", 4, 3) + " ]")), library).DisabledReason);
        Assert.Equal("action.units.min must be 1-500", Parse(Json.Event("w", action: Waves("[ " + Range("elite", 0, 3) + " ]")), library).DisabledReason);
        Assert.Equal("action.units.max must be min-500", Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite", "\"min\": 3") + " ]")), library).DisabledReason);
        Assert.Equal("action.units: min/max need a pool entry",
            Parse(Json.Event("w", action: Waves("[ { \"prefab\": \"" + Thug + "\", \"min\": 1, \"max\": 2 } ]")), library).DisabledReason);
        // SpawnWaves' wave size sums the maxes
        Assert.Equal("action.units must hold at most 500 units",
            Parse(Json.Event("w", action: Waves("[ " + Range("elite", 1, 500) + ", { \"prefab\": \"" + Thug + "\", \"count\": 1 } ]")), library).DisabledReason);
        // a horde march's maxes stay within 500 and its mins reach 10; its horde rolls each entry
        Assert.Equal(EventValidator.HordeUnitsRule, Parse(Apo.Event(action: Inf.March(units: "[ " + Range("elite", 5, 300) + ", " + Range("mix", 5, 201) + " ]")), library).DisabledReason);
        Assert.Equal(EventValidator.HordeUnitsRule, Parse(Apo.Event(action: Inf.March(units: "[ " + Range("elite", 4, 300) + ", " + Range("mix", 5, 200) + " ]")), library).DisabledReason);
        var march = Parse(Apo.Event(action: Inf.March(units: "[ " + Range("elite", 10, 20) + " ]")), library);
        Assert.True(march.Startable, march.DisabledReason);
        var sizes = Enumerable.Range(0, 100).Select(s => HordeSchedule.MarchUnits(march.Apocalypse!, [], Precedence.NoWaveCap, 0, 500, Seeded1(s), [], out _).Count).ToHashSet();
        Assert.True(sizes.Min() >= 10 && sizes.Max() <= 20 && sizes.Count > 3, string.Join(",", sizes));
        // a weighted list refuses min/max
        Assert.Equal(EventValidator.RangeWeighted("action.units.1"), Parse(Apo.Event(action: Apo.Action(units: "[ " + Range("elite", 1, 2) + " ]")), library).DisabledReason);
        Assert.Equal(EventValidator.RangeWeighted("action.units.1"),
            Parse(Apo.Event(action: Inf.Action().Replace(Apo.Unit() + ", ", Range("elite", 1, 2) + ", ")), library).DisabledReason);
        Assert.True(Encoding.UTF8.GetByteCount(EventValidator.RangeWeighted("action.waveList.10.units.10")) <= Wire.ReasonBytes);
        // the chat token and the info text
        var token = CommandArgs.SettableValue("action.units", "pool:elite:2-5");
        Assert.True(token.Ok, token.Error);
        var entry = Assert.Single((UnitEntry[])token.Value!);
        Assert.Equal((2, 5, "elite"), (entry.Min!.Value, entry.Count, entry.Pool!));
        Assert.False(CommandArgs.SettableValue("action.units", "pool:elite:5-2").Ok);
        Assert.False(CommandArgs.SettableValue("action.units", "pool:elite:0-2").Ok);
        Assert.Equal("pool elite ×2-5", EventLines.EntryText(entry));
    }

    [Fact]
    public void Count_passes_a_fixed_count_draws_as_before()
    {
        var d = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite") + " ]")), Library());
        Assert.All(Enumerable.Range(0, 20), s => Assert.Equal(4, Pools.Resolve(d.Action!, 1, Seeded1(s)).Units.Count));
        Assert.Equal(4, Pools.Roll(UnitEntry.FromPool("elite", 4), new StepRandom()));
    }

    [Fact]
    public void Count_empty_a_one_value_range_draws_exactly_it()
    {
        var d = Parse(Json.Event("w", action: Waves("[ " + PoolUnit("elite", "\"min\": 3, \"max\": 3") + " ]")), Library());
        Assert.True(d.Startable, d.DisabledReason);
        Assert.All(Enumerable.Range(0, 20), s => Assert.Equal(3, Pools.Resolve(d.Action!, 1, Seeded1(s)).Units.Count));
    }
}
