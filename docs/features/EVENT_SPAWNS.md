# Event spawns (Pillar D)

**Status:** designed, not started. Depends on Foundation + spike S2. Shares the `SpawnWaves` action with
Pillars B and C — this doc defines it.

## Goal

Admins summon waves of chosen units into chosen places, at base strength or modified, on a schedule, on a
trigger, or by command. "Every night at 22:00 three waves of Cursed spawn at the Witch's Cottage" or
"when the Frost Vampire dies, spiders flood the Silverlight Hills."

## The `SpawnWaves` action

```json
{
  "type": "SpawnWaves",
  "location": { "type": "Zone", "zone": "cursed-forest-clearing" },
  "composition": [
    { "unit": "CHAR_Cursed_Wolf", "count": 4 },
    { "unit": "CHAR_Cursed_Wolf_Spirit", "count": 1, "chance": 0.5 }
  ],
  "waves": 3, "waveInterval": 90, "spawnRadius": 12,
  "modifiers": { "levelDelta": 3, "maxHealth": 1.5, "power": 1.2, "moveSpeed": 1.0, "loot": false },
  "behaviour": { "type": "Guard", "leashRadius": 40 },
  "unitLifetime": 600,
  "spawnVisual": "Buff_General_Spawn_Unit_Fast_WarEvent"
}
```

- **location types:** `Point {x,z}`, `Zone {zone}`, `AroundPlayer {minDist,maxDist}` (a random eligible
  online player — respects "not inside own castle"), `AroundBoss` (Pillar C), `CastleOf` (Pillar B1).
- **modifiers:** `levelDelta` or `level` (absolute), `maxHealth`, `power` (physical+spell), `moveSpeed`,
  `attackSpeed`, `loot`. Scaled from the **prefab** baseline so they never compound.
- **behaviour:** `Guard` (hold an anchor, leash radius), `Hunt` (seeded aggro on the triggering/nearest
  players), `JoinFight` (Pillar C), `Assault` (Pillar B1).

## Mechanism

1. Validate: unit exists (`prefab_names.tsv`/runtime lookup), not on the deny-list (`GAME_ASSETS.md`), caps.
2. Resolve location → spawn points on a ring within `spawnRadius` (Beelzebub radial offsets).
3. Spawn: `InstantiateEntityImmediate(Entity.Null, guid)` → set `Translation`/`LastTranslation`.
4. `UnitSetup` (Beelzebub `ApplyPlayerAllySetup` minus the player-ally parts; Bloodcraft
   `FamiliarBindingSystem.cs:600-730`): `Aggroable`/`AggroConsumer.Active` on,
   `CanPreventDisableWhenNoPlayersInRange=false`, strip `ServantConvertable`/`CharmSource`, clear
   `DropTableBuffer` if `loot=false`, remove `Minion` if Bloodcraft stamped it, shared `Team` for the wave.
5. Modifiers: direct writes (`UnitLevel`, `Health.MaxHealth` + `Value`, `UnitStats`, `AiMoveSpeeds`,
   `AbilityBar_Shared`) — acceptable because these units are ours and temporary (Bloodcraft
   `ModifyPrimalUnit`, BloodyBoss `ModifyBoss`). Zero `PassiveHealthRegen` for bosses/elites only.
6. Safety: `LifeTime{unitLifetime, Destroy}`, `DestroyWhenDisabled`, marker buff, `SpawnTracker.Register`.
7. Spawn visual 0.25 s later (Bloodcraft).
8. Stagger large waves across ticks.

## Edge cases

- Unit spawned inside geometry / water: prefer the vanilla spawner's ground snapping if this is a problem
  (fixed duration-key path, RESEARCH_NOTES §Spawning).
- Zone or point inside a player's territory: refuse unless the event explicitly allows it (BloodyEncounters
  issue #3 — encounters spawning on castles).
- Level-gap immortality: clamp `levelDelta` to ±5 by default (BloodyBoss issue #10).
- Faction infighting: units keep their `FactionReference` (AI behaviour), share a `Team` within a wave.

## Test plan

- [ ] `.nyar spawn CHAR_Bandit_Thug 5 20 1.5 1.2` spawns, fights, drops nothing, despawns at lifetime.
- [ ] Run far away → `DestroyWhenDisabled` cleans up.
- [ ] Restart mid-wave → boot sweep removes survivors (S2).
- [ ] 3 waves on schedule, caps respected, announcements correct.

## Test results

### S2 restart spike · 2026-09-24 · spikes step 3 sessions 9–11 (throwaway save; in progress)

Units: CHAR_Bandit_Thug from `.nyar spike tag`, marked by the inert AB_Consumable_PhysicalPowerPotion_T01_Buff carrying SpellLevel 1314472274, with LifeTime (EndAction Destroy) and DestroyWhenDisabled. Even-numbered units also carry PersistenceV2.DontSaveEntity. `keep 1` sets CanPreventDisableWhenNoPlayersInRange.CanDisable = false. Restarts are a hard stop right after an autosave finishes (spikes A8).

- [x] sweep before the restart (run 2, `tag 6 600 1`): "marked 6, listed 6, faults 0 | saved 3 […] dontsave 3 […]"
- [x] sweep after the restart (AutoSave_433): "marked 3, listed 0, faults 3: … not listed | saved 3 […] dontsave 0 []". "not listed" is expected, since the in-memory list is lost on restart
- [x] marker survives restart: yes. The query found every surviving unit by its marker alone, in run 2 and for 5 march units after an earlier restart
- [x] DontSaveEntity units present after restart: 0 of 3. DontSaveEntity works
- [x] DestroyWhenDisabled removed units once no player was near: yes, three ways:
  - run 1 (`tag 6 600` without keep): all 6 were gone after the restart, removed at boot
  - `tag 4 600` with the owner 200 m away for 2–3 min: all 4 gone, with 589 s of LifeTime still left
  - session 2: units spawned 100 m from any player were gone within 5 s

  Units that must outlive a player's absence need CanDisable = false and must then be bounded some other way
- [x] LifeTime without a restart: a unit made with InstantiateEntityImmediate has no Age, so LifeTime never ran. Units lived past 600 s, and `tag 2 30` units were still alive at 60 s. With Age added at spawn (spikes A10), `tag 2 30` showed "21s … age 9s" and was gone by 60 s
- [ ] LifeTime after restart (continued, reset or gone), with Age: pending
- [ ] load errors: none in any boot so far ("exception" count 0 in BepInEx/LogOutput.log after every restart)
- S2 verdict: pending (LifeTime across a restart with Age)
