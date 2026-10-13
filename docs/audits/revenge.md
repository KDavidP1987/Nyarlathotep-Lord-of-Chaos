# Audit — revenge

## Pre-audit
### Step 1 · 2026-10-13 · c5d1738
- git status: clean at c5d1738 (the plan started with A1 and A2; reviews 4 and 5 READY)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Error(s); tests 2935/2935
- preflight: PREFLIGHT OK, "secrets: none (1958 files scanned, 1908 index blobs)"
- dod status: 0/32 verified (just started)
- feature doc read: docs/features/REVENGE.md created in this step (Status: in progress); docs/features/HUNTING_GROUNDS.md (released 0.11.0) and docs/features/WAVE_SETS.md read

### Step 2 · 2026-10-13 · 8a43069
- git status: clean at 8a43069 (step 1 post-audit committed)
- compile: 0 Warning(s), 0 Error(s); tests 3102/3102
- preflight: PREFLIGHT OK, "secrets: none (1972 files scanned, 1922 index blobs)"
- dod status: 17/32 verified (D1-D9, D11-D13, D15, D17, D28-D30); D31 waits on TemplateLibraryTests.Bosses
- feature doc read: docs/features/REVENGE.md (Status: step 1 built; open questions: idle squads when the focus leaves Hunt range, scaling and replacements after Halloween)
- baseline boot: the 8a43069 Release DLL on the dev server inside snapshot rv1 (`dev-snapshot.ps1 -Save rv1`, 33 files); logs copied to %TEMP%\nyar-rv1-logs first; "events: reloaded: 12 valid, 1 disabled" (example-empowerment, the known one), "templates: 12/12 valid", "triggers: all hooks available"; -LogCheck PREFLIGHT OK
- timing baseline (Review 3 F2): hunting-grounds-timing.vrs on the 8a43069 DLL (hg-timing, 24 tracked spiders for 25 min) SCENARIO PASS 12/12; `preflight -TimingSpan <log copy> -Windows 10 -MinTracked 10` → "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks" (window averages 0.27-3.75 ms). Runs 1 and 2 stopped at "the HUD never appeared": Chaos lay dead at the pond from the owner's test; respawned at the coffin, run 3 passed

### Step 3 · 2026-10-13 · 12ee095
- git status: clean at 12ee095 (Session 1 and the step 2 post-audit committed)
- compile: 0 Warning(s), 0 Error(s); tests 3132/3132
- preflight: PREFLIGHT OK, "secrets: none (1987 files scanned, 1931 index blobs)", "release tags: 13/13", wire api 7
- dod status: 22/32 verified; D18-D21, D32 manual (owner acceptance), D22 the owner's second-player session, D24-D27 this step
- feature doc read: docs/features/REVENGE.md (Status: Session 1 passed; open questions: idle squads beyond Hunt range, scaling and replacements after Halloween)
- in-game baseline: none needed for a release step beyond the rollback gate's own boots; the dev server is stopped, snapshot rv1 restored

## Post-audit
### Step 1 · 2026-10-13 · revenge half: 1c9127c (builder subagent, on 0aacdba)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --no-build` Passed! 3056/3056 (2935 before, 121 new)
- preflight: PREFLIGHT OK, "secrets: none (1911 files scanned, 1905 index blobs)", "data inventory: 54 entries; 59/59 globs and files, 167/167 plan rows", "wire contract: … (api 7)"
- method: one Logic seam per service hook of step 2 (RevengeScan on PlayerTriggerFeed.Scan, Died's tell and pillarOn, RevengeRule.StartLines/StartPrivate/EndPrivate/Classify/AdminFocus, PlayerPick.ChooseFocus with WaveFacts.FocusOnly, HuntPlan.TargetsFor/FocusKey keyed by the existing HuntCandidate key, so no new HuntCandidate field and no amendment); a set reply shows trigger.warnAt as "a,b" or "none" (Logic/DefinitionEditor.cs)
- existing tests changed: the api-6 assertions of ContractDocTests (.cs, .Api4, .Automation, .WaveSets, .Swarm, .HuntingGrounds) and WireFormatTests move to api 7 or "at least 6" (D15); TriggerActivationTests and DependencyFailureTests gain the Revenge trigger type (each grows per type); PrivacyTests.Cases gains the four revenge builders (D9); CommandArgTests.Library SettableFields_passes_every_table_name (59 → 62 fields); ControlCaseTests.ControlCases_passes_plan_and_table_agree (the revenge controls)
- Codex: once per child before the release (Halloween track, design §9 D34), not per step
- planted faults: one per new control (D1-D9, D11-D13, D15, D17), each failed with its fault and passed after the copy back:
  - D1 MinHeat 3 → 2 → EventValidationTests.Revenge_fails_when_a_bound_is_off_by_one failed
  - D2 the fanOut refusal removed → EventValidationTests.RevengeAction_fails_when_a_combination_loads failed
  - D3 a V Blood kill counts 1 → RevengeTests.Heat_fails_when_a_vblood_kill_adds_other_than_vblood_heat failed
  - D4 no re-arm on a fall → RevengeTests.Warnings_fails_when_a_threshold_does_not_rearm failed
  - D5 the held-reason memory removed → RevengeTests.Gate_fails_when_the_hold_line_repeats failed
  - D6 ChooseFocus picks among every player → RevengeTests.FocusOnly_fails_when_a_wave_is_centred_on_another_player failed
  - D7 the start banner also queued for a Revenge definition → RevengeTests.Alert_passes_alert_and_private_start_line failed
  - D8 a purge sends the end line → RevengeTests.EndPaths_fails_when_a_silent_path_sends_a_line failed
  - D9 the faction text not cleaned → PrivacyTests.Revenge_fails_when_a_builder_line_exceeds_480_bytes_or_holds_angle failed
  - D11 the editor's revenge window floor 60 → 10 → CommandArgTests.Revenge_fails_when_window_bounds_cross_trigger_types failed
  - D12 "vblood +v" dropped from the trigger line → AuthoringTests.Revenge_fails_when_a_field_is_missing_from_the_line failed
  - D13 AdminFocus returns null → RevengeTests.AdminStart_fails_when_the_start_has_no_focus failed
  - D15 the wire value revenge → factionkills → ContractDocTests.Revenge_passes_api_7_and_the_value failed
  - D17 the pending entry of D10 removed → ControlCaseTests.ControlCases_passes_plan_and_table_agree failed
- carried to step 2: the services (DeathEventPatch's V Blood flag and private send, the 5 s scan with RevengeScan, the start path's alert, private lines and admin focus, the wave pick and hunt seeding through PlayerPick.ChooseFocus and HuntPlan.TargetsFor, the end paths' lines, the counts in `event info`); RevengeDependencyFailureTests joins ControlCases.NewClasses with its class

### Step 1 · 2026-10-13 · merged tree: 7031eb2 (boss half b81599b + revenge half 1c9127c)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: Passed! 3102/3102 (3056 on the revenge half, 46 from the boss half and the merge's control rows)
- preflight: PREFLIGHT OK, "secrets: none (1972 files scanned, 1922 index blobs)", wire api 7
- merge: conflicts only in this file and docs/features/REVENGE.md (both kept); ControlCases gains the D28-D31 rows and BossWaveTests in NewClasses; SettableFields 63; the builder touched no Services or Patches
- review of the diff: the boss half's 10 planted faults and the revenge half's 14 each caught and restored (Log notes); Codex once before the release (design §9 D34)
- dod status: D1-D9, D11-D13, D15, D17, D28-D30 pass at 7031eb2; D31 waits on TemplateLibraryTests.Bosses (step 2)
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)

### Step 2 · 2026-10-13 · b766a73 + fix 3e6ff70
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: Passed! 3132/3132
- preflight: PREFLIGHT OK; `-DependencySuite revenge` → "dependency suite: revenge 2/2 (heat-read, private-send)"
- planted faults: D10 heat read 1/6 and private send 1/4, D14 1/13, D31 3/7, D16 the suite's FAILED line; each caught and restored (Log notes)
- review of the diff: fresh read-only subagent, VERDICT: ISSUES (2), both low: a fault end wrote no revenge end line (fixed in 3e6ff70, logged and silent to the player); the boot's restart line reads the current catalogue (accepted, REVENGE.md › Restart line). Codex once before the release (design §9 D34)
- in game: Session 1 (REVENGE.md › Test results) revenge 39/39, revenge-vblood 20/20, revenge-restart 8/8 + boot lines, revenge-boot2 4/4, revenge-timing 20/20 with the D23 span 10/10 under 5 ms, boss-wave 13/13, vblood-player-death 9/9; every [Warning]/[Error] in both logs read (state lines only); snapshot rv1 restored
- dod status: D10, D14, D16, D23, D31 pass; D18-D21 and D32 manual, waiting on the owner's acceptance; D22 waits on a second player; D24-D27 are step 3
- Codex verdict: deferred to step 3 (the child's single cross-inspection, design §9 D34)

### Step 3 · 2026-10-13 · 8ee59c5 (chore(release): v0.12.0, tag v0.12.0 local)
- compile: `dotnet build Nyarlathotep.sln -c Release --no-incremental` at 8ee59c5 with a clean tree: 0 Warning(s), 0 Error(s) (the DLL also deployed to the stopped dev server)
- tests: Passed! 3134/3134 (3132 at step 2; the two Codex fixes added RevengeTests.Heat_fails_when_a_row_queue_grows_past_max_heat and Heat_fails_when_pillar_off_keeps_heat_until_the_next_scan)
- preflight: PREFLIGHT OK, "secrets: none (1987 files scanned, 1937 index blobs)", "release tags: 13/13" before the tag
- Codex verdict: CHANGES REQUESTED (2) — the child's single cross-inspection (`git diff v0.11.0` plus the surfaces, tools/vrclient/** left out): F1 medium (a heat row's point queue unbounded within a long window), F2 medium (a pillar turned off kept its Revenge heat until the next 5 s scan, D8); both fixed before 8ee59c5 (Log)
- Codex verdict: PASS — re-check of the two fixes (`git diff` of Logic, Services and the tests), no findings
- review of the diff: fresh read-only subagent, VERDICT: ISSUES (4), all low docs: F1-F3 fixed before 8ee59c5, F4 kept (Log)
- planted faults: the queue cap and the PruneRevenge call commented out → both new tests failed (3132/3134), restored by copy
- tooling: the rollback gate's repo part could not revert a range holding the step 1 merge 7031eb2 → tools/repo-rollback-drill.ps1 reverts the first-parent line (a merge with -m 1), selftest "repo rollback selftest: 6/6"; its routes part read D24's old range from the frozen Baseline → RollbackRoutes leaves `## Baseline` out (fixture good-baseline) (Log)
- rollback: `rollback-drill.ps1 -SelfTest` "drill selftest: 29/29"; `repo-rollback-drill.ps1 -SelfTest` "repo rollback selftest: 6/6"; `rollback-gate.ps1 -From v0.11.0 -To v0.12.0 -Plan revenge` "rollback gate: 4/4" (repo with -BeforePush, the tag not yet on origin; its boots each 0 unhandled, 0 orphan errors, 0 unity errors)
- records: `-AuditOf revenge` "audit steps: revenge 3/3 pre, 3/3 post, 3/3 Codex verdicts"; `-SessionsOf revenge` "session logs: revenge 1/1 checked; snapshots 1/1 from session 1"; `-Paths -DeclaredOf revenge` "paths: 1959 walked, all in manifest; declared: 132/132 in revenge"
- tcli build: kdpen-Nyarlathotep-0.12.0.zip (495333 bytes), built at the release commit 8ee59c5 with a clean tree, the tcli token variable set to a sentinel (absent from build/tcli-build.log); icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (915968 bytes), CHANGELOG.md, LICENSE; no platform id or mail name in the zip's README or CHANGELOG
- zip sha256: kdpen-Nyarlathotep-0.12.0.zip F793056AD187ABDE7C7822E6FE36C894235EC9F8CC79D54B7A86BCC6DFE30C91
- session 1 log check: 0 unhandled, 133 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
  - session 1's line is its last boot (after the D21 restart, `-LogCheck -ServerPath` over the saved copies); its restart-run boot read 0 unhandled, 2167 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons, and the earlier boots of the session (scout, timing runs) held no frame from our assembly and no [Error]
- push: waits on the owner's OK; then the privacy grep, `git log -p origin/main..main`, HEAD = ls-remote, the pre-release and release-verify
