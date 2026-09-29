# Walkable spawns — wave units on ground they can walk

**Status:** released in 0.5.1 (docs/dod/walkable-spawns.md, audit docs/audits/walkable-spawns.md). Session 1 chose
the world-metres source; Session 2 put five waves at a pond shore with none in water. The temporary `debug walk` verb
is removed.

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
- **Step 2:** each wave opens one walk check at the height level of its centre (TileLayerUtility.GetHeightLevel of the
  admin's or the Point's y, A13), with its map data made once for the wave. Logic SpawnPoints tries the ring point, the
  11 other angles of the ring, the 12 at half the radius, then the centre; a point is walkable when a 0.5 m circle is
  free in world metres and its tile is grounded. WavePoints keeps one point per unit in ring order under the per-tick
  WalkBudget (2,500 game calls). The wave line gains "(<m> moved, <u> unchecked)" and ", walk h <level>". A Point
  saved without a height is not checked. A failing check (no tile world, a height outside -100 to 1000 m (automation A5), a throw) leaves
  the rest of the wave on its ring points, logs "walk check unavailable: <reason>" once per streak and shows
  "spawns: walk check unavailable" in health until a wave's check answers; the next wave tries again (D5, D6).
  Release 0.5.1 follows Session 2.

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

- Whether the wave's height level (from the centre's y) equals the admin's `debug walk` h at the same spot: Session 2
  compares them (A13).
- Answered by Session 1: the circle test takes world metres (A8); water reads blocked on the world source (S-3).

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
- wall: walk -910.9 -883.0 h 12 r 0.50: blocked grounded yes (singleton world)
- wall: walk -910.9 -882.9 h 12 r 1.00: blocked grounded yes (singleton world)
- pond: walk -939.8 -921.6 h 10 r 0.50: blocked grounded yes (singleton world)
- water: walk -1087.6 -947.1 h 10 r 0.50: blocked grounded yes (singleton world)
- dry: walk -939.8 -904.4 h 10 r 0.50: free grounded yes (singleton tile)
- pond: walk -940.7 -920.8 h 10 r 0.50: free grounded yes (singleton tile)
- cliff: walk -934.4 -889.7 h 10 r 0.50: free grounded yes (singleton tile)
- cliff: walk -934.4 -889.7 h 10 r 1.00: free grounded yes (singleton tile)
- floor: walk -898.2 -927.8 h 11 r 0.50: free grounded yes (singleton tile)
- ledge: walk -915.6 -890.8 h 12 r 0.50: free grounded yes (singleton tile)
- wall: walk -910.9 -883.0 h 12 r 0.50: free grounded yes (singleton tile)
- wall: walk -910.9 -882.9 h 12 r 1.00: free grounded yes (singleton tile)
- pond: walk -939.8 -921.6 h 10 r 0.50: free grounded yes (singleton tile)
- water: walk -1087.6 -947.1 h 10 r 0.50: free grounded yes (singleton tile)
- go/no-go (singleton world): go
- go/no-go (singleton tile): no-go
- Refusals: `.nyar debug walk abc` and `.nyar debug walk 1 2` both replied "radius must be 0.1-5" (D7).
- Chat: every reply showed whole, two lines per walk; readability without colour not yet stated by the owner (D8).
- Reading: the world source separates dry from water and cliff; the tile source reads free everywhere, including the
  pond, so it cannot be go. GetIsGrounded read yes everywhere, water included, so it does not tell water apart. The world
  source reads free inside the building (a floor the admin stands on) and blocked on the ledge the admin stood on.
- Owner decision in plan mode (option A, A12): the interior reading is a floor (recorded, not counted); a building's
  outer wall (label wall) and a second water body (label water) are read next.
- Second part, same server run (A12, review F6): pressed against a map building's outer wall at r 0.50 and r 1.00,
  then at the edge of the first pond again (pond) and of a second pond at about -1087.6, -947.1 (water); a player cannot
  walk into a pond, so every water reading was taken at its edge.
- Verdict: singleton world is go (dry free and grounded; pond, water, cliff and wall blocked); singleton tile is no-go
  (it reads free everywhere). Step 2 uses the world source (A8). The world check is cautious at edges: a pond edge and
  a ledge the admin stood on read blocked, so placement moves units inward, the safe direction.
- D8: every reply showed whole in chat; the walk reply is plain text with no colour markup (Logic/AdminLines.cs WalkReply),
  so it reads the same without colour.
- Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save ws1` before the step 1 DLL was deployed; stopped after the autosave
  that followed the last reading (13:45:43); -LogCheck "0 unhandled, 158 nyar lines, 0 orphan errors, 0 unity errors".
  Then `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (ws1, …
yar-snap-ws1 deleted)".

### Session 2 · 2026-09-28 · waves at a pond shore (owner, 0.5.0+7f02abb, snapshot ws2)
The owner stood on the shore of event-library's Session 7 pond, 3-5 m from the water, where `.nyar debug walk` replied
"walk -919.7 -825.5 h 10 r 0.50: free grounded yes (singleton world)", and started bandit-ambush (radius 10, 6 units a
wave, 3 waves 60 s apart) twice: the first run was stopped after wave 2, the second ran all three waves.
- Wave lines:
  - run 1: wave 1/3 "6 units queued (2 moved, 0 unchecked) … walk h 10"; wave 2/3 "6 units queued (3 moved, 0 unchecked) … walk h 10"
  - run 2: waves 1/3, 2/3 and 3/3 each "6 units queued (3 moved, 0 unchecked) … walk h 10"
- In water: none, in any of the five waves (owner, by sight).
- Height level (A13): the wave's level, from the admin's y, is 10, equal to the `debug walk` h at the same spot.
- Tick timing (Debug.TimingLog) over the waves: avg 0.929, 0.246, 0.269, 0.104, 0.124 ms, all under 5 ms (D4); the
  highest single tick was 24.9 ms, the tick that spawned run 1's first wave.
- Health line during run 2: "nyar health: 1 events, 3 tracked, degraded: none"; no "walk check unavailable" line.
- Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save ws2` before 7f02abb was deployed; stopped after AutoSave 1654,
  which followed the last wave; -LogCheck "0 unhandled, 60 nyar lines, 0 orphan errors, 0 unity errors". Then
  `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (ws2, …nyar-snap-ws2 deleted)".

