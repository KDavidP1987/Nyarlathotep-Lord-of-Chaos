# Changelog — Nyarlathotep, Lord of Chaos (full / GitHub)

The complete technical history. The concise, player-facing changelog that ships to Thunderstore lives at
`Nyarlathotep/Nyarlathotep/CHANGELOG.md`. Public beta from 0.2.0; features stay experimental until validated on live servers.

## [0.15.0] - 2026-10-24

Horde pools: the `horde-pools` child of the DoD Epic (`docs/dod/horde-pools.md`; feature doc `docs/features/HORDE_POOLS.md`;
audit `docs/audits/horde-pools.md`; Raphael handoff `docs/RAPHAEL_HANDOFF_HORDE_POOLS.md`). Released without the
in-game scenario run (D12): the owner skipped it on 2026-10-24; the dev server's boot was checked for errors.

- Full-size hordes: a HordeMarch, Infestation or Apocalypse horde is held only by the free `[Limits] MaxTrackedUnits`
  slots, never by `MaxUnitsPerWave`, which still holds SpawnWaves waves (destination waves included). A horde's cut
  names `MaxTrackedUnits`. `MaxTrackedUnits` defaults to 500 (range 1-500 unchanged); a cfg that already holds a value
  keeps it. The boot log prints the loaded limits (`[nyar] limits: …`).
- The castle switch: `allowTerritory` (bool, default false) on the three horde types. Off: no hunter or roamer targets
  a player on claimed territory and the hunt layer seeds no aggro on one; no jump, hop, drift, march step, spread or
  growth spawn lands on a claimed block; a march stops at its last unclaimed point and counts as arrived; a claimed
  start is refused ("start is inside a castle's territory; set action.allowTerritory true to allow it"); claimed
  hotspots and origins are skipped and counted in the log. On: castles are open. Chat `.nyar event set <id>
  action.allowTerritory true|false`; `event info` adds "claimed territory allowed".
- The pool library `BepInEx/config/Nyarlathotep/pools.json` (SchemaVersion 1, 1-64 pools under the event pool rules):
  seeded once with 17 pools, `<group>-units` and `<group>-bosses` for the nine allied groups (no `bandits-bosses`),
  members unknown on the server left out with a warning; never overwritten. Any pool entry, boss roll, spread or growth
  pool of an event may name a library pool; the event's own pool of the same name wins; a running event keeps what it
  resolved. An invalid file loads no pool, logs `pools.json not loaded: <reason>` and opens the `pools unknown` health
  entry; a pool spanning groups logs `pool <name> mixes groups: …`.
- Random counts: a unit pool entry on SpawnWaves (units and waveList) and HordeMarch takes `min` and `max` (1-500)
  instead of `count`; each draw rolls min..max. Refused on Apocalypse and Infestation, where `count` is a weight. A
  horde's maxes sum to at most 500 and its mins to at least 10; the wave-size check reads the max. Chat
  `pool:<name>:<min>-<max>`; `event info` shows `pool <name> ×<min>-<max>`.
- `.nyar pool list [page] | info <pool> [page] | reload` (admin): the library, a pool's members and the events that
  name it, and a reload that names every event invalid now (or says events.json was not reloaded).
- Raphael: no wire change (api 8); the contract states the new keys; an editor must keep keys it does not know.

## [0.14.0] - 2026-10-18

Undead hordes: the `undead-hordes` child of the DoD Epic (`docs/dod/undead-hordes.md`; feature doc
`docs/features/UNDEAD_HORDES.md`; audit `docs/audits/undead-hordes.md`; Raphael handoff
`docs/RAPHAEL_HANDOFF_UNDEAD_HORDES.md`).

- New event type `Infestation`: the dead rise at `origins.count` graveyards (places with at least `minNatives` of the
  faction's own NPCs, `minSpacing` apart), `perOrigin` each. `hunters` % of them hunt the nearest player anywhere on the
  map; the rest drift within `driftRadius` of their graveyard. `spread` raises `perKillMin`-`perKillMax` more for each
  kill they make, up to `max` and `perMinute`.
- New event type `HordeMarch`: one horde of up to 500 (units and bosses) rises at a Point, the admin's spot or a
  graveyard at least `minDistance` from the goal, and marches to `destination` at `marchSpeed`, growing by `growth`.
  On arrival, after 2 minutes stalled, or once players have watched it for 30 s in a row, it becomes an infestation
  where it stands (`infest`). Chat: `The horde has reached <destination>!`; an infestation opens with `The dead rise in
  <n> places: <event>.`
- Both run on the apocalypse engine: one apocalypse-type event at a time, resumed after a restart, scored and ended as
  before.
- Waves that march: any spawn event can set `destination` (a Point or Admin, `radius` 10-100, `name`) and `marchSpeed`
  (0.5-5.0). A wave marches as one body and takes its behaviour on arrival. A player within 60 m holds the front; a
  watched front waits for any unit left more than 30 m behind; a wave watched for 30 s is met and fights where it stands.
  Log lines: `march <id> wave <k>: arrived after <s> s, <n> units`, `met by a player after …`, `held by a player`,
  `marching on`, `stalled <d> m short of its destination`.
- Spread from a pool: roamers, infestations and growth take `perKillMin`/`perKillMax` (0-3) and `pool`; the old
  `perKill` plans as before.
- A start or wave cut by a cap names it: `capped: <n> of <m> by <key> <value>; raise it in the cfg`. An infestation's
  start forecasts only its rise against the free `MaxTrackedUnits` slots.
- `MaxUnitsPerWave` ceiling raised to 500, like `MaxTrackedUnits`. Work per tick is bounded at 500 units, taken in
  turn. Roamer `max` and horde `size` go to 500, horde `growth` to 100.
- Templates `undead-infestation` and `undead-horde-march` (off); `zombie-apocalypse` retuned (5 hotspots, larger
  hordes from minute 3).
- Diagnostics: an infestation logs one `infest moves:` count line a minute; stall lines name the distance left, never
  a point.
- Known limits: hunters jump in straight lines and do not path, so a player behind a river or a cliff band is reached
  only by those with a clear line (A7). The last jump lands 50-60 m away; the game does not walk an idle unit closer
  (design §9 D50). The game stops its own NPCs 80 m or more from every player (the mod keeps its units running), so
  kills, spread and growth happen only near players (A8). A castle territory stays a refuge.
- Rollback to 0.13.1 disables the events that use the new kinds or keys (`tools/rollback-gate.ps1`).
- Wire unchanged (api 8).
- Load ladder on the local server: 100 and 250 units pass every bar; 500 fails the frame bar with a player among them
  (the game's own cost; the mod's tick stays near 1 ms). `MaxTrackedUnits` keeps its ceiling of 500 (owner's call).
- In game (vrclient): wave-destination 42/42, infestation 24/24, horde march at 500 39/39, and the ceiling run: 500
  units held 16 minutes on each side of a restart, rebuilt 480 of 480, purged in 25 s, tick under 2.3 ms average.

## [0.13.1] - 2026-10-17

The login notice: the `login-notice` child of the DoD Epic (`docs/dod/login-notice.md`; design §9 D63, Epic A45, A47;
feature doc `docs/features/LOGIN_NOTICE.md`; audit `docs/audits/login-notice.md`). A player who connects while an event
runs gets one private line 10 s later, for example `Events in progress: Bandit raid: 5 min left; Siege: 20 min left. Type
.nyar status for details.`

- New cfg switch `[Announcements] LoginEventNotice`, **on by default**: the one announcement that is (D63), since it
  only tells a player what an admin already started. Enabled events that are not running are never named.
- The running list is read when the line leaves, not at connect, so nothing is sent if the event ended in those 10 s,
  if the player left, or if another player now holds the user entity. Each part is the event's `.nyar status` line,
  soonest end first; a line over 480 bytes keeps whole parts and ends `… and N more`.
- One notice per player per 60 s of reconnects, with its own gate, separate from the admin degraded notice's; a
  connect that sends nothing, or a send that fails, does not use the gate up. A quick drop and rejoin replaces the
  queued notice, so it arrives 10 s into the new session; turning the switch off drops the queued ones.
- A throwing read or send is logged once per streak (`login notice failed:` / `private message failed:`) and never
  stops the tick or the other notices. Log line per notice: `[nyar] login notice sent: <n> events`.
- Wire unchanged (api 8); `[NYAR:version]` keeps its keys (`docs/RAPHAEL_INTEGRATION_CONTRACT.md`).
- Preflight `AnnouncementDefaults` now requires exactly six `[Announcements]` switches, all off but LoginEventNotice
  (Epic D41 as amended by A47). vrclient: join waits out a slow server list and retries swallowed menu clicks; leave
  confirms LEAVE GAME.
- In game: `tools/vrclient/scenarios/login-notice.vrs` SCENARIO PASS (a notice at login while an event runs, none
  after it stops).

## [0.13.0] - 2026-10-16

The Halloween release: three children of the DoD Epic. `apocalypse` (`docs/dod/apocalypse.md`) adds a third action kind,
a map-wide outbreak that marches on a goal and resumes after a restart; `unit-watch` (`docs/dod/unit-watch.md`) recovers
or removes stuck event units so no wave hangs; `wave-variety` (`docs/dod/wave-variety.md`) adds pursuit, unit pools and
boss rolls and fourteen templates. With them: six faction wave templates (Epic A43) and the fresh-world start-up fix
(Epic A42). Design and sessions: `docs/features/APOCALYPSE.md`, `docs/features/UNIT_WATCH.md`,
`docs/features/WAVE_VARIETY.md`, `docs/features/EVENT_LIBRARY.md`; audits: `docs/audits/apocalypse.md`,
`docs/audits/unit-watch.md`, `docs/audits/wave-variety.md`; design §9 D37, D44, D45, D49-D51, D54, D56, D60, D61. Wire
api 8. state.json gains an optional `Apocalypse` record; the cfg gains `[UnitWatch]`. Test builds v0.13.0-rc.1 to rc.3
came first.

<details open>
<summary><b>Definition keys</b></summary>

A key that fails validation disables the event, with one reason that names the field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `action.type` `"Apocalypse"` | `faction`, `units` (1-10, as SpawnWaves'; `count` 1-50 is the weight), `hotspots { count 1-5, minNatives 3-50, minSpacing 100-3000 }`, `roamers { perHotspot 1-10, perKill 0-2, max 1-100, perMinute 1-30, huntRange 20-60 }`, `hordes { count 1-10, firstAfterMinutes 0-1440, everyMinutes 5-1440, size 5-50, growth 0-20 }`, `goal { location Point (x, z, optional y) or Admin, radius 10-100, name 1-32 }`, `marchSpeed` 0.5-5.0, optional `bosses` (revenge's form), `scope`, `scoreboard` (absent = true) | The outbreak (Runtime below). Spawns pillar. `durationSeconds` is 600-604800 for this kind (every other kind keeps 30-7200); an Admin goal needs a Manual trigger; a last horde rising at or after the end is refused "action.hordes: last horde rises at or after the duration" |
| `action.pursuit` | `{ "afterSeconds": 15-600 }`, needs Hunt; refused beside `fanOut` or `regionLock: true` | Once no eligible player has stood within the arena (or Hunt range) of the centre for afterSeconds, the run pursues until it ends |
| `action.pools` | 1-8 named lists (`a-z`, `0-9`, `-`, 1-24 characters) of 1-30 distinct prefabs | Named unit lists for the two entries below |
| unit entry `{ "pool", "count" }` | `count` 1-50, in `units` or a wave's `units` | Draws `count` units from the pool, with repeats, when the wave is due |
| `action.waveList[n].bosses` | 1-3 entries, `{ "prefab", "count" 1-3, "modifiers" }` or `{ "pool", "min", "max" }` (1-5 distinct), at most 5 bosses a wave | Any wave may bring bosses, fixed or rolled; the action-level `bosses` stays the last wave's and also takes a roll. A roll on a pool shared with too few distinct members for its `min` is refused |
</details>

<details open>
<summary><b>Runtime: apocalypse</b></summary>

- **Hotspots** (`Logic/Apocalypse.cs` Hotspots.Pick, `Services/ApocalypseAction.cs`). At the start the faction's eligible
  natives inside `scope` are grouped into 100 m cells; up to `count` cells with at least minNatives, minSpacing apart,
  become hotspots. The read never writes to a native. No hotspot refuses the start "no hotspot: fewer than <n> <faction>
  natives in one place in scope".
- **Roamers and spread** (design §9 D49). Each hotspot is topped up to perHotspot at the start and at each horde rise,
  under `roamers.max`. Roamers hunt map-wide: each hotspot's roamers go after the eligible player nearest them, moved by
  hunting-grounds' hidden jumps, never into claimed territory. A kill by a roamer or horde unit (player or native)
  queues perKill roamers within 3 m of the victim, at most perMinute a minute.
- **Hordes and the march.** Horde k rises at start + firstAfterMinutes + (k−1)·everyMinutes, at hotspot ((k−1) mod
  hotspots) + 1, size + (k−1)·growth units clamped by MaxUnitsPerWave and the free MaxTrackedUnits slots; a horde that
  queues nothing is retried every 60 s. The last horde brings `bosses` (cut after its regular units). The front hops
  along the line to the goal at marchSpeed, holds while a player is within 60 m (except within goal.radius + 60 m of
  the goal) and crosses up to 120 m of unwalkable ground while unobserved. The walk spike found the game's pathing not
  usable for this (design §9 D50 stays a 1.0 study). Lines: "<event>: a horde of <n> rises in <region> and marches on
  <goal>", each 25 % of the way, "horde <k> of <c> destroyed".
- **Outcomes.** Lost when a front is within goal.radius with a unit within goal.radius + 30 m ("<goal> has fallen");
  won when every horde has risen and none lives ("the living have won"); survived at the end ("the living held out");
  lost beats won on one tick. Each logs "event <id> ended: <won|lost|survived> (<r> of <c> hordes risen)", then the
  scoreboard (wave-sets' rules, kills of roamers and horde units). `event stop` shows the scoreboard without an outcome;
  purge, `pillar spawns off` and a fault show neither. Every end path removes the resume record.
- **Resume after a restart** (design §9 D37, D44, D45). The record (event id, times, the resolved goal, hotspots, hordes
  risen, living fronts, counts and living bosses, roamers per hotspot, the spread window, the scoreboard rows by
  platform id) is written at a rise or defeat, every 30 s of a march and every 60 s otherwise. After the boot sweep has
  drained (or 5 min, with a warning), "event <id> resumed after restart (horde <r>/<c>, <u> units to rebuild)"; hordes
  missed while down rise once each, in order. A definition gone, disabled, changed or ended while down is not resumed.
  The record keeps the top 200 scoreboard rows.
- **Lifetime and caps.** Units carry at most 2 h of LifeTime, renewed while the run lives, so a removed mod leaves none
  past about 2 h. One apocalypse at a time, counted as a concurrent event. The tally line "apocalypse <id> units: <q>
  queued = …" at each rise and the end.
- **Status.** `.nyar status`: "<event>: horde <r>/<c>, <p> % to <goal>, <n> roamers, ends in <t>". `event info` adds the
  hotspots, fronts and tally for admins.
- **Fixed since rc.1: the Admin goal** (`Logic/AdminFlows.cs`). A started `zombie-apocalypse` takes the admin's position
  as its goal; rc.1 refused it `code=badarg arg=location reason=admin_location` (G-Portal test, 2026-10-15).
</details>

<details open>
<summary><b>Runtime: unit watch</b></summary>

- **Stuck** (every 5 s, every unit SpawnTracker holds for a running event; `.nyar spawn` units are never watched):
  standing, no more than 5 m moved and no fight for StuckSeconds while holding a player as its aggro target (design §9
  D56, plan A2); off the ground, more than 10 m below the lowest grounded level or with none (the spike's fallback);
  still, not 0.5 m moved within SpawnStillSeconds of a spawn or landing, with a player target. A fight (the
  StatChangeSystem damage read, or a player target within 5 m) clears it.
- **Hidden recovery.** Two tries: a stuck unit is moved only while no player is within 100 m, to walkable ground 50 m
  or more from every player, outside claimed territory and inside the scope or arena, beside its group, else 50-60 m
  from the nearest eligible player, else its anchor.
- **Removal with no kill credit.** After the second failed try the unit leaves through the staged despawn (waiting up to
  StuckSeconds for an unseen moment, then even in view). It counts as gone for its wave, raises no scoreboard row,
  V Blood trigger, kill count, revenge heat or spread, and is never rebuilt by a resume.
- **Backstop.** A wave the event waits on with no timer (a waveList wave before a clear-only wave, a waveList's last
  wave, an apocalypse's hordes after the last rise) that sees no death and no fight for StallMinutes marks its leftovers
  stuck.
- **Chat.** `.nyar event unstick <id>` marks every live unit of the running instance that has not fought lately:
  "unstick <id>: <n> units marked, <f> fighting left alone"; refused "unit watch is off: [UnitWatch] Enabled". `event
  info` shows "unit watch: <s> stuck now, <r> recovered, <x> removed".
- **Lines.** "[nyar] unit-watch <event> stuck|recovered|try failed|removed|cleared|stall: …", prefab and our units'
  distances only; read and move failures once per streak with a health entry. Purge, pillar off and every end path drop
  the rows; a restart drops them (memory only).
</details>

<details open>
<summary><b>Runtime: wave variety</b></summary>

- **Pools and rolls** (`Logic/Pools.cs` Pools.Resolve). Each wave is rolled once, when due: pool entries draw with
  repeats, boss rolls draw min..max distinct bosses. Bosses spawn first and the caps cut units before bosses. Logged
  "event <id> wave <n>/<N> rolled: <prefab>x<k>, …; bosses: …".
- **Pursuit** (`Logic/Pursuit.cs`, the region phase, every 5 s). The quiet clock starts at the first check that sees no
  eligible player within the arena (or Hunt range); after afterSeconds "event <id> pursuit: on, no player within <r> m
  for <s> s", on until the run ends. Groups are seeded on the nearest eligible player map-wide and moved by the
  hidden jumps, 20 per tick shared across events. Claimed territory and PvP combat stay a refuge. A failing check logs
  "pursuit <id>: check failed: …" with a health entry.
- **Allied groups** (`Logic/FactionGroups.cs`, docs/GAME_ASSETS.md › Faction compatibility). Grown by the wide mix runs
  to nine groups: undead, spiders, bandits, militia, church, legion, gloomrot, cursed, blackfangs. Every shipped
  template keeps its units, pools and bosses in one group, or, as realm-gauntlet, one group per wave with each wave
  waiting for the last to be cleared (design §9 D60).
</details>

<details open>
<summary><b>Other runtime changes</b></summary>

- **Fixed: a brand-new world** (Epic A42, `docs/features/FOUNDATION.md`). Init waited for nothing, so a fresh world's
  first boot logged "degraded: hook DayNight, regions: unavailable" and day/night events never fired. It now logs
  "world data not loaded yet (…); retrying each second for up to 120 s", then "world data ready after <n> s"; past
  120 s it initialises without them, with a warning, as before.
- **Early departures** (`Logic/EarlyDepartureLog.cs`). Whatever VerboseLogging says, an event unit that leaves within
  60 s of its spawn logs "early departure: <prefab> of <id> wave <n> after <s> s: <how>" (died with the killer's prefab,
  or removed by the game), at most 20 a minute.
</details>

<details open>
<summary><b>Chat</b></summary>

- **Apocalypse** (D16): `.nyar event set <id> action.goal here` (x, y and z), `action.goal.radius`, `action.goal.name`,
  `action.hordes.count|everyMinutes|size`, `action.roamers.max`, `action.marchSpeed`, `action.bosses` and
  `durationSeconds` within the kind's bounds; every other key in events.json and `.nyar event reload`.
- **Wave variety** (D10): `action.pursuit <15-600|none>`; `action.pools.<name> <list|+prefab|-prefab|none>` (refused
  while an entry names the pool); `pool:<name>:<count>` tokens in `action.units` and `action.waveList.<n>.units`;
  `action.bosses` and `action.waveList.<n>.bosses` take `<prefab>:<count>` or `pool:<name>:<min>-<max>` or `none`.
  `event info` shows pools, entries, rolls and pursuit.
- **Unit watch:** `.nyar event unstick <id>` (admin).
</details>

<details open>
<summary><b>Templates</b></summary>

Thirty-four in all (thirteen at 0.12.0), every one disabled; the twenty-one new ones start by command:

- `zombie-apocalypse`: Faction_Undead, levels 22-38 (a Guardian leader with doubled health), 3 hotspots, 4 roamers each
  (max 30, 6 a minute), 4 hordes from 10 min every 30 min of 7 growing by 4, the last with the Bishop of Death, an Admin
  goal of radius 30 named "the living", 2 m/s, 4 h. Its worst case, 83 tracked, fits the default caps.
- Faction waves (Epic A43): `militia-muster`, `silverlight-crusade`, `legion-incursion`, `gloomrot-assault`,
  `cursed-night` (4-5 growing waves 150 s apart, legion 180 s, or on a clear, ending on a V Blood) and `realm-gauntlet`
  (one faction per wave, each on a clear, ending on the Overseer). A 40 m arena at the admin, Hunt, the scoreboard and
  the K/D winner.
- Timed (`<base>-timed`, seven): spider-swarm, graveyard-waves and the five faction waves on a fixed 75 s beat, pursuit
  after 60 s.
- Onslaught (`<faction>-onslaught`, seven: spider, undead, militia, silverlight, legion, gloomrot, cursed): pools
  `troops`, `elite` and `lords`, waves about 1.5 times the base's (at most 20), one rolled lord mid-run and one or two at
  the end, 75 s beat, pursuit after 60 s. No timed or onslaught run passes the default caps with every wave alive.
</details>

<details open>
<summary><b>Wire</b></summary>

api 8 (unit-watch): the twin `.nyar api event unstick <id>` → `[NYAR:ok] cmd=event verb=unstick id=<id> marked=<n>
fighting=<f>`, refusals `state`, `badarg` and `invalid reason=watch_off`. No tag, key or push kind. An apocalypse's
status row is `kind=waves` with `wave=<hordes risen>/<hordes>`, every outcome the usual `event-end`; hotspots, roamers,
fronts and the goal never reach the wire. Wave variety adds nothing. Raphael: `docs/RAPHAEL_HANDOFF_API8.md`,
`docs/RAPHAEL_HANDOFF_WAVE_VARIETY.md`.
</details>

<details open>
<summary><b>Config</b></summary>

`[UnitWatch]`: `Enabled` (true), `StuckSeconds` (90, 30-600), `StallMinutes` (10, 0-60, 0 = backstop off),
`SpawnStillSeconds` (10, 5-60). Recovery tries are fixed at 2. No [Limits] key changed: a big horde needs
`MaxUnitsPerWave` (a horde never passes it; default 20, up to 50) and `MaxTrackedUnits` raised.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **Rollback drill.** 0.12.0 disables an Apocalypse lasting over 7200 s with "durationSeconds must be 30-7200" and a
  shorter one with "unknown action type Apocalypse", and the wave-variety keys with "unknown field action.pursuit",
  "action.pools" (a pool entry, too) and "action.waveList.<n>.bosses"; it ignores state.json's `Apocalypse` and the
  `[UnitWatch]` entries. Fixtures pair-apocalypse, pair-apocalypse-short, pair-pursuit, pair-pools, pair-poolentry and
  pair-wavebosses pass, pair-otheraction and pair-otherwavekey fail; selftest 37/37.
- **Dependency suites.** `apocalypse` 5/5 (native-read, spread-read, march-read, resume-read, state-write),
  `unit-watch` 2/2 (watch-read, watch-move), `wave-variety` 1/1 (pursuit-read).
- **vrclient.** Scenarios apocalypse, apocalypse-prep-restart/-boot2, apocalypse-timing, faction-mix(-waves, -wide),
  unit-watch (with -apocalypse, -ends, -perch, -remove, -stall, -timing), pursuit, pursuit-timing, wave-variety,
  wave-templates.
- **Tests.** 3830 (3134 at 0.12.0), with a control row for every D-item (ControlCases).
</details>

## [0.12.0] - 2026-10-13

The `revenge` child of the DoD Epic (`docs/dod/revenge.md`). A faction takes revenge on one player who killed too many
of its units, and a spawn event's last wave can carry bosses. Design and sessions: `docs/features/REVENGE.md`; audit:
`docs/audits/revenge.md`; design §9 D46, D47, D51. Wire api 7. state.json and the cfg are unchanged; heat lives in
memory only.

<details open>
<summary><b>Definition keys</b></summary>

A key that fails validation disables the event, with one reason that names the field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `trigger` | `{ "type": "Revenge", "factions": [1-5 factions], "heat": 3-500, "windowSeconds": 60-3600, "warnAt": [0-3 values below heat, ascending], "vbloodHeat": 0-100 (10), "playerCooldownMinutes": 0-1440 (30) }`, optional `scope` | Per definition and player, the kills of the factions within the window: 1 point a kill, `vbloodHeat` for a V Blood. Our own spawned units and other players add none. Spawns pillar, SpawnWaves only, with an AroundPlayer location (tiers 15-40 m out), Hunt, no fanOut and no `allowTerritory` |
| `action.bosses` | `[ { "prefab": "<V Blood or unit>", "count": 1-3, "modifiers": {...} } ]`, at most 3 entries | On any SpawnWaves action: the bosses join the last wave only, beside its units. A boss without modifiers takes the wave's (growth included). Refused with fanOut |
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Heat** (`Logic/Revenge.cs` RevengeHeat). Fed from the same guarded death read as FactionKills (`Services/TriggerBus.cs`
  KillReader, with the victim's V Blood flag from DeathRule). Rows per definition and player, 200 per definition and
  2000 in all, the least recently updated non-due row dropped first; a due player's row is never dropped. Warning
  levels and cooldowns live outside the rows. "[nyar] revenge <id>: warning <k> of <n>", "vblood kill +<p> heat" and
  "revenge <id> due" carry the definition id and counts only.
- **Gate.** Every 5 s the due players start in the order they became due: held "(territory)" inside claimed territory,
  "(busy)" while the definition runs, "(target busy)" while another revenge has the player, "(ineligible)" while the
  player is offline, dead, in PvP combat, at an unusable position or outside the action's scope (or the territory map
  is down), "(controls)" when the caps, cooldowns or purge cooldown refuse, one line per change of reason. A started revenge
  resets the heat and starts `playerCooldownMinutes`; a conditions refusal resets the heat without a cooldown; a
  controls refusal keeps it. `.nyar event start` of a Revenge definition focuses the admin.
- **Focus only.** Each tier spawns around the target only (Logic/PlayerPick ChooseFocus), never in claimed territory,
  and Hunt seeds only the target (HuntPlan.TargetsFor). Another player who attacks a squad is fought through the game's
  own aggro. A squad whose target leaves its Hunt range idles until the target returns or the event ends.
- **Lines.** A server alert from RevengeAlertPool names the faction only; the private RevengeWarning, RevengeStart and
  RevengeEnd go to the target alone (`Announcer.SendTo`, skipped when offline; a failing send logs once per streak and
  never stops the heat or the start). The end line goes on a natural end, all waves defeated and an admin stop.
- **End paths.** "[nyar] revenge <id> ended: natural end|all waves defeated|admin stop|purge|restart|pillar off|fault".
  Purge empties every heat row, due player, warning and cooldown; a restart cancels the instance, the boot sweep
  removes its units, and the heat is gone (memory only). A disable or an invalid edit drops the definition's rows at
  the next 5 s scan; `.nyar pillar <name> off` drops them at once. A heat row keeps at most 500 entries (the largest
  `heat`), so a long window cannot grow it without bound.
- **Bosses.** A boss is a SpawnTracker unit like any other: the caps cut the last wave's regular units before its
  bosses, a Region wave trades a boss into a group that found a spot, and every end path, the purge and the boot sweep
  remove it. Ungoro's own summons belong to the game and end within 120 s.
- **Changed: our V Bloods raise nothing** (`Logic/Empowerment.cs` DeathRule, D30). A V Blood spawned by an event or
  `.nyar spawn` no longer raises VBloodKilled, so a boss wave cannot start another event; a native V Blood still does.
- **Chat.** `.nyar event set <id> trigger.factions|trigger.heat|trigger.windowSeconds|trigger.warnAt|trigger.vbloodHeat|
  trigger.playerCooldownMinutes <value>` and `action.bosses <prefab[:count],…|none>`, refused with the validator's
  reason. `event info` shows the trigger, the bosses and "revenge: <r> heat rows, <d> due, <n> running".
- **Templates.** `bandit-revenge` (disabled): Faction_Bandits, heat 30 in 900 s, warnings at 10 and 20, three tiers
  (thugs and hunters, then a deadeye with doubled health) around the target, Hunt, the scoreboard. `spider-swarm`'s
  and `spider-hunting-grounds`' last waves carry Ungoro (CHAR_Spider_Queen_VBlood). Thirteen templates in all.
- **Wire.** api 7: `trigger=revenge` on def and tpl rows; nothing else.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **Rollback drill.** Accepts 0.11.0's "unknown trigger type Revenge" and "unknown field action.bosses"; fixtures
  pair-revenge and pair-bosses pass, pair-othertrigger2 ("Swarmz") fails; selftest 29/29.
- **`-DependencySuite revenge`.** heat-read and private-send.
- **vrclient.** Scenarios revenge, revenge-vblood, revenge-restart/-boot2, revenge-timing, boss-wave; NyarDevTools
  `.devkill <prefab> <radius> <max>` and `.devunits` over far (disabled) chunks.
- **Tests.** 3134 (2935 at 0.11.0), with a control row for every D-item (ControlCases).
</details>

## [0.11.0] - 2026-10-13

The `hunting-grounds` child of the DoD Epic (`docs/dod/hunting-grounds.md`). A spawn event can be bound to the game's
world regions: its waves spread over the regions, its units stay inside them, and its groups hunt the nearest player
inside. Design and sessions: `docs/features/HUNTING_GROUNDS.md`; audit: `docs/audits/hunting-grounds.md`; design §9
D48-D50. No wire change (api 6). state.json and the cfg are unchanged.

<details open>
<summary><b>Definition keys</b></summary>

All optional, on a SpawnWaves action, and each needs a regional `scope` (a list of region names, never Global). A key
that fails validation disables the event, with one reason that names the field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `action.location` | `{ "type": "Region" }` | Each wave spawns in groups of at most 5 units at spots drawn over the scope regions, weighted by area, at least 30 m apart, on free and grounded tiles, outside claimed territory. A group without a spot is skipped; a wave with none is skipped. Refused with `arena` and `fanOut` |
| `action.regionLock` | `true` / `false` (absent: false) | Every 5 s, a unit of the running instance that has left the scope regions is moved back to its last spot inside, near where it left, and drops its targets on players outside. Hunt never seeds a player outside. Refused with `arena` |
| `action.behaviour` | `{ "type": "RegionHunt" }`, needs `regionLock: true` | Each group targets the nearest eligible player inside the regions. A unit more than 60 m from its target is moved toward it in hidden jumps, the last landing 50-60 m away; a unit within 100 m of an eligible player inside the regions is set on them, and the game's own pathing and aggro take over |
</details>

<details open>
<summary><b>The four modes</b></summary>

1. **Region roam:** a Region location with the lock and RegionHunt (the `spider-hunting-grounds` template).
2. **Point arena:** swarm's `arena`, unchanged.
3. **Region ambush:** an AroundPlayer location with a regional scope picks only players standing inside the regions;
   with no player inside, the wave is skipped "no eligible player". With the lock the units never chase out.
4. **Point spread:** a Point location with the lock and RegionHunt: the units start at the point and spread through
   the region after players.

The fifth, map-wide spread, comes with the apocalypse event, on this release's mover.
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Region phase** (`Logic/HuntingGrounds.cs` LockPhase, HuntPhase, JumpRule; `Services/SpawnTracker.cs` RegionTick;
  the EventScheduler "region" phase after "arena"). Every 5 s while a locked or RegionHunt event runs: the lock, then
  the hunt. Only the running instance's own tracked units are read or moved; units of other events, of earlier
  instances and of `.nyar spawn` are never touched. A move writes Translation, LastTranslation, PreCombatPosition and
  SpawnTransform.Position, empties the path buffer and zeroes velocity.
- **Hidden jumps** (D8, A1-A3). A unit jumps only while no online player stands within 100 m of it: at most 25 m
  along the line to its target, or 35, 45 or 55 m to cross a ravine when no shorter landing passes, never ending
  within 100 m of the target; the final jump lands 50-60 m from the target. A landing is free and grounded at the
  unit's height level or a neighbouring one (two or three levels on hillsides), inside a scope region, outside
  claimed territory and at least 50 m from every online player. At most 20 units jump per check across all events,
  and the hunt spends at most 200 walk checks per check. Every event's units that can jump plan before any stuck unit;
  a stuck unit rejoins, 3 m beside, the unit of its instance nearest its target. A unit that cannot move unseen waits.
- **Lines.** "[nyar] region lock <id>: returned <n>, farthest <d> m out" and "[nyar] region hunt <id>: <j> jumped,
  <w> waiting, <r> in reach, landed <d> m", counts and one distance only, never a name or a position. A failing check
  logs "check failed" or "move failed" once per streak and holds `region lock: check failing` or `region hunt: move
  failing` on the health line and `.nyar status` until a check succeeds. A region phase of 100 ms or more logs
  "region parts: …" (where its time went), at most once a minute.
- **End paths.** Stop, pillar off, purge, the event's end and a restart drop the instance's records; nothing moves
  after the end.
- **Chat.** `.nyar event set <id> location region`, `action.regionLock <true|false|none>` and `action.behaviour
  regionhunt`, refused with the validator's reason; `action.scope global` on a Region or locked event, `action.arena`
  on a locked event, removing the lock under RegionHunt and `fanOut` on a Region location are refused. `event info`
  shows "location: anywhere in the scope regions", "region lock: on" and "behaviour: hunts the nearest player in the
  regions, hidden jumps beyond 60 m".
- **Template.** `spider-hunting-grounds`: a Region location over DunleyFarmlands with the lock and RegionHunt, three
  waves of 2 forestlings, 2 forest spiders and 2 ranged spiders at level 30, 300 s apart, the scoreboard, 1800 s. Manual,
  disabled. Twelve templates in all.
- **Swarm.** The arena return was seen in game with a player walking out (swarm Session 3); it is no longer marked
  experimental.
- **Fixed: a player's death raised VBloodKilled** (`Logic/Empowerment.cs` DeathRule, `Patches/DeathEventPatch.cs`). A
  player character carries VBloodConsumeSource, so since faction-empowerment D13 every player death reached each
  `"bosses": ["any"]` trigger (seen in the owner's test as "trigger: VBloodKilled CHAR_VampireMale"). A death of a
  PlayerCharacter now raises nothing.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **Rollback drill.** Accepts 0.10.0's "unknown field action.regionLock", "unknown behaviour type RegionHunt" and its
  three-type location rule (a Region location) among the newer keys; fixtures pair-regionlock, pair-regionhunt and
  pair-regionlocation pass, pair-colourkey still fails; selftest 26/26.
- **`-DependencySuite hunting-grounds`.** region-lock and region-hunt.
- **Tests.** 2935 (2768 at 0.10.0), with a control row for every D-item (ControlCases).
</details>

## [0.10.0] - 2026-10-11

The `swarm` child of the DoD Epic (`docs/dod/swarm.md`). A spawn event can be held inside an arena, grow its waves
from one base wave, and name the player with the best kill-to-death ratio. The arena return has not yet been seen in game (see Sessions). Design and sessions:
`docs/features/SWARM.md`; audit: `docs/audits/swarm.md`; design §9 D33, D42. No wire change (api 6). state.json and
the cfg are unchanged.

<details open>
<summary><b>Definition keys</b></summary>

All optional, on a SpawnWaves action. A key that fails validation disables the event, with one reason that names the
field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `action.arena` | `{ "radius": 15-60 }`, Point or Admin location | Keeps the event's own units in a circle around the spot, or around the admin who started it. Refused on AroundPlayer, and when the spawn radius or Hunt range is wider than the arena |
| `action.growth` | `{ "addUnits": 0-10, "addLevels": 0-5, "whenCleared": bool }`, units form, `waves` 2-10 | Generates `waves` waves from the base wave. Wave n has each entry's count + (n−1)·addUnits and level + (n−1)·addLevels (capped at 120). Waves 2+ start `intervalSeconds` later, or also when the previous wave is cleared. The last wave may hold at most 50 units; each wave is still clamped by `[Limits] MaxUnitsPerWave` (default 20) |
| `action.winner` | `"KillDeath"`, needs `scoreboard: true` | Adds a winner line after the scoreboard: the best kills ÷ max(1, deaths) among players with a kill |
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Arena** (`Logic/Arena.cs`, `Services/SpawnTracker.cs` ArenaTick, the EventScheduler "arena" phase). Every 5 s while
  an arena event runs, a unit of that running instance more than radius + 10 m from the centre is returned to a spot
  within the spawn radius. The return uses the walkable height (a Point saved without one uses the units' own median
  height), regroups, and drops the unit's targets that are outside the arena. Units of other events, of earlier
  instances and of `.nyar spawn` are never moved. A return logs "[nyar] arena <id>: returned <n>, farthest <d> m",
  never a name or a position.
- **Growth** (`Logic/Growth.cs`). The parser stores the generated list as an ordinary wave list, so the 0.9.0 wave
  schedule, clears, "all waves defeated" and the scoreboard run unchanged. The generated waves are never written to
  events.json.
- **Winner** (`Logic/Scoreboard.cs`, `Logic/Messages.cs`). The winner is picked over every scoreboard row, not only
  the top 3. Ties go to more kills, then fewer deaths, then the earlier first kill. The line is "<event> winner:
  <name>, K/D <r> (<k> kills, <d> deaths)", with r in the invariant culture, or "winner: none". It shows only where
  the scoreboard shows. The scoreboard log line ends ", winner K/D <r>" or ", no winner", never a name.
- **Failures.** A failing arena check (one unit, or the whole phase) logs "[nyar] arena <id>: check failed: …" once per
  streak and holds `arena: check failing` on the health line and `.nyar status` until a check succeeds. No later tick
  phase, despawn batch or event end is skipped.
- **Chat.** `.nyar event set <id>` takes `action.arena <15-60|none>`, `action.winner <killdeath|none>`,
  `action.growth.addUnits`, `.addLevels`, `.whenCleared` and `action.growth none`. An edit that would add an arena,
  winner or growth conflict is refused with the validator's reason; a conflict the file already held never blocks an
  edit. `event info` shows the arena, the winner, the growth line and one line per generated wave.
- **Template.** `spider-swarm`: an Admin location, arena 40, radius 12, Hunt 40, scoreboard with winner, 1200 s,
  five spider waves growing from 6 units at level 30 to 18 units at level 42 (every 120 s or when cleared). Manual,
  disabled. Eleven templates in all.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **Rollback drill.** Accepts 0.9.0's "unknown field action.arena", "unknown field action.winner" and "unknown field
  action.growth" among the newer keys; fixtures pair-arena, pair-winner and pair-growth pass, pair-otherkey still
  fails; selftest 23/23.
- **`-DependencySuite swarm`.** arena-read, with its control.
- **vrclient** (dev only): scenarios swarm, swarm-waves, swarm-death, swarm-prep-restart, swarm-noadmins,
  swarm-purge, swarm-timing and swarm-chase; launch waits 10 s so the first PLAY click can't land on Continue.
</details>

<details>
<summary><b>Sessions</b></summary>

- **Session 1** (vrclient, three boots): sw-arena's 8 units stayed at the spot with Chaos 60 m away, but they never
  engaged the invulnerable Chaos, so the return never fired (no "[nyar] arena" line). spider-swarm
  grew 6/9/12/15/18 units at levels 30/33/…/42 and ended "all waves defeated (5 of 5 waves)" with a 60-kill winner.
  The winner line was seen rendered in chat. A death counted (K/D 15.00). Admins off gave no winner. Stop, purge and
  restart each left 0 tracked units. Tick budget "10/10 windows under 5 ms".
- **Session 2** (vrclient): a chase probe. vrclient can't walk the character, and an invulnerable character draws no
  attack, so the in-game return is left to the owner's run with a vulnerable player.
</details>

## [0.9.0] - 2026-10-11

The `wave-sets` child of the DoD Epic (`docs/dod/wave-sets.md`): a spawn event's waves can each have their own units,
levels and stats, start on a timer or when the wave before is cleared, end the event when the last one is beaten, and
end with a scoreboard. Design and sessions: `docs/features/WAVE_SETS.md`; audit: `docs/audits/wave-sets.md`; design §9
D38–D41. No wire change (api 6); state.json unchanged.

<details open>
<summary><b>Definition keys</b></summary>

All optional; each failure disables the event with one reason naming the field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `action.waveList` | 1–10 waves; each `units` 1–10 entries (`prefab`, `count`, `chance`, optional `modifiers`) | Replaces `units`, `waves`, `intervalSeconds` and `modifiers` (both forms together are refused); each entry keeps its own level or `levelDelta` and stat multipliers |
| `waveList[n].afterSeconds` | 10–3600, waves 2+ | Starts that long after the previous wave's decision |
| `waveList[n].whenCleared` | `true`/`false`, waves 2+ | Starts when the previous wave is cleared; with `afterSeconds`, the earlier of the two; neither key means cleared |
| `action.scoreboard` | `true`/`false`, either form | Counts kills of the event's units and players' deaths to them; the top 3 at the end |
| cfg `[Scoreboard] IncludeAdmins` | `false` | Admins appear on the scoreboard only when true |
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Per-entry tuning.** `Logic/Spawning.cs` `WaveRoll.ExpandEntries` gives each rolled copy its entry index; the clamps
  and the fan-out deal keep it, so every queued unit carries its own entry's tuning. The units form is byte-for-byte
  0.8.0.
- **Schedule** (design §9 D40). `Logic/Engine.cs` decides each wave at the earlier of the previous decision +
  `afterSeconds` and the previous wave cleared. Cleared = decided, with no pending order and no tracked unit of that
  wave (`SpawnLedger` keeps each unit's wave and instance start). A skipped, zero-rolled or zero-queued wave is
  cleared at its decision. The log line is "event <id> wave <n> cleared".
- **Victory** (D41). The last wave cleared after every wave was fought ends the event as a natural end: "event <id>
  ended: all waves defeated (<k> of <k> waves)", with the usual grace cleanup.
- **Scoreboard** (D38, D39). `Logic/Scoreboard.cs` keeps rows in memory (at most `Limits.ScoreboardPlayers` 200; a
  further player adds to the totals only). `Logic/ScoreFeed.cs`, fed by `Patches/DeathEventPatch.cs`, credits a
  familiar's or summon's kill to its player and counts a player's death to an event unit. Only units spawned since
  that instance started count, so a stop-and-start inside the grace credits nothing from the earlier one (A3). Shown
  at the natural end, victory and `event stop`: "<event> scoreboard: 1. <name> <k> kills, <d> deaths …" and "<p>
  players, <k> kills, <d> deaths", or "no player scored". Never on a purge, restart, pillar off or fault. Names appear
  only in those chat lines.
- **Failures.** A failing kill read or cleared read logs once per streak and holds `scoreboard: kill read failing` or
  `wave sets: cleared read failing` on the health line and `.nyar status` until a read succeeds.
- **Chat.** `.nyar event set` takes `action.scoreboard`, `action.waveList.<n>.units`, `.afterSeconds`, `.whenCleared`,
  `.units.<m>.<level|levelDelta|maxHealth|power|moveSpeed|attackSpeed>` and `action.waveList.<n> none`. On a
  units-form event, `waveList.1.units` converts it. `event info` shows one line per wave. Every reply is under 480
  bytes.
- **Template.** `graveyard-waves`: three growing armored Farbane waves around a player, the last with a level-30
  Guardian; Hunt 45, scoreboard, 900 s, Manual, disabled. Ten templates in all.
- **Timing** (A7). With TimingLog, a hunt phase over 20 ms logs "hunt parts: <ms> (sweep, players, seed of which
  buffer, verbose, outside parts; gc net <n> il2cpp <n|n/a>; units, events)", at most once a minute.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **Rollback drill.** Accepts 0.8.0's "unknown field action.waveList" and "unknown field action.scoreboard" among the
  newer keys; fixtures pair-wavelist and pair-scoreboard pass and pair-colourkey (a key no release has) fails;
  selftest 20/20.
- **`-DependencySuite wave-sets`.** kill-read and cleared-read, each with its control.
- **vrclient** (dev only): scenarios wave-sets, wave-sets-prep-restart, wave-sets-boot2, wave-sets-death and
  wave-sets-timing; NyarDevTools gains `.devkill <prefab guid>` and `.devdie`.
</details>

<details>
<summary><b>Sessions</b></summary>

- **Session 1** (vrclient): waves cleared and left alive on their timer, all waves defeated with a 28-kill scoreboard,
  a death counted, admins off, then stop, restart and purge at wave 2, each leaving 0 tracked units.
- **Session 2** (vrclient): 20 tracked level-30 units hunting for ten windows, "timing span: 10/10 windows under 5
  ms, 0 slow ticks"; the one hunt-parts line was the first hunt tick (29.9 ms).
</details>

## [0.8.0] - 2026-10-04

The `automation` child of the DoD Epic (`docs/dod/automation.md`): events start on their own, from a timer or from
what players do, and a wave can spread over several players. Design and sessions: `docs/features/AUTOMATION.md`;
audit: `docs/audits/automation.md`; contract: `docs/RAPHAEL_INTEGRATION_CONTRACT.md` (api 6).

<details open>
<summary><b>Definition keys</b></summary>

All optional; each failure disables the event with one reason naming the field. SchemaVersion stays 1.

| Key | Values | Effect |
|---|---|---|
| `trigger` `Interval` | `minMinutes`, `maxMinutes` 5–1440, min ≤ max; optional `scope` | The next start is drawn uniformly in whole seconds after the event was last seen inactive (its end, a refused start, an enable or reload, the boot) |
| `trigger` `RegionEntered` | `scope` (region names, required), `playerCooldownMinutes` 0–1440 (absent 30) | A player's entry into a scope region starts the event, aimed at that player |
| `trigger` `FactionKills` | `factions` 1–5 (catalog names, not deny-listed), `kills` 3–500, `windowSeconds` 10–3600, `shared` (absent false); optional `scope` | `kills` deaths of those factions by one player (or by all, `shared`) within the window |
| `action.fanOut` | `{ "maxInstances": 2–10, "minSpacing": 50–500 }`, AroundPlayer only | One group per picked player, at least `minSpacing` m apart; each group rolled on its own, the groups together clamped once to the caps and the allowed units dealt round robin |
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Interval.** `Logic/Schedule.cs` `IntervalClock` polls every tick (phase "interval"). The next start lives in
  state.json `NextInterval` (pinned name, left out when empty, one bad entry drops only itself); one at or before the
  boot is redrawn, so downtime is never replayed. A purge cooldown refuses a due start, which redraws.
- **Player scan.** The tick phase "player triggers" reads players every 5 s through one query over `User` (A6);
  `Logic/PlayerTriggers.cs` keeps region entries, cooldowns and kill windows in memory only. A refused start's line is
  throttled to one per definition per minute.
- **Kills.** `Patches/DeathEventPatch.cs` reads the killer and the victim's faction; SpawnTracker.Died, the V Blood
  path and TriggerBus.Died each run in their own try/catch, so none can skip another death.
- **Fan-out.** `Logic/Spawning.cs` `PlayerPick.ChooseMany` (the triggering player first) and `WaveGate.DecideGroups`;
  each group gets its own Hunt tag; a fanned-out wave sends one push and one wave line ("around <n> players").
- **Reachable spawn points** (A7–A16, design §9 D31–D32). A checked point needs a straight walkable line from the
  picked player, following the terrain one level per sample; a blocked line gives its farthest reachable sample
  ("shortened"); with no walkable spot at the player, the nearest one 1–3 m away is the start, and failing that the
  point is placed by the spot check alone ("spot only"). With VerboseLogging, the first group without a walkable line
  logs a walk survey of eight directions, never a coordinate.
- **Failures.** A failing scan or kill read logs once per streak and holds `triggers: player scan failing` or
  `triggers: kill read failing` on the health line and `.nyar status` until a read succeeds.
- **Chat.** `.nyar event set` takes `trigger.type Interval|RegionEntered|FactionKills`, `trigger.minMinutes`,
  `trigger.maxMinutes`, `trigger.playerCooldownMinutes`, `trigger.factions`, `trigger.kills`, `trigger.windowSeconds`,
  `trigger.shared` and `action.fanOut none|"<max> <spacing>"`; `event info` shows the new trigger, fanOut and an
  Interval's next start. Every reply is under 480 bytes.
- **Templates.** `roaming-hunters` (Interval 60–90 min, fanOut 3/150), `border-watch` (RegionEntered Dunley
  Farmlands, 30 min) and `bandit-reprisal` (FactionKills 20 bandits in 300 s), all disabled, all Hunt 45; nine in all.
- **Wire.** api 6: the trigger values `interval`, `regionentered`, `factionkills`. No new field, push kind or command.
- **Debug only.** `Debug.FaultInjection = phantoms:<n>` adds n phantom players at 200 m steps for fan-out tests; a
  Release build has none.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **`-TimingSpan -MinScans <n>`.** Sums the ", <k> player scans" field of the counted windows and fails below n.
- **`-DependencySuite automation`.** player-scan, kill-read, state-write and release-tools, each with its control.
- **Rollback drill.** Accepts 0.7.0's "unknown trigger type Interval|RegionEntered|FactionKills" and "unknown field
  action.fanOut", printing "(<t> newer action types, <k> newer keys, <r> newer trigger types)"; for a newer release of
  0.8.0 or later it adds `drill-interval` and asserts state.json carries its `NextInterval` entry; selftest 17/17.
- **vrclient** (`tools/vrclient/`, dev only, never shipped): drives the real client for in-game scenarios, with the
  dev plugins DevChatEcho (command replies in the log) and NyarDevTools (`.devtp`, `.devmark`).
</details>

<details>
<summary><b>Sessions</b></summary>

- **Sessions 1–1f** (with the owner): each trigger in game, the chat fields, the placement fixes A4–A16.
- **Session 1g** (vrclient): an ambush with Hunt reached the player within 20 s at a rock ravine and on a strip inside
  a pond; without Hunt the pond strip leaves units across the water (the known limit below).
- **Session 2** (vrclient, Debug build, phantoms:4): three fanned-out Hunt events held "around 5 players", the
  MaxUnitsPerWave and MaxTrackedUnits clamps and 150 tracked; ten windows averaged 0.34–0.50 ms with 12–13 player scans
  each, 0 slow ticks; stop, natural end, purge, restart (20 found, then 0) and uninstall (0 found, 20 listed) each
  left 0 tracked units.
</details>

- **Known limit.** A spawn point needs a straight walkable line to the player; where none exists (a strip inside a
  pond), a wave without Hunt can stand across the water. A flood fill over the tile map is planned after Halloween.
- **Scale.** The tick budget is measured and promised at the default caps, with fan-out, the scan and the kill feed
  running; `MaxTrackedUnits` 151–500 stays best effort.
- **Upgrading / rollback.** No new cfg keys. 0.7.0 loads 0.8.0's files but disables a definition using a new trigger
  type or `fanOut`, and ignores `NextInterval` (rollback gate).

## [0.7.0] - 2026-09-29

The `event-spawns` child of the DoD Epic (`docs/dod/event-spawns.md`): spawn waves gain strength, a behaviour, a
player-relative location and a castle-territory rule. Design and sessions: `docs/features/EVENT_SPAWNS.md`; audit:
`docs/audits/event-spawns.md`.

<details open>
<summary><b>Definition keys</b></summary>

All optional on a `SpawnWaves` action; each failure disables the event with one reason naming the field. SchemaVersion
stays 1.

| Key | Values | Effect |
|---|---|---|
| `units[].chance` | number 0.05–1.0 (absent 1.0) | Each copy of the entry is rolled on its own (`WaveRoll.Expand`); the caps clamp the rolled count |
| `modifiers.level` / `levelDelta` | integer 1–120 / −5..5, never both | Absolute level, or the prefab level plus the delta clamped 1–120 |
| `modifiers.maxHealth` · `power` · `moveSpeed` · `attackSpeed` | 0.5–3.0, two decimals | `MultiplyBaseAdd` modifiers of value − 1 on the unit's marker buff; `power` covers physical and spell power, `attackSpeed` primary and ability attack speed |
| `loot` | boolean (absent false) | `DropTableBuffer` is cleared unless true |
| `behaviour` | `{ "type": "Hunt", "range": 10–60 }` | Guard and Ambush are refused as unknown until `spawn-extras` |
| `location` | `{ "type": "AroundPlayer", "minDist": 10–60, "maxDist": 15–80 }` | minDist < maxDist; with Hunt, maxDist ≤ range |
| `allowTerritory` | boolean (absent false) | Lifts the claimed-territory skip and ring-point block, nothing else |
</details>

<details open>
<summary><b>Runtime</b></summary>

- **Wave precedence.** `Logic/Spawning.cs` `WaveGate.Decide` gives each wave one outcome, in order: a start blocker or
  an ended event; "territory unknown" when the wave needs a territory map and it could not be built (AroundPlayer and
  Hunt always need it, other waves unless `allowTerritory`); no eligible player; a centre in claimed territory; the
  chance roll ("0 units rolled"); then the `MaxUnitsPerWave` and `MaxTrackedUnits` clamps. A skipped wave uses its
  slot, sends no push, and is logged `wave <n> of <id> skipped: <reason>`.
- **Territory.** `Services/TerritoryMap.cs` reads, once per wave, the blocks of every `CastleTerritory` a `CastleHeart`
  names (decaying hearts included); `Logic.Territory.IsClaimed` decides with KindredCommands' block conversion. A
  claimed ring point counts as blocked in the walkable-point search. A castle claimed during a wave counts from the
  next wave.
- **Player pick.** `Logic.PlayerPick.Choose` picks uniformly among online, alive characters (admins included) not in
  claimed territory and not carrying `Buff_InCombat_PvPVampire`. With a regional `action.scope` the player must stand
  in it and the centre is retried at the 11 other ring angles until it is in scope. No log line, message or api row
  names or locates the player ("around a player").
- **Hunt.** `Services/HuntAction.cs` every 5 s adds one `AggroBuffer` entry per unit for each of `HuntPlan.Targets`
  (eligible players within range of the wave centre, nearest first, at most 5). It never seeds a player the buffer
  already holds, removes only its own seeds, and removes all of them when no player is eligible or the event's latest
  territory build failed. Its seed record, keyed by unit entity and target, stays authoritative after a partial write
  (A67: kept seeds are recognised by Entity).
- **End paths.** `Logic.WaveLifecycle` empties the spawn ledger, the seed set and the kept territory map together on
  natural end, `event stop`, fault cancel, `purge confirm` and restart.
- **Failures.** A throwing modifier, loot or level write discards the unit (`SpawnTracker.Prepare`, `Abandon`); a
  throwing hunt query or aggro write logs `hunt <id>: seed failed` once per streak and the wave continues unhunted; a
  throwing player query skips the wave. The health line, admin `.nyar status` and the login notice gain
  `spawns: territory unknown`, `unit setup failing (<id>)`, `hunt seed failing (<id>)` and
  `player query failing (<id>)` while each streak is open.
- **Chat.** `.nyar event set <id>` takes `action.modifiers.<key> <value|none>`, `action.loot`, `action.allowTerritory`,
  `action.behaviour none` or `action.behaviour "hunt <range>"`, `action.units.<n>.chance` and
  `location "aroundplayer <min> <max>"` (a quoted value is one argument); every reply
  is under 480 bytes (`CommandArgTests.ChatBytes`). `event info` prints the new keys; `.nyar debug here` adds position,
  spell power, move speed and primary attack speed.
- **Templates.** `undead-rising` gains `"modifiers": { "levelDelta": 2, "maxHealth": 1.2 }`; all six stay disabled.
- **Timing.** A slow tick names its top three phases (A70); the first wave after a boot costs one long tick (up to
  ~200 ms, spawn queues and the first Hunt tick), under the 250 ms slow-tick warning.
- **Wire.** Unchanged: api 5, no new field, push kind or command.
</details>

<details>
<summary><b>Static checks and tools</b></summary>

- **EntityWrites.** A preflight check finds every entity write call (component, buffer, destroy, instantiate and
  `EntityManager` mutators, and locals taken from `GetBuffer<`/`ReadBuffer`) outside `Services/` and `Patches/`.
- **Gateway.** HuntAction joins `$DispatchedServices`; the tick and boot entry points that write are `[Mutating]` and
  accepted from `EventScheduler.cs`, `Core.cs` and `Patches/` as the System actor, by name only.
- **`-TimingSpan`.** Checks a log copy for N consecutive one-minute windows under 5 ms with a tracked and a Hunt-target
  floor, after a skipped warm-up, and fails on a slow tick after the last window.
- **`-SessionsOf`.** A session whose log copies were lost before a count line was written can be listed in
  `sessionsUncounted`, bounded by the script's allow list, with exactly one "uncounted (<reason>)" audit line (A72).
- **Rollback drill.** Accepts 0.6.0 disabling a definition for a 0.7.0 key ("unknown field action.<key>",
  "unknown field action.units.chance", an AroundPlayer location) and prints "(<t> newer action types, <k> newer keys)";
  selftest 13/13.
</details>

<details>
<summary><b>Sessions</b></summary>

- **Session 1** (parts A–C): each modifier read back in `.nyar debug here` against a plain wave; loot true kept the
  drop table and loot false cleared it; the claimed-territory skip and `allowTerritory`; AroundPlayer placement; Hunt
  seeding and re-seeding; every new chat reply rendered whole. Logs read by hand, no `[Error]`.
- **Session 2:** a modified Hunt event of 30 units left 0 tracked units and no seeds after stop, natural end, purge,
  restart (boot sweep 30 found, then 0) and uninstall (0 found, 30 listed: the game's LifeTime removed them). At 150
  tracked Hunt units with a player in range, ten consecutive windows averaged 0.28–0.35 ms (max 2.7 ms), 0 slow ticks.
</details>

- **Scale.** The tick budget is measured and promised at the default caps (150 tracked). `MaxTrackedUnits` 151–500 is
  best effort, watched by the 250 ms slow-tick warning.
- **Upgrading / rollback.** No new cfg keys or files. 0.6.0 loads 0.7.0's files but disables a definition that uses a
  new key until it is removed (rollback gate).

## [0.6.0] - 2026-09-28

The `regions` child of the DoD Epic (`docs/dod/regions.md`): an event's trigger and action can be limited to named
world regions. Design and session: `docs/features/REGIONS.md`; audit: `docs/audits/regions.md`; contract:
`docs/RAPHAEL_INTEGRATION_CONTRACT.md` (api 5).

- **Region index.** `Services/RegionMap.cs` reads the game's `WorldRegionPolygon` entities once per boot (read-only:
  `PolygonBounds` and the `WorldRegionPolygonVertex` buffer) into `Logic/Regions.cs` `RegionIndex`: a box test, then
  an even-odd point-in-polygon test. The boot line is `regions: <p> polygons, <r> regions (<names>); <k> untagged,
  <d> dropped`; the dev world reads 10 polygons, one per region. Region names are the game's `WorldRegionType` names
  except None and Other (StartCave, FarbaneWoods, DunleyFarmlands, CursedForest, HallowedMountains, SilverlightHills,
  Gloomrot_South, Gloomrot_North, RuinsOfMortium, Strongblade), checked against the game at boot.
- **`scope` key.** `trigger.scope` and `action.scope` in events.json and templates: `"Global"` (the default when absent)
  or an array of 1–10 distinct region names, matched case-insensitively and stored in the game's spelling. The two are
  independent. An unknown, None, Other, empty, duplicate or non-string entry disables the event with a reason; so does
  a name the built index holds no polygon for ("region <name> is not on the map"). SchemaVersion stays 1.
- **Triggers.** A scoped VBloodKilled trigger starts only for a kill inside its regions (the victim's position is read
  only when a scoped definition matches; an unreadable position starts only Global ones). Schedule, GameTime and
  Manual need an online player in the regions: a scheduled or game-time occurrence with none is consumed and logged
  `event <id>: skipped, no player in <regions>`; an admin start is refused with `no player is in the event's regions`
  (`code=state reason=no_player_in_region`).
- **Empowerment.** A regional Empower sweep skips units outside the scope with the new skip reason `region` (after
  `other`); a unit already carrying the buff keeps it until the event ends.
- **Waves.** A Point location outside the scope disables the event at load; an Admin location outside it refuses the
  start with `your position is outside the event's regions` (`code=badarg reason=out_of_region`); a ring point outside
  the scope counts as blocked in the walkable-point search, before the walk budget.
- **Unavailable.** When the map cannot be read, every regional definition is disabled with "regions unavailable",
  Global ones run unchanged, the health line adds `regions: unavailable`, and `.nyar event reload` retries the build.
- **Commands.** `.nyar region list` (one line per region with its enabled-event count, then `global:`) and
  `.nyar region here` (admin-only; both only read). `event info` shows `trigger scope:` and `action scope:`,
  `event list` appends ` [<regions>]`, `event set <id> trigger.scope|action.scope <Global|name,name>` edits them, a
  `trigger.type` change clears `trigger.scope`, and announcements take `{region}`.
- **Wire, api 5.** `.nyar api regions [page]` (`[NYAR:region] id= events=` rows); `[NYAR:def]` and `[NYAR:event]`
  rows and the event-start and event-end pushes gain `region=` (the action scope, `-` for Global).
- **Session 1.** A regional wave refused in Dunley and started in Farbane; a Rufus kill started the Farbane-scoped
  vengeance event and not its Dunley copy; a Cursed Forest empowerment skipped all 193 Undead by region; a
  Farbane-scoped bandit empowerment applied 200 carriers and skipped 136 bandits outside Farbane. Tick timing averages
  stayed under 5 ms (sweep minutes up to 3.7 ms avg).
- **Upgrading / rollback.** No new cfg keys or files; a definition without `scope` behaves exactly as in 0.5.x. 0.5.2
  loads 0.6.0's files, but disables a definition carrying `scope` (unknown key) or an announcement using `{region}`
  (unknown placeholder) until they are removed (rollback gate).

## [0.5.2] - 2026-09-28

The `raphael-api-admin` child of the DoD Epic (`docs/dod/raphael-api-admin.md`): api 4, the admin actions as
machine-readable twins for the Raphael client. Design and session: `docs/features/RAPHAEL_API_ADMIN.md`; audit:
`docs/audits/raphael-api-admin.md`; contract: `docs/RAPHAEL_INTEGRATION_CONTRACT.md` §5a; client handoff:
`docs/RAPHAEL_HANDOFF_API4.md`.

- **Typed outcomes.** Every admin path (`Logic/AdminFlows.cs`, one flow per verb, shared by the human command and its
  twin) returns an `Outcome`: ok with keys, or a refusal with a code, an argument and a reason
  (`Logic/Outcome.cs`, contract §4). The human commands reply `outcome.Human`, pinned to 0.5.1's texts by
  `HumanReplyTests` (85 captured replies covering all 89 of 0.5.1's reply literals), so no human reply changes.
- **Twins.** `.nyar api event <verb> …` (start, stop, enable, disable, set, reload, new, copy, delete),
  `.nyar api template use <template> [as <id>]`, `.nyar api pillar <name> on|off` and `.nyar api purge [confirm]`
  answer exactly one `[NYAR:ok]` or `[NYAR:err]` line (admin-only). Order: rate gate, verb, argument count, argument
  checks, the admin log line (prefixed `api `), the action gateway, the operation. A malformed argument answers
  `code=badarg` before the log line.
- **Reads.** `.nyar api templates [pillar] [page]` (`tpl` rows), `.nyar api template info <template>`,
  `.nyar api pillar list` (five `pillar` rows) and `.nyar api killswitch` (`ks`).
- **Rate limit.** Five twins per admin per second (`Logic/RateGate.cs`, at most 64 admins tracked); the sixth
  answers `code=ratelimit secs=1`. Reads and human commands are not counted.
- **Failures.** A twin whose operation throws answers `code=io reason=internal` and logs the exception once per
  streak. `internal` promises no rollback: the action may be partly or fully applied, and the client re-reads state
  (A16).
- **Wire.** New codes `exists`, `state`, `invalid`, `full`, `io`, `confirm`, `limit`; `verb=` and `reason=` keys; a
  location value is written with one decimal, culture-invariant. `config-changed` is pushed after a pillar switch,
  as after the human command.
- **Static checks.** `Logic/ApiCommandTable.cs` copies every `.nyar api` signature; preflight `WireContract` holds
  it equal to the commands both ways, fails a parameter without a default, and `ApiOverloadTests` proves VCF can
  route every (word, argument count). The command walks reject attribute syntax they cannot read (qualified,
  aliased, escaped, listed or targeted `Command` attributes).
- **Upgrading / rollback.** No new cfg keys or files; `events.json` and `state.json` are unchanged. Raphael clients
  on api 1–3 keep using the human commands. 0.5.1 loads 0.5.2's files unchanged (rollback gate).

## [0.5.1] - 2026-09-28

The `walkable-spawns` child of the DoD Epic (`docs/dod/walkable-spawns.md`), split from event-spawns (Epic A26): wave
units spawn on walkable ground. Design and sessions: `docs/features/WALKABLE_SPAWNS.md`; audit:
`docs/audits/walkable-spawns.md`.

- **Walk check.** `Services/WalkCheck.cs` reads the game's static tile collision (read-only): per wave it builds map
  data over the live `TileWorldSingleton` (a system entity: `ServerScriptMapper.GetSingletonEntity`, then an
  `IncludeSystems` query, A11) at the height level `TileLayerUtility.GetHeightLevel(centre y)` (A13), and a point is
  walkable when `TileMapCollisionMath.CheckStaticCircle` (0.5 m circle, world metres, `CollideNormalMovement`) is free
  and `GetIsGrounded` holds. Session 1's walk probe chose the world-metres source: dry free and grounded, pond, second
  water body, cliff and building wall blocked; the tile-grid source read free everywhere (A8, A12). A default or
  uncreated TileWorld and a negative tile index never reach native code (A10).
- **Placement.** `Logic/Spawning.cs`: `SpawnPoints.Choose` keeps a free ring point, else tries the ring's 11 other
  angles, 12 angles at half the radius, then the centre (at most 25 checks); `WavePoints.Plan` keeps one point per
  unit in ring order under `WalkBudget` (2,500 game calls per tick, reset each tick; a point past it keeps its ring
  point, unchecked). A Point saved without a height is not checked. The wave line gains
  `(<m> moved, <u> unchecked)` and `, walk h <level>`.
- **Fail open.** No tile world, a height outside 0–1000 m, a throw or a failed dispose leaves the rest of that wave on
  its ring points, logs `walk check unavailable: <reason>` once per streak and adds `spawns: walk check unavailable`
  to the degraded list (health line, `.nyar status`, admin login notice) until a wave's check returns; the next wave
  tries again. `Dependency.WalkCheck` has its policy row and fault case.
- **Session 2.** bandit-ambush at a pond shore, radius 10, five waves: none in water, moved 2–3 of 6 per wave,
  tick timing avg ≤ 0.93 ms; the wave's height level equalled the admin's.
- **Removed.** The temporary `.nyar debug walk` probe of step 1. Preflight `DebugCommands` fails while any `debug`
  command's usage or parameter comparison names a verb other than `here`; `Changelogs` forbids the 0.5.0
  known-issue note in both READMEs from 0.5.1 on; ControlCases rows are keyed by plan slug and D-id.
- **Upgrading / rollback.** No new cfg keys, files or keys; events.json, state.json and api 3 unchanged. 0.5.0
  loads 0.5.1's files unchanged (rollback gate).

## [0.5.0] - 2026-09-28

The `event-library` child of the DoD Epic (`docs/dod/event-library.md`): built-in templates and in-game authoring.
Design: `docs/features/EVENT_LIBRARY.md`; sessions 1–7 there; audit: `docs/audits/event-library.md`.

- **Template catalogue.** `Resources/templates.json`, embedded and read-only, six templates (all `enabled: false`):
  legion-weekend-surge (Schedule Sat 20:00, Legion ×1.5 physical power and max health, 30 min), bandit-vengeance
  (8 bandit V Bloods, ×1.3 physical power and attack speed, 10 min, 30 min cooldown), undead-nightfall (GameTime
  night, ×1.25 physical and spell power, 20 min), militia-crackdown (15 Militia/Church V Bloods, ×1.3 max health,
  15 min, 30 min cooldown), bandit-ambush (manual, 3 waves of 4 CHAR_Bandit_Thug + 2 CHAR_Bandit_Hunter, radius 10)
  and undead-rising (manual, 2 waves of 5 armoured skeletons + 2 skeleton crossbows, radius 12). Validated at boot
  ("templates: 6/6 valid"); a bad catalogue disables the template commands, never the mod.
- **Commands.** `.nyar template list [pillar] [page] | info <id> | use <id> [as <newId>]`; `.nyar event new | copy
  | delete [confirm]` (30 s arming); `.nyar event set` gains trigger, faction, unit and `location here` fields
  (Point with x, y, z); `.nyar pillar list | <name> on|off` (written to the cfg, a running event of a pillar
  switched off ends). Every chat write is a whole-file edit through `.tmp` + replace with one `.bak`, refused when
  the file changed on disk since load or holds a newer schema, then reloaded, so a chat write equals a hand edit.
- **Readiness.** `.nyar event list` shows `ready`, `off (purge)`, `off (mod)`, `off (pillar)`, `full (cap)`,
  `invalid: <reason>` or `off (event)`; long V Blood triggers print as `vbloodkilled <n> bosses` with the bosses
  on `bosses:` lines of `info`.
- **Wave placement.** A Point keeps its height (`y`); a wave unit the game snaps onto another terrain level than
  the centre (more than 2 m off) is moved once to within 1 m of the centre, with its AI home
  (`AggroConsumer.PreCombatPosition`) (A23).
- **Slow-tick warning.** `[nyar] slow tick: <t> ms (<phase> <ms> ms, …; outside phases <r> ms)` for a tick of
  250 ms or more, at most one a minute with a held-back count, independent of `Debug.TimingLog` (A25).
- **Known issue.** A ring point in deep water at the centre's level is not detected; the unit is stuck until its
  wave ends (A27). A walkability check is planned for 0.5.1.
- **Soak.** Session 7: three boots with the six templates, a restart mid-event and a short boot after the final
  stop; `tools/soak-report.ps1` → "soak: 565 timing minutes, 27 starts, 25 ends, 2 cancelled by restart, 0 unpaired,
  0 unhandled, tick avg max 3.528 ms, templates 6/6", pass; no slow-tick line.
- **Tooling.** `tools/soak-report.ps1`, `preflight -Paths -DeclaredOf`, `-SessionsOf`, `-AuditOf`, `-RollbackOf`,
  `-DependencySuite`, `rollback-gate.ps1 -Plan`, `tools/ingame/session-events.py`.
- **Upgrading / rollback.** No new cfg keys; events.json stays SchemaVersion 1. 0.4.0 loads 0.5.0's files with the
  same valid and disabled counts (rollback gate, D29).

## [0.4.0] - 2026-09-26

The `faction-empowerment` child of the DoD Epic (`docs/dod/faction-empowerment.md`): **Pillar A, faction
empowerment**. Design: `docs/features/FACTION_EMPOWERMENT.md`; sessions 1–3 there; audit:
`docs/audits/faction-empowerment.md`.

- **`Empower` action** (pillar `empowerment`, which it must pair with): `factions` (1–5 `Faction_` names; players,
  servants, traders, critters, prisoners and `Faction_Ignored` refused), optional `includeUnits` / `excludeUnits`
  and `includeVBloods` (default false), and `stats`: `physicalPower`, `spellPower`, `maxHealth`, `attackSpeed`,
  `moveSpeed`, each a multiplier 1.0–3.0, at least one above 1.0. The seeded `example-empowerment` (disabled) rallies
  the bandits for 10 minutes after any V Blood kill.
- **Carriers, not stat writes.** Each eligible NPC gets one carrier buff (`BuffType.Replace`, marked
  `SpellLevel = 1313952069`, `LifeTime` = the event's seconds left, `EndAction Destroy`) holding the stat modifiers;
  the NPC's own stats are never written, so a carrier ends on its own even without the mod. Eligibility skips
  prefabs, the dead, V Bloods (unless included), our own spawned units and familiars' `Faction_Ignored`.
- **Sweeps and budget.** An event sweeps its factions at start and again every 15 s while active, so respawned and
  newly loaded NPCs are caught; applies and removals share `Limits.EmpowerBatchPerTick` (new cfg key, default 200,
  50–1000), removals first. Stop, purge and restart remove carriers; a carrier still present 5 s after its event's
  natural end is removed. The boot sweep splits our markers: units are despawned, carriers removed, their NPCs kept.
- **One empowerment per faction.** A start that overlaps an active Empower event's factions or units is refused
  ("faction <name> already empowered by <id>").
- **Messages and admin.** `{faction}` in announcements names the factions; `.nyar event set <id>
  action.stats.<stat> <1.0–3.0>`; `.nyar debug here` also lists up to 10 native NPCs with their carrier and stats;
  a manual start refused by the purge cooldown says how many seconds are left. The V Blood trigger now needs
  `VBloodConsumeSource`, so gate bosses no longer fire it.
- **Raphael api 3.** `.nyar api status` rows for an Empower event carry `kind=empower`, `faction=<names>`,
  `wave=-`, and for admins `units` = NPCs holding its carrier. api 3 only adds values; an api 2 client keeps working
  (`docs/RAPHAEL_HANDOFF.md` › api 3).
- **Release tooling.** `tools/dev-snapshot.ps1` (wraps every dev-server session), `tools/repo-rollback-drill.ps1`,
  `tools/rollback-gate.ps1` (the repository revert, the N-1 boot drill and the snapshot selftest as one gate),
  `tools/release-verify.ps1` (the pre-release zip's hash against the audit); `tools/rollback-drill.ps1` accepts that
  N-1 disables a definition of an action type it does not know. `preflight -SelfTest` runs every registered external
  selftest and the unit tests.
- **Upgrading.** events.json stays SchemaVersion 1 and existing definitions are unchanged. An `example-empowerment`
  written by an earlier release (a SpawnWaves action under pillar empowerment) now loads disabled with "pillar
  empowerment takes an Empower action": set its pillar to `spawns`, or replace it with the Empower template in the
  README (Faction empowerment). **Rolling back to 0.3.0:** it loads Empower definitions disabled ("unknown action type Empower") and
  does not remove carriers, which then expire within their event's remaining time (at most 2 h); run `.nyar purge
  confirm` on 0.4.0 first to remove them at once.
- 871 tests.

## [0.3.0] - 2026-09-26

The `raphael-api-core` child of the DoD Epic (`docs/dod/raphael-api-core.md`): the Raphael wire moves from api 1
(handshake only) to **api 2**. Contract: `docs/RAPHAEL_INTEGRATION_CONTRACT.md` §1–§4; client handoff:
`docs/RAPHAEL_HANDOFF.md`; sessions: `docs/features/RAPHAEL_API.md`.

- **`.nyar api status`** (anyone): one `[NYAR:event]` row per active event (`state=active`, or `ending` while its
  units wait out `GraceSeconds`, with `wave=-`), then `[NYAR:end] cmd=status count=<n>`. `units` goes to admins
  only; no row carries a position.
- **`.nyar api events [page]`** (admin): `[NYAR:def]` rows, 10 per page, then `[NYAR:end] cmd=events
  page=<cur>/<total> count=<n>`. The action is named by pillar even for a definition validation disabled; `name`
  is cut to 64 UTF-8 bytes and `reason` to 120 on a character boundary so every line stays under 480 bytes. A bad
  page answers `[NYAR:err] cmd=events code=badarg arg=page`. Both reads answer normally when `General.Enabled=false`.
- **`.nyar api sub on|off`** (anyone) and six push types, `[NYAR:ev] type=event-start|event-end|wave-warn|wave|
  killswitch|config-changed`. Subscriptions are in memory only (cap 128, `ratelimit` past it), pruned on
  disconnect through a `ServerBootstrapSystem.OnUserDisconnected` hook; the push queue holds 50 lines (oldest
  dropped) and sends 5 per tick. Fairness: `wave-warn` pushes only when the chat warning fires. An event's end drops
  its queued wave-warn lines; a waiting config-changed absorbs a new one. `.nyar event reload|set|enable|disable`
  push config-changed only when a load was applied (`Logic/DefinitionEditor`).
- **Failure isolation.** A throwing disconnect hook, user source, recipient or push entry point is caught, logged
  once per streak, and never stops the event tick or the other subscribers.
- **Preflight.** `Test-CheckWireContract` fails when a wire tag or `api` command in the code is missing from the
  contract, still PLANNED there, or the api numbers differ; `-SessionsOf` reads the preflight-checks `childDocs`
  mapping and fails on a session numbered twice; the data inventory covers each listed plan's Design › Data rows.
- **Rollback drill.** `tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1` (with `-SelfTest`) boots N on a fresh
  config, lets it write `events.json` and `state.json`, then boots N-1 on them and checks every load line. 0.3.0
  writes no new file and no new schema.
- 703 tests.

## [0.2.1] - 2026-09-25

Fixes from the foundation child's closing review (Codex whole-child cross-inspection, `docs/audits/foundation.md`
› Post-audit › Step 9). Foundation closes at 40/40 items verified.

- **Every unit despawns through the budget (A21).** 0.2.0 pushed a unit's `LifeTime` past the despawn queue's drain
  only when the event end decided its due time (A16), so a unit whose own `unitLifetimeSeconds` or
  `ManualSpawnLifetimeSeconds` decided it was removed by the game's lifetime system, one spawn batch per frame,
  outside `MaxDespawnsPerTick`. Every tracked unit now carries a due time (`SpawnLedger.Lifetime`: min(own lifetime,
  event end + `GraceSeconds`), or the manual lifetime); `SpawnLedger.QueueDue` moves due units into the budgeted
  queue each tick, earliest first; `LifeTime` is due time + ceil(`MaxTrackedUnits` / `MaxDespawnsPerTick`) s + 60 s
  for every unit, only a backstop. Session 20: 10 units with a 30 s lifetime left 1 a tick at `MaxDespawnsPerTick`
  1. The wave log line now reads "due in <n>s, lifetime <m>s".
- **Marker first (A22).** `SpawnTracker.Prepare` sets the marker buff (its `SpellLevel` record first) before any
  other fallible step and attempts `LifeTime` and `Age` even when marking fails, so a unit whose setup and destroy
  both fail keeps the boot sweep's record or its lifetime. Session 21: a kill after an autosave with 5 units alive,
  then "boot marker sweep: 5 found, 5 queued".
- 539 tests.

## [0.2.0] - 2026-09-25

First public beta: the foundation child of the DoD Epic (`docs/dod/foundation.md`, 34/40 items verified at
release, the rest close with it). The engine runs `SpawnWaves` events under every pillar switch; each pillar's
own action arrives with its child plan.

- **Pure logic core (`Logic/`, xUnit, 531 tests).** Event definitions v1 with per-field validation (a bad entry
  is disabled with its event id and the reason logged; a file that does not parse keeps the last valid set), control
  precedence (purge > `General.Enabled` > pillar switch > `[Limits]` caps > definition), server-local schedules
  keyed by occurrence so a restart never fires one twice, `ActionGateway` authorization for every mutating
  operation, the `[NYAR:*]` wire format, text sinks, message builders that cannot carry positions, the spawn
  ledger and the announcer rules.
- **Persistence.** `events.json`, `state.json` (running instances, tracked units, last fire and start per event,
  purge cooldown, daily banner key) under `BepInEx/config/Nyarlathotep/`, written through `.tmp` + `File.Replace`
  with one `.bak`; a corrupt file is kept as `.corrupt`. The first boot seeds five disabled example events.
- **Spawner.** `SpawnTracker` spawns through a per-tick budget (`MaxSpawnsPerTick`) with every unit registered,
  marked (a stat-carrying marker buff) and given a `LifeTime` that outlasts the budgeted despawn drain (A16);
  despawns go through `MaxDespawnsPerTick`; a boot sweep removes marked survivors of a crash or kill; a failed
  despawn is requeued with its slot.
- **Engine.** `TriggerBus` (Schedule, GameTime day and night, VBloodKilled via `DeathEventListenerSystem`,
  Manual), `EventScheduler` phases (spawn queues → triggers → events → announcements → health → state flush,
  each phase guarded), `EventRuntime` (waves, grace, expiry, fault limit, purge with `PurgeCooldownSeconds`),
  conditions (minPlayers, cooldownMinutes, chancePercent, window). A restart cancels every event.
- **Messages.** `Announcer` queue (20 lines, one a second, oldest informational line dropped first; a warning
  still queued at its wave time is dropped), wave warnings at `WarningOffsets`, start and end banners, the daily
  banner at `DailyBannerTime`, `.nyar announce`, a 10-minute health line, and a private degraded notice to an
  admin 10 s after login (not again on a reconnect within 60 s). `[Announcements]` switches all default off.
- **Commands.** `.nyar` (lists the caller's commands), `.nyar status`, `.nyar api version` (contract §2, api 1),
  and admin-only `.nyar event`, `.nyar spawn`, `.nyar purge`, `.nyar debug here`, `.nyar announce`. Every command
  starts with the `Core.IsReady` guard.
- **Config.** `General.AnnounceEvents` retired (S-8): the key is no longer bound and is ignored. New sections
  `[Limits]` (eight caps, clamped at load), `[Announcements]`, `[Debug]` (`VerboseLogging`, `TimingLog`;
  `FaultInjection` works in Debug builds only).
- **Fixes found in game.** A refusal line showing "< >" lost words to the chat's rich-text parser; it now says
  "angle brackets" (A20). Units spawned in an event's last tick are now included in its cleanup (A17).
- **Tooling.** `tools/preflight.ps1` gained 26 self-tested checks (structural-edit and file-write fences, patch
  guards, pillar and announcement defaults, ready guard, gateway-only mutation, fault injection debug-only,
  secrets), `-LogCheck` over the BepInEx and server logs, `-SessionsOf`, `-AuditOf`, `-Paths`, `-ServerWrites`,
  `-ListCommands`. `tools/ingame/` session helpers for the development world `save-data-nyardev`.
- **Spikes child (tooling only, not shipped).** A throwaway harness answered the engine's open questions
  (marching, marking, save behaviour of spawned units, sweep); its code was removed before foundation.
- Verified on a local dedicated server in 18 recorded sessions (`docs/features/FOUNDATION.md` › Test results).

## [0.1.0] - 2026-09-23

- **Project initialized.** BepInEx IL2CPP server-side scaffold modeled on Faust/Uriel: `Plugin` (server-only
  guard), `Core` (deferred init via `SpawnTeamSystem_OnPersistenceLoad`, coroutine host),
  `EntityExtensions`, `Config/Settings` (master switch, five pillar switches all default **off**, safety
  caps), and a bare `.nyar` root command.
- **Design + research docs.** `docs/NYARLATHOTEP_DESIGN.md`, one design doc per pillar under
  `docs/features/`, `docs/RESEARCH_NOTES.md` (sibling-mod, learning-mod, and Thunderstore findings), and
  `docs/GAME_ASSETS.md` (how to use the prefab dump; faction and key buff GUIDs).
- **Tooling.** `tools/preflight.ps1` release-surface check; local Claude Code hooks (session preflight,
  reference-path guard, release-sync reminder, spawn-safety reminder).
- **Reference data copied in (gitignored).** Prefab dump (23,535 files) and `prefab_names.tsv` from
  Beelzebub; `script_edges.tsv` (boss phase buffs); generated `unit_index.tsv` (533 units with faction,
  level, V Blood flag). Under `Learning Mods/`: Bloodcraft 1.13.24, KindredCommands, VampireCommandFramework,
  DyWorld Rising (README/DLL), and source clones of 11 public Thunderstore mods (BloodyBoss, BloodyCore,
  BloodyEncounters, XPRising, ScarletCore, TideOfWar, SanguineArchives, KindredArenas, RaidForge, HookDOTS,
  NPCs).
