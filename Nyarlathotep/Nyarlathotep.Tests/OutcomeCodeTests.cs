using System.Reflection;
using Nyarlathotep.Logic;
using static Nyarlathotep.Tests.HumanReplyTests;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D2: each refusal of the D1 paths carries the code, arg and reason of the plan's Business
/// rules 3 table (A13's rows included), driven through the real Logic path over the fakes. Each case names its table
/// row by the row's "Human text" cell; every row of the table must have a case.</summary>
public class OutcomeCodeTests
{
    public sealed record Case(string Row, string Name, Func<Outcome?> Run, RefusalCode Code, string? Arg, string? Reason, int? Secs = null);

    static readonly DateTime Now = Zones.Utc(2026, 9, 24, 20, 0);

    static Outcome? Flow(Func<Rig, Outcome?> run, Rig? rig = null) => run(rig ?? Default());

    static Rig Cooled()
    {
        var r = Default();
        r.Ops.PurgeableUnits = 1;
        Assert.True(r.Flows.PurgeAsk(r.Who).Ok);
        Assert.True(r.Flows.PurgeConfirm(r.Who).Ok);
        return r;
    }

    static Rig Pillarless() => new(Library.Of(Json.Event("raid")), pillarsOn: false);

    static Rig Templated(TemplateCatalog? catalog = null)
    {
        var r = TemplateRig();
        r.Ops.Templates = catalog ?? Templates(r.Lib);
        return r;
    }

    static readonly Case[] Cases =
    [
        new("purge cooldown active", "admin start in the cooldown", () => Flow(r => r.Flows.Start(r.Who, "raid"), Cooled()), RefusalCode.Cooldown, null, null, 300),
        new("purge cooldown active", "the blocker itself", () =>
        {
            var r = Default();
            return Precedence.StartBlocker(r.Lib.Catalog.Current.Find("raid")!, r.Ops.Controls() with { PurgeCooldownActive = true });
        }, RefusalCode.Cooldown, null, null),
        new("General.Enabled is false", "master off", () =>
        {
            var r = Default();
            r.Ops.Pillars.GeneralEnabled = false;
            return r.Flows.Start(r.Who, "raid");
        }, RefusalCode.Disabled, null, Reasons.General),
        new("pillar <p> is off", "pillar off", () => Flow(r => r.Flows.Start(r.Who, "raid"), Pillarless()), RefusalCode.Disabled, "pillar", Reasons.PillarOff),
        new("skipped by MaxConcurrentEvents", "cap", () =>
        {
            var r = Default();
            r.Ops.MaxConcurrent = 0;
            return r.Flows.Start(r.Who, "raid");
        }, RefusalCode.Limit, null, Reasons.MaxConcurrent),
        new("event <id> is disabled[: <reason>]", "invalid", () => Flow(r => r.Flows.Start(r.Who, "bad"), Of(UnknownUnit("bad"))), RefusalCode.State, "id", Reasons.Disabled),
        new("event <id> is disabled[: <reason>]", "switched off", () => Flow(r => r.Flows.Start(r.Who, "off"), Of(Disabled(Json.Event("off")))), RefusalCode.State, "id", Reasons.Disabled),
        new("unknown event <id>", "engine", () => Flow(r => r.Flows.Start(r.Who, "nope")), RefusalCode.NotFound, "id", null),
        new("unknown event <id>", "catalog", () => Default().Lib.Catalog.TryStart("nope", Now, out _), RefusalCode.NotFound, "id", null),
        new("already active", "engine", () => Flow(r =>
        {
            Assert.True(r.Flows.Start(r.Who, "raid").Ok);
            return r.Flows.Start(r.Who, "raid");
        }), RefusalCode.State, "id", Reasons.AlreadyActive),
        new("already active", "catalog", () =>
        {
            var lib = Default().Lib;
            Assert.Null(lib.Catalog.TryStart("raid", Now, out _));
            return lib.Catalog.TryStart("raid", Now, out _);
        }, RefusalCode.State, "id", Reasons.AlreadyActive),
        new("faction or unit already empowered by <id>", "faction", () => Flow(r =>
        {
            Assert.True(r.Flows.Start(r.Who, "a").Ok);
            return r.Flows.Start(r.Who, "b");
        }, Of(Emp("a", "[\"Faction_Bandits\"]"), Emp("b", "[\"Faction_Bandits\"]"))), RefusalCode.State, "id", Reasons.EmpowerClash),
        new("faction or unit already empowered by <id>", "unit", () => Flow(r =>
        {
            Assert.True(r.Flows.Start(r.Who, "a").Ok);
            return r.Flows.Start(r.Who, "b");
        }, Of(Emp("a", "[\"Faction_Legion\"]", "\"includeUnits\": [\"CHAR_Bandit_Thug\"]"), Emp("b", "[\"Faction_Undead\"]", "\"includeUnits\": [\"CHAR_Bandit_Thug\"]"))),
            RefusalCode.State, "id", Reasons.EmpowerClash),
        new("spawns at the admin: start it with .nyar event start", "no origin", () =>
        {
            var r = Of(AdminEvent("adm"));
            return r.Ops.Engine.Start("adm", "manual", r.Ops.Now, r.Ops.Controls());
        }, RefusalCode.BadArg, "location", Reasons.AdminLocation),
        new("your position could not be read", "start", () =>
        {
            var r = Of(AdminEvent("adm"));
            r.Position = null;
            return r.Flows.Start(r.Who, "adm");
        }, RefusalCode.BadArg, "location", Reasons.NoPosition),
        new("off; a condition blocker (players, mode, window, cooldown, chance)", "off", () => AdminLines.SystemOff, RefusalCode.Disabled, null, Reasons.General),
        new("off; a condition blocker (players, mode, window, cooldown, chance)", "condition", () => AdminLines.ConditionBlocked("too few players"),
            RefusalCode.State, null, Reasons.Condition),
        new("not active", "stop", () => Flow(r => r.Flows.Stop(r.Who, "raid")), RefusalCode.State, "id", Reasons.NotActive),
        new("unknown event / has no <part>", "unknown", () => Flow(r => r.Flows.Set(r.Who, "nope", "name", "x")), RefusalCode.NotFound, "id", null),
        new("unknown event / has no <part>", "no action", () => Flow(r => r.Flows.Set(r.Who, "bare", "action.waves", "2"),
            Of(Json.Event("bare").Replace(", " + Json.ValidAction, "", StringComparison.Ordinal))), RefusalCode.NotFound, "field", null),
        new("not a field, unsupported value, needs a trigger, stats rule", "not a field", () => Flow(r => r.Flows.Set(r.Who, "surge", "action.waves", "2")),
            RefusalCode.Invalid, "field", Reasons.Field),
        new("not a field, unsupported value, needs a trigger, stats rule", "location", () => Flow(r => r.Flows.Set(r.Who, "surge", "location", "here")),
            RefusalCode.Invalid, "field", Reasons.Field),
        new("not a field, unsupported value, needs a trigger, stats rule", "unsupported", () => Default().Ops.OpEdit("raid", "durationSeconds", new object()),
            RefusalCode.Invalid, "field", Reasons.Value),
        new("not a field, unsupported value, needs a trigger, stats rule", "trigger", () => Flow(r => r.Flows.Set(r.Who, "raid", "trigger.phase", "day")),
            RefusalCode.Invalid, "field", Reasons.Trigger),
        new("not a field, unsupported value, needs a trigger, stats rule", "stats", () => Flow(r => r.Flows.Set(r.Who, "surge", "action.stats.physicalPower", "1.0")),
            RefusalCode.Invalid, "field", Reasons.Stats),
        new("could not be read / not found / does not parse / no events array", "read", () => Flow(r =>
        {
            r.Lib.Fs.FailReads = true;
            return r.Flows.Enable(r.Who, "raid", false);
        }), RefusalCode.Io, null, Reasons.Read),
        new("could not be read / not found / does not parse / no events array", "not found", () => Flow(r =>
        {
            r.Lib.Fs.Delete(DataFile.Events, FileVariant.Main);
            return r.Flows.Enable(r.Who, "raid", false);
        }), RefusalCode.Io, null, Reasons.Read),
        new("could not be read / not found / does not parse / no events array", "reload of a broken file", () => Flow(r =>
        {
            r.Lib.Fs.Put(DataFile.Events, FileVariant.Main, "{ broken");
            return r.Flows.ReloadEvents(r.Who);
        }), RefusalCode.Io, null, Reasons.Parse),
        new("could not be read / not found / does not parse / no events array", "does not parse", () => Authoring.Copy("{ broken", "raid", "raid-2").Refusal,
            RefusalCode.Io, null, Reasons.Parse),
        new("could not be read / not found / does not parse / no events array", "no events array", () =>
        {
            EventsEditor.Apply("{ \"SchemaVersion\": 1 }", "raid", "name", "x", out var error);
            return error;
        }, RefusalCode.Io, null, Reasons.Parse),
        new("stale, read-only (newer schema)", "stale", () => Flow(r =>
        {
            r.Lib.Fs.Put(DataFile.Events, FileVariant.Main, "{ broken");
            return r.Flows.Enable(r.Who, "raid", false);
        }), RefusalCode.Io, null, Reasons.Stale),
        new("stale, read-only (newer schema)", "read-only", () => Flow(r => r.Flows.Enable(r.Who, "raid", false),
            new Rig(new Library(Json.File(Json.Event("raid")).Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2", StringComparison.Ordinal)))),
            RefusalCode.Io, null, Reasons.ReadOnly),
        new("would exceed 1 MB", "copy", () =>
        {
            string[] Events(int nameLength) =>
                Enumerable.Range(0, 180).Select(i => Json.Event($"e{i:000}").Replace("\"Bandit raid\"", $"\"{new string('n', nameLength)}\"")).ToArray();
            var bare = System.Text.Encoding.UTF8.GetByteCount(Json.File(Events(0)));
            var r = new Rig(new Library(Json.File(Events((1_040_000 - bare) / 180)), AuthoringTests.Units()));
            return r.Flows.Copy(r.Who, "e000", "e-copy");
        }, RefusalCode.Full, null, Reasons.Size),
        new("write uncertain", "promote threw", () => Flow(r =>
        {
            r.Lib.Fs.ThrowAfterPromote = true;
            return r.Flows.New(r.Who, "fresh", "spawns");
        }), RefusalCode.Io, null, Reasons.WriteUncertain),
        new("write uncertain", "unreadable after", () => Flow(r =>
        {
            r.Lib.Fs.ThrowAfterPromote = true;
            r.Lib.Fs.BeforePromote = () => r.Lib.Fs.FailReads = true;
            return r.Flows.Enable(r.Who, "raid", false);
        }), RefusalCode.Io, null, Reasons.WriteUncertain),
        new("id exists", "new", () => Flow(r => r.Flows.New(r.Who, "raid", "spawns")), RefusalCode.Exists, "id", null),
        new("id exists", "copy", () => Flow(r => r.Flows.Copy(r.Who, "raid", "surge")), RefusalCode.Exists, "newId", null),
        new("id exists", "template", () => Flow(r =>
        {
            Assert.True(r.Flows.TemplateUse(r.Who, "bandit-ambush", null).Ok);
            return r.Flows.TemplateUse(r.Who, "bandit-ambush", null);
        }, Templated()), RefusalCode.Exists, "id", null),
        new("holds 200 definitions", "new", () => Flow(r => r.Flows.New(r.Who, "one-more", "spawns"),
            Of(Enumerable.Range(0, EventValidator.MaxDefinitions).Select(i => Json.Event($"e{i:000}")).ToArray())), RefusalCode.Full, null, Reasons.Count),
        new("id must be 1-32 of a-z 0-9 -", "new", () => Flow(r => r.Flows.New(r.Who, "Bad_Id", "spawns")), RefusalCode.BadArg, "id", null),
        new("id must be 1-32 of a-z 0-9 -", "copy", () => Flow(r => r.Flows.Copy(r.Who, "raid", "Bad_Id")), RefusalCode.BadArg, "newId", null),
        new("id must be 1-32 of a-z 0-9 -", "template as", () => Flow(r => r.Flows.TemplateUse(r.Who, "bandit-ambush", "Bad_Id"), Templated()),
            RefusalCode.BadArg, "id", null),
        new("template catalogue unavailable", "use", () => Flow(r => r.Flows.TemplateUse(r.Who, "bandit-ambush", null), TemplateRig()),
            RefusalCode.Io, null, Reasons.Read),
        new("template <t> is invalid: <reason>", "use", () =>
        {
            var units = TemplateLibraryTests.Units();
            units.Factions.Remove("Faction_Legion");
            var r = Templated(TemplateCatalog.Load(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), units, units));
            return r.Flows.TemplateUse(r.Who, "legion-weekend-surge", null);
        }, RefusalCode.Invalid, "template", Reasons.Template),
        new("edit refused: <exception> (the edit plan threw)", "threw", () => Default().Ops.OpAuthor(_ => throw new InvalidOperationException("boom")),
            RefusalCode.Io, null, Reasons.Internal),
        new("events.json write failed (the file untouched)", "disk full", () => Flow(r =>
        {
            r.Lib.Fs.FailWrites = true;
            return r.Flows.Enable(r.Who, "raid", false);
        }), RefusalCode.Io, null, Reasons.Save),
        new("<done>; <reload error> (written, but the reload failed)", "parse", () => Default().Ops.OpAuthor(_ => new EditPlan("{ broken", null, "wrote it", null)),
            RefusalCode.Io, null, Reasons.Parse),
        new("unknown pillar / unknown template", "new", () => Flow(r => r.Flows.New(r.Who, "fresh", "nope")), RefusalCode.NotFound, "pillar", null),
        new("unknown pillar / unknown template", "template", () => Flow(r => r.Flows.TemplateUse(r.Who, "nope", null), Templated()), RefusalCode.NotFound, "template", null),
        new("running; stop it first", "ask", () => Flow(r =>
        {
            Assert.True(r.Flows.Start(r.Who, "raid").Ok);
            return r.Flows.DeleteEvent(r.Who, "raid", false);
        }), RefusalCode.State, "id", Reasons.Running),
        new("running; stop it first", "confirm", () => Flow(r =>
        {
            Assert.True(r.Flows.DeleteEvent(r.Who, "raid", false).Ok);
            Assert.True(r.Flows.Start(r.Who, "raid").Ok);
            return r.Flows.DeleteEvent(r.Who, "raid", true);
        }), RefusalCode.State, "id", Reasons.Running),
        new("no delete pending", "confirm", () => Flow(r => r.Flows.DeleteEvent(r.Who, "raid", true)), RefusalCode.Confirm, null, null),
        new("unknown pillar / use on or off", "unknown", () => Flow(r => r.Flows.Pillar(r.Who, "nope", "on")), RefusalCode.NotFound, "pillar", null),
        new("unknown pillar / use on or off", "state", () => Flow(r => r.Flows.Pillar(r.Who, "spawns", "maybe")), RefusalCode.BadArg, "state", null),
        new("could not read or save the cfg (a failed save still ends events when the file says off, D5)", "read", () => Flow(r =>
        {
            r.Ops.Pillars.FailReload = true;
            return r.Flows.Pillar(r.Who, "spawns", "on");
        }, Pillarless()), RefusalCode.Io, null, Reasons.Read),
        new("could not read or save the cfg (a failed save still ends events when the file says off, D5)", "save", () => Flow(r =>
        {
            r.Ops.Pillars.TruncateThenThrow = true;
            return r.Flows.Pillar(r.Who, "spawns", "on");
        }, Pillarless()), RefusalCode.Io, null, Reasons.Save),
        new("could not read or save the cfg (a failed save still ends events when the file says off, D5)", "save, read-back fails", () => Flow(r =>
        {
            r.Ops.Pillars.FailReloadAfterSet = true;
            r.Ops.Pillars.WriteThenThrow = true;
            return r.Flows.Pillar(r.Who, "spawns", "on");
        }, Pillarless()), RefusalCode.Io, null, Reasons.Save),
        new("nothing to purge", "ask", () => Flow(r => r.Flows.PurgeAsk(r.Who)), RefusalCode.State, null, Reasons.NothingToPurge),
        new("nothing to purge", "confirm", () => Flow(r => r.Flows.PurgeConfirm(r.Who)), RefusalCode.State, null, Reasons.NothingToPurge),
        new("run .nyar purge first (only when something is left to purge)", "confirm", () => Flow(r =>
        {
            r.Ops.PurgeableUnits = 3;
            return r.Flows.PurgeConfirm(r.Who);
        }), RefusalCode.Confirm, null, null),
        new("denied", "player", () => new ActionGateway(_ => { }).Run(ActionKind.Purge, Actor.Player, () => Outcome.Done("purged")), RefusalCode.NoAccess, null, null),
        new("a usage or argument refusal; an unknown verb; a surplus word", "bad id", () => Flow(r => r.Flows.Start(r.Who, "Bad Id")), RefusalCode.BadArg, "id", null),
        new("a usage or argument refusal; an unknown verb; a surplus word", "unknown field", () => Flow(r => r.Flows.Set(r.Who, "raid", "nope", "1")),
            RefusalCode.BadArg, "field", null),
        new("a usage or argument refusal; an unknown verb; a surplus word", "bad value", () => Flow(r => r.Flows.Set(r.Who, "raid", "durationSeconds", "soon")),
            RefusalCode.BadArg, "value", null),
        new("a usage or argument refusal; an unknown verb; a surplus word", "location here unread", () => Flow(r =>
        {
            r.Position = null;
            return r.Flows.Set(r.Who, "raid", "location", "here");
        }), RefusalCode.BadArg, "value", null),
    ];

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases) data.Add($"{c.Row} · {c.Name}");
        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void OutcomeCode_passes_each_refusal_row(string name)
    {
        var c = Cases.Single(x => $"{x.Row} · {x.Name}" == name);
        var o = c.Run();
        Assert.NotNull(o);
        Assert.False(o!.Ok, $"{name}: {o.Human}");
        Assert.Equal((c.Code, c.Arg, c.Reason, c.Secs), (o.Code!.Value, o.Arg, o.Reason, o.Secs));
        if (o.Reason is not null) Assert.Contains(o.Reason, Reasons.All);
        var row = PlanTable(Plan()).Single(r => r.Human == c.Row);
        Assert.True(row.Admits(o), $"{name}: ({o.Code}, {o.Arg ?? "—"}, {o.Reason ?? "—"}) is not in the plan's row ({row.Codes}, {row.Args}, {row.Reasons})");
    }

    static string Plan() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "raphael-api-admin.md"));

    /// <summary>One row of Business rules 3: its Human text and the code, arg and reason cells as written.</summary>
    internal sealed record PlanRow(string Human, string Codes, string Args, string Reasons)
    {
        /// <summary>True when the outcome's code, arg and reason are each one the row's cell allows.</summary>
        public bool Admits(Outcome o) =>
            Allowed(Codes) is { } codes && codes.Contains(o.Code.ToString()!.ToLowerInvariant())
            && (Allowed(Args) is not { } args || args.Contains(o.Arg ?? "—"))
            && (Allowed(Reasons) is not { } reasons || reasons.Contains(o.Reason ?? "—"));
    }

    /// <summary>The words a cell allows: "—" is none, alternatives split on "/", " or ", ";" and ","; a cell whose text is
    /// prose names its words in parentheses ("the reload's reason (read or parse)"), a parenthesis after words is a note
    /// ("cooldown (secs = time left)"); null when an alternative is prose with no list (any value: "the argument's name").</summary>
    internal static HashSet<string>? Allowed(string cell)
    {
        static string[] Split(string s) => System.Text.RegularExpressions.Regex.Split(s, @"\s*(?:/|;|,|\bor\b)\s*")
            .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        static bool Word(string s) => s == "—" || System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Za-z_]+$");
        var m = System.Text.RegularExpressions.Regex.Match(cell, @"^(?<out>[^(]*?)\s*(?:\((?<in>[^)]*)\))?\s*$");
        var outside = Split(m.Groups["out"].Value);
        if (outside.Length > 0 && outside.All(Word)) return outside.ToHashSet(StringComparer.Ordinal);
        if (m.Groups["in"].Success && Split(m.Groups["in"].Value) is { Length: > 0 } inside && inside.All(Word)) return inside.ToHashSet(StringComparer.Ordinal);
        return null;
    }

    /// <summary>The rows of Business rules 3 in docs/dod/raphael-api-admin.md (copied to the test output).</summary>
    internal static IReadOnlyList<PlanRow> PlanTable(string planText) =>
        PlanCells(planText).Select(c => new PlanRow(c[2], c[3], c[4], c[5])).ToList();

    /// <summary>The "Human text" cells of Business rules 3.</summary>
    internal static IReadOnlyList<string> PlanRows(string planText) => PlanCells(planText).Select(c => c[2]).ToList();

    static IReadOnlyList<string[]> PlanCells(string planText)
    {
        var lines = planText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("3. **Codes", StringComparison.Ordinal));
        Assert.True(start >= 0, "the plan has no Business rules 3");
        var rows = new List<string[]>();
        for (var i = start + 1; i < lines.Length && !lines[i].StartsWith("4. ", StringComparison.Ordinal); i++)
        {
            if (!lines[i].StartsWith('|')) continue;
            var cells = lines[i].Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 7 || cells[2] is "Human text (0.5.1)" || cells[2].StartsWith("---", StringComparison.Ordinal)) continue;
            rows.Add(cells);
        }
        Assert.True(rows.Count > 0, "Business rules 3 lists no row");
        return rows;
    }

    [Fact]
    public void OutcomeCode_passes_every_row_has_a_case()
    {
        var rows = PlanRows(Plan());
        var cased = Cases.Select(c => c.Row).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(Array.Empty<string>(), rows.Where(r => !cased.Contains(r)).ToArray());
        Assert.Equal(Array.Empty<string>(), cased.Where(r => !rows.Contains(r)).ToArray());      // a case names a row that exists
    }

    [Fact]
    public void OutcomeCode_empty_plan_table()
    {
        Assert.Contains("no Business rules 3", Assert.ThrowsAny<Exception>(() => PlanRows("## Business rules\n1. nothing\n")).Message);
        Assert.Contains("lists no row", Assert.ThrowsAny<Exception>(() => PlanRows("3. **Codes (4.1, D2):**\n\n4. **Idempotency\n")).Message);
    }

    [Fact]
    public void OutcomeCode_fails_when_cell_changes()
    {
        Assert.Equal(["cooldown"], Allowed("cooldown (secs = time left)")!);
        Assert.Equal(["read", "parse"], Allowed("the reload's reason (read or parse)")!);
        Assert.Equal(["field", "value", "trigger", "stats"], Allowed("the validator's word (field, value, trigger, stats)")!);
        Assert.Equal(["id", "newId"], Allowed("id or newId")!);
        Assert.Equal(["notfound", "badarg"], Allowed("notfound / badarg")!);
        Assert.Equal(["—"], Allowed("—")!);
        Assert.Null(Allowed("the argument's name; verb; extra"));
        var stale = Outcome.Refused("not active", RefusalCode.State, "id", reason: Reasons.NotActive);
        Assert.True(new PlanRow("not active", "state", "id", "not_active").Admits(stale));
        Assert.False(new PlanRow("not active", "notfound", "id", "not_active").Admits(stale));   // the code cell changed
        Assert.False(new PlanRow("not active", "state", "—", "not_active").Admits(stale));       // the arg cell changed
        Assert.False(new PlanRow("not active", "state", "id", "—").Admits(stale));               // the reason cell changed
    }

    /// <summary>The RefusalCode names without a WireError of the same name, and the WireError names without a RefusalCode.</summary>
    internal static IReadOnlyList<string> Unpaired(IEnumerable<string> refusals, IEnumerable<string> wires)
    {
        var r = refusals.ToHashSet(StringComparer.Ordinal);
        var w = wires.ToHashSet(StringComparer.Ordinal);
        return r.Except(w).Select(x => $"RefusalCode.{x} has no WireError").Concat(w.Except(r).Select(x => $"WireError.{x} has no RefusalCode")).ToList();
    }

    [Fact]
    public void OutcomeCode_passes_codes_map_one_to_one()
    {
        Assert.Empty(Unpaired(Enum.GetNames<RefusalCode>(), Enum.GetNames<WireError>()));
        foreach (var code in Enum.GetValues<RefusalCode>()) Assert.Equal(code.ToString(), Wire.Code(code).ToString());
    }

    [Fact]
    public void OutcomeCode_fails_when_a_code_has_no_twin()
    {
        Assert.Equal(["RefusalCode.Limit has no WireError"], Unpaired(Enum.GetNames<RefusalCode>(), Enum.GetNames<WireError>().Where(n => n != "Limit")));
        Assert.Equal(["WireError.Extra has no RefusalCode"], Unpaired(Enum.GetNames<RefusalCode>(), Enum.GetNames<WireError>().Append("Extra")));
    }

    [Fact]
    public void OutcomeCode_passes_reasons_closed_list()
    {
        var constants = typeof(Reasons).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(constants, Reasons.All.OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(Reasons.All, r => Assert.Matches("^[a-z]+(_[a-z]+)*$", r));
    }

    [Fact]
    public void OutcomeCode_fails_when_built_outside_factories()
    {
        Assert.Empty(typeof(Outcome).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Throws<ArgumentException>(() => Outcome.Done(""));
        Assert.Throws<ArgumentException>(() => Outcome.Refused("", RefusalCode.State));
        Assert.Throws<ArgumentException>(() => Outcome.Refused("x", RefusalCode.State, reason: "not a reason"));
        var refused = Outcome.Refused("x", RefusalCode.Io, "id", 3, Reasons.Read);
        Assert.Equal((false, "y", RefusalCode.Io, "id", 3, Reasons.Read), (refused.WithHuman("y").Ok, refused.WithHuman("y").Human,
            refused.WithHuman("y").Code!.Value, refused.WithHuman("y").Arg, refused.WithHuman("y").Secs, refused.WithHuman("y").Reason));
        var done = Outcome.Done("d", ("changed", "1"));
        Assert.Equal(("e", "1", (RefusalCode?)null), (done.WithHuman("e").Human, done.WithHuman("e").Field("changed"), done.WithHuman("e").Code));
        Assert.Null(done.Field("missing"));
    }
}
