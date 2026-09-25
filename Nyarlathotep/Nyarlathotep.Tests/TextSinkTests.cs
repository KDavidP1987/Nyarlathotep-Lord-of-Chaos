using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D14: `.nyar announce` text and player or clan names on their way to chat, the log and the
/// wire.</summary>
public class TextSinkTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  padded  ", "padded")]
    [InlineData("x", "x")]
    public void Clean_announcements_pass_trimmed(string raw, string text)
    {
        Assert.Equal(text, TextSink.Announcement(raw, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Exactly_200_characters_pass_and_201_do_not()
    {
        Assert.NotNull(TextSink.Announcement(new string('a', 200), out _));
        Assert.Null(TextSink.Announcement(new string('a', 201), out var error));
        Assert.Equal(TextSink.AnnounceRule, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("two\nlines")]
    [InlineData("bell\u0007")]
    [InlineData("<b>x</b>")]
    [InlineData("a > b")]
    [InlineData("<color=red>hi")]
    [InlineData("line\u2028break")]
    [InlineData("para\u2029break")]
    [InlineData("\u202Ereversed")]
    [InlineData("zero\u200Bwidth")]
    [InlineData("tag\U000E0001\U000E0041")]
    public void Empty_multiline_control_or_markup_announcements_are_refused(string? raw)
    {
        Assert.Null(TextSink.Announcement(raw, out var error));
        Assert.Equal("announce: 1-200 characters, no < > or control characters", error);
    }

    [Theory]
    [InlineData("Vlad", "Vlad")]
    [InlineData("<color=red>Vlad</color>", "color=redVlad/color")]
    [InlineData("Vl\nad\t", "Vlad")]
    [InlineData("\u202EdalV\u2028", "dalV")]
    [InlineData("Vl\U000E0001ad\U000E007F", "Vlad")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCDEFGHIJKLMNOPQRST")]
    [InlineData("", "")]
    public void Names_for_chat_and_the_log_lose_markup_and_control_characters_and_are_cut_to_20(string raw, string name)
    {
        Assert.Equal(name, TextSink.Name(raw));
    }

    [Fact]
    public void A_name_is_never_cut_inside_a_surrogate_pair()
    {
        var raw = new string('a', 19) + "\U0001F987" + "tail";   // the 20th element is one emoji of two chars
        var name = TextSink.Name(raw);
        Assert.Equal(new string('a', 19) + "\U0001F987", name);
        Assert.False(char.IsHighSurrogate(name[^1]));
    }

    [Theory]
    [InlineData("Vlad the Impaler", "Vlad_the_Impaler")]
    [InlineData("a=b;c:d", "abcd")]
    [InlineData("<b>\n</b>", "b/b")]
    [InlineData("==;;::", "-")]
    [InlineData(null, "-")]
    [InlineData("\U000E0020Bat\u202E", "Bat")]
    public void Names_for_a_wire_value_are_also_mapped(string? raw, string wire)
    {
        Assert.Equal(wire, TextSink.WireName(raw));
    }

    [Fact]
    public void Every_sink_changes_a_hostile_name()
    {
        const string hostile = "<color=#f00>Evil:\n=King;</color>";
        var chat = TextSink.Name(hostile);
        var wire = TextSink.WireName(hostile);
        Assert.NotEqual(hostile, chat);
        Assert.True(chat.IndexOfAny(new[] { '<', '>', '\n' }) < 0);
        Assert.True(wire.IndexOfAny(new[] { '<', '>', '\n', '=', ';', ':', ' ' }) < 0);
        Assert.True(chat.Length <= TextSink.NameMax);
    }

    [Fact]
    public void A_line_is_cut_to_the_chat_byte_limit_never_inside_a_character()
    {
        Assert.Equal("short", TextSink.CutToBytes("short", 480));
        var han = new string('漢', 200);            // 600 bytes
        var cut = TextSink.CutToBytes(han, 480);
        Assert.Equal(160, cut.Length);                   // 480 / 3
        var emoji = "ab" + "😀";                 // 2 + 4 bytes
        Assert.Equal("ab", TextSink.CutToBytes(emoji, 5));
        Assert.Equal(emoji, TextSink.CutToBytes(emoji, 6));
    }
}
