# Audit — raphael-api-core

Build plan steps 1–6 of docs/dod/raphael-api-core.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.2.1 = 8b405a0.

## Pre-audit
### Step 1 · 2026-09-25 · 0453740
- pre-child: 0453740 — the plan's approve-and-start commit; the build's rollback base is the tag v0.2.1 (8b405a0)
- git status: clean
- compile: 0 errors, 0 warnings
- tests: 539 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: raphael-api-core 0/22 verified (just started); review human (Review 4 READY after Codex rounds 1–3)
- feature doc read: none yet — docs/features/RAPHAEL_API.md is created by this step; read the plan's D1–D4, Business rules 5, docs/RAPHAEL_INTEGRATION_CONTRACT.md §3 and §4, Logic/Wire.cs, Logic/Model.cs, Logic/Engine.cs
- server: not running; step 1 is pure logic with no in-game test

### Step 2 · 2026-09-25 · f64e04a
- git status: clean
- compile: 0 errors, 0 warnings
- tests: 600 passed
- preflight: exit 0 ("PREFLIGHT OK"); -AuditOf raphael-api-core 1/6 as expected
- dod status: raphael-api-core 3/22 verified (D1–D3); review human, not pending (A1 and A2 name no gating probe)
- feature doc read: docs/features/RAPHAEL_API.md (Status: step 1 of 6 done; Open questions: none); plan D4, D7–D11, D14, Build plan step 2 and A1; tools/preflight.ps1 Get-CommandWalk, Test-CheckAdminList, Test-CheckSecrets, Invoke-SelfTest
- server: not running; step 2 has no in-game test
- found on the way: AdminList/bad-2 already existed (the suffixed-attribute plant), so it moves to bad-3 and D11's two-group fixture takes bad-2 as the plan names it; a mislabelled command changes no count, so the admin-list check now also requires every walked command to begin a command documented in docs/NYARLATHOTEP_DESIGN.md § 6, which is what makes AdminList/bad-2 fail

### Step 3 · 2026-09-25 · 3ccf6bc
- git status: clean
- compile: 0 errors, 0 warnings
- tests: 649 passed
- preflight: exit 0 ("PREFLIGHT OK"); -AuditOf raphael-api-core 2/6 as expected; -AuthSuite fails only on `.nyar api sub` not found, expected until this step (A1)
- dod status: raphael-api-core 6/22 verified (D1–D4, D9, D14); review human, not pending (A3 and A4 name no gating probe)
- feature doc read: docs/features/RAPHAEL_API.md (Status: step 2 of 6 done, post-audit READY; Open questions: none); plan D5–D7, D12, D21, Build plan step 3, Business rules 1, 8–10, Interfaces, Design › States and Permissions, Failure & observability; contract § Push events; Services/Announcer.cs (WarningClock, UpcomingWave, GameUsers), Logic/Hooks.cs (Broadcaster, IUserSource), Services/EventRuntime.cs, WaveAction.cs, EventStore.cs, EventScheduler.cs, TriggerBus.cs, Patches/UserConnectPatch.cs
- server: not running; step 3's in-game checks run in step 4's session

### Step 4 · 2026-09-25 · c744f1b
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0 ("PREFLIGHT OK"); dod --check raphael-api-core: problems 0, warnings 0
- dod status: raphael-api-core 13/22 verified (D1–D11, D14, D21)
- feature doc read: docs/features/RAPHAEL_API.md (Status: step 3 of 6 done, post-audit READY; Open questions: none; step 3 results name the checks for the step 4 and 5 sessions); plan Build plan step 4, D20
- server: not running; dev config General.Enabled true, TimingLog true, WaveWarnings false, MaxDespawnsPerTick 5; the session uses tools/ingame/session-events.py mode `a21` (t-own and t-end at now+3)

### Step 5 · 2026-09-25 · 6cb8346
- git status: clean but tools/ingame/session-events.py (mode `rac2`, committed with this entry)
- compile: 0 errors, 0 warnings
- preflight: exit 0 ("PREFLIGHT OK"); -SessionsOf raphael-api-core "1/1 checked"; -AuditOf 4/6 as expected
- dod status: raphael-api-core 13/22 verified; review human, not pending (A8 names no gating probe)
- feature doc read: docs/features/RAPHAEL_API.md (Status: step 4 of 6 done; Open questions: none; the step 3 checks moved to Session 2); plan D5, D6, D12, D13, D22, Build plan step 5
- baseline boot: Session 1 (c744f1b, the same plugin code), log check clean
- session config: `session-events.py rac2` (11 definitions: the 5 examples with example-spawns enabled, 2 waves 40 s apart, 120 s; t-150 10 waves of 15, 20 s apart, 300 s, warnings on; t-spare and t-fill-1..4 disabled); dev cfg WaveWarnings false → true for D22 (copy of the previous cfg kept in %TEMP%); TimingLog true
- plugin version: the session build is `-p:Version=0.3.0` so `api version` reads the release number D13 names; step 6 bumps the six surfaces without a code change

### Step 6 · 2026-09-26 · dda8ba9
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0 ("PREFLIGHT OK", "release tags: 2/2", "data inventory: 43/43 complete"); -Paths "684 walked, all in manifest"; -AuditOf 5/6; -SessionsOf "2/2 checked"
- dod status: raphael-api-core 16/22 verified; D15–D20 open (step 6)
- feature doc read: docs/features/RAPHAEL_API.md (Status: step 5 of 6 done; Open questions: none); plan D15–D20, Build plan step 6, Design › Data, Rollout, Failure & observability (drill rows); Epic D32, D38, D45
- server: not running; the deployed DLL is the 0.3.0 session build; tcli at ~/.dotnet/tools
- drill design (D16, the plan leaves the "schema line" open): no boot line names a schema, so a file N wrote counts as read by N-1 when its load line is present and no "SchemaVersion … is newer … read-only" warning is logged — events.json "events: reloaded: <v> valid, <x> disabled", state.json "(<n> listed in state.json)" in the marker-sweep line, stats.json reported absent. The drill empties BepInEx/config/Nyarlathotep/ (saved first) so N seeds events.json and writes state.json itself, then edits one event name, stops, and boots N-1

## Post-audit
### Step 1 · 2026-09-25 · 6437b90
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests` → Passed 600, Failed 0 (61 new: ApiLinesTests, PagingTests, WireFormatTests cases)
- mutation check: sending units to players failed 2 cases; accepting page "0" failed 1; letting a duplicate id read active failed 1; restored code passes
- preflight: exit 0 ("PREFLIGHT OK", tree clean)
- /code-review (eca9522): 2 low findings, both fixed in 6437b90 — a running event no longer startable sent a reason (now reason only with state=disabled); a duplicate id row read active (now only the definition Find returns can be active)
- Codex cross-inspection round 1 (eca9522): REVISE, 5 findings — (1) action=- for a definition without an action: accepted, the pillar's action (amendment A2 changes D2); (2) an overdue cleanup showed an ending row: accepted, filtered; (3) Pages(int.MaxValue) overflowed: accepted; (4) examples were read from the whole contract: accepted, from the section documenting each tag; (5) err with secs never exercised: accepted, contract example and order test added
- Codex cross-inspection round 2 (6437b90): F2–F5 resolved, no new finding; F1 open against the unamended D2
- Codex cross-inspection round 3 (A2 at 76c55c3): F1 resolved, no new finding
- Codex verdict: READY (round 3)
- amendments: A1 (discovered, 5.1: `sub` moves to step 3 with the code it calls), A2 (discovered, 3.1: action named by pillar)
- in-game: none (pure logic step)
- dod status: D1, D2, D3 pass lines added; D4 waits for Wire.Api = 2 in step 2
### Step 2 · 2026-09-25 · f05f2ac
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests` → Passed 649, Failed 0 (ContractDocTests, ApiAccessTests, WireFormatTests and AuthorizationTests cases)
- mutation check: Wire.Api = 3 failed 2 WireFormatTests/ContractDocTests cases; restored code passes
- preflight: exit 0 ("PREFLIGHT OK"); selftest 27/27 checks, 89 extra bad fixtures, none passing
- -AuthSuite: fails only on ".nyar api sub not found once", expected until step 3 (A1)
- /code-review (09fbce5): a literal tag in Commands/ reached the wire unchecked (the check read Logic/ only) — fixed in c317a63, tags now collected from the whole plugin (A4)
- Codex cross-inspection round 1 (09fbce5): REVISE, 5 findings — alias/static import of Wire, tools/ credential rule by spelling, -AuthSuite could pass with zero tests, ApiAccessTests checked known keys only, ContractDocTests matched the design table loosely: all accepted (c317a63, A4)
- Codex round 2 (c317a63): F3–F5 resolved; new — global:: alias, imported Python environ / destructured Node env, ${env:TEMP} rejected: accepted (d03a335)
- Codex round 3 (d03a335): whitespace around :: or . in an alias; shell/batch expansions: accepted (86063d5 — tools/ holds no shell or batch script)
- Codex round 4 (86063d5): escaped @alias; extensionless and .zsh scripts: accepted (f05f2ac — tools/ holds only .ps1 .psm1 .py .mjs .js .json .txt .md)
- Codex round 5 (f05f2ac): both resolved, no new finding
- Codex verdict: READY (round 5)
- amendments: A3 (discovered, 4.5: the wire check reads the plugin only), A4 (discovered, 4.5: every call form, the environment allow-list, the tools/ file-type rule, reverse check in step 3)
- in-game: none (commands exercised in step 4's server session)
- dod status: D4, D9, D14 pass lines added; D7, D8, D10, D11 wait for step 3 (sub, the reverse check, UserDisconnectPatch)
### Step 3 · 2026-09-25 · 1ba3604
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests` → Passed 703, Failed 0 (SubscriptionTests, PushTests, ConfigChangedTests, the push cases of ApiAccessTests, DependencyFailureTests rows HookUserDisconnect and PushDelivery)
- mutation check: 25 mutants of Subscriptions, PushQueue, Engine, EventCatalog and DefinitionEditor each fail at least one test; two more make no observable difference (a malformed mutant, and a failed read that reloads, which also fails and pushes nothing)
- preflight: exit 0 ("PREFLIGHT OK"); "wire contract: 7 tags, 4 api commands, all documented (api 2)"; "patch guards: 5/5"; "ready guard: 11/11"; selftest 27/27, 90 extra bad fixtures; -AuthSuite "auth suite: pass (tests, commands, admin list, gateway)"
- /code-review (cc1b828): 3 findings, all fixed in 89cece2 — the warnings and the send shared one guard (now two); at the 128 cap players who left held the slots (now pruned before refusing, A6); the disconnect prefix threw and logged for connections never approved (now TryGetValue, returns quietly)
- Codex cross-inspection round 1 (cc1b828): REVISE, 3 findings — (1) exception messages in failure logs could carry a SteamID: accepted, the type only, sentinel test; (2) the overflow streak survived a drop below the cap: accepted; (3) the transition wiring was untested: accepted, the engine and catalog report through IPushSink (A6)
- Codex round 2 (89cece2): F1, F2 and CR1–CR3 resolved; F3 open — the reload/edit flows lived in the service: accepted, moved to Logic/DefinitionEditor with ConfigChangedTests (A7)
- Codex round 3 (bd8d677): F3 open — the EventStore delegates themselves and enable=true untested: enable and name cases added; the delegates are game-bound and go to step 4's session
- Codex round 4 (1ba3604): F3 resolved, no new finding
- Codex verdict: READY (round 4)
- amendments: A5 (discovered, 5.3: push values, own-method hook checks, dependency rows, bad-9), A6 (discovered, 12.4: transitions reported where they happen; prune at the cap), A7 (discovered, 12.4: the reload and edit flows in Logic)
- in-game: none in this step; step 4's session covers D12, the gateway path of `sub`, and the EventStore delegates
- dod status: D5, D6, D7, D8, D10, D11, D21 pass lines added; D12, D13, D22 wait for steps 4 and 5

### Step 4 · 2026-09-25 · 8bcb4bd
- compile: 0 errors, 0 warnings; deployed c744f1b's build (Release) to the dev server; no code changed in this step
- preflight: exit 0 ("PREFLIGHT OK")
- session 1: docs/features/RAPHAEL_API.md › Test results › Session 1 — "triggers: all hooks available" (UserDisconnect included), "push: ready", a21's t-own and t-end spawned 16 units and drained them to "0 left"; the boot sweep drained 5 units the previous save held; stopped after "Finished Saving"
- session 1 log check: 0 unhandled, 28 nyar lines, 0 orphan errors, 0 unity errors
- warnings read: Il2CppInterop Class::Init and two Beelzebub TUNE lines (BepInEx); 226 PrefabLookupMap warnings in the server log, all before "Startup Completed" (the game's save load). None from this mod.
- /code-review: not run; the step changes only the audit and the feature doc (no code)
- Codex cross-inspection round 1 (8bcb4bd): 4 non-blocking confirmations — the record matches the log; the step meets Build plan step 4 (TriggerBus's "all hooks available" requires the disconnect patch); moving the player-dependent step 3 checks to Session 2 changes test allocation, not scope, and step 5 covers D12; every warning is outside the mod. It counted 224 "unknown state" lines: the record now names both kinds (224 + 2 = 226)
- `-SessionsOf raphael-api-core` then failed ("docs/features/RAPHAEL_API_CORE.md not found"): the check ignored childDocs, which D20 names. A8 (defect, ~D20) records it; e8d73f2 reads every mapped doc (SessionLogs bad-13). A mutant that ignores the mapping fails the selftest on bad-13
- Codex round 2 (e8d73f2): the first run could start no process in its sandbox and reviewed nothing, so it was rerun with the commit, the function and the outputs pasted in. REVISE — (1) a session number in two mapped docs collapsed into one: accepted, a number used twice fails, in two docs (bad-14) or one (bad-15); (2) an empty mapping fell back to the slug-derived doc: accepted, it fails (bad-16); (3) this entry said "amendments: none": fixed here. Mutants: dropping the duplicate rule fails bad-14 and bad-15, and ignoring the mapping fails bad-13, bad-14 and bad-16; dropping the empty-mapping guard still fails bad-16 (as "no sessions"), so the guard changes only the message
- Codex round 3 (a91962e): F1, F2 and F3 resolved, no new finding
- Codex verdict: READY (round 3)
- amendments: A8 (defect, ~D20, 12.4: -SessionsOf reads the childDocs docs; one doc per session number; an empty mapping fails)
- dod status: D20 waits for steps 5 and 6 (every step and session); 13/22 verified

### Step 5 · 2026-09-26 · e480752
- compile: no code changed in this step; the session build of 881927c with -p:Version=0.3.0, 0 errors, 0 warnings
- preflight: exit 0 ("PREFLIGHT OK")
- session 2: docs/features/RAPHAEL_API.md › Test results › Session 2, two parts on one boot (owner, 127.0.0.1:9876, Raphael off). Every D13 line seen in the owner's screenshots; D12 by the log's "push: 0 subscribed (disconnect)" and a reconnect with no push; D22 tick averages 0.21–0.98 ms with 150 units and one subscriber; the step 3 checks (reload, enable, disable, set push one config-changed each; `set nope` none)
- step errors in the owner steps, not the mod: part 1 ran `purge confirm` after the event had drained ("nothing to purge"), and both parts omitted `.nyar purge` before `confirm`, which purge requires by design (foundation D20); part 2 repeated the killswitch check with both commands
- session 2 log check: 0 unhandled, 807 nyar lines, 0 orphan errors, 0 unity errors
- warnings read: Il2CppInterop Class::Init, two Beelzebub TUNE lines, the mod's purge line (Warning by design); 226 PrefabLookupMap warnings in the server log, all before "Startup Completed". None unexplained
- the owner's SteamID appears in the server log's "admin … ran" lines only; no record here or in the feature doc carries it
- dev cfg WaveWarnings restored to false; the deployed DLL stays the 0.3.0 session build until step 6's release build
- Codex cross-inspection round 1 (a8174fb): REVISE, 1 blocking — (1) D12's pass line proved the behaviour but not the item's structural clauses (Prefix, IsReady first, the user read, try/catch, once-per-streak log, Hook.UserDisconnect in the health line): accepted, the pass line now cites each in the source and the preflight and test evidence. Non-blocking: (2) D13's plugin=0.3.0 is the deployed session build: noted in D13's pass line; (3) D22's reply times by the owner's statement: accepted as manual evidence; (4) the purge refusal is a step error: the reusable steps for step 6's drill will name `.nyar purge` then `.nyar purge confirm`; (5) step 3 checks complete; (6) no SteamID in the repo: `git grep 7656119` finds only the plan's grep instruction, and no server log is stored in the repository
- Codex round 2 (d896479): F1 resolved; F2–F6 dispositions consistent; one low note — the D12 pass line named the test row HookUserDisconnect, whose method is DisconnectHook and covers the offline-prune fallback: the citation now says so
- Codex verdict: READY (round 2)
- amendments: none
- dod status: D12, D13, D22 pass lines added; 16/22 verified; D15–D20 wait for step 6

### Sessions 3 and 4 · 2026-09-26 (step 6 build)
- session 3 log check: 0 unhandled, 6 nyar lines, 0 orphan errors, 0 unity errors
  - practice drill runs A–C (docs/features/RAPHAEL_API.md › Session 3); the line is run C's last boot, run B's last boot read the same; earlier boots of runs A and B were overwritten before a check
- session 4 log check: 0 unhandled, 6 nyar lines, 0 orphan errors, 0 unity errors
  - practice drill run D; the drill ran -LogCheck after each of its three boots (6, 9 and 6 nyar lines, all 0 unhandled, 0 orphan, 0 unity)
- A9 (discovered, ~D16, gating 14.3): review: pending; a fresh review is owed before close

### Release 0.3.0 · 2026-09-26 (step 6 build)
- six surfaces at 0.3.0; compile check 0 errors, 0 warnings; 703 tests pass
- tcli build: Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.3.0.zip SHA-256 474b64d1b4d2b2f1f4081f942aaa0a3afd90978397ecfc924ff98018ad1b5abd (icon, README, manifest 0.3.0, BepInEx/plugins/Nyarlathotep.dll, CHANGELOG, LICENSE)

### Session 5 · 2026-09-26 (step 6 release drill)
- session 5 log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors
  - the drill's last boot (v0.2.1 on v0.3.0's files); its seed and drill-mark boots of v0.3.0 read 7 and 11 nyar lines, 0 unhandled, 0 orphan, 0 unity

### Session 6 · 2026-09-26 (step 6 drill after the push)
- session 6 log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors
  - the last boot (v0.2.1); the v0.3.0 boots read 6 and 10 nyar lines, 0 unhandled, 0 orphan, 0 unity

### Session 7 · 2026-09-26 (step 6 drill after A13)
- session 7 log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors
  - the last boot (v0.2.1); the v0.3.0 boots read 6 and 15 nyar lines, 0 unhandled, 0 orphan, 0 unity; no plugin DLL and a hidden config file planted, both restored as they were

### Session 8 · 2026-09-26 (step 6 drill after A14)
- session 8 log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors
  - part 1's boot (the server the drill refused to touch) and part 2's last boot (v0.2.1) read the same; part 2's v0.3.0 boots 6 and 15 nyar lines, 0 unhandled, 0 orphan, 0 unity
- exception to D20's per-boot rule, recorded once: in Session 3 the boots of practice runs A and B before their last one were overwritten before a log check (see Session 3). Their logs cannot be recovered; every boot since Session 4 is checked by the drill itself

### Session 9 · 2026-09-26 (step 6 crash and recovery, A15)
- session 9 log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors
  - part 1's crashed boot read 0 unhandled, 6 nyar lines, 0 orphan, 0 unity; part 2's v0.3.0 boots 6 and 14, its last boot (v0.2.1) the line above
