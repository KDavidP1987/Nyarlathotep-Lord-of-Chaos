# Reviews: walkable-spawns

## Review 1 · 2026-09-28 · codex · plan commit afd181a · plan 40696 B · 12 items · files 0 · e3b0c44298fc · prompt 3efd159a1f82
Reviewer: Codex CLI, read-only (`codex exec -s read-only`, Windows sandbox on); the prompt held no Steam ID.

F1 [blocking] Probes `2.1` and `10.1` are unanswered: D9’s `-AuthSuite` only promises to fail when `debug` loses `adminOnly`, not when an indirect path bypasses `Gateway.Run` and reaches `RequestWave`.
Fix: Make `pwsh tools/preflight.ps1 -AuthSuite` fail when any actor or entry path—including scheduler, admin command, job, and direct/indirect wave invocation—can reach placement outside its stated authorization boundary.

F2 [blocking] Probe `3.3` is unanswered: the Data table omits review HTML/pages, `%TEMP%\nyar-session`, release/drill temporary directories, dev-server files touched during sessions, and transient step-created artifacts; D10 only checks declared paths and inventory fields, not actual retention or deletion.
Fix: Enumerate every produced artifact with owner, duration, deletion and single-copy status, and make one command fail when any artifact is absent from inventory or survives past its retention boundary.

F3 [blocking] Probe `6.2` is unanswered: “synchronous, so it cannot be slow beyond the tick” is false—a collision call can stall the tick—and git, gh, tcli, Codex and game APIs lack decisions for timeout, rate-limit and successful-but-garbage responses.
Fix: State timeout/retry/abort and garbage-validation behavior for each dependency, then make `WalkCheck_` or a named dependency command fail when any required behavior is removed.

F4 [blocking] Probe `10.3` is unanswered: D11 claims the environment token is never written or logged, but its commands scan only an unspecified file set plus tracked files, so removing log/temp/output redaction would not necessarily fail.
Fix: Define the complete secret-scanning set—including logs, audit text, prompts, temporary output, packages and release assets—and provide one command that plants a token in each relevant sink and fails.

F5 [blocking] Probe `12.4` is unanswered: the “each check” table omits new checks named by D6, D7, D8, D10 and D11, including WalkHealth, WalkRadius, ChatBytes, audit/session/path/data-inventory checks and privacy scanning.
Fix: Add failure, silent and empty-input cases for every introduced check and register fixtures under `pwsh tools/preflight.ps1 -SelfTest` so that command fails if any bad/empty fixture passes or any good fixture fails.

F6 [blocking] Probe `14.4` is unanswered: the walker expressly cannot see paths created and deleted within a step, and the manifest omits the review-prompt/scratchpad artifacts listed under Data; therefore D10 can pass while shipped or written paths are undeclared.
Fix: Declare every omitted path and instrument creation before deletion—or record a durable per-step write manifest—so `-Paths -DeclaredOf walkable-spawns` fails for transient as well as surviving paths.

F7 [blocking] D11 is unverifiable by its stated evidence: a stranger cannot use repository preflight and `git grep` to verify that `TCLI_AUTH_TOKEN` exists only in the owner’s environment, is never written to disk, or is rotated by the owner.
Fix: Restrict D11 to observable repository/package/log properties and give operational token storage and rotation a separately evidenced manual record or credential-store inspection.

F8 [blocking] D12 is unverifiable by its stated evidence: it says “the owner publishes,” but its commands stop at the GitHub pre-release and do not prove that version 0.5.1 was published to—or deliberately withheld from—Thunderstore.
Fix: Either remove Thunderstore publication from D12 or add a manual/API evidence record identifying the published 0.5.1 package and hash.

F9 [advisory] Concurrent failure and recovery are not exercised under `7.2`: two waves can update the shared failure-streak state in opposite orders, producing duplicate logs, a stale health entry, or premature recovery.
Fix: State whether execution is single-threaded or make streak transitions thread-safe, then add a race-focused test.

F10 [advisory] The maximal-stretch claim under `9.1` is supported only by Session 2 at defaults, so a cap-sized wave performing the worst-case 25 probes per unit may violate the 5 ms budget without being detected.
Fix: Add a cap-sized worst-case benchmark or session measurement and record the degradation behavior at the bound.

F11 [advisory] The unauthorized scenario under `2.2` is asserted through the framework but never observed: a non-admin invoking `.nyar debug walk` could receive the wrong message or expose coordinates while D9 still passes.
Fix: Add a framework-level authorization test or recorded non-admin session that verifies denial and absence of a walk reply.

F12 [advisory] `## Also considered` is silent on compliance/legal, localisation and time formats, running cost/quotas as a distinct decision, analytics/success measurement, and support tooling.
Fix: Add one explicit applicability or decision line for every rubric-listed topic.

F13 [advisory] S-8 is not genuinely reversible as written because its fallback depends on event-spawns first creating per-slug dependency-suite support; the fallback is unavailable during this child’s independent build.
Fix: Provide a fallback this child can implement immediately or classify the suite decision as validated rather than reversible.

9/15 layers · 41/49 probes
VERDICT: REVISE
### Dispositions
- F1 · rejected · advisory by rule: 2.1 and 10.1 are answered; WalkCheck is read-only and placement runs inside SpawnTracker.RequestWave, a dispatched service reached only through Gateway.Run, EventRuntime or the scheduler, and foundation's existing GatewayOnly and admin-list checks under -AuthSuite fail on a bypass (Security 10.1); D9 now says so
- F2 · accepted · narrowly: Design › Data gains the review pages (docs/dod/walkable-spawns.review.html and .html), %TEMP%\nyar-session, the release and drill temp folders and the dev-server files a session touches, each with location, owner, retention and deletion, singleCopy in the inventory; retention beyond what a step leaves is outside the check by design §9 D18
- F3 · accepted · in part: Interfaces 6.2 states the walk check's cost bound (probes per wave ≤ units × 25, 1,250 for a cap-sized wave), timed by foundation's TickTimer and named by event-library D36's slow-tick warning; garbage fails open and is counted (no map data, an out-of-range height level, a NaN point), in D5 and its fails-when; dev-tool failure injection is rejected by reference (event-spawns Review 6 F3: dev tools run only at build time and fail closed)
- F4 · accepted · D11 is restricted to observable repository, package and log properties checked by the existing Secrets check, its fixtures named; token storage and rotation are the owner's, stated in Security 10.3 outside the item
- F5 · accepted · the checks table gains failing, silent and empty-input rows for SpawnPoints, WalkHealth, WalkRadius, ChatBytes and the existing Secrets check, beside the probe records, DebugCommands and the Changelogs known-issue rows; each fixture is registered under -SelfTest and each test row runs there through dotnet test
- F6 · rejected · owner decision design §9 D18 (S-6): paths created and deleted inside one step are outside the declared-paths check; no write journal
- F7 · accepted · with F4: D11 no longer claims what repository evidence cannot show
- F8 · accepted · D12 no longer claims the Thunderstore publication; it ends at the GitHub pre-release and release-verify, and "the owner publishes" is a sentence in Rollout › Shipping outside the item
- F9 · accepted · Design › States 7.2 states that every call runs on the server's main thread (single-threaded), so streak transitions cannot race
- F10 · accepted · by statement: the worst case (a cap-sized wave with every point searched) is bounded by F3's number in Performance 13.1; Session 2 records the timing line of the pond waves
- F11 · accepted · narrowly: Permissions 2.2 states adminOnly is VCF's and the -AuthSuite admin list fails when `debug` loses it; no non-admin session is planned
- F12 · accepted · Also considered gains one line each for compliance, localisation and time formats, running cost, success measurement and support tooling
- F13 · accepted · S-8's fallback is one this child can do now: add walk-check to the fixed $script:DependencyCategories list in tools/preflight.ps1 with a test class, if a suite entry is wanted before event-spawns
