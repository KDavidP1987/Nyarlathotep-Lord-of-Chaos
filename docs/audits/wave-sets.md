# Audit — wave-sets

Build plan steps 1–3 of docs/dod/wave-sets.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; the child's single Codex cross-inspection (design §9 D34) is in step 3's post-audit, and every
other post-audit carries a "Codex verdict:" line pointing to it. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.8.0.

## Pre-audit
### Step 1 · 2026-10-04 · 9a402e3
- pre-child: automation closed and 0.8.0 released; the plan approved after Review 1 (READY, F1-F10 advisory, all applied) and started at 9a402e3
- git status: clean at 9a402e3
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2446 passed
- preflight: PREFLIGHT OK
- dod status: wave-sets 0/27 verified (just started); `dod-index.mjs --check wave-sets` → problems 0, warnings 0
- feature doc read: docs/features/WAVE_SETS.md is created by this step; docs/features/EVENT_SPAWNS.md and AUTOMATION.md read for the wave gate, the roll and the fan-out deal this child extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

## Post-audit
