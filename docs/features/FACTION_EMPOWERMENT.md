# Faction empowerment (Pillar A)

**Status:** in build (docs/dod/faction-empowerment.md, approved 2026-09-26). Steps 1–2 of 7 done: the Empower action's
schema and pairing rule, eligibility, the carrier recipe and ledger, the boot-sweep split, the one-per-faction rule,
{faction} in messages and the stat fields of `event set`, all pure logic under unit test (audit:
docs/audits/faction-empowerment.md). Step 2 (post-audited, Codex READY): Services/EmpowerAction.cs applies and removes carriers in game
through the ledger, EventRuntime dispatches Empower events, the boot sweep removes leftover carriers without touching
their NPCs, `.nyar debug here` lists native NPCs with their carrier read back, and the V Blood trigger needs
VBloodConsumeSource. A carrier still present 5 s after its event's natural end is queued for removal (A4). Not yet run on a server: Session 1 is step 4. Spike S3: go (2026-09-24; carrier recipe in Test
results).

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

### Session 1 · 2026-09-26 · faction-empowerment step 4 (build 5d775a9, dev world nyardev, unattended)

Setup: `pwsh tools/dev-snapshot.ps1 -Save s1` (28 files), Release build deployed, `python tools/ingame/session-events.py fe1` at
12:25:04: Pillars.FactionEmpowerment and Debug.VerboseLogging on, two Schedule-triggered Empower events on Faction_Bandits with
physicalPower 1.5 and maxHealth 1.5: fe-short (Sat 12:28, 60 s) and fe-long (Sat 12:31, 1200 s). No player connected.

- [x] boot 1 (12:25): "boot carrier sweep: 0 found, 0 queued for removal"
- [x] fe-short started by Schedule 12:28; "empower fe-short: query 41 of 41 faction entities"; "sweep 40 applied, 1 skipped (vblood 1)"
- [x] apply sample: "empower fe-short sample CHAR_Bandit_Scout: pp 11.49 -> 17.24, hp max 53.05 -> 79.58" (both exactly ×1.5)
- [x] natural end: "event fe-short ended (40 carriers expire with it)", then one tick later the revert sample "pp 17.24 -> 11.49, hp max 79.58 -> 53.05"; no "outlived the end" line, so all 40 carriers were gone within 5 s of the end (A4 watch)
- [x] fe-long started by Schedule 12:31; "sweep 40 applied, 1 skipped (vblood 1)"; apply sample the same ×1.5
- [x] tick timing with both sweeps: avg 0.05–1.7 ms, max 55.5 ms over 61 ticks (the tick of the first 40 applies), then max ≤ 3.5 ms
- [x] mid-window stop at 12:40 (fe-long due to end 12:51, several autosaves after its apply), hard stop as in the rollback drill
- [x] boot 2 (12:40): "event fe-long cancelled by restart (it was due to end 2026-09-26 16:51:00Z)" and "boot carrier sweep: 40 found, 40 queued for removal" (k = 40 > 0); the removals ran, then the next autosave (12:43) before the stop
- [x] boot 3 (12:43): "boot carrier sweep: 0 found, 0 queued for removal"
- [x] `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s1, … deleted)"; the fe1 backups in %TEMP%\nyar-session archived to the session scratchpad; `-Paths` clean
- logs: each boot's BepInEx log has only the three known warnings (Beelzebub TUNE ×2, Il2CppInterop Class::Init); the Unity log's 226 "PrefabLookupMap.TryGet - Prefab with PrefabGUID <n> is in an unknown state" warnings all fall between the save load and "Startup Completed" (one per GUID, from the save), 0 after it, 0 exceptions
- not covered here: the stop and purge paths (owner, Session 2); S-7 is decided after Session 2

### Session 2 · faction-empowerment step 5 (with the owner) — steps

Setup (Claude, before the owner connects): `pwsh tools/dev-snapshot.ps1 -Save s2`, Release build deployed,
`python tools/ingame/session-events.py fe2` (example-empowerment: Manual, Faction_Bandits, pp 1.5, sp 1.5, maxHealth 2.0,
attackSpeed 1.5, moveSpeed 1.5, 900 s; fe-second; fe-expire: 60 s; fe-big: Undead, Militia, Legion, Blackfangs and
Gloomrot, 600 s; fe-vblood: VBloodKilled any, 120 s; fe-spawns: 3 Bandit Thugs at the admin; cfg: FactionEmpowerment,
EventSpawns, VerboseLogging, TimingLog, EventBanners on), server booted on world nyardev. With VerboseLogging every tick
that takes a removal logs "empower tick: <k> removals (batch <b>), <p> still queued" (A6).
Everything `.nyar debug here` prints is also written in full to the BepInEx log, so the owner only notes what the log
cannot see: damage numbers, chat lines and what the bandits look like they are doing.

Owner steps (server **127.0.0.1:9876**, Direct Connect, world "Nyar Dev"):

1. Connect to 127.0.0.1:9876 with your admin character. Open the console (the ~ key) and enter `adminauth`, then
   close it.
2. Run `.nyar event list`. Expect example-empowerment, fe-second, fe-expire, fe-big, fe-vblood and fe-spawns, all ready.
3. Go to a Farbane Woods bandit camp with at least 11 bandits (e.g. Rufus the Foreman's lumber camp). Stand in the middle,
   out of their aggro if you can.
4. Run `.nyar debug here 40`. Expect up to 10 "native" rows, each with "carrier none". These are the plain readings.
   **Stay on this spot until step 7**: the rows are matched by prefab and distance, so the same NPCs must be read twice
   (if the bandits walk around, stand still anyway; Claude only uses rows whose prefab and distance match).
5. Let one Bandit Thug (or another melee bandit; note which) hit you 3 times, without changing your gear. Note the 3
   damage numbers.
6. Run `.nyar event start example-empowerment`. Note every chat line you see (the reply and the server-wide banner).
7. Wait 20 seconds, then run `.nyar debug here 40` again. Expect the rows to show "carrier example-empowerment" with
   "type Replace stacks 1 incr False end Destroy mark ok strip ok".
8. Let the same unit type hit you 3 more times, with the same gear. Note the 3 damage numbers (expected about 1.5×).
   Watch whether the bandits attack and move visibly faster, and note what you see.
9. Run `.nyar event start fe-second`. Note the reply (expected: "faction Bandits already empowered by
   example-empowerment").
10. Kill 2 or 3 bandits of the camp and stay within about 40 m. When one reappears, run `.nyar debug here 40` at once and
    again every 5 seconds until its row shows "carrier example-empowerment" (the log timestamps each run, so the
    carrier's arrival is bounded to 5 s). If none respawns in 10 minutes, write "no respawn" and go on; that clause of
    D16 is then retried in Session 3.
11. Run `.nyar status`. Note the time left shown for example-empowerment.
12. Run `.nyar event stop example-empowerment`. Note the chat lines. Wait 5 seconds, then run `.nyar debug here 40`.
    Expect "carrier none" on every row.
13. Run `.nyar event start example-empowerment`, `.nyar event start fe-big`, then `.nyar event start fe-spawns`
    (3 Bandit Thugs spawn around you). Wait 30 seconds and run `.nyar status` (three events expected).
14. Run `.nyar purge`, then `.nyar purge confirm` within 30 seconds. Wait 10 seconds, then run `.nyar status` (expected:
    no active events) and `.nyar debug here 40` (expected: no tracked units, "carrier none" on every native row).
15. Wait 2 minutes (the purge cooldown is 60 s), still at the camp. Run `.nyar event start fe-expire`, wait 20 seconds
    and run `.nyar debug here 40` (expected: "carrier fe-expire"). Wait until `.nyar status` shows no active event (about
    a minute), then wait 10 more seconds and run `.nyar debug here 40` (expected: "carrier none" on every row).
16. Kill any V Blood boss (an easy one near Farbane is fine). Note the chat lines. Right after, run `.nyar status`
    (expected: fe-vblood active, about 120 s left). Wait until it ends on its own, about 2 minutes, and note the end line
    if one shows.
17. Disconnect and tell Claude "session 2 done", with your notes from steps 5, 6, 8, 9, 10, 12, 15 and 16.

After the owner (Claude): stop the server, `pwsh tools/preflight.ps1 -LogCheck`, read every [Error]/[Warning] line of
both logs, record the results below against D4, D14, D15, D16, D18 (purge part; uninstall and coexistence are Session 3),
D19 and S-7, then `pwsh tools/dev-snapshot.ps1 -Restore`. D15 is judged from the step 4 and step 7 debug lines in the
log: rows matched by prefab and distance (±1 m), only rows showing "other stat buffs 0" in both, level unchanged, and for
each stat the empowered reading = plain × multiplier ±1 % (hp max ×2.0, pp ×1.5, sp ×1.5, aspd ×1.5, mspd ×1.5); a stat
with no matched pair, or one outside the tolerance, fails D15 and goes to a `discovered` amendment. The step 5/8 damage
numbers must differ by ×1.5 ±10 %. S-7 needs an "empower tick: 200 removals (batch 200)" line in the
purge drain with no error; if the query totals of example-empowerment and fe-big stay under 200, S-7's 200-removal clause
is recorded as unsettled and brought to the owner before step 6.

### Session 2 · 2026-09-26 · faction-empowerment step 5 (build 7400fc6, dev world nyardev, with the owner)

Owner at Rufus the Foreman's lumber camp, 17:19–17:47. The owner skipped the damage top-up, so the damage
comparison moves to Session 3. Chat lines are from the owner's notes; log lines are from BepInEx/LogOutput.log.

- [x] step 2 `.nyar event list`: page 1/1, all six events enabled with pillar and trigger (example-empowerment,
  fe-big, fe-expire, fe-second, fe-spawns, fe-vblood vbloodkilled any)
- [x] steps 4 and 7, D14: 10 native rows each, nearest first, "carrier none … other stat buffs 0" before, then
  "carrier example-empowerment left 869s type Replace stacks 1 incr False end Destroy mark ok strip ok mods
  PhysicalPower:MultiplyBaseAdd:0.5,SpellPower:MultiplyBaseAdd:0.5,MaxHealth:MultiplyBaseAdd:1,
  PrimaryAttackSpeed:MultiplyBaseAdd:0.5,AbilityAttackSpeed:MultiplyBaseAdd:0.5,MovementSpeed:MultiplyBaseAdd:0.5 other
  stat buffs 0" (D4: the recipe read back from the live carrier, the mods list entry for entry equal to
  EmpowerStatsTests' expected order for the event's five stats)
- [x] step 6: "event example-empowerment started", banner "The Bandits rally begins."; log "query 90 of 90 faction
  entities", "sweep 88 applied, 2 skipped (vblood 2)"; Rufus (CHAR_Bandit_Foreman_VBlood) reads "carrier none"
- D15 readings, all rows "other stat buffs 0", same prefab and level:
  - hp max ×2.0: Thug and Hunter 54 → 108, Mugger 128 → 256, Thief 122 → 243,
    Wolf 61 → 121, Woodcutter 51 → 101; the displays are rounded, so the plain maxima are near 121.5, 60.5 and 50.5
    and their doubles display as 243, 121 and 101; all within ±1 %
  - pp and sp ×1.5: 13.659 → 20.488, 12.884 → 19.327, 18.5 → 27.75, 19.697 → 29.545, 11.494 → 17.241 (Rascal)
  - aspd ×1.5: 1 → 1.5 on every row
  - mspd ×1.5: the reading is the unit's live speed (idle, walking or chasing), so pairs are taken in the same state:
    the step 10 (carried) and step 12 (plain) readings of matched same-prefab, same-level rows in comparable aggro
    movement states (the rows carry no entity id) give Hunter d 9m 3.75 / 2.5, Wolf
    8.25 / 5.5, Thief 5.25 / 3.5, Mugger 4.5 / 3, Rascal 4.8 / 3.2, all exactly ×1.5; the step 4 → 7 pairs differ
    because the bandits aggroed in between (Thug 1 → 4.8)
  - only the Hunter at d 9m keeps its distance in steps 4 and 7 (±1 m); the other pairs match by prefab and level,
    whose base stats are identical per prefab
  - not recorded: the damage numbers (steps 5 and 8) and the owner's by-eye view of attack and move speed; both
    move to Session 3, so D15 stays open
- [x] step 9: "faction Bandits already empowered by example-empowerment" (log: "event fe-second not started by manual")
- step 10, D16 respawn: after the owner killed Woodcutters and Thugs, the log has mid-window sweeps "sweep 1 applied,
  86 skipped (vblood 2 carried 84)", then 1, 1 and 2 more, so units that appeared during the window were carried; the
  17:36 reading shows two Rascals with "left 201s" and "left 199s". The owner could not tell a respawn from a
  wandering bandit and did not poll every 5 s, and the BepInEx log has no timestamps, so the 15 s bound is not
  measured; retried in Session 3
- [x] step 11: "Bandits rally: 3 min left"
- [x] step 12: "event example-empowerment stopped", banner "The Bandits fall quiet. Bandits rally has ended."; log
  "89 carriers queued for removal (stopped)", "stopped: 89 removed, 0 left to expire", "empower tick: 89 removals
  (batch 200), 0 still queued" (one tick), the revert sample "pp 17.24 -> 11.49, hp max 106.11 -> 53.05"; the debug
  rows 5 s later all "carrier none" with base stats
- [x] step 13: three events started, banners "The Bandits stir: Bandits rally has begun." (Messages.cs picks one of
  its start lines) and "The Undead, Militia, Legion, Blackfangs, Gloomrot stir: The world stirs has begun."; fe-big
  "query 91 of 91", "sweep 89 applied"; fe-spawns spawned 3 CHAR_Bandit_Thug (lifetime 720s); `.nyar status` listed
  three events and "tracked units: 3"; the late sweeps skipped "ours 3", so our own spawns are never carried
- [x] step 14, D18 purge: "purge ends 3 events and despawns 3 units; run .nyar purge confirm within 30 s", then
  "purged: 3 events, 3 units queued"; log "despawn batch: 3 of 3 destroyed", "example-empowerment stopped: 94
  removed", "fe-big stopped: 91 removed", "empower tick: 185 removals (batch 200), 0 still queued", both revert
  samples; `.nyar status` "No active events.", "tracked units: 0"; the debug rows all "carrier none" with base stats
- step 15: not run. The dev cfg still held PurgeCooldownSeconds = 240 from an earlier session and the steps said to
  wait 2 minutes, so all four starts of fe-expire (17:41–17:44) replied "purge cooldown active", as they should
  (a step error, not a mod fault; A7 records the fix). The natural end is covered by step 16.
- [x] step 16: Rufus killed; "trigger: VBloodKilled CHAR_Bandit_Foreman_VBlood", "event fe-vblood started by
  VBloodKilled …", "sweep 84 applied, 2 skipped (dead 1 vblood 1)", banner "The Bandits avenge their boss begins.",
  `.nyar status` "Bandits avenge their boss: 2 min left"; at its end "event fe-vblood ended (84 carriers expire with
  it)", "The Bandits fall quiet. Bandits avenge their boss has ended." and the revert sample "hp max 79.58 -> 53.05"
  (the natural-end sample is back to base; no `.nyar debug here` was taken after the end, so complete carrier removal
  at a natural end is not yet shown in game and is retried in Session 3)
- [x] D19 tick timing (Debug.TimingLog): the highest average during the empowerment interval is 2.105 ms (max 111 ms,
  the tick of the 88-carrier sweep; the boot-wide highest, before any event, is 2.481 ms); 1.481 ms with three events; 0.414 ms over the purge drain; every average under 5 ms
- S-7: the 89- and 185-removal ticks reverted every stat with no error or warning; 200 was not reached (about 185
  units were loaded around one player). Owner decision 1B (plan mode 2026-09-26): destroy is accepted as safe at
  scale, and Session 3 sets EmpowerBatchPerTick = 50 so its purge shows the cap splitting a drain in game
- logs: the BepInEx log has the three known warnings plus the two expected nyar warnings (the stop and the purge);
  the Unity log's 226 PrefabLookupMap "unknown state" lines all come before "Startup Completed"; 0 exceptions.
  `pwsh tools/preflight.ps1 -LogCheck` → "log check: 0 unhandled, 575 nyar lines, 0 orphan errors, 0 unity errors"
- [x] `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s2, … deleted)"

### Session 3 · faction-empowerment step 6 (with the owner) — steps

Setup (Claude, before the owner connects): `pwsh tools/dev-snapshot.ps1 -Save s3`, Release build deployed,
`python tools/ingame/session-events.py fe3` (fe2's events plus fe-u-empower, Bandits pp 1.5 and maxHealth 2.0 for 900 s, and
fe-u-spawns, 3 Bandit Thugs at the admin for 900 s with unitLifetimeSeconds 840, both far longer than the wait for the
next autosave (the server's AutoSaveInterval is 120 s); fe2's cfg keys plus
Limits.PurgeCooldownSeconds = 60 and Limits.EmpowerBatchPerTick = 50). Bloodcraft 1.13.22 and KindredCommands 2.5.8 from
Thunderstore are copied into BepInEx/plugins; one boot writes Bloodcraft's cfg, in which Claude sets only
`[Familiars] FamiliarSystem = true` (every other Bloodcraft system is off by default), then the server boots again on world
nyardev.

Part 1 (owner, about 25 minutes; server **127.0.0.1:9876**, Direct Connect, world "Nyar Dev"):

1. Connect to 127.0.0.1:9876 with your admin character. Open the console (the ~ key), enter `adminauth`, and close it.
2. Run `.nyar event list`. Expect example-empowerment, fe-second, fe-expire, fe-big, fe-vblood, fe-spawns, fe-u-empower and
   fe-u-spawns, all ready.
3. Go to the Farbane bandit camp of Session 2 (Rufus's lumber camp). Let one Bandit Thug hit you 3 times, without changing
   your gear. **Note the 3 damage numbers**, and note how fast the Thugs swing (time between swings) and how fast they
   move or chase you, as the baseline for step 7.
4. Run `.fam a <your character name> CHAR_Bandit_Thug`, then `.fam l` (the list shows the Thug as number 1), then `.fam b 1`
   (your Thug familiar appears), then `.fam c` (combat off, so it stays by you). Note each reply.
5. Stand still with the familiar next to you and run `.nyar debug here 40`. The familiar is the CHAR_Bandit_Thug row at
   about 1-3 m. **Note that row's "carrier" and "hp" parts, and also the "hp" part of one camp CHAR_Bandit_Thug row
   further away** (a native Thug's base reading, compared again in step 14).
6. Run `.nyar event start example-empowerment`. Wait 20 seconds and run `.nyar debug here 40` again. Expect the camp's
   bandits on "carrier example-empowerment" and **the familiar's row still "carrier none" with the same hp as in step 5.**
7. Run `.fam t` (your familiar leaves). Let a Bandit Thug hit you 3 more times with the same gear. **Note the 3 damage
   numbers** (expected about 1.5× step 3). **Note separately** whether they attack faster (time between swings) and whether they move or chase faster than before step 6.
8. Kill 2 or 3 bandits and stay within about 40 m. When one reappears, run `.nyar debug here 40` at once and again every
   5 seconds until its row shows "carrier example-empowerment". Wait up to 10 minutes for a real respawn and stop
   polling after 2 minutes of rows without the carrier. If no bandit reappears in 10 minutes, write "no respawn" and go on;
   D16's respawn clause then stays open (KindredCommands' `.spawnnpc` is no substitute: it spawns units owned by the admin,
   which Nyarlathotep skips on purpose).
9. Walk about 40 m away from the camp, so no camp bandit is within 25 m of you. Run `.nyar event start fe-big`, then `.nyar event start fe-spawns` (3 Bandit Thugs appear around you), then
   `.spawnnpc CHAR_Bandit_Thug 2` (KindredCommands; 2 more Thugs). Wait 30 seconds and run `.nyar status`. **Note the
   fe-spawns line**: it should count 3 units, not 5.
10. Run `.nyar purge`, then `.nyar purge confirm` within 30 seconds. At once run `.nyar event start fe-expire` and **note the
    reply** (expected: "purge cooldown active (n s left)" with a number of seconds).
11. Wait 10 seconds, run `.nyar status` (expected: no active events) and `.nyar debug here 40` (expected: "carrier none" on
    every row, no tracked units). Then, still at the step 9 spot, run `.despawnnpc CHAR_Bandit_Thug 25` to remove the KindredCommands Thugs (the
    camp is more than 25 m away, so its Thugs stay). Walk back to the camp.
12. Wait until the seconds named in step 10's reply have passed, plus 5 more (about 65 seconds after the purge confirm;
    if the reply still says "purge cooldown active", wait the seconds it names and try again), then run
    `.nyar event start fe-expire` (it starts now). Wait 20 seconds and run
    `.nyar debug here 40` (expected: "carrier fe-expire"). Run `.nyar status` every 20 seconds until it shows no active
    event (about a minute), wait 10 more seconds, and run `.nyar debug here 40` (expected: "carrier none" on every row).
13. Walk about 50 m away from the camp, out of any fight. Run `.nyar event start fe-u-empower`, then
    `.nyar event start fe-u-spawns` (3 Thugs appear). Do not kill them: keep about 30 m from them (keep moving if they
    chase you) and stay connected, so they stay loaded and are saved. Within about 5 minutes the server stops and
    disconnects you (Claude's watcher stops it right after the next autosave). Tell Claude **"part 1 done"** with your
    notes from steps 3 to 12 (the debug rows are also in the log).

Between the parts (Claude, about 25 minutes, no player needed; Epic D12): Claude's watcher sees fe-u-spawns start, waits for
the next autosave after it and hard-stops the server mid-window (the carriers and the 3 Thugs are in that save); Claude deletes BepInEx/plugins/Nyarlathotep.dll, boots,
waits 16 minutes (longer than fe-u-empower's 900 s and the units' 840 s) plus the next autosave, hard-stops, reinstalls the
DLL with General.Enabled = false, boots, and reads "boot marker sweep: 0 found" in the log. Claude then tells the owner
the server is up again.

Part 2 (owner, about 3 minutes):

14. Connect to 127.0.0.1:9876 again (`adminauth` if asked). Go back to the camp, stand where you stood in step 5, and run
    `.nyar debug here 40`. Expect "carrier none" on every row, base stats (a native CHAR_Bandit_Thug row's hp max equal to the native
    Thug noted in step 5, not the familiar's), and no tracked unit. Then walk to where you started fe-u-spawns and run `.nyar debug here 40` there (expected: no tracked unit and no
    Thug left from fe-u-spawns).
15. Run `.fam ub` (unbind the familiar), disconnect, and tell Claude **"session 3 done"** with your notes from step 14.

After the owner (Claude): stop the server, `pwsh tools/preflight.ps1 -LogCheck`, read every [Error]/[Warning] line of both
logs, record the results below against D15 (damage and speed by eye), D16 (the respawn bound, the natural-end read-back),
D18 (the 50-cap drain, uninstall, coexistence) and D30, then `pwsh tools/dev-snapshot.ps1 -Restore` (it removes both mods,
their configs and the Bloodcraft player data, and must print "snapshot restored; hashes equal"). The respawn bound passes
when a respawned bandit shows the carrier within 15 s + ceil(m / 50) ticks of its first carrier-less row; the drain passes
when every "empower tick: <k> removals (batch 50)" line has k ≤ 50 and at least two such lines follow the purge.

## Open questions

- Which stats are worth exposing beyond power/HP/speed? (resistances, `SiegePower` for sieges)
- Does the vanilla level-difference damage modifier matter here (we don't change `UnitLevel` in this pillar)?
