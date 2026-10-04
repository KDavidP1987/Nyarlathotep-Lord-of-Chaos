# Audit — automation

Build plan steps 1–4 of docs/dod/automation.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.7.0.

## Pre-audit
### Step 1 · 2026-09-29 · 37eb8a1
- pre-child: event-spawns closed and 0.7.0 released; the plan approved after Review 4 (READY) and started at 37eb8a1 (A1, A2)
- git status: clean at 37eb8a1
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2173 passed
- preflight: PREFLIGHT OK
- dod status: automation 0/31 verified (just started)
- feature doc read: docs/features/AUTOMATION.md is created by this step (the plan's Build plan step 1); docs/features/EVENT_SPAWNS.md read for the AroundPlayer pick and the wave gate the fan-out extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

### Step 2 · 2026-09-29 · def4d20
- git status: clean at def4d20 (step 1 built, reviewed and post-audited)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2388 passed
- preflight: PREFLIGHT OK
- dod status: automation 16/31 checked; `dod-index.mjs --check automation` → problems 0, warnings 0
- feature doc read: docs/features/AUTOMATION.md Status (step 1 built) and Open questions; step 2 carries round 2 F6 (WaveAction reports each decided wave once through EventEngine.WaveDecided)
- in-game baseline: the deployed 0.7.0 DLL (0.7.0+3aa2215) booted on the dev server: "triggers: all hooks available", "Nyarlathotep initialized … (attempt #1)"; 4 [Warning] lines, none new: ours is the shipped example-empowerment reason line, the others Il2CppInterop's substitute notice and two Beelzebub TUNE lines; 0 [Error]; -LogCheck PREFLIGHT OK; server stopped

## Post-audit
### Step 1 · 2026-09-29 · 3b8613a
- built: the schema and validation of the Interval, RegionEntered and FactionKills triggers and action.fanOut (D1, D4, D8, D10), IntervalClock (D2), Logic/PlayerTriggers.cs (RegionEntries, KillRule, KillWindows, PlayerTriggerGate, Phantoms; D9, D11, D14, D29), PlayerPick.ChooseMany and WaveGate.DecideGroups (D5, D6, D13), state.json NextInterval (D31), the chat fields and `event info` lines (D16, D27), api 6 with the contract and design §6 (D17), ControlCases rows and pending controls (D30), the registries, this record and docs/features/AUTOMATION.md
- compile: 0 Warning(s), 0 Error(s); tests: 2388 passed
- preflight: PREFLIGHT OK; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"; -SelfTest → "selftest: 40/40 checks, 7/7 external selftests"
- found by the new tests while building: a FactionKills `event info` line of 628 bytes with five 96-character factions → EventLines.Fit shortens it to "factionkills <n> factions …" with "factions:" lines (D27); "needs a Interval trigger" → "needs an Interval trigger"
- /code-review round 1 (fresh subagent, read-only, on 3b8613a): VERDICT READY, 9 advisory findings — F1 Serialize nulled an empty NextInterval in place → fixed (the document is left unchanged; StateNextInterval test); F2 the D17 push test checked a test helper → fixed (EventEngine.WaveDecided, the one report step 2's WaveAction calls, is what the test drives); F3 no reason defined for a fanned-out wave dealt 0 by MaxTrackedUnits → A3 (discovered); F4 a skipped wave logged "0 units rolled" without "skipped:" → fixed (WaveLines.ZeroRolledSkip, D6's form); F5 the phantom anchor ignored territory and scope → fixed (Phantoms.Place takes the pick's test); F6 trigger.factions refused with another wording than D10's → fixed (TriggerFactionsRule); F7 aged-out kill counters stayed until the bounds → fixed (KillWindows.Prune, called per scan in step 2); F8 a picked wave with no centre spawned at the origin → fixed (treated as no eligible player); F9 the contract's api 5 sentence → reworded
- /code-review round 2 (fresh subagent, read-only, on 37eb8a1..8848be3): VERDICT READY, 7 advisory findings — F1 WaveDecided counted a blocked (NoWave) wave as skipped → fixed (neither counted nor pushed, as 0.7.0); F2 RegionEntries dropped cooldown rows when a definition left the scan (disable, reload, regions unavailable) → fixed (rows keep their own expiry and end only on expiry, the bound or restart, as Design › Data says); F3 a unit killing itself counted for its owner → fixed (KillFacts.VictimIsKiller, D11); F4 `location here` moved a fanned-out event off AroundPlayer and the reload disabled it → fixed (refused: "action.fanOut needs an AroundPlayer location: set action.fanOut none first"); F5 `event info` promised a next start for a disabled Interval definition → fixed; F6 the D17 push control cannot see WaveAction yet → carried to step 2 (WaveAction reports every decided wave through EventEngine.WaveDecided exactly once, checked in step 2's code review and Session 1); F7 NextIntervalConverter shifted an unspecified time by the server offset → fixed (written as UTC unchanged)
- no round 3: both rounds READY; round 2's fixes are covered by new cases in AutomationTests, AuthoringTests Automation and PushTests Automation
- Codex verdict: READY (round 1, on 37eb8a1..3fd6f06) — no findings; tests not run in its read-only sandbox (MSBuild temp denied), run here: 2388 passed
- in-game: none in step 1 (Session 1 is step 2)
- dod status: 19/31 with pass lines at 3fd6f06; 16 checked (D1, D2, D4, D5, D6, D8, D9, D10, D11, D13, D14, D16, D19, D29, D30, D31); D17 waits for step 2's WaveAction (round 2 F6), D20 for step 2's services, D27 for Session 1's manual part; --check problems 0

### Step 2 · 2026-09-29 · 75d92a7
- built: TriggerBus's Interval poll in its own tick phase "interval" (Logic IntervalClock.PollState over state.json) and the 5 s "player triggers" scan (D2, D3, D9); Logic PlayerTriggerFeed (the guarded scan and kill read, their once-per-streak lines and health entries, the gate's throttled refusals; D11, D14, D15); Services KillReader and Patches/DeathEventPatch (ledger membership, SpawnTracker.Died, then the V Blood path and the kill feed each in its own try/catch); EventRuntime.StartEvent's focus and quiet refusals (D13, D14); WaveAction's fan-out through PlayerPick.ChooseMany, WaveGate.DecideGroups and Logic WaveRun.Run, one Hunt tag per group and one EventEngine.WaveDecided per wave (D5, D6, D17); Debug-only phantoms (D29); HealthMonitor entries; AutomationDependencyFailureTests and `dependencySuites.automation` (D15, D28)
- found while building: preflight's gateway check matches the name `Tick` outside Gateway.Run, so IntervalClock.Tick became PollAll; the player-trigger logic moved from TriggerBus into Logic PlayerTriggerFeed so the D15 failure paths are testable
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2404 passed
- preflight: PREFLIGHT OK; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"; -DependencySuite automation → "dependency suite: automation 4/4 (player-scan, kill-read, state-write, release-tools)"
- /code-review round 1 (fresh subagent, read-only, on def4d20..7ba9200): VERDICT READY, 6 advisory findings, all fixed in 1fce105: F1 region rows kept through a gap with no scan, so a login during it counted as an entry → fixed (RegionEntries.ForgetRegions); F2 the kill-read health entry outlived the last FactionKills definition → fixed (cleared by the scan); F3 TriggerBus.WantsKills unguarded in the death patch → fixed (KillFaults streak); F4 the Interval poll shared the "triggers" phase → fixed (its own phase); F5 the garbage-faction test passed without its control → fixed (PlayerTriggerFeed.Clean asserted directly); F6 phantom group lines for a non-AroundPlayer wave → fixed
- Codex round 1 (def4d20..1fce105): REVISE — F1 exception messages in log lines → rejected by reference: D15 specifies "scan failed: <message>" and "read failed: <message>"; F2 refusal rows unbounded → fixed in 0c2a09a (PlayerTriggerGate.Keep on each scan); F3 the D17 test did not reach WaveAction → fixed in 0c2a09a (a structural check), replaced in round 3
- Codex round 2 (def4d20..0c2a09a): REVISE — F1 blocking: a throwing VBloodKilled aborted the death loop → fixed in 7b0d564 (the V Blood path guarded per death, "vblood kill: death skipped" once per streak)
- Codex round 3 (def4d20..7b0d564): REVISE — F1 the D17 check was textual → fixed in 75d92a7 (Logic WaveRun.Run carries the decided-wave sequence, tested per outcome, and WaveAction's only WaveDecided is WaveRun's report); F2 the state-write tests composed the poll by hand → fixed in 75d92a7 (Logic IntervalClock.PollState is TriggerBus's poll and the tests drive it). Both controls were checked by planting the fault: 3 tests failed, then passed on the restored code
- Codex verdict: READY (round 4, on def4d20..75d92a7) — no findings
- in-game: Session 1 (D3, D12, D27) follows; results under docs/features/AUTOMATION.md › Test results
- dod status: D15, D17, D20, D28 pass at 75d92a7 and are checked; D3, D12, D27 wait for Session 1
- session 1 log check: 0 unhandled, 833 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (boot 1); boot 2 "log check: 0 unhandled, 34 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; every [Warning] kind listed in docs/features/AUTOMATION.md › Session 1; no [Error]
- session 1 snapshot: "snapshot restored; hashes equal (au1, …)"

### A4-A6 fixes · 2026-09-29 · 6b5fd4b
- recorded first: amendments A4 (AroundPlayer regroup), A5 (walk height below 0), A6 (player scan cost), all defect, and design §9 D30 (owner, plan mode option A) in a74b7c1, before the code
- built (235d5ee): WavePlan.GroupAnchor gives an AroundPlayer group no anchor and WavePlan.WalkY reads its walk check at the player's height (A4); WalkHeight.MinY -100 (A5); PlayerQuery's User query built once, and with Debug.TimingLog a "player triggers" scan of 5 ms or more split into read, regions and rest (A6)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2408 passed; the A4 and A5 controls checked by planting the fault (4 tests failed, then passed on the restored code)
- preflight: PREFLIGHT OK; -AuthSuite pass
- /code-review (fresh subagent, read-only, on a74b7c1..235d5ee): VERDICT READY, advisories fixed in 6b5fd4b: WALKABLE_SPAWNS.md still said 0-1000 m; EVENT_LIBRARY.md and AUTOMATION.md did not say AroundPlayer groups are not regrouped; PlayerQuery.Read's try/finally left over-indented; the query is never disposed → accepted, as TriggerBus's DayNightCycle query (built once for the server's life); negative heights through TileLayerUtility.GetHeightLevel → checked in Session 1b
- Codex round 1 (a74b7c1..235d5ee): REVISE, one advisory: the A4 test covered only the helpers → fixed in 6b5fd4b (EngineTests AnchorWiringProblems over WaveAction's source, with two planted-fault cases in the test)
- Codex verdict: READY (round 2, on a74b7c1..6b5fd4b) — no findings; its log's 3 "blocked by policy" hits are quoted text of docs/audits/faction-empowerment.md, no command blocked
- in-game: Session 1b (D12, D27 and the A4-A6 checks) follows
- session 1b log check: boot 0 (6b5fd4b, before the owner) "log check: 0 unhandled, 76 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; boots 1-2 (e1ba90a, the session): 0 [Error] lines in both logs, every [Warning] kind listed in docs/features/AUTOMATION.md › Session 1b
- session 1b: D12 and D27 pass (e1ba90a); amendments A7-A9 recorded before their build; the snapshot au1b is restored after Session 1d

### A6-A10 build · 2026-09-29 · 3207666
- recorded first: A7-A9 and design §9 D31 (owner, plan mode option A) in 148ae80; A10 recorded with round 1's fixes in fa164f4 (the fallback under minDist and the half-radius ring skip were not in the approved plan; the owner confirms A10 at Session 1d)
- built: A6 verbose scan line only on change, the TimingLog split with log and gc (e1ba90a); A7 Logic/Spawning.cs WalkLine, WalkReach, WavePoints.Plan's line test, FanOutPick.Origins, WavePlan.Reach, the wave line's shortened count; A8 TickTimer.Scanned and ", <n> player scans" (05ec2c4)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2420 passed; controls checked by planting each fault (the line test off: 5 failed; the spent guard, free-answer reuse, the zero-length line, the in-scope keep: each its own test failed)
- preflight: PREFLIGHT OK; -AuthSuite pass
- /code-review round 1 (fresh subagent, read-only, on 2133e8a..05ec2c4): REVISE — F1 blocking: D32 and D31 said the line starts at the wave centre and did not name the fallback → reworded under A10; F2 the zero-length line (as Codex F1) → fixed; F3 the tile cache (as Codex F3) → fixed; F4 the scan counter grew with TimingLog off → fed only with TimingLog on; F5 weak budget test → strengthened; F6 FanOutPick ToString privacy → tested
- Codex round 1 (2133e8a..05ec2c4): REVISE — F1 blocking: a line of length 0 checked nothing → fixed; F2 blocking: the fallback under minDist → kept as A10, D32 and D31 reworded, owner to confirm; F3 blocking: a free answer reused within a half-metre tile → fixed (grounded per tile and blocked reused, free never). Log: 0 "blocked by policy"
- /code-review round 2 (fresh subagent, on 2133e8a..fa164f4): READY, 5 advisories fixed in 3207666 (the in-scope fallback sample, a stale comment, a budget case past minDist, the scan-drain source check, a reordered TickTimer test)
- Codex round 2 (2133e8a..fa164f4): READY, no findings; round 3 (fa164f4..3207666): READY, no findings; logs: 0 "blocked by policy"
- in-game: Session 1d (D33, A10) follows

### A11-A12 build · 2026-09-29 · e6c5e86
- recorded first: A11 and +D34 in dbeed30 (Session 1d: rock face pass, pond and river crossing all unchecked with no reason; the planning tick's 19 ms against 58 ms showed every line failing at its first sample, not a spent budget); the survey is a log line, not a `debug` verb, since walkable-spawns D9's DebugCommands check keeps `debug` to `here`; A12 recorded with round 2's fix
- built: UncheckedReason and WavePoints.UncheckedText (the wave line's reasons; the player's spot for one group), WaveWalk.NoLine and OriginSpot, WalkSurvey (verbose, own 482-call budget, once a wave, "no check" after a failure) (cd2f2bb, f6f44e4, e6c5e86)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2427 passed; controls checked by planting each fault (no NoCheck reason, wrong reason, no `lines` test, spot read per point, spot read at the centre, NoLine flag dropped, survey off by one, survey unbudgeted, survey on the placement budget, survey after a failure, survey reason swapped, survey coordinate, survey empty, survey flag per group): each its test failed
- preflight: PREFLIGHT OK; -AuthSuite pass; dod-index --check automation 0 problems
- /code-review round 1 (fresh subagent, read-only, dbeed30..cd2f2bb): REVISE — F1 blocking: the survey called a probe whose wave had failed → "no check", no calls; F2 the survey took the placement budget → its own budget; F3 the spot shown for fan-out waves → one group only; F4 four defects the reason test missed → four cases; F5 D32 wording → "no walkable line"; F6 advisory ordering check, kept
- Codex round 1 (dbeed30..cd2f2bb): REVISE — blocking: a NoLine group with no budget left for the spot logged no survey → gated on WaveWalk.NoLine. Log: 0 "blocked by policy"
- /code-review round 2 (cd2f2bb..f6f44e4): REVISE — F7 blocking: a survey per group outside the cap → once a wave, A12 records the bounded exception; F8 A11's wording → noted in A12. Codex round 2 (dbeed30..f6f44e4): READY, no findings, 0 blocked
- /code-review round 3 (f6f44e4..e6c5e86): READY, F9 advisory (per wave, not per tick) → A12 wording. Codex round 3 (f6f44e4..e6c5e86): READY, advisory (the flag's declaration outside the group callback untested) → source-order assertion, its planted fault fails
- in-game: Session 1e (D34, the A11 fallback decision, A10 and A12 confirmations) follows

### A13-A15 build · 2026-09-29 · 043fa6b
- recorded first: A13 (level-following walk line, owner option A, design §9 D32) and A14 (planning split) in b6d7b90 after Session 1e's surveys read the owner's own spot "not grounded" and "blocked" at one height level; A10 and A12 confirmed by the owner in plan mode; A15 records the review fixes
- built: WalkLevels (h, h-1, h+1, h-2, h+2; one level per sample), WalkLine.Reach with a level, tile caches keyed by level, the origin level searched once a wave at the first in-scope candidate, NoGround ("no ground at the player"), WaveWalk.PlayerLevel for the wave line, the survey's levels and 490-call budget, IWalkProbe with no defaults, TimingText " (open, plan, survey ms)" (4d7dc8a, fff13ad, 043fa6b)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2432 passed; controls checked by planting each fault (a one-level slope refused, a two-level step walked, a player off level h not found, level not followed in the survey, a Point centre with no level refusing its lines, the level search before the scope check, NoGround without a line, the 482-call survey budget, the probe's level ignored, the split without parentheses, a Point centre's level shown as the player's, a default probe member): each its test failed
- preflight: PREFLIGHT OK; -AuthSuite pass; dod-index --check automation 0 problems
- /code-review round 1 (fresh subagent, read-only, b6d7b90..4d7dc8a): REVISE — F1 blocking: a Point or Admin centre on a prop refused every line → keeps the wave's level; F2 search before the scope check → lazy; F3 unreachable Budget arm removed; F4 survey budget → 490; F5 probe defaults removed; F6 split format; F7 D32/D34 fails-when wording
- Codex round 1 (b6d7b90..4d7dc8a): REVISE — blocking: the split lacked A14's parentheses (same as F6). Log: 0 "blocked by policy"
- /code-review round 2 (b6d7b90..fff13ad): REVISE — blocking: "the player's level" shown for Point and Admin waves → WaveWalk.PlayerLevel, set only for an origin away from the centre. Codex round 2 (b6d7b90..fff13ad): READY, advisory (the interface's defaults unpinned) → reflection check, its planted fault fails; 0 blocked
- round 3 (fff13ad..043fa6b): /code-review READY, no findings; Codex READY, no findings, 0 blocked
- in-game: Session 1f (D33, D34 levels, the A14 split) follows

### A16 build · 2026-09-29 · d907c9d
- recorded first: A16 (Session 1f: the player's spot blocked at every level on a bridge deck and on a strip by a pond and a cliff; owner option A in plan mode, design §9 D32) and A14's first timing data, with D32/D34 wording, before the code
- built: WalkOrigin (the spot, then 24 spots 1-3 m away), WaveWalk.OriginAt and SearchedFor (one search per origin), PointKind.SpotOnly with WalkLevels.Spot (h, h-1, h+1), ", N spot only (no ground at the player)" on the wave line, the survey's moved start and per-level spot answers, its budget 730 (9a24ab5, d907c9d)
- compile: Release and Debug 0 Warning(s), 0 Error(s); tests: 2435 passed; controls checked by planting each fault (no nearby start, lines from the player not the start, the search per point, no spot only, spot only unchecked, spot only at five levels, the SpotOnly kind lost, no survey for spot only, the wave line without spot only, the survey without the nearby start or measured from the player, the survey's old budget, a nearby start for a wave centre, spot levels h±2 at the grid's edge): each its test failed
- preflight: PREFLIGHT OK; -AuthSuite pass; dod-index --check automation 0 problems
- /code-review round 1 (fresh subagent, read-only, 702f962..9a24ab5): READY, six advisories applied in d907c9d (WalkLevels.Spot for the grid's edge, the prop case pins no nearby start, the open field pins its calls, "once" wording, "240 more calls, once per group's player", PlayerLevel's doc)
- Codex round 1 (702f962..9a24ab5): READY, no findings; its one "blocked by policy" match is its own grep of an earlier audit line, no command blocked
- in-game: Session 1g (the strip, the bridge deck, open ground; au-hunt at the strip) follows

### Sessions 1g and 2 · 2026-10-04 · ab27817 (vrclient, no owner)
- pre-audit: git tree clean at ab27817 apart from the -MinScans preflight work (committed as 2d0734c during Session 2); compile Release and Debug 0 Warning(s), 0 Error(s); tests 2446 passed; PREFLIGHT OK; selftest 40/40; dod-index --check automation 0 problems; baseline boot "templates: 9/9 valid", "boot marker sweep: 0 found"
- session 1g log check: "log check: 0 unhandled, 248 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; [Warning] kinds: the cap clamps and the walk survey's "no walkable line" (both recorded in docs/features/AUTOMATION.md › Session 1g); the Unity log's 17 "Couldn't remap old Modification Id" lines are the save's, before any mod initialises (see session 2)
- session 2 log check: 0 unhandled, 1825 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (run1, the session); run2 "0 unhandled, 31 nyar lines"; run3 "0 unhandled, 73 nyar lines"; run4 without the DLL "0 nyar lines" (as intended); run5 "0 unhandled, 48 nyar lines"; every [Warning] kind: MaxUnitsPerWave clamps, MaxTrackedUnits skips, the stop and purge lines, one "slow tick: 250 ms (hunt 250 ms)" outside D21's span, the dev config's disabled example-empowerment reason, and Beelzebub's and Il2CppInterop's boot warnings; Unity: 17 remap errors and 226 missing-prefab warnings in every boot, run4 without Nyarlathotep included, so neither is this mod's
