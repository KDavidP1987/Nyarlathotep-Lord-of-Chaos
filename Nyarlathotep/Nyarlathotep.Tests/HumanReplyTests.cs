using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-admin D1: every human reply of the D1 paths is 0.5.1's reply for the same input. Each row of
/// Fixtures/human-replies-&lt;version&gt;.txt (the newest file per scenario id, A4) names a scenario here, the reply's
/// template and the v0.5.1 line it came from; the scenario runs the flow (AdminFlows over FakeAdminOps, or the member
/// itself where 0.5.1 had no command path) and its Outcome.Human must equal the template rendered with the scenario's
/// values. The HumanReplies preflight check proves each template against the cited v0.5.1 line.</summary>
public class HumanReplyTests
{
    static readonly Regex FileName = new(@"^human-replies-(\d+\.\d+\.\d+)\.txt$", RegexOptions.CultureInvariant);

    /// <summary>The capture rows, the newest file's row per scenario id.</summary>
    internal static IReadOnlyList<(string Id, string Template, string File)> Load(IEnumerable<(string Name, string Text)> files)
    {
        var rows = new Dictionary<string, (string Template, string File, Version Version)>(StringComparer.Ordinal);
        foreach (var (name, text) in files)
        {
            var m = FileName.Match(name);
            if (!m.Success) continue;
            var version = Version.Parse(m.Groups[1].Value);
            foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var parts = line.Split(" · ");
                Assert.True(parts.Length == 3, $"{name}: a row needs id · template · cite: {line}");
                if (!rows.TryGetValue(parts[0], out var had) || had.Version < version) rows[parts[0]] = (parts[1], name, version);
            }
        }
        if (rows.Count == 0) Assert.Fail("no captured replies");
        return rows.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => (r.Key, r.Value.Template, r.Value.File)).ToList();
    }

    static IEnumerable<(string, string)> Captures() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures"), "human-replies-*.txt")
            .Select(p => (Path.GetFileName(p), File.ReadAllText(p)));

    public static TheoryData<string, string, string> Rows()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (id, template, file) in Load(Captures())) data.Add(id, template, file);
        return data;
    }

    /// <summary>The template with each {name} replaced by the scenario's value and \n as a newline.</summary>
    internal static string Render(string template, IReadOnlyDictionary<string, string> values) =>
        Regex.Replace(template.Replace("\\n", "\n", StringComparison.Ordinal), @"\{(\w+)\}", m =>
            values.TryGetValue(m.Groups[1].Value, out var v) ? v : throw new KeyNotFoundException($"no value for {{{m.Groups[1].Value}}}"));

    [Theory]
    [MemberData(nameof(Rows))]
    public void HumanReply_passes_captured_rows(string id, string template, string file) =>
        Assert.Null(Mismatch(id, template) is { } m ? $"{file}: {m}" : null);

    /// <summary>Why scenario <paramref name="id"/>'s reply is not <paramref name="template"/> rendered, or null.</summary>
    static string? Mismatch(string id, string template)
    {
        if (!Scenarios.TryGetValue(id, out var scenario)) return $"row {id} has no scenario";
        var (human, values) = scenario();
        var want = Render(template, values);
        return want == human ? null : $"row {id}: the capture says \"{want}\", the flow replied \"{human}\"";
    }

    [Fact]
    public void HumanReply_fails_when_reply_differs()
    {
        foreach (var (id, template, _) in Load(Captures()).Take(5))
        {
            Assert.Null(Mismatch(id, template));
            Assert.NotNull(Mismatch(id, template + " (changed)"));
            Assert.NotNull(Mismatch(id, "x" + template));
        }
        Assert.Equal("row nowhere has no scenario", Mismatch("nowhere", "x"));
    }

    [Fact]
    public void HumanReply_passes_every_row_has_a_scenario()
    {
        var ids = Load(Captures()).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(Array.Empty<string>(), Scenarios.Keys.Where(k => !ids.Contains(k)).ToArray());
        Assert.Equal(Array.Empty<string>(), ids.Where(k => !Scenarios.ContainsKey(k)).ToArray());
    }

    [Fact]
    public void HumanReply_empty_capture()
    {
        Assert.Contains("no captured replies", Assert.ThrowsAny<Exception>(() => Load([("human-replies-0.5.1.txt", "# comments only\n")])).Message);
        Assert.Contains("no captured replies", Assert.ThrowsAny<Exception>(() => Load([])).Message);
    }

    [Fact]
    public void HumanReply_passes_newest_capture_wins()
    {
        var rows = Load([
            ("human-replies-0.5.1.txt", "a · old · x.cs:1\r\nb · kept · x.cs:2\r\n"),
            ("human-replies-0.10.0.txt", "a · new {n}\\nline · x.cs:3\n"),
            ("notes.txt", "c · ignored · x.cs:4\n"),
        ]);
        Assert.Equal([("a", "new {n}\\nline", "human-replies-0.10.0.txt"), ("b", "kept", "human-replies-0.5.1.txt")], rows);
        Assert.Equal("new 7\nline", Render(rows[0].Template, new Dictionary<string, string> { ["n"] = "7" }));
    }

    // ---- the scenarios

    static readonly DateTime Now = Zones.Utc(2026, 9, 24, 20, 0);

    /// <summary>The flows over one library, all pillars on unless a scenario turns them off.</summary>
    internal sealed class Rig
    {
        public Library Lib { get; }
        public FakeAdminOps Ops { get; }
        public AdminFlows Flows { get; }
        public (float X, float Y, float Z)? Position { get; set; } = (5f, 5f, 5f);
        public AdminCaller Who { get; }

        /// <summary>The flows' warnings (a twin's failure line, raphael-api-admin D12).</summary>
        public List<string> Warns { get; } = [];

        /// <summary>The flows over the fakes; the gateway records "gate &lt;kind&gt;" into Ops.Calls as a call enters it,
        /// and <paramref name="gate"/> is the twins' rate gate (by default one no test reaches).</summary>
        public Rig(Library lib, bool pillarsOn = true, RateGate? gate = null)
        {
            Lib = lib;
            Ops = new FakeAdminOps(lib);
            if (pillarsOn)
                foreach (var (_, pillar, _) in PillarNames.All) Ops.Pillars.Set(pillar, true);
            Flows = new AdminFlows(Ops, new ActionGateway(lib.Log.Add, (kind, _) => Ops.Calls.Add($"gate {kind}")), new PurgeArming(),
                lib.Log.Add, () => Ops.Now, gate ?? new RateGate(perSecond: 100_000), Warns.Add);
            Who = new AdminCaller(7, "Chaos", () => Position);
        }
    }

    internal static Rig Of(params string[] events) => new(Library.Of(events));
    internal static Rig Default() => Of(Json.Event("raid"), Json.Empower("surge"));

    internal static string Disabled(string ev) => ev.Replace("\"enabled\": true", "\"enabled\": false", StringComparison.Ordinal);
    internal static string UnknownUnit(string id) => Json.Event(id, action: Json.ValidAction.Replace("CHAR_Bandit_Thug", "CHAR_Nobody", StringComparison.Ordinal));
    internal static string AdminEvent(string id) =>
        Json.Event(id, action: Json.ValidAction.Replace("{ \"type\": \"Point\", \"x\": -1200.5, \"z\": -800 }", "{ \"type\": \"Admin\" }", StringComparison.Ordinal));
    internal static string Emp(string id, string factions, string? extra = null) => Json.Empower(id, action: Json.EmpowerAction(factions, extra: extra));

    static (string, IReadOnlyDictionary<string, string>) R(Outcome? o, params (string Key, string Value)[] values)
    {
        Assert.NotNull(o);
        return (o!.Human, values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal));
    }

    static (string, IReadOnlyDictionary<string, string>) R(string human, params (string Key, string Value)[] values) =>
        (human, values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal));

    internal static TemplateCatalog Templates(Library lib) => TemplateCatalog.Load(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), lib.Units, lib.Units);

    internal static Rig TemplateRig() => new(new Library(Json.File(Json.Event("raid")), TemplateLibraryTests.Units()));

    static readonly IReadOnlyDictionary<string, Func<(string Human, IReadOnlyDictionary<string, string> Values)>> Scenarios =
        new Dictionary<string, Func<(string, IReadOnlyDictionary<string, string>)>>(StringComparer.Ordinal)
        {
            // ---- the start controls (Precedence, Engine, EventCatalog)
            ["blocker-purge"] = () =>
            {
                var r = Default();
                return R(Precedence.StartBlocker(r.Lib.Catalog.Current.Find("raid")!, r.Ops.Controls() with { PurgeCooldownActive = true }));
            },
            ["start-general-off"] = () =>
            {
                var r = Default();
                r.Ops.Pillars.GeneralEnabled = false;
                return R(r.Flows.Start(r.Who, "raid"));
            },
            ["start-pillar-off"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                return R(r.Flows.Start(r.Who, "raid"), ("pillar", "spawns"));
            },
            ["start-max-concurrent"] = () =>
            {
                var r = Default();
                r.Ops.MaxConcurrent = 0;
                return R(r.Flows.Start(r.Who, "raid"));
            },
            ["start-invalid"] = () =>
            {
                var r = Of(UnknownUnit("bad"));
                return R(r.Flows.Start(r.Who, "bad"), ("id", "bad"), ("reason", r.Lib.Catalog.Current.Find("bad")!.DisabledReason!));
            },
            ["start-disabled"] = () =>
            {
                var r = Of(Disabled(Json.Event("off")));
                return R(r.Flows.Start(r.Who, "off"), ("id", "off"));
            },
            ["start-faction-clash"] = () =>
            {
                var r = Of(Emp("a", "[\"Faction_Bandits\"]"), Emp("b", "[\"Faction_Bandits\"]"));
                Assert.True(r.Flows.Start(r.Who, "a").Ok);
                return R(r.Flows.Start(r.Who, "b"), ("faction", "Bandits"), ("other", "a"));
            },
            ["start-unit-clash"] = () =>
            {
                var r = Of(Emp("a", "[\"Faction_Legion\"]", "\"includeUnits\": [\"CHAR_Bandit_Thug\"]"),
                    Emp("b", "[\"Faction_Undead\"]", "\"includeUnits\": [\"CHAR_Bandit_Thug\"]"));
                Assert.True(r.Flows.Start(r.Who, "a").Ok);
                return R(r.Flows.Start(r.Who, "b"), ("unit", "CHAR_Bandit_Thug"), ("other", "a"));
            },
            ["start-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.Start(r.Who, "nope"), ("id", "nope"));
            },
            ["start-already-active"] = () =>
            {
                var r = Default();
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                return R(r.Flows.Start(r.Who, "raid"));
            },
            ["engine-admin-no-origin"] = () =>
            {
                var r = Of(AdminEvent("adm"));
                return R(r.Ops.Engine.Start("adm", "manual", r.Ops.Now, r.Ops.Controls()), ("id", "adm"));
            },
            ["catalog-start-unknown"] = () => R(Default().Lib.Catalog.TryStart("nope", Now, out _), ("id", "nope")),
            ["catalog-start-active"] = () =>
            {
                var lib = Default().Lib;
                Assert.Null(lib.Catalog.TryStart("raid", Now, out _));
                return R(lib.Catalog.TryStart("raid", Now, out _));
            },
            ["catalog-end-inactive"] = () => R(Default().Lib.Catalog.TryEnd("raid")),
            ["start-no-position"] = () =>
            {
                var r = Of(AdminEvent("adm"));
                r.Position = null;
                return R(r.Flows.Start(r.Who, "adm"));
            },
            ["start"] = () =>
            {
                var r = Default();
                return R(r.Flows.Start(r.Who, "raid"), ("id", "raid"));
            },
            ["stop"] = () =>
            {
                var r = Default();
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                return R(r.Flows.Stop(r.Who, "raid"), ("id", "raid"));
            },
            ["stop-inactive"] = () =>
            {
                var r = Default();
                return R(r.Flows.Stop(r.Who, "raid"));
            },

            // ---- the edits (DefinitionEditor, EventsEditor)
            ["set-stale"] = () =>
            {
                var r = Default();
                r.Lib.Fs.Put(DataFile.Events, FileVariant.Main, "{ broken");
                return R(r.Flows.Enable(r.Who, "raid", false));
            },
            ["author-too-large"] = () =>
            {
                string[] Events(int nameLength) =>
                    Enumerable.Range(0, 180).Select(i => Json.Event($"e{i:000}").Replace("\"Bandit raid\"", $"\"{new string('n', nameLength)}\"")).ToArray();
                var bare = System.Text.Encoding.UTF8.GetByteCount(Json.File(Events(0)));
                var r = new Rig(new Library(Json.File(Events((1_040_000 - bare) / 180)), AuthoringTests.Units()));
                return R(r.Flows.Copy(r.Who, "e000", "e-copy"));
            },
            ["reload"] = () =>
            {
                var r = Of(Json.Event("raid"), Json.Empower("surge"), UnknownUnit("bad"));
                return R(r.Flows.ReloadEvents(r.Who), ("valid", "2"), ("disabled", "1"));
            },
            ["enable"] = () =>
            {
                var r = Of(Disabled(Json.Event("raid")));
                return R(r.Flows.Enable(r.Who, "raid", true), ("id", "raid"));
            },
            ["disable"] = () =>
            {
                var r = Default();
                return R(r.Flows.Enable(r.Who, "raid", false), ("id", "raid"));
            },
            ["set-field"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "raid", "durationSeconds", "300"), ("id", "raid"), ("field", "durationSeconds"), ("value", "300"));
            },
            ["edit-refused-fallback"] = () => R(DefinitionEditor.EditRefused("raid"), ("id", "raid")),
            ["read-fails"] = () =>
            {
                var r = Default();
                r.Lib.Fs.FailReads = true;
                return R(r.Flows.Enable(r.Who, "raid", false), ("error", "read failed"));
            },
            ["not-found"] = () =>
            {
                var r = Default();
                r.Lib.Fs.Delete(DataFile.Events, FileVariant.Main);
                return R(r.Flows.Enable(r.Who, "raid", false));
            },
            ["plan-threw"] = () =>
            {
                var r = Default();
                return R(r.Ops.OpAuthor(_ => throw new InvalidOperationException("boom")), ("error", "boom"));
            },
            ["plan-empty"] = () => R(Default().Ops.OpAuthor(_ => new EditPlan(null, null, "", null))),
            ["reload-failed-after-write"] = () =>
            {
                var r = Default();
                var reply = r.Ops.OpAuthor(_ => new EditPlan("{ broken", null, "wrote it", null));
                return R(reply, ("done", "wrote it"), ("reload", r.Ops.OpReload().Human));
            },
            ["enable-now-disabled"] = () =>
            {
                var r = Of(Disabled(UnknownUnit("bad")));
                return R(r.Flows.Enable(r.Who, "bad", true), ("done", "event bad enabled"), ("reason", r.Lib.Catalog.Current.Find("bad")!.DisabledReason!));
            },
            ["uncertain-unreadable"] = () =>
            {
                var r = Default();
                r.Lib.Fs.ThrowAfterPromote = true;
                r.Lib.Fs.BeforePromote = () => r.Lib.Fs.FailReads = true;
                var reply = r.Flows.Enable(r.Who, "raid", false);
                return R(reply, ("error", r.Lib.Events.Load(r.Lib.Units).Result.FileError!));
            },
            ["uncertain"] = () =>
            {
                var r = Default();
                r.Lib.Fs.ThrowAfterPromote = true;
                return R(r.Flows.New(r.Who, "fresh", "spawns"), ("n", "3"));
            },
            ["apply-unparsable"] = () =>
            {
                EventsEditor.Apply("{ broken", "raid", "name", "x", out var error);
                return R(error);
            },
            ["apply-no-array"] = () =>
            {
                EventsEditor.Apply("{ \"SchemaVersion\": 1 }", "raid", "name", "x", out var error);
                return R(error);
            },
            ["set-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "nope", "name", "x"), ("id", "nope"));
            },
            ["edit-unsupported"] = () => R(Default().Ops.OpEdit("raid", "durationSeconds", new object()), ("path", "durationSeconds")),
            ["set-location-empower"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "surge", "location", "here"));
            },
            ["set-factions-waves"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "raid", "action.factions", "Faction_Legion"), ("path", "action.factions"), ("article", "a SpawnWaves"));
            },
            ["set-stat-waves"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "raid", "action.stats.physicalPower", "1.5"), ("path", "action.stats.physicalPower"), ("type", "SpawnWaves"));
            },
            ["set-waves-empower"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "surge", "action.waves", "2"), ("path", "action.waves"));
            },
            ["set-no-action"] = () =>
            {
                var r = Of(Json.Event("raid"), Json.Event("bare").Replace(", " + Json.ValidAction, "", StringComparison.Ordinal));
                return R(r.Flows.Set(r.Who, "bare", "action.waves", "2"), ("id", "bare"), ("part", "action"));
            },
            ["set-stats-rule"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "surge", "action.stats.physicalPower", "1.0"));
            },
            ["set-trigger-mismatch"] = () =>
            {
                var r = Default();
                return R(r.Flows.Set(r.Who, "raid", "trigger.phase", "day"), ("path", "trigger.phase"), ("need", "GameTime"));
            },

            // ---- authoring (Authoring, EventDeleter, templates)
            ["new-limit"] = () =>
            {
                var r = Of(Enumerable.Range(0, EventValidator.MaxDefinitions).Select(i => Json.Event($"e{i:000}")).ToArray());
                return R(r.Flows.New(r.Who, "one-more", "spawns"), ("max", EventValidator.MaxDefinitions.ToString()));
            },
            ["new-bad-id"] = () =>
            {
                var r = Default();
                return R(r.Flows.New(r.Who, "Bad_Id", "spawns"));
            },
            ["template-invalid"] = () =>
            {
                var r = TemplateRig();
                var units = TemplateLibraryTests.Units();
                units.Factions.Remove("Faction_Legion");
                r.Ops.Templates = TemplateCatalog.Load(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), units, units);
                return R(r.Flows.TemplateUse(r.Who, "legion-weekend-surge", null), ("t", "legion-weekend-surge"),
                    ("reason", r.Ops.Templates.Find("legion-weekend-surge")!.Invalid!));
            },
            ["template-use"] = () =>
            {
                var r = TemplateRig();
                r.Ops.Templates = Templates(r.Lib);
                return R(r.Flows.TemplateUse(r.Who, "bandit-ambush", "ambush-2"), ("t", "bandit-ambush"), ("id", "ambush-2"));
            },
            ["template-exists"] = () =>
            {
                var r = TemplateRig();
                r.Ops.Templates = Templates(r.Lib);
                Assert.True(r.Flows.TemplateUse(r.Who, "bandit-ambush", null).Ok);
                return R(r.Flows.TemplateUse(r.Who, "bandit-ambush", null), ("t", "bandit-ambush"), ("id", "bandit-ambush"));
            },
            ["template-unavailable"] = () =>
            {
                var r = TemplateRig();
                return R(r.Flows.TemplateUse(r.Who, "bandit-ambush", null));
            },
            ["template-unknown"] = () =>
            {
                var r = TemplateRig();
                r.Ops.Templates = Templates(r.Lib);
                return R(r.Flows.TemplateUse(r.Who, "nope", null), ("t", "nope"));
            },
            ["new"] = () =>
            {
                var r = Default();
                return R(r.Flows.New(r.Who, "fresh", "empowerment"), ("id", "fresh"), ("pillar", "empowerment"));
            },
            ["new-exists"] = () =>
            {
                var r = Default();
                return R(r.Flows.New(r.Who, "raid", "spawns"), ("id", "raid"));
            },
            ["copy-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.Copy(r.Who, "nope", "raid-2"), ("id", "nope"));
            },
            ["copy"] = () =>
            {
                var r = Default();
                return R(r.Flows.Copy(r.Who, "raid", "raid-2"), ("id", "raid"), ("newId", "raid-2"));
            },
            ["copy-exists"] = () =>
            {
                var r = Default();
                return R(r.Flows.Copy(r.Who, "raid", "surge"), ("newId", "surge"));
            },
            ["authoring-delete-unknown"] = () => R(Authoring.Delete(Default().Lib.Text, "nope").Refusal, ("id", "nope")),
            ["authoring-unparsable"] = () => R(Authoring.Copy("{ broken", "raid", "raid-2").Refusal),
            ["authoring-no-array"] = () => R(Authoring.Copy("{ \"SchemaVersion\": 1 }", "raid", "raid-2").Refusal),
            ["delete-confirm"] = () =>
            {
                var r = Default();
                Assert.True(r.Flows.DeleteEvent(r.Who, "raid", false).Ok);
                return R(r.Flows.DeleteEvent(r.Who, "raid", true), ("id", "raid"));
            },
            ["delete-row-left"] = () =>
            {
                var r = Default();
                r.Lib.State.Document.LastStart["raid"] = Now.AddHours(-1);
                r.Lib.Fs.FailWritesOf.Add(DataFile.State);
                Assert.True(r.Flows.DeleteEvent(r.Who, "raid", false).Ok);
                return R(r.Flows.DeleteEvent(r.Who, "raid", true), ("deleted", Authoring.Deleted("raid")));
            },
            ["delete-row-kept"] = () =>
            {
                var r = Default();
                r.Lib.Fs.Put(DataFile.State, FileVariant.Main, @"{ ""SchemaVersion"": 99, ""lastStart"": { ""raid"": ""2026-09-24T19:00:00Z"" } }");
                r.Lib.State.Load();
                Assert.True(r.Flows.DeleteEvent(r.Who, "raid", false).Ok);
                return R(r.Flows.DeleteEvent(r.Who, "raid", true), ("deleted", Authoring.Deleted("raid")));
            },
            ["delete-running"] = () =>
            {
                var r = Default();
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                return R(r.Flows.DeleteEvent(r.Who, "raid", false), ("id", "raid"));
            },
            ["delete-unarmed"] = () =>
            {
                var r = Default();
                return R(r.Flows.DeleteEvent(r.Who, "raid", true), ("id", "raid"));
            },
            ["delete-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.DeleteEvent(r.Who, "nope", false), ("id", "nope"));
            },
            ["delete-ask"] = () =>
            {
                var r = Default();
                return R(r.Flows.DeleteEvent(r.Who, "raid", false), ("id", "raid"));
            },
            ["delete-confirm-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.DeleteEvent(r.Who, "nope", true), ("id", "nope"));
            },

            // ---- pillars
            ["pillar-unknown"] = () =>
            {
                var r = Default();
                return R(r.Flows.Pillar(r.Who, "nope", "on"), ("name", "nope"), ("choices", PillarNames.Choices));
            },
            ["pillar-bad-state"] = () =>
            {
                var r = Default();
                return R(r.Flows.Pillar(r.Who, "spawns", "maybe"));
            },
            ["pillar-read-fails"] = () =>
            {
                var r = Default();
                r.Ops.Pillars.FailReload = true;
                return R(r.Flows.Pillar(r.Who, "spawns", "on"), ("name", "spawns"), ("error", "cfg locked"));
            },
            ["pillar-already"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                return R(r.Flows.Pillar(r.Who, "spawns", "off"), ("name", "spawns"), ("state", "off"));
            },
            ["pillar-reread-fails"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                r.Ops.Pillars.FailReloadAfterSet = true;
                r.Ops.Pillars.WriteThenThrow = true;
                return R(r.Flows.Pillar(r.Who, "spawns", "on"), ("name", "spawns"), ("error", "cfg locked"));
            },
            ["pillar-save-fails"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                r.Ops.Pillars.TruncateThenThrow = true;
                return R(r.Flows.Pillar(r.Who, "spawns", "on"), ("name", "spawns"), ("state", "off"));
            },
            ["pillar-on"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                return R(r.Flows.Pillar(r.Who, "spawns", "on"), ("name", "spawns"), ("state", "on"));
            },
            ["pillar-off-ended"] = () =>
            {
                var r = new Rig(Library.Of(Json.Event("raid")), pillarsOn: false);
                Assert.True(r.Flows.Pillar(r.Who, "spawns", "on").Ok);
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                return R(r.Flows.Pillar(r.Who, "spawns", "off"), ("switched", "pillar spawns off (saved to cfg)"), ("id", "raid"));
            },

            // ---- purge
            ["purge-nothing"] = () =>
            {
                var r = Default();
                return R(r.Flows.PurgeAsk(r.Who));
            },
            ["purge-unarmed"] = () =>
            {
                var r = Default();
                r.Ops.PurgeableUnits = 3;
                return R(r.Flows.PurgeConfirm(r.Who));
            },
            ["purge-ask"] = () =>
            {
                var r = Default();
                r.Ops.PurgeableUnits = 4;
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                return R(r.Flows.PurgeAsk(r.Who), ("events", "1"), ("units", "4"));
            },
            ["purge-confirm"] = () =>
            {
                var r = Default();
                r.Ops.PurgeableUnits = 4;
                Assert.True(r.Flows.Start(r.Who, "raid").Ok);
                Assert.True(r.Flows.PurgeAsk(r.Who).Ok);
                return R(r.Flows.PurgeConfirm(r.Who), ("events", "1"), ("units", "4"));
            },
            ["start-purge-cooldown"] = () =>
            {
                var r = Default();
                r.Ops.PurgeableUnits = 1;
                Assert.True(r.Flows.PurgeAsk(r.Who).Ok);
                Assert.True(r.Flows.PurgeConfirm(r.Who).Ok);
                return R(r.Flows.Start(r.Who, "raid"), ("secs", r.Ops.CooldownSeconds.ToString()));
            },
            ["gateway-denied"] = () => R(new ActionGateway(_ => { }).Run(ActionKind.Purge, Actor.Player, () => Outcome.Done("purged"))),

            // ---- the commands' own replies
            ["event-bad-verb"] = () => R(AdminLines.EventVerbs),
            ["info-unknown"] = () => R(AdminLines.UnknownEvent("nope"), ("id", "nope")),
            ["template-bad-verb"] = () => R(AdminLines.TemplateVerbs),
            ["purge-bad-arg"] = () => R(AdminLines.PurgeArgs),
        };
}
