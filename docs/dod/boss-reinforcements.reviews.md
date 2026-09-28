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
