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

