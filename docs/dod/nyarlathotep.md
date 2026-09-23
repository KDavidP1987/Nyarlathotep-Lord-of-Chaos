---
dod: 2
rubric: 2
id: dod-20260923-nyar
slug: nyarlathotep
title: Nyarlathotep, Lord of Chaos, v0.1 to v1.0
status: in-progress
size: Epic
parent: none
kind: product
created: 2026-09-23
baselined: 2026-09-23
closed: none
commit: 8c92928
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: 15/15 layers · 49/49 probes
review: human
---

# DoD: Nyarlathotep, Lord of Chaos, v0.1 to v1.0

**Size:** Epic. The product is four pillars plus a shared engine: seven child plans, each its own M/L plan (Epic test: a multi-feature module).
**Planned:** interactively
**Request:** "Now that the scaffolding has been set up for this new mod I would like to proceed with additional development. [...] I would like to utilize our new skill /dod In establishing a plan for the mod's development. [...] make sure that, procedurally, we utilize pre-audit and post-audit testing as well as Claudex for the evaluation of plans and code."

## Definition of Done
- [x] D1 · **Public GitHub repo** repository KDavidP1987/Nyarlathotep-Lord-of-Chaos is public and its default branch is main · cmd: gh repo view KDavidP1987/Nyarlathotep-Lord-of-Chaos --json visibility,defaultBranchRef → visibility PUBLIC, defaultBranchRef.name main (fails when: the repo is private or missing, or the default branch is master)
- [ ] D2 · **Compiles clean** the solution (plugin and Nyarlathotep.Tests) builds in Release with 0 errors and 0 warnings without deploying · cmd: dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ → "0 Warning(s)" and "0 Error(s)" (fails when: any source file has a compile error or a new warning)
- [ ] D3 · **Release surfaces in sync** preflight passes its version, changelog and description checks · cmd: pwsh tools/preflight.ps1 → exit 0 (fails when: csproj Version differs from thunderstore.toml versionNumber, or either CHANGELOG lacks the version)
- [ ] D4 · **Pillars ship off** every [Pillars] Bind default in Config/Settings.cs is false and every event in Resources/*.json has "enabled": false · cmd: pwsh tools/preflight.ps1 → line "pillar defaults: all off" (fails when: a Pillars Bind default is true or a shipped template event is enabled)
- [ ] D5 · **Admin-only commands** every [Command] attribute in any tracked or untracked-not-ignored .cs file (git ls-files plus git ls-files --others --exclude-standard) lives under Commands/ and carries adminOnly: true except the allow-list {nyar, status, help} · cmd: pwsh tools/preflight.ps1 → line "commands: <n> admin-only, <m> public (allow-listed)" (fails when: a command outside the allow-list lacks adminOnly: true, or a [Command] attribute exists outside Commands/)
- [ ] D6 · **Structural edits fenced** EntityManager.AddComponent, RemoveComponent, AddBuffer and DestroyEntity calls appear only in EntityExtensions.cs, whose helpers refuse any entity carrying the Prefab component · cmd: pwsh tools/preflight.ps1 → line "structural edits: fenced" (fails when: one of those calls appears in any other tracked .cs file)
- [ ] D7 · **One data folder** File.Write*, File.Replace, File.Move, File.Delete and Directory.Create calls appear only in Services/Persistence.cs, which exposes no path or file-name parameter and writes only the constant names events.json, zones.json, state.json and their .bak/.tmp siblings under BepInEx/config/Nyarlathotep/ · cmd: pwsh tools/preflight.ps1 → line "file writes: fenced" (fails when: a file-write call appears in any other tracked .cs file)
- [ ] D8 · **Hooks fail safe** every [HarmonyPrefix]/[HarmonyPostfix] method in Patches/*.cs starts with `if (!Core.IsReady) return` and puts the rest of its body in one try/catch; the one exception is GameDataInitializedPatch, which sets IsReady and so starts with the inverse `if (Core.IsReady) return` · cmd: pwsh tools/preflight.ps1 → line "patch guards: <n>/<n>" (fails when: a patch method's first statement is not its guard, it does work outside the try/catch, or a patch other than GameDataInitializedPatch uses the inverse guard)
- [ ] D9 · **No secrets committed** no tracked, staged or untracked-not-ignored file, no file under dist/ or build/ (including inside built zips) and no captured log under tools/preflight-fixtures/ contains a Thunderstore token (tss_), a GitHub token (ghp_, gho_, ghu_, ghs_, ghr_, github_pat_), an "Authorization: Bearer" header, or a TCLI_AUTH_TOKEN or GH_TOKEN assignment, and no .cs file calls Environment.GetEnvironmentVariable · cmd: pwsh tools/preflight.ps1 → line "secrets: none" (fails when: any scanned location contains one of those forms or a .cs file reads the environment)
- [ ] D10 · **Preflight self-test** tools/preflight-checks.json lists every preflight check with a good, a bad and an empty fixture under tools/preflight-fixtures/<check>/; the check list is derived independently by scanning tools/preflight.ps1 for functions named Test-Check*, and the manifest must equal that list both ways; each check passes good, fails bad, and fails empty (a missing input is never a pass) · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, 3 fixtures each" (fails when: a Test-Check function is missing from the manifest or vice versa, a check passes its bad or empty fixture, fails its good one, or an entry lacks a fixture)
- [ ] D11 · **Kill switch** `.nyar purge` then `.nyar purge confirm` ends every active event, suppresses scheduled starts for PurgeCooldownSeconds, and leaves 0 tracked units, despawning at most Limits.MaxDespawnsPerTick per tick; a second confirm is a no-op · manual: on the local server start one spawn event with 30 units and one empowerment event; run `.nyar purge`, `.nyar purge confirm`, then `.nyar purge confirm` again; `.nyar status` shows 0 active events and 0 tracked units within 10 s, the second confirm replies "nothing to purge", and the log shows despawn batches no larger than the budget
- [ ] D12 · **Clean uninstall** removing Nyarlathotep.dll mid-event leaves no spawned unit and no carrier buff behind · manual: start a spawn event and an empowerment event, stop the server, delete Nyarlathotep.dll, start the server, wait the longest configured unitLifetime; reinstall with General.Enabled=false; the boot log reports "marker sweep: 0 found" and a unit of the empowered faction shows base stats via `.nyar debug here`
- [ ] D13 · **No orphans after restart** a restart mid-event removes every surviving marked unit on boot and cancels active events · manual: start a 30-unit spawn event, stop the server with units alive, start it; the log shows "marker sweep: <k> found, <k> queued" and `.nyar status` shows 0 tracked units and 0 active events after the drain
- [ ] D14 · **Caps beat definitions** Limits in kdpen.Nyarlathotep.cfg clamp every event definition and are logged · manual: set MaxUnitsPerWave=5, MaxTrackedUnits=8; start an event requesting 2 waves of 20; exactly 5 then 3 units spawn and the log shows "clamped by MaxUnitsPerWave" and "skipped by MaxTrackedUnits"
- [ ] D15 · **Bad JSON survives** a malformed or out-of-range events.json never stops the server: the bad event is disabled with a logged reason and the rest load · manual: add one event with an unknown unit GUID and one with a syntax error to a copy, run `.nyar event reload`; the log names each rejected event and its reason, `.nyar event list` shows the valid ones, the server keeps ticking
- [ ] D16 · **Players see no positions** non-admin replies and every announcement contain no coordinates, zone radii or player names · manual: as a non-admin run `.nyar` and `.nyar status` during a running event and read every announcement of one event of each pillar; none contains a number pair, a zone radius or a player name
- [ ] D17 · **Audit record per child** every child listed under this plan's ## Children whose plan status is done has docs/audits/<slug>.md with "## Pre-audit", "## Post-audit" and "Codex verdict:" · cmd: pwsh tools/preflight.ps1 → line "audits: <n>/<n> done children recorded" (fails when: a done child has no audit file or the file lacks one of the three markers)
- [ ] D18 · **Test results per pillar** every done child's feature doc (mapping in tools/preflight-checks.json) has a "## Test results" section with at least one YYYY-MM-DD entry · cmd: pwsh tools/preflight.ps1 → line "feature results: <n>/<n>" (fails when: a done child's doc lacks the section or a dated entry)
- [ ] D19 · **Thunderstore icon** Nyarlathotep/Nyarlathotep/icon.png exists and its PNG header states 256x256 · cmd: pwsh tools/preflight.ps1 → line "icon: 256x256" (fails when: the file is missing or states another size)
- [ ] D20 · **Package builds** tcli builds the Thunderstore zip from the staged dist · cmd: cd Nyarlathotep/Nyarlathotep && tcli build → build/kdpen-Nyarlathotep-<version>.zip created (fails when: icon.png, README.md, dist/BepInEx/plugins/Nyarlathotep.dll or CHANGELOG.md is missing)
- [ ] D21 · **Tagged releases** every commit whose subject matches ^chore\(release\): v\d+\.\d+\.\d+ has an annotated tag of that version pushed to origin · cmd: pwsh tools/preflight.ps1 → line "release tags: <n>/<n>" (fails when: a release commit has no tag, or zero release commits exist after the first release was expected)
- [ ] D22 · **Plan index fresh** the dod store index matches the plans · cmd: node C:/Users/KDPen/.claude/skills/dod/scripts/dod-index.mjs --check-index --dir docs/dod → exit 0 (fails when: a plan was edited without regenerating the index)
- [ ] D23 · **Coexists with common mods** with Bloodcraft and KindredCommands installed, no familiar is empowered, swept or counted as ours, and both mods' spawns still work · manual: install both on the local server, summon a Bloodcraft familiar, run an empowerment event on the familiar's source faction and a spawn event; `.nyar status` tracked count equals our spawns only, the familiar's stats are unchanged, KindredCommands' spawnnpc still spawns
- [ ] D24 · **Tick budget** with Debug.TimingLog=true the scheduler tick averages under 1 ms idle, under 5 ms at the default MaxTrackedUnits (150) and under 15 ms at the 500 ceiling, each over 5 minutes · manual: enable TimingLog, run idle 5 min, a 150-unit event 5 min, then with MaxTrackedUnits=500 a 500-unit event 5 min; read the three logged averages
- [ ] D25 · **Tick survives faults** an exception inside an event's tick is caught, logged with the event id, and after 3 consecutive failures the event is cancelled and its units queued for despawn while other events keep running · manual: set Debug.FaultInjection=<event id> on the local server with two events running; the log shows 3 caught faults then "event <id> cancelled after 3 faults", the other event continues, and `.nyar status` lists the degraded pillar
- [ ] D26 · **Non-admins denied** every admin-only command run by a non-admin is refused by VCF and changes nothing · manual: as a non-admin run each command listed by `.nyar` for admins (event start/stop/enable/disable/reload/set, zone add/remove, spawn, purge, debug); each is refused and `.nyar status` is unchanged
- [ ] D27 · **Precedence tests** the precedence resolver applies purge > General.Enabled > pillar switch > caps > definition when they conflict simultaneously · test: Nyarlathotep.Tests ControlPrecedenceTests (fails when: any lower-precedence control overrides a higher one in the conflict matrix)
- [ ] D28 · **Schedule tests** real-clock schedules fire once per matching local minute (a DST-repeated hour fires once, a skipped hour is not replayed, a backward clock change does not re-fire), and in-game schedules fire on the day/night edge · test: Nyarlathotep.Tests ScheduleTests (fails when: a repeated local time fires twice or a skipped time is replayed)
- [ ] D29 · **Validation tests** EventDefinition validation rejects unknown fields, unknown trigger/action types, out-of-range numbers, level delta beyond ±5, empty compositions and templates with unknown placeholders, with one reason per rejection · test: Nyarlathotep.Tests EventValidationTests (fails when: an invalid definition is accepted or a valid one rejected)
- [ ] D30 · **Duplicate-action tests** starting an active event, stopping a stopped one, a second purge confirm, a double reload and a trigger repeated within 5 s each leave exactly one outcome · test: Nyarlathotep.Tests IdempotencyTests (fails when: a repeated action creates a second instance or a second effect)
- [ ] D31 · **Persistence path tests** Persistence resolves only its constant file names under the data folder and refuses rooted paths, .. segments and reparse points · test: Nyarlathotep.Tests PersistencePathTests (fails when: any resolved path lies outside BepInEx/config/Nyarlathotep/)
- [ ] D32 · **Rollback drill** a script installs release N, boots the local server, edits one event, stops, installs N-1, boots again, and checks the log for "Nyarlathotep initialized", the schema line (read-only warning when N's SchemaVersion is newer, none otherwise) and "marker sweep"; it also checks that <N-1 tag> is an ancestor of <N tag> and that `git diff --name-only <N-1>..<N>` lists only manifest paths · cmd: pwsh tools/rollback-drill.ps1 -From v<N> -To v<N-1> → "rollback drill: pass" (fails when: N-1 does not initialize on N's state, the schema line is wrong, the tags are not ancestor-ordered, or the range touches a path outside tools/paths-manifest.txt)
- [ ] D33 · **Paths manifest** after `dotnet build`, `tcli build` and a deploy, every path from git ls-files, git ls-files --others --exclude-standard, git status --ignored --porcelain, and the server's BepInEx/plugins/Nyarlathotep* and BepInEx/config/Nyarlathotep/* matches a tracked, ignored or server glob in tools/paths-manifest.txt, which mirrors Rollout › Paths walked · cmd: pwsh tools/preflight.ps1 -Paths → line "paths: <n> walked, all in manifest" (fails when: any walked path matches no manifest glob)
- [ ] D34 · **Data inventory complete** tools/data-inventory.json has an entry with location, owner, retention, deletion and singleCopy for every constant file name in Services/Persistence.cs and every ignored or server glob in tools/paths-manifest.txt · cmd: pwsh tools/preflight.ps1 → line "data inventory: <n>/<n> complete" (fails when: a Persistence constant or manifest glob has no entry, or an entry lacks one of the five fields)
- [ ] D35 · **Dependency failure tests** through Logic/ abstractions (IFileStore, IHookRegistry), a failing or slow disk write keeps state in memory and retries once per second, an unreadable state.json is renamed .corrupt and ignored, garbage JSON disables only the bad event, and an unavailable hook disables only its pillar · test: Nyarlathotep.Tests DependencyFailureTests (fails when: any of the four faults stops another pillar, loses in-memory state or throws out of the tick)
- [ ] D36 · **Authorization path tests** every mutating operation is an ActionKind executed through Logic/ActionGateway, whose table grants each ActionKind to actors Admin, Operator (file load) or System (schedule and triggers); System may only start and end enabled definitions; the test enumerates every ActionKind × actor, and a preflight check (Test-CheckGatewayOnly) fails when any file under Commands/, Patches/ or Services/ other than Logic/ActionGateway.cs and the services it dispatches to calls a mutating EventRuntime, SpawnTracker, EmpowerAction, WaveAction or Persistence method directly · test: Nyarlathotep.Tests AuthorizationTests (fails when: an ActionKind has no table entry, a System or non-admin actor is granted anything beyond its row, or the gateway-only check finds a direct call)

## Purpose & typical use
- **Who:** a V Rising dedicated-server admin (weekly to daily) who wants the world to react: "Make the Legion surge every Saturday night", "make Tristan's death unleash the Vampire Hunters", "send a war party at my players' castles". Players experience it; they do not configure it.
- **Job:** turn a static PvE world into scheduled and reactive events without the admin writing code, with nothing permanent left behind in the world (admin-owned configuration files stay until the admin deletes them).
- **Coexists with:** vanilla Blood Moon (a player buff, untouched), Bloodcraft, KindredCommands, BloodyBoss, RaidForge, XPRising (docs/RESEARCH_NOTES.md §Compatibility), and the author's own Beelzebub, Uriel and Faust on the same server. It replaces nothing; the closest prior art (DyWorld Rising) is closed-source.

## Use cases
### Typical
Admin enables Pillars.FactionEmpowerment, edits the seeded "legion-surge" template to enabled, runs `.nyar event reload`. Saturday 20:00 the scheduler fires, every Legion NPC gets a 20-minute carrier buff, players see "The Legion surges!", buffs expire on their own.
### Minimal stretch
Mod installed and nothing else: all pillars off, no event enabled, the only effect is the `.nyar` command, a log line, and the seeded config files. Admin runs one `.nyar spawn CHAR_Bandit_Thug 1` test spawn and never again: the unit expires at its lifetime and nothing remains in state.json. Uninstalling leaves kdpen.Nyarlathotep.cfg and BepInEx/config/Nyarlathotep/ by design; the README's "Uninstall" section says to delete both for a full removal.
### Maximal stretch
All pillars on, MaxConcurrentEvents events running at once, MaxTrackedUnits units, a restart in the middle, Bloodcraft installed, 40 players online. Caps clamp, the boot sweep clears survivors, familiars are ignored, and the tick stays under budget (D24). An operator who raises caps is stopped by hard ceilings in code (Business rules 9).

## Business rules
Inherited by every child (each child plan restates the rules it touches):
1. **Precedence, highest first:** kill switch (`.nyar purge confirm`) > General.Enabled=false > the pillar switch > Limits caps in kdpen.Nyarlathotep.cfg > the event definition's own values (D27). A cap clamps silently for the player and loudly in the log (D14). An exception to a cap is only possible by the admin changing the cap in the cfg file, and never beyond the hard ceilings in rule 9.
2. **One empowerment per faction at a time:** a second empowerment on a faction already empowered is rejected with a log line; the faction-empowerment child owns the item that tests it.
3. **Durations are hard:** everything an event created is reverted or despawned at its end; a unit's LifeTime is event end + GraceSeconds (default 30).
4. **Spawned units:** level delta clamped to ±5 by default (D29); modifiers scale from the prefab baseline, never the live value, so re-application never compounds.
5. **Every X sets:**
   - "every NPC of a faction" is an EntityQuery over PrefabGUID+FactionReference+Health+UnitStats with IncludeDisabled|IncludeSpawnTag, minus entities with the Prefab component, minus our own units, minus Faction_Players*; units that stream in after the sweep are caught by the 15 s re-sweep (S-8).
   - "every spawned unit" is SpawnTracker's registry, the only source of truth; after a restart the marker sweep finds survivors (S-7).
   - "every command" (D5) is every [Command] attribute found by walking all tracked .cs files (`git ls-files '*.cs'`), not only Commands/; one outside Commands/ is itself a failure. An untracked new file is caught at commit time because preflight runs in the pre-audit on a clean tree.
   - "every done child" (D17, D18) is parsed from this plan's ## Children section and each child plan's frontmatter status; the doc mapping is in tools/preflight-checks.json.
   - "every release commit" (D21) is `git log --format=%s` filtered by ^chore\(release\): v\d+\.\d+\.\d+.
   - "every tracked path" (D33) is `git ls-files`; ignored and generated paths are listed in the manifest as ignored so the set is complete.
   - The self-test plants a member outside the expected directory for each walk (a [Command] outside Commands/, a file write outside Persistence.cs) (D10).
6. **Loot and time:** event-spawned units drop no loot unless the event sets loot true; XP is vanilla (S-10). Real-clock schedules use the server-local wall clock at minute resolution; a schedule fires at most once per matching local minute, tracked by its last-fired UTC instant, so a DST-repeated hour fires once and a skipped hour or downtime is not replayed (D28). In-game schedules fire on the day/night transition edge (S-14).
7. **Siege eligibility** (S-13), all admin settings: owner or clan member online or last online within RecentlyOnlineHours; castle-heart level at least MinCastleHeartLevel when set; never sealed or decaying; PvE/PvP availability per event; re-checked every tick, and an ineligible target ends the siege and despawns its units.
8. **Duplicate actions** (D30): `event start` on an active event is rejected "already active"; `event stop` on an inactive one replies "not active"; a second `purge confirm` replies "nothing to purge"; `event reload` is idempotent; a trigger from the same source entity within 5 s of the last fires once.
9. **Hard ceilings in code** (cfg values above them are clamped and logged at load): MaxTrackedUnits ≤ 500, MaxUnitsPerWave ≤ 50, MaxConcurrentEvents ≤ 10, MaxDespawnsPerTick ≤ 20.
10. Numbers stated both here and in Config/Settings.cs: Settings.cs is authoritative; this plan quotes it.

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- Reads: game ECS through Core (Core.cs `Server`, `EntityManager`, `ServerGameManager`, `PrefabCollectionSystem`); `Reference Data/unit_index.tsv` and `prefab_names.tsv` only at authoring time, never at runtime.
- Writes: live NPC entities (carrier buffs only, never direct stat writes on native NPCs), units it spawns (direct writes allowed), BepInEx/config/Nyarlathotep/*.json through Services/Persistence.cs (D7, D31).
- Shared contract introduced: `EventDefinition` in events.json (trigger, conditions, action, duration, announce) with SchemaVersion 1; its field list is fixed in the foundation child against the C# record it deserialises into (docs/NYARLATHOTEP_DESIGN.md §2 is the draft) and validated by D29.
- Pure logic (validation, schedule computation, precedence, idempotency, path resolution) lives in Nyarlathotep/Nyarlathotep/Logic/ with no Il2Cpp or Unity usings, so Nyarlathotep.Tests (xUnit, net6.0) can compile it by linked files and run it without the game (S-21).
- Hook points (each in its own Patches/*.cs): SpawnTeamSystem_OnPersistenceLoad (init, exists), DeathEventListenerSystem (kills), VBloodSystem (feeds), PlayerCombatBuffSystem_OnAggro (boss engaged), CreateGameplayEventOnBehaviourStateChangedSystem (leash), SpawnTransformSystem_OnSpawn (only if S-8 needs it).
### External — dependencies and their failure behaviour
| Dependency | Version / contract | Slow or down | Garbage or incompatible |
|---|---|---|---|
| V Rising server + VampireReferenceAssemblies 1.1.12-r99041-b2 | pinned to the siblings; components and systems sampled per hook in the foundation and spikes children | n/a (in-process) | a renamed system makes its Harmony patch fail at load → "hook <name> unavailable, pillar disabled", other pillars run (D8); an exception inside a tick is caught and after 3 faults the event is cancelled (D25) |
| BepInEx.Unity.IL2CPP 6.0.0-be.733, VCF 0.10.* | pinned; VCF adminOnly is the authorization boundary | n/a | VCF missing → BepInEx refuses to load the plugin (hard dependency, declared) |
| Admin-edited JSON / cfg | EventDefinition SchemaVersion 1; cfg keys stable | n/a | invalid event disabled with reason (D15, D29); out-of-range cfg clamped (Business rules 9) |
| Disk (Persistence) | local filesystem | a failed write keeps state in memory, logs once, retries next second; a stale .tmp is deleted on boot | unreadable state.json is renamed state.json.corrupt and ignored (nothing permanent depends on it) |
| Other mods (Bloodcraft, KindredCommands, BloodyBoss, RaidForge) | we never write components on entities we did not spawn except via a carrier buff; we match only our own spawn keys | n/a | conflicts surface in D23 |
| gh / GitHub | gh 2.x, free tier; no quota concern at this volume | release step waits and is retried by hand; nothing is published partially | a failed `gh release create` leaves the tag; rerun uploads the asset |
| tcli / Thunderstore | tcli 0.2.4; free | publish retried later by hand; the GitHub release stands alone | a rejected package (e.g. icon size) fails D19/D20 before publish |
| Codex (reviewer) | codex-cli 0.151.0, read-only | after a 10-minute timeout or a failure, the review falls back to a fresh-context subagent (dod review.md order), recorded as such | a review produced without reading the plan is void and rerun |
Runtime dependencies (game, VCF, disk, JSON) are exercised by D8, D25, D29 and D35; their contracts are sampled by the spikes child (components and systems each hook reads) and the foundation child (VCF command registration, BepInEx config binding). Release tooling (gh, tcli, git tags) is process, not product: its contract is sampled on the first release by D20, D21 and D32, and no quota or cost applies at this volume; an incompatible tool version stops the release step, never the mod.

## Design
### Data
| Artifact | Where | Owner | Retention / deletion | Single copy? |
|---|---|---|---|---|
| kdpen.Nyarlathotep.cfg | BepInEx/config/ | server admin | until the admin deletes it; survives uninstall by design | yes |
| events.json, zones.json | BepInEx/config/Nyarlathotep/ | server admin | until deleted; the mod writes them only on `.nyar event enable/disable/set` and `.nyar zone add/remove`, keeping one .bak | no (.bak) |
| state.json | BepInEx/config/Nyarlathotep/ | the mod | rewritten atomically (.tmp + File.Replace) at most once per second; cleared of units and events by purge and the boot sweep | yes; losing it loses nothing permanent (units self-expire, marker sweep) |
| .tmp / .corrupt siblings | same folder | the mod | .tmp deleted on boot; .corrupt kept for the admin to inspect, overwritten by the next corruption | yes |
| BepInEx LogOutput.log | BepInEx/ | server operator | BepInEx overwrites it on each boot; contains admin names + SteamIDs for admin commands, never player positions | yes |
| Build outputs dist/, build/*.zip | repo, gitignored | developer | overwritten each build | no (GitHub release asset is the kept copy) |
| GitHub releases and tags | GitHub | repo owner | kept forever; a bad release is marked pre-release or deleted by the owner, the tag stays | no |
| Audit records, reviews, plans | docs/audits/, docs/dod/ | repo | kept in git forever | no (git history) |
tools/data-inventory.json is the machine-readable copy of this table and the authority for it; D34 fails when an artifact the code or the paths manifest names has no entry.
Every file carries SchemaVersion; a newer-than-known version is loaded read-only with a warning (D32), an older one is migrated in memory and written back only on the next admin edit (PREFLIGHT §6).
### States
Event instance: Scheduled → Active → Ending (despawn queue draining) → Ended; Cancelled on purge, fault limit (D25) or boot. First run: seeds events.json with one disabled template per pillar. Empty: no events → `.nyar status` says "No active events." Partial init: nothing runs until Core.IsReady; commands reply "still loading". Error: an invalid event is Disabled(reason).
Concurrency: Harmony hooks, VCF command handlers and the scheduler coroutine all run on the server main thread, so every mutation is serialised by construction in arrival order; no locks. Worked permutation (tested in D30): reload then purge then tick → the new definitions are loaded, every instance is cancelled, the tick starts nothing (cooldown); purge then reload then tick → same final state, since reload never starts instances; tick then purge → the event the tick started is cancelled with the rest. In each case each admin gets the reply of their own command, state.json ends with zero instances and zero units, and the tracker is empty after the drain. A purge sets PurgeCooldownSeconds (default 60) during which scheduled starts are suppressed, so a scheduler tick after a purge cannot restart an event (D11). `event reload` parses into a new set and swaps it in only if parsing finished; running instances keep the definition snapshot they started with, so a correction affects the next start, not a running event (D30). Restart: every event is cancelled on boot and marked survivors are queued for despawn (S-11, D13).
### Permissions
Actors: (a) server admin in game (VCF adminOnly), (b) server operator editing cfg/json on disk, who is fully trusted as the machine owner and may author any definition capability (the operator can replace the DLL anyway; definitions are validated for correctness, not restricted for trust), (c) players (only `.nyar`, `.nyar status`, `help`; everything else denied by VCF with its standard "no permission" reply, D26), (d) the game and other mods (triggers only: they read game state and cannot invoke commands or pass text), (e) builders: Claude and Codex (Codex read-only). Mutating commands log "admin <name> (<steamid>) ran <command>" to the BepInEx log. Any admin may change or stop any definition or event, whoever created it.
### UX
Chat root `.nyar` (S-4); `.nyar` lists the commands the caller may run; commands in docs/NYARLATHOTEP_DESIGN.md §6; replies one line each, at most 480 bytes, parse-friendly. Announcements from message pools, no positions (D16). The chat window, fonts, contrast and input belong to the game client; the mod's only accessibility levers are short plain-text lines where colour tags are decorative and never carry meaning alone.

## Security
- Authorization: VCF adminOnly on every mutating command (D5, D26), and every mutation behind it goes through Logic/ActionGateway, whose actor table (Admin, Operator, System) is tested exhaustively (D36). Scheduled and triggered paths act as System, which may only start and end enabled definitions; they take no player input. The debug fault injection is a cfg key (Operator), not a command.
- Injection: the sinks are chat (FixedString512Bytes, truncated to 480 bytes; templates substitute only {faction}, {minutes}, {event}, {zone}, validated by D29) and files (constant names only, D7, D31). No input reaches a shell, a URL or a query.
- Secrets: the mod has none. The GitHub credential lives in gh's own store (rotate with `gh auth refresh`, revoke at github.com Settings). The Thunderstore token lives only in the TCLI_AUTH_TOKEN environment variable of the publishing shell, is rotated on thunderstore.io when exposed, and is never echoed, logged or written to a file; D9 scans tracked files and build outputs.
- Personal data: admin names and SteamIDs appear only in LogOutput.log, which BepInEx overwrites on each boot; announcements and player replies carry no positions or names (D16).

## Failure & observability
| Failure class | What the admin sees | What they do next |
|---|---|---|
| Hook unavailable after a game patch | load line "hook <name> unavailable, pillar disabled"; `.nyar status` shows "degraded: <pillar>" | keep running other pillars; update the mod |
| Invalid event definition | log line with id and reason; `.nyar event list` shows it disabled with the reason | fix the JSON, `.nyar event reload` |
| Event faults in its tick | 3 logged faults, then "event <id> cancelled"; status shows degraded | report with the log; the event stays disabled until reload |
| Cap reached | log "clamped"/"skipped"; players see fewer units | raise the cap within the ceiling |
| Persistence write failed | one log line per failure streak | check disk space and permissions |
| Units not cleaned up | tracked count not returning to 0 in `.nyar status` | `.nyar purge confirm` |
- Health: every 10 minutes the log gets one "nyar health: <n> events, <m> tracked, degraded: <list>" line, and an admin who logs in while any pillar is degraded gets one private chat line saying so (foundation child). There is no external dashboard; the server log and the login notice are the alert.
- Debug.VerboseLogging, Debug.TimingLog and Debug.FaultInjection exist from the foundation on (DEV_REMINDERS #34).
- Gating-control matrix (one evidence command per gating probe):

| Probe | Evidence | Fails on | Silent on | Empty input |
|---|---|---|---|---|
| 2.1 actors | D5 preflight commands check | a mutating [Command] without adminOnly, or one outside Commands/ | allow-listed nyar/status/help | no .cs files → failure "commands: none found" |
| 3.3 persistence | D34 preflight data inventory (with D7 as the write fence) | a Persistence constant or manifest glob with no inventory entry | a complete inventory | missing data-inventory.json → failure |
| 4.4 precedence | D27 ControlPrecedenceTests | a lower control winning a conflict | an unconflicted definition | zero cases in the matrix → test fails |
| 6.2 dependencies | D35 DependencyFailureTests | a disk, JSON or hook fault escaping its scope | healthy fakes | zero fault cases → test fails |
| 10.1 authorization | D36 AuthorizationTests | an ActionKind without a table row, or System/non-admin granted beyond its row | the declared table | an empty ActionKind enum → test fails |
| 10.3 secrets | D9 preflight secrets scan | a planted tss_/ghp_/Bearer string or an env read | ordinary text containing "token" | no files to scan → failure |
| 12.4 failing cases | D10 preflight -SelfTest | a check passing its bad or empty fixture, or a manifest/function mismatch | good fixtures | an empty manifest → failure |
| 14.3 rollback | D32 rollback-drill.ps1 | N-1 failing to start on N's state, wrong schema line, bad tag order, out-of-manifest range | an unchanged schema | fewer than two release tags → failure "rollback drill: needs two releases" (run from 0.3.0 on) |
| 14.4 paths | D33 preflight -Paths | a walked path matching no glob | manifested paths | empty manifest → failure |
Each check's fixture spells real input: fixtures are copies of real repository files with one planted change, never hand-written approximations.

## Performance
Budget: scheduler tick < 1 ms idle, < 5 ms at caps (D24). Hot paths: the empowerment sweep (at most EmpowerBatchPerTick=200 units per tick) and the despawn queue (MaxDespawnsPerTick=5). Caps: MaxTrackedUnits 150, MaxUnitsPerWave 20, MaxConcurrentEvents 3 (Config/Settings.cs), with hard ceilings (Business rules 9). A cap reached skips the spawn and logs it (D14); the despawn limit came from Beelzebub's 36-destroys-in-one-frame crash (DEV_REMINDERS #8). The valid case the default excludes is a 300-unit spectacle event, which an admin can reach by raising the cap up to the 500 ceiling; D24 is measured at the default cap, and a raised-cap run is recorded in the foundation child's test results.

## Build plan
Every step runs inside the procedure in `## Rollout` › Procedure (pre-audit, build, post-audit, Codex cross-inspection).
1. Extend tools/preflight.ps1: add checks for D4 D5 D6 D7 D8 D9 D17 D18 D19 D21 D33 D34, each a function named Test-Check<Name> returning pass/fail and one summary line; create tools/preflight-checks.json (check name, inputs, fixtures, child-to-feature-doc mapping) and tools/preflight-fixtures/<check>/{good,bad,empty}/; add -SelfTest that runs every manifest entry against its three fixtures and reports "selftest: n/n checks, 3 fixtures each"; create tools/paths-manifest.txt from Rollout › Paths walked (tracked, ignored and server globs) and tools/data-inventory.json from Design › Data; add the -Paths mode that walks tracked, untracked, ignored and server paths after a build. · satisfies D4, D5, D6, D7, D8, D9, D10, D17, D18, D21, D33, D34
2. Repository identity: confirm the public repo and default branch main (created 2026-09-23, commit 8c92928); create Nyarlathotep/Nyarlathotep/icon.png (256x256) by scaling the whole artwork/icon-option-2-dragon.png (S-16, never cropped) and docs/img/nyarlathotep-cover.jpg (the same image, 1024 px wide) linked from README.md; artwork/ stays gitignored. · satisfies D1, D19
3. Process records: CLAUDE.md "Development procedure" section and dod pointer block, docs/audits/README.md template (done 2026-09-23); regenerate the dod index after every plan edit. · satisfies D22
4. Plan child `spikes` with dod (plan → Codex review → approve), then build S1–S3 on the local server and record results in docs/features/SIEGES.md, EVENT_SPAWNS.md, FACTION_EMPOWERMENT.md. · satisfies D13
5. Plan and build child `foundation`: Logic/ (incl. ActionGateway, IFileStore, IHookRegistry) + Nyarlathotep.Tests (xUnit, linked files, added to Nyarlathotep.sln), Persistence, EventStore + validation, SpawnTracker, UnitSetup, TriggerBus, EventScheduler, EventRuntime, Announcer, health line, commands status/event/spawn/purge/debug. · satisfies D2, D11, D14, D15, D16, D24, D25, D26, D27, D28, D29, D30, D31, D35, D36
6. Plan and build child `faction-empowerment`. · satisfies D12, D23
7. Plan and build child `event-spawns`. · satisfies D12
8. Plan and build child `boss-reinforcements`. · satisfies D12
9. Plan and build child `defended-zones`. · satisfies D12
10. Plan and build child `sieges` (MVP scope per S-6). · satisfies D12
11. For each child that closes: chore(release) commit moving the six surfaces (preflight D3), annotated tag, GitHub release with the tcli zip; create tools/rollback-drill.ps1 with the first release and run it from 0.3.0 on (D32); Thunderstore publication (tcli publish with TCLI_AUTH_TOKEN from the environment) once foundation, faction-empowerment and event-spawns are done (S-17). · satisfies D3, D20, D21, D32
12. Epic close: run D11–D16, D23–D26 on the local server, regenerate the dod index, `dod close nyarlathotep`. · satisfies D22

## Work breakdown
- W1 · **Tooling and process**
- W1.1 · **Preflight checks** · items: D4 D5 D6 D7 D8 D9 D10 D33 D34 · steps: 1
- W1.2 · **Process records** · items: D17 D18 D22 · steps: 1, 3, 12
- W1.3 · **Repository and identity** · items: D1 D19 · steps: 2
- W2 · **Children**
- W2.1 · **Spikes and foundation** · items: D2 D11 D13 D14 D15 D16 D24 D25 D26 D27 D28 D29 D30 D31 D35 D36 · steps: 4, 5
- W2.2 · **Pillars** · items: D12 D23 · steps: 6, 7, 8, 9, 10
- W3 · **Release**
- W3.1 · **Packaging, tags, rollback** · items: D3 D20 D21 D32 · steps: 11

## Rollout
### Procedure (applies to every child)
- **Plan:** each child is planned with `dod plan` (parent: nyarlathotep) and reviewed by Codex read-only (Claudex phase 2) before approve.
- **Pre-audit** (before each Build-plan step; recorded in docs/audits/<slug>.md): git status clean, compile check (D2 command), `pwsh tools/preflight.ps1`, `dod status <slug>`, the feature doc's Status/Open questions read, and for in-game steps a baseline boot of the current DLL with the log checked for errors.
- **Build:** Claude builds (Claudex phase 3, Claude-builds direction).
- **Post-audit** (after each step): compile check, preflight, `dotnet test` for Nyarlathotep.Tests, `/code-review` on the diff, a fresh read-only Codex cross-inspection of the diff with its verdict line written to the audit record, in-game verification on the local server with results in the feature doc's "## Test results", `dod status <slug>` with evidence lines.
### Shipping
Each child closes with a minor version (0.2.0 foundation, then one minor per pillar; S-19). Everything ships disabled (D4); the admin turns pillars on per server. Who can turn it off: any admin (`.nyar purge`, pillar switches) or the operator (delete the DLL, D12).
### Compatibility
Plugin GUID kdpen.Nyarlathotep and the cfg keys are stable from v0.1.0; renaming a key requires a migration note in both changelogs. events.json SchemaVersion starts at 1. Existing chat reply formats stay unchanged when a machine-readable mode is added later (S-20).
### Rollback
Per release: install the previous tag's DLL from its GitHub release (D32). Data written by a newer version stays loadable by the older one because newer-schema files load read-only. Live state: `.nyar purge confirm` (D11); worst case delete the DLL (D12). Commit range: a child's work lies between its predecessor's release tag and its own tag; `git revert <prev-tag>..<tag>` undoes it, since the only committed generated file is docs/dod/README.md, regenerated after the revert.
### Paths walked
Committed: Nyarlathotep/Nyarlathotep.sln; Nyarlathotep/Nyarlathotep/** (source incl. Logic/, icon.png, README.md, CHANGELOG.md, thunderstore.toml, LICENSE, Resources/events.default.json); Nyarlathotep/Nyarlathotep.Tests/**; README.md, CHANGELOG.md, CLAUDE.md, LICENSE, .gitignore; docs/** (NYARLATHOTEP_DESIGN.md, DEV_REMINDERS.md, GAME_ASSETS.md, RESEARCH_NOTES.md, PREFLIGHT.md, DOC_STYLE.md, features/*.md, audits/*.md, img/*, dod/*.md incl. the generated dod/README.md and profile.md, dod/*.reviews.md, dod/*.html review pages, dod/wbs.*); tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/**, tools/paths-manifest.txt, tools/data-inventory.json, tools/rollback-drill.ps1. Ignored: artwork/, dist/, build/, bin/, obj/, .claude/, Reference Data/, Learning Mods/, *.zip. Outside git: ~/.dod/feedback-consent.json; server side BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/*.json (+ .bak, .tmp, .corrupt); GitHub release assets.

## Child constraints
Constraints each child inherits (a child's review checks they appear verbatim or by reference):
- **spikes** — S1 movement, S2 restart/marker/DontSaveEntity, S3 carrier buff on a native NPC; each spike's result recorded in its feature doc with a go/no-go line; no spike code ships (it lives behind a debug command removed before the child closes). Samples the component and system contracts each later hook depends on.
- **foundation** — owns D11 D13 D14 D15 D16 D24 D25 D26 D27 D28 D29 D30 D31 D35 D36; input families it defines and validates: cfg keys (types, ranges, ceilings), events.json (every field's type, range and error message), command arguments (arity, types, unknown ids → one-line error); Persistence is the only file writer (D7); SpawnTracker is the only spawner and the only destroyer of units; purge and boot sweep drain through the despawn budget; restart policy S-11; events.json schema S-5; main-thread serialisation, reload snapshot and purge cooldown per Design › States; health line and admin login notice per Failure & observability.
- **faction-empowerment** — carrier buffs only (no direct stat writes on native NPCs), BuffType.Replace, LifeTime = remaining seconds; excludes Faction_Players*, Prefab entities, our own units, V Bloods unless opted in; late arrivals per S-8; owns the item for Business rules 2.
- **event-spawns** — owns the SpawnWaves action and its input validation (composition, counts, radii, modifiers); every unit through SpawnTracker with finite LifeTime, DestroyWhenDisabled and the marker; loot per S-10; refuses spawn points inside claimed territory unless the event allows it.
- **boss-reinforcements** — keys fights by boss entity, dedupes triggers within 5 s, despawns adds on boss death or reset, excludes BloodyBoss-renamed bosses by default.
- **defended-zones** — owns zones.json input validation (name charset and length, radius range, overlap with claimed territory rejected); per-zone cooldown; announcements name the zone, never a player.
- **sieges** — MVP scope per S-6; target eligibility per S-13 re-checked every tick (ineligible → end + despawn); announcements to the owner's clan only; community-admin pre-clearance recorded in docs/features/SIEGES.md before the release that ships it.

## Children
- spikes · in-progress
- foundation · planned
- faction-empowerment · planned
- event-spawns · planned
- boss-reinforcements · planned
- defended-zones · planned
- sieges · planned

## Out of scope
- Deferred beyond v1.0, each a future plan slug in docs/dod/ (not children of this epic): `structure-damage` (siege phase 2, HookDOTS, RaidForge deferral; S-6), `faction-heat`, `bloodmoon-trigger`, `map-markers`, `raphael-api` (S-20).
- Any client-side component. Any edit to sibling workspaces or Reference Data/Learning Mods.
- Automated in-game tests: V Rising has no headless test harness; pure logic is unit-tested (Nyarlathotep.Tests), everything touching the ECS is verified manually on the local server.

## Also considered
- Compliance: sieges touch other players' property → off by default, clan-only warnings, community-admin pre-clearance before release (sieges child).
- Localisation: English only; message pools are admin-editable, which is the localisation path.
- Running cost: none beyond server tick time (D24).
- Operational ownership: the server admin; the runbook is the Thunderstore README's "Kill switch" and "Uninstall" sections.
- Documentation and changelog: six surfaces per release (CLAUDE.md), feature docs updated with the code.
- Analytics: none; Faust already covers server analytics.
- Decommissioning: nothing replaced.
- Support tooling: `.nyar status`, `.nyar debug here`, VerboseLogging, the health line.

## Assumptions
- S-1 · validated · The local dedicated server exists at C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer with BepInEx and VCF installed · source: BepInEx/plugins listing 2026-09-23
- S-2 · validated · Codex CLI 0.151.0, Node v22.16.0 and tcli 0.2.4 are installed · source: version commands 2026-09-23
- S-3 · validated · The public repo exists and gh is authenticated as KDavidP1987 · source: gh repo create 2026-09-23
- S-4 · validated · Chat root is `.nyar` (design D1) · source: user confirmed 2026-09-23
- S-5 · validated · One unified events.json of EventDefinition records, trigger and action mixed freely, SchemaVersion 1 (design D2) · source: user confirmed 2026-09-23
- S-6 · validated · Sieges ship as a harassment MVP (defenders, servants, exposed pieces under vanilla rules); structure damage is a later phase behind its own off-by-default switch that defers to RaidForge (design D3) · source: user confirmed 2026-09-23
- S-7 · validated · Our units carry our own inert marker buff with a distinct magic value, validated in spike S2; BlockFeedBuff is not used (design D4) · source: user confirmed 2026-09-23
- S-8 · validated · Empowerment sweeps at start and re-sweeps every 15 s with the remaining time; the spawn hook is added only if the sweep proves too coarse (design D5) · source: user confirmed 2026-09-23
- S-9 · validated · Waves use our own spawner (InstantiateEntityImmediate); vanilla War Events stay a possible later action type (design D6) · source: user confirmed 2026-09-23
- S-10 · validated · Loot is a per-event setting defaulting to off; XP is vanilla (design D7) · source: user confirmed 2026-09-23
- S-11 · validated · Every event is cancelled on boot and marked survivors are swept; resuming is out of scope for v1 (design D8) · source: user confirmed 2026-09-23
- S-12 · validated · Players see announcements and a player `.nyar status` (active events and time left, no positions) (design D9) · source: user confirmed 2026-09-23
- S-13 · validated · Siege eligibility is admin-configurable: owner or a clan member online, or last online within RecentlyOnlineHours (anti logout-dodge); optional minimum castle-heart level (not gear level, which players can swap); sealed or decaying hearts never; PvE/PvP availability per event; re-checked every tick (design D10) · source: user confirmed 2026-09-23
- S-14 · validated · Schedules support both the real server-local clock and the in-game day/night cycle (design D11) · source: user confirmed 2026-09-23
- S-15 · validated · The Rollout › Procedure applies to every build step: pre-audit, build, post-audit with /code-review and a fresh read-only Codex cross-inspection, in-game test · source: user confirmed 2026-09-23
- S-16 · validated · The icon is the whole dragon artwork (artwork/icon-option-2-dragon.png) scaled to 256x256, not cropped; the same image is the README cover · source: user confirmed 2026-09-23
- S-17 · validated · First Thunderstore publication after foundation, faction-empowerment and event-spawns pass in-game (about 0.4.0); GitHub releases before that · source: user confirmed 2026-09-23
- S-18 · reversible · Children run in the order spikes, foundation, faction-empowerment, event-spawns, boss-reinforcements, defended-zones, sieges · fallback: reorder the Children list before a child starts; only boss-reinforcements, defended-zones and sieges depend on event-spawns' SpawnWaves
- S-19 · reversible · Each closed child is a minor version bump (0.2.0 foundation onward) and 1.0.0 is the epic close · fallback: numbering may change only before the first Thunderstore publication; after it, versions move forward only and a mistake is corrected by the next version
- S-20 · reversible · Raphael integration is deferred; replies stay parse-friendly and a later machine-readable mode is opt-in per call, leaving existing reply formats unchanged (design D12) · fallback: the raphael-api plan adds the mode without touching existing replies
- S-21 · reversible · Pure logic lives in Logic/ without game types and is unit-tested by Nyarlathotep.Tests through linked files · fallback: if linking proves brittle, move Logic/ into a separate net6.0 class library referenced by both projects; the tests do not change

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D5 D16 D26 D36; 2.2 D5 D26; 2.3 prose: any admin may change any definition; siege ownership changes are re-checked every tick by the sieges child (S-13) |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D15 D29; 3.2 D16; 3.3 D34 D7 D31; 3.4 prose: SchemaVersion starts at 1; older files migrate in memory, newer load read-only |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D14 D29; 4.2 D6 D13; 4.3 D28 D13; 4.4 D27 D14; 4.5 D5 D10 D17 D33 D34 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D8; 5.2 D6 D23; 5.3 D29 D15 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D2 D20; 6.2 D35 D8 D25; 6.3 prose: no sandbox mode exists; the local dedicated server is the test bed and spike code is removed before spikes closes |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D15 D25; 7.2 D30 D11; 7.3 D13 D30 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D4; 8.2 D13 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D14 D24; 9.2 D5 D15 D29; 9.3 D30 D11 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D36 D5 D26; 10.2 D29 D31; 10.3 D9; 10.4 D16 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D5; 11.2 D16; 11.3 prose: chat-only surface owned by the game client; short plain lines, colour never carries meaning alone; 11.4 prose: runs only after an admin enables a pillar and an event; the log line event started shows it did |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D15 D25; 12.2 D14; 12.3 D24 D25; 12.4 D10 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D24; 13.2 D14 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D4 D17; 14.2 D3; 14.3 D32 D11 D12; 14.4 D22 D33 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline
- [ ] D1 · **Public GitHub repo** repository KDavidP1987/Nyarlathotep-Lord-of-Chaos is public and its default branch is main · cmd: gh repo view KDavidP1987/Nyarlathotep-Lord-of-Chaos --json visibility,defaultBranchRef → visibility PUBLIC, defaultBranchRef.name main (fails when: the repo is private or missing, or the default branch is master)
- [ ] D2 · **Compiles clean** the solution (plugin and Nyarlathotep.Tests) builds in Release with 0 errors and 0 warnings without deploying · cmd: dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ → "0 Warning(s)" and "0 Error(s)" (fails when: any source file has a compile error or a new warning)
- [ ] D3 · **Release surfaces in sync** preflight passes its version, changelog and description checks · cmd: pwsh tools/preflight.ps1 → exit 0 (fails when: csproj Version differs from thunderstore.toml versionNumber, or either CHANGELOG lacks the version)
- [ ] D4 · **Pillars ship off** every [Pillars] Bind default in Config/Settings.cs is false and every event in Resources/*.json has "enabled": false · cmd: pwsh tools/preflight.ps1 → line "pillar defaults: all off" (fails when: a Pillars Bind default is true or a shipped template event is enabled)
- [ ] D5 · **Admin-only commands** every [Command] attribute in any tracked or untracked-not-ignored .cs file (git ls-files plus git ls-files --others --exclude-standard) lives under Commands/ and carries adminOnly: true except the allow-list {nyar, status, help} · cmd: pwsh tools/preflight.ps1 → line "commands: <n> admin-only, <m> public (allow-listed)" (fails when: a command outside the allow-list lacks adminOnly: true, or a [Command] attribute exists outside Commands/)
- [ ] D6 · **Structural edits fenced** EntityManager.AddComponent, RemoveComponent, AddBuffer and DestroyEntity calls appear only in EntityExtensions.cs, whose helpers refuse any entity carrying the Prefab component · cmd: pwsh tools/preflight.ps1 → line "structural edits: fenced" (fails when: one of those calls appears in any other tracked .cs file)
- [ ] D7 · **One data folder** File.Write*, File.Replace, File.Move, File.Delete and Directory.Create calls appear only in Services/Persistence.cs, which exposes no path or file-name parameter and writes only the constant names events.json, zones.json, state.json and their .bak/.tmp siblings under BepInEx/config/Nyarlathotep/ · cmd: pwsh tools/preflight.ps1 → line "file writes: fenced" (fails when: a file-write call appears in any other tracked .cs file)
- [ ] D8 · **Hooks fail safe** every [HarmonyPrefix]/[HarmonyPostfix] method in Patches/*.cs returns early unless Core.IsReady and wraps its body in try/catch · cmd: pwsh tools/preflight.ps1 → line "patch guards: <n>/<n>" (fails when: a patch method lacks the IsReady check or the catch)
- [ ] D9 · **No secrets committed** no tracked, staged or untracked-not-ignored file, no file under dist/ or build/ (including inside built zips) and no captured log under tools/preflight-fixtures/ contains a Thunderstore token (tss_), a GitHub token (ghp_, gho_, ghu_, ghs_, ghr_, github_pat_), an "Authorization: Bearer" header, or a TCLI_AUTH_TOKEN or GH_TOKEN assignment, and no .cs file calls Environment.GetEnvironmentVariable · cmd: pwsh tools/preflight.ps1 → line "secrets: none" (fails when: any scanned location contains one of those forms or a .cs file reads the environment)
- [ ] D10 · **Preflight self-test** tools/preflight-checks.json lists every preflight check with a good, a bad and an empty fixture under tools/preflight-fixtures/<check>/; the check list is derived independently by scanning tools/preflight.ps1 for functions named Test-Check*, and the manifest must equal that list both ways; each check passes good, fails bad, and fails empty (a missing input is never a pass) · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, 3 fixtures each" (fails when: a Test-Check function is missing from the manifest or vice versa, a check passes its bad or empty fixture, fails its good one, or an entry lacks a fixture)
- [ ] D11 · **Kill switch** `.nyar purge` then `.nyar purge confirm` ends every active event, suppresses scheduled starts for PurgeCooldownSeconds, and leaves 0 tracked units, despawning at most Limits.MaxDespawnsPerTick per tick; a second confirm is a no-op · manual: on the local server start one spawn event with 30 units and one empowerment event; run `.nyar purge`, `.nyar purge confirm`, then `.nyar purge confirm` again; `.nyar status` shows 0 active events and 0 tracked units within 10 s, the second confirm replies "nothing to purge", and the log shows despawn batches no larger than the budget
- [ ] D12 · **Clean uninstall** removing Nyarlathotep.dll mid-event leaves no spawned unit and no carrier buff behind · manual: start a spawn event and an empowerment event, stop the server, delete Nyarlathotep.dll, start the server, wait the longest configured unitLifetime; reinstall with General.Enabled=false; the boot log reports "marker sweep: 0 found" and a unit of the empowered faction shows base stats via `.nyar debug here`
- [ ] D13 · **No orphans after restart** a restart mid-event removes every surviving marked unit on boot and cancels active events · manual: start a 30-unit spawn event, stop the server with units alive, start it; the log shows "marker sweep: <k> found, <k> queued" and `.nyar status` shows 0 tracked units and 0 active events after the drain
- [ ] D14 · **Caps beat definitions** Limits in kdpen.Nyarlathotep.cfg clamp every event definition and are logged · manual: set MaxUnitsPerWave=5, MaxTrackedUnits=8; start an event requesting 2 waves of 20; exactly 5 then 3 units spawn and the log shows "clamped by MaxUnitsPerWave" and "skipped by MaxTrackedUnits"
- [ ] D15 · **Bad JSON survives** a malformed or out-of-range events.json never stops the server: the bad event is disabled with a logged reason and the rest load · manual: add one event with an unknown unit GUID and one with a syntax error to a copy, run `.nyar event reload`; the log names each rejected event and its reason, `.nyar event list` shows the valid ones, the server keeps ticking
- [ ] D16 · **Players see no positions** non-admin replies and every announcement contain no coordinates, zone radii or player names · manual: as a non-admin run `.nyar` and `.nyar status` during a running event and read every announcement of one event of each pillar; none contains a number pair, a zone radius or a player name
- [ ] D17 · **Audit record per child** every child listed under this plan's ## Children whose plan status is done has docs/audits/<slug>.md with "## Pre-audit", "## Post-audit" and "Codex verdict:" · cmd: pwsh tools/preflight.ps1 → line "audits: <n>/<n> done children recorded" (fails when: a done child has no audit file or the file lacks one of the three markers)
- [ ] D18 · **Test results per pillar** every done child's feature doc (mapping in tools/preflight-checks.json) has a "## Test results" section with at least one YYYY-MM-DD entry · cmd: pwsh tools/preflight.ps1 → line "feature results: <n>/<n>" (fails when: a done child's doc lacks the section or a dated entry)
- [ ] D19 · **Thunderstore icon** Nyarlathotep/Nyarlathotep/icon.png exists and its PNG header states 256x256 · cmd: pwsh tools/preflight.ps1 → line "icon: 256x256" (fails when: the file is missing or states another size)
- [ ] D20 · **Package builds** tcli builds the Thunderstore zip from the staged dist · cmd: cd Nyarlathotep/Nyarlathotep && tcli build → build/kdpen-Nyarlathotep-<version>.zip created (fails when: icon.png, README.md, dist/BepInEx/plugins/Nyarlathotep.dll or CHANGELOG.md is missing)
- [ ] D21 · **Tagged releases** every commit whose subject matches ^chore\(release\): v\d+\.\d+\.\d+ has an annotated tag of that version pushed to origin · cmd: pwsh tools/preflight.ps1 → line "release tags: <n>/<n>" (fails when: a release commit has no tag, or zero release commits exist after the first release was expected)
- [ ] D22 · **Plan index fresh** the dod store index matches the plans · cmd: node C:/Users/KDPen/.claude/skills/dod/scripts/dod-index.mjs --check-index --dir docs/dod → exit 0 (fails when: a plan was edited without regenerating the index)
- [ ] D23 · **Coexists with common mods** with Bloodcraft and KindredCommands installed, no familiar is empowered, swept or counted as ours, and both mods' spawns still work · manual: install both on the local server, summon a Bloodcraft familiar, run an empowerment event on the familiar's source faction and a spawn event; `.nyar status` tracked count equals our spawns only, the familiar's stats are unchanged, KindredCommands' spawnnpc still spawns
- [ ] D24 · **Tick budget** with Debug.TimingLog=true the scheduler tick averages under 1 ms idle, under 5 ms at the default MaxTrackedUnits (150) and under 15 ms at the 500 ceiling, each over 5 minutes · manual: enable TimingLog, run idle 5 min, a 150-unit event 5 min, then with MaxTrackedUnits=500 a 500-unit event 5 min; read the three logged averages
- [ ] D25 · **Tick survives faults** an exception inside an event's tick is caught, logged with the event id, and after 3 consecutive failures the event is cancelled and its units queued for despawn while other events keep running · manual: set Debug.FaultInjection=<event id> on the local server with two events running; the log shows 3 caught faults then "event <id> cancelled after 3 faults", the other event continues, and `.nyar status` lists the degraded pillar
- [ ] D26 · **Non-admins denied** every admin-only command run by a non-admin is refused by VCF and changes nothing · manual: as a non-admin run each command listed by `.nyar` for admins (event start/stop/enable/disable/reload/set, zone add/remove, spawn, purge, debug); each is refused and `.nyar status` is unchanged
- [ ] D27 · **Precedence tests** the precedence resolver applies purge > General.Enabled > pillar switch > caps > definition when they conflict simultaneously · test: Nyarlathotep.Tests ControlPrecedenceTests (fails when: any lower-precedence control overrides a higher one in the conflict matrix)
- [ ] D28 · **Schedule tests** real-clock schedules fire once per matching local minute (a DST-repeated hour fires once, a skipped hour is not replayed, a backward clock change does not re-fire), and in-game schedules fire on the day/night edge · test: Nyarlathotep.Tests ScheduleTests (fails when: a repeated local time fires twice or a skipped time is replayed)
- [ ] D29 · **Validation tests** EventDefinition validation rejects unknown fields, unknown trigger/action types, out-of-range numbers, level delta beyond ±5, empty compositions and templates with unknown placeholders, with one reason per rejection · test: Nyarlathotep.Tests EventValidationTests (fails when: an invalid definition is accepted or a valid one rejected)
- [ ] D30 · **Duplicate-action tests** starting an active event, stopping a stopped one, a second purge confirm, a double reload and a trigger repeated within 5 s each leave exactly one outcome · test: Nyarlathotep.Tests IdempotencyTests (fails when: a repeated action creates a second instance or a second effect)
- [ ] D31 · **Persistence path tests** Persistence resolves only its constant file names under the data folder and refuses rooted paths, .. segments and reparse points · test: Nyarlathotep.Tests PersistencePathTests (fails when: any resolved path lies outside BepInEx/config/Nyarlathotep/)
- [ ] D32 · **Rollback drill** a script installs release N, boots the local server, edits one event, stops, installs N-1, boots again, and checks the log for "Nyarlathotep initialized", the schema line (read-only warning when N's SchemaVersion is newer, none otherwise) and "marker sweep"; it also checks that <N-1 tag> is an ancestor of <N tag> and that `git diff --name-only <N-1>..<N>` lists only manifest paths · cmd: pwsh tools/rollback-drill.ps1 -From v<N> -To v<N-1> → "rollback drill: pass" (fails when: N-1 does not initialize on N's state, the schema line is wrong, the tags are not ancestor-ordered, or the range touches a path outside tools/paths-manifest.txt)
- [ ] D33 · **Paths manifest** after `dotnet build`, `tcli build` and a deploy, every path from git ls-files, git ls-files --others --exclude-standard, git status --ignored --porcelain, and the server's BepInEx/plugins/Nyarlathotep* and BepInEx/config/Nyarlathotep/* matches a tracked, ignored or server glob in tools/paths-manifest.txt, which mirrors Rollout › Paths walked · cmd: pwsh tools/preflight.ps1 -Paths → line "paths: <n> walked, all in manifest" (fails when: any walked path matches no manifest glob)
- [ ] D34 · **Data inventory complete** tools/data-inventory.json has an entry with location, owner, retention, deletion and singleCopy for every constant file name in Services/Persistence.cs and every ignored or server glob in tools/paths-manifest.txt · cmd: pwsh tools/preflight.ps1 → line "data inventory: <n>/<n> complete" (fails when: a Persistence constant or manifest glob has no entry, or an entry lacks one of the five fields)
- [ ] D35 · **Dependency failure tests** through Logic/ abstractions (IFileStore, IHookRegistry), a failing or slow disk write keeps state in memory and retries once per second, an unreadable state.json is renamed .corrupt and ignored, garbage JSON disables only the bad event, and an unavailable hook disables only its pillar · test: Nyarlathotep.Tests DependencyFailureTests (fails when: any of the four faults stops another pillar, loses in-memory state or throws out of the tick)
- [ ] D36 · **Authorization path tests** every mutating operation is an ActionKind executed through Logic/ActionGateway, whose table grants each ActionKind to actors Admin, Operator (file load) or System (schedule and triggers); System may only start and end enabled definitions; the test enumerates every ActionKind × actor, and a preflight check (Test-CheckGatewayOnly) fails when any file under Commands/, Patches/ or Services/ other than Logic/ActionGateway.cs and the services it dispatches to calls a mutating EventRuntime, SpawnTracker, EmpowerAction, WaveAction or Persistence method directly · test: Nyarlathotep.Tests AuthorizationTests (fails when: an ActionKind has no table entry, a System or non-admin actor is granted anything beyond its row, or the gateway-only check finds a direct call)

## Amendments
- A1 · 2026-09-23 · discovered · ~D8 · layer: 5.1 · the init patch runs before Core.IsReady and sets it, so it cannot return early unless ready; D8 now names that one inverse-guard exception and requires guard-then-try/catch structure (step-1 cross-inspection F4)

## Log
- 2026-09-23 · status → draft · plan
- 2026-09-23 · note · decisions S-4 to S-17 confirmed by the user in plan mode
- 2026-09-23 · note · review 1 (codex) REVISE, 26 findings; plan revised: +D25 to D33, unit-test project (S-21), dependency and data tables, gating-control matrix, duplicate-action and time rules, deferred slugs
- 2026-09-23 · note · review 2 (codex) REVISE, 13 findings; +D34 data inventory, +D35 dependency-failure tests, +D36 authorization gateway tests; D32 scripted; D5 D9 D10 D24 D33 widened
- 2026-09-23 · note · review 3 (codex) REVISE at the 3-round cap; F6 applied (gateway-only check in D36); F2-F5, F7-F9 reclassified advisory by rule; taken to human review
- 2026-09-23 · status → ready · approve · review: human
- 2026-09-23 · status → in-progress · start
- 2026-09-23 · D1 · pass · cmd: gh repo view → visibility PUBLIC, defaultBranchRef main · 184fb41 · claude
- 2026-09-23 · note · A1 recorded (~D8, init patch exception) during Build step 1 post-audit
- 2026-09-23 · D4 · pass · cmd: pwsh tools/preflight.ps1 → "pillar defaults: all off (5 switches, 0 templates)" · bf25842 · claude
- 2026-09-23 · D5 · pass · cmd: pwsh tools/preflight.ps1 → "commands: 0 admin-only, 1 public (allow-listed)" · bf25842 · claude
- 2026-09-23 · D6 · pass · cmd: pwsh tools/preflight.ps1 → "structural edits: fenced (0 Prefab-guarded calls in EntityExtensions.cs)" · bf25842 · claude
- 2026-09-23 · D7 · pass · cmd: pwsh tools/preflight.ps1 → "file writes: fenced (0 calls in Services/Persistence.cs, no Persistence.cs yet)" · bf25842 · claude
- 2026-09-23 · D8 · pass · cmd: pwsh tools/preflight.ps1 → "patch guards: 1/1" · bf25842 · claude
- 2026-09-23 · D9 · pass · cmd: pwsh tools/preflight.ps1 → "secrets: none (187 files scanned, 39 index blobs)" · bf25842 · claude
- 2026-09-23 · D10 · pass · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: 16/16 checks, 3 fixtures each, 18 extra bad fixtures" · bf25842 · claude
- 2026-09-23 · D19 · pass · cmd: pwsh tools/preflight.ps1 → "icon: 256x256" · bf25842 · claude
- 2026-09-23 · D33 · pass · cmd: pwsh tools/preflight.ps1 -Paths → "paths: 199 walked, all in manifest" · bf25842 · claude
- 2026-09-23 · D34 · pass · cmd: pwsh tools/preflight.ps1 → "data inventory: 29/29 complete" · bf25842 · claude
