# Audit — horde-pools

## Pre-audit
### Step 1 · 2026-10-24 · 52b35f1
- git status: clean at 52b35f1 (the plan, its two reviews, its approval and its start committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK
- dod status: 0/18 verified (just started)
- feature doc read: docs/features/HORDE_POOLS.md is created in this step; docs/features/UNDEAD_HORDES.md (caps, the march, Test results) and docs/features/APOCALYPSE.md (hotspots, roam, territory) read; step 1 is Logic and Services with no in-game check, so no baseline boot

### Step 2 · 2026-10-24 · cc46e6f
- git status: clean at cc46e6f (step 1 and its evidence committed)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests Passed! 4058/4058
- preflight: PREFLIGHT OK
- dod status: 5/18 verified (D1-D4, D10)
- feature doc read: docs/features/HORDE_POOLS.md (Status, Design summary's pool library paragraph, Open questions none); Logic/Pools.cs, Logic/FactionGroups.cs, Logic/Validation.cs (ParsePools, PoolOf, SpreadPool, PoolsUsed, ParseEntries), Services/EventStore.cs and Services/HealthMonitor.cs read; step 2 is Logic, Services and Commands with no in-game check, so no baseline boot

### Step 3 · 2026-10-24 · skipped by the owner
- owner, 2026-10-24: skip the in-game test and release now; the dev server's boot is checked for errors instead (step 4). D12 stays open
### Step 4 · 2026-10-24 · 3c73d2d
- git tree: the release edits only (READMEs, changelogs, versions, the handoff); compile 0 Warning(s), 0 Error(s); tests Passed! 4076/4076; preflight OK
- baseline boot: the dev server on the 0.14.0 DLL was last booted 2026-10-18 (its logs backed up before this step's deploy)

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

### Step 2 · 2026-10-24 · the step's commit follows
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests Passed! 4076/4076 with horde-pools D5-D9 and D11 in their rows and D12, D14-D17 in ControlCases.Pending
- preflight: PREFLIGHT OK (after the design doc's `.nyar pool` row and the mutating floor's EventStore.ReloadPools); `-Paths -DeclaredOf horde-pools` declares 75/75, its v0.14.0 tag, release and temp-folder lines wait for step 4
- planted faults: D5, D6 (green on the first plant; the test gained an own-pool roll check, then red), D7, D8, D9, D11; each red and restored by copy, hashes checked (the plan's Log)
- /code-review (low): the first pass read half the diff, no finding; a second read-only pass over the rest found F1: `.nyar pool reload` dropped events.json's refusal and replied "events invalid now: none" from the old, unchecked set. Fixed: the reply says "events.json not reloaded, its events are unchecked: <reason>" (PoolCommandTests)
- Codex verdict: once for the child's diff before its release (Halloween track, design §9 D34), at step 4
- amendment: none; Rollout › Paths walked gains "Step 2 also" (Log note)
- in-game: none; step 2 is Logic, Services and commands, run in game at step 3 (D12)
- dod status: D5-D9 and D11 have evidence after this commit

### Step 4 · 2026-10-24 · the release commit follows
- Codex verdict: REVISE on 52b35f1..3c73d2d (F1: an unforeseen throw inside PoolLibrary.Load left the old library loaded while reload replied "0 loaded"); fixed (Load wraps its body and empties the library on any failure; PoolLibraryTests File); one cross-inspection per child (Halloween track), no re-run
- compile: `dotnet build Nyarlathotep.sln -c Release` 0 Warning(s), 0 Error(s); tests Passed! 4076/4076 with D14-D17 moved from ControlCases.Pending to their rows
- preflight: PREFLIGHT OK, "version: 0.15.0 (csproj = thunderstore.toml)", "changelogs: 0.15.0 in both"
- dev server boot (owner's check in place of the in-game run): Nyarlathotep 0.15.0 loaded; "first run: seeded pools.json with 17 pools", "pools: 17 loaded (17 seeded)", "templates: 36/36 valid", "limits: MaxTrackedUnits 500, MaxUnitsPerWave 500, …" (the dev cfg's values); [Error/[Warning lines 30 in LogOutput.log and 452 in NyarDev.log, the same counts as the 0.14.0 boot (HarmonyX type-load warnings, the game's 226 missing prefabs, KindredCommands and Beelzebub notices); the one nyar warning is the dev server's old example-empowerment ("pillar empowerment takes an Empower action"); stopped with taskkill after 5 s
- READMEs: the package README gains the full reference (Configuration, Setting up events, Unit pools, all collapsible), written from the code; its stale 150 default, "Thirty-four" templates and horde-cap sentences corrected
- in-game: none (owner skipped D12)
