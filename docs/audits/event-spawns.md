# Audit — event-spawns

Build plan steps 1–4 of docs/dod/event-spawns.md. Each step has one "### Step <n>" entry under "## Pre-audit" and
one under "### Step 2 · 2026-09-29 · 9580152
- git status: clean at 9580152 (step 1 code c5e8f33 and its post-audit)
- compile: 0 Warning(s), 0 Error(s); tests: 2133 passed
- preflight: PREFLIGHT OK
- dod status: event-spawns 6/24 verified (D6, D8, D9, D18, D29, D30; D11 and D32 test parts pass, manual parts in Session 1); review subagent after Review 28; amendments A1-A57
- feature doc read: docs/features/EVENT_SPAWNS.md (Status: designed, not started; Spike S2 go); plan D10, D11, D13, D16, D17, D21, D22, D31, D32, Build plan step 2
- in-game baseline: boot of the deployed 0.6.0 DLL, no player; -LogCheck → "log check: 0 unhandled, 9 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; its warnings are Il2CppInterop's substitute signature, Beelzebub's two TUNE lines, and the owner's example-empowerment event ("pillar empowerment takes an Empower action"), all seen in earlier boots

## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.6.0.

## Pre-audit
### Step 1 · 2026-09-29 · 0bb3a57
- pre-child: regions closed (16/16) and 0.6.0 released (tag v0.6.0 at 85f6080, GitHub pre-release, release-verify "hashes equal"); walkable-spawns closed; S-5 and S-14 hold and the rollback base is v0.6.0
- git status: clean at bbbd65d before the start commit 0bb3a57 (start, A1-A9); this record follows in the next commit
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 1842 passed
- preflight: PREFLIGHT OK
- dod status: event-spawns 0/24 verified (just started); A1-A7 are Review 11's advisories, A8 owner decision 1A, A9 the api number; A1, A2, A3, A4 and A9 touch gating probes (6.2, 10.1, 12.4), so a scoped fresh-subagent re-review (Review 12) runs before step 1's code is committed
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0
- feature doc read: docs/features/EVENT_SPAWNS.md (Status: designed, not started); plan D6, D8, D9, D16-D18, D20, D27, D29, D30, D33, D34, Build plan step 1, A1-A9; code: Logic/{Model,Validation,Spawning,CommandArgs,Authoring,Regions}.cs, Services/SpawnTracker.cs, tools/preflight.ps1
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

## Post-audit
### Step 1 · 2026-09-29 · c5e8f33
- built: the SpawnWaves schema and validation (per-unit chance, modifiers level/levelDelta/maxHealth/power/moveSpeed/attackSpeed, loot, behaviour Hunt, AroundPlayer location, allowTerritory; D6), the `.nyar event set` keys and their refusals (D18, AdminFlows, DefinitionEditor, CommandArgs), the pure planners in Logic/Spawning.cs (unit chance rolls, WaveGate, WaveLifecycle, TerritoryMaps and HuntSeeds end paths, PlayerPick with the action scope, D8, D9, D16, D17, D29, D33), SpawnLedger and SpawnHealth entries (D11, D30), EventLines' new info lines (D32), and the preflight checks and fixtures: EntityWrites (D34), the gateway check's type-qualified System actor, alias and shadow rules, MutatingFloor (A55), -AuthSuite's command inventory, the Secrets sentinel, TCLI and index rules, the per-slug -DependencySuite with its floor (A52), -Paths dataTests; tools/data-inventory.json runtime rows; ControlCases rows
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 2133 passed
- preflight: PREFLIGHT OK ("mutating floor: 43/43 listed methods [Mutating] in 12 services, none unlisted"; "entity writes: only dispatched services (68 sites, 8 [Mutating] methods)"; "secrets: none (1733 files scanned, 1691 index blobs)")
- -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"
- -SelfTest → "selftest: 39/39 checks, 7/7 external selftests (3 fixtures each, 221 extra bad fixtures; secrets: none (1733 files scanned, 1691 index blobs); index probe: a staged tcli token read and a staged tools/ credential read fail on the real tree)"
- -Paths -DeclaredOf event-spawns → "paths: 1720 walked, all in manifest; declared: 488/488 in event-spawns; data tests 14 passed; plants: 15/15 fail, tempvar 15/15 lines"
- not foreseen by the plan, recorded before building: A48 (.claude/ declared), A49 (DestroyUtility.Destroy in D34), A50 (the refusal argument, the location field name, lowercase booleans), A51 (D18's field is `location`), A52 (the suite floor), A53 (bounded unknown fields and echoed values, defect), A54 (the D22 checks, defect), A55 (the [Mutating] floor and tuple return types), A56 (the Secrets index rules, defect), A57 (the data-test floor); plan Reviews 22-28 (22 READY A48; 23 READY A49-A52; 24 READY A53-A54; 25 REVISE, 26 READY A55; 27 READY A56; 28 READY A57)
- /code-review (fresh subagent, read-only) on the uncommitted diff: F1 the suite floor → A52; F2 DestroyUtility.Destroy outside D34 → A49; F3 the refusal named "field" for a bad chance → fixed (A50, AuthoringTests.Spawns_fails_when_refusal_names_wrong_argument); F4 levelDelta formatting culture → fixed (FormattableString.Invariant); F5 chance rounding → fixed (0.######); F6 `location aroundplayer` recorded as location → fixed (DefinitionEditor); D18's wording → A51; the exact-case test then found set replies printing True → fixed (A50)
- Codex verdict: REVISE (round 1) — F1 the units info line unbounded → fixed (PackList, "units below"); F2 the System-actor exemption by bare name → fixed (type-qualified, GatewayOnly/bad-systemtype; EventScheduler's own Tick renamed RunPhases); F3 require every tick call to exist → rejected (an authorization control, not a liveness check; the fixture it cites plants something else); F4 the inventory did not check handlers → fixed (AuthSuite/bad-unhandled); F5 UnitSetup's level culture → fixed; F6 the D22 row text → fixed
- Codex verdict: REVISE (round 2) — F1 a using alias could fake an entry point → fixed (GatewayOnly/bad-alias); F2 a string literal could stand in for a handler → fixed (Hide-CsStringsButCases, AuthSuite/bad-literal); F3 the ChatBytes fails-when exercised no production code → fixed (a 480-character behaviour type through the validator)
- Codex verdict: REVISE (round 3) — F1 unknown field names copied whole into chat → fixed (A53, EventValidator.UnknownField); F2 a case label counted without its call → fixed (A54, Get-EventVerbHandler, AuthSuite/bad-gutted)
- Codex verdict: REVISE (round 4) — F1 removing [Mutating] hid a writer from the gateway check → fixed (A55, Test-CheckMutatingFloor; the tuple-returning SpawnTracker writers had never been seen, GatewayOnly/bad-tuple)
- Codex verdict: REVISE (round 5) — F1 the index side of the Secrets check skipped the tcli token rule → fixed (A56, Get-IndexSecretHits, Test-IndexSecretProbe)
- Codex verdict: REVISE (round 6) — F1 the dataTests entry could drop EndPathTests and still pass → fixed (A57, $script:DataTestFloor, Paths/bad-datatests-floor)
- Codex verdict: READY (round 7) — no findings; tests not run in its read-only sandbox (MSBuild temp denied), run here: 2133 passed
- dod status: 6/24 verified (D6, D8, D9, D18, D29, D30; D11 and D32 pass their tests and wait for their Session 1 manual parts; the rest wait for steps 2-4 and Sessions 1-2); --check problems 0
- privacy grep (7656119, kdpenland): only lines quoting the pattern (D31, the plans' "Grep for" lines, earlier audits' grep lines)
- design notes: the tcli build log is build/tcli-build.log; fixture directories that git must not treat as dist/ or build/ are stored as dist.ignored/ and build.ignored/ and renamed by Copy-Fixture; `dataTests` is required only when the plan names it, and Paths/good-datatests exercises the run; EventScheduler's own Tick is RunPhases
- in-game: none in step 1 (Session 1 is step 2)
