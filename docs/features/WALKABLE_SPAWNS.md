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
  `walk <x> <z> h <heightLevel> r <radius>: <free|blocked> grounded <yes|no> (<source>)`. The source is the
  server's live tile world (`singleton`) and the coordinate space of the circle test (`world` metres or the `tile`
  grid, x·2 + 6400), since the game's metadata does not say which one it takes (A8); grounded is read once, in tile
  space, for both lines. A missing singleton or a failing read adds `walk check unavailable: <reason>`; XPRising's
  empty TileWorld is never read, since a default struct could fault in native code (A10).
- **Step 2 (if Session 1 is go):** Logic SpawnPoints and WavePoints, the per-tick WalkBudget, the moved and unchecked
  counts on the wave line, the health entry "spawns: walk check unavailable", and release 0.5.1.

## Test plan

- **Session 1 (owner):** `.nyar debug walk` on dry open ground (dry), in a pond (pond), in a second water body such as
  the pond of event-library's Session 7 at about -912.9, -828.8 or a river (water), against a cliff face (cliff), against
  a building's outer wall (wall), on a building's floor (floor) and on a cliff-top ledge (ledge). Per source, go when
  every dry reading is free and grounded and every pond, water, cliff and wall reading is blocked; floor and ledge are
  recorded but not counted (A12). Also `.nyar debug walk abc` and
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

### Session 1 · 2026-09-28 · walk probe (owner, 0.5.0+37b1ea8, snapshot ws1)
The first attempt on 2536488 replied "walk check unavailable: singleton: none" to every `.nyar debug walk`; A11 (defect)
fixed the singleton lookup and the session went on at 37b1ea8. Labels follow the step order the owner ran (dry, pond, cliff
twice with `walk` and `walk 1`, inside a building (label floor since A12), ledge); the owner found no river. The pond reading was taken at the owner's pond,
not at the Session 7 point (-912.9, -828.8).
- dry: walk -939.8 -904.4 h 10 r 0.50: free grounded yes (singleton world)
- pond: walk -940.7 -920.8 h 10 r 0.50: blocked grounded yes (singleton world)
- cliff: walk -934.4 -889.7 h 10 r 0.50: blocked grounded yes (singleton world)
- cliff: walk -934.4 -889.7 h 10 r 1.00: blocked grounded yes (singleton world)
- floor: walk -898.2 -927.8 h 11 r 0.50: free grounded yes (singleton world)
- ledge: walk -915.6 -890.8 h 12 r 0.50: blocked grounded yes (singleton world)
- dry: walk -939.8 -904.4 h 10 r 0.50: free grounded yes (singleton tile)
- pond: walk -940.7 -920.8 h 10 r 0.50: free grounded yes (singleton tile)
- cliff: walk -934.4 -889.7 h 10 r 0.50: free grounded yes (singleton tile)
- cliff: walk -934.4 -889.7 h 10 r 1.00: free grounded yes (singleton tile)
- floor: walk -898.2 -927.8 h 11 r 0.50: free grounded yes (singleton tile)
- ledge: walk -915.6 -890.8 h 12 r 0.50: free grounded yes (singleton tile)
- go/no-go (singleton world): incomplete
- go/no-go (singleton tile): incomplete
- Refusals: `.nyar debug walk abc` and `.nyar debug walk 1 2` both replied "radius must be 0.1-5" (D7).
- Chat: every reply showed whole, two lines per walk; readability without colour not yet stated by the owner (D8).
- Reading: the world source separates dry from water and cliff; the tile source reads free everywhere, including the
  pond, so it cannot be go. GetIsGrounded read yes everywhere, water included, so it does not tell water apart. The world
  source reads free inside the building (a floor the admin stands on) and blocked on the ledge the admin stood on.
- Owner decision in plan mode (option A, A12): the interior reading is a floor (recorded, not counted); a building's
  outer wall (label wall) and a second water body (label water) are read next.
