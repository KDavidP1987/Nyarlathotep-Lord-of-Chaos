using System.Text;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>revenge D31: `.nyar event set &lt;id&gt; action.bosses &lt;prefab[:count],…|none&gt;` takes exactly what the
/// validator takes (D28) and refuses with its reasons before anything is written; none removes the key; every reply fits
/// 480 bytes; the §6 table lists the field.</summary>
public partial class CommandArgTests
{
    const string BossTourok = "CHAR_Bandit_Tourok_VBlood";

    static Library BossLib() => new(Json.File(
        Json.Event("raid"),
        Json.Event("fanned", action: HgJson("{ \"type\": \"AroundPlayer\", \"minDist\": 10, \"maxDist\": 20 }",
            "\"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }")),
        Json.Event("bossed", action: HgJson(HgPoint, "\"bosses\": [ { \"prefab\": \"" + BossTourok + "\", \"count\": 1, \"modifiers\": { \"level\": 60 } } ]"))),
        AuthoringTests.Units());

    [Theory]
    [InlineData("CHAR_Bandit_Tourok_VBlood:0", EventValidator.BossCountRule)]
    [InlineData("CHAR_Bandit_Tourok_VBlood:4", EventValidator.BossCountRule)]
    [InlineData("CHAR_Bandit_Tourok_VBlood:x", EventValidator.BossCountRule)]
    [InlineData("CHAR_A,CHAR_B,CHAR_C,CHAR_D", EventValidator.BossesRule)]
    [InlineData("CHAR_A,,CHAR_B", EventValidator.BossesRule)]
    public void Bosses_fails_when_the_field_accepts_a_value_the_validator_refuses(string value, string reason)
    {
        var lib = BossLib();
        var hash = lib.Hash;
        var reply = AuthoringTests.Set(lib, "raid", CommandArgs.BossesField, value);
        Assert.Equal(reason, reply);
        Assert.Equal(hash, lib.Hash);
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes);
    }

    [Fact]
    public void Bosses_fails_when_a_name_or_fanOut_is_let_through()
    {
        var lib = BossLib();
        var hash = lib.Hash;
        Assert.Equal(CommandArgs.NameRule(CommandArgs.BossesField), AuthoringTests.Set(lib, "raid", CommandArgs.BossesField, "Bandit_Tourok"));
        var fanned = AuthoringTests.Set(lib, "fanned", CommandArgs.BossesField, BossTourok);
        Assert.StartsWith(EventValidator.BossesFanOut, fanned);
        var other = AuthoringTests.Set(lib, "bossed", CommandArgs.FanOutField, "2 100");
        Assert.True(other.Contains(EventValidator.BossesFanOut, StringComparison.Ordinal) || other.Contains(EventValidator.FanOutLocation, StringComparison.Ordinal), other);
        Assert.Equal(hash, lib.Hash);
    }

    [Fact]
    public void Bosses_passes_set_and_none()
    {
        var lib = BossLib();
        var reply = AuthoringTests.Set(lib, "raid", CommandArgs.BossesField, BossTourok + ":2,CHAR_Bandit_Deadeye");
        Assert.True(Encoding.UTF8.GetByteCount(reply) <= Wire.MaxBytes, reply);
        var d = HgLoaded(lib, "raid");
        Assert.Equal([(BossTourok, 2), ("CHAR_Bandit_Deadeye", 1)], d.Action!.Bosses!.Select(b => (b.Prefab, b.Count)));

        AuthoringTests.Set(lib, "bossed", CommandArgs.BossesField, BossTourok + ":3");                   // the same prefab keeps its modifiers
        var kept = HgLoaded(lib, "bossed").Action!.Bosses!.Single();
        Assert.Equal((3, 60), (kept.Count, (int)kept.Modifiers!.Level!));

        AuthoringTests.Set(lib, "bossed", CommandArgs.BossesField, "none");
        Assert.Null(HgAction(lib, "bossed")["bosses"]);
        Assert.Null(HgLoaded(lib, "bossed").Action!.Bosses);
    }

    [Fact]
    public void Bosses_passes_the_table_name() =>
        Assert.Equal(("spawn action", "admin"), CommandArgs.SettableFields[CommandArgs.BossesField]);

    [Fact]
    public void Bosses_empty_value_required() =>
        Assert.Equal(CommandArgs.ValueRequired, AuthoringTests.Set(BossLib(), "raid", CommandArgs.BossesField, ""));
}
