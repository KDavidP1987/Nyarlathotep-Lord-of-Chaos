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

## Post-audit
### Step 1 · 2026-10-12 · e11f66c + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s) (the mod is unchanged in this step)
- preflight: PREFLIGHT OK, "secrets: none (1928 files scanned, 1877 index blobs)"
- review of the diff (dev-only tools/vrclient/NyarDevTools/Plugin.cs: `.devhop`, `.devunits`, `.devjump`): the NativeArray and query are disposed in finally, components are written only behind Has checks, and the commands are admin-only; the `.devjump` header line now names `clearPath`. Advisory: these commands move every living unit of the prefab in range, native ones included, which is acceptable for a dev tool that is never shipped (D30's zip holds only the mod)
- Codex: once per child before the release (Halloween track, design §9 D34), not per step
- in game: Session 0 (D20), five boots of the 0.10.0 DLL, logs backed up per boot to %TEMP%\nyar-hg0-logs\boot1..boot5; every `pwsh tools/preflight.ps1 -LogCheck` "0 unhandled … 0 orphan errors, 0 unity errors, regions 10 polygons" (boot5: 80 nyar lines); [Warning] lines only the known Il2CppInterop, Beelzebub TUNE and example-empowerment ones plus the purge line; NyarDev.log only the 226 missing-prefab lines (the baseline)
- snapshot: `dev-snapshot -Save hg0` before the deploy; `-Restore` "snapshot restored; hashes equal (hg0 …)"
- result: "mover verdict: go — hidden jumps" (docs/features/HUNTING_GROUNDS.md › Test results › Spike); D20 waits for the owner's acceptance
### Step 2 · 2026-10-12 · 7297537 + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` Passed! 2888/2888 (2768 before, 120 new)
- preflight: PREFLIGHT OK, "secrets: none (1935 files scanned, 1882 index blobs)"; `-Paths -DeclaredOf hunting-grounds` OK after Paths walked gained five paths (Log note); the Session 0 log copies moved out of %TEMP% (leftover temp nyar-hg0-logs)
- existing tests changed (D11): EventValidationTests.Invalid_actions_disable_the_event (the location rule names Region); CommandArgTests.EventSpawns Spawns_fails_when_value_out_of_range and AuthoringTests.Location_empty_no_character ("location takes here or aroundplayer MIN MAX" → "location takes here, aroundplayer MIN MAX or region"); CommandArgTests.Library SettableFields_passes_every_table_name (58 → 59 fields, action.regionLock); ControlCaseTests.ControlCases_passes_plan_and_table_agree (the hunting-grounds controls)
- review of the diff (orchestrator, Logic/HuntingGrounds.cs read in full and the Logic diff): pure code behind IRegionWorld; per-unit try/catch with one failure line per streak in LockPhase and HuntPhase; at most 20 jumps per tick across events; no jump while a player is within 100 m; final landing 50-60 m; landings at least 50 m from every online player; log lines carry counts and one distance only. The existing services tag only `Hunt` behaviours (Services/WaveAction.cs:95), so a RegionHunt or Region definition does nothing extra until step 3 wires it (it is not released before then). Builder choices accepted: no walk probe → no spot (the wave is skipped, never placed unchecked); the hunt line's counts-only "landed <d> m" (Review 1 F4); "in 1 group"
- planted faults: 14 controls (D1-D5, D7, D8, D10, D12-D15, D17, D19), each failed with its fault and passed after the copy back (Log notes)
- carried to step 3: Services/RegionMap.cs exposes the scope polygons; the tracked-unit record gains the wave group and spawn point (S-14); HuntingGroundsDependencyFailureTests joins ControlCases.NewClasses with its class
