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

## Post-audit
### Step 1 · 2026-09-26 · e84c8a3 + working tree (committed as step 1)
- compile / preflight: 0 errors, 0 warnings; 1309 tests passed (871 before the step); PREFLIGHT OK; dod --check 0 problems (6 warnings, all old review-round notes)
- mutation checks: 28 planted defects in a scratch copy, one per control and per post-audit fix (D1, D4, D5, D6/D27, D8 ×3, D9 ×2, D10 ×2, D11, D12 ×2, D13, D14 ×3, D16, D17, D19 ×4, D20 ×2, D32, and the null lastStart), each fails at least one named test; D5 "enabled false" and D20 "event copy max 3" first survived and got TemplateUse_passes_enabled_template_copied_disabled and two surplus-argument CommandForms cases
- /code-review: three findings, all fixed — `pillar <name> off` on a pillar a cfg edit already turned off left its events running (A9, discovered, ~D14; PillarCommand_passes_off_after_hand_edit_ends_running); `event set <id> location here` passed validation and then always failed (Commands/EventCommands.cs now resolves the position through LocationArg.FromContext; defect); a delete with state.json read-only dropped the cooldown row from memory only and said "cleared on the next save" (A10, discovered, ~D8; StateWrite_fails_when_state_read_only)
- Codex verdict: READY (round 4) — round 1 REVISE: the stale-file check ran after the edit plan, so a hand edit could be interpreted instead of refused (defect; EventsFile.Writable before the plan, Equivalence_fails_when_stale_file_alters_the_plan; foundation's A_refused_edit_pushes_nothing now expects the stale refusal for a file broken since the load), and D2 had no trigger-type or times case (added); round 2 REVISE: a state.json with "lastStart": null would throw in the delete confirm and the engine (defect; StateDocument.TryParse normalises it, StateWrite_empty_null_cooldown_rows); round 3 REVISE: D5's plan text read "as <newId>", which chat drops as a tag (A11, discovered: the plan corrected to "as new-id", the test asserts no "<"); round 4 READY, no findings
- privacy grep (7656119, kdpenland): only the plans' own "Grep for" lines and earlier audit lines; none in the new files
- in-game: none (step 1 is pure logic)
- dod status: 0/34 checked — every step-1 item also has a Services, Commands, preflight or in-game half (steps 2–5); the Logic and unit-test halves of D1, D2, D4–D14, D16, D19, D27, D31 and D32 pass their named tests
