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

## Owner requests (2026-09-26, Epic A23)

- **Adds at a health threshold:** "units will show up to reinforce the boss [...] at 80% health". This is
  `BossHealthPhase {belowPercent: 80}` above.
- **Anti-carry burst trigger:** "if the user does more than 40% damage to the boss within less than 2 seconds,
  then the boss themselves gets empowered, and it immediately summons reinforcements", to balance high-level
  players carrying low-level areas. Planned as trigger `BossBurstDamage {boss?, percent, windowSeconds}`
  (health lost within a sliding window, sampled on the same health poll), with two actions:
  - an empowerment of that one boss through a boss-targeted carrier (faction empowerment skips V Bloods by
    design);
  - a reinforcement wave, as for the other triggers.
- **Open for the child's planning:** whether the burst fires once per fight; the poll rate a 2 s window needs
  (the 1 s tick may be too coarse); how the boss carrier is removed on fight reset.

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
