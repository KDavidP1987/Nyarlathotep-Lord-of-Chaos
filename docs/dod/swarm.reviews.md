# Reviews: swarm

## Review 1 · 2026-10-11 · subagent · plan commit 8b80ba2 · plan 69489 B · 25 items · files 0 · e3b0c44298fc · prompt 33719adffad5
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F3
L4 · Considered · F5, F6
L5 · Considered · no finding
L6 · Considered · F4
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · F8
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Gap · F1, F2
L13 · Considered · F7
L14 · Considered · F2
L15 · Considered · no finding

What I checked in the code (HEAD 8b80ba2):
- `Limits.MaxUnitsPerWave` is (20, 1, 50), so the 50-unit ceiling and the template's 20-unit check are right.
- `MaxLevelDelta` is 5, `MinHuntRange`/`MaxHuntRange` are 10/60, `HuntPlan.IntervalSeconds` is 5, and `Limits.ScoreboardPlayers` is 200.
- `ScoreRow.First` exists, so the "earlier first credit" tie-break has data to use.
- `TrackedUnit.SpawnedUtc` exists, so the instance filter by start time can be built.
- `HuntPlan.Targets` measures range from the wave centre. That backs the claim that Hunt targets only players inside the arena.
- `Engine.Start` refuses an Admin location that has no origin, so an Admin-location arena always has a centre.
- `EventAdmin.Apply` edits the raw JSON and decides the form by whether `waveList` is present in the JSON. A growth event's generated waves therefore cannot be written back to events.json or block edits to its base wave.
- 0.9.0's `OnlyKeys` gives the reason "unknown field action.<key>", which is what D20 expects.
- The template library has 10 entries today, so 11 after this child is right.

F1 · advisory · 12.4: the dry runs for D1–D12, D24 and D25 ran existing test classes, or got "No test matches". None of them ran a failing input against the new check, so strictly 12.4 is a Gap. The plan moves this to step 1 ("planted fault run once… logged `note · planted`"), so the product does not fail at anything.
Fix: keep the step-1 planted-fault notes as the gate. Before `dod approve`, write one sentence in Coverage 12.4 saying the failing cases of new test classes run in step 1.

F2 · advisory · 12.4/14.4: the Log's D20 dry-run note says "this child's step 3 to 22", but D20 and the S-12 note say 23/23 (17 → 20 from wave-sets, then +3 pairs).
Fix: correct the D20 dry-run note to 23 so the expected output has one source.

F3 · advisory · 3.1/7.1: D3 places the returned unit "within action.radius of the centre" on x and z, but does not say what height it lands at. A Point saved without a height spawns at height 0 (Model.cs `Location` doc). A return to that height could put a unit under the terrain.
Fix: state that a returned unit keeps its own height or takes the spawn path's walkable height (builder's choice). Add one D3 fails-when for a Point without a height.

F4 · advisory · 6.2: a returned unit that keeps its aggro on a player outside the arena walks straight back out and is returned every 5 s. D15's "within 35 m from 10 s after the move" catches this only if the unit cannot cover about 27 m in 5 s.
Fix: name whether a return keeps or drops the unit's combat target (method is the builder's). Add to D18 that the owner judges the yo-yo specifically.

F5 · advisory · 4.5: D1 allows `arena` on the units form and the waveList form, and D16 runs it on a generated list. The `siblings:` line names only the location types, and D3's test does not say it covers each form.
Fix: add `siblings: units form, waveList form, growth form` for containment, and a D3 fails-when for each form, or `twins: none — <reason>`.

F6 · advisory · 4.1: D25 lets a base levelDelta grow past `MaxLevelDelta` (5), for example a delta of 5 plus 4 × 5. A hand-written waveList cannot express that, so D25's "differs from the hand-written equivalent" comparison is undefined for such waves.
Fix: limit D25's equivalence fixture to absolute levels, or to deltas whose generated value stays within ±5. Assert the larger deltas directly through LevelArg's 1–120 clamp.

F7 · advisory · 13.1: the one measurement quoted comes from wave-sets Session 1, not from a recon spike of this child. Wave-sets A7's 8.524 ms hunt-phase window is still undiagnosed, so D19 can fail for a cause outside this child.
Fix: say in D19 how a window carrying an A7-style hunt stall is treated, or that D19 waits for A7's fix.

F8 · advisory · 9.1: "Five arena events at once stay under MaxConcurrentEvents (default 3)" contradicts itself: five cannot run at once under a cap of 3.
Fix: reword it to "a fourth arena event is refused by MaxConcurrentEvents (default 3), as today".

F9 · advisory · 10.1: D9's dry run says "the -AuthSuite half was not run on the draft".
Fix: run `pwsh tools/preflight.ps1 -AuthSuite` once and record its output as a dry-run note.

missing components: none
undeclared families: units form, waveList form, growth form
14/15 layers · 49/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · Coverage 12.4 adds one sentence: the failing cases of the new test classes run in step 1 (SwarmDependencyFailureTests and TemplateLibraryTests Swarm in step 2, where they are written), each planted fault logged `note · planted`
- F2 · accepted · the Log is append-only, so a new D20 dry-run note (2026-10-11, "drill selftest: 20/20" today) corrects the earlier note's "to 22": this child's step 3 brings it to 23/23, as D20 says
- F3 · accepted · D3: a returned unit lands at the spawn path's walkable height for its spot, never a Point's saved height (0 when saved without one); D3's fails-when gains a Point without a height
- F4 · accepted · D3: a return drops the unit's seeded or combat target on a player outside the arena (the method is the builder's), and its fails-when gains the kept target; D18 names the yo-yo for the owner's look check
- F5 · accepted · D3's statement and fails-when cover the units form, the waveList form and the growth form, and Business rules 7 defines "every arena event" as those three; the `siblings:` line stays Point location, Admin location because the checker allows one sibling set per plan (see UF1)
- F6 · accepted · D25: the equivalence fixture uses absolute levels and levelDeltas whose generated values stay within ±5 (MaxLevelDelta); a levelDelta grown past ±5 is asserted directly through LevelArg's 1-120 clamp, a new fails-when
- F7 · accepted · D19: wave-sets Session 2 (2026-10-11) passed its D23 span with A7's breakdown in place (only the first hunt tick, 29.9 ms, logged a breakdown; the 176.8 ms stall did not recur), and a window carrying an A7-style first-tick warm-up is read through its `hunt parts:` line
- F8 · accepted · Maximal stretch reworded: "A fourth arena event is refused by MaxConcurrentEvents (default 3), as today."
- F9 · accepted · `pwsh tools/preflight.ps1 -AuthSuite` run from the repo root → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)", recorded as a D9 dry-run note in the Log
- UF1 · accepted · D3 covers the family (units form, waveList form, growth form): its statement runs the check the same for each form and its fails-when names a missed stray of each; Business rules 7 lists them as "every arena event"
