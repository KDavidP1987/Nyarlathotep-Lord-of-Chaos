#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The chat names of the pillars and their [Pillars] keys in the cfg (event-library D13). PillarSwitchTests
/// reads every `Bind("Pillars", …)` key from Config/Settings.cs and compares it with this map.</summary>
public static class PillarNames
{
    public static readonly IReadOnlyList<(string Name, Pillar Pillar, string Key)> All =
    [
        ("empowerment", Pillar.Empowerment, "FactionEmpowerment"),
        ("spawns", Pillar.Spawns, "EventSpawns"),
        ("boss", Pillar.Boss, "BossReinforcements"),
        ("zones", Pillar.Zones, "DefendedZones"),
        ("sieges", Pillar.Sieges, "SiegeWaves"),
    ];

    public const string Choices = "empowerment, spawns, boss, zones or sieges";

    public static string Unknown(string name) => $"unknown pillar {name}; use {Choices}";

    public static string Name(Pillar p) => All.First(x => x.Pillar == p).Name;

    public static string Key(Pillar p) => All.First(x => x.Pillar == p).Key;

    public static bool TryParse(string? name, out Pillar pillar)
    {
        foreach (var x in All)
            if (x.Name == name) { pillar = x.Pillar; return true; }
        pillar = default;
        return false;
    }

    /// <summary>What is wrong with a name map against the cfg's [Pillars] keys: a key without a name, a name whose key
    /// the cfg does not bind, or two names sharing a key. Empty when the map and the keys agree; no keys at all is a
    /// problem.</summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<(string Name, string Key)> map, IReadOnlyCollection<string> bindKeys)
    {
        var problems = new List<string>();
        if (bindKeys.Count == 0) problems.Add("Settings.cs binds no [Pillars] key");
        foreach (var key in bindKeys.Where(k => map.All(m => m.Key != k))) problems.Add($"[Pillars] {key} has no chat name");
        foreach (var m in map.Where(m => !bindKeys.Contains(m.Key))) problems.Add($"pillar {m.Name} maps to {m.Key}, which Settings.cs does not bind");
        foreach (var g in map.GroupBy(m => m.Key).Where(g => g.Count() > 1)) problems.Add($"{string.Join(" and ", g.Select(m => m.Name))} share [Pillars] {g.Key}");
        return problems;
    }
}

/// <summary>The cfg as `.nyar pillar` sees it (event-library D14, D19, S-11). Services/PillarSwitches backs it with the
/// BepInEx ConfigEntries; the tests with a fake file. Every member may throw.</summary>
public interface IPillarStore
{
    bool GeneralEnabled { get; }

    /// <summary>The in-memory value of the pillar's ConfigEntry.</summary>
    bool Get(Pillar pillar);

    /// <summary>Re-reads the cfg from disk into every entry (ConfigFile.Reload): a hand edit saved to disk is in memory
    /// afterwards, and a missing or unparsable key reads as its default, off.</summary>
    void Reload();

    /// <summary>Sets the entry's Value; BepInEx saves the cfg itself (SaveOnConfigSet).</summary>
    void Set(Pillar pillar, bool on);
}

/// <summary>`.nyar pillar list` and `.nyar pillar &lt;name&gt; on|off` (event-library D14, D19; Business rules 6).
/// A switch reloads the cfg first, so an operator's saved hand edit is in memory and kept by the save; it then sets only
/// that entry. `off` ends the pillar's running events through the stop path (<paramref name="endEvents"/>, S-7).</summary>
public sealed class PillarCommand(IPillarStore store, Func<Pillar, IReadOnlyList<string>> endEvents, Action<string> log)
{
    public IReadOnlyList<string> List()
    {
        var lines = new List<string>();
        if (!store.GeneralEnabled) lines.Add(EventLines.MasterOff);
        foreach (var (name, pillar, key) in PillarNames.All) lines.Add($"{name} {OnOff(store.Get(pillar))} (Pillars.{key})");
        return lines;
    }

    public IReadOnlyList<string> Switch(string name, string state)
    {
        if (!PillarNames.TryParse(name, out var pillar)) return [PillarNames.Unknown(name)];
        if (state is not ("on" or "off")) return ["use on or off"];
        var want = state == "on";
        try { store.Reload(); }
        catch (Exception ex) { return [$"pillar {name} not changed: could not read the cfg: {ex.Message}"]; }
        if (store.Get(pillar) == want)
        {
            // An operator's cfg edit may have turned the pillar off with its events still running: off always ends them.
            var already = new List<string> { $"pillar {name} already {state}" };
            if (!want) already.AddRange(End(pillar));
            return already;
        }

        try { store.Set(pillar, want); }
        catch (Exception ex)
        {
            // The save may have truncated or half written the file: memory and the reply follow what it holds now.
            try { store.Reload(); }
            catch (Exception reread)
            {
                log($"pillar {name}: cfg save failed ({ex.Message}) and the cfg could not be read back ({reread.Message})");
                return [$"pillar {name} could not be saved; the cfg could not be read back: {reread.Message}"];
            }
            var now = store.Get(pillar);
            log($"pillar {name}: cfg save failed ({ex.Message}); the file says {OnOff(now)}");
            var failed = new List<string> { $"pillar {name} could not be saved; the file says {OnOff(now)}" };
            if (!want && !now) failed.AddRange(End(pillar));
            return failed;
        }

        log($"pillar {name} {state} (saved to cfg)");
        var lines = new List<string> { $"pillar {name} {state} (saved to cfg)" };
        if (!want) lines.AddRange(End(pillar));
        return lines;
    }

    IEnumerable<string> End(Pillar pillar)
    {
        foreach (var id in endEvents(pillar))
        {
            var line = $"event {id} ended (pillar off)";
            log(line);
            yield return line;
        }
    }

    static string OnOff(bool on) => on ? "on" : "off";
}

/// <summary>The [Pillars] switches as the cfg file on disk holds them (event-library D14, D19). BepInEx's ConfigFile.Reload
/// sets only the keys it finds and can parse, so a missing or garbled key would keep its in-memory value; the keys this
/// returns are the ones the file holds with true or false, and Services/PillarSwitches reads every other one as off.</summary>
public static class PillarCfg
{
    public static IReadOnlyDictionary<string, bool> Read(string? text)
    {
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        var inPillars = false;
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { inPillars = line == "[Pillars]"; continue; }
            if (!inPillars) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            if (bool.TryParse(line[(eq + 1)..].Trim(), out var on)) values[line[..eq].Trim()] = on;
        }
        return values;
    }
}
