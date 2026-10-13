using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D31: `.nyar event info` shows "final wave adds: &lt;prefab&gt; x&lt;count&gt;, …" within 480 bytes; an event
/// without bosses shows 0.11.0's lines.</summary>
public partial class AuthoringTests
{
    [Fact]
    public void Bosses_passes_the_final_wave_line()
    {
        var lines = EventLines.Info(HgEvent(HgPoint,
            "\"bosses\": [ { \"prefab\": \"CHAR_Bandit_Tourok_VBlood\", \"count\": 2 }, { \"prefab\": \"CHAR_Bandit_Deadeye\", \"count\": 1 } ]"), null, Now);
        Assert.Contains("final wave adds: CHAR_Bandit_Tourok_VBlood x2, CHAR_Bandit_Deadeye x1", lines);
        Assert.DoesNotContain(EventLines.Info(HgEvent(HgPoint, "\"loot\": false"), null, Now), l => l.StartsWith("final wave adds", StringComparison.Ordinal));
    }

    [Fact]
    public void Bosses_fails_when_the_line_exceeds_480_bytes()
    {
        var name = "CHAR_" + new string('V', CommandArgs.MaxNameLength - 5);
        var a = new SpawnWavesAction([new UnitEntry("CHAR_Bandit_Thug", 1)], 1, 60, 10, new Location(LocationType.Point, 0, 0), null,
            Bosses: [new UnitEntry(name, 3), new UnitEntry(name, 3), new UnitEntry(name, 3)]);
        var line = EventLines.BossesLine(a)!;
        Assert.True(Encoding.UTF8.GetByteCount(line) <= Wire.MaxBytes, line);
        Assert.Null(EventLines.BossesLine(a with { Bosses = null }));
    }
}
