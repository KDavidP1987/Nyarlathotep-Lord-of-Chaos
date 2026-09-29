# Reviews: regions

## Review 1 · 2026-09-28 · subagent · plan commit 90fa3fb · plan 48062 B · 16 items · files 0 · e3b0c44298fc · prompt beff7037586a
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 4.5, S-5. Design §9 D21 and Epic A28 were settled by the owner: "Every trigger and action takes `scope`". The plan lets only VBloodKilled take a trigger scope; D3 disables Schedule, GameTime and Manual triggers carrying one, and 15.1 defers "players in the region" to an unnamed later child. That narrows an owner decision, so it cannot be a planner-chosen reversible assumption; the "every trigger" set misses three of the four trigger types.
Fix: put the narrowing to the owner in plan mode and record the answer as a validated assumption and in D21, or give a non-positional trigger scope a meaning (e.g. fire only when a player stands in a named region) with a D-item; either way 15.2 names the child.

F2 · blocking · 12.4. PrivacyTests Region on an empty line set passes, and RegionTests Cost on an empty index passes (0 ms); D15's fixture is written only in step 3, yet step 1 claims D15.
Fix: both fail on empty input; move D15's check to step 3 or record a placeholder count.

F3 · advisory · 5.3. `reason=out_of_region` is not added to the closed Reasons list (Logic/Outcome.cs, OutcomeCodeTests) nor to contract §5a; neither path is walked.

F4 · advisory · 14.4. Compatibility promises a human-reply capture row set that no step writes; Logic/AdminFlows.cs is not walked.

F5 · advisory · 7.3. What happens to `trigger.scope` when `trigger.type` changes is unstated (EventAdmin resets trigger keys, Logic/EventAdmin.cs:103).

F6 · advisory · 7.1. Boot order of RegionMap against the definitions' load is unstated; loading first would disable every regional event until a reload.

F7 · advisory · 13.2. The 1-10 bound is hard-coded from today's enum; tie it to RegionNames.Count.

F8 · advisory · 4.4. Whether a trigger scope and an action scope must overlap is unstated.

F9 · advisory · 14.2. The README rollback note omits the `{region}` placeholder, which 0.5.2 also disables (Validation.cs:506).

Hunted: trigger type changed while a scope is set (F5); definitions applied before RegionMap (F6); a game update adding a region (F7). Claims verified: the 0.5.x nested unknown-key fail-safe (Validation.cs:210), JsonNode edits keep unknown keys, SkipReasons order, carriers not refreshed per sweep (S-4), contract reserves `api regions` and `region=`.

13/15 layers · 47/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · owner decision in plan mode (option A): every trigger type takes a scope; VBloodKilled by the kill's region, Schedule, GameTime and Manual by "a player is in a named region" (D4, S-5 validated, design §9 D21)
- F2 · accepted · PrivacyTests Region fails on zero lines; RegionTests Cost fails on a missing or zero fixture and moves to step 3 with the fixture (D14, D15, W3.1)
- F3 · accepted · D6 names the Reasons entry, its OutcomeCodeTests row and contract §5a; step 2 walks Outcome, AdminFlows and AdminLines and OutcomeCodeTests
- F4 · accepted · Compatibility: every 0.5.2 human reply keeps its text; this child only adds lines, so the capture stays unchanged
- F5 · accepted · a `trigger.type` change clears `trigger.scope` (D9 and its test)
- F6 · accepted · RegionMap builds before EventStore applies the definitions (D7 and its test)
- F7 · accepted · the bound is RegionNames.Count (D3, Business rules 1, 13.2)
- F8 · accepted · the two scopes are independent (D3, Business rules 1)
- F9 · accepted · the README and Rollback name `{region}` (D16)

## Review 2 · 2026-09-28 · subagent · plan commit cccc91f · plan 52095 B · 16 items · files 0 · e3b0c44298fc · prompt c143fb6da365
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 4.2 / 9.1. Walkable-spawns' WalkBudget keeps a ring point without calling the game once the tick budget is spent; with the scope check inside isFree, a busy tick can place a wave unit outside the action scope (Business rules 2, Epic A28). Fix: evaluate the scope outside WalkBudget (pure Logic, cheap); SpawningTests case with the budget spent and an out-of-scope ring point.

F2 · advisory · 12.4 / D2. Test-CheckLogCheck (tools/preflight.ps1:1488) does not assert a "regions:" boot line, so D2's cmd evidence cannot fail for its stated reasons. Fix: add the assertion with a fixture, or keep D2 manual.

F3 · advisory · 6.2. A renamed WorldRegionType does not throw; ToString returns the new name, so a scope naming the old one validates but never matches. Fix: disable a definition whose scope names a region absent from the built index; correct the 6.2 sentence.

F4 · advisory · 14.4. The Manual trigger-scope refusal needs online players' positions in the admin flow; step 2 omits Services/AdminOps.cs, Commands/ApiAdminCommands.cs, the human `event start` command file and FakeStores.cs. Fix: walk them and name the reader's interface member.

F5 · advisory · 7.3. The effect of `event set … action.scope` on a running event is unstated. Fix: follow DefinitionEditor's existing live-edit rule, with an EventAdminTests case.

F6 · advisory · 4.1 / D6. "Deferred to start" can never run, since RegionMap builds before definitions and a missing index disables regional ones (D7). Fix: delete the branch.

F7 · advisory · 12.4 / D15. A 20 ms wall-clock limit is flaky and machine-bound. Fix: assert on counted box and polygon tests; keep time as a logged figure or a generous ceiling.

F8 · advisory · 2.2 / D4. The Manual refusal is a world-state condition, not a bad argument. Fix: consider `code=state reason=no_player_in_region`; record the choice in contract §5a.

Hunted: a busy tick spends the walk budget (F1); a scope narrowed while running (F5); a patch renames a region (F3).

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · amendment at `start`: the scope check runs outside WalkBudget; SpawningTests case (D6)
- F2 · accepted · amendment at `start`: Test-CheckLogCheck asserts the "regions:" boot line for a regions session, with a fixture (D2)
- F3 · accepted · amendment at `start`: a scope naming a region absent from the built index disables the definition; the 6.2 sentence corrected (D3, D7)
- F4 · accepted · amendment at `start`: step 2 walks Services/AdminOps.cs, Commands/ApiAdminCommands.cs, the event start command file and FakeStores.cs; IAdminOps gains a read of online players' positions (D4)
- F5 · accepted · amendment at `start`: a scope edit follows DefinitionEditor's live-edit rule, with an EventAdminTests case (D9)
- F6 · accepted · amendment at `start`: the deferred branch is removed from D6
- F7 · accepted · amendment at `start`: D15 asserts counted box and polygon tests; time is logged with a generous ceiling
- F8 · accepted · amendment at `start`: the Manual refusal answers `code=state reason=no_player_in_region`, recorded in contract §5a (D4)

## Review 3 · 2026-09-28 · subagent · plan commit 762f43d · plan 71736 B · 16 items · files 0 · e3b0c44298fc · prompt 071cf6346820 · scope A1,A2,A3,A4,A5,A6,A7,A8
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A1-A8 (design §9 D26); the prompt held no Steam ID.

F1 · advisory · A1 (4.2, D6): putting the scope check before WalkBudget does not by itself keep a spent budget from placing a unit outside the scope. In Logic/Spawning.cs:140, `WavePoints.Plan` returns the original ring point `p` as Unchecked whenever `spent` is set, even when `p` itself just failed the scope check. The fall-open path at :120-123 (no probe, or `walk.Failure` set) also places every ring point unchecked without calling isFree, so no scope check runs there at all.
Fix: state in D6 that an Unchecked placement (spent budget, fall-open, or no probe) whose ring point is outside the scope uses the wave centre, which is in scope by the Point and Admin checks. Add "a fall-open or no-probe wave places an out-of-scope ring point" to D6's `fails when`.

F2 · advisory · A4 (4.1, D4): the shared check now runs inside `EventEngine.Start` for every start, and Start does not know the trigger type. A VBloodKilled definition with a trigger scope would then also need an online player in its regions. A kill inside the region, by a killer standing just across the border, would be refused, which contradicts D4's VBloodKilled rule ("its trigger scope contains RegionOf(x, z)").
Fix: say the player-in-region gate applies to Manual, Schedule and GameTime starts only, keyed on `def.Trigger.Type`. Add a TriggerActivationTests case: an in-region kill with no player in the region starts the event.

F3 · advisory · A4 (14.4): "a position reader in its start controls" most naturally means a new field on `ControlState`, which lives in Logic/Precedence.cs. Step 2's Paths walked does not list Precedence.cs. ControlState is also constructed directly in ControlPrecedenceTests.cs, EngineTests.cs, EngineTests.Empower.cs, TemplateCommandTests.cs and TriggerActivationTests.Empower.cs. `-Paths -DeclaredOf` would catch the missing path at D13, but only late.
Fix: name where the reader goes (a defaulted `ControlState` parameter, or a Start argument). Add Logic/Precedence.cs to Paths walked step 2, and the test files too if the record changes.

F4 · advisory · A4 (4.1, D4): the Schedule and GameTime rule "consumed as handled, not retried in the same slot" now goes through a refusal from Start. The plan does not say that the scheduler treats a `no_player_in_region` refusal as consumed, or that it logs "event <id>: skipped, no player in <regions>" instead of the generic refused-start line.
Fix: one clause in D4 saying that the scheduler, on that reason, marks the slot handled and logs the skip line.

F5 · advisory · A7 (12.4): the Failing-cases table still says "RegionTests Cost (D15) | 10,000 points over 20 ms", which contradicts the amended D15 (counted box and polygon tests, 200 ms ceiling).
Fix: change that row to "a point costing more box tests than polygons or more polygon tests than box hits, or the run over 200 ms".

F6 · advisory · A2 (12.4): the regions assertion depends on reading the plugin's version from the log. A log without a readable Nyarlathotep version line would skip the assertion without a message. Also, of the three new fixtures none is a good 0.6.0 log, so a check that fails every 0.6.0 log would still pass -SelfTest.
Fix: -LogCheck fails when the loaded version cannot be read, and a LogCheck/good-regions fixture is added. Also add a LogCheck row to the Failing-cases table: fails on missing, zero and differ; silent on a pre-0.6.0 log and on the good fixture; an empty log already fails.

F7 · advisory · A3 (6.2, D3): D7 and A3 can both describe the same broken map. With no polygons at all, every regional definition could be disabled with "region <name> is not on the map" instead of D7's "regions unavailable", depending on which check runs first.
Fix: state that D7's message wins when the index is unavailable, and that A3 applies only to a built, non-empty index. Add an EventValidationTests or DependencyFailureTests case.

F8 · advisory · A8 (2.2): I found nothing wrong. `RefusalCode.State` exists in Logic/Outcome.cs, and the reason list is closed (`Reasons.All`), so the `OutcomeCodeTests` row and the §5a entry cover the new reason. Noted only to confirm it.

Summary: A5 and A6 are consistent with the code (EventEngine's "the instance keeps that definition until it ends"; RegionMap builds before definitions per D7). A1 and A4 are the amendments whose wording needs tightening before the build (F1 to F4). None of them leaves a gating probe unanswered or needs a decision the builder could not make alone.

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · A9: an Unchecked out-of-scope ring point uses the wave centre (D6)
- F2 · accepted · A10: the player gate applies to Manual, Schedule and GameTime only (D4)
- F3 · accepted · A11: the reader is a defaulted ControlState parameter; Precedence.cs walked in step 2 (D4)
- F4 · accepted · A12: the scheduler keeps the slot handled and logs the skip line (D4)
- F5 · accepted · A13: the Failing-cases row matches A7
- F6 · accepted · A14: an unreadable version fails; fixtures bad-regions-noversion and good-regions (D2)
- F7 · accepted · A15: D7's message wins over A3's (D7)
- F8 · accepted · a confirmation of A8; no change

## Review 4 · 2026-09-28 · subagent · plan commit f921636 · plan 75476 B · 16 items · files 0 · e3b0c44298fc · prompt 39eafbe2c07d · scope A9,A10,A11,A12,A13,A14,A15
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A9-A15 (design §9 D26); the prompt held no Steam ID. It found A9, A11, A13, A14 and A15 consistent with the code.

F1 · blocking · 4.4 · A10 keys the player gate on `def.Trigger.Type`, but `EventEngine.Start` (Engine.cs:208) accepts an admin start (`AdminOps.OpStartEvent` → `StartEvent(id, "manual", Actor.Admin, …)`) of any definition type. An admin's `.nyar event start` of a VBloodKilled definition with a trigger scope therefore meets neither the player gate nor a kill position and starts unconditionally, against Business rules 5 ("no actor can bypass the scope") and D4's Manual rule. Fix: key the gate on how the start arrives, or state an explicit exception and amend Business rules 5; add a test case.

F2 · advisory · A12 / D4's fails-when "a skipped occurrence logs the generic refused-start line": the generic line lives in Services/EventRuntime.cs:90, not in the scheduler, and Services is not compiled into Nyarlathotep.Tests, so the clause cannot fail. Fix: name EventRuntime.StartEvent and move the choice into Logic (an AdminLines/Outcome helper).

F3 · advisory · A11: "null reads nothing" does not say how the gate decides for a scoped definition with no reader. Fix: a null reader gives zero positions, so a scoped Schedule, GameTime or Manual start is refused (fails closed, as in 6.2).

F4 · advisory · A10/A4: where the player gate runs relative to EmpowerClash and the Admin-location check inside `EventEngine.Start` is unstated. Fix: name the order, e.g. after the Admin-location check and before `catalog.TryStart`.

Hunted: an admin starts a trigger-scoped VBloodKilled definition (F1); a scheduled start refused by the gate, its log line in Services (F2); a Manual start with the default null reader (F3).

15/15 layers · 49/49 probes, as written; F1 disputes 4.4 (14/15 layers · 48/49 probes if it holds)
VERDICT: REVISE

### Dispositions
- F1 · accepted · A18: the gate is keyed on the start; only the VBloodKilled route's start carries the kill position and skips it (D4)
- F2 · accepted · A19: a Logic/AdminLines.cs helper chooses the log line; EventRuntime calls it (D4)
- F3 · accepted · A20: a null reader gives zero positions and the start is refused (D4)
- F4 · accepted · A18: the gate runs after the Admin-location checks and before catalog.TryStart (D4)

## Review 5 · 2026-09-28 · subagent · plan commit 68a158e · plan 79039 B · 16 items · files 0 · e3b0c44298fc · prompt 619618781210 · scope A16,A17,A18,A19,A20
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A16-A20 (design §9 D26); the prompt held no Steam ID. It confirmed A16 against the 1.1.12 reference assembly (None, Other and ten map regions) and A17-A20 against Dependency.cs, EventEngine.Start, AdminOps and TriggerBus.

F1 · advisory · 4.4: the gating-evidence row for 4.4 filters only EmpowerEligibilityTests and SpawningTests (D5, D6); A18 moves the trigger-scope part of "no actor bypasses the scope" into EngineTests ScopeGate and TriggerActivationTests Region (D4), so removing A18's gate would not fail the 4.4 command. Fix: add both to the filter and D4 to the Item column.

F2 · advisory · 12.4: the Failing-cases row for TriggerActivationTests Region (D4) lists only the kill-position cases, not A18's admin start, A20's null reader or A19's helper mapping. Fix: extend the row.

F3 · advisory · 12.2: A19's helper keyed on the no_player_in_region reason alone would log an admin's Manual refusal as "event <id>: skipped, no player in <regions>". Fix: the helper takes the actor and uses the skip line for System starts only.

F4 · advisory · 3.1: A16 makes "Other" an unknown name, but D3's fails-when names only "None". Fix: add "Other" to D3's refused names and test case.

F5 · advisory · 12.2: A16 adds "<k> untagged" to the boot line, but D2's format does not say where, nor whether p counts every polygon read or only the indexed ones. Fix: state the exact shape, p the indexed count, and spell it in the good-regions fixture.

F6 · advisory · 4.4: a start carrying a kill position skips the gate and relies on TriggerRouter having checked the region. Fix (optional, defence in depth): in Start, check RegionOf(kill) against the trigger scope instead of skipping.

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A21: the 4.4 command runs EngineTests and TriggerActivationTests, with D4
- F2 · accepted · A22: the D4 Failing-cases row lists the A18, A20, A21 and A23 cases
- F3 · accepted · A23: the helper takes the actor; the skip line is for System starts only (D4)
- F4 · accepted · A25: "Other" is named among D3's refused names and in its test
- F5 · accepted · A24: the boot line's exact shape, p the indexed count (D2)
- F6 · accepted · A21: Start checks RegionOf(kill) against the trigger scope (D4)
