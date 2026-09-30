# Spawning lessons for Uriel's castle spawns

Written 2026-09-29 from Nyarlathotep's world spawning (plans foundation, event-spawns, walkable-spawns and automation;
`docs/DEV_REMINDERS.md` › Spawning). Uriel today spawns objects only, and `ObjectSpawnService.IsNonObject` rejects
units. NPC spawns in castles are an exploratory roadmap item (`docs/ROADMAP.md`, "Triggered NPC spawns", "PvP Arena
boss mode"). These lessons are for when that item becomes work.

## 1. Units are a different path from objects

Keep unit spawns out of `ObjectSpawnService`. A unit has AI, aggro, loot, a lifetime and child entities (ability
groups, buffs). Its failure modes are save pollution, frozen waves and crashed ticks, not overlapping props. Uriel
found that force-spawned units "spawned then immediately vanished". A unit needs the setup below.

## 2. Safety rules

Each rule was learned from a crash or a polluted save. Sources are in `docs/DEV_REMINDERS.md` › Spawning.

### Tracking and cleanup
- **One registry for every spawned unit** (Nyarlathotep's `SpawnTracker`): an in-memory set plus a persistent marker
  that a boot sweep can find after a crash. The marker must be a vanilla component or buff, because the save drops
  custom components. This matches Uriel's own lesson: sweep only entities you marked, never by a heuristic component
  match (the ~2000 plants and 315 nodes).
- **Keep the kill switch working:** one command stops everything and despawns what the mod made.

### Lifetime and saving
- **LifeTime needs Age.** A unit made with `InstantiateEntityImmediate` has no `Age`, so its `LifeTime` never expires,
  across restarts too. Either add `Age`, or spawn through `UnitSpawnerUpdateSystem.SpawnUnit`.
- **Never put `DontSaveEntity` on a unit.** Its child entities are saved without it, and the next boot logs orphan
  errors for several boots.

### Despawning
- **Stage despawns** through a per-tick budget. 36 destroys in one frame crashed Beelzebub.
- **Never destroy twice.** Check `DestroyTag` first, or Burst throws `AppendRemovedComponentRecordError`.

### Spawn calls
- **`SpawnUnit` is async and returns nothing.** Use the KindredCommands duration-key trick, and fix its `return` →
  `continue` bug.
- **Health reads 0 on the spawn frame.** Test "alive" with `Exists()`.

### Setting units up
- **Make units fight:**
  - set `CanPreventDisableWhenNoPlayersInRange.CanDisable = false`, or they freeze with no player near;
  - check `Aggroable`/`AggroConsumer`;
  - clear `DropTableBuffer` against farming;
  - remove `ServantConvertable`/`CharmSource` unless charming is wanted.
- **Never set `Follower.Followed` to a non-player.** It aborted the server twice.

### What not to spawn
- The deny-list is in `docs/GAME_ASSETS.md` › Do-not-spawn: mounts, `CarriagePrisonerRelease*`, `MicroPOI*`, anything
  with `DropInInventoryOnSpawn`.

## 3. Placement: standing is not the same as reaching

Castle spawns will hit what the automation plan hit around ponds and cliffs. Details are in
`docs/features/WALKABLE_SPAWNS.md` and `docs/dod/automation.md` A7–A16.

### Asking the game where a unit can stand
- **The walk check** (`Services/WalkCheck.cs`):
  - `TileMapCollisionMath.CheckStaticCircle(ref map, float2, level, 0.5f, MapCollisionFlags.CollideNormalMovement)`
    for "free";
  - `TileWorld.GetIsGrounded(int2 tile, level)` for "grounded";
  - build the map once per wave with `TileCollisionHelper.CreateMapData`. The first build took 15.5 ms, later ones
    0.1 ms.
- **Tile index:** `floor(v*2)+6400`, the same as Uriel's `ConvertPosToTile`.
- **Height level:** `TileLayerUtility.GetHeightLevel(y)`. Uriel stores `CompressedHeight 0` and keeps height in Y only.
  For units the level matters, because each storey or terrace is its own level.
- **Use the world source, not the tile source.** The singleton tile source read "free" everywhere, water included.
  `GetIsGrounded` alone reads "yes" on water; only the collision circle tells water apart.

### Height levels are not flat
- One level per wave misread slopes. The walk now follows the level: at most one up or down per metre, and two or more
  is a cliff (design §9 D32).
- **Castle risk:** stairs and multi-storey floors will need the same level-following walk, or a unit is placed on a
  floor it cannot reach.

### The spot you stand on can read as blocked
- At a water's edge or a cliff's foot, the 0.5 m circle touches the edge, so the player's exact spot reads blocked.
- The fix starts from walkable ground 1–3 m away (A16).
- **Castle risk:** a player standing against a wall, a door or furniture will read the same way.

### Bridges
- A bridge deck is not ground in the tile map at any level near the player's, yet NPCs walk over it. Some structures
  walk through another system, probably the game's pathfinding or nav mesh.
- **Castle risk:** before trusting the tile map inside castles, verify it for player-built floors, walls, doors and
  stairs.

### Reaching the player
- Standing is not reaching. A unit spawned across water does not path around it and drops its aggro.
- Straight lines from the player fail on curving shores. A flood fill outward from the player over the tile map (the
  next step, option B) or the game's own pathfinding (option C) is the real answer.
- **Castle risk:** inside a castle, walls and closed doors make straight lines nearly useless. Plan for the flood fill,
  or the pathfinding query, from the start.

### Budgets and fallbacks
- **Budget every game call.** Nyarlathotep caps them at 2500 a tick. A verbose-only survey has its own budget.
- **Fall back to something checked, never to nothing.** When no line works, check each spot alone ("spot only"). An
  unchecked spot can be in water.

## 4. How to test it

- **Put game calls behind an interface** (`IWalkProbe`) and test the placement logic with fake terrains: walls,
  islands, slopes, cliffs, a bridge over water. `Nyarlathotep.Tests/SpawnPlacementTests.cs` is the model.
- **Diagnose first.** When an in-game spawn lands wrong, log the reason per unit and a survey before guessing a fix.
  The survey line (`WalkSurvey`) found the root cause in two sessions.
- **Time it.** A spawn tick of five units took 388 ms once. Log the timing split from the first build.
- **Snapshot the dev world** before test sessions and restore it after (`tools/dev-snapshot.ps1`).

## 5. Castle questions to answer before building

- Are player-built floors, walls, doors and stairs in the tile map at the storey's height level?
- Do castle defences, servants or the castle heart react to hostile units spawned inside a territory?
- Does a unit spawned inside a castle keep to the storey it was placed on, and does it path through doors?
- What happens to spawned units when the castle streams out and back in? Uriel's objects get new handles on reload, so
  unit tracking must look units up again rather than cache entity handles.
