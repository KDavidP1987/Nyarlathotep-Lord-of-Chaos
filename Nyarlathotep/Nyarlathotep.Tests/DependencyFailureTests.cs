using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D9: every member of Logic/Dependency has a policy row and a fault case; a fault stays in its
/// scope, keeps in-memory state, logs once per streak and never throws.</summary>
public partial class DependencyFailureTests
{
    static readonly IReadOnlyDictionary<Dependency, Action> Cases = new Dictionary<Dependency, Action>
    {
        [Dependency.Disk] = Disk,
        [Dependency.EventsJson] = EventsJson,
        [Dependency.StateJson] = StateJson,
        [Dependency.CfgValues] = CfgValues,
        [Dependency.HookDeathEvent] = () => HookFault(Hook.DeathEvent, TriggerType.VBloodKilled),
        [Dependency.HookDayNight] = () => HookFault(Hook.DayNight, TriggerType.GameTime),
        [Dependency.HookUserConnect] = () => HookFault(Hook.UserConnect, null),
        [Dependency.HookUserDisconnect] = DisconnectHook,
        [Dependency.ConnectedUsers] = ConnectedUsers,
        [Dependency.CommandRegistration] = CommandRegistration,
        [Dependency.PushDelivery] = PushDelivery,
        [Dependency.WalkCheck] = WalkCheckFault,
        [Dependency.Regions] = RegionsFault,
    };

    public static TheoryData<Dependency> All()
    {
        var data = new TheoryData<Dependency>();
        foreach (var d in Enum.GetValues<Dependency>()) data.Add(d);
        return data;
    }

    [Fact]
    public void Every_dependency_has_a_policy_row_and_a_fault_case()
    {
        Assert.NotEmpty(Enum.GetValues<Dependency>());
        foreach (var d in Enum.GetValues<Dependency>())
        {
            Assert.True(DependencyPolicy.Rows.ContainsKey(d), $"{d} has no policy row");
            Assert.True(Cases.ContainsKey(d), $"{d} has no fault case");
        }
    }

    [Theory]
    [MemberData(nameof(All))]
    public void The_fault_stays_in_scope_and_never_throws(Dependency d) => Cases[d]();

    static void Disk()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        state.Load();
        state.Document.Units.Add(new StateUnit("raid", "CHAR_Bandit_Thug", 1, 2, fs.Now));
        state.MarkDirty();

        // Failing writes: retried once per second, one log line for the streak, state kept in memory.
        fs.FailWrites = true;
        var start = fs.Now;
        foreach (var ms in new[] { 0, 400, 1000, 1500, 2000, 3000 })
        {
            fs.Now = start.AddMilliseconds(ms);
            state.Flush();
        }
        Assert.Equal(4, fs.Ops.Count(o => o == "write state.json.tmp")); // 0, 1000, 2000, 3000
        Assert.Equal(1, log.Count("state.json write failed"));
        Assert.True(state.Dirty);
        Assert.Single(state.Document.Units);
        Assert.False(fs.Exists(DataFile.State, FileVariant.Main));

        // Recovery ends the streak; a new failure starts a new one.
        fs.FailWrites = false;
        fs.Now = start.AddSeconds(4);
        state.Flush();
        Assert.False(state.Dirty);
        Assert.Contains("CHAR_Bandit_Thug", fs.Text(DataFile.State, FileVariant.Main));
        fs.FailWrites = true;
        state.MarkDirty();
        fs.Now = start.AddSeconds(5);
        state.Flush();
        Assert.Equal(2, log.Count("state.json write failed"));

        // Slow writes: the state is still written, one log line per slow streak.
        fs.FailWrites = false;
        fs.WriteDelay = TimeSpan.FromSeconds(2);
        for (var i = 0; i < 3; i++)
        {
            state.MarkDirty();
            fs.Now += TimeSpan.FromSeconds(1);
            state.Flush();
            Assert.False(state.Dirty);
        }
        Assert.Equal(1, log.Count("state.json write slow"));

        // Events edits are refused, not lost silently, and the file is untouched.
        fs.WriteDelay = TimeSpan.Zero;
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("a")));
        var events = new EventsFile(new DataStore(fs, log.Add), log.Add);
        var (_, stamp) = events.Load(FakeUnits.Default());
        fs.FailWrites = true;
        Assert.StartsWith("events.json write failed", events.WriteEdit(System.Text.Encoding.UTF8.GetBytes("{}"), stamp, out _));
        Assert.Contains("\"a\"", fs.Text(DataFile.Events, FileVariant.Main));
    }

    static void EventsJson()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        var events = new EventsFile(new DataStore(fs, log.Add), log.Add);
        var catalog = new EventCatalog();

        // Garbage in one event disables only that event.
        fs.Put(DataFile.Events, FileVariant.Main, Json.File(Json.Event("good"), Json.Event("bad", trigger: "{ \"type\": \"Sometimes\" }")));
        var (r, stamp) = events.Load(FakeUnits.Default());
        Assert.Null(catalog.Reload(r, stamp!));
        Assert.Null(catalog.Current.Find("good")!.DisabledReason);
        Assert.NotNull(catalog.Current.Find("bad")!.DisabledReason);

        // A file that does not parse keeps the last valid set.
        fs.Put(DataFile.Events, FileVariant.Main, "{ \"SchemaVersion\": 1, \"events\": [ ");
        var (broken, s2) = events.Load(FakeUnits.Default());
        Assert.StartsWith("events.json rejected: line", catalog.Reload(broken, s2!));
        Assert.NotNull(catalog.Current.Find("good"));

        // An unreadable file is a file error, never a throw.
        fs.FailReads = true;
        var (unread, s3) = events.Load(FakeUnits.Default());
        Assert.StartsWith("events.json could not be read", unread.FileError);
        Assert.Null(s3);
    }

    static void StateJson()
    {
        var fs = new MemoryFileStore();
        var log = new LogLines();
        var store = new DataStore(fs, log.Add);

        fs.Put(DataFile.State, FileVariant.Main, "\u0000\u0001garbage");
        var state = new StateStore(store, () => fs.Now, log.Add);
        state.Load();
        Assert.Empty(state.Document.Units);
        Assert.True(fs.Exists(DataFile.State, FileVariant.Corrupt));

        // A corrupt file that cannot even be renamed still starts.
        fs.Put(DataFile.State, FileVariant.Main, "garbage");
        fs.FailRename = true;
        var again = new StateStore(store, () => fs.Now, log.Add);
        again.Load();
        Assert.Empty(again.Document.Units);
        Assert.Equal(1, log.Count("could not be renamed"));

        // An unreadable state starts empty too, and is still writable afterwards.
        fs.FailReads = true;
        var unread = new StateStore(store, () => fs.Now, log.Add);
        unread.Load();
        Assert.Equal(1, log.Count("state.json could not be read"));
        Assert.False(unread.ReadOnly);
    }

    static void CfgValues()
    {
        // Out-of-range values clamp only their own key, with one log line each.
        var clamped = Limits.All.Select(l => l.Clamp(int.MaxValue)).ToList();
        Assert.All(clamped.Zip(Limits.All), p => Assert.Equal(p.Second.Max, p.First.Value));
        var (ok, noLog) = Limits.MaxSpawnsPerTick.Clamp(Limits.MaxSpawnsPerTick.Default);
        Assert.Equal(Limits.MaxSpawnsPerTick.Default, ok);
        Assert.Null(noLog);
        Assert.All(clamped, c => Assert.NotNull(c.Log));
    }

    static void HookFault(Hook failing, TriggerType? blocked)
    {
        var log = new LogLines();
        var registry = new FakeHooks(failing);
        var hooks = new HookSet(registry, log.Add);
        hooks.RegisterAll();
        hooks.RegisterAll();

        Assert.Equal(new[] { failing }, hooks.Unavailable);
        Assert.Equal(1, log.Count($"hook {failing} unavailable, pillar disabled"));
        Assert.Single(log.Lines);
        foreach (var other in Enum.GetValues<Hook>().Where(h => h != failing))
        {
            Assert.True(hooks.IsAvailable(other));
            Assert.Contains(other, registry.Registered);
        }
        foreach (var t in Enum.GetValues<TriggerType>())
            Assert.Equal(t != blocked, hooks.AllowsTrigger(t));
    }

    /// <summary>raphael-api-core D21: the disconnect hook unavailable is logged once, and a subscriber who left is
    /// still removed by the offline prune at the next push.</summary>
    static void DisconnectHook()
    {
        HookFault(Hook.UserDisconnect, null);
        var log = new LogLines();
        var users = new FakeUsers();
        var hub = new PushHub(users, [60], log.Add);
        hub.Subscribe(1);
        hub.Subscribe(2);
        users.Online.Remove(2);                                   // left, and no hook told the hub
        hub.ConfigChanged();
        Assert.Equal(1, hub.Tick(DateTime.UtcNow, [], waveWarnings: false));
        Assert.False(hub.Subscriptions.Contains(2));
        Assert.Equal(1, log.Count("push: 1 subscribed (offline)"));
    }

    /// <summary>raphael-api-core D21: a user list that throws skips the line without keeping it and logs once per
    /// streak; a recipient that throws is skipped while the others receive; an entry point that throws is caught,
    /// logged once per streak, and the caller goes on.</summary>
    static void PushDelivery()
    {
        var log = new LogLines();
        var users = new FakeUsers { FailList = true };
        var hub = new PushHub(users, [60], log.Add);
        hub.Subscribe(1);
        hub.Subscribe(3);
        for (var i = 0; i < 3; i++)
        {
            hub.ConfigChanged();
            Assert.Equal(0, hub.Tick(DateTime.UtcNow, [], waveWarnings: false));
        }
        Assert.Equal(0, hub.Queue.Count);                         // the skipped lines are not kept
        Assert.Equal(1, log.Count("push: user list unavailable"));

        users.FailList = false;
        users.Unreachable.Add(1);
        hub.Wave("raid", 1);
        hub.Wave("raid", 2);
        Assert.Equal(2, hub.Tick(DateTime.UtcNow, [], waveWarnings: false));
        Assert.Equal(new[] { 3UL, 3UL }, users.Sent.Select(s => s.Id));
        Assert.Equal(1, log.Count("push: a subscriber could not be reached"));

        for (var i = 0; i < 3; i++) hub.EventStarted(null!);    // an entry point that throws
        Assert.Equal(1, log.Count("push: event-start failed"));
        hub.EventEnded("raid");                                   // the others still work
        Assert.Equal(1, hub.Queue.Count);
    }

    static void ConnectedUsers()
    {
        var log = new LogLines();
        var users = new FakeUsers { FailList = true };
        var b = new Broadcaster(users, log.Add);
        for (var i = 0; i < 3; i++) Assert.Equal(0, b.SendToAll("hello"));
        Assert.Equal(1, log.Count("user list unavailable"));

        users.FailList = false;
        users.Unreachable.Add(2);
        Assert.Equal(2, b.SendToAll("hello"));
        Assert.Equal(2, b.SendToAll("again"));
        Assert.Equal(new[] { 1UL, 3UL, 1UL, 3UL }, users.Sent.Select(s => s.Id));
        Assert.Equal(1, log.Count("recipient could not be reached"));

        users.FailList = true;
        b.SendToAll("x");
        Assert.Equal(2, log.Count("user list unavailable"));
    }

    static void CommandRegistration()
    {
        var log = new LogLines();
        var registered = new List<string>();
        var ok = CommandGroups.RegisterEach(
        [
            ("StatusCommands", () => registered.Add("status")),
            ("EventCommands", () => throw new InvalidOperationException("duplicate command")),
            ("PurgeCommands", () => registered.Add("purge")),
        ], log.Add);
        Assert.Equal(2, ok);
        Assert.Equal(new[] { "status", "purge" }, registered);
        Assert.Equal(1, log.Count("command group EventCommands failed to register"));

        // A failure while discovering the groups (reflection) is contained too, keeping what registered before it.
        var log2 = new LogLines();
        var registered2 = new List<string>();
        Assert.Equal(1, CommandGroups.RegisterEach(Discovery(registered2), log2.Add));
        Assert.Equal(new[] { "status" }, registered2);
        Assert.Equal(1, log2.Count("command discovery failed"));

        static IEnumerable<(string, Action)> Discovery(List<string> into)
        {
            yield return ("StatusCommands", () => into.Add("status"));
            throw new System.Reflection.ReflectionTypeLoadException([], []);
        }
        Assert.Single(log.Lines);
    }

    // regions D7, A15, A17: the region polygons. A build that finds none, or throws, disables only regional definitions,
    // keeps the health entry while it lasts, never throws, and `.nyar event reload` rebuilds it.

    static readonly RegionPolygon Square = new("CursedForest", 0, 0, 10, 10, [(0, 0), (10, 0), (10, 10), (0, 10)]);

    static readonly string[] GameNames = ["None", "Other", .. RegionNames.All];

    static string RegionalAndGlobal() => Json.File(
        Json.Event("regional", "{ \"type\": \"Manual\", \"scope\": [\"CursedForest\"] }"),
        Json.Event("global"));

    static void RegionsFault()
    {
        var (state, log) = (new RegionState(), new LogLines());
        state.Build(() => throw new InvalidOperationException("query failed"), () => GameNames, log.Add, log.Add);
        Assert.False(state.Available);
        Assert.Equal([RegionState.HealthEntry], state.Entries);
        Assert.Equal(["regions unavailable: InvalidOperationException: query failed"], log.Lines);
        var r = EventValidator.Parse(RegionalAndGlobal(), FakeUnits.Default(), regions: state);
        Assert.Equal("regions unavailable", r.Set.Find("regional")!.DisabledReason);
        Assert.Null(r.Set.Find("global")!.DisabledReason);
    }

    [Fact]
    public void Regions_fails_when_no_polygons()
    {
        var (state, log) = (new RegionState(), new LogLines());
        state.Build(() => [], () => GameNames, log.Add, log.Add);
        Assert.False(state.Available);
        Assert.Equal([RegionState.HealthEntry], state.Entries);
        Assert.Equal(["regions unavailable: no region polygons (0 dropped, 0 untagged)"], log.Lines);
        var r = EventValidator.Parse(RegionalAndGlobal(), FakeUnits.Default(), regions: state);
        Assert.Equal("regions unavailable", r.Set.Find("regional")!.DisabledReason);
        Assert.True(r.Set.Find("global")!.Startable);
    }

    [Fact]
    public void Regions_fails_when_build_throws() => RegionsFault();

    [Fact]
    public void Regions_empty_healthy_build_leaves_no_health_entry()
    {
        var (state, log) = (new RegionState(), new LogLines());
        state.Build(() => [Square], () => GameNames, log.Add, log.Add);
        Assert.True(state.Available);
        Assert.Empty(state.Entries);
        var r = EventValidator.Parse(RegionalAndGlobal(), FakeUnits.Default(), regions: state);
        Assert.True(r.Set.Find("regional")!.Startable);
        Assert.True(r.Set.Find("global")!.Startable);
    }

    [Fact]
    public void Regions_fails_when_log_sink_throws()
    {
        void Throw(string _) => throw new InvalidOperationException("log down");
        var state = new RegionState();
        state.Build(() => [Square], () => GameNames.Append("Oakveil"), Throw, Throw);     // info fails after a good build
        Assert.True(state.Available);
        Assert.Empty(state.Entries);
        var failed = new RegionState();
        failed.Build(() => throw new InvalidOperationException("query failed"), () => throw new InvalidOperationException(), Throw, Throw);
        Assert.False(failed.Available);
        Assert.Equal([RegionState.HealthEntry], failed.Entries);
        failed.Retry(() => [Square], () => GameNames, Throw, Throw);
        Assert.True(failed.Available);
    }

    [Fact]
    public void Regions_passes_unavailable_wins_over_not_on_map()
    {
        var r = EventValidator.Parse(Json.File(Json.Event("x", "{ \"type\": \"Manual\", \"scope\": [\"StartCave\"] }")), FakeUnits.Default(),
            regions: NoRegions.Instance);
        Assert.Equal("regions unavailable", Assert.Single(r.Set.All).DisabledReason);
    }

    [Fact]
    public void Regions_passes_reload_rebuilds_and_clears_health()
    {
        var (state, log) = (new RegionState(), new LogLines());
        var reads = 0;
        state.Build(() => { reads++; return []; }, () => GameNames, log.Add, log.Add);
        state.Retry(() => { reads++; return [Square]; }, () => GameNames, log.Add, log.Add);
        Assert.True(state.Available);
        Assert.Empty(state.Entries);
        Assert.Contains("regions: 1 polygons, 1 regions (CursedForest); 0 untagged, 0 dropped", log.Lines);
        state.Retry(() => { reads++; return []; }, () => GameNames, log.Add, log.Add);
        Assert.Equal(2, reads);                                        // a built index is not read again
        Assert.True(state.Available);
        var r = EventValidator.Parse(RegionalAndGlobal(), FakeUnits.Default(), regions: state);
        Assert.Null(r.Set.Find("regional")!.DisabledReason);
    }

    [Fact]
    public void Regions_fails_when_names_differ()
    {
        var (state, log) = (new RegionState(), new LogLines());
        state.Build(() => [Square], () => GameNames.Where(n => n != "Strongblade").Append("Oakveil"), log.Add, log.Add);
        Assert.True(state.Available);
        Assert.Equal(1, log.Count("regions: names differ from the game: +Oakveil, -Strongblade"));
        state.Build(() => [Square], () => throw new InvalidOperationException(), log.Add, log.Add);
        Assert.Equal(1, log.Count("regions: names differ from the game: unreadable (InvalidOperationException)"));
    }

    [Fact]
    public void Regions_passes_built_before_definitions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        var core = File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Core.cs"));
        var regions = core.IndexOf("Services.RegionMap.Initialize();", StringComparison.Ordinal);
        var events = core.IndexOf("Services.EventStore.Initialize();", StringComparison.Ordinal);
        Assert.True(regions >= 0 && events > regions, "RegionMap.Initialize must precede EventStore.Initialize in Core.TryInitialize");
        var store = File.ReadAllText(Path.Combine(dir.FullName, "Nyarlathotep", "Nyarlathotep", "Services", "EventStore.cs"));
        var reload = store.IndexOf("internal static Outcome Reload()", StringComparison.Ordinal);
        Assert.True(reload >= 0 && store.IndexOf("RegionMap.Retry();", reload, StringComparison.Ordinal) > reload, "Reload must retry the region build");
        var init = store[store.IndexOf("internal static void Initialize()", StringComparison.Ordinal)..reload];
        Assert.DoesNotContain(", Reload)", init);                     // the boot load must not retry the build it just made
        Assert.DoesNotContain("RegionMap.Retry", init);
    }

    [Fact]
    public void Regions_passes_read_only_map()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "preflight.ps1"))) dir = dir.Parent;
        var map = File.ReadAllText(Path.Combine(dir!.FullName, "Nyarlathotep", "Nyarlathotep", "Services", "RegionMap.cs"));
        Assert.Contains("GetBuffer<WorldRegionPolygonVertex>(entity, true)", map);
        Assert.Contains("entities.Dispose()", map);
        Assert.Contains("query.Dispose()", map);
        foreach (var write in new[] { "SetComponentData", "AddComponent", "RemoveComponent", ".Write<", "SetBuffer", "AddBuffer", "DestroyEntity" })
            Assert.DoesNotContain(write, map);
    }
}
