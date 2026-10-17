---
dod: 2
rubric: 3
profile: full
id: dod-20261017-lgn1
slug: login-notice
title: Login notice — a player who logs in during a running event is told what is running
status: in-progress
size: M
parent: nyarlathotep
kind: feature
created: 2026-10-17
baselined: 2026-10-17
closed: none
recon_commit: f80be11
coverage_author: 15/15 layers · 50/50 probes
coverage_reviewer: 15/15 layers · 50/50 probes
review: subagent
---

# DoD: Login notice — a player who logs in during a running event is told what is running

**Size:** M. One deliverable feature with a new player-facing chat line and a new cfg key (`[Announcements] LoginEventNotice`), touching Logic/AnnouncerCore.cs and Logic/Messages.cs (the rule and the text), Services/Announcer.cs (the delivery), Config/Settings.cs, tools/preflight.ps1 (the announcement-defaults check) and the release surfaces. No schema change in events.json or state.json, no new command, no wire change.
**Planned:** interactively; the owner asked for it on 2026-10-17 (design §9 D63, Epic A45). The Halloween track's process applies (design §9 D34): one review round, two at most; Codex once on the child's diff before release; a vrclient scenario as the in-game evidence.
**Request:** the owner, 2026-10-17: "by default, if a player joins Or logs online to a server with an active event going (not just an enabled event, but an active event), they want the player to be notified that there is an active event in progress so that they're aware."

**North Star:** every player who logs in while an event is running gets one private line naming the running events and their time left, on by default and switchable off in the cfg, shipped as 0.13.1 during the players' test weekend; finished when D1-D12 have evidence.

## Definition of Done
- [x] D1 · **Notice text** new `Messages.LoginNotice(running, utcNow, own)` returns null when no instance runs, else one line "Events in progress: <name> (<n> min left)[; <name> (<n> min left) …]. Type .nyar status for details." with the instances ordered by end time, each part the text `.nyar status` shows for it (an apocalypse's own status line through `own`, Messages.Status's "<n> min left" otherwise), cut to Wire.MaxBytes (512) by whole parts and ending "… and <m> more" when parts are left out; it holds no coordinate, zone radius, player name or platform id · test: Nyarlathotep.Tests LoginNoticeTests Text (fails when: a line is returned with nothing running, a part is out of end-time order, a line passes 512 bytes or cuts a part in half, the "… and <m> more" count is wrong, or a part differs from the status line of the same instance)
- [ ] D2 · **Who gets it, when** new `LoginNotices` in Logic/AnnouncerCore.cs with an injected clock: every user who connects, admin or not, while LoginEventNotice is on is queued once; the notice leaves 10 s after the connect (NoticeDelay) and its running list is read when it leaves, so a user is sent nothing when no event runs at that moment, when they disconnected or another player holds their user entity (platform id changed), or when they connected again within 60 s of their previous connect (its own LoginGate, separate from the admin degraded notice's, so a degraded admin still gets that notice too); an "event" is an instance in EventStore.Catalog.Running (started and not ended), never a definition that is only enabled · test: Nyarlathotep.Tests LoginNoticeTests Delivery (fails when: a notice leaves before 10 s, is sent with nothing running at its due time, reaches a disconnected or replaced user, is sent twice within 60 s of reconnects, is sent for an enabled event that is not running, skips a non-admin, or stops the admin degraded notice)
- [x] D3 · **On by default, switchable** Config/Settings.cs binds `[Announcements] LoginEventNotice`, default true, described "Private line to a player who logs in while an event runs, naming the running events and their time left."; false sends no login notice; the boot line "[nyar] announcements: …" lists LoginEventNotice when on; tools/preflight.ps1 Test-CheckAnnouncementDefaults binds six switches and passes only when LoginEventNotice defaults to true and every other [Announcements] bool to false (design §9 D63's exception to S-24) · cmd: pwsh tools/preflight.ps1 → "announcement defaults: all off but LoginEventNotice (6 switches)" (fails when: LoginEventNotice is missing or false, or another announcement switch defaults to true)
- [ ] D4 · **Send failures contained** a private send that throws is counted by the announcer's private-message streak: one "[nyar] private message failed: <message>" line per streak, the other due notices of the same tick still sent, the scheduler tick not failed; a throwing running-list read leaves that notice unsent with one "[nyar] login notice failed: <message>" line per streak · test: Nyarlathotep.Tests LoginNoticeTests Faults (fails when: a throwing send stops the other notices, logs more than once in a streak, or escapes the tick, or a throwing read sends a notice)
- [x] D5 · **Wire and privacy unchanged** Wire.Api stays at its value and `.nyar api version` keeps its fields (the switch is not on the wire in api 8); docs/RAPHAEL_INTEGRATION_CONTRACT.md says LoginEventNotice is a server cfg switch Raphael neither reads nor sets in api 8; the notice and its log line "[nyar] login notice sent: <k> events" carry no player name, platform id or position (PrivacyTests enumerates both) · test: Nyarlathotep.Tests ContractDocTests LoginNotice and PrivacyTests LoginNotice (fails when: Wire.Api or the handshake changes, the contract lacks the sentence, or the notice or its log line carries a name, id or position)
- [x] D6 · **Controls registered** login-notice joins ControlCases.Plans with a row for every test and cmd control of this plan, a later step's control in ControlCases.Pending until its step's post-audit; LoginNoticeTests joins ControlCases.NewClasses · test: Nyarlathotep.Tests ControlCaseTests (fails when: a test or cmd item of this plan has no row, or a pending row outlives its step's post-audit)
- [ ] D7 · **Notice in game** tools/vrclient/scenarios/login-notice.vrs on the dev server: with `console TPHome` first, an event started by `.nyar event start` (a Manual spawns event copied from a template, so nothing depends on the clock), then `leave` and `ensure` to log in again after 61 s: the log shows "login notice sent: 1 events" within 30 s and the chat shows "Events in progress" (expect-chat); after `.nyar event stop`, a further `leave`, 61 s and `ensure`: no "login notice sent" line within 30 s · cmd: python tools/vrclient/vrclient.py run scenarios/login-notice.vrs → "SCENARIO PASS <n>/<n>" (fails when: the notice is missing with an event running, does not render in chat, or is sent with nothing running)
- [ ] D8 · **Rollback drilled** a server that installs 0.13.0 again on 0.13.1's files boots clean: BepInEx keeps the unknown LoginEventNotice entry unread, events.json and state.json are untouched by this child, and Rollout › Rollback › On a server says the step stays possible after data is written · cmd: pwsh tools/rollback-gate.ps1 -From v0.13.0 -To v0.13.1 -Plan login-notice → "rollback gate: 4/4" (fails when: the 0.13.0 boot logs an unhandled error, an orphan error or a Unity error, or the route text is missing)
- [ ] D9 · **Release 0.13.1** the six surfaces move to 0.13.1 in one `chore(release): v0.13.1` commit: both changelogs describe the login notice and its switch, both READMEs' cfg tables list `LoginEventNotice` (default `true`) and the announcement text says it is the one switch on by default; docs/features/LOGIN_NOTICE.md describes it with its Status; an annotated tag v0.13.1 and a GitHub pre-release with the tcli zip · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>" (fails when: a surface lacks the entry, the versions differ, or the tag is not pushed)
- [ ] D10 · **Sessions, records, paths** every server session is "### Session <n> · <date>" under docs/features/LOGIN_NOTICE.md › Test results with its log-check line in docs/audits/login-notice.md before the next restart and every [Error and [Warning line read; the audit has a pre-audit and a post-audit per Build plan step and the child's one "Codex verdict:" line; every path this child writes is in tools/paths-manifest.txt and declared by Rollout › Paths walked · cmd: pwsh tools/preflight.ps1 -AuditOf login-notice → "audit steps: login-notice 3/3 pre, 3/3 post, …"; pwsh tools/preflight.ps1 -SessionsOf login-notice → "session logs: login-notice <n>/<n> checked …"; pwsh tools/preflight.ps1 -Paths -DeclaredOf login-notice → "paths: <n> walked, all in manifest; declared: <k>/<k> in login-notice" (fails when: a step lacks an audit entry, a session lacks its log check, or a written path is undeclared)
- [ ] D11 · **Secrets and privacy** this child adds no credential; no tracked file or build log holds a token or the sentinel, and no tracked line holds a platform-id digit run or the owner's mail name except lines quoting the pattern · cmd: pwsh tools/preflight.ps1 → "secrets: none (…)"; then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern itself (fails when: a token, the sentinel, a platform id or the mail name appears in a scanned file)
- [x] D12 · **Feature doc** docs/features/LOGIN_NOTICE.md exists with Status, Goal, Design summary, Open questions and `## Test results`, and tools/preflight-checks.json childDocs maps login-notice to it · file: docs/features/LOGIN_NOTICE.md contains "## Test results"

## Components
- C1 · **The notice** one private line at login naming every running event and its time left, as `.nyar status` shows them · D1 D2 D7
- C2 · **Admin control** on by default, off with one cfg switch, its default guarded by preflight · D3
- C3 · **Safe to ship** contained failures, wire and privacy unchanged, registered controls, a drilled rollback, a recorded release · D4 D5 D6 D8 D9 D10 D11 D12

## Requirements
- R1 · **Told on login** "if a player joins Or logs online to a server with an active event going ... they want the player to be notified that there is an active event in progress" · source: request · D1 D2 D7
- R2 · **Running, not enabled** "not just an enabled event, but an active event" · source: request · D2
- R3 · **By default** "by default" · source: request · D3
- R4 · **Raphael shows it** a Raphael panel line for the notice switch · source: request · out of scope: the request names chat only; the switch stays off the wire in api 8 (D5) and goes to the live-ops child if asked

## Purpose & typical use
A player logs in at 21:10 while the scheduled Legion surge and the zombie apocalypse are running. Before 0.13.1 nothing told them: the banners went out when the events started, while they were offline. With the notice (C1) they see, ten seconds after they arrive, "Events in progress: Legion weekend surge (25 min left); Zombie apocalypse: horde 2 of 4 risen … Type .nyar status for details." An admin who does not want it sets `LoginEventNotice = false` (C2). 1.1: every player, at every login during an event; 1.2: they know at once that something is on; 1.3: it extends the announcer's login hook, which today sends only the admin degraded notice, and coexists with the event banners.

## Use cases
### Typical
The scheduler starts `legion-weekend-surge` at 21:00 (banners on). A player logs in at 21:10, loads in, and 10 s after connecting gets the private line. They type `.nyar status` for the same list. A friend of theirs disconnects and comes back within a minute after a crash: no second line.

### Minimal stretch
- 8.1 No event running: nothing is sent and nothing is logged; one event running: a single part; the switch at its default (true) with no other setup (D1, D2, D3).
- 8.2 Used once: the queue holds a connect for 10 s and the gate a platform id for 60 s, both in memory; nothing is written to disk (D2).

### Maximal stretch
- 9.1 Volume: 40 players connecting in one minute queue 40 notices, each one private send when due; ten running events (MaxConcurrentEvents' ceiling) fit one 512-byte line or end "… and <m> more" (D1, D2).
- 9.2 Abuse: a player reconnecting in a loop gets at most one line a minute (D2); the line names only what `.nyar status` already shows any player (D1, D5).
- 9.3 Repeated use: a reconnect within 60 s is silent; two connects of one platform id in one tick queue one notice (D2).

## Business rules
1. **What counts** (D2): an instance in EventStore.Catalog.Running, of any pillar and kind, the same set `.nyar status` lists; an enabled definition that is not running never counts.
2. **When** (D2): 10 s after the connect, the list read then; nothing when the list is empty then.
3. **How often** (D2): once per connect, never again within 60 s of the same platform id's previous connect.
4. **Precedence** (4.4): LoginEventNotice false beats everything (nothing sent); General.Enabled false leaves no event running, so nothing is sent; a purge ends every event, so a notice due after it sends nothing; the admin degraded notice is independent of it (both may reach an admin).
5. **"Every" sets** (4.5): every user who connects reaches Announcer.UserConnected through the one login hook (Patches/UserConnectPatch, the only caller, found by grep); every running instance is read through EventStore.Catalog.Running (the list `.nyar status` uses); every end path of an event (expiry, stop, purge, pillar off, fault, restart) removes the instance from that list before the next tick, so a notice due after it no longer names it.
- whole: none — the notice lists running events but no parts add up to a total anyone checks
- siblings: none — one notice to one audience; the admin degraded notice is a separate, unchanged line

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads (5.1):** EventStore.Catalog.Running and Services.ApocalypseAction.StatusLine (as StatusCommands does), the User component of the connecting user entity, Settings.LoginEventNotice.
- **Writes or changes (5.2):** Logic/Messages.cs (LoginNotice), Logic/AnnouncerCore.cs (LoginNotices, a second LoginGate), Services/Announcer.cs (UserConnected queues the notice; the announcements phase sends due notices), Config/Settings.cs (the switch), tools/preflight.ps1 and its AnnouncementDefaults fixtures. A mistake here can only spam or miss a private line; it touches no unit, buff or file.
- **Contracts (5.3):** the cfg key; the chat line; the log line "[nyar] login notice sent: <k> events"; the wire unchanged (D5). Functions the platform calls: ServerBootstrapSystem.OnUserConnected through the existing postfix (unchanged), the scheduler's announcements phase (one more call).

### External — dependencies and their failure behaviour
- **6.1 The game:** OnUserConnected (the existing hook, proven by the admin degraded notice since foundation D31) and ServerChatUtils.SendSystemMessageToClient (proven by every private line since revenge D4). A connect whose approved-user lookup throws is the hook's existing streak.
- **6.2 Failure:** a throwing send or running-list read (D4); a stale user entity at send time is skipped (D2); the hook's own failure path is unchanged.
- **6.3 Test mode:** none new; vrclient's `leave` and `ensure` log Chaos out and in again (D7).

## Design
### Data
| Artifact | Where | Owner | Kept | Deleted |
|---|---|---|---|---|
| the LoginEventNotice switch | BepInEx/config/kdpen.Nyarlathotep.cfg | the operator | until edited | by the operator; 0.13.0 ignores it (D8) |
| queued notices | memory, Logic/AnnouncerCore.cs | the announcer | 10 s per connect | when sent or skipped |
| the login gate | memory | the announcer | 60 s per platform id | dropped on each call past 60 s |
| the log line | BepInEx/LogOutput.log | the server | the log's rotation | with the log |
| session log copies | %TEMP%\nyar-ln-logs | Claude | the session | deleted in the same session (D10) |

Nothing is persisted by the mod. Migration (3.4): none; a cfg without the key gets it at its default on the first boot of 0.13.1.

### States
- **7.1** Empty: nothing running sends nothing; first run: the key is written at its default; error: D4's streak lines.
- **7.2** Concurrent use: many connects in one tick each queue their own notice; one platform id twice in a tick queues once (the gate); the announcements phase is the only sender.
- **7.3** Cancel and re-entry: an event that ends during the 10 s wait is not named (the list is read at send time); a server restart drops the queue (the players reconnect and are queued again); a cfg change applies at the next boot, as every BepInEx key does.

### Permissions
- **2.1** Players and admins: receive the notice; no command is added. The operator: the cfg switch. Unauthenticated users: none (a user reaches OnUserConnected only once approved by the game).
- **2.2** There is no unauthorized path: the notice is a private line to its own recipient and names only what `.nyar status` shows any player.
- **2.3** Ownership: a notice belongs to the connecting user's platform id; another player taking over the user entity before it is due gets nothing (D2).

### UX
- **11.1** Discovery: it arrives by itself at login; the READMEs' cfg tables and changelogs name it (D9).
- **11.2** Feedback: one line in the game's system-message style, rendered in the real chat by D7; the empty result is silence.
- **11.3** Accessibility: plain text within 512 bytes, no colour or rich text, no "< >" (the chat eats them).
- **11.4** Activation: a login while an event runs and the switch is on; it never fires on a respawn, a region change or an event start (the banners cover starts).
- **11.5** Design bar: no design bar beyond the project's plain chat style — the owner asked for awareness, not a look; D7 proves the line renders.

## Security
- **10.1** Authorization: no command, wire command or api twin is added; the switch is the operator's cfg file; the line goes only to its recipient (D2, D5).
- **10.2** Injection: event names come from validated definitions (names are checked at load, foundation D29) and the line is cut to 512 bytes (D1); nothing reaches a shell or a query.
- **10.3** Secrets: none added (D11).
- **10.4** Personal data: the platform id is held in memory for 60 s for the gate and never logged or stored; the notice and its log line carry no name, id or position (D5).

## Failure & observability
- **12.1** What the player sees on a failure: nothing (a missed private line); what the admin sees: the streak line in the log (D4). What they do next: nothing is needed; `.nyar status` still answers.
- **12.2** Logged: "[nyar] login notice sent: <k> events" per notice (D5), "[nyar] private message failed: <message>" and "[nyar] login notice failed: <message>" once per streak (D4).
- **12.3** Knowing it is broken: the notice lines in the log after a login during an event; -LogCheck after every session (D10); the vrclient scenario (D7).
- **12.4** Failing cases: every test and cmd item names its fails-when and has a ControlCases row (D6); the preflight check's bad fixtures plant a missing switch, LoginEventNotice false and another switch true (D3); each new test's planted fault is run in its step and logged; the dry runs are in the Log.

## Performance
- **13.1** Budget: the existing tick budget (5 ms average at the default caps, automation D21). The added work is one list read and one private send per due notice, in the announcements phase that already sends one queued line a second; recon baseline: Debug.TimingLog's announcements phase is part of "outside phases 0 ms" in the G-Portal boot of 0.13.0 ("slow tick: 313 ms (events 265 ms, watch 48 ms; outside phases 0 ms)", 2026-10-17).
- **13.2** Bounds: one notice per connect, the gate's 60 s, the line's 512 bytes; at most MaxConcurrentEvents (1-10) parts.

## Build plan
1. **Logic: the text, the delivery rule, the switch check.** · needs: Logic/Messages.cs Status (exists), Logic/AnnouncerCore.cs LoginGate (exists), tools/preflight.ps1 Test-CheckAnnouncementDefaults (exists) · gives: the notice rule, the switch · advances C1 C2 C3 · satisfies D1, D2, D3, D4, D5, D6, D12
   - Pre-audit per CLAUDE.md into docs/audits/login-notice.md (template docs/audits/README.md), `git status` clean; create docs/features/LOGIN_NOTICE.md with Status, Goal, Design summary, Open questions and `## Test results`; tools/preflight-checks.json childDocs.
   - Logic/Messages.cs LoginNotice (D1); Logic/AnnouncerCore.cs LoginNotices with an injected clock, a send delegate and a list reader (D2, D4); Config/Settings.cs LoginEventNotice (D3); the announcements boot line.
   - tools/preflight.ps1 Test-CheckAnnouncementDefaults with the six switches and the one true default; fixtures good (Settings.cs copy), bad (another switch true), bad-2 (a switch missing), bad-3 (LoginEventNotice false); tools/preflight-checks.json plant texts.
   - Tests: new LoginNoticeTests (Text, Delivery, Faults); ContractDocTests LoginNotice and PrivacyTests LoginNotice; the contract sentence in docs/RAPHAEL_INTEGRATION_CONTRACT.md; ControlCases rows, Pending for steps 2-3; Nyarlathotep.Tests.csproj copies docs/dod/login-notice.md and docs/audits/login-notice.md.
   - Each new test's planted fault is run once and logged `note · planted · D<n> · …`; `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`, `dotnet test`, `pwsh tools/preflight.ps1 -SelfTest`, post-audit.
2. **Services and the session in game.** · needs: the notice rule, the switch · gives: the notice in game · advances C1 C3 · satisfies D2, D4, D7
   - Services/Announcer.cs: UserConnected queues every connect while the switch is on; the announcements phase sends due notices through SendPrivate and logs D5's line.
   - tools/vrclient/scenarios/login-notice.vrs (D7); Session 1 on a Release build: snapshot before and restore after (`pwsh tools/dev-snapshot.ps1 -Save ln1`, `-Restore`), both logs copied to %TEMP%\nyar-ln-logs before each stop, -LogCheck before the next restart, every [Error and [Warning read; results under docs/features/LOGIN_NOTICE.md › Test results.
3. **Release 0.13.1.** · needs: the notice in game · advances C3 · satisfies D8, D9, D10, D11
   - The single Codex cross-inspection of the child's diff (`codex exec -s read-only -c features.experimental_windows_sandbox=true`, prompt by stdin) and /code-review; verdict lines in step 3's post-audit.
   - The six surfaces and the feature doc's Status (D9) in one `chore(release): v0.13.1` commit; `pwsh tools/rollback-gate.ps1 -From v0.13.0 -To v0.13.1 -Plan login-notice` (D8); tcli build with the sentinel token, release-verify, tag, push, GitHub pre-release; -AuditOf, -SessionsOf and -Paths -DeclaredOf login-notice (D10); the privacy grep (D11); `dod close login-notice`. The push and the release wait for the owner's OK.

## Work breakdown
- W1 · **Login notice**
- W1.1 · **Text, rule, switch, controls** · items: D1 D2 D3 D4 D5 D6 D12 · steps: 1, 2
- W1.2 · **In game** · items: D7 · steps: 2
- W2 · **Release**
- W2.1 · **Drill, surfaces, records** · items: D8 D9 D10 D11 · steps: 3

## Rollout
### Shipping
In 0.13.1 (a GitHub pre-release; the owner publishes to Thunderstore and updates the G-Portal server). It ships on: the owner asked for it by default (design §9 D63). Who turns it off: the operator, with `LoginEventNotice = false` in kdpen.Nyarlathotep.cfg and a restart; any admin can also stop the events it names (`.nyar event stop`, `.nyar purge confirm`).

### Compatibility
- events.json and state.json unchanged; the wire unchanged (D5); 0.13.0 cfg files gain the key at its default.
- Raphael api 8 clients see no change.

### Rollback
- **In the repository:** `git revert --no-edit v0.13.0..v0.13.1` (the child's commits, listed by `git log v0.13.0..v0.13.1`), tested by the gate's repository drill (D8).
- **On the dev server during the build:** every session wrapped by tools/dev-snapshot.ps1 (D10).
- **On a server:** install the 0.13.0 DLL; it ignores the LoginEventNotice key and reads every other file as before, so the step stays possible after data is written, after any amount of use (D8).
- **Published release:** tags and releases are never deleted; a bad one is withdrawn by retitling it (Epic S-19).
- **Commit range:** v0.13.0..v0.13.1.

### Paths walked
- **Steps 1-3:** docs/audits/login-notice.md, docs/features/LOGIN_NOTICE.md, docs/dod/login-notice.md, docs/dod/login-notice.build.md, docs/dod/README.md, docs/dod/nyarlathotep.md, docs/NYARLATHOTEP_DESIGN.md (§9), docs/RAPHAEL_INTEGRATION_CONTRACT.md, docs/SESSION_HANDOFF.md.
- **Step 1:** Nyarlathotep/Nyarlathotep/Logic/{Messages,AnnouncerCore}.cs, Nyarlathotep/Nyarlathotep/Config/Settings.cs; Nyarlathotep/Nyarlathotep.Tests/** (LoginNoticeTests.cs, ContractDocTests.cs, PrivacyTests.cs, ControlCases.cs, Nyarlathotep.Tests.csproj); tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/AnnouncementDefaults/**, tools/paths-manifest.txt.
- **Step 2:** Nyarlathotep/Nyarlathotep/Services/Announcer.cs, tools/vrclient/scenarios/login-notice.vrs; the dev server's BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events.json,events.json.bak,state.json}, BepInEx/LogOutput.log, logs/NyarDev.log, save-data-nyardev/**; %TEMP%\vrclient\**, %TEMP%\nyar-ln-logs, %TEMP%\nyar-snap-*.
- **Step 3:** the six surfaces CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/{CHANGELOG.md,README.md,Nyarlathotep.csproj,thunderstore.toml} and CLAUDE.md's status line; git-ignored Nyarlathotep/Nyarlathotep/build/**, dist/**, obj/**, bin/**; the tools' scratch folders %TEMP%\nyar-logs-*, %TEMP%\nyar-rel-*, %TEMP%\nyar-rollback-*, %TEMP%\nyar-secrets-index-*, %TEMP%\nyar-selftest-*, %TEMP%\nyar-session, %TEMP%\nyar-drill-*; remote-tag v0.13.1, remote-release v0.13.1.
- **Review process:** docs/dod/login-notice.reviews.md, docs/dod/login-notice.review.html, docs/dod/dod-dashboard.html, %TEMP%\dod-review-*.txt.

## Out of scope
- **Considered and excluded (15.1):**
  - Telling a player about events scheduled later today: the daily banner does that (DailyBanner).
  - A notice on respawn or on entering a region: the request names logins only.
  - A per-event opt-out of the notice: one server switch was asked for; every running event is named.
  - A Raphael panel line or wire field for the switch (R4).
- **Deferred (15.2):**
  - Private login stats (the reserved LoginStats switch): child stats.

## Also considered
- **Compliance:** none; nothing about a player is stored.
- **Localisation:** English only, like every stock line.
- **Running cost:** one private send per login during an event.
- **Operational ownership:** the operator's cfg switch.
- **Documentation and changelog:** the six surfaces (D9), LOGIN_NOTICE.md (D12).
- **Support tooling:** `.nyar status` shows the same list; the log line per notice.

## Assumptions
- S-1 · validated · A player logging in during a running (not merely enabled) event gets a notice, on by default · source: design §9 D63 (owner, 2026-10-17)
- S-2 · validated · Process: one review round (two at most), Codex once on the child's diff, a vrclient scenario as in-game evidence · source: design §9 D34; CLAUDE.md › Development procedure
- S-3 · reversible · The notice waits 10 s after the connect, the admin degraded notice's delay, so it reaches a client that has loaded · fallback: a longer constant in Logic/AnnouncerCore.cs; one test value changes
- S-4 · reversible · A reconnect within 60 s is silent, as the admin notice's gate · fallback: a different window constant; one test value changes
- S-5 · reversible · The child ships as 0.13.1, before undead-hordes (0.14.0) · fallback: it ships inside 0.14.0; the six surfaces move one version

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D2 D5; 2.2 D5 D1; 2.3 D2 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D3 D2; 3.2 D1 D5; 3.3 D2 D10; 3.4 D3 D8 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D2; 4.2 D2 D1; 4.3 D2; 4.4 D2 D3; 4.5 D2 D1 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D1 D2; 5.2 D2 D3; 5.3 D5 D3 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D7; 6.2 D4 D2; 6.3 D7 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D2 D4; 7.2 D2; 7.3 D2 D7 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D1 D2; 8.2 D2 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D1 D2; 9.2 D2 D5; 9.3 D2 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D5 D2; 10.2 D1; 10.3 D11; 10.4 D5 |
| 11 | Design & UX | Considered | 5/5 | Design › UX › 11.1 D9; 11.2 D7 D1; 11.3 D1; 11.4 D2 D7; 11.5 prose: no design bar beyond the plain chat style; awareness was asked, not a look |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D4; 12.2 D5 D4; 12.3 D7 D10; 12.4 D6 D3 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D2; 13.2 D1 D2 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D3 D9; 14.2 D5 D8; 14.3 D8; 14.4 D10 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline
- [ ] D1 · **Notice text** new `Messages.LoginNotice(running, utcNow, own)` returns null when no instance runs, else one line "Events in progress: <name> (<n> min left)[; <name> (<n> min left) …]. Type .nyar status for details." with the instances ordered by end time, each part the text `.nyar status` shows for it (an apocalypse's own status line through `own`, Messages.Status's "<n> min left" otherwise), cut to Wire.MaxBytes (512) by whole parts and ending "… and <m> more" when parts are left out; it holds no coordinate, zone radius, player name or platform id · test: Nyarlathotep.Tests LoginNoticeTests Text (fails when: a line is returned with nothing running, a part is out of end-time order, a line passes 512 bytes or cuts a part in half, the "… and <m> more" count is wrong, or a part differs from the status line of the same instance)
- [ ] D2 · **Who gets it, when** new `LoginNotices` in Logic/AnnouncerCore.cs with an injected clock: every user who connects, admin or not, while LoginEventNotice is on is queued once; the notice leaves 10 s after the connect (NoticeDelay) and its running list is read when it leaves, so a user is sent nothing when no event runs at that moment, when they disconnected or another player holds their user entity (platform id changed), or when they connected again within 60 s of their previous connect (its own LoginGate, separate from the admin degraded notice's, so a degraded admin still gets that notice too); an "event" is an instance in EventStore.Catalog.Running (started and not ended), never a definition that is only enabled · test: Nyarlathotep.Tests LoginNoticeTests Delivery (fails when: a notice leaves before 10 s, is sent with nothing running at its due time, reaches a disconnected or replaced user, is sent twice within 60 s of reconnects, is sent for an enabled event that is not running, skips a non-admin, or stops the admin degraded notice)
- [ ] D3 · **On by default, switchable** Config/Settings.cs binds `[Announcements] LoginEventNotice`, default true, described "Private line to a player who logs in while an event runs, naming the running events and their time left."; false sends no login notice; the boot line "[nyar] announcements: …" lists LoginEventNotice when on; tools/preflight.ps1 Test-CheckAnnouncementDefaults binds six switches and passes only when LoginEventNotice defaults to true and every other [Announcements] bool to false (design §9 D63's exception to S-24) · cmd: pwsh tools/preflight.ps1 → "announcement defaults: all off but LoginEventNotice (6 switches)" (fails when: LoginEventNotice is missing or false, or another announcement switch defaults to true)
- [ ] D4 · **Send failures contained** a private send that throws is counted by the announcer's private-message streak: one "[nyar] private message failed: <message>" line per streak, the other due notices of the same tick still sent, the scheduler tick not failed; a throwing running-list read leaves that notice unsent with one "[nyar] login notice failed: <message>" line per streak · test: Nyarlathotep.Tests LoginNoticeTests Faults (fails when: a throwing send stops the other notices, logs more than once in a streak, or escapes the tick, or a throwing read sends a notice)
- [ ] D5 · **Wire and privacy unchanged** Wire.Api stays at its value and `.nyar api version` keeps its fields (the switch is not on the wire in api 8); docs/RAPHAEL_INTEGRATION_CONTRACT.md says LoginEventNotice is a server cfg switch Raphael neither reads nor sets in api 8; the notice and its log line "[nyar] login notice sent: <k> events" carry no player name, platform id or position (PrivacyTests enumerates both) · test: Nyarlathotep.Tests ContractDocTests LoginNotice and PrivacyTests LoginNotice (fails when: Wire.Api or the handshake changes, the contract lacks the sentence, or the notice or its log line carries a name, id or position)
- [ ] D6 · **Controls registered** login-notice joins ControlCases.Plans with a row for every test and cmd control of this plan, a later step's control in ControlCases.Pending until its step's post-audit; LoginNoticeTests joins ControlCases.NewClasses · test: Nyarlathotep.Tests ControlCaseTests (fails when: a test or cmd item of this plan has no row, or a pending row outlives its step's post-audit)
- [ ] D7 · **Notice in game** tools/vrclient/scenarios/login-notice.vrs on the dev server: with `console TPHome` first, an event started by `.nyar event start` (a Manual spawns event copied from a template, so nothing depends on the clock), then `leave` and `ensure` to log in again after 61 s: the log shows "login notice sent: 1 events" within 30 s and the chat shows "Events in progress" (expect-chat); after `.nyar event stop`, a further `leave`, 61 s and `ensure`: no "login notice sent" line within 30 s · cmd: python tools/vrclient/vrclient.py run scenarios/login-notice.vrs → "SCENARIO PASS <n>/<n>" (fails when: the notice is missing with an event running, does not render in chat, or is sent with nothing running)
- [ ] D8 · **Rollback drilled** a server that installs 0.13.0 again on 0.13.1's files boots clean: BepInEx keeps the unknown LoginEventNotice entry unread, events.json and state.json are untouched by this child, and Rollout › Rollback › On a server says the step stays possible after data is written · cmd: pwsh tools/rollback-gate.ps1 -From v0.13.0 -To v0.13.1 -Plan login-notice → "rollback gate: 4/4" (fails when: the 0.13.0 boot logs an unhandled error, an orphan error or a Unity error, or the route text is missing)
- [ ] D9 · **Release 0.13.1** the six surfaces move to 0.13.1 in one `chore(release): v0.13.1` commit: both changelogs describe the login notice and its switch, both READMEs' cfg tables list `LoginEventNotice` (default `true`) and the announcement text says it is the one switch on by default; docs/features/LOGIN_NOTICE.md describes it with its Status; an annotated tag v0.13.1 and a GitHub pre-release with the tcli zip · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>" (fails when: a surface lacks the entry, the versions differ, or the tag is not pushed)
- [ ] D10 · **Sessions, records, paths** every server session is "### Session <n> · <date>" under docs/features/LOGIN_NOTICE.md › Test results with its log-check line in docs/audits/login-notice.md before the next restart and every [Error and [Warning line read; the audit has a pre-audit and a post-audit per Build plan step and the child's one "Codex verdict:" line; every path this child writes is in tools/paths-manifest.txt and declared by Rollout › Paths walked · cmd: pwsh tools/preflight.ps1 -AuditOf login-notice → "audit steps: login-notice 3/3 pre, 3/3 post, …"; pwsh tools/preflight.ps1 -SessionsOf login-notice → "session logs: login-notice <n>/<n> checked …"; pwsh tools/preflight.ps1 -Paths -DeclaredOf login-notice → "paths: <n> walked, all in manifest; declared: <k>/<k> in login-notice" (fails when: a step lacks an audit entry, a session lacks its log check, or a written path is undeclared)
- [ ] D11 · **Secrets and privacy** this child adds no credential; no tracked file or build log holds a token or the sentinel, and no tracked line holds a platform-id digit run or the owner's mail name except lines quoting the pattern · cmd: pwsh tools/preflight.ps1 → "secrets: none (…)"; then, before each push, git grep -n -E "7656119|kdpenland" → only lines that quote the pattern itself (fails when: a token, the sentinel, a platform id or the mail name appears in a scanned file)
- [ ] D12 · **Feature doc** docs/features/LOGIN_NOTICE.md exists with Status, Goal, Design summary, Open questions and `## Test results`, and tools/preflight-checks.json childDocs maps login-notice to it · file: docs/features/LOGIN_NOTICE.md contains "## Test results"

## Amendments

## Log
- 2026-10-17 · status → draft · plan
- 2026-10-17 · note · dry-run · D1 · later · step 1 · plants a part out of end-time order and a 513-byte line in LoginNoticeTests Text
- 2026-10-17 · note · dry-run · D2 · later · step 1 · plants a notice sent before 10 s and one sent with nothing running in LoginNoticeTests Delivery
- 2026-10-17 · note · dry-run · D3 · cmd: `pwsh tools/preflight.ps1` → "announcement defaults: all off (5 switches)", not the expected "all off but LoginEventNotice (6 switches)": the check fails as it should before the build
- 2026-10-17 · note · dry-run · D4 · later · step 1 · plants a throwing send that stops the other notices in LoginNoticeTests Faults
- 2026-10-17 · note · dry-run · D5 · later · step 1 · plants a player name in the notice's log line in PrivacyTests LoginNotice
- 2026-10-17 · note · dry-run · D6 · later · step 1 · plants a test item of this plan with no ControlCases row
- 2026-10-17 · note · dry-run · D7 · later · step 2 · plants a run with no event started, where the scenario's notice check must fail
- 2026-10-17 · note · dry-run · D8 · cmd: `pwsh tools/rollback-gate.ps1 -From v0.13.0 -To v0.13.1 -Plan login-notice` → "rollback gate: 1/4, failed: repo, drill, routes" ("tag v0.13.1 not found"; routes 3/5 until Rollout › Rollback named the revert range and the retitling, then `-RollbackOf` printed "rollback routes: login-notice 5/5")
- 2026-10-17 · note · dry-run · D9 · cmd: `pwsh tools/preflight.ps1` → "PREFLIGHT OK" and "release tags: 15/15" at 0.13.0: it passes before the build, so the evidence is the 0.13.1 entries and tag being present, checked after the release commit
- 2026-10-17 · note · dry-run · D10 · cmd: `pwsh tools/preflight.ps1 -AuditOf login-notice` → "audit steps: docs/audits/login-notice.md not found" and PREFLIGHT FAILED; -SessionsOf → "session logs: docs/features/LOGIN_NOTICE.md not found"; -Paths -DeclaredOf → "paths: declared: the created or changed paths of login-notice are unreadable (audit Step 1 base or git)"
- 2026-10-17 · note · dry-run · D11 · cmd: `pwsh tools/preflight.ps1` → "secrets: none (2136 files scanned, 2077 index blobs)"; `git grep -n -E "7656119|kdpenland"` → 101 lines, each a quoted pattern or a test's planted fake id (as at 0.13.0)
- 2026-10-17 · note · dry-run · D1 · test: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --filter "FullyQualifiedName~LoginNoticeTests|FullyQualifiedName~PrivacyTests.LoginNotice"` → "No test matches the given testcase filter": the Text test does not exist yet, so the item cannot pass
- 2026-10-17 · note · dry-run · D2 · test: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --filter "FullyQualifiedName~LoginNoticeTests|FullyQualifiedName~PrivacyTests.LoginNotice"` → "No test matches the given testcase filter": the Delivery test does not exist yet
- 2026-10-17 · note · dry-run · D4 · test: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --filter "FullyQualifiedName~LoginNoticeTests|FullyQualifiedName~PrivacyTests.LoginNotice"` → "No test matches the given testcase filter": the Faults test does not exist yet
- 2026-10-17 · note · dry-run · D5 · test: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --filter "FullyQualifiedName~LoginNoticeTests|FullyQualifiedName~PrivacyTests.LoginNotice"` → "No test matches the given testcase filter": PrivacyTests LoginNotice does not exist yet
- 2026-10-17 · note · dry-run · D7 · cmd: `python tools/vrclient/vrclient.py run scenarios/login-notice.vrs` → "FileNotFoundError: [Errno 2] No such file or directory: 'scenarios/login-notice.vrs'": no scenario, no PASS line
- 2026-10-17 · note · deferred · Review 1 F1 · an Epic amendment of D41's evidence text and S-24 for LoginEventNotice, in step 1
- 2026-10-17 · note · deferred · Review 1 F2 · one rule for a part's text: the Messages.Status element of its instance
- 2026-10-17 · note · deferred · Review 1 F3 · the Epic Log notes the size M
- 2026-10-17 · note · deferred · Review 1 F4 · consult the 60 s gate only when a notice is due
- 2026-10-17 · note · deferred · Review 1 F5 · an Open question on slow-loading remote clients in LOGIN_NOTICE.md
- 2026-10-17 · note · deferred · Review 1 F6 · log the planted runs of steps 1 and 2 before checking their items
- 2026-10-17 · note · a first approve, uncommitted, was refused by --check (the miss-history probes had only deferred dry runs) and withdrawn with its Baseline copy before any commit; the observed dry runs above were then run and the plan approved on the same READY review, its items unchanged
- 2026-10-17 · status → ready · approve · review: subagent
- 2026-10-17 · status → in-progress · start
- 2026-10-17 · note · Review 1 F1 done · Epic A47 (~D41) and an Epic Log note pointing S-24 at D63; preflight prints "announcement defaults: all off but LoginEventNotice (6 switches)"
- 2026-10-17 · note · Review 1 F2 done · the rule kept is D1's own "each part the text `.nyar status` shows for it" and its fails-when: each part is the Messages.Status line of its instance ("<name>: <n> min left"), so D1's illustrative "<name> (<n> min left)" is not what the line prints; the cut is Wire.MaxBytes, which is 480 (under the 512 D1 names); LoginNoticeTests Text asserts both
- 2026-10-17 · note · Review 1 F3 done · Epic Log note: A45 said S, the plan is M
- 2026-10-17 · note · Review 1 F4 done · LoginNotices consults the 60 s gate only for a notice about to leave, with the connect time; a connect that sends nothing does not use it up (LoginNoticeTests Delivery a_connect_with_nothing_running_uses_up_the_gate)
- 2026-10-17 · note · Review 1 F5 done · docs/features/LOGIN_NOTICE.md › Open questions: the 10 s delay on a slow-loading remote server, confirmed at the G-Portal weekend
- 2026-10-17 · note · method · the notice's recipient is generic in Logic (LoginNotices<TUser>): the user entity in game, any value in tests; the private-message streak is the announcer's, passed in
- 2026-10-17 · note · method · the preflight self-test's external rollback-drill row still expects "drill selftest: 32/32" while the drill prints 37/37 since 0.13.0; corrected in tools/preflight-checks.json in this step, and the MutatingFloor good fixture's seven service copies, older than unit watch's [Mutating] methods, refreshed from the tree (no plan item; both failed -SelfTest at 1435352)
- 2026-10-17 · note · planted · D1 · Messages.cs LoginNotice returns a line with nothing running · LoginNoticeTests.Text_fails_when_a_line_is_returned_with_nothing_running red (+Text_empty_no_running_instance_no_line) · restored
- 2026-10-17 · note · planted · D2 · AnnouncerCore.cs LoginNotices due at 0 s instead of 10 s · LoginNoticeTests.Delivery_fails_when_a_notice_leaves_before_10_s red · restored
- 2026-10-17 · note · planted · D3 · Config/Settings.cs LoginEventNotice default false · pwsh tools/preflight.ps1 → "announcement defaults: OFF by default but must be on: LoginEventNotice", PREFLIGHT FAILED: 1 check(s) · restored
- 2026-10-17 · note · planted · D4 · AnnouncerCore.cs a send failure logged on every failure, not once per streak · LoginNoticeTests.Faults_fails_when_a_throwing_send_stops_the_other_notices_or_logs_twice_in_a_streak red · restored
- 2026-10-17 · note · planted · D5 · AnnouncerCore.cs the sent line carries the platform id · PrivacyTests.LoginNotice_fails_when_a_name_id_or_position_reaches_a_line red (+LoginNotice_passes_counts_and_event_names_only) · restored
- 2026-10-17 · note · planted · D5 · docs/RAPHAEL_INTEGRATION_CONTRACT.md the login-notice sentence shortened · ContractDocTests.LoginNotice_fails_when_the_api_or_handshake_changes_or_the_contract_lacks_the_sentence red (+LoginNotice_passes_api_8_and_the_sentence) · restored
- 2026-10-17 · note · planted · D6 · ControlCases.cs the D1 row renamed D1x · ControlCaseTests.ControlCases_passes_plan_and_table_agree red · restored
- 2026-10-17 · note · dry-run · D6 · test: `dotnet test Nyarlathotep/Nyarlathotep.sln -c Release --filter "FullyQualifiedName~ControlCaseTests.ControlCases_passes"` → Failed! 1/1, ControlCases_passes_plan_and_table_agree red with the login-notice D1 row renamed D1x (a test item with no row); restored
- 2026-10-17 · D1 · pass · test: dotnet test --filter "FullyQualifiedName~LoginNoticeTests.Text_" → Passed! 5/5 (in the 31/31 evidence run) · 3f7803b · claude
- 2026-10-17 · D3 · pass · cmd: pwsh tools/preflight.ps1 → "announcement defaults: all off but LoginEventNotice (6 switches)", PREFLIGHT OK; -SelfTest AnnouncementDefaults good passes, bad, bad-2, bad-3 and empty fail · 3f7803b · claude
- 2026-10-17 · D5 · pass · test: dotnet test --filter "FullyQualifiedName~ContractDocTests.LoginNotice_|FullyQualifiedName~PrivacyTests.LoginNotice_" → Passed! 6/6 (in the 31/31 evidence run) · 3f7803b · claude
- 2026-10-17 · D6 · pass · test: dotnet test --filter "FullyQualifiedName~ControlCaseTests" → Passed! (in the 31/31 evidence run); login-notice in ControlCases.Plans, D7 to D11 in Pending · 3f7803b · claude
- 2026-10-17 · D12 · pass · file: docs/features/LOGIN_NOTICE.md has Status, Goal, Design summary, Open questions and "## Test results"; tools/preflight-checks.json childDocs maps login-notice to it · 3f7803b · claude
