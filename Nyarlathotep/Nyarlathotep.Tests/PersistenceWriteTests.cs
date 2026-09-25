using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D8: the write protocol — .tmp then promote, one .bak for events.json, stale .tmp removed at
/// load, corrupt state.json rotated, newer schemas read-only, older ones migrated, state written at most once
/// per second.</summary>
public class PersistenceWriteTests
{
    static byte[] Bytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    static (MemoryFileStore Fs, DataStore Store, LogLines Log) New()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        return (fs, new DataStore(fs, log.Add), log);
    }

    [Fact]
    public void A_write_goes_to_the_tmp_and_is_promoted()
    {
        var (fs, store, _) = New();
        Assert.Null(store.WriteAtomic(DataFile.State, Bytes("{}"), keepBackup: false));
        Assert.Equal(new[] { "write state.json.tmp", "promote state.json" }, fs.Ops);
        Assert.Equal("{}", fs.Text(DataFile.State, FileVariant.Main));
        Assert.False(fs.Exists(DataFile.State, FileVariant.Tmp));
    }

    [Fact]
    public void A_crash_mid_write_leaves_the_old_file_and_no_tmp()
    {
        var (fs, store, _) = New();
        fs.Put(DataFile.Events, FileVariant.Main, "old");
        fs.PartialWrites = true;
        Assert.NotNull(store.WriteAtomic(DataFile.Events, Bytes("new content"), keepBackup: true));
        Assert.Equal("old", fs.Text(DataFile.Events, FileVariant.Main));
        Assert.False(fs.Exists(DataFile.Events, FileVariant.Tmp));
    }

    [Fact]
    public void A_failed_promote_leaves_the_old_file_and_no_tmp()
    {
        var (fs, store, _) = New();
        fs.Put(DataFile.Events, FileVariant.Main, "old");
        fs.FailPromote = true;
        Assert.Equal("replace failed", store.WriteAtomic(DataFile.Events, Bytes("new"), keepBackup: true));
        Assert.Equal("old", fs.Text(DataFile.Events, FileVariant.Main));
        Assert.False(fs.Exists(DataFile.Events, FileVariant.Tmp));
    }

    [Fact]
    public void Events_edits_keep_exactly_one_bak_of_the_previous_file()
    {
        var (fs, store, log) = New();
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("a")));
        var events = new EventsFile(store, log.Add);
        var (_, stamp) = events.Load(FakeUnits.Default());
        var first = fs.Text(DataFile.Events, FileVariant.Main);

        Assert.Null(events.WriteEdit(Bytes(Json.File(Json.Event("b"))), stamp, out var s2));
        Assert.Equal(first, fs.Text(DataFile.Events, FileVariant.Bak));
        var second = fs.Text(DataFile.Events, FileVariant.Main);
        Assert.Null(events.WriteEdit(Bytes(Json.File(Json.Event("c"))), s2, out _));
        Assert.Equal(second, fs.Text(DataFile.Events, FileVariant.Bak));

        Assert.Single(fs.Keys, k => k is (DataFile.Events, FileVariant.Bak));
        Assert.DoesNotContain(fs.Keys, k => k.Variant == FileVariant.Tmp);
    }

    [Fact]
    public void State_writes_keep_no_bak()
    {
        var (fs, store, log) = New();
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        for (var i = 0; i < 3; i++)
        {
            state.Document.Units.Add(new StateUnit("raid", "CHAR_Bandit_Thug", i, 0, fs.Now));
            state.MarkDirty();
            state.Flush();
            fs.Now += TimeSpan.FromSeconds(2);
        }
        Assert.DoesNotContain(fs.Keys, k => k.Variant == FileVariant.Bak);
        Assert.Contains("CHAR_Bandit_Thug", fs.Text(DataFile.State, FileVariant.Main));
    }

    [Theory]
    [InlineData(DataFile.Events)]
    [InlineData(DataFile.State)]
    public void A_stale_tmp_is_deleted_at_load(DataFile file)
    {
        var (fs, store, log) = New();
        fs.Put(file, FileVariant.Tmp, "half a fi");
        if (file == DataFile.Events)
        {
            fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("a")));
            new EventsFile(store, log.Add).Load(FakeUnits.Default());
        }
        else new StateStore(store, () => fs.Now, log.Add).Load();
        Assert.False(fs.Exists(file, FileVariant.Tmp));
        Assert.Equal(1, log.Count($"removed stale {DataPaths.FileName(file, FileVariant.Tmp)}"));
    }

    [Fact]
    public void A_corrupt_state_is_renamed_and_an_empty_state_used_twice_in_a_row()
    {
        var (fs, store, log) = New();
        fs.Put(DataFile.State, FileVariant.Main, "{ not json");
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        Assert.Empty(state.Document.Units);
        Assert.Equal("{ not json", fs.Text(DataFile.State, FileVariant.Corrupt));
        Assert.False(fs.Exists(DataFile.State, FileVariant.Main));

        fs.Put(DataFile.State, FileVariant.Main, "[1, 2]");
        var again = new StateStore(store, () => fs.Now, log.Add);
        again.Load();
        Assert.Empty(again.Document.Instances);
        Assert.Equal("[1, 2]", fs.Text(DataFile.State, FileVariant.Corrupt));
        Assert.Single(fs.Keys, k => k is (DataFile.State, FileVariant.Corrupt));
        Assert.Equal(2, log.Count("renamed to state.json.corrupt"));
    }

    [Fact]
    public void A_newer_state_schema_loads_read_only_and_is_never_written()
    {
        var (fs, store, log) = New();
        var newer = "{ \"SchemaVersion\": 2, \"units\": [ { \"eventId\": \"raid\", \"prefab\": \"CHAR_Bandit_Thug\", \"x\": 1, \"z\": 2, \"spawnedUtc\": \"2026-09-24T20:00:00Z\" } ] }";
        fs.Put(DataFile.State, FileVariant.Main, newer);
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        Assert.True(state.ReadOnly);
        Assert.Single(state.Document.Units);
        state.MarkDirty();
        state.Flush(force: true);
        fs.Now += TimeSpan.FromSeconds(5);
        state.Flush();
        Assert.Equal(newer, fs.Text(DataFile.State, FileVariant.Main));
        Assert.DoesNotContain(fs.Ops, o => o.StartsWith("write", StringComparison.Ordinal));
        Assert.Equal(1, log.Count("loaded read-only, never written"));
    }

    [Fact]
    public void A_newer_events_schema_loads_read_only_and_edits_are_refused()
    {
        var (fs, store, log) = New();
        var text = Json.File(Json.Event("a")).Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2");
        fs.Put(DataFile.Events, FileVariant.Main, text);
        var events = new EventsFile(store, log.Add);
        var (result, stamp) = events.Load(FakeUnits.Default());
        Assert.Null(result.FileError);
        Assert.True(events.ReadOnly);
        Assert.Equal(1, log.Count("loaded read-only, never written"));
        Assert.StartsWith("events.json SchemaVersion 2 is newer", events.WriteEdit(Bytes("{}"), stamp, out _));
        Assert.Equal(text, fs.Text(DataFile.Events, FileVariant.Main));
    }

    [Fact]
    public void An_older_schema_migrates_and_a_current_one_does_not()
    {
        Assert.Equal(SchemaMode.Migrate, SchemaPolicy.Decide(1, 2));
        Assert.Equal(SchemaMode.Current, SchemaPolicy.Decide(1, 1));
        Assert.Equal(SchemaMode.ReadOnly, SchemaPolicy.Decide(2, 1));
    }

    [Fact]
    public void State_is_written_at_most_once_per_second()
    {
        var (fs, store, log) = New();
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        var start = fs.Now;
        for (var ms = 0; ms <= 3000; ms += 100)
        {
            fs.Now = start.AddMilliseconds(ms);
            state.MarkDirty();
            state.Flush();
        }
        var writes = fs.Ops.Count(o => o == "write state.json.tmp");
        Assert.Equal(4, writes); // 0, 1000, 2000, 3000 ms
        Assert.False(state.Dirty);
    }

    [Fact]
    public void A_shutdown_flush_ignores_the_interval_and_nothing_is_written_when_clean()
    {
        var (fs, store, log) = New();
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        state.MarkDirty();
        state.Flush();
        state.MarkDirty();
        state.Flush(force: true);
        state.Flush(force: true);
        Assert.Equal(2, fs.Ops.Count(o => o == "write state.json.tmp"));
    }

    [Fact]
    public void State_round_trips_its_v1_fields()
    {
        var doc = new StateDocument { PurgeUntilUtc = Zones.Utc(2026, 9, 24, 21, 0), DailyBanner = "2026-09-24 20:00" };
        doc.Instances.Add(new StateInstance("raid", Zones.Utc(2026, 9, 24, 20, 0), Zones.Utc(2026, 9, 24, 20, 10), "Active"));
        doc.Units.Add(new StateUnit("raid", "CHAR_Bandit_Thug", -1200.5f, -800f, Zones.Utc(2026, 9, 24, 20, 1)));
        doc.LastFired["raid"] = new LastFired("2026-09-24 16:00", Zones.Utc(2026, 9, 24, 20, 0));
        doc.LastStart["raid"] = Zones.Utc(2026, 9, 24, 20, 0);
        var text = System.Text.Encoding.UTF8.GetString(doc.Serialize());
        foreach (var key in new[] { "\"SchemaVersion\": 1", "\"instances\"", "\"units\"", "\"lastFired\"", "\"lastStart\"", "\"purgeUntilUtc\"", "\"dailyBanner\"", "\"occurrence\"", "\"eventId\"" })
            Assert.Contains(key, text);
        var back = StateDocument.TryParse(doc.Serialize())!;
        Assert.Equal(doc.Instances, back.Instances);
        Assert.Equal(doc.Units, back.Units);
        Assert.Equal(doc.LastFired["raid"], back.LastFired["raid"]);
        Assert.Equal(doc.LastStart["raid"], back.LastStart["raid"]);
        Assert.Equal(doc.PurgeUntilUtc, back.PurgeUntilUtc);
        Assert.Equal(doc.DailyBanner, back.DailyBanner);
    }

    [Fact]
    public void An_edit_of_a_file_changed_on_disk_is_refused_and_writes_nothing()
    {
        var (fs, store, log) = New();
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("a")));
        var events = new EventsFile(store, log.Add);
        var (_, stamp) = events.Load(FakeUnits.Default());
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("hand-edited")));
        Assert.Equal(StaleFile.Refusal, events.WriteEdit(Bytes("{}"), stamp, out var written));
        Assert.Null(written);
        Assert.Contains("hand-edited", fs.Text(DataFile.Events, FileVariant.Main));
        Assert.False(fs.Exists(DataFile.Events, FileVariant.Bak));
    }

    [Fact]
    public void The_seed_is_written_only_when_there_is_no_events_json()
    {
        var (fs, store, log) = New();
        var events = new EventsFile(store, log.Add);
        Assert.Null(events.Seed(Bytes(SeedTests.SeedText)));
        Assert.Equal(SeedTests.SeedText, fs.Text(DataFile.Events, FileVariant.Main));
        Assert.Equal("events.json already exists", events.Seed(Bytes("{}")));
        Assert.Equal(SeedTests.SeedText, fs.Text(DataFile.Events, FileVariant.Main));
    }

    [Fact]
    public void The_seed_never_overwrites_a_file_that_appears_while_it_writes()
    {
        var (fs, store, log) = New();
        var events = new EventsFile(store, log.Add);
        fs.BeforePromote = () => fs.Put(DataFile.Events, FileVariant.Main, "{\"admin\":true}");
        Assert.NotNull(events.Seed(Bytes(SeedTests.SeedText)));
        Assert.Equal("{\"admin\":true}", fs.Text(DataFile.Events, FileVariant.Main));
        Assert.False(fs.Exists(DataFile.Events, FileVariant.Tmp));
    }

    [Fact]
    public void A_hand_edit_landing_during_an_admin_edit_is_kept_as_the_bak()
    {
        var (fs, store, log) = New();
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("original")));
        var events = new EventsFile(store, log.Add);
        var (_, stamp) = events.Load(FakeUnits.Default());
        fs.BeforePromote = () => fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("hand-edited")));
        Assert.Null(events.WriteEdit(Bytes("{}"), stamp, out _));
        Assert.Contains("hand-edited", fs.Text(DataFile.Events, FileVariant.Bak));
    }
}

/// <summary>foundation D28: the embedded seed is SchemaVersion 1, valid, one example per pillar, every one disabled.</summary>
public class SeedTests
{
    public static string SeedText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "events.default.json"));

    [Fact]
    public void The_seed_validates_with_one_disabled_example_per_pillar()
    {
        var units = new FakeUnits("CHAR_Bandit_Thug", "CHAR_Bandit_Deadeye", "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Bandit_Tourok_VBlood");
        var r = EventValidator.Parse(SeedText, units);
        Assert.Null(r.FileError);
        Assert.Equal(1, r.SchemaVersion);
        Assert.Empty(r.Log);
        Assert.All(r.Set.All, d => Assert.False(d.Enabled));
        Assert.All(r.Set.All, d => Assert.Null(d.DisabledReason));
        Assert.Equal(Enum.GetValues<Pillar>().OrderBy(p => p), r.Set.All.Select(d => d.Pillar).OrderBy(p => p));
    }
}
