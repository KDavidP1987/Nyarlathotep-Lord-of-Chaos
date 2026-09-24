# Reviews — foundation

## Review 1 · 2026-09-24 · codex · plan commit 5f9855a
F1 blocking · Probe `3.1` is unanswered: the plan does not specify shapes and ranges for several command/config inputs, including `.nyar spawn` count/level/multipliers, pagination, warning offsets, daily time, and purge confirmation timing, so the Build plan is not executable without inventing interface policy. — enumerate every command and configuration input with syntax, defaults, ranges, validation reply, and authoritative source.
F2 blocking · Probe `3.3` is unanswered: D34 proves inventory-entry presence, not ownership, retention, or deletion for each artifact; `stats.json`/`zones.json`, release archives, deployed DLLs, generated review pages, logs, and test outputs lack enforceable lifecycle evidence. — complete the artifact table and add one command-backed inventory check that fails when any artifact lacks owner, retention, deletion, and copy-count fields.
F3 blocking · Probe `4.4` is unanswered: the stated precedence omits who can authorize exceptions, while `unitLifetimeSeconds`, “event end + GraceSeconds,” purge, and cap behavior can conflict without a declared winner. — state that exceptions are forbidden or name the deciding actor, and define/test precedence for explicit unit lifetime versus event-end cleanup.
F4 advisory · Probe `4.3` is decided but internally suspect: storing only `last-fired UTC` cannot by itself suppress the second occurrence of an identical server-local minute during a DST fallback. — specify a local schedule-occurrence key, such as local date plus configured time, and make D4 exercise both UTC instants of the repeated minute.
F5 blocking · Probe `6.1` is unanswered: the dependency table does not enumerate or sample the actual contracts used from all V Rising systems, VCF command metadata, connected-user records, death events, or day/night records. — name every consumed symbol/record shape and map each to a compile, unit, spike, or in-game evidence case.
F6 blocking · Probe `6.2` lacks a single control command: D9 covers disk, JSON, and hooks, but not the declared VCF, NuGet, Codex/preflight, connected-user, or game-system failure modes. — assign each dependency an explicit failure policy and provide one evidence command that fails when any declared dependency has no exercised slow/down/garbage case.
F7 blocking · Probe `12.4` is unanswered: D18 self-tests only two new checks, while D19, D33, D34, D37, and D38 introduce checks without stated good, bad, and empty fixtures or empty-input output. — put every introduced check in the self-test manifest with faithful good/bad/empty fixtures and make `preflight.ps1 -SelfTest` fail on missing cases.
F8 blocking · Probe `12.4` makes D19 unverifiable: `preflight.ps1 -ListCommands admin` runs against the repository, but its fails-when clause requires a planted fixture that the stated command never selects. — add a fixture/root parameter exercised by `-SelfTest`, or remove the fixture claim and provide another command that demonstrably tests omission detection.
F9 blocking · Probe `12.4` makes D33 unverifiable: one current `LogOutput.log` cannot establish “every in-game session,” and permitting unrecorded sessions as “not checked” directly contradicts the item. — either narrow D33 to every recorded test session or archive and command-check each session log before restart, failing on any missing record.
F10 blocking · Probe `13.2` is unanswered: limits are listed, but most lack the case that produced the bound and the valid case excluded; `event list` says “10 per page” without page syntax or boundary behavior. — document provenance, bound behavior, excluded valid case, and pagination contract for each applicable limit.
F11 blocking · Probe `14.3` makes D38 unsafe and unverifiable: after `git worktree add`, the command never changes into the disposable worktree, so `git revert` and `git diff` run in the caller’s repository; it also drills only `<drill base>`, not necessarily the advertised `<pre-child>..<v0.2.0` range. — execute every revert/build/check inside the verified disposable-worktree path and drill the exact recorded release range.
F12 blocking · Probe `14.4` is unanswered: D34 runs after operations and cannot reconstruct all paths they walked, including removed temporary worktrees, server paths, tag/ref writes, GitHub assets, ignored outputs, and regenerated review records. — make each build/deploy/review/release operation emit a path trace consumed by one manifest command that fails on any unlisted path.
F13 advisory · Probe `5.2` is nominally answered, but the Build plan never explicitly wires EventStore, TriggerBus, scheduler, runtime, announcer, health monitor, patches, and persistence into Plugin/Core startup, shutdown, and tick order. — add a wiring step naming the modified bootstrap files, construction order, hook registration, tick ownership, readiness transition, and disposal behavior.
F14 advisory · Probe `4.5` is weaker than claimed: D18 promises out-of-directory fixtures only for GatewayOnly and FaultInjection, not for every walk named under “Every X,” so command, patch, template, and marker-set completeness can regress undetected. — add faithful outside-expected-directory and untracked-not-ignored fixtures for every derived set, or narrow the prose to the walks actually self-tested.
9/15 layers · 40/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · Design › UX gains a command table (syntax, defaults, ranges, who, replies incl. event list paging and the 30 s purge confirm window) and a cfg table (every new key with default and range); CommandArgs validation is D5
- F2 · rejected · Test-CheckDataInventory (tools/preflight.ps1:637) already fails when any entry lacks location, owner, retention, deletion or singleCopy and when any non-tracked manifest glob or Persistence constant has no entry; stats.json and zones.json are in the existing entry; test outputs are covered by **/bin/** and **/obj/**; review pages and plans are tracked files outside the inventory by design. D34's text now states the field check explicitly
- F3 · accepted · Business rules 1 states there are no exceptions and only the operator changes a cap, within its ceiling; rule 2 defines unit lifetime as min(own, event end + GraceSeconds − now) and the manual-spawn lifetime; D3 tests both
- F4 · accepted · schedules are keyed by the occurrence (local date + HH:mm) stored in state.json; D4 exercises both UTC instants of a repeated minute
- F5 · accepted · Interfaces gains a table of game and library contracts consumed, each compiled by D1 and behaviour-proven by a named item
- F6 · accepted · runtime dependencies are enumerated as Logic/Dependency with a DependencyPolicy row each, and D9 fails when a member has no fault case; build-time and tooling dependencies are stated to fail closed before shipping (D1, plugin load, dod review.md)
- F7 · accepted · in part: D19's admin list and D33's session logs become self-tested checks (Test-CheckAdminList, Test-CheckSessionLogs) with good, bad and empty fixtures under D18; D34 and D37 use the Epic's existing self-tested checks; D38's failure signal is git's and dotnet's exit codes, no new check is introduced
- F8 · accepted · the omission case moves to Test-CheckAdminList's bad fixture (an admin command planted outside Commands/), run by -SelfTest
- F9 · accepted · D33 is now every session recorded under FOUNDATION.md › Test results with a matching log check line in the audit, checked by -SessionsOf foundation; the 'not checked' allowance is removed
- F10 · accepted · Performance is now a table of every bound with its provenance, behaviour at the bound and the excluded valid case; event list paging is specified in the UX table
- F11 · accepted · D38 runs every command against the worktree path (git -C, the worktree's solution and preflight) and drills the exact release range <pre-child>..v0.2.0 from the tag
- F12 · accepted · in part: D34 adds the -ServerWrites before/after compare (snapshot before step 2's first boot, compare after step 8) so every server and LocalLow write is traced; the removed worktree, .git/worktrees and the tag are named in Paths walked; a per-operation trace of transient paths is disproportionate since -Paths and -ServerWrites see every path that persists
- F13 · accepted · Design › States gains Startup and shutdown (construction order, tick ownership, IsReady, Unload); steps 2, 4, 5 and 6 name the Core.cs and Plugin.cs wiring
- F14 · accepted · Business rules 7 now names which walk each fixture self-tests; templates are enumerated at run time by D15 and markers are exercised in game by D21

## Review 2 · 2026-09-24 · codex · plan commit d52bc6e (revision after Review 1)
F1 blocking · Probe `6.1` remains unanswered: the contract table still names only “the server’s day/night state” and “the buff apply path,” without the consumed symbols or record shapes, so compilation cannot validate that the intended game contracts were selected. — name the exact day/night and buff-application types, members, and sampled fields, and map each to D1 plus an in-game evidence item.
F2 blocking · Probe `6.2` remains unanswered: D9 enumerates only `Logic/Dependency`, while declared dependencies such as VCF registration/permission enforcement, NuGet restore, plugin hard-dependency loading, Codex review, and preflight itself have prose policies but no exercised slow/down/garbage cases under the single control. — expand the dependency enumeration and D9/self-test evidence so every declared runtime and tooling dependency has an explicit failure policy and an executable adverse case.
F3 blocking · Probe `13.2` remains unanswered: provenance and excluded-valid-case analysis is supplied for major caps, but not for numerous enforced limits including identifiers, names, durations, schedule counts, conditions, spawn level/multipliers/radius, purge confirmation, debug radius, warning offsets, share limits, and announcement length. — enumerate every enforced bound with its originating case, boundary behavior, and a valid excluded case—or explicitly classify and justify which constraints are validation syntax rather than scale limits.
F4 blocking · Probe `14.4` remains unanswered: D34’s before/after server snapshot cannot observe a path created and deleted between snapshots, while `-Paths` is not specified as consuming an operation-emitted trace; it therefore cannot verify “every walked path,” removed worktrees, Git refs, or GitHub release assets. — make each build, test, deploy, review, and release operation append normalized filesystem/ref/remote paths to a trace, then have one D34 command compare that trace with the manifest and fail on an unlisted path.
F5 advisory · Probe `4.3` has a concrete policy, but its persisted representation contradicts it: Business rule 5 stores a local date-plus-time occurrence key, while `state.json v1` defines only `lastFired: { eventId: utc }`. A restart between the two UTC instants of a repeated DST minute therefore has no specified durable key with which to suppress the second firing. — put the local occurrence key in the state schema and make D4 cover restart between both UTC instants.
F6 advisory · Probe `14.3` is materially improved, but D38 uses a fixed temporary path without verifying absence or enabling fail-fast behavior. If `git worktree add` fails because that path already exists, later commands can operate on unrelated contents and `remove --force` can remove that worktree. — create and validate a unique temporary directory, stop on the first nonzero exit, verify `git -C <path> rev-parse HEAD` equals `v0.2.0`, and remove only the path created by the drill.
F7 advisory · Probe `12.4` is answered by D18, but the closing sentence says “the two new checks” although D18 and Build step 3 introduce four. That ambiguity can make the implemented fixture manifest narrower than the acceptance item. — change it to “four new checks” and name GatewayOnly, FaultInjection, AdminList, and SessionLogs consistently.
F8 advisory · Probe `4.5` is answered sufficiently to score, but templates and marker discovery still lack the same planted outside-directory and untracked-not-ignored omissions promised for code walks; D15’s fixture pool and D21’s successful marker sweep do not demonstrate completeness when a valid source or marker form is omitted. — add omission fixtures for every derived template source and marker kind, or narrow the completeness claim to the explicitly enumerated sources and registered marker values.
EARLIER: F1 resolved—the UX and configuration tables now give syntax, defaults, ranges, and invalid replies; F2 resolved as written—D34 requires location, owner, retention, deletion, and singleCopy and fails on a missing field, though repository execution could not be independently run because this review environment rejected all filesystem commands; F3 resolved—exceptions are forbidden and event end wins over unit lifetime; F4 not resolved—the durable schema still stores UTC rather than the declared local occurrence key; F5 not resolved—the day/night and buff contracts remain unnamed; F6 not resolved—D9 still excludes several declared runtime/tooling dependencies; F7 resolved—D18 covers all four checks introduced by this child with good, bad, and empty fixtures; F8 resolved—AdminList now shares the repository walk and its omission fixture is selected through SelfTest; F9 resolved—sessions are recorded and must be log-checked before restart, with SessionsOf enforcing correspondence; F10 not resolved—many enforced bounds still lack provenance and an excluded valid case; F11 resolved—the exact pre-child-to-v0.2.0 range and `git -C $wt` are now used, subject to F6’s fixed-path safety advisory; F12 not resolved—snapshots and a static manifest still do not capture every runtime-walked, transient, ref, or remote path; F13 resolved—the startup construction order, hook registration, tick ownership, readiness transition, and shutdown sequence are explicit; F14 partly resolved—the code walks gained outside-directory fixtures, but template and marker completeness still lacks equivalent omission evidence.
12/15 layers · 45/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · the contract table names ServerGameManager.InstantiateEntityImmediate and TryInstantiateBuffEntityImmediate with their shapes, and the ProjectM.DayNightCycle singleton read through DayNightCycleExtensions.IsDay (both identifiers confirmed in ProjectM.Shared.dll of 1.1.12-r99041-b2), each mapped to D1 and an in-game item
- F2 · accepted · in part: VCF permission enforcement is exercised adversely by D26 and command registration is a D9 member; NuGet restore, VCF presence, Codex and preflight are tooling that fails closed before shipping, stated in Interfaces with the gate each failure stops — a simulated-outage harness for build tooling is disproportionate (as settled for spikes, Review 1 F7)
- F3 · accepted · Performance classifies every other range as a validation limit with its provenance (game, Epic rule, readability) and says a wider range is an amendment; scale limits keep the per-bound table
- F4 · rejected · a path created and deleted between snapshots leaves no data, which is what 14.4 protects (settled for spikes, Review 3 F1); -Paths walks every tracked, untracked and ignored repository path and -ServerWrites every server and LocalLow file that persists; the tag and GitHub asset are verified by D37 and the temporary worktree by git worktree remove in D38. Author and reviewer lines stand side by side
- F5 · accepted · state.json lastFired stores the local occurrence key with the UTC instant, and D4 covers a restart between the two UTC instants
- F6 · accepted · D38 uses a unique GUID path, stops on the first failure, verifies the worktree HEAD equals v0.2.0 and removes only that path
- F7 · accepted · the sentence names the four new checks
- F8 · accepted · the completeness claim is narrowed to three enumerated template sources and the registered marker values of Logic/Markers (added to step 4)

## Review 3 · 2026-09-24 · codex · plan commit d134c35 (revision after Review 2)
F1 blocking · Probe `6.2` remains unanswered for tooling and load-time dependencies: D9 enumerates only `Logic/Dependency`, so its command cannot fail when NuGet stalls, VCF hard-dependency loading fails, Codex returns garbage, or preflight hangs; “fails closed before shipping” is policy, not an exercised adverse control. — enumerate runtime, load-time, and tooling dependencies in a machine-readable policy and make one D9/self-test command simulate each applicable slow/down/garbage case and verify the declared fail-closed or fallback behavior.
F2 blocking · Probe `12.4` is unanswered for the new `-LogCheck` control: D18 self-tests SessionLogs but not LogCheck, while D33 supplies failure cases without a good/silent fixture or an empty-input diagnostic guaranteed not to read as a pass. — add `Test-CheckLogCheck` with real-format good, stack-frame bad, missing-`[nyar` bad, and empty/missing-log fixtures, register it in `preflight-checks.json`, and include it in D18’s single self-test command.
F3 blocking · Probe `14.4` remains unanswered: D34 runs before step 9 and has no operation trace, so `-Paths` cannot fail when D38 creates and removes an unlisted worktree or `.git/worktrees` entry; it also delegates the tag and GitHub asset to D37 instead of providing the required single paths control. — have every build, review, deploy, rollback, tag, and release operation append normalized filesystem/ref/remote targets to a trace, then make D34’s single command compare that trace with the manifest after step 9.
F4 advisory · A second corrupt `state.json` is an unhandled repeated-use scenario under `3.3`: the plan says one fixed `state.json.corrupt` is kept until the next corruption but does not specify whether that existing file is replaced, rotated, or causes the rename/load path to fail. — state the collision policy and add a persistence test that loads two successive corrupt state files without preventing startup.
F5 advisory · D15 does not fully verify its `10.4` privacy claim: rejecting position-shaped parameters cannot detect a message builder that reads coordinates indirectly from captured state, a service, or a globally accessible object. — test emitted player-facing messages with planted coordinate values and assert that no coordinate or radius representation reaches chat or logs.
F6 advisory · D38 leaves its disposable worktree behind precisely when `revert`, build, or preflight fails because fail-fast exits before `git worktree remove`; this weakens the interruption/re-entry answer under `7.3`. — wrap creation and drill execution in `try/finally`, removing only the validated path created by this invocation.
F7 advisory · D37 verifies only the GitHub asset’s filename, so a stale or unrelated archive named `kdpen-Nyarlathotep-0.2.0.zip` satisfies the evidence even though the item claims the release carries this build’s tcli zip. — record the locally built archive hash and have the release evidence download the asset and compare hashes.
EARLIER: F1 resolved—the exact day/night and marker-buff symbols, members, sampled fields, versions, D1 compilation, and in-game evidence are now named; F2 unresolved—the partial acceptance leaves tooling/load-time adverse cases outside D9’s executable enumeration; F3 resolved—the remaining limits are explicitly classified as validation limits with provenance categories, rejection behavior, and amendment treatment for otherwise-valid wider values; F4 unresolved—the rejected position does not let D34 observe removed worktrees/ref/remote operations or provide one command covering them; F5 resolved—the durable local occurrence key is now in `state.json v1`, and D4 covers restart between repeated-minute instants; F6 resolved—the drill uses a GUID path, fail-fast native-command handling, HEAD verification, and removes the created path, though failure cleanup remains advisory; F7 resolved—the four checks are named consistently; F8 resolved—the completeness claim is narrowed to three enumerated template sources and registered marker values, with empty-source failure and the marker exercised in game.
12/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · rejected · owner decision 2026-09-24: tooling and load-time dependencies keep their fail-closed policy (each failure stops the gate it feeds before anything ships); runtime dependencies are enumerated and exercised by D9, VCF permission by D26; a tooling fault harness is out of proportion (as for spikes)
- F2 · accepted · Test-CheckLogCheck is the function behind -LogCheck, with good, stack-frame bad, missing-nyar bad and empty/missing-log fixtures, listed in D18 and step 3
- F3 · rejected · owner decision 2026-09-24: 14.4 is answered by walking what persists (-Paths over tracked, untracked and ignored repository paths; -ServerWrites over the server and LocalLow; D37 over the tag and the release asset by hash); step 9 now reruns -Paths and git worktree list after the drill; an operation trace of transient paths is out of proportion
- F4 · accepted · a new corruption replaces the earlier state.json.corrupt; D8 loads two successive corrupt files and both start
- F5 · accepted · D15 also seeds every builder state source with planted coordinates and fails if one reaches an emitted player-facing line
- F6 · accepted · D38 wraps the drill in try/finally, removing only the validated GUID path
- F7 · accepted · D37 downloads the release asset and compares its SHA-256 with the local build's, recorded in the step 8 audit

## Review 4 · 2026-09-24 · human · plan commit 7910a22
F1 advisory · Coverage: the project owner, having read Codex rounds 1-3 and the author's dispositions, accepts the plan as READY; the two declined asks (a tooling fault-injection harness for 6.2 and an operation-level path trace for 14.4) are accepted as out of proportion, with 6.2 answered by D9 (runtime), D26 (VCF permission) and the fail-closed tooling policy, and 14.4 by -Paths, -ServerWrites, D37 and the post-drill walk; every item verifiable by its evidence type — no blocking gap
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · accepted · no change; anything the build reveals is recorded as an amendment before it is built

## Review 5 · 2026-09-24 · codex · plan commit 8815d4a (amendment A1)
F1 blocking · Probe 14.4 is unanswered: D34 requires both `pwsh tools/preflight.ps1 -Paths` and `-ServerWrites -Compare <snapshot>`, while its gating matrix names only `-Paths`; moreover the amended check accepts any manifest-covered save, so adding `server: save-data-other/**` makes an unauthorized world pass despite D34’s “only … save-data-nyardev” claim. — provide one 14.4 evidence command that walks repository and server writes and fails unless the complete Saves-folder set is exactly the owner’s untouched LocalServer plus `save-data-nyardev`.
F2 blocking · Probe 12.4 is unanswered for A1: changing `Test-CheckServerWrites` introduces new check behavior, but D18’s `pwsh tools/preflight.ps1 -SelfTest` covers only five other named checks; A1 specifies only an undeclared-save bad fixture, not real-spelling good, bad, and empty fixtures with empty producing a non-pass result. — add `Test-CheckServerWrites` to the self-test manifest with a real-format declared-nyardev good fixture, both undeclared and misleadingly manifested-other-save bad fixtures, and an empty snapshot/compare fixture whose output is explicitly not a pass.
F3 blocking · Probe 6.2 is unanswered: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` exercises runtime dependencies only; the stated Codex-timeout fallback and preflight-failure behavior are prose, with no single evidence command that fails when either internal collaborator mishandles timeout, malformed output, or failure. — name one executable fault test that injects Codex timeout/malformed verdict and preflight failure and verifies the documented fallback or build stop.
F4 blocking · D34 is not verifiable by its stated `cmd` evidence: `-ServerWrites -Compare` proves manifest coverage, not the stronger amended assertion that no Saves folder other than the owner’s LocalServer and `save-data-nyardev` exists; an unchanged pre-existing save or an extra manifested save can satisfy the printed success line. — make the command enumerate all Saves folders under both roots independently of the change diff and reject every identity outside the exact allow-list.
F5 blocking · Probe 14.4 also leaves Build step 2 non-executable from the plan alone: it supplies relative `save-data-nyardev` and log paths but no exact dedicated-server root, executable/start command, or concrete snapshot pathname to substitute for `<scratch file>`. — state the approved absolute development-server root, exact launch/stop commands and working directory, and a deterministic snapshot path subsequently passed to D34’s compare command.
F6 advisory · A1 is not honestly just `discovered`: discovering the missing test world exposed the issue, but choosing a persistent cross-child world, its retention policy, and a generalized manifest-driven authorization rule are new policy and scope decisions. — split the record into the discovered gap and an owner-approved decision amendment, or classify A1 under the project’s decision/scope category with its approver.
F7 advisory · Minimal-stretch scenario, probes 4.5/14.4: an unrelated Saves folder already present and unchanged between snapshots can be invisible to a change-only comparison. — inspect the complete post-run Saves-folder set, not merely created, changed, or deleted files.
F8 advisory · Maximal-stretch scenario, probes 4.5/14.4: a builder can add a broad `server:` glob covering multiple development worlds, after which the amended check authorizes all of them and its planted “undeclared” fixture no longer models the prohibited case. — self-test manifest broadening and enforce the exact permitted world identity separately from ordinary path coverage.
F9 advisory · Concurrent/failure scenario, probe 6.2: a reviewer timeout or malformed verdict concurrent with a gated post-audit has no tested rule establishing whether the step stops, retries, or accepts the fallback verdict. — add the documented orchestration outcome to the tooling fault fixture and audit assertion.
12/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · the 14.4 row names -Paths and -ServerWrites -Compare $env:TEMP\nyarfoundation-before.tsv; the Saves allow-list is exactly save-data-nyardev plus the untouched LocalServer, independent of manifest globs
- F2 · accepted · ServerWrites (already in tools/preflight-checks.json with good, bad and empty fixtures since spikes) gets a nyardev good fixture and bad-9 (undeclared), bad-10 (manifested) and bad-11 (unchanged) other saves; empty is not a pass; -SelfTest must pass in step 2
- F3 · rejected · re-raises probe 6.2 for tooling dependencies, settled by owner decision 2026-09-24 (Review 3 F1, Review 4); A1 changes no tooling dependency
- F4 · accepted · the check reads every Saves folder of the whole after snapshot, not only the change diff, and D34's fails-when names an unchanged or manifested other save
- F5 · accepted · step 2 names the server root C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer, the launch command, the stop (Ctrl+C, then tasklist) and the snapshot path $env:TEMP\nyarfoundation-before.tsv (as spikes used $env:TEMP\nyarspikes-before.tsv)
- F6 · rejected · lifecycle's test for discovered is that the plan missed something the build needs; the missing test world is that, and the chosen world is the design change it records (one design change); retention across children is reversible (the owner may delete the folder at any time) and the owner's standing instruction authorises plan revisions found in the build
- F7 · accepted · with F4
- F8 · accepted · with F1 and F2 (bad-10)
- F9 · rejected · with F3

## Review 6 · 2026-09-24 · codex · plan commit 74b4871 (round 2 of the A1 re-review)
F1 blocking · Probe 12.4 remains unanswered for the amended `Test-CheckServerWrites`: Build step 2 specifies good, bad-9, bad-10, bad-11, and empty fixtures, but D18’s authoritative list of checks covered by `pwsh tools/preflight.ps1 -SelfTest` still omits `Test-CheckServerWrites`, so a stranger cannot verify that the manifested-other-save and unchanged-other-save cases are actually registered and executed. — add `Test-CheckServerWrites` to D18 and its self-test manifest, explicitly requiring the real-format good fixture, bad-9 through bad-11, and an empty fixture whose result is not a pass.
EARLIER: F1 resolved — D34 now requires both `-Paths` and `-ServerWrites -Compare`, with save-folder authorization independent of manifest coverage.
EARLIER: F2 not resolved — step 2 describes the fixtures, but D18 still excludes `Test-CheckServerWrites` from the self-tested checks.
EARLIER: F3 resolved — the recorded owner decision keeps tooling dependencies fail-closed and outside A1’s scope.
EARLIER: F4 resolved — D34 now checks the complete after-snapshot Saves-folder set, including unchanged and manifested folders.
EARLIER: F5 resolved — step 2 names the server root, working directory, launch and stop procedure, and `$env:TEMP\nyarfoundation-before.tsv`.
EARLIER: F6 resolved — A1 records the discovered missing-world gap and makes one reversible retained-world choice with a defined deletion point.
EARLIER: F7 resolved — bad-11 and D34 cover an unrelated unchanged Saves folder.
EARLIER: F8 resolved — bad-10 proves that a broad manifest glob cannot authorize another save identity.
EARLIER: F9 resolved — the existing owner decision governs reviewer timeout and malformed-output handling; A1 changes no tooling dependency.
14/15 layers · 48/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · D18 lists Test-CheckServerWrites (changed by A1) with its good, bad-9, bad-10, bad-11 and empty fixtures; A1's ops are ~D34 ~D18

## Review 7 · 2026-09-24 · codex · plan commit 05f7494 (round 3 of the A1 re-review)
EARLIER: F1 resolved — D18 now explicitly registers `Test-CheckServerWrites` with the real-format good fixture, bad-9 through bad-11, and a non-passing empty fixture under `pwsh tools/preflight.ps1 -SelfTest`.
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- none · no findings

## Review 8 · 2026-09-24 · codex · plan commit eb3a589 (amendments A9, A10)
F1 blocking — Probe `4.4` is unanswered because the current D27 requires “no DontSaveEntity” while the still-actionable Baseline D27 requires `DontSaveEntity`, and current D33 conflicts similarly with Baseline D33; a context-free builder has no stated authority rule for these duplicate requirements.  
Fix: State that the top Definition of Done plus amendments are normative and Baseline is immutable historical evidence, or update/remove the contradictory Baseline items.

F2 blocking — Probe `3.3` leaves a persistence path unanswered: after the first boot sweep destroys a saved unit, one of its saved child entities could survive into the next autosave and produce an orphan only on the second restart; D27 stops after the first restart and therefore cannot verify deletion of every persisted by-product.  
Fix: Define that despawning a marked unit must remove all owned child entities, then autosave after the completed sweep, restart a second time, require `marker sweep: 0 found`, zero tracked units, and `-LogCheck` with zero orphan errors.

F3 blocking — Probe `12.4` is defeated by D33’s exception: `-SessionsOf foundation` may pass a nonzero orphan count whenever the line cites any existing amendment, so the stated command does not fail when the “no orphan errors” control is absent.  
Fix: Require every post-A10 session to record `0 orphan errors`; preserve historical nonzero results in separately labelled pre-A10 records that the acceptance command cannot treat as passing sessions.

F4 blocking — Probe `12.4` does not specify an A10-complete `Test-CheckLogCheck` fixture contract: D18 and Build step 2 still describe only BepInEx good/frame/no-nyar/empty inputs, while A10 merely says “new fixtures” without requiring a real-format server-log orphan fixture that makes the check fail.  
Fix: Amend D18 and step 2 to name a two-log good fixture, an orphan-pattern server-log bad fixture, a non-orphan Unity-error silent fixture, and independently missing/empty BepInEx and server-log fixtures.

F5 blocking — D27 is unverifiable as written: its manual procedure does not expose or inspect `Age`, `LifeTime`, `DestroyWhenDisabled`, or absence of `DontSaveEntity`, so a stranger can observe expiry and a sweep while several claimed recipe components are absent.  
Fix: Add an explicit debug/component inspection command or integration assertion that prints and checks those four components before the save, alongside the behavioral restart evidence.

F6 advisory — Scenario for probes `4.5`/`12.3`: A10 defines “orphan error” using five substrings, so an equivalent Unity entity-remap failure with different wording is counted only as a Unity error and does not fail D33.  
Fix: Document the classifier as intentionally limited to those signatures and review every distinct Unity-error kind, or classify the broader entity-link/remap family as fatal.

F7 advisory — Scenario for probe `9.1`: D21/D27 exercise 30 or 3 units, while the maximal 500-unit restart can leave the sweep draining for many ticks and expose shutdown-during-drain behavior not covered by the persistence evidence.  
Fix: Add a restart-during-sweep case at the tracked-unit ceiling and confirm the following boot safely requeues remaining markers without duplicates.

Blind score: 1 Considered—Purpose & typical use; 2 Considered—Permissions/D10/D26; 3 Gap—`3.3`; 4 Gap—`4.4`; 5 Considered—Interfaces; 6 Considered—External dependencies/D9; 7 Considered—States/D6/D16/D21; 8 Considered—Minimal stretch; 9 Considered—Maximal stretch; 10 Considered—Security/D10/D11/D14/D17; 11 Considered—UX; 12 Gap—`12.4`; 13 Considered—Performance/D24; 14 Considered—Rollout/D34/D38; 15 Considered—Out of scope.

12/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · rejected · the dod format settles it: ## Baseline is the frozen copy the report measures against and is never edited; the current Definition of Done with its amendments is what the build satisfies (Reviews 5-7 re-reviewed amended items the same way)
- F2 · accepted · D27 adds a second restart after the drain and the next autosave: the sweep shows 0 queued, status 0 tracked, -LogCheck 0 orphan errors
- F3 · accepted · the citation exception is gone: any orphan count above 0 fails; sessions 6 and 7, recorded before A10, keep the old line format with the server log's counts in a sub-bullet
- F4 · accepted · D18 names the A10 fixtures of both checks (LogCheck good with a silent non-orphan Unity error, bad-4 to bad-7; SessionLogs bad-4 to bad-7)
- F5 · accepted · `debug here` prints "recipe ok" or the missing components (LifeTime, Age, DestroyWhenDisabled, DontSaveEntity present), Logic/AdminLines.Recipe with tests; D27 requires it
- F6 · accepted · D33: the session entry attributes every Unity error kind -LogCheck lists, which is how an entity-link failure of another wording is caught
- F7 · rejected · the boot sweep rebuilds its queue from the markers it finds on every boot and the ledger starts empty, so a restart during a drain re-finds the remaining units without duplicates; the 500-unit ceiling is D24's performance case in step 5, not A9's persistence change

## Review 9 · 2026-09-24 · codex · plan commit acc8c3d (round 2 of the A9/A10 re-review)
1. F1 blocking — Probe `4.4` remains unanswered because the current D27/A9 forbids `DontSaveEntity` while Baseline D27 requires it, and current D33/A10 conflicts with Baseline D33; the document never states that current DoD plus amendments supersede the still-checkboxed Baseline, so a context-free builder lacks an authority rule.  
Fix: State that `## Baseline` is immutable historical text and non-actionable, and that the current DoD as modified by amendments is authoritative.

2. F2 advisory — Probe `4.5` scenario: the boot sweep derives “every surviving unit” from surviving marker-buff entities, so a saved unit whose marker child is absent or damaged is invisible to the sweep; D27 samples three intact recipes but does not define or detect this reverse-orphan case.  
Fix: Define whether an unmarked surviving Nyarlathotep spawn is possible and, if so, add an independent durable identity or a negative restart fixture proving parent and marker persistence are inseparable.

3. F3 advisory — Probe `12.4` scenario: D33 permits pre-A10 session lines to omit orphan counts and place the server-log result in a sub-bullet, but its stated `-SessionsOf` failure contract does not say that a nonzero orphan count in that sub-bullet fails; a historically dirty session can therefore contribute to `<n>/<n> checked`.  
Fix: Require `Test-CheckSessionLogs` to parse legacy sub-bullets and fail on nonzero orphan counts, or explicitly exclude pre-A10 sessions from the clean-session numerator.

4. F4 advisory — Probes `4.5`/`12.3` scenario: the five literal orphan substrings still miss an equivalent Unity entity-remap failure with different wording, which is counted only as a Unity error and does not fail D33.  
Fix: Define orphan detection structurally or maintain a reviewed error-kind allow-list so unknown entity-link/remap error kinds fail closed.

5. F5 advisory — Probe `9.1` scenario: the maximal 500-unit restart can leave the boot sweep draining for many ticks, but D21/D27 exercise only 30/3 units and never stop the server during that drain; repeated restart during cleanup can therefore leave persistence behavior unverified.  
Fix: Extend the 500-unit run with restart-during-drain and post-autosave restart observations.

EARLIER: F1 not resolved — A9/A10 update the current items, but no explicit rule makes the contradictory Baseline non-actionable.

EARLIER: F2 resolved — D27 now drains after the first restart, waits for the next autosave, and performs a second restart with zero queued units and zero orphan errors.

EARLIER: F3 resolved — D33 now fails nonzero orphan counts, and D18 includes a bad SessionLogs fixture whose line cites an amendment.

EARLIER: F4 resolved — D18 now specifies paired real-format BepInEx/server-log fixtures, including server-log orphan failures, missing and empty logs, and a non-orphan Unity-error silent case.

EARLIER: F5 resolved — `.nyar debug here` now reports “recipe ok” only when LifeTime, Age and DestroyWhenDisabled are present and DontSaveEntity is absent, backed by tested `Logic/AdminLines.Recipe`.

EARLIER: F6 not resolved — orphan classification remains limited to five literal substrings.

EARLIER: F7 not resolved — the 500-unit evidence measures tick performance but still does not exercise restart or shutdown during a maximal drain.

Blind coverage: 1 Considered—Purpose; 2 Considered—Permissions/D10/D26; 3 Considered—Data/D7–D9/D27/D34; 4 Gap—Business rules, probe 4.4; 5 Considered—Interfaces/D5/D8/D12/D27/D29; 6 Considered—Dependencies/D1/D9/D23/D25/D31; 7 Considered—States/D6/D16/D20/D21/D28; 8 Considered—Minimal stretch/D17/D27/D28; 9 Considered—Maximal stretch/D5/D6/D14/D16/D22/D24/D26; 10 Considered—Security/D10/D11/D14/D15/D17/D26; 11 Considered—UX/D17/D19/D23/D26/D29/D30; 12 Considered—Failure & observability/D2/D18/D22/D23/D25/D31/D33; 13 Considered—Performance/D5/D16/D22/D24; 14 Considered—Rollout/D8/D17/D20/D34/D37/D38; 15 Considered—Out of scope.

14/15 layers · 48/49 probes

VERDICT: REVISE
### Dispositions
- F1 · accepted · the Definition of Done now opens with the authority rule: its items as amended are what the build satisfies; the Baseline section is frozen at approval, never built to, and loses where it differs
- F2 · rejected · a saved unit whose marker is missing still carries its own saved LifeTime, Age and DestroyWhenDisabled, so it expires or is destroyed without the sweep; the marker is the sweep's finder, not the only bound (D27's "recipe ok" shows all three per unit)
- F3 · accepted · the pre-A10 sessions are named (foundation 1–7) and the check's pass line says "(<k> before A10, server log not checked)", so they never read as server-log-clean; Codex's code round 2 asked for the same cutoff (fixture bad-8)
- F4 · rejected · with Review 8 F6: D33 has the session entry attribute every Unity error kind -LogCheck lists, which is the reviewed list that catches a new wording; failing closed on every unknown kind would fail sessions on the game's own errors
- F5 · rejected · with Review 8 F7

## Review 10 · 2026-09-24 · codex · plan commit 3006c89 (round 3 of the A9/A10 re-review, the cap)
F1 blocking — Probe `12.4` remains unanswered for pre-A10 sessions: D33 permits their orphan count only in a sub-bullet, while `-SessionsOf foundation` fails only from the main log-check line; therefore known-dirty session 6 can contribute to `<n>/<n> checked`, and a stranger cannot verify “Session logs clean” from the stated command.
Fix: Make `Test-CheckSessionLogs` parse each permitted pre-A10 sub-bullet and fail when its orphan count is nonzero, with a self-test fixture reproducing that exact historical format.

F2 advisory — Probes `4.5`/`12.3`: an equivalent entity-remap failure using wording outside the five literals is merely counted and attributed as a Unity error, so D27 can report “0 orphan errors” despite evidence of a Nyarlathotep-caused orphan.
Fix: Require attributed Unity error kinds to be classified as game-caused or Nyarlathotep-caused, and make `-SessionsOf` fail any Nyarlathotep-caused entity/remap kind without failing unrelated game errors.

F3 advisory — Probe `9.1`: the plan explains that each boot reconstructs the marker queue, but no evidence interrupts the 500-unit drain with another restart; a defect affecting disabled or already-queued entities could therefore pass D21, D24 and D27.
Fix: Extend the 500-unit run to stop during the drain, restart, and verify the remaining marked units are rediscovered, fully drained, and absent after the following autosave/restart.

EARLIER: F1 resolved — the current Definition of Done explicitly says it supersedes the frozen Baseline wherever they differ, removing the A9/A10 authority conflict.

EARLIER: F2 resolved — the rejection is reasonable: the marker sweep is expressly scoped to marked survivors, while an otherwise valid unit missing its marker remains bounded by its own persisted LifeTime, Age and DestroyWhenDisabled components.

EARLIER: F3 not resolved — the newly named pre-A10 sessions and explanatory suffix identify the exception but still allow a nonzero orphan count in the required sub-bullet to pass `-SessionsOf`.

EARLIER: F4 not resolved — attributing every Unity error kind improves visibility, but nothing makes an attributed Nyarlathotep remap/orphan error with different wording fail.

EARLIER: F5 not resolved — rebuilding the queue explains the intended recovery and D24 covers performance, but neither command verifies a restart while the maximal drain is actually in progress.

14/15 layers · 48/49 probes

VERDICT: REVISE
### Dispositions
- F1 · accepted · with the reviewer's second fix: only sessions after foundation's session 7 count as checked (at least one is required), the pre-A10 sub-bullet's orphan count is parsed and every pre-A10 session with orphan errors is named in the line ("7 before A10 not counted (orphan errors in session 6, 7)"); fixture bad-9 has no post-A10 session. Made after the round cap, so no reviewer has seen it
- F2 · rejected · with Review 8 F6 and Review 9 F4: the attribution D33 requires is where a Nyarlathotep-caused kind of another wording is found, and it is then an unhandled finding recorded by amendment; a machine rule over unknown wordings would have to guess
- F3 · rejected · with Review 8 F7 and Review 9 F5

## Review 11 · 2026-09-24 · codex · plan commit 5b9bed9 (extra round past the cap, owner-approved)
F1 blocking — Probe `12.4`: D33 requires every Unity error kind listed by `-LogCheck` to be attributed, but `-SessionsOf foundation` does not fail when an attribution is absent, so a stranger cannot verify the whole item from its stated evidence.
Fix: Make `-SessionsOf` require one attribution for every distinct listed Unity error kind, and add a self-test fixture containing a listed kind with no attribution.

F2 advisory — Probe `3.3`: D27 exercises persistence with only `CHAR_Bandit_Thug`; a prefab whose saved child or marker topology differs could survive a restart without remaining discoverable by the marker query.
Fix: Add a second fixture unit with materially different child/buff topology to the two-restart D27 exercise, or document repository evidence that all permitted prefabs share the relevant persistence behavior.

F3 advisory — Probe `11.4`: D29 proves that a disabled Schedule twin stays inactive, but does not exercise disabled GameTime or VBloodKilled triggers; a trigger-specific enabled check could regress while the evidence still passes.
Fix: Exercise one disabled definition for each implemented automatic trigger kind, or add a parameterized trigger-activation test covering enabled and disabled definitions.

EARLIER: F1 resolved — D33 now excludes sessions 1–7 from the checked numerator, requires at least one post-session-7 result, parses the legacy orphan sub-bullets, names the dirty sessions, and D18 supplies bad-9 for the no-post-A10 case.

EARLIER: F2 resolved — rejection is reasonable: unknown Unity wording cannot safely be classified mechanically, while D33 requires each listed Unity error kind to be reviewed and attributed; the remaining problem is that this attribution requirement is not enforced by the stated command, captured in current F1.

EARLIER: F3 resolved — rejection is reasonable: the boot sweep reconstructs its queue from persisted markers on every boot, so another interruption does not depend on the previous in-memory queue; D24 separately covers the 500-unit performance ceiling.

14/15 layers · 48/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · the owner chose to enforce it (A13): -LogCheck lists every Unity error kind, Test-CheckSessionLogs fails a line with Unity errors that lists no kinds, leaves a kind without a '  - unity "<kind>": game|ours' sub-bullet, or attributes one to ours without citing an amendment; fixtures bad-10 to bad-12, each caught by a mutation of its rule. Also closes the gap Review 10 F2 pointed at: a Nyarlathotep-caused kind of any wording must be attributed to ours and carry an amendment
- F2 · accepted · no DoD change: step 5's restart test (D21) spawns its wave from a unit prefab other than CHAR_Bandit_Thug, so the boot sweep is seen on a second unit type
- F3 · accepted · D40 (A13): TriggerActivationTests covers enabled and disabled definitions for every automatic trigger kind

## Review 12 · 2026-09-24 · codex · plan commit a30f343 (confirmation of A13, owner-approved)
F1 advisory — Probe `7.3`: a crash or exception after unit creation but before marker attachment can leave a saved, unmarked unit that the boot sweep cannot discover.
Fix: Make incomplete spawn setup despawn the unit, and add a fault case between instantiation and marker attachment.

F2 advisory — Probe `4.3`: changing the server’s time zone can reinterpret persisted local occurrence keys, potentially suppressing or repeating a Schedule occurrence.
Fix: Document occurrence-key behavior after a time-zone change and add a corresponding Schedule test.

F3 advisory — Probe `9.2`: the marker sweep assumes no other mod can use the same inert buff and magic `SpellLevel` value; a collision could despawn another mod’s unit.
Fix: Document collision handling or require an additional ownership discriminator in the sweep query.

EARLIER: F1 resolved — D33 now requires every listed Unity error kind to have a matching attribution, rejects unattributed or unlisted kinds and uncited “ours” attributions, and D18 supplies bad-10 through bad-12 fixtures.

EARLIER: F2 not resolved — D21 using a prefab other than `CHAR_Bandit_Thug` adds useful diversity, but neither the DoD nor the supplied disposition establishes that its saved child or marker topology is materially different; accepting this without a DoD change is tolerable only because the finding remains advisory.

EARLIER: F3 resolved — D40 parameterizes activation across Schedule, GameTime, and VBloodKilled and fails for either a disabled start or a missing automatic-trigger case.

15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · rejected · already built: SpawnTracker.Instantiate discards the unit when Prepare throws (Services/SpawnTracker.cs, "never leave a half-set unit behind"), and instantiation, recipe and marker run in one call on the main thread, so no autosave falls between them; a unit left by a process crash inside that call also carries LifeTime and DestroyWhenDisabled once Prepare has begun
- F2 · rejected · a server time-zone change is an operator action outside the plan's clock cases; the occurrence key is the local date and HH:mm, so at worst one occurrence is fired or skipped, within Business rules 5 (downtime is not replayed); no DoD change
- F3 · rejected · the Epic's Interfaces row "Other mods' units" already states the marker magic values are ours; a collision needs another mod to write SpellLevel 1314472274 on the same inert potion buff, and its units would then be ours to sweep by that contract
