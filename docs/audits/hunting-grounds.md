# Audit — hunting-grounds

## Pre-audit
### Step 1 · 2026-10-12 · e11f66c
- git status: clean at e11f66c (the plan approved); then `dod start` (status in-progress, the build view)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK, "secrets: none (1924 files scanned, 1877 index blobs)"
- dod status: 0/32 verified (just started)
- feature doc read: docs/features/HUNTING_GROUNDS.md created in this step (Status: in progress); docs/features/SWARM.md (released 0.10.0) and docs/features/REGIONS.md read

### Step 2 · 2026-10-12 · 7297537
- git status: clean at 7297537 (step 1 committed)
- compile: 0 Warning(s), 0 Error(s) (the step 1 post-audit build, same tree)
- preflight: PREFLIGHT OK, "secrets: none (1928 files scanned, 1877 index blobs)"
- dod status: 0/32 verified; D20 waits for the owner's acceptance (Log note)
- feature doc read: docs/features/HUNTING_GROUNDS.md (Status: step 1 passed, go for hidden jumps; Open questions F5 and pathing)
- built by one builder subagent from the plan; the orchestrator reviews, tests and commits

### Step 3 · 2026-10-12 · HEAD after step 2
- git status: clean after the step 2 commits (2fd1529 and its pass lines)
- compile: 0 Warning(s), 0 Error(s); tests 2888/2888 (step 2 post-audit, same tree)
- preflight: PREFLIGHT OK; `-Paths -DeclaredOf hunting-grounds` OK
- dod status: 7/32 (D1-D3, D10, D14, D15, D17 pass; D20 waits for the owner)
- feature doc read: docs/features/HUNTING_GROUNDS.md (Status step 2 next → step 3; Open questions F5 and pathing)
- baseline boot: the Session 1 boot of this step's Release build is read before its first scenario

- work in progress (step 3, before its post-audit):
  - Session 1 (the border scan, 0.10.0 DLL): log check "0 unhandled, 68 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; [Warning] only the known three; NyarDev.log only the 226 missing-prefab lines; logs copied to the session scratchpad (hg1-logs); "snapshot restored; hashes equal (hg1 …)"
  - code (builder subagent, reviewed by the orchestrator): compile 0/0; tests 2918/2918 (2888 before, 30 new); PREFLIGHT OK; "dependency suite: hunting-grounds 2/2 (region-lock, region-hunt)"; -Paths -DeclaredOf OK after Paths walked gained three paths (Log note). Review: every write goes only to units in SpawnTracker's `_entities` and sits behind Has/Exists checks; Move writes Translation, LastTranslation, PreCombatPosition and SpawnTransform.Position, empties PathBuffer and zeroes Velocity (the Session 0 method); Return drops AggroBuffer entries on players outside the scope regions, with their seed records, before the move; the walk probe is disposed in finally each tick; players are read once per tick, memory only; with no territory map nobody is a target (fail closed), and RegionHunt's NeedsMap builds the map each wave (Services/WaveAction.cs:59). Existing tests changed (D11): TemplateLibraryTests (11 → 12, 11/11 → 12/12, Differences +2 → +3, the fake unit list), TemplateLibraryTests.WaveSets and .Swarm (!= 11 → != 12), TemplateCommandTests (the spawns id list; ValidCount 10 → 11), DependencyFailureTests.Library (12/12, 11/12)
  - planted faults: D6, D9, D16, D18, each caught and restored by copy (Log notes)

### Step 4 · 2026-10-12 · 744b63a
- git status: clean at 744b63a (the step 3 post-audit commit)
- compile: 0 Warning(s), 0 Error(s); tests 2929/2929 (step 3 post-audit, same tree)
- preflight: PREFLIGHT OK; `-Paths -DeclaredOf hunting-grounds` only "leftover temp nyar-snap-hg2" (kept for the owner's run)
- dod status: verified 20/32; D20-D25 wait for the owner's acceptance, D26/D27 for the owner's run, D29-D32 are this step's
- feature doc read: docs/features/HUNTING_GROUNDS.md (Status step 3 done; Session 2 results; Open questions F5 and pathing)
- in game: the rollback gate's drill boots the dev world and refuses while a server runs, so it waits until the owner's D26/D27 run is done and snapshot hg2 is restored

## Post-audit
### Step 1 · 2026-10-12 · e11f66c + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s) (the mod is unchanged in this step)
- preflight: PREFLIGHT OK, "secrets: none (1928 files scanned, 1877 index blobs)"
- review of the diff (dev-only tools/vrclient/NyarDevTools/Plugin.cs: `.devhop`, `.devunits`, `.devjump`): the NativeArray and query are disposed in finally, components are written only behind Has checks, and the commands are admin-only; the `.devjump` header line now names `clearPath`. Advisory: these commands move every living unit of the prefab in range, native ones included, which is acceptable for a dev tool that is never shipped (D30's zip holds only the mod)
- Codex: once per child before the release (Halloween track, design §9 D34), not per step
- Codex verdict: PASS — by the child's single cross-inspection at step 4 (`git diff v0.10.0`, which holds this step's code): FAIL on F1/F2 (step 3 code), fixed in b136d7f, re-check PASS
- in game: Session 0 (D20), five boots of the 0.10.0 DLL, logs backed up per boot to %TEMP%\nyar-hg0-logs\boot1..boot5; every `pwsh tools/preflight.ps1 -LogCheck` "0 unhandled … 0 orphan errors, 0 unity errors, regions 10 polygons" (boot5: 80 nyar lines); [Warning] lines only the known Il2CppInterop, Beelzebub TUNE and example-empowerment ones plus the purge line; NyarDev.log only the 226 missing-prefab lines (the baseline)
- snapshot: `dev-snapshot -Save hg0` before the deploy; `-Restore` "snapshot restored; hashes equal (hg0 …)"
- result: "mover verdict: go — hidden jumps" (docs/features/HUNTING_GROUNDS.md › Test results › Spike); D20 waits for the owner's acceptance
### Step 2 · 2026-10-12 · 7297537 + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` Passed! 2888/2888 (2768 before, 120 new)
- preflight: PREFLIGHT OK, "secrets: none (1935 files scanned, 1882 index blobs)"; `-Paths -DeclaredOf hunting-grounds` OK after Paths walked gained five paths (Log note); the Session 0 log copies moved out of %TEMP% (leftover temp nyar-hg0-logs)
- existing tests changed (D11): EventValidationTests.Invalid_actions_disable_the_event (the location rule names Region); CommandArgTests.EventSpawns Spawns_fails_when_value_out_of_range and AuthoringTests.Location_empty_no_character ("location takes here or aroundplayer MIN MAX" → "location takes here, aroundplayer MIN MAX or region"); CommandArgTests.Library SettableFields_passes_every_table_name (58 → 59 fields, action.regionLock); ControlCaseTests.ControlCases_passes_plan_and_table_agree (the hunting-grounds controls)
- review of the diff (orchestrator, Logic/HuntingGrounds.cs read in full and the Logic diff): pure code behind IRegionWorld; per-unit try/catch with one failure line per streak in LockPhase and HuntPhase; at most 20 jumps per tick across events; no jump while a player is within 100 m; final landing 50-60 m; landings at least 50 m from every online player; log lines carry counts and one distance only. The existing services tag only `Hunt` behaviours (Services/WaveAction.cs:95), so a RegionHunt or Region definition does nothing extra until step 3 wires it (it is not released before then). Builder choices accepted: no walk probe → no spot (the wave is skipped, never placed unchecked); the hunt line's counts-only "landed <d> m" (Review 1 F4); "in 1 group"
- Codex verdict: PASS — by the child's single cross-inspection at step 4 (`git diff v0.10.0`, which holds this step's code): FAIL on F1/F2 (step 3 code), fixed in b136d7f, re-check PASS
- planted faults: 14 controls (D1-D5, D7, D8, D10, D12-D15, D17, D19), each failed with its fault and passed after the copy back (Log notes)
- carried to step 3: Services/RegionMap.cs exposes the scope polygons; the tracked-unit record gains the wave group and spawn point (S-14); HuntingGroundsDependencyFailureTests joins ControlCases.NewClasses with its class
### Step 3 · 2026-10-12 · a902a20 + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` Passed! 2929/2929 (2918 at the step 3 services commit; 11 new for A1-A3, the re-review and the region parts line)
- preflight: PREFLIGHT OK, "secrets: none (1945 files scanned, 1899 index blobs)", "dependency table: … hunting-grounds 2/2 categories"; `-Paths -DeclaredOf hunting-grounds`: only "leftover temp nyar-snap-hg2", the Session 2 snapshot kept for the owner's D26/D27 run (restored after it); EngineTests.cs added to Paths walked (Log note)
- review 1 (fresh read-only subagent, 770fc2f..32d960b): VERDICT REVISE, F1-F3 fixed in 24cd1a7 (Log note)
- review 2 (fresh read-only subagent, `git diff 6d05e56..HEAD -- Nyarlathotep/`, A1-A3 and the region parts line): VERDICT REVISE; F1 (blocks: one event's stuck units could spend the shared cap before another event's movable units) fixed in a902a20 by planning every event's free units first; F2, F3 fixed; F4 tests added; F5 no change (Log note)
- Codex: once per child before the release (Halloween track, design §9 D34), not per step
- Codex verdict: PASS — by the child's single cross-inspection at step 4 (`git diff v0.10.0`, which holds this step's code): FAIL on F1/F2 (step 3 code), fixed in b136d7f, re-check PASS
- amendments: A1 (hillside levels), A2 (ravine crossings), A3 (the walk cap, rotation and the rejoin), each recorded before its code; prediction rate 3 design changes
- planted faults: A1, F1, F2, A2, A3, A3 revised (5 faults), re-review (4 faults), each caught and restored by copy (Log notes)
- in game, Session 2 (vrclient, Release builds, logs copied per boot to the session scratchpad hg2-logs/boot1..boot10): D21 8/8, D22 19/19, D23 12/12, D24 17/17 (last on a902a20, `-151056`), D25 18/18, D28 timing 12/12 (last on a902a20, `-154144`) with "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks"; last boot `-LogCheck` "0 unhandled, 762 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; [Warning] lines only the known Il2CppInterop, Beelzebub TUNE and example-empowerment ones plus the stop and purge lines; NyarDev.log only the 226 missing-prefab lines
- snapshot: `dev-snapshot -Save hg2` before the step 3 deploy; the restore waits for the owner's D26/D27 run on the same dev server (docs/SESSION_HANDOFF.md)
- result: D8, D12, D19, D28 pass at a902a20 or after its code; D20-D25 wait for the owner's acceptance (Log notes), D26/D27 for the owner's run

### Step 4 · 2026-10-13 · 91fe093 (chore(release): v0.11.0, tag v0.11.0 local)
- compile: `dotnet build Nyarlathotep.sln -c Release --no-incremental` at 91fe093 with a clean tree: 0 Warning(s), 0 Error(s) (the DLL also deployed to the stopped dev server)
- tests: Passed! 2935/2935 (2929 at step 3; A4, A5, A6, review 2's F1/F5 and the VBloodKilled fix added cases)
- preflight: PREFLIGHT OK, "release tags: 12/12" before the tag
- Codex verdict: FAIL — the child's single cross-inspection (`git diff v0.10.0` plus the surfaces, tools/vrclient/** left out): F1 major (one event's missing map vetoed another event's targets), F2 (a group could take another group's spot); both fixed in b136d7f (Log)
- Codex verdict: PASS — re-check of b136d7f alone
- Codex verdict: PASS — A4/A5 (`git diff df76a01 ee7b1d2 -- Nyarlathotep/`), no findings
- Codex verdict: PASS — A6 and review 2's F5 (`git diff ee7b1d2 772762a -- Nyarlathotep/`), no findings: each castle guard is exercised by a test that fails when it is removed
- Codex verdict: PASS — the VBloodKilled fix 62ce215; its low finding (a defaulted isPlayer) built in e17c17e
- plan review 2 (fresh read-only subagent, scope A4,A5,A6, prompt 2ef73e0ec8cd): VERDICT READY; F1 and F5 built, F2-F4 deferred to the Log
- amendments: A4 (discovered, seed range 100 m), A5 (discovered, the seeded unit's return point at its player: the spider leash is 45 m), A6 (requested, the owner's castle rule; plan v2)
- in game: the owner's Session 3 run on 772762a — D26 PASS, D27 PASS (who = kd); vrclient on the final build: hunt PASS 18/18, lock PASS 19/19, timing (A5) PASS 12/12, vblood-player-death PASS 8/8; every LogCheck "0 unhandled … 0 orphan errors, 0 unity errors"; every [Error]/[Warning] read in both logs: the known Il2CppInterop, Beelzebub TUNE and example-empowerment warnings, the stop lines, and Unity's LogMissingPrefab stack traces (vanilla and other mods, as in earlier sessions)
- snapshot: "snapshot restored; hashes equal (hg2, …)" after the owner's run
- rollback: `rollback-drill.ps1 -SelfTest` "drill selftest: 26/26"; `rollback-gate.ps1 -From v0.10.0 -To v0.11.0 -Plan hunting-grounds` "rollback gate: 4/4" (repo with -BeforePush, the tag not yet on origin)
- tcli build: kdpen-Nyarlathotep-0.11.0.zip (474936 bytes), built at the release commit 91fe093 with a clean tree, the tcli token variable set to a sentinel (absent from build/tcli-build.log); icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (865792 bytes), CHANGELOG.md, LICENSE; no platform id or mail name in the zip's README or CHANGELOG
- zip sha256: kdpen-Nyarlathotep-0.11.0.zip BBE9C3303E0FF74DEE3941C5D275148EA90F4F04787CB1E9195E24F0421C59EF
- push: waits on the owner's OK; then the privacy grep, `git log -p origin/main..main`, HEAD = ls-remote, the pre-release and release-verify
- session 1 log check: 0 unhandled, 68 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
- session 2 log check: 0 unhandled, 762 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
- session 3 log check: 0 unhandled, 2844 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
  - session 0 (the spike) has no count line: its five boots' checks were recorded as "0 unhandled … 0 orphan errors, 0 unity errors, regions 10 polygons" without the nyar line count; session 2's line is its last boot (boot10, a902a20); session 3's is the owner's boot (boot17, 772762a); the session's other boots (boot11-16, boot18) were each 0 unhandled, 0 orphan errors, 0 unity errors (boot16 72 and boot18 34 nyar lines)
