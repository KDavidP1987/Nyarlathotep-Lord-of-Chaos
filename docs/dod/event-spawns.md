---
dod: 2
rubric: 2
id: dod-20260928-evs1
slug: event-spawns
title: Event spawns — walkable points, modifiers, behaviours, ambush and player locations
status: draft
size: L
parent: nyarlathotep
kind: feature
created: 2026-09-28
baselined: none
closed: none
commit: c67d5df
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: pending
review: pending
---

# DoD: Event spawns — walkable points, modifiers, behaviours, ambush and player locations

**Size:** L. The plan touches several modules:
- **Logic:** Model, Validation, CommandArgs, Authoring, a new Spawning.cs holding the spawn-point, chance, tuning, hunt, player-pick and territory planners, Engine, Precedence, Messages.
- **Services:** SpawnTracker, UnitSetup, WaveAction, and new services WalkCheck, TerritoryMap and HuntAction.
- **Commands:** EventCommands, DebugCommands.
- **Resources:** templates.json.
- **tools:** preflight checks and fixtures, rollback-drill.ps1.

It widens the events.json SpawnWaves action schema with six optional keys and one location type. It ships two releases: 0.5.1 (walkable spawn points) and 0.6.0 (the rest). It adds no dependency and no data file.

**Planned:** interactively. This plan is a child of the approved Epic `nyarlathotep`. Its `## Child constraints` › event-spawns entry, Business rules 3, 4, 6 and 9, S-10 and D12 govern it. The owner settled four scope decisions in plan mode on 2026-09-28 (S-1 to S-4). The assumptions marked reversible below are decisions taken inside that scope without asking, each with its fallback. The build starts only after event-library releases 0.5.0 (S-5).

**Request:** the owner's decisions of 2026-09-28 (plan mode, all four recommendations taken):
1. 0.6.0 carries the full child constraint minus the locations owned by other pillars: per-unit spawn chance, modifiers (level or levelDelta clamped ±5, maxHealth, power, moveSpeed, attackSpeed), loot on or off, behaviours Guard and Hunt, the AroundPlayer location, an optional spawn visual and the claimed-territory refusal, all settable in chat. Zone, AroundBoss and CastleOf stay with defended-zones, boss-reinforcements and sieges.
2. Stealth ambush is a third behaviour, Ambush, inside this child, behind a probe.
3. The walkability fix (event-library A27, a wave unit stuck in a pond) is this plan's first step, released alone as 0.5.1.
4. Hunt seeds aggro on players near the wave; AroundPlayer picks a random eligible online player outside every castle, and announcements never name the player.

## Definition of Done
- [ ] D1 · **Walk probe decides the check** a temporary admin command `.nyar debug walk [radius]` reads the game's static tile collision at the admin's position through Services/WalkCheck.cs (radius per D28) and replies one line "walk <x> <z> h <heightLevel> r <radius>: <free|blocked> (<source>)", source naming the map data used; Session 1 (the owner) records the reply on dry open ground, standing in the pond of Session 7 (event-library Test results), in a river, against a cliff face and inside a building, and the feature doc records a go/no-go line: go when every dry reading is free and every pond, river, cliff and building reading is blocked; on no-go 0.5.1 is not released: D2-D5 are removed by a discovered amendment, placement stays as in 0.5.0, the README keeps the known-issue note, and the owner decides in plan mode whether another check is probed (S-6); the command is removed before any release ships (D22's debug-command check); the go/no-go check's cases: it reports no-go on any dry reading blocked or any pond, river, cliff or building reading free, stays silent (go) when the five readings match their places, and on an empty input (fewer than five readings, or a reply without its free or blocked word) the record reads "go/no-go: incomplete", which is neither go nor a pass, and the session is repeated; D27's -SessionsOf probe-record check fails a Session 1 without five "walk …: free|blocked" lines and one "go/no-go: go|no-go" line · manual: Session 1 records the five replies and the go/no-go line in docs/features/EVENT_SPAWNS.md › Test results
- [ ] D2 · **Walkable point planner** Logic/Spawning.cs `SpawnPoints.Choose(point, centre, radius, isFree)` returns the ring point when isFree(point); else tries, in order, the 11 other angles of 12 on the same ring, then the same 12 angles at half the radius, then the centre, returning the first free one with its kind (ring, moved, centre); when none is free it returns the centre with kind "unchecked"; it calls isFree at most 25 times and never with a point farther than radius from the centre · test: Nyarlathotep.Tests SpawningTests SpawnPoints (fails when: a free ring point is moved, a blocked point is kept, the search order differs, a point outside the radius is tried, isFree is called more than 25 times, or an all-blocked ring returns other than the centre with kind unchecked)
- [ ] D3 · **Waves spawn on walkable ground** SpawnTracker.RequestWave passes each ring point through SpawnPoints.Choose with WalkCheck.IsFree at the centre's height level; a moved or unchecked point is counted in the wave line "wave <n> of <id>: <k> units (<m> moved, <u> unchecked)" · manual: Session 2 starts bandit-ambush with its centre on the shore of the Session 7 pond (radius 10 reaching the water) three times; no unit stands in water (`.nyar debug here` shows each unit's position and the owner confirms none is stuck), and the wave lines show moved > 0
- [ ] D4 · **Walk check failure is harmless** WalkCheck.IsFree that throws, or finds no map data, counts as free (the ring point is used as before 0.5.1), logs "walk check unavailable: <reason>" once per failure streak and never stops a wave; a unit left in water still despawns at its due time (Epic Business rules 3) · test: Nyarlathotep.Tests DependencyFailureTests WalkCheck (fails when: a throwing isFree stops the wave, a point is dropped, the log line repeats within a streak, or a recovered check is not used again)
- [ ] D5 · **Release 0.5.1** csproj Version and thunderstore.toml versionNumber are 0.5.1; both changelogs describe the walkable spawn points and remove 0.5.0's known issue; both READMEs drop the known-issue note; the annotated tag v0.5.1 is pushed and the GitHub pre-release carries the tcli zip; no tcli publish, the owner publishes · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan event-spawns → "rollback gate: 3/3"; then pwsh tools/release-verify.ps1 -Tag v0.5.1 -Asset kdpen-Nyarlathotep-0.5.1.zip → "release verify: hashes equal" (fails when: 0.5.0 does not initialize on 0.5.1's files or the repository revert of v0.5.0..v0.5.1 is not clean, a surface differs, the tag is missing or unpushed, the release has no zip, a README still carries the pond note, or the hashes differ)
- [ ] D6 · **New action keys validated** ParseSpawnWaves accepts, besides today's keys, the optional keys `modifiers`, `loot`, `behaviour`, `spawnVisual` and `allowTerritory`, a per-unit `chance`, and location type `AroundPlayer`; each failure disables the event with one reason naming the field: units[].chance a number 0.05-1.0 (absent 1.0); modifiers an object with keys among level (integer 1-120), levelDelta (integer -5..5), maxHealth, power, moveSpeed, attackSpeed (each a number 0.5-3.0 with at most two decimals), level and levelDelta never both, an empty object refused; loot and spawnVisual and allowTerritory JSON booleans (absent false); behaviour `{ "type": "Guard", "leash": 10-80 }`, `{ "type": "Hunt", "range": 10-60 }` or `{ "type": "Ambush" }`; location `{ "type": "AroundPlayer", "minDist": 10-60, "maxDist": 15-80 }` with minDist < maxDist, allowed with every trigger; an unknown key stays "unknown field action.<key>" · test: Nyarlathotep.Tests EventValidationTests Spawns (fails when: a chance of 0.04, 1.01 or "0.5", a levelDelta of 6 or 2.5, level and levelDelta together, a multiplier of 0.49, 3.01 or 1.234, an empty modifiers object, loot "true", a Guard leash of 81, a Hunt range of 9, an unknown behaviour type, an AroundPlayer with minDist 40 and maxDist 30, or an unknown key is accepted; or a definition using every new key validly is disabled; or a 0.5.0 definition without the new keys loads differently)
- [ ] D7 · **Ambush needs a hiding unit** a behaviour Ambush is valid only when every units[].prefab has a Hiding spawn state (IUnitCatalog.HidingBuff(prefab) returns the buff the prefab's SpawnBuffElement lists with Kind Hiding); otherwise the event is disabled with "behaviour Ambush needs units that can hide: <prefab> cannot"; the catalog reads the prefab's SpawnBuffElement buffer once per prefab, and at boot logs "ambush: <n> units can hide (<k> without entity)" from Logic `HidingIndex.Build(names, readHiding)`, where names are every key of PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary starting "CHAR_" (the same map SpawnTracker.Spawn resolves prefabs from, so a name outside it can never spawn), read once after GameDataInitialized (Core.IsReady); a name whose prefab entity is missing is counted in k and cannot hide · test: Nyarlathotep.Tests EventValidationTests Ambush and HidingIndexTests (fails when: HidingIndex omits a CHAR_ name whose buffer lists Hiding, includes a non-CHAR_ name, counts a missing entity as hiding or omits it from k, a unit without a Hiding entry is accepted under Ambush, a CHAR_Bandit_Thug or CHAR_Blackfang_Lurker is refused, or the reason does not name the first unit that cannot hide)
- [ ] D8 · **Chance roll** Logic/Spawning.cs `WaveRoll.Expand(units, rng)` rolls each copy of each entry independently against its chance with the injected IRandom and returns the prefabs to spawn in entry order; the count after the roll, not before, is what WavePlan.Split clamps by MaxUnitsPerWave and MaxTrackedUnits; a roll of zero units logs "wave <n> of <id>: 0 units rolled" and spawns nothing · test: Nyarlathotep.Tests SpawningTests WaveRoll (fails when: a chance-1.0 entry loses a copy, a chance entry's copies are rolled as one, the order changes, the caps clamp the pre-roll count, or a zero roll spawns or throws)
- [ ] D9 · **Modifier recipe** Logic/Spawning.cs `TuningFrom(modifiers)` gives the UnitTuning every spawned unit of the event gets: Level = level, or the prefab level plus levelDelta resolved on the unit and clamped 1-120; maxHealth → MaxHealth; power → PhysicalPower and SpellPower; moveSpeed → MovementSpeed; attackSpeed → PrimaryAttackSpeed and AbilityAttackSpeed; each a MultiplyBaseAdd modifier of value multiplier − 1 on the unit's marker buff (UnitSetup.StatModifiers), a multiplier of 1.0 giving no entry; no modifiers gives UnitTuning.None; `.nyar spawn`'s tuning keeps its arguments and is built by the same function · test: Nyarlathotep.Tests SpawningTests Tuning (fails when: a stat maps to another UnitStatType, a value is not multiplier − 1, power or attackSpeed gives other than two entries, a 1.0 gives an entry, levelDelta resolves from anything but the prefab level, a resolved level leaves 1-120, or an event without modifiers gets a non-None tuning)
- [ ] D10 · **Modifiers verified in game** (profile note 6.1) for each of level, levelDelta, maxHealth, power, moveSpeed and attackSpeed, a unit of a modified wave reads in `.nyar debug here` its plain reading × the multiplier (±1 %), or the set or resolved level; a modifier whose reading does not change is removed from D6 and D9 by a discovered amendment before 0.6.0 · manual: Session 3 records each reading pair against a plain wave of the same prefab
- [ ] D11 · **Loot follows the event** SpawnTracker clears DropTableBuffer only when the order's Loot is false (the default, Epic S-10); a loot true unit keeps its drop table · test: Nyarlathotep.Tests SpawningTests Loot (the order recipe's ClearDrops flag; fails when: an order without loot keeps its drops or a loot true order clears them); manual: Session 3 kills five units of a loot true wave and five of a loot false wave of CHAR_Bandit_Thug; the first drop at least one item, the second none
- [ ] D12 · **Guard holds its ground** a Guard wave's units get AggroConsumer.PreCombatPosition = their spawn point and MaxDistanceFromPreCombatPosition = leash, written by UnitSetup at spawn · test: Nyarlathotep.Tests SpawningTests Behaviour (the recipe's PreCombat and Leash fields; fails when: a Guard order lacks either, a non-Guard order sets them, or the leash differs from the definition); manual: Session 3 pulls a Guard 20 wave 40 m away and records the units returning to within 20 m of their spawn points
- [ ] D13 · **Hunt seeks nearby players** while a Hunt wave has live units, Services/HuntAction.cs every 5 s adds one AggroBuffer entry per unit for each target of Logic `HuntPlan.Targets(players, centre, range)`: online, alive player characters within range of the wave centre, nearest first, at most 5; no entry is added twice for the same unit and player; seeding stops at the wave's end, stop, purge or restart · test: Nyarlathotep.Tests SpawningTests Hunt (fails when: a dead, offline or out-of-range player is targeted, a sixth target is added, the order is not by distance, a duplicate entry is planned, or a plan is made for an ended wave); manual: Session 3 stands 40 m from a Hunt 60 wave centre out of sight and records the units coming to the owner
- [ ] D14 · **Ambush probe and behaviour** Session 3 first probes: a CHAR_Bandit_Thug spawned with its HidingBuff (D7) applied at spawn by UnitSetup is hidden (weapon hidden, no health bar) until the owner comes within its aggro range or attacks it, then reveals and fights; the feature doc records go or no-go; on go an Ambush wave's units all spawn hidden and reveal by either path; on no-go Ambush is removed by a discovered amendment (-D14, ~D6, ~D7) and bandit-ambush keeps no behaviour; with Debug.VerboseLogging on, D7's boot line is followed by "ambush units: <names, sorted>", and the sorted name list must equal the sorted list of CHAR_ files of Reference Data/Prefabs whose SpawnBuffElement has a Hiding entry (36: 32 Bandit, 4 Blackfang); the record holds both lists and their `diff`, which must be empty; a non-empty diff fails the item until a discovered amendment names and classifies each differing prefab; the ambush probe's cases: no-go when the thug is visible at spawn or does not reveal on approach or on attack, silent (go) when it is hidden at spawn and reveals by both paths, and an empty input (no probe run, or an ambush-units line with no names) records "ambush probe: incomplete", never go; D27's -SessionsOf probe-record check fails a Session 3 without one "ambush probe: go|no-go" line and one "ambush diff: <n> lines" line · manual: Session 3 records the probe, the boot count and, on go, two trials of an Ambush wave of bandit-ambush: the owner walks in from 30 m without attacking (reveal on approach, recording the distance), and on a fresh wave attacks one unit with a ranged weapon from 20 m (reveal on attack)
- [ ] D15 · **Spawn visual** spawnVisual true applies Buff_General_Spawn_Unit_Fast_WarEvent (-133411573) to each unit 0.25 s after it spawns through the SpawnTracker tick queue; spawnVisual false applies nothing; the visual buff is never applied to a unit no longer tracked · test: Nyarlathotep.Tests SpawnLedgerTests Visual (fails when: a visual is queued for a false order, applied before 0.25 s, or applied after the unit left the ledger); manual: Session 3 sees the spawn effect on a visual wave
- [ ] D16 · **Player pick for AroundPlayer** Logic `PlayerPick.Choose(players, rng)` picks uniformly among online, alive player characters that are not inside claimed territory (D17) and not in PvP combat (InCombatBuff_PvP); the wave centre is at a random angle and a distance in minDist..maxDist from the player, at the player's height; no eligible player skips the wave with "wave <n> of <id> skipped: no eligible player"; the pick is made once per wave when the wave is queued and the centre it gives is fixed for that wave: a player who dies, disconnects, enters combat or enters claimed territory afterwards changes nothing about the queued wave, whose ring points are still checked against the wave's territory set (D17); no message, log line or api row names or locates the player (the log shows "around a player") · test: Nyarlathotep.Tests SpawningTests PlayerPick and PrivacyTests (fails when: an offline, dead, in-territory or PvP-combat player is picked, the distance leaves minDist..maxDist, a no-player wave spawns, a queued wave's centre changes when the player's state changes, or a message builder or log line takes the player's name or position)
- [ ] D17 · **Claimed territory refused** Services/TerritoryMap.cs builds, once per wave, the block coordinates of every CastleTerritory entity that a CastleHeart names (CastleHeart.CastleTerritoryEntity, whatever the heart's state, decaying included; a territory no heart names is unclaimed) (block = floor((floor(x × 2) + 6400) / 10) per axis, KindredCommands' CastleTerritoryService conversion, S-7); Logic `Territory.IsClaimed(blocks, x, z)` decides; a wave whose centre is claimed is skipped with "wave <n> of <id> skipped: centre in claimed territory" unless allowTerritory is true; a claimed ring point is treated as blocked by SpawnPoints.Choose (D2) unless allowTerritory; a map that cannot be built skips the wave with "territory unknown" (fail closed); territory is read once per wave, so a castle claimed while a wave spawns is seen by the next wave (the 7.2 and 2.3 policy), and both READMEs and docs/features/EVENT_SPAWNS.md state it at 0.6.0 (D26) · test: Nyarlathotep.Tests SpawningTests Territory and DependencyFailureTests Territory (fails when: a point in a listed block is free, a point outside every block is claimed, a territory without a heart is claimed, the conversion differs at a negative coordinate or a block edge, an allowTerritory wave is refused, or a failed map build spawns, or a map built for one wave is reused by the next); manual: Session 3 sets a Point inside the owner's castle and records the skip line, then allowTerritory true and records the wave
- [ ] D18 · **Chat fields for the new keys** `.nyar event set <id>` takes: action.modifiers.<level|levelDelta|maxHealth|power|moveSpeed|attackSpeed> <value|none>, action.loot <true|false>, action.spawnVisual <true|false>, action.allowTerritory <true|false>, action.behaviour <none|guard <leash>|hunt <range>|ambush>, action.units.<n>.chance <0.05-1.0>, and action.location aroundplayer <minDist> <maxDist>; each value obeys D6's range and is refused with its rule otherwise; a set that would give level and levelDelta together is refused; each is a whole-file edit through the event-library editor with one .bak and a reload; an Empower definition refuses them all · test: Nyarlathotep.Tests AuthoringTests Spawns and CommandArgTests Spawns (fails when: an out-of-range value, a second level form, a behaviour ambush on a non-hiding unit or a field on an Empower definition is written, or a valid set does not change the file)
- [ ] D19 · **Template examples** undead-rising gains `"modifiers": { "levelDelta": 2, "maxHealth": 1.2 }` and `"behaviour": { "type": "Guard", "leash": 30 }`; bandit-ambush gains `"behaviour": { "type": "Ambush" }` when D14 is go; both stay disabled and valid ("templates: 6/6 valid") · test: Nyarlathotep.Tests TemplateLibraryTests StarterTemplates (fails when: either template lacks its new keys, a template fails validation, or one ships enabled)
- [ ] D20 · **Wire unchanged** the new keys add no status field, push kind or api command; Wire.Api stays 3 and docs/RAPHAEL_INTEGRATION_CONTRACT.md is unchanged; a skipped wave (no player, claimed territory) sends no wave push; `.nyar api events` lists a definition with the new keys as before · test: Nyarlathotep.Tests ContractDocTests and PushTests Spawns (fails when: api is not 3, a skipped wave pushes, or an events row carries a new field)
- [ ] D21 · **Dependency failures** a throwing modifier, loot, behaviour or visual write inside the unit recipe discards the unit as today (SpawnTracker.Prepare, Abandon) and logs once per streak; a HuntAction query or AggroBuffer write that throws is caught, logged "hunt <id>: seed failed" once per streak, and the wave continues without hunting; a HidingBuff that cannot be applied spawns the unit unhidden with "ambush <id>: hide failed" once per streak; a player query that throws skips the wave like no eligible player; garbage values are refused where read: a HidingBuff guid with no prefab entity means cannot hide (D7), a CastleTerritory block outside the map's 0-1279 block range is ignored and counted in the verbose log, and a player position that is NaN or outside ±10000 makes that player ineligible (D16); every game call is synchronous and in process, so none can be slow beyond the tick, which the slow-tick warning names (event-library D36); TerritoryMap fails closed (D17) and WalkCheck fails open (D4); a unit whose recipe fails leaves the wave's units already spawned tracked and on their due time, and its queued visual (D15) is dropped; SpawnLedger and the event-library editor run in process with no latency: an exception from either lands in the event's try/catch, which cancels the event after 3 faults (Epic D25), and a failed editor write changes nothing on disk (event-library D19) · test: Nyarlathotep.Tests DependencyFailureTests Spawns, also run by pwsh tools/preflight.ps1 -DependencySuite (fails when: a failing unit untracks the units spawned before it, a discarded unit's visual is applied, a NaN player position is picked, an out-of-range block is kept, a guid without an entity counts as hiding, any of these throws escapes the event's try/catch, a failing unit is kept tracked without its recipe, a line repeats within a streak, a hunt failure ends the wave, or a player-query failure spawns)
- [ ] D22 · **Authorization and static checks** every new [Mutating] method (HuntAction, the new UnitSetup writes, WalkCheck is read-only) is called only from SpawnTracker, EventRuntime or Gateway.Run, HuntAction joining $DispatchedServices in tools/preflight.ps1; the gateway check enumerates Nyarlathotep/Nyarlathotep/{Commands,Patches,Services}/**/*.cs through Get-TreeFiles (git ls-files --cached --others --exclude-standard, so a new untracked file is seen and only git-ignored build output is not); the new event set fields go through SetEventField; `debug walk` is admin-only and absent from the 0.5.1 and 0.6.0 DLLs (preflight "debug commands: none temporary"); every new `.nyar` command or subcommand is adminOnly (the auth suite's admin list); ControlCases.Table has a row for each new test control (D2, D4, D6, D7, D8, D9, D11, D12, D13, D15, D16, D17, D18, D21, D28, D29, D30, D33) · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)", the suite now also running D34's EntityWrites check, so one command fails when any path loses its authorization control; then pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests"; then pwsh tools/preflight.ps1 → PREFLIGHT OK with "debug commands: none temporary" (fails when: a new [Mutating] method is called outside those callers, `debug walk` is registered in a release build, a control lacks its ControlCases row, a fixture that plants a registered `debug walk` in Commands/ passes, a new command without adminOnly passes, or the new fixture GatewayOnly/bad-new, a [Mutating] method in a new untracked Services/Stray.cs, passes)
- [ ] D23 · **End paths leave nothing** (profile note 7.3) a modified Hunt wave of 30 units with spawnVisual and loot false leaves 0 tracked units and no Hunt seeding after each end path: natural end (despawn within GraceSeconds plus the drain), `.nyar event stop`, `.nyar purge confirm` (Epic D11), a restart mid-wave (boot sweep "boot marker sweep: <n> found" then "0 found"), and the Epic D12 uninstall (delete the DLL, restart, wait the longest unitLifetime; the reinstall boot logs "marker sweep: 0 found") · manual: Session 4 records each path's log lines and `.nyar status`; the Epic Log gets the D11 and D12 pass lines
- [ ] D24 · **Tick budget with behaviours** with Debug.TimingLog on, while Hunt and Guard waves at the default caps (MaxTrackedUnits 150) are live and at least one player stands within Hunt range, the copy of Session 4's BepInEx/LogOutput.log holds, after the first health line showing at least 140 tracked units, a skipped warm-up window and then ten consecutive "tick timing: avg <a> ms, max <m> ms over <n> ticks" lines (one per 60-tick window, foundation TickTimer) each with a < 5 ms (Epic D24), every health line in that span showing at least 140 tracked, and no "slow tick" line (event-library D36) between the first and the last of the ten · cmd: pwsh tools/preflight.ps1 -TimingSpan <log copy> -MinTracked 140 -Windows 10 → "timing span: 10/10 windows under 5 ms, tracked >= 140, 0 slow ticks" (fails when: fewer than ten consecutive windows follow the warm-up, a window's average is 5 ms or more, a health line in the span shows fewer than 140 tracked, a slow-tick line lies in the span, or the log has no qualifying health line, which prints "timing span: no span" as a failure; fixtures TimingSpan/{good,bad-avg,bad-slow,bad-gap,empty} are copies of real timing and health lines)
- [ ] D25 · **Rollback drill across new keys** tools/rollback-drill.ps1 accepts that 0.5.1 disables definitions 0.6.0 understands only when every extra disabled definition's reason is "unknown field action.<key>" for a key of D6 or "action.location must be …" for an AroundPlayer location; it prints "(<k> newer keys)"; its selftest gains a pair where 0.5.1 disabled a definition with modifiers (pass) and one disabled for another reason (fail) · cmd: pwsh tools/rollback-drill.ps1 -SelfTest → "drill selftest: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.1 -To v0.6.0 -Plan event-spawns → "rollback gate: 3/3"; then pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.6.0 -Plan event-spawns → "rollback gate: 3/3", covering both releases in one run, 0.5.0 loading 0.6.0's written files with every new-key definition disabled for an unknown field and every other definition valid (fails when: either range's revert is not clean, 0.5.0 or 0.5.1 does not initialize on 0.6.0's files, a count difference with another reason passes, or 0.5.1 does not initialize on 0.6.0's files)
- [ ] D26 · **Release 0.6.0** csproj Version and thunderstore.toml versionNumber are 0.6.0; both changelogs and both READMEs describe the new keys, behaviours, locations, chat fields and the territory rule, and the READMEs say the tick budget is promised at the default caps and that raised caps (151-500 tracked) are best effort, watched by the slow-tick warning; the annotated tag v0.6.0 is pushed and the GitHub pre-release carries the tcli zip; Epic D12 passes again (D23); no tcli publish · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/release-verify.ps1 -Tag v0.6.0 -Asset kdpen-Nyarlathotep-0.6.0.zip → "release verify: hashes equal" (fails when: a surface differs, the tag is missing or unpushed, the release has no zip, or the hashes differ)
- [ ] D27 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/EVENT_SPAWNS.md › Test results, with its log-check line in docs/audits/event-spawns.md from -LogCheck before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; every path this child writes is in tools/paths-manifest.txt and declared by Rollout › Paths walked · cmd: pwsh tools/preflight.ps1 -AuditOf event-spawns → "audit steps: event-spawns 6/6 pre, 6/6 post, 6/6 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf event-spawns → "session logs: event-spawns <n>/<n> checked; probe records <p>/<p>" (the probe records of tools/preflight-checks.json `probeRecords`: Session 1's five walk lines and go/no-go line, D1; Session 3's ambush probe and ambush diff lines, D14); pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns → "paths: <n> walked, all in manifest; declared: <d>/<d>; data tests <t> passed", the last part running through Invoke-ClassTests the test classes tools/preflight-checks.json `dataTests` lists for the slug (EndPathTests, D33), so persisted artifacts and runtime state are one command for probe 3.3 (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a walked path matches no manifest glob, a written path is undeclared, a %TEMP%\nyar-* folder remains after its session ("paths: leftover temp <name>"), a Design › Data row or a manifest `temp:` or `server:` glob has no data-inventory entry or an empty field, or a tools/ line takes the %TEMP% root without naming a declared nyar-<name> folder or a '# nyar-temp:' registration (the existing unmarked-temp-root scan, fixture Paths/bad-tempvar: $env:TEMP, GetTempPath(), New-TemporaryFile, Python's os.environ and os.getenv TEMP, tempfile.gettempdir, mkdtemp, mkstemp, TemporaryDirectory and NamedTemporaryFile, Node's process.env.TEMP and os.tmpdir), the `dataTests` entry for the slug is missing or empty ("paths: event-spawns names no data tests", fixture Paths/bad-datatests), a listed class runs zero tests or one fails, a probe record is missing a reading or its go/no-go line (fixtures SessionLogs/bad-probe and bad-probe-2), or a probe record reads "incomplete")

- [ ] D28 · **Walk radius argument** `.nyar debug walk [radius]` parses radius with Logic CommandArgs.WalkRadius: absent 0.5, a decimal 0.1-5 in invariant culture with at most two decimals; "0", "5.01", "1,5", "abc" or a second argument reply "radius must be 0.1-5" and read nothing · test: Nyarlathotep.Tests CommandArgTests WalkRadius (fails when: an absent radius is not 0.5, 0.1 or 5 is refused, 0.09, 5.01, "1,5", "abc" or an extra argument is accepted, or a refused radius calls the check)
- [ ] D29 · **Wave precedence order** Logic/Spawning.cs `WaveGate.Decide(blocked, playerFound, centreClaimed, allowTerritory, rolled, caps)` gives each wave one outcome, in this order: a Precedence.StartBlocker control or an ended event (no wave); no eligible player for AroundPlayer (skip); centre in claimed territory without allowTerritory (skip); the chance roll (zero units → "0 units rolled"); then WavePlan.Split's MaxUnitsPerWave and MaxTrackedUnits clamps; allowTerritory lifts only the territory skip; WaveAction takes the outcome from WaveGate and decides nothing itself · test: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WavePrecedence → "Passed!" with at least 12 tests (fails when: any two rules are swapped, allowTerritory lifts a cap or a player skip, a skipped wave rolls or spawns, a control-blocked wave reaches the roll, or the filter runs fewer than 12 tests)
- [ ] D30 · **Health shows spawn trouble** HealthMonitor.Degraded adds Logic SpawnHealth.Entries: "spawns: walk check unavailable" while WalkCheck's failure streak is open (D4) and "spawns: territory unknown" from a wave skipped for it until a map builds again (D17), and, while the streak of each D21 class is open, "spawns: unit setup failing (<id>)", "spawns: hunt seed failing (<id>)", "spawns: ambush hide failing (<id>)" and "spawns: player query failing (<id>)" (Failure & observability › What the admin sees); the 10-minute health line, `.nyar status` for admins and the admin login notice carry them (foundation D31), so an admin learns of it on login without reading the log · test: Nyarlathotep.Tests HealthTests Spawns (fails when: an open streak of any of the six classes gives no entry, a recovered check keeps its entry, or an entry's text differs from the table)
- [ ] D31 · **Secrets and privacy** this child adds no credential: gh keeps its token in the Windows credential store (gh auth), TCLI_AUTH_TOKEN exists only in the owner's environment while the owner publishes and is never written to disk, and rotation is the owner's in the GitHub and Thunderstore settings; no log line, message, record or artifact holds a token, a SteamID or a player position · cmd: pwsh tools/preflight.ps1 → "secrets: none" (the Secrets check over the tracked and untracked tree plus dist/ and build/, where the tcli zip is built); then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern itself (the audits' privacy-grep records and this item) ; the Secrets check also fails on any read of TCLI_AUTH_TOKEN in a tracked .ps1, .py, .mjs or .cs file (only the owner's own shell reads it; docs may name it), and the release step builds the zip with $env:TCLI_AUTH_TOKEN set to a sentinel value and fails if the sentinel appears in the zip, in dist/ or build/, or in the build log (fails when: the planted Secrets fixtures, a tss_ token, a tools/ script calling `gh auth token` and the new fixture Secrets/bad-envread (a tools/ script writing $env:TCLI_AUTH_TOKEN to a file), pass -SelfTest, the sentinel reaches an artifact or log, a tracked file or the built zip holds a token shape, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D32 · **Chat lines in real chat** (profile note 11.2) every new reply is rendered in the game's chat before its release: D18's set replies and refusals, D6 and D7 reasons as `.nyar event list` shows them, and `debug walk`'s line; each is under 480 bytes, wraps in the chat window without losing its reason, and carries no meaning by colour; chat is typed and read with the keyboard and the game offers no screen reader, a limitation inherited from V Rising that the README states · manual: Sessions 1 and 3 record each rendered line as the owner's pasted chat

- [ ] D33 · **End paths empty the state** Logic `WaveLifecycle` drives the SpawnLedger, the hunt seed set and the visual queue together, and each end path leaves all three empty for the event: natural end (after GraceSeconds and the despawn drain), `event stop`, fault cancel, `purge confirm`, and a restart (a new ledger from the boot sweep, empty seed set and visual queue); a unit whose despawn throws stays queued until it goes (foundation) · test: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter EndPaths → "Passed!" with at least 5 tests ; with two AroundPlayer Hunt events on the same player, stopping one empties only that event's units, seeds and visuals and leaves the other's centre, ledger rows and seeds unchanged (fails when: any end path leaves a tracked unit past its drain, a hunt seed or a queued visual for the event, stopping one of two events touches the other's state, a restart keeps a seed or visual, an event with no units reports other than 0 tracked, or the filter runs fewer than 5 tests)
- [ ] D34 · **Entity writes stay in services** a new preflight check finds every entity write call, independent of any annotation (the patterns `.Write(`, `.Write<`, `AddComponentSafe`, `RemoveComponentSafe`, `AddBufferSafe`, `DestroySafe`, `RemoveBuffSafe`, `GetBuffer<` followed by `.Add(`, `.Clear(` or an indexer write in the same statement, `InstantiateEntityImmediate`, `TryInstantiateBuffEntityImmediate`, and any `EntityManager.` call whose name begins Set, Add, Remove, Destroy, Instantiate or CreateEntity, and a `DynamicBuffer<` local written by .Add, .Clear, .RemoveAt or an indexer later in the same method; by Review 4 also `.ValueRW` (RefRW<T> writes), an indexer assignment on a ComponentLookup<, BufferLookup< or ComponentDataFromEntity< local, an EntityCommandBuffer or `CommandBuffer` receiver's AddComponent, SetComponent, RemoveComponent, DestroyEntity, AppendToBuffer, SetBuffer, AddBuffer or Instantiate, and SystemAPI.SetComponent, SetBuffer or SetComponentEnabled; excluded forms are none: an `unsafe` block or a GetUnsafePtr, GetUnsafeReadOnlyPtr or UnsafeUtility call anywhere in the tree fails the check outright, since a pointer write cannot be classified) in Get-TreeFiles' .cs files, and fails when one appears outside $DispatchedServices and EntityExtensions.cs, or inside a non-private method of a dispatched service that is not [Mutating]; so removing [Mutating] from a writing method fails the check instead of hiding it from the gateway check · cmd: pwsh tools/preflight.ps1 → "entity writes: only dispatched services (<n> sites, <m> [Mutating] methods)"; pwsh tools/preflight.ps1 -SelfTest (fails when: fixture EntityWrites/bad plants a `.Write(` in Commands/DebugCommands.cs, bad-new a write in a new untracked Services/Stray.cs, bad-unmarked a public writing method of SpawnTracker.cs without [Mutating], bad-em an `EntityManager.SetComponentData` in Commands/, bad-split a `DynamicBuffer<` local taken in one statement and cleared in another in a new Services/Stray.cs, bad-refrw a `.ValueRW` assignment in Commands/, bad-ecb an EntityCommandBuffer.SetComponent in Commands/, bad-lookup a ComponentLookup indexer write in Commands/, bad-unsafe a GetUnsafePtr call in a service, and any of them passes; good is a copy of the real services; empty has no .cs file and prints "entity writes: no source files" as a failure)

## Purpose & typical use
- **Who:** a server admin who wants the world to strike back with more than a crowd of stock NPCs. They'd say "at nightfall, a hunting pack of level +3 Cursed wolves goes after whoever is near the Witch's cottage", or "bandits lie hidden on the road and jump the next player who passes". Players meet the waves; they don't configure them.
- **Job:** waves that spawn on ground a unit can walk on, at a chosen strength, that guard a place, hunt nearby players or hide in ambush, around a fixed point, the admin, or a random player out in the world, never inside a castle unless the admin says so. Every unit is gone at its time, even if the mod is removed.
- **Coexists with:**
  - the foundation engine, meaning core SpawnWaves, SpawnTracker, caps, purge and boot sweep (Epic A17);
  - event-library's templates and chat authoring;
  - faction empowerment, which never touches our units;
  - Bloodcraft and KindredCommands familiars (untouched);
  - Raphael (the wire is unchanged, D20).

## Use cases
### Typical
**Setup.** The admin runs `.nyar template use undead-rising`, `.nyar event set undead-rising location here`, `.nyar event enable undead-rising` and `.nyar event start undead-rising`.

**What happens:**
- Two waves of armoured skeletons at level +2 with 1.2× health spawn on dry ground within 12 m and hold the spot within 30 m (Guard).
- A player who pulls one away sees it walk back.
- At the end every unit despawns within the grace period.

**Bandit-ambush.** The same commands with bandit-ambush put its bandits in hiding. They reveal when a player comes close.

### Minimal stretch
- **Least use (8.1):** a 0.5.0 definition with none of the new keys spawns exactly as in 0.5.0, except that ring points in water or rock move to walkable ground (D2, D3, D6). The pillar off, which is the default, spawns nothing (Epic D27).
- **Least definition:** one unit entry with count 1, one wave, no modifiers and no behaviour (D6).
- **Once and never again (8.2):** after one event, every unit despawns at its due time (D23) and Hunt seeding stops (D13). Runtime wave state does not linger: the unit rows foundation writes are cleared (state.json Units), and the territory map and hunt state are in memory and rebuilt per wave (D17, D13, D33). Configuration does linger by design: the edited events.json definition and its one .bak stay until the admin changes them.
### Maximal stretch
- **Volume (9.1):**
  - Three concurrent events with 50-unit waves, every wave Hunt, reach the default caps: MaxTrackedUnits 150 and MaxUnitsPerWave 20, raised by an admin to 50 and 500 at most (Epic Business rules 9).
  - Hunt seeding is at most 5 targets × live units every 5 s.
  - SpawnPoints tries at most 25 points per unit (D2).
  - The territory map is built once per wave, not per unit (D17).
  - The tick budget is measured at the caps (D24).
- **Abuse (9.2):**
  - An admin sets every multiplier to 3.0 and level 120. This is allowed, bounded by D6's ranges and the caps.
  - levelDelta beyond ±5 is refused (Epic Business rules 4, D6).
  - allowTerritory true on a Point inside a player's castle is the admin's explicit choice. It is logged at start (D17) and documented in the README.
  - An AroundPlayer event cannot target a chosen player: the pick is random among eligible players (D16).
  - A player can do nothing: every path is admin or System (D22).
- **Repeated use (9.3):**
  - A wave already queued is not queued again (foundation SpawnLedger).
  - A Hunt seed is never added twice for a unit and player (D13).
  - A second `event set` of the same value rewrites the same file content (event-library idempotency).
  - Starting an active event replies "already active" (foundation).

## Business rules
1. **Units are ours (CLAUDE.md › Spawn & buff safety):** every change in this plan lands on units SpawnTracker spawned and will despawn:
   - modifiers ride on the unit's marker buff (MultiplyBaseAdd, A7 of foundation);
   - the level, the aggro fields and the hiding buff are written on the unit at spawn.
   No native NPC and no prefab is changed. Modifiers scale the prefab baseline through MultiplyBaseAdd, so they never compound (Epic Business rules 4).
2. **Ranges (D6):**
   - chance 0.05-1.0;
   - level 1-120, or levelDelta -5..5 (Epic Business rules 4, never both);
   - multipliers 0.5-3.0 with two decimals (S-8);
   - Guard leash 10-80 m, Hunt range 10-60 m (S-9);
   - AroundPlayer minDist 10-60 and maxDist 15-80, minDist < maxDist.
3. **Spawn points (D2, D3, D17):**
   - A ring point is used when it is walkable and, unless allowTerritory, unclaimed. Otherwise the planner searches, in order, the same ring, the half ring and the centre.
   - A centre in claimed territory skips the wave (unless allowTerritory).
   - A failed walk check uses the ring point unchanged (fail open: the worst outcome is a stuck unit, which still despawns on time).
   - A failed territory map skips the wave (fail closed: a player's property is never at risk from a lookup failure).
4. **Loot and XP (Epic S-10, Business rules 6):** loot is off unless the event sets `loot: true`; XP stays vanilla.
5. **Behaviours:** at most one per event (D6).
   - **Guard:** the leash and home point are set at spawn (D12).
   - **Hunt:** seeded every 5 s within range of the wave centre, at most 5 targets (D13).
   - **Ambush:** the prefab's own Hiding buff, applied at spawn, reveals by the game's own rules (D7, D14).
   Behaviours never outlive the wave: seeding stops at every end path (D23).
6. **AroundPlayer (D16):** one random eligible player per wave; eligible means online, alive, outside claimed territory and not in PvP combat. The player is never named or located in any output. No eligible player means no wave.
7. **Durations are hard (Epic Business rules 3):** nothing in this plan extends a unit's life. Due time, LifeTime with Age, DestroyWhenDisabled and the boot sweep are unchanged (D23).
8. **Precedence (4.4, D29):**
   - The caps and controls of Precedence.StartBlocker and WavePlan.Split win over everything here.
   - A skip for claimed territory or no eligible player comes before the roll.
   - The chance roll comes before the caps (D8).
   - allowTerritory lifts only the territory rule, never a cap.
   - The admin who writes the definition decides every exception. There is no per-player exception.
9. **"Every" sets (4.5):**
   - "Every spawned unit" is every order SpawnTracker.Spawn receives; its only callers are RequestWave (event waves) and SpawnManual (`.nyar spawn`).
   - "Every end path" is natural end, stop, fault cancel, purge, restart and uninstall (D23).
   - "Every new key" is the OnlyKeys list of ParseSpawnWaves and ParseLocation, which D6's test enumerates.
   - "Every new control" is D22's ControlCases list.

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:**
  - Logic/Model.cs SpawnWavesAction and Location.
  - Logic/Validation.cs ParseSpawnWaves and ParseLocation.
  - Logic/Engine.cs WavePlan (Split, Center, Anchor, Regroup).
  - Logic/SpawnLedger.cs (Request, Confirm, Lifetime, Around).
  - Services/SpawnTracker.cs (RequestWave, Spawn, Prepare, TryMark, Regroup).
  - Services/UnitSetup.cs (Apply, StatModifiers).
  - Services/WaveAction.cs QueueDueWave.
  - Services/EventStore.cs PrefabUnitCatalog (IUnitCatalog).
  - the event-library editor (Logic/Authoring.cs, DefinitionEditor) and CommandArgs.
- **Writes:**
  - Model: new records UnitEntry.Chance, SpawnModifiers, Behaviour (Guard, Hunt, Ambush), Location type AroundPlayer with MinDist/MaxDist, and SpawnWavesAction fields Modifiers, Loot, Behaviour, SpawnVisual, AllowTerritory, all optional with defaults that equal 0.5.0 behaviour (D6).
  - Validation for the new keys (D6, D7).
  - Logic/Spawning.cs, new: SpawnPoints, WaveRoll, TuningFrom, HuntPlan, PlayerPick, Territory (D2, D8, D9, D13, D16, D17).
  - SpawnOrder gains Loot, Behaviour, Visual and the resolved point kind.
  - SpawnTracker applies them (D3, D11, D12, D14, D15).
  - UnitSetup writes the aggro fields and the hiding buff.
  - New services WalkCheck (read-only), TerritoryMap (read-only) and HuntAction ([Mutating], AggroBuffer).
  - CommandArgs and Authoring fields (D18).
  - IUnitCatalog.HidingBuff (D7).
  - templates.json (D19).
  - tools/rollback-drill.ps1 (D25).
  - the `debug walk` command, added and removed (D1, D22).
- **What breaks if wrong:** a wrong default would change 0.5.0 definitions (D6's last fails-when); a wrong territory conversion would spawn inside castles (D17); a hunt that survives its wave would pull players' aggro forever (D13, D23).
- **Shared types (5.3):**
  - events.json's action gains the keys and ranges enumerated in D6 against the C# records they deserialise into. Its tests read the records, not the prose.
  - The api contract is unchanged (D20).
  - SpawnOrder is internal.

### External — dependencies and their failure behaviour
- **V Rising server 1.1.12 (VampireReferenceAssemblies 1.1.12-r99041-b2), sampled per component (6.1):**
  - `TileMapCollisionMath.CheckStaticCircle` and its map data: ProjectM.Tiles in ProjectM.Shared.dll. Metadata was read 2026-09-28; its behaviour is unproven until the D1 probe.
  - `CastleTerritory` and its blocks: KindredCommands' use, S-7 (D17).
  - `AggroConsumer` fields PreCombatPosition and MaxDistanceFromPreCombatPosition, and `AggroBuffer`: Bloodcraft, spikes S1.
  - `SpawnBuffElement` with Kind Hiding, the Bandit and Blackfang ambush buffs: prefab dump, 36 Hiding entries.
  - `Buff_General_Spawn_Unit_Fast_WarEvent`: prefab dump; used by Bloodcraft FamiliarBindingSystem.
  - `DropTableBuffer`: RESEARCH_NOTES spike.
  - `InCombatBuff_PvP`: BloodyEncounters.
  - Every in-game claim has a session item: D1, D3, D10–D17.
  - Record variants sampled: every CHAR_ prefab's SpawnBuffElement at boot (D7's count against the dump's 36, D14); territories with and without a heart (D17); players online, offline, dead, in territory and in PvP combat (D16's tests over the component set the pick reads).
  - No quota and no monetary cost: everything runs in process on the admin's server.
- **Build, review and release tooling (6.1):** version policy is "the version installed on the build machine at the step's pre-audit, recorded there"; the floors below are the versions this child was planned with. No paid cost beyond the owner's existing subscriptions.
  - git 2.53 (floor 2.40); local only, no quota. Failure stops the step.
  - gh 2.92 (floor 2.40), authenticated as the owner's GitHub account; GitHub API rate limits apply to push, release and release-verify, which retry once and otherwise stop the release step before the pre-release is created.
  - tcli 0.2.4 (floor 0.2.4, the version release-verify's zip naming was built against); `tcli build` only, never publish; failure stops the release step before the tag.
  - Codex CLI 0.151 (any version with `exec -s read-only`), under the owner's subscription quota; "Selected model is at capacity" or a timeout gives no verdict: the review is retried later and never skipped, and a run the reviewer did not read is not counted as a round.
  - Python 3.13 (floor 3.10) for tools/ingame/session-events.py; pwsh 7.5 (floor 7.2) for tools/*.ps1; .NET SDK 10.0.302 building net6.0 (floor: an SDK that builds net6.0).
  - The release tools' own failure cases run in `pwsh tools/preflight.ps1 -DependencySuite` (category release-tools: release-verify and rollback-gate selftests, D21's command).
- **Failure behaviour (6.2):**
  - A walk check that throws or finds no data counts as free (D4).
  - A territory map that fails skips the wave (D17).
  - A hunt seed that throws stops hunting, not the wave (D21).
  - A hide that fails spawns the unit unhidden (D21).
  - A player query that fails skips the wave (D21).
  - A recipe write that throws discards the unit (D21).
  - A game update that renames a component makes the lookup throw, which lands in these paths.
  - The internal collaborators (SpawnLedger, the event-library editor) are in process and never slow: an exception from either lands in the event's try/catch and faults the event (Epic D25), and a failed write changes nothing (D21).
  - tcli failing stops the release step before the tag; a session helper (tools/ingame/session-events.py, dev-snapshot) failing stops the session before boot, and the snapshot restore puts the server back (D27).
  - Tooling (git, gh, Codex, tcli) aborts and reruns under $ErrorActionPreference='Stop' (release-verify, rollback gate); a Codex timeout gives no READY and the step waits.
- **No sandbox (6.3):**
  - The dev server (127.0.0.1:9876, save-data-nyardev) is the test world. Every session is wrapped by tools/dev-snapshot.ps1 (D27), so its plugins and config return to their saved state.
  - The `debug walk` probe never ships (D22).
  - Unit tests use fakes (IRandom, isFree, player lists), never game types.

## Design
### Data
| Artifact | Where | Owner | Kept | Deleted |
|---|---|---|---|---|
| events.json new keys | BepInEx/config/Nyarlathotep/events.json | the admin | until edited | by the admin; 0.5.1 disables such a definition with its reason (D25) |
| templates.json examples | embedded in the DLL | the mod | per release | with the DLL |
| territory block set | memory, per wave | TerritoryMap | one wave | at the wave's spawn |
| hunt seed set | memory | HuntAction | while the wave lives | at every end path (D23) |
| walk probe replies | the admin's chat and BepInEx/LogOutput.log | the admin | until the log rotates | with the log; the command is removed (D22) |
| session log copies | %TEMP%\nyar-s*-logs, %TEMP%\nyar-soak-* | Claude | the session | deleted in the same session (D27) |
| snapshot | %TEMP%\nyar-snap-* | tools/dev-snapshot.ps1 | the session | on restore (D27) |
| events.json.bak | BepInEx/config/Nyarlathotep/ | the mod (event-library editor) | one copy, the file before the last chat write | replaced by the next write; by the admin |
| events.json.tmp, state.json.tmp | BepInEx/config/Nyarlathotep/ | the mod (DataStore) | milliseconds, during a write | moved over the target by the same write; a leftover from a crash is overwritten by the next write |
| review prompts | %TEMP%\dod-review-*.txt | the dod skill | until the next prompt of the same plan and date | replaced by the next build of the prompt; never committed |
| Claude's scratchpad (Codex prompts and outputs, drafts) | Claude Code's per-session scratchpad, outside the repository | Claude | the session | removed with the session; never committed; SteamIDs redacted before any Codex prompt |
| build outputs | Nyarlathotep/**/bin, obj, *.binlog (git-ignored) | Claude | until the next build | overwritten by each build; never shipped |
| release zip | Nyarlathotep/Nyarlathotep/build/*.zip, dist/ (git-ignored) | Claude | until the next release | replaced by the next tcli build; its SHA-256 is kept in the audit |
| tags and GitHub releases v0.5.1, v0.6.0 | origin | the owner | for ever (exist only once) | never deleted; a bad one is retitled (Epic S-19) |
| plan, reviews, audit, feature doc | docs/ (committed) | the repository | for ever | only by a later commit |

Only the tags and releases exist only once; every other artifact is rebuilt or replaced. Every row of this table and every `temp:` and `server:` glob of tools/paths-manifest.txt has a tools/data-inventory.json entry with its five fields (storage, owner, retention, deletion, copies), and one command fails on a missing entry or a leftover temporary folder: `pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns` (D27). state.json is unchanged. The unit rows are foundation's and hold no new field. The territory set and hunt set are rebuilt, never persisted. Migration (3.4): none, because every key is optional and absent means the 0.5.0 behaviour (D6).

### States
- **Empty and first run (7.1):**
  - A definition without the new keys behaves as in 0.5.0 (D6).
  - A zero-unit roll spawns nothing (D8).
  - No eligible player, or a claimed centre, skips the wave (D16, D17).
  - A walk check with no map data is fail-open (D4).
- **Loading:** definitions load at boot; before Core.IsReady no wave runs (foundation ready guard), so no half-loaded definition spawns.
- **Partial:** a wave spawns over several ticks within SpawnsPerTick. A unit whose recipe fails is discarded alone; the units before it stay tracked and on their due time, and its visual is dropped (D21, D15).
- **Error states:** each dependency failure of D21.
- **Concurrent use (7.2):**
  - Two admins editing the same event go through the event-library editor, which refuses a stale file (unchanged, D18).
  - Two Hunt waves near the same player seed separately; the game's AggroBuffer takes both.
  - A wave queued while `event set` changes the definition keeps the definition it started with (foundation D6).
  - The actors are admins, the scheduler and Claude (planner and build tooling).
  - Claude never runs two sessions on one server: the snapshot refuses a leftover (D27).
  - A castle claimed while a wave spawns: territory is read once per wave, so the claim is seen by the next wave; the units of the wave being spawned may stand on the new claim until they despawn (D17, stated in the READMEs).
- **Stale data, cancel and re-entry (7.3):**
  - The territory map is built per wave, so a castle claimed mid-event is seen at the next wave.
  - A player picked for AroundPlayer who logs off leaves the wave where it spawned.
  - Stop, purge and restart end hunting and despawn units (D23).
  - A restart mid-wave cancels the event (foundation), and the boot sweep removes the units.
  - An `event set` changes the next start only (event-library D12).

### Permissions
- **Actors (2.1):**
  - Admins write definitions (files or `.nyar event set`) and run `debug walk` (0.5.1 build only).
  - The System actor (scheduler, triggers) starts events.
  - Players have no command here: they are targets of Hunt and AroundPlayer, never callers.
  - Unauthenticated connections never reach chat: the game admits a client only after Steam authentication, and VCF reads only authenticated users' chat.
  - The operator (whoever has the server's file system) may edit events.json and the cfg, or remove the DLL; validation (D6) is the same for a hand edit as for chat.
  - Claude builds, deploys to the dev server and pushes; the owner alone publishes to Thunderstore (D31). There is no service account.
  - Each new command is adminOnly and the auth suite fails otherwise (D22).
- **Unauthorised path (2.2):** VCF's adminOnly rejects a player's `event set` or `debug walk` with the framework's denial line (D22). The auth suite checks every command.
- **Ownership (2.3):**
  - Definitions belong to the server; any admin may change any definition (event-library).
  - A unit belongs to its event: stop, purge and end remove it whoever started it (D23).
  - A player picked by AroundPlayer owns nothing of the wave.
  - Claimed territory belongs to its clan, and the refusal protects it unless an admin writes allowTerritory (D17).

### UX
- **Where it lives (11.1):**
  - The new keys are in events.json and in `.nyar event set` (D18).
  - The templates show two of them (D19).
  - `.nyar event info` prints the action's new keys.
  - The README's event reference documents each key with its range (D26).
- **Feedback (11.2):**
  - A set replies with the new value or the rule it broke (D18).
  - A wave logs moved, unchecked and skipped counts (D3, D16, D17).
  - Validation reasons name the field (D6, D7).
  - Every player-facing text is rendered in real chat before release (profile note 11.2; Session 3).
- **Accessibility (11.3):** chat lines under 480 bytes, wrapped by the game; no meaning is carried by colour; chat is keyboard-only; the game has no screen reader, an inherited limitation stated in the README; every new line is rendered in real chat (D32).
- **Activation (11.4):**
  - A definition with the new keys starts by its trigger as any event does.
  - The templates are the pointer: `.nyar template info undead-rising` shows modifiers and Guard (D19).
  - The start log line and `.nyar status` show a wave ran.
  - Walkability activates for every wave from 0.5.1 on, with no key: its signal is the wave line's "(<m> moved, <u> unchecked)" (D3); a wave keeps 0.5.0 placement only where the check fails open (D4).
  - Every other feature activates only when an admin writes its key.

## Security
- **Authorization (10.1):**
  - Every new path is admin (`event set`, `debug walk`) or System (the scheduler running a definition).
  - HuntAction and the recipe writes are [Mutating] and called only from SpawnTracker and EventRuntime, which the gateway check proves (D22).
  - AroundPlayer takes no player argument, so no admin or player can aim it (D16).
- **Injection (10.2):**
  - Every value is parsed as a typed number, boolean or enum word against D6's ranges.
  - No value reaches a shell, a query or a template placeholder; messages take only the event name and unit names already validated (D6, D18).
- **Secrets (10.3):** none are added. gh keeps its token in the Windows credential store; TCLI_AUTH_TOKEN lives only in the owner's environment while publishing; rotation is the owner's. Tokens, SteamIDs and player positions are never logged (D31).
- **Personal data (10.4):** AroundPlayer and Hunt read player positions in memory only. No log line, message or api row carries a player's name, SteamID or position (D16, PrivacyTests). The audit and session records never copy "admin …" log lines that carry a SteamID (D27).

## Failure & observability
- **What the admin sees (12.1):**
  - A refused definition shows its reason in `.nyar event list` (D6, D7).
  - A skipped wave logs its reason (D16, D17).
  - A failing dependency logs once per streak (D4, D21).
  - The next step is to fix the definition or move the centre.

Each failure class of D21, what the admin sees and the next action (D30's entries asserted by HealthTests Spawns):

| Failure class | Admin sees | Next action |
|---|---|---|
| Walk check unavailable (D4) | "spawns: walk check unavailable" in `.nyar status`, the health line and the login notice; waves still spawn, unchecked | none needed at once; report the server build, since a game update may have moved the tile data |
| Territory unknown (D17) | "spawns: territory unknown"; the wave is skipped | move the event outside castles or set allowTerritory; report if it persists |
| Unit setup failing (a recipe write throws, D21) | "spawns: unit setup failing (<id>)"; the unit is discarded, the rest spawn | remove the event's newest modifier, behaviour or visual key and reload |
| Hunt seed failing (D21) | "spawns: hunt seed failing (<id>)"; the wave stays without hunting | set behaviour Guard or none for the event |
| Ambush hide failing (D21) | "spawns: ambush hide failing (<id>)"; units spawn visible | remove behaviour Ambush; report the unit |
| Player query failing (D21) | "spawns: player query failing (<id>)"; AroundPlayer waves are skipped | use a Point location until fixed |
| SpawnLedger or editor exception (D21) | the event's fault cancel after 3 faults (Epic D25), in the log and `.nyar status` | `.nyar event disable <id>`, then report the log line |
- **Logs (12.2):**
  - Wave lines carry the moved, unchecked and rolled counts (D3, D8).
  - Skip lines name the rule (D16, D17).
  - Hunt and hide failures are logged per streak (D21).
  - VerboseLogging lists each unit's tuning (UnitSetup.Apply's note).
  - The slow-tick warning (event-library D36) names the phase if hunting is slow.
- **Knowing it is broken (12.3):** the admin, on login. "spawns: walk check unavailable" and "spawns: territory unknown" join the degraded list that the 10-minute health line, `.nyar status` and the admin login notice carry (D30). There is no external dashboard; the admin's login notice is the alert, and -LogCheck covers every dev session (D27).
- **Failing cases (12.4):**
  - Every test and cmd item names its fails-when, and every new control has a ControlCases row (D22).
  - The probe's no-go path is spelled out: D1 for walkability, and D14 with its amendment for Ambush.
  - An empty input prints a result that cannot read as a pass: a zero roll reports "0 units rolled" (D8), and an empty player list reports "no eligible player" (D16).

Each check this plan introduces, with its failing input, its silent input and its empty input:

| Check | Reports a failure on | Stays silent on | Empty input |
|---|---|---|---|
| DebugCommands (preflight) | fixture bad: a registered `debug walk` in Commands/ | fixture good: no temporary command | fixture empty: no Commands/ file gives "debug commands: no command files" (fail) |
| GatewayOnly bad-new (preflight) | a [Mutating] method in a new untracked Services/Stray.cs | the real tree | the existing empty fixture gives "gateway: no [Mutating] method found" (fail) |
| rollback-drill unknown-field pair | a 0.5.1 log disabling a definition for another reason | a 0.5.1 log disabling only for unknown new keys | no log pair gives "drill: no logs" (fail) |
| WavePrecedence filter | a swapped rule | the stated order | fewer than 12 tests run: fail (D29) |
| Logic controls D2, D4, D6-D9, D11-D13, D15-D18, D21, D28-D30 | each item's fails-when | its passes case | its empty case, each a ControlCases row with the three method names (D22) |

| EntityWrites (preflight, D34) | fixtures bad, bad-new, bad-unmarked, bad-em, bad-split, bad-refrw, bad-ecb, bad-lookup, bad-unsafe | fixture good | fixture empty: "entity writes: no source files" (fail) |
| Probe records (-SessionsOf, D27) | fixtures SessionLogs/bad-probe (a walk reading missing) and bad-probe-2 (no go/no-go line) | a Session 1 with five readings and a go/no-go line | a record reading "go/no-go: incomplete" or "ambush probe: incomplete" (fail) |
| Data tests (-Paths -DeclaredOf, D27) | fixture Paths/bad-datatests (the slug's `dataTests` entry empty); a listed class failing | the real tree | zero tests run: fail (Invoke-ClassTests) |
| EndPaths filter (D33) | a path leaving a unit, seed or visual | every path emptying all three | fewer than 5 tests run: fail |
| TimingSpan (preflight, D24) | fixtures bad-avg, bad-slow, bad-gap | fixture good | fixture empty: "timing span: no span" (fail) |
| Secrets bad-envread (D31) | a tools/ script writing $env:TCLI_AUTH_TOKEN | the real tree | the existing empty case, "secrets: no files scanned" (fail) |

The empty input of each Logic control, and what it returns (each a `<Name>_empty_<input>` test, D22):

| Control | Empty input | Result |
|---|---|---|
| D2 SpawnPoints | radius 0 | the centre, one isFree call |
| D4 WalkCheck | no map data | counts free, "walk check unavailable: no map data" once |
| D6 validation | `"modifiers": {}` | refused "action.modifiers must name at least one modifier" |
| D7 HidingIndex | a prefab with no SpawnBuffElement | cannot hide |
| D8 WaveRoll | an empty unit list | "0 units rolled", nothing spawned |
| D9 TuningFrom | no modifiers | UnitTuning.None |
| D11 loot | an order without Loot | drops cleared |
| D12 behaviour | no behaviour | no aggro field written |
| D13 HuntPlan | no players | no targets, nothing seeded |
| D15 visual | no visual orders | an empty queue |
| D16 PlayerPick | no players | "no eligible player", wave skipped |
| D17 Territory | an empty block set (a built map of an unclaimed world, distinct from a failed build) | nothing claimed |
| D18 event set | a field with no value | "value required" |
| D21 failures | no failure | no log line |
| D28 WalkRadius | no argument | 0.5 |
| D29 WaveGate | nothing blocked, no skip, a roll of zero | "0 units rolled" |
| D30 SpawnHealth | no open streak | no entry |
| D33 WaveLifecycle | an event with no units | 0 tracked, no seed, no visual |

One evidence command per gating probe:

| Probe | Command | Item |
|---|---|---|
| 2.1 actors | pwsh tools/preflight.ps1 -AuthSuite | D22 |
| 3.3 persistence | pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns (inventory, manifest, leftovers, and the `dataTests` classes, EndPathTests of D33, through Invoke-ClassTests) | D27 |
| 4.4 precedence | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WavePrecedence | D29 |
| 6.2 dependency failure | pwsh tools/preflight.ps1 -DependencySuite | D21 |
| 10.1 authorization | pwsh tools/preflight.ps1 -AuthSuite (commands, admin list, gateway, entity writes) | D22, D34 |
| 10.3 secrets | pwsh tools/preflight.ps1 ("secrets: none") | D31 |
| 12.4 failing cases | pwsh tools/preflight.ps1 -SelfTest (it runs every fixture and every registered external selftest, among them `dotnet test Nyarlathotep/Nyarlathotep.Tests`, whose ControlCaseTests fail when a control lacks its fails-when, passes or empty test) | D22 |
| 14.3 rollback | pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.6.0 -Plan event-spawns (the whole child's range: the repository revert of v0.5.0..v0.6.0, 0.5.0 loading 0.6.0's written files, the snapshot selftest); each release also runs its own range | D25 |
| 14.4 paths | pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns | D27 |

Release, privacy, audit, session and path checks are the existing ones of foundation, faction-empowerment and event-library, with their fixtures; this plan adds only the rows above. Every fixture spells what the real input spells: the DebugCommands fixtures are copies of the real Commands/DebugCommands.cs with and without the command.

## Performance
- **Budget and hot path (13.1):** the scheduler tick stays under 5 ms on average at the caps (Epic D24, D24 here). The hot paths are:
  - the per-unit point search, at most 25 isFree calls (D2);
  - the per-wave territory build;
  - the 5 s hunt seed, at most 5 targets × live units.
- **Supported envelope:** the budget is measured and promised at the default caps (MaxTrackedUnits 150, D24). An admin may raise the caps to their ceilings (500 tracked, 50 per wave); the work then grows linearly (point search and hunt seeding per unit), and the slow-tick warning (event-library D36) names the phase if a tick reaches 250 ms. The README says the budget holds at the defaults.
- **Limits (13.2):**
  - MaxTrackedUnits, MaxUnitsPerWave and the spawn and despawn budgets are unchanged (foundation).
  - Hunt targets are capped at 5. The cap comes from Bloodcraft's familiar aggro scale and the 60 m hold range of spikes S1; the valid case it excludes is a crowd of more than 5 players near one wave, where the nearest 5 are hunted and the game's own aggro takes the rest.
  - The search is capped at 25 points; it excludes a ring that is almost all water, which falls back to the centre.

| Bound | Source | At the bound | Valid case excluded |
|---|---|---|---|
| chance 0.05-1.0 | a copy under 5 % is noise in a wave of at most 50 | 0.05 accepted, 0.04 refused (D6) | a rare 1 % elite |
| levelDelta -5..5 | Epic Business rules 4 (BloodyBoss issue 10, level-gap immortality) | ±5 accepted, ±6 refused (D6) | a deliberately unkillable guard; `level` covers it (S-11) |
| level 1-120 | the game's unit levels, `.nyar spawn`'s range | 1 and 120 accepted | none |
| multipliers 0.5-3.0, two decimals | Empower's 3.0 ceiling (faction-empowerment S-1), S-8's floor | 0.5 and 3.0 accepted, 3.01 refused (D6) | a 5× raid boss wave |
| Guard leash 10-80 m | vanilla MaxDistanceFromPreCombatPosition 45 on bandits, TideOfWar's long chase | 80 accepted, 81 refused (D6) | a map-wide chase (the game drops targets past about 86 m, spikes S1) |
| Hunt range 10-60 m | spikes S1: a seeded target holds within about 60 m with line of sight | 60 accepted, 61 refused (D6) | hunting a player across a region |
| AroundPlayer 10-60 / 15-80 m | inside Vision.Range 30 plus a run-up; beyond 80 m DestroyWhenDisabled may remove units near no player | 80 accepted | an ambush out of sight a region away |
| Hunt targets 5 | above | 5 targets | the sixth nearest player |
| point search 25 | 12 + 12 + 1 | the 25th point is the centre | a walkable spot beyond the ring |
| chat line 480 bytes | the game's chat (profile note 11.2) | wrapped, never cut | none |
| units: MaxTrackedUnits 150 (≤ 500), MaxUnitsPerWave 20 (≤ 50) | foundation caps (Epic Business rules 9) | clamped with a log line | a 300-unit spectacle at defaults, reachable by raising the cap |

## Build plan
1. **Walk probe (0.5.1, Session 1 with the owner).**
   - Pre-audit per CLAUDE.md into docs/audits/event-spawns.md (created with the template of docs/audits/README.md).
   - Add Services/WalkCheck.cs: IsFree(x, z, heightLevel, radius), reading the server's static tile world and calling TileMapCollisionMath.CheckStaticCircle with MapCollisionFlags.CollideNormalMovement.
   - Probe candidate map-data sources in order, recording which one answered:
     - a TileWorld or StaticTileWorld singleton through the EntityManager;
     - the map data of the server's collision system;
     - XPRising's `new TileWorld()`, which is expected to read empty.
   - Add the temporary admin command `.nyar debug walk [radius]` in Commands/DebugCommands.cs.
   - Deploy with the server stopped, wrapped by `pwsh tools/dev-snapshot.ps1 -Save es1`.
   - Session 1: the owner stands on dry ground, in the pond, in a river, against a cliff and inside a building, running the command at each.
   - Record the replies and the go/no-go line in docs/features/EVENT_SPAWNS.md › Test results.
   - On no-go: a discovered amendment removes D2–D5 (fallback S-6) and the owner decides the next probe in plan mode, before any further code.
   - Satisfies D1, D28, D32.
2. **Walkable points and release 0.5.1.**
   - Add Logic/Spawning.cs SpawnPoints and its tests (SpawningTests, DependencyFailureTests WalkCheck, ControlCases rows).
   - Wire it into Services/SpawnTracker.cs RequestWave and the wave line.
   - Remove `debug walk`, and add the preflight check "debug commands: none temporary" with fixtures DebugCommands/{good,bad,empty} in tools/preflight-checks.json.
   - Session 2 (owner): the pond shore test of D3.
   - Release 0.5.1 on the six surfaces:
     - the changelogs note the fix;
     - the READMEs drop the known issue;
     - `tcli build`, annotated tag v0.5.1, rollback gate `pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan event-spawns`;
     - privacy grep, push, GitHub pre-release, release-verify.
   - The owner publishes.
   - Satisfies D2, D3, D4, D5, D22, D31.
3. **Schema, validation and planners.**
   - Model records, Validation (ParseSpawnWaves, ParseLocation, IUnitCatalog.HidingBuff).
   - Logic/Spawning.cs WaveRoll, TuningFrom, HuntPlan, PlayerPick and Territory.
   - CommandArgs and Authoring fields.
   - Tests: EventValidationTests Spawns and Ambush, SpawningTests, AuthoringTests Spawns, CommandArgTests Spawns, ContractDocTests, PushTests Spawns, PrivacyTests, and ControlCases rows.
   - `.nyar spawn` builds its tuning with TuningFrom.
   - No in-game step.
   - Logic/Spawning.cs WaveGate, SpawnHealth, HidingIndex and WaveLifecycle; HealthMonitor.Degraded adds SpawnHealth.Entries.
   - The EntityWrites preflight check and its fixtures (D34), fixtures bad-refrw, bad-ecb, bad-lookup and bad-unsafe included.
   - tools/preflight.ps1: -SessionsOf gains the `probeRecords` check (fixtures SessionLogs/bad-probe and bad-probe-2) and -Paths -DeclaredOf the `dataTests` run (fixture Paths/bad-datatests), both registered in tools/preflight-checks.json with the event-spawns entries (D27).
   - Satisfies D6, D7, D8, D9, D16, D17, D18, D20, D27, D29, D30, D33, D34.
4. **Services and Session 3 (owner).**
   - SpawnOrder fields; SpawnTracker.Prepare applies loot, aggro fields, hiding and the visual queue.
   - UnitSetup writes; Services/TerritoryMap.cs; Services/HuntAction.cs driven by EventRuntime; AroundPlayer in WaveAction.
   - DependencyFailureTests Spawns.
   - Session 3 records D10 (modifier readings), D11 (loot), D12 (Guard), D13 (Hunt), D14 (the Ambush probe, then the Ambush wave), D15 (visual), D16 (AroundPlayer) and D17 (castle skip and allowTerritory).
   - A no-go on Ambush is amended before step 5.
   - Session 3 renders every new chat line (D32).
   - Satisfies D10, D11, D12, D13, D14, D15, D16, D17, D21, D32.
5. **Templates, end paths and budget (Session 4, owner kick-off then unattended).**
   - templates.json examples (D19).
   - Add preflight mode -TimingSpan with its fixtures (D24).
   - Session 4 runs the end paths of D23: natural end, stop, purge, restart and the uninstall.
   - It also measures D24 with TimingLog on and 150 units of Hunt and Guard waves.
   - Satisfies D19, D23, D24.
6. **Release 0.6.0.**
   - Extend tools/rollback-drill.ps1 for "unknown field" differences with its selftest pair (D25).
   - The six surfaces; `tcli build`; tag v0.6.0; `pwsh tools/rollback-gate.ps1 -From v0.5.1 -To v0.6.0 -Plan event-spawns`.
   - Privacy grep; push; GitHub pre-release; release-verify (D26).
   - -AuditOf, -SessionsOf and -Paths -DeclaredOf event-spawns (D27).
   - Epic D12 pass line from Session 4's uninstall (D23); `dod close event-spawns`. The owner publishes.
   - Satisfies D23, D25, D26, D27, D31.

## Work breakdown
- W1 · **Walkable spawn points (0.5.1)**
- W1.1 · **Probe and planner** · items: D1 D2 D4 D28 · steps: 1, 2
- W1.2 · **In game and release** · items: D3 D5 D22 · steps: 2
- W2 · **Wave features (0.6.0)**
- W2.1 · **Schema and planners** · items: D6 D7 D8 D9 D16 D17 D18 D20 D29 D30 D33 D34 · steps: 3, 4
- W2.2 · **Services in game** · items: D10 D11 D12 D13 D14 D15 D21 D32 · steps: 1, 4
- W2.3 · **Templates, end paths, budget** · items: D19 D23 D24 · steps: 5, 6
- W3 · **Release 0.6.0**
- W3.1 · **Drill, release and records** · items: D25 D26 D27 D31 · steps: 2, 6

## Rollout
### Shipping
Two releases, each a GitHub pre-release; the owner publishes to Thunderstore:
- 0.5.1 (steps 1-2, walkable points, no schema change);
- 0.6.0 (steps 3-6).

Everything still ships off: every new key is absent in existing definitions, the templates stay disabled, and Pillars.EventSpawns defaults to off (Epic D4). Who turns it off:
- any admin, with `.nyar pillar spawns off`, `.nyar event disable`, `.nyar event stop` or `.nyar purge confirm`;
- the operator, by removing the DLL (Epic D12, D23).

### Compatibility
- events.json stays SchemaVersion 1. Every key is optional, and absent means 0.5.0's behaviour (D6).
- 0.5.1 changes placement only. A ring point in water or rock moves, which is the fix itself.
- The cfg gains no key. The api stays 3 (D20).
- `.nyar spawn` keeps its arguments (D9).

### Rollback
- **In the repository:** `git revert --no-edit v0.5.0..v0.5.1` and `git revert --no-edit v0.5.1..v0.6.0`, drilled by the rollback gate (D25; the gate's repository part per release).
- **On the dev server during the build:** every session is wrapped by tools/dev-snapshot.ps1 (D27).
- **On a server, 0.6.0 → 0.5.1:** install the 0.5.1 DLL. A definition using a new key is disabled with "unknown field action.<key>", and every other definition runs; this is the drill's accepted difference (D25). The admin removes the keys to run those definitions again. This remains possible after data is written, because 0.6.0 writes nothing else.
- **On a server, 0.5.1 → 0.5.0:** install the 0.5.0 DLL. Nothing changed on disk.
- **Published releases:** tags and releases are never deleted. A bad release is withdrawn by retitling it, and versions move forward only (Epic S-19).
- **Commit ranges:** v0.5.0..v0.5.1 and v0.5.1..v0.6.0.

### Paths walked
Walking the Build plan. tools/paths-manifest.txt and tools/data-inventory.json are themselves written by this child (steps 2 and 6 add its globs and inventory rows). A path created and removed inside a step (the temporary folders) is declared here and checked by -DeclaredOf against the manifest whether or not it still exists. Writes are not traced at run time; instead every write site is found statically: -Paths scans every tools/ script for an expression that takes the %TEMP% root and fails unless the line names a declared nyar-<name> folder or carries a '# nyar-temp: <reason>' registration (event-library A18, fixture Paths/bad-tempvar), so a helper that stages into an undeclared %TEMP% folder and cleans it up fails the check before it ever runs; the mod's own writes are fenced by the FileWrites check (foundation) to BepInEx/config/Nyarlathotep. The walker (-Paths, Epic D33) reads git ls-files, git ls-files --others --exclude-standard and git status --ignored --porcelain after the build and the tcli build, so new, untracked and generated files (bin, obj, dist, build/*.zip) are seen; it also reads the dev server's plugin and config paths, %TEMP%\nyar-* and the remote tags and releases (`git ls-remote --tags origin`, `gh release list`).
- **Step 1:**
  - docs/audits/event-spawns.md, docs/features/EVENT_SPAWNS.md.
  - Nyarlathotep/Nyarlathotep/Services/WalkCheck.cs, Nyarlathotep/Nyarlathotep/Commands/DebugCommands.cs, Nyarlathotep/Nyarlathotep/Logic/CommandArgs.cs and Nyarlathotep/Nyarlathotep.Tests/CommandArgTests.cs (D28).
- **Step 2:**
  - Nyarlathotep/Nyarlathotep/Logic/Spawning.cs, Nyarlathotep/Nyarlathotep/Services/SpawnTracker.cs.
  - Nyarlathotep/Nyarlathotep.Tests/{SpawningTests,DependencyFailureTests,ControlCases,ControlCaseTests}.cs.
  - Nyarlathotep/Nyarlathotep/Commands/DebugCommands.cs (the removal).
  - tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/DebugCommands/**, tools/preflight-fixtures/EntityWrites/**, tools/paths-manifest.txt, tools/data-inventory.json.
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - The remote tag v0.5.1 and the GitHub release v0.5.1 (`remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt).
- **Step 3:**
  - Nyarlathotep/Nyarlathotep/Logic/{Model,Validation,Spawning,CommandArgs,Authoring}.cs, Nyarlathotep/Nyarlathotep/Services/EventStore.cs (HidingBuff), Nyarlathotep/Nyarlathotep/Commands/{EventCommands,SpawnCommands}.cs.
  - Nyarlathotep/Nyarlathotep.Tests/{EventValidationTests,SpawningTests,AuthoringTests,CommandArgTests,ContractDocTests,PushTests,PrivacyTests,HealthTests,ControlCases,ControlCaseTests,FakeStores}.cs.
- **Step 4:**
  - Nyarlathotep/Nyarlathotep/Services/{SpawnTracker,UnitSetup,WaveAction,TerritoryMap,HuntAction,EventRuntime,HealthMonitor}.cs, tools/preflight.ps1 ($DispatchedServices) and tools/preflight-fixtures/GatewayOnly/bad-new/**, Nyarlathotep/Nyarlathotep/Logic/SpawnLedger.cs (the visual queue).
  - Nyarlathotep/Nyarlathotep.Tests/{DependencyFailureTests,SpawnLedgerTests}.cs.
- **Step 5:** Nyarlathotep/Nyarlathotep/Resources/templates.json, Nyarlathotep/Nyarlathotep.Tests/TemplateLibraryTests.cs, tools/preflight.ps1, tools/preflight-checks.json and tools/preflight-fixtures/TimingSpan/**, the Session 4 log copy in %TEMP%\nyar-s*-logs, tools/ingame/session-events.py (a mode `es4` for the unattended end paths).
- **Step 6:**
  - tools/rollback-drill.ps1 and its fixtures.
  - The same six release surfaces.
  - The remote tag v0.6.0 and the GitHub release v0.6.0.
  - docs/dod/event-spawns.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Sessions 1-4, on the server:** BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json{,.bak,.tmp}, save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log.
- **Outside the repository:** %TEMP%\nyar-snap-*, %TEMP%\nyar-s*-logs, %TEMP%\nyar-soak-* and %TEMP%\nyar-session, plus the selftest scratch folders %TEMP%\nyar-{selftest,depsuite,snaptest,drilltest}-* and the release tools' %TEMP%\nyar-rel-*, nyar-rollback-* and nyar-drill-*, each an existing `temp:` line of the manifest.
- **Build outputs:** Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/** and Nyarlathotep/Nyarlathotep/build/**, all existing ignored globs.
- **Review process:** docs/dod/event-spawns.reviews.md, docs/dod/event-spawns.review.html and docs/dod/event-spawns.html.

## Out of scope
- **Considered and excluded (15.1):**
  - Location types Zone (defended-zones owns zones.json), AroundBoss (boss-reinforcements) and CastleOf (sieges).
  - Structure damage (the Epic's `structure-damage`).
  - Weighted composition beyond the per-unit chance: the chance per copy gives the variety the child constraint's "weighted composition" asks for, without a second weighting model (S-10).
  - A per-unit loot table of our own; custom stealth buffs; rewards on kill.
  - Changing native NPCs.
- **Deferred (15.2):**
  - Stats counting of event units: child `stats`.
  - Behaviours JoinFight and Assault: boss-reinforcements and sieges.
  - Spawn ring shapes other than a circle: none planned.

## Also considered
- **Compliance:** AroundPlayer and Hunt bring danger to players. They are off by default, and the admin opts in per event. The README states it (D26).
- **Localisation:** English only; the message pools are admin-editable (Epic).
- **Running cost:** tick time only (D24).
- **Operational ownership:** the server admin; the runbook is the README's kill switch and uninstall sections.
- **Documentation and changelog:** the six surfaces per release (D5, D26), and docs/features/EVENT_SPAWNS.md updated with the code.
- **Analytics:** none; the stats child counts kills.
- **Decommissioning:** 0.5.0's known-issue note is removed by 0.5.1 (D5).
- **Support tooling:** `.nyar debug here`, `.nyar event info`, VerboseLogging and the wave lines.

## Assumptions
- S-1 · validated · 0.6.0 scope is the full child constraint minus Zone, AroundBoss and CastleOf: chance, modifiers (level or levelDelta ±5, maxHealth, power, moveSpeed, attackSpeed), loot, Guard and Hunt, AroundPlayer, spawn visual, territory refusal, all settable in chat · source: owner decision 1A, plan mode 2026-09-28
- S-2 · validated · Stealth ambush is behaviour Ambush inside this child, behind a probe; on no-go it is removed by amendment and the rest ships · source: owner decision 2A, plan mode 2026-09-28
- S-3 · validated · The walkability fix of event-library A27 is this plan's first step, released alone as 0.5.1 · source: owner decision 3A, plan mode 2026-09-28
- S-4 · validated · Hunt seeds players near the wave (within range of the centre); AroundPlayer picks a random eligible online player outside every castle; announcements never name the player · source: owner decision 4A, plan mode 2026-09-28
- S-5 · validated · The build starts after event-library releases 0.5.0 and closes · source: docs/dod/nyarlathotep.md Build plan steps 15 and 7, S-18
- S-6 · validated · The walk check uses TileMapCollisionMath.CheckStaticCircle over the server's static tile map data, gated by the D1 probe; a no-go is a gated scope branch, not a cheap reversal: the amendment it triggers removes D2-D5 and needs a fresh READY review before any build past step 1 · source: owner decision 3A, plan mode 2026-09-28 ("a probe of the tile-collision check on your pond … then a walkability check"); on no-go, the walkable-ground promise is withdrawn rather than approximated: a discovered amendment removes D2-D5, 0.5.1 is not released, placement stays as in 0.5.0 with the known-issue note, and the owner decides in plan mode whether another source (the AI's Relocate_Unstuck state, a pathfinding query) is probed; no unproven placement ships as a fix
- S-7 · reversible · A pre-release conditional decision (before 0.6.0 a change is a discovered amendment with Session 3 run again; after it, an amendment and a release): claimed territory is read as KindredCommands' CastleTerritoryService does: the CastleTerritoryBlocks of every CastleTerritory entity, block = floor((floor(x × 2) + 6400) / 10) · fallback: if Session 3's castle test disagrees, CastleTerritory.WorldBounds with CastleTerritoryExtensions.IsTileInTerritory (RaidForge) replaces the conversion, a discovered amendment on D17
- S-8 · reversible · Spawned-unit multipliers may weaken (0.5-3.0), unlike Empower's 1.0-3.0, because the units are ours and a weaker wave is a valid event · fallback: raise the floor to 1.0 with a corrected amendment; no data migrates since a stored 0.5 then disables its definition with the rule
- S-9 · reversible · A pre-release conditional decision (before 0.6.0 a change is a discovered amendment with Session 3 run again; after it, an amendment and a release): Hunt's range caps at 60 m, the distance spikes S1 found the game keeps a seeded target; Guard's leash caps at 80 m · fallback: if Session 3 shows a seeded unit dropping a player in line of sight inside 60 m, the Hunt cap becomes the measured hold distance minus 10 m, rounded down to 5 m, by a discovered amendment on D6; if a Guard unit at leash 80 is removed by DestroyWhenDisabled while players are near, the leash cap becomes 45 m (the vanilla bandit value)
- S-10 · validated · The child constraint's "weighted composition" is met by a per-unit spawn chance; a weighted pick among entries is future scope (Out of scope) · source: owner decision 1A, plan mode 2026-09-28 ("a spawn chance per unit")
- S-11 · reversible · A pre-release conditional decision (before 0.6.0 a change is a corrected amendment with D6's tests run again; after it, an amendment and a release): an absolute `level` is allowed 1-120 because it is the admin's explicit choice, as with `.nyar spawn`; the ±5 clamp governs levelDelta (Epic Business rules 4) · fallback: clamp level to the prefab level ±5 with a corrected amendment
- S-12 · validated · The profile notes apply: in-game claims have a session item (6.1), every end path is named (7.3), every chat text is rendered before release (11.2), every control has a test seam (12.4), and the release plan copies the earlier release-step amendments (14.3: release-verify, rollback gate with -Plan, privacy grep, the unpushed-tag check) · source: docs/dod/profile.md

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D22 D16 D31; 2.2 D22; 2.3 D23 D17 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D6 D7 D18 D28; 3.2 D3 D9 D11 D12 D13 D14 D15 D16 D17 D18 D20 D21; 3.3 D27 D33 D13 D23 D26; 3.4 D6 D25 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D1 D2 D8 D9 D13 D16 D17; 4.2 D9 D13 D23; 4.3 D13 D23; 4.4 D29 D17; 4.5 D6 D7 D22 D23 D29 D34 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D3 D9 D18 D7; 5.2 D6 D9 D23; 5.3 D6 D20 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D1 D7 D10 D14 D17 D16; 6.2 D4 D21 D17 D30; 6.3 D27 D22 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D6 D8 D16 D4 D21; 7.2 D18 D13 D16 D17; 7.3 D23 D17 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D6 D3; 8.2 D23 D13 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D24 D2; 9.2 D6 D16 D22; 9.3 D13 D18 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D22 D34 D16; 10.2 D6 D18; 10.3 D31; 10.4 D16 D27 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D18 D19 D26; 11.2 D18 D3 D6; 11.3 D32; 11.4 D19 D23 D3 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D6 D16 D17 D30 D21; 12.2 D3 D8 D21; 12.3 D30 D27; 12.4 D22 D29 D33 D34 D1 D14 D8 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D24 D2 D6 D26; 13.2 D6 D13 D2 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D5 D26; 14.2 D6 D9 D20; 14.3 D5 D25 D27; 14.4 D27 D31 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline

## Amendments

## Log
- 2026-09-28 · status → draft · plan
- 2026-09-28 · note · owner decisions 1A, 2A, 3A and 4A in plan mode (S-1 to S-4); recon at c67d5df (SpawnWaves code map and reference-mod research recorded in docs/features/EVENT_SPAWNS.md at step 1)
- 2026-09-28 · note · round cap · after Review 3 · owner: approved option A in plan mode — Reviews 4–6 (codex, full); a scope finding goes back to the owner · through Review 6
- 2026-09-28 · note · review: codex Review 4 REVISE (F1-F9, 6 blocking, 3 advisory); F1-F5 and F7-F9 accepted, F6 rejected with evidence (the temp-root scan already covers Python's and Node's temporary APIs; D27 now names them); revision applied for Review 5
