using BepInEx.Configuration;

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
    public static ConfigEntry<int> MaxTrackedUnits { get; private set; }
    public static ConfigEntry<int> MaxUnitsPerWave { get; private set; }
    public static ConfigEntry<int> MaxConcurrentEvents { get; private set; }
    public static ConfigEntry<bool> AnnounceEvents { get; private set; }

#if DEBUG
    // ---- Debug builds only (foundation D25): a Release DLL has no such key and ignores it in the cfg ----
    public static ConfigEntry<string> FaultInjection { get; private set; }
#endif

    static bool FaultArmed => FaultInjection?.Value is { Length: > 0 };

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

        MaxTrackedUnits = config.Bind("Limits", "MaxTrackedUnits", 150,
            "Hard cap on mod-spawned units alive at once across all events. New spawns are skipped past this cap.");
        MaxUnitsPerWave = config.Bind("Limits", "MaxUnitsPerWave", 20,
            "Hard cap on units in a single wave, regardless of what an event definition requests.");
        MaxConcurrentEvents = config.Bind("Limits", "MaxConcurrentEvents", 3,
            "How many events (of any pillar) may be active at the same time.");

#if DEBUG
        FaultInjection = config.Bind("Debug", "FaultInjection", "",
            "Debug builds only. An event id (its tick throws) or hook:<name> (that hook reports unavailable). Empty: off.");
#endif
    }
}
