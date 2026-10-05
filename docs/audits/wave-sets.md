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
