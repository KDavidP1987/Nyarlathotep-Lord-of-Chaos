# Audit — login-notice

## Pre-audit
### Step 1 · 2026-10-17 · 1435352
- git status: clean at 1435352 (the plan, its review and its start committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK
- dod status: 0/12 verified (just started)
- feature doc read: docs/features/LOGIN_NOTICE.md created in this step; no announcements feature doc exists; Services/Announcer.cs and Logic/AnnouncerCore.cs (the login gate, the degraded notice) read
- baseline boot: not an in-game step

### Step 2 · 2026-10-17 · 81fe8ca
- git status: clean at 81fe8ca but for step 2's own Services/Announcer.cs wiring and tools/vrclient/scenarios/login-notice.vrs, written before this entry (the order slipped: this record follows the code, precedes the session)
- compile: 0 Warning(s), 0 Error(s) with the wiring; tests Passed! 3854/3854
- preflight: PREFLIGHT OK (step 1's post-audit, same tree but for the wiring)
- dod status: 5/12 verified (D1, D3, D5, D6, D12)
- feature doc read: docs/features/LOGIN_NOTICE.md (Status: step 1 built; open question: the 10 s delay on a slow remote server)
- baseline boot: the dev server's last boot (the 0.13.0 rollback gate's v0.12.0 boot) `-LogCheck` "0 unhandled, 10 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; every [Warning read: the four baseline kinds (Il2CppInterop, HarmonyX x3) and Beelzebub TUNE, no [Error; logs copied to %TEMP%\nyar-ln-logs\s0-baseline; snapshot ln1 saved (34 files)

### Step 3 · 2026-10-17 · f81022c
- git status: clean at f81022c
- compile: 0 Warning(s), 0 Error(s); tests Passed! 3854/3854 (step 2's post-audit, same code)
- preflight: PREFLIGHT OK (step 2's post-audit)
- dod status: 8/12 verified (D1-D7, D12)
- feature doc read: docs/features/LOGIN_NOTICE.md (Status: steps 1-2 built, Session 1 passed)
- baseline boot: the dev server's last session (Session 1) log check clean; the snapshot restored the 0.13.0 DLL and config

## Post-audit
### Step 1 · 2026-10-17 · 3f7803b
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ --no-incremental` 0 Warning(s), 0 Error(s); tests Passed! 3854/3854
- preflight: PREFLIGHT OK, "announcement defaults: all off but LoginEventNotice (6 switches)", "secrets: none (2144 files scanned, 2080 index blobs)"; `-SelfTest` 40/40 checks, 7/7 external selftests, after two 0.13.0 leftovers were fixed (the drill row expected 32/32 against 37/37; the MutatingFloor good fixture predated unit watch's [Mutating] methods)
- planted faults: D1, D2, D3, D4, D5 (twice), D6 each red once and restored (plan Log)
- /code-review: the builder read the diff (Logic/Messages LoginNotice, Logic/AnnouncerCore LoginNotices, Settings, preflight); no finding. The child's one Codex cross-inspection and /code-review of the whole diff run at step 3 (Halloween track, CLAUDE.md)
- in-game: none (step 2)
- dod status: D1, D3, D5, D6, D12 have evidence after this commit; D2 and D4 wait for step 2's service wiring
- session 1 log check: 0 unhandled, 552 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (`-LogCheck` on the live logs before the next start; copies in %TEMP%\nyar-ln-logs\s1-srv); every [Warning read: the four boot kinds (Il2CppInterop, HarmonyX x3), Beelzebub TUNE, the dev example-empowerment line at each reload, and the runs' own "event ln-test stopped" lines; no [Error

### Step 2 · 2026-10-17 · e4bfe99
- compile: 0 Warning(s), 0 Error(s); tests Passed! 3854/3854 with D7 moved from ControlCases.Pending to its row
- preflight: PREFLIGHT OK, "secrets: none (2145 files scanned, 2086 index blobs)"; `-SessionsOf login-notice` "session logs: login-notice 1/1 checked"
- /code-review: the builder read the diff (Services/Announcer.cs: the LoginNotices instance on the shared private-message streak, Holds, SendLoginNotice; the tooling fixes in tools/vrclient/vrclient.py); no finding. Codex and /code-review of the child run once at step 3
- in-game: Session 1, login-notice.vrs SCENARIO PASS 17/17, recorded in docs/features/LOGIN_NOTICE.md › Test results; snapshot ln1 restored "hashes equal"
- dod status: D2, D4 and D7 have evidence after this commit
