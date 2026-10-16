using System.Reflection;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>wave-variety D11: no line this child adds (the "rolled:" line, the pursuit lines, the check-failed line and
/// health entry, and the info lines of pools, rolls and pursuit) carries a player's name, platform id or position; the
/// pursuit check and the hunt it drives read players by Hunt key and position only.</summary>
public partial class PrivacyTests
{
    static readonly (float X, float Z) ChasedAt = (4321.7f, 8765.3f);
    static readonly string[] WaveVarietyMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    sealed class ChaseWorld : IRegionWorld, IPursuitWorld
    {
        public List<TrackedUnit> Tracked { get; } = [];
        public Dictionary<long, RegionUnit> At { get; } = [];
        public bool Throw { get; set; }
        public IEnumerable<TrackedUnit> Units => Tracked;
        public RegionUnit? Read(TrackedUnit unit) => At[unit.Key];
        public string RegionOf(float x, float z) => "None";
        public bool IsClaimed(string eventId, float x, float z) => false;
        public bool Walkable(float x, float z, byte level) => true;
        public float HeightOf(byte level) => 3f;
        public WalkBudget Budget { get; } = new();
        public long? FocusKey(ActiveEvent a) => (long)PlantedId;
        public IReadOnlyList<(float X, float Z)> Centres(ActiveEvent a) => [];

        public IReadOnlyList<HuntCandidate> Players() =>
            Throw ? throw new InvalidOperationException("player query failed")
            : [new HuntCandidate((long)PlantedId, ChasedAt.X, ChasedAt.Z, true, true, false, false)];

        public void Return(RegionReturn r, Scope scope) => At[r.Key] = At[r.Key] with { X = r.X, Z = r.Z };
        public void Jump(RegionJump j) => At[j.Key] = At[j.Key] with { X = j.X, Z = j.Z };

        public void Add(long key, float x, float z, int wave = 1)
        {
            Tracked.Add(new TrackedUnit(key, "CHAR_Bandit_Thug", "wv", Now.AddSeconds(5), 600, Now.AddMinutes(10), wave));
            At[key] = new RegionUnit(key, "wv", Now.AddSeconds(5), x, 3f, z, 10, wave, 0);
        }
    }

    static EventDefinition WaveVarietyDefinition() => PlantedDefinition("wv") with
    {
        Action = PlantedDefinition().Action! with
        {
            Units = [UnitEntry.FromPool("thugs", 3)],
            Pools = new Dictionary<string, IReadOnlyList<string>> { ["thugs"] = ["CHAR_Bandit_Thug", "CHAR_Bandit_Deadeye"], ["lords"] = ["CHAR_Bandit_Tourok_VBlood"] },
            Bosses = [BossRoll.Roll("lords", 1, 1)],
            Behaviour = new Behaviour(BehaviourType.Hunt, 30),
            Pursuit = new Pursuit(20),
        },
    };

    /// <summary>Every line of this child with the planted player the only one online, standing far from the planted event:
    /// info, status, definitions, the start push, a roll, the pursuit start, a pursuing hunt with its group line, the
    /// check-failed line and the health entries.</summary>
    static List<string> WaveVarietyLines()
    {
        var d = WaveVarietyDefinition();
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null);
        active.Record(Now, fought: true);
        var lines = new List<string>();
        lines.AddRange(EventLines.Info(d, active, Now));
        lines.AddRange(EventLines.Info(d, null, Now));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], new DefinitionSet([d]), new Dictionary<string, int>(), true, Now));
        lines.AddRange(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { d.Id }));
        lines.Add(Pools.RolledLine("wv", 3, 3, Pools.Resolve(d.Action!, 3, new SystemRandom(new Random(1)))));

        var world = new ChaseWorld();
        world.Add(1, 4000f, 8000f);
        var pursuit = new PursuitPhase();
        pursuit.Tick(Now.AddSeconds(30), [active], world, lines.Add, lines.Add);
        Assert.True(active.Pursuing);
        var hunt = new HuntPhase();
        hunt.Tick([active], world, lines.Add, lines.Add);
        var other = new ActiveEvent(new RunningInstance(d with { Id = "wx" }, Now, Now.AddMinutes(10)), "manual", null);
        other.Record(Now, fought: true);
        world.Throw = true;
        pursuit.Tick(Now.AddSeconds(35), [other], world, lines.Add, lines.Add);
        lines.AddRange(pursuit.Health);
        return lines;
    }

    [Fact]
    public void WaveVariety_passes_counts_and_ids_only()
    {
        var lines = WaveVarietyLines();
        Assert.Contains(lines, l => l.StartsWith("event wv wave 3/3 rolled: ", StringComparison.Ordinal));
        Assert.Contains($"event wv pursuit: on, no player within 30 m for 30 s", lines);
        Assert.Contains("event wv pursuit: group 1.0 on a player", lines);
        Assert.Contains("pursuit wx: check failed: player query failed", lines);
        Assert.Contains(PursuitPhase.Failing, lines);
        Assert.Contains(lines, l => l.StartsWith("pools: thugs (2 units): ", StringComparison.Ordinal));
        Assert.Contains("pursuit: after 20 s with no player within 30 m", lines);
        foreach (var line in lines)
            foreach (var mark in WaveVarietyMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void WaveVariety_fails_when_a_name_or_position_reaches_a_line()
    {
        foreach (var line in WaveVarietyLines())
            foreach (var mark in WaveVarietyMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        // the line builders take an event id, counts, a draw and a message, never a player
        var parameters = new[] { typeof(Pursuit), typeof(Pools) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.EndsWith("Line", StringComparison.Ordinal))
            .SelectMany(m => m.GetParameters()).Select(p => p.ParameterType).Distinct().ToList();
        Assert.Equal([typeof(string), typeof(int), typeof(WaveDraw)], parameters.OrderBy(t => t.Name == "String" ? 0 : t.Name == "Int32" ? 1 : 2));
        Assert.DoesNotContain(typeof(HuntCandidate).GetProperties(), p => p.PropertyType == typeof(string));
    }

    [Fact]
    public void WaveVariety_fails_when_state_gains_a_property()
    {
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(StateDocument).Assembly.GetTypes().Where(t => t.Name.StartsWith("State", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties()), p => p.Name.Contains("Pursu", StringComparison.Ordinal) || p.Name.Contains("Pool", StringComparison.Ordinal));
    }

    [Fact]
    public void WaveVariety_empty_no_wave_variety_keys() => Assert.Empty(EventLines.WaveVarietyLines(PlantedDefinition("raid").Action!));
}
