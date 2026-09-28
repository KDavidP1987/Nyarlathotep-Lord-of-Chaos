# Audit — walkable-spawns

Build plan steps 1–2 of docs/dod/walkable-spawns.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.5.0.

## Pre-audit
### Step 1 · 2026-09-28 · e34dadb
- pre-child: event-library closed (v0.5.0 tagged and released on GitHub; the owner publishes to Thunderstore); the plan was approved on subagent Review 2 READY (b228a5a) with advisories A1–A7 applied after approve; rollback base is v0.5.0
- git status: clean at e34dadb
- compile: 0 errors, 0 warnings
- tests: 1337 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: walkable-spawns 0/12 verified (just started); review subagent, not pending (A1–A7 are not gating)
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0; tcli is not on the PATH here (step 2's `tcli build` runs from its install)
- feature doc read: docs/features/WALKABLE_SPAWNS.md, created by this step (Status: in build); plan D1, D7, D8, D9, D10, Interfaces, Design › Data, Build plan step 1, Rollout › Paths walked; Commands/SpawnCommands.cs, Logic/CommandArgs.cs, Logic/AdminLines.cs, Services/SpawnTracker.cs (Regroup, the Height read), tools/preflight.ps1 (Test-CheckSessionLogs, Invoke-FixtureBattery), tools/preflight-checks.json, tools/data-inventory.json; KindredCommands Helper.ConvertPosToTileGrid; scratchpad walk-probe notes (the ProjectM.Shared metadata)
- server: `pwsh tools/dev-snapshot.ps1 -Save ws1` → "snapshot saved: ws1 (28 files)"; baseline boot of the deployed 0.5.0 DLL (0.5.0+06d6325) on nyardev: -LogCheck "0 unhandled, 8 nyar lines, 0 orphan errors, 0 unity errors"; warnings only Il2CppInterop Class::Init, Beelzebub's own two and ours "event example-empowerment: pillar empowerment takes an Empower action" (the leftover dev events.json entry, as in event-library's baseline); NyarDev.log carries only the game's PrefabLookupMap missing-prefab traces; stopped before the step 1 DLL is deployed
- found on the way, recorded before the code that depends on it:
  - A8 (discovered, ~D1 ~D10, layer 6.1): the metadata does not say whether the tile calls take world metres or the tile grid, so the probe reads both spaces and the go/no-go is per source.
  - A9 (discovered, ~D10, layer 12.4): the fixture battery knows good, bad* and empty only; good-probe folds into SessionLogs/good and empty-probe becomes bad-probe-4.

### Step 2 · 2026-09-28 · 19bcc3a
- git: tree clean at 19bcc3a (main)
- compile check: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- preflight: PREFLIGHT OK; -SessionsOf walkable-spawns "1/1 checked; snapshots 1/1 from session 1; probe records 1/1"
- dod status walkable-spawns: D1, D7, D8 pass (Session 1, cdd03cd); D2-D6 and D9-D12 open; A13 (discovered) recorded before building: the centre's height level from TileLayerUtility.GetHeightLevel(y)
- feature doc: Status and Open questions read; the coordinate-space question is answered (world metres), the water question too (blocked on the world source)
- tools: git 2.53.0, gh 2.92.0, .NET SDK 10.0.302 (builds net6.0), PowerShell 7.5.2
- in-game baseline: Session 1 ran on this build's DLL (37b1ea8) with -LogCheck "0 unhandled, 158 nyar lines, 0 orphan errors, 0 unity errors"; the snapshot is restored

## Post-audit
### Step 1 · 2026-09-28 · 2536488 (in progress: Session 1 pending)
- compile: 0 errors, 0 warnings; tests: 1351 passed (CommandArgTests WalkRadius 13 cases, ChatBytes)
- preflight: PREFLIGHT OK; -SelfTest "33/33 checks, 7/7 external selftests (… 136 extra bad fixtures …)", SessionLogs good passes with its probe record, bad-probe to bad-probe-5 fail for their planted reasons; -Paths -DeclaredOf walkable-spawns OK before the session (during it only "leftover temp nyar-snap-ws1", the open snapshot)
- /code-review (fresh subagent, read-only) on e34dadb..4dade0f: F1 a default TileWorld handed to native code and F2 GetIsGrounded with negative world indices could fault natively → fixed, A10 (live singleton only; grounded read once in tile space, off-grid refused); F3 the floored tile-space circle centre sits on a four-tile corner → fixed (x·2 + 6400 unfloored for the circle, floored index for grounded); F4 `debug here 30 junk` now ran silently → refused "arguments must be 1-2"; F5 the unavailable line beside readings → kept only as a per-space failure note; F6 unknown or case-folded sources → sources checked case-sensitively against the two known names; F7 no bug
- Codex verdict: REVISE (round 1, e34dadb..4dade0f) — F1 TilePolygons never disposed → fixed (nested try/finally); F2 query leak if ToEntityArray throws → fixed; F3 a record without a ledge passes → rejected by reference to D1 (incomplete means fewer than five readings; the ledge is outside the verdict); F4 a record may omit a source → fixed (both sources required, fixture bad-probe-5); F5 loose reading regex → anchored with WalkReply's numeric shapes; repeated readings of a label stay allowed (D1 "every dry reading")
- Codex verdict: REVISE (round 2, e34dadb..2536488) — F1 the tile-space circle centre should be floored as in A8 → rejected: A8 cites floor(x·2) + 6400 for the grid's scale and offset; CheckStaticCircle takes a float2, and a floored centre is the corner of four tiles, which reads blocked beside one blocked tile (the code review's F3); the floored index stays for GetIsGrounded, which takes an int2. No other finding.
- in-game: deployed 0.5.0+2536488 on nyardev inside snapshot ws1; initialized cleanly; Session 1 (owner) pending
- Session 1 attempt (owner): every `.nyar debug walk` replied "walk check unavailable: singleton: none"; the default query on TileWorldSingleton leaves out system entities → A11 (defect): ServerScriptMapper.GetSingletonEntity first, then an IncludeSystems query; compile check "Build succeeded, 0 warnings"; dotnet test 1351/1351; PREFLIGHT OK; logs of the first attempt copied to %TEMP%\nyar-s1a-logs before the stop (the owner's walk commands change nothing, so no autosave was awaited)
- Codex verdict: REVISE (A11 round 1) — F1 a throwing GetSingletonEntity skipped the query fallback → fixed (caught inside LiveTileWorld); Codex verdict: READY (A11 round 2) — "Lookup exceptions are caught, only live component data reaches native calls, and both temporary native containers are disposed correctly"
- Session 1 readings (owner, 37b1ea8): world source dry free, pond and cliff blocked, building interior free, ledge blocked; tile source free everywhere; no river → owner decision in plan mode (option A): A12 (discovered) reads dry, pond, water, cliff and wall, floor and ledge not counted; checker and SessionLogs fixtures relabelled
- A12 review (fresh subagent, read-only, on 4578bb8): VERDICT: READY; advisory F1-F7 all accepted: fixture plant text and checker comment (F1, F2), Interfaces variants line (F3), D1's reply list with the wall at r 0.50 and r 1.00 (F4, F7), fixture bad-probe-6 (a free world wall with go) fails under -SelfTest, 137 extra bad fixtures (F5), wall and water appended to Session 1 in the same run (F6); PREFLIGHT OK
- session 1 log check: 0 unhandled, 158 nyar lines, 0 orphan errors, 0 unity errors
- session 1 logs read (both, copied to %TEMP%\nyar-s1a-logs and nyar-s1b-logs): BepInEx warnings only the known four (Il2CppInterop Class::Init; Beelzebub's two TUNE lines; ours "event example-empowerment: pillar empowerment takes an Empower action", the leftover dev events.json entry); NyarDev.log: 225 PrefabLookupMap "unknown state"/"converted but does not exist" traces (the game's, as in the baseline) and two Unity "JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames" lines at boot, before the first autosave, present in both runs including the first one where the walk check read nothing, so not from WalkCheck; no [Error] line
- snapshot restored; hashes equal (ws1); walk verdict: singleton world go, singleton tile no-go (A8: step 2 uses the world source)

### Step 2 · 2026-09-28 · build (in progress: Session 2 pending)
- built: Logic/Spawning.cs (SpawnPoints, WalkBudget, WaveWalk, WavePoints, WalkHeight, SpawnHealth); Services/WalkCheck.cs OpenWave (world-metres map data once per wave at TileLayerUtility.GetHeightLevel(anchor y), A13) and Settle; SpawnTracker.RequestWave plans each entry's ring slice; WaveAction sums "(<m> moved, <u> unchecked)" and appends ", walk h <level>"; EventScheduler resets the budget before the spawn phase; HealthMonitor.Degraded adds SpawnHealth.Entries; Dependency.WalkCheck with its policy row and fault case
- compile: Build succeeded, 0 warnings; tests: 1385 passed; --filter WavePoints_ 9 passed, --filter WalkCheck_ 13 passed
- preflight: PREFLIGHT OK; -AuthSuite "auth suite: pass (tests, commands, admin list, gateway, vcf dependency)"
- /code-review (fresh subagent, read-only) on the uncommitted diff: F1 a wave that never called the game (budget spent, no point, or OpenWave's failure behind a probe that never ran) closed the failure streak → fixed: WaveWalk.Answered is set only when a game call returned, and OpenWave records its failure on the WaveWalk directly
- Codex verdict: REVISE (round 1) — F1 a default TileWorld from an existing singleton could reach CreateMapData → fixed: ChunkAllocation and WorldCells must be created and WorldCells non-empty ("singleton: not created"); F2 the budget reserved two calls per check while a blocked point makes one → fixed: one unit per game call; F3 as the code review's F1 → fixed; F4 a throwing Dispose escaped and stopped the wave → fixed: caught and recorded as the wave's failure
- Codex verdict: READY (round 2) — "Round 1 findings F1–F4 are addressed, and no new correctness or safety issue was identified"
- deferred within step 2, by dependency: ControlCases re-keying (D9) lands with the DebugCommands check and its fixtures after Session 2, since its D9 row names DebugCommands fixtures that exist only once the verb is removed
- in-game: deployed 0.5.0+7f02abb on nyardev inside snapshot ws2 (`dev-snapshot.ps1 -Save ws2`, 28 files); initialized cleanly ("triggers: all hooks available", both boot sweeps 0)
- Session 2 (owner): five waves at the Session 7 pond shore, none in water; moved 2, 3, 3, 3, 3, unchecked 0; walk h 10 equal to `debug walk` h 10; tick avg ≤ 0.929 ms
- session 2 log check: 0 unhandled, 60 nyar lines, 0 orphan errors, 0 unity errors
- session 2 logs read (both, copied to %TEMP%
yar-s2-logs before the stop, deleted after): BepInEx warnings only the known four (Il2CppInterop Class::Init; Beelzebub's two TUNE lines; "event example-empowerment: pillar empowerment takes an Empower action", 3 reloads) and ours "event bandit-ambush stopped: 0 units queued, 0 spawns cancelled" (the owner's stop); NyarDev.log: 225 PrefabLookupMap traces (the game's), the game's RepairVBloodProgressionSystem Lookup notice, and two JobTempAlloc lines before the first autosave (boot, as in Session 1); no [Error] line
- snapshot restored; hashes equal (ws2)

