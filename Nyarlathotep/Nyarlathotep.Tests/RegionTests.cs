using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>regions D1 (point in region), D2's names and boot line, and the Scope value (D3).</summary>
public class RegionTests
{
    static RegionPolygon Poly(string region, params (float X, float Z)[] v) =>
        new(region, v.Min(p => p.X), v.Min(p => p.Z), v.Max(p => p.X), v.Max(p => p.Z), v);

    // A "U": the notch between x 3..7 above z 3 is outside.
    static readonly RegionPolygon U = Poly("FarbaneWoods", (0, 0), (10, 0), (10, 10), (7, 10), (7, 3), (3, 3), (3, 10), (0, 10));

    // A triangle whose box also covers its empty upper-right half.
    static readonly RegionPolygon Triangle = Poly("CursedForest", (20, 0), (30, 0), (20, 10));

    // A diamond with vertices at z 5, on the ray of a point at z 5.
    static readonly RegionPolygon Diamond = Poly("DunleyFarmlands", (40, 5), (45, 0), (50, 5), (45, 10));

    static RegionIndex Index() => RegionIndex.Build([U, Triangle, Diamond]);

    [Theory]
    [InlineData(1f, 5f, "FarbaneWoods")]
    [InlineData(5f, 1f, "FarbaneWoods")]
    [InlineData(8.5f, 9f, "FarbaneWoods")]
    [InlineData(5f, 6f, "None")]                                   // the notch
    [InlineData(21f, 1f, "CursedForest")]
    [InlineData(29f, 9f, "None")]                                  // in the triangle's box, not in the triangle
    [InlineData(41f, 5f, "DunleyFarmlands")]                        // the ray passes the vertex (50, 5)
    [InlineData(35f, 5f, "None")]                                  // the ray passes both z-5 vertices
    [InlineData(45f, 9.5f, "DunleyFarmlands")]
    [InlineData(-100f, -100f, "None")]
    [InlineData(1000f, 5f, "None")]
    public void Point_passes_each_shape(float x, float z, string region) =>
        Assert.Equal(region, Index().RegionOf(x, z));

    [Theory]
    [InlineData(float.NaN, 5f)]
    [InlineData(5f, float.NaN)]
    [InlineData(float.PositiveInfinity, 5f)]
    public void Point_fails_when_not_finite(float x, float z) =>
        Assert.Equal(RegionNames.None, Index().RegionOf(x, z));

    [Fact]
    public void Point_passes_first_polygon_in_index_order()
    {
        var a = Poly("StartCave", (0, 0), (10, 0), (10, 10), (0, 10));
        var b = Poly("FarbaneWoods", (5, 5), (15, 5), (15, 15), (5, 15));
        Assert.Equal("StartCave", RegionIndex.Build([a, b]).RegionOf(7, 7));
        Assert.Equal("FarbaneWoods", RegionIndex.Build([b, a]).RegionOf(7, 7));
    }

    [Fact]
    public void Point_fails_when_polygon_degenerate_or_untagged()
    {
        var two = Poly("CursedForest", (0, 0), (10, 10));
        var nan = new RegionPolygon("CursedForest", 0, 0, 10, 10, [(0, 0), (10, float.NaN), (0, 10)]);
        var badBox = new RegionPolygon("CursedForest", float.NegativeInfinity, 0, 10, 10, [(0, 0), (10, 0), (0, 10)]);
        var other = Poly("Other", (0, 0), (10, 0), (0, 10));
        var none = Poly("None", (0, 0), (10, 0), (0, 10));
        var index = RegionIndex.Build([two, nan, badBox, other, none, U]);
        Assert.Equal(1, index.PolygonCount);
        Assert.Equal(3, index.Dropped);
        Assert.Equal(2, index.Untagged);
        Assert.Equal(["FarbaneWoods"], index.Regions);
        Assert.False(index.OnMap("CursedForest"));
        Assert.Equal("FarbaneWoods", index.RegionOf(1, 1));
        Assert.Equal(RegionNames.None, index.RegionOf(5, 4));       // in U's notch, inside the left-out Other and None triangles
    }

    [Fact]
    public void Point_empty_index()
    {
        Assert.False(RegionIndex.Empty.Available);
        Assert.Equal(RegionNames.None, RegionIndex.Empty.RegionOf(0, 0));
        Assert.Empty(RegionIndex.Empty.Regions);
    }

    [Fact]
    public void Box_tests_run_per_polygon_and_polygon_tests_per_box_hit()
    {
        var index = Index();
        index.RegionOf(-100, -100);                                    // no box hit
        Assert.Equal(3, index.BoxTests);
        Assert.Equal(0, index.PolygonTests);
        index.RegionOf(29, 9);                                         // the triangle's box only
        Assert.Equal(6, index.BoxTests);
        Assert.Equal(1, index.PolygonTests);
        index.RegionOf(1, 5);                                          // the first polygon: stops there
        Assert.Equal(7, index.BoxTests);
        Assert.Equal(2, index.PolygonTests);
    }

    [Fact]
    public void Region_names_are_the_ten_map_regions_of_1_1_12()
    {
        Assert.Equal(10, RegionNames.Count);
        Assert.DoesNotContain("None", RegionNames.All);
        Assert.DoesNotContain("Other", RegionNames.All);
        Assert.Equal(RegionNames.All.Count, RegionNames.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)RegionNames.All)[0] = "Narnia");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)RegionNames.NotRegions)[0] = "StartCave");
    }

    [Fact]
    public void Names_differ_only_when_the_game_has_another_list()
    {
        string[] game = ["None", "Other", .. RegionNames.All];
        Assert.Empty(RegionNames.Differ(game));
        Assert.Equal(["+Oakveil", "-Strongblade"], RegionNames.Differ(game.Where(n => n != "Strongblade").Append("Oakveil")));
    }

    [Theory]
    [InlineData("cursedforest", "CursedForest")]
    [InlineData("GLOOMROT_SOUTH", "Gloomrot_South")]
    [InlineData("FarbaneWoods", "FarbaneWoods")]
    public void A_name_matches_case_insensitively_in_the_games_spelling(string typed, string canonical)
    {
        Assert.True(RegionNames.TryCanonical(typed, out var c));
        Assert.Equal(canonical, c);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Other")]
    [InlineData("Farbane Woods")]
    [InlineData("")]
    [InlineData(null)]
    public void A_name_outside_the_list_does_not_match(string? typed) => Assert.False(RegionNames.TryCanonical(typed, out _));

    [Theory]
    [InlineData("FarbaneWoods", "Farbane Woods")]
    [InlineData("Gloomrot_South", "Gloomrot South")]
    [InlineData("RuinsOfMortium", "Ruins Of Mortium")]
    [InlineData("StartCave", "Start Cave")]
    [InlineData("Strongblade", "Strongblade")]
    public void The_display_name_splits_at_capitals_and_underscores(string name, string display) =>
        Assert.Equal(display, RegionNames.Display(name));

    [Fact]
    public void Scope_defaults_to_global_and_compares_by_names()
    {
        Assert.True(default(Scope).IsGlobal);
        Assert.Equal(Scope.Global, new Scope([]));
        Assert.Equal("Global", Scope.Global.ToString());
        var s = new Scope(["CursedForest", "FarbaneWoods"]);
        Assert.Equal(new Scope(["CursedForest", "FarbaneWoods"]), s);
        Assert.NotEqual(new Scope(["FarbaneWoods", "CursedForest"]), s);
        Assert.Equal("CursedForest,FarbaneWoods", s.ToString());
        Assert.True(s.Names("CursedForest"));
        Assert.False(s.Names("StartCave"));
        Assert.False(Scope.Global.Names("CursedForest"));
    }

    [Fact]
    public void Scope_keeps_its_own_copy_of_the_names()
    {
        var names = new List<string> { "CursedForest" };
        var s = new Scope(names);
        var hash = s.GetHashCode();
        names.Add("FarbaneWoods");
        Assert.Equal(["CursedForest"], s.Regions);
        Assert.Equal(hash, s.GetHashCode());
        Assert.Equal(new Scope(["CursedForest"]), s);
        Assert.IsNotType<string[]>(s.Regions);                        // no cast back to a mutable array
        Assert.Throws<NotSupportedException>(() => ((IList<string>)s.Regions)[0] = "StartCave");
    }

    [Fact]
    public void The_boot_line_names_the_indexed_polygons_and_regions()
    {
        var index = RegionIndex.Build([U, Triangle, Diamond, Poly("Other", (0, 0), (1, 0), (0, 1)), Poly("StartCave", (0, 0), (1, 1))]);
        Assert.Equal("regions: 3 polygons, 3 regions (FarbaneWoods, DunleyFarmlands, CursedForest); 1 untagged, 1 dropped",
            RegionState.BootLine(index));
    }

    // ---- D8 Lines

    static DefinitionSet Set(params string[] events) =>
        EventValidator.Parse(Json.File(events), FakeUnits.Default(), regions: FakeRegions.All()).Set;

    [Fact]
    public void Lines_passes_list_counts_per_region_then_global()
    {
        var set = Set(
            Json.Event("a", "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\"] }"),
            Json.Event("b", "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\", \"Gloomrot_South\"] }", action:
                Json.ValidAction[..^2] + ", \"scope\": [\"FarbaneWoods\"] }"),
            Json.Event("off", "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\"] }").Replace("\"enabled\": true", "\"enabled\": false"),
            Json.Event("g1"),
            Json.Empower("g2"),
            Json.Empower("e", action: Json.EmpowerAction(extra: "\"scope\": [\"DunleyFarmlands\"]")));
        var lines = RegionLines.List(set);
        Assert.Equal(RegionNames.Count + 1, lines.Count);
        Assert.Contains("CursedForest (Cursed Forest): 2 events", lines);             // "off" is not counted
        Assert.Contains("FarbaneWoods (Farbane Woods): 1 events", lines);
        Assert.Contains("Gloomrot_South (Gloomrot South): 1 events", lines);
        Assert.Contains("DunleyFarmlands (Dunley Farmlands): 1 events", lines);
        Assert.Contains("Strongblade (Strongblade): 0 events", lines);
        Assert.Equal("global: 2 events", lines[^1]);
        Assert.Equal(RegionNames.All, lines.Take(RegionNames.Count).Select(l => l[..l.IndexOf(' ')]));
    }

    [Fact]
    public void Lines_empty_no_events()
    {
        var lines = RegionLines.List(Set());
        Assert.Equal("global: 0 events", lines[^1]);
        Assert.All(lines.Take(RegionNames.Count), l => Assert.EndsWith(": 0 events", l));
    }

    [Fact]
    public void Lines_passes_here_region_outside_or_unavailable()
    {
        Assert.Equal("you are in Gloomrot_South (Gloomrot South)", RegionLines.Here(true, (_, _) => "Gloomrot_South", 1, 2));
        Assert.Equal("you are outside every region", RegionLines.Here(true, (_, _) => RegionNames.None, 1, 2));
        Assert.Equal("you are outside every region", RegionLines.Here(true, (_, _) => "Other", 1, 2));
        Assert.Equal("regions unavailable", RegionLines.Here(false, (_, _) => "CursedForest", 1, 2));
        Assert.Equal("argument must be list or here", RegionLines.BadVerb);
    }

    [Fact]
    public void Lines_fails_when_definition_disabled()
    {
        var lines = RegionLines.List(Set(
            Json.Event("off", "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\"] }").Replace("\"enabled\": true", "\"enabled\": false"),
            Json.Event("g-off").Replace("\"enabled\": true", "\"enabled\": false")));
        Assert.Contains("CursedForest (Cursed Forest): 0 events", lines);
        Assert.Equal("global: 0 events", lines[^1]);
    }

    // ---- D15 Cost: a real-sized index (the polygon count of the boot line, Fixtures/region-index-size.txt), each
    // polygon a 256-vertex ring, so the point-in-polygon test is as long as a real region's outline.

    /// <summary>The polygon count read at boot; null when the fixture is missing or unreadable.</summary>
    internal static int? FixtureSize(string? text = null)
    {
        if (text is null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "region-index-size.txt");
            if (!File.Exists(path)) return null;
            text = File.ReadAllText(path);
        }
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
        return int.TryParse(line, out var n) ? n : null;
    }

    /// <summary>`count` rings of 256 vertices on a grid 1000 apart, radius 600, so neighbouring boxes overlap and a
    /// point can hit several boxes; the regions cycle through the game's names.</summary>
    static RegionIndex RealSized(int count) => RegionIndex.Build(Rings(count));

    static List<RegionPolygon> Rings(int count) => Enumerable.Range(0, count).Select(i =>
    {
        var (cx, cz) = (i % 4 * 1000f, i / 4 * 1000f);
        var ring = Enumerable.Range(0, 256).Select(k =>
        {
            var a = k * Math.PI * 2 / 256;
            var r = k % 2 == 0 ? 600.0 : 450.0;                                 // a star, so the outline is concave
            return (X: cx + (float)(r * Math.Cos(a)), Z: cz + (float)(r * Math.Sin(a)));
        }).ToArray();
        return Poly(RegionNames.All[i % RegionNames.Count], ring);
    }).ToList();

    [Fact]
    public void Cost_passes_10000_points_over_a_real_sized_index()
    {
        var size = FixtureSize();
        Assert.True(size is > 0, "Fixtures/region-index-size.txt is missing or holds 0 polygons");
        var rings = Rings(size!.Value);
        var index = RegionIndex.Build(rings);
        Assert.Equal(size.Value, index.PolygonCount);
        var rng = new Random(20260928);
        var points = Enumerable.Range(0, 10_000).Select(_ => ((float)(rng.NextDouble() * 4600 - 800), (float)(rng.NextDouble() * 4600 - 800))).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var (x, z) in points)
        {
            var (box, poly) = (index.BoxTests, index.PolygonTests);
            index.RegionOf(x, z);
            Assert.True(index.BoxTests - box <= index.PolygonCount, "a point costs more box tests than polygons");
            var hits = rings.Count(p => x >= p.MinX && x <= p.MaxX && z >= p.MinZ && z <= p.MaxZ);
            Assert.True(index.PolygonTests - poly <= hits, "a point costs more polygon tests than its box hits");
        }
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds <= 200, $"10,000 points took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Cost_passes_a_sweep_reads_one_position_per_unit()
    {
        var index = RealSized(FixtureSize() ?? 0);
        var scoped = new EmpowerAction(["Faction_Bandits"], [], [], false, new EmpowerStats(PhysicalPower: 1.3), new Scope(["CursedForest"]));
        var (reads, batch) = (0, Limits.EmpowerBatchPerTick.Default);
        for (var i = 0; i < batch; i++)
        {
            var (x, z) = (i * 17f % 3000, i * 29f % 3000);
            var box = index.BoxTests;
            Eligibility.Decide(Unit with { Region = () => { reads++; return index.RegionOf(x, z); } }, scoped);
            Assert.True(index.BoxTests - box <= index.PolygonCount);
        }
        Assert.Equal(batch, reads);
        Assert.True(index.BoxTests <= (long)batch * index.PolygonCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("# only a comment\n")]
    [InlineData("0\n")]
    [InlineData("ten\n")]
    public void Cost_fails_when_fixture_missing_or_zero(string? text)
    {
        var size = text is null ? FixtureSize("") : FixtureSize(text);
        Assert.False(size is > 0);
    }

    [Fact]
    public void Cost_empty_global_scope_reads_no_position()
    {
        var index = RealSized(FixtureSize() ?? 0);
        var global = new EmpowerAction(["Faction_Bandits"], [], [], false, new EmpowerStats(PhysicalPower: 1.3));
        var reads = 0;
        for (var i = 0; i < Limits.EmpowerBatchPerTick.Default; i++)
            Eligibility.Decide(Unit with { Region = () => { reads++; return index.RegionOf(0, 0); } }, global);
        Assert.Equal(0, reads);
        Assert.Equal(0, index.BoxTests);
    }

    static readonly UnitFacts Unit = new("CHAR_Bandit_Thug", "Faction_Bandits", IsPrefab: false, IsDead: false,
        HasVBloodUnit: false, IsOurs: false, OwnedByPlayer: false, CarrierOf: null);

    // ---- A34: `region here` and the players' positions never throw

    [Fact]
    public void Lines_fails_when_here_position_unreadable()
    {
        Assert.Equal(RegionLines.Unreadable, RegionLines.HereReply(true, () => null, (_, _) => "CursedForest"));
        Assert.Equal(RegionLines.Unreadable, RegionLines.HereReply(true, () => throw new InvalidOperationException("gone"), (_, _) => "CursedForest"));
        var reads = 0;
        Assert.Equal(RegionLines.Unavailable, RegionLines.HereReply(false, () => { reads++; return (1f, 2f); }, (_, _) => "CursedForest"));
        Assert.Equal(0, reads);                                                      // availability is checked first
        Assert.Equal("you are in CursedForest (Cursed Forest)", RegionLines.HereReply(true, () => (1f, 2f), (_, _) => "CursedForest"));
    }

    // ---- A34 structure: the production reads go through the guarded helpers (Codex step 2 round 2 F1, F2)

    static string Source(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", relative));
    }

    [Fact]
    public void Guarded_reads_route_through_the_helpers()
    {
        var runtime = Source(Path.Combine("Services", "EventRuntime.cs"));
        Assert.Contains("OnlinePositions() => PositionReader.Collect(ConnectedCharacters, CharacterPosition);", runtime);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(runtime, @"TryGetComponent<Translation>"));   // the one position read, in CharacterPosition
        var command = Source(Path.Combine("Commands", "RegionCommands.cs"));
        var here = command.IndexOf("RegionLines.HereReply(RegionMap.State.Available, () =>", StringComparison.Ordinal);
        Assert.True(here > 0, "`region here` does not call RegionLines.HereReply with availability first");
        var sender = command.IndexOf("SenderCharacterEntity", StringComparison.Ordinal);
        Assert.True(sender > here, "`region here` reads the sender outside HereReply's position lambda");
        Assert.Equal(sender, command.LastIndexOf("SenderCharacterEntity", StringComparison.Ordinal));
    }
}
