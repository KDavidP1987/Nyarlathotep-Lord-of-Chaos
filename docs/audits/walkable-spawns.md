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
- server: `pwsh tools/dev-snapshot.ps1 -Save ws1` → "snapshot saved: ws1 (28 files)"; baseline boot of the deployed 0.5.0 DLL (0.5.0+06d6325) on nyardev: -LogCheck "0 unhandled, 8 nyar lines, 0 orphan errors, 0 unity errors"; warnings only Il2CppInterop Class::Init, Beelzebub's own two and ours "event example-empowerment: pillar empowerment takes an Empower action" (the leftover dev events.json entry, as in event-library's baseline); NyarDev.log carries only the game's PrefabLookupMap missing-prefab traces; stopped before the step 1 DLL is deployed
- found on the way, recorded before the code that depends on it:
  - A8 (discovered, ~D1 ~D10, layer 6.1): the metadata does not say whether the tile calls take world metres or the tile grid, so the probe reads both spaces and the go/no-go is per source.
  - A9 (discovered, ~D10, layer 12.4): the fixture battery knows good, bad* and empty only; good-probe folds into SessionLogs/good and empty-probe becomes bad-probe-4.

## Post-audit
### Step 1 · 2026-09-28 · 2536488 (in progress: Session 1 pending)
- compile: 0 errors, 0 warnings; tests: 1351 passed (CommandArgTests WalkRadius 13 cases, ChatBytes)
- preflight: PREFLIGHT OK; -SelfTest "33/33 checks, 7/7 external selftests (… 136 extra bad fixtures …)", SessionLogs good passes with its probe record, bad-probe to bad-probe-5 fail for their planted reasons; -Paths -DeclaredOf walkable-spawns OK before the session (during it only "leftover temp nyar-snap-ws1", the open snapshot)
- /code-review (fresh subagent, read-only) on e34dadb..4dade0f: F1 a default TileWorld handed to native code and F2 GetIsGrounded with negative world indices could fault natively → fixed, A10 (live singleton only; grounded read once in tile space, off-grid refused); F3 the floored tile-space circle centre sits on a four-tile corner → fixed (x·2 + 6400 unfloored for the circle, floored index for grounded); F4 `debug here 30 junk` now ran silently → refused "arguments must be 1-2"; F5 the unavailable line beside readings → kept only as a per-space failure note; F6 unknown or case-folded sources → sources checked case-sensitively against the two known names; F7 no bug
- Codex verdict: REVISE (round 1, e34dadb..4dade0f) — F1 TilePolygons never disposed → fixed (nested try/finally); F2 query leak if ToEntityArray throws → fixed; F3 a record without a ledge passes → rejected by reference to D1 (incomplete means fewer than five readings; the ledge is outside the verdict); F4 a record may omit a source → fixed (both sources required, fixture bad-probe-5); F5 loose reading regex → anchored with WalkReply's numeric shapes; repeated readings of a label stay allowed (D1 "every dry reading")
- Codex verdict: REVISE (round 2, e34dadb..2536488) — F1 the tile-space circle centre should be floored as in A8 → rejected: A8 cites floor(x·2) + 6400 for the grid's scale and offset; CheckStaticCircle takes a float2, and a floored centre is the corner of four tiles, which reads blocked beside one blocked tile (the code review's F3); the floored index stays for GetIsGrounded, which takes an int2. No other finding.
- in-game: deployed 0.5.0+2536488 on nyardev inside snapshot ws1; initialized cleanly; Session 1 (owner) pending
