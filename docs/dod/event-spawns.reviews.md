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

## Review 2 · 2026-09-28 · codex · plan uncommitted · plan 69405 B · 32 items · files 0 · e3b0c44298fc · prompt 5f616b5f388b
F1 `advisory` — Blind rescoring is: Considered 1 (`Purpose & typical use`), 2 (`Design › Permissions`), 5 (`Interfaces › Internal`), 8 (`Use cases › Minimal stretch`), 9 (`Use cases › Maximal stretch`), 13 (`Performance`), and 15 (`Out of scope`); Gap 3, 4, 6, 7, 10, 11, 12, and 14 on the probes identified below; no layer is N/A.
Fix: Replace the blank coverage table with those statuses and point each Gap row to its unresolved probe.

F2 `blocking` — Probe `3.3` has no single evidence command that fails when runtime artifacts linger: D23 is manual, while D27 detects temporary folders and declarations but not retained units or Hunt state.
Fix: Make one command exercise every end path and fail unless tracked units, Hunt seeds, visual work, temporary files, and persisted by-products match the retention table.

F3 `blocking` — Probe `4.1` is unresolved on D1 no-go: S-6’s two-metre fallback does not establish walkability for scheduled, Point, or AroundPlayer centres and therefore cannot satisfy the stated dry-ground outcome.
Fix: Decide either a fallback that proves every selected spawn point walkable or explicitly narrow the feature contract when collision data is unavailable.

F4 `blocking` — Probe `4.5` does not define how D7 computes “every CHAR_ prefab”: the catalog’s enumeration source, dynamically unavailable prefabs, boot-time visibility, and verifier are unspecified.
Fix: State the authoritative prefab enumeration, its timing and exclusions, and a test that fails when an eligible prefab is omitted.

F5 `blocking` — Probe `6.2` covers exceptions but not slow, rate-limited, or garbage responses for every dependency; notably malformed component values and hung or corrupt git/gh/tcli/session-tool results have no bounded behavior or common failing command.
Fix: Specify timeout and invalid-result behavior for each dependency class and provide one dependency-suite command that fails when any fallback or bound is removed.

F6 `blocking` — Probe `7.2` leaves the race between AroundPlayer selection and spawning undecided: the selected player can die, disconnect, enter PvP combat, or enter claimed territory while the wave is queued across ticks.
Fix: State whether eligibility is snapshotted or revalidated immediately before spawning, including the resulting skip/cancel behavior.

F7 `blocking` — Probe `10.1` is not enforced by D22 because removing `[Mutating]` from a write method removes it from the gateway checker’s universe, allowing the authorization control itself to disappear while `-AuthSuite` passes.
Fix: Enumerate mutation-capable symbols independently of their annotation and make `pwsh tools/preflight.ps1 -AuthSuite` fail for an unannotated or unauthorized write path.

F8 `blocking` — Probe `12.4` claims one evidence command per gating probe, but the table supplies none for the complete `3.3`, `6.2`, and `14.3` controls and collapses many logic checks into unnamed “three method” rows without spelling their actual empty inputs or outputs.
Fix: Add an explicit gating-probe-to-command matrix and concrete fail, silent, and empty fixtures for every introduced check.

F9 `blocking` — Probe `14.3` has no DoD evidence command for the 0.5.1→0.5.0 rollback: the Build plan mentions `rollback-gate`, but D5’s evidence runs only preflight and release verification.
Fix: Add `pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan event-spawns` to a D-item with an asserted failure when that rollback no longer initializes or restores compatibility.

F10 `blocking` — Probe `14.4` omits `tools/paths-manifest.txt` even though D27 requires this child to add its path declarations there; a post-build walk also cannot discover temporary paths created and removed earlier in a session.
Fix: Add the manifest itself to Paths walked and have the path command consume a per-step write trace so deleted intermediate paths are verified.

F11 `advisory` — Probe `11.4` says nothing activates without a new key, but 0.5.1 walkability automatically changes every legacy SpawnWaves definition; D19 and D23 do not evidence that activation.
Fix: State that walkability activates for all waves on 0.5.1, identify its observable wave-line signal, and list the cases that retain legacy placement through fail-open behavior.

F12 `advisory` — S-10 is not genuinely reversible: replacing independent chance rolls with weighted selection changes composition semantics, schema, authoring, templates, and tests; fortunately the current chance decision is already validated by S-1.
Fix: Mark S-10 validated and treat weighted selection solely as future scope.

F13 `advisory` — S-9’s fallback merely says ranges will change by amendment, without selecting replacement caps or a measurement threshold that triggers them.
Fix: Either retain the decided caps without a fallback label or state the measured criterion and exact alternate caps.

F14 `advisory` — D24 verifies the five-millisecond budget at 150 tracked units although the supported configured maximum is 500, leaving the maximal supported Hunt workload unmeasured under probe `13.1`.
Fix: Measure at 500 units or document the expected degradation and define a lower supported performance envelope.

F15 `advisory` — Minimal stretch says nothing persists beyond foundation unit rows, contradicting the Data table where the edited events.json definition and backup remain until changed by the admin.
Fix: Say explicitly that configuration and its rolling backup linger after one use while runtime wave state is removed.

F16 `advisory` — Build steps 3 and 4 both implement or verify D16 and D17, but step 4’s `Satisfies` line omits them; step 6 likewise reruns Epic D12/D23 without citing D23.
Fix: Add every actually satisfied or reverified D-item to the corresponding Build-plan step.

EARLIER: all resolved
7/15 layers · 39/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · consequence of F2-F16; Coverage pointers updated with D33 and D34
- F2 · accepted · +D33 (End paths empty the state): Logic WaveLifecycle over the ledger, the hunt seed set and the visual queue, with `dotnet test --filter EndPaths` failing when any end path leaves a unit, seed or visual; temporary files stay with D27
- F3 · accepted · the no-go fallback narrows the contract instead of approximating it: D1 and S-6 now say 0.5.1 is not released, D2-D5 are removed by amendment, 0.5.0 placement and the known-issue note stay, and the owner decides the next probe in plan mode
- F4 · accepted · D7 names the enumeration: every "CHAR_" key of PrefabCollectionSystem.SpawnableNameToPrefabGuidDictionary (the map SpawnTracker.Spawn resolves from), read once after GameDataInitialized, missing entities counted apart, built by Logic HidingIndex with HidingIndexTests failing on an omitted eligible prefab
- F5 · accepted · D21 decides garbage values where they are read (a guid without an entity cannot hide, an out-of-range territory block is ignored, a NaN or out-of-range player position makes the player ineligible) and states that every game call is synchronous in process, bounded by the tick and named by the slow-tick warning; -DependencySuite runs these cases; tooling is covered by the existing release tools under $ErrorActionPreference='Stop'
- F6 · accepted · D16 snapshots the pick when the wave is queued: the centre is fixed for the wave, later player changes do not move it, ring points are still checked against the wave's territory set; a fails-when case covers it
- F7 · accepted · +D34 (Entity writes stay in services): a preflight check over every entity write call, independent of annotations, failing outside the dispatched services and EntityExtensions.cs or in a non-private unmarked method, with fixtures bad, bad-new, bad-unmarked, good and empty
- F8 · accepted · Failure & observability gains the empty input and result of every Logic control and a gating-probe-to-command table (2.1, 3.3, 4.4, 6.2, 10.1, 10.3, 12.4, 14.3, 14.4)
- F9 · accepted · D5's evidence runs `pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.5.1 -Plan event-spawns` with its fails-when
- F10 · accepted · Paths walked adds tools/paths-manifest.txt, tools/data-inventory.json and the EntityWrites fixtures, and says paths created and removed inside a step are declared and checked by -DeclaredOf whether or not they still exist
- F11 · accepted · Design › UX 11.4 states walkability activates for every wave from 0.5.1 with the wave line as its signal, and the fail-open case that keeps 0.5.0 placement
- F12 · accepted · S-10 is validated by owner decision 1A (a spawn chance per unit); weighted selection is future scope
- F13 · accepted · S-9's fallback names the measured criterion and the replacement caps (hold distance minus 10 m for Hunt; 45 m for Guard)
- F14 · accepted · Performance states the supported envelope: the budget is promised and measured at the default caps; above them the work grows linearly and the slow-tick warning names the phase; the README says so
- F15 · accepted · Minimal stretch separates runtime state (cleared, D33) from configuration (events.json and its .bak, which linger by design)
- F16 · accepted · step 4 satisfies D16 and D17 as well; step 6 cites D23 for the Epic D12 pass line, and the work breakdown lists the extra steps

## Review 3 · 2026-09-28 · codex · plan uncommitted · plan 77439 B · 34 items · files 0 · e3b0c44298fc · prompt 2d3581540d11
F1 `advisory` — Blind re-score: Considered 1 (Purpose & typical use), 2 (Permissions), 4 (Business rules), 5 (Interfaces), 6 (External dependencies), 7 (States), 8–9 (Use cases), 11 (UX), 13 (Performance), and 15 (Out of scope); Gap 3 (`3.3`), 10 (`10.1`), 12 (`12.4`), and 14 (`14.3`, `14.4`); no layer qualifies as N/A.
Fix: Use those section pointers for the Considered layers and close the five probe failures below before claiming full coverage.

F2 `blocking` — Probe `3.3` is unanswered because `events.json.tmp` and other transient editor/release intermediates appear under Paths walked but not in the persistence inventory, and neither of its two cited commands alone fails when an artifact lacks an owner, retention period, or deletion rule.
Fix: Inventory every temporary/intermediate artifact with storage, owner, retention, and deletion, and provide one command that fails when any produced artifact is absent from that inventory or violates cleanup.

F3 `blocking` — Probe `10.1` lacks the required single evidence command: `-AuthSuite` checks command/gateway authorization while the separate full preflight supplies EntityWrites, so either half of the control can be removed while one cited command still passes.
Fix: Provide one authorization-suite command that runs command-role, indirect-caller, gateway, and entity-write-boundary checks and fails when any path loses its authorization control.

F4 `blocking` — Probe `10.3` is not fully controlled by D31: scanning token-shaped contents and forbidding `gh auth token` does not fail for code that writes or logs `$env:TCLI_AUTH_TOKEN` when the secret is absent during preflight.
Fix: Make the secrets command statically reject credential-environment reads flowing to files/logs and dynamically plant a sentinel credential whose appearance in every artifact and log surface fails the suite.

F5 `blocking` — Probe `12.4` is unanswered because its gating table names “`-SelfTest and ControlCaseTests`” rather than one exact command, and the abbreviated `dotnet test …` is not independently executable evidence for a stranger.
Fix: Add one exact wrapper command that runs all control selftests and fails on each planted failing case, unexpected report on the silent case, or pass-like empty-input output.

F6 `blocking` — Probe `14.3` lacks one rollback evidence command covering both release ranges; either cited invocation can pass while rollback of the other release is broken.
Fix: Add one exact two-release rollback command that exercises `v0.5.0..v0.5.1` and `v0.5.1..v0.6.0`, including written-data rollback, and fails if either range fails.

F7 `blocking` — Probe `14.4` is unanswered because `-Paths` inspects filesystem/git/remote state only after the build, so an undeclared path created and deleted successfully during a step is invisible; scenario: a release helper writes `%TEMP%\secret-stage` and cleans it before the walker runs.
Fix: Trace or instrument writes throughout every build, session, review, and release command, then make `-Paths -DeclaredOf event-spawns` fail for any observed path not declared in the manifest.

F8 `blocking` — D14 is unverifiable by its `manual` evidence because “the difference is explained” permits any boot count other than 36 without an objective criterion, so a stranger cannot determine whether the Hiding index is correct.
Fix: Require the record to enumerate the exact differing prefab names and classifications and compare them with an attached prefab-dump/hash or a command-produced diff that must be empty or explicitly approved.

F9 `blocking` — D24 is unverifiable by its `manual` evidence because recording ten timing lines and the largest average does not prove that no intervening timing or slow-tick line was omitted, so a stranger cannot verify the claimed continuous span.
Fix: Preserve the complete timestamp-bounded log span and use an exact command that fails unless it contains the required ten consecutive post-warm-up windows, qualifying tracked counts, averages below 5 ms, and zero slow-tick lines.

F10 `advisory` — Probe `4.5` is answered, but D34’s lexical EntityWrites set can miss semantically equivalent writes such as an alias, a split `GetBuffer` mutation, or an unlisted ECS write API; scenario: a new service uses such an API outside the dispatched boundary and the check stays green.
Fix: Derive the write set from a maintained authoritative API list or syntax analysis and add representative equivalent-write fixtures.

F11 `advisory` — Probe `7.2` decides separate Hunt-wave behavior but does not test the maximal concurrent case where two AroundPlayer waves choose the same eligible player while another admin stops one event; this is implementation work rather than a missing policy.
Fix: Add a concurrency fixture confirming independent fixed centres, caps, ledgers, hunt seeds, and cleanup for only the stopped event.

F12 `advisory` — Probe `9.1` states that work grows linearly above default caps but validates the latency promise only at 150 tracked units; scenario: the documented maximum of 500 units produces repeated 250 ms ticks while remaining within an advertised supported bound.
Fix: Either label 151–500 explicitly degraded/best-effort in the README or add a maximum-cap soak with a stated acceptable degradation threshold.

F13 `advisory` — S-6 is not cheaply reversible in plan terms: a no-go deletes four DoD items, cancels an advertised release, rewrites rollout and coverage, and requires another owner decision.
Fix: Label it as a gated scope branch rather than reversible, and require re-review of the amended DoD before continuing beyond the probe.

EARLIER: all resolved
11/15 layers · 44/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · consequence of F2-F13
- F2 · accepted · Design › Data adds events.json.tmp and state.json.tmp, the review prompts in %TEMP% and Claude's scratchpad, each with storage, owner, retention and deletion; D27's one command, `-Paths -DeclaredOf event-spawns`, fails on a missing data-inventory entry or empty field for any Data row or manifest `temp:`/`server:` glob, and on a leftover temporary folder
- F3 · accepted · -AuthSuite now also runs D34's EntityWrites check, so one command (`pwsh tools/preflight.ps1 -AuthSuite`) covers commands, admin list, gateway and entity writes; the gating table cites it alone
- F4 · accepted · D31: the Secrets check fails on any read of TCLI_AUTH_TOKEN in tracked code (new fixture Secrets/bad-envread), and the release step builds the zip with a sentinel TCLI_AUTH_TOKEN and fails if it appears in the zip, dist/, build/ or the build log
- F5 · accepted · the 12.4 row names `pwsh tools/preflight.ps1 -SelfTest`, which runs every fixture and the registered `dotnet test Nyarlathotep/Nyarlathotep.Tests` (ControlCaseTests included); every dotnet command is spelled in full
- F6 · accepted · D25 adds `pwsh tools/rollback-gate.ps1 -From v0.5.0 -To v0.6.0 -Plan event-spawns`, one run over both releases' range including 0.5.0 loading 0.6.0's written files; the 14.3 row cites it
- F7 · accepted · with a different control: run-time write tracing is not used; instead every write site is found statically: -Paths' unmarked-temp-root scan (event-library A18, fixture Paths/bad-tempvar) fails any tools/ line that takes the %TEMP% root without naming a declared nyar-<name> folder or carrying a '# nyar-temp:' registration, so the reviewer's scenario (a helper staging into an undeclared %TEMP% folder and cleaning it up) fails before it runs; the mod's own file writes stay fenced by the FileWrites check; D27's fails-when names it
- F8 · accepted · D14 compares the sorted in-game name list (verbose boot line) with the sorted CHAR_ files of the prefab dump whose SpawnBuffElement has a Hiding entry; the record holds both lists and their diff, which must be empty, and a non-empty diff fails the item until an amendment classifies each name
- F9 · accepted · D24 becomes a cmd: `pwsh tools/preflight.ps1 -TimingSpan <log copy> -MinTracked 140 -Windows 10` checks ten consecutive post-warm-up windows, the tracked counts and the absence of slow-tick lines in the span, with fixtures good, bad-avg, bad-slow, bad-gap and empty
- F10 · accepted · D34's write set adds every EntityManager Set/Add/Remove/Destroy/Instantiate/CreateEntity call and a DynamicBuffer local written later in the method, with fixtures bad-em and bad-split
- F11 · accepted · D33 adds two AroundPlayer Hunt events on one player: stopping one leaves the other's centre, ledger rows and seeds unchanged
- F12 · accepted · D26: the READMEs say the budget is promised at the default caps and raised caps (151-500 tracked) are best effort, watched by the slow-tick warning
- F13 · accepted · S-6 is relabelled validated by owner decision 3A as a gated scope branch: a no-go amendment removes D2-D5 and needs a fresh READY review before any build past step 1

## Review 4 · 2026-09-28 · codex · plan commit 24c122f · plan 82497 B · 34 items · files 0 · e3b0c44298fc · prompt ea24c81932f7
Reviewer: Codex CLI, read-only (`codex exec -s read-only`, Windows sandbox on); the prompt redacted of Steam IDs before sending (none present, the hash is the same). The first round after the owner's round-cap note (option A, through Review 6).

F1 [blocking] Probe 3.3 has two evidence commands, so neither single command fails when either persisted artifacts or runtime state cleanup is absent.
Fix: make one D-item command invoke both the inventory/path checks and EndPaths tests, failing unless all persistence and deletion rules pass.

F2 [blocking] Probe 4.5 is unanswered for D34’s “every entity write”: the syntactic pattern list does not state valid writes it misses; a `RefRW<T>.ValueRW` mutation can bypass EntityWrites while its command passes.
Fix: define the enforced write set and enumerate excluded valid forms, or extend the scanner and fixtures to cover every supported mutation API.

F3 [blocking] Probe 6.1 omits supported versions, quotas and costs for operational dependencies including git, gh, tcli, Codex and the session helpers.
Fix: state the supported version or version policy, quota/rate assumptions and cost for every external build, review and release dependency.

F4 [blocking] Probe 12.1 does not give an admin-visible outcome and next action for every D21 failure; for example, a HidingBuff failure silently degrades Ambush to an ordinary visible spawn except for a log line.
Fix: map each D21 failure class to its exact admin-visible message and prescribed recovery action.

F5 [blocking] Probe 12.4 is unanswered for the D1 and D14 go/no checks: their failing cases exist, but no silent input or empty input is defined, and `-SelfTest` cannot fail when those manual control cases are absent.
Fix: specify failing, silent and empty cases for both probes and add a single selftest command that fails when any required case is missing.

F6 [blocking] Probe 14.4 is not enforced for ephemeral paths created outside the PowerShell `%TEMP%` syntax scan; for example, `session-events.py` can use Python’s temporary-directory API, delete the directory, and leave nothing for the post-build walker to detect.
Fix: instrument filesystem writes or statically cover every language and temporary-path API used by build/session tooling, with an undeclared-create-and-delete fixture that makes D27’s command fail.

F7 [advisory] The 3.2 coverage pointer names wave messages, skips and wire behavior but omits material side effects such as stat buffs, drop-table clearing, aggro writes, hiding buffs, visual queues and file edits.
Fix: expand the pointer to D9, D11–D15, D18 and D21, or point to the complete Interfaces › Writes section.

F8 [advisory] S-7, S-9 and S-11 are labelled reversible even though changing territory classification, behavior ranges or accepted level semantics alters protection or configuration contracts and requires amendments plus renewed validation.
Fix: describe them as pre-release conditional decisions rather than cheap reversals, retaining their explicit fallback branches.

F9 [advisory] Concurrent territory ownership changes are intentionally observed only by the next wave, so units from an already-built territory set can enter land claimed while that wave is spawning.
Fix: record this as the explicit 7.2/2.3 policy and mention it in the admin documentation.

EARLIER: all resolved
10/15 layers · 43/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · D27's `pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns` also runs, through Invoke-ClassTests, the test classes tools/preflight-checks.json `dataTests` lists for the slug (EndPathTests of D33), failing on zero tests, a failure, or a missing or empty entry (fixture Paths/bad-datatests); the 3.3 gating row names that one command and D27 alone; built in step 3
- F2 · accepted · D34's pattern set adds `.ValueRW`, ComponentLookup, BufferLookup and ComponentDataFromEntity indexer assignments, EntityCommandBuffer and CommandBuffer writes, and SystemAPI.SetComponent, SetBuffer and SetComponentEnabled; excluded forms are none: `unsafe`, GetUnsafePtr, GetUnsafeReadOnlyPtr and UnsafeUtility fail the check outright; fixtures bad-refrw, bad-ecb, bad-lookup and bad-unsafe
- F3 · accepted · Interfaces › External gains "Build, review and release tooling (6.1)": git, gh, tcli, Codex CLI, Python, pwsh and the .NET SDK with the planned version, a floor, the version policy (recorded at each pre-audit), quota and rate limits, cost (none beyond the owner's subscriptions) and failure behaviour, the release tools' failure cases pointing at `-DependencySuite` category release-tools
- F4 · accepted · Failure & observability gains a table mapping every D21 failure class to what the admin sees and the next action; D30's degraded list adds "spawns: unit setup failing (<id>)", "spawns: hunt seed failing (<id>)", "spawns: ambush hide failing (<id>)" and "spawns: player query failing (<id>)", and HealthTests Spawns fails when any of the six classes' open streak gives no entry or its text differs from the table
- F5 · accepted · D1 and D14 state their failing, silent and empty cases (an incomplete record reads "incomplete", never go); D27's -SessionsOf gains the `probeRecords` check (Session 1's five walk lines and go/no-go line, Session 3's ambush probe and ambush diff lines) with fixtures SessionLogs/bad-probe and bad-probe-2, and `-SelfTest` runs them as plants
- F6 · rejected · the unmarked-temp-root scan already covers Python's temporary-directory APIs: tools/preflight.ps1's pattern (about lines 1068-1075) matches tempfile.gettempdir, mkdtemp, mkstemp, TemporaryDirectory and NamedTemporaryFile, os.environ and os.getenv TEMP, Node's process.env.TEMP and os.tmpdir, GetTempPath, GetTempFileName and New-TemporaryFile, and fixture Paths/bad-tempvar asserts each plant line through unmarked.txt (event-library A18, A24); every tool language used here (pwsh, Python, Node) is covered, and a tool line using any of them without a nyar-<name> literal or a registration fails D27; D27's fails-when now names these APIs
- F7 · accepted · the 3.2 pointer names D9, D11-D15, D18 and D21 beside D3, D16, D17 and D20
- F8 · accepted · S-7, S-9 and S-11 are described as pre-release conditional decisions (before 0.6.0 an amendment with its session or tests run again, after it an amendment and a release), their fallbacks kept
- F9 · accepted · D17 and Design › States › 7.2 state the policy (territory read once per wave; a claim made while a wave spawns is seen by the next wave), with the READMEs and the feature doc stating it at 0.6.0; D17's test fails when a map built for one wave is reused by the next

## Review 5 · 2026-09-28 · codex · plan commit d989523 · plan 90285 B · 34 items · files 0 · e3b0c44298fc · prompt 70ab4f94e0b8
Reviewer: Codex CLI, read-only (`codex exec -s read-only`, Windows sandbox on); the prompt held no Steam ID, so the redacted copy equals the source. The second round under the owner's round-cap note (option A, through Review 6).

Blind rescore:

1. Considered — `Purpose & typical use`.
2. Considered — `Design › Permissions`, D22/D23/D31.
3. Considered — `Design › Data`, D6/D27/D33.
4. Gap — probe 4.5.
5. Considered — `Interfaces › Internal`, D6/D20.
6. Considered — `Interfaces › External`, D1/D21/D27.
7. Gap — probe 7.3.
8. Considered — `Use cases › Minimal stretch`, D6/D23.
9. Considered — `Use cases › Maximal stretch`, D2/D24.
10. Considered — `Security`, D16/D22/D31, although D31’s evidence defect is separately blocking.
11. Considered — `Design › UX`, D18/D19/D32.
12. Gap — probe 12.4.
13. Considered — `Performance`, D2/D6/D24.
14. Gap — probe 14.4.
15. Considered — `Out of scope`.
No N/A claims were made. Every Build-plan step cites D-items, and every otherwise-Considered layer 2–14 maps to at least one D-item.

F1 `[blocking]` Hunt adds aggro entries but never decides what happens to a previously seeded target who dies, disconnects, leaves range, or enters PvP combat; probe 7.3 remains unanswered because stale target state during a live wave has no invalidation policy.  
Fix: State whether HuntAction removes its own stale AggroBuffer entries on the next seed tick or deliberately leaves them to the game, and make D13/D33 test that decision.

F2 `[blocking]` The 12.4 inventory does not give failing, silent, and empty fixtures for every introduced aggregate check—especially the enhanced `-Paths -DeclaredOf` and `-AuthSuite`; probe 12.4 remains unanswered because `-SelfTest` cannot demonstrate those aggregates reject empty input and stay silent on a valid input.  
Fix: Enumerate each introduced or extended check separately with three executable fixtures, register them under `-SelfTest`, and state the exact non-pass output for empty input.

F3 `[blocking]` D27’s path command observes surviving Git/generated/server paths and statically scans only temporary-root write sites, so a helper can create and delete an undeclared repository, AppData, or other non-TEMP path before the final walk without detection; probe 14.4 remains unanswered because its evidence command would still pass with that control absent.  
Fix: Make `pwsh tools/preflight.ps1 -Paths -DeclaredOf event-spawns` trace writes or statically enumerate all filesystem-write sites regardless of root, and add a fixture that writes then deletes an undeclared non-TEMP path.

F4 `[blocking]` D31 is unverifiable as written: its evidence scans credential patterns and SteamIDs but cannot establish that no log, record, artifact, or zip contains a player position, so a stranger cannot verify its personal-data claim by the stated `cmd`; probe 10.4 needs an evidence-backed definition of which outputs are checked for positions.  
Fix: Move the position claim to a typed-output/privacy test that fails when any relevant message, log, API, session, or artifact receives coordinates, or narrow D31 to the token and identifier claims its commands actually verify.

F5 `[blocking]` The 4.5 list omits universal claims such as D27’s “every path this child writes” and D31’s “no log line, message, record or artifact” and never defines a computation that can see cleaned non-TEMP writes or arbitrary position-bearing output; probe 4.5 remains unanswered because those “every/no X” sets are not computable by the stated tools.  
Fix: Add each universal set to Business rules 9 with its membership algorithm, known exclusions, review owner, and proof that the enumerator can see newly created, untracked, generated, and subsequently deleted members.

F6 `[advisory]` S-7, S-8, S-9, and S-11 are operationally amendable but not cheap reversals after 0.6.0: they require changed validation or game integration, repeated sessions, another review, and a forward release; S-8 can also disable stored definitions.  
Fix: Relabel them “pre-release conditional” or explicitly state that post-release fallback is a compatibility-affecting forward migration rather than a cheap reversal.

F7 `[advisory]` D11’s five-kill manual check is probabilistic: a correctly preserved low-probability drop table can yield no drops and fail the item, while its deterministic recipe test already establishes the control.  
Fix: Use a guaranteed-drop fixture/prefab or record the actual DropTableBuffer before and after setup; retain ordinary kills only as exploratory evidence.

F8 `[advisory]` D1’s record check validates line counts and the presence of `go|no-go`, but not that `go` agrees with dry=`free` and pond/river/cliff/building=`blocked`; an inconsistent manual record can pass `-SessionsOf`.  
Fix: Have the probe-record parser associate each labelled location with its expected result and recompute the only valid verdict.

EARLIER: all resolved
11/15 layers · 45/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · D13: on each 5 s tick HuntAction re-reads the eligible targets and removes the AggroBuffer entries it seeded for a player no longer a target (dead, disconnected, out of range, or ineligible by D16's territory and PvP-combat rule), never an entry the game added; with no eligible player it removes all its seeds and adds none (Logic HuntPlan.Diff); SpawningTests Hunt fails when a stale seeded entry is kept, a game-added entry is removed or an empty target list keeps a seed; the 7.3 pointer names D13
- F2 · accepted · the 12.4 table lists each introduced or extended check separately with its failing, silent and empty fixture and the exact non-pass output: the probe records (bad-probe, bad-probe-2, bad-probe-3, good-probe, empty-probe), the -DeclaredOf data tests (bad-datatests, Paths/good, empty-datatests), -AuthSuite's entity-writes part, its admin list and gateway parts (existing fixtures named, GatewayOnly/bad-new) and the dependency-suite category; every fixture is registered under -SelfTest in the step that adds its check
- F3 · rejected · owner decision docs/NYARLATHOTEP_DESIGN.md §9 D18 (event-library Review 11 F1, widened 2026-09-27 at Review 15 F2): a path created and deleted inside one step is outside the declared-paths check, the observation boundary is what a step leaves, and a write journal was declined; the plan now states it as validated assumption S-13 (14.4's pointer names it), and Business rules 9 names the exclusion and its owner (each step's /code-review and Codex cross-inspection)
- F4 · accepted · D31 is narrowed to what its commands verify: tokens and the sentinel over the Secrets check's set, SteamID digit runs and the owner's mail name over the tracked tree; the player-position claim stays with D16, whose PrivacyTests fail when a message builder, log line or api row takes a player's name or position; Security › 10.3 and 10.4 say so
- F5 · accepted · Business rules 9 adds "every path this child writes" (its membership algorithm, the S-13 exclusion and its owner) and the D31 and D16 sets (the Secrets check's set, the tracked-tree grep, and the call sites PrivacyTests enumerate); the 4.5 pointer names D27, D31 and D16
- F6 · accepted · S-8 joins S-7, S-9 and S-11 as a pre-release conditional decision; after 0.6.0 a change is a forward migration and a release, not a cheap reversal; a stored value outside a narrowed range disables its definition with the rule as its reason, never a crash (D6)
- F7 · accepted · D11's in-game evidence is the verbose line "drops <unit>: <n> before, <m> after setup" for one unit of a loot true and one of a loot false wave; kills are exploratory only
- F8 · accepted · the probe-record parser maps each labelled reading (dry, pond, river, cliff, building) to its expected result, recomputes the verdict and fails a go/no-go line that disagrees (fixture SessionLogs/bad-probe-3)

## Review 6 · 2026-09-28 · codex · plan commit 06e38fa · plan 95005 B · 34 items · files 0 · e3b0c44298fc · prompt 803cc7847dd9
Reviewer: Codex CLI, read-only (`codex exec -s read-only`, Windows sandbox on); the prompt held no Steam ID. The third round under the owner's round-cap note (option A, through Review 6); the owner's next note sets Review 7 to a fresh-context subagent. "EARLIER: unresolved F3" names Review 5 F3, rejected by design §9 D18 (S-13), as F5 here.

F1 `[blocking]` Probe `2.1` is unanswered by its claimed control: `-AuthSuite` checks command metadata and mutation dispatch, but cannot fail when the operator, build agent, owner-only publisher, scheduler, or an inherited command such as `event start` gains unauthorized capability.
Fix: Extend one named authorization command to enumerate every reachable entry point and actor/action matrix, with a failing fixture for each unauthorized actor—including a non-admin invoking inherited activation paths.

F2 `[blocking]` Probe `3.3` is incomplete: the data inventory omits produced runtime artifacts including the visual queue, unit marker/stat components, seeded `AggroBuffer` entries, and health/failure-streak state, so D27 cannot fail when their ownership, retention, or deletion is undefined.
Fix: Add every produced runtime artifact to Design › Data and `data-inventory.json`, naming storage, owner, retention, deletion and copies, and make `-Paths -DeclaredOf event-spawns` exercise their deletion through D33.

F3 `[blocking]` Probe `6.2` is unanswered by `-DependencySuite`: its stated fixtures cover game collaborators and two release tools, but not slow, down, rate-limited, garbage or ambiguous-success behavior for git, gh, tcli, Codex, Python, pwsh, .NET and session helpers; for example, `gh release create` may succeed remotely and then time out despite the assertion that failure occurs before creation.
Fix: Make one dependency-suite command inject and reconcile each applicable failure mode, including querying remote state after an ambiguous gh response before retrying or aborting.

F4 `[blocking]` Probe `12.4` lacks a faithful failing fixture for D31’s sentinel control: `Secrets/bad-envread` writes the environment variable from source, but does not plant the state the real run produces—a sentinel leaked into a zip, `dist/`, `build/`, or the build log.
Fix: Add registered selftest fixtures containing the sentinel in each scanned artifact form, plus clean and empty fixtures, and require `pwsh tools/preflight.ps1 -SelfTest` to reject them.

F5 `[blocking]` Probe `14.4` remains unanswered because S-13 explicitly excludes paths created and deleted within a step, while the claimed command observes post-build state and statically scans only selected write mechanisms; a transient repository, server, or AppData write can therefore escape `-Paths -DeclaredOf`.
Fix: Require a write journal/filesystem trace covering every build step, or forbid unobservable transient writes and make the paths command fail on every write API not statically classifiable.

F6 `[blocking]` D32 is unverifiable for probes `11.2` and `11.3`: pasted chat text does not let a stranger verify actual in-game wrapping, truncation, colour dependence, keyboard rendering, or the 480-byte limit.
Fix: Require session evidence containing byte counts and screenshots or recorded observations of the rendered success, refusal and validation lines at the relevant viewport.

F7 `[advisory]` Probe `7.2` states the steady-state Hunt policy but not the concurrent partial-write case: a unit can despawn, or eligibility can change, between `HuntPlan.Diff` and successive `AggroBuffer` mutations, leaving seed bookkeeping inconsistent after an exception.
Fix: Define mutation reconciliation after partial application and add a race fixture that removes a unit or target between planning and writing.

F8 `[advisory]` Probe `9.1` calls raised caps of 500 tracked units “best effort” without saying what correctness remains guaranteed when 2,500 Hunt target relationships are processed and ticks exceed budget.
Fix: State that only latency degrades at raised caps—or define shedding behavior—and record one maximal-cap observation confirming lifecycle cleanup and bounded queues.

F9 `[advisory]` S-7, S-8, S-9 and S-11 are mislabeled `reversible`: each changes persisted-definition validity or released behavior and explicitly requires an amendment and forward release after publication, so reversal is not cheap.
Fix: Relabel them validated conditional decisions with pre-release probe branches and post-release migration policies.

EARLIER: unresolved F3
10/15 layers · 44/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · narrowly: -AuthSuite's command inventory enumerates every reachable `.nyar` command from the [Command] attributes, inherited ones included (event start, stop, enable, disable, set, copy, delete, template use, spawn), and fails when a command that can activate or change a spawn event is not adminOnly or reaches a [Mutating] method outside Gateway.Run, SpawnTracker or EventRuntime (fixture AuthSuite/bad-inherited); Design › Permissions names the actors and the actor and action matrix, and states that build agents and the publisher have no chat identity or in-game capability and reach the server only as the operator
- F2 · accepted · Design › Data adds the runtime rows (seeded AggroBuffer entries, visual queue, marker buff and stat modifiers, player-pick snapshot, health and failure-streak state) with storage, owner, retention and deletion, none persisted or copied; step 3 adds their tools/data-inventory.json entries, so D27's data-inventory check fails when a row lacks one, and D33's EndPaths tests exercise their deletion
- F3 · accepted · narrowly: after a failed or ambiguous `gh release create` the release step runs `gh release view <tag>` and retries only when the release is absent, never creating twice (-DependencySuite release-tools case gh-ambiguous); failure injection for git, Codex, Python, pwsh and .NET is not planned: they run only at build and review time and fail closed under $ErrorActionPreference Stop, and a failed or capacity-limited Codex run is retried and never counted (Interfaces › External, tooling block)
- F4 · accepted · Secrets fixtures bad-sentinel-zip, bad-sentinel-dist, bad-sentinel-build and bad-sentinel-log plant the sentinel in each scanned form and must fail, good-sentinel is clean and must pass, empty-sentinel prints "secrets: nothing scanned" as a failure; all registered under -SelfTest (D31, the 12.4 table, step 3)
- F5 · rejected · owner decision docs/NYARLATHOTEP_DESIGN.md §9 D18 (event-library Review 11 F1, widened 2026-09-27 at Review 15 F2), stated in this plan as S-13: a path created and deleted inside one step is outside the declared-paths check; the observation boundary is what a step leaves, and a write journal or filesystem trace was declined; the third time this finding is raised (Review 5 F3, Review 6 F5)
- F6 · accepted · narrowly: D32's evidence is the owner's recorded observation per new line (whole, not cut, readable without colour; a screenshot optional) and a unit test, CommandArgTests ChatBytes, that computes each line's byte count from its template at its fields' maximum lengths and fails above 480 bytes
- F7 · accepted · D13: after a partial write HuntAction rebuilds its seed record, keyed by unit entity and target, from the unit's AggroBuffer on the next tick, and skips a unit or target that no longer Exists; SpawningTests Hunt gains the race fixture (a target removed between Diff and write)
- F8 · accepted · Performance › Supported envelope: at raised caps only latency degrades (the per-tick budgets stay, queues are bounded by the caps, lifecycle cleanup is unchanged, D33); one maximal-cap observation may be recorded in Session 4 as exploratory evidence
- F9 · accepted · for S-7 and S-9, which have sources (KindredCommands' CastleTerritoryService.cs; RESEARCH_NOTES spike S1 and TideOfWar), now validated conditional decisions with a pre-release probe branch and a post-release forward migration; S-8 and S-11 stay reversible because no owner decision or measurement is their source, which the grammar requires of validated, and each states its post-release forward-migration policy

## Review 7 · 2026-09-28 · subagent · plan commit 6514509 · plan 100703 B · 34 items · files 0 · e3b0c44298fc · prompt 263d02a1197b
Reviewer: a fresh-context general-purpose subagent (never a fork), read-only, given the score-redacted plan and no prior review, under the owner's option A after Review 6 (Codex's REVISE of Review 6 stays on record beside this one). It read tools/rollback-gate.ps1, tools/preflight.ps1 and tools/preflight-checks.json and the prefab dump to test the plan's claims.

F1 `[blocking]` The rollback evidence for probes 14.3 and 12.4 cannot pass as written: `rollback-gate.ps1 -Plan` runs four parts and prints "rollback gate: 4/4", but D5 and D25 expect 3/3; the routes check (Test-CheckRollbackRoutes) accepts exactly one `-Plan event-spawns` range while the plan names three, and reads bullets named "On a server", "Published release" and "Commit range", which the plan spells "On a server, 0.6.0 → 0.5.1", "Published releases" and "Commit ranges", with no "install the 0.5.0 DLL" route.
Fix: declare one range (v0.5.0..v0.6.0) with the five bullets spelled as the check reads them; run 0.5.1's gate without -Plan (3/3) and expect 4/4 for the child's range; or extend the check to several ranges.

F2 `[advisory]` D21 and the 6.2 row run `-DependencySuite` with no slug, and dependencySuites has no event-spawns entry.
Fix: `-DependencySuite event-spawns` and a dependencySuites.event-spawns entry (DependencyFailureTests Spawns plus release-tools) in step 3 or 4.

F3 `[advisory]` Paths walked is incomplete: step 3 lists no tools/ path though it adds checks, fixtures and inventory rows; step 2 lists the EntityWrites fixtures a step early; HidingIndexTests, EndPath and WavePrecedence test files and Services/HealthMonitor.cs are missing; "steps 2 and 6 add inventory rows" contradicts step 3.
Fix: correct the per-step lists.

F4 `[advisory]` D14's "36: 32 Bandit, 4 Blackfang" is wrong: 30 Bandit and 6 Blackfang (DartFlinger, Lurker, Striker, each with a _Servant variant).
Fix: correct the breakdown.

F5 `[advisory]` D13 rebuilds the seed record from the AggroBuffer, which cannot tell a seed from a game entry for the same unit and player, contradicting "never an entry the game added".
Fix: keep the seed record authoritative; drop only record entries missing from the buffer; never adopt buffer entries.

F6 `[advisory]` An AroundPlayer Hunt wave can have maxDist up to 80 m while Hunt range is at most 60 m, so the picked player may be out of range.
Fix: validate maxDist ≤ range for Hunt, or state it is intended.

F7 `[advisory]` Ambush with spawnVisual true is neither refused nor tested; the visual buff may reveal the hidden unit.
Fix: refuse the combination in D6, or probe it in D14.

F8 `[advisory]` Step 2 claims D22 and D31 though their evidence is built in steps 3-4.
Fix: cite them at a later step as well and mark step 2 partial.

F9 `[advisory]` D24 requires a player within Hunt range throughout, but -TimingSpan reads only timing and health lines; an unattended player facing about 140 hunters will likely die.
Fix: carry a hunt-target count the span check requires, or state how the player survives.

F10 `[advisory]` D5's "a README still carries the pond note" is tied to no check.
Fix: name the check or move the condition to a manual line.

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · the first option: Rollout › Rollback declares one range, v0.5.0..v0.6.0, with the five bullets spelled as Test-CheckRollbackRoutes reads them ("On a server" now names the 0.5.0 DLL route, with 0.5.1 as the same case); D5 and step 2 run `rollback-gate.ps1 -From v0.5.0 -To v0.5.1` without -Plan (3/3); D25 and step 6 run v0.5.1..v0.6.0 without -Plan (3/3), then `-From v0.5.0 -To v0.6.0 -Plan event-spawns` (4/4). Checked against the real check: `pwsh tools/preflight.ps1 -RollbackOf event-spawns -From v0.5.0 -To v0.6.0` → "rollback routes: event-spawns 5/5"
- F2 · accepted · D21, the 6.2 row and Interfaces name `-DependencySuite event-spawns`; step 3 adds the dependencySuites.event-spawns entry (DependencyFailureTests Spawns categories and release-tools with gh-ambiguous)
- F3 · accepted · step 2 lists its own tools/ paths (DebugCommands and Changelogs/bad-knownissue fixtures); step 3 lists preflight.ps1, preflight-checks.json, data-inventory.json, the fixtures folders, HidingIndexTests, EndPathTests, WavePrecedenceTests and Services/HealthMonitor.cs; the inventory sentence names step 3
- F4 · accepted · 30 Bandit, 6 Blackfang, with the three Blackfang names
- F5 · accepted · the seed record stays authoritative; after a partial write HuntAction drops only record entries missing from the buffer and never adopts one (this replaces Review 6 F7's rebuild)
- F6 · accepted · D6 and Business rules 2: with behaviour Hunt an AroundPlayer maxDist must be at most the Hunt range; D6's fails-when case "an AroundPlayer Hunt with maxDist 70 and range 60"
- F7 · accepted · D6 and Business rules 2 refuse spawnVisual true with Ambush; D6's fails-when case "an Ambush with spawnVisual true"
- F8 · accepted · step 2 satisfies D22 and D31 in part; step 4 lists them as well
- F9 · accepted · with TimingLog on HuntAction logs "hunt targets: <n>" once per timing window; -TimingSpan -MinTargets 1 fails a window without a line of at least 1 (fixture bad-notarget); a span in which the player dies fails and is run again
- F10 · accepted · the Changelogs check forbids the 0.5.0 known-issue text in both READMEs from 0.5.1 (fixture Changelogs/bad-knownissue), named in D5 and step 2

## Review 8 · 2026-09-28 · subagent · plan commit 26554a8 · plan 103818 B · 34 items · files 0 · e3b0c44298fc · prompt b9a836dda527
Reviewer: a fresh-context general-purpose subagent (never a fork), read-only, under the owner's round-cap note after Review 7 (through Review 9). It checked the plan's claims against tools/preflight.ps1, tools/preflight-checks.json, the mod's sources and Reference Data/prefab_names.tsv.

F1 `[blocking]` 10.1 (also 12.4): D34's EntityWrites check would fail on today's tree, so its evidence cannot pass: `.Write(` matches file and editor writes (Logic/DataStore.cs `fs.Write`, Logic/Authoring.cs `editor.Write`, test files) outside $DispatchedServices; "a call whose name begins CreateEntity" matches the read-only CreateEntityQuery in Services/TriggerBus.cs; EmpowerAction's nested Ops methods write entities without [Mutating].
Fix: state the scan policy (files scanned, which `.Write(` receivers count, CreateEntityQuery excluded as a read, nested-class members), and add a real-tree pass case.

F2 `[blocking]` 3.3 (also 12.4): tools/preflight-checks.json `dataTables` lists only raphael-api-core, faction-empowerment and event-library, and `snapshotSessions` only event-library, so D27's data-inventory and snapshot conditions cannot fail for this child; step 3 adds inventory entries for the runtime rows only.
Fix: step 3 adds docs/dod/event-spawns.md to dataTables with an entry for every Data row and "event-spawns": 1 to snapshotSessions; D27's expected line includes the snapshots part.

F3 `[advisory]` 3.1 / 11.2: `.nyar debug walk` collides with the existing `debug` command (Commands/SpawnCommands.cs:67), and Commands/DebugCommands.cs does not exist.
Fix: add `walk` as a verb of the existing Debug method and point the check and fixtures at SpawnCommands.cs.

F4 `[advisory]` 6.1: `InCombatBuff_PvP` is not in the prefab dump; the PvP combat buff is Buff_InCombat_PvPVampire (697095869).
Fix: name it in D16 and use its guid in PlayerPick's fixture.

F5 `[advisory]` 3.3: Design › Data names the inventory fields "storage, owner, retention, deletion, copies"; the check requires location, owner, retention, deletion, singleCopy.
Fix: use the check's field names.

F6 `[advisory]` 12.4: the checks table is broken by a blank line before the EntityWrites row and a paragraph between the DependencySuite and EndPaths rows.
Fix: move the paragraph below the table and remove the blank line.

F7 `[advisory]` 4.1 / 6.2: D3 checks every ring point at the centre's height level; on terraced ground a point on another level may read wrong.
Fix: a Session 1 reading at a height-level change, and state which height level IsFree takes.

F8 `[advisory]` 6.1: D12 assumes the game keeps AggroConsumer.PreCombatPosition as written.
Fix: a probe branch for the case where the game overwrites it on combat entry.

F9 `[advisory]` 2.1 / 9.2: PlayerPick and HuntPlan do not say whether admins (invisible, spectating) are eligible.
Fix: state it.

EARLIER: all resolved
13/15 layers · 47/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · D34 states the scan policy: the .cs files of Nyarlathotep/Nyarlathotep/ outside Logic/; Logic/ and Nyarlathotep.Tests/ are compiled into the test project, which references no game assembly, so they cannot write an entity, and the check fails when a Logic/ file has a `using` of Unity.*, ProjectM* or Stunlock* (fixture bad-logicusing); `EntityManager.` matches CreateEntity exactly, not CreateEntityQuery; a method of a private nested class (EmpowerAction.Ops) counts as private; the real tree at the step 3 commit must pass
- F2 · accepted · step 3 adds docs/dod/event-spawns.md to `dataTables`, `"event-spawns": 1` to `snapshotSessions`, and an inventory entry for every Data row; D27's -SessionsOf line gains "snapshots <n>/<n> from session 1"
- F3 · accepted · `walk` becomes a verb of the existing `debug` command in Commands/SpawnCommands.cs; the check's fixtures copy that file; Paths walked updated
- F4 · accepted · Buff_InCombat_PvPVampire (697095869) in D16 and Interfaces, source prefab_names.tsv
- F5 · accepted · the check's field names
- F6 · accepted · the table is one table; the paragraph follows it
- F7 · accepted · IsFree takes the centre's height level and a tile not grounded at that level (TileWorld.GetIsGrounded) counts as blocked; Session 1 adds a sixth reading, ledge, outside the verdict
- F8 · accepted · D12 gains the probe branch: if PreCombatPosition is overwritten on combat entry, a discovered amendment before step 5 rewrites it every 5 s or removes Guard, the owner deciding in plan mode
- F9 · accepted · admins are eligible like any player; an admin who does not want waves stops the event

## Review 9 · 2026-09-28 · subagent · plan commit 97a9026 · plan 106614 B · 34 items · files 0 · e3b0c44298fc · prompt 678870300484
Reviewer: a fresh-context general-purpose subagent (never a fork), read-only, the last round under the owner's round-cap note after Review 7. It checked the plan against tools/preflight.ps1, the test project and the mod's sources.

F1 `[blocking]` 6.2 (also 12.4): -DependencySuite requires event-library's fixed nine categories for every slug ($script:DependencyCategories, preflight.ps1:2115, Get-DependencyTableProblems :2127, the run loop :2339, Test-CheckDependencySuite :2156), so an event-spawns entry with Spawns categories fails "category events-write missing", and copying event-library's categories would run no Spawns control.
Fix: read the required categories per slug from dependencySuites.<slug>, name event-spawns' categories, add the preflight change and a failing fixture to step 3.

F2 `[blocking]` 12.4 (D22): ControlCaseTests reads only event-library.md and its ids; ControlCases keys rows by bare D-id, so event-spawns rows collide; the csproj copies only event-library.md; D11, D12 and D13's evidence does not contain a literal "(fails when:".
Fix: key rows by slug and D-id, read each plan, copy event-spawns.md, reword D11-D13, add the csproj to Paths walked.

F3 `[blocking]` 6.1: D3 and D10 read positions, SpellPower, MovementSpeed and attack speeds from `.nyar debug here`, which prints none of them (SpawnTracker.cs:277-300, AdminLines.DebugUnit).
Fix: extend DebugHere/AdminLines.DebugUnit within D32's 480 bytes, or name another command; add the files to Paths walked.

F4 `[advisory]` 10.1 / 12.4: D34's split-statement rule matches only a `DynamicBuffer<`-typed local, while the real tree writes `var mods = …GetBuffer<…>(…)`.
Fix: match any local assigned from GetBuffer< or ReadBuffer; bad-split in the real spelling.

F5 `[advisory]` 12.4 / 13.1: TickTimer's window is one wall-clock minute, not 60 ticks.
Fix: emit "hunt targets" with TickTimer's line and fix the wording.

F6 `[advisory]` 14.3: v0.5.0..v0.6.0 holds commits that are not this child's (event-library's close records, the release-verify fixes); a revert would undo them.
Fix: name them and re-apply them after the revert.

F7 `[advisory]` 14.3 / 3.4: 0.5.0 reports a per-unit chance as "unknown field action.units.chance", not among D25's accepted reasons.
Fix: accept it and add a chance-only selftest pair.

F8 `[advisory]` 6.1 / 4.1: foundation's Regroup rewrites PreCombatPosition for a unit landed on another level; D12 does not say so.
Fix: a regrouped Guard unit's home is its regroup point; cover it in the Behaviour test.

F9 `[advisory]` 4.4 / 13.1: D13 drops seeds for territory and PvP-combat ineligibility, but HuntPlan.Targets filters only online, alive and range, and TerritoryMap is built per wave.
Fix: say which territory set the tick uses; add in-territory and PvP-combat cases.

EARLIER: all resolved
13/15 layers · 47/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · step 3 changes -DependencySuite and Test-CheckDependencySuite to read required categories per slug from dependencySuites.<slug>; event-spawns' categories are walk-check, territory, hunt-seed, ambush-hide, player-query, unit-recipe and release-tools; fixture DependencySuite/bad-spawns-missing
- F2 · accepted · ControlCases rows keyed by slug and D-id, ControlCaseTests reads each listed plan, the csproj copies event-spawns.md (Paths walked step 3); D11 and D12's evidence reworded so "(fails when:" is literal (D13's already was)
- F3 · accepted · step 4 extends AdminLines.DebugUnit and SpawnTracker.DebugHere with position, SpellPower, MovementSpeed and the primary attack speed, bound by D32's 480 bytes with an AdminLines test; D3 and D10 name it; Paths walked step 4
- F4 · accepted · any local assigned from GetBuffer< or ReadBuffer, `var` included; bad-split written as the real spelling
- F5 · accepted · "hunt targets" is emitted with TickTimer's line, a one-minute wall-clock window
- F6 · accepted · Rollback › In the repository names the shared commits and re-applies them with git cherry-pick after a revert
- F7 · accepted · "unknown field action.units.chance" accepted; a chance-only selftest case
- F8 · accepted · a regrouped Guard unit's home is its regroup point; a Behaviour fails-when case
- F9 · accepted · HuntPlan.Targets takes the in-territory and PvP-combat flags, the territory flag read each tick from the wave's TerritoryMap; Hunt fails-when cases

## Review 10 · 2026-09-28 · subagent · plan commit afd181a · plan 97605 B · 28 items · files 0 · e3b0c44298fc · prompt f8e328e1f474
Reviewer: a fresh-context general-purpose subagent (never a fork), read-only, the first round after the walkable-spawns split (owner option A, Epic A26). It checked the plan against tools/preflight.ps1, the test project and the mod's sources.

F1 `[blocking]` 10.1 (also 12.4): D34's "the real tree at the step 1 commit must pass" fails: SpawnTracker.Tick (Services/SpawnTracker.cs:155, internal static, not [Mutating]) calls DestroySafe (:211), a non-private writing method of a dispatched service. Marking it [Mutating] breaks the gateway check (its caller is EventScheduler.cs:52), and moving the write into a private helper is an escape route the check does not see.
Fix: (a) make the tick and boot entry points [Mutating] and let the gateway check accept their scheduler, Core and Patches callers as the System actor, and/or (b) treat a non-private method calling a private writing method as a writer; add fixture bad-privatehelper.

F2 `[advisory]` 6.2 / 12.4: step 1 adds the dependencySuites.event-spawns entry with its test rows, but DependencyFailureTests Spawns arrive in step 2, so step 1's preflight fails.
Fix: add the entry and its test rows in step 2; step 1 keeps only the per-slug change and fixture bad-spawns-missing.

F3 `[advisory]` 14.4: Paths walked omits tools/paths-manifest.txt (steps 1 and 4), the audit and feature doc under steps 1-4, and the six surface paths at step 4.
Fix: declare them.

F4 `[advisory]` 4.2 / 7.2: D13 can seed a player the AggroBuffer already holds as a game entry, then remove the game's entry as its own.
Fix: HuntAction never seeds a player already in the buffer and removes a seed only while exactly its own entry remains; fails-when "a player already in the buffer is seeded".

F5 `[advisory]` 9.1 / 13.1: D24's span may never finish while 150 Hunt units attack the owner's character.
Fix: allow admin invulnerability during the span, recorded in Session 2.

F6 `[advisory]` 8.1 / 13.2: D23's 30-unit wave exceeds the default MaxUnitsPerWave 20.
Fix: "30 units over two waves at default caps".

F7 `[advisory]` 12.3: step 1's "HealthMonitor.Degraded adds SpawnHealth.Entries" re-wires what walkable-spawns already wires.
Fix: "SpawnHealth gains the event-spawns entries (D30); the HealthMonitor wiring is walkable-spawns'".

Confirmed: the 36 hiding prefabs (30 Bandit, 6 Blackfang); guids -133411573 and 697095869; KindredCommands' territory conversion; the rollback gate; every Review 9 fix handled.

EARLIER: all resolved
14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · both parts: (a) SpawnTracker.Tick, EventRuntime.Tick, EmpowerAction's tick and the boot marker sweep (step 1) and HuntAction's tick (step 2) become [Mutating]; the gateway check accepts EventScheduler.cs, Core.cs and Patches/ as System-actor callers, fixture GatewayOnly/bad-tickcaller (D22, Security 10.1); (b) EntityWrites treats a non-private method calling a private writing method of the same class as a writer, fixture bad-privatehelper must fail; D34's real-tree list names the ticks marked by D22
- F2 · accepted · the dependencySuites.event-spawns entry and its categories move to step 2 with DependencyFailureTests Spawns; step 1 keeps the per-slug -DependencySuite change and fixture bad-spawns-missing (D21, Build plan)
- F3 · accepted · Paths walked declares tools/paths-manifest.txt at steps 1 and 4, the audit and feature doc under steps 1-4, and the six surface paths at step 4
- F4 · accepted · D13: HuntAction never seeds a player the AggroBuffer already holds and removes a seed only while exactly its own entry remains; fails-when "a player already in the buffer is seeded"
- F5 · accepted · D24 and step 3: the owner's character may use admin invulnerability during the span, recorded in Session 2
- F6 · accepted · D23: a modified Hunt event of 30 units over two waves at the default caps (MaxUnitsPerWave 20)
- F7 · accepted · step 1: "SpawnHealth gains the event-spawns entries (D30); the HealthMonitor wiring is walkable-spawns'"; HealthMonitor.cs leaves step 1's Paths walked
- Trim (owner Decision 3, plan mode 2026-09-28): D7, D12, D14 and D15 with their rules, fields, tests, fixtures, session steps and rows move to a later child spawn-extras; the release is 0.7.0 after regions 0.6.0 (S-14, S-15)

## Review 11 · 2026-09-28 · subagent · plan commit 0f45f53 · plan 94189 B · 24 items · files 0 · e3b0c44298fc · prompt 539d36cb5636
Reviewer: a fresh-context general-purpose subagent (never a fork), read-only, the last round under the owner's round-cap note (through Review 11). It verified the plan against tools/preflight.ps1, tools/preflight-checks.json, Services/SpawnTracker.cs, Services/EventScheduler.cs, Core.cs, Logic/Engine.cs and KindredCommands' territory conversion.

F1 `[advisory]` 6.2: D21 and External › Build tooling cite a release-tools "case gh-ambiguous" in `-DependencySuite event-spawns`; no tool implements it (the release-tools row is `kind: selftests` over release-verify and repo-rollback-drill, and no script in tools/ runs `gh release create`).
Fix: add a small release-publish script with a selftest stubbing `gh`, or drop the "case" wording and keep `view` before any retry as a recorded procedure of the release step, as walkable-spawns D12 does.

F2 `[advisory]` 10.1: Test-CheckGatewayOnly enumerates only Commands/, Patches/ and Services/ (tools/preflight.ps1:1642), so "the gateway check accepts callers in … Core.cs" is empty and a stray [Mutating] call from Plugin.cs or Config/ would pass.
Fix: widen the enumeration to every non-Logic .cs file, allow Core.cs only as a System-actor caller, and add fixture GatewayOnly/bad-rootcaller.

F3 `[advisory]` 12.4: the Paths fixture names contradict each other (D27's bad-datatests is the empty entry; the 12.4 table and step 1 use bad-datatests for a planted failing test and empty-datatests for the empty entry), and Invoke-ClassTests always runs the real test project, so a fixture cannot plant a failing test class.
Fix: one naming across D27, the table and step 1; the failing case names a class that runs zero tests ("tests: <class> ran 0 tests").

F4 `[advisory]` 12.4: D24 lists fixture TimingSpan/bad-notarget but the check table's TimingSpan row lists only bad-avg, bad-slow and bad-gap.
Fix: add bad-notarget (a window without a "hunt targets: n ≥ 1" line) to that row.

F5 `[advisory]` 12.2: D11's manual evidence relies on the verbose line "drops <unit>: <n> before, <m> after setup", which does not exist and which step 2 does not add.
Fix: step 2 adds that VerboseLogging line in SpawnTracker.Prepare next to the DropTableBuffer clear, and Logs (12.2) names it.

F6 `[advisory]` 4.2, 7.2: D13's seed record cannot tell our seed from a game entry for the same player when the game drops our seed and adds its own between two ticks; HuntAction would remove the game's entry later.
Fix: an entry whose game-maintained fields changed since HuntAction wrote it is the game's and its record is dropped; a matching race fixture in SpawningTests Hunt.

F7 `[advisory]` 5.1: D34's real-tree list names "DataStore.cs fs.Write", but DataStore.cs is in Logic/, outside D34's own scan.
Fix: drop DataStore.cs from the list or say it is outside the scan.

EARLIER: all resolved (Review 10 F1-F7 reflected in the plan text)
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · applied as an amendment when the build starts (the reviewed revision stays frozen until approve; the child is built after raphael-api-admin and regions)
- F2 · accepted · applied as an amendment when the build starts
- F3 · accepted · applied as an amendment when the build starts
- F4 · accepted · applied as an amendment when the build starts
- F5 · accepted · applied as an amendment when the build starts
- F6 · accepted · applied as an amendment when the build starts
- F7 · accepted · applied as an amendment when the build starts

## Review 12 · 2026-09-29 · subagent · plan commit 0bb3a57 · plan 131451 B · 24 items · files 0 · e3b0c44298fc · prompt 7a26c311ae6b · scope A1,A2,A3,A4,A5,A6,A7,A8,A9
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A1-A9 (design §9 D26); the prompt held no Steam ID. It checked A1 (no tools/ script runs `gh release create`; release-tools is kind: selftests), A5 (SpawnTracker.Prepare clears DropTableBuffer), A7 (DataStore.cs is in Logic/), A8 (SpawnPoints.Angles is 12; design §9 D27) and A9 (Wire.Api is 5) against the repository.

F1 · advisory · 10.1, 12.4 · A2: Test-CheckGatewayOnly matches callers by bare method name; widened to every non-Logic .cs file it sees EntityExtensions.cs's `Write<T>` (colliding with [Mutating] Persistence.Write), and D22's [Mutating] SpawnTracker.Tick and EventRuntime.Tick collide with HealthMonitor.Tick and TriggerBus.Tick, so the real tree fails. Fix: match a [Mutating] name only through its declaring class, or exempt EntityExtensions.cs; record the choice as an amendment.

F2 · advisory · 6.2, 12.1 · A8: D16 does not say what a regional AroundPlayer does when the region index is unavailable or `regionOf` throws; ScopeCheck's fail-closed rule would report the misleading "no eligible player". Fix: state the fail-closed rule, the reason shown, a throwing `regionOf` as D21's player-query failure, and a fixture.

F3 · advisory · 4.2 · A6: "fields differ from what HuntAction wrote" does not name the fields (Entity, DamageValue, Weight, RESEARCH_NOTES S1) nor whether the game rewrites an untouched entry each update; if it does, every seed stops being HuntAction's after one tick and ineligible targets keep their seeds. Fix: name the fields and add a Session 1 observation that an unengaged seed keeps them over two 5 s ticks; a no-go is a discovered amendment.

F4 · advisory · 4.1 · A8: an out-of-scope centre is retried at other angles but a claimed centre is skipped without retry; defined and testable, but asymmetric. Fix: state the asymmetry as intended or let the retry skip claimed centres too.

F5 · advisory · 12.4 · A3: the 12.4 table's silent fixture Paths/good has no `dataTests` entry today, and "tests: <class> ran 0 tests" is not Get-TestRunVerdict's current wording. Fix: step 1 adds the entry to Paths/good and pins the printed text.

EARLIER: Review 11 F1-F7 all resolved by A1-A7 (F2's fix carries F1 here, F6's fix carries F3 here)
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A10: a call matches only through the declaring class (qualified, or unqualified in the class's own file); fixture GatewayOnly/good-samename must pass
- F2 · accepted · A11: a regional definition is disabled while regions are unavailable (regions D-items), so no pick runs without an index; a throwing region read is D21's player-query failure
- F3 · accepted · A12: the fields are Entity, DamageValue and Weight; a verbose "left to the game" count and a Session 1 observation over two ticks
- F4 · accepted · A13: the asymmetry is intended and stated in D16 (the territory rule is D17's per-wave skip for every location type)
- F5 · accepted · A14: step 1 gives Paths/good a `dataTests` entry; D27 already pins the printed text

## Review 13 · 2026-09-29 · subagent · plan uncommitted · plan 134472 B · 24 items · files 0 · e3b0c44298fc · prompt 55afed0c52f2 · scope A10,A11,A12,A13,A14
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A10-A14 (design §9 D26); the prompt held no Steam ID. It confirmed HealthMonitor.Tick, TriggerBus.Tick and EntityExtensions.Write exist and collide under bare-name matching, that Logic/Validation.cs:271 disables a regional definition with "regions unavailable", and that Paths/good has no preflight-checks.json today.

F1 · blocking · 10.1 · A10: matching only `<Class>.<Method>` or unqualified in the class's file leaves no gateway control on the existing instance-method [Mutating] members reached through Persistence.Disk and AdminOps.Instance; a `Persistence.Disk.Delete(...)` in Commands/, caught today by the bare-name match (tools/preflight.ps1:1764), would pass, and no GatewayOnly fixture plants it. Fix: match through a receiver of the declaring type too, and add fixture GatewayOnly/bad-instance.

F2 · advisory · 4.2 · A12: if the game changes an unengaged seed's DamageValue or Weight each update, every seed reads as the game's after one tick and Hunt stops removing seeds for ineligible players; the plan names no fallback. Fix: name it now (e.g. Entity only plus a HuntAction Weight sentinel).

F3 · advisory · 4.1 · A13: the scope retry takes the first in-scope angle even when it is claimed; intended, but no test pins it. Fix: a PlayerPick or WaveGate test.

F4 · advisory · 6.2 · A11: D21's player-query category does not name the region read as a source. Fix: name it and add "a throwing region read spawns" to D21's fails-when.

F5 · advisory · 10.1 · A10: the rule is silent on `using static`, nested-class callers, nameof and method-group references in other files, which the bare-name match covers today. Fix: say how each is treated.

F6 · advisory · 12.4 · A14: step 1's -Paths bullet does not name Paths/good's new `dataTests` entry. Fix: name it.

EARLIER: Review 12 F2-F5 resolved by A11-A14; F1's fix (A10) carries F1 and F5 here
14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · A16: A10 withdrawn; the bare-name match stays (it covers instance receivers), a non-accepted file may not declare a [Mutating] name, the colliding HealthMonitor.Tick, TriggerBus.Tick and Persistence.Write are renamed in step 1; fixtures bad-instance and bad-samename
- F2 · accepted · A17: the fallback is an Entity-only comparison
- F3 · accepted · A18: D16's fails-when pins it
- F4 · accepted · A19
- F5 · accepted · A16: bare names cover `using static`, nameof and method groups
- F6 · accepted · A20

## Review 14 · 2026-09-29 · subagent · plan uncommitted · plan 137865 B · 24 items · files 0 · e3b0c44298fc · prompt 724ce2403c65 · scope A15,A16,A17,A18,A19,A20
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A15-A20 (design §9 D26); the prompt held no Steam ID. It confirmed the committed Test-CheckGatewayOnly matches bare names (bad-instance is caught), that outside Logic/ the only collisions in non-accepted files are EntityExtensions.Write, HealthMonitor.Tick and TriggerBus.Tick, and that ControlCaseTests has no Pending support today.

F1 · advisory · 12.4 · A15: the Pending list and D22's row list miss controls ControlCaseTests pulls (D20, D22, D26, D27, D31, D34); D11's ClearDrops arrives with step 2. Fix: list every control as a step-1 row or Pending with its step.

F2 · advisory · 12.4 · A15: a pending entry fails only at close, after the tag, and a pending key that is no control is never reported. Fix: fail once its step's post-audit is recorded, and fail a non-control key.

F3 · advisory · 14.4 · A16: the renames touch undeclared paths (Services/HealthMonitor, TriggerBus, Persistence, EventScheduler; Logic/IFileStore, DataStore). Fix: declare them in Paths walked › Step 1.

F4 · advisory · 10.1 · A16: whether the dispatched services count as accepted callers is unsaid (Announcer and Pusher declare Tick). Fix: say they stay exempt.

F5 · advisory · 12.4 · A16: bad-samename fails with or without the new declaration rule, so it cannot show the rule. Fix: assert its message or drop the rule.

F6 · advisory · 10.1 · D22 via A16: EventScheduler.cs, Core.cs and Patches/ are accepted for every [Mutating] name. Fix: accept them only for the tick and boot entry-point names.

F7 · advisory · 4.1 · A18: PlayerPick takes no territory set. Fix: state the seam (PlayerPick with Territory.IsClaimed and WaveGate).

F8 · advisory · 4.2 · A17: the no-go wording is unclear and the fallback flips the replace fixture. Fix: "<d> above 0 on an unengaged wave", and the fallback rewrites the fixture.

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A22: every control a step-1 row except D21 (step 2), D19 and D24 (step 3), D25 (step 4); D11's order flag is built in step 1 (Logic/SpawnLedger.cs, A21)
- F2 · accepted · A22
- F3 · accepted · A21 (recorded before this review returned)
- F4 · accepted · A23
- F5 · accepted · A23: the rule is dropped; bad-samename stays as a regression fixture
- F6 · accepted · A23: fixture GatewayOnly/bad-systemcaller
- F7 · accepted · A24
- F8 · accepted · A25

## Review 15 · 2026-09-29 · subagent · plan uncommitted · plan 141672 B · 24 items · files 0 · e3b0c44298fc · prompt d5ee1c8a48b5 · scope A21,A22,A23,A24,A25,A26
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A21-A26 (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 4.4 · A26: a Hunt wave at a Point or the admin with allowTerritory and a failed map build passes WaveGate while HuntAction reads "the TerritoryMap built for the wave"; whether it spawns without hunting is unsaid. Fix: a Hunt wave always needs the map, or no map seeds nobody; add the case to the fails-when.

F2 · advisory · 4.4 · A26: WaveGate.Decide's signature has no map state, location type or behaviour input, and the fails-when has no "territory unknown lifted by allowTerritory on AroundPlayer" case. Fix: add the input and the case.

F3 · advisory · 12.4 · A22: D22's row list still holds D21 (pending) and misses D20, D22, D26, D27, D31, D32, D34; A22 also misses D32. Fix: replace the list by the rule and name D32.

F4 · advisory · 12.4 · A22: the pending check reads docs/audits/event-spawns.md, which the test csproj never copies, a missing file is unspecified, and D25's step-4 entry fails only after the tag. Fix: copy the audit, fail a missing one, fail a step-4 entry once the Version is 0.7.0.

F5 · advisory · 12.4 · A22: step-1 rows for D11, D13, D22 and D32 name seams partly built in step 2, and ControlCaseTests fails a row naming a missing method. Fix: say what step 1 builds and that later methods join their rows in their step, or pend D11.

F6 · advisory · 14.4 · A21: its list is complete, but no renamed symbol is in Core.cs, Plugin.cs or Patches/; Logic Engine, Precedence and Messages appear in no step's walk. Fix: correct the reason and declare those three where edited.

F7 · advisory · 10.1 · D22: "called only from SpawnTracker, EventRuntime or Gateway.Run" is not what the gateway check enforces once every dispatched service stays accepted. Fix: reword to Gateway.Run or a $DispatchedServices class.

F8 · advisory · 10.1 · A23 verified in HEAD: Announcer and Pusher Tick pass as dispatched services, EventScheduler.cs uses only Tick, Core.cs only BootSweep, Patches/, Plugin.cs and Config/ call no [Mutating] name; the only collisions are the three A16 renames. Fix: none; record as evidence.

EARLIER: all resolved
14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · A27: a Hunt wave always needs the map; "territory unknown" skips it whatever allowTerritory says
- F2 · accepted · A27
- F3 · accepted · A28
- F4 · accepted · A29
- F5 · accepted · A28: step 1 builds SpawnOrder.ClearDrops and the HuntSeeds race and replace cases; later methods join their rows
- F6 · accepted · A30
- F7 · accepted · A31
- F8 · accepted · evidence for the A16 rename list; no change

## Review 16 · 2026-09-29 · subagent · plan uncommitted · plan 144725 B · 24 items · files 0 · e3b0c44298fc · prompt 5259d0da3beb · scope A27,A28,A29,A30,A31
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A27-A31 (design §9 D26); the prompt held no Steam ID. It verified WaveFacts and WaveGate against A27, SpawnOrder.ClearDrops and HuntSeeds against A28, the csproj copies and Version against A29, and the three Logic files of A30.

F1 · advisory · 4.1 · A27: the new D29 case "a Point wave with allowTerritory and a failed map is skipped" contradicts D17's fails-when "a failed map build spawns" and Business rules 3, neither qualified by need. Fix: qualify both with "a wave that needs the map" and name D29 the authority in Business rules 8.

F2 · advisory · 12.4 · A28: D32's evidence starts "manual:", so ControlCaseTests does not see it as a control and a D32 row fails; the 12.4 table's Logic-controls row lists fewer than A28's rule. Fix: put D32's test evidence first and widen the row.

F3 · advisory · 14.4 · A30: Logic/AdminFlows.cs is modified in the working tree but not in Paths walked › Step 1. Fix: declare it.

F4 · advisory · 10.1 · A31: the -AuthSuite clause and Security › Authorization keep the old caller list. Fix: the same wording in both.

F5 · advisory · 12.4 · A29: the new ControlCases_* methods would be named by no row. Fix: add them to the existing ControlCases test row (event-library D31).

Scenarios: (1) 4.4 a Point Hunt wave with allowTerritory inside a castle hunts nobody there; the README should say so. (2) 12.4 a D32 row fails as a non-control (F2). (3) 7.3/4.4 which map a live Hunt tick reads when a later wave's build fails is unstated.

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A32
- F2 · accepted · A33
- F3 · rejected · the AdminFlows.cs edit was reverted before this review returned; step 1 writes no undeclared file (location aroundplayer is mapped inside EventsEditor)
- F4 · accepted · A34
- F5 · accepted · A35
- Scenario 1 · accepted · A32 (Business rules 8 and both READMEs)
- Scenario 3 · accepted · A36

## Review 17 · 2026-09-29 · subagent · plan uncommitted · plan 146876 B · 24 items · files 0 · e3b0c44298fc · prompt 1ec03db8ead2 · scope A32,A33,A34,A35,A36
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A32-A36 (design §9 D26); the prompt held no Steam ID. It checked A34's wording against $DispatchedServices (tools/preflight.ps1:1681) and A35 against ControlCaseTests.Problems.

F1 · advisory · 3.3 · A36: the per-event kept map outlives Design › Data's "one wave … at the wave's spawn" and 8.2's "rebuilt per wave", and D33 does not empty it. Fix: Data row per event, dropped at every end path; add it to D33.

F2 · advisory · 4.4 · A36 vs D17: D17's "a map built for one wave is reused by the next" contradicts the kept map; no D13 case pins which map a tick reads. Fix: narrow D17; add a D13 fails-when.

F3 · advisory · 4.4 · A36: live Hunt waves seed from a stale map with no limit while "territory unknown" shows, against Business rules 3. Fix: state the exception, bound it, or drop Hunt seeds while territory is unknown.

F4 · advisory · 12.4 · A33: D32 is now a step-1 control but step 1 names no ChatBytes_* form methods, and the empty-input table has no D32 row. Fix: name them in step 1 and add the row, or pend D32.

F5 · advisory · 12.4 · A33: step 2's AdminLines 480-byte test cannot join D32's CommandArgTests row. Fix: put it in CommandArgTests ChatBytes_* or add a second row.

F6 · advisory · 4.1 · A32: Interfaces › Failure behaviour and the Failure & observability row still say any failed map skips the wave. Fix: "a wave that needs the map (D29)".

F7 · advisory · 12.4 · A35 verified; Paths walked › Step 1 should name both copied files.

Scenarios: (1) 7.2/4.4 a castle placed after the last good build is not seen by live Hunt units (F3). (2) 8.2/3.3 a single-wave Hunt event's kept map lingers to restart (F1). (3) 12.4 step 1 without ChatBytes_* fails its own ControlCaseTests (F4).

15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A39
- F2 · accepted · A37 (D13 fails-when) and A38 (D17)
- F3 · accepted · A37: fail closed, Hunt seeding stops while the event's latest build failed
- F4 · accepted · A41
- F5 · accepted · A41: the DebugUnit byte check joins CommandArgTests ChatBytes_*
- F6 · accepted · A40
- F7 · accepted · A41 (Paths walked wording)

## Review 18 · 2026-09-29 · subagent · plan uncommitted · plan 149478 B · 24 items · files 0 · e3b0c44298fc · prompt 945cc61f0e0e · scope A37,A38,A39,A40,A41
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A37-A41 (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 3.3 · A39: D33's fails-when does not cover the kept territory map, so EndPaths (D27's 3.3 data test) stays green if an end path leaves it. Fix: add "the event's kept territory map survives an end path (TerritoryMaps.Holds(id) after EventEnded, Purged or Restart)".

F2 · advisory · 4.1/4.4 · A37: after a successful build a live Hunt unit reads a map as old as the event's last wave and may seed a player in a castle claimed since, against Business rules 8's "never". Fix: soften Business rules 8 to the last built map and say so in the READMEs, or rebuild on a cadence.

F3 · advisory · 6.2/12.3 · A37, A40: after a failed build on the last wave Hunt stays off with no retry, and D30's "territory unknown" may outlive the event. Fix: state no retry is intended, or retry; clear the entry at the event's end paths.

F4 · advisory · 12.4 · A41: D32's empty-input result reads as a pass over no lines. Fix: assert the line list is not empty.

F5 · advisory · 14.4 · A41: step 2 writes CommandArgTests.cs, and the DebugUnit assertions live in SpawnLedgerTests.cs, but step 2's walk names only AdminLinesTests.cs (covered by step 1's Tests/** glob). Fix: name both.

F6 · advisory · 12.4 · A37: D13's new fails-when names no seam. Fix: name TerritoryMaps.ForHunt.

Scenarios: (1) 4.1/7.3 a castle placed after a one-wave Hunt event's only build (F2). (2) 6.2/12.3 a failed build on the last wave leaves "territory unknown" after the event ends (F3). (3) 3.3/7.3 stop then restart the same event id with the old map still held (F1).

14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · A42
- F2 · accepted · A43: Business rules 8 softened to the event's last built map; no rebuild cadence
- F3 · accepted · A44: no retry intended; the entry follows TerritoryMaps.AnyFailed and clears at end paths
- F4 · accepted · A45
- F5 · accepted · A46
- F6 · accepted · A44

## Review 19 · 2026-09-29 · subagent · plan uncommitted · plan 151570 B · 24 items · files 0 · e3b0c44298fc · prompt cd62a7f6fd67 · scope A42,A43,A44,A45,A46
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A42-A46 (design §9 D26); the prompt held no Steam ID. It checked TerritoryMaps, AnyFailed, ForHunt and WaveLifecycle's end paths in Logic/Spawning.cs against A42 and A44.

F1 · advisory · 12.1/6.2 · A44: D30's fails-when does not pin that "territory unknown" clears after the end path of the only failed event; SpawnHealth keeps its own flag. Fix: add the case, driven through WaveLifecycle.EventEnded then Territory(Maps.AnyFailed).

F2 · advisory · 7.2/3.3 · A42: D33's two-event case does not cover the other event's kept map. Fix: B's ForHunt and Holds unchanged.

F3 · advisory · 12.4 · A45: the non-empty line list is only in the empty-input table, not D32's fails-when. Fix: append it.

F4 · advisory · 4.1 · A43: no check that the READMEs carry the once-per-wave Hunt rule. Fix: a step-4 checklist line, or accept as prose.

F5 · advisory · 12.4 · A42: the WaveLifecycle empty-input row does not say "no kept map". Fix: add Holds(id) false.

F6 · advisory · 14.4 · A46: step 2's walk names exact test files while classes are split into partials. Fix: a glob.

Scenarios: (1) 7.2/3.3 two Hunt events share a player, A failed and B built; stopping A must keep B's map (F2). (2) 12.1/6.2 a Point event's last build fails and it ends; the health entry lingers (F1). (3) 7.3/3.3 restart during a failed-build window is handled (no finding).

EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · built as a HealthTests Spawns case (EventEnded, then Territory(Maps.AnyFailed) clears the entry)
- F2 · accepted · built in EndPathTests' two-event case (the other event's ForHunt and Holds unchanged)
- F3 · accepted · built: ChatBytes_empty_ asserts the line list is not empty (A45)
- F4 · accepted · step 4 checks both READMEs for the Hunt territory sentence as a checklist line of the six-surface bullet
- F5 · accepted · built in EndPathTests' empty case (Holds(id) false)
- F6 · accepted · no change: step 1's Nyarlathotep.Tests/** glob (A21) declares any partial file

## Review 20 · 2026-09-29 · subagent · plan uncommitted · plan 152246 B · 24 items · files 0 · e3b0c44298fc · prompt 6e08faba292a · scope A10,A11,A14,A21,A22,A23,A26,A37,A38,A39,A40,A41
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to the gating amendments last seen in REVISE rounds (design §9 D26); the prompt held no Steam ID. It verified A11 (Logic/Regions.cs), A21's symbols and renames, A23's $DispatchedServices and A14's Paths/good fixture in the tree.

F1 · advisory · 14.4: step 1 edits docs/NYARLATHOTEP_DESIGN.md (§6 Settable fields) but Paths walked › Step 1 does not list it. Fix: declare it.

F2 · advisory · 12.4: D22's fails-when omits fixture GatewayOnly/bad-systemcaller, though its body and the 12.4 table name it; -SelfTest still catches it. Fix: add it to the fails-when.

F3 · advisory · 10.1: A23's entry-point name list has no stated home, and bare-name matching accepts any [Mutating] Tick from the three System-actor files. Fix: name the list in tools/preflight.ps1 ($SystemEntryPoints) and say names added there are accepted in all three places.

F4 · advisory · 3.3: D33's Holds(id) examples omit event stop and fault cancel. Fix: add them to the EndPaths cases.

F5 · advisory · 4.4: whether a control-blocked wave builds the map is unsaid; a blocked event with a failing build would show "territory unknown". Fix: build the map only for a wave past the blocker, with a WavePrecedence case.

F6 · advisory · 14.4: Paths walked names CommandArgTests.cs while step 1 edits CommandArgTests.Library.cs (covered by Nyarlathotep.Tests/**). Fix: name the partial or a glob.

Scenarios: (1) 7.2/4.4 a pillar-off Hunt event with a failing build shows "territory unknown" (F5). (2) 8.2/3.3 event stop regression on the kept map passes the filter (F4). (3) 10.1 a future HuntAction.Tick called from Patches/ passes as the System actor (F3).

15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · A47
- F2 · accepted · no plan change: D22's body names the fixture and -SelfTest fails any bad fixture that passes; the D22 cmd row lists it
- F3 · accepted · built: tools/preflight.ps1 holds the names as $script:SystemEntryPoints with a comment that each is accepted from EventScheduler.cs, Core.cs and Patches/
- F4 · accepted · built: EndPathTests assert Holds(id) false after event stop and fault cancel (EventEnded's two other callers) as well
- F5 · accepted · built: D29 already gives a blocked wave "no wave", so WaveAction (step 2) builds the map only after Precedence.StartBlocker passes; a WavePrecedence case shows a blocked wave with MapFailed yields NoWave and no territory line
- F6 · accepted · no change: covered by Nyarlathotep.Tests/** (A21)

## Review 21 · 2026-09-29 · subagent · plan uncommitted · plan 152941 B · 24 items · files 0 · e3b0c44298fc · prompt a4afb479feab · scope A47
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to A47 (design §9 D26); the prompt held no Steam ID. It compared `git status --short --ignored` and `git diff --stat` with Paths walked and found every path step 1 has written declared.

F1 · advisory · 14.4: the plan store docs/dod/event-spawns.md, which step 1 amends, is declared under Step 4 and the header sentence, not the Steps 1-4 line. Fix: add it to the Steps 1-4 line.

F2 · advisory · 14.4: CommandArgTests.Library.cs, EventValidationTests.EventSpawns.cs and SpawningTests.EventSpawns.cs are covered only by A21's Tests/** glob. Fix: name them, or note the glob.

Scenarios: (1) a new partial test file is covered by the glob. (2) a later §9 edit is covered at path level. (3) a per-step -DeclaredOf run that does not treat the plan store as declared would flag it (F1).

15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · accepted · no change: Paths walked's header declares the plan store for every step, as in the closed children, whose steps passed -Paths -DeclaredOf with their plan stores amended
- F2 · accepted · no change: A21's Nyarlathotep/Nyarlathotep.Tests/** is the declaration; the named list is a guide

## Review 22 · 2026-09-29 · subagent · plan uncommitted · plan 154187 B · 24 items · files 0 · e3b0c44298fc · prompt 13ad6338e245 · scope A48
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A48 (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 14.4 · A48: .claude/ also holds hooks/ and settings.local.json, which this child does not write; the .claude/ glob covers them, but naming two files may read as the whole content. Fix: optional wording.

F2 · advisory · 14.4 · A48: a worktree agent also writes git metadata (.git/worktrees/<name>, a temporary branch ref), removed with the worktree and branch. Fix: none needed.

F3 · advisory · — · the rest of the plan was not regraded, per the scope.

15/15 layers · 49/49 probes
VERDICT: READY

### Dispositions
- F1 · rejected · the declaration names what this child writes; the .claude/ glob already covers the rest
- F2 · accepted · no change: the worktree and its branch were removed after the merge; git's own metadata went with them
- F3 · accepted · no change: the scope excludes the rest of the plan

## Review 23 · 2026-09-29 · subagent · plan uncommitted · plan 156627 B · 24 items · files 0 · e3b0c44298fc · prompt 975364e7d093 · scope A49,A50,A51,A52
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A49-A52 (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 12.4 · A49's fixture bad-destroyutility is named only in D34's pattern list, not in D34's -SelfTest fails-when list, the 12.4 EntityWrites and -AuthSuite rows, or step 1's EntityWrites bullet. Fix: name it in all three.
F2 · advisory · 10.1 · D34 names DestroyUtility.Destroy as a literal, while the StructuralEdits check matches DestroyUtility.Destroy\w*( with a nested first argument. Fix: say D34 matches the same form.
F3 · advisory · 12.4 · event-spawns' five floor categories would be checked in step 1, before its dependencySuites entry exists (step 2). Fix: the floor joins in step 2 with the entry, or is checked only for a named slug; add bad-floor to the 12.4 DependencySuite row and step 1.
F4 · advisory · 5.1 · A50's three behaviours (the refused chance's argument name, the aroundplayer edit's field name, lowercase booleans) are named by no fails-when. Fix: add them to D18's tests and fails-when.

Scenarios hunted: a nested-argument DestroyUtility call in Commands/ (F2); step 1's preflight with the event-spawns floor but no entry (F3); an empty entry and floor for a new slug (already "has no categories", exit 1).

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · bad-destroyutility named in D34's fails-when, the EntityWrites 12.4 row (the -AuthSuite row reads "the EntityWrites bad fixtures above") and step 1
- F2 · accepted · D34 and $script:EntityWriteRx match DestroyUtility.Destroy\w*( ; the fixture's first argument is nested, Em(Core.Server)
- F3 · accepted · $script:SuiteFloor holds event-library's nine in step 1; event-spawns' five join with its entry in step 2; a floor slug without an entry fails; bad-floor in the 12.4 row and step 1
- F4 · accepted · D18's fails-when names all three; AuthoringTests.Spawns_fails_when_refusal_names_wrong_argument pins the argument name, Spawns_passes_valid_set_changes_file now compares exactly and pins action.location and lowercase true; the exact compare found set replies still printed True, fixed in DefinitionEditor.cs

## Review 24 · 2026-09-29 · subagent · plan uncommitted · plan 159007 B · 24 items · files 0 · e3b0c44298fc · prompt 22f807a582f3 · scope A53,A54
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A53 and A54 (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 10.1 · Test-SystemEntryUse trusts the identifier before the method; A54 closes the using alias only, and a field, local or parameter named after a service in a System-actor file passes the same way. Fix: extend the rule to declarations, fixture GatewayOnly/bad-shadow.
F2 · advisory · 10.1 · the second System-actor exemption (a <Type>.<Name> whose Services/<Type>.cs declares no [Mutating] <Name>, Announcer.Tick and Pusher.Tick) is recorded nowhere; D22 still describes renaming. Fix: one clause in A54 or D22.
F3 · advisory · 12.4 · D22's fails-when does not name the five new fixtures; only the 12.4 row does. Fix: append them.
F4 · advisory · 10.1 · the handler check is textual; an unreachable flow call after return still passes. Not an authorization gap (adminOnly is checked separately). Fix: reject it, or note the limit beside Get-EventVerbHandler.
F5 · advisory · 11.3 · other validator reasons echo raw input unbounded (unknown pillar, region, unit, faction, trigger and action type; deny-listed unit and faction), breaking D32 as A53's case did and passing rich text. Fix: one bounded echo helper, or a deferred defect.
F6 · advisory · 12.4 · no ChatBytes case plants an over-long action.stats key though A53 names it. Fix: add it on an Empower definition.
F7 · advisory · A53 is tagged 11.2 while the plan maps D32 to 11.3. Fix: re-tag, or leave.

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · Test-CheckGatewayOnly fails on a value declared under a service's name in a System-actor file; fixture GatewayOnly/bad-shadow (var SpawnTracker = Services.Persistence.Store; SpawnTracker.Tick())
- F2 · accepted · D22 states the type-qualified match, the own-method acceptance of Announcer.Tick and Pusher.Tick, and the alias and value failures (A54)
- F3 · accepted · D22's fails-when names bad-systemtype, bad-alias, bad-shadow, bad-unhandled, bad-literal and bad-gutted
- F4 · accepted · noted as a known limit beside Get-EventVerbHandler; the authorization control is adminOnly
- F5 · accepted · EventValidator.Shown bounds every echoed value (96 characters, plain text), in A53; tested with 480-character and rich-text values
- F6 · accepted · the ChatBytes test plants a 480-character stats key on an Empower definition
- F7 · accepted · A53 re-tagged 11.3

## Review 25 · 2026-09-29 · subagent · plan uncommitted · plan 161040 B · 24 items · files 0 · e3b0c44298fc · prompt 2175e8e85d73 · scope A55
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A55 (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 10.1 · the floor is said to hold every real [Mutating] method, but SpawnTracker's RequestWave, EndEventUnits and PurgeUnits return tuples, which neither the gateway check's declaration regex nor the floor's reads, so they were never protected and a call from Commands/ passes; nothing fails when the floor is incomplete. Fix: read tuple return types, fail on a [Mutating] declaration missing from the floor, list the three, fixture GatewayOnly/bad-tuple, all in D22's fails-when.
F2 · advisory · 10.1 / 12.4 · the floor skips a missing service file or a name matching no declaration (a rename, a generic, no modifier). Fix: fail when a floor name matches no declaration.
F3 · advisory · 12.4 · the 12.4 table's gateway rows do not name the new fixture. Fix: add it beside the A54 fixtures.
F4 · advisory · 12.4 · bad-unmarked is untracked; the step 1 commit must add it (its path is declared).

Scenarios hunted: a later command calls SpawnTracker.EndEventUnits directly (F1); Persistence.cs moved or Delete made generic without the attribute (F2); step 2 adds HuntAction's writers without the floor (F1).

14/15 layers · 48/49 probes

VERDICT: REVISE

### Dispositions
- F1 · accepted · $script:MutatingDeclRx reads tuple return types (and generic method names); the floor lists all 43 real [Mutating] methods, SpawnTracker's three included; the new Test-CheckMutatingFloor (in -AuthSuite's gateway part) fails on an unlisted [Mutating] method, a listed method without [Mutating], and a [Mutating] attribute on a declaration it cannot read; fixtures GatewayOnly/bad-tuple and MutatingFloor/bad-tuple, bad-unlisted, bad-unreadable; D22 names them
- F2 · accepted · a floor service missing or a floor name not declared fails (MutatingFloor/bad-missing, bad-renamed); the fixtures carry copies of the real services, so no fixture exemption is needed
- F3 · accepted · the 12.4 gateway row names the A55 fixtures
- F4 · accepted · the fixture moved to MutatingFloor/bad; the step 1 commit adds tools/preflight-fixtures/MutatingFloor/**, declared in step 1's paths

## Review 26 · 2026-09-29 · subagent · plan uncommitted · plan 162281 B · 24 items · files 0 · e3b0c44298fc · prompt 4c194929d84a · scope A55
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A55, round 2 (design §9 D26); the prompt held no Steam ID.

F1 · advisory · 10.1 · the floor counts only the spelling [Mutating], while the entity-writes check takes [Mutating()] or [Mutating, Obsolete] as marked; a new writer spelled that way is neither unreadable nor listed and the gateway check never learns it. Fix: count any attribute list naming Mutating, fixture MutatingFloor/bad-parens.
F2 · advisory · 12.4 / 14.4 · the floor check is authsuite mode, so plain preflight never runs it. Fix: default mode, or say so in D22.
F3 · advisory · 4.5 · the floor protects writers that carry [Mutating]; a new file-writing method without the attribute is outside every check, so "cannot drop a writer (a Persistence disk write among them)" holds for listed writers only. Fix: narrow the sentence, or add file-write patterns.

Earlier findings: all resolved (Review 25 F1, F2 in code and fixtures; F3, F4 advisories).

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · Test-CheckMutatingFloor counts `\[[^\]]*\bMutating(?:Attribute)?\b[^\]]*\]`, so [Mutating()] fails as unreadable; fixture MutatingFloor/bad-parens
- F2 · accepted · MutatingFloor runs in default mode: plain preflight prints "mutating floor: 43/43 listed methods [Mutating] in 12 services, none unlisted"
- F3 · accepted · D22 narrowed to listed writers, naming the file-writing method without [Mutating] as outside, as before

## Review 27 · 2026-09-29 · subagent · plan uncommitted · plan 163830 B · 24 items · files 0 · e3b0c44298fc · prompt 4143d85750bd · scope A56
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A56 (design §9 D26); the prompt held no Steam ID. It removed the index-side token search in a scratchpad copy and saw the probe fail.

F1 · advisory · 10.3 / 12.4 · the tools/ credential rule on the index is claimed but no evidence guards it: the probe checks only the tcli token label, so deleting the tools/ loop leaves -SelfTest green. Fix: a second probe case (a staged `gh auth token`), named in D31's fails-when.
F2 · advisory · 12.4 · an empty index returns no hits and 0 blobs and reads as a pass. Fix: fail on Blobs -eq 0.
F3 · advisory · 6.2 · the probe discards git init and git add failures, so a setup failure shows a misleading reason. Fix: check $LASTEXITCODE.
F4 · advisory · 13.1 · the tools/ index loop runs one git show per script. Fix: none now; git cat-file --batch if it slows.

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · the probe stages a tools/ credential read as a second case and requires its "(index, reads a credential or the environment" hit; D31's fails-when names it
- F2 · accepted · Get-IndexSecretHits returns "index empty: no staged blob to search" as an error
- F3 · accepted · the probe returns "scratch repository setup failed (git init|git add)"
- F4 · rejected · no change needed at today's size (10 tracked tools/ scripts); the reviewer's own fix is conditional

## Review 28 · 2026-09-29 · subagent · plan uncommitted · plan 165263 B · 24 items · files 0 · e3b0c44298fc · prompt 7b01d02e9981 · scope A57
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A57 (design §9 D26); the prompt held no Steam ID. In a scratch copy it emptied $script:DataTestFloor and saw Paths/bad-datatests-floor pass, and ran -Paths -DeclaredOf event-spawns on the real tree.

F1 · advisory · 12.4 · the Paths plant loops check only that a bad fixture fails, not that it fails for its plant, so a later unrelated fault in Paths/bad-datatests-floor would keep it failing with the floor removed. Fix: an expected line per plant.
F2 · advisory · 12.4 / D33 · the floor requires EndPathTests to run at least one test, not D33's "fewer than 5 tests run: fail". Fix: a per-class minimum beside the floor, if wanted.
F3 · advisory · text · the floor compares with -notcontains, which ignores case, so a misspelt entry is reported as "ran 0 tests" rather than as the floor. Fix: -cnotcontains.

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · the real tree's plant loop requires Paths/bad-datatests-floor to report "miss EndPathTests (floor)", else it counts as passing ("plants: bad-datatests-floor (not for its floor: ...) passed")
- F2 · rejected · out of A57's scope: D33's own evidence (dotnet test --filter EndPaths, "Passed!" with at least 5 tests) carries its test-count bound, and -Paths runs the class only to prove it runs and passes
- F3 · accepted · the floor compares with -cnotcontains

## Review 29 · 2026-09-29 · subagent · plan uncommitted · plan 168878 B · 24 items · files 0 · e3b0c44298fc · prompt 0734c276f132 · scope A58-A59
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A58 and A59 (design §9 D26); the prompt held no Steam ID. It ran -SelfTest with verbose output (each DependencySuite and MutatingFloor fixture fails for its own plant), -DependencySuite event-spawns and event-library, and the PushTests Spawns tests.

F1 · blocking · 5.3 · A59 counts a skipped wave in ActiveEvent.WavesSpawned, the status row's `wave=<spawned>/<total>` (Logic/ApiLines.cs, docs/RAPHAEL_INTEGRATION_CONTRACT.md) and `.nyar event info`'s wave n/m, so a skipped wave reads as spawned, against D20's unchanged contract. Fix: redefine the field, or keep spawned meaning spawned with a separate counter, with a fails-when.
F2 · advisory · 4.3 · no test covers WaveAction.QueueDueWave's call to WaveSkipped; deleting it passes every test. Fix: a Session 1 observation (the skip line once, the next wave one interval later) or a static check.
F3 · advisory · 12.4 · event-spawns' floor has no fixture of its own; A58's one-slug unroll is defensive and pinned by no fixture. Fix: DependencySuite/bad-floor-spawns.
F4 · advisory · 7.3 / 9.3 · a NoWave outcome (a control blocker) does not count the wave, so a blocker lifted after several intervals releases the overdue waves one per tick. Fix: state catch-up or counting.
F5 · advisory · 11.2 · the Announcer's next-wave warning fires before a wave that then skips. Fix: document it, or leave it to spawn-extras.

14/15 layers · 48/49 probes

VERDICT: REVISE

### Dispositions
- F1 · accepted · A62: the second option, keeping spawned meaning spawned; WavesSkipped counts skips, WavesUsed drives the schedule, the warning and the `wave` push's number, the status row and event info keep WavesSpawned; the PushTests control asserts the status row reads wave=1/ after a skip and a spawn
- F2 · accepted · as a Session 1 observation: docs/features/EVENT_SPAWNS.md's owner steps give es-castle two waves 60 s apart, and the record needs the skip line once per wave, the second one interval after the first
- F3 · accepted · A63: fixture DependencySuite/bad-floor-spawns, "event-spawns: category hunt-seed missing from the entry (floor)"
- F4 · rejected · out of scope for A58-A59 and not new: NoWave is WaveGate's step 1 outcome for a wave that must not be decided yet; the catch-up after a lifted blocker is a step 3 question (recorded as a step 3 note in the plan's Log, Review 30 F5), not part of D24's text
- F5 · accepted · documented in docs/features/EVENT_SPAWNS.md › Edge cases: a warned wave can still skip (territory, no eligible player, a zero roll); the warning promises the time, not the units

## Review 30 · 2026-09-29 · subagent · plan uncommitted · plan 171527 B · 24 items · files 0 · e3b0c44298fc · prompt 6e6773c2ca08 · scope A58,A59,A60,A61,A62,A63
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendments A58-A63 (design §9 D26), round 2 of this run; the prompt held no Steam ID. It checked each amendment against the uncommitted step 2 code and ran dotnet test (2150 passed), -DependencySuite event-spawns (5/5) and -SelfTest (39/39, each DependencySuite and MutatingFloor fixture failing for its own plant, bad-floor-spawns included).

F1 · advisory · 5.3 · after A62 the `wave` push numbers the wave in the schedule while the status row counts spawned waves; the contract does not say which count the push's <n> is. Fix: a sentence in docs/features/EVENT_SPAWNS.md › Edge cases.
F2 · advisory · 11.2 / 4.3 · A62's switch of AnnouncerCore.UpcomingWave to WavesUsed has no control. Fix: an AnnouncerTests case with a skip.
F3 · advisory · 12.1 · A61's purge-path streak close is in Services only; removing it fails no command. Fix: a Session observation or a Logic seam EndPathTests drives.
F4 · advisory · — · bad-floor-spawns sits in the D33 DependencySuite row, not beside D21's rows. Fix: none needed.
F5 · advisory · — · Review 29's F4 disposition points the NoWave catch-up at D24, whose text does not cover it. Fix: a step 3 note, or reword.

EARLIER: all resolved

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · docs/features/EVENT_SPAWNS.md › Edge cases: the push and the warning number a wave by its place in the schedule, so after a skip the push's wave=2 can run ahead of the status row's wave=1/3
- F2 · accepted · AnnouncerTests.A_skipped_wave_moves_the_warning_to_the_next_wave
- F3 · accepted · as a step 3 note in the plan's Log (a Logic seam for EndPathTests, or a Session 2 observation); a streak cannot be opened on demand in Session 1
- F4 · rejected · no change needed, as the reviewer says; the D33 row is where the DependencySuite fixtures live
- F5 · accepted · Review 29's F4 disposition reworded; the catch-up is a step 3 note in the plan's Log

## Review 31 · 2026-09-29 · subagent · plan uncommitted · plan 172835 B · 24 items · files 0 · e3b0c44298fc · prompt bc106a0633cf · scope A64
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, scoped to amendment A64 (design §9 D26), round 3 of this run; the prompt held no Steam ID. It ran -Paths -DeclaredOf event-spawns (declared 517/517) and checked every changed path in git status against the plan's declarations.

F1 · advisory · 14.4 · -DeclaredOf matches the plan's declarations as a whole; step 2 touched files declared only under Step 1 or a glob (Spawning.cs, SpawnLedger.cs, test partials, fixture folders), so Step 2's list is incomplete as a record. Fix: add them, or say the step lists are attribution only.
F2 · advisory · 14.4 · a Logic seam for A61's purge close in step 3 would need a Step 3 path. Fix: an amendment when it is built.

15/15 layers · 49/49 probes

VERDICT: READY

### Dispositions
- F1 · accepted · deferred to step 3's Paths walked amendment (a step 3 note in the Log), so this run's review rounds end at 3 without another gating edit
- F2 · accepted · the same step 3 note
