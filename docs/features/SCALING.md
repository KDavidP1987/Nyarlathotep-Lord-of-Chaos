# Scaling — wave and horde sizes and levels follow the players in scope

**Status:** building (docs/dod/scaling.md, in progress; design §9 D52, D55, D57, owner 2026-10-14), on the git branch
`scaling` until 0.13.0 is tagged. Step 1 (the gear spike) done, waiting on the owner's acceptance of D19; step 2 (Logic) and step 3 (the Services
wiring: the reads, the guard with its "scaling: player read failing" health entry, Debug.FaultInjection
`scaleplayers:<n>@<gear>` in a Debug build) built; step 4 (the vrclient sessions) next. Planned release 0.14.0, the first 1.0 child.

## Goal

Every count and level of an event is fixed in events.json today, so an event tuned for four level-80 players swamps one
level-40 player and bores a full group. With an opt-in `scaling` block, a wave, hunting ground, revenge, swarm growth,
apocalypse horde or roamer top-up grows with the number of players in the event's scope and takes their gear level.

## The block (design §9 D57)

```json
"scaling": { "unitsPerPlayer": 2, "levelFromPlayers": true, "levelOffset": 2, "levelPerWave": 1 }
```

On a SpawnWaves action (units form, waveList, growth, any location and trigger) or an Apocalypse action. Without it the
event runs exactly as before. Set from chat with `.nyar event set <id> action.scaling.<field> <value>`; removed with
`.nyar event set <id> action.scaling none`.

| Key | Range | Does |
|---|---|---|
| unitsPerPlayer | 0-5 | extra units of each entry per counted player beyond the first |
| levelFromPlayers | true/false | level = the counted players' average gear level + levelOffset |
| levelOffset | -20..20 | added to the average |
| levelPerWave | 0-5 | added per wave after the first |

## The rules

- **Counted players:** online, alive, inside the event's action scope (every online player for a Global event); admins
  only when [Scoreboard] IncludeAdmins is true; read again before each wave, fanOut group, horde and top-up.
- **Counts:** count + unitsPerPlayer × (players − 1) per entry, then MaxUnitsPerWave and the free MaxTrackedUnits slots
  cut it: regular units are cut first, bosses are kept. A fanOut group counts the players near its own player. Bosses are never
  multiplied.
- **Level:** each player's gear level is the Equipment sum (armor + spell + weapon) as the character screen shows it
  (the rounding is the spike's verdict); the average is rounded half up; the level is clamped 1-120. An absolute `level` the
  admin set on an entry, a units-form action or a boss stays; a V Blood keeps its own level.
- **Failure:** a failed player read falls back to the configured counts and levels, never to more units.
- Advice for admins: a player-aimed event (revenge, AroundPlayer) on a Global scope counts every online player; give it a
  regional scope to scale only to the players nearby.

## Open questions

- None blocking. The gear rounding is settled as far as the spike could see (all readings whole numbers; S-6's floor kept).

## Test results

### Spike · 2026-10-14 · step 1 (the mod at 0f90351 code + NyarDevTools .devgear) · vrclient as Chaos on the local dev server

tools/vrclient/scenarios/scaling-spike.vrs: **SCENARIO PASS 14/14** (results scaling-spike-20261014-184257.json; the first run
was 12/14 on the scenario's one-step purge, fixed to the two-step purge). Snapshot sc0 before, restored after (hashes equal);
logs in %TEMP%/nyar-sc0-logs; -LogCheck 0 unhandled; only the known boot warnings.

| Check | Result |
|---|---|
| (a) Equipment sum vs the character screen's "Gear Level" | 5 of 5 equal: by hand 30 (all armour broken), 39 (one lvl-9 piece equipped), 48 (two); in the scenario 48, then 39 after a right click took the chest piece off (shots sc-gear-before, sc-gear-after). `GetFullLevel()` equals the sum each time; a broken piece counts 0 |
| (b) User.IsAdmin for Chaos | true |
| (c) V Blood flag | CHAR_Bandit_Foreman_VBlood vblood=yes (lvl 20); CHAR_Bandit_Thug vblood=no (lvl 16) |
| (d) cost of one player read | see the re-run below (the first run timed the Equipment read alone: 0.006-0.008 ms) |

gear verdict: floor (S-6 kept; every reading was a whole number, so floor and round agreed and no method note is needed)

Re-run after the Codex cross-inspection (Codex asked for the whole player read in (d) and guarded unit reads in
`.devgear`): **SCENARIO PASS 14/14** (scaling-spike-20261014-185455.json), sums 48 then 39 again with the screen. One player
read as the mod will make it (User and its admin flag, the character, its position, its Equipment), timed by a Stopwatch:
3.81 ms on the first call after boot (JIT and first touch), 0.027 ms after. Debug.TimingLog was not used: it times whole
ticks, and one read is far below its resolution; D22's timing run measures the mod's real read inside its tick.
Snapshot sc1 before, restored after; -LogCheck 0 unhandled; only the known boot warnings. The dev snapshot holds the
BepInEx trees, not the world save: Chaos keeps the two working Dracula's Grim pieces (chest, leggings) the spike put on in
place of the broken Dread pieces, which cannot be put back on (gear 48 instead of 30).
