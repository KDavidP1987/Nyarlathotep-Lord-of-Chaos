# Event spawns (Pillar D)

**Status:** steps 1-2 built and post-audited; Session 1 (owner, in game) next. Depends on Foundation. Spike S2: go (2026-09-24; LifeTime needs Age; Test results). Shares the `SpawnWaves` action with
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
- A warned wave can still be skipped: its centre is in claimed territory, no player is eligible, or every unit's
  chance rolled no unit. The warning promises the time, not the units. A skipped wave uses up its slot, so the next
  wave comes on time, and `.nyar event info` and the status row count only waves that spawned. The `wave` push and
  the wave warning number a wave by its place in the schedule, so after a skip the push's `wave=2` can run ahead of
  the status row's `wave=1/3`.

## Test plan

- [ ] `.nyar spawn CHAR_Bandit_Thug 5 20 1.5 1.2` spawns, fights, drops nothing, despawns at lifetime.
- [ ] Run far away → `DestroyWhenDisabled` cleans up.
- [ ] Restart mid-wave → boot sweep removes survivors (S2).
- [ ] 3 waves on schedule, caps respected, announcements correct.

## Test results

### Owner steps for Session 1 · event-spawns step 2

Setup (Claude): the step 2 build deployed, snapshot es1, dev world booted. Debug.VerboseLogging is on. Two
invalid events, es-bad-health (action.modifiers.maxHealth 9) and es-bad-guard (behaviour Guard), are planted in
events.json so their reasons show in `.nyar event list`. Skip lines, drop counts and hunt ticks go to the BepInEx
log, not to chat, so I read those. From you I need only what differs from each expectation, and whether each
chat line shows whole. Every step happens in open ground near your castle. If your character has no castle on
Nyar Dev, place a Castle Heart first.

1. Connect to **127.0.0.1:9876** (Nyar Dev) with your admin character. Open the console (the ~ key), enter
   `adminauth`, and close it.
2. Create the test events (you can do this anywhere). Every command should be accepted with a reply naming the
   field and its new value.
   - `.nyar event new es-plain spawns`
   - `.nyar event copy es-plain es-mod`
   - `.nyar event set es-mod action.modifiers.maxHealth 2`
   - `.nyar event set es-mod action.modifiers.power 1.5`
   - `.nyar event set es-mod action.modifiers.moveSpeed 1.5`
   - `.nyar event set es-mod action.modifiers.attackSpeed 2`
   - `.nyar event set es-mod action.loot true`
   - `.nyar event copy es-plain es-lvl`
   - `.nyar event set es-lvl action.modifiers.level 30`
   - `.nyar event copy es-plain es-delta`
   - `.nyar event set es-delta action.modifiers.levelDelta +5`
   - `.nyar event copy es-plain es-hunt`
   - `.nyar event set es-hunt action.behaviour "hunt 60"`
   - `.nyar event copy es-plain es-around`
   - `.nyar event set es-around location "aroundplayer 15 25"`
   - `.nyar event copy es-plain es-castle`
   - `.nyar event set es-castle action.waves 2`
   - `.nyar event enable es-plain`, then the same for es-mod, es-lvl, es-delta, es-hunt, es-around and es-castle.

   The quotes around `hunt 60` and `aroundplayer 15 25` keep each value as one argument. If the game refuses
   them, send me the reply.
3. Send each of these. Each should be **refused** with its rule, and nothing should change:
   - `.nyar event set es-mod action.modifiers.maxHealth 5` (must be 0.5-3.0)
   - `.nyar event set es-lvl action.modifiers.levelDelta 3` (level and levelDelta together)
   - `.nyar event set es-mod action.behaviour guard` (unknown behaviour type guard)
   - `.nyar event set es-mod action.units.1.chance 2` (chance rule)
   - `.nyar event set es-mod action.loot maybe` (must be true or false)
   - `.nyar event set es-around location "aroundplayer 5 25"` (minDist must be 10-60)

   For each reply, is it whole (not cut off), and readable without relying on colour?
4. Run `.nyar event list`. Expect es-bad-health and es-bad-guard marked invalid, each with its reason
   (maxHealth's range, and "unknown behaviour type guard"). Are the reasons whole?
5. **Modifiers (plain wave).** Stand in open ground outside your castle. Run `.nyar event start es-plain`. When the
   three Bandit Thugs appear, run `.nyar debug here 30` straight away, before you fight them. Expect three rows,
   each with lvl, hp, pp, sp, ms, as and "at x,z". Then run `.nyar event stop es-plain`.
6. **Modifiers (multipliers and loot).** Run `.nyar event start es-mod`, then `.nyar debug here 30` straight away.
   Expect hp about 2× step 5's, pp and sp about 1.5×, ms about 1.5× and as about 2× (I compare the log rows). If
   you like, kill one to see whether it drops loot (es-mod has loot on, es-plain has it off). Then run
   `.nyar event stop es-mod`.
7. **Level.** Run `.nyar event start es-lvl`, then `.nyar debug here 30`. Expect lvl 30 on every row. Run
   `.nyar event stop es-lvl`. Then run `.nyar event start es-delta`, then `.nyar debug here 30`. Expect lvl 5 above
   step 5's. Run `.nyar event stop es-delta`.
8. **AroundPlayer.** Run `.nyar event start es-around`. Expect Thugs to appear 15 to 25 m from you, on a spot
   around you rather than on top of you. Run `.nyar event stop es-around`.
9. **AroundPlayer with a scope you are not in.** Run `.nyar event set es-around action.scope CursedForest` (unless
   you stand in the Cursed Forest; if you do, use `FarbaneWoods`). Run `.nyar event start es-around`. Expect the
   start reply and **no** Thugs; I read the "no eligible player" skip line in the log. Run
   `.nyar event stop es-around`.
10. **Hunt.** Pick a spot with a building, rock or hill you can hide behind about 40 m away. Stand on the spot and
    run `.nyar event set es-hunt location here`. Walk about 40 m away, out of sight behind the cover (roughly
    8 seconds of walking). Run `.nyar event start es-hunt`. Then run `.nyar debug here 45` and
    `.nyar debug here 35` at once. Expect the Thugs listed by the first and not by the second. Stay out of sight
    and don't attack. Expect the Thugs to come to you within about 15 seconds. Tell me whether they came. Then run
    `.nyar event stop es-hunt`.
11. **Claimed territory.** Go inside your castle, within its floor. Run `.nyar event set es-castle location here`,
    then `.nyar event start es-castle`. Expect the start reply and **no** Thugs. Wait about 70 seconds (es-castle
    has a second wave one minute later). Expect no Thugs then either. I read the two "centre in claimed territory"
    skip lines in the log, one per wave, a minute apart. Run `.nyar event stop es-castle`.
12. Still inside the castle, run `.nyar event set es-castle action.allowTerritory true`, then
    `.nyar event start es-castle`. Expect three Thugs inside the castle. Run `.nyar event stop es-castle` right away.
    Expect them to vanish within a few seconds.
13. Stay connected for about 2 minutes, so the server autosaves after these steps. Then tell me you're done, and
    paste anything that differed from an expectation.

Afterwards (Claude):
- Stop the server after an autosave that follows the last step.
- Copy both logs, run `-LogCheck`, and read every [Error] and [Warning] line.
- Record under Session 1 below:
  - D10: each reading pair and ratio, from the `[nyar] debug:` lines.
  - D11: the "drops CHAR_Bandit_Thug: <n> before, <m> after setup" lines for es-plain (m 0) and es-mod (m = n, n > 0).
  - D13: the Hunt ticks, two consecutive ones with "0 left to the game".
  - D16: the " around a player" wave line and the skip line.
  - D17: the skip lines (one per wave, the second one interval after the first; Review 29 F2), then the
    allowTerritory wave.
  - D32: the owner's rendering notes.
- Run `dev-snapshot.ps1 -Restore`, then redeploy.

### S2 restart spike · 2026-09-24 · spikes step 3 sessions 9–12 (throwaway save)

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
- [x] LifeTime after restart, with Age: continued. `tag 2 300 1` swept "287s age 13s"; AutoSave_447, hard stop 11:12:16, restart. The sweep at 11:14 showed "saved 1 [… 175s age 125s] dontsave 0". Age and LifeTime are saved with the unit and keep counting after the load, so a restart does not extend or reset a unit's life
- [x] load errors: none. "exception" count was 0 in BepInEx/LogOutput.log after every one of the six restarts in sessions 4–12
- S2 verdict: go — tag with the inert marker buff (SpellLevel 1314472274) and find units after a restart with an IncludeDisabled | IncludeSpawnTag query on Buff + SpellLevel; bound every unit with LifeTime **plus Age** (without Age LifeTime never runs); add DestroyWhenDisabled for units that may be left alone (it removes them at boot and when players leave) or CanDisable = false for units that must persist, which LifeTime then bounds; add PersistenceV2.DontSaveEntity to units that must never survive a restart
