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

## Review 2 · 2026-10-11 · subagent · plan commit 02ebc62 · plan 71655 B · 25 items · files 0 · e3b0c44298fc · prompt dea4003b26f9
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F5
L4 · Considered · F1
L5 · Considered · F2
L6 · Considered · no finding
L7 · Considered · F5
L8 · Considered · no finding
L9 · Considered · F1
L10 · Considered · no finding
L11 · Considered · F1
L12 · Considered · F3, F5
L13 · Considered · no finding
L14 · Considered · F2, F4
L15 · Considered · no finding

**Code claims checked and found true:**
- `Limits.MaxUnitsPerWave` has a default of 20 and a ceiling of 50.
- `ScoreboardPlayers` is 200.
- `MaxLevelDelta` is 5.
- The Hunt range is 10-60.
- `HuntPlan.Targets` measures the range from the wave centre.
- `ActiveEvent.Origin` is the starting admin's position.
- An Admin location cannot start without an admin (Engine.cs:357), so an arena's centre is always defined.
- `TrackedUnit.SpawnedUtc` exists, so the instance filter can be built.
- `ScoreRow.First` exists, so the tie-break can be built.
- `SpawnTuning.For` takes each entry's modifiers when there is a waveList, which matches D25's "the action's modifiers on each entry".
- `intervalSeconds` (10-600) fits inside `afterSeconds` (10-3600), so generated waves are valid waveList waves.
- `DefinitionEditor` edits the events.json text, not a serialized model, so a generated waveList cannot be written back.
- There are 10 templates today.
- CHAR_Spider_Forestling, CHAR_Spider_Forest and CHAR_Spider_Range are all Faction_Spiders and none is a V Blood.
- The template's numbers work out: 6, 9, 12, 15 and 18 units at levels 30-42, with wave 3 at level 36.
- Wave-sets Session 2 records the A7 figures exactly as D19 quotes them: 29.9 ms on the first hunt tick, and no repeat of the 176.8 ms stall.

**Findings:**

F1 · advisory · probe 4.4 / 9.2 — D9 refuses two edits that would break an arena or winner rule (`action.scoreboard false` under a winner, `action.location aroundplayer` under an arena). It says nothing about `action.radius` or `action.behaviour.range` being widened past `arena.radius` from chat. Those edits go through `DefinitionEditor.Write`, and the reload then disables the definition with "now disabled: <reason>". The admin is told, but the behaviour differs from the two refused edits.
Fix: either refuse those two edits the same way in D9, or add one line to D9 saying they are applied and then disabled by the reload (the existing behaviour). Add a fails-when for whichever is chosen.

F2 · advisory · probe 14.4 / 5.2 — Paths walked misses files the build plan will probably touch:
- `Logic/Engine.cs`: `TickTimer` lives there (Engine.cs:519), and step 2 adds an arena phase "timed in the TickTimer like the hunt phase".
- `Services/HuntAction.cs`: D3 says the unit's target on a player outside the arena is dropped.
- `Services/UnitSetup.cs` or `Services/WaveAction.cs`: needed if the optional `MaxDistanceFromPreCombatPosition` is set at spawn.
- `tools/preflight.ps1`: needed if the timing line gains an arena field that `-TimingSpan` parses.

Fix: list them in Step 1 and Step 2 of Paths walked. If they are left out, `-DeclaredOf` will catch them at build time as a `discovered` amendment.

F3 · advisory · probe 12.4 — D4 only fails when the health entry outlives a good check. The Data table says the entry also clears at an event end or a restart, and no fails-when covers that.
Fix: add "or the health entry outlives its event's end" to D4's fails-when.

F4 · advisory · probe 14.1 — A swarm event with growth also gets the units-form `chance` line suppressed in `event info`. The reason: `EventAdmin.cs:551` and `:558` check `WaveList is not null`, and a generated list sets it.
Fix: D10's keyless-unchanged clause does not cover a growth event. Add "a growth event's base-entry chances are shown in its generated wave lines" (or accept the wave-sets D15 rendering) to D10.

F5 · advisory · probe 7.2 / 3.3 — The scoreboard is keyed by event id, not by instance (`Scoreboard._byEvent`). A stop followed by a restart inside the grace period could credit an old-instance unit's kill to the new instance's winner. The plan says "never ... credits the old instance's units" (Maximal stretch › Repeated use, citing wave-sets A3), but no D-item in this child fails on it.
Fix: if wave-sets already owns this boundary, cite that wave-sets item by number. Otherwise add "an earlier instance's kill reaches the winner" to D5's fails-when.

**Summary lines:**

missing components: none
undeclared families: action location aroundplayer, action scoreboard false, action radius, action behaviour range
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, D9 not edited): `note · deferred · Review 2 F1`, the builder chooses refuse-at-chat or apply-then-disable for `action.radius` and `action.behaviour.range` widened past arena.radius, with a fails-when for the choice, recorded as an amendment if it changes a promise
- F2 · accepted · deferred to the Log: `note · deferred · Review 2 F2`, Engine.cs, HuntAction.cs, UnitSetup.cs/WaveAction.cs and tools/preflight.ps1 are declared by amendment when `pwsh tools/preflight.ps1 -Paths -DeclaredOf swarm` finds them touched
- F3 · accepted · deferred to the Log: `note · deferred · Review 2 F3`, D4's health entry clearing at an event's end and at a restart (Data table) is checked at build in SwarmDependencyFailureTests
- F4 · accepted · deferred to the Log: `note · deferred · Review 2 F4`, a growth event's `event info` renders its generated waveList by wave-sets D15, base-entry chances shown in the wave lines
- F5 · accepted · deferred to the Log: `note · deferred · Review 2 F5`, wave-sets A3 already bounds the credit: Logic/ScoreFeed.cs ScoreRule.Counting credits a unit only when `u.SpawnedUtc >= a.Instance.StartedUtc`, ScoreboardTests `Counting_fails_when_an_earlier_instance_unit_credits` fails on it, and Scoreboard.End removes the event's rows at every end path, so a restart starts empty
- UF1 · rejected · (the reviewer wrote the names with dots, `action.location aroundplayer, action.scoreboard false, action.radius, action.behaviour.range`; the checker's name grammar allows no dot, so the line above spells them with spaces) not edited after the READY (review.md, freeze): D9 already refuses `action.scoreboard false` under a winner and `action.location aroundplayer` on an arena; `action.radius` and `action.behaviour.range` past arena.radius are refused by D1's validator, which disables the definition on reload; whether D9 refuses them at the chat command too is the builder's choice under Log note Review 2 F1, an amendment if it changes a promise

## Review 3 · 2026-10-11 · subagent · plan commit a7ddeb8 · plan 84763 B · 25 items · files 0 · e3b0c44298fc · prompt 20805a692def · scope A1
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
L12 · Considered · F3
L13 · Considered · no finding
L14 · Considered · F1, F2
L15 · Considered · no finding
F1 · advisory · probe 14.3 · Build plan step 3 still says the gate gives "(4/4)", which contradicts A1 and the amended D20. Fix: record in the step 3 post-audit or the Log that A1 / D20's "3/4, failed: repo" replaces it.
F2 · advisory · probe 14.3 · A1 does not say where the hand-run revert's evidence goes. Fix: put the four outputs and the gate's own repo-part output in docs/audits/swarm.md › Post-audit › Step 3, and cite that place on D20's pass line.
F3 · advisory · probe 12.4 · "that test alone" is read from the gate's console. Fix: quote the failing test's fully qualified name and message from both runs when recording D20.
missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · logged: `note · deferred · Review 3 F1`; D20 governs, and the step 3 post-audit says so
- F2 · accepted · applied: the hand-run outputs and the gate's repo-part output are in docs/audits/swarm.md › Post-audit › Step 3, cited on D20's pass line
- F3 · accepted · applied: both runs name ControlCaseTests.ControlCases_passes_plan_and_table_agree and its message in the audit

