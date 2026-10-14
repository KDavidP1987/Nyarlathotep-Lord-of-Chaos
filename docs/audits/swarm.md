# Audit — swarm

Build plan steps 1–3 of docs/dod/swarm.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one under
"## Post-audit"; the child's single Codex cross-inspection (design §9 D34) is in step 3's post-audit, and every other
post-audit carries a "Codex verdict:" line pointing to it. Session log checks are lines "- session <n> log check: …".
Rollback base: v0.9.0.

## Pre-audit
### Step 1 · 2026-10-11 · b22f135
- pre-child: wave-sets closed (27/27) and 0.9.0 released (tag v0.9.0, GitHub pre-release); the plan approved after Review 2 (READY, F1-F5 deferred to the Log) and started at b22f135
- git status: clean at b22f135 before the start line, docs/features/SWARM.md and this record
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2621 passed
- preflight: PREFLIGHT OK
- dod status: swarm 0/25 verified (just started); `dod-index.mjs --check swarm` → problems 0, warnings 1 (prose: MaxConcurrentEvents named in Use cases without an item, an existing cap this child does not change)
- feature doc read: docs/features/SWARM.md is created by this step; docs/features/WAVE_SETS.md read for the wave list, the clear, the victory and the scoreboard this child extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

### Step 2 · 2026-10-11 · 0b05cc9
- git status: clean at 0b05cc9 (step 1 built, reviewed and post-audited, pushed and verified)
- compile: Release 0 Warning(s), 0 Error(s); tests: 2744 passed
- preflight: PREFLIGHT OK; -Paths -DeclaredOf swarm → "declared: 52/52 in swarm"
- dod status: swarm 11/25 checked; `dod-index.mjs --check swarm` → problems 0, 1 warning (prose: MaxConcurrentEvents, an existing cap)
- feature doc read: docs/features/SWARM.md Status (step 1) and Open questions (whether the game's own leash holds our units, Session 1's first probe)
- in-game baseline: the deployed 0.9.0 DLL (f65736b) booted on the dev server, logs first copied to %TEMP%\nyar-sw1-logs\pre-baseline: "templates: 10/10 valid", "triggers: all hooks available", "boot marker sweep: 0 found", "Nyarlathotep initialized … (attempt #1)"; 3 [Warning] lines, none new (Il2CppInterop's substitute notice, Beelzebub's TUNE line, example-empowerment's reason); 0 [Error]; -LogCheck "log check: 0 unhandled, 10 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; stopped by taskkill without /F in 6 s

### Step 3 · 2026-10-11 · 53222e8
- git status: clean at 53222e8 (step 2 post-audited, pushed and verified: HEAD = origin/main)
- compile: Release 0 Warning(s), 0 Error(s); tests: 2768 passed
- preflight: PREFLIGHT OK, "secrets: none (1908 files scanned, 1863 index blobs)"; -SessionsOf swarm → "session logs: swarm 2/2 checked; snapshots 2/2 from session 1"
- dod status: swarm 17/25 checked; `dod-index.mjs --check swarm` → problems 0, 1 warning (prose: MaxConcurrentEvents, an existing cap)
- feature doc read: docs/features/SWARM.md Status and Open questions (the in-game return, waiting on the owner's D18 run)
- in-game baseline: not needed (step 3 has no in-game part; the dev server stopped after Session 2 and restored, "snapshot restored; hashes equal (sw2, …)")
- written after the step began: the build of D20 (one builder subagent) started right after these checks ran at 53222e8; this entry records them

## Post-audit
### Step 1 · 2026-10-11 · 2309b9b
- built (9e62092, one builder subagent; the orchestrator reviewed, tested and committed): Model's Arena, WinnerRule and Growth as trailing optional parameters; ParseSpawnWaves's three keys and the D1, D2, D24 reasons as constants; Logic/Growth.cs Generate (D25); Logic/Arena.cs, the pure containment rule (D3); Scoreboard's winner over every row (D5); ScoreboardLines' winner line (D6) and ScoreboardRule.LogLine's suffix (D7); the six `event set` fields, their refusals and the info lines (D9, D10); design §6 rows; the contract's D12 sentence; ControlCases rows and Pending entries; csproj copies; preflight-checks childDocs, dataTables, snapshotSessions; data-inventory rows; paths-manifest `temp: nyar-sw*-logs`
- found while building: CommandArgTests.Library.cs missing from Paths walked (added); Review 2 F1 decided as a refusal at set time and F4 built as the deferred note says; ScoreboardRule.End reads the instance's own definition; all logged as notes in the plan (they change no item)
- compile: Release (--no-incremental) and Debug 0 Warning(s), 0 Error(s); tests: 2744 passed (2621 before the step)
- preflight: PREFLIGHT OK, "secrets: none (1897 files scanned, 1855 index blobs)"; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"
- planted faults: 24 planted, 24 CAUGHT (the builder's 23: D1, D2, D3 ×3, D5 ×2, D6 ×2, D7 ×2, D8, D9 ×2, D10 ×2, D12, D14, D24 ×2, D25 ×3; the orchestrator's one for review F1: Apply comparing only the first conflict); each file backed up to the scratchpad and restored by copy, rebuilt, suite green after
- /code-review (fresh subagent, read-only, on f80e02f..9e62092): VERDICT READY, 0 blocker, 0 major; F1/F2 minor (Apply compared only the first conflict, so a fix could be refused and a new conflict could pass) → fixed in 2309b9b (EventsEditor.SwarmConflicts, the set; CommandArgTests Swarm_fails_when_an_old_conflict_blocks_a_fix_or_hides_a_new_one, planted fault caught); F3 advisory (a vacuous made-up-leak assertion) → replaced by the mark sweep over every line; F4 advisory (no long accepted reply to measure) → documented on the test
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)
- in-game: none in step 1 (Session 1 is step 2)
- dod status: D1, D2, D5, D6, D8, D9, D10, D12, D14, D24, D25 pass at 2309b9b and are checked; D3 and D7 wait for step 2's arena phase and end-path wiring, which use the tested logic
- privacy grep (7656119, kdpenland): none in the step 1 diffs

### Step 2 · 2026-10-11 · 8329cb9
- built (cc095a8, one builder subagent; the orchestrator reviewed, tested and committed): the arena phase (EventScheduler "arena", SpawnTracker.ArenaTick/ArenaWorld, Logic ArenaPhase), the "arena: check failing" health entry, EventRuntime's ArenaEnded at every end, the spider-swarm template, SwarmDependencyFailureTests, TemplateLibraryTests Swarm, dependencySuites.swarm, the swarm.vrs and swarm-timing.vrs scenarios; review fixes 0865d32; Sessions 1-2 records, the D19 control row and the session scenarios 8329cb9
- found while building: four test files of the template count and tools/preflight.ps1 (ArenaTick as a mutating entry point) were not in Paths walked; the Sessions 1-2 tooling (vrclient.py's launch wait, five scenarios and swarm-chase.vrs, NyarDevTools rebuilt and redeployed, marks.json). All declared in Paths walked and logged as notes (they change no item)
- compile: Release 0 Warning(s), 0 Error(s); tests: 2768 passed (2744 before the step)
- preflight: PREFLIGHT OK, "secrets: none (1908 files scanned, 1863 index blobs)"; -DependencySuite swarm → "dependency suite: swarm 1/1 (arena-read)"; -Paths -DeclaredOf swarm → every changed path declared, the only remaining problem being "leftover temp nyar-sw1-logs; leftover temp nyar-sw2-logs" (the session copies, kept as evidence until step 3)
- planted faults: ArenaRead_fails_when_a_phase_fault_is_silent (review F5) planted and caught; the builder's step 2 tests each run once against their fault (logged in the build)
- /code-review (fresh subagent, read-only, on 0b05cc9..cc095a8): VERDICT READY; F1, F2, F5, F7 fixed in 0865d32; F3, F4, F6 advisory, logged as deferred notes
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)
- session 1 log check: 0 unhandled, 586 nyar lines, 0 orphan errors, 0 unity errors (the three boots together): boot 1 "log check: 0 unhandled, 214 nyar lines, 0 orphan errors, 0 unity errors", boot 2 "0 unhandled, 171 nyar lines, 0 orphan errors, 0 unity errors", boot 3 "0 unhandled, 201 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"
- session 1 snapshot: "snapshot restored; hashes equal (sw1, …)"
- session 2 log check: 0 unhandled, 182 nyar lines, 0 orphan errors, 0 unity errors
- session 2 snapshot: "snapshot restored; hashes equal (sw2, …)"
- in-game: Session 1 (three boots) and Session 2 (the D15 chase probe), docs/features/SWARM.md › Test results; every [Error]/[Warning] line read in both logs of every boot: 0 [Error], the known warnings only
- dod status: D3, D4, D7, D11, D13, D19 pass and are checked (17/25); D16 and D17 have their Session 1 evidence logged for the owner's acceptance (rubric 3); D15's chase, return and combat parts and D18 wait on the owner's run
- privacy grep (7656119[0-9]{10}): none in the tree or in the unpushed commits

### Step 3 · 2026-10-11 · c06a928
- built: the rollback drill's three newer-key reasons and pair-arena, pair-winner, pair-growth (083d191, one builder subagent; its planted fault, growth dropped from the accepted reasons, gave "drill selftest: 22/23", restored by copy → 23/23); the review fixes 493964d (the arena return drops only player targets, and drops them before the move); A1 and the Rollout phrases a7ddeb8; the six surfaces, CLAUDE.md's status line, SWARM.md's Status and ControlCases D19-D23 rows in the release commit c06a928
- found while building: A1 (the gate's repo part meets the v0.9.0 tag's own record-test defect), recorded before D20 is checked, re-reviewed (Review 3 READY, scoped to A1)
- compile: `dotnet build -c Release --no-incremental` 0 Warning(s), 0 Error(s); tests: 2768 passed
- preflight: every check passes but "release tags: 11/12 (v0.10.0 not pushed)" (until the push); "secrets: none (1919 files scanned, 1873 index blobs)"; `pwsh tools/rollback-drill.ps1 -SelfTest` → "drill selftest: 23/23"
- /code-review (fresh subagent, read-only, on 53222e8..ac2dd1f, the drill and the surfaces): VERDICT READY, 0 blocker, 0 major; six minor/advisory wording findings applied
- Codex verdict: NOT READY (5 blocker/major), the child's one cross-inspection (`codex exec -s read-only -c features.experimental_windows_sandbox=true`, prompt by stdin, v0.9.0..ac2dd1f without tools/vrclient/**). F2 (DropTargets removed NPC and unreadable entries) and F3 (targets dropped after the move, so a throw left a moved unit still targeting) were fixed in 493964d; F6 (the surfaces claimed the untested return) was applied; F1 (AggroConsumer.AggroTarget not cleared) was declined, since no reference mod writes it and D18's "no unit yo-yos" judges it in game; F4 (the conflict set) was rejected by Review 2 F1's settled decision; F5 (the drill matches disabled ids without their reasons) is older drill behaviour outside this diff, advisory
- tcli build: kdpen-Nyarlathotep-0.10.0.zip (440731 bytes), built at the release commit c06a928 with a clean tree (`dotnet build -c Release --no-incremental`, 0 warnings, 0 errors; the DLL also deployed to the stopped dev server) with the tcli token variable set to a sentinel, log build/tcli-build.log; icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (750080 bytes), CHANGELOG.md, LICENSE
- zip sha256: kdpen-Nyarlathotep-0.10.0.zip B1BBCC41D276089C914CF57B636FB7C28A96E7F004C308401C60D084C7B04FE1
- tag: v0.10.0 (annotated) at c06a928, local; `pwsh tools/rollback-gate.ps1 -From v0.9.0 -To v0.10.0 -Plan swarm` → "rollback gate: 3/4, failed: repo" (A1): repo ran with -BeforePush and failed only "Nyarlathotep.Tests.ControlCaseTests.ControlCases_passes_plan_and_table_agree" ("pending wave-sets D25 outlived step 3's post-audit", D26, D27 likewise); drill "rollback drill: pass" (v0.9.0 initialized on v0.10.0's files, "7 valid, 0 disabled" both, hashes equal after the restore); snapshot "snapshot selftest: 6/6"; routes "rollback routes: swarm 5/5"
- repo revert by hand (A1): a disposable worktree of v0.10.0 at %TEMP%\nyar-rollback-manual-swarm; `git revert --no-edit v0.9.0..v0.10.0` exit 0; `git diff --quiet v0.9.0` → equal; Release build 0 Warning(s), 0 Error(s); `dotnet test` "Failed: 1, Passed: 2620, Total: 2621", the one failure "Nyarlathotep.Tests.ControlCaseTests.ControlCases_passes_plan_and_table_agree" with the same three pending wave-sets messages; v0.9.0's own `pwsh tools/preflight.ps1` → its only failure "release tags: 11/12 (v0.10.0 not on its release commit)", the unpushed-tag check (run before the release commit was rebuilt; the gate's -BeforePush run accepts it as "not pushed"); the worktree removed
- release: waiting for the owner's OK to push main and v0.10.0 and create the GitHub pre-release (D21); D15 to D18 wait on the owner's spider-swarm run
- privacy grep (7656119[0-9]{10}): none in the tree or in `git log -p origin/main..main`
- dod status: D20 and D23 pass at c06a928; D21 and D22 wait on the push and the release records
- session 3 (the owner's run, 2026-10-12, docs/features/SWARM.md › Session 3): "[nyar] arena sw-arena: returned 4, farthest 46 m"; spider-swarm "all waves defeated (5 of 5 waves)"; the owner's verdict "It went well" passes D15 to D18 (recorded as the owner's, who kd)
- session 3 log check: 0 unhandled, 1774 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons; every [Error]/[Warning] line read in both logs: 0 [Error], the known warnings plus the sw-arena stop
- session 3 snapshot: "snapshot restored; hashes equal (sw3, …)"
- dod status after session 3: 23/25; D21 and D22 wait on the push

