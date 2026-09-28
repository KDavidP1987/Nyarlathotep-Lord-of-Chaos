# Walkable spawns — wave units on ground they can walk

**Status:** in build (docs/dod/walkable-spawns.md, audit docs/audits/walkable-spawns.md); step 1 of 2 (the walk probe)
built, Session 1 (the owner's six readings) next. Nothing of it ships yet; 0.5.0 is the current release.

## Goal

0.5.0 places a wave's units on a ring around the event's centre. A ring point in a pond or a river leaves a unit
standing in water until its lifetime ends (the 0.5.0 known issue, event-library Session 7). 0.5.1 checks each ring
point against the game's own static tile collision and moves a blocked point to the nearest walkable one on the same
ring, then half the ring, then the centre, without adding, dropping or reordering a unit.

## What ships

- **Step 1 (temporary, removed in step 2):** `.nyar debug walk [radius]`, a verb of the admin-only `debug` command.
  Radius 0.1–5 m, default 0.5. It reads the tile collision at the admin's position and replies one line per source:
  `walk <x> <z> h <heightLevel> r <radius>: <free|blocked> grounded <yes|no> (<source>)`. The source names the
  tile world (`singleton`, the server's live one; `empty`, XPRising's construction, read only when the singleton
  is missing) and the coordinate space (`world` metres or the `tile` grid, floor(x·2) + 6400), since the game's
  metadata does not say which one the calls take (A8). A source that fails adds `walk check unavailable: <reason>`.
- **Step 2 (if Session 1 is go):** Logic SpawnPoints and WavePoints, the per-tick WalkBudget, the moved and unchecked
  counts on the wave line, the health entry "spawns: walk check unavailable", and release 0.5.1.

## Test plan

- **Session 1 (owner):** `.nyar debug walk` at six places: dry open ground, the pond of event-library's Session 7
  (about -912.9, -828.8), a river, against a cliff face, inside a building, and on a cliff-top ledge. Each reply is
  recorded under its label. Per source, go when every dry reading is free and grounded and every pond, river, cliff
  and building reading is blocked; the ledge is recorded but not counted. Also `.nyar debug walk abc` and
  `.nyar debug walk 1 2` (the refusal line), and whether each line shows whole in chat without colour (D8).
- **Session 2 (owner):** bandit-ambush centred on the pond shore, radius 10, three times: no unit in water, moved > 0
  on each wave line, the tick timing under 5 ms.

`pwsh tools/preflight.ps1 -SessionsOf walkable-spawns` recomputes Session 1's go/no-go from the readings (D10).

## Open questions

- Which coordinate space the tile calls take (A8): Session 1 answers it.
- Whether the live tile world covers water as blocked for normal movement: Session 1 answers it (S-3).

## Test results

### 2026-09-28 · step 1 · unit tests
- CommandArgTests WalkRadius (13 cases) and ChatBytes: pass; 1351 tests in all.
- preflight -SelfTest: SessionLogs good (with a probe record) passes; bad-probe to bad-probe-4 fail with the planted
  reason.
