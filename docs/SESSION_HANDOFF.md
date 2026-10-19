# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-19 (session close, the owner restarting the PC): start here.** v0.14.0 is released, on GitHub and on both servers. The session
ended with three backlog items researched and the owner's answers still due. All work is pushed (main = origin/main).
- **Where things run:**
  - **G-Portal:** on 0.14.0 since 2026-10-18 (only Nyarlathotep.dll swapped; details in the block below).
    - Verified running (FTP read, 2026-10-18): "Loading [Nyarlathotep 0.14.0]", templates 36/36, events 4 valid,
      "degraded: none", no Nyarlathotep error.
    - Other mods there: KindredCommands logs "Missing NetTopologySuite.dll" (respawn forcing limited), and Bloodcraft is
      1.13.22 (the newest is 1.13.26). Fixing both was offered, not asked for; do it only on the owner's request, after
      they shut the server down.
  - **Local dev server mods (synced 2026-10-18, owner's request: the Raphael-covered server mods):**
    - New: Bloodcraft 1.13.26 (Thunderstore, newest) and KindredCommands 2.5.8 with its NetTopologySuite.dll.
    - Updated: Beelzebub 0.137.5 → 0.137.7 (the GitHub release zip; that repo's HEAD only adds docs commits).
    - Already newest: Uriel 0.22.0 (deployed by the Uriel session; release commit 0bad5fe), Faust 0.16.5 (= its
      HEAD), Nyarlathotep 0.14.0. VampireCommandFramework stays 0.10.4: Bloodcraft and KindredCommands ask for it,
      and Thunderstore's 0.11.0 is not adopted.
    - Boot check: all nine plugins loaded, Bloodcraft "Startup checks passed", nyar templates 36/36, 0 [Error] lines.
      The 226 game missing-prefab warnings at save load are the same 224 GUIDs as the boot before.
    - Old plugins folder backed up in the session scratchpad (modsync/plugins-backup-20261018).
    - Bloodcraft now runs in every vrclient session: if a scenario changes (levels, familiars, stats), suspect it first.
    - Owner's scope (2026-10-18): the dev server must carry what Raphael covers (Bloodcraft, KindredCommands,
      Beelzebub, Uriel, Faust, Nyarlathotep). G-Portal's other 13 mods are of no consequence for now; they are kept in
      mind for future testing. G-Portal itself lacks Beelzebub, Uriel and Faust, and its Bloodcraft (file dated
      2026-10-15) is likely one version behind. Updating it was offered, not asked for: do it only on the owner's
      request, after they shut it down.
  - **Local dev server:** stopped, on the release DLL. Its cfg keeps MaxTrackedUnits 500 and MaxUnitsPerWave 500 for
    D27; set them back to 150 and 20 after.
- **Waiting on the owner, first thing next session:**
  1. **D27, undead hordes look and feel.** The sheet is in docs/features/UNDEAD_HORDES.md › Owner's check. Start the dev
     server first; the owner connects to 127.0.0.1:9876. Record the owner's line as D27's pass. Never self-accept it.
  2. **`dod close undead-hordes` (partial while D27 waits).** The owner confirms:
     - A3 (the 500 ceiling kept): new scope, or a reversal?
     - the 15 `note · method` Log lines (plan lines ~384-405, ~438-447): they changed how, not what.
     Then write the report, regenerate the pages, remove the build view, run the feedback step.
  3. **Decisions on the new backlog items:**
     - #13 has 8 decisions; #14-15 have 5. Each doc has a table with a recommendation per decision.
     - Next step for each: `/dod plan` (or the claudex loop: interrogation, Codex, sign-off).
- **New backlog** (the table "Backlog to 1.0" below, rows 13-15; Codex cross-read each doc read-only):
  - **#13 Defend objective,** docs/features/DEFEND_OBJECTIVE.md. Villagers the waves and horde marches go for: killed =
    lose; attackers cleared or time out = win.
    - Five spikes come before a plan: attack an NPC, stay put and stay safe, servants, unattended, placement.
    - Recommended first scope: SpawnWaves only. Size about L.
  - **#14 Concurrent events cap,** docs/features/LIMITS_CONTROL.md decision 1.
    - MaxConcurrentEvents is already a cfg key (default 3), capped at 10.
    - Raise the ceiling after an event-count load test (10, 25, 50 light events plus empowerments; target 50).
      Codex: per-event work exists (EventRuntime.Tick, empowerment, hunt passes), so it is not load-safe by the unit
      caps alone. Size S.
  - **#15 Live limits,** the same doc, decisions 2-5. `.nyar limit list/set` without a restart, an api 9 `limits` read
    so Raphael's editor can show the caps, and scheduled starts held up to 5 minutes for a free slot. Size M.
  - **Raphael's MaxUnitsPerWave question:** answered, with no change for 0.14.0 (LIMITS_CONTROL.md › The answer to
    Raphael now). The editor accepts 1-500, shows a note above 20, and never blocks.
- **Raphael docs, in order:** RAPHAEL_INTEGRATION_CONTRACT.md, then RAPHAEL_HANDOFF_API8.md, RAPHAEL_HANDOFF_WAVE_VARIETY.md
  and RAPHAEL_HANDOFF_UNDEAD_HORDES.md. Offered and not yet asked for: one merged page.
- **Other priority work, in the owner's order:** the "Backlog to 1.0" table:
  - scaling (#1), then replacements (#2);
  - the open closes: wave-variety 20/24, apocalypse 29/38, unit-watch 7/34, revenge 26/32, login-notice 12/12 (all wait on
    owner checks);
  - the suggestion not built: a validator warning when a horde march's growth.max ≤ its size.

**Written 2026-10-18 (v0.14.0 released)**: undead hordes shipped. **v0.14.0** is tagged at f0f9df0, pushed, and on GitHub
as a pre-release with kdpen-Nyarlathotep-0.14.0.zip. release-verify gives "hashes equal" and preflight "release tags: 17/17".
undead-hordes (docs/dod/undead-hordes.md) is **32/33**: only D27, the owner's look-and-feel check, is open.
- **In game (Session 1, vrclient):** D23 wave-destination 42/42 (rerun on the reviewed build 00874af), D24 infestation
  24/24, D25 horde march at 500 39/39, D26 the ceiling run across a restart, "ceiling run: pass (held 500, swept 500,
  rebuilt 480 of 480 …)". Limits found and recorded: A7 (hunters jump in straight lines) and A8 (the game stops NPCs 80 m
  or more from every player, so kills and growth happen only near players).
- **Dev server:** stopped. It runs the release zip's DLL. The uh4 snapshot is restored, and the cfg is at defaults except
  MaxTrackedUnits 500 and MaxUnitsPerWave 500, kept for D27. After D27, set them back to 150 and 20.
- **Waiting on the owner:**
  1. D27: the sheet is in docs/features/UNDEAD_HORDES.md › Owner's check. Record their line as D27's pass.
  2. `dod close undead-hordes` (partial while D27 waits): the owner confirms the excluded amendment (A3, requested) and the
     method notes first (lifecycle close step 4).
- **G-Portal test server on 0.14.0 (2026-10-18):** with the server stopped by the owner, only
  BepInEx/plugins/Nyarlathotep.dll was replaced over FTP by the release zip's DLL (sha256 aaa70ee4…bea13, read back equal;
  the other 17 plugins unchanged; config, events and state untouched). The 0.13.0 DLL it replaced: sha256 85bb12cc…7cea.
  The owner starts the server; read its first boot's nyar lines and every [Error/[Warning over FTP.
- **Raphael:** docs/RAPHAEL_HANDOFF_UNDEAD_HORDES.md lists the new fields, ranges and editor hints. The wire is unchanged
  (api 8).
- **Suggestion not built:** a validator warning when a horde march's growth.max is at or below its size (the D25
  fixture fault).

**Written 2026-10-17 (undead-hordes step 3 done)**: undead-hordes (docs/dod/undead-hordes.md, 0.14.0) is in progress,
**20/33**, with steps 1-3 closed. Steps 2 and 3 are Logic only, so nothing new has run in game yet.
- **Local only, not pushed:** step 2 (e366845, 40385fb, c737599) and step 3 (dbc04ad, evidence bfa8aa6). The owner was
  asked for the OK to push; there is no answer yet. Before a push, run the privacy grep `git grep -n -E "7656119|kdpenland"`;
  after it, check that `git rev-parse HEAD` equals `git ls-remote origin main`.
- **Step 3:** the Infestation and HordeMarch types, the clash rule, resume, chat fields, the templates and A5 (stall lines
  give the distance, never a point). Tests 4034/4034. Codex: REVISE, then REVISE twice more, all fixed, then READY.
  /code-review: 9 of 10 fixed. Records: docs/audits/undead-hordes.md › Post-audit › Step 3.
- **Changelog:** the root CHANGELOG.md has an `[Unreleased] - 0.14.0` section; the Thunderstore changelog follows at the release.
- **Next: step 4 (Services and the in-game sessions: D6, D9, D11, D17, D18, D23-D27).** The work deferred from steps 2-3:
  - EventRuntime passes the admin origin to ApocalypseHost.Prepare and the mode to ApocalypseOutcome.Line.
  - Suppress the generic start banner for an Infestation.
  - Wire the destination march in Services/WaveAction.cs.
  - Bound HuntAction's aggro seeding per tick.
  Tell the owner before each vrclient run. D27 is the owner's own look-and-feel test.
- **Known, not this child's:** the dod check flags an old review record in docs/dod/spikes.reviews.md (a VERDICT not on
  its own line); `-SessionsOf apocalypse` and `-SessionsOf unit-watch` fail on missing session log-check lines.

**Written 2026-10-17 (undead-hordes step 1 done)**: v0.13.1 is pushed and released. The undead-hordes child
(docs/dod/undead-hordes.md, L, 0.14.0, design §9 D62) is in progress, **4/33**, with step 1 closed.
- **Local only:** the plan commits (17a190b..3e22e87), step 1 158ec59 and its evidence f0e4132. Pushing them waits on the owner's OK.
- **Load ladder (Session 0):** 100 pass, 250 pass, 500 fail. At 500 the quiet minutes held 30 fps, but with Chaos among
  the units the fight minutes ran at about 19 fps; the mod's tick stayed at 1.2 ms. The owner kept MaxTrackedUnits at
  500 (A3). MaxUnitsPerWave goes 50 → 500 and the tick budgets 20 → 40. Rows: docs/features/UNDEAD_HORDES.md › Load test.
- **Hostility:** all four natives probed fight the spawned undead (feature doc › Hostility).
- **Owner:** A2 confirmed (D33, the stall rule spares the infestation and horde march).
- **Kept for the release:** %TEMP%\nyar-udh-logs (Session 0 log copies; `-Paths` flags them until they are deleted).
- **Known, not this child's:** `-SessionsOf apocalypse` and `-SessionsOf unit-watch` fail on missing session log-check lines.
- **Next:** step 2 (Logic: the destination rule and the spread draw: D5-D7, D12, D16, D18, D19, D21); pre-audit first.

**Written 2026-10-17 (login-notice, v0.13.1 ready, not pushed)**: the owner asked that a player who logs in while an event
is *running* be told so, on by default (design §9 D63, Epic A45, A47). The login-notice child (docs/dod/login-notice.md) is
built: **11/12**, only D9 left, and D9 needs the push.
- **Local only, 10 commits ahead of origin** (1435352..bc4d55f): the plan; steps 1-2; the Codex and /code-review fixes
  (bd6e01f, f6f5326, 8e2421a); release commit 7ec3328 `chore(release): v0.13.1`; **annotated tag v0.13.1 (local)**; and the
  evidence commit bc4d55f.
- **Waiting on the owner's OK:** `git push origin main v0.13.1`; then confirm `git rev-parse HEAD` equals
  `git ls-remote origin main`; then the GitHub pre-release v0.13.1 with `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.13.1.zip`
  (sha256 0724CA56…, recorded in docs/audits/login-notice.md); then `pwsh tools/release-verify.ps1 -Tag v0.13.1 -Asset
  kdpen-Nyarlathotep-0.13.1.zip` → "release verify: hashes equal"; then preflight "release tags: 16/16"; D9's pass line;
  and `dod close login-notice`. The owner uploads to Thunderstore.
- **What it does:** `[Announcements] LoginEventNotice` (default true). 10 s after a connect, a private line names each
  running event with its `.nyar status` text ("Events in progress: ln-test: 4 min left. Type .nyar status for details.").
  Nothing is sent when nothing runs, the player left, or the switch is off. There is one notice per 60 s of reconnects, and a
  quick rejoin replaces the queued notice. Failures are logged once per streak. Wire unchanged (api 8).
- **Checks:** tests 3859/3859; preflight OK except "v0.13.1 not pushed"; selftest 40/40 and 7/7; rollback gate 4/4;
  login-notice.vrs PASS 17/17 in Sessions 1 and 2 (Session 2 on the 0.13.1 build); audit 3/3/3, sessions 2/2, paths 74/74.
- **Codex:** REVISE (4) → fixed → re-check REVISE on one regex spelling → fixed (bad-5). /code-review: 10 findings, 3 bugs
  fixed, 2 comments fixed, 5 advisory left (plan Log).
- **G-Portal** still runs 0.13.0. Update it only on the owner's request (the DLL only; other mods live there).
- **Next:** after the push, plan the **undead-hordes** child (L, 0.14.0, design §9 D62): the owner's answers ("Up to 1000,
  tested", "Reuse the hidden march", "Two new + keep the old", "Game's own hostility only"). Wave start and destination,
  an infestation spreading from graveyards (1-3 undead per kill, from a pool), and a horde march of 50-500 from A to B that
  grows per kill and then infests. The 1000-unit load test comes first.

**Written 2026-10-17 (the v0.13.0 release)**: at the owner's request ("make sure that the current version is published to
GitHub … ready to be submitted to Thunderstore"), **0.13.0 is released before the players' test weekend**; the weekend now
runs on the released build and its findings go to 0.13.x.
- **Published:** release commit b695063 `chore(release): v0.13.0` (six surfaces), annotated tag v0.13.0, GitHub pre-release
  https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.13.0 with `kdpen-Nyarlathotep-0.13.0.zip` (the
  Thunderstore package, sha256 68E1978F…, `release verify: hashes equal`), the tester workbook and its PDF. Records commit
  f0d50f4 and the dod evidence commit after it, all pushed.
- **Thunderstore:** not uploaded (the owner publishes). The zip is `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.13.0.zip`
  and the same file is on the GitHub release: upload it at thunderstore.io (V Rising community, kdpen team), or
  `tcli publish --file <zip>` with the owner's token in TCLI_AUTH_TOKEN.
- **Checks:** tests 3830/3830; preflight OK ("release tags: 15/15", secrets none); rollback gates apocalypse and unit-watch
  4/4 (real boots of 0.13.0 then 0.12.0 on the same files); drill selftest 37/37; wave-variety audit 4/4/4, sessions 6/6,
  paths declared 127/127.
- **Tester package:** rows rewritten as real setups (saved spot, schedule plus announcement, named place to defend), event
  banners on in the install steps, now pointing at the v0.13.0 release; tester-setups.vrs 79/80 on rc.3 (the refused
  `action.arena 30` step dropped).
- **dod:** wave-variety 20/24 (D13, D17, D18, D19 wait on the owner), apocalypse 29/38, unit-watch 7/34 (the test weekend's
  items and older session-record items stay open: `-SessionsOf apocalypse` 0/4, `-SessionsOf unit-watch` 0/1 because those
  sessions' log-check and snapshot lines are not in the audit form). `dod close` waits for the weekend.
- **G-Portal test server on 0.13.0 (2026-10-17, owner's request):** with the server stopped by the owner, only
  BepInEx/plugins/Nyarlathotep.dll was replaced over FTP (hash checked; the other 17 plugins, config, events and state
  untouched). First boot: "regions: 10 polygons, 10 regions", "templates: 34/34 valid", "triggers: all hooks available",
  "nyar health: 2 events, 12 tracked, degraded: none" (0.12.0 there said "degraded: hook DayNight, regions: unavailable").
  One "slow tick: 313 ms" during the undead-nightfall empower sweep of 190 units at boot; no Nyarlathotep error.
- **Next:** the owner's test weekend on 0.13.0 (apocalypse D31, unit-watch D23 and D29, wave-variety D19); then 0.13.x fixes;
  the HuntAction eligibility-helper advice; live-ops planning.

**Written 2026-10-16 (wave-variety session close)**: the wave-variety child (docs/dod/wave-variety.md, 18/24) is
built through step 4's pre-release work, commits a896846..88cb5cf, all pushed (main cf2ef2a).
- **Published (owner's OK 2026-10-16):** test build **v0.13.0-rc.3** (tag on cf2ef2a, GitHub pre-release with the zip,
  workbook and PDF; DLL md5 2b7ddd324615617f6e4ed5121ba7d43f). Workbook rows N-18, N-19 cover the new templates.
- **Raphael:** docs/RAPHAEL_HANDOFF_WAVE_VARIETY.md (nothing new on the wire, still api 8; new editor fields and the
  template grouping). Take it with the contract to the Raphael session.
- **What it adds (design §9 D61, Epic A44):** `action.pursuit` (after N s with nobody in the arena, the waves hunt the
  nearest eligible player map-wide; claimed territory is a refuge), named `action.pools` with pool entries and boss rolls
  (a wave's `bosses`, min-max from a pool), the grown allied groups (docs/GAME_ASSETS.md › Faction compatibility), and 14
  templates: seven `<base>-timed` (a fixed 75 s beat) and seven `<faction>-onslaught` (rolled waves, a boss mid-run and
  one or two at the end). Feature doc docs/features/WAVE_VARIETY.md; EVENT_LIBRARY.md lists them.
- **In game (vrclient, Sessions 1-6):** pursuit.vrs 27/27 (last on the final build, Session 6), wave-variety.vrs 26/26,
  tick budget with 20+ pursuers "10/10 windows under 5 ms". Natives (bears, wolves) east of the pond kill pursuers even
  at level 60; the timing scenario now records the count and leaves the bar to -TimingSpan.
- **Codex (the child's single cross-inspection):** CHANGES REQUESTED (2) → both fixed (boss rolls on shared pools; the
  quiet clock of a run decided between checks) → re-check found the shared-member double count → fixed (a matching) →
  **PASS**. The Codex lines go into step 4's post-audit with the release (the post-audit header ends D22-D24's pending
  controls, so it is written in the release commit).
- **Rollback (D21):** 0.12.0's reasons read from a real boot; "drill selftest: 37/37".
- **Waiting on the owner (rubric 3):** D13, D17, D18 (accept the vrclient evidence), D19 (look and feel: connect to
  127.0.0.1:9876, `.nyar template use undead-onslaught`, `.nyar event enable undead-onslaught`, `.nyar event start
  undead-onslaught`, walk 60 m away and wait; PASS when the units come after you without visibly teleporting in view and
  the waves differ between two runs).
- **Next:** the 0.13.0 release commit (apocalypse D33, unit-watch, wave-variety D22/D23): six surfaces with the README
  template tables listing all 34 templates, step 4's post-audit with the Codex lines, `-AuditOf/-SessionsOf/-Paths
  -DeclaredOf wave-variety`, then `dod close wave-variety` (partial while D13/D17-D19 wait). rc.3 is published; the
  release waits on the owner's test weekend. Remove the %TEMP% log folders `-Paths` reports after the release records.
- **Advice left (/code-review step 3, finding 5):** the eligibility rule (claimed territory, PvP combat) is written three
  times in Services/HuntAction.cs (targets, verbose tally, seeds); one helper after 0.13.0.
- **Stale references:** docs/dod/scaling.md and replacements.md (both ready) mention BossWave.Units; the code is now
  Pools.Resolve. Not edited (an edit voids READY); update when each plan is next amended.
- **Dev server:** stopped; snapshot wv4 restored, so it holds the step 3 DLL (f20d98e) with VerboseLogging false. Session
  log copies in %TEMP%\nyar-wv3-logs (and nyar-wv2-logs, nyar-logs-20261016-rc2): remove them after the release records,
  or `-Paths` reports them as leftover temp.

**Written 2026-10-16 (session close)**: fixes from the owner's G-Portal test (2026-10-15) and six new wave templates,
commit d492bd5 on main (local; push and a new test build wait on the owner's OK).
- **G-Portal (hosted, read-only by owner's rule: never change anything on it; FTP only to read logs).** Findings: a
  brand-new world initialised Nyarlathotep before the day/night cycle and region polygons existed ("degraded: hook
  DayNight, regions: unavailable", so day/night events never fired). Fixed: init waits up to 120 s (Epic A42,
  docs/features/FOUNDATION.md). The apocalypse Admin goal was refused "admin_location": fixed (AdminFlows).
  pillar_off for bandit-ambush is correct (Spawns ships off). units=0 was the owner's fast kills; an early-departure log
  line now names any event unit that leaves within 60 s, with how.
- **Six wave templates (Epic A43, design §9 D60, apocalypse A14):** militia-muster, silverlight-crusade,
  legion-incursion, gloomrot-assault, cursed-night, realm-gauntlet (one faction per wave, each waiting for the last to be
  cleared). In game: wave-templates.vrs PASS 54/54, faction-mix-waves.vrs 131/132 then cursed 26/26 after
  CHAR_Cursed_Mosquito was dropped (the game removed one 10 s after spawn, cause unknown). D38 ticked. Codex PASS.
- **Raphael:** its analysis is in the Raphael repo `docs/NYAR_FEEDBACK_2026-10-15.md` (its own items fixed in Raphael
  0.71.0). Here: handoff API8 §7 step 7 corrected (a purge sends only `killswitch`); its requests R1-R3 (units-gone push,
  `action=apocalypse`, the pillar in pillar_off) are in contract §8 and §10.5/§10.7 for live-ops.
- **Not yet done:** README template tables and both CHANGELOGs (at the 0.13.0 release commit, six surfaces);
  `v0.13.0-rc.2` test build for the G-Portal retest (tag, pre-release, `ROUND["download"]`); push.
- **Dev server:** running (VerboseLogging back to false). Config backed up to %TEMP%\nyar-fresh-20261016 and restored
  after the brand-new-world boot (save folder `save-data-nyarfresh` left on disk, unused).

**Written 2026-10-15 (session close)**: a docs-only session, pushed to main.
- **Raphael:** `docs/RAPHAEL_HANDOFF_API8.md` is the new handoff for the Raphael workspace (api 8 panels, refresh
  schedule, notifications, the player overlay, a test checklist). Contract §10.5-§10.9 specify the proposed live-ops
  and communications wire.
- **Decisions:** the owner settled them as design §9 D59 (L1-L8): exact coordinates of every spawned unit for admins,
  per event or in bulk; a player view with countdowns and personal alerts, without unit details.
- **Epic:** A41 adds children live-ops (next, beside the test weekend, before scaling) and communications (after
  replacements). Neither has a plan yet; `/dod plan live-ops` is the next planning step.
- **Uriel:** `docs/lessons/SPAWNING_LESSONS_FOR_URIEL.md` is rewritten as unit lessons for Uriel's castle events.
- **GitHub pages:** the root README was rewritten, `docs/README.md` indexes the docs, the Thunderstore README's feature
  blocks were regrouped by pillar (moves only), and the repo description and topics were updated.

The 2026-10-14 close below still describes the build state.

**Written 2026-10-14 (session close)**: the coming 0.13.0 passed Codex's pre-release review after four fixes (b1e3cdc);
the replacements plan (D58) is approved; the tester package is in GitHub (`testing/`) with install steps, and test
build **v0.13.0-rc.1** is a GitHub pre-release (mod zip + blank workbook + PDF, booted on the dev server first). main
and `scaling` are pushed and equal to origin. The repo is
`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.12.0 (revenge + final-wave bosses)** pushed with its GitHub pre-release. `dod close revenge` (partial) waits on the
  owner's sign-off.
- **Apocalypse + unit watch → 0.13.0** (`docs/dod/apocalypse.md`, `docs/dod/unit-watch.md`): steps 1-3 of both done;
  test weekend sheets in `docs/features/APOCALYPSE.md` and `docs/features/UNIT_WATCH.md`. Codex's once-per-child
  pre-release review (`git diff v0.12.0..main`) said REVISE with four findings, all fixed in b1e3cdc and re-checked
  APPROVED:
  - unit-watch's off-ground level scan costs walk-budget per probe and stops 10 m above the unit (D10's ceiling);
  - a failed apocalypse Begin rolls the start back (logged "begin failed; the start is rolled back", refused);
  - a purge counts the run's waiting orders in the final tally;
  - the fight read follows three owner links.
  The deployed dev DLL (md5 6345be3f…) predates 6f9f75c and b1e3cdc; the weekend build must be rebuilt from main.
- **Scaling** (`docs/dod/scaling.md`, 18/27, branch `scaling`, release 0.14.0): steps 1-4 done; step 5 sheet for real
  players in `docs/features/SCALING.md` › Owner test sheet. After merging b1e3cdc, e26f2bb updated one source check for
  `EventRuntime.End`'s new `announce` parameter (3700/3700).
- **Replacements** (`docs/dod/replacements.md`, ready 0/29, design §9 D58, Epic A40, release 0.15.0): review 1 REVISE
  (world death causes never exercised) → D29 Session 3 per cause (water, fall, other NPCs, sunlight) and S-16 (a cause
  the game delivers as a removal is not replaced, told to the owner); review 2 READY. Build on branch `replacements`
  from the `scaling` tip after `dod start replacements`; step 1 is the `.devperish` tool and the spike.
- **Tester package** `tools/tester-package` (round 1, 20 production-setup rows). `python tools/tester-package/build.py`
  builds it and publishes the blank workbook + PDF to `testing/` (committed, with `testing/source.json`);
  `TesterPackageTests` fails when `rows.py`/`build.py` change without a rebuild. The "Start here" sheet, PDF and HTML
  carry the download link and nine install steps. Filled-in workbooks stay out of git (gitignored output folder).
- **Test build v0.13.0-rc.1**: annotated tag on aa7e4d3 and GitHub pre-release
  https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.13.0-rc.1 with
  `Nyarlathotep-0.13.0-rc.1.zip` (DLL + INSTALL.txt), the blank `.xlsx` and `.pdf`; downloaded back, md5 match. The rc DLL
  was built with `-p:Version=0.13.0-rc.1` (csproj stays 0.12.0) and booted on the dev server ("v0.13.0-rc.1 loaded",
  "push: ready", only the 6 baseline warnings). Untested: whether the optional Raphael client minds the "-rc.1" version.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21, D32, D22; apocalypse D24, D37, D25-D28,
  D30, D31; unit-watch D19, D24-D27 (and D23, D29 at the weekend); scaling D19, D20, D21 (and D23 with real players).
- **dod store:** the closed plan `spikes` reports one `--check` problem (an old plan, not touched this session).
- **Dev server:** stopped; snapshot sc4 restored.

## Next, in order

00. With the owner's OK: push d492bd5 and its docs commit, build `v0.13.0-rc.2` (step 1's recipe below) and hand it to
    the G-Portal test (retest: day/night events, zombie-apocalypse at the admin, the new templates with the Spawns
    pillar on).
0. `/dod plan live-ops` (parent nyarlathotep; contract §10.5-§10.7, D59), one subagent review round; build on its own
   branch beside the test weekend.
1. A row edit in `tools/tester-package/rows.py` needs `python tools/tester-package/build.py` (TesterPackageTests fails
   otherwise); a new test build is a new `v0.13.0-rc.<n>` pre-release (DLL built with `-p:Version=0.13.0-rc.<n>`,
   tag and release declared in tools/paths-manifest.txt, `ROUND["download"]` updated).
2. Test weekend (~10-24/25) on v0.13.0-rc.1 (or a later rc): apocalypse and unit-watch sheets; scaling's step 5 sheet on a
   build of `scaling`.
3. Joint 0.13.0 release (apocalypse + unit watch): six surfaces, tag, rollback gate `-From v0.12.0 -To v0.13.0`, by
   10-31. Then merge main into `scaling`, scaling step 6 → 0.14.0.
4. Replacements: `dod start replacements`, branch, step 1 (pre-audit, `.devperish`, spike Session 0).
5. Then (D55): other boss types + faction compatibility, ambush spacing, the pathing study (D50), the Epic children.

## Backlog to 1.0 (as given to the owner 2026-10-14)

**Halloween 0.13.0 (apocalypse + unit watch), due 2026-10-31**

1. Test weekend (~10-24/25) on v0.13.0-rc.1: tester workbook round 1 (20 rows), apocalypse and unit-watch sheets; the
   scaling sheet can run the same weekend on a `scaling` build.
2. Owner sign-offs (rubric 3, never Claude): apocalypse D24, D25-D28, D30, D31, D37; unit-watch D19, D23-D27, D29;
   revenge D18-D22, D32 (lets revenge close `done · partial`).
3. Weekend fixes, then `v0.13.0-rc.2` if anything changed.
4. Release 0.13.0: six surfaces, tag, rollback gate `-From v0.12.0 -To v0.13.0`, GitHub pre-release; the owner publishes
   on Thunderstore.

**After Halloween, in the owner's order (D55)**

| # | Item | State | What's left |
|---|---|---|---|
| 1 | Scaling → 0.14.0 (`docs/dod/scaling.md`) | 18/27 | step 5 real-player session + owner sign-off (D19-D21, D23); step 6 release |
| 2 | Replacements → 0.15.0 (`docs/dod/replacements.md`, D58) | ready 0/29 | step 1 `.devperish` + spike; 2 Logic; 3 Services; 4 vrclient sessions incl. D29 world deaths (water, fall, other NPCs, sunlight); 5 owner session; 6 release |
| 3 | Other boss types + faction compatibility | no plan | final-wave bosses for undead and other factions; do ghosts, undead, shadow creatures fight each other when mixed |
| 4 | Ambush spacing | no plan | ambushes appear 30-40 m away, not on top of the player (owner, pond test) |
| 5 | Pathing study (D50) | no plan | walk to a map spot or castle heart on the game's roads/pathing instead of hidden jumps |
| 6 | Boss reinforcements (`docs/dod/boss-reinforcements.md`) | ready 0/20 | adds joining V Blood fights as the boss loses health |
| 7 | Anti-farming (Epic A29) | no plan | escalating reinforcements against a group farming one area, incl. V Blood copies |
| 8 | Outbreak (Epic A30) | no plan | faction hordes spreading from home territory; waves of 30-100 led by empowered boss copies |
| 9 | Sieges (Epic A31) | no plan | NPC waves on castles; optional castle destruction off by default, raid windows only, 5-minute clan warning |
| 10 | Spawn extras (Epic A32) | no plan | Ambush and Guard modes, spawn visual effect |
| 11 | Stats | no plan | kill/participation counters, leaderboards, daily digest, privacy controls |
| 12 | Epic close-out (`docs/dod/nyarlathotep.md`) | 38/47 | D16, D22 (closed plan `spikes` has one `--check` problem), D32 rollback drill, D36 permissions, D37 Raphael wire lines, D43 messages in game; D39, D40, D46 wait on Stats |
| 13 | Defend objective (owner, 2026-10-18) | researched, no plan | spawn villagers (one or a room) that waves and horde marches go for; killed = lose, attackers beaten or time out = win. Research, spikes and 7 open decisions: `docs/features/DEFEND_OBJECTIVE.md` |
| 14 | Concurrent events cap (owner, 2026-10-18) | researched, no plan | `MaxConcurrentEvents` is already a cfg key (default 3) but capped at 10; raise the ceiling (target 50, after an event-count load test) so admins pick 3, 10 or 20. `docs/features/LIMITS_CONTROL.md` decision 1 |
| 15 | Live limits and Raphael's cap read (owner, 2026-10-18) | researched, no plan | `.nyar limit list/set` without a restart, an api 9 `limits` read so Raphael's editor shows `MaxUnitsPerWave`; held scheduled starts. `docs/features/LIMITS_CONTROL.md` decisions 2-5 |
