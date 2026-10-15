# Unit lessons for Uriel's castle events

Written 2026-10-15 from Nyarlathotep 0.12.0 (released) and 0.13.0-rc.1 (test build). It replaces the 2026-09-29
spawning-only version of this file.

This is a learning document for a Claude session working on **Uriel** (`C:\dev\projects\Games\V Rising\Uriel Lord of
Oaths`). Nyarlathotep runs exterior events: it spawns NPC units, makes them fight, hunt, stay in an area and leave
cleanly. Uriel wants interior events: units an admin or player spawns inside a castle, for example a pay-to-enter
challenge room that respawns its mobs as the door opens, or a boss arena. This file says what Nyarlathotep learned so
Uriel does not have to learn it again.

## How to read this

Every claim carries one of three marks:

| Mark | Meaning |
|---|---|
| **proven** | Seen in game on the dev server, with the session or scenario named. |
| **unit-tested** | Covered by Nyarlathotep.Tests only; never seen in game. |
| **untested** | Planned, designed or guessed. Treat it as a question. |

Paths are relative to the Nyarlathotep repo (`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`). Code paths
start at `Nyarlathotep/Nyarlathotep/`, shortened here to `src/`. Line numbers are as of 2026-10-15 on `main`. Session
names point at `docs/features/<FEATURE>.md › Test results`; A-numbers are amendments in `docs/dod/<plan>.md`; D-numbers
without a plan are design decisions in `docs/NYARLATHOTEP_DESIGN.md` §9.

Copying Nyarlathotep's own code into Uriel is fine. The mods under `Learning Mods/` are learn-from-only: re-implement,
never paste.

**Almost everything below was proven outdoors.** Nyarlathotep's events skip claimed castle territory by default. Only
one test ever put Nyarlathotep units inside a castle (section 2). Read every section as "what works in the open world"
and check section 11 before relying on it indoors.

## 1. Spawning

### What works (proven)

The recipe lives in `src/Services/SpawnTracker.cs` (Spawn, Prepare, TryTime, TryMark, lines 1029-1168). In order:

1. `ServerGameManager.InstantiateEntityImmediate(Entity.Null, guid)` (line 1037). The guid comes from
   `PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary`. The unit exists in the same frame, so every write
   below happens before the game's systems see it.
2. **Marker first** (foundation A22). A buff entity made with `TryInstantiateBuffEntityImmediate(unit, unit,
   MarkerBuff)` on `AB_Consumable_PhysicalPowerPotion_T01_Buff` (-1954355403), with `SpellLevel.Level = 1314472274`
   ("NYAR"). Its gameplay-event components are stripped, its stat buffer replaced, and its own LifeTime set to 0 /
   None, so the buff lives exactly as long as the unit (lines 1140-1168). The values are in `src/Logic/Markers.cs`.
   The marker is how the boot sweep finds our units (below). It does not depend on a mod component, which the save
   would drop.
3. **LifeTime** with `EndAction = Destroy` and **`Age = 0`** (lines 1079-1095). Without the Age write, LifeTime never
   counts down (spikes A10). Age and LifeTime are saved and keep counting across a restart (spikes S2). This is the
   safety net: if the mod is removed, the game itself destroys the units on time. Proven by the uninstall tests
   (event-spawns S2, faction-empowerment S3: all 30 units gone with the DLL absent).
4. `Translation` and `LastTranslation` set to the spot.
5. `DestroyWhenDisabled` added, so the game deletes the unit if it is ever disabled.
6. `DropTableBuffer` cleared unless the event turns loot on.
7. `UnitSetup.Apply` (`src/Services/UnitSetup.cs:17-39`):
   - `CanPreventDisableWhenNoPlayersInRange.CanDisable = false`. Without it, `DestroyWhenDisabled` deletes the unit at
     boot, when players leave, or within 5 s of spawning 100 m from any player (spikes S2). This is very likely what
     Uriel saw as "spawned then immediately vanished".
   - `AggroConsumer.Active = true`.
   - `UnitLevel` written if the event sets a level.
8. Stat multipliers ride on the marker buff, never as direct Health or PhysicalPower writes
   (`UnitSetup.StatModifiers`, lines 46-65: `ModifyUnitStatBuff_DOTS`, `MultiplyBaseAdd`, value = multiplier - 1). The
   game recalculates Health and PP from the level after a spawn, so a direct write is lost (foundation A7: Session 5
   failed, Session 8 read ×1.51 / ×1.21 as asked).
9. A level write makes the game rescale stats from the level (scaling branch, 0.14.0: level-50 thugs read hp 121, pp
   34). **proven** on the branch, not yet released.

`DontSaveEntity` is **never** used (foundation A9). Session 6 tried it and got 120 Unity orphan errors from child
entities. Units save normally; the marker and LifeTime clean them up.

### Boot sweep (proven)

`SpawnTracker.BootSweep` (lines 94-117) queries Buff + SpellLevel with `IncludeDisabled | IncludeSpawnTag` (lines
1176-1206), finds every marker with our level value, and queues its owner for despawn. It found units when the state
file listed none (Session 22) and when the file lagged (automation S2: "20 found, 17 listed"). Typical line: "boot
marker sweep: 31 found, 31 queued". **Use the marker, not your JSON, as the source of truth for what exists.**

For Uriel's persistent rooms the rule differs: a room that should keep its mobs across a restart must not sweep them.
Either give them a second marker value that the sweep leaves alone, or sweep and respawn them on boot from the room's
record. Respawning on boot is simpler and safer (untested in either mod).

### Staged queues, caps, kill switch (proven)

- `SpawnTracker.Tick()` (lines 232-322) drains a spawn queue and a despawn queue under `MaxSpawnsPerTick` and
  `MaxDespawnsPerTick` (for example 10 / 5). Never mass-create or mass-destroy in one frame.
- `DestroySafe` (`src/EntityExtensions.cs:65-71`) skips entities that already have `DestroyTag` and refuses Prefab
  entities. All structural edits go through `AddComponentSafe` / `RemoveComponentSafe` / `AddBufferSafe` (lines
  42-62).
- When you stop an event, push each unit's LifeTime **past** the drain time first (A16, A18, A21; `DrainMargin()` at
  line 171). In Session 15 the game's own LifeTime removed about 90 units a second and bypassed the budget. Fixed and
  proven in Session 16 (30 batches of 5) and Session 20.
- Units the game removed on its own are pruned from the ledger each tick.
- Caps (`src/Config/Settings.cs`; `src/Logic/SpawnLedger.cs` Request 104-114, Confirm 140-146): "clamped by
  MaxUnitsPerWave: 20 -> 5" and "skipped by MaxTrackedUnits" (Session 14).
- Kill switch: `.nyar purge`, then `.nyar purge confirm` within 30 s, with a cooldown. Proven many times.

### Prefabs to refuse (proven)

From `docs/GAME_ASSETS.md`:

- `CHAR_Mount_Horse_Vampire`, `CHAR_Mount_Horse_Gloomrot`
- `CarriagePrisonerRelease*`, `MicroPOI*`
- anything with `DropInInventoryOnSpawn`
- units whose `BehaviourTreeInstance` is Immobile
- `CHAR_Undead_ShadowSoldier` killed its own twin

### What failed

- A dev `.devkill` that took every entity with Health + UnitLevel killed **232 world entities** at a castle
  (wave-sets Session 1, A5). The world was restored from autosave. Indoors this is worse: servants, prisoners and
  structures with Health are all around. Only touch entities you marked.

### Timing (proven)

- The first spawn and first hunt tick after a boot cost 30-206 ms (interop warm-up, likely, not proven).
- One wave start took 172.7 ms (spawn queues 105 ms).
- Steady state is under 5 ms with 150 tracked; 500 units averaged about 2.86 ms a tick.

### Safety rule

Every spawned unit carries the marker, a counting LifeTime, `DestroyWhenDisabled` plus `CanDisable = false`, and is
in your ledger. Spawns and despawns go through budgeted queues. One command removes everything.

### Copy from

`src/Services/SpawnTracker.cs:1029-1206` (spawn + marker + sweep query), `src/Services/UnitSetup.cs`,
`src/Logic/Markers.cs`, `src/EntityExtensions.cs:42-71`, `src/Logic/SpawnLedger.cs`.

## 2. Placement indoors and in castles

### What works outdoors (proven)

`src/Services/WalkCheck.cs`:

- `TileCollisionHelper.CreateMapData` once per wave (line 120). The live singleton lookup needs
  `EntityQueryOptions.IncludeSystems` (A11, lines 152-176). Check `Created()` on native containers before use.
- `TileMapCollisionMath.CheckStaticCircle(ref map, float2, level, 0.5f, CollideNormalMovement)` (line 126): is a
  0.5 m circle free at this height level.
- `TileWorld.GetIsGrounded(int2, level)` (line 132): is there ground.
- Tile index `floor(v * 2) + 6400` (line 144). A negative index throws, so guard it (a native fault otherwise).
- `TileLayerUtility.GetHeightLevel` and `GetHeightForLevel` convert between y and height levels.

Placement logic (`WavePoints.Plan`, `src/Logic/Spawning.cs:345-370`): try the ring point, then 11 other angles, then half
the radius, then the centre. A spot is accepted only if a straight line from the centre to it is walkable, sampled
every 1 m (A7, D31). A16 falls back to "spot only" when no line works. Walk budget: 2500 calls a tick; one call costs
about 0.4 ms in game, so the hunting-grounds plan caps 200 calls a check (A3).

### What failed outdoors (proven)

- The tile source alone reads "free" everywhere, and `GetIsGrounded` says yes on water (walkable-spawns Session 1).
  Use the world source.
- Ponds and bridges: units placed across water stood idle (automation 1b, 1c). A strip inside a pond had every line
  blocked (A17); a flood fill is planned, not built. Bridge decks are not ground in the tile map.
- Hillsides need ±2 or 3 levels tried (hunting-grounds A1).
- At standing units the walk check gave 3 false off-ground readings in 7 (unit-watch spike). The game's height
  correction lifts a sunk unit back within a frame, so "off ground" is only trusted more than 10 m below the lowest
  grounded level.

### Inside buildings (partly proven)

- The walk-check readings inside a **world** building: floor at height 11 read free; the building's outer wall read
  blocked (walkable-spawns Session 1). **proven**.
- **Castle-built floors, walls, doors and stairs were never sampled.** Nobody knows whether player-built tiles appear
  in the static tile map the walk check reads. **untested**, and the first spike Uriel should run.

### Height levels and the regroup (proven, with a trap)

`SpawnTracker.Regroup` (lines 329-362, A23): once `Height.ServerHeightLevel > 0`, a unit on another level than its
group's anchor is moved to within 1 m of the anchor and its `PreCombatPosition` updated.

- An AroundPlayer ring took the player's height and the regroup pushed units into the terrain (automation Session 1).
  Fix: AroundPlayer groups are never regrouped (A4).
- **A Thug on a raised castle plot was regrouped from height 5.0 to 0.0** (event-spawns part B). On a castle,
  `ServerHeightLevel` reflects storeys and raised plots; a level-based regroup fights the building. For a multi-storey
  castle, record the intended storey per unit (Uriel's 2.5 m-per-storey band) and never move a unit across storeys.

### Castle territory (proven)

`src/Services/TerritoryMap.cs:76-97`: every `CastleHeart` → `CastleHeart.CastleTerritoryEntity` →
`CastleTerritoryBlocks` buffer, read once per wave, fail closed (no map, no spawn).

- By default a wave whose centre is in claimed territory is skipped ("centre in claimed territory"). **proven**.
- With `allowTerritory: true`, **3 Thugs spawned inside the owner's castle and the event stop removed them**
  (event-spawns part B step 3). **This is the only in-castle unit spawn Nyarlathotep has ever run.** It proved spawn
  and removal; it did not test fighting, doors, stairs or pathing indoors.
- Hunt leaves out players standing in claimed territory (part A step 10: owner on his plot, "0 seeds kept").
- AroundPlayer finds no eligible player at home in castle territory (wave-sets Session 1).
- Uncertainty: TerritoryMap's CastleHeart query is a default query (no `IncludeDisabled`). Uriel's lessons say hearts
  can be disabled while streamed out. Nyarlathotep never saw a missing heart, but Uriel should use
  `IncludeDisabled | IncludeSpawnTag` and its existing `TryResolvePlot` (position → block → `CastleTerritory` → heart).

### Safety rule

Validate every spot before spawning; fail closed when the map cannot be read; never spawn into water, walls or another
storey. Resolve the castle heart freshly each time; never cache castle entity handles (they change on stream-in, Uriel
Session 9).

### Copy from

`src/Services/WalkCheck.cs:110-176`, `src/Logic/Spawning.cs:345-370` (`WavePoints.Plan`), `src/Services/TerritoryMap.cs:76-97`,
`src/Services/SpawnTracker.cs:329-362` (the regroup, as a warning).

## 3. Controlling: making a unit fight a target

### What works (proven, S1 spike, docs/features/SIEGES.md)

Only the game's own aggro chase walks a unit. To point a unit at a player:

1. Widen `AggroConsumer.ProximityRadius` and `MaxDistanceFromPreCombatPosition`, and `AggroModifiers`
   `CircleRadiusFactor` / `ConeRadiusFactor`, if the target may be far.
2. Add one `AggroBuffer` entry `{ Entity = player character, DamageValue = 500, Weight = 1 }`
   (`src/Services/HuntAction.cs:277`).

Reach: 30 m 5/5 in 13 s, 60 m 4/5, 80 m 4/10; past about 86-94 m the game prunes the entry. Event-spawns part C:
3/3 Thugs came from 40 m out of sight.

`HuntAction` (`src/Services/HuntAction.cs`) re-seeds every 5 s. Lessons in its code:

- Keep a record of which entries you wrote (`HuntSeeds` in `src/Logic/Spawning.cs`). An entry the game has rewritten is the game's now;
  never remove an entry the game shares (A67).
- Never seed a player the buffer already holds.
- Fail closed: if the territory map failed, remove every seed and add none.
- One query over `User`, built once and kept (`PlayerQuery`, lines 325-375). A new query each read cost up to 295 ms
  a tick (automation A6).

### What failed (proven)

- **Line of sight is required.** In the wall run, units behind a castle stone wall stopped at about 30 m in Combat;
  they neither pathed around nor attacked, and the game pruned the target every second while it was hidden. This is
  the single most important unknown for interiors: a room behind a door or wall may freeze the mob.
- A unit that loses its target freezes in Combat.
- `Follower.Followed` on an anchor (a rat) teleports the unit and **aborted the server twice** ("entity does not exist
  … AppendDestroyedEntityRecordError"). Never use follow links on spawned units.
- Setting `PreCombatPosition` and forcing a Return state leaves the unit Idle.
- **Leash.** A unit drops a chase about `MaxDistanceFromPreCombatPosition` from its spawn (a spider's is 45 m;
  hunting-grounds A5). Fix: move the return point to the target (`Rehome`, lines 288-303, writes both
  `AggroConsumer.PreCombatPosition` and `SpawnTransform.Position`). Small rooms will rarely hit this.
- **NPCs never target an invulnerable admin.** A forced `.devaggro` entry was dropped at once, 0/16 held (unit-watch
  Session 1). Admins testing an event must not be invulnerable, or must test with a second player.

### Factions (proven, `faction-mix.vrs` PASS 89/89)

Units keep their own `FactionReference`. Allied in mixed waves: the undead mix (including the Bishop of Death V
Blood), spiders (including the Queen V Blood), bandits, militia. Legion Shadowkin is hostile to the undead. Native
NPCs kill our units (bandits killed arrivals; a bear killed 2). Indoors, the castle's servants and defences may do
the same (untested).

Bloodcraft moves familiars to faction Ignored; Bloodcraft and KindredCommands both prefix `UnitSpawnerReactSystem`
and Bloodcraft stamps IsMinion. Spawning with `InstantiateEntityImmediate` avoids that system entirely.

### Safety rule

Write AggroBuffer entries only on your own units, track which entries are yours, and give the game back any entry it
changed. No follow links.

### Copy from

`src/Services/HuntAction.cs` (whole file; `SeedUnit` 248-282, `Rehome` 288-303, `PlayerQuery` 325-375),
`src/Logic/Spawning.cs` (`HuntBuffer` from line 896, `HuntSeeds` from line 1062).

## 4. Following and hunting: moving units somewhere

### Hidden jump (proven)

`SpawnTracker.Move` (lines 531-548) writes, together:

- `Translation` and `LastTranslation`
- `AggroConsumer.PreCombatPosition`
- `SpawnTransform.Position` (without it the unit walks home)
- an emptied `ProjectM.Pathfinding.PathBuffer`
- `Velocity` zeroed

Hunting-grounds spike run 4: 12/12 arrived 52-60 m away. Nyarlathotep only jumps while no player is within 100 m and
lands 50-60 m from the player, then lets the aggro chase do the last stretch. The owner's D26 first failed (units
idled at 55-64 m, beyond seed range); A4 (seed range) and A5 (return point) fixed it. Indoors a jump is a teleport
the player may see; use it only out of sight or as a deliberate effect.

The apocalypse march hops 10 m every 5 s: 20/20 kept up with the path cleared; without clearing `PathBuffer`, units
fell behind. `MoveOwn` and `RenewOwn` (rolling LifeTime) are at lines 563-578.

### Walking to a coordinate (partly proven)

- Moving the return point plus a Return state: 0/10 walked ("walk not usable", pathing probe).
- **WalkToPos buff** (`docs/RESEARCH_NOTES.md`, 2026-10-14): Valyr's `AB_Blackfang_Valyr_WalkToPos_WalkToPosBuff`
  (131220609), with the buff's Translation = the mark, lifetime 120, RecalculationFrequency 1, AmplifyBuff removed.
  **3 of 5 walked 110 m** and stayed without walking home; 2 never moved (cause unproven). Untested: castle-heart
  targets, combat on the way, longer legs, indoors.
- Other candidates, all **untested**: Lucie's buff -1341107993 (walk to an entity); `Follower` patrol mode
  (FollowMode.Patrol = 1); `Script_Buff_MoveToBusStop` for roads.

### Safety rule

Move only your own units, write all five components together, and never move a unit while a player can see the jump
unless the event means it.

### Copy from

`src/Services/SpawnTracker.cs:531-578`, `src/Services/HuntAction.cs:288-303`.

## 5. Activities: what units do when no one fights them

| Activity | State | Evidence | Code |
|---|---|---|---|
| Stand at the spawn point | **proven** | A Point wave without Hunt stands where spawned (an hg-roam test-sheet error proved it) | default |
| Stay in an arena | **proven** | Owner: "returned 4, farthest 46 m"; units more than radius + 10 m out are returned every 5 s; AggroBuffer entries on players outside are dropped first | `src/Services/SpawnTracker.cs:400-440` (`Return`, `DropTargets`), `src/Logic/Arena.cs` |
| Stay in a region | **proven** | "returned 6, farthest 77 m out" | `src/Logic/HuntingGrounds.cs`, `src/Services/SpawnTracker.cs:690-730` |
| Hunt players in range | **proven** | section 3 | `src/Services/HuntAction.cs` |
| Guard, Ambush, Patrol | **untested** | Not built (Epic A32); "Guard" is refused as an unknown behaviour type | none |
| Vanilla idle animations | **untested** | `TM_IdleInteractionLocation_*` (16 idle spots) are in Uriel's catalog as exclusions; never tried as a lever | none |

The arena is the closest fit for a castle room: a centre, a radius, return-if-outside, and targets outside dropped so
the mob never leaves to chase someone in the corridor. Indoors, a jump back through a wall may land on the wrong side
of it; return to the recorded spawn spot, not to a computed ring point.

## 6. Stuck detection, kill credit and death detection

### Unit watch (proven, 0.13.0, `src/Logic/UnitWatch.cs`, `docs/features/UNIT_WATCH.md`)

- Standing and still rules apply only while a unit holds a player target (unit-watch D56). A unit with no target is
  allowed to stand.
- A stuck unit is recovered twice by a hidden move, then removed with no kill credit.
- A 10-minute stall backstop ends a wave no one can finish; `.nyar event unstick` does it by hand.
- Scenarios PASS 23/23 and 25/25. A staged despawn raises no death event (6/6 silent).

### Fight read (proven)

`src/Patches/FightPatch.cs:21-73`: a postfix on `StatChangeSystem.ApplyStatChanges`. A row whose `Reason` is
`StatChangeReason.DealDamageSystem_0` is one hit; `Entity` is the one hit, `Source` the attacker. Follow up to three
`EntityOwner` links to reach a player or our unit from a projectile. 0.083 ms a call. Return at once when nothing is
tracked.

Failed alternatives: reading `DamageTakenEvent` cost 54.8 ms; a postfix on DealDamage gave 0 lines. A hit on a player
was never observable with an invulnerable admin.

### Death (proven)

`src/Patches/DeathEventPatch.cs`: a postfix on `DeathEventListenerSystem.OnUpdate`, reading
`__instance._DeathEventQuery` (line 47). Each `DeathEvent` has `Died` and `Killer`. The system runs only when
something dies, so nothing ticks from here.

- Read everything you need from your ledger **before** you forget the victim (`DeathPass.Run`, line 62; ledger reads
  before `SpawnTracker.Died` at line 86). Guard each consumer in its own try/catch so one throw never skips another
  death; log failures once per streak (`Logic.FailureStreak`).
- Kill credit: the killer, its `EntityOwner`, and the player that owner's `Follower.Followed` points to
  (`ScoreReader.Players`, lines 173-182). Kills by players, familiars and summons were all credited (wave-sets: 28
  kills).
- A V Blood carries `VBloodConsumeSource`. `VBloodSystem` fires on feed, not on kill. A player character carries
  `VBloodConsumeSource` too: a player's death raised a false VBloodKilled until 62ce215.
- A vampire killed by an event unit gives killer = that unit, though the death screen reads "Killed by: Unknown"
  (wave-sets Session 1).
- World death causes (water, falling, sunlight, other NPCs) were **never sampled**; whether each arrives as a
  `DeathEvent` or as a silent removal is unknown (replacements D29, planned). Indoors, sunlight through a window or an
  open door is the likely one.

### Copy from

`src/Patches/FightPatch.cs`, `src/Logic/FightRead.cs` (`OwnerChain` from line 69), `src/Patches/DeathEventPatch.cs`,
`src/Logic/ScoreFeed.cs` (`DeathPass` from line 117), `src/Logic/UnitWatch.cs`.

## 7. Respawning, replacing, waves and cleared detection

### Waves (proven, wave-sets 0.9.0)

`waveList` with per-wave units, and `afterSeconds` / `whenCleared` starts (`WaveSchedule.Due` / `Defeated` in
`src/Logic/Engine.cs:282-315`). **Cleared = the wave was decided and has no pending order and no tracked unit left.**
A skipped or zero-rolled wave counts as cleared but rules out a "victory" end. Victory: the last wave cleared and every
wave fought → "all waves defeated" (Session 1: 3 of 3, 28 kills).

Instances: a stop and start inside the grace period leaves the earlier instance's units tracked; the new instance
neither waits for them nor credits their kills.

Triggers proven in Nyarlathotep: Interval, RegionEntered (an entry trigger with a per-player cooldown), FactionKills,
Schedule, GameTime, VBloodKilled, Revenge heat. RegionEntered is the model for "player enters the room".

### Replacing deaths (untested, planned for 0.15.0)

`docs/dod/replacements.md` (ready 0/29, design D58): opt-in `"replacements": { "budget": 0-100 }`. A unit is
**engaged** if it hit or was hit, was killed by a player, or had a player target within 5 m at death. An unengaged
death is replaced at its group's spawn point, walk-checked, with the same tuning and due time; the wave is held (not
cleared) meanwhile. Never for removals or despawns.

Today nothing in Nyarlathotep respawns on a timer except the apocalypse roamer top-up (`src/Logic/Apocalypse.cs:214` and 919,
`TopUp`). For Uriel's "respawn on door open", the simpler model is better: on the trigger, despawn what is
left through the queue, wait for the despawn to finish, then spawn the full set. Uriel's own `RespawnTick` already
tells "destroyed" from "streamed out" for objects (disabled-included live index plus `HeartExistsByTile`); use the
same idea for units, but note a unit counted "gone" may only be disabled (untested for units, because
`CanDisable = false` should stop that).

### Safety rule

A respawn spends a budget and goes through the spawn queue and the caps. A wave is cleared only from your ledger, never
from a world query.

## 8. Buffs through reversible carrier buffs

### What works (proven, faction-empowerment)

`src/Services/EmpowerAction.cs`, `src/Logic/Empowerment.cs`:

- Carrier buff `-1591883586` (T02 potion buff) made with `TryInstantiateBuffEntityImmediate`, `BuffType.Replace`,
  LifeTime = remaining time, `EndAction = Destroy`; marked with SpellLevel 1313952069.
- Strip the gameplay-event components; `Clear()` the `ModifyUnitStatBuff_DOTS` buffer and write your own
  (`MultiplyBaseAdd`, ×1.5 → 0.5).
- Exact ×1.5 / ×2 stats; reverts on expiry keeping the health ratio; survives stream-out; persists across a restart
  with LifeTime still counting, so the boot sweep removes it.
- Remove with `RemoveBuffSafe` (`DestroyDebugReason.TryRemoveBuff`). Apply in batches of 50.
- The carrier prefab must already have LifeTime, or Burst crashes.

### What failed (proven)

- A faction query admitted a prisoner villager. Filter what you buff.
- Familiars must be left alone (they were, by filter).

### Safety rule

Buff natives only through a timed carrier. Direct stat writes are permanent; use them only on units you spawned and
will despawn, and scale from prefab stats, not from current ones.

## 9. Bosses as adds or as the event

### What works (proven, revenge D32, `boss-wave.vrs` PASS 13/13)

- A final-wave `bosses` entry spawned Ungoro (`CHAR_Spider_Queen_VBlood`) with the normal recipe; it was killed and
  raised **no** VBloodKilled. A V Blood we spawned never raises it (D30).
- Its own summons (`CHAR_Spiderling_Summon`) belong to the game, end after 120 s and count for nothing.
- Players can feed on a spawned boss. For a prize arena, decide whether feeding is allowed (untested: whether a feed
  unlocks the V Blood's abilities for a player who already has them).
- A V Blood keeps its own level under scaling (Rufus at level 20).
- Boss summon abilities spawn nothing when a mod drives them.

Boss reinforcements (`docs/dod/boss-reinforcements.md`) are designed only. **untested**.

For Uriel's "PvP Arena boss mode": spawn 1-3 bosses with the recipe, decide victory from your ledger (all boss units
dead by `DeathEvent`), credit by the death patch's killer chain, then pay out. Two players fighting each other in the
arena is PvP; whether the bosses pick sides is untested.

## 10. Testing (vrclient)

`tools/vrclient/` (guide `tools/vrclient/README.md`): Claude plays the owner's character `Chaos` on
`127.0.0.1:9876` and runs `.vrs` scenarios; a run prints `SCENARIO PASS n/n` and writes a results JSON. Every scenario
starts with `console TPHome`. Uriel can copy the tool; the scenarios are Nyarlathotep's own.

What made testing work:

- One greppable log line per state change a test checks: `[nyar] event x wave 1/3 …: 12 units queued`. Diagnostic
  before fix: if a failure's cause is not proven, the next build adds the line that names it.
- Dev-only commands in a separate DLL (NyarDevTools): `.devtp`, `.devmark`, `.devkill`, `.devdie`, `.devhop`,
  `.devjump`, `.devunits`, `.devaggro`, `.devstrand`, `.devwalkto`, `.devgear` (`.devperish` planned). `.devkill`
  takes a prefab guid since A5.
- `pwsh tools/preflight.ps1 -LogCheck` reads both `BepInEx/LogOutput.log` and `logs/NyarDev.log`; Unity errors are
  only in the second (foundation A10). Read every `[Error` and `[Warning` anyway. Baseline noise: 226 "PrefabGUID
  428176881 is in an unknown state" warnings at world load.
- `-TimingSpan` measures tick cost; `tools/dev-snapshot.ps1 -Save / -Restore` puts the world back.
- Copy both logs before a stop; stop only after an autosave; `taskkill` without `/F` (force after 60 s).

Limits: vrclient cannot walk the character; an invulnerable admin draws no attacks (keep Chaos invulnerable anyway,
and judge fights from logs); OCR misses some chat lines. Fights with a real player, doors opened by hand and anything
about look and feel go to the owner.

## 11. Castle questions to answer before building

Each is a short spike: one scenario, a log line per reading, results under Uriel's feature doc. Ordered by how much
depends on the answer.

1. **Do castle-built floors, walls, doors and stairs exist in the tile map?** Run `CheckStaticCircle` and
   `GetIsGrounded` on a castle floor, against a castle wall, in a doorway (open and closed) and on stairs. If built
   tiles are absent, every placement check indoors reads open air.
2. **Do units chase indoors?** Seed a target through a doorway, around a corner and one storey up. The wall run says
   line of sight is needed; does an open door count?
3. **Do units path through doors and up stairs?** Do NPCs open castle doors (probably not)? A closed door may be the
   cheapest way to keep a room's mob inside.
4. **Does the unit keep its storey?** Spawn on storey 2 and fight there; read `Height.ServerHeightLevel` and y. Never
   use Nyarlathotep's level regroup on a castle (section 2).
5. **What do castle servants, defences and the heart do?** Do servants attack the mob or the mob the servants? Do
   golem-type defences or the heart's protections react? Does an NPC hit a structure, and does that count as raid
   damage outside raid hours?
6. **Do streamed-out castles disable our units?** With `CanDisable = false` they should stay. Check that units in a
   castle whose players all left are neither deleted nor frozen, and that handles change on stream-in (Uriel Session 9
   says castle handles do; units were not checked).
7. **Does the heart query need `IncludeDisabled`?** Nyarlathotep's TerritoryMap uses a default query and never saw a
   miss; Uriel's lessons suggest it can.
8. **Do units die in sunlight through windows or open doors?** And does that death arrive as a `DeathEvent`?
9. **Does WalkToPos work indoors?** It is the only coordinate-walk lever that moved units (3/5, outdoors).
10. **Who may spawn where?** Nyarlathotep's default is never in claimed territory; Uriel inverts it. Decide: only the
    owner's own plot, the clan's, or admin-only anywhere, and refuse a spawn whose centre resolves to someone else's
    heart.
11. **What does a door-entry trigger read?** Nyarlathotep's RegionEntered polls player positions every 5 s; a pay
    gate that teleports the player in can trigger directly instead.
12. **Do respawned room mobs keep the room clean across restart?** Decide sweep-and-respawn on boot (recommended)
    versus persistence.

## Suggested Uriel architecture

Port in this order; each step works and is tested before the next.

1. **Unit spawner** from `SpawnTracker`: the recipe (section 1), a separate marker value for Uriel, the boot sweep,
   budgeted spawn and despawn queues, caps in config, and a two-step purge. Keep it apart from
   `ObjectSpawnService`, whose `IsNonObject` rule rightly rejects units.
2. **`UnitSetup`**: `CanDisable = false`, aggro on, level, stats on the marker.
3. **Room placement**: `WalkCheck` plus Uriel's own `TryResolvePlot`, the storey band and the overlap guard; after
   spike 1 says whether the tile map sees castle tiles. Store each room's spawn spots, chosen once by an admin
   (`here`), rather than computing rings.
4. **Death patch** with the ledger-first order and the killer chain, for cleared detection and credit.
5. **Room containment** from the arena: return to the recorded spot, drop targets outside the room.
6. **Room lifecycle**: trigger (pay at the door, entry, timer) → despawn the rest → spawn the set → seed the entrant
   → cleared → reward or reset. Model the timer on Uriel's `RespawnTick` and the death rules on replacements D58.
7. **Tests**: a vrclient scenario per room feature with greppable `[uriel …]` lines.

Keep the kill switch working from step 1 on.
