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
