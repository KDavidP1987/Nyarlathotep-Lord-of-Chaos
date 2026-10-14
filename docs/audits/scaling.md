# Audit — scaling

## Pre-audit
### Step 1 · 2026-10-14 · 5b6ad38 (branch scaling)
- git status: clean at 5b6ad38 before the step began (the plan's start lines, the dev tool's .devgear, the spike scenario and docs/features/SCALING.md then added)
- compile: 0 errors, 0 warnings (`dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`)
- preflight: exit 0, PREFLIGHT OK
- dod status: 0/27 verified
- feature doc read: docs/features/SCALING.md (new this step; Open questions: the gear rounding, which the spike answers)
- baseline boot (in-game steps): dev server booted the deployed DLL (0.12.0+ unit-watch code, md5 6345be3f…, no mod code changed since 0f90351) with NyarDevTools (.devgear); "push: ready"; LogOutput.log 0 [Error lines at boot; dev snapshot sc0 saved before

### Step 2 · 2026-10-14 · ff1a1b7 (branch scaling)
- git status: clean (step 1 committed ff1a1b7)
- compile: 0 errors, 0 warnings; tests 3560/3560
- preflight: exit 0, PREFLIGHT OK
- dod status: 0/27 verified (D19 waits on the owner)
- feature doc read: docs/features/SCALING.md (gear verdict floor; no blocking open question)
- baseline boot: not an in-game step (Logic, commands, docs and tests only)

## Post-audit
### Step 1 · 2026-10-14 · (this commit, branch scaling)
- compile / preflight: no mod code changed (mod 0 errors, 0 warnings); NyarDevTools builds 0 errors (dev server only, never shipped); preflight OK; -Paths flags only this session's temp folders (nyar-sc0-logs and older), left for release-time cleanup
- /code-review: the diff is the dev-only `.devgear` (DevGear.cs, Plugin.cs's loaded line), scaling-spike.vrs, docs/features/SCALING.md, three pointer lines and the plan's Log; read in full: admin-only, no names logged, native arrays disposed in finally, every read guarded after Codex F2. No other findings.
- Codex verdict: REVISE (first pass) — D19(d) timed only the Equipment read; unit-loop reads unguarded; SCALING.md's cap and absolute-level wording unclear; the screen comparison is by eye. Dispositions: F1 fixed (the whole player read timed, re-run 14/14), F2 fixed (guards), F3 fixed (wording), F4 accepted as designed (D19 is manual; the owner accepts the screenshots). Re-check: APPROVED — all three resolved, no new finding.
- in-game: scaling-spike.vrs SCENARIO PASS 14/14 twice (184257 and 185455); Equipment sum = screen Gear Level 7 of 7 readings; gear verdict floor (S-6 kept). -LogCheck 0 unhandled both boots; only known warnings. Recorded in docs/features/SCALING.md › Test results › Spike.
- dod status: 0/27 verified; D19 waits on the owner's acceptance (manual, rubric 3)

### Step 2 · 2026-10-14 · (this commit, branch scaling)
- compile / preflight: 0 errors, 0 warnings; tests 3683/3683 (123 new); PREFLIGHT OK; -AuthSuite pass
- built by a builder subagent, reviewed by the orchestrator: Logic/Scaling.cs read in full and every Logic diff read (Spawning, Apocalypse, ApocalypseHost, SpawnLedger, Model, Validation, CommandArgs, EventAdmin); no-block paths default to the old behaviour; bosses never multiplied; V Blood keeps its own level; Review 1 F1, F4, F6, F8 honoured
- /code-review: no findings beyond Codex's
- Codex verdict: REVISE (first pass) — F1 High: unchecked int arithmetic in Scaling.cs could wrap past the caps; F2 Medium: D15's end-path test does not drive real end paths. Dispositions: F1 fixed (saturating arithmetic, Scaling.MaxExtra, test Caps_fails_when_huge_inputs_wrap_past_the_caps, planted red); F2 recorded in the plan's Log as owed by step 3, where the end paths are wired. Re-check: APPROVED — F1 resolved, F2's deferral acceptable, no new finding
- plan: amendment A1 (discovered, ~D16: the wire's reason is a fixed word, so the twin answers `code=invalid arg=field reason=field` / `code=badarg arg=value`); method notes for the undeclared files (SpawnLedger.cs, ApocalypseHost.cs) and the added D1 reason; 16 planted faults logged (docs/audits/scaling-step2-planted.txt)
- in-game: none (step 4)
- dod status: 0/27 verified; D1-D11 and D14-D17 have their tests, checked at status time; D19 waits on the owner
