using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>regions D9: the scope in `.nyar event info` and `list`, and `event set trigger.scope|action.scope` through
/// the existing editor (A28).</summary>
public class EventAdminTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 9, 28, 20, 0);

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    static LoadResult Load(string text) => EventValidator.Parse(text, FakeUnits.Default(), regions: FakeRegions.All());

    static string Set(string text, string id, string field, string value)
    {
        var v = CommandArgs.SettableValue(field, value);
        Assert.Null(v.Error);
        var edited = EventsEditor.Apply(text, id, field, v.Value!, out var error);
        Assert.Null(error);
        return edited!;
    }

    const string Cursed = "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\"] }";

    [Fact]
    public void Scope_passes_info_shows_both_scopes()
    {
        var d = Load(Json.File(Json.Event("raid", Cursed, action: Json.ValidAction[..^2] + ", \"scope\": [\"FarbaneWoods\", \"Gloomrot_South\"] }"))).Set.Find("raid")!;
        var lines = EventLines.Info(d, null, T0);
        Assert.Contains("trigger scope: CursedForest", lines);
        Assert.Contains("action scope: FarbaneWoods,Gloomrot_South", lines);
        var global = EventLines.Info(Load(Json.File(Json.Event("g"))).Set.Find("g")!, null, T0);
        Assert.Contains("trigger scope: Global", global);
        Assert.Contains("action scope: Global", global);
        var empower = EventLines.Info(Load(Json.File(Json.Empower("e", action: Json.EmpowerAction(extra: "\"scope\": [\"DunleyFarmlands\"]")))).Set.Find("e")!, null, T0);
        Assert.Contains("action scope: DunleyFarmlands", empower);
    }

    [Fact]
    public void Scope_passes_list_suffix_regional_only()
    {
        var set = Load(Json.File(
            Json.Event("a", Cursed, action: Json.ValidAction[..^2] + ", \"scope\": [\"FarbaneWoods\", \"CursedForest\"] }"),
            Json.Event("g"))).Set;
        Assert.EndsWith(" [CursedForest,FarbaneWoods]", EventLines.Line(set.Find("a")!, false, Open()));
        Assert.EndsWith(" [CursedForest,FarbaneWoods] RUNNING", EventLines.Line(set.Find("a")!, true, Open()));
        Assert.DoesNotContain("[", EventLines.Line(set.Find("g")!, false, Open()));
        Assert.Equal("", EventLines.RegionSuffix(set.Find("g")!));
    }

    [Theory]
    [InlineData("trigger.scope")]
    [InlineData("action.scope")]
    public void Scope_passes_set_writes_games_spelling(string field)
    {
        var text = Set(Json.File(Json.Event("raid")), "raid", field, "farbanewoods");
        var d = Load(text).Set.Find("raid")!;
        Assert.Null(d.DisabledReason);
        var scope = field == "trigger.scope" ? d.Trigger.Scope : d.Action!.Scope;
        Assert.Equal(["FarbaneWoods"], scope.Regions);
        d = Load(Set(text, "raid", field, "global")).Set.Find("raid")!;
        Assert.True((field == "trigger.scope" ? d.Trigger.Scope : d.Action!.Scope).IsGlobal);
    }

    [Theory]
    [InlineData("Schedule")]
    [InlineData("GameTime")]
    [InlineData("VBloodKilled")]
    public void Scope_passes_set_every_trigger_type(string type)
    {
        var text = Set(Json.File(Json.Event("raid")), "raid", "trigger.type", type);
        var d = Load(Set(text, "raid", "trigger.scope", "CursedForest")).Set.Find("raid")!;
        Assert.Null(d.DisabledReason);
        Assert.Equal(["CursedForest"], d.Trigger.Scope.Regions);
    }

    [Theory]
    [InlineData("Narnia", "unknown region Narnia")]
    [InlineData("None", "unknown region None")]
    [InlineData("Other", "unknown region Other")]
    [InlineData("CursedForest,,FarbaneWoods", "trigger.scope must be Global or 1-10 region names")]
    [InlineData("CursedForest,cursedforest", "trigger.scope lists CursedForest twice; nothing written")]
    public void Scope_fails_when_set_value_reload_rejects(string value, string reply) =>
        Assert.Equal(reply, CommandArgs.SettableValue("trigger.scope", value).Error);

    [Fact]
    public void Scope_empty_value() =>
        Assert.Equal("trigger.scope must be Global or 1-10 region names", CommandArgs.SettableValue("trigger.scope", "").Error);

    [Fact]
    public void Scope_fails_when_set_more_names_than_regions()
    {
        var eleven = string.Join(",", RegionNames.All.Append("CursedForest"));
        Assert.Equal($"action.scope must be Global or 1-{RegionNames.Count} region names", CommandArgs.SettableValue("action.scope", eleven).Error);
        Assert.True(CommandArgs.SettableValue("action.scope", string.Join(",", RegionNames.All)).Ok);
    }

    [Fact]
    public void Scope_fails_when_trigger_type_changes()
    {
        var text = Set(Json.File(Json.Event("raid", Cursed)), "raid", "trigger.type", "GameTime");
        var d = Load(text).Set.Find("raid")!;
        Assert.True(d.Trigger.Scope.IsGlobal);
        Assert.DoesNotContain("scope", text);
    }

    [Fact]
    public void Scope_passes_edit_leaves_running_instance()
    {
        var catalog = new EventCatalog();
        var text = Json.File(Json.Event("raid"));
        Assert.Null(catalog.Reload(Load(text), FileStamp.Of(T0, [1])));
        var engine = new EventEngine(catalog);
        Assert.Null(engine.Start("raid", "manual", T0, Open()));
        Assert.Null(catalog.Reload(Load(Set(text, "raid", "trigger.scope", "CursedForest")), FileStamp.Of(T0, [2])));
        Assert.True(engine.Find("raid")!.Definition.Trigger.Scope.IsGlobal);      // the next start reads the new scope
        Assert.Equal(["CursedForest"], catalog.Current.Find("raid")!.Trigger.Scope.Regions);
    }
}
