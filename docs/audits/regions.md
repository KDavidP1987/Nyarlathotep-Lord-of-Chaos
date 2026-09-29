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

### Step 3 · 2026-09-28 · 85f6080 (chore(release): v0.6.0)
- rg1: before it, `-LogCheck` on the last logs clean; `pwsh tools/dev-snapshot.ps1 -Save rg1`; the 0.6.0 build deployed (version pair uncommitted, A33, A35); boot line "regions: 10 polygons, 10 regions (…); 0 untagged, 0 dropped", equal to Nyarlathotep.Tests/Fixtures/region-index-size.txt (10), so no fixture change (A31)
- Session 1 (owner, 22:10–23:23): record in docs/features/REGIONS.md › Test results › Session 1; steps 3, 4, 5, 7 and 11 (chat-only replies) confirmed by the owner on 2026-09-28, the rest read from the log; steps 12–13 could not pass as written (no ordinary Undead in the Cursed Forest) → A39 (discovered, ~D12), shown instead by rg-bandit-fw in Farbane ("sweep 200 applied, 138 skipped (region 136 vblood 2)", bandit rows "carrier rg-bandit-fw", "carrier none" after the stop)
- session 1 log check: 0 unhandled, 296 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
- session 1 logs read (both, copied to the session scratchpad before the stop, after AutoSave_1765): BepInEx warnings only the known kinds (Il2CppInterop Class::Init; Beelzebub's two TUNE lines; ours "event example-empowerment: pillar empowerment takes an Empower action", once per reload) and the session's own stop warnings ("event rg-ambush stopped: 6 units queued, 0 spawns cancelled", "empower rg-vengeance: 328 carriers queued for removal (stopped)", "empower rg-undead: 0 carriers queued …", "empower rg-bandit-fw: 200 carriers queued …"); no [Error]; Unity log 0 exceptions
- tick timing: every average under 5 ms; sweep minutes avg 3.678/2.540/1.390 ms, max 77.0/89.9/54.3 ms
- snapshot restored; hashes equal (rg1, C:\Users\KDPen\AppData\Local\Temp\nyar-snap-rg1 deleted); the 0.6.0 build redeployed (0 warnings)
- six surfaces: csproj Version and thunderstore.toml versionNumber 0.6.0 (set before rg1, A33, committed with the rest, A35); both changelogs carry a 0.6.0 entry; both READMEs at 0.6.0 with regional scope, `.nyar region list|here`, api 5 and the 0.5.x rollback sentence (D16); compile 0 Warning(s), 0 Error(s); tests 1842 passed; PREFLIGHT OK ("release tags: 7/7", "wire contract: 11 tags, 11 api commands, all documented (api 5)")
- /code-review (fresh subagent, read-only) on the uncommitted diff: every behaviour claim checked against the code (scope syntax `Name,Name`, counts, codes, wire keys, admin-only and public commands, the 0.5.2 rollback behaviour through OnlyKeys and AllowedPlaceholders); F1 (medium) steps 3, 4, 5, 7, 11 unconfirmed → the owner confirmed each (recorded); F2 (low) "refused in Dunley" → tied to step 5's confirmed reading; F3 (low) thunderstore.toml description does not mention regions → not changed (242/250 characters; the README and changelog carry it)
- Codex verdict: REVISE (round 1) — F1 (blocking) the audit, changelog and session record disagreed on the unconfirmed chat-only steps → owner confirmation recorded in all three; F2 (advisory) A39 overstated unit_index as placement evidence and the region skip as proof of absence → A39, the record and the changelog reworded to the observed condition; kind and layer judged honest
- Codex verdict: READY (round 2) — no findings
- privacy grep (7656119, kdpenland): none in the added lines of the step 3 diff
- tcli build: kdpen-Nyarlathotep-0.6.0.zip (338440 bytes), built in the repository at the release commit 85f6080 with a clean tree (`dotnet build -c Release --no-incremental`, 0 warnings, 0 errors; the DLL also deployed to the stopped dev server) with icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (468480 bytes), CHANGELOG.md, LICENSE
- zip sha256: kdpen-Nyarlathotep-0.6.0.zip A8F37F0C2FEF42DFCE32597304BEC14E7370C35A27752AF6E8865FABBBB4FC2E
- tag: v0.6.0 annotated at 85f6080 (chore(release): v0.6.0)
- Rollout › Rollback worded to the gate's route phrases ("after data is written", "withdrawn by retitling"), committed at 0661e19; the first gate run failed only on them ("rollback routes: regions 3/5")
- rollback gate before the push: `pwsh tools/rollback-gate.ps1 -From v0.5.2 -To v0.6.0 -Plan regions` → "rollback gate: 4/4" (repository drill with -BeforePush; N-1 boot drill "events.json: v0.6.0 '6 valid, 0 disabled', v0.5.2 '6 valid, 0 disabled'", "boot v0.6.0 (seed): log check: … regions 10 polygons", v0.5.2 initialized on v0.6.0's files; snapshot selftest 6/6; "rollback routes: regions 5/5")
- privacy grep before the push (`git grep -n -E` for the SteamID prefix and the owner's mail name): only lines quoting the pattern
- release: main (0661e19) and v0.6.0 pushed; GitHub pre-release https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.6.0 with the zip, created once (exit 0); no tcli publish (the owner publishes)
- release verify: `pwsh tools/release-verify.ps1 -Tag v0.6.0 -Asset kdpen-Nyarlathotep-0.6.0.zip` → "release verify: hashes equal"; the v0.6.0 remote tag and release added to tools/paths-manifest.txt (`remote-tag:`, `remote-release:`)
