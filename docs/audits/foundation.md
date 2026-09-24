# Audit — foundation

Build plan steps 1–9 of docs/dod/foundation.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line (D36). Session log
checks are lines "- session <n> log check: …" (D33).

## Pre-audit
### Step 1 · 2026-09-24 · e5ebddc
- pre-child (D38): e5ebddc — the parent of this entry's commit, the first commit of the build
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0
- dod status: foundation 0/38 verified (just started); Epic 11/46
- feature doc read: none yet — docs/features/FOUNDATION.md is created by this step; read docs/dod/foundation.md Business rules 1–9, Design › Data (EventDefinition v1, state.json v1), UX (command and cfg tables), D1–D6; docs/GAME_ASSETS.md › Do-not-spawn list
- toolchain: SDKs 9.0.316 and 10.0.302, runtime Microsoft.NETCore.App 6.0.36 present, so a net6.0 test project runs
- server: not running; not touched by this step

### Step 2 · 2026-09-24 · 8562fe3
- git status: clean at 8815d4a and again at 8562fe3 (after the A1 re-review)
- compile: 0 errors, 0 warnings
- preflight: exit 0
- dod status: foundation 5/38 verified (D1, D3–D6); review re-opened by A1 (gating 14.4), cleared by Review 7 READY (Codex rounds 5–7) before any step 2 code
- feature doc read: docs/features/FOUNDATION.md (Status: step 1 of 9; Open questions: none); plan Interfaces, Design › Data, States › Startup and shutdown, Failure & observability, the file-write fence in tools/preflight.ps1
- baseline boot: none of the old DLL — there was no world to boot it in (the gap A1 records), and D34 wants the snapshot before the first boot; the step's first boot is the first boot of save-data-nyardev
- server snapshot (D34): `pwsh tools/preflight.ps1 -ServerWrites -Snapshot $env:TEMP\nyarfoundation-before.tsv` at 2026-09-24 15:01 (after the tooling commit ad6bd79, before the deploying build and the first boot) → "snapshot of 1634 files and folders"; no save-data-* folder existed; server and game client not running
- found on the way: A2 (defect: the fence read File.Move's overwrite flag as a path), A3 (discovered: D23's reload half and D33's -LogCheck were claimed by step 2 but built later)

## Post-audit
### Step 1 · 2026-09-24 · 40505e2
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 194, Failed 0; classes ControlPrecedenceTests, ScheduleTests, EventValidationTests, ConfigClampTests, CommandArgTests, IdempotencyTests
- mutation check: swapping purge and General.Enabled in Precedence.StartBlocker failed 8 cases; removing the occurrence-key comparison in Schedule.Due failed 8 cases; restored code passes
- preflight: exit 0; `-Paths` → "paths: 374 walked, all in manifest" (the test project was already in tools/paths-manifest.txt and .gitignore, so neither changed)
- toolchain note: Microsoft.NET.Test.Sdk 17.14 refuses net6.0; pinned 17.8.0 with xunit 2.9.3 and xunit.runner.visualstudio 2.8.2 (within S-6's "17.x", "2.9.x", "2.8.x"; no amendment)
- /code-review (inline, c1c6faa..7998ac2): Logic/ has no Unity, Il2Cpp or BepInEx using; each Logic file opens `#nullable enable` because the plugin has no <Nullable>; validation stops at the first failing field so each disabled event carries one reason; no finding beyond Codex's
- Codex cross-inspection round 1 (7998ac2): REVISE, 5 findings — (1) boss names skipped the CHAR_ rule: accepted in part, CHAR_ and catalog enforced, the spawn deny list not applied because a kill trigger never spawns; (2) stale check by write time only: accepted, FileStamp adds length and SHA-256; (3) dedupe window exclusive at 5 s: accepted, inclusive; (4, 5) reload tests did not use a reload path: accepted, Logic/EventCatalog added and the tests go through it
- Codex cross-inspection round 2 (40505e2): no defects; every earlier finding resolved
- Codex verdict: READY (round 2)
- in-game: none (pure logic step)
- dod status: foundation D1, D3, D4, D5, D6 pass lines added (D2 waits for every class of D3–D16)
