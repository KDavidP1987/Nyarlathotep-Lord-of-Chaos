using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The early-departure diagnostic (2026-10-15, G-Portal test): an event's unit that leaves within 60 s of its
/// spawn is named with how it left, at most 20 lines a minute.</summary>
public class EarlyDepartureLogTests
{
    static readonly DateTime T0 = new(2026, 10, 15, 22, 0, 0, DateTimeKind.Utc);

    static TrackedUnit Unit(string? ev = "spider-swarm", int wave = 2, long key = 1) =>
        new(key, "CHAR_Spider_Melee", ev, T0, 1200, T0.AddSeconds(1200), wave);

    [Fact]
    public void A_unit_leaving_inside_the_window_is_named_with_how()
    {
        var log = new EarlyDepartureLog();
        Assert.Equal("early departure: CHAR_Spider_Melee of spider-swarm wave 2 after 4 s: died (killer CHAR_Spider_Range)",
            log.Line(Unit(), "died (killer CHAR_Spider_Range)", T0.AddSeconds(4)));
        Assert.Equal("early departure: CHAR_Spider_Melee of spider-swarm after 60 s: removed by the game",
            log.Line(Unit(wave: 0), "removed by the game", T0.AddSeconds(EarlyDepartureLog.WindowSeconds)));
    }

    [Fact]
    public void A_unit_past_the_window_or_of_no_event_is_not_named()
    {
        var log = new EarlyDepartureLog();
        Assert.Null(log.Line(Unit(), "died", T0.AddSeconds(EarlyDepartureLog.WindowSeconds + 1)));
        Assert.Null(log.Line(Unit(ev: null), "died", T0.AddSeconds(1)));
    }

    [Fact]
    public void At_most_twenty_lines_a_minute_and_the_next_counts_the_rest()
    {
        var log = new EarlyDepartureLog();
        var now = T0.AddSeconds(5);
        for (var i = 0; i < EarlyDepartureLog.PerMinute; i++) Assert.NotNull(log.Line(Unit(key: i), "died", now));
        Assert.Null(log.Line(Unit(key: 100), "died", now));
        Assert.Null(log.Line(Unit(key: 101), "died", now));
        var later = new TrackedUnit(200, "CHAR_Spider_Melee", "spider-swarm", now.AddSeconds(55), 1200, now.AddSeconds(1255), 3);
        Assert.Equal("early departure: CHAR_Spider_Melee of spider-swarm wave 3 after 5 s: died (2 more not logged)",
            log.Line(later, "died", now.AddSeconds(60)));
    }
}
