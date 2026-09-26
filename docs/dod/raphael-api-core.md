---
dod: 2
rubric: 2
id: dod-20260925-rac1
slug: raphael-api-core
title: Raphael api 2 — status, events, push subscription, paging, contract check
status: draft
size: L
parent: nyarlathotep
kind: feature
created: 2026-09-25
baselined: none
closed: none
commit: 2dedb8f
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: pending
review: pending
---

# DoD: Raphael api 2 — status, events, push subscription, paging, contract check

**Size:** L. It touches several modules: Logic (Wire, new ApiLines, Paging, Subscriptions and PushQueue), Services (a new Pusher, with calls from EventRuntime, WaveAction, Announcer and EventStore), Patches (a new disconnect patch), Commands (a new ApiCommands file) and tools (preflight, a new rollback-drill.ps1). It also changes an external contract, docs/RAPHAEL_INTEGRATION_CONTRACT.md, from api 1 to api 2. It adds no new dependency and no new data file.
**Planned:** interactively. This is a child of the approved Epic `nyarlathotep`, and its `## Child constraints` › raphael-api-core entry (A20) governs this plan. The owner settled two decisions in plan mode on 2026-09-25 (S-1, S-2).
**Request:** "I'd like to continue developing it out to enhance the base abilities of Nyarl and functionality in conjunction with Raphael before publishing." The owner's plan decision 1A (Epic A20) reads: "a new child raphael-api-core is built next and gives api 2 for what exists: api status (live events), api events (definitions, paged), api sub push lines (event-start/end, wave-warn, wave, killswitch, config-changed), the §4 paging and error lines, and the contract-sync preflight check (Epic D38)."

## Definition of Done
- [ ] D1 · **Status rows** `.nyar api status` (anyone) replies one `[NYAR:event]` line per active event, then one per ended event whose units are still waiting out the grace (state=ending, left = seconds until its despawn), then `[NYAR:end] cmd=status count=<rows sent>`, each line its own reply. Row keys in order: id kind name state faction left wave units. kind=waves for the Spawns pillar, faction=-, wave=<spawned>/<total>. units is the event's tracked unit count for an admin and `-` for a player. No row carries a coordinate, radius or player key. With no events the reply is only `[NYAR:end] cmd=status count=0` · test: Nyarlathotep.Tests ApiLinesTests (fails when: a key is missing or out of the contract's order, a player row carries a unit count, any row carries x, y, z, r or a player name, an ending event has no row, or count differs from the rows built)
- [ ] D2 · **Definition rows** `.nyar api events [page]` is adminOnly. It replies one `[NYAR:def]` line per definition of the page (keys in order: id name enabled trigger action duration state reason), then the end line of D3. trigger maps Manual, Schedule, GameTime and VBloodKilled to manual, schedule, ingame and vbloodkilled; action=waves. state is active while the event runs, else disabled when the definition is not startable, else scheduled when its trigger is not manual, else idle. reason is the wire-safe DisabledReason when state=disabled and `-` otherwise · test: Nyarlathotep.Tests ApiLinesTests (fails when: an active, disabled, scheduled or idle definition gets another state, a disabled row has reason=-, a trigger is unmapped, or a reason keeps a space, '=', ';' or ':')
- [ ] D3 · **Paging per contract §4** a paged read sends at most 10 rows, then `[NYAR:end] cmd=<cmd> page=<cur>/<total> count=<rows in all pages>`. No page means page 1. An empty result is `page=1/1 count=0`. A page past the last sends no rows and `page=<asked>/<total>`. A page below 1, not an integer, or outside the int range sends only `[NYAR:err] cmd=<cmd> code=badarg arg=page` · test: Nyarlathotep.Tests PagingTests (fails when: page "0", "-1", "x", "1.5" or "99999999999" is accepted, an empty set is not page=1/1 count=0, page 2 of 10 rows sends rows, page 2 of 11 rows sends other than the eleventh, or count counts only the page)
- [ ] D4 · **Wire key sets from the contract** WireFormatTests reads docs/RAPHAEL_INTEGRATION_CONTRACT.md (copied to the test output) and, for each of the tags event, def, end, err, ok and ev, asserts that the builder's keys equal the example line's keys of the contract section that documents the tag. Every built line passes the grammar check (at most 480 bytes; no '<', '>' or newline; values free of space, '=', ';' and ':'), including a row with a 32-character id and a 200-character name. The `[NYAR:version]` line reports api=2 · test: Nyarlathotep.Tests WireFormatTests (fails when: a builder adds, drops or reorders a documented key, a line exceeds 480 bytes, a value holds a forbidden character, or api is not 2)
- [ ] D5 · **Subscriptions end on disconnect** (Epic D45). `.nyar api sub on|off` (anyone) keys an in-memory set by the caller's SteamID from the VCF context; no argument names a player. It replies `[NYAR:ok] cmd=sub on=1` or `on=0`, and any other argument replies `[NYAR:err] cmd=sub code=badarg arg=state`. Running `sub on` twice leaves one entry. A disconnect removes the entry, and a new process starts empty. A push goes only to a subscriber who is connected at send time; a subscriber found offline is removed without a send. The log names counts only, never a SteamID or a name · test: Nyarlathotep.Tests SubscriptionTests (fails when: a push reaches an unsubscribed or disconnected id, an entry survives Disconnected(id), two `on` leave two entries, `sub maybe` is accepted, or a log line contains the id)
- [ ] D6 · **Push rules** each transition queues one `[NYAR:ev]` line with keys type, id and secs: event-start: secs = the event's duration; event-end: on expiry, stop or fault cancel; wave: with wave=<n>, when a wave is queued; wave-warn: secs = the time until the wave, plus wave=<n>. It is queued only while [Announcements] WaveWarnings is on and the definition's announce.warnings is true, once per WarningOffsets offset (S-1); killswitch: on a purge, with id=- and secs = the purge cooldown; config-changed: on a successful reload, enable, disable or set, with id=-; a second config-changed while one waits is not queued. The queue holds 50 lines; at 51 the oldest is dropped and "push queue full, oldest dropped" is logged once per overflow streak. Each tick sends at most 5 lines, each to every connected subscriber. No push line carries a coordinate · test: Nyarlathotep.Tests PushTests (fails when: wave-warn is queued with WaveWarnings off or announce.warnings false, an offset fires twice, a transition queues no line or two, two config-changed wait at once, the 51st line keeps the oldest, a tick sends 6 lines, or a line carries a coordinate key)
- [ ] D7 · **Subscribe through the gateway** ActionKind.Subscribe exists and is granted to Admin and Player only. The sub command runs it through Gateway.Run on ctx.User.PlatformId, and AuthorizationTests enumerates it with every actor (Epic D36's Player row for Subscribe) · cmd: dotnet test Nyarlathotep/Nyarlathotep.Tests --filter AuthorizationTests; then pwsh tools/preflight.ps1 → "Passed!" and line "gateway: only ActionGateway mutates (<n> call sites)" (fails when: Subscribe has no table row, System or Operator is granted it, or Pusher.Subscribe is called outside Gateway.Run)
- [ ] D8 · **Wire contract check** (Epic D38). Test-CheckWireContract collects every tag passed to Wire.Record or Wire.Line in Logic/ and every [Command] in a `[CommandGroup("nyar api")]` class. Each must appear in the contract's "Tags and commands" table marked IMPLEMENTED (api <n>), and Wire.Api must equal the contract's "**Current api:** <n>" · cmd: pwsh tools/preflight.ps1 → line "wire contract: <n> tags, <m> api commands, all documented (api 2)" (fails when: a tag or api command is missing from the table, a row is PLANNED, the two api numbers differ, or no tag is found at all)
- [ ] D9 · **Command walk sees each group** Get-CommandWalk gives each [Command] the nearest [CommandGroup] above it in its file, so `.nyar api events` is listed as that and never as `.nyar events` · cmd: pwsh tools/preflight.ps1 -ListCommands admin → a line ".nyar api events" (fails when: a file with two groups labels a command with the first group; the selftest fixture tools/preflight-fixtures/AdminList/bad-2 plants such a file)
- [ ] D10 · **Static checks pass** preflight prints "commands: <n> admin-only, <m> public (allow-listed)" with api status, version and sub public and api events admin-only; "ready guard: <n>/<n>" covering the three new commands; "patch guards: <n>/<n>" covering Patches/UserDisconnectPatch.cs; "secrets: none"; and PREFLIGHT OK · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK with each of those lines (fails when: a new command lacks adminOnly or the ready guard, or the disconnect patch lacks its guard)
- [ ] D11 · **New checks self-tested** tools/preflight-checks.json has good, bad and empty fixtures for Test-CheckWireContract: good: the real Wire.cs, ApiCommands.cs and contract; bad: a tag missing from the table; bad-2: a row still PLANNED; bad-3: Wire.Api 3 against contract 2; empty: no Logic files, which fails. AdminList gains bad-2, a two-group file · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, 3 fixtures each" (fails when: a fixture passes that should fail, or good fails)
- [ ] D12 · **Disconnect hook** Patches/UserDisconnectPatch.cs is a Prefix on ServerBootstrapSystem.OnUserDisconnected. It returns first when !Core.IsReady, reads the user as UserConnectPatch does, calls Pusher.Disconnected(platformId) inside try/catch, and logs a failure once per streak. Hook.UserDisconnect joins Logic/Hooks.cs, and the hook health line lists it · manual: in game, run `.nyar api sub on`, disconnect, then read BepInEx/LogOutput.log → "[nyar] push: subscriber left (0 subscribed)"; reconnect and trigger a push without `sub on` → no `[NYAR:ev]` line in chat
- [ ] D13 · **Reads and pushes in game** on the local server with one admin and the example event: `.nyar api version` shows api=2 plugin=0.3.0; `.nyar api status` shows the rows of D1 while the event runs and `count=0` after; `.nyar api events` and `.nyar api events 2` show the D2 rows and end lines; `.nyar api events x` shows badarg; `.nyar api sub on` then `.nyar event start` shows event-start and wave lines, then event-end, in chat; `.nyar purge confirm` shows killswitch; `.nyar event reload` shows config-changed; `.nyar api sub off` stops them · manual: the steps of docs/features/RAPHAEL_API.md › Test results › Session 2 performed by the owner at 127.0.0.1:9876; each observed line recorded
- [ ] D14 · **Contract at api 2** docs/RAPHAEL_INTEGRATION_CONTRACT.md: says "**Current api:** 2"; has a "Tags and commands" table listing version, event, def, end, err, ok and ev and the commands version, status, events and sub, each IMPLEMENTED (api 1 or 2); marks §3 status and events and the push section, and §4, IMPLEMENTED (api 2); states S-1's rule on wave-warn and S-3's on notready; leaves me, top and zones PLANNED, naming the child that fills each. docs/NYARLATHOTEP_DESIGN.md §6 lists the three new commands · file: docs/RAPHAEL_INTEGRATION_CONTRACT.md contains "**Current api:** 2" and "### `status` — active events (anyone) — IMPLEMENTED (api 2)"
- [ ] D15 · **Raphael handoff** docs/RAPHAEL_HANDOFF.md tells a session in the Raphael workspace, in the terms of contract §6 and §8, what to build against api 2: the parser and state files; the probe, then `sub on` after each handshake; the Events board, Admin › Events and Kill switch panels; the panels that wait for later api rows; how to file a §8 request. It is never copied into the Raphael workspace from here · file: docs/RAPHAEL_HANDOFF.md contains "## Build against api 2" and "## Not yet (later api)"
- [ ] D16 · **Rollback drill script** (Epic D32). tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1 does the following: It refuses while any VRisingServer process runs. It builds each tag's DLL in a disposable worktree. It saves the dev server's plugin DLL and BepInEx/config/Nyarlathotep/ and restores both in a finally. It installs N, edits one event's name in events.json, boots the dev world save-data-nyardev, and waits for "Nyarlathotep initialized". It stops the server, installs N-1, boots again, and checks for "Nyarlathotep initialized", the schema line of each file N wrote (stats.json is reported absent while no release writes it) and "marker sweep". It checks that v0.2.1 is an ancestor of v0.3.0 and that `git diff --name-only v0.2.1..v0.3.0` lists only tools/paths-manifest.txt paths. · cmd: pwsh tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1 → "rollback drill: pass" (fails when: a server process runs, N-1 does not initialize on N's state, a schema line is missing, the tags are not ancestor-ordered, the range touches an unlisted path, or the saved config differs from the restored one)
- [ ] D17 · **Release 0.3.0** csproj Version and thunderstore.toml versionNumber are 0.3.0; both changelogs and both READMEs describe the Raphael api 2 reads and pushes; the annotated tag v0.3.0 is pushed; and the GitHub pre-release carries the tcli zip (Epic D3, D20, D21). No tcli publish: the owner publishes (Epic A20) · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; gh release download v0.3.0 -p kdpen-Nyarlathotep-0.3.0.zip -D <scratch dir>; its SHA-256 equals the one recorded in docs/audits/raphael-api-core.md (fails when: a surface differs, the tag is missing or unpushed, the release has no zip, or the hashes differ)
- [ ] D18 · **Repository rollback drill** in a disposable worktree at v0.3.0, `git revert --no-edit <pre-child>..v0.3.0` (<pre-child> recorded in docs/audits/raphael-api-core.md at step 1) builds, passes preflight and leaves `git diff --quiet <pre-child>` true, run by the same worktree command form as foundation D38 · cmd: the foundation D38 command with v0.2.0 replaced by v0.3.0 → exit 0 and "rollback: clean" (fails when: the revert conflicts, the build or preflight fails, or the tree differs from <pre-child>)
- [ ] D19 · **Paths and data** every path this child writes is in tools/paths-manifest.txt, and tools/data-inventory.json needs no new entry because subscriptions and the push queue live in memory only · cmd: pwsh tools/preflight.ps1 -Paths → "paths: <n> walked, all in manifest" and preflight's "data inventory: <n> entries" (fails when: a new file such as tools/rollback-drill.ps1 or docs/RAPHAEL_HANDOFF.md is not in the manifest)
- [ ] D20 · **Sessions and records** every server session is an entry "### Session <n> · <date>" under docs/features/RAPHAEL_API.md › Test results, with a "- session <n> log check: 0 unhandled, <s> nyar lines, 0 orphan errors, <u> unity errors" line in docs/audits/raphael-api-core.md from `pwsh tools/preflight.ps1 -LogCheck` run before the next restart. The audit has a pre-audit and a post-audit with a "Codex verdict:" line for every Build plan step. tools/preflight-checks.json childDocs maps raphael-api-core to docs/features/RAPHAEL_API.md · cmd: pwsh tools/preflight.ps1 -AuditOf raphael-api-core → "audit steps: raphael-api-core 6/6 pre, 6/6 post, 6/6 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf raphael-api-core → "sessions: <n>/<n> checked" (fails when: a step lacks an entry or verdict, or a session lacks its log-check line)

## Purpose & typical use
Raphael, Lord of Wisdom, is the owner's client UI mod. Its players and admins want to see Nyarlathotep's events in a panel instead of typing commands. Today Raphael can only detect the mod through the api 1 handshake. With api 2 it can:
- list the running events with a countdown, for players;
- list and manage the event definitions, for admins (the human commands already exist; Raphael needs the machine-readable list);
- receive a line the moment an event starts, a wave comes or the kill switch fires.

A player never types these commands: Raphael sends them silently and hides the replies. The feature extends the foundation's `.nyar api version` (contract §2) and lives next to the human `.nyar status` and `.nyar event list`, which stay unchanged.

## Use cases
### Typical
Raphael logs in, probes `.nyar api version` until ready=1 and api≥2, then sends `.nyar api sub on` and `.nyar api status`. It draws the Events board and updates it from `[NYAR:ev]` lines. An admin opens Admin › Events, and Raphael pages `.nyar api events 1..n`. When the admin clicks Enable, Raphael sends `.nyar event enable <id>`, receives config-changed and re-reads `api events`.
### Minimal stretch
No events defined: `api events` → `[NYAR:end] cmd=events page=1/1 count=0` (D3); nothing running: `api status` → `[NYAR:end] cmd=status count=0` (D1). A player who never subscribes gets no push line (D5). A Raphael that is never installed changes nothing: every api command is opt-in, and the subscription set stays empty (8.2: nothing lingers past a disconnect or restart, D5).
### Maximal stretch
- **Volume:** 500 definitions are 50 pages of 10 (D3). A tick sends at most 5 push lines, each to every connected subscriber, so 100 subscribers get at most 500 system messages a tick (D6). Queued lines beyond 50 drop the oldest (D6).
- **Abuse:** a player spamming `.nyar api status` gets at most MaxConcurrentEvents plus ending rows, plus one line, per call (D1). Spamming `sub on` stays one entry (D5). A player cannot subscribe anyone else, because no argument names a player (D5, D7). A player cannot read definitions, because `api events` is adminOnly (D10).
- **Parallel:** two admins paging events see the same current set, since every call reads the current DefinitionSet (D2).

## Business rules
1. **Fairness (S-1):** a push never tells a subscriber more than chat or the public `.nyar status` tells every player. event-start, event-end, wave and killswitch are public state and always push. wave-warn pushes only when chat warnings would fire: WaveWarnings on, announce.warnings true, at WarningOffsets (D6).
2. **Privacy (Epic):** no wire line sent to a non-admin carries coordinates, a radius or another player's position or name; `units` is admin-only (D1, D6).
3. **Paging (contract §4):** 10 rows, 1-based, with the end line and error rules of D3. The contract is authoritative for key names and order; the tests read it (D4).
4. **Enabled off (S-2):** with General.Enabled=false, `api status` and `api events` still answer normally; `version` reports enabled=0. `disabled` stays reserved for stats and zones.
5. **Api number:** Wire.Api moves from 1 to 2 in the commit that adds the first api-2 line, together with the contract (D8, D14).
6. **Push ordering:** lines leave in queue order. A config-changed line that is already waiting absorbs a new one (D6). A wave-warn line whose event ended before it was sent is dropped when the event ends, like chat warnings.
7. **Precedence (4.4):** push volume yields to the queue caps (50 lines, 5 lines per tick), which yield to nothing: a dropped line is logged, never retried. A player's subscription never overrides the S-1 gates. The owner decides exceptions by changing the switches.

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:**
  - EventRuntime.Engine.Active and PendingCleanups (Logic/Engine.cs);
  - EventStore.Catalog.Current (Logic/Model.cs DefinitionSet);
  - SpawnTracker.Ledger.Tracked (per-event unit counts);
  - Settings (WaveWarnings, WarningOffsets, PurgeCooldownSeconds);
  - Persistence.State.Document.PurgeUntilUtc;
  - TextSink.WireValue.
- **Writes and changes:**
  - Logic/Wire.cs: Api=2 and new builders Event, Def, End, EndPaged, Ok, Ev.
  - New Logic/ApiLines.cs, Logic/Paging.cs, Logic/Subscriptions.cs and Logic/PushQueue.cs.
  - New Services/Pusher.cs (subscription set, push queue, the Tick send and a push WarningClock).
  - Services/EventRuntime.cs (StartEvent, End, Tick expiry and Purge call Pusher).
  - Services/WaveAction.cs (a wave queued → Pusher.Wave).
  - Services/EventStore.cs (a successful Reload or Edit → Pusher.ConfigChanged).
  - Services/EventScheduler.cs (a Pusher.Tick phase after the announcements phase).
  - Logic/ActionGateway.cs (+Subscribe) and Logic/Hooks.cs (+UserDisconnect).
  - Commands/ApiCommands.cs (new; version moves here from MessageCommands.cs) and Patches/UserDisconnectPatch.cs (new).
  - tools/preflight.ps1 (Get-CommandWalk group fix, Test-CheckWireContract), tools/preflight-checks.json and fixtures.
  - tools/rollback-drill.ps1 (new).
- **What breaks if this is wrong:**
  - If a push call throws inside EventRuntime, the event tick faults. So every Pusher entry point catches and logs once per streak (D6, Failure & observability).
  - A wrong walk group would mislabel the admin list of Epic D26 (D9).
- **Shared contract (5.3):** the `[NYAR:*]` lines. Each field is enumerated in D1, D2, D5 and D6 against the builder, and D4 compares the builder's keys with the contract's example lines.
### External — dependencies and their failure behaviour
- **VCF 0.10.4.** `[CommandGroup("nyar api")]` with string arguments, so a non-numeric page reaches our parser rather than VCF's type error (D3). VCF refuses adminOnly commands itself (contract §4 noaccess reserved; D10).
- **ServerBootstrapSystem.OnUserDisconnected** (the game). A Prefix, as in KindredCommands Patches/PlayerConnectivityPatches.cs and Bloodcraft Patches/ServerBootstrapSystemPatches.cs.
  - If the method is missing or the patch fails, Hook.UserDisconnect is reported unavailable, and a stale entry is still pruned on the next push, because the send reads the connected users (D5, D12).
- **ServerChatUtils.SendSystemMessageToClient.** A recipient that throws is skipped and logged once per streak, through the Broadcaster pattern of Logic/Hooks.cs (D5).
- **Raphael** (another workspace) is a consumer only. A Raphael older than api 2 ignores the new lines: unknown tags and keys are ignored (contract §1). A garbage command from it gets badarg (D3, D5).
- **Our own tools:** Codex (review) and git and gh (release, D17). If gh is down, the release waits and nothing is published half-way. The drill refuses to run beside a live server (D16).

## Design
### Data
Nothing new is persisted. The subscription set (SteamID → nothing) and the push queue (at most 50 strings) live in memory in Services/Pusher and are gone on restart (D5, D19). The documents written are the contract, the handoff, the feature doc docs/features/RAPHAEL_API.md, the audit docs/audits/raphael-api-core.md and the release surfaces; each exists once in git. The drill writes to the dev server only: the save-data-nyardev world, logs/NyarDev.log and BepInEx/LogOutput.log. The config it saves goes to a scratch folder deleted in its finally, and it never touches C:\VRising-LocalServer (D16). No migration: api 1 lines are unchanged.
### States
- **Subscription:** absent → on (`sub on`) → absent on `sub off`, a disconnect, being found offline at a push, or a restart (D5).
- **Push line:** queued → sent, or dropped on overflow (oldest first) or by its event's end (wave-warn only) (D6).
- **Read states:** before Core.IsReady every api command replies "still loading" (foundation D39 guard, S-3). Empty and partial results are covered in D1 and D3; an error is `[NYAR:err]`.
- **Concurrency:** all of it runs on the server main thread (commands, the scheduler tick and the Harmony prefix), so there are no locks. Two Raphael clients hold two entries. A disconnect racing a push: the push reads the connected users at send time (D5).
- **Stale data:** a paged read after a reload reads the new set, and Raphael re-reads on config-changed (D6).
### Permissions
| Command | Who | Check |
|---|---|---|
| `.nyar api version` | anyone | public allow-list (Epic D5) |
| `.nyar api status` | anyone; `units` only for admins | public; ctx.IsAdmin decides units (D1) |
| `.nyar api events [page]` | admin | adminOnly: true; VCF refuses others (D10) |
| `.nyar api sub on\|off` | anyone, own SteamID only | Gateway Subscribe on ctx.User.PlatformId (D7) |

The unauthorized path is VCF's standard refusal line; the mod never runs.
### UX
The surface is a machine wire read by Raphael. A human who types the commands sees the raw lines; that is expected and the contract says so (§1). Discovery: `.nyar` lists the api commands the caller may run (foundation D26 list). Feedback: every command answers with rows and an end line, an ok line or an err line; an empty result is count=0 (D1, D3). Activation (11.4): Raphael sends `sub on` after each handshake; nothing else subscribes a player. Pushes start only from the transitions of D6, never on a timer. Accessibility belongs to Raphael's panels; the wire carries no colour.

## Security
- **10.1 Authorization on every path:**
  - `api events` is adminOnly (D10).
  - `sub` changes only the caller's own entry, through the gateway (D7).
  - Pushes are an indirect path: they go only to connected subscribers (D5) and carry only public state (Business rules 1 and 2, D1, D6).
  - The drill is a local owner tool that runs no game command.
- **10.2 Injection:** every value passes TextSink.WireValue (D4). The page and state arguments are parsed strictly (D3, D5), so no input reaches a shell, a query or a file.
- **10.3 Secrets:** none are added. The preflight secrets check runs (D10). gh uses its own stored login, and no token is written (D17).
- **10.4 Personal data:** the SteamID is held in memory for the subscription only and is never logged (D5). Names appear on no api-2 line.

## Failure & observability
- **12.1:** a bad argument → `[NYAR:err]` naming the argument (D3, D5). An admin-only refusal → VCF's line. A hook failure → the hook health line and `.nyar status` degraded list (D12).
- **12.2:** the log has lines for:
  - a subscriber joining or leaving, with the count ("push: <n> subscribed");
  - a push queue overflow;
  - a send failure once per streak;
  - each drill stage.
- **12.3:** in production, the `.nyar status` degraded line and the BepInEx log show a broken hook. For a broken wire, Raphael's diagnostics (NyarDiag, contract §6) show lines that do not parse, and the owner files a §8 request.
- **12.4:** each new check has a failing input and a silent input, and prints something on an empty input:
  - Test-CheckWireContract: fixtures good, bad, bad-2 and bad-3; empty fails with "wire contract: no tags found" (D8, D11).
  - The AdminList bad-2 fixture (D9).
  - The drill prints "rollback drill: fail — <stage>" and never "pass" on a missing log line (D16).
  - Every test item names its fails-when.
- Every Pusher entry point wraps its work in try/catch, so a push failure never faults an event tick (D6).

## Performance
- **Hot path:** Pusher.Tick once per scheduler tick. With an empty queue it is one count check. Otherwise it sends at most 5 lines × subscribers (D6).
- **Reads:**
  - `api status` is O(active + cleanups + tracked units), and tracked units are at most 500.
  - `api events` sorts nothing new: DefinitionSet.All is already ordered.
- **Bounds:**
  - 10 rows per page;
  - 50 queued push lines;
  - 5 lines per tick;
  - 480 bytes per line.

  These come from the contract (paging, bytes) and from S-4 (queue). The valid case the queue bound excludes is a burst of more than 50 transitions between two ticks, which the log reports (D6).

## Build plan
Every step runs inside the Epic's `## Rollout` › Procedure: a pre-audit and a post-audit recorded in docs/audits/raphael-api-core.md, `/code-review`, and a Codex read-only cross-inspection of the step's diff (`codex exec -s read-only`, prompt via stdin) until "VERDICT: READY", with its line written to the audit. Compile check: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`. Tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests`.
1. **Records and pure logic.**
   - Create docs/audits/raphael-api-core.md (pre-child commit = the commit before step 1's first change) and docs/features/RAPHAEL_API.md with a `## Test results` section.
   - Map raphael-api-core to that feature doc in tools/preflight-checks.json childDocs.
   - Add Logic/Paging.cs (Parse(string?) → page or badarg; Page(rows, page) → rows plus the end tokens) and Logic/ApiLines.cs (StatusRows(active, cleanups, unitCounts, isAdmin, now); DefRows(set, activeIds)), built with Wire.
   - Extend Logic/Wire.cs with the Event, Def, End (unpaged), EndPaged, Ok and Ev builders; Api stays 1 until step 2.
   - Add tests ApiLinesTests and PagingTests, and the WireFormatTests key-set cases for the new tags. The event, def, end, err, ok and ev key sets are read from the contract, so the contract's example lines are made exact in this step while still marked PLANNED.
   - Satisfies D1, D2, D3, D4.
2. **Commands and contract.**
   - Move ApiCommands to Commands/ApiCommands.cs and add status, events [page] (adminOnly) and sub <on|off>, each starting with the ready guard. sub runs Gateway.Run(ActionKind.Subscribe, ctx.IsAdmin ? Actor.Admin : Actor.Player, …).
   - Add ActionKind.Subscribe to Logic/ActionGateway.cs and AuthorizationTests.
   - Set Wire.Api = 2.
   - Update docs/RAPHAEL_INTEGRATION_CONTRACT.md per D14, with a new "Tags and commands" table: | Tag or command | Kind | Status | Api |.
   - Add the three commands to docs/NYARLATHOTEP_DESIGN.md §6.
   - In tools/preflight.ps1, fix Get-CommandWalk (nearest preceding group) and add Test-CheckWireContract to the default run, with fixtures WireContract/{good,bad,bad-2,bad-3,empty} and AdminList/bad-2 registered in tools/preflight-checks.json.
   - Satisfies D4, D7, D8, D9, D10, D11, D14.
3. **Push.**
   - Add Logic/Subscriptions.cs (On, Off, Disconnected, and Deliver(IUserSource, text) that prunes offline entries and logs counts) and Logic/PushQueue.cs (capacity 50, drop-oldest with a streak log, config-changed collapse, DropWarnings(id), Take(5)).
   - Add Services/Pusher.cs: EventStarted, EventEnded, Wave, Purged, ConfigChanged, the WarningTick using Logic/AnnouncerCore WarningClock and UpcomingWave under the S-1 gates, and Tick. Every entry point is wrapped in try/catch.
   - Call it from EventRuntime (StartEvent, End, Tick expiry, Purge), WaveAction (after WaveSpawned), EventStore (Reload and Edit on success) and EventScheduler (a phase after the announcements).
   - Add Patches/UserDisconnectPatch.cs and Hook.UserDisconnect with its registration in TriggerBus.
   - Add tests SubscriptionTests and PushTests.
   - Satisfies D5, D6, D12.
4. **Unattended session.**
   - Stop the server and run `pwsh tools/preflight.ps1 -LogCheck` on the last logs. Deploy with `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release`.
   - Boot the dev world (`$env:SteamAppId='1604030'`; VRisingServer.exe -persistentDataPath .\save-data-nyardev -serverName "Nyar Dev" -saveName nyardev -logFile .\logs\NyarDev.log).
   - Run a scheduled event from tools/ingame/session-events.py (mode `a21`), and check the boot hook line lists UserDisconnect available and that the event ran and despawned cleanly.
   - Stop the server, run -LogCheck, and record Session 1.
   - Satisfies D20.
5. **In-game session with the owner.**
   - Write the exact numbered steps (server 127.0.0.1:9876) into docs/features/RAPHAEL_API.md › Test results › Session 2, boot, and hand the steps to the owner.
   - Record each observed line, stop the server, run -LogCheck, and record it.
   - Satisfies D12, D13, D20.
6. **Release and handoff.**
   - Write tools/rollback-drill.ps1 (D16) and docs/RAPHAEL_HANDOFF.md (D15), and add both to tools/paths-manifest.txt.
   - Run the D18 repository drill, then `pwsh tools/preflight.ps1` and -Paths.
   - Move the six surfaces to 0.3.0 in one `chore(release): v0.3.0` commit; tcli build; record the zip's SHA-256 in the audit.
   - Create the annotated tag v0.3.0, then run `pwsh tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1` with the server stopped.
   - Push the commit and tag, and create the GitHub pre-release with the zip. Grep for 7656119 and kdpenland before pushing.
   - Record the Epic pass lines (D38, D45; notes for D32, D36 and D37), then run `dod close raphael-api-core`.
   - Satisfies D15, D16, D17, D18, D19, D20.

## Work breakdown
- W1 · **Wire and reads**
- W1.1 · **Rows, paging and key sets** · items: D1 D2 D3 D4 · steps: 1
- W1.2 · **Commands and contract** · items: D7 D14 · steps: 2
- W2 · **Push**
- W2.1 · **Subscriptions and push queue** · items: D5 D6 D12 · steps: 3
- W3 · **Tooling**
- W3.1 · **Preflight checks** · items: D8 D9 D10 D11 · steps: 2
- W4 · **Verification and release**
- W4.1 · **Sessions and records** · items: D13 D20 · steps: 4, 5
- W4.2 · **Release, drills and handoff** · items: D15 D16 D17 D18 D19 · steps: 6

## Rollout
### Shipping
One release, 0.3.0, a GitHub pre-release at close. Nothing is switched on by it: the api commands answer only when asked, and no player is subscribed until their client sends `sub on`. Turning it off: an admin's pillar and General switches stop the events and so the pushes. `sub off` or a disconnect ends a subscription. The operator can remove the DLL. The owner publishes to Thunderstore when they choose (Epic A20).
### Compatibility
api 1 lines and `.nyar api version` keep every key, and only api changes from 1 to 2. Human commands and replies are unchanged. No config key or data file changes. A Raphael built for api 1 ignores the new tags (contract §1).
### Rollback
- **In the repository:** `git revert <pre-child>..v0.3.0`, drilled by D18.
- **On a server:** install the 0.2.1 DLL. Nothing was persisted by this child, so 0.2.1 runs on 0.3.0's files; tools/rollback-drill.ps1 proves it (D16). A Raphael then sees api=1 and hides the api-2 panels.
- **Commit range:** every commit of this child from <pre-child> to v0.3.0.
### Paths walked
Walking the Build plan:
- **Step 1:** docs/audits/raphael-api-core.md, docs/features/RAPHAEL_API.md, tools/preflight-checks.json, Nyarlathotep/Nyarlathotep/Logic/{Wire,Paging,ApiLines}.cs, Nyarlathotep/Nyarlathotep.Tests/{ApiLinesTests,PagingTests,WireFormatTests}.cs, docs/RAPHAEL_INTEGRATION_CONTRACT.md (the example lines) and the test output copy (ignored bin/).
- **Step 2:** Commands/ApiCommands.cs, Commands/MessageCommands.cs, Logic/ActionGateway.cs, Nyarlathotep.Tests/AuthorizationTests.cs, docs/RAPHAEL_INTEGRATION_CONTRACT.md, docs/NYARLATHOTEP_DESIGN.md, tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/WireContract/** and AdminList/bad-2/**.
- **Step 3:** Logic/{Subscriptions,PushQueue,Hooks}.cs, Services/{Pusher,EventRuntime,WaveAction,EventStore,EventScheduler,TriggerBus}.cs, Patches/UserDisconnectPatch.cs, Nyarlathotep.Tests/{SubscriptionTests,PushTests,DependencyFailureTests}.cs.
- **Steps 4–5:**
  - On the server: the plugins DLL, BepInEx/config/Nyarlathotep/{events,state}.json (+.bak/.tmp), save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log.
  - In the repository: docs/features/RAPHAEL_API.md and the audit.
- **Step 6:** tools/rollback-drill.ps1, docs/RAPHAEL_HANDOFF.md, tools/paths-manifest.txt, the six release surfaces, Nyarlathotep/Nyarlathotep/dist/** and build/*.zip (ignored), the drill's temp worktrees and scratch config copy (outside the repository, deleted in finally), docs/dod/raphael-api-core.md, docs/dod/nyarlathotep.md and docs/dod/README.md.
- **Review process:** docs/dod/raphael-api-core.reviews.md and docs/dod/raphael-api-core.review.html.

## Out of scope
- 15.1: excluded here:
  - `api me` and `api top`: they belong to the stats child;
  - `api zones`: the defended-zones child;
  - siege rows and siege-only push routing: the sieges child;
  - empower rows (kind=empower, faction): the faction-empowerment child.
  - Each adds its rows under the Epic's raphael-api-core constraint (A20).
- Building Raphael's panels: the Raphael workspace is never edited from here. The handoff (D15) is the input for a session there.
- Thunderstore publication (the owner's).
- 15.2: deferred:
  - the both-mods in-game check to the pause after this child (Epic A20);
  - `notready` emission to never, while no player can connect before ready (S-3).

## Also considered
- **Compliance and legal:** only SteamIDs, held in memory and never logged or stored; nothing else applies.
- **Localisation:** the wire is not localised by design (contract §1); times are seconds.
- **Running cost:** none.
- **Operational ownership:** the owner is the operator.
- **Documentation and changelog:** the contract, the handoff and the six surfaces (D14, D15, D17).
- **Analytics:** none; a subscriber count in the log is enough.
- **Decommissioning:** nothing is replaced; api 1 stays.
- **Support tooling:** Raphael's NyarDiag, and the raw lines when a human types the commands.

## Assumptions
- S-1 · validated · a push never tells a subscriber more than chat or `.nyar status` tells every player; wave-warn pushes only when WaveWarnings is on and announce.warnings is true, at WarningOffsets; the other push types always go · source: owner decision in plan mode 2026-09-25 (Decision 1A)
- S-2 · validated · with General.Enabled=false, api status and api events answer normally; `disabled` is reserved for stats and zones · source: owner decision in plan mode 2026-09-25 (Decision 2A)
- S-3 · reversible · before Core.IsReady the api commands reply "still loading" like every command (foundation D39); `notready` stays documented but is not sent in api 2, since no player can connect before the world is ready (foundation session 1) · fallback: have the guarded commands send `[NYAR:err] code=notready` instead, a one-line change per command and a preflight ready-guard update
- S-4 · reversible · the push queue holds 50 lines and sends 5 per tick, outside the one-per-second chat queue, with config-changed collapsed · fallback: the two numbers are constants in Logic/PushQueue.cs; a cfg key can be added later
- S-5 · reversible · ending rows (units waiting out the grace) appear in `api status` with state=ending; `scheduled` is never sent by status in api 2 · fallback: drop the ending rows; Raphael ignores rows it does not expect
- S-6 · validated · every api command runs on the server main thread (VCF commands, the scheduler tick, Harmony prefixes), so Pusher needs no locks · source: foundation Design › States (main-thread serialisation), Services/EventScheduler.cs
- S-7 · reversible · the in-game session (D13) is done by the owner typing the commands, without Raphael, because Raphael's panels are built after this child · fallback: repeat D13 through Raphael in the both-mods check

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D7 D10 D5; 2.2 D10 D3 D5; 2.3 D5 D7 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D3 D5; 3.2 D1 D2 D6 D4; 3.3 D19 D5 D16; 3.4 prose: nothing persisted and api 1 lines unchanged, so nothing migrates |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D3 D6; 4.2 D1 D5 D4; 4.3 D6 D1; 4.4 D6 D5; 4.5 D8 D9 D1 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D1 D2; 5.2 D6 D9; 5.3 D4 D14 D8 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D3 D12 D14; 6.2 D5 D12 D16 D17; 6.3 D16 D11 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D1 D3; 7.2 D5 D2; 7.3 D6 D5 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D1 D3 D5; 8.2 D5 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D3 D6; 9.2 D5 D7 D10; 9.3 D5 D2 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D10 D7 D5 D1; 10.2 D4 D3; 10.3 D10; 10.4 D5 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D10 D14; 11.2 D1 D3 D13; 11.3 prose: panels and contrast belong to Raphael; the wire carries no colour; 11.4 D5 D6 D13 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D3 D12; 12.2 D5 D6 D20; 12.3 D12 D20; 12.4 D11 D8 D16 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D6; 13.2 D3 D6 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D17; 14.2 D4 D14; 14.3 D18 D16; 14.4 D19 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline

## Amendments

## Log
- 2026-09-25 · status → draft · plan
