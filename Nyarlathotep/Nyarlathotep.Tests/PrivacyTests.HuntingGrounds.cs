using System.Reflection;
using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>hunting-grounds D13: no line, reply, push, health entry or state.json field of this child carries a player's
/// name, platform id or position; the lock, hunt and spread lines carry counts, distances and our own units' spots only;
/// state.json gains nothing.</summary>
public partial class PrivacyTests
{
    static readonly (float X, float Z) HuntedAt = (4321.7f, 8765.3f);
    static readonly string[] HuntingGroundsMarks = [PlantedName, PlantedId.ToString(), "4321", "8765"];

    /// <summary>The planted player's region: everything east of x 4000 (and on the map) is FarbaneWoods.</summary>
    static string PlantedRegionOf(float x, float z) => x > 4000 ? "FarbaneWoods" : "None";

    static EventDefinition HuntingGroundsDefinition() => PlantedDefinition("hg") with
    {
        Action = PlantedDefinition().Action! with
        {
            Location = new Location(LocationType.Region, 0, 0), Scope = new Scope(["FarbaneWoods"]), RegionLock = true,
            Behaviour = new Behaviour(BehaviourType.RegionHunt, 0), Radius = 8,
        },
    };

    sealed class PlantedWorld : IRegionWorld
    {
        public List<TrackedUnit> Tracked { get; } = [];
        public Dictionary<long, RegionUnit> At { get; } = [];
        public bool Throw { get; set; }
        public IEnumerable<TrackedUnit> Units => Tracked;
        public RegionUnit? Read(TrackedUnit unit) => Throw ? throw new InvalidOperationException("entity gone") : At[unit.Key];
        public string RegionOf(float x, float z) => PlantedRegionOf(x, z);
        public bool IsClaimed(string eventId, float x, float z) => false;
        public bool Walkable(float x, float z, byte level) => true;
        public float HeightOf(byte level) => 3f;
        public WalkBudget Budget { get; } = new();
        public IReadOnlyList<HuntCandidate> Players() => [new HuntCandidate((long)PlantedId, HuntedAt.X, HuntedAt.Z, true, true, false, false)];
        public void Return(RegionReturn r, Scope scope) => At[r.Key] = At[r.Key] with { X = r.X, Z = r.Z };
        public void Jump(RegionJump j) => At[j.Key] = At[j.Key] with { X = j.X, Z = j.Z };

        public void Add(long key, float x, float z, int group, (float, float, float)? spawn = null)
        {
            Tracked.Add(new TrackedUnit(key, "CHAR_Bandit_Thug", "hg", Now.AddSeconds(5), 600, Now.AddMinutes(10)));
            At[key] = new RegionUnit(key, "hg", Now.AddSeconds(5), x, 3f, z, 10, 1, group, spawn);
        }
    }

    /// <summary>Every line of this child: info, status, definitions and the start push of a hunting-grounds event, the
    /// spread lines, a lock check that returned units, a hunt check with a jump and a unit in reach, the failure lines and
    /// the health entries.</summary>
    static List<string> HuntingGroundsLines()
    {
        var d = HuntingGroundsDefinition();
        var active = new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null);
        var lines = new List<string>();
        lines.AddRange(EventLines.Info(d, active, Now));
        lines.AddRange(EventLines.Info(d, null, Now));
        lines.Add(PushLines.EventStart(active.Instance).Text);
        lines.AddRange(ApiLines.Status([active], [], new DefinitionSet([d]), new Dictionary<string, int>(), true, Now));
        lines.AddRange(ApiLines.Definitions(new DefinitionSet([d]), new HashSet<string> { d.Id }));
        lines.Add(WaveLines.NoRegionSpot(2, "hg"));
        lines.Add(RegionWave.Where(3, d.Action!.Scope));
        lines.Add(RegionWave.SpotLine("hg", 1, 0, new RegionSpot(4500.2f, 3f, 8500.4f, 10, "FarbaneWoods")));

        var world = new PlantedWorld();
        world.Add(1, 4330f, 8770f, 0);                                                  // in reach of the planted player
        world.Add(2, 4320f, 8300f, 1);                                                  // far: jumps toward them, unseen
        world.Add(3, 3990f, 8765.3f, 2, (4010f, 3f, 8765.3f));                          // out of the regions by 20 m: returned
        var phase = new RegionPhase();
        phase.Tick(Now, [active], world, () => 0.5, lines.Add, lines.Add);
        world.Throw = true;
        phase.Tick(Now.AddSeconds(5), [active], world, () => 0.5, lines.Add, lines.Add);
        lines.AddRange(phase.Health);
        return lines;
    }

    [Fact]
    public void HuntingGrounds_passes_counts_and_distances_only()
    {
        var lines = HuntingGroundsLines();
        Assert.Contains(lines, l => l.StartsWith("region lock hg: returned 1, farthest ", StringComparison.Ordinal));
        Assert.Contains(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^region hunt hg: [12] jumped, 0 waiting, 1 in reach, landed \d+ m$"));
        Assert.Contains("region lock hg: check failed: entity gone", lines);
        Assert.Contains(LockPhase.Failing, lines);
        Assert.Contains(HuntPhase.Failing, lines);
        Assert.Contains(EventLines.RegionHuntLine, lines);
        foreach (var line in lines)
            foreach (var mark in HuntingGroundsMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void HuntingGrounds_fails_when_a_name_or_position_reaches_a_line()
    {
        foreach (var line in HuntingGroundsLines())
            foreach (var mark in HuntingGroundsMarks) Assert.DoesNotContain(mark, line, StringComparison.Ordinal);
        // the players the hunt reads carry a key and a position, never a name or a platform id
        Assert.DoesNotContain(typeof(HuntCandidate).GetProperties(), p => p.PropertyType == typeof(string));
        // the line builders take counts, distances, an event id and a region, never a player
        var builders = new[] { typeof(LockRule), typeof(JumpRule), typeof(RegionWave) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.EndsWith("Line", StringComparison.Ordinal) && m.Name != "SpotLine")
            .SelectMany(m => m.GetParameters()).Select(p => p.ParameterType).Distinct().ToList();
        Assert.DoesNotContain(builders, t => t == typeof(HuntCandidate) || t == typeof(PickCandidate));
    }

    [Fact]
    public void HuntingGrounds_fails_when_state_gains_a_property()
    {
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            typeof(StateDocument).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var text = Encoding.UTF8.GetString(new StateDocument().Serialize());
        Assert.All(HuntingGroundsMarks, m => Assert.DoesNotContain(m, text, StringComparison.Ordinal));
    }

    [Fact]
    public void HuntingGrounds_empty_no_hunting_grounds_keys()
    {
        var d = PlantedDefinition("raid");
        Assert.Empty(EventLines.HuntingGroundsLines(d.Action!));
        Assert.False(HuntingGrounds.Locked(new ActiveEvent(new RunningInstance(d, Now, Now.AddMinutes(10)), "manual", null)));
    }
}
