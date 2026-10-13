# Audit — revenge

## Pre-audit
### Step 1 · 2026-10-13 · c5d1738
- git status: clean at c5d1738 (the plan started with A1 and A2; reviews 4 and 5 READY)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Error(s); tests 2935/2935
- preflight: PREFLIGHT OK, "secrets: none (1958 files scanned, 1908 index blobs)"
- dod status: 0/32 verified (just started)
- feature doc read: docs/features/REVENGE.md created in this step (Status: in progress); docs/features/HUNTING_GROUNDS.md (released 0.11.0) and docs/features/WAVE_SETS.md read

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
