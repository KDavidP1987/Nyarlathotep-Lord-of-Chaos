# Swarm — a contained arena of growing waves with a kill-to-death winner

**Status:** 0.10.0 (docs/dod/swarm.md, audit docs/audits/swarm.md). The owner's Session 3 (2026-10-12) passed the
arena return and the look and feel. Region-bound placement modes, which the owner asked for after that run, are
planned separately (design §9).

## Goal

A spawn event can be held in one place and can grow by itself. Three optional keys on a SpawnWaves action:

- `arena { radius }` keeps the event's own units inside a circle around a Point location (or around the admin who
  started an Admin-location event). A unit more than radius + 10 m out is returned near the centre every 5 s.
- `growth { addUnits, addLevels, whenCleared }` turns one units-form base wave into `waves` waves, each bigger and
  stronger than the one before. The generated list is an ordinary 0.9.0 wave list.
- `winner: "KillDeath"` (needs `scoreboard: true`) adds one chat line at the end naming the best kill-to-death ratio.

The `spider-swarm` template uses all three (design §9 D33, D42).

## Design summary

- **Arena** (D1, D3): radius 15-60 m; refused on AroundPlayer, with a spawn radius or Hunt range wider than the
  arena. Only units of the running instance are ever moved (event id and spawned at or after the instance start).
- **Growth** (D24, D25): wave n = each base entry with count + (n−1)·addUnits and level + (n−1)·addLevels (absolute
  levels capped at 120); waves 2-n start `intervalSeconds` after the previous one, or at its clear as well with
  whenCleared. The last wave may hold at most 50 units; the caps clamp at spawn as for any wave.
- **Winner** (D5, D6): over every scoreboard row, players with a kill, highest kills ÷ max(1, deaths), then more
  kills, fewer deaths, earlier first credit. Shown only where the scoreboard shows; the name appears only in that
  chat line.

## Open questions

- Resolved 2026-10-12 (Session 3): the arena return was seen in game. A player walked out, the spiders followed,
  "[nyar] arena sw-arena: returned 4, farthest 46 m" was logged, and the owner judged the run good.

## Test results

### Session 1 · 2026-10-11 · swarm step 2 (0865d32, dev world nyardev, Claude via vrclient)

Setup: `pwsh tools/dev-snapshot.ps1 -Save sw1` first; the Release build of 0865d32 deployed; the dev cfg with
Scoreboard.IncludeAdmins true (false for boot 3) and Debug.TimingLog on; the dev events.json gained sw-arena and
sw-timing (the entries in swarm.vrs and swarm-timing.vrs); dev marks pond (-1880, -1640), sw-near (+10 m east),
sw-far (+60 m east) and sw-far2 (60 m west). Chaos invulnerable throughout (`ToggleInvulnerable true`). Three boots;
both logs copied to %TEMP%
yar-sw1-logsoot<n> before each stop.
Restored after: "snapshot restored; hashes equal (sw1, …)".

- D15, kept inside (partly): sw-arena at the pond spawned its 8 CHAR_Spider_Forest at the spot (`.nyar debug here 8`:
  all at -1880,-1640). With Chaos teleported 60 m east and then 60 m west, `.nyar debug here 25` read "no tracked
  units within 25 m" at 10 s and 30 s. Back at the spot, `.nyar debug here 35` listed all 8 and `.nyar status` read
  "tracked units: 8". No "[nyar] arena" line in any boot. The units never moved: they didn't engage the invulnerable
  Chaos, so the game's leash and our return were never tested. The 10 m combat check wasn't observed either (the
  sw-arena-near screenshots show no fight). Open; see Session 2 and D18.
- D16, waves and winner: spider-swarm from `.nyar template use spider-swarm` (event info listed "growth: +1 units per
  entry and +3 levels per wave, every 120s or when cleared"). Wave lines "wave 1/5: 6 units", "2/5: 9", "3/5: 12",
  "4/5: 15", "5/5: 18", on the 120 s timer (boot 1) and on clears (boot 2). Levels read by `.nyar debug here`:
  wave 1 at 30, wave 2 at 33, wave 5 at 42. Wave 3's 36 wasn't read directly (the scenario's "lvl 36" check
  matched a native StoneGolem), but it follows from the same steps. All 60 killed with `.devkill`:
  "event spider-swarm ended: all waves defeated (5 of 5 waves)", "scoreboard: 1 players, 60 kills, 0 deaths, winner
  K/D 60.00" (swarm-waves 29/31; its two misses are chat OCR with the chat panel hidden).
  Death run (swarm-death 7/8): `.devdie` → "[DEV DIE] found=1"; stopped at wave 3: "1 players, 15 kills, 1 deaths,
  winner K/D 15.00", r = kills ÷ 1. The scenario expected 6 kills, but the double `.devkill` also cleared wave 2.
  Rendered in chat (vrclient shot chat-history-211536): "Spider swarm scoreboard: 1. Chaos 15 kills, 1 deaths",
  "Spider swarm winner: Chaos, K/D 15.00 (15 kills, 1 deaths)". IncludeAdmins false (boot 3, swarm-noadmins 17/18):
  all 5 waves defeated, "scoreboard: 0 players, 0 kills, 0 deaths, no winner", no "winner:" line. Its one miss is the
  "no player scored" chat line, which OCR couldn't read with the chat hidden; that line is covered by
  ScoreboardTests WinnerLines' empty case.
- D17, end paths: `.nyar event stop` at wave 3 (boot 2) → the scoreboard with "winner K/D 15.00", then "tracked
  units: 0". `.nyar purge confirm` at wave 1 (boot 1, swarm-purge.vrs in boot 3 at wave 3, 9/9) → "purge: 1 events
  ended, 12 units queued, 0 spawns cancelled, cooldown 240s", no scoreboard or winner line, "tracked units: 0".
  Restart at wave 3 (swarm-prep-restart 6/6, 12 tracked), boot 3: "event spider-swarm cancelled by restart (it was due
  to end …)", "boot marker sweep: 12 found, 12 queued for despawn (12 listed in state.json)", no scoreboard line.
  No "[nyar] arena" line anywhere. The plan says wave 2; waves 1 and 3 run the same end paths.
- D19, tick budget (boot 3, swarm-timing 13/13): `pwsh tools/preflight.ps1 -TimingSpan <boot 3 LogOutput copy>
  -Windows 10 -MinTracked 10` → "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks";
  the arena phase 0.1-1.7 ms at its slowest ticks.
- Incident: after `.devdie`, Chaos respawned at the Wooden Coffin in daylight and died to the sun once more before
  `.devgo pond` and `ToggleInvulnerable true` recovered it. The world save keeps both deaths: a death container stands
  at the pond. The snapshot covers only BepInEx/plugins and config.
- Logs: -LogCheck per boot: boot 1 "0 unhandled, 214 nyar lines, 0 orphan errors, 0 unity errors", boot 2 "0
  unhandled, 171 nyar lines, …", boot 3 "0 unhandled, 201 nyar lines, 0 orphan errors, 0 unity errors, regions 10
  polygons". [Warning] lines: Il2CppInterop's substitute notice, Beelzebub's TUNE line, example-empowerment's reason,
  the stop and purge lines; 0 [Error]. The server log's 226 "unknown state" prefab warnings per boot come from the
  world load, the same count as before.
- Tooling: boot 1's NyarDevTools.dll was an old build without `.devkill`/`.devdie` (an earlier snapshot restore had
  put it back); rebuilt and redeployed before boot 2. vrclient's launch now waits 10 s so PLAY can't land on Continue.

### Session 2 · 2026-10-11 · D15 chase probe (0865d32, dev world nyardev, Claude via vrclient)

Setup: `pwsh tools/dev-snapshot.ps1 -Save sw2`; the Release build deployed; sw-arena added back to the dev events.json. One boot.
Logs copied to %TEMP%
yar-sw2-logs before the stop. Restored after: "snapshot restored; hashes equal (sw2, …)".

- swarm-chase.vrs SCENARIO PASS 18/18 (results swarm-chase-20261011-220828.json), but the probe failed at its purpose.
  The three held-D legs never moved Chaos ([DEV WHERE] read -1880, -1640 every time, and again after a teleport
  15 m east). The 8 spiders sat stacked under Chaos at full health and never attacked. So no unit left the arena, and
  no "[nyar] arena" line was logged. The stop left "tracked units: 0".
- Finding: vrclient can't walk the character, and an invulnerable character draws no attack. D15's chase, return
  and combat parts need a vulnerable player who walks, which is the owner's D18 run.
- Logs: -LogCheck "0 unhandled, 182 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; [Warning] lines
  the known three plus the stop; 0 [Error].

### Session 3 · 2026-10-12 · owner run, D15 and D18 (c06a928, dev world nyardev, the owner as Chaos)

Setup: the 0.10.0 Release DLL ("templates: 11/11 valid"), `pwsh tools/dev-snapshot.ps1 -Save sw3`, Scoreboard.IncludeAdmins
true, sw-arena in the dev events.json. One boot. The owner connected by Direct Connect to 127.0.0.1:9876.

- sw-arena (D15): `.nyar event set sw-arena location here` saved "Point -1878.9, -1641.1"; `.nyar event start sw-arena`
  queued 8 units (2 moved, 4 shortened). After the owner moved away, the log read "[nyar] arena sw-arena: returned 4,
  farthest 46 m". The owner stopped it: "scoreboard: 0 players, 0 kills, 0 deaths, no winner", 8 units queued for despawn.
- spider-swarm (D16, D18): `.nyar template use spider-swarm`, `event enable`, `event start`. Waves of 6, 9, 12, 15 and 18
  units, each cleared, then "event spider-swarm ended: all waves defeated (5 of 5 waves)" and "scoreboard: 1 players,
  60 kills, 0 deaths, winner K/D 60.00".
- Owner's verdict: "I've completed all of the testing. It went well." That covers the arena return and its feel (D15,
  D18) and the acceptance of the Session 1 evidence (D16, D17).
- Follow-up from the owner: wave and swarm events also need region-bound modes (spawn anywhere in a region and stay in
  it, spawn at a point and spread through the region, ambush only players inside a region, and a map-wide spread for
  apocalypse). Recorded in design §9 and planned as its own child.
- Logs: -LogCheck "0 unhandled, 1774 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons". [Warning] lines:
  the known example-empowerment reason, Beelzebub's TUNE line, Il2CppInterop's Class::Init line, and the sw-arena
  stop. 0 [Error]. NyarDev.log's missing-prefab stacks equal every earlier boot's (226). The server was stopped without
  /F; "snapshot restored; hashes equal (sw3, …)".
