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
