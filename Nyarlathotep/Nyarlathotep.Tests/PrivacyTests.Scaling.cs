using System.Reflection;
using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>scaling D11: no log line, reply, wire line or `event info` line carries a player's name, platform id or position
/// because of scaling; a player's gear level appears only as the counted players' average; players' rows and gear levels are
/// held for one decision in memory only, and state.json gains no property.</summary>
public partial class PrivacyTests
{
    const float ScPlantedX = -1234.5f, ScPlantedZ = 678.25f;          // a planted player's position (FarbaneWoods: x < 0)
    const double ScGearA = 31.0, ScGearB = 71.0;                        // two players' own gear levels; their average is 51

    static readonly Regex ScPlayerMarks = new(@"7656119\d{10}|1234|678|Chaosplant", RegexOptions.CultureInvariant);
    static readonly Regex ScOwnGear = new(@"\b(31|71)\b", RegexOptions.CultureInvariant);

    /// <summary>Every line and reply of scaling with two planted players: a wave's, a fanOut wave's, a horde's and a top-up's
    /// lines, `event info` while it runs, and the chat and wire replies of a scaling field.</summary>
    static List<string> ScalingOutput()
    {
        var rig = new ScalingRig();
        rig.Rows.AddRange([new ScalingPlayer(ScPlantedX, ScPlantedZ, true, true, false, ScGearA), new ScalingPlayer(ScPlantedX + 40, ScPlantedZ, true, true, true, ScGearB)]);
        var output = new List<string>();
        var def = ScalingTests.Def("\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 3 } ], \"waves\": 2, " +
            "\"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"AroundPlayer\", \"minDist\": 20, \"maxDist\": 40 }, " +
            "\"fanOut\": { \"maxInstances\": 2, \"minSpacing\": 100 }, \"scaling\": { \"unitsPerPlayer\": 2, \"levelFromPlayers\": true, \"levelOffset\": 2 } }");
        var read = Scaling.Read(rig, Scope.Global, includeAdmins: true);
        var wave = Scaling.ForWave(def.Action!, "sc", 1, read);
        var fan = Scaling.ForWave(def.Action!, "sc", 2, read, [(ScPlantedX, ScPlantedZ)]);
        output.AddRange([wave.Line!, fan.Line!]);
        var active = new ActiveEvent(new RunningInstance(def, Now, Now.AddSeconds(600)), "manual", null);
        var book = new ScalingBook();
        book.Record(active, wave.View!.Value);
        output.AddRange(EventLines.Info(def, active, Now.AddSeconds(30), scaling: Scaling.Lines.Now(book.Latest(active)!.Value)));
        var run = Apo.Run(Apo.Active(Apo.Def(Apo.Action(extra: "\"scaling\": { \"unitsPerPlayer\": 2, \"levelFromPlayers\": true }")), Now));
        var w = new ApoWorld();
        output.AddRange(run.TopUp(w, Now, rig.Source(book)));
        var rise = run.Rise(w, Now.AddMinutes(1), out var topUp, scale: rig.Source(book))!;
        output.Add(rise.Scaling!);
        output.AddRange(rise.Log);
        output.AddRange(topUp);
        var r = HumanReplyTests.Default();
        output.Add(r.Flows.Set(r.Who, "raid", "action.scaling.unitsPerPlayer", "2").Human);
        output.Add(r.Flows.ApiEvent(r.Who, "set", "raid", "action.scaling.levelFromPlayers", "true", ""));
        output.Add(r.Flows.Set(r.Who, "raid", "action.scaling.levelOffset", "9").Human);
        output.AddRange(r.Lib.Log.Lines);
        // a failing read whose exception message names a player, his platform id and his position (D11, D12)
        var leaky = new ScalingRig { Throw = "Chaosplant 76561190000000123 at -1234.5, 678.25" };
        new ScalingRuntime().Read(active, def.Action!.Scaling, Scope.Global, leaky, () => true, output.Add);
        return output;
    }

    [Fact]
    public void Scaling_fails_when_a_planted_player_reaches_a_line()
    {
        var output = ScalingOutput();
        Assert.Contains(output, l => l.StartsWith("scaling sc wave 1: players 2, ", StringComparison.Ordinal));
        Assert.Contains("scaling sc: player read failed: InvalidOperationException", output);   // the exception's type, never its message
        Assert.All(output, l => Assert.DoesNotMatch(ScPlayerMarks, l));
        // a player's row has no name or platform id to leak: positions, flags and the gear level only
        Assert.DoesNotContain(typeof(ScalingPlayer).GetProperties(), p => p.PropertyType == typeof(string) || p.PropertyType == typeof(long) || p.PropertyType == typeof(ulong));
    }

    [Fact]
    public void Scaling_fails_when_a_single_gear_level_is_shown()
    {
        var output = ScalingOutput();
        Assert.Contains(output, l => l.Contains("gear 51", StringComparison.Ordinal));
        Assert.All(output.Where(l => l.StartsWith("scaling", StringComparison.Ordinal)), l => Assert.DoesNotMatch(ScOwnGear, l));
    }

    [Fact]
    public void Scaling_fails_when_state_gains_a_property()
    {
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        // the book keeps counts and averages only, never a player row, and no saved type holds one
        var fields = typeof(ScalingBook).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.All(fields, f => Assert.DoesNotContain(typeof(ScalingPlayer), Args(f.FieldType)));
        Assert.All(fields, f => Assert.DoesNotContain(typeof(ScalingRead), Args(f.FieldType)));
        Assert.DoesNotContain(typeof(ScalingView).GetProperties(), p => p.PropertyType == typeof(ScalingPlayer) || p.PropertyType == typeof(double?));
        Assert.DoesNotContain(typeof(ApocalypseRecord).GetProperties(), p => p.Name.Contains("Scal", StringComparison.Ordinal) || p.Name.Contains("Level", StringComparison.Ordinal));

        static IEnumerable<Type> Args(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Args).Prepend(t) : [t];
    }

    [Fact]
    public void Scaling_passes_lines_carry_counts_and_the_average()
    {
        var lines = ScalingOutput().Where(l => l.StartsWith("scaling ", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Matches(@"^scaling ([a-z0-9-]+ (wave|horde) \d+: players \d+, gear (\d+|none), level (\d+|own), units \d+ -> \d+(, groups [\d/]+)?" +
            @"|[a-z0-9-]+ roamers: players \d+, per hotspot \d+, max \d+|now: \d+ players, gear (\d+|none), level (\d+|own)|[a-z0-9-]+: player read failed: [A-Za-z]+Exception)$", l));
        Assert.Contains("scaling sc wave 1: players 2, gear 51, level 53, units 3 -> 5", lines);
    }

    [Fact]
    public void Scaling_empty_no_player_no_gear()
    {
        var read = Scaling.Read(new ScalingRig(), Scope.Global, true);
        var def = ScalingTests.Def("\"action\": { \"type\": \"SpawnWaves\", \"units\": [ { \"prefab\": \"CHAR_Bandit_Thug\", \"count\": 3 } ], \"waves\": 1, " +
            "\"intervalSeconds\": 60, \"radius\": 10, \"location\": { \"type\": \"Point\", \"x\": 0, \"z\": 0 }, \"scaling\": { \"levelFromPlayers\": true } }");
        Assert.Equal("scaling sc wave 1: players 0, gear none, level own, units 3 -> 3", Scaling.ForWave(def.Action!, "sc", 1, read).Line);
    }
}
