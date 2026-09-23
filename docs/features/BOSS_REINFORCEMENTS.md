# Boss reinforcements (Pillar C)

**Status:** designed, not started. Depends on Pillar D's `SpawnWaves`.

## Goal

V Blood fights get harder: adds join when the fight starts, at health thresholds, or periodically while it
lasts. Per boss or globally ("every boss in Dunley Farmlands calls two militia at 50% HP").

## Example

```json
{
  "id": "tristan-calls-hunters",
  "enabled": false,
  "trigger": { "type": "BossHealthPhase", "boss": "CHAR_VHunter_Leader_VBlood", "belowPercent": 50, "once": true },
  "action": {
    "type": "SpawnWaves",
    "location": { "type": "AroundBoss", "minDist": 6, "maxDist": 12 },
    "composition": [ { "unit": "CHAR_Militia_Crossbow_Summon", "count": 2 } ],
    "waves": 1,
    "modifiers": { "levelDelta": 0, "loot": false },
    "behaviour": { "type": "JoinFight" },
    "unitLifetime": 300
  }
}
```

Triggers: `BossEngaged {boss?}`, `BossHealthPhase {boss?, belowPercent, once}`, and optionally
`BossFightTick {every: 45}` for sustained pressure.

## Mechanism

- **Engaged:** prefix on `PlayerCombatBuffSystem_OnAggro`; `InverseAggroEvents.Added` where `Producer` is a
  player and `Consumer` has `VBloodUnit` (SanguineArchives). Verify the system name in current interop.
- **Health phase:** poll engaged bosses on the 1 s tick (`Health.Value / MaxHealth`). Alternative: watch the
  boss's own phase buffs from `Reference Data/script_edges.tsv` spawning (Beelzebub `BuffSpawnServerPatch`).
- **Fight over:** boss death (death hook) or reset — behaviour state leaves `AnyCombat` / health back to full
  (SanguineArchives). Either way: despawn the fight's adds (staged).
- **Adds:** same team as the boss (`Team` copied, `FactionReference` untouched — BloodyBoss `SummonMechanic`),
  `EntityOwner = boss` so the game's minion tracking also cleans them up on boss death, seeded
  `AggroBuffer` with the engaged players, `BehaviourTreeState = Combat`.
- **Default adds:** Beelzebub `AbilityCastStartedSystemPatch.SummonTargets` maps bosses to the units their
  own summon abilities create — a natural default composition per boss.
- Boss position: `Translation`, not `LocalToWorld` (Faust `BossService`).

## Edge cases

- Several players, several `VBloodConsumed`/aggro events per fight → key the fight by boss entity, dedupe.
- BloodyBoss world bosses (renamed with `bb` suffix) — exclude by default.
- Bosses with arenas/phases that teleport (Dracula, Solarus, Adam) — test individually; allow per-boss disable.
- Don't spawn adds that are boss parts (`BehaviourTreeInstance.Immobile`).
- Fight reset must not leave adds wandering.

## Test plan

- [ ] Engaged trigger fires once per fight on a low-tier boss (Alpha Wolf, Errol).
- [ ] 50% phase add spawn; adds attack players; die/despawn with the boss.
- [ ] Reset the fight (run away) → adds removed, trigger re-arms.
