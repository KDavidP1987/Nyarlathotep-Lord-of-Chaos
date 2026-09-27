using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D12: every chat write of the library raises exactly one config-changed when applied and none
/// when refused.</summary>
public partial class ConfigChangedTests
{
    public static TheoryData<string> LibraryWrites => new() { "template use", "event new", "event copy", "event delete" };

    static EditPlan LibraryWrite(string kind, string text) => kind switch
    {
        "template use" => Authoring.TemplateUse(text, TemplateLibraryTests.Real(), "bandit-ambush", null),
        "event new" => Authoring.New(text, "fresh", "empowerment"),
        "event copy" => Authoring.Copy(text, "raid", "raid-2"),
        _ => Authoring.Delete(text, "raid"),
    };

    [Theory]
    [MemberData(nameof(LibraryWrites))]
    public void Equivalence_passes_one_notice_per_applied_write(string kind)
    {
        var lib = AuthoringTests.Lib(Json.Event("raid"));
        lib.Write(t => LibraryWrite(kind, t));
        Assert.Equal(1, lib.ConfigChanged);
        Assert.Single(lib.Hub.Queue.Lines, l => l.Type == PushLines.ConfigChangedType);
    }

    [Theory]
    [MemberData(nameof(LibraryWrites))]
    public void Equivalence_fails_when_write_refused(string kind)
    {
        var lib = AuthoringTests.Lib(Json.Event("raid"), Json.Event("raid-2"), Json.Event("fresh"), Json.Event("bandit-ambush"));
        var delete = kind == "event delete";
        if (delete) lib.Fs.FailWrites = true;                                     // a delete is refused by the disk here
        var reply = lib.Write(t => LibraryWrite(kind, t));
        Assert.True(delete ? reply.StartsWith("could not write", StringComparison.Ordinal) : reply.Contains("already exists", StringComparison.Ordinal), reply);
        Assert.Equal(0, lib.ConfigChanged);
    }

    [Fact]
    public void Equivalence_empty_events_file()
    {
        var lib = AuthoringTests.Lib();
        lib.Write(t => LibraryWrite("event new", t));
        Assert.Equal(1, lib.ConfigChanged);
    }
}
