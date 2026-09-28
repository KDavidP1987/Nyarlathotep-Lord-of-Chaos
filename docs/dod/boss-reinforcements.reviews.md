# Reviews: boss-reinforcements

## Review 1 · 2026-09-28 · subagent · plan commit 604408e · plan 76044 B · 20 items · files 0 · e3b0c44298fc · prompt 9b9ef6bbab50
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 14.2 (and 3.4). D2 and S-20 require a boss trigger on pillar boss, while D2, Compatibility and Design › Data promise that 0.7.0 definitions load unchanged. The shipped Resources/events.default.json `example-boss` (pillar boss, VBloodKilled, Point) and `event new <id> boss` skeletons (design §6: Manual) would be disabled; TemplateTests expects example-boss.
Fix: one policy in D2, 14.2 and 3.4: keep pillar boss accepting the non-boss triggers, or accept and document the break.

F2 · blocking · 4.4 (and 2.3). A fight is global (D1: keyed by boss, several event ids, End closes it once and re-arms fired flags), yet 2.3 says a fight belongs to its instance; `event stop A`, A's hard end or fault cancel would end B's part too and re-arm B's once-flags.
Fix: stop, hardend and fault detach only that instance; the fight and others' flags end only on death, reset, gone, purge, pillar off or restart; D1/D9 cases.

F3 · advisory · 4.1/7.3. EmpowerBoss accepting maxHealth shifts Health/MaxHealth without damage: false phases (D3), a skewed burst denominator (D4), a blocked 99 % reset (S-9).
Fix: keep maxHealth out, or compute against the pre-carrier MaxHealth.

F4 · advisory · 7.3/9.3. Queued spawns and late spawn callbacks of an ended fight stay registered to it.
Fix: cancel queued spawns and despawn late callbacks; a BossEndPaths case.

F5 · advisory · 9.2/6.1. BossFilter does not exclude player-owned V Blood units (Bloodcraft V Blood familiars keep VBloodUnit); JoinFight would copy a player's team onto adds.
Fix: exclude a player EntityOwner/Follower or player team; a D8 case.

F6 · advisory · 7.1/8.2. D1's Engage has no gate, while 7.1, 8.2 and D19 claim an empty fight table when the pillar is off or no boss definition is enabled.
Fix: gate Engage; a fails-when case.

F7 · advisory · 14.4. Logic/Dependency.cs (Dependency members, DependencyPolicy rows) and the HookSet.HookFor signature (one Hook?, burst needs two) are not walked.

F8 · advisory · 4.5. D9's end paths omit `.nyar pillar boss off` and the master kill switch.

F9 · advisory · 9.2. MaxBossFights (4) is global; four held bosses block boss events server-wide until a reset.
Fix: accept and state it in 13.2.

Owner-decision flag: S-20 changed design §6's `event new <id> <pillar>` row (Manual trigger); with F1 it is not cheap to reverse.

12/15 layers · 46/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · non-breaking: pillar boss keeps accepting today's triggers and locations; only the boss triggers, AroundBoss, JoinFight and EmpowerBoss are tied to pillar boss; `event new <id> boss` keeps design §6's Manual skeleton and example-boss loads unchanged (D2, S-20, 14.2, 3.4); this also removes the owner-decision flag
- F2 · accepted · stop, hardend and fault cancel detach only their instance; the fight ends and re-arms only on death, reset, gone, purge, pillar off or restart (D1, D9, Business rules, 2.3)
- F3 · accepted · EmpowerBoss takes no maxHealth (D2, Business rules)
- F4 · accepted · queued spawns cancelled and late callbacks despawned (D9, BossEndPaths)
- F5 · accepted · player-owned V Blood units excluded (D8, Compatibility)
- F6 · accepted · Engage gated by pillar boss and a matching enabled definition (D1)
- F7 · accepted · Logic/Dependency.cs and the HookFor change walked in step 2 (Interfaces)
- F8 · accepted · pillar off and the kill switch named as end paths (D9, BossEndPaths)
- F9 · accepted · the global cap stated in 13.2

## Review 2 · 2026-09-28 · subagent · plan commit 288acfe · plan 82301 B · 20 items · files 0 · e3b0c44298fc · prompt 2d34504f97fc
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 7.1 (and 2.1). `.nyar event start <id>` starts any definition (Commands/EventCommands.cs:75); for a boss-trigger definition with no fight, an AroundBoss wave has no centre, EmpowerBoss no boss, and D6's instance end with zero fights is undefined.
Fix: refuse with a fixed line, or join every open matching fight and refuse when none; a D-item case.

F2 · advisory · 3.3. S-18's cooldown from the last firing has no Data row; in memory it resets on restart (today's cooldown reads state.json's LastStartUtc, Engine.cs:53, 183).

F3 · advisory · 6.2/7.2. Whether burst firings run inside the StatChangeSystem.ApplyStatChanges postfix or are deferred is unstated.

F4 · advisory · 4.2. "One carrier per boss" is tested only from EmpowerBoss; a faction sweep with includeVBloods must skip a boss carried by EmpowerBoss (Eligibility "carried", Empowerment.cs:49).

F5 · advisory · 6.1/14.4. Fixture ids PatchGuards/bad-5, Commands/bad-4 and GatewayOnly/bad-9 assume unbuilt plans take the ids between.

Contested assumptions: none changes an owner decision; S-8 extends D21 by analogy, consistent with it.

EARLIER: all resolved
15/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · a manual start of a boss-trigger definition starts nothing and answers a fixed line (reversible; fallback: join every open matching fight)
- F2 · accepted · the last firing is kept in state.json with the definition's start time; a restart keeps the cooldown
- F3 · accepted · the postfix only appends; burst firings start on the next fight tick
- F4 · accepted · CarrierLedgerTests Boss: a faction sweep skips a boss carried by EmpowerBoss
- F5 · accepted · step 1's pre-audit re-reads the fixture folders; a renumbering is a corrected amendment

## Review 3 · 2026-09-28 · subagent · plan commit 19bcc3a · plan 86623 B · 20 items · files 0 · e3b0c44298fc · prompt 46eac03d80e3
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 7.3/4.3. A fight joining a shared instance later has no stated wave schedule (the engine schedules wave k at instance start + k × interval, Engine.cs:231-237). Fix: a fight's wave k is due at its join + k × interval, cut off at EndsUtc; an EngineTests BossFights case.
F2 · advisory · 14.1/11.4. The Session 1 probe build's exception (fights for every V Blood) has no item proving 0.8.0 lacks it. Fix: gate or remove it in step 3; a D1 case or Session 2 line.
F3 · advisory · 9.1/13.2. Engage ignores the trigger scope; out-of-region bosses can fill the global cap. Fix: gate Engage on scope or accept it in 13.2.
F4 · advisory · 7.3. An instance end's grace Cleanup (Engine.cs:175-179) and D9's EndFightUnits could both queue the adds. Fix: boss-instance ends skip the grace Cleanup, or EndFightUnits supersedes it; a BossEndPaths case.
F5 · advisory · 4.4. WaveGate's skip order is stated but not asserted. Fix: a WavePrecedence case with several skips.
F6 · advisory · 5.3. Contract §10.4 says `boss=<V Blood name>`; D14 emits the prefab. Fix: D14 names the change; ContractDocTests checks it.
F7 · advisory · 14.4. The twin's reason mapping file is not named in step 3; `%TEMP%\dod-review-*.txt` is a Data row outside the walker. Fix: name the file; add the path or an exclusion.
F8 · advisory · 14.3/3.4. Whether state.json's tracked-unit shape gains a fight field is unstated. Fix: memory-only link, shape unchanged, or cover the extra field in the drill.

No reversible assumption overrides an owner decision.

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · amendment at `start`: a fight's wave k is due at its join + k × interval, cut off at EndsUtc (D6)
- F2 · accepted · amendment at `start`: the probe exception is removed in step 3, and a Session 2 line shows a V Blood with no matching definition opens no fight (D1, D16)
- F3 · accepted · amendment at `start`: Engage is gated on the trigger scope at the boss's position (D1)
- F4 · accepted · amendment at `start`: boss-instance ends skip the grace Cleanup; EndFightUnits supersedes it (D9)
- F5 · accepted · amendment at `start`: a WavePrecedence case asserts the skip order (D7)
- F6 · accepted · amendment at `start`: D14 changes §10.4's `boss=` to the prefab, checked by ContractDocTests
- F7 · accepted · amendment at `start`: step 3 names the twin's file; the review-prompt temp path is an exclusion under S-3
- F8 · accepted · amendment at `start`: the fight link is memory-only and state.json's unit shape is unchanged (D17)
