# Audit — login-notice

## Pre-audit
### Step 1 · 2026-10-17 · 1435352
- git status: clean at 1435352 (the plan, its review and its start committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK
- dod status: 0/12 verified (just started)
- feature doc read: docs/features/LOGIN_NOTICE.md created in this step; no announcements feature doc exists; Services/Announcer.cs and Logic/AnnouncerCore.cs (the login gate, the degraded notice) read
- baseline boot: not an in-game step

## Post-audit
### Step 1 · 2026-10-17 · 3f7803b
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ --no-incremental` 0 Warning(s), 0 Error(s); tests Passed! 3854/3854
- preflight: PREFLIGHT OK, "announcement defaults: all off but LoginEventNotice (6 switches)", "secrets: none (2144 files scanned, 2080 index blobs)"; `-SelfTest` 40/40 checks, 7/7 external selftests, after two 0.13.0 leftovers were fixed (the drill row expected 32/32 against 37/37; the MutatingFloor good fixture predated unit watch's [Mutating] methods)
- planted faults: D1, D2, D3, D4, D5 (twice), D6 each red once and restored (plan Log)
- /code-review: the builder read the diff (Logic/Messages LoginNotice, Logic/AnnouncerCore LoginNotices, Settings, preflight); no finding. The child's one Codex cross-inspection and /code-review of the whole diff run at step 3 (Halloween track, CLAUDE.md)
- in-game: none (step 2)
- dod status: D1, D3, D5, D6, D12 have evidence after this commit; D2 and D4 wait for step 2's service wiring
