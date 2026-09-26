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
    /// take the line past <see cref="MaxBytes"/> is dropped with every token after it, never cut in half; use
    /// <see cref="Record"/> when every token is required.</summary>
    public static string Line(string tag, params (string Key, string Value)[] tokens) => Build(tag, tokens, required: false);

    /// <summary>A line whose every token is required (the handshake, an error): it throws rather than drop one.</summary>
    public static string Record(string tag, params (string Key, string Value)[] tokens) => Build(tag, tokens, required: true);

    static string Build(string tag, (string Key, string Value)[] tokens, bool required)
    {
        if (!NameRx.IsMatch(tag)) throw new ArgumentException($"bad wire tag {tag}");
        var sb = new StringBuilder($"[NYAR:{tag}]");
        var bytes = Encoding.UTF8.GetByteCount(sb.ToString());
        foreach (var (key, value) in tokens)
        {
            if (!NameRx.IsMatch(key)) throw new ArgumentException($"bad wire key {key}");
            var token = $" {key}={TextSink.WireValue(value)}";
            var n = Encoding.UTF8.GetByteCount(token);
            if (bytes + n > MaxBytes)
            {
                if (required) throw new ArgumentException($"wire line {tag} would exceed {MaxBytes} bytes at {key}");
                break;
            }
            sb.Append(token);
            bytes += n;
        }
        return sb.ToString();
    }

    public static string Bool(bool b) => b ? "1" : "0";

    /// <summary>`[NYAR:version]` with every key of contract §2, in its order.</summary>
    public static string Version(VersionInfo v) => Record("version",
        ("api", v.Api.ToString()), ("plugin", v.Plugin), ("ready", Bool(v.Ready)), ("admin", Bool(v.Admin)),
        ("enabled", Bool(v.Enabled)), ("killswitch", Bool(v.KillSwitch)),
        ("empower", Bool(v.Empower)), ("waves", Bool(v.Waves)), ("boss", Bool(v.Boss)), ("zones", Bool(v.Zones)),
        ("sieges", Bool(v.Sieges)), ("stats", Bool(v.Stats)),
        ("annwarn", Bool(v.AnnWarn)), ("annbanner", Bool(v.AnnBanner)), ("anndaily", Bool(v.AnnDaily)),
        ("annlogin", Bool(v.AnnLogin)), ("annshare", Bool(v.AnnShare)));

    /// <summary>A definition name on the wire: mapped, then cut to this many UTF-8 bytes (raphael-api-core Business
    /// rules 5), so no name pushes a later key off its line.</summary>
    public const int NameBytes = 64;

    /// <summary>A validation reason on the wire: mapped, then cut to this many UTF-8 bytes.</summary>
    public const int ReasonBytes = 120;

    /// <summary><paramref name="raw"/> mapped by <see cref="TextSink.WireValue"/> and cut on a character boundary.</summary>
    public static string Cut(string? raw, int maxBytes) => TextSink.CutToBytes(TextSink.WireValue(raw), maxBytes);

    /// <summary>`[NYAR:event]`, one `api status` row (contract §3 status).</summary>
    public static string Event(string id, string kind, string name, string state, string faction, int left, string wave, int? units) =>
        Record("event", ("id", id), ("kind", kind), ("name", Cut(name, NameBytes)), ("state", state), ("faction", faction),
            ("left", left.ToString()), ("wave", wave), ("units", units?.ToString() ?? "-"));

    /// <summary>`[NYAR:def]`, one `api events` row (contract §3 events).</summary>
    public static string Def(string id, string name, bool enabled, string trigger, string action, int duration, string state, string? reason) =>
        Record("def", ("id", id), ("name", Cut(name, NameBytes)), ("enabled", Bool(enabled)), ("trigger", trigger),
            ("action", action), ("duration", duration.ToString()), ("state", state), ("reason", reason is null ? "-" : Cut(reason, ReasonBytes)));

    /// <summary>`[NYAR:end] cmd= count=` after an unpaged read (contract §4).</summary>
    public static string End(string cmd, int count) => Record("end", ("cmd", cmd), ("count", count.ToString()));

    /// <summary>`[NYAR:end] cmd= page=&lt;cur&gt;/&lt;total&gt; count=` after a paged read (contract §4).</summary>
    public static string EndPaged(string cmd, int page, int total, int count) =>
        Record("end", ("cmd", cmd), ("page", $"{page}/{total}"), ("count", count.ToString()));

    /// <summary>`[NYAR:ok] cmd=&lt;cmd&gt; …`, the acknowledgement of a command that changes only a subscription.</summary>
    public static string Ok(string cmd, params (string Key, string Value)[] tokens) =>
        Record("ok", new[] { ("cmd", cmd) }.Concat(tokens).ToArray());

    /// <summary>`[NYAR:ev] type= id= secs= [wave=]`, one push line (contract §3 push events).</summary>
    public static string Ev(string type, string id, int secs, int? wave = null)
    {
        var tokens = new List<(string, string)> { ("type", type), ("id", id), ("secs", secs.ToString()) };
        if (wave is { } w) tokens.Add(("wave", w.ToString()));
        return Record("ev", tokens.ToArray());
    }

    /// <summary>`[NYAR:err] cmd=&lt;cmd&gt; code=&lt;code&gt; [secs=] [arg=]` (contract §4).</summary>
    public static string Error(string cmd, WireError code, int? secs = null, string? arg = null)
    {
        var tokens = new List<(string, string)> { ("cmd", cmd), ("code", code.ToString().ToLowerInvariant()) };
        if (secs is { } s) tokens.Add(("secs", s.ToString()));
        if (arg is not null) tokens.Add(("arg", arg));
        return Record("err", tokens.ToArray());
    }
}
