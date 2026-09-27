# Audit — event-library

Build plan steps 1–6 of docs/dod/event-library.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.4.0 = ae3821b.

## Pre-audit
### Step 1 · 2026-09-26 · e84c8a3
- pre-child: faction-empowerment closed (9733d05, `dod close`, 30/30) and the tag v0.4.0 (ae3821b) pushed with its GitHub pre-release, so S-2 holds; the feedback note e84c8a3 is the last commit; rollback base is v0.4.0
- git status: clean at e84c8a3; this entry, the plan's start line, A5 and A6, the feature doc and the childDocs mapping are committed with step 1
- compile: 0 errors, 0 warnings
- tests: 871 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: event-library 0/34 verified (just started); review codex (Review 9 READY), not pending
- feature doc read: docs/features/EVENT_LIBRARY.md, created by this step (Status: in build); plan D1–D34, Business rules 1–9, Interfaces, Design › Data, States, Permissions, UX, Failure & observability, Build plan, Rollout; Logic/DefinitionEditor.cs, DataStore.cs, IFileStore.cs, EventAdmin.cs, CommandArgs.cs, Precedence.cs, ActionGateway.cs, Idempotency.cs, Paging.cs, EventCatalog.cs, Model.cs, Validation.cs, Engine.cs, Schedule.cs; Services/EventStore.cs and TriggerBus.cs; Commands/EventCommands.cs; the tests FakeStores.cs, TestSupport.cs, ConfigChangedTests.cs, ContractDocTests.cs, AuthorizationTests.cs and ControlPrecedenceTests.cs
- server: not running; step 1 is pure logic with no in-game test
- found on the way, recorded before any code:
  - A5 (discovered, ~D19, layer 6.2): the day and night read lives in Services/TriggerBus.cs, so D19's phase-source case gets a Logic seam, PhaseSampler in Logic/Schedule.cs.
  - A6 (discovered, ~D16, layer 4.4): the validator gives every definition it disables the pillar spawns, so the readiness of an invalid empowerment definition would follow the wrong switch; the validator keeps the parsed pillar.

### Step 2 · 2026-09-26 · 2b083dc
- git status: clean at 2b083dc (step 1)
- compile: 0 errors, 0 warnings
- tests: 1309 passed
- preflight: PREFLIGHT OK; -Paths "822 walked, all in manifest"
- dod status: event-library 0/34 checked (step 1's items wait for their Services, Commands, preflight or in-game halves); review codex, not pending (A9-A11 are not gating)
- feature doc read: docs/features/EVENT_LIBRARY.md (Status: step 1 done); plan D3, D17, D18, D20, D32, D33, D34 and step 2; Plugin.cs, Services/EventStore.cs, EventRuntime.cs, HealthMonitor.cs, Gateway.cs, Commands/EventCommands.cs, RootCommands.cs, Config/Settings.cs; tools/preflight.ps1 and preflight-checks.json
- server: not running; step 2 has no in-game test (Session 1 is step 3)

### Step 3 · 2026-09-26 · 31fbe53
- git status: clean at 31fbe53 (step 2 and the feature doc's Status)
- compile: 0 errors, 0 warnings
- tests: 1313 passed
- preflight: PREFLIGHT OK; -Paths PREFLIGHT OK
- dod status: event-library 0/34 checked; review codex, not pending (A12 is not gating)
- feature doc read: docs/features/EVENT_LIBRARY.md (Status: steps 1 and 2 done); plan D3, D24, D29, D30, Design › Data and step 3; tools/rollback-gate.ps1, tools/data-inventory.json, tools/paths-manifest.txt, tools/preflight.ps1 (Test-CheckDataInventory, Get-PathsListings, Test-CheckPaths), tools/ingame/session-events.py
- server: baseline boot of the deployed 0.4.0 DLL on nyardev before Session 1: -LogCheck "0 unhandled, 7 nyar lines, 0 orphan errors, 0 unity errors"; warnings only Il2CppInterop Class::Init, Beelzebub's own and ours "event example-empowerment: pillar empowerment takes an Empower action" (the leftover dev events.json, replaced by el1); stopped before the step 2 DLL was deployed

## Post-audit
### Step 1 · 2026-09-26 · e84c8a3 + working tree (committed as step 1)
- compile / preflight: 0 errors, 0 warnings; 1309 tests passed (871 before the step); PREFLIGHT OK; dod --check 0 problems (6 warnings, all old review-round notes)
- mutation checks: 28 planted defects in a scratch copy, one per control and per post-audit fix (D1, D4, D5, D6/D27, D8 ×3, D9 ×2, D10 ×2, D11, D12 ×2, D13, D14 ×3, D16, D17, D19 ×4, D20 ×2, D32, and the null lastStart), each fails at least one named test; D5 "enabled false" and D20 "event copy max 3" first survived and got TemplateUse_passes_enabled_template_copied_disabled and two surplus-argument CommandForms cases
- /code-review: three findings, all fixed — `pillar <name> off` on a pillar a cfg edit already turned off left its events running (A9, discovered, ~D14; PillarCommand_passes_off_after_hand_edit_ends_running); `event set <id> location here` passed validation and then always failed (Commands/EventCommands.cs now resolves the position through LocationArg.FromContext; defect); a delete with state.json read-only dropped the cooldown row from memory only and said "cleared on the next save" (A10, discovered, ~D8; StateWrite_fails_when_state_read_only)
- Codex verdict: READY (round 4) — round 1 REVISE: the stale-file check ran after the edit plan, so a hand edit could be interpreted instead of refused (defect; EventsFile.Writable before the plan, Equivalence_fails_when_stale_file_alters_the_plan; foundation's A_refused_edit_pushes_nothing now expects the stale refusal for a file broken since the load), and D2 had no trigger-type or times case (added); round 2 REVISE: a state.json with "lastStart": null would throw in the delete confirm and the engine (defect; StateDocument.TryParse normalises it, StateWrite_empty_null_cooldown_rows); round 3 REVISE: D5's plan text read "as <newId>", which chat drops as a tag (A11, discovered: the plan corrected to "as new-id", the test asserts no "<"); round 4 READY, no findings
- privacy grep (7656119, kdpenland): only the plans' own "Grep for" lines and earlier audit lines; none in the new files
- in-game: none (step 1 is pure logic)
- dod status: 0/34 checked — every step-1 item also has a Services, Commands, preflight or in-game half (steps 2–5); the Logic and unit-test halves of D1, D2, D4–D14, D16, D19, D27, D31 and D32 pass their named tests

### Step 2 · 2026-09-26 · 2b083dc + working tree (committed as step 2)
- compile / preflight: 0 errors, 0 warnings; 1313 tests passed; PREFLIGHT OK with "templates: 2 valid", "pillar defaults: all off (5 switches, 2 templates)", "secrets: none", "cfg writes: only PillarSwitches (6 call sites)", "dependency table: event-library 9/9 categories"; -Paths "867 walked, all in manifest"; dod --check 0 problems
- suites: `-SelfTest` → "selftest: 32/32 checks, 6/6 external selftests"; `-AuthSuite` → "auth suite: pass (tests, commands, admin list, gateway, vcf dependency)"; `-Tests ReadinessTests,ControlPrecedenceTests` → "tests: 2/2 classes, 145 passed"; `-DependencySuite event-library` → "dependency suite: event-library 9/9 (events-write, events-promote, state-write, cfg-save, catalogue, location-context, phase-source, vcf, release-tools)"; `-ControlSuite event-library` → "control suite: event-library tests 13/13 classes, selftest 32/32 checks"; fail-closed: `-DependencySuite ghost` → "dependency suite: ghost has no categories", exit 1; `-Tests GhostTests` → "no tests ran", exit 1
- found while building: Invoke-ClassTests' `--no-build` flag came from an `if` expression, which unwrapped the one-element array, so every class after the first failed; the two calls are now written out
- mutation checks: the new fixtures (CfgWrites bad/bad-2, VcfDependency bad to bad-3, TestRuns bad to bad-3, DependencySuite bad/bad-2, TemplatesJson bad-3) each fail and their good fixtures pass in -SelfTest; PillarCfg's section tracking and its bool parse, each removed, fail 2 SaveFailure tests
- /code-review: one finding, fixed (defect) — BepInEx's ConfigFile.Reload sets only the keys it finds and parses, so a [Pillars] key a truncated save or a hand edit removed or garbled kept its old in-memory value, against D19's "reads as its default, off"; Services/PillarSwitches Reload now reads the file through Logic's PillarCfg.Read and sets each missing or unparsable pillar off in memory without a save; SaveFailure_fails_when_file_lacks_or_garbles_key, SaveFailure_passes_file_values_read
- Codex verdict: READY (round 2) — round 1 READY with no findings (its log shows every new file and the preflight functions read); round 2, over the PillarCfg fix, READY with no findings
- amendments: A12 (discovered, ~D30, 14.4): Core.cs and TemplatesJson/good were not in step 2's Paths walked
- in-game: none (step 2 has no in-game test; Session 1 is step 3)
- dod status: 0/34 checked — D17, D18 (its soak-report selftest is step 3), D20, D33 and D34 have their evidence commands passing as above; the Commands and Services halves of D5-D14 and D32 wait for Session 2

### Step 3 · 2026-09-27 · 63d3a80 + working tree (committed as step 3)
- compile / tests: 0 errors, 0 warnings; 1313 passed (step 3 adds tooling only, no C#)
- preflight: PREFLIGHT OK; -SelfTest "selftest: 33/33 checks, 7/7 external selftests (3 fixtures each, 126 extra bad fixtures; secrets: none …)"; soak-report -SelfTest 8/8; rollback-gate -SelfTest 6/6; -Paths -DeclaredOf event-library "paths: 969 walked, all in manifest; declared: 221/221 in event-library"; -SessionsOf event-library 1/1; -RollbackOf event-library "rollback routes: event-library 5/5"; dod --check 0 problems (12 warnings, all old review-round notes)
- mutation checks: a locked tool source fails -Paths -DeclaredOf ("a tool source is unreadable"); scratch soak logs: a restart cancel in the same boot or two boots on, a second natural end, a pillar-off echo after an unrelated line, in the next boot, twice or out of stop order, and an empower plain stop followed by a pillar echo each fail as unpaired; a cancel in the next boot, pillar-off runs of one and two events and an empower pillar-off run pass; six bad preflight usage combinations each exit 2
- /code-review: five findings, all fixed — a missing -Log file was skipped silently (now "soak: fail — no log <path>"); a pillar-off double end and a purge counted as unpaired; a bold marker at a line's start stayed in a Paths walked token; -RollbackOf took the first rollback-gate range anywhere in the plan (now every such command must name one range); an unreadable file under a walked path read as "not written" (now the listing is unreadable and the check fails); plus usage guards for -DeclaredOf, -RollbackOf, -From and -To
- Codex verdict: READY (round 6) — round 1 REVISE: soak-report's frame regex (rejected: the backtick is literal in a single-quoted class, and bad-unhandled fails "1 unhandled"), bad fixtures not held to their planted reason, an unreadable tool source read as empty, -RollbackOf with only -From or -To; round 2 REVISE: a restart cancel paired from any boot, any later end forgiven after an end (A17, defect); round 3 REVISE: a pillar-off echo forgiven across unrelated lines (the real order is all stop lines, then all echoes: an unbroken run; fixture bad-echo); round 4 REVISE: usage guards ran after the early-exit modes (now one mode at a time, before any mode); round 5 REVISE: echoes unordered (now a queue in stop order); round 6 READY, "No material problems found"
- plan reviews during the step: Review 10 (A13) REVISE → A14; Review 11 (A13, A14) REVISE → A15 and the owner's decision D18 on F1 (design §9); Review 12 (A13-A15) REVISE → A16 (F1 rejected by D18); a scoped re-review of A13-A17 waits on the owner's round-cap decision (it would be the fourth round since the last READY)
- amendments: A13, A14 (discovered, ~D30, 14.4 / 4.5), A15 (discovered, ~D30, 12.4), A16 (discovered, ~D30, 3.3), A17 (defect, ~D24, 12.4); each recorded before it was built
- privacy grep (7656119, kdpenland): over the staged step 3 diff, only this audit line itself
- in-game: Session 1 (docs/features/EVENT_LIBRARY.md › Test results), unattended; D3 "templates: 6/6 valid"; log check "0 unhandled, 60 nyar lines, 0 orphan errors, 0 unity errors"; "snapshot restored; hashes equal (s1, … deleted)"
- dod status: D3 (manual, Session 1) and D24 (soak-report -SelfTest 8/8) pass and are checked with the step 3 commit; D29's routes part and D30's paths, inventory and session parts pass as above and are checked at the release, with the gate and the last session

## Sessions
- session 1 log check: 0 unhandled, 60 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (23:10–23:48), unattended; run after the stop and before any restart; BepInEx: the three known warnings (Il2CppInterop, two Beelzebub TUNE); Unity log: 226 PrefabLookupMap lines at save load, the game's RepairVBloodProgressionSystem lookup notice and the hard stop's Crashpad and temp-memory notices; then "snapshot restored; hashes equal (s1, … deleted)"
