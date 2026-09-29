# Audit — event-spawns

Build plan steps 1–4 of docs/dod/event-spawns.md. Each step has one "### Step <n>" entry under "## Pre-audit" and
one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
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

### Step 2 · 2026-09-29 · 9580152
- git status: clean at 9580152 (step 1 code c5e8f33 and its post-audit)
- compile: 0 Warning(s), 0 Error(s); tests: 2133 passed
- preflight: PREFLIGHT OK
- dod status: event-spawns 6/24 verified (D6, D8, D9, D18, D29, D30; D11 and D32 test parts pass, manual parts in Session 1); review subagent after Review 28; amendments A1-A57
- feature doc read: docs/features/EVENT_SPAWNS.md (Status: designed, not started; Spike S2 go); plan D10, D11, D13, D16, D17, D21, D22, D31, D32, Build plan step 2
- in-game baseline: boot of the deployed 0.6.0 DLL, no player; -LogCheck → "log check: 0 unhandled, 9 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; its warnings are Il2CppInterop's substitute signature, Beelzebub's two TUNE lines, and the owner's example-empowerment event ("pillar empowerment takes an Empower action"), all seen in earlier boots

### Step 3 · 2026-09-29 · b249b9a
- git status: clean at b249b9a (Session 1's evidence)
- compile: 0 Warning(s), 0 Error(s); tests: 2156 passed
- preflight: PREFLIGHT OK; `dod-index.mjs --check event-spawns` → 0 problems
- dod status: event-spawns items D6, D8-D11, D13, D16-D18, D20-D22, D29-D34 per their evidence lines; open for step 3: D19, D23, D24
- feature doc read: docs/features/EVENT_SPAWNS.md (Session 1 parts A-C); plan D19, D23, D24, D32, D33, Build plan step 3; A68-A71 recorded before the code (Reviews 34 REVISE, 35 and 36 READY)
- in-game baseline: Session 1 part C's boot of 0309313 plus A67 (logs checked, no [Error])

### Step 4 · 2026-09-29 · 76638a3
- git status: clean at 76638a3 (Session 2's evidence)
- compile: 0 Warning(s), 0 Error(s); tests: 2173 passed
- preflight: PREFLIGHT OK; `dod-index.mjs --check event-spawns` → 0 problems
- dod status: D19, D23 and D24 pass (Session 2); open for step 4: D25, D26, D27, D31 and the evidence lines of D16, D20-D22, D33, D34
- feature doc read: docs/features/EVENT_SPAWNS.md (Sessions 1 and 2); plan D25-D27, D31, Build plan step 4, Rollout › Rollback; -SessionsOf event-spawns → "1/2 checked (no log check line for session 1)" → A72
- in-game baseline: Session 2's reinstall boot of 332b54f ("log check: 0 unhandled, 15 nyar lines, 0 orphan errors, 0 unity errors")

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

### Step 2 · 2026-09-29 · (this commit)
- built: Services/TerritoryMap.cs (the claimed-territory map per wave, D17), Services/HuntAction.cs (the Hunt seeds and PlayerQuery, D13, D16), WaveAction's gate, AroundPlayer pick, territory-blocked ring points, tuning, loot and Hunt tag (D16, D17, D29), SpawnTracker's loot-aware drop clear with its verbose drops line (D11), the unit-setup failure streak (D21), the DebugUnit readings sp, ms, as and position (D10), EventRuntime's end paths for the spawn state (D33), Engine.WaveSkipped with WavesSkipped and WavesUsed (A59, A62), SpawnsDependencyFailureTests (D21) and the dependencySuites.event-spawns entry, ControlCases' D21 rows (D21 left the pending list)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 2151 passed
- preflight: PREFLIGHT OK ("mutating floor: 44/44 listed methods [Mutating] in 13 services"; "dependency table: event-library 9/9, event-spawns 5/5")
- -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"
- -SelfTest → "selftest: 39/39 checks, 7/7 external selftests (3 fixtures each, 222 extra bad fixtures; ...)"
- -DependencySuite event-spawns → "dependency suite: event-spawns 5/5 (territory, hunt-seed, player-query, unit-recipe, release-tools)"
- -Paths -DeclaredOf event-spawns → "paths: 1739 walked, all in manifest; declared: 517/517 in event-spawns; data tests 14 passed; plants: 15/15 fail, tempvar 15/15 lines"
- not foreseen by the plan, recorded before the commit: A58 (the suite slug unroll, defect), A59 (a skipped wave uses its slot), A60 (the player height and Health, defect), A61 (the purge streaks and the Hunt tick guard, defect), A62 (spawned stays spawned), A63 (bad-floor-spawns), A64 (the Announcer paths); plan Reviews 29 (REVISE, F1 the wave counter), 30 (READY A58-A63), 31 (READY A64)
- /code-review (fresh subagent, read-only) on the uncommitted diff: F1 the AroundPlayer centre at the player's height → rejected (D16 states it; the landing is a Session 1 observation) and "walk h" as a privacy leak → rejected (a height-level byte, which does not locate a player); F2 the purge left streaks open → fixed (A61); F3 HuntAction.Tick's sweep unguarded → fixed (A61)
- Codex verdict: REVISE (round 1) — F1 seeds left on units after an end path → rejected (Design › Data: removed with the unit at every end path; seeding stops); F2 purge bypassed EndSpawnState → fixed (A61); F3 a NaN or infinite player height → fixed (A60); F4 a character without Health counted alive → fixed (A60); F5 the NaN control covered x only → fixed (x, y and z)
- Codex verdict: READY (round 2) — no findings; after it, Review 30's advisories added only AnnouncerTests.A_skipped_wave_moves_the_warning_to_the_next_wave and doc lines (no production code)
- dod status: see the Log's status lines after this commit
- privacy grep (7656119, kdpenland): 0 hits in the diff
- in-game: Session 1 (owner) follows this commit; its steps are in docs/features/EVENT_SPAWNS.md › Owner steps for Session 1

### Step 2 · Session 1 and its fix · 2026-09-29 · b918028
- in-game: Session 1 part A (steps 2-12) and part B (A65 re-check, redo of steps 10 and 12), results in docs/features/EVENT_SPAWNS.md › Test results; part A's finds recorded before the fix: A65 (~D32, the chat drops `<n>`), A66 (the Hunt and unit-removal diagnostics); plan Review 32 (subagent, scope A65-A66): READY, F1 rejected (ChatUsage strips brackets), F2-F6 accepted
- logs (both sessions, copied and deleted after): no [Error]; BepInEx warnings only the known kinds; the server log's 224 "unknown state" at save load; tick averages ≤ 2.6 ms, single slow ticks 114.0 and 85.2 ms in idle windows (host pauses, as before)
- compile: 0 Warning(s), 0 Error(s); tests: 2154 passed; preflight OK; -Paths -DeclaredOf event-spawns: "declared: 519/519 in event-spawns" once the Session 1 temp folders were gone
- /code-review (fresh subagent, read-only): no blocking; F1 a failed tally would read as a failed seed → fixed (its own guard); F2 the usage table's placeholders → the step 3 note (VCF usage attributes; CommandForm.ChatUsage strips them in our own replies)
- Codex verdict: REVISE (round 1) — F1 VCF usage attributes hold `<id>` → deferred to step 3 (Log note, since event-library); F2 the tally left players over the cap uncounted → fixed (HuntTally.OverCap); F3 the diagnostic before Release → fixed (Left never throws, Release in finally); F4 no Services seam → rejected (observed in part B)
- Codex verdict: READY (round 2) — no findings; after it, the code review's F1 guard only
- privacy grep (7656119, kdpenland): 0 hits in the diff

### Step 2 · D13's fallback (A67) · 2026-09-29 · 0309313
- found in Session 1 part B: the tick after seeding logged "0 seeds kept, 3 left to the game", D13's no-go branch; A67 recorded before the fix; plan Review 33 (subagent, scope A67): READY, F1-F6 accepted (D13's text follows A67; the rewrite recorded in A67 and the Test results)
- compile: 0 Warning(s), 0 Error(s); tests: 2156 passed; preflight OK
- Codex verdict: REVISE (round 1) — F1 the Entity-only removal had no control → fixed (Logic HuntBuffer.RemovalIndex, SeedUnit calls it); F2 the NaN duplicate marker → fixed (HuntBuffer.Read, AggroSeed.Count)
- Codex verdict: READY (round 2) — F1 advisory, HuntAction's class summary still described the value comparison → fixed (comment only)
- in-game: Session 1 part C, all three Thugs reached the owner; five consecutive ticks "2 seeds kept, 0 left to the game" (EVENT_SPAWNS.md › Test results)
- privacy grep (7656119, kdpenland): 0 hits in the diff

### Step 3 · 2026-09-29 · (this commit)
- built: undead-rising's modifiers (D19); the usage strings in capitals and their markup control (A68, D32); the scheduler's "hunt" phase, "hunt targets: n" and TickTimer's "slowest tick:" line (A70, D24); SpawnHealth.EventEnded and Purged at the end paths (A71, D33); preflight -TimingSpan with its fixtures (D24); session-events.py mode es2 (Session 2)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Error(s); tests: 2173 passed
- preflight: PREFLIGHT OK; -SelfTest → "selftest: 40/40 checks, 7/7 external selftests (3 fixtures each, 228 extra bad fixtures; …)"; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"; -Paths -DeclaredOf event-spawns → "paths: 1747 walked, all in manifest; declared: 538/538 in event-spawns; data tests 20 passed; plants: 15/15 fail, tempvar 15/15 lines"
- -TimingSpan on each fixture: good "timing span: 10/10 windows under 5 ms, tracked >= 140, targets >= 1, 0 slow ticks"; bad, bad-avg, bad-slow, bad-gap, bad-notarget, bad-tracked, bad-slowlast and empty fail with their planted reason; -MinTargets -2 → usage error
- /code-review (fresh subagent, read-only): F1 HuntAction counted a target that no write held (a player gone before the write, or a later unit of the event throwing) → fixed: SeedUnit adds only targets its buffer holds afterwards, and an event's targets join the tick's count only after its whole loop
- Codex verdict: REVISE (round 1) — F1 -TimingSpan stopped at the tenth timing line, so a slow tick logged right after it for the same tick went unread → fixed (the lines after the last timing line are read; fixture TimingSpan/bad-slowlast); F2 negative -MinTracked/-MinTargets or -Windows < 1 accepted → fixed (usage error, and the check refuses them)
- Codex verdict: READY (round 2) — F1 advisory, D24's ControlCases row lacked bad-slowlast → fixed; F2 advisory, the usage markup scan reads source literals, not runtime strings (an escaped `<` would pass) → accepted as is: no usage string in Commands/*.cs uses an escape, a constant or a concatenation, so the literals are the runtime strings
- privacy grep (7656119, kdpenland): 0 hits in the diff beyond the plan's D31 line that quotes the pattern
- in-game: Session 2 (owner kick-off, then the load, the restart and the uninstall) follows this commit; its steps are in docs/features/EVENT_SPAWNS.md › Owner steps for Session 2
- session 1 log check: uncounted (parts A-C: both logs of each part read by hand, no [Error], every warning kind listed in docs/features/EVENT_SPAWNS.md › Session 1; the copies were deleted before a -LogCheck line was written, A72)
- session 2 log check: 0 unhandled, 327 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
  - five boots: the owner's (to 13:26, the line above, run on the log copy before boot 1), restart boot 1 ("21 nyar lines"), boot 2 and es2-uninstall ("39 nyar lines"), without Nyarlathotep (13:38–13:47, 0 nyar lines, not checked), the reinstall boot ("15 nyar lines"); each 0 unhandled, 0 orphan errors, 0 unity errors; BepInEx warnings only the three known kinds plus the stop and purge summaries

### Step 4 · 2026-09-29 · (the release commit)
- built: A72 (sessionsUncounted, bounded by $script:UncountedAllowed, duplicates refused; fixtures SessionLogs/bad-uncounted-1..5) and A73 (the drill's two-counter summary and 0.6.0's exact location text; pairs pair-modifiers, pair-chance, pair-otherkey, pair-aroundplayer, pair-otherlocation); D25 moved from Pending to a ControlCases row; the six surfaces at 0.7.0
- reviews of the amendments: Review 37 (A72) READY, F1-F6 accepted; Review 38 (A73) READY, F1-F4 accepted
- compile: 0 Warning(s), 0 Error(s); tests: 2173 passed (the README's night-hunt example also parsed by EventValidator in a throwaway test, deleted)
- preflight: PREFLIGHT OK; -SelfTest → "selftest: 40/40 checks, 7/7 external selftests (3 fixtures each, 233 extra bad fixtures; …)"; rollback-drill -SelfTest → "drill selftest: 13/13"; -SessionsOf event-spawns → "session logs: event-spawns 1/1 checked; 1 uncounted (session 1); snapshots 2/2 from session 1"
- /code-review (fresh subagent, read-only): no bugs; 9 low (drill help count, a lost comma, the quoted chat values and "unknown field action.units.chance" in the root changelog, Hunt's territory timing, maxDist ≤ range in the README table, three say-it-once repeats, AroundPlayer named as a location) → all applied
- Codex verdict: REVISE (round 1) — F1 root README "never inside a castle" against allowTerritory → reworded; F2 the drill accepted any location refusal → 0.6.0's exact text, pairs pair-aroundplayer and pair-otherlocation; F3 duplicate sessionsUncounted entries → refused (bad-uncounted-5); F4 "slow tick" for a tick under 250 ms → "long tick"
- Codex verdict: REVISE (round 2) — D25's stated summary and D27's missing duplicate clause → A73, Review 38
- Codex verdict: REVISE (round 3) — two findings on the frozen ## Baseline copy of D25 and D27 → rejected (the Baseline is never edited; the live items carry A72 and A73)
- Codex verdict: REVISE (round 4) — D32's screen-reader statement "missing": it existed but wrapped across two lines → reworded on one line
- Codex verdict: READY (round 5) — no findings
