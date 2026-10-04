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
    The player read reuses one query over User (A6), and the verbose "player triggers" line is written only when its
    counts change: a console write every 5 s scan cost up to 190 ms (A6). A spawn point needs a walkable line from the
    picked player (A7, design §9 D31; docs/features/WALKABLE_SPAWNS.md). With Debug.TimingLog, each "tick timing" line
    ends ", <n> player scans" when the scan read players in that window (A8, D21). Phantom players exist only in a Debug build.
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

### Session 1b · 2026-09-29 · A4-A6 fixes (e1ba90a, dev world nyardev, with the owner; 1c the same day, same boot)

Setup: the e1ba90a Release build deployed with the server stopped; `pwsh tools/dev-snapshot.ps1 -Save au1b`;
Debug.VerboseLogging and Debug.TimingLog on; three enabled definitions written to the dev events.json before the boot,
each 3 Bandit Thugs and 2 Bandit Hunters, one wave, 180 s, AroundPlayer 20-40 m, no Hunt: au-border (RegionEntered
DunleyFarmlands, cooldown 2 min), au-reprisal (FactionKills Faction_Bandits, 5 in 300 s) and au-here (Manual). Both
logs of each boot were copied to the session scratchpad before each stop.

- [x] A6, before the owner joined (0 players): the timing split showed read 0.0 ms, rest 0.0-1.3 ms, gc 0 and log
  8.7-190.6 ms per scan; the cost was the verbose "player triggers: <n> players, <m> entries" console write. With the
  line written only on change (e1ba90a), the slowest tick of each window after the first was 0.1-0.2 ms, and "player
  triggers" never reached 5 ms again in the session (the first scan after boot: 23.5 ms, 12.2 of it the read's JIT)
- [x] A4, au-border: two crossings into Dunley at different points, "started by RegionEntered" each time; the owner saw
  all five units both times (the second time behind them: the 5 s scan centres the ring where the player was, and the
  owner had walked on); `debug here 60` listed each wave's five units
- [x] A5: every wave logged "walk h 9" or "walk h 10"; no "walk check unavailable", no degraded notice
- [x] D12, FactionKills: "au-reprisal reached 5", then "started by FactionKills", in 1b and again in 1c; `debug here 60`
  listed its five units
- [ ] reachability (A7): 1b, au-here at a pond: the units stood on the far side and never noticed the owner. 1c,
  au-reprisal: its units stood across a bridge, idle until the owner walked past them. 1b and 1c, au-here at a rock
  face near the world edge: the owner found no unit although `debug here 60` listed all five (two had lost health,
  fighting wolves out of sight). The walk check tests the spot, not a path from it to the player; the owner notes that
  V Rising NPCs neither see nor reach a player across cliffs and scenery. Fixed by the line test of A7, re-checked in
  Session 1d (D33)
- warnings, all boots: ours are "event au-here stopped: 5 units queued, 0 spawns cancelled" and the example definition's
  reason; the others are Il2CppInterop's substitute notice and two Beelzebub TUNE lines. NyarDev.log: the 226
  PrefabLookupMap save-load warnings only. No [Error] line and no exception in either log

### Sessions 1d-1f · 2026-09-29 · A7-A16 placement (3207666, e6c5e86, 043fa6b, with the owner)

Each recorded in docs/audits/automation.md with its log check; logs in %TEMP% nyar-s1d-logs, nyar-s1e-logs and
nyar-s1f-logs. 1d: the rock face passed; the pond and a river crossing left every point unchecked (A11, A12). 1e: the
walk surveys read the owner's own spot as not grounded or blocked at one height level (A13, A14). 1f: on a bridge deck
and on a strip between a pond and a cliff the player's spot read blocked at every level (A16).

### Session 1g · 2026-10-04 · D33 by vrclient (d907c9d, dev world nyardev, no owner)

Setup: d907c9d deployed (unchanged since 1f); the dev-only NyarDevTools plugin (tools/vrclient/NyarDevTools) for
`.devtp`/`.devmark`; the character Chaos invulnerable (`ToggleInvulnerable`); VerboseLogging and TimingLog on. Scenario
`tools/vrclient/scenarios/automation-reach.vrs` run once per spot: au-here (no Hunt), `debug here 60`, stop; au-hunt
(Hunt 60), 20 s, `debug here 15`, stop. Spots: "ravine", a rock ravine by the water 40 m east of the castle, and
"pond", a strip of land with rocks inside a pond about 140 m north-east of it (screenshots in %TEMP%\vrclient\shots).

- [x] smoke and devtools scenarios: `SCENARIO PASS smoke 3/3`, `SCENARIO PASS devtools 7/7`
- [x] ravine, au-here: "5 units queued (0 moved, 5 shortened, 0 unchecked)"; `debug here` placed all five 10-13 m from the
  character, on the ravine floor; `SCENARIO PASS automation-reach 11/11`
- [x] ravine, au-hunt: "5 units queued (3 moved, 2 shortened, 0 unchecked)"; after 20 s `debug here 15` listed all five
  within 2-7 m of the character
- [ ] pond, au-here: "5 units queued (0 moved, 0 shortened, 5 unchecked: 5 no walkable line; the player's level 10)";
  the survey "spot moved 1 m E, level 10 … E 10 m blocked …"; the five stood about 38 m north-west, across the water.
  The straight line from a strip inside a pond is blocked in every direction, so the units fall back to unchecked
  points: the limit of straight lines that Session 1f foresaw (option B, a local flood fill over the tile map) → A17
- [x] pond, au-hunt: "5 units queued (0 moved, 5 shortened, 0 unchecked)"; after 20 s all five within 1-2 m of the
  character; `SCENARIO PASS automation-reach 11/11`
- not run: the bridge deck (no bridge spot was found yet; world bridges are scenery, not prefab entities to look up)
- logs: see the audit's Session 1g log-check line

### Session 2 · 2026-10-04 · D7, D21, D22 by vrclient (Debug build of ab27817, dev world nyardev, no owner)

Setup: the Debug build with Debug.FaultInjection = phantoms:4, VerboseLogging and TimingLog on; Chaos invulnerable at
x -1880, z -1640 (Farbane Woods, outside claimed territory; the four phantoms at +200 to +800 m on x). Events in the dev
events.json: s2-fan-a/b/c (Manual, 1500 s, Thug ×3 + Hunter ×2, 3 waves at 30 s, AroundPlayer 20-40 m, fanOut 5/150,
Hunt 60, lifetime 1500 s), s2-interval (Interval 5-6 min, one Thug at a Point), s2-fan-short (as s2-fan-a, 2 waves,
600 s, lifetime 240 s, for the restart and the uninstall); au-border (RegionEntered) and au-reprisal (FactionKills)
enabled throughout. Logs: %TEMP%\nyar-s2-logs (run1 the session, run2-run5 the boots after it).

- [x] D7 fan-out: every wave of the three events logged "phantoms: 4 of 4 placed" and "wave k/3 around 5 players: 20 units
  queued (…)" after "clamped by MaxUnitsPerWave: 25 -> 20"; four "phantom group <i> <d> m from its phantom" lines per
  wave at 21-40 m; `.nyar debug here` showed the fifth group 2-8 m from Chaos; with all three running, fan-b wave 3
  "skipped by MaxTrackedUnits: 10 of 20" and fan-c wave 3 "20 of 20"; every health line "3 events, 150 tracked"
- [x] D21 tick budget: `pwsh tools/preflight.ps1 -TimingSpan <run1 copy at the twelfth window> -MinScans 10` → "timing
  span: 10/10 windows under 5 ms, tracked >= 140, targets >= 1, player scans >= 10, 0 slow ticks" (windows 0.34-0.50 ms
  average, 12-13 player scans each)
- [x] D22 stop: "event s2-fan-a stopped: 60 units queued", despawn batches of 5 to "0 left"; `.nyar status` "tracked
  units: 90"; no "hunt s2-fan-a" line after the stop
- [x] D22 natural end: "event s2-fan-b ended (3 of 3 waves)", "event s2-fan-c ended (2 of 3 waves)", "<n> units due for
  despawn" batches of 5; `.nyar status` "No active events." "tracked units: 0"; no hunt line after
- [x] D22 purge (Epic D11, spawn part): s2-fan-short (40 units) and s2-interval live; "purge ends 2 events and despawns
  41 units", "purge: 2 events ended, 41 units queued, 0 spawns cancelled, cooldown 240s", nine batches of at most 5;
  `.nyar status` 0 events, 0 tracked about 6 s after the confirm; the second confirm "nothing to purge"; a manual start
  during the cooldown "purge cooldown active (191 s left)"; s2-interval's next start 18:25:39Z, about 350 s after the
  confirm (its 5-6 min draw lands past 240 s anyway; the refused-start redraw is the unit test's)
- [x] D22 restart mid-wave: wave 1 of s2-fan-short spawned, AutoSave_3406, stop; boot: "event s2-fan-short cancelled by
  restart", "boot marker sweep: 20 found, 20 queued for despawn (17 listed in state.json)", four batches to "0 left";
  after AutoSave_3408 boot 2 "boot marker sweep: 0 found". state.json lagged by three units; the markers caught them
- [x] D22 uninstall (Epic D12, spawn part): wave 1 of s2-fan-short spawned, AutoSave_3410, stop, Nyarlathotep.dll
  removed; the server ran 18:37-18:46 without it ("6 plugins to load", 0 Nyarlathotep lines), stopped after
  AutoSave_3415; DLL back, boot: "event s2-fan-short cancelled by restart", "boot marker sweep: 0 found, 0 queued for
  despawn (20 listed in state.json)"; in game "tracked units: 0", `.nyar debug here 60` "no tracked units within 60 m"
- observed, outside D21's span: one "slow tick: 250 ms (hunt 250 ms)" with three events live, its window 59 ticks
  instead of 61 (a host stall during the hunt phase, not repeated in 30 windows); natural-end despawn ticks up to 120 ms
  in the spawn queues at five units per tick (the game's destroy cost, as A70 recorded for wave starts)
- logs: -LogCheck per boot in the audit's Session 2 line; both Unity error kinds (17 "Couldn't remap old Modification
  Id" at load, 226 missing-prefab warnings) appear the same in the boot without Nyarlathotep
