# Audit — automation

Build plan steps 1–4 of docs/dod/automation.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.7.0.

## Pre-audit
### Step 1 · 2026-09-29 · 37eb8a1
- pre-child: event-spawns closed and 0.7.0 released; the plan approved after Review 4 (READY) and started at 37eb8a1 (A1, A2)
- git status: clean at 37eb8a1
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2173 passed
- preflight: PREFLIGHT OK
- dod status: automation 0/31 verified (just started)
- feature doc read: docs/features/AUTOMATION.md is created by this step (the plan's Build plan step 1); docs/features/EVENT_SPAWNS.md read for the AroundPlayer pick and the wave gate the fan-out extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

## Post-audit
