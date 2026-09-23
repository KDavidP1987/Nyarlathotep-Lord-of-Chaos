# Faction empowerment (Pillar A)

**Status:** designed, not started. Depends on Foundation + spike S3.

## Goal

For a fixed window, every NPC of chosen factions is stronger: a Blood Moon for the enemy. Triggered on a
schedule ("Saturdays 20:00–20:20, the Legion surges") or by an event ("when Tristan the Vampire Hunter
falls, the Vampire Hunters rage for 10 minutes").

## Example definition

```json
{
  "id": "legion-surge",
  "enabled": false,
  "trigger": { "type": "Schedule", "days": ["Sat"], "time": "20:00" },
  "duration": 1200,
  "action": {
    "type": "Empower",
    "factions": ["Faction_Legion"],
    "includeUnits": [], "excludeUnits": [], "includeVBloods": false,
    "stats": { "physicalPower": 1.3, "spellPower": 1.3, "maxHealth": 1.5, "moveSpeed": 1.1, "attackSpeed": 1.15 },
    "visual": "Buff_BloodBuff_Creature_Tier4_Empower"
  },
  "announce": { "start": ["The Legion surges! Its soldiers fight with renewed fury for 20 minutes."], "end": ["The Legion's fury subsides."] }
}
```

Trigger variants: `{ "type": "VBloodKilled", "boss": "CHAR_VHunter_Leader_VBlood" }` (any boss if omitted),
`{ "type": "BloodMoon" }` (Phase 6), `{ "type": "Manual" }`.

## Mechanism

1. **Target set:** units with `FactionReference.FactionGuid` in `factions`, plus/minus explicit unit lists
   (by name or GUID; see `Reference Data/unit_index.tsv`). Always excluded: `Faction_Players*`, traders,
   critters, our own spawned units unless opted in, V Bloods unless `includeVBloods`, anything without
   `Health`/`UnitStats`.
   Query: `PrefabGUID + FactionReference + Health + UnitStats`, `IncludeDisabled | IncludeSpawnTag`, and
   **explicitly skip prefab entities** (`Prefab` component) — see DEV_REMINDERS #4.
2. **Apply:** one carrier buff per unit via `ServerGameManager.TryInstantiateBuffEntityImmediate`,
   `BuffType.Replace` (so re-applying refreshes, never stacks), `LifeTime.Duration` = seconds remaining,
   `EndAction = Destroy`. `Clear()` its `ModifyUnitStatBuff_DOTS`, add one entry per stat:
   multipliers → `ModificationType.MultiplyBaseAdd` with `value - 1`; attack/cast speed → the
   cooldown-recovery stats. Strip gameplay-event components so the carrier is inert.
   Source: Beelzebub `GrantPowerScalingService.cs:103-147`, KindredCommands `BoostedPlayerService.cs:433-509`.
3. **Visual (optional):** a second buff turned into a pure aura by stripping gameplay components (Bloodcraft
   `Utilities/Buffs.cs:264-324`), same `LifeTime`. Candidates in `GAME_ASSETS.md` §Key buffs. Verify each
   has no side effects before shipping it as a default.
4. **Late arrivals:** NPCs that spawn or stream in during the window get the buff on the periodic re-sweep
   (~15 s, Decision D5) with the *remaining* time.
5. **End:** buffs expire on their own. On `stop`, remove them explicitly (guard double-destroy with
   `Has<DestroyTag>()`).

## Why buffs, not direct stat writes

Direct writes to `UnitStats`/`Health` on native NPCs are **saved with the entity** and permanent; a crash
mid-event would leave the Legion empowered forever. A buff with `LifeTime` reverts itself even if the mod
is gone. (Bloodcraft learned the double-apply lesson the hard way — RESEARCH_NOTES §Tagging.)

## Edge cases

- Two empowerment events on the same faction: the carrier is `Replace`, so the later one wins. Decide
  whether to forbid overlap per faction (probably yes: reject the second with a log line).
- Units in combat when the buff lands: `MaxHealth` up without `Value` up leaves them "damaged". Scale
  `Health.Value` proportionally? It's the buff's `MaxHealth` stat — check what the game does on apply (S3).
- Boss rooms: `includeVBloods=false` by default; faction-adjacent V Bloods are a big difficulty jump.
- Performance: a server has thousands of NPCs. Batch the sweep across ticks (e.g. 200 units/tick).

## Test plan

- [ ] S3: buff one bandit, confirm stat change (damage taken/dealt), expiry, and that it survives stream-out/in.
- [ ] Manual trigger on Bandits; confirm count applied (log), announcement, expiry.
- [ ] VBloodKilled trigger fires once per kill (dedupe), not per participant.
- [ ] Restart mid-event: no NPC remains empowered after the window (D8).
- [ ] Bloodcraft installed: familiars (Players faction) untouched.

## Open questions

- Which stats are worth exposing beyond power/HP/speed? (resistances, `SiegePower` for sieges)
- Does the vanilla level-difference damage modifier matter here (we don't change `UnitLevel` in this pillar)?

## Test results
### 2026-09-23 · spike
- S verdict: go — recorded
