# Audit — faction-empowerment

Build plan steps 1–7 of docs/dod/faction-empowerment.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.3.0 = ee36a7d.

## Pre-audit
### Step 1 · 2026-09-26 · b996191
- pre-child: b996191, the commit recording owner decisions 1A–4A (A1 reversed S-4, so the build starts without the Epic A20 both-mods check); rollback base is the tag v0.3.0 (ee36a7d)
- git status: one pending edit, docs/dod/faction-empowerment.md (the Build plan intro sentence recording A1); it is committed with this step
- compile: 0 errors, 0 warnings
- tests: 703 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: faction-empowerment 0/29 verified (just started); review codex (Review 8 READY), not pending
- feature doc read: docs/features/FACTION_EMPOWERMENT.md (Status: design only; the S3 carrier spike under Test results; Open questions answered by the plan's S-1–S-3); plan D1–D13, D20, Business rules 1–9, Interfaces, Design › Data and States; Logic/Model.cs, Validation.cs, Engine.cs, Markers.cs, Limits.cs, Messages.cs, CommandArgs.cs, EventAdmin.cs, DefinitionEditor.cs, Precedence.cs; Services/SpawnTracker.cs TryMark and MarkedUnits
- server: not running; step 1 is pure logic with no in-game test
- found on the way: Markers.IsOurs is the boot sweep's despawn filter (Services/SpawnTracker.cs MarkedUnits), so adding Carrier to Markers.All must not make IsOurs match a carrier, or step 1 alone would despawn empowered native NPCs at the next boot; IsOurs becomes `KindOf(level) == MarkerKind.Unit` in this step, before step 2 routes the boot sweep through SweepPlan (D7)
- step order: the shipped template (Resources/events.default.json, listed under step 2) moves into step 1, because the pairing rule of D1/D2 disables the old SpawnWaves example-empowerment and the step's TemplateTests read the shipped file; no design change

### Step 2 · 2026-09-26 · 9584149
- git status: clean after 9584149 except the plan edit recording A3 (below), committed with this entry
- compile: 0 errors, 0 warnings
- tests: 848 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: faction-empowerment 7/29 verified; review codex (Review 8 READY), not pending (A3 is layer 5.2, not gating)
- feature doc read: docs/features/FACTION_EMPOWERMENT.md (Status: step 1 of 7 done); plan step 2, D2, D7, D8, D9, D13, D14, D20, D21, D22, D26, Business rules 1, 5, 6, 9; Services/EventRuntime.cs, SpawnTracker.cs (BootSweep, TryMark, MarkedUnits, DebugHere), WaveAction.cs, EventStore.cs (PrefabUnitCatalog), EntityExtensions.cs, Patches/DeathEventPatch.cs, Services/UnitSetup.cs; tools/preflight.ps1 Test-CheckStructuralEdits and tools/preflight-checks.json StructuralEdits
- server: not running; step 2 has no in-game test (Session 1 is step 4)
- found on the way: EntityExtensions.DestroySafe already calls DestroyUtility.Destroy (unit despawn), and StructuralEdits/bad-2 to bad-10 already exist, so D8's fence and fixture names as written would fail the real tree and collide; recorded as A3 (discovered, ~D8 ~D22, layer 5.2) before any step 2 code

### Step 3 · 2026-09-26 · 31f11c9
- git status: clean at 31f11c9 (step 2 post-audit)
- compile: 0 errors, 0 warnings
- tests: 861 passed
- preflight: exit 0 ("PREFLIGHT OK"; "wire contract: 7 tags, 4 api commands, all documented (api 2)")
- dod status: faction-empowerment 13/29 verified; review not pending
- feature doc read: docs/features/FACTION_EMPOWERMENT.md (Status: steps 1–2 done); plan step 3, D11, D22 (wire line), D27; Logic/ApiLines.cs, Logic/Wire.cs, Commands/ApiCommands.cs; docs/RAPHAEL_INTEGRATION_CONTRACT.md §3 and §7 (it has no change log section yet, which D11 names: step 3 adds "## 9. Change log"); docs/RAPHAEL_HANDOFF.md (its "## api 3 (faction empowerment)" section was written at planning, marked PLANNED); ContractDocTests, WireFormatTests, ApiLinesTests; tools/preflight-fixtures/WireContract
- server: not running; step 3 has no in-game test

### Step 4 · 2026-09-26 · 998d65f
- git status: clean at 998d65f (step 3 post-audit); the step's tooling (tools/snapshot-lib.ps1, tools/dev-snapshot.ps1, the drill's move to the library, session-events.py modes fe1 and show) and amendment A5 are committed with this entry, before the session
- compile: 0 errors, 0 warnings
- tests: 868 passed
- preflight: exit 0 ("PREFLIGHT OK"); -SelfTest 27/27; -Paths 777 walked, all in manifest; `pwsh tools/dev-snapshot.ps1 -SelfTest` → "snapshot selftest: 6/6"; `pwsh tools/rollback-drill.ps1 -SelfTest` → "drill selftest: 6/6" before and after the move (8/8 is D23's later extension)
- dod status: faction-empowerment 15/29 verified; review not pending (A5 is layer 12.1, not gating)
- feature doc read: docs/features/FACTION_EMPOWERMENT.md (Status: steps 1–2 done, Session 1 is step 4); plan step 4, D17, D26, D28, S-7
- baseline boot: no server running; the last logs (09:59) give "log check: 0 unhandled, 5 nyar lines, 0 orphan errors, 0 unity errors"; their 3 warning lines are Beelzebub's TUNE notices and Il2CppInterop's "Class::Init signatures have been exhausted", none from Nyarlathotep
- found on the way: the tooling agent added -Restore refusals and a copy hash check beyond D28 → amendment A5 before the tooling is committed or run

### Step 5 · 2026-09-26 · 80df420
- git status: clean after the step 4 post-audit commit (80df420)
- compile: 0 errors, 0 warnings; tests: 869 passed
- preflight: PREFLIGHT OK; -SelfTest 27/27; dev-snapshot and drill selftests 6/6
- dod status: faction-empowerment 16/29 verified; amendment A6 recorded before its build (7400fc6)
- feature doc read: docs/features/FACTION_EMPOWERMENT.md › Test results › Session 2 steps (Codex round 6 READY); plan step 5, D4, D14, D15, D16, D18, D19, S-7
- baseline boot: the Session 2 boot of the 7400fc6 build, after `pwsh tools/dev-snapshot.ps1 -Save s2` and `session-events.py fe2`; its log check is the Session 2 record's first line

### Step 6 · 2026-09-26 · f706fe6
- git status: clean after the step 5 records (f706fe6)
- compile: 0 errors, 0 warnings; tests: 869 passed
- preflight: PREFLIGHT OK
- dod status: faction-empowerment 19/30 verified; A7 (+D30) recorded before its build
- feature doc read: docs/features/FACTION_EMPOWERMENT.md › Test results › Session 2 (the carried-over checks) and Build plan step 6; D15, D16, D18, D26, D28, D30
- baseline boot: the Session 2 boot of 7400fc6 (log check "0 unhandled, 575 nyar lines, 0 orphan errors, 0 unity errors"); the server is stopped and the s2 snapshot restored
- session setup (ee13316 → 980b1ae): `pwsh tools/dev-snapshot.ps1 -Save s3` → "snapshot saved: s3 (28 files)"; Release build deployed (0 warnings); fe3 written; Bloodcraft 1.13.22 and KindredCommands 2.5.8 (Thunderstore zips, SHA-256 7283dbea… and 80b9cf90…) in plugins with only Bloodcraft's FamiliarSystem switched on; setup boots' log checks "0 unhandled, 6 nyar lines" and "0 unhandled, 13 nyar lines", 0 orphan and 0 unity errors; new warnings are Bloodcraft's own (startup check before bootstrap, a NullReferenceException in its VSystemManager.AddSystem; it then logs "Initialized [1.13.22]") and KindredCommands' player-cache lines
- pre-session Codex cross-inspection of the D30 and fe3 diff and the Session 3 steps: READY (round 7); rounds 1-6 REVISE, all on the owner steps: the cooldown wait, the native Thug baseline, .despawnnpc near the camp, the speed baseline, the uninstall events outliving the autosave wait (300 s → 900 s), the respawn fallback (KindredCommands units are admin-owned and skipped, so no stand-in), the tracked-units line, Thug rows read before each damage sample; one rejected with evidence (debug rows are logged, SpawnCommands.cs Debug); every round's log: 0 "blocked by policy" except round 1's hit, which is the text of this audit file

### Step 7 · 2026-09-26 · 3a535c1
- git status: clean, level with origin/main after the step 6 post-audit (3a535c1)
- compile: 0 errors, 0 warnings; tests: 871 passed
- preflight: PREFLIGHT OK; -Paths "778 walked, all in manifest"
- dod status: faction-empowerment 24/30 verified; open D22, D23, D24, D25, D26, D29, all Build plan step 7
- feature doc read: docs/features/FACTION_EMPOWERMENT.md Status and Open questions; Build plan step 7 and Rollout › Compatibility and Rollback of docs/dod/faction-empowerment.md
- baseline boot: Session 3's boots of ee13316 (log checks "0 unhandled, 367 nyar lines" and "0 unhandled, 88 nyar lines"); the server is stopped and the s3 snapshot restored
- releases: tags v0.2.0, v0.2.1, v0.3.0, each a GitHub pre-release; v0.3.0 = ee36a7d is the rollback base
- privacy grep (7656119, kdpenland): only the plans' own "Grep for" lines, their fixtures and the raphael-api-core audit sentence

## Post-audit
### Step 1 · 2026-09-26 · 0559d4c → 23f2ba4 → 5ca6812
- compile / preflight: 0 errors, 0 warnings; 848 tests passed; PREFLIGHT OK; dod --check 0 problems, 0 warnings
- mutation checks: each of the 7 original controls and each post-audit fix, reverted by sed, fails at least one named test
- /code-review (0559d4c): no defects; observations dispositioned: a unit whose stopped carrier is still queued for removal could get a second carrier → fixed (CarrierOf "removing", test A_unit_whose_stopped_carrier_is_still_queued_is_not_given_a_second_one); SweepPlan.From quadratic → HashSets; a vanished-unit comment corrected; PrefabUnitCatalog has no IFactionCatalog yet → step 2 (Empower definitions load disabled in game until then); prune order and float rounding → already fixed from Codex round 1
- Codex verdict: READY (round 3) — round 1 REVISE: LifeTime float rounding up, create-then-mark crash window, stale removals free of budget, prune before a throwing query (the second became amendment A2, ~D20; the other three code defects, fixed in 23f2ba4); round 2 REVISE: a failed expiry fallback cleared the unit's removing state while its carrier still existed (fixed in 5ca6812 with a tombstone held while the buff exists); round 3 READY, no findings
- in-game: none (step 1 is pure logic)
- dod status: 7/29 verified (D1, D2, D3, D5, D6, D10, D12); D4, D7, D9, D13 and D20 pass their logic tests and wait for their game half (step 2, Session 2)

### Step 2 · 2026-09-26 · 0bce3db → e477d29 → d5889b0
- compile / preflight: 0 errors, 0 warnings; 861 tests passed; PREFLIGHT OK; -SelfTest pass (StructuralEdits bad-14, bad-15 new); -AuthSuite pass; dod --check 0 problems
- mutation checks: the natural-end watch, the removal pass's finally, the purge flush of the watch and the watch's due-time order, each reverted by sed, fail at least one named CarrierLedgerTests test
- /code-review (0bce3db): dispositioned: a naturally ended carrier on a disabled NPC may never age → amendment A4 (~D5 natural-end watch, ~D20 Create writes LifeTime, Age and an empty stat buffer), built in e477d29; the faction-count pass ran every sweep → only on an event's first query; faction check before the prefab-name read; player teams missed offline characters → IncludeDisabled; debug native lines cut short in chat → logged in full; the stop line wording differed from the plan → "empower <id>: <k> carriers queued for removal (<why>)"; a throw outside the per-entry catch could lose a queued removal → finally re-queues it; unlogged samples were dropped at stop → kept; EmpowerAction could call KillOrDestroyEntity unchecked → preflight ban and fixture bad-15; an unreadable %TEMP% read as empty → a failed listing
- Codex verdict: READY (round 3) — round 1 REVISE: Leads failed open on a missing target, Samples() outside the scheduler catch, any T02 potion buff taken for a carrier, RemoveBuffSafe's TryRemoveBuff reason unchecked (fixed e477d29, fixture bad-14); round 2 REVISE: a sample marked logged before its read, Create cleared but did not establish the stat buffer, the watch processed from its head could hold back a due entry (fixed d5889b0); rounds 1–2 (and step 1's three rounds) ran with a Codex sandbox that blocked every file read, found in round 2's log and fixed with `-c features.experimental_windows_sandbox=true` (reads work, writes still denied); round 3 read the files and inspected steps 1–2 whole (ee36a7d..HEAD): READY, no findings
- in-game: none (Session 1 is step 4)
- dod status: 13/29 verified (D1, D2, D3, D5 re-verified after A4, D6, D7, D8, D9, D10, D12, D13, D20, D21); D4 and D14 wait for Session 2's read-back, D22 for the api 3 wire line (step 3) and the externalSelfTests registry (step 4)

### Step 3 · 2026-09-26 · 0f9c6d6 → 82d2aaf → 7f1bd1c
- compile / preflight: 0 errors, 0 warnings; 868 tests passed; PREFLIGHT OK with "wire contract: 7 tags, 4 api commands, all documented (api 3)"; -SelfTest pass (WireContract good and bad-3 re-copied at api 3); dod --check 0 problems
- mutation checks: the empower branch of ApiLines.Status disabled fails 6 tests; the Faction_ prefix kept fails 4
- /code-review (0f9c6d6): the row's worst-case length was unbounded by any test → The_largest_empower_row_fits_the_line_limit (82d2aaf, then corrected in 7f1bd1c to five distinct allowed factions); the empower admin count comes from EmpowerAction.CarriersOf, so a waves event's tracked-unit count is unchanged; no other findings
- Codex verdict: READY (round 2, file access confirmed) — round 1 REVISE: the contract promised tolerance of unknown keys only while api 3 first sends kind=empower, and the handoff both said "no change" and asked for a rendering change (contract §1 now states value tolerance and that kind=empower was listed since api 2; the handoff gates empower rendering on api>=3); the size test repeated one faction (now five distinct, non-denied)
- in-game: none (step 3 is the wire shape; Session 2 reads `.nyar api status` in game)
- dod status: 15/29 verified (D1, D2, D3, D5, D6, D7, D8, D9, D10, D11, D12, D13, D20, D21, D27)

### Step 4 · 2026-09-26 · 5d775a9 → 5b774af → 7400fc6
- compile / preflight: 0 errors, 0 warnings; 869 tests passed; PREFLIGHT OK; -SelfTest 27/27; -Paths clean; `pwsh tools/dev-snapshot.ps1 -SelfTest` → "snapshot selftest: 6/6"; `pwsh tools/rollback-drill.ps1 -SelfTest` → "drill selftest: 6/6"; dod --check 0 problems
- mutation checks: removing the restore's copy pre-check, its running-server guard, the two-held guard, the other-server guard, the directory record or the kind check each drops the snapshot selftest below 6/6; removing CarrierLedger's TickRemovals increment fails CarrierLedgerTests.A_drain_reports_its_removals_per_tick
- /code-review (7400fc6 diff): fe2 matches the shipped template's pillar and trigger syntax; fe2's cfg footprint (EventSpawns, TimingLog, EventBanners beyond fe1's two keys) is what D18, D19 and UX 11.2 need, is backed up first and is undone by the session's snapshot restore (documented in --help); a test written for the round 3 finding exposed Format-EntryFiles turning the $null of an empty listing into one blank row, so an empty tree could never be saved → nulls dropped
- Codex verdict: READY (round 6) — round 1 REVISE: a restore deleted live files before noticing a corrupted copy (blocking), the save missed files added mid-copy and empty directories, the selftest skipped A5's restore refusals; round 2 REVISE: earlier entries not re-read after later ones copied (fixed), fe2's wider cfg footprint (rejected as a defect: required by D18/D19, backed up, restored by the snapshot; documented); round 3 REVISE: a Dir entry replaced by a file passed the pre-check (fixed, selftest case added); round 4 REVISE: the Session 2 steps left the respawn bound, the natural-end read-back and S-7's 200-removal tick unobservable → amendment A6 (verbose removal-per-tick line), fe-expire, fe-big, 5 s debug polling; round 5 REVISE: D15's five stats not compared on matched rows → the steps pin the owner's spot and state the comparison; round 6: READY, no findings. Every round's log: 0 "blocked by policy"
- in-game: Session 1 (unattended) passed, recorded in docs/features/FACTION_EMPOWERMENT.md › Test results › Session 1 and under Sessions below; the A6 line is first seen in Session 2
- dod status: 16/29 verified (D17 added from Session 1); D28's selftest passes and its "each session records the restored line" clause completes after Session 3

### Step 5 · 2026-09-26 · 7400fc6 (the Session 2 build) → 8e26984 → f706fe6
- compile / preflight: 0 errors, 0 warnings; 869 tests passed; PREFLIGHT OK; dod --check 0 problems
- in-game: Session 2 with the owner (docs/features/FACTION_EMPOWERMENT.md › Test results › Session 2); D4, D14 and D19 pass; D15, D16 and D18 stay open with their remaining checks in Build plan step 6; S-7 validated (owner decision 1B, 185 removals in one tick); A7 adds D30 (cooldown reply names time left) from the owner's four "purge cooldown active" replies; the step 15 miss was a step error (the dev cfg held PurgeCooldownSeconds = 240), fixed by session-events mode fe3
- /code-review: the step's diff is records only (no source change); checked by the Codex cross-inspection below against the log
- Codex verdict: READY (round 2) — round 1 REVISE: the natural end was claimed as a full D16 pass from one sample line without a debug read-back (blocking → carried to Session 3 as an in-game read-back), and three wording fixes (timing highest average qualified to the empowerment interval with the boot-wide 2.481 ms, hp rounding, movement-speed pairs called matched rows rather than the same units); round 2: EARLIER all resolved, no findings. Log excerpts sent with the SteamID redacted; 0 blocked reads
- dod status: 19/30 verified (D4, D14, D19 added; D30 new)

### Step 6 · 2026-09-26 · ee13316 (the Session 3 build) → 19513df → 4f0d5d4
- compile / preflight: 0 errors, 0 warnings; 871 tests passed; PREFLIGHT OK; -Paths "778 walked, all in manifest" after archiving %TEMP%\nyar-session; dod --check 0 problems (faction-empowerment and event-library)
- mutation checks: CooldownLeft with Floor instead of Ceiling fails CooldownLeft_rounds_the_seconds_up; with >= instead of > fails CooldownLeft_is_null_once_the_cooldown_is_over
- /code-review (5a74b75..HEAD): EventRuntime matched the purge blocker by a second copy of its label, so renaming the label in Precedence would silently drop the seconds-left reply → one constant Precedence.PurgeCooldown (bfa863c), no behaviour or text change; fe3 and the Session 3 steps reviewed through the Codex rounds below
- Codex verdict: READY (round 2 of the post-session pass) — pre-session pass: rounds 1-6 REVISE on the owner steps (listed under the pre-audit), round 7 READY; post-session record (bc6771a..19513df with redacted log excerpts): READY round 1, no findings; refactor and A8/A4 (19513df..bfa863c): round 1 REVISE, one low finding (A4 named only the damage check, not the by-eye view; fixed), round 2 READY; every round's log: 0 "blocked by policy" besides round 1's audit-text hit
- in-game: Session 3 with the owner (docs/features/FACTION_EMPOWERMENT.md › Test results › Session 3); D18, D28, D30 pass; D15 and D16 pass after owner decision A (amendment A8, corrected; the damage check moves to event-library D23 by that plan's A4)
- dod status: 24/30 verified; open D22, D23, D24, D25, D26, D29 (all Build plan step 7)

### Step 7 · 2026-09-26 · 35c5425 → f45d4f6 → 7d55e3b → ae3821b (v0.4.0) → 027a246 → 7b280f5
- tcli build: Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.4.0.zip, built from ae3821b (clean tree; `dotnet build -c Release --no-incremental`, 0 warnings, 0 errors, the DLL also deployed to the stopped dev server with an equal hash) with icon.png, README.md, manifest.json, BepInEx/plugins/Nyarlathotep.dll (325632 bytes), CHANGELOG.md, LICENSE
- zip sha256: kdpen-Nyarlathotep-0.4.0.zip E17C6564CB0CDCCB5AF3351E59D75C34491270463387958CC727093C7F3412BF
- compile / preflight: 0 errors, 0 warnings; 871 tests passed; PREFLIGHT OK with "release tags: 4/4" after the push and "selftest registry: 6 external selftests, each with its cases; every tools -SelfTest registered"; -SelfTest "28/28 checks, 6/6 external selftests (… secrets: none (802 files scanned, 789 index blobs) on the real tree)" at 027a246; -Paths "805 walked, all in manifest"; -SessionsOf "faction-empowerment 5/5 checked" once the two owner-steps headings stopped reusing session numbers ("### Owner steps for Session 2/3"); dod --check 0 problems
- selftests and mutations: drill 8/8 (dropping the reason check → 7/8, pair-other passes); repo rollback 5/5; gate 5/5 (ignoring the success line → 4/5, ignoring failures → 1/5); release verify 4/4; preflight -SelfTest with the drill's registered success line changed → "5/6 external selftests — FAILED"
- amendment A9 (discovered, the lesson of raphael-api-core A10): the repository drill accepts, before the push only, v0.3.0's preflight failing just "release tags: 3/4 (v0.4.0 not pushed)"; recorded before it was built
- /code-review (c738f23..ae3821b, a fresh-context agent): no high or medium findings; four low: gh's exit code not seen inside release-verify's closure, the first of two "zip sha256:" lines used, a locked worktree folder left silently by the repository drill → fixed in 027a246 (release verify 4/4, repo rollback 5/5 again); the drill's "(<n> newer action types)" counts definitions, not types → kept, it is D23's required wording
- release: v0.4.0 (ae3821b) tagged; rollback gate before the push "rollback gate: 3/3" (Session 5; the repo part with -BeforePush); privacy grep (7656119, kdpenland) over v0.4.0's tree and the pushed range: only the plans' own "Grep for" lines, their fixtures, the raphael-api-core audit sentence and the step 7 pre-audit line naming the two patterns; the commit author identity is the owner's git config, as on every earlier push; pushed main and the tag; GitHub pre-release https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.4.0 with the zip; `pwsh tools/release-verify.ps1 -Tag v0.4.0 -Asset kdpen-Nyarlathotep-0.4.0.zip` → "release verify: hashes equal"; after the push D25's full command (`tools/repo-rollback-drill.ps1 -From v0.3.0 -To v0.4.0`) → 703 tests, v0.3.0's PREFLIGHT OK with "release tags: 4/4", "rollback: clean". No tcli publish: the owner publishes to Thunderstore
- Codex verdict: READY (round 2 of the post-audit pass) — tools 35c5425: round 1 REVISE (F1 blocking: the zip sha256 line not anchored to its own line; F2 advisory: newly disabled definitions compared by id and reason) → f45d4f6, round 2 READY; A9 7d55e3b: round 1 READY; post-audit over 7d55e3b..ae379df: round 1 REVISE (F1 blocking: the feature doc said event-library "takes" checks not yet run → "deferred"; F2 advisory: "about 15 s" omits the unfinished-sweep delay → rejected, batch ticks at 200 per tick keep it about 15 s and the changelog ships in the published zip; F3 advisory: a stale worktree registration not checked → fixed) → 7b280f5, round 2 READY; every round's log: 0 "blocked by policy" besides round 1 post-audit's two hits in audit text Codex read
- in-game: Sessions 4 and 5 (docs/features/FACTION_EMPOWERMENT.md › Test results), both unattended drills on the dev world

## Sessions
- session 1 log check: 0 unhandled, 48 nyar lines, 0 orphan errors, 0 unity errors
  - boot 1 (12:25–12:40): "log check: 0 unhandled, 33 nyar lines, 0 orphan errors, 0 unity errors"; boot 2 (12:40–12:43): 9 nyar lines, otherwise the same; boot 3 (12:43): 6 nyar lines, otherwise the same; each run before the next boot
- session 2 log check: 0 unhandled, 575 nyar lines, 0 orphan errors, 0 unity errors
  - one boot (13:06–17:47), owner connected 17:19–17:47; run after the stop and before any restart; BepInEx log: three known warnings plus the stop and purge warnings; Unity log: 226 PrefabLookupMap lines, all before "Startup Completed"; then "snapshot restored; hashes equal (s2, … deleted)"
- session 3 log check: 0 unhandled, 455 nyar lines, 0 orphan errors, 0 unity errors
  - three boots: Part 1 (boot to 19:34, "0 unhandled, 367 nyar lines"), without Nyarlathotep (19:35–19:52, 0 nyar lines), Part 2 with General.Enabled = false (19:53–20:41, "0 unhandled, 88 nyar lines"); each checked before the next boot; BepInEx: the three known warnings, Bloodcraft's and KindredCommands' own warnings, one Bloodcraft [Error] (FamiliarBindingSystem), our purge warning; Unity: 226 PrefabLookupMap GUIDs before "Startup Completed" in each; then "snapshot restored; hashes equal (s3, … deleted)"
- session 4 log check: 0 unhandled, 8 nyar lines, 0 orphan errors, 0 unity errors
  - the drill's last boot (v0.3.0 on HEAD's files); its seed and drill-mark boots of HEAD read 7 and 15 nyar lines, 0 unhandled, 0 orphan, 0 unity; each run by the drill before the next boot; BepInEx: the three known warnings (Il2CppInterop, two Beelzebub TUNE) and v0.3.0's expected "unknown action type Empower"
- session 5 log check: 0 unhandled, 7 nyar lines, 0 orphan errors, 0 unity errors
  - the gate's drill, last boot (v0.3.0 on v0.4.0's files); the v0.4.0 boots read 8 and 16 nyar lines, 0 unhandled, 0 orphan, 0 unity; BepInEx and the server log after the gate: the same three known warnings and the expected Empower line, no [Error]
