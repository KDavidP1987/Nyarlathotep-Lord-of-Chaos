# Wave variety — pursuit, unit and boss pools, timed and onslaught templates

**Status:** in progress for 0.13.0 (docs/dod/wave-variety.md, audit docs/audits/wave-variety.md; design §9 D61, Epic
A44). Step 1 (the logic) is being built.

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

## Design summary

To be filled in as step 1 lands.

## Open questions

- Which units of each faction fight beside each other (step 2's mix runs, docs/GAME_ASSETS.md › Faction compatibility).

## Test results

### Session 1 · 2026-10-16 · v0.13.0-rc.2 DLL · vrclient as Chaos on the local dev server

Setup: snapshot wvmix (`pwsh tools/dev-snapshot.ps1 -Save wvmix`, 34 files), `[Debug] VerboseLogging = true`. The mix
runs use only `.nyar spawn` and NyarDevTools, which the running DLL already has.

- **D13** `tools/vrclient/scenarios/faction-mix-wide.vrs` → SCENARIO FAIL 284/337
  (%TEMP%rclientesultsaction-mix-wide-20261016-122616.json). Spiders (7), Church of Light (17), Legion (22),
  Gloomrot (17), cursed (16) and Blackfangs (12) passed every step: no "died (killer CHAR_<group>" line in either phase,
  every unit counted in place. All 53 failures were the undead (30) and militia (23) counts, `[DEV UNITS] n=0`: those
  passes ran longer than `[Limits] ManualSpawnLifetimeSeconds` (300 s), so their units were despawned by lifetime
  ("despawned CHAR_Undead_Assassin of manual") before the counts, with no death. Rerun in Session 2.
- `CHAR_Undead_ZealousCultist_Ghost` "died (killer unknown)" about 10 s after its spawn.
- One warning: "slow tick: 551 ms (spawn queues 548 ms …)" right after the first spawn of `CHAR_Militia_Hound_VBlood`.

### Session 2 · 2026-10-16 · v0.13.0-rc.2 DLL · vrclient as Chaos on the local dev server

Setup: as Session 1, plus `[Limits] ManualSpawnLifetimeSeconds = 1800`.

- **D13** the undead and militia passes of faction-mix-wide.vrs (same generator, `genmixwide.py undead militia`) →
  SCENARIO FAIL 118/119 (%TEMP%rclientesultsaction-mix-wide-rerun-20261016-125738.json). The one failure:
  `CHAR_Undead_ZealousCultist_Ghost` again "died (killer unknown)" seconds after spawning, `.devunits` n=0; recorded
  left out (a timed ghost, not a standalone unit). Every other undead and militia unit and V Blood: no own-group killer
  in either phase, counted in place. The Hound V Blood's slow first spawn did not recur.
- Recorded: docs/GAME_ASSETS.md › Faction compatibility (one row per group, the left-out unit, the slow spawn);
  Logic/FactionGroups.cs grows every group and adds blackfangs (D14, FactionGroupsTests Recorded).

