# Reviews: apocalypse

## Review 1 · 2026-10-11 · subagent · plan commit 8b80ba2 · plan 84735 B · 35 items · files 0 · e3b0c44298fc · prompt 8f09b5ef8665
F1 · blocking · blocks: outcome. After a restart, a run whose goal is `Admin` loses its goal, and that includes the shipped `zombie-apocalypse` template. Probes 3.3 and 5.3 are unanswered. S-10 says the goal is "saved in the record at the start so a resume keeps it", but D11's field list and the 5.3 `Apocalypse{…}` contract carry no goal. D19 also forbids "a player position" in the record, and an Admin goal *is* the starting admin's position. So the record has no goal, a resumed march has no target, and the lost outcome (D8) cannot fire. The Admin location validates to `(0,0)` today (`Logic/Validation.cs:751`), so there is nothing in the definition to fall back on.
Fix: add `goal{x,y,z}` to D11's record, the 5.3 contract and D19's allowed fields. State in D19 or design §9 D45 that the resolved goal is a map location and not a player position. Make D12's Resume test fail when a resumed Admin-goal run has no goal or marches to (0,0).

F2 · blocking · blocks: outcome (D18 cannot pass as written). The template's 4th horde is 12 + 3×4 = 24 units. The default `MaxUnitsPerWave` is 20 (`Logic/Limits.cs:24`, README "`20` … (1-50)"). D18 claims the worst case "(30 roamers + a 24-unit horde) fits the default caps" and fails when it does not, so it fails against the shipped defaults. The 4th horde would also be clamped without anyone noticing.
Fix: either size the template to the default (for example size 8, growth 4 → 20 at horde 4), or change D18 to say horde 4 is clamped to MaxUnitsPerWave by default and the README names the cap to raise. One owner-visible choice; record it before step 3.

F3 · advisory. The Rollout text and D9 disagree about `stop`. Rollout › Shipping says "the last three" (disable, pillar off, purge) also stop the resume, which implies `stop` keeps the record. D9 says stop, purge, pillar off and fault all remove it. `.nyar event disable` of a running apocalypse is also not an end path in D9; it only takes effect at the next resume (D12).
Fix: make Rollout match D9, and state what `event disable` does to a live run.

F4 · advisory. Maximal stretch says "A player AFK at the goal cannot hold every horde". Under D6 a player standing at the goal holds every horde whose front gets within 60 m of them. With the template's radius 30, the front then never enters goal.radius, so the run can only end won or survived.
Fix: correct the sentence, or state the intended precedence (for example, a hold does not apply inside goal.radius + 60 m).

F5 · advisory. D4 and S-7 never say which hotspot a spread roamer belongs to. That decides its Hunt centre (a roamer spawned on the road may walk back towards a far hotspot), whether it counts in a hotspot's perHotspot top-up, and where D11 / D12 rebuild it ("living roamers per hotspot").
Fix: one sentence, for example "a spread roamer joins the nearest hotspot", reflected in D3's and D12's tests.

F6 · advisory. "A horde missed while down rises once at the resume" (D12, S-17) is ambiguous when two or more were missed. It is also unclear what happens if the boot-sweep drain is not finished by the 5-minute bound.
Fix: state "each missed horde rises once, in order" (or "only the latest"), and say whether the resume proceeds or is refused at the 5-minute bound.

F7 · advisory. Nothing stops a definition whose last horde rises after EndsUtc (for example 10 hordes every 1440 min in a 4 h run). Such a run can never be won.
Fix: have D1 refuse it, or have `event info` warn when firstAfterMinutes + (count−1) × everyMinutes ≥ durationSeconds.

F8 · advisory. D21 lists five faults; D22's suite has four categories (no state-write). The state write relies on Persistence's existing streak.
Fix: either add a `state-write` category, or note in D22 that it is covered by the existing Persistence suite.

F9 · advisory. Most step-2 dry runs (D2-D15) print "No test matches the given testcase filter". That is the class missing, not a failing input that was run. Step 2 records the planted-fault runs, which is acceptable under rubric 3.
Fix: make sure ControlCases.Pending holds each of these items until its planted fault is recorded.

F10 · advisory. A `.nyar purge confirm` or `pillar spawns off` issued during the resume's drain wait (between boot and the rebuild) is not named among the D9 paths.
Fix: add it to D9's EndPaths test: a purge during the wait leaves no record and rebuilds nothing.

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Gap · F1
L4 · Considered · F4, F7
L5 · Gap · F1
L6 · Considered · F8
L7 · Considered · F3, F6, F10
L8 · Considered · no finding
L9 · Considered · F4
L10 · Considered · no finding
L11 · Considered · F2
L12 · Considered · F9
L13 · Considered · no finding
L14 · Considered · F3
L15 · Considered · no finding

Missing components: none

Undeclared families: none

13/15 layers · 48/50 probes

VERDICT: REVISE

### Dispositions
- F1 · accepted · D11's record, the 5.3 contract and D19's allowed fields gain the resolved goal {x,y,z}, a map location fixed at the start (an Admin goal's starting admin position read once, never a tracked player position); D12 resumes with the goal from the record and its Resume test fails when a resumed Admin-goal run has no goal or marches to (0,0); D11's and D19's tests and S-10 follow
- F2 · accepted · option A (a reversible default, S-18): zombie-apocalypse is size 8 growth 4, so horde 4 is 20 units = MaxUnitsPerWave's default and D18's worst case (30 roamers + 20) holds against the shipped caps; the Typical use line and D29's dev-cfg cap (24) follow
- F3 · accepted · Rollout › Shipping matches D9 (stop, pillar off and purge each end the run and remove the record); D9 and Rollout say `event disable` does not end a live run (use `event stop`) and a disabled definition is not resumed after a restart (D12)
- F4 · accepted · D6 and Business rules 6: no hold applies while a horde's front is within goal.radius + 60 m of the goal, so a player at the goal cannot block a loss; D6's test and the Maximal stretch sentence follow
- F5 · accepted · D4 and Business rules 4: a spread roamer joins the hotspot nearest its spawn point (its Hunt centre, its top-up count, its place in the record and rebuild); D3's, D4's and D12's tests follow
- F6 · accepted · D12 and Business rules 8: each horde missed while down rises once, in order, spaced by the existing wave staging; at the 5-minute bound the resume proceeds after one warning (the leftover units are marked, queued and counted against the caps, so proceeding weakens no safety rule; refusing would end a days-long run for a slow drain); D12's test follows
- F7 · accepted · D1 refuses a definition whose last horde rises at or after its duration with "action.hordes: last horde rises at or after the duration"; D1's test follows
- F8 · accepted · D22 gains the `state-write` category: "dependency suite: apocalypse 5/5 (native-read, spread-read, march-read, resume-read, state-write)"
- F9 · accepted · Build plan step 2: every step-2 and step-3 item stays in ControlCases.Pending until its planted fault is recorded in its step's post-audit
- F10 · accepted · D9 and its EndPaths test: a purge or pillar off during the resume's drain wait leaves no record and rebuilds nothing

## Review 2 · 2026-10-11 · subagent · plan commit 092ef38 · plan 89030 B · 35 items · files 0 · e3b0c44298fc · prompt fa7245104125
Review 2 of plan `dod-20261011-apc1` (apocalypse), rubric 3. I checked the plan's code claims against the repository: `Logic/Limits.cs:23-24` (MaxTrackedUnits 150, ceiling 500; MaxUnitsPerWave 20, ceiling 50), `Limits.cs:35` (ScoreboardPlayers 200) and `Logic/Validation.cs:750-751` (an Admin location needs a Manual trigger and resolves to (0,0)). All of them hold.

**Probes that blocked in Review 1**
- **3.3: now answered.** The Design › Data table names every artifact and by-product with its owner, how long it is kept and how it is deleted, and marks which exist only once. D11 now stores the resolved goal `{x,y,z}` as a map location fixed at the start. D12 resumes from it, and its test fails on a missing goal or a march to (0,0).
- **5.3: now answered.** The `Apocalypse{…}` contract in Interfaces includes `goal{x,y,z}`. D19's allowed fields and PrivacyTests follow. The events.json contract is checked against the parser's OnlyKeys lists. The functions the game calls are counted: the existing DeathEventListenerSystem postfix and the scheduler coroutine, with no new Harmony patch.

**Layers**
- L1 · Considered · no finding
- L2 · Considered · no finding
- L3 · Considered · no finding
- L4 · Considered · F3
- L5 · Considered · no finding
- L6 · Considered · no finding
- L7 · Considered · F4
- L8 · Considered · no finding
- L9 · Considered · F4
- L10 · Considered · no finding
- L11 · Considered · no finding
- L12 · Considered · F1, F2
- L13 · Considered · no finding
- L14 · Considered · no finding
- L15 · Considered · no finding

**Findings**

**F1 · advisory.** D30 cannot pass as written.
- **Problem:** D30 asks for a 3 h `zombie-apocalypse` run on the dev server with no player near the hordes. Under D6, horde 1 then marches unopposed at 2 m/s. Under D8, the run ends **lost** as soon as horde 1 reaches the Admin goal, roughly 10 min plus the march time. So the run cannot last 3 h, and "every horde rise on time" cannot be observed for hordes 2-4.
- **Fix:** run D30 on a dev variant whose goal can't be reached in 3 h (a far Point goal, or marchSpeed 0.5 with a goal more than 6 km away), or have Chaos hold each front. Record the variant as an amendment before Session 3.
- **Why advisory:** the product behaves correctly; only the test rig needs changing, and that needs no decision.

**F2 · advisory.** D26's "survived" run and D28's "horde 2 marching at the restart" have the same problem with apo-test.
- **Problem:** apo-test puts the Point goal 200 m out at marchSpeed 3.0, so an unheld horde reaches it in about 70 s and the run ends lost. A 30-min "survived" run, or a restart while horde 2 is still marching, only works if Chaos holds the front within 60 m without killing it. Neither item says this.
- **Fix:** in D26 and D28, state the hold (Chaos stays within 60 m of the front, invulnerable and not attacking) or use a farther goal. Build that into `apocalypse.vrs` and `apocalypse-prep-restart.vrs`.

**F3 · advisory.** D18's worst-case arithmetic is wrong, though D18 still passes.
- **Problem:** D18 says "30 roamers + a 20-unit horde = 50, the earlier hordes dead or not". If the earlier hordes are still alive, the worst case is 30 + 8 + 12 + 16 + 20 = 86. That is still under MaxTrackedUnits 150, so the check passes.
- **Fix:** restate D18's worst case as 86 tracked (all four hordes alive plus 30 roamers), still under 150 and with no horde over 20.

**F4 · advisory.** D12 does not say how missed hordes affect the roamer top-up.
- **Problem:** each horde missed while the server was down rises once at the resume, in order. D3 also tops roamers up "at each horde rise". The plan does not say whether several catch-up rises cause several top-ups or one, or how they interact with the rebuild count D12's test checks ("rebuilds more units than recorded").
- **Fix:** one sentence in D12 (for example, "catch-up rises top up roamers once, after the rebuild"), reflected in its test.

**Earlier findings and summary lines**

EARLIER: all resolved
- F1 (goal in the record) and F2 (template sized to the default cap, S-18) are fixed in D11, D12, D19 and D18.
- F3 to F10 are applied as their dispositions state: Rollout matches D9, the hold no longer applies within goal.radius + 60 m, spread roamers join the nearest hotspot, missed hordes rise once in order with proceed-on-timeout, D1 refuses a last horde past the duration, the suite has a state-write category, ControlCases.Pending is kept until each planted fault is recorded, and D9 covers a purge during the drain wait.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · D30 runs on a dev variant of zombie-apocalypse whose goal cannot be reached in 3 h (a Point goal more than 6 km from every hotspot and marchSpeed 0.5, in the dev events.json), specified in D30 now rather than as a Session 3 amendment
- F2 · accepted · D26's survived run and D28's restart state that Chaos holds each marching front within 60 m, invulnerable and not attacking (D6), built into apocalypse.vrs and apocalypse-prep-restart.vrs
- F3 · accepted · D18's worst case is restated as 86 tracked (30 roamers + all four hordes alive, 8 + 12 + 16 + 20), under MaxTrackedUnits 150, with no horde over MaxUnitsPerWave 20
- F4 · accepted · D12: catch-up rises top up roamers once, after the rebuild, not once per rise; D12's Resume test fails when they top up more than once or before the rebuild

## Review 3 · 2026-10-11 · subagent · plan commit bec06ff · plan 89904 B · 35 items · files 0 · e3b0c44298fc · prompt fbb68f8fb733
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F3
L4 · Considered · F4
L5 · Considered · no finding
L6 · Considered · no finding
L7 · Considered · F1, F2
L8 · Considered · no finding
L9 · Considered · F1
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F5
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

Review 3 · fresh subagent · read-only · scope: the current text of docs/dod/apocalypse.md. I compared it with Reviews 1 and 2, the decisions in design §9 (D44, D45) and the code: `Logic/Limits.cs` lines 23-35 (MaxTrackedUnits 150/500, MaxUnitsPerWave 20/50, ScoreboardPlayers 200), `Logic/Validation.cs:748` (Admin location), `Logic/DataStore.cs` StateDocument, and `Core.cs:81` BootSweep. The dispositions of every Review 1 and Review 2 finding are in the text. None of them is raised again here.

**F1 · advisory** (7.2, 9.3, concurrent path). The plan does not say whether a resume waiting for the boot sweep to drain already holds the "one apocalypse at a time" slot (D14), or the definition's single instance. `Core.cs:81` runs BootSweep and then the scheduler starts. During the wait of up to 5 min, a Schedule or Interval trigger could start a fresh run of the same apocalypse or of another one. The resume would then hit "an apocalypse is already running", and D12 lists no reason for that case. The obvious answer is a builder's choice and needs no owner decision: the pending resume holds the slot from boot until it rebuilds or is refused.
Fix: D12 and D14 state that a pending resume counts as the running apocalypse (and as its instance) from boot. D12's Resume test fails when a trigger firing during the drain wait starts a second run.

**F2 · advisory** (7.3, crash path). D11 writes the record at most every 60 s, or every 30 s during a march. If the server crashes (as opposed to a clean stop) up to that long after the last write, it loses scoreboard credits, the spread window and front progress since that write. Likewise, a horde defeated after the last write is rebuilt at resume, because the record still lists it. Nothing in the plan says this loss is accepted.
Fix: one sentence in D12 or §7.3: "a crash loses at most the last write interval (≤ 60 s) of credits and progress; a horde defeated inside that interval is rebuilt". D28 can stay a clean restart.

**F3 · advisory** (3.3 / 4.5 `whole:`). D15's tally ("the run's queued units", the plan's `whole:`) lives in SpawnTracker's ledger, which is in memory. The record (D11) carries no tally counts. After a restart, the units queued before it are removed by the boot marker sweep, outside the run's ledger. So D15's test "after … a resume" either covers only the units since boot or needs counts that were never saved. The plan does not say which.
Fix: D15 states either that the tally is per boot (it restarts at the resume, and the pre-restart units are accounted by the boot sweep line) or that the record carries the counts. D15's fails-when then matches that choice.

**F4 · advisory** (4.5 siblings). D1 refuses an Admin goal "with a Schedule trigger". The rule is "an Admin goal needs a Manual trigger", so every trigger kind other than Manual (Schedule, Interval, and the event-driven kinds 11.4 names with "…") should be refused. The test plants only Schedule. That is a family checked on one member, and the plan declares no `siblings:` line for it.
Fix: D1's fails-when says "an Admin goal loads with any trigger other than Manual" and the test plants each trigger kind. Alternatively, add a `twins:` or `siblings:` line naming the trigger kinds.

**F5 · advisory** (12.4). This repeats Review 1 F9 for the record only. Most step-2 dry runs (D2-D15, D21) print "No test matches", which is the class missing, not a failing input run against a real check. Step 2's rule (kept Pending until the planted fault is recorded) is the accepted remedy under rubric 3. No action beyond what is already planned.

Hunt (three scenarios the plan does not handle in full):
1. A scheduled trigger fires during the resume's drain wait (F1; probes 7.2 and 9.3).
2. A crash just before a record write: lost credits and a rebuilt defeated horde (F2; probe 7.3).
3. The D15 tally across a restart, where the ledger's whole resets (F3; probes 4.5 and 3.3).

Test the tests: every D-item has an evidence type and a fails-when, or a manual record location. Every Build-plan step cites its items. Every layer 2-14 maps to at least one item (2: D16/D17/D14; 10: D16/D17/D35/D19; 12: D21/D22/D15; 14: D32/D33/D34). The commands behind the controls:
- 2.1 and 10.1: `preflight.ps1 -AuthSuite`.
- 3.3: ApocalypseStateTests Record and PrivacyTests Apocalypse.
- 4.4: ApocalypseTests Outcomes and EndPaths.
- 6.2: ApocalypseDependencyFailureTests and `-DependencySuite apocalypse`.
- 10.3: preflight secrets and the privacy grep.
- 14.3: `rollback-drill.ps1 -SelfTest` and `rollback-gate.ps1 -Plan apocalypse`.
- 14.4: `-Paths -DeclaredOf apocalypse`.
- 12.4: ControlCaseTests.

No finding carries `blocks:`. Each fix above is wording or work that the builder can record as an amendment.

missing components: none
undeclared families: Manual, Schedule, Interval, event triggers
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F1`, a pending resume holds the one-apocalypse slot (D14) and its definition's single instance from boot until it rebuilds or is refused, so a Schedule or Interval trigger firing during the drain wait starts no second run; D12's Resume test fails when one does; recorded as an amendment if it changes a promise
- F2 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F2`, docs/features/APOCALYPSE.md (§7.3) states that a crash loses at most the last record-write interval (60 s, 30 s during a march) of scoreboard credits, spread window and front progress, and that a horde defeated inside it is rebuilt at the resume; D28 stays a clean restart
- F3 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F3`, the builder settles whether D15's tally is per boot (it restarts at the resume and the pre-restart units are accounted by the boot sweep line) or the record (D11) carries the counts, and D15's fails-when matches the choice; recorded as an amendment if it changes a promise
- F4 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F4`, D1's test plants an Admin goal with each trigger kind other than Manual (Schedule, Interval and each event-driven kind), each refused, so the rule "an Admin goal needs a Manual trigger" is checked across the family (UF1)
- F5 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F5`, no action beyond the plan: Build plan step 2 keeps every step-2 and step-3 item in ControlCases.Pending until its planted fault is recorded in its step's post-audit (Review 1 F9)
- UF1 · rejected · not edited after the READY (review.md, freeze): D1's rule is "an Admin goal needs a Manual trigger", so every other trigger kind is refused by the same check; the builder plants each kind (Schedule, Interval and each event-driven kind) in D1's test under Log note Review 3 F4, an amendment if it changes a promise

## Review 4 · 2026-10-13 · subagent · plan commit a2771a8 · plan 132155 B · 38 items · files 0 · e3b0c44298fc · prompt d82a0ae8e6bf
Scope: the full re-review that A1-A6 owe (51 % of items touched after `start`).

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F1
L4 · Considered · F2, F3
L5 · Considered · F1
L6 · Considered · F5
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · F7
L12 · Considered · F5
L13 · Considered · F6
L14 · Considered · F4
L15 · Considered · no finding

F1 · blocking · blocks: outcome · probe 5.3 / 3.3 · D36 says a living boss must be rebuilt at a resume (its test fails when "a resume … leaves a boss out"). The D11 record and the Interfaces 5.3 contract `hordes[]{number,frontX,frontZ,living}` hold only one living count per horde, and D19 fixes the record at "exactly `Apocalypse` with its listed fields". So after a restart the rebuild cannot tell a horde of 5 regulars plus a living boss from 6 regulars with the boss dead. Fix: D11, 5.3 and D19's field list give each living horde a living-boss count, and D12 rebuilds from it.
F2 · advisory · probe 4.1 · Business rule 3 still says roamers "hunt within huntRange of their hotspot", and S-7 says "'roaming' is hunting around the hotspot". Both contradict the amended D3 and design §9 D49. Fix: rewrite Business rule 3 to D3's map-wide rule and mark S-7 superseded by A2 / D49.
F3 · advisory · probe 4.1 · D3 hands over to Hunt "within huntRange (20-60)", but hunting-grounds' mover lands units 50-60 m from the target and seeds aggro within 100 m (SeedReach = WatchRange), so a huntRange under 50 would leave roamers waiting. "Nearest eligible player" also does not say nearest to what. Fix: D3 names the mover's seed reach (100 m) and measures from the roamers' mean position; give apo-test (D25) an explicit huntRange.
F4 · advisory · probe 14.4 · Paths walked does not list Logic/FactionGroups.cs (D38), tools/vrclient/scenarios/faction-mix.vrs and docs/GAME_ASSETS.md (D37), or Logic/HuntingGrounds.cs and its service for a no-region mover. Fix: add them under Steps 1-3.
F5 · advisory · probe 6.2 · D21 and D22's five faults cover march reads but not a failing roamer move (A2). Fix: a throwing roam move leaves the roamers where they stand, logs once per streak and shows a health entry, in the march-read category or a sixth.
F6 · advisory · probe 13.1 · The hot-path list predates A2 and leaves out the roam check for up to roamers.max map-wide roamers; D29 runs with Chaos near the hordes. Fix: add the roam check to 13.1 and make D29's run include roamers moving towards Chaos from afar.
F7 · advisory · probe 11.2 · revenge D31 gives SpawnWaves an `action.bosses` chat field; D16 does not list it for Apocalypse. Fix: add `action.bosses` to D16, or name it events.json-only.

missing components: none
undeclared families: undead mix, spider mix, bandit mix, militia mix
15/15 layers · 50/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · fixed by A7 (discovered, 5.3): D11 records each living horde's living-boss count, the 5.3 contract gains `livingBosses`, D12 rebuilds that many bosses, D19's field list and D36's test name it.
- F2 · accepted · fixed: Business rule 3 states D3's map-wide rule; S-7 marked superseded by A2 (design §9 D49).
- F3 · accepted · fixed under A2: D3 measures from the roamers' mean position and hands over at the mover's seed reach (100 m, hunting-grounds A4); D25's apo-test gains huntRange 40.
- F4 · accepted · fixed: Paths walked step 1 adds faction-mix.vrs and docs/GAME_ASSETS.md; step 2 adds Logic/HuntingGrounds.cs and Logic/FactionGroups.cs; step 3 adds Services/WaveAction.cs (the mover's service) and Logic/FactionGroups.cs.
- F5 · accepted · fixed by A8 (discovered, 6.2): the roam move fault is in D21 under march-read, so D22's suite keeps five categories.
- F6 · accepted · fixed by A9 (discovered, 13.1): the roam check is a 13.1 hot path; D29's run keeps Chaos 300 m or more from the hotspots for its first windows.
- F7 · accepted · fixed by A10 (discovered, 11.2): D16 takes `action.bosses` with revenge D31's form and reasons.
- UF1 · accepted · D37

## Review 5 · 2026-10-13 · subagent · plan commit 71342dd · plan 104487 B · 38 items · files 0 · e3b0c44298fc · prompt 2225c0b3bc84
Scope: round 2 of the full re-review owed by A1-A10.

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding (Review 4's 3.3 block is answered: D11 records each living horde's `livingBosses`, D12 rebuilds from it)
L4 · Considered · F2
L5 · Considered · F1 (Review 4's 5.3 block is answered for state.json: `hordes[]{number,frontX,frontZ,living,livingBosses}`, D19 names it, D36 and D12's tests fail when a boss is rebuilt wrongly)
L6 · Considered · no finding
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

F1 · advisory · probe 5.3 · The events.json contract in Interfaces › Contracts (5.3) lists the action's keys without `bosses`, and Build plan step 2's Model.cs list leaves out `Bosses`, although D1, D16 and D36 require it; D1's test catches a missing parser rule, so no outcome fails. Fix: add `bosses[]{prefab,count,modifiers}` to the 5.3 contract and `Bosses` to step 2's Model.cs list.
F2 · advisory · probe 4.1 · Since A2, D3 hands roamers over at the mover's seed reach (100 m), and `roamers.huntRange` (20-60) is still a required D1 key with no item saying what it now controls. Fix: one clause saying huntRange is the Hunt behaviour's range after aggro is seeded (event-spawns' Hunt), or drop the key.

EARLIER: all resolved
missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 5 F1`, step 2 adds `Bosses` to ApocalypseAction and the 5.3 contract row in docs/features/APOCALYPSE.md names `bosses[]{prefab,count,modifiers}`
- F2 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 5 F2`, huntRange stays the Hunt behaviour's range once aggro is seeded (event-spawns' Hunt), stated in docs/features/APOCALYPSE.md

## Review 6 · 2026-10-13 · subagent · plan uncommitted · plan 105457 B · 38 items · files 0 · e3b0c44298fc · prompt 1bfa8aafa93a · scope A7,A8,A9,A10
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F4
L5 · Considered · F1
L6 · Considered · F2
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · F4
L12 · Considered · F2
L13 · Considered · F3
L14 · Considered · no finding
L15 · Considered · no finding

F1 · blocking · blocks: outcome · probe 5.3 (A7): one `livingBosses` count per horde cannot say which boss to rebuild when `bosses` holds up to 3 entries with their own prefab, count and modifiers (bosses A×1 and B×1, A dead: the record says 1). Fix: one living count per `bosses` entry index, the rebuild spawns each entry's prefab with its modifiers; carry it into 5.3, D11, D19 and a mixed-entry case in D12/D36.
F2 · advisory · D21/D22 (A8): the roam-move fault sits under march-read, but nothing says the march-read control fails when only the roam-move guard is removed. Fix: plant a fault in the roam-move guard alone in step 3's post-audit.
F3 · advisory · D29 (A9): nothing shows roamers jumped inside the measured windows, and the cmd reads "<the Session 2 log copy>" while D29 says "in its own run". Fix: the session record shows a roam line inside the span; name D29's own log copy.
F4 · advisory · D16 (A10): A10's fails-when includes a refused valid `action.bosses`, D16's list does not; D1 allows 0-3 entries while revenge D28 refuses an empty list. Fix: add the case to D16; align D1 to "absent, or 1-3 entries".

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · fixed by A11 (discovered, 5.3): D11 records the living bosses per `bosses` entry, the 5.3 contract reads `livingBosses[]`, D12 rebuilds each entry's prefab with its modifiers and fails on two boss prefabs with one dead, D19 and D36 follow.
- F2 · accepted · Log `note · method · Review 6 F2`: step 3's post-audit plants the roam-move guard fault alone.
- F3 · accepted · fixed by A11: D29's cmd names the log copy of its own run (Session 2) and its record needs a roam line inside the span.
- F4 · accepted · fixed by A11: D16 fails when a valid `action.bosses` is refused; D1's `bosses` is absent or 1-3 entries.

## Review 7 · 2026-10-13 · subagent · plan commit 143af72 · plan 106769 B · 38 items · files 0 · e3b0c44298fc · prompt 215d293b0a67 · scope A7,A8,A9,A10,A11
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F2
L5 · Considered · F1, F2
L6 · Considered · no finding
L7 · Considered · F2
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

Probe 5.3 (Review 6's blocker) is answered: `hordes[]{…,living,livingBosses[]}` keeps one living count per `bosses` entry by index; D11 and D12 rebuild each entry's prefab with its modifiers; D12 and D36 name "two boss prefabs, one dead"; D19 lists the field; D1's `bosses` is absent or 1-3 entries (revenge D28); D16 fails when a valid `action.bosses` is refused; D29 names its own log copy and a roam line inside the span.

F1 · advisory · The 5.3 events.json line lists the action keys without `bosses`, and step 2's Model.cs field list leaves out `Bosses`; D1's test still catches a parser that rejects the key. Fix: add both (Review 5 F1 already deferred this to step 2).
F2 · advisory · The record keeps living bosses by entry index and the resume reads each prefab from the current definition, so an admin who edits `bosses` while the run is live can have the restart rebuild the wrong boss or index past the list; D21's resume path keeps the boot alive. Fix: count a change to the `bosses` entries (number or prefabs) as "changed" in D12 and Business rule 10, or store each entry's prefab in the record beside its count.

missing components: none
undeclared families: none
EARLIER: all resolved
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no D-item edited): already `note · deferred · Review 5 F1`
- F2 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 7 F2`, step 2 stores each living `bosses` entry's prefab beside its count, so the rebuild spawns the recorded prefab, and an entry whose prefab is gone from the definition counts as "changed" under D12's refusal; recorded as an amendment if it changes a promise

## Review 8 · 2026-10-13 · subagent · plan commit 1e02b3d · plan 107358 B · 38 items · files 0 · e3b0c44298fc · prompt 4b6a8a3d396c · scope A1,A2,A3,A4,A5,A6
Scope: the same-day scoped pass over A1-A6, which Review 5 (unscoped, READY) covered in full.

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F2, F3
L5 · Considered · no finding
L6 · Considered · F2
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · F1
L15 · Considered · no finding

F1 · advisory · 14.3 (A1): Rollout › Rollback › On a server still says "Nothing is written that 0.11.0 cannot read past"; the target is now 0.12.0. Fix: read 0.12.0.
F2 · advisory · 6.1 / 4.5 (A5, A6): D37 names only zombie-apocalypse's undead units; undead-rising and graveyard-waves (CHAR_Undead_ArmoredSkeletonCrossbow_Farbane, CHAR_Undead_Guardian), bandit-revenge (CHAR_Bandit_Deadeye) and spider-hunting-grounds (CHAR_Spider_Forestling) spawn units too; D37's opening rule covers them. Fix: name every shipped template per mix so faction-mix.vrs spawns those units.
F3 · advisory · 4.5 (A6): D38's "an Apocalypse template's group holds its `faction`" needs each group to carry its Faction_* name(s). Fix: say so in D38.
F4 · advisory · 4.1 (A4): D18 does not give the boss's count; the worst case 83 and horde 4 = 20 assume count 1. Fix: state `count: 1` in D18.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 8 F1`, step 5 reads 0.12.0 in Rollout's sentence when the release surfaces move
- F2 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 8 F2`, faction-mix.vrs already spawns CHAR_Undead_ArmoredSkeletonCrossbow_Farbane, CHAR_Undead_Guardian, CHAR_Bandit_Deadeye and CHAR_Spider_Forestling, and docs/GAME_ASSETS.md › Faction compatibility names every template per mix
- F3 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 8 F3`, Logic/FactionGroups.cs gives each group its Faction_* names beside its prefixes
- F4 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 8 F4`, zombie-apocalypse's boss entry has count 1 (horde 4 = 19 + 1 = 20)

## Review 9 · 2026-10-14 · subagent · plan commit 7a52dee · plan 121767 B · 38 items · files 0 · e3b0c44298fc · prompt 71084487529a · scope A13
Scope: A13 (D32's refusal text and the drill's newer action types).

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · no finding
L5 · Considered · no finding
L6 · Considered · no finding
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F2
L13 · Considered · no finding
L14 · Considered · F1, F3
L15 · Considered · no finding

F1 · blocking · blocks: outcome · 14.3: A13 says 0.12.0 refuses an Apocalypse definition with "unknown action type Apocalypse", but at v0.12.0 ParseEvent checks durationSeconds (30-7200, Validation.cs line 183) before ParseAction (line 186), so the shipped zombie-apocalypse (14400 s) and any run of hours is disabled with "durationSeconds must be 30-7200"; the drill as D32 states it counts that as another reason and fails, and Rollout › On a server ("disabled with the action-type reason") is wrong for the template. Fix: D32 and Rollout name both reasons; the drill counts the duration reason as a newer limit (N refuses a non-apocalypse over 7200 s with the same text, so such a definition is never an extra); pair-apocalypse built from a definition over 7200 s, or a pair per reason; pair-otheraction stays the failing pair.
F2 · advisory · 12.4 (outside A13's scope): the real drill seeds events.json from Resources/events.default.json (no Apocalypse) plus drill-mark and drill-interval, so the N-1 boot never sees an Apocalypse definition or record; only the selftest fixtures show it. Fix: add a disabled drill-apocalypse and a record for N >= 0.13.0, or say in D32 that the selftest fixtures alone cover the apocalypse.
F3 · advisory · 14.3 (frozen text): Rollout › On a server still says "Nothing is written that 0.11.0 cannot read past"; the previous release is now 0.12.0 (Review 8 F1 deferred it to step 5). Fix: read 0.12.0.

missing components: none
undeclared families: unknown action type Apocalypse, durationSeconds must be 30-7200
15/15 layers · 50/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · fixed under A13 (still unapproved, revised before Review 10): D32 and Rollout › On a server name both 0.12.0 reasons ("durationSeconds must be 30-7200" over 7200 s, else "unknown action type Apocalypse"); the drill counts the duration reason among newer limits; the selftest gains pair-apocalypse (a 14400 s definition, the duration reason), pair-apocalypse-short (600 s, the action-type reason) and pair-otheraction (fail)
- F2 · accepted · fixed in D32's text: the selftest fixtures carry the apocalypse rollback (definition and record); the live drill's seed has no Apocalypse
- F3 · accepted · fixed now (step 5 preparation): Rollout reads 0.12.0
- UF1 · accepted · D32 (both 0.12.0 reasons, each with its own selftest pair: pair-apocalypse, pair-apocalypse-short)

## Review 10 · 2026-10-14 · subagent · plan commit e265651 · plan 122525 B · 38 items · files 0 · e3b0c44298fc · prompt 96364a864e02 · scope A13
Scope: A13 after Review 9 (both 0.12.0 refusals, the drill's named action types and duration limit).

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · no finding
L5 · Considered · no finding
L6 · Considered · no finding
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · F1, F2
L15 · Considered · no finding

F1 · advisory · 14.4: Rollout › Paths walked › Step 5 lists tools/rollback-drill-fixtures/{pair-apocalypse,pair-otheraction}/**, but A13 and Build plan step 5 add pair-apocalypse-short; the path manifest check would miss that folder. Fix: name it in Paths walked in the same edit that adds the fixture.
F2 · advisory · 14.3: the duration reason counts as a newer limit by its text alone; safe because N refuses a non-Apocalypse over 7200 s too, unsafe if a later release raised the limit for another action type. Fix (optional): a comment in rollback-drill.ps1 or a note in the D32 ControlCases row.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no plan text edited): `note · deferred · Review 10 F1`, step 5 names pair-apocalypse-short in Paths walked when it adds the fixture
- F2 · accepted · deferred to the Log: `note · deferred · Review 10 F2`, rollback-drill.ps1 says beside the duration reason that it is accepted only because N raises the limit for Apocalypse alone

## Review 11 · 2026-10-16 · subagent · plan uncommitted · plan 128082 B · 38 items · files 0 · e3b0c44298fc · prompt c4d4227e6903 · scope A14
Scope: A14 (design §9 D60: D38 lets a SpawnWaves template mix allied groups across serial waves).

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F2, F4
L5 · Considered · no finding
L6 · Considered · F1
L7 · Considered · F3
L8 · Considered · no finding
L9 · Considered · F4
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F5
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

F1 · advisory · 6.1: A14 amends D38 but not D37; D37's mixes list names only the undead, spider, bandit and militia (Light, Crossbow) mixes and Build plan step 1 is done, while the new templates spawn units in no FactionGroups group (other militia units, ChurchOfLight_*, Gloomrot_*, Cursed_*, Legion_* with CHAR_Vampire_BloodKnight_VBlood); D37's general clause still covers them. Fix: name the six templates' mixes in D37, name the step that runs the mix for them, and keep D38 open until those runs land in docs/GAME_ASSETS.md › Faction compatibility.
F2 · advisory · 4.5: A14 records `layer: —` although it changes a business rule (what one allied group means for D38). Fix: record it as 4.5 (or 4.4).
F3 · advisory · 7.2: D38 holds per run and per template only; a realm-gauntlet restarted inside its own grace window, or two single-group templates run at one spot, can still put hostile groups together. Fix: one sentence saying D38 is per run and per template; cross-event and cross-run mixing is the admin's choice.
F4 · advisory · 9.2: D38 checks only the shipped text; `.nyar event set realm-gauntlet action.waveList.3.afterSeconds 60` brings overlapping hostile waves back with no warning. Fix: accept it as the admin's choice, or warn on event set or event info.
F5 · advisory · 12.4: the new theory Factions_fails_when_a_gauntlet_lets_two_groups_meet has no row in Nyarlathotep.Tests/ControlCases.cs, so ControlCaseTests will fail; record a planted-fault run (remove the `AfterSeconds is not null` clause) in the post-audit.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no plan text edited): `note · deferred · Review 11 F1`, tools/vrclient/scenarios/faction-mix-waves.vrs runs the five new groups' mixes before any of them ships, and docs/GAME_ASSETS.md › Faction compatibility records the result
- F2 · accepted · deferred to the Log: `note · deferred · Review 11 F2`, the rule change sits on probe 4.5
- F3 · accepted · deferred to the Log: `note · deferred · Review 11 F3`, docs/features/EVENT_LIBRARY.md says the rule holds per run and per template
- F4 · accepted · deferred to the Log: `note · deferred · Review 11 F4`, an admin's afterSeconds on a mixed template is the admin's choice, said in the same doc
- F5 · accepted · fixed in code: ControlCases.cs gains the D38 rows; the planted fault is recorded in the post-audit
