using System.Reflection;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>login-notice D5: the notice and its log line "login notice sent: &lt;k&gt; events" carry no player name,
/// platform id or position: the rule sees the recipient only as an opaque user and a platform id it never prints, and the
/// text is built from the running instances alone.</summary>
public partial class PrivacyTests
{
    static readonly string[] LoginNoticeMarks = [PlantedName, PlantedId.ToString(), .. Planted];

    /// <summary>The notice, the sent line and the failure lines, with the planted player connecting during the planted
    /// events.</summary>
    static List<string> LoginNoticeLines()
    {
        var lines = new List<string>();
        var now = Now;
        var faults = new FailureStreak();
        var notices = new LoginNotices<string>(() => now, lines.Add, lines.Add, faults);
        notices.Connected(PlantedName, PlantedId, true);
        notices.Connected(PlantedName + "2", PlantedId + 1, true);
        now = Now.AddSeconds(10);
        var first = true;
        notices.Tick(true, (_, _) => true, () => Running, _ => null, (_, text) =>
        {
            lines.Add(text);
            if (first) { first = false; return; }
            throw new InvalidOperationException("send broke");
        });
        notices.Connected(PlantedName, PlantedId + 2, true);
        now = Now.AddSeconds(20);
        notices.Tick(true, (_, _) => true, () => throw new InvalidOperationException("read broke"), _ => null, (_, text) => lines.Add(text));
        return lines;
    }

    [Fact]
    public void LoginNotice_passes_counts_and_event_names_only()
    {
        var lines = LoginNoticeLines();
        Assert.Contains(lines, l => l.StartsWith("Events in progress: Bandit raid: ", StringComparison.Ordinal));
        Assert.Contains("login notice sent: 2 events", lines);
        Assert.Contains("private message failed: send broke", lines);
        Assert.Contains("login notice failed: read broke", lines);
        foreach (var line in lines)
            foreach (var mark in LoginNoticeMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginNotice_fails_when_a_name_id_or_position_reaches_a_line()
    {
        foreach (var line in LoginNoticeLines())
            foreach (var mark in LoginNoticeMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        // the text builder takes the running instances, a clock and the own-line reader, never a player or a position
        var parameters = typeof(Messages).GetMethod(nameof(Messages.LoginNotice))!.GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Equal([typeof(IEnumerable<RunningInstance>), typeof(DateTime), typeof(Func<RunningInstance, string?>)], parameters);
    }

    [Fact]
    public void LoginNotice_empty_no_running_event_no_line()
    {
        var lines = new List<string>();
        var notices = new LoginNotices<string>(() => Now.AddSeconds(10), lines.Add, lines.Add, new FailureStreak());
        notices.Connected(PlantedName, PlantedId, true);
        notices.Tick(true, (_, _) => true, () => [], _ => null, (_, text) => lines.Add(text));
        Assert.Empty(lines);
    }
}
