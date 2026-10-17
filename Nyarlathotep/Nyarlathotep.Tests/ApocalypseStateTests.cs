using System.Text;
using System.Text.Json;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>apocalypse D11: state.json's `Apocalypse` resume record: it round-trips, holds the resolved goal and each
/// living horde's per-entry living bosses, is left out while none runs, drops alone when it is bad, carries no name or
/// player position, and goes with the run.</summary>
public class ApocalypseStateTests
{
    static readonly DateTime T0 = Zones.Utc(2026, 10, 31, 20, 0);

    /// <summary>A run with an Admin goal at (-321.5, 7, 88), every horde risen and its last horde's bosses spawned (the
    /// Tourok boss killed), with two scoreboard rows; its record now.</summary>
    static ApocalypseRecord Recorded(out ApoWorld w, out ApocalypseRun run)
    {
        var def = Apo.Def(Apo.Action(hordes: Apo.Hordes(count: 2), goal: Apo.Goal(location: "{ \"type\": \"Admin\" }"),
            extra: "\"bosses\": [ " + Apo.BossJson() + ", " + Apo.BossJson(Apo.Deadeye) + " ]"));
        w = new ApoWorld();
        run = new ApocalypseRun(Apo.Active(def, T0, (-321.5f, 7f, 88f)), Apo.TwoSpots, (-321.5f, 7f, 88f));
        run.TopUp(w, T0);
        run.Rise(w, T0.AddMinutes(1), out _);
        run.Rise(w, T0.AddMinutes(6), out _);
        var units = w.SpawnAll(run, T0.AddMinutes(6));
        w.Kill(run, units.Single(u => u.Wave == Apocalypse.BossWave(2, 1)).Key);
        return run.Record(w, [new ApocalypseScore("p1", 4, 1, 1), new ApocalypseScore("p2", 0, 2, 2)], T0.AddMinutes(7));
    }

    static StateDocument Reparse(StateDocument doc) => StateDocument.TryParse(doc.Serialize())!;

    [Fact]
    public void Record_fails_when_the_record_does_not_round_trip()
    {
        var record = Recorded(out _, out _);
        var back = Reparse(new StateDocument { Apocalypse = record }).Apocalypse!;
        Assert.Null(back.Unreadable);
        Assert.Equal(JsonSerializer.Serialize(record), JsonSerializer.Serialize(back));
        Assert.Equal(("apo", T0, T0.AddHours(1), 2), (back.EventId, back.StartedUtc, back.EndsUtc, back.HordesRisen));
        Assert.Equal([3, 3], back.Roamers);
        Assert.Equal(["p1", "p2"], back.Scores.Select(s => s.PlatformId));
    }

    [Fact]
    public void Record_fails_when_the_goal_or_the_living_bosses_are_missing()
    {
        var record = Recorded(out var w, out var run);
        Assert.Equal((-321.5f, 7f, 88f), (record.Goal!.X, record.Goal.Y, record.Goal.Z));         // the admin's start position, fixed
        Assert.Equal([(0f, 0f), (1000f, 0f)], record.Hotspots.Select(h => (h.X, h.Z)));
        Assert.Equal([1, 2], record.Hordes.Select(h => h.Number));
        var last = record.Hordes[1];
        Assert.Equal([(Apo.Tourok, 0), (Apo.Deadeye, 1)], last.LivingBosses.Select(b => (b.Prefab, b.Living)));
        Assert.Equal(12 + 1, last.Living);
        Assert.Empty(record.Hordes[0].LivingBosses);                            // an earlier horde has no bosses
        Assert.Equal((0f, 0f), (record.Hordes[0].FrontX, record.Hordes[0].FrontZ));
        // a defeated horde leaves the record
        foreach (var u in w.OfWave(run, 1)) w.Kill(run, u.Key);
        Assert.Equal([2], run.Record(w, [], T0.AddMinutes(8)).Hordes.Select(h => h.Number));
    }

    [Fact]
    public void Record_fails_when_a_bad_record_takes_the_rest_of_state_with_it()
    {
        foreach (var bad in new[] { "42", "{ \"EventId\": 7 }", "{ \"eventId\": \"apo\", \"startedUtc\": \"2026-10-31T21:00:00Z\", \"endsUtc\": \"2026-10-31T20:00:00Z\" }" })
        {
            var fs = new MemoryFileStore();
            var log = new LogLines();
            fs.Put(DataFile.State, FileVariant.Main, "{ \"SchemaVersion\": 1, \"purgeUntilUtc\": \"2026-10-31T22:00:00Z\", \"lastStart\": { \"raid\": \"2026-10-31T19:00:00Z\" }, \"Apocalypse\": " + bad + " }");
            var state = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
            state.Load();
            Assert.Null(state.Document.Apocalypse);
            Assert.Equal(Zones.Utc(2026, 10, 31, 22, 0), state.Document.PurgeUntilUtc);
            Assert.Equal(Zones.Utc(2026, 10, 31, 19, 0), state.Document.LastStart["raid"]);
            Assert.Equal(1, log.Count("apocalypse record unreadable: "));
            Assert.Equal(1, log.Count("; nothing resumes"));
            Assert.True(state.Dirty);                                           // the next write leaves it out
            Assert.Null(state.SaveNow());
            Assert.DoesNotContain("Apocalypse", fs.Text(DataFile.State, FileVariant.Main));
        }
    }

    [Fact]
    public void Record_fails_when_a_row_carries_a_name_or_a_position()
    {
        var record = Recorded(out _, out _);
        var json = Encoding.UTF8.GetString(new StateDocument { Apocalypse = record }.Serialize());
        Assert.DoesNotContain("Chaos", json);
        Assert.DoesNotContain("\"name\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Player", json);
        // the only coordinates are the goal, the hotspots, the fronts and a horde march's arrival
        var keys = new List<string>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) { keys.Add(p.Name); Walk(p.Value); }
            else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) Walk(x);
        }
        Walk(JsonDocument.Parse(json).RootElement.GetProperty("Apocalypse"));
        Assert.Equal(["arrival", "arrived", "deaths", "endsUtc", "eventId", "first", "frontX", "frontZ", "goal", "growth", "hordes", "hordesRisen", "hotspots", "kills",
                "living", "livingBosses", "mode", "number", "platformId", "prefab", "roamers", "scores", "spreadWindow", "startedUtc", "x", "y", "z"],
            keys.Distinct().OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Record_fails_when_an_ended_run_keeps_its_record()
    {
        // step 2 review L4: the real end path (Logic/ApocalypseHost.Ended, which EventRuntime calls on every path, checked by
        // ApocalypseTests.EndPaths_fails_when_the_runtime_skips_an_apocalypse_end_path) over the real state store and file
        foreach (var path in Enum.GetValues<ApocalypseEnd>())
        {
            var rig = ApocalypseDependencyFailureTests.Running();
            Assert.Equal("apo", rig.State.Document.Apocalypse?.EventId);         // Begin wrote it
            rig.Host.Ended("apo", path, rig.Game, rig.State);
            rig.Files.Now = T0.AddSeconds(5);
            rig.State.Flush();
            var json = Encoding.UTF8.GetString(rig.Files.Read(DataFile.State, FileVariant.Main)!);
            var kept = path is ApocalypseEnd.Restart or ApocalypseEnd.Disable;
            Assert.True(kept == json.Contains("\"Apocalypse\"", StringComparison.Ordinal), $"{path}: record kept {!kept}");
            Assert.True(kept == (rig.Host.Run is not null), $"{path}: run kept {!kept}");
        }
        // the purge and a pillar off in the drain wait remove a pending resume's record too
        var purged = ApocalypseDependencyFailureTests.Running();
        purged.Host.Purged(purged.Game, purged.State);
        Assert.Null(purged.State.Document.Apocalypse);
    }

    [Fact]
    public void Record_fails_when_a_record_with_a_bad_field_is_kept()
    {
        var good = Recorded(out _, out _);
        Assert.Null(good.Problem());
        foreach (var (mutate, reason) in new (Action<ApocalypseRecord>, string)[]
                 {
                     (r => r.EventId = "", "eventId missing"),
                     (r => r.EndsUtc = r.StartedUtc, "startedUtc is not before endsUtc"),
                     (r => r.Hotspots.Clear(), "hotspots must hold 1-5 entries"),
                     (r => r.Roamers.Add(1), "roamers must hold one count per hotspot"),
                     (r => r.HordesRisen = 11, "hordesRisen must be 0-10"),
                     (r => r.Hordes[0].Number = 3, "a horde's number or living count is out of range"),
                     (r => r.Hordes[1].LivingBosses[1].Living = 99, "a horde's living bosses are out of range"),
                     (r => r.Hordes[1].LivingBosses[0].Prefab = "", "a horde's living bosses are out of range"),
                     (r => r.Hordes[1].Number = 1, "a horde is listed twice"),
                     (r => r.SpreadWindow = -1, "spreadWindow is negative"),
                     (r => r.Roamers[0] = 100_000, "the living counts exceed the tracked-unit cap"),
                     (r => r.Hordes[0].Living = int.MaxValue, "the living counts exceed the tracked-unit cap"),
                     (r => r.Scores[0].PlatformId = "", "scores are out of range"),
                 })
        {
            var r = Recorded(out _, out _);
            mutate(r);
            Assert.Equal(reason, r.Problem());
            var back = Reparse(new StateDocument { Apocalypse = r }).Apocalypse!;
            Assert.Equal(reason, back.Unreadable);
        }
    }

    [Fact]
    public void Record_passes_a_resumed_run_rebuilds_from_the_saved_file()
    {
        var record = Recorded(out _, out _);
        var fs = new MemoryFileStore();
        var log = new LogLines();
        var state = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        state.Load();
        state.Document.Apocalypse = record;
        Assert.Null(state.SaveNow());
        var reloaded = new StateStore(new DataStore(fs, log.Add), () => fs.Now, log.Add);
        reloaded.Load();
        var back = reloaded.Document.Apocalypse!;
        Assert.Empty(log.Lines);
        Assert.Equal(ResumeRule.Rebuild(record, Apo.Def(Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson() + ", " + Apo.BossJson(Apo.Deadeye) + " ]")).Apocalypse!),
            ResumeRule.Rebuild(back, Apo.Def(Apo.Action(extra: "\"bosses\": [ " + Apo.BossJson() + ", " + Apo.BossJson(Apo.Deadeye) + " ]")).Apocalypse!));
    }

    [Fact]
    public void Record_empty_no_run_no_key()
    {
        var json = Encoding.UTF8.GetString(new StateDocument().Serialize());
        Assert.DoesNotContain("Apocalypse", json);
        Assert.Null(StateDocument.TryParse(Encoding.UTF8.GetBytes("{ \"SchemaVersion\": 1 }"))!.Apocalypse);
        Assert.Null(StateDocument.TryParse(Encoding.UTF8.GetBytes("{ \"SchemaVersion\": 1, \"Apocalypse\": null }"))!.Apocalypse);
    }
}
