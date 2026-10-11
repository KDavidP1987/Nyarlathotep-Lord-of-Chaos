# Swarm — a contained arena of growing waves with a kill-to-death winner

**Status:** in progress (target 0.10.0). Step 1 (logic: keys, arena rule, growth, winner, chat) is being built
(docs/dod/swarm.md, audit docs/audits/swarm.md).

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

- Whether the game's own leash (AggroConsumer.MaxDistanceFromPreCombatPosition) holds our units well enough that
  the 5 s return rarely fires; Session 1's first probe decides (plan step 2).

## Test results

None yet; Session 1 is plan step 2.
