# Automation — interval, player-action triggers and fan-out

**Status:** in progress (docs/dod/automation.md, audit docs/audits/automation.md). Step 1 (schema, validation, the pure
planners, chat fields, api 6) is built. Step 2 wires them into the services; Session 1 tests them in game. Step 3 adds
the templates and Session 2 (fan-out, tick budget, end paths). Step 4 is the 0.8.0 release.

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
- **Services (step 2):**
  - The tick phase "interval" polls the clock every tick through `IntervalClock.PollState`, which marks state.json
    dirty when a next start changes. The tick phase "player triggers" scans the players every 5 s.
  - Patches/DeathEventPatch reads whether the victim is ours and calls SpawnTracker.Died. The V Blood path and
    TriggerBus.Died then run each in its own try/catch, so neither can skip another death.
  - Logic/PlayerTriggers.cs `PlayerTriggerFeed` guards the scan and the kill read. A failure logs once per streak and
    holds a health entry ("triggers: player scan failing", "triggers: kill read failing") until a read succeeds.
  - A player-action start is focused on its player. Its refusal line is throttled to one per definition per minute.
  - WaveAction picks with `ChooseMany` (the focus first), sizes with `DecideGroups`, gives each group its own Hunt tag,
    and reports each wave once: `WaveRun.Run` queues the groups, then calls `EventEngine.WaveDecided`.
  - An AroundPlayer group is never regrouped: its units stay where the game grounds them at their ring points, and
    its walk check reads at the player's height (A4, design §9 D30). The walk check reads heights from -100 m (A5).
    The player read reuses one query over User (A6). Phantom players exist only in a Debug build.
- **Privacy:** no line names or locates a player. The per-player rows (regions, cooldowns, kill counters, the start's
  focus) live in memory only; state.json gains only `NextInterval`.
- **Wire:** api 6 adds the trigger values `interval`, `regionentered` and `factionkills`; a fanned-out wave sends one
  wave push.

## Open questions

None open. The owner's decisions (1A-3A, F1 option A) are recorded in the plan's Assumptions.

## Test results

Step 1 is covered by the unit tests (AutomationTests, EventValidationTests, CommandArgTests, AuthoringTests,
ContractDocTests, PushTests and PrivacyTests, the automation cases).

### Session 1 · 2026-09-29 · automation step 2 (75d92a7, dev world nyardev, with the owner)

Setup: the step-2 Release build deployed; `pwsh tools/dev-snapshot.ps1 -Save au1`; four invalid definitions added to
the dev events.json (au-bad-interval, au-bad-fanout, au-bad-region, au-bad-kills); Debug.VerboseLogging on. The owner
built au-tick (Interval 5-6 min, a Point, 3 Thugs, 60 s), au-border (RegionEntered DunleyFarmlands, cooldown 5 min,
AroundPlayer 20-40 m) and au-reprisal (FactionKills Faction_Bandits, 5 kills, AroundPlayer 20-40 m) with
`.nyar template use bandit-ambush as <id>` and `.nyar event set`. A value with spaces is quoted:
`.nyar event set <id> location "aroundplayer 20 40"`, `.nyar event set <id> action.fanOut "3 150"`; the steps first
given without quotes got VCF's usage line, and au-border stayed on the template's Admin location, disabled ("action.
location Admin needs a Manual trigger"), until the quoted set. Set the location before trigger.type. Both logs of both
boots were copied to the session scratchpad before each stop; the snapshot was restored afterwards.

- [x] D27, event list: the four reasons whole in chat, "au-bad-fanout invalid: action.fanOut needs an AroundPlayer
  location", "au-bad-interval invalid: trigger.minMinutes must be at most trigger.maxMinutes", "au-bad-kills invalid:
  faction Faction_Players is deny-listed", "au-bad-region invalid: trigger.scope must name regions for RegionEntered"
- [x] D27, set replies and refusals: every set accepted with its field and value; "trigger.minMinutes must be 5-1440"
  and "action.fanOut needs an AroundPlayer location" whole; a set that leaves the definition invalid ends "; now
  disabled: <reason>"
- [x] D27, event info: "au-tick … trigger interval 5-6 min duration 60s" and "next start in 6 min"; "au-border …
  trigger regionentered cooldown 5 min", "trigger scope: DunleyFarmlands"; "au-reprisal … trigger factionkills Bandits
  5 in 300s"; every line whole
- [x] D3, unattended starts: "event au-tick started by Interval" at 17:38:33, 17:45:11, 17:51:32 and 17:58:04 local,
  each 5.3-5.6 min after the previous instance ended, with no admin command
- [x] D3, restart: state.json held NextInterval au-tick 22:04:53.730Z after its end; after autosave 2070 the server was
  stopped and booted (init 18:00:16); "event au-tick started by Interval (ends 22:05:53Z)" was seen at 18:04:54.2 local
  by a 0.3 s poll, under 0.5 s after the stored time
- [x] D12, RegionEntered start: walking from Farbane into Dunley logged "player triggers: 1 players, 1 entries" and
  "event au-border started by RegionEntered" at 17:40:21, 17:46:35 and 17:52:43 (each after the 5 min cooldown ended)
- [x] D12, non-starts: after the 17:52:43 start, a re-entry, a relog inside Dunley and a second re-entry within 5 min
  started nothing ("0 entries" on every scan; the relog showed "0 players" scans, then "1 players, 0 entries")
- [x] D12, FactionKills: six bandit kills at a Farbane camp; "faction kills: au-reprisal reached 5", then "event
  au-reprisal started by FactionKills"; three more bandit kills while it ran started nothing; `.nyar debug here 60`
  listed the five au-reprisal Thugs; `.nyar event stop au-reprisal` queued all five
- [ ] D12, the wave near the owner: `.nyar debug here 60` listed every au-border and au-reprisal unit within 60 m, but
  the owner saw none of them. Each wave logged "regrouped … from height 10.0 (6.5, -5.9, -4.5) to its centre at height
  0.0": an AroundPlayer ring point takes the player's height as its anchor, and the regroup (event-spawns A23) moves a
  unit the game placed on the ring point's own level to that height, into the terrain. 0.7.0 has the same anchor;
  event-spawns Session 1 step 8 passed on flat ground. To be fixed and re-checked
- [ ] walk check: one au-border wave logged "walk check unavailable: height out of range" with the centre at height
  -0.0 (a player height just below 0; WalkHeight refuses y < 0, while the terrain has levels below 0, e.g. -4.5), the
  wave went out unchecked and the admin got "nyar: degraded: spawns: walk check unavailable". To be fixed
- [ ] tick timing: the "player triggers" phase was the slowest phase in several windows, 78-295 ms ("slow tick: 253 ms
  (player triggers 253 ms)"), with one player; one spawn tick took 1675 ms (6 units, the first au-border wave). To be
  measured and fixed before Session 2's budget run (D21)
- warnings, boot 1: ours are the definition reasons (each reload repeats them), "walk check unavailable" (above), two
  slow ticks (above) and "event au-reprisal stopped: 5 units queued, 0 spawns cancelled"; the others are
  Il2CppInterop's substitute notice and two Beelzebub TUNE lines. Boot 2: the reasons and the same three others.
  NyarDev.log in each boot: 226 PrefabLookupMap "is in an unknown state" warnings, one per GUID, logged while the save
  loads, before our plugin initializes; 0 exceptions. No [Error] line in either log
