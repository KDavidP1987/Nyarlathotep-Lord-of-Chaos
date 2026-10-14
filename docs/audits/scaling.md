# Audit — scaling

## Pre-audit
### Step 1 · 2026-10-14 · 5b6ad38 (branch scaling)
- git status: clean at 5b6ad38 before the step began (the plan's start lines, the dev tool's .devgear, the spike scenario and docs/features/SCALING.md then added)
- compile: 0 errors, 0 warnings (`dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`)
- preflight: exit 0, PREFLIGHT OK
- dod status: 0/27 verified
- feature doc read: docs/features/SCALING.md (new this step; Open questions: the gear rounding, which the spike answers)
- baseline boot (in-game steps): dev server booted the deployed DLL (0.12.0+ unit-watch code, md5 6345be3f…, no mod code changed since 0f90351) with NyarDevTools (.devgear); "push: ready"; LogOutput.log 0 [Error lines at boot; dev snapshot sc0 saved before

## Post-audit
### Step 1 · 2026-10-14 · (this commit, branch scaling)
- compile / preflight: no mod code changed (mod 0 errors, 0 warnings); NyarDevTools builds 0 errors (dev server only, never shipped); preflight OK; -Paths flags only this session's temp folders (nyar-sc0-logs and older), left for release-time cleanup
- /code-review: the diff is the dev-only `.devgear` (DevGear.cs, Plugin.cs's loaded line), scaling-spike.vrs, docs/features/SCALING.md, three pointer lines and the plan's Log; read in full: admin-only, no names logged, native arrays disposed in finally, every read guarded after Codex F2. No other findings.
- Codex verdict: REVISE (first pass) — D19(d) timed only the Equipment read; unit-loop reads unguarded; SCALING.md's cap and absolute-level wording unclear; the screen comparison is by eye. Dispositions: F1 fixed (the whole player read timed, re-run 14/14), F2 fixed (guards), F3 fixed (wording), F4 accepted as designed (D19 is manual; the owner accepts the screenshots). Re-check: APPROVED — all three resolved, no new finding.
- in-game: scaling-spike.vrs SCENARIO PASS 14/14 twice (184257 and 185455); Equipment sum = screen Gear Level 7 of 7 readings; gear verdict floor (S-6 kept). -LogCheck 0 unhandled both boots; only known warnings. Recorded in docs/features/SCALING.md › Test results › Spike.
- dod status: 0/27 verified; D19 waits on the owner's acceptance (manual, rubric 3)
