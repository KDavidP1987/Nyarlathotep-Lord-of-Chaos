#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace Nyarlathotep.Logic;

/// <summary>The contract's error codes (docs/RAPHAEL_INTEGRATION_CONTRACT.md §4).</summary>
public enum WireError { NotReady, NoAccess, Disabled, NotFound, BadArg, RateLimit, Cooldown }

/// <summary>What `.nyar api version` reports (contract §2).</summary>
public sealed record VersionInfo(
    int Api, string Plugin, bool Ready, bool Admin, bool Enabled, bool KillSwitch,
    bool Empower, bool Waves, bool Boss, bool Zones, bool Sieges, bool Stats,
    bool AnnWarn, bool AnnBanner, bool AnnDaily, bool AnnLogin, bool AnnShare);

/// <summary>Builds the machine lines Raphael reads (foundation D12, Epic D37): "[NYAR:&lt;tag&gt;]" then
/// space-separated key=value tokens, at most 480 bytes, with no '&lt;', '&gt;' or newline. Every value passes
/// through <see cref="TextSink.WireValue"/>, so it holds no space, '=', ';' or ':'.</summary>
public static class Wire
{
    public const int Api = 1;
    public const int MaxBytes = 480;

    static readonly Regex NameRx = new("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant);

    /// <summary>One line. Tags and keys are code constants ([a-z][a-z0-9-]*); a bad one throws. A token that would
    /// take the line past <see cref="MaxBytes"/> is dropped with every token after it, never cut in half.</summary>
    public static string Line(string tag, params (string Key, string Value)[] tokens)
    {
        if (!NameRx.IsMatch(tag)) throw new ArgumentException($"bad wire tag {tag}");
        var sb = new StringBuilder($"[NYAR:{tag}]");
        var bytes = Encoding.UTF8.GetByteCount(sb.ToString());
        foreach (var (key, value) in tokens)
        {
            if (!NameRx.IsMatch(key)) throw new ArgumentException($"bad wire key {key}");
            var token = $" {key}={TextSink.WireValue(value)}";
            var n = Encoding.UTF8.GetByteCount(token);
            if (bytes + n > MaxBytes) break;
            sb.Append(token);
            bytes += n;
        }
        return sb.ToString();
    }

    public static string Bool(bool b) => b ? "1" : "0";

    /// <summary>`[NYAR:version]` with every key of contract §2, in its order.</summary>
    public static string Version(VersionInfo v) => Line("version",
        ("api", v.Api.ToString()), ("plugin", v.Plugin), ("ready", Bool(v.Ready)), ("admin", Bool(v.Admin)),
        ("enabled", Bool(v.Enabled)), ("killswitch", Bool(v.KillSwitch)),
        ("empower", Bool(v.Empower)), ("waves", Bool(v.Waves)), ("boss", Bool(v.Boss)), ("zones", Bool(v.Zones)),
        ("sieges", Bool(v.Sieges)), ("stats", Bool(v.Stats)),
        ("annwarn", Bool(v.AnnWarn)), ("annbanner", Bool(v.AnnBanner)), ("anndaily", Bool(v.AnnDaily)),
        ("annlogin", Bool(v.AnnLogin)), ("annshare", Bool(v.AnnShare)));

    /// <summary>`[NYAR:err] cmd=&lt;cmd&gt; code=&lt;code&gt; [secs=] [arg=]` (contract §4).</summary>
    public static string Error(string cmd, WireError code, int? secs = null, string? arg = null)
    {
        var tokens = new List<(string, string)> { ("cmd", cmd), ("code", code.ToString().ToLowerInvariant()) };
        if (secs is { } s) tokens.Add(("secs", s.ToString()));
        if (arg is not null) tokens.Add(("arg", arg));
        return Line("err", tokens.ToArray());
    }
}
