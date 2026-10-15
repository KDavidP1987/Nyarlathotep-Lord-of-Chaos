# Scaling — wave and horde sizes and levels follow the players in scope

**Status:** building (docs/dod/scaling.md, in progress; design §9 D52, D55, D57, owner 2026-10-14), on the git branch
`scaling` until 0.13.0 is tagged. Step 1 (the gear spike) done, waiting on the owner's acceptance of D19; step 2 (Logic) and step 3 (the Services
wiring: the reads, the guard with its "scaling: player read failing" health entry, Debug.FaultInjection
`scaleplayers:<n>@<gear>` in a Debug build) built; step 4 (the vrclient sessions) done (Sessions 1 and 2 and the timing run passed; D20, D21 wait on the owner, D22 10/10); step 5 (real players, the owner) next. Planned release 0.14.0, the first 1.0 child.

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

## Owner test sheet · step 5 (D23, real players)

Needs at least two real players. Build: the Release DLL of the step 4 commit (642d376) on the dev server or your own
server; tools/vrclient/fixtures/sc-wave.json in events.json, enabled. Takes about 15 minutes.

1. Connect to 127.0.0.1:9876 (Direct Connect, or your own server's address). Both players stand together at the pond:
   `.devtp -1880 -1640` (dev server), or anywhere in one region on your server after `.nyar event set sc-wave action.location here`.
2. `.nyar event info sc-wave` → a line "scaling: +2 units a player beyond the first, level players' gear +2, +1 a wave".
3. `.nyar event start sc-wave`, then `.nyar event info sc-wave` → "scaling now: <n> players, gear <A>, level <L>".
   **PASS:** <n> is the number of non-admin players in the event's scope (admins count only with [Scoreboard] IncludeAdmins = true);
   wave 1 has 4 + 2 × (<n> − 1) bandit thugs at level <L> = the average of your character screens' Gear Level + 2.
4. Kill wave 1. Wave 2 comes with Rufus the Foreman. **PASS:** the thugs are one level higher; Rufus is level 20.
5. One player logs out (sc-wave has no scope, so it counts every online player wherever they stand); stop with `.nyar event stop sc-wave` and start it again.
   **PASS:** "scaling now" counts one player fewer and wave 1 is 2 thugs smaller.
6. Your verdict: are the size and the level fair for your group? Anything to change becomes an amendment before it is built.

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

### Session 1 · 2026-10-14 · step 4 (branch scaling at 0421b64, Release DLL md5 52205b02…) · vrclient as Chaos (gear 48)

sc-wave (tools/vrclient/fixtures/sc-wave.json) at the pond (-1880, -1640). Snapshot sc4 before; logs in %TEMP%/nyar-sc4-logs.

| Run | Result | What the log and chat showed |
|---|---|---|
| scaling-admins-off.vrs, IncludeAdmins false | **SCENARIO PASS 10/10** (scaling-admins-off-20261014-203139.json) | "scaling sc-wave wave 1: players 0, gear none, level own, units 4 -> 4"; thugs at their prefab lvl 16; `event info` "scaling now: 0 players, gear none, level own" (shot sc-admins-off) |
| scaling.vrs, IncludeAdmins true | **SCENARIO PASS 19/19** (scaling-20261014-203653.json) | `.devgear` 48; "wave 1: players 1, gear 48, level 50, units 4 -> 4", thugs lvl 50 (hp 121, pp 34: the game scales their stats with the level); after `.devkill` "wave 2: players 1, gear 48, level 51, units 5 -> 5", thugs lvl 51, Rufus the Foreman (V Blood) at his own lvl 20; `event info` shows both scaling lines (shot sc-info-running); `.nyar event set sc-wave action.scaling.unitsPerPlayer 6` replied "action.scaling.unitsPerPlayer must be 0-5" in real chat (shot sc-set-refused; the run checked the word unitsPerPlayer, the scenario now checks the whole reply); after the stop no "scaling now" line and 0 tracked |

Both boots: no [Error line; the only warnings are the known boot warnings and the stop's "units queued" line; the
Unity log's 226 missing-prefab lines are the same as the boot before the change. D20 is ready for the owner's acceptance.

### Session 2 · 2026-10-14 · step 4 (branch scaling at 0421b64, Debug DLL md5 240cc002…) · D21

[Debug] FaultInjection `scaleplayers:3@40` (three phantom counted players of gear 40 beside Chaos, Debug builds only),
IncludeAdmins true, [Limits] MaxUnitsPerWave 8. scaling-phantoms.vrs: **SCENARIO PASS 22/22**
(scaling-phantoms-20261014-211254.json; the first run was 21/22 on the script asking `.nyar status` once while the
apocalypse stop's staged despawns were still running, "6 (despawning 6)"; a 30 s wait was added as in the other scenarios).

| Check | Log |
|---|---|
| phantoms placed | "scaling phantoms: 3 of 3 at gear 40" |
| sc-wave | "scaling sc-wave wave 1: players 4, gear 42, level 44, units 4 -> 10" (round((48 + 120) / 4) = 42, + 2), then "clamped by MaxUnitsPerWave: 10 -> 8"; `.nyar debug here 40` 8 thugs at lvl 44; 8 tracked; `event info` "scaling now: 4 players, gear 42, level 44" (shot sc-phantoms-wave) |
| sc-region (Region location, FarbaneWoods) | "scaling sc-region wave 1: players 4, gear 42, level 44, units 4 -> 10" logged before "wave 1/1 in 2 groups over FarbaneWoods: 8 units queued" (clamped 10 -> 8) |
| apo-test with the block (fixtures/sc-apo.json) | "scaling apo-test roamers: players 4, per hotspot 9, max 24" (3 + 6) at the start; a minute in "scaling apo-test horde 1: players 4, gear 42, level 44, units 8 -> 14" (size 8 + 6) |

No [Error line; warnings only the stop lines and the two cap lines; `-LogCheck`: 0 unhandled, 0 orphan errors, 0 unity
errors. Snapshot sc4 restored after (hashes equal; the cfg, events.json and the deployed DLL back as before the session).
D21 is ready for the owner's acceptance.

### Session 3 · 2026-10-14 · step 4, the timing run (Release DLL md5 52205b02…) · D22

scaling-timing.vrs: **SCENARIO PASS 25/25** (scaling-timing-20261014-210128.json). sc-timing-a..d (fixtures/sc-timing-*.json:
12 then 6 CHAR_Spider_Forest each, scaled) and apo-test from fixtures/sc-apo-timing.json (3 hotspots × 10 roamers, horde of
24, scaled) together; 126 tracked; the log copy holds a scaling line for each of the eight waves (players 1, gear 48, level 50, wave 2
level 51), for the roamers (per hotspot 10, max 30; the line is written when the count changes) and for horde 1 (units 24 -> 24). `pwsh tools/preflight.ps1 -TimingSpan <%TEMP%/nyar-sc4-logs/d-LogOutput.log> -Windows 10 -MinTracked 50`:
**timing span: 10/10 windows under 5 ms, tracked >= 50, targets >= 1, 0 slow ticks**. [UnitWatch] StallMinutes 30 and
[Limits] MaxUnitsPerWave 24 for this run only. No [Error line; warnings only the five stop lines.
