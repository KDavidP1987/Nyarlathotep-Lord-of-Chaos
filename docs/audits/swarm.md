# Audit — swarm

Build plan steps 1–3 of docs/dod/swarm.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one under
"## Post-audit"; the child's single Codex cross-inspection (design §9 D34) is in step 3's post-audit, and every other
post-audit carries a "Codex verdict:" line pointing to it. Session log checks are lines "- session <n> log check: …".
Rollback base: v0.9.0.

## Pre-audit
### Step 1 · 2026-10-11 · b22f135
- pre-child: wave-sets closed (27/27) and 0.9.0 released (tag v0.9.0, GitHub pre-release); the plan approved after Review 2 (READY, F1-F5 deferred to the Log) and started at b22f135
- git status: clean at b22f135 before the start line, docs/features/SWARM.md and this record
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2621 passed
- preflight: PREFLIGHT OK
- dod status: swarm 0/25 verified (just started); `dod-index.mjs --check swarm` → problems 0, warnings 1 (prose: MaxConcurrentEvents named in Use cases without an item, an existing cap this child does not change)
- feature doc read: docs/features/SWARM.md is created by this step; docs/features/WAVE_SETS.md read for the wave list, the clear, the victory and the scoreboard this child extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)
