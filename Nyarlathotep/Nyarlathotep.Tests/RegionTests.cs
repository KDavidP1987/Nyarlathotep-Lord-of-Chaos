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
    public void A_point_answers_its_region(float x, float z, string region) =>
        Assert.Equal(region, Index().RegionOf(x, z));

    [Theory]
    [InlineData(float.NaN, 5f)]
    [InlineData(5f, float.NaN)]
    [InlineData(float.PositiveInfinity, 5f)]
    public void A_non_finite_point_answers_none(float x, float z) =>
        Assert.Equal(RegionNames.None, Index().RegionOf(x, z));

    [Fact]
    public void The_first_polygon_in_index_order_wins()
    {
        var a = Poly("StartCave", (0, 0), (10, 0), (10, 10), (0, 10));
        var b = Poly("FarbaneWoods", (5, 5), (15, 5), (15, 15), (5, 15));
        Assert.Equal("StartCave", RegionIndex.Build([a, b]).RegionOf(7, 7));
        Assert.Equal("FarbaneWoods", RegionIndex.Build([b, a]).RegionOf(7, 7));
    }

    [Fact]
    public void Degenerate_and_untagged_polygons_are_left_out_and_counted()
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
    public void An_empty_index_answers_none_everywhere()
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
}
