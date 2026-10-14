using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D15: `.nyar event info` of a definition with the block shows "scaling: …" and, while it runs, "scaling
/// now: …" from its latest decision (ScalingBook); a definition without the block shows no scaling line; the running line is
/// dropped at every end path; every line fits 480 bytes; the rest of `event info` is unchanged.</summary>
public partial class AuthoringTests
{
    static readonly DateTime ScT0 = Zones.Utc(2026, 10, 31, 20, 0);

    static string ScaledAction(string block) => Json.ValidAction[..^2] + ", \"scaling\": " + block + " }";

    static EventDefinition ScaledDef(string block, string id = "raid") => Json.One(Json.Event(id, action: ScaledAction(block)));

    /// <summary>The end paths of D15, each of which drops the instance's rows through ScalingBook.EventEnded; a restart
    /// clears the book.</summary>
    static readonly string[] ScalingEndPaths =
        ["natural end", "all waves defeated", "apocalypse won", "apocalypse lost", "apocalypse survived", "stop", "purge", "pillar off", "fault cancel"];

    [Fact]
    public void Scaling_fails_when_a_scaled_definition_lacks_the_line_or_a_value_is_wrong()
    {
        var full = EventLines.Info(ScaledDef("{ \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": -3, \"levelPerWave\": 1 }"), null, ScT0);
        Assert.Contains("scaling: +2 units a player beyond the first, level players' gear -3, +1 a wave", full);
        Assert.Contains("scaling: +3 units a player beyond the first, level own", EventLines.Info(ScaledDef("{ \"unitsPerPlayer\": 3 }"), null, ScT0));
        Assert.Contains("scaling: +0 units a player beyond the first, level players' gear +2, +0 a wave",
            EventLines.Info(ScaledDef("{ \"levelFromPlayers\": true, \"levelOffset\": 2 }"), null, ScT0));
        Assert.Contains("scaling: +0 units a player beyond the first, level players' gear +0, +0 a wave",
            EventLines.Info(ScaledDef("{ \"levelFromPlayers\": true }"), null, ScT0));
        Assert.Contains("scaling: +1 units a player beyond the first, level own",
            EventLines.Info(Apo.Def(Apo.Action(extra: "\"scaling\": { \"unitsPerPlayer\": 1 }")), null, ScT0));
        // the line sits after the action lines, before the trigger scope
        var at = full.ToList().FindIndex(l => l.StartsWith("scaling: ", StringComparison.Ordinal));
        Assert.Equal("trigger scope: Global", full[at + 1]);
    }

    [Fact]
    public void Scaling_fails_when_an_unscaled_definition_shows_one()
    {
        var def = Json.One(Json.Event("raid"));
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        Assert.DoesNotContain(EventLines.Info(def, null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.DoesNotContain(EventLines.Info(def, active, ScT0, scaling: "scaling now: 3 players, gear 40, level 42"),
            l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.DoesNotContain(EventLines.Info(Apo.Def(), null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
    }

    [Fact]
    public void Scaling_fails_when_the_running_line_outlives_an_end_path()
    {
        var def = ScaledDef("{ \"unitsPerPlayer\": 2 }");
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        foreach (var path in ScalingEndPaths)
        {
            var book = new ScalingBook();
            book.Record(active, new ScalingView(3, 40, null));
            Assert.True(book.RoamersChanged(active, 3));
            Assert.NotNull(book.Latest(active));
            book.EventEnded("raid");
            Assert.True(book.Latest(active) is null, path);
            Assert.Equal(0, book.Count);
        }
        var restart = new ScalingBook();
        restart.Record(active, new ScalingView(3, 40, null));
        restart.Clear();
        Assert.Null(restart.Latest(active));
        // a new instance never shows the last one's decision, and another event's end keeps this one's
        var kept = new ScalingBook();
        kept.Record(active, new ScalingView(3, 40, null));
        kept.EventEnded("other");
        Assert.NotNull(kept.Latest(active));
        var next = new ActiveEvent(new RunningInstance(def, ScT0.AddHours(1), ScT0.AddHours(1).AddSeconds(600)), "manual", null);
        Assert.Null(kept.Latest(next));
        Assert.Null(kept.Latest(null));
        // a stopped event shows no running line even when one is passed
        Assert.DoesNotContain(EventLines.Info(def, null, ScT0, scaling: "scaling now: 3 players, gear 40, level own"), l => l.StartsWith("scaling now", StringComparison.Ordinal));
    }

    [Fact]
    public void Scaling_fails_when_a_line_exceeds_480_bytes()
    {
        var id = new string('a', 32);
        var def = ScaledDef("{ \"unitsPerPlayer\": 5, \"levelFromPlayers\": true, \"levelOffset\": -20, \"levelPerWave\": 5 }", id);
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var lines = EventLines.Info(def, active, ScT0, scaling: Scaling.Lines.Now(new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue)));
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
        Assert.All(new[]
        {
            Scaling.Lines.Info(new Scaling(int.MaxValue, true, int.MinValue, int.MaxValue)),
            Scaling.Lines.Wave(id, int.MaxValue, new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue), int.MaxValue, int.MaxValue, Enumerable.Repeat(int.MaxValue, 10).ToArray()),
            Scaling.Lines.Horde(id, int.MaxValue, new ScalingView(int.MaxValue, int.MaxValue, int.MaxValue), int.MaxValue, int.MaxValue),
            Scaling.Lines.Roamers(id, int.MaxValue, int.MaxValue, int.MaxValue),
        }, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= Wire.MaxBytes, l));
    }

    [Fact]
    public void Scaling_fails_when_another_line_of_event_info_changes()
    {
        var plain = Json.One(Json.Event("raid"));
        var scaled = ScaledDef("{ \"unitsPerPlayer\": 2 }");
        Assert.Equal(EventLines.Info(plain, null, ScT0), EventLines.Info(scaled, null, ScT0).Where(l => !l.StartsWith("scaling", StringComparison.Ordinal)));
        var a = new ActiveEvent(new RunningInstance(plain, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var b = new ActiveEvent(new RunningInstance(scaled, ScT0, ScT0.AddSeconds(600)), "manual", null);
        Assert.Equal(EventLines.Info(plain, a, ScT0.AddSeconds(30)),
            EventLines.Info(scaled, b, ScT0.AddSeconds(30), scaling: "scaling now: 3 players, gear 40, level own").Where(l => !l.StartsWith("scaling", StringComparison.Ordinal)));
        Assert.Equal(EventLines.Info(plain, null, ScT0).Count + 1, EventLines.Info(scaled, null, ScT0).Count);
    }

    [Fact]
    public void Scaling_passes_running_line()
    {
        var def = ScaledDef("{ \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": -1 }");
        var active = new ActiveEvent(new RunningInstance(def, ScT0, ScT0.AddSeconds(600)), "manual", null);
        var book = new ScalingBook();
        book.Record(active, new ScalingView(2, 30, 29));
        book.Record(active, Scaling.ForWave(def.Action!, "raid", 1, new ScalingRead(3, 40, [])).View!.Value);     // the latest wins
        var lines = EventLines.Info(def, active, ScT0.AddSeconds(30), scaling: Scaling.Lines.Now(book.Latest(active)!.Value));
        Assert.Contains("scaling now: 3 players, gear 40, level 39", lines);
        Assert.Equal("scaling now: 0 players, gear none, level own", Scaling.Lines.Now(new ScalingView(0, null, null)));
        Assert.True(lines.ToList().IndexOf("scaling now: 3 players, gear 40, level 39") > lines.ToList().FindIndex(l => l.StartsWith("scaling: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Scaling_empty_not_running_no_line()
    {
        var def = ScaledDef("{ \"unitsPerPlayer\": 2 }");
        Assert.Single(EventLines.Info(def, null, ScT0), l => l.StartsWith("scaling", StringComparison.Ordinal));
        Assert.Null(new ScalingBook().Latest(null));
    }
}
