# Automation — interval, player-action triggers and fan-out

**Status:** in progress (docs/dod/automation.md, audit docs/audits/automation.md). Step 1 (schema, validation, the pure
planners, chat fields, api 6) is built; step 2 wires them into the services and Session 1 tests them in game; step 3
adds the templates and Session 2 (fan-out, tick budget, end paths); step 4 is the 0.8.0 release.

## Goal

Most events should start on their own. 0.8.0 adds three trigger types and one wave key so an admin can build a server
where events come without anyone typing a command:

- **Interval:** the event starts again a random `minMinutes`-`maxMinutes` after its last start ended. The next start is
  kept in state.json, so a restart does not reset it; downtime is not replayed.
- **RegionEntered:** a player walking into one of the event's regions starts it, with a per-player cooldown.
- **FactionKills:** `kills` kills of the listed factions within `windowSeconds` start it, counted per player or shared.
- **fanOut** on an AroundPlayer wave: the wave spawns one group near each of up to `maxInstances` players at least
  `minSpacing` m apart. The whole wave still obeys MaxUnitsPerWave and MaxTrackedUnits; the units are dealt round robin.

## Design summary

- **Logic (step 1):** Logic/Validation.cs parses the new triggers and `action.fanOut`; Logic/Schedule.cs `IntervalClock`
  draws and keeps the next starts; Logic/PlayerTriggers.cs holds the region entries, the kill rule and windows, the
  refusal throttle and the Debug-only phantom players; Logic/Spawning.cs `PlayerPick.ChooseMany` and
  `WaveGate.DecideGroups` pick and size the groups.
- **Services (step 2):** TriggerBus polls the interval clock, scans players every 5 s and reads each death;
  WaveAction spawns the groups.
- **Privacy:** no line names or locates a player. The per-player rows (regions, cooldowns, kill counters, the start's
  focus) live in memory only; state.json gains only `NextInterval`.
- **Wire:** api 6 adds the trigger values `interval`, `regionentered` and `factionkills`; a fanned-out wave sends one
  wave push.

## Open questions

None open. The owner's decisions (1A-3A, F1 option A) are recorded in the plan's Assumptions.

## Test results

No in-game session yet. Step 1 is covered by the unit tests (AutomationTests, EventValidationTests, CommandArgTests,
AuthoringTests, ContractDocTests, PushTests and PrivacyTests, the automation cases).
