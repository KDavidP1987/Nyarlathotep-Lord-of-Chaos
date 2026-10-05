# Wave sets — per-wave units and stats, cleared waves, the event scoreboard

**Status:** step 1 (Logic) in progress (docs/dod/wave-sets.md, audit docs/audits/wave-sets.md). Ships in 0.9.0.

## Goal

A spawn event can have a shape. An optional `waveList` on a SpawnWaves action gives every wave its own units, every
unit entry its own level (or level offset) and stat multipliers, and every wave after the first its own start: after
`afterSeconds`, when the previous wave is cleared, or whichever comes first. When the last wave is beaten (and every
wave was fought), the event ends at once with "all waves defeated". With `scoreboard: true`, the end shows the top 3
players by kills, with their deaths, and the totals.

## Design summary

- **Schema** (D1, D2): `action.waveList` (1-10 waves, 1-10 entries each, `prefab`, `count`, `chance`, optional
  `modifiers`), `afterSeconds` 10-3600 and `whenCleared` on waves 2+; it replaces `units`, `waves`,
  `intervalSeconds` and `modifiers`. `action.scoreboard` is a boolean on either form.
- **Start rule** (design §9 D40): the earlier of the previous wave's decision + `afterSeconds` and the previous wave
  cleared; cleared alone when neither key is set. Cleared = decided and no pending order or tracked unit of that wave
  left. A skipped, zero-rolled or zero-queued wave is cleared at its decision but rules out the early end.
- **Victory** (D41): the last wave cleared and every wave fought → ended as a natural end, "all waves defeated".
- **Scoreboard** (D38, D39): kills of the event's units by players (familiars and summons credit their player),
  deaths of players to the event's units, admins left out unless `[Scoreboard] IncludeAdmins`. Shown at the natural
  end, victory and an admin stop; never on purge, restart, pillar off or a fault. Names only in those chat lines;
  nothing is persisted. At most 200 players get a row (Limits.ScoreboardPlayers, bounding memory): a further player's
  kills and deaths add to the totals, but "<p> players" counts the rows, so it reads 200 at most. Counts are always
  plural ("1 kills"), the form D10 and D21 give.
- **Instances** (A3): a stop and start inside the grace leaves the earlier instance's units tracked; the running
  instance neither waits for them to clear its waves nor credits their kills (units spawned before its start).
- The units form is unchanged.

## Open questions

- Answered in Session 1: a vampire's death to an event unit raises a DeathEvent whose killer resolves to that unit
  ("1 players, 0 kills, 1 deaths"), though the game's death screen says "Killed by: Unknown".
- Why ws-three's units left the ledger in batches with no credit in D23's first runs (A6): the verbose died line now
  names the killer; no unit died in the run that had it on.
- The hunt phase's 176.8 ms tick in D23's run (A7): the cause is not yet named.

## Test results

### Session 1 · 2026-10-04 · wave-sets step 2, into 10-05 (33063d9 + c6deb38, dev world nyardev, Claude via vrclient)

Setup: the step 2 Release build deployed with NyarDevTools (`.devkill`, `.devdie`; A5); `pwsh tools/dev-snapshot.ps1
-Save ws1`; the dev events.json gained ws-three (Manual, 900 s; wave 1 CHAR_Undead_SkeletonSoldier_Withered ×6 at
level 30 and ×6 at levelDelta +3 maxHealth 1.5; waves 2 and 3 ×8, wave 2 afterSeconds 120 whenCleared, wave 3
whenCleared; AroundPlayer 10-20 m, radius 5, Hunt 30, scoreboard true); the cfg gained `[Scoreboard] IncludeAdmins =
true`, with TimingLog on. Three boots, both logs copied before each stop to %TEMP%\nyar-ws1-logs; afterwards
"snapshot restored; hashes equal (ws1, …)".

- [x] D20, waves: "event ws-three wave 1/3 around a player: 12 units queued" at once; `.nyar debug here 60` listed 6
  units at lvl 30 (hp 79) and 6 at lvl 4 (hp 53, base 1 + 3); "event ws-three wave 1 cleared" then the wave 2/3 line
  in the same second, about 20 s after the start, and "wave 2 cleared" then "wave 3/3" likewise
- [x] D20, left alive: wave 1 "due in 930s", wave 2 "due in 810s": 120 s later; the scenario's expect-no-log held 105 s
- [x] D21, scoreboard: "event ws-three ended: all waves defeated (3 of 3 waves)" and "event ws-three scoreboard: 1
  players, 28 kills, 0 deaths" (12 + 8 + 8 units); in chat "Wave set test scoreboard: 1. Chaos 28 kills, 0 deaths" and
  "1 players, 28 kills, 0 deaths" (screenshot wave-sets-ws-scoreboard-222652)
- [x] D21, a death: `.devdie` (the nearest ws-three unit as the killer, 11.6 m) → after `.nyar event stop ws-three`
  "event ws-three scoreboard: 1 players, 0 kills, 1 deaths"
- [x] D21, admins off (boot 2, IncludeAdmins false): every wave killed by Chaos, "all waves defeated (3 of 3 waves)",
  "scoreboard: 0 players, 0 kills, 0 deaths", chat "no player scored" (screenshot wave-sets-boot2-expect-chat-004549)
- [x] D22, stop at wave 2: "event ws-three stopped: 9 units queued", "scoreboard: 0 players, 0 kills, 0 deaths", chat
  "no player scored"; after the grace `.nyar status` "tracked units: 0"
- [x] D22, restart at wave 2: boot 2 logged "event ws-three cancelled by restart" and "boot marker sweep: 8 found, 8
  queued for despawn (8 listed in state.json)", no scoreboard line; `.nyar status` "tracked units: 0"
- [x] D22, purge at wave 2: "purge: 1 events ended, 8 units queued, 0 spawns cancelled, cooldown 240s", no scoreboard
  line; "tracked units: 0"
- [ ] D23, tick budget: not met. The last run (ws-three 1800 s, every entry CHAR_Undead_SkeletonSoldier_Armored_Farbane
  at level 30, at the pond; 20 tracked through two health lines, no unit died) read "timing span: window 10 avg 8.524
  ms, not under 5 ms": one tick's hunt phase took 176.8 ms (an earlier one 30.0 ms) after nine windows under 5 ms (A7)
- Scenarios: wave-sets.vrs PASS 22/22, wave-sets-prep-restart.vrs PASS 6/6, wave-sets-timing.vrs PASS 8/8 (its span
  fails, above); wave-sets-death.vrs 5/6 (an extra `.devgo` step) and wave-sets-boot2.vrs 15/17 (the purge needs
  `.nyar purge` before `confirm`, fixed in the file; the purge was then run by hand, above)
- Findings: the first `.devkill` took every unit with Health and UnitLevel and killed 232 world entities at the castle;
  the world went back to AutoSave_3527, written before it (A5). AroundPlayer found no eligible player at home (castle
  territory) and at the ravine one wave's centre fell in the territory: the runs moved to the pond. At the pond the
  Withered skeletons fell to native units or decayed (20 tracked to 9), so D23's run used armored level-30 units for
  1800 s. `ToggleInvulnerable` takes a Boolean; the owner keeps Chaos invulnerable. Chaos's death dropped two hotbar
  items (slots 5 and 6) at the pond. vrclient's expect-chat can resend the last chat line (a few extra `.devkill`s)

