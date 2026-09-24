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
    public const string AnnounceRule = "announce: 1-200 characters, no < > or control characters";

    /// <summary>The trimmed text, or null with <paramref name="error"/> set to <see cref="AnnounceRule"/>.</summary>
    public static string? Announcement(string? raw, out string? error)
    {
        var text = (raw ?? "").Trim();
        error = null;
        if (text.Length is 0 or > AnnounceMax || text.Any(Forbidden))
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
        var kept = new string((raw ?? "").Where(c => !Forbidden(c)).ToArray()).Trim();
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
        foreach (var c in raw ?? "")
        {
            if (Forbidden(c) || c is '=' or ';' or ':') continue;
            sb.Append(char.IsWhiteSpace(c) ? '_' : c);
        }
        return sb.Length == 0 ? "-" : sb.ToString();
    }

    // Control characters, format characters (bidi overrides, zero-width marks) and the Unicode line and paragraph
    // separators: each can break a line or reorder what a reader sees.
    static bool Forbidden(char c) => c is '<' or '>' || char.GetUnicodeCategory(c) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
