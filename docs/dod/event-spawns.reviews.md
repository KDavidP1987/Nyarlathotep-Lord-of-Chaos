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
- F7 · accepted with a different control · run-time write tracing is not used; instead every write site is found statically: -Paths' unmarked-temp-root scan (event-library A18, fixture Paths/bad-tempvar) fails any tools/ line that takes the %TEMP% root without naming a declared nyar-<name> folder or carrying a '# nyar-temp:' registration, so the reviewer's scenario (a helper staging into an undeclared %TEMP% folder and cleaning it up) fails before it runs; the mod's own file writes stay fenced by the FileWrites check; D27's fails-when names it
- F8 · accepted · D14 compares the sorted in-game name list (verbose boot line) with the sorted CHAR_ files of the prefab dump whose SpawnBuffElement has a Hiding entry; the record holds both lists and their diff, which must be empty, and a non-empty diff fails the item until an amendment classifies each name
- F9 · accepted · D24 becomes a cmd: `pwsh tools/preflight.ps1 -TimingSpan <log copy> -MinTracked 140 -Windows 10` checks ten consecutive post-warm-up windows, the tracked counts and the absence of slow-tick lines in the span, with fixtures good, bad-avg, bad-slow, bad-gap and empty
- F10 · accepted · D34's write set adds every EntityManager Set/Add/Remove/Destroy/Instantiate/CreateEntity call and a DynamicBuffer local written later in the method, with fixtures bad-em and bad-split
- F11 · accepted · D33 adds two AroundPlayer Hunt events on one player: stopping one leaves the other's centre, ledger rows and seeds unchanged
- F12 · accepted · D26: the READMEs say the budget is promised at the default caps and raised caps (151-500 tracked) are best effort, watched by the slow-tick warning
- F13 · accepted · S-6 is relabelled validated by owner decision 3A as a gated scope branch: a no-go amendment removes D2-D5 and needs a fresh READY review before any build past step 1
