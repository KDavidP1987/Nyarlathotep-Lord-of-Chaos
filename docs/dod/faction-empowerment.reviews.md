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

## Review 2 · 2026-09-26 · codex · plan commit 723cd63
EARLIER: unresolved F4

F1 — `blocking` — Probe `4.5` is unanswered: “every NPC” is computed by a query requiring `PrefabGUID + FactionReference + Health + UnitStats`, but the plan identifies only late-created and destroyed units as misses, not otherwise-valid faction NPCs lacking one of those components.
Fix: Define eligible NPCs as requiring all four components and justify excluded entities, or query faction members broadly and specify/test the handling of missing `Health` or `UnitStats`.

F2 — `blocking` — Probe `6.2` remains unanswered for Bloodcraft/KindredCommands: D18 tests their expected current records, but no policy says what happens when the familiar tag is absent, renamed, malformed, or returned without a resolvable owner; that can empower a player-owned familiar contrary to the invariant.
Fix: Specify fail-closed behavior for unrecognised dependency ownership data and add that garbage/absent-tag case to `DependencyFailureTests` or another named evidence item.

F3 — `blocking` — Probe `14.3` omits the project-profile rollback requirement for tools that change the live dev installation: steps 4–6 deploy DLL/config changes and install/remove mods without an atomic snapshot manifest, leftover-snapshot refusal, hash-verified restore, or crash test.
Fix: Add a D-item and command that snapshots every changed live path atomically, refuses an existing snapshot, simulates interruption, restores it, and verifies hashes.

F4 — `blocking` — D14 is unverifiable for probe `11.2`: recording one empowered and one plain bandit cannot establish the claimed ten-row cap, nearest-first ordering, or exact output alternatives, so a stranger cannot verify the item by its stated manual evidence.
Fix: Make Session 2 record a controlled set of more than ten natives with known distances, the emitted rows, their order/count, and both carrier renderings.

F5 — `blocking` — D24 is unverifiable for probe `14.1`: `gh release download … -D <scratch dir>` is not an executable evidence command, and the scratch directory’s creation and resulting asset path are unspecified.
Fix: Replace the placeholder with a complete PowerShell command that creates a unique scratch directory, downloads the asset there, hashes its exact path, and cleans it up.

F6 — `blocking` — D27 is unverifiable for probe `5.3`: its file evidence checks the heading, `kind=empower`, `wave=-`, and `api>=2`, but not its claimed `faction=<comma-joined names>` or admin `units` contract.
Fix: Extend the file evidence to check the exact faction and admin-units clauses, preferably through `ContractDocTests`.

F7 — `advisory` — Build step 4 says it satisfies D17 but schedules only one event and stops it mid-window; D17 also requires a separate 60-second event to expire naturally and produce the post-expiry sample.
Fix: Add the natural-expiry event and its required sample lines to step 4.

Layer grading:

1. Considered — `Purpose & typical use` answers 1.1–1.3.
2. Considered — `Design › Permissions` and D21 answer 2.1–2.3.
3. Considered — D1, D10–D12, D16–D17, D23, D26 and `Design › Data` answer 3.1–3.4.
4. Gap — `Business rules` answers 4.1–4.4, but the component-filter omission leaves 4.5 unanswered.
5. Considered — `Interfaces › Internal` with D1, D7, D9, D11 and D20 answers 5.1–5.3; D27 nevertheless fails its own evidence claim.
6. Gap — the dependency table answers 6.1 and 6.3, but Bloodcraft/KindredCommands garbage behavior leaves 6.2 unanswered.
7. Considered — `Design › States` and D5–D6/D17 answer 7.1–7.3.
8. Considered — `Use cases › Minimal stretch` and D1/D16/D17 answer 8.1–8.2.
9. Considered — `Use cases › Maximal stretch` and D1/D5/D6/D12/D19/D21 answer 9.1–9.3.
10. Considered — `Security` and D1/D10/D11/D21/D22 answer 10.1–10.4.
11. Considered — `Design › UX` and D6/D10/D13/D14/D16/D24 answer 11.1–11.4; D14’s evidence remains defective.
12. Considered — `Failure & observability`, its matrices, and D1/D6/D7/D11/D14/D20/D22/D23/D26 answer 12.1–12.4.
13. Considered — `Performance` and D1/D5/D19 answer 13.1–13.2.
14. Gap — `Rollout` answers 14.1, 14.2 and the repository/server version rollback, while the required live-install transactional rollback leaves 14.3 unanswered; D26 answers 14.4.
15. Considered — `Out of scope` answers 15.1–15.2.

The inherited Epic decisions are otherwise present: carrier-only mutation, `BuffType.Replace`, remaining-seconds `LifeTime`, exclusions, S-8 late arrivals, precedence, one empowerment per faction, hard durations, every-X enumeration, and the API-3 rows/integer bump.

12/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · Business rules 9 justifies the four-component query (no UnitStats → nothing to raise; no Health → not a combatant) and makes the gap visible: the first sweep logs "query <n> of <m> faction entities" and, verbose, the gap's prefabs (D16)
- F2 · accepted · Ownership.Decide reads only game components (Follower, EntityOwner, Team) and fails closed on any unreadable one; D20 adds the unreadable-owner case; the dependency table and S-8 say so (also closes round 1's F4)
- F3 · accepted · +D28: tools/dev-snapshot.ps1 with the drill's snapshot code moved into tools/snapshot-lib.ps1 (atomic manifest, leftover refusal, hash-verified restore, crash cases in -SelfTest); steps 4-6 save before and restore after each session
- F4 · accepted · D14 gains a distance column and a Logic seam, AdminLines.Natives, tested for cap, order, ties and both carrier renderings; Session 2 records a camp with more than 10 natives
- F5 · accepted · D24's download is a complete command: a unique scratch folder, the exact asset path hashed, the folder removed in finally
- F6 · accepted · D27's file evidence adds "faction=<names joined by ','>", "faction=Legion,Bandits" and "units=<NPCs holding the event's empowerment>"; the contract's §3 text stays D11's ContractDocTests
- F7 · accepted · step 4 now schedules fe-short (60 s, natural expiry, reverted sample line) and fe-long (1200 s, stopped mid-window)

## Review 3 · 2026-09-26 · codex · plan commit 43a9296

EARLIER: all resolved

F1 [blocking] Probe 6.1 is unanswered: Bloodcraft and KindredCommands are identified only as their “current Thunderstore releases,” while git, gh, GitHub, and Codex also lack concrete versions and quota/cost decisions, so the external contract cannot be reproduced later.
Fix: pin or record the tested versions for every dependency and state quota/cost as a value or explicitly “none/not applicable.”

F2 [blocking] Probe 4.2 has contradictory ownership invariants: D3 promises ownership uses only game components and “never another mod’s data,” but S-8’s fallback permits adding a Bloodcraft-specific component and temporarily avoiding the familiar’s faction.
Fix: choose one invariant-preserving fallback—either fail closed using game-owned evidence only, or explicitly approve and specify the Bloodcraft contract as a dependency.

F3 [blocking] D4 is unverifiable for probe 5.2: `CarrierRecipeTests` can prove the pure recipe, but the stated evidence cannot prove that `Services/EmpowerAction.cs` writes exactly that recipe into the actual carrier.
Fix: add an injectable carrier-write seam or a static/integration check whose evidence command fails when the service omits or changes any recipe field.

F4 [blocking] D8 is unverifiable for probe 4.5: the shown structural-edits command permits `DestroyUtility.Destroy*` anywhere inside `EntityExtensions.cs`, so it does not prove the claimed control that carrier removal occurs only through `RemoveBuffSafe`.
Fix: make the check identify the enclosing method and fail unless carrier destruction is inside `RemoveBuffSafe`, with bad fixtures for another method in `EntityExtensions.cs`.

F5 [blocking] Probe 14.3 remains incomplete for Session 3: D28 snapshots the plugin directory and only Nyarlathotep’s config paths, yet D18 claims restoration removes Bloodcraft and KindredCommands “and their configs”; third-party configuration files elsewhere under `BepInEx/config` are neither captured nor removed by the specified snapshot.
Fix: snapshot and restore the complete relevant config tree, or enumerate both mods’ exact config paths and add round-trip fixtures proving newly installed DLLs and configs are removed.

F6 [blocking] Probe 14.3 does not give exact rollback steps for the pushed `v0.4.0` tag and GitHub prerelease created by D24; D25 reverts the repository tree, but the published tag, release, and asset remain live.
Fix: state and drill the release rollback policy and commands—such as deleting/yanking the prerelease and tag or publishing a designated rollback release—including what happens after users have downloaded 0.4.0.

Layer rescore:

1. Considered — Purpose & typical use  
2. Considered — Design › Permissions and D21  
3. Considered — Design › Data and D1/D11/D17/D23/D26  
4. Gap — Business rules and S-8 conflict on probe 4.2  
5. Considered — Interfaces › Internal  
6. Gap — Interfaces › External lacks reproducible dependency versions for 6.1  
7. Considered — Design › States  
8. Considered — Use cases › Minimal stretch  
9. Considered — Use cases › Maximal stretch  
10. Considered — Security  
11. Considered — Design › UX  
12. Considered — Failure & observability and its selftest/control matrices  
13. Considered — Performance  
14. Gap — Rollout › Rollback omits parts of the live-install and published-release rollback  
15. Considered — Out of scope  

12/15 layers · 46/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · the dependency table pins Bloodcraft 1.13.24, KindredCommands 2.5.8, git 2.53.0, gh 2.92.0, codex-cli 0.151.0, BepInEx 6.0.0-be.733, VampireReferenceAssemblies 1.1.12-r99041-b2, each with its quota and cost ("free", "none")
- F2 · accepted · one invariant: Ownership.Decide reads game components plus any mod contract declared in the dependency table (none today); S-8's fallback is a discovered amendment declaring Bloodcraft 1.13.24's familiar component as a versioned contract, read fail-closed, with D3 and D20 cases, and the release waits for it
- F3 · accepted · D4's recipe is read back from the live carrier by D14's debug row ("type Replace stacks 1 end Destroy mark ok"), recorded in Session 2; the service's single writer takes only the CarrierRecipe object
- F4 · accepted · D8's structural check allows DestroyUtility.Destroy only inside RemoveBuffSafe's body (enclosing method found by signature and braces); fixture StructuralEdits/bad-3 moves the call into EntityExtensions.DestroySafe
- F5 · accepted · D28 snapshots the whole BepInEx/plugins and BepInEx/config trees; restore removes files added since and verifies no unlisted file remains; -SelfTest adds a DLL and a cfg after the save and expects both removed
- F6 · accepted · Rollout › Rollback gains a published-release policy: tags and releases are never deleted (Epic S-19), a bad 0.4.0 is withdrawn with the exact `gh release edit` command and fixed forward by 0.4.1, the owner withdraws the Thunderstore version, and servers that downloaded 0.4.0 follow the server rollback
