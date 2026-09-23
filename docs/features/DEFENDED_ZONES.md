# Defended zones (Pillar B2)

**Status:** designed, not started. Depends on Pillar D and (for reinforcements arriving from a distance)
spike S1.

## Goal

Admins mark areas that fight back. When vampire activity inside a zone crosses a threshold, the zone's
faction sends reinforcements — the opposite of today's static world, where you only meet more enemies by
walking into their patrol paths.

## Example

```json
{
  "id": "dunley-garrison",
  "enabled": false,
  "trigger": {
    "type": "ZoneActivity", "zone": "dunley-town",
    "minVampires": 2, "sustainSeconds": 20, "orKillsWithin": { "kills": 6, "seconds": 60 },
    "cooldown": 300
  },
  "action": {
    "type": "SpawnWaves",
    "location": { "type": "Zone", "zone": "dunley-town", "spawnAtEdge": true },
    "composition": [ { "unit": "CHAR_Militia_Guard_Summon", "count": 3 }, { "unit": "CHAR_Militia_Crossbow_Summon", "count": 2 } ],
    "waves": 2, "waveInterval": 45,
    "behaviour": { "type": "Hunt" },
    "unitLifetime": 300
  }
}
```

Zones: `zones.json`, created in-game with `.nyar zone add <name> <radius>` at the admin's position (circles,
KindredArenas model). Later: named world regions (`WorldRegionType`, Faust `GetWorldRegionName`).

## Mechanism

- **Activity sampling** (1 s tick, cheap): online players' positions (Faust `PlayerInfoService` pattern:
  connected `User` → `LocalCharacter` → `LocalToWorld`), XZ distance to each zone. Track "vampires in zone
  for N seconds". Kill pressure: death hook counts NPC deaths inside the zone killed by players.
- **Escalation:** per-zone state machine `Quiet → Alerted → Reinforcing → Cooldown`. Optional escalation
  tiers (first response small, sustained presence → bigger waves).
- **Arrival:** v1 spawns at the zone edge farthest from the vampires and hunts them (seeded `AggroBuffer`).
  v2 (after S1): reinforcements spawn off-screen and **march in**.
- **Hit one, all respond:** optional `DamageTakenEvent` hook alerts the whole squad (Beelzebub
  `ReactToDamageOnSummon`).

## Edge cases

- A player's castle inside a zone: zones must not overlap claimed territory by default (check at
  `zone add` and at trigger time).
- One player idling at a zone edge: `sustainSeconds` + cooldown prevent farm loops; loot off by default.
- Never announce *who* or *where* precisely (the Raphael positional-leak lesson) — "Dunley's garrison
  sounds the alarm" is fine.

## Test plan

- [ ] `zone add`/`list`/`remove`; persisted across restart.
- [ ] Enter zone → alert after `sustainSeconds` → reinforcements hunt you → cooldown respected.
- [ ] Zone overlapping a castle is rejected.
