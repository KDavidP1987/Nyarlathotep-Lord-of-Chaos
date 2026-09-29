using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-spawns D18 (the `.nyar event set` values of the new keys, each in D6's range) and D32 (every new chat
/// line within 480 bytes at the maximum lengths of its fields).</summary>
public partial class CommandArgTests
{
    // ---- D18 Spawns

    [Theory]
    [InlineData("action.units.1.chance", "0.04", CommandArgs.UnitChanceRule)]
    [InlineData("action.units.10.chance", "1.01", CommandArgs.UnitChanceRule)]
    [InlineData("action.units.1.chance", "0.555", CommandArgs.UnitChanceRule)]
    [InlineData("action.units.1.chance", ".5", CommandArgs.UnitChanceRule)]
    [InlineData("action.units.1.chance", "0,5", CommandArgs.UnitChanceRule)]
    [InlineData("action.modifiers.level", "0", EventValidator.LevelRule)]
    [InlineData("action.modifiers.level", "121", EventValidator.LevelRule)]
    [InlineData("action.modifiers.level", "+3", EventValidator.LevelRule)]
    [InlineData("action.modifiers.levelDelta", "6", EventValidator.LevelDeltaRule)]
    [InlineData("action.modifiers.levelDelta", "-6", EventValidator.LevelDeltaRule)]
    [InlineData("action.modifiers.levelDelta", "2.5", EventValidator.LevelDeltaRule)]
    [InlineData("action.modifiers.levelDelta", "+", EventValidator.LevelDeltaRule)]
    [InlineData("action.modifiers.maxHealth", "0.49", "action.modifiers.maxHealth must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.modifiers.power", "3.01", "action.modifiers.power must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.modifiers.moveSpeed", "1.234", "action.modifiers.moveSpeed must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.modifiers.attackSpeed", "1.", "action.modifiers.attackSpeed must be a number 0.5-3.0 with at most two decimals")]
    [InlineData("action.loot", "True", "action.loot must be true or false")]
    [InlineData("action.allowTerritory", "1", "action.allowTerritory must be true or false")]
    [InlineData("action.behaviour", "guard", "unknown behaviour type guard")]
    [InlineData("action.behaviour", "Ambush 20", "unknown behaviour type ambush")]
    [InlineData("action.behaviour", "hunt", EventValidator.HuntRangeRule)]
    [InlineData("action.behaviour", "hunt 9", EventValidator.HuntRangeRule)]
    [InlineData("action.behaviour", "hunt 61", EventValidator.HuntRangeRule)]
    [InlineData("action.behaviour", "hunt 20 30", CommandArgs.BehaviourRule)]
    [InlineData("action.behaviour", "{hunt}", CommandArgs.BehaviourRule)]
    [InlineData("location", "aroundplayer 30 30", EventValidator.DistOrder)]
    [InlineData("location", "aroundplayer 61 70", EventValidator.MinDistRule)]
    [InlineData("location", "aroundplayer 20 14", EventValidator.MaxDistRule)]
    [InlineData("location", "aroundplayer 20", "location takes here or aroundplayer MIN MAX")]
    public void Spawns_fails_when_value_out_of_range(string field, string value, string error)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.False(arg.Ok);
        Assert.Equal(error, arg.Error);
    }

    [Theory]
    [InlineData("action.spawnVisual")]
    [InlineData("action.units.N.chance")]
    [InlineData("action.units.0.chance")]
    [InlineData("action.units.01.chance")]
    [InlineData("action.units.11.chance")]
    [InlineData("action.units.1.count")]
    [InlineData("action.modifiers.armor")]
    [InlineData("action.modifiers")]
    [InlineData("action.behaviour.range")]
    [InlineData("action.location")]
    public void Spawns_fails_when_field_not_settable(string field)
    {
        Assert.False(CommandArgs.IsSettable(field));
        Assert.Equal($"field {field} is not settable; edit events.json and reload", CommandArgs.SettableValue(field, "1").Error);
    }

    [Theory]
    [InlineData("action.units.1.chance", "0.05", "0.05")]
    [InlineData("action.units.10.chance", "1", "1")]
    [InlineData("action.units.3.chance", "0.5", "0.5")]
    [InlineData("action.modifiers.level", "1", "1")]
    [InlineData("action.modifiers.level", "120", "120")]
    [InlineData("action.modifiers.levelDelta", "-5", "-5")]
    [InlineData("action.modifiers.levelDelta", "+3", "3")]
    [InlineData("action.modifiers.levelDelta", "0", "0")]
    [InlineData("action.modifiers.maxHealth", "0.5", "0.5")]
    [InlineData("action.modifiers.power", "3", "3")]
    [InlineData("action.modifiers.attackSpeed", "1.25", "1.25")]
    [InlineData("action.modifiers.moveSpeed", "none", "none")]
    [InlineData("action.modifiers.level", "none", "none")]
    [InlineData("action.loot", "true", "True")]
    [InlineData("action.allowTerritory", "false", "False")]
    [InlineData("action.behaviour", "hunt 10", "hunt 10")]
    [InlineData("action.behaviour", "HUNT 60", "hunt 60")]
    [InlineData("action.behaviour", "none", "none")]
    [InlineData("location", "aroundplayer 10 15", "aroundplayer 10 15")]
    [InlineData("location", "AroundPlayer 60 80", "aroundplayer 60 80")]
    public void Spawns_passes_values_in_range(string field, string value, string shown)
    {
        var arg = CommandArgs.SettableValue(field, value);
        Assert.True(arg.Ok, arg.Error);
        Assert.Equal(shown, Convert.ToString(arg.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(CommandArgs.IsSettable(field));
        if (value == "none") Assert.Same(FieldRemoval.Instance, arg.Value);
    }

    public static TheoryData<string> SpawnFields()
    {
        var data = new TheoryData<string>();
        foreach (var f in CommandArgs.SpawnKeyFields.Select(f => f.Replace(".N.", ".1.", StringComparison.Ordinal)).Append("location")) data.Add(f);
        return data;
    }

    [Theory]
    [MemberData(nameof(SpawnFields))]
    public void Spawns_empty_value(string field)
    {
        Assert.False(CommandArgs.SettableValue(field, null).Ok);
        Assert.False(CommandArgs.SettableValue(field, "").Ok);
        Assert.Equal("value required", CommandArgs.SettableValue(field, "").Error);
    }

    // ---- D32 ChatBytes

    static readonly string MaxId = new('a', EventLibraryIdMax);
    const int EventLibraryIdMax = 32;
    static readonly string MaxBehaviour = new('x', CommandArgs.MaxNameLength - "CHAR_".Length);    // the longest type chat can name
    static readonly string MaxUnit = "CHAR_" + new string('U', CommandArgs.MaxNameLength - "CHAR_".Length);

    static string LocationRule => Json.One(Json.Event(action: Json.ValidAction.Replace("\"Point\"", "\"Zone\""))).DisabledReason!;

    /// <summary>The D6 reasons and D18 refusals this child adds, at the given lengths of their fields.</summary>
    static IEnumerable<string> Reasons(string type) =>
    [
        EventValidator.ChanceRule, EventValidator.EmptyModifiers, EventValidator.BothLevels, EventValidator.LevelRule, EventValidator.LevelDeltaRule,
        EventValidator.HuntRangeRule, EventValidator.MaxDistOverRange, EventValidator.MinDistRule, EventValidator.MaxDistRule, EventValidator.DistOrder,
        .. EventValidator.ModifierKeys.Select(EventValidator.ModifierRule), EventValidator.UnknownBehaviour(type), CommandArgs.BehaviourRule,
        CommandArgs.UnitChanceRule, "action.loot must be true or false", "action.allowTerritory must be true or false", "unknown field action.spawnVisual",
        $"{EventValidator.BothLevels}: set action.modifiers.levelDelta none first", "action.units has no entry 10",
        "location takes here or aroundplayer MIN MAX", LocationRule,
    ];

    /// <summary>Every new line of the child, built from its template: the D18 set replies (each with the longest reload
    /// reason), refusals, the D6 reasons as `.nyar event list` shows them, and the wave planners' lines.</summary>
    static List<string> NewLines(string id, string value, string type, int wave)
    {
        var reasons = Reasons(type).ToList();
        var longest = reasons.OrderByDescending(r => Encoding.UTF8.GetByteCount(r)).First();
        var fields = CommandArgs.SpawnKeyFields.Select(f => f.Replace(".N.", ".10.", StringComparison.Ordinal)).Append("location").ToList();
        var lines = new List<string>();
        lines.AddRange(fields.Select(f => $"event {id} {f} = {value}; now disabled: {longest}"));
        lines.AddRange(fields.Select(f => $"{CommandArgs.TableName(f)} is not an Empower field"));
        lines.AddRange(reasons);
        lines.AddRange(reasons.Select(r => $"{id} {Readiness.Invalid}{r}"));
        lines.AddRange(
        [
            WaveLines.ZeroRolled(wave, id), WaveLines.NoEligiblePlayer(wave, id), WaveLines.CentreClaimed(wave, id),
            WaveLines.TerritoryUnknown(wave, id), WaveLines.PlayerQueryFailed(wave, id),
            // `.nyar debug here` with D10's readings (A46), every field at its widest, with the widest recipe and flag
            AdminLines.DebugUnit(MaxUnit, id, int.MaxValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue,
                -float.MaxValue, -float.MaxValue, (int.MinValue, int.MinValue)) + " " + AdminLines.Recipe(false, false, false, true) + " UNMARKED",
        ]);
        return lines;
    }

    static List<string> OverLimit(IEnumerable<string> lines) => lines.Where(l => Encoding.UTF8.GetByteCount(l) > Wire.MaxBytes).ToList();

    [Fact]
    public void ChatBytes_fails_when_line_exceeds_480()
    {
        var planted = $"event {MaxId} action.behaviour = " + new string('x', 480);
        Assert.Equal([planted], OverLimit(NewLines(MaxId, "aroundplayer 60 80", MaxBehaviour, 10).Append(planted)));
        var edge = new string('é', 240);                                       // 480 bytes: at the limit, not over
        Assert.Empty(OverLimit([edge]));
        Assert.Single(OverLimit([edge + "x"]));
        // the production control (Codex step 1 round 2 F3): a behaviour type of 480 characters would make the reason
        // over-long, so the validator leaves it out of the reason it writes and `.nyar event list` shows
        var huge = new string('x', 480);
        var reason = Json.One(Json.Event(action: Json.ValidAction.TrimEnd('}') + $",\"behaviour\":{{\"type\":\"{huge}\",\"range\":30}}}}")).DisabledReason!;
        Assert.Equal("unknown behaviour type", reason);
        Assert.Empty(OverLimit(NewLines(MaxId, "aroundplayer 60 80", huge, 10)));
        // an unknown field name of 480 characters, at every level that names one (Codex step 1 round 3 F1, A53)
        EventDefinition Def(string action) => Json.One(Json.Event(MaxId, action: action));
        string Reason(string action) => Def(action).DisabledReason!;
        var bare = Json.ValidAction.TrimEnd('}');
        foreach (var (action, expected) in new[]
        {
            (bare + $",\"modifiers\":{{\"{huge}\":1}}}}", "unknown field in action.modifiers"),
            (bare + $",\"behaviour\":{{\"type\":\"Hunt\",\"range\":30,\"{huge}\":1}}}}", "unknown field in action.behaviour"),
            (bare + $",\"{huge}\":1}}", "unknown field in action"),
        })
        {
            var r = Reason(action);
            Assert.Equal(expected, r);
            Assert.Empty(OverLimit([$"{MaxId} {Readiness.Invalid}{r}", .. EventLines.Info(Def(action), null, DateTime.UtcNow)]));
        }
        Assert.Equal("unknown field", Json.One(Json.Event(MaxId).TrimEnd('}') + $",\"{huge}\":1}}").DisabledReason);
        // Review 24 F5, F6: the stats level, and every reason that echoes an input value (unit, faction, types, pillar)
        foreach (var (json, expected) in new[]
        {
            (Json.Empower(MaxId, action: Json.EmpowerAction(stats: $"{{\"{huge}\":1.3}}")), "unknown field in action.stats"),
            (Json.Empower(MaxId, action: Json.EmpowerAction(factions: $"[\"Faction_{huge}\"]")), "unknown faction"),
            (Json.Event(MaxId, action: Json.ValidAction.Replace("CHAR_Bandit_Thug", "CHAR_" + huge)), "unknown unit"),
            (Json.Event(MaxId, action: Json.ValidAction.Replace("\"SpawnWaves\"", $"\"{huge}\"")), "unknown action type"),
            (Json.Event(MaxId, trigger: $"{{ \"type\": \"{huge}\" }}"), "unknown trigger type"),
            (Json.Event(MaxId).Replace("\"pillar\": \"spawns\"", $"\"pillar\": \"{huge}\""), "unknown pillar"),
            (Json.Event(MaxId, action: Json.ValidAction.Replace("CHAR_Bandit_Thug", "CHAR_<b>x</b>")), "unknown unit"),
        })
        {
            var d = Json.One(json);
            Assert.Equal(expected, d.DisabledReason);
            Assert.Empty(OverLimit(EventLines.Info(d, null, DateTime.UtcNow)));
        }
        Assert.Equal("unknown unit CHAR_Nope", Json.One(Json.Event(MaxId, action: Json.ValidAction.Replace("CHAR_Bandit_Thug", "CHAR_Nope"))).DisabledReason);
        Assert.Equal("unknown field action.modifiers.hp", EventValidator.UnknownField("action.modifiers", "hp"));   // a short name stays
    }

    static List<string> Markup(IEnumerable<string> lines) => lines.Where(l => l.IndexOfAny(['<', '>']) >= 0).ToList();

    [Fact]
    public void ChatBytes_fails_when_line_holds_markup()
    {
        // The game's chat reads <n> as a rich-text tag and drops it (Session 1: "action.units..chance", A65).
        var planted = "action.units.<n>.chance must be 0.05-1.0 with at most two decimals";
        Assert.Equal([planted], Markup(NewLines(MaxId, "aroundplayer 60 80", "<b>", 10).Append(planted)));
        Assert.Single(Markup(["hunt >"]));
        Assert.Equal("unknown behaviour type", EventValidator.UnknownBehaviour("<b>"));   // a type from the file is left out
        Assert.Empty(Markup([CommandArgs.UnitChanceField, CommandArgs.UnitChanceRule, CommandArgs.BehaviourRule, CommandArgs.LocationSetRule]));
    }

    /// <summary>Each `usage:` and `description:` string of the [Command] attributes in <paramref name="source"/>.</summary>
    static List<(string Key, string Text)> AttributeStrings(string source) =>
        System.Text.RegularExpressions.Regex.Matches(source, "\\b(usage|description):\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();

    [Fact]
    public void ChatBytes_fails_when_usage_holds_markup()
    {
        // VCF's .help prints each usage attribute raw, and the chat drops `<id>` as a tag (A68).
        Assert.Equal(["<id>"], Markup(AttributeStrings("[" + "Command(\"x\", usage: \"<id>\", description: \"d\")]").Select(a => a.Text)));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        var commands = Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Commands");
        var strings = Directory.GetFiles(commands, "*.cs").SelectMany(f => AttributeStrings(File.ReadAllText(f))).ToList();
        Assert.Contains(strings, a => a.Key == "usage");
        Assert.Empty(Markup(strings.Select(a => a.Text)));

        // Without usage:, VCF writes a [Remainder] parameter as "<name...>" (Review 34 F6).
        Assert.Equal(["Say"], GeneratedMarkup("[" + "Command(\"say\")]\n    public static void Say(ChatCommandContext ctx, [Remainder] string text)"));
        var sources = Directory.GetFiles(commands, "*.cs").Select(File.ReadAllText).ToList();
        Assert.Empty(sources.SelectMany(GeneratedMarkup));
        // Every [Command] without usage: is one the scan read, so none is skipped by its shape (Review 35 F1).
        var noUsage = sources.Sum(s => System.Text.RegularExpressions.Regex.Matches(s, @"\[Command\((?![^\]]*usage:)").Count);
        Assert.True(noUsage > 0);
        Assert.Equal(noUsage, sources.Sum(s => NoUsageCommands(s).Count()));
    }

    static IEnumerable<System.Text.RegularExpressions.Match> NoUsageCommands(string source) =>
        System.Text.RegularExpressions.Regex.Matches(source, @"\[Command\(([^\]]*)\]\s*public static void (\w+)\(([^)]*)\)")
            .Where(m => !m.Groups[1].Value.Contains("usage:", StringComparison.Ordinal));

    /// <summary>The [Command] methods without `usage:` whose parameters hold a [Remainder] one.</summary>
    static List<string> GeneratedMarkup(string source) =>
        NoUsageCommands(source).Where(m => m.Groups[3].Value.Contains("[Remainder]", StringComparison.Ordinal)).Select(m => m.Groups[2].Value).ToList();

    [Fact]
    public void ChatBytes_passes_new_lines_at_maximum_lengths()
    {
        var lines = NewLines(MaxId, "aroundplayer 60 80", MaxBehaviour, 10);
        var def = new EventDefinition(MaxId, new string('N', 40), true, Pillar.Spawns, Trigger.Manual(), new Conditions(), 7200,
            new SpawnWavesAction([new UnitEntry(MaxUnit, 50, 0.55)], 10, 600, 30, new Location(LocationType.AroundPlayer, 0, 0, null, 60, 80), 7200,
                Modifiers: new SpawnModifiers(null, -5, 2.75, 2.75, 2.75, 2.75), Loot: true, Behaviour: new Behaviour(BehaviourType.Hunt, 60),
                AllowTerritory: true),
            Announce.None);
        var spawn = EventLines.SpawnKeys(def.Action!)!;
        Assert.Equal("spawn: modifiers levelDelta -5, maxHealth x2.75, power x2.75, moveSpeed x2.75, attackSpeed x2.75; chance #1 0.55; loot on; hunt 60 m; " +
            "claimed territory allowed", spawn);
        lines.AddRange(EventLines.Info(def, null, DateTime.UtcNow));
        Assert.Contains(spawn, lines);
        Assert.Contains(lines, l => l.Contains("around a player 60-80 m", StringComparison.Ordinal));
        Assert.True(lines.Count > 60);
        Assert.Empty(OverLimit(lines));
        Assert.Empty(Markup(lines));

        // Ten distinct maximum-length units (Codex step 1 F1): the unit list moves to packed "units:" lines, none dropped.
        var ten = Enumerable.Range(0, 10).Select(i => new UnitEntry(MaxUnit[..^1] + i, 50, 0.55)).ToList();
        var info = EventLines.Info(def with { Action = def.Action! with { Units = ten } }, null, DateTime.UtcNow);
        Assert.Contains(info, l => l.StartsWith("action: 10 waves every 600s", StringComparison.Ordinal) && l.EndsWith(", unit lifetime 7200s, units below", StringComparison.Ordinal));
        var unitLines = info.Where(l => l.StartsWith("units: ", StringComparison.Ordinal)).ToList();
        Assert.True(unitLines.Count > 1);
        Assert.All(ten, u => Assert.Single(unitLines, l => l.Contains($"50 {u.Prefab}", StringComparison.Ordinal)));
        Assert.Empty(OverLimit(info));
    }

    [Fact]
    public void ChatBytes_empty_fields()
    {
        var lines = NewLines("", "", "", 0);
        Assert.NotEmpty(lines);
        Assert.Contains("wave 0 of : 0 units rolled", lines);
        Assert.All(lines, l => Assert.InRange(Encoding.UTF8.GetByteCount(l), 1, Wire.MaxBytes));
        Assert.Null(EventLines.SpawnKeys(new SpawnWavesAction([new UnitEntry(MaxUnit, 1)], 1, 10, 2, new Location(LocationType.Admin, 0, 0), null)));
    }
}
