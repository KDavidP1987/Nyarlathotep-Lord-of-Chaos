# Audit — undead-hordes

## Pre-audit
### Step 1 · 2026-10-17 · 3e22e87
- git status: clean at 3e22e87 (the plan, its three reviews and its start committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK, "release tags: 16/16"
- dod status: 0/32 verified (just started)
- feature doc read: docs/features/UNDEAD_HORDES.md created in this step; docs/features/APOCALYPSE.md (the march, hotspots, resume) and Logic/Limits.cs (the ceilings) read
- baseline boot: the dev server's last session (login-notice Session 2, the 0.13.1 build) `-LogCheck` "0 unhandled, 95 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; snapshot ln2 restored "hashes equal"; Session 0 boots this step's build after a fresh snapshot

### Step 2 · 2026-10-17 · 074a3a0
- git status: clean at 074a3a0 (step 1, its evidence and A2's confirmation committed and pushed; HEAD equals origin/main)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK, "release tags: 16/16"
- dod status: 4/33 verified (D1-D4)
- feature doc read: docs/features/UNDEAD_HORDES.md (Status, Load test, Hostility; no open question); step 2 is Logic only, so no baseline boot

### Step 3 · 2026-10-17 · c737599 (written up at the post-audit)
- git status: clean at c737599 (step 2 and its evidence committed; not yet pushed)
- compile: step 2's post-audit build at the same tree, 0 Warning(s), 0 Error(s); preflight OK
- dod status: 8/33 verified (D1-D5, D7, D12, D21)
- feature doc read: docs/features/UNDEAD_HORDES.md (Status, Design summary; no open question); docs/features/APOCALYPSE.md (hotspots, roamers, resume); step 3 is Logic only, so no baseline boot

### Step 4 · 2026-10-17 · 640dc05
- git status: clean at 640dc05 (steps 2 and 3 with their evidence and the handoff committed; not yet pushed)
- compile: step 3's post-audit build at the same tree, 0 Warning(s), 0 Error(s); tests 4034/4034; preflight OK
- dod status: 20/33 verified (D1-D5, D7-D17, D20-D22, D33)
- feature doc read: docs/features/UNDEAD_HORDES.md (Status, Design, Load test, Hostility); the baseline boot runs at the start of Session 1, before any scenario
- baseline boot: 5ccf276's Release DLL on the dev server, no client: "Nyarlathotep initialized via GameDataInitializedPatch (attempt #1)", events 17 valid, templates 36/36, boot marker sweep 0 found, boot carrier sweep 0 found; warnings only the known example-empowerment line, Beelzebub's TUNE line and HarmonyX's type-load lines; the Unity log's 226 missing-prefab lines are the game's own

### Step 5 · 2026-10-18 · the step 4 evidence commit
- git status: clean after the step 4 evidence commit (release surfaces staged in the release commit)
- compile: `dotnet build Nyarlathotep.sln -c Release` 0 Warning(s), 0 Error(s) at 00874af; tests 4046/4046
- dod status: 29/33 verified; open D27 (owner), D28-D31 (this step)
- feature doc read: docs/features/UNDEAD_HORDES.md (Status, Owner's check, Test results); the dev server is stopped, as the rollback drill needs

## Post-audit
### Step 1 · 2026-10-17 · the step's commit follows
- compile: `dotnet build Nyarlathotep.sln -c Release` 0 Warning(s), 0 Error(s); tests Passed! 3868/3868 with D2, D3 and D4 moved from ControlCases.Pending to their rows
- preflight: PREFLIGHT OK; `-SelfTest` 40/40 checks, 7/7 external selftests; `python tools/vrclient/load-ladder.py selftest` "load ladder selftest: pass"
- records: `-SessionsOf undead-hordes` "session logs: undead-hordes 1/1 checked after A10" (tools/preflight.ps1's A10 cutoff defaulted to 0, which left a Session 0 uncounted; it is -1 now, and every other plan's count is unchanged); `-Paths -DeclaredOf undead-hordes` fails only on "leftover temp nyar-udh-logs" (the Session 0 log copies, declared in tools/data-inventory.json and tools/paths-manifest.txt, kept until the release) and on the audit file git cannot date before this commit
- /code-review: the builder read the diff (Logic/Limits.cs, the literals moved to Limits in Logic/Validation.cs, Logic/CommandArgs.cs and Logic/EventAdmin.cs, the dev tools); no finding beyond Codex's
- Codex verdict: REVISE (F1 the boss inside a step's n leaves the mid share one short, kept as method and pinned by the selftest; F2 the report accepted phases out of order, fixed; F3 D4's test trusted the printed ceiling, fixed; F2 and F3 planted red once); re-check of the fixes READY
- in-game: Session 0 (hostility probe and the load ladder) recorded in docs/features/UNDEAD_HORDES.md › Test results and › Load test; snapshot udh0 restored "hashes equal"
- session 0 log check: 0 unhandled, 423 nyar lines, 0 orphan errors, 0 unity errors (`-LogCheck` before the stop; copies in %TEMP%\nyar-udh-logs\s0a, s0b, s0c); every [Warning read: Il2CppInterop, HarmonyX, Beelzebub TUNE, the dev example-empowerment line at each reload, the runs' purge lines; no [Error; the 226 game missing-prefab warnings as at every boot
- owner decision: the ladder's ceiling 250 told to the owner, who kept MaxTrackedUnits at 500 (plan A3, design §9 D62)
- dod status: D1-D4 have evidence after this commit

### Step 2 · 2026-10-17 · the step's commit follows
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests Passed! 3936/3936 with D5, D6, D7, D12, D16, D18, D19 and D21 moved from ControlCases.Pending to their rows (D16, D18 and D19 gain the step 3 types' cases there)
- preflight: PREFLIGHT OK
- planted faults: one per control (D5, D6 twice, D7, D12, D16, D18 twice, D19 twice, D21) and one per review fix (F2, F3, review 1, 2, 4, 5, 7, 8), each red and restored (the plan's Log)
- /code-review: 10 findings; 1-8 fixed (a destination event's cut kept for `event info`, a hold on unread ground adds no stall time, the spread cut's asked count, every spread cut logged, `action.destination none`, radius and name need a destination (A4), the round robin by unit key, a new run's round robins cleared); 9 (the 0.13 clamp line beside the new capped line) and 10 (PerKill as the draw's minimum) kept as method
- Codex verdict: REVISE (F1 `destination here` echoes the admin's position: rejected by reference to raphael-api-admin D15, pinned by a test; F2 the hop and roam bound per group, F3 the stall bound per wave: fixed, one budget a tick); re-check REVISE (a destination wave's own cut not named; the start logged a forecast the rise logged again): fixed; final re-check READY
- deferred to step 4 (Services): the destination wave's march in Services/WaveAction.cs, HuntAction's bounded aggro seeding
- in-game: none; step 2 is Logic only (the march is wired and run in game at step 4)
- dod status: D5, D6, D7, D12, D21 have evidence after this commit; D16, D18 and D19 complete with step 3's types

### Step 3 · 2026-10-17 · the step's commit follows
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests Passed! 4034/4034 with D8, D9, D10, D11, D13, D14, D15, D17, D20 and D33 moved from ControlCases.Pending to their rows, and D16, D18, D19 and D22 completed with the new types' cases
- preflight: PREFLIGHT OK
- planted faults: one per control (plant-uh3.py, 11) and one per review fix (plant-uh3f.py, plant-uh3g.py, plant-uh3h.py), each red and restored except code review 1's old "rose" condition, which the spawned-unit guard also holds (the plan's Log)
- /code-review: 10 findings; 1-9 fixed (a win needs a spawned unit, the infestation retry, "the horde is gathering", roles over every living unit, `event info` for an Admin start, a boss roll at its most, the bosses queued, a mode by name only, one JSON path writer); 10 (Living scans the ledger) kept as method
- Codex verdict: REVISE (F1 the hunter share drifted after deaths, fixed; F2 stall lines and an Admin start's hotspot named a point, fixed by A5; F3 the infestation template's numbers, Design text reconciled); re-check REVISE (the start's top-up not a try; fronts marching on the admin printed their point): fixed; second re-check REVISE (a resume not a try): fixed; final re-check READY
- deferred to step 4 (Services): the admin origin to ApocalypseHost.Prepare, the mode to ApocalypseOutcome.Line, the Infestation start banner, the destination march in WaveAction, HuntAction's bounded aggro seeding
- in-game: none; step 3 is Logic only (both types run in game at step 4)
- dod status: D8, D9, D10, D11, D13, D14, D15, D16, D17, D20, D22 and D33 have evidence after this commit

### Step 4 · 2026-10-18 · the step 4 evidence commit
- compile: `dotnet build Nyarlathotep.sln -c Release` 0 Warning(s), 0 Error(s); tests Passed! 4046/4046 (the review fixes' two tests in the D6 and D9 rows)
- preflight: PREFLIGHT OK
- planted faults: one per control and one per fix (the plan's Log; plant-uh4r.py for the review fixes, 4 plants, all red, restored)
- /code-review: a fresh read-only subagent on 972bb7c..eb41867, VERDICT: REVISE; (1) the moves tally carried across runs and (2) a flickering watch could hold a front forever: both fixed at 00874af with tests; (3) the host's tally wiring covered
- Codex verdict: REVISE (4a: HuntAction's per-unit searches, fixed at a8d08dd; 4b: F1 a seen front left a unit behind, F2 the ceiling report never checked the purge, F3 any event's resume counted: fixed, with selftest cases); the review fix 00874af is cross-inspected with the release diff at step 5
- in-game: Session 1, D23-D26 in docs/features/UNDEAD_HORDES.md › Test results; D23 rerun on 00874af 42/42; D26 on 00874af across a restart
- session 1 log check: 0 unhandled, 432 nyar lines, 0 orphan errors, 0 unity errors
- dod status: D6, D11, D18, D19, D23, D24, D25, D26 have evidence; D27 waits on the owner

### Step 5 · 2026-10-18 · f0f9df0 (the release commit)
- Codex verdict: REVISE on eb41867..the release tree (M a Log note gave the character's death spot as coordinates, removed; L the package README's per-tick range 1-20, fixed to 1-40); re-check READY; MetClock judged to match D6
- compile: `dotnet build Nyarlathotep.sln -c Release` 0 Warning(s), 0 Error(s); tests Passed! 4046/4046 with D23-D26 and D28-D31 moved from ControlCases.Pending to their rows
- preflight: PREFLIGHT OK but "release tags: 16/17 (v0.14.0 not pushed)" before the push; "version: 0.14.0 (csproj = thunderstore.toml)"; "secrets: none (2209 files scanned, 2143 index blobs)"
- records: `-AuditOf undead-hordes` with this entry; `-SessionsOf undead-hordes` "session logs: undead-hordes 2/2 checked after A10"; `-Paths -DeclaredOf undead-hordes` "paths: 2165 walked, all in manifest; declared: 156/156 in undead-hordes" after Rollout › Paths walked gained the files steps 2-4 also wrote and the tool scratch line; the step 4 log copies removed from %TEMP%
- snapshot: uh4 restored "snapshot restored; hashes equal (uh4"; the dev cfg then set to MaxTrackedUnits 500 and MaxUnitsPerWave 500 only, for the owner's D27
- release: v0.14.0 (f0f9df0) tagged locally; `pwsh tools/rollback-gate.ps1 -From v0.13.1 -To v0.14.0 -Plan undead-hordes` "rollback gate: 4/4" (repo "rollback: clean", drill "rollback drill: pass", "snapshot selftest: 6/6", "rollback routes: undead-hordes 5/5")
- tcli build: kdpen-Nyarlathotep-0.14.0.zip (642075 bytes), built at the release commit f0f9df0 with a clean tree, the tcli token variable set to a sentinel (absent from build/tcli-build.log and the zip); icon.png, README.md, manifest.json (version_number 0.14.0), BepInEx/plugins/Nyarlathotep.dll (1339392 bytes), CHANGELOG.md, LICENSE; no platform id or mail name in the zip; the local dev server runs this zip's DLL
- zip sha256: kdpen-Nyarlathotep-0.14.0.zip 4704AC2C99934816CDFF956FDE35CB9858586686BE28C029F58277E688AF47C1
- privacy grep (7656119, kdpenland) before the push: only lines quoting the pattern and the tests' fake ids (76561190000000…); no real id anywhere in the tree
- dod status: D1-D26 and D28, D31 have evidence; D29 and D30 after the push; D27 waits on the owner
