using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>event-library D19 (every dependency of the library failing, through Logic's IFileStore, IPillarStore and
/// phase-source fakes) and D32 (the library's entries of the degraded list). The method prefixes are the categories of
/// D33's dependency suite: EventsWrite, EventsPromote, StateWrite, Catalogue, LocationContext, PhaseSource.</summary>
public class LibraryDependencyFailureTests
{
    static readonly DateTime Now = AuthoringTests.Now;

    static Library Lib() => AuthoringTests.Lib(Json.Event("raid"), Json.Empower("surge"));

    // ---- EventsWrite: the .tmp write fails, before Promote

    [Fact]
    public void EventsWrite_fails_when_tmp_write_fails()
    {
        var lib = Lib();
        var (hash, snapshot) = (lib.Hash, AuthoringTests.Snapshot(lib.Catalog.Current));
        lib.Fs.FailWrites = true;
        Assert.Equal("could not write events.json: disk full", lib.Write(t => Authoring.New(t, "fresh", "spawns")));
        Assert.Equal(hash, lib.Hash);
        Assert.Equal(snapshot, AuthoringTests.Snapshot(lib.Catalog.Current));
        Assert.Equal(0, lib.ConfigChanged);
        Assert.False(lib.Events.WriteUncertain);
    }

    [Fact]
    public void EventsWrite_passes_after_disk_recovers()
    {
        var lib = Lib();
        lib.Fs.FailWrites = true;
        lib.Write(t => Authoring.New(t, "fresh", "spawns"));
        lib.Fs.FailWrites = false;
        Assert.StartsWith("event fresh created", lib.Write(t => Authoring.New(t, "fresh", "spawns")));
        Assert.Equal(1, lib.ConfigChanged);
    }

    [Fact]
    public void EventsWrite_empty_refused_plan_writes_nothing()
    {
        var lib = Lib();
        lib.Fs.FailWrites = true;
        Assert.Equal("event raid already exists", lib.Write(t => Authoring.New(t, "raid", "spawns")));
        Assert.DoesNotContain(lib.Fs.Ops, o => o.StartsWith("write", StringComparison.Ordinal));
    }

    // ---- EventsPromote: Promote throws, so the write is uncertain

    [Fact]
    public void EventsPromote_fails_when_promote_replaces_then_throws()
    {
        var lib = Lib();
        lib.Fs.ThrowAfterPromote = true;
        Assert.Equal("write uncertain: file now holds 3 definitions", lib.Write(t => Authoring.New(t, "fresh", "spawns")));
        Assert.Contains("\"fresh\"", lib.Text);                                           // the file was replaced
        Assert.Equal(AuthoringTests.Snapshot(lib.HandEdit(lib.Text)), AuthoringTests.Snapshot(lib.Catalog.Current));
        Assert.Equal(1, lib.ConfigChanged);
        Assert.True(lib.Events.WriteUncertain);
        Assert.Equal(1, lib.Log.Count("events.json write uncertain (replaced, then the handle failed): reloaded 3 definitions from disk"));
    }

    [Fact]
    public void EventsPromote_passes_throw_before_replace_keeps_file()
    {
        var lib = Lib();
        var (hash, snapshot) = (lib.Hash, AuthoringTests.Snapshot(lib.Catalog.Current));
        lib.Fs.FailPromote = true;
        Assert.Equal("write uncertain: file now holds 2 definitions", lib.Write(t => Authoring.New(t, "fresh", "spawns")));
        Assert.Equal(hash, lib.Hash);
        Assert.Equal(snapshot, AuthoringTests.Snapshot(lib.Catalog.Current));
        Assert.Equal(0, lib.ConfigChanged);                                                // the set did not change
        Assert.True(lib.Events.WriteUncertain);
    }

    [Fact]
    public void EventsPromote_empty_events_file()
    {
        var lib = AuthoringTests.Lib();
        lib.Fs.ThrowAfterPromote = true;
        Assert.Equal("write uncertain: file now holds 1 definitions", lib.Write(t => Authoring.New(t, "fresh", "spawns")));
        Assert.NotNull(lib.Catalog.Current.Find("fresh"));
    }

    // ---- StateWrite: a delete's state.json write fails after its events.json write

    static (Library Lib, EventDeleter Deleter) Deletion()
    {
        var lib = Lib();
        lib.State.Document.LastStart["raid"] = Now.AddHours(-1);
        lib.State.Document.LastStart["surge"] = Now.AddHours(-1);
        return (lib, new EventDeleter(new DeleteArming(), lib.Editor, lib.Catalog, _ => false, lib.State, lib.Log.Add));
    }

    [Fact]
    public void StateWrite_fails_when_state_write_fails()
    {
        var (lib, deleter) = Deletion();
        lib.Fs.FailWritesOf.Add(DataFile.State);
        deleter.Request(1, "raid", Now);
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file); cooldown row left, cleared on the next save",
            deleter.Confirm(1, "raid", Now.AddSeconds(1), lib.Units).Human);
        Assert.Null(lib.Catalog.Current.Find("raid"));                                     // the definition stays deleted
        Assert.Equal(1, lib.Log.Count("delete raid: cooldown row left, cleared on the next save (disk full)"));
        Assert.True(lib.State.Dirty);

        lib.Fs.FailWritesOf.Clear();
        lib.State.Flush(force: true);
        var state = lib.Fs.Text(DataFile.State, FileVariant.Main)!;
        Assert.DoesNotContain("\"raid\"", state);
        Assert.Contains("\"surge\"", state);
    }

    [Fact]
    public void StateWrite_fails_when_state_read_only()
    {
        var (lib, deleter) = Deletion();
        lib.Fs.Put(DataFile.State, FileVariant.Main, @"{ ""SchemaVersion"": 99, ""lastStart"": { ""raid"": ""2026-09-26T11:00:00Z"" } }");
        lib.State.Load();
        Assert.True(lib.State.ReadOnly);
        deleter.Request(1, "raid", Now);
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file); state.json is read-only; its cooldown row is kept",
            deleter.Confirm(1, "raid", Now.AddSeconds(1), lib.Units).Human);
        Assert.Null(lib.Catalog.Current.Find("raid"));
        Assert.True(lib.State.Document.LastStart.ContainsKey("raid"));                     // memory matches the file it never writes
        Assert.Equal(1, lib.Log.Count("delete raid: state.json is read-only; its cooldown row is kept"));
        Assert.Equal(0, lib.Log.Count("cooldown row left"));
    }

    [Fact]
    public void StateWrite_passes_both_writes()
    {
        var (lib, deleter) = Deletion();
        deleter.Request(1, "raid", Now);
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file)", deleter.Confirm(1, "raid", Now.AddSeconds(1), lib.Units).Human);
        Assert.DoesNotContain("\"raid\"", lib.Fs.Text(DataFile.State, FileVariant.Main));
        Assert.Equal(0, lib.Log.Count("cooldown row left"));
    }

    [Fact]
    public void StateWrite_empty_null_cooldown_rows()
    {
        var (lib, deleter) = Deletion();
        lib.Fs.Put(DataFile.State, FileVariant.Main, @"{ ""SchemaVersion"": 1, ""lastStart"": null }");  // a hand-written null
        lib.State.Load();
        Assert.NotNull(lib.State.Document.LastStart);
        deleter.Request(1, "raid", Now);
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file)", deleter.Confirm(1, "raid", Now.AddSeconds(1), lib.Units).Human);
        Assert.Null(lib.Catalog.Current.Find("raid"));
    }

    [Fact]
    public void StateWrite_empty_no_cooldown_row()
    {
        var lib = Lib();
        lib.Fs.FailWritesOf.Add(DataFile.State);
        var deleter = new EventDeleter(new DeleteArming(), lib.Editor, lib.Catalog, _ => false, lib.State, lib.Log.Add);
        deleter.Request(1, "raid", Now);
        Assert.Equal("event raid deleted (events.json.bak keeps the previous file)", deleter.Confirm(1, "raid", Now.AddSeconds(1), lib.Units).Human);
        Assert.Null(lib.Fs.Text(DataFile.State, FileVariant.Main));                        // no row, no second write
    }

    // ---- Catalogue: the embedded catalogue is missing or unparsable

    [Theory]
    [InlineData(null)]
    [InlineData("{ broken")]
    [InlineData("{\"SchemaVersion\": 2, \"events\": []}")]
    public void Catalogue_fails_when_missing_or_unparsable(string? text)
    {
        var log = new LogLines();
        var bytes = text is null ? null : TemplateLibraryTests.Bytes(text);
        var c = TemplateCatalog.Boot(bytes, TemplateLibraryTests.Units(), TemplateLibraryTests.Units(), log.Add, log.Add);
        Assert.Equal(1, log.Count("template catalogue unavailable: "));
        Assert.Single(log.Lines);
        Assert.Equal([TemplateLines.CatalogueUnavailable], TemplateLines.List(c, [], []));
        Assert.Equal([TemplateLines.CatalogueUnavailable], TemplateLines.Info(c, "bandit-ambush", Now));
        var lib = Lib();
        Assert.Equal(TemplateLines.CatalogueUnavailable, lib.Write(t => Authoring.TemplateUse(t, c, "bandit-ambush", null)));
        Assert.StartsWith("event fresh created", lib.Write(t => Authoring.New(t, "fresh", "spawns")));       // other commands work
        Assert.Equal([LibraryHealth.CatalogueUnavailable], LibraryHealth.Entries(c.Error, false));
    }

    [Fact]
    public void Catalogue_passes_real_file()
    {
        var log = new LogLines();
        var c = TemplateCatalog.Boot(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), TemplateLibraryTests.Units(), TemplateLibraryTests.Units(), log.Add, log.Add);
        Assert.Equal(["templates: 10/10 valid"], log.Lines);
        Assert.Null(c.Error);

        var units = TemplateLibraryTests.Units();
        units.Factions.Remove("Faction_Legion");
        log.Lines.Clear();
        TemplateCatalog.Boot(TemplateLibraryTests.Bytes(TemplateLibraryTests.RealText), units, units, log.Add, log.Add);
        Assert.Equal(["template legion-weekend-surge invalid: unknown faction Faction_Legion", "templates: 9/10 valid"], log.Lines);
    }

    [Fact]
    public void Catalogue_empty_zero_templates()
    {
        var log = new LogLines();
        var c = TemplateCatalog.Boot(TemplateLibraryTests.Bytes("{\"SchemaVersion\":1,\"events\":[]}"), TemplateLibraryTests.Units(), TemplateLibraryTests.Units(), log.Add, log.Add);
        Assert.Equal(["templates: 0/0 valid"], log.Lines);
        Assert.Empty(LibraryHealth.Entries(c.Error, false));
    }

    // ---- LocationContext: location here without a character or position

    [Fact]
    public void LocationContext_fails_when_no_character()
    {
        var lib = Lib();
        var hash = lib.Hash;
        Assert.Equal(LocationArg.NoCharacter, LocationArg.FromContext(() => throw new InvalidOperationException("no entity")).Error);
        Assert.Equal("location here needs your character in the world", AuthoringTests.Set(lib, "raid", "location", "here", null));
        Assert.Equal(hash, lib.Hash);
    }

    [Fact]
    public void LocationContext_passes_character_in_world()
    {
        var p = LocationArg.FromContext(() => (1.26f, 3.04f, -2.24f));
        Assert.True(p.Ok);
        Assert.Equal(new PointArg(1.3m, 3.0m, -2.2m), p.Value);
    }

    [Fact]
    public void LocationContext_empty_console_context() =>
        Assert.Equal(LocationArg.NoCharacter, LocationArg.FromContext(() => null).Error);

    // ---- PhaseSource: the day and night read throws

    static DefinitionSet Triggers()
    {
        var lib = AuthoringTests.Lib(
            Json.Event("sched", trigger: "{ \"type\": \"Schedule\", \"days\": [\"Sat\"], \"times\": [\"20:00\"] }"),
            Json.Empower("night", trigger: "{ \"type\": \"GameTime\", \"phase\": \"night\" }"));
        return lib.Catalog.Current;
    }

    [Fact]
    public void PhaseSource_fails_when_read_throws()
    {
        var set = Triggers();
        var log = new LogLines();
        var failing = true;
        var day = true;
        var sampler = new PhaseSampler(() => failing ? throw new InvalidOperationException("day/night system gone") : day, log.Add);
        var saturday = Zones.Utc(2026, 9, 26, 20, 0);
        var due = TriggerTick.Collect(set, saturday, TimeZoneInfo.Utc, _ => null, sampler);
        Assert.Equal(["sched"], due.Scheduled.Select(s => s.Definition.Id));             // the other trigger fires on the same tick
        Assert.Null(due.Entered);
        Assert.Empty(due.PhaseStarts);
        for (var i = 1; i <= 5; i++) TriggerTick.Collect(set, saturday.AddMinutes(i), TimeZoneInfo.Utc, _ => null, sampler);
        Assert.Equal(1, log.Count("day/night read failed: day/night system gone; GameTime triggers wait"));   // once per streak

        failing = false;
        sampler.Sample();                                                                  // day: the first good read
        day = false;
        Assert.Equal(["night"], TriggerTick.Collect(set, saturday.AddMinutes(9), TimeZoneInfo.Utc, _ => null, sampler).PhaseStarts.Select(d => d.Id));
        failing = true;
        sampler.Sample();
        Assert.Equal(2, log.Count("day/night read failed"));                              // a new streak logs again
    }

    [Fact]
    public void PhaseSource_passes_edges()
    {
        var set = Triggers();
        var day = true;
        var sampler = new PhaseSampler(() => day, _ => throw new Xunit.Sdk.XunitException("no error expected"));
        Assert.Null(sampler.Sample());
        day = false;
        var due = TriggerTick.Collect(set, Zones.Utc(2026, 9, 25, 3, 0), TimeZoneInfo.Utc, _ => null, sampler);
        Assert.Equal(DayPhase.Night, due.Entered);
        Assert.Equal(["night"], due.PhaseStarts.Select(d => d.Id));
        Assert.Empty(due.Scheduled);
    }

    [Fact]
    public void PhaseSource_empty_no_phase_hook()
    {
        var due = TriggerTick.Collect(Triggers(), Zones.Utc(2026, 9, 26, 20, 0), TimeZoneInfo.Utc, _ => null, null);
        Assert.Equal(["sched"], due.Scheduled.Select(s => s.Definition.Id));
        Assert.Null(due.Entered);
    }

    // ---- D32 Degraded

    [Fact]
    public void Degraded_fails_when_write_uncertain()
    {
        var lib = Lib();
        lib.Fs.ThrowAfterPromote = true;
        lib.Write(t => Authoring.New(t, "a", "spawns"));
        lib.Write(t => Authoring.New(t, "b", "spawns"));                                   // repeated: one entry
        var entries = LibraryHealth.Entries("embedded templates.json missing", lib.Events.WriteUncertain);
        Assert.Equal(["template catalogue unavailable", "events.json write uncertain"], entries);
        Assert.Equal("nyar health: 3 events, 0 tracked, degraded: template catalogue unavailable, events.json write uncertain",
            HealthClock.Line(3, 0, entries));
        Assert.Equal("nyar: degraded: template catalogue unavailable, events.json write uncertain (see .nyar status and the server log)",
            AdminLines.DegradedNotice(entries));
    }

    [Fact]
    public void Degraded_passes_cleared_by_clean_write_or_reload()
    {
        var lib = Lib();
        lib.Fs.ThrowAfterPromote = true;
        lib.Write(t => Authoring.New(t, "a", "spawns"));
        lib.Fs.ThrowAfterPromote = false;
        Assert.StartsWith("event b created", lib.Write(t => Authoring.New(t, "b", "spawns")));
        Assert.Empty(LibraryHealth.Entries(null, lib.Events.WriteUncertain));

        lib.Fs.ThrowAfterPromote = true;
        lib.Write(t => Authoring.New(t, "c", "spawns"));
        Assert.True(lib.Events.WriteUncertain);
        Assert.StartsWith("reloaded", lib.Editor.Reload(lib.Units).Human);
        Assert.Empty(LibraryHealth.Entries(null, lib.Events.WriteUncertain));
    }

    [Fact]
    public void Degraded_empty_healthy() => Assert.Empty(LibraryHealth.Entries(null, Lib().Events.WriteUncertain));
}
