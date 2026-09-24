using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>foundation D9: every member of Logic/Dependency has a policy row and a fault case; a fault stays in its
/// scope, keeps in-memory state, logs once per streak and never throws.</summary>
public class DependencyFailureTests
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
        [Dependency.ConnectedUsers] = ConnectedUsers,
        [Dependency.CommandRegistration] = CommandRegistration,
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
        Assert.Single(log.Lines);
    }
}
