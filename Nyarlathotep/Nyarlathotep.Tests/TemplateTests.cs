using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D2: the shipped Resources/events.default.json carries example-empowerment as a disabled
/// Empower definition on Faction_Bandits and its four other templates unchanged.</summary>
public class TemplateTests
{
    static LoadResult Seed() => EventValidator.Parse(SeedTests.SeedText, new FakeUnits(
        "CHAR_Bandit_Thug", "CHAR_Bandit_Deadeye", "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Bandit_Tourok_VBlood"));

    [Fact]
    public void The_empowerment_template_is_a_disabled_Empower_on_bandits()
    {
        var d = Seed().Set.Find("example-empowerment")!;
        Assert.Null(d.DisabledReason);
        Assert.False(d.Enabled);
        Assert.Equal(Pillar.Empowerment, d.Pillar);
        Assert.Equal(TriggerType.VBloodKilled, d.Trigger.Type);
        Assert.Equal(["any"], d.Trigger.Bosses);
        Assert.Equal(600, d.DurationSeconds);
        Assert.Null(d.Action);
        var e = d.Empower!;
        Assert.Equal(["Faction_Bandits"], e.Factions);
        Assert.Empty(e.IncludeUnits);
        Assert.Empty(e.ExcludeUnits);
        Assert.False(e.IncludeVBloods);
        Assert.Equal(new EmpowerStats(1.3, 1.3, 1.5, 1.15, 1.1), e.Stats);
    }

    [Fact]
    public void The_other_four_templates_are_SpawnWaves_and_disabled()
    {
        var others = Seed().Set.All.Where(d => d.Id != "example-empowerment").ToList();
        Assert.Equal(["example-boss", "example-sieges", "example-spawns", "example-zones"], others.Select(d => d.Id));
        Assert.All(others, d =>
        {
            Assert.Null(d.DisabledReason);
            Assert.False(d.Enabled);
            Assert.Equal(EventActionKind.Waves, EventActions.ActionKindOf(d));
        });
    }

    [Fact]
    public void An_enabled_empowerment_template_would_be_caught()
    {
        var text = SeedTests.SeedText;
        const string off = "\"enabled\": false";
        var at = text.IndexOf(off, text.IndexOf("\"example-empowerment\"", StringComparison.Ordinal), StringComparison.Ordinal);
        var enabled = text[..at] + "\"enabled\": true" + text[(at + off.Length)..];
        Assert.NotEqual(SeedTests.SeedText, enabled);
        var d = EventValidator.Parse(enabled, FakeUnits.Default()).Set.Find("example-empowerment")!;
        Assert.True(d.Enabled);                                  // what the shipped-template test above refuses
    }
}
