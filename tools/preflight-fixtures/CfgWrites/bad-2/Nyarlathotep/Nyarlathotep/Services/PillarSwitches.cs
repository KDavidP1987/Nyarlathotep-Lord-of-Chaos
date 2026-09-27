using System.Collections.Generic;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// The [Pillars] switches of the cfg as `.nyar pillar` changes them (event-library D13, D14, D19; S-11). The only file
/// that sets a pillar's ConfigEntry.Value or calls ConfigFile.Reload (Test-CheckCfgWrites); BepInEx saves the cfg
/// itself (SaveOnConfigSet), never a ConfigFile.Save here. `off` ends the pillar's running events through
/// EventRuntime's stop path (S-7).
/// </summary>
internal sealed class PillarSwitches : IPillarStore
{
    static readonly PillarSwitches Store = new();
    static readonly PillarCommand Command = new(Store, EventRuntime.EndPillar, line => Core.Log.LogInfo($"[nyar] {line}"));

    /// <summary>`.nyar pillar list`: reads only.</summary>
    internal static IReadOnlyList<string> List() => Command.List();

    /// <summary>`.nyar pillar &lt;name&gt; on|off`: one reply line per "\n".</summary>
    [Mutating]
    internal static string SetPillar(string name, string state) => string.Join("\n", Command.Switch(name, state));

    public bool GeneralEnabled => Settings.Enabled.Value;

    public bool Get(Pillar pillar) => pillar switch
    {
        Pillar.Empowerment => Settings.EmpowermentEnabled.Value,
        Pillar.Spawns => Settings.EventSpawnsEnabled.Value,
        Pillar.Boss => Settings.BossReinforcementsEnabled.Value,
        Pillar.Zones => Settings.DefendedZonesEnabled.Value,
        Pillar.Sieges => Settings.SiegeWavesEnabled.Value,
        _ => false,
    };

    /// <summary>ConfigFile.Reload, then every [Pillars] key the file lacks or cannot parse is set off in memory without a
    /// save: BepInEx's Reload keeps such a key's old value, and D19 reads it as its default, off.</summary>
    public void Reload()
    {
        var config = Plugin.Instance.Config;
        config.Reload();
        var inFile = PillarCfg.Read(System.IO.File.Exists(config.ConfigFilePath) ? System.IO.File.ReadAllText(config.ConfigFilePath) : null);
        var save = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        try
        {
            foreach (var (_, pillar, key) in PillarNames.All)
                if (!inFile.ContainsKey(key) && Get(pillar)) Set(pillar, false);
        }
        finally { config.SaveOnConfigSet = save; }
    }

    public void Set(Pillar pillar, bool on)
    {
        switch (pillar)
        {
            case Pillar.Empowerment: Settings.EmpowermentEnabled.Value = on; break;
            case Pillar.Spawns: Settings.EventSpawnsEnabled.Value = on; break;
            case Pillar.Boss: Settings.BossReinforcementsEnabled.Value = on; break;
            case Pillar.Zones: Settings.DefendedZonesEnabled.Value = on; break;
            case Pillar.Sieges: Settings.SiegeWavesEnabled.Value = on; break;
        }
        Plugin.Instance.Config.Save();
    }
}
