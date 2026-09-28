---
dod: 2
rubric: 2
id: dod-20260928-bos1
slug: boss-reinforcements
title: Boss reinforcements — adds, phases and the anti-carry burst, and release 0.8.0
status: draft
size: L
parent: nyarlathotep
kind: feature
created: 2026-09-28
baselined: none
closed: none
commit: 284f840
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: pending
review: pending
---

# DoD: Boss reinforcements — adds, phases and the anti-carry burst, and release 0.8.0

**Size:** L. It touches several modules:
- **Logic:** a new BossFights.cs (fight table, boss filter, phase rule, burst window, fight targets, fight lifecycle); Model and Validation (three trigger types, the AroundBoss location, the JoinFight behaviour, the EmpowerBoss action); Engine (the boss route, fights inside an instance, WaveGate's boss skips); Empowerment (a single-boss carrier entry); Hooks, Limits, Wire, ApiLines, AdminLines and the event-set fields.
- **Services and Patches:** a new Services/BossFights.cs; three new patches (aggro, behaviour state, stat change); TriggerBus, DeathEventPatch, WaveAction, SpawnTracker, UnitSetup, HuntAction, EmpowerAction, EventRuntime, Pusher and HealthMonitor.
- **Commands:** a new `.nyar boss fights`; `event set` and `event new` gain the boss fields.
- **Other:** two cfg keys and one limit, one template, the contract from api 5 to api 6, rollback-drill's newer-key list, and the six surfaces of 0.8.0. No new dependency and no new data file; events.json gains optional keys and types without a schema bump.

**Planned:** autonomously, by Claude under the owner's standing directive of 2026-09-28 ("proceed autonomously … present decisions in plan mode"). The owner settled the scope in the Epic's `## Child constraints` › boss-reinforcements entry and A23 (plan-mode decision 2A). Every choice this plan makes inside that scope is a `reversible` assumption with its fallback, listed under Assumptions for the owner to see. This plan is a child of the approved Epic `nyarlathotep`.

**Precondition:** step 1 starts only after event-spawns has closed and v0.7.0 is tagged, in the order of design §9 D20 (walkable-spawns, raphael-api-admin, regions, event-spawns, boss-reinforcements). It builds on event-spawns' SpawnWaves keys (behaviour, modifiers, loot), its HuntAction seed set and WaveGate, walkable-spawns' SpawnPoints, and regions' scope and RegionIndex. If event-spawns released under another number, S-5 below applies before step 1.

**Request:** the Epic's child constraint: "keys fights by boss entity, dedupes triggers within 5 s, despawns adds on boss death or reset, excludes BloodyBoss-renamed bosses by default; besides BossHealthPhase (e.g. adds at 80 % health) it plans the owner's anti-carry trigger BossBurstDamage {boss?, percent, windowSeconds} (e.g. more than 40 % of the boss's health lost in under 2 s), which can empower that one boss through a boss-targeted carrier and call a reinforcement wave at once (A23)". Contract §10 names the release: "boss-reinforcements | 0.8.0 | the push `boss-adds`".

## Definition of Done
- [ ] D1 · **Fight tracker keyed by boss** Logic/BossFights.cs `FightTable` holds the open fights keyed by the boss's entity handle (a long of Index and Version); `Engage(boss, prefab, name, immobile, utcNow)` opens a fight numbered from 1 per boot unless BossFilter excludes the boss (D8), a fight for that entity is already open (a no-op, so every further aggro event of the fight is absorbed), or the table already holds Limits.MaxBossFights fights (new limit, default 4, 1-10; "boss fights full (<n>)" logged once per streak); `End(boss, reason)` with reason death, reset, gone, stop, purge or hardend closes a fight exactly once and returns its number, its event ids and its per-definition fired flags for cleanup; a boss engaged again after its fight ended opens a new fight with no fired flag (re-armed) · test: Nyarlathotep.Tests BossFightTests Table (fails when: two aggro events for one boss open two fights, a fight opens at the cap or for an excluded boss, an ended fight ends twice or reports its cleanup twice, a re-engaged boss keeps a fired flag, or two bosses engaged together share one fight)
- [ ] D2 · **Boss triggers and actions validated** Validation accepts the trigger types BossEngaged {bosses?}, BossHealthPhase {bosses?, belowPercent, once?} and BossBurstDamage {bosses?, percent, windowSeconds, once?}: bosses follows VBloodKilled's rule (["any"] or 1-20 known CHAR_ names) and is ["any"] when absent (S-19); belowPercent an integer 1-99; percent an integer 5-100; windowSeconds a number 0.5-10 with at most one decimal; once a JSON boolean, true when absent; each takes regions' `scope`; a boss trigger needs pillar boss and pillar boss needs a boss trigger (S-20); the location AroundBoss {minDist 2-30, maxDist 4-40, minDist < maxDist} is allowed only with a boss trigger; the behaviour {"type": "JoinFight"} only with an AroundBoss location; the action EmpowerBoss {stats} (EmpowerStats' rule: each 1.0-3.0, at least one above 1.0) only in pillar boss; each failure disables the event with one reason naming the field; SchemaVersion stays 1 and a 0.7.0 definition loads unchanged · test: Nyarlathotep.Tests EventValidationTests Boss (fails when: a belowPercent of 0, 100 or 50.5, a percent of 4 or 101, a windowSeconds of 0.4, 10.1 or 1.25, once "true", an unknown boss name, a boss trigger in pillar spawns, pillar boss with a Schedule trigger, AroundBoss with a Manual trigger, minDist 12 with maxDist 10, JoinFight with a Point location, or EmpowerBoss in pillar empowerment is accepted; or a valid definition of each trigger type is disabled; or a 0.7.0 definition's parse result changes)
- [ ] D3 · **Health phase crossing** Logic `PhaseRule.Crossed(previous, current, belowPercent)` fires when the previous sample was at or above the threshold and the current one is below it, and on a fight's first sample when that sample is already below (a boss engaged under the threshold); once true fires once per fight and definition; once false re-arms when health rises to the threshold plus 5 points (S-12); Services/BossFights samples Health.Value / Health.MaxHealth of every open fight's boss on the 1 s scheduler tick (S-10), a sample with MaxHealth 0 or a non-finite value being ignored and counted in the verbose log · test: Nyarlathotep.Tests BossFightTests Phase (fails when: a sample equal to the threshold fires, a crossing from 51 % to 49 % at 50 does not fire, a once definition fires twice in one fight, a once false definition fires again before health passes the threshold plus 5 or fails to fire after it, a first sample under the threshold does not fire, or a MaxHealth 0 sample fires)
- [ ] D4 · **Burst damage window** Logic `BurstWindow` keeps per fight the damage entries (utc time, amount) the BossDamage hook reports for the boss (negative Health changes only; heals are ignored, S-11) and fires a definition when the sum within its last windowSeconds, as a percent of the boss's MaxHealth at the newest entry, reaches its percent (inclusive); once true fires once per fight and definition; once false re-arms after windowSeconds without a firing; entries older than 10 s are pruned and at most 512 are kept per fight (the oldest dropped and counted) · test: Nyarlathotep.Tests BossFightTests Burst (fails when: 40 % dealt over 2.1 s fires a 40 % in 2 s definition, 40 % over 1.9 s does not fire, exactly 40 % does not fire, a heal offsets damage, a once definition fires twice in one fight, a once false one fires again inside its window, an entry older than 10 s counts, or a 513th entry grows the queue)
- [ ] D5 · **Boss triggers route** Logic `TriggerRouter.Boss(set, type, bossPrefab, region)` returns the enabled definitions of that trigger type whose bosses are "any" or name the prefab and whose trigger scope contains the region of the boss's position (regions D4's rule applied to the boss, S-8: a Global scope reads no position, an unreadable position reaches only Global definitions); TriggerBus fires each through TriggerDedupe with the key "<id>|<type>|<fight>" (the 5 s window of the Epic's child constraint); every firing meets Precedence.StartBlocker and the definition's conditions, with cooldownMinutes counted from its last firing (S-18); BossEngaged fires when its fight opens, BossHealthPhase from D3, BossBurstDamage from D4 · test: Nyarlathotep.Tests TriggerActivationTests Boss (fails when: a definition for another boss fires, "any" misses a boss, a scoped definition fires for a boss outside its regions or at an unreadable position, a Global one fails to fire, a second firing within 5 s for the same fight passes the dedupe, a firing for another fight is deduped, a blocked control or an unmet condition still fires, or BossEngaged fires twice in one fight)
- [ ] D6 · **Fights inside one instance** a boss definition keeps the engine's one instance per definition (S-14): its first firing starts the instance with that fight, and a firing for another open fight while it runs joins that fight to the running ActiveEvent instead of answering "already active"; each fight has its own waves, adds, seeds and carrier; the instance ends when its last fight ends or at durationSeconds (the hard end), whichever comes first; `.nyar event stop <id>` ends every fight of it; its `.nyar status` row and `[NYAR:event]` row stay one per definition, units summed over its fights · test: Nyarlathotep.Tests EngineTests BossFights (fails when: a second boss's firing is refused or starts a second instance, ending one fight touches another's adds, seeds or carrier, the instance outlives its last fight or its duration, stop leaves a fight open, or the status shows one row per fight)
- [ ] D7 · **Adds spawn around and join** an AroundBoss wave takes the boss's Translation when the wave is queued as its centre and places its units minDist..maxDist from it through walkable-spawns' SpawnPoints with regions' scope check; WaveGate (event-spawns D29) gains the skips "boss gone" (the boss no longer exists, is dead, or its fight has ended) and "boss outside action scope" right after the control block; a JoinFight unit, ours only and never the boss or a native unit, gets the boss's Team copied, FactionReference untouched, AggroConsumer.Active on, PreCombatPosition set to the boss's position and MaxDistanceFromPreCombatPosition and ProximityRadius set to 40 (S-16), and is seeded every 5 s through event-spawns' HuntAction seed set (HuntPlan.Diff) with Logic `FightTargets.From(bossAggro, players, bossPosition)`: the player characters in the boss's AggroBuffer that are online, alive and within 60 m of the boss, nearest first, at most 5 (S-17); EntityOwner is not set (S-16) · test: Nyarlathotep.Tests SpawningTests JoinFight (fails when: a target beyond 60 m, dead, offline or absent from the boss's AggroBuffer is chosen, a sixth target is added, the order is not by distance, the recipe writes the boss or changes FactionReference, a wave for a dead or ended boss spawns, or a boss outside the action scope gets a wave)
- [ ] D8 · **BloodyBoss and excluded bosses** Logic `BossFilter.Excluded(prefab, name, immobile, settings)` excludes a BloodyBoss world boss (a NameableInteractable name ending in "bb", ordinal, with a length of at least 34) unless the new cfg Boss.IncludeBloodyBoss is true (default false); a prefab in the new cfg Boss.ExcludedBosses (default CHAR_Vampire_Dracula_VBlood, CHAR_ChurchOfLight_Paladin_VBlood and CHAR_Gloomrot_Monster_VBlood, the teleporting bosses of docs/features/BOSS_REINFORCEMENTS.md, S-15); and a boss part whose behaviour tree is Immobile; an unknown name in the list is logged once at boot as "boss: unknown excluded boss <name>" and ignored · test: Nyarlathotep.Tests BossFightTests Filter (fails when: a 34-character name ending in "bb" opens a fight with the default, a 33-character one ending in "bb" or a name ending in "Bb" is excluded, IncludeBloodyBoss true still excludes it, a listed prefab opens a fight, an Immobile boss part opens one, or an unknown listed name throws)
- [ ] D9 · **Adds leave with the fight** every end path of a fight leaves nothing of it: death (DeathEventPatch sees an open fight's boss die, or the fight tick reads Health.IsDead), reset (S-9: the boss has left AnyCombat and its health is back to 99 % or more, or it has been out of combat for 60 s), gone (the boss entity no longer exists), `.nyar event stop`, `.nyar purge confirm` (Epic D11), the instance's hard end, the fault cancel (Epic D25) and a restart (the boot marker sweep); each queues the fight's adds to SpawnTracker's despawn budget through the new SpawnTracker.EndFightUnits(eventId, fight) (MaxDespawnsPerTick, no grace), stops their seeding and removes the fight's boss carrier (D10), as planned by Logic `FightLifecycle.End`; the triggers re-arm for the boss's next fight · test: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter BossEndPaths → "Passed!" with at least 8 tests (fails when: any end path leaves an add tracked past its drain, a seed or a carrier, a reset is taken while the boss is still in combat, a boss out of combat at 90 % health is not reset after 60 s, ending one fight queues another fight's adds, the despawns exceed MaxDespawnsPerTick in a tick, or the filter runs fewer than 8 tests)
- [ ] D10 · **Boss carrier** an EmpowerBoss action empowers the fight's boss alone: CarrierLedger gains the public `ApplyOne(eventId, fight, unit, stats, endsUtc)` over the existing private Apply stages (create and mark, Lifetime, Strip, Modifiers) inside EmpowerBatchPerTick, and Logic `BossEligibility.Decide(facts)` skips a boss that no longer exists, is dead, has no VBloodUnit, is owned by a player, already holds a carrier of any event ("carried"), or stands outside the action scope; the carrier's LifeTime ends with the instance, and a boss holds one carrier at most; it is removed at its fight's end (D9), at stop, purge and the instance's end, and after a restart by the existing boot marker sweep; nothing is written to the boss outside the carrier (CLAUDE.md spawn and buff safety) · test: Nyarlathotep.Tests CarrierLedgerTests Boss (fails when: a boss already carried gets a second carrier, a dead or player-owned boss or one outside the scope is empowered, the carrier's LifeTime outlasts the instance, a fight's end leaves its carrier, ApplyOne exceeds the tick budget, or a fight's end touches a faction sweep's carriers)
- [ ] D11 · **Boss hooks, health and faults** Logic/Hooks.cs Hook gains BossAggro (a PlayerCombatBuffSystem_InitialApplication_Aggro.OnUpdate prefix reading InverseAggroEvents.Added with a PlayerCharacter Producer and a VBloodUnit Consumer, S-7), BossBehaviour (a CreateGameplayEventOnBehaviourStateChangedSystem prefix reading BehaviourTreeStateChangedEvent for an open fight's boss) and BossDamage (a StatChangeSystem.ApplyStatChanges postfix taking StatType.Health changes below 0 on an open fight's boss, returning at once when no fight is open); each patch follows the patch-guard rule (Core.IsReady guard, then try/catch); TriggerBus.Registry checks each applied; HookFor maps BossEngaged and BossHealthPhase to BossAggro, and BossBurstDamage to BossAggro and BossDamage, so a definition whose hook is unavailable is not started and HealthMonitor.Degraded shows "hook <name>"; without BossBehaviour a fight resets on the 60 s out-of-combat rule alone and health shows "boss: reset hook unavailable"; a throw inside a patch body or the fight tick logs once per streak ("boss <hook>: failed (<message>)") and ends only the fight it concerns, as gone · test: Nyarlathotep.Tests DependencyFailureTests Boss, also run as dotnet test Nyarlathotep/Nyarlathotep.Tests --filter Boss_ → "Passed!" with at least 6 tests (fails when: an unavailable hook still starts a definition that needs it, the health entry is missing or stays after the hook is back, a throw ends another fight or escapes the patch, a line repeats within a streak, a missing BossBehaviour stops resets, or the filter runs fewer than 6 tests); cmd: pwsh tools/preflight.ps1 → "patch guards: <n>/<n>" (fails when: fixture PatchGuards/bad-5, a boss patch without its IsReady guard, passes -SelfTest)
- [ ] D12 · **Hooks proved in game** (profile note 6.1) Session 1 on the local server (127.0.0.1:9876), with Debug.VerboseLogging on and the step 2 DLL, whose hooks open and end fights and log but fire no definition: engaging Alpha Wolf (CHAR_Forest_Wolf_VBlood) logs "boss probe: aggro CHAR_Forest_Wolf_VBlood" and opens one fight in `.nyar boss fights`; hitting it logs "boss probe: damage <amount>" lines whose sum matches the health the boss lost (±5 %); running beyond its leash logs "boss probe: behaviour <state>" leaving combat and then "boss fight <n> ended: reset"; a kill logs "boss fight <n> ended: death"; a hook that stays silent takes its S-7, S-9 or S-11 fallback through a corrected amendment before step 3 · manual: Session 1 in docs/features/BOSS_REINFORCEMENTS.md › Test results records each line and the go or no-go per hook
- [ ] D13 · **Commands, fields and template** `.nyar boss fights` (adminOnly) replies one line per open fight, "<boss> fight <n>: <p>% health, <a> adds, carrier <on|off>, <s>s", then "boss fights: <k> of <max>", or "no boss fights"; any other argument replies "argument must be fights"; no line names a player or a position; `.nyar event set <id>` takes trigger.bosses (now also for the boss triggers), trigger.belowPercent, trigger.once, trigger.percent, trigger.windowSeconds, action.location aroundboss <minDist> <maxDist>, action.behaviour joinfight and action.stats.<stat> on an EmpowerBoss definition, each checked by D2's rule and written through the existing editor and SetEventField row; `trigger.type` set to a boss type gives that type's default; `.nyar event new <id> boss` gives a disabled BossEngaged skeleton with bosses ["any"], an AroundBoss 6 12 location and JoinFight (S-20); templates.json gains the disabled template "boss-calls-hunters" (BossHealthPhase 50, two CHAR_Militia_Crossbow_Summon, AroundBoss 6 12, JoinFight) and every template stays valid · test: Nyarlathotep.Tests AdminLinesTests Boss, AuthoringTests Boss, CommandArgTests Boss and TemplateLibraryTests StarterTemplates (fails when: a line differs from Design › UX or exceeds 480 bytes, a line names a player or a position, a value D2 refuses is written, a field is accepted on a definition of another trigger or action type, the boss skeleton fails D2 once its units are set, or a template fails validation or ships enabled)
- [ ] D14 · **Wire at api 6** the push `[NYAR:ev] type=boss-adds id=<event> secs=0 boss=<V Blood prefab> phase=<n> count=<adds spawned>` follows each AroundBoss wave that spawned at least one add, phase being belowPercent for BossHealthPhase, 100 for BossEngaged and the boss's health percent, rounded down, for BossBurstDamage; it goes only to subscribers who are the fight's targets (D7) or admins (S-17); `[NYAR:def]` rows carry trigger=bossengaged, bosshealth or bossburst and action=boss; `[NYAR:event]` rows of a boss event carry kind=boss; Wire.Api is 6 (S-6) and docs/RAPHAEL_INTEGRATION_CONTRACT.md marks `boss-adds` IMPLEMENTED (api 6), adds `bossburst` to §3's trigger list, moves §10's boss-reinforcements row and §10.4's boss-adds row into §3, adds a §9 row and says "**Current api:** 6"; docs/RAPHAEL_HANDOFF_API4.md gains the boss-adds note · test: Nyarlathotep.Tests WireFormatTests Boss, PushTests Boss and ContractDocTests (fails when: a key set or order differs from the contract's example, a push reaches a subscriber who is neither a target nor an admin, a wave of zero adds pushes, a line exceeds 480 bytes with the longest V Blood prefab, Wire.Api differs from the contract, or a row is still PLANNED); cmd: pwsh tools/preflight.ps1 → "wire contract: <n> tags, <m> api commands, all documented (api 6)" (fails when: the contract and Wire.Api differ)
- [ ] D15 · **Authorization and static checks** `boss` is one adminOnly command with the ready guard; the new [Mutating] methods (Services/BossFights' tick and end, SpawnTracker.EndFightUnits, the JoinFight recipe and seeding, EmpowerAction's single-boss apply) are called only from Gateway.Run, SpawnTracker, EventRuntime or the System actor's tick and patch callers that event-spawns D22 accepts, BossFights joining $DispatchedServices in tools/preflight.ps1; the entity-writes check (event-spawns D34) passes with no write to a boss outside the carrier; the new event set fields go through SetEventField; ControlCases.Table gains a "boss-reinforcements Dn" row for each control of this plan, the set being every D-item whose evidence carries a `(fails when:` clause (D1-D11, D13-D15, D17-D20), and Nyarlathotep.Tests.csproj copies docs/dod/boss-reinforcements.md · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (…)"; pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests"; pwsh tools/preflight.ps1 → PREFLIGHT OK with "commands: <n> admin-only, <m> public (allow-listed)" (fails when: `boss` loses adminOnly or its ready guard, fixture Commands/bad-4 (a `boss` command without adminOnly) passes, fixture GatewayOnly/bad-9 (SpawnTracker.EndFightUnits called from Commands/) passes, a boss write outside a dispatched service passes, or a control lacks its row)
- [ ] D16 · **A real boss fight** Session 2 on the local server (127.0.0.1:9876) with the boss pillar on and definitions on Errol (CHAR_Bandit_StoneBreaker_VBlood) and Alpha Wolf: BossEngaged spawns its adds once per fight; BossHealthPhase 50 spawns two adds that attack the owner (seen, and counted in `.nyar boss fights`); running away until the reset despawns the adds and removes the carrier, and a new engagement fires BossEngaged again; BossBurstDamage 40 % in 2 s, set off by the owner at a high gear level, puts a carrier on the boss (seen in `.nyar debug here`) and calls a wave from a second definition on the same trigger; the kill despawns the adds, staged; `.nyar event stop`, `.nyar purge confirm` and a restart mid-fight (boot sweep, then "0 found") each leave 0 tracked units and no carrier; with Debug.TimingLog on, every "tick timing" line during the fights shows avg under 5 ms (Epic D24); every new chat line of D13 and D2's reasons in `.nyar event list` shows whole in the game's chat and reads without colour (profile note 11.2) · manual: Session 2 in docs/features/BOSS_REINFORCEMENTS.md › Test results records each observed line
- [ ] D17 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/BOSS_REINFORCEMENTS.md › Test results, with its -LogCheck line in docs/audits/boss-reinforcements.md before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/preflight-checks.json gains `childDocs`, `snapshotSessions` (from session 1) and `dataTables` entries for boss-reinforcements and a `dataTests` entry naming BossEndPaths' class; tools/data-inventory.json has a "boss-reinforcements › <artifact>" entry per Design › Data row · cmd: pwsh tools/preflight.ps1 -AuditOf boss-reinforcements → "audit steps: boss-reinforcements 4/4 pre, 4/4 post, 4/4 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf boss-reinforcements → "session logs: boss-reinforcements 2/2 checked; snapshots 2/2 from session 1"; pwsh tools/preflight.ps1 -Paths -DeclaredOf boss-reinforcements → "paths: <n> walked, all in manifest; declared: <d>/<d>; data tests <t> passed" (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a walked path matches no manifest glob or is undeclared, a %TEMP%\nyar-* folder outlives its session, a Data row lacks an inventory entry or field, or a listed data test class runs zero tests or one fails)
- [ ] D18 · **Secrets and privacy** this child adds no credential; no wire line, announcement, reply or log line carries a player's name, SteamID or position because of a boss fight: `boss-adds` names only the event, the boss prefab, the phase and a count, and goes only to the fight's targets and admins (D14); `boss fights` names only bosses; the probe and fight log lines name prefabs and fight numbers only; no tracked file holds a SteamID digit run or the owner's mail name except lines quoting the pattern · cmd: pwsh tools/preflight.ps1 → "secrets: none (<n> files scanned…)"; then, before each push, the privacy grep the earlier children run (git grep -n -E over the SteamID prefix and the owner's mail name, the pattern quoted in docs/dod/regions.md D14 and not repeated here) → only lines that quote the pattern; test: Nyarlathotep.Tests PrivacyTests Boss (fails when: a token shape is in the scanned set, which the existing Secrets fixtures plant under -SelfTest, a boss line, push or log line carries a player name, SteamID or position, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D19 · **Tick cost bounded** the fight tick reads at most one boss per open fight (health, position, state), so at most Limits.MaxBossFights reads per scheduler tick; the BossDamage postfix returns before reading any event when no fight is open and otherwise costs one set lookup per health change; BurstWindow handles 10,000 entries over 4 fights, and FightTable 10,000 engage and sample calls, each in under 20 ms on the dev machine; Session 2 times the tick during real fights (D16) · test: Nyarlathotep.Tests BossFightTests Cost (fails when: either run exceeds 20 ms, the postfix's counting fake records an event read with no fight open, or a tick samples more bosses than open fights)
- [ ] D20 · **Release 0.8.0** csproj Version and thunderstore.toml versionNumber are 0.8.0; both changelogs and both READMEs describe the three boss triggers, AroundBoss and JoinFight, EmpowerBoss, the BloodyBoss and teleporting-boss exclusions, `.nyar boss fights` and api 6; the README states that 0.7.x disables, after a rollback, a definition using a boss trigger, AroundBoss, JoinFight or EmpowerBoss; tools/rollback-drill.ps1 accepts that 0.7.0 disables definitions 0.8.0 understands only when every extra disabled definition's reason is "unknown trigger type Boss…", "unknown action type EmpowerBoss", the AroundBoss "action.location must be …" or the JoinFight "unknown behaviour type", printing "(<k> newer keys)", its selftest gaining one pass pair and one fail pair; the annotated tag v0.8.0 is pushed and the GitHub pre-release carries the tcli zip; after a failed or ambiguous `gh release create` the step runs `gh release view v0.8.0` and retries only when the release is absent; no tcli publish · cmd: pwsh tools/rollback-drill.ps1 -SelfTest → "drill selftest: <n>/<n>"; pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.7.0 -To v0.8.0 -Plan boss-reinforcements → "rollback gate: 4/4"; then pwsh tools/release-verify.ps1 -Tag v0.8.0 -Asset kdpen-Nyarlathotep-0.8.0.zip → "release verify: hashes equal" (fails when: a surface differs, 0.7.0 does not initialize on 0.8.0's files, a count difference with another reason passes the drill, the repository revert of v0.7.0..v0.8.0 is not clean, a Rollback route is missing or names another range, the tag is missing or unpushed, the release has no zip, or the hashes differ)

## Purpose & typical use
- **Who:** a server admin whose V Blood fights feel too easy, especially when a high-level player carries newcomers through a low-level area. They'd say "when Errol drops to half health, have his crossbowmen join the fight" or "if someone melts the Alpha Wolf in two seconds, make him fight back".
- **Job:** a V Blood fight gets harder while it lasts: adds join when it starts, at a health threshold, or when the boss takes a burst of damage; a burst can also empower that one boss. Everything the fight brought leaves when the boss dies or the fight resets.
- **Coexists with:**
  - event-spawns' SpawnWaves pipeline, HuntAction seeding and WaveGate (D7);
  - faction-empowerment's CarrierLedger, gaining a single-unit entry (D10);
  - regions' scope, applied at the boss's position (D5, D7);
  - the later children: anti-farming's V Blood copies and outbreak's boss-led hordes reuse the fight table's filter and the single-boss carrier; stats counts "boss adds killed" from the adds this child tracks.

## Use cases
### Typical
The admin runs `.nyar template use boss-calls-hunters` (D13), sets `trigger.bosses CHAR_Bandit_StoneBreaker_VBlood` and enables it with the boss pillar on. A player engages Errol: the fight opens (D1). At 50 % health (D3) two crossbowmen appear 6-12 m from Errol (D7), take his team and attack the players in his aggro list. The player runs away; Errol resets, the crossbowmen are despawned (D9), and the next engagement starts fresh. The fight's players with Raphael see `boss-adds … phase=50 count=2` (D14).

### Minimal stretch
- **Least use (8.1):** an events.json without a boss definition runs exactly as 0.7.0 (D2); the boss pillar ships off. A boss definition with defaults only (`event new <id> boss`, then units) is valid (D13).
- **Once and never again (8.2):** a fight leaves nothing when it ends (D9); an unused fight table holds nothing and the damage hook returns at once (D19). Disabling the definition or the pillar stops new fights; open ones end with their instance (D6, D9).

### Maximal stretch
- **Volume (9.1):** ten bosses engaged at once meet Limits.MaxBossFights (default 4); the fifth is not tracked and the log says so once (D1). Each fight's waves stay inside MaxUnitsPerWave and MaxTrackedUnits; the damage hook costs one set lookup per health change (D19).
- **Abuse (9.2):** players cannot reach any of it: `boss` is admin-only and the definitions come from events.json and admin edits (D15). A player farming the trigger by re-engaging a boss gets one fight per engagement, deduped within 5 s (D5); a player carrying low-level friends through a boss is what the burst trigger answers (D4). A BloodyBoss world boss, which that mod already scales, is left alone (D8).
- **Repeated use (9.3):** a hundred aggro events of one fight open one fight (D1); a trigger for the same fight within 5 s fires once (D5); `boss fights` is a pure read.

## Business rules
1. **Thresholds (4.1, D2-D4):**
   - **BossHealthPhase:** fires when health falls below belowPercent (1-99) of MaxHealth, crossing from at or above to below (D3).
   - **BossBurstDamage:** fires when the damage in the last windowSeconds (0.5-10) reaches percent (5-100) of MaxHealth, inclusive; heals do not count (D4).
   - **BossEngaged:** fires when the fight opens (D5).
   - **once:** true (the default) fires once per fight and definition; false re-arms per D3 and D4.
   - The authoritative document for each threshold is this plan's D2; the feature doc quotes it.
2. **A fight (4.1, D1, D9):** opens at the first player aggro on a V Blood unit (gate bosses included) that BossFilter does not exclude (D8), and ends once, by death, reset, gone, stop, purge or the instance's hard end. A reset is "left combat, then health back to 99 % or more, or 60 s out of combat" (S-9).
3. **Invariants (4.2):**
   - One fight per boss entity at a time (D1), and one instance per definition holding its fights (D6).
   - One carrier per boss (D10). Nothing is written to a boss except the timed carrier; direct component writes touch only units we spawned (D7, CLAUDE.md).
   - Every add is tracked by SpawnTracker and leaves with its fight (D9).
4. **Time (4.3):**
   - Health is sampled on the 1 s tick (D3); damage is taken as it happens (D4).
   - The dedupe window is 5 s (D5); a burst window slides with each entry (D4).
   - A carrier's LifeTime ends with its instance (D10); a fight's adds get no grace (D9).
   - Times are UTC in memory; nothing about a fight persists across a restart (D9).
5. **Precedence (4.4):**
   - The existing controls come first: Precedence.StartBlocker (mod, pillar, purge cooldown, caps), then the definition's conditions, then the trigger scope (D5).
   - For a wave: WaveGate's control block, then "boss gone", then "boss outside action scope", then event-spawns' roll and caps (D7).
   - For a carrier: BossEligibility's skips in their order, the action scope last (D10).
   - There is no exception path; no actor can bypass these rules.
6. **"Every" sets (4.5):**
   - "Every end path of a fight" is the eight paths of D9, each a BossEndPaths test.
   - "Every boss trigger" is the three trigger types added to Logic/Model.cs TriggerType, each with a D5 route case and a HookFor mapping (D11).
   - "Every control" is every D-item whose evidence carries a `(fails when:` clause; ControlCaseTests reads this plan (D15).
   - "Every path this child writes" is computed by `-Paths -DeclaredOf boss-reinforcements` (D17; known exclusion: a path created and deleted inside one step, S-3).

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:** Logic/Model.cs (Trigger, SpawnWavesAction, EmpowerStats, Location); Logic/Validation.cs (ParseTrigger's VBloodKilled bosses rule, ParseAction, ParseLocation, event-spawns' behaviour parse); Logic/Engine.cs (TriggerRouter, EventEngine.Start, ActiveEvent); Logic/Idempotency.cs (TriggerDedupe, InstanceGuard); Logic/Empowerment.cs (CarrierLedger, CarrierRecipe, Ownership); event-spawns' Logic/Spawning.cs (WaveGate, HuntPlan) and Services/HuntAction.cs; walkable-spawns' SpawnPoints; regions' Logic/Regions.cs (RegionIndex, Scope); Services/TriggerBus.cs; Patches/DeathEventPatch.cs.
- **Writes:**
  - Logic/BossFights.cs (new): FightTable, BossFilter, PhaseRule, BurstWindow, FightTargets, FightLifecycle, BossEligibility.
  - Logic/Model.cs: TriggerType gains BossEngaged, BossHealthPhase and BossBurstDamage; Trigger gains BelowPercent, Percent, WindowSeconds and Once (defaulted); LocationType gains AroundBoss with MinDist and MaxDist; the Behaviour record gains JoinFight; a new EmpowerBossAction record on EventDefinition.
  - Logic/Validation.cs (D2); Logic/Engine.cs (the boss route, fights in ActiveEvent, EventActionKind.EmpowerBoss); event-spawns' WaveGate (the two skips); Logic/Empowerment.cs (ApplyOne); Logic/Hooks.cs (three hooks); Logic/Limits.cs (MaxBossFights).
  - Logic/Wire.cs and Logic/ApiLines.cs (api 6, trigger names, boss-adds); Logic/AdminLines.cs (boss fights lines); Logic/EventAdmin.cs, CommandArgs.cs, Authoring.cs (fields, the boss skeleton).
  - Services/BossFights.cs (new: the fight tick and hook entry points); Services/TriggerBus.cs (registry, routing); Services/WaveAction.cs, SpawnTracker.cs (EndFightUnits), UnitSetup.cs (JoinFight recipe), HuntAction.cs (fight targets), EmpowerAction.cs (single-boss apply), EventRuntime.cs, Pusher.cs, HealthMonitor.cs; Config/Settings.cs (Boss section).
  - Patches/BossAggroPatch.cs, BossBehaviourPatch.cs, BossDamagePatch.cs (new); Patches/DeathEventPatch.cs (fight death).
  - Commands/BossCommands.cs (new); Resources/templates.json.
- **What breaks if wrong:** a fight that never ends leaves adds wandering (D9, Session 2); a carrier that outlives its fight keeps a boss empowered for the next player (D10); a WaveGate order swapped by the new skips changes event-spawns' waves (D7 keeps event-spawns' WavePrecedence tests passing).
- **Shared types (5.3):** Trigger gains defaulted fields and the enums gain members, so every existing constructor call compiles unchanged; EventDefinition gains a defaulted EmpowerBoss parameter. `[NYAR:ev] type=boss-adds` is a new push type and `bossburst` a new trigger value, additive per contract §7 (D14).

### External — dependencies and their failure behaviour
- **V Rising server 1.1.12 (VampireReferenceAssemblies 1.1.12-r99041-b2) (6.1):**
  - `PlayerCombatBuffSystem_InitialApplication_Aggro.OnUpdate` and `InverseAggroEvents.Added` (Producer, Consumer). The design's `PlayerCombatBuffSystem_OnAggro` does not exist in the pinned interop; Bloodcraft's Patches/PlayerCombatBuffSystemPatch.cs:25 patches the InitialApplication system, and SanguineArchives' PlayerCombatBuffSystemPatch.cs:12-27 reads the Added events (re-implemented, not copied).
  - `CreateGameplayEventOnBehaviourStateChangedSystem` and `BehaviourTreeStateChangedEvent`, with `Health` for the reset (SanguineArchives BehaviorStateChangedSystemPatch.cs).
  - `StatChangeSystem.ApplyStatChanges(NativeArray<StatChangeEvent>)`, StatType.Health (XPRising StatChangeSystemHook.cs:16-38).
  - Read-only: Health (Value, MaxHealth, IsDead), Translation (Faust BossService), VBloodUnit, NameableInteractable, BehaviourTreeInstance, the boss's AggroBuffer, Team.
  - Written on our units only: Team, AggroConsumer (Active, MaxDistanceFromPreCombatPosition, ProximityRadius, PreCombatPosition) and AggroBuffer (Bloodcraft Utilities/Familiars.cs:826-859, TideOfWar Core.cs:395-403; spike S1: an AggroBuffer entry holds within about 60 m with line of sight and is dropped beyond about 86 m).
  - Record variants Session 1 samples: a normal V Blood (Alpha Wolf), a gate boss if one is near, an aggro from two players if the owner has a second account, a leash reset and a kill. No quota and no cost.
- **Other mods (6.1):** BloodyBoss renames its world bosses (the "bb" suffix, RESEARCH_NOTES Compatibility) and spawns adds with EntityOwner = boss; we leave its bosses alone (D8). Bloodcraft and KindredCommands are untouched.
- **Raphael (6.1):** reads `boss-adds` and `bossburst` from api 6; an api 5 parser skips an unknown push type (contract §7).
- **Build and release tooling (6.1):** versions recorded at each pre-audit; floors git 2.40, gh 2.40 (the owner's account), tcli 0.2.4 (`tcli build` only), Codex CLI (`exec -s read-only`), pwsh 7.2, a .NET SDK building net6.0.
- **Failure behaviour (6.2):**
  - A hook that is not applied: the definitions that need it do not start, health shows it (D11); a silent hook in Session 1 takes its fallback before step 3 (D12).
  - A throw in a patch body or the fight tick: logged once per streak, only that fight ends, as gone (D11).
  - Garbage values: MaxHealth 0 or a non-finite health is ignored (D3); an unreadable boss position reaches only Global definitions (D5) and skips the wave as "boss gone" (D7); a burst queue past 512 entries drops the oldest (D4).
  - A game update renaming a system makes its patch fail to apply, which lands in D11.
  - The dev tools get no injected failures: git, gh, tcli and Codex stop the step under $ErrorActionPreference Stop; an ambiguous `gh release create` is checked with `gh release view` before any retry (D20); a Codex run at capacity or timed out is retried and not counted as a round.
- **No sandbox (6.3):** the dev server (127.0.0.1:9876, save-data-nyardev), wrapped by tools/dev-snapshot.ps1 (D17). Unit tests use hand-built fights, damage entries and a fake ICarrierOps (D1, D10), never game types.

## Design
### Data
| Artifact | Where | Owner | Kept | Deleted |
|---|---|---|---|---|
| fight table, burst windows, dedupe keys | memory | Services/BossFights | while a fight is open | at the fight's end (D9); lost on restart, which ends every fight |
| adds | the game world, tracked in SpawnTracker's ledger and state.json | SpawnTracker | until their fight ends and the despawn budget drains them | by EndFightUnits (D9); after a crash by LifeTime and the boot marker sweep |
| boss carrier | a timed buff on a native V Blood | EmpowerAction's ledger | until its fight or instance ends | removed at the end (D10); after a crash by its LifeTime and the boot marker sweep |
| boss keys and types | BepInEx/config/Nyarlathotep/events.json | the operator and admins | as long as the definition | by editing or deleting the definition |
| cfg keys Boss.ExcludedBosses, Boss.IncludeBloodyBoss, Limits.MaxBossFights | BepInEx/config/kdpen.Nyarlathotep.cfg | the operator | as long as the cfg | by editing the cfg; 0.7.0 leaves them as orphans |
| template boss-calls-hunters | Nyarlathotep/Nyarlathotep/Resources/templates.json (embedded, committed) | the repository | for ever | only by a later commit |
| session log copies | %TEMP%\nyar-s*-logs | Claude | the session | deleted in the same session (D17) |
| snapshot | %TEMP%\nyar-snap-* | tools/dev-snapshot.ps1 | the session | on restore (D17) |
| session marker | %TEMP%\nyar-session | tools/dev-snapshot.ps1 | the session | when the session ends (D17) |
| review prompts | %TEMP%\dod-review-*.txt | the dod skill | until the next prompt | replaced by it; never committed |
| Claude's scratchpad (Codex prompts, outputs) | the per-session scratchpad, outside the repository | Claude | the session | removed with it; SteamIDs redacted before any Codex prompt |
| build outputs | Nyarlathotep/**/bin, obj, *.binlog (git-ignored) | Claude | until the next build | overwritten by each build; never shipped |
| release zip | Nyarlathotep/Nyarlathotep/build/*.zip, dist/ (git-ignored) | Claude | until the next release | replaced by the next tcli build; its SHA-256 is kept in the audit |
| release and drill temp folders | %TEMP%\nyar-rel-*, nyar-rollback-*, nyar-drill-*, nyar-{selftest,depsuite,snaptest,drilltest}-* | release-verify, rollback-gate, rollback-drill, preflight -SelfTest | the run | deleted by the script that made it (D17) |
| review pages | docs/dod/boss-reinforcements.review.html, docs/dod/boss-reinforcements.html | the dod skill | until the next render | overwritten by it |
| dev-server files | the server's Nyarlathotep.dll, cfg, events.json and state.json with .bak and .tmp, save-data-nyardev, NyarDev.log, LogOutput.log | Claude (Sessions 1 and 2) | the session | restored by tools/dev-snapshot.ps1 (D17) |
| tag and GitHub release v0.8.0 | origin | the owner | for ever (exist only once) | never deleted; a bad one is retitled (Epic S-19) |
| plan, reviews, audit, feature doc | docs/ (committed) | the repository | for ever | only by a later commit |

Inputs (3.1): the boss keys of events.json and the event-set fields (D2, D13), the cfg keys (D8, D1), and the game's aggro, behaviour and stat-change events (D11). Outputs (3.2): adds (D7), the boss carrier (D10), the replies of D13 and the push of D14. Migration (3.4): none; the keys are optional, SchemaVersion stays 1, and 0.7.0 disables a boss definition after a rollback, which the drill accepts and the README states (D20). Every row has a tools/data-inventory.json entry (D17).

### States
- **Empty and first run (7.1):** no boss definition and the boss pillar off: the fight table stays empty, the damage hook returns at once (D19), and `boss fights` replies "no boss fights" (D13).
- **Loading, partial and error:** a definition with a bad boss key is disabled with its reason and the others load (D2); a hook that failed to apply disables only the definitions that need it (D11).
- **Concurrent use (7.2):**
  - Two players engaging one boss open one fight (D1); two bosses fought at once each get a fight inside the same instance (D6).
  - A trigger racing its own repeat within 5 s fires once (D5).
  - Every hook, tick and command runs on the server's main thread, so the fight table needs no lock.
  - Actors: admins, the scheduler and hooks (System), Claude (builder and tester). Claude never runs two sessions on one server (D17).
- **Stale data, cancel and re-entry (7.3):**
  - A boss position and health are read at each decision, never cached across ticks (D3, D7).
  - A reset, stop, purge, hard end, fault cancel or restart ends the fight and removes what it brought (D9, D10); the next engagement is a new fight, re-armed (D1).
  - A reload keeps running instances on the definition they started with (foundation D6); new fights of a reloaded definition use the new one.

### Permissions
- **Actors (2.1):**
  - Admins run `boss fights`, edit the boss fields and start or stop boss events (D13, D15).
  - The System actor opens and ends fights, fires boss triggers and spawns adds; it cannot change a definition.
  - Players fight bosses; they reach no command here. A player's aggro opens a fight; it never gives the player a new capability.
  - Unauthenticated connections never reach chat; the operator may edit events.json and the cfg, or remove the DLL.
  - Claude builds, deploys to the dev server and pushes; the owner alone publishes. Codex reads only.
- **Unauthorised path (2.2):** `boss` is adminOnly (D15); a player's `.nyar boss fights` gets VCF's denial line.
- **Ownership (2.3):** a definition belongs to no one admin; any admin may edit it (Epic Design › Permissions). A fight belongs to its instance: when one fight ends, the others of the instance keep theirs (D6); when the players of a fight change (one dies, another joins the boss's aggro), JoinFight's targets follow the boss's AggroBuffer at the next 5 s seed (D7).

### UX
- **Where it lives (11.1):** the boss keys in events.json, `.nyar event set` and `event new <id> boss`; the template boss-calls-hunters; the README's boss section documents the triggers, the exclusions and the teleporting bosses (D20); `.nyar boss fights` shows what is live (D13).
- **Feedback (11.2):** the lines of D13, exactly:
  - "<boss> fight <n>: <p>% health, <a> adds, carrier <on|off>, <s>s", "boss fights: <k> of <max>", "no boss fights", "argument must be fights";
  - D2's reasons, as `.nyar event info` and `.nyar event list` show them;
  - the log lines of D1, D8, D11 and D12.
- **Accessibility (11.3):** chat lines under 480 bytes, plain text, no meaning by colour, keyboard only; the game offers no screen reader, an inherited limitation the README states. Session 2 records that each line shows whole (D16).
- **Activation (11.4):** nothing activates without a boss definition that is enabled with the boss pillar on. Then a player's aggro on a V Blood opens a fight; the admin sees it in `boss fights` and the Raphael user in `boss-adds` (D5, D14). Should not activate: a BloodyBoss world boss, an excluded teleporting boss, a boss part (D8), or a boss outside the trigger scope (D5).

## Security
- **Authorization (10.1):** `boss` adminOnly with the ready guard; every new writing method behind the gateway or the System actor's callers; the boss fields through the existing SetEventField row (D15).
- **Injection (10.2):** boss names are checked against the unit catalogue and the fixed rules of D2; nothing reaches a shell, query or template; JSON is written by System.Text.Json; `boss fights` prints prefab names only (D13).
- **Secrets (10.3):** none added; the Secrets check and the privacy grep (D18). gh keeps its token in the Windows credential store; TCLI_AUTH_TOKEN lives only in the owner's environment while the owner publishes.
- **Personal data (10.4):** a boss name reveals where a fight is, so `boss-adds` goes only to the fight's targets and admins, who already know (D14, S-17); no line names a player or carries a position (D18). A boss event's `.nyar status` row names the definition, not the boss; an admin who puts a boss name in an event id makes that visible, which the README says.

## Failure & observability
- **What the admin sees (12.1):**

| Failure class | Admin sees | Next action |
|---|---|---|
| A boss hook not applied (D11) | "hook <name>" in `.nyar status`, the health line and the login notice; the definitions that need it not started | report the server build; a game update renamed a system |
| Reset hook missing (D11) | "boss: reset hook unavailable" in health | none; fights reset after 60 s out of combat |
| A hook or fight tick throwing (D11) | "boss <hook>: failed (<message>)" once per streak | report the log; the other fights continue |
| Bad boss key (D2) | the event disabled with its reason in `.nyar event info` | fix the key |
| Too many fights (D1) | "boss fights full (<n>)" once per streak | raise Limits.MaxBossFights or accept it |
| Boss gone before its wave (D7) | "wave <n> of <id> skipped: boss gone" | none |
| Unknown excluded boss (D8) | "boss: unknown excluded boss <name>" at boot | fix the cfg list |

- **Logs (12.2):** "boss fight <n> opened: <prefab>" and "boss fight <n> ended: <reason> (<a> adds queued, carrier <removed|none>)" per fight (D1, D9); each firing "trigger: <type> <prefab> fight <n>" (D5); the verbose sample and probe lines (D3, D12).
- **Knowing it is broken (12.3):** the degraded entries reach the admin on login (D11); -LogCheck covers every dev session (D17).
- **Failing cases (12.4):** each check this plan introduces:

| Check | Reports a failure on | Stays silent on | Empty input |
|---|---|---|---|
| BossFightTests Table (D1) | two fights for one boss, a fight past the cap | one fight per boss | no fight: `boss fights` "no boss fights", not a pass of any trigger |
| EventValidationTests Boss (D2) | a belowPercent of 100 accepted | valid boss definitions | no boss key: 0.7.0 parse unchanged |
| BossFightTests Phase (D3) | a sample at the threshold firing | a crossing below it | no sample: nothing fires |
| BossFightTests Burst (D4) | 40 % over 2.1 s firing | 40 % over 1.9 s | no entry: nothing fires |
| TriggerActivationTests Boss (D5) | a firing for another boss or region | the matching firing | no definition: no firing |
| EngineTests BossFights (D6) | a second boss refused "already active" | two fights in one instance | no fight: instance not started |
| SpawningTests JoinFight (D7) | a target beyond 60 m | the in-range targets | no target: no seed, no throw |
| BossFightTests Filter (D8) | a "bb" name of 34 characters allowed | a normal V Blood | an empty exclusion list: only BloodyBoss and Immobile excluded |
| BossEndPaths (D9) | an end path leaving an add | every path empty | no adds: the plan is empty and reports 0, not a skip |
| CarrierLedgerTests Boss (D10) | a second carrier on a boss | one carrier | no boss: skipped with its reason |
| DependencyFailureTests Boss (D11) | an unavailable hook still firing | the hooks present | no hook registered: every boss definition blocked, health lists them |
| PatchGuards (preflight, D11) | fixture PatchGuards/bad-5 | the real tree | the existing PatchGuards/empty |
| AdminLinesTests and CommandArgTests Boss (D13) | a line off Design › UX | the stated lines | no fights: "no boss fights" |
| WireFormatTests, PushTests Boss, ContractDocTests (D14) | a push to a non-target, api 5 against 6 | api 6 lines | a wave of 0 adds: no push |
| Commands, GatewayOnly (preflight, D15) | fixtures Commands/bad-4, GatewayOnly/bad-9 | the real tree | the existing Commands/empty and GatewayOnly/empty |
| ControlCaseTests (D15) | a control without its row | the real table | "the plan lists no control id" (fail) |
| PrivacyTests Boss (D18) | a player name or position in a boss line | the built lines | zero boss lines built: fail |
| BossFightTests Cost (D19) | a run over 20 ms | the measured runs | no fight open: the postfix's fake records 0 reads |
| rollback-drill -SelfTest (D20) | a disabled definition for another reason | the newer-key reasons | no extra disabled definition: "(0 newer keys)" and a pass only when counts are equal |

Each fixture is registered under `-SelfTest` in the step that adds its check; `-SelfTest` fails when a bad or empty fixture passes or a good one fails.

One evidence command per gating probe:

| Probe | Command | Item |
|---|---|---|
| 2.1 actors | pwsh tools/preflight.ps1 -AuthSuite | D15 |
| 3.3 persistence | pwsh tools/preflight.ps1 -Paths -DeclaredOf boss-reinforcements | D17 |
| 4.4 precedence | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter "FullyQualifiedName~TriggerActivationTests|FullyQualifiedName~WavePrecedence|FullyQualifiedName~CarrierLedgerTests" | D5 D7 D10 |
| 6.2 dependency failure | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter Boss_ | D11 |
| 10.1 authorization | pwsh tools/preflight.ps1 -AuthSuite | D15 |
| 10.3 secrets | pwsh tools/preflight.ps1 ("secrets: none") | D18 |
| 12.4 failing cases | pwsh tools/preflight.ps1 -SelfTest | D15 |
| 14.3 rollback | pwsh tools/rollback-gate.ps1 -From v0.7.0 -To v0.8.0 -Plan boss-reinforcements | D20 |
| 14.4 paths | pwsh tools/preflight.ps1 -Paths -DeclaredOf boss-reinforcements | D17 |

## Performance
- **Budget and hot path (13.1):** the scheduler tick stays under 5 ms on average (Epic D24). Two hot paths: the fight tick, at most one boss read per open fight (4 by default), and the BossDamage postfix, which runs for every stat change batch on the server, returns at once with no fight open, and otherwise costs one set lookup per health change. D19 measures the Logic side; Session 2 times the tick during real fights (D16).
- **Limits (13.2):**

| Bound | Source | At the bound | Valid case excluded |
|---|---|---|---|
| 4 open fights (1-10) | Limits.MaxBossFights, sized for a small server's simultaneous V Blood fights and the tick budget | the next boss is not tracked; logged once per streak (D1) | a fifth simultaneous fight, until one ends |
| 512 damage entries per fight | a 10 s window of heavy multi-player damage, well above observed hit rates | the oldest entry dropped and counted (D4) | none within a 10 s window at realistic hit rates |
| 5 targets per add, 60 m | event-spawns' Hunt bound; spike S1's aggro range | the nearest five within 60 m (D7) | a sixth player in a raid-sized fight gets no seed, but the game's own aggro still applies |
| 1-20 bosses per trigger | the VBloodKilled rule | refused past 20 (D2) | none |
| MaxUnitsPerWave, MaxTrackedUnits | the Epic caps | event-spawns' clamps | none new |
| 480 bytes a line | contract §1 | `boss-adds` with the longest V Blood prefab fits (D14) | none |

## Build plan
Every step runs inside the Epic's `## Rollout` › Procedure: a pre-audit and a post-audit in docs/audits/boss-reinforcements.md (template docs/audits/README.md), `/code-review`, and a Codex read-only cross-inspection of the step's diff (`codex exec -s read-only -c features.experimental_windows_sandbox=true`, prompt via stdin, SteamIDs redacted) until "VERDICT: READY", its line written to the audit. Compile check: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`. Tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests`.
1. **Fights, triggers and rules in Logic.**
   - Pre-audit; create docs/audits/boss-reinforcements.md (rollback base v0.7.0) and add a `## Test results` section to docs/features/BOSS_REINFORCEMENTS.md, updating its Status and Mechanism (the pinned interop's aggro system); add the `childDocs`, `snapshotSessions`, `dataTables` and `dataTests` entries and the data-inventory rows (D17).
   - Add Logic/BossFights.cs (FightTable, BossFilter, PhaseRule, BurstWindow, FightLifecycle, BossEligibility) with BossFightTests Table, Filter, Phase and Burst and the BossEndPaths tests; the model and validation of D2 with EventValidationTests Boss; TriggerRouter.Boss and the fights inside ActiveEvent with TriggerActivationTests Boss and EngineTests BossFights; CarrierLedger.ApplyOne with CarrierLedgerTests Boss; Limits.MaxBossFights.
   - Satisfies D1, D2, D3, D4, D5, D6, D8, D9 (Logic), D10 (Logic).
2. **Hooks, the fight tick, and Session 1 (owner).**
   - Patches/BossAggroPatch.cs, BossBehaviourPatch.cs and BossDamagePatch.cs; Hook members and the registry checks; Services/BossFights.cs opening and ending fights and logging the probe lines, firing no definition yet; DeathEventPatch's fight death; Config/Settings.cs's Boss section; HealthMonitor entries; DependencyFailureTests Boss; fixture PatchGuards/bad-5 (D11).
   - Stop the server; -LogCheck on the last logs; `pwsh tools/dev-snapshot.ps1 -Save br1`; deploy with `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release`; boot the dev world ($env:SteamAppId='1604030'; VRisingServer.exe -persistentDataPath .\save-data-nyardev -serverName "Nyar Dev" -saveName nyardev -logFile .\logs\NyarDev.log).
   - Write the exact numbered Session 1 steps (server 127.0.0.1:9876, Alpha Wolf) into docs/features/BOSS_REINFORCEMENTS.md and hand them to the owner; record every observed line and the go or no-go per hook (D12); stop after an autosave; -LogCheck; read every [Error] and [Warning] line; restore the snapshot. A no-go takes its fallback as a corrected amendment before step 3.
   - Satisfies D11, D12, D17 (Session 1).
3. **Adds, carrier, commands and wire.**
   - AroundBoss placement and WaveGate's two skips in WaveAction; the JoinFight recipe in UnitSetup and FightTargets through HuntAction, with SpawningTests JoinFight (D7); SpawnTracker.EndFightUnits and the service side of every end path (D9); EmpowerAction's single-boss apply (D10); PhaseRule and BurstWindow wired to the fight tick and the damage hook, and TriggerBus firing the boss route (D3-D5).
   - Commands/BossCommands.cs, the event-set fields, `event new <id> boss` and the template (D13).
   - `boss-adds` and the trigger names, Wire.Api = 6, the contract, docs/NYARLATHOTEP_DESIGN.md §2, §3 and §6 (the aggro system's name, the command) and docs/RAPHAEL_HANDOFF_API4.md (D14).
   - $DispatchedServices, fixtures Commands/bad-4 and GatewayOnly/bad-9, ControlCases rows and the csproj link (D15); PrivacyTests Boss (D18); BossFightTests Cost (D19).
   - Satisfies D3, D4, D5, D7, D9, D10, D13, D14, D15, D18, D19.
4. **Session 2 (owner) and release 0.8.0.**
   - Stop the server; -LogCheck; `pwsh tools/dev-snapshot.ps1 -Save br2`; deploy; boot; write the exact numbered Session 2 steps (server 127.0.0.1:9876, Errol and Alpha Wolf, the burst at a high gear level, reset, kill, stop, purge, restart mid-fight, TimingLog on) into docs/features/BOSS_REINFORCEMENTS.md and hand them to the owner; record every observed line (D16); stop after an autosave; -LogCheck; read every [Error] and [Warning] line; restore the snapshot.
   - tools/rollback-drill.ps1's newer-key reasons and selftest pairs under tools/rollback-drill-fixtures/ (D20).
   - Release 0.8.0 on the six surfaces; `tcli build`; annotated tag v0.8.0; `pwsh tools/rollback-gate.ps1 -From v0.7.0 -To v0.8.0 -Plan boss-reinforcements` (4/4); privacy grep; push; GitHub pre-release; release-verify.
   - -AuditOf, -SessionsOf and -Paths -DeclaredOf boss-reinforcements; `dod close boss-reinforcements`. The owner publishes.
   - Satisfies D16, D17, D18, D20.

## Work breakdown
- W1 · **Fights and triggers**
- W1.1 · **Fight table and exclusions** · items: D1 D8 · steps: 1
- W1.2 · **Trigger rules and routing** · items: D2 D3 D4 D5 D6 · steps: 1, 3
- W2 · **Hooks**
- W2.1 · **Hooks, health and probe** · items: D11 D12 · steps: 2
- W3 · **What a fight brings**
- W3.1 · **Adds, cleanup and carrier** · items: D7 D9 D10 · steps: 1, 3
- W3.2 · **Commands, wire and checks** · items: D13 D14 D15 D18 D19 · steps: 3
- W4 · **Verification and release**
- W4.1 · **Sessions, records and release** · items: D16 D17 D20 · steps: 2, 4

## Rollout
### Shipping
One release, 0.8.0, a GitHub pre-release; D20 ends there, with release-verify. The owner publishes to Thunderstore, outside the plan. Nothing is switched on by it: the boss pillar ships off (Epic D4) and no default definition is enabled; the template is disabled (D13). Who turns it off: an admin switches the boss pillar off (`.nyar pillar boss off`, ending its events and their fights), disables or stops the event, or purges; the operator installs 0.7.0.

### Compatibility
- A 0.7.0 events.json loads unchanged (D2); SchemaVersion stays 1.
- api 5 lines keep every key; `boss-adds` and `bossburst` are additive, and api goes from 5 to 6 (contract §7, D14).
- Every human reply of 0.7.0 keeps its text; this child only adds lines (D13), so raphael-api-admin's human-reply capture stays unchanged.
- BloodyBoss's world bosses are untouched by default (D8); Bloodcraft's familiars never open a fight, since the aggro producer must be a player character (D11).

### Rollback
- **In the repository:** `git revert --no-edit v0.7.0..v0.8.0`, drilled by the rollback gate with -Plan (D20). Commits in the range that are not this child's are re-applied after a revert with `git cherry-pick`, listed from `git log v0.7.0..v0.8.0` minus those touching this child's exclusive paths.
- **On the dev server during the build:** Sessions 1 and 2 are wrapped by tools/dev-snapshot.ps1 (D17).
- **On a server:** install the 0.7.0 DLL. A definition using a boss trigger, AroundBoss, JoinFight or EmpowerBoss is disabled by 0.7.0 with "unknown trigger type", "unknown action type" or the location or behaviour reason, the drill's accepted difference (D20); every other definition runs. Adds left by a crash despawn by LifeTime and 0.7.0's boot marker sweep, and a boss carrier by its LifeTime and the same sweep, since the marker is unchanged. The cfg keys stay as orphans BepInEx ignores. This remains possible after data is written, because 0.8.0 writes nothing else.
- **Published release:** tags and releases are never deleted; a bad release is retitled and versions move forward only (Epic S-19).
- **Commit range:** v0.7.0..v0.8.0

### Paths walked
Walking the Build plan. The walker (-Paths, Epic D33) reads git's tracked, untracked and ignored files, the dev server's paths, %TEMP%\nyar-* and the remote tags and releases.
- **Step 1:**
  - docs/audits/boss-reinforcements.md, docs/features/BOSS_REINFORCEMENTS.md, tools/preflight-checks.json, tools/data-inventory.json, tools/paths-manifest.txt.
  - Nyarlathotep/Nyarlathotep/Logic/{BossFights,Model,Validation,Engine,Empowerment,Limits,Spawning}.cs.
  - Nyarlathotep/Nyarlathotep.Tests/{BossFightTests,BossEndPathsTests,EventValidationTests,TriggerActivationTests,EngineTests,CarrierLedgerTests,FakeStores,FakeCarrierOps}.cs.
- **Step 2:**
  - Nyarlathotep/Nyarlathotep/Patches/{BossAggroPatch,BossBehaviourPatch,BossDamagePatch,DeathEventPatch}.cs, Nyarlathotep/Nyarlathotep/Services/{BossFights,TriggerBus,HealthMonitor,EventScheduler}.cs, Nyarlathotep/Nyarlathotep/Logic/Hooks.cs, Nyarlathotep/Nyarlathotep/Config/Settings.cs.
  - Nyarlathotep/Nyarlathotep.Tests/DependencyFailureTests.Boss.cs; tools/preflight-fixtures/PatchGuards/bad-5/**, tools/preflight-checks.json.
  - docs/features/BOSS_REINFORCEMENTS.md, docs/audits/boss-reinforcements.md.
- **Step 3:**
  - Nyarlathotep/Nyarlathotep/Services/{WaveAction,SpawnTracker,UnitSetup,HuntAction,EmpowerAction,EventRuntime,Pusher,BossFights,TriggerBus}.cs, Nyarlathotep/Nyarlathotep/Logic/{Wire,ApiLines,AdminLines,EventAdmin,CommandArgs,Authoring,Spawning,Engine}.cs, Nyarlathotep/Nyarlathotep/Commands/{BossCommands,EventCommands}.cs, Nyarlathotep/Nyarlathotep/Resources/templates.json.
  - Nyarlathotep/Nyarlathotep.Tests/{SpawningTests,AdminLinesTests,AuthoringTests,CommandArgTests,TemplateLibraryTests,WireFormatTests,PushTests,ContractDocTests,PrivacyTests,ControlCases,ControlCaseTests,BossFightTests}.cs, Nyarlathotep/Nyarlathotep.Tests/Nyarlathotep.Tests.csproj (links this plan).
  - docs/RAPHAEL_INTEGRATION_CONTRACT.md, docs/NYARLATHOTEP_DESIGN.md, docs/RAPHAEL_HANDOFF_API4.md.
  - tools/preflight.ps1 ($script:DispatchedServices), tools/preflight-checks.json, tools/preflight-fixtures/Commands/bad-4/**, tools/preflight-fixtures/GatewayOnly/bad-9/**, tools/paths-manifest.txt.
- **Step 4:**
  - tools/rollback-drill.ps1, tools/rollback-drill-fixtures/** (the new pairs).
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - The remote tag v0.8.0 and the GitHub release v0.8.0 (`remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt), and tools/paths-manifest.txt itself.
  - docs/features/BOSS_REINFORCEMENTS.md, docs/audits/boss-reinforcements.md, docs/dod/boss-reinforcements.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Sessions 1 and 2, on the server:** BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json{,.bak,.tmp}, save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log.
- **Outside the repository** (existing `temp:` lines): %TEMP%\nyar-snap-*, %TEMP%\nyar-s*-logs, %TEMP%\nyar-session, %TEMP%\nyar-{selftest,depsuite,snaptest,drilltest}-*, nyar-rel-*, nyar-rollback-*, nyar-drill-*.
- **Build outputs** (existing ignored globs): Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/**, Nyarlathotep/Nyarlathotep/build/**.
- **Review process:** docs/dod/boss-reinforcements.reviews.md, docs/dod/boss-reinforcements.review.html and docs/dod/boss-reinforcements.html.

## Out of scope
- **Considered and excluded (15.1):**
  - `BossFightTick {every}` (the feature doc's optional sustained-pressure trigger): a once-false BossHealthPhase or BossBurstDamage covers repeated pressure; a later requested amendment may add it.
  - Default adds per boss from the boss's own summon abilities (Beelzebub's SummonTargets): the admin names the units.
  - Watching the boss's own phase buffs (script_edges.tsv) instead of polling health: kept as S-10's fallback.
  - Setting EntityOwner = boss on adds, and writing anything on the boss other than the carrier (S-16, D10).
  - A `{boss}` announcement placeholder: announcements stay the definition's own text (Security 10.4).
  - Per-fight rows in `api status` (S-14).
- **Deferred (15.2):** V Blood copies escalating a farming group (anti-farming); boss-led hordes and boss-linked horde empowerment (outbreak); the "boss adds killed" counter (stats); boss encounters with timed boss spawns (docs/features/BOSS_ENCOUNTERS.md, backlog). Each reuses FightTable's filter or the single-boss carrier.

## Also considered
- **Documentation:** the six surfaces (D20), the contract and handoff (D14), docs/features/BOSS_REINFORCEMENTS.md, design §2, §3 and §6 (D14). **Decommissioning:** contract §10's boss-reinforcements row (moved, D14). **Ownership:** the server admin; the runbook is the README's kill switch, `.nyar pillar boss off` and `boss fights`.
- **Compliance:** none; no personal data is stored or sent (Security 10.4).
- **Localisation and time formats:** prefab names are the game's identifiers; every `.nyar` line is English; seconds are integers.
- **Running cost:** none beyond the bounded tick and hook (Performance); no quota.
- **Success measurement:** Session 2's observed fights and timing (D16); no analytics.
- **Support tooling:** `.nyar boss fights`, `.nyar debug here`, the fight open and end log lines, and the health entries (D1, D9, D11, D13).

## Assumptions
- S-1 · validated · Boss reinforcements keys fights by boss entity, dedupes triggers within 5 s, despawns adds on boss death or reset, excludes BloodyBoss-renamed bosses by default, and plans BossBurstDamage, which can empower that one boss through a boss-targeted carrier and call a wave · source: Epic docs/dod/nyarlathotep.md › Child constraints › boss-reinforcements and A23 (owner, plan-mode decision 2A, 2026-09-26)
- S-2 · validated · Plans are reviewed by a fresh-context subagent (up to 3 rounds); Codex cross-inspects code diffs only · source: owner Decision 7, plan mode 2026-09-28 (design §9 D26)
- S-3 · validated · Paths a step creates and deletes inside the same step are outside the declared-paths check · source: owner decision, docs/NYARLATHOTEP_DESIGN.md §9 D18
- S-4 · validated · The profile notes apply: in-game claims have a session item (6.1), every chat text is rendered before release (11.2), every control has a test seam (12.4), end paths of a timed effect on native entities are named (7.3), and the release copies the earlier release-step amendments (14.3) · source: docs/dod/profile.md
- S-5 · reversible · This child starts after event-spawns closes (design §9 D20) and releases 0.8.0 on base v0.7.0, as contract §10's row names · fallback: if event-spawns released under another number, a corrected amendment before step 1 makes that tag the base in D20, the Rollback range and the audit, and renumbers this release to the next minor
- S-6 · reversible · The wire goes to api 6: raphael-api-admin takes api 4, regions api 5, and event-spawns adds no tag (its D20) · fallback: the number is the current api plus one at ship (contract §10's rule); a corrected amendment changes D14's number and its test
- S-7 · reversible · Engagement is read from PlayerCombatBuffSystem_InitialApplication_Aggro.OnUpdate's InverseAggroEvents.Added (Producer PlayerCharacter, Consumer VBloodUnit), since the design's PlayerCombatBuffSystem_OnAggro does not exist in VampireReferenceAssemblies 1.1.12-r99041-b2 · fallback: if Session 1 shows no aggro line (D12), a corrected amendment opens a fight at the first BossDamage entry whose source is a player character on a VBloodUnit
- S-8 · reversible · A boss trigger's `scope` is tested against the region of the boss's position when it fires, the analogue of design §9 D21's "the kill's region" for a V Blood kill; an unreadable position reaches only Global definitions (regions D4's rule) · fallback: a corrected amendment switches boss triggers to D21's "at least one online player in a named region" rule, a one-case change in TriggerRouter.Boss
- S-9 · reversible · A fight resets when the BossBehaviour hook sees its boss leave AnyCombat and its health is then back to 99 % or more, or after 60 s out of combat; death ends it through DeathEventPatch or Health.IsDead · fallback: a corrected amendment detects the reset from the boss's Buff_InCombat_VBlood buff being destroyed while Health.IsDead is false
- S-10 · reversible · Health phases are sampled on the existing 1 s scheduler tick (the design's poll) · fallback: a corrected amendment adds a 0.5 s fight poll (BloodyBoss HealthMonitorSystem) or reads the boss's own phase buffs from Reference Data/script_edges.tsv
- S-11 · reversible · Burst damage is event-driven: a StatChangeSystem.ApplyStatChanges postfix sums negative Health changes on open fights' bosses, heals ignored, returning at once with no fight open (XPRising StatChangeSystemHook.cs) · fallback: if Session 1 shows no damage line or Session 2's timing fails, a corrected amendment measures net health loss from a 0.25 s poll of open fights' bosses
- S-12 · reversible · `once` is true by default for BossHealthPhase and BossBurstDamage (answering the feature doc's open question "whether the burst fires once per fight"); once false re-arms a phase 5 points above its threshold and a burst after one window · fallback: a corrected amendment changes the default or the re-arm margin, both constants in Logic/BossFights.cs
- S-13 · reversible · Gate bosses (VBloodUnit without VBloodConsumeSource) open fights like V Bloods; `bosses` narrows it · fallback: a corrected amendment requires VBloodConsumeSource in BossFilter
- S-14 · reversible · Fights live inside one instance per definition, so the engine's one-instance rule, `event stop <id>`, the status row and contract §10.1's `code=state` stay as they are; Limits.MaxBossFights (4) bounds the fights across all definitions · fallback: a corrected amendment keys instances as "<id>@<fight>" in InstanceGuard and ActiveEvent, with per-fight status rows under a new api number
- S-15 · reversible · Boss.ExcludedBosses defaults to the teleporting bosses the feature doc names (Dracula CHAR_Vampire_Dracula_VBlood, Solarus CHAR_ChurchOfLight_Paladin_VBlood, Adam CHAR_Gloomrot_Monster_VBlood) · fallback: the operator edits the cfg list; a corrected amendment changes the default after testing one of them
- S-16 · reversible · Adds copy the boss's Team, keep their FactionReference, get PreCombatPosition at the boss and a leash of 40 m (MaxDistanceFromPreCombatPosition and ProximityRadius), and do not get EntityOwner = boss, since SpawnTracker already ends them with the fight and the boss stays unwritten · fallback: if Session 2 shows adds wandering off or not fighting, a corrected amendment sets EntityOwner = boss on our adds only (BloodyBoss's way) or widens the leash
- S-17 · reversible · JoinFight seeds the player characters in the boss's AggroBuffer within 60 m, nearest first, at most 5, through event-spawns' HuntAction seed set; `boss-adds` goes only to the fight's targets and admins (contract §3's fairness rule: the boss name says where a fight is), with phase = belowPercent, 100 for BossEngaged, or the boss's health percent for a burst · fallback: a corrected amendment seeds players within 40 m of the boss, or broadcasts `boss-adds` to every subscriber if the owner prefers the §10.4 audience
- S-18 · reversible · A boss definition's conditions gate every firing, and cooldownMinutes counts from its last firing, not only its last instance start · fallback: a corrected amendment gates only the instance's start, as for the other triggers
- S-19 · reversible · Boss triggers reuse VBloodKilled's `bosses` list (["any"] or 1-20 names) and default it to ["any"], instead of the feature doc's singular `boss` · fallback: a corrected amendment accepts `boss` as an alias the parse stores as a one-name list
- S-20 · reversible · Pillar boss requires a boss trigger, so `.nyar event new <id> boss` gives a BossEngaged skeleton instead of design §6's Manual trigger; burst empowerment and a burst wave are two definitions on the same trigger, each holding one action, with EmpowerBoss its own action type in pillar boss · fallback: a corrected amendment allows a Manual trigger in pillar boss for EmpowerBoss tests, or adds a second action key to one definition

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D15 D18; 2.2 D15; 2.3 D6 D7 D13 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D2 D8 D11 D13; 3.2 D7 D10 D13 D14; 3.3 D17 D9 D10; 3.4 D2 D20 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D2 D3 D4 D5 D9; 4.2 D1 D6 D10; 4.3 D3 D4 D5 D9; 4.4 D5 D7 D10; 4.5 D9 D11 D15 D17 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D5 D7 D10; 5.2 D6 D7 D9 D10; 5.3 D2 D14 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D11 D12 D16 D8; 6.2 D11 D3 D7 D20; 6.3 D17 D1 D10 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D1 D2 D13; 7.2 D1 D5 D6 D17; 7.3 D9 D10 D1 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D2 D13; 8.2 D9 D19 D6 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D1 D19; 9.2 D15 D5 D4 D8; 9.3 D1 D5 D13 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D15; 10.2 D2 D13; 10.3 D18; 10.4 D18 D14 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D20 D13; 11.2 D13 D16; 11.3 D16; 11.4 D5 D8 D14 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D11 D2 D1 D7 D8; 12.2 D1 D9 D5 D12; 12.3 D11 D17; 12.4 D15 D1 D2 D3 D4 D5 D6 D7 D9 D11 D20 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D19 D16; 13.2 D1 D4 D7 D14 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D20; 14.2 D2 D14 D20; 14.3 D20; 14.4 D17 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline

## Log
- 2026-09-28 · status → draft · plan
- 2026-09-28 · note · planned autonomously from the Epic's boss-reinforcements constraint, A23, design §9 D20 and D21, contract §10 and recon at 284f840 (Model, Validation, Idempotency, Hooks, TriggerBus, Empowerment, Limits; event-spawns and regions plans; Bloodcraft, SanguineArchives, XPRising, TideOfWar and BloodyBoss notes); choices inside the scope are reversible assumptions S-5 to S-20
