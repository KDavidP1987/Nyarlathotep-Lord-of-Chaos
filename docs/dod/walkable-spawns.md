---
dod: 2
rubric: 2
id: dod-20260928-wks1
slug: walkable-spawns
title: Walkable spawns — the walk probe, walkable spawn points and release 0.5.1
status: in-progress
size: M
parent: nyarlathotep
kind: feature
created: 2026-09-28
baselined: 2026-09-28
closed: none
commit: de87b99
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: 15/15 layers · 49/49 probes
review: subagent
---

# DoD: Walkable spawns — the walk probe, walkable spawn points and release 0.5.1

**Size:** M: a new Logic/Spawning.cs and read-only Services/WalkCheck.cs, SpawnTracker.RequestWave, HealthMonitor, CommandArgs, the temporary `debug walk` verb, preflight checks, and the six surfaces of 0.5.1. No events.json, cfg or api change, no dependency, no data file.

**Planned:** interactively. This plan is a child of the approved Epic `nyarlathotep`. The Epic's `## Child constraints` › walkable-spawns entry, Business rules 3 and 9, and amendment A26 govern it. The owner split it from event-spawns in plan mode on 2026-09-28, after event-spawns' Review 9 (option A). Its items reuse event-spawns D1–D5 and D28, reviewed there nine times. Reversible assumptions below are decisions taken inside that scope, each with its fallback.

**Request:** event-library A27 recorded a wave unit stuck in a pond (Session 7 of docs/features/EVENT_LIBRARY.md › Test results), and 0.5.0 shipped with that known issue. The owner's decision 3A of 2026-09-28: probe the game's tile collision on that pond, then move spawn points to walkable ground, and release the fix alone as 0.5.1.

## Definition of Done
- [x] D1 · **Walk probe decides the check** a temporary admin command `.nyar debug walk [radius]` (radius per D7) reads the game's static tile collision at the admin's position through Services/WalkCheck.cs and replies one line per source "walk <x> <z> h <heightLevel> r <radius>: <free|blocked> grounded <yes|no> (<source>)", source naming the map data used and the coordinate space, world metres or the tile grid (A4, A8); Session 1 (the owner) records the reply on dry open ground (dry), in a pond (pond), in a second water body such as the pond of event-library's Session 7 or a river (water), against a cliff face (cliff) and against a building's outer wall (wall), plus readings labelled ledge (a cliff top edge) and floor (a building's interior floor) recorded but outside the verdict (A12); the feature doc records a go/no-go line per source: go when every dry reading is free and grounded (A4) and every pond, water, cliff and wall reading is blocked, no-go when any differs; the child goes on when one source is go and step 2 uses that source (A8); fewer than five readings, or a reply without its free or blocked word, records "go/no-go: incomplete", which is neither go nor a pass, and the session is repeated; on no-go 0.5.1 is not released, D2–D6 and D12 are removed by a discovered amendment, placement and the known-issue note stay as in 0.5.0, and the owner decides in plan mode whether another check is probed (S-3); D10's probe-record check enforces the record · manual: Session 1 records the labelled replies (dry, pond, water, cliff and wall, with floor and ledge; the wall read at r 0.50 and r 1.00, pressed against a map building's outer wall) and the go/no-go line in docs/features/WALKABLE_SPAWNS.md › Test results
- [ ] D2 · **Walkable point planner** Logic/Spawning.cs `SpawnPoints.Choose(point, centre, radius, isFree)` returns the ring point when isFree(point); otherwise it tries, in order, the 11 other angles of 12 on the same ring, then the same 12 angles at half the radius, then the centre, and returns the first free one with its kind (ring, moved, centre); when none is free it returns the centre with kind "unchecked"; it calls isFree at most 25 times and never with a point farther than radius from the centre; at radius 0 it calls isFree once, on the centre, and returns the centre with kind centre when free, unchecked when blocked (A6) · test: Nyarlathotep.Tests SpawningTests SpawnPoints (fails when: radius 0 calls isFree more than once, a free ring point is moved, a blocked point is kept, the search order differs, a point outside the radius is tried, isFree is called more than 25 times, or an all-blocked ring returns other than the centre with kind unchecked)
- [ ] D3 · **Wave placement keeps the wave** Logic/Spawning.cs `WavePoints.Plan(ring, centre, radius, isFree, isGrounded)` runs after WavePlan.Split has clamped the wave by MaxUnitsPerWave and MaxTrackedUnits, returns exactly one point per unit in ring order, and treats a point whose tile is not grounded at the centre's height level (a cliff top or a foot on another terrace) as blocked; it never adds, drops or reorders a unit and never changes a cap; SpawnTracker.RequestWave calls it once per unit entry with WalkCheck.IsFree and WalkCheck.IsGrounded at the centre's height level, each entry planning its own slice of the ring, and returns the entry's moved and unchecked counts; Services/WaveAction.cs sums them into its existing line, "event <id> wave <n>/<N>: <total> units queued (<m> moved, <u> unchecked), due in …" (A2); the checks are wrapped by Logic `WalkBudget`, 2,500 game calls per server tick, reset each tick, after which a point keeps its ring point with kind unchecked without calling the game (A3) · test: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WavePoints_ → "Passed!" with at least 7 tests (fails when: a point after the spent budget calls the check or is dropped, a point count differs from the unit count, a unit is dropped or reordered, a not-grounded point is kept, the counts in the line differ from the kinds, planning runs before the caps or changes them, or the filter runs fewer than 7 tests)
- [ ] D4 · **Waves spawn on walkable ground** in game, walkable placement moves units out of water without slowing the tick · manual: Session 2 starts bandit-ambush with its centre on the shore of event-library's Session 7 pond, radius 10 reaching the water, three times; the owner confirms by sight that no unit stands in water, and the three wave lines show moved > 0; with Debug.TimingLog on, every "tick timing" line covering the three waves shows avg under 5 ms (Epic D24)
- [ ] D5 · **Walk check failure is harmless** WalkCheck.IsFree or IsGrounded that throws, finds no map data, or gets a NaN point or a height level outside the map's range counts as free and grounded and opens the failure streak (the ring point is used as before 0.5.1); the rest of that wave is then treated as free and grounded without calling the game, and the next wave tries the check again (A5); it logs "walk check unavailable: <reason>" once per failure streak and never stops a wave; a unit left in water still despawns at its due time (Epic Business rules 3) · test: Nyarlathotep.Tests DependencyFailureTests WalkCheck, also run as dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WalkCheck_ → "Passed!" with at least 4 tests (fails when: a throwing check stops the wave, a point of the same wave after a failure calls the game, a NaN point or out-of-range height level is checked instead of failing open, a point is dropped, the log line repeats within a streak, a recovered check is not used again, or the filter runs fewer than 4 tests)
- [ ] D6 · **Health shows the walk check** HealthMonitor.Degraded adds Logic `SpawnHealth.Entries`: "spawns: walk check unavailable" while WalkCheck's failure streak is open (D5); the 10-minute health line, `.nyar status` for admins and the admin login notice carry it (foundation D31) · test: Nyarlathotep.Tests HealthTests WalkHealth (fails when: an open streak gives no entry, a recovered check keeps its entry, or the entry's text differs from Failure & observability's table)
- [x] D7 · **Walk radius argument** `.nyar debug walk [radius]` parses radius with Logic CommandArgs.WalkRadius: absent 0.5, else a decimal 0.1-5 in invariant culture with at most two decimals; "0", "5.01", "1,5", "abc" or a second argument reply "radius must be 0.1-5" and read nothing · test: Nyarlathotep.Tests CommandArgTests WalkRadius (fails when: an absent radius is not 0.5, 0.1 or 5 is refused, 0.09, 5.01, "1,5", "abc" or an extra argument is accepted, or a refused radius calls the check)
- [x] D8 · **Chat lines in real chat** (profile note 11.2) the walk reply and the radius refusal are rendered in the game's chat in Session 1; each is under 480 bytes, wraps in the chat window without losing its reason, and carries no meaning by colour; chat is typed and read with the keyboard and the game offers no screen reader, an inherited limitation the README states · manual: Session 1 records, per line, the owner's observation that it shows whole and is readable without colour; test: Nyarlathotep.Tests CommandArgTests ChatBytes computes each line's byte count from its template at the maximum lengths of its fields (coordinates ±10000.0, height level 255, radius 5.00, the longest source name) (fails when: a line exceeds 480 bytes)
- [ ] D9 · **Authorization and static checks** `debug walk` is a verb of the existing adminOnly `debug` command in Commands/SpawnCommands.cs (usage "here [radius] | walk [radius]"), so no new command and no [Mutating] method exist; WalkCheck is read-only, and placement runs inside SpawnTracker.RequestWave, a dispatched service reached only through Gateway.Run, EventRuntime or the scheduler, so the existing GatewayOnly and admin-list checks of -AuthSuite (foundation) fail on a bypass (Security 10.1); step 2 removes the verb, and a new preflight check DebugCommands fails while Commands/SpawnCommands.cs carries a `walk` verb, read from the `debug` command's usage text and from the string literal Debug compares its first argument with (A7) ("debug commands: none temporary"); step 2 keys ControlCases rows by slug and D-id ("walkable-spawns D2"), ControlCaseTests reads every plan its table lists, and Nyarlathotep.Tests.csproj copies docs/dod/walkable-spawns.md; the table has a row for every control of this plan, D2, D3, D5, D6, D7, D9, D10, D11 and D12, a cmd item's row naming the preflight fixture that fails it (A1) · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (…)"; pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests"; pwsh tools/preflight.ps1 → PREFLIGHT OK with "debug commands: none temporary" (fails when: `debug` loses adminOnly, the verb ships, fixture DebugCommands/bad passes, DebugCommands/empty prints other than "debug commands: no command files" as a failure, a control lacks its row, or an event-library row is lost in the re-keying)
- [ ] D10 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/WALKABLE_SPAWNS.md › Test results, with its -LogCheck line in docs/audits/walkable-spawns.md before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/preflight-checks.json gains `childDocs`, `snapshotSessions` (1), `dataTables` and a new `probeRecords` entry for walkable-spawns, whose new -SessionsOf part parses Session 1's labelled walk lines, recomputes the verdict per source (dry free and grounded (A4); pond, water, cliff and wall blocked; ledge and floor not counted; A8, A12) and fails a go/no-go line that disagrees; tools/data-inventory.json has a "walkable-spawns › <artifact>" entry per Design › Data row · cmd: pwsh tools/preflight.ps1 -AuditOf walkable-spawns → "audit steps: walkable-spawns 2/2 pre, 2/2 post, 2/2 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf walkable-spawns → "session logs: walkable-spawns 2/2 checked; snapshots 2/2 from session 1; probe records 1/1"; pwsh tools/preflight.ps1 -Paths -DeclaredOf walkable-spawns → "paths: <n> walked, all in manifest; declared: <d>/<d> in walkable-spawns" (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a probe record misses a reading or its go/no-go line, disagrees with its readings or reads "incomplete" (fixtures SessionLogs/bad-probe, bad-probe-2 and bad-probe-3 fail, SessionLogs/good with its probe record passes, bad-probe-4 prints "probe records: <slug> session <n> has no readings" as a failure; A9), a walked path matches no manifest glob or is undeclared, a %TEMP%\nyar-* folder outlives its session, or a Data row lacks an inventory entry or field)
- [ ] D11 · **Secrets and privacy** this child adds no credential; no tracked file, built zip, dist/ or build/ file holds a token, and no tracked line holds a SteamID digit run or the owner's mail name except lines quoting the pattern; the walk reply carries the admin's own coordinates to the admin only, and its log line carries no SteamID · cmd: pwsh tools/preflight.ps1 → "secrets: none (<n> files scanned…)" (the existing Secrets check, whose fixtures Secrets/bad…bad-9 plant a token shape and Secrets/empty an empty tree under -SelfTest); then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern (fails when: a token shape is in the scanned set, which the existing Secrets fixtures plant under -SelfTest, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D12 · **Release 0.5.1** csproj Version and thunderstore.toml versionNumber are 0.5.1; both changelogs describe the walkable spawn points; both READMEs drop the known-issue note, and the Changelogs check forbids "Known issue (0.5.0)" and "Known issue in 0.5.0" in both READMEs from 0.5.1 on (fixture Changelogs/bad-knownissue); the annotated tag v0.5.1 is pushed and the GitHub pre-release carries the tcli zip; after a failed or ambiguous `gh release create` the step runs `gh release view v0.5.1` and retries only when the release is absent; no tcli publish · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan walkable-spawns → "rollback gate: 4/4"; then pwsh tools/release-verify.ps1 -Tag v0.5.1 -Asset kdpen-Nyarlathotep-0.5.1.zip → "release verify: hashes equal" (fails when: a surface differs, 0.5.0 does not initialize on 0.5.1's files, the repository revert of v0.5.0..v0.5.1 is not clean, a Rollback route is missing or names another range, the tag is missing or unpushed, the release has no zip, a README still carries the known-issue text, fixture Changelogs/bad-knownissue passes, or the hashes differ)

## Purpose & typical use
- **Who:** a server admin running wave events. They'd say "my bandits spawned in the lake and just stood there".
- **Job:** every wave unit spawns on ground it can walk on, with no setting to turn on. Waves otherwise behave exactly as in 0.5.0.
- **Coexists with:**
  - the foundation's SpawnWaves, SpawnTracker, caps, purge and boot sweep, all unchanged;
  - event-library's templates and authoring (no key changes);
  - event-spawns, which builds on this child's SpawnPoints and adds territory to the same search.

## Use cases
### Typical
The admin starts bandit-ambush by a pond. Ring points in the water move to the shore (D2, D3), the wave line shows "(3 moved, 0 unchecked)", and the units fight and despawn as in 0.5.0.

### Minimal stretch
- **Least use (8.1):** a wave of one unit on dry ground spawns exactly where 0.5.0 put it, since a free ring point is kept (D2). The pillar off (the default) spawns nothing (Epic D27). A radius of 0 gives the centre after one check (D2's empty case).
- **Once and never again (8.2):** the check keeps no state beyond its failure streak, which clears on success and on restart (D5, D6). The `debug walk` verb is gone by release (D9).

### Maximal stretch
- **Volume (9.1):** at the raised caps (500 tracked, 50 per wave) the search costs at most 25 checks per unit, and SpawnsPerTick bounds units per tick; Session 2 measures the tick at the defaults (D4).
- **Abuse (9.2):** players have no path (`debug walk` is admin-only, D9) and placement takes no input; an all-blocked ring falls back to the centre, reported unchecked (D2).
- **Repeated use (9.3):** the check is a pure read, so a second `debug walk` gives the same reply; a wave is planned once when queued (foundation SpawnLedger).

## Business rules
1. **Units are ours (CLAUDE.md › Spawn & buff safety):** this child writes nothing to any entity. It only chooses the point SpawnTracker already spawns at (D3, D9).
2. **Search (4.1, D2):** ring point; else the other 11 of 12 angles on the ring; else the same 12 angles at half the radius; else the centre. At most 25 checks. Never beyond the radius. All blocked gives the centre, kind unchecked (D2).
3. **Walkable (4.1, D1, D3):** free under CheckStaticCircle with CollideNormalMovement at the centre's height level, radius 0.5 m, and grounded at that level. The probe decides whether the game's answer means what we need (D1).
4. **Invariants (4.2, D3):** one point per unit, in ring order; no unit added or dropped; the caps are never touched.
5. **Time (4.3):** the check runs when a wave is planned, never later; a unit placed on dry ground that the terrain changes under (none does in V Rising) is not moved again. A unit left in water by a failed check still despawns at its due time (Epic Business rules 3).
6. **Precedence (4.4, D3):** the controls of Precedence.StartBlocker and WavePlan.Split's caps come first and decide how many units there are; placement comes after and decides only where. A failed check falls open to the ring point (D5). There is no exception and no one to grant one.
7. **"Every" sets (4.5):**
   - "Every wave unit" is every point WavePoints.Plan returns for RequestWave, SpawnTracker's only wave path. `.nyar spawn` spawns at the admin's feet and is not re-placed.
   - "Every control" is ControlCases' walkable-spawns rows (D9), which ControlCaseTests compares with this plan's test and cmd items.
   - "Every path this child writes" (D10) is computed by `-Paths -DeclaredOf walkable-spawns`: git diff against the commit of the audit's Step 1 pre-audit, plus untracked, ignored and server paths written after it and the %TEMP%\nyar-* folders; known exclusion: a path created and deleted inside one step (S-6).
   - "No token or SteamID" (D11) is the Secrets check's set plus the git grep over tracked files before each push.

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:** Logic/Engine.cs WavePlan (Split, Center); Services/SpawnTracker.cs RequestWave; Services/HealthMonitor.cs Degraded; Commands/SpawnCommands.cs Debug.
- **Writes:** Services/WalkCheck.cs, new and read-only (IsFree(x, z, heightLevel, radius), IsGrounded(x, z, heightLevel)); Logic/Spawning.cs, new (SpawnPoints, WavePoints, SpawnHealth: D2, D3, D6); RequestWave's use of WavePoints and the moved and unchecked counts it returns, summed by Services/WaveAction.cs into its wave line (D3, A2); Logic WalkBudget (D3, A3); CommandArgs.WalkRadius (D7); the `walk` verb, added in step 1 and removed in step 2 (D9).
- **What breaks if wrong:** a planner that drops a point would spawn fewer units than the wave asks (D3's test); a check that reads wrongly would move units off good ground, which the probe gates (D1).
- **Shared types (5.3):** none change. The wave line gains a parenthesis; SpawnPoints' kinds are internal. event-spawns will extend SpawnPoints' isFree with its territory rule and SpawnHealth with its entries.

### External — dependencies and their failure behaviour
- **V Rising server 1.1.12 (VampireReferenceAssemblies 1.1.12-r99041-b2) (6.1):**
  - ProjectM.Shared's `TileWorldSingleton.GetTileWorld()`, `TileCollisionHelper.CreateMapData`, `TileMapCollisionMath.CheckStaticCircle(ref mapData, float2, byte heightLevel, radius, MapCollisionFlags.CollideNormalMovement)` and `TileWorld.GetIsGrounded`. Metadata was read on 2026-09-28; the behaviour is unproven until D1.
  - Record variants sampled by D1: dry ground, pond, a second water body, cliff face, a building's outer wall, a building's floor and a ledge (A12).
  - No quota and no cost: the check runs in process on the admin's server.
- **Build and release tooling (6.1):** versions recorded at each pre-audit; floors git 2.40, gh 2.40 (the owner's account), tcli 0.2.4 (`tcli build` only), Codex CLI (`exec -s read-only`), pwsh 7.2, a .NET SDK building net6.0. No cost beyond the owner's subscriptions.
- **Failure behaviour (6.2):**
  - A check that throws or finds no map data counts as free and grounded (D5); the health entry shows it (D6). Every game call is synchronous and has no timeout; its cost is bounded instead: SpawnLedger.Request plans a whole wave in the tick it is queued, so without a bound 10 concurrent cap-sized waves could ask 10 × 50 × 25 = 12,500 checks in one tick; WalkBudget caps the game calls at 2,500 per tick and the map data is created once per wave (A3), timed by foundation's TickTimer, and a slow tick is named by event-library D36's warning.
  - Garbage fails open and is counted like a throw (D5): no map data, a height level outside the map's range, a NaN or infinite point.
  - The dev tools get no injected failures (event-spawns Review 6 F3): they run only at build time and stop the step on any error, as below.
  - A game update that renames the tile types makes the lookup throw, which lands in the same path.
  - git, gh, tcli and Codex stop the step under $ErrorActionPreference Stop; an ambiguous `gh release create` is checked with `gh release view` before any retry (D12); a Codex run at capacity or timed out is retried and not counted as a round. release-verify's and the rollback drill's own failure cases run under `-SelfTest` (the existing external selftests).
- **No sandbox (6.3):** the dev server (127.0.0.1:9876, save-data-nyardev) is the test world, each session wrapped by tools/dev-snapshot.ps1 (D10). The `debug walk` verb never ships (D9). Unit tests use fake isFree and isGrounded functions, never game types.

## Design
### Data
| Artifact | Where | Owner | Kept | Deleted |
|---|---|---|---|---|
| walk failure-streak state | memory | WalkCheck, SpawnHealth | the process | reset when the check succeeds and on restart (D5, D6) |
| walk probe replies | the admin's chat and BepInEx/LogOutput.log | the admin | until the log rotates | with the log; the verb is removed (D9) |
| session log copies | %TEMP%\nyar-s*-logs | Claude | the session | deleted in the same session (D10) |
| snapshot | %TEMP%\nyar-snap-* | tools/dev-snapshot.ps1 | the session | on restore (D10) |
| review prompts | %TEMP%\dod-review-*.txt | the dod skill | until the next prompt | replaced by it; never committed |
| Claude's scratchpad (Codex prompts, outputs) | the per-session scratchpad, outside the repository | Claude | the session | removed with it; SteamIDs redacted before any Codex prompt |
| build outputs | Nyarlathotep/**/bin, obj, *.binlog (git-ignored) | Claude | until the next build | overwritten by each build; never shipped |
| release zip | Nyarlathotep/Nyarlathotep/build/*.zip, dist/ (git-ignored) | Claude | until the next release | replaced by the next tcli build; its SHA-256 is kept in the audit |
| review pages | docs/dod/walkable-spawns.review.html, docs/dod/walkable-spawns.html | the dod skill | until the next render | overwritten by it |
| session marker | %TEMP%\nyar-session | tools/dev-snapshot.ps1 | the session | deleted when the session ends (D10) |
| release and drill temp folders | %TEMP%\nyar-rel-*, nyar-rollback-*, nyar-drill-*, nyar-{selftest,depsuite,snaptest,drilltest}-* | release-verify, rollback-gate, preflight -SelfTest | the run | deleted by the script that made it (D10) |
| dev-server files | the server's Nyarlathotep.dll, cfg, events.json and state.json with .bak and .tmp, save-data-nyardev, NyarDev.log, LogOutput.log | Claude (Sessions 1-2) | the session | restored by tools/dev-snapshot.ps1 (D10) |
| tag and GitHub release v0.5.1 | origin | the owner | for ever (exist only once) | never deleted; a bad one is retitled (Epic S-19) |
| plan, reviews, audit, feature doc | docs/ (committed) | the repository | for ever | only by a later commit |

Only the tag and release exist only once. Nothing new is persisted: events.json, state.json and the cfg are unchanged, so there is no migration (3.4). Every row has a tools/data-inventory.json entry with the five fields (location, owner, retention, deletion, singleCopy), checked by `-Paths -DeclaredOf walkable-spawns` (D10). Retention beyond what a step leaves is outside the check by design §9 D18 (S-6).

### States
- **Empty and first run (7.1):** no map data before the world loads: no wave runs before Core.IsReady (foundation), and after it a missing map is fail-open (D5). A radius of 0 gives the centre (D2).
- **Partial and error:** a check that throws for one unit leaves that unit's ring point and the rest of the wave checked (D5).
- **Concurrent use (7.2):** two waves planned in one tick read the same static map, which nothing writes. Every call runs on the server's main thread (single-threaded), so the failure streak's transitions cannot race. The actors are admins, the scheduler and Claude (planner and build tooling); Claude never runs two sessions on one server, since the snapshot refuses a leftover (D10).
- **Stale data, cancel and re-entry (7.3):** the tile map is static for a server build, so a plan never goes stale. Stop, purge and restart are unchanged (foundation); only the failure streak is state, and a restart clears it.

### Permissions
- **Actors (2.1):**
  - Admins run `debug walk` (step 1 build only) and every existing `.nyar` command, unchanged.
  - The System actor (scheduler, triggers) starts events; placement runs for it with no input.
  - Players have no command here and cannot reach placement.
  - Unauthenticated connections never reach chat (Steam authentication comes first); the operator may remove the DLL, and nothing here reads a file.
  - Claude builds, deploys to the dev server and pushes; the owner alone publishes (Rollout › Shipping). Build agents have no chat identity.
- **Unauthorised path (2.2):** VCF's adminOnly rejects a player's `debug walk` with the framework's denial line; adminOnly is VCF's, and the -AuthSuite admin list fails when `debug` loses it (D9). No non-admin session is planned.
- **Ownership (2.3):** a unit belongs to its event, whoever started it; placement changes no ownership. The admin's own position is read only for the admin's own reply (D11).

### UX
- **Where it lives (11.1):** nowhere to configure: every wave uses it from 0.5.1. The README's event section says units spawn on walkable ground and what "unchecked" means (D12).
- **Feedback (11.2):** the wave line's "(<m> moved, <u> unchecked)" (D3); `debug walk`'s one line (D1); the refusal "radius must be 0.1-5" (D7).
- **Accessibility (11.3):** chat lines under 480 bytes, no meaning by colour, keyboard only; no screen reader, an inherited limitation (D8).
- **Activation (11.4):** it activates for every wave with no key. What shows it ran is the wave line's counts (D3). It does not activate for `.nyar spawn`, which spawns at the admin's feet by design.

## Security
- **Authorization (10.1):** the only new path is the admin-only `debug walk` verb (D9), removed before release. Placement is reached only from RequestWave, which the scheduler and admins' `event start` already reach through Gateway.Run; SpawnTracker is a dispatched service reached only through Gateway.Run, EventRuntime or the scheduler, and foundation's GatewayOnly check, run by -AuthSuite, fails on a bypass (D9).
- **Injection (10.2):** the radius is parsed as an invariant-culture decimal against 0.1-5 (D7); nothing reaches a shell or a template.
- **Secrets (10.3):** none added; the Secrets check and the privacy grep (D11). Token storage and rotation are the owner's and outside D11: gh keeps its token in the Windows credential store, and TCLI_AUTH_TOKEN lives only in the owner's environment while the owner publishes.
- **Personal data (10.4):** the walk reply shows the admin their own position; the log line has no SteamID. Audit and session records never copy "admin …" log lines carrying a SteamID (D10, D11).

## Failure & observability
- **What the admin sees (12.1):**

| Failure class | Admin sees | Next action |
|---|---|---|
| Walk check unavailable (D5) | "spawns: walk check unavailable" in `.nyar status`, the health line and the login notice; waves still spawn, unchecked | none at once; report the server build, since a game update may have moved the tile data |
| All points blocked (D2) | the wave line counts the unit as unchecked; it spawns at the centre | move the event's centre to open ground |
| Bad radius (D7) | "radius must be 0.1-5" | retype the command |

- **Logs (12.2):** the wave line's counts (D3); "walk check unavailable: <reason>" once per streak (D5); `debug walk`'s reply is logged with the map source (D1).
- **Knowing it is broken (12.3):** the admin, on login: the degraded entry joins the 10-minute health line, `.nyar status` and the login notice (D6). -LogCheck covers every dev session (D10).
- **Failing cases (12.4):** each check this plan introduces:

| Check | Reports a failure on | Stays silent on | Empty input |
|---|---|---|---|
| DebugCommands (preflight, D9) | fixture bad: SpawnCommands.cs with the verb | fixture good: without it | fixture empty: "debug commands: no command files" (fail) |
| Changelogs known-issue rule (D12) | fixture Changelogs/bad-knownissue: a 0.5.1 README with "Known issue (0.5.0)" | the existing good fixture at 0.5.1 | the existing empty fixture (fail) |
| Probe records (-SessionsOf, D10) | SessionLogs/bad-probe (a reading missing), bad-probe-2 (no go/no-go), bad-probe-3 (go while the pond is free) | SessionLogs/good-probe | SessionLogs/empty-probe: "… has no readings" (fail) |
| ControlCases per slug (D9) | a walkable-spawns control without its fails-when, passes or empty test | the real tree | a plan with no control rows: "control cases: walkable-spawns has no rows" (fail) |
| WavePoints filter (D3) | a dropped or reordered unit | the stated plan | fewer than 6 tests run: fail |
| WalkCheck_ filter (D5) | a throwing check that stops a wave | fail-open behaviour | fewer than 4 tests run: fail |
| SpawnPoints (D2) | a moved free point, a kept blocked one, a 26th call | the stated search | radius 0: the centre after one call |
| WalkHealth (D6) | an open streak without its entry | a closed streak | no streak: no entry |
| WalkRadius (D7) | 0.09, 5.01, "1,5", "abc" or an extra argument accepted | 0.1, 0.5, 5 | no argument: 0.5 |
| ChatBytes (D8) | a line template over 480 bytes at its maximum fields | every line under 480 | a template with no fields still measured |
| Secrets (D11, existing) | Secrets/bad…bad-9 | Secrets/good | Secrets/empty |

Each fixture is registered under `-SelfTest` in the step that adds its check, and each test row runs there through `dotnet test`; `-SelfTest` fails when a bad or empty fixture passes or a good one fails. The DebugCommands fixtures are copies of the real SpawnCommands.cs.

The empty input of each Logic control (each a `<Name>_empty_<input>` test):

| Control | Empty input | Result |
|---|---|---|
| D2 SpawnPoints | radius 0 | the centre, one isFree call |
| D3 WavePoints | no units | no points, "0 units" in the line |
| D5 WalkCheck | no map data | counts free, "walk check unavailable: no map data" once |
| D6 SpawnHealth | no open streak | no entry |
| D7 WalkRadius | no argument | 0.5 |

One evidence command per gating probe:

| Probe | Command | Item |
|---|---|---|
| 2.1 actors | pwsh tools/preflight.ps1 -AuthSuite | D9 |
| 3.3 persistence | pwsh tools/preflight.ps1 -Paths -DeclaredOf walkable-spawns | D10 |
| 4.4 precedence | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WavePoints_ | D3 |
| 6.2 dependency failure | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WalkCheck_ | D5 |
| 10.1 authorization | pwsh tools/preflight.ps1 -AuthSuite | D9 |
| 10.3 secrets | pwsh tools/preflight.ps1 ("secrets: none") | D11 |
| 12.4 failing cases | pwsh tools/preflight.ps1 -SelfTest (every fixture and external selftest, `dotnet test` with ControlCaseTests among them) | D9 |
| 14.3 rollback | pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan walkable-spawns | D12 |
| 14.4 paths | pwsh tools/preflight.ps1 -Paths -DeclaredOf walkable-spawns | D10 |

The 6.2 evidence is a test filter, not `-DependencySuite`, whose categories are event-library's fixed list; this child has one failure class (S-8).

## Performance
- **Budget and hot path (13.1):** the scheduler tick stays under 5 ms on average (Epic D24). The hot path is the point search: at most 25 checks per unit, and SpawnsPerTick units per tick. A whole wave is planned in the tick it is queued; WalkBudget bounds the game calls at 2,500 per tick whatever the number of waves, the rest keeping their ring points as unchecked, and the map data is created once per wave (A3, Interfaces 6.2); Session 2 records the timing line of the pond waves (D4).
- **Limits (13.2):**

| Bound | Source | At the bound | Valid case excluded |
|---|---|---|---|
| point search 25 | 12 + 12 + 1 | the 25th point is the centre | a walkable spot beyond the ring |
| walk checks 2,500 per tick | 10 waves × 50 units could ask 12,500 (A3) | later points keep the ring point, unchecked | a check of points past the budget |
| walk radius 0.1-5 m | a unit's footprint is about 0.5 m; 5 m is a boss's | 5 accepted, 5.01 refused (D7) | probing a whole clearing at once |
| chat line 480 bytes | the game's chat (profile note 11.2) | wrapped, never cut | none |
| units: foundation caps | Epic Business rules 9 | clamped before placement (D3) | none new |

## Build plan
1. **Walk probe and Session 1 (owner).**
   - Pre-audit per CLAUDE.md into docs/audits/walkable-spawns.md (created from docs/audits/README.md's template).
   - Add Services/WalkCheck.cs: IsFree and IsGrounded over the server's static tile world with CheckStaticCircle and CollideNormalMovement. Probe the map-data sources in order and record which one answered: TileWorldSingleton.GetTileWorld(), the map data of the server's collision system, then XPRising's `new TileWorld()` (expected to read empty).
   - Add `walk` as a verb of the existing `debug` command in Commands/SpawnCommands.cs (a second `debug` command would collide), with CommandArgs.WalkRadius and CommandArgTests WalkRadius and ChatBytes.
   - Create docs/features/WALKABLE_SPAWNS.md; add the `childDocs`, `snapshotSessions`, `dataTables` and `probeRecords` entries, the -SessionsOf probe-record part with its SessionLogs fixtures, and the data-inventory rows (D10).
   - Deploy with the server stopped, wrapped by `pwsh tools/dev-snapshot.ps1 -Save ws1`.
   - Session 1: the owner runs the command at the six places; every reply prints its grounded result, and the go condition requires dry ground to read grounded (A4); the replies and the go/no-go line go to Test results. On no-go, a discovered amendment removes D2–D6 and D12 (S-3), and the owner decides the next probe in plan mode before any further code.
   - Satisfies D1, D7, D8.
2. **Walkable points and release 0.5.1.**
   - Add Logic/Spawning.cs SpawnPoints, WavePoints and SpawnHealth, with SpawningTests, DependencyFailureTests WalkCheck and HealthTests WalkHealth; wire WavePoints and WalkBudget into SpawnTracker.RequestWave, the moved and unchecked totals into WaveAction.Tick's line (A2, A3), and SpawnHealth into HealthMonitor.Degraded.
   - ControlCases keyed by slug and D-id, ControlCaseTests reading each listed plan, the csproj copying walkable-spawns.md (D9).
   - Session 2 (owner): the pond shore test of D4, wrapped by `dev-snapshot.ps1 -Save ws2`.
   - Remove the `walk` verb; add the DebugCommands check and the Changelogs known-issue rule with their fixtures.
   - Release 0.5.1 on the six surfaces: changelogs note the fix, READMEs drop the known issue; `tcli build`; annotated tag v0.5.1; `pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan walkable-spawns` (4/4); privacy grep; push; GitHub pre-release; release-verify.
   - -AuditOf, -SessionsOf and -Paths -DeclaredOf walkable-spawns (D10); `dod close walkable-spawns`. The owner publishes.
   - Satisfies D2, D3, D4, D5, D6, D9, D10, D11, D12.

## Rollout
### Shipping
One release, 0.5.1, a GitHub pre-release; D12 ends there, with release-verify. The owner publishes to Thunderstore, outside the plan. Everything still ships off: Pillars.EventSpawns defaults to off (Epic D4), and placement only changes waves an admin has enabled. Who turns it off:
- any admin, with `.nyar pillar spawns off`, `.nyar event stop` or `.nyar purge confirm`;
- the operator, by removing the DLL or installing 0.5.0.

### Compatibility
- events.json, state.json, the cfg and the api (3) are unchanged; 0.5.0's definitions run unchanged.
- Only placement changes: a ring point in water or rock moves, which is the fix itself.

### Rollback
- **In the repository:** `git revert --no-edit v0.5.0..v0.5.1`, drilled by the rollback gate with -Plan (D12). Commits in the range that are not this child's (event-library's close records, event-spawns' plan records, shared tool fixes) are re-applied after a revert with `git cherry-pick`, listed from `git log v0.5.0..v0.5.1` minus those touching this child's exclusive paths.
- **On the dev server during the build:** every session is wrapped by tools/dev-snapshot.ps1 (D10).
- **On a server:** install the 0.5.0 DLL. Waves go back to 0.5.0's placement and nothing else changes. This remains possible after data is written, because 0.5.1 writes no new file and no new key.
- **Published release:** tags and releases are never deleted. A bad release is withdrawn by retitling it, and versions move forward only (Epic S-19).
- **Commit range:** v0.5.0..v0.5.1

### Paths walked
Walking the Build plan. The walker (-Paths, Epic D33) reads git's tracked, untracked and ignored files, the dev server's paths, %TEMP%\nyar-* and the remote tags and releases.
- **Step 1:**
  - docs/audits/walkable-spawns.md, docs/features/WALKABLE_SPAWNS.md.
  - Nyarlathotep/Nyarlathotep/Services/WalkCheck.cs, Nyarlathotep/Nyarlathotep/Commands/SpawnCommands.cs, Nyarlathotep/Nyarlathotep/Logic/CommandArgs.cs, Nyarlathotep/Nyarlathotep/Logic/AdminLines.cs (the walk reply lines), Nyarlathotep/Nyarlathotep.Tests/CommandArgTests.cs.
  - tools/preflight.ps1 (-SessionsOf probe records), tools/preflight-checks.json (childDocs, snapshotSessions, dataTables, probeRecords), tools/preflight-fixtures/SessionLogs/**, tools/data-inventory.json, tools/paths-manifest.txt.
- **Step 2:**
  - Nyarlathotep/Nyarlathotep/Logic/Spawning.cs, Nyarlathotep/Nyarlathotep/Services/SpawnTracker.cs, Nyarlathotep/Nyarlathotep/Services/WaveAction.cs, Nyarlathotep/Nyarlathotep/Services/HealthMonitor.cs, Nyarlathotep/Nyarlathotep/Services/WalkCheck.cs, Nyarlathotep/Nyarlathotep/Commands/SpawnCommands.cs (the verb removed).
  - Nyarlathotep/Nyarlathotep.Tests/{SpawningTests,DependencyFailureTests.Walk,HealthTests,ControlCases,ControlCaseTests}.cs, Nyarlathotep/Nyarlathotep.Tests/Nyarlathotep.Tests.csproj (copies walkable-spawns.md).
  - tools/preflight.ps1 (DebugCommands, the Changelogs rule), tools/preflight-checks.json, tools/preflight-fixtures/DebugCommands/**, tools/preflight-fixtures/Changelogs/bad-knownissue/**, tools/paths-manifest.txt.
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - The remote tag v0.5.1 and the GitHub release v0.5.1 (`remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt).
  - docs/dod/walkable-spawns.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Sessions 1-2, on the server:** BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json{,.bak,.tmp}, save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log.
- **Outside the repository** (existing `temp:` lines): %TEMP%\nyar-snap-*, %TEMP%\nyar-s*-logs, %TEMP%\nyar-session, %TEMP%\nyar-{selftest,depsuite,snaptest,drilltest}-*, nyar-rel-*, nyar-rollback-*, nyar-drill-*.
- **Build outputs** (existing ignored globs): Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/**, Nyarlathotep/Nyarlathotep/build/**.
- **Review process:** docs/dod/walkable-spawns.reviews.md, docs/dod/walkable-spawns.review.html and docs/dod/walkable-spawns.html.

## Out of scope
- **Considered and excluded (15.1):**
  - A pathfinding query or the AI's unstuck state as the check: only if D1 is no-go, by the owner's decision (S-3).
  - Re-placing units after spawn, or placing `.nyar spawn` units.
  - Claimed-territory refusal: event-spawns adds it to the same search.
- **Deferred (15.2):** every behaviour, modifier and location of child `event-spawns`, which starts from 0.5.1.

## Also considered
- **Documentation:** the six surfaces (D12) and docs/features/WALKABLE_SPAWNS.md. **Decommissioning:** the known-issue note (D12) and the `debug walk` verb (D9) go. **Ownership:** the server admin; the runbook is the README's kill switch.
- **Compliance:** none; no personal data is stored or sent (Security 10.4).
- **Localisation and time formats:** chat is English like every `.nyar` line; the radius is invariant-culture (D7); no time is shown.
- **Running cost:** none beyond the bounded checks (Interfaces 6.2); no quota.
- **Success measurement:** Session 2's wave lines (moved > 0, no unit in water) and the timing lines (D4); no analytics.
- **Support tooling:** the wave line's counts, the health entry and `.nyar status` (D3, D6).

## Assumptions
- S-1 · validated · The walk probe, walkable spawn points and release 0.5.1 are their own child, built before event-spawns · source: owner decision in plan mode 2026-09-28 (event-spawns round cap after Review 9, option A), Epic A26
- S-2 · validated · The walkability fix of event-library A27 ships alone as 0.5.1 · source: owner decision 3A, plan mode 2026-09-28 (event-spawns S-3, carried by Epic A26)
- S-3 · validated · The walk check uses TileMapCollisionMath.CheckStaticCircle over the server's static tile map, gated by the D1 probe; a no-go is a gated scope branch: the amendment removes D2–D6 and D12 and needs a fresh READY review before any build past step 1, 0.5.1 is not released, and the owner decides in plan mode whether another source (the AI's Relocate_Unstuck state, a pathfinding query) is probed · source: owner decision 3A, plan mode 2026-09-28 ("a probe of the tile-collision check on your pond … then a walkability check")
- S-4 · reversible · The default walk radius is 0.5 m, a unit's footprint · fallback: if Session 1's readings show 0.5 m reads a shore as free while a unit sticks there, a corrected amendment raises the radius SpawnTracker uses before step 2
- S-5 · reversible · A tile not grounded at the centre's height level counts as blocked, so a point on a cliff top or at its foot moves · fallback: if Session 2 shows the rule moving points off walkable slopes, a corrected amendment drops it and D3's grounded case
- S-6 · validated · Paths a step creates and deletes inside the same step are outside the declared-paths check; each step's /code-review and Codex cross-inspection read the code that would write such a path · source: owner decision, docs/NYARLATHOTEP_DESIGN.md §9 D18 (event-library Review 11 F1, 2026-09-27)
- S-7 · validated · The profile notes apply: in-game claims have a session item (6.1), every chat text is rendered before release (11.2), every control has a test seam (12.4), and the release copies the earlier release-step amendments (14.3: release-verify, the rollback gate with -Plan, the privacy grep, the unpushed-tag check) · source: docs/dod/profile.md
- S-8 · reversible · No per-slug `-DependencySuite` here: the one failure class is proved by the WalkCheck_ test filter, and event-spawns makes the suite per slug · fallback: if a suite entry is wanted before event-spawns, add walk-check to the fixed $script:DependencyCategories list in tools/preflight.ps1 with a test class, which this child can do now

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D9 D11; 2.2 D9; 2.3 D3 D11 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D7 D1; 3.2 D3 D1 D6; 3.3 D10 D12; 3.4 D12 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D2 D3 D1; 4.2 D3; 4.3 D3 D5; 4.4 D3; 4.5 D3 D9 D10 D11 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D3 D6; 5.2 D3 D6; 5.3 D3 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D1 D4; 6.2 D5 D6 D12; 6.3 D10 D9 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D2 D5; 7.2 D3 D10; 7.3 D5 D3 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D2; 8.2 D5 D9 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D2 D4; 9.2 D9 D2; 9.3 D1 D3 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D9; 10.2 D7; 10.3 D11; 10.4 D11 D1 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D12; 11.2 D3 D1 D7; 11.3 D8; 11.4 D3 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D5 D2 D7; 12.2 D3 D5; 12.3 D6 D10; 12.4 D9 D10 D3 D5 D1 D12 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D4 D2; 13.2 D2 D7 D8 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D12; 14.2 D3 D12; 14.3 D12; 14.4 D10 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline
- [ ] D1 · **Walk probe decides the check** a temporary admin command `.nyar debug walk [radius]` (radius per D7) reads the game's static tile collision at the admin's position through Services/WalkCheck.cs and replies one line "walk <x> <z> h <heightLevel> r <radius>: <free|blocked> (<source>)", source naming the map data used; Session 1 (the owner) records the reply on dry open ground, in the pond of event-library's Session 7, in a river, against a cliff face and inside a building, plus a sixth reading labelled ledge (a cliff top edge) recorded with GetIsGrounded but outside the verdict; the feature doc records a go/no-go line: go when every dry reading is free and every pond, river, cliff and building reading is blocked, no-go when any differs; fewer than five readings, or a reply without its free or blocked word, records "go/no-go: incomplete", which is neither go nor a pass, and the session is repeated; on no-go 0.5.1 is not released, D2–D6 and D12 are removed by a discovered amendment, placement and the known-issue note stay as in 0.5.0, and the owner decides in plan mode whether another check is probed (S-3); D10's probe-record check enforces the record · manual: Session 1 records the six replies and the go/no-go line in docs/features/WALKABLE_SPAWNS.md › Test results
- [ ] D2 · **Walkable point planner** Logic/Spawning.cs `SpawnPoints.Choose(point, centre, radius, isFree)` returns the ring point when isFree(point); otherwise it tries, in order, the 11 other angles of 12 on the same ring, then the same 12 angles at half the radius, then the centre, and returns the first free one with its kind (ring, moved, centre); when none is free it returns the centre with kind "unchecked"; it calls isFree at most 25 times and never with a point farther than radius from the centre · test: Nyarlathotep.Tests SpawningTests SpawnPoints (fails when: a free ring point is moved, a blocked point is kept, the search order differs, a point outside the radius is tried, isFree is called more than 25 times, or an all-blocked ring returns other than the centre with kind unchecked)
- [ ] D3 · **Wave placement keeps the wave** Logic/Spawning.cs `WavePoints.Plan(ring, centre, radius, isFree, isGrounded)` runs after WavePlan.Split has clamped the wave by MaxUnitsPerWave and MaxTrackedUnits, returns exactly one point per unit in ring order, and treats a point whose tile is not grounded at the centre's height level (a cliff top or a foot on another terrace) as blocked; it never adds, drops or reorders a unit and never changes a cap; its counts give the wave line "wave <n> of <id>: <k> units (<m> moved, <u> unchecked)"; SpawnTracker.RequestWave calls it with WalkCheck.IsFree and WalkCheck.IsGrounded at the centre's height level · test: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WavePoints_ → "Passed!" with at least 6 tests (fails when: a point count differs from the unit count, a unit is dropped or reordered, a not-grounded point is kept, the counts in the line differ from the kinds, planning runs before the caps or changes them, or the filter runs fewer than 6 tests)
- [ ] D4 · **Waves spawn on walkable ground** in game, walkable placement moves units out of water without slowing the tick · manual: Session 2 starts bandit-ambush with its centre on the shore of event-library's Session 7 pond, radius 10 reaching the water, three times; the owner confirms by sight that no unit stands in water, and the three wave lines show moved > 0; with Debug.TimingLog on, every "tick timing" line covering the three waves shows avg under 5 ms (Epic D24)
- [ ] D5 · **Walk check failure is harmless** WalkCheck.IsFree or IsGrounded that throws, finds no map data, or gets a NaN point or a height level outside the map's range counts as free and grounded and opens the failure streak (the ring point is used as before 0.5.1); it logs "walk check unavailable: <reason>" once per failure streak and never stops a wave; a unit left in water still despawns at its due time (Epic Business rules 3) · test: Nyarlathotep.Tests DependencyFailureTests WalkCheck, also run as dotnet test Nyarlathotep/Nyarlathotep.Tests --filter WalkCheck_ → "Passed!" with at least 4 tests (fails when: a throwing check stops the wave, a NaN point or out-of-range height level is checked instead of failing open, a point is dropped, the log line repeats within a streak, a recovered check is not used again, or the filter runs fewer than 4 tests)
- [ ] D6 · **Health shows the walk check** HealthMonitor.Degraded adds Logic `SpawnHealth.Entries`: "spawns: walk check unavailable" while WalkCheck's failure streak is open (D5); the 10-minute health line, `.nyar status` for admins and the admin login notice carry it (foundation D31) · test: Nyarlathotep.Tests HealthTests WalkHealth (fails when: an open streak gives no entry, a recovered check keeps its entry, or the entry's text differs from Failure & observability's table)
- [ ] D7 · **Walk radius argument** `.nyar debug walk [radius]` parses radius with Logic CommandArgs.WalkRadius: absent 0.5, else a decimal 0.1-5 in invariant culture with at most two decimals; "0", "5.01", "1,5", "abc" or a second argument reply "radius must be 0.1-5" and read nothing · test: Nyarlathotep.Tests CommandArgTests WalkRadius (fails when: an absent radius is not 0.5, 0.1 or 5 is refused, 0.09, 5.01, "1,5", "abc" or an extra argument is accepted, or a refused radius calls the check)
- [ ] D8 · **Chat lines in real chat** (profile note 11.2) the walk reply and the radius refusal are rendered in the game's chat in Session 1; each is under 480 bytes, wraps in the chat window without losing its reason, and carries no meaning by colour; chat is typed and read with the keyboard and the game offers no screen reader, an inherited limitation the README states · manual: Session 1 records, per line, the owner's observation that it shows whole and is readable without colour; test: Nyarlathotep.Tests CommandArgTests ChatBytes computes each line's byte count from its template at the maximum lengths of its fields (coordinates ±10000.0, height level 255, radius 5.00, the longest source name) (fails when: a line exceeds 480 bytes)
- [ ] D9 · **Authorization and static checks** `debug walk` is a verb of the existing adminOnly `debug` command in Commands/SpawnCommands.cs (usage "here [radius] | walk [radius]"), so no new command and no [Mutating] method exist; WalkCheck is read-only, and placement runs inside SpawnTracker.RequestWave, a dispatched service reached only through Gateway.Run, EventRuntime or the scheduler, so the existing GatewayOnly and admin-list checks of -AuthSuite (foundation) fail on a bypass (Security 10.1); step 2 removes the verb, and a new preflight check DebugCommands fails while Commands/SpawnCommands.cs registers a `walk` verb ("debug commands: none temporary"); step 2 keys ControlCases rows by slug and D-id ("walkable-spawns D2"), ControlCaseTests reads every plan its table lists, and Nyarlathotep.Tests.csproj copies docs/dod/walkable-spawns.md; the table has a row for D2, D3, D5, D6 and D7 · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (…)"; pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests"; pwsh tools/preflight.ps1 → PREFLIGHT OK with "debug commands: none temporary" (fails when: `debug` loses adminOnly, the verb ships, fixture DebugCommands/bad passes, DebugCommands/empty prints other than "debug commands: no command files" as a failure, a control lacks its row, or an event-library row is lost in the re-keying)
- [ ] D10 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/WALKABLE_SPAWNS.md › Test results, with its -LogCheck line in docs/audits/walkable-spawns.md before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/preflight-checks.json gains `childDocs`, `snapshotSessions` (1), `dataTables` and a new `probeRecords` entry for walkable-spawns, whose new -SessionsOf part parses Session 1's labelled walk lines, recomputes the verdict (dry free; pond, river, cliff and building blocked; ledge not counted) and fails a go/no-go line that disagrees; tools/data-inventory.json has a "walkable-spawns › <artifact>" entry per Design › Data row · cmd: pwsh tools/preflight.ps1 -AuditOf walkable-spawns → "audit steps: walkable-spawns 2/2 pre, 2/2 post, 2/2 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf walkable-spawns → "session logs: walkable-spawns 2/2 checked; snapshots 2/2 from session 1; probe records 1/1"; pwsh tools/preflight.ps1 -Paths -DeclaredOf walkable-spawns → "paths: <n> walked, all in manifest; declared: <d>/<d> in walkable-spawns" (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a probe record misses a reading or its go/no-go line, disagrees with its readings or reads "incomplete" (fixtures SessionLogs/bad-probe, bad-probe-2, bad-probe-3 fail, good-probe passes, empty-probe prints "probe records: walkable-spawns session 1 has no readings" as a failure), a walked path matches no manifest glob or is undeclared, a %TEMP%\nyar-* folder outlives its session, or a Data row lacks an inventory entry or field)
- [ ] D11 · **Secrets and privacy** this child adds no credential; no tracked file, built zip, dist/ or build/ file holds a token, and no tracked line holds a SteamID digit run or the owner's mail name except lines quoting the pattern; the walk reply carries the admin's own coordinates to the admin only, and its log line carries no SteamID · cmd: pwsh tools/preflight.ps1 → "secrets: none (<n> files scanned…)" (the existing Secrets check, whose fixtures Secrets/bad…bad-9 plant a token shape and Secrets/empty an empty tree under -SelfTest); then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern (fails when: a token shape is in the scanned set, which the existing Secrets fixtures plant under -SelfTest, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D12 · **Release 0.5.1** csproj Version and thunderstore.toml versionNumber are 0.5.1; both changelogs describe the walkable spawn points; both READMEs drop the known-issue note, and the Changelogs check forbids "Known issue (0.5.0)" and "Known issue in 0.5.0" in both READMEs from 0.5.1 on (fixture Changelogs/bad-knownissue); the annotated tag v0.5.1 is pushed and the GitHub pre-release carries the tcli zip; after a failed or ambiguous `gh release create` the step runs `gh release view v0.5.1` and retries only when the release is absent; no tcli publish · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan walkable-spawns → "rollback gate: 4/4"; then pwsh tools/release-verify.ps1 -Tag v0.5.1 -Asset kdpen-Nyarlathotep-0.5.1.zip → "release verify: hashes equal" (fails when: a surface differs, 0.5.0 does not initialize on 0.5.1's files, the repository revert of v0.5.0..v0.5.1 is not clean, a Rollback route is missing or names another range, the tag is missing or unpushed, the release has no zip, a README still carries the known-issue text, fixture Changelogs/bad-knownissue passes, or the hashes differ)

## Amendments
- A1 · 2026-09-28 · discovered · ~D9 · layer: 12.4 · Review 2 F1 (advisory): ControlCaseTests treats every test or cmd item with a fails-when clause as a control, so D9-D12 need rows too; D9 lists every control's row
- A2 · 2026-09-28 · discovered · ~D3 · layer: 5.1 · Review 2 F2 (advisory): RequestWave runs once per unit entry and the wave line is written in Services/WaveAction.cs; each entry returns its counts and WaveAction sums them into its existing line; WaveAction joins Interfaces and Paths walked
- A3 · 2026-09-28 · discovered · ~D3 · layer: 13.1 · Review 2 F3 (advisory): a whole wave is planned in the tick it is queued, so 10 waves could ask 12,500 checks in one tick; Logic WalkBudget caps game calls at 2,500 per tick and the map data is created once per wave
- A4 · 2026-09-28 · discovered · ~D1 ~D10 · layer: 4.1 · Review 2 F4 (advisory): the grounded rule was not tested on dry ground before step 2 relies on it; every walk reply prints its grounded result and the go condition requires dry ground to read grounded
- A5 · 2026-09-28 · discovered · ~D5 · layer: 6.2 · Review 2 F5 (advisory): a throwing check was still called for every point of the wave; after a failure the rest of that wave is treated as free and the next wave tries again
- A6 · 2026-09-28 · discovered · ~D2 · layer: 8.1 · Review 2 F6 (advisory): at radius 0 all 25 search points are the centre; radius 0 checks once
- A7 · 2026-09-28 · discovered · ~D9 · layer: 12.4 · Review 2 F7 (advisory): `walk` is a literal inside Debug, not a [Command]; the DebugCommands check reads the usage text and the compared literal
- A8 · 2026-09-28 · discovered · ~D1 ~D10 · layer: 6.1 · step 1 build: the metadata does not say whether CheckStaticCircle and GetIsGrounded take world metres or the tile grid (KindredCommands Helper.ConvertPosToTileGrid: floor(x·2) + 6400), so the probe replies one line per source and space, the go/no-go is per source, and step 2 uses the source that is go; XPRising's empty TileWorld is read only when the singleton is missing
- A9 · 2026-09-28 · discovered · ~D10 · layer: 12.4 · step 1 build: the -SelfTest fixture battery knows only good, bad* and empty per check, so good-probe is the probe record inside SessionLogs/good (probeRecords foundation: 8) and empty-probe is SessionLogs/bad-probe-4, printing "probe records: foundation session 8 has no readings"; bad-probe to bad-probe-3 as planned
- A10 · 2026-09-28 · discovered · ~D1 · layer: 6.2 · step 1 code review F1, F2: a default TileWorld handed to native code, and GetIsGrounded indexed with negative world metres, could fault natively where no managed catch helps; the probe reads only the live singleton (a missing singleton replies "walk check unavailable: singleton: none" and reads nothing), and grounded is read once, in tile space (floor(x·2) + 6400), for both lines
- A11 · 2026-09-28 · defect · — · layer: 6.1 · Session 1 (owner): every `.nyar debug walk` replied "walk check unavailable: singleton: none"; the plan reads the live singleton, but the code queried TileWorldSingleton with a default query, which leaves out system entities where the game keeps it (TileWorldSystem); the probe asks ServerScriptMapper.GetSingletonEntity<TileWorldSingleton>() first, then a query with EntityQueryOptions.IncludeSystems, still reading nothing when both find none
- A12 · 2026-09-28 · discovered · ~D1 ~D10 · layer: 4.1 · Session 1: the admin inside a building stands on a walkable floor, which rightly reads free, so D1's "inside a building … blocked" was wrong, and no river was at hand; owner decision in plan mode (option A): the verdict reads dry, pond, water (a second water body), cliff and wall (a building's outer wall); ledge and floor are recorded, not counted; tools/preflight.ps1 Get-ProbeRecordProblem and the SessionLogs probe fixtures use the new labels; the wall and water readings are appended to Session 1 in the same server run (review F6), so probeRecords stays session 1
- A13 · 2026-09-28 · discovered · ~D3 · layer: 5.1 · step 2 pre-audit: the plan names "the centre's height level" but no source holds one (an Admin origin and a Point keep only x, y, z); the level is ProjectM.Tiles.TileLayerUtility.GetHeightLevel(centre y), the game's own conversion, read once per wave; a Point stored without a height (event-library A20) is not checked and its units count as unchecked, as in 0.5.0; Session 2 compares the level with the admin's `debug walk` h reading; Dependency gains WalkCheck with a policy row and its DependencyFailureTests case (D5)
## Log
- 2026-09-28 · status → draft · plan
- 2026-09-28 · note · split from event-spawns (Epic A26, owner option A in plan mode after event-spawns Review 9): that plan's D1, D2, D3, D4, D5 and D28 became D1, D2, D3 with D4, D5, D12 and D7 here, and D6, D8-D11 carry its shared items' walk parts; the reviewed text is reused with Review 7–9's applicable fixes (per-slug ControlCases, dataTables and snapshotSessions entries, probe records, the known-issue rule, the 480-byte bound)
- 2026-09-28 · note · review: codex Review 1 REVISE (F1-F13, 8 blocking, 5 advisory; 9/15 layers, 41/49 probes); F1 advisory by rule, F6 rejected by design §9 D18, the rest accepted (F3, F10, F11 narrowly); revision applied for Review 2 by a fresh-context subagent (design §9 D26)
- 2026-09-28 · note · review: subagent Review 2 READY (15/15 layers, 49/49 probes, EARLIER all resolved); advisory F1-F7 applied after approve as A1-A7
- 2026-09-28 · status → ready · approve
- 2026-09-28 · status → in-progress · start
- 2026-09-28 · note · amendments A1-A7 from Review 2's advisory findings F1-F7 (discovered)
- 2026-09-28 · note · Rollout › Paths walked step 1 names Logic/AdminLines.cs, where the walk reply and refusal lines live (found by -Paths -DeclaredOf)
- 2026-09-28 · D1 · pass · manual: Session 1 (owner, 37b1ea8) in docs/features/WALKABLE_SPAWNS.md › Test results: dry free and grounded; pond (two edges), water (a second pond), cliff (r 0.50, r 1.00) and wall (r 0.50, r 1.00) blocked on singleton world → "go/no-go (singleton world): go"; singleton tile free everywhere → no-go; -SessionsOf walkable-spawns "session logs: walkable-spawns 1/1 checked; snapshots 1/1 from session 1; probe records 1/1" · 37b1ea8 · owner
- 2026-09-28 · D7 · pass · test: Nyarlathotep.Tests CommandArgTests WalkRadius (13 cases) in 1351 passed; in game `.nyar debug walk abc` and `.nyar debug walk 1 2` replied "radius must be 0.1-5" · 37b1ea8 · claude
- 2026-09-28 · D8 · pass · manual: Session 1, every walk reply and the radius refusal showed whole in the game chat; the reply is plain text with no colour markup (Logic/AdminLines.cs WalkReply); CommandArgTests ChatBytes passed in 1351 · 37b1ea8 · owner
