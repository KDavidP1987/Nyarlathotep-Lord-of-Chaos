# Reviews: event-spawns

## Review 1 · 2026-09-28 · codex · plan uncommitted · plan 56565 B · 27 items · files 0 · e3b0c44298fc · prompt c82be1da5cd5
F1 `advisory` — Blind re-score: 1 Considered—Purpose & typical use; 2 Gap; 3 Gap; 4 Gap; 5 Considered—Interfaces › Internal; 6 Gap; 7 Gap; 8 Considered—Use cases › Minimal stretch; 9 Considered—Use cases › Maximal stretch; 10 Gap; 11 Gap; 12 Gap; 13 Gap; 14 Gap; 15 Considered—Out of scope.  
Fix: Populate the Coverage table with these statuses and replace each Gap only after resolving the findings below.

F2 `blocking` — Probe `2.1` does not account for unauthenticated connections, service accounts, the filesystem/server operator, or release publisher; D22 only tests registered commands and mutating callers.  
Fix: State whether each actor can reach the feature and its permitted actions, then make `pwsh tools/preflight.ps1 -AuthSuite` fail when any reachable actor/path lacks that policy.

F3 `blocking` — Probe `3.1` is unanswered for the temporary `debug walk [radius]` input: its default, range, numeric format, and invalid-input response are unspecified.  
Fix: Define those parsing rules in D1 and add failing, valid, omitted, and malformed-radius cases to its evidence.

F4 `blocking` — Probe `3.3` lacks retention and deletion decisions for build outputs, release zips/releases, audit/review documents, backups and temporary files; D27 checks path declaration, not cleanup or retention.  
Fix: Extend the artifact table with owner, retention and deletion for every produced artifact, and name one command that fails when retained artifacts or expired temporary directories remain.

F5 `blocking` — Probe `4.4` states precedence in prose, but no single evidence command fails if caps, territory/no-player skips, rolling, or `allowTerritory` are reordered; D8 and D17 test isolated rules rather than the complete conflict order.  
Fix: Add a precedence test matrix and an exact command such as a filtered `dotnet test` invocation that fails when any stated ordering or exception authority changes.

F6 `blocking` — Probe `4.5` does not say how the D22 scanner computes “every new `[Mutating]` method/control” or whether it sees untracked, generated, newly created, and excluded files when it runs.  
Fix: Specify the enumeration root and inclusion rules, require a tracked-plus-untracked filesystem walk, and add selftest fixtures for newly created and excluded-path members.

F7 `blocking` — Probe `6.1` lists game contracts but makes no actionable quota/cost decision and does not establish that every runtime record/component variant—not merely expected ones—was sampled.  
Fix: State “no quota or monetary cost” where true and define the enumerated component/record set plus the command or session evidence that validates every member.

F8 `blocking` — Probe `6.2` defers SpawnLedger and editor failures to existing tests and gives no slow/down/garbage behavior for them; tooling coverage also omits concrete failure behavior for tcli and the session helpers.  
Fix: Decide fail-open/fail-closed, retry and user-visible outcomes for each collaborator, then name one dependency-failure command that fails when any required containment is removed.

F9 `blocking` — Probe `7.1` never decides whether loading and partially completed spawn/recipe states exist or what happens to units already prepared when a later unit or visual operation fails.  
Fix: State that these states are impossible with the enforcing boundary, or define their cleanup and feedback behavior and map it to a D-item.

F10 `blocking` — Probe `10.3` is supported only by “the Secrets check keeps running”; D22’s stated `fails when` does not include secret storage, rotation, or logging and therefore supplies no control evidence.  
Fix: State where existing release credentials live and rotate, what fields must never be logged, and name a secrets command that fails when a planted credential reaches source, configuration, logs, or artifacts.

F11 `blocking` — Probe `11.3` answers line length and colour only; keyboard operation, screen-reader exposure, and small-screen wrapping/truncation remain undecided.  
Fix: State the inherited game-chat behavior for all three, including any unsupported limitation, and map the decision to a manual or automated D-item.

F12 `blocking` — Probe `12.3` names log messages as “the alert,” but supplies no production alert/dashboard or an explicit decision that the admin must monitor a named log/health command at a stated cadence.  
Fix: Choose a production detection mechanism, owner and trigger condition, and add evidence showing that a planted failure is surfaced there.

F13 `blocking` — Probe `12.4` is not met by the blanket claim that tests have `fails-when`: the plan does not give every introduced check a failing input, silent input and non-passing empty-input output, and D22’s ControlCases list omits release, privacy, audit, session and path controls.  
Fix: Add a check-by-check fixture table covering fail/silent/empty behavior and make `pwsh tools/preflight.ps1 -SelfTest` fail when any fixture or exact real-input spelling/state is missing.

F14 `blocking` — Probe `13.2` gives provenance and excluded-valid-case reasoning only for Hunt’s five-target cap and the 25-point search, not for chance, level, multiplier, leash, range, AroundPlayer, chat-length, or unit caps.  
Fix: For every bound, state its source, behavior at the boundary, and one valid case it intentionally excludes.

F15 `blocking` — Probe `14.4` uses unresolved labels such as “the six release surfaces” and does not establish that the walker observes untracked/new/generated paths or the release zip before manifest comparison.  
Fix: Enumerate the six paths explicitly and make `pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns` walk the working filesystem, generated outputs and remote/review records, with fixtures proving each missing class fails.

F16 `blocking` — D24 is unverifiable by a stranger because “tick average” has no measurement duration, warm-up, sampling count, percentile policy, or exact timing-log aggregation rule.  
Fix: Define the measurement window and calculation, then require the recorded lines and command/manual calculation that independently produces the asserted average.

F17 `blocking` — D14 claims reveal both on proximity and on attack, but its evidence records only one Ambush wave from 30 m and does not identify which activation path was exercised.  
Fix: Require two recorded trials—approach without attack and attack while outside approach range—or narrow the claim to the single behavior actually evidenced.

F18 `advisory` — S-10 is not cheaply reversible: adding a later `weights` model changes validation, chat authoring, templates and composition semantics, while per-copy chance does not generally implement weighted selection.  
Fix: Relabel it as a settled scope interpretation or obtain an explicit owner decision that weighted selection is deferred to a named child.

5/15 layers · 35/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · consequence of F2-F17; the Coverage pointers are updated with the new items
- F2 · accepted · Design › Permissions names unauthenticated connections (never reach chat), the operator (file edits validated alike by D6), Claude (builds and pushes) and the owner (sole publisher, D31), no service account; D22's auth suite now also fails on a new command without adminOnly
- F3 · accepted · +D28 (Walk radius argument): default 0.5, 0.1-5 invariant decimal, the refusal line and CommandArgTests WalkRadius with its fails-when; D1 cites it
- F4 · accepted · Design › Data gains rows for events.json.bak, build outputs, the release zip, tags and releases (the only things that exist once) and the committed docs, each with owner, retention and deletion; D27 fails on a leftover %TEMP%
yar-* folder ("paths: leftover temp")
- F5 · accepted · +D29 (Wave precedence order): Logic WaveGate.Decide gives one outcome in the stated order and WaveAction decides nothing itself; evidence `dotnet test --filter WavePrecedence` with at least 12 tests; Business rules 8 cites it
- F6 · accepted · D22 states the enumeration: Get-TreeFiles (git ls-files --cached --others --exclude-standard) over Commands, Patches and Services, so a new untracked file is seen; HuntAction joins $DispatchedServices; new fixture GatewayOnly/bad-new plants a [Mutating] method in a new untracked Services/Stray.cs
- F7 · accepted · Interfaces › External states no quota and no monetary cost, and the record variants sampled: every CHAR_ prefab's SpawnBuffElement at boot (D7's "ambush: <n> units can hide" against the dump's 36, recorded in D14), territories with and without a heart (D17), and every player state PlayerPick reads (D16)
- F8 · accepted · D21 and Interfaces › External decide SpawnLedger and the editor (in process; an exception faults the event per Epic D25; a failed write changes nothing, event-library D19), tcli (stops the release before the tag) and the session helpers (stop before boot; the snapshot restores); D21 runs under -DependencySuite
- F9 · accepted · Design › States adds Loading (no wave before Core.IsReady) and Partial (a failing unit is discarded alone; earlier units stay tracked; its visual is dropped); D21's fails-when covers both
- F10 · accepted · +D31 (Secrets and privacy): where credentials live (gh credential store; TCLI_AUTH_TOKEN only in the owner's environment), who rotates them, what is never logged, and the Secrets check plus the pre-push privacy grep with their planted fixtures
- F11 · accepted · +D32 (Chat lines in real chat): every new reply rendered in game; keyboard-only chat and no screen reader, stated as an inherited limitation in the README; Design › UX 11.3 cites it
- F12 · accepted · +D30 (Health shows spawn trouble): the walk and territory failures join HealthMonitor's degraded list, so the 10-minute health line, `.nyar status` and the admin login notice carry them; Failure & observability 12.3 names the login notice as the alert
- F13 · accepted · Failure & observability gains a table of every check this plan introduces with its failing, silent and empty input; release, privacy, audit, session and path checks are the existing ones with their fixtures; ControlCases list extended with D28-D30
- F14 · accepted · Performance gains a bounds table: every range and cap with its source, its behaviour at the bound and the valid case it excludes
- F15 · accepted · Rollout › Paths walked enumerates the six surfaces and states what the walker reads (tracked, untracked, ignored and generated files after the builds, server paths, %TEMP%
yar-*, remote tags and releases)
- F16 · accepted · D24 defines the measurement: the ten 60-tick timing lines after the first health line showing at least 140 tracked units, the first window skipped as warm-up, each average under 5 ms, no slow-tick line in the span, a player within Hunt range
- F17 · accepted · D14 requires two trials: approach from 30 m without attacking (recording the reveal distance), and a ranged attack from 20 m on a fresh wave
- F18 · accepted · S-10's fallback now says a weighted pick is new semantics that would come as a requested amendment with its own key, validation, chat field and template, leaving chance unchanged; it stays reversible because nothing built here is undone
