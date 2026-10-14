using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>The game side of an apocalypse host (apocalypse D21): the ledger, natives, players, unit positions, moves and
/// renewals of Services/ApocalypseAction, each able to throw on demand, with every log, warning and announcement kept.</summary>
sealed class ApoGame : IApocalypseGame
{
    public SpawnLedger Ledger { get; } = new(new LedgerLimits(200, 50, 1000, 1000));
    public IRandom Random { get; } = new StepRandom();
    public int GraceSeconds => 60;
    public int DrainMarginSeconds => 120;
    public WalkBudget Budget { get; } = new();
    public int LeftToDrain { get; set; }

    public Func<IEnumerable<HotspotNative>> NativeRead { get; set; } = () => [];
    public List<HuntCandidate> Online { get; } = [];
    public Exception? PlayersThrow { get; set; }
    public Exception? ReadThrow { get; set; }
    public Exception? MoveThrow { get; set; }
    public Exception? PlaceThrow { get; set; }
    public Dictionary<long, (float X, float Z)> Positions { get; } = [];
    public List<RegionJump> Moves { get; } = [];
    public List<string> Logs { get; } = [];
    public List<string> Warns { get; } = [];
    public List<string> Announced { get; } = [];
    long _key = 5000;

    public (float X, float Y, float Z) Place(float x, float z, float radius, int i, int n)
    {
        if (PlaceThrow is { } e) throw e;
        var (px, pz) = SpawnLedger.Around(x, z, radius, i, n, 0.3);
        return (px, 0, pz);
    }

    public IEnumerable<HotspotNative> Natives() => NativeRead();
    public IReadOnlyList<HuntCandidate> Players() => PlayersThrow is { } e ? throw e : Online;

    public RegionUnit? Read(TrackedUnit unit)
    {
        if (ReadThrow is { } e) throw e;
        var (x, z) = Positions.TryGetValue(unit.Key, out var p) ? p : (0f, 0f);
        return new RegionUnit(unit.Key, unit.EventId, unit.SpawnedUtc, x, 0, z, 1, unit.Wave);
    }

    public bool IsClaimed(string eventId, float x, float z) => false;
    public bool Walkable(float x, float z, byte level) => true;
    public float HeightOf(byte level) => level;

    public void Move(RegionJump jump)
    {
        if (MoveThrow is { } e) throw e;
        Moves.Add(jump);
        Positions[jump.Key] = (jump.X, jump.Z);
    }

    public void Renew(long key, UnitLifetime life) => Ledger.Renew(key, life);
    public string? RegionName(float x, float z) => "Farbane Woods";
    public void MapTerritory(string eventId) { }
    public void Log(string line) => Logs.Add(line);
    public void Warn(string line) => Warns.Add(line);
    public void Announce(string eventId, string line) => Announced.Add(line);

    /// <summary>Confirms every waiting order at its spawn point, telling the host as SpawnTracker does; returns the keys.</summary>
    public List<long> SpawnAll(ApocalypseHost host, DateTime at)
    {
        var keys = new List<long>();
        for (var batch = Ledger.TakeSpawns(); batch.Count > 0; batch = Ledger.TakeSpawns())
            foreach (var o in batch)
            {
                var key = ++_key;
                Assert.True(Ledger.Confirm(o, key, at));
                Positions[key] = (o.X, o.Z);
                host.Settled(o.EventId, o.Wave, true);
                keys.Add(key);
            }
        return keys;
    }
}

/// <summary>apocalypse D21 and D22: the five dependency failures of tools/preflight-checks.json › dependencySuites.apocalypse
/// (native-read, spread-read, march-read with the roam move, resume-read, state-write), driven through Logic/ApocalypseHost
/// over the real engine, ledger, scoreboard and state store. Each fault is held, logged once per streak, never thrown into
/// the tick or the death pass, and its health entry goes with the first good call.</summary>
public class ApocalypseDependencyFailureTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);

    static ControlState Open() => new(false, true, new HashSet<Pillar>(Enum.GetValues<Pillar>()), 0, 3);

    static IEnumerable<HotspotNative> Cluster(float x, float z, int n) =>
        Enumerable.Range(0, n).Select(i => new HotspotNative(new UnitFacts(Apo.Zombie, "Faction_Undead", false, false, false, false, false, null,
            () => "FarbaneWoods"), x + i, z + i));

    static readonly IReadOnlyList<HotspotNative> TwoClusters = [.. Cluster(0, 0, 5), .. Cluster(1000, 0, 4)];

    static InvalidOperationException Boom(string message) => new(message);

    internal sealed record Rig(ApoGame Game, ApocalypseHost Host, EventEngine Engine, StateStore State, MemoryFileStore Files, Scoreboard Board, LogLines StoreLog)
    {
        public Outcome? Start(DateTime at) => Engine.Start("apo", "manual", at, Open(), gate: d => Host.Prepare(d, Game));
        public ActiveEvent Active => Engine.Find("apo")!;
        public ApocalypseStep Tick(DateTime at) => Host.Tick(Active, Game, State, Board, at);
    }

    internal static Rig Make(string? action = null)
    {
        var files = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(files, log.Add), () => files.Now, log.Add);
        var game = new ApoGame { NativeRead = () => TwoClusters };
        return new Rig(game, new ApocalypseHost(), ApocalypseTests.Engine(out _, Apo.Event(action: action)), state, files, new Scoreboard(), log);
    }

    /// <summary>A started run: its hotspots read, its roamers queued and confirmed.</summary>
    internal static Rig Running(string? action = null)
    {
        var rig = Make(action);
        Assert.Null(rig.Start(T0));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0));
        rig.Game.SpawnAll(rig.Host, T0);
        return rig;
    }

    static ScoreSides RoamerKill => new(null, new OurUnit("apo", Apocalypse.RoamerWave(1), T0.AddSeconds(1)));

    // ---- native-read: the hotspot read at the start (D2)

    [Fact]
    public void NativeRead_fails_when_a_throwing_read_starts_or_repeats_its_line()
    {
        var rig = Make();
        rig.Game.NativeRead = () => throw Boom("query broke");
        var refused = rig.Start(T0);
        Assert.NotNull(refused);
        Assert.Equal(Reasons.Read, refused!.Reason);
        Assert.Contains("no hotspot: native read failed: query broke", refused.Human);
        Assert.Null(rig.Engine.Find("apo"));
        Assert.Null(rig.Host.Run);
        Assert.Single(rig.Game.Warns);
        Assert.NotNull(rig.Start(T0.AddSeconds(5)));                         // the same streak: no second line
        Assert.Single(rig.Game.Warns);
        Assert.Null(rig.Engine.Find("apo"));
    }

    [Fact]
    public void NativeRead_passes_good_read_ends_the_streak()
    {
        var rig = Make();
        rig.Game.NativeRead = () => throw Boom("query broke");
        Assert.NotNull(rig.Start(T0));
        rig.Game.NativeRead = () => TwoClusters;
        Assert.Null(rig.Start(T0.AddSeconds(5)));
        Assert.True(rig.Host.Begin(rig.Active, rig.Game, rig.State, rig.Board, T0.AddSeconds(5)));
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("apocalypse apo hotspots: 2", StringComparison.Ordinal));
        Assert.Equal(6, rig.Game.Ledger.PendingSpawns);                     // 3 roamers at each hotspot
        rig.Engine.Cancel("apo");
        rig.Host.Ended("apo", ApocalypseEnd.Stop, rig.Game, rig.State);
        rig.Game.NativeRead = () => throw Boom("query broke");
        Assert.NotNull(rig.Start(T0.AddSeconds(10)));
        Assert.Equal(2, rig.Game.Warns.Count);                               // a new streak, a new line
    }

    [Fact]
    public void NativeRead_empty_no_native_no_hotspot()
    {
        var rig = Make();
        rig.Game.NativeRead = () => [];
        var refused = rig.Start(T0);
        Assert.Equal(Reasons.Condition, refused!.Reason);
        Assert.Empty(rig.Game.Warns);
        Assert.Contains(rig.Game.Logs, l => l.Contains("no hotspot: fewer than 3 Undead natives", StringComparison.Ordinal));
        Assert.Empty(rig.Host.Health);
    }

    // ---- spread-read: the death patch's read before SpawnTracker.Died (D4)

    [Fact]
    public void SpreadRead_fails_when_a_throw_skips_the_death_pass()
    {
        var rig = Running();
        var order = new List<string>();
        var before = rig.Game.Ledger.PendingSpawns;
        DeathPass.Run(new[] { "a", "b" },
            d => rig.Host.Before(() => d == "a" ? throw Boom("ledger gone") : RoamerKill, () => (40f, 0f), rig.Game),
            d => order.Add($"died {d}"),
            (d, _) => order.Add($"vblood {d}"),
            (d, read) => { rig.Host.After(read, rig.Game, T0.AddSeconds(30)); order.Add($"spread {d}"); });
        Assert.Equal(["died a", "vblood a", "spread a", "died b", "vblood b", "spread b"], order);
        Assert.Equal(before + 2, rig.Game.Ledger.PendingSpawns);           // the later death still spreads (perKill 2)
        Assert.Equal(["apocalypse: spread skipped: ledger gone"], rig.Game.Warns);
    }

    [Fact]
    public void SpreadRead_fails_when_one_throwing_read_prints_more_than_one_line()
    {
        var rig = Running();
        for (var i = 0; i < 3; i++)
            Assert.Null(rig.Host.Before(() => throw Boom("ledger gone"), () => (0f, 0f), rig.Game));
        Assert.Single(rig.Game.Warns);
        Assert.Contains(ApocalypseHost.SpreadFailing, rig.Host.Health);
        // a throwing victim position is the same read
        Assert.Null(rig.Host.Before(() => RoamerKill, () => throw Boom("no position"), rig.Game));
        Assert.Single(rig.Game.Warns);
    }

    [Fact]
    public void SpreadRead_passes_good_read_clears_the_entry()
    {
        var rig = Running();
        Assert.Null(rig.Host.Before(() => throw Boom("ledger gone"), () => (0f, 0f), rig.Game));
        Assert.Contains(ApocalypseHost.SpreadFailing, rig.Host.Health);
        var read = rig.Host.Before(() => RoamerKill, () => (40f, 0f), rig.Game);
        Assert.NotNull(read);
        rig.Host.After(read, rig.Game, T0.AddSeconds(30));
        Assert.Empty(rig.Host.Health);
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("apocalypse apo spread: +2", StringComparison.Ordinal));
        Assert.Null(rig.Host.Before(() => throw Boom("ledger gone"), () => (0f, 0f), rig.Game));
        Assert.Equal(2, rig.Game.Warns.Count);                               // a new streak, a new line
    }

    [Fact]
    public void SpreadRead_empty_no_run_reads_nothing()
    {
        var rig = Make();
        var reads = 0;
        Assert.Null(rig.Host.Before(() => { reads++; return RoamerKill; }, () => (0f, 0f), rig.Game));
        rig.Host.After(null, rig.Game, T0);
        Assert.Equal(0, reads);
        Assert.Empty(rig.Game.Warns);
        Assert.Empty(rig.Host.Health);
    }

    // ---- march-read: the players and our units' positions, the hops, and the roam move (D6, A8)

    [Fact]
    public void MarchRead_fails_when_a_throwing_read_crashes_or_repeats()
    {
        var rig = Running();
        var rose = rig.Tick(T0.AddMinutes(1));                               // horde 1 rises, its front at hotspot 1
        Assert.Equal(1, rose.Rose);
        rig.Game.SpawnAll(rig.Host, T0.AddMinutes(1));
        var front = rig.Host.Run!.Fronts[1];
        var (x, z) = (front.X, front.Z);
        rig.Game.ReadThrow = Boom("position gone");
        for (var s = 1; s <= 12; s++) Assert.Equal(ApocalypseResult.None, rig.Tick(T0.AddMinutes(1).AddSeconds(s)).Result);
        Assert.Equal((x, z), (front.X, front.Z));                            // the march holds
        Assert.Empty(rig.Game.Moves);                                        // and nothing hops or roams
        Assert.Equal(["apocalypse: march skipped: position gone"], rig.Game.Warns);
        Assert.Contains(ApocalypseHost.MarchFailing, rig.Host.Health);
        rig.Game.ReadThrow = null;
        rig.Game.PlayersThrow = Boom("players gone");                         // the player read is the same category
        rig.Tick(T0.AddMinutes(1).AddSeconds(13));
        Assert.Single(rig.Game.Warns);
    }

    [Fact]
    public void MarchRead_fails_when_a_throwing_roam_move_crashes_or_repeats()
    {
        var rig = Running();
        rig.Game.Online.Add(new HuntCandidate(7, 3000, 0, true, true, false, false));   // far from every roamer: they jump toward it
        rig.Game.MoveThrow = Boom("move refused");
        var before = rig.Game.Positions.ToDictionary(p => p.Key, p => p.Value);
        for (var s = 0; s <= 15; s += 5) rig.Tick(T0.AddSeconds(s));
        Assert.Equal(["apocalypse: roam move skipped: move refused"], rig.Game.Warns);
        Assert.Contains(ApocalypseHost.RoamFailing, rig.Host.Health);
        Assert.DoesNotContain(ApocalypseHost.MarchFailing, rig.Host.Health);
        Assert.Equal(before, rig.Game.Positions);                            // the roamers stay where they stand
    }

    [Fact]
    public void MarchRead_passes_good_read_clears_the_entry()
    {
        var rig = Running();
        rig.Tick(T0.AddMinutes(1));
        rig.Game.SpawnAll(rig.Host, T0.AddMinutes(1));
        var front = rig.Host.Run!.Fronts[1];
        rig.Game.ReadThrow = Boom("position gone");
        rig.Tick(T0.AddMinutes(1).AddSeconds(1));
        Assert.Contains(ApocalypseHost.MarchFailing, rig.Host.Health);
        var x = front.X;
        rig.Game.ReadThrow = null;
        rig.Tick(T0.AddMinutes(1).AddSeconds(2));
        rig.Tick(T0.AddMinutes(1).AddSeconds(3));
        Assert.Empty(rig.Host.Health);
        Assert.True(front.X > x, "the front marches again on good reads");
        // the roam move: a failing move, then a good one
        rig.Game.Online.Add(new HuntCandidate(7, 3000, 0, true, true, false, false));
        rig.Game.MoveThrow = Boom("move refused");
        rig.Tick(T0.AddMinutes(1).AddSeconds(10));
        Assert.Contains(ApocalypseHost.RoamFailing, rig.Host.Health);
        rig.Game.MoveThrow = null;
        rig.Tick(T0.AddMinutes(1).AddSeconds(15));
        Assert.DoesNotContain(ApocalypseHost.RoamFailing, rig.Host.Health);
        Assert.NotEmpty(rig.Game.Moves);
    }

    [Fact]
    public void MarchRead_empty_no_horde_no_line()
    {
        var rig = Running();
        for (var s = 1; s <= 10; s++) Assert.Equal(ApocalypseResult.None, rig.Tick(T0.AddSeconds(s)).Result);
        Assert.Empty(rig.Game.Warns);
        Assert.Empty(rig.Host.Health);
        Assert.Empty(rig.Host.Run!.Fronts);
    }

    // ---- resume-read: the boot record and the rebuild (D12)

    /// <summary>A run that wrote its record, then a restart: a new host and engine over the same state.</summary>
    static (Rig Before, Rig After) Restarted()
    {
        var before = Running();
        before.Tick(T0.AddMinutes(1));
        before.Game.SpawnAll(before.Host, T0.AddMinutes(1));
        Assert.NotNull(before.State.Document.Apocalypse);
        var after = Make() with { State = before.State };
        return (before, after);
    }

    [Fact]
    public void ResumeRead_fails_when_a_throwing_read_resumes_or_stops_the_boot()
    {
        var (_, rig) = Restarted();
        var id = rig.Host.Boot(() => throw Boom("record unreadable"), () => Apo.Set(Apo.Event()), rig.Engine, rig.Game, rig.State, T0.AddMinutes(2));
        Assert.Null(id);                                                     // returned: every other boot step runs
        Assert.Null(rig.Engine.ResumePending);
        Assert.Null(rig.State.Document.Apocalypse);
        Assert.Equal(["apocalypse - not resumed: resume read failed: record unreadable"], rig.Game.Warns);
        // a throw in the rebuild after the drain: not resumed, nothing left running, the record gone, one line
        var (_, again) = Restarted();
        Assert.Equal("apo", again.Host.Boot(() => again.State.Document.Apocalypse, () => Apo.Set(Apo.Event()), again.Engine, again.Game,
            again.State, T0.AddMinutes(2)));
        again.Game.PlaceThrow = Boom("ground unreadable");
        Assert.Null(again.Host.Resume(again.Engine, Open(), again.Game, again.State, again.Board, T0.AddMinutes(2)));
        Assert.Null(again.Engine.Find("apo"));
        Assert.Null(again.State.Document.Apocalypse);
        Assert.Null(again.Host.Run);
        Assert.Equal(["apocalypse apo not resumed: resume read failed: ground unreadable"], again.Game.Warns);
        Assert.Null(again.Host.Resume(again.Engine, Open(), again.Game, again.State, again.Board, T0.AddMinutes(2).AddSeconds(1)));
        Assert.Single(again.Game.Warns);
    }

    [Fact]
    public void ResumeRead_passes_a_good_record_resumes()
    {
        var (_, rig) = Restarted();
        Assert.Equal("apo", rig.Host.Boot(() => rig.State.Document.Apocalypse, () => Apo.Set(Apo.Event()), rig.Engine, rig.Game, rig.State,
            T0.AddMinutes(2)));
        Assert.Equal("apo", rig.Engine.ResumePending);
        rig.Game.LeftToDrain = 3;
        Assert.Null(rig.Host.Resume(rig.Engine, Open(), rig.Game, rig.State, rig.Board, T0.AddMinutes(2)));   // the drain gate waits
        rig.Game.LeftToDrain = 0;
        var resumed = rig.Host.Resume(rig.Engine, Open(), rig.Game, rig.State, rig.Board, T0.AddMinutes(2).AddSeconds(1));
        Assert.NotNull(resumed);
        Assert.Equal(T0, resumed!.Instance.StartedUtc);                     // the clock ran while down
        Assert.Contains(rig.Game.Logs, l => l.StartsWith("event apo resumed after restart (horde 1/3", StringComparison.Ordinal));
        Assert.Empty(rig.Game.Warns);
        Assert.NotNull(rig.State.Document.Apocalypse);
    }

    [Fact]
    public void ResumeRead_empty_no_record_nothing_resumes()
    {
        var rig = Make();
        Assert.Null(rig.Host.Boot(() => null, () => Apo.Set(Apo.Event()), rig.Engine, rig.Game, rig.State, T0));
        Assert.Null(rig.Host.Resume(rig.Engine, Open(), rig.Game, rig.State, rig.Board, T0));
        Assert.Empty(rig.Game.Logs);
        Assert.Empty(rig.Game.Warns);
        Assert.Null(rig.Engine.ResumePending);
    }

    // ---- state-write: the record through Persistence's streak (D11)

    static string Saved(MemoryFileStore files) => Encoding.UTF8.GetString(files.Read(DataFile.State, FileVariant.Main) ?? []);

    [Fact]
    public void StateWrite_fails_when_a_failed_write_stops_the_run()
    {
        var rig = Running();
        rig.Files.FailWrites = true;
        for (var s = 0; s <= 90; s++)
        {
            rig.Files.Now = T0.AddSeconds(s);
            rig.Tick(T0.AddSeconds(s));
            rig.State.Flush();
        }
        Assert.Equal(1, rig.Host.Run!.Risen);                                // the run goes on: horde 1 rose at 1 min
        Assert.Equal(1, rig.State.Document.Apocalypse!.HordesRisen);        // the record in memory is current
        Assert.True(rig.State.FailureCount > 1);
        Assert.Equal(1, rig.StoreLog.Count("state.json write failed"));      // Persistence's streak: one line
        Assert.Empty(rig.Game.Warns);
    }

    [Fact]
    public void StateWrite_passes_good_write_saves_the_record()
    {
        var rig = Running();
        rig.Files.Now = T0.AddSeconds(5);
        rig.State.Flush();
        var saved = StateDocument.TryParse(rig.Files.Read(DataFile.State, FileVariant.Main)!)!;
        Assert.Equal("apo", saved.Apocalypse!.EventId);
        Assert.Equal(0, rig.State.FailureCount);
    }

    [Fact]
    public void StateWrite_empty_no_run_no_key()
    {
        var rig = Make();
        rig.State.MarkDirty();
        rig.State.Flush();
        Assert.DoesNotContain("\"Apocalypse\"", Saved(rig.Files), StringComparison.Ordinal);
    }
}
