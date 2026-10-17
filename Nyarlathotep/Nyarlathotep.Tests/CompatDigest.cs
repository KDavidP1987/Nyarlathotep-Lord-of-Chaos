using System.Text;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>undead-hordes D21's digest of how definitions validate and plan: for each definition its startable state and
/// reason, its `event info` lines, and with fixed seeds a SpawnWaves action's rolled waves or an Apocalypse's keys, horde
/// units, roamer top-up and ten spreads. It uses only what 0.13.1 already had, so the same file run on a v0.13.1
/// worktree wrote Resources/compat/plans-0.13.1.txt, the golden CompatTests compares against.</summary>
static class CompatDigest
{
    static readonly DateTime T = Zones.Utc(2026, 10, 17, 20, 0);

    public static string Of(IEnumerable<EventDefinition> definitions)
    {
        var b = new StringBuilder();
        foreach (var d in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            b.Append("== ").Append(d.Id).Append(" startable=").Append(d.Startable).Append(" reason=").Append(d.DisabledReason ?? "-").Append('\n');
            foreach (var line in EventLines.Info(d, null, T)) b.Append("info ").Append(line).Append('\n');
            if (!d.Startable) continue;
            if (d.Action is { } a) Waves(b, a);
            if (d.Apocalypse is not null) Apocalypse(b, d);
        }
        return b.ToString();
    }

    /// <summary>The definitions the digest covers: every template of <paramref name="templatesText"/> (0.13.1's file, kept
    /// under Resources/compat/), the default SpawnWaves fixture and the default Apocalypse fixture.</summary>
    public static IEnumerable<EventDefinition> Definitions(string templatesText)
    {
        var catalog = TemplateCatalog.Load(TemplateLibraryTests.Bytes(templatesText), TemplateLibraryTests.Units(), TemplateLibraryTests.Units());
        foreach (var t in catalog.Templates) yield return t.Definition;
        yield return EventValidator.Parse(Json.File(Json.Event("fixture-raid")), FakeUnits.Default(), regions: FakeRegions.All()).Set.All.Single();
        yield return Apo.Def();
    }

    static string Units(IEnumerable<UnitEntry> units) => string.Join(", ", units.Select(u => $"{u.Prefab}x{u.Count}"));

    static void Waves(StringBuilder b, SpawnWavesAction a)
    {
        var waves = a.WaveList?.Count ?? a.Waves;
        for (var w = 1; w <= waves; w++)
        {
            var draw = Pools.Resolve(a, w, new SystemRandom(new Random(w)));
            b.Append($"wave {w} lead {draw.Lead}: ").Append(Units(draw.Units)).Append(" | ").Append(Units(draw.Expand(new SystemRandom(new Random(100 + w))))).Append('\n');
        }
    }

    static void Apocalypse(StringBuilder b, EventDefinition d)
    {
        var apo = d.Apocalypse!;
        var r = apo.Roamers;
        var h = apo.Hordes;
        b.Append($"roamers {r.PerHotspot} {r.PerKill} {r.Max} {r.PerMinute} {r.HuntRange}; hordes {h.Count} {h.FirstAfterMinutes} {h.EveryMinutes} {h.Size} {h.Growth}; march {apo.MarchSpeed}\n");
        for (var k = 1; k <= h.Count; k++)
        {
            var log = new List<string>();
            var units = HordeSchedule.Units(apo, k, 50, 0, 500, new SystemRandom(new Random(k)), log);
            b.Append($"horde {k}: ").Append(string.Join(", ", units.Select(u => $"{u.Unit.Prefab}x{u.Unit.Count}@{u.Entry}"))).Append(" log ").Append(string.Join("; ", log)).Append('\n');
        }
        var active = new ActiveEvent(new RunningInstance(d, T, T.AddSeconds(d.DurationSeconds)), "manual", null);
        var run = new ApocalypseRun(active, Apo.TwoSpots, (500f, 12f, 0f));
        var world = new ApoWorld { Random = new SystemRandom(new Random(7)) };
        foreach (var line in run.TopUp(world, T)) b.Append("topup ").Append(line).Append('\n');
        for (var i = 0; i < 10; i++)
            b.Append("spread ").Append(run.SpreadOn(world, new ScoreSides(null, new OurUnit(d.Id, 101, T)), (i * 7, 0), T.AddSeconds(i * 9)) ?? "-").Append('\n');
        var orders = new List<string>();
        for (var batch = world.Ledger.TakeSpawns(); batch.Count > 0; batch = world.Ledger.TakeSpawns()) orders.AddRange(batch.Select(o => $"{o.Prefab}@{o.Wave}"));
        b.Append("orders ").Append(string.Join(", ", orders)).Append('\n');
    }
}
