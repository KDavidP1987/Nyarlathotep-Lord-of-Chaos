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

### Step 2 · 2026-09-28 · 8be5926
- git status: clean at 8be5926 (step 1 code e512382 and its post-audit)
- compile: 0 Warning(s), 0 Error(s) (step 1 post-audit, unchanged tree); tests: 1770 passed
- preflight: PREFLIGHT OK
- dod status: regions 2/16 verified (D1, D3); review READY after Review 6; amendments A1-A27, of which step 2 builds A1, A4-A6, A8-A12, A14, A18-A23, A26-A27
- feature doc read: docs/features/REGIONS.md (Status: step 1 built; no open questions); plan D4-D6, D8-D11, D14, Business rules, Paths walked step 2
- in-game baseline: not needed (step 2 has no in-game part; Session 1 is step 3)

### Step 3 · 2026-09-28 · d062a68
- git status: clean at d062a68 (step 2 code a85852e and its post-audit)
- compile: 0 Warning(s), 0 Error(s); tests: 1842 passed
- preflight: PREFLIGHT OK (before the 0.6.0 version pair, A35)
- dod status: regions 12/16 verified (D1, D3-D11, D14, D15); open D2, D12, D13, D16, all step 3; review READY after Review 9; amendments A1-A38
- feature doc read: docs/features/REGIONS.md (Status: step 2 built and post-audited; no open questions); plan D2, D12, D13, D15, D16, Build plan step 3, Rollout, A31, A33, A35, A36
- in-game baseline: the rg1 boot of the 0.6.0 build is the baseline before Session 1 (its "regions:" line against the fixture, A31); rg0 was step 2's

## Post-audit
### Step 1 · 2026-09-28 · e512382
- built: Logic/Regions.cs (RegionNames: the 10 WorldRegionType names except None and Other, A16; Scope, Global by default, a read-only copy; IRegionCatalog and NoRegions; RegionIndex, box then even-odd test with the half-open rule, degenerate polygons dropped and untagged ones left out, both counted, box and polygon tests counted for D15; RegionState, the build that never throws, its boot line (A24), health entry and reload retry); `scope` on every trigger type and on SpawnWaves and Empower actions (Logic/Model.cs, Logic/Validation.cs: "Global" or 1-RegionNames.Count names, case-insensitive, "unknown region <name>", "regions unavailable" before "region <name> is not on the map", A3, A15, A25); Services/RegionMap.cs (read-only WorldRegionPolygon query, second in Core.TryInitialize before EventStore, retried by `.nyar event reload`); HealthMonitor entry; Dependency.Regions with its policy row (A17); tests RegionTests (36), EventValidationTests Scope_ (23), DependencyFailureTests Regions_ (9)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 1770 passed; `--filter Regions_` → Passed! 9/9
- preflight: PREFLIGHT OK; `-Paths -DeclaredOf regions` → "paths: 1274 walked, all in manifest; declared: 42/42 in regions"
- not foreseen by the plan, recorded before building: A16 (WorldRegionType also holds Other), A17 (Dependency.Regions and Logic/Dependency.cs); the plan's scoped re-reviews ran alongside (Review 3 READY, A9-A15; Review 4 REVISE, A18-A20; Review 5 READY, A21-A25; Review 6 READY, A26-A27), none changing step 1's code. Scope is validated but not yet enforced; enforcement is step 2 (D4-D6)
- /code-review (fresh subagent, read-only) on the uncommitted diff: the even-odd test, Scope equality, ParseScope's order, counting, RegionMap's axes (vertex .x/.y against bounds .x/.z) and the health and policy rows correct; F1 (low) EventStore.Initialize's boot load went through Reload(), which now retries the region build, so a failed build ran and warned twice at boot → fixed (the boot load calls Editor.Reload directly; Regions_are_built_before_the_definitions_are_applied asserts Initialize neither calls Reload nor RegionMap.Retry)
- Codex verdict: REVISE (round 1) — F1 (blocking) a throwing info or warn sink could escape RegionState.Build and reset a built index → fixed (the index is set before logging; every log call wrapped; Regions_a_throwing_log_sink_never_throws_and_keeps_the_index); F2 GetBuffer read-write → fixed (GetBuffer<WorldRegionPolygonVertex>(entity, true)); F3 Scope kept the caller's list → fixed (copied); F4 no throwing-sink test → fixed with F1
- Codex verdict: READY (round 2) — advisories: Scope.Regions castable back to an array → fixed (ReadOnlyCollection, mutation test); RegionMap's read-only access and disposal untested → fixed (Regions_the_map_is_read_only_and_disposes_its_query, a source test)
- Codex verdict: READY (round 3) — advisory: RegionNames.All and NotRegions castable to a mutable array → fixed (Array.AsReadOnly, mutation test); after it: compile 0/0, 1770 passed, PREFLIGHT OK
- in-game: none in step 1 (Session 1 is step 3)

### Step 2 · 2026-09-28 · a85852e
- built: regional triggers (the kill position read only for a scoped definition, A38; a player in the regions for the other types and for admin starts; ScopeGate, TriggerRouter, TriggerBus, DeathEventPatch), the "region" empowerment skip, wave points and Point/Admin locations kept in scope, `.nyar region list|here` (RegionCommands, RegionLines.HereReply), scope in `event info|list|set` and `{region}`, wire api 5 (`api regions`, `region=` on def/event rows and event-start/event-end pushes) with the contract, handoff and design docs, ControlCases rows for the regions plan (tests renamed to the three forms), -LogCheck's regions assertion (last boot only) with six LogCheck fixtures, the fixture battery running every good-* fixture, RegionTests Cost over the boot's 10-polygon count
- not foreseen by the plan, recorded before building: A28-A33, A35-A36 (discovered) and A34, A37, A38 (defects: the code was wrong, the plan right); plan Review 7 REVISE → A32, Review 8 REVISE → A33, Review 9 READY
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 1842 passed
- preflight: PREFLIGHT OK ("commands: 15 admin-only, 6 public (allow-listed)"; "wire contract: 11 tags, 11 api commands, all documented (api 5); command table equal"); -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, vcf dependency)"; -SelfTest → "selftest: 36/36 checks, 7/7 external selftests"; `-Paths -DeclaredOf regions` → "paths: 1292 walked, all in manifest; declared: 106/106 in regions"
- /code-review (forked, read-only) on the uncommitted diff: F1 (low) the names-differ check read the whole log, so an earlier boot's warning failed a clean later one → fixed (A37: the last boot's lines only; LogCheck/good-regions-restart); wire line sizes, kill routing, wave points, scope fields and carriers checked and correct; a draft finding on `region here` withdrawn as already fixed (A34)
- Codex verdict: REVISE (round 1) — F1 (blocking) EventRuntime.OnlinePositions read the ECS unguarded → fixed (A34, PositionReader.Collect); F2 (blocking) `region here` read the sender before checking availability, unguarded → fixed (A34, RegionLines.HereReply); F3 (advisory) contract change-log rows out of order → fixed; F4 (advisory) no seam test → EngineTests.ScopeGate_fails_when_position_source_throws
- Codex verdict: READY (round 2) — advisories F1-F2: tests did not prove production routes through the helpers → RegionTests.Guarded_reads_route_through_the_helpers
- Codex verdict: REVISE (round 3) — F1 (blocking) every V Blood kill read the victim's position though D4 says a Global trigger reads none → fixed (A38, TriggerRouter.KillFor; TriggerActivationTests.Region_empty_global_kill_reads_no_position)
- Codex verdict: READY (round 4) — no findings
- baseline-boot record confirmed by hand (D13, A33): the "- baseline boot rg0 log check:" line and "snapshot restored; hashes equal (rg0" are below
- baseline boot rg0 (A31, A32; no in-game steps, so no session): before it, `-LogCheck` on the last logs → "log check: 0 unhandled, 8 nyar lines, 0 orphan errors, 0 unity errors"; `pwsh tools/dev-snapshot.ps1 -Save rg0` (28 files); the step 2 build deployed (0.5.2 version stamp, sha256 edd6290d1e35…); booted the dev world; boot line "[nyar] regions: 10 polygons, 10 regions (StartCave, FarbaneWoods, DunleyFarmlands, CursedForest, HallowedMountains, SilverlightHills, Gloomrot_South, Gloomrot_North, RuinsOfMortium, Strongblade); 0 untagged, 0 dropped", no names-differ warning; the count went to Nyarlathotep.Tests/Fixtures/region-index-size.txt (10); stopped after AutoSave_1726
- baseline boot rg0 log check: 0 unhandled, 11 nyar lines, 0 orphan errors, 0 unity errors (the build still stamps 0.5.2, so the regions assertion did not apply; the line above was read by hand)
  - both logs read (copied to %TEMP%\nyar-s-rg0-logs, deleted after): BepInEx warnings only the known four kinds (Il2CppInterop Class::Init; Beelzebub's two TUNE lines; ours "event example-empowerment: pillar empowerment takes an Empower action", the dev world's own events.json); the server log's 224 "PrefabLookupMap.TryGet … is in an unknown state" warnings at save load, the known set; no [Error]
  - snapshot restored; hashes equal (rg0, the snapshot folder deleted); the step 2 build redeployed after the restore
