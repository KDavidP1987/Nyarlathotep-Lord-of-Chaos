# Reviews: raphael-api-core

## Review 1 · 2026-09-25 · codex · plan commit 4b58cf3
F1 advisory · Blind re-score: Considered—1 (`Purpose & typical use`), 5 (`Interfaces`), 8 (`Use cases › Minimal stretch`), 9 (`Use cases › Maximal stretch`), 15 (`Out of scope`); Gap—2, 3, 4, 6, 7, 10, 11, 12, 13, 14; N/A—none, because the raw commands are human-facing and every other layer applies.
Fix: Update the Coverage table to this assessment, then resolve the probe-specific gaps below.
F2 blocking · Probe 2.1 is unanswered: “anyone” does not enumerate unauthenticated callers, disconnected identities, System, Operator, service/tool callers, or establish that they cannot reach each command; neither `AuthorizationTests` nor preflight is a single command that fails for the complete actor matrix.
Fix: State the complete actor/reachability matrix and add one evidence command whose cases fail whenever any actor gains an unauthorized command, field, subscription, or indirect push path.
F3 blocking · Probe 3.3 is unanswered: `Design › Data` names repository, server, log, world-save, scratch, backup and temporary artifacts but gives no retention owner or deletion rule for logs, sessions, world data, audit records, release artifacts, `.bak/.tmp`, and failed-drill leftovers; `-Paths` and the data inventory do not test retention.
Fix: Give every produced artifact a location, owner, retention/deletion rule and cardinality, with one evidence command that fails when an artifact is absent from that inventory.
F4 blocking · Probe 4.5 is unanswered: D8/D9 cover two derived sets, but the plan never defines and validates the membership computation for “every path,” “every server session,” “every Pusher entry point,” “every transition,” or “every api command,” including untracked, generated, ignored and newly created files.
Fix: Enumerate each every-X set, its derivation and known blind spots, assign its checker, and make fixtures prove the checker sees newly created, untracked, generated and excluded members as applicable.
F5 blocking · Probe 6.1 is unanswered: only VCF has a version, while the game API, Harmony/patch target, git, GitHub CLI/API and Raphael contract lack supported versions, quotas/cost decisions, and sampled input contracts across their record types.
Fix: State the supported version and quota/cost position for every dependency and identify representative contract samples for every external record type.
F6 blocking · Probe 6.2 is unanswered: no single evidence command fails when VCF, the disconnect target, connected-user lookup, git, Codex, or GitHub is slow, unavailable, rate-limited, malformed, or partially successful; “release waits” and try/catch prose do not establish all dependency behavior.
Fix: Decide timeout, retry, abort, cleanup and user-visible behavior for each dependency failure class and add one dependency-failure command that removes each control in turn and fails.
F7 blocking · Probe 7.3 is unanswered: reload staleness and warning cancellation are covered, but the plan makes no decision for interrupted paging/subscription commands, cancellation or undo of queued non-warning pushes, restart during delivery, or what an event correction invalidates.
Fix: State which operations are resumable, retryable, cancellable or undoable and exactly which cached rows and queued messages each correction invalidates.
F8 blocking · Probe 10.1 is unanswered as a control: D7 tests only Subscribe, D10 statically classifies commands, and D1 tests field redaction, but no one command fails for authorization removal on every direct and indirect route, including status redaction, events, subscription mutation, disconnect and push delivery.
Fix: Add one authorization-suite command covering every actor × direct/indirect path and make it fail when any authorization or field-level redaction control is removed.
F9 blocking · Probe 10.3 is unanswered: “gh uses its own stored login” does not decide credential location, rotation/revocation, inherited environment handling, subprocess exposure, or the complete never-log set, while `secrets: none` only scans repository content.
Fix: Specify the credential provider, rotation/revocation procedure and prohibited log/output surfaces, then add one command that fails on seeded token leakage through files, logs, command lines and captured subprocess output.
F10 blocking · Probe 10.4 is unanswered: SteamID minimisation and non-logging are stated, but visibility, deletion timing on every exit path, export/debug exposure and whether subscription changes require an audit trail are not decided.
Fix: Define who may observe the SteamID, its deletion guarantees, all export/debug exclusions, and either the required audit record or an explicit no-audit policy.
F11 blocking · Probe 11.3 is unanswered: the commands are intentionally human-visible, so assigning accessibility to future Raphael panels and saying the wire has no colour does not decide keyboard, screen-reader, wrapping/overflow or small-screen behavior for the current raw-command surface.
Fix: State the accessibility and narrow-display contract for raw command invocation and replies, or establish with a real applicability test that humans cannot use or read this surface.
F12 blocking · Probe 12.3 is unanswered: hook-health and NyarDiag are reactive troubleshooting surfaces, not a decision about the production alert or dashboard that tells the operator the wire, queue, delivery, contract or disconnect cleanup is broken.
Fix: Name the production signal, threshold, destination and owner for each detectable failure, or explicitly decide that operator polling is the monitoring mechanism and define its cadence.
F13 blocking · Probe 12.4 is unanswered: most introduced tests/checks do not specify a failure input, a must-remain-silent input and non-pass empty-input output; AdminList lacks good/empty cases, the rollback drill lacks a silent case, and fixtures are not shown to preserve the real spelling and generated state.
Fix: Add a selftest matrix for every introduced check with fail/silent/empty inputs and real-form fixtures, and provide one selftest command that fails when any required case or gating evidence command is missing.
F14 blocking · Probes 13.1 and 13.2 are unanswered: asymptotic descriptions and row/queue caps do not set a latency or elapsed-throughput budget, and the plan does not give the originating case plus excluded valid case for each limit, especially 10 rows, 480 bytes, 500 tracked units and per-tick fan-out.
Fix: Set measurable latency/throughput budgets and, for every bound, record its source case, behavior at the bound and the valid case intentionally excluded.
F15 blocking · Probe 14.3 lacks verifiable evidence: D18 refers to an unspecified “foundation D38 command,” a future `<pre-child>` value and a revision-range form rather than giving a stranger one executable evidence command; therefore the repository rollback control cannot be independently verified as written.
Fix: Record the concrete base commit and exact disposable-worktree command that reverts the full inclusive child range, builds, tests and compares against that base after persisted-state compatibility has been exercised.
F16 blocking · Probe 14.4 is unanswered: D19 checks membership in a manifest without defining an independent discovery walk, so omitted paths can pass; “six release surfaces” is not an enumeration, and generated, ignored, untracked, external review/release outputs and files created after the scan have no demonstrated visibility.
Fix: Enumerate the six surfaces and define an independent step-derived plus filesystem/git discovery command that fails on omitted tracked, untracked, ignored, generated, review, plan-store and release paths.
F17 blocking · D14 and D15 are unverifiable by their stated `file` evidence: checking two substrings cannot establish the many required table rows, implementation states, deferred-child mappings, parser/state instructions, handshake behavior, panels or §8 workflow, so a stranger could pass both items with nearly empty files.
Fix: Replace each with a structural documentation check that asserts every claimed heading, row, status and required instruction, or narrow the D-item claims to what the file evidence actually verifies.
F18 advisory · S-3, S-4 and S-5 are not cheaply reversible as written: changing not-ready output or ending-row semantics changes the published api-2 contract and consumers, while adding a queue configuration key creates configuration compatibility and documentation work.
Fix: Treat their current behavior as explicit versioned decisions and require compatibility review for later changes; reserve `reversible` for implementation choices without contract or schema consequences.
5/15 layers · 33/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · the re-score is answered by F2–F17's fixes; each gap it names is closed below
- F2 · accepted · Design › Permissions gains the actor matrix; D7 becomes one authorization-suite command over every actor and path
- F3 · accepted · Design › Data gains the artifact table (location, owner, retention, copies); D19's -Paths walk fails on an unlisted path
- F4 · accepted · Business rules 7 lists each every-X set, its derivation and blind spot; D8 adds the "[NYAR:" literal rule
- F5 · accepted · Interfaces › External gains the dependency table with versions, cost and sampled inputs
- F6 · accepted · +D21 dependency-failure tests; the table states each dependency's failure behaviour
- F7 · accepted · Design › States states the 7.3 decisions: stateless paging, idempotent sub, which corrections invalidate what
- F8 · accepted · D7 widened (see F2)
- F9 · accepted · Security 10.3 names the gh credential store, rotation, and that no script reads it
- F10 · accepted · Security 10.4 states visibility, deletion paths, no export, and the no-audit policy
- F11 · accepted · Design › UX states the raw-surface behaviour (plain lines, client wrap)
- F12 · accepted · Failure & observability 12.3 states operator polling as the monitoring decision and the signals
- F13 · accepted · selftest matrix added; AdminList already has good and empty fixtures; the drill gains -SelfTest with fixtures (D16)
- F14 · accepted · Performance gains the budget (+D22) and a bounds table with source and excluded cases
- F15 · accepted · D18 now carries the full worktree command over v0.2.1..v0.3.0
- F16 · accepted · the six surfaces are enumerated in Paths walked; -Paths is a discovery walk of tracked, untracked, ignored and server files, run last
- F17 · accepted · D14 is now a test (ContractDocTests); D15 lists every required heading
- F18 · accepted · S-3 and S-5 fallbacks now state the api 3 bump and contract note they need
