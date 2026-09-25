#nullable enable
using System.Globalization;
using System.Text;

namespace Nyarlathotep.Logic;

/// <summary>Every piece of outside text on its way to chat, the log or a wire value (foundation D14, Epic D44):
/// `.nyar announce` text is refused unless clean; player and clan names are cleaned and cut.</summary>
public static class TextSink
{
    public const int AnnounceMax = 200;
    public const int NameMax = 20;
    public const string AnnounceRule = "announce: 1-200 characters, no angle brackets or control characters";

    /// <summary>The trimmed text, or null with <paramref name="error"/> set to <see cref="AnnounceRule"/>.</summary>
    public static string? Announcement(string? raw, out string? error)
    {
        var text = (raw ?? "").Trim();
        error = null;
        if (text.Length is 0 or > AnnounceMax || text.EnumerateRunes().Any(Forbidden))
        {
            error = AnnounceRule;
            return null;
        }
        return text;
    }

    /// <summary>A name for chat or the log: '&lt;', '&gt;' and control characters removed, cut to 20 characters
    /// (never inside a surrogate pair).</summary>
    public static string Name(string? raw)
    {
        var kept = Keep(raw).Trim();
        var info = new StringInfo(kept);
        return info.LengthInTextElements <= NameMax ? kept : info.SubstringByTextElements(0, NameMax);
    }

    /// <summary>A name for a wire value: <see cref="Name"/>, then mapped by <see cref="WireValue"/>.</summary>
    public static string WireName(string? raw) => WireValue(Name(raw));

    /// <summary>Any text for a wire value (Raphael contract §1): '&lt;', '&gt;' and control characters removed,
    /// space to '_', '=', ';' and ':' removed; an empty result is "-".</summary>
    public static string WireValue(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var r in Keep(raw).EnumerateRunes())
        {
            if (r.Value is '=' or ';' or ':') continue;
            if (Rune.IsWhiteSpace(r)) sb.Append('_');
            else sb.Append(r.ToString());
        }
        return sb.Length == 0 ? "-" : sb.ToString();
    }

    /// <summary><paramref name="text"/> cut to at most <paramref name="maxBytes"/> UTF-8 bytes, never inside a
    /// character, so it fits the game's 512-byte chat string.</summary>
    public static string CutToBytes(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;
        var sb = new StringBuilder();
        var bytes = 0;
        foreach (var r in text.EnumerateRunes())
        {
            if (bytes + r.Utf8SequenceLength > maxBytes) break;
            sb.Append(r.ToString());
            bytes += r.Utf8SequenceLength;
        }
        return sb.ToString();
    }

    // The text without its forbidden characters, judged per Unicode scalar so a character outside the Basic
    // Multilingual Plane (a surrogate pair in UTF-16) is judged as the one character it is. A lone surrogate is
    // replaced by U+FFFD by EnumerateRunes, which is harmless.
    static string Keep(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var r in (raw ?? "").EnumerateRunes()) if (!Forbidden(r)) sb.Append(r.ToString());
        return sb.ToString();
    }

    // Control characters, format characters (bidi overrides, zero-width marks, tag characters) and the Unicode line
    // and paragraph separators: each can break a line or change what a reader sees.
    static bool Forbidden(Rune r) => r.Value is '<' or '>' || Rune.GetUnicodeCategory(r) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
