using System.Reflection;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D17: no log line, reply, wire line, push or health entry carries a player's name, platform id or
/// position because of unit watch; the distances in the lines are our units' own; players are held for one check only and
/// state.json gains no property.</summary>
public partial class PrivacyTests
{
    const long WatchPlantedKey = 76561190000000123;                    // a fake platform-id-shaped Hunt key
    const float WatchPlantedX = 1234.5f, WatchPlantedZ = 678.25f;

    static readonly Regex WatchPlayerMarks = new(@"7656119\d{10}|1234|678|Chaosplant", RegexOptions.CultureInvariant);

    /// <summary>Every line and reply of the watch with one planted player near our units: a player landing, a wait, a
    /// removal in view, `unstick` and its twin line, and the health entries of a failing read and move.</summary>
    static List<string> WatchOutput()
    {
        var output = new List<string>();
        var (r, a) = Watch.Rig();
        var lost = r.W.Add("uw", WatchRig.T0.AddSeconds(1), 1000, 600, -150f);
        r.W.People.Add(new HuntCandidate(WatchPlantedKey, WatchPlantedX, WatchPlantedZ, true, true, false, false));
        r.For(20, () => { if (r.W.At.ContainsKey(lost)) r.W.Sink(lost, -150f); });   // a player landing beside the planted player
        var seen = r.W.Add("uw", r.Now, WatchPlantedX + 60, WatchPlantedZ, -150f);
        r.W.Walk = (_, _, _) => false;
        r.For(300, () => { if (r.W.At.ContainsKey(seen)) r.W.Sink(seen, -150f); });  // a wait and a removal in view
        var idle = r.W.Add("uw", r.Now, WatchPlantedX + 30, WatchPlantedZ);
        var o = r.Unstick("uw");
        output.Add(o.Human);
        output.Add(ApiLines.Unstick("uw", o));
        r.W.ThrowRead.Add(idle);
        r.W.ThrowMove = true;
        r.For(30);
        output.AddRange(r.P.Health);
        output.AddRange(r.Log);
        output.AddRange(r.Errors);
        Assert.Contains(r.Log, l => l.EndsWith("(player)", StringComparison.Ordinal));
        Assert.Contains(r.Log, l => l.EndsWith("(in view)", StringComparison.Ordinal));
        Assert.Contains(r.Log, l => l.Contains("waiting:", StringComparison.Ordinal));
        return output;
    }

    [Fact]
    public void UnitWatch_fails_when_a_planted_player_reaches_a_line()
    {
        var output = WatchOutput();
        Assert.NotEmpty(output);
        Assert.All(output, l => Assert.DoesNotMatch(WatchPlayerMarks, l));
    }

    [Fact]
    public void UnitWatch_fails_when_state_gains_a_property()
    {
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        // the phase keeps no player between checks: none of its fields holds a player or a list of them
        var fields = typeof(UnitWatchPhase).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.All(fields, f => Assert.DoesNotContain(typeof(HuntCandidate), Args(f.FieldType)));
        Assert.All(typeof(FightBook).GetFields(BindingFlags.Instance | BindingFlags.NonPublic), f => Assert.DoesNotContain(typeof(HuntCandidate), Args(f.FieldType)));

        static IEnumerable<Type> Args(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Args).Prepend(t) : [t];
    }

    [Fact]
    public void UnitWatch_passes_lines_carry_our_units_only()
    {
        var output = WatchOutput();
        Assert.All(output.Where(l => l.StartsWith("unit-watch uw ", StringComparison.Ordinal)), l => Assert.Contains("CHAR_Spider_Forest", l));
        Assert.Contains("[NYAR:ok] cmd=event verb=unstick id=uw marked=1 fighting=0", output);
    }

    [Fact]
    public void UnitWatch_empty_no_watch_no_line()
    {
        var (r, _) = Watch.Rig();
        r.W.People.Add(new HuntCandidate(WatchPlantedKey, WatchPlantedX, WatchPlantedZ, true, true, false, false));
        r.For(300);
        Assert.Empty(r.Log);
        Assert.Empty(r.P.Health);
    }
}
