#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace Nyarlathotep.Logic;

/// <summary>Paging of the api reads (docs/RAPHAEL_INTEGRATION_CONTRACT.md §4; raphael-api-core D3): 1-based, 10 rows a
/// page, a live view of the current set. Every reply ends with `[NYAR:end] cmd= page=&lt;cur&gt;/&lt;total&gt; count=`.</summary>
public static class Paging
{
    public const int PageSize = 10;

    /// <summary>The page asked for: none or empty means 1; otherwise digits only, in the int range, at least 1.
    /// "0", "-1", "+1", " 1", "1.5", "x" and "99999999999" are refused.</summary>
    public static bool TryParse(string? raw, out int page)
    {
        page = 1;
        if (string.IsNullOrEmpty(raw)) return true;
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1;
    }

    /// <summary>The number of pages: at least 1, so an empty result is page 1/1.</summary>
    public static int Pages(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);

    /// <summary>The reply for <paramref name="rawPage"/> of <paramref name="rows"/>: that page's rows then the end line;
    /// a page past the last is the end line alone; a bad page is `[NYAR:err] cmd= code=badarg arg=page` alone.</summary>
    public static IReadOnlyList<string> Reply(string cmd, IReadOnlyList<string> rows, string? rawPage)
    {
        if (!TryParse(rawPage, out var page)) return [Wire.Error(cmd, WireError.BadArg, arg: "page")];
        var reply = new List<string>();
        var from = (long)(page - 1) * PageSize;
        for (var i = from; i < rows.Count && i < from + PageSize; i++) reply.Add(rows[(int)i]);
        reply.Add(Wire.EndPaged(cmd, page, Pages(rows.Count), rows.Count));
        return reply;
    }
}
