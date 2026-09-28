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
