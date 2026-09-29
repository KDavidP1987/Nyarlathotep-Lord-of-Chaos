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
- session 1 log check: boot 1 "log check: 0 unhandled, 833 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; boot 2 "log check: 0 unhandled, 34 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; every [Warning] kind listed in docs/features/AUTOMATION.md › Session 1; no [Error]
- session 1 snapshot: "snapshot restored; hashes equal (au1, …)"
