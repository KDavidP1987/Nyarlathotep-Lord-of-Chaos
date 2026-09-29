# Event spawns (Pillar D)

**Status:** steps 1-2 built, post-audited and tested in game (Session 1, 2026-09-29, parts A and B); step 3 next. Depends on Foundation. Spike S2: go (2026-09-24; LifeTime needs Age; Test results). Shares the `SpawnWaves` action with
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
   (maxHealth's range, and "unknown behaviour type Guard"). Are the reasons whole?
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

### Session 1 · 2026-09-29 · event-spawns step 2, part A (b918028, dev world nyardev, with the owner)

Setup as in the steps above (`pwsh tools/dev-snapshot.ps1 -Save es1` before the deploy). The owner ran steps 2–12 and
disconnected; the server was stopped after autosave 1954, both logs copied to %TEMP%\nyar-es1a-logs. Part B redoes
steps 10 and 12 on a build with A65 and A66.

- [x] step 2 (D18, D32): every set accepted with its field and value, e.g. "event es-hunt action.behaviour = hunt 60;
  reloaded: 15 valid, 3 disabled"; each reply whole in chat
- [ ] step 3 (D18, D32): five refusals whole and readable without colour; the unit-chance refusal read "action.units..chance
  must be 0.05-1.0 …", because the chat drops `<n>` as a tag → A65 (`action.units.N.chance`, and "hunt RANGE",
  "aroundplayer MIN MAX"); re-checked in part B
- [x] step 4 (D6, D32): `.nyar event list` showed both reasons whole: "action.modifiers.maxHealth must be a number 0.5-3.0
  with at most two decimals" and "unknown behaviour type Guard"
- [x] step 5 (D10): es-plain rows "lvl 16 hp 54/54 pp 14 sp 14 ms 3.2 as 1 at … recipe ok" (ms 1 on a unit standing)
- [x] step 6 (D9, D10, D11): es-mod rows hp 108 (2×), pp and sp 20 (1.5× of a base between 13.5 and 13.67, both
  rounded by `debug here`), ms 4.8 and 1.5 (1.5×), as 2 (2×); drops "1 before, 0 after setup" for es-plain and "1 before,
  1 after setup" for es-mod; the owner killed two es-mod Thugs and saw loot drop; the stop queued the one left
- [x] step 7 (D9): es-lvl rows lvl 30; es-delta rows lvl 21 (16 + 5)
- [x] step 8 (D16): "event es-around wave 1/1 around a player: 3 units queued (3 moved, 0 unchecked)"; the owner saw them
  appear around them, not on top
- [x] step 9 (D16, 1A): with action.scope CursedForest, "wave 1 of es-around skipped: no eligible player"; no Thugs
- [ ] step 10 (D13): es-hunt's three Thugs spawned; `debug here 45` listed them and `debug here 35` did not, as expected;
  every Hunt tick logged "0 seeds kept, 0 left to the game" and the Thugs did not come. The owner stood on their castle
  plot, which is claimed territory (D13 leaves such a player out), and the log could not say so. The three left the
  ledger about 15 s later with no line (died or removed by the game), so the stop queued 0 → A66 (the Hunt line's
  counts, and a verbose line when a unit dies or the game removes it); redone in part B away from any castle plot
- [x] step 11 (D17): "wave 1 of es-castle skipped: centre in claimed territory", and wave 2's line one interval later
  (Review 29 F2); no Thugs
- [ ] step 12 (D17): not run: the stops were typed `en-castle`, so es-castle stayed active ("not started by manual:
  already active") and ended on its own ("ended (0 of 2 waves)"); redone in part B
- [x] logs: no [Error]; BepInEx warnings only the known kinds (Il2CppInterop Class::Init; Beelzebub's two TUNE lines; the
  dev world's example-empowerment line at each reload), the two planted invalid events at each reload, and the stop
  summaries; the server log's 224 "PrefabLookupMap.TryGet … is in an unknown state" warnings at save load, the known set;
  tick timing max 30.1 ms once (the es-hunt start), otherwise under 5 ms

### Owner steps for Session 1 part B · A65, A66 and the redo of steps 10 and 12

Setup (Claude): the build with A65 and A66 deployed on the same world (snapshot es1 still held), VerboseLogging on.
The es-* events from part A are still in events.json; es-castle already has allowTerritory true.

1. Connect to **127.0.0.1:9876** (Nyar Dev) with your admin character. Open the console (the ~ key), enter
   `adminauth`, and close it.
2. **Chat text (A65).** Send each of these. Each should be refused, and the reply should show whole, with nothing
   missing between the dots or at the end:
   - `.nyar event set es-mod action.units.1.chance 2` → "action.units.N.chance must be 0.05-1.0 with at most two
     decimals"
   - `.nyar event set es-mod action.behaviour "hunt 20 30"` → "action.behaviour takes none or hunt RANGE"
   - `.nyar event set es-around location "aroundplayer 20"` → "location takes here or aroundplayer MIN MAX"
3. **Claimed territory allowed (redo of step 12).** Go inside your castle, within its floor. Run
   `.nyar event set es-castle location here`, then `.nyar event start es-castle`. Expect three Thugs inside the castle.
   Then run `.nyar event stop es-castle` (check the spelling: **es**-castle) right away. Expect them to vanish within a
   few seconds.
4. **Hunt (redo of step 10).** Leave your castle's territory entirely: the plot reaches well past the walls, so open
   the map and stand outside your castle's territory outline, at least 100 m from any castle. Pick a spot with a
   building, rock or hill you can hide behind about 40 m away. Stand on the spot and run
   `.nyar event set es-hunt location here`. Walk about 40 m away, out of sight behind the cover, still outside any
   castle's territory. Run `.nyar event start es-hunt`. Then run `.nyar debug here 45` and `.nyar debug here 35`.
   Stay out of sight and don't attack. Expect the Thugs to come to you within about 15 seconds. Tell me whether they
   came. Then run `.nyar event stop es-hunt`.
5. Stay connected for about 2 minutes, so the server autosaves after these steps. Then tell me you're done, and
   paste anything that differed from an expectation.

### Session 1 · 2026-09-29 · event-spawns step 2, part B (b918028 plus the uncommitted A65 and A66, with the owner)

The A65-A66 build before Codex round 1's fixes (the Hunt line without the over-the-cap count; Left without its guard),
deployed on the same world; owner connected about 10:58–11:03; server stopped after autosave 1997, both logs copied to
%TEMP%\nyar-es1b-logs.

- [x] step 2 (A65, D32): the three refusals whole in chat: "action.units.N.chance must be 0.05-1.0 with at most two
  decimals", "action.behaviour takes none or hunt RANGE", "location takes here or aroundplayer MIN MAX"
- [x] step 3 (D17, redo of part A step 12): with allowTerritory true, "event es-castle wave 1/2: 3 units queued (0 moved, 0
  unchecked)" at the part A castle point (the owner's `location here.` with a period was refused with the location rule,
  so the point stayed); the owner saw the Thugs inside the castle; stop → "3 units queued", "despawn batch: 3 of 3
  destroyed"
- [x] step 4 (D13, redo of part A step 10), outside any claimed territory: the first Hunt tick "1 players read, 1 targeted;
  left out: 0 dead or unreadable, 0 in claimed territory, 0 in PvP combat, 0 out of range" (A66) wrote the seeds; the next
  "0 seeds kept, 3 left to the game": the game took over the three entries (D13: a changed entry is the game's). One Thug
  came at the owner. One stood on a raised floor ("regrouped CHAR_Bandit_Thug from height 5.0 to its centre at height
  0.0", A23) and the owner saw it on a raised castle plot nearby; one had other stats in `debug here` (hp 65/65 pp 22, a
  game buff, not ours: es-hunt has no modifiers) and "CHAR_Bandit_Thug of es-hunt died" (A66) about 25 s after the start,
  with a Skeleton Golem and its minions fighting 29–36 m away. The stop queued the two left ("2 units queued", "2 of 2
  destroyed"). Whether a unit reaches the player is the game's pathing once it holds the aggro; step 3's Session 2
  watches where the ring points of a wave near a raised plot land
- [x] part A's open items: step 3 passes with A65 (step 2 above); step 10 passes (step 4 above); step 12 passes (step 3 above)
- [x] logs: no [Error]; BepInEx warnings only the known kinds and the two planted invalid events at each reload; the
  server log's 224 "PrefabLookupMap.TryGet … is in an unknown state" at save load, the known set. Tick timing: every
  average ≤ 2.6 ms; two single slow ticks, 114.0 ms and 85.2 ms, each in an idle window (0 events, 0 tracked) whose only
  line is a GameTime day/night edge, while the other eight edges of parts A and B cost under 1 ms; a pause of the host
  or the runtime, as REGIONS.md and FACTION_EMPOWERMENT.md recorded before; part A's 85.6 ms tick is the one after a
  3-unit despawn batch. Step 3's soak measures the budget

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
