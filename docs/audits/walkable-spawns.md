# Audit — walkable-spawns

Build plan steps 1–2 of docs/dod/walkable-spawns.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.5.0.

## Pre-audit
### Step 1 · 2026-09-28 · e34dadb
- pre-child: event-library closed (v0.5.0 tagged and released on GitHub; the owner publishes to Thunderstore); the plan was approved on subagent Review 2 READY (b228a5a) with advisories A1–A7 applied after approve; rollback base is v0.5.0
- git status: clean at e34dadb
- compile: 0 errors, 0 warnings
- tests: 1337 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: walkable-spawns 0/12 verified (just started); review subagent, not pending (A1–A7 are not gating)
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0; tcli is not on the PATH here (step 2's `tcli build` runs from its install)
- feature doc read: docs/features/WALKABLE_SPAWNS.md, created by this step (Status: in build); plan D1, D7, D8, D9, D10, Interfaces, Design › Data, Build plan step 1, Rollout › Paths walked; Commands/SpawnCommands.cs, Logic/CommandArgs.cs, Logic/AdminLines.cs, Services/SpawnTracker.cs (Regroup, the Height read), tools/preflight.ps1 (Test-CheckSessionLogs, Invoke-FixtureBattery), tools/preflight-checks.json, tools/data-inventory.json; KindredCommands Helper.ConvertPosToTileGrid; scratchpad walk-probe notes (the ProjectM.Shared metadata)
- server: not running; the baseline boot is taken with Session 1's snapshot
- found on the way, recorded before the code that depends on it:
  - A8 (discovered, ~D1 ~D10, layer 6.1): the metadata does not say whether the tile calls take world metres or the tile grid, so the probe reads both spaces and the go/no-go is per source.
  - A9 (discovered, ~D10, layer 12.4): the fixture battery knows good, bad* and empty only; good-probe folds into SessionLogs/good and empty-probe becomes bad-probe-4.

## Post-audit
