using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D27: the chat writes at events.json's 200-definition limit, and their time on a file of 199
/// definitions and on a valid file of 200 padded to just under 1 MB (median of five, parse and validation included).</summary>
public class AuthoringCapacityTests
{
    static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(200);
    static readonly DateTime Now = AuthoringTests.Now;

    static string[] Events(int n) => Enumerable.Range(0, n).Select(i => Json.Event($"e{i:000}")).ToArray();

    static Library Of(int n) => AuthoringTests.Lib(Events(n));

    /// <summary>One creating form (event-library D5-D7) adding definition <paramref name="id"/>.</summary>
    static EditPlan Create(string form, string text, string id) => form switch
    {
        "template use" => Authoring.TemplateUse(text, TemplateLibraryTests.Real(), "bandit-ambush", id),
        "event new" => Authoring.New(text, id, "spawns"),
        _ => Authoring.Copy(text, "e000", id),
    };

    static string Confirm(Library lib, string id)
    {
        var arming = new DeleteArming();
        var deleter = new EventDeleter(arming, lib.Editor, lib.Catalog, _ => false, lib.State, lib.Log.Add);
        deleter.Request(1, id, Now);
        return deleter.Confirm(1, id, Now.AddSeconds(1), lib.Units).Human;
    }

    /// <summary>The median of five timed runs, each on a fresh fixture built outside the timing.</summary>
    static TimeSpan Median(Func<Library> fixture, Action<Library> op)
    {
        var times = new List<TimeSpan>();
        for (var i = 0; i < 5; i++)
        {
            var lib = fixture();
            var sw = Stopwatch.StartNew();
            op(lib);
            times.Add(sw.Elapsed);
        }
        return times.OrderBy(t => t).ElementAt(2);
    }

    // ---- D27 Capacity

    [Theory]
    [InlineData("template use")]
    [InlineData("event new")]
    [InlineData("event copy")]
    public void Capacity_passes_creating_form_writes_200th(string form)
    {
        var lib = Of(199);
        Assert.DoesNotContain("limit", lib.Write(t => Create(form, t, "two-hundred")));
        Assert.Equal(200, lib.Catalog.Current.All.Count);
        var hash = lib.Hash;
        Assert.Equal("events.json holds 200 definitions, the limit", lib.Write(t => Create(form, t, "two-hundred-one")));
        Assert.Equal(hash, lib.Hash);
        Assert.Equal(200, lib.Catalog.Current.All.Count);
    }

    [Fact]
    public void Capacity_passes_set_and_delete_at_200()
    {
        var lib = Of(200);
        Assert.Equal("event e005 durationSeconds = 900", AuthoringTests.Set(lib, "e005", "durationSeconds", "900"));
        Assert.StartsWith("event e006 deleted", Confirm(lib, "e006"));
        Assert.Equal(199, lib.Catalog.Current.All.Count);
    }

    [Theory]
    [InlineData("template use")]
    [InlineData("event new")]
    [InlineData("event copy")]
    [InlineData("set")]
    [InlineData("delete confirm")]
    public void Capacity_passes_each_write_under_200ms_at_199(string op)
    {
        var median = Median(() => Of(199), lib =>
        {
            var reply = op switch
            {
                "set" => AuthoringTests.Set(lib, "e005", "durationSeconds", "900"),
                "delete confirm" => Confirm(lib, "e006"),
                _ => lib.Write(t => Create(op, t, "fresh")),
            };
            Assert.DoesNotContain("limit", reply);
        });
        Assert.True(median < Budget, $"{op} median {median.TotalMilliseconds} ms");
    }

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string LongName(string stem, int i) => $"CHAR_{stem}{i:00}_".PadRight(CommandArgs.MaxNameLength, 'x');

    /// <summary>A valid file of 200 definitions, each with 20 bosses and 10 units of 96-character names and ten announce
    /// lines of <paramref name="lineLength"/> characters, in the editor's own indented format.</summary>
    static string NearMegabyte(int lineLength)
    {
        var line = new string('a', lineLength);
        var events = new JsonArray();
        for (var i = 0; i < 200; i++)
        {
            events.Add(new JsonObject
            {
                ["id"] = $"e{i:000}",
                ["name"] = $"Padded event {i:000}",
                ["enabled"] = false,
                ["pillar"] = "spawns",
                ["trigger"] = new JsonObject { ["type"] = "VBloodKilled", ["bosses"] = new JsonArray(Enumerable.Range(0, 20).Select(b => (JsonNode?)LongName("Boss", b)).ToArray()) },
                ["durationSeconds"] = 600,
                ["action"] = new JsonObject
                {
                    ["type"] = "SpawnWaves",
                    ["units"] = new JsonArray(Enumerable.Range(0, 10).Select(u => (JsonNode?)new JsonObject { ["prefab"] = LongName("Unit", u), ["count"] = 50 }).ToArray()),
                    ["waves"] = 10, ["intervalSeconds"] = 600, ["radius"] = 30,
                    ["location"] = new JsonObject { ["type"] = "Point", ["x"] = -1234.5m, ["z"] = 876.5m },
                },
                ["announce"] = new JsonObject
                {
                    ["start"] = new JsonArray(Enumerable.Range(0, 5).Select(_ => (JsonNode?)line).ToArray()),
                    ["end"] = new JsonArray(Enumerable.Range(0, 5).Select(_ => (JsonNode?)line).ToArray()),
                },
            });
        }
        return new JsonObject { ["SchemaVersion"] = 1, ["events"] = events }.ToJsonString(Indented) + Environment.NewLine;
    }

    static FakeUnits LongUnits() =>
        TemplateLibraryTests.Units(Enumerable.Range(0, 20).Select(b => LongName("Boss", b)).Concat(Enumerable.Range(0, 10).Select(u => LongName("Unit", u)))
            .Append("CHAR_Bandit_Deadeye").ToArray());

    /// <summary>The announce line length that brings the file closest to, and under, 1 MB less 2 KB.</summary>
    static string NearMegabyteText()
    {
        var target = EventValidator.MaxFileBytes - 2048;
        var at0 = System.Text.Encoding.UTF8.GetByteCount(NearMegabyte(0));
        var at1 = System.Text.Encoding.UTF8.GetByteCount(NearMegabyte(1));
        var length = Math.Min(200, (target - at0) / (at1 - at0));
        return NearMegabyte(length);
    }

    [Fact]
    public void Capacity_passes_near_1mb_set_and_delete_under_200ms()
    {
        var text = NearMegabyteText();
        var size = System.Text.Encoding.UTF8.GetByteCount(text);
        Assert.InRange(size, EventValidator.MaxFileBytes - 16_384, EventValidator.MaxFileBytes);
        Library Fixture()
        {
            var lib = new Library(text, LongUnits());
            Assert.Equal(200, lib.Catalog.Current.All.Count(d => d.DisabledReason is null));
            return lib;
        }
        var set = Median(Fixture, lib => Assert.Equal("event e005 durationSeconds = 900", AuthoringTests.Set(lib, "e005", "durationSeconds", "900")));
        var delete = Median(Fixture, lib => Assert.StartsWith("event e006 deleted", Confirm(lib, "e006")));
        Assert.True(set < Budget, $"set median {set.TotalMilliseconds} ms");
        Assert.True(delete < Budget, $"delete median {delete.TotalMilliseconds} ms");
    }

    [Theory]
    [InlineData("template use")]
    [InlineData("event new")]
    [InlineData("event copy")]
    public void Capacity_fails_when_file_holds_200(string form)
    {
        var lib = Of(200);
        var hash = lib.Hash;
        Assert.Equal("events.json holds 200 definitions, the limit", lib.Write(t => Create(form, t, "extra")));
        Assert.Equal(hash, lib.Hash);
    }

    [Fact]
    public void Capacity_empty_events_file()
    {
        var median = Median(() => AuthoringTests.Lib(), lib => Assert.StartsWith("event fresh created", lib.Write(t => Authoring.New(t, "fresh", "spawns"))));
        Assert.True(median < Budget);
    }
}
