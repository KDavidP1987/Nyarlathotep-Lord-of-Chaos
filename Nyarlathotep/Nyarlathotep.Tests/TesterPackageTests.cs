using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nyarlathotep.Tests;

/// <summary>The committed tester package (testing/): the blank workbook and reading copy testers download stay built
/// from the current tools/tester-package/rows.py and build.py, so a row edited without a rebuild fails here.</summary>
public class TesterPackageTests
{
    [Fact]
    public void Published_fails_when_the_workbook_is_older_than_its_rows()
    {
        var repo = Repo();
        var stamp = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "testing", "source.json"))).RootElement;
        Assert.True(Hash(repo) == stamp.GetProperty("source_sha256").GetString(),
            "testing/ is older than tools/tester-package/rows.py or build.py: run python tools/tester-package/build.py");
        var files = stamp.GetProperty("files").EnumerateArray().Select(f => f.GetString()!).ToList();
        Assert.Equal(2, files.Count);                                    // the two files a tester gets, nothing else
        Assert.Contains(files, f => f.EndsWith(".xlsx", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith(".pdf", StringComparison.Ordinal));
        foreach (var f in files) Assert.True(new FileInfo(Path.Combine(repo, "testing", f)).Length > 0, f);
        Assert.Equal(files.Count, Directory.GetFiles(Path.Combine(repo, "testing"), "*.xlsx").Length
                                  + Directory.GetFiles(Path.Combine(repo, "testing"), "*.pdf").Length);   // one round only
    }

    [Fact]
    public void Published_fails_when_a_rows_edit_keeps_the_same_stamp()
    {
        // the stamp is the hash build.py writes: one changed character in rows.py changes it
        var repo = Repo();
        var rows = File.ReadAllBytes(Path.Combine(repo, "tools", "tester-package", "rows.py"));
        var build = File.ReadAllBytes(Path.Combine(repo, "tools", "tester-package", "build.py"));
        Assert.NotEqual(HashOf(rows, build), HashOf(rows.Concat("\n# edited"u8.ToArray()).ToArray(), build));
        Assert.Equal(HashOf(rows, build), HashOf(Crlf(rows), Crlf(build)));      // line endings never count
    }

    static string Hash(string repo) => HashOf(File.ReadAllBytes(Path.Combine(repo, "tools", "tester-package", "rows.py")),
        File.ReadAllBytes(Path.Combine(repo, "tools", "tester-package", "build.py")));

    static string HashOf(byte[] rows, byte[] build)
    {
        using var sha = SHA256.Create();
        var all = Lf(rows).Concat(Lf(build)).ToArray();
        return Convert.ToHexString(sha.ComputeHash(all)).ToLowerInvariant();
    }

    static byte[] Lf(byte[] data) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("\r\n", "\n"));

    static byte[] Crlf(byte[] data) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Lf(data)).Replace("\n", "\r\n"));

    static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
