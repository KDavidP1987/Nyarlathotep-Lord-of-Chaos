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

### Step 3 · 2026-09-24 · a4971e5
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0
- dod status: foundation 8/38 verified (D1, D3–D9); review codex, no pending re-review
- feature doc read: docs/features/FOUNDATION.md (Status: step 2 of 9; one open question, D28's "still loading", which step 5 answers); plan Business rules 7, Design › Permissions, UX, Security › Injection, the gating-control matrix; docs/RAPHAEL_INTEGRATION_CONTRACT.md §1, §2 and §4
- baseline boot: none; step 3 is pure logic and tooling, with no in-game test
- how "mutating" is identified (the plan leaves this open): a service method carries Logic's [Mutating] attribute. Test-CheckGatewayOnly collects those methods and requires every call site in Commands/, Patches/ or Services/ outside the declaring file to sit inside a Gateway.Run(...) call. EventStore.Reload (ActionKind LoadDefinitions) is marked too, so the boot load runs through the gateway as Operator.
- Debug.FaultInjection: the cfg key is bound now, inside `#if DEBUG`, so that Test-CheckFaultInjection has a real reference to check; its consumer comes with the scheduler in step 5

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

### Step 2 · 2026-09-24 · 31cca9e
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 235, Failed 0; PersistencePathTests 12, PersistenceWriteTests 18 (incl. SeedTests), DependencyFailureTests 10
- mutation check: removing StateStore.Flush's one-second interval and the per-streak log gate failed 2 cases; removing DataPaths.Check's `..` and reparse-point checks failed 2 cases; reverting EventsFile.Seed to an overwriting promote failed 1; unguarding RegisterEach's discovery step failed 1; restored code passes
- preflight: exit 0; `-SelfTest` → "selftest: 20/20 checks, 3 fixtures each, 46 extra bad fixtures"; `-Paths` → "paths: 406 walked, all in manifest"
- /code-review (inline, 8562fe3..2d4e85c): one finding, fixed in 2d4e85c: EventStore.Initialize could let an exception escape into Core.TryInitialize, so it is now wrapped and logs "events.json load failed … no events loaded"
- session 1 log check: 0 unhandled, 3 nyar lines
- session 2 log check: 0 unhandled, 3 nyar lines
- session 3 log check: 0 unhandled, 2 nyar lines
- Codex cross-inspection round 1 (2d4e85c): REVISE, 6 findings. The fixes are in 0a27e9d.
  - (1) a junction above BepInEx/config: declined. It is the admin's deployment choice, and D7 fences the mod's file names and the data folder, which Guard checks before every access.
  - (2) older-schema migration can't be reached: accepted as a record. Schema 1 is the first version, so the migrate branch first becomes reachable at schema 2, which brings its own legacy parser and test.
  - (3) the seed checks, then writes: fixed with IFileStore.PromoteNew, which never overwrites.
  - (4) a hand edit can land during an admin edit: declined with a test. File.Replace keeps the hand edit as events.json.bak.
  - (5) command discovery ran outside the guard: fixed. The enumeration is guarded and loadable types are kept.
  - (6) -LogCheck matched only frames indented with three spaces: fixed with a regex and fixture LogCheck/bad-3.
- Codex cross-inspection round 2 (0a27e9d): all round 1 fixes and dispositions accepted. One new finding: the regex counted prose ("look at Nyarlathotep.Core"). Fixed in 4e3abe0 by requiring the frame's "(".
- Codex cross-inspection round 3 (4e3abe0): one finding: Mono puts a space before "(". Fixed in 31cca9e and added to bad-3, which now counts 4 unhandled lines. The good fixture holds the prose line and passes. The live log still reads 0 unhandled.
- Codex verdict: REVISE (round 3 of 3, at the cap). Its one finding is fixed in 31cca9e and proven by fixture LogCheck/bad-3; no finding is open.
- in-game: sessions 1–3 in docs/features/FOUNDATION.md › Test results (first run and seed, boot with an unknown unit, boot with broken JSON)
- dod status: D7, D8, D9 pass lines; notes on D28 (first run and empty state seen; "still loading" pending step 5) and D23 (boot half seen; the reload half is step 5)
- open: the -ServerWrites -Compare against $env:TEMP
yarfoundation-before.tsv waits for step 8 (D34), with the game client closed

### Step 3 · 2026-09-24 · 40d0e30
- compile: 0 errors, 0 warnings in Release and in Debug (the Debug build compiles the `#if DEBUG` FaultInjection key)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 449, Failed 0; AuthorizationTests 162 (every ActionKind × actor × enabled), WireFormatTests 13, TextSinkTests 32, PrivacyTests 7
- mutation check: each of 9 planted faults failed its class. The faults: Player granted Announce (4), System's enabled check removed (5), a denial not logged (57), wire values unmapped (5), the 480-byte cap removed (1), a handshake key dropped (1), control characters kept (6), the announcement length unchecked (1), and the status line leaking the radius (PrivacyTests, 1)
- preflight: exit 0 with "gateway: only ActionGateway mutates (1 call sites)", "fault injection: debug-only (2 references)", "admin list: 0 admin commands, equal to the commands check"; `-SelfTest` → "selftest: 24/24 checks, 3 fixtures each, 56 extra bad fixtures"; `-SessionsOf foundation` → "session logs: foundation 3/3 checked"; `-ListCommands admin` → "admin commands: 0"; `-Paths` → "paths: 483 walked, all in manifest"
- /code-review (inline, a4971e5..b084801): one finding, fixed in 49464d9. Remove-CsLiterals blanked whole interpolated strings, so a call inside `$"{…}"` escaped the gateway and fault-injection checks. Fixtures GatewayOnly/bad-3 and FaultInjection/bad-4.
- Codex cross-inspection round 1 (b084801): REVISE, 5 findings. The fixes are in 6801d7a.
  - (1) uses of a mutating method in its own declaring file were exempt: declined and aligned with the plan. D11 exempts "the services it dispatches to", which must call each other.
  - (2) the suffixed `[CommandAttribute]` form: fixed; fixture AdminList/bad-2.
  - (3) a session line with 0 nyar lines passed: fixed; fixture SessionLogs/bad-3.
  - (4) required wire fields dropped on overflow: fixed. `Wire.Record` throws instead.
  - (5) Unicode format and separator characters: fixed in TextSink.
- Codex cross-inspection round 2 (6801d7a): REVISE, 3 findings. The fixes are in 9980886.
  - (1) a file could exempt itself by declaring a dummy `[Mutating]`: fixed. The dispatched services are named, and a `[Mutating]` elsewhere fails (fixture GatewayOnly/bad-4).
  - (2) interpolated text gives false matches: declined, because the error is a loud failure and never a miss.
  - (3) supplementary-plane format characters: fixed by judging Rune scalars.
- Codex cross-inspection round 3 (9980886): one finding. A "(" in an interpolated string's text inside `Gateway.Run` stretched the span over a later direct call. This overturned disposition (2) of round 2 as far as parenthesis matching goes.
  - Fixed in 40d0e30: Hide-InterpolatedText masks the text and keeps the holes; the lexer takes a raw literal's `$` prefix; an unmatched span contains nothing. Fixture GatewayOnly/bad-5.
  - Known limit, documented at the lexer: a quote nested inside a hole ends the literal early, and the unmatched-span rule makes that fail rather than pass.
- Codex verdict: REVISE (round 3 of 3, at the cap). Its one finding is fixed in 40d0e30 and proven by fixture GatewayOnly/bad-5; no finding is open.
- in-game: none (pure logic and tooling step)
- dod status: D10, D11, D12, D14, D15, D18, D19 pass lines
