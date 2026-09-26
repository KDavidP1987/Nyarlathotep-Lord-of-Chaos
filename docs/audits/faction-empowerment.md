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

## Post-audit
### Step 1 · 2026-09-26 · 0559d4c → 23f2ba4 → 5ca6812
- compile / preflight: 0 errors, 0 warnings; 848 tests passed; PREFLIGHT OK; dod --check 0 problems, 0 warnings
- mutation checks: each of the 7 original controls and each post-audit fix, reverted by sed, fails at least one named test
- /code-review (0559d4c): no defects; observations dispositioned: a unit whose stopped carrier is still queued for removal could get a second carrier → fixed (CarrierOf "removing", test A_unit_whose_stopped_carrier_is_still_queued_is_not_given_a_second_one); SweepPlan.From quadratic → HashSets; a vanished-unit comment corrected; PrefabUnitCatalog has no IFactionCatalog yet → step 2 (Empower definitions load disabled in game until then); prune order and float rounding → already fixed from Codex round 1
- Codex verdict: READY (round 3) — round 1 REVISE: LifeTime float rounding up, create-then-mark crash window, stale removals free of budget, prune before a throwing query (the second became amendment A2, ~D20; the other three code defects, fixed in 23f2ba4); round 2 REVISE: a failed expiry fallback cleared the unit's removing state while its carrier still existed (fixed in 5ca6812 with a tombstone held while the buff exists); round 3 READY, no findings
- in-game: none (step 1 is pure logic)
- dod status: 7/29 verified (D1, D2, D3, D5, D6, D10, D12); D4, D7, D9, D13 and D20 pass their logic tests and wait for their game half (step 2, Session 2)
