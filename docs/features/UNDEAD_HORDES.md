# Undead hordes — waves that march to a destination, an infestation, a horde march, up to 500 units

**Status:** in progress, step 1 of 5 (docs/dod/undead-hordes.md, audit docs/audits/undead-hordes.md; design §9 D62,
Epic A46). Release target 0.14.0.

## Goal

The demo of 0.13.0 found the zombie apocalypse underwhelming: a few roamers at three graveyards far from the players,
and only one or two reached them. The owner asked for three things:

1. Any wave can be sent from a start to a destination.
2. An undead **infestation**: the dead rise at the graveyards, spread over the map, and every kill adds 1 to 3 more,
   drawn at random from a pool.
3. A **horde march**: one large horde (50 to 500), mostly weak undead with stronger ones and a boss, walks from a
   start to a destination, grows on every kill on the way, and infests the map when it arrives.

Up to 1000 tracked units, once a load test on the local server proves it takes them. The test passed 250 and failed
500, so the ceiling stays at 0.13.1's 500 (owner, 2026-10-17; Load test below).

## Design summary

- **Load ladder first.** 100, 250, 500, 750 and 1000 units, 80 % weak (level 20), 15 % mid (level 40), 5 % strong
  (level 60) and one boss; 10 quiet minutes and 5 fight minutes per step; bars: the mod's tick under 5 ms a window, the
  server's frame interval average under 36 ms (28 of its 30 frames a second) and p95 under 50 ms. The ceiling is the
  highest step that passes, never below 500 without the owner. Result: 500 (Load test).
- **Marching** reuses the apocalypse's hidden march: a front holds while a player is within 60 m; stragglers are moved
  only when nobody is within 100 m.
- **Graveyards** are the hotspots of the faction's native NPCs (the apocalypse's hotspot pick).
- **Hostility** is the game's own: nothing writes a unit's faction.
- **Caps** stay modest by default (150 tracked, 20 a wave); a cut names the cfg key to raise.

## Open questions

- None open. Session 0 settled the hostility question (Hostility below).

## Load test

Session 0 results, one row per step. Bars as in the Design summary.

| Step | Spawn s | Tick avg / max ms (quiet) | Tick avg / max ms (fight) | Slow ticks | Frame avg / p95 ms | Memory MB | Drain s | Result |
|---|---|---|---|---|---|---|---|---|
| 100 | 13 | 0.29 / 3.2 | 0.31 / 2.5 | 0 | 33.6 / 33.4 | 3224 | 11 | pass |
| 250 | 22 | 0.50 / 3.6 | 0.64 / 5.1 | 0 | 33.6 / 33.4 | 3220 | 18 | pass |
| 500 | 31 | 0.93 / 7.0 | 1.21 / 9.2 | 0 | 39.3 / 86.1 | 3218 | 25 | fail: fight frame window 1 avg 51.7 ms |

load ladder: 100 pass, 250 pass, 500 fail (fight frame window 1 avg 51.7 ms) · ceiling 250

- **500 units.** Quiet, the server held its 30 fps (33.5 ms frames). With Chaos among them every fight minute ran at
  about 19 fps (51-52 ms average, 82-86 ms p95) while the mod's tick stayed at 1.2 ms: the cost is the game's own
  simulation of 500 units around a player. The ladder stopped there; 750 and 1000 were not run.
- **Ceiling.** The owner kept MaxTrackedUnits at 0.13.1's 500 rather than lower it to 250 (plan A3). An admin who sets
  more than 250 should expect the frame rate to drop when players meet a crowd that size.
- **Reruns.** Step 100 ran three times: the unit watch's stall rule cleared the held units after 10 minutes (fixed for
  the ladder with StallMinutes 60, which needed a restart; plan A1, A2). Step 250 ran twice: the first run had one
  10.1 s frame at nightfall (quiet window average 41 ms); the rerun crossed a nightfall without one. Its cause is
  unknown; the frame meter now logs any frame over 1 s, and none recurred.

## Hostility

Session 0 (tools/vrclient/scenarios/hostility.vrs, `load-ladder.py report`): 5 of each native spawned 10 m from 10
CHAR_Undead_SkeletonSoldier_Armored_Farbane at level 20; deaths in 60 s from the mod's "died (killer …)" lines. Every
native probed fights the undead, so the spread test can use any of them (a wild bear joined in as well).

| Native | Fights the undead | Deaths in 60 s |
|---|---|---|
| CHAR_Bandit_Thug | yes | 1 undead killed by it, 5 killed by the undead |
| CHAR_Militia_Light | yes | 10 undead killed by it, 3 killed by the undead |
| CHAR_Forest_Wolf | yes | 0 undead killed by it, 5 killed by the undead |
| CHAR_Legion_Shadowkin_Lesser | yes | 10 undead killed by it, 2 killed by the undead |

## Test results

### Session 0 · 2026-10-17 · step 1 Release build (version string 0.13.1) · vrclient as Chaos on the local dev server

Setup: snapshot udh0, restored after ("hashes equal"); dev cfg MaxTrackedUnits 1000, MaxUnitsPerWave 500,
MaxSpawnsPerTick 20, MaxDespawnsPerTick 20, [Debug] TimingLog true, [UnitWatch] StallMinutes 60; NyarDevTools with
`.devframes` and `.devnote`. Two restarts in the session (s0a: StallMinutes is read at boot only; s0b: the long-frame
line added), each with a clean `-LogCheck`.

- **D4 hostility** `tools/vrclient/scenarios/hostility.vrs` → SCENARIO PASS 45/45
  (%TEMP%\vrclient\results\hostility-20261017-114557.json); counts in Hostility above.
- **D3 load ladder** `tools/vrclient/scenarios/load-ladder.vrs`, one run per step: 100 → SCENARIO PASS 21/21
  (load-ladder-20261017-130459.json), 250 → PASS 21/21 (…-135731.json), 500 → PASS 21/21 (…-142237.json, the scenario
  ran; the step failed its frame bar). `python tools/vrclient/load-ladder.py report <s0a> <s0b> <live LogOutput.log>` gives
  the rows and the ladder line in Load test above.
- Log check before the stop: 0 unhandled, 423 nyar lines, 0 orphan errors, 0 unity errors. Every [Warning read: the
  boot kinds (Il2CppInterop, HarmonyX), Beelzebub TUNE, the dev example-empowerment line at each reload, the runs' own
  purge lines; no [Error. Unity's log: the 226 game missing-prefab warnings, as at every boot.
