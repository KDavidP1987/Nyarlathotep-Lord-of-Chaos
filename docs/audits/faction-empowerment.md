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
