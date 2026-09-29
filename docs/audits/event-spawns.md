# Audit — event-spawns

Build plan steps 1–4 of docs/dod/event-spawns.md. Each step has one "### Step <n>" entry under "## Pre-audit" and
one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.6.0.

## Pre-audit
### Step 1 · 2026-09-29 · 0bb3a57
- pre-child: regions closed (16/16) and 0.6.0 released (tag v0.6.0 at 85f6080, GitHub pre-release, release-verify "hashes equal"); walkable-spawns closed; S-5 and S-14 hold and the rollback base is v0.6.0
- git status: clean at bbbd65d before the start commit 0bb3a57 (start, A1-A9); this record follows in the next commit
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 1842 passed
- preflight: PREFLIGHT OK
- dod status: event-spawns 0/24 verified (just started); A1-A7 are Review 11's advisories, A8 owner decision 1A, A9 the api number; A1, A2, A3, A4 and A9 touch gating probes (6.2, 10.1, 12.4), so a scoped fresh-subagent re-review (Review 12) runs before step 1's code is committed
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0
- feature doc read: docs/features/EVENT_SPAWNS.md (Status: designed, not started); plan D6, D8, D9, D16-D18, D20, D27, D29, D30, D33, D34, Build plan step 1, A1-A9; code: Logic/{Model,Validation,Spawning,CommandArgs,Authoring,Regions}.cs, Services/SpawnTracker.cs, tools/preflight.ps1
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

## Post-audit
