# Audit — horde-pools

## Pre-audit
### Step 1 · 2026-10-24 · 52b35f1
- git status: clean at 52b35f1 (the plan, its two reviews, its approval and its start committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK
- dod status: 0/18 verified (just started)
- feature doc read: docs/features/HORDE_POOLS.md is created in this step; docs/features/UNDEAD_HORDES.md (caps, the march, Test results) and docs/features/APOCALYPSE.md (hotspots, roam, territory) read; step 1 is Logic and Services with no in-game check, so no baseline boot

## Post-audit
### Step 1 · 2026-10-24 · the step's commit follows
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests Passed! 4058/4058 with horde-pools D1-D4 and D10 in their rows and D5-D9, D11, D12, D14-D17 in ControlCases.Pending
- preflight: PREFLIGHT OK
- planted faults: D1 three times (MarchUnits, Units, the start's cut), D2, D3, D4 twice (the chat field, HuntAction's apocalypse branch), D10; each red and restored by copy (the plan's Log)
- /code-review (low): no finding
- Codex verdict: once for the child's diff before its release (Halloween track, design §9 D34), at step 4
- amendment: A1 (discovered, ~D4): chat `action.allowTerritory none` stays refused, as event-spawns D18 decided
- in-game: none; step 1 is Logic and Services, run in game at step 3 (D12)
- dod status: D1-D4 and D10 have evidence after this commit
