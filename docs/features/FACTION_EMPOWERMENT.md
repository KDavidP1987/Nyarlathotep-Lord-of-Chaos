# Faction empowerment (Pillar A)

**Status:** in build (docs/dod/faction-empowerment.md, approved 2026-09-26). Step 1 of 7 done: the Empower action's
schema and pairing rule, eligibility, the carrier recipe and ledger, the boot-sweep split, the one-per-faction rule,
{faction} in messages and the stat fields of `event set`, all pure logic under unit test (audit:
docs/audits/faction-empowerment.md). Next: step 2, the service that applies carriers in game. Spike S3: go (2026-09-24;
carrier recipe in Test results).

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

The plan's D-items are authoritative (docs/dod/faction-empowerment.md); this list maps them to where they are checked.

- Unit tests, `dotnet test Nyarlathotep/Nyarlathotep.Tests` (step 1): EventValidationTests and TemplateTests (D1, D2),
  EmpowerEligibilityTests (D3), EmpowerStatsTests and CarrierRecipeTests (D4), CarrierLedgerTests (D5), EngineTests (D6,
  D9), SweepPlanTests (D7), AnnouncerTests (D10), CommandArgTests and ConfigChangedTests (D12), DeathRuleTests and
  TriggerActivationTests (D13), DependencyFailureTests (D20).
- Session 1, unattended (step 4): two Schedule-triggered Empower events on Faction_Bandits, one ending by expiry and one
  cut by a server stop; sample stat lines and both boot carrier-sweep lines (D17).
- Session 2, with the owner (step 5): debug readings per stat, damage numbers, respawn catch-up, stop, the second-event
  refusal, a V Blood kill, purge with both kinds of event running, tick timing (D14–D16, D18, D19).
- Session 3, with the owner (step 6): Bloodcraft familiar and KindredCommands spawnnpc coexistence, then the uninstall
  steps (D18).

Earlier checklist:

- [ ] S3: buff one bandit, confirm stat change (damage taken/dealt), expiry, and that it survives stream-out/in. Done: S3 verdict go (Test results).
- [ ] Manual trigger on Bandits; confirm count applied (log), announcement, expiry.
- [ ] VBloodKilled trigger fires once per kill (dedupe), not per participant.
- [ ] Restart mid-event: no NPC remains empowered after the window (D8).
- [ ] Bloodcraft installed: familiars (Players faction) untouched.

## Test results

### S3 carrier spike · 2026-09-24 · spikes step 3 session 4 (build 35dfbe9, throwaway save)

Carrier: `AB_Consumable_PhysicalPowerPotion_T02_Buff` (-1591883586). The prefab has LifeTime; `empower` overwrites it with the requested duration and EndAction Destroy, strips the gameplay-event components, clears the stat buffer and adds PhysicalPower +50 % and MaxHealth +100 % (MultiplyBaseAdd).

- [x] inspect before: CHAR_Bandit_Hunter PhysicalPower 13.66, MaxHealth 53.8, carrier none; CHAR_Bandit_Mugger PhysicalPower 19.7, MaxHealth 127.8, carrier none
- [x] inspect during (`empower 120`): Hunter 20.49 / 107.7; Mugger 29.55 / 255.7 with the carrier and "left 91s", "left 54s", "left 21s". Both stats are exactly ×1.5 and ×2. `empower 120` at 10 m applied to 3/3 native NPCs
- [x] Health.Value when the buff applies: a unit at full health goes to the new full value (53.8 → 107.7, 127.8 → 255.7)
- [x] stats back to base after expiry: yes. Mugger 19.7 / 127.8, carrier none. Health kept its ratio (165.2 of 255.7 → 82.6 of 127.8, 64.6 % both times); the unit was not killed or healed by the expiry
- [x] damage dealt and taken with and without the buff, in combat: qualitative only. The owner reports that buffed bandits "took longer than mobs of that level should to kill", which matches MaxHealth ×2. The buffed Mugger went from 255.7 to 165.2 Health in about 33 s of fighting. There is no per-hit comparison: the harness does not log damage events, and the faction-empowerment child measures damage when it picks its multipliers
- [x] buff present after streaming out to 150 m for 60 s and back: yes. `empower 300` → inspect "left 288s"; after the walk-away inspect "left 234s", same unit 325776:9, carrier still present, stats still ×1.5 / ×2, and Health had regenerated 165.2 → 191.6
- [x] state after a restart mid-buff: the buff persists and keeps counting. `empower 600` on a CHAR_Bandit_Thug gave 20.49 / 107.7 with "left 591s". The next autosave ran, then the server was hard-stopped and loaded AutoSave_400. After the restart, inspect showed the same thug under a new entity id (563005:5 → 326832:1), still at 20.49 / 107.7, carrier present, "left 394s". The carrier is saved with the NPC, and its LifeTime continues rather than resetting, so it still ends on its own. Two consequences for later children: entity ids do not survive a restart, and a window that must not outlive a restart needs its carrier removed at boot (Test plan › Restart mid-event)
- also seen: `empower` at 10 m buffed a CHAR_Bandit_Prisoner_Villager_Female, because the spike's native-NPC filter admits prisoners. The pillar must filter by faction and exclude prisoners and other non-combatants
- S3 verdict: go — AB_Consumable_PhysicalPowerPotion_T02_Buff (-1591883586) applied with TryInstantiateBuffEntityImmediate; gameplay-event components stripped; LifeTime set to the window with EndAction Destroy; ModifyUnitStatBuff_DOTS cleared and refilled with MultiplyBaseAdd modifiers. Stats apply at once, revert on expiry keeping the Health ratio, survive streaming out and back, and persist across a restart with LifeTime continuing

## Open questions

- Which stats are worth exposing beyond power/HP/speed? (resistances, `SiegePower` for sieges)
- Does the vanilla level-difference damage modifier matter here (we don't change `UnitLevel` in this pillar)?
