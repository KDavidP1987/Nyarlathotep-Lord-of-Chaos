using System.Collections.Generic;
using BepInEx.Configuration;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Config;

/// <summary>
/// BepInEx config bindings (BepInEx/config/kdpen.Nyarlathotep.cfg). This file holds only the
/// global switches and safety caps; per-event definitions (which faction, which trigger, which
/// wave composition) live in JSON under BepInEx/config/Nyarlathotep/ because they are lists of
/// structured records, not scalar settings. See docs/NYARLATHOTEP_DESIGN.md §"Configuration".
/// Every pillar ships DISABLED so installing the mod changes nothing until an admin opts in.
/// </summary>
internal static class Settings
{
    // ---- Global ----
    public static ConfigEntry<bool> Enabled { get; private set; }

    // ---- Pillar switches ----
    public static ConfigEntry<bool> EmpowermentEnabled { get; private set; }
    public static ConfigEntry<bool> SiegeWavesEnabled { get; private set; }
    public static ConfigEntry<bool> DefendedZonesEnabled { get; private set; }
    public static ConfigEntry<bool> BossReinforcementsEnabled { get; private set; }
    public static ConfigEntry<bool> EventSpawnsEnabled { get; private set; }

    // ---- Safety caps (server-wide; protect tick time and the save file) ----
    // Every Limits-section key of Logic/Limits, bound and clamped to its range at load (D5). Read through Limit().
    static readonly Dictionary<string, int> _limits = new();
    public static ConfigEntry<bool> AnnounceEvents { get; private set; }

    /// <summary>The loaded, clamped value of <paramref name="limit"/>; its default before Initialize.</summary>
    public static int Limit(IntLimit limit) => _limits.TryGetValue(limit.Name, out var v) ? v : limit.Default;

    // ---- Debug ----
    public static ConfigEntry<bool> TimingLog { get; private set; }

#if DEBUG
    // ---- Debug builds only (foundation D25): a Release DLL has no such key and ignores it in the cfg ----
    public static ConfigEntry<string> FaultInjection { get; private set; }
#endif

    public static void Initialize(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Master switch. When false, no events run and no units are spawned.");
        AnnounceEvents = config.Bind("General", "AnnounceEvents", true,
            "Broadcast a server-wide chat message when an event starts or ends.");

        EmpowermentEnabled = config.Bind("Pillars", "FactionEmpowerment", false,
            "Timed faction empowerment (NPC 'blood moon'): scheduled or trigger-driven buffs on every NPC of a faction.");
        SiegeWavesEnabled = config.Bind("Pillars", "SiegeWaves", false,
            "NPC waves that spawn and march on player castles.");
        DefendedZonesEnabled = config.Bind("Pillars", "DefendedZones", false,
            "Admin-defined zones that summon reinforcements when vampire activity inside them crosses a threshold.");
        BossReinforcementsEnabled = config.Bind("Pillars", "BossReinforcements", false,
            "Summon adds during V Blood boss fights.");
        EventSpawnsEnabled = config.Bind("Pillars", "EventSpawns", false,
            "Admin-scheduled or command-triggered wave spawns into chosen areas.");

        BindLimit(config, Limits.MaxTrackedUnits,
            "Hard cap on mod-spawned units alive at once across all events. New spawns are skipped past this cap.");
        BindLimit(config, Limits.MaxUnitsPerWave,
            "Hard cap on units in a single wave, regardless of what an event definition requests.");
        BindLimit(config, Limits.MaxConcurrentEvents,
            "How many events (of any pillar) may be active at the same time.");
        BindLimit(config, Limits.MaxSpawnsPerTick,
            "Most units spawned in one server tick; a larger wave spreads over the next ticks.");
        BindLimit(config, Limits.MaxDespawnsPerTick,
            "Most units despawned in one server tick (many destroys in one frame can crash the server).");
        BindLimit(config, Limits.GraceSeconds,
            "Seconds an event's units outlive the event's end before they expire.");
        BindLimit(config, Limits.PurgeCooldownSeconds,
            "After .nyar purge confirm, seconds during which no event starts and no unit is spawned.");
        BindLimit(config, Limits.ManualSpawnLifetimeSeconds,
            "Lifetime in seconds of a unit spawned with .nyar spawn.");

        TimingLog = config.Bind("Debug", "TimingLog", false,
            "Log the scheduler tick's average and maximum duration once a minute.");

#if DEBUG
        FaultInjection = config.Bind("Debug", "FaultInjection", "",
            "Debug builds only. An event id (its tick throws) or hook:<name> (that hook reports unavailable). Empty: off.");
#endif
    }

    static void BindLimit(ConfigFile config, IntLimit limit, string description)
    {
        var entry = config.Bind(limit.Section, limit.Key, limit.Default,
            $"{description} Range {limit.Min}-{limit.Max}; a value outside it is clamped at load.");
        var (value, log) = limit.Clamp(entry.Value);
        if (log is not null) Plugin.PluginLog.LogWarning($"[nyar] {log}");
        _limits[limit.Name] = value;
    }
}
