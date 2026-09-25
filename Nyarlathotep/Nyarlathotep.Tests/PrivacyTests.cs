using System.Reflection;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D15: no player-facing builder takes a position or radius, every template uses only the allowed
/// placeholders, and with every source seeded with planted coordinates no emitted line carries one.</summary>
public class PrivacyTests
{
    const float PlantedCoord = 12345.6f;
    const int PlantedRadius = 987;
    static readonly string[] Planted = ["12345", "987"];

    static readonly DateTime Now = Zones.Utc(2026, 9, 24, 20, 0);

    /// <summary>A definition whose every position-bearing field holds the planted numbers, with its own
    /// announcement templates.</summary>
    static EventDefinition PlantedDefinition(string id = "raid") => new(
        id, "Bandit raid", true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 600,
        new SpawnWavesAction(
            [new UnitEntry("CHAR_Bandit_Thug", 5)], 3, 60, PlantedRadius,
            new Location(LocationType.Point, PlantedCoord, PlantedCoord), null),
        new Announce(["The {event} of the {faction}, wave {wave} of {waves} in {minutes} min at {zone}."], ["{event} ends."], true));

    static readonly RunningInstance[] Running =
    [
        new(PlantedDefinition("raid"), Now.AddMinutes(-2), Now.AddMinutes(8)),
        new(PlantedDefinition("siege") with { Announce = Announce.None }, Now, Now.AddSeconds(90)),
    ];

    // Tracked units are part of the builders' state; they carry the planted position too. No builder of this child
    // takes them, which the parameter check below would catch the moment one does.
    static readonly StateUnit[] Units = [new("raid", "CHAR_Bandit_Thug", PlantedCoord, PlantedCoord, Now)];

    /// <summary>Every public static member of Messages that produces text, run over the planted state. A new member
    /// without a case here fails <see cref="Every_builder_has_a_planted_case"/>.</summary>
    static readonly Dictionary<string, Func<IEnumerable<string>>> Cases = new()
    {
        [nameof(Messages.Overview)] = () => [Messages.Overview("0.2.0")],
        [nameof(Messages.CommandList)] = () => Messages.CommandList([".nyar", ".nyar status", ".nyar api version"]),
        [nameof(Messages.Status)] = () => Messages.Status(Running, Now).Concat(Messages.Status([], Now)),
        [nameof(Messages.Render)] = () => AllTemplates().Select(t => Messages.Render(t, MessageContext.For(PlantedDefinition(), 5, 2))),
        [nameof(Messages.StartBanner)] = () => Running.SelectMany(r => Enumerable.Range(0, 3).Select(p => Messages.StartBanner(r.Definition, 10, p))),
        [nameof(Messages.EndBanner)] = () => Running.SelectMany(r => Enumerable.Range(0, 3).Select(p => Messages.EndBanner(r.Definition, p))),
        [nameof(Messages.WaveWarning)] = () => Running.SelectMany(r => new[] { 300, 60, 10 }.Select(s => Messages.WaveWarning(r.Definition, 2, s, s))),
        [nameof(Messages.Pools)] = () => Messages.Pools().Values.SelectMany(p => p),
        [nameof(Messages.StillLoading)] = () => [Messages.StillLoading],
        [nameof(Messages.NoActiveEvents)] = () => [Messages.NoActiveEvents],
        [nameof(Messages.EventStartPool)] = () => Messages.EventStartPool,
        [nameof(Messages.EventEndPool)] = () => Messages.EventEndPool,
        [nameof(Messages.WaveWarningPool)] = () => Messages.WaveWarningPool,
        [nameof(Messages.DailyBannerPool)] = () => Messages.DailyBannerPool,
        [nameof(Messages.WaveImminentPool)] = () => Messages.WaveImminentPool,
        [nameof(Messages.DailyBannerMaxNames)] = () => [Messages.DailyBannerMaxNames.ToString()],
        [nameof(Messages.DailyBannerText)] = () => Enumerable.Range(0, 3).Select(p => Messages.DailyBannerText(Running.Select(r => r.Definition.Name).ToList(), p)),
    };

    static IEnumerable<string> Members() =>
        typeof(Messages).GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m is MethodInfo { IsSpecialName: false } || m is FieldInfo || m is PropertyInfo)
            .Select(m => m.Name)
            .Distinct();

    [Fact]
    public void Every_builder_has_a_planted_case()
    {
        var members = Members().ToList();
        Assert.NotEmpty(members);
        foreach (var m in members) Assert.True(Cases.ContainsKey(m), $"Messages.{m} has no planted-coordinate case");
    }

    [Fact]
    public void No_builder_takes_a_position_or_radius()
    {
        var methods = typeof(Messages).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => !m.IsSpecialName).ToList();
        Assert.NotEmpty(methods);
        foreach (var method in methods)
            foreach (var p in method.GetParameters())
            {
                var type = (Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType).Name.ToLowerInvariant();
                Assert.False(type is "single" or "double" or "decimal" || type.StartsWith("float") || type.StartsWith("vector"),
                    $"Messages.{method.Name}({p.Name}) takes a {p.ParameterType.Name}");
                Assert.False(Regex.IsMatch(p.Name ?? "", "pos|radius|coord", RegexOptions.IgnoreCase),
                    $"Messages.{method.Name} has a parameter named {p.Name}");
                Assert.NotEqual(typeof(StateUnit), p.ParameterType);
                Assert.NotEqual(typeof(Location), p.ParameterType);
            }
        // The context a template renders from has no position field at all.
        Assert.DoesNotContain(typeof(MessageContext).GetProperties(), p => p.PropertyType == typeof(float) || p.PropertyType == typeof(Location));
    }

    [Fact]
    public void No_emitted_line_carries_a_planted_coordinate()
    {
        Assert.NotEmpty(Units);
        var emitted = 0;
        foreach (var (name, run) in Cases)
            foreach (var line in run())
            {
                emitted++;
                foreach (var n in Planted) Assert.False(line.Contains(n), $"Messages.{name} emitted '{line}'");
            }
        Assert.True(emitted > Cases.Count);
    }

    // ---- templates: exactly three sources, each non-empty

    static IEnumerable<string> SeedTemplates()
    {
        var r = EventValidator.Parse(SeedTests.SeedText, FakeUnits.Default());
        return r.Set.All.SelectMany(d => d.Announce.Start.Concat(d.Announce.End));
    }

    static IEnumerable<string> LoadedTemplates()
    {
        var file = Json.File(Json.Event("loaded", extra:
            "\"announce\": { \"start\": [ \"The {faction} march: {event}, {minutes} min.\" ], \"end\": [ \"{event} over after wave {wave} of {waves}.\" ], \"warnings\": true }"));
        var r = EventValidator.Parse(file, FakeUnits.Default());
        return r.Set.All.SelectMany(d => d.Announce.Start.Concat(d.Announce.End));
    }

    static IEnumerable<string> AllTemplates() =>
        SeedTemplates().Concat(LoadedTemplates()).Concat(Messages.Pools().Values.SelectMany(p => p));

    public static TheoryData<string> Sources() => new() { "seed", "loaded", "pools" };

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_template_source_yields_templates_with_only_allowed_placeholders(string source)
    {
        var templates = (source switch
        {
            "seed" => SeedTemplates(),
            "loaded" => LoadedTemplates(),
            _ => Messages.Pools().Values.SelectMany(p => p),
        }).ToList();
        Assert.NotEmpty(templates);
        foreach (var t in templates) Assert.Null(EventValidator.UnknownPlaceholder(t));
    }

    [Fact]
    public void A_template_with_a_disallowed_placeholder_is_caught()
    {
        Assert.Equal("x", EventValidator.UnknownPlaceholder("at {x},{z}"));
        Assert.Equal("player", EventValidator.UnknownPlaceholder("{player} did it"));
    }
}
