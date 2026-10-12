# Audit — hunting-grounds

## Pre-audit
### Step 1 · 2026-10-12 · e11f66c
- git status: clean at e11f66c (the plan approved); then `dod start` (status in-progress, the build view)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK, "secrets: none (1924 files scanned, 1877 index blobs)"
- dod status: 0/32 verified (just started)
- feature doc read: docs/features/HUNTING_GROUNDS.md created in this step (Status: in progress); docs/features/SWARM.md (released 0.10.0) and docs/features/REGIONS.md read

## Post-audit
### Step 1 · 2026-10-12 · e11f66c + working tree
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s) (the mod is unchanged in this step)
- preflight: PREFLIGHT OK, "secrets: none (1928 files scanned, 1877 index blobs)"
- review of the diff (dev-only tools/vrclient/NyarDevTools/Plugin.cs: `.devhop`, `.devunits`, `.devjump`): the NativeArray and query are disposed in finally, components are written only behind Has checks, and the commands are admin-only; the `.devjump` header line now names `clearPath`. Advisory: these commands move every living unit of the prefab in range, native ones included, which is acceptable for a dev tool that is never shipped (D30's zip holds only the mod)
- Codex: once per child before the release (Halloween track, design §9 D34), not per step
- in game: Session 0 (D20), five boots of the 0.10.0 DLL, logs backed up per boot to %TEMP%\nyar-hg0-logs\boot1..boot5; every `pwsh tools/preflight.ps1 -LogCheck` "0 unhandled … 0 orphan errors, 0 unity errors, regions 10 polygons" (boot5: 80 nyar lines); [Warning] lines only the known Il2CppInterop, Beelzebub TUNE and example-empowerment ones plus the purge line; NyarDev.log only the 226 missing-prefab lines (the baseline)
- snapshot: `dev-snapshot -Save hg0` before the deploy; `-Restore` "snapshot restored; hashes equal (hg0 …)"
- result: "mover verdict: go — hidden jumps" (docs/features/HUNTING_GROUNDS.md › Test results › Spike); D20 waits for the owner's acceptance
