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
