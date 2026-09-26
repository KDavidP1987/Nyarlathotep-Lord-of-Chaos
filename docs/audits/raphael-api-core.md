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
- Codex verdict: READY (round 1)
- amendments: A8 (defect, ~D20, 12.4: -SessionsOf reads the childDocs docs; one doc per session number; an empty mapping fails)
- dod status: D20 waits for steps 5 and 6 (every step and session); 13/22 verified
