# Wave variety — pursuit, unit and boss pools, timed and onslaught templates

**Status:** in progress for 0.13.0 (docs/dod/wave-variety.md, audit docs/audits/wave-variety.md; design §9 D61, Epic
A44). Steps 1 (the logic), 2 (the mix runs) and 3 (services, templates, sessions) are built; step 4 (the release with
0.13.0) is being built.

## Goal

Three additions to SpawnWaves events, each off until a definition uses it:

- **Pursuit** (`action.pursuit { "afterSeconds": 15-600 }`, needs Hunt): once no eligible player has stood in the
  event's area for afterSeconds, the run goes on the offensive until it ends. Its units leave the arena and go after
  the nearest eligible player by the apocalypse roamers' hidden jumps. A castle (claimed territory) and PvP combat
  stay a refuge.
- **Pools** (`action.pools`, 1-8 named lists of 1-30 units): a unit entry `{ "pool", "count" }` draws its count from
  the pool with repeats, and a boss roll `{ "pool", "min", "max" }` draws min..max distinct bosses. Each wave is rolled
  once, when it is due, and the roll is logged.
- **Wave bosses** (`action.waveList[n].bosses`, 1-3 entries, at most 5 bosses): any wave may bring bosses, fixed or
  rolled; the action-level `bosses` stays the final wave's.

Fourteen new templates use them: seven timed (`<base>-timed`, a fixed 60-90 s beat, pursuit after 60 s) and seven
onslaught (`<faction>-onslaught`, pools at about 1.5 times the base counts, one rolled boss mid-run and one or two at
the end).

## The fourteen templates

All are disabled, manual, at the admin's position (`location Admin`), arena 40, Hunt 40, the scoreboard and the K/D
winner, pursuit after 60 s, and a fixed 75 s beat between waves (no wave waits for a clear). No wave passes the default
MaxUnitsPerWave (20) and no run passes MaxTrackedUnits (150) with every wave left alive, bosses at their roll maxima.
Each keeps to one allied group (docs/GAME_ASSETS.md › Faction compatibility). Checked by TemplateLibraryTests
Timed and Onslaught (D15, D16).

**Timed** (`<base>-timed`): the base template's waves and final bosses, on the beat.

| Template | Waves (units) | Run | Final bosses |
|---|---|---|---|
| spider-swarm-timed | 6/9/12/15/18 | 60 | Spider Queen |
| graveyard-waves-timed | 8/9/7 | 24 | none |
| militia-muster-timed | 6/9/9/11/11 | 46 | Militia Leader |
| silverlight-crusade-timed | 7/9/9/11/10 | 46 | Paladin |
| legion-incursion-timed | 6/8/8/8 | 30 | Blood Knight |
| gloomrot-assault-timed | 6/8/9/11 | 34 | Voltage |
| cursed-night-timed | 7/8/9/9 | 33 | Cursed Witch |

**Onslaught** (`<faction>-onslaught`, 30 minutes): three pools from the faction's allied group: `troops` (the lower
half by level), `elite` (the upper half) and `lords` (its V Bloods). The waves are about 1.5 times the base's, at most
20: wave 1 all troops, later waves a quarter elite before the middle wave and two fifths from it on. The middle wave
rolls one lord; the final wave rolls one or two distinct lords (one when the pool has one). Spider bosses come at level
45, as the base's.

| Template | Waves (units) | Pools troops/elite/lords | Boss rolls |
|---|---|---|---|
| spider-onslaught | 9/14/18/20/20 | 3/3/1 | wave 3: 1 · wave 5: 1 |
| undead-onslaught | 12/14/10 | 11/11/7 | wave 2: 1 · wave 3: 1-2 |
| militia-onslaught | 9/14/14/16/16 | 7/6/10 | wave 3: 1 · wave 5: 1-2 |
| silverlight-onslaught | 10/14/14/16/15 | 6/6/5 | wave 3: 1 · wave 5: 1-2 |
| legion-onslaught | 9/12/12/12 | 9/9/4 | wave 2: 1 · wave 4: 1-2 |
| gloomrot-onslaught | 9/12/14/16 | 6/6/5 | wave 2: 1 · wave 4: 1-2 |
| cursed-onslaught | 10/12/14/14 | 5/5/6 | wave 2: 1 · wave 4: 1-2 |

An admin changes any of it per event, e.g. `.nyar event set undead-onslaught action.waveList.3.bosses
pool:lords:2-2` for two bosses at the end every time.

## Design summary

- **The roll** (`Logic/Pools.cs`, `Pools.Resolve`): when a wave is due, each pool entry draws its count from its pool
  with repeats and each boss roll draws min..max distinct bosses; fixed entries pass through. Bosses spawn first and
  the caps (MaxUnitsPerWave, free MaxTrackedUnits slots) cut units before bosses. The log line `event <id> wave
  <n>/<N> rolled: <prefab>x<k>, …; bosses: …` names what came, within the wire size.
- **Pursuit** (`Logic/Pursuit.cs`, run by the region phase every 5 s): a run with `action.pursuit` turns on once no
  eligible player has stood within its arena (or Hunt range) of its centre for afterSeconds, logs `event <id>
  pursuit: on, no player within <r> m for <s> s`, and stays on until the run ends. Its groups are then seeded on the
  nearest eligible player map-wide (`pursuit: group <wave>.<g> on a player`) and moved by the hunt phase's jumps, at
  most 20 per tick shared across events. A player in claimed territory or in PvP combat is never a target. A failing
  check logs `pursuit <id>: check failed: …` and shows in `.nyar status` health.
- **Groups**: a template's units, pools and boss rolls must keep to one allied group (`Logic/FactionGroups.cs`, from
  docs/GAME_ASSETS.md › Faction compatibility); a template that mixes groups waves by wave (realm-gauntlet) must wait
  for each wave to be cleared.

## Open questions

- None. Which units fight beside each other was answered by step 2's mix runs (Sessions 1 and 2 below).

## Test results

### Session 1 · 2026-10-16 · v0.13.0-rc.2 DLL · vrclient as Chaos on the local dev server

Setup: snapshot wvmix (`pwsh tools/dev-snapshot.ps1 -Save wvmix`, 34 files), `[Debug] VerboseLogging = true`. The mix
runs use only `.nyar spawn` and NyarDevTools, which the running DLL already has.

- **D13** `tools/vrclient/scenarios/faction-mix-wide.vrs` → SCENARIO FAIL 284/337
  (%TEMP%\vrclient\results\faction-mix-wide-20261016-122616.json). Spiders (7), Church of Light (17), Legion (22),
  Gloomrot (17), cursed (16) and Blackfangs (12) passed every step: no "died (killer CHAR_<group>" line in either phase,
  every unit counted in place. All 53 failures were the undead (30) and militia (23) counts, `[DEV UNITS] n=0`: those
  passes ran longer than `[Limits] ManualSpawnLifetimeSeconds` (300 s), so their units were despawned by lifetime
  ("despawned CHAR_Undead_Assassin of manual") before the counts, with no death. Rerun in Session 2.
- `CHAR_Undead_ZealousCultist_Ghost` "died (killer unknown)" about 10 s after its spawn.
- One warning: "slow tick: 551 ms (spawn queues 548 ms …)" right after the first spawn of `CHAR_Militia_Hound_VBlood`.

### Session 2 · 2026-10-16 · v0.13.0-rc.2 DLL · vrclient as Chaos on the local dev server

Setup: as Session 1, plus `[Limits] ManualSpawnLifetimeSeconds = 1800`.

- **D13** the undead and militia passes of faction-mix-wide.vrs (same generator, `genmixwide.py undead militia`) →
  SCENARIO FAIL 118/119 (%TEMP%\vrclient\results\faction-mix-wide-rerun-20261016-125738.json). The one failure:
  `CHAR_Undead_ZealousCultist_Ghost` again "died (killer unknown)" seconds after spawning, `.devunits` n=0; recorded
  left out (a timed ghost, not a standalone unit). Every other undead and militia unit and V Blood: no own-group killer
  in either phase, counted in place. The Hound V Blood's slow first spawn did not recur.
- Recorded: docs/GAME_ASSETS.md › Faction compatibility (one row per group, the left-out unit, the slow spawn);
  Logic/FactionGroups.cs grows every group and adds blackfangs (D14, FactionGroupsTests Recorded).


### Session 3 · 2026-10-16 · step 3 Release build · vrclient as Chaos on the local dev server

Setup: snapshot wv3 (`pwsh tools/dev-snapshot.ps1 -Save wv3`, after restoring wvmix), `[Debug] VerboseLogging = true`,
TimingLog on. Three runs of `tools/vrclient/scenarios/pursuit.vrs`, all SCENARIO FAIL, each for a scenario fault:

- Run 1 (22/27): `action.behaviour hunt 25` was sent unquoted, so VCF read two arguments and answered its usage line;
  the event had no Hunt and `action.pursuit` was refused ("action.pursuit needs behaviour Hunt"). Now quoted.
- Runs 2 and 3 (23/27): pursuit turned on ("pursuit: on, no player within 25 m for 20 s"), groups targeted Chaos and
  the region hunt brought the six units 223, 195, 168, 143 m … closer every 5 s; the scenario's no-log window (13 s,
  then 9 s) still saw the line. The clock's cause was not proven, so the next build added the verbose check line.
- Log check: 0 unhandled, 583 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (copy in
  %TEMP%\nyar-wv3-logs\s3-srv); [Warning lines: the known baseline kinds (Il2CppInterop, HarmonyX type loads, Beelzebub TUNE, example-empowerment) and the runs' own "stopped" lines; no [Error.

### Session 4 · 2026-10-16 · step 3 build + the pursuit check line · vrclient as Chaos on the local dev server

- **D17** `pursuit.vrs` → SCENARIO PASS 27/27 (%TEMP%\vrclient\results\pursuit-20261016-140050.json). The check
  line showed the quiet clock reset at each check while Chaos stood at the spot and count 5, 10, 15, 20 s after Chaos left;
  so "on" came 15-20 s after the leaving, up to 5 s early. Fixed after the session (the clock starts at the first check
  that sees nobody); rerun in Session 5.
- **D18** `wave-variety.vrs` → SCENARIO PASS 26/26
  (%TEMP%\vrclient\results\wave-variety-20261016-140858.json). militia-muster-timed: wave 2/5 (9 units) queued on
  the beat with wave 1 alive, no "cleared" line. undead-onslaught: wave 1/3 rolled 12 troops (no boss), wave 2/3 rolled
  14 units and one boss (CHAR_Undead_Infiltrator_VBlood), wave 3/3 with the final roll set to 2-2 rolled
  CHAR_Undead_Leader_Vblood and CHAR_Undead_ZealousCultist_VBlood, and all three "spawned … for undead-onslaught". Every
  roll named only undead-pool units. Both runs stopped to "tracked units: 0".
- **D20** `pursuit-timing.vrs` → SCENARIO PASS 25/25, but -TimingSpan "6 tracked in the span, below 20": the
  level-20 skeletons pursued Chaos into a bear and wolf area 250 m east and 22 of 24 were killed by natives (17 by
  CHAR_Forest_Bear_Standard, 5 by CHAR_Forest_Wolf). The scenario's "tracked units: 2" check matched "2"; it now
  reads the logged reply against 20-24, and the units are level 60. Rerun in Session 5.
- Log check: 0 unhandled, 1494 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (copy in
  %TEMP%\nyar-wv3-logs\s4-srv); [Warning lines: the known baseline kinds (Il2CppInterop, HarmonyX type loads, Beelzebub TUNE, example-empowerment) and the runs' own "stopped" lines; no [Error.

### Session 5 · 2026-10-16 · step 3 build with the fixed quiet clock · vrclient as Chaos on the local dev server

- **D17** `pursuit.vrs`, run 1 → SCENARIO FAIL 23/27 (pursuit-20261016-144928.json): the mod was right (the check line
  read quiet 0, 5, 10, 15, 20 s, then "on"), but the client-side no-log window started 10 s late behind the console
  steps. The scenario now times "on" from the server's own lines. Run 2 → **SCENARIO PASS 27/27**
  (%TEMP%\vrclient\results\pursuit-20261016-145616.json): the first empty check logged "quiet 0 s", "pursuit: on, no
  player within 25 m for 20 s" followed, group 1.2 took Chaos as its target, a unit of the run stood within 60 m of
  Chaos 60 s later, none within 60 m of home 60 s after Chaos went home, and the stop left "tracked units: 0".
- **D20** `pursuit-timing.vrs` → SCENARIO FAIL 25/26 (pursuit-timing-20261016-152624.json) on its end-of-run count
  only: the bears and wolves 250 m east killed 8 of the 24 level-60 pursuers, so 16 were left. The budget itself
  passed: `pwsh tools/preflight.ps1 -TimingSpan %TEMP%\nyar-wv3-logs\s5-srv\BepInEx\LogOutput.log -Windows 10
  -MinTracked 20` → "timing span: 10/10 windows under 5 ms, tracked >= 20, targets >= 1, 0 slow ticks". The
  scenario now records the end count and leaves the bar of 20 to -TimingSpan.
- Log check: 0 unhandled, 1061 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (copy in
  %TEMP%\nyar-wv3-logs\s5-srv); [Warning lines: the known baseline kinds (Il2CppInterop, HarmonyX type loads x3,
  Beelzebub TUNE, example-empowerment) and the runs' own "stopped" lines; no [Error. NyarDev.log: the game's own
  "Prefab … is in an unknown state" warnings (226, as in Session 4).
- Rollback boot (step 4, D21): with four drill definitions (pursuit, a wave's bosses, pools, a pool entry) in the dev
  events.json, the step 3 build logged "events: reloaded: 21 valid, 1 disabled" (example-empowerment only) and v0.12.0
  "16 valid, 6 disabled": "unknown field action.pursuit", "unknown field action.waveList.2.bosses", "unknown field
  action.pools" for both pools definitions, and the dev world's apo-admin "durationSeconds must be 30-7200" (the limit
  apocalypse raised); then the wv3 snapshot was restored ("hashes equal", VerboseLogging back to false).
