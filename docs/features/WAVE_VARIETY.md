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
