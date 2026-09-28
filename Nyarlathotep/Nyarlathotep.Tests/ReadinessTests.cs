using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D16: the readiness column of `.nyar event list` is StartBlocker's first blocker, mapped one to
/// one to a label, over all 64 combinations of the controls and the definition's four states.</summary>
public class ReadinessTests
{
    static readonly Pillar[] AllPillars = [Pillar.Empowerment, Pillar.Spawns, Pillar.Boss, Pillar.Zones, Pillar.Sieges];

    const string Reason = "unknown unit CHAR_Nobody";

    /// <summary>The definition in one of its four states: valid-enabled, valid-disabled, invalid-enabled, invalid-disabled.</summary>
    static EventDefinition Def(int state)
    {
        var d = Json.One(Json.Event("raid"));
        return d with { Enabled = state is 0 or 2, DisabledReason = state >= 2 ? Reason : null };
    }

    static ControlState Controls(bool purge, bool general, bool pillar, bool capFull) => new(
        purge, general, pillar ? AllPillars.ToHashSet() : AllPillars.Where(p => p != Pillar.Spawns).ToHashSet(), capFull ? 3 : 0, 3);

    public static IEnumerable<object[]> Combinations()
    {
        for (var mask = 0; mask < 16; mask++)
            for (var state = 0; state < 4; state++)
                yield return [mask, state];
    }

    /// <summary>The label this test expects, in the control order written out here (purge, General.Enabled, the pillar,
    /// the cap, the definition's reason, its flag), independently of Readiness.</summary>
    static string Expected(bool purge, bool general, bool pillar, bool capFull, int state) =>
        purge ? "off (purge)"
        : !general ? "off (mod)"
        : !pillar ? "off (pillar)"
        : capFull ? "full (cap)"
        : state >= 2 ? "invalid: " + Reason
        : state == 1 ? "off (event)"
        : "ready";

    // ---- D16 Readiness

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Readiness_passes_every_combination(int mask, int state)
    {
        bool On(int bit) => (mask & (1 << bit)) != 0;
        var (purge, general, pillar, capFull) = (On(0), !On(1), !On(2), On(3));
        var d = Def(state);
        var s = Controls(purge, general, pillar, capFull);
        var label = Readiness.Of(d, s);
        Assert.Equal(Readiness.Label(d, Precedence.StartBlocker(d, s)?.Human), label);
        Assert.Equal(Expected(purge, general, pillar, capFull, state), label);
        var line = EventLines.Line(d, false, s);
        Assert.Equal(label.StartsWith("invalid: ", StringComparison.Ordinal) ? $"raid {label}" : $"raid {label} spawns manual", line);
    }

    [Fact]
    public void Readiness_passes_combination_count() => Assert.Equal(64, Combinations().Count());

    [Fact]
    public void Readiness_passes_labels_one_to_one()
    {
        var replies = new[]
        {
            (Def(0), Controls(true, true, true, false)), (Def(0), Controls(false, false, true, false)),
            (Def(0), Controls(false, true, false, false)), (Def(0), Controls(false, true, true, true)),
            (Def(3), Controls(false, true, true, false)), (Def(1), Controls(false, true, true, false)),
            (Def(0), Controls(false, true, true, false)),
        }.Select(x => (Reply: Precedence.StartBlocker(x.Item1, x.Item2), Label: Readiness.Of(x.Item1, x.Item2))).ToList();
        Assert.Equal(7, replies.Select(r => r.Reply).Distinct().Count());
        Assert.Equal(7, replies.Select(r => r.Label).Distinct().Count());
    }

    [Theory]
    [InlineData("pillar boss is off")]
    [InlineData("something new")]
    public void Readiness_fails_when_blocker_has_no_label(string blocker) =>
        Assert.Throws<ArgumentException>(() => Readiness.Label(Def(0), blocker));

    [Fact]
    public void Readiness_fails_when_a_control_blocks()
    {
        Assert.Equal("off (pillar)", Readiness.Of(Def(2), Controls(false, true, false, false)));   // invalid, pillar off
        Assert.Equal("full (cap)", Readiness.Of(Def(0), Controls(false, true, true, true)));
        Assert.Equal("invalid: " + Reason, Readiness.Of(Def(3), Controls(false, true, true, false)));
        Assert.Equal("ready", Readiness.Of(Def(0), Controls(false, true, true, false)));
    }

    [Fact]
    public void Readiness_passes_list_master_line_and_running()
    {
        var set = AuthoringTests.Lib(Json.Event("raid")).Catalog.Current;
        var off = Controls(false, false, true, false);
        Assert.Equal(["General.Enabled is off: nothing starts", "page 1/1", "raid off (mod) spawns manual"], EventLines.List(set, 1, [], off));
        Assert.Equal(["page 1/1", "raid ready spawns manual RUNNING"], EventLines.List(set, 1, ["raid"], Controls(false, true, true, false)));
    }

    [Fact]
    public void Readiness_empty_no_events()
    {
        var set = AuthoringTests.Lib().Catalog.Current;
        Assert.Equal(["No events defined."], EventLines.List(set, 1, [], Controls(false, true, true, false)));
        Assert.Equal(["General.Enabled is off: nothing starts", "No events defined."], EventLines.List(set, 1, [], Controls(false, false, true, false)));
    }
}
