# Reviews: faction-empowerment

## Review 1 · 2026-09-26 · codex · plan: the first draft (uncommitted; the redacted prompt is kept in the session scratchpad)
Blind layer score: 1 Considered—Purpose & typical use; 2 Considered—Design › Permissions; 3 Gap—3.3; 4 Gap—4.1/4.2/4.3/4.5; 5 Considered—Interfaces › Internal; 6 Gap—6.2; 7 Considered—Design › States; 8 Considered—Use cases › Minimal stretch; 9 Considered—Use cases › Maximal stretch; 10 Gap—10.3; 11 Considered—Design › UX; 12 Gap—12.4; 13 Considered—Performance; 14 Gap—14.4; 15 Considered—Out of scope. No layer is legitimately N/A.

F1 [blocking] Probe 4.2 is unanswered: the Epic requires `BuffType.Replace`, but Business rule 1 substitutes “skipping a carried unit” for that setting; preventing stacking is not the inherited buff-type invariant.
Fix: State verbatim that every carrier is created with `BuffType.Replace`, add it to the enumerated carrier contract, and make an automated test fail when another `BuffType` is emitted.

F2 [blocking] Probes 4.1 and 4.3 contain a contradictory lifetime policy: “whole seconds until end,” “at least 1,” and “never exceeds seconds left” cannot all hold when less than one second remains; a late-arrival carrier can therefore outlive the hard event end.
Fix: Decide an exact rounding/cut-off rule—such as skipping applies with under one second remaining or using fractional `LifeTime`—and test the sub-second boundary.

F3 [blocking] Probe 3.3 lacks a single evidence command that fails when its persistence controls are absent: D26 only checks inventory text, while carrier deletion, world-save persistence, instance-row clearing, local ZIP retention, logs, temporary files, and Session-3 plugin cleanup rely on prose or manual evidence.
Fix: Add one executable persistence/drain audit command that plants each produced artifact, verifies its location/owner/copy count, exercises its deletion condition, and fails on leftovers.

F4 [blocking] Probe 6.2 is incomplete: D20 tests only carrier operations and the sweep query, not slow/down/garbage behavior for BepInEx/Harmony, Bloodcraft/KindredCommands, git/gh/GitHub, Codex CLI, or Raphael.
Fix: State the behavior for each dependency condition and attach one executable dependency-failure command that fails when any required containment or abort behavior is removed.

F5 [blocking] Probe 10.3 has no named evidence control: D22 neither promises the Epic secrets-check output nor lists leaked credentials as a `fails when`, so `pwsh tools/preflight.ps1 -SelfTest` could pass without checking secrets.
Fix: Add the secrets check and a registered planted-secret fixture to D22, naming the exact preflight command and failure output.

F6 [blocking] Probe 12.4 is not answered for every introduced check: the matrix omits D13’s source check, D21’s auth suite, D24’s release/tag/hash checks, D25’s repository rollback, and D26’s audit/session/path checks; several “empty fixture” cells also do not state the required non-pass output.
Fix: Extend the matrix with failing, silent-valid, and explicitly non-passing empty inputs for every check, plus one executable command per gating control and fixtures spelling the real inputs and generated state.

F7 [blocking] Probe 14.4 is unanswered: `-Paths` is said to run last, but the plan never defines how it derives the complete set or proves it sees new untracked files, ignored/generated outputs, server-side files, temporary worktrees, review artifacts, and files created by the current build.
Fix: Define the enumerator and its visibility sources explicitly, then add planted tracked, untracked, ignored/generated, external-session, and newly-created-path fixtures that make `pwsh tools/preflight.ps1 -Paths` fail.

F8 [blocking] D13 is unverifiable by its stated `file` evidence: a stranger finding `Has<VBloodConsumeSource>()` and no `Has<VBloodUnit>()` cannot establish that the check is on the dead entity, gates the emitted event, or fires exactly once.
Fix: Change D13 to an automated patch/helper test covering consume-source, VBloodUnit-only, unrelated death, and duplicate-delivery cases.

F9 [advisory] The late-arrival reference is internally broken: D5 and Business rules 9 cite S-8 for the 15-second rule, but this child’s S-8 concerns Bloodcraft familiar recognition.
Fix: Cite the Epic’s S-8 explicitly or renumber the child assumptions so the reference resolves unambiguously.

F10 [advisory] S-7 is not demonstrably cheap to reverse: switching from destruction to forced lifetime expiry changes removal semantics, and lowering the shared batch limit changes latency and the D16/D18 bounds.
Fix: Classify it as a pre-build validation risk or specify the already-approved fallback values and revised timing guarantees.

F11 [advisory] S-8’s fallback does not guarantee the inherited “player-owned units are excluded” rule: an unavailable or changed Bloodcraft tag during Session 3 can leave familiars empowered until a discovered amendment is designed.
Fix: Add an automated `UnitFacts` adapter test for every supported ownership signal and define fail-closed behavior when the optional tag cannot be resolved.

F12 [advisory] A concrete concurrency scenario remains weakly evidenced under probe 7.2: a stop or purge can race an unfinished apply sweep on the main thread, yet D5 does not explicitly assert that cancellation invalidates queued applies before removals are processed.
Fix: Add a ledger test that stops an event mid-sweep and proves no later tick applies another carrier.

F13 [advisory] D26’s audit command appears able to validate counts and marker strings without proving that each recorded “Codex verdict” corresponds to the reviewed step diff.
Fix: Record immutable step commit identifiers beside verdicts and have the audit check their association.

F14 [advisory] The child does satisfy the Raphael inheritance requirement: D11 adds the player-visible status-row values and bumps `Wire.Api` to 3, while D27 supplies the handoff; no additional decision is missing there.
Fix: None.

Repository-recon limitation: the active repository policy rejected even read-only `rg --files` and `dir`, and no connected GitHub repository was available, so claims about the current implementations of `Logic/`, `Services/`, `Patches/`, the two scripts, template, feature document, and profile could not be independently confirmed. The findings above therefore do not credit unverified code-recon claims.

9/15 layers · 40/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · ProjectM.BuffType is Parallel, Replace or Block (read from ProjectM.Shared.dll 1.1.12-r99041-b2); D4 becomes "Carrier recipe and stat map": CarrierRecipe gives BuffType Replace, MaxStacks 1, IncreaseStacks false, EndAction Destroy and the strip list, tested by CarrierRecipeTests; Business rules 1 states it
- F2 · accepted · D5: LifeTime is the fractional seconds left and no Apply is made with under 1 s left; the 0.99 s boundary is a fails-when
- F3 · accepted · the gating-control matrix names D26's data-inventory line as the 3.3 evidence; D26 now requires every Design › Data row to have a complete entry; carrier deletion is D17's boot line, plugin cleanup D18's hash, the zip D24's hash
- F4 · accepted · D20 states each dependency's behaviour: the hook-unavailable path for the patch, abort-and-rerun for git, gh and Codex (D24, D25), contract §1 for Raphael, D18 for Bloodcraft and KindredCommands
- F5 · accepted · D22 prints "secrets: none" and runs the existing Secrets bad fixtures; its fails-when names a token shape
- F6 · accepted · the selftest matrix gains rows for D21, D24, D25 and D26 (D13 is now a unit test), and a gating-control matrix gives one command per gating probe
- F7 · accepted · D26 names -Paths' walk (Epic D33's four sources, run after build, tcli build and deploy) and its existing bad fixtures; Bloodcraft and KindredCommands DLLs are outside the Nyarlathotep* server glob and are covered by D18's hash
- F8 · accepted · D13 is a test: DeathRule.IsVBloodKill with consume-source, VBloodUnit-only and neither cases, plus a duplicate delivery within 5 s
- F9 · accepted · the 15 s rule cites Epic S-8 everywhere
- F10 · accepted · S-7 names the sessions that settle it and the fallback's values and timing bound (EmpowerBatchPerTick 100; same ceil(n / budget) + 1 bound)
- F11 · accepted · Ownership.Decide fails closed: an unresolvable owner or followed entity counts as owned; D3 tests it
- F12 · accepted · D5: a stop, cancel or purge drops the event's queued sweep; "a stop mid-sweep is followed by an Apply" is a fails-when
- F13 · rejected · advisory — each post-audit is headed "### Step <n> · <date> · <commit>" (the raphael-api-core audit's form), which ties the verdict to the step's diff; -AuditOf counts them
- F14 · rejected · advisory — a confirmation, not a gap; no change
- Recon limitation · accepted · the next round's prompt carries the code excerpts the plan cites, since the reviewer's sandbox refused file reads
