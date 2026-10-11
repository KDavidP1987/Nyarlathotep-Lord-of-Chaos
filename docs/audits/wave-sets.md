# Audit — wave-sets

Build plan steps 1–3 of docs/dod/wave-sets.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; the child's single Codex cross-inspection (design §9 D34) is in step 3's post-audit, and every
other post-audit carries a "Codex verdict:" line pointing to it. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.8.0.

## Pre-audit
### Step 1 · 2026-10-04 · 9a402e3
- pre-child: automation closed and 0.8.0 released; the plan approved after Review 1 (READY, F1-F10 advisory, all applied) and started at 9a402e3
- git status: clean at 9a402e3
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2446 passed
- preflight: PREFLIGHT OK
- dod status: wave-sets 0/27 verified (just started); `dod-index.mjs --check wave-sets` → problems 0, warnings 0
- feature doc read: docs/features/WAVE_SETS.md is created by this step; docs/features/EVENT_SPAWNS.md and AUTOMATION.md read for the wave gate, the roll and the fan-out deal this child extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

### Step 2 · 2026-10-04 · 6e251e9
- git status: clean at 6e251e9 (step 1 built, reviewed and post-audited)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2591 passed
- preflight: PREFLIGHT OK
- dod status: wave-sets 11/27 checked; `dod-index.mjs --check wave-sets` → problems 0, 1 warning (review pending, A2)
- feature doc read: docs/features/WAVE_SETS.md Status (step 1 built) and Open questions (whether a vampire's death to an NPC's projectile raises a DeathEvent with the killer resolvable, which Session 1 answers); step 2 carries D3, D5 and D7's wiring (step 1 post-audit)
- in-game baseline: the deployed 0.8.0 DLL (0.8.0+40e3d6a) booted on the dev server, logs first copied to %TEMP%\nyar-ws1-logs\pre-baseline: "templates: 9/9 valid", "triggers: all hooks available", "boot marker sweep: 0 found", "Nyarlathotep initialized … (attempt #1)"; 3 [Warning] lines, none new (Il2CppInterop's substitute notice, a Beelzebub TUNE line, the shipped example-empowerment reason line); 0 [Error]; -LogCheck "log check: 0 unhandled, 10 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; server stopped with taskkill (no /F) after 4 s
- session 1 log check: boot 1 (the step 2 build + c6deb38's tools) "log check: 0 unhandled, 449 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; boot 2 "0 unhandled, 487 nyar lines, 0 orphan errors, 0 unity errors"; boot 3 (A6's diagnostic) "0 unhandled, 933 nyar lines, 0 orphan errors, 0 unity errors"; every [Warning] read: Il2CppInterop's substitute notice, Beelzebub's TUNE line, example-empowerment's reason, and the stop and purge lines (warnings by design); 0 [Error]; each stop by taskkill without /F within 4 s; an aborted first attempt (A5's .devkill, 232 world entities) was rolled back to AutoSave_3527, its post-kill AutoSave_3528 moved aside
- session 1 snapshot: "snapshot restored; hashes equal (ws1, …)"

### Step 3 · 2026-10-11 · 0802476
- git status: clean at 0802476 apart from the step's six release surfaces, CLAUDE.md's status line and WAVE_SETS.md's Status, being written (the step's own build); steps 1 and 2 post-audited (603f958), D24's drill built (0c6daa3)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release --no-incremental -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 2619 passed
- preflight: PREFLIGHT OK; `pwsh tools/rollback-drill.ps1 -SelfTest` → "drill selftest: 20/20"
- dod status: wave-sets 23/27 checked (D24-D27 are this step's); `dod-index.mjs --check wave-sets` → problems 0, warnings 2 (D26 named by 5 amendments; review pending until the amendment re-review)
- feature doc read: docs/features/WAVE_SETS.md Status (step 2 done) and Open questions (A6 not exercised, A7 a likely one-time warm-up), carried into the release notes as stated
- in-game baseline: not needed (step 3 has no in-game part); the dev server from Session 2 stopped by taskkill without /F within 4 s after both logs were copied (scratchpad ws2-logs LogOutput.final.log, NyarDev.final.log): 0 [Error], [Warning] lines the known four (Il2CppInterop's substitute notice, Beelzebub's TUNE line, example-empowerment's reason ×2, the ws-three stop line)
- session 2 restore (A10): cfg and events.json put back from the pre-session copies; SHA-256 equal (cfg 606a6b83111401c6… = 606a6b83111401c6…, events.json 9c619b1a7ee19927… = 9c619b1a7ee19927…)

## Post-audit
### Step 1 · 2026-10-04 · aa1aece
- built (6d7f6ad): the waveList and scoreboard schema and validation (D1, D2), per-entry tuning through the roll, the clamps and the fan-out deal (SpawnTuning.For; D3), WaveSchedule and the per-wave decision times (D4), SpawnLedger's wave on orders and units and WaveCleared (D5), the schedule readers (D6), Engine.Complete (D7), Logic/Scoreboard.cs with ScoreboardRule.End per end path (D8, D9's rule, D11's rule), Messages.ScoreboardLines (D10), the privacy boundary (D12), the `event set` wave-list fields and the conversion (D14), the `event info` wave lines (D15), the contract's two sentences (D17), the ControlCases rows and Pending entries (D19); Config/Settings.cs Scoreboard.IncludeAdmins moved here from step 2 (A2)
- found while building: A1 (a whenCleared-false wave needs afterSeconds; victory needs every wave cleared) and A2 (Paths walked missed four files, the build outputs and the tools' %TEMP% folders; -DeclaredOf read 41/61), both recorded before the code
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2591 passed
- preflight: PREFLIGHT OK; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"; -Paths -DeclaredOf wave-sets → "paths: 1833 walked, all in manifest; declared: 62/62 in wave-sets; plants: 15/15 fail, tempvar 15/15 lines"
- planted faults (scratchpad driver, each fault planted, its filter run, the source restored): D1, D2, D3, D4, D5, D6, D7, D8, D9, D10, D11, D12, D14 (two plants), D15, D17 and the review fixes A3, F1, F6 — all 19 CAUGHT
- /code-review round 1 (fresh subagent, read-only, on c8e4f97..6d7f6ad): VERDICT READY, 6 advisory findings, all dispositioned in aa1aece: F1 a units-form field on a waveList event was written and only disabled the event → fixed (EventsEditor refuses it with WaveListTakesWaves; CommandArgTests WaveSets_fails_when_a_units_form_field_meets_a_wave_list); F2 a stop and start inside the grace let the old instance's units hold the new wave 1 and would credit their kills → fixed as amendment A3 (discovered, 7.2), recorded first (WaveCleared and EventOf take the instance's start; EventEngine.ClearedBy); F3 WaveDecided's optional decision time could give a later wave the start's time → fixed (time and queued count required; WaveAction passes now and the queued units); F4 singular counts differed from D10 and D21's text → fixed (always plural); F5 "<p> players" stops at the 200 rows → accepted and documented in docs/features/WAVE_SETS.md (the cap bounds memory; an uncapped id set would not); F6 a wave's count and chance reasons did not name the entry → fixed ("action.waveList.<n>.units.<m>.count")
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)
- in-game: none in step 1 (Session 1 is step 2)
- dod status: D1, D2, D4, D6, D8, D10, D12, D14, D15, D17, D19 pass at aa1aece and are checked; D3, D5, D7 wait for step 2's WaveAction and EventRuntime, which wire the tested logic; D9 and D11 for step 2 (the binding's read and the end-path calls); --check problems 0, 1 warning (review pending: the A2 re-review is owed before close)
- privacy grep (7656119, kdpenland): none in the step 1 diffs

### Step 2 · 2026-10-11 · 603f958
- session 2 pre-audit (2026-10-11, e4f0594, the first session after the move to C:\dev\projects): git clean and equal to GitHub; compile 0 Warning(s), 0 Error(s); tests 2614 passed, 1 failed (ControlCases "pending wave-sets D23 outlived step 2's post-audit", expected until D23 passes); preflight PREFLIGHT OK; Reference Data and Learning Mods resolve in the new tree
- session 2 build: e8c35d3 (A7's hunt-parts line, D23's control row); tests 2618 passed; deployed by a Release build
- session 2 in-game: D23 pass (10/10 windows under 5 ms); wave-sets-timing.vrs PASS 8/8; -LogCheck 0 unhandled, 0 orphan errors, 0 unity errors; every [Warning] read, 0 [Error]
- session 2 paths: -Paths -DeclaredOf wave-sets read 93/100 before A8, 99/99 after; the session's log folders moved out of %TEMP%\nyar-* (leftover temp)
- built: 33063d9 (services, template), c6deb38 (Session 1 tooling); Session 1 in docs/features/WAVE_SETS.md › Test results
- in-game: D20, D21, D22 pass; D23 fail (176.8 ms hunt-phase tick, A7); A6 (uncredited batch deaths) still unexplained
- not yet run: A7 diagnostic and D23 rerun, /code-review on c5c8848..HEAD, planted faults, dod status, privacy grep
- handoff: docs/SESSION_HANDOFF.md
- planted faults (scratchpad driver: each fault planted, its filter run, the source restored and rebuilt): D3, D5, NewlyCleared, D7, D11, D13, D16 CAUGHT; D8 (a self-kill with a player owner credited) and D9 (ScoreRule ignoring IncludeAdmins) MISSED: test gaps, closed in 659179a (ScoreboardTests: the self-kill case with Owner, the admin cases through ScoreRule.Credit), then both CAUGHT: 9/9
- /code-review (fresh subagent, read-only, on c5c8848..HEAD): VERDICT READY, 4 advisory: F1 two failed IL2CPP GC reads logged "il2cpp 0" (a false no-collection) → fixed, "n/a" (603f958, EngineTests.HuntParts_fails_when_an_unreadable_count_reads_as_zero); F2 the breakdown took the last phase as the hunt's → fixed, by name (603f958); F3 `feed.Before(() => throw ex, …)` reads like a bug → accepted (style, behaviour correct); F4 the guarded cleared read is rebuilt per access → accepted (negligible at the caps)
- compile: 0 Warning(s), 0 Error(s); tests: 2619 passed; preflight PREFLIGHT OK; -Paths -DeclaredOf wave-sets 99/99
- privacy grep (7656119, kdpenland) over c5c8848..HEAD: none
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)
- dod status: D3 D5 D7 D8 D9 D11 D13 D16 D18 D23 pass and checked; D24-D27 are step 3's

### Step 3 · 2026-10-11 · 6df0afe
- built: 0c6daa3 (D24's drill: newer keys waveList and scoreboard, fixtures pair-wavelist, pair-scoreboard and pair-colourkey per A9, selftest 20/20); the six surfaces, CLAUDE.md's status and WAVE_SETS.md's Status (D25); 02ebc62 and 6df0afe (review fixes, below)
- found while building: A10 (Session 2 had no dev-snapshot; config restored by hand, hashes equal), recorded before this audit
- Codex round 1 (v0.8.0..working tree, tools/vrclient/** left out; the child's one cross-inspection, design §9 D34): REVISE — F1 blocking: a throwing Scoreboard.Wants read opened the kill-read streak and the next feed.Idle() cleared it, so the health entry never held and every frame logged again → fixed in 02ebc62 (ScoreFeed.Wants; WaveSetDependencyFailureTests.KillRead_fails_when_a_throwing_wants_read_ends_its_streak, its planted fault CAUGHT). Log: 2 "blocked by policy" matches, both its own grep of earlier audit lines, no command blocked
- Codex round 2: REVISE — F1 blocking: the root README said "Nine built-in templates" → Ten, plus a wave-sets sentence; F1 of round 1 confirmed; 0 blocked
- /code-review (fresh subagent, read-only, 8b80ba2..working tree): REVISE — F1 blocking: the package README gave `CHAR_<name> <count>` for `waveList.<n>.units`, the parser takes `CHAR_<name>[:<count>]` → fixed; F2 advisory whenCleared's value → `(true|false)`; F3 advisory the package CHANGELOG's victory needs every wave fought → reworded; F4 advisory the drill's case text in tools/preflight-checks.json → the three pairs added; drill and 02ebc62 confirmed
- Codex round 3: REVISE — F1 blocking: the root README's "0.8.x is a public beta" → 0.9.x; the package README's Uninstall gains 0.8.0's rollback sentence; 0 blocked
- Codex round 4: REVISE — F1 blocking: the csproj <Description> named 0.4.0 → rewritten without a version; 0 blocked
- Codex round 5: REVISE — F1 blocking: SpawnLedger.TakeDespawns dropped a unit's record before DestroySafe; a failed destroy requeued it as a survivor without event and wave, so a unit with its own lifetime could leave its wave reading cleared (or "all waves defeated") while alive → fixed in 6df0afe (SpawnLedger.RetryDespawn; SpawnLedgerTests.WaveCleared_fails_when_a_failed_destroy_reads_cleared, its planted fault CAUGHT); 0 blocked
- Codex verdict: READY (round 6, v0.8.0..working tree at 6df0afe) — no findings; 0 "blocked by policy"
- compile: `dotnet build -c Release --no-incremental` 0 Warning(s), 0 Error(s); tests: 2621 passed
- preflight: PREFLIGHT OK; drill selftest 20/20
- privacy grep (7656119, kdpenland) over the tree and `git log -p origin/main..main`: only lines quoting the pattern (plans' D-items and dry-run notes, earlier audits' grep lines)
- in-game: none in step 3 (D20-D23 passed in Sessions 1 and 2)
- tcli build: kdpen-Nyarlathotep-0.9.0.zip (428817 bytes), built at the release commit f65736b with a clean tree (`dotnet build -c Release --no-incremental`, 0 warnings, 0 errors; the DLL also deployed to the stopped dev server) with the tcli token variable set to a sentinel, log build/tcli-build.log; icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (717312 bytes), CHANGELOG.md, LICENSE; preflight "secrets: none (1878 files scanned, 1836 index blobs)" with the zip, dist/, build/ and the log in the scan
- zip sha256: kdpen-Nyarlathotep-0.9.0.zip 4AC0498BE8AFE4AC3EA7450353CA5554111CF2CB613D76A215E2CE5A82D7C7CB
- tag: v0.9.0 (annotated) at f65736b, local; `pwsh tools/rollback-gate.ps1 -From v0.8.0 -To v0.9.0 -Plan wave-sets` → "rollback gate: 4/4" (routes 5/5 after the Rollout line was reworded to the gate's phrase, a Log note)
