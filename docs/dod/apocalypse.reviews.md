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
