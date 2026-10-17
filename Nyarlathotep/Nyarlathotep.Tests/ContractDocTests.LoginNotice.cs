using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>login-notice D5: the wire is unchanged: Wire.Api stays 8, `[NYAR:version]` keeps its keys (the switch is not on
/// the wire), and the contract says LoginEventNotice is a server cfg switch Raphael neither reads nor sets in api 8.</summary>
public partial class ContractDocTests
{
    /// <summary>Wire.Api before this child (unit-watch's api 8); the login notice adds nothing past it.</summary>
    const int LoginNoticeApi = 8;

    internal const string LoginNoticeSentence = "LoginEventNotice is a server cfg switch Raphael neither reads nor sets in api 8, " +
        "and `[NYAR:version]` keeps its keys.";

    static readonly string[] VersionKeys =
        ["api", "plugin", "ready", "admin", "enabled", "killswitch", "empower", "waves", "boss", "zones", "sieges", "stats",
         "annwarn", "annbanner", "anndaily", "annlogin", "annshare"];

    static IEnumerable<string> KeysOf(string versionLine) =>
        versionLine["[NYAR:version] ".Length..].Split(' ').Select(t => t[..t.IndexOf('=')]);

    static string VersionLine() => Wire.Version(new VersionInfo(Wire.Api, "0.13.1", true, false, true, false,
        false, false, false, false, false, false, false, false, false, false, false));

    /// <summary>What keeps the wire and its contract from describing the login notice; empty when they do.</summary>
    internal static List<string> LoginNoticeProblems(string contract, int api, string versionLine)
    {
        var p = new List<string>();
        if (api != LoginNoticeApi) p.Add($"Wire.Api is {api}, not {LoginNoticeApi}");
        if (!KeysOf(versionLine).SequenceEqual(VersionKeys)) p.Add("the handshake keys changed");
        if (!contract.Contains(LoginNoticeSentence, StringComparison.Ordinal)) p.Add("the contract misses the login-notice sentence");
        return p;
    }

    [Fact]
    public void LoginNotice_passes_api_8_and_the_sentence() =>
        Assert.Empty(LoginNoticeProblems(Contract, Wire.Api, VersionLine()));

    [Fact]
    public void LoginNotice_fails_when_the_api_or_handshake_changes_or_the_contract_lacks_the_sentence()
    {
        Assert.Equal(["Wire.Api is 9, not 8"], LoginNoticeProblems(Contract, 9, VersionLine()));
        Assert.Equal(["the handshake keys changed"], LoginNoticeProblems(Contract, Wire.Api, VersionLine() + " annnotice=1"));
        Assert.Equal(["the contract misses the login-notice sentence"],
            LoginNoticeProblems(Contract.Replace(LoginNoticeSentence, ""), Wire.Api, VersionLine()));
    }

    [Fact]
    public void LoginNotice_empty_contract() => Assert.Single(LoginNoticeProblems("", LoginNoticeApi, VersionLine()));
}
