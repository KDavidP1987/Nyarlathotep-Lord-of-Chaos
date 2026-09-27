using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D4 (`.nyar template list` and `info`) and D5 (`.nyar template use`), over the real catalogue,
/// the empty catalogue fixture and a 23-template paging fixture.</summary>
public class TemplateCommandTests
{
    static readonly DateTime Now = Zones.Utc(2026, 9, 26, 12, 0);
    static readonly string Empty = "{\"SchemaVersion\":1,\"events\":[]}";

    static TemplateCatalog Of(string text) => TemplateCatalog.Load(TemplateLibraryTests.Bytes(text), TemplateLibraryTests.Units(), TemplateLibraryTests.Units());

    /// <summary>22 spawns templates (s01..s22) and one empowerment template (e01), the empowerment one tenth in order.</summary>
    static TemplateCatalog TwentyThree()
    {
        var root = JsonNode.Parse(TemplateLibraryTests.RealText)!;
        var events = (JsonArray)root["events"]!;
        var spawn = events.OfType<JsonObject>().First(e => (string)e["id"]! == "bandit-ambush").ToJsonString();
        var empower = events.OfType<JsonObject>().First(e => (string)e["id"]! == "undead-nightfall").ToJsonString();
        var list = new JsonArray();
        var s = 0;
        for (var i = 0; i < 23; i++)
        {
            var o = JsonNode.Parse(i == 9 ? empower : spawn)!.AsObject();
            o["id"] = i == 9 ? "e01" : $"s{++s:00}";
            list.Add(o);
        }
        return Of(new JsonObject { ["SchemaVersion"] = 1, ["events"] = list }.ToJsonString());
    }

    static IReadOnlyList<string> List(TemplateCatalog c, params string[] words) => TemplateLines.List(c, words, []);

    static string[] Ids(IEnumerable<string> lines) => lines.Where(l => !l.StartsWith("page ", StringComparison.Ordinal)).Select(l => l.Split(' ')[0]).ToArray();

    // ---- D4 TemplateList

    [Fact]
    public void TemplateList_passes_real_catalogue()
    {
        var c = TemplateLibraryTests.Real();
        var lines = TemplateLines.List(c, [], ["bandit-ambush"]);
        Assert.Equal(6, lines.Count);
        Assert.Equal("legion-weekend-surge empowerment schedule Sat 20:00 \"Legion weekend surge\"", lines[0]);
        Assert.Equal("bandit-ambush spawns manual \"Bandit ambush\" (in events.json)", lines[4]);
        Assert.DoesNotContain(lines, l => l.Contains("[NYAR:", StringComparison.Ordinal));
        Assert.Equal(["bandit-ambush", "undead-rising"], Ids(List(c, "spawns")));
        Assert.Equal(["no templates for pillar zones"], List(c, "zones"));
        Assert.Equal(EventLines.Info(c.Find("bandit-ambush")!.Definition, null, Now), TemplateLines.Info(c, "bandit-ambush", Now));
    }

    [Fact]
    public void TemplateList_passes_invalid_template_with_reason()
    {
        var units = TemplateLibraryTests.Units();
        units.Factions.Remove("Faction_Legion");
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), units, units);
        Assert.Equal(5, c.ValidCount);
        Assert.EndsWith(" invalid: unknown faction Faction_Legion", List(c)[0]);
        Assert.Equal(["legion-weekend-surge disabled: unknown faction Faction_Legion"], TemplateLines.Info(c, "legion-weekend-surge", Now));
    }

    [Fact]
    public void TemplateList_passes_three_pages()
    {
        var c = TwentyThree();
        var p1 = List(c);
        var p2 = List(c, "2");
        var p3 = List(c, "3");
        Assert.Equal("page 1/3; .nyar template list 2 for more", p1[^1]);
        Assert.Equal("page 2/3; .nyar template list 3 for more", p2[^1]);
        Assert.Equal("page 3/3", p3[^1]);
        Assert.Equal(10, Ids(p1).Length);
        var all = Ids(p1).Concat(Ids(p2)).Concat(Ids(p3)).ToList();
        Assert.Equal(c.Templates.Select(t => t.Id), all);                              // each once, in catalogue order

        var f1 = List(c, "spawns");
        var f2 = List(c, "spawns", "2");
        var f3 = List(c, "spawns", "3");
        Assert.Equal("page 1/3; .nyar template list spawns 2 for more", f1[^1]);
        Assert.Equal("page 2/3; .nyar template list spawns 3 for more", f2[^1]);
        Assert.Equal("page 3/3", f3[^1]);
        var spawns = Ids(f1).Concat(Ids(f2)).Concat(Ids(f3)).ToList();
        Assert.Equal(c.Templates.Where(t => t.Definition.Pillar == Pillar.Spawns).Select(t => t.Id), spawns);
        Assert.Equal(["e01"], Ids(List(c, "empowerment")));
    }

    [Theory]
    [InlineData(new[] { "4" }, "no page 4; 3 pages")]
    [InlineData(new[] { "0" }, "page must be 1 or more")]
    [InlineData(new[] { "spawns", "0" }, "page must be 1 or more")]
    [InlineData(new[] { "spawns", "9" }, "no page 9; 3 pages")]
    [InlineData(new[] { "dusk" }, "unknown pillar dusk; use empowerment, spawns, boss, zones or sieges")]
    [InlineData(new[] { "2", "spawns" }, "usage: .nyar template list [pillar] [page]")]
    [InlineData(new[] { "spawns", "2", "x" }, "usage: .nyar template list [pillar] [page]")]
    public void TemplateList_fails_when_arguments_are_bad(string[] words, string reply) =>
        Assert.Equal([reply], List(TwentyThree(), words));

    [Fact]
    public void TemplateList_fails_when_catalogue_unavailable()
    {
        var c = TemplateCatalog.Unavailable("templates.json SchemaVersion 2 is not 1");
        Assert.Equal([TemplateLines.CatalogueUnavailable], List(c));
        Assert.Equal([TemplateLines.CatalogueUnavailable], TemplateLines.Info(c, "bandit-ambush", Now));
    }

    [Fact]
    public void TemplateList_fails_when_info_id_unknown() =>
        Assert.Equal(["unknown template nope; .nyar template list shows them"], TemplateLines.Info(TemplateLibraryTests.Real(), "nope", Now));

    [Fact]
    public void TemplateList_empty_catalogue()
    {
        var c = Of(Empty);
        Assert.Equal(["no templates"], List(c));
        Assert.Equal(["unknown template bandit-ambush; .nyar template list shows them"], TemplateLines.Info(c, "bandit-ambush", Now));
        var lib = new Library(Json.File(Json.Event("raid")), TemplateLibraryTests.Units());
        var hash = lib.Hash;
        Assert.Equal("unknown template bandit-ambush; .nyar template list shows them", lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null)));
        Assert.Equal(hash, lib.Hash);
    }

    // ---- D5 TemplateUse

    static Library Lib() => new(Json.File(Json.Event("raid")), TemplateLibraryTests.Units());

    static JsonObject Entry(Library lib, string id) =>
        ((JsonArray)JsonNode.Parse(lib.Text)!["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == id);

    [Fact]
    public void TemplateUse_passes_copy_disabled()
    {
        var lib = Lib();
        var c = TemplateLibraryTests.Real();
        Assert.Equal("template bandit-ambush added as bandit-ambush (disabled); .nyar event enable bandit-ambush to arm it",
            lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null)));
        var copy = Entry(lib, "bandit-ambush");
        Assert.False((bool)copy["enabled"]!);
        Assert.Equal(c.Find("bandit-ambush")!.Json.ToJsonString(), copy.ToJsonString());   // the template ships enabled:false
        Assert.False(lib.Catalog.Current.Find("bandit-ambush")!.Enabled);
        Assert.Null(lib.Catalog.Current.Find("bandit-ambush")!.DisabledReason);

        Assert.Equal("template bandit-ambush added as ambush-2 (disabled); .nyar event enable ambush-2 to arm it",
            lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", "ambush-2")));
        var named = Entry(lib, "ambush-2");
        named["id"] = "bandit-ambush";
        Assert.Equal(copy.ToJsonString(), named.ToJsonString());                                // only the id differs; the name is kept
    }

    [Fact]
    public void TemplateUse_passes_enabled_template_copied_disabled()
    {
        var lib = Lib();
        var root = JsonNode.Parse(TemplateLibraryTests.RealText)!;
        ((JsonArray)root["events"]!).OfType<JsonObject>().Single(e => (string)e["id"]! == "bandit-ambush")["enabled"] = true;
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes(root.ToJsonString()), TemplateLibraryTests.Units(), TemplateLibraryTests.Units());
        Assert.Null(c.Find("bandit-ambush")!.Invalid);                                          // a hand-built catalogue that ships it on
        lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null));
        Assert.False((bool)Entry(lib, "bandit-ambush")["enabled"]!);
        Assert.False(lib.Catalog.Current.Find("bandit-ambush")!.Enabled);
    }

    [Theory]
    [InlineData("nope", null, "unknown template nope; .nyar template list shows them")]
    [InlineData("bandit-ambush", "raid", "event raid already exists; use .nyar template use bandit-ambush as new-id")]
    [InlineData("bandit-ambush", "Bad_Id", "id must be 1-32 of a-z 0-9 -")]
    [InlineData("legion-weekend-surge", null, "template legion-weekend-surge is invalid: unknown faction Faction_Legion")]
    public void TemplateUse_fails_when_refused(string t, string? asId, string reply)
    {
        var lib = Lib();
        var units = TemplateLibraryTests.Units();
        units.Factions.Remove("Faction_Legion");
        var c = TemplateCatalog.Load(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), units, units);
        var (hash, bak, changed) = (lib.Hash, lib.Bak, lib.ConfigChanged);
        Assert.Equal(reply, lib.Write(text => Authoring.TemplateUse(text, c, t, asId)));
        Assert.Equal(hash, lib.Hash);
        Assert.Equal(bak, lib.Bak);
        Assert.Equal(changed, lib.ConfigChanged);
    }

    [Fact]
    public void TemplateUse_fails_when_used_twice()
    {
        var lib = Lib();
        var c = TemplateLibraryTests.Real();
        lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null));
        var hash = lib.Hash;
        var reply = lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null));
        Assert.Equal("event bandit-ambush already exists; use .nyar template use bandit-ambush as new-id", reply);
        Assert.DoesNotContain("<", reply);                                                  // chat drops <…> as a tag (A7, A11)
        Assert.Equal(hash, lib.Hash);
        Assert.Single(lib.Catalog.Current.All, d => d.Id == "bandit-ambush");
    }

    [Fact]
    public void TemplateUse_empty_catalogue_unavailable()
    {
        var lib = Lib();
        var hash = lib.Hash;
        Assert.Equal(TemplateLines.CatalogueUnavailable,
            lib.Write(t => Authoring.TemplateUse(t, TemplateCatalog.Unavailable("embedded templates.json missing"), "bandit-ambush", null)));
        Assert.Equal(hash, lib.Hash);
    }
}
