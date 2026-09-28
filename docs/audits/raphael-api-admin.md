# Audit — raphael-api-admin

Build plan steps 1–3 of docs/dod/raphael-api-admin.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.5.1.

## Pre-audit
### Step 1 · 2026-09-28 · d30f985
- pre-child: walkable-spawns released 0.5.1 (tag v0.5.1 at 437dbb4, GitHub pre-release, release-verify "hashes equal"); its D1-D12 pass and its close waits on the owner's confirmation of A11 (defect); the precondition of this plan holds and S-9's base v0.5.1 stands
- git status: clean at d30f985 (the start commit: A1-A12, the feature doc, childDocs, snapshotSessions, dataTables and the data-inventory rows)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 1387 passed
- preflight: PREFLIGHT OK ("data inventory: 34 entries; 54/54 globs and files, 60/60 plan rows")
- dod status: raphael-api-admin 0/16 verified (just started); review subagent (A1-A12 are Review 4's own advisories and hunted scenarios)
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0
- feature doc read: docs/features/RAPHAEL_API_ADMIN.md, created by the start commit (Status: in build; no open questions); plan D1, D2, D11, Business rules 3, Interfaces, Failure & observability, Build plan step 1, Rollout › Paths walked; Commands/{Event,Template,Pillar,Spawn}Commands.cs, Services/{EventRuntime,EventStore,PillarSwitches,TemplateLibrary,Gateway}.cs, Logic/{Precedence,EventCatalog,DefinitionEditor,ActionGateway,Idempotency,Engine,EventAdmin,Authoring,Pillars,AdminLines,Wire,DataStore}.cs; contract §4 and §10
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 3)
- found on the way, recorded before the code that depends on it:
  - A13 (discovered, ~D2, layer 4.1): six refusals of the D1 paths had no row in Business rules 3.
  - A14 (discovered, layer 5.1): the start flow needs a `Definitions` read before the log line; OpPurgeCounts is read inside Run<T>.

## Post-audit
