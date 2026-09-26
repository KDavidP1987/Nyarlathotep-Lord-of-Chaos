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

## Review 4 · 2026-09-26 · codex · plan commit a37ab57

EARLIER: unresolved: F3

Earlier F3 remains unresolved: D4 claims Session 2 reads back “each field” from the live carrier, but D14 exposes only `BuffType`, stacks, end action, marker, lifetime and effective stats. It does not expose the raw modifier buffer or prove that all five gameplay-event components were stripped. A service implementation could omit those writes while CarrierRecipeTests, AdminLinesTests and the stated manual rows still pass.

F1 [blocking] D4 is not verifiable by its stated evidence, leaving probe 5.2 without evidence that the service writes the complete recipe into the live carrier.
Fix: Add an ECS integration seam or live diagnostic that reads every written Buff/LifeTime/modifier value and confirms each stripped component is absent after `EmpowerAction` applies the carrier.

F2 [blocking] Probes 12.4 and 14.3 require one evidence command per gating probe, but the rollback row needs three independent commands—D25, D23 and D28—so no single command fails when any repository, data-compatibility or live-install rollback control is absent.
Fix: Add one rollback-gate command that runs all three drills and emits success only when repository revert, N−1 compatibility and snapshot restoration all pass.

F3 [blocking] The probe 14.4 control cannot see every path it claims to police: `-Paths` walks the repository and named dev-server trees, but not `%TEMP%\nyar-snap-*`, temporary rollback worktrees, or remote tag/release writes; those paths can be added or changed without making the command fail.
Fix: Make the gating command consume an instrumented build/write manifest covering repository, server, temporary and remote artifacts, or add explicit checks for every external path/action and fail on undeclared entries.

F4 [advisory] The Bloodcraft/KindredCommands dependency row pins preferred versions but permits installing an unspecified replacement version if one is withdrawn. That makes the eventual compatibility claim dependent on whatever happens to be available.
Fix: Define an allowed fallback version rule—such as an exact approved replacement recorded by amendment before installation.

1. Purpose & typical use — Considered (3/3): “Purpose & typical use” identifies admin/player roles, desired outcomes, and coexistence with the foundation, SpawnWaves, Blood Moon, companion mods and Raphael.
2. Actors & permissions — Considered (3/3): “Design › Permissions,” the actor matrix and D21 cover reachable actors, unauthorized behavior and server/event ownership.
3. Inputs, outputs & data — Considered (4/4): D1, D10–D12, D16, D17, D23, D26 and “Design › Data” specify validation, outputs, persistence, deletion and migration.
4. Business rules & invariants — Considered (5/5): “Business rules” and D3–D8/D26 state calculations, exclusivity, duration, precedence and every-X sets. The Epic’s carrier-only, precedence, one-per-faction, hard-duration, eligibility and S-8 late-arrival decisions are present.
5. Internal interfaces — Considered (3/3): “Interfaces › Internal” enumerates reads, writes and shared contracts. D4’s evidence defect fails the acceptance gate but the interface decision itself is stated.
6. External dependencies & contracts — Considered (3/3): the dependency table supplies versions, costs/quotas, sampled inputs and failure behavior; D20 and 6.3 cover failures and test isolation.
7. States & lifecycle — Considered (3/3): “Design › States” covers empty/loading/partial/error states, main-thread concurrency, cancellation, restart and reload behavior.
8. Minimal stretch — Considered (2/2): “Use cases › Minimal stretch” covers disabled/default, one-faction/one-stat, empty results and one-time cleanup.
9. Maximal stretch — Considered (3/3): “Use cases › Maximal stretch” covers thousands of NPCs, bounded misuse, deduplication and repeated starts/sweeps.
10. Security & privacy — Considered (4/4): “Security” plus D1, D10, D11, D21 and D22 cover authorization, injection boundaries, credentials and data minimization.
11. Design & UX — Considered (4/4): “Design › UX” covers discovery, feedback, accessibility and activation/should-not-activate cases.
12. Failure handling & observability — Gap (3/4): 12.1–12.3 are answered by “Failure & observability” and D1/D6/D7/D11/D14/D16/D20/D26; 12.4 fails because the rollback gating probe lacks one comprehensive evidence command.
13. Performance & scale — Considered (2/2): “Performance” and D1/D5/D19 define the latency budget, hot path, bounds, behavior at bounds and excluded valid cases.
14. Rollout & compatibility — Gap (2/4): Shipping and Compatibility answer 14.1–14.2; 14.3 lacks a single complete rollback gate, and 14.4’s command cannot observe all temporary and remote paths.
15. Out of scope — Considered (2/2): exclusions and named future-plan slugs are explicit.

13/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · also closes the unresolved round-3 F3: D14's native row reads back IncreaseStacks, the whole ModifyUnitStatBuff_DOTS buffer ("mods <type>:<mod>:<value>,…") and the presence of each of the five stripped gameplay-event types ("strip ok|missing:<names>"); D4's Session 2 evidence requires "type Replace stacks 1 incr False end Destroy mark ok strip ok" and a mods list equal to EmpowerStats.Modifiers; AdminLinesTests covers the renderings
- F2 · accepted · +D29: tools/rollback-gate.ps1 runs the repository drill (D25, saved as tools/repo-rollback-drill.ps1), the N-1 drill (D23) and the snapshot selftest (D28), printing "rollback gate: 3/3" only when all pass; its -SelfTest has 5 stub cases; it is the single 14.3 evidence command and step 7 runs it before and after the push
- F3 · accepted · D26's -Paths also fails on a leftover %TEMP%\nyar-snap-* or nyar-rollback-* folder, a leftover git worktree, and a remote tag or GitHub release not declared by a remote-tag:/remote-release: line of tools/paths-manifest.txt; fixtures Paths/bad-temp, bad-worktree, bad-remote
- F4 · accepted · a withdrawn Bloodcraft or KindredCommands version is replaced only by an external amendment naming the exact version before installation, and D18 runs against it

## Review 5 · 2026-09-26 · codex · plan commit c5423e7

EARLIER: all resolved

F1 [blocking] Probe 3.3 remains unanswered for temporary evidence artifacts: the Design › Data inventory omits D24’s `%TEMP%\nyar-rel-*` download, D25’s `%TEMP%\nyar-rollback-*` worktree, and the rollback drill’s saved configuration, so their owner, retention, deletion, and copy count are unspecified and D26 cannot detect the omission because it checks only rows already present.
Fix: Add each temporary/worktree/saved-config artifact to Design › Data and `tools/data-inventory.json`, including location, owner, retention/deletion, and copies; make the inventory check derive the expected artifacts from the Build plan or paths manifest.

F2 [blocking] Probe 14.4 remains unanswered for D24’s `%TEMP%\nyar-rel-*` directory: Paths walked does not name it and D26’s `-Paths` detects only `nyar-snap-*` and `nyar-rollback-*`, so an interrupted release download can leave an unmanifested directory without failing the gating command.
Fix: Add `%TEMP%\nyar-rel-*` to Paths walked and the manifest policy, make `preflight.ps1 -Paths` fail on a leftover instance, and add a `Paths/bad-rel` fixture spelling the real directory name.

F3 [blocking] Probe 12.4 remains unanswered by its single gating command: `pwsh tools/preflight.ps1 -SelfTest` exercises preflight fixtures, but it does not run or verify the failing/silent/empty behavior of the repository drill, rollback gate, snapshot selftest, filtered unit-test controls, release download/hash check, or manual/session checks; a stranger could remove those negative cases while D22 still passes.
Fix: Provide one umbrella evidence command for 12.4 that invokes every introduced check and verifies its bad, good/silent, and empty case—or narrow its asserted scope and add a meta-selftest that fails whenever a declared check lacks registered negative, silent, and empty fixtures.

1. Purpose & typical use — Considered. “Purpose & typical use” answers 1.1–1.3: admin/player roles and frequency, desired outcome, and coexistence with the foundation, SpawnWaves, Blood Moon, companion mods, and Raphael.

2. Actors & permissions — Considered. “Design › Permissions,” D21, D5, and D6 answer 2.1–2.3, including player, admin, System, Operator, Raphael, other mods and unauthenticated clients; denial behavior; and server/event ownership and re-entry.

3. Inputs, outputs & data — Gap. D1/D12 answer 3.1, D10/D11/D16 answer 3.2, and D2/D23 plus Design › Data answer 3.4. Probe 3.3 is incomplete because several temporary and rollback artifacts acknowledged by the Build plan lack the required persistence record.

4. Business rules & invariants — Considered. “Business rules” and D3–D8 answer 4.1–4.5: exact modifier calculations, carrier and uniqueness invariants, hard duration rules, inherited precedence, and enumerated query/apply/removal/marker/session/path sets.

5. Internal interfaces — Considered. “Interfaces › Internal” answers 5.1–5.3 with concrete symbols and paths, affected behavior, failure consequences, and enumerated JSON, wire, and marker contracts consistent with the pasted current-code recon targets.

6. External dependencies & contracts — Considered. The dependency table and D15/D18/D20 answer 6.1–6.3. Versions, sampled game records, failure behavior and test-mode isolation are decided. The withdrawn-mod case now requires an exact replacement version by amendment before installation.

7. States & lifecycle — Considered. “Design › States,” D5–D7, D11 and D17 answer 7.1–7.3, including empty/loading/partial/error states, main-thread concurrency, contested starts, stale entities, stop, restart and reload behavior.

8. Minimal stretch — Considered. “Use cases › Minimal stretch,” D1, D16 and D17 answer 8.1–8.2 for the disabled default, smallest valid definition, empty faction, and one-time cleanup.

9. Maximal stretch — Considered. “Use cases › Maximal stretch,” D1, D5, D6, D12, D19 and D21 answer 9.1–9.3 for thousands of NPCs, bounded hostile configuration, unauthorized players, duplicate triggers and repeated sweeps.

10. Security & privacy — Considered. “Security,” D1, D10, D11, D21 and D22 answer 10.1–10.4. Authorization covers direct and indirect paths; catalog/range validation bounds inputs; credential storage and logging are stated; and entity-only telemetry avoids personal data.

11. Design & UX — Considered. “Design › UX,” D6, D10, D13, D14, D16 and D24 answer 11.1–11.4: discovery, real-chat feedback and empty results, text accessibility constraints, activation evidence, and unrelated should-not-activate cases.

12. Failure handling & observability — Gap. “Failure & observability,” D1, D6, D7, D11, D14, D16, D20 and D26 answer 12.1–12.3. Probe 12.4 is not enforceable by its named single command across all checks the plan introduces.

13. Performance & scale — Considered. “Performance,” D1, D5 and D19 answer 13.1–13.2 with the under-5-ms budget, identified hot path, bounded batches and queries, source cases, behavior at each bound and excluded valid cases.

14. Rollout & compatibility — Gap. “Rollout,” D2, D11, D22–D25 and D27–D29 answer 14.1–14.3, including the now-unified repository/N-1/snapshot rollback command and committed range. Probe 14.4 misses the release-download temporary path.

15. Out of scope — Considered. “Out of scope” answers 15.1–15.2 with explicit exclusions and named future plans or owning children.

12/15 layers · 46/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · Design › Data gains rows for %TEMP%\nyar-rel-* (release download), %TEMP%\nyar-rollback-* (rollback worktree) and %TEMP%\nyar-drill-* (drill saved config) with owner, retention, deletion and copies; the inventory check derives its expected set from the `temp:` globs of tools/paths-manifest.txt too, with fixture DataInventory/bad-temp
- F2 · accepted · -Paths fails on any leftover %TEMP%\nyar-* folder, the manifest declares nyar-snap-*, nyar-rel-*, nyar-rollback-*, nyar-drill-* as `temp:` lines, fixture Paths/bad-rel spells the real name; D24's download moves into tools/release-verify.ps1 with its own -SelfTest
- F3 · accepted · D22's -SelfTest also runs an `externalSelfTests` registry in tools/preflight-checks.json (rollback drill, snapshot, rollback gate, release verify, repository drill with its new -SelfTest, unit tests), fails when an entry lacks a bad, good or empty case or when a tools script declaring -SelfTest is unregistered (fixtures SelfTestRegistry/bad, bad-2); the 12.4 matrix row names it as the single command

## Review 6 · 2026-09-26 · codex · plan commit 1c05157

EARLIER: all resolved

F1 [blocking] Probe 4.4 is unanswered because D5/D16 promise stop removes every carrier within the batch bound, while D20 drops a removal after three failures and leaves it until LifeTime expiry; the plan never establishes which rule wins.
Fix: State an explicit precedence decision: either failed removals are a documented exception to the stop bound, or continue/fallback removal until the bound is met, and make the selected behavior fail under one Engine/CarrierLedger evidence command.

F2 [blocking] Probe 6.2 is unanswered for an Apply that throws after the carrier entity is instantiated but before the recipe is completely written: D20 says only “skips it,” leaving a possible partially configured, untracked potion buff with native gameplay behavior.
Fix: Define failed-Apply atomicity—destroy or queue removal of every partially created carrier—and test throws after instantiation and after each recipe-writing stage through `DependencyFailureTests`.

F3 [blocking] Probe 10.3 lacks the required single evidence command: the gating matrix names `preflight -SelfTest` followed by ordinary `preflight`, so neither named command alone is specified to prove both the negative secret fixtures and the real repository scan.
Fix: Make one command, such as `pwsh tools/preflight.ps1 -SelfTest`, run the real secrets scan plus its bad/good/empty fixtures and fail if either portion fails.

F4 [advisory] Probe 4.1’s D15 oracle says each live reading equals the plain reading multiplied by the configured multiplier, but `MultiplyBaseAdd` need not produce that result when another native or mod buff is already contributing to the same stat.
Fix: Compare the carrier delta with the unmodified base contribution, or explicitly constrain Session 2 subjects to have no other modifiers and verify that precondition in the debug row.

F5 [advisory] Probe 4.2’s `Faction_Players*` invariant is broader than its named negative example: D3 expressly tests `Faction_Players`, but does not name a derived faction such as `Faction_Players_Servants`.
Fix: Add a wildcard-derived faction case proving the prefix rule, not merely the exact catalog name.

1. Purpose & typical use — Considered (3/3): `Purpose & typical use` identifies the admin and players, the desired timed-world-pressure outcome, and the foundation, SpawnWaves, Blood Moon, companion mods, and Raphael coexistence.

2. Actors & permissions — Considered (3/3): `Design › Permissions`, D21, and the actor matrix cover all reachable actors, unauthorized refusal, server-owned definitions, carrier ownership, contention, stopping, and restarting.

3. Inputs, outputs & data — Considered (4/4): D1/D12 enumerate and validate inputs; D10/D11/D16 enumerate outputs and side effects; `Design › Data` plus D26 covers persistent, in-memory, remote, ignored, temporary, and drill artifacts; D2/D23 and Compatibility cover migration.

4. Business rules & invariants — Gap (4/5): `Business rules`, D1–D7, and the Every-X inventory answer 4.1, 4.2, 4.3, and 4.5. Probe 4.4 remains unanswered because the hard stop-removal bound conflicts with D20’s abandon-after-three-failures rule.

5. Internal interfaces — Considered (3/3): `Interfaces › Internal` names reads, writes, affected symbols, failure consequences, and the JSON, wire, and marker contracts field by field.

6. External dependencies & contracts — Gap (2/3): the dependency table and 6.3 answer 6.1 and 6.3. Probe 6.2 does not decide cleanup of a carrier created before a later Apply operation throws.

7. States & lifecycle — Considered (3/3): `Design › States` covers empty/loading/partial/error states, main-thread concurrency and start races, cancellation, stale entities, restart re-entry, reload, and correction invalidation.

8. Minimal stretch — Considered (2/2): `Use cases › Minimal stretch` covers pillar-off, one faction/one stat, an empty faction, and one-time cleanup or boot recovery.

9. Maximal stretch — Considered (3/3): `Use cases › Maximal stretch`, D5, D6, D12, and D19 cover multi-thousand-NPC volume, bounded misuse, duplicate starts, repeated sweeps, and trigger deduplication.

10. Security & privacy — Gap (3/4): `Security`, D1, D10, D11, and D21 answer authorization design, injection, and personal-data handling. Probe 10.3 lacks one evidence command that validates both the real tree and negative secret fixtures.

11. Design & UX — Considered (4/4): `Design › UX`, D6, D10, D13, D14, D16, and D24 cover discovery, feedback, empty output, chat accessibility constraints, activation evidence, and unrelated should-not-activate cases.

12. Failure handling & observability — Considered (4/4): `Failure & observability`, its selftest matrix, D20, D22, and D26 cover user-visible failures, diagnostic logs, production detection, and bad/good/empty cases under the registered aggregate selftest command.

13. Performance & scale — Considered (2/2): `Performance`, D5, and D19 state the five-millisecond budget, hot path, operational bounds, their source cases, bound behavior, and excluded valid cases.

14. Rollout & compatibility — Considered (4/4): `Rollout`, D23–D29, and `Paths walked` cover disabled-by-default shipping, API/config/data compatibility, repository/server/published rollback, and all repository, server, temporary, remote, review, and plan paths.

15. Out of scope — Considered (2/2): `Out of scope` identifies exclusions and names the future children or workspaces receiving deferred functionality.

12/15 layers · 46/49 probes

VERDICT: REVISE
### Dispositions
- F1 · accepted · D20 states the precedence: the stop bound of D5 governs every removal that does not throw; a removal failing 3 retries falls back to expiring the carrier (LifeTime.Duration = age, S-7), and only when that also throws is it the documented exception, counted in "empower <id> stopped: <n> removed, <f> left to expire" (D16); CarrierLedgerTests and DependencyFailureTests fail on each break
- F2 · accepted · D20 defines Apply atomicity: staged create → mark → lifetime → strip → modifiers, the mark written first so D7's boot sweep finds any existing carrier; a throw after create queues the carrier for removal and never records it as applied; DependencyFailureTests throws after each stage
- F3 · accepted · D22's -SelfTest runs the Secrets check against its fixtures and then on the real tracked tree, failing on any finding; the 10.3 matrix row names that one command
- F4 · accepted · D14 adds "other stat buffs <k>", and D15 uses only subjects showing 0, so the × multiplier oracle holds
- F5 · accepted · D3's tests add the derived factions Faction_Players_Castle_Prisoners, Faction_Players_Mutant and Faction_Players_Shapeshift_Human (from unit_index.tsv) as denied units and a rejected `factions` entry
