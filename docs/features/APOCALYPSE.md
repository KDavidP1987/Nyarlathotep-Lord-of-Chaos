# Apocalypse: the outbreak event

> **Status:** step 1 spikes recorded (march go — hop, pathing walk not usable, faction mixes); step 2 (Logic) merged. Plan `docs/dod/apocalypse.md` (0.13.0, the Halloween release). The
> owner's brief and the settled questions are in docs/features/OUTBREAK.md.
> Stuck hordes and roamers are handled by unit watch (docs/features/UNIT_WATCH.md, released with this in 0.13.0).

## Design summary

- **One action kind** `"type": "Apocalypse"` beside SpawnWaves and Empower, under the Spawns pillar switch.
- **Hotspots** are found once per run from where the faction's native NPCs stand (cells of 100 m), read only.
- **Roamers** are topped up at each hotspot at the start and at each horde rise. They hunt map-wide (design §9
  D49): each group goes after the eligible player nearest to it, moved by hunting-grounds' mover (hidden jumps only
  while no player is within 100 m, never landing in a claimed castle).
- **Spread:** a kill by a roamer or a horde unit queues one or two roamers where the victim fell, under the caps.
- **Hordes** rise one after another at the hotspots and march on a goal (a point or the admin's start position).
  The last horde brings the action's bosses (design §9 D51).
- **Outcomes:** lost when a horde reaches the goal, won when every horde is dead, survived when the time runs
  out. A scoreboard ranks the players.
- **Resume:** the only event that survives a restart, rebuilt from its record in state.json.

## Action contract

The keys of `"type": "Apocalypse"` (D1), each refused with its path when out of range; an Apocalypse runs 600-604800 s
(every other kind keeps 30-7200 s).

| Key | Values |
|---|---|
| `faction` | a known, not deny-listed `Faction_` name |
| `units[]{prefab,count,chance,modifiers}` | 1-10 entries, as SpawnWaves' units |
| `hotspots{count,minNatives,minSpacing}` | 1-5; 3-50 natives in one 100 m cell; 100-3000 m apart |
| `roamers{perHotspot,perKill,max,perMinute,huntRange}` | 1-10; 0-2; 1-100; 1-30; 20-60 |
| `hordes{count,firstAfterMinutes,everyMinutes,size,growth}` | 1-10; 0-1440; 5-1440; 5-50; 0-20; the last horde rises before the end |
| `goal{location,radius,name}` | `{ "type": "Point", x, z, optional y }` or `{ "type": "Admin" }` (Manual trigger only); 10-100 m; 1-32 characters |
| `marchSpeed` | 0.5-5.0 m/s |
| `bosses[]{prefab,count,modifiers}` | optional, revenge D31's form; joins the last horde |
| `scope` | Global or region names; it bounds where hotspots are found, never where roamers hunt |
| `scoreboard` | true (default) or false |

`roamers.huntRange` is the Hunt behaviour's range once the mover has brought a roamer within reach of its player and
seeded aggro (event-spawns' Hunt); the map-wide search itself has no range.

## Resume and crash loss

The record in state.json is written at once at a horde's rise or defeat, every 30 s of a march and every 60 s
otherwise. A crash therefore loses at most the last record-write interval (60 s, 30 s during a march) of scoreboard
credits, spread window and front progress, and a horde defeated inside that interval is rebuilt at the resume. A clean
restart loses nothing. The unit tally (D15) is per boot: it restarts at the resume, the units from before the restart
are accounted by the boot sweep's line, and the rebuilt units are queued into the new tally.

The record keeps the scoreboard's top 200 rows. After a resume, the scoreboard's totals (players, kills, deaths) count
only those 200 rows, so a run with more than 200 scoring players shows lower totals after a restart. This is accepted
for the Halloween release; the record gains no totals field (step 3, L3).

The step 3 dev fixture is `tools/vrclient/fixtures/apo-test.json` (put into the dev events.json). Its hordes rise every
5 minutes, not every 3 as D25 says, because D1's lower bound for `everyMinutes` is 5. Its units are Guardians and light
ghost militia, so `.devkill` spares the native Farbane skeletons at the hotspots. The Session 1 and 2 scenarios are
`apocalypse.vrs`, `apocalypse-prep-restart.vrs` and `apocalypse-boot2.vrs`. Since no hold applies within goal.radius +
60 m of the goal (D6), Chaos holds a front by standing on it, never at the goal: for every run but the lost one the
scenarios move the goal (`event set … action.goal here`) to 200 m beyond hotspot 1 on the line from hotspot 2, so Chaos at
hotspot 1 holds horde 1 at its origin and horde 2 60 m short of him; Session 2 also slows the march to 0.5 m/s, so the
fronts stay well away from the goal while nobody is connected across the restart.

## Open questions

- Answered by the step 1 spike (owner acceptance pending): the march hops (path cleared, height from the walk
  check); the game's pathing could not be made to walk a horde with a moved return point, so hordes hold while a player
  is near (D6 as written); undead, ghosts and the Farbane V Blood are allied, Legion shadows are not (docs/GAME_ASSETS.md).
- Pathing for crowded areas (design §9 D50) stays a 1.0 study: another lever (a move command or a patrol path) may
  make the game walk a horde; none was found for Halloween.

## Test results

### Spike · 2026-10-13 · step 1 (0.12.0 DLL + NyarDevTools .devhop clearPath, .devwalk) · vrclient as Chaos on the local dev server

Snapshot `tools/dev-snapshot.ps1 -Save apo0` before the first boot; logs copied to %TEMP%\nyar-apo0-logs before each
stop. Dev cfg for the session: VerboseLogging true (died lines), ManualSpawnLifetimeSeconds 1800 (boot 2; at 300 the
marched units fell due mid-march in run 1). Chaos was found dead in the restored world and was respawned at his coffin.

**March (D24)**: `tools/vrclient/scenarios/apocalypse-spike.vrs`, run 3 SCENARIO PASS 83/83
(`apocalypse-spike-20261013-195425.json`). 20 CHAR_Undead_SkeletonSoldier_Armored_Farbane hopped 10 m east every 5 s,
30 times, from the pond (-1880, -1640) to (-1580, -1640), anchored on the moving mark apo-s0, home moved and the path
cleared on each hop, Chaos 150 m north of the line's middle (no player within 100 m of a hop).

- [x] units alive: 20/20 at the end of the march (run 3; run 2 kept 19/20, one fell out of the hop radius)
- [x] units within 30 m of the moved point: 20/20 at the last read before the approach; 18/20 when Chaos arrived (a native CHAR_Forest_Bear_Standard killed 2 at the end point, "died (killer CHAR_Forest_Bear_Standard)")
- [x] height within 2 m of the walk level: 20/20 at the end (y 0.0, Chaos's [DEV WHERE] y 0.0); mid-march, over the pond's water, some units read y -50 for a hop or two and came back to the ground by the end; step 3's hop writes the walk check's height, as the plan says
- [x] units that engage Chaos on approach: 18/18 living (state Combat, target=1, within 10 s)
- [x] errors: none from the hops (the log check below)
- Run 1 without the path clear: units fell behind the moving anchor (moved 20, 18, 15, 13, 9 … 0 within the 25 m hop radius), the hunting-grounds spike's variant 2 finding; clearing the path and velocity, as hunting-grounds' mover does, kept every unit with the anchor.

march verdict: go — hop

**Pathing probe (D24, design §9 D50)**: the same scenario's second half. 10 fresh units at the pond got their return
point (SpawnTransform.Position and PreCombatPosition) moved 40 m ahead toward apo-s0 every 5 s for 120 s; run 3 also put
each unit not in combat into the Return state (GenericEnemyState.Return). Chaos stood 150 m north of the line.

- [ ] units within 30 m of the walk target after 120 s: 0/10 (runs 1 and 3)
- longest stall: 238 s in run 3 (no unit got 2 m closer at any call; "farthest" stayed 302.9 m from the first call to the last)
- errors: none
- Reading: an idle unit does not walk to a moved return point, and writing the Return state does not start a walk either; the game's own pathing was not reached with these levers.

pathing verdict: walk not usable

D6 stays as written (hordes hop, hold while a player is near); hordes do not keep walking among players. D24 waits on the
owner's acceptance of this record (rubric 3).

**Faction mixes (D37)**: `tools/vrclient/scenarios/faction-mix.vrs`, run 4 SCENARIO PASS 89/89
(`faction-mix-20261013-201008.json`; run with `--var UX=-1388 --var UZ=-1334 --var UAX=-1428 --var UAZ=-1334
--var UNATIVE=-837329073 --var MX=-1058 --var MZ=-992 --var MAX=-1098 --var MAZ=-992 --var MNATIVE=-63435588`; the sites
were found with `.devunits <guid> 6000` from home). Runs 1-3 shaped the scenario: run 1 found CHAR_Undead_ShadowSoldier
killing its twin at spawn (left out); runs 2-3 showed that the native count is not a check (natives that chased Chaos
leash home out of the 80 m read, with no died line), so it is logged only.

- [x] undead mix allied: Farbane soldier and crossbowman, Undead Guardian, ghost militia, banshee and the V Blood
  CHAR_Undead_BishopOfDeath_VBlood: no died line in 60 s, every unit counted, every [DEV UNIT] Idle with target=0 after the wait
- [x] Legion shadows hostile to the undead: "CHAR_Legion_Shadowkin_Lesser of manual died (killer CHAR_Undead_GhostBanshee)"
  and "(killer CHAR_Undead_SkeletonSoldier_Armored_Farbane)" within the 60 s window (runs 1, 3 and 4 alike)
- [x] spider mix allied (Forest, Melee, Range, Forestling, Queen V Blood)
- [x] bandit mix allied (Thug, Hunter, Deadeye)
- [x] militia mix allied (Crossbow, Light)
- The table: docs/GAME_ASSETS.md › Faction compatibility. D37 waits on the owner's acceptance of this record (rubric 3).

Session close: both logs copied to %TEMP%\nyar-apo0-logs before each stop; log check lines in docs/audits/apocalypse.md;
`tools/dev-snapshot.ps1 -Save apo0` before the first boot, snapshot restored; hashes equal (apo0, both boots).


### Session 1 · 2026-10-13/14 · step 3 DLL (the mod at 847bcc5 for runs A-F) · vrclient as Chaos on the local dev server

Snapshot `tools/dev-snapshot.ps1 -Save apo1` before the first boot; both logs copied to %TEMP%\nyar-apo1-logs before every
stop (pre-, boot1-…boot4-, run6-, run7-, s2prep-, s2boot2-). Dev cfg: [Scoreboard] IncludeAdmins true, TimingLog true;
apo-test from tools/vrclient/fixtures/apo-test.json, every other spawning definition disabled.

**Runs A-F (D25, D26, D27)**: `tools/vrclient/scenarios/apocalypse.vrs` run 8 SCENARIO FAIL 105/106
(`apocalypse-20261014-005028.json`; `--var UX=-1388 --var UZ=-1334 --var UNATIVE=-837329073 --var WX=-1690 --var WZ=-1400`).
The one miss is the chat OCR of the won line; the won-chat check below shows the line was sent.

- [x] outbreak (D25): "hotspots: 2 (36 at -1360 -1356, 23 at -1951 -1830)", "roamers: +3 at hotspot 1", "roam: hotspot 1 on a
  player" with Chaos 333 m from the nearer hotspot; 77 s later 4 Guardians and 1 ghost militia stood within 100 m of him
- [x] spread (D25): a roamer killed Chaos ("[DEV DIE] found=1"), "spread: +2 (8/12, 2/4 this minute)", 5 of our units within
  6 m of the death point
- [x] lost (D26): Chaos at the wait spot; each 25 % step's keep-up line read 8/8, 8/8 and 8/8 within 30 m of the front;
  "event apo-test ended: lost (1 of 2 hordes risen)", chat "the test camp has fallen", tracked 0 after the grace
- [x] won (D26): horde 1 held at its origin by Chaos (15 % then 15 %), killed, "horde 1 of 2 destroyed" in log and chat;
  horde 2 marched from hotspot 2 to 60 m short of Chaos (the front 46 m from him, 73 % then 73 %: held), killed;
  "event apo-test ended: won (2 of 2 hordes risen)", "scoreboard: 1 players, 33 kills, 1 deaths", tracked 0
- [x] won announcement: `tools/vrclient/scenarios/apocalypse-won-chat.vrs` (one horde) with Debug.VerboseLogging on logged
  "announced to 1: Apocalypse test: the living have won" after "horde 1 of 1 destroyed"; the chat OCR missed it in both
  runs because Beelzebub posts one system line per kill and the game's chat history shows about 8 lines
- [x] survived (D26): Chaos at hotspot 1 for the 30 min held horde 1 (0 %, 0 %) and horde 2 (72 %, 72 %, its front 59 m from
  him); 15 min with no win or loss, then "ended: survived (2 of 2 hordes risen)", chat "the living held out", tracked 0
- [x] stop at horde 2 (D27): "stopped: 30 units queued", "scoreboard: 0 players, 0 kills, 0 deaths", tracked 0
- [x] pillar spawns off at horde 2 (D27): "event apo-test ended (pillar off)", no scoreboard line, tracked 0
- [x] purge at horde 2 (D27): "purge: 1 events ended, 28 units queued", no scoreboard line, tracked 0; natives at the undead
  site 5 before run A and 5 after run F
- D25, D26 and D27 wait on the owner's acceptance of this record (rubric 3).

Findings fixed during the session (each with a test and a planted fault in docs/audits/apocalypse-step3-planted.txt):
the native read skipped the natives the game disables far from players ("no hotspot"); the keep-up line counted hopped
units where they stood; a hop gave up after one unwalkable point; a front held for good where the ground climbed two levels;
units the game leaves standing still fell 25-40 m behind the front (keep-up 4-6 of 8: stragglers now hop past 15 m to a
point 7.5 m ahead of the front); a straight line crossed water and cliffs and horde 2 held for good 70 m out (A12: an
unobserved front crosses up to 120 m, seen in game as "horde 2 crossed 18 m of unwalkable ground"). vrclient gained
`expect-seen` (a line read by an earlier check), `respawn-coffin`, the System-menu close in `launch`, the OCR text on an
`expect-chat` miss, and `.devdie` with a guid list.

### Session 2 · 2026-10-14 · step 3 DLL (the mod at 5bda54f) · vrclient as Chaos on the local dev server

Same snapshot (apo1) and log folder (s2b-prep-, s2b-boot2-, s2-boot3-, s2-timing2-, s2-timing3-). The first attempt ran
on the DLL before 5bda54f: the resume rebuilt 21, then topped up 1 roamer and spread 2 (24 tracked), which found the
top-up defect (a resume with no missed horde must not top up, D12); fixed with a test and a planted fault, then rerun.

**Resume (D28)**: `tools/vrclient/scenarios/apocalypse-prep-restart.vrs` SCENARIO PASS 34/34 (horde 2 marching, Chaos
holding its front, 16 kills credited, 17 tracked; the scenario ends with `console TPHome`), logs copied, the server stopped
after an autosave and started again; `apocalypse-boot2.vrs --var UNITS=17` SCENARIO PASS 23/23.

- [x] boot: "boot marker sweep: 17 found, 17 queued for despawn (17 listed in state.json)", then "event apo-test resumed
  after restart (horde 2/2, 17 units to rebuild)"; `.nyar status` read "tracked units: 17" within the first minute
- [x] the front went on from its saved progress: the last line before the stop read "horde 2 front 15 % units 11/11", the
  first after the resume "front 16 %", then 17 %, 19 % … 28 %, each "units 11/11 within 30 m of the front"
- [x] scoreboard across the restart: "event apo-test scoreboard: 1 players, 17 kills, 0 deaths" (16 before the restart, 1
  after), tracked 0 after the end
- [x] a restart after `.nyar purge confirm`: "boot marker sweep: 0 found", no resume line, state.json has no `Apocalypse`,
  and the timing scenario's first read was "tracked units: 0"
- D28 waits on the owner's acceptance of this record (rubric 3).

**Tick budget (D29)**: `tools/vrclient/scenarios/apocalypse-timing.vrs --var WX=-1690 --var WZ=-1400 --var RX=-1880
--var RZ=-1640` run 3 SCENARIO PASS 22/22 (`apocalypse-timing-20261014-030415.json`), apo-test from
tools/vrclient/fixtures/apo-test-timing.json (3 hotspots × 10 roamers, max 30, one 24-unit horde marching unobserved),
[Limits] MaxUnitsPerWave 24, Debug.TimingLog on, 54 tracked. Chaos waited 331 m from the nearest hotspot ("roam: hotspot
3 on a player"), and inside the span stepped into his castle for 12 s just after a window's sample and came back 306 m
away, so all three hotspots' roamers jumped towards him again ("roam: hotspot 1/2/3 on a player" at lines 48-50 of the
334-line span). The log copy cut at the run's start (s2-timing3-run.log) gives
`pwsh tools/preflight.ps1 -TimingSpan <copy> -Windows 10 -MinTracked 50` → "timing span: 10/10 windows under 5 ms,
tracked >= 50, targets >= 1, 0 slow ticks"; the worst window averaged 0.645 ms (max tick 4.1 ms). Runs 1-2 failed on
the scenario, not the mod: a regex read the `event info` list's `\n` as a newline, the span began at the first 10-minute
health line with 50 tracked so 15 min left only 6 windows, and a castle hop over a window's sample read 0 targets.

### Session 3 · 2026-10-14 · step 3 DLL (the mod at 2b4d93e) · no player near the hordes

**A run of hours (D30)**: apo-soak from `tools/vrclient/fixtures/apo-soak.json` (zombie-apocalypse with a Point goal 6 km
off the map at (6000, -1334), marchSpeed 0.5, 14400 s), [Limits] MaxUnitsPerWave 20 and MaxTrackedUnits 150,
Debug.TimingLog on, VerboseLogging off. `apocalypse-soak-start.vrs` SCENARIO PASS 5/5 started it at 07:07:54 UTC; the
client was then closed and the server ran alone for 3 h 2 min. Log copies: soak-LogOutput.log, soak-NyarDev.log; the
record's write times sampled every 20 s by `tools/vrclient/soak-sampler.ps1` (soak-state-writes.txt).

- [x] hotspots 3; the four hordes rose 7, 11, 15 and 20 units, each three 10-minute health lines
  after the one before, so on the 30-minute schedule. The log has no timestamps, so "within 60 s" is read from that
  spacing, not measured to the second
- [x] lifetime renewals: eight "renewed <n> lifetimes" lines, alternating 12 (the roamers) and each horde's count, the
  last "renewed 53 lifetimes", which with the 12 roamers covers all 65 tracked
- [x] the record: 322 distinct writes from 07:07:56 to 10:09:36, the longest gap 60.6 s (38 gaps over 60 s by under
  1 s, the write riding on the 60 s state flush)
- [x] tracked: the health line rose 12, 19, 30, 45, 65 and stayed at 65 (cap 150); "degraded: none" throughout
- [x] `pwsh tools/preflight.ps1 -LogCheck -ServerPath <dev server>` → "0 unhandled, 3467 nyar lines, 0 orphan errors,
  0 unity errors, regions 10 polygons"; no [Error] in either log
- [ ] warnings of ours: example-empowerment's known pillar warning, and one "slow tick: 2298 ms (apocalypse 2294 ms,
  state flush 4 ms; outside phases 0 ms)" about 13 min in, between horde 1's rise and the next health line; none in the
  2 h 50 min after. The cause is not proven (the Unity log shows the client's disconnect and the server reconnecting to Steam near then). The DLL from
  d88dbbc on logs the step's parts ("slow step: …") when a step takes 250 ms or more, so a repeat names its part
- D30 waits on the owner's acceptance of this record (rubric 3), the slow tick included.

### Session 4 · 2026-10-16 · the 0.13.0 work after the G-Portal test (local build, uncommitted on f01f18f) · vrclient as Chaos on the local dev server

Dev cfg for the session: VerboseLogging true (restored to false afterwards). Logs copied to %TEMP%\nyar-logs-20261016-*.

- **Admin goal (fix):** `tools/vrclient/scenarios/wave-templates.vrs` run 2 SCENARIO PASS 54/54
  (`wave-templates-20261016-012328.json`). `.nyar template use zombie-apocalypse as apo-admin`, enable, start at the pond:
  "event apo-admin started by manual", no `admin_location` refusal (G-Portal, 2026-10-15: `code=badarg arg=location
  reason=admin_location`); stopped: "12 units queued, 0 spawns cancelled".
- **D38 as amended by D60 (A14):** `tools/vrclient/scenarios/faction-mix-waves.vrs` run 1 131/132
  (`faction-mix-waves-20261016-004501.json`): militia, church, legion and gloomrot passed whole, no unit killed by its
  own group and every count held after 60 s. The one failure was one `CHAR_Cursed_Mosquito` that the game removed about
  10 s after its spawn, with no death; the mosquito was dropped from cursed-night and from the group, and the cursed pass
  alone (`faction-mix-waves-cursed.vrs`) then passed 26/26 (`faction-mix-waves-cursed-20261016-005919.json`). Record:
  docs/GAME_ASSETS.md › Faction compatibility.
- **realm-gauntlet (D60):** the same wave-templates run: waves 1-5 (bandits 8, undead 8, militia 9, gloomrot 9, church
  8 plus `CHAR_ChurchOfLight_Overseer_VBlood`) each queued only after "wave <n> cleared", never two factions alive
  together, ending "all waves defeated (5 of 5 waves)". Run 1 failed only its own expectation (the last wave's queued
  count includes its V Blood: 9, not 8).
- Logs: no Nyarlathotep error; the warnings were the stop lines and the dev events.json's own example-empowerment line.

### Test weekend · about 2026-10-24/25 · the owner with the players (D31)

**Sheet.** Install the Release build of the step-3 commit (`Nyarlathotep.dll` from `Nyarlathotep/Nyarlathotep/bin/Release/net6.0/`)
on the owner's server with the server stopped, then start it. Spawns pillar on (`.nyar pillar spawns on`), default caps.

1. As an admin, at the spot the players will defend (outside any castle): `.nyar template use zombie-apocalypse`, then
   `.nyar event enable zombie-apocalypse`, then `.nyar event start zombie-apocalypse`. The goal is where you stand when
   you start it. PASS: chat shows "The dead rise: Zombie apocalypse." and `.nyar event info zombie-apocalypse` lists
   3 hotspots with their regions.
2. Watch the roamers (4 per hotspot at first). They head for the nearest player outside a castle; each kill one of
   ours makes (a player or a native NPC) adds a roamer where the victim fell (up to 30, at most 6 a minute). PASS: players far from the goal meet roamers within a few minutes, and none jump into a castle.
3. Horde 1 rises after 10 min, then one every 30 min (4 hordes, 7 units growing by 4; the last brings the Bishop of
   Death). Each marches to the goal at 2 m/s and stops while a player stands within 60 m of its front. PASS: chat
   announces each rise, each 25 % of the march and each horde destroyed.
4. Defend the goal. Won = every horde destroyed; lost = a front reaches the goal; survived = the 4 hours end first. PASS:
   the outcome line and the scoreboard (kills, deaths) show in chat.
5. To stop early: `.nyar event stop zombie-apocalypse` (scoreboard shown); emergency: `.nyar purge confirm` (everything
   of ours despawns, nothing shown). PASS: `.nyar status` reads "tracked units: 0" within a minute.

Note for the verdict: how the march, the announcements, the spread and the scoreboard look and feel to the players; any
change asked for becomes an amendment before it is built.

Verdict: waiting on the owner.
