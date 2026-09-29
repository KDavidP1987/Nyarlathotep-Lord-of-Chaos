---
dod: 2
rubric: 2
id: dod-20260928-reg1
slug: regions
title: Regions — global or regional scope for triggers and actions, and release 0.6.0
status: in-progress
size: L
parent: nyarlathotep
kind: feature
created: 2026-09-28
baselined: 2026-09-28
closed: none
commit: 7dbd9df
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: 15/15 layers · 49/49 probes
review: subagent
---

# DoD: Regions — global or regional scope for triggers and actions, and release 0.6.0

**Size:** L. It touches several modules:
- **Logic:** new Regions.cs (point-in-polygon, scope model and checks); Model and Validation (a `scope` key on trigger and action); Empowerment eligibility; the VBloodKilled route; the wave point search; ApiLines and Wire.
- **Services:** a new read-only RegionMap over the game's WorldRegionPolygon entities; EmpowerAction facts; TriggerBus; WaveAction; HealthMonitor.
- **Patches:** DeathEventPatch passes the victim's position.
- **Commands:** `.nyar region list|here` and `.nyar api regions`.
- **Other:** the contract from api 4 to api 5, and the six surfaces of 0.6.0. No new dependency and no new data file; events.json gains an optional key without a schema bump.

**Planned:** autonomously, by Claude under the owner's standing directive of 2026-09-28 ("proceed autonomously … present decisions in plan mode"). The owner settled the scope in design §9 D21 and Epic A28. Every choice this plan makes inside that scope is a `reversible` assumption with its fallback, listed under Assumptions for the owner to see. This plan is a child of the approved Epic `nyarlathotep`; its `## Child constraints` › regions entry and A28 govern it.

**Precondition:** step 1 starts only after raphael-api-admin has closed and v0.5.2 is tagged. If a no-go moved that release to 0.5.1 (raphael-api-admin S-9), S-9 below applies before step 1.

**Request:** the owner's scope request of 2026-09-28, settled as design §9 D21: "Every trigger and action takes `scope: Global` or a list of the game's world regions". Also Epic A28: "empowerment buffs only the region's NPCs; waves and hordes stay in or come from the region; `.nyar region list|here`".

## Definition of Done
- [ ] D1 · **Point in region** Logic/Regions.cs `RegionIndex` holds polygons (a region name, an axis-aligned box and x/z vertices) and answers `RegionOf(x, z)`: the box test first, then the even-odd crossing test over the vertices, the first polygon in index order that contains the point wins, and a point in none answers "None"; a polygon with fewer than 3 vertices or a non-finite coordinate is dropped when the index is built and counted · test: Nyarlathotep.Tests RegionTests (fails when: a point inside a concave polygon's notch answers that region, a point on the far side of a box-only overlap answers the box's region, a point outside every polygon answers other than None, a vertex on the query point's ray is counted twice, a NaN point answers a region, or a degenerate polygon enters the index uncounted)
- [ ] D2 · **Regions read from the game** Services/RegionMap.cs builds the RegionIndex once, after Core.IsReady, from every entity with ProjectM.Terrain.WorldRegionPolygon (disabled included), its PolygonBounds and its WorldRegionPolygonVertex buffer, naming each polygon by WorldRegionType.ToString(); it logs "regions: <p> polygons, <r> regions (<names>)" and never writes a component; the region names the game knows are the WorldRegionType names except None and Other (A16), and Logic's RegionNames list must equal them; a polygon tagged None, Other or a name outside RegionNames is left out of the index and counted in the boot line as "<k> untagged", checked at boot with "regions: names differ from the game: <diff>" as a warning · manual: Session 1 records the boot line and `.nyar region list` · cmd: pwsh tools/preflight.ps1 -LogCheck on Session 1's log → the "regions:" line present with p > 0, which -LogCheck asserts when the log loads Nyarlathotep 0.6.0 or later (A2; fixtures LogCheck/bad-regions-missing, bad-regions-zero and bad-regions-differ; a log with nyar lines whose Nyarlathotep version cannot be read fails, fixture LogCheck/bad-regions-noversion, and LogCheck/good-regions passes, A14) (fails when: the line is missing, p is 0, the names-differ warning appears, the loaded version is unreadable, or the good 0.6.0 fixture fails)
- [ ] D3 · **Scope in events.json** trigger and action each take an optional `scope`: the string "Global" (the default when absent) or an array of 1 to RegionNames.Count (10 at 1.1.12) distinct region names, matched case-insensitively and stored in the game's spelling; an unknown name, "None", an empty or longer array, a duplicate, or a non-string disables the event with "trigger.scope must be Global or 1-<RegionNames.Count> region names" or "action.scope must be …" (or "unknown region <name>"); a known name that the built index holds no polygon for disables the event with "region <name> is not on the map" (A3); every trigger type takes a scope (D4 gives each its meaning); SchemaVersion stays 1, templates accept the same key, and a definition without either key behaves exactly as in 0.5.x; the trigger scope and the action scope are independent, so a trigger on CursedForest with an action on FarbaneWoods is valid · test: Nyarlathotep.Tests EventValidationTests Scope (fails when: a trigger scope disjoint from its action scope is refused, an absent scope is not Global, "cursedforest" is not stored as CursedForest, an unknown, None, empty, longer-than-RegionNames.Count, duplicate or numeric scope is accepted, a name absent from the built index loads enabled, the maximum is a literal instead of RegionNames.Count, a trigger scope on any of the four trigger types is refused, or a 0.5.x definition's parse result changes)
- [ ] D4 · **Regional triggers** a trigger scope other than Global is a start condition for every trigger type (owner, S-5): VBloodKilled: DeathEventPatch passes the victim's x and z to TriggerBus.VBloodKilled, and TriggerRouter.VBloodKilled starts a definition only when its trigger scope contains RegionOf(x, z); a kill whose position cannot be read starts only Global definitions and logs "vblood kill: position unreadable" once per streak. Schedule and GameTime: when an occurrence is due, Logic ScopeGate.AnyPlayerIn(scope, positions) over the online players' x/z (read by Services only for a definition with a trigger scope) decides; with no player in a named region the occurrence is consumed as handled, not retried in the same slot, and logs "event <id>: skipped, no player in <regions>": the scheduler, on a no_player_in_region refusal from Start, keeps the slot marked handled (TriggerBus records the slot before the start) and EventRuntime.StartEvent logs that line instead of the generic refused-start line, the line chosen by a Logic helper in Logic/AdminLines.cs that the tests reach (A12, A19). Manual (`.nyar event start` and its api twin): with no player in a named region the start is refused with "no player is in the event's regions" (Outcome refusal state arg=scope reason=no_player_in_region, A8; the check runs in EventEngine.Start after Precedence.StartBlocker, reading the online players' x/z through a reader in its start controls that Services supply, A4: the reader is a defaulted `ControlState` parameter, so existing constructions are unchanged, A11, and a null reader gives zero positions, so a trigger-scoped start without one is refused (fails closed, A20); the player gate is keyed on the start, not the definition's type: only a start from the VBloodKilled route carries the kill position (a Start argument), which TriggerRouter already checked against the trigger scope, and it skips the gate; every other start (an admin's `.nyar event start` or api twin of a definition of any trigger type, Schedule, GameTime) needs a player in the trigger scope, A10, A18; in Start the gate runs after the Admin-location checks (D6) and before catalog.TryStart, A18; so the Schedule and GameTime skips and the Manual refusal share one check, the reason added to Logic/Outcome.cs Reasons with an OutcomeCodeTests row and to contract §5a). A Global trigger reads no position · test: Nyarlathotep.Tests TriggerActivationTests Region and EngineTests ScopeGate (fails when: a scoped definition starts on a kill outside its regions, a Global one fails to start, an unreadable kill position starts a scoped one, the log repeats within a streak, a scheduled or game-time occurrence starts with no player in its regions or is retried in the same slot, one fails to start with a player inside, a Manual start with no player inside starts or answers another code, an in-region kill with no player in the region fails to start, an admin start of a trigger-scoped VBloodKilled definition with no player in its regions starts, a trigger-scoped start with a null reader starts, the helper maps a no_player_in_region refusal to the generic refused-start line, or a Global trigger reads positions)
- [ ] D5 · **Regional empowerment** Eligibility.Decide gains the skip reason "region" after "other": a unit whose position (read into UnitFacts at sweep time) lies outside the action's scope is skipped; a unit whose position cannot be read is skipped with "region" when the scope is not Global; a unit already carrying the event's buff keeps it until the event ends, wherever it walks (S-4); a Global scope never reads a position · test: Nyarlathotep.Tests EmpowerEligibilityTests Region (fails when: an out-of-region unit is empowered, an in-region unit is skipped, an unreadable position is empowered under a regional scope, "region" is ordered before "denied" or "other", or a Global scope reads the position)
- [ ] D6 · **Regional waves** a SpawnWaves action with a regional scope: a Point location outside the scope disables the event at load with "action.location is outside action.scope" (checked at load; RegionMap builds before the definitions, D7, A6); an Admin location whose origin lies outside the scope refuses the start with "your position is outside the event's regions" (Outcome refusal badarg arg=location reason=out_of_region, the reason added to Logic/Outcome.cs Reasons with an OutcomeCodeTests row and to contract §5a's reason list); a ring point outside the scope counts as blocked in walkable-spawns' SpawnPoints search (isFree and in scope), so units move inward before falling back to the centre; the scope check runs before WalkBudget and spends none of it, so a spent budget never keeps a ring point outside the scope (A1); an Unchecked placement (spent budget, fall-open or no probe) whose ring point is outside the scope uses the wave centre, which the Point and Admin checks keep in scope (A9) · test: Nyarlathotep.Tests SpawningTests Region and EngineTests Region (fails when: a point outside the scope is used while an in-scope one is free, a spent walk budget keeps an out-of-scope ring point, a fall-open or no-probe wave places an out-of-scope ring point, a Point location outside the scope loads enabled, an Admin start outside the scope starts, out_of_region is missing from Reasons or §5a, or a Global scope calls the region check)
- [ ] D7 · **Regions unavailable** RegionMap builds in Core's deferred init before EventStore applies the definitions, so a boot never validates a regional definition without the index; when RegionMap finds no polygon, or its build throws, every definition with a regional scope is disabled with "regions unavailable" and Global ones run unchanged; HealthMonitor.Degraded adds "regions: unavailable"; `.nyar region here` replies "regions unavailable"; the build is retried at the next `.nyar event reload`; when the index is unavailable this message wins over D3's "region <name> is not on the map", which applies only to a built, non-empty index (A15) · test: Nyarlathotep.Tests DependencyFailureTests Regions, also run as dotnet test Nyarlathotep/Nyarlathotep.Tests --filter Regions_ → "Passed!" with at least 5 tests (fails when: a regional definition runs without an index, a Global one is disabled, the health entry is missing or stays after a successful rebuild, a throwing build stops the boot, definitions are applied before the build, an unavailable index disables with "region <name> is not on the map", or the filter runs fewer than 5 tests)
- [ ] D8 · **Admin commands** `.nyar region list` (adminOnly) replies one line per region in RegionNames order, "<name> (<display name>): <n> events", n counting enabled definitions whose trigger or action scope names it, then "global: <g> events"; `.nyar region here` (adminOnly) replies "you are in <name> (<display name>)" or "you are outside every region" from the admin's position; any other verb replies "argument must be list or here"; neither changes anything · test: Nyarlathotep.Tests RegionTests Lines (fails when: a line's text differs from Design › UX, a count includes a disabled definition, or the display name is not the CamelCase name split at capitals with `_` as a space)
- [ ] D9 · **Scope in replies** `.nyar event info` shows "trigger scope: <Global|names>" and "action scope: <Global|names>"; `.nyar event list` appends " [<names>]" to a regional event; `.nyar event set <id> trigger.scope|action.scope <Global|name,name>` edits the key through the existing editor and gateway row (SetEventField); an edit to a running event applies at its next start, DefinitionEditor's rule for every field (A5); a `trigger.type` change clears `trigger.scope` with the other trigger keys (Logic/EventAdmin.cs's reset), so the event returns to a Global trigger; an announcement may use the placeholder {region}, the action scope's display names joined by ", " or "the world" for Global · test: Nyarlathotep.Tests EventAdminTests Scope and AnnouncerTests Region (fails when: info lacks either line, a Global event gains a list suffix, `set` accepts a name `.nyar event reload` would reject, a `trigger.type` change keeps `trigger.scope`, a scope edit changes a running instance, or {region} renders other than the stated text)
- [ ] D10 · **Wire at api 5** `.nyar api regions [page]` (anyone) replies `[NYAR:region] id=<name> events=<n>` rows paged per contract §4, n counting active events whose action scope names the region; `[NYAR:def]` and `[NYAR:event]` rows gain `region=<name,…>` (action scope; `-` for Global) as their last key; event-start and event-end pushes gain `region=` after `secs`; Wire.Api is 5 and docs/RAPHAEL_INTEGRATION_CONTRACT.md marks the `region` tag and `regions` command IMPLEMENTED (api 5), moves §10.2's regions row and §10.4's region keys into §3, and says "**Current api:** 5"; docs/RAPHAEL_HANDOFF_API4.md gains the region picker note · test: Nyarlathotep.Tests WireFormatTests Region, ApiLinesTests Region and ContractDocTests (fails when: a key set or order differs from the contract's example, a Global event carries other than `region=-`, a line exceeds 480 bytes with 10 regions, Wire.Api differs from the contract, or a row still PLANNED); cmd: pwsh tools/preflight.ps1 → "wire contract: <n> tags, <m> api commands, all documented (api 5)"
- [ ] D11 · **Authorization and static checks** `region` is one adminOnly command with the ready guard; `api regions` is public and allow-listed in $script:PublicCommands; RegionMap is read-only, so no [Mutating] method and no ActionKind is added; the scope edit runs through the existing SetEventField row; ControlCases gains a row per control of this plan, the set being every D-item whose evidence carries a `(fails when:` clause (D1-D11, D13-D16), and ControlCaseTests reads this plan from the test Resources · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (…)"; pwsh tools/preflight.ps1 → PREFLIGHT OK with "commands: <n> admin-only, <m> public (allow-listed)"; pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests" (fails when: `region` loses adminOnly or its ready guard, `api regions` is public without its allow-list entry, fixture Commands/bad-3 (a `region` command without adminOnly) passes, or a control lacks its row)
- [ ] D12 · **Regions in game** Session 1 on the local server (127.0.0.1:9876): `.nyar region here` in Farbane Woods, Dunley Farmlands and the Cursed Forest names each; an Empower event of Faction_Undead scoped to CursedForest shows, via `.nyar debug here`, a carrier on an undead unit in the Cursed Forest and none on one in Farbane Woods; a SpawnWaves event with an Admin location scoped to FarbaneWoods refuses to start in Dunley and starts in Farbane; a VBloodKilled event scoped to FarbaneWoods starts on a Farbane V Blood kill; with Debug.TimingLog on, every "tick timing" line during the empowerment sweep shows avg under 5 ms (Epic D24) · manual: Session 1 in docs/features/REGIONS.md › Test results records each observed line
- [ ] D13 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/REGIONS.md › Test results, with its -LogCheck line in docs/audits/regions.md before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/preflight-checks.json gains `childDocs`, `snapshotSessions` (1) and `dataTables` entries for regions; tools/data-inventory.json has a "regions › <artifact>" entry per Design › Data row · cmd: pwsh tools/preflight.ps1 -AuditOf regions → "audit steps: regions 3/3 pre, 3/3 post, 3/3 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf regions → "session logs: regions 1/1 checked; snapshots 1/1 from session 1"; pwsh tools/preflight.ps1 -Paths -DeclaredOf regions → "paths: <n> walked, all in manifest; declared: <d>/<d> in regions" (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a walked path matches no manifest glob or is undeclared, a %TEMP%\nyar-* folder outlives its session, or a Data row lacks an inventory entry or field)
- [ ] D14 · **Secrets and privacy** this child adds no credential; no wire line, announcement or log line carries a player's position or name because of a region (the `region=` key names only an event's configured scope, and `region here` answers the admin alone); no tracked file holds a SteamID digit run or the owner's mail name except lines quoting the pattern · cmd: pwsh tools/preflight.ps1 → "secrets: none (<n> files scanned…)"; then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern; test: Nyarlathotep.Tests PrivacyTests Region (fails when: a token shape is in the scanned set, which the existing Secrets fixtures plant under -SelfTest, a region line or push carries a player position, name or SteamID, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D15 · **Tick cost bounded** a regional empowerment sweep reads one position and runs at most RegionIndex's polygon count box tests per unit, bounded by EmpowerBatchPerTick (200); RegionIndex answers 10,000 random points over a real-sized index (the polygon count read from the boot line in step 3 into Nyarlathotep.Tests/Fixtures/region-index-size.txt; the test is added in step 3 with it) with at most the polygon count box tests and at most the box hits' polygon tests per point, counted by the index; the elapsed time is logged by the test with a 200 ms ceiling (A7) · test: Nyarlathotep.Tests RegionTests Cost (fails when: the fixture is missing or holds 0 polygons, a Global scope reads positions, a unit costs more box tests than polygons, a point costs more polygon tests than its box hits, or the 10,000-point run exceeds 200 ms)
- [ ] D16 · **Release 0.6.0** csproj Version and thunderstore.toml versionNumber are 0.6.0; both changelogs and both READMEs describe regional scope, `.nyar region list|here` and api 5; the README states that 0.5.x disables, after a rollback, a definition carrying `scope` (unknown key) or an announcement using `{region}` (unknown placeholder); the annotated tag v0.6.0 is pushed and the GitHub pre-release carries the tcli zip; after a failed or ambiguous `gh release create` the step runs `gh release view v0.6.0` and retries only when the release is absent; no tcli publish · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.2 -To v0.6.0 -Plan regions → "rollback gate: 4/4"; then pwsh tools/release-verify.ps1 -Tag v0.6.0 -Asset kdpen-Nyarlathotep-0.6.0.zip → "release verify: hashes equal" (fails when: a surface differs, 0.5.2 does not initialize on 0.6.0's files, the repository revert of v0.5.2..v0.6.0 is not clean, a Rollback route is missing or names another range, the tag is missing or unpushed, the release has no zip, or the hashes differ)

## Purpose & typical use
- **Who:** a server admin who wants events to feel local. They'd say "when someone kills the Cursed Forest boss, the undead there should get stronger, not the whole map".
- **Job:** an event can be limited to named regions of the map: the V Blood trigger fires only for kills there, empowerment reaches only NPCs standing there, and waves spawn only there. With no scope everything stays global, exactly as in 0.5.x.
- **Coexists with:**
  - faction-empowerment's carrier and sweep, gaining one skip reason (D5);
  - walkable-spawns' point search, gaining one blocked condition (D6);
  - raphael-api-admin's twins, through which `event set … scope` also works;
  - the later children: event-spawns' locations, anti-farming's region filter and outbreak's hotspots all use RegionIndex.

## Use cases
### Typical
The admin runs `.nyar event set undead-rising action.scope CursedForest`. When the Cursed Forest V Blood falls, the undead in the forest get the carrier buff (D5), while undead in Farbane Woods do not. `.nyar event info undead-rising` shows "action scope: CursedForest" (D9), and Raphael shows `region=CursedForest` on the row (D10).

### Minimal stretch
- **Least use (8.1):** an events.json without a `scope` key runs exactly as in 0.5.x (D3). A server where the region polygons cannot be read keeps every Global event running and disables only the regional ones, visibly (D7).
- **Once and never again (8.2):** the region index is built once per boot and rebuilt only on `.nyar event reload` after a failure (D7). Removing the key puts an event back to Global.

### Maximal stretch
- **Volume (9.1):** an Empower sweep of 200 units a tick adds 200 position reads and at most 200 × (polygon count) box tests; the lookup is measured (D15) and the tick timed in Session 1 (D12).
- **Abuse (9.2):** players cannot reach any of it: `region` is admin-only, `api regions` is a read, and the scope comes only from events.json and admin edits (D11). A crafted scope with 1,000 names is refused past RegionNames.Count (D3).
- **Repeated use (9.3):** `region here` and `region list` are pure reads; a repeated `event set … scope` writes the same value, as any `set` does.

## Business rules
1. **Scope (4.1, D3):** `scope` is "Global" (the default) or 1 to RegionNames.Count distinct WorldRegionType names except None and Other (A16). A trigger scope and an action scope are independent of each other. The game's spelling is stored; matching is case-insensitive.
2. **Where each scope applies (4.1, D4, D5, D6):**
   - **trigger.scope:** on VBloodKilled, the victim's position; on Schedule, GameTime and Manual, whether at least one online player stands in a named region when the trigger fires (D4).
   - **action.scope, Empower:** the unit's position at the sweep that would buff it.
   - **action.scope, SpawnWaves:** the Point location at load, the admin's origin at start, and every ring point when placed.
3. **Invariants (4.2):** a Global scope never reads a position (D5, D6, D15). A unit is never unbuffed early because it walked out (S-4). Nothing is written to any game entity (D2).
4. **Time (4.3):** positions are read when a decision is made (the kill, the sweep, the start, the placement), never cached across ticks. The region index is static for a server build and built once per boot (D2).
5. **Precedence (4.4):** the existing controls come first (Precedence.StartBlocker, the caps, Eligibility's earlier skips), then the scope: "region" follows "other" in SkipReasons (D5), and the scope check follows walkable's free check in the point search (D6). There is no exception path and no actor can bypass the scope.
6. **"Every" sets (4.5):**
   - "Every trigger and action" (D21) is the trigger and the action of each definition, over the four trigger types of Logic/Model.cs TriggerType (Manual, Schedule, GameTime, VBloodKilled), each with a D4 case; a later trigger type adds its own case to D4's route in its child.
   - "Every region" is the WorldRegionType names except None and Other (A16), which D2 checks against the game at boot.
   - "Every control" is every D-item whose evidence carries a `(fails when:` clause; ControlCaseTests reads this plan (D11).
   - "Every path this child writes" is computed by `-Paths -DeclaredOf regions` (D13; known exclusion: a path created and deleted inside one step, S-7).

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:** Logic/Model.cs (Trigger, SpawnWavesAction, EmpowerAction, EventDefinition); Logic/Validation.cs (ParseTrigger, ParseAction, ParseEmpower, ParseSpawnWaves, ParseLocation, EventKeys); Logic/Empowerment.cs (UnitFacts, Eligibility, CarrierLedger.TickEvent); Logic/Engine.cs (TriggerRouter.VBloodKilled, WavePlan); walkable-spawns' Logic/Spawning.cs SpawnPoints; Services/EmpowerAction.cs Ops.Facts; Services/TriggerBus.cs; Patches/DeathEventPatch.cs.
- **Writes:**
  - Logic/Regions.cs (new): RegionIndex, RegionNames, Scope (Global or names), IRegionCatalog, and the display-name rule.
  - Logic/Model.cs: Trigger and the two action records gain `Scope Scope` (default Global).
  - Logic/Validation.cs: the scope parse and the location-in-scope check, through IRegionCatalog.
  - Logic/Empowerment.cs: UnitFacts gains X and Z (read only for a regional scope); Eligibility gains "region".
  - Logic/Engine.cs: TriggerRouter.VBloodKilled takes the position; ScheduleDue and the GameTime candidates pass through ScopeGate; Logic/Regions.cs holds ScopeGate.
  - Logic/Spawning.cs: the point search's isFree wraps the scope check.
  - Logic/EventAdmin.cs (info, list, set), Logic/AnnouncerCore.cs ({region}), Logic/ApiLines.cs and Logic/Wire.cs (api 5), Logic/Validation.cs AllowedPlaceholders.
  - Services/RegionMap.cs (new, read-only); Services/EmpowerAction.cs (positions into facts); Services/TriggerBus.cs and Patches/DeathEventPatch.cs (the kill position); Services/HealthMonitor.cs ("regions: unavailable").
  - Commands/RegionCommands.cs (new) and Commands/ApiCommands.cs (`regions`).
- **What breaks if wrong:** a wrong point-in-polygon test buffs or spawns in the wrong region (D1, Session 1); a scope that is not optional breaks every 0.5.x events.json (D3's unchanged-parse case).
- **Shared types (5.3):** Trigger, SpawnWavesAction and EmpowerAction gain a defaulted parameter, so every existing constructor call compiles unchanged; UnitFacts gains two defaulted fields. `[NYAR:def]` and `[NYAR:event]` gain a last key, additive per contract §7.

### External — dependencies and their failure behaviour
- **V Rising server 1.1.12 (VampireReferenceAssemblies 1.1.12-r99041-b2) (6.1):** ProjectM.Terrain `WorldRegionPolygon` (WorldRegion, PolygonBounds), the buffer `WorldRegionPolygonVertex` (VertexPos) and the enum `WorldRegionType`, read the way KindredCommands' Services/RegionService.cs reads them (re-implemented, not copied). Record variants Session 1 samples: three regions and an outside point. No quota and no cost.
- **Raphael (6.1):** reads `region=` and `api regions` from api 5; an api 4 parser skips the new key (contract §7).
- **Build and release tooling (6.1):** versions recorded at each pre-audit; floors git 2.40, gh 2.40 (the owner's account), tcli 0.2.4 (`tcli build` only), Codex CLI (`exec -s read-only`), pwsh 7.2, a .NET SDK building net6.0.
- **Failure behaviour (6.2):**
  - No polygons, a throwing build, or a names mismatch: D7 (regional events disabled, Global unchanged, health entry) and D2's warning.
  - A position that cannot be read: a scoped decision fails closed (not buffed, not started, not placed there), a Global one never asks (D4, D5, D6).
  - Garbage polygons (fewer than 3 vertices, NaN) are dropped and counted (D1).
  - A game update renaming a region does not throw: WorldRegionType.ToString returns the new name, D2 warns that the names differ, and a scope naming a region the index lacks disables its definition (D3, A3).
  - The dev tools get no injected failures: git, gh, tcli and Codex stop the step under $ErrorActionPreference Stop; an ambiguous `gh release create` is checked with `gh release view` before any retry (D16); a Codex run at capacity or timed out is retried and not counted as a round.
- **No sandbox (6.3):** the dev server (127.0.0.1:9876, save-data-nyardev), wrapped by tools/dev-snapshot.ps1 (D13). Unit tests use hand-built polygons and a fake IRegionCatalog, never game types.

## Design
### Data
| Artifact | Where | Owner | Kept | Deleted |
|---|---|---|---|---|
| region index | memory | RegionMap | the process | rebuilt on boot and on reload after a failure (D7) |
| `scope` keys | BepInEx/config/Nyarlathotep/events.json | the operator and admins | as long as the definition | by editing or deleting the definition |
| polygon-count fixture for D15 | Nyarlathotep.Tests/Fixtures/region-index-size.txt (committed) | the repository | for ever | only by a later commit |
| session log copies | %TEMP%\nyar-s*-logs | Claude | the session | deleted in the same session (D13) |
| snapshot | %TEMP%\nyar-snap-* | tools/dev-snapshot.ps1 | the session | on restore (D13) |
| session marker | %TEMP%\nyar-session | tools/dev-snapshot.ps1 | the session | when the session ends (D13) |
| review prompts | %TEMP%\dod-review-*.txt | the dod skill | until the next prompt | replaced by it; never committed |
| Claude's scratchpad (Codex prompts, outputs) | the per-session scratchpad, outside the repository | Claude | the session | removed with it; SteamIDs redacted before any Codex prompt |
| build outputs | Nyarlathotep/**/bin, obj, *.binlog (git-ignored) | Claude | until the next build | overwritten by each build; never shipped |
| release zip | Nyarlathotep/Nyarlathotep/build/*.zip, dist/ (git-ignored) | Claude | until the next release | replaced by the next tcli build; its SHA-256 is kept in the audit |
| release and drill temp folders | %TEMP%\nyar-rel-*, nyar-rollback-*, nyar-drill-*, nyar-{selftest,depsuite,snaptest,drilltest}-* | release-verify, rollback-gate, preflight -SelfTest | the run | deleted by the script that made it (D13) |
| review pages | docs/dod/regions.review.html, docs/dod/regions.html | the dod skill | until the next render | overwritten by it |
| dev-server files | the server's Nyarlathotep.dll, cfg, events.json and state.json with .bak and .tmp, save-data-nyardev, NyarDev.log, LogOutput.log | Claude (Session 1) | the session | restored by tools/dev-snapshot.ps1 (D13) |
| tag and GitHub release v0.6.0 | origin | the owner | for ever (exist only once) | never deleted; a bad one is retitled (Epic S-19) |
| plan, reviews, audit, feature doc | docs/ (committed) | the repository | for ever | only by a later commit |

Inputs (3.1): the `scope` values (D3), the admin's position for `region here` (D8), and positions at decisions (D4-D6). Outputs (3.2): the replies, rows and pushes of D8-D10. Migration (3.4): none; the key is optional, SchemaVersion stays 1, and 0.5.x disables a scoped event as an unknown key after a rollback, which is fail-safe and is stated in the README (D16). Every row has a tools/data-inventory.json entry (D13).

### States
- **Empty and first run (7.1):** no events.json scope keys, so everything is Global (D3). A boot before the polygons exist cannot happen: RegionMap builds after Core.IsReady; if it finds none, D7.
- **Partial and error:** a regional event whose Point is outside its scope is disabled with a reason, and the others load (D6).
- **Concurrent use (7.2):** every read and decision runs on the server's main thread; the index is immutable after its build, and a rebuild swaps it in one assignment. Actors: admins, the scheduler and triggers, Claude (builder and tester); Claude never runs two sessions on one server (D13).
- **Stale data, cancel and re-entry (7.3):** positions are read at decision time, never cached (Business rules 4). A buffed unit that walks out keeps its buff until the event ends (S-4). Stop, purge and restart are unchanged.

### Permissions
- **Actors (2.1):**
  - Admins run `region list|here`, set scopes and see the scope in info.
  - Anyone may run `api regions` (a read of configured scopes, no positions).
  - The System actor fires scoped triggers and sweeps; it cannot change a scope.
  - Players have no command here beyond the public read.
  - Unauthenticated connections never reach chat; the operator may edit events.json or remove the DLL.
  - Claude builds, deploys to the dev server and pushes; the owner alone publishes. Codex reads only.
- **Unauthorised path (2.2):** `region` is adminOnly (D11); a player's `region here` gets VCF's denial line.
- **Ownership (2.3):** a scope belongs to its definition; any admin may edit it (Epic Design › Permissions). `region here` reads only the caller's own position.

### UX
- **Where it lives (11.1):** the `scope` key in events.json and `.nyar event set`; the README's event section documents it with the region names (D16); `.nyar region list` shows the names to type.
- **Feedback (11.2):** the lines of D8 and D9, exactly:
  - "<name> (<display name>): <n> events", "global: <g> events";
  - "you are in <name> (<display name>)", "you are outside every region", "regions unavailable", "argument must be list or here";
  - "trigger scope: <Global|names>", "action scope: <Global|names>";
  - the refusals of D3 and D6.
- **Accessibility (11.3):** chat lines under 480 bytes, plain text, no meaning by colour, keyboard only; the game offers no screen reader, an inherited limitation the README states. Session 1 records that each line shows whole (D12).
- **Activation (11.4):** a scope activates when a definition carries one; the admin sees it in info and list (D9) and on the Raphael row (D10). Nothing activates without a key.

## Security
- **Authorization (10.1):** `region` adminOnly; `api regions` public and allow-listed; the scope edit through the existing SetEventField row (D11).
- **Injection (10.2):** region names are matched against the fixed RegionNames list; nothing else is accepted (D3); JSON is written by System.Text.Json.
- **Secrets (10.3):** none added; the Secrets check and the privacy grep (D14). gh keeps its token in the Windows credential store; TCLI_AUTH_TOKEN lives only in the owner's environment while the owner publishes.
- **Personal data (10.4):** no line tells anyone where a player is; `region here` answers the admin about the admin (D14).

## Failure & observability
- **What the admin sees (12.1):**

| Failure class | Admin sees | Next action |
|---|---|---|
| Regions unavailable (D7) | "regions: unavailable" in `.nyar status`, the health line and the login notice; regional events disabled with "regions unavailable" | report the server build; `.nyar event reload` retries |
| Names differ from the game (D2) | the boot warning naming the difference | report it; a game update renamed a region |
| Bad scope (D3) | the event disabled with its reason in `.nyar event info` | fix the name (`.nyar region list` shows them) |
| Point outside its scope (D6) | the event disabled with "action.location is outside action.scope" | move the location or widen the scope |
| Admin start outside the scope (D6) | "your position is outside the event's regions" | walk into the region |
| Manual start of a trigger-scoped event with no player in its regions (D4) | "no player is in the event's regions" | wait for a player there, or set `trigger.scope Global` |
| Scheduled or game-time occurrence skipped (D4) | the log line "event <id>: skipped, no player in <regions>" | none; the next occurrence tries again |

- **Logs (12.2):** the boot "regions:" line (D2); "vblood kill: position unreadable" once per streak (D4); the existing sweep counts gain "region" in their skip reasons (D5).
- **Knowing it is broken (12.3):** the degraded entry reaches the admin on login (D7); -LogCheck covers every dev session (D13).
- **Failing cases (12.4):** each check this plan introduces:

| Check | Reports a failure on | Stays silent on | Empty input |
|---|---|---|---|
| RegionTests (D1) | a notch point counted inside, a double-counted vertex | the stated polygons | an empty index: every point None |
| EventValidationTests Scope (D3) | an unknown or 11-name scope accepted | "Global" and valid names | absent key: Global |
| TriggerActivationTests Region (D4) | a scoped start outside its regions | an in-region kill | unreadable position: only Global starts |
| EmpowerEligibilityTests Region (D5) | an out-of-region unit buffed | an in-region unit | Global scope: no position read |
| SpawningTests / EngineTests Region (D6) | an out-of-scope point used while one is free | in-scope placement | Global: no scope call |
| DependencyFailureTests Regions (D7) | a regional event running without an index | Global events running | no polygons: regional disabled |
| RegionTests Lines (D8) | a line off Design › UX | the stated lines | no events: "global: 0 events" |
| EventAdminTests / AnnouncerTests (D9) | a missing scope line, a wrong {region} | the stated lines | Global: "the world" |
| WireFormatTests / ApiLinesTests / ContractDocTests (D10) | a key off the contract, Wire.Api 4 against 5 | api 5 rows | no regions scoped: `count=` of the region list with events=0 |
| Commands (preflight, D11) | fixture Commands/bad-3 | the real tree | the existing Commands/empty |
| ControlCaseTests (D11) | a regions control without its row | the real table | "the plan lists no control id" (fail) |
| PrivacyTests Region (D14) | a position or name in a region line | the built lines | zero region lines built: fail |
| RegionTests Cost (D15) | a point costing more box tests than polygons or more polygon tests than box hits, or the run over 200 ms | the measured index | a missing fixture or 0 polygons: fail |
| LogCheck regions (D2) | fixtures bad-regions-missing, -zero, -differ and -noversion | a pre-0.6.0 log and LogCheck/good-regions | an empty log (existing LogCheck/empty): fail |
| Secrets (D14, existing) | Secrets/bad…bad-9 | Secrets/good | Secrets/empty |

Each fixture is registered under `-SelfTest` in the step that adds its check; `-SelfTest` fails when a bad or empty fixture passes or a good one fails.

One evidence command per gating probe:

| Probe | Command | Item |
|---|---|---|
| 2.1 actors | pwsh tools/preflight.ps1 -AuthSuite | D11 |
| 3.3 persistence | pwsh tools/preflight.ps1 -Paths -DeclaredOf regions | D13 |
| 4.4 precedence | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter "FullyQualifiedName~EmpowerEligibilityTests|FullyQualifiedName~SpawningTests" | D5 D6 |
| 6.2 dependency failure | dotnet test Nyarlathotep/Nyarlathotep.Tests --filter Regions_ | D7 |
| 10.1 authorization | pwsh tools/preflight.ps1 -AuthSuite | D11 |
| 10.3 secrets | pwsh tools/preflight.ps1 ("secrets: none") | D14 |
| 12.4 failing cases | pwsh tools/preflight.ps1 -SelfTest | D11 |
| 14.3 rollback | pwsh tools/rollback-gate.ps1 -From v0.5.2 -To v0.6.0 -Plan regions | D16 |
| 14.4 paths | pwsh tools/preflight.ps1 -Paths -DeclaredOf regions | D13 |

## Performance
- **Budget and hot path (13.1):** the scheduler tick stays under 5 ms on average (Epic D24). The hot path is the regional Empower sweep: one position read and at most (polygon count) box tests per unit, 200 units a tick at most; the point-in-polygon runs only inside a matching box. D15 measures the lookup; Session 1 times the tick (D12).
- **Limits (13.2):**

| Bound | Source | At the bound | Valid case excluded |
|---|---|---|---|
| 1 to RegionNames.Count regions per scope | the game names 10 regions at 1.1.12; a later update adding one raises the bound with RegionNames (D2 warns until then) | a name beyond the count refused (D3) | none |
| 200 units a sweep tick | faction-empowerment EmpowerBatchPerTick | later units wait for the next tick | none new |
| 480 bytes a line | contract §1 | `region=` with 10 names fits | none |
| 10 rows a page | contract §4 | paged | none |

## Build plan
Every step runs inside the Epic's `## Rollout` › Procedure: a pre-audit and a post-audit in docs/audits/regions.md (template docs/audits/README.md), `/code-review`, and a Codex read-only cross-inspection of the step's diff (`codex exec -s read-only -c features.experimental_windows_sandbox=true`, prompt via stdin, SteamIDs redacted) until "VERDICT: READY", its line written to the audit. Compile check: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`. Tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests`.
1. **Region index and scope model.**
   - Pre-audit; create docs/audits/regions.md (rollback base v0.5.2) and docs/features/REGIONS.md with a `## Test results` section; add the `childDocs`, `snapshotSessions` and `dataTables` entries and the data-inventory rows (D13).
   - Add Logic/Regions.cs (RegionIndex, RegionNames, Scope, IRegionCatalog, display names) with RegionTests; add `scope` to Model and Validation with EventValidationTests Scope; Services/RegionMap.cs with its boot line and D7's disabled path, HealthMonitor's entry and DependencyFailureTests Regions.
   - Satisfies D1, D2, D3, D7.
2. **Scope in triggers, empowerment, waves, commands and wire.**
   - DeathEventPatch and TriggerBus pass the kill position; TriggerRouter filters (D4). UnitFacts positions and the "region" skip (D5). The scope check in the point search and the Point and Admin location checks (D6).
   - Commands/RegionCommands.cs and the `set` scope fields, info and list lines, {region} (D8, D9).
   - `api regions`, `region=` keys and pushes, Wire.Api = 5, the contract, docs/NYARLATHOTEP_DESIGN.md §6 and docs/RAPHAEL_HANDOFF_API4.md (D10).
   - Fixture Commands/bad-3, the allow-list entry, ControlCases rows and ControlCaseTests' plan list with the csproj link (D11); PrivacyTests Region (D14).
   - Satisfies D4, D5, D6, D8, D9, D10, D11, D14.
3. **Session 1 (owner) and release 0.6.0.**
   - Stop the server; -LogCheck on the last logs; `pwsh tools/dev-snapshot.ps1 -Save rg1`; deploy with `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release`; boot the dev world ($env:SteamAppId='1604030'; VRisingServer.exe -persistentDataPath .\save-data-nyardev -serverName "Nyar Dev" -saveName nyardev -logFile .\logs\NyarDev.log); read the boot "regions:" line and record the polygon count into Nyarlathotep.Tests/Fixtures/region-index-size.txt; add RegionTests Cost over it (D2, D15).
   - Write the exact numbered Session 1 steps (server 127.0.0.1:9876) into docs/features/REGIONS.md and hand them to the owner; record every observed line (D12); stop after an autosave; -LogCheck; read every [Error] and [Warning] line; restore the snapshot.
   - Release 0.6.0 on the six surfaces; `tcli build`; annotated tag v0.6.0; `pwsh tools/rollback-gate.ps1 -From v0.5.2 -To v0.6.0 -Plan regions` (4/4); privacy grep; push; GitHub pre-release; release-verify.
   - -AuditOf, -SessionsOf and -Paths -DeclaredOf regions; `dod close regions`. The owner publishes.
   - Satisfies D2, D12, D13, D14, D15, D16.

## Work breakdown
- W1 · **Regions**
- W1.1 · **Index, model and failure** · items: D1 D2 D3 D7 · steps: 1
- W2 · **Scope in use**
- W2.1 · **Triggers, empowerment and waves** · items: D4 D5 D6 · steps: 2
- W2.2 · **Commands, replies and wire** · items: D8 D9 D10 D11 D14 · steps: 2
- W3 · **Verification and release**
- W3.1 · **Session, records and release** · items: D12 D13 D15 D16 · steps: 3

## Rollout
### Shipping
One release, 0.6.0, a GitHub pre-release; D16 ends there, with release-verify. The owner publishes to Thunderstore, outside the plan. Nothing is switched on by it: without a `scope` key every event stays Global, and every pillar still ships off (Epic D4). Who turns it off: an admin removes the key or sets it to Global (`.nyar event set <id> action.scope Global`), or stops the event; the operator installs 0.5.2.

### Compatibility
- A 0.5.x events.json loads unchanged (D3); SchemaVersion stays 1.
- api 4 lines keep every key; `region=` is additive, and api goes from 4 to 5 (contract §7).
- Every human reply of 0.5.2 keeps its text. This child only adds lines: the info lines and list suffix (D9) and the out-of-region refusal in Logic/AdminLines.cs (D6). raphael-api-admin's human-reply capture (Nyarlathotep.Tests/Fixtures/human-replies-0.5.1.txt) therefore stays unchanged, and HumanReplies still passes on it; a reply whose text changes would add a dated row set (its Review 4 F4 rule).

### Rollback
- **In the repository:** `git revert --no-edit v0.5.2..v0.6.0`, drilled by the rollback gate with -Plan (D16). Commits in the range that are not this child's are re-applied after a revert with `git cherry-pick`, listed from `git log v0.5.2..v0.6.0` minus those touching this child's exclusive paths.
- **On the dev server during the build:** Session 1 is wrapped by tools/dev-snapshot.ps1 (D13).
- **On a server:** install the 0.5.2 DLL. A definition carrying `scope` is disabled by 0.5.2 as an unknown key, and one whose announcement uses `{region}` as an unknown placeholder (Validation.cs), which is fail-safe (no event runs map-wide by surprise); the admin removes the key or stays on 0.6.0. The rollback gate proves 0.5.2 initializes on 0.6.0's files.
- **Published release:** tags and releases are never deleted; a bad release is retitled and versions move forward only (Epic S-19).
- **Commit range:** v0.5.2..v0.6.0

### Paths walked
Walking the Build plan. The walker (-Paths, Epic D33) reads git's tracked, untracked and ignored files, the dev server's paths, %TEMP%\nyar-* and the remote tags and releases.
- **Step 1:**
  - docs/audits/regions.md, docs/features/REGIONS.md, tools/preflight-checks.json, tools/data-inventory.json, tools/paths-manifest.txt.
  - Nyarlathotep/Nyarlathotep/Logic/{Regions,Model,Validation,Templates,Dependency}.cs, Nyarlathotep/Nyarlathotep/Services/{RegionMap,HealthMonitor,EventStore}.cs, Nyarlathotep/Nyarlathotep/Core.cs (the boot call).
  - Nyarlathotep/Nyarlathotep.Tests/{RegionTests,EventValidationTests,DependencyFailureTests,FakeStores}.cs.
- **Step 2:**
  - Nyarlathotep/Nyarlathotep/Logic/{Empowerment,Engine,Spawning,EventAdmin,AnnouncerCore,ApiLines,Wire,Validation,CommandArgs,Outcome,AdminFlows,AdminLines,Precedence}.cs, Nyarlathotep/Nyarlathotep/Services/{EmpowerAction,TriggerBus,WaveAction,SpawnTracker,Pusher,EventRuntime,AdminOps}.cs, Nyarlathotep/Nyarlathotep/Patches/DeathEventPatch.cs, Nyarlathotep/Nyarlathotep/Commands/{RegionCommands,ApiCommands}.cs.
  - Nyarlathotep/Nyarlathotep.Tests/{TriggerActivationTests,EmpowerEligibilityTests,SpawningTests,EngineTests,EventAdminTests,FakeStores,AnnouncerTests,WireFormatTests,ApiLinesTests,ContractDocTests,PrivacyTests,ControlCases,ControlCaseTests,OutcomeCodeTests}.cs, Nyarlathotep/Nyarlathotep.Tests/Nyarlathotep.Tests.csproj (links this plan).
  - docs/RAPHAEL_INTEGRATION_CONTRACT.md, docs/NYARLATHOTEP_DESIGN.md, docs/RAPHAEL_HANDOFF_API4.md.
  - tools/preflight.ps1 ($script:PublicCommands, LogCheck's regions line), tools/preflight-checks.json, tools/preflight-fixtures/Commands/bad-3/**, tools/preflight-fixtures/LogCheck/{bad-regions-missing,bad-regions-zero,bad-regions-differ,bad-regions-noversion,good-regions}/**, tools/paths-manifest.txt.
- **Step 3:**
  - Nyarlathotep/Nyarlathotep.Tests/Fixtures/region-index-size.txt, Nyarlathotep/Nyarlathotep.Tests/RegionTests.cs (Cost).
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - The remote tag v0.6.0 and the GitHub release v0.6.0 (`remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt), and tools/paths-manifest.txt itself.
  - docs/features/REGIONS.md, docs/audits/regions.md, docs/dod/regions.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Session 1, on the server:** BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json{,.bak,.tmp}, save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log.
- **Outside the repository** (existing `temp:` lines): %TEMP%\nyar-snap-*, %TEMP%\nyar-s*-logs, %TEMP%\nyar-session, %TEMP%\nyar-{selftest,depsuite,snaptest,drilltest}-*, nyar-rel-*, nyar-rollback-*, nyar-drill-*.
- **Build outputs** (existing ignored globs): Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/**, Nyarlathotep/Nyarlathotep/build/**.
- **Review process:** docs/dod/regions.reviews.md, docs/dod/regions.review.html and docs/dod/regions.html.

## Out of scope
- **Considered and excluded (15.1):**
  - Removing a carrier when its unit leaves the region (S-4).
  - Admin-drawn areas (circles, polygons): anti-farming's zones.
  - Friendly aliases in events.json ("forest", "farbane"): only the game names, case-insensitive (S-3).
- **Deferred (15.2):** horde hotspots filtered by region (outbreak); anti-farming's region filter; event-spawns' AroundPlayer filtered by region; each uses RegionIndex.

## Also considered
- **Documentation:** the six surfaces (D16), the contract and handoff (D10), docs/features/REGIONS.md. **Decommissioning:** contract §10's regions rows (moved, D10). **Ownership:** the server admin; the runbook is the README's kill switch and `event set … Global`.
- **Compliance:** none; no personal data is stored or sent (Security 10.4).
- **Localisation and time formats:** region names are the game's identifiers; display names are the identifiers split at capitals, English like every `.nyar` line.
- **Running cost:** none beyond the bounded lookups (Performance); no quota.
- **Success measurement:** Session 1's observed lines and timing (D12); no analytics.
- **Support tooling:** `.nyar region here`, `region list`, the boot line and the health entry (D2, D7, D8).

## Assumptions
- S-1 · validated · Every trigger and action takes `scope: Global` or a list of the game's world regions, built as child regions after raphael-api-admin · source: owner decision in plan mode 2026-09-28 (design §9 D21, D20; Epic A28)
- S-2 · validated · Plans are reviewed by a fresh-context subagent (up to 3 rounds); Codex cross-inspects code diffs only · source: owner Decision 7, plan mode 2026-09-28 (design §9 D26)
- S-3 · reversible · events.json takes only the game's WorldRegionType names, matched case-insensitively; no aliases · fallback: a corrected amendment adds an alias table in Logic/Regions.cs, which the parse maps to the game name
- S-4 · reversible · A unit buffed inside the region keeps its buff if it walks out, until the event ends (the carrier's own timer still bounds it) · fallback: a corrected amendment adds a per-sweep "left the region" removal through the existing carrier stop path
- S-5 · validated · A trigger scope means the kill's region for VBloodKilled, and "at least one online player stands in a named region" for Schedule, GameTime and Manual (a skipped occurrence is logged; a Manual start is refused) · source: owner decision in plan mode 2026-09-28 (regions Review 1 F1, option A; design §9 D21)
- S-6 · reversible · A ring point outside the scope counts as blocked in the walkable search, moving units inward · fallback: a corrected amendment requires only the centre in scope
- S-7 · validated · Paths a step creates and deletes inside the same step are outside the declared-paths check · source: owner decision, docs/NYARLATHOTEP_DESIGN.md §9 D18
- S-8 · validated · The profile notes apply: in-game claims have a session item (6.1), every chat text is rendered before release (11.2), every control has a test seam (12.4), and the release copies the earlier release-step amendments (14.3) · source: docs/dod/profile.md
- S-9 · reversible · This child releases 0.6.0 on base v0.5.2 · fallback: if raphael-api-admin released 0.5.1 (its S-9), a corrected amendment before step 1 makes the base v0.5.1 in D16, the Rollback range and the audit
- S-10 · reversible · `api regions` counts active events per region by action scope, and `region=` on rows carries the action scope · fallback: a corrected amendment adds `tregion=` for the trigger scope if Raphael asks for it (contract §8)

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D11 D14; 2.2 D11; 2.3 D8 D9 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D3 D8; 3.2 D8 D9 D10; 3.3 D13 D16; 3.4 D3 D16 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D3 D4 D5 D6; 4.2 D5 D6 D15; 4.3 D4 D5 D2; 4.4 D5 D6; 4.5 D2 D11 D13 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D4 D5 D6; 5.2 D3 D10; 5.3 D3 D10 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D2 D12 D10; 6.2 D7 D4 D16; 6.3 D13 D1 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D3 D7; 7.2 D2 D13; 7.3 D5 D4 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D3 D7; 8.2 D7 D3 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D15 D12; 9.2 D11 D3; 9.3 D8 D9 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D11; 10.2 D3; 10.3 D14; 10.4 D14 D8 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D16 D8; 11.2 D8 D9; 11.3 D12; 11.4 D9 D10 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D7 D2 D3 D6; 12.2 D2 D4 D5; 12.3 D7 D13; 12.4 D11 D1 D3 D4 D5 D6 D7 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D15 D12; 13.2 D3 D10 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D16; 14.2 D3 D10 D16; 14.3 D16; 14.4 D13 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline
- [ ] D1 · **Point in region** Logic/Regions.cs `RegionIndex` holds polygons (a region name, an axis-aligned box and x/z vertices) and answers `RegionOf(x, z)`: the box test first, then the even-odd crossing test over the vertices, the first polygon in index order that contains the point wins, and a point in none answers "None"; a polygon with fewer than 3 vertices or a non-finite coordinate is dropped when the index is built and counted · test: Nyarlathotep.Tests RegionTests (fails when: a point inside a concave polygon's notch answers that region, a point on the far side of a box-only overlap answers the box's region, a point outside every polygon answers other than None, a vertex on the query point's ray is counted twice, a NaN point answers a region, or a degenerate polygon enters the index uncounted)
- [ ] D2 · **Regions read from the game** Services/RegionMap.cs builds the RegionIndex once, after Core.IsReady, from every entity with ProjectM.Terrain.WorldRegionPolygon (disabled included), its PolygonBounds and its WorldRegionPolygonVertex buffer, naming each polygon by WorldRegionType.ToString(); it logs "regions: <p> polygons, <r> regions (<names>)" and never writes a component; the region names the game knows are the WorldRegionType names except None, and Logic's RegionNames list must equal them, checked at boot with "regions: names differ from the game: <diff>" as a warning · manual: Session 1 records the boot line and `.nyar region list` · cmd: pwsh tools/preflight.ps1 -LogCheck on Session 1's log → the "regions:" line present with p > 0 (fails when: the line is missing, p is 0, or the names-differ warning appears)
- [ ] D3 · **Scope in events.json** trigger and action each take an optional `scope`: the string "Global" (the default when absent) or an array of 1 to RegionNames.Count (10 at 1.1.12) distinct region names, matched case-insensitively and stored in the game's spelling; an unknown name, "None", an empty or longer array, a duplicate, or a non-string disables the event with "trigger.scope must be Global or 1-<RegionNames.Count> region names" or "action.scope must be …" (or "unknown region <name>"); every trigger type takes a scope (D4 gives each its meaning); SchemaVersion stays 1, templates accept the same key, and a definition without either key behaves exactly as in 0.5.x; the trigger scope and the action scope are independent, so a trigger on CursedForest with an action on FarbaneWoods is valid · test: Nyarlathotep.Tests EventValidationTests Scope (fails when: a trigger scope disjoint from its action scope is refused, an absent scope is not Global, "cursedforest" is not stored as CursedForest, an unknown, None, empty, longer-than-RegionNames.Count, duplicate or numeric scope is accepted, the maximum is a literal instead of RegionNames.Count, a trigger scope on any of the four trigger types is refused, or a 0.5.x definition's parse result changes)
- [ ] D4 · **Regional triggers** a trigger scope other than Global is a start condition for every trigger type (owner, S-5): VBloodKilled: DeathEventPatch passes the victim's x and z to TriggerBus.VBloodKilled, and TriggerRouter.VBloodKilled starts a definition only when its trigger scope contains RegionOf(x, z); a kill whose position cannot be read starts only Global definitions and logs "vblood kill: position unreadable" once per streak. Schedule and GameTime: when an occurrence is due, Logic ScopeGate.AnyPlayerIn(scope, positions) over the online players' x/z (read by Services only for a definition with a trigger scope) decides; with no player in a named region the occurrence is consumed as handled, not retried in the same slot, and logs "event <id>: skipped, no player in <regions>". Manual (`.nyar event start` and its api twin): with no player in a named region the start is refused with "no player is in the event's regions" (Outcome refusal badarg arg=scope reason=no_player_in_region, the reason added to Logic/Outcome.cs Reasons with an OutcomeCodeTests row and to contract §5a). A Global trigger reads no position · test: Nyarlathotep.Tests TriggerActivationTests Region and EngineTests ScopeGate (fails when: a scoped definition starts on a kill outside its regions, a Global one fails to start, an unreadable kill position starts a scoped one, the log repeats within a streak, a scheduled or game-time occurrence starts with no player in its regions or is retried in the same slot, one fails to start with a player inside, a Manual start with no player inside starts or answers another code, or a Global trigger reads positions)
- [ ] D5 · **Regional empowerment** Eligibility.Decide gains the skip reason "region" after "other": a unit whose position (read into UnitFacts at sweep time) lies outside the action's scope is skipped; a unit whose position cannot be read is skipped with "region" when the scope is not Global; a unit already carrying the event's buff keeps it until the event ends, wherever it walks (S-4); a Global scope never reads a position · test: Nyarlathotep.Tests EmpowerEligibilityTests Region (fails when: an out-of-region unit is empowered, an in-region unit is skipped, an unreadable position is empowered under a regional scope, "region" is ordered before "denied" or "other", or a Global scope reads the position)
- [ ] D6 · **Regional waves** a SpawnWaves action with a regional scope: a Point location outside the scope disables the event at load with "action.location is outside action.scope" (checked when the region index is available, else deferred to start); an Admin location whose origin lies outside the scope refuses the start with "your position is outside the event's regions" (Outcome refusal badarg arg=location reason=out_of_region, the reason added to Logic/Outcome.cs Reasons with an OutcomeCodeTests row and to contract §5a's reason list); a ring point outside the scope counts as blocked in walkable-spawns' SpawnPoints search (isFree and in scope), so units move inward before falling back to the centre · test: Nyarlathotep.Tests SpawningTests Region and EngineTests Region (fails when: a point outside the scope is used while an in-scope one is free, a Point location outside the scope loads enabled, an Admin start outside the scope starts, out_of_region is missing from Reasons or §5a, or a Global scope calls the region check)
- [ ] D7 · **Regions unavailable** RegionMap builds in Core's deferred init before EventStore applies the definitions, so a boot never validates a regional definition without the index; when RegionMap finds no polygon, or its build throws, every definition with a regional scope is disabled with "regions unavailable" and Global ones run unchanged; HealthMonitor.Degraded adds "regions: unavailable"; `.nyar region here` replies "regions unavailable"; the build is retried at the next `.nyar event reload` · test: Nyarlathotep.Tests DependencyFailureTests Regions, also run as dotnet test Nyarlathotep/Nyarlathotep.Tests --filter Regions_ → "Passed!" with at least 5 tests (fails when: a regional definition runs without an index, a Global one is disabled, the health entry is missing or stays after a successful rebuild, a throwing build stops the boot, definitions are applied before the build, or the filter runs fewer than 5 tests)
- [ ] D8 · **Admin commands** `.nyar region list` (adminOnly) replies one line per region in RegionNames order, "<name> (<display name>): <n> events", n counting enabled definitions whose trigger or action scope names it, then "global: <g> events"; `.nyar region here` (adminOnly) replies "you are in <name> (<display name>)" or "you are outside every region" from the admin's position; any other verb replies "argument must be list or here"; neither changes anything · test: Nyarlathotep.Tests RegionTests Lines (fails when: a line's text differs from Design › UX, a count includes a disabled definition, or the display name is not the CamelCase name split at capitals with `_` as a space)
- [ ] D9 · **Scope in replies** `.nyar event info` shows "trigger scope: <Global|names>" and "action scope: <Global|names>"; `.nyar event list` appends " [<names>]" to a regional event; `.nyar event set <id> trigger.scope|action.scope <Global|name,name>` edits the key through the existing editor and gateway row (SetEventField); a `trigger.type` change clears `trigger.scope` with the other trigger keys (Logic/EventAdmin.cs's reset), so the event returns to a Global trigger; an announcement may use the placeholder {region}, the action scope's display names joined by ", " or "the world" for Global · test: Nyarlathotep.Tests EventAdminTests Scope and AnnouncerTests Region (fails when: info lacks either line, a Global event gains a list suffix, `set` accepts a name `.nyar event reload` would reject, a `trigger.type` change keeps `trigger.scope`, or {region} renders other than the stated text)
- [ ] D10 · **Wire at api 5** `.nyar api regions [page]` (anyone) replies `[NYAR:region] id=<name> events=<n>` rows paged per contract §4, n counting active events whose action scope names the region; `[NYAR:def]` and `[NYAR:event]` rows gain `region=<name,…>` (action scope; `-` for Global) as their last key; event-start and event-end pushes gain `region=` after `secs`; Wire.Api is 5 and docs/RAPHAEL_INTEGRATION_CONTRACT.md marks the `region` tag and `regions` command IMPLEMENTED (api 5), moves §10.2's regions row and §10.4's region keys into §3, and says "**Current api:** 5"; docs/RAPHAEL_HANDOFF_API4.md gains the region picker note · test: Nyarlathotep.Tests WireFormatTests Region, ApiLinesTests Region and ContractDocTests (fails when: a key set or order differs from the contract's example, a Global event carries other than `region=-`, a line exceeds 480 bytes with 10 regions, Wire.Api differs from the contract, or a row still PLANNED); cmd: pwsh tools/preflight.ps1 → "wire contract: <n> tags, <m> api commands, all documented (api 5)"
- [ ] D11 · **Authorization and static checks** `region` is one adminOnly command with the ready guard; `api regions` is public and allow-listed in $script:PublicCommands; RegionMap is read-only, so no [Mutating] method and no ActionKind is added; the scope edit runs through the existing SetEventField row; ControlCases gains a row per control of this plan, the set being every D-item whose evidence carries a `(fails when:` clause (D1-D11, D13-D16), and ControlCaseTests reads this plan from the test Resources · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (…)"; pwsh tools/preflight.ps1 → PREFLIGHT OK with "commands: <n> admin-only, <m> public (allow-listed)"; pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests" (fails when: `region` loses adminOnly or its ready guard, `api regions` is public without its allow-list entry, fixture Commands/bad-3 (a `region` command without adminOnly) passes, or a control lacks its row)
- [ ] D12 · **Regions in game** Session 1 on the local server (127.0.0.1:9876): `.nyar region here` in Farbane Woods, Dunley Farmlands and the Cursed Forest names each; an Empower event of Faction_Undead scoped to CursedForest shows, via `.nyar debug here`, a carrier on an undead unit in the Cursed Forest and none on one in Farbane Woods; a SpawnWaves event with an Admin location scoped to FarbaneWoods refuses to start in Dunley and starts in Farbane; a VBloodKilled event scoped to FarbaneWoods starts on a Farbane V Blood kill; with Debug.TimingLog on, every "tick timing" line during the empowerment sweep shows avg under 5 ms (Epic D24) · manual: Session 1 in docs/features/REGIONS.md › Test results records each observed line
- [ ] D13 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/REGIONS.md › Test results, with its -LogCheck line in docs/audits/regions.md before the next restart and "snapshot restored; hashes equal" from tools/dev-snapshot.ps1; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/preflight-checks.json gains `childDocs`, `snapshotSessions` (1) and `dataTables` entries for regions; tools/data-inventory.json has a "regions › <artifact>" entry per Design › Data row · cmd: pwsh tools/preflight.ps1 -AuditOf regions → "audit steps: regions 3/3 pre, 3/3 post, 3/3 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf regions → "session logs: regions 1/1 checked; snapshots 1/1 from session 1"; pwsh tools/preflight.ps1 -Paths -DeclaredOf regions → "paths: <n> walked, all in manifest; declared: <d>/<d> in regions" (fails when: a step lacks an entry or verdict, a session lacks its log-check or snapshot line, a walked path matches no manifest glob or is undeclared, a %TEMP%\nyar-* folder outlives its session, or a Data row lacks an inventory entry or field)
- [ ] D14 · **Secrets and privacy** this child adds no credential; no wire line, announcement or log line carries a player's position or name because of a region (the `region=` key names only an event's configured scope, and `region here` answers the admin alone); no tracked file holds a SteamID digit run or the owner's mail name except lines quoting the pattern · cmd: pwsh tools/preflight.ps1 → "secrets: none (<n> files scanned…)"; then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern; test: Nyarlathotep.Tests PrivacyTests Region (fails when: a token shape is in the scanned set, which the existing Secrets fixtures plant under -SelfTest, a region line or push carries a player position, name or SteamID, or the grep finds a SteamID digit run or the owner's address on any other line)
- [ ] D15 · **Tick cost bounded** a regional empowerment sweep reads one position and runs at most RegionIndex's polygon count box tests per unit, bounded by EmpowerBatchPerTick (200); RegionIndex answers 10,000 random points over a real-sized index (the polygon count read from the boot line in step 3 into Nyarlathotep.Tests/Fixtures/region-index-size.txt; the test is added in step 3 with it) in under 20 ms on the dev machine · test: Nyarlathotep.Tests RegionTests Cost (fails when: the fixture is missing or holds 0 polygons, a Global scope reads positions, a unit costs more box tests than polygons, or the 10,000-point run exceeds 20 ms)
- [ ] D16 · **Release 0.6.0** csproj Version and thunderstore.toml versionNumber are 0.6.0; both changelogs and both READMEs describe regional scope, `.nyar region list|here` and api 5; the README states that 0.5.x disables, after a rollback, a definition carrying `scope` (unknown key) or an announcement using `{region}` (unknown placeholder); the annotated tag v0.6.0 is pushed and the GitHub pre-release carries the tcli zip; after a failed or ambiguous `gh release create` the step runs `gh release view v0.6.0` and retries only when the release is absent; no tcli publish · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/rollback-gate.ps1 -From v0.5.2 -To v0.6.0 -Plan regions → "rollback gate: 4/4"; then pwsh tools/release-verify.ps1 -Tag v0.6.0 -Asset kdpen-Nyarlathotep-0.6.0.zip → "release verify: hashes equal" (fails when: a surface differs, 0.5.2 does not initialize on 0.6.0's files, the repository revert of v0.5.2..v0.6.0 is not clean, a Rollback route is missing or names another range, the tag is missing or unpushed, the release has no zip, or the hashes differ)

## Amendments
- A1 · 2026-09-28 · discovered · ~D6 · layer: 4.2 · Review 2 F1 (advisory): walkable-spawns' WalkBudget keeps a ring point unchecked once the tick budget is spent, so a scope check inside isFree could let a busy tick place a unit outside the scope; the scope check runs before the budget and spends none of it, with a SpawningTests case
- A2 · 2026-09-28 · discovered · ~D2 · layer: 12.4 · Review 2 F2 (advisory): -LogCheck asserted no "regions:" line, so D2's cmd evidence could not fail; it asserts the line (p > 0, no names-differ warning) when the log loads Nyarlathotep 0.6.0 or later, with three LogCheck fixtures
- A3 · 2026-09-28 · discovered · ~D3 · layer: 6.2 · Review 2 F3 (advisory): a renamed WorldRegionType does not throw, so a scope naming the old name would validate and never match; a name the built index holds no polygon for disables the definition with "region <name> is not on the map", and the 6.2 sentence is corrected
- A4 · 2026-09-28 · discovered · ~D4 · layer: 14.4 · Review 2 F4 (advisory): the Manual refusal needs online players' positions and step 2 did not walk the files that supply them; the check runs in EventEngine.Start after Precedence.StartBlocker (Business rules 5) with a position reader in its start controls, supplied by Services/EventRuntime.cs and faked in FakeStores.cs; Paths walked lists them
- A5 · 2026-09-28 · discovered · ~D9 · layer: 7.3 · Review 2 F5 (advisory): a scope edit on a running event was unstated; it follows DefinitionEditor's rule (a running instance keeps its definition, the edit applies at the next start), with a test case
- A6 · 2026-09-28 · discovered · ~D6 · layer: 4.1 · Review 2 F6 (advisory): D6's "else deferred to start" could never run, since RegionMap builds before the definitions and a missing index disables regional ones (D7); the branch is removed
- A7 · 2026-09-28 · discovered · ~D15 · layer: 12.4 · Review 2 F7 (advisory): a 20 ms wall-clock limit is flaky; D15 asserts counted box and polygon tests and logs the time with a 200 ms ceiling
- A8 · 2026-09-28 · discovered · ~D4 · layer: 2.2 · Review 2 F8 (advisory): no player in the regions is a world state, not a bad argument; the Manual refusal answers code=state reason=no_player_in_region, recorded in contract §5a
- A9 · 2026-09-28 · discovered · ~D6 · layer: 4.2 · Review 3 F1 (advisory): WavePoints.Plan keeps the ring point Unchecked when the budget is spent and places every ring point unchecked on fall-open or with no probe, so A1 alone did not keep units in scope; an Unchecked out-of-scope ring point uses the wave centre, with a D6 fails-when case
- A10 · 2026-09-28 · discovered · ~D4 · layer: 4.1 · Review 3 F2 (advisory): with A4's check in EventEngine.Start, a VBloodKilled start would also need a player in its regions; the player gate applies to Manual, Schedule and GameTime only, keyed on def.Trigger.Type, with a TriggerActivationTests case for an in-region kill with no player there
- A11 · 2026-09-28 · discovered · ~D4 · layer: 14.4 · Review 3 F3 (advisory): the position reader is a defaulted ControlState parameter (Logic/Precedence.cs), so the tests that construct ControlState compile unchanged; Paths walked step 2 lists Precedence.cs
- A12 · 2026-09-28 · discovered · ~D4 · layer: 4.1 · Review 3 F4 (advisory): the scheduler, on a no_player_in_region refusal, keeps the slot handled and logs the skip line instead of the generic refused-start line
- A13 · 2026-09-28 · discovered · ~D15 · layer: 12.4 · Review 3 F5 (advisory): the Failing-cases row for RegionTests Cost still stated A7's old 20 ms limit; it states the counted tests and the 200 ms ceiling
- A14 · 2026-09-28 · discovered · ~D2 · layer: 12.4 · Review 3 F6 (advisory): a log whose version cannot be read would skip the regions assertion silently, and no good 0.6.0 fixture proved it passes; -LogCheck fails an unreadable version on a log with nyar lines (fixture bad-regions-noversion) and LogCheck/good-regions is added, with a Failing-cases row
- A15 · 2026-09-28 · discovered · ~D7 · layer: 6.2 · Review 3 F7 (advisory): with no polygons both D7 and A3 could name the failure; D7's "regions unavailable" wins, A3 applies only to a built, non-empty index, with a DependencyFailureTests case
- A16 · 2026-09-28 · discovered · ~D2 · layer: 6.1 · recon of VampireReferenceAssemblies 1.1.12-r99041-b2 ProjectM.Shared WorldRegionType at step 1: the enum holds None, Other and ten map regions (StartCave, FarbaneWoods, DunleyFarmlands, CursedForest, HallowedMountains, SilverlightHills, Gloomrot_South, Gloomrot_North, RuinsOfMortium, Strongblade), so "except None" gave 11 against the stated 10; RegionNames is the names except None and Other, and a polygon tagged outside RegionNames is left out of the index and counted
- A17 · 2026-09-28 · discovered · ~D7 · layer: 14.4 · recon of Logic/Dependency.cs at step 1: foundation D9 gives every runtime dependency a Dependency member, a DependencyPolicy row and a DependencyFailureTests fault case, which the test Every_dependency_has_a_policy_row_and_a_fault_case enforces; the region polygons are one, so Dependency.Regions and its row ("regional definitions", "disable them with regions unavailable, the health entry while it lasts, retried at reload") are added and Paths walked step 1 lists Logic/Dependency.cs
- A18 · 2026-09-28 · discovered · ~D4 · layer: 4.4 · Review 4 F1 (blocking) and F4 (advisory): A10 keyed the player gate on def.Trigger.Type, so an admin's `.nyar event start` of a trigger-scoped VBloodKilled definition met no region check, against Business rules 5 (no actor bypasses the scope); the gate is keyed on the start: only the VBloodKilled route's start carries the kill position and skips it, every other start needs a player in the trigger scope, and in Start the gate runs after the Admin-location checks and before catalog.TryStart, with a test case
- A19 · 2026-09-28 · discovered · ~D4 · layer: 12.4 · Review 4 F2 (advisory): the generic refused-start line is written in Services/EventRuntime.StartEvent, which the tests do not compile, so A12's fails-when could not fail; a Logic/AdminLines.cs helper chooses between the skip line and the generic line, and EventRuntime calls it
- A20 · 2026-09-28 · discovered · ~D4 · layer: 6.2 · Review 4 F3 (advisory): a null position reader was unstated for a trigger-scoped start; it gives zero positions, so the start is refused (fails closed)

## Log
- 2026-09-28 · status → draft · plan
- 2026-09-28 · note · planned autonomously from design §9 D21, Epic A28 and recon at 7dbd9df (KindredCommands RegionService approach, Validation, Empowerment, TriggerBus, WavePlan); choices inside the scope are reversible assumptions S-3 to S-6, S-9 and S-10
- 2026-09-28 · note · review: subagent Review 1 REVISE (F1-F2 blocking, F3-F9 advisory; 13/15 layers, 47/49 probes); F2-F9 accepted and applied: empty inputs fail and D15 measured in step 3 (F2), out_of_region in Reasons and §5a (F3), the capture unchanged (F4), a trigger type change clears trigger.scope (F5), RegionMap before definitions (F6), the bound is RegionNames.Count (F7), scopes independent (F8), {region} in the rollback note (F9); F1 (trigger scope on every trigger narrows design §9 D21) goes to the owner in plan mode
- 2026-09-28 · note · owner decision in plan mode (Review 1 F1, option A): every trigger type takes a scope; Schedule, GameTime and Manual mean "a player is in a named region" (D4, S-5, Business rules 2 and 6, design §9 D21)
- 2026-09-28 · note · review: subagent Review 2 READY (F1-F8 advisory; 15/15 layers, 49/49 probes; EARLIER all resolved); all accepted, applied as amendments at `start`
- 2026-09-28 · status → ready · approve
- 2026-09-28 · status → in-progress · start
- 2026-09-28 · note · amendments A1-A8 from Review 2's advisory findings F1-F8 (discovered); raphael-api-admin is closed and v0.5.2 tagged, so the precondition holds and S-9's base v0.5.2 stands
- 2026-09-28 · note · re-review A1-A8 · Review 3 READY (F1-F7 advisory, F8 a confirmation; 15/15 layers, 49/49 probes); F1-F7 accepted as amendments A9-A15 (discovered)
- 2026-09-28 · note · A16: WorldRegionType at 1.1.12 also holds Other; RegionNames excludes None and Other (10 names)
- 2026-09-28 · note · A17: Dependency.Regions with its policy row; Paths walked step 1 lists Logic/Dependency.cs
- 2026-09-28 · note · re-review A9-A15 · Review 4 REVISE (F1 blocking, F2-F4 advisory; 15/15 layers, 49/49 probes as written, F1 disputes 4.4); F1-F4 accepted as amendments A18-A20 (discovered)
