# Audit — regions

Build plan steps 1–3 of docs/dod/regions.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.5.2.

## Pre-audit
### Step 1 · 2026-09-28 · 762f43d
- pre-child: raphael-api-admin closed (16/16) and 0.5.2 released (tag v0.5.2 at 32cc082, GitHub pre-release, release-verify "hashes equal"); the precondition of this plan holds and S-9's base v0.5.2 stands
- git status: clean at f08ba51 before the start commit 762f43d (start, A1-A8); this entry, the feature doc, childDocs, snapshotSessions, dataTables and the data-inventory rows follow in the next commit
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 1702 passed
- preflight: PREFLIGHT OK
- dod status: regions 0/16 verified (just started); A1-A8 are Review 2's own advisories; A2, A4 and A7 touch gating probes (12.4, 14.4), so a scoped fresh-subagent re-review of A1-A8 runs before step 1's code is committed
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0
- feature doc read: docs/features/REGIONS.md, created with this entry (Status: in build; no open questions); plan D1-D3, D7, Business rules, Interfaces, Design, Failure & observability, Build plan step 1, A1-A8; code: Logic/{Model,Validation,Empowerment,Engine,Spawning,DefinitionEditor,EventAdmin,AdminFlows,Outcome,Messages,ApiLines,Wire}.cs, Services/{EventStore,HealthMonitor,TriggerBus,EmpowerAction,AdminOps}.cs, Core.cs, Patches/DeathEventPatch.cs; KindredCommands Services/RegionService.cs (how WorldRegionPolygon, PolygonBounds and WorldRegionPolygonVertex.VertexPos are read; re-implemented, not copied)
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 3)
