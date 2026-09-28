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

### Step 4 · 2026-09-27 · 31e73ea
- git status: clean at 31e73ea (after A18, A19 and Reviews 13-16)
- compile: 0 errors, 0 warnings; tests: 1313 passed
- preflight: PREFLIGHT OK; -SelfTest "33/33 checks, 7/7 external selftests (3 fixtures each, 130 extra bad fixtures; secrets: none …)"; -Paths -DeclaredOf event-library "paths: 1008 walked, all in manifest; declared: 263/263 in event-library; plants: 12/12 fail"; -SessionsOf event-library 1/1; soak-report -SelfTest 9/9
- dod status: event-library 2/34 verified (D3, D24 at 9/9); review subagent (Review 16 READY, 15/15 · 49/49), Codex Review 15 REVISE kept beside it (layer 14 under the owner's D18, widened 2026-09-27)
- tooling between steps 3 and 4: A18 (Review 13) and A19 (Review 14); Codex cross-inspection of that diff, 6 rounds: round 1 REVISE (${env:TEMP}, os.getenv, process.env forms; a marker inside a string), round 2 REVISE (code after a closing #>; process.env["TEMP"], destructuring and GetEnvironmentVariable rejected: the secrets allow-list refuses them), round 3 REVISE (spaced getenv; a nyar- literal in a comment), round 4 REVISE (GetTempPath( ); a quoted <#; a snapshotSessions start that is no session), round 5 REVISE (spaced NamedTemporaryFile; a bare nyar- token; PowerShell backslash no escape), round 6 REVISE (unanchored API tokens, fixed; API text inside a string literal, rejected: a false positive, cleared by a registration); every false-negative finding fixed with a plant in Paths/bad-tempvar (13 lines) or SessionLogs/bad-17..19; the step 4 post-audit's Codex round rereads this code with step 4's change
- feature doc read: docs/features/EVENT_LIBRARY.md (Status: steps 1-3 done; Session 2 steps written); plan D15, D21, D22, D23, step 4; Epic D11 (docs/dod/nyarlathotep.md)
- server: baseline boot of the deployed pre-session DLL (restored by s1) on nyardev: -LogCheck "0 unhandled, 12 nyar lines, 0 orphan errors, 0 unity errors"; the boot carrier sweep removed 74 carriers Session 1's world save held (undead-nightfall's sweep; designed behaviour, S-12); warnings only Il2CppInterop Class::Init, Beelzebub TUNE ×2 and the seeded example-empowerment note; Unity log only the game's 226 PrefabLookupMap lines
- setup: `pwsh tools/dev-snapshot.ps1 -Save s2` → "snapshot saved: s2 (28 files)"; kdpen.Nyarlathotep.cfg and config/Nyarlathotep/ deleted; Release build deployed (hash 93eddc69b87e9521 equal to bin)
### Step 4 · Session 3 · 2026-09-27 · c95fcbe
- git tree clean at c95fcbe; compile 0 errors, 0 warnings, 1320 tests passed; PREFLIGHT OK; dod --check event-library 0 problems, verified 4/34
- feature doc read: EVENT_LIBRARY.md › Test results › Session 3 (the retest steps) and Session 2's open cases
- setup: `dev-snapshot.ps1 -Save s3` (28 files); deploying build with the server stopped, DLL ACDC9D8264A48658 in plugins equals the build output
- baseline boot: "templates: 6/6 valid", "triggers: all hooks available", boot marker and carrier sweeps 0 found; warnings only Il2CppInterop Class::Init, Beelzebub TUNE ×2 and the dev world's old example-empowerment disabled line

### A22 probe · Session 4 · 2026-09-27 · 86f6f53
- git tree clean at 86f6f53 (the Session 4 steps added after); compile 0 errors, 0 warnings, 1320 tests passed; PREFLIGHT OK
- feature doc read: EVENT_LIBRARY.md › Test results › Session 4 (probe steps) and Session 3 Round 3
- setup: `dev-snapshot.ps1 -Save s4` (28 files); Debug.VerboseLogging = true in the dev cfg; deploying build with the server stopped, DLL 189334244498E48D in plugins equals the build output
- baseline boot: "templates: 6/6 valid", "triggers: all hooks available", boot marker and carrier sweeps 0 found; warnings only Il2CppInterop Class::Init, Beelzebub TUNE ×2 and the dev world's old example-empowerment line

### A23 · Session 5 · 2026-09-27 · b4beecf
- git tree clean at b4beecf; compile 0 errors, 0 warnings, 1333 tests passed; PREFLIGHT OK; dod --check event-library 0 problems
- feature doc read: EVENT_LIBRARY.md › Test results › Session 5 (the D35 confirm) and Session 4's finding
- setup: `dev-snapshot.ps1 -Save s5` (28 files); Debug.VerboseLogging = true in the dev cfg; deploying build with the server stopped, DLL B665B0AE108E1ECB in plugins equals the build output
- baseline boot: "templates: 6/6 valid", "triggers: all hooks available", boot marker and carrier sweeps 0 found; warnings only Il2CppInterop Class::Init, Beelzebub TUNE ×2 and the dev world's old example-empowerment line

### Step 5 · Session 6 · 2026-09-27 · b084e47
- git tree clean at b084e47 (Review 19 dispositions; the DLL source unchanged since b4beecf); compile 0 errors, 0 warnings, 1333 tests passed; PREFLIGHT OK (data inventory 31 entries, 54/54 globs and files, 32/32 plan rows); dod --check event-library 0 problems, verified 7/35; review pending (A24, Review 19 REVISE, the round cap reached and put to the owner)
- -Paths -DeclaredOf event-library fails only on the session's own open folders ("leftover temp nyar-session; leftover temp nyar-snap-s6"), removed by the -Restore that closes the session
- feature doc read: EVENT_LIBRARY.md › Test results › Session 5 and the Status block; plan D25, D26, D30 and step 5
- setup: `pwsh tools/dev-snapshot.ps1 -Save s6` (28 files); `python tools/ingame/session-events.py soak --delay 10` (legion-weekend-surge Sun 18:08 to 22:10, twelve times 22 min apart); Release build deployed with the server stopped, DLL B8C75323E580B5DC in plugins equal to the build output
- baseline boot 17:55: "events: reloaded: 4 valid, 0 disabled", "templates: 6/6 valid", "triggers: all hooks available", boot marker and carrier sweeps 0 found; warnings only Il2CppInterop Class::Init and Beelzebub TUNE ×2
### Step 5 · Session 7 · 2026-09-27 · aaaf4d9
- git tree clean at aaaf4d9 (A25's slow-tick warning, A26's short boot); compile 0 errors, 0 warnings, 1337 tests passed; PREFLIGHT OK; -Paths -DeclaredOf event-library clean before the session's own folders; dod --check event-library 0 problems, verified 8/36, review codex
- feature doc read: EVENT_LIBRARY.md › Test results › Session 6 (why the soak runs again) and Open questions; plan D25, D26, D36, step 5 as amended by A25 and A26
- setup: `pwsh tools/dev-snapshot.ps1 -Save s7` (28 files); `python tools/ingame/session-events.py soak --delay 15` (legion-weekend-surge Sun 22:42 to Mon 02:44, twelve times 22 min apart); Release build deployed with the server stopped, DLL 60CFEF7AAFF9D307 in plugins equal to the build output
- baseline boot 22:24: "events: reloaded: 4 valid, 0 disabled", "templates: 6/6 valid", "triggers: all hooks available", "boot marker sweep: 0 found", "boot carrier sweep: 135 found, 135 queued for removal" (Session 6's last undead-nightfall carriers in the world save, the designed cleanup); warnings only Il2CppInterop Class::Init and Beelzebub TUNE ×2
### Step 6 · 2026-09-28 · d989523
- git tree clean at d989523 (step 5 closed at afe59bf; the next child's Review 4 revision committed beside it); compile 0 errors, 0 warnings; 1337 tests passed; PREFLIGHT OK; -AuditOf event-library "5/6 pre, 5/6 post, 5/6 Codex verdicts" before this entry; dod --check event-library 0 problems
- dod status: every test and cmd item run again and recorded (D1, D2, D4-D14, D16-D20, D27, D31-D34 pass); D15, D21-D23 and D35 carry their manual pass lines from Sessions 2, 3 and 5; verified 33/36, the open three being D28, D29 and D30, which this step's release, rollback gate and records close
- feature doc read: EVENT_LIBRARY.md › Status, Open questions (units in water, A27, the 0.5.0 known issue) and Session 7; plan step 6 and the release checklist; the scratchpad release drafts checked against the six surfaces
- no server session in this step; the server is stopped (rollback-gate needs it stopped)
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

### Step 4 fixes · 2026-09-27 · 50c992c + working tree (A20, A21, committed with this record)
- compile / tests: 0 errors, 0 warnings; 1320 passed (7 new: the Point height load and two y refusals, the rounded height and its bounds, the long militia-crackdown trigger in list, info and event list with BossLines wrapping 40 names, and WavePlan.Center)
- mutation checks: Fits forced true and a dropped stored y failed 5 tests; WavePlan.Center without the height failed Wave_center_uses_point_height; each restored and green
- preflight: PREFLIGHT OK; dod --check event-library 0 problems
- /code-review: no findings (a schedule trigger with very many days or times could still pass 480 bytes; outside A21, which names vbloodkilled lines)
- Codex verdict: READY (round 1) — F1 advisory accepted: the spawn centre moved to Logic WavePlan.Center with a test, so dropping the height fails a test; F2 advisory rejected: an action line of ten 96-character prefabs could still be cut, outside A21/D4, which name trigger lines, and no shipped or authored event comes near it; F3 advisory rejected: ±10000 is D11's bound for every axis, `location here` reads the admin's real position, and a hand-edited height is admin-authored like x and z; F4 confirmation
- in-game: D21 cases 1 and 12 and D22's four silent commands are rerun in Session 3

### A22 probe · 2026-09-27 · 9f2f315 + working tree (Services/GroundProbe.cs, log only)
- compile 0 errors, 0 warnings; 1320 tests passed; PREFLIGHT OK
- scope: a VerboseLogging-only spike that reads Translation, ProjectM.Height, SnapToHeight and FallToHeight at spawn, ~1 s and ~5 s; it writes nothing. The plan's CollisionWorld ray was left out: the game's Height component (ProjectM.HeightCorrectionSystem) answers the question directly with less IL2CPP risk (Codex agreed, F4)
- /code-review (self, 60-line diff): no findings
- Codex verdict: READY (round 2) — round 1 NOT READY: F1 pending samples kept logging after VerboseLogging turned off (fixed: Tick clears them), F2 units past 64 got no follow-up silently (fixed: pass 0 says "not followed"), F3 the flag read sat outside the try (fixed); round 2 no findings
- privacy grep (7656119, kdpenland): none in the diff
- in-game: Session 4 (the probe session)

### A23 regroup · 2026-09-27 · 2242093 + working tree
- compile 0 errors, 0 warnings; 1333 tests passed (1320 before); PREFLIGHT OK; dod --check event-library 0 problems
- scope: WavePlan.Anchor, Regroup, Step, RegroupPoint (Logic/Engine.cs) and SpawnOrder.Anchor (Logic/SpawnLedger.cs); SpawnTracker.Regroup writes Translation, LastTranslation and AggroConsumer.PreCombatPosition on tracked units only; Services/GroundProbe.cs removed
- mutation check: WavePlan.Regroup forced to false → 3 tests fail
- /code-review (self): no findings
- Codex verdict: READY (round 2) — round 1 NOT READY: F1 the runtime regroup path untested (partly accepted: the per-look decision and the target point moved into Logic with tests, Wave_unit_regroup_step_waits_for_the_snap and Wave_unit_regroups_within_a_metre; the ECS shell is D35's manual check, as for the plan's other Services halves), F2 AI home may stay at the ring point (accepted: PreCombatPosition follows, as Bloodcraft's familiar return does); round 2 one advisory: Session 5 checks units stay near the centre
- privacy grep (7656119, kdpenland): none in the diff
- in-game: Session 5 (D35 confirm)

### Step 4 · 2026-09-27 · 31e73ea..bc74beb (Sessions 2-5, A20-A24)
- compile 0 errors, 0 warnings; 1333 tests passed; PREFLIGHT OK; -SelfTest 33/33; -Paths -DeclaredOf event-library "paths: 1011 walked, all in manifest; declared: 274/274 in event-library; plants: 12/12 fail, tempvar 15/15 lines"; -SessionsOf "session logs: event-library 5/5 checked; snapshots 5/5 from session 1"; dod --check event-library 0 problems
- in-game: Sessions 2-5 recorded under EVENT_LIBRARY.md › Test results; D15, D21, D22, D23, D35 and Epic D11 pass
- /code-review (self) of each fix: no findings (entries above)
- Codex verdict: READY (round 2) — round 1 NOT READY over the step's code diff and the A18/A19 temp scan: F1 SpawnManual's anchor (rejected: A23's D35 names `.nyar spawn`), F2 spaced member access `os.environ .get("TEMP")` / `process . env . TEMP` missed by the temp-root scan (accepted: pattern widened, two plants, A24); round 2 READY with one advisory (WavePlan's stale height comment in Services/WaveAction.cs, fixed)
- plan: A24 (gating 14.4) → review pending; Review 17 (codex, scope A24) REVISE, F1-F3 accepted (F1's fail-after-delete half rejected); Review 16's carried F2 (the -SessionsOf snapshot count) and F6 (matrix rows) built here
- leftovers: %TEMP%\nyar-s4-logs and %TEMP%\nyar-s5-logs (hand copies of Sessions 4 and 5's logs) found by -Paths and deleted; later sessions copy logs to %TEMP%\nyar-soak-* or delete them in the session
- privacy grep (7656119, kdpenland): only the audit's own grep lines

### A25 slow-tick warning · 2026-09-27 · b297046 + working tree
- compile 0 errors, 0 warnings; 1337 tests passed (1333 before); PREFLIGHT OK; -Paths -DeclaredOf event-library "paths: 1022 walked, all in manifest; declared: 291/291 in event-library; plants: 13/13 fail, tempvar 15/15 lines"; dod --check event-library 0 problems
- scope: Logic SlowTickLog (Logic/Engine.cs) and its wiring in Services/EventScheduler.cs (a Stopwatch sample per phase; the warning built in its own try/catch); D36's row in ControlCases.Table and D31's control list
- mutation check: threshold 250 → >250, the phase order dropped, the quiet minute removed, the held-back count dropped → each fails one SlowTick test
- /code-review (self): one finding fixed — a clock stepped back would have silenced the warning until it caught up (utcNow >= last guard, asserted in SlowTick_fails_when_ticks_repeat_within_a_minute)
- Codex verdict: READY (round 2) — round 1 NOT READY: F1 the quiet minute was measured from the tick's start, so a tick of a minute or more could let the next warning through at once (accepted: DateTime.UtcNow at the warning)
- privacy grep (7656119, kdpenland): none in the diff
- in-game: Session 7 (the soak run again, A26)

### Step 5 · 2026-09-28 · aaaf4d9..92a21ce (records of Session 7; no code since A25's post-audit)
- compile / preflight: 0 errors, 0 warnings; SlowTick 4/4 passed; PREFLIGHT OK; -Paths -DeclaredOf event-library "paths: 1024 walked, all in manifest; declared: 294/294 in event-library; plants: 13/13 fail, tempvar 15/15 lines" after A28; -SessionsOf event-library "7/7 checked; snapshots 7/7 from session 1"; dod --check event-library 0 problems
- soak: soak-report over Session 7's three boots "soak: pass" (565 timing minutes, 0 unpaired, tick avg max 3.528 ms); no "slow tick" line; D25 and D36 pass, D26 re-verified on Session 6 (A30)
- /code-review (self, records only): the Session 7 block lacked D25's first start and end lines per template (added, from the soak-1 log read before its deletion); the restart deviation is stated in the feature doc, the audit and the plan's log
- plan: A28 (14.4, the next child's plan files declared as docs/dod/event-spawns.*), Reviews 21 REVISE (F1 accepted with a different control) and 22 READY (scope A28); A29 and A30 (7.3) name the plan's Session 3 as feature-doc Sessions 6 and 7
- Codex verdict: READY (round 2) — round 1 REVISE: F1 D25's first start and end lines and the Hunter observation's session (A29, lines added), F2 D26 named Session 3 while its pass was Session 6 (A30, pass re-verified); round 2 READY, "No findings", EARLIER: all resolved
- privacy grep (7656119, kdpenland): none in the diff
- in-game: Session 7 (above); dod status: D25, D26, D36 checked with pass lines
### Step 6 · 2026-09-28 · 06d6325 (chore(release): v0.5.0)
- six surfaces: csproj Version and thunderstore.toml versionNumber 0.5.0; both changelogs (0.5.0 entries from the drafts, the root soak line filled from Session 7); both READMEs' Quick start the five commands of D23 (the root README gains a Quick start section), the templates and authoring sections, the water known issue (A27); preflight "release tags: 5/5", PREFLIGHT OK
- tcli build: Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.5.0.zip, built from 06d6325 (`dotnet build -c Release --no-incremental`, 0 warnings, 0 errors, the DLL deployed to the stopped dev server with an equal hash 18D67BDCD04A2161…) with icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (391680 bytes), CHANGELOG.md, LICENSE
- zip sha256: kdpen-Nyarlathotep-0.5.0.zip 4330CE533097B1D539364436172922B7B665D56331D0936ECCD08DD922BCE56E
- rollback gate before the push: `pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0 -Plan event-library` → "rollback gate: 4/4" (repository drill with -BeforePush; N-1 boot drill "events.json: v0.5.0 '6 valid, 0 disabled', v0.4.0 '6 valid, 0 disabled'", v0.4.0 boot log check 0 unhandled, "rollback drill: pass"; snapshot selftest 6/6; "rollback routes: event-library 5/5")
- privacy grep (7656119, kdpenland) over the 59 pushed commits and the v0.5.0 tree: only lines quoting the pattern (the audits' grep records and the next child's D31); no 17-digit id; commit messages none; the author identity is the owner's git config, as on every earlier push
- release: v0.5.0 tagged at 06d6325 and pushed with main; GitHub pre-release https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.5.0 with the zip; no tcli publish (the owner publishes)
- found in the release: `pwsh tools/release-verify.ps1 -Tag v0.5.0 -Asset kdpen-Nyarlathotep-0.5.0.zip` (D28's command) failed "the audit has no zip sha256 line": -Audit defaulted to faction-empowerment's audit (A31, defect); fixed in 25d122f and fb4982b (without -Audit the one audit recording the asset's own line is used; none, two audits or two lines in one audit fail); release-verify -SelfTest 8/8; then "release verify: hashes equal" for v0.5.0, and for v0.4.0 both with and without -Audit
- Paths: the v0.5.0 remote tag and release added to tools/paths-manifest.txt (`remote-tag:`, `remote-release:`), tools/release-verify.ps1 and tools/preflight-checks.json declared in Paths walked step 6 (A31); -Paths -DeclaredOf event-library "declared: 302/302"
- /code-review (self, 06d6325..fb4982b): the new Find-ReleaseAudit uses Test-ReleaseAsset's own-line regex, so a quoted line elsewhere does not select an audit; the -Audit path is unchanged; each new selftest case fails when Find-ReleaseAudit returns the wrong audit or no reason
- plan: Reviews 23 (codex, scope A31) REVISE (F1-F3 accepted: the duplicate-line case) and 24 READY, 15/15 layers · 49/49 probes
- Codex verdict: READY (round 1) — cross-inspection of 06d6325..25d122f (the six surfaces, the audit record, the manifest lines and the A31 fix): "No findings"; the later selftest case (fb4982b) is covered by Review 24 READY

## Sessions
- session 1 log check: 0 unhandled, 60 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (23:10–23:48), unattended; run after the stop and before any restart; BepInEx: the three known warnings (Il2CppInterop, two Beelzebub TUNE); Unity log: 226 PrefabLookupMap lines at save load, the game's RepairVBloodProgressionSystem lookup notice and the hard stop's Crashpad and temp-memory notices; then "snapshot restored; hashes equal (s1, … deleted)"
- session 2 log check: 0 unhandled, 15 nyar lines, 0 orphan errors, 0 unity errors
  - two boots (the first to 16:09, the second 16:10–16:15); the line is the second boot's, run after the final stop; the first boot's logs were overwritten by the restart before a copy was taken, so they were read only in part during the session (no [Error], no unexpected warning); BepInEx: the three known warnings; Unity log: 224 + 2 PrefabLookupMap lines at save load, 0 exceptions; then "snapshot restored; hashes equal (s2, … deleted)"
- session 3 log check: 0 unhandled, 74 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (16:2x–16:52), both logs copied before the stop, which followed an autosave after the last action; BepInEx: the three known warnings, the dev world's old example-empowerment line at each reload, two stop summaries; Unity log: 0 exceptions, no PrefabLookupMap line after startup; then "snapshot restored; hashes equal (s3, … deleted)"
- session 4 log check: 0 unhandled, 80 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (17:0x–17:13), both logs copied before the stop, which followed AutoSave_1133 after the last action; BepInEx: the three known warnings, the old example-empowerment line at each reload, one stop summary; Unity log: 0 exceptions, no PrefabLookupMap line after startup; then "snapshot restored; hashes equal (s4, … deleted)"
- session 5 log check: 0 unhandled, 69 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (17:2x–17:31), both logs copied before the stop, which followed AutoSave_1137 after the last action; BepInEx: the three known warnings, the old example-empowerment line at each reload, two stop summaries; Unity log: 0 exceptions, no PrefabLookupMap line after startup; then "snapshot restored; hashes equal (s5, … deleted)"
- session 6 log check: 0 unhandled, 373 nyar lines, 0 orphan errors, 0 unity errors
  - two boots (17:55–20:34 and 20:34–22:09); each boot's logs copied (%TEMP%\nyar-soak-1, nyar-soak-2) after an autosave that followed the last action, then -LogCheck (the first boot's line: 0 unhandled, 340 nyar lines, 0 orphan errors, 0 unity errors) before the next boot; BepInEx: the three known warnings in each boot; Unity log: 0 exceptions, the 226 PrefabLookupMap notices at save load only; soak-report failed (1 unpaired, 1 slow window at the time of a Windows shadow copy, phase unknown, EVENT_LIBRARY.md › Session 6); both folders deleted after the lines were copied
- session 7 log check: 0 unhandled, 10 nyar lines, 0 orphan errors, 0 unity errors
  - three boots (22:24–07:38, 07:40–08:02 and the A26 short boot 08:02–08:05); each boot's logs copied (%TEMP%\nyar-soak-1, -2, -3) after an autosave that followed the last action (AutoSave_1540, 1551, 1552), then -LogCheck before the next boot (0 unhandled, 2097 nyar lines, 0 orphan errors, 0 unity errors; 0 unhandled, 95 nyar lines, 0 orphan errors, 0 unity errors; the line above); BepInEx: the three known warnings in each boot; Unity log: 0 exceptions, the 226 PrefabLookupMap notices at save load only; soak-report "soak: pass" (565 timing minutes, 0 unpaired); the mid-soak restart ran during undead-nightfall, not a Legion surge (EVENT_LIBRARY.md › Session 7); then "snapshot restored; hashes equal (s7, … deleted)"; the three folders deleted after the lines were copied
